using System.Collections.Immutable;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Backups;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Infrastructure.Backups;
using Fixture = PersonalTradingJournal.Infrastructure.Tests.Persistence.TradingPlans.TradingPlanPersistenceTests.Fixture;

namespace PersonalTradingJournal.Infrastructure.Tests.Backups;

public sealed class SetupExampleTests
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    [Fact]
    public async Task OriginalsAndOptionalTimeframesRoundTripAndDeletionChecksEveryOwner()
    {
        await using var f = await Fixture.Create();
        var store = f.Get<ISetupExampleStore>();
        var first = await store.AddAsync(f.Setup.Id, "entry.png", Png, " 5 min ", default);
        var second = await store.AddAsync(f.Setup.Id, "exit.png", Png, null, default);
        Assert.Equal(2, (await store.ListAsync(f.Setup.Id, default)).Count);
        string archive = Path.Combine(f.Root, "examples.ptjbackup");
        Assert.True((await f.Get<IBackupArchiveService>().CreateAsync(archive)).Succeeded);
        await using var target = await Fixture.Create();
        var check = await new RestorePreflightService().InspectAsync(archive, target.Root);
        Assert.True(check.Validated, check.ToString());
        string damaged = Path.Combine(f.Root, "damaged.ptjbackup");
        File.Copy(archive, damaged);
        using (var zip = ZipFile.Open(damaged, ZipArchiveMode.Update))
        {
            using var stream = zip.GetEntry(BackupArchiveContract.ScreenshotsPrefix + first.File.StorageKey)!.Open();
            stream.WriteByte(0);
        }
        Assert.False((await new RestorePreflightService().InspectAsync(damaged, target.Root)).Validated);
        await target.Stop();
        var restored = await new JournalRestoreService(target.Paths).RestoreAsync(new(archive, check.Summary!.ArchiveSha256));
        Assert.Equal(JournalRestoreStatus.Restored, restored.Status);
        await target.Restart();
        var examples = target.Get<ISetupExampleStore>();
        var rows = await examples.ListAsync(f.Setup.Id, default);
        Assert.Equal(new[]{ first, second }, rows);
        var files = target.Get<IPlaybookImageStorage>();
        Assert.Equal(Png, await files.ReadAsync(first.File));
        Assert.Equal(Png, await files.ReadAsync(second.File));
        Assert.Equal("5 min", first.Timeframe);
        Assert.Null(second.Timeframe);

        // A second Setup retains the same original. Deleting its first owner must keep it readable.
        var other = new TradingSetup("Independent", null, f.Now);
        await target.Get<ITradingSetupStore>().AddAsync(other);
        await using (var db = target.Context())
        {
            db.SetupExamples.Add(new() { Id = Guid.NewGuid(), SetupId = other.Id, FileName = first.File.FileName,
                StorageKey = first.File.StorageKey, SizeBytes = first.File.SizeBytes, Sha256 = first.File.Sha256 });
            await db.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => examples.RemoveAsync(other.Id, first.Id, default));
        await examples.RemoveAsync(f.Setup.Id, first.Id, default);
        Assert.Equal(Png, await files.ReadAsync(first.File));
        var delete = target.Get<ITradingSetupDeletionStore>();
        await delete.DeleteAsync(other.Id);
        Assert.False(File.Exists(Path.Combine(target.Paths.ScreenshotsDirectory, first.File.StorageKey)));
        Assert.Equal(Png, await files.ReadAsync(second.File));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreatePersistsSetupAndOptionalExamplesTogether(bool withExamples)
    {
        await using var f = await Fixture.Create();
        var create = new CreateTradingSetupUseCase(f.Get<ITradingSetupStore>(), f.Get<ITradingSetupNameChecker>(),
            TimeProvider.System, f.Get<ISetupExampleStore>());
        NewSetupExample[] images = withExamples ? [new("entry.png", Png, " 5 min "), new("exit.png", Png, null)] : [];
        var id = await create.ExecuteAsync(new("Retest", "New pattern", images));
        Assert.Equal("Retest", (await f.Get<ITradingSetupStore>().GetByIdAsync(id))!.Name);
        var saved = await f.Get<ISetupExampleStore>().ListAsync(id, default);
        Assert.Equal(images.Length, saved.Count);
        if (withExamples)
        {
            Assert.Equal("5 min", saved.Single(x => x.File.FileName == "entry.png").Timeframe);
            Assert.Null(saved.Single(x => x.File.FileName == "exit.png").Timeframe);
        }
        foreach (var image in saved)
            Assert.Equal(Png, await f.Get<IPlaybookImageStorage>().ReadAsync(image.File));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedSecondFileOrDatabaseWriteRemovesOnlyPreparedFiles(bool failDatabase)
    {
        await using var f = await Fixture.Create();
        var store = f.Get<ISetupExampleStore>();
        var existing = await store.AddAsync(f.Setup.Id, "existing.png", Png, null, default);
        var setup = failDatabase ? f.Setup : new TradingSetup("Unsaved", null, f.Now);
        NewSetupExample[] images = [new("first.png", Png, null), new(failDatabase ? "second.png" : "../invalid.png", Png, "1h")];
        if (failDatabase)
            await Assert.ThrowsAsync<DbUpdateException>(() => store.CreateWithExamplesAsync(setup, images, default));
        else
            await Assert.ThrowsAsync<ArgumentException>(() => store.CreateWithExamplesAsync(setup, images, default));
        await using var db = f.Context();
        Assert.Equal(f.Setup.Id, (await db.TradingSetups.SingleAsync()).Id);
        Assert.Equal(existing.Id, (await db.SetupExamples.SingleAsync()).Id);
        Assert.Equal(existing.File.StorageKey, Path.GetFileName(Assert.Single(Directory.GetFiles(f.Paths.ScreenshotsDirectory))));
    }

    [Fact]
    public async Task CancellationAfterFirstCopyRollsBackCreateAndReclaimsItsFile()
    {
        await using var f = await Fixture.Create();
        using var cancel = new CancellationTokenSource();
        var files = new CancelAfterCopy(f.Get<IPlaybookImageStorage>(), cancel);
        var store = new Infrastructure.Setups.SetupExampleStore(f.Get<IDbContextFactory<Infrastructure.Persistence.JournalDbContext>>(), files);
        var setup = new TradingSetup("Cancelled", null, f.Now);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.CreateWithExamplesAsync(setup,
            [new("first.png", Png, null), new("second.png", Png, null)], cancel.Token));
        Assert.Empty(Directory.GetFiles(f.Paths.ScreenshotsDirectory));
        await using var db = f.Context();
        Assert.False(await db.TradingSetups.AnyAsync(row => row.Id == setup.Id));
        Assert.Empty(await db.SetupExamples.ToListAsync());
    }

    private sealed class CancelAfterCopy(IPlaybookImageStorage files, CancellationTokenSource cancel) : IPlaybookImageStorage
    {
        public async Task<PlaybookExampleImage> StoreAsync(string name, ReadOnlyMemory<byte> bytes, CancellationToken token)
        {
            var image = await files.StoreAsync(name, bytes, token);
            cancel.Cancel();
            return image;
        }
        public Task<byte[]> ReadAsync(PlaybookExampleImage image, CancellationToken token) => files.ReadAsync(image, token);
        public Task DeleteUnreferencedAsync(string key, CancellationToken token) => files.DeleteUnreferencedAsync(key, token);
    }

    [Fact]
    public async Task FailedFileCopyOrForeignKeyInsertPublishesNeitherMetadataNorBytes()
    {
        await using var f = await Fixture.Create();
        var store = f.Get<ISetupExampleStore>();
        await Assert.ThrowsAsync<DbUpdateException>(() => store.AddAsync(Guid.NewGuid(), "sample.png", Png, null, default));
        Assert.Empty(Directory.GetFiles(f.Paths.ScreenshotsDirectory));
        Directory.Delete(f.Paths.ScreenshotsDirectory);
        await File.WriteAllTextAsync(f.Paths.ScreenshotsDirectory, "synthetic path obstruction");
        await Assert.ThrowsAsync<IOException>(() => store.AddAsync(f.Setup.Id, "sample.png", Png, null, default));
        Assert.Empty(await store.ListAsync(f.Setup.Id, default));
    }

    [Fact]
    public async Task LastSchemaWithoutImagesRestoresAndUpgradesWithAnEmptyGallery()
    {
        await using var old = await Fixture.Create("20261122212516_RetirePlaybook");
        ImmutableArray<string> migrations;
        await using (var db = old.Context())
        {
            migrations = [.. await db.Database.GetAppliedMigrationsAsync()];
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=DELETE");
        }
        await old.Stop();
        var bytes = await File.ReadAllBytesAsync(old.Paths.DatabasePath);
        string archive = Path.Combine(old.Root, "older.ptjbackup");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry(BackupArchiveContract.DatabasePath); entry.ExternalAttributes = 0;
            using (var stream = entry.Open()) stream.Write(bytes);
            var payload = new BackupFileEntry(entry.FullName, BackupFileKind.Database, bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)));
            var manifest = new BackupManifest(BackupArchiveContract.Format, 1, "1.0.0", old.Now, new("sqlite", migrations),
                BackupArchiveContract.HashAlgorithm, [payload]);
            var metadata = zip.CreateEntry(BackupArchiveContract.ManifestPath); metadata.ExternalAttributes = 0;
            using var output = metadata.Open(); JsonSerializer.Serialize(output, manifest, BackupArchiveContract.JsonOptions);
        }
        await using var target = await Fixture.Create();
        var check = await new RestorePreflightService().InspectAsync(archive, target.Root);
        Assert.Equal(BackupSchemaCompatibility.VerifiedBeforeSetupImages, check.Compatibility);
        await target.Stop();
        Assert.Equal(JournalRestoreStatus.Restored, (await new JournalRestoreService(target.Paths).RestoreAsync(new(archive, check.Summary!.ArchiveSha256))).Status);
        await target.Restart();
        await using var upgraded = target.Context();
        await upgraded.Database.MigrateAsync();
        Assert.Equal(old.Setup.Id, (await upgraded.TradingSetups.SingleAsync()).Id);
        Assert.Empty(await target.Get<ISetupExampleStore>().ListAsync(old.Setup.Id, default));
    }
}
