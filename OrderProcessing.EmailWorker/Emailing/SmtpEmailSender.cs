using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using OrderProcessing.EmailWorker.Configuration;

namespace OrderProcessing.EmailWorker.Emailing;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var mimeMessage = new MimeMessage();

        mimeMessage.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));

        mimeMessage.To.Add(MailboxAddress.Parse(message.Recipient));

        mimeMessage.Subject = message.Subject;

        mimeMessage.Body = new TextPart("plain")
        {
            Text = message.Body
        };

        using var smtpClient = new MailKit.Net.Smtp.SmtpClient();

        var socketOptions = GetSecureSocketOptions();

        await smtpClient.ConnectAsync(_options.Host, _options.Port, socketOptions, cancellationToken);

        await smtpClient.AuthenticateAsync(_options.UserName, _options.Password, cancellationToken);

        await smtpClient.SendAsync(mimeMessage, cancellationToken);

        await smtpClient.DisconnectAsync(quit: true, cancellationToken);

        _logger.LogInformation(
            "Email sent successfully to {Recipient} " +
            "with subject {Subject}",
            message.Recipient,
            message.Subject);
    }

    private SecureSocketOptions GetSecureSocketOptions()
    {
        return _options.SecurityMode switch
        {
            SmtpSecurityMode.StartTls => SecureSocketOptions.StartTls,

            SmtpSecurityMode.SslOnConnect => SecureSocketOptions.SslOnConnect,

            _ => throw new InvalidOperationException($"Unsupported SMTP security mode '{_options.SecurityMode}'.")
        };
    }
}