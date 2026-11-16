using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence;

// Explicitly seeds pre-Description schemas without changing their columns or migration history.
internal static class LegacyTradeSchemaFixture
{
    internal static readonly Expression<Func<TradeRecord, TradeRecord>> TradeColumns = t => new TradeRecord
    {
        Id = t.Id, TradingAccountId = t.TradingAccountId, InstrumentId = t.InstrumentId,
        PricingPointValue = t.PricingPointValue, PricingCurrency = t.PricingCurrency,
        TradingSetupId = t.TradingSetupId, CreatedAtUtc = t.CreatedAtUtc, UpdatedAtUtc = t.UpdatedAtUtc
    };

    internal static void SaveBeforeDescription(JournalDbContext db)
    {
        var trades = db.ChangeTracker.Entries<TradeRecord>().Select(e => e.Entity).ToArray();
        var dependents = db.ChangeTracker.Entries().Where(e => e.Entity is TradeExecutionRecord or TradeScreenshotRecord or TradeMistakeRecord).Select(e => e.Entity).ToArray();
        foreach (var entity in trades.Cast<object>().Concat(dependents)) db.Entry(entity).State = EntityState.Detached;
        db.SaveChanges(); // Account, Instrument and classification principals.
        foreach (var trade in trades)
            db.Database.ExecuteSqlInterpolated($"INSERT INTO Trades (Id,TradingAccountId,InstrumentId,PricingPointValue,PricingCurrency,TradingSetupId,CreatedAtUtc,UpdatedAtUtc) VALUES ({trade.Id},{trade.TradingAccountId},{trade.InstrumentId},{trade.PricingPointValue},{trade.PricingCurrency},{trade.TradingSetupId},{trade.CreatedAtUtc.UtcDateTime},{trade.UpdatedAtUtc.UtcDateTime})");
        db.AddRange(dependents);
        db.SaveChanges();
    }
}
