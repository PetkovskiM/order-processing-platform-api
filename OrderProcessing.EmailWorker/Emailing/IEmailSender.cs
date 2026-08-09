namespace OrderProcessing.EmailWorker.Emailing;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}