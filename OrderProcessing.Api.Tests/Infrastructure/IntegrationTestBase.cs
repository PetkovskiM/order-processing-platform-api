using OrderProcessing.Api.DTOs.Orders;
using System.Net.Http.Json;

namespace OrderProcessing.Api.Tests.Infrastructure;

public abstract class IntegrationTestBase : IDisposable
{
    protected CustomWebApplicationFactory Factory { get; }

    protected HttpClient Client { get; }

    protected IntegrationTestBase()
    {
        Factory = new CustomWebApplicationFactory();
        Client = Factory.CreateClient();
    }

    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();

        GC.SuppressFinalize(this);
    }

    public async Task<OrderResponse> CreateTestOrderAsync(
    int quantity = 1,
    int customerId = TestDataSeeder.CustomerId)
    {
        var request = new CreateOrderRequest
        {
            CustomerId = customerId,
            Items =
            [
                new CreateOrderItemRequest
            {
                ProductId = TestDataSeeder.ProductId,
                Quantity = quantity
            }
            ]
        };

        var response = await Client.PostAsJsonAsync(
            "/api/orders",
            request);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<OrderResponse>()
            ?? throw new InvalidOperationException(
                "The create-order response was empty.");
    }
}