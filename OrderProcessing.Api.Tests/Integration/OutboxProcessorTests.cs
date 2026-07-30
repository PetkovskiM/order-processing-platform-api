using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrderProcessing.Api.Data;
using OrderProcessing.Api.Services.Messaging;
using OrderProcessing.Api.Services.Outbox;
using OrderProcessing.Api.Tests.Infrastructure;

namespace OrderProcessing.Api.Tests.Integration;

public sealed class OutboxProcessorTests : IntegrationTestBase
{
    [Fact]
    public async Task ProcessPendingAsync_WhenMessageExists_MarksItProcessed()
    {
        // Arrange
        var createdOrder = await CreateTestOrderAsync();

        await using (var processingScope = Factory.Services.CreateAsyncScope())
        {
            var processor = processingScope.ServiceProvider
                .GetRequiredService<OutboxProcessor>();

            // Act
            var processedCount = await processor.ProcessPendingAsync();

            // Assert
            Assert.Equal(1, processedCount);
        }

        await using var assertionScope = Factory.Services.CreateAsyncScope();

        var dbContext = assertionScope.ServiceProvider
            .GetRequiredService<OrderProcessingDbContext>();

        var message = await dbContext.OutboxMessages
            .AsNoTracking()
            .SingleAsync();

        Assert.NotNull(message.ProcessedAtUtc);
        Assert.NotNull(message.LastAttemptAtUtc);
        Assert.Equal(0, message.RetryCount);
        Assert.Null(message.LastError);
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenNoMessagesExist_ReturnsZero()
    {
        // Arrange
        await using var scope = Factory.Services.CreateAsyncScope();

        var processor = scope.ServiceProvider
            .GetRequiredService<OutboxProcessor>();

        // Act
        var processedCount = await processor.ProcessPendingAsync();

        // Assert
        Assert.Equal(0, processedCount);
    }


    private sealed class FailingIntegrationEventPublisher : IIntegrationEventPublisher
    {
        public Task PublishAsync(IntegrationEventEnvelope message, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(
                "Simulated publisher failure.");
        }
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenPublisherFails_RecordsRetry()
    {
        // Arrange
        await CreateTestOrderAsync();

        await using (var processingScope =
            Factory.Services.CreateAsyncScope())
        {
            var dbContext = processingScope.ServiceProvider
                .GetRequiredService<OrderProcessingDbContext>();

            var processor = new OutboxProcessor(
                dbContext,
                new FailingIntegrationEventPublisher(),
                Options.Create(
                    new OutboxOptions
                    {
                        BatchSize = 20,
                        MaxRetryCount = 5
                    }),
                NullLogger<OutboxProcessor>.Instance);

            // Act
            var processedCount =
                await processor.ProcessPendingAsync();

            // Assert
            Assert.Equal(0, processedCount);
        }

        await using var assertionScope =
            Factory.Services.CreateAsyncScope();

        var assertionContext = assertionScope.ServiceProvider
            .GetRequiredService<OrderProcessingDbContext>();

        var message = await assertionContext.OutboxMessages
            .AsNoTracking()
            .SingleAsync();

        Assert.Null(message.ProcessedAtUtc);
        Assert.NotNull(message.LastAttemptAtUtc);
        Assert.Equal(1, message.RetryCount);

        Assert.Equal(
            "Simulated publisher failure.",
            message.LastError);
    }
}