namespace OrderProcessing.Api.Services.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public int BatchSize { get; set; } = 20;

    public int MaxRetryCount { get; set; } = 5;

    public int PollingIntervalSeconds { get; set; } = 5;
}