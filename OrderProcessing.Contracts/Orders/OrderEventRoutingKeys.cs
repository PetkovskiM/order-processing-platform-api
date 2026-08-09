namespace OrderProcessing.Contracts.Orders;

public static class OrderEventRoutingKeys
{
    public const string Created = "order.created";

    public const string Completed = "order.completed";

    public const string Cancelled = "order.cancelled";
}