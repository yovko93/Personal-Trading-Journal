namespace PersonalTradingJournal.Application.Backups;

/// <summary>Explicit, read-only logical export. The destination must be a new directory with an existing parent.</summary>
public interface IPortableExportService
{
    Task<PortableExportResult> CreateAsync(string destinationDirectory, CancellationToken cancellationToken = default);
}

public static class PortableExportContract
{
    public const string Format = "personal-trading-journal-logical-export";
    public const int Version = 1;
    public const string DataFile = "journal.json";
    public const string InventoryFile = "export-manifest.json";
    public const long MaximumOutputBytes = 32L * 1024 * 1024 * 1024;
    public const int MaximumCellBytes = 16 * 1024 * 1024;
    public const string ScreenshotContent = "metadata-only";
}

public enum PortableExportStatus { Success, Cancelled, DestinationUnavailable, SnapshotFailed, InvalidData, LimitExceeded, IoFailure }
public enum PortableExportPhase { Destination, Snapshot, Read, Write, Inventory, Cleanup, Publish }

/// <summary>Safe numeric/allowlisted diagnostics only; no paths, row content, credentials or exception messages.</summary>
public sealed record PortableExportResult(Guid OperationId, PortableExportStatus Status, PortableExportPhase Phase,
    long Rows = 0, long OutputBytes = 0, DatabaseSnapshotStatus? SnapshotFailure = null, bool CleanupFailed = false)
{
    public bool Succeeded => Status == PortableExportStatus.Success;
}
