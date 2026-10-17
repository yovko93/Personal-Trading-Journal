namespace PersonalTradingJournal.Application.Analytics.Breakdown;

public enum BreakdownFinancialSource { NoEvaluableValues, Net, NetGross, GrossFallback, Gross }

/// <summary>Provenance of the selected values, independent of numeric completeness and actual Net coverage.</summary>
public sealed record BreakdownFinancialCoverage(BreakdownCalculationMode Mode, int EligibleTradeCount,
    int NetUsedCount, int GrossFallbackCount, int GrossUsedCount, int UnavailableCount)
{
    public int EvaluableTradeCount => EligibleTradeCount - UnavailableCount;
    public BreakdownFinancialSource Source => Mode == BreakdownCalculationMode.Gross ? BreakdownFinancialSource.Gross
        : EvaluableTradeCount == 0 ? BreakdownFinancialSource.NoEvaluableValues
        : GrossFallbackCount == 0 ? BreakdownFinancialSource.Net
        : NetUsedCount == 0 ? BreakdownFinancialSource.GrossFallback : BreakdownFinancialSource.NetGross;
}

/// <summary>
/// Costs use authoritative TotalCosts availability: no inferred component values. A retained Setup ID
/// counts as classified even if its metadata is missing. Unclassified is strictly a null Setup ID.
/// </summary>
public sealed record BreakdownDataCoverage(MetricCoverage ActualNet, MetricCoverage Gross,
    MetricCoverage KnownCosts, MetricCoverage SetupClassification, int UnavailableSetupMetadataCount)
{
    public int MissingCostTradeCount => KnownCosts.UnknownTradeCount;
    public int UnclassifiedTradeCount => SetupClassification.UnknownTradeCount;
}

/// <summary>
/// Numerical results only; enclosing Mode/FinancialSources describe the selected-value policy.
/// Known outcome counts/subtotal may describe a subset; Total, ratios and averages require complete
/// nonempty coverage. Average loss is the established positive magnitude, not a signed losing P&amp;L.
/// </summary>
public sealed record BreakdownPnlMetrics(MetricCoverage Coverage, decimal? Total, decimal? KnownSubtotal,
    int Wins, int Losses, int BreakEvens, decimal? WinningSum, decimal? LosingMagnitude,
    decimal? WinRatePercent, ProfitFactorMetric ProfitFactor,
    AveragePnlMetric AverageTrade, AveragePnlMetric AverageWin, AveragePnlMetric AverageLoss)
{
    public int EvaluableTradeCount => Coverage.KnownTradeCount;

    // Reuse shared sums, outcome/ratio/average rules without exporting its basis/estimate tags.
    internal static BreakdownPnlMetrics FromShared(PnlMetrics metrics) => new(metrics.Coverage,
        metrics.Total, metrics.KnownSubtotal, metrics.KnownWins, metrics.KnownLosses, metrics.KnownBreakEvens,
        metrics.KnownProfitSum, metrics.KnownLossMagnitude, metrics.WinRatePercent, metrics.ProfitFactor,
        metrics.Coverage.Status == MetricCoverageStatus.Complete
            ? new(AveragePnlStatus.Defined, metrics.Total / metrics.Coverage.ClosedTradeCount)
            : new(metrics.Coverage.Status == MetricCoverageStatus.Empty ? AveragePnlStatus.NoTrades : AveragePnlStatus.IncompleteCoverage, null),
        metrics.AverageWin, metrics.AverageLoss);
}

public sealed record BreakdownMetrics(BreakdownPnlMetrics Selected, BreakdownPnlMetrics Gross,
    BreakdownFinancialCoverage FinancialSources, BreakdownDataCoverage Data,
    IReadOnlyList<Guid> TradeIds)
{
    public int EligibleTradeCount => Selected.Coverage.ClosedTradeCount;
}

public sealed record BreakdownGroupMetrics(BreakdownGroupKey Key, string Label, BreakdownMetrics Metrics);

/// <summary>One currency, complete filtered population, stable group and source-ID ordering. No persisted analytics.</summary>
public sealed record BreakdownAnalyticsSnapshot(BreakdownQuery Query, BreakdownMetrics Summary,
    IReadOnlyList<BreakdownGroupMetrics> Groups);
