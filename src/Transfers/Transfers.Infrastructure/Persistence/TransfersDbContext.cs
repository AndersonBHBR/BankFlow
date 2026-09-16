using Microsoft.EntityFrameworkCore;
using Transfers.Domain.Transfers;
using Transfers.Infrastructure.Persistence.Messaging;

namespace Transfers.Infrastructure.Persistence;

public sealed class TransfersDbContext(DbContextOptions<TransfersDbContext> options) : DbContext(options)
{
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("transfers");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TransfersDbContext).Assembly);
    }
}
