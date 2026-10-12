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
                Assert.True(Contrast((SolidColorBrush)app.Resources["PtjAccentBrush"], (SolidColorBrush)app.Resources["PtjSurfaceBrush"]) >= 4.5);
                Assert.True(Contrast((SolidColorBrush)app.Resources["PtjDangerBrush"], (SolidColorBrush)app.Resources["PtjDangerSurfaceBrush"]) >= 4.5);
                foreach (var (width, dpi) in new[] { (960, 96), (480, 240) })
                {
                    var images = new ExampleImagesFake { Preview = thumbnail };
                    var f = new PlaybookFixture(images, images, deletion: new PlaybookValidationDeletionTests.Deletion()); f.Seed(12); f.OpenAsync().GetAwaiter().GetResult();
                    var view = new PlaybookView { DataContext = f.Vm };
                    VisualTreeHelper.SetRootDpi(view, new DpiScale(dpi / 96d, dpi / 96d));
                    void Layout() { view.Measure(new Size(width, 650)); view.Arrange(new Rect(0, 0, width, 650)); view.UpdateLayout(); }
                    Layout();
                    var selector = (ComboBox)view.FindName("SetupSelector");
                    Assert.Equal(f.Setup.Id, ((Application.Setups.TradingSetupListItem)selector.SelectedItem).Id);
                    Assert.Equal("Trading Setup for Playbook", AutomationProperties.GetName(selector));
                    Assert.Equal("Current revision 12", ((TextBlock)view.FindName("Version")).Text);
                    Assert.Equal(Visibility.Collapsed, ((TextBlock)view.FindName("Version")).Visibility);
                    Assert.Equal(Visibility.Visible, ((Border)view.FindName("ReadingPanel")).Visibility);
                    Assert.Contains(Descendants((Border)view.FindName("ReadingPanel")).OfType<TextBlock>(), b => b.Text == "Market context" && b.Visibility == Visibility.Visible);
                    Assert.Same(app.Resources["PtjPlaybookRefreshButtonStyle"], ((Button)view.FindName("RefreshPlaybook")).Style);
                    Assert.Same(app.Resources["PtjPlaybookArchiveButtonStyle"], ((Button)view.FindName("ArchiveSetup")).Style);
                    Assert.Same(app.Resources["PtjPlaybookCreateButtonStyle"], ((Button)view.FindName("EditGuidance")).Style);
                    foreach (string name in new[] { "RefreshPlaybook", "ArchiveSetup", "EditGuidance" })
                    {
                        var action = (Button)view.FindName(name);
                        Assert.InRange(action.TranslatePoint(new Point(action.ActualWidth, 0), view).X, 0, width);
                    }
                    Assert.True(selector.Focusable);
                    double currentCardWidth = ((Border)view.FindName("ReadingPanel")).ActualWidth;
                    Render(view, theme, width, dpi, "current");
                    f.Vm.ViewRevisionCommand.Execute(f.Vm.Revisions[1]); Layout();
                    var returnAction = (Button)view.FindName("ReturnToCurrent");
                    Assert.Same(app.Resources["PtjPlaybookReturnButtonStyle"], returnAction.Style);
                    Assert.NotNull(returnAction.FocusVisualStyle); Assert.True(returnAction.IsEnabled);
                    var history = (ItemsControl)view.FindName("History");
                    var deletes = Descendants(history).OfType<Button>().Where(b => Equals(b.Content, "Delete revision")).ToArray();
                    Assert.Equal(10, deletes.Length);
                    Assert.All(deletes, b => { Assert.Same(f.Vm.DeleteRevisionCommand, b.Command); Assert.True(b.Focusable && b.IsEnabled); Assert.Same(b.DataContext, b.CommandParameter); Assert.Same(app.Resources["PtjPlaybookRemoveButtonStyle"], b.Style); });
                    Assert.All(Descendants(view).OfType<TextBlock>().Where(b => b.Text is "Market context" or "Entry conditions" or "Confirmations" or "Invalidation" or "Execution guidance"),
                        b => Assert.Equal(((SolidColorBrush)app.Resources["PtjAccentBrush"]).Color, ((SolidColorBrush)b.Foreground).Color));
                    Assert.Contains("Historical revision 11", ((TextBlock)view.FindName("Version")).Text);
                    Assert.Equal(Visibility.Visible, ((TextBlock)view.FindName("Version")).Visibility);
                    var reading = (Border)view.FindName("ReadingPanel");
                    var hostPanel = (StackPanel)reading.Parent;
                    var heading = (TextBlock)view.FindName("Version");
                    var actions = (WrapPanel)view.FindName("PlaybookActions");
                    Assert.Same(hostPanel, heading.Parent); // Heading is outside the card.
                    Assert.Equal(TextAlignment.Center, heading.TextAlignment);
                    Assert.InRange(heading.TranslatePoint(new Point(heading.ActualWidth / 2, 0), hostPanel).X - hostPanel.ActualWidth / 2, -.5, .5);
                    Assert.InRange(reading.ActualWidth - currentCardWidth, -.5, .5);
                    Assert.InRange(reading.ActualWidth - hostPanel.ActualWidth, -.5, .5);
                    Assert.InRange(reading.TranslatePoint(new Point(), hostPanel).X, -.5, .5);
                    Assert.True(heading.TranslatePoint(new Point(), hostPanel).Y >= actions.TranslatePoint(new Point(0, actions.ActualHeight), hostPanel).Y + 3.5);
                    Assert.True(reading.TranslatePoint(new Point(), hostPanel).Y >= heading.TranslatePoint(new Point(0, heading.ActualHeight), hostPanel).Y + 11.5);
                    Assert.DoesNotContain(Descendants(reading).OfType<TextBlock>(), b => b.Text == f.Vm.DisplayedRevision!.SetupName || b.Text.StartsWith("Historical revision", StringComparison.Ordinal));
                    Assert.All(Descendants(reading).OfType<TextBlock>(), b => Assert.Equal(TextAlignment.Left, b.TextAlignment));
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
                    fields[1].Text = ""; fields[3].Text = " ";
                    f.Vm.SaveCommand.ExecuteAsync(null).GetAwaiter().GetResult(); Layout();
                    Assert.Equal(Visibility.Visible, ((Border)view.FindName("ValidationSummaryPanel")).Visibility);
                    Assert.Equal(Visibility.Visible, ((Border)view.FindName("FormGuidance")).Visibility);
                    foreach (var invalid in new[] { fields[1], fields[3] })
                    {
                        Assert.True(PersonalTradingJournal.Desktop.Validation.FormValidation.GetIsInvalid(invalid));
                        Assert.Equal("This section is required.", AutomationProperties.GetItemStatus(invalid));
                        Assert.Contains("required", AutomationProperties.GetHelpText(invalid));
                        var chrome = (Border)invalid.Template.FindName("TextBoxChrome", invalid);
                        Assert.Equal(((SolidColorBrush)app.Resources["PtjDangerBrush"]).Color, ((SolidColorBrush)chrome.BorderBrush).Color);
                    }
                    Assert.True(f.Vm.IsEditing); Assert.Equal("Bound edit", fields[0].Text);
                    scroll.ScrollToTop(); Layout(); Render(view, theme, width, dpi, "validation");
                    scroll.ScrollToVerticalOffset(scroll.VerticalOffset + fields[1].TransformToAncestor(scroll).Transform(new Point()).Y - 30); Layout();
                    Render(view, theme, width, dpi, "validation-field");
                    fields[1].Text = "corrected"; fields[3].Text = "corrected"; Layout();
                    Assert.Equal(Visibility.Collapsed, ((Border)view.FindName("ValidationSummaryPanel")).Visibility);
                    Assert.False(PersonalTradingJournal.Desktop.Validation.FormValidation.GetIsInvalid(fields[1]));
                    f.Vm.AttachExampleCommand.ExecuteAsync(null).GetAwaiter().GetResult();
                    var example = f.Vm.EditorExamples.Last();
                    f.Vm.SaveCommand.ExecuteAsync(null).GetAwaiter().GetResult(); Layout();
                    Assert.True(example.HasKindError && example.HasAnnotationError);
                    var invalidKind = Descendants((ItemsControl)view.FindName("ExampleRows")).OfType<ComboBox>().Last();
                    var invalidNotes = Descendants((ItemsControl)view.FindName("ExampleRows")).OfType<TextBox>().Last();
                    foreach (var control in new Control[] { invalidKind, invalidNotes })
                    {
                        Assert.True(PersonalTradingJournal.Desktop.Validation.FormValidation.GetIsInvalid(control));
                        Assert.NotEmpty(AutomationProperties.GetItemStatus(control)); Assert.NotEmpty(AutomationProperties.GetHelpText(control));
                        var chrome = (Border)control.Template.FindName(control is ComboBox ? "ComboBoxChrome" : "TextBoxChrome", control);
                        Assert.Equal(((SolidColorBrush)app.Resources["PtjDangerBrush"]).Color, ((SolidColorBrush)chrome.BorderBrush).Color);
                    }
                    invalidKind.BringIntoView(); Layout(); Render(view, theme, width, dpi, "invalid-example");
                    scroll.ScrollToVerticalOffset(scroll.VerticalOffset + invalidKind.TransformToAncestor(scroll).Transform(new Point()).Y - 32); Layout();
                    Render(view, theme, width, dpi, "invalid-example-fields");
                    invalidKind.SelectedItem = PlaybookExampleKind.Invalid; Layout(); Assert.False(example.HasKindError); Assert.True(example.HasAnnotationError);
                    example.Annotation = "NQ synthetic example: the retest failed its confirmation. Lesson: wait for the stated condition; this label does not evaluate a Trade.";
                    Assert.False(f.Vm.HasValidationErrors);
                    example.Thumbnail = thumbnail;
                    f.Vm.ViewRevisionCommand.Execute(f.Vm.Revisions[1]); Layout();
                    Assert.True(f.Vm.IsEditing && f.Vm.IsHistorical); Assert.Same(example, f.Vm.EditorExamples.Last());
                    Assert.Equal(Visibility.Visible, ((ItemsControl)view.FindName("EditorFields")).Visibility);
                    Assert.True(reading.TranslatePoint(new Point(0, reading.ActualHeight), hostPanel).Y <= ((Border)view.FindName("FormGuidance")).TranslatePoint(new Point(), hostPanel).Y + .5);
                    scroll.ScrollToTop(); Layout(); Render(view, theme, width, dpi, "history-above-draft");
                    f.Vm.ShowCurrentCommand.Execute(null); Layout(); Assert.True(f.Vm.IsEditing);
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
                    {
                        Assert.True(button.Focusable); Assert.InRange(button.ActualWidth, 20, width);
                        Assert.Same(app.Resources[button.Content is "Remove example" ? "PtjPlaybookRemoveButtonStyle" : "PtjPlaybookViewButtonStyle"], button.Style);
                        Assert.InRange(button.TranslatePoint(new Point(button.ActualWidth, 0), view).X, 0, width);
                    }
                    Render(view, theme, width, dpi, "examples");
                    exampleButtons.Last(b => b.Content is "Remove example").BringIntoView(); Layout();
                    Render(view, theme, width, dpi, "example-actions");
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
                    var palette = new WrapPanel { Background = (Brush)app.Resources["PtjSurfaceBrush"] };
                    foreach (var (key, label) in new[] { ("Refresh", "Refresh"), ("Archive", "Archive Setup"), ("Create", "Create guidance"),
                        ("View", "Open full-size chart"), ("Return", "Return to current"), ("Close", "Close image"), ("Remove", "Remove example") })
                    {
                        var button = new Button { Content = label, Style = (Style)app.Resources[$"PtjPlaybook{key}ButtonStyle"] };
                        palette.Children.Add(button); button.ApplyTemplate();
                        Assert.True(Contrast((SolidColorBrush)button.Foreground, (SolidColorBrush)button.Background) >= 4.5, theme + ": " + key);
                        Assert.NotNull(button.Template.FindName("FocusIndicator", button));
                        Assert.True(button.Focusable);
                        var triggers = (key == "Return" ? button.Style.BasedOn : button.Style).Triggers.OfType<Trigger>().ToArray();
                        Assert.Contains(triggers, t => t.Property == UIElement.IsMouseOverProperty);
                        Assert.Contains(triggers, t => t.Property == Button.IsPressedProperty);
                        Assert.Contains(triggers, t => t.Property == UIElement.IsEnabledProperty);
                        foreach (var trigger in triggers.Where(t => t.Property == UIElement.IsMouseOverProperty || t.Property == Button.IsPressedProperty))
                        {
                            var value = Assert.IsType<DynamicResourceExtension>(trigger.Setters.OfType<Setter>().Single().Value);
                            Assert.True(Contrast((SolidColorBrush)button.Foreground, (SolidColorBrush)app.Resources[value.ResourceKey]) >= 4.5,
                                theme + ": " + key + " " + trigger.Property.Name);
                        }
                        button.IsEnabled = false;
                        Assert.Equal(((SolidColorBrush)app.Resources["PtjTextSecondaryBrush"]).Color, ((SolidColorBrush)button.Foreground).Color);
                        button.IsEnabled = true;
                    }
                    VisualTreeHelper.SetRootDpi(palette, new DpiScale(dpi / 96d, dpi / 96d));
                    palette.Measure(new Size(width, 650)); palette.Arrange(new Rect(0, 0, width, 650)); palette.UpdateLayout();
                    foreach (Button button in palette.Children)
                        Assert.InRange(button.TranslatePoint(new Point(button.ActualWidth, button.ActualHeight), palette).X, 0, width);
                    Render(palette, theme, width, dpi, "action-colors");
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
    private static double Contrast(SolidColorBrush foreground, SolidColorBrush background)
    {
        static double Luminance(Color c)
        {
            static double Linear(byte n) { double v = n / 255d; return v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4); }
            return .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B);
        }
        double a = Luminance(foreground.Color), b = Luminance(background.Color);
        return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
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
