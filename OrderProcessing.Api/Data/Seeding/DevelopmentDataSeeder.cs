using Microsoft.EntityFrameworkCore;
using OrderProcessing.Api.Entities;

namespace OrderProcessing.Api.Data.Seeding;

public sealed class DevelopmentDataSeeder
{
    private static readonly DateTime DemoCreatedAtUtc =
        new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly OrderProcessingDbContext _dbContext;
    private readonly ILogger<DevelopmentDataSeeder> _logger;

    public DevelopmentDataSeeder(
        OrderProcessingDbContext dbContext,
        ILogger<DevelopmentDataSeeder> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task SeedAsync(
        CancellationToken cancellationToken = default)
    {
        var addedCustomers = await SeedCustomersAsync(
            cancellationToken);

        var addedProducts = await SeedProductsAsync(
            cancellationToken);

        if (addedCustomers == 0 && addedProducts == 0)
        {
            _logger.LogInformation(
                "Development demo data already exists");

            return;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Development demo data seeded. CustomersAdded: {CustomersAdded}, ProductsAdded: {ProductsAdded}",
            addedCustomers,
            addedProducts);
    }

    private async Task<int> SeedCustomersAsync(
        CancellationToken cancellationToken)
    {
        var existingEmails = await _dbContext.Customers
            .AsNoTracking()
            .Select(customer => customer.Email)
            .ToListAsync(cancellationToken);

        var emailSet = existingEmails.ToHashSet(
            StringComparer.OrdinalIgnoreCase);

        var customers = new[]
        {
            new Customer
            {
                FirstName = "John",
                LastName = "Smith",
                Email = "john.smith@example.com",
                PhoneNumber = "+38970111222",
                CreatedAtUtc = DemoCreatedAtUtc
            },
            new Customer
            {
                FirstName = "Ana",
                LastName = "Petrovska",
                Email = "ana.petrovska@example.com",
                PhoneNumber = "+38970222333",
                CreatedAtUtc = DemoCreatedAtUtc
            },
            new Customer
            {
                FirstName = "Peter",
                LastName = "Johnson",
                Email = "peter.johnson@example.com",
                PhoneNumber = "+38970333444",
                CreatedAtUtc = DemoCreatedAtUtc
            }
        };

        var missingCustomers = customers
            .Where(customer => !emailSet.Contains(customer.Email))
            .ToList();

        if (missingCustomers.Count > 0)
        {
            await _dbContext.Customers.AddRangeAsync(
                missingCustomers,
                cancellationToken);
        }

        return missingCustomers.Count;
    }

    private async Task<int> SeedProductsAsync(
        CancellationToken cancellationToken)
    {
        var existingSkus = await _dbContext.Products
            .AsNoTracking()
            .Select(product => product.Sku)
            .ToListAsync(cancellationToken);

        var skuSet = existingSkus.ToHashSet(
            StringComparer.OrdinalIgnoreCase);

        var products = new[]
        {
            new Product
            {
                Sku = "LAP-15",
                Name = "Laptop Pro 15",
                Description = "Development demo laptop.",
                Price = 1200m,
                StockQuantity = 50,
                CreatedAtUtc = DemoCreatedAtUtc
            },
            new Product
            {
                Sku = "KEY-01",
                Name = "Mechanical Keyboard",
                Description = "Development demo keyboard.",
                Price = 85m,
                StockQuantity = 200,
                CreatedAtUtc = DemoCreatedAtUtc
            },
            new Product
            {
                Sku = "HUB-07",
                Name = "USB-C Hub",
                Description = "Development demo USB-C hub.",
                Price = 45m,
                StockQuantity = 150,
                CreatedAtUtc = DemoCreatedAtUtc
            },
            new Product
            {
                Sku = "MOU-09",
                Name = "Wireless Mouse",
                Description = "Development demo wireless mouse.",
                Price = 35m,
                StockQuantity = 120,
                CreatedAtUtc = DemoCreatedAtUtc
            }
        };

        var missingProducts = products
            .Where(product => !skuSet.Contains(product.Sku))
            .ToList();

        if (missingProducts.Count > 0)
        {
            await _dbContext.Products.AddRangeAsync(
                missingProducts,
                cancellationToken);
        }

        return missingProducts.Count;
    }
}