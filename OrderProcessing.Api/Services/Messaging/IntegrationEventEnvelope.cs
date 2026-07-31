namespace OrderProcessing.Api.Services.Messaging;

public sealed record IntegrationEventEnvelope(
    Guid MessageId,
    string Type,
    string Payload,
    DateTime OccurredAtUtc);