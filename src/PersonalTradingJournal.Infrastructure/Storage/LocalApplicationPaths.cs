using PersonalTradingJournal.Application.Common.Storage;

namespace PersonalTradingJournal.Infrastructure.Storage;

public sealed class LocalApplicationPaths : IApplicationPaths
{
    private const string ApplicationDirectoryName = "PersonalTradingJournal";

    public LocalApplicationPaths()
        : this(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
    {
    }

    public LocalApplicationPaths(string localApplicationDataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localApplicationDataDirectory);

        DataDirectory = Path.Combine(localApplicationDataDirectory, ApplicationDirectoryName);
        DatabasePath = Path.Combine(DataDirectory, "journal.db");
        ScreenshotsDirectory = Path.Combine(DataDirectory, "screenshots");
        LogsDirectory = Path.Combine(DataDirectory, "logs");
        BackupsDirectory = Path.Combine(DataDirectory, "backups");
    }

    public string DataDirectory { get; }

    public string DatabasePath { get; }

    public string ScreenshotsDirectory { get; }

    public string LogsDirectory { get; }

    public string BackupsDirectory { get; }

    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(ScreenshotsDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(BackupsDirectory);
    }
}
