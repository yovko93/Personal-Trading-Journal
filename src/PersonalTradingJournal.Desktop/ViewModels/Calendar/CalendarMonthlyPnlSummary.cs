using System.Globalization;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Desktop.Converters;

namespace PersonalTradingJournal.Desktop.ViewModels.Calendar;

public sealed record CalendarMonthlyPnlSummary(TradingCalendarMonthlyPnl Summary)
{
    public decimal? Amount => Summary.Total;
    public string AmountText => Amount is { } value
        ? $"{value.ToString("N2", CultureInfo.CurrentCulture)} {Summary.Currency}" : $"— {Summary.Currency}";
    public PnLOutcome Outcome => Amount is null ? PnLOutcome.None
        : Amount > 0 ? PnLOutcome.Positive : Amount < 0 ? PnLOutcome.Negative : PnLOutcome.Zero;
    public string Description => $"Monthly P/L: {AmountText}; {Summary.Coverage.ClosedTradeCount} closed Trades in the selected New York month. "
        + (Summary.IsEstimated ? $"Estimated — commission/fees unknown for {Summary.EstimatedTradeCount} Trades. " : "")
        + (Amount is null ? $"P&L unavailable; economics available for {Summary.Coverage.KnownTradeCount} of {Summary.Coverage.ClosedTradeCount} Trades."
            : Summary.IsEstimated ? "Known Gross is used as estimated Net." : "Verified Net P&L.");
}
