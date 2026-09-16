using Accounts.Domain.Accounts;
using Accounts.Domain.Ledger;

namespace Accounts.Application.Abstractions;

public interface IAccountRepository
{
    public Task<bool> NumberOrPixKeyExistsAsync(string number, string pixKey, CancellationToken cancellationToken);
    public Task<BankAccount?> GetByIdAsync(Guid id, bool forUpdate, CancellationToken cancellationToken);
    public Task<BankAccount?> GetByPixKeyAsync(string pixKey, CancellationToken cancellationToken);
    public Task<(IReadOnlyList<BankAccount> Items, int TotalCount)> ListAsync(
        int page, int pageSize, string? search, AccountStatus? status, CancellationToken cancellationToken);
    public Task<(IReadOnlyList<LedgerEntry> Items, int TotalCount)> ListLedgerAsync(
        Guid accountId, int page, int pageSize, CancellationToken cancellationToken);
    public void Add(BankAccount account);
    public void AddLedgerEntry(LedgerEntry entry);
    public void SetExpectedRowVersion(BankAccount account, byte[] expectedRowVersion);
    public Task<AccountSaveOutcome> SaveChangesAsync(CancellationToken cancellationToken);
}

public enum AccountSaveOutcome
{
    Success,
    DuplicateResource,
    ConcurrencyConflict
}
