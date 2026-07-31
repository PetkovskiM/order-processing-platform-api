using Microsoft.Extensions.Options;
using OrderProcessing.Api.Services.Outbox;

namespace OrderProcessing.Api.BackgroundJobs;

public sealed class OutboxBackgroundService
    : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxBackgroundService> _logger;

    public OutboxBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxOptions> options,
        ILogger<OutboxBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Outbox background service started with polling interval " +
            "{PollingIntervalSeconds} seconds",
            _options.PollingIntervalSeconds);

        try
        {
            // Process existing messages immediately at application startup.
            await ProcessPendingMessagesAsync(stoppingToken);

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.PollingIntervalSeconds));

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Outbox background service is stopping");
        }
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();

            var processedCount = await processor.ProcessPendingAsync(cancellationToken);

            if (processedCount > 0)
            {
                _logger.LogInformation(
                    "Outbox processing cycle completed. " +
                    "{ProcessedCount} messages were processed",
                    processedCount);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "An unexpected error occurred during " +
                "the outbox processing cycle");
        }
    }
}