using Accounts.Infrastructure.Persistence.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accounts.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).ValueGeneratedNever();
        builder.Property(message => message.CorrelationId).HasMaxLength(128).IsRequired();
        builder.Property(message => message.Type).HasMaxLength(500).IsRequired();
        builder.Property(message => message.RoutingKey).HasMaxLength(200).IsRequired();
        builder.Property(message => message.Payload).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(message => message.LastError).HasMaxLength(2000);
        builder.HasIndex(message => new { message.PublishedAtUtc, message.NextAttemptAtUtc });
    }
}
