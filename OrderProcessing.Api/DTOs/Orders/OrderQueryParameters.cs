using System.ComponentModel.DataAnnotations;
using OrderProcessing.Api.Entities;

namespace OrderProcessing.Api.DTOs.Orders;

public sealed class OrderQueryParameters
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 10;

    [Range(1, int.MaxValue)]
    public int? CustomerId { get; set; }

    public OrderStatus? Status { get; set; }

    public DateTime? CreatedFromUtc { get; set; }

    public DateTime? CreatedToUtc { get; set; }

    public OrderSortBy SortBy { get; set; } = OrderSortBy.CreatedAtUtc;

    public SortDirection SortDirection { get; set; } = SortDirection.Descending;
}