namespace OrderProcessing.Api.Services.Messaging;

public interface IIntegrationEventPublisher
{
    Task PublishAsync(IntegrationEventEnvelope message, CancellationToken cancellationToken);
}