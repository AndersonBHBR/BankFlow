using Accounts.Application.Abstractions;
using Accounts.Domain.Accounts;
using Accounts.Domain.Ledger;
using Microsoft.EntityFrameworkCore;

namespace Accounts.Infrastructure.Persistence;

public sealed class AccountRepository(AccountsDbContext dbContext) : IAccountRepository
{
    public Task<bool> NumberOrPixKeyExistsAsync(string number, string pixKey, CancellationToken cancellationToken) =>
        dbContext.Accounts.AnyAsync(item => item.Number == number || item.PixKey == pixKey, cancellationToken);

    public Task<BankAccount?> GetByIdAsync(Guid id, bool forUpdate, CancellationToken cancellationToken)
    {
        var query = forUpdate ? dbContext.Accounts.AsQueryable() : dbContext.Accounts.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public Task<BankAccount?> GetByPixKeyAsync(string pixKey, CancellationToken cancellationToken) =>
        dbContext.Accounts.AsNoTracking().SingleOrDefaultAsync(item => item.PixKey == pixKey, cancellationToken);

    public async Task<(IReadOnlyList<BankAccount> Items, int TotalCount)> ListAsync(
        int page, int pageSize, string? search, AccountStatus? status, CancellationToken cancellationToken)
    {
        var query = dbContext.Accounts.AsNoTracking();
        if (search is not null)
        {
            query = query.Where(item => item.Number.Contains(search) || item.HolderName.Contains(search)
                || item.PixKey.Contains(search));
        }

        if (status is not null)
        {
            query = query.Where(item => item.Status == status);
        }

        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(item => item.HolderName).ThenBy(item => item.Number)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);
        return (items, count);
    }

    public async Task<(IReadOnlyList<LedgerEntry> Items, int TotalCount)> ListLedgerAsync(
        Guid accountId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.LedgerEntries.AsNoTracking().Where(item => item.AccountId == accountId);
        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(item => item.OccurredAtUtc).ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);
        return (items, count);
    }

    public void Add(BankAccount account) => dbContext.Accounts.Add(account);
    public void AddLedgerEntry(LedgerEntry entry) => dbContext.LedgerEntries.Add(entry);
    public void SetExpectedRowVersion(BankAccount account, byte[] expectedRowVersion) =>
        dbContext.Entry(account).Property(item => item.RowVersion).OriginalValue = expectedRowVersion;

    public async Task<AccountSaveOutcome> SaveChangesAsync(CancellationToken cancellationToken)
    {
        var hasNewAccount = dbContext.ChangeTracker.Entries<BankAccount>().Any(entry => entry.State == EntityState.Added);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return AccountSaveOutcome.Success;
        }
        catch (DbUpdateConcurrencyException)
        {
            return AccountSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException) when (hasNewAccount)
        {
            return AccountSaveOutcome.DuplicateResource;
        }
    }
}
