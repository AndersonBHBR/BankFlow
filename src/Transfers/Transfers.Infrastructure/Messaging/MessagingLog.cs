using Microsoft.Extensions.Logging;

namespace Transfers.Infrastructure.Messaging;

internal static partial class MessagingLog
{
    [LoggerMessage(EventId = 4101, Level = LogLevel.Warning, Message = "RabbitMQ indisponível no serviço {Worker}; nova tentativa será realizada.")]
    public static partial void RabbitMqUnavailable(ILogger logger, string worker, Exception exception);

    [LoggerMessage(EventId = 4102, Level = LogLevel.Warning, Message = "Falha ao publicar a mensagem {MessageId}; ela continuará pendente na Outbox.")]
    public static partial void PublishFailed(ILogger logger, Guid messageId, Exception exception);

    [LoggerMessage(EventId = 4103, Level = LogLevel.Warning, Message = "Mensagem inválida enviada para a DLQ. Routing key: {RoutingKey}.")]
    public static partial void PoisonMessage(ILogger logger, string routingKey, Exception exception);

    [LoggerMessage(EventId = 4104, Level = LogLevel.Warning, Message = "Falha ao processar {RoutingKey}; requeue: {Requeue}.")]
    public static partial void ProcessingFailed(ILogger logger, string routingKey, bool requeue, Exception exception);
}
