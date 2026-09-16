namespace Accounts.Infrastructure.Persistence.Messaging;

public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    private OutboxMessage(
        Guid id,
        DateTimeOffset occurredAtUtc,
        string correlationId,
        string type,
        string routingKey,
        string payload)
    {
        Id = id;
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
        CorrelationId = correlationId;
        Type = type;
        RoutingKey = routingKey;
        Payload = payload;
        NextAttemptAtUtc = occurredAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public string Type { get; private set; } = string.Empty;
    public string RoutingKey { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTimeOffset? PublishedAtUtc { get; private set; }
    public int Attempts { get; private set; }
    public DateTimeOffset NextAttemptAtUtc { get; private set; }
    public string? LastError { get; private set; }

    public static OutboxMessage Create(
        Guid id,
        DateTimeOffset occurredAtUtc,
        string correlationId,
        string type,
        string routingKey,
        string payload) => new(id, occurredAtUtc, correlationId, type, routingKey, payload);

    public void MarkPublished(DateTimeOffset publishedAtUtc)
    {
        PublishedAtUtc = publishedAtUtc.ToUniversalTime();
        LastError = null;
    }

    public void MarkFailed(DateTimeOffset attemptedAtUtc, string error)
    {
        Attempts++;
        var backoffSeconds = Math.Min(60, Math.Pow(2, Math.Min(Attempts, 6)));
        NextAttemptAtUtc = attemptedAtUtc.ToUniversalTime().AddSeconds(backoffSeconds);
        LastError = error.Length <= 2000 ? error : error[..2000];
    }
}
