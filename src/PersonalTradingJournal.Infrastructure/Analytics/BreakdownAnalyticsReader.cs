using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.Analytics.Breakdown;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Analytics;

public sealed class BreakdownAnalyticsReader(IDbContextFactory<JournalDbContext> contextFactory)
    : IBreakdownAnalyticsReader
{
    public async Task<BreakdownAnalyticsSnapshot> GetAsync(BreakdownQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        List<BreakdownTradeFact> facts;
        await using (JournalDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken))
        {
            // One SELECT provides SQLite statement-snapshot consistency for membership,
            // Domain-derived financial projections and all display metadata. No collection
            // joins, separate counts/reference reads, active filters or browse-page limits.
            var rows = from trade in context.Trades.AsNoTracking()
                       join browse in context.TradeBrowse.AsNoTracking() on trade.Id equals browse.TradeId
                       join account in context.TradingAccounts.AsNoTracking() on trade.TradingAccountId equals account.Id into accounts
                       from account in accounts.DefaultIfEmpty()
                       join instrument in context.Instruments.AsNoTracking() on trade.InstrumentId equals instrument.Id into instruments
                       from instrument in instruments.DefaultIfEmpty()
                       join setup in context.TradingSetups.AsNoTracking() on trade.TradingSetupId equals setup.Id into setups
                       from setup in setups.DefaultIfEmpty()
                       where browse.Status == TradeStatus.Closed && browse.ClosedAtUtc != null
                           && trade.PricingCurrency == query.Currency
                       select new { Trade = trade, Browse = browse, Account = account, Instrument = instrument, Setup = setup };
            var filter = query.TradeFilter;
            if (filter.TradingAccountId is { } accountId)
                rows = rows.Where(row => row.Trade.TradingAccountId == accountId);
            if (filter.InstrumentId is { } instrumentId)
                rows = rows.Where(row => row.Trade.InstrumentId == instrumentId);
            if (filter.ClosedFromUtc is { } start)
                rows = rows.Where(row => row.Browse.ClosedAtUtc >= start);
            if (filter.ClosedBeforeUtc is { } end)
                rows = rows.Where(row => row.Browse.ClosedAtUtc < end);

            facts = await rows.Select(row => new BreakdownTradeFact(
                new TradeAnalyticsFact(row.Trade.Id, row.Browse.Status, row.Browse.OpenedAtUtc, row.Browse.ClosedAtUtc,
                    row.Trade.PricingCurrency, row.Trade.TradingSetupId, row.Browse.GrossPnL,
                    row.Browse.TotalCosts, row.Browse.NetPnL)
                    { InstrumentSymbol = row.Instrument == null ? null : row.Instrument.Symbol },
                row.Trade.TradingAccountId, row.Trade.InstrumentId, row.Browse.Direction)
            {
                AccountName = row.Account == null ? null : row.Account.Name,
                AccountIsActive = row.Account == null ? null : (bool?)row.Account.IsActive,
                InstrumentName = row.Instrument == null ? null : row.Instrument.Symbol,
                InstrumentIsActive = row.Instrument == null ? null : (bool?)row.Instrument.IsActive,
                SetupName = row.Setup == null ? null : row.Setup.Name,
                SetupIsActive = row.Setup == null ? null : (bool?)row.Setup.IsActive,
            }).ToListAsync(cancellationToken);
        }
        // No database scope survives materialization or is held during CPU aggregation.
        return BreakdownMetricCalculator.Calculate(query, facts, cancellationToken);
    }
}
