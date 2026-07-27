namespace OrderProcessing.Contracts;

public interface IIntegrationEvent
{
    Guid MessageId { get; }

    DateTime OccurredAtUtc { get; }
}