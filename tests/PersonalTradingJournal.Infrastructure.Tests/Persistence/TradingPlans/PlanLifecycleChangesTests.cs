using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Domain.TradingPlans;
using PersonalTradingJournal.Infrastructure.TradingPlans;
using Fixture = PersonalTradingJournal.Infrastructure.Tests.Persistence.TradingPlans.TradingPlanPersistenceTests.Fixture;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.TradingPlans;

public sealed class PlanLifecycleChangesTests
{
    [Fact]
    public async Task LegacyDraftConfirmationsDoNotBlockOrBecomeNewReadyEvidence()
    {
        await using var f = await Fixture.Create(); var legacy = await f.CreatePlan();
        legacy.SetConfirmations(legacy.Version, TradingPlanChecklist.All, f.Now);
        await using (var db = f.Context())
        {
            var row = await db.TradingPlans.SingleAsync();
            row.Version = legacy.Version; row.DraftJson = TradingPlanJson.WriteDraft(legacy.WorkingDraft!);
            await db.SaveChangesAsync();
        }
        var result = await f.Store.WriteAsync(TradingPlanPersistenceTests.Command(TradingPlanWriteOperation.MarkReady, legacy));
        Assert.Equal(TradingPlanWriteStatus.Success, result.Status);
        Assert.Equal(TradingPlanChecklist.None, result.Plan!.LatestReadyRevision!.Content.Confirmed);
        await f.Restart();
        Assert.Equal(TradingPlanChecklist.None, (await f.Store.LoadAsync(f.Key))!.LatestReadyRevision!.Content.Confirmed);
    }

