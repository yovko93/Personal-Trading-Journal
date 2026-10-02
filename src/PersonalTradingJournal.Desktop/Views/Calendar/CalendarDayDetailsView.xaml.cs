using System.Windows;
using System.Windows.Controls;

namespace PersonalTradingJournal.Desktop.Views.Calendar;

public partial class CalendarDayDetailsView : UserControl
{
    public CalendarDayDetailsView() => InitializeComponent();

    private void OnViewTrade(object sender, RoutedEventArgs e)
    {
        // Button commands run after Click. Release modality before the established View
        // command navigates away from Calendar; no database work belongs in this callback.
        if (Window.GetWindow(this) is CalendarDayDialogWindow dialog) dialog.Close();
    }
}
