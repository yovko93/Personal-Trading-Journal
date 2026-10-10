namespace PersonalTradingJournal.Application.Analytics;

// Shared outcome formulas for Dashboard/Calendar and Daily Review; no economics reconstruction.
internal sealed class PnlAccumulator(PnlBasis basis)
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
