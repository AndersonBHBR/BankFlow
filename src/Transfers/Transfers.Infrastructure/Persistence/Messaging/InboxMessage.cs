namespace Transfers.Infrastructure.Persistence.Messaging;

public sealed class InboxMessage
{
    private InboxMessage()
    {
    }

    private InboxMessage(Guid messageId, string type, DateTimeOffset receivedAtUtc)
    {
        MessageId = messageId;
        Type = type;
        ReceivedAtUtc = receivedAtUtc.ToUniversalTime();
        ProcessedAtUtc = receivedAtUtc.ToUniversalTime();
    }

    public Guid MessageId { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public DateTimeOffset ReceivedAtUtc { get; private set; }

    public DateTimeOffset ProcessedAtUtc { get; private set; }

    public static InboxMessage Create(Guid messageId, string type, DateTimeOffset receivedAtUtc) =>
        new(messageId, type, receivedAtUtc);
}
