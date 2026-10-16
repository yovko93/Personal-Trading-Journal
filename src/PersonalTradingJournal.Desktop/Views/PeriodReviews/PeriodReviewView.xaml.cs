using System.Windows;
using System.Windows.Controls;
using PersonalTradingJournal.Desktop.ViewModels.PeriodReviews;

namespace PersonalTradingJournal.Desktop.Views.PeriodReviews;

public partial class PeriodReviewView : UserControl
{
    public PeriodReviewView() => InitializeComponent();
    private void OnDateError(object? sender, DatePickerDateValidationErrorEventArgs e)
    { e.ThrowException = false; if (DataContext is PeriodReviewViewModel vm) vm.SelectedDate = null; }
    private void OnCitationClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: PeriodCitation citation } || DataContext is not PeriodReviewViewModel vm) return;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
        {
            if (DataContext != vm || vm.Citation != citation) return;
            CitationDetail.UpdateLayout(); CitationDetail.BringIntoView(); CitationDetail.Focus();
        }));
    }
}

/// <summary>Two balanced columns only when readable; narrow/high-DPI content stacks without a nested scroll area.</summary>
public sealed class ReviewCardsPanel : Panel
{
    protected override Size MeasureOverride(Size available)
    {
        double width = double.IsInfinity(available.Width) ? 780 : available.Width;
        int columns = width >= 840 ? 2 : 1; double height = 0;
        for (int i = 0; i < InternalChildren.Count; i += columns)
        {
            double row = 0;
            for (int j = i; j < Math.Min(i + columns, InternalChildren.Count); j++)
            { InternalChildren[j].Measure(new(width / columns, double.PositiveInfinity)); row = Math.Max(row, InternalChildren[j].DesiredSize.Height); }
            height += row;
        }
        return new(width, height);
    }
    protected override Size ArrangeOverride(Size final)
    {
        int columns = final.Width >= 840 ? 2 : 1; double top = 0;
        for (int i = 0; i < InternalChildren.Count; i += columns)
        {
            double row = 0;
            for (int j = i; j < Math.Min(i + columns, InternalChildren.Count); j++) row = Math.Max(row, InternalChildren[j].DesiredSize.Height);
            for (int j = i; j < Math.Min(i + columns, InternalChildren.Count); j++) InternalChildren[j].Arrange(new((j - i) * final.Width / columns, top, final.Width / columns, row));
            top += row;
        }
        return final;
    }
}
