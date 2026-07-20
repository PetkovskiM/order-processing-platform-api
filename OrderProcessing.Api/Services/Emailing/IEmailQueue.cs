namespace OrderProcessing.Api.Services.Emailing;

public interface IEmailQueue
{
    ValueTask EnqueueAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default);

    ValueTask<EmailMessage> DequeueAsync(
        CancellationToken cancellationToken = default);
}