using System.Windows.Controls;
using System.Windows;

namespace PersonalTradingJournal.Desktop.Views.Calendar;

public partial class CalendarDayDetailsView : UserControl
{
    public CalendarDayDetailsView() => InitializeComponent();

    private void OnJournalClick(object sender, RoutedEventArgs e)
    {
        // Only the owned modal can launch Journal: detached render components never navigate.
        if (Window.GetWindow(this) is CalendarDayDialogWindow dialog)
            dialog.RequestJournalNavigation();
        e.Handled = true;
    }
}
