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
using PersonalTradingJournal.Desktop.Tests.Trades;

namespace PersonalTradingJournal.Desktop.Tests.CalendarPage;

public sealed class CalendarDayModalTests
{
    [Theory]
    [InlineData("Light", 1100, 96, false)]
    [InlineData("Dark", 1100, 96, false)]
    [InlineData("Light", 1900, 96, false)]
    [InlineData("Dark", 1900, 96, false)]
    [InlineData("Light", 480, 240, false)]
    [InlineData("Dark", 480, 240, false)]
    [InlineData("Light", 1100, 96, true)]
    [InlineData("Dark", 1100, 96, true)]
    [InlineData("Light", 1900, 96, true)]
    [InlineData("Dark", 1900, 96, true)]
    [InlineData("Light", 480, 240, true)]
    [InlineData("Dark", 480, 240, true)]
    public async Task ResponsiveColumnsCenterNetFillViewportAndKeepAccountStableWithExpandedRows(string theme, int width, int dpi, bool longName)
    {
        var first = CalendarDayDetailsTests.Row(new(2026, 9, 5), -1234567.89m, null) with { TradingAccountName = "P 21" };
        var second = first with { Id = Guid.NewGuid(), GrossPnL = 200m, NetPnL = 197m, TotalCosts = 3m, TradingAccountName = longName
            ? "An unusually long historical account name that must remain available in full" : "Q 7" };
        var detail = TradesViewModelTests.CreateEditableTradeDetail(second, null);
        var details = new FakeTradeDetailReader(); details.EnqueueResult(detail);
        var editor = TradesViewModelTests.CreateViewModel(tradeDetailReader: details);
        var classification = new CalendarTradeClassification(Guid.NewGuid(), longName
            ? "A long historical Setup name with several conditions and confirmations to review"
            : "15m key level + 15m range", true, longName
                ? [new(Guid.NewGuid(), "Entered before confirmation of the planned Setup", true),
                   new(Guid.NewGuid(), "Exceeded the planned position size", false),
                   new(Guid.NewGuid(), "Moved the protective stop repeatedly", true)] : []);
        var reader = new FakeTradingCalendarDayReader { Handler = (q, ct) => Task.FromResult(
            TradingCalendarDayDetails.Create(q.Date, [first, second]) with
            { Classifications = new Dictionary<Guid, CalendarTradeClassification> { [second.Id] = classification } }) };
        var vm = await CalendarSummaryFixture.CreateAsync(reader, editor);
        await vm.SelectDayCommand.ExecuteAsync(CalendarDayDetailsTests.Cell(vm, new(2026, 9, 5)));
        await vm.ViewTradeCommand.ExecuteAsync(second);
        await OnSta(() =>
        {
            var content = new CalendarDayDetailsView { DataContext = vm, Resources = CalendarViewLayoutTests.SharedThemeResources(theme) };
            var window = new CalendarDayDialogWindow { DataContext = vm, Content = content, Width = width, Height = 760, ShowInTaskbar = false };
            try
            {
                window.Show(); Pump(); window.UpdateLayout();
                var header = (Grid)content.FindName("DayTradesHeader");
                var scroller = (ScrollViewer)content.FindName("DayTradesScroller");
                Grid[] rows = Descendants(content).OfType<Grid>().Where(g => g.Name == "DayTradeRow").ToArray();
                Assert.Equal(2, rows.Length);
                Grid[] grids = [header, .. rows];
                Assert.Equal(new[] { "Time (New York)", "Instrument", "Account", "Net P&L", "Size", "Direction", "Setup", "Trading Mistakes", "Action" },
                    header.Children.OfType<TextBlock>().OrderBy(Grid.GetColumn).Select(t => t.Text));
                TextBlock[] names = rows.Select(g => g.Children.OfType<TextBlock>().Single(t => Grid.GetColumn(t) == 2)).ToArray();
                foreach (Grid row in rows)
                {
                    var trade = (CalendarTradePresentation)row.DataContext;
                    Assert.Equal(trade.AmountText, row.Children.OfType<StackPanel>().Single(p => Grid.GetColumn(p) == 3).Children.OfType<TextBlock>().First().Text);
                    Assert.Equal(trade.SizeText, row.Children.OfType<TextBlock>().Single(t => Grid.GetColumn(t) == 4).Text);
                    Assert.Equal(trade.Trade.Direction.ToString(), row.Children.OfType<TextBlock>().Single(t => Grid.GetColumn(t) == 5).Text);
                    foreach (int column in new[] { 6, 7 })
                    {
                        var text = row.Children.OfType<TextBlock>().Single(t => Grid.GetColumn(t) == column);
                        Assert.Equal(column == 6 ? trade.SetupText : trade.MistakesText, text.Text);
                        Assert.Equal(text.Text, text.ToolTip);
                        Assert.Equal(TextWrapping.Wrap, text.TextWrapping);
                        Assert.Equal(row.ColumnDefinitions[column].ActualWidth - 8, text.ActualWidth, 1);
                        // TextBlocks stretch to the tallest cell; desired height measures actual wrapping.
                        if (longName && width < 700 && trade.Trade.Id == second.Id) Assert.True(text.DesiredSize.Height > 30);
                        if (text.Text == "None assigned") Assert.True(text.DesiredSize.Height < 25);
                    }
                    Assert.Equal("View", row.Children.OfType<Button>().Single(b => Grid.GetColumn(b) == 8).Content);
                    var net = row.Children.OfType<StackPanel>().Single(p => Grid.GetColumn(p) == 3);
                    Assert.All(net.Children.OfType<TextBlock>(), t => Assert.Equal(TextAlignment.Center, t.TextAlignment));
                    var amount = net.Children.OfType<TextBlock>().First();
                    amount.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    Assert.True(amount.DesiredSize.Width <= net.ActualWidth, "Signed currency value must fit the compact Net column.");
                    Assert.Same(content.Resources[trade.Amount < 0 ? "PtjDangerBrush" : "PtjSuccessBrush"], amount.Foreground);
                    Assert.Equal(trade.NetDescription, net.ToolTip);
                    Assert.Equal(trade.IsEstimated ? Visibility.Visible : Visibility.Collapsed, net.Children.OfType<TextBlock>().Last().Visibility);
                    double columnCenter = row.ColumnDefinitions.Take(3).Sum(c => c.ActualWidth) + row.ColumnDefinitions[3].ActualWidth / 2;
                    Assert.Equal(columnCenter, net.TransformToAncestor(row).Transform(new Point()).X + net.ActualWidth / 2, 1);
                }
                void CheckAlignment()
                {
                    foreach (Grid row in rows)
                        for (int column = 0; column < header.ColumnDefinitions.Count; column++)
                        {
                            var heading = header.Children.OfType<FrameworkElement>().Single(e => Grid.GetColumn(e) == column);
                            var value = row.Children.OfType<FrameworkElement>().Single(e => Grid.GetColumn(e) == column);
                            Assert.Equal(heading.TransformToAncestor(scroller).Transform(new Point()).X,
                                value.TransformToAncestor(scroller).Transform(new Point()).X, 1);
                        }
                }
                double after = header.ColumnDefinitions[2].ActualWidth;
                var label = header.Children.OfType<TextBlock>().Single(t => Grid.GetColumn(t) == 2);
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Assert.InRange(after, label.DesiredSize.Width - .1, 160);
                // WPF layout rounding at the host's display DPI may shave a fraction of a DIP.
                if (longName) Assert.InRange(after, 159, 160);
                else Assert.True(after < 90, $"Short Account width was {after}");
                Assert.All(names, t => Assert.Equal(((CalendarTradePresentation)t.DataContext).Trade.TradingAccountName, t.ToolTip));
                Assert.All(names, t => Assert.Equal(TextTrimming.CharacterEllipsis, t.TextTrimming));
                CheckAlignment();
                Assert.All(rows, g => Assert.Equal(after, g.ColumnDefinitions[2].ActualWidth, 1));
                Assert.All(grids, grid => Assert.Equal(150, grid.ColumnDefinitions[3].ActualWidth, 1));
                Assert.True(header.ColumnDefinitions[6].ActualWidth >= 150);
                Assert.True(header.ColumnDefinitions[7].ActualWidth >= 170);
                var netHeading = header.Children.OfType<TextBlock>().Single(t => Grid.GetColumn(t) == 3);
                Assert.Equal(TextAlignment.Center, netHeading.TextAlignment);
                Assert.Equal(header.ColumnDefinitions.Take(3).Sum(c => c.ActualWidth) + header.ColumnDefinitions[3].ActualWidth / 2,
                    netHeading.TransformToAncestor(header).Transform(new Point()).X + netHeading.ActualWidth / 2, 1);
                Assert.Equal(header.ActualWidth, header.ColumnDefinitions.Sum(c => c.ActualWidth), 1);
                if (width >= 1100)
                {
                    Assert.All(rows, row =>
                    {
                        var action = row.Children.OfType<Button>().Single();
                        double right = action.TransformToAncestor(scroller).Transform(new Point()).X + action.ActualWidth;
                        // A stretched ScrollContentPresenter can be wider than its desired extent.
                        Assert.InRange(Math.Max(scroller.ViewportWidth, scroller.ExtentWidth) - right, 7, 9);
                    });
                }
                if (width >= 1900)
                {
                    Assert.Equal(0, scroller.ScrollableWidth);
                    foreach (int column in new[] { 6, 7 })
                        Assert.True(header.ColumnDefinitions[column].ActualWidth > header.ColumnDefinitions[column].MinWidth);
                }
                Assert.Single(vm.DayTrades, r => r.IsExpanded && r.Trade.Id == second.Id);
                if (width < 700)
                {
                    scroller.ScrollToRightEnd(); Pump(); window.UpdateLayout();
                    Assert.True(scroller.HorizontalOffset > 0);
                    Assert.Equal(scroller.ScrollableWidth, scroller.HorizontalOffset, 1);
                    CheckAlignment();
                    Assert.Equal(after, header.ColumnDefinitions[2].ActualWidth, 1);
                    scroller.ScrollToLeftEnd(); Pump(); window.UpdateLayout();
                }

                // Measure the preceding double-star Net layout at the same viewport.
                GridLength[] responsiveWidths = header.ColumnDefinitions.Select(c => c.Width).ToArray();
                double[] minimumWidths = header.ColumnDefinitions.Select(c => c.MinWidth).ToArray();
                GridLength[] priorWidths = [new(1, GridUnitType.Star), new(1, GridUnitType.Star), GridLength.Auto,
                    new(2, GridUnitType.Star), new(.5, GridUnitType.Star), new(.75, GridUnitType.Star), new(150), new(170), new(64)];
                double[] priorMinimums = [112, 100, 0, 170, 56, 70, 0, 0, 0];
                foreach (Grid grid in grids)
                {
                    for (int column = 0; column < priorWidths.Length; column++)
                    {
                        grid.ColumnDefinitions[column].Width = priorWidths[column];
                        grid.ColumnDefinitions[column].MinWidth = priorMinimums[column];
                    }
                }
                Pump(); window.UpdateLayout();
                double beforeGap = header.ActualWidth - header.ColumnDefinitions.Sum(c => c.ActualWidth);
                double beforeNetWidth = header.ColumnDefinitions[3].ActualWidth;
                foreach (Grid grid in grids)
                {
                    for (int column = 0; column < responsiveWidths.Length; column++)
                    {
                        grid.ColumnDefinitions[column].Width = responsiveWidths[column];
                        grid.ColumnDefinitions[column].MinWidth = minimumWidths[column];
                    }
                }
                Pump(); window.UpdateLayout();
                Assert.Equal(after, header.ColumnDefinitions[2].ActualWidth, 1);
                CheckAlignment();
                double afterGap = header.ActualWidth - header.ColumnDefinitions.Sum(c => c.ActualWidth);
                Assert.InRange(afterGap, -.1, .1);
                string measurements = $"Window={window.ActualWidth:F2}, Viewport={scroller.ViewportWidth:F2} DIPs; unused after Action before={beforeGap:F2}, after={afterGap:F2}; Net width before={beforeNetWidth:F2}, after={header.ColumnDefinitions[3].ActualWidth:F2}; Account={after:F2}; Columns={string.Join(", ", header.ColumnDefinitions.Select(c => c.ActualWidth.ToString("F2")))}";
                var page = (ScrollViewer)content.FindName("DayContentScroller");
                page.ScrollToVerticalOffset(page.VerticalOffset + header.TransformToAncestor(page).Transform(new Point()).Y - 12);
                Pump(); window.UpdateLayout();
                Render(content, $"account-{(longName ? "long" : "short")}-{theme}", width, dpi);
                if (width < 700)
                {
                    scroller.ScrollToRightEnd(); Pump(); window.UpdateLayout();
                    foreach (Grid row in rows)
                    {
                        var action = row.Children.OfType<Button>().Single(b => Grid.GetColumn(b) == 8);
                        double x = action.TransformToAncestor(scroller).Transform(new Point()).X;
                        Assert.True(x >= 0 && x + action.ActualWidth <= scroller.ViewportWidth + 1);
                    }
                    Render(content, $"account-{(longName ? "long" : "short")}-right-{theme}", width, dpi);
                    scroller.ScrollToLeftEnd(); Pump(); window.UpdateLayout();
                }
                if (Environment.GetEnvironmentVariable("PTJ_CALENDAR_RENDER_DIRECTORY") is { Length: > 0 } output)
                    File.WriteAllText(System.IO.Path.Combine(output, $"calendar-account-{theme}-{width}-{dpi}-{longName}.txt"),
                        measurements);
            }
            finally { window.Close(); }
        });
    }

