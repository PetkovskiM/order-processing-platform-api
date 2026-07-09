using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderProcessing.Api.Entities;

namespace OrderProcessing.Api.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.EntityName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.EntityId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.Action)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.OldValues);

        builder.Property(a => a.NewValues);

        builder.Property(a => a.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(a => a.CreatedAtUtc);

        builder.HasIndex(a => new { a.EntityName, a.EntityId });
    }
}