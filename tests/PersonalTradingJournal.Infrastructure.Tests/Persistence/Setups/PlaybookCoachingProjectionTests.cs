using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Application.Trades;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Setups;

public sealed partial class PlaybookPersistenceTests
{
    [Fact]
    public async Task CoachingProjectionContainsFrozenAnnotationsButNeverOwnedImageStorageMetadata()
    {
        await using var f = await Fixture.Create(); var g = await f.Seed();
        var definition = await SaveImage(f, g);
        var store = f.Get<ITradePlaybookAssociationStore>();
        Assert.True(await store.TryApplyAsync(await store.PreviewAsync(g.Trade.Id, g.Setup.Id)));
        var evidence = await f.Get<IDailyReviewEvidenceReader>().GetAsync(new(new(2026, 10, 11)));
        var packet = CoachingEvidencePacketBuilder.Build(evidence).Packet!;
        var example = Assert.Single(packet.Content.RecordedTradeFacts[0].Playbook!.Associations[0].Guidance!.Examples);
        var original = Assert.Single(definition.Guidance.Examples);
        Assert.True(example.HasImage); Assert.Equal(original.Annotation, example.Annotation); Assert.Equal(original.Id, example.Id);
        Assert.DoesNotContain(original.Image!.StorageKey, packet.Json);
        Assert.DoesNotContain(original.Image.FileName, packet.Json);
        Assert.DoesNotContain(original.Image.Sha256, packet.Json);
        Assert.DoesNotContain(f.Root, packet.Json);
        Assert.Contains("No chart images", packet.Content.ContentHandling);
        var response = new CoachingResponse(packet.ContractVersion, packet.PacketId,
            new("User-authored plan, not adherence", [CoachingEvidencePacketBuilder.PlaybookSource(g.Trade.Id, 1)]), [], [], [], []);
        var saved = await f.Get<ICoachingAnalysisRepository>().SaveAsync(CoachingAnalysisSnapshot.Create(packet,
            new(CoachingGenerationStatus.Success, response, new("Fake", "test-model", "client-synthetic"), "Success"), Now));
        Assert.True(await store.TryApplyAsync(await store.PreviewAsync(g.Trade.Id, null)));
        await f.Restart();
        Assert.Equal(saved.EvidenceJson, (await f.Get<ICoachingAnalysisRepository>().GetAsync(saved.Summary.Id))!.EvidenceJson);
        Assert.Equal(ReviewPlaybookState.NoAssociation,
            (await f.Get<IDailyReviewEvidenceReader>().GetAsync(new(new(2026, 10, 11)))).Trades[0].Playbook!.CurrentState);
    }
}
