using System.Text.Json;
using BankFlow.Contracts.Messaging;
using Transfers.Application.Abstractions;
using Transfers.Infrastructure.Persistence;
using Transfers.Infrastructure.Persistence.Messaging;

namespace Transfers.Infrastructure.Messaging;

public sealed class IntegrationOutbox(TransfersDbContext dbContext) : IIntegrationOutbox
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public void Enqueue<T>(T message, string routingKey) where T : class, IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(routingKey);

        var type = typeof(T).FullName ?? typeof(T).Name;
        dbContext.OutboxMessages.Add(OutboxMessage.Create(
            message.MessageId,
            message.OccurredAtUtc,
            message.CorrelationId,
            type,
            routingKey,
            JsonSerializer.Serialize(message, SerializerOptions)));
    }
}
