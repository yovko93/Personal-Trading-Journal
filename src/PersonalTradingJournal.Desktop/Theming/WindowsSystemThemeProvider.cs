using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace PersonalTradingJournal.Desktop.Theming;

public sealed class WindowsSystemThemeProvider : ISystemThemeProvider, IDisposable
{
    private const string PersonalizeRegistryPath =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValueName = "AppsUseLightTheme";
    private readonly Func<object?> _readThemeValue;
    private readonly ILogger<WindowsSystemThemeProvider> _logger;
    private AppTheme _lastKnownTheme;
    private bool _isSubscribed;

    public WindowsSystemThemeProvider(ILogger<WindowsSystemThemeProvider> logger)
        : this(ReadAppsUseLightThemeValue, logger, subscribeToSystemEvents: true)
    {
    }

    internal WindowsSystemThemeProvider(
        Func<object?> readThemeValue,
        ILogger<WindowsSystemThemeProvider> logger,
        bool subscribeToSystemEvents = false)
    {
        ArgumentNullException.ThrowIfNull(readThemeValue);
        ArgumentNullException.ThrowIfNull(logger);

        _readThemeValue = readThemeValue;
        _logger = logger;
        _lastKnownTheme = ReadCurrentTheme();

        if (subscribeToSystemEvents)
        {
            TrySubscribeToSystemEvents();
        }
    }

    public event EventHandler? SystemThemeChanged;

    public AppTheme GetCurrentTheme()
    {
        _lastKnownTheme = ReadCurrentTheme();
        return _lastKnownTheme;
    }

    public void Dispose()
    {
        if (!_isSubscribed)
        {
            return;
        }

        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _isSubscribed = false;
    }

    internal void RefreshForTesting()
    {
        AppTheme currentTheme = ReadCurrentTheme();
        if (currentTheme == _lastKnownTheme)
        {
            return;
        }

        _lastKnownTheme = currentTheme;
        SystemThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private static object? ReadAppsUseLightThemeValue()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PersonalizeRegistryPath);
        return key?.GetValue(AppsUseLightThemeValueName);
    }

    private AppTheme ReadCurrentTheme()
    {
        try
        {
            object? value = _readThemeValue();
            if (value is int integerValue)
            {
                return integerValue switch
                {
                    0 => AppTheme.Dark,
                    1 => AppTheme.Light,
                    _ => LogFallback("invalid"),
                };
            }

            return LogFallback(value is null ? "missing" : "invalid");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Windows application theme could not be read; using Dark fallback");
            return AppTheme.Dark;
        }
    }

    private AppTheme LogFallback(string reason)
    {
        _logger.LogWarning(
            "Windows application theme value is {Reason}; using Dark fallback",
            reason);
        return AppTheme.Dark;
    }

    private void TrySubscribeToSystemEvents()
    {
        try
        {
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            _isSubscribed = true;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Live Windows theme tracking is unavailable; startup detection remains active");
        }
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) =>
        RefreshForTesting();
}
