using PersonalTradingJournal.Desktop.Theming;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

public sealed class FakeSystemThemeProvider(AppTheme currentTheme) : ISystemThemeProvider
{
    private EventHandler? _systemThemeChanged;

    public AppTheme CurrentTheme { get; private set; } = currentTheme;

    public int SubscriberCount { get; private set; }

    public event EventHandler? SystemThemeChanged
    {
        add
        {
            _systemThemeChanged += value;
            SubscriberCount++;
        }
        remove
        {
            _systemThemeChanged -= value;
            SubscriberCount--;
        }
    }

    public AppTheme GetCurrentTheme() => CurrentTheme;

    public void SetTheme(AppTheme theme)
    {
        CurrentTheme = theme;
        _systemThemeChanged?.Invoke(this, EventArgs.Empty);
    }
}
