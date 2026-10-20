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

public sealed class RiskReadinessViewTests
{
    private const string Host = "PTJ_PLAN_RISK_VIEW_HOST";
    [Fact]
    public async Task CompiledRiskRulesChecklistAndCrossSectionFocusUseLightDarkResponsiveBindings()
    {
        if (Environment.GetEnvironmentVariable(Host) != "1")
        {
            await IsolatedTestProcess.RunSuiteAsync(typeof(RiskReadinessViewTests), "plan-risk-readiness", Host,
                TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30)); return;
        }
        var fixtures = new Queue<(RiskReadinessTests.Fixture Fixture, SessionPreparationViewModel Ready)>();
        for (int i = 0; i < 4; i++)
        {
            var f = new RiskReadinessTests.Fixture();
            var r = f.Rules.Revisions[0].Revise(1, new(new string('R', 120), string.Join('\n', Enumerable.Repeat("Synthetic Rule: wait for recorded confirmation. A planning link is not an assessment.", 6))), RiskReadinessTests.Fixture.Now);
            f.Rules.Revisions[0] = r; f.Rules.History.Add(r);
            await f.Vm.LoadCatalogAsync(); await f.Vm.LoadRulesAsync(); f.Vm.RuleCandidate = f.Vm.RuleChoices[0]; await f.Vm.LinkRuleAsync();
            await f.Vm.CheckReferencesAsync(); f.Vm.RiskPerTrade.Text = "25.12345678901234567890123456";
            f.Vm.MaximumTrades.Text = "3"; f.Vm.ConsecutiveLosses.Text = "2";
            foreach (var item in f.Vm.Checklist) item.IsChecked = true;
            Assert.Equal(PersonalTradingJournal.Application.TradingPlans.TradingPlanWriteStatus.Success, await f.Vm.SaveAsync());
            var saved = f.Base.Store.Plan;
            var ready = TradingPlan.Rehydrate(saved.Id, saved.Key, saved.Version, saved.WorkingDraft, saved.LatestReadyRevision, saved.CreatedAtUtc, saved.UpdatedAtUtc);
            ready.MarkReady(ready.Version, f.References.Snapshot(ready.WorkingDraft!.Content), RiskReadinessTests.Fixture.Now);
            var reading = new SessionPreparationViewModel(ready, f.Base.Store, f.Base.Catalog);
            reading.ConfigureRules(f.Rules, f.Rules); await reading.LoadRulesAsync();
            fixtures.Enqueue((f, reading));
        }
        await CalendarStaTest.RunAsync(() =>
        {
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (string theme in new[] { "Light", "Dark" })
            foreach (var (width, dpi) in new[] { (1100, 96), (460, 240) })
            {
                app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
                var entry = fixtures.Dequeue(); using var f = entry.Fixture; using var ready = entry.Ready; var vm = f.Vm;
                foreach (var field in vm.RiskFields) field.Text = "";
                foreach (var item in vm.Checklist) item.IsChecked = false;
                var section = new RiskReadinessSection { DataContext = vm };
                var scroll = new ScrollViewer { Content = section, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Background = (Brush)app.Resources["PtjBackgroundBrush"] };
                VisualTreeHelper.SetRootDpi(scroll, new DpiScale(dpi / 96d, dpi / 96d));
                void Layout() { scroll.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); scroll.Measure(new Size(width, 1000)); scroll.Arrange(new Rect(0, 0, width, 1000)); scroll.UpdateLayout(); }
                Layout();
                var inputs = Descendants(section).OfType<TextBox>().Where(t => t.DataContext is PreparationField).ToArray(); Assert.Equal(4, inputs.Length);
                Assert.All(inputs, input => { Assert.True(input.IsTabStop); Assert.NotEmpty(AutomationProperties.GetName(input));
                    Assert.InRange(input.TranslatePoint(new Point(input.ActualWidth, 0), section).X, 1, section.ActualWidth); });
                var left = (StackPanel)section.FindName("LimitsAndRules");
                Assert.InRange(left.ActualWidth, section.ActualWidth - 40, section.ActualWidth);
                Assert.Null(section.FindName("ChecklistAndReadiness"));
                Assert.Equal(0, vm.CompletedConfirmations); Render(scroll, theme, width, dpi, "empty");
                var amount = inputs.Single(t => ReferenceEquals(t.DataContext, vm.DailyLossLimit));
                amount.SetCurrentValue(TextBox.TextProperty, "0.00"); vm.ConsecutiveLosses.Text = "2";
                Assert.Equal("0.00", vm.DailyLossLimit.Text); Assert.Equal(0, vm.CompletedConfirmations);
                var boxes = Descendants(section).OfType<CheckBox>().ToArray(); Assert.Empty(boxes);
                Assert.Empty(Descendants(section).OfType<ProgressBar>());
                Assert.Equal("0.00", amount.Text); Render(scroll, theme, width, dpi, "partial");
                amount.SetCurrentValue(TextBox.TextProperty, "-1"); vm.MaximumTrades.Text = "1.5"; Layout();
                Assert.True(FormValidation.GetIsInvalid(amount)); Assert.Contains("0 or more", AutomationProperties.GetHelpText(amount));
                Assert.All(boxes, b => Assert.False(b.IsEnabled)); Render(scroll, theme, width, dpi, "invalid");
                amount.SetCurrentValue(TextBox.TextProperty, "0.00"); vm.MaximumTrades.Text = "3"; Layout(); Assert.False(FormValidation.GetIsInvalid(amount));
                var rules = Descendants(section).OfType<Expander>().Single(e => AutomationProperties.GetName(e) == "Linked Trading Rules");
                rules.IsExpanded = true; Layout();
                var list = (ItemsControl)section.FindName("LinkedRuleList");
                Assert.Empty(Descendants(list).OfType<Expander>());
                var unlink = Assert.Single(Descendants(list).OfType<Button>());
                Assert.Equal("Unlink rule: " + vm.LinkedRules[0].Name, AutomationProperties.GetName(unlink));
                Assert.True(unlink.IsTabStop); Assert.True(unlink.IsEnabled);
                var name = Descendants(list).OfType<TextBlock>().Single(t => t.Text == vm.LinkedRules[0].Name);
                Assert.Contains("Synthetic Rule", AutomationProperties.GetHelpText(name));
                Assert.True(name.Focusable); Assert.False(vm.LinkedRules[0].HasAvailabilityWarning);
                Assert.True(name.TranslatePoint(new Point(name.ActualWidth, 0), list).X <= unlink.TranslatePoint(new Point(), list).X);
                Assert.InRange(unlink.TranslatePoint(new Point(unlink.ActualWidth, 0), list).X, list.ActualWidth - 10, list.ActualWidth);
                scroll.ScrollToEnd(); Layout(); Render(scroll, theme, width, dpi, "rules-bottom");
                foreach (var item in vm.Checklist) item.IsChecked = true; Layout();
                Assert.Empty(Descendants(section).OfType<ProgressBar>());
                Render(scroll, theme, width, dpi, "confirmed");
                section.DataContext = ready; scroll.ScrollToTop(); Layout();
                Assert.All(Descendants(section).OfType<TextBox>(), t => Assert.True(t.IsReadOnly));
                Assert.All(Descendants(section).OfType<CheckBox>(), b => Assert.False(b.IsEnabled));
                Assert.False(ready.LinkRuleCommand.CanExecute(null)); Assert.False(ready.LinkedRules[0].RemoveCommand.CanExecute(null));
                Assert.False(Assert.Single(Descendants(list).OfType<Button>()).IsEnabled);
                Assert.Contains("USD", ready.CurrencyLabel); Render(scroll, theme, width, dpi, "ready");
            }
            using var focus = new RiskReadinessTests.Fixture(); var model = focus.Vm;
            var allSections = new StackPanel(); allSections.Children.Add(new SessionPreparationSection { DataContext = model });
            allSections.Children.Add(new ScenariosSection { DataContext = model }); allSections.Children.Add(new RiskReadinessSection { DataContext = model });
            var page = new ScrollViewer { Content = allSections, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var window = new Window { Content = page, Width = 650, Height = 750 }; window.Show();
            try
            {
                model.MaximumTrades.Text = "3.5"; model.SaveAsync().GetAwaiter().GetResult();
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.Same(model.MaximumTrades, Assert.IsType<TextBox>(Keyboard.FocusedElement).DataContext);
                Assert.True(page.VerticalOffset > 0);
                model.Scenarios[0].Condition.Text = new('x', 4001); model.SaveAsync().GetAwaiter().GetResult();
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.Same(model.Scenarios[0].Condition, Assert.IsType<TextBox>(Keyboard.FocusedElement).DataContext);
                model.Contexts[0].Levels[0].Text = "bad price"; model.SaveAsync().GetAwaiter().GetResult();
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.Same(model.Contexts[0].Levels[0], Assert.IsType<TextBox>(Keyboard.FocusedElement).DataContext);
                Assert.Equal(0, focus.Base.Store.Writes);
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
        Directory.CreateDirectory(directory); var bitmap = new RenderTargetBitmap(width * dpi / 96, 1000 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(element); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(directory, $"risk-{theme}-{width}-{dpi}-{state}.png")); encoder.Save(file);
    }
}
