using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.ViewModels.Setups;

namespace PersonalTradingJournal.Desktop.Views.Setups;

public partial class TradingSetupsView : UserControl
{
    public TradingSetupsView() => InitializeComponent();

    private void PreviewVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true && Window.GetWindow(this)?.IsActive == true)
            Dispatcher.InvokeAsync(() => { if (BackToCards.IsVisible && Window.GetWindow(this)?.IsActive == true) BackToCards.Focus(); }, DispatcherPriority.Input);
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
