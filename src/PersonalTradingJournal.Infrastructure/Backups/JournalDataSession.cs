using Microsoft.Data.Sqlite;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PersonalTradingJournal.Application.Common.Storage;

namespace PersonalTradingJournal.Infrastructure.Backups;

/// <summary>Lifetime lease for normal application use. Maintenance is offline, never acquired over an active host.</summary>
public sealed class JournalDataSession : IDisposable
{
    private readonly FileStream lease;
    private int disposed;
    public JournalDataSession(IApplicationPaths paths)
    {
        lease = OpenLease(paths, exclusive: false);
        if (File.Exists(StatePath(paths)) || Directory.Exists(StatePath(paths)))
        { lease.Dispose(); throw new JournalMaintenanceException("Interrupted restore detected. Offline recovery is required before normal startup."); }
    }
    public void Dispose() { if (Interlocked.Exchange(ref disposed, 1) == 0) lease.Dispose(); }
    internal void EnsureActive()
    { if (Volatile.Read(ref disposed) != 0) throw new JournalMaintenanceException("The journal session is closed. Reopen the application after maintenance."); }
    internal static string StatePath(IApplicationPaths paths) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(paths.DataDirectory)) + ".restore-state.json";
    // Keep native SQLite staging below its Windows path budget; operation GUIDs isolate journals sharing a parent.
    internal static string WorkRoot(IApplicationPaths paths) => Path.Combine(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(paths.DataDirectory)))!, ".ptj-restore");
    internal static FileStream OpenLease(IApplicationPaths paths, bool exclusive)
    {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(paths.DataDirectory));
        SqliteDatabaseSnapshotService.EnsureNoLinks(root);
        string path = root + ".access.lock";
        SqliteDatabaseSnapshotService.EnsureNoLinks(path);
        try
        {
            // Stable sibling, not inside either directory being switched. The lock file is never deleted.
            Directory.CreateDirectory(Path.GetDirectoryName(root)!);
            var file = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, exclusive ? FileShare.None : FileShare.ReadWrite);
            if (!BackupFileIdentity.IsUnaliased(file, path)) { file.Dispose(); throw new IOException(); }
            return file;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { throw new JournalMaintenanceException("Journal maintenance is active or exclusive access is unavailable."); }
    }
    internal static void ClearOwnedPools(IApplicationPaths paths)
    {
        // Only after exclusive lifetime access. Do not invalidate pools belonging to another data root.
        var builder = new SqliteConnectionStringBuilder { DataSource = paths.DatabasePath, ForeignKeys = true };
        using (var connection = new SqliteConnection(builder.ToString())) SqliteConnection.ClearPool(connection);
        builder.Mode = SqliteOpenMode.ReadOnly;
        using (var connection = new SqliteConnection(builder.ToString())) SqliteConnection.ClearPool(connection);
    }
}

public sealed class JournalMaintenanceException(string message) : IOException(message);

internal sealed class JournalSessionConnectionInterceptor(JournalDataSession session) : DbConnectionInterceptor
{
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    { session.EnsureActive(); return result; }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
        InterceptionResult result, CancellationToken cancellationToken = default)
    { session.EnsureActive(); return ValueTask.FromResult(result); }
}
