using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.Navigation;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Views.Setups;
using PersonalTradingJournal.Desktop.Screenshots;
using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Desktop.Tests.Setups;

public sealed class PlaybookViewTests
{
    private const string Host = "PTJ_PLAYBOOK_VIEW_HOST";
    [Fact]
    public async Task CompiledLightDarkReadingHistoryEditingScrollingSelectionVetoAndOwnerClosing()
    {
        if (Environment.GetEnvironmentVariable(Host) != "1")
        {
            await IsolatedTestProcess.RunSuiteAsync(typeof(PlaybookViewTests), "playbook-view", Host,
                TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30));
            return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            byte[] chart = SyntheticChart();
            var thumbnail = PlaybookExampleImages.Decode(chart, true);
            Assert.InRange(thumbnail.PixelWidth, 1, 384); Assert.True(thumbnail.IsFrozen);
            var full = PlaybookExampleImages.Decode(chart, false); Assert.Equal(800, full.PixelWidth);
            Assert.ThrowsAny<Exception>(() => PlaybookExampleImages.Decode([1, 2, 3], true));
            Assert.Throws<InvalidDataException>(() => PlaybookExampleImages.Decode(new byte[PlaybookExampleImage.MaximumBytes + 1], true));
            Assert.ThrowsAny<Exception>(() => PlaybookExampleImages.Decode(SyntheticChart(8193, 1), true));
            Assert.ThrowsAsync<InvalidDataException>(() => PlaybookExampleImages.ReadBoundedAsync(new MemoryStream(), default)).GetAwaiter().GetResult();
            Assert.ThrowsAsync<InvalidDataException>(() => PlaybookExampleImages.ReadBoundedAsync(new MemoryStream(new byte[PlaybookExampleImage.MaximumBytes + 1]), default)).GetAwaiter().GetResult();
            foreach (var theme in new[] { "Light", "Dark" })
            {
                app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
                foreach (var (width, dpi) in new[] { (960, 96), (480, 240) })
                {
                    var images = new ExampleImagesFake { Preview = thumbnail };
                    var f = new PlaybookFixture(images, images); f.Seed(12); f.OpenAsync().GetAwaiter().GetResult();
                    var view = new PlaybookView { DataContext = f.Vm };
                    VisualTreeHelper.SetRootDpi(view, new DpiScale(dpi / 96d, dpi / 96d));
                    void Layout() { view.Measure(new Size(width, 650)); view.Arrange(new Rect(0, 0, width, 650)); view.UpdateLayout(); }
                    Layout();
                    var selector = (ComboBox)view.FindName("SetupSelector");
                    Assert.Equal(f.Setup.Id, ((Application.Setups.TradingSetupListItem)selector.SelectedItem).Id);
                    Assert.Equal("Trading Setup for Playbook", AutomationProperties.GetName(selector));
                    Assert.Equal("Current revision 12", ((TextBlock)view.FindName("Version")).Text);
                    Assert.True(selector.Focusable);
                    Render(view, theme, width, dpi, "current");
                    f.Vm.ViewRevisionCommand.Execute(f.Vm.Revisions[1]); Layout();
                    Assert.Contains("Historical revision 11", ((TextBlock)view.FindName("Version")).Text);
                    Assert.False(((Button)view.FindName("EditGuidance")).IsEnabled);
                    Assert.Equal(Visibility.Collapsed, ((ItemsControl)view.FindName("EditorFields")).Visibility);
                    Render(view, theme, width, dpi, "historical");
                    f.Vm.ShowCurrentCommand.Execute(null); f.Edit(string.Join("\n", Enumerable.Repeat("Synthetic guidance remains readable and editable.", 40))); Layout();
                    var fields = Descendants((ItemsControl)view.FindName("EditorFields")).OfType<TextBox>().ToArray();
                    Assert.Equal(5, fields.Length);
                    Assert.Equal(f.Vm.Fields.Select(x => x.Label), fields.Select(AutomationProperties.GetName));
                    Assert.All(fields, box =>
                    {
                        Assert.True(box.AcceptsReturn && !box.AcceptsTab && box.Focusable);
                        Assert.True(AutomationProperties.GetIsRequiredForForm(box));
                        Assert.Equal(ScrollBarVisibility.Auto, box.VerticalScrollBarVisibility);
                        Assert.InRange(box.ActualWidth, 100, Math.Min(width, 760));
                        Assert.Equal(0, box.MaxLength); // Validation, not silent truncation.
                    });
                    var save = (Button)view.FindName("SavePlaybook"); var cancel = (Button)view.FindName("CancelEditor");
                    foreach (var button in new[] { save, cancel })
                    {
                        // Detached measure/render hosts have no native PresentationSource. Check
                        // the actual visibility bindings, nonzero geometry and viewport reachability.
                        Assert.Equal(Visibility.Visible, button.Visibility);
                        Assert.Equal(Visibility.Visible, ((WrapPanel)view.FindName("EditorActions")).Visibility);
                        Assert.True(button.IsEnabled && button.Focusable && button.ActualHeight > 0);
                        Assert.InRange(button.TranslatePoint(new Point(button.ActualWidth, button.ActualHeight), view).X, 0, width);
                        Assert.InRange(button.TranslatePoint(new Point(0, button.ActualHeight), view).Y, 0, 650);
                        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)));
                    }
                    Assert.Same(f.Vm.SaveCommand, ((KeyBinding)view.InputBindings[0]).Command);
                    Assert.NotNull(save.FocusVisualStyle);
                    fields[0].Text = "Bound edit"; Assert.Equal("Bound edit", f.Vm.Fields[0].Text);
                    selector.SelectedItem = f.Vm.Setups.Single(s => s.Id == f.Other.Id); Layout();
                    Assert.Equal(f.Setup.Id, ((Application.Setups.TradingSetupListItem)selector.SelectedItem).Id);
                    Assert.Equal("Bound edit", f.Vm.Fields[0].Text);
                    var scroll = (ScrollViewer)view.FindName("PageScroll");
                    scroll.ScrollToBottom(); Layout(); Assert.True(scroll.VerticalOffset > 0);
                    Assert.InRange(scroll.ExtentHeight - scroll.ViewportHeight - scroll.VerticalOffset, -1, 1);
                    scroll.ScrollToTop(); Layout();
                    scroll.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120) { RoutedEvent = UIElement.PreviewMouseWheelEvent });
                    Layout(); Assert.True(scroll.VerticalOffset > 0);
                    scroll.ScrollToTop(); Layout(); Render(view, theme, width, dpi, "editor");
                    f.Vm.AttachExampleCommand.ExecuteAsync(null).GetAwaiter().GetResult();
                    var example = f.Vm.EditorExamples.Last(); example.Kind = PlaybookExampleKind.Invalid;
                    example.Annotation = "NQ synthetic example: the retest failed its confirmation. Lesson: wait for the stated condition; this label does not evaluate a Trade.";
                    example.Thumbnail = thumbnail;
                    Layout(); var exampleEditor = (StackPanel)view.FindName("ExampleEditor");
                    exampleEditor.BringIntoView(); Layout();
                    var annotation = Descendants((ItemsControl)view.FindName("ExampleRows")).OfType<TextBox>().Last();
                    ((Border)VisualTreeHelper.GetParent(VisualTreeHelper.GetParent(annotation))).BringIntoView(); Layout();
                    Assert.True(annotation.Focusable); Assert.InRange(annotation.ActualWidth, 100, width);
                    Assert.Contains("condition and lesson", AutomationProperties.GetName(annotation));
                    var kind = Descendants((ItemsControl)view.FindName("ExampleRows")).OfType<ComboBox>().Last();
                    Assert.Equal(PlaybookExampleKind.Invalid, kind.SelectedItem);
                    var exampleButtons = Descendants((ItemsControl)view.FindName("ExampleRows")).OfType<Button>().Where(b => b.Content is "Remove example" or "Open full-size chart").ToArray();
                    Assert.Equal(3, exampleButtons.Count(b => b.Visibility == Visibility.Visible));
                    Assert.Single(exampleButtons, b => b.Content is "Open full-size chart" && b.Visibility == Visibility.Collapsed); // Legacy notes have no image.
                    foreach (var button in exampleButtons.Where(b => b.Visibility == Visibility.Visible))
                    { Assert.True(button.Focusable); Assert.InRange(button.ActualWidth, 20, width); }
                    Render(view, theme, width, dpi, "examples");
                    foreach (var field in f.Vm.Fields) field.Text = "Synthetic guidance";
                    f.Vm.SaveCommand.ExecuteAsync(null).GetAwaiter().GetResult(); Layout();
                    Assert.False(f.Vm.IsEditing); Assert.Equal(2, f.Vm.ReadingExamples.Count);
                    Assert.Equal(PlaybookExampleKind.Invalid, f.Vm.ReadingExamples[1].Kind);
                    f.Vm.ReadingExamples[1].Thumbnail = thumbnail;
                    foreach (var provenance in Descendants(view).OfType<Expander>())
                    { Assert.Equal(((SolidColorBrush)app.Resources["PtjTextPrimaryBrush"]).Color, ((SolidColorBrush)provenance.Foreground).Color); Assert.Equal("Example provenance", AutomationProperties.GetName(provenance)); }
                    var image = Descendants(view).OfType<Image>().First(i => ReferenceEquals(i.Source, thumbnail));
                    image.BringIntoView(); Layout(); Render(view, theme, width, dpi, "saved-examples");
                    using var shell = MainWindowViewModelTests.PlaybookShell(f.Vm);
                    shell.NavigateCommand.Execute(NavigationDestination.Playbook); // Re-activation is only for this synthetic shell fixture.
                    f.Edit("owner-close guard");
                    var window = new MainWindow(shell);
                    var args = new CancelEventArgs();
                    typeof(MainWindow).GetMethod("OnClosing", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [args]);
                    Assert.True(args.Cancel); Assert.True(f.Vm.IsEditing); Assert.Equal("owner-close guard", f.Vm.Fields[0].Text);
                    f.Dialogs.ConfirmationResult = true; args = new();
                    typeof(MainWindow).GetMethod("OnClosing", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [args]);
                    Assert.False(args.Cancel); Assert.True(f.Vm.IsEditing);
                    window.DataContext = null; window.Close(); view.DataContext = null;
                }
            }
        });
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static byte[] SyntheticChart(int width = 800, int height = 240)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.DarkSlateGray, null, new Rect(0, 0, width, height));
            for (int i = 0; i < 10; i++) dc.DrawRectangle(i % 3 == 0 ? Brushes.Coral : Brushes.LightGreen, null, new Rect(25 + i * 70, 35 + (i % 3) * 30, 28, 70));
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = new MemoryStream(); encoder.Save(output); return output.ToArray();
    }
    private static void Render(FrameworkElement view, string theme, int width, int dpi, string state)
    {
        if (Environment.GetEnvironmentVariable("PTJ_PLAYBOOK_RENDER_DIRECTORY") is not { Length: > 0 } directory) return;
        Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap(width * dpi / 96, 650 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(view); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(directory, $"playbook-{theme}-{width}-{dpi}-{state}.png")); encoder.Save(file);
    }
}
