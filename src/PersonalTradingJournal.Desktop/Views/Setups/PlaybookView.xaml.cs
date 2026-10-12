using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Desktop.ViewModels.Setups;

namespace PersonalTradingJournal.Desktop.Views.Setups;

public partial class PlaybookView : UserControl
{
    private bool _syncing;
    public PlaybookView() => InitializeComponent();
    private void OnSetupChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || DataContext is not PlaybookViewModel vm || vm.IsLoading) return;
        if (!vm.TrySelectSetup(SetupSelector.SelectedItem as TradingSetupListItem))
        {
            _syncing = true;
            SetupSelector.SetCurrentValue(ComboBox.SelectedItemProperty, vm.SelectedSetup);
            _syncing = false;
        }
    }
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is PlaybookViewModel old) PropertyChangedEventManager.RemoveHandler(old, OnStateChanged, "");
        if (e.NewValue is PlaybookViewModel current) PropertyChangedEventManager.AddHandler(current, OnStateChanged, "");
    }
    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlaybookViewModel.ValidationAttempt))
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (!IsLoaded || !ReferenceEquals(DataContext, sender)) return;
                var first = Descendants(EditorFields).Concat(Descendants(ExampleRows)).OfType<Control>()
                    .FirstOrDefault(PersonalTradingJournal.Desktop.Validation.FormValidation.GetIsInvalid);
                first?.BringIntoView(); first?.Focus();
            });
            return;
        }
        if (e.PropertyName == nameof(PlaybookViewModel.HistoricalPreviewRequest))
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (IsLoaded && ReferenceEquals(DataContext, sender) && DataContext is PlaybookViewModel { IsHistorical: true })
                    Version.BringIntoView();
            });
        }
        if (e.PropertyName == nameof(PlaybookViewModel.IsWriting) && sender is PlaybookViewModel { IsWriting: false, IsEditing: true } editor && editor.AttachExampleCommand.IsRunning)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (!IsLoaded || !ReferenceEquals(DataContext, editor) || !editor.IsEditing) return;
                var classification = Descendants(ExampleRows).OfType<ComboBox>().LastOrDefault();
                classification?.BringIntoView(); classification?.Focus();
            });
            return;
        }
        if (e.PropertyName != nameof(PlaybookViewModel.IsEditing) &&
            !(e.PropertyName == nameof(PlaybookViewModel.IsWriting) && sender is PlaybookViewModel { IsWriting: false, IsEditing: false })) return;
        Dispatcher.BeginInvoke(() =>
        {
            if (!IsLoaded || DataContext is not PlaybookViewModel vm) return;
            if (vm.IsEditing)
            {
                var box = Descendants(EditorFields).OfType<TextBox>().FirstOrDefault();
                box?.BringIntoView(); box?.Focus();
            }
            else
            {
                EditGuidance.BringIntoView();
                if (EditGuidance.IsEnabled) EditGuidance.Focus(); else SetupSelector.Focus();
            }
        });
    }
    private void OnRemoveExample(object sender, RoutedEventArgs e) => Dispatcher.BeginInvoke(() =>
    {
        if (!IsLoaded) return;
        AttachExample.BringIntoView(); AttachExample.Focus();
    });
    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Let a multiline editor consume its own scroll until it reaches an edge.
        DependencyObject? source = e.OriginalSource as DependencyObject;
        while (source is not null && source != PageScroll)
        {
            if (source is TextBox box && ((e.Delta > 0 && box.VerticalOffset > 0) ||
                (e.Delta < 0 && box.VerticalOffset + box.ViewportHeight < box.ExtentHeight))) return;
            source = source is Visual ? VisualTreeHelper.GetParent(source) : null;
        }
        PageScroll.ScrollToVerticalOffset(PageScroll.VerticalOffset - e.Delta);
        e.Handled = true;
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
