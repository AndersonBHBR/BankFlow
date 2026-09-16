using Accounts.Domain.Accounts;
using Accounts.Domain.Ledger;
using Microsoft.EntityFrameworkCore;

namespace Accounts.Infrastructure.Persistence;

public static class DemoDataSeeder
{
    public static async Task SeedAsync(AccountsDbContext context, CancellationToken cancellationToken = default)
    {
        if (await context.Accounts.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var accounts = new[]
        {
            BankAccount.Create("10012026", "cliente-anderson", "Anderson Silva", "12345678901",
                "anderson@bankflow.dev", AccountType.Checking, 25_000m, 15_000m, 2_000m, now),
            BankAccount.Create("10022026", "cliente-marina", "Marina Costa", "98765432100",
                "marina@bankflow.dev", AccountType.Checking, 8_500m, 8_000m, 1_000m, now),
            BankAccount.Create("10032026", "cliente-roberto", "Roberto Lima", "45678912300",
                "+5511999992026", AccountType.Savings, 3_200m, 5_000m, 500m, now)
        };
        accounts[2].UpdateLimitsAndStatus(5_000m, 500m, AccountStatus.Blocked);

        context.Accounts.AddRange(accounts);
        context.LedgerEntries.AddRange(accounts.Select(account => LedgerEntry.Create(account.Id, null,
            LedgerEntryType.CashIn, account.Balance, account.Balance, "Saldo inicial para demonstração.",
            "demo-seeder", now)));
        await context.SaveChangesAsync(cancellationToken);
    }
}
