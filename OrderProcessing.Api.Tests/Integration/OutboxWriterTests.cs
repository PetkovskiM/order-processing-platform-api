using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderProcessing.Api.Data;
using OrderProcessing.Api.Services.Outbox;
using OrderProcessing.Api.Tests.Infrastructure;
using OrderProcessing.Contracts.Orders;

namespace OrderProcessing.Api.Tests.Integration;

public sealed class OutboxWriterTests : IntegrationTestBase
{
    [Fact]
    public async Task Add_WhenChangesAreSaved_PersistsSerializedEvent()
    {
        // Arrange
        var integrationEvent = CreateOrderCreatedEvent();

        await using (var arrangeScope =
            Factory.Services.CreateAsyncScope())
        {
            var outboxWriter = arrangeScope.ServiceProvider
                .GetRequiredService<IOutboxWriter>();

            var dbContext = arrangeScope.ServiceProvider
                .GetRequiredService<OrderProcessingDbContext>();

            // Act
            outboxWriter.Add(integrationEvent);

            await dbContext.SaveChangesAsync();
        }

        // Assert using a different DbContext.
        await using var assertScope =
            Factory.Services.CreateAsyncScope();

        var assertContext = assertScope.ServiceProvider
            .GetRequiredService<OrderProcessingDbContext>();

        var storedMessage = await assertContext.OutboxMessages
            .AsNoTracking()
            .SingleAsync(message =>
                message.Id == integrationEvent.MessageId);

        Assert.Equal(
            typeof(OrderCreatedIntegrationEvent).FullName,
            storedMessage.Type);

        Assert.Equal(
            integrationEvent.OccurredAtUtc,
            storedMessage.OccurredAtUtc);

        Assert.Null(storedMessage.ProcessedAtUtc);
        Assert.Equal(0, storedMessage.RetryCount);

        var deserialized =
            JsonSerializer.Deserialize<OrderCreatedIntegrationEvent>(
                storedMessage.Payload,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(deserialized);
        Assert.Equal(
            integrationEvent.MessageId,
            deserialized.MessageId);

        Assert.Equal(
            integrationEvent.OrderId,
            deserialized.OrderId);

        Assert.Equal(
            integrationEvent.CustomerEmail,
            deserialized.CustomerEmail);

        Assert.Single(deserialized.Items);
    }

    [Fact]
    public async Task Add_WhenSaveChangesIsNotCalled_DoesNotPersistMessage()
    {
        // Arrange
        var integrationEvent = CreateOrderCreatedEvent();

        await using (var arrangeScope =
            Factory.Services.CreateAsyncScope())
        {
            var outboxWriter = arrangeScope.ServiceProvider
                .GetRequiredService<IOutboxWriter>();

            // Act
            outboxWriter.Add(integrationEvent);

            // Intentionally no SaveChangesAsync.
        }

        // Assert
        await using var assertScope =
            Factory.Services.CreateAsyncScope();

        var assertContext = assertScope.ServiceProvider
            .GetRequiredService<OrderProcessingDbContext>();

        var exists = await assertContext.OutboxMessages
            .AsNoTracking()
            .AnyAsync(message =>
                message.Id == integrationEvent.MessageId);

        Assert.False(exists);
    }

    private static OrderCreatedIntegrationEvent CreateOrderCreatedEvent()
    {
        var occurredAtUtc = new DateTime(
            2026,
            7,
            27,
            10,
            0,
            0,
            DateTimeKind.Utc);

        return new OrderCreatedIntegrationEvent(
            MessageId: Guid.NewGuid(),
            OccurredAtUtc: occurredAtUtc,
            OrderId: 1050,
            CustomerId: TestDataSeeder.CustomerId,
            CustomerName: "Integration Customer",
            CustomerEmail:"integration.customer@example.com",
            TotalAmount: 24.99m,
            CreatedAtUtc: occurredAtUtc,
            Items:
            [
                new OrderItemIntegrationModel(
                    ProductId: TestDataSeeder.ProductId,
                    ProductName: "Integration Test Product",
                    Quantity: 1,
                    UnitPrice: 24.99m,
                    LineTotal: 24.99m)
            ]);
    }
}