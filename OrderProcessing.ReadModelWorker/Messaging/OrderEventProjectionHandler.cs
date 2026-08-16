using System.Text.Json;
using OrderProcessing.Contracts.Orders;
using OrderProcessing.ReadModels.Orders;
using OrderProcessing.ReadModelWorker.Persistence;

namespace OrderProcessing.ReadModelWorker.Messaging;

public sealed class OrderEventProjectionHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IOrderReadModelRepository _repository;
    private readonly ILogger<OrderEventProjectionHandler> _logger;

    public OrderEventProjectionHandler(IOrderReadModelRepository repository, ILogger<OrderEventProjectionHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task HandleAsync(string eventType, byte[] body, CancellationToken cancellationToken)
    {
        if (eventType == typeof(OrderCreatedIntegrationEvent).FullName)
        {
            var integrationEvent = Deserialize<OrderCreatedIntegrationEvent>(body);

            await HandleCreatedAsync(integrationEvent, cancellationToken);

            return;
        }

        if (eventType == typeof(OrderCompletedIntegrationEvent).FullName)
        {
            var integrationEvent = Deserialize<OrderCompletedIntegrationEvent>(body);

            await _repository.MarkCompletedAsync(
                integrationEvent.OrderId,
                integrationEvent.CompletedAtUtc,
                integrationEvent.OccurredAtUtc,
                cancellationToken);

            return;
        }

        if (eventType == typeof(OrderCancelledIntegrationEvent).FullName)
        {
            var integrationEvent = Deserialize<OrderCancelledIntegrationEvent>(body);

            await _repository.MarkCancelledAsync(
                integrationEvent.OrderId,
                integrationEvent.CancelledAtUtc,
                integrationEvent.OccurredAtUtc,
                cancellationToken);

            return;
        }

        throw new InvalidOperationException($"Unsupported integration event type '{eventType}'.");
    }

    private async Task HandleCreatedAsync(OrderCreatedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        var order = new OrderReadModel
            {
                OrderId = integrationEvent.OrderId,
                CustomerId = integrationEvent.CustomerId,
                CustomerName =
                    integrationEvent.CustomerName,
                Status = "Pending",
                TotalAmount =
                    integrationEvent.TotalAmount,
                CreatedAtUtc =
                    integrationEvent.CreatedAtUtc,
                CompletedAtUtc = null,
                CancelledAtUtc = null,
                LastUpdatedAtUtc =
                    integrationEvent.OccurredAtUtc,

                Items = integrationEvent.Items
                    .Select(item =>
                        new OrderItemReadModel
                        {
                            ProductId = item.ProductId,
                            ProductName = item.ProductName,
                            Quantity = item.Quantity,
                            UnitPrice = item.UnitPrice,
                            LineTotal = item.LineTotal
                        }).ToList()
            };

        await _repository.CreateIfMissingAsync(order, cancellationToken);

        _logger.LogInformation(
            "Projected order-created event into MongoDB " +
            "for order {OrderId}",
            integrationEvent.OrderId);
    }

    private static TEvent Deserialize<TEvent>(byte[] body)
    {
        return JsonSerializer.Deserialize<TEvent>(
            body,
            SerializerOptions)
            ?? throw new JsonException(
                $"Could not deserialize " +
                $"{typeof(TEvent).Name}.");
    }
}