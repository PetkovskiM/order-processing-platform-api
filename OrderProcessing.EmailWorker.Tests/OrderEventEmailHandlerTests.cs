using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using OrderProcessing.Contracts.Orders;
using OrderProcessing.EmailWorker.Emailing;
using OrderProcessing.EmailWorker.Messaging;

namespace OrderProcessing.EmailWorker.Tests;

public sealed class OrderEventEmailHandlerTests
{
    [Fact]
    public async Task HandleAsync_ForOrderCreatedEvent_SendsCreatedEmail()
    {
        // Arrange
        var sender = new TestEmailSender();

        var handler = new OrderEventEmailHandler(
            sender,
            NullLogger<OrderEventEmailHandler>.Instance);

        var integrationEvent =
            new OrderCreatedIntegrationEvent(
                MessageId: Guid.NewGuid(),
                OccurredAtUtc: DateTime.UtcNow,
                OrderId: 1050,
                CustomerId: 1001,
                CustomerName: "John Smith",
                CustomerEmail: "john.smith@example.com",
                TotalAmount: 99.99m,
                CreatedAtUtc: DateTime.UtcNow,
                Items:
                [
                    new OrderItemIntegrationModel(
                        ProductId: 2001,
                        ProductName: "Keyboard",
                        Quantity: 1,
                        UnitPrice: 99.99m,
                        LineTotal: 99.99m)
                ]);

        var body = JsonSerializer.SerializeToUtf8Bytes(
            integrationEvent,
            new JsonSerializerOptions(
                JsonSerializerDefaults.Web));

        // Act
        await handler.HandleAsync(
            typeof(OrderCreatedIntegrationEvent).FullName!,
            body,
            CancellationToken.None);

        // Assert
        var email = Assert.Single(sender.Messages);

        Assert.Equal(
            "john.smith@example.com",
            email.Recipient);

        Assert.Contains(
            "1050",
            email.Subject);

        Assert.Contains(
            "created",
            email.Subject,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_ForUnknownEvent_ThrowsException()
    {
        // Arrange
        var handler = new OrderEventEmailHandler(
            new TestEmailSender(),
            NullLogger<OrderEventEmailHandler>.Instance);

        // Act
        var action = () => handler.HandleAsync(
            "UnknownIntegrationEvent",
            [],
            CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<
            UnsupportedIntegrationEventException>(
                action);
    }

    private sealed class TestEmailSender : IEmailSender
    {
        public List<EmailMessage> Messages { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);

            return Task.CompletedTask;
        }
    }
}