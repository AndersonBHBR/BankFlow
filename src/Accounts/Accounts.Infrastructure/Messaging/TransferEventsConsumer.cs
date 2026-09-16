using System.Net.Sockets;
using System.Text.Json;
using Accounts.Domain.Accounts;
using Accounts.Domain.Ledger;
using Accounts.Infrastructure.Persistence;
using Accounts.Infrastructure.Persistence.Messaging;
using BankFlow.Contracts.Events;
using BankFlow.Contracts.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Accounts.Infrastructure.Messaging;

internal sealed class TransferEventsConsumer(
    IDbContextFactory<AccountsDbContext> dbContextFactory,
    RabbitMqSettings settings,
    TimeProvider timeProvider,
    ILogger<TransferEventsConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan BusinessTimeOffset = TimeSpan.FromHours(-3);

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
                MessagingLog.RabbitMqUnavailable(logger, nameof(TransferEventsConsumer), exception);
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
        await channel.BasicQosAsync(0, 1, global: false, cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) => HandleDeliveryAsync(channel, delivery, cancellationToken);
        await channel.BasicConsumeAsync(MessagingTopology.AccountsQueue, autoAck: false, consumer, cancellationToken);
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
                case MessagingTopology.TransferRequestedRoutingKey:
                    var requested = JsonSerializer.Deserialize<TransferRequestedV1>(delivery.Body.Span, SerializerOptions)
                        ?? throw new JsonException("Evento de transferência vazio.");
                    Validate(requested);
                    await SettleAsync(requested, cancellationToken);
                    break;
                case MessagingTopology.TransferReversalRequestedRoutingKey:
                    var reversal = JsonSerializer.Deserialize<TransferReversalRequestedV1>(delivery.Body.Span, SerializerOptions)
                        ?? throw new JsonException("Evento de estorno vazio.");
                    Validate(reversal);
                    await ReverseAsync(reversal, cancellationToken);
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

    private async Task SettleAsync(TransferRequestedV1 message, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await IsProcessedAsync(dbContext, message.MessageId, cancellationToken))
        {
            return;
        }

        var existingSettlement = await dbContext.TransferSettlements.AsNoTracking()
            .SingleOrDefaultAsync(item => item.TransferId == message.TransferId, cancellationToken);
        if (existingSettlement is not null)
        {
            dbContext.InboxMessages.Add(CreateInbox(message));
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var accounts = await dbContext.Accounts
            .Where(item => item.Id == message.SourceAccountId || item.Id == message.DestinationAccountId)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var rejection = await ValidateSettlementAsync(dbContext, message, accounts, cancellationToken);
        var now = timeProvider.GetUtcNow();

        if (rejection is not null)
        {
            AddOutbox(dbContext, new TransferRejectedV1(Guid.CreateVersion7(), now,
                message.CorrelationId, message.TransferId, rejection.Value.Code, rejection.Value.Detail));
        }
        else
        {
            var source = accounts[message.SourceAccountId];
            var destination = accounts[message.DestinationAccountId];
            var sourceBalance = source.Debit(message.Amount);
            var destinationBalance = destination.Credit(message.Amount);
            dbContext.LedgerEntries.AddRange(
                LedgerEntry.Create(source.Id, message.TransferId, LedgerEntryType.TransferDebit,
                    message.Amount, sourceBalance, message.Description, "transfer-settlement", now),
                LedgerEntry.Create(destination.Id, message.TransferId, LedgerEntryType.TransferCredit,
                    message.Amount, destinationBalance, message.Description, "transfer-settlement", now));
            dbContext.TransferSettlements.Add(TransferSettlement.Create(message.TransferId,
                source.Id, destination.Id, message.Amount, now));
            AddOutbox(dbContext, new TransferCompletedV1(Guid.CreateVersion7(), now,
                message.CorrelationId, message.TransferId, sourceBalance, destinationBalance));
        }

        dbContext.InboxMessages.Add(CreateInbox(message));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ReverseAsync(TransferReversalRequestedV1 message, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await IsProcessedAsync(dbContext, message.MessageId, cancellationToken))
        {
            return;
        }

        var settlement = await dbContext.TransferSettlements
            .SingleOrDefaultAsync(item => item.TransferId == message.TransferId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (settlement is null)
        {
            AddOutbox(dbContext, ReversalRejected(message, now,
                TransferRejectionReasonV1.SettlementNotFound, "A liquidação original não foi encontrada."));
        }
        else if (settlement.ReversedAtUtc is not null)
        {
            AddOutbox(dbContext, ReversalRejected(message, now,
                TransferRejectionReasonV1.AlreadyReversed, "A transferência já foi estornada."));
        }
        else
        {
            var accounts = await dbContext.Accounts
                .Where(item => item.Id == settlement.SourceAccountId || item.Id == settlement.DestinationAccountId)
                .ToDictionaryAsync(item => item.Id, cancellationToken);
            if (!accounts.TryGetValue(settlement.SourceAccountId, out var originalSource)
                || !accounts.TryGetValue(settlement.DestinationAccountId, out var originalDestination))
            {
                AddOutbox(dbContext, ReversalRejected(message, now,
                    TransferRejectionReasonV1.AccountNotFound, "Uma das contas da liquidação não foi encontrada."));
            }
            else if (originalDestination.Status != AccountStatus.Active)
            {
                AddOutbox(dbContext, ReversalRejected(message, now,
                    TransferRejectionReasonV1.AccountBlocked, "A conta que devolveria o valor não está ativa."));
            }
            else if (originalDestination.Balance < settlement.Amount)
            {
                AddOutbox(dbContext, ReversalRejected(message, now,
                    TransferRejectionReasonV1.InsufficientFunds, "A conta destinatária não possui saldo para o estorno."));
            }
            else
            {
                var destinationBalance = originalDestination.Debit(settlement.Amount);
                var sourceBalance = originalSource.Credit(settlement.Amount);
                dbContext.LedgerEntries.AddRange(
                    LedgerEntry.Create(originalDestination.Id, message.TransferId, LedgerEntryType.ReversalDebit,
                        settlement.Amount, destinationBalance, message.Reason, message.RequestedBy, now),
                    LedgerEntry.Create(originalSource.Id, message.TransferId, LedgerEntryType.ReversalCredit,
                        settlement.Amount, sourceBalance, message.Reason, message.RequestedBy, now));
                settlement.MarkReversed(now);
                AddOutbox(dbContext, new TransferReversedV1(Guid.CreateVersion7(), now,
                    message.CorrelationId, message.TransferId, sourceBalance, destinationBalance));
            }
        }

        dbContext.InboxMessages.Add(CreateInbox(message));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<(TransferRejectionReasonV1 Code, string Detail)?> ValidateSettlementAsync(
        AccountsDbContext dbContext, TransferRequestedV1 message,
        Dictionary<Guid, BankAccount> accounts, CancellationToken cancellationToken)
    {
        if (!accounts.TryGetValue(message.SourceAccountId, out var source)
            || !accounts.TryGetValue(message.DestinationAccountId, out var destination))
        {
            return (TransferRejectionReasonV1.AccountNotFound, "A conta de origem ou destino não foi encontrada.");
        }

        if (source.Status == AccountStatus.Blocked)
        {
            return (TransferRejectionReasonV1.AccountBlocked, "A conta de origem está bloqueada.");
        }

        if (source.Status == AccountStatus.Closed || destination.Status == AccountStatus.Closed)
        {
            return (TransferRejectionReasonV1.AccountClosed, "Uma das contas está encerrada.");
        }

        if (source.Balance < message.Amount)
        {
            return (TransferRejectionReasonV1.InsufficientFunds, "Saldo disponível insuficiente.");
        }

        var nowBusiness = timeProvider.GetUtcNow().ToOffset(BusinessTimeOffset);
        var startBusinessDay = new DateTimeOffset(nowBusiness.Date, BusinessTimeOffset).ToUniversalTime();
        var spentToday = await dbContext.LedgerEntries.AsNoTracking()
            .Where(item => item.AccountId == source.Id && item.Type == LedgerEntryType.TransferDebit
                && item.OccurredAtUtc >= startBusinessDay)
            .SumAsync(item => item.Amount, cancellationToken);
        if (spentToday + message.Amount > source.DailyTransferLimit)
        {
            return (TransferRejectionReasonV1.DailyLimitExceeded, "O limite diário de transferências seria excedido.");
        }

        var isNight = nowBusiness.Hour >= 20 || nowBusiness.Hour < 6;
        if (isNight)
        {
            var nightStartDate = nowBusiness.Hour >= 20 ? nowBusiness.Date : nowBusiness.Date.AddDays(-1);
            var nightStart = new DateTimeOffset(nightStartDate.AddHours(20), BusinessTimeOffset).ToUniversalTime();
            var spentAtNight = await dbContext.LedgerEntries.AsNoTracking()
                .Where(item => item.AccountId == source.Id && item.Type == LedgerEntryType.TransferDebit
                    && item.OccurredAtUtc >= nightStart)
                .SumAsync(item => item.Amount, cancellationToken);
            if (spentAtNight + message.Amount > source.NightlyTransferLimit)
            {
                return (TransferRejectionReasonV1.NightlyLimitExceeded, "O limite acumulado do período noturno seria excedido.");
            }
        }

        return null;
    }

    private static void Validate(TransferRequestedV1 message)
    {
        if (message.MessageId == Guid.Empty || message.TransferId == Guid.Empty
            || message.SourceAccountId == Guid.Empty || message.DestinationAccountId == Guid.Empty
            || message.SourceAccountId == message.DestinationAccountId || message.Amount <= 0
            || decimal.Round(message.Amount, 2) != message.Amount || !Enum.IsDefined(message.Method)
            || string.IsNullOrWhiteSpace(message.CorrelationId) || string.IsNullOrWhiteSpace(message.Description))
        {
            throw new JsonException("O evento TransferRequestedV1 possui dados inválidos.");
        }
    }

    private static void Validate(TransferReversalRequestedV1 message)
    {
        if (message.MessageId == Guid.Empty || message.TransferId == Guid.Empty
            || string.IsNullOrWhiteSpace(message.CorrelationId) || string.IsNullOrWhiteSpace(message.Reason)
            || string.IsNullOrWhiteSpace(message.RequestedBy))
        {
            throw new JsonException("O evento TransferReversalRequestedV1 possui dados inválidos.");
        }
    }

    private static TransferReversalRejectedV1 ReversalRejected(TransferReversalRequestedV1 message,
        DateTimeOffset now, TransferRejectionReasonV1 reason, string detail) =>
        new(Guid.CreateVersion7(), now, message.CorrelationId, message.TransferId, reason, detail);

    private static Task<bool> IsProcessedAsync(AccountsDbContext context, Guid id, CancellationToken token) =>
        context.InboxMessages.AnyAsync(item => item.MessageId == id, token);

    private InboxMessage CreateInbox(IIntegrationEvent message) => InboxMessage.Create(message.MessageId,
        message.GetType().FullName ?? message.GetType().Name, timeProvider.GetUtcNow());

    private static void AddOutbox<T>(AccountsDbContext context, T message) where T : class, IIntegrationEvent
    {
        var routingKey = message switch
        {
            TransferCompletedV1 => MessagingTopology.TransferCompletedRoutingKey,
            TransferRejectedV1 => MessagingTopology.TransferRejectedRoutingKey,
            TransferReversedV1 => MessagingTopology.TransferReversedRoutingKey,
            TransferReversalRejectedV1 => MessagingTopology.TransferReversalRejectedRoutingKey,
            _ => throw new InvalidOperationException("Evento de saída não suportado.")
        };
        context.OutboxMessages.Add(OutboxMessage.Create(message.MessageId, message.OccurredAtUtc,
            message.CorrelationId, typeof(T).FullName ?? typeof(T).Name, routingKey,
            JsonSerializer.Serialize(message, SerializerOptions)));
    }
}
