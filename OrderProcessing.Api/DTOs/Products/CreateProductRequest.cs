using System.ComponentModel.DataAnnotations;

namespace OrderProcessing.Api.DTOs.Products;

public class CreateProductRequest
{
    [Required]
    [MaxLength(64)]
    public string Sku { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Range(0, 999999999)]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; set; }
}