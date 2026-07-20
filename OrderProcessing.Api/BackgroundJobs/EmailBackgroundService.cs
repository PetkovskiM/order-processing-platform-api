using OrderProcessing.Api.Services.Emailing;

namespace OrderProcessing.Api.BackgroundJobs;

public sealed class EmailBackgroundService : BackgroundService
{
    private readonly IEmailQueue _emailQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailBackgroundService> _logger;

    public EmailBackgroundService(
        IEmailQueue emailQueue,
        IServiceScopeFactory scopeFactory,
        ILogger<EmailBackgroundService> logger)
    {
        _emailQueue = emailQueue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            EmailMessage message;

            try
            {
                message = await _emailQueue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();

                var emailSender = scope.ServiceProvider
                    .GetRequiredService<IEmailSender>();

                await emailSender.SendAsync(message, stoppingToken);

                _logger.LogInformation(
                    "Queued email processed successfully. Recipient: {Recipient}, Subject: {Subject}",
                    message.To,
                    message.Subject);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to process queued email. Recipient: {Recipient}, Subject: {Subject}",
                    message.To,
                    message.Subject);
            }
        }

        _logger.LogInformation("Email background service stopped");
    }
}