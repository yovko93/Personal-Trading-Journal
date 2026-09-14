using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Setups;

namespace PersonalTradingJournal.Desktop.ViewModels.Setups;

public sealed class TradingSetupsViewModel : ObservableObject
{
    private readonly ITradingSetupReader _reader;
    private readonly CreateTradingSetupUseCase _createUseCase;
    private readonly TradingSetupLifecycleUseCase _lifecycleUseCase;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private IReadOnlyList<TradingSetupListItem> _tradingSetups = [];
    private bool _hasLoadedSuccessfully;
    private bool _isLoading;
    private bool _isSaving;
    private bool _isChangingStatus;
    private bool _isCreateFormVisible;
    private string _nameText = string.Empty;
    private string _descriptionText = string.Empty;
    private string? _validationErrorMessage;
    private string? _saveErrorMessage;
    private string? _listErrorMessage;
    private string? _successMessage;

    public TradingSetupsViewModel(ITradingSetupReader reader,
        CreateTradingSetupUseCase createUseCase, TradingSetupLifecycleUseCase lifecycleUseCase)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(createUseCase);
        ArgumentNullException.ThrowIfNull(lifecycleUseCase);
        _reader = reader;
        _createUseCase = createUseCase;
        _lifecycleUseCase = lifecycleUseCase;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        ShowCreateCommand = new RelayCommand(ShowCreate, () => !IsCreateFormVisible && !IsBusy);
        CancelCreateCommand = new RelayCommand(CancelCreate, () => IsCreateFormVisible && !IsSaving);
        CreateCommand = new AsyncRelayCommand(CreateAsync, () => IsCreateFormVisible && !IsBusy);
        ToggleActiveCommand = new AsyncRelayCommand<TradingSetupListItem>(ToggleAsync,
            setup => setup is not null && !IsBusy && !IsCreateFormVisible);
    }

    public IReadOnlyList<TradingSetupListItem> TradingSetups
    {
        get => _tradingSetups;
        private set { if (SetProperty(ref _tradingSetups, value)) { OnPropertyChanged(nameof(HasTradingSetups)); ToggleActiveCommand.NotifyCanExecuteChanged(); } }
    }
    public bool HasTradingSetups => TradingSetups.Count > 0;
    public bool IsLoading { get => _isLoading; private set { if (SetProperty(ref _isLoading, value)) NotifyCommands(); } }
    public bool IsSaving { get => _isSaving; private set { if (SetProperty(ref _isSaving, value)) NotifyCommands(); } }
    public bool IsChangingStatus { get => _isChangingStatus; private set { if (SetProperty(ref _isChangingStatus, value)) NotifyCommands(); } }
    public bool IsCreateFormVisible { get => _isCreateFormVisible; private set { if (SetProperty(ref _isCreateFormVisible, value)) NotifyCommands(); } }
    public string NameText { get => _nameText; set => SetProperty(ref _nameText, value); }
    public string DescriptionText { get => _descriptionText; set => SetProperty(ref _descriptionText, value); }
    public string? ValidationErrorMessage { get => _validationErrorMessage; private set => SetMessage(ref _validationErrorMessage, value, nameof(HasValidationError)); }
    public bool HasValidationError => ValidationErrorMessage is not null;
    public string? SaveErrorMessage { get => _saveErrorMessage; private set => SetMessage(ref _saveErrorMessage, value, nameof(HasSaveError)); }
    public bool HasSaveError => SaveErrorMessage is not null;
    public string? ListErrorMessage { get => _listErrorMessage; private set => SetMessage(ref _listErrorMessage, value, nameof(HasListError)); }
    public bool HasListError => ListErrorMessage is not null;
    public string? SuccessMessage { get => _successMessage; private set => SetMessage(ref _successMessage, value, nameof(HasSuccessMessage)); }
    public bool HasSuccessMessage => SuccessMessage is not null;
    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand ShowCreateCommand { get; }
    public IRelayCommand CancelCreateCommand { get; }
    public IAsyncRelayCommand CreateCommand { get; }
    public IAsyncRelayCommand<TradingSetupListItem> ToggleActiveCommand { get; }

    public async Task EnsureLoadedAsync() =>
        _ = await LoadAsync(false, CancellationToken.None);

    private async Task RefreshAsync(CancellationToken token) => _ = await LoadAsync(true, token);

    private void ShowCreate()
    {
        ResetDraft();
        ValidationErrorMessage = SaveErrorMessage = SuccessMessage = null;
        IsCreateFormVisible = true;
    }

    private void CancelCreate()
    {
        ResetDraft();
        ValidationErrorMessage = SaveErrorMessage = null;
        IsCreateFormVisible = false;
    }

    private async Task CreateAsync(CancellationToken token)
    {
        ValidationErrorMessage = SaveErrorMessage = SuccessMessage = null;
        if (string.IsNullOrWhiteSpace(NameText))
        {
            ValidationErrorMessage = "Name is required.";
            return;
        }

        IsSaving = true;
        try
        {
            await _createUseCase.ExecuteAsync(new CreateTradingSetupCommand(NameText, DescriptionText), token);
            SuccessMessage = "Trading setup created.";
            ResetDraft();
            IsCreateFormVisible = false;
            _ = await LoadAsync(true, CancellationToken.None);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (InvalidOperationException ex) when (ex.Message == CreateTradingSetupUseCase.DuplicateNameMessage)
        { ValidationErrorMessage = CreateTradingSetupUseCase.DuplicateNameMessage; }
        catch (ArgumentException) { ValidationErrorMessage = "Please check the trading setup details."; }
        catch (Exception) { SaveErrorMessage = "Trading setup could not be created."; }
        finally { IsSaving = false; }
    }

    private async Task ToggleAsync(TradingSetupListItem? setup, CancellationToken token)
    {
        if (setup is null) return;
        ValidationErrorMessage = SaveErrorMessage = SuccessMessage = null;
        IsChangingStatus = true;
        try
        {
            await _lifecycleUseCase.ExecuteAsync(
                new SetTradingSetupActiveStateCommand(setup.Id, !setup.IsActive), token);
            SuccessMessage = setup.IsActive ? "Trading setup deactivated." : "Trading setup activated.";
            _ = await LoadAsync(true, CancellationToken.None);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (KeyNotFoundException) { SaveErrorMessage = "The selected trading setup is no longer available."; }
        catch (Exception) { SaveErrorMessage = "Trading setup status could not be changed."; }
        finally { IsChangingStatus = false; }
    }

    private bool IsBusy => IsLoading || IsSaving || IsChangingStatus;
    private void ResetDraft() { NameText = string.Empty; DescriptionText = string.Empty; }
    private void SetMessage(ref string? field, string? value, string hasProperty)
    { if (SetProperty(ref field, value)) OnPropertyChanged(hasProperty); }
    private void NotifyCommands()
    {
        RefreshCommand.NotifyCanExecuteChanged(); ShowCreateCommand.NotifyCanExecuteChanged();
        CancelCreateCommand.NotifyCanExecuteChanged(); CreateCommand.NotifyCanExecuteChanged();
        ToggleActiveCommand.NotifyCanExecuteChanged();
    }

    private async Task<bool> LoadAsync(bool force, CancellationToken token)
    {
        if (!await _loadGate.WaitAsync(0, token)) return false;
        try
        {
            if (!force && _hasLoadedSuccessfully) return true;
            IsLoading = true; ListErrorMessage = null;
            try
            {
                TradingSetups = await _reader.GetAllAsync(token);
                _hasLoadedSuccessfully = true;
                return true;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception) { ListErrorMessage = "Trading setups could not be loaded."; return false; }
            finally { IsLoading = false; }
        }
        finally { _loadGate.Release(); }
    }
}
