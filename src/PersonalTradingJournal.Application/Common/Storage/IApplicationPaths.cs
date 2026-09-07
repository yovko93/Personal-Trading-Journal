namespace PersonalTradingJournal.Application.Common.Storage;

public interface IApplicationPaths
{
    string DataDirectory { get; }

    string DatabasePath { get; }

    string ScreenshotsDirectory { get; }

    string LogsDirectory { get; }

    string BackupsDirectory { get; }
}
