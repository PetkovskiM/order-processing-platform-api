using System.ComponentModel.DataAnnotations;

namespace OrderProcessing.Api.DTOs.Orders;

public class CreateOrderItemRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "ProductId must be greater than 0.")]
    public int ProductId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be greater than 0.")]
    public int Quantity { get; set; }
}