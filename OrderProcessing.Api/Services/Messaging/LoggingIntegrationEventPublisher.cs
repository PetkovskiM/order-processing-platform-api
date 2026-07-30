namespace OrderProcessing.Api.Services.Messaging;

public sealed class LoggingIntegrationEventPublisher : IIntegrationEventPublisher
{
    private readonly ILogger<LoggingIntegrationEventPublisher> _logger;

    public LoggingIntegrationEventPublisher(ILogger<LoggingIntegrationEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync(IntegrationEventEnvelope message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            "Simulated publishing of integration event " +
            "{MessageId} with type {EventType}, occurred at {OccurredAtUtc}",
            message.MessageId,
            message.Type,
            message.OccurredAtUtc);

        return Task.CompletedTask;
    }
}