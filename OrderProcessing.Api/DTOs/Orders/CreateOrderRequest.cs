using System.ComponentModel.DataAnnotations;

namespace OrderProcessing.Api.DTOs.Orders;

public class CreateOrderRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "CustomerId must be greater than 0.")]
    public int CustomerId { get; set; }

    [Required(ErrorMessage = "Order items are required.")]
    [MinLength(1, ErrorMessage = "An order must contain at least one item.")]
    public List<CreateOrderItemRequest> Items { get; set; } = [];
}