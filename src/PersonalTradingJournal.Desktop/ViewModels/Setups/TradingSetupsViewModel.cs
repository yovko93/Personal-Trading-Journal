using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Desktop.Dialogs;

namespace PersonalTradingJournal.Desktop.ViewModels.Setups;

public sealed class TradingSetupsViewModel : ObservableObject
{
    private const string NotFoundMessage =
        "The selected trading setup is no longer available. The list was refreshed.";
    private const string DeleteBlockedMessage =
        "This setup is used by existing trades and cannot be deleted. " +
        "Deactivate it instead to preserve historical trade classification.";

    private readonly ITradingSetupReader _reader;
    private readonly CreateTradingSetupUseCase _createUseCase;
    private readonly TradingSetupLifecycleUseCase _lifecycleUseCase;
    private readonly GetTradingSetupDetailsUseCase _detailsUseCase;
    private readonly UpdateTradingSetupUseCase _updateUseCase;
    private readonly DeleteTradingSetupUseCase _deleteUseCase;
    private readonly IDialogService _dialogService;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private IReadOnlyList<TradingSetupListItem> _tradingSetups = [];
    private bool _hasLoadedSuccessfully;
    private bool _isLoading;
    private bool _isSaving;
    private bool _isChangingStatus;
    private bool _isLoadingDetails;
    private bool _isUpdating;
    private bool _isDeleting;
    private bool _isCreateFormVisible;
    private bool _isEditFormVisible;
    private bool _isNameInvalid;
    private bool _isEditNameInvalid;
    private string _nameText = string.Empty;
    private string _descriptionText = string.Empty;
    private string _editNameText = string.Empty;
    private string _editDescriptionText = string.Empty;
    private string? _validationErrorMessage;
    private string? _editErrorMessage;
    private string? _actionErrorMessage;
    private string? _saveErrorMessage;
    private string? _listErrorMessage;
    private string? _successMessage;
    private TradingSetupDetails? _selectedTradingSetup;

