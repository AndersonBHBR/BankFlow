namespace Transfers.Application.Abstractions;

public interface IAccountCatalog
{
    public Task<AccountLookupResult> GetAccountAsync(Guid accountId, string authorization, CancellationToken cancellationToken);
}

public sealed record AccountSnapshot(Guid Id, string Number, string HolderName, string PixKey,
    string Status, decimal Balance, decimal DailyTransferLimit, decimal NightlyTransferLimit);

public sealed record AccountLookupResult(AccountLookupStatus Status, AccountSnapshot? Account, string? Detail);

public enum AccountLookupStatus
{
    Found,
    NotFound,
    Unavailable
}
