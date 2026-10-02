using System.Windows.Controls;
using System.Windows.Input;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;

namespace PersonalTradingJournal.Desktop.Views.Calendar;

public partial class CalendarView : UserControl
{
    public CalendarView() => InitializeComponent();

    private void OnDayClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border cell) { cell.Focus(); SelectDay(cell); e.Handled = true; }
    }

    private void OnDayKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Space) || sender is not Border cell) return;
        SelectDay(cell);
        e.Handled = true;
    }

    private void SelectDay(Border cell)
    {
        if (DataContext is CalendarViewModel vm && cell.DataContext is CalendarDayCell day && vm.SelectDayCommand.CanExecute(day))
            vm.SelectDayCommand.Execute(day);
    }
}
