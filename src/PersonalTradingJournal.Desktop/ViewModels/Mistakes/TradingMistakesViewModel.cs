using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Mistakes;
using PersonalTradingJournal.Desktop.Dialogs;

namespace PersonalTradingJournal.Desktop.ViewModels.Mistakes;

public sealed class TradingMistakesViewModel : ObservableObject
{
    private const string NotFoundMessage =
        "The selected trading mistake is no longer available. The list was refreshed.";
    private const string DeleteBlockedMessage =
        "This mistake is assigned to existing trades and cannot be deleted. " +
        "Deactivate it instead to preserve historical trade review data.";

    private readonly ITradingMistakeReader _reader;
    private readonly CreateTradingMistakeUseCase _createUseCase;
    private readonly TradingMistakeLifecycleUseCase _lifecycleUseCase;
    private readonly GetTradingMistakeDetailsUseCase _detailsUseCase;
    private readonly UpdateTradingMistakeUseCase _updateUseCase;
    private readonly DeleteTradingMistakeUseCase _deleteUseCase;
    private readonly IDialogService _dialogService;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private IReadOnlyList<TradingMistakeListItem> _tradingMistakes = [];
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
    private TradingMistakeDetails? _selectedTradingMistake;

    public TradingMistakesViewModel(
        ITradingMistakeReader reader,
        CreateTradingMistakeUseCase createUseCase,
        TradingMistakeLifecycleUseCase lifecycleUseCase,
        GetTradingMistakeDetailsUseCase detailsUseCase,
        UpdateTradingMistakeUseCase updateUseCase,
        DeleteTradingMistakeUseCase deleteUseCase,
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
        ToggleActiveCommand = new AsyncRelayCommand<TradingMistakeListItem>(
            ToggleAsync,
            item => item is not null && !IsBusy && !IsCreateFormVisible && !IsEditFormVisible);
        ViewCommand = new AsyncRelayCommand<Guid>(ViewAsync, CanUseRowAction);
        EditCommand = new AsyncRelayCommand<Guid>(EditAsync, CanUseRowAction);
        CloseDetailsCommand = new RelayCommand(
            CloseDetails,
            () => SelectedTradingMistake is not null && !IsBusy && !IsEditFormVisible);
        CancelEditCommand = new RelayCommand(
            CancelEdit,
            () => IsEditFormVisible && !IsUpdating);
        SaveChangesCommand = new AsyncRelayCommand(
            SaveChangesAsync,
            () => IsEditFormVisible && SelectedTradingMistake is not null && !IsBusy);
        DeleteCommand = new AsyncRelayCommand<Guid>(DeleteAsync, CanUseRowAction);
    }

