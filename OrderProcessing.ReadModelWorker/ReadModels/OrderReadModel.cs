using MongoDB.Bson.Serialization.Attributes;

namespace OrderProcessing.ReadModelWorker.ReadModels;

public sealed class OrderReadModel
{
    [BsonId]
    public int OrderId { get; set; }

    public int CustomerId { get; set; }

    public required string CustomerName { get; set; }

    public required string Status { get; set; }

    public decimal TotalAmount { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public List<OrderItemReadModel> Items { get; set; } = [];

    public DateTime LastUpdatedAtUtc { get; set; }
}