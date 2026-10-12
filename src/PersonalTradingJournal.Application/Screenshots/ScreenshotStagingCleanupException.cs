namespace PersonalTradingJournal.Application.Screenshots;

/// <summary>No final file was published, but an operation-owned partial file could not be removed.</summary>
public sealed class ScreenshotStagingCleanupException() : IOException("Screenshot staging cleanup failed. An unused partial file may remain.");
