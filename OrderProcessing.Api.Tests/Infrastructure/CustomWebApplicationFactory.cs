using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using OrderProcessing.Api.Data;
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

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove the SQL Server DbContext registration
            // from the production application.
            services.RemoveAll<
                DbContextOptions<OrderProcessingDbContext>>();

            services.RemoveAll<OrderProcessingDbContext>();

            services.RemoveAll<
            IDbContextOptionsConfiguration<OrderProcessingDbContext>>();

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
}