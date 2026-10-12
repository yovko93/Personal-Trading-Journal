using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Application.Backups;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Setups;

public sealed partial class PlaybookPersistenceTests
{
    [Fact]
    public async Task DeleteMiddleRetainsSharedImagesAndCurrentAndNeverReusesNumbersAfterRestart()
    {
        await using var f = await Fixture.Create(); var g = await f.Seed(); var first = await SaveImage(f, g);
        var image = first.Guidance.Examples.Single().Image!;
        var save = f.Get<SaveTradingSetupPlaybookUseCase>();
        await save.ExecuteAsync(new(g.Setup.Id, 1, first.Guidance));
        await save.ExecuteAsync(new(g.Setup.Id, 2, first.Guidance));
        var deletion = f.Get<IPlaybookRevisionDeletion>();
        Assert.Equal(PlaybookRevisionDeletionStatus.CurrentRevision, (await deletion.CheckAsync(g.Setup.Id, 3)).Status);
        Assert.Equal(PlaybookRevisionDeletionStatus.CurrentRevision, await deletion.DeleteAsync(g.Setup.Id, 3, 3));
        Assert.Equal(new PlaybookRevisionDeletionCheck(PlaybookRevisionDeletionStatus.Eligible, 3), await deletion.CheckAsync(g.Setup.Id, 2));
        Assert.Equal(PlaybookRevisionDeletionStatus.Deleted, await deletion.DeleteAsync(g.Setup.Id, 2, 3));
        await f.Restart();
        Assert.Equal(new[] { 3, 1 }, (await f.History.GetHistoryAsync(g.Setup.Id, null, 10)).Select(r => r.Revision));
        Assert.Equal(ImageBytes, await f.Get<IPlaybookImageStorage>().ReadAsync(image));
        Assert.Equal(4, (await f.Get<SaveTradingSetupPlaybookUseCase>().ExecuteAsync(new(g.Setup.Id, 3, Guidance()))).Revision);
        await using var db = f.Context();
        Assert.False(await db.PlaybookExamples.AnyAsync(e => e.Revision == 2)); Assert.Single(await db.Trades.ToArrayAsync());
        Assert.Empty(await db.Database.SqlQueryRaw<string>("SELECT 'broken' AS Value FROM pragma_foreign_key_check").ToArrayAsync());
        string archive = Path.Combine(f.Root, "gapped-history.ptjbackup");
        Assert.True((await f.Get<IBackupArchiveService>().CreateAsync(archive)).Succeeded);
        Assert.True((await f.Get<IRestorePreflightService>().InspectAsync(archive, f.Root)).Validated);
        string export = Path.Combine(f.Root, "export");
        Assert.True((await f.Get<IPortableExportService>().CreateAsync(export)).Succeeded);
        using var json = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(export, PortableExportContract.DataFile)));
        Assert.Equal(new[] { 1, 3, 4 }, json.RootElement.GetProperty("tables").GetProperty("playbookRevisions").EnumerateArray().Select(r => r.GetProperty("revision").GetInt32()));
    }

    [Fact]
    public async Task TriggerMigrationPreservesExistingGuidanceAndAllowsOnlyEligibleOlderDeletion()
    {
        await using var f = await Fixture.Create(); var g = await f.Seed(); await f.Save(g, true);
        await f.Get<SaveTradingSetupPlaybookUseCase>().ExecuteAsync(new(g.Setup.Id, 1, Guidance()));
        await using var db = f.Context(); var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261011175435_AddTradePlaybookAssociations");
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM PlaybookExamples WHERE Revision=1"));
        await migrator.MigrateAsync();
        Assert.Equal(2, await db.PlaybookRevisions.CountAsync()); Assert.Single(await db.PlaybookExamples.ToArrayAsync());
        Assert.Equal(PlaybookRevisionDeletionStatus.Deleted, await f.Get<IPlaybookRevisionDeletion>().DeleteAsync(g.Setup.Id, 1, 2));
        Assert.Equal(2, (await f.Store.GetLatestAsync(g.Setup.Id))!.Revision);
        Assert.True(File.Exists(Path.Combine(f.Paths.ScreenshotsDirectory, g.Screenshot.StorageKey)));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task FrozenContextAndRemovedAssociationProtectHistoricalRevision(bool association)
    {
        await using var f = await Fixture.Create(); var g = await f.Seed(); var first = await f.Save(g, true);
        if (association)
        {
            var links = f.Get<ITradePlaybookAssociationStore>();
            Assert.True(await links.TryApplyAsync(await links.PreviewAsync(g.Trade.Id, g.Setup.Id)));
            Assert.True(await links.TryApplyAsync(await links.PreviewAsync(g.Trade.Id, null)));
        }
        else Assert.True(await f.Contexts.TryAddAsync(TradePlaybookContext.Capture(g.Trade, g.Setup, first, Now.AddMinutes(1)), Now));
        await f.Get<SaveTradingSetupPlaybookUseCase>().ExecuteAsync(new(g.Setup.Id, 1, Guidance()));
        var deletion = f.Get<IPlaybookRevisionDeletion>();
        Assert.Equal(PlaybookRevisionDeletionStatus.Referenced, (await deletion.CheckAsync(g.Setup.Id, 1)).Status);
        Assert.Equal(PlaybookRevisionDeletionStatus.Referenced, await deletion.DeleteAsync(g.Setup.Id, 1, 2));
        await using var db = f.Context();
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM PlaybookExamples WHERE Revision=1"));
        Assert.NotNull(await f.History.GetRevisionAsync(g.Setup.Id, 1));
        Assert.True(File.Exists(Path.Combine(f.Paths.ScreenshotsDirectory, g.Screenshot.StorageKey)));
    }

    [Fact]
    public async Task NewReferenceAfterCheckAndNewRevisionAfterConfirmationAreRechecked()
    {
        await using var f = await Fixture.Create(); var g = await f.Seed(); var first = await f.Save(g);
        var save = f.Get<SaveTradingSetupPlaybookUseCase>(); await save.ExecuteAsync(new(g.Setup.Id, 1, Guidance()));
        var deletion = f.Get<IPlaybookRevisionDeletion>();
        Assert.Equal(PlaybookRevisionDeletionStatus.Eligible, (await deletion.CheckAsync(g.Setup.Id, 1)).Status);
        Assert.True(await f.Contexts.TryAddAsync(TradePlaybookContext.Capture(g.Trade, g.Setup, first, Now.AddMinutes(1)), Now));
        Assert.Equal(PlaybookRevisionDeletionStatus.Referenced, await deletion.DeleteAsync(g.Setup.Id, 1, 2));
        await save.ExecuteAsync(new(g.Setup.Id, 2, Guidance()));
        Assert.Equal(PlaybookRevisionDeletionStatus.Conflict, await deletion.DeleteAsync(g.Setup.Id, 1, 2));
        Assert.Equal(3, (await f.Store.GetLatestAsync(g.Setup.Id))!.Revision);
    }

    [Fact]
    public async Task ConcurrentDeletionHasOneWinnerAndFailureRollsBackExampleRemoval()
    {
        await using var f = await Fixture.Create(); var g = await f.Seed(); var first = await f.Save(g, true);
        await f.Get<SaveTradingSetupPlaybookUseCase>().ExecuteAsync(new(g.Setup.Id, 1, Guidance()));
        var deletion = f.Get<IPlaybookRevisionDeletion>();
        await using var db = f.Context();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER SyntheticDeleteFailure BEFORE DELETE ON PlaybookRevisions BEGIN SELECT RAISE(ABORT, 'synthetic'); END;");
        await Assert.ThrowsAsync<SqliteException>(() => deletion.DeleteAsync(g.Setup.Id, 1, 2));
        Assert.Single((await f.History.GetRevisionAsync(g.Setup.Id, 1))!.Guidance.Examples);
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER SyntheticDeleteFailure;");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => deletion.DeleteAsync(g.Setup.Id, 1, 2, new(true)));
        var results = await Task.WhenAll(Task.Run(() => deletion.DeleteAsync(g.Setup.Id, 1, 2)), Task.Run(() => deletion.DeleteAsync(g.Setup.Id, 1, 2)));
        Assert.Single(results, s => s == PlaybookRevisionDeletionStatus.Deleted);
        Assert.Single(results, s => s == PlaybookRevisionDeletionStatus.Missing);
        Assert.True(File.Exists(Path.Combine(f.Paths.ScreenshotsDirectory, g.Screenshot.StorageKey)));
    }

    [Fact]
    public async Task SavedAnalysisIsSelfContainedAfterTradeAndEligibleRevisionDeletion()
    {
        await using var f = await Fixture.Create(); var g = await f.Seed(); var first = await SaveImage(f, g);
        var links = f.Get<ITradePlaybookAssociationStore>();
        Assert.True(await links.TryApplyAsync(await links.PreviewAsync(g.Trade.Id, g.Setup.Id)));
        var packet = CoachingEvidencePacketBuilder.Build(await f.Get<IDailyReviewEvidenceReader>().GetAsync(new(new(2026, 10, 11)))).Packet!;
        var response = new CoachingResponse(packet.ContractVersion, packet.PacketId,
            new("Synthetic plan", [CoachingEvidencePacketBuilder.PlaybookSource(g.Trade.Id, 1)]), [], [], [], []);
        var saved = await f.Get<ICoachingAnalysisRepository>().SaveAsync(CoachingAnalysisSnapshot.Create(packet,
            new(CoachingGenerationStatus.Success, response, new("Fake", "test-model", "synthetic"), "Success"), Now));
        await f.Get<SaveTradingSetupPlaybookUseCase>().ExecuteAsync(new(g.Setup.Id, 1, Guidance()));
        Assert.NotNull(await f.Get<ITradeDeletionStore>().DeleteAsync(g.Trade.Id));
        Assert.Equal(PlaybookRevisionDeletionStatus.Deleted, await f.Get<IPlaybookRevisionDeletion>().DeleteAsync(g.Setup.Id, 1, 2));
        await f.Restart();
        Assert.Equal(saved.EvidenceJson, (await f.Get<ICoachingAnalysisRepository>().GetAsync(saved.Summary.Id))!.EvidenceJson);
        Assert.Equal(ImageBytes, await f.Get<IPlaybookImageStorage>().ReadAsync(first.Guidance.Examples.Single().Image!));
    }
}
