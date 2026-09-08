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

    public DbSet<StrategyRecord> Strategies => Set<StrategyRecord>();

    public DbSet<TradingSetupRecord> TradingSetups => Set<TradingSetupRecord>();

    public DbSet<TradingMistakeRecord> TradingMistakes => Set<TradingMistakeRecord>();

    public DbSet<TradeRecord> Trades => Set<TradeRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new InstrumentRecordConfiguration());
        modelBuilder.ApplyConfiguration(new TradingAccountRecordConfiguration());
        modelBuilder.ApplyConfiguration(new StrategyRecordConfiguration());
        modelBuilder.ApplyConfiguration(new TradingSetupRecordConfiguration());
        modelBuilder.ApplyConfiguration(new TradingMistakeRecordConfiguration());
        modelBuilder.ApplyConfiguration(new TradeRecordConfiguration());
    }
}
