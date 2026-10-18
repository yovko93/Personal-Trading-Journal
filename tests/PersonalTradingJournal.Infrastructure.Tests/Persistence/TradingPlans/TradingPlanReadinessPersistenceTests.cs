using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Infrastructure.TradingPlans;
using PersonalTradingJournal.Domain.TradingPlans;
using Fixture = PersonalTradingJournal.Infrastructure.Tests.Persistence.TradingPlans.TradingPlanPersistenceTests.Fixture;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.TradingPlans;

public sealed class TradingPlanReadinessPersistenceTests
{
    [Fact]
    public async Task SaveAndExplicitConfirmationsAreAtomicPersistedAndReadBackAfterRestart()
    {
        await using var f = await Fixture.Create(); var plan = await f.CreatePlan();
        var content = TradingPlanPersistenceTests.Change(plan.WorkingDraft!.Content, focus: "New preparation");
        var save = await f.Store.WriteAsync(new(TradingPlanWriteOperation.SaveDraft, f.Key, content, new(plan.Id, plan.Version), explicitConfirmations: TradingPlanChecklist.All));
        Assert.Equal(TradingPlanWriteStatus.Success, save.Status); Assert.Equal(TradingPlanChecklist.All, save.Plan!.WorkingDraft!.Content.Confirmed);
        Assert.Equal("USD", save.VerifiedReferences!.Account.Currency);
        Assert.Equal(plan.Version + 2, save.Plan.Version); // Existing EditDraft and SetConfirmations transitions, one DB commit.
        var same = await f.Store.WriteAsync(new(TradingPlanWriteOperation.SaveDraft, f.Key, save.Plan.WorkingDraft.Content, new(plan.Id, save.Plan.Version), explicitConfirmations: TradingPlanChecklist.All));
        Assert.Equal(save.Plan.Version, same.Plan!.Version);
        await f.Restart(); var loaded = (await f.Store.LoadAsync(f.Key))!;
        Assert.Equal("New preparation", loaded.WorkingDraft!.Content.Focus); Assert.Equal(TradingPlanChecklist.All, loaded.WorkingDraft.Content.Confirmed);
        Assert.Empty(await f.Store.ReadyHistoryAsync(plan.Id));
        var stale = await f.Store.WriteAsync(new(TradingPlanWriteOperation.SaveDraft, f.Key, content, new(plan.Id, plan.Version), explicitConfirmations: TradingPlanChecklist.None));
        Assert.Equal(TradingPlanWriteStatus.Conflict, stale.Status);
        Assert.Equal(TradingPlanChecklist.All, (await f.Store.LoadAsync(f.Key))!.WorkingDraft!.Content.Confirmed);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task FailedOrCancelledCombinedSaveRollsBackBothContentAndChecks(bool cancel)
    {
        await using var f = await Fixture.Create(); var plan = await f.CreatePlan();
        using var ct = new CancellationTokenSource();
        var store = new TradingPlanStore(f.Factory(new BeforeCommit(ct, cancel)), f.Clock);
        var result = await store.WriteAsync(new(TradingPlanWriteOperation.SaveDraft, f.Key,
            TradingPlanPersistenceTests.Change(plan.WorkingDraft!.Content, "retained client input"), new(plan.Id, plan.Version), explicitConfirmations: TradingPlanChecklist.All), ct.Token);
        Assert.Equal(cancel ? TradingPlanWriteStatus.Cancelled : TradingPlanWriteStatus.StorageFailure, result.Status);
        var read = (await f.Store.LoadAsync(f.Key))!;
        Assert.Equal(plan.Version, read.Version); Assert.Equal(plan.WorkingDraft.Content.Focus, read.WorkingDraft!.Content.Focus);
        Assert.Equal(TradingPlanChecklist.None, read.WorkingDraft.Content.Confirmed);
    }
    [Fact]
    public async Task PreflightIsReadOnlyDetectsSavedAccountBaselineAndStaleToken()
    {
        await using var f = await Fixture.Create(); var plan = await f.CreatePlan();
        var reader = f.Get<ITradingPlanReferenceReader>(); var expected = new TradingPlanExpectedState(plan.Id, plan.Version);
        var valid = await reader.CheckAsync(f.Key, expected, plan.WorkingDraft!.Content);
        Assert.Equal(TradingPlanWriteStatus.Success, valid.Status); Assert.True(valid.AccountMatchesBaseline);
        Assert.Equal("USD", valid.CurrentAccount!.Currency); Assert.NotNull(valid.VerifiedReferences);
        await using (var db = f.Context()) await db.TradingAccounts.ExecuteUpdateAsync(s => s.SetProperty(x => x.Currency, "EUR"));
        var changed = await reader.CheckAsync(f.Key, expected, plan.WorkingDraft.Content);
        Assert.Equal(TradingPlanWriteStatus.ReferenceChanged, changed.Status); Assert.False(changed.AccountMatchesBaseline);
        Assert.Equal("EUR", changed.CurrentAccount!.Currency); Assert.Null(changed.VerifiedReferences);
        Assert.Equal(plan.Version, (await f.Store.LoadAsync(f.Key))!.Version);
        Assert.Equal(TradingPlanWriteStatus.Conflict, (await reader.CheckAsync(f.Key, new(plan.Id, plan.Version + 1), plan.WorkingDraft.Content)).Status);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.CheckAsync(f.Key, expected, plan.WorkingDraft.Content, new(true)));
    }
    [Fact]
    public async Task CurrencyAcknowledgementIsRecheckedAndFrozenReadyCurrencyNeverChanges()
    {
        await using var f = await Fixture.Create(); var ready = await f.Ready(await f.CreatePlan());
        var id = ready.LatestReadyRevision!.Id; var source = ready.LatestReadyRevision;
        await using (var db = f.Context()) await db.TradingAccounts.ExecuteUpdateAsync(s => s.SetProperty(x => x.Currency, "EUR"));
        var start = new TradingPlanWriteCommand(TradingPlanWriteOperation.StartDraft, f.Key, source.Content, new(ready.Id, ready.Version), id);
        Assert.Equal(TradingPlanWriteStatus.ReferenceChanged, (await f.Store.WriteAsync(start)).Status);
        var accepted = await f.Store.WriteAsync(new(TradingPlanWriteOperation.StartDraft, f.Key, source.Content, start.Expected, id, expectedAccountCurrency: "EUR"));
        Assert.Equal(TradingPlanWriteStatus.Success, accepted.Status); var draft = accepted.Plan!;
        await using (var db = f.Context()) await db.TradingAccounts.ExecuteUpdateAsync(s => s.SetProperty(x => x.Currency, "GBP"));
        var mismatch = await f.Store.WriteAsync(new(TradingPlanWriteOperation.RefreshReferences, f.Key, draft.WorkingDraft!.Content,
            new(draft.Id, draft.Version), expectedAccountCurrency: "EUR"));
        Assert.Equal(TradingPlanWriteStatus.ReferenceChanged, mismatch.Status);
        Assert.Equal(draft.Version, (await f.Store.LoadAsync(f.Key))!.Version);
        Assert.Equal("USD", (await f.Store.LoadReadyAsync(id))!.References.Account.Currency);
        Assert.Equal(source.Content.RiskLimits, (await f.Store.LoadReadyAsync(id))!.Content.RiskLimits);
        var copyKey = new TradingPlanKey(f.Account.Id, f.Key.NewYorkDate.AddDays(1));
        Assert.Equal(TradingPlanWriteStatus.ReferenceChanged, (await f.Store.WriteAsync(new(TradingPlanWriteOperation.CopyForward, copyKey, new(), sourceRevision: id))).Status);
        Assert.Null(await f.Store.LoadAsync(copyKey));
    }
    private sealed class BeforeCommit(CancellationTokenSource source, bool cancel) : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (cancel) source.Cancel(); else throw new IOException("Synthetic staged write failure");
            return ValueTask.FromResult(result);
        }
    }
}
