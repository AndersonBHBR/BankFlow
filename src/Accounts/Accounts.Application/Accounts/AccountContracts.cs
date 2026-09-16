using Accounts.Domain.Accounts;
using Accounts.Domain.Ledger;

namespace Accounts.Application.Accounts;

public sealed record CreateAccountCommand(string Number, string HolderId, string HolderName,
    string Document, string PixKey, AccountType Type, decimal InitialBalance,
    decimal DailyTransferLimit, decimal NightlyTransferLimit, string PerformedBy);

public sealed record UpdateAccountCommand(Guid AccountId, decimal DailyTransferLimit,
    decimal NightlyTransferLimit, AccountStatus Status, string RowVersion);

public sealed record CashInCommand(Guid AccountId, decimal Amount, string Description, string PerformedBy);

public sealed record AccountResponse(Guid Id, string Number, string HolderId, string HolderName,
    string Document, string PixKey, AccountType Type, AccountStatus Status, decimal Balance,
    decimal DailyTransferLimit, decimal NightlyTransferLimit, DateTimeOffset OpenedAtUtc, string RowVersion)
{
    public static AccountResponse From(BankAccount account) => new(account.Id, account.Number,
        account.HolderId, account.HolderName, MaskDocument(account.Document), account.PixKey,
        account.Type, account.Status, account.Balance, account.DailyTransferLimit,
        account.NightlyTransferLimit, account.OpenedAtUtc, Convert.ToBase64String(account.RowVersion));

    private static string MaskDocument(string value) => value.Length == 11
        ? $"***.***.{value[6..9]}-**"
        : $"**.***.{value[5..8]}/****-**";
}

public sealed record LedgerEntryResponse(Guid Id, Guid AccountId, Guid? TransferId,
    LedgerEntryType Type, decimal Amount, decimal BalanceAfter, string Description,
    string PerformedBy, DateTimeOffset OccurredAtUtc)
{
    public static LedgerEntryResponse From(LedgerEntry entry) => new(entry.Id, entry.AccountId,
        entry.TransferId, entry.Type, entry.Amount, entry.BalanceAfter, entry.Description,
        entry.PerformedBy, entry.OccurredAtUtc);
}

public sealed record CashInResponse(AccountResponse Account, LedgerEntryResponse Entry);
