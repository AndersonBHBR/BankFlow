using Microsoft.Extensions.Options;

namespace BankFlow.Identity.Api.Auth;

public interface IUserCredentialStore
{
    public DemoUser? FindByLogin(string login);
}

public sealed class ConfigurationUserCredentialStore(IOptions<DemoUserOptions> options) : IUserCredentialStore
{
    private readonly IReadOnlyDictionary<string, DemoUser> _users = options.Value.Users
        .ToDictionary(user => user.Login, StringComparer.OrdinalIgnoreCase);

    public DemoUser? FindByLogin(string login) =>
        _users.GetValueOrDefault(login.Trim());
}
