using OrderProcessing.Contracts.Orders;

namespace OrderProcessing.Api.Services.Messaging;

public static class RabbitMqTopology
{
    public const string OrderCreatedRoutingKey = "order.created";

    public const string OrderCompletedRoutingKey = "order.completed";

    public const string OrderCancelledRoutingKey = "order.cancelled";

    public static string GetRoutingKey(string eventType)
    {
        return eventType switch
        {
            var type when type ==
                typeof(OrderCreatedIntegrationEvent).FullName =>
                    OrderCreatedRoutingKey,

            var type when type ==
                typeof(OrderCompletedIntegrationEvent).FullName =>
                    OrderCompletedRoutingKey,

            var type when type ==
                typeof(OrderCancelledIntegrationEvent).FullName =>
                    OrderCancelledRoutingKey,

            _ => throw new InvalidOperationException(
                $"No RabbitMQ routing key is configured " +
                $"for integration event type '{eventType}'.")
        };
    }
}