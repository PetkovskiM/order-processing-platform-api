using System.Text.Json;
using System.Text.Json.Serialization;
using OrderProcessing.Api.Data;
using OrderProcessing.Api.Entities;

namespace OrderProcessing.Api.Services.Auditing;

public sealed class AuditService : IAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    private readonly OrderProcessingDbContext _dbContext;

    public AuditService(OrderProcessingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Add(
        string entityName,
        string entityId,
        string action,
        object? oldValues = null,
        object? newValues = null,
        DateTime? createdAtUtc = null)
    {
        var auditLog = new AuditLog
        {
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            OldValues = Serialize(oldValues),
            NewValues = Serialize(newValues),
            CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow
        };

        _dbContext.AuditLogs.Add(auditLog);
    }

    private static string? Serialize(object? value)
    {
        return value is null
            ? null
            : JsonSerializer.Serialize(value, value.GetType(), JsonOptions);
    }
}