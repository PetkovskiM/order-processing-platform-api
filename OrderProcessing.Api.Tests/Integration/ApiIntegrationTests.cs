using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderProcessing.Api.Data;
using OrderProcessing.Api.DTOs.Orders;
using OrderProcessing.Api.Tests.Infrastructure;

namespace OrderProcessing.Api.Tests.Integration;

public sealed class ApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ApiIntegrationTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task TestDatabase_ContainsDedicatedFixtureData()
    {
        using var scope = _factory.Services.CreateScope();

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
        var response = await _client.GetAsync("/api/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMissingOrder_ReturnsConsistentNotFoundProblem()
    {
        // Act
        var response = await _client.GetAsync(
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
        var response = await _client.PostAsJsonAsync(
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
        var stockBefore = await GetProductStockAsync(TestDataSeeder.ProductId);

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
        var response = await _client.PostAsJsonAsync(
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

        Assert.Equal(stockBefore - 1, stockAfter);

        Assert.NotNull(response.Headers.Location);
        Assert.Contains(
            $"/api/orders/{createdOrder.Id}",
            response.Headers.Location.ToString().ToLowerInvariant());
    }

    private async Task<int> GetProductStockAsync(
        int productId)
    {
        using var scope = _factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<OrderProcessingDbContext>();

        return await dbContext.Products
            .AsNoTracking()
            .Where(product => product.Id == productId)
            .Select(product => product.StockQuantity)
            .SingleAsync();
    }
}