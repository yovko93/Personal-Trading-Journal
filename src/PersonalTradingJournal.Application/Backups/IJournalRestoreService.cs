namespace PersonalTradingJournal.Application.Backups;

/// <summary>Offline maintenance only. A preview fingerprint is checked again, never treated as replacement authority.</summary>
public interface IJournalRestoreService
{
    Task<JournalRestoreResult> RestoreAsync(JournalRestoreRequest request, CancellationToken cancellationToken = default);
    Task<JournalRestoreResult> RecoverInterruptedAsync(CancellationToken cancellationToken = default);
}

public sealed record JournalRestoreRequest(string ArchivePath, string ConfirmedArchiveSha256)
{
    public override string ToString() => "JournalRestoreRequest (local source omitted)";
}

public enum JournalRestoreStatus { Restored, RecoveredOriginal, NothingToRecover, Blocked, RejectedArchive, SourceChanged,
    RecoveryCaptureFailed, InsufficientStorage, Cancelled, FailedOriginalIntact, RecoveryRequired }
public enum JournalRestorePhase { Maintenance, Incoming, RecoveryCapture, RecoveryVerified, Prepared, OriginalMoved,
    IncomingInstalled, Verified, Committed, Rollback, IncomingQuarantined, OriginalRestored, Finished }

/// <summary>Local handoff locations are deliberately excluded from diagnostic rendering; never put this object in structured logs.</summary>
public sealed record JournalRestoreResult(JournalRestoreStatus Status, Guid OperationId, JournalRestorePhase Phase,
    string? RecoveryArchivePath = null, string? RetainedDirectoryPath = null, bool CleanupFailed = false,
    BackupValidationCode? ValidationFailure = null, DatabaseSnapshotStatus? SnapshotFailure = null)
{
    public bool Succeeded => Status == JournalRestoreStatus.Restored;
    public bool NormalUseBlocked => Status == JournalRestoreStatus.RecoveryRequired;
    public string Message => Status switch
    {
        JournalRestoreStatus.Restored => "Restore verified. Recovery archive and original dataset retained. Restart the application before use.",
        JournalRestoreStatus.RecoveredOriginal => "The original dataset was recovered and verified. Recovery files are retained.",
        JournalRestoreStatus.NothingToRecover => "No interrupted restore was found.",
        JournalRestoreStatus.Blocked => "Close all application instances and database connections before offline restore.",
        JournalRestoreStatus.RejectedArchive => "The selected backup failed current-schema validation. No dataset was replaced.",
        JournalRestoreStatus.SourceChanged => "The backup changed since confirmation. Run preflight again before restoring.",
        JournalRestoreStatus.RecoveryCaptureFailed => "The current journal could not be safely backed up and verified. No dataset was replaced.",
        JournalRestoreStatus.InsufficientStorage => "Insufficient local staging space. Free space before retrying; no dataset was replaced.",
        JournalRestoreStatus.Cancelled => "Restore cancelled before replacement. The original dataset is intact.",
        JournalRestoreStatus.RecoveryRequired => "Restore recovery is required before normal startup. Preserve the retained recovery files and run offline recovery.",
        _ => "Restore did not complete. The original dataset is intact; retained recovery files have not been deleted."
    };
    public override string ToString() => $"{Status}; operation={OperationId}; phase={Phase}; cleanupFailed={CleanupFailed}; validation={ValidationFailure}; snapshot={SnapshotFailure}";
}
