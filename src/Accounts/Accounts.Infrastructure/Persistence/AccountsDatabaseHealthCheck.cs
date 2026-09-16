using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Accounts.Infrastructure.Persistence;

public sealed class AccountsDatabaseHealthCheck(IDbContextFactory<AccountsDbContext> factory) : IHealthCheck
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
                : HealthCheckResult.Unhealthy("Accounts Database não respondeu à verificação de conexão.");
        }
        catch (DbException exception)
        {
            return HealthCheckResult.Unhealthy("Falha ao verificar Accounts Database.", exception);
        }
        catch (InvalidOperationException exception)
        {
            return HealthCheckResult.Unhealthy("Falha ao verificar Accounts Database.", exception);
        }
    }
}
