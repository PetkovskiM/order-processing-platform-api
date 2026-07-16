using OrderProcessing.Api.Validation;
using System.ComponentModel.DataAnnotations;

namespace OrderProcessing.Api.DTOs.Customers;

public class CreateCustomerRequest
{
    [Required]
    [NotWhiteSpace]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [NotWhiteSpace]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [NotWhiteSpace]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? PhoneNumber { get; set; }
}