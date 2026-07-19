namespace OrderProcessing.Api.Services.Emailing;

public sealed record EmailMessage
{
    public required string To { get; init; }

    public required string Subject { get; init; }

    public required string Body { get; init; }

    public bool IsHtml { get; init; }
}