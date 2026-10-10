using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Backups;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Backups;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Initialization;
using PersonalTradingJournal.Infrastructure.Persistence.Records;
using Fixture = PersonalTradingJournal.Infrastructure.Tests.Backups.BackupArchiveTests.Fixture;

namespace PersonalTradingJournal.Infrastructure.Tests.Backups;

public sealed class JournalRestoreTests
{
    [Fact]
    public async Task SuccessfulRestorePreservesIncomingRelationshipsAndRetainsVerifiedRecoveryAndOriginalDirectory()
    {
        await using var source = await Fixture.Create(); await Seed(source, "incoming.png");
        await File.WriteAllTextAsync(source.Paths.SettingsPath, "{\"Theme\":\"Dark\"}");
        var request = await Archive(source);
        await using var target = await Fixture.Create(); await Seed(target, "old.png");
        await File.WriteAllTextAsync(target.Paths.SettingsPath, "{ \"Theme\" : \"Light\" }");
        Directory.CreateDirectory(Path.GetDirectoryName(target.Paths.CoachingCredentialsPath)!);
        await File.WriteAllBytesAsync(target.Paths.CoachingCredentialsPath, [9, 8, 7]); // Synthetic sentinel, not a credential.
        await File.WriteAllTextAsync(Path.Combine(target.Paths.DataDirectory, "orphan.txt"), "synthetic orphan");
        string old = await StopAndFingerprint(target), incoming = LogicalFingerprint(source.Paths.DatabasePath);
        var result = await new JournalRestoreService(target.Paths).RestoreAsync(request);
        Assert.True(result.Status == JournalRestoreStatus.Restored, result.ToString()); Assert.False(result.NormalUseBlocked);
        Assert.Equal(incoming, LogicalFingerprint(target.Paths.DatabasePath));
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(Path.Combine(target.Paths.ScreenshotsDirectory, "incoming.png")));
        Assert.False(File.Exists(Path.Combine(target.Paths.ScreenshotsDirectory, "old.png")));
        Assert.Equal(new byte[] { 9, 8, 7 }, await File.ReadAllBytesAsync(target.Paths.CoachingCredentialsPath));
        Assert.Contains("Dark", await File.ReadAllTextAsync(target.Paths.SettingsPath));
        Assert.Contains("Light", await File.ReadAllTextAsync(Path.Combine(result.RetainedDirectoryPath!, "original", "settings.json")));
        Assert.Equal("synthetic orphan", await File.ReadAllTextAsync(Path.Combine(result.RetainedDirectoryPath!, "original", "orphan.txt")));
        Assert.Equal(old, LogicalFingerprint(Path.Combine(result.RetainedDirectoryPath!, "original", "journal.db")));
        var recovery = await new RestorePreflightService().InspectAsync(result.RecoveryArchivePath!, target.Root);
        Assert.True(recovery.Validated); Assert.Equal(1, recovery.Summary!.TradeCount); Assert.Equal(2, recovery.Summary.JournalRevisionCount);
        Assert.Equal(1, recovery.Summary.AnalysisCount); Assert.False(File.Exists(JournalDataSession.StatePath(target.Paths)));
        Assert.DoesNotContain(target.Root, result.ToString()); Assert.DoesNotContain(source.Root, result.ToString());
        await using var restarted = new ServiceCollection().AddPersistence(target.Paths).BuildServiceProvider();
        await restarted.GetRequiredService<JournalDatabaseInitializer>().InitializeAsync();
        await using var context = await restarted.GetRequiredService<IDbContextFactory<JournalDbContext>>().CreateDbContextAsync();
        Assert.Equal(2, await context.TradeExecutions.CountAsync()); Assert.Equal(1, await context.CoachingAnalyses.CountAsync());
    }

    [Fact]
    public async Task ChangedArchiveAfterPreflightNeverChangesInstalledData()
    {
        await using var source = await Fixture.Create(); var request = await Archive(source);
        await using var target = await Fixture.Create(); await Seed(target, "old.png"); string old = await StopAndFingerprint(target);
        await File.AppendAllTextAsync(source.Output, "changed");
        var result = await new JournalRestoreService(target.Paths).RestoreAsync(request);
        Assert.Equal(JournalRestoreStatus.SourceChanged, result.Status); Assert.Null(result.RecoveryArchivePath);
        Assert.Equal(old, LogicalFingerprint(target.Paths.DatabasePath)); Assert.False(File.Exists(JournalDataSession.StatePath(target.Paths)));
    }

    [Theory]
    [InlineData("active-session")]
    [InlineData("raw-connection")]
    [InlineData("file-lock")]
    public async Task MaintenanceRequiresClosedSessionsAndDatabaseHandles(string mode)
    {
        await using var source = await Fixture.Create(); var request = await Archive(source);
        await using var target = await Fixture.Create(); await Seed(target, "old.png");
        if (mode != "active-session") await StopAndFingerprint(target);
        using var database = mode == "raw-connection" ? Open(target.Paths.DatabasePath) : null;
        using var file = mode == "file-lock" ? new FileStream(target.Paths.DatabasePath, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
        var result = await new JournalRestoreService(target.Paths).RestoreAsync(request);
        Assert.Equal(JournalRestoreStatus.Blocked, result.Status); Assert.Null(result.RecoveryArchivePath);
        Assert.False(File.Exists(JournalDataSession.StatePath(target.Paths)));
    }

    [Theory]
    [InlineData(JournalRestorePhase.Maintenance)]
    [InlineData(JournalRestorePhase.Incoming)]
    [InlineData(JournalRestorePhase.RecoveryCapture)]
    [InlineData(JournalRestorePhase.RecoveryVerified)]
    [InlineData(JournalRestorePhase.Prepared)]
    public async Task CancellationBeforeReplacementLeavesOriginalIntact(JournalRestorePhase at)
    {
        await using var source = await Fixture.Create(); var request = await Archive(source);
        await using var target = await Fixture.Create(); await Seed(target, "old.png"); string old = await StopAndFingerprint(target);
        using var cts = new CancellationTokenSource();
        var result = await new JournalRestoreService(target.Paths, (phase, _) => { if (phase == at) cts.Cancel(); }).RestoreAsync(request, cts.Token);
        Assert.Equal(JournalRestoreStatus.Cancelled, result.Status); Assert.Equal(old, LogicalFingerprint(target.Paths.DatabasePath));
        Assert.True(File.Exists(Path.Combine(target.Paths.ScreenshotsDirectory, "old.png"))); Assert.False(File.Exists(JournalDataSession.StatePath(target.Paths)));
    }

    [Theory]
    [InlineData(JournalRestorePhase.OriginalMoved)]
    [InlineData(JournalRestorePhase.IncomingInstalled)]
    [InlineData(JournalRestorePhase.Verified)]
    public async Task CancellationAfterReplacementStartsCannotAbandonMixedDataset(JournalRestorePhase at)
    {
        await using var source = await Fixture.Create(); await Seed(source, "incoming.png"); var request = await Archive(source);
        await using var target = await Fixture.Create(); await StopAndFingerprint(target);
        using var cts = new CancellationTokenSource();
        var result = await new JournalRestoreService(target.Paths, (phase, _) => { if (phase == at) cts.Cancel(); }).RestoreAsync(request, cts.Token);
        Assert.Equal(JournalRestoreStatus.Restored, result.Status); Assert.True(File.Exists(Path.Combine(target.Paths.ScreenshotsDirectory, "incoming.png")));
        Assert.False(File.Exists(JournalDataSession.StatePath(target.Paths)));
    }

    [Theory]
    [InlineData(JournalRestorePhase.Prepared)]
    [InlineData(JournalRestorePhase.OriginalMoved)]
    [InlineData(JournalRestorePhase.IncomingInstalled)]
    [InlineData(JournalRestorePhase.Verified)]
    public async Task FailureAtSwitchBoundariesRecoversOriginalAndKeepsRecovery(JournalRestorePhase at)
    {
        await using var source = await Fixture.Create(); var request = await Archive(source);
        await using var target = await Fixture.Create(); await Seed(target, "old.png"); string old = await StopAndFingerprint(target);
        var result = await new JournalRestoreService(target.Paths, (phase, _) => { if (phase == at) throw new IOException("synthetic sensitive diagnostic"); }).RestoreAsync(request);
        Assert.Equal(at == JournalRestorePhase.Prepared ? JournalRestoreStatus.FailedOriginalIntact : JournalRestoreStatus.RecoveredOriginal, result.Status);
        Assert.Equal(old, LogicalFingerprint(target.Paths.DatabasePath)); Assert.True(File.Exists(Path.Combine(target.Paths.ScreenshotsDirectory, "old.png")));
        Assert.True(File.Exists(result.RecoveryArchivePath)); Assert.False(File.Exists(JournalDataSession.StatePath(target.Paths)));
        Assert.DoesNotContain("sensitive", result.ToString());
    }

    [Theory]
    [InlineData(JournalRestorePhase.Prepared)]
    [InlineData(JournalRestorePhase.OriginalMoved)]
    [InlineData(JournalRestorePhase.IncomingInstalled)]
    [InlineData(JournalRestorePhase.Verified)]
    [InlineData(JournalRestorePhase.Committed)]
    public async Task InterruptedRestartBlocksNormalUseUntilRecoveryResolvesDurableState(JournalRestorePhase at)
    {
        await using var source = await Fixture.Create(); var request = await Archive(source);
        await using var target = await Fixture.Create(); await Seed(target, "old.png"); string old = await StopAndFingerprint(target);
        await Assert.ThrowsAsync<RestoreInterruption>(() => new JournalRestoreService(target.Paths,
            (phase, _) => { if (phase == at) throw new RestoreInterruption(); }).RestoreAsync(request));
        Assert.Throws<JournalMaintenanceException>(() => new JournalDataSession(target.Paths));
        await using (var restarted = new ServiceCollection().AddPersistence(target.Paths).BuildServiceProvider())
            Assert.Throws<JournalMaintenanceException>(() => restarted.GetRequiredService<IDbContextFactory<JournalDbContext>>());
        var recovered = await new JournalRestoreService(target.Paths).RecoverInterruptedAsync();
        Assert.Equal(at == JournalRestorePhase.Committed ? JournalRestoreStatus.Restored : JournalRestoreStatus.RecoveredOriginal, recovered.Status);
        if (at != JournalRestorePhase.Committed) Assert.Equal(old, LogicalFingerprint(target.Paths.DatabasePath));
        Assert.False(File.Exists(JournalDataSession.StatePath(target.Paths)));
        using var normal = new JournalDataSession(target.Paths);
        Assert.True(File.Exists(recovered.RecoveryArchivePath));
    }

    [Theory]
    [InlineData("database")]
    [InlineData("screenshot")]
    public async Task FailedPostReplacementVerificationRestoresOriginal(string corrupt)
    {
        await using var source = await Fixture.Create(); await Seed(source, "incoming.png"); var request = await Archive(source);
        await using var target = await Fixture.Create(); await Seed(target, "old.png"); string old = await StopAndFingerprint(target);
        var result = await new JournalRestoreService(target.Paths, (phase, _) =>
        {
            if (phase == JournalRestorePhase.IncomingInstalled)
                File.WriteAllBytes(corrupt == "database" ? target.Paths.DatabasePath : Path.Combine(target.Paths.ScreenshotsDirectory, "incoming.png"), [5]);
        }).RestoreAsync(request);
        Assert.Equal(JournalRestoreStatus.RecoveredOriginal, result.Status); Assert.Equal(old, LogicalFingerprint(target.Paths.DatabasePath));
        Assert.True(File.Exists(result.RecoveryArchivePath)); Assert.True(File.Exists(Path.Combine(result.RetainedDirectoryPath!, "failed", "journal.db")));
    }

    [Theory]
    [InlineData("unavailable")]
    [InlineData("missing-screenshot")]
    [InlineData("space")]
    public async Task RecoveryMustBeAvailableAndCompleteBeforeLiveReplacement(string defect)
    {
        await using var source = await Fixture.Create(); var request = await Archive(source);
        await using var target = await Fixture.Create(); await Seed(target, "old.png"); string old = await StopAndFingerprint(target);
        if (defect == "missing-screenshot") File.Delete(Path.Combine(target.Paths.ScreenshotsDirectory, "old.png"));
        var result = await new JournalRestoreService(target.Paths, (phase, root) =>
        {
            if (defect == "unavailable" && phase == JournalRestorePhase.RecoveryCapture) Directory.CreateDirectory(Path.Combine(root, "recovery.ptjbackup"));
        }, defect == "space" ? _ => 0 : null).RestoreAsync(request);
        Assert.Equal(defect == "space" ? JournalRestoreStatus.InsufficientStorage : JournalRestoreStatus.RecoveryCaptureFailed, result.Status);
        Assert.Equal(old, LogicalFingerprint(target.Paths.DatabasePath)); Assert.False(File.Exists(JournalDataSession.StatePath(target.Paths)));
    }

    [Fact]
    public async Task FailedRollbackRemainsDetectableAndCanBeResumedWithoutDeletingRecovery()
    {
        await using var source = await Fixture.Create(); var request = await Archive(source);
        await using var target = await Fixture.Create(); await Seed(target, "old.png"); string old = await StopAndFingerprint(target);
        var result = await new JournalRestoreService(target.Paths, (phase, _) =>
        { if (phase is JournalRestorePhase.IncomingInstalled or JournalRestorePhase.Rollback) throw new IOException(); }).RestoreAsync(request);
        Assert.Equal(JournalRestoreStatus.RecoveryRequired, result.Status); Assert.True(result.NormalUseBlocked);
        Assert.Throws<JournalMaintenanceException>(() => new JournalDataSession(target.Paths)); Assert.True(File.Exists(result.RecoveryArchivePath));
        var resumed = await new JournalRestoreService(target.Paths).RecoverInterruptedAsync();
        Assert.Equal(JournalRestoreStatus.RecoveredOriginal, resumed.Status); Assert.Equal(old, LogicalFingerprint(target.Paths.DatabasePath));
    }

    [Fact]
    public async Task MaintenanceBlocksNewApplicationSessionsAndOverlappingRestore()
    {
        await using var source = await Fixture.Create(); var request = await Archive(source);
        await using var target = await Fixture.Create(); await StopAndFingerprint(target);
        var result = await new JournalRestoreService(target.Paths, (phase, _) =>
        {
            if (phase != JournalRestorePhase.RecoveryCapture) return;
            Assert.Throws<JournalMaintenanceException>(() => new JournalDataSession(target.Paths));
            var overlap = new JournalRestoreService(target.Paths).RestoreAsync(request).GetAwaiter().GetResult();
            Assert.Equal(JournalRestoreStatus.Blocked, overlap.Status);
        }).RestoreAsync(request);
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(JournalRestorePhase.IncomingQuarantined)]
    [InlineData(JournalRestorePhase.OriginalRestored)]
    public async Task RestartDuringRollbackResumesWithoutLosingEitherDataset(JournalRestorePhase at)
    {
        await using var source = await Fixture.Create(); var request = await Archive(source);
        await using var target = await Fixture.Create(); await Seed(target, "old.png"); string old = await StopAndFingerprint(target);
        await Assert.ThrowsAsync<RestoreInterruption>(() => new JournalRestoreService(target.Paths, (phase, _) =>
        {
            if (phase == JournalRestorePhase.IncomingInstalled) throw new IOException();
            if (phase == at) throw new RestoreInterruption();
        }).RestoreAsync(request));
        Assert.Throws<JournalMaintenanceException>(() => new JournalDataSession(target.Paths));
        var result = await new JournalRestoreService(target.Paths).RecoverInterruptedAsync();
        Assert.Equal(JournalRestoreStatus.RecoveredOriginal, result.Status); Assert.Equal(old, LogicalFingerprint(target.Paths.DatabasePath));
        Assert.True(File.Exists(result.RecoveryArchivePath)); Assert.True(Directory.Exists(Path.Combine(result.RetainedDirectoryPath!, "failed")));
    }

    [Fact]
    public async Task CorruptionAfterCommitButBeforeStartupReleaseRecoversOriginal()
    {
        await using var source = await Fixture.Create(); var request = await Archive(source);
        await using var target = await Fixture.Create(); await Seed(target, "old.png"); string old = await StopAndFingerprint(target);
        await Assert.ThrowsAsync<RestoreInterruption>(() => new JournalRestoreService(target.Paths,
            (phase, _) => { if (phase == JournalRestorePhase.Committed) throw new RestoreInterruption(); }).RestoreAsync(request));
        await File.WriteAllBytesAsync(target.Paths.DatabasePath, [1, 2, 3]);
        var result = await new JournalRestoreService(target.Paths).RecoverInterruptedAsync();
        Assert.Equal(JournalRestoreStatus.RecoveredOriginal, result.Status); Assert.Equal(old, LogicalFingerprint(target.Paths.DatabasePath));
    }

    [Fact]
    public async Task DamagedRecoveryArchiveDoesNotAuthorizeRollbackOrStartup()
    {
        await using var source = await Fixture.Create(); var request = await Archive(source);
        await using var target = await Fixture.Create(); await Seed(target, "old.png"); string old = await StopAndFingerprint(target);
        string? root = null;
        await Assert.ThrowsAsync<RestoreInterruption>(() => new JournalRestoreService(target.Paths, (phase, path) =>
        { if (phase == JournalRestorePhase.OriginalMoved) { root = path; throw new RestoreInterruption(); } }).RestoreAsync(request));
        string archive = Path.Combine(root!, "recovery.ptjbackup"); byte[] originalArchive = await File.ReadAllBytesAsync(archive);
        await File.WriteAllBytesAsync(archive, [1]);
        var blocked = await new JournalRestoreService(target.Paths).RecoverInterruptedAsync();
        Assert.Equal(JournalRestoreStatus.RecoveryRequired, blocked.Status); Assert.False(Directory.Exists(target.Paths.DataDirectory));
        Assert.Throws<JournalMaintenanceException>(() => new JournalDataSession(target.Paths));
        Assert.Equal(old, LogicalFingerprint(Path.Combine(root!, "original", "journal.db")));
        await File.WriteAllBytesAsync(archive, originalArchive);
        Assert.Equal(JournalRestoreStatus.RecoveredOriginal, (await new JournalRestoreService(target.Paths).RecoverInterruptedAsync()).Status);
    }

    [Fact]
    public async Task DisposedHostCannotOpenALateRetainedContextDuringMaintenance()
    {
        await using var source = await Fixture.Create(); var request = await Archive(source);
        await using var target = await Fixture.Create(); await using var late = target.Context();
        _ = late.Database.GetDbConnection(); // Initialize EF services while the host is alive, without opening SQLite.
        await StopAndFingerprint(target);
        var result = await new JournalRestoreService(target.Paths, (phase, _) =>
        {
            if (phase == JournalRestorePhase.Incoming) Assert.Throws<JournalMaintenanceException>(() => late.Database.OpenConnection());
        }).RestoreAsync(request);
        Assert.True(result.Succeeded, result.ToString());
        await Assert.ThrowsAsync<JournalMaintenanceException>(() => late.Database.OpenConnectionAsync());
    }

    [Theory]
    [InlineData("invalid-json")]
    [InlineData("future-version")]
    [InlineData("too-large")]
    public async Task UnreadableDurableStateFailsClosedWithoutChangingInstalledDataset(string defect)
    {
        await using var target = await Fixture.Create(); await Seed(target, "old.png"); string old = await StopAndFingerprint(target);
        await File.WriteAllTextAsync(JournalDataSession.StatePath(target.Paths), defect == "too-large" ? new string('x', 4097) :
            defect == "invalid-json" ? "invalid" : "{\"version\":99}");
        var result = await new JournalRestoreService(target.Paths).RecoverInterruptedAsync();
        Assert.Equal(JournalRestoreStatus.RecoveryRequired, result.Status); Assert.Equal(old, LogicalFingerprint(target.Paths.DatabasePath));
        Assert.Throws<JournalMaintenanceException>(() => new JournalDataSession(target.Paths));
    }

    [Fact]
    public async Task RecoveryArchiveIncludesCommittedWalDataAndSqliteClosesItsOwnSidecars()
    {
        await using var source = await Fixture.Create(); var request = await Archive(source);
        await using var target = await Fixture.Create(); await Seed(target, "old.png"); await StopAndFingerprint(target);
        var captured = new Dictionary<string, byte[]>();
        using (var keeper = Open(target.Paths.DatabasePath))
        {
            using var command = keeper.CreateCommand();
            command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA wal_autocheckpoint=0; UPDATE DailyJournals SET Text='Synthetic committed WAL';"; command.ExecuteNonQuery();
            foreach (string suffix in new[] { "", "-wal", "-shm" })
            {
                using var file = new FileStream(target.Paths.DatabasePath + suffix, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var bytes = new MemoryStream(); file.CopyTo(bytes); captured.Add(suffix, bytes.ToArray());
            }
        }
        // Controlled synthetic crash fixture: one committed writer, no concurrent writes while capturing the complete trio.
        // Production restore never creates snapshots by copying these files.
        foreach (var item in captured) await File.WriteAllBytesAsync(target.Paths.DatabasePath + item.Key, item.Value);
        Assert.True(new FileInfo(target.Paths.DatabasePath + "-wal").Length > 0);
        var result = await new JournalRestoreService(target.Paths).RestoreAsync(request);
        Assert.True(result.Succeeded, result.ToString());
        Assert.False(File.Exists(target.Paths.DatabasePath + "-wal")); Assert.False(File.Exists(target.Paths.DatabasePath + "-shm"));
        using var original = Open(Path.Combine(result.RetainedDirectoryPath!, "original", "journal.db"));
        using var read = original.CreateCommand(); read.CommandText = "SELECT Text FROM DailyJournals;";
        Assert.Equal("Synthetic committed WAL", read.ExecuteScalar());
        var recovery = await new RestorePreflightService().InspectAsync(result.RecoveryArchivePath!, target.Root);
        Assert.True(recovery.Validated); Assert.Equal(1, recovery.Summary!.JournalCount);
    }

    [Fact]
    public async Task ChangesToOriginalArchiveAfterPinnedCaptureCannotChangeInstalledPayloads()
    {
        await using var source = await Fixture.Create(); await Seed(source, "incoming.png"); var request = await Archive(source);
        await using var target = await Fixture.Create(); await StopAndFingerprint(target);
        var result = await new JournalRestoreService(target.Paths, (phase, _) =>
        { if (phase == JournalRestorePhase.RecoveryCapture) File.WriteAllBytes(source.Output, [9]); }).RestoreAsync(request);
        Assert.True(result.Succeeded, result.ToString()); Assert.True(File.Exists(Path.Combine(target.Paths.ScreenshotsDirectory, "incoming.png")));
    }

    [Fact]
    public async Task RecoveryWithoutAnIntentDoesNothingAndCancelledRecoveryDoesNotClaimBlockedStartup()
    {
        await using var target = await Fixture.Create(); await StopAndFingerprint(target);
        var service = new JournalRestoreService(target.Paths);
        Assert.Equal(JournalRestoreStatus.NothingToRecover, (await service.RecoverInterruptedAsync()).Status);
        using var cts = new CancellationTokenSource(); cts.Cancel();
        var cancelled = await service.RecoverInterruptedAsync(cts.Token);
        Assert.Equal(JournalRestoreStatus.Cancelled, cancelled.Status); Assert.False(cancelled.NormalUseBlocked);
        using var normal = new JournalDataSession(target.Paths);
    }

    private static async Task Seed(Fixture f, string key)
    {
        await f.Add(key); var day = new DateOnly(2026, 10, 10); var now = new DateTimeOffset(2026, 10, 10, 16, 0, 0, TimeSpan.Zero);
        await using (var context = f.Context())
        {
            Guid trade = (await context.Trades.SingleAsync()).Id;
            context.TradeExecutions.AddRange(new TradeExecutionRecord { Id = Guid.NewGuid(), TradeId = trade, Sequence = 1,
                ExecutedAtUtc = now.AddMinutes(-1), Side = ExecutionSide.Buy, Quantity = 1, Price = 100, Commission = null, Fees = 0 },
                new TradeExecutionRecord { Id = Guid.NewGuid(), TradeId = trade, Sequence = 2, ExecutedAtUtc = now,
                    Side = ExecutionSide.Sell, Quantity = 1, Price = 110, Commission = 1, Fees = 0 });
            await context.SaveChangesAsync();
        }
        var journals = f.Services.GetRequiredService<IDailyJournalRepository>();
        var entry = (await journals.CreateAsync(new(day, null, "Synthetic " + key))).Journal!.Entry;
        await journals.UpdateAsync(new(entry.Id, entry.Revision, "Synthetic revision " + key));
        var packet = CoachingEvidencePacketBuilder.Build(await f.Services.GetRequiredService<IDailyReviewEvidenceReader>().GetAsync(new(day))).Packet!;
        var analysis = CoachingAnalysisSnapshot.Create(packet, new(CoachingGenerationStatus.Success,
            new(packet.ContractVersion, packet.PacketId, new("Synthetic summary", ["calculated:day"]), [], [], [], []),
            new("Fake", "test-model", "synthetic-client", null, null, new(1, 0, 1, 2)), "Unused"), now);
        await f.Services.GetRequiredService<ICoachingAnalysisRepository>().SaveAsync(analysis);
    }
    private static async Task<JournalRestoreRequest> Archive(Fixture f)
    {
        Assert.True((await f.Service().CreateAsync(f.Output)).Succeeded);
        var check = await new RestorePreflightService().InspectAsync(f.Output, f.Root); Assert.True(check.Validated);
        return new(f.Output, check.Summary!.ArchiveSha256);
    }
    private static async Task<string> StopAndFingerprint(Fixture f)
    {
        await f.Services.DisposeAsync(); JournalDataSession.ClearOwnedPools(f.Paths);
        return LogicalFingerprint(f.Paths.DatabasePath);
    }
    private static SqliteConnection Open(string file)
    { var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = file, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString()); db.Open(); return db; }
    private static string LogicalFingerprint(string file)
    {
        using var db = Open(file); using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string table in new[] { "TradingAccounts", "Instruments", "Trades", "TradeExecutions", "TradeScreenshots", "DailyJournals", "DailyJournalRevisions", "CoachingAnalyses" })
        {
            using var command = db.CreateCommand(); command.CommandText = "SELECT * FROM " + table + " ORDER BY 1,2;";
            using var rows = command.ExecuteReader(); while (rows.Read())
                for (int i = 0; i < rows.FieldCount; i++) digest.AppendData(System.Text.Encoding.UTF8.GetBytes(rows.GetValue(i).ToString() + "\0"));
        }
        return Convert.ToHexStringLower(digest.GetHashAndReset());
    }
}
