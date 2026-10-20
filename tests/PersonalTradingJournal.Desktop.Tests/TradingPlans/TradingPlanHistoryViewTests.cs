using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.TradingPlans;
using PersonalTradingJournal.Desktop.Views.TradingPlans;
using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Desktop.Tests.TradingPlans;

public sealed class TradingPlanHistoryViewTests
{
    private const string Host = "PTJ_PLAN_HISTORY_HOST";
    private static readonly Lazy<Task> Suite = new(() => IsolatedTestProcess.RunSuiteAsync(typeof(TradingPlanHistoryViewTests),
        "plan-history", Host, TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30)));

    // Each complete theme workflow is a reporting boundary. Migration/seeding,
    // both theme workflows and cleanup previously consumed one inactivity budget.
    // Keep the 30s case and 90s child contracts, with no shared mutable fixture.
    [Theory]
    [InlineData(980, 96, "Light")] [InlineData(980, 96, "Dark")]
    [InlineData(460, 240, "Light")] [InlineData(460, 240, "Dark")]
    public async Task CompiledHistoryFrozenViewerTransfersAndRetainedEditorAreAccessible(int width, int dpi, string theme)
    {
        if (Environment.GetEnvironmentVariable(Host) != "1") { await Suite.Value; return; }
        WpfStepDiagnostics.Write($"History case start width={width}; dpi={dpi}; theme={theme}");
        TradingPlanWorkspaceTests.Fixture f;
        using (CalendarStaTest.Timing("History fixture creation"))
            f = await TradingPlanWorkspaceTests.Fixture.CreateAsync(true);
        try
        {
            WpfStepDiagnostics.Write("History fixture created");
            using var seedTiming = CalendarStaTest.Timing("History seed and Ready writes");
            var first = await TradingPlanHistoryTests.Ready(f, "Frozen preparation, exactly as recorded");
            await TradingPlanHistoryTests.Ready(f, "Later preparation"); await f.Vm.StartDraftAsync();
            var store = f.Store.Inner;
            await store.WriteAsync(new(TradingPlanWriteOperation.CopyForward, new(first.Key.AccountId, new(2026, 10, 16)), first.Content, sourceRevision: first.Id));
            await store.WriteAsync(new(TradingPlanWriteOperation.CopyForward, new(first.Key.AccountId, new(2026, 10, 15)), first.Content, sourceRevision: first.Id));
            var other = (await store.LoadAsync(new(first.Key.AccountId, new(2026, 10, 15))))!;
            await store.WriteAsync(new(TradingPlanWriteOperation.SaveDraft, other.Key, first.Content, new(other.Id, other.Version)));
            other = (await store.LoadAsync(other.Key))!;
            await store.WriteAsync(new(TradingPlanWriteOperation.MarkReady, other.Key, other.WorkingDraft!.Content, new(other.Id, other.Version)));
            f.Source.Account.UpdateDetails("Synthetic Account with a deliberately long display name for wrapping and exact identity", f.Source.Account.AccountType,
                null, null, "USD", 1000, DateTimeOffset.UtcNow);
            await f.Db.Provider.GetRequiredService<ITradingAccountStore>().UpdateAsync(f.Source.Account);
            seedTiming.Dispose();
            WpfStepDiagnostics.Write("History seed completed; requesting STA");
            await CalendarStaTest.RunAsync(() =>
            {
                var app = System.Windows.Application.Current ?? new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                {
                    using var themeTiming = CalendarStaTest.Timing($"History workflow {theme} {width}/{dpi}");
                    WpfStepDiagnostics.Write($"History theme start {theme}");
                    app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme); using var vm = f.NewWorkspace(); Pump(vm.ActivateAsync());
                    VerifyCurrencyConfirmation(theme, dpi);
                    Pump(vm.SelectContextAsync(first.Key.NewYorkDate, vm.Accounts.First(a => a.Id.HasValue))); var editor = vm.Editor!;
                    editor.MaximumTrades.Text = "retained invalid count";
                    TradingPlanWorkspaceView page;
                    using (CalendarStaTest.Phase("History view construction"))
                        page = new TradingPlanWorkspaceView { DataContext = vm, Background = (Brush)app.Resources["PtjBackgroundBrush"] };
                    VisualTreeHelper.SetRootDpi(page, new(dpi / 96d, dpi / 96d));
                    void Layout()
                    {
                        using (CalendarStaTest.Phase("History dispatcher idle")) Idle();
                        using (CalendarStaTest.Phase("History measure")) page.Measure(new Size(width, 950));
                        using (CalendarStaTest.Phase("History arrange")) page.Arrange(new Rect(0, 0, width, 950));
                        using (CalendarStaTest.Phase("History update layout")) page.UpdateLayout();
                    }
                    TradingPlanHistoryTests.Filters(vm); vm.HistoryTo = "17 Oct 2026"; Pump(vm.SearchHistoryAsync()); Layout();
                    var history = Descendants(page).OfType<TradingPlanHistoryView>().Single();
                    var scroll = (ScrollViewer)history.FindName("HistoryScroll");
                    Assert.Equal(3, vm.HistoryRows.Count); Assert.Contains(vm.HistoryRows, r => r.Lifecycle == "Draft after Ready");
                    Assert.Contains(vm.HistoryRows, r => r.Lifecycle == "Ready"); Assert.Contains(vm.HistoryRows, r => r.Lifecycle == "Draft");
                    Assert.Same(editor, vm.Editor); Assert.Equal(0, scroll.ScrollableWidth);
                    foreach (var button in Descendants(history).OfType<Button>().Where(b => b.ActualWidth > 0 && Presented(b)))
                    { Assert.True(button.IsTabStop); Assert.NotEmpty(new ButtonAutomationPeer(button).GetName()); Assert.InRange(button.TranslatePoint(new Point(button.ActualWidth, 0), page).X, 0, width + 1); }
                    Render(page, theme, width, dpi, "history");
                    var from = (TextBox)history.FindName("From"); from.Text = "not a date"; Pump(vm.SearchHistoryAsync()); Layout();
                    Assert.Equal("not a date", vm.HistoryFrom); Assert.NotEmpty(AutomationProperties.GetHelpText(from));
                    Assert.Equal(3, vm.HistoryRows.Count); Render(page, theme, width, dpi, "invalid-filter");
                    vm.HistoryFrom = "01 Oct 2026"; vm.HistoryTo = "01 Oct 2026"; Pump(vm.SearchHistoryAsync()); Layout(); Assert.Empty(vm.HistoryRows);
                    Render(page, theme, width, dpi, "empty");
                    vm.HistoryTo = "17 Oct 2026"; Pump(vm.SearchHistoryAsync());
                    Pump(vm.ShowRevisionsAsync(vm.HistoryRows.Single(r => r.State.Key == first.Key)));
                    Pump(vm.ViewRevisionAsync(vm.RevisionRows.Single(r => r.Revision.Id == first.Id))); Layout();
                    var readOnly = (SessionPreparationSection)history.FindName("HistoricalPreparation");
                    Assert.Same(vm.HistoricalViewer, readOnly.DataContext); Assert.True(vm.HistoricalViewer!.IsReadOnly);
                    Assert.All(Descendants(readOnly).OfType<TextBox>().Where(t => t.DataContext is PreparationField), b => Assert.True(b.IsReadOnly));
                    Assert.Contains("Frozen preparation", vm.HistoricalViewer.Focus.Text);
                    var identity = (TextBlock)history.FindName("HistoricalIdentity");
                    scroll.ScrollToVerticalOffset(scroll.VerticalOffset + identity.TranslatePoint(new Point(), scroll).Y); Layout(); Render(page, theme, width, dpi, "ready");
                    var target = (TextBox)history.FindName("CopyTarget"); target.Text = "broken target"; Pump(vm.TransferAsync(true)); Layout();
                    Assert.Equal("broken target", vm.CopyDate); Assert.NotEmpty(AutomationProperties.GetHelpText(target)); Render(page, theme, width, dpi, "invalid-copy");
                    f.Interaction.Choice = PlanLeaveChoice.Cancel; vm.CopyDate = "16 Oct 2026"; Pump(vm.TransferAsync(true)); Assert.Same(editor, vm.Editor);
                    f.Interaction.Choice = PlanLeaveChoice.Discard; Pump(vm.TransferAsync(true)); Layout();
                    Assert.True(vm.HasRecoveryTarget); Assert.Equal("retained invalid count", editor.MaximumTrades.Text);
                    Assert.Contains("already exists", ((PersonalTradingJournal.Desktop.ViewModels.TradingPlans.PlanNotice)((ContentControl)history.FindName("HistoryStatus")).Content).Text);
                    Render(page, theme, width, dpi, "conflict");
                    vm.CopyDate = theme == "Light" ? "25 Oct 2026" : "26 Oct 2026"; Pump(vm.TransferAsync(true)); Layout();
                    Assert.False(vm.IsHistorySelected); Assert.Contains("saved and verified", vm.Message); Assert.False(vm.Editor!.IsDirty);
                    Render(page, theme, width, dpi, "copied-draft");
                    // A real native focus surface tests tab return without relying on offscreen rendering.
                    if (width == 980)
                    {
                        var window = new Window { Width = 1050, Height = 800, Content = page };
                        using (CalendarStaTest.Phase("History native window Show")) window.Show();
                        try
                        {
                            vm.SelectTab(true); Idle(); from.BringIntoView(); Idle(); Assert.True(from.Focus()); Assert.Same(from, Keyboard.FocusedElement);
                            vm.SelectTab(false); Idle(); var save = (Button)page.FindName("SaveDraft"); Assert.True(save.Focus()); Assert.Same(save, Keyboard.FocusedElement);
                        }
                        finally { using (CalendarStaTest.Phase("History native window Close")) window.Close(); }
                    }
                    Pump(vm.CancelAsync());
                    WpfStepDiagnostics.Write($"History theme assertions and cancellation completed {theme}");
                }
            }, shutdownDispatcher: false);
            WpfStepDiagnostics.Write("History STA completed; disposing fixture");
        }
        finally
        {
            using (CalendarStaTest.Timing("History fixture disposal")) await f.DisposeAsync();
            Assert.Equal(0, f.Db.Connections.ActiveCount);
            Assert.False(Directory.Exists(f.Db.Root));
            WpfStepDiagnostics.Write("History fixture disposed");
        }
    }
    private static void VerifyCurrencyConfirmation(string theme, int dpi)
    {
        var dialogs = new FakeDialogService();
        Assert.False(new WpfTradingPlanInteraction(dialogs).ConfirmCopyForwardCurrency("USD", "EUR", "USD"));
        var dialog = new PersonalTradingJournal.Desktop.Dialogs.PtjDialogWindow(dialogs.ConfirmationRequest!);
        try
        {
            var panel = (FrameworkElement)dialog.Content;
            VisualTreeHelper.SetRootDpi(panel, new(dpi / 96d, dpi / 96d));
            panel.Measure(new(408, 760));
            panel.Arrange(new(0, 0, 408, panel.DesiredSize.Height)); panel.UpdateLayout();
            var message = (TextBlock)dialog.FindName("DialogMessage");
            Assert.Contains("USD", new TextBlockAutomationPeer(message).GetName());
            Assert.Contains("EUR", new TextBlockAutomationPeer(message).GetName());
            var cancel = (Button)dialog.FindName("CancelButton");
            var proceed = (Button)dialog.FindName("PrimaryButton");
            Assert.Equal("Cancel", new ButtonAutomationPeer(cancel).GetName());
            Assert.Equal("Continue", new ButtonAutomationPeer(proceed).GetName());
            Assert.True(cancel.IsCancel); Assert.True(cancel.IsDefault);
            Assert.True(cancel.IsTabStop); Assert.True(proceed.IsTabStop);
            Assert.InRange(panel.ActualHeight, 100, 760);
            Assert.True(cancel.TranslatePoint(new(cancel.ActualWidth, 0), panel).X <= proceed.TranslatePoint(new(), panel).X);
            Assert.InRange(proceed.TranslatePoint(new(proceed.ActualWidth, 0), panel).X, 0, 408);
            FocusManager.SetFocusedElement(dialog, cancel);
            Assert.Same(cancel, FocusManager.GetFocusedElement(dialog));
            Render(panel, theme, 440, dpi, "currency-confirmation");
        }
        finally { dialog.Close(); }
    }
    private static bool Presented(DependencyObject e)
    { for (var p = e; p is not null; p = VisualTreeHelper.GetParent(p)) if (p is UIElement u && u.Visibility != Visibility.Visible) return false; return true; }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    { for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var c = VisualTreeHelper.GetChild(root, i); yield return c; foreach (var n in Descendants(c)) yield return n; } }
    private static void Idle() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static void Pump(Task task, [CallerLineNumber] int line = 0)
    {
        using var phase = CalendarStaTest.Phase($"History async operation/pump line {line}; initial={task.Status}");
        var frame = new DispatcherFrame(); var dispatcher = Dispatcher.CurrentDispatcher; _ = task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default); if (!task.IsCompleted) Dispatcher.PushFrame(frame); task.GetAwaiter().GetResult(); Idle();
    }
    private static void Render(FrameworkElement view, string theme, int width, int dpi, string state)
    {
        if (Environment.GetEnvironmentVariable("PTJ_PLAN_HISTORY_RENDER_DIRECTORY") is not { Length: > 0 } path) return;
        using var phase = CalendarStaTest.Phase($"History render {state}");
        Directory.CreateDirectory(path); var bitmap = new RenderTargetBitmap(width * dpi / 96, 950 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32); bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var output = File.Create(Path.Combine(path, $"history-{theme}-{width}-{dpi}-{state}.png")); encoder.Save(output);
    }
}
