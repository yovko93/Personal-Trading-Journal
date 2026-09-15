namespace PersonalTradingJournal.Desktop.Theming;

public interface IThemeService
{
    AppTheme CurrentTheme { get; }

    void ApplyTheme(AppTheme theme);
}