    public IReadOnlyList<TradingMistakeListItem> TradingMistakes
    {
        get => _tradingMistakes;
        private set
        {
            if (SetProperty(ref _tradingMistakes, value))
            {
                OnPropertyChanged(nameof(HasTradingMistakes));
                OnPropertyChanged(nameof(SelectedTradingMistakeListItem));
                ToggleActiveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasTradingMistakes => TradingMistakes.Count > 0;
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
            if (SetProperty(ref _editNameText, value) &&
                IsEditNameInvalid &&
                !string.IsNullOrWhiteSpace(value))
            {
                IsEditNameInvalid = false;
                EditErrorMessage = null;
            }
        }
    }

    public string EditDescriptionText { get => _editDescriptionText; set => SetProperty(ref _editDescriptionText, value); }
    public bool IsEditNameInvalid { get => _isEditNameInvalid; private set => SetProperty(ref _isEditNameInvalid, value); }

    public TradingMistakeDetails? SelectedTradingMistake
    {
        get => _selectedTradingMistake;
        private set
        {
            if (SetProperty(ref _selectedTradingMistake, value))
            {
                OnPropertyChanged(nameof(HasSelectedTradingMistake));
                OnPropertyChanged(nameof(SelectedTradingMistakeListItem));
                NotifyCommands();
            }
        }
    }

    public bool HasSelectedTradingMistake => SelectedTradingMistake is not null;
    public TradingMistakeListItem? SelectedTradingMistakeListItem =>
        SelectedTradingMistake is null
            ? null
            : TradingMistakes.FirstOrDefault(
                item => item.Id == SelectedTradingMistake.Id);

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
    public IAsyncRelayCommand<TradingMistakeListItem> ToggleActiveCommand { get; }
    public IAsyncRelayCommand<Guid> ViewCommand { get; }
    public IAsyncRelayCommand<Guid> EditCommand { get; }
    public IRelayCommand CloseDetailsCommand { get; }
    public IRelayCommand CancelEditCommand { get; }
    public IAsyncRelayCommand SaveChangesCommand { get; }
    public IAsyncRelayCommand<Guid> DeleteCommand { get; }

    public async Task EnsureLoadedAsync() =>
        _ = await LoadAsync(false, CancellationToken.None);

    private async Task RefreshAsync(CancellationToken cancellationToken) =>
        _ = await LoadAsync(true, cancellationToken);

    private void ShowCreate()
    {
        CloseDetails();
        ResetCreateDraft();
        ValidationErrorMessage = SaveErrorMessage = SuccessMessage = null;
        IsCreateFormVisible = true;
    }

    private void CancelCreate()
    {
        ResetCreateDraft();
        ValidationErrorMessage = SaveErrorMessage = null;
        IsCreateFormVisible = false;
    }

    private async Task CreateAsync(CancellationToken cancellationToken)
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
            await _createUseCase.ExecuteAsync(
                new CreateTradingMistakeCommand(NameText, DescriptionText),
                cancellationToken);
            SuccessMessage = "Trading mistake created.";
            ResetCreateDraft();
            IsCreateFormVisible = false;
            _ = await LoadAsync(true, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidOperationException exception)
            when (exception.Message == CreateTradingMistakeUseCase.DuplicateNameMessage)
        {
            IsNameInvalid = true;
            ValidationErrorMessage = CreateTradingMistakeUseCase.DuplicateNameMessage;
        }
        catch (ArgumentException)
        {
            ValidationErrorMessage = "Please check the trading mistake details.";
        }
        catch (Exception)
        {
            SaveErrorMessage = "Trading mistake could not be created.";
        }
        finally
        {
            IsSaving = false;
        }
    }

    private bool CanUseRowAction(Guid mistakeId) =>
        mistakeId != Guid.Empty &&
        !IsBusy &&
        !IsCreateFormVisible &&
        !IsEditFormVisible;

    private async Task ViewAsync(Guid mistakeId, CancellationToken cancellationToken) =>
        _ = await LoadDetailsAsync(mistakeId, cancellationToken);

    private async Task EditAsync(Guid mistakeId, CancellationToken cancellationToken)
    {
        TradingMistakeDetails? mistake = await LoadDetailsAsync(
            mistakeId,
            cancellationToken);
        if (mistake is null)
        {
            return;
        }

        EditNameText = mistake.Name;
        EditDescriptionText = mistake.Description ?? string.Empty;
        IsEditNameInvalid = false;
        EditErrorMessage = null;
        IsEditFormVisible = true;
    }

    private async Task<TradingMistakeDetails?> LoadDetailsAsync(
        Guid mistakeId,
        CancellationToken cancellationToken)
    {
        ActionErrorMessage = null;
        IsLoadingDetails = true;
        try
        {
            TradingMistakeDetails? mistake = await _detailsUseCase.ExecuteAsync(
                mistakeId,
                cancellationToken);
            if (mistake is null)
            {
                if (SelectedTradingMistake?.Id == mistakeId)
                {
                    SelectedTradingMistake = null;
                }

                ActionErrorMessage = NotFoundMessage;
                _ = await LoadAsync(true, cancellationToken);
                return null;
            }

            SelectedTradingMistake = mistake;
            return mistake;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            ActionErrorMessage = "Trading mistake details could not be loaded.";
            return null;
        }
        finally
        {
            IsLoadingDetails = false;
        }
    }

