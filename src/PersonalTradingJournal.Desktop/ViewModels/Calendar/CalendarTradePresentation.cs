using System.Globalization;
using PersonalTradingJournal.Application.Common.Time;
using PersonalTradingJournal.Application.Trades;

namespace PersonalTradingJournal.Desktop.ViewModels.Calendar;

/// <summary>Formatting only, over the selected day's authoritative browse row.</summary>
public sealed record CalendarTradePresentation(TradeListItem Trade)
{
    public string ClosingTime => TradingTimePolicy.ConvertUtcToTradingTime(Trade.ClosedAtUtc!.Value)
        .ToString("HH:mm:ss zzz", CultureInfo.CurrentCulture);
    public string SizeText => Trade.Size.ToString("0.############################", CultureInfo.CurrentCulture);
    public decimal? Amount => Trade.EffectiveNet.Value;
    public bool IsEstimated => Trade.EffectiveNet.IsEstimated;
    public string AmountText => Amount is { } amount
        ? $"{amount.ToString("N2", CultureInfo.CurrentCulture)} {Trade.Currency}" : $"— {Trade.Currency}";
    public string NetDescription => $"{(Amount is { } value ? value.ToString(CultureInfo.CurrentCulture) : "P&L unavailable")} {Trade.Currency}. " +
        (IsEstimated ? "Estimated Net — commission/fees unknown; Gross is used."
            : Amount.HasValue ? "Verified Net P&L." : "Gross and Net economics unavailable.");
    public string AccessibleName => $"{ClosingTime} New York; {Trade.InstrumentSymbol}; {Trade.TradingAccountName}; " +
        $"{Trade.Direction}; Size {SizeText}; {NetDescription}";
}
