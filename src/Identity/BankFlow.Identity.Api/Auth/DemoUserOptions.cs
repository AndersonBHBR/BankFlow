namespace BankFlow.Identity.Api.Auth;

public sealed class DemoUserOptions
{
    public const string SectionName = "DemoUsers";

    public List<DemoUser> Users { get; init; } = [];
}

public sealed class DemoUser
{
    public Guid Id { get; init; }

    public string Login { get; init; } = string.Empty;

    public string PasswordSalt { get; init; } = string.Empty;

    public string PasswordHash { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    public List<string> Roles { get; init; } = [];
}
