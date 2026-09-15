namespace PersonalTradingJournal.Desktop.Theming;

public interface ISystemThemeProvider
{
    event EventHandler? SystemThemeChanged;

    AppTheme GetCurrentTheme();
}
