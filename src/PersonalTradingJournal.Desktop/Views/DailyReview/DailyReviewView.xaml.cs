using System.Windows.Controls;
using PersonalTradingJournal.Desktop.ViewModels.DailyReview;

namespace PersonalTradingJournal.Desktop.Views.DailyReview;

public partial class DailyReviewView : UserControl
{
    public DailyReviewView() => InitializeComponent();

    private void OnDateValidationError(object? sender, DatePickerDateValidationErrorEventArgs e)
    {
        e.ThrowException = false;
        if (DataContext is DailyReviewViewModel vm) vm.SelectedDate = null;
    }
}
