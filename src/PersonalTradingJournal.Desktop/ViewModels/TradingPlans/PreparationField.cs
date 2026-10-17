using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PersonalTradingJournal.Desktop.ViewModels.TradingPlans;

/// <summary>Editor-only raw input, never a persisted alternative to TradingPlanContent.</summary>
public sealed class PreparationField : ObservableObject
{
    private readonly Func<bool> _canEdit;
    private readonly Action _changed;
    private readonly CultureInfo _culture;
    private readonly int _limit;
    private readonly bool _numeric;
    private string _text;

    internal PreparationField(string label, string text, int limit, bool numeric,
        CultureInfo culture, Func<bool> canEdit, Action changed)
    {
        Label = label; _text = text; _limit = limit; _numeric = numeric;
        _culture = culture; _canEdit = canEdit; _changed = changed;
    }

    public string Label { get; }
    public string Text
    {
        get => _text;
        set
        {
            if (!_canEdit() || !SetProperty(ref _text, value ?? "")) return;
            OnPropertyChanged(nameof(Error)); OnPropertyChanged(nameof(IsInvalid)); _changed();
        }
    }
    public bool IsReadOnly => !_canEdit();
    public string Error => Text.Length > _limit ? $"Use at most {_limit} characters." :
        _numeric && !TryGetDecimal(out _) ? "Enter an exact decimal price, or leave blank. Group separators and rounded values are not accepted." : "";
    public bool IsInvalid => Error.Length != 0;
    internal void NotifyAccess() => OnPropertyChanged(nameof(IsReadOnly));

    internal bool TryGetDecimal(out decimal? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(Text)) return true;
        const NumberStyles styles = NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite |
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        foreach (var culture in new[] { _culture, CultureInfo.InvariantCulture })
        {
            if (!decimal.TryParse(Text, styles, culture, out var value)) continue;
            // decimal.TryParse can round excessive precision. Never quietly persist that rounding.
            string input = Text.Trim().Replace(culture.NumberFormat.NumberDecimalSeparator, ".", StringComparison.Ordinal);
            if (Canonical(input) != Canonical(value.ToString("0.############################", CultureInfo.InvariantCulture))) continue;
            result = value; return true;
        }
        return false;
    }

    private static string Canonical(string value)
    {
        bool negative = value.StartsWith('-');
        value = value.TrimStart('+', '-');
        if (value.Contains('.')) value = value.TrimEnd('0').TrimEnd('.');
        value = value.TrimStart('0');
        if (value.Length == 0) return "0";
        return (negative ? "-" : "") + value;
    }
}
