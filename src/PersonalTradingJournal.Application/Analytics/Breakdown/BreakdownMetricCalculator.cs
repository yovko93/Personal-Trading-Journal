using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Analytics.Breakdown;

/// <summary>
/// Pure, deterministic calculations over a complete candidate population. Validates unique projections
/// before filtering; invalid inputs, cancellation and decimal overflow return no partial result.
/// Does not reconstruct economics, write data, estimate costs or implement database paging.
/// </summary>
public static class BreakdownMetricCalculator
{
    public static BreakdownAnalyticsSnapshot Calculate(BreakdownQuery query, IEnumerable<BreakdownTradeFact> trades,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(trades);
        cancellationToken.ThrowIfCancellationRequested();
        var ids = new HashSet<Guid>();
        var selected = new List<ResolvedTrade>();
        foreach (var trade in trades)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Validate(trade);
            if (!ids.Add(trade.Facts.TradeId))
                throw new ArgumentException("Supply exactly one projection per Trade ID, not duplicate join or page rows.", nameof(trades));
            if (!Matches(query, trade)) continue;

            // Resolve once. The same unrounded value feeds all selected metrics and outcomes.
            decimal? value = query.Mode == BreakdownCalculationMode.Gross ? trade.Facts.GrossPnL
                : trade.Facts.NetPnL ?? trade.Facts.GrossPnL;
            ValueSource source = value is null ? ValueSource.Unavailable
                : query.Mode == BreakdownCalculationMode.Gross ? ValueSource.Gross
                : trade.Facts.NetPnL.HasValue ? ValueSource.Net : ValueSource.GrossFallback;
            selected.Add(new(trade, Group(query.Grouping, trade), value, source));
        }
        // Decimal arithmetic and metadata choice cannot depend on SQL/input enumeration order.
        ResolvedTrade[] ordered = selected.OrderBy(t => t.Trade.Facts.TradeId).ToArray();
        var summary = Summarize(query.Mode, ordered, cancellationToken);
        var groups = new List<BreakdownGroupMetrics>();
        foreach (var group in ordered.GroupBy(t => t.Key).OrderBy(g => g.Key))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var members = group.ToArray();
            groups.Add(new(group.Key, Label(group.Key, members[0].Trade), Summarize(query.Mode, members, cancellationToken)));
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new(query, summary, groups.AsReadOnly());
    }

    private static bool Matches(BreakdownQuery query, BreakdownTradeFact trade)
    {
        var filter = query.TradeFilter;
        return trade.Facts.Status == query.IncludedStatus && trade.Facts.ClosedAtUtc is { } close
            && (filter.TradingAccountId is null || trade.TradingAccountId == filter.TradingAccountId)
            && (filter.InstrumentId is null || trade.InstrumentId == filter.InstrumentId)
            && string.Equals(trade.Facts.Currency, query.Currency, StringComparison.Ordinal)
            && (filter.ClosedFromUtc is null || close >= filter.ClosedFromUtc)
            && (filter.ClosedBeforeUtc is null || close < filter.ClosedBeforeUtc);
    }

    private static BreakdownGroupKey Group(BreakdownGrouping dimension, BreakdownTradeFact trade) => dimension switch
    {
        BreakdownGrouping.Instrument => BreakdownGroupKey.ForInstrument(trade.InstrumentId),
        BreakdownGrouping.Account => BreakdownGroupKey.ForAccount(trade.TradingAccountId),
        BreakdownGrouping.TradingSetup => BreakdownGroupKey.ForSetup(trade.Facts.TradingSetupId),
        BreakdownGrouping.Direction => BreakdownGroupKey.ForDirection(trade.Direction),
        BreakdownGrouping.EntryWeekday => BreakdownGroupKey.FromNewYorkEntryWeekday(trade.Facts.OpenedAtUtc),
        BreakdownGrouping.EntryHour => BreakdownGroupKey.FromNewYorkEntryHour(trade.Facts.OpenedAtUtc),
        BreakdownGrouping.HoldingDuration => BreakdownGroupKey.ForHoldingDuration(
            HoldingDurationRange.FromTimestamps(trade.Facts.OpenedAtUtc, trade.Facts.ClosedAtUtc)
            ?? throw new ArgumentException("An eligible closed Trade needs a valid holding duration.", nameof(trade))),
        _ => throw new ArgumentOutOfRangeException(nameof(dimension)),
    };

    private static string Label(BreakdownGroupKey key, BreakdownTradeFact trade) => key.Grouping switch
    {
        BreakdownGrouping.Instrument => key.GetLabel(trade.InstrumentName, trade.InstrumentIsActive),
        BreakdownGrouping.Account => key.GetLabel(trade.AccountName, trade.AccountIsActive),
        BreakdownGrouping.TradingSetup => key.GetLabel(trade.SetupName, trade.SetupIsActive),
        _ => key.GetLabel(),
    };

    private static BreakdownMetrics Summarize(BreakdownCalculationMode mode, ResolvedTrade[] rows, CancellationToken token)
    {
        // PnlAccumulator's basis is only a shared arithmetic tag here, not exposed as the
        // Breakdown policy. Its numeric rules are reused; no estimate flag is ever supplied.
        var selected = new PnlAccumulator(PnlBasis.Gross);
        var gross = new PnlAccumulator(PnlBasis.Gross);
        int netUsed = 0, fallback = 0, grossUsed = 0, unavailable = 0;
        int netKnown = 0, grossKnown = 0, costsKnown = 0, classified = 0, missingSetup = 0;
        foreach (var row in rows)
        {
            token.ThrowIfCancellationRequested();
            selected.Add(row.Value);
            gross.Add(row.Trade.Facts.GrossPnL);
            switch (row.Source)
            {
                case ValueSource.Net: netUsed++; break;
                case ValueSource.GrossFallback: fallback++; break;
                case ValueSource.Gross: grossUsed++; break;
                case ValueSource.Unavailable: unavailable++; break;
            }
            if (row.Trade.Facts.NetPnL.HasValue) netKnown++;
            if (row.Trade.Facts.GrossPnL.HasValue) grossKnown++;
            if (row.Trade.Facts.TotalCosts.HasValue) costsKnown++;
            if (row.Trade.Facts.TradingSetupId.HasValue)
            {
                classified++;
                if (string.IsNullOrWhiteSpace(row.Trade.SetupName)) missingSetup++;
            }
        }
        int count = rows.Length;
        return new(BreakdownPnlMetrics.FromShared(selected.Snapshot(count)), BreakdownPnlMetrics.FromShared(gross.Snapshot(count)),
            new(mode, count, netUsed, fallback, grossUsed, unavailable),
            new(new(count, netKnown), new(count, grossKnown), new(count, costsKnown), new(count, classified), missingSetup),
            Array.AsReadOnly(rows.Select(t => t.Trade.Facts.TradeId).ToArray()));
    }

    private static void Validate(BreakdownTradeFact trade)
    {
        ArgumentNullException.ThrowIfNull(trade);
        ArgumentNullException.ThrowIfNull(trade.Facts);
        TradeAnalyticsFact fact = trade.Facts;
        if (fact.TradeId == Guid.Empty || trade.TradingAccountId == Guid.Empty || trade.InstrumentId == Guid.Empty
            || fact.TradingSetupId == Guid.Empty || !Enum.IsDefined(fact.Status) || !Enum.IsDefined(trade.Direction))
            throw new ArgumentException("Supply valid Trade/reference identities, direction and status.", nameof(trade));
        if (string.IsNullOrWhiteSpace(fact.Currency) || fact.Currency.Length > 8 || fact.Currency != fact.Currency.Trim().ToUpperInvariant())
            throw new ArgumentException("Supply the canonical historical pricing currency.", nameof(trade));
        if (fact.OpenedAtUtc.Offset != TimeSpan.Zero
            || fact.ClosedAtUtc is { } close && (close.Offset != TimeSpan.Zero || close < fact.OpenedAtUtc)
            || (fact.Status == TradeStatus.Closed) != fact.ClosedAtUtc.HasValue)
            throw new ArgumentException("Supply consistent lifecycle timestamps in UTC; invalid/unavailable closed durations cannot be grouped.", nameof(trade));
        if (fact.TotalCosts < 0m || fact.NetPnL.HasValue && (!fact.GrossPnL.HasValue || !fact.TotalCosts.HasValue)
            || fact.Status == TradeStatus.Open && (fact.GrossPnL.HasValue || fact.NetPnL.HasValue))
            throw new ArgumentException("Supply authoritative final economics; known actual Net requires known Gross and costs, and open Trades have no final P&L.", nameof(trade));
    }

    private enum ValueSource { Net, GrossFallback, Gross, Unavailable }
    private sealed record ResolvedTrade(BreakdownTradeFact Trade, BreakdownGroupKey Key, decimal? Value, ValueSource Source);
}
