using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.PeriodReviews;

/// <summary>Pure calculation over a complete M19.1 snapshot. Reuses Daily Review's strict-cost
/// eligibility and Dashboard/Calendar formulas. No rounding, IO or evidence mutation. Invalid
/// evidence throws ArgumentException; overflow/cancellation return no partial statistics.</summary>
public static class PeriodReviewStatisticsCalculator
{
    public static PeriodReviewStatistics Calculate(PeriodReviewEvidence evidence, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(evidence.Query);
        cancellationToken.ThrowIfCancellationRequested();
        if ((long)evidence.ClosedTrades.Count + evidence.ActivityContext.Count > new PeriodEvidenceLimits().MaxRowsPerSet)
            throw new ArgumentException("Supply a complete, bounded M19.1 evidence snapshot.", nameof(evidence));
        foreach (var trade in evidence.ClosedTrades)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (trade.Inclusion != DailyReviewTradeInclusion.ClosedOnDate)
                throw new ArgumentException("ClosedTrades must contain only closure-attributed evidence.", nameof(evidence));
        }
        foreach (var trade in evidence.ActivityContext)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (trade.Inclusion == DailyReviewTradeInclusion.ClosedOnDate ||
                trade.Facts is { Status: TradeStatus.Closed, ClosedAtUtc: not null } ||
                !trade.Executions.Any(e => e.ExecutedAtUtc >= evidence.Query.FromUtc && e.ExecutedAtUtc < evidence.Query.BeforeUtc))
                throw new ArgumentException("Context must have in-period activity, not an attributable closed Trade.", nameof(evidence));
        }

        var rows = evidence.ClosedTrades.Concat(evidence.ActivityContext).ToArray();
        var shared = DailyReviewStatisticsCalculator.CalculateTrades(rows, evidence.Query.Scope.AccountId,
            evidence.Query.FromUtc, evidence.Query.BeforeUtc, cancellationToken);
        var byId = rows.ToDictionary(t => t.TradeId); // Shared validation rejected duplicate identities.
        var currencies = new List<PeriodCurrencyStatistics>();
        foreach (var currency in shared.Currencies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var accounts = currency.Accounts.Select(a => new PeriodAccountStatistics(a.Account,
                Group(a.Population, a.Gross, a.Net))).ToArray();
            currencies.Add(new(currency.Currency, Group(currency.Population, currency.Gross, currency.Net), Array.AsReadOnly(accounts)));
        }
        var result = new PeriodReviewStatistics(evidence, shared.Population, Days(shared.Population.Closed), currencies.AsReadOnly());
        cancellationToken.ThrowIfCancellationRequested();
        return result;

        IReadOnlyList<PeriodTradingDay> Days(DailyReviewTradeSet closed)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Array.AsReadOnly(closed.TradeIds.GroupBy(id => DashboardMetricCalculator.GetNewYorkCloseDate(byId[id].Facts!.ClosedAtUtc!.Value))
                .OrderBy(g => g.Key).Select(g => new PeriodTradingDay(g.Key, new(Array.AsReadOnly(g.Order().ToArray())))).ToArray());
        }

        PeriodGroupStatistics Group(DailyReviewPopulation population, DailyReviewPnlStatistics gross, DailyReviewPnlStatistics net)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var days = Days(population.Closed);
            var closed = population.Closed.TradeIds.ToHashSet();
            var costs = new PeriodCostCoverage(new(Array.AsReadOnly(population.UnknownCommissions.TradeIds.Where(closed.Contains).ToArray())),
                new(Array.AsReadOnly(population.UnknownFees.TradeIds.Where(closed.Contains).ToArray())), population.ClosedUnknownCosts);
            return new(population, days, costs, Extend(gross, days.Count), Extend(net, days.Count));
        }

        PeriodPnlStatistics Extend(DailyReviewPnlStatistics pnl, int activeDays)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var coverage = pnl.Metrics.Coverage;
            if (coverage.Status != MetricCoverageStatus.Complete)
            {
                var unavailable = new AveragePnlMetric(coverage.Status == MetricCoverageStatus.Empty
                    ? AveragePnlStatus.NoTrades : AveragePnlStatus.IncompleteCoverage, null);
                return new(pnl, unavailable, unavailable, null, null);
            }
            // Only use authoritative projected values after the shared calculator established
            // complete numeric/cost coverage. Extrema include all ties; no second economics formula.
            decimal Value(Guid id) => pnl.Metrics.Basis == PnlBasis.Gross ? byId[id].Facts!.GrossPnL!.Value : byId[id].Facts!.NetPnL!.Value;
            decimal best = decimal.MinValue, worst = decimal.MaxValue;
            foreach (var id in pnl.Known.TradeIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                decimal value = Value(id); best = Math.Max(best, value); worst = Math.Min(worst, value);
            }
            PeriodTradeExtreme Extreme(decimal value) => new(value,
                new(Array.AsReadOnly(pnl.Known.TradeIds.Where(id => Value(id) == value).ToArray())));
            return new(pnl, new(AveragePnlStatus.Defined, pnl.Metrics.Total / coverage.ClosedTradeCount),
                new(AveragePnlStatus.Defined, pnl.Metrics.Total / activeDays), Extreme(best), Extreme(worst));
        }
    }
}
