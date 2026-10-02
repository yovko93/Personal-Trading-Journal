using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Desktop.ViewModels.Trades;
using PersonalTradingJournal.Application.Common.Time;
using PersonalTradingJournal.Application.Trades;

namespace PersonalTradingJournal.Desktop.ViewModels.Calendar;

/// <summary>Formatting only, over the selected day's authoritative browse row.</summary>
public sealed class CalendarTradePresentation(TradeListItem trade, CalendarTradeClassification? classification = null) : ObservableObject
{
    public TradeListItem Trade { get; } = trade;
    public string ClosingTime => Trade.ClosedAtUtc is { } closed
        ? TradingTimePolicy.ConvertUtcToTradingTime(closed).ToString("HH:mm:ss.FFFFFFF", CultureInfo.CurrentCulture) : "—";
    public string ClosingTimeDescription => Trade.ClosedAtUtc is { } closed
        ? TradingTimePolicy.ConvertUtcToTradingTime(closed).ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF 'UTC'zzz", CultureInfo.CurrentCulture) + " New York"
        : "Closing time unavailable";
    public string SetupText => classification?.SetupId is null ? "None assigned"
        : ReferenceName(classification.SetupName, classification.IsSetupActive, "Setup");
    public string MistakesText => classification?.Mistakes.Count > 0
        ? string.Join(", ", classification.Mistakes.Select(m => ReferenceName(m.Name, m.IsActive, "Mistake"))) : "None assigned";
    private static string ReferenceName(string? name, bool? active, string kind) => name is null
        ? $"Unavailable {kind}" : name + (active == false ? " (inactive)" : "");
    private bool _isExpanded;
    public bool IsExpanded { get => _isExpanded; internal set => SetProperty(ref _isExpanded, value); }
    public TradesViewModel? Editor { get; internal set; }
    public string SizeText => Trade.Size.ToString("0.############################", CultureInfo.CurrentCulture);
    public decimal? Amount => Trade.EffectiveNet.Value;
    public bool IsEstimated => Trade.EffectiveNet.IsEstimated;
    public string AmountText => Amount is { } amount
        ? $"{amount.ToString("N2", CultureInfo.CurrentCulture)} {Trade.Currency}" : $"— {Trade.Currency}";
    public string NetDescription => $"{(Amount is { } value ? value.ToString(CultureInfo.CurrentCulture) : "P&L unavailable")} {Trade.Currency}. " +
        (IsEstimated ? "Estimated Net — commission/fees unknown; Gross is used."
            : Amount.HasValue ? "Verified Net P&L." : "Gross and Net economics unavailable.");
    public string AccessibleName => $"{ClosingTimeDescription}; {Trade.InstrumentSymbol}; {Trade.TradingAccountName}; " +
        $"{Trade.Direction}; Size {SizeText}; {NetDescription}; Setup: {SetupText}; Trading Mistakes: {MistakesText}";
}
