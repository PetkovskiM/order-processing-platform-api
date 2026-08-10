namespace OrderProcessing.EmailWorker.Persistence.Entities;

public sealed class ProcessedMessage
{
    public Guid MessageId { get; set; }

    public required string EventType { get; set; }

    public DateTime ProcessedAtUtc { get; set; }
}