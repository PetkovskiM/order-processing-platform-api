using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderProcessing.Api.Entities;

namespace OrderProcessing.Api.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", table =>
        {
            table.HasCheckConstraint("CK_Products_Price_NonNegative", "[Price] >= 0");
            table.HasCheckConstraint("CK_Products_StockQuantity_NonNegative", "[StockQuantity] >= 0");
        });

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Sku)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Description)
            .HasMaxLength(1000);

        builder.Property(p => p.Price)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(p => p.StockQuantity)
            .IsRequired();

        builder.Property(p => p.CreatedAtUtc)
            .IsRequired();

        builder.Property(p => p.UpdatedAtUtc);

        builder.HasIndex(p => p.Sku)
            .IsUnique();

        builder.HasIndex(p => p.Name);

        builder.HasData(
    new Product
    {
        Id = 1001,
        Sku = "LAPTOP-001",
        Name = "Business Laptop",
        Description = "Reliable laptop for office and development work.",
        Price = 899.99m,
        StockQuantity = 15,
        CreatedAtUtc = new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAtUtc = null
    },
    new Product
    {
        Id = 1002,
        Sku = "MOUSE-001",
        Name = "Wireless Mouse",
        Description = "Ergonomic wireless mouse.",
        Price = 24.99m,
        StockQuantity = 100,
        CreatedAtUtc = new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAtUtc = null
    },
    new Product
    {
        Id = 1003,
        Sku = "KEYBOARD-001",
        Name = "Mechanical Keyboard",
        Description = "Mechanical keyboard with backlight.",
        Price = 79.99m,
        StockQuantity = 50,
        CreatedAtUtc = new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAtUtc = null
    },
    new Product
    {
        Id = 1004,
        Sku = "MONITOR-001",
        Name = "27 Inch Monitor",
        Description = "27 inch full HD monitor.",
        Price = 199.99m,
        StockQuantity = 25,
        CreatedAtUtc = new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAtUtc = null
    }
);
    }
}