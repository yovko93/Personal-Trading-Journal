using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Mistakes;

namespace PersonalTradingJournal.Desktop.ViewModels.Mistakes;

public sealed class TradingMistakesViewModel : ObservableObject
{
    private readonly ITradingMistakeReader _reader;
    private readonly CreateTradingMistakeUseCase _create;
    private readonly TradingMistakeLifecycleUseCase _lifecycle;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private IReadOnlyList<TradingMistakeListItem> _tradingMistakes = [];
    private bool _loaded, _isLoading, _isSaving, _isChangingStatus, _isCreateFormVisible;
    private bool _isNameInvalid;
    private string _nameText = string.Empty, _descriptionText = string.Empty;
    private string? _validationErrorMessage, _saveErrorMessage, _listErrorMessage, _successMessage;

    public TradingMistakesViewModel(ITradingMistakeReader reader, CreateTradingMistakeUseCase create,
        TradingMistakeLifecycleUseCase lifecycle)
    {
        ArgumentNullException.ThrowIfNull(reader); ArgumentNullException.ThrowIfNull(create); ArgumentNullException.ThrowIfNull(lifecycle);
        _reader = reader; _create = create; _lifecycle = lifecycle;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        ShowCreateCommand = new RelayCommand(ShowCreate, () => !IsCreateFormVisible && !IsBusy);
        CancelCreateCommand = new RelayCommand(CancelCreate, () => IsCreateFormVisible && !IsSaving);
        CreateCommand = new AsyncRelayCommand(
            CreateAsync,
            () => IsCreateFormVisible && !IsBusy && !string.IsNullOrWhiteSpace(NameText));
        ToggleActiveCommand = new AsyncRelayCommand<TradingMistakeListItem>(ToggleAsync,
            item => item is not null && !IsBusy && !IsCreateFormVisible);
    }

    public IReadOnlyList<TradingMistakeListItem> TradingMistakes { get => _tradingMistakes; private set { if (SetProperty(ref _tradingMistakes, value)) { OnPropertyChanged(nameof(HasTradingMistakes)); ToggleActiveCommand.NotifyCanExecuteChanged(); } } }
    public bool HasTradingMistakes => TradingMistakes.Count > 0;
    public bool IsLoading { get => _isLoading; private set { if (SetProperty(ref _isLoading, value)) Notify(); } }
    public bool IsSaving { get => _isSaving; private set { if (SetProperty(ref _isSaving, value)) Notify(); } }
    public bool IsChangingStatus { get => _isChangingStatus; private set { if (SetProperty(ref _isChangingStatus, value)) Notify(); } }
    public bool IsCreateFormVisible { get => _isCreateFormVisible; private set { if (SetProperty(ref _isCreateFormVisible, value)) Notify(); } }
    public string NameText
    {
        get => _nameText;
        set
        {
            if (SetProperty(ref _nameText, value))
            {
                CreateCommand.NotifyCanExecuteChanged();
                if (IsNameInvalid && !string.IsNullOrWhiteSpace(value))
                {
                    IsNameInvalid = false;
                    ValidationErrorMessage = null;
                }
            }
        }
    }
    public bool IsNameInvalid { get => _isNameInvalid; private set => SetProperty(ref _isNameInvalid, value); }
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
    public IAsyncRelayCommand<TradingMistakeListItem> ToggleActiveCommand { get; }
    public async Task EnsureLoadedAsync() => _ = await LoadAsync(false, CancellationToken.None);
    private async Task RefreshAsync(CancellationToken token) => _ = await LoadAsync(true, token);

    private void ShowCreate() { Reset(); ValidationErrorMessage = SaveErrorMessage = SuccessMessage = null; IsCreateFormVisible = true; }
    private void CancelCreate() { Reset(); ValidationErrorMessage = SaveErrorMessage = null; IsCreateFormVisible = false; }
    private async Task CreateAsync(CancellationToken token)
    {
        ValidationErrorMessage = SaveErrorMessage = SuccessMessage = null;
        if (string.IsNullOrWhiteSpace(NameText)) { IsNameInvalid = true; ValidationErrorMessage = "Name is required."; return; }
        IsSaving = true;
        try
        {
            await _create.ExecuteAsync(new CreateTradingMistakeCommand(NameText, DescriptionText), token);
            SuccessMessage = "Trading mistake created."; Reset(); IsCreateFormVisible = false;
            _ = await LoadAsync(true, CancellationToken.None);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (InvalidOperationException ex) when (ex.Message == CreateTradingMistakeUseCase.DuplicateNameMessage)
        { IsNameInvalid = true; ValidationErrorMessage = CreateTradingMistakeUseCase.DuplicateNameMessage; }
        catch (ArgumentException) { ValidationErrorMessage = "Please check the trading mistake details."; }
        catch (Exception) { SaveErrorMessage = "Trading mistake could not be created."; }
        finally { IsSaving = false; }
    }

    private async Task ToggleAsync(TradingMistakeListItem? item, CancellationToken token)
    {
        if (item is null) return;
        ValidationErrorMessage = SaveErrorMessage = SuccessMessage = null; IsChangingStatus = true;
        try
        {
            await _lifecycle.ExecuteAsync(new SetTradingMistakeActiveStateCommand(item.Id, !item.IsActive), token);
            SuccessMessage = item.IsActive ? "Trading mistake deactivated." : "Trading mistake activated.";
            _ = await LoadAsync(true, CancellationToken.None);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (KeyNotFoundException) { SaveErrorMessage = "The selected trading mistake is no longer available."; }
        catch (Exception) { SaveErrorMessage = "Trading mistake status could not be changed."; }
        finally { IsChangingStatus = false; }
    }

    private bool IsBusy => IsLoading || IsSaving || IsChangingStatus;
    private void Reset() { NameText = string.Empty; DescriptionText = string.Empty; IsNameInvalid = false; }
    private void SetMessage(ref string? field, string? value, string property) { if (SetProperty(ref field, value)) OnPropertyChanged(property); }
    private void Notify() { RefreshCommand.NotifyCanExecuteChanged(); ShowCreateCommand.NotifyCanExecuteChanged(); CancelCreateCommand.NotifyCanExecuteChanged(); CreateCommand.NotifyCanExecuteChanged(); ToggleActiveCommand.NotifyCanExecuteChanged(); }
    private async Task<bool> LoadAsync(bool force, CancellationToken token)
    {
        if (!await _loadGate.WaitAsync(0, token)) return false;
        try
        {
            if (!force && _loaded) return true; IsLoading = true; ListErrorMessage = null;
            try { TradingMistakes = await _reader.GetAllAsync(token); _loaded = true; return true; }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception) { ListErrorMessage = "Trading mistakes could not be loaded."; return false; }
            finally { IsLoading = false; }
        }
        finally { _loadGate.Release(); }
    }
}
