using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
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
                var heading = (TextBlock)view.FindName("ScreenshotsHeading");
                AssertRightAligned(heading, add);
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
                bool zeroNet = width == 360 && theme == "Light";
                bool unknownNet = width == 360 && theme == "Dark";
                var details = TradesViewModelTests.CreateAssociationRenderModel(image, imported: true, zeroNet: zeroNet, unknownNet: unknownNet).GetAwaiter().GetResult();
                var page = new TradesView { DataContext = details };
                page.Measure(new Size(width, 800)); page.Arrange(new Rect(0, 0, width, 800)); page.UpdateLayout();
                VisualTreeHelper.SetRootDpi(page, new DpiScale(dpi / 96d, dpi / 96d));
                page.Measure(new Size(width, 800)); page.Arrange(new Rect(0, 0, width, 800)); page.UpdateLayout();
                Assert.DoesNotContain(Descendants(page).OfType<TextBlock>(), t => t.Text is "Trades" or "PRICING SNAPSHOT");
                Assert.DoesNotContain(Descendants(page).OfType<Button>(), b => b.Content?.ToString()?.Contains("captured context") == true || b.Content?.ToString()?.Contains("Assess Trading Rules") == true);
                Assert.Contains(Descendants(page).OfType<TextBlock>(), t => t.Text == "Description");
                Assert.Contains(Descendants(page).OfType<Image>(), i => ReferenceEquals(i.Source, image));
                Assert.DoesNotContain(Descendants(page).OfType<TextBlock>(), t => t.Text == "Legacy metadata preserved, not displayed");
                Render(page, theme, width, dpi, "details");
                var pageScroll = Descendants(page).OfType<ScrollViewer>().First();
                var summary = (Grid)page.FindName("TradeEconomicsSummary");
                AssertEconomicsSummary(page, summary, width == 360);
                AssertAmount(page, "SelectedTradeDetail.GrossPnL", unknownNet ? null : zeroNet ? 3.50m : 1262.50m);
                AssertAmount(page, "SelectedTradeDetail.EffectiveNet.Value", unknownNet ? null : zeroNet ? 0m : 1259m);
                AssertAmount(page, "GrossPnL", unknownNet ? null : zeroNet ? 3.50m : 1262.50m);
                AssertAmount(page, "EffectiveNet.Value", unknownNet ? null : zeroNet ? 0m : 1259m);

                var editDescription = (Button)page.FindName("EditImportedDescription");
                Assert.True(IsShown(editDescription) && editDescription.IsEnabled);
                Assert.False(IsShown(Assert.Single(Descendants(page).OfType<Button>(),
                    b => ReferenceEquals(b.Command, details.ShowSelectedTradeEditCommand))));
                editDescription.Command.Execute(null);
                page.UpdateLayout();
                var descriptionEditor = (StackPanel)page.FindName("ImportedDescriptionEditor");
                var importedText = Assert.Single(Descendants(descriptionEditor).OfType<TextBox>());
                Assert.Equal("Imported annotation", importedText.Text);
                Assert.Equal(TextWrapping.Wrap, importedText.TextWrapping);
                pageScroll.ScrollToVerticalOffset(descriptionEditor.TranslatePoint(new Point(), pageScroll).Y + pageScroll.VerticalOffset - 50);
                page.UpdateLayout();
                Render(page, theme, width, dpi, "imported-description");
                details.CancelDescriptionEditorCommand.Execute(null);
                page.UpdateLayout();
                Assert.Equal(Visibility.Collapsed, descriptionEditor.Visibility);
                var setupStatus = (TextBlock)page.FindName("SetupSuccessStatus");
                Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(setupStatus));
                Assert.Equal(Visibility.Collapsed, setupStatus.Visibility);
                pageScroll.ScrollToVerticalOffset(summary.TranslatePoint(new Point(), pageScroll).Y + pageScroll.VerticalOffset - 20);
                page.UpdateLayout();
                Render(page, theme, width, dpi, "economics");
                var facts = Descendants(page).OfType<Grid>().First(g => ReferenceEquals(g.Background, app.Resources["PtjTradeExecutionFactsBrush"]));
                Assert.Contains(Descendants(page).OfType<Grid>(), g => ReferenceEquals(g.Background, app.Resources["PtjTradeExecutionSourceBrush"]));
                pageScroll.ScrollToVerticalOffset(facts.TranslatePoint(new Point(), pageScroll).Y + pageScroll.VerticalOffset);
                page.UpdateLayout();
                Render(page, theme, width, dpi, "executions");
                var executions = Assert.Single(Descendants(page).OfType<ScrollViewer>(),
                    viewer => AutomationProperties.GetName(viewer) == "Trade execution columns");
                AssertExecutionScrolling(page, pageScroll, executions, facts, width == 360);

                var rowButtons = Descendants(page).OfType<Button>().ToArray();
                var rowView = Assert.Single(rowButtons, b => ReferenceEquals(b.Command, details.ShowTradeDetailCommand));
                var rowMenu = Assert.Single(rowButtons, b => ReferenceEquals(b.DataContext, rowView.DataContext) &&
                    AutomationProperties.GetName(b) == "More trade actions");
                var actionsHeading = Assert.Single(Descendants(page).OfType<TextBlock>(), t => t.Text == "Actions");
                var header = (Grid)actionsHeading.Parent;
                var column = header.ColumnDefinitions[Grid.GetColumn(actionsHeading)];
                Assert.Equal(column.Offset + column.ActualWidth / 2,
                    actionsHeading.TranslatePoint(new Point(actionsHeading.ActualWidth / 2, 0), header).X, 2);
                Assert.Equal(36d, rowView.ActualHeight);
                Assert.Equal(rowView.ActualHeight, rowMenu.ActualHeight);
                Assert.Equal(rowView.TranslatePoint(new Point(), page).Y, rowMenu.TranslatePoint(new Point(), page).Y, 6);
                Assert.True(PersonalTradingJournal.Desktop.Interactions.EntityActionMenu.GetOpensContextMenu(rowMenu));
                Assert.NotNull(rowMenu.ContextMenu);
                var listScroll = (ScrollViewer)page.FindName("TradeListScrollViewer");
                listScroll.ScrollToRightEnd();
                pageScroll.ScrollToVerticalOffset(rowView.TranslatePoint(new Point(), pageScroll).Y + pageScroll.VerticalOffset - 20);
                page.UpdateLayout();
                var listViewport = Assert.IsType<ScrollContentPresenter>(listScroll.Template.FindName("PART_ScrollContentPresenter", listScroll));
                Assert.InRange(rowView.TranslatePoint(new Point(), listViewport).X, 0, listViewport.ActualWidth);
                Assert.InRange(rowMenu.TranslatePoint(new Point(rowMenu.ActualWidth, 0), listViewport).X, 0, listViewport.ActualWidth);
                Render(page, theme, width, dpi, "row-actions");

                var setup = Assert.Single(Descendants(page).OfType<TradeSetupClassificationView>());
                var setupEdit = (FrameworkElement)setup.FindName("SetupEditor");
                var setupAdd = (Button)setup.FindName("OpenSetupEditor");
                AssertRightAligned((TextBlock)setup.FindName("SetupHeading"), setupAdd);
                Assert.Equal(Visibility.Collapsed, setupEdit.Visibility);
                setupAdd.Command.Execute(null);
                page.UpdateLayout();
                Assert.Equal(Visibility.Visible, setupEdit.Visibility);
                var setupChoice = (ComboBox)setup.FindName("ClassificationSetup");
                setupChoice.SelectedItem = Assert.Single(details.TradeDetailTradingSetupOptions, option => option.Id != details.SelectedTradeDetail!.TradingSetupId);
                Assert.True(((Button)setup.FindName("SaveClassification")).IsEnabled);
                pageScroll.ScrollToVerticalOffset(setup.TranslatePoint(new Point(), pageScroll).Y + pageScroll.VerticalOffset - 20);
                page.UpdateLayout();
                Render(page, theme, width, dpi, "setup-open");
                details.CancelSetupEditorCommand.Execute(null);
                page.UpdateLayout();
                Assert.Equal(Visibility.Collapsed, setupEdit.Visibility);

                var mistakeAdd = (Button)page.FindName("OpenMistakeEditor");
                var mistakeEdit = (Border)page.FindName("MistakeEditor");
                Assert.Equal(Visibility.Collapsed, mistakeEdit.Visibility);
                var mistakeHeading = Assert.Single(Descendants(page).OfType<TextBlock>(), t => t.Text == "Trading Mistakes");
                AssertRightAligned(mistakeHeading, mistakeAdd);
                var screenshotAdd = Assert.Single(Descendants(page).OfType<Button>(),
                    b => IsShown(b) && ReferenceEquals(b.Command, details.ShowAddScreenshotCommand));
                var removeMistake = Assert.Single(Descendants(page).OfType<Button>(),
                    b => b.CommandParameter is PersonalTradingJournal.Application.Mistakes.TradeMistakeListItem && Equals(b.Content, "Remove"));
                Assert.Equal("Change Setup", setupAdd.Content);
                AssertActionContrast(app, setupAdd, hover, "PtjJournalDraftBrush", "PtjJournalDraftHoverBrush");
                AssertActionContrast(app, mistakeAdd, hover, "PtjJournalReviewBrush", "PtjJournalReviewHoverBrush");
                AssertActionContrast(app, screenshotAdd, hover, "PtjJournalRevisionBrush", "PtjJournalRevisionHoverBrush");
                AssertActionContrast(app, removeMistake, hover, "PtjDangerBrush", "PtjWorkspaceRemoveHoverBrush");
                Assert.Equal(3, new[] { setupAdd.Background, mistakeAdd.Background, screenshotAdd.Background }
                    .Cast<SolidColorBrush>().Select(brush => brush.Color).Distinct().Count());
                mistakeAdd.Command.Execute(null);
                page.UpdateLayout();
                Assert.Equal(Visibility.Visible, mistakeEdit.Visibility);
                var mistakeChoice = (ComboBox)page.FindName("MistakeSelector");
                mistakeChoice.SelectedItem = Assert.Single(details.AvailableTradingMistakes);
                Assert.True(details.AssignMistakeCommand.CanExecute(null));
                pageScroll.ScrollToVerticalOffset(mistakeHeading.TranslatePoint(new Point(), pageScroll).Y + pageScroll.VerticalOffset - 20);
                page.UpdateLayout();
                Render(page, theme, width, dpi, "mistake-open");
                details.CancelMistakeEditorCommand.Execute(null);
                page.UpdateLayout();
                Assert.Equal(Visibility.Collapsed, mistakeEdit.Visibility);
                Render(page, theme, width, dpi, "mistake-closed");
                pageScroll.ScrollToTop();
                page.UpdateLayout();
                details.ShowManualEntryCommand.Execute(null);
                details.TradeDescriptionText = "Optional Trade description, kept separate from screenshots and Journal text.";
                page.UpdateLayout();
                var description = Assert.Single(Descendants(page).OfType<TextBox>(), t => AutomationProperties.GetName(t) == "Trade description");
                Assert.Equal(details.TradeDescriptionText, description.Text); Assert.Equal(TextWrapping.Wrap, description.TextWrapping);
                AssertEntryFieldNames(page, details.QuantityLabel);
                Render(page, theme, width, dpi, "manual");
                pageScroll.ScrollToVerticalOffset(description.TranslatePoint(new Point(), pageScroll).Y + pageScroll.VerticalOffset);
                page.UpdateLayout();
                Render(page, theme, width, dpi, "manual-screenshots");
            }
            app.Shutdown();
        });
    }

    // These roots are deliberately detached from a native window.
    private static bool IsShown(FrameworkElement element)
    {
        for (DependencyObject? current = element; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is UIElement { Visibility: not Visibility.Visible }) return false;
        }
        return true;
    }

    private static void AssertAmount(TradesView page, string path, decimal? amount)
    {
        var text = Assert.Single(Descendants(page).OfType<TextBlock>(),
            t => t.GetBindingExpression(TextBlock.TextProperty)?.ParentBinding.Path?.Path == path);
        if (amount.HasValue)
        {
            Assert.True(IsShown(text), $"Expected a visible amount for {path}.");
            Assert.Equal(amount.Value.ToString("N2", text.Language.GetSpecificCulture()), text.Text);
        }
        else
        {
            Assert.True(!IsShown(text) || text.Text == "—");
        }
    }

    private static void AssertActionContrast(System.Windows.Application app, Button button, DependencyPropertyKey hover, string normal, string hovered)
    {
        button.ApplyTemplate();
        var focus = Assert.IsType<System.Windows.Shapes.Rectangle>(button.Template.FindName("FocusIndicator", button));
        Color foreground = ((SolidColorBrush)button.Foreground).Color;
        Assert.Equal(foreground, ((SolidColorBrush)focus.Stroke).Color);
        Assert.Same(app.Resources[normal], button.Background);
        Assert.True(Contrast(foreground, ((SolidColorBrush)button.Background).Color) >= 4.5);
        button.SetValue(hover, true);
        Assert.Same(app.Resources[hovered], button.Background);
        Assert.True(Contrast(foreground, ((SolidColorBrush)button.Background).Color) >= 4.5);
        button.SetValue(hover, false);
    }

    private static void AssertEntryFieldNames(TradesView page, string quantityLabel)
    {
        // Exercise the names exposed by UI Automation on the compiled entry template.
        var expected = new Dictionary<string, string>
        {
            ["SelectedAccount"] = "Trading Account",
            ["SelectedInstrument"] = "Instrument",
            ["SelectedDirection"] = "Direction",
            ["QuantityText"] = quantityLabel,
            ["SelectedTradingSetup"] = "Trading Setup (optional)",
            ["EntryExecutedAtNewYorkText"] = "Entry time (New York)",
            ["EntryPriceText"] = "Entry price",
            ["EntryCommissionText"] = "Entry commission",
            ["EntryFeesText"] = "Entry fees",
            ["ExitExecutedAtNewYorkText"] = "Exit time (New York)",
            ["ExitPriceText"] = "Exit price",
            ["ExitCommissionText"] = "Exit commission",
            ["ExitFeesText"] = "Exit fees",
        };
        foreach (var control in Descendants(page).OfType<Control>())
        {
            var binding = control switch
            {
                ComboBox combo => combo.GetBindingExpression(ComboBox.SelectedItemProperty),
                TextBox text => text.GetBindingExpression(TextBox.TextProperty),
                _ => null
            };
            if (binding?.ParentBinding.Path.Path is { } path && expected.Remove(path, out var name))
            {
                var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(control);
                Assert.Equal(name, peer.GetName());
            }
        }
        Assert.Empty(expected);
    }

    private static void AssertEconomicsSummary(TradesView page, Grid summary, bool narrow)
    {
        Assert.Equal(narrow ? 2 : 1, Grid.GetColumnSpan(summary.Children[0]));
        Assert.Equal(narrow ? 1 : 0, Grid.GetRow(summary.Children[2]));
        Assert.Equal(narrow ? 2 : 1, Grid.GetRow(summary.Children[4]));
        if (narrow)
        {
            foreach (int index in new[] { 2, 4, 6 })
                Assert.Equal(0, ((FrameworkElement)summary.Children[index]).TranslatePoint(new Point(), summary).X, 1);
        }
        foreach ((string name, string caption, string timestamp) in new[]
        {
            ("TradeOpenedTimestamp", "Opened (New York)", "2026-09-10 09:30:00"),
            ("TradeClosedTimestamp", "Closed (New York)", "2026-09-10 10:30:00")
        })
        {
            var value = (TextBlock)page.FindName(name);
            var label = Assert.IsType<TextBlock>(((StackPanel)value.Parent).Children[0]);
            Assert.Equal(caption, label.Text);
            Assert.Equal(timestamp, value.Text);
            AssertFits(summary, label);
            AssertFits(summary, value);
        }

        static void AssertFits(Grid summary, TextBlock text)
        {
            var formatted = new FormattedText(text.Text, CultureInfo.InvariantCulture, text.FlowDirection,
                new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch), text.FontSize,
                text.Foreground, VisualTreeHelper.GetDpi(text).PixelsPerDip);
            Assert.True(text.ActualWidth >= formatted.Width,
                $"'{text.Text}' has {text.ActualWidth:F1} DIP, needs {formatted.Width:F1} DIP.");
            Assert.True(text.ActualHeight >= formatted.Height - 1);
            var trailingEdge = text.TranslatePoint(new Point(formatted.Width, 0), summary).X;
            Assert.InRange(trailingEdge, 0, summary.ActualWidth);
        }
    }

    private static void AssertRightAligned(TextBlock heading, Button action)
    {
        var grid = (Grid)VisualTreeHelper.GetParent(action);
        Point buttonStart = action.TranslatePoint(new Point(), grid);
        Point titleEnd = heading.TranslatePoint(new Point(heading.ActualWidth, 0), grid);
        Assert.True(heading.ActualWidth > 0 && heading.ActualHeight > 0);
        Assert.True(action.ActualWidth > 0 && action.ActualHeight > 0);
        Assert.True(titleEnd.X <= buttonStart.X);
        Assert.Equal(grid.ActualWidth, buttonStart.X + action.ActualWidth + action.Margin.Right, 1);
    }

    private static void AssertExecutionScrolling(TradesView page, ScrollViewer outer, ScrollViewer inner, Grid source, bool narrow)
    {
        Assert.Equal(PanningMode.VerticalOnly, outer.PanningMode);
        Assert.Equal(0, inner.ScrollableHeight);
        if (narrow)
        {
            Assert.True(inner.ScrollableWidth > 0);
            inner.ScrollToRightEnd();
            page.UpdateLayout();
            Assert.InRange(inner.ScrollableWidth - inner.HorizontalOffset, 0, 1);
        }

        double horizontalOffset = inner.HorizontalOffset;
        outer.ScrollToTop();
        page.UpdateLayout();
        RaiseVerticalWheel(-120);
        Assert.True(outer.VerticalOffset > 0);
        double afterNotch = outer.VerticalOffset;
        RaiseVerticalWheel(-30);
        Assert.True(outer.VerticalOffset > afterNotch);
        RaiseVerticalWheel(120);
        Assert.True(outer.VerticalOffset < afterNotch + 1);
        Assert.Equal(horizontalOffset, inner.HorizontalOffset);

        using var inputSource = new HwndSource(new HwndSourceParameters("Trade execution routed key test"));
        double beforeKey = outer.VerticalOffset;
        var key = new KeyEventArgs(Keyboard.PrimaryDevice, inputSource, 0, Key.PageDown)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent
        };
        source.RaiseEvent(key);
        page.UpdateLayout();
        Assert.True(key.Handled);
        Assert.True(outer.VerticalOffset > beforeKey);

        void RaiseVerticalWheel(int delta)
        {
            var wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, delta)
            {
                RoutedEvent = Mouse.PreviewMouseWheelEvent
            };
            source.RaiseEvent(wheel);
            page.UpdateLayout();
            Assert.True(wheel.Handled);
        }
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
        int pixelHeight = (int)Math.Ceiling(view.ActualHeight * dpi / 96);
        var bitmap = new RenderTargetBitmap(width * dpi / 96, pixelHeight, dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(view); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(Path.Combine(directory, $"{theme}-{width}-{dpi}-{state}.png")); encoder.Save(output);
    }
}
