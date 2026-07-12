using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderProcessing.Api.Entities;

namespace OrderProcessing.Api.Data.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(c => c.PhoneNumber)
            .HasMaxLength(30);

        builder.Property(c => c.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(c => c.Email)
            .IsUnique();

        builder.HasData(
    new Customer
    {
        Id = 1001,
        FirstName = "John",
        LastName = "Smith",
        Email = "john.smith@example.com",
        PhoneNumber = "+38970111222",
        CreatedAtUtc = new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc)
    },
    new Customer
    {
        Id = 1002,
        FirstName = "Ana",
        LastName = "Petrovska",
        Email = "ana.petrovska@example.com",
        PhoneNumber = "+38970222333",
        CreatedAtUtc = new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc)
    },
    new Customer
    {
        Id = 1003,
        FirstName = "Mark",
        LastName = "Johnson",
        Email = "mark.johnson@example.com",
        PhoneNumber = null,
        CreatedAtUtc = new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc)
    }
);
    }
}