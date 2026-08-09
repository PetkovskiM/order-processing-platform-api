namespace OrderProcessing.EmailWorker.Emailing;

public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            "Simulated sending email to {Recipient} " +
            "with subject {Subject}",
            message.Recipient,
            message.Subject);

        return Task.CompletedTask;
    }
}