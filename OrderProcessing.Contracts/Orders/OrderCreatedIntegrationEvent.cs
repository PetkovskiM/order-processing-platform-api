namespace OrderProcessing.Contracts.Orders;

public sealed record OrderCreatedIntegrationEvent(
    Guid MessageId,
    DateTime OccurredAtUtc,
    int OrderId,
    int CustomerId,
    string CustomerName,
    string CustomerEmail,
    decimal TotalAmount,
    DateTime CreatedAtUtc,
    IReadOnlyCollection<OrderItemIntegrationModel> Items)
    : IIntegrationEvent;