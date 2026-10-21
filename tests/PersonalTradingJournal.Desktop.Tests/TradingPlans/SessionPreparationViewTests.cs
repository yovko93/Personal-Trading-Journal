using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Validation;
using PersonalTradingJournal.Desktop.ViewModels.TradingPlans;
using PersonalTradingJournal.Desktop.Views.TradingPlans;

namespace PersonalTradingJournal.Desktop.Tests.TradingPlans;

[Collection(ExclusiveWpfCollection.Name)]
[Trait(ExclusiveWpfCollection.Trait, ExclusiveWpfCollection.Value)]
public sealed class SessionPreparationViewTests
{
    private const string Host = "PTJ_PLAN_PREPARATION_VIEW_HOST";
    [Fact]
    public async Task CompiledBindingsAccessibleInputsValidationAndResponsiveLightDarkRenders()
    {
        if (Environment.GetEnvironmentVariable(Host) != "1")
        {
            string? expected = ValidationFocusTrace.Enabled ? $"{typeof(SessionPreparationViewTests).FullName}.{nameof(CompiledBindingsAccessibleInputsValidationAndResponsiveLightDarkRenders)}" : null;
            await IsolatedTestProcess.RunSuiteAsync(typeof(SessionPreparationViewTests), "plan-preparation", Host,
                TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30),
                testCaseFilter: expected is null ? null : IsolatedTestProcess.ExactDisplayNameFilter(expected), expectedTestName: expected);
            return;
        }
        var f = new SessionPreparationTests.Fixture();
        await f.Vm.LoadCatalogAsync();
        await CalendarStaTest.RunAsync(() =>
        {
            using var vm = f.Vm;
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (string theme in new[] { "Light", "Dark" })
            {
                app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
                foreach (var (width, dpi) in new[] { (1040, 96), (460, 240) })
                {
                    vm.InstrumentToAdd = null;
                    vm.Focus.Text = "Wait for confirmation.\nKeep the session focused.";
                    vm.News.Text = "Manual notes: review scheduled restrictions before trading.\nNo external calendar is connected.";
                    vm.Contexts[0].Thesis.Text = "Synthetic context: wait for acceptance around the prior range. A linked plan is not proof of adherence.";
                    vm.Contexts[0].Levels[0].Text = "20123.123456789012345678901234";
                    var section = new SessionPreparationSection { DataContext = vm };
                    var scroll = new ScrollViewer { Content = section, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Background = (Brush)app.Resources["PtjBackgroundBrush"] };
                    VisualTreeHelper.SetRootDpi(scroll, new DpiScale(dpi / 96d, dpi / 96d));
                    void Layout() { scroll.Measure(new Size(width, 1000)); scroll.Arrange(new Rect(0, 0, width, 1000)); scroll.UpdateLayout(); }
                    Layout();
                    var inputs = Descendants(section).OfType<TextBox>().Where(t => t.DataContext is PreparationField).ToArray();
                    Assert.Equal(9, inputs.Length);
                    foreach (var input in inputs)
                    {
                        Assert.True(input.Focusable); Assert.True(input.IsTabStop);
                        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(input)));
                        Assert.InRange(input.TranslatePoint(new Point(input.ActualWidth, 0), section).X, 1, section.ActualWidth);
                    }
                    var focus = inputs.Single(t => AutomationProperties.GetName(t) == "Today's Focus");
                    focus.SetCurrentValue(TextBox.TextProperty, "Bound\nmultiline focus");
                    Assert.Equal("Bound\nmultiline focus", vm.Focus.Text); Assert.True(vm.IsDirty);
                    var context = (StackPanel)section.FindName("ContextFields");
                    var levels = (StackPanel)section.FindName("LevelFields");
                    var origin = levels.TranslatePoint(new Point(), context);
                    Assert.True(width > 760 ? origin.X > context.ActualWidth : origin.Y >= context.ActualHeight);
                    var selector = (ComboBox)section.FindName("ContextSelector");
                    Assert.Equal(vm.SelectedInstrument, selector.SelectedItem);
                    ((Expander)section.FindName("InstrumentPicker")).IsExpanded = true; Layout();
                    var add = Descendants(section).OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Add Instrument");
                    Assert.False(add.IsEnabled);
                    ((ComboBox)section.FindName("CatalogSelector")).SetCurrentValue(ComboBox.SelectedItemProperty, vm.AvailableInstruments.Last());
                    Assert.True(add.IsEnabled);
                    ((Expander)section.FindName("InstrumentPicker")).IsExpanded = false; Layout();
                    Render(scroll, theme, width, dpi, "draft");
                    var price = inputs.Single(t => AutomationProperties.GetName(t) == "Previous Day High");
                    price.SetCurrentValue(TextBox.TextProperty, "not a price"); Layout();
                    Assert.True(FormValidation.GetIsInvalid(price)); Assert.Contains("exact decimal", AutomationProperties.GetHelpText(price));
                    Assert.Equal("not a price", vm.Contexts[0].Levels[0].Text);
                    Assert.False(vm.TryBuildContent(out _));
                    Render(scroll, theme, width, dpi, "invalid");
                    scroll.ScrollToEnd(); Layout();
                    var news = inputs.Single(t => AutomationProperties.GetName(t).StartsWith("News &"));
                    Assert.InRange(news.TranslatePoint(new Point(0, news.ActualHeight), scroll).Y, 1, scroll.ActualHeight);
                    Render(scroll, theme, width, dpi, "bottom");
                    price.SetCurrentValue(TextBox.TextProperty, "0"); Assert.False(FormValidation.GetIsInvalid(price));
                    Assert.Equal(0, vm.Contexts[0].Build().Levels.PreviousDayHigh);
                }
            }
            var readyPlan = f.Store.Plan;
            readyPlan.MarkReady(readyPlan.Version, Domain.TradingPlans.TradingPlanReferenceSnapshot.Capture(readyPlan.Key,
                readyPlan.WorkingDraft!.Content, f.Account, [f.Instrument], [], [], SessionPreparationTests.Fixture.Now), SessionPreparationTests.Fixture.Now);
            using (var readyVm = new SessionPreparationViewModel(readyPlan, f.Store, f.Catalog))
            {
                var reading = new SessionPreparationSection { DataContext = readyVm };
                reading.Measure(new Size(1040, double.PositiveInfinity)); reading.Arrange(new Rect(0, 0, 1040, reading.DesiredSize.Height)); reading.UpdateLayout();
                var textInputs = Descendants(reading).OfType<TextBox>().Where(t => t.DataContext is PreparationField).ToArray();
                Assert.Equal(9, textInputs.Length); Assert.All(textInputs, t => Assert.True(t.IsReadOnly));
                Assert.False(((ComboBox)reading.FindName("BiasSelector")).IsEnabled);
                Assert.False(readyVm.SaveCommand.CanExecute(null));
            }
            // Real compiled owner/focus routing, not a human keyboard or screen-reader test.
            var nativeSection = new SessionPreparationSection { DataContext = vm };
            var window = new Window { Content = new ScrollViewer { Content = nativeSection }, Width = 650, Height = 750 };
            var diagnostic = ValidationFocusTrace.Attach("plan-preparation", window, vm);
            window.Show();
            try
            {
                diagnostic?.Record("shown");
                ValidationFocusOwner.Ready(window, nativeSection);
                diagnostic?.Record("owner-ready-before-validation");
                vm.InstrumentToAdd = f.Catalog.Items[1]; vm.AddInstrumentCommand.Execute(null);
                vm.Contexts[0].Levels[0].Text = "invalid in another context";
                Assert.NotSame(vm.Contexts[0], vm.SelectedInstrument);
                diagnostic?.Record("before-level-save");
                // Selection/bindings must still be changed by validation, not by setup.
                ValidationFocusOwner.AssertReady(window, (FrameworkElement)nativeSection.FindName("LevelFields"));
                var save = vm.SaveAsync(); diagnostic?.Record("level-save-returned", new { save.IsCompleted, save.Status });
                save.GetAwaiter().GetResult(); diagnostic?.Record("level-save-awaited");
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                diagnostic?.Record("level-after-original-idle");
                Assert.Same(vm.Contexts[0], vm.SelectedInstrument);
                var focused = Assert.IsType<TextBox>(System.Windows.Input.Keyboard.FocusedElement);
                Assert.Equal("Previous Day High", AutomationProperties.GetName(focused));
                Assert.True(FormValidation.GetIsInvalid(focused));
            }
            finally { diagnostic?.Record("before-window-close"); window.Close(); diagnostic?.Dispose(); }
        }, shutdownDispatcher: false);
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var next in Descendants(child)) yield return next;
        }
    }
    private static void Render(FrameworkElement element, string theme, int width, int dpi, string state)
    {
        if (Environment.GetEnvironmentVariable("PTJ_PLAN_RENDER_DIRECTORY") is not { Length: > 0 } directory) return;
        Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap(width * dpi / 96, 1000 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(directory, $"preparation-{theme}-{width}-{dpi}-{state}.png")); encoder.Save(file);
    }
}
