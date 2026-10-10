using System.Collections.Immutable;

namespace PersonalTradingJournal.Application.Backups;

/// <summary>Inspection only. Staging is always cleaned; a result is never authorization to replace installed data.</summary>
public interface IRestorePreflightService
{
    Task<RestorePreflightResult> InspectAsync(string archivePath, string stagingParentDirectory,
        CancellationToken cancellationToken = default);
}

public enum RestorePreflightPhase { Container, Manifest, Staging, Payloads, Database, Attachments, Summary }
public enum RestorePreflightWarning { UnencryptedSensitiveData, AiCredentialsExcluded, RevalidationRequired }

/// <summary>Counts and trusted version labels only. No names, content, local paths or source record IDs.</summary>
public sealed record RestorePreflightSummary(DateTimeOffset CreatedAtUtc, int ArchiveVersion, string LatestMigration,
    int MigrationCount, long AccountCount, long TradeCount, long JournalCount, long JournalRevisionCount,
    long AnalysisCount, long ScreenshotCount, int ScreenshotFileCount, long PayloadBytes, long ArchiveBytes,
    string ArchiveSha256, ImmutableArray<RestorePreflightWarning> Warnings);

public sealed record RestorePreflightResult(Guid OperationId, RestorePreflightPhase Phase,
    BackupValidationCode? Failure, BackupSchemaCompatibility Compatibility = BackupSchemaCompatibility.Unsupported,
    RestorePreflightSummary? Summary = null, bool CleanupFailed = false)
{
    public bool Validated => Failure is null && Summary is not null;
    public string? CleanupMessage => CleanupFailed ? "Private staging cleanup is incomplete. Sensitive temporary files may remain; no installed data was changed." : null;
    public string Message => Validated ? "Archive checked. Revalidate before any restore; no installed data was changed." :
        Compatibility == BackupSchemaCompatibility.RequiresStagedMigration ? "Older schema recognized. A supported staged upgrade is required; this preflight never migrates data." :
        Failure switch
        {
            BackupValidationCode.Cancelled => "Backup inspection cancelled; no installed data was changed.",
            BackupValidationCode.LimitExceeded => "The archive or validation work exceeds supported safety limits.",
            BackupValidationCode.UnsupportedArchiveVersion or BackupValidationCode.UnsupportedDatabaseSchema => "This backup version or database schema is not supported by this application.",
            BackupValidationCode.DatabaseAttachmentMismatch => "Screenshot references do not match the archived files. Select a complete backup.",
            BackupValidationCode.DatabaseIntegrityFailed => "The archived database failed integrity, relationship or schema checks.",
            BackupValidationCode.IoFailure => "The archive or private staging area could not be accessed. Check permissions and free space.",
            _ => "The archive is damaged, incomplete or unsafe. Select another complete backup."
        };
}
