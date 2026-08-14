using OrderProcessing.ReadModelWorker.ReadModels;

namespace OrderProcessing.ReadModelWorker.Persistence;

public interface IOrderReadModelRepository
{
    Task CreateIfMissingAsync(OrderReadModel order, CancellationToken cancellationToken);

    Task MarkCompletedAsync(
        int orderId,
        DateTime completedAtUtc,
        DateTime eventOccurredAtUtc,
        CancellationToken cancellationToken);

    Task MarkCancelledAsync(
        int orderId,
        DateTime cancelledAtUtc,
        DateTime eventOccurredAtUtc,
        CancellationToken cancellationToken);
}