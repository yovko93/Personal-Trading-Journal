using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Validation;
using PersonalTradingJournal.Desktop.ViewModels.TradingPlans;
using PersonalTradingJournal.Desktop.Views.TradingPlans;
using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Desktop.Tests.TradingPlans;

public sealed class ScenariosViewTests
{
    private const string Host = "PTJ_PLAN_SCENARIOS_VIEW_HOST";
    [Fact]
    public async Task CompiledScenarioCardsPreserveBindingsOrderValidationFocusAndLightDarkLayouts()
    {
        if (Environment.GetEnvironmentVariable(Host) != "1")
        {
            await IsolatedTestProcess.RunSuiteAsync(typeof(ScenariosViewTests), "plan-scenarios", Host,
                TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30)); return;
        }
        var fixtures = new Queue<SessionPreparationTests.Fixture>();
        for (int i = 0; i < 4; i++)
        {
            var fixture = new SessionPreparationTests.Fixture(); await fixture.Vm.LoadCatalogAsync();
            var books = new ScenarioTests.Books(); fixture.Vm.ConfigurePlaybooks(books, books, books);
            await fixture.Vm.LoadPlaybooksAsync(); fixture.Vm.Scenarios[0].Candidate = fixture.Vm.AvailablePlaybooks[0];
            await fixture.Vm.Scenarios[0].LinkCommand.ExecuteAsync(null);
            fixtures.Enqueue(fixture);
        }
        await CalendarStaTest.RunAsync(() =>
        {
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (string theme in new[] { "Light", "Dark" })
            foreach (var (width, dpi) in new[] { (1100, 96), (460, 240) })
            {
                app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
                var f = fixtures.Dequeue(); using var vm = f.Vm;
                vm.AddScenarioCommand.Execute(null); vm.AddScenarioCommand.Execute(null);
                vm.Scenarios[1].Direction = PersonalTradingJournal.Domain.Trades.TradeDirection.Short;
                foreach (var row in vm.Scenarios)
                {
                    row.Name.Text = "Synthetic conditional scenario";
                    row.Condition.Text = string.Join('\n', Enumerable.Repeat("Wait for observable acceptance; this is authored preparation, not verified adherence.", 9));
                    row.Response.Text = "Wait for confirmation.\nKeep the original conditions visible.";
                    row.Invalidation.Text = "Stand aside if the condition fails.";
                }
                var section = new ScenariosSection { DataContext = vm };
                var scroll = new ScrollViewer { Content = section, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Background = (Brush)app.Resources["PtjBackgroundBrush"] };
                VisualTreeHelper.SetRootDpi(scroll, new DpiScale(dpi / 96d, dpi / 96d));
                void Layout() { scroll.Measure(new Size(width, 1000)); scroll.Arrange(new Rect(0, 0, width, 1000)); scroll.UpdateLayout(); }
                Layout(); Layout();
                var inputs = Descendants(section).OfType<TextBox>().Where(t => t.DataContext is PreparationField).ToArray();
                Assert.Equal(13, inputs.Length);
                foreach (var input in inputs)
                {
                    Assert.True(input.Focusable); Assert.True(input.IsTabStop); Assert.NotEmpty(AutomationProperties.GetName(input));
                    Assert.InRange(input.TranslatePoint(new Point(input.ActualWidth, 0), section).X, 1, section.ActualWidth);
                }
                var cards = (ItemsControl)section.FindName("ScenarioCards");
                var first = (FrameworkElement)cards.ItemContainerGenerator.ContainerFromIndex(0);
                var second = (FrameworkElement)cards.ItemContainerGenerator.ContainerFromIndex(1);
                var third = (FrameworkElement)cards.ItemContainerGenerator.ContainerFromIndex(2);
                var offset = second.TranslatePoint(new Point(), first);
                Assert.True(width > 900 ? offset.X >= first.ActualWidth - 1 && Math.Abs(offset.Y) < 1 : offset.Y >= first.ActualHeight - 1);
                Assert.True(third.TranslatePoint(new Point(), first).Y >= first.ActualHeight - 1);
                var condition = inputs.Single(t => ReferenceEquals(t.DataContext, vm.Scenarios[0].Condition));
                condition.SetCurrentValue(TextBox.TextProperty, "Bound\nIF field"); Assert.Equal("Bound\nIF field", vm.Scenarios[0].Condition.Text);
                var moved = vm.Scenarios[1];
                Descendants(second).OfType<Expander>().Single().IsExpanded = true; Layout();
                var up = Descendants(second).OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Move scenario earlier");
                Assert.True(up.IsEnabled); up.Command.Execute(up.CommandParameter); Layout(); Assert.Same(moved, vm.Scenarios[0]);
                Render(scroll, theme, width, dpi, "multiple");
                moved.Condition.Text = new('x', 4001); Layout();
                var invalid = Descendants(section).OfType<TextBox>().Single(t => ReferenceEquals(t.DataContext, moved.Condition));
                Assert.True(FormValidation.GetIsInvalid(invalid)); Assert.Contains("4000", AutomationProperties.GetHelpText(invalid));
                Render(scroll, theme, width, dpi, "invalid");
                moved.Condition.Text = "corrected"; Assert.False(FormValidation.GetIsInvalid(invalid));
                scroll.ScrollToEnd(); Layout(); Render(scroll, theme, width, dpi, "bottom");
                foreach (var row in vm.Scenarios.ToArray()) row.RemoveCommand.Execute(null);
                vm.IsStandAside = true; vm.StandAside.Text = "No confirmation. No entries today.";
                scroll.ScrollToTop(); Layout();
                Assert.Empty(vm.Scenarios); Assert.Empty(Descendants(section).OfType<ComboBox>());
                Render(scroll, theme, width, dpi, "stand-aside");

                var readyFixture = new SessionPreparationTests.Fixture(); using var unused = readyFixture.Vm;
                var readyPlan = readyFixture.Store.Plan;
                readyPlan.MarkReady(readyPlan.Version, TradingPlanReferenceSnapshot.Capture(readyPlan.Key, readyPlan.WorkingDraft!.Content,
                    readyFixture.Account, [readyFixture.Instrument], [], [], SessionPreparationTests.Fixture.Now), SessionPreparationTests.Fixture.Now);
                using var reading = new SessionPreparationViewModel(readyPlan, readyFixture.Store, readyFixture.Catalog);
                section.DataContext = reading; Layout();
                Assert.All(Descendants(section).OfType<TextBox>(), t => Assert.True(t.IsReadOnly));
                Assert.All(Descendants(section).OfType<ComboBox>(), c => Assert.False(c.IsEnabled));
                Render(scroll, theme, width, dpi, "ready");
            }
            // Both reusable sections share one real owner and one first-invalid-field focus intent.
            var focusFixture = new SessionPreparationTests.Fixture(); using var focusVm = focusFixture.Vm;
            var combined = new StackPanel();
            combined.Children.Add(new SessionPreparationSection { DataContext = focusVm });
            combined.Children.Add(new ScenariosSection { DataContext = focusVm });
            var ownerScroll = new ScrollViewer { Content = combined, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var window = new Window { Content = ownerScroll, Width = 650, Height = 750 };
            window.Show();
            try
            {
                focusVm.Scenarios[0].Response.Text = new('x', 4001);
                Assert.Equal(PersonalTradingJournal.Application.TradingPlans.TradingPlanWriteStatus.Invalid, focusVm.SaveAsync().GetAwaiter().GetResult());
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                var input = Assert.IsType<TextBox>(Keyboard.FocusedElement);
                Assert.Same(focusVm.Scenarios[0].Response, input.DataContext); Assert.True(FormValidation.GetIsInvalid(input));
                Assert.True(ownerScroll.VerticalOffset > 0); Assert.Equal(0, focusFixture.Store.Writes);
            }
            finally { window.Close(); }
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
        bitmap.Render(element); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(directory, $"scenarios-{theme}-{width}-{dpi}-{state}.png")); encoder.Save(file);
    }
}
