using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Dashboard;
using PersonalTradingJournal.Desktop.Views.Dashboard;

namespace PersonalTradingJournal.Desktop.Tests.Dashboard;

public sealed class DashboardRefinementTests
{
    [Fact]
    public async Task EmptyScopeKeepsCardOrderAndClearsOldDaysWithoutInventingCurrencyOrEconomics()
    {
        var reader = new FakeDashboardAnalyticsReader { Read = (_, _) => Task.FromResult(Snapshot(100m, -40m, 0m, 20m)) };
        var vm = Create(reader);
        await vm.RefreshAsync();
        var labels = vm.Selected!.Cards.Select(c => c.Label).ToArray();
        Assert.NotNull(vm.Selected.BestDay);
        reader.Read = (_, _) => Task.FromResult(DashboardMetricCalculator.Calculate([]));
        vm.TodayCommand.Execute(null);
        await vm.LoadTask;
        Assert.True(vm.IsEmpty);
        Assert.NotNull(vm.Selected);
        Assert.Null(vm.Selected.Source); // No fabricated USD bucket, prices, or calculator result.
        Assert.Empty(vm.Currencies);
        Assert.Equal(labels, vm.Selected.Cards.Select(c => c.Label));
        Assert.Equal(DashboardCurrencyPresentation.Money(0m, ""), Card(vm.Selected, "Net P&L").Value);
        Assert.Equal("0", Card(vm.Selected, "Total Trades").Value);
        var win = Card(vm.Selected, "Win Rate").Ring!;
        Assert.Equal("0%", win.Value);
        Assert.False(win.IsAvailable);
        Assert.Equal("0 wins", win.WinsText);
        Assert.Equal("0 losses", win.LossesText);
        Assert.Equal(0m, win.WinsShare + win.LossesShare + win.BreakEvenShare);
        foreach (string label in new[] { "Profit Factor", "Avg Win / Avg Loss", "Best Day", "Worst Day" })
        {
            Assert.Equal("N/A", Card(vm.Selected, label).Value);
            Assert.Equal("", Card(vm.Selected, label).Date);
        }
        Assert.Null(vm.Selected.BestDay);
        Assert.Null(vm.Selected.WorstDay);
        Assert.Empty(vm.Selected.DailyPnl);
        Assert.Empty(vm.Selected.CumulativePnl);
        Assert.True(vm.Selected.HasNoSetups);
    }

    [Theory]
    [InlineData(100, -40, 120, 40, 3d, 0.75)]
    [InlineData(0, 0, 20, 0, null, 1)]
    [InlineData(-20, -40, 20, 60, 0.3333333333333333, 0.25)]
    public void DonutReusesProfitAndAbsoluteLossFromTheSameEffectiveNetMetric(int first, int second, int profit, int loss, double? factor, double share)
    {
        var source = Assert.Single(Snapshot(first, second, 0m, 20m).Currencies);
        var card = Card(new(source), "Profit Factor");
        var ring = card.Factor!;
        Assert.Equal(profit, ring.Profit);
        Assert.Equal(loss, ring.Loss);
        Assert.Equal((decimal)share, ring.WinsShare);
        Assert.Equal(1m, ring.WinsShare + ring.LossesShare);
        Assert.Contains("USD", ring.ProfitText);
        Assert.Contains("USD", ring.LossText);
        Assert.Equal(source.Metrics.EffectiveNet.ProfitFactor.Value, ring.Metrics!.ProfitFactor.Value);
        if (factor is null) Assert.Equal("N/A", card.Value);
        else Assert.Equal((double)source.Metrics.EffectiveNet.ProfitFactor.Value!.Value, factor.Value, 12);
    }

    [Fact]
    public void CountsExplainPercentageAndMonetaryDonutIsNotAWinCountDonut()
    {
        var view = new DashboardCurrencyPresentation(Assert.Single(Snapshot(100m, -40m, 0m, 20m).Currencies));
        var win = Card(view, "Win Rate").Ring!;
        Assert.Equal("2 wins", win.WinsText);
        Assert.Equal("1 losses", win.LossesText);
        Assert.Equal("1 break-even", win.BreakEvensText);
        Assert.True(win.HasBreakEvens);
        Assert.Equal(50m, win.Metrics!.WinRatePercent);
        Assert.Equal(0.5m, win.WinsShare);
        Assert.Equal(0.25m, win.BreakEvenShare);
        Assert.Equal(0.75m, Card(view, "Profit Factor").Factor!.WinsShare);
    }

