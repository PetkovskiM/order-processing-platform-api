using OrderProcessing.Contracts.Orders;

namespace OrderProcessing.Api.Services.Messaging;

public static class RabbitMqTopology
{
    public const string OrderCreatedRoutingKey = OrderEventRoutingKeys.Created;

    public const string OrderCompletedRoutingKey = OrderEventRoutingKeys.Completed;

    public const string OrderCancelledRoutingKey = OrderEventRoutingKeys.Cancelled;

    public static string GetRoutingKey(string eventType)
    {
        return eventType switch
        {
            var type when type == typeof(OrderCreatedIntegrationEvent).FullName => OrderCreatedRoutingKey,

            var type when type == typeof(OrderCompletedIntegrationEvent).FullName => OrderCompletedRoutingKey,

            var type when type == typeof(OrderCancelledIntegrationEvent).FullName => OrderCancelledRoutingKey,

            _ => throw new InvalidOperationException(
                $"No RabbitMQ routing key is configured " +
                $"for integration event type '{eventType}'.")
        };
    }
}