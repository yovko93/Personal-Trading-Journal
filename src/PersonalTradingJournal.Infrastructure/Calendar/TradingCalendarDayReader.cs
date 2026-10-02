using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Trades;

namespace PersonalTradingJournal.Infrastructure.Calendar;

public sealed class TradingCalendarDayReader(IDbContextFactory<JournalDbContext> contextFactory)
    : ITradingCalendarDayReader
{
    public async Task<TradingCalendarDayDetails> GetAsync(TradingCalendarDayQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        await using JournalDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = from trade in context.Trades.AsNoTracking()
                   join browse in context.TradeBrowse.AsNoTracking() on trade.Id equals browse.TradeId
                   join account in context.TradingAccounts.AsNoTracking() on trade.TradingAccountId equals account.Id into accounts
                   from account in accounts.DefaultIfEmpty()
                   join instrument in context.Instruments.AsNoTracking() on trade.InstrumentId equals instrument.Id into instruments
                   from instrument in instruments.DefaultIfEmpty()
                   where browse.Status == TradeStatus.Closed && browse.ClosedAtUtc >= query.ClosedFromUtc &&
                       browse.ClosedAtUtc < query.ClosedBeforeUtc
                   select new { Trade = trade, Browse = browse, Account = account, Instrument = instrument };
        if (query.TradingAccountId is { } accountId)
            rows = rows.Where(row => row.Trade.TradingAccountId == accountId);
        var selected = await rows.OrderByDescending(row => row.Browse.ClosedAtUtc).ThenBy(row => row.Trade.Id)
            .Select(row => new TradeListItem(row.Trade.Id, row.Trade.TradingAccountId,
                row.Account == null ? "Unavailable Account" : row.Account.Name, row.Trade.InstrumentId,
                row.Instrument == null ? "Unavailable Instrument" : row.Instrument.Symbol,
                row.Browse.Direction, row.Browse.Status, row.Browse.OpenedAtUtc, row.Browse.ClosedAtUtc,
                row.Browse.OpenQuantity, row.Browse.AverageEntryPrice, row.Browse.AverageExitPrice,
                row.Browse.TotalCosts, row.Browse.GrossPnL, row.Browse.NetPnL, row.Trade.PricingCurrency, 0m))
            .ToListAsync(cancellationToken);
        Dictionary<Guid, decimal> sizes = await TradePositionSizeReader.GetAsync(context,
            selected.Select(row => row.Id).ToArray(), cancellationToken);
        Guid[] ids = selected.Select(row => row.Id).ToArray();
        // Fixed-count batch reads, including historical inactive or missing references.
        var setups = await (from trade in context.Trades.AsNoTracking()
                            join setup in context.TradingSetups.AsNoTracking() on trade.TradingSetupId equals (Guid?)setup.Id into matches
                            from setup in matches.DefaultIfEmpty()
                            where ids.Contains(trade.Id)
                            select new { trade.Id, trade.TradingSetupId, Name = setup == null ? null : setup.Name,
                                Active = setup == null ? (bool?)null : setup.IsActive }).ToListAsync(cancellationToken);
        var mistakes = await (from assignment in context.TradeMistakes.AsNoTracking()
                              join mistake in context.TradingMistakes.AsNoTracking() on assignment.TradingMistakeId equals mistake.Id into matches
                              from mistake in matches.DefaultIfEmpty()
                              where ids.Contains(assignment.TradeId)
                              orderby assignment.TradeId, assignment.TradingMistakeId
                              select new { assignment.TradeId, assignment.TradingMistakeId,
                                  Name = mistake == null ? null : mistake.Name,
                                  Active = mistake == null ? (bool?)null : mistake.IsActive }).ToListAsync(cancellationToken);
        var byTrade = mistakes.ToLookup(m => m.TradeId);
        return TradingCalendarDayDetails.Create(query.Date,
            selected.Select(row => row with { Size = sizes[row.Id] }), cancellationToken) with
        {
            Classifications = setups.ToDictionary(s => s.Id, s => new CalendarTradeClassification(
                s.TradingSetupId, s.Name, s.Active, byTrade[s.Id].Select(m =>
                    new CalendarAssignedMistake(m.TradingMistakeId, m.Name, m.Active)).ToArray())),
        };
    }
}
