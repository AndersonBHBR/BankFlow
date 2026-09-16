namespace BankFlow.ServiceDefaults;

public static class SecurityRoles
{
    public const string TransfersUser = "transfers.user";
    public const string AccountsManager = "accounts.manager";
    public const string Admin = "admin";
}

public static class SecurityPolicies
{
    public const string Authenticated = "authenticated";
    public const string Transfers = "transfers";
    public const string Accounts = "accounts";
    public const string Administrator = "administrator";
}
