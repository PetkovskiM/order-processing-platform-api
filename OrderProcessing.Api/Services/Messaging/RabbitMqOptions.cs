namespace OrderProcessing.Api.Services.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public bool Enabled { get; set; }

    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string UserName { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    public string VirtualHost { get; set; } = "/";

    public string ExchangeName { get; set; } = "order-processing.events";

    public string EmailQueueName { get; set; } = "order-processing.email";

    public string ClientProvidedName { get; set; } = "order-processing-api-publisher";

    public int NetworkRecoveryIntervalSeconds { get; set; } = 5;
}