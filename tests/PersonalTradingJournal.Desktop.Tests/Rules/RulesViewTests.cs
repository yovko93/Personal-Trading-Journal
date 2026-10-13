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
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (string theme in new[] { "Light", "Dark" })
            {
                app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
                foreach (var (width, dpi) in new[] { (960, 96), (480, 240) })
                {
                    var f = new RulesFixture(); f.Data.Seed(12);
                    var id = Guid.Parse("00000000-0000-0000-0000-000000000001");
                    for (int i = 0; i < f.Data.Rows.Count; i++)
                    {
                        var old = f.Data.Rows[i];
                        f.Data.Rows[i] = PersonalTradingJournal.Domain.Rules.TradingRuleRevision.Rehydrate(id, old.Revision, old.Definition, old.IsActive, old.RecordedAtUtc);
                    }
                    f.Data.Rows[^1] = PersonalTradingJournal.Domain.Rules.TradingRuleRevision.Rehydrate(id, 12,
                        new("A longer synthetic rule name to check wrapping", "A short statement that remains readable beside the right-aligned actions.", "Retained evidence",
                            new(PersonalTradingJournal.Domain.Rules.RuleProfitBasis.StrictNet, 123.456m, "EUR", capability: PersonalTradingJournal.Domain.Rules.RuleCheckCapability.ClosedDayLoss)),
                        true, DateTimeOffset.UtcNow.AddDays(-1));
                    var secondId = Guid.Parse("00000000-0000-0000-0000-000000000002");
                    var thirdId = Guid.Parse("00000000-0000-0000-0000-000000000003");
                    foreach (var other in new[] { secondId, thirdId })
                        f.Data.Rows.Add(PersonalTradingJournal.Domain.Rules.TradingRuleRevision.Create(other, new("Another synthetic principle", "Leave these rows unchanged"), DateTimeOffset.UtcNow.AddDays(-1)));
                    Await(f.Open(id));
                    var view = new RulesView { DataContext = f.Vm }; FocusManager.SetIsFocusScope(view, true);
                    VisualTreeHelper.SetRootDpi(view, new DpiScale(dpi / 96d, dpi / 96d));
                    void Layout() { view.Measure(new Size(width, 400)); view.Arrange(new Rect(0, 0, width, 400)); view.UpdateLayout(); }
                    Layout();
                    var scroller = (ScrollViewer)view.FindName("RulesScroller");
                    Assert.Equal(3, ((ItemsControl)view.FindName("RuleRows")).Items.Count);
                    Assert.Null(view.FindName("RuleSelector")); Assert.Null(view.FindName("RevisionHistory"));
                    Assert.Null(view.FindName("AccountSelector")); Assert.Null(view.FindName("TargetSelector"));
                    var rows = (ItemsControl)view.FindName("RuleRows");
                    var rowGrid = Descendants(rows).OfType<Grid>().Single(g => g.Name == "RuleRowLayout" && g.DataContext is PersonalTradingJournal.Desktop.ViewModels.Rules.RuleRow r && r.Id == id);
                    var rowButtons = Descendants(rowGrid).OfType<Button>().ToArray();
                    Assert.Equal(new[] { "Edit", "Delete" }, rowButtons.Select(b => b.Content));
                    foreach (var button in rowButtons)
                    {
                        Assert.True(button.Focusable); Assert.NotEmpty(AutomationProperties.GetName(button));
                        Assert.InRange(button.TranslatePoint(new Point(button.ActualWidth, 0), view).X, 0, width);
                    }
                    Assert.DoesNotContain(Descendants(view).OfType<TextBlock>(), t => t.IsVisible && (t.Text.Contains("Revision") || t.Text.Contains("Account scope")));

                    Assert.InRange(rowGrid.ActualWidth - rowButtons[1].TranslatePoint(new Point(rowButtons[1].ActualWidth, 0), rowGrid).X, 0, 1);
                    Assert.Equal(rowButtons[0].TranslatePoint(new Point(), rowGrid).Y, rowButtons[1].TranslatePoint(new Point(), rowGrid).Y);
                    Assert.True(rowButtons[0].TranslatePoint(new Point(), rowGrid).X > rowGrid.ActualWidth / 2);
                    var condition = Descendants(rowGrid).OfType<TextBlock>().Single(t => t.Text == f.Vm.SelectedRule!.AutomaticCondition);
                    Assert.Equal(Visibility.Visible, condition.Visibility); Assert.Contains("EUR", condition.Text); Assert.Contains("123.456", condition.Text);
                    Assert.InRange(condition.TranslatePoint(new Point(condition.ActualWidth, 0), rowGrid).X, 0, rowButtons[0].TranslatePoint(new Point(), rowGrid).X - 1);
                    Assert.Null(view.FindName("RulePagination"));
                    Assert.DoesNotContain(Descendants(view).OfType<Button>(), b => Equals(b.Content, "Previous rules") || Equals(b.Content, "Next rules"));
                    Assert.DoesNotContain(Descendants(view).OfType<TextBlock>(), t => t.Text == "Rule editor" || t.Text.StartsWith("Write a general trading principle."));
                    var marker = Find<TextBlock>(rowGrid, "RuleMarker");
                    Assert.False(marker.Focusable); Assert.False(marker.IsHitTestVisible);
                    Assert.Equal("Rule list item", AutomationProperties.GetName(marker));
                    Assert.True(condition.TranslatePoint(new Point(), rowGrid).X >= marker.ActualWidth);
                    var feedback = (Border)view.FindName("FeedbackBox");
                    Assert.True(feedback.TranslatePoint(new Point(0, feedback.ActualHeight), view).Y <= scroller.TranslatePoint(new Point(), view).Y);
                    Assert.Equal(new Thickness(1), feedback.BorderThickness);
                    Assert.Equal(((SolidColorBrush)app.Resources["PtjSuccessSurfaceBrush"]).Color, ((SolidColorBrush)feedback.Background).Color);
                    Render(view, theme, width, dpi, "catalog");
                    f.Vm.EditCommand.Execute(null); Layout();
                    Assert.Equal(((SolidColorBrush)app.Resources["PtjInformationSurfaceBrush"]).Color, ((SolidColorBrush)feedback.Background).Color);
                    var inline = Descendants(rowGrid).OfType<ContentControl>().Single(c => c.Name == "InlineEditorHost");
                    Assert.Same(f.Vm, inline.Content);
                    Assert.False(((ContentControl)view.FindName("CreateEditorHost")).HasContent);
                    Assert.InRange(inline.TranslatePoint(new Point(), rowGrid).Y, rowGrid.RowDefinitions[0].ActualHeight, rowGrid.ActualHeight);
                    var nextRow = Descendants(rows).OfType<Grid>().Single(g => g.Name == "RuleRowLayout" && g.DataContext is PersonalTradingJournal.Desktop.ViewModels.Rules.RuleRow r && r.Id == secondId);
                    Assert.True(inline.TranslatePoint(new Point(0, inline.ActualHeight), rows).Y < nextRow.TranslatePoint(new Point(), rows).Y);
                    foreach (string name in new[] { "CheckCapability", "CheckBasis", "CheckLimit", "CheckCurrency" })
                    {
                        Assert.Null(view.FindName(name));
                    }
                    Assert.Empty(Descendants(view).OfType<CheckBox>());
                    Assert.DoesNotContain(Descendants(view).OfType<Label>(), l => Equals(l.Content, "Required evidence"));
                    var fields = Descendants(Find<ItemsControl>(view, "EditorFields")).OfType<TextBox>().ToArray();
                    Assert.Equal(2, fields.Length); Assert.Equal(44, fields[0].ActualHeight); Assert.Equal(100, fields[1].ActualHeight);
                    fields[1].SetCurrentValue(TextBox.TextProperty, ""); Await(f.Vm.SaveCommand.ExecuteAsync(null)); Layout();
                    Assert.Equal(((SolidColorBrush)app.Resources["PtjDangerSurfaceBrush"]).Color, ((SolidColorBrush)feedback.Background).Color);
                    Assert.True(FormValidation.GetIsInvalid(fields[1])); Assert.Equal("This field is required.", AutomationProperties.GetItemStatus(fields[1]));
                    Assert.Equal(((SolidColorBrush)app.Resources["PtjDangerBrush"]).Color, ((SolidColorBrush)fields[1].BorderBrush).Color);
                    Assert.Equal(Visibility.Visible, Find<Border>(view, "ValidationSummary").Visibility);
                    fields[1].BringIntoView(); Flush(); Layout();
                    Assert.InRange(fields[1].TranslatePoint(new Point(), scroller).Y, -1, scroller.ViewportHeight);
                    Render(view, theme, width, dpi, "validation");
                    fields[1].SetCurrentValue(TextBox.TextProperty, "Unsaved criterion"); Layout();
                    Assert.False(FormValidation.GetIsInvalid(fields[1])); Assert.True(AutomationProperties.GetIsRequiredForForm(fields[1]));
                    foreach (var name in new[] { "CreateRule", "SaveRule" })
                    {
                        var button = Find<Button>(view, name); Assert.True(button.Focusable); Assert.NotEmpty(AutomationProperties.GetName(button));
                        Assert.NotNull(button.FocusVisualStyle);
                        Assert.InRange(button.TranslatePoint(new Point(button.ActualWidth, 0), view).X, 0, width);
                    }
                    Assert.InRange(scroller.ExtentWidth, 0, width); Assert.Equal(ScrollBarVisibility.Disabled, scroller.HorizontalScrollBarVisibility);
                    scroller.ScrollToHome(); Flush(); Layout();
                    var wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120) { RoutedEvent = UIElement.PreviewMouseWheelEvent };
                    fields[1].RaiseEvent(wheel); Flush(); Layout();
                    Assert.True(wheel.Handled); Assert.True(scroller.VerticalOffset > 0);
                    double offset = scroller.VerticalOffset; scroller.PageDown(); Flush(); Layout(); Assert.True(scroller.VerticalOffset > offset);
                    var save = Find<Button>(view, "SaveRule"); save.BringIntoView(); Flush(); Layout();
                    Assert.Same(f.Vm.SaveCommand, save.Command);
                    Assert.InRange(save.TranslatePoint(new Point(save.ActualWidth, save.ActualHeight), scroller).Y, 0, scroller.ViewportHeight + 1);
                    Render(view, theme, width, dpi, "editor");
                    // Guarded switching leaves this exact inline form and draft intact on veto.
                    Await(f.Vm.EditRowCommand.ExecuteAsync(f.Vm.Rules.Single(r => r.Id == secondId))); Layout();
                    Assert.Same(f.Vm, inline.Content); Assert.Equal("Unsaved criterion", fields[1].Text);
                    f.Dialogs.ConfirmationResult = true;
                    Await(f.Vm.EditRowCommand.ExecuteAsync(f.Vm.Rules.Single(r => r.Id == secondId))); Layout();
                    Assert.False(inline.HasContent);
                    var nextHost = Descendants(nextRow).OfType<ContentControl>().Single(c => c.Name == "InlineEditorHost");
                    Assert.Same(f.Vm, nextHost.Content);
                    Assert.Single(Descendants(view).OfType<StackPanel>(), p => p.Name == "Editor");
                    f.Vm.CancelEditCommand.Execute(null); Layout(); Assert.False(nextHost.HasContent);
                    f.Vm.CreateCommand.Execute(null); Layout();
                    var create = (ContentControl)view.FindName("CreateEditorHost");
                    Assert.Same(f.Vm, create.Content);
                    Assert.True(create.TranslatePoint(new Point(0, create.ActualHeight), view).Y <= rows.TranslatePoint(new Point(), view).Y);
                    Assert.All(f.Vm.Fields, field => Assert.Empty(field.Text));
                    scroller.ScrollToHome(); Flush(); Layout(); Render(view, theme, width, dpi, "create");
                    f.Fill(); Await(f.Vm.SaveCommand.ExecuteAsync(null)); Layout();
                    Assert.False(create.HasContent); Assert.DoesNotContain(Descendants(view).OfType<StackPanel>(), p => p.Name == "Editor");
                    f.Vm.Deactivate();
                }
            }
            // Logical field focus and bring-into-view on a real WPF presentation source,
            // without asserting ownership of the Windows foreground keyboard device.
            var clock = new FeedbackClock(); var focus = new RulesFixture(clock); Await(focus.Vm.ActivateAsync());
            var focusView = new RulesView { DataContext = focus.Vm };
            var focusWindow = new Window { Content = focusView, Width = 760, Height = 640, ShowActivated = false };
            focusWindow.Show();
            try
            {
                focus.Vm.CreateCommand.Execute(null); Flush(); focusWindow.UpdateLayout();
                focus.Vm.Fields[0].Text = "Synthetic focus rule";
                Await(focus.Vm.SaveCommand.ExecuteAsync(null)); Flush(); focusWindow.UpdateLayout();
                var invalid = Descendants(focusView).OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == "Criterion");
                Assert.True(FormValidation.GetIsInvalid(invalid));
                Assert.Same(invalid, FocusManager.GetFocusedElement(focusWindow));
                clock.Advance(10); Await(focus.Vm.FeedbackExpiryTask); Flush(); focusWindow.UpdateLayout();
                Assert.False(focus.Vm.HasMessage); Assert.True(FormValidation.GetIsInvalid(invalid));
                Assert.Same(invalid, FocusManager.GetFocusedElement(focusWindow));
                var scroll = (ScrollViewer)focusView.FindName("RulesScroller");
                Assert.InRange(invalid.TranslatePoint(new Point(), scroll).Y, 0, scroll.ViewportHeight - invalid.ActualHeight + 1);
                focus.Fill(); Await(focus.Vm.SaveCommand.ExecuteAsync(null)); Flush(); focusWindow.UpdateLayout();
                Await(focus.Vm.EditRowCommand.ExecuteAsync(Assert.Single(focus.Vm.Rules))); Flush(); focusWindow.UpdateLayout();
                var selectedRow = Descendants(focusView).OfType<Grid>().Single(g => g.Name == "RuleRowLayout");
                Assert.InRange(selectedRow.TranslatePoint(new Point(), scroll).Y, 0, scroll.ViewportHeight);
                // A clean Edit -> Create transition keeps IsEditing true but changes the form host.
                focus.Vm.CreateCommand.Execute(null); Flush(); focusWindow.UpdateLayout();
                var createName = Descendants((ContentControl)focusView.FindName("CreateEditorHost")).OfType<TextBox>().First();
                Assert.Empty(createName.Text); Assert.Same(createName, FocusManager.GetFocusedElement(focusWindow));
                Assert.InRange(createName.TranslatePoint(new Point(), scroll).Y, 0, scroll.ViewportHeight - createName.ActualHeight + 1);
            }
            finally { focusWindow.Close(); focus.Vm.Deactivate(); }
            // Exercise the actual MainWindow.OnClosing override, without native activation assumptions.
            var dirty = new RulesFixture(); using var shell = MainWindowViewModelTests.RulesShell(dirty.Vm);
            shell.NavigateCommand.Execute(NavigationDestination.Rules); shell.CurrentContentViewModel.ToString(); Await(dirty.Vm.LoadTask);
            dirty.Vm.CreateCommand.Execute(null); dirty.Fill();
            var window = new MainWindow(shell);
            var closing = typeof(MainWindow).GetMethod("OnClosing", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var veto = new CancelEventArgs(); closing.Invoke(window, [veto]); Assert.True(veto.Cancel); Assert.True(dirty.Vm.IsEditing);
            dirty.Dialogs.ConfirmationResult = true; var accepted = new CancelEventArgs(); closing.Invoke(window, [accepted]);
            Assert.False(accepted.Cancel); Assert.Equal("Recorded criterion", dirty.Vm.Fields[1].Text); window.Close();
        }, "Rules Light/Dark normal/high-DPI and guarded window close");
    }
    private static T Find<T>(DependencyObject root, string name) where T : FrameworkElement =>
        Descendants(root).OfType<T>().Single(e => e.Name == name);

    private static void Await(Task task)
    {
        // Await actual completion while servicing the same UI dispatcher; no sleeps or retries.
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            var dispatcher = Dispatcher.CurrentDispatcher;
            _ = task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)), TaskScheduler.Default);
            Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult(); // The existing supervised 30-second STA deadline still applies.
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
        var bitmap = new RenderTargetBitmap(width * dpi / 96, (int)view.ActualHeight * dpi / 96, dpi, dpi, PixelFormats.Pbgra32); bitmap.Render(view);
        if (Environment.GetEnvironmentVariable("PTJ_RULES_RENDER_DIRECTORY") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(output, $"rules-{theme}-{width}-{dpi}-{state}.png")); encoder.Save(stream);
    }
}
