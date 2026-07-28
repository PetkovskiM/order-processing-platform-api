using OrderProcessing.Contracts;

namespace OrderProcessing.Api.Services.Outbox;

public interface IOutboxWriter
{
    void Add(IIntegrationEvent integrationEvent);
}