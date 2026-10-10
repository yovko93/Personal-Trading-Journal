using System.Collections.Immutable;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Backups;
using PersonalTradingJournal.Application.Common.Storage;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Backups;

/// <summary>Uses SQLite's backup facility, never a live-file copy. No source writes, migration or pool-wide cleanup.</summary>
public sealed class SqliteDatabaseSnapshotService : IDatabaseSnapshotService
{
    private readonly IApplicationPaths _paths;
    private readonly Action<DatabaseSnapshotPhase, SqliteConnection?>? _checkpoint;

    public SqliteDatabaseSnapshotService(IApplicationPaths paths) : this(paths, null) { }

    // Deterministic fault/coordination seam; not registered or exposed to application callers.
    internal SqliteDatabaseSnapshotService(IApplicationPaths paths, Action<DatabaseSnapshotPhase, SqliteConnection?>? checkpoint)
    { _paths = paths; _checkpoint = checkpoint; }

    public Task<DatabaseSnapshotResult> CreateAsync(string stagingParentDirectory, CancellationToken cancellationToken = default) =>
        Task.Run(() => Create(stagingParentDirectory, cancellationToken), CancellationToken.None);

    private DatabaseSnapshotResult Create(string parent, CancellationToken token)
    {
        Guid id = Guid.NewGuid();
        var phase = DatabaseSnapshotPhase.Source;
        string? owned = null;
        string? published = null;
        var result = new DatabaseSnapshotResult(DatabaseSnapshotStatus.IoFailure, id, phase);
        void Check(DatabaseSnapshotPhase next, SqliteConnection? connection = null)
        { phase = next; token.ThrowIfCancellationRequested(); _checkpoint?.Invoke(next, connection); token.ThrowIfCancellationRequested(); }
        try
        {
            Check(DatabaseSnapshotPhase.Source);
            string sourcePath = Path.GetFullPath(_paths.DatabasePath);
            EnsureNoLinks(sourcePath);
            if ((File.GetAttributes(sourcePath) & FileAttributes.Directory) != 0)
                throw new SnapshotFailure(DatabaseSnapshotStatus.SourceUnavailable);
            using var source = Connection(sourcePath, SqliteOpenMode.ReadOnly);
            source.Open();

            Check(DatabaseSnapshotPhase.Staging);
            if (!Path.IsPathFullyQualified(parent)) throw new SnapshotFailure(DatabaseSnapshotStatus.DestinationUnavailable);
            parent = Path.GetFullPath(parent);
            EnsureNoLinks(parent);
            Directory.CreateDirectory(parent);
            string pending = Path.Combine(parent, $"snapshot-{id:N}.partial");
            string completed = Path.Combine(parent, $"snapshot-{id:N}");
            if (Directory.Exists(pending) || Directory.Exists(completed) || File.Exists(pending) || File.Exists(completed))
                throw new SnapshotFailure(DatabaseSnapshotStatus.DestinationUnavailable);
            Directory.CreateDirectory(pending);
            owned = pending;
            string file = Path.Combine(pending, "journal.db");
            // Reserve a new regular file; never open an arbitrary existing destination for replacement.
            using (new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
            ImmutableArray<string> migrations;
            using (var destination = Connection(file, SqliteOpenMode.ReadWrite))
            {
                destination.Open();
                Check(DatabaseSnapshotPhase.Copy, destination);
                source.BackupDatabase(destination);
                // BackupDatabase has no CancellationToken and performs one synchronous native step.
                Check(DatabaseSnapshotPhase.Integrity, destination);
                // Only the destination is changed to standalone rollback mode; source WAL is never checkpointed here.
                if (!string.Equals(Scalar(destination, "PRAGMA journal_mode=DELETE;", token)?.ToString(), "delete", StringComparison.OrdinalIgnoreCase))
                    throw new SnapshotFailure(DatabaseSnapshotStatus.InvalidDatabase);
                using (var command = destination.CreateCommand())
                {
                    command.CommandText = "PRAGMA integrity_check;";
                    using var rows = command.ExecuteReader();
                    if (!rows.Read() || rows.GetString(0) != "ok" || rows.Read())
                        throw new SnapshotFailure(DatabaseSnapshotStatus.InvalidDatabase);
                }
                Check(DatabaseSnapshotPhase.ForeignKeys, destination);
                using (var command = destination.CreateCommand())
                {
                    command.CommandText = "PRAGMA foreign_key_check;";
                    using var rows = command.ExecuteReader();
                    if (rows.Read()) throw new SnapshotFailure(DatabaseSnapshotStatus.ForeignKeyViolation);
                }
                Check(DatabaseSnapshotPhase.Schema, destination);
                migrations = ValidateSchema(destination, token);
            }
            source.Close();
            Check(DatabaseSnapshotPhase.Finalize);
            long size = new FileInfo(file).Length;
            if (size <= 0 || size > BackupArchiveContract.MaximumDatabaseBytes)
                throw new SnapshotFailure(DatabaseSnapshotStatus.LimitExceeded);
            // A successful standalone snapshot must not depend on journal/WAL/shared-memory sidecars.
            if (Directory.EnumerateFiles(pending).Any(p => p != file))
                throw new SnapshotFailure(DatabaseSnapshotStatus.InvalidDatabase);
            string hash;
            using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                byte[] buffer = new byte[81920]; int read;
                while ((read = stream.Read(buffer)) != 0) { token.ThrowIfCancellationRequested(); digest.AppendData(buffer, 0, read); }
                hash = Convert.ToHexStringLower(digest.GetHashAndReset());
            }
            token.ThrowIfCancellationRequested();
            Directory.Move(pending, completed); // Does not overwrite an existing completed directory.
            published = completed;
            token.ThrowIfCancellationRequested();
            result = new(DatabaseSnapshotStatus.Success, id, phase,
                new(completed, Path.Combine(completed, "journal.db"), DateTimeOffset.UtcNow,
                    new("sqlite", migrations), new(BackupArchiveContract.DatabasePath, BackupFileKind.Database, size, hash)));
        }
        catch (OperationCanceledException) { result = new(DatabaseSnapshotStatus.Cancelled, id, phase); }
        catch (SnapshotFailure e) { result = new(e.Status, id, phase); }
        catch (SqliteException e)
        {
            var status = e.SqliteErrorCode switch
            {
                5 or 6 => DatabaseSnapshotStatus.Busy,
                13 => DatabaseSnapshotStatus.InsufficientStorage,
                11 or 26 => DatabaseSnapshotStatus.InvalidDatabase,
                14 or 3 or 8 => phase == DatabaseSnapshotPhase.Source ? DatabaseSnapshotStatus.SourceUnavailable : DatabaseSnapshotStatus.DestinationUnavailable,
                1 when phase == DatabaseSnapshotPhase.Schema => DatabaseSnapshotStatus.IncompatibleSchema,
                1 when phase is DatabaseSnapshotPhase.Integrity or DatabaseSnapshotPhase.ForeignKeys => DatabaseSnapshotStatus.InvalidDatabase,
                _ => DatabaseSnapshotStatus.IoFailure
            };
            result = new(status, id, phase, SqliteErrorCode: e.SqliteErrorCode);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            var status = (e.HResult & 0xffff) is 39 or 112 ? DatabaseSnapshotStatus.InsufficientStorage :
                phase == DatabaseSnapshotPhase.Source ? (e is FileNotFoundException or DirectoryNotFoundException
                    ? DatabaseSnapshotStatus.SourceMissing : DatabaseSnapshotStatus.SourceUnavailable) :
                phase == DatabaseSnapshotPhase.Staging ? DatabaseSnapshotStatus.DestinationUnavailable : DatabaseSnapshotStatus.IoFailure;
            result = new(status, id, phase);
        }
        finally
        {
            if (result.Status != DatabaseSnapshotStatus.Success && owned is not null)
            {
                // Only files owned by this operation; no recursive deletion or broad pool invalidation.
                string cleanup = published ?? owned;
                bool failed = false;
                foreach (string suffix in new[] { "", "-wal", "-shm", "-journal" })
                    try { File.Delete(Path.Combine(cleanup, "journal.db" + suffix)); }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException) { failed = true; }
                try { Directory.Delete(cleanup); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { failed = true; }
                result = result with { CleanupFailed = failed };
            }
        }
        return result;
    }

