using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Desktop.ViewModels.TradingPlans;

public sealed class PlanChecklistItem(SessionPreparationViewModel owner, TradingPlanChecklist flag, string label, string description) : ObservableObject
{
    public TradingPlanChecklist Flag { get; } = flag;
    public string Label { get; } = label;
    public string Description { get; } = description;
    public bool IsChecked { get => owner.Confirmed.HasFlag(Flag); set => owner.SetConfirmation(Flag, value); }
    public bool CanInteract => owner.CanConfirm && (Flag != TradingPlanChecklist.RiskLimits || !owner.HasMonetaryLimits || owner.CurrencyVerified);
    internal void NotifyState() { OnPropertyChanged(nameof(IsChecked)); OnPropertyChanged(nameof(CanInteract)); }
}

public sealed class PlanRuleItem : ObservableObject
{
    private readonly SessionPreparationViewModel _owner;
    private string _text;
    private bool _available;
    private string _availability = "Load Rules to check current availability; the selected revision is retained.";
    internal PlanRuleItem(SessionPreparationViewModel owner, TradingPlanRuleLink link)
    {
        _owner = owner; Link = link; _text = $"Selected Rule revision {link.ExpectedRevision}. Load Rules to inspect its saved definition and availability.";
        RemoveCommand = new RelayCommand(() => owner.RemoveRule(this), () => owner.CanEdit);
        OpenCommand = new AsyncRelayCommand(ct => owner.RequestRuleAsync(this, ct), () => !owner.IsBusy && !owner.IsDisposed && _available && owner.RuleNavigation is not null);
    }
    public TradingPlanRuleLink Link { get; private set; }
    public string Text => _text;
    public string Title => _text.Split('\n')[0];
    public string Availability => _availability;
    public IRelayCommand RemoveCommand { get; }
    public IAsyncRelayCommand OpenCommand { get; }
    internal void Replace(TradingPlanRuleLink link) { Link = link; OnPropertyChanged(nameof(Link)); }
    internal void Present(string text, bool available, string? availability = null)
    { _text = text; _available = available; _availability = availability ?? (available ? "Current Rule available; this link is not an assessment." : "Current navigation unavailable; retained context remains below.");
        OnPropertyChanged(nameof(Text)); OnPropertyChanged(nameof(Title)); OnPropertyChanged(nameof(Availability)); NotifyAccess(); }
    internal void NotifyAccess() { RemoveCommand.NotifyCanExecuteChanged(); OpenCommand.NotifyCanExecuteChanged(); }
}

public sealed record PlanReadinessBlocker(string Field, string Message);
