using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.Navigation;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Validation;
using PersonalTradingJournal.Desktop.ViewModels.TradingPlans;
using PersonalTradingJournal.Desktop.Views.TradingPlans;
using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Desktop.Tests.TradingPlans;

public sealed class TradingPlanWorkspaceViewTests
{
    private const string Host = "PTJ_PLAN_WORKSPACE_VIEW_HOST";
    private static readonly Lazy<Task> NativeSuite = new(() => IsolatedTestProcess.RunSuiteAsync(
        typeof(TradingPlanWorkspaceViewTests), "plan-workspace", Host, TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30)));
    [Theory]
    [InlineData(980, 96)] [InlineData(1280, 144)] [InlineData(1600, 192)] [InlineData(460, 240)]
    public async Task FullPageBindingsLifecycleGuardsFocusAndLightDarkResponsiveLayouts(int width, int dpi)
    {
        if (Environment.GetEnvironmentVariable(Host) != "1")
        {
            await NativeSuite.Value; return;
        }
        await using var fixture = await TradingPlanWorkspaceTests.Fixture.CreateAsync(seed: true);
        await CalendarStaTest.RunAsync(() =>
        {
            var app = System.Windows.Application.Current ?? new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            // Theme dictionaries are created once on their owning STA, not per rendered state.
            var themes = new[] { "Light", "Dark" }.ToDictionary(t => t, CalendarViewLayoutTests.SharedThemeResources);
            foreach (string theme in themes.Keys)
            {
                app.Resources = themes[theme];
                using var vm = fixture.NewWorkspace(); Pump(vm.ActivateAsync());
                var page = new TradingPlanWorkspaceView { DataContext = vm };
                VisualTreeHelper.SetRootDpi(page, new DpiScale(dpi / 96d, dpi / 96d));
                void Layout() { Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); page.Measure(new Size(width, 950)); page.Arrange(new Rect(0, 0, width, 950)); page.UpdateLayout(); }
                Layout(); Assert.Contains(Descendants(page).OfType<TextBlock>(), t => t.Text.Contains("Select an exact Account", StringComparison.Ordinal));
                var startDraft = (Button)page.FindName("StartDraft"); Assert.False(IsPresented(startDraft));
                Render(page, theme, width, dpi, "empty");
                var readEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var readRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                fixture.Store.Read = async (key, ct) => { readEntered.SetResult(); await readRelease.Task.WaitAsync(ct); return await fixture.Store.Inner.LoadAsync(key, ct); };
                var reading = vm.SelectContextAsync(fixture.Source.Store.Plan.Key.NewYorkDate, vm.Accounts[0]); Pump(readEntered.Task); Layout();
                Assert.True(vm.IsLoading); Assert.False(vm.SaveCommand.CanExecute(null)); Assert.False(IsPresented(startDraft)); Render(page, theme, width, dpi, "loading");
                readRelease.SetResult(); Pump(reading); fixture.Store.Read = null; Layout();
                Assert.Same(vm.Editor, ((SessionPreparationSection)page.FindName("Preparation")).DataContext);
                Assert.Same(vm.Editor, ((ScenariosSection)page.FindName("Scenarios")).DataContext);
                Assert.Same(vm.Editor, ((RiskReadinessSection)page.FindName("Risk")).DataContext);
                var editor = vm.Editor!;
                var save = (Button)page.FindName("SaveDraft"); Assert.True(save.IsEnabled); Assert.True(save.IsTabStop);
                Assert.NotEmpty(new System.Windows.Automation.Peers.ButtonAutomationPeer(save).GetName());
                var toolbar = (WrapPanel)page.FindName("Toolbar");
                foreach (var action in Descendants(toolbar).OfType<Button>().Where(b => IsPresented(b) && b.ActualWidth > 0))
                    Assert.InRange(action.TranslatePoint(new Point(action.ActualWidth, 0), page).X, 0, width + 1);
                Assert.Equal(0, ((ScrollViewer)page.FindName("PageScroll")).ScrollableWidth);
                Assert.Single(Descendants(page).OfType<TextBlock>(), t => IsPresented(t) && t.ActualHeight > 0 && t.Text == "01 Session Preparation");
                editor.Focus.Text = "Synthetic focus: wait for observable confirmation, not a prediction.";
                editor.Scenarios[0].Condition.Text = string.Join('\n', Enumerable.Repeat("Synthetic IF context remains explicit and wraps without horizontal clipping.", 4));
                while (editor.Scenarios.Count > 1) editor.Scenarios.Last().RemoveCommand.Execute(null);
                editor.AddScenarioCommand.Execute(null); var second = editor.Scenarios.Last(); second.Name.Text = "Alternative scenario";
                second.Condition.Text = "Alternative condition"; second.Response.Text = "Stand aside until confirmed"; second.Invalidation.Text = "Condition fails";
                second.Direction = PersonalTradingJournal.Domain.Trades.TradeDirection.Short;
                Layout(); Render(page, theme, width, dpi, "draft");
                var scroll = (ScrollViewer)page.FindName("PageScroll");
                ((ScenariosSection)page.FindName("Scenarios")).BringIntoView(); Layout(); Render(page, theme, width, dpi, "scenarios");
                editor.MaximumTrades.Text = "3.5"; Pump(vm.SaveAsync()); scroll.ScrollToEnd(); Layout();
                var invalid = Descendants(page).OfType<TextBox>().Single(t => ReferenceEquals(t.DataContext, editor.MaximumTrades));
                Assert.True(FormValidation.GetIsInvalid(invalid)); Assert.NotEmpty(AutomationProperties.GetHelpText(invalid));
                Render(page, theme, width, dpi, "invalid");
                editor.MaximumTrades.Text = "3"; foreach (var check in editor.Checklist) check.IsChecked = true;
                Pump(vm.SaveAsync()); Pump(vm.MarkReadyAsync()); Layout();
                Assert.True(vm.Editor!.IsReadOnly); Assert.False(save.IsEnabled); Assert.True(IsPresented(startDraft)); Assert.True(startDraft.IsEnabled);
                Assert.All(Descendants(page).OfType<TextBox>().Where(t => t.DataContext is PreparationField), t => Assert.True(t.IsReadOnly));
                Assert.All(Descendants(page).OfType<CheckBox>().Where(b => b.DataContext is PlanChecklistItem), b => Assert.False(b.IsEnabled));
                scroll.ScrollToTop(); Layout(); Render(page, theme, width, dpi, "ready");
                Pump(vm.StartDraftAsync()); Layout(); Assert.False(vm.Editor!.IsReadOnly); Assert.Equal(0, vm.Editor.CompletedConfirmations); Assert.False(IsPresented(startDraft));
                Assert.Contains("preserved", vm.Editor.PreservedReadyLabel); Render(page, theme, width, dpi, "draft-after-ready");
                fixture.Store.Read = (_, _) => throw new IOException("Synthetic read failure, not for UI");
                Pump(vm.SelectContextAsync(vm.SelectedDate.AddDays(1), vm.SelectedAccount)); Layout();
                Assert.False(vm.CanCreateDraft); Assert.Contains("could not be loaded", vm.Message); Assert.False(IsPresented(startDraft)); Render(page, theme, width, dpi, "error");
                fixture.Store.Read = null;
                // Each next rendering host reads this persisted Draft, not stale in-memory source state.
            }
            if (width != 980) return; // Real owner/focus scenario runs once; all four sizes render both themes.
            app.Resources = themes["Light"];
            using var focus = fixture.NewWorkspace(); Pump(focus.ActivateAsync());
            Pump(focus.SelectContextAsync(fixture.Source.Store.Plan.Key.NewYorkDate, focus.Accounts[0]));
            var realPage = new TradingPlanWorkspaceView { DataContext = focus };
            var window = new Window { Content = realPage, Width = 670, Height = 800 }; window.Show();
            try
            {
                var editor = focus.Editor!; editor.RiskPerTrade.Text = "bad"; Pump(focus.SaveAsync()); Idle();
                Assert.Same(editor.RiskPerTrade, Assert.IsType<TextBox>(Keyboard.FocusedElement).DataContext);
                Assert.True(((ScrollViewer)realPage.FindName("PageScroll")).VerticalOffset > 0);
                editor.Scenarios[0].Condition.Text = new string('x', 4001); Pump(focus.SaveAsync()); Idle();
                Assert.Same(editor.Scenarios[0].Condition, Assert.IsType<TextBox>(Keyboard.FocusedElement).DataContext);
                editor.Contexts[0].Levels[0].Text = "bad"; Pump(focus.SaveAsync()); Idle();
                Assert.Same(editor.Contexts[0].Levels[0], Assert.IsType<TextBox>(Keyboard.FocusedElement).DataContext);
                focus.DateText = "broken typed date"; Pump(focus.OpenDateCommand.ExecuteAsync(null)); Idle();
                Assert.Equal("broken typed date", Assert.IsType<TextBox>(Keyboard.FocusedElement).Text);
                window.Focus(); Idle(); Assert.Equal("broken typed date", focus.DateText);
            }
            finally { window.Close(); }

            // Actual MainWindow Closing, including a later subscriber veto. Discard consent must not
            // destroy the retained session until the owner close's final result is accepted.
            using var closing = fixture.NewWorkspace(); Pump(closing.ActivateAsync());
            Pump(closing.SelectContextAsync(fixture.Source.Store.Plan.Key.NewYorkDate, closing.Accounts[0]));
            using var shell = MainWindowViewModelTests.CreatePlanShell(closing);
            shell.NavigateCommand.Execute(PersonalTradingJournal.Desktop.Navigation.NavigationDestination.TradingPlan);
            app.Resources.Add(new DataTemplateKey(typeof(TradingPlanWorkspaceViewModel)), new DataTemplate(typeof(TradingPlanWorkspaceViewModel))
            { VisualTree = new FrameworkElementFactory(typeof(TradingPlanWorkspaceView)) });
            var owner = new MainWindow(shell); owner.Show();
            try
            {
                Idle(); Assert.Equal("Trading Plan", ((TextBlock)owner.FindName("PageTitleText")).Text);
                Assert.Single(Descendants(owner).OfType<TradingPlanWorkspaceView>());
                Render(owner, "Light", 1280, 96, "shared-shell");
                var retained = closing.Editor!; retained.Focus.Text = "unsaved retained owner text";
                bool veto = true; CancelEventHandler later = (_, e) => e.Cancel = veto;
                owner.Closing += later; fixture.Interaction.Choice = PlanLeaveChoice.Discard;
                int prompts = fixture.Interaction.Prompts; owner.Close(); Idle();
                Assert.True(owner.IsVisible); Assert.Same(retained, closing.Editor); Assert.Equal("unsaved retained owner text", retained.Focus.Text);
                Assert.Equal(prompts + 1, fixture.Interaction.Prompts);
                veto = false; fixture.Interaction.Choice = PlanLeaveChoice.Save; owner.Close();
                // Await the explicit asynchronous Save and subsequent dispatcher close, not elapsed time.
                Pump(retained.OperationCompletion); Idle();
                Assert.False(owner.IsVisible); Assert.False(retained.IsDirty);
            }
            finally { if (owner.IsVisible) { fixture.Interaction.Choice = PlanLeaveChoice.Discard; owner.Close(); Idle(); } }
        }, shutdownDispatcher: false);
    }
    private static void Idle() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static bool IsPresented(DependencyObject element)
    {
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is UIElement ui && ui.Visibility != Visibility.Visible) return false;
        return true;
    }
    private static void Pump(Task task)
    {
        var frame = new DispatcherFrame();
        var dispatcher = Dispatcher.CurrentDispatcher;
        _ = task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
        if (!task.IsCompleted) Dispatcher.PushFrame(frame);
        task.GetAwaiter().GetResult(); Idle();
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        { var child = VisualTreeHelper.GetChild(root, i); yield return child; foreach (var next in Descendants(child)) yield return next; }
    }
    private static void Render(FrameworkElement page, string theme, int width, int dpi, string state)
    {
        if (Environment.GetEnvironmentVariable("PTJ_PLAN_RENDER_DIRECTORY") is not { Length: > 0 } directory) return;
        Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap(width * dpi / 96, 950 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(page); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(directory, $"workspace-{theme}-{width}-{dpi}-{state}.png")); encoder.Save(file);
    }
}
