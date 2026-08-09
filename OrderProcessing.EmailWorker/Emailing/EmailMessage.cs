namespace OrderProcessing.EmailWorker.Emailing;

public sealed record EmailMessage(string Recipient, string Subject, string Body);