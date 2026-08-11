using Microsoft.EntityFrameworkCore;
using OrderProcessing.EmailWorker.Persistence;
using OrderProcessing.EmailWorker.Persistence.Entities;

namespace OrderProcessing.EmailWorker.Messaging;

public sealed class IdempotentEmailMessageProcessor
{
    private readonly EmailWorkerDbContext _dbContext;
    private readonly OrderEventEmailHandler _eventHandler;
    private readonly ILogger<IdempotentEmailMessageProcessor>
        _logger;

    public IdempotentEmailMessageProcessor(EmailWorkerDbContext dbContext, OrderEventEmailHandler eventHandler, ILogger<IdempotentEmailMessageProcessor> logger)
    {
        _dbContext = dbContext;
        _eventHandler = eventHandler;
        _logger = logger;
    }

    public async Task ProcessAsync(Guid messageId, string eventType, byte[] body, CancellationToken cancellationToken)
    {
        var alreadyProcessed = await _dbContext.ProcessedMessages
                .AsNoTracking()
                .AnyAsync(message => message.MessageId == messageId, cancellationToken);

        if (alreadyProcessed)
        {
            _logger.LogInformation(
                "Skipping duplicate integration event " +
                "{MessageId} of type {EventType}",
                messageId,
                eventType);

            return;
        }

        await _eventHandler.HandleAsync(eventType, body, cancellationToken);

        _dbContext.ProcessedMessages.Add(
            new ProcessedMessage
            {
                MessageId = messageId,
                EventType = eventType,
                ProcessedAtUtc = DateTime.UtcNow
            });

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Integration event {MessageId} recorded " +
            "as successfully processed",
            messageId);
    }
}