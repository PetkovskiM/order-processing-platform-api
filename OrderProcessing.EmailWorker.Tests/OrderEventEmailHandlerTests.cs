using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OrderProcessing.Contracts.Orders;
using OrderProcessing.EmailWorker.Emailing;
using OrderProcessing.EmailWorker.Messaging;
using OrderProcessing.EmailWorker.Persistence;

namespace OrderProcessing.EmailWorker.Tests;

public sealed class IdempotentEmailMessageProcessorTests
{
    [Fact]
    public async Task ProcessAsync_WhenMessageIsDeliveredTwice_SendsEmailOnce()
    {
        // Arrange
        await using var connection =
            new SqliteConnection("Data Source=:memory:");

        await connection.OpenAsync();

        var options =
            new DbContextOptionsBuilder<
                EmailWorkerDbContext>()
                .UseSqlite(connection)
                .Options;

        await using var dbContext =
            new EmailWorkerDbContext(options);

        await dbContext.Database.EnsureCreatedAsync();

        var emailSender = new TestEmailSender();

        var eventHandler =
            new OrderEventEmailHandler(
                emailSender,
                NullLogger<OrderEventEmailHandler>.Instance);

        var processor =
            new IdempotentEmailMessageProcessor(
                dbContext,
                eventHandler,
                NullLogger<
                    IdempotentEmailMessageProcessor>.Instance);

        var messageId = Guid.NewGuid();

        var integrationEvent =
            new OrderCreatedIntegrationEvent(
                MessageId: messageId,
                OccurredAtUtc: DateTime.UtcNow,
                OrderId: 123,
                CustomerId: 456,
                CustomerName: "Test Customer",
                CustomerEmail: "test@example.com",
                TotalAmount: 100m,
                CreatedAtUtc: DateTime.UtcNow,
                Items: []);

        var body =
            JsonSerializer.SerializeToUtf8Bytes(
                integrationEvent,
                new JsonSerializerOptions(
                    JsonSerializerDefaults.Web));

        var eventType =
            typeof(OrderCreatedIntegrationEvent)
                .FullName!;

        // Act
        await processor.ProcessAsync(
            messageId,
            eventType,
            body,
            CancellationToken.None);

        await processor.ProcessAsync(
            messageId,
            eventType,
            body,
            CancellationToken.None);

        // Assert
        Assert.Single(emailSender.Messages);

        var processedMessages =
            await dbContext.ProcessedMessages
                .ToListAsync();

        var processed =
            Assert.Single(processedMessages);

        Assert.Equal(
            messageId,
            processed.MessageId);
    }

    [Fact]
    public async Task ProcessAsync_WhenEmailSendingFails_DoesNotRecordMessage()
    {
        await using var connection =
            new SqliteConnection("Data Source=:memory:");

        await connection.OpenAsync();

        var options =
            new DbContextOptionsBuilder<
                EmailWorkerDbContext>()
                .UseSqlite(connection)
                .Options;

        await using var dbContext =
            new EmailWorkerDbContext(options);

        await dbContext.Database.EnsureCreatedAsync();

        var eventHandler =
            new OrderEventEmailHandler(
                new FailingEmailSender(),
                NullLogger<OrderEventEmailHandler>.Instance);

        var processor =
            new IdempotentEmailMessageProcessor(
                dbContext,
                eventHandler,
                NullLogger<
                    IdempotentEmailMessageProcessor>.Instance);

        var messageId = Guid.NewGuid();

        var integrationEvent =
            new OrderCompletedIntegrationEvent(
                MessageId: messageId,
                OccurredAtUtc: DateTime.UtcNow,
                OrderId: 123,
                CustomerId: 456,
                CustomerName: "Test Customer",
                CustomerEmail: "test@example.com",
                TotalAmount: 100m,
                CompletedAtUtc: DateTime.UtcNow);

        var body =
            JsonSerializer.SerializeToUtf8Bytes(
                integrationEvent,
                new JsonSerializerOptions(
                    JsonSerializerDefaults.Web));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => processor.ProcessAsync(
                messageId,
                typeof(OrderCompletedIntegrationEvent)
                    .FullName!,
                body,
                CancellationToken.None));

        Assert.Empty(
            await dbContext.ProcessedMessages.ToListAsync());
    }

    private sealed class TestEmailSender : IEmailSender
    {
        public List<EmailMessage> Messages { get; } = [];

        public Task SendAsync(
            EmailMessage message,
            CancellationToken cancellationToken)
        {
            Messages.Add(message);

            return Task.CompletedTask;
        }
    }

    private sealed class FailingEmailSender : IEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException( "Simulated email failure.");
        }
    }
}