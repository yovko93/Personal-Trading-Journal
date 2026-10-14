using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.DailyReview;

namespace PersonalTradingJournal.Application.PeriodReviews;

/// <summary>A New York date with at least one closed Trade, including zero/unknown P&L.
/// Open activity and Journal-only dates do not create active trading days.</summary>
public sealed record PeriodTradingDay(DateOnly Date, DailyReviewTradeSet ClosedTrades);

/// <summary>Trade counts, not execution counts. MissingCostTrades is the union, not a sum.</summary>
public sealed record PeriodCostCoverage(DailyReviewTradeSet MissingCommissionTrades,
    DailyReviewTradeSet MissingFeeTrades, DailyReviewTradeSet MissingCostTrades);

/// <summary>A complete-population extremum in the enclosing currency/basis. Includes every tied ID.</summary>
public sealed record PeriodTradeExtreme(decimal Value, DailyReviewTradeSet Trades);

/// <summary>All means/extrema require complete coverage. AveragePerClosedTrade divides by all
/// closed Trades (including break-evens); AveragePerActiveDay divides by distinct closure dates.
/// Outcomes retains explicitly labeled known-subset facts, never an incomplete period total.</summary>
public sealed record PeriodPnlStatistics(DailyReviewPnlStatistics Outcomes,
    AveragePnlMetric AveragePerClosedTrade, AveragePnlMetric AveragePerActiveDay,
    PeriodTradeExtreme? BestTrade, PeriodTradeExtreme? WorstTrade);

public sealed record PeriodGroupStatistics(DailyReviewPopulation Population,
    IReadOnlyList<PeriodTradingDay> ActiveDays, PeriodCostCoverage Costs,
    PeriodPnlStatistics Gross, PeriodPnlStatistics Net);
public sealed record PeriodAccountStatistics(DailyReviewReference Account, PeriodGroupStatistics Statistics);
public sealed record PeriodCurrencyStatistics(string Currency, PeriodGroupStatistics Statistics,
    IReadOnlyList<PeriodAccountStatistics> Accounts);

/// <summary>No cross-currency money total. Original evidence, including Journals, Playbooks,
/// Rules and assessments, is carried unchanged; this calculator makes no compliance claims.</summary>
public sealed record PeriodReviewStatistics(PeriodReviewEvidence Evidence, DailyReviewPopulation Population,
    IReadOnlyList<PeriodTradingDay> ActiveDays, IReadOnlyList<PeriodCurrencyStatistics> Currencies);
