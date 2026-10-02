using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Diagnostics;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;
using PersonalTradingJournal.Desktop.Views.Calendar;

namespace PersonalTradingJournal.Desktop.Tests.CalendarPage;

public sealed class CalendarDayModalTests
{
    private static readonly Lazy<Task> NativeHost = new(RunNativeHostAsync);
    [Fact]
    public void PerformanceUsesActualClosuresCurrencyPartitionsAndExactAuthoritativeNet()
    {
        DateOnly date = new(2026, 9, 5);
        TradeListItem first = CalendarDayDetailsTests.Row(date, 110m, 100m) with { ClosedAtUtc = new(2026, 9, 5, 15, 0, 0, TimeSpan.Zero) };
        TradeListItem loss = CalendarDayDetailsTests.Row(date, -40m, -40m) with { ClosedAtUtc = first.ClosedAtUtc!.Value.AddMinutes(30) };
        TradeListItem zero = CalendarDayDetailsTests.Row(date, 0m, 0m) with { ClosedAtUtc = loss.ClosedAtUtc };
        TradeListItem euro = CalendarDayDetailsTests.Row(date, 5m, 5m) with { Currency = "EUR" };
        var series = CalendarDayPerformance.From([zero, euro, loss, first]);
        Assert.Equal(new[] { "EUR", "USD" }, series.Select(s => s.Currency));
        var usd = series[1].Points;
        Assert.Equal(new decimal?[] { 100m, 60m }, usd.Select(p => p.Value));
        Assert.Equal(new[] { 1, 2 }, usd.Select(p => p.TradeCount));
        Assert.Equal(first.ClosedAtUtc, usd[0].ClosedAtUtc);
        Assert.Equal(loss.ClosedAtUtc, usd[1].ClosedAtUtc);
        Assert.All(usd, p => Assert.False(p.IsEstimated));
        Assert.Equal(5m, Assert.Single(series[0].Points).Value);
        var estimated = CalendarDayPerformance.From([CalendarDayDetailsTests.Row(date, -285m, null)]);
        Assert.Equal(-285m, estimated[0].Points[0].Value);
        Assert.Contains("commission/fees unknown", estimated[0].Points[0].Description);
        var unknown = CalendarDayDetailsTests.Row(date, null, null) with { ClosedAtUtc = first.ClosedAtUtc!.Value.AddMinutes(15) };
        Assert.Equal(new decimal?[] { 100m, null, null }, CalendarDayPerformance.From([first, unknown, loss])[0].Points.Select(p => p.Value));
        Assert.Empty(CalendarDayPerformance.From([]));
        Assert.Throws<OverflowException>(() => CalendarDayPerformance.From([first with { NetPnL = decimal.MaxValue }, loss with { NetPnL = 1m }]));
    }

