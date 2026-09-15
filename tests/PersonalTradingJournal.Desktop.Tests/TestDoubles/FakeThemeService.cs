using PersonalTradingJournal.Desktop.Theming;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

public sealed class FakeThemeService : IThemeService
{
    public FakeThemeService(AppTheme currentTheme = AppTheme.Dark)
    {
        CurrentTheme = currentTheme;
    }

    public AppTheme CurrentTheme { get; private set; }

    public List<AppTheme> AppliedThemes { get; } = [];

    public void ApplyTheme(AppTheme theme)
    {
        AppliedThemes.Add(theme);
        CurrentTheme = theme;
    }
}
