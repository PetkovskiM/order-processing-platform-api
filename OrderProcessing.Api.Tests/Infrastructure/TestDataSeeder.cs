using Microsoft.EntityFrameworkCore;
using OrderProcessing.Api.Data;
using OrderProcessing.Api.Entities;

namespace OrderProcessing.Api.Tests.Infrastructure;

internal static class TestDataSeeder
{
    public const int CustomerId = 9001;
    public const int SecondCustomerId = 9002;

    public const int ProductId = 9101;
    public const int SecondProductId = 9102;

    public const int InitialProductStock = 100;
    public const decimal ProductPrice = 24.99m;

    private static readonly DateTime SeedDateUtc =
        new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static void Seed(OrderProcessingDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        SeedCustomers(dbContext);
        SeedProducts(dbContext);

        dbContext.SaveChanges();
    }

    private static void SeedCustomers(OrderProcessingDbContext dbContext)
    {
        if (!dbContext.Customers.Any(
                customer => customer.Id == CustomerId))
        {
            dbContext.Customers.Add(new Customer
            {
                Id = CustomerId,
                FirstName = "Integration",
                LastName = "Customer",
                Email = "integration.customer@example.com",
                PhoneNumber = "+38970000001",
                CreatedAtUtc = SeedDateUtc
            });
        }

        if (!dbContext.Customers.Any(
                customer => customer.Id == SecondCustomerId))
        {
            dbContext.Customers.Add(new Customer
            {
                Id = SecondCustomerId,
                FirstName = "Second",
                LastName = "Customer",
                Email = "second.integration.customer@example.com",
                PhoneNumber = "+38970000002",
                CreatedAtUtc = SeedDateUtc
            });
        }
    }

    private static void SeedProducts(OrderProcessingDbContext dbContext)
    {
        if (!dbContext.Products.Any(product => product.Id == ProductId))
        {
            dbContext.Products.Add(new Product
            {
                Id = ProductId,
                Sku = "TEST-PRODUCT-001",
                Name = "Integration Test Product",
                Description = "Product used by integration tests.",
                Price = ProductPrice,
                StockQuantity = InitialProductStock,
                CreatedAtUtc = SeedDateUtc
            });
        }

        if (!dbContext.Products.Any(product => product.Id == SecondProductId))
        {
            dbContext.Products.Add(new Product
            {
                Id = SecondProductId,
                Sku = "TEST-PRODUCT-002",
                Name = "Second Integration Test Product",
                Description = "Second product used by integration tests.",
                Price = 49.99m,
                StockQuantity = 50,
                CreatedAtUtc = SeedDateUtc
            });
        }
    }
}