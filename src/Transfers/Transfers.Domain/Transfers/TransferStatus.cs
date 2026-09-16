namespace Transfers.Domain.Transfers;

public enum TransferStatus
{
    PendingProcessing = 1,
    Completed = 2,
    Rejected = 3,
    ReversalPending = 4,
    Reversed = 5,
    ReversalRejected = 6
}

public enum TransferMethod
{
    Pix = 1,
    Internal = 2,
    Ted = 3
}
