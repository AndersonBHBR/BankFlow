using Accounts.Domain.Accounts;
using Accounts.Domain.Ledger;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accounts.Infrastructure.Persistence.Configurations;

public sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("LedgerEntries", table =>
        {
            table.HasCheckConstraint("CK_LedgerEntries_Amount", "[Amount] > 0");
            table.HasCheckConstraint("CK_LedgerEntries_Balance", "[BalanceAfter] >= 0");
            table.HasCheckConstraint("CK_LedgerEntries_Type", "[Type] BETWEEN 1 AND 5");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Amount).HasPrecision(19, 2);
        builder.Property(item => item.BalanceAfter).HasPrecision(19, 2);
        builder.Property(item => item.Description).HasMaxLength(300).IsRequired();
        builder.Property(item => item.PerformedBy).HasMaxLength(160).IsRequired();
        builder.HasIndex(item => new { item.AccountId, item.OccurredAtUtc });
        builder.HasIndex(item => new { item.TransferId, item.Type }).IsUnique().HasFilter("[TransferId] IS NOT NULL");
        builder.HasOne<BankAccount>().WithMany().HasForeignKey(item => item.AccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