    private static SqliteConnection Connection(string path, SqliteOpenMode mode) => new(new SqliteConnectionStringBuilder
    { DataSource = path, Mode = mode, Cache = SqliteCacheMode.Private, Pooling = false, DefaultTimeout = 2, ForeignKeys = true }.ToString());

    private static object? Scalar(SqliteConnection connection, string sql, CancellationToken token)
    { token.ThrowIfCancellationRequested(); using var command = connection.CreateCommand(); command.CommandText = sql; return command.ExecuteScalar(); }

    internal static ImmutableArray<string> ValidateSchema(SqliteConnection snapshot, CancellationToken token)
    {
        // Construct the trusted schema from shipped migrations in memory, never from the installed source or manifest claims.
        using var reference = new SqliteConnection("Data Source=:memory:;Pooling=False;Default Timeout=2");
        reference.Open();
        using var context = new JournalDbContext(new DbContextOptionsBuilder<JournalDbContext>().UseSqlite(reference).Options);
        var known = context.Database.GetMigrations().ToImmutableArray();
        var applied = ImmutableArray.CreateBuilder<string>();
        using (var command = snapshot.CreateCommand())
        {
            command.CommandText = "SELECT CASE WHEN length(MigrationId) <= 160 THEN MigrationId ELSE NULL END FROM __EFMigrationsHistory ORDER BY MigrationId LIMIT 1025;";
            using var rows = command.ExecuteReader();
            while (rows.Read())
            {
                token.ThrowIfCancellationRequested();
                if (rows.IsDBNull(0)) throw new SnapshotFailure(DatabaseSnapshotStatus.IncompatibleSchema);
                applied.Add(rows.GetString(0));
            }
        }
        // Capture only the current schema; M16.1 older-prefix restore migration remains a later workflow.
        if (!known.SequenceEqual(applied)) throw new SnapshotFailure(DatabaseSnapshotStatus.IncompatibleSchema);
        context.Database.Migrate();
        var expected = Schema(reference, token);
        if (!expected.SequenceEqual(Schema(snapshot, token))) throw new SnapshotFailure(DatabaseSnapshotStatus.IncompatibleSchema);
        return applied.ToImmutable();
    }

    private static List<string> Schema(SqliteConnection connection, CancellationToken token)
    {
        using var command = connection.CreateCommand();
        // SQLite's internal indexes/stats are not application schema; EF's transient migration lock is not journal data.
        command.CommandText = "SELECT type, name, tbl_name, sql FROM sqlite_schema WHERE name NOT GLOB 'sqlite_*' AND name <> '__EFMigrationsLock' ORDER BY type, name LIMIT 4097;";
        using var rows = command.ExecuteReader(); var result = new List<string>();
        while (rows.Read())
        {
            token.ThrowIfCancellationRequested();
            if (result.Count == 4096) throw new SnapshotFailure(DatabaseSnapshotStatus.IncompatibleSchema);
            result.Add(string.Join('\n', Enumerable.Range(0, 4).Select(i => rows.IsDBNull(i) ? "" : rows.GetString(i))));
        }
        return result;
    }

    internal static void EnsureNoLinks(string path)
    {
        for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
            if (Path.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked storage is not supported for snapshot staging.");
    }

    internal sealed class SnapshotFailure(DatabaseSnapshotStatus status) : Exception
    { public DatabaseSnapshotStatus Status { get; } = status; }
}
