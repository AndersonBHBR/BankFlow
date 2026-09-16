using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Transfers.Application.Abstractions;
using Transfers.Application.Transfers;
using Transfers.Infrastructure.Accounts;
using Transfers.Infrastructure.Messaging;
using Transfers.Infrastructure.Persistence;

namespace Transfers.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTransfersInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("TransfersDatabase")
            ?? throw new InvalidOperationException("ConnectionStrings:TransfersDatabase é obrigatória.");
        var accountsBaseUrl = configuration["Services:AccountsBaseUrl"]
            ?? throw new InvalidOperationException("Services:AccountsBaseUrl é obrigatória.");
        if (!Uri.TryCreate(accountsBaseUrl, UriKind.Absolute, out var accountsUri))
        {
            throw new InvalidOperationException("Services:AccountsBaseUrl deve ser uma URL absoluta.");
        }

        services.AddDbContextFactory<TransfersDbContext>(options =>
            options.UseSqlServer(connectionString, sqlServer =>
                sqlServer.EnableRetryOnFailure(maxRetryCount: 8, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)));

        services
            .AddHttpClient<IAccountCatalog, AccountCatalogClient>(client =>
            {
                client.BaseAddress = accountsUri;
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .AddStandardResilienceHandler(options =>
            {
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
                options.Retry.MaxRetryAttempts = 2;
                options.CircuitBreaker.MinimumThroughput = 5;
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
            });
        services.AddScoped<ITransferRepository, TransferRepository>();
        services.AddScoped<IIntegrationOutbox, IntegrationOutbox>();
        services.AddScoped<TransferService>();
        services.AddSingleton(RabbitMqSettings.From(configuration));
        services.AddHostedService<TransfersOutboxPublisher>();
        services.AddHostedService<TransferResultConsumer>();

        services.AddHealthChecks()
            .AddCheck<TransfersDatabaseHealthCheck>("transfers-database", tags: ["ready"])
            .AddCheck<RabbitMqHealthCheck>("transfers-rabbitmq", tags: ["ready"]);

        return services;
    }
}
