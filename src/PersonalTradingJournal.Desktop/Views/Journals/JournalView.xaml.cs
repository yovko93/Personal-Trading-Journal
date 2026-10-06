using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.ViewModels.Journals;

namespace PersonalTradingJournal.Desktop.Views.Journals;

public partial class JournalView : UserControl
{
    private DispatcherOperation? _dateValidation;

    public JournalView()
    {
        InitializeComponent();
        JournalDate.Language = XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag);
        JournalDate.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnDateTextChanged));
    }

    // DatePicker commits its SelectedDate on Enter/focus loss. Until then, a typed
    // different/invalid date must not allow saving against the old bound date.
    private void OnDateTextChanged(object sender, TextChangedEventArgs e)
    {
        ValidateDateText();
        ScheduleDateValidation();
    }

    private void OnDateChanged(object? sender, SelectionChangedEventArgs e) => ScheduleDateValidation();
    private void OnLoaded(object sender, RoutedEventArgs e) => ScheduleDateValidation();
    private void OnUnloaded(object sender, RoutedEventArgs e) { _dateValidation?.Abort(); _dateValidation = null; }

    // This page owns vertical scrolling. Let a bounded editor scroll its text first,
    // but do not let a disabled or exhausted inner TextBox viewer swallow the wheel.
    // Revision text grows with the page; no nested revision/list viewport is needed.
    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || e.Delta == 0 || (Keyboard.Modifiers & ModifierKeys.Shift) != 0) return;
        for (DependencyObject? node = e.OriginalSource as DependencyObject; node is not null && node != JournalScroller;
             node = node is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
        {
            if (node is ScrollViewer inner && inner.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled &&
                (e.Delta > 0 ? inner.VerticalOffset > 0 : inner.VerticalOffset < inner.ScrollableHeight)) return;
        }
        if (!(e.Delta > 0 ? JournalScroller.VerticalOffset > 0 : JournalScroller.VerticalOffset < JournalScroller.ScrollableHeight)) return;
        e.Handled = true;
        JournalScroller.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = MouseWheelEvent,
            Source = JournalScroller,
        });
    }

    private void OnDateValidationError(object? sender, DatePickerDateValidationErrorEventArgs e)
    {
        e.ThrowException = false;
        if (DataContext is JournalViewModel vm) vm.HasDateInputError = true;
    }

    private void ScheduleDateValidation()
    {
        _dateValidation?.Abort();
        _dateValidation = Dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(() =>
        {
            _dateValidation = null;
            ValidateDateText();
        }));
    }

    private void ValidateDateText()
    {
        if (DataContext is not JournalViewModel vm || JournalDate.Template.FindName("PART_TextBox", JournalDate) is not TextBox input)
            return;
        vm.HasDateInputError = !DateTime.TryParse(input.Text, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out DateTime date)
            || vm.SelectedDate is null || date.Date != vm.SelectedDate.Value.Date;
    }
}
