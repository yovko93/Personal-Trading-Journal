using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml.Linq;
using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Dashboard;
using PersonalTradingJournal.Desktop.Views.Dashboard;

namespace PersonalTradingJournal.Desktop.Tests.Dashboard;

public sealed class DashboardRangeAndAverageTests
{
    [Fact]
    public async Task RenderedAverageCardKeepsHeadingBarRatioAndAmountsInOrderAtNarrowWidth()
    {
        await OnSta(() =>
        {
            DirectoryInfo? repo = new(AppContext.BaseDirectory);
            while (repo is not null && !File.Exists(System.IO.Path.Combine(repo.FullName, "PersonalTradingJournal.sln"))) repo = repo.Parent;
            Assert.NotNull(repo);
            XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            var viewSource = XDocument.Load(System.IO.Path.Combine(repo.FullName,
                "src/PersonalTradingJournal.Desktop/Views/Dashboard/DashboardView.xaml"));
            var template = Assert.Single(viewSource.Descendants(p + "DataTemplate"), t =>
                (string?)t.Attribute(x + "Key") == "AverageWinLossTemplate");
            string gridXaml = Assert.Single(template.Elements(p + "Grid")).ToString(SaveOptions.DisableFormatting)
                .Replace("clr-namespace:PersonalTradingJournal.Desktop.Views.Dashboard\"",
                    "clr-namespace:PersonalTradingJournal.Desktop.Views.Dashboard;assembly=PersonalTradingJournal.Desktop\"", StringComparison.Ordinal);
            var source = Assert.Single(DashboardMetricCalculator.Calculate([
                DashboardViewModelTests.Fact(100m), DashboardViewModelTests.Fact(20m),
                DashboardViewModelTests.Fact(-40m)]).Currencies);
            DashboardCard averageCard = Assert.Single(new DashboardCurrencyPresentation(source).Cards,
                c => c.Label == "Avg Win / Avg Loss");
            foreach (var (theme, dpi) in new[] { ("Dark", 240), ("Light", 96) })
            {
                var card = new Border { Width = 280, Padding = new Thickness(16), Resources = Resources(theme) };
                card.Resources.MergedDictionaries.Add((ResourceDictionary)System.Windows.Application.LoadComponent(
                    new Uri("/PersonalTradingJournal.Desktop;component/Resources/Typography.xaml", UriKind.Relative)));
                var heading = new TextBlock { Text = averageCard.Label,
                    Style = (Style)card.Resources["PtjSecondaryTextStyle"] };
                var content = (Grid)XamlReader.Parse(gridXaml);
                content.DataContext = averageCard.Averages;
                var stack = new StackPanel();
                stack.Children.Add(heading);
                stack.Children.Add(content);
                card.Child = stack;
                Draw(card, 280, 280, dpi);
                var text = Descendants(card).OfType<TextBlock>().ToArray();
                var ratio = Assert.Single(text, t => t.Text == "1.50" && t.ActualWidth > 0);
                var win = Assert.Single(text, t => AutomationProperties.GetName(t).StartsWith("Average Win:", StringComparison.Ordinal));
                var loss = Assert.Single(text, t => AutomationProperties.GetName(t).StartsWith("Average Loss:", StringComparison.Ordinal));
                var bar = Assert.Single(Descendants(card).OfType<AverageComparisonBar>());
                static Rect Bounds(FrameworkElement element, Border parent) =>
                    element.TransformToAncestor(parent).TransformBounds(new Rect(element.RenderSize));
                Rect headingBounds = Bounds(heading, card), barBounds = Bounds(bar, card), ratioBounds = Bounds(ratio, card);
                Rect winBounds = Bounds(win, card), lossBounds = Bounds(loss, card);
                Assert.True(headingBounds.Bottom < barBounds.Top);
                Assert.True(barBounds.Bottom < ratioBounds.Top);
                Assert.True(ratioBounds.Bottom < Math.Min(winBounds.Top, lossBounds.Top));
                Assert.Equal(barBounds.Left + barBounds.Width / 2, ratioBounds.Left + ratioBounds.Width / 2, 1);
                Assert.True(winBounds.Right <= lossBounds.Left);
                Assert.True(lossBounds.Right <= card.ActualWidth);
            }
        });
    }

    [Theory]
    [InlineData("2026-01-29", RangeDay.None)]
    [InlineData("2026-01-30", RangeDay.Start)]
    [InlineData("2026-01-31", RangeDay.Interior)]
    [InlineData("2026-02-01", RangeDay.Interior)]
    [InlineData("2026-02-02", RangeDay.End)]
    [InlineData("2026-02-03", RangeDay.None)]
    public void InclusiveRangeClassificationCrossesMonthBoundaries(string day, RangeDay expected) =>
        Assert.Equal(expected, CalendarRange.Classify(DateTime.Parse(day), new(2026, 1, 30), new(2026, 2, 2)));

