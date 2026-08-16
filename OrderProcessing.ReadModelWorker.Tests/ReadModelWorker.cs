using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using OrderProcessing.Contracts.Orders;
using OrderProcessing.ReadModels.Orders;
using OrderProcessing.ReadModelWorker.Messaging;
using OrderProcessing.ReadModelWorker.Persistence;

namespace OrderProcessing.ReadModelWorker.Tests;

public sealed class OrderEventProjectionHandlerTests
{
    [Fact]
    public async Task HandleAsync_ForOrderCreatedEvent_CreatesPendingReadModel()
    {
        // Arrange
        var repository = new FakeOrderReadModelRepository();

        var handler = new OrderEventProjectionHandler(repository, NullLogger<OrderEventProjectionHandler>.Instance);

        var occurredAtUtc = DateTime.UtcNow;

        var integrationEvent =
            new OrderCreatedIntegrationEvent(
                MessageId: Guid.NewGuid(),
                OccurredAtUtc: occurredAtUtc,
                OrderId: 123,
                CustomerId: 456,
                CustomerName: "John Smith",
                CustomerEmail: "john@example.com",
                TotalAmount: 199.98m,
                CreatedAtUtc: occurredAtUtc,
                Items:
                [
                    new OrderItemIntegrationModel(
                        ProductId: 10,
                        ProductName: "Keyboard",
                        Quantity: 2,
                        UnitPrice: 99.99m,
                        LineTotal: 199.98m)
                ]);

        var body = Serialize(integrationEvent);

        // Act
        await handler.HandleAsync(
            typeof(OrderCreatedIntegrationEvent).FullName!,
            body,
            CancellationToken.None);

        // Assert
        Assert.NotNull(repository.CreatedOrder);

        Assert.Equal(
            integrationEvent.OrderId,
            repository.CreatedOrder.OrderId);

        Assert.Equal(
            integrationEvent.CustomerId,
            repository.CreatedOrder.CustomerId);

        Assert.Equal(
            integrationEvent.CustomerName,
            repository.CreatedOrder.CustomerName);

        Assert.Equal(
            "Pending",
            repository.CreatedOrder.Status);

        Assert.Equal(
            integrationEvent.TotalAmount,
            repository.CreatedOrder.TotalAmount);

        Assert.Equal(
            occurredAtUtc,
            repository.CreatedOrder.LastUpdatedAtUtc);

        var item =
            Assert.Single(repository.CreatedOrder.Items);

        Assert.Equal(10, item.ProductId);
        Assert.Equal("Keyboard", item.ProductName);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(99.99m, item.UnitPrice);
    }

    [Fact]
    public async Task HandleAsync_ForOrderCompletedEvent_MarksOrderCompleted()
    {
        // Arrange
        var repository = new FakeOrderReadModelRepository();

        var handler = new OrderEventProjectionHandler(
            repository,
            NullLogger<OrderEventProjectionHandler>.Instance);

        var occurredAtUtc = DateTime.UtcNow;
        var completedAtUtc = occurredAtUtc;

        var integrationEvent =
            new OrderCompletedIntegrationEvent(
                MessageId: Guid.NewGuid(),
                OccurredAtUtc: occurredAtUtc,
                OrderId: 123,
                CustomerId: 456,
                CustomerName: "John Smith",
                CustomerEmail: "john@example.com",
                TotalAmount: 100m,
                CompletedAtUtc: completedAtUtc);

        // Act
        await handler.HandleAsync(
            typeof(OrderCompletedIntegrationEvent).FullName!,
            Serialize(integrationEvent),
            CancellationToken.None);

        // Assert
        Assert.Equal(
            123,
            repository.CompletedOrderId);

        Assert.Equal(
            completedAtUtc,
            repository.CompletedAtUtc);

        Assert.Equal(
            occurredAtUtc,
            repository.CompletedEventOccurredAtUtc);
    }

    [Fact]
    public async Task HandleAsync_ForOrderCancelledEvent_MarksOrderCancelled()
    {
        // Arrange
        var repository = new FakeOrderReadModelRepository();

        var handler = new OrderEventProjectionHandler(
            repository,
            NullLogger<OrderEventProjectionHandler>.Instance);

        var occurredAtUtc = DateTime.UtcNow;
        var cancelledAtUtc = occurredAtUtc;

        var integrationEvent =
            new OrderCancelledIntegrationEvent(
                MessageId: Guid.NewGuid(),
                OccurredAtUtc: occurredAtUtc,
                OrderId: 123,
                CustomerId: 456,
                CustomerName: "John Smith",
                CustomerEmail: "john@example.com",
                TotalAmount: 100m,
                CancelledAtUtc: cancelledAtUtc);

        // Act
        await handler.HandleAsync(
            typeof(OrderCancelledIntegrationEvent).FullName!,
            Serialize(integrationEvent),
            CancellationToken.None);

        // Assert
        Assert.Equal(
            123,
            repository.CancelledOrderId);

        Assert.Equal(
            cancelledAtUtc,
            repository.CancelledAtUtc);

        Assert.Equal(
            occurredAtUtc,
            repository.CancelledEventOccurredAtUtc);
    }

    [Fact]
    public async Task HandleAsync_ForUnknownEventType_ThrowsException()
    {
        // Arrange
        var handler = new OrderEventProjectionHandler(
            new FakeOrderReadModelRepository(),
            NullLogger<OrderEventProjectionHandler>.Instance);

        // Act
        var action = () => handler.HandleAsync(
            "UnknownIntegrationEvent",
            [],
            CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            action);
    }

    private static byte[] Serialize<T>(T value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(
            value,
            new JsonSerializerOptions(
                JsonSerializerDefaults.Web));
    }

    private sealed class FakeOrderReadModelRepository
        : IOrderReadModelRepository
    {
        public OrderReadModel? CreatedOrder { get; private set; }

        public int? CompletedOrderId { get; private set; }

        public DateTime? CompletedAtUtc { get; private set; }

        public DateTime? CompletedEventOccurredAtUtc
        {
            get;
            private set;
        }

        public int? CancelledOrderId { get; private set; }

        public DateTime? CancelledAtUtc { get; private set; }

        public DateTime? CancelledEventOccurredAtUtc
        {
            get;
            private set;
        }

        public Task CreateIfMissingAsync(
            OrderReadModel order,
            CancellationToken cancellationToken)
        {
            CreatedOrder = order;

            return Task.CompletedTask;
        }

        public Task MarkCompletedAsync(
            int orderId,
            DateTime completedAtUtc,
            DateTime eventOccurredAtUtc,
            CancellationToken cancellationToken)
        {
            CompletedOrderId = orderId;
            CompletedAtUtc = completedAtUtc;
            CompletedEventOccurredAtUtc =
                eventOccurredAtUtc;

            return Task.CompletedTask;
        }

        public Task MarkCancelledAsync(
            int orderId,
            DateTime cancelledAtUtc,
            DateTime eventOccurredAtUtc,
            CancellationToken cancellationToken)
        {
            CancelledOrderId = orderId;
            CancelledAtUtc = cancelledAtUtc;
            CancelledEventOccurredAtUtc =
                eventOccurredAtUtc;

            return Task.CompletedTask;
        }
    }
}