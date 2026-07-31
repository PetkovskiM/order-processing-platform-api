using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OrderProcessing.Api.Data;
using OrderProcessing.Api.Services.Messaging;

namespace OrderProcessing.Api.Services.Outbox;

public sealed class OutboxProcessor
{
    private readonly OrderProcessingDbContext _dbContext;
    private readonly IIntegrationEventPublisher _publisher;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        OrderProcessingDbContext dbContext,
        IIntegrationEventPublisher publisher,
        IOptions<OutboxOptions> options,
        ILogger<OutboxProcessor> logger)
    {
        _dbContext = dbContext;
        _publisher = publisher;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var messages = await _dbContext.OutboxMessages
            .Where(message =>
                message.ProcessedAtUtc == null &&
                message.RetryCount < _options.MaxRetryCount)
            .OrderBy(message => message.OccurredAtUtc)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return 0;
        }

        var processedCount = 0;

        foreach (var message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            message.LastAttemptAtUtc = DateTime.UtcNow;

            try
            {
                var envelope = new IntegrationEventEnvelope(
                    MessageId: message.Id,
                    Type: message.Type,
                    Payload: message.Payload,
                    OccurredAtUtc: message.OccurredAtUtc);

                await _publisher.PublishAsync(envelope, cancellationToken);

                message.ProcessedAtUtc = DateTime.UtcNow;
                message.LastError = null;

                await _dbContext.SaveChangesAsync(cancellationToken);

                processedCount++;

                _logger.LogInformation(
                    "Outbox message {MessageId} processed successfully",
                    message.Id);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                message.RetryCount++;
                message.LastError = TruncateError(exception.Message);

                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogError(
                    exception,
                    "Failed to process outbox message {MessageId}. " +
                    "Retry count is {RetryCount}",
                    message.Id,
                    message.RetryCount);
            }
        }

        return processedCount;
    }

    private static string TruncateError(string error)
    {
        const int maximumLength = 2000;

        return error.Length <= maximumLength
            ? error
            : error[..maximumLength];
    }
}