    public TradingSetupsViewModel(
        ITradingSetupReader reader,
        CreateTradingSetupUseCase createUseCase,
        TradingSetupLifecycleUseCase lifecycleUseCase,
        GetTradingSetupDetailsUseCase detailsUseCase,
        UpdateTradingSetupUseCase updateUseCase,
        DeleteTradingSetupUseCase deleteUseCase,
        IDialogService dialogService)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(createUseCase);
        ArgumentNullException.ThrowIfNull(lifecycleUseCase);
        ArgumentNullException.ThrowIfNull(detailsUseCase);
        ArgumentNullException.ThrowIfNull(updateUseCase);
        ArgumentNullException.ThrowIfNull(deleteUseCase);
        ArgumentNullException.ThrowIfNull(dialogService);
        _reader = reader;
        _createUseCase = createUseCase;
        _lifecycleUseCase = lifecycleUseCase;
        _detailsUseCase = detailsUseCase;
        _updateUseCase = updateUseCase;
        _deleteUseCase = deleteUseCase;
        _dialogService = dialogService;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        ShowCreateCommand = new RelayCommand(
            ShowCreate,
            () => !IsCreateFormVisible && !IsEditFormVisible && !IsBusy);
        CancelCreateCommand = new RelayCommand(
            CancelCreate,
            () => IsCreateFormVisible && !IsSaving);
        CreateCommand = new AsyncRelayCommand(
            CreateAsync,
            () => IsCreateFormVisible && !IsBusy && !string.IsNullOrWhiteSpace(NameText));
        ToggleActiveCommand = new AsyncRelayCommand<TradingSetupListItem>(
            ToggleAsync,
            setup => setup is not null && !IsBusy && !IsCreateFormVisible && !IsEditFormVisible);
        ViewCommand = new AsyncRelayCommand<Guid>(ViewAsync, CanUseRowAction);
        EditCommand = new AsyncRelayCommand<Guid>(EditAsync, CanUseRowAction);
        CloseDetailsCommand = new RelayCommand(
            CloseDetails,
            () => SelectedTradingSetup is not null && !IsBusy && !IsEditFormVisible);
        CancelEditCommand = new RelayCommand(
            CancelEdit,
            () => IsEditFormVisible && !IsUpdating);
        SaveChangesCommand = new AsyncRelayCommand(
            SaveChangesAsync,
            () => IsEditFormVisible && SelectedTradingSetup is not null && !IsBusy);
        DeleteCommand = new AsyncRelayCommand<Guid>(DeleteAsync, CanUseRowAction);
    }

    public IReadOnlyList<TradingSetupListItem> TradingSetups
    {
        get => _tradingSetups;
        private set
        {
            if (SetProperty(ref _tradingSetups, value))
            {
                OnPropertyChanged(nameof(HasTradingSetups));
                OnPropertyChanged(nameof(SelectedTradingSetupListItem));
                ToggleActiveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasTradingSetups => TradingSetups.Count > 0;
    public bool IsLoading { get => _isLoading; private set { if (SetProperty(ref _isLoading, value)) NotifyCommands(); } }
    public bool IsSaving { get => _isSaving; private set { if (SetProperty(ref _isSaving, value)) NotifyCommands(); } }
    public bool IsChangingStatus { get => _isChangingStatus; private set { if (SetProperty(ref _isChangingStatus, value)) NotifyCommands(); } }
    public bool IsLoadingDetails { get => _isLoadingDetails; private set { if (SetProperty(ref _isLoadingDetails, value)) NotifyCommands(); } }
    public bool IsUpdating { get => _isUpdating; private set { if (SetProperty(ref _isUpdating, value)) NotifyCommands(); } }
    public bool IsDeleting { get => _isDeleting; private set { if (SetProperty(ref _isDeleting, value)) NotifyCommands(); } }
    public bool IsCreateFormVisible { get => _isCreateFormVisible; private set { if (SetProperty(ref _isCreateFormVisible, value)) NotifyCommands(); } }
    public bool IsEditFormVisible { get => _isEditFormVisible; private set { if (SetProperty(ref _isEditFormVisible, value)) NotifyCommands(); } }

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

    public string DescriptionText { get => _descriptionText; set => SetProperty(ref _descriptionText, value); }
    public bool IsNameInvalid { get => _isNameInvalid; private set => SetProperty(ref _isNameInvalid, value); }

    public string EditNameText
    {
        get => _editNameText;
        set
        {
            if (SetProperty(ref _editNameText, value) && IsEditNameInvalid && !string.IsNullOrWhiteSpace(value))
            {
                IsEditNameInvalid = false;
                EditErrorMessage = null;
            }
        }
    }

    public string EditDescriptionText { get => _editDescriptionText; set => SetProperty(ref _editDescriptionText, value); }
    public bool IsEditNameInvalid { get => _isEditNameInvalid; private set => SetProperty(ref _isEditNameInvalid, value); }

    public TradingSetupDetails? SelectedTradingSetup
    {
        get => _selectedTradingSetup;
        private set
        {
            if (SetProperty(ref _selectedTradingSetup, value))
            {
                OnPropertyChanged(nameof(HasSelectedTradingSetup));
                OnPropertyChanged(nameof(SelectedTradingSetupListItem));
                NotifyCommands();
            }
        }
    }

    public bool HasSelectedTradingSetup => SelectedTradingSetup is not null;
    public TradingSetupListItem? SelectedTradingSetupListItem => SelectedTradingSetup is null
        ? null
        : TradingSetups.FirstOrDefault(item => item.Id == SelectedTradingSetup.Id);

    public string? ValidationErrorMessage { get => _validationErrorMessage; private set => SetMessage(ref _validationErrorMessage, value, nameof(HasValidationError)); }
    public bool HasValidationError => ValidationErrorMessage is not null;
    public string? EditErrorMessage { get => _editErrorMessage; private set => SetMessage(ref _editErrorMessage, value, nameof(HasEditError)); }
    public bool HasEditError => EditErrorMessage is not null;
    public string? ActionErrorMessage { get => _actionErrorMessage; private set => SetMessage(ref _actionErrorMessage, value, nameof(HasActionError)); }
    public bool HasActionError => ActionErrorMessage is not null;
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
    public IAsyncRelayCommand<Guid> ViewCommand { get; }
    public IAsyncRelayCommand<Guid> EditCommand { get; }
    public IRelayCommand CloseDetailsCommand { get; }
    public IRelayCommand CancelEditCommand { get; }
    public IAsyncRelayCommand SaveChangesCommand { get; }
    public IAsyncRelayCommand<Guid> DeleteCommand { get; }

    public async Task EnsureLoadedAsync() => _ = await LoadAsync(false, CancellationToken.None);

    public void ResetTransientState()
    {
        ViewCommand.Cancel();
        EditCommand.Cancel();

        ResetDraft();
        IsCreateFormVisible = false;

        EditNameText = string.Empty;
        EditDescriptionText = string.Empty;
        IsEditNameInvalid = false;
        EditErrorMessage = null;
        IsEditFormVisible = false;

        SelectedTradingSetup = null;
        ValidationErrorMessage = null;
        ActionErrorMessage = null;
        SaveErrorMessage = null;
        SuccessMessage = null;
    }

    private async Task RefreshAsync(CancellationToken token) => _ = await LoadAsync(true, token);

    private void ShowCreate()
    {
        CloseDetails();
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
            IsNameInvalid = true;
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
            _ = await LoadAsync(true, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (InvalidOperationException exception) when (exception.Message == CreateTradingSetupUseCase.DuplicateNameMessage)
        {
            IsNameInvalid = true;
            ValidationErrorMessage = CreateTradingSetupUseCase.DuplicateNameMessage;
        }
        catch (ArgumentException) { ValidationErrorMessage = "Please check the trading setup details."; }
        catch (Exception) { SaveErrorMessage = "Trading setup could not be created."; }
        finally { IsSaving = false; }
    }

    private bool CanUseRowAction(Guid setupId) =>
        setupId != Guid.Empty && !IsBusy && !IsCreateFormVisible && !IsEditFormVisible;

    private async Task ViewAsync(Guid setupId, CancellationToken token) =>
        _ = await LoadDetailsAsync(setupId, token);

    private async Task EditAsync(Guid setupId, CancellationToken token)
    {
        TradingSetupDetails? setup = await LoadDetailsAsync(setupId, token);
        if (setup is null) return;
        EditNameText = setup.Name;
        EditDescriptionText = setup.Description ?? string.Empty;
        IsEditNameInvalid = false;
        EditErrorMessage = null;
        IsEditFormVisible = true;
    }

    private async Task<TradingSetupDetails?> LoadDetailsAsync(Guid setupId, CancellationToken token)
    {
        ActionErrorMessage = null;
        IsLoadingDetails = true;
        try
        {
            TradingSetupDetails? setup = await _detailsUseCase.ExecuteAsync(setupId, token);
            if (setup is null)
            {
                if (SelectedTradingSetup?.Id == setupId) SelectedTradingSetup = null;
                ActionErrorMessage = NotFoundMessage;
                _ = await LoadAsync(true, token);
                return null;
            }

            SelectedTradingSetup = setup;
            return setup;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            ActionErrorMessage = "Trading setup details could not be loaded.";
            return null;
        }
        finally { IsLoadingDetails = false; }
    }

    private void CloseDetails()
    {
        if (IsEditFormVisible) return;
        SelectedTradingSetup = null;
        ActionErrorMessage = null;
    }

    private void CancelEdit()
    {
        IsEditNameInvalid = false;
        EditErrorMessage = null;
        IsEditFormVisible = false;
    }

    private async Task SaveChangesAsync(CancellationToken token)
    {
        EditErrorMessage = null;
        if (SelectedTradingSetup is null) return;
        if (string.IsNullOrWhiteSpace(EditNameText))
        {
            IsEditNameInvalid = true;
            EditErrorMessage = "Name is required.";
            return;
        }

        IsUpdating = true;
        try
        {
            UpdateTradingSetupResult result = await _updateUseCase.ExecuteAsync(
                new UpdateTradingSetupCommand(SelectedTradingSetup.Id, EditNameText, EditDescriptionText), token);
            SelectedTradingSetup = result.TradingSetup;
            ReplaceListItem(result.TradingSetup);
            IsEditNameInvalid = false;
            IsEditFormVisible = false;
            SuccessMessage = result.WasChanged ? "Trading setup updated." : null;
            if (result.WasChanged) _ = await LoadAsync(true, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (KeyNotFoundException)
        {
            IsEditFormVisible = false;
            SelectedTradingSetup = null;
            ActionErrorMessage = NotFoundMessage;
            _ = await LoadAsync(true, token);
        }
        catch (InvalidOperationException exception) when (exception.Message == UpdateTradingSetupUseCase.DuplicateNameMessage)
        {
            IsEditNameInvalid = true;
            EditErrorMessage = UpdateTradingSetupUseCase.DuplicateNameMessage;
        }
        catch (ArgumentException) { EditErrorMessage = "Please check the trading setup details."; }
        catch (Exception) { EditErrorMessage = "Trading setup changes could not be saved."; }
        finally { IsUpdating = false; }
    }

    private async Task DeleteAsync(Guid setupId, CancellationToken token)
    {
        TradingSetupListItem? row = TradingSetups.FirstOrDefault(item => item.Id == setupId);
        string name = row?.Name ?? SelectedTradingSetup?.Name ?? "this setup";
        bool confirmed = _dialogService.Confirm(new ConfirmationDialogRequest(
            "Delete trading setup?",
            $"Delete \"{name}\"? This action cannot be undone.",
            "Delete Setup",
            isDestructive: true));
        if (!confirmed) return;

        ActionErrorMessage = null;
        IsDeleting = true;
        try
        {
            DeleteTradingSetupResult result = await _deleteUseCase.ExecuteAsync(setupId, token);
            if (result == DeleteTradingSetupResult.Referenced)
            {
                _dialogService.ShowInformation(new InformationDialogRequest(
                    "Cannot delete trading setup",
                    DeleteBlockedMessage));
                return;
            }

            TradingSetups = TradingSetups.Where(item => item.Id != setupId).ToArray();
            if (SelectedTradingSetup?.Id == setupId)
            {
                IsEditFormVisible = false;
                SelectedTradingSetup = null;
            }

            SuccessMessage = "Trading setup deleted.";
            _ = await LoadAsync(true, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (KeyNotFoundException)
        {
            TradingSetups = TradingSetups.Where(item => item.Id != setupId).ToArray();
            if (SelectedTradingSetup?.Id == setupId)
            {
                IsEditFormVisible = false;
                SelectedTradingSetup = null;
            }
            ActionErrorMessage = NotFoundMessage;
            _ = await LoadAsync(true, token);
        }
        catch (Exception) { ActionErrorMessage = "Trading setup could not be deleted."; }
        finally { IsDeleting = false; }
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
            _ = await LoadAsync(true, token);
            if (SelectedTradingSetup?.Id == setup.Id) _ = await LoadDetailsAsync(setup.Id, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (KeyNotFoundException)
        {
            SaveErrorMessage = "The selected trading setup is no longer available.";
            if (SelectedTradingSetup?.Id == setup.Id) SelectedTradingSetup = null;
            _ = await LoadAsync(true, token);
        }
        catch (Exception) { SaveErrorMessage = "Trading setup status could not be changed."; }
        finally { IsChangingStatus = false; }
    }

    private void ReplaceListItem(TradingSetupDetails setup)
    {
        var replacement = new TradingSetupListItem(
            setup.Id, setup.Name, setup.Description, setup.IsActive,
            setup.CreatedAtUtc, setup.UpdatedAtUtc);
        TradingSetups = TradingSetups.Select(item => item.Id == setup.Id ? replacement : item).ToArray();
    }

    private bool IsBusy => IsLoading || IsSaving || IsChangingStatus || IsLoadingDetails || IsUpdating || IsDeleting;

    private void ResetDraft()
    {
        NameText = string.Empty;
        DescriptionText = string.Empty;
        IsNameInvalid = false;
    }

    private void SetMessage(ref string? field, string? value, string hasProperty)
    {
        if (SetProperty(ref field, value)) OnPropertyChanged(hasProperty);
    }

    private void NotifyCommands()
    {
        RefreshCommand.NotifyCanExecuteChanged(); ShowCreateCommand.NotifyCanExecuteChanged();
        CancelCreateCommand.NotifyCanExecuteChanged(); CreateCommand.NotifyCanExecuteChanged();
        ToggleActiveCommand.NotifyCanExecuteChanged(); ViewCommand.NotifyCanExecuteChanged();
        EditCommand.NotifyCanExecuteChanged(); CloseDetailsCommand.NotifyCanExecuteChanged();
        CancelEditCommand.NotifyCanExecuteChanged(); SaveChangesCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
    }

    private async Task<bool> LoadAsync(bool force, CancellationToken token)
    {
        if (!await _loadGate.WaitAsync(0, token)) return false;
        try
        {
            if (!force && _hasLoadedSuccessfully) return true;
            IsLoading = true;
            ListErrorMessage = null;
            try
            {
                IReadOnlyList<TradingSetupListItem> setups = await _reader.GetAllAsync(token);
                TradingSetups = setups;
                if (SelectedTradingSetup is not null && setups.All(item => item.Id != SelectedTradingSetup.Id))
                {
                    IsEditFormVisible = false;
                    SelectedTradingSetup = null;
                }
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
