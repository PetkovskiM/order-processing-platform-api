using MediatR;
using Microsoft.EntityFrameworkCore;
using OrderProcessing.Api.Data;
using OrderProcessing.Api.DTOs.Common;
using OrderProcessing.Api.DTOs.Orders;
using OrderProcessing.Api.Entities;
using OrderProcessing.Api.Exceptions;

namespace OrderProcessing.Api.Features.Orders.Queries.GetOrders;

public sealed class GetOrdersQueryHandler
    : IRequestHandler<GetOrdersQuery, PagedResponse<OrderResponse>>
{
    private readonly OrderProcessingDbContext _dbContext;

    public GetOrdersQueryHandler(
        OrderProcessingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResponse<OrderResponse>> Handle(
        GetOrdersQuery request,
        CancellationToken cancellationToken)
    {
        var parameters = request.Parameters;

        ValidateDateRange(parameters);

        var query = _dbContext.Orders
            .AsNoTracking()
            .AsQueryable();

        query = ApplyFilters(query, parameters);

        var totalCount = await query.CountAsync(
            cancellationToken);

        query = ApplySorting(query, parameters);

        var orders = await query
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .Select(order => new OrderResponse
            {
                Id = order.Id,
                CustomerId = order.CustomerId,
                CustomerName =
                    order.Customer.FirstName + " " +
                    order.Customer.LastName,
                Status = order.Status,
                TotalAmount = order.TotalAmount,
                CreatedAtUtc = order.CreatedAtUtc,
                CompletedAtUtc = order.CompletedAtUtc,
                CancelledAtUtc = order.CancelledAtUtc,
                Items = order.Items
                    .OrderBy(item => item.Id)
                    .Select(item => new OrderItemResponse
                    {
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        LineTotal = item.LineTotal
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return new PagedResponse<OrderResponse>
        {
            Items = orders,
            Page = parameters.Page,
            PageSize = parameters.PageSize,
            TotalCount = totalCount
        };
    }

    private static IQueryable<Order> ApplyFilters(
        IQueryable<Order> query,
        OrderQueryParameters parameters)
    {
        if (parameters.CustomerId.HasValue)
        {
            query = query.Where(order =>
                order.CustomerId == parameters.CustomerId.Value);
        }

        if (parameters.Status.HasValue)
        {
            query = query.Where(order =>
                order.Status == parameters.Status.Value);
        }

        if (parameters.CreatedFromUtc.HasValue)
        {
            query = query.Where(order =>
                order.CreatedAtUtc >= parameters.CreatedFromUtc.Value);
        }

        if (parameters.CreatedToUtc.HasValue)
        {
            query = query.Where(order =>
                order.CreatedAtUtc <= parameters.CreatedToUtc.Value);
        }

        return query;
    }

    private static IQueryable<Order> ApplySorting(
        IQueryable<Order> query,
        OrderQueryParameters parameters)
    {
        return (parameters.SortBy, parameters.SortDirection) switch
        {
            (OrderSortBy.Id, SortDirection.Ascending) =>
                 query.OrderBy(order => order.Id),

            (OrderSortBy.Id, SortDirection.Descending) =>
                query.OrderByDescending(order => order.Id),

            (OrderSortBy.TotalAmount, SortDirection.Ascending) =>
                query
                    .OrderBy(order => order.TotalAmount)
                    .ThenBy(order => order.Id),

            (OrderSortBy.TotalAmount, SortDirection.Descending) =>
                query
                    .OrderByDescending(order => order.TotalAmount)
                    .ThenByDescending(order => order.Id),

            (OrderSortBy.CreatedAtUtc, SortDirection.Ascending) =>
                query
                    .OrderBy(order => order.CreatedAtUtc)
                    .ThenBy(order => order.Id),

            _ =>
                query
                    .OrderByDescending(order => order.CreatedAtUtc)
                    .ThenByDescending(order => order.Id)
        };
    }

    private static void ValidateDateRange(
        OrderQueryParameters parameters)
    {
        if (parameters.CreatedFromUtc.HasValue &&
           parameters.CreatedToUtc.HasValue &&
           parameters.CreatedFromUtc > parameters.CreatedToUtc)
        {
            throw new BadRequestException(
                "CreatedFromUtc cannot be later than CreatedToUtc.",
                ErrorCodes.InvalidDateRange);
        }
    }
}