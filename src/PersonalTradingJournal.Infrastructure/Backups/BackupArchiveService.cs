using System.Collections.Immutable;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using PersonalTradingJournal.Application.Backups;
using PersonalTradingJournal.Application.Common.Storage;
using PersonalTradingJournal.Infrastructure.Screenshots;

namespace PersonalTradingJournal.Infrastructure.Backups;

/// <summary>Capture, package, verify, then publish. Never extracts into installed data or performs restore.</summary>
public sealed class BackupArchiveService : IBackupArchiveService
{
    private readonly IApplicationPaths _paths;
    private readonly IDatabaseSnapshotService _snapshots;
    private readonly Action<BackupArchivePhase, string>? _checkpoint;
    private readonly ArchiveLimits _limits;

    public BackupArchiveService(IApplicationPaths paths, IDatabaseSnapshotService snapshots)
        : this(paths, snapshots, null, new()) { }

    // Fault/coordination seams and lower test ceilings, not public configuration or production retries.
    internal BackupArchiveService(IApplicationPaths paths, IDatabaseSnapshotService snapshots,
        Action<BackupArchivePhase, string>? checkpoint, ArchiveLimits limits)
    { _paths = paths; _snapshots = snapshots; _checkpoint = checkpoint; _limits = limits; }

    public Task<BackupArchiveResult> CreateAsync(string destinationPath, CancellationToken cancellationToken = default) =>
        Task.Run(() => Create(destinationPath, cancellationToken), CancellationToken.None);

