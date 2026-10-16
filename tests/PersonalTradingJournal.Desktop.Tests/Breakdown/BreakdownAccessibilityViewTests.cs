using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.Interactions;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Breakdown;
using PersonalTradingJournal.Desktop.Views.Breakdown;
using PersonalTradingJournal.Desktop.Views.Trades;
using PersonalTradingJournal.Desktop.Tests.Trades;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Desktop.Tests.Breakdown;

public sealed class BreakdownAccessibilityViewTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ScopedTradesEntryDetailReturnMissingRowAndExitUseLiveFocusTargets(bool inputDuringDelete)
    {
        const string host = "PTJ_BREAKDOWN_ACCESSIBILITY_HOST";
        if (Environment.GetEnvironmentVariable(host) != "1")
        {
            await IsolatedTestProcess.RunSuiteAsync(typeof(BreakdownAccessibilityViewTests), "breakdown-accessibility", host,
                TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30)); return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            var app = System.Windows.Application.Current ?? new() { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources = CalendarViewLayoutTests.SharedThemeResources("Light");
            app.Resources["UtcToTradingTimeConverter"] = new PersonalTradingJournal.Desktop.Converters.UtcToTradingTimeConverter();
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/PersonalTradingJournal.Desktop;component/Views/Trades/TradeEntryForm.xaml", UriKind.Relative) });
            TradeListItem row = new(Guid.NewGuid(), Guid.NewGuid(), "Synthetic Account", Guid.NewGuid(), "TEST",
                TradeDirection.Long, TradeStatus.Closed, new(2026, 3, 8, 12, 0, 0, TimeSpan.Zero), new(2026, 3, 8, 13, 0, 0, TimeSpan.Zero),
                0m, 100m, 110m, 2m, 10m, 8m, "EUR", 1m);
            TradeDetail detail = new(row.Id, row.TradingAccountId, row.TradingAccountName, row.InstrumentId, row.InstrumentSymbol,
                "Synthetic instrument", null, null, null, row.Direction, row.Status, row.OpenedAtUtc, row.ClosedAtUtc,
                row.OpenQuantity, row.AverageEntryPrice, row.AverageExitPrice, row.TotalCosts, row.GrossPnL, row.NetPnL, 1m, row.Currency, []);
            var reader = new FakeTradeListReader(); var details = new FakeTradeDetailReader();
            var deletion = new FakeTradeDeletionStore { Result = new(row.Id, []) };
            var vm = TradesViewModelTests.CreateViewModel(tradeListReader: reader, tradeDetailReader: details,
                tradeDeletionStore: deletion, dialogService: new() { ConfirmationResult = true });
            vm.SetBreakdownScope(TradesViewModelTests.CohortContext(row.Id));
            var view = new TradesView { DataContext = vm };
            var window = new Window { Content = view, Width = 1000, Height = 900, ShowActivated = false, ShowInTaskbar = false };
            var prior = SynchronizationContext.Current; SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            try
            {
                reader.EnqueuePage([row], 1); Pump(vm.EnsureLoadedAsync()); window.Show(); Idle();
                Assert.Same(view.FindName("BackToBreakdownButton"), FocusManager.GetFocusedElement(view));
                Button RowAction() => Descendants<Button>(view).Single(b => ReferenceEquals(b.Command, vm.ShowTradeDetailCommand));
                var action = RowAction(); Assert.Contains("TEST", AutomationProperties.GetName(action));
                details.EnqueueResult(detail); PressSpace(action); Pump(vm.ShowTradeDetailCommand.ExecutionTask!); Idle();
                Assert.Same(view.FindName("TradeDetailsHeading"), FocusManager.GetFocusedElement(view));
                var close = Descendants<Button>(view).Single(b => ReferenceEquals(b.Command, vm.CloseTradeDetailCommand));
                FocusManager.SetFocusedElement(view, close); PressSpace(close); Idle();
                Assert.Same(RowAction(), FocusManager.GetFocusedElement(view));
                // Refresh removes the currently focused contributor: do not keep a stale/recycled row focus.
                reader.EnqueuePage([], 0); Pump(vm.RefreshCommand.ExecuteAsync(null)); Idle();
                Assert.Same(view.FindName("TradesRefreshButton"), FocusManager.GetFocusedElement(view));
                reader.EnqueuePage([row], 1); Pump(vm.RefreshCommand.ExecuteAsync(null)); Idle();
                details.EnqueueResult(detail); PressSpace(RowAction()); Pump(vm.ShowTradeDetailCommand.ExecutionTask!); Idle();
                var reload = new TaskCompletionSource<TradeListPage>(TaskCreationOptions.RunContinuationsAsynchronously);
                reader.EnqueuePageBehavior((_, _) => reload.Task);
                var deleting = vm.DeleteSelectedTradeCommand.ExecuteAsync(null); Idle();
                Assert.True(vm.IsDeletingTrade); Assert.False(vm.IsTradeDetailVisible);
                if (inputDuringDelete)
                {
                    view.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120) { RoutedEvent = Mouse.PreviewMouseWheelEvent });
                    FocusManager.SetFocusedElement(view, (ScrollViewer)view.Content);
                }
                reload.SetResult(new([], 1, 20, 0)); Pump(deleting); Idle();
                Assert.Equal(1, deletion.CallCount); Assert.Equal(row.Id, deletion.RequestedTradeId);
                Assert.Same(inputDuringDelete ? view.Content : view.FindName("TradesRefreshButton"), FocusManager.GetFocusedElement(view));
                var exit = Descendants<Button>(view).Single(b => ReferenceEquals(b.Command, vm.ExitBreakdownScopeCommand));
                FocusManager.SetFocusedElement(view, exit); PressSpace(exit); Pump(vm.ExitBreakdownScopeCommand.ExecutionTask!); Idle();
                Assert.False(vm.HasBreakdownScope); Assert.Same(view.FindName("TradesRefreshButton"), FocusManager.GetFocusedElement(view));
            }
            finally { window.Close(); vm.DisposeBrowse(); view.DataContext = null; SynchronizationContext.SetSynchronizationContext(prior); }
        });
    }

    [Fact]
    public async Task OwnedFocusValidationAutomationAndRetainedStatesInBothThemes()
    {
        const string host = "PTJ_BREAKDOWN_ACCESSIBILITY_HOST";
        if (Environment.GetEnvironmentVariable(host) != "1")
        {
            await IsolatedTestProcess.RunSuiteAsync(typeof(BreakdownAccessibilityViewTests), "breakdown-accessibility", host,
                TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30)); return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            using var f = new BreakdownFixture();
            var app = System.Windows.Application.Current ?? new() { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources = CalendarViewLayoutTests.SharedThemeResources("Light");
            var view = new BreakdownView { DataContext = f.Vm };
            var window = new Window { Content = view, Width = 1000, Height = 900, ShowActivated = false, ShowInTaskbar = false };
            var prior = SynchronizationContext.Current; SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            try
            {
                Pump(f.Vm.ActivateAsync()); window.Show(); Idle();
                var currency = (TextBox)view.FindName("CurrencyFilter");
                var period = (ComboBox)view.FindName("PeriodFilter");
                Assert.Same(period, FocusManager.GetFocusedElement(view));
                foreach (string name in new[] { "PeriodFilter", "AccountFilter", "InstrumentFilter", "CurrencyFilter", "GroupingFilter", "ModeFilter", "FromDate", "ThroughDate", "ChartMetricSelector" })
                {
                    var element = (FrameworkElement)view.FindName(name);
                    Assert.NotNull(AutomationProperties.GetLabeledBy(element)); Assert.NotEmpty(AutomationProperties.GetName(element));
                }
                var apply = Descendants<Button>(view).Single(b => Equals(b.Content, "Apply filters"));
                f.Vm.Currency = "";
                PressSpace(apply); Idle();
                Assert.NotNull(f.Vm.CurrencyValidation); Assert.Same(currency, FocusManager.GetFocusedElement(view));
                Assert.Equal(f.Vm.CurrencyValidation, AutomationProperties.GetItemStatus(currency));
                var status = (TextBlock)view.FindName("OperationStatus");
                Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(status));
                Assert.Equal(f.Vm.StatusMessage, LiveAnnouncement.GetText(status));
                var guard = new PageFocus(view);
                guard.Request(() => period); FocusManager.SetFocusedElement(view, apply); Idle();
                Assert.Same(apply, FocusManager.GetFocusedElement(view)); // New input wins over queued focus.
                guard.Request(() => period); guard.Invalidate(); Idle(); Assert.Same(apply, FocusManager.GetFocusedElement(view));
                guard.Request(() => period); view.DataContext = null; Idle(); Assert.Same(apply, FocusManager.GetFocusedElement(view));
                view.DataContext = f.Vm;
                f.Vm.OpenTradesAsync = _ => System.Threading.Tasks.Task.CompletedTask;
                f.Vm.Currency = "EUR"; Pump(f.Vm.ApplyAsync()); Idle();
                var chart = (ListBox)view.FindName("ComparisonChart");
                Assert.Equal(KeyboardNavigationMode.Once, KeyboardNavigation.GetTabNavigation(chart));
                var chartPeer = UIElementAutomationPeer.CreatePeerForElement(chart);
                Assert.IsAssignableFrom<ISelectionProvider>(chartPeer.GetPattern(PatternInterface.Selection));
                chart.SelectedIndex = 0; Idle();
                var detailsAction = (Button)view.FindName("SelectedTradesButton");
                Assert.Contains(f.Vm.SelectedGroup!.Label, AutomationProperties.GetName(detailsAction));
                var table = (DataGrid)view.FindName("GroupTable"); table.BringIntoView(); Idle();
                var header = Descendants<DataGridColumnHeader>(table).Single(h => h.Column?.SortMemberPath == "Pnl");
                PressSpace(header); Idle();
                Assert.Equal(BreakdownSortColumn.Pnl, f.Vm.SortColumn);
                Assert.Equal("Ascending", AutomationProperties.GetItemStatus(header));
                Assert.True(table.IsReadOnly); Assert.True(table.EnableRowVirtualization);
                // Completion of an asynchronous read never takes focus away from edited input.
                f.Reader.Hold = true; f.Reader.Drain(); var pending = f.Vm.RefreshAsync(); var call = f.Reader.Next(); Pump(call);
                FocusManager.SetFocusedElement(view, currency);
                call.Result.Completion.SetResult(BreakdownFixture.Snapshot(call.Result.Query)); Pump(pending); Idle();
                Assert.Same(currency, FocusManager.GetFocusedElement(view));
                f.Reader.Drain(); pending = f.Vm.RefreshAsync(); call = f.Reader.Next(); Pump(call); Idle();
                var cancel = Descendants<Button>(view).Single(b => ReferenceEquals(b.Command, f.Vm.CancelCommand));
                Assert.True(cancel.IsVisible); Assert.True(cancel.IsEnabled);
                FocusManager.SetFocusedElement(view, cancel); PressSpace(cancel); Idle();
                Assert.Same(view.FindName("RefreshButton"), FocusManager.GetFocusedElement(view));
                call.Result.Completion.SetResult(BreakdownFixture.Snapshot(call.Result.Query)); Pump(pending); Idle();
                Assert.Contains("cancelled", f.Vm.StatusMessage); f.Reader.Hold = false;
                foreach (string theme in new[] { "Light", "Dark" })
                foreach (var (width, dpi) in new[] { (1280, 96), (700, 240) })
                {
                    // Fresh off-screen roots avoid retaining the HWND's layout constraints/DPI.
                    window.Content = null; Idle();
                    BreakdownView? rendered = null;
                    f.Vm.Currency = ""; Pump(f.Vm.ApplyAsync()); Layout(); Render("validation-retained");
                    f.Vm.Currency = "EUR"; f.Reader.MakeResult = _ => throw new IOException("Synthetic private exception");
                    Pump(f.Vm.ApplyAsync()); Layout(); Render("error-retained");
                    Assert.DoesNotContain("private", f.Vm.ErrorMessage!); Assert.NotNull(f.Vm.Result);
                    f.Reader.MakeResult = q => new(q, BreakdownPresentationTests.Metrics(0, 0, null), []);
                    Pump(f.Vm.RefreshAsync()); Layout(); Render("empty");
                    Assert.Contains(Descendants<TextBlock>(rendered!), t => t.Text == "No matching closed Trades.");
                    f.Reader.MakeResult = q => BreakdownFixture.Snapshot(q); Pump(f.Vm.RefreshAsync());
                    window.Content = view; Idle();
                    void Layout()
                    {
                        if (rendered is not null) rendered.DataContext = null;
                        rendered = new() { DataContext = f.Vm };
                        rendered.Resources.MergedDictionaries.Add(CalendarViewLayoutTests.SharedThemeResources(theme));
                        VisualTreeHelper.SetRootDpi(rendered, new(dpi / 96d, dpi / 96d));
                        rendered.Measure(new(width, 900)); rendered.Arrange(new(0, 0, width, 900)); rendered.UpdateLayout();
                        Assert.Equal(width, rendered.ActualWidth);
                        Assert.InRange(((ScrollViewer)rendered.Content).ExtentWidth, 0, width);
                    }
                    void Render(string state)
                    {
                        string path = Path.Combine(AppContext.BaseDirectory, "breakdown-accessibility-renders"); Directory.CreateDirectory(path);
                        var bitmap = new RenderTargetBitmap(width * dpi / 96, 900 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32); bitmap.Render(rendered);
                        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using var output = File.Create(Path.Combine(path, $"{theme}-{width}-{dpi}-{state}.png")); encoder.Save(output);
                    }
                }
                guard.Request(() => period); window.Content = null; Idle();
                Assert.False(view.IsLoaded); // Unloaded views do not accept delayed focus.
            }
            finally { window.Close(); view.DataContext = null; SynchronizationContext.SetSynchronizationContext(prior); }
        });
    }
    private static void PressSpace(UIElement button)
    {
        var source = PresentationSource.FromVisual(button);
        button.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Space) { RoutedEvent = Keyboard.KeyDownEvent });
        button.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Space) { RoutedEvent = Keyboard.KeyUpEvent });
    }
    private static void Idle() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
    private static void Pump(Task task)
    {
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            _ = task.ContinueWith(_ => frame.Continue = false, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
            Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); if (child is T found) yield return found;
            foreach (var item in Descendants<T>(child)) yield return item;
        }
    }
}
