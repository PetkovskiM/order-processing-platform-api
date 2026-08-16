using Microsoft.Extensions.Options;
using MongoDB.Driver;
using OrderProcessing.ReadModels.Orders;
using OrderProcessing.ReadModelWorker.Configuration;

namespace OrderProcessing.ReadModelWorker.Persistence;

public sealed class OrderReadModelStore
{
    public IMongoCollection<OrderReadModel> Orders { get; }

    public OrderReadModelStore(IMongoClient mongoClient, IOptions<MongoDbOptions> options)
    {
        var mongoOptions = options.Value;

        var database = mongoClient.GetDatabase(mongoOptions.DatabaseName);

        Orders = database.GetCollection<OrderReadModel>(mongoOptions.OrdersCollectionName);
    }
}