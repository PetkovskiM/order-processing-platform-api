using OrderProcessing.Api.DTOs.Orders;
using OrderProcessing.Api.Features.Orders.Queries.ReadModel;
using OrderProcessing.ReadModels.Orders;

namespace OrderProcessing.Api.Tests.Infrastructure;

public sealed class TestOrderReadModelReader : IOrderReadModelReader
{
    public const int ExistingOrderId = 88001;

    public Task<OrderReadModel?> GetByIdAsync(int orderId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var order = orderId == ExistingOrderId ? CreateExistingOrder() : null;

        return Task.FromResult(order);
    }

    public Task<OrderReadModelPage> GetPageAsync(OrderQueryParameters parameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<OrderReadModel> query =
        [
            CreateExistingOrder()
        ];

        query = ApplyFilters(query, parameters);

        var totalCount = query.Count();

        query = ApplySorting(query, parameters);

        var orders = query
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToList();

        return Task.FromResult(
            new OrderReadModelPage(orders, totalCount));
    }

    private static IEnumerable<OrderReadModel> ApplyFilters(IEnumerable<OrderReadModel> query, OrderQueryParameters parameters)
    {
        if (parameters.CustomerId.HasValue)
        {
            query = query.Where(order => order.CustomerId == parameters.CustomerId.Value);
        }

        if (parameters.Status.HasValue)
        {
            var status = parameters.Status.Value.ToString();

            query = query.Where(order =>
                string.Equals(
                    order.Status,
                    status,
                    StringComparison.OrdinalIgnoreCase));
        }

        if (parameters.CreatedFromUtc.HasValue)
        {
            query = query.Where(order => order.CreatedAtUtc >= parameters.CreatedFromUtc.Value);
        }

        if (parameters.CreatedToUtc.HasValue)
        {
            query = query.Where(order => order.CreatedAtUtc <= parameters.CreatedToUtc.Value);
        }

        return query;
    }

    private static IEnumerable<OrderReadModel> ApplySorting(IEnumerable<OrderReadModel> query, OrderQueryParameters parameters)
    {
        return (parameters.SortBy, parameters.SortDirection) switch
        {
            (OrderSortBy.Id, SortDirection.Ascending) =>
                query.OrderBy(order => order.OrderId),

            (OrderSortBy.Id, SortDirection.Descending) =>
                query.OrderByDescending(order => order.OrderId),

            (OrderSortBy.TotalAmount, SortDirection.Ascending) =>
                query
                    .OrderBy(order => order.TotalAmount)
                    .ThenBy(order => order.OrderId),

            (OrderSortBy.TotalAmount, SortDirection.Descending) =>
                query
                    .OrderByDescending(order => order.TotalAmount)
                    .ThenByDescending(order => order.OrderId),

            (OrderSortBy.CreatedAtUtc, SortDirection.Ascending) =>
                query
                    .OrderBy(order => order.CreatedAtUtc)
                    .ThenBy(order => order.OrderId),

            _ =>
                query
                    .OrderByDescending(order => order.CreatedAtUtc)
                    .ThenByDescending(order => order.OrderId)
        };
    }

    private static OrderReadModel CreateExistingOrder()
    {
        var occurredAtUtc = new DateTime(
            2026,
            8,
            1,
            10,
            0,
            0,
            DateTimeKind.Utc);

        return new OrderReadModel
        {
            OrderId = ExistingOrderId,
            CustomerId = TestDataSeeder.CustomerId,
            CustomerName = "Integration Test Customer",
            Status = "Pending",
            TotalAmount = 24.99m,
            CreatedAtUtc = occurredAtUtc,
            LastUpdatedAtUtc = occurredAtUtc,
            Items =
            [
                new OrderItemReadModel
                {
                    ProductId = TestDataSeeder.ProductId,
                    ProductName = "Integration Test Product",
                    Quantity = 1,
                    UnitPrice = 24.99m,
                    LineTotal = 24.99m
                }
            ]
        };
    }
}