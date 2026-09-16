using Accounts.Application.Abstractions;
using Accounts.Application.Accounts;
using Accounts.Infrastructure.Persistence;
using Accounts.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Accounts.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAccountsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AccountsDatabase")
            ?? throw new InvalidOperationException("ConnectionStrings:AccountsDatabase é obrigatória.");

        services.AddDbContextFactory<AccountsDbContext>(options =>
            options.UseSqlServer(connectionString, sqlServer =>
                sqlServer.EnableRetryOnFailure(maxRetryCount: 8, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)));

        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<AccountService>();
        services.AddSingleton(RabbitMqSettings.From(configuration));
        services.AddHostedService<AccountsOutboxPublisher>();
        services.AddHostedService<TransferEventsConsumer>();

        services.AddHealthChecks()
            .AddCheck<AccountsDatabaseHealthCheck>("accounts-database", tags: ["ready"])
            .AddCheck<RabbitMqHealthCheck>("accounts-rabbitmq", tags: ["ready"]);

        return services;
    }
}
