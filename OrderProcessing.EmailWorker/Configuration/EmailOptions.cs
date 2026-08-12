namespace OrderProcessing.EmailWorker.Configuration;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public bool UseSmtp { get; set; }

    public string Host { get; set; } = "smtp.gmail.com";

    public int Port { get; set; } = 587;

    public SmtpSecurityMode SecurityMode { get; set; } = SmtpSecurityMode.StartTls;

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } =
        "Order Processing Platform";
}