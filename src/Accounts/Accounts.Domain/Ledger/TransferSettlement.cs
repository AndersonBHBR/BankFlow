namespace Accounts.Domain.Ledger;

public sealed class TransferSettlement
{
    private TransferSettlement()
    {
    }

    private TransferSettlement(Guid transferId, Guid sourceAccountId, Guid destinationAccountId,
        decimal amount, DateTimeOffset settledAtUtc)
    {
        Id = Guid.CreateVersion7();
        TransferId = transferId;
        SourceAccountId = sourceAccountId;
        DestinationAccountId = destinationAccountId;
        Amount = amount;
        SettledAtUtc = settledAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid TransferId { get; private set; }
    public Guid SourceAccountId { get; private set; }
    public Guid DestinationAccountId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTimeOffset SettledAtUtc { get; private set; }
    public DateTimeOffset? ReversedAtUtc { get; private set; }

    public static TransferSettlement Create(Guid transferId, Guid sourceAccountId,
        Guid destinationAccountId, decimal amount, DateTimeOffset settledAtUtc)
    {
        if (transferId == Guid.Empty || sourceAccountId == Guid.Empty || destinationAccountId == Guid.Empty)
        {
            throw new ArgumentException("Os identificadores da liquidação são obrigatórios.");
        }

        if (sourceAccountId == destinationAccountId || amount <= 0 || decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentException("A liquidação possui contas ou valor inválidos.");
        }

        return new TransferSettlement(transferId, sourceAccountId, destinationAccountId, amount, settledAtUtc);
    }

    public void MarkReversed(DateTimeOffset reversedAtUtc) =>
        ReversedAtUtc ??= reversedAtUtc.ToUniversalTime();
}