    [Fact]
    public void IncompleteEconomicsStayUnavailableAndEstimatesRetainProvenance()
    {
        var source = Assert.Single(DashboardMetricCalculator.Calculate([
            DashboardViewModelTests.Fact(100m), DashboardViewModelTests.Fact(null, null)]).Currencies);
        var view = new DashboardCurrencyPresentation(source);
        Assert.Equal("—", Card(view, "Net P&L").Value);
        var factor = Card(view, "Profit Factor").Factor!;
        Assert.False(factor.IsAvailable);
        Assert.Null(factor.Profit);
        Assert.Null(factor.Loss);
        Assert.Equal("N/A", factor.ProfitText);
        Assert.Equal("N/A", factor.Value);
        Assert.Equal(0m, factor.WinsShare + factor.LossesShare);
        var win = Card(view, "Win Rate").Ring!;
        Assert.Equal("N/A", win.Value);
        Assert.Equal("1 known wins", win.WinsText);
        source = Assert.Single(DashboardMetricCalculator.Calculate([
            DashboardViewModelTests.Fact(100m), DashboardViewModelTests.Fact(-285m, null)]).Currencies);
        factor = Card(new(source), "Profit Factor").Factor!;
        Assert.Equal(100m, factor.Profit);
        Assert.Equal(285m, factor.Loss);
        Assert.Contains("Estimated", factor.Description);
        Assert.Null(source.Metrics.Net.Total);
        Assert.Equal(source.Metrics.EffectiveNet.ProfitFactor.Value, factor.Profit / factor.Loss);
    }

    [Theory]
    [InlineData(0, false, 0)]
    [InlineData(-10, true, 0)]
    [InlineData(10, true, 1)]
    public void ZeroAndOneSidedPopulationsDoNotInventInfinityOrProportions(int pnl, bool available, int positiveShare)
    {
        var factor = Card(new(Assert.Single(Snapshot(pnl).Currencies)), "Profit Factor").Factor!;
        Assert.Equal(available, factor.IsAvailable);
        Assert.Equal(positiveShare, factor.WinsShare);
        Assert.Equal(pnl < 0 ? 1m : 0m, factor.LossesShare);
        Assert.Equal(pnl < 0 ? "0.00".Replace(".", System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator) : "N/A", factor.Value);
    }

    [Fact]
    public void HugeValidSumsCannotOverflowTheVisualDenominator()
    {
        PnlMetrics metrics = Assert.Single(Snapshot(1m, -1m).Currencies).Metrics.EffectiveNet with
            { KnownProfitSum = decimal.MaxValue, KnownLossMagnitude = decimal.MaxValue };
        var factor = new ProfitFactorPresentation(metrics, "EUR");
        Assert.Equal(0.5m, factor.WinsShare);
        Assert.Equal(0.5m, factor.LossesShare);
        Assert.Contains("EUR", factor.ProfitText);
    }

