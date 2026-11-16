using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Views.Trades;

namespace PersonalTradingJournal.Desktop.Tests.Trades;

public sealed class TradePlaybookViewTests
{
    [Fact]
    public async Task CompiledSetupClassificationWrapsAcrossThemesAndDpi()
    {
        const string host = "PTJ_TRADE_PLAYBOOK_VIEW_HOST";
        if (Environment.GetEnvironmentVariable(host) != "1")
        {
            await IsolatedTestProcess.RunSuiteAsync(typeof(TradePlaybookViewTests), "trade-playbook", host,
                TimeSpan.FromSeconds(60), caseHangTimeout: TimeSpan.FromSeconds(30)); return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (string theme in new[] { "Light", "Dark" })
            foreach (var (width, dpi) in new[] { (960, 96), (480, 240) })
            {
                app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
                var vm = TradesViewModelTests.CreateAssociationRenderModel().GetAwaiter().GetResult();
                var view = new TradeSetupClassificationView { DataContext = vm, Margin = new Thickness(12) };
                var scroll = new ScrollViewer { Content = view, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Background = (Brush)app.Resources["PtjSurfaceBrush"] };
                VisualTreeHelper.SetRootDpi(scroll, new DpiScale(dpi / 96d, dpi / 96d));
                void Layout() { scroll.Measure(new Size(width, 650)); scroll.Arrange(new Rect(0,0,width,650)); scroll.UpdateLayout(); }
                Layout();
                var selector = (ComboBox)view.FindName("ClassificationSetup");
                Assert.Same(vm.SelectedTradeDetailTradingSetup, selector.SelectedItem);
                Assert.True(selector.Focusable); Assert.Contains("Trading Setup", AutomationProperties.GetName(selector));
                var confirm = (Button)view.FindName("SaveClassification");
                Assert.Same(vm.SaveTradingSetupCommand, confirm.Command); Assert.True(confirm.IsEnabled && confirm.Focusable);
                Assert.NotNull(confirm.FocusVisualStyle); Assert.InRange(confirm.TranslatePoint(new Point(confirm.ActualWidth,0), scroll).X, 0, width);
                Assert.Null(view.FindName("OpenCapturedContext"));
                Assert.Empty(Descendants(view).OfType<Expander>());
                Assert.All(Descendants(view).OfType<Button>().Where(b => b.IsVisible), b =>
                    Assert.InRange(b.TranslatePoint(new Point(b.ActualWidth, 0), scroll).X, 0, width));
                Render(scroll, theme, width, dpi, "classification");
                scroll.ScrollToBottom(); Layout(); Assert.InRange(scroll.ScrollableHeight - scroll.VerticalOffset, 0, 1);
            }
            app.Shutdown();
        });
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    { for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root,i); yield return child; foreach (var next in Descendants(child)) yield return next; } }
    private static void Render(FrameworkElement view, string theme, int width, int dpi, string state)
    {
        if (Environment.GetEnvironmentVariable("PTJ_PLAYBOOK_RENDER_DIRECTORY") is not { Length: > 0 } directory) return;
        Directory.CreateDirectory(directory); var bitmap = new RenderTargetBitmap(width*dpi/96, 650*dpi/96, dpi,dpi,PixelFormats.Pbgra32);
        bitmap.Render(view); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory,$"trade-playbook-{theme}-{width}-{dpi}-{state}.png")); encoder.Save(stream);
    }
}
