namespace Accounts.Domain.Ledger;

public enum LedgerEntryType
{
    CashIn = 1,
    TransferDebit = 2,
    TransferCredit = 3,
    ReversalDebit = 4,
    ReversalCredit = 5
}

public sealed class LedgerEntry
{
    private LedgerEntry()
    {
    }

    private LedgerEntry(Guid accountId, Guid? transferId, LedgerEntryType type, decimal amount,
        decimal balanceAfter, string description, string performedBy, DateTimeOffset occurredAtUtc)
    {
        Id = Guid.CreateVersion7();
        AccountId = accountId;
        TransferId = transferId;
        Type = type;
        Amount = amount;
        BalanceAfter = balanceAfter;
        Description = description;
        PerformedBy = performedBy;
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public Guid? TransferId { get; private set; }
    public LedgerEntryType Type { get; private set; }
    public decimal Amount { get; private set; }
    public decimal BalanceAfter { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string PerformedBy { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }

    public static LedgerEntry Create(Guid accountId, Guid? transferId, LedgerEntryType type, decimal amount,
        decimal balanceAfter, string description, string performedBy, DateTimeOffset occurredAtUtc)
    {
        if (accountId == Guid.Empty || transferId == Guid.Empty)
        {
            throw new ArgumentException("Os identificadores do lançamento são inválidos.");
        }

        if (!Enum.IsDefined(type) || amount <= 0 || decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentException("O tipo ou valor do lançamento é inválido.");
        }

        return new LedgerEntry(accountId, transferId, type, amount, balanceAfter,
            Normalize(description, 300, "descrição"), Normalize(performedBy, 160, "responsável"), occurredAtUtc);
    }

    private static string Normalize(string value, int maximumLength, string fieldName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException($"A informação de {fieldName} deve possuir no máximo {maximumLength} caracteres.", nameof(value));
        }

        return normalized;
    }
}
