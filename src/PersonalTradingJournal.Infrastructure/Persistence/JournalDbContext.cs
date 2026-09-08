using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Infrastructure.Persistence.Configurations;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Persistence;

public sealed class JournalDbContext : DbContext
{
    public JournalDbContext(DbContextOptions<JournalDbContext> options)
        : base(options)
    {
    }

    public DbSet<InstrumentRecord> Instruments => Set<InstrumentRecord>();

    public DbSet<TradingAccountRecord> TradingAccounts => Set<TradingAccountRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new InstrumentRecordConfiguration());
        modelBuilder.ApplyConfiguration(new TradingAccountRecordConfiguration());
    }
}
