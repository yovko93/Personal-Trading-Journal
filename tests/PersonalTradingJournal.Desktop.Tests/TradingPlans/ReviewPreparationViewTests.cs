using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.PeriodReviews;
using PersonalTradingJournal.Application.PeriodReviews.Coaching;
using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.DailyReview;
using PersonalTradingJournal.Desktop.Tests.PeriodReviews;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.DailyReview;
using PersonalTradingJournal.Desktop.Views.DailyReview;
using PersonalTradingJournal.Desktop.Views.PeriodReviews;
using PersonalTradingJournal.Desktop.Views.TradingPlans;

namespace PersonalTradingJournal.Desktop.Tests.TradingPlans;
public sealed class ReviewPreparationViewTests
{
    private const string Host = "PTJ_REVIEW_PREPARATION_HOST";
    private static readonly Lazy<Task> Suite = new(() => IsolatedTestProcess.RunSuiteAsync(typeof(ReviewPreparationViewTests),
        "review-preparation", Host, TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30)));
    [Theory] [InlineData(980, 96)] [InlineData(460, 240)]
    public async Task BoundDailyAndPeriodPreparationMissingRetrospectiveAndSavedCitationsWrapAndNavigate(int width, int dpi)
    {
        if (Environment.GetEnvironmentVariable(Host) != "1") { await Suite.Value; return; }
        await using var f = await TradingPlanWorkspaceTests.Fixture.CreateAsync(true);
        var ready = ReviewPlanRevision.From(await TradingPlanHistoryTests.Ready(f, "Wait for recorded confirmation; this is an intention, not an execution fact."));
        var date = ReviewPlanEvidence.EntryDate(ready.ReadyAtUtc.AddMinutes(-1));
        ready = ready with { Key = new(ready.Key.AccountId, date) };
        var trade = ReviewFixture.Evidence(new(date)).Trades[0];
        trade = trade with { Account = new(ready.Key.AccountId, ready.Account.Name, true), Instrument = new(ready.Instruments[0].InstrumentId, "Synthetic NQ", true),
            Facts = trade.Facts! with { OpenedAtUtc = ready.ReadyAtUtc.AddMinutes(-1), ClosedAtUtc = ready.ReadyAtUtc }, Inclusion = DailyReviewTradeInclusion.ClosedOnDate };
        var prep = ReviewPlanEvidence.Build(date, date, ready.Key.AccountId,
            [new(ready.Key, ready.Id.PlanId, ReviewPlanState.ReadyWithDraft, ready.Id.Revision, ready.ReadyAtUtc)], [ready], [trade]);
        var evidence = new DailyReviewEvidence(new(date, ready.Key.AccountId), [trade], [], prep);
        await CalendarStaTest.RunAsync(() =>
        {
            var app = System.Windows.Application.Current ?? new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (string theme in new[] { "Light", "Dark" })
            {
                app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
                var daily = new ReviewFixture(); daily.Reader.Handler = (_, _) => Task.FromResult(evidence);
                daily.Vm.SelectedDate = date.ToDateTime(TimeOnly.MinValue); daily.Vm.SelectedAccount = new(ready.Key.AccountId, ready.Account.Name);
                daily.History.Items.Add(ReviewFixture.Saved(evidence, true)); Pump(daily.Vm.ActivateAsync());
                var view = new DailyReviewView { DataContext = daily.Vm }; Layout(view, width, dpi);
                var expander = Descendants(view).OfType<Expander>().Single(e => e.Header?.ToString()?.StartsWith("Trading Plan preparation") == true);
                Assert.False(expander.IsExpanded); expander.IsExpanded = true; Layout(view, width, dpi);
                var preparation = Descendants(expander).OfType<ReviewPreparationView>().Single();
                Assert.Contains("retrospective", preparation.Preparation!.Comparison); Assert.Contains("Draft text excluded", preparation.Preparation.Coverage);
                var open = Descendants(preparation).OfType<Button>().Single(); Assert.True(open.IsTabStop); Assert.NotEmpty(new ButtonAutomationPeer(open).GetName());
                Assert.Same(daily.Vm.OpenPreparationCommand, open.Command);
                Guid? opened = null; daily.Vm.OpenPreparationAsync = p => { opened = p.Id.PlanId; return Task.FromResult(true); };
                Pump(daily.Vm.OpenPreparationCommand.ExecuteAsync(open.CommandParameter)); Assert.Equal(ready.Id.PlanId, opened);
                var scroll = (ScrollViewer)view.FindName("ReviewScroll"); scroll.ScrollToVerticalOffset(expander.TranslatePoint(new Point(), scroll).Y - 24); Layout(view, width, dpi);
                Assert.Equal(0, scroll.ScrollableWidth); Render(view, theme, width, dpi, "daily-retrospective");
                Pump(daily.Vm.OpenAnalysisCommand.ExecuteAsync(daily.Vm.Analyses.Single())); Layout(view, width, dpi);
                Assert.Contains(daily.Vm.Snapshot!.Statements.SelectMany(s => s.Citations), c => c.Preparation?.Id == ready.Id);
                daily.Reader.Handler = (_, _) => Task.FromResult(evidence with { Trades = [], Preparation = ReviewPlanEvidence.Build(date, date, ready.Key.AccountId, [new(ready.Key, null, ReviewPlanState.NoPlan, null, null)], [], []) });
                Pump(daily.Vm.RefreshCommand.ExecuteAsync(null)); Layout(view, width, dpi);
                expander = Descendants(view).OfType<Expander>().Single(e => e.Header?.ToString()?.StartsWith("Trading Plan preparation") == true);
                expander.IsExpanded = true; Layout(view, width, dpi); Render(view, theme, width, dpi, "daily-missing"); daily.Vm.Deactivate();

                var period = new PeriodFixture(); period.Vm.SelectedDate = date.ToDateTime(TimeOnly.MinValue); period.Vm.SelectedAccount = new(ready.Key.AccountId, ready.Account.Name);
                var q = period.Vm.Query!; var pair = PeriodFixture.Pair(q, true);
                pair = pair with { Current = pair.Current with { ClosedTrades = [trade], Preparation = ReviewPlanEvidence.Build(q.FirstDate, q.LastDate, ready.Key.AccountId, prep.Days, [ready], [trade]) } };
                period.Reader.Handler = (_, _) => Task.FromResult(new PeriodComparisonEvidenceResult(PeriodEvidenceStatus.Ready, pair, null));
                var packet = PeriodCoachingPacketBuilder.Build(pair).Packet!;
                period.Store.Items.Add(PeriodReviewSnapshot.Create(new(PeriodCoachingStatus.Success,
                    PeriodCoachingResponseValidator.Validate(PeriodFixture.ProviderFake.Json(packet), packet).Response, packet, new("external-paste", "unverified", "synthetic"), ""), ready.ReadyAtUtc).Review);
                Pump(period.Vm.ActivateAsync()); Pump(period.Vm.OpenCommand.ExecuteAsync(period.Vm.History.Single()));
                Assert.Equal(ready.Id, Assert.Single(period.Vm.Report!.Preparation.Revisions).Source.Id);
                Assert.Contains("not captured", period.Vm.Report.PreviousPreparation.Coverage);
                var citation = period.Vm.Report!.Cards.SelectMany(c => c.Findings).SelectMany(x => x.Citations).First(c => c.HasPreparation);
                period.Vm.OpenCitationCommand.Execute(citation);
                var page = new PeriodReviewView { DataContext = period.Vm }; Layout(page, width, dpi);
                var detail = (ContentControl)page.FindName("CitationDetail");
                var button = Descendants(detail).OfType<Button>().Single(b => b.Content?.ToString() == "Open exact Ready revision");
                Assert.Same(period.Vm.OpenPreparationCommand, button.Command); Assert.Equal(ready.Id, ((ReviewPlanRevision)button.CommandParameter).Id);
                var ps = Descendants(page).OfType<ScrollViewer>().First(); ps.ScrollToVerticalOffset(detail.TranslatePoint(new Point(), ps).Y); Layout(page, width, dpi);
                Assert.Equal(0, ps.ScrollableWidth); Assert.True(button.ActualWidth <= width); Render(page, theme, width, dpi, "period-saved-citation");
                Assert.Equal(0, period.Provider.Calls); Assert.Equal(0, period.Store.Saves); period.Vm.Deactivate();
            }
        }, shutdownDispatcher: false);
    }
    private static void Layout(FrameworkElement page, int width, int dpi)
    { VisualTreeHelper.SetRootDpi(page, new(dpi / 96d, dpi / 96d)); page.Measure(new Size(width, 950)); page.Arrange(new Rect(0, 0, width, 950)); page.UpdateLayout(); Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); }
    private static void Pump(Task t)
    { var f = new DispatcherFrame(); var d = Dispatcher.CurrentDispatcher; _ = t.ContinueWith(_ => d.BeginInvoke(() => f.Continue = false), TaskScheduler.Default); if (!t.IsCompleted) Dispatcher.PushFrame(f); t.GetAwaiter().GetResult(); }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject r)
    { for (int i = 0; i < VisualTreeHelper.GetChildrenCount(r); i++) { var c = VisualTreeHelper.GetChild(r, i); yield return c; foreach (var x in Descendants(c)) yield return x; } }
    private static void Render(FrameworkElement view, string theme, int width, int dpi, string state)
    {
        if (Environment.GetEnvironmentVariable("PTJ_PREPARATION_RENDER_DIRECTORY") is not { Length: > 0 } path) return;
        Directory.CreateDirectory(path); var bitmap = new RenderTargetBitmap(width * dpi / 96, 950 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32); bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var output = File.Create(Path.Combine(path, $"{state}-{theme}-{width}-{dpi}.png")); encoder.Save(output);
    }
}
