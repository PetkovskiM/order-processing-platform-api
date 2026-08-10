using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace OrderProcessing.Api.Services.Messaging;

public sealed class RabbitMqIntegrationEventPublisher : IIntegrationEventPublisher, IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly ConnectionFactory _connectionFactory;
    private readonly ILogger<RabbitMqIntegrationEventPublisher> _logger;

    private readonly SemaphoreSlim _publishLock = new(initialCount: 1, maxCount: 1);

    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqIntegrationEventPublisher(IOptions<RabbitMqOptions> options, ILogger<RabbitMqIntegrationEventPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
        _connectionFactory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost,

            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,

            NetworkRecoveryInterval =
                TimeSpan.FromSeconds(_options.NetworkRecoveryIntervalSeconds)
        };
    }

    public async Task PublishAsync(IntegrationEventEnvelope message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        await _publishLock.WaitAsync(cancellationToken);

        try
        {
            var channel = await GetOrCreateChannelAsync(cancellationToken);

            var routingKey =RabbitMqTopology.GetRoutingKey(message.Type);

            var properties = new BasicProperties
            {
                ContentType = "application/json",
                ContentEncoding = "utf-8",
                MessageId = message.MessageId.ToString(),
                Type = message.Type,
                AppId = "OrderProcessing.Api",
                Persistent = true,
                Timestamp = new AmqpTimestamp(
                    new DateTimeOffset(
                        message.OccurredAtUtc)
                    .ToUnixTimeSeconds())
            };

            var body = Encoding.UTF8.GetBytes(
                message.Payload);

            await channel.BasicPublishAsync(
                exchange: _options.ExchangeName,
                routingKey: routingKey,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Published integration event {MessageId} " +
                "with type {EventType} and routing key {RoutingKey}",
                message.MessageId,
                message.Type,
                routingKey);
        }
        catch
        {
            await ResetConnectionAsync();
            throw;
        }
        finally
        {
            _publishLock.Release();
        }
    }

    private async Task<IChannel> GetOrCreateChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        await ResetConnectionAsync();

        _connection =
            await _connectionFactory.CreateConnectionAsync(
                _options.ClientProvidedName,
                cancellationToken);

        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);

        _channel =
            await _connection.CreateChannelAsync(
                channelOptions,
                cancellationToken);

        await DeclareTopologyAsync(
            _channel,
            cancellationToken);

        _logger.LogInformation(
            "RabbitMQ publisher connected to {HostName}:{Port} " +
            "using exchange {ExchangeName}",    
            _options.HostName,
            _options.Port,
            _options.ExchangeName);

        return _channel;
    }

    private async Task DeclareTopologyAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            exchange: _options.ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        var emailQueueArguments = new Dictionary<string, object?>
        {
            ["x-queue-type"] = "quorum",

            ["x-delivery-limit"] = _options.DeliveryLimit,

            ["x-delayed-retry-type"] = "failed",

            ["x-delayed-retry-min"] = _options.RetryMinDelayMilliseconds,

            ["x-delayed-retry-max"] = _options.RetryMaxDelayMilliseconds,

            ["x-dead-letter-exchange"] = _options.DeadLetterExchangeName,

            ["x-dead-letter-routing-key"] = _options.DeadLetterRoutingKey
        };

        await channel.QueueDeclareAsync(
             queue: _options.EmailQueueName,
             durable: true,
             exclusive: false,
             autoDelete: false,
             arguments: emailQueueArguments,
             cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: _options.EmailQueueName,
            exchange: _options.ExchangeName,
            routingKey:
                RabbitMqTopology.OrderCreatedRoutingKey,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: _options.EmailQueueName,
            exchange: _options.ExchangeName,
            routingKey:
                RabbitMqTopology.OrderCompletedRoutingKey,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: _options.EmailQueueName,
            exchange: _options.ExchangeName,
            routingKey:
                RabbitMqTopology.OrderCancelledRoutingKey,
            cancellationToken: cancellationToken);
    }

    private async ValueTask ResetConnectionAsync()
    {
        var channel = _channel;
        _channel = null;

        if (channel is not null)
        {
            try
            {
                if (channel.IsOpen)
                {
                    await channel.CloseAsync(CancellationToken.None);
                }
            }
            catch (Exception exception)
            {
                _logger.LogDebug(
                    exception,
                    "An error occurred while closing " +
                    "the RabbitMQ channel");
            }

            await channel.DisposeAsync();
        }

        var connection = _connection;
        _connection = null;

        if (connection is not null)
        {
            try
            {
                if (connection.IsOpen)
                {
                    await connection.CloseAsync(CancellationToken.None);
                }
            }
            catch (Exception exception)
            {
                _logger.LogDebug(
                    exception,
                    "An error occurred while closing " +
                    "the RabbitMQ connection");
            }

            await connection.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _publishLock.WaitAsync();

        try
        {
            await ResetConnectionAsync();
        }
        finally
        {
            _publishLock.Release();
            _publishLock.Dispose();
        }
    }
}