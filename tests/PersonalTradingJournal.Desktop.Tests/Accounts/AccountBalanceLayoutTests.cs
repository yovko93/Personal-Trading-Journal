using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Desktop.Interactions;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Views.Accounts;

namespace PersonalTradingJournal.Desktop.Tests.Accounts;

public sealed class AccountBalanceLayoutTests
{
    private static readonly Lazy<Task> Host = new(() => IsolatedTestProcess.RunSuiteAsync(typeof(AccountBalanceLayoutTests),
        "account-balance-layout", "PTJ_ACCOUNT_BALANCE_HOST", TimeSpan.FromMinutes(2), caseHangTimeout: TimeSpan.FromSeconds(30)));

    [Theory]
    [InlineData("Light", 1280, 96)]
    [InlineData("Dark", 1280, 96)]
    [InlineData("Light", 480, 240)]
    [InlineData("Dark", 480, 240)]
    public async Task BalancesAlignAfterStartingBalanceAndRemainReachableWithCoverageAndThemeColors(string theme, int width, int dpi)
    {
        if (Environment.GetEnvironmentVariable("PTJ_ACCOUNT_BALANCE_HOST") != "1") { await Host.Value; return; }
        var reader = new AccountBalancePresentationTests.Balances();
        reader.Reads.Enqueue(_ => Task.FromResult<IReadOnlyList<AccountListItem>>([
            AccountBalancePresentationTests.Item(1, true), AccountBalancePresentationTests.Item(-1, true), AccountBalancePresentationTests.Item(0, false)]));
        var vm = AccountBalancePresentationTests.Create(reader); await vm.EnsureLoadedAsync();
        await CalendarStaTest.RunAsync(() =>
        {
            var app = System.Windows.Application.Current ?? new() { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
            var view = new AccountsView { DataContext = vm };
            var root = new Border { Child = view }; root.SetResourceReference(Border.BackgroundProperty, "PtjBackgroundBrush");
            root.Measure(new(width, 700)); root.Arrange(new(0, 0, width, 700)); root.UpdateLayout();
            var elements = Descendants(root).ToArray();
            var scroll = Assert.IsType<ScrollViewer>(view.FindName("AccountTableScroll"));
            Assert.True(NestedTableWheelRouting.GetForwardVerticalWheel(scroll));
            var header = elements.OfType<TextBlock>().Single(t => t.Text == "Current Balance");
            var starting = Descendants(scroll).OfType<TextBlock>().Single(t => t.Text == "Starting Balance");
            Assert.Equal(Grid.GetColumn(starting) + 1, Grid.GetColumn(header));
            var values = elements.OfType<TextBlock>().Where(t => t.Name == "CurrentBalanceValue").ToArray();
            Assert.Equal(3, values.Length);
            string[] colors = ["PtjSuccessBrush", "PtjDangerBrush", "PtjTextPrimaryBrush"];
            for (int i = 0; i < values.Length; i++)
            {
                Assert.Equal(((SolidColorBrush)app.Resources[colors[i]]).Color, ((SolidColorBrush)values[i].Foreground).Color);
                Assert.True(values[i].Focusable);
                Assert.Contains("Not a broker balance", AutomationProperties.GetName(values[i]));
                if (i < 2) Assert.StartsWith("Estimated", values[i].Text);
                double headerRight = header.TranslatePoint(new(header.ActualWidth, 0), scroll).X;
                double valueRight = values[i].TranslatePoint(new(values[i].ActualWidth, 0), scroll).X;
                var headerGrid = (Grid)header.Parent;
                var rowGrid = (Grid)((StackPanel)values[i].Parent).Parent;
                Assert.True(Math.Abs(headerRight - valueRight - 12) <= 2,
                    $"Header edge {headerRight}; value edge {valueRight}; header width {headerGrid.ActualWidth}, row {rowGrid.ActualWidth}; header columns {string.Join(',', headerGrid.ColumnDefinitions.Select(c => c.ActualWidth))}; row columns {string.Join(',', rowGrid.ColumnDefinitions.Select(c => c.ActualWidth))}");
            }
            Assert.Equal(width < 1120, scroll.ScrollableWidth > 0);
            scroll.ScrollToRightEnd(); root.UpdateLayout();
            var action = elements.OfType<Button>().First(b => b.ContextMenu is not null && b.DataContext is AccountListItem);
            Assert.InRange(action.TranslatePoint(new(action.ActualWidth, 0), scroll).X, 0, scroll.ViewportWidth);
            values[0].BringIntoView(); root.UpdateLayout();
            Assert.InRange(values[0].TranslatePoint(new(0, 0), scroll).X, -1, scroll.ViewportWidth);
            if (Environment.GetEnvironmentVariable("PTJ_ACCOUNT_RENDER_DIRECTORY") is { Length: > 0 } path)
            {
                Directory.CreateDirectory(path);
                var bitmap = new RenderTargetBitmap(width * dpi / 96, 700 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32); bitmap.Render(root);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(path, $"balance-{theme}-{width}-{dpi}.png")); encoder.Save(file);
            }
            root.Child = null;
        }, shutdownDispatcher: false);
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject node)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        { var child = VisualTreeHelper.GetChild(node, i); yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
}
