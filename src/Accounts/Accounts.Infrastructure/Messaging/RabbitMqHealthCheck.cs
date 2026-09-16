using System.Net.Sockets;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client.Exceptions;

namespace Accounts.Infrastructure.Messaging;

public sealed class RabbitMqHealthCheck(RabbitMqSettings settings) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var factory = settings.CreateConnectionFactory();
            await using var connection = await factory.CreateConnectionAsync(cancellationToken);
            return connection.IsOpen
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("RabbitMQ não abriu a conexão de prontidão.");
        }
        catch (BrokerUnreachableException exception)
        {
            return Unhealthy(exception);
        }
        catch (IOException exception)
        {
            return Unhealthy(exception);
        }
        catch (SocketException exception)
        {
            return Unhealthy(exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Unhealthy(exception);
        }
    }

    private static HealthCheckResult Unhealthy(Exception exception) =>
        HealthCheckResult.Unhealthy("Falha ao verificar RabbitMQ.", exception);
}
