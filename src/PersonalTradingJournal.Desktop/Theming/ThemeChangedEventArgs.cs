namespace PersonalTradingJournal.Desktop.Theming;

public sealed class ThemeChangedEventArgs(
    AppTheme preferredTheme,
    AppTheme effectiveTheme) : EventArgs
{
    public AppTheme PreferredTheme { get; } = preferredTheme;

    public AppTheme EffectiveTheme { get; } = effectiveTheme;
}
