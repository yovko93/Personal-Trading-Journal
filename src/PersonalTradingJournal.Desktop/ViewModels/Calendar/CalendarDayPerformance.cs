using System.Globalization;
using PersonalTradingJournal.Application.Common.Time;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.Formatting;

namespace PersonalTradingJournal.Desktop.ViewModels.Calendar;

/// <summary>Presentation series over authoritative Effective Net values, never price-derived economics.</summary>
public sealed record CalendarDayPerformance(string Currency, IReadOnlyList<CalendarDayPerformancePoint> Points)
{
    public static IReadOnlyList<CalendarDayPerformance> From(IEnumerable<TradeListItem> trades)
    {
        // The date reader rejects unattributable rows; defensive presentation never invents a time.
        return trades.Where(t => t.ClosedAtUtc.HasValue).GroupBy(t => t.Currency, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(currency =>
            {
                decimal? cumulative = 0m;
                bool estimated = false;
                var points = new List<CalendarDayPerformancePoint>();
                // Simultaneous closures have one combined chart point, not an invented intraday fill order.
                foreach (var closure in currency.GroupBy(t => t.ClosedAtUtc!.Value.ToUniversalTime()).OrderBy(g => g.Key))
                {
                    foreach (TradeListItem trade in closure.OrderBy(t => t.Id))
                    {
                        cumulative = cumulative is { } sum && trade.EffectiveNet.Value is { } amount ? checked(sum + amount) : null;
                        estimated |= trade.EffectiveNet.IsEstimated;
                    }
                    points.Add(new(closure.Key, cumulative, estimated, closure.Count(), currency.Key));
                }
                return new CalendarDayPerformance(currency.Key, points.AsReadOnly());
            }).ToArray();
    }
}

public sealed record CalendarDayPerformancePoint(DateTimeOffset ClosedAtUtc, decimal? Value, bool IsEstimated,
    int TradeCount, string Currency)
{
    public string TimeText => TradingTimePolicy.ConvertUtcToTradingTime(ClosedAtUtc).ToString("HH:mm:ss", CultureInfo.CurrentCulture);
    private string DetailedTimeText => TradingTimestampFormatter.FormatNewYork(ClosedAtUtc, "HH:mm:ss.FFFFFFF", CultureInfo.CurrentCulture) + " New York";
    public string Description => $"{DetailedTimeText}; {TradeCount} closed {(TradeCount == 1 ? "Trade" : "Trades")}. Cumulative realized P&L: " +
        (Value is { } value ? $"{value.ToString(CultureInfo.CurrentCulture)} {Currency}. " : $"Unavailable {Currency}: incomplete economics. ") +
        (IsEstimated ? "Includes estimated Net — commission/fees unknown; Gross is used." : "");
}
