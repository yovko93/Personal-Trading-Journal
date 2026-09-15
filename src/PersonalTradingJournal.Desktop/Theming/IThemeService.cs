namespace PersonalTradingJournal.Desktop.Theming;

public interface IThemeService
{
    AppTheme PreferredTheme { get; }

    AppTheme EffectiveTheme { get; }

    event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    void SetPreferredTheme(AppTheme theme);
}
