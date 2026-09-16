using Accounts.Domain.Accounts;
using Xunit;

namespace UnitTests;

public sealed class BankAccountTests
{
    [Fact]
    public void Create_WithValidData_OpensActiveAccount()
    {
        var account = CreateAccount(1_000m);

        Assert.Equal(AccountStatus.Active, account.Status);
        Assert.Equal(1_000m, account.Balance);
        Assert.Equal("12345678", account.Number);
        Assert.Equal("cliente@bankflow.dev", account.PixKey);
    }

    [Fact]
    public void Debit_WithoutFunds_IsRejectedAndKeepsBalance()
    {
        var account = CreateAccount(100m);

        Assert.Throws<InvalidOperationException>(() => account.Debit(100.01m));
        Assert.Equal(100m, account.Balance);
    }

    [Fact]
    public void BlockedAccount_CannotTransferButCanReceive()
    {
        var account = CreateAccount(100m);
        account.UpdateLimitsAndStatus(5_000m, 1_000m, AccountStatus.Blocked);

        Assert.Throws<InvalidOperationException>(() => account.Debit(10m));
        Assert.Equal(110m, account.Credit(10m));
    }

    [Fact]
    public void Close_WithPositiveBalance_IsRejected()
    {
        var account = CreateAccount(100m);

        Assert.Throws<InvalidOperationException>(() =>
            account.UpdateLimitsAndStatus(5_000m, 1_000m, AccountStatus.Closed));
    }

    [Fact]
    public void NightLimit_CannotExceedDailyLimit()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateAccount(0m, dailyLimit: 1_000m, nightlyLimit: 1_001m));
    }

    private static BankAccount CreateAccount(decimal balance, decimal dailyLimit = 5_000m,
        decimal nightlyLimit = 1_000m) => BankAccount.Create("123456-78", "holder-001",
            "Cliente BankFlow", "123.456.789-01", "CLIENTE@BANKFLOW.DEV",
            AccountType.Checking, balance, dailyLimit, nightlyLimit, DateTimeOffset.UtcNow);
}
