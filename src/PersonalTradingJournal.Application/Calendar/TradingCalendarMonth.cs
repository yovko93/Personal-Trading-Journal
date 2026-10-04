using PersonalTradingJournal.Application.Analytics;

namespace PersonalTradingJournal.Application.Calendar;

/// <summary>Null Metrics means no closed Trades, not a known zero P&amp;L result.</summary>
public sealed record TradingCalendarDay(DateOnly Date, bool IsInDisplayedMonth, ClosedTradeMetrics? Metrics)
{
    public int ClosedTradeCount => Metrics?.ClosedTradeCount ?? 0;
    public decimal? EffectiveNetTotal => Metrics?.EffectiveNet.Total;
    public bool IsEstimated => Metrics?.EffectiveNet.IsEstimated ?? false;
}

/// <summary>All seven New York dates contribute, including Sunday after the future Saturday-cell summary.</summary>
public sealed record TradingCalendarWeek(
    DateOnly Monday, DateOnly Sunday, IReadOnlyList<TradingCalendarDay> Days, ClosedTradeMetrics? Metrics)
{
    public int ClosedTradeCount => Metrics?.ClosedTradeCount ?? 0;
    public decimal? EffectiveNetTotal => Metrics?.EffectiveNet.Total;
    public bool IsEstimated => Metrics?.EffectiveNet.IsEstimated ?? false;
}

/// <summary>One historical pricing currency; no cross-currency P&amp;L is exposed.</summary>
public sealed record TradingCalendarCurrency(string Currency, IReadOnlyList<TradingCalendarWeek> Weeks);

/// <summary>Complete Monday–Sunday grid even when there are no closed Trades or currency buckets.</summary>
public sealed record TradingCalendarMonth(
    DateOnly MonthStart,
    DateOnly GridStart,
    DateOnly GridEnd,
    IReadOnlyList<DateOnly> GridDates,
    IReadOnlyList<TradingCalendarCurrency> Currencies);
