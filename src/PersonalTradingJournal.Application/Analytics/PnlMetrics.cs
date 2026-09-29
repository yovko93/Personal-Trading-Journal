namespace PersonalTradingJournal.Application.Analytics;

public enum PnlBasis { Gross, Net }
public enum MetricCoverageStatus { Empty, Complete, Partial, Unavailable }
public enum ProfitFactorStatus { Defined, NoTrades, IncompleteCoverage, NoLosses, AllBreakEven }

public sealed record MetricCoverage(int ClosedTradeCount, int KnownTradeCount)
{
    public int UnknownTradeCount => ClosedTradeCount - KnownTradeCount;
    public MetricCoverageStatus Status => ClosedTradeCount == 0 ? MetricCoverageStatus.Empty
        : KnownTradeCount == ClosedTradeCount ? MetricCoverageStatus.Complete
        : KnownTradeCount == 0 ? MetricCoverageStatus.Unavailable : MetricCoverageStatus.Partial;
}

/// <summary>NoLosses means positive profit / zero loss (unbounded), never a decimal sentinel.</summary>
public sealed record ProfitFactorMetric(ProfitFactorStatus Status, decimal? Value);

/// <summary>
/// Total, WinRatePercent and ProfitFactor are complete-population metrics, never subset estimates.
/// KnownSubtotal and known counts are explicitly partial evidence when Coverage is incomplete.
/// Empty/all-unknown selections have a null subtotal, distinct from an actual zero.
/// </summary>
public sealed record PnlMetrics(
    PnlBasis Basis,
    MetricCoverage Coverage,
    decimal? Total,
    decimal? KnownSubtotal,
    int KnownWins,
    int KnownLosses,
    int KnownBreakEvens,
    decimal? KnownProfitSum,
    decimal? KnownLossMagnitude,
    decimal? WinRatePercent,
    ProfitFactorMetric ProfitFactor);

public sealed record ClosedTradeMetrics(
    int ClosedTradeCount,
    int UnknownCostTradeCount,
    PnlMetrics Gross,
    PnlMetrics Net);
