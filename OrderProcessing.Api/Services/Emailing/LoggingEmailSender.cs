namespace OrderProcessing.Api.Services.Emailing;

public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            "Email delivery simulated. Recipient: {Recipient}, Subject: {Subject}, IsHtml: {IsHtml}",
            message.To,
            message.Subject,
            message.IsHtml);

        return Task.CompletedTask;
    }
}