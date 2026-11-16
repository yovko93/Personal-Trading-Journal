using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Views.Trades;
using PersonalTradingJournal.Desktop.Screenshots;
using PersonalTradingJournal.Domain.Screenshots;

namespace PersonalTradingJournal.Desktop.Tests.Trades;

public sealed class TradeWorkspaceViewTests
{
    [Fact]
    public async Task CompiledScreenshotFormCardsAndTradeSurfacesAcrossThemesAndDpi()
    {
        const string host = "PTJ_TRADE_WORKSPACE_HOST";
        if (Environment.GetEnvironmentVariable(host) != "1")
        {
            await IsolatedTestProcess.RunSuiteAsync(typeof(TradeWorkspaceViewTests), "trade-workspace", host,
                TimeSpan.FromSeconds(60), caseHangTimeout: TimeSpan.FromSeconds(30)); return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            // Owned Calendar windows can instantiate the shared template before attaching theme resources.
            var detached = new TradeScreenshotWorkspace();
            detached.Measure(new Size(360, 650));
            detached.Arrange(new Rect(0, 0, 360, 650));
            foreach (string theme in new[] { "Light", "Dark" })
            foreach (var (width, dpi) in new[] { (960, 96), (360, 240) })
            {
                app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
                app.Resources["UtcToTradingTimeConverter"] = new PersonalTradingJournal.Desktop.Converters.UtcToTradingTimeConverter();
                var picker = new FakeTradeScreenshotFilePicker { Selection = new("synthetic.png", new MemoryStream([1, 2, 3])) };
                var image = new DrawingImage(new GeometryDrawing(Brushes.SteelBlue, new Pen(Brushes.SeaGreen, 3), new RectangleGeometry(new Rect(0, 0, 160, 90))));
                image.Freeze();
                var vm = TradesViewModelTests.CreateViewModel(tradeScreenshotFilePicker: picker,
                    tradeScreenshotImageDecoder: new FakeTradeScreenshotImageDecoder { ImageToReturn = image });
                vm.ShowManualEntryCommand.Execute(null);
                var view = new TradeScreenshotWorkspace { DataContext = vm, Margin = new Thickness(16) };
                var scroll = new ScrollViewer { Content = view, Background = (Brush)app.Resources["PtjSurfaceBrush"],
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                void Layout() { scroll.Measure(new Size(width, 650)); scroll.Arrange(new Rect(0, 0, width, 650)); scroll.UpdateLayout(); }
                Layout(); VisualTreeHelper.SetRootDpi(scroll, new DpiScale(dpi / 96d, dpi / 96d)); Layout();
                Assert.Equal(dpi / 96d, VisualTreeHelper.GetDpi(scroll).DpiScaleX);
                var add = (Button)view.FindName("AddScreenshot"); Assert.True(add.IsEnabled);
                add.Command.Execute(null); Layout(); Assert.Equal(0, picker.CallCount);
                Assert.True(vm.IsAddScreenshotVisible);
                var type = (ComboBox)view.FindName("ScreenshotType"); type.SelectedItem = TradeScreenshotType.Entry;
                var timeframe = (TextBox)view.FindName("ScreenshotTimeframe"); timeframe.Text = "5m";
                Assert.Single(Descendants(view).OfType<TextBox>());
                Assert.Equal("Screenshot timeframe", AutomationProperties.GetName(timeframe));
                vm.UploadScreenshotCommand.ExecuteAsync(null).GetAwaiter().GetResult(); Layout();
                Assert.Equal(1, picker.CallCount); Assert.Same(image, vm.UploadedScreenshotImage);
                Assert.True(vm.SaveScreenshotCommand.CanExecute(null));
                Assert.True(((Button)view.FindName("SaveScreenshot")).IsEnabled);
                Render(scroll, theme, width, dpi, "form");
                vm.SaveScreenshotCommand.ExecuteAsync(null).GetAwaiter().GetResult(); Layout();
                Assert.False(vm.IsAddScreenshotVisible); Assert.Single(vm.StagedScreenshots);
                Assert.Equal("5m", vm.StagedScreenshots[0].Draft.Timeframe);
                Assert.Contains(Descendants(view).OfType<Image>(), i => ReferenceEquals(i.Source, image) && i.ActualHeight > 0);
                Assert.All(Descendants(view).OfType<Button>().Where(b => b.IsVisible), b =>
                    Assert.InRange(b.TranslatePoint(new Point(b.ActualWidth, 0), scroll).X, 0, width));
                Render(scroll, theme, width, dpi, "card");
                vm.CancelManualEntryCommand.Execute(null); Assert.Empty(vm.StagedScreenshots);

                var button = new Button { Content = "View", Style = (Style)app.Resources["PtjTradeViewButtonStyle"] };
                button.ApplyTemplate();
                var normal = button.Background;
                Assert.NotEqual(((SolidColorBrush)app.Resources["PtjSurfaceBrush"]).Color, ((SolidColorBrush)normal).Color);
                var hover = (DependencyPropertyKey)typeof(UIElement).GetField("IsMouseOverPropertyKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.GetValue(null)!;
                button.SetValue(hover, true);
                Assert.Equal(((SolidColorBrush)app.Resources["PtjJournalRevisionHoverBrush"]).Color, ((SolidColorBrush)button.Background).Color);
                Assert.NotEqual(normal, button.Background);
                button.SetValue(hover, false);
                button.IsEnabled = false;
                Assert.NotEqual(normal, button.Background);
                Assert.NotNull(button.Template.FindName("FocusIndicator", button));
                Assert.NotEqual(((SolidColorBrush)app.Resources["PtjTradeExecutionFactsBrush"]).Color,
                    ((SolidColorBrush)app.Resources["PtjTradeExecutionSourceBrush"]).Color);

                foreach (string background in new[] { "PtjTradeExecutionFactsBrush", "PtjTradeExecutionSourceBrush" })
                foreach (string foreground in new[] { "PtjTextPrimaryBrush", "PtjTextMutedBrush" })
                    Assert.True(Contrast(((SolidColorBrush)app.Resources[foreground]).Color,
                        ((SolidColorBrush)app.Resources[background]).Color) >= 4.5, $"{theme}: {foreground} on {background}");
                var compiledForm = new ResourceDictionary { Source = new Uri("/PersonalTradingJournal.Desktop;component/Views/Trades/TradeEntryForm.xaml", UriKind.Relative) };
                app.Resources["TradeEntryFormTemplate"] = compiledForm["TradeEntryFormTemplate"];
                var details = TradesViewModelTests.CreateAssociationRenderModel(image).GetAwaiter().GetResult();
                var page = new TradesView { DataContext = details };
                page.Measure(new Size(width, 800)); page.Arrange(new Rect(0, 0, width, 800)); page.UpdateLayout();
                VisualTreeHelper.SetRootDpi(page, new DpiScale(dpi / 96d, dpi / 96d));
                page.Measure(new Size(width, 800)); page.Arrange(new Rect(0, 0, width, 800)); page.UpdateLayout();
                Assert.DoesNotContain(Descendants(page).OfType<TextBlock>(), t => t.Text == "Trades");
                Assert.DoesNotContain(Descendants(page).OfType<Button>(), b => b.Content?.ToString()?.Contains("captured context") == true || b.Content?.ToString()?.Contains("Assess Trading Rules") == true);
                Assert.Contains(Descendants(page).OfType<TextBlock>(), t => t.Text == "Description");
                Assert.Contains(Descendants(page).OfType<Image>(), i => ReferenceEquals(i.Source, image));
                Assert.DoesNotContain(Descendants(page).OfType<TextBlock>(), t => t.Text == "Legacy metadata preserved, not displayed");
                Render(page, theme, width, dpi, "details");
                var pageScroll = Descendants(page).OfType<ScrollViewer>().First();
                var facts = Descendants(page).OfType<Grid>().First(g => ReferenceEquals(g.Background, app.Resources["PtjTradeExecutionFactsBrush"]));
                Assert.Contains(Descendants(page).OfType<Grid>(), g => ReferenceEquals(g.Background, app.Resources["PtjTradeExecutionSourceBrush"]));
                pageScroll.ScrollToVerticalOffset(facts.TranslatePoint(new Point(), pageScroll).Y + pageScroll.VerticalOffset);
                page.UpdateLayout();
                Render(page, theme, width, dpi, "executions");
                pageScroll.ScrollToTop(); page.UpdateLayout();
                details.ShowManualEntryCommand.Execute(null);
                details.TradeDescriptionText = "Optional Trade description, kept separate from screenshots and Journal text.";
                page.UpdateLayout();
                var description = Assert.Single(Descendants(page).OfType<TextBox>(), t => AutomationProperties.GetName(t) == "Trade description");
                Assert.Equal(details.TradeDescriptionText, description.Text); Assert.Equal(TextWrapping.Wrap, description.TextWrapping);
                Render(page, theme, width, dpi, "manual");
                pageScroll.ScrollToVerticalOffset(description.TranslatePoint(new Point(), pageScroll).Y + pageScroll.VerticalOffset);
                page.UpdateLayout();
                Render(page, theme, width, dpi, "manual-screenshots");
            }
            app.Shutdown();
        });
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    { for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); yield return child; foreach (var next in Descendants(child)) yield return next; } }
    private static double Contrast(Color a, Color b)
    {
        static double Linear(byte channel) { double value = channel / 255d; return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4); }
        static double Luminance(Color color) => .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
        double first = Luminance(a), second = Luminance(b);
        return (Math.Max(first, second) + .05) / (Math.Min(first, second) + .05);
    }
    private static void Render(FrameworkElement view, string theme, int width, int dpi, string state)
    {
        string directory = Path.Combine(Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY") ?? "TestResults", "trade-workspace-renders");
        Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap(width * dpi / 96, 650 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(view); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(Path.Combine(directory, $"{theme}-{width}-{dpi}-{state}.png")); encoder.Save(output);
    }
}
