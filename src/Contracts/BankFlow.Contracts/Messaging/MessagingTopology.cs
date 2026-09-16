namespace BankFlow.Contracts.Messaging;

public static class MessagingTopology
{
    public const string EventsExchange = "bankflow.events";
    public const string DeadLetterExchange = "bankflow.dead-letter";

    public const string AccountsQueue = "accounts.transfer-commands.v1";
    public const string AccountsDeadLetterQueue = "accounts.transfer-commands.v1.dlq";
    public const string AccountsDeadLetterRoutingKey = "accounts.transfer-commands.dead";

    public const string TransfersQueue = "transfers.settlement-results.v1";
    public const string TransfersDeadLetterQueue = "transfers.settlement-results.v1.dlq";
    public const string TransfersDeadLetterRoutingKey = "transfers.settlement-results.dead";

    public const string TransferRequestedRoutingKey = "transfers.transfer.requested.v1";
    public const string TransferReversalRequestedRoutingKey = "transfers.transfer.reversal-requested.v1";
    public const string TransferCompletedRoutingKey = "accounts.transfer.completed.v1";
    public const string TransferRejectedRoutingKey = "accounts.transfer.rejected.v1";
    public const string TransferReversedRoutingKey = "accounts.transfer.reversed.v1";
    public const string TransferReversalRejectedRoutingKey = "accounts.transfer.reversal-rejected.v1";
}
