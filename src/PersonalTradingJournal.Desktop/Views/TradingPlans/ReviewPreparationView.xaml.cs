using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PersonalTradingJournal.Desktop.ViewModels.TradingPlans;

namespace PersonalTradingJournal.Desktop.Views.TradingPlans;
public partial class ReviewPreparationView : UserControl
{
    public ReviewPreparationView() => InitializeComponent();
    public static readonly DependencyProperty PreparationProperty = DependencyProperty.Register(nameof(Preparation), typeof(ReviewPreparation), typeof(ReviewPreparationView));
    public ReviewPreparation? Preparation { get => (ReviewPreparation?)GetValue(PreparationProperty); set => SetValue(PreparationProperty, value); }
    public static readonly DependencyProperty OpenCommandProperty = DependencyProperty.Register(nameof(OpenCommand), typeof(ICommand), typeof(ReviewPreparationView));
    public ICommand? OpenCommand { get => (ICommand?)GetValue(OpenCommandProperty); set => SetValue(OpenCommandProperty, value); }
}
