using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
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
