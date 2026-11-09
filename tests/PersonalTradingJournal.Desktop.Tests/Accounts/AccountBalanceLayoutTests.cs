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
            VisualTreeHelper.SetRootDpi(root, new DpiScale(dpi / 96d, dpi / 96d));
            root.Measure(new(width, 700)); root.Arrange(new(0, 0, width, 700)); root.UpdateLayout();
            var elements = Descendants(root).ToArray();
            var scroll = Assert.IsType<ScrollViewer>(view.FindName("AccountTableScroll"));
            Assert.True(NestedTableWheelRouting.GetForwardVerticalWheel(scroll));
            var header = elements.OfType<TextBlock>().Single(t => t.Text == "Current Balance");
            var starting = Descendants(scroll).OfType<TextBlock>().Single(t => t.Text == "Starting Balance");
            Assert.Equal(Grid.GetColumn(starting) + 1, Grid.GetColumn(header));
            var payoutHeader = elements.OfType<TextBlock>().Single(t => t.Text == "Payout");
            var statusHeader = elements.OfType<TextBlock>().Single(t => t.Text == "Status");
            Assert.Equal(Grid.GetColumn(header) + 1, Grid.GetColumn(payoutHeader));
            Assert.Equal(Grid.GetColumn(payoutHeader) + 1, Grid.GetColumn(statusHeader));
            Assert.All(Descendants(scroll).OfType<TextBlock>(), t => Assert.Equal(TextAlignment.Left, t.TextAlignment));
            Assert.All(Descendants(scroll).OfType<StackPanel>().Where(p => p.Orientation == Orientation.Horizontal && p.Parent is Grid),
                p => Assert.Equal(HorizontalAlignment.Right, p.HorizontalAlignment));
            var actionsHeader = elements.OfType<TextBlock>().Single(t => t.Text == "Actions");
            var headerGridForActions = (Grid)actionsHeader.Parent;
            Assert.Equal(HorizontalAlignment.Right, actionsHeader.HorizontalAlignment);
            Assert.True(headerGridForActions.ColumnDefinitions[8].Width.IsAuto);
            var actionPanels = Descendants(scroll).OfType<StackPanel>().Where(p => p.Parent is Grid && Grid.GetColumn(p) == 8).ToArray();
            Assert.Equal(3, actionPanels.Length);
            foreach (var panel in actionPanels)
            {
                var row = (Grid)panel.Parent;
                var buttons = panel.Children.OfType<Button>().ToArray();
                Assert.Equal(2, buttons.Length);
                Assert.Equal(VerticalAlignment.Center, panel.VerticalAlignment);
                Assert.Equal(36, buttons[0].ActualHeight);
                Assert.Equal(buttons[0].ActualHeight, buttons[1].ActualHeight);
                Assert.Equal(buttons[0].TranslatePoint(new(), row).Y, buttons[1].TranslatePoint(new(), row).Y, 2);
                Assert.Equal(row.ActualHeight / 2, panel.TranslatePoint(new(0, panel.ActualHeight / 2), row).Y, 2);
                Assert.Equal(row.ActualWidth, panel.TranslatePoint(new(panel.ActualWidth, 0), row).X, 2);
                Assert.Equal(actionsHeader.TranslatePoint(new(actionsHeader.ActualWidth, 0), scroll).X,
                    buttons[1].TranslatePoint(new(buttons[1].ActualWidth, 0), scroll).X, 2);
                Assert.Equal(panel.ActualWidth, row.ColumnDefinitions[8].ActualWidth, 2);
                for (int column = 0; column < 9; column++)
                {
                    Assert.Equal(headerGridForActions.ColumnDefinitions[column].ActualWidth, row.ColumnDefinitions[column].ActualWidth, 2);
                    if (column == 8) continue;
                    var heading = headerGridForActions.Children.OfType<TextBlock>().Single(h => Grid.GetColumn(h) == column);
                    var cell = row.Children.OfType<FrameworkElement>().Single(c => Grid.GetColumn(c) == column);
                    Assert.Equal(heading.TranslatePoint(new(), scroll).X, cell.TranslatePoint(new(), scroll).X, 2);
                }
            }
            var values = elements.OfType<TextBlock>().Where(t => t.Name == "CurrentBalanceValue").ToArray();
            Assert.Equal(3, values.Length);
            Assert.Equal(TextAlignment.Left, header.TextAlignment);
            string[] colors = ["PtjSuccessBrush", "PtjDangerBrush", "PtjTextPrimaryBrush"];
            for (int i = 0; i < values.Length; i++)
            {
                Assert.Equal(((SolidColorBrush)app.Resources[colors[i]]).Color, ((SolidColorBrush)values[i].Foreground).Color);
                Assert.True(values[i].Focusable);
                Assert.Contains("Not a broker balance", AutomationProperties.GetName(values[i]));
                if (i < 2) Assert.StartsWith("Estimated", values[i].Text);
                double headerCenter = header.TranslatePoint(new(0, 0), scroll).X;
                double valueCenter = values[i].TranslatePoint(new(0, 0), scroll).X;
                var headerGrid = (Grid)header.Parent;
                var rowGrid = (Grid)((StackPanel)values[i].Parent).Parent;
                Assert.Equal(headerGrid.ColumnDefinitions[5].ActualWidth, rowGrid.ColumnDefinitions[5].ActualWidth, 2);
                Assert.Equal(headerCenter, valueCenter, 2);
                Assert.Equal(TextAlignment.Left, values[i].TextAlignment);
                var coverage = ((StackPanel)values[i].Parent).Children.OfType<TextBlock>().Single(t => t.Name == "CurrentBalanceCoverage");
                Assert.Equal(headerCenter, coverage.TranslatePoint(new(0, 0), scroll).X, 2);
                Assert.Equal(TextAlignment.Left, coverage.TextAlignment);
                Assert.Equal(0, values[i].TranslatePoint(new(), (StackPanel)values[i].Parent).Y, 2);
                double coverageTop = coverage.TranslatePoint(new(), (StackPanel)values[i].Parent).Y;
                Assert.Equal(values[i].ActualHeight, coverageTop, 2); // TranslatePoint can differ by floating-point roundoff (18.62 vs 18.619999999999976).
                Assert.Equal(i < 2, coverage.Text.Length > 0);
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

    [Theory]
    [InlineData("Light", 1280, 96)]
    [InlineData("Dark", 1280, 96)]
    [InlineData("Light", 480, 240)]
    [InlineData("Dark", 480, 240)]
    public async Task PayoutDetailsRemainReadableAndReachable(string theme, int width, int dpi)
    {
        if (Environment.GetEnvironmentVariable("PTJ_ACCOUNT_BALANCE_HOST") != "1") { await Host.Value; return; }
        var store = new AccountPayoutViewModelTests.Payouts();
        await store.AddAsync(store.Id, "EUR", 12.34m); await store.AddAsync(store.Id, "EUR", 56.78m);
        var vm = AccountPayoutViewModelTests.Create(store, new FakeDialogService());
        await vm.EnsureLoadedAsync(); await vm.ViewAccountCommand.ExecuteAsync(store.Id);
        await CalendarStaTest.RunAsync(() =>
        {
            var app = System.Windows.Application.Current ?? new() { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
            var view = new AccountsView { DataContext = vm };
            var root = new Border { Child = view }; root.SetResourceReference(Border.BackgroundProperty, "PtjBackgroundBrush");
            VisualTreeHelper.SetRootDpi(root, new DpiScale(dpi / 96d, dpi / 96d));
            root.Measure(new(width, 900)); root.Arrange(new(0, 0, width, 900)); root.UpdateLayout();
            var section = Assert.IsType<Border>(view.FindName("AccountPayoutSection"));
            var children = Descendants(section).OfType<FrameworkElement>().Where(e => e is Button or TextBox).ToArray();
            Assert.Equal(4, children.Length);
            var removeButtons = Descendants(section).OfType<Button>().Where(b => b.Content as string == "Remove…").ToArray();
            Assert.Equal(2, removeButtons.Length);
            foreach (var remove in removeButtons)
            {
                Assert.Same(app.Resources["PtjDangerButtonStyle"], remove.Style);
                Assert.Equal(((SolidColorBrush)app.Resources["PtjDangerBrush"]).Color, ((SolidColorBrush)remove.Background).Color);
                Assert.Equal(((SolidColorBrush)app.Resources["PtjDangerBrush"]).Color, ((SolidColorBrush)remove.BorderBrush).Color);
                Assert.Equal(((SolidColorBrush)app.Resources["PtjSurfaceBrush"]).Color, ((SolidColorBrush)remove.Foreground).Color);
                Assert.Same(vm.RemovePayoutCommand, remove.Command);
                Assert.Contains(Assert.IsType<AccountPayout>(remove.CommandParameter), vm.Payouts);
            }
            foreach (var element in children)
            {
                Assert.True(element.IsEnabled);
                Assert.InRange(element.TranslatePoint(new(), section).X, 0, section.ActualWidth);
                Assert.InRange(element.TranslatePoint(new(element.ActualWidth, 0), section).X, 0, section.ActualWidth);
            }
            Assert.Equal(((SolidColorBrush)app.Resources["PtjSuccessBrush"]).Color, ((SolidColorBrush)section.BorderBrush).Color);
            section.BringIntoView(); root.UpdateLayout();
            if (Environment.GetEnvironmentVariable("PTJ_ACCOUNT_RENDER_DIRECTORY") is { Length: > 0 } path)
            {
                Directory.CreateDirectory(path);
                var bitmap = new RenderTargetBitmap(width * dpi / 96, 900 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32); bitmap.Render(root);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(path, $"payout-{theme}-{width}-{dpi}.png")); encoder.Save(file);
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
