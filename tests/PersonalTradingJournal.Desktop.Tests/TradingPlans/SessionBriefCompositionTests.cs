using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.Navigation;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.TradingPlans;
using PersonalTradingJournal.Desktop.Views.TradingPlans;

namespace PersonalTradingJournal.Desktop.Tests.TradingPlans;

public sealed class SessionBriefCompositionTests
{
    private const string Host = "PTJ_BRIEF_COMPOSITION_HOST";
    private static readonly Lazy<Task> Native = new(() => IsolatedTestProcess.RunSuiteAsync(
        typeof(SessionBriefCompositionTests), "brief-composition", Host, TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30)));

    [Theory]
    [InlineData(1586, 1000, 96)]
    [InlineData(1000, 850, 240)]
    public async Task ProductionShellDraftReadyAndInvalidComposition(int width, int height, int dpi)
    {
        if (Environment.GetEnvironmentVariable(Host) != "1") { await Native.Value; return; }
        await using var fixture = await TradingPlanWorkspaceTests.Fixture.CreateAsync(seed: true);
        await CalendarStaTest.RunAsync(() =>
        {
            var app = System.Windows.Application.Current ?? new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (string theme in new[] { "Light", "Dark" })
            {
                app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
                app.Resources.Add(new DataTemplateKey(typeof(TradingPlanWorkspaceViewModel)), new DataTemplate(typeof(TradingPlanWorkspaceViewModel))
                { VisualTree = new FrameworkElementFactory(typeof(TradingPlanWorkspaceView)) });
                using var vm = fixture.NewWorkspace(); Pump(vm.ActivateAsync());
                Pump(vm.SelectContextAsync(fixture.Source.Store.Plan.Key.NewYorkDate, vm.Accounts[0]));
                using var shell = MainWindowViewModelTests.CreatePlanShell(vm);
                shell.NavigateCommand.Execute(PersonalTradingJournal.Desktop.Navigation.NavigationDestination.TradingPlan);
                var window = new MainWindow(shell) { Width = width, Height = height };
                // Use the actual shell's content tree and resources, without monitor work-area
                // clamping. A native HWND cannot represent a 1000-DIP-tall viewport on this monitor.
                var root = (FrameworkElement)window.Content;
                window.Content = null;
                root.DataContext = shell;
                root.Resources.MergedDictionaries.Add(window.Resources);
                root.SetValue(System.Windows.Documents.TextElement.FontFamilyProperty, window.FontFamily);
                root.SetValue(System.Windows.Documents.TextElement.ForegroundProperty, window.Foreground);
                TextOptions.SetTextFormattingMode(root, TextFormattingMode.Display);
                root.UseLayoutRounding = true;
                try
                {
                    VisualTreeHelper.SetRootDpi(root, new DpiScale(dpi / 96d, dpi / 96d));
                    void Layout()
                    {
                        Idle(); root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout();
                        Idle(); root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout();
                    }
                    Layout(); var page = Descendants(root).OfType<TradingPlanWorkspaceView>().Single();
                    var editor = vm.Editor!;
                    if (editor.IsReadOnly) { Pump(vm.StartDraftAsync()); editor = vm.Editor!; }
                    editor.Focus.Text = "Be patient. Trade only a confirmed setup.";
                    editor.Scenarios[0].Condition.Text = "Price sweeps a recorded level and confirms a shift.";
                    while (editor.Scenarios.Count > 1) editor.Scenarios.Last().RemoveCommand.Execute(null);
                    editor.AddScenarioCommand.Execute(null);
                    var second = editor.Scenarios.Last(); second.Name.Text = "Alternative scenario";
                    second.Condition.Text = "Opposite context confirms"; second.Response.Text = "Wait for confirmation"; second.Invalidation.Text = "Confirmation fails";
                    second.Direction = PersonalTradingJournal.Domain.Trades.TradeDirection.Short;
                    Layout(); Capture("draft");
                    Assert.Same(editor, ((SessionPreparationSection)Find(page, "Preparation")).DataContext);
                    Assert.Equal(0, ((ScrollViewer)Find(page, "PageScroll")).ScrollableWidth);
                    var cards = (ItemsControl)Find(page, "ScenarioCards");
                    var firstCard = (FrameworkElement)cards.ItemContainerGenerator.ContainerFromIndex(0);
                    var secondCard = (FrameworkElement)cards.ItemContainerGenerator.ContainerFromIndex(1);
                    var relative = secondCard.TranslatePoint(new Point(), firstCard);
                    Assert.True(width > 1200 ? relative.X >= firstCard.ActualWidth - 1 && Math.Abs(relative.Y) < 1 : relative.Y >= firstCard.ActualHeight - 1);
                    var save = (Button)Find(page, "SaveDraft"); var tabs = Find(page, "Tabs");
                    Assert.True(save.IsTabStop); Assert.True(save.IsEnabled);
                    if (width > 1200) Assert.InRange(Math.Abs(save.TranslatePoint(new Point(), tabs).Y), 0, tabs.ActualHeight);
                    var focus = Find(page, "FocusBanner");
                    Assert.InRange(focus.ActualHeight, 40, 85);
                    Assert.All(Descendants(page).OfType<TextBox>().Where(t => t.DataContext is PreparationField && t.ActualWidth > 0), t =>
                        Assert.InRange(t.TranslatePoint(new Point(t.ActualWidth, 0), page).X, 0, page.ActualWidth + 1));
                    var pageScroll = (ScrollViewer)Find(page, "PageScroll");
                    pageScroll.ScrollToEnd(); Layout(); Capture("draft-bottom");
                    editor.MaximumTrades.Text = "invalid"; Pump(vm.SaveAsync()); Layout(); Capture("invalid");
                    Assert.Equal("invalid", editor.MaximumTrades.Text); Assert.True(editor.HasValidationErrors);
                    editor.MaximumTrades.Text = "3"; foreach (var check in editor.Checklist) check.IsChecked = true;
                    Pump(vm.SaveAsync()); Pump(vm.MarkReadyAsync());
                    Assert.True(vm.Editor!.IsReadOnly);
                    ((ScrollViewer)Find(page, "PageScroll")).ScrollToTop(); Layout(); Capture("ready");
                    pageScroll.ScrollToEnd(); Layout(); Capture("ready-bottom");

                    void Capture(string state)
                    {
                        if (Environment.GetEnvironmentVariable("PTJ_BRIEF_RENDER_DIRECTORY") is not { Length: > 0 } directory) return;
                        Directory.CreateDirectory(directory);
                        string name = $"{theme}-{width}-{dpi}-{state}";
                        var bitmap = new RenderTargetBitmap(width * dpi / 96, height * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
                        bitmap.Render(root); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using (var file = File.Create(Path.Combine(directory, name + ".png"))) encoder.Save(file);
                        var prep = (SessionPreparationSection)Find(page, "Preparation");
                        var scenarios = (ScenariosSection)Find(page, "Scenarios"); var risk = (RiskReadinessSection)Find(page, "Risk");
                        File.WriteAllText(Path.Combine(directory, name + ".txt"), $"Page {page.ActualWidth:F1} x {page.ActualHeight:F1} DIP; preparation {prep.ActualHeight:F1}; scenarios {scenarios.ActualHeight:F1}; risk {risk.ActualHeight:F1}; scroll extent {((ScrollViewer)Find(page, "PageScroll")).ExtentHeight:F1}");
                    }
                }
                finally { fixture.Interaction.Choice = PlanLeaveChoice.Discard; window.Close(); Idle(); }
            }
        }, shutdownDispatcher: false);
    }
    private static FrameworkElement Find(DependencyObject root, string name) => Descendants(root).OfType<FrameworkElement>().First(e => e.Name == name && Presented(e));
    private static bool Presented(DependencyObject element)
    {
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is UIElement ui && ui.Visibility != Visibility.Visible) return false;
        return true;
    }
    private static void Idle() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static void Pump(Task task)
    {
        var frame = new DispatcherFrame(); var dispatcher = Dispatcher.CurrentDispatcher;
        _ = task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
        if (!task.IsCompleted) Dispatcher.PushFrame(frame);
        task.GetAwaiter().GetResult(); Idle();
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        { var child = VisualTreeHelper.GetChild(root, i); yield return child; foreach (var next in Descendants(child)) yield return next; }
    }
}
