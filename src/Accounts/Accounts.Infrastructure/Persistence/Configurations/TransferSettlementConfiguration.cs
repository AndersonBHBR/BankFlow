using Accounts.Domain.Ledger;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accounts.Infrastructure.Persistence.Configurations;

public sealed class TransferSettlementConfiguration : IEntityTypeConfiguration<TransferSettlement>
{
    public void Configure(EntityTypeBuilder<TransferSettlement> builder)
    {
        builder.ToTable("TransferSettlements", table =>
            table.HasCheckConstraint("CK_TransferSettlements_Amount", "[Amount] > 0"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Amount).HasPrecision(19, 2);
        builder.HasIndex(item => item.TransferId).IsUnique();
        builder.HasIndex(item => new { item.SourceAccountId, item.SettledAtUtc });
    }
}
