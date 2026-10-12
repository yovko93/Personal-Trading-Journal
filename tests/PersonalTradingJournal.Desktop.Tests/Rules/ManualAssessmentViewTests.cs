using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.Dialogs;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Validation;
using PersonalTradingJournal.Desktop.Views.Rules;

namespace PersonalTradingJournal.Desktop.Tests.Rules;

public sealed class ManualAssessmentViewTests
{
    private const string Host = "PTJ_MANUAL_ASSESSMENT_HOST";
    [Fact]
    public async Task CompiledDialogThemesHighDpiValidationFocusScrollingAndOwnerCloseGuard()
    {
        if (Environment.GetEnvironmentVariable(Host) != "1")
        {
            await IsolatedTestProcess.RunSuiteAsync(typeof(ManualAssessmentViewTests), "manual-assessment-view", Host, TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30)); return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (string theme in new[] { "Light", "Dark" })
            {
                app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
                foreach (var (width, dpi) in new[] { (960, 96), (480, 240) })
                {
                    var f = new AssessmentFixture(); f.Open().GetAwaiter().GetResult();
                    var view = new RuleAssessmentView { DataContext = f.Vm }; FocusManager.SetIsFocusScope(view, true);
                    VisualTreeHelper.SetRootDpi(view, new DpiScale(dpi / 96d, dpi / 96d));
                    void Layout() { view.Measure(new Size(width, 720)); view.Arrange(new Rect(0, 0, width, 720)); view.UpdateLayout(); }
                    Layout(); var scroll = (ScrollViewer)view.FindName("PageScroll");
                    var run = (Button)view.FindName("RunChecks");
                    Assert.Same(f.Vm.RunChecksCommand, run.Command); Assert.True(run.Focusable);
                    Assert.NotEmpty(AutomationProperties.GetName(run)); Assert.NotNull(run.FocusVisualStyle);
                    Assert.InRange(run.TranslatePoint(new Point(run.ActualWidth, 0), view).X, 0, width);
                    Assert.Equal(f.Data.RuleId, ((ComboBox)view.FindName("RuleSelector")).SelectedValue);
                    Assert.Contains("Not assessed", ((TextBlock)view.FindName("LatestState")).Text);
                    var explanation = (TextBox)view.FindName("Explanation");
                    Assert.True(AutomationProperties.GetIsRequiredForForm(explanation));
                    f.Vm.SaveCommand.ExecuteAsync(null).GetAwaiter().GetResult(); Flush(); Layout();
                    Assert.True(FormValidation.GetIsInvalid(explanation)); Assert.NotEmpty(AutomationProperties.GetItemStatus(explanation));
                    Assert.Same(explanation, FocusManager.GetFocusedElement(view));
                    Assert.InRange(explanation.TranslatePoint(new Point(), scroll).Y, -1, scroll.ViewportHeight);
                    Assert.Equal(((SolidColorBrush)app.Resources["PtjDangerBrush"]).Color, ((SolidColorBrush)explanation.BorderBrush).Color);
                    Render(view, theme, width, dpi, "validation");
                    explanation.SetCurrentValue(TextBox.TextProperty, "Required commission evidence is missing");
                    f.Vm.Evidence[0].Selected = true; Layout(); Assert.False(FormValidation.GetIsInvalid(explanation));
                    var save = (Button)view.FindName("SaveAssessment"); save.BringIntoView(); Flush(); Layout();
                    Assert.Same(f.Vm.SaveCommand, save.Command); Assert.True(save.IsEnabled); Assert.True(save.Focusable); Assert.NotNull(save.FocusVisualStyle);
                    Assert.InRange(save.TranslatePoint(new Point(save.ActualWidth, 0), view).X, 0, width);
                    Assert.InRange(save.TranslatePoint(new Point(0, save.ActualHeight), scroll).Y, 0, scroll.ViewportHeight + 1);
                    f.Vm.SaveCommand.ExecuteAsync(null).GetAwaiter().GetResult(); Layout();
                    Assert.Single(((ItemsControl)view.FindName("HistoryRows")).Items);
                    Assert.Contains("Cannot evaluate", ((TextBlock)view.FindName("LatestState")).Text);
                    ((ItemsControl)view.FindName("HistoryRows")).BringIntoView(); Flush(); Layout(); Render(view, theme, width, dpi, "history");
                    scroll.ScrollToHome(); Flush(); Layout(); Render(view, theme, width, dpi, "overview");
                    var wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120) { RoutedEvent = UIElement.PreviewMouseWheelEvent };
                    ((TextBlock)view.FindName("TargetLabel")).RaiseEvent(wheel); Flush(); Layout();
                    Assert.True(wheel.Handled); Assert.True(scroll.VerticalOffset > 0);
                    Assert.InRange(scroll.ExtentWidth, 0, width); Assert.Equal(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
                    var close = (Button)view.FindName("CloseAssessment"); Assert.True(close.Focusable); Assert.NotEmpty(AutomationProperties.GetName(close));
                    Assert.InRange(close.TranslatePoint(new Point(close.ActualWidth, close.ActualHeight), view).Y, 0, 720);
                    f.Vm.Deactivate();
                }
            }
            // The actual owned ShowDialog path, with logical focus restoration and no
            // dependence on which native window owns the runner's keyboard/pointer.
            var fixture = new AssessmentFixture(); fixture.Dialogs.ConfirmationResult = false;
            using var dialogPhase = CalendarStaTest.Phase("owned manual dialog lifecycle");
            var focusButton = new Button { Content = "Synthetic origin" };
            var root = new Window { Width = 900, Height = 740, ShowActivated = false }; root.Show();
            var owner = new Window { Owner = root, Content = focusButton, Width = 800, Height = 680, ShowActivated = false };
            app.MainWindow = owner; owner.Show(); FocusManager.SetFocusedElement(owner, focusButton);
            Exception? error = null;
            Action interact = () =>
            {
                WpfStepDiagnostics.Write("manual dialog callback entered");
                var child = app.Windows.OfType<Window>().Single(w => w.Content is RuleAssessmentView);
                try
                {
                    Assert.True(fixture.Vm.CanRecord); fixture.Fill();
                    WpfStepDiagnostics.Write($"manual loaded: busy={fixture.Vm.IsBusy}; canRecord={fixture.Vm.CanRecord}");
                    child.Close(); WpfStepDiagnostics.Write("child close veto returned"); Assert.True(child.IsVisible); Assert.Equal("Recorded sources are incomplete", fixture.Vm.Explanation);
                    owner.Close(); WpfStepDiagnostics.Write("owner close veto returned"); Assert.True(owner.IsVisible); Assert.True(child.IsVisible);
                    root.Close(); Assert.True(root.IsVisible); Assert.True(owner.IsVisible); Assert.True(child.IsVisible);
                    Assert.Equal(fixture.Data.Target, fixture.Vm.Target); Assert.Empty(fixture.Data.Saved);
                    fixture.Dialogs.ConfirmationResult = true; root.Close(); WpfStepDiagnostics.Write("ancestor close accepted returned");
                    Assert.False(owner.IsVisible); Assert.False(child.IsVisible); Assert.Empty(fixture.Data.Saved);
                }
                catch (Exception ex) { error = ex; }
                finally { fixture.Dialogs.ConfirmationResult = true; fixture.Vm.Explanation = ""; child.Close(); owner.Close(); root.Close(); WpfStepDiagnostics.Write("dialog callback cleanup returned"); }
            };
            // ShowWindow can pump queued work before ShowDialog enters its modal frame.
            // Only operate after observable modal readiness, not merely Window.IsVisible.
            EventHandler? entered = null;
            entered = (_, _) =>
            {
                ComponentDispatcher.EnterThreadModal -= entered;
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, interact);
            };
            ComponentDispatcher.EnterThreadModal += entered;
            try { new WpfRuleAssessmentDialog(() => fixture.Vm).ShowTarget(fixture.Data.Target); }
            finally { ComponentDispatcher.EnterThreadModal -= entered; }
            if (error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
        }, "Manual assessments compiled themes, guarded owned dialog");
    }
    private static void Flush()
    {
        var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame);
    }
    private static void Render(FrameworkElement view, string theme, int width, int dpi, string state)
    {
        var bitmap = new RenderTargetBitmap(width * dpi / 96, 720 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32); bitmap.Render(view);
        if (Environment.GetEnvironmentVariable("PTJ_RULES_RENDER_DIRECTORY") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(output, $"manual-rules-{theme}-{width}-{dpi}-{state}.png")); encoder.Save(stream);
    }
}