    [Theory]
    [InlineData("Light", 1100, 96)]
    [InlineData("Dark", 1100, 96)]
    [InlineData("Light", 480, 240)]
    [InlineData("Dark", 480, 240)]
    public async Task ExpandedTradeUsesSharedEditorAndPreservesModalAtNarrowWidths(string theme, int width, int dpi)
    {
        var row = CalendarDayDetailsTests.Row(new(2026, 9, 5), 200, 197);
        var detail = TradesViewModelTests.CreateEditableTradeDetail(row, null);
        var details = new FakeTradeDetailReader(); details.EnqueueResult(detail); details.EnqueueResult(detail);
        var editor = TradesViewModelTests.CreateViewModel(tradeDetailReader: details,
            reader: TradesViewModelTests.CreateEditReferenceReader(detail));
        var reader = new FakeTradingCalendarDayReader { Handler = (q, ct) => Task.FromResult(TradingCalendarDayDetails.Create(q.Date, [row])) };
        var vm = await CalendarSummaryFixture.CreateAsync(reader, editor);
        await vm.SelectDayCommand.ExecuteAsync(CalendarDayDetailsTests.Cell(vm, new(2026, 9, 5)));
        await vm.ViewTradeCommand.ExecuteAsync(row);
        await editor.ShowSelectedTradeEditCommand.ExecuteAsync(null);
        await OnSta(() =>
        {
            var content = new CalendarDayDetailsView { DataContext = vm, Resources = CalendarViewLayoutTests.SharedThemeResources(theme) };
            var window = new CalendarDayDialogWindow { DataContext = vm, Content = content, Width = width, Height = 760, ShowInTaskbar = false };
            try
            {
                window.Show(); Pump(); window.UpdateLayout();
                var inline = Assert.Single(Descendants(content).OfType<CalendarInlineTradeView>());
                Assert.Same(editor, inline.DataContext);
                Assert.True(inline.IsVisible);
                Assert.Contains(Descendants(content).OfType<TextBlock>(), t => t.Text == "Time (New York)");
                Assert.Contains(Descendants(content).OfType<TextBlock>(), t => t.Text == "Profit");
                Assert.Contains(Descendants(content).OfType<TextBlock>(), t => t.Text == "Time");
                Button save = Assert.Single(Descendants(inline).OfType<Button>(), b => Equals(b.Content, "Save Changes"));
                Assert.Same(editor.SaveTradeEditCommand, save.Command);
                Assert.True(save.IsVisible);
                Button cancel = Assert.Single(Descendants(inline).OfType<Button>(), b => Equals(b.Content, "Cancel") && b.IsVisible);
                Assert.Same(editor.CancelTradeEditCommand, cancel.Command);
                Assert.False(vm.TryCloseDayDialog());
                Assert.True(window.IsVisible);
                inline.BringIntoView(); Pump(); window.UpdateLayout();
                Assert.True(inline.ActualWidth >= 700);
                var tableScroller = Descendants(content).OfType<ScrollViewer>().First(s => s.HorizontalScrollBarVisibility == ScrollBarVisibility.Auto && s.ExtentWidth >= 1040);
                if (width < 700)
                {
                    Assert.True(tableScroller.ScrollableWidth > 0);
                    tableScroller.ScrollToRightEnd(); Pump(); window.UpdateLayout();
                    Assert.Equal(tableScroller.ScrollableWidth, tableScroller.HorizontalOffset, 1);
                    tableScroller.ScrollToLeftEnd(); Pump(); window.UpdateLayout();
                    Assert.Equal(0, tableScroller.HorizontalOffset);
                }
                Render(content, "inline-" + theme, width, dpi);
                cancel.Command.Execute(null);
                Assert.False(editor.IsTradeEditVisible);
                Assert.True(vm.TryCloseDayDialog());
            }
            finally
            {
                if (editor.CancelTradeEditCommand.CanExecute(null)) editor.CancelTradeEditCommand.Execute(null);
                window.Close();
            }
        });
    }

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
    public async Task ViewExpandsExactTradeWhileKeepingModalOpen(string theme)
    {
        var reader = new FakeTradingCalendarDayReader { Handler = (q, ct) => Task.FromResult(TradingCalendarDayDetails.Create(q.Date, [CalendarDayDetailsTests.Row(q.Date, 7m, 7m)], ct)) };
        var vm = await CalendarSummaryFixture.CreateAsync(reader);
        await OnSta(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            var view = new CalendarView { DataContext = vm, Resources = CalendarViewLayoutTests.SharedThemeResources(theme) };
            var owner = new Window { Content = view, Width = 900, Height = 700, ShowInTaskbar = false };
            Exception? failure = null;
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
                        Pump();
                        Assert.Same(dialog, view.DayDialog);
                        Assert.True(vm.DayTrades.Single().IsExpanded);
                        dialog.Close();
                    }
                    catch (Exception exception) { failure = exception; view.DayDialog?.Close(); }
                }));
                CalendarDayHost cell = Assert.Single(Descendants(view).OfType<CalendarDayHost>(), c => ((CalendarDayCell)c.DataContext).Date == new DateOnly(2026, 9, 5));
                cell.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.MouseUpEvent });
                Pump();
                if (failure is not null) throw failure;
                Assert.Null(view.DayDialog);
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
    internal static Task OnSta(Action action)
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
        start.ArgumentList.Add("/TestCaseFilter:FullyQualifiedName~CalendarDayModalTests|FullyQualifiedName~CalendarDayPerformanceChartTests");
        start.Environment["PTJ_CALENDAR_MODAL_TEST_HOST"] = "1";
        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync(), error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(2));
        Assert.True(process.ExitCode == 0, $"Isolated native modal tests failed:\n{await output}\n{await error}");
    }
}
