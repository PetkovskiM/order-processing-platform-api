using Microsoft.Extensions.Options;
using MongoDB.Driver;
using OrderProcessing.Api.Configuration;
using OrderProcessing.Api.DTOs.Orders;
using OrderProcessing.ReadModels.Orders;
using SortDirection = OrderProcessing.Api.DTOs.Orders.SortDirection;

namespace OrderProcessing.Api.Features.Orders.Queries.ReadModel;

public sealed class MongoOrderReadModelReader : IOrderReadModelReader
{
    private readonly IMongoCollection<OrderReadModel> _orders;

    public MongoOrderReadModelReader(IMongoClient mongoClient, IOptions<MongoDbOptions> options)
    {
        var mongoOptions = options.Value;

        var database = mongoClient.GetDatabase(
            mongoOptions.DatabaseName);

        _orders = database.GetCollection<OrderReadModel>(
            mongoOptions.OrdersCollectionName);
    }

    public Task<OrderReadModel?> GetByIdAsync(int orderId, CancellationToken cancellationToken)
    {
        return _orders.Find(order => order.OrderId == orderId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<OrderReadModelPage> GetPageAsync(OrderQueryParameters parameters, CancellationToken cancellationToken)
    {
        var filter = BuildFilter(parameters);
        var sort = BuildSort(parameters);

        var totalCount = await _orders.CountDocumentsAsync(
            filter,
            cancellationToken: cancellationToken);

        var skip = (parameters.Page - 1) * parameters.PageSize;

        var orders = await _orders
            .Find(filter)
            .Sort(sort)
            .Skip(skip)
            .Limit(parameters.PageSize)
            .ToListAsync(cancellationToken);

        return new OrderReadModelPage(orders, checked((int)totalCount));
    }

    private static FilterDefinition<OrderReadModel> BuildFilter(OrderQueryParameters parameters)
    {
        var builder = Builders<OrderReadModel>.Filter;
        var filters = new List<FilterDefinition<OrderReadModel>>();

        if (parameters.CustomerId.HasValue)
        {
            filters.Add(builder.Eq(
                order => order.CustomerId,
                parameters.CustomerId.Value));
        }

        if (parameters.Status.HasValue)
        {
            filters.Add(builder.Eq(
                order => order.Status,
                parameters.Status.Value.ToString()));
        }

        if (parameters.CreatedFromUtc.HasValue)
        {
            filters.Add(builder.Gte(
                order => order.CreatedAtUtc,
                parameters.CreatedFromUtc.Value));
        }

        if (parameters.CreatedToUtc.HasValue)
        {
            filters.Add(builder.Lte(
                order => order.CreatedAtUtc,
                parameters.CreatedToUtc.Value));
        }

        return filters.Count == 0
            ? builder.Empty
            : builder.And(filters);
    }

    private static SortDefinition<OrderReadModel> BuildSort(OrderQueryParameters parameters)
    {
        var builder = Builders<OrderReadModel>.Sort;

        return (parameters.SortBy, parameters.SortDirection) switch
        {
            (OrderSortBy.Id, SortDirection.Ascending) => builder.Ascending(order => order.OrderId),

            (OrderSortBy.Id, SortDirection.Descending) => builder.Descending(order => order.OrderId),

            (OrderSortBy.TotalAmount, SortDirection.Ascending) =>
                builder.Combine(
                    builder.Ascending(order => order.TotalAmount),
                    builder.Ascending(order => order.OrderId)),

            (OrderSortBy.TotalAmount, SortDirection.Descending) =>
                builder.Combine(
                    builder.Descending(order => order.TotalAmount),
                    builder.Descending(order => order.OrderId)),

            (OrderSortBy.CreatedAtUtc, SortDirection.Ascending) =>
                builder.Combine(
                    builder.Ascending(order => order.CreatedAtUtc),
                    builder.Ascending(order => order.OrderId)),

            _ =>
                builder.Combine(
                    builder.Descending(order => order.CreatedAtUtc),
                    builder.Descending(order => order.OrderId))
        };
    }
}