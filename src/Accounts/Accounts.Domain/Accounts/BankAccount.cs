using System.Globalization;
using System.Text.RegularExpressions;

namespace Accounts.Domain.Accounts;

public enum AccountType
{
    Checking = 1,
    Savings = 2
}

public enum AccountStatus
{
    Active = 1,
    Blocked = 2,
    Closed = 3
}

public sealed partial class BankAccount
{
    private BankAccount()
    {
    }

    private BankAccount(
        Guid id,
        string number,
        string holderId,
        string holderName,
        string document,
        string pixKey,
        AccountType type,
        decimal initialBalance,
        decimal dailyTransferLimit,
        decimal nightlyTransferLimit,
        DateTimeOffset openedAtUtc)
    {
        Id = id;
        Number = number;
        HolderId = holderId;
        HolderName = holderName;
        Document = document;
        PixKey = pixKey;
        Type = type;
        Status = AccountStatus.Active;
        Balance = initialBalance;
        DailyTransferLimit = dailyTransferLimit;
        NightlyTransferLimit = nightlyTransferLimit;
        OpenedAtUtc = openedAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public string HolderId { get; private set; } = string.Empty;
    public string HolderName { get; private set; } = string.Empty;
    public string Document { get; private set; } = string.Empty;
    public string PixKey { get; private set; } = string.Empty;
    public AccountType Type { get; private set; }
    public AccountStatus Status { get; private set; }
    public decimal Balance { get; private set; }
    public decimal DailyTransferLimit { get; private set; }
    public decimal NightlyTransferLimit { get; private set; }
    public DateTimeOffset OpenedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static BankAccount Create(
        string number,
        string holderId,
        string holderName,
        string document,
        string pixKey,
        AccountType type,
        decimal initialBalance,
        decimal dailyTransferLimit,
        decimal nightlyTransferLimit,
        DateTimeOffset openedAtUtc)
    {
        ValidateMoney(initialBalance, nameof(initialBalance), allowZero: true, maximum: 1_000_000_000m);
        ValidateLimits(dailyTransferLimit, nightlyTransferLimit);
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), "O tipo da conta é inválido.");
        }

        return new BankAccount(
            Guid.CreateVersion7(),
            NormalizeNumber(number),
            NormalizeRequired(holderId, 64, "identificador do titular"),
            NormalizeRequired(holderName, 160, "nome do titular"),
            NormalizeDocument(document),
            NormalizePixKey(pixKey),
            type,
            initialBalance,
            dailyTransferLimit,
            nightlyTransferLimit,
            openedAtUtc);
    }

    public void UpdateLimitsAndStatus(
        decimal dailyTransferLimit,
        decimal nightlyTransferLimit,
        AccountStatus status)
    {
        ValidateLimits(dailyTransferLimit, nightlyTransferLimit);
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), "A situação da conta é inválida.");
        }

        if (Status == AccountStatus.Closed && status != AccountStatus.Closed)
        {
            throw new InvalidOperationException("Uma conta encerrada não pode ser reativada.");
        }

        if (status == AccountStatus.Closed && Balance != 0)
        {
            throw new InvalidOperationException("A conta só pode ser encerrada com saldo igual a zero.");
        }

        DailyTransferLimit = dailyTransferLimit;
        NightlyTransferLimit = nightlyTransferLimit;
        Status = status;
    }

    public decimal Credit(decimal amount)
    {
        EnsureCanReceive();
        ValidateMoney(amount, nameof(amount), allowZero: false, maximum: 50_000_000m);
        Balance += amount;
        return Balance;
    }

    public decimal Debit(decimal amount)
    {
        EnsureCanTransfer();
        ValidateMoney(amount, nameof(amount), allowZero: false, maximum: 50_000_000m);
        if (Balance < amount)
        {
            throw new InvalidOperationException("Saldo disponível insuficiente.");
        }

        Balance -= amount;
        return Balance;
    }

    public static string NormalizeNumber(string number)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        var normalized = number.Trim().Replace("-", string.Empty, StringComparison.Ordinal);
        if (normalized.Length is < 6 or > 12 || !normalized.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("O número da conta deve possuir entre 6 e 12 dígitos.", nameof(number));
        }

        return normalized;
    }

    public static string NormalizePixKey(string pixKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pixKey);
        var normalized = pixKey.Trim().ToLowerInvariant();
        if (normalized.Length is < 3 or > 140)
        {
            throw new ArgumentException("A chave Pix deve possuir entre 3 e 140 caracteres.", nameof(pixKey));
        }

        return normalized;
    }

    private void EnsureCanTransfer()
    {
        if (Status != AccountStatus.Active)
        {
            throw new InvalidOperationException("A conta de origem precisa estar ativa para transferir.");
        }
    }

    private void EnsureCanReceive()
    {
        if (Status == AccountStatus.Closed)
        {
            throw new InvalidOperationException("Uma conta encerrada não pode receber valores.");
        }
    }

    private static void ValidateLimits(decimal daily, decimal nightly)
    {
        ValidateMoney(daily, nameof(daily), allowZero: false, maximum: 50_000_000m);
        ValidateMoney(nightly, nameof(nightly), allowZero: false, maximum: 50_000_000m);
        if (nightly > daily)
        {
            throw new ArgumentException("O limite noturno não pode superar o limite diário.", nameof(nightly));
        }
    }

    private static void ValidateMoney(decimal value, string parameter, bool allowZero, decimal maximum)
    {
        if (value < (allowZero ? 0m : 0.01m) || value > maximum || decimal.Round(value, 2) != value)
        {
            throw new ArgumentOutOfRangeException(
                parameter,
                $"O valor deve ficar entre {(allowZero ? "0,00" : "0,01")} e {maximum.ToString("N2", CultureInfo.GetCultureInfo("pt-BR"))}, com no máximo duas casas decimais.");
        }
    }

    private static string NormalizeDocument(string document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(document);
        var normalized = NonDigitRegex().Replace(document, string.Empty);
        if (normalized.Length is not (11 or 14))
        {
            throw new ArgumentException("O documento deve conter 11 dígitos (CPF) ou 14 dígitos (CNPJ).", nameof(document));
        }

        return normalized;
    }

    private static string NormalizeRequired(string value, int maximumLength, string fieldName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length < 2 || normalized.Length > maximumLength)
        {
            throw new ArgumentException($"O {fieldName} deve possuir entre 2 e {maximumLength} caracteres.", nameof(value));
        }

        return normalized;
    }

    [GeneratedRegex("[^0-9]", RegexOptions.CultureInvariant)]
    private static partial Regex NonDigitRegex();
}
