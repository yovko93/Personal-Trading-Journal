using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using PersonalTradingJournal.Application.Backups;
using PersonalTradingJournal.Application.Common.Storage;

namespace PersonalTradingJournal.Infrastructure.Backups;

/// <summary>Offline, recovery-first directory switch. Never describes a multi-file restore as atomic.</summary>
public sealed class JournalRestoreService : IJournalRestoreService
{
    private readonly IApplicationPaths paths;
    private readonly Action<JournalRestorePhase, string>? checkpoint;
    private readonly Func<string, long> freeSpace;
    public JournalRestoreService(IApplicationPaths paths) : this(paths, null, null) { }
    internal JournalRestoreService(IApplicationPaths paths, Action<JournalRestorePhase, string>? checkpoint, Func<string, long>? freeSpace = null)
    { this.paths = paths; this.checkpoint = checkpoint; this.freeSpace = freeSpace ?? AvailableSpace; }

    public Task<JournalRestoreResult> RestoreAsync(JournalRestoreRequest request, CancellationToken cancellationToken = default) =>
        Task.Run(() => Restore(request, cancellationToken), CancellationToken.None);
    public Task<JournalRestoreResult> RecoverInterruptedAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Recover(cancellationToken), CancellationToken.None);

    private JournalRestoreResult Restore(JournalRestoreRequest request, CancellationToken token)
    {
        Guid id = Guid.NewGuid(); var phase = JournalRestorePhase.Maintenance;
        string? root = null, recovery = null; FileStream? lease = null; bool started = false, intent = false;
        RestoreState? state = null;
        void Check(JournalRestorePhase next, bool cancellable = true)
        { phase = next; if (cancellable) token.ThrowIfCancellationRequested(); checkpoint?.Invoke(next, root ?? ""); if (cancellable) token.ThrowIfCancellationRequested(); }
        JournalRestoreResult Result(JournalRestoreStatus status) => new(status, id, phase, recovery, root);
        try
        {
            Check(JournalRestorePhase.Maintenance);
            ValidateLayout(); lease = JournalDataSession.OpenLease(paths, exclusive: true);
            if (Path.Exists(JournalDataSession.StatePath(paths))) return Result(JournalRestoreStatus.RecoveryRequired);
            JournalDataSession.ClearOwnedPools(paths);
            AssertClosed(paths.DatabasePath);
            if (!IsHash(request.ConfirmedArchiveSha256)) return Result(JournalRestoreStatus.RejectedArchive);
            string work = JournalDataSession.WorkRoot(paths);
            SqliteDatabaseSnapshotService.EnsureNoLinks(work);
            BackupArchiveService.CreatePrivateDirectory(work);
            root = Path.Combine(work, id.ToString("N"));
            if (Path.Exists(root)) throw new IOException();
            BackupArchiveService.CreatePrivateDirectory(root);
            string incomingArchive = Path.Combine(root, "incoming.ptjbackup"), incomingDirectory = Path.Combine(root, "incoming");
            Check(JournalRestorePhase.Incoming);
            RequireSpace(new FileInfo(request.ArchivePath).Length * 2 + 64L * 1024 * 1024);
            if (CopyPinned(request.ArchivePath, incomingArchive, BackupArchiveContract.MaximumArchiveBytes, token) != request.ConfirmedArchiveSha256)
                return Result(JournalRestoreStatus.SourceChanged);
            BackupManifest? incomingManifest = null; bool stagingSpaceUnavailable = false;
            var incoming = Inspect(incomingArchive, root, (manifest, staged, summary) =>
            {
                try { RequireSpace(checked(summary.PayloadBytes * 2 + 64L * 1024 * 1024)); }
                catch (StorageFailure) { stagingSpaceUnavailable = true; throw; }
                incomingManifest = manifest; Stage(manifest, staged, incomingDirectory, token);
            }, token);
            if (!incoming.Validated || incoming.CleanupFailed)
                return stagingSpaceUnavailable ? Result(JournalRestoreStatus.InsufficientStorage) :
                    incoming.Failure == BackupValidationCode.Cancelled ? Result(JournalRestoreStatus.Cancelled) :
                    Result(JournalRestoreStatus.RejectedArchive) with { CleanupFailed = incoming.CleanupFailed, ValidationFailure = incoming.Failure };

            Check(JournalRestorePhase.RecoveryCapture);
            AssertClosed(paths.DatabasePath);
            RequireSpace(checked(new FileInfo(paths.DatabasePath).Length * 3 + 64L * 1024 * 1024));
            recovery = Path.Combine(root, "recovery.ptjbackup");
            var captured = new BackupArchiveService(paths, new SqliteDatabaseSnapshotService(paths)).CreateAsync(recovery, token).GetAwaiter().GetResult();
            if (!captured.Succeeded || captured.CleanupFailed)
                return Result(captured.Failure == BackupValidationCode.Cancelled ? JournalRestoreStatus.Cancelled : JournalRestoreStatus.RecoveryCaptureFailed)
                    with { RecoveryArchivePath = File.Exists(recovery) ? recovery : null, CleanupFailed = captured.CleanupFailed,
                        ValidationFailure = captured.Failure, SnapshotFailure = captured.SnapshotFailure };
            BackupManifest? recoveryManifest = null;
            var recoveryCheck = Inspect(recovery, root, (manifest, _, _) => recoveryManifest = manifest, token);
            if (!recoveryCheck.Validated || recoveryCheck.CleanupFailed)
                return Result(recoveryCheck.Failure == BackupValidationCode.Cancelled ? JournalRestoreStatus.Cancelled : JournalRestoreStatus.RecoveryCaptureFailed)
                    with { CleanupFailed = recoveryCheck.CleanupFailed, ValidationFailure = recoveryCheck.Failure };
            Check(JournalRestorePhase.RecoveryVerified);

            // Only SQLite handles/checkpoints its WAL/SHM/journal. Never unlink sidecars ourselves.
            ShutdownSqlite();
            var originalDatabase = Inventory(paths.DatabasePath, BackupArchiveContract.DatabasePath, BackupFileKind.Database, token);
            var originalPreferences = recoveryManifest!.Files.Any(p => p.Kind == BackupFileKind.Preferences)
                ? Inventory(paths.SettingsPath, BackupArchiveContract.PreferencesPath, BackupFileKind.Preferences, token) : null;
            var physicalOriginal = OriginalManifest(recoveryManifest, originalDatabase, originalPreferences);
            VerifyDirectory(paths.DataDirectory, physicalOriginal, recoveryCheck.Summary!, token);
            VerifyDirectory(incomingDirectory, incomingManifest!, incoming.Summary!, token);
            state = new(1, id, false, incoming.Summary!.ArchiveSha256, recoveryCheck.Summary!.ArchiveSha256, originalDatabase, originalPreferences);
            intent = true; WriteState(state);
            Check(JournalRestorePhase.Prepared);
            // Cancellation linearization point: after this check, finish verification or recover, never abandon a mixed set.
            token.ThrowIfCancellationRequested(); started = true;
            Directory.Move(paths.DataDirectory, Path.Combine(root, "original"));
            Check(JournalRestorePhase.OriginalMoved, false);
            Directory.Move(incomingDirectory, paths.DataDirectory);
            Check(JournalRestorePhase.IncomingInstalled, false);
            VerifyDirectory(paths.DataDirectory, incomingManifest!, incoming.Summary!, CancellationToken.None);
            Check(JournalRestorePhase.Verified, false);
            var committed = state with { Committed = true };
            WriteState(committed); state = committed;
            Check(JournalRestorePhase.Committed, false);
            ClearState(); intent = false;
            phase = JournalRestorePhase.Finished;
            return Result(JournalRestoreStatus.Restored);
        }
        catch (RestoreInterruption) { throw; } // Internal deterministic process-loss simulation; no production caller can install it.
        catch (Exception e)
        {
            if (intent && state is not null)
            {
                try
                {
                    if (started) return RecoverCore(state, CancellationToken.None);
                    ClearState();
                }
                catch (RestoreInterruption) { throw; }
                catch (Exception) { return Result(JournalRestoreStatus.RecoveryRequired); }
            }
            return Result(e switch
            {
                OperationCanceledException => JournalRestoreStatus.Cancelled,
                JournalMaintenanceException => JournalRestoreStatus.Blocked,
                StorageFailure => JournalRestoreStatus.InsufficientStorage,
                _ when phase == JournalRestorePhase.Maintenance => JournalRestoreStatus.Blocked,
                _ when phase == JournalRestorePhase.RecoveryCapture => JournalRestoreStatus.RecoveryCaptureFailed,
                _ => JournalRestoreStatus.FailedOriginalIntact
            });
        }
        finally { lease?.Dispose(); }
    }

    private JournalRestoreResult Recover(CancellationToken token)
    {
        RestoreState? state = null;
        try
        {
            token.ThrowIfCancellationRequested(); ValidateLayout();
            using var lease = JournalDataSession.OpenLease(paths, exclusive: true);
            if (!Path.Exists(JournalDataSession.StatePath(paths))) return new(JournalRestoreStatus.NothingToRecover, Guid.Empty, JournalRestorePhase.Maintenance);
            state = ReadState();
            JournalDataSession.ClearOwnedPools(paths);
            return RecoverCore(state, token);
        }
        catch (RestoreInterruption) { throw; }
        catch (Exception e)
        {
            return new(e is JournalMaintenanceException ? JournalRestoreStatus.Blocked :
                e is OperationCanceledException && !Path.Exists(JournalDataSession.StatePath(paths)) ? JournalRestoreStatus.Cancelled : JournalRestoreStatus.RecoveryRequired,
                state?.OperationId ?? Guid.Empty, JournalRestorePhase.Rollback,
                state is null ? null : Path.Combine(OperationRoot(state), "recovery.ptjbackup"), state is null ? null : OperationRoot(state));
        }
    }

    private JournalRestoreResult RecoverCore(RestoreState state, CancellationToken token)
    {
        string root = OperationRoot(state), recoveryPath = Path.Combine(root, "recovery.ptjbackup");
        SqliteDatabaseSnapshotService.EnsureNoLinks(root);
        // Once committed, restart finishes only after verifying the same incoming archive and entire installed set.
        if (state.Committed)
        {
            try
            {
                BackupManifest? incomingManifest = null;
                var check = Inspect(Path.Combine(root, "incoming.ptjbackup"), root, (m, _, _) => incomingManifest = m, token);
                if (!check.Validated || check.CleanupFailed || check.Summary!.ArchiveSha256 != state.IncomingHash) throw new IOException();
                VerifyDirectory(paths.DataDirectory, incomingManifest!, check.Summary, token);
                ClearState();
                return new(JournalRestoreStatus.Restored, state.OperationId, JournalRestorePhase.Finished, recoveryPath, root);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                token.ThrowIfCancellationRequested();
                // The app has remained blocked since commit; no later user work can be discarded by this fallback.
                state = state with { Committed = false }; WriteState(state);
            }
        }
        BackupManifest? manifest = null;
        var recovery = Inspect(recoveryPath, root, (m, _, _) => manifest = m, token);
        if (!recovery.Validated || recovery.CleanupFailed || recovery.Summary!.ArchiveSha256 != state.RecoveryHash) throw new IOException();
        manifest = OriginalManifest(manifest!, state.OriginalDatabase, state.OriginalPreferences);
        string original = Path.Combine(root, "original"), failed = Path.Combine(root, "failed");
        string source = Directory.Exists(original) ? original : paths.DataDirectory;
        AssertClosed(Path.Combine(source, "journal.db"));
        VerifyDirectory(source, manifest, recovery.Summary, token);
        token.ThrowIfCancellationRequested();
        checkpoint?.Invoke(JournalRestorePhase.Rollback, root);
        if (Directory.Exists(original))
        {
            if (Directory.Exists(paths.DataDirectory))
            {
                Directory.Move(paths.DataDirectory, failed);
                checkpoint?.Invoke(JournalRestorePhase.IncomingQuarantined, root);
            }
            Directory.Move(original, paths.DataDirectory);
        }
        checkpoint?.Invoke(JournalRestorePhase.OriginalRestored, root);
        VerifyDirectory(paths.DataDirectory, manifest, recovery.Summary, CancellationToken.None);
        ClearState();
        return new(JournalRestoreStatus.RecoveredOriginal, state.OperationId, JournalRestorePhase.Finished, recoveryPath, root);
    }

    private static RestorePreflightResult Inspect(string archive, string parent,
        Action<BackupManifest, IReadOnlyDictionary<string, string>, RestorePreflightSummary> capture, CancellationToken token) =>
        new RestorePreflightService(null, new(), capture).InspectAsync(archive, parent, token).GetAwaiter().GetResult();

    private static void Stage(BackupManifest manifest, IReadOnlyDictionary<string, string> staged, string directory, CancellationToken token)
    {
        BackupArchiveService.CreatePrivateDirectory(directory);
        BackupArchiveService.CreatePrivateDirectory(Path.Combine(directory, "screenshots"));
        foreach (var entry in manifest.Files)
        {
            string target = PayloadPath(directory, entry);
            if (CopyPinned(staged[entry.Path], target, entry.SizeBytes, token) != entry.Sha256 || new FileInfo(target).Length != entry.SizeBytes)
                throw new IOException();
        }
    }
    private static void VerifyDirectory(string directory, BackupManifest manifest, RestorePreflightSummary expected, CancellationToken token)
    {
        SqliteDatabaseSnapshotService.EnsureNoLinks(directory);
        var staged = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in manifest.Files)
        {
            string path = PayloadPath(directory, entry);
            var actual = Inventory(path, entry.Path, entry.Kind, token);
            if (actual != entry) throw new IOException();
            staged.Add(entry.Path, path);
        }
        var actualCounts = new RestorePreflightService().VerifyStaged(manifest, staged, expected.ArchiveBytes,
            expected.ArchiveSha256, _ => token.ThrowIfCancellationRequested(), token);
        if (actualCounts.AccountCount != expected.AccountCount || actualCounts.TradeCount != expected.TradeCount ||
            actualCounts.JournalCount != expected.JournalCount || actualCounts.JournalRevisionCount != expected.JournalRevisionCount ||
            actualCounts.AnalysisCount != expected.AnalysisCount || actualCounts.ScreenshotCount != expected.ScreenshotCount ||
            actualCounts.ScreenshotFileCount != expected.ScreenshotFileCount) throw new IOException();
    }
    private static string PayloadPath(string directory, BackupFileEntry entry) => entry.Kind switch
    {
        BackupFileKind.Database when entry.Path == BackupArchiveContract.DatabasePath => Path.Combine(directory, "journal.db"),
        BackupFileKind.Preferences when entry.Path == BackupArchiveContract.PreferencesPath => Path.Combine(directory, "settings.json"),
        BackupFileKind.Screenshot when entry.Path.StartsWith(BackupArchiveContract.ScreenshotsPrefix, StringComparison.Ordinal) &&
            BackupManifestValidator.IsPortableScreenshotKey(entry.Path[BackupArchiveContract.ScreenshotsPrefix.Length..]) =>
                Path.Combine(directory, "screenshots", entry.Path[BackupArchiveContract.ScreenshotsPrefix.Length..]),
        _ => throw new IOException()
    };
    private static BackupManifest OriginalManifest(BackupManifest manifest, BackupFileEntry database, BackupFileEntry? preferences) =>
        manifest with { Files = manifest.Files.Select(f => f.Kind == BackupFileKind.Database ? database :
            f.Kind == BackupFileKind.Preferences ? preferences ?? throw new IOException() : f).ToImmutableArray() };

    private void ShutdownSqlite()
    {
        AssertClosed(paths.DatabasePath);
        using (var database = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = paths.DatabasePath, Mode = SqliteOpenMode.ReadWrite, Pooling = false, DefaultTimeout = 2 }.ToString()))
        {
            database.Open();
            using (var command = database.CreateCommand())
            {
                command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                using var rows = command.ExecuteReader();
                if (!rows.Read() || rows.GetInt32(0) != 0) throw new JournalMaintenanceException("SQLite shutdown is blocked.");
            }
            using (var command = database.CreateCommand())
            { command.CommandText = "PRAGMA journal_mode=DELETE;"; if (!string.Equals(command.ExecuteScalar()?.ToString(), "delete", StringComparison.OrdinalIgnoreCase)) throw new IOException(); }
        }
        AssertClosed(paths.DatabasePath);
        if (new[] { "-wal", "-shm", "-journal" }.Any(s => File.Exists(paths.DatabasePath + s))) throw new IOException();
    }
    private static void AssertClosed(string database)
    {
        SqliteDatabaseSnapshotService.EnsureNoLinks(database);
        using var probe = new FileStream(database, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        if (!BackupFileIdentity.IsUnaliased(probe, database)) throw new IOException();
        foreach (string suffix in new[] { "-wal", "-shm", "-journal" })
        {
            string sidecar = database + suffix;
            SqliteDatabaseSnapshotService.EnsureNoLinks(sidecar);
            if (!File.Exists(sidecar)) continue;
            using var sidecarProbe = new FileStream(sidecar, FileMode.Open, FileAccess.Read, FileShare.None);
            if (!BackupFileIdentity.IsUnaliased(sidecarProbe, sidecar)) throw new IOException();
        }
    }
    private static BackupFileEntry Inventory(string path, string portable, BackupFileKind kind, CancellationToken token)
    {
        SqliteDatabaseSnapshotService.EnsureNoLinks(path);
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (!BackupFileIdentity.IsUnaliased(file, path)) throw new IOException();
        return new(portable, kind, file.Length, Transfer(file, Stream.Null, BackupArchiveContract.MaximumPayloadBytes, token));
    }
    private static string CopyPinned(string source, string target, long maximum, CancellationToken token)
    {
        SqliteDatabaseSnapshotService.EnsureNoLinks(source);
        using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (!BackupFileIdentity.IsUnaliased(input, Path.GetFullPath(source))) throw new IOException();
        using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.WriteThrough);
        string hash = Transfer(input, output, maximum, token); output.Flush(true); return hash;
    }
    private static string Transfer(Stream input, Stream output, long maximum, CancellationToken token)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); byte[] buffer = new byte[81920]; long length = 0; int read;
        while ((read = input.Read(buffer)) != 0)
        {
            token.ThrowIfCancellationRequested(); if (read > maximum - length) throw new IOException(); length += read;
            hash.AppendData(buffer, 0, read); output.Write(buffer, 0, read);
        }
        token.ThrowIfCancellationRequested(); return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    private string OperationRoot(RestoreState state) => Path.Combine(JournalDataSession.WorkRoot(paths), state.OperationId.ToString("N"));
    private void WriteState(RestoreState state)
    {
        string destination = JournalDataSession.StatePath(paths);
        string pending = Path.Combine(OperationRoot(state), "intent-" + Guid.NewGuid().ToString("N") + ".json");
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(state, BackupArchiveContract.JsonOptions);
        using (var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        { output.Write(bytes); output.Flush(true); }
        File.Move(pending, destination, overwrite: true);
    }
    private RestoreState ReadState()
    {
        string file = JournalDataSession.StatePath(paths); SqliteDatabaseSnapshotService.EnsureNoLinks(file);
        using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > 4096 || !BackupFileIdentity.IsUnaliased(input, file)) throw new IOException();
        byte[] bytes = new byte[(int)input.Length]; input.ReadExactly(bytes);
        var state = JsonSerializer.Deserialize<RestoreState>(bytes, BackupArchiveContract.JsonOptions) ?? throw new IOException();
        if (state.Version != 1 || state.OperationId == Guid.Empty || !IsHash(state.IncomingHash) || !IsHash(state.RecoveryHash) ||
            state.OriginalDatabase.Path != BackupArchiveContract.DatabasePath || state.OriginalDatabase.Kind != BackupFileKind.Database ||
            !IsHash(state.OriginalDatabase.Sha256) || state.OriginalDatabase.SizeBytes <= 0 ||
            (state.OriginalPreferences is { } p && (p.Path != BackupArchiveContract.PreferencesPath || p.Kind != BackupFileKind.Preferences || !IsHash(p.Sha256))))
            throw new IOException();
        return state;
    }
    private void ClearState()
    {
        // Only the fixed operation-control file, never database sidecars or user-selected paths.
        File.Delete(JournalDataSession.StatePath(paths));
    }
    private void ValidateLayout()
    {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(paths.DataDirectory));
        if (Path.GetDirectoryName(root) is null || !Path.IsPathFullyQualified(paths.DataDirectory) ||
            !string.Equals(Path.GetFullPath(paths.DatabasePath), Path.Combine(root, "journal.db"), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetFullPath(paths.ScreenshotsDirectory), Path.Combine(root, "screenshots"), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetFullPath(paths.SettingsPath), Path.Combine(root, "settings.json"), StringComparison.OrdinalIgnoreCase)) throw new IOException();
        SqliteDatabaseSnapshotService.EnsureNoLinks(root);
        SqliteDatabaseSnapshotService.EnsureNoLinks(JournalDataSession.StatePath(paths));
    }
    private void RequireSpace(long bytes) { if (freeSpace(paths.DataDirectory) < bytes) throw new StorageFailure(); }
    private static long AvailableSpace(string path)
    {
        var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!);
        if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable)) throw new IOException();
        return drive.AvailableFreeSpace;
    }
    private static bool IsHash(string value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private sealed record RestoreState(int Version, Guid OperationId, bool Committed, string IncomingHash, string RecoveryHash,
        BackupFileEntry OriginalDatabase, BackupFileEntry? OriginalPreferences);
    private sealed class StorageFailure : IOException;
}

internal sealed class RestoreInterruption : Exception;
