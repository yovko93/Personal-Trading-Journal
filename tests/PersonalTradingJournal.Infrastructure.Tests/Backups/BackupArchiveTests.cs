using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Backups;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Infrastructure.Backups;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Initialization;
using PersonalTradingJournal.Infrastructure.Persistence.Records;
using PersonalTradingJournal.Infrastructure.Screenshots;
using PersonalTradingJournal.Infrastructure.Storage;
using PersonalTradingJournal.Infrastructure.Tests.Persistence;

namespace PersonalTradingJournal.Infrastructure.Tests.Backups;

public sealed class BackupArchiveTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public async Task ArchiveContainsExactlySnapshotReferencesAndVerifiedBytesWithoutUnrelatedFiles(int count)
    {
        await using var f = await Fixture.Create();
        for (int i = 0; i < count; i++) await f.Add("image" + i + ".png");
        if (count > 0) await f.Add("image0.png"); // Shared storage key, two rows, one portable payload.
        await File.WriteAllTextAsync(Path.Combine(f.Paths.ScreenshotsDirectory, "orphan.png"), "untouched");
        await File.WriteAllTextAsync(f.Paths.SettingsPath, "{\"Theme\":\"Dark\",\"UnknownSecret\":\"never-package\"}");
        Directory.CreateDirectory(Path.GetDirectoryName(f.Paths.GroqCredentialsPath)!);
        await File.WriteAllTextAsync(f.Paths.GroqCredentialsPath, "synthetic-secret-excluded");
        await File.WriteAllTextAsync(Path.Combine(f.Paths.LogsDirectory, "private.log"), "excluded");

        var result = await f.Service().CreateAsync(f.Output);
        Assert.True(result.Succeeded, result.ToString()); Assert.False(result.CleanupFailed);
        Assert.Empty(f.Partials());
        using var zip = ZipFile.OpenRead(f.Output);
        var manifest = JsonSerializer.Deserialize<BackupManifest>(zip.GetEntry("manifest.json")!.Open(), BackupArchiveContract.JsonOptions)!;
        Assert.Equal(count + 2, manifest.Files.Length);
        Assert.Equal(count + 3, zip.Entries.Count);
        foreach (var item in manifest.Files)
        {
            using var bytes = zip.GetEntry(item.Path)!.Open();
            using var copy = new MemoryStream(); await bytes.CopyToAsync(copy);
            Assert.Equal(item.SizeBytes, copy.Length);
            Assert.Equal(item.Sha256, Convert.ToHexStringLower(SHA256.HashData(copy.ToArray())));
        }
        using (var prefs = new StreamReader(zip.GetEntry(BackupArchiveContract.PreferencesPath)!.Open()))
            Assert.Equal("{\"Theme\":\"Dark\"}", await prefs.ReadToEndAsync());
        string db = Path.Combine(f.Root, "inspect.db");
        zip.GetEntry(BackupArchiveContract.DatabasePath)!.ExtractToFile(db);
        using var connection = new SqliteConnection($"Data Source={db};Mode=ReadOnly;Pooling=False"); connection.Open();
        using var command = connection.CreateCommand(); command.CommandText = "SELECT DISTINCT StorageKey FROM TradeScreenshots ORDER BY StorageKey";
        using var rows = command.ExecuteReader(); var keys = new List<string>();
        while (rows.Read()) keys.Add(rows.GetString(0));
        Assert.Equal(keys.Select(k => BackupArchiveContract.ScreenshotsPrefix + k), manifest.Files.Where(x => x.Kind == BackupFileKind.Screenshot).Select(x => x.Path));
        Assert.Equal("untouched", await File.ReadAllTextAsync(Path.Combine(f.Paths.ScreenshotsDirectory, "orphan.png")));
        Assert.Equal("synthetic-secret-excluded", await File.ReadAllTextAsync(f.Paths.GroqCredentialsPath));
        Assert.DoesNotContain(f.Root, result.ToString());
    }

    [Theory]
    [InlineData("../escape.png")]
    [InlineData("C:/private.png")]
    [InlineData("folder/image.png")]
    [InlineData("CON.png")]
    [InlineData("image.PNG")]
    public async Task UnsafeSnapshotKeysAreRejectedWithoutFollowingThem(string key)
    {
        await using var f = await Fixture.Create(); await f.Add(key, false);
        var result = await f.Service().CreateAsync(f.Output);
        Assert.Equal(BackupValidationCode.UnsafePath, result.Failure); f.AssertNoOutput();
    }

    [Fact]
    public async Task CaseAliasesAreNotSilentlyMerged()
    {
        await using var f = await Fixture.Create(); await f.Add("image.png"); await f.Add("IMAGE.png", false);
        Assert.Equal(BackupValidationCode.DuplicateEntry, (await f.Service().CreateAsync(f.Output)).Failure); f.AssertNoOutput();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingFileOrExternalDeletionBlocksTheWholeArchive(bool deleteAfterSnapshot)
    {
        await using var f = await Fixture.Create(); await f.Add("image.png", deleteAfterSnapshot);
        var result = await f.Service((phase, _) =>
        { if (phase == BackupArchivePhase.Capture && deleteAfterSnapshot) File.Delete(Path.Combine(f.Paths.ScreenshotsDirectory, "image.png")); }).CreateAsync(f.Output);
        Assert.Equal(BackupValidationCode.DatabaseAttachmentMismatch, result.Failure); f.AssertNoOutput();
    }

    [Fact]
    public async Task UnreadableSourceFailsAndDoesNotTouchItsBytes()
    {
        await using var f = await Fixture.Create(); await f.Add("image.png");
        using var locked = new FileStream(Path.Combine(f.Paths.ScreenshotsDirectory, "image.png"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal(BackupValidationCode.IoFailure, (await f.Service().CreateAsync(f.Output)).Failure); f.AssertNoOutput();
    }

    [Fact]
    public async Task LinkedScreenshotRootIsRejectedWithoutPackagingOutsideBytes()
    {
        await using var f = await Fixture.Create(); await f.Add("image.png");
        string original = f.Paths.ScreenshotsDirectory; string elsewhere = Path.Combine(f.Root, "outside");
        Directory.Move(original, elsewhere);
        Directory.CreateSymbolicLink(original, elsewhere);
        try
        { Assert.False((await f.Service().CreateAsync(f.Output)).Succeeded); f.AssertNoOutput(); }
        finally { Directory.Delete(original); Directory.Move(elsewhere, original); }
    }

    [Fact]
    public async Task HardLinkedScreenshotCannotSmuggleAnOutsideFileIntoTheArchive()
    {
        await using var f = await Fixture.Create(); await f.Add("image.png", false);
        string outside = Path.Combine(f.Root, "outside.png"); await File.WriteAllBytesAsync(outside, [1, 2, 3]);
        Assert.True(CreateHardLink(Path.Combine(f.Paths.ScreenshotsDirectory, "image.png"), outside, IntPtr.Zero));
        Assert.Equal(BackupValidationCode.UnsafePath, (await f.Service().CreateAsync(f.Output)).Failure);
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(outside)); f.AssertNoOutput();
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string filename, string existingFilename, IntPtr attributes);

    [Fact]
    public async Task SnapshotRatherThanLaterLiveReferencesControlsInventory()
    {
        await using var f = await Fixture.Create(); await f.Add("original.png");
        var result = await f.Service((phase, _) =>
        {
            if (phase != BackupArchivePhase.Inventory) return;
            using var db = f.Context(); db.TradeScreenshots.RemoveRange(db.TradeScreenshots); db.SaveChanges();
            // New live metadata is intentionally not visible in the already captured DB.
            f.Add("later.png").GetAwaiter().GetResult();
        }).CreateAsync(f.Output);
        Assert.True(result.Succeeded, result.ToString());
        using var zip = ZipFile.OpenRead(f.Output);
        Assert.NotNull(zip.GetEntry(BackupArchiveContract.ScreenshotsPrefix + "original.png"));
        Assert.Null(zip.GetEntry(BackupArchiveContract.ScreenshotsPrefix + "later.png"));
    }

    [Fact]
    public async Task DeletionWaitsForCaptureAndCannotDamageAlreadyStagedArchive()
    {
        await using var f = await Fixture.Create(); await f.Add("image.png");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? delete = null;
        var result = await f.Service((phase, _) =>
        {
            if (phase == BackupArchivePhase.Capture)
            {
                delete = Task.Factory.StartNew(() =>
                {
                    entered.SetResult();
                    new LocalTradeScreenshotFileStorage(f.Paths).DeleteIfExistsAsync("image.png").GetAwaiter().GetResult();
                    release.SetResult();
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                entered.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                Assert.False(release.Task.IsCompleted);
            }
            if (phase == BackupArchivePhase.Package) release.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        }).CreateAsync(f.Output);
        await delete!;
        Assert.True(result.Succeeded, result.ToString());
        Assert.False(File.Exists(Path.Combine(f.Paths.ScreenshotsDirectory, "image.png")));
        using var zip = ZipFile.OpenRead(f.Output); Assert.NotNull(zip.GetEntry(BackupArchiveContract.ScreenshotsPrefix + "image.png"));
    }

    [Theory]
    [InlineData(BackupArchivePhase.Snapshot)]
    [InlineData(BackupArchivePhase.Inventory)]
    [InlineData(BackupArchivePhase.Capture)]
    [InlineData(BackupArchivePhase.Package)]
    [InlineData(BackupArchivePhase.Verify)]
    [InlineData(BackupArchivePhase.Publish)]
    public async Task CancellationNeverPublishesAndCleansOnlyOwnedStaging(BackupArchivePhase at)
    {
        await using var f = await Fixture.Create(); await f.Add("image.png"); using var cancellation = new CancellationTokenSource();
        var result = await f.Service((phase, _) => { if (phase == at) cancellation.Cancel(); }).CreateAsync(f.Output, cancellation.Token);
        Assert.Equal(BackupValidationCode.Cancelled, result.Failure); Assert.False(result.CleanupFailed); f.AssertNoOutput();
        Assert.True(File.Exists(Path.Combine(f.Paths.ScreenshotsDirectory, "image.png")));
    }

    [Theory]
    [InlineData("database")]
    [InlineData("screenshot")]
    [InlineData("payload")]
    [InlineData("archive")]
    [InlineData("files")]
    public async Task ActualResourceLimitsBlockPublication(string limit)
    {
        await using var f = await Fixture.Create(); await f.Add("image.png");
        var limits = limit switch
        { "database" => new ArchiveLimits(DatabaseBytes: 1), "screenshot" => new(ScreenshotBytes: 1), "payload" => new(PayloadBytes: 1), "archive" => new(ArchiveBytes: 1), _ => new(Files: 1) };
        Assert.Equal(BackupValidationCode.LimitExceeded, (await f.Service(limits: limits).CreateAsync(f.Output)).Failure); f.AssertNoOutput();
    }

    [Theory]
    [InlineData("payload")]
    [InlineData("database")]
    [InlineData("manifest")]
    [InlineData("duplicate")]
    [InlineData("missing")]
    [InlineData("link")]
    [InlineData("truncated")]
    public async Task IndependentVerificationRejectsCorruptContainer(string fault)
    {
        await using var f = await Fixture.Create(); await f.Add("image.png");
        var result = await f.Service((phase, root) =>
        {
            if (phase != BackupArchivePhase.Verify) return;
            string archive = Path.Combine(root, "archive.partial");
            if (fault == "truncated") { using var file = File.OpenWrite(archive); file.SetLength(16); return; }
            using var zip = ZipFile.Open(archive, ZipArchiveMode.Update);
            string name = fault == "manifest" ? "manifest.json" : fault == "database" ? BackupArchiveContract.DatabasePath : BackupArchiveContract.ScreenshotsPrefix + "image.png";
            var entry = zip.GetEntry(name)!;
            if (fault == "link") { entry.ExternalAttributes = unchecked((int)0xA0000000); return; }
            if (fault != "duplicate") entry.Delete();
            if (fault == "missing") return;
            var added = zip.CreateEntry(name); added.ExternalAttributes = 0;
            using var write = added.Open(); write.Write(Encoding.UTF8.GetBytes("corrupt"));
        }).CreateAsync(f.Output);
        Assert.Equal(fault switch
        {
            "link" => BackupValidationCode.UnsafePath,
            "duplicate" => BackupValidationCode.DuplicateEntry,
            "missing" => BackupValidationCode.MissingEntry,
            "truncated" => BackupValidationCode.IncompleteArchive,
            _ => BackupValidationCode.ContentMismatch
        }, result.Failure);
        Assert.Equal(BackupArchivePhase.Verify, result.Phase); Assert.False(result.CleanupFailed); f.AssertNoOutput();
    }

    [Fact]
    public async Task ChangedStagedScreenshotFailsRatherThanHashingSubstituteBytes()
    {
        await using var f = await Fixture.Create(); await f.Add("image.png");
        var result = await f.Service((phase, root) =>
        { if (phase == BackupArchivePhase.Package) File.WriteAllBytes(Directory.GetFiles(root, "attachment-*.bin").Single(), [9, 9, 9]); }).CreateAsync(f.Output);
        Assert.Equal(BackupValidationCode.SourceChanged, result.Failure); f.AssertNoOutput();
    }

    [Fact]
    public async Task ExistingDestinationIsNeverReplacedEvenWhenCreatedAtPublication()
    {
        await using var f = await Fixture.Create();
        var result = await f.Service((phase, _) =>
        { if (phase == BackupArchivePhase.Publish) File.WriteAllText(f.Output, "existing"); }).CreateAsync(f.Output);
        Assert.False(result.Succeeded); Assert.Equal("existing", await File.ReadAllTextAsync(f.Output)); Assert.Empty(f.Partials());
        Assert.Equal(BackupValidationCode.DuplicateEntry, (await f.Service().CreateAsync(f.Output)).Failure);
    }

    [Fact]
    public async Task CleanupFailureIsExplicitAndDoesNotPublishAnArchive()
    {
        await using var f = await Fixture.Create(); FileStream? locked = null;
        try
        {
            var result = await f.Service((phase, root) =>
            {
                if (phase != BackupArchivePhase.Verify) return;
                locked = new FileStream(Path.Combine(root, "archive.partial"), FileMode.Open, FileAccess.Read, FileShare.Read);
                throw new IOException("synthetic sensitive path must not appear");
            }).CreateAsync(f.Output);
            Assert.False(result.Succeeded); Assert.True(result.CleanupFailed); Assert.False(File.Exists(f.Output)); Assert.Single(f.Partials());
            Assert.DoesNotContain("sensitive", result.ToString());
        }
        finally { locked?.Dispose(); }
    }

    [Fact]
    public async Task UnexpectedStagingFileIsNeverClaimedOrDeletedByCleanup()
    {
        await using var f = await Fixture.Create(); string? unexpected = null;
        var result = await f.Service((phase, root) =>
        {
            if (phase != BackupArchivePhase.Package) return;
            unexpected = Path.Combine(root, "archive.partial"); File.WriteAllText(unexpected, "do not delete");
        }).CreateAsync(f.Output);
        Assert.False(result.Succeeded); Assert.True(result.CleanupFailed); Assert.False(File.Exists(f.Output));
        Assert.Equal("do not delete", await File.ReadAllTextAsync(unexpected!));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"Theme\":\"Invalid\"}")]
    [InlineData("{\"Theme\":\"Dark\",\"theme\":\"Light\"}")]
    public async Task InvalidPreferencesAreNotQuietlyOmitted(string settings)
    {
        await using var f = await Fixture.Create(); await File.WriteAllTextAsync(f.Paths.SettingsPath, settings);
        Assert.Equal(BackupValidationCode.InvalidManifest, (await f.Service().CreateAsync(f.Output)).Failure); f.AssertNoOutput();
    }

    [Fact]
    public async Task PrivateStagingHasCurrentUserOnlyProtectedWindowsAcl()
    {
        await using var f = await Fixture.Create();
        var result = await f.Service((phase, root) =>
        {
            if (phase != BackupArchivePhase.Snapshot || !OperatingSystem.IsWindows()) return;
            using var identity = WindowsIdentity.GetCurrent();
            var security = new DirectoryInfo(root).GetAccessControl();
            Assert.True(security.AreAccessRulesProtected);
            var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToArray();
            Assert.NotEmpty(rules);
            foreach (var rule in rules) Assert.Equal(identity.User, rule.IdentityReference);
        }).CreateAsync(f.Output);
        Assert.True(result.Succeeded, result.ToString());
    }

    [Fact]
    public async Task StableReadHandleRejectsConcurrentReplacementAndDeletion()
    {
        await using var f = await Fixture.Create(); await f.Add("image.png");
        var files = new LocalTradeScreenshotFileStorage(f.Paths);
        using (var read = await files.OpenReadAsync("image.png"))
        {
            Assert.Throws<IOException>(() => File.WriteAllBytes(Path.Combine(f.Paths.ScreenshotsDirectory, "image.png"), [4]));
            await Assert.ThrowsAsync<IOException>(() => files.DeleteIfExistsAsync("image.png"));
            Assert.Equal(1, read!.ReadByte());
        }
        Assert.True((await f.Service().CreateAsync(f.Output)).Succeeded);
    }

    [Fact]
    public async Task BusyCaptureIsBoundedAndCancellationDoesNotLeakTheMutex()
    {
        await using var f = await Fixture.Create();
        var acquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holder = Task.Factory.StartNew(() =>
        {
            using var lease = ScreenshotCaptureLease.Acquire(f.Paths.ScreenshotsDirectory, CancellationToken.None);
            acquired.SetResult(); release.Task.WaitAsync(TimeSpan.FromSeconds(20)).GetAwaiter().GetResult();
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try
        {
            await acquired.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var result = await f.Service().CreateAsync(f.Output).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(BackupValidationCode.IoFailure, result.Failure); Assert.Equal(BackupArchivePhase.Snapshot, result.Phase); f.AssertNoOutput();
            Assert.True(result.CaptureBusy);
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            Assert.Equal(BackupValidationCode.Cancelled, (await f.Service().CreateAsync(f.Output, cancelled.Token)).Failure); f.AssertNoOutput();
        }
        finally { release.SetResult(); await holder; }
        Assert.True((await f.Service().CreateAsync(f.Output)).Succeeded);
    }

    [Fact]
    public async Task InvalidSnapshotNeverEntersPackaging()
    {
        await using var f = await Fixture.Create();
        await using (var db = f.Context()) await db.Database.ExecuteSqlRawAsync("DROP TABLE CoachingAnalyses;");
        var result = await f.Service().CreateAsync(f.Output);
        Assert.Equal(BackupValidationCode.DatabaseIntegrityFailed, result.Failure);
        Assert.Equal(DatabaseSnapshotStatus.IncompatibleSchema, result.SnapshotFailure); f.AssertNoOutput();
    }

    [Fact]
    public async Task MultiBufferPayloadRetainsEveryByteAndDoesNotLoadOtherLiveFiles()
    {
        await using var f = await Fixture.Create(); await f.Add("image.png");
        byte[] expected = new byte[1024 * 1024 + 17]; new Random(314159).NextBytes(expected);
        await File.WriteAllBytesAsync(Path.Combine(f.Paths.ScreenshotsDirectory, "image.png"), expected);
        Assert.True((await f.Service().CreateAsync(f.Output)).Succeeded);
        using var zip = ZipFile.OpenRead(f.Output); using var input = zip.GetEntry(BackupArchiveContract.ScreenshotsPrefix + "image.png")!.Open();
        Assert.Equal(SHA256.HashData(expected), await SHA256.HashDataAsync(input));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "ptj-archive-test-" + Guid.NewGuid().ToString("N"));
        internal LocalApplicationPaths Paths { get; }
        internal ServiceProvider Services { get; }
        internal string Output => Path.Combine(Root, "result.ptjbackup");
        private Fixture() { Paths = new(Root); Paths.EnsureDirectoriesExist(); Services = new ServiceCollection().AddPersistence(Paths).BuildServiceProvider(); }
        internal static async Task<Fixture> Create()
        {
            var f = new Fixture();
            try { await f.Services.GetRequiredService<JournalDatabaseInitializer>().InitializeAsync(); return f; }
            catch { await f.DisposeAsync(); throw; }
        }
        internal JournalDbContext Context() => Services.GetRequiredService<IDbContextFactory<JournalDbContext>>().CreateDbContext();
        internal BackupArchiveService Service(Action<BackupArchivePhase, string>? hook = null, ArchiveLimits? limits = null) =>
            new(Paths, Services.GetRequiredService<IDatabaseSnapshotService>(), hook, limits ?? new());
        internal string[] Partials() => Directory.GetDirectories(Root, ".ptjbackup-*.partial");
        internal void AssertNoOutput() { Assert.False(File.Exists(Output)); Assert.Empty(Partials()); }
        internal async Task Add(string key, bool bytes = true)
        {
            var now = DateTimeOffset.UtcNow; Guid account = Guid.NewGuid(), instrument = Guid.NewGuid(), trade = Guid.NewGuid();
            await using var db = Context();
            db.TradingAccounts.Add(new TradingAccountRecord { Id = account, Name = "Synthetic", AccountType = TradingAccountType.Demo, Currency = "USD", IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now });
            db.Instruments.Add(new InstrumentRecord { Id = instrument, Symbol = "SYN" + instrument.ToString("N"), DisplayName = "Synthetic", AssetClass = AssetClass.Futures, Exchange = "TEST", Currency = "USD", TickSize = 1, TickValue = 1, IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now });
            db.Trades.Add(new TradeRecord { Id = trade, TradingAccountId = account, InstrumentId = instrument, PricingPointValue = 1, PricingCurrency = "USD", CreatedAtUtc = now, UpdatedAtUtc = now });
            db.TradeScreenshots.Add(new TradeScreenshotRecord { Id = Guid.NewGuid(), TradeId = trade, StorageKey = key, FileName = "label-not-a-path.png", CreatedAtUtc = now, UpdatedAtUtc = now });
            await db.SaveChangesAsync();
            if (bytes) await File.WriteAllBytesAsync(Path.Combine(Paths.ScreenshotsDirectory, key), [1, 2, 3]);
        }
        public async ValueTask DisposeAsync()
        {
            await Services.DisposeAsync(); SqliteTestPoolCleanup.ClearPersistencePools(Paths.DatabasePath);
            Directory.Delete(Root, true);
        }
    }
}
