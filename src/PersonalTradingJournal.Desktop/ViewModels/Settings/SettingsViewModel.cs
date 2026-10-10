using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PersonalTradingJournal.Desktop.Settings;
using PersonalTradingJournal.Desktop.Theming;
using PersonalTradingJournal.Application.DailyReview.Coaching;

namespace PersonalTradingJournal.Desktop.ViewModels.Settings;

public sealed class SettingsViewModel : ObservableObject, IDisposable
{
    private const string PersistenceError =
        "The theme was applied, but your preference could not be saved.";
    private readonly IDesktopSettingsStore _settingsStore;
    private readonly IThemeService _themeService;
    private readonly ILogger<SettingsViewModel> _logger;
    private AppTheme _selectedTheme;
    private bool _isSaving;
    private string? _saveErrorMessage;
    private readonly ProtectedCoachingCredentials? _credentials;
    private readonly CoachingConfiguration? _configuration;
    private ProtectedCoachingCredentials? ActiveStore => _configuration is null ? _credentials :
        SelectedProvider == CoachingProviderKind.Unavailable ? null : _configuration.StoreFor(SelectedProvider);
    public IReadOnlyList<CoachingProviderKind> AvailableProviders { get; } = [CoachingProviderKind.Groq, CoachingProviderKind.OpenAI];
    public CoachingProviderKind SelectedProvider
    {
        get => _configuration?.SelectedProvider ?? CoachingProviderKind.OpenAI;
        set
        {
            if (_configuration is null || value == SelectedProvider) return;
            CredentialMessage = _configuration.Select(value) ? "Provider selected. No request was sent; its own credential source is shown below."
                : "Provider choice could not be saved. Check local Settings storage; generation is not switched.";
            OnPropertyChanged(); OnPropertyChanged(nameof(ApiKeyLabel)); OnPropertyChanged(nameof(CredentialMessage));
            RefreshCredentialStatus();
        }
    }
    public string ApiKeyLabel => $"{SelectedProvider} API key";
    public CoachingCredentialSource CredentialSource { get; private set; }
    public string? CredentialMessage { get; private set; }
    public string CredentialStatus => SelectedProvider == CoachingProviderKind.Unavailable
        ? "Provider preference cannot be read. Select Groq or OpenAI explicitly to recover; no fallback request will be sent."
        : CredentialSource switch
    {
        CoachingCredentialSource.Saved => "Configured · source: saved Settings key (this Windows user).",
        CoachingCredentialSource.Environment => $"Configured · source: {(SelectedProvider == CoachingProviderKind.Groq ? "GROQ_API_KEY" : "OPENAI_API_KEY")} environment fallback. No saved key.",
        CoachingCredentialSource.Unreadable => "Saved key cannot be read. Replace or remove it in Settings. Environment fallback is not used while saved storage is unreadable.",
        _ => "Not configured · no saved key or environment fallback.",
    };
    public bool CanRemoveKey => CredentialSource is CoachingCredentialSource.Saved or CoachingCredentialSource.Unreadable;
    public IRelayCommand RemoveKeyCommand { get; }

    public void RefreshCredentialStatus()
    {
        CredentialSource = _configuration?.GetSource() ?? _credentials?.GetSource() ?? CoachingCredentialSource.None;
        OnPropertyChanged(nameof(CredentialSource)); OnPropertyChanged(nameof(CredentialStatus));
        OnPropertyChanged(nameof(CanRemoveKey)); RemoveKeyCommand.NotifyCanExecuteChanged();
    }

    // The PasswordBox hands over only the new input; no stored key is ever returned to the view.
    public void SaveKey(string key)
    {
        CredentialMessage = ActiveStore?.Save(key) == true
            ? $"Key saved locally for {SelectedProvider}. It will be used on your next explicit generation; it has not been tested with the provider."
            : "Key was not saved. Enter a nonempty key without spaces (up to 4096 characters), or check local storage access.";
        OnPropertyChanged(nameof(CredentialMessage)); RefreshCredentialStatus();
    }

    private void RemoveKey()
    {
        CredentialMessage = ActiveStore?.Remove() == true
            ? $"Saved key removed for {SelectedProvider}. The active source is shown below. This does not revoke the key at the provider."
            : "The saved key could not be removed. Check local storage access and try again.";
        OnPropertyChanged(nameof(CredentialMessage)); RefreshCredentialStatus();
    }

    public SettingsViewModel(
        IThemeService themeService,
        IDesktopSettingsStore settingsStore,
        ILogger<SettingsViewModel> logger,
        ProtectedCoachingCredentials? credentials = null, CoachingConfiguration? configuration = null,
        DataBackupsViewModel? dataBackups = null)
    {
        ArgumentNullException.ThrowIfNull(themeService);
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(logger);

        _themeService = themeService;
        _settingsStore = settingsStore;
        _logger = logger;
        _credentials = credentials;
        _configuration = configuration;
        DataBackups = dataBackups;
        RemoveKeyCommand = new RelayCommand(RemoveKey, () => CanRemoveKey);
        RefreshCredentialStatus();
        _selectedTheme = themeService.PreferredTheme;
        AvailableThemes = Enum.GetValues<AppTheme>();
        ChangeThemeCommand = new AsyncRelayCommand<AppTheme>(
            ChangeThemeAsync,
            _ => !IsSaving);
        _themeService.ThemeChanged += OnThemeChanged;
    }

    public IReadOnlyList<AppTheme> AvailableThemes { get; }
    public DataBackupsViewModel? DataBackups { get; }

    public AppTheme SelectedTheme
    {
        get => _selectedTheme;
        private set
        {
            if (SetProperty(ref _selectedTheme, value))
            {
                OnPropertyChanged(nameof(IsDarkSelected));
                OnPropertyChanged(nameof(IsLightSelected));
                OnPropertyChanged(nameof(IsSystemSelected));
            }
        }
    }

    public bool IsDarkSelected => SelectedTheme == AppTheme.Dark;

    public bool IsLightSelected => SelectedTheme == AppTheme.Light;

    public bool IsSystemSelected => SelectedTheme == AppTheme.System;

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

    public void Dispose() => _themeService.ThemeChanged -= OnThemeChanged;

    private async Task ChangeThemeAsync(AppTheme theme, CancellationToken cancellationToken)
    {
        if (theme == SelectedTheme)
        {
            return;
        }

        _themeService.SetPreferredTheme(theme);
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

    private void OnThemeChanged(object? sender, ThemeChangedEventArgs e) =>
        SelectedTheme = e.PreferredTheme;
}