    private BackupArchiveResult Create(string destination, CancellationToken token)
    {
        Guid id = Guid.NewGuid();
        var phase = BackupArchivePhase.Destination;
        var result = new BackupArchiveResult(id, phase, BackupValidationCode.IoFailure);
        string? root = null;
        var owned = new List<string>();
        var directories = new List<string>();
        void Check(BackupArchivePhase next)
        { phase = next; token.ThrowIfCancellationRequested(); _checkpoint?.Invoke(next, root!); token.ThrowIfCancellationRequested(); }
        try
        {
            token.ThrowIfCancellationRequested();
            if (!Path.IsPathFullyQualified(destination) || !destination.EndsWith(".ptjbackup", StringComparison.OrdinalIgnoreCase))
                throw new Failure(BackupValidationCode.UnsafePath);
            destination = Path.GetFullPath(destination);
            SqliteDatabaseSnapshotService.EnsureNoLinks(destination);
            if (Path.Exists(destination)) throw new Failure(BackupValidationCode.DuplicateEntry);
            string parent = Path.GetDirectoryName(destination)!;
            // Destination parent must already exist. No speculative directory tree creation.
            if (!Directory.Exists(parent)) throw new Failure(BackupValidationCode.IoFailure);
            root = Path.Combine(parent, ".ptjbackup-" + id.ToString("N") + ".partial");
            if (Path.Exists(root)) throw new Failure(BackupValidationCode.DuplicateEntry);
            CreatePrivateDirectory(root); directories.Add(root);
            var payload = new SortedDictionary<string, (BackupFileEntry Entry, string File)>(StringComparer.Ordinal);
            StagedDatabaseSnapshot snapshot;
            long total = 0;
            Check(BackupArchivePhase.Snapshot);
            using (ScreenshotCaptureLease.Acquire(_paths.ScreenshotsDirectory, token))
            {
                // Stay on this worker thread while holding the thread-affine cross-process lease.
                var captured = _snapshots.CreateAsync(root, token).GetAwaiter().GetResult();
                if (captured.Status != DatabaseSnapshotStatus.Success)
                {
                    result = result with { SnapshotFailure = captured.Status, CleanupFailed = captured.CleanupFailed };
                    throw new Failure(captured.Status == DatabaseSnapshotStatus.Cancelled ? BackupValidationCode.Cancelled : BackupValidationCode.DatabaseIntegrityFailed);
                }
                snapshot = captured.Snapshot!;
                directories.Add(snapshot.DirectoryPath); owned.Add(snapshot.DatabasePath);
                Check(BackupArchivePhase.Inventory);
                // Hold the same immutable DB handle from inventory through staging; validate its original digest.
                using var database = Read(snapshot.DatabasePath);
                var actualDb = Transfer(database, Stream.Null, snapshot.File.Path, BackupFileKind.Database, _limits.DatabaseBytes, token);
                if (actualDb != snapshot.File) throw new Failure(BackupValidationCode.SourceChanged);
                var keys = ReadKeys(snapshot.DatabasePath, token);
                payload.Add(snapshot.File.Path, (snapshot.File, snapshot.DatabasePath)); total = snapshot.File.SizeBytes;
                var storage = new LocalTradeScreenshotFileStorage(_paths);
                Check(BackupArchivePhase.Capture);
                foreach (string key in keys)
                {
                    token.ThrowIfCancellationRequested();
                    string source = storage.ResolveStoragePath(key);
                    EnsureUnlinked(source);
                    using var input = storage.OpenReadAsync(key, token).GetAwaiter().GetResult();
                    if (input is null) throw new Failure(BackupValidationCode.DatabaseAttachmentMismatch);
                    EnsureUnlinked(source);
                    if (input is not FileStream sourceFile || !BackupFileIdentity.IsUnaliased(sourceFile, source))
                        throw new Failure(BackupValidationCode.UnsafePath);
                    string stage = Path.Combine(root, "attachment-" + payload.Count + ".bin");
                    using var output = NewFile(stage);
                    owned.Add(stage);
                    long remaining = _limits.PayloadBytes - total;
                    var entry = Transfer(input, output, BackupArchiveContract.ScreenshotsPrefix + key, BackupFileKind.Screenshot,
                        Math.Min(_limits.ScreenshotBytes, remaining), token);
                    output.Flush(true);
                    // A second read detects changes on platforms where sharing is advisory, not a Windows write/delete lock.
                    input.Position = 0;
                    if (Transfer(input, Stream.Null, entry.Path, entry.Kind, entry.SizeBytes, token) != entry)
                        throw new Failure(BackupValidationCode.SourceChanged);
                    payload.Add(entry.Path, (entry, stage)); total += entry.SizeBytes;
                }
            }
            // Captured bytes no longer depend on live screenshot lifetime. New app screenshots use fresh keys.
            CapturePreferences(root, owned, payload, token);
            if (payload.Count > _limits.Files || payload.Values.Sum(p => p.Entry.SizeBytes) > _limits.PayloadBytes)
                throw new Failure(BackupValidationCode.LimitExceeded);
            var manifest = new BackupManifest(BackupArchiveContract.Format, BackupArchiveContract.Version,
                typeof(BackupArchiveService).Assembly.GetName().Version!.ToString(), snapshot.CreatedAtUtc,
                snapshot.Schema, BackupArchiveContract.HashAlgorithm, payload.Values.Select(p => p.Entry).ToImmutableArray());
            var check = BackupManifestValidator.Validate(manifest, snapshot.Schema.AppliedMigrations, token);
            if (!check.IsValidManifest) throw new Failure(check.Issues[0].Code);
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(manifest, BackupArchiveContract.JsonOptions);
            if (json.Length > BackupArchiveContract.MaximumManifestBytes) throw new Failure(BackupValidationCode.LimitExceeded);
            string archive = Path.Combine(root, "archive.partial");
            Check(BackupArchivePhase.Package);
            using (var file = NewFile(archive))
            {
                owned.Add(archive);
                using (var zip = new ZipArchive(file, ZipArchiveMode.Create, true))
                {
                    foreach (var item in payload.Values)
                    {
                        token.ThrowIfCancellationRequested();
                        using var input = Read(item.File);
                        using var output = Entry(zip, item.Entry.Path).Open();
                        if (Transfer(input, output, item.Entry.Path, item.Entry.Kind, item.Entry.SizeBytes, token) != item.Entry)
                            throw new Failure(BackupValidationCode.SourceChanged);
                        if (file.Position > _limits.ArchiveBytes) throw new Failure(BackupValidationCode.LimitExceeded);
                    }
                    using var manifestOutput = Entry(zip, BackupArchiveContract.ManifestPath).Open();
                    manifestOutput.Write(json);
                }
                file.Flush(true);
                if (file.Length > _limits.ArchiveBytes) throw new Failure(BackupValidationCode.LimitExceeded);
            }
            Check(BackupArchivePhase.Verify);
            VerifyCreatedArchive(archive, manifest, json, token);
            // Remove staging before publication. Any cleanup error fails the attempt, never hides a partial success.
            foreach (string path in owned.Where(p => p != archive)) File.Delete(path);
            foreach (string directory in directories.AsEnumerable().Reverse().Where(p => p != root)) Directory.Delete(directory);
            Check(BackupArchivePhase.Publish);
            // Same-volume rename, no overwrite. Cancellation is linearized at this final check.
            File.Move(archive, destination, overwrite: false);
            result = result with { Phase = phase, Failure = null };
        }
        catch (OperationCanceledException) { result = result with { Phase = phase, Failure = BackupValidationCode.Cancelled }; }
        catch (ScreenshotCaptureBusyException) { result = result with { Phase = phase, Failure = BackupValidationCode.IoFailure, CaptureBusy = true }; }
        catch (Failure e) { result = result with { Phase = phase, Failure = e.Code }; }
        catch (JsonException) { result = result with { Phase = phase, Failure = BackupValidationCode.InvalidManifest }; }
        catch (InvalidDataException) { result = result with { Phase = phase, Failure = BackupValidationCode.IncompleteArchive }; }
        catch (SqliteException) { result = result with { Phase = phase, Failure = BackupValidationCode.DatabaseIntegrityFailed }; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { result = result with { Phase = phase, Failure = BackupValidationCode.IoFailure }; }
        finally
        {
            // Explicit operation-owned files only. Never recursively delete a caller path or unexpected content.
            foreach (string path in owned)
                try { File.Delete(path); }
                catch (DirectoryNotFoundException) { /* A previously cleaned operation-owned parent is already absent. */ }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { result = result with { CleanupFailed = true }; }
            foreach (string directory in directories.AsEnumerable().Reverse())
                try { Directory.Delete(directory); }
                catch (DirectoryNotFoundException) { /* Already removed before publication. */ }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { result = result with { CleanupFailed = true }; }
        }
        return result;
    }

    private List<string> ReadKeys(string database, CancellationToken token)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = database, Mode = SqliteOpenMode.ReadOnly, Pooling = false, DefaultTimeout = 2 }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT StorageKey COLLATE BINARY FROM TradeScreenshots ORDER BY StorageKey COLLATE BINARY LIMIT 100001;";
        using var rows = command.ExecuteReader();
        var keys = new List<string>(); var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (rows.Read())
        {
            token.ThrowIfCancellationRequested();
            if (keys.Count >= Math.Min(100000, _limits.Files - 1)) throw new Failure(BackupValidationCode.LimitExceeded);
            string key = rows.GetString(0);
            if (!BackupManifestValidator.IsPortableScreenshotKey(key)) throw new Failure(BackupValidationCode.UnsafePath);
            if (!unique.Add(key)) throw new Failure(BackupValidationCode.DuplicateEntry);
            keys.Add(key);
        }
        return keys;
    }

    private void CapturePreferences(string root, List<string> owned,
        SortedDictionary<string, (BackupFileEntry Entry, string File)> payload, CancellationToken token)
    {
        EnsureUnlinked(_paths.SettingsPath);
        FileStream input;
        try { input = Read(_paths.SettingsPath); }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException) { return; }
        using (input)
        {
            if (!BackupFileIdentity.IsUnaliased(input, _paths.SettingsPath)) throw new Failure(BackupValidationCode.UnsafePath);
            if (input.Length > BackupArchiveContract.MaximumPreferencesBytes) throw new Failure(BackupValidationCode.LimitExceeded);
            using var bounded = new MemoryStream();
            Transfer(input, bounded, BackupArchiveContract.PreferencesPath, BackupFileKind.Preferences, BackupArchiveContract.MaximumPreferencesBytes, token);
            using var document = JsonDocument.Parse(bounded.ToArray());
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new Failure(BackupValidationCode.InvalidManifest);
            string? theme = null;
            foreach (var property in document.RootElement.EnumerateObject())
                if (property.Name.Equals("Theme", StringComparison.OrdinalIgnoreCase))
                {
                    if (theme is not null || property.Value.ValueKind != JsonValueKind.String) throw new Failure(BackupValidationCode.InvalidManifest);
                    theme = property.Value.GetString();
                }
            if (theme is not ("System" or "Light" or "Dark")) throw new Failure(BackupValidationCode.InvalidManifest);
            byte[] safe = JsonSerializer.SerializeToUtf8Bytes(new { Theme = theme });
            string file = Path.Combine(root, "preferences.json");
            using var output = NewFile(file); owned.Add(file); using var source = new MemoryStream(safe);
            var entry = Transfer(source, output, BackupArchiveContract.PreferencesPath, BackupFileKind.Preferences, BackupArchiveContract.MaximumPreferencesBytes, token);
            output.Flush(true); payload.Add(entry.Path, (entry, file));
        }
    }

    private void VerifyCreatedArchive(string path, BackupManifest manifest, byte[] json, CancellationToken token)
    {
        using var file = Read(path);
        if (file.Length > _limits.ArchiveBytes) throw new Failure(BackupValidationCode.LimitExceeded);
        using var zip = new ZipArchive(file, ZipArchiveMode.Read);
        if (zip.Entries.Count > _limits.Files + 1) throw new Failure(BackupValidationCode.LimitExceeded);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var expected = manifest.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        foreach (var entry in zip.Entries)
        {
            token.ThrowIfCancellationRequested();
            if (!seen.Add(entry.FullName)) throw new Failure(BackupValidationCode.DuplicateEntry);
            if (entry.ExternalAttributes != 0) throw new Failure(BackupValidationCode.UnsafePath);
            using var input = entry.Open();
            if (entry.FullName == BackupArchiveContract.ManifestPath)
            {
                if (entry.Length != json.Length) throw new Failure(BackupValidationCode.ContentMismatch);
                using var bytes = new MemoryStream();
                Transfer(input, bytes, entry.FullName, BackupFileKind.Preferences, BackupArchiveContract.MaximumManifestBytes, token);
                var readManifest = JsonSerializer.Deserialize<BackupManifest>(bytes.ToArray(), BackupArchiveContract.JsonOptions);
                if (!BackupManifestValidator.Validate(readManifest, manifest.DatabaseSchema.AppliedMigrations, token).IsValidManifest ||
                    !bytes.ToArray().AsSpan().SequenceEqual(json)) throw new Failure(BackupValidationCode.ContentMismatch);
            }
            else
            {
                if (!expected.TryGetValue(entry.FullName, out var item)) throw new Failure(BackupValidationCode.UnexpectedEntry);
                if (entry.Length != item.SizeBytes || Transfer(input, Stream.Null, item.Path, item.Kind, item.SizeBytes, token) != item)
                    throw new Failure(BackupValidationCode.ContentMismatch);
            }
        }
        if (!seen.Contains(BackupArchiveContract.ManifestPath) || expected.Keys.Any(k => !seen.Contains(k)))
            throw new Failure(BackupValidationCode.MissingEntry);
        // DB hash equals the integrity/FK/schema-validated snapshot used to read the exact key set.
        // Screenshot paths came only from that key set; all payload bytes have now been independently re-read.
    }

    private static BackupFileEntry Transfer(Stream input, Stream output, string path, BackupFileKind kind, long maximum, CancellationToken token)
    {
        if (input.CanSeek && input.Length > maximum) throw new Failure(BackupValidationCode.LimitExceeded);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[81920]; long length = 0; int read;
        while ((read = input.Read(buffer)) != 0)
        {
            token.ThrowIfCancellationRequested();
            if (read > maximum - length) throw new Failure(BackupValidationCode.LimitExceeded);
            length += read; hash.AppendData(buffer, 0, read); output.Write(buffer, 0, read);
        }
        token.ThrowIfCancellationRequested();
        if (length == 0) throw new Failure(BackupValidationCode.ContentMismatch);
        return new(path, kind, length, Convert.ToHexStringLower(hash.GetHashAndReset()));
    }

    private static ZipArchiveEntry Entry(ZipArchive zip, string name)
    { var entry = zip.CreateEntry(name, CompressionLevel.Fastest); entry.ExternalAttributes = 0; return entry; }
    private static FileStream NewFile(string path) => new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.SequentialScan);
    private static FileStream Read(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan);

    private static void EnsureUnlinked(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
            if (Path.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new Failure(BackupValidationCode.UnsafePath);
    }

    internal static void CreatePrivateDirectory(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            using var identity = WindowsIdentity.GetCurrent();
            security.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            new DirectoryInfo(path).Create(security);
        }
        else Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private sealed class Failure(BackupValidationCode code) : Exception { public BackupValidationCode Code { get; } = code; }
}

internal sealed record ArchiveLimits(long DatabaseBytes = BackupArchiveContract.MaximumDatabaseBytes,
    long ScreenshotBytes = BackupArchiveContract.MaximumScreenshotBytes, long PayloadBytes = BackupArchiveContract.MaximumPayloadBytes,
    long ArchiveBytes = BackupArchiveContract.MaximumArchiveBytes, int Files = BackupArchiveContract.MaximumFiles);
