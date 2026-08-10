namespace OrderProcessing.EmailWorker.Configuration;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string UserName { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    public string VirtualHost { get; set; } = "/";

    public string ExchangeName { get; set; } = "order-processing.events";

    public string EmailQueueName { get; set; } = "order-processing.email";

    public string ClientProvidedName { get; set; } = "order-processing-email-worker";

    public int NetworkRecoveryIntervalSeconds { get; set; } = 5;

    public ushort PrefetchCount { get; set; } = 1;

    public string DeadLetterExchangeName { get; set; } = "order-processing.dead-letter";

    public string DeadLetterQueueName { get; set; } = "order-processing.email.dead-letter";

    public string DeadLetterRoutingKey { get; set; } = "email.failed";

    public int DeliveryLimit { get; set; } = 5;

    public int RetryMinDelayMilliseconds { get; set; } = 5000;

    public int RetryMaxDelayMilliseconds { get; set; } = 30000;
}