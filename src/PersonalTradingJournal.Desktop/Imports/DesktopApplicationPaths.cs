using System.IO;
using PersonalTradingJournal.Infrastructure.Storage;

namespace PersonalTradingJournal.Desktop.Imports;

/// <summary>Explicit opt-in isolation for acceptance runs of the production executable.</summary>
public static class DesktopApplicationPaths
{
    public static LocalApplicationPaths FromArguments(string[] args)
    {
        if (args.Length == 0) return new LocalApplicationPaths();
        if (args.Length != 2 || args[0] != "--isolated-data-root" || !Path.IsPathFullyQualified(args[1]))
            throw new ArgumentException("Use --isolated-data-root followed by an absolute, separate acceptance-data directory.");
        string root = Path.GetFullPath(args[1]);
        string real = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        if (Path.TrimEndingDirectorySeparator(root).Equals(Path.TrimEndingDirectorySeparator(real), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The isolated data root must not be the real LocalAppData root.");
        return new LocalApplicationPaths(root);
    }
}
