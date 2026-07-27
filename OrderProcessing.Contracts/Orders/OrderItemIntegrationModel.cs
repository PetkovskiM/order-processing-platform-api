namespace OrderProcessing.Contracts.Orders;

public sealed record OrderItemIntegrationModel(
    int ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);