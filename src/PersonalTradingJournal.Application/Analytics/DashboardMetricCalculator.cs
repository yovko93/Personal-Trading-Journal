using PersonalTradingJournal.Application.Common.Time;
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
            TradeAnalyticsFact[] closed = currency.Where(t => t.Status == TradeStatus.Closed).ToArray();
            DailyTradeMetrics[] days = closed.GroupBy(t => GetNewYorkCloseDate(t.ClosedAtUtc!.Value))
                .OrderBy(g => g.Key)
                .Select(g => new DailyTradeMetrics(g.Key, Summarize(g.ToArray(), cancellationToken))).ToArray();
            SetupTradeMetrics[] setups = closed.GroupBy(t => t.TradingSetupId)
                .OrderBy(g => g.Key)
                .Select(g => new SetupTradeMetrics(g.Key, Summarize(g.ToArray(), cancellationToken))).ToArray();
            currencies.Add(new(currency.Key, currency.Count() - closed.Length,
                Summarize(closed, cancellationToken), Array.AsReadOnly(days), Array.AsReadOnly(setups)));
        }

        return new(selected.Count, selected.Count(t => t.Status == TradeStatus.Open), currencies.AsReadOnly());
    }

    /// <summary>New York civil date of closure, not broker TradeDay or a futures-session rollover.</summary>
    public static DateOnly GetNewYorkCloseDate(DateTimeOffset closedAtUtc) =>
        DateOnly.FromDateTime(TradingTimePolicy.ConvertUtcToTradingTime(closedAtUtc).DateTime);

    private static ClosedTradeMetrics Summarize(TradeAnalyticsFact[] closed, CancellationToken token) =>
        new(closed.Length, closed.Count(t => !t.TotalCosts.HasValue),
            CalculatePnl(closed, PnlBasis.Gross, token), CalculatePnl(closed, PnlBasis.Net, token));

    private static PnlMetrics CalculatePnl(TradeAnalyticsFact[] closed, PnlBasis basis, CancellationToken token)
    {
        decimal subtotal = 0m, profits = 0m, losses = 0m;
        int wins = 0, lossCount = 0, breakEvens = 0;
        foreach (TradeAnalyticsFact trade in closed)
        {
            token.ThrowIfCancellationRequested();
            decimal? value = basis == PnlBasis.Gross ? trade.GrossPnL : trade.NetPnL;
            if (value is not { } amount) continue;
            subtotal = checked(subtotal + amount);
            if (amount > 0m) { wins++; profits = checked(profits + amount); }
            else if (amount < 0m) { lossCount++; losses = checked(losses - amount); }
            else breakEvens++;
        }

        int knownCount = wins + lossCount + breakEvens;
        var coverage = new MetricCoverage(closed.Length, knownCount);
        bool complete = coverage.Status == MetricCoverageStatus.Complete;
        ProfitFactorMetric factor = coverage.Status == MetricCoverageStatus.Empty
            ? new(ProfitFactorStatus.NoTrades, null)
            : !complete ? new(ProfitFactorStatus.IncompleteCoverage, null)
            : losses > 0m ? new(ProfitFactorStatus.Defined, profits / losses)
            : profits > 0m ? new(ProfitFactorStatus.NoLosses, null)
            : new(ProfitFactorStatus.AllBreakEven, null);

        return new(basis, coverage, complete ? subtotal : null, knownCount > 0 ? subtotal : null,
            wins, lossCount, breakEvens, knownCount > 0 ? profits : null, knownCount > 0 ? losses : null,
            complete ? 100m * wins / closed.Length : null, factor);
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
