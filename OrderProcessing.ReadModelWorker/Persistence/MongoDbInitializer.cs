using MongoDB.Driver;
using OrderProcessing.ReadModels.Orders;

namespace OrderProcessing.ReadModelWorker.Persistence;

public sealed class MongoDbInitializer : IHostedService
{
    private readonly OrderReadModelStore _store;
    private readonly ILogger<MongoDbInitializer> _logger;

    public MongoDbInitializer(OrderReadModelStore store, ILogger<MongoDbInitializer> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var indexes = new[]
            {
                new CreateIndexModel<OrderReadModel>(
                    Builders<OrderReadModel>
                        .IndexKeys
                        .Descending(order => order.CreatedAtUtc)),

                new CreateIndexModel<OrderReadModel>(
                    Builders<OrderReadModel>
                        .IndexKeys
                        .Ascending(order => order.CustomerId)
                        .Descending(order => order.CreatedAtUtc)),

                new CreateIndexModel<OrderReadModel>(
                    Builders<OrderReadModel>
                        .IndexKeys
                        .Ascending(order => order.Status)
                        .Descending(order => order.CreatedAtUtc))
            };

        await _store.Orders.Indexes.CreateManyAsync(indexes, cancellationToken);

        _logger.LogInformation("MongoDB order read-model indexes ensured");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}