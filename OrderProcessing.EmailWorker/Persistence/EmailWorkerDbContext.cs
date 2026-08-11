using Microsoft.EntityFrameworkCore;
using OrderProcessing.EmailWorker.Persistence.Entities;

namespace OrderProcessing.EmailWorker.Persistence;

public sealed class EmailWorkerDbContext
    : DbContext
{
    public EmailWorkerDbContext(DbContextOptions<EmailWorkerDbContext> options) : base(options)
    {
    }

    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ProcessedMessage>(
            builder =>
            {
                builder.ToTable(
                    "ProcessedMessages",
                    schema: "email");

                builder.HasKey(message =>message.MessageId);

                builder.Property(message => message.MessageId)
                    .ValueGeneratedNever();

                builder.Property(message => message.EventType)
                    .HasMaxLength(256)
                    .IsRequired();

                builder.Property(message => message.ProcessedAtUtc)
                    .IsRequired();

                builder.HasIndex(message => message.ProcessedAtUtc);
            });
    }
}