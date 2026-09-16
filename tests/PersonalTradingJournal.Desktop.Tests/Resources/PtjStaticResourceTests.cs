using System.Text.RegularExpressions;

namespace PersonalTradingJournal.Desktop.Tests.Resources;

public sealed class PtjStaticResourceTests
{
    private static readonly Regex DefinitionPattern = new(
        "x:Key\\s*=\\s*\"(?<key>Ptj[^\"]+)\"",
        RegexOptions.CultureInvariant);

    private static readonly Regex ReferencePattern = new(
        "\\{StaticResource\\s+(?<key>Ptj[A-Za-z0-9_]+)",
        RegexOptions.CultureInvariant);

    private static readonly Regex DynamicReferencePattern = new(
        "\\{DynamicResource\\s+(?<key>Ptj[A-Za-z0-9_]+)",
        RegexOptions.CultureInvariant);

    private static readonly Regex NavigationIconPattern = new(
        "\"(?<key>PtjIcon[A-Za-z0-9_]+)\"",
        RegexOptions.CultureInvariant);

    [Fact]
    public void EveryPtjStaticResourceReferenceHasADefinition()
    {
        string repositoryRoot = FindRepositoryRoot();
        string desktopProjectDirectory = Path.Combine(
            repositoryRoot,
            "src",
            "PersonalTradingJournal.Desktop");
        string[] xamlFiles = Directory
            .EnumerateFiles(desktopProjectDirectory, "*.xaml", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedPath(path))
            .ToArray();

        HashSet<string> definitions = CollectKeys(xamlFiles, DefinitionPattern);
        HashSet<string> references = CollectKeys(xamlFiles, ReferencePattern);
        string[] missing = references
            .Except(definitions, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            missing.Length == 0,
            $"Undefined PTJ StaticResource key(s): {string.Join(", ", missing)}");
    }

    [Fact]
    public void EveryPtjDynamicResourceReferenceHasADefinition()
    {
        string repositoryRoot = FindRepositoryRoot();
        string desktopProjectDirectory = Path.Combine(
            repositoryRoot,
            "src",
            "PersonalTradingJournal.Desktop");
        string[] xamlFiles = Directory
            .EnumerateFiles(desktopProjectDirectory, "*.xaml", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedPath(path))
            .ToArray();

        HashSet<string> definitions = CollectKeys(xamlFiles, DefinitionPattern);
        HashSet<string> references = CollectKeys(xamlFiles, DynamicReferencePattern);
        string[] missing = references
            .Except(definitions, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            missing.Length == 0,
            $"Undefined PTJ DynamicResource key(s): {string.Join(", ", missing)}");
    }

    [Fact]
    public void EveryNavigationIconKeyHasAGeometryDefinition()
    {
        string repositoryRoot = FindRepositoryRoot();
        string navigationSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "PersonalTradingJournal.Desktop",
            "ViewModels",
            "MainWindowViewModel.cs"));
        string iconsXaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "PersonalTradingJournal.Desktop",
            "Resources",
            "Icons.xaml"));

        HashSet<string> iconKeys = NavigationIconPattern
            .Matches(navigationSource)
            .Select(match => match.Groups["key"].Value)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> definitions = DefinitionPattern
            .Matches(iconsXaml)
            .Select(match => match.Groups["key"].Value)
            .ToHashSet(StringComparer.Ordinal);
        string[] missing = iconKeys
            .Except(definitions, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(19, iconKeys.Count);
        Assert.True(
            missing.Length == 0,
            $"Undefined navigation icon key(s): {string.Join(", ", missing)}");
        Assert.Contains("PtjIconChevron", definitions);
    }

    private static HashSet<string> CollectKeys(IEnumerable<string> xamlFiles, Regex pattern)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (string xamlFile in xamlFiles)
        {
            string xaml = File.ReadAllText(xamlFile);
            foreach (Match match in pattern.Matches(xaml))
            {
                _ = keys.Add(match.Groups["key"].Value);
            }
        }

        return keys;
    }

    private static bool IsGeneratedPath(string path)
    {
        string relativePath = Path.GetRelativePath(FindRepositoryRoot(), path);
        char separator = Path.DirectorySeparatorChar;
        return relativePath.Contains($"{separator}bin{separator}", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains($"{separator}obj{separator}", StringComparison.OrdinalIgnoreCase);
    }

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
