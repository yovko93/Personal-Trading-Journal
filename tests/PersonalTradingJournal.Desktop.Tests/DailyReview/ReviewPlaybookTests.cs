using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.DailyReview;
using PersonalTradingJournal.Desktop.Views.DailyReview;

namespace PersonalTradingJournal.Desktop.Tests.DailyReview;

public sealed partial class DailyReviewViewTests
{
    internal static DailyReviewEvidence PlaybookEvidence()
    {
        var evidence = ReviewFixture.Evidence(new(ReviewFixture.Day, ReviewFixture.AccountId));
        var trade = evidence.Trades[0]; var setup = Guid.NewGuid();
        var g = new ReviewPlaybookGuidance(setup, "NQ saved reclaim", null, 3, trade.CreatedAtUtc,
            "Prior range", "Wait for reclaim", "Retest holds", "Rejection of range", "Limit risk; no adherence claim",
            [new(Guid.NewGuid(), "Only this caption was supplied, not the chart image.", "Invalid", null, null, true)]);
        return evidence with { Journals = [], Trades = [trade with { Setup = new(setup, "NQ saved reclaim", true),
            Playbook = new(ReviewPlaybookState.Captured, [new(7, setup, trade.UpdatedAtUtc, trade.CreatedAtUtc,
                ReviewPlaybookAssociationState.Current, g)]) }] };
    }

    [Fact]
    public void OldV1SnapshotsWithoutPlaybooksRemainReadableWithoutBeingRebuilt()
    {
        var saved = ReviewFixture.Saved(ReviewFixture.Evidence(new(ReviewFixture.Day)), true);
        var evidence = JsonNode.Parse(saved.EvidenceJson)!;
        evidence["contractVersion"] = CoachingContract.LegacyVersion;
        foreach (var trade in evidence["content"]!["recordedTradeFacts"]!.AsArray()) trade!.AsObject().Remove("playbook");
        var response = JsonNode.Parse(saved.ResponseJson)!; response["contractVersion"] = CoachingContract.LegacyVersion;
        saved = saved with { EvidenceContractVersion = CoachingContract.LegacyVersion, ResponseContractVersion = CoachingContract.LegacyVersion,
            EvidenceJson = evidence.ToJsonString(), ResponseJson = response.ToJsonString() };
        var before = saved.EvidenceJson;
        var presentation = ReviewSnapshot.Read(saved);
        Assert.NotEmpty(presentation.Statements); Assert.NotEmpty(presentation.Evidence.Trades);
        Assert.All(presentation.Evidence.Trades, t => Assert.Null(t.Source.Playbook));
        Assert.Equal(before, saved.EvidenceJson);
    }

    [Fact]
    public async Task CapturedCitationRemainsSavedAndCurrentTradeNavigationRetainsGuardAndIdentity()
    {
        var f = new ReviewFixture(); var evidence = PlaybookEvidence(); var saved = ReviewFixture.Saved(evidence, true);
        f.History.Items.Add(saved); f.Vm.SelectedAccount = new(ReviewFixture.AccountId, "P 21"); await f.Vm.ActivateAsync();
        await f.Vm.OpenAnalysisCommand.ExecuteAsync(f.Vm.Analyses.Single());
        var citation = Assert.Single(f.Vm.Snapshot!.Statements.SelectMany(s => s.Citations), c => c.HasPlaybook);
        Assert.Contains("revision 3", citation.Label); Assert.Contains("not the current Setup", citation.CapturedPlaybook);
        Assert.Contains("not verify adherence", citation.CapturedPlaybook); Assert.Contains("Retrospective", citation.CapturedPlaybook);
        Guid? target = null; bool leave = false;
        f.Vm.OpenTradeAsync = id => { target = id; return Task.CompletedTask; }; // Existing host owns its navigation guard.
        await f.Vm.OpenTradeCommand.ExecuteAsync(citation.Trade);
        Assert.Equal(evidence.Trades[0].TradeId, target);
        f.Vm.OpenTradeAsync = _ => { leave = false; return Task.CompletedTask; }; // Veto is a no-op, not source substitution.
        await f.Vm.OpenTradeCommand.ExecuteAsync(citation.Trade); Assert.False(leave);
        Assert.Same(citation, f.Vm.Snapshot.Statements.SelectMany(s => s.Citations).Single(c => c.HasPlaybook));
        Assert.Equal(saved.EvidenceJson, f.History.Items[0].EvidenceJson); Assert.Equal(0, f.History.Writes);
    }

    [Fact]
    public async Task SavedPlaybookCitationExpandsReadOnlyInBothThemesAndNarrowHighDpi()
    {
        if (Environment.GetEnvironmentVariable("PTJ_DAILY_REVIEW_TEST_HOST") != "1") { await Host.Value; return; }
        var f = new ReviewFixture(); f.History.Items.Add(ReviewFixture.Saved(PlaybookEvidence(), true));
        f.Vm.SelectedAccount = new(ReviewFixture.AccountId, "P 21");
        await f.Vm.ActivateAsync(); await f.Vm.OpenAnalysisCommand.ExecuteAsync(f.Vm.Analyses.Single());
        await CalendarStaTest.RunAsync(() =>
        {
            var app = System.Windows.Application.Current ?? new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (var (theme, width, dpi) in new[] { ("Light", 960, 96), ("Dark", 960, 96), ("Light", 480, 240), ("Dark", 480, 240) })
            {
                app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
                var view = new DailyReviewView { DataContext = f.Vm };
                var root = new Border { Child = view };
                root.SetResourceReference(Border.BackgroundProperty, "PtjBackgroundBrush");
                root.Measure(new Size(width,760)); root.Arrange(new Rect(0,0,width,760)); root.UpdateLayout(); Flush();
                var expand = Assert.Single(Descendants(view).OfType<Expander>(), e => e.Visibility == Visibility.Visible && e.DataContext is ReviewCitation { HasPlaybook: true });
                Assert.False(expand.IsExpanded); Assert.True(expand.Focusable);
                Assert.Contains("Captured Playbook citation", AutomationProperties.GetName(expand));
                expand.IsExpanded = true; root.UpdateLayout(); Flush();
                var scroll = (ScrollViewer)view.FindName("ReviewScroll");
                scroll.ScrollToVerticalOffset(scroll.VerticalOffset + expand.TranslatePoint(new Point(), scroll).Y - 60);
                root.UpdateLayout(); Flush();
                Assert.Equal(0, scroll.ScrollableWidth);
                Assert.Contains(Descendants(expand).OfType<TextBlock>(), t => t.Text.Contains("Market context") && t.Text.Contains("No chart images"));
                Assert.DoesNotContain(Descendants(expand).OfType<TextBox>(), _ => true);
                Render(root, theme, width, dpi, "playbook-citation");
                scroll.ScrollToEnd(); root.UpdateLayout(); Flush();
                Assert.InRange(scroll.VerticalOffset, scroll.ScrollableHeight - 1, scroll.ScrollableHeight + 1);
                expand.IsExpanded = false; Assert.NotNull(f.Vm.Snapshot); root.Child = null;
            }
            f.Vm.Deactivate();
        }, shutdownDispatcher: false);
    }
}
