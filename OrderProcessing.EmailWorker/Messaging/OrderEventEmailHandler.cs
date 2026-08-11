using System.Text.Json;
using OrderProcessing.Contracts.Orders;
using OrderProcessing.EmailWorker.Emailing;

namespace OrderProcessing.EmailWorker.Messaging;

public sealed class OrderEventEmailHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IEmailSender _emailSender;
    private readonly ILogger<OrderEventEmailHandler> _logger;

    public OrderEventEmailHandler(IEmailSender emailSender, ILogger<OrderEventEmailHandler> logger)
    {
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task HandleAsync(string eventType, byte[] body, CancellationToken cancellationToken)
    {
        switch (eventType)
        {
            case var type when type == typeof(OrderCreatedIntegrationEvent).FullName:

                var createdEvent = Deserialize<OrderCreatedIntegrationEvent>(body);

                await HandleCreatedAsync(createdEvent, cancellationToken);

                break;

            case var type when type == typeof(OrderCompletedIntegrationEvent).FullName:

                var completedEvent = Deserialize<OrderCompletedIntegrationEvent>(body);

                await HandleCompletedAsync(completedEvent, cancellationToken);

                break;

            case var type when type == typeof(OrderCancelledIntegrationEvent).FullName:

                var cancelledEvent = Deserialize<OrderCancelledIntegrationEvent>(body);

                await HandleCancelledAsync(cancelledEvent, cancellationToken);

                break;

            default:
                throw new UnsupportedIntegrationEventException(eventType);
        }
    }

    private async Task HandleCreatedAsync(OrderCreatedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        var email = new EmailMessage(
            Recipient: integrationEvent.CustomerEmail,
            Subject:
                $"Order #{integrationEvent.OrderId} created",
            Body:
                $"Hello {integrationEvent.CustomerName}, " +
                $"your order #{integrationEvent.OrderId} " +
                $"was created with a total of " +
                $"{integrationEvent.TotalAmount:C}.");

        await _emailSender.SendAsync(email, cancellationToken);

        _logger.LogInformation(
            "Handled order-created event {MessageId} " +
            "for order {OrderId}",
            integrationEvent.MessageId,
            integrationEvent.OrderId);
    }

    private async Task HandleCompletedAsync(OrderCompletedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        var email = new EmailMessage(
            Recipient: integrationEvent.CustomerEmail,
            Subject:
                $"Order #{integrationEvent.OrderId} completed",
            Body:
                $"Hello {integrationEvent.CustomerName}, " +
                $"your order #{integrationEvent.OrderId} " +
                "has been completed.");

        await _emailSender.SendAsync(email, cancellationToken);

        _logger.LogInformation(
            "Handled order-completed event {MessageId} " +
            "for order {OrderId}",
            integrationEvent.MessageId,
            integrationEvent.OrderId);
    }

    private async Task HandleCancelledAsync(
        OrderCancelledIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        var email = new EmailMessage(
            Recipient: integrationEvent.CustomerEmail,
            Subject:
                $"Order #{integrationEvent.OrderId} cancelled",
            Body:
                $"Hello {integrationEvent.CustomerName}, " +
                $"your order #{integrationEvent.OrderId} " +
                "has been cancelled.");

        await _emailSender.SendAsync(email, cancellationToken);

        _logger.LogInformation(
            "Handled order-cancelled event {MessageId} " +
            "for order {OrderId}",
            integrationEvent.MessageId,
            integrationEvent.OrderId);
    }

    private static TEvent Deserialize<TEvent>(byte[] body)
    {
        return JsonSerializer.Deserialize<TEvent>(body, SerializerOptions) ?? throw new JsonException(
                $"Could not deserialize " +
                $"{typeof(TEvent).Name}.");
    }
}