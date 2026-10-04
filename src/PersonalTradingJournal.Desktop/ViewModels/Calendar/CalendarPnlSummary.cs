using System.Globalization;
using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Desktop.Converters;

namespace PersonalTradingJournal.Desktop.ViewModels.Calendar;

/// <summary>Formatting and sign only; the reader supplies the complete currency-specific metrics.</summary>
public sealed record CalendarPnlSummary(string Currency, ClosedTradeMetrics Metrics, bool TintBackground = false)
{
    public decimal? Amount => Metrics.EffectiveNet.Total;
    public string AmountText => Amount is { } value
        ? $"{value.ToString("N2", CultureInfo.CurrentCulture)} {Currency}" : $"— {Currency}";
    public string TradeCountText => $"{Metrics.ClosedTradeCount} {(Metrics.ClosedTradeCount == 1 ? "Trade" : "Trades")}";
    public PnLOutcome Outcome => Amount is null ? PnLOutcome.None
        : Amount > 0m ? PnLOutcome.Positive : Amount < 0m ? PnLOutcome.Negative : PnLOutcome.Zero;
    public string Description => $"{(Amount is { } value ? value.ToString(CultureInfo.CurrentCulture) : "P&L unavailable")} {Currency}; {TradeCountText}. " +
        (Metrics.EffectiveNet.IsEstimated ? "Estimated — commission/fees unknown; known Gross is used for those Trades. " : "") +
        (Amount is null ? $"Economics available for {Metrics.EffectiveNet.Coverage.KnownTradeCount} of {Metrics.ClosedTradeCount} closed Trades; no complete total is available."
            : Metrics.EffectiveNet.IsEstimated ? $"{Metrics.EffectiveNet.EstimatedTradeCount} estimated Trades." : "Verified Net P&L.");
}
