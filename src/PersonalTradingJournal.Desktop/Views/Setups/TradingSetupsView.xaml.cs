using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.ViewModels.Setups;

namespace PersonalTradingJournal.Desktop.Views.Setups;

public partial class TradingSetupsView : UserControl
{
    public TradingSetupsView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is TradingSetupsViewModel previous)
                WeakEventManager<TradingSetupsViewModel, EventArgs>.RemoveHandler(previous, nameof(previous.DetailsOpened), ScrollToDetails);
            if (e.NewValue is TradingSetupsViewModel current)
                WeakEventManager<TradingSetupsViewModel, EventArgs>.AddHandler(current, nameof(current.DetailsOpened), ScrollToDetails);
        };
    }

    private void ScrollToDetails(object? sender, EventArgs e) => PageScroll.ScrollToTop();

    private void ScrollDetailsWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || e.Delta == 0) return;
        // Preserve fractional wheel deltas from precision devices and the Windows wheel preference.
        int lines = SystemParameters.WheelScrollLines;
        double distance = lines < 0 ? PageScroll.ViewportHeight : lines * 16d;
        double offset = Math.Clamp(PageScroll.VerticalOffset - distance * e.Delta / 120d, 0, PageScroll.ScrollableHeight);
        if (offset == PageScroll.VerticalOffset) return;
        PageScroll.ScrollToVerticalOffset(offset);
        e.Handled = true;
    }

    private void PreviewImageUpdated(object sender, DataTransferEventArgs e)
    {
        if (OriginalExample?.Source is null || Window.GetWindow(this)?.IsActive != true) return;
        Dispatcher.InvokeAsync(() =>
        {
            if (BackToCards.IsVisible && Window.GetWindow(this)?.IsActive == true)
            {
                BackToCards.Focus();
                BackToCards.BringIntoView();
            }
        }, DispatcherPriority.Input);
    }

    private void ReturnToGallery(object sender, RoutedEventArgs e)
    {
        if (DataContext is not TradingSetupsViewModel { Preview.Card: { } card }) return;
        Dispatcher.InvokeAsync(() =>
        {
            if (SetupExampleCards.IsVisible &&
                SetupExampleCards.ItemContainerGenerator.ContainerFromItem(card) is ContentPresenter item &&
                SetupExampleCards.ItemTemplate.FindName("OpenExampleCard", item) is Button opener)
                opener.Focus();
        }, DispatcherPriority.Input);
    }
}
