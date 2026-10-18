using System.Windows.Controls;
using System.ComponentModel;
using System.Windows;
using PersonalTradingJournal.Desktop.Interactions;
using PersonalTradingJournal.Desktop.ViewModels.TradingPlans;

namespace PersonalTradingJournal.Desktop.Views.TradingPlans;

public partial class TradingPlanHistoryView : UserControl
{
    private readonly PageFocus _focus;
    private SessionPreparationViewModel? _shown;
    public TradingPlanHistoryView() { InitializeComponent(); _focus = new(this); }
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        _focus.Invalidate(); _shown = null;
        if (e.OldValue is TradingPlanWorkspaceViewModel old) PropertyChangedEventManager.RemoveHandler(old, OnState, nameof(old.HistoricalViewer));
        if (e.NewValue is TradingPlanWorkspaceViewModel current) PropertyChangedEventManager.AddHandler(current, OnState, nameof(current.HistoricalViewer));
    }
    private void OnState(object? sender, PropertyChangedEventArgs e)
    {
        if (DataContext is not TradingPlanWorkspaceViewModel vm || ReferenceEquals(_shown, vm.HistoricalViewer)) return;
        _shown = vm.HistoricalViewer;
        if (_shown is null) _focus.Invalidate();
        else if (vm.IsHistorySelected) _focus.Request(() => HistoricalIdentity);
    }
}