    [Fact]
    public async Task UnifiedRangeKeepsDraftSeparateCancelsAndAppliesOnlyCompleteValidDates()
    {
        var reader = new FakeDashboardAnalyticsReader { Read = (_, _) => Task.FromResult(Snapshot(25m)) };
        var vm = Create(reader);
        await vm.ActivateAsync();
        vm.IsDateRangeOpen = true;
        Assert.Equal(vm.StartMonth.AddMonths(1), vm.EndMonth);
        vm.StartDate = new(2026, 3, 8);
        Assert.False(vm.ApplyRangeCommand.CanExecute(null));
        Assert.Equal("All history", vm.PeriodLabel);
        Assert.Single(reader.Queries);
        vm.EndDate = new(2026, 3, 8);
        vm.CancelRangeCommand.Execute(null);
        Assert.False(vm.IsDateRangeOpen);
        Assert.Null(vm.StartDate);
        Assert.Single(reader.Queries);
        vm.IsDateRangeOpen = true;
        vm.StartDate = vm.EndDate = new DateTime(2026, 3, 8);
        vm.ApplyRangeCommand.Execute(null);
        await vm.LoadTask;
        Assert.False(vm.IsDateRangeOpen);
        Assert.Equal(DashboardPeriod.Custom, vm.Period);
        Assert.Equal(23d, (vm.Query.ClosedBeforeUtc - vm.Query.ClosedFromUtc)!.Value.TotalHours);
        Assert.False(vm.PreviousCommand.CanExecute(null));
        vm.IsDateRangeOpen = true;
        vm.EndDate = vm.StartDate!.Value.AddDays(-1);
        Assert.False(vm.ApplyRangeCommand.CanExecute(null));
        Assert.Equal(new DateOnly(2026, 3, 8), vm.Query.ClosedThroughNewYork);
        vm.CancelRangeCommand.Execute(null);
        Assert.Equal(new DateTime(2026, 3, 8), vm.EndDate);
    }

    [Theory]
    [InlineData("Light", 96)]
    [InlineData("Dark", 240)]
    public async Task CalendarsAndBothDonutsRenderWithThemeResourcesAndKeyboardFocusableNativeDays(string theme, int dpi)
    {
        await OnSta(() =>
        {
            var resources = new ResourceDictionary();
            resources.MergedDictionaries.Add(Load($"Resources/Themes/{theme}Theme.xaml"));
            resources.MergedDictionaries.Add(Load("Views/Dashboard/DashboardCalendarResources.xaml"));
            var calendar = new Calendar { Resources = resources, Style = (Style)resources["RangeCalendar"],
                DisplayDate = new(2026, 3, 1), SelectedDate = new(2026, 3, 8), Width = 240 };
            CalendarRange.SetStart(calendar, new(2026, 3, 8));
            CalendarRange.SetEnd(calendar, new(2026, 3, 8));
            Draw(calendar, 240, 280, dpi);
            var days = Descendants(calendar).OfType<CalendarDayButton>().ToArray();
            Assert.Equal(42, days.Length);
            var selected = Assert.Single(days, d => d.IsSelected);
            Assert.True(selected.Focusable);
            Assert.Equal(BrushColor((Brush)resources["PtjOnAccentBrush"]), BrushColor(selected.Foreground));
            Assert.Contains(Descendants(selected).OfType<Border>(), b => BrushColor(b.Background) == BrushColor((Brush)resources["PtjAccentBrush"]));
            Assert.Contains(days, d => !d.IsSelected && BrushColor(d.Foreground) == BrushColor((Brush)resources["PtjTextPrimaryBrush"]));
            calendar.DisplayDate = new(2026, 4, 1);
            Draw(calendar, 240, 280, dpi);
            Assert.Equal(4, calendar.DisplayDate.Month);
            calendar.DisplayMode = CalendarMode.Year;
            Draw(calendar, 240, 280, dpi);
            // Off-screen WPF render tests have no PresentationSource: IsVisible is false even
            // for laid-out controls. Verify the actual template mode instead.
            CalendarItem item = Assert.Single(Descendants(calendar).OfType<CalendarItem>());
            Assert.Equal(Visibility.Visible, ((Grid)item.Template.FindName("PART_YearView", item)).Visibility);
            Assert.Equal(Visibility.Hidden, ((Grid)item.Template.FindName("PART_MonthView", item)).Visibility);
            Assert.Equal(12, Descendants(calendar).OfType<CalendarButton>().Count());
            // Exercise the same native two-way selection bindings used by the unified control.
            var reader = new FakeDashboardAnalyticsReader();
            var vm = Create(reader);
            vm.IsDateRangeOpen = true;
            calendar.DisplayMode = CalendarMode.Month;
            calendar.SetBinding(Calendar.SelectedDateProperty, new Binding(nameof(vm.StartDate)) { Source = vm });
            var end = new Calendar { Resources = resources, Style = (Style)resources["RangeCalendar"], Width = 240 };
            end.SetBinding(Calendar.SelectedDateProperty, new Binding(nameof(vm.EndDate)) { Source = vm });
            calendar.SetCurrentValue(Calendar.SelectedDateProperty, new DateTime(2026, 3, 8));
            Assert.Equal(new DateTime(2026, 3, 8), vm.StartDate);
            Assert.False(vm.ApplyRangeCommand.CanExecute(null));
            end.SetCurrentValue(Calendar.SelectedDateProperty, new DateTime(2026, 3, 9));
            Assert.True(vm.ApplyRangeCommand.CanExecute(null));
            Assert.Equal("All history", vm.PeriodLabel);
            Assert.Empty(reader.Queries); // Selection is presentation-only until explicit Apply.
            var pair = new WrapPanel();
            calendar.Margin = new Thickness(0, 0, 16, 0);
            pair.Children.Add(calendar); pair.Children.Add(end);
            Draw(pair, 510, 600, dpi);
            Assert.Equal(0d, VisualTreeHelper.GetOffset(end).Y);
            Draw(pair, 300, 600, dpi);
            Assert.True(VisualTreeHelper.GetOffset(end).Y >= calendar.ActualHeight);
            Assert.True(end.ActualWidth <= 300);
            var metrics = Assert.Single(Snapshot(100m, -40m, 0m, 20m).Currencies).Metrics.EffectiveNet;
            foreach (IOutcomeRingPresentation value in new IOutcomeRingPresentation[] { new WinRatePresentation(metrics), new ProfitFactorPresentation(metrics, "USD"), new WinRatePresentation(null), new ProfitFactorPresentation(null, "") })
            {
                var ring = new OutcomeRing { Resources = resources, Value = value, Width = 142, Height = 142 };
                ring.SetResourceReference(OutcomeRing.WinBrushProperty, "PtjSuccessBrush");
                ring.SetResourceReference(OutcomeRing.LossBrushProperty, "PtjDangerBrush");
                ring.SetResourceReference(OutcomeRing.NeutralBrushProperty, "PtjTextMutedBrush");
                Draw(ring, 142, 142, dpi);
                var drawing = VisualTreeHelper.GetDrawing(ring).Children.OfType<GeometryDrawing>().ToArray();
                Assert.Equal(BrushColor((Brush)resources["PtjTextMutedBrush"]), BrushColor(drawing[0].Pen.Brush));
                if (!value.IsAvailable) Assert.Single(drawing);
                else
                {
                    Assert.Contains(drawing, d => BrushColor(d.Pen.Brush) == BrushColor((Brush)resources["PtjSuccessBrush"]));
                    Assert.Contains(drawing, d => BrushColor(d.Pen.Brush) == BrushColor((Brush)resources["PtjDangerBrush"]));
                }
            }
        });
    }

