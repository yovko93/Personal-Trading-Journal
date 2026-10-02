using System.Windows;
using System.Windows.Input;

namespace PersonalTradingJournal.Desktop.Views.Calendar;

public partial class CalendarDayDialogWindow : Window
{
    public CalendarDayDialogWindow() => InitializeComponent();
    private void OnClose(object sender, RoutedEventArgs e) => Close();
    private void OnDialogKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        Close();
    }
}
