using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Transfers.Domain.Transfers;

namespace Transfers.Infrastructure.Persistence.Configurations;

public sealed class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        builder.ToTable("Transfers", table =>
        {
            table.HasCheckConstraint("CK_Transfers_Amount", "[Amount] > 0");
            table.HasCheckConstraint("CK_Transfers_Method", "[Method] BETWEEN 1 AND 3");
            table.HasCheckConstraint("CK_Transfers_Status", "[Status] BETWEEN 1 AND 6");
            table.HasCheckConstraint("CK_Transfers_DifferentAccounts", "[SourceAccountId] <> [DestinationAccountId]");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Number).HasMaxLength(40).IsRequired();
        builder.HasIndex(item => item.Number).IsUnique();
        builder.Property(item => item.ExternalReference).HasMaxLength(100).IsRequired();
        builder.HasIndex(item => item.ExternalReference).IsUnique();
        builder.Property(item => item.Amount).HasPrecision(19, 2);
        builder.Property(item => item.Description).HasMaxLength(300).IsRequired();
        builder.Property(item => item.StatusReason).HasMaxLength(500);
        builder.Property(item => item.CreatedBy).HasMaxLength(160).IsRequired();
        builder.Property(item => item.ReversalReason).HasMaxLength(300);
        builder.Property(item => item.ReversalRequestedBy).HasMaxLength(160);
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(item => new { item.SourceAccountId, item.CreatedAtUtc });
        builder.HasIndex(item => new { item.DestinationAccountId, item.CreatedAtUtc });
        builder.HasIndex(item => new { item.Status, item.CreatedAtUtc });
    }
}
