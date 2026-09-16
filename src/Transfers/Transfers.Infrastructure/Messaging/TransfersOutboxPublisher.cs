using System.Net.Sockets;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Transfers.Infrastructure.Persistence;

namespace Transfers.Infrastructure.Messaging;

internal sealed class TransfersOutboxPublisher(
    IDbContextFactory<TransfersDbContext> dbContextFactory,
    RabbitMqSettings settings,
    TimeProvider timeProvider,
    ILogger<TransfersOutboxPublisher> logger) : BackgroundService
{
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
            catch (BrokerUnreachableException exception)
            {
                MessagingLog.RabbitMqUnavailable(logger, nameof(TransfersOutboxPublisher), exception);
            }
            catch (AlreadyClosedException exception)
            {
                MessagingLog.RabbitMqUnavailable(logger, nameof(TransfersOutboxPublisher), exception);
            }
            catch (OperationInterruptedException exception)
            {
                MessagingLog.RabbitMqUnavailable(logger, nameof(TransfersOutboxPublisher), exception);
            }
            catch (IOException exception)
            {
                MessagingLog.RabbitMqUnavailable(logger, nameof(TransfersOutboxPublisher), exception);
            }
            catch (SocketException exception)
            {
                MessagingLog.RabbitMqUnavailable(logger, nameof(TransfersOutboxPublisher), exception);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task RunSessionAsync(CancellationToken cancellationToken)
    {
        var factory = settings.CreateConnectionFactory();
        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);
        await using var channel = await connection.CreateChannelAsync(channelOptions, cancellationToken);
        await RabbitMqTopologyInitializer.DeclareAsync(channel, cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var now = timeProvider.GetUtcNow();
            var messages = await dbContext.OutboxMessages
                .Where(message => message.PublishedAtUtc == null)
                .OrderBy(message => message.OccurredAtUtc)
                .ThenBy(message => message.Id)
                .Take(20)
                .ToArrayAsync(cancellationToken);

            if (messages.Length == 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
                continue;
            }

            if (messages[0].NextAttemptAtUtc > now)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
                continue;
            }

            foreach (var message in messages)
            {
                if (message.NextAttemptAtUtc > now)
                {
                    break;
                }

                var publishSucceeded = true;
                var properties = new BasicProperties
                {
                    ContentType = "application/json",
                    DeliveryMode = DeliveryModes.Persistent,
                    MessageId = message.Id.ToString("D"),
                    CorrelationId = message.CorrelationId,
                    Type = message.Type,
                    Timestamp = new AmqpTimestamp(message.OccurredAtUtc.ToUnixTimeSeconds())
                };

                try
                {
                    using var activity = MessagingTelemetry.StartPublish(message.RoutingKey, message.CorrelationId);
                    await channel.BasicPublishAsync(
                        BankFlow.Contracts.Messaging.MessagingTopology.EventsExchange,
                        message.RoutingKey,
                        mandatory: true,
                        properties,
                        Encoding.UTF8.GetBytes(message.Payload),
                        cancellationToken);
                    message.MarkPublished(timeProvider.GetUtcNow());
                    MessagingTelemetry.RecordPublished(message.RoutingKey);
                }
                catch (PublishException exception)
                {
                    publishSucceeded = false;
                    message.MarkFailed(timeProvider.GetUtcNow(), exception.Message);
                    MessagingTelemetry.RecordFailed(message.RoutingKey);
                    MessagingLog.PublishFailed(logger, message.Id, exception);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                if (!publishSucceeded)
                {
                    break;
                }
            }
        }
    }
}
