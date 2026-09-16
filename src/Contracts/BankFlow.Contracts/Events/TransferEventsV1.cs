using BankFlow.Contracts.Messaging;

namespace BankFlow.Contracts.Events;

public enum TransferMethodV1
{
    Pix = 1,
    Internal = 2,
    Ted = 3
}

public enum TransferRejectionReasonV1
{
    AccountNotFound = 1,
    AccountBlocked = 2,
    AccountClosed = 3,
    InsufficientFunds = 4,
    DailyLimitExceeded = 5,
    NightlyLimitExceeded = 6,
    InvalidDestination = 7,
    SettlementNotFound = 8,
    AlreadyReversed = 9
}

public sealed record TransferRequestedV1(
    Guid MessageId,
    DateTimeOffset OccurredAtUtc,
    string CorrelationId,
    Guid TransferId,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    TransferMethodV1 Method,
    string Description) : IIntegrationEvent;

public sealed record TransferCompletedV1(
    Guid MessageId,
    DateTimeOffset OccurredAtUtc,
    string CorrelationId,
    Guid TransferId,
    decimal SourceBalance,
    decimal DestinationBalance) : IIntegrationEvent;

public sealed record TransferRejectedV1(
    Guid MessageId,
    DateTimeOffset OccurredAtUtc,
    string CorrelationId,
    Guid TransferId,
    TransferRejectionReasonV1 ReasonCode,
    string Detail) : IIntegrationEvent;

public sealed record TransferReversalRequestedV1(
    Guid MessageId,
    DateTimeOffset OccurredAtUtc,
    string CorrelationId,
    Guid TransferId,
    string Reason,
    string RequestedBy) : IIntegrationEvent;

public sealed record TransferReversedV1(
    Guid MessageId,
    DateTimeOffset OccurredAtUtc,
    string CorrelationId,
    Guid TransferId,
    decimal OriginalSourceBalance,
    decimal OriginalDestinationBalance) : IIntegrationEvent;

public sealed record TransferReversalRejectedV1(
    Guid MessageId,
    DateTimeOffset OccurredAtUtc,
    string CorrelationId,
    Guid TransferId,
    TransferRejectionReasonV1 ReasonCode,
    string Detail) : IIntegrationEvent;
