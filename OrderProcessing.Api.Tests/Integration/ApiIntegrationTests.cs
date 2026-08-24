using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderProcessing.Api.Data;
using OrderProcessing.Api.DTOs.Common;
using OrderProcessing.Api.DTOs.Orders;
using OrderProcessing.Api.Entities;
using OrderProcessing.Api.Services.Auditing;
using OrderProcessing.Api.Tests.Infrastructure;
using OrderProcessing.Contracts.Orders;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OrderProcessing.Api.Tests.Integration;

public sealed class ApiIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task TestDatabase_StartsWithoutOrders()
    {
        using var scope = Factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<OrderProcessingDbContext>();

        var orderCount = await dbContext.Orders.CountAsync();
        var auditLogCount = await dbContext.AuditLogs.CountAsync();

        Assert.Equal(0, orderCount);
        Assert.Equal(0, auditLogCount);
    }

    [Fact]
    public async Task CancelOrder_RestoresProductStock()
    {
        // Arrange
        var createdOrder = await CreateTestOrderAsync(quantity: 2);

        var stockAfterCreation = await GetProductStockAsync(
            TestDataSeeder.ProductId);

        Assert.Equal(
            TestDataSeeder.InitialProductStock - 2,
            stockAfterCreation);

        // Act
        var response = await Client.PatchAsync(
            $"/api/orders/{createdOrder.Id}/cancel",
            content: null);

        var cancelledOrder = await response.Content
            .ReadFromJsonAsync<OrderResponse>();

        var stockAfterCancellation = await GetProductStockAsync(
            TestDataSeeder.ProductId);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.NotNull(cancelledOrder);
        Assert.Equal(OrderStatus.Cancelled, cancelledOrder.Status);

        Assert.Equal(
            TestDataSeeder.InitialProductStock,
            stockAfterCancellation);
    }


    [Fact]
    public async Task GetOrders_ReturnsPagedResponse()
    {
        // Act
        var response = await Client.GetAsync(
            "/api/orders?page=1&pageSize=2");

        var result = await response.Content
            .ReadFromJsonAsync<PagedResponse<OrderResponse>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.NotNull(result);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.False(result.HasPreviousPage);
        Assert.True(result.HasNextPage);
    }

    [Fact]
    public async Task GetOrders_WhenCustomerFilterProvided_ReturnsOnlyMatchingOrders()
    {
        // Act
        var response = await Client.GetAsync(
            $"/api/orders?customerId={TestDataSeeder.SecondCustomerId}");

        var result = await response.Content
            .ReadFromJsonAsync<PagedResponse<OrderResponse>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.NotNull(result);
        Assert.Single(result.Items);

        Assert.All(
            result.Items,
            order => Assert.Equal(
                TestDataSeeder.SecondCustomerId,
                order.CustomerId));
    }

    [Fact]
    public async Task GetOrders_WithInvalidDateRange_ReturnsBadRequest()
    {
        // Act
        var response = await Client.GetAsync(
            "/api/orders" +
            "?createdFromUtc=2026-07-20T00:00:00Z" +
            "&createdToUtc=2026-07-10T00:00:00Z");

        var body = await response.Content
            .ReadFromJsonAsync<JsonElement>();

        // Assert
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        Assert.Equal(
            "invalid_date_range",
            body.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task CompleteOrder_UpdatesStatusAndCreatesAuditLog()
    {
        // Arrange
        var createdOrder = await CreateTestOrderAsync();

        // Act
        var response = await Client.PatchAsync(
            $"/api/orders/{createdOrder.Id}/complete",
            content: null);

        var completedOrder = await response.Content
            .ReadFromJsonAsync<OrderResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.NotNull(completedOrder);
        Assert.Equal(OrderStatus.Completed, completedOrder.Status);
        Assert.NotNull(completedOrder.CompletedAtUtc);

        using var scope = Factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<OrderProcessingDbContext>();

        var auditLogExists = await dbContext.AuditLogs
            .AsNoTracking()
            .AnyAsync(audit =>
                audit.EntityName == nameof(Order) &&
                audit.EntityId == createdOrder.Id.ToString() &&
                audit.Action == AuditActions.Completed);

        Assert.True(auditLogExists);
    }

    [Fact]
    public async Task TestDatabase_ContainsDedicatedFixtureData()
    {
        using var scope = Factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<OrderProcessingDbContext>();

        var customerExists = await dbContext.Customers
            .AsNoTracking()
            .AnyAsync(
                customer =>
                    customer.Id == TestDataSeeder.CustomerId);

        var productExists = await dbContext.Products
            .AsNoTracking()
            .AnyAsync(
                product =>
                    product.Id == TestDataSeeder.ProductId);

        Assert.True(customerExists);
        Assert.True(productExists);
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync("/api/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMissingOrder_ReturnsConsistentNotFoundProblem()
    {
        // Act
        var response = await Client.GetAsync(
            "/api/orders/999999");

        var body = await response.Content
            .ReadFromJsonAsync<JsonElement>();

        // Assert
        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        Assert.Equal(
            "Not Found",
            body.GetProperty("title").GetString());

        Assert.Equal(
            404,
            body.GetProperty("status").GetInt32());

        Assert.Equal(
            "resource_not_found",
            body.GetProperty("errorCode").GetString());

        Assert.True(body.TryGetProperty("traceId", out _));
        Assert.True(body.TryGetProperty("timestampUtc", out _));
    }

    [Fact]
    public async Task CreateOrder_WithInvalidRequest_ReturnsValidationProblem()
    {
        // Arrange
        var request = new CreateOrderRequest
        {
            CustomerId = 0,
            Items = []
        };

        // Act
        var response = await Client.PostAsJsonAsync(
            "/api/orders",
            request);

        var body = await response.Content
            .ReadFromJsonAsync<JsonElement>();

        // Assert
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        Assert.Equal(
            "validation_failed",
            body.GetProperty("errorCode").GetString());

        Assert.True(body.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("CustomerId", out _));
        Assert.True(errors.TryGetProperty("Items", out _));
    }

    [Fact]
    public async Task CreateOrder_WithValidRequest_CreatesOrderAndReducesStock()
    {
        var stockBefore = await GetProductStockAsync(
        TestDataSeeder.ProductId);

        Assert.Equal(
            TestDataSeeder.InitialProductStock,
            stockBefore);

        var request = new CreateOrderRequest
        {
            CustomerId = TestDataSeeder.CustomerId,
            Items =
            [
                new CreateOrderItemRequest
                {
                    ProductId = TestDataSeeder.ProductId,
                    Quantity = 1
                }
            ]
        };

        // Act
        var response = await Client.PostAsJsonAsync(
            "/api/orders",
            request);

        var createdOrder = await response.Content
            .ReadFromJsonAsync<OrderResponse>();

        var stockAfter = await GetProductStockAsync(TestDataSeeder.ProductId);

        // Assert
        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        Assert.NotNull(createdOrder);
        Assert.True(createdOrder.Id > 0);
        Assert.Equal(TestDataSeeder.CustomerId, createdOrder.CustomerId);
        Assert.Equal(24.99m, createdOrder.TotalAmount);

        Assert.Equal(
        TestDataSeeder.InitialProductStock - 1,
        stockAfter);

        Assert.NotNull(response.Headers.Location);
        Assert.Contains(
            $"/api/orders/{createdOrder.Id}",
            response.Headers.Location.ToString().ToLowerInvariant());
    }

    private async Task<int> GetProductStockAsync(
        int productId)
    {
        using var scope = Factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<OrderProcessingDbContext>();

        return await dbContext.Products
            .AsNoTracking()
            .Where(product => product.Id == productId)
            .Select(product => product.StockQuantity)
            .SingleAsync();
    }


    [Fact]
    public async Task CancelOrder_CreatesCancellationAuditLog()
    {
        // Arrange
        var createdOrder = await CreateTestOrderAsync(quantity: 2);

        // Act
        var response = await Client.PatchAsync(
            $"/api/orders/{createdOrder.Id}/cancel",
            content: null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = Factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<OrderProcessingDbContext>();

        var auditLog = await dbContext.AuditLogs
            .AsNoTracking()
            .SingleOrDefaultAsync(audit =>
                audit.EntityName == nameof(Order) &&
                audit.EntityId == createdOrder.Id.ToString() &&
                audit.Action == AuditActions.Cancelled);

        Assert.NotNull(auditLog);
        Assert.NotNull(auditLog.OldValues);
        Assert.NotNull(auditLog.NewValues);
    }


    [Fact]
    public async Task CancelOrder_WhenOrderIsCompleted_ReturnsInvalidStatusError()
    {
        // Arrange
        var createdOrder = await CreateTestOrderAsync();

        var completeResponse = await Client.PatchAsync(
            $"/api/orders/{createdOrder.Id}/complete",
            content: null);

        completeResponse.EnsureSuccessStatusCode();

        var stockBeforeCancellationAttempt =
            await GetProductStockAsync(TestDataSeeder.ProductId);

        // Act
        var response = await Client.PatchAsync(
            $"/api/orders/{createdOrder.Id}/cancel",
            content: null);

        var body = await response.Content
            .ReadFromJsonAsync<JsonElement>();

        var stockAfterCancellationAttempt =
            await GetProductStockAsync(TestDataSeeder.ProductId);

        // Assert
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        Assert.Equal(
            "invalid_order_status",
            body.GetProperty("errorCode").GetString());

        Assert.Equal(
            stockBeforeCancellationAttempt,
            stockAfterCancellationAttempt);
    }


    [Fact]
    public async Task CancelOrder_WhenOrderDoesNotExist_ReturnsNotFound()
    {
        // Act
        var response = await Client.PatchAsync(
            "/api/orders/999999/cancel",
            content: null);

        var body = await response.Content
            .ReadFromJsonAsync<JsonElement>();

        // Assert
        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        Assert.Equal(
            "resource_not_found",
            body.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task CreateOrder_PersistsOrderCreatedEventInOutbox()
    {
        // Arrange and Act
        var createdOrder = await CreateTestOrderAsync(quantity: 2);

        // Assert
        await using var scope = Factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<OrderProcessingDbContext>();

        var messages = await dbContext.OutboxMessages
            .AsNoTracking()
            .Where(message =>
                message.Type == typeof(OrderCreatedIntegrationEvent).FullName)
            .ToListAsync();

        var storedMessage = Assert.Single(messages);

        Assert.Null(storedMessage.ProcessedAtUtc);
        Assert.Equal(0, storedMessage.RetryCount);
        Assert.Null(storedMessage.LastAttemptAtUtc);
        Assert.Null(storedMessage.LastError);

        var orderExists = await dbContext.Orders
        .AsNoTracking()
        .AnyAsync(order =>
         order.Id == createdOrder.Id);

        Assert.True(orderExists);

        var integrationEvent =
            JsonSerializer.Deserialize<OrderCreatedIntegrationEvent>(
                storedMessage.Payload,
                new JsonSerializerOptions(
                    JsonSerializerDefaults.Web));

        Assert.NotNull(integrationEvent);

        Assert.Equal(
            storedMessage.Id,
            integrationEvent.MessageId);

        Assert.Equal(
            createdOrder.Id,
            integrationEvent.OrderId);

        Assert.Equal(
            TestDataSeeder.CustomerId,
            integrationEvent.CustomerId);

        Assert.Equal(
            createdOrder.TotalAmount,
            integrationEvent.TotalAmount);

        var item = Assert.Single(integrationEvent.Items);

        Assert.Equal(
            TestDataSeeder.ProductId,
            item.ProductId);

        Assert.Equal(2, item.Quantity);
    }


    [Fact]
    public async Task CreateOrder_WhenCustomerDoesNotExist_DoesNotCreateOutboxMessage()
    {
        // Arrange
        var request = new CreateOrderRequest
        {
            CustomerId = 999999,
            Items =
            [
                new CreateOrderItemRequest
            {
                ProductId = TestDataSeeder.ProductId,
                Quantity = 1
            }
            ]
        };

        // Act
        var response = await Client.PostAsJsonAsync(
            "/api/orders",
            request);

        // Assert
        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        await using var scope =
            Factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<OrderProcessingDbContext>();

        var outboxMessageCount =
            await dbContext.OutboxMessages.CountAsync();

        Assert.Equal(0, outboxMessageCount);

        var orderCount =
            await dbContext.Orders.CountAsync();

        Assert.Equal(0, orderCount);
    }


    [Fact]
    public async Task CompleteOrder_PersistsOrderCompletedEventInOutbox()
    {
        // Arrange
        var createdOrder = await CreateTestOrderAsync();

        // Act
        var response = await Client.PatchAsync(
            $"/api/orders/{createdOrder.Id}/complete",
            content: null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope =
            Factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<OrderProcessingDbContext>();

        var storedMessage = await dbContext.OutboxMessages
            .AsNoTracking()
            .SingleAsync(message =>
                message.Type ==
                typeof(OrderCompletedIntegrationEvent).FullName);

        Assert.Null(storedMessage.ProcessedAtUtc);
        Assert.Equal(0, storedMessage.RetryCount);

        var integrationEvent =
            JsonSerializer.Deserialize<OrderCompletedIntegrationEvent>(
                storedMessage.Payload,
                new JsonSerializerOptions(
                    JsonSerializerDefaults.Web));

        Assert.NotNull(integrationEvent);

        Assert.Equal(
            storedMessage.Id,
            integrationEvent.MessageId);

        Assert.Equal(
            createdOrder.Id,
            integrationEvent.OrderId);

        Assert.Equal(
            TestDataSeeder.CustomerId,
            integrationEvent.CustomerId);

        Assert.Equal(
            createdOrder.TotalAmount,
            integrationEvent.TotalAmount);

        Assert.NotEqual(
            default,
            integrationEvent.CompletedAtUtc);
    }

    [Fact]
    public async Task CancelOrder_PersistsOrderCancelledEventInOutbox()
    {
        // Arrange
        var createdOrder =
            await CreateTestOrderAsync(quantity: 2);

        // Act
        var response = await Client.PatchAsync(
            $"/api/orders/{createdOrder.Id}/cancel",
            content: null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope =
            Factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<OrderProcessingDbContext>();

        var storedMessage = await dbContext.OutboxMessages
            .AsNoTracking()
            .SingleAsync(message =>
                message.Type ==
                typeof(OrderCancelledIntegrationEvent).FullName);

        Assert.Null(storedMessage.ProcessedAtUtc);
        Assert.Equal(0, storedMessage.RetryCount);

        var integrationEvent =
            JsonSerializer.Deserialize<OrderCancelledIntegrationEvent>(
                storedMessage.Payload,
                new JsonSerializerOptions(
                    JsonSerializerDefaults.Web));

        Assert.NotNull(integrationEvent);

        Assert.Equal(
            storedMessage.Id,
            integrationEvent.MessageId);

        Assert.Equal(
            createdOrder.Id,
            integrationEvent.OrderId);

        Assert.Equal(
            TestDataSeeder.CustomerId,
            integrationEvent.CustomerId);

        Assert.Equal(
            createdOrder.TotalAmount,
            integrationEvent.TotalAmount);

        Assert.NotEqual(
            default,
            integrationEvent.CancelledAtUtc);
    }

    [Fact]
    public async Task GetOrderById_WhenReadModelExists_ReturnsOrder()
    {
        var response = await Client.GetAsync(
            $"/api/orders/" +
            $"{TestOrderReadModelReader.ExistingOrderId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();

        Assert.NotNull(order);

        Assert.Equal(TestOrderReadModelReader.ExistingOrderId, order.Id);
    }

    [Fact]
    public async Task GetOrderById_WhenReadModelDoesNotExist_ReturnsNotFound()
    {
        var response = await Client.GetAsync("/api/orders/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}