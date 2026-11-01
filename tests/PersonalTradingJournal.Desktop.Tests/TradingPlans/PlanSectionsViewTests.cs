using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Views.TradingPlans;

namespace PersonalTradingJournal.Desktop.Tests.TradingPlans;

[Collection(ExclusiveWpfCollection.Name)]
[Trait(ExclusiveWpfCollection.Trait, ExclusiveWpfCollection.Value)]
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
        string scenario = $"PlanSections/{theme}/{width}/{dpi}";
        var trace = new UiCaseTrace(scenario);
        trace.Write("child case", "entry");
        var f = await trace.Await(() => TradingPlanWorkspaceTests.Fixture.CreateAsync(seed: true));
        try
        {
            trace.Write("STA", "scheduling");
            await CalendarStaTest.RunAsync(() =>
            {
                trace.Write("STA", "action entry");
                using var actionPhase = trace.Phase("STA action / application and window lifetime");
                trace.Write("application/resources", "initializing");
                var app = System.Windows.Application.Current ?? new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
                trace.Write("application/resources", "initialized");
                using var vm = f.NewWorkspace(); trace.Pump(() => vm.ActivateAsync());
                trace.Write("window", "initializing and showing");
                var page = new TradingPlanWorkspaceView { DataContext = vm, Width = width, Height = 950, Background = (Brush)app.Resources["PtjBackgroundBrush"] };
                var window = new Window { Content = page, SizeToContent = SizeToContent.WidthAndHeight }; window.Show();
                trace.Write("window", "shown");
                try
                {
                    void Layout([CallerLineNumber] int line = 0) { using var phase = trace.Phase("layout / ApplicationIdle", line); page.Measure(new(width, 950)); page.Arrange(new(0, 0, width, 950)); page.UpdateLayout(); Idle(); }
                    Layout(); Assert.Null(vm.Editor); Assert.True(vm.Principles.CanWrite); Render(page, theme, width, dpi, "empty");
                    ValidationFocusOwner.Ready(window, page);
                    trace.Write("owner readiness", "active owner established once before validation/input; no recovery activation");
                    var principles = (PlanSectionExpander)page.FindName("PrinciplesExpander");
                    var peer = new ExpanderAutomationPeer(principles); var pattern = (IExpandCollapseProvider)peer.GetPattern(PatternInterface.ExpandCollapse)!;
                    Assert.Equal(ExpandCollapseState.Expanded, pattern.ExpandCollapseState);
                    var input = (TextBox)page.FindName("PrincipleInput"); Assert.NotEmpty(AutomationProperties.GetName(input));
                    var entry = (Grid)page.FindName("PrincipleEntry"); var add = (Button)page.FindName("PrincipleAdd");
                    entry.Width = 280; Layout(); Assert.True(input.ActualWidth >= 250);
                    Assert.True(add.TranslatePoint(new(), entry).Y >= input.TranslatePoint(new(0, input.ActualHeight), entry).Y);
                    entry.Width = double.NaN; Layout();
                    pattern.Collapse(); vm.Principles.Input = " "; trace.Pump(() => vm.Principles.AddCommand.ExecuteAsync(null)); Layout();
                    Assert.True(principles.IsExpanded); Assert.Same(input, FocusManager.GetFocusedElement(page)); Assert.True(vm.Principles.HasError);
                    input.Text = "Не търгувай ако си уморен"; Assert.False(vm.Principles.HasError);
                    Assert.Same(vm.Principles.AddCommand, Assert.Single(input.InputBindings.OfType<KeyBinding>()).Command);
                    trace.Pump(() => vm.Principles.AddCommand.ExecuteAsync(null));
                    Layout(); Render(page, theme, width, dpi, "single");
                    vm.Principles.Input = "Следвай стратегия, не емоции"; trace.Pump(() => vm.Principles.AddCommand.ExecuteAsync(null));
                    vm.Principles.Input = string.Join(" · ", Enumerable.Repeat("По-добре 2 качествени сделки отколкото 20 хаотични", 5)); trace.Pump(() => vm.Principles.AddCommand.ExecuteAsync(null));
                    Layout(); double expanded = principles.ActualHeight; Render(page, theme, width, dpi, "populated-no-plan");
                    vm.Principles.Input = "Unsubmitted reminder"; pattern.Collapse(); Layout();
                    Assert.False(vm.Principles.IsExpanded); Assert.True(principles.ActualHeight < expanded);
                    Assert.Equal(ExpandCollapseState.Collapsed, pattern.ExpandCollapseState); Render(page, theme, width, dpi, "collapsed");
                    var header = (ToggleButton)principles.Template.FindName("HeaderToggle", principles);
                    var icon = Assert.Single(Descendants(header).OfType<PlanFoldIcon>());
                    Assert.Null(principles.Template.FindName("Chevron", principles));
                    icon.Animate = false; icon.SetState(false); Assert.Equal(0, icon.Progress);
                    Assert.Contains("Expand", header.ToolTip.ToString());
                    header.Focus(); header.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(header), 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent });
                    Layout(); Assert.True(vm.Principles.IsExpanded);
                    Assert.Equal(1, icon.Progress); Assert.Contains("Collapse", header.ToolTip.ToString());
                    icon.Animate = true; pattern.Collapse();
                    icon.Clock!.Controller!.SeekAlignedToLastTick(TimeSpan.FromMilliseconds(100), TimeSeekOrigin.BeginTime);
                    double middle = icon.Progress; Assert.InRange(middle, .1, .9);
                    Assert.False(principles.IsExpanded); // Content changes immediately, independently of the icon clock.
                    pattern.Expand(); Assert.InRange(icon.Progress, middle - .01, middle + .01);
                    icon.Clock!.Controller!.SeekAlignedToLastTick(TimeSpan.FromMilliseconds(200), TimeSeekOrigin.BeginTime);
                    Assert.Equal(1, icon.Progress);
                    for (int toggle = 0; toggle < 12; toggle++) { principles.IsExpanded = !principles.IsExpanded; }
                    icon.SetState(false); Assert.Equal(principles.IsExpanded ? 1 : 0, icon.Progress);
                    icon.Animate = false; pattern.Collapse(); Assert.Null(icon.Clock); Assert.Equal(0, icon.Progress);
                    pattern.Expand(); Assert.Equal(1, icon.Progress);
                    Assert.True(PlanFoldIcon.Strip(.5, -1).Bounds.Width < PlanFoldIcon.Strip(0, -1).Bounds.Width);
                    Assert.NotEqual(PlanFoldIcon.Strip(.5, -1).Bounds.Height, PlanFoldIcon.Strip(0, -1).Bounds.Height);
                    RenderFoldFrames(app, theme, dpi);
                    trace.Pump(() => vm.SelectContextAsync(f.Source.Store.Plan.Key.NewYorkDate, vm.Accounts.Single(a => a.Id == f.Source.Account.Id)));
                    var editor = vm.Editor!; var token = editor.Token;
                    editor.RiskPerTrade.Text = "invalid raw amount"; editor.Focus.Text = "unsaved focus";
                    var preparation = (SessionPreparationSection)page.FindName("Preparation");
                    var scenarios = (ScenariosSection)page.FindName("Scenarios"); var risk = (RiskReadinessSection)page.FindName("Risk");
                    var riskField = Descendants(risk).OfType<TextBox>().Single(t => ReferenceEquals(t.DataContext, editor.RiskPerTrade));
                    vm.PreparationExpanded = vm.ScenariosExpanded = vm.RiskExpanded = false; Layout();
                    Assert.False(preparation.IsSectionExpanded); Assert.False(scenarios.IsSectionExpanded); Assert.False(risk.IsSectionExpanded);
                    Assert.Equal(token, editor.Token); Assert.Equal("invalid raw amount", editor.RiskPerTrade.Text);
                    trace.Pump(() => vm.SaveAsync()); Layout(); Assert.True(vm.RiskExpanded); Assert.Same(editor.RiskPerTrade, ((TextBox)FocusManager.GetFocusedElement(risk)).DataContext);
                    Assert.Same(riskField, Descendants(risk).OfType<TextBox>().Single(t => ReferenceEquals(t.DataContext, editor.RiskPerTrade)));
                    Assert.True(riskField.IsVisible); Assert.Equal(token, editor.Token); Assert.Equal(0, f.Store.Writes);
                    editor.Contexts[0].Levels[0].Text = "bad level"; trace.Pump(() => vm.SaveAsync()); Layout(); Assert.True(vm.PreparationExpanded);
                    Assert.Same(editor.Contexts[0].Levels[0], ((TextBox)FocusManager.GetFocusedElement(preparation)).DataContext);
                    editor.Contexts[0].Levels[0].Text = "1"; editor.RiskPerTrade.Text = "1";
                    editor.Scenarios[0].Condition.Text = new string('x', 4001); trace.Pump(() => vm.SaveAsync()); Layout(); Assert.True(vm.ScenariosExpanded);
                    Assert.Same(editor.Scenarios[0].Condition, ((TextBox)FocusManager.GetFocusedElement(scenarios)).DataContext);
                    editor.Scenarios[0].Condition.Text = ""; editor.Scenarios[0].Direction = null; trace.Pump(() => vm.SaveAsync());
                    vm.ScenariosExpanded = false; trace.Pump(() => vm.MarkReadyAsync()); Layout(); Assert.True(vm.ScenariosExpanded);
                    Assert.Equal("Direction", Assert.IsType<Button>(FocusManager.GetFocusedElement(page)).Name);
                    editor.Scenarios[0].ToggleDirectionCommand.Execute(null); trace.Pump(() => vm.SaveAsync()); trace.Pump(() => vm.MarkReadyAsync()); Layout(); Assert.True(vm.Editor!.IsReadOnly);
                    Assert.True(vm.Principles.AddCommand.CanExecute(null)); Assert.Equal("Unsubmitted reminder", vm.Principles.Input);
                    var scroll = (ScrollViewer)page.FindName("PageScroll"); scroll.ScrollToTop(); Layout(); Render(page, theme, width, dpi, "ready");
                    vm.PreparationExpanded = vm.ScenariosExpanded = vm.RiskExpanded = false; scroll.ScrollToTop(); Layout(); Render(page, theme, width, dpi, "ready-collapsed");
                    vm.SelectTab(true); vm.SelectTab(false); Layout(); Assert.False(vm.RiskExpanded);
                    trace.Pump(() => vm.NextDayCommand.ExecuteAsync(null)); Layout(); Assert.Null(vm.Editor); Assert.Single(vm.Principles.Rows, x => x.Number == 3);
                    Assert.Equal("Unsubmitted reminder", vm.Principles.Input); Assert.False(vm.RiskExpanded);
                    Assert.All(Descendants(page).OfType<Button>().Where(b => Equals(b.Content, "×")), b =>
                    { Assert.True(b.IsEnabled); Assert.NotEmpty(AutomationProperties.GetName(b)); Assert.True(b.ActualWidth >= 25); Assert.True(b.TranslatePoint(new(b.ActualWidth, 0), page).X <= page.ActualWidth); });
                    // Remove the middle, last, then only row through the actual button/command path.
                    var rows = (ItemsControl)page.FindName("PrincipleRows");
                    foreach (int index in new[] { 1, 1, 0 })
                    {
                        var buttons = Descendants(rows).OfType<Button>().ToArray(); var remove = buttons[index];
                        FocusManager.SetFocusedElement(page, remove); remove.Focus();
                        using var removal = trace.Phase($"automation remove row {index}: command creation through Invoke");
                        ((IInvokeProvider)new ButtonAutomationPeer(remove).GetPattern(PatternInterface.Invoke)!).Invoke(); Idle();
                        trace.Pump(() => vm.Principles.RemoveCommand.ExecutionTask ?? throw new InvalidOperationException("Automation removal did not start a command task.")); Layout();
                        if (vm.Principles.Rows.Count == 0) Assert.Same(input, FocusManager.GetFocusedElement(page));
                        else Assert.Same(vm.Principles.Rows[Math.Min(index, vm.Principles.Rows.Count - 1)], ((Button)FocusManager.GetFocusedElement(page)).DataContext);
                    }
                    Assert.Equal("Unsubmitted reminder", vm.Principles.Input); Assert.Empty(vm.Principles.Rows);
                }
                finally { using var cleanup = trace.Phase("window cleanup"); window.Close(); }
            }, scenario: scenario, shutdownDispatcher: false);
            trace.Write("STA", "action returned; shared child dispatcher remains live until process exit");
        }
        finally
        {
            await trace.Await(() => f.DisposeAsync().AsTask(), "fixture disposal");
            trace.Write("child case", "exit; dispatcher shutdown is owned by isolated child process");
        }
    }
    private static void Idle() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static void RenderFoldFrames(System.Windows.Application app, string theme, int dpi)
    {
        if (Environment.GetEnvironmentVariable("PTJ_PLAN_RENDER_DIRECTORY") is not { Length: > 0 } directory) return;
        var strip = new StackPanel { Orientation = Orientation.Horizontal, Background = (Brush)app.Resources["PtjSurfaceBrush"] };
        foreach (double progress in new[] { 0, .25, .5, .75, 1 })
        {
            var icon = new PlanFoldIcon { Width = 36, Height = 36, Margin = new Thickness(12), Foreground = (Brush)app.Resources["PtjAccentBrush"] };
            icon.SetValue(PlanFoldIcon.ProgressProperty, progress); strip.Children.Add(icon);
        }
        strip.Measure(new(300, 60)); strip.Arrange(new(0, 0, 300, 60)); strip.UpdateLayout();
        var bitmap = new RenderTargetBitmap(300 * dpi / 96, 60 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32); bitmap.Render(strip);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(directory, $"fold-frames-{theme}-{dpi}.png")); encoder.Save(file);
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        { var child = VisualTreeHelper.GetChild(root, i); yield return child; foreach (var next in Descendants(child)) yield return next; }
    }
    private static void Render(FrameworkElement page, string theme, int width, int dpi, string state)
    {
        if (Environment.GetEnvironmentVariable("PTJ_PLAN_RENDER_DIRECTORY") is not { Length: > 0 } directory) return;
        // Endpoint screenshots are deterministic; intermediate folding frames are captured separately.
        foreach (var icon in Descendants(page).OfType<PlanFoldIcon>()) icon.SetState(false);
        page.UpdateLayout(); // Flush the invalidated vector drawing before RenderTargetBitmap reads it.
        Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap(width * dpi / 96, 950 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32); bitmap.Render(page);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(directory, $"principles-{theme}-{width}-{dpi}-{state}.png")); encoder.Save(file);
    }
}
