namespace PersonalTradingJournal.Application.Backups;

/// <summary>Database-only staging. Success is not a complete backup; the caller owns the returned staging directory.</summary>
public interface IDatabaseSnapshotService
{
    Task<DatabaseSnapshotResult> CreateAsync(string stagingParentDirectory, CancellationToken cancellationToken = default);
}

public enum DatabaseSnapshotStatus
{
    Success, SourceMissing, SourceUnavailable, DestinationUnavailable, Busy, InsufficientStorage,
    InvalidDatabase, ForeignKeyViolation, IncompatibleSchema, LimitExceeded, Cancelled, IoFailure
}

public enum DatabaseSnapshotPhase { Source, Staging, Copy, Integrity, ForeignKeys, Schema, Finalize }

/// <summary>Paths are local handoff details, never manifest fields or diagnostic text.</summary>
public sealed record StagedDatabaseSnapshot(string DirectoryPath, string DatabasePath, DateTimeOffset CreatedAtUtc,
    BackupDatabaseSchema Schema, BackupFileEntry File)
{
    public override string ToString() => "StagedDatabaseSnapshot (database only)";
}

/// <summary>Allowlisted diagnostics only. CleanupFailed means an incomplete staging directory may remain; never usable.</summary>
public sealed record DatabaseSnapshotResult(DatabaseSnapshotStatus Status, Guid OperationId,
    DatabaseSnapshotPhase Phase, StagedDatabaseSnapshot? Snapshot = null, int? SqliteErrorCode = null,
    bool CleanupFailed = false)
{
    public override string ToString() => $"Database snapshot: {Status}; phase={Phase}; operation={OperationId:N}; sqlite={SqliteErrorCode}; cleanupFailed={CleanupFailed}";
}
