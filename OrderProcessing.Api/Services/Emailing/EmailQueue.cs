using System.Threading.Channels;

namespace OrderProcessing.Api.Services.Emailing;

public sealed class EmailQueue : IEmailQueue
{
    private readonly Channel<EmailMessage> _channel;

    public EmailQueue(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        };

        _channel = Channel.CreateBounded<EmailMessage>(options);
    }

    public ValueTask EnqueueAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        return _channel.Writer.WriteAsync(message, cancellationToken);
    }

    public ValueTask<EmailMessage> DequeueAsync(
        CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }
}