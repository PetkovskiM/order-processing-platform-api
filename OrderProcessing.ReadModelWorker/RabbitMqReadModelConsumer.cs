using Microsoft.Extensions.Options;
using OrderProcessing.Contracts.Orders;
using OrderProcessing.ReadModelWorker.Configuration;
using OrderProcessing.ReadModelWorker.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace OrderProcessing.ReadModelWorker
{
    public class RabbitMqReadModelConsumer : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly RabbitMqOptions _options;
        private readonly ILogger<RabbitMqReadModelConsumer> _logger;

        private IConnection? _connection;
        private IChannel? _channel;
        private string? _consumerTag;

        public RabbitMqReadModelConsumer(
            IServiceScopeFactory scopeFactory,
            IOptions<RabbitMqOptions> options,
            ILogger<RabbitMqReadModelConsumer> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunConsumerAsync(stoppingToken);
                    return;
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "RabbitMQ Read consumer failed to start. " +
                        "Retrying in {RetryIntervalSeconds} seconds",
                        _options.NetworkRecoveryIntervalSeconds);

                    await CloseRabbitMqAsync();

                    await Task.Delay(TimeSpan.FromSeconds(_options.NetworkRecoveryIntervalSeconds), stoppingToken);
                }
            }
        }

        private async Task RunConsumerAsync(CancellationToken stoppingToken)
        {
            var connectionFactory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost,

                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,

                NetworkRecoveryInterval = TimeSpan.FromSeconds(_options.NetworkRecoveryIntervalSeconds),

                ConsumerDispatchConcurrency = 1
            };

            _connection = await connectionFactory.CreateConnectionAsync(_options.ClientProvidedName, stoppingToken);

            _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

            await DeclareTopologyAsync(_channel, stoppingToken);

            await _channel.BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: _options.PrefetchCount,
                global: false,
                cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.ReceivedAsync += async (_, eventArgs) =>
                    await HandleDeliveryAsync(eventArgs);

            _consumerTag =
                await _channel.BasicConsumeAsync(
                    queue: _options.ReadModelQueueName,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

            _logger.LogInformation(
                "RabbitMQ Read consumer is consuming queue {QueueName} " +
                "on RabbitMQ {HostName}:{Port} " +
                "with consumer tag {ConsumerTag}",
                _options.ReadModelQueueName,
                _options.HostName,
                _options.Port,
                _consumerTag);

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "RabbitMQ Read consumer is stopping");
            }
            finally
            {
                await CloseRabbitMqAsync();
            }
        }

        private async Task HandleDeliveryAsync(BasicDeliverEventArgs eventArgs)
        {
            var channel = _channel ?? throw new InvalidOperationException("RabbitMQ channel is not available.");

            // RabbitMQ.Client 7 uses ReadOnlyMemory<byte>.
            // Copy it before this callback returns.
            var body = eventArgs.Body.ToArray();

            var eventType = eventArgs.BasicProperties.Type;
            var messageIdText = eventArgs.BasicProperties.MessageId;

            try
            {
                if (string.IsNullOrWhiteSpace(eventType))
                {
                    throw new UnsupportedIntegrationEventException("<missing>");
                }

                if (!Guid.TryParse(messageIdText, out var messageId))
                {
                    throw new UnsupportedIntegrationEventException($"Invalid message ID '{messageIdText}'.");
                }

                await using var scope = _scopeFactory.CreateAsyncScope();

                var handler = scope.ServiceProvider.GetRequiredService<OrderEventProjectionHandler>();

                await handler.HandleAsync(eventType, body, CancellationToken.None);

                await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);

                _logger.LogInformation(
                    "Acknowledged RabbitMQ message {MessageId} " +
                    "with routing key {RoutingKey}",
                    eventArgs.BasicProperties.MessageId,
                    eventArgs.RoutingKey);
            }
            catch (JsonException exception)
            {
                await RejectPermanentFailureAsync(
                    channel,
                    eventArgs,
                    exception);
            }
            catch (UnsupportedIntegrationEventException exception)
            {
                await RejectPermanentFailureAsync(
                    channel,
                    eventArgs,
                    exception);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Temporary failure processing RabbitMQ " +
                    "message {MessageId}. The message will be retried",
                    eventArgs.BasicProperties.MessageId);

                await channel.BasicRejectAsync(
                    deliveryTag: eventArgs.DeliveryTag,
                    requeue: true);
            }
        }

        private async Task RejectPermanentFailureAsync(
            IChannel channel,
            BasicDeliverEventArgs eventArgs,
            Exception exception)
        {
            _logger.LogError(exception,
            "Permanent failure processing RabbitMQ " +
            "message {MessageId}. " +
            "The message will be dead-lettered",
            eventArgs.BasicProperties.MessageId);

            await channel.BasicRejectAsync(
            deliveryTag: eventArgs.DeliveryTag,
            requeue: false);
        }

        private async Task DeclareTopologyAsync(IChannel channel, CancellationToken cancellationToken)
        {
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

            await channel.ExchangeDeclareAsync(
                exchange: _options.DeadLetterExchangeName,
                type: ExchangeType.Direct,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);

            var deadLetterQueueArguments = new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum"
            };

            await channel.QueueDeclareAsync(
                queue: _options.DeadLetterQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: deadLetterQueueArguments,
                cancellationToken: cancellationToken);

            await channel.QueueBindAsync(
                queue: _options.DeadLetterQueueName,
                exchange: _options.DeadLetterExchangeName,
                routingKey: _options.DeadLetterRoutingKey,
                cancellationToken: cancellationToken);

            await channel.ExchangeDeclareAsync(
                exchange: _options.ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);

            await channel.QueueDeclareAsync(
                queue: _options.ReadModelQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: emailQueueArguments,
                cancellationToken: cancellationToken);

            await channel.QueueBindAsync(
                queue: _options.ReadModelQueueName,
                exchange: _options.ExchangeName,
                routingKey: OrderEventRoutingKeys.Created,
                cancellationToken: cancellationToken);

            await channel.QueueBindAsync(
                queue: _options.ReadModelQueueName,
                exchange: _options.ExchangeName,
                routingKey: OrderEventRoutingKeys.Completed,
                cancellationToken: cancellationToken);

            await channel.QueueBindAsync(
                queue: _options.ReadModelQueueName,
                exchange: _options.ExchangeName,
                routingKey: OrderEventRoutingKeys.Cancelled,
                cancellationToken: cancellationToken);
        }

        private async Task CloseRabbitMqAsync()
        {
            var channel = _channel;
            _channel = null;
            _consumerTag = null;

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
                        "Error closing RabbitMQ consumer channel");
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
                        "Error closing RabbitMQ consumer connection");
                }

                await connection.DisposeAsync();
            }
        }
    }
}
