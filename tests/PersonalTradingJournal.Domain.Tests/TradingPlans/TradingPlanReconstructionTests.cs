using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Domain.Tests.TradingPlans;

public sealed class TradingPlanReconstructionTests
{
    [Theory] [InlineData("USD")] [InlineData("USDT")] [InlineData("SYNTH123")]
    public void SnapshotReconstructionPreservesExistingCatalogCurrencyContract(string currency)
    {
        var at = TradingPlanFixture.Time;
        Assert.Equal(currency, new TradingPlanAccountSnapshot(Guid.NewGuid(), "Synthetic", currency, at).Currency);
        Assert.Equal(currency, new TradingPlanInstrumentSnapshot(Guid.NewGuid(), "SYN", "Synthetic", currency, 1m, at).Currency);
    }
    [Fact]
    public void ReconstructionDoesNotExecuteTransitionsOrResetChecks()
    {
        var f = new TradingPlanFixture(); var p = f.Plan;
        p.SetConfirmations(1, TradingPlanChecklist.All, p.UpdatedAtUtc);
        var restored = TradingPlan.Rehydrate(p.Id, p.Key, p.Version, p.WorkingDraft, null, p.CreatedAtUtc, p.UpdatedAtUtc);
        Assert.Equal(p.Id, restored.Id); Assert.Equal(p.Version, restored.Version);
        Assert.Equal(TradingPlanChecklist.All, restored.WorkingDraft!.Content.Confirmed);
        var ready = f.Ready();
        var draft = TradingPlanDraft.Rehydrate(ready.Content, ready.DerivedFromRevision, ready.DraftStartedAtUtc, ready.DraftUpdatedAtUtc);
        var historical = TradingPlanReadyRevision.Rehydrate(ready.Id, ready.Key, draft, ready.References,
            ready.PlanCreatedAtUtc, ready.ReadyAtUtc, ready.SourceDraftVersion);
        Assert.Equal(ready.Content, historical.Content); Assert.Equal(ready.ReadyAtUtc, historical.ReadyAtUtc);
        Assert.Equal(ready.Id, historical.Id);
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void MalformedLifecycleOwnerVersionAndChronologyAreRejected(int defect)
    {
        var f = new TradingPlanFixture(); var ready = f.Ready(); var p = f.Plan;
        Assert.ThrowsAny<ArgumentException>(() => TradingPlan.Rehydrate(defect == 0 ? Guid.NewGuid() : p.Id,
            defect == 1 ? new(Guid.NewGuid(), p.Key.NewYorkDate) : p.Key, defect == 2 ? 0 : p.Version,
            null, defect == 3 ? null : ready, p.CreatedAtUtc, defect == 4 ? p.UpdatedAtUtc.AddMinutes(1) : p.UpdatedAtUtc));
    }

    [Fact]
    public void DraftAndSnapshotReconstructionRejectNonUtcDuplicateAndMissingValues()
    {
        var f = new TradingPlanFixture(); var ready = f.Ready(); var r = ready.References;
        Assert.Throws<ArgumentException>(() => TradingPlanDraft.Rehydrate(f.Content, null,
            f.Plan.CreatedAtUtc, f.Plan.CreatedAtUtc.AddSeconds(-1)));
        Assert.Throws<ArgumentException>(() => new TradingPlanAccountSnapshot(Guid.Empty, "A", "USD", f.Plan.CreatedAtUtc));
        Assert.Throws<ArgumentException>(() => new TradingPlanInstrumentSnapshot(Guid.NewGuid(), "NQ", "NQ", "USD", 0, f.Plan.CreatedAtUtc));
        Assert.Throws<InvalidOperationException>(() => TradingPlanReferenceSnapshot.Rehydrate(r.Account,
            [r.Instruments[0], r.Instruments[0]], r.Setups, r.Rules, r.CapturedAtUtc));
    }
}
