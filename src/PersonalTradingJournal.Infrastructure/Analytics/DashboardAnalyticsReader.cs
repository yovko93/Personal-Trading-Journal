using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Analytics;

public sealed class DashboardAnalyticsReader(IDbContextFactory<JournalDbContext> contextFactory)
    : IDashboardAnalyticsReader
{
    public async Task<DashboardAnalyticsSnapshot> GetAsync(
        DashboardAnalyticsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        await using JournalDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Production writes maintain this Domain-derived projection transactionally. Currency and
        // classification belong to the Trade, not today's Instrument/Account reference economics.
        var rows = from trade in context.Trades.AsNoTracking()
                   join browse in context.TradeBrowse.AsNoTracking() on trade.Id equals browse.TradeId
                   join setup in context.TradingSetups.AsNoTracking() on trade.TradingSetupId equals setup.Id into setups
                   from setup in setups.DefaultIfEmpty()
                   join instrument in context.Instruments.AsNoTracking() on trade.InstrumentId equals instrument.Id into instruments
                   from instrument in instruments.DefaultIfEmpty()
                   where browse.Status == TradeStatus.Closed && browse.ClosedAtUtc != null
                   select new { Trade = trade, Browse = browse, Setup = setup, Instrument = instrument };
        if (query.TradingAccountId is { } accountId)
            rows = rows.Where(row => row.Trade.TradingAccountId == accountId);
        if (query.InstrumentId is { } instrumentId)
            rows = rows.Where(row => row.Trade.InstrumentId == instrumentId);
        if (query.ClosedFromUtc is { } start)
            rows = rows.Where(row => row.Browse.ClosedAtUtc >= start);
        if (query.ClosedBeforeUtc is { } end)
            rows = rows.Where(row => row.Browse.ClosedAtUtc < end);

        var selected = await rows.Select(row => new
        {
            Fact = new TradeAnalyticsFact(row.Trade.Id, row.Browse.Status, row.Browse.OpenedAtUtc, row.Browse.ClosedAtUtc,
                row.Trade.PricingCurrency, row.Trade.TradingSetupId, row.Browse.GrossPnL,
                row.Browse.TotalCosts, row.Browse.NetPnL)
                { InstrumentSymbol = row.Instrument == null ? null : row.Instrument.Symbol },
            Setup = row.Setup == null ? null : new TradingSetupAnalyticsReference(
                row.Setup.Id, row.Setup.Name, row.Setup.IsActive)
        }).ToListAsync(cancellationToken);
        // Metadata and facts come from the same SQL snapshot; no per-Setup or per-point queries.
        TradingSetupAnalyticsReference[] references = selected.Where(row => row.Setup is not null)
            .Select(row => row.Setup!).DistinctBy(setup => setup.TradingSetupId).ToArray();
        return DashboardMetricCalculator.Calculate(selected.Select(row => row.Fact), references, cancellationToken);
    }
}
