using Accounts.Domain.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accounts.Infrastructure.Persistence.Configurations;

public sealed class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
{
    public void Configure(EntityTypeBuilder<BankAccount> builder)
    {
        builder.ToTable("Accounts", table =>
        {
            table.HasCheckConstraint("CK_Accounts_Balance", "[Balance] >= 0");
            table.HasCheckConstraint("CK_Accounts_Limits", "[DailyTransferLimit] > 0 AND [NightlyTransferLimit] > 0 AND [NightlyTransferLimit] <= [DailyTransferLimit]");
            table.HasCheckConstraint("CK_Accounts_Status", "[Status] BETWEEN 1 AND 3");
            table.HasCheckConstraint("CK_Accounts_Type", "[Type] BETWEEN 1 AND 2");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Number).HasMaxLength(12).IsRequired();
        builder.HasIndex(item => item.Number).IsUnique();
        builder.Property(item => item.HolderId).HasMaxLength(64).IsRequired();
        builder.Property(item => item.HolderName).HasMaxLength(160).IsRequired();
        builder.Property(item => item.Document).HasMaxLength(14).IsRequired();
        builder.Property(item => item.PixKey).HasMaxLength(140).IsRequired();
        builder.HasIndex(item => item.PixKey).IsUnique();
        builder.Property(item => item.Balance).HasPrecision(19, 2);
        builder.Property(item => item.DailyTransferLimit).HasPrecision(19, 2);
        builder.Property(item => item.NightlyTransferLimit).HasPrecision(19, 2);
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(item => new { item.Status, item.OpenedAtUtc });
    }
}
