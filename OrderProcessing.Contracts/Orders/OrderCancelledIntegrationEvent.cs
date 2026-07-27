namespace OrderProcessing.Contracts.Orders;

public sealed record OrderCancelledIntegrationEvent(
    Guid MessageId,
    DateTime OccurredAtUtc,
    int OrderId,
    int CustomerId,
    string CustomerName,
    string CustomerEmail,
    decimal TotalAmount,
    DateTime CancelledAtUtc)
    : IIntegrationEvent;