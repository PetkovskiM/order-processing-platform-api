using MongoDB.Driver;
using OrderProcessing.ReadModelWorker.ReadModels;

namespace OrderProcessing.ReadModelWorker.Persistence;

public sealed class MongoOrderReadModelRepository : IOrderReadModelRepository
{
    private readonly OrderReadModelStore _store;
    private readonly ILogger<MongoOrderReadModelRepository> _logger;

    public MongoOrderReadModelRepository(OrderReadModelStore store, ILogger<MongoOrderReadModelRepository> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task CreateIfMissingAsync(OrderReadModel order, CancellationToken cancellationToken)
    {
        var filter = Builders<OrderReadModel>.Filter.Eq(existing => existing.OrderId, order.OrderId);

        var update = Builders<OrderReadModel>.Update
                .SetOnInsert(existing => existing.OrderId, order.OrderId)
                .SetOnInsert(existing => existing.CustomerId, order.CustomerId)
                .SetOnInsert(existing => existing.CustomerName, order.CustomerName)
                .SetOnInsert(existing => existing.Status, order.Status)
                .SetOnInsert(existing => existing.TotalAmount, order.TotalAmount)
                .SetOnInsert(existing => existing.CreatedAtUtc, order.CreatedAtUtc)
                .SetOnInsert(existing => existing.CompletedAtUtc, order.CompletedAtUtc)
                .SetOnInsert(existing => existing.CancelledAtUtc, order.CancelledAtUtc)
                .SetOnInsert(existing => existing.Items, order.Items)
                .SetOnInsert(existing => existing.LastUpdatedAtUtc, order.LastUpdatedAtUtc);

        await _store.Orders.UpdateOneAsync(filter, update,
            new UpdateOptions
            {
                IsUpsert = true
            },
            cancellationToken);
    }

    public Task MarkCompletedAsync(
        int orderId,
        DateTime completedAtUtc,
        DateTime eventOccurredAtUtc,
        CancellationToken cancellationToken)
    {
        return UpdateStatusAsync(
            orderId,
            status: "Completed",
            completedAtUtc: completedAtUtc,
            cancelledAtUtc: null,
            eventOccurredAtUtc,
            cancellationToken);
    }

    public Task MarkCancelledAsync(
        int orderId,
        DateTime cancelledAtUtc,
        DateTime eventOccurredAtUtc,
        CancellationToken cancellationToken)
    {
        return UpdateStatusAsync(
            orderId,
            status: "Cancelled",
            completedAtUtc: null,
            cancelledAtUtc: cancelledAtUtc,
            eventOccurredAtUtc,
            cancellationToken);
    }

    private async Task UpdateStatusAsync(
        int orderId,
        string status,
        DateTime? completedAtUtc,
        DateTime? cancelledAtUtc,
        DateTime eventOccurredAtUtc,
        CancellationToken cancellationToken)
    {
        var existing = await _store.Orders.Find(order => order.OrderId == orderId)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            throw new InvalidOperationException($"Order read model {orderId} does not exist yet.");
        }

        if (existing.LastUpdatedAtUtc > eventOccurredAtUtc)
        {
            _logger.LogInformation(
                "Ignoring stale read-model event for order " +
                "{OrderId}. Event occurred at {OccurredAtUtc}",
                orderId,
                eventOccurredAtUtc);

            return;
        }

        var filter = Builders<OrderReadModel>.Filter.And(
                Builders<OrderReadModel>.Filter.Eq(
                    order => order.OrderId,
                    orderId),

                Builders<OrderReadModel>.Filter.Lte(
                    order => order.LastUpdatedAtUtc,
                    eventOccurredAtUtc));

        var update = Builders<OrderReadModel>.Update
                .Set(order => order.Status, status)
                .Set(order => order.LastUpdatedAtUtc, eventOccurredAtUtc);

        if (completedAtUtc.HasValue)
        {
            update = update.Set(order => order.CompletedAtUtc, completedAtUtc.Value);
        }

        if (cancelledAtUtc.HasValue)
        {
            update = update.Set(order => order.CancelledAtUtc, cancelledAtUtc.Value);
        }

        await _store.Orders.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }
}