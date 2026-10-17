using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.Interactions;
using PersonalTradingJournal.Desktop.ViewModels.Trades;

namespace PersonalTradingJournal.Desktop.Views.Trades;

public partial class TradesView : UserControl
{
    private readonly PageFocus _focus;
    private TradesViewModel? _observed;
    private Guid? _returnTradeId;
    private IInputElement? _removedDetailFocus;
    public TradesView()
    {
        InitializeComponent();
        _focus = new(this);
        FocusManager.SetIsFocusScope(this, true);
        Loaded += (_, _) => { Observe(); if (_observed?.HasBreakdownScope == true) _focus.Request(() => BackToBreakdownButton); };
        Unloaded += (_, _) => { _focus.Invalidate(); Unobserve(); };
        DataContextChanged += (_, _) => { _focus.Invalidate(); Unobserve(); if (IsLoaded) Observe(); };
        AddHandler(Button.ClickEvent, new RoutedEventHandler(OnAction));
        // Input after an operation started owns focus, even if it only moves the page scroll viewer.
        PreviewKeyDown += (_, _) => _removedDetailFocus = null;
        PreviewMouseDown += (_, _) => _removedDetailFocus = null;
        PreviewMouseWheel += (_, _) => _removedDetailFocus = null;
    }
    private void Observe()
    {
        if (DataContext is not TradesViewModel vm || ReferenceEquals(vm, _observed)) return;
        _observed = vm; vm.PropertyChanged += OnStateChanged;
    }
    private void Unobserve()
    {
        if (_observed is { } vm) vm.PropertyChanged -= OnStateChanged;
        _observed = null; _removedDetailFocus = null;
    }
    private void OnAction(object sender, RoutedEventArgs e)
    {
        if (_observed is not { HasBreakdownScope: true } vm || e.OriginalSource is not Button button) return;
        if (ReferenceEquals(button.Command, vm.ShowTradeDetailCommand) && button.CommandParameter is TradeListItem trade)
        {
            _returnTradeId = trade.Id;
            _focus.Request(() => TradeDetailsHeading);
        }
        else if (ReferenceEquals(button.Command, vm.CloseTradeDetailCommand)) _focus.Request(ReturnTarget);
        else if (ReferenceEquals(button.Command, vm.ExitBreakdownScopeCommand)) _focus.Request(() => TradesRefreshButton);
    }
    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_observed?.HasBreakdownScope != true) return;
        var focused = Keyboard.FocusedElement ?? FocusManager.GetFocusedElement(this);
        if (e.PropertyName == nameof(TradesViewModel.IsDeletingTrade) && _observed.IsDeletingTrade &&
            FocusManager.GetFocusedElement(this) is DependencyObject deletingInput && TradeDetailCard.IsAncestorOf(deletingInput))
            _removedDetailFocus = deletingInput as IInputElement;
        // A successful deletion can remove the detail and its focused action without a Close click.
        if (e.PropertyName == nameof(TradesViewModel.IsTradeDetailVisible) && !_observed.IsTradeDetailVisible &&
            FocusManager.GetFocusedElement(this) is DependencyObject detailInput && TradeDetailCard.IsAncestorOf(detailInput))
        {
            if (_observed.IsDeletingTrade) _removedDetailFocus = detailInput as IInputElement;
            else _focus.Request(ReturnTarget);
        }
        if (e.PropertyName == nameof(TradesViewModel.IsDeletingTrade) && !_observed.IsDeletingTrade && _removedDetailFocus is { } removed)
        {
            _removedDetailFocus = null;
            // Wait for the committed reload to enable actions, but newer user focus still wins.
            var logical = FocusManager.GetFocusedElement(this);
            if (!_observed.IsTradeDetailVisible && (ReferenceEquals(logical, removed) || removed is UIElement { IsVisible: false } hidden &&
                (logical is null || logical is UIElement ancestor && ancestor.IsAncestorOf(hidden))) &&
                (Keyboard.FocusedElement is null || Keyboard.FocusedElement is DependencyObject input && IsAncestorOf(input)))
                _focus.Request(ReturnTarget);
        }
        // Only repair a focused disappearing row, not every background refresh or edit.
        if (e.PropertyName != nameof(TradesViewModel.RecentTrades) ||
            focused is not Button { CommandParameter: TradeListItem row } button ||
            (!IsAncestorOf(button) && !ReferenceEquals(FocusManager.GetFocusedElement(this), button))) return;
        if (!_observed.RecentTrades.Any(t => t.Id == row.Id)) _focus.Request(ReturnTarget);
    }
    private FrameworkElement ReturnTarget()
    {
        var buttons = Descendants(this).OfType<Button>().Where(b => ReferenceEquals(b.Command, _observed?.ShowTradeDetailCommand) &&
            b.CommandParameter is TradeListItem && b.IsEnabled && b.IsVisible).ToArray();
        return buttons.FirstOrDefault(b => ((TradeListItem)b.CommandParameter).Id == _returnTradeId)
            ?? buttons.FirstOrDefault() ?? (FrameworkElement)TradesRefreshButton;
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
