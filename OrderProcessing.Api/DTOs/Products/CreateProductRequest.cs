using OrderProcessing.Api.Validation;
using System.ComponentModel.DataAnnotations;

namespace OrderProcessing.Api.DTOs.Products;

public class CreateProductRequest
{
    [Required]
    [NotWhiteSpace]
    [MaxLength(64)]
    public string Sku { get; set; } = string.Empty;

    [Required]
    [NotWhiteSpace]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Range(typeof(decimal), "0.01", "999999999", ErrorMessage = "Price must be greater than 0.")]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Stock quantity cannot be negative.")]
    public int StockQuantity { get; set; }
}