using System.IO.Compression;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Backups;
using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Screenshots;
using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Backups;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;
using PersonalTradingJournal.Infrastructure.Setups;
using PersonalTradingJournal.Infrastructure.Storage;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Setups;

public sealed partial class PlaybookPersistenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
    private static PlaybookGuidance Guidance(params PlaybookExample[] examples) =>
        new("Synthetic NQ context", "Entry", "Confirm", "Invalidate", "Execute", examples);

    [Fact]
    public async Task RestartRehydratesGuidanceExamplesAndFrozenTradeContextWithoutLiveSetupSubstitution()
    {
        await using var f = await Fixture.Create();
        var g = await f.Seed();
        var first = await f.Save(g, true);
        var captured = TradePlaybookContext.Capture(g.Trade, g.Setup, first, Now.AddHours(1));
        Assert.True(await f.Contexts.TryAddAsync(captured, Now));
        g.Setup.UpdateDetails("Renamed NQ", "Changed description", Now.AddHours(2));
        await f.Get<ITradingSetupStore>().UpdateAsync(g.Setup);
        var second = first.Revise(g.Setup, 1, new("New context", "e", "c", "i", "x"), Now.AddHours(2));
        Assert.True(await f.Store.TryAppendAsync(second, 1, g.Setup.UpdatedAtUtc));
        g.Setup.Deactivate(Now.AddHours(3));
        await f.Get<ITradingSetupStore>().UpdateAsync(g.Setup);
        await using (var db = f.Context())
        {
            await db.Trades.Where(t => t.Id == g.Trade.Id).ExecuteUpdateAsync(s => s.SetProperty(t => t.TradingSetupId, (Guid?)null));
        }
        await f.Restart();
        var history = await f.History.GetHistoryAsync(g.Setup.Id, null, 10);
        Assert.Equal(new[] { 2, 1 }, history.Select(r => r.Revision));
        Assert.Equal("Renamed NQ", history[0].SetupName);
        Assert.Equal("Synthetic NQ", history[1].SetupName);
        var example = Assert.Single(history[1].Guidance.Examples);
        Assert.Equal(g.Screenshot.Id, example.SourceScreenshotId);
        Assert.Equal(g.Trade.Id, example.SourceTradeId);
        Assert.Equal("Synthetic annotated retest", example.Annotation);
        var read = Assert.IsType<TradePlaybookContext>(await f.Contexts.GetAsync(g.Trade.Id));
        Assert.Equal("Synthetic NQ", read.Definition.SetupName);
        Assert.Equal(first.RecordedAtUtc, read.Definition.RecordedAtUtc);
        Assert.Equal("Synthetic NQ context", read.Definition.Guidance.MarketContext);
        Assert.Equal(example.Id, Assert.Single(read.Definition.Guidance.Examples).Id);
        Assert.Equal(captured.CapturedAtUtc, read.CapturedAtUtc);
        Assert.True(read.IsRetrospective);
        Assert.Equal(g.Trade.OpenedAtUtc, read.TradeOpenedAtUtc);
    }

    [Fact]
    public async Task TwoConcurrentWritersAppendExactlyOneConsecutiveRevisionAndStaleRetryWritesNothing()
    {
        await using var f = await Fixture.Create(); var g = await f.Seed(); var first = await f.Save(g);
        var a = first.Revise(g.Setup, 1, Guidance(), Now.AddMinutes(1));
        var b = first.Revise(g.Setup, 1, new("Other", "e", "c", "i", "x"), Now.AddMinutes(1));
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> Write(TradingSetupPlaybookRevision revision) => Task.Run(async () =>
        { await start.Task; return await f.Store.TryAppendAsync(revision, 1, Now); });
        var one = Write(a); var two = Write(b); start.SetResult();
        bool[] results = await Task.WhenAll(one, two).WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Single(results, r => r); Assert.Single(results, r => !r);
        Assert.False(await f.Store.TryAppendAsync(a, 1, Now));
        var current = (await f.Store.GetLatestAsync(g.Setup.Id))!;
        Assert.Equal(2, current.Revision);
        Assert.True(await f.Store.TryAppendAsync(current.Revise(g.Setup, 2, Guidance(), Now.AddMinutes(2)), 2, Now));
        Assert.Equal(new[] { 3, 2, 1 }, (await f.History.GetHistoryAsync(g.Setup.Id, null, 10)).Select(r => r.Revision));
    }

    [Fact]
    public async Task ExampleInsertFailureRollsBackEntireRevisionAndKeepsExpectedNextNumber()
    {
        await using var f = await Fixture.Create(); var g = await f.Seed(); var first = await f.Save(g);
        await using var db = f.Context();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER SyntheticFailure BEFORE INSERT ON PlaybookExamples BEGIN SELECT RAISE(ABORT, 'synthetic'); END;");
        var next = first.Revise(g.Setup, 1, Guidance(PlaybookExample.FromScreenshot(Guid.NewGuid(), "note", g.Screenshot)), Now.AddMinutes(1));
        await Assert.ThrowsAsync<DbUpdateException>(() => f.Store.TryAppendAsync(next, 1, Now));
        Assert.Equal(1, await db.PlaybookRevisions.CountAsync()); Assert.Empty(await db.PlaybookExamples.ToArrayAsync());
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER SyntheticFailure;");
        Assert.True(await f.Store.TryAppendAsync(next, 1, Now));
        Assert.Equal(2, (await f.Store.GetLatestAsync(g.Setup.Id))!.Revision);
    }

    [Theory]
    [InlineData("archive")]
    [InlineData("rename-same-timestamp")]
    [InlineData("delete")]
    public async Task ChangedOwnerBlocksAppendWithoutChangingExistingSetupOrOtherData(string change)
    {
        await using var f = await Fixture.Create(); var g = await f.Seed();
        var candidate = TradingSetupPlaybookRevision.Create(g.Setup, Guidance(), Now);
        if (change == "delete")
        {
            await using var db = f.Context();
            await db.Trades.Where(t => t.Id == g.Trade.Id).ExecuteUpdateAsync(s => s.SetProperty(t => t.TradingSetupId, (Guid?)null));
            await f.Get<ITradingSetupDeletionStore>().DeleteAsync(g.Setup.Id);
        }
        else
        {
            if (change == "archive") g.Setup.Deactivate(Now); else g.Setup.UpdateDetails("Changed", null, Now);
            await f.Get<ITradingSetupStore>().UpdateAsync(g.Setup);
        }
        Assert.False(await f.Store.TryAppendAsync(candidate, 0, Now));
        Assert.Empty(await f.History.GetHistoryAsync(g.Setup.Id, null, 10));
        await using var read = f.Context(); Assert.Equal(1, await read.Trades.CountAsync());
    }

    [Fact]
    public async Task ArchivedHistoryRemainsReadableAndReactivationContinuesNumbering()
    {
        await using var f = await Fixture.Create(); var g = await f.Seed(); var first = await f.Save(g);
        var candidate = first.Revise(g.Setup, 1, Guidance(), Now.AddMinutes(2));
        g.Setup.Deactivate(Now.AddMinutes(1)); await f.Get<ITradingSetupStore>().UpdateAsync(g.Setup);
        Assert.False(await f.Store.TryAppendAsync(candidate, 1, g.Setup.UpdatedAtUtc));
        Assert.Equal(1, (await f.History.GetRevisionAsync(g.Setup.Id, 1))!.Revision);
        g.Setup.Activate(Now.AddMinutes(3)); await f.Get<ITradingSetupStore>().UpdateAsync(g.Setup);
        Assert.True(await f.Store.TryAppendAsync(first.Revise(g.Setup, 1, Guidance(), Now.AddMinutes(3)), 1, g.Setup.UpdatedAtUtc));
        await using var db = f.Context();
        await db.Trades.Where(t => t.Id == g.Trade.Id).ExecuteUpdateAsync(s => s.SetProperty(t => t.TradingSetupId, (Guid?)null));
        Assert.Equal(DeleteTradingSetupResult.Referenced, await new DeleteTradingSetupUseCase(f.Get<ITradingSetupStore>(), f.Get<ITradingSetupDeletionStore>()).ExecuteAsync(g.Setup.Id));
    }

    [Theory]
    [InlineData("UPDATE PlaybookRevisions SET SetupName = 'tampered'")]
    [InlineData("DELETE FROM PlaybookRevisions")]
    [InlineData("UPDATE PlaybookExamples SET Annotation = 'tampered'")]
    [InlineData("DELETE FROM PlaybookExamples")]
    [InlineData("UPDATE TradePlaybookContexts SET DefinitionJson = 'tampered'")]
    public async Task DatabaseImmutableHistoryRejectsMutation(string sql)
    {
        await using var f = await Fixture.Create(); var g = await f.Seed(); var first = await f.Save(g, true);
        Assert.True(await f.Contexts.TryAddAsync(TradePlaybookContext.Capture(g.Trade, g.Setup, first, Now.AddMinutes(1)), Now));
        await using var db = f.Context();
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync(sql));
        Assert.Single(await f.History.GetHistoryAsync(g.Setup.Id, null, 10));
        Assert.NotNull(await f.Contexts.GetAsync(g.Trade.Id));
    }

    [Fact]
    public async Task HistoryUsesBoundedStableCursorAndUnavailableResultsAreEmpty()
    {
        await using var f = await Fixture.Create(); var g = await f.Seed(); var current = await f.Save(g);
        for (int i = 1; i < 6; i++)
        { var next = current.Revise(g.Setup, i, Guidance(), Now.AddMinutes(i)); Assert.True(await f.Store.TryAppendAsync(next, i, Now)); current = next; }
        var page = await f.History.GetHistoryAsync(g.Setup.Id, null, 2);
        Assert.Equal(new[] { 6, 5 }, page.Select(r => r.Revision));
        Assert.Equal(new[] { 4, 3 }, (await f.History.GetHistoryAsync(g.Setup.Id, page[^1].Revision, 2)).Select(r => r.Revision));
        Assert.Empty(await f.History.GetHistoryAsync(g.Setup.Id, 1, 10));
        Assert.Null(await f.History.GetRevisionAsync(g.Setup.Id, 100));
        Assert.Null(await f.Store.GetLatestAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => f.History.GetHistoryAsync(g.Setup.Id, null, 101));
    }

    [Fact]
    public async Task ContextRejectsStaleTradeForgedDefinitionAndSecondCaptureWithoutChangingEconomics()
    {
        await using var f = await Fixture.Create(); var g = await f.Seed(); var first = await f.Save(g);
        var captured = TradePlaybookContext.Capture(g.Trade, g.Setup, first, Now.AddMinutes(1));
        Assert.False(await f.Contexts.TryAddAsync(captured, Now.AddTicks(1)));
        var forged = TradingSetupPlaybookRevision.Create(g.Setup, new("Forged", "e", "c", "i", "x"), Now);
        Assert.False(await f.Contexts.TryAddAsync(TradePlaybookContext.Capture(g.Trade, g.Setup, forged, Now.AddMinutes(1)), Now));
        Assert.True(await f.Contexts.TryAddAsync(captured, Now));
        Assert.False(await f.Contexts.TryAddAsync(captured, Now));
        await using var db = f.Context();
        Assert.Equal(1, await db.TradePlaybookContexts.CountAsync());
        Assert.Equal(1, await db.TradeExecutions.CountAsync());
        Assert.Null((await db.TradeExecutions.SingleAsync()).Commission);
        Assert.Equal(Now, (await db.Trades.SingleAsync()).UpdatedAtUtc);
    }

    [Fact]
    public async Task TradeOwnedContextDeletesWithTradeButSetupRevisionIsRetained()
    {
        await using var f = await Fixture.Create(); var g = await f.Seed(); var first = await f.Save(g);
        Assert.True(await f.Contexts.TryAddAsync(TradePlaybookContext.Capture(g.Trade, g.Setup, first, Now.AddMinutes(1)), Now));
        await f.Get<ITradeDeletionStore>().DeleteAsync(g.Trade.Id);
        Assert.Null(await f.Contexts.GetAsync(g.Trade.Id));
        Assert.NotNull(await f.History.GetRevisionAsync(g.Setup.Id, 1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrWrongTradeScreenshotCannotCreatePartialRevision(bool wrongOwner)
    {
        await using var f = await Fixture.Create(); var g = await f.Seed();
        var example = new PlaybookExample(Guid.NewGuid(), "note", wrongOwner ? Guid.NewGuid() : g.Trade.Id,
            wrongOwner ? g.Screenshot.Id : Guid.NewGuid());
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Store.TryAppendAsync(TradingSetupPlaybookRevision.Create(g.Setup, Guidance(example), Now), 0, Now));
        await using var db = f.Context(); Assert.Empty(await db.PlaybookRevisions.ToArrayAsync()); Assert.Empty(await db.PlaybookExamples.ToArrayAsync());
    }

    [Theory]
    [InlineData("screenshot")]
    [InlineData("trade")]
    [InlineData("account-trades")]
    public async Task PinnedImageBlocksDeletionBeforeAnyDatabaseOrPhysicalFileLoss(string deletion)
    {
        await using var f = await Fixture.Create(); var g = await f.Seed(); await f.Save(g, true);
        var files = f.Get<ITradeScreenshotFileStorage>();
        if (deletion == "screenshot")
            await Assert.ThrowsAsync<InvalidOperationException>(() => new DeleteTradeScreenshotUseCase(f.Get<ITradeScreenshotDeletionStore>(), files).ExecuteAsync(new(g.Trade.Id, g.Screenshot.Id)));
        else if (deletion == "trade")
            await Assert.ThrowsAsync<InvalidOperationException>(() => new DeleteTradeUseCase(f.Get<ITradeDeletionStore>(), files).ExecuteAsync(g.Trade.Id));
        else
        {
            var store = f.Get<IAccountTradeDeletionStore>(); var plan = (await store.PrepareAsync(g.Trade.TradingAccountId))!;
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.DeleteAsync(plan));
        }
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(Path.Combine(f.Paths.ScreenshotsDirectory, g.Screenshot.StorageKey)));
        await using var db = f.Context(); Assert.Equal(1, await db.Trades.CountAsync()); Assert.Equal(1, await db.TradeScreenshots.CountAsync());
    }

    [Theory]
    [InlineData(false, "synthetic.png", "SYNTHETIC.png")]
    [InlineData(true, "synthetic.png", "SYNTHETIC.png")]
    [InlineData(false, "\u00e9xample.png", "\u00c9XAMPLE.png")]
    [InlineData(true, "\u00e9xample.png", "\u00c9XAMPLE.png")]
    public async Task DeletingAliasCannotRemoveImageStillOwnedByPinnedScreenshot(bool deleteTrade, string originalKey, string aliasKey)
    {
        await using var f = await Fixture.Create(); var g = await f.Seed(key: originalKey); await f.Save(g, true);
        var other = await f.Seed("Other", aliasKey);
        var files = f.Get<ITradeScreenshotFileStorage>();
        if (deleteTrade) await new DeleteTradeUseCase(f.Get<ITradeDeletionStore>(), files).ExecuteAsync(other.Trade.Id);
        else Assert.True((await new DeleteTradeScreenshotUseCase(f.Get<ITradeScreenshotDeletionStore>(), files).ExecuteAsync(new(other.Trade.Id, other.Screenshot.Id))).FileCleanupSucceeded);
        Assert.True(File.Exists(Path.Combine(f.Paths.ScreenshotsDirectory, g.Screenshot.StorageKey)));
        await using var db = f.Context(); Assert.True(await db.TradeScreenshots.AnyAsync(s => s.Id == g.Screenshot.Id));
    }

    [Fact]
    public async Task CancellationBeforeAndAfterSaveChangesRollsBackAndDoesNotConsumeRevision()
    {
        await using var f = await Fixture.Create(); var g = await f.Seed();
        var candidate = TradingSetupPlaybookRevision.Create(g.Setup, Guidance(), Now);
        using var early = new CancellationTokenSource(); early.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Store.TryAppendAsync(candidate, 0, Now, early.Token));
        using var late = new CancellationTokenSource();
        await using var source = f.Context();
        var options = new DbContextOptionsBuilder<JournalDbContext>().UseSqlite(source.Database.GetConnectionString())
            .AddInterceptors(new CancelSaved(late)).Options;
        var store = new TradingSetupPlaybookStore(new Factory(options));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.TryAppendAsync(candidate, 0, Now, late.Token));
        Assert.Null(await f.Store.GetLatestAsync(g.Setup.Id));
        Assert.True(await f.Store.TryAppendAsync(candidate, 0, Now));
    }

    [Fact]
    public async Task AdditiveMigrationPreservesPreM17SetupTradeClassificationExecutionsAndScreenshot()
    {
        await using var f = await Fixture.Create(preM17: true); var g = await f.Seed();
        await using (var db = f.Context())
        {
            Assert.DoesNotContain("20261011154034_AddPlaybookHistory", await db.Database.GetAppliedMigrationsAsync());
            await db.Database.MigrateAsync();
        }
        await f.Restart();
        Assert.Null(await f.Store.GetLatestAsync(g.Setup.Id));
        Assert.Null(await f.Contexts.GetAsync(g.Trade.Id));
        await using var read = f.Context();
        Assert.Equal(g.Setup.Id, (await read.Trades.SingleAsync()).TradingSetupId);
        Assert.Equal(g.Setup.Name, (await read.TradingSetups.SingleAsync()).Name);
        Assert.Equal(g.Trade.Executions[0].Id, (await read.TradeExecutions.SingleAsync()).Id);
        Assert.Equal(g.Screenshot.Id, (await read.TradeScreenshots.SingleAsync()).Id);
        Assert.True(File.Exists(Path.Combine(f.Paths.ScreenshotsDirectory, g.Screenshot.StorageKey)));
        Assert.Equal(1, (await f.Save(g)).Revision);
    }

    [Fact]
    public async Task CompleteBackupAndPreflightIncludePlaybookSnapshotAndPinnedImage()
    {
        await using var f = await Fixture.Create(); var g = await f.Seed(); var first = await f.Save(g, true);
        Assert.True(await f.Contexts.TryAddAsync(TradePlaybookContext.Capture(g.Trade, g.Setup, first, Now.AddMinutes(1)), Now));
        string archive = Path.Combine(f.Root, "synthetic.ptjbackup");
        var result = await f.Get<IBackupArchiveService>().CreateAsync(archive);
        Assert.True(result.Succeeded, result.ToString());
        var check = await f.Get<IRestorePreflightService>().InspectAsync(archive, f.Root);
        Assert.True(check.Validated, check.ToString());
        using var zip = ZipFile.OpenRead(archive);
        Assert.NotNull(zip.GetEntry(BackupArchiveContract.ScreenshotsPrefix + g.Screenshot.StorageKey));
        string path = Path.Combine(f.Root, "snapshot.db"); zip.GetEntry(BackupArchiveContract.DatabasePath)!.ExtractToFile(path);
        var options = new DbContextOptionsBuilder<JournalDbContext>().UseSqlite($"Data Source={path};Mode=ReadOnly;Pooling=False").Options;
        await using var saved = new JournalDbContext(options);
        Assert.Equal(1, await saved.PlaybookRevisions.CountAsync()); Assert.Equal(1, await saved.PlaybookExamples.CountAsync());
        Assert.Equal(1, await saved.TradePlaybookContexts.CountAsync());
    }

    [Fact]
    public async Task OfflineRestoreReopensFrozenContextAndRetainsRecoveryDataset()
    {
        await using var f = await Fixture.Create(); var g = await f.Seed(); var first = await f.Save(g, true);
        Assert.True(await f.Contexts.TryAddAsync(TradePlaybookContext.Capture(g.Trade, g.Setup, first, Now.AddMinutes(1)), Now));
        string archive = Path.Combine(f.Root, "restore.ptjbackup");
        Assert.True((await f.Get<IBackupArchiveService>().CreateAsync(archive)).Succeeded);
        var preview = await f.Get<IRestorePreflightService>().InspectAsync(archive, f.Root);
        Assert.True(preview.Validated, preview.ToString());
        await f.Seed("Later Trade");
        await f.Stop();
        var restored = await new JournalRestoreService(f.Paths).RestoreAsync(new(archive, preview.Summary!.ArchiveSha256));
        Assert.True(restored.Succeeded, restored.ToString());
        Assert.True(File.Exists(restored.RecoveryArchivePath));
        var recovery = await new RestorePreflightService().InspectAsync(restored.RecoveryArchivePath!, f.Root);
        Assert.True(recovery.Validated, recovery.ToString()); Assert.Equal(2, recovery.Summary!.TradeCount);
        await f.Restart();
        Assert.Equal("Synthetic NQ context", (await f.Contexts.GetAsync(g.Trade.Id))!.Definition.Guidance.MarketContext);
        Assert.Equal(g.Screenshot.Id, Assert.Single((await f.Store.GetLatestAsync(g.Setup.Id))!.Guidance.Examples).SourceScreenshotId);
        await using var db = f.Context(); Assert.Equal(1, await db.Trades.CountAsync());
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(Path.Combine(f.Paths.ScreenshotsDirectory, g.Screenshot.StorageKey)));
    }

    [Fact]
    public async Task LogicalExportVersionThreePreservesAllPlaybookFieldsAndExactFrozenDefinition()
    {
        await using var f = await Fixture.Create(); var g = await f.Seed(); var first = await f.Save(g, true);
        Assert.True(await f.Contexts.TryAddAsync(TradePlaybookContext.Capture(g.Trade, g.Setup, first, Now.AddMinutes(1)), Now));
        Assert.True(await f.Store.TryAppendAsync(first.Revise(g.Setup, 1, new("New context", "e", "c", "i", "x"), Now.AddMinutes(2)), 1, Now));
        string output = Path.Combine(f.Root, "export");
        Assert.True((await f.Get<IPortableExportService>().CreateAsync(output)).Succeeded);
        using var data = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(output, PortableExportContract.DataFile)));
        Assert.Equal(10, data.RootElement.GetProperty("version").GetInt32());
        var tables = data.RootElement.GetProperty("tables");
        var revisions = tables.GetProperty("playbookRevisions");
        Assert.Equal(new[] { 1, 2 }, revisions.EnumerateArray().Select(r => r.GetProperty("revision").GetInt32()));
        Assert.Equal(first.Guidance.MarketContext, revisions[0].GetProperty("marketContext").GetString());
        Assert.Equal("New context", revisions[1].GetProperty("marketContext").GetString());
        var example = Assert.Single(tables.GetProperty("playbookExamples").EnumerateArray());
        Assert.Equal(g.Screenshot.Id, example.GetProperty("sourceScreenshotId").GetGuid());
        Assert.Equal("Synthetic annotated retest", example.GetProperty("annotation").GetString());
        await using var db = f.Context();
        var snapshot = Assert.Single(tables.GetProperty("tradePlaybookContexts").EnumerateArray());
        Assert.Equal(g.Trade.Id, snapshot.GetProperty("tradeId").GetGuid());
        Assert.Equal((await db.TradePlaybookContexts.SingleAsync()).DefinitionJson, snapshot.GetProperty("definitionJson").GetString());
        Assert.Equal(4, Directory.GetFiles(output, "*.csv").Length);
        Assert.Empty(Directory.GetDirectories(output)); // Screenshot bytes are still metadata-only in exports.
    }

    [Fact]
    public async Task PreM17ArchiveIsRecognizedButRejectedWithoutMigratingOrChangingInstalledData()
    {
        await using var f = await Fixture.Create(preM17: true); var g = await f.Seed();
        string snapshot = Path.Combine(f.Root, "old.db"), archive = Path.Combine(f.Root, "old.ptjbackup");
        ImmutableArray<string> migrations;
        await using (var db = f.Context())
        {
            migrations = (await db.Database.GetAppliedMigrationsAsync()).ToImmutableArray();
            await db.Database.OpenConnectionAsync();
            using var destination = new SqliteConnection($"Data Source={snapshot};Pooling=False"); destination.Open();
            ((SqliteConnection)db.Database.GetDbConnection()).BackupDatabase(destination);
        }
        byte[] database = await File.ReadAllBytesAsync(snapshot), screenshot = [1, 2, 3];
        string shotPath = BackupArchiveContract.ScreenshotsPrefix + g.Screenshot.StorageKey;
        var manifest = new BackupManifest(BackupArchiveContract.Format, 1, "1.0.0", Now,
            new("sqlite", migrations), "SHA-256",
            [new(BackupArchiveContract.DatabasePath, BackupFileKind.Database, database.Length, Convert.ToHexStringLower(SHA256.HashData(database))),
             new(shotPath, BackupFileKind.Screenshot, screenshot.Length, Convert.ToHexStringLower(SHA256.HashData(screenshot)))]);
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            foreach (var entry in new[] { (BackupArchiveContract.DatabasePath, database), (shotPath, screenshot),
                         (BackupArchiveContract.ManifestPath, JsonSerializer.SerializeToUtf8Bytes(manifest, BackupArchiveContract.JsonOptions)) })
            { using var stream = zip.CreateEntry(entry.Item1).Open(); stream.Write(entry.Item2); }
        var result = await f.Get<IRestorePreflightService>().InspectAsync(archive, f.Root);
        Assert.False(result.Validated); Assert.Equal(BackupSchemaCompatibility.RequiresStagedMigration, result.Compatibility);
        Assert.Equal(BackupValidationCode.UnsupportedDatabaseSchema, result.Failure);
        Assert.Empty(Directory.GetDirectories(f.Root, ".preflight-*.partial"));
        await using var installed = f.Context();
        Assert.Equal(migrations.ToArray(), (await installed.Database.GetAppliedMigrationsAsync()).ToArray());
        Assert.Equal(g.Trade.Id, (await installed.Trades.SingleAsync()).Id);
    }

    [Theory]
    [InlineData("archive")]
    [InlineData("classification")]
    [InlineData("opening")]
    public async Task ContextInsertionRechecksActualMutableState(string change)
    {
        await using var f = await Fixture.Create(); var g = await f.Seed(); var definition = await f.Save(g);
        var capture = TradePlaybookContext.Capture(g.Trade, g.Setup, definition, Now.AddMinutes(1));
        await using var db = f.Context();
        if (change == "archive") await db.TradingSetups.ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));
        if (change == "classification") await db.Trades.ExecuteUpdateAsync(s => s.SetProperty(x => x.TradingSetupId, (Guid?)null));
        if (change == "opening") await db.TradeExecutions.ExecuteUpdateAsync(s => s.SetProperty(x => x.ExecutedAtUtc, Now.AddSeconds(1)));
        Assert.False(await f.Contexts.TryAddAsync(capture, Now));
        Assert.Empty(await db.TradePlaybookContexts.ToArrayAsync());
    }

    [Fact]
    public async Task ContextCommitCancellationAndFailureLeaveNoSnapshotAndBulkPlanDetectsNewContext()
    {
        await using var f = await Fixture.Create(); var g = await f.Seed(); var definition = await f.Save(g);
        var capture = TradePlaybookContext.Capture(g.Trade, g.Setup, definition, Now.AddMinutes(1));
        await using var db = f.Context();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER SyntheticContextFailure BEFORE INSERT ON TradePlaybookContexts BEGIN SELECT RAISE(ABORT, 'synthetic'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => f.Contexts.TryAddAsync(capture, Now));
        Assert.Null(await f.Contexts.GetAsync(g.Trade.Id));
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER SyntheticContextFailure;");
        using var cancelled = new CancellationTokenSource();
        var options = new DbContextOptionsBuilder<JournalDbContext>().UseSqlite(db.Database.GetConnectionString())
            .AddInterceptors(new CancelSaved(cancelled)).Options;
        var store = new PersonalTradingJournal.Infrastructure.Trades.TradePlaybookContextStore(new Factory(options));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.TryAddAsync(capture, Now, cancelled.Token));
        Assert.Null(await f.Contexts.GetAsync(g.Trade.Id));
        var deletion = f.Get<IAccountTradeDeletionStore>(); var plan = (await deletion.PrepareAsync(g.Trade.TradingAccountId))!;
        Assert.True(await f.Contexts.TryAddAsync(capture, Now));
        Assert.Equal(AccountTradeDeletionStatus.Changed, (await deletion.DeleteAsync(plan)).Status);
        Assert.NotNull(await f.Contexts.GetAsync(g.Trade.Id));
    }

    private sealed class Factory(DbContextOptions<JournalDbContext> options) : IDbContextFactory<JournalDbContext>
    { public JournalDbContext CreateDbContext() => new(options); }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class CancelSaved(CancellationTokenSource source) : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        { source.Cancel(); return ValueTask.FromResult(result); }
    }
    private sealed record Graph(TradingSetup Setup, Trade Trade, TradeScreenshot Screenshot);
    private sealed class Fixture : IAsyncDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "ptj-playbook-" + Guid.NewGuid().ToString("N"));
        internal LocalApplicationPaths Paths { get; }
        private ServiceProvider services;
        private Fixture() { Paths = new(Root); Paths.EnsureDirectoriesExist(); services = Build(); }
        private ServiceProvider Build() => new ServiceCollection().AddSingleton<TimeProvider>(new Clock()).AddPersistence(Paths).BuildServiceProvider();
        internal T Get<T>() where T : notnull => services.GetRequiredService<T>();
        internal JournalDbContext Context() => Get<IDbContextFactory<JournalDbContext>>().CreateDbContext();
        internal ITradingSetupPlaybookStore Store => Get<ITradingSetupPlaybookStore>();
        internal ITradingSetupPlaybookHistoryReader History => Get<ITradingSetupPlaybookHistoryReader>();
        internal ITradePlaybookContextStore Contexts => Get<ITradePlaybookContextStore>();
        internal static async Task<Fixture> Create(bool preM17 = false)
        {
            var f = new Fixture();
            try
            {
                await using var db = f.Context();
                string? target = preM17 ? "20261008195353_AddCoachingAnalysisSnapshots" : null;
                await db.GetService<IMigrator>().MigrateAsync(target);
                return f;
            }
            catch { await f.DisposeAsync(); throw; }
        }
        internal async Task Stop() { await services.DisposeAsync(); SqliteTestPoolCleanup.ClearPersistencePools(Paths.DatabasePath); }
        internal async Task Restart() { await Stop(); services = Build(); }
        internal async Task<Graph> Seed(string name = "Synthetic NQ", string? key = null)
        {
            var setup = new TradingSetup(name, "Original description", Now);
            Guid account = Guid.NewGuid(), instrument = Guid.NewGuid();
            await using (var db = Context())
            {
                db.TradingAccounts.Add(new TradingAccountRecord { Id = account, Name = name, AccountType = TradingAccountType.Demo, Currency = "USD", IsActive = true, CreatedAtUtc = Now, UpdatedAtUtc = Now });
                db.Instruments.Add(new InstrumentRecord { Id = instrument, Symbol = "SYN" + instrument.ToString("N"), DisplayName = "Synthetic", AssetClass = AssetClass.Futures, Currency = "USD", TickSize = 1, TickValue = 1, IsActive = true, CreatedAtUtc = Now, UpdatedAtUtc = Now });
                db.TradingSetups.Add(TradingSetupPersistenceMapper.ToRecord(setup)); await db.SaveChangesAsync();
            }
            var trade = Trade.Start(account, instrument, new TradePricingSnapshot(1, "USD"),
                new TradeExecution(Guid.NewGuid(), 1, Now, ExecutionSide.Buy, 1, 100, null, null, null, null, null), Now);
            trade.SetTradingSetup(setup.Id, Now); await Get<ITradeStore>().AddAsync(trade);
            var screenshot = new TradeScreenshot(trade.Id, TradeScreenshotType.Entry, key ?? Guid.NewGuid().ToString("N") + ".png", "example.png", Now, null, null, Now);
            await Get<ITradeScreenshotStore>().AddAsync(screenshot);
            await File.WriteAllBytesAsync(Path.Combine(Paths.ScreenshotsDirectory, screenshot.StorageKey), [1, 2, 3]);
            return new(setup, trade, screenshot);
        }
        internal async Task<TradingSetupPlaybookRevision> Save(Graph g, bool example = false)
        {
            var guidance = example ? Guidance(PlaybookExample.FromScreenshot(Guid.NewGuid(), "Synthetic annotated retest", g.Screenshot)) : Guidance();
            return await Get<SaveTradingSetupPlaybookUseCase>().ExecuteAsync(new(g.Setup.Id, 0, guidance));
        }
        public async ValueTask DisposeAsync()
        { await services.DisposeAsync(); SqliteTestPoolCleanup.ClearPersistencePools(Paths.DatabasePath); Directory.Delete(Root, true); }
    }
}
