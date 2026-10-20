using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.Tests.Navigation;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.DailyReview;
using PersonalTradingJournal.Desktop.ViewModels.TradingPlans;

namespace PersonalTradingJournal.Desktop.Tests.TradingPlans;
public sealed class ReviewPreparationNavigationTests
{
    [Fact]
    public async Task ExactReviewLinkAndReturnKeepSavedSnapshotScopeAndInvalidEditorWithoutWrites()
    {
        await using var f = await TradingPlanWorkspaceTests.Fixture.CreateAsync(true);
        var first = await TradingPlanHistoryTests.Ready(f, "First frozen focus");
        var reference = ReviewPlanRevision.From(first);
        var reader = f.Db.Provider.GetRequiredService<IDailyReviewEvidenceReader>();
        var evidence = await reader.GetAsync(new(first.Key.NewYorkDate, first.Key.AccountId));
        var packet = CoachingEvidencePacketBuilder.Build(evidence).Packet!;
        var source = ReviewPlanEvidence.SourceId(first.Id);
        var history = f.Db.Provider.GetRequiredService<ICoachingAnalysisRepository>();
        var saved = await history.SaveAsync(CoachingAnalysisSnapshot.Create(packet,
            new(CoachingGenerationStatus.Success, new(packet.ContractVersion, packet.PacketId, new("Preparation is an intention", [source]), [], [], [], []),
                new("external-paste", "unverified", "synthetic"), "Validated synthetic response"), DateTimeOffset.UtcNow));
        await TradingPlanHistoryTests.Ready(f, "Later preparation is not the saved source"); await f.Vm.StartDraftAsync();
        var editor = f.Vm.Editor!; editor.MaximumTrades.Text = "invalid retained count"; f.Vm.DateText = "invalid retained date";
        var daily = new DailyReviewViewModel(reader, history, f.Db.Provider.GetRequiredService<ITradingAccountReader>(), new FakeDialogService(), TimeProvider.System);
        using var shell = MainWindowViewModelTests.CreatePlanShell(f.Vm, dailyReview: daily);
        shell.NavigateCommand.Execute(NavigationDestination.TradingPlan);
        await f.Vm.OpenDailyReviewCommand.ExecuteAsync(null); await daily.LoadTask;
        Assert.Equal(NavigationDestination.DailyReview, shell.CurrentDestination);
        Assert.Equal(first.Key.AccountId, daily.SelectedAccount.Id); Assert.Equal(first.Key.NewYorkDate, DateOnly.FromDateTime(daily.SelectedDate!.Value));
        await daily.OpenAnalysisCommand.ExecuteAsync(daily.Analyses.Single(x => x.Source.Id == saved.Summary.Id));
        var snapshot = daily.Snapshot!; Assert.Single(snapshot.Evidence.Preparation.Revisions);
        var citation = Assert.Single(Assert.Single(snapshot.Statements).Citations);
        Assert.Equal(first.Id, citation.Preparation!.Id); Assert.Equal("First frozen focus", citation.Preparation.Content.Focus);
        int writes = f.Store.Writes;
        await daily.OpenPreparationCommand.ExecuteAsync(citation.Preparation);
        Assert.Equal(NavigationDestination.TradingPlan, shell.CurrentDestination);
        Assert.True(f.Vm.IsHistorySelected); Assert.Contains("Ready revision 1", f.Vm.HistoricalViewer!.StateLabel); Assert.Equal(first.Id, f.Vm.RevisionRows.Single().Revision.Id);
        Assert.Same(editor, f.Vm.Editor); Assert.Equal("invalid retained count", editor.MaximumTrades.Text); Assert.Equal("invalid retained date", f.Vm.DateText);
        f.Vm.ReturnToReviewCommand.Execute(null);
        Assert.Equal(NavigationDestination.DailyReview, shell.CurrentDestination); Assert.Same(snapshot, daily.Snapshot);
        f.Store.ReadyRead = (_, _) => Task.FromResult<Domain.TradingPlans.TradingPlanReadyRevision?>(null);
        await daily.OpenPreparationCommand.ExecuteAsync(reference);
        Assert.Equal(NavigationDestination.DailyReview, shell.CurrentDestination); Assert.Same(snapshot, daily.Snapshot);
        Assert.Contains("could not be opened", daily.CurrentError); Assert.Same(editor, f.Vm.Editor); Assert.Equal(writes, f.Store.Writes);
        shell.NavigateCommand.Execute(NavigationDestination.Dashboard); await shell.PlanNavigationTask;
        Assert.Equal(NavigationDestination.DailyReview, shell.CurrentDestination); Assert.True(f.Interaction.Prompts > 0);
        daily.Deactivate();
    }

    [Fact]
    public void OldEvidenceRemainsNotCapturedRatherThanAnInventedNoPlan()
    {
        var e = new DailyReviewEvidence(new(new(2026, 11, 1)), [], []);
        var presentation = new ReviewEvidencePresentation(e, DailyReviewStatisticsCalculator.Calculate(e));
        Assert.Contains("not captured", presentation.Preparation.Coverage);
        Assert.Empty(presentation.Preparation.Revisions);
    }
}