    [Fact]
    public async Task PrefillPreservesFrozenCurrencyAndCancelledCreateWritesNothing()
    {
        await using var f = await Fixture.Create(); var source = await f.Ready(await f.CreatePlan());
        await using (var db = f.Context()) await db.TradingAccounts.ExecuteUpdateAsync(s => s.SetProperty(x => x.Currency, "EUR"));
        var key = new TradingPlanKey(f.Account.Id, source.Key.NewYorkDate.AddDays(2));
        var command = new TradingPlanWriteCommand(TradingPlanWriteOperation.CreateDraft, key, new());
        Assert.Equal(TradingPlanWriteStatus.Cancelled, (await f.Store.WriteAsync(command, new(true))).Status);
        Assert.Null(await f.Store.LoadAsync(key));
        var created = await f.Store.WriteAsync(command);
        Assert.Equal(TradingPlanWriteStatus.Success, created.Status);
        Assert.Equal("USD", created.Plan!.WorkingDraft!.Content.PlanCurrency);
        Assert.Equal(source.LatestReadyRevision!.Content.RiskLimits, created.Plan.WorkingDraft.Content.RiskLimits);
        Assert.Equal(TradingPlanWriteStatus.ReferenceChanged, (await f.Store.WriteAsync(
            TradingPlanPersistenceTests.Command(TradingPlanWriteOperation.MarkReady, created.Plan))).Status);
        Assert.Empty(await f.Store.ReadyHistoryAsync(created.Plan.Id));
        Assert.Equal("USD", (await f.Store.LoadReadyAsync(source.LatestReadyRevision.Id))!.References.Account.Currency);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task TombstoneDraftOrReadyRetainsExactHistoryAndReleasesOnlyItsScope(bool global, bool ready)
    {
        await using var f = await Fixture.Create();
        var key = new TradingPlanKey(global ? null : f.Account.Id, f.Key.NewYorkDate);
        var p = (await f.Store.WriteAsync(new(TradingPlanWriteOperation.CreateDraft, key, f.Content.WithPlanCurrency("USD")))).Plan!;
        if (ready) p = (await f.Store.WriteAsync(TradingPlanPersistenceTests.Command(TradingPlanWriteOperation.MarkReady, p))).Plan!;
        var snapshot = p.LatestReadyRevision is { } r ? TradingPlanJson.WriteReady(r) : null;
        var deletion = TradingPlanPersistenceTests.Command(TradingPlanWriteOperation.Delete, p);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Equal(TradingPlanWriteStatus.Cancelled, (await f.Store.WriteAsync(deletion, cancelled.Token)).Status);
        Assert.NotNull(await f.Store.LoadAsync(key));
        Assert.Equal(TradingPlanWriteStatus.Conflict, (await f.Store.WriteAsync(new(TradingPlanWriteOperation.Delete, key,
            f.Content, new(p.Id, p.Version + 1)))).Status);
        var result = await f.Store.WriteAsync(deletion);
        Assert.Equal(TradingPlanWriteStatus.Success, result.Status); Assert.Equal(p.Id, result.DeletedPlanId);
        Assert.Equal(TradingPlanWriteStatus.Conflict, (await f.Store.WriteAsync(deletion)).Status);
        Assert.Null(await f.Store.LoadAsync(key));
        Assert.Empty(await f.Store.HistoryAsync(new(key.AccountId, key.NewYorkDate, key.NewYorkDate)));
        var replacement = await f.Store.WriteAsync(new(TradingPlanWriteOperation.CreateDraft, key, f.Content.WithPlanCurrency("USD")));
        Assert.Equal(TradingPlanWriteStatus.Success, replacement.Status); Assert.NotEqual(p.Id, replacement.Plan!.Id);
        await f.Restart();
        Assert.Equal(replacement.Plan.Id, (await f.Store.LoadAsync(key))!.Id);
        if (snapshot is not null) Assert.Equal(snapshot, TradingPlanJson.WriteReady((await f.Store.LoadReadyAsync(p.LatestReadyRevision!.Id))!));
        await using var db = f.Context();
        Assert.Equal(2, await db.TradingPlans.CountAsync());
        Assert.NotNull((await db.TradingPlans.SingleAsync(x => x.Id == p.Id)).DeletedAtUtc);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task CreateUsesLatestEarlierSavedDraftOrReadyAcrossGapWithExactValuesAndNewIdentity(bool ready)
    {
        await using var f = await Fixture.Create();
        var source = await f.CreatePlan();
        if (ready) source = (await f.Store.WriteAsync(TradingPlanPersistenceTests.Command(TradingPlanWriteOperation.MarkReady, source))).Plan!;
        var saved = source.WorkingDraft?.Content ?? source.LatestReadyRevision!.Content;
        var key = new TradingPlanKey(f.Account.Id, f.Key.NewYorkDate.AddDays(4));
        var creationTime = f.Now.AddDays(4);
        var creator = new TradingPlanStore(f.Get<IDbContextFactory<PersonalTradingJournal.Infrastructure.Persistence.JournalDbContext>>(), new CreationClock(creationTime));
        var created = await creator.WriteAsync(new(TradingPlanWriteOperation.CreateDraft, key, new()));
        Assert.Equal(TradingPlanWriteStatus.Success, created.Status);
        Assert.Equal(f.Key.NewYorkDate, created.PrefilledFromDate);
        var p = created.Plan!; var c = p.WorkingDraft!.Content;
        Assert.NotEqual(source.Id, p.Id); Assert.Equal(1, p.Version); Assert.Equal(key, p.Key);
        Assert.Null(p.LatestReadyRevision); Assert.Null(p.WorkingDraft.DerivedFromRevision);
        Assert.Equal(creationTime, p.CreatedAtUtc); Assert.Equal(creationTime, p.WorkingDraft.StartedAtUtc);
        Assert.NotEqual(source.CreatedAtUtc, p.CreatedAtUtc);
        Assert.Equal(saved.Focus, c.Focus); Assert.Equal(saved.Session, c.Session); Assert.Equal(saved.Intent, c.Intent);
        Assert.Equal(saved.Instruments.ToArray(), c.Instruments.ToArray());
        Assert.Equal(saved.NewsAndRestrictions, c.NewsAndRestrictions); Assert.Equal(saved.StandAside, c.StandAside);
        Assert.Equal(saved.RiskLimits, c.RiskLimits); Assert.Equal(saved.Rules.ToArray(), c.Rules.ToArray());
        Assert.Equal(saved.Scenarios[0].Condition, c.Scenarios[0].Condition); Assert.Equal(saved.Scenarios[0].Setup, c.Scenarios[0].Setup);
        Assert.Equal(saved.Scenarios[0].Name, c.Scenarios[0].Name); Assert.Equal(saved.Scenarios[0].Response, c.Scenarios[0].Response);
        Assert.Equal(saved.Scenarios[0].Invalidation, c.Scenarios[0].Invalidation); Assert.Equal(saved.Scenarios[0].Role, c.Scenarios[0].Role);
        Assert.Equal(saved.Scenarios[0].Direction, c.Scenarios[0].Direction);
        Assert.NotEqual(saved.Scenarios[0].Id, c.Scenarios[0].Id);
        Assert.Equal("USD", c.PlanCurrency); Assert.Empty(c.Screenshots); Assert.Equal(TradingPlanChecklist.None, c.Confirmed);
        Assert.Equal(TradingPlanWriteStatus.TargetAlreadyExists, (await f.Store.WriteAsync(new(TradingPlanWriteOperation.CreateDraft, key, new()))).Status);
        Assert.Equal(source.Version, (await f.Store.LoadAsync(source.Key))!.Version);
        await f.Restart(); Assert.Equal(c.Focus, (await f.Store.LoadAsync(key))!.WorkingDraft!.Content.Focus);
    }

    [Fact]
    public async Task PrefillExcludesFutureDeletedAndOtherScopeAndDoesNotWriteOnRead()
    {
        await using var f = await Fixture.Create();
        var source = await f.CreatePlan();
        var global = new TradingPlanKey(null, f.Key.NewYorkDate.AddDays(1));
        var blank = await f.Store.WriteAsync(new(TradingPlanWriteOperation.CreateDraft, global, new(planCurrency: "EUR")));
        Assert.Null(blank.PrefilledFromDate); Assert.Empty(blank.Plan!.WorkingDraft!.Content.Instruments);
        await f.Store.WriteAsync(TradingPlanPersistenceTests.Command(TradingPlanWriteOperation.Delete, source));
        var target = new TradingPlanKey(f.Account.Id, source.Key.NewYorkDate.AddDays(-1));
        Assert.Null(await f.Store.LoadAsync(target));
        await using (var db = f.Context()) Assert.Equal(2, await db.TradingPlans.CountAsync());
        var empty = await f.Store.WriteAsync(new(TradingPlanWriteOperation.CreateDraft, target, new()));
        Assert.Null(empty.PrefilledFromDate); Assert.Equal("", empty.Plan!.WorkingDraft!.Content.Focus);
        var later = await f.Store.WriteAsync(new(TradingPlanWriteOperation.CreateDraft, new(f.Account.Id, source.Key.NewYorkDate.AddDays(2)), new()));
        Assert.Equal(target.NewYorkDate, later.PrefilledFromDate); Assert.Empty(later.Plan!.WorkingDraft!.Content.Instruments);
    }

    [Fact]
    public async Task ChangedReferencesKeepCopiedTextButCannotPublishReady()
    {
        await using var f = await Fixture.Create(); var source = await f.CreatePlan();
        await using (var db = f.Context()) await db.Instruments.ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));
        var result = await f.Store.WriteAsync(new(TradingPlanWriteOperation.CreateDraft,
            new(f.Account.Id, source.Key.NewYorkDate.AddDays(1)), new()));
        Assert.Equal(TradingPlanWriteStatus.Success, result.Status); Assert.NotEmpty(result.References);
        Assert.Equal(source.WorkingDraft!.Content.Focus, result.Plan!.WorkingDraft!.Content.Focus);
        Assert.Null(result.VerifiedReferences);
        var mark = await f.Store.WriteAsync(TradingPlanPersistenceTests.Command(TradingPlanWriteOperation.MarkReady, result.Plan));
        Assert.Equal(TradingPlanWriteStatus.ReferenceUnavailable, mark.Status);
        Assert.Empty(await f.Store.ReadyHistoryAsync(result.Plan.Id));
    }

