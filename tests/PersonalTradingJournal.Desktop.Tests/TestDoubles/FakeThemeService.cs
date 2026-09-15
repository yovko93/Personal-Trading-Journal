using PersonalTradingJournal.Desktop.Theming;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

public sealed class FakeThemeService : IThemeService
{
    private EventHandler<ThemeChangedEventArgs>? _themeChanged;

    public FakeThemeService(
        AppTheme preferredTheme = AppTheme.System,
        AppTheme? effectiveTheme = null)
    {
        PreferredTheme = preferredTheme;
        EffectiveTheme = effectiveTheme
            ?? (preferredTheme == AppTheme.System ? AppTheme.Dark : preferredTheme);
        SystemTheme = preferredTheme == AppTheme.System
            ? EffectiveTheme
            : AppTheme.Dark;
    }

    public AppTheme PreferredTheme { get; private set; }

    public AppTheme EffectiveTheme { get; private set; }

    public AppTheme SystemTheme { get; private set; }

    public List<AppTheme> SetPreferences { get; } = [];

    public int SubscriberCount { get; private set; }

    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged
    {
        add
        {
            _themeChanged += value;
            SubscriberCount++;
        }
        remove
        {
            _themeChanged -= value;
            SubscriberCount--;
        }
    }

    public void SetPreferredTheme(AppTheme theme)
    {
        AppTheme previousPreference = PreferredTheme;
        AppTheme previousEffectiveTheme = EffectiveTheme;
        PreferredTheme = theme;
        EffectiveTheme = theme == AppTheme.System ? SystemTheme : theme;
        SetPreferences.Add(theme);

        if (previousPreference != PreferredTheme || previousEffectiveTheme != EffectiveTheme)
        {
            RaiseThemeChanged();
        }
    }

    public void SimulateSystemThemeChange(AppTheme theme)
    {
        SystemTheme = theme;
        if (PreferredTheme != AppTheme.System || EffectiveTheme == theme)
        {
            return;
        }

        EffectiveTheme = theme;
        RaiseThemeChanged();
    }

    private void RaiseThemeChanged() => _themeChanged?.Invoke(
        this,
        new ThemeChangedEventArgs(PreferredTheme, EffectiveTheme));
}
