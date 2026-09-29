namespace PersonalTradingJournal.Application.Analytics;

public enum PnlBasis { Gross, Net, EffectiveNet }
public enum MetricCoverageStatus { Empty, Complete, Partial, Unavailable }
public enum ProfitFactorStatus { Defined, NoTrades, IncompleteCoverage, NoLosses, AllBreakEven }
public enum AveragePnlStatus { Defined, NoTrades, IncompleteCoverage, NoWins, NoLosses }

public sealed record MetricCoverage(int ClosedTradeCount, int KnownTradeCount)
{
    public int UnknownTradeCount => ClosedTradeCount - KnownTradeCount;
    public MetricCoverageStatus Status => ClosedTradeCount == 0 ? MetricCoverageStatus.Empty
        : KnownTradeCount == ClosedTradeCount ? MetricCoverageStatus.Complete
        : KnownTradeCount == 0 ? MetricCoverageStatus.Unavailable : MetricCoverageStatus.Partial;
}

/// <summary>NoLosses means positive profit / zero loss (unbounded), never a decimal sentinel.</summary>
public sealed record ProfitFactorMetric(ProfitFactorStatus Status, decimal? Value);

/// <summary>An average in the enclosing currency/basis; losses are positive magnitudes.</summary>
public sealed record AveragePnlMetric(AveragePnlStatus Status, decimal? Value);

/// <summary>
/// Total, WinRatePercent, ProfitFactor and averages require complete-population coverage.
/// KnownSubtotal and known counts are explicitly partial evidence when Coverage is incomplete.
/// Empty/all-unknown selections have a null subtotal, distinct from an actual zero.
/// For EffectiveNet, numeric coverage includes estimates. IsEstimated applies to every
/// amount, ratio and outcome count; complete numeric coverage does not mean verified costs.
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
    ProfitFactorMetric ProfitFactor,
    int EstimatedTradeCount = 0)
{
    /// <summary>Applies to this entire metric bundle: total/subtotal, ratios and outcome counts.</summary>
    public bool IsEstimated => EstimatedTradeCount > 0;
    public int VerifiedTradeCount => Coverage.KnownTradeCount - EstimatedTradeCount;

    /// <summary>Excludes break-evens; shares this bundle's basis, coverage and estimate provenance.</summary>
    public AveragePnlMetric AverageWin => Average(KnownProfitSum, KnownWins, AveragePnlStatus.NoWins);

    /// <summary>Positive loss magnitude; excludes break-evens and retains this bundle's provenance.</summary>
    public AveragePnlMetric AverageLoss => Average(KnownLossMagnitude, KnownLosses, AveragePnlStatus.NoLosses);

    // Reuse the calculator's authoritative sums/counts, including for periods and cumulative snapshots.
    // Never expose a known-subset average as a complete metric when any outcome is unavailable.
    private AveragePnlMetric Average(decimal? sum, int count, AveragePnlStatus absent) =>
        Coverage.Status == MetricCoverageStatus.Empty ? new(AveragePnlStatus.NoTrades, null)
        : Coverage.Status != MetricCoverageStatus.Complete ? new(AveragePnlStatus.IncompleteCoverage, null)
        : count == 0 ? new(absent, null)
        : new(AveragePnlStatus.Defined, sum / count);
}

public sealed record ClosedTradeMetrics(
    int ClosedTradeCount,
    int UnknownCostTradeCount,
    PnlMetrics Gross,
    PnlMetrics Net,
    PnlMetrics EffectiveNet);
