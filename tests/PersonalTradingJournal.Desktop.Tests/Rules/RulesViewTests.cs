using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.Navigation;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Validation;
using PersonalTradingJournal.Desktop.Views.Rules;

namespace PersonalTradingJournal.Desktop.Tests.Rules;

public sealed class RulesViewTests
{
    private const string Host = "PTJ_RULES_VIEW_HOST";
    [Fact]
    public async Task CompiledThemesNarrowHighDpiBindingsValidationHistoryAndWindowClose()
    {
        if (Environment.GetEnvironmentVariable(Host) != "1")
        {
            await IsolatedTestProcess.RunSuiteAsync(typeof(RulesViewTests), "rules-view", Host, TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30)); return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (string theme in new[] { "Light", "Dark" })
            {
                app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
                foreach (var (width, dpi) in new[] { (960, 96), (480, 240) })
                {
                    var f = new RulesFixture(); var id = f.Data.Seed(12); f.Open(id).GetAwaiter().GetResult();
                    var view = new RulesView { DataContext = f.Vm }; FocusManager.SetIsFocusScope(view, true);
                    VisualTreeHelper.SetRootDpi(view, new DpiScale(dpi / 96d, dpi / 96d));
                    void Layout() { view.Measure(new Size(width, 700)); view.Arrange(new Rect(0, 0, width, 700)); view.UpdateLayout(); }
                    Layout();
                    var scroller = (ScrollViewer)view.FindName("RulesScroller");
                    var selector = (ComboBox)view.FindName("RuleSelector");
                    Assert.Equal(id, selector.SelectedValue); Assert.Equal("Select Trading Rule", AutomationProperties.GetName(selector));
                    Assert.True(selector.Focusable);
                    Assert.Contains("Current", f.Vm.Message); Assert.Equal("Revision 12 · Active", ((TextBlock)view.FindName("Version")).Text);
                    Render(view, theme, width, dpi, "current");
                    ((Expander)view.FindName("RevisionHistory")).IsExpanded = true; Layout();
                    Assert.Equal(10, ((ItemsControl)view.FindName("HistoryRows")).Items.Count);
                    f.Vm.EditCommand.Execute(null); Layout();
                    f.Vm.Automatic = true; Layout();
                    foreach (string name in new[] { "CheckBasis", "CheckLimit", "CheckCurrency" })
                    {
                        var input = (Control)view.FindName(name); input.BringIntoView(); Flush(); Layout();
                        Assert.True(input.Focusable); Assert.NotEmpty(AutomationProperties.GetName(input));
                        Assert.InRange(input.TranslatePoint(new Point(input.ActualWidth, 0), view).X, 0, width);
                    }
                    Render(view, theme, width, dpi, "typed-check");
                    var fields = Descendants((ItemsControl)view.FindName("EditorFields")).OfType<TextBox>().ToArray();
                    Assert.Equal(3, fields.Length); Assert.Equal(44, fields[0].ActualHeight); Assert.Equal(100, fields[1].ActualHeight);
                    fields[1].SetCurrentValue(TextBox.TextProperty, ""); f.Vm.SaveCommand.ExecuteAsync(null).GetAwaiter().GetResult(); Layout();
                    Assert.True(FormValidation.GetIsInvalid(fields[1])); Assert.Equal("This field is required.", AutomationProperties.GetItemStatus(fields[1]));
                    Assert.Equal(((SolidColorBrush)app.Resources["PtjDangerBrush"]).Color, ((SolidColorBrush)fields[1].BorderBrush).Color);
                    Assert.Equal(Visibility.Visible, ((Border)view.FindName("ValidationSummary")).Visibility);
                    fields[1].BringIntoView(); Flush(); Layout();
                    Assert.InRange(fields[1].TranslatePoint(new Point(), scroller).Y, -1, scroller.ViewportHeight);
                    Render(view, theme, width, dpi, "validation");
                    fields[1].SetCurrentValue(TextBox.TextProperty, "Unsaved criterion"); Layout();
                    Assert.False(FormValidation.GetIsInvalid(fields[1])); Assert.True(AutomationProperties.GetIsRequiredForForm(fields[1]));
                    f.Vm.ViewRevisionCommand.Execute(f.Vm.Revisions.Last()); Layout();
                    Assert.True(f.Vm.IsHistorical); Assert.Equal("Unsaved criterion", fields[1].Text);
                    Assert.Contains("read-only", ((TextBlock)view.FindName("Version")).Text);
                    Assert.Equal("criterion 3", ((TextBlock)view.FindName("ReadCriterion")).Text);
                    Assert.Empty(Descendants((Border)view.FindName("ReadingCard")).OfType<TextBox>());
                    ((Border)view.FindName("ReadingCard")).BringIntoView(); Flush(); Layout(); Render(view, theme, width, dpi, "history");
                    f.Vm.ReturnCommand.Execute(null); Layout();
                    foreach (var name in new[] { "CreateRule", "EditRule", "ArchiveRule", "SaveRule" })
                    {
                        var button = (Button)view.FindName(name); Assert.True(button.Focusable); Assert.NotEmpty(AutomationProperties.GetName(button));
                        Assert.NotNull(button.FocusVisualStyle);
                        Assert.InRange(button.TranslatePoint(new Point(button.ActualWidth, 0), view).X, 0, width);
                    }
                    Assert.InRange(scroller.ExtentWidth, 0, width); Assert.Equal(ScrollBarVisibility.Disabled, scroller.HorizontalScrollBarVisibility);
                    scroller.ScrollToHome(); Flush(); Layout();
                    var wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120) { RoutedEvent = UIElement.PreviewMouseWheelEvent };
                    fields[1].RaiseEvent(wheel); Flush(); Layout();
                    Assert.True(wheel.Handled); Assert.True(scroller.VerticalOffset > 0);
                    double offset = scroller.VerticalOffset; scroller.PageDown(); Flush(); Layout(); Assert.True(scroller.VerticalOffset > offset);
                    var save = (Button)view.FindName("SaveRule"); save.BringIntoView(); Flush(); Layout();
                    Assert.Same(f.Vm.SaveCommand, save.Command);
                    Assert.InRange(save.TranslatePoint(new Point(save.ActualWidth, save.ActualHeight), scroller).Y, 0, scroller.ViewportHeight + 1);
                    Render(view, theme, width, dpi, "editor");
                    f.Vm.Deactivate();
                }
            }
            // Logical field focus and bring-into-view on a real WPF presentation source,
            // without asserting ownership of the Windows foreground keyboard device.
            var focus = new RulesFixture(); focus.Vm.ActivateAsync().GetAwaiter().GetResult();
            var focusView = new RulesView { DataContext = focus.Vm };
            var focusWindow = new Window { Content = focusView, Width = 760, Height = 640, ShowActivated = false };
            focusWindow.Show();
            try
            {
                focus.Vm.CreateCommand.Execute(null); Flush(); focusWindow.UpdateLayout();
                focus.Vm.Fields[0].Text = "Synthetic focus rule";
                focus.Vm.SaveCommand.ExecuteAsync(null).GetAwaiter().GetResult(); Flush(); focusWindow.UpdateLayout();
                var invalid = Descendants(focusView).OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == "Criterion");
                Assert.True(FormValidation.GetIsInvalid(invalid));
                Assert.Same(invalid, FocusManager.GetFocusedElement(focusWindow));
                var scroll = (ScrollViewer)focusView.FindName("RulesScroller");
                Assert.InRange(invalid.TranslatePoint(new Point(), scroll).Y, 0, scroll.ViewportHeight - invalid.ActualHeight + 1);
            }
            finally { focusWindow.Close(); focus.Vm.Deactivate(); }
            // Exercise the actual MainWindow.OnClosing override, without native activation assumptions.
            var dirty = new RulesFixture(); using var shell = MainWindowViewModelTests.RulesShell(dirty.Vm);
            shell.NavigateCommand.Execute(NavigationDestination.Rules); shell.CurrentContentViewModel.ToString(); dirty.Vm.LoadTask.GetAwaiter().GetResult();
            dirty.Vm.CreateCommand.Execute(null); dirty.Fill();
            var window = new MainWindow(shell);
            var closing = typeof(MainWindow).GetMethod("OnClosing", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var veto = new CancelEventArgs(); closing.Invoke(window, [veto]); Assert.True(veto.Cancel); Assert.True(dirty.Vm.IsEditing);
            dirty.Dialogs.ConfirmationResult = true; var accepted = new CancelEventArgs(); closing.Invoke(window, [accepted]);
            Assert.False(accepted.Cancel); Assert.Equal("Recorded criterion", dirty.Vm.Fields[1].Text); window.Close();
        }, "Rules Light/Dark normal/high-DPI and guarded window close");
    }
    private static void Flush()
    {
        var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame);
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
    private static void Render(FrameworkElement view, string theme, int width, int dpi, string state)
    {
        var bitmap = new RenderTargetBitmap(width * dpi / 96, 700 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32); bitmap.Render(view);
        if (Environment.GetEnvironmentVariable("PTJ_RULES_RENDER_DIRECTORY") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(output, $"rules-{theme}-{width}-{dpi}-{state}.png")); encoder.Save(stream);
    }
}
