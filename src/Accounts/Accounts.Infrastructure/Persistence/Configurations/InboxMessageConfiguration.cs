using Accounts.Infrastructure.Persistence.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accounts.Infrastructure.Persistence.Configurations;

public sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("InboxMessages");
        builder.HasKey(message => message.MessageId);
        builder.Property(message => message.MessageId).ValueGeneratedNever();
        builder.Property(message => message.Type).HasMaxLength(500).IsRequired();
        builder.HasIndex(message => message.ProcessedAtUtc);
    }
}
