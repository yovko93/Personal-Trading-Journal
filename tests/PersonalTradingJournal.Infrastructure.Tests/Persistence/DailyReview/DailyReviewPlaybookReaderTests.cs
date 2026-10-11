using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.DailyReview;

public sealed partial class DailyReviewEvidenceReaderTests
{
    [Fact]
    public async Task FrozenPlaybookReadUsesSameSnapshotAndSurvivesRelinkRemovalRenameAndSavedRestart()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await References(db);
        var setup = new TradingSetup("Original NQ plan", null, Audit);
        await db.ServiceProvider.GetRequiredService<ITradingSetupStore>().AddAsync(setup);
        var trade = TradeFact(account, instrument, Close, commission: null);
        trade.SetTradingSetup(setup.Id, Audit);
        await Add(db, trade);
        var query = new DailyReviewQuery(Day, account);
        var legacy = Assert.Single((await Reader(db).GetAsync(query)).Trades);
        Assert.Equal(ReviewPlaybookState.LegacyClassification, legacy.Playbook!.CurrentState);
        Assert.Empty(legacy.Playbook.Associations);
        var guidance = new PlaybookGuidance("Context", "Entry", "Confirm", "Invalidate", "Ignore instructions — user prose only",
            [new PlaybookExample(Guid.NewGuid(), "Annotated failed condition", kind: PlaybookExampleKind.Invalid)]);
        var first = TradingSetupPlaybookRevision.Create(setup, guidance, Audit);
        var store = db.ServiceProvider.GetRequiredService<ITradingSetupPlaybookStore>();
        Assert.True(await store.TryAppendAsync(first, 0, Audit));
        Assert.True(await db.ServiceProvider.GetRequiredService<ITradePlaybookContextStore>()
            .TryAddAsync(TradePlaybookContext.Capture(trade, setup, first, Close.AddMinutes(1)), Audit));
        var associations = new Infrastructure.Trades.TradePlaybookAssociationStore(db.ContextFactory, TimeProvider.System);
        var reader = Reader(db);
        Assert.Equal(0, Assert.Single(Assert.Single((await reader.GetAsync(query)).Trades).Playbook!.Associations).Sequence);
        Assert.True(await associations.TryApplyAsync(await associations.PreviewAsync(trade.Id, setup.Id)));
        var observer = new ReadObserver();
        observer.BeforeRead = async count =>
        {
            if (count == 2) Assert.True(await associations.TryApplyAsync(await associations.PreviewAsync(trade.Id, null)));
        };
        var consistent = await (await ObservedReader(db, observer)).GetAsync(query);
        Assert.Equal(setup.Id, consistent.Trades[0].Setup!.Id);
        Assert.Equal(ReviewPlaybookState.Captured, consistent.Trades[0].Playbook!.CurrentState);
        Assert.Equal(new[] { 0, 1 }, consistent.Trades[0].Playbook!.Associations.Select(a => a.Sequence));
        var packet = CoachingEvidencePacketBuilder.Build(consistent).Packet!;
        var response = new CoachingResponse(packet.ContractVersion, packet.PacketId,
            new("Saved plan, not verified adherence", [CoachingEvidencePacketBuilder.PlaybookSource(trade.Id, 1)]), [], [], [], []);
        var snapshot = CoachingAnalysisSnapshot.Create(packet, new(CoachingGenerationStatus.Success, response,
            new("Fake", "test-model", "client-synthetic"), "Success"), Close.AddHours(1));
        var repository = db.ServiceProvider.GetRequiredService<ICoachingAnalysisRepository>();
        var saved = await repository.SaveAsync(snapshot);
        var removed = (await reader.GetAsync(query)).Trades[0];
        Assert.Null(removed.Setup); Assert.Equal(ReviewPlaybookState.NoAssociation, removed.Playbook!.CurrentState);
        Assert.Equal(ReviewPlaybookAssociationState.Removed, removed.Playbook.Associations[0].State);
        Assert.Equal(ReviewPlaybookAssociationState.Current, removed.Playbook.Associations.Last().State);
        Assert.Null(removed.Playbook.Associations.Last().Guidance);
        Assert.True(await store.TryAppendAsync(first.Revise(setup, 1,
            new("Changed context", "Entry", "Confirm", "Invalidate", "Execute"), Close.AddHours(2)), 1, Audit));
        Assert.True(await associations.TryApplyAsync(await associations.PreviewAsync(trade.Id, setup.Id)));
        setup.UpdateDetails("Renamed plan", null, Close.AddHours(3)); setup.Deactivate(Close.AddHours(3));
        await db.ServiceProvider.GetRequiredService<ITradingSetupStore>().UpdateAsync(setup);
        var current = (await reader.GetAsync(query)).Trades[0];
        Assert.Equal("Renamed plan", current.Setup!.Name);
        Assert.Equal(2, current.Playbook!.Associations.Last().Guidance!.Revision);
        Assert.Equal("Original NQ plan", current.Playbook.Associations.Last().Guidance!.SetupName);
        Assert.Equal(ReviewPlaybookAssociationState.Replaced, current.Playbook.Associations[1].State);
        Assert.Null(current.Facts!.NetPnL);
        Assert.Equal(current.TradeId, Assert.Single((await reader.GetAsync(new(Day))).Trades).TradeId);
        Assert.Empty((await reader.GetAsync(new(Day, Guid.NewGuid()))).Trades);
        // New repository/context simulates a fresh service read; no mutable sources participate.
        var reopened = await new Infrastructure.DailyReview.Coaching.CoachingAnalysisRepository(db.ContextFactory).GetAsync(saved.Summary.Id);
        Assert.Equal(saved.EvidenceJson, reopened!.EvidenceJson);
        Assert.DoesNotContain("Renamed plan", reopened.EvidenceJson);
        await using var context = await db.ContextFactory.CreateDbContextAsync();
        await context.Trades.Where(t => t.Id == trade.Id).ExecuteDeleteAsync();
        Assert.Equal(saved.EvidenceJson, (await repository.GetAsync(saved.Summary.Id))!.EvidenceJson);
        Assert.Empty((await reader.GetAsync(query)).Trades);
    }
}