    [Fact]
    public async Task ConcurrentCreateAndDeleteHaveSingleWinnersAndReadyRequiresNoConfirmations()
    {
        await using var f = await Fixture.Create(); var source = await f.CreatePlan();
        var ready = await f.Store.WriteAsync(TradingPlanPersistenceTests.Command(TradingPlanWriteOperation.MarkReady, source));
        Assert.Equal(TradingPlanWriteStatus.Success, ready.Status);
        Assert.Equal(TradingPlanChecklist.None, ready.Plan!.LatestReadyRevision!.Content.Confirmed);
        var delete = TradingPlanPersistenceTests.Command(TradingPlanWriteOperation.Delete, ready.Plan);
        var outcomes = await Task.WhenAll(Task.Run(() => f.Store.WriteAsync(delete)), Task.Run(() => f.Store.WriteAsync(delete)));
        Assert.Single(outcomes, o => o.Status == TradingPlanWriteStatus.Success);
        Assert.Single(outcomes, o => o.Status == TradingPlanWriteStatus.Conflict);
        var create = new TradingPlanWriteCommand(TradingPlanWriteOperation.CreateDraft, f.Key, f.Content);
        outcomes = await Task.WhenAll(Task.Run(() => f.Store.WriteAsync(create)), Task.Run(() => f.Store.WriteAsync(create)));
        Assert.Single(outcomes, o => o.Status == TradingPlanWriteStatus.Success);
        Assert.Single(outcomes, o => o.Status == TradingPlanWriteStatus.TargetAlreadyExists);
    }
    private sealed class CreationClock(DateTimeOffset now) : TimeProvider
    { public override DateTimeOffset GetUtcNow() => now; }
}