    [Theory]
    [InlineData("Light", 1100, 96, false)]
    [InlineData("Dark", 1100, 96, true)]
    [InlineData("Light", 480, 240, true)]
    [InlineData("Dark", 480, 240, false)]
    public async Task OwnedModalDimsCalendarClosesAndRestoresDateFocusWithJournalDisabled(string theme, int width, int dpi, bool empty)
    {
        var reader = new FakeTradingCalendarDayReader { Handler = (q, ct) => Task.FromResult(TradingCalendarDayDetails.Create(q.Date,
            empty ? [] : [CalendarDayDetailsTests.Row(q.Date, -285m, null), CalendarDayDetailsTests.Row(q.Date, 10m, 9m) with { Currency = "EUR" }], ct)) };
        CalendarViewModel vm = await CalendarSummaryFixture.CreateAsync(reader);
        await OnSta(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            var view = new CalendarView { DataContext = vm, Resources = CalendarViewLayoutTests.SharedThemeResources(theme) };
            var owner = new Window { Content = view, Width = width, Height = 760, ShowInTaskbar = false };
            Exception? failure = null;
            try
            {
                owner.Show();
                Pump(); // Let Loaded/layout/container work run before interacting with the native window.
                owner.UpdateLayout();
                var saturday = Assert.Single(Descendants(view).OfType<CalendarDayHost>(), c => ((CalendarDayCell)c.DataContext).Date == new DateOnly(2026, 9, 5));
                var account = vm.SelectedAccount;
                string currency = vm.SelectedCurrency;
                dispatcher.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        var dialog = Assert.IsType<CalendarDayDialogWindow>(view.DayDialog);
                        Assert.Same(owner, dialog.Owner);
                        Assert.True(dialog.IsVisible);
                        Assert.Equal(Visibility.Visible, ((Border)view.FindName("ModalShade")).Visibility);
                        await vm.DayLoadTask;
                        dialog.UpdateLayout();
                        Assert.Equal(saturday.DataContext, CalendarDayDetailsTests.Cell(vm, new(2026, 9, 5)));
                        Assert.Equal(empty ? 0 : 2, vm.DayDetails!.ClosedTradeCount);
                        Assert.Null(view.FindName("DayDetailsPanel"));
                        var content = (CalendarDayDetailsView)dialog.FindName("DayContent");
                        Assert.Contains(Descendants(content).OfType<TextBlock>(), t => t.Text == "Day Performance");
                        Button journal = Assert.Single(Descendants(content).OfType<Button>(), b => Equals(b.Content, "Add Journal"));
                        Assert.False(journal.IsEnabled);
                        Assert.Null(journal.Command);
                        Assert.Contains("coming later", AutomationProperties.GetName(journal), StringComparison.OrdinalIgnoreCase);
                        Assert.Equal(empty ? 0 : 2, Descendants(content).OfType<DayPerformanceChart>().Count());
                        if (empty) Assert.Contains(Descendants(content).OfType<TextBlock>(), t => t.Text == "No closed Trades to chart." && t.IsVisible);
                        else
                        {
                            Button[] buttons = Descendants(content).OfType<Button>().Where(b => Equals(b.Content, "View")).ToArray();
                            Assert.Equal(2, buttons.Length);
                            Assert.All(buttons, button => { Assert.Same(vm.ViewTradeCommand, button.Command); Assert.Contains(vm.DayTrades, row => ReferenceEquals(row.Trade, button.CommandParameter)); });
                            reader.Handler = (q, ct) => Task.FromResult(TradingCalendarDayDetails.Create(q.Date, [CalendarDayDetailsTests.Row(q.Date, 9m, 9m)], ct));
                            vm.OnDataCommitted();
                            await vm.LoadTask;
                            dialog.UpdateLayout();
                            Assert.True(dialog.IsVisible);
                            Assert.Equal(9m, Assert.Single(Assert.Single(vm.DayPerformance).Points).Value);
                            Assert.Equal(9m, Assert.Single(Descendants(content).OfType<DayPerformanceChart>()).Series!.Points.Single().Value);
                        }
                        Render(content, theme, width, dpi);
                        if (empty) ((Button)dialog.FindName("CloseButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                        else
                        {
                            var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(dialog)!, 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                            dialog.RaiseEvent(args);
                            Assert.True(args.Handled);
                        }
                    }
                    catch (Exception exception) { failure = exception; view.DayDialog?.Close(); }
                }));
                saturday.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.MouseUpEvent });
                Pump();
                if (failure is not null) throw failure;
                Assert.Null(view.DayDialog);
                Assert.Equal(Visibility.Collapsed, ((Border)view.FindName("ModalShade")).Visibility);
                Assert.Equal(new DateOnly(2026, 9, 5), vm.SelectedDate);
                Assert.Equal(new DateOnly(2026, 9, 1), vm.SelectedMonth);
                Assert.Same(account, vm.SelectedAccount);
                Assert.Equal(currency, vm.SelectedCurrency);
                Assert.True(saturday.IsKeyboardFocused);
            }
            finally { owner.Close(); }
        });
    }

    [Fact]
    public async Task ClosingWhileLoadingCancelsReadAndRejectsItsLateResultWithoutClearingSelection()
    {
        var pending = new TaskCompletionSource<TradingCalendarDayDetails>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reader = new FakeTradingCalendarDayReader { Handler = (_, _) => { started.TrySetResult(); return pending.Task; } };
        var vm = await CalendarSummaryFixture.CreateAsync(reader);
        await OnSta(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            var view = new CalendarView { DataContext = vm, Resources = CalendarViewLayoutTests.SharedThemeResources("Dark") };
            var owner = new Window { Content = view, Width = 900, Height = 700, ShowInTaskbar = false };
            Exception? failure = null;
            try
            {
                owner.Show(); Pump(); owner.UpdateLayout();
                dispatcher.BeginInvoke(new Action(async () =>
                {
                    try { await started.Task; Assert.True(vm.IsDayLoading); view.DayDialog!.Close(); }
                    catch (Exception exception) { failure = exception; view.DayDialog?.Close(); }
                }));
                var cell = Assert.Single(Descendants(view).OfType<CalendarDayHost>(), c => ((CalendarDayCell)c.DataContext).Date == new DateOnly(2026, 9, 5));
                cell.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.MouseUpEvent });
                if (failure is not null) throw failure;
                Assert.Equal(new DateOnly(2026, 9, 5), vm.SelectedDate);
                Assert.True(reader.Calls.Single().Token.IsCancellationRequested);
                Assert.False(vm.IsDayLoading);
                var frame = new DispatcherFrame();
                _ = vm.DayLoadTask.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)), TaskScheduler.Default);
                pending.SetResult(TradingCalendarDayDetails.Create(vm.SelectedDate!.Value, [CalendarDayDetailsTests.Row(vm.SelectedDate.Value, 999m, 999m)]));
                Dispatcher.PushFrame(frame);
                vm.DayLoadTask.GetAwaiter().GetResult();
                Assert.Empty(vm.DayTrades);
                Assert.Empty(vm.DayPerformance);
                Assert.Null(vm.DayDetails);
            }
            finally { owner.Close(); }
        });
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task ViewReleasesModalBeforeOpeningExactTrade(string theme)
    {
        var reader = new FakeTradingCalendarDayReader { Handler = (q, ct) => Task.FromResult(TradingCalendarDayDetails.Create(q.Date, [CalendarDayDetailsTests.Row(q.Date, 7m, 7m)], ct)) };
        var vm = await CalendarSummaryFixture.CreateAsync(reader);
        await OnSta(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            var view = new CalendarView { DataContext = vm, Resources = CalendarViewLayoutTests.SharedThemeResources(theme) };
            var owner = new Window { Content = view, Width = 900, Height = 700, ShowInTaskbar = false };
            Guid? opened = null;
            Exception? failure = null;
            vm.OpenTradeAsync = row => { Assert.Null(view.DayDialog); opened = row.Id; return Task.CompletedTask; };
            try
            {
                owner.Show(); Pump(); owner.UpdateLayout();
                dispatcher.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        await vm.DayLoadTask;
                        var dialog = view.DayDialog!;
                        dialog.UpdateLayout();
                        Button viewButton = Assert.Single(Descendants(dialog).OfType<Button>(), b => Equals(b.Content, "View"));
                        ((IInvokeProvider)new ButtonAutomationPeer(viewButton).GetPattern(PatternInterface.Invoke)).Invoke();
                    }
                    catch (Exception exception) { failure = exception; view.DayDialog?.Close(); }
                }));
                CalendarDayHost cell = Assert.Single(Descendants(view).OfType<CalendarDayHost>(), c => ((CalendarDayCell)c.DataContext).Date == new DateOnly(2026, 9, 5));
                cell.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.MouseUpEvent });
                Pump();
                if (failure is not null) throw failure;
                Assert.Equal(vm.DayTrades.Single().Trade.Id, opened);
            }
            finally { owner.Close(); }
        });
    }

    [Theory]
    [InlineData("Light", 960, 96)]
    [InlineData("Dark", 480, 240)]
    public async Task IntradayChartUsesClosureTimeStepsExactAccessibleTargetsAndUnknownGaps(string theme, int width, int dpi)
    {
        await OnSta(() =>
        {
            DateOnly date = new(2026, 11, 1);
            TradeListItem a = CalendarDayDetailsTests.Row(date, 100m, 100m) with { ClosedAtUtc = new(2026, 11, 1, 5, 30, 0, TimeSpan.Zero) };
            TradeListItem b = a with { Id = Guid.NewGuid(), ClosedAtUtc = a.ClosedAtUtc!.Value.AddHours(1), NetPnL = -140m };
            TradeListItem c = a with { Id = Guid.NewGuid(), ClosedAtUtc = b.ClosedAtUtc!.Value.AddHours(2), NetPnL = null, GrossPnL = null };
            var series = CalendarDayPerformance.From([c, b, a])[0];
            var chart = new DayPerformanceChart { Series = series, Resources = CalendarViewLayoutTests.SharedThemeResources(theme),
                PositiveBrush = Brushes.Green, NegativeBrush = Brushes.Red, NeutralBrush = Brushes.Gray };
            chart.Measure(new Size(width, 260)); chart.Arrange(new Rect(0, 0, width, 260)); chart.UpdateLayout();
            Border[] targets = Descendants(chart).OfType<Border>().Where(b => b.Tag is CalendarDayPerformancePoint).ToArray();
            Assert.Equal(3, targets.Length);
            Assert.All(targets, target => { Assert.True(target.Focusable); Assert.Equal(((CalendarDayPerformancePoint)target.Tag).Description, AutomationProperties.GetName(target)); Assert.IsType<ToolTip>(target.ToolTip); });
            Assert.Contains("01:30:00 -04:00", AutomationProperties.GetName(targets[0]));
            Assert.Contains("01:30:00 -05:00", AutomationProperties.GetName(targets[1]));
            double x1 = Canvas.GetLeft(targets[0]), x2 = Canvas.GetLeft(targets[1]), x3 = Canvas.GetLeft(targets[2]);
            Assert.Equal(2d, (x3 - x2) / (x2 - x1), 6); // Actual elapsed UTC time, including the DST overlap.
            var steps = Descendants(chart).OfType<Line>().Where(line => Equals(line.Tag, "ClosureStep")).ToArray();
            Assert.Equal(3, steps.Length); // One hold plus a zero-crossing split at the second closure only.
            Assert.All(steps, line => Assert.True(line.X1 == line.X2 || line.Y1 == line.Y2));
            Assert.Contains(steps, line => line.Stroke == Brushes.Red);
            Assert.Contains(steps, line => line.Stroke == Brushes.Green);
            Assert.Contains(Descendants(chart).OfType<TextBlock>(), t => t.Text.Contains("Unavailable cumulative"));
            var bitmap = new RenderTargetBitmap(width * dpi / 96, 260 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
            bitmap.Render(chart);
            chart.Series = new("USD", []);
            chart.UpdateLayout();
            Assert.DoesNotContain(Descendants(chart).OfType<Border>(), target => target.Tag is CalendarDayPerformancePoint);
            Assert.Contains(Descendants(chart).OfType<TextBlock>(), t => t.Text == "No closed Trades to chart.");
        });
    }

    private static void Render(CalendarDayDetailsView content, string theme, int width, int dpi)
    {
        if (Environment.GetEnvironmentVariable("PTJ_CALENDAR_RENDER_DIRECTORY") is not { Length: > 0 } output) return;
        content.Measure(new Size(width, 700)); content.Arrange(new Rect(0, 0, width, 700)); content.UpdateLayout();
        var image = new RenderTargetBitmap(width * dpi / 96, 700 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
        image.Render(content);
        Directory.CreateDirectory(output);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var file = File.Create(System.IO.Path.Combine(output, $"calendar-modal-{theme}-{width}-{dpi}.png")); encoder.Save(file);
    }
    private static void Pump()
    {
        var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame);
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    private static Task OnSta(Action action)
    {
        // Another existing test shuts down WPF's process-wide Application. Native windows
        // must have a fresh WPF lifetime; never depend on xUnit collection execution order.
        if (Environment.GetEnvironmentVariable("PTJ_CALENDAR_MODAL_TEST_HOST") != "1") return NativeHost.Value;
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { try { action(); done.SetResult(); } catch (Exception exception) { done.SetException(exception); } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return done.Task.WaitAsync(TimeSpan.FromSeconds(45));
    }

    private static async Task RunNativeHostAsync()
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("vstest");
        start.ArgumentList.Add(typeof(CalendarDayModalTests).Assembly.Location);
        start.ArgumentList.Add("/TestCaseFilter:FullyQualifiedName~CalendarDayModalTests");
        start.Environment["PTJ_CALENDAR_MODAL_TEST_HOST"] = "1";
        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync(), error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(2));
        Assert.True(process.ExitCode == 0, $"Isolated native modal tests failed:\n{await output}\n{await error}");
    }
}
