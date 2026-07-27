using System.Text.Json;
using OrderProcessing.Contracts.Orders;

namespace OrderProcessing.Api.Tests.Unit;

public sealed class IntegrationEventSerializationTests
{
    [Fact]
    public void OrderCreatedIntegrationEvent_CanBeSerializedAndDeserialized()
    {
        // Arrange
        var messageId = Guid.NewGuid();

        var integrationEvent = new OrderCreatedIntegrationEvent(
            MessageId: messageId,
            OccurredAtUtc: new DateTime(
                2026,
                7,
                26,
                10,
                0,
                0,
                DateTimeKind.Utc),
            OrderId: 1050,
            CustomerId: 1001,
            CustomerName: "John Smith",
            CustomerEmail: "john.smith@example.com",
            TotalAmount: 109.98m,
            CreatedAtUtc: new DateTime(
                2026,
                7,
                26,
                10,
                0,
                0,
                DateTimeKind.Utc),
            Items:
            [
                new OrderItemIntegrationModel(
                    ProductId: 2001,
                    ProductName: "Mechanical Keyboard",
                    Quantity: 1,
                    UnitPrice: 79.99m,
                    LineTotal: 79.99m),

                new OrderItemIntegrationModel(
                    ProductId: 2002,
                    ProductName: "Wireless Mouse",
                    Quantity: 1,
                    UnitPrice: 29.99m,
                    LineTotal: 29.99m)
            ]);

        // Act
        var json = JsonSerializer.Serialize(integrationEvent);

        var deserialized =
            JsonSerializer.Deserialize<OrderCreatedIntegrationEvent>(
                json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(messageId, deserialized.MessageId);
        Assert.Equal(1050, deserialized.OrderId);
        Assert.Equal(2, deserialized.Items.Count);
        Assert.Equal(109.98m, deserialized.TotalAmount);
        Assert.Equal(
            "john.smith@example.com",
            deserialized.CustomerEmail);
    }
}