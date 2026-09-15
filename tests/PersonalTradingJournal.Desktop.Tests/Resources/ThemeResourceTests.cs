using System.Text.RegularExpressions;

namespace PersonalTradingJournal.Desktop.Tests.Resources;

public sealed class ThemeResourceTests
{
    private static readonly Regex KeyPattern = new(
        "x:Key\\s*=\\s*\"(?<key>Ptj(?:Color)?[A-Za-z0-9_]+)\"",
        RegexOptions.CultureInvariant);
    private static readonly Regex DynamicReferencePattern = new(
        "\\{DynamicResource\\s+(?<key>Ptj[A-Za-z0-9_]+)",
        RegexOptions.CultureInvariant);

    [Fact]
    public void DarkAndLightThemesHaveIdenticalProjectOwnedKeySets()
    {
        (string darkPath, string lightPath) = GetThemePaths();

        HashSet<string> darkKeys = CollectKeys(darkPath, KeyPattern);
        HashSet<string> lightKeys = CollectKeys(lightPath, KeyPattern);

        Assert.True(
            darkKeys.SetEquals(lightKeys),
            $"Theme key mismatch. Dark-only: {string.Join(", ", darkKeys.Except(lightKeys))}; " +
            $"Light-only: {string.Join(", ", lightKeys.Except(darkKeys))}");
    }

    [Fact]
    public void EveryDynamicThemeResourceExistsInBothThemes()
    {
        string desktopDirectory = GetDesktopProjectDirectory();
        string[] xamlFiles = Directory
            .EnumerateFiles(desktopDirectory, "*.xaml", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedPath(path))
            .ToArray();
        HashSet<string> references = new(StringComparer.Ordinal);
        foreach (string path in xamlFiles)
        {
            references.UnionWith(CollectKeys(path, DynamicReferencePattern));
        }

        (string darkPath, string lightPath) = GetThemePaths();
        HashSet<string> darkKeys = CollectKeys(darkPath, KeyPattern);
        HashSet<string> lightKeys = CollectKeys(lightPath, KeyPattern);
        string[] missing = references
            .Where(key => !darkKeys.Contains(key) || !lightKeys.Contains(key))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            missing.Length == 0,
            $"Dynamic theme key(s) missing from one or both themes: {string.Join(", ", missing)}");
    }

    private static (string Dark, string Light) GetThemePaths()
    {
        string themesDirectory = Path.Combine(
            GetDesktopProjectDirectory(),
            "Resources",
            "Themes");
        return (
            Path.Combine(themesDirectory, "DarkTheme.xaml"),
            Path.Combine(themesDirectory, "LightTheme.xaml"));
    }

    private static HashSet<string> CollectKeys(string path, Regex pattern) =>
        pattern.Matches(File.ReadAllText(path))
            .Select(match => match.Groups["key"].Value)
            .ToHashSet(StringComparer.Ordinal);

    private static bool IsGeneratedPath(string path)
    {
        string relativePath = Path.GetRelativePath(FindRepositoryRoot(), path);
        char separator = Path.DirectorySeparatorChar;
        return relativePath.Contains($"{separator}bin{separator}", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains($"{separator}obj{separator}", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetDesktopProjectDirectory() => Path.Combine(
        FindRepositoryRoot(),
        "src",
        "PersonalTradingJournal.Desktop");

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PersonalTradingJournal.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
