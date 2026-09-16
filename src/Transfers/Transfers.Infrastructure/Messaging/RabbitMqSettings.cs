using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace Transfers.Infrastructure.Messaging;

public sealed record RabbitMqSettings(
    string Host,
    int Port,
    string Username,
    string Password,
    string VirtualHost,
    bool UseTls)
{
    public static RabbitMqSettings From(IConfiguration configuration)
    {
        var host = configuration["RabbitMq:Host"]
            ?? throw new InvalidOperationException("RabbitMq:Host é obrigatório.");
        var username = configuration["RabbitMq:Username"]
            ?? throw new InvalidOperationException("RabbitMq:Username é obrigatório.");
        var password = configuration["RabbitMq:Password"]
            ?? throw new InvalidOperationException("RabbitMq:Password é obrigatório.");
        var portValue = configuration["RabbitMq:Port"];
        var port = string.IsNullOrWhiteSpace(portValue)
            ? AmqpTcpEndpoint.UseDefaultPort
            : int.TryParse(portValue, out var configuredPort) && configuredPort is > 0 and <= 65_535
                ? configuredPort
                : throw new InvalidOperationException("RabbitMq:Port deve ser uma porta TCP válida.");
        var virtualHost = configuration["RabbitMq:VirtualHost"] ?? "/";
        var useTlsValue = configuration["RabbitMq:UseTls"];
        var useTls = string.IsNullOrWhiteSpace(useTlsValue)
            ? false
            : bool.TryParse(useTlsValue, out var configuredUseTls)
                ? configuredUseTls
                : throw new InvalidOperationException("RabbitMq:UseTls deve ser true ou false.");

        return new RabbitMqSettings(host, port, username, password, virtualHost, useTls);
    }

    public ConnectionFactory CreateConnectionFactory() => new()
    {
        HostName = Host,
        Port = Port,
        UserName = Username,
        Password = Password,
        VirtualHost = VirtualHost,
        AutomaticRecoveryEnabled = true,
        TopologyRecoveryEnabled = true,
        NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
        RequestedConnectionTimeout = TimeSpan.FromSeconds(3),
        RequestedHeartbeat = TimeSpan.FromSeconds(15),
        Ssl = new SslOption
        {
            Enabled = UseTls,
            ServerName = Host
        }
    };
}
