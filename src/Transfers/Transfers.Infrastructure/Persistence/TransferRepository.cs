using Microsoft.EntityFrameworkCore;
using Transfers.Application.Abstractions;
using Transfers.Domain.Transfers;

namespace Transfers.Infrastructure.Persistence;

public sealed class TransferRepository(TransfersDbContext dbContext) : ITransferRepository
{
    public Task<Transfer?> GetByIdAsync(Guid transferId, bool forUpdate, CancellationToken cancellationToken)
    {
        var query = forUpdate ? dbContext.Transfers.AsQueryable() : dbContext.Transfers.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == transferId, cancellationToken);
    }

    public Task<Transfer?> GetByExternalReferenceAsync(string externalReference, CancellationToken cancellationToken) =>
        dbContext.Transfers.AsNoTracking().SingleOrDefaultAsync(
            item => item.ExternalReference == externalReference, cancellationToken);

    public async Task<(IReadOnlyList<Transfer> Items, int TotalCount)> ListAsync(int page, int pageSize,
        Guid? accountId, TransferStatus? status, CancellationToken cancellationToken)
    {
        var query = dbContext.Transfers.AsNoTracking();
        if (accountId is not null)
        {
            query = query.Where(item => item.SourceAccountId == accountId || item.DestinationAccountId == accountId);
        }

        if (status is not null)
        {
            query = query.Where(item => item.Status == status);
        }

        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(item => item.CreatedAtUtc).ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);
        return (items, count);
    }

    public void Add(Transfer transfer) => dbContext.Transfers.Add(transfer);
    public void SetExpectedRowVersion(Transfer transfer, byte[] expectedRowVersion) =>
        dbContext.Entry(transfer).Property(item => item.RowVersion).OriginalValue = expectedRowVersion;

    public async Task<TransferSaveOutcome> SaveChangesAsync(CancellationToken cancellationToken)
    {
        var added = dbContext.ChangeTracker.Entries<Transfer>().Any(entry => entry.State == EntityState.Added);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return TransferSaveOutcome.Success;
        }
        catch (DbUpdateConcurrencyException)
        {
            return TransferSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException) when (added)
        {
            return TransferSaveOutcome.DuplicateExternalReference;
        }
    }
}
