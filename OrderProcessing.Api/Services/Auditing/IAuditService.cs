namespace OrderProcessing.Api.Services.Auditing;

public interface IAuditService
{
    void Add(
        string entityName,
        string entityId,
        string action,
        object? oldValues = null,
        object? newValues = null,
        DateTime? createdAtUtc = null);
}