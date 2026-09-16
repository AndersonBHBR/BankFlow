namespace BankFlow.Contracts.Messaging;

public interface IIntegrationEvent
{
    public Guid MessageId { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public string CorrelationId { get; }
}
