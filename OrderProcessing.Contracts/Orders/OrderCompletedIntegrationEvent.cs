namespace OrderProcessing.Contracts.Orders;

public sealed record OrderCompletedIntegrationEvent(
    Guid MessageId,
    DateTime OccurredAtUtc,
    int OrderId,
    int CustomerId,
    string CustomerName,
    string CustomerEmail,
    decimal TotalAmount,
    DateTime CompletedAtUtc)
    : IIntegrationEvent;