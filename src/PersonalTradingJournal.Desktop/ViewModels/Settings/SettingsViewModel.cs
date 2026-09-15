using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PersonalTradingJournal.Desktop.Settings;
using PersonalTradingJournal.Desktop.Theming;

namespace PersonalTradingJournal.Desktop.ViewModels.Settings;

public sealed class SettingsViewModel : ObservableObject
{
    private const string PersistenceError =
        "The theme was applied, but your preference could not be saved.";
    private readonly IDesktopSettingsStore _settingsStore;
    private readonly IThemeService _themeService;
    private readonly ILogger<SettingsViewModel> _logger;
    private AppTheme _selectedTheme;
    private bool _isSaving;
    private string? _saveErrorMessage;

    public SettingsViewModel(
        IThemeService themeService,
        IDesktopSettingsStore settingsStore,
        ILogger<SettingsViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(themeService);
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(logger);

        _themeService = themeService;
        _settingsStore = settingsStore;
        _logger = logger;
        _selectedTheme = themeService.CurrentTheme;
        AvailableThemes = Enum.GetValues<AppTheme>();
        ChangeThemeCommand = new AsyncRelayCommand<AppTheme>(
            ChangeThemeAsync,
            _ => !IsSaving);
    }

    public IReadOnlyList<AppTheme> AvailableThemes { get; }

    public AppTheme SelectedTheme
    {
        get => _selectedTheme;
        private set
        {
            if (SetProperty(ref _selectedTheme, value))
            {
                OnPropertyChanged(nameof(IsDarkSelected));
                OnPropertyChanged(nameof(IsLightSelected));
            }
        }
    }

    public bool IsDarkSelected => SelectedTheme == AppTheme.Dark;

    public bool IsLightSelected => SelectedTheme == AppTheme.Light;

    public bool IsSaving
    {
        get => _isSaving;
        private set
        {
            if (SetProperty(ref _isSaving, value))
            {
                ChangeThemeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string? SaveErrorMessage
    {
        get => _saveErrorMessage;
        private set
        {
            if (SetProperty(ref _saveErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasSaveError));
            }
        }
    }

    public bool HasSaveError => !string.IsNullOrWhiteSpace(SaveErrorMessage);

    public IAsyncRelayCommand<AppTheme> ChangeThemeCommand { get; }

    private async Task ChangeThemeAsync(AppTheme theme, CancellationToken cancellationToken)
    {
        if (theme == SelectedTheme)
        {
            return;
        }

        _themeService.ApplyTheme(theme);
        SelectedTheme = theme;
        SaveErrorMessage = null;
        IsSaving = true;

        try
        {
            await _settingsStore.SaveAsync(new DesktopSettings(theme), cancellationToken);
            _logger.LogInformation("Theme preference saved: {Theme}", theme);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            SaveErrorMessage = PersistenceError;
            _logger.LogError(exception, "Theme preference could not be saved: {Theme}", theme);
        }
        finally
        {
            IsSaving = false;
        }
    }
}
