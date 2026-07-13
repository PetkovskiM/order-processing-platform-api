using System.ComponentModel.DataAnnotations;

namespace OrderProcessing.Api.DTOs.Orders;

public class CreateOrderRequest
{
    [Range(1, int.MaxValue)]
    public int CustomerId { get; set; }

    [Required]
    [MinLength(1)]
    public List<CreateOrderItemRequest> Items { get; set; } = [];
}