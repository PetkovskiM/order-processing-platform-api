using OrderProcessing.Api.Security;
using OrderProcessing.Api.Tests.Infrastructure;
using SharpCompress.Factories;
using System.Net;

public sealed class AuthorizationTests : IntegrationTestBase
{
    [Fact]
    public async Task GetOrders_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/orders");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetOrders_WithReadPermission_ReturnsOk()
    {
        using var client = Factory.CreateClientWithScopes(ApiScopes.Read);

        var response = await client.GetAsync("/api/orders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_WithoutAuthentication_ReturnsOk()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}