    [Fact]
    public void SinglePartialAndReversedRangesNeverInventInteriorDays()
    {
        DateTime date = new(2026, 1, 30);
        Assert.Equal(RangeDay.Single, CalendarRange.Classify(date, date, date));
        Assert.Equal(RangeDay.Partial, CalendarRange.Classify(date, date, null));
        Assert.Equal(RangeDay.Partial, CalendarRange.Classify(date, null, date));
        Assert.Equal(RangeDay.None, CalendarRange.Classify(date.AddDays(1), date, null));
        Assert.Equal(RangeDay.Partial, CalendarRange.Classify(date, date, date.AddDays(-2)));
        Assert.Equal(RangeDay.None, CalendarRange.Classify(date.AddDays(-1), date, date.AddDays(-2)));
        Assert.Equal(RangeDay.Interior, CalendarRange.Classify(new(2027, 1, 1), new(2026, 12, 31), new(2027, 1, 2)));
    }

    [Fact]
    public async Task BoxAlwaysDescribesAppliedRangeWhileCancelAndCloseRestoreItWithoutQueries()
    {
        var reader = new FakeDashboardAnalyticsReader { Read = (_, _) => Task.FromResult(DashboardMetricCalculator.Calculate([])) };
        var vm = new DashboardViewModel(reader, TimeProvider.System, new FakeTradeListReader(), new FakeTradingAccountReader());
        await vm.RefreshAsync();
        Assert.Equal("All history", vm.AppliedRangeLabel);
        vm.ToggleRangeCommand.Execute(null);
        vm.StartDate = new(2026, 1, 30);
        Assert.True(vm.IsRangeDraft);
        Assert.Contains("Draft", vm.RangeSelectionStatus);
        Assert.Equal("All history", vm.AppliedRangeLabel);
        Assert.False(vm.ApplyRangeCommand.CanExecute(null));
        vm.EndDate = new(2026, 2, 2);
        vm.ApplyRangeCommand.Execute(null);
        await vm.LoadTask;
        Assert.False(vm.IsDateRangeOpen);
        Assert.False(vm.IsRangeDraft);
        Assert.Equal("2026-01-30 – 2026-02-02", vm.AppliedRangeLabel);
        int reads = reader.Queries.Count;
        vm.ToggleRangeCommand.Execute(null);
        vm.StartDate = new(2026, 1, 31);
        Assert.True(vm.IsRangeDraft);
        Assert.Equal("2026-01-30 – 2026-02-02", vm.AppliedRangeLabel);
        vm.CancelRangeCommand.Execute(null);
        Assert.Equal(new DateTime(2026, 1, 30), vm.StartDate);
        Assert.False(vm.IsRangeDraft);
        Assert.Equal(reads, reader.Queries.Count);
        vm.ToggleRangeCommand.Execute(null);
        vm.EndDate = new(2026, 2, 3);
        vm.ToggleRangeCommand.Execute(null); // Closing via the always-visible box also cancels.
        Assert.False(vm.IsDateRangeOpen);
        Assert.False(vm.IsRangeDraft);
        Assert.Equal(new DateTime(2026, 2, 2), vm.EndDate);
        Assert.Equal(reads, reader.Queries.Count);
    }

    [Fact]
    public void CombinedCardUsesAveragesNotTotalsAndPreservesPositiveDomainLossMagnitude()
    {
        var source = Assert.Single(DashboardMetricCalculator.Calculate(
            new[] { 100m, 20m, -40m, 0m }.Select(v => DashboardViewModelTests.Fact(v))).Currencies);
        var cards = new DashboardCurrencyPresentation(source).Cards;
        Assert.DoesNotContain(cards, c => c.Label is "Average Win" or "Average Loss");
        var averages = Assert.Single(cards, c => c.Label == "Avg Win / Avg Loss").Averages!;
        Assert.Equal(60m, averages.Win);
        Assert.Equal(40m, averages.LossMagnitude);
        Assert.Equal(1.5m, averages.Ratio);
        Assert.Equal(0.6m, averages.WinShare);
        Assert.Equal(3m, source.Metrics.EffectiveNet.ProfitFactor.Value); // Ratio is not Profit Factor.
        Assert.Equal(DashboardCurrencyPresentation.Money(-40m, "USD"), averages.LossText);
        Assert.Equal(40m, source.Metrics.EffectiveNet.AverageLoss.Value);
        Assert.Equal("USD", averages.Currency);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(-20)]
    [InlineData(0)]
    public void OneSidedAndBreakEvenPopulationsKeepUndefinedRatioNeutral(int value)
    {
        var metrics = Assert.Single(DashboardMetricCalculator.Calculate([DashboardViewModelTests.Fact(value)]).Currencies).Metrics.EffectiveNet;
        var averages = new AverageWinLossPresentation(metrics, "USD");
        Assert.Null(averages.Ratio);
        Assert.Equal("N/A", averages.RatioText);
        Assert.False(averages.HasComparison);
        Assert.Equal(0m, averages.WinShare);
        Assert.Equal(value > 0 ? DashboardCurrencyPresentation.Money(value, "USD") : "N/A", averages.WinText);
        Assert.Equal(value < 0 ? DashboardCurrencyPresentation.Money(value, "USD") : "N/A", averages.LossText);
    }

