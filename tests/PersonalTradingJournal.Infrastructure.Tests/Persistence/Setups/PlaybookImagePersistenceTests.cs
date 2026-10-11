using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonalTradingJournal.Application.Backups;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Infrastructure.Backups;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Setups;

public sealed partial class PlaybookPersistenceTests
{
    private static readonly byte[] ImageBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
    private static Task<TradingSetupPlaybookRevision> SaveImage(Fixture f, Graph g, PlaybookExampleKind kind = PlaybookExampleKind.Valid)
    {
        Guid id = Guid.NewGuid();
        return f.Get<SavePlaybookExamplesUseCase>().ExecuteAsync(new(g.Setup.Id, 0,
            Guidance(new PlaybookExample(id, "Synthetic context; condition; lesson", kind: kind))), [new(id, "chart.png", ImageBytes)]);
    }

    [Theory]
    [InlineData(PlaybookExampleKind.Valid)] [InlineData(PlaybookExampleKind.Invalid)]
    public async Task OwnedChartBytesAndAnnotationsSurviveRemovalRestartBackupPreflightAndRestore(PlaybookExampleKind kind)
    {
        await using var f = await Fixture.Create(); var g = await f.Seed();
        var first = await SaveImage(f, g, kind); var example = Assert.Single(first.Guidance.Examples);
        var image = Assert.IsType<PlaybookExampleImage>(example.Image);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(ImageBytes)), image.Sha256);
        Assert.Equal(ImageBytes, await f.Get<IPlaybookImageStorage>().ReadAsync(image));
        Assert.True(await f.Contexts.TryAddAsync(PersonalTradingJournal.Domain.Trades.TradePlaybookContext.Capture(g.Trade, g.Setup, first, Now.AddMinutes(1)), Now));
        await f.Get<SaveTradingSetupPlaybookUseCase>().ExecuteAsync(new(g.Setup.Id, 1, Guidance()));
        await f.Get<IPlaybookImageStorage>().DeleteUnreferencedAsync(image.StorageKey); // Still held by revision 1.
        await f.Restart();
        Assert.Empty((await f.Store.GetLatestAsync(g.Setup.Id))!.Guidance.Examples);
        Assert.Equal(kind, Assert.Single((await f.History.GetRevisionAsync(g.Setup.Id, 1))!.Guidance.Examples).Kind);
        Assert.Equal(image, Assert.Single((await f.Contexts.GetAsync(g.Trade.Id))!.Definition.Guidance.Examples).Image);
        string export = Path.Combine(f.Root, "export"); Assert.True((await f.Get<IPortableExportService>().CreateAsync(export)).Succeeded);
        using (var json = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(export, PortableExportContract.DataFile))))
        {
            var row = Assert.Single(json.RootElement.GetProperty("tables").GetProperty("playbookExamples").EnumerateArray());
            Assert.Equal((int)kind, row.GetProperty("kind").GetInt32()); Assert.Equal(image.Sha256, row.GetProperty("imageSha256").GetString());
        }
        string archive = Path.Combine(f.Root, "images.ptjbackup");
        Assert.True((await f.Get<IBackupArchiveService>().CreateAsync(archive)).Succeeded);
        var preview = await f.Get<IRestorePreflightService>().InspectAsync(archive, f.Root); Assert.True(preview.Validated, preview.ToString());
        Assert.Equal(2, preview.Summary!.ScreenshotFileCount);
        using (var zip = ZipFile.OpenRead(archive))
        { using var stream = zip.GetEntry(BackupArchiveContract.ScreenshotsPrefix + image.StorageKey)!.Open(); using var copy = new MemoryStream(); stream.CopyTo(copy); Assert.Equal(ImageBytes, copy.ToArray()); }
        await f.Seed("Later"); await f.Stop();
        var restored = await new JournalRestoreService(f.Paths).RestoreAsync(new(archive, preview.Summary.ArchiveSha256));
        Assert.True(restored.Succeeded, restored.ToString()); Assert.True(File.Exists(restored.RecoveryArchivePath));
        await f.Restart(); Assert.Equal(ImageBytes, await f.Get<IPlaybookImageStorage>().ReadAsync(image));
        Assert.Equal(example.Annotation, Assert.Single((await f.History.GetRevisionAsync(g.Setup.Id, 1))!.Guidance.Examples).Annotation);
    }

    [Theory]
    [InlineData("stale")] [InlineData("metadata-failure")] [InlineData("cancel")]
    public async Task FailedImageAppendCleansNewFilesAndKeepsOriginalRevision(string failure)
    {
        await using var f = await Fixture.Create(); var g = await f.Seed();
        if (failure == "stale") await f.Save(g);
        await using var db = f.Context();
        if (failure == "metadata-failure") await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER SyntheticImageFailure BEFORE INSERT ON PlaybookExamples BEGIN SELECT RAISE(ABORT, 'synthetic'); END;");
        var before = Directory.GetFiles(f.Paths.ScreenshotsDirectory).Order().ToArray();
        using var cancellation = new CancellationTokenSource(); if (failure == "cancel") cancellation.Cancel();
        Guid id = Guid.NewGuid();
        await Assert.ThrowsAnyAsync<Exception>(() => f.Get<SavePlaybookExamplesUseCase>().ExecuteAsync(new(g.Setup.Id, 0,
            Guidance(new PlaybookExample(id, "retain note", kind: PlaybookExampleKind.Invalid))), [new(id, "chart.png", ImageBytes)], cancellation.Token));
        Assert.Equal(before, Directory.GetFiles(f.Paths.ScreenshotsDirectory).Order().ToArray());
        Assert.Equal(failure == "stale" ? 1 : 0, await db.PlaybookRevisions.CountAsync());
        Assert.Empty(await db.PlaybookExamples.ToArrayAsync());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task MissingOrChangedOwnedImageRejectsSaveAndBackupWithoutLosingNotes(bool changed)
    {
        await using var f = await Fixture.Create(); var g = await f.Seed(); var first = await SaveImage(f, g);
        var image = first.Guidance.Examples[0].Image!; string path = Path.Combine(f.Paths.ScreenshotsDirectory, image.StorageKey);
        if (changed) { var altered = ImageBytes.ToArray(); altered[^1] ^= 1; await File.WriteAllBytesAsync(path, altered); } else File.Delete(path);
        var readError = await Assert.ThrowsAnyAsync<Exception>(() => f.Get<IPlaybookImageStorage>().ReadAsync(image));
        var saveError = await Assert.ThrowsAnyAsync<Exception>(() => f.Get<SaveTradingSetupPlaybookUseCase>().ExecuteAsync(new(g.Setup.Id, 1, first.Guidance)));
        foreach (var error in new[] { readError, saveError })
            if (changed) Assert.IsType<InvalidDataException>(error); else Assert.IsType<FileNotFoundException>(error);
        string archive = Path.Combine(f.Root, "invalid.ptjbackup"); var result = await f.Get<IBackupArchiveService>().CreateAsync(archive);
        Assert.False(result.Succeeded); Assert.False(File.Exists(archive));
        Assert.Single(await f.History.GetHistoryAsync(g.Setup.Id, null, 10));
        Assert.Equal(first.Guidance.Examples[0].Annotation, (await f.Store.GetLatestAsync(g.Setup.Id))!.Guidance.Examples[0].Annotation);
    }

    [Theory]
    [InlineData("screenshot")] [InlineData("trade")] [InlineData("account-trades")]
    public async Task TradeCleanupCannotDeleteAFileRetainedByAnOwnedPlaybookExample(string deletion)
    {
        await using var f = await Fixture.Create(); var g = await f.Seed();
        byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(f.Paths.ScreenshotsDirectory, g.Screenshot.StorageKey));
        var image = new PlaybookExampleImage(g.Screenshot.StorageKey, "chart.png", bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)));
        await f.Get<SaveTradingSetupPlaybookUseCase>().ExecuteAsync(new(g.Setup.Id, 0, Guidance(new PlaybookExample(Guid.NewGuid(), "Shared chart", kind: PlaybookExampleKind.Valid, image: image))));
        var files = f.Get<ITradeScreenshotFileStorage>();
        if (deletion == "screenshot") await new DeleteTradeScreenshotUseCase(f.Get<ITradeScreenshotDeletionStore>(), files).ExecuteAsync(new(g.Trade.Id, g.Screenshot.Id));
        else if (deletion == "trade") await new DeleteTradeUseCase(f.Get<ITradeDeletionStore>(), files).ExecuteAsync(g.Trade.Id);
        else { var store = f.Get<IAccountTradeDeletionStore>(); var result = await store.DeleteAsync((await store.PrepareAsync(g.Trade.TradingAccountId))!); Assert.Empty(result.ScreenshotStorageKeys); }
        await f.Get<IPlaybookImageStorage>().DeleteUnreferencedAsync(image.StorageKey);
        Assert.Equal(bytes, await f.Get<IPlaybookImageStorage>().ReadAsync(image));
    }

    [Fact]
    public async Task NewReferenceBetweenTradeDatabaseDeleteAndPhysicalCleanupRetainsTheImage()
    {
        await using var f = await Fixture.Create(); var g = await f.Seed();
        var deletion = (await f.Get<ITradeDeletionStore>().DeleteAsync(g.Trade.Id))!;
        string key = Assert.Single(deletion.ScreenshotStorageKeys);
        byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(f.Paths.ScreenshotsDirectory, key));
        var image = new PlaybookExampleImage(key, "shared.png", bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)));
        await f.Get<SaveTradingSetupPlaybookUseCase>().ExecuteAsync(new(g.Setup.Id, 0,
            Guidance(new PlaybookExample(Guid.NewGuid(), "Retained during cleanup gap", kind: PlaybookExampleKind.Valid, image: image))));
        await f.Get<ITradeScreenshotFileStorage>().DeleteIfExistsAsync(key);
        Assert.Equal(bytes, await f.Get<IPlaybookImageStorage>().ReadAsync(image));
    }

    [Fact]
    public async Task AdditiveImageMigrationKeepsPreImageFrozenJsonByteForByteAndLegacyExamplesUnclassified()
    {
        await using var f = await Fixture.Create(preM17: true); var g = await f.Seed();
        await using var db = f.Context(); await db.GetService<IMigrator>().MigrateAsync("20261011154034_AddPlaybookHistory");
        // Seed the historical schema directly: the new EF model cannot query absent columns.
        string setup = g.Setup.Id.ToString().ToUpperInvariant(); string example = Guid.NewGuid().ToString().ToUpperInvariant();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO PlaybookRevisions VALUES ({setup},1,1,{"NQ"},{(string?)null},{"c"},{"e"},{"c"},{"i"},{"x"},{Now.UtcDateTime});");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO PlaybookExamples VALUES ({setup},1,{example},0,{"old note"},{(Guid?)null},{(Guid?)null});");
        await db.Database.MigrateAsync();
        var old = (await f.History.GetRevisionAsync(g.Setup.Id, 1))!;
        Assert.Equal(PlaybookExampleKind.Unspecified, old.Guidance.Examples[0].Kind); Assert.Null(old.Guidance.Examples[0].Image);
        string json = PlaybookPersistenceMapper.Serialize(old);
        Assert.DoesNotContain("Image", json); Assert.DoesNotContain("Kind", json);
        Assert.Equal(json, PlaybookPersistenceMapper.Serialize(PlaybookPersistenceMapper.Deserialize(json)));
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("UPDATE PlaybookExamples SET Annotation = 'changed';"));
    }
}
