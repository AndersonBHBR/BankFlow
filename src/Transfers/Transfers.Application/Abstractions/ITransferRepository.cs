using Transfers.Domain.Transfers;

namespace Transfers.Application.Abstractions;

public interface ITransferRepository
{
    public Task<Transfer?> GetByIdAsync(Guid transferId, bool forUpdate, CancellationToken cancellationToken);
    public Task<Transfer?> GetByExternalReferenceAsync(string externalReference, CancellationToken cancellationToken);
    public Task<(IReadOnlyList<Transfer> Items, int TotalCount)> ListAsync(int page, int pageSize,
        Guid? accountId, TransferStatus? status, CancellationToken cancellationToken);
    public void Add(Transfer transfer);
    public void SetExpectedRowVersion(Transfer transfer, byte[] expectedRowVersion);
    public Task<TransferSaveOutcome> SaveChangesAsync(CancellationToken cancellationToken);
}

public enum TransferSaveOutcome
{
    Success,
    DuplicateExternalReference,
    ConcurrencyConflict
}
