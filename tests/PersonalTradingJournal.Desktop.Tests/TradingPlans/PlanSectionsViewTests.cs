using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Views.TradingPlans;

namespace PersonalTradingJournal.Desktop.Tests.TradingPlans;

public sealed class PlanSectionsViewTests
{
    private const string Host = "PTJ_PLAN_SECTIONS_HOST";
    private static readonly Lazy<Task> Suite = new(() => IsolatedTestProcess.RunSuiteAsync(typeof(PlanSectionsViewTests), "plan-sections", Host,
        TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30)));
    [Theory]
    [InlineData("Light", 980, 96)] [InlineData("Dark", 980, 96)]
    [InlineData("Light", 460, 240)] [InlineData("Dark", 460, 240)]
    public async Task GlobalRemindersAndCollapsibleSectionsPreserveEditingAndExpandForValidation(string theme, int width, int dpi)
    {
        if (Environment.GetEnvironmentVariable(Host) != "1") { await Suite.Value; return; }
        await using var f = await TradingPlanWorkspaceTests.Fixture.CreateAsync(seed: true);
        await CalendarStaTest.RunAsync(() =>
        {
            var app = System.Windows.Application.Current ?? new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
            using var vm = f.NewWorkspace(); Pump(vm.ActivateAsync());
            var page = new TradingPlanWorkspaceView { DataContext = vm, Width = width, Height = 950, Background = (Brush)app.Resources["PtjBackgroundBrush"] };
            var window = new Window { Content = page, SizeToContent = SizeToContent.WidthAndHeight }; window.Show();
            try
            {
                void Layout() { page.Measure(new(width, 950)); page.Arrange(new(0, 0, width, 950)); page.UpdateLayout(); Idle(); }
                Layout(); Assert.Null(vm.Editor); Assert.True(vm.Principles.CanWrite); Render(page, theme, width, dpi, "empty");
                var principles = (PlanSectionExpander)page.FindName("PrinciplesExpander");
                var peer = new ExpanderAutomationPeer(principles); var pattern = (IExpandCollapseProvider)peer.GetPattern(PatternInterface.ExpandCollapse)!;
                Assert.Equal(ExpandCollapseState.Expanded, pattern.ExpandCollapseState);
                var input = (TextBox)page.FindName("PrincipleInput"); Assert.NotEmpty(AutomationProperties.GetName(input));
                pattern.Collapse(); vm.Principles.Input = " "; Pump(vm.Principles.AddCommand.ExecuteAsync(null)); Layout();
                Assert.True(principles.IsExpanded); Assert.Same(input, FocusManager.GetFocusedElement(page)); Assert.True(vm.Principles.HasError);
                input.Text = "Не търгувай ако си уморен"; Assert.False(vm.Principles.HasError);
                Assert.Same(vm.Principles.AddCommand, Assert.Single(input.InputBindings.OfType<KeyBinding>()).Command);
                Pump(vm.Principles.AddCommand.ExecuteAsync(null));
                vm.Principles.Input = "Следвай стратегия, не емоции"; Pump(vm.Principles.AddCommand.ExecuteAsync(null));
                vm.Principles.Input = string.Join(" · ", Enumerable.Repeat("По-добре 2 качествени сделки отколкото 20 хаотични", 5)); Pump(vm.Principles.AddCommand.ExecuteAsync(null));
                Layout(); double expanded = principles.ActualHeight; Render(page, theme, width, dpi, "populated-no-plan");
                vm.Principles.Input = "Unsubmitted reminder"; pattern.Collapse(); Layout();
                Assert.False(vm.Principles.IsExpanded); Assert.True(principles.ActualHeight < expanded);
                Assert.Equal(ExpandCollapseState.Collapsed, pattern.ExpandCollapseState); Render(page, theme, width, dpi, "collapsed");
                var header = (ToggleButton)principles.Template.FindName("HeaderToggle", principles);
                header.Focus(); header.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(header), 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent });
                Layout(); Assert.True(vm.Principles.IsExpanded);
                Pump(vm.SelectContextAsync(f.Source.Store.Plan.Key.NewYorkDate, vm.Accounts.Single(a => a.Id == f.Source.Account.Id)));
                var editor = vm.Editor!; var token = editor.Token;
                editor.RiskPerTrade.Text = "invalid raw amount"; editor.Focus.Text = "unsaved focus";
                var preparation = (SessionPreparationSection)page.FindName("Preparation");
                var scenarios = (ScenariosSection)page.FindName("Scenarios"); var risk = (RiskReadinessSection)page.FindName("Risk");
                var riskField = Descendants(risk).OfType<TextBox>().Single(t => ReferenceEquals(t.DataContext, editor.RiskPerTrade));
                vm.PreparationExpanded = vm.ScenariosExpanded = vm.RiskExpanded = false; Layout();
                Assert.False(preparation.IsSectionExpanded); Assert.False(scenarios.IsSectionExpanded); Assert.False(risk.IsSectionExpanded);
                Assert.Equal(token, editor.Token); Assert.Equal("invalid raw amount", editor.RiskPerTrade.Text);
                Pump(vm.SaveAsync()); Layout(); Assert.True(vm.RiskExpanded); Assert.Same(editor.RiskPerTrade, ((TextBox)FocusManager.GetFocusedElement(risk)).DataContext);
                Assert.Same(riskField, Descendants(risk).OfType<TextBox>().Single(t => ReferenceEquals(t.DataContext, editor.RiskPerTrade)));
                Assert.True(riskField.IsVisible); Assert.Equal(token, editor.Token); Assert.Equal(0, f.Store.Writes);
                editor.Contexts[0].Levels[0].Text = "bad level"; Pump(vm.SaveAsync()); Layout(); Assert.True(vm.PreparationExpanded);
                Assert.Same(editor.Contexts[0].Levels[0], ((TextBox)FocusManager.GetFocusedElement(preparation)).DataContext);
                editor.Contexts[0].Levels[0].Text = "1"; editor.RiskPerTrade.Text = "1";
                editor.Scenarios[0].Condition.Text = new string('x', 4001); Pump(vm.SaveAsync()); Layout(); Assert.True(vm.ScenariosExpanded);
                Assert.Same(editor.Scenarios[0].Condition, ((TextBox)FocusManager.GetFocusedElement(scenarios)).DataContext);
                editor.Scenarios[0].Condition.Text = ""; editor.Scenarios[0].Direction = null; Pump(vm.SaveAsync());
                vm.ScenariosExpanded = false; Pump(vm.MarkReadyAsync()); Layout(); Assert.True(vm.ScenariosExpanded);
                Assert.Equal("Direction", Assert.IsType<Button>(FocusManager.GetFocusedElement(page)).Name);
                editor.Scenarios[0].ToggleDirectionCommand.Execute(null); Pump(vm.SaveAsync()); Pump(vm.MarkReadyAsync()); Layout(); Assert.True(vm.Editor!.IsReadOnly);
                Assert.True(vm.Principles.AddCommand.CanExecute(null)); Assert.Equal("Unsubmitted reminder", vm.Principles.Input);
                var scroll = (ScrollViewer)page.FindName("PageScroll"); scroll.ScrollToTop(); Layout(); Render(page, theme, width, dpi, "ready");
                vm.PreparationExpanded = vm.ScenariosExpanded = vm.RiskExpanded = false; scroll.ScrollToTop(); Layout(); Render(page, theme, width, dpi, "ready-collapsed");
                vm.SelectTab(true); vm.SelectTab(false); Layout(); Assert.False(vm.RiskExpanded);
                Pump(vm.NextDayCommand.ExecuteAsync(null)); Layout(); Assert.Null(vm.Editor); Assert.Single(vm.Principles.Rows, x => x.Number == 3);
                Assert.Equal("Unsubmitted reminder", vm.Principles.Input); Assert.False(vm.RiskExpanded);
                Assert.All(Descendants(page).OfType<Button>().Where(b => Equals(b.Content, "×")), b =>
                { Assert.True(b.IsEnabled); Assert.NotEmpty(AutomationProperties.GetName(b)); Assert.True(b.ActualWidth >= 25); Assert.True(b.TranslatePoint(new(b.ActualWidth, 0), page).X <= page.ActualWidth); });
            }
            finally { window.Close(); }
        }, shutdownDispatcher: false);
    }
    private static void Idle() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static void Pump(Task task)
    {
        var frame = new DispatcherFrame(); var dispatcher = Dispatcher.CurrentDispatcher;
        _ = task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
        if (!task.IsCompleted) Dispatcher.PushFrame(frame); task.GetAwaiter().GetResult(); Idle();
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
        var bitmap = new RenderTargetBitmap(width * dpi / 96, 950 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32); bitmap.Render(page);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(directory, $"principles-{theme}-{width}-{dpi}-{state}.png")); encoder.Save(file);
    }
}
