using System.Windows;
using System.Windows.Threading;

namespace PersonalTradingJournal.Desktop.Theming;

public sealed class ThemeService : IThemeService, IDisposable
{
    private const string DarkThemeResource = "DarkTheme.xaml";
    private const string LightThemeResource = "LightTheme.xaml";
    private readonly ResourceDictionary _applicationResources;
    private readonly Action<Action> _dispatch;
    private readonly Dictionary<ResourceDictionary, AppTheme> _managedThemes = [];
    private readonly ISystemThemeProvider _systemThemeProvider;
    private readonly Func<AppTheme, ResourceDictionary> _themeDictionaryFactory;
    private bool _isDisposed;

    public ThemeService(
        ResourceDictionary applicationResources,
        ISystemThemeProvider systemThemeProvider)
        : this(
            applicationResources,
            systemThemeProvider,
            CreateThemeDictionary,
            DispatchToApplication)
    {
    }

    internal ThemeService(
        ResourceDictionary applicationResources,
        ISystemThemeProvider systemThemeProvider,
        Func<AppTheme, ResourceDictionary> themeDictionaryFactory,
        Action<Action> dispatch)
    {
        ArgumentNullException.ThrowIfNull(applicationResources);
        ArgumentNullException.ThrowIfNull(systemThemeProvider);
        ArgumentNullException.ThrowIfNull(themeDictionaryFactory);
        ArgumentNullException.ThrowIfNull(dispatch);

        _applicationResources = applicationResources;
        _systemThemeProvider = systemThemeProvider;
        _themeDictionaryFactory = themeDictionaryFactory;
        _dispatch = dispatch;
        PreferredTheme = AppTheme.System;
        EffectiveTheme = GetInitialEffectiveTheme();
        _systemThemeProvider.SystemThemeChanged += OnSystemThemeChanged;
        SetPreferredTheme(AppTheme.System);
    }

    public AppTheme PreferredTheme { get; private set; }

    public AppTheme EffectiveTheme { get; private set; }

    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    public void SetPreferredTheme(AppTheme theme)
    {
        if (!Enum.IsDefined(theme))
        {
            throw new ArgumentOutOfRangeException(nameof(theme), theme, "Unsupported application theme.");
        }

        AppTheme previousPreference = PreferredTheme;
        AppTheme previousEffectiveTheme = EffectiveTheme;
        PreferredTheme = theme;
        AppTheme concreteTheme = theme == AppTheme.System
            ? _systemThemeProvider.GetCurrentTheme()
            : theme;

        ApplyConcreteTheme(concreteTheme);
        if (previousPreference != PreferredTheme || previousEffectiveTheme != EffectiveTheme)
        {
            RaiseThemeChanged();
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _systemThemeProvider.SystemThemeChanged -= OnSystemThemeChanged;
        _isDisposed = true;
    }

    private AppTheme GetInitialEffectiveTheme()
    {
        ResourceDictionary? activeTheme = GetActiveThemeDictionaries().FirstOrDefault();
        return activeTheme is null ? AppTheme.Dark : GetTheme(activeTheme);
    }

    private void ApplyConcreteTheme(AppTheme theme)
    {
        if (theme is not AppTheme.Dark and not AppTheme.Light)
        {
            throw new InvalidOperationException("A concrete theme must be Dark or Light.");
        }

        List<ResourceDictionary> activeThemes = GetActiveThemeDictionaries().ToList();
        if (activeThemes.Count == 1 && GetTheme(activeThemes[0]) == theme)
        {
            EffectiveTheme = theme;
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
        EffectiveTheme = theme;
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

    private void OnSystemThemeChanged(object? sender, EventArgs e)
    {
        if (PreferredTheme != AppTheme.System)
        {
            return;
        }

        _dispatch(() =>
        {
            if (PreferredTheme != AppTheme.System)
            {
                return;
            }

            AppTheme previousEffectiveTheme = EffectiveTheme;
            ApplyConcreteTheme(_systemThemeProvider.GetCurrentTheme());
            if (previousEffectiveTheme != EffectiveTheme)
            {
                RaiseThemeChanged();
            }
        });
    }

    private void RaiseThemeChanged() => ThemeChanged?.Invoke(
        this,
        new ThemeChangedEventArgs(PreferredTheme, EffectiveTheme));

    private static void DispatchToApplication(Action action)
    {
        Dispatcher? dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _ = dispatcher.BeginInvoke(action);
    }

    private static Uri GetThemeResourceUri(AppTheme theme) => new(
        $"/PersonalTradingJournal.Desktop;component/Resources/Themes/{theme}Theme.xaml",
        UriKind.Relative);

    private static ResourceDictionary CreateThemeDictionary(AppTheme theme) => new()
    {
        Source = GetThemeResourceUri(theme),
    };
}
