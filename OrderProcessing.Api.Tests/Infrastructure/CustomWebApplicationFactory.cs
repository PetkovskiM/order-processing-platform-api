using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using OrderProcessing.Api.Data;
using OrderProcessing.Api.Features.Orders.Queries.ReadModel;
using OrderProcessing.Api.Security;
using System.Data.Common;

namespace OrderProcessing.Api.Tests.Infrastructure;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public CustomWebApplicationFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(
    (_, configuration) =>
    {
        configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["AzureAd:Instance"] =
                    "https://login.microsoftonline.com/",

                ["AzureAd:TenantId"] =
                    "00000000-0000-0000-0000-000000000001",

                ["AzureAd:ClientId"] =
                    "00000000-0000-0000-0000-000000000002"
            });
    });

        builder.ConfigureServices(services =>
        {
            // Remove the SQL Server DbContext registration
            // from the production application.
            services.RemoveAll<DbContextOptions<OrderProcessingDbContext>>();

            services.RemoveAll<OrderProcessingDbContext>();

            services.RemoveAll<IDbContextOptionsConfiguration<OrderProcessingDbContext>>();

            // All test DbContext instances use the same open
            // SQLite connection.
            services.AddSingleton<DbConnection>(_connection);

            services.AddDbContext<OrderProcessingDbContext>(
                (serviceProvider, options) =>
                {
                    var connection = serviceProvider
                        .GetRequiredService<DbConnection>();

                    options.UseSqlite(connection);
                });

            services.RemoveAll<IOrderReadModelReader>();

            services.AddSingleton<IOrderReadModelReader, TestOrderReadModelReader>();

            services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;

                options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName, _ => { });
            });

    }

    protected override IHost CreateHost(
        IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<OrderProcessingDbContext>();

        dbContext.Database.EnsureCreated();

        TestDataSeeder.Seed(dbContext);

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }

    public HttpClient CreateAuthenticatedClient()
    {
        return CreateClientWithScopes(ApiScopes.Read, ApiScopes.Write);
    }

    public HttpClient CreateClientWithScopes(
        params string[] scopes)
    {
        var client = CreateClient();

        AddTestUser(client);

        if (scopes.Length > 0)
        {
            client.DefaultRequestHeaders.Add(TestAuthenticationHandler.ScopesHeaderName, string.Join(' ', scopes));
        }

        return client;
    }

    public HttpClient CreateClientWithRoles(params string[] roles)
    {
        var client = CreateClient();

        AddTestUser(client);

        if (roles.Length > 0)
        {
            client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RolesHeaderName, string.Join(' ', roles));
        }

        return client;
    }

    public HttpClient CreateAuthenticatedClientWithoutPermissions()
    {
        var client = CreateClient();

        AddTestUser(client);

        return client;
    }

    private static void AddTestUser(HttpClient client)
    {
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeaderName, "integration-test-user");
    }
}