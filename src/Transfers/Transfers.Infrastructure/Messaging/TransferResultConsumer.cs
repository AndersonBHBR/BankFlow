using System.Net.Sockets;
using System.Text.Json;
using BankFlow.Contracts.Events;
using BankFlow.Contracts.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using Transfers.Domain.Transfers;
using Transfers.Infrastructure.Persistence;
using Transfers.Infrastructure.Persistence.Messaging;

namespace Transfers.Infrastructure.Messaging;

internal sealed class TransferResultConsumer(IDbContextFactory<TransfersDbContext> dbContextFactory,
    RabbitMqSettings settings, TimeProvider timeProvider, ILogger<TransferResultConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunSessionAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (exception is BrokerUnreachableException or AlreadyClosedException
                or OperationInterruptedException or IOException or SocketException)
            {
                MessagingLog.RabbitMqUnavailable(logger, nameof(TransferResultConsumer), exception);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task RunSessionAsync(CancellationToken cancellationToken)
    {
        var factory = settings.CreateConnectionFactory();
        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await RabbitMqTopologyInitializer.DeclareAsync(channel, cancellationToken);
        await channel.BasicQosAsync(0, 1, false, cancellationToken);
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) => HandleDeliveryAsync(channel, delivery, cancellationToken);
        await channel.BasicConsumeAsync(MessagingTopology.TransfersQueue, false, consumer, cancellationToken);
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }

    private async Task HandleDeliveryAsync(IChannel channel, BasicDeliverEventArgs delivery,
        CancellationToken cancellationToken)
    {
        using var activity = MessagingTelemetry.StartConsume(delivery.RoutingKey, delivery.BasicProperties.CorrelationId);
        try
        {
            switch (delivery.RoutingKey)
            {
                case MessagingTopology.TransferCompletedRoutingKey:
                    await ApplyCompletedAsync(Deserialize<TransferCompletedV1>(delivery), cancellationToken);
                    break;
                case MessagingTopology.TransferRejectedRoutingKey:
                    await ApplyRejectedAsync(Deserialize<TransferRejectedV1>(delivery), cancellationToken);
                    break;
                case MessagingTopology.TransferReversedRoutingKey:
                    await ApplyReversedAsync(Deserialize<TransferReversedV1>(delivery), cancellationToken);
                    break;
                case MessagingTopology.TransferReversalRejectedRoutingKey:
                    await ApplyReversalRejectedAsync(Deserialize<TransferReversalRejectedV1>(delivery), cancellationToken);
                    break;
                default:
                    throw new JsonException($"Routing key desconhecida: {delivery.RoutingKey}.");
            }

            await channel.BasicAckAsync(delivery.DeliveryTag, false, cancellationToken);
            MessagingTelemetry.RecordProcessed(delivery.RoutingKey);
        }
        catch (JsonException exception)
        {
            MessagingTelemetry.RecordFailed(delivery.RoutingKey);
            MessagingLog.PoisonMessage(logger, delivery.RoutingKey, exception);
            await channel.BasicRejectAsync(delivery.DeliveryTag, false, cancellationToken);
        }
        catch (Exception exception) when (exception is DbUpdateException or InvalidOperationException or ArgumentException)
        {
            MessagingTelemetry.RecordFailed(delivery.RoutingKey);
            var requeue = !delivery.Redelivered;
            MessagingLog.ProcessingFailed(logger, delivery.RoutingKey, requeue, exception);
            await channel.BasicNackAsync(delivery.DeliveryTag, false, requeue, cancellationToken);
        }
    }

    private async Task ApplyCompletedAsync(TransferCompletedV1 message, CancellationToken token) =>
        await ApplyAsync(message, transfer => transfer.Complete(message.OccurredAtUtc),
            TransferStatus.PendingProcessing, TransferStatus.Completed, token);

    private async Task ApplyRejectedAsync(TransferRejectedV1 message, CancellationToken token) =>
        await ApplyAsync(message, transfer => transfer.Reject(message.Detail),
            TransferStatus.PendingProcessing, TransferStatus.Rejected, token);

    private async Task ApplyReversedAsync(TransferReversedV1 message, CancellationToken token) =>
        await ApplyAsync(message, transfer => transfer.CompleteReversal(message.OccurredAtUtc),
            TransferStatus.ReversalPending, TransferStatus.Reversed, token);

    private async Task ApplyReversalRejectedAsync(TransferReversalRejectedV1 message, CancellationToken token) =>
        await ApplyAsync(message, transfer => transfer.RejectReversal(message.Detail),
            TransferStatus.ReversalPending, TransferStatus.ReversalRejected, token);

    private async Task ApplyAsync<T>(T message, Action<Transfer> transition,
        TransferStatus expectedState, TransferStatus completedState, CancellationToken cancellationToken)
        where T : class, IIntegrationEvent
    {
        Validate(message);
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await context.InboxMessages.AnyAsync(item => item.MessageId == message.MessageId, cancellationToken))
        {
            return;
        }

        var transferId = message switch
        {
            TransferCompletedV1 item => item.TransferId,
            TransferRejectedV1 item => item.TransferId,
            TransferReversedV1 item => item.TransferId,
            TransferReversalRejectedV1 item => item.TransferId,
            _ => Guid.Empty
        };
        var transfer = await context.Transfers.SingleOrDefaultAsync(item => item.Id == transferId, cancellationToken)
            ?? throw new InvalidOperationException($"Transferência {transferId} não encontrada para atualização.");
        if (transfer.Status != expectedState && transfer.Status != completedState)
        {
            throw new InvalidOperationException($"A transferência {transferId} não aceita o resultado no estado {transfer.Status}.");
        }

        if (transfer.Status == expectedState)
        {
            transition(transfer);
        }

        context.InboxMessages.Add(InboxMessage.Create(message.MessageId,
            typeof(T).FullName ?? typeof(T).Name, timeProvider.GetUtcNow()));
        await context.SaveChangesAsync(cancellationToken);
    }

    private static T Deserialize<T>(BasicDeliverEventArgs delivery) where T : class =>
        JsonSerializer.Deserialize<T>(delivery.Body.Span, SerializerOptions)
        ?? throw new JsonException($"Evento {typeof(T).Name} vazio.");

    private static void Validate(IIntegrationEvent message)
    {
        var transferId = message switch
        {
            TransferCompletedV1 item => item.TransferId,
            TransferRejectedV1 item => item.TransferId,
            TransferReversedV1 item => item.TransferId,
            TransferReversalRejectedV1 item => item.TransferId,
            _ => Guid.Empty
        };
        if (message.MessageId == Guid.Empty || transferId == Guid.Empty
            || string.IsNullOrWhiteSpace(message.CorrelationId) || message.CorrelationId.Length > 128)
        {
            throw new JsonException("O evento de resultado possui dados inválidos.");
        }
    }
}
