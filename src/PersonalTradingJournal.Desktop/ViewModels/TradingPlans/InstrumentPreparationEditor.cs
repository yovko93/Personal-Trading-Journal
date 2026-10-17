using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Desktop.ViewModels.TradingPlans;

public sealed class InstrumentPreparationEditor : ObservableObject
{
    private readonly Func<bool> _canEdit;
    private readonly Action _changed;
    private TradingPlanBias? _bias;
    private string _label;

    internal InstrumentPreparationEditor(TradingPlanInstrumentContext content, string label, CultureInfo culture,
        Func<bool> canEdit, Action changed)
    {
        InstrumentId = content.InstrumentId; _label = label; _bias = content.Bias;
        _canEdit = canEdit; _changed = changed;
        Thesis = new("Market Thesis", content.MarketThesis, TradingPlanContent.MaximumTextLength, false, culture, canEdit, changed);
        var l = content.Levels;
        var values = new[] { l.PreviousDayHigh, l.PreviousDayLow, l.AsiaHigh, l.AsiaLow, l.LondonHigh, l.LondonLow };
        string[] names = ["Previous Day High", "Previous Day Low", "Asia High", "Asia Low", "London High", "London Low"];
        Levels = names.Select((name, i) => new PreparationField(name, values[i]?.ToString(culture) ?? "",
            128, true, culture, canEdit, changed)).ToArray();
    }
    public Guid InstrumentId { get; }
    public string Label { get => _label; internal set => SetProperty(ref _label, value); }
    public PreparationField Thesis { get; }
    public IReadOnlyList<PreparationField> Levels { get; }
    public TradingPlanBias? Bias
    {
        get => _bias;
        set
        {
            if (!_canEdit() || (value.HasValue && !Enum.IsDefined(value.Value))) return;
            if (SetProperty(ref _bias, value)) _changed();
        }
    }
    internal IEnumerable<PreparationField> Fields => new[] { Thesis }.Concat(Levels);
    internal TradingPlanInstrumentContext Build()
    {
        var values = Levels.Select(f => { if (!f.TryGetDecimal(out var v)) throw new InvalidOperationException("Invalid price."); return v; }).ToArray();
        return new(InstrumentId, Bias, Thesis.Text, new(values[0], values[1], values[2], values[3], values[4], values[5]));
    }
}
