using System.Text.Json;
using OrderProcessing.Api.Data;
using OrderProcessing.Api.Entities;
using OrderProcessing.Contracts;

namespace OrderProcessing.Api.Services.Outbox;

public sealed class OutboxWriter : IOutboxWriter
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly OrderProcessingDbContext _dbContext;

    public OutboxWriter(OrderProcessingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Add(IIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var eventType = integrationEvent.GetType();

        var typeName =
            eventType.FullName
            ?? throw new InvalidOperationException(
                $"Integration-event type {eventType.Name} has no full name.");

        var payload = JsonSerializer.Serialize(
            integrationEvent,
            eventType,
            SerializerOptions);

        var outboxMessage = new OutboxMessage
        {
            Id = integrationEvent.MessageId,
            Type = typeName,
            Payload = payload,
            OccurredAtUtc = integrationEvent.OccurredAtUtc
        };

        _dbContext.OutboxMessages.Add(outboxMessage);
    }
}