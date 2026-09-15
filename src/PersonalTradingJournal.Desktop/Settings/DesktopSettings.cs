using PersonalTradingJournal.Desktop.Theming;

namespace PersonalTradingJournal.Desktop.Settings;

public sealed record DesktopSettings(AppTheme Theme)
{
    public static DesktopSettings Default { get; } = new(AppTheme.System);
}
