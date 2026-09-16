using BankFlow.Contracts.Messaging;

namespace Transfers.Application.Abstractions;

public interface IIntegrationOutbox
{
    public void Enqueue<T>(T message, string routingKey) where T : class, IIntegrationEvent;
}
