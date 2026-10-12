using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using PersonalTradingJournal.Desktop.ViewModels.Rules;
using PersonalTradingJournal.Desktop.Views.Rules;
using PersonalTradingJournal.Domain.Rules;

namespace PersonalTradingJournal.Desktop.Dialogs;

public sealed class WpfRuleAssessmentDialog(Func<RuleAssessmentViewModel> create) : IRuleAssessmentDialog
{
    public void ShowTarget(TradingRuleAssessmentTarget target) => Show(target, null);
    public void ShowRuleHistory(Guid ruleId) => Show(null, ruleId);
    private void Show(TradingRuleAssessmentTarget? target, Guid? rule)
    {
        var vm = create();
        var owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
            ?? System.Windows.Application.Current.MainWindow;
        var focus = Keyboard.FocusedElement;
        var window = new Window { Title = "Rule assessments", Content = new RuleAssessmentView { DataContext = vm },
            Width = Math.Min(960, owner?.ActualWidth ?? 960), Height = Math.Min(820, owner?.ActualHeight ?? 820),
            MinWidth = 340, MinHeight = 320, Owner = owner, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        window.SetResourceReference(FrameworkElement.StyleProperty, "PtjWindowStyle");
        bool acceptedOwnerClose = false;
        var owners = new List<Window>();
        for (var ancestor = owner; ancestor is not null; ancestor = ancestor.Owner) owners.Add(ancestor);
        CancelEventHandler ownerClosing = (_, e) =>
        {
            if (e.Cancel || acceptedOwnerClose) return;
            acceptedOwnerClose = vm.TryLeave();
            if (!acceptedOwnerClose) e.Cancel = true;
            else window.Close(); // End this modal frame before WPF destroys owned windows.
            // Another owner guard may veto after ours. Consent is for this closing event only.
            window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Send,
                new Action(() => acceptedOwnerClose = false));
        };
        // WPF does not raise Closing for an owned window when its ancestor closes.
        // Include the main owner even when this dialog was opened inside Day Performance.
        foreach (var ancestor in owners) ancestor.Closing += ownerClosing;
        window.Closing += (_, e) => { if (!acceptedOwnerClose && !vm.TryLeave()) e.Cancel = true; };
        window.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; window.Close(); } };
        vm.CloseRequested += window.Close;
        window.Loaded += async (_, _) => await vm.OpenAsync(target, rule);
        try { window.ShowDialog(); }
        finally
        {
            foreach (var ancestor in owners) ancestor.Closing -= ownerClosing;
            vm.CloseRequested -= window.Close; vm.Deactivate();
            if (focus is UIElement { IsVisible: true, IsEnabled: true } element) element.Focus();
        }
    }
}
