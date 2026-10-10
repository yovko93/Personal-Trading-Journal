using System.Collections.Immutable;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Backups;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Backups;

/// <summary>Untrusted archive inspection. Has no dependency on the installed journal or screenshot store.</summary>
public sealed class RestorePreflightService : IRestorePreflightService
{
    private readonly Action<RestorePreflightPhase, string?>? hook;
    private readonly PreflightLimits limits;
    private readonly Action<BackupManifest, IReadOnlyDictionary<string, string>, RestorePreflightSummary>? capture;
    public RestorePreflightService() : this(null, new()) { }
    internal RestorePreflightService(Action<RestorePreflightPhase, string?>? hook, PreflightLimits limits,
        Action<BackupManifest, IReadOnlyDictionary<string, string>, RestorePreflightSummary>? capture = null)
    { this.hook = hook; this.limits = limits; this.capture = capture; }

    public Task<RestorePreflightResult> InspectAsync(string archivePath, string stagingParentDirectory, CancellationToken cancellationToken = default) =>
        Task.Run(() => Inspect(archivePath, stagingParentDirectory, cancellationToken), CancellationToken.None);

    private RestorePreflightResult Inspect(string archivePath, string stagingParentDirectory, CancellationToken token)
    {
        Guid id = Guid.NewGuid(); var phase = RestorePreflightPhase.Container;
        string? owned = null; var files = new List<string>();
        var compatibility = BackupSchemaCompatibility.Unsupported;
        RestorePreflightResult result = new(id, phase, BackupValidationCode.IncompleteArchive);
        void Check(RestorePreflightPhase next) { phase = next; token.ThrowIfCancellationRequested(); hook?.Invoke(next, owned); token.ThrowIfCancellationRequested(); }
        try
        {
            Check(RestorePreflightPhase.Container);
            string source = Path.GetFullPath(archivePath), parent = Path.GetFullPath(stagingParentDirectory);
            SqliteDatabaseSnapshotService.EnsureNoLinks(source);
            SqliteDatabaseSnapshotService.EnsureNoLinks(parent);
            if (!Directory.Exists(parent)) throw new IOException();
            using var file = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (!BackupFileIdentity.IsUnaliased(file, source)) throw new PreflightFailure(BackupValidationCode.UnsafePath);
            var directory = PreflightZipDirectory.Read(file, limits, token);
            file.Position = 0;
            string archiveHash = Hash(file, token);
            using var zip = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: true);
            if (zip.Entries.Count != directory.Count) throw new PreflightFailure(BackupValidationCode.IncompleteArchive);
            var entries = new Dictionary<string, (ZipArchiveEntry Zip, PreflightZipDirectory.Entry Metadata)>(StringComparer.Ordinal);
            for (int i = 0; i < directory.Count; i++)
            {
                var entry = zip.Entries[i]; var metadata = directory[i];
                if (entry.FullName != metadata.Name || entry.Length != metadata.Size || entry.CompressedLength != metadata.CompressedSize)
                    throw new PreflightFailure(BackupValidationCode.IncompleteArchive);
                entries.Add(metadata.Name, (entry, metadata));
            }
            Check(RestorePreflightPhase.Manifest);
            if (!entries.TryGetValue(BackupArchiveContract.ManifestPath, out var manifestEntry)) throw new PreflightFailure(BackupValidationCode.MissingEntry);
            using var manifestBytes = new MemoryStream();
            Transfer(file, manifestEntry.Metadata, manifestBytes, token);
            var manifest = JsonSerializer.Deserialize<BackupManifest>(manifestBytes.GetBuffer().AsSpan(0, (int)manifestBytes.Length), BackupArchiveContract.JsonOptions);
            using var catalog = new JournalDbContext(new DbContextOptionsBuilder<JournalDbContext>().UseSqlite("Data Source=:memory:").Options);
            var known = catalog.Database.GetMigrations().ToImmutableArray();
            var check = BackupManifestValidator.Validate(manifest, known, token);
            compatibility = check.SchemaCompatibility;
            if (!check.Issues.IsEmpty) throw new PreflightFailure(check.Issues[0].Code);
            if (compatibility != BackupSchemaCompatibility.Exact) throw new PreflightFailure(BackupValidationCode.UnsupportedDatabaseSchema);
            var payloads = manifest!.Files;
            if (payloads.Any(p => !entries.ContainsKey(p.Path))) throw new PreflightFailure(BackupValidationCode.MissingEntry);
            if (entries.Count != payloads.Length + 1) throw new PreflightFailure(BackupValidationCode.UnexpectedEntry);
            if (payloads.Any(p => p.SizeBytes != entries[p.Path].Metadata.Size)) throw new PreflightFailure(BackupValidationCode.ContentMismatch);

            Check(RestorePreflightPhase.Staging);
            string pending = Path.Combine(parent, ".preflight-" + id.ToString("N") + ".partial");
            if (Path.Exists(pending)) throw new IOException();
            BackupArchiveService.CreatePrivateDirectory(pending);
            owned = pending;
            Check(RestorePreflightPhase.Payloads);
            var staged = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var payload in payloads)
            {
                token.ThrowIfCancellationRequested();
                // Never extract an untrusted name to a filesystem path, even after path validation.
                string target = Path.Combine(owned, "payload-" + files.Count.ToString("D6", System.Globalization.CultureInfo.InvariantCulture));
                using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    files.Add(target);
                    var entry = entries[payload.Path];
                    if (Transfer(file, entry.Metadata, output, token) != payload.Sha256)
                        throw new PreflightFailure(BackupValidationCode.ContentMismatch);
                }
                staged.Add(payload.Path, target);
                if (payload.Kind == BackupFileKind.Preferences) ValidatePreferences(target);
            }
            var summary = VerifyStaged(manifest, staged, file.Length, archiveHash, Check, token);
            capture?.Invoke(manifest, staged, summary);
            token.ThrowIfCancellationRequested();
            result = new(id, phase, null, compatibility, summary);
        }
        catch (OperationCanceledException) { result = new(id, phase, BackupValidationCode.Cancelled, compatibility); }
        catch (PreflightFailure e) { result = new(id, phase, e.Code, compatibility); }
        catch (SqliteDatabaseSnapshotService.SnapshotFailure) { result = new(id, phase, BackupValidationCode.UnsupportedDatabaseSchema, compatibility); }
        catch (SqliteException e) { result = new(id, phase, token.IsCancellationRequested ? BackupValidationCode.Cancelled :
            e.SqliteErrorCode == 9 ? BackupValidationCode.LimitExceeded : BackupValidationCode.DatabaseIntegrityFailed, compatibility); }
        catch (JsonException) { result = new(id, phase, BackupValidationCode.InvalidManifest, compatibility); }
        catch (InvalidDataException) { result = new(id, phase, BackupValidationCode.IncompleteArchive, compatibility); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or OverflowException)
        { result = new(id, phase, BackupValidationCode.IoFailure, compatibility); }
        finally
        {
            bool failed = false;
            foreach (string path in files)
                try { File.Delete(path); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { failed = true; }
            if (owned is not null)
                try { Directory.Delete(owned); }
                catch (DirectoryNotFoundException) { }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { failed = true; }
            result = result with { CleanupFailed = failed };
        }
        return result;
    }

    internal RestorePreflightSummary VerifyStaged(BackupManifest manifest, IReadOnlyDictionary<string, string> staged, long archiveBytes,
        string archiveHash, Action<RestorePreflightPhase> check, CancellationToken token)
    {
        var payloads = manifest.Files;
        check(RestorePreflightPhase.Database);
        // M16.2 produces rollback-mode standalone files. A WAL-mode header could create sidecars on a read-only open.
        using (var header = File.OpenRead(staged[BackupArchiveContract.DatabasePath]))
        {
            Span<byte> bytes = stackalloc byte[100];
            if (header.Read(bytes) != bytes.Length || !bytes[..16].SequenceEqual("SQLite format 3\0"u8) || bytes[18] != 1 || bytes[19] != 1)
                throw new PreflightFailure(BackupValidationCode.DatabaseIntegrityFailed);
        }
        using (var database = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = staged[BackupArchiveContract.DatabasePath], Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private, Pooling = false, DefaultTimeout = 2 }.ToString()))
        {
            database.Open();
            var watch = Stopwatch.StartNew(); int callbacks = 0;
            SQLitePCL.raw.sqlite3_limit(database.Handle, SQLitePCL.raw.SQLITE_LIMIT_LENGTH, 16 * 1024 * 1024);
            SQLitePCL.raw.sqlite3_limit(database.Handle, SQLitePCL.raw.SQLITE_LIMIT_SQL_LENGTH, 1024 * 1024);
            SQLitePCL.raw.sqlite3_limit(database.Handle, SQLitePCL.raw.SQLITE_LIMIT_ATTACHED, 0);
            SQLitePCL.raw.sqlite3_progress_handler(database.Handle, limits.DatabaseProgressInterval, _ =>
                token.IsCancellationRequested || ++callbacks > limits.DatabaseProgressCallbacks || watch.Elapsed.TotalSeconds > limits.DatabaseSeconds ? 1 : 0, null);
            Execute(database, "PRAGMA query_only=ON; PRAGMA trusted_schema=OFF; PRAGMA temp_store=MEMORY; PRAGMA cache_size=-4096;");
            using (var command = database.CreateCommand())
            {
                command.CommandText = "PRAGMA integrity_check(1);";
                using var rows = command.ExecuteReader();
                if (!rows.Read() || rows.GetString(0) != "ok" || rows.Read()) throw new PreflightFailure(BackupValidationCode.DatabaseIntegrityFailed);
            }
            using (var command = database.CreateCommand())
            {
                command.CommandText = "PRAGMA foreign_key_check;";
                using var rows = command.ExecuteReader();
                if (rows.Read()) throw new PreflightFailure(BackupValidationCode.DatabaseIntegrityFailed);
            }
            // Bound materialized schema strings before the shared exact-schema comparer reads them.
            using (var command = database.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(*), COALESCE(SUM(length(CAST(sql AS BLOB))),0), COALESCE(MAX(length(name)+length(tbl_name)),0) FROM sqlite_schema;";
                using var rows = command.ExecuteReader();
                if (!rows.Read() || rows.GetInt64(0) > 4096 || rows.GetInt64(1) > 4 * 1024 * 1024 || rows.GetInt64(2) > 512)
                    throw new PreflightFailure(BackupValidationCode.LimitExceeded);
            }
            var migrations = SqliteDatabaseSnapshotService.ValidateSchema(database, token);
            if (!manifest.DatabaseSchema.AppliedMigrations.SequenceEqual(migrations)) throw new PreflightFailure(BackupValidationCode.UnsupportedDatabaseSchema);
            check(RestorePreflightPhase.Attachments);
            var expected = payloads.Where(p => p.Kind == BackupFileKind.Screenshot).Select(p => p.Path).ToHashSet(StringComparer.Ordinal);
            var actual = new HashSet<string>(StringComparer.Ordinal);
            using (var command = database.CreateCommand())
            {
                command.CommandText = "SELECT DISTINCT StorageKey COLLATE BINARY FROM TradeScreenshots LIMIT 100001;";
                using var rows = command.ExecuteReader();
                while (rows.Read())
                {
                    token.ThrowIfCancellationRequested();
                    string key = rows.GetString(0);
                    if (!BackupManifestValidator.IsPortableScreenshotKey(key)) throw new PreflightFailure(BackupValidationCode.DatabaseAttachmentMismatch);
                    actual.Add(BackupArchiveContract.ScreenshotsPrefix + key);
                    if (actual.Count > limits.Files - 2) throw new PreflightFailure(BackupValidationCode.LimitExceeded);
                }
            }
            if (!expected.SetEquals(actual)) throw new PreflightFailure(BackupValidationCode.DatabaseAttachmentMismatch);
            check(RestorePreflightPhase.Summary);
            long Count(string table) { token.ThrowIfCancellationRequested(); using var command = database.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM " + table; return (long)command.ExecuteScalar()!; }
            var summary = new RestorePreflightSummary(manifest.CreatedAtUtc, manifest.ArchiveVersion, migrations[^1], migrations.Length,
                Count("TradingAccounts"), Count("Trades"), Count("DailyJournals"), Count("DailyJournalRevisions"), Count("CoachingAnalyses"),
                Count("TradeScreenshots"), expected.Count, payloads.Sum(p => p.SizeBytes), archiveBytes, archiveHash,
                [RestorePreflightWarning.UnencryptedSensitiveData, RestorePreflightWarning.AiCredentialsExcluded, RestorePreflightWarning.RevalidationRequired]);
            token.ThrowIfCancellationRequested();
            return summary;
        }
    }

    private static void Execute(SqliteConnection db, string sql) { using var command = db.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery(); }
    private static void ValidatePreferences(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path), new JsonDocumentOptions { MaxDepth = 2 });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 1 ||
            !root.TryGetProperty("Theme", out var theme) || theme.ValueKind != JsonValueKind.String || theme.GetString() is not ("System" or "Light" or "Dark"))
            throw new PreflightFailure(BackupValidationCode.ContentMismatch);
    }
    private static string Hash(Stream stream, CancellationToken token)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); var buffer = new byte[81920]; int read;
        while ((read = stream.Read(buffer)) != 0) { token.ThrowIfCancellationRequested(); hash.AppendData(buffer, 0, read); }
        token.ThrowIfCancellationRequested(); return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    private static string Transfer(FileStream archive, PreflightZipDirectory.Entry metadata, Stream output, CancellationToken token)
    {
        // ZipArchiveEntry.Open can stop at a forged declared length. Inflate the bounded compressed range ourselves
        // so additional output is detected, not silently ignored even when the declared prefix's hash/CRC is valid.
        using var compressed = new CompressedRange(archive, metadata.DataOffset, metadata.CompressedSize);
        using Stream input = metadata.Method == 8 ? new DeflateStream(compressed, CompressionMode.Decompress, leaveOpen: true) : compressed;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920]; long length = 0; uint crc = uint.MaxValue; int read;
        while ((read = input.Read(buffer)) != 0)
        {
            token.ThrowIfCancellationRequested();
            if (read > metadata.Size - length) throw new PreflightFailure(BackupValidationCode.LimitExceeded);
            length += read; hash.AppendData(buffer, 0, read);
            for (int i = 0; i < read; i++) crc = CrcTable[(crc ^ buffer[i]) & 255] ^ (crc >> 8);
            output.Write(buffer, 0, read);
        }
        token.ThrowIfCancellationRequested();
        if (length != metadata.Size || ~crc != metadata.Crc || compressed.Remaining != 0) throw new PreflightFailure(BackupValidationCode.ContentMismatch);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(i =>
    { uint value = (uint)i; for (int bit = 0; bit < 8; bit++) value = (value & 1) != 0 ? 0xedb88320 ^ (value >> 1) : value >> 1; return value; }).ToArray();

    private sealed class CompressedRange : Stream
    {
        private readonly FileStream source;
        internal long Remaining { get; private set; }
        internal CompressedRange(FileStream source, long start, long length) { this.source = source; source.Position = start; Remaining = length; }
        public override int Read(byte[] buffer, int offset, int count)
        { int read = source.Read(buffer, offset, (int)Math.Min(count, Remaining)); Remaining -= read; return read; }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