    private static DashboardCard Card(DashboardCurrencyPresentation view, string name) => Assert.Single(view.Cards, c => c.Label == name);
    private static DashboardAnalyticsSnapshot Snapshot(params decimal[] values) => DashboardMetricCalculator.Calculate(values.Select(v => DashboardViewModelTests.Fact(v)));
    private static DashboardViewModel Create(FakeDashboardAnalyticsReader reader) => new(reader,
        new FixedTime(), new FakeTradeListReader(), new FakeTradingAccountReader());
    private sealed class FixedTime : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero); }
    private static Color? BrushColor(Brush? brush) => (brush as SolidColorBrush)?.Color;
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        { var child = VisualTreeHelper.GetChild(parent, i); yield return child; foreach (var descendant in Descendants(child)) yield return descendant; }
    }
    private static void Draw(FrameworkElement element, int width, int height, int dpi)
    {
        element.Measure(new(width, height)); element.Arrange(new(0, 0, width, height)); element.UpdateLayout();
        new RenderTargetBitmap(width * dpi / 96, height * dpi / 96, dpi, dpi, PixelFormats.Pbgra32).Render(element);
    }
    private static ResourceDictionary Load(string relative)
    {
        return (ResourceDictionary)System.Windows.Application.LoadComponent(
            new Uri($"/PersonalTradingJournal.Desktop;component/{relative}", UriKind.Relative));
    }
    private static Task OnSta(Action action)
    {
        var result = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { try { action(); result.SetResult(); } catch (Exception e) { result.SetException(e); } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return result.Task;
    }
}
