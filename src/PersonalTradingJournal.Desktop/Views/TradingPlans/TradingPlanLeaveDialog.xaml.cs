using System.Windows;
using PersonalTradingJournal.Desktop.Dialogs;
using PersonalTradingJournal.Desktop.ViewModels.TradingPlans;

namespace PersonalTradingJournal.Desktop.Views.TradingPlans;

public partial class TradingPlanLeaveDialog : Window
{
    public TradingPlanLeaveDialog() => InitializeComponent();
    public PlanLeaveChoice Choice { get; private set; }
    private void Cancel(object sender, RoutedEventArgs e) => Close();
    private void Discard(object sender, RoutedEventArgs e) { Choice = PlanLeaveChoice.Discard; DialogResult = true; }
    private void Save(object sender, RoutedEventArgs e) { Choice = PlanLeaveChoice.Save; DialogResult = true; }
}
public sealed class WpfTradingPlanInteraction(IDialogService dialogs) : ITradingPlanInteraction
{
    public PlanLeaveChoice ConfirmLeave()
    {
        var dialog = new TradingPlanLeaveDialog();
        var owner = System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
            ?? System.Windows.Application.Current?.MainWindow;
        if (owner?.IsVisible == true) dialog.Owner = owner;
        return dialog.ShowDialog() == true ? dialog.Choice : PlanLeaveChoice.Cancel;
    }
    public bool ConfirmCurrency(string savedCurrency, string currentCurrency) => dialogs.Confirm(new(
        "Review planned currency", $"The Ready revision uses {savedCurrency}; the current Account uses {currentCurrency}. Start a Draft with the same numeric limits in {currentCurrency}? No conversion is performed. Review every amount and confirm the checklist again.", "Start Draft", "Keep Ready"));
}
