using PersonalTradingJournal.Application.Analytics;

namespace PersonalTradingJournal.Application.DailyReview;

/// <summary>Ordered source IDs, not rounded display values or inferred assessments.</summary>
public sealed record DailyReviewTradeSet(IReadOnlyList<Guid> TradeIds)
{
    public int Count => TradeIds.Count;
}

/// <summary>Outcome sets refer to known values only. Metrics coverage uses every closed Trade
/// in the enclosing population; incomplete coverage never supplies a complete total or ratio.</summary>
public sealed record DailyReviewPnlStatistics(PnlMetrics Metrics,
    DailyReviewTradeSet Known, DailyReviewTradeSet Unavailable,
    DailyReviewTradeSet Wins, DailyReviewTradeSet Losses, DailyReviewTradeSet BreakEvens);

public sealed record DailyReviewQualityGroup(DailyReviewTradeQuality Flag, DailyReviewTradeSet Trades);

/// <summary>Quality counts include context Trades too; ClosedUnknownCosts explicitly restricts
/// the cost-coverage union to realized candidates. Missing executions imply unknown components.</summary>
public sealed record DailyReviewPopulation(DailyReviewTradeSet Closed,
    DailyReviewTradeSet ExcludedOpenActivity, DailyReviewTradeSet ExcludedUnavailableLifecycle,
    DailyReviewTradeSet UnknownCommissions, DailyReviewTradeSet UnknownFees,
    DailyReviewTradeSet ClosedUnknownCosts, IReadOnlyList<DailyReviewQualityGroup> SourceQuality);

public sealed record DailyReviewAccountStatistics(DailyReviewReference Account,
    DailyReviewPopulation Population, DailyReviewPnlStatistics Gross, DailyReviewPnlStatistics Net);

public sealed record DailyReviewCurrencyStatistics(string Currency,
    DailyReviewPopulation Population, DailyReviewPnlStatistics Gross, DailyReviewPnlStatistics Net,
    IReadOnlyList<DailyReviewAccountStatistics> Accounts);

/// <summary>No cross-currency money total. Journals remain source context, not statistical inputs.</summary>
public sealed record DailyReviewStatistics(DailyReviewQuery Query, DailyReviewPopulation Population,
    IReadOnlyList<DailyReviewCurrencyStatistics> Currencies);
