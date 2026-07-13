using System.ComponentModel.DataAnnotations;

namespace OrderProcessing.Api.DTOs.Orders;

public class CreateOrderItemRequest
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}