    private void CloseDetails()
    {
        if (IsEditFormVisible)
        {
            return;
        }

        SelectedTradingMistake = null;
        ActionErrorMessage = null;
    }

    private void CancelEdit()
    {
        IsEditNameInvalid = false;
        EditErrorMessage = null;
        IsEditFormVisible = false;
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        EditErrorMessage = null;
        if (SelectedTradingMistake is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(EditNameText))
        {
            IsEditNameInvalid = true;
            EditErrorMessage = "Name is required.";
            return;
        }

        IsUpdating = true;
        try
        {
            UpdateTradingMistakeResult result = await _updateUseCase.ExecuteAsync(
                new UpdateTradingMistakeCommand(
                    SelectedTradingMistake.Id,
                    EditNameText,
                    EditDescriptionText),
                cancellationToken);
            SelectedTradingMistake = result.TradingMistake;
            ReplaceListItem(result.TradingMistake);
            IsEditNameInvalid = false;
            IsEditFormVisible = false;
            SuccessMessage = result.WasChanged ? "Trading mistake updated." : null;
            if (result.WasChanged)
            {
                _ = await LoadAsync(true, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (KeyNotFoundException)
        {
            IsEditFormVisible = false;
            SelectedTradingMistake = null;
            ActionErrorMessage = NotFoundMessage;
            _ = await LoadAsync(true, cancellationToken);
        }
        catch (InvalidOperationException exception)
            when (exception.Message == UpdateTradingMistakeUseCase.DuplicateNameMessage)
        {
            IsEditNameInvalid = true;
            EditErrorMessage = UpdateTradingMistakeUseCase.DuplicateNameMessage;
        }
        catch (ArgumentException)
        {
            EditErrorMessage = "Please check the trading mistake details.";
        }
        catch (Exception)
        {
            EditErrorMessage = "Trading mistake changes could not be saved.";
        }
        finally
        {
            IsUpdating = false;
        }
    }

    private async Task DeleteAsync(Guid mistakeId, CancellationToken cancellationToken)
    {
        TradingMistakeListItem? row = TradingMistakes.FirstOrDefault(
            item => item.Id == mistakeId);
        string name = row?.Name ?? SelectedTradingMistake?.Name ?? "this mistake";
        bool confirmed = _dialogService.Confirm(new ConfirmationDialogRequest(
            "Delete trading mistake?",
            $"Delete \"{name}\"? This action cannot be undone.",
            "Delete Mistake",
            isDestructive: true));
        if (!confirmed)
        {
            return;
        }

        ActionErrorMessage = null;
        IsDeleting = true;
        try
        {
            DeleteTradingMistakeResult result = await _deleteUseCase.ExecuteAsync(
                mistakeId,
                cancellationToken);
            if (result == DeleteTradingMistakeResult.Referenced)
            {
                _dialogService.ShowInformation(new InformationDialogRequest(
                    "Cannot delete trading mistake",
                    DeleteBlockedMessage));
                return;
            }

            TradingMistakes = TradingMistakes
                .Where(item => item.Id != mistakeId)
                .ToArray();
            if (SelectedTradingMistake?.Id == mistakeId)
            {
                IsEditFormVisible = false;
                SelectedTradingMistake = null;
            }

            SuccessMessage = "Trading mistake deleted.";
            _ = await LoadAsync(true, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (KeyNotFoundException)
        {
            TradingMistakes = TradingMistakes
                .Where(item => item.Id != mistakeId)
                .ToArray();
            if (SelectedTradingMistake?.Id == mistakeId)
            {
                IsEditFormVisible = false;
                SelectedTradingMistake = null;
            }

            ActionErrorMessage = NotFoundMessage;
            _ = await LoadAsync(true, cancellationToken);
        }
        catch (Exception)
        {
            ActionErrorMessage = "Trading mistake could not be deleted.";
        }
        finally
        {
            IsDeleting = false;
        }
    }

    private async Task ToggleAsync(
        TradingMistakeListItem? item,
        CancellationToken cancellationToken)
    {
        if (item is null)
        {
            return;
        }

        ValidationErrorMessage = SaveErrorMessage = SuccessMessage = null;
        IsChangingStatus = true;
        try
        {
            await _lifecycleUseCase.ExecuteAsync(
                new SetTradingMistakeActiveStateCommand(item.Id, !item.IsActive),
                cancellationToken);
            SuccessMessage = item.IsActive
                ? "Trading mistake deactivated."
                : "Trading mistake activated.";
            _ = await LoadAsync(true, cancellationToken);
            if (SelectedTradingMistake?.Id == item.Id)
            {
                _ = await LoadDetailsAsync(item.Id, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (KeyNotFoundException)
        {
            SaveErrorMessage = "The selected trading mistake is no longer available.";
            if (SelectedTradingMistake?.Id == item.Id)
            {
                SelectedTradingMistake = null;
            }

            _ = await LoadAsync(true, cancellationToken);
        }
        catch (Exception)
        {
            SaveErrorMessage = "Trading mistake status could not be changed.";
        }
        finally
        {
            IsChangingStatus = false;
        }
    }

    private void ReplaceListItem(TradingMistakeDetails mistake)
    {
        var replacement = new TradingMistakeListItem(
            mistake.Id,
            mistake.Name,
            mistake.Description,
            mistake.IsActive,
            mistake.CreatedAtUtc,
            mistake.UpdatedAtUtc);
        TradingMistakes = TradingMistakes
            .Select(item => item.Id == mistake.Id ? replacement : item)
            .ToArray();
    }

    private bool IsBusy =>
        IsLoading ||
        IsSaving ||
        IsChangingStatus ||
        IsLoadingDetails ||
        IsUpdating ||
        IsDeleting;

    private void ResetCreateDraft()
    {
        NameText = string.Empty;
        DescriptionText = string.Empty;
        IsNameInvalid = false;
    }

    private void SetMessage(ref string? field, string? value, string hasProperty)
    {
        if (SetProperty(ref field, value))
        {
            OnPropertyChanged(hasProperty);
        }
    }

    private void NotifyCommands()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        ShowCreateCommand.NotifyCanExecuteChanged();
        CancelCreateCommand.NotifyCanExecuteChanged();
        CreateCommand.NotifyCanExecuteChanged();
        ToggleActiveCommand.NotifyCanExecuteChanged();
        ViewCommand.NotifyCanExecuteChanged();
        EditCommand.NotifyCanExecuteChanged();
        CloseDetailsCommand.NotifyCanExecuteChanged();
        CancelEditCommand.NotifyCanExecuteChanged();
        SaveChangesCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
    }

    private async Task<bool> LoadAsync(
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        if (!await _loadGate.WaitAsync(0, cancellationToken))
        {
            return false;
        }

        try
        {
            if (!forceRefresh && _hasLoadedSuccessfully)
            {
                return true;
            }

            IsLoading = true;
            ListErrorMessage = null;
            try
            {
                IReadOnlyList<TradingMistakeListItem> mistakes =
                    await _reader.GetAllAsync(cancellationToken);
                TradingMistakes = mistakes;
                if (SelectedTradingMistake is not null &&
                    mistakes.All(item => item.Id != SelectedTradingMistake.Id))
                {
                    IsEditFormVisible = false;
                    SelectedTradingMistake = null;
                }

                _hasLoadedSuccessfully = true;
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                ListErrorMessage = "Trading mistakes could not be loaded.";
                return false;
            }
            finally
            {
                IsLoading = false;
            }
        }
        finally
        {
            _loadGate.Release();
        }
    }
}
