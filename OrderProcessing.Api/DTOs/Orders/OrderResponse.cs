using OrderProcessing.Api.Entities;

namespace OrderProcessing.Api.DTOs.Orders;

public class OrderResponse
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public OrderStatus Status { get; set; }

    public decimal TotalAmount { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public IReadOnlyList<OrderItemResponse> Items { get; set; } = [];
}