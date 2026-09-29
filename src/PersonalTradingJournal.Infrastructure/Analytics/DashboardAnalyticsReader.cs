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
                   where browse.Status == TradeStatus.Closed && browse.ClosedAtUtc != null
                   select new { Trade = trade, Browse = browse };
        if (query.TradingAccountId is { } accountId)
            rows = rows.Where(row => row.Trade.TradingAccountId == accountId);
        if (query.InstrumentId is { } instrumentId)
            rows = rows.Where(row => row.Trade.InstrumentId == instrumentId);
        if (query.ClosedFromUtc is { } start)
            rows = rows.Where(row => row.Browse.ClosedAtUtc >= start);
        if (query.ClosedBeforeUtc is { } end)
            rows = rows.Where(row => row.Browse.ClosedAtUtc < end);

        List<TradeAnalyticsFact> facts = await rows.Select(row => new TradeAnalyticsFact(
            row.Trade.Id, row.Browse.Status, row.Browse.OpenedAtUtc, row.Browse.ClosedAtUtc,
            row.Trade.PricingCurrency, row.Trade.TradingSetupId, row.Browse.GrossPnL,
            row.Browse.TotalCosts, row.Browse.NetPnL)).ToListAsync(cancellationToken);
        return DashboardMetricCalculator.Calculate(facts, cancellationToken);
    }
}
