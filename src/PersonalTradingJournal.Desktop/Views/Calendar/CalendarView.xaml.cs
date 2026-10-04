using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;

namespace PersonalTradingJournal.Desktop.Views.Calendar;

public partial class CalendarView : UserControl
{
    private CalendarDayDialogWindow? _dayDialog;
    internal CalendarDayDialogWindow? DayDialog => _dayDialog;
    public CalendarView()
    {
        InitializeComponent();
        Unloaded += (_, _) => _dayDialog?.Close();
    }

    private void OnDayInvoked(object sender, RoutedEventArgs e)
    {
        if (sender is Border cell) { cell.Focus(); SelectDay(cell); e.Handled = true; }
    }

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
        {
            vm.SelectDayCommand.Execute(day);
            // Detached components have no native owner. The real page opens an owned,
            // modal WPF window immediately, so loading/errors are visible inside it.
            if (_dayDialog is not null || Window.GetWindow(this) is not { IsVisible: true } owner) return;
            var dialog = new CalendarDayDialogWindow
            {
                Owner = owner,
                DataContext = vm,
            };
            dialog.FitToOwner();
            dialog.Resources.MergedDictionaries.Add(Resources);
            dialog.Closed += (_, _) => { _dayDialog = null; ModalShade.Visibility = Visibility.Collapsed; };
            _dayDialog = dialog;
            ModalShade.Visibility = Visibility.Visible;
            System.ComponentModel.PropertyChangedEventHandler selectionChanged = (_, args) =>
            {
                if (args.PropertyName == nameof(vm.HasSelectedDate) && !vm.HasSelectedDate) dialog.Close();
            };
            vm.PropertyChanged += selectionChanged;
            try { _ = dialog.ShowDialog(); }
            finally
            {
                vm.PropertyChanged -= selectionChanged;
                _dayDialog = null;
                ModalShade.Visibility = Visibility.Collapsed;
                if (vm.IsDayLoading) vm.CancelDayCommand.Execute(null);
                // Preserve month/filter/date state. The same-grid container survives refresh.
                if (IsVisible) FindSelectedCell(this)?.Focus();
            }
            // ShowDialog has unwound and the Calendar is no longer disabled. Never navigate
            // from Closing/Closed, where a veto or re-entrant unload could lose the editor.
            if (dialog.JournalNavigationRequest is { } request)
                _ = vm.NavigateToJournalAsync(request.Date, request.Account);
        }
    }

    private static CalendarDayHost? FindSelectedCell(DependencyObject parent)
    {
        if (parent is CalendarDayHost { DataContext: CalendarDayCell { IsSelected: true } } cell) return cell;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            if (FindSelectedCell(System.Windows.Media.VisualTreeHelper.GetChild(parent, i)) is { } selected) return selected;
        return null;
    }
}