    [Fact]
    public void EmptyIncompleteEstimatedCurrencyAndOverflowStatesRemainExplicit()
    {
        var empty = new AverageWinLossPresentation(null, "");
        Assert.Equal("N/A", empty.WinText);
        Assert.Equal("N/A", empty.LossText);
        Assert.Null(empty.Ratio);
        var incomplete = Assert.Single(DashboardMetricCalculator.Calculate([
            DashboardViewModelTests.Fact(100m), DashboardViewModelTests.Fact(-40m), DashboardViewModelTests.Fact(null, null)]).Currencies).Metrics.EffectiveNet;
        var missing = new AverageWinLossPresentation(incomplete, "USD");
        Assert.Equal("N/A", missing.WinText);
        Assert.Equal("N/A", missing.LossText);
        Assert.Null(missing.Ratio);
        var known = Assert.Single(DashboardMetricCalculator.Calculate([
            DashboardViewModelTests.Fact(100m) with { Currency = "EUR" },
            DashboardViewModelTests.Fact(-285m, null) with { Currency = "EUR" }]).Currencies).Metrics.EffectiveNet;
        var estimated = new AverageWinLossPresentation(known, "EUR");
        Assert.Contains("Estimated", estimated.Description);
        Assert.Contains("EUR", estimated.WinText);
        Assert.Equal(100m / 285m, estimated.Ratio);
        Assert.Null(new AverageWinLossPresentation(known with { KnownLossMagnitude = 0m }, "EUR").Ratio);
        Assert.Null(new AverageWinLossPresentation(known with { KnownProfitSum = decimal.MaxValue, KnownLossMagnitude = 0.01m }, "EUR").Ratio);
        Assert.Equal(0.5m, new AverageWinLossPresentation(known with { KnownProfitSum = decimal.MaxValue, KnownLossMagnitude = decimal.MaxValue }, "EUR").WinShare);
    }

