using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Desktop.Screenshots;
using PersonalTradingJournal.Domain.TradingPlans;
using PersonalTradingJournal.Domain.Setups;
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
    [InlineData(1586, 1000, 96, "Light")]
    [InlineData(1586, 1000, 96, "Dark")]
    [InlineData(1000, 850, 240, "Light")]
    [InlineData(1000, 850, 240, "Dark")]
    public async Task ProductionShellDraftReadyAndInvalidComposition(int width, int height, int dpi, string theme)
    {
        if (Environment.GetEnvironmentVariable(Host) != "1") { await Native.Value; return; }
        string scenario = $"SessionBrief/{theme}/{width}/{height}/{dpi}";
        var trace = new UiCaseTrace(scenario);
        trace.Write("child case", "entry");
        var fixture = await trace.Await(() => TradingPlanWorkspaceTests.Fixture.CreateAsync(seed: true));
        try
        {
            var original = fixture.Source.Content;
            var shared = new TradingPlanContent(original.Session, original.Focus, instruments: original.Instruments,
                scenarios: original.Scenarios, riskLimits: original.RiskLimits, planCurrency: "USD");
            Assert.Equal(TradingPlanWriteStatus.Success, (await trace.Await(() => fixture.Store.Inner.WriteAsync(new(TradingPlanWriteOperation.CreateDraft,
                TradingPlanKey.AllAccounts(fixture.Vm.SelectedDate), shared)))).Status);
            trace.Write("STA", "scheduling");
            await CalendarStaTest.RunAsync(() =>
            {
                trace.Write("STA", "action entry");
                using var actionPhase = trace.Phase("STA action / application and window lifetime");
                trace.Write("application/resources", "initializing");
                var app = System.Windows.Application.Current ?? new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                // One production theme per case bounds high-DPI image encoding under the
                // unchanged 30-second deadline; all lifecycle/layout assertions run for each.
                {
                    app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
                    app.Resources.Add(new DataTemplateKey(typeof(TradingPlanWorkspaceViewModel)), new DataTemplate(typeof(TradingPlanWorkspaceViewModel))
                    { VisualTree = new FrameworkElementFactory(typeof(TradingPlanWorkspaceView)) });
                    trace.Write("application/resources", "initialized");
                    var images = new SyntheticImages(fixture.Db.Provider.GetRequiredService<IPlaybookImageStorage>());
                    using var vm = fixture.NewWorkspace(images); trace.Pump(() => vm.ActivateAsync());
                    Assert.Null(vm.SelectedAccount!.Id);
                    trace.Pump(() => vm.SelectContextAsync(fixture.Vm.SelectedDate, vm.Accounts.Single(a => a.Id is null)));
                    using var shell = MainWindowViewModelTests.CreatePlanShell(vm);
                    shell.NavigateCommand.Execute(PersonalTradingJournal.Desktop.Navigation.NavigationDestination.TradingPlan);
                    trace.Write("window", "initializing detached shell content");
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
                    trace.Write("window", "initialized; content intentionally offscreen");
                    try
                    {
                        VisualTreeHelper.SetRootDpi(root, new DpiScale(dpi / 96d, dpi / 96d));
                        void Layout([CallerLineNumber] int line = 0)
                        {
                            using var phase = trace.Phase("layout / ApplicationIdle", line);
                            Idle(); root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout();
                            Idle(); root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout();
                        }
                        Layout(); var page = Descendants(root).OfType<TradingPlanWorkspaceView>().Single();
                        var editor = vm.Editor!;
                        if (editor.IsReadOnly) { trace.Pump(() => vm.StartDraftAsync()); editor = vm.Editor!; }
                        editor.Focus.Text = "Be patient. Trade only a confirmed setup.";
                        editor.Scenarios[0].Condition.Text = "Price sweeps a recorded level and confirms a shift.";
                        while (editor.Scenarios.Count > 1) editor.Scenarios.Last().RemoveCommand.Execute(null);
                        editor.AddScenarioCommand.Execute(null);
                        var second = editor.Scenarios.Last(); second.Name.Text = "Alternative scenario";
                        second.Condition.Text = "Opposite context confirms"; second.Response.Text = "Wait for confirmation"; second.Invalidation.Text = "Confirmation fails";
                        second.Direction = PersonalTradingJournal.Domain.Trades.TradeDirection.Short;
                        Layout(); Capture("draft");
                        foreach (var item in editor.VisibleScreenshots.ToArray()) item.RemoveCommand.Execute(null);
                        Layout(); Capture("screenshots-empty");
                        var timeframe = (ComboBox)Find(page, "ScreenshotTimeframe");
                        var upload = Descendants(Find(page, "Preparation")).OfType<Button>().Single(b => b.Content as string == "Upload Screenshot");
                        foreach (var choice in SessionPreparationViewModel.Timeframes)
                        {
                            editor.SelectedTimeframe = choice; Layout();
                            var presenter = Descendants(timeframe).OfType<ContentPresenter>().Single(p => p.TemplatedParent == timeframe);
                            var text = Descendants(presenter).OfType<TextBlock>().Single();
                            var measured = new FormattedText(choice.Label, System.Globalization.CultureInfo.CurrentCulture,
                                FlowDirection.LeftToRight, new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch),
                                text.FontSize, Brushes.Black, dpi / 96d);
                            Assert.Equal(choice.Label, text.Text);
                            Assert.True(presenter.ActualWidth >= measured.WidthIncludingTrailingWhitespace);
                            Assert.True(presenter.TranslatePoint(new Point(presenter.ActualWidth, 0), timeframe).X <= timeframe.ActualWidth - 32);
                            Assert.InRange(Math.Abs(upload.TranslatePoint(new Point(0, upload.ActualHeight / 2), timeframe).Y - timeframe.ActualHeight / 2), 0, 1);
                            Assert.True(upload.TranslatePoint(new Point(), timeframe).X >= timeframe.ActualWidth + 8);
                        }
                        editor.SelectedTimeframe = SessionPreparationViewModel.Timeframes.Single(t => t.Label == "15m");
                        Layout(); Capture("timeframe-15m");
                        var strip = Find(page, "StatusStrip"); var toolbar = Find(page, "Toolbar");
                        Assert.Equal("Draft", ((ContentControl)Find(page, "LifecycleBadge")).Content);
                        Assert.InRange(Math.Abs(strip.TranslatePoint(new Point(), toolbar).X), 0, 1);
                        Assert.True(strip.TranslatePoint(new Point(0, strip.ActualHeight), toolbar).Y <= 1);
                        var picker = (Expander)Find(page, "InstrumentPicker"); picker.IsExpanded = true;
                        Layout(); Capture("instrument-actions"); picker.IsExpanded = false;
                        trace.Pump(() => editor.UploadScreenshotCommand.ExecuteAsync(null));
                        editor.SelectedTimeframe = SessionPreparationViewModel.Timeframes.Single(t => t.Label == "4h");
                        trace.Pump(() => editor.UploadScreenshotCommand.ExecuteAsync(null));
                        Assert.Equal(2, editor.VisibleScreenshots.Count);
                        editor.Scenarios[0].Name.Text = editor.Scenarios[0].Condition.Text = editor.Scenarios[0].Response.Text = editor.Scenarios[0].Invalidation.Text = "";
                        Layout(); Capture("screenshots-and-optional");
                        Assert.Same(editor, ((SessionPreparationSection)Find(page, "Preparation")).DataContext);
                        Assert.Equal(0, ((ScrollViewer)Find(page, "PageScroll")).ScrollableWidth);
                        var cards = (ItemsControl)Find(page, "ScenarioCards");
                        var firstCard = (FrameworkElement)cards.ItemContainerGenerator.ContainerFromIndex(0);
                        var secondCard = (FrameworkElement)cards.ItemContainerGenerator.ContainerFromIndex(1);
                        var relative = secondCard.TranslatePoint(new Point(), firstCard);
                        Assert.True(width > 1200 ? relative.X >= firstCard.ActualWidth - 1 && Math.Abs(relative.Y) < 1 : relative.Y >= firstCard.ActualHeight - 1);
                        var role = Descendants(secondCard).OfType<Button>().Single(b => b.Name == "Role");
                        var direction = Descendants(secondCard).OfType<Button>().Single(b => b.Name == "Direction");
                        Assert.True(role.IsTabStop); Assert.True(direction.IsTabStop);
                        Assert.Contains("Switch", System.Windows.Automation.AutomationProperties.GetName(role));
                        direction.Command.Execute(null); Assert.Equal(PersonalTradingJournal.Domain.Trades.TradeDirection.Long, second.Direction);
                        Assert.Equal(TradingPlanScenarioRole.Alternative, second.Role);
                        role.Command.Execute(null); Assert.Equal(TradingPlanScenarioRole.Primary, second.Role);
                        Assert.Equal(PersonalTradingJournal.Domain.Trades.TradeDirection.Long, second.Direction);
                        direction.Command.Execute(null); Layout();
                        secondCard.BringIntoView(); Layout(); Capture("primary-short");
                        ((ScrollViewer)Find(page, "PageScroll")).ScrollToTop(); Layout();
                        role.Command.Execute(null); Layout();
                        Assert.Contains(Descendants(secondCard).OfType<Button>(), b => b.Content as string == "Delete" && b.IsEnabled);
                        var save = (Button)Find(page, "SaveDraft"); var tabs = Find(page, "Tabs");
                        Assert.True(save.IsTabStop); Assert.True(save.IsEnabled);
                        if (width > 1200) Assert.InRange(Math.Abs(save.TranslatePoint(new Point(), tabs).Y), 0, tabs.ActualHeight);
                        var focus = Find(page, "FocusBanner");
                        Assert.InRange(focus.ActualHeight, 40, 85);
                        Assert.All(Descendants(page).OfType<TextBox>().Where(t => t.DataContext is PreparationField && t.ActualWidth > 0), t =>
                            Assert.InRange(t.TranslatePoint(new Point(t.ActualWidth, 0), page).X, 0, page.ActualWidth + 1));
                        var pageScroll = (ScrollViewer)Find(page, "PageScroll");
                        pageScroll.ScrollToEnd(); Layout(); Capture("draft-bottom");
                        editor.MaximumTrades.Text = "invalid"; trace.Pump(() => vm.SaveAsync()); Layout(); Capture("invalid");
                        Assert.Equal("invalid", editor.MaximumTrades.Text); Assert.True(editor.HasValidationErrors);
                        Assert.Equal(PlanNoticeKind.Warning, vm.Notice.Kind);
                        editor.MaximumTrades.Text = "3"; foreach (var check in editor.Checklist) check.IsChecked = true;
                        pageScroll.ScrollToTop();
                        fixture.Store.Status = TradingPlanWriteStatus.StorageFailure;
                        trace.Pump(() => vm.SaveAsync()); Layout(); Capture("save-error");
                        Assert.Equal(PlanNoticeKind.Error, vm.Notice.Kind); Assert.True(editor.IsDirty);
                        fixture.Store.Status = null;
                        trace.Pump(() => vm.SaveAsync()); Layout(); Capture("save-success");
                        Assert.Equal(PlanNoticeKind.Success, vm.Notice.Kind);
                        var notice = (ContentControl)Find(page, "Status");
                        Assert.Equal(vm.Notice, notice.Content);
                        var announcement = Descendants(notice).OfType<TextBlock>().Single(t => t.Name == "NoticeText");
                        Assert.Equal(vm.Notice.AccessibleText, System.Windows.Automation.AutomationProperties.GetName(announcement));
                        Assert.InRange(announcement.ActualWidth, 1, page.ActualWidth);
                        trace.Pump(() => vm.MarkReadyAsync());
                        Assert.True(vm.Editor!.IsReadOnly);
                        Layout(); Assert.Equal("Ready", ((ContentControl)Find(page, "LifecycleBadge")).Content);
                        Assert.All(vm.Editor.VisibleScreenshots, s => Assert.NotNull(s.Thumbnail));
                        Assert.False(vm.Editor.Scenarios[0].ToggleRoleCommand.CanExecute(null));
                        Assert.False(vm.Editor.VisibleScreenshots[0].RemoveCommand.CanExecute(null));
                        ((ScrollViewer)Find(page, "PageScroll")).ScrollToTop(); Layout(); Capture("ready");
                        Assert.False(save.IsEnabled);
                        // Same compiled template: read-only information and empty-state collapse, without a second notification service.
                        var statusBinding = System.Windows.Data.BindingOperations.GetBindingBase(notice, ContentControl.ContentProperty)!;
                        notice.Content = new PlanNotice("Read-only saved preparation. Start Draft / Edit to change working content.");
                        Layout(); Capture("readonly-info");
                        notice.Content = new PlanNotice(""); Layout();
                        Assert.Equal(0, notice.ActualHeight);
                        notice.SetBinding(ContentControl.ContentProperty, statusBinding); Layout();
                        pageScroll.ScrollToEnd(); Layout(); Capture("ready-bottom");
                        trace.Pump(() => vm.StartDraftAsync()); Layout();
                        Assert.Equal("Working Draft after Ready", ((ContentControl)Find(page, "LifecycleBadge")).Content);
                        pageScroll.ScrollToTop(); Layout(); Capture("working-after-ready");
                        // A saved working Draft can navigate to an empty day without an unsaved prompt.
                        trace.Pump(() => vm.NextDayCommand.ExecuteAsync(null)); Layout(); Capture("no-plan");
                        Assert.Null(vm.Editor); Assert.False(vm.Notice.HasText); Assert.Equal(0, notice.ActualHeight);
                        Assert.Equal(Visibility.Collapsed, strip.Visibility); Assert.True(vm.CanCreateDraft);

                        void Capture(string state)
                        {
                            if (Environment.GetEnvironmentVariable("PTJ_BRIEF_RENDER_DIRECTORY") is not { Length: > 0 } directory) return;
                            Directory.CreateDirectory(directory);
                            string name = $"{theme}-{width}-{dpi}-{state}";
                            var bitmap = new RenderTargetBitmap(width * dpi / 96, height * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
                            bitmap.Render(root); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                            using (var file = File.Create(Path.Combine(directory, name + ".png"))) encoder.Save(file);
                            var prep = Descendants(page).OfType<SessionPreparationSection>().Single(p => p.Name == "Preparation");
                            var scenarios = Descendants(page).OfType<ScenariosSection>().Single(p => p.Name == "Scenarios");
                            var risk = Descendants(page).OfType<RiskReadinessSection>().Single(p => p.Name == "Risk");
                            File.WriteAllText(Path.Combine(directory, name + ".txt"), $"Page {page.ActualWidth:F1} x {page.ActualHeight:F1} DIP; preparation {prep.ActualHeight:F1}; scenarios {scenarios.ActualHeight:F1}; risk {risk.ActualHeight:F1}; scroll extent {((ScrollViewer)Find(page, "PageScroll")).ExtentHeight:F1}");
                        }
                    }
                    finally { using var cleanup = trace.Phase("window cleanup / ApplicationIdle"); fixture.Interaction.Choice = PlanLeaveChoice.Discard; window.Close(); Idle(); }
                }
            }, scenario: scenario, shutdownDispatcher: false);
            trace.Write("STA", "action returned; shared child dispatcher remains live until process exit");
        }
        finally
        {
            await trace.Await(() => fixture.DisposeAsync().AsTask(), "fixture disposal");
            trace.Write("child case", "exit; dispatcher shutdown is owned by isolated child process");
        }
    }
    private static FrameworkElement Find(DependencyObject root, string name) => Descendants(root).OfType<FrameworkElement>().First(e => e.Name == name && Presented(e));
    private sealed class SyntheticImages(IPlaybookImageStorage storage) : ITradingPlanImages
    {
        private readonly TradingPlanImages reader = new(storage);
        public Task<PlanImageSelection?> PickAsync(CancellationToken token)
        {
            var source = BitmapSource.Create(64, 32, 96, 96, PixelFormats.Bgr32, null,
                Enumerable.Repeat(unchecked((int)0x00609F94), 64 * 32).ToArray(), 64 * 4);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source));
            using var stream = new MemoryStream(); encoder.Save(stream);
            var bytes = stream.ToArray(); var decoded = PlaybookExampleImages.Decode(bytes, true);
            return Task.FromResult<PlanImageSelection?>(new("synthetic.png", bytes, decoded));
        }
        public Task<PlaybookExampleImage> StoreAsync(PlanImageSelection s, CancellationToken ct) => storage.StoreAsync(s.FileName, s.Bytes, ct);
        public Task<ImageSource> ThumbnailAsync(PlaybookExampleImage image, CancellationToken ct) => reader.ThumbnailAsync(image, ct);
        public Task ShowAsync(PlaybookExampleImage? image, PlanImageSelection? pending, string label, CancellationToken ct) => throw new NotSupportedException("Native picker/image interaction is not claimed by a render test.");
        public Task CleanupAsync(string key) => storage.DeleteUnreferencedAsync(key);
    }
    private static bool Presented(DependencyObject element)
    {
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is UIElement ui && ui.Visibility != Visibility.Visible) return false;
        return true;
    }
    private static void Idle() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        { var child = VisualTreeHelper.GetChild(root, i); yield return child; foreach (var next in Descendants(child)) yield return next; }
    }
}
