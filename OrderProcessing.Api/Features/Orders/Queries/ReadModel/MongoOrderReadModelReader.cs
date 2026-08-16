using Microsoft.Extensions.Options;
using MongoDB.Driver;
using OrderProcessing.Api.Configuration;
using OrderProcessing.ReadModels.Orders;

namespace OrderProcessing.Api.Features.Orders.Queries.ReadModel;

public sealed class MongoOrderReadModelReader : IOrderReadModelReader
{
    private readonly IMongoCollection<OrderReadModel> _orders;

    public MongoOrderReadModelReader(IMongoClient mongoClient, IOptions<MongoDbOptions> options)
    {
        var mongoOptions = options.Value;

        var database = mongoClient.GetDatabase(mongoOptions.DatabaseName);

        _orders = database.GetCollection<OrderReadModel>(mongoOptions.OrdersCollectionName);
    }

    public Task<OrderReadModel?> GetByIdAsync(int orderId, CancellationToken cancellationToken)
    {
        return _orders.Find(order => order.OrderId == orderId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}