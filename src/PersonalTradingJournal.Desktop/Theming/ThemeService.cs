using System.Windows;

namespace PersonalTradingJournal.Desktop.Theming;

public sealed class ThemeService : IThemeService
{
    private const string DarkThemeResource = "DarkTheme.xaml";
    private const string LightThemeResource = "LightTheme.xaml";
    private readonly ResourceDictionary _applicationResources;
    private readonly Func<AppTheme, ResourceDictionary> _themeDictionaryFactory;
    private readonly Dictionary<ResourceDictionary, AppTheme> _managedThemes = [];

    public ThemeService(ResourceDictionary applicationResources)
        : this(applicationResources, CreateThemeDictionary)
    {
    }

    internal ThemeService(
        ResourceDictionary applicationResources,
        Func<AppTheme, ResourceDictionary> themeDictionaryFactory)
    {
        ArgumentNullException.ThrowIfNull(applicationResources);
        ArgumentNullException.ThrowIfNull(themeDictionaryFactory);

        _applicationResources = applicationResources;
        _themeDictionaryFactory = themeDictionaryFactory;
        ResourceDictionary[] activeThemes = GetActiveThemeDictionaries().ToArray();
        AppTheme initialTheme = activeThemes.Length == 0
            ? AppTheme.Dark
            : GetTheme(activeThemes[0]);

        if (activeThemes.Length == 1)
        {
            CurrentTheme = initialTheme;
        }
        else
        {
            ApplyTheme(initialTheme);
        }
    }

    public AppTheme CurrentTheme { get; private set; }

    public void ApplyTheme(AppTheme theme)
    {
        if (!Enum.IsDefined(theme))
        {
            throw new ArgumentOutOfRangeException(nameof(theme), theme, "Unsupported application theme.");
        }

        List<ResourceDictionary> activeThemes = GetActiveThemeDictionaries().ToList();
        if (activeThemes.Count == 1 && GetTheme(activeThemes[0]) == theme)
        {
            CurrentTheme = theme;
            return;
        }

        int insertionIndex = activeThemes.Count == 0
            ? 0
            : _applicationResources.MergedDictionaries.IndexOf(activeThemes[0]);

        foreach (ResourceDictionary activeTheme in activeThemes)
        {
            _ = _applicationResources.MergedDictionaries.Remove(activeTheme);
            _ = _managedThemes.Remove(activeTheme);
        }

        ResourceDictionary replacement = _themeDictionaryFactory(theme);
        _managedThemes.Add(replacement, theme);
        _applicationResources.MergedDictionaries.Insert(insertionIndex, replacement);
        CurrentTheme = theme;
    }

    private IEnumerable<ResourceDictionary> GetActiveThemeDictionaries() =>
        _applicationResources.MergedDictionaries.Where(IsThemeDictionary);

    private bool IsThemeDictionary(ResourceDictionary dictionary)
    {
        if (_managedThemes.ContainsKey(dictionary))
        {
            return true;
        }

        string? source = dictionary.Source?.OriginalString;
        return source is not null
            && (source.EndsWith(DarkThemeResource, StringComparison.OrdinalIgnoreCase)
                || source.EndsWith(LightThemeResource, StringComparison.OrdinalIgnoreCase));
    }

    private AppTheme GetTheme(ResourceDictionary dictionary)
    {
        if (_managedThemes.TryGetValue(dictionary, out AppTheme managedTheme))
        {
            return managedTheme;
        }

        string source = dictionary.Source?.OriginalString
            ?? throw new InvalidOperationException("The active theme dictionary has no source.");

        return source.EndsWith(DarkThemeResource, StringComparison.OrdinalIgnoreCase)
            ? AppTheme.Dark
            : AppTheme.Light;
    }

    private static Uri GetThemeResourceUri(AppTheme theme) => new(
        $"/PersonalTradingJournal.Desktop;component/Resources/Themes/{theme}Theme.xaml",
        UriKind.Relative);

    private static ResourceDictionary CreateThemeDictionary(AppTheme theme) => new()
    {
        Source = GetThemeResourceUri(theme),
    };
}
