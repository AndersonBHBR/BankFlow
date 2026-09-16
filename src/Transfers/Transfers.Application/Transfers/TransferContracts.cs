using Transfers.Domain.Transfers;

namespace Transfers.Application.Transfers;

public sealed record CreateTransferCommand(Guid SourceAccountId, Guid DestinationAccountId,
    string ExternalReference, TransferMethod Method, decimal Amount, string Description,
    string CreatedBy, string Authorization, string CorrelationId);

public sealed record RequestReversalCommand(Guid TransferId, string RowVersion,
    string Reason, string RequestedBy, string CorrelationId);

public sealed record CreateTransferResponse(TransferResponse Transfer, bool IsReplay);

public sealed record TransferResponse(Guid Id, string Number, Guid SourceAccountId,
    Guid DestinationAccountId, string ExternalReference, TransferMethod Method, decimal Amount,
    string Description, TransferStatus Status, string? StatusReason, string CreatedBy,
    DateTimeOffset CreatedAtUtc, DateTimeOffset? CompletedAtUtc, string? ReversalReason,
    string? ReversalRequestedBy, DateTimeOffset? ReversalRequestedAtUtc,
    DateTimeOffset? ReversedAtUtc, string RowVersion)
{
    public static TransferResponse From(Transfer transfer) => new(transfer.Id, transfer.Number,
        transfer.SourceAccountId, transfer.DestinationAccountId, transfer.ExternalReference,
        transfer.Method, transfer.Amount, transfer.Description, transfer.Status, transfer.StatusReason,
        transfer.CreatedBy, transfer.CreatedAtUtc, transfer.CompletedAtUtc, transfer.ReversalReason,
        transfer.ReversalRequestedBy, transfer.ReversalRequestedAtUtc, transfer.ReversedAtUtc,
        Convert.ToBase64String(transfer.RowVersion));
}