    [Theory]
    [InlineData("Light", 96)]
    [InlineData("Dark", 240)]
    public async Task ActualRangeTemplatesShadeBothMonthsEndpointsDraftAndPartialWithoutSelectingEveryDate(string theme, int dpi)
    {
        await OnSta(() =>
        {
            var resources = Resources(theme);
            DateTime first = new(2026, 1, 30), last = new(2026, 2, 2);
            foreach (var month in new[] { new DateTime(2026, 1, 1), new DateTime(2026, 2, 1) })
            {
                var calendar = new Calendar { Resources = resources, Style = (Style)resources["RangeCalendar"], DisplayDate = month, Width = 240 };
                CalendarRange.SetStart(calendar, first); CalendarRange.SetEnd(calendar, last);
                Draw(calendar, 240, 280, dpi);
                var days = Descendants(calendar).OfType<CalendarDayButton>().ToArray();
                foreach (var day in days)
                {
                    var kind = CalendarRange.Classify((DateTime)day.DataContext, first, last);
                    Assert.Equal(kind, day.Tag);
                    var band = (Border)day.Template.FindName("RangeBand", day);
                    Assert.Equal(kind == RangeDay.Interior ? Visibility.Visible : Visibility.Collapsed, band.Visibility);
                    if (kind is RangeDay.Start or RangeDay.End)
                    {
                        var endpoint = (Border)day.Template.FindName("Day", day);
                        Assert.Equal(Color((Brush)resources["PtjAccentBrush"]), Color(endpoint.Background));
                        Assert.Equal(kind == RangeDay.Start ? new CornerRadius(6, 0, 0, 6) : new CornerRadius(0, 6, 6, 0), endpoint.CornerRadius);
                    }
                }
                Assert.Empty(calendar.SelectedDates); // Highlighting never fabricates native selections or updates endpoints.
                var interior = days.First(d => Equals(d.Tag, RangeDay.Interior));
                var interiorBand = (Border)interior.Template.FindName("RangeBand", interior);
                double appliedOpacity = interiorBand.Opacity;
                CalendarRange.SetIsDraft(calendar, true);
                Draw(calendar, 240, 280, dpi);
                Assert.True(interiorBand.Opacity < appliedOpacity);
                CalendarRange.SetEnd(calendar, null);
                Draw(calendar, 240, 280, dpi);
                Assert.DoesNotContain(days, d => Equals(d.Tag, RangeDay.Interior));
                var start = days.Single(d => (DateTime)d.DataContext == first);
                Assert.Equal(RangeDay.Partial, start.Tag);
                Assert.Equal(new Thickness(2), ((Border)start.Template.FindName("Day", start)).BorderThickness);
                // Deterministic focus-trigger check on the actual template (no desktop input injection).
                Assert.True(start.Focusable);
                var focusTrigger = Assert.Single(start.Template.Triggers.OfType<Trigger>(), t => t.Property == UIElement.IsKeyboardFocusedProperty);
                var setter = Assert.Single(focusTrigger.Setters.OfType<Setter>());
                Assert.Equal("KeyboardFocus", setter.TargetName);
                Assert.Equal(Visibility.Visible, setter.Value);
                var outline = (Rectangle)start.Template.FindName("KeyboardFocus", start);
                Assert.Equal(new[] { 2d, 1d }, outline.StrokeDashArray);
                Assert.Equal(Color((Brush)resources["PtjTextPrimaryBrush"]), Color(outline.Stroke));
            }
            var comparison = new AverageComparisonBar { Resources = resources };
            comparison.SetResourceReference(AverageComparisonBar.WinBrushProperty, "PtjSuccessBrush");
            comparison.SetResourceReference(AverageComparisonBar.LossBrushProperty, "PtjDangerBrush");
            comparison.SetResourceReference(AverageComparisonBar.NeutralBrushProperty, "PtjTextMutedBrush");
            foreach (int width in new[] { 162, 88 })
            {
                foreach (var (values, key) in new (decimal[] Values, string BrushKey)[]
                {
                    ([100m, 20m, -40m], "PtjSuccessBrush"),
                    ([20m, -40m], "PtjDangerBrush"),
                    ([40m, -40m], "PtjTextMutedBrush"),
                    ([-40m], "PtjTextMutedBrush")
                })
                {
                    var metrics = Assert.Single(DashboardMetricCalculator.Calculate(values.Select(v => DashboardViewModelTests.Fact(v))).Currencies).Metrics.EffectiveNet;
                    comparison.Value = new(metrics, "USD");
                    Draw(comparison, width, 10, dpi);
                    var bar = Assert.Single(VisualTreeHelper.GetDrawing(comparison).Children.OfType<GeometryDrawing>());
                    Assert.Equal(width, bar.Geometry.Bounds.Width, 5);
                    Assert.Equal(Color((Brush)resources[key]), Color(bar.Brush));
                }
                comparison.Value = new(null, "");
                Draw(comparison, width, 10, dpi);
                var unavailable = Assert.Single(VisualTreeHelper.GetDrawing(comparison).Children.OfType<GeometryDrawing>());
                Assert.Equal(width, unavailable.Geometry.Bounds.Width, 5);
                Assert.Equal(Color((Brush)resources["PtjTextMutedBrush"]), Color(unavailable.Brush));
            }
        });
    }

    private static ResourceDictionary Resources(string theme)
    {
        var resources = new ResourceDictionary();
        foreach (string path in new[] { $"Resources/Themes/{theme}Theme.xaml", "Views/Dashboard/DashboardCalendarResources.xaml" })
            resources.MergedDictionaries.Add((ResourceDictionary)System.Windows.Application.LoadComponent(new Uri($"/PersonalTradingJournal.Desktop;component/{path}", UriKind.Relative)));
        return resources;
    }
    private static Color? Color(Brush? brush) => (brush as SolidColorBrush)?.Color;
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        { var child = VisualTreeHelper.GetChild(parent, i); yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
    private static void Draw(FrameworkElement element, int width, int height, int dpi)
    {
        element.Measure(new(width, height)); element.Arrange(new(0, 0, width, height)); element.UpdateLayout();
        new RenderTargetBitmap(width * dpi / 96, height * dpi / 96, dpi, dpi, PixelFormats.Pbgra32).Render(element);
    }
    private static Task OnSta(Action action)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { try { action(); done.SetResult(); } catch (Exception e) { done.SetException(e); } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return done.Task;
    }
}
