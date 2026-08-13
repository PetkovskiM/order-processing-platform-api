namespace OrderProcessing.ReadModelWorker.ReadModels;

public sealed class OrderItemReadModel
{
    public int ProductId { get; set; }

    public required string ProductName { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }
}