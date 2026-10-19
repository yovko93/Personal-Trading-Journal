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
    public bool ConfirmDelete(string date, string scope) => dialogs.Confirm(new(
        "Delete Plan", $"Delete the loaded Plan for {date}, {scope}, including its Draft and history from normal browsing? Unsaved input will be discarded. Retained historical evidence and images are not erased.",
        "Delete Plan", "Keep Plan"));

}
