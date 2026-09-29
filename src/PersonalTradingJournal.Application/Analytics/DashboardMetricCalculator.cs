using PersonalTradingJournal.Application.Common.Time;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Analytics;

/// <summary>Pure rules over authoritative facts. Never reconstructs economics from prices.</summary>
public static class DashboardMetricCalculator
{
    public static DashboardAnalyticsSnapshot Calculate(
        IEnumerable<TradeAnalyticsFact> trades,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trades);
        cancellationToken.ThrowIfCancellationRequested();
        var selected = new List<TradeAnalyticsFact>();
        var identities = new HashSet<Guid>();
        foreach (TradeAnalyticsFact trade in trades)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Validate(trade);
            if (!identities.Add(trade.TradeId))
                throw new ArgumentException("Analytics requires one fact per Trade, not duplicate/join rows.", nameof(trades));
            selected.Add(trade);
        }

        var currencies = new List<CurrencyTradeMetrics>();
        foreach (var currency in selected.GroupBy(t => t.Currency, StringComparer.Ordinal)
                     .OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            TradeAnalyticsFact[] closed = currency.Where(t => t.Status == TradeStatus.Closed)
                .OrderBy(t => t.ClosedAtUtc).ThenBy(t => t.TradeId).ToArray();
            var days = new List<DailyTradeMetrics>();
            var weeks = new List<WeeklyTradeMetrics>();
            var cumulativeDays = new ClosedMetricsAccumulator();
            foreach (var day in closed.GroupBy(t => GetNewYorkCloseDate(t.ClosedAtUtc!.Value)).OrderBy(g => g.Key))
            {
                var period = new ClosedMetricsAccumulator();
                foreach (TradeAnalyticsFact trade in day)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    period.Add(trade);
                    cumulativeDays.Add(trade);
                }
                days.Add(new(day.Key, period.Snapshot(), cumulativeDays.Snapshot()));
            }
            var cumulativeWeeks = new ClosedMetricsAccumulator();
            foreach (var week in closed.GroupBy(t => GetWeekStartingMonday(GetNewYorkCloseDate(t.ClosedAtUtc!.Value)))
                         .OrderBy(g => g.Key))
            {
                var period = new ClosedMetricsAccumulator();
                foreach (TradeAnalyticsFact trade in week)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    period.Add(trade);
                    cumulativeWeeks.Add(trade);
                }
                weeks.Add(new(week.Key, period.Snapshot(), cumulativeWeeks.Snapshot()));
            }
            SetupTradeMetrics[] setups = closed.GroupBy(t => t.TradingSetupId)
                .OrderBy(g => g.Key)
                .Select(g => new SetupTradeMetrics(g.Key, Summarize(g.ToArray(), cancellationToken))).ToArray();
            currencies.Add(new(currency.Key, currency.Count() - closed.Length,
                cumulativeDays.Snapshot(), days.AsReadOnly(), weeks.AsReadOnly(), Array.AsReadOnly(setups)));
        }

        return new(selected.Count, selected.Count(t => t.Status == TradeStatus.Open), currencies.AsReadOnly());
    }

    /// <summary>New York civil date of closure, not broker TradeDay or a futures-session rollover.</summary>
    public static DateOnly GetNewYorkCloseDate(DateTimeOffset closedAtUtc) =>
        DateOnly.FromDateTime(TradingTimePolicy.ConvertUtcToTradingTime(closedAtUtc).DateTime);

    public static DateOnly GetWeekStartingMonday(DateOnly newYorkDate) =>
        newYorkDate.AddDays(-(((int)newYorkDate.DayOfWeek + 6) % 7));

    private static ClosedTradeMetrics Summarize(TradeAnalyticsFact[] closed, CancellationToken token)
    {
        var accumulator = new ClosedMetricsAccumulator();
        foreach (TradeAnalyticsFact trade in closed)
        {
            token.ThrowIfCancellationRequested();
            accumulator.Add(trade);
        }
        return accumulator.Snapshot();
    }

    // Running accumulators avoid rescanning every prefix. Snapshots contain values, not mutable state.
    // The same M12.1 rules serve overall, Setup, daily, weekly and cumulative results.
    private sealed class ClosedMetricsAccumulator
    {
        private int _count, _unknownCosts;
        private readonly PnlAccumulator _gross = new(PnlBasis.Gross);
        private readonly PnlAccumulator _net = new(PnlBasis.Net);
        private readonly PnlAccumulator _effectiveNet = new(PnlBasis.EffectiveNet);
        public void Add(TradeAnalyticsFact trade)
        {
            _count = checked(_count + 1);
            if (!trade.TotalCosts.HasValue) _unknownCosts = checked(_unknownCosts + 1);
            _gross.Add(trade.GrossPnL);
            _net.Add(trade.NetPnL);
            EffectiveNetPnL effective = EffectiveNetPnL.Resolve(trade.Status, trade.GrossPnL, trade.NetPnL);
            _effectiveNet.Add(effective.Value, effective.IsEstimated);
        }
        public ClosedTradeMetrics Snapshot() => new(_count, _unknownCosts, _gross.Snapshot(_count),
            _net.Snapshot(_count), _effectiveNet.Snapshot(_count));
    }

    private sealed class PnlAccumulator(PnlBasis basis)
    {
        private decimal _subtotal, _profits, _losses;
        private int _wins, _lossCount, _breakEvens, _estimated;
        public void Add(decimal? value, bool estimated = false)
        {
            if (value is not { } amount) return;
            if (estimated) _estimated++;
            _subtotal = checked(_subtotal + amount);
            if (amount > 0m) { _wins++; _profits = checked(_profits + amount); }
            else if (amount < 0m) { _lossCount++; _losses = checked(_losses - amount); }
            else _breakEvens++;
        }
        public PnlMetrics Snapshot(int closedCount)
        {
            int knownCount = _wins + _lossCount + _breakEvens;
            var coverage = new MetricCoverage(closedCount, knownCount);
            bool complete = coverage.Status == MetricCoverageStatus.Complete;
            ProfitFactorMetric factor = coverage.Status == MetricCoverageStatus.Empty
                ? new(ProfitFactorStatus.NoTrades, null)
                : !complete ? new(ProfitFactorStatus.IncompleteCoverage, null)
                : _losses > 0m ? new(ProfitFactorStatus.Defined, _profits / _losses)
                : _profits > 0m ? new(ProfitFactorStatus.NoLosses, null)
                : new(ProfitFactorStatus.AllBreakEven, null);

            return new(basis, coverage, complete ? _subtotal : null, knownCount > 0 ? _subtotal : null,
                _wins, _lossCount, _breakEvens, knownCount > 0 ? _profits : null, knownCount > 0 ? _losses : null,
                complete ? 100m * _wins / closedCount : null, factor, _estimated);
        }
    }

    private static void Validate(TradeAnalyticsFact trade)
    {
        ArgumentNullException.ThrowIfNull(trade);
        if (trade.TradeId == Guid.Empty || trade.TradingSetupId == Guid.Empty || !Enum.IsDefined(trade.Status))
            throw new ArgumentException("Analytics requires valid Trade/Setup identities and lifecycle status.", nameof(trade));
        if (string.IsNullOrWhiteSpace(trade.Currency) || trade.Currency != trade.Currency.Trim().ToUpperInvariant())
            throw new ArgumentException("Use the canonical historical pricing currency, not an inferred account currency.", nameof(trade));
        if (trade.OpenedAtUtc.Offset != TimeSpan.Zero ||
            trade.ClosedAtUtc is { } close && (close.Offset != TimeSpan.Zero || close < trade.OpenedAtUtc) ||
            (trade.Status == TradeStatus.Closed) != trade.ClosedAtUtc.HasValue)
            throw new ArgumentException("Analytics requires consistent lifecycle timestamps with zero UTC offsets.", nameof(trade));
        if (trade.TotalCosts < 0m ||
            trade.NetPnL.HasValue && (!trade.GrossPnL.HasValue || !trade.TotalCosts.HasValue) ||
            trade.Status == TradeStatus.Open && (trade.GrossPnL.HasValue || trade.NetPnL.HasValue))
            throw new ArgumentException("Use authoritative final P&L; unknown costs cannot supply Net and open Trades have no final P&L.", nameof(trade));
    }
}
