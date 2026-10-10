namespace PersonalTradingJournal.Application.Backups;

/// <summary>Explicit creation only. Contains sensitive, unencrypted journal data; never overwrites a destination.</summary>
public interface IBackupArchiveService
{
    Task<BackupArchiveResult> CreateAsync(string destinationPath, CancellationToken cancellationToken = default);
}

public enum BackupArchivePhase { Destination, Snapshot, Inventory, Capture, Package, Verify, Publish }

/// <summary>No source paths, keys, payloads or exception messages in diagnostics. Null Failure means success.</summary>
public sealed record BackupArchiveResult(Guid OperationId, BackupArchivePhase Phase,
    BackupValidationCode? Failure = null, DatabaseSnapshotStatus? SnapshotFailure = null,
    bool CleanupFailed = false, bool CaptureBusy = false)
{
    public bool Succeeded => Failure is null;
}
