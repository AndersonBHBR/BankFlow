using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Transfers.Infrastructure.Persistence;

public sealed class TransfersDatabaseHealthCheck(IDbContextFactory<TransfersDbContext> factory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Transfers Database não respondeu à verificação de conexão.");
        }
        catch (DbException exception)
        {
            return HealthCheckResult.Unhealthy("Falha ao verificar Transfers Database.", exception);
        }
        catch (InvalidOperationException exception)
        {
            return HealthCheckResult.Unhealthy("Falha ao verificar Transfers Database.", exception);
        }
    }
}
