using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Strategies;

namespace PersonalTradingJournal.Desktop.ViewModels.Strategies;

public sealed class StrategiesViewModel : ObservableObject
{
    private const string ListFailureMessage = "Strategies could not be loaded.";
    private const string SaveFailureMessage = "Strategy could not be created.";
    private const string LifecycleFailureMessage = "Strategy status could not be changed.";
    private const string LifecycleMissingMessage = "The selected strategy is no longer available.";

    private readonly IStrategyReader _strategyReader;
    private readonly CreateStrategyUseCase _createStrategyUseCase;
    private readonly StrategyLifecycleUseCase _strategyLifecycleUseCase;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private IReadOnlyList<StrategyListItem> _strategies = [];
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

    public StrategiesViewModel(
        IStrategyReader strategyReader,
        CreateStrategyUseCase createStrategyUseCase,
        StrategyLifecycleUseCase strategyLifecycleUseCase)
    {
        ArgumentNullException.ThrowIfNull(strategyReader);
        ArgumentNullException.ThrowIfNull(createStrategyUseCase);
        ArgumentNullException.ThrowIfNull(strategyLifecycleUseCase);

        _strategyReader = strategyReader;
        _createStrategyUseCase = createStrategyUseCase;
        _strategyLifecycleUseCase = strategyLifecycleUseCase;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, CanRefresh);
        ShowCreateCommand = new RelayCommand(ShowCreate, CanShowCreate);
        CancelCreateCommand = new RelayCommand(CancelCreate, CanCancelCreate);
        CreateCommand = new AsyncRelayCommand(CreateAsync, CanCreate);
        ToggleActiveCommand = new AsyncRelayCommand<StrategyListItem>(
            ToggleActiveAsync,
            CanToggleActive);
    }

    public IReadOnlyList<StrategyListItem> Strategies
    {
        get => _strategies;
        private set
        {
            if (SetProperty(ref _strategies, value))
            {
                OnPropertyChanged(nameof(HasStrategies));
                ToggleActiveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasStrategies => Strategies.Count > 0;

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsSaving
    {
        get => _isSaving;
        private set
        {
            if (SetProperty(ref _isSaving, value))
            {
                NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsChangingStatus
    {
        get => _isChangingStatus;
        private set
        {
            if (SetProperty(ref _isChangingStatus, value))
            {
                NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsCreateFormVisible
    {
        get => _isCreateFormVisible;
        private set
        {
            if (SetProperty(ref _isCreateFormVisible, value))
            {
                NotifyCanExecuteChanged();
            }
        }
    }

    public string NameText
    {
        get => _nameText;
        set => SetProperty(ref _nameText, value);
    }

    public string DescriptionText
    {
        get => _descriptionText;
        set => SetProperty(ref _descriptionText, value);
    }

    public string? ValidationErrorMessage
    {
        get => _validationErrorMessage;
        private set => SetMessage(ref _validationErrorMessage, value, nameof(HasValidationError));
    }

    public bool HasValidationError => ValidationErrorMessage is not null;

    public string? SaveErrorMessage
    {
        get => _saveErrorMessage;
        private set => SetMessage(ref _saveErrorMessage, value, nameof(HasSaveError));
    }

    public bool HasSaveError => SaveErrorMessage is not null;

    public string? ListErrorMessage
    {
        get => _listErrorMessage;
        private set => SetMessage(ref _listErrorMessage, value, nameof(HasListError));
    }

    public bool HasListError => ListErrorMessage is not null;

    public string? SuccessMessage
    {
        get => _successMessage;
        private set => SetMessage(ref _successMessage, value, nameof(HasSuccessMessage));
    }

    public bool HasSuccessMessage => SuccessMessage is not null;

    public IAsyncRelayCommand RefreshCommand { get; }

    public IRelayCommand ShowCreateCommand { get; }

    public IRelayCommand CancelCreateCommand { get; }

    public IAsyncRelayCommand CreateCommand { get; }

    public IAsyncRelayCommand<StrategyListItem> ToggleActiveCommand { get; }

    public async Task EnsureLoadedAsync()
    {
        _ = await LoadAsync(forceRefresh: false, CancellationToken.None);
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        _ = await LoadAsync(forceRefresh: true, cancellationToken);
    }

    private bool CanRefresh() => !IsBusy;

    private void ShowCreate()
    {
        ResetDraft();
        ClearMessages();
        IsCreateFormVisible = true;
    }

    private bool CanShowCreate() => !IsCreateFormVisible && !IsBusy;

    private void CancelCreate()
    {
        ResetDraft();
        ValidationErrorMessage = null;
        SaveErrorMessage = null;
        IsCreateFormVisible = false;
    }

    private bool CanCancelCreate() => IsCreateFormVisible && !IsSaving;

    private bool CanCreate() => IsCreateFormVisible && !IsBusy;

    private async Task CreateAsync(CancellationToken cancellationToken)
    {
        ValidationErrorMessage = null;
        SaveErrorMessage = null;
        SuccessMessage = null;

        if (string.IsNullOrWhiteSpace(NameText))
        {
            ValidationErrorMessage = "Name is required.";
            return;
        }

        IsSaving = true;
        try
        {
            await _createStrategyUseCase.ExecuteAsync(
                new CreateStrategyCommand(NameText, DescriptionText),
                cancellationToken);

            SuccessMessage = "Strategy created.";
            ResetDraft();
            IsCreateFormVisible = false;
            _ = await LoadAsync(forceRefresh: true, CancellationToken.None);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidOperationException exception)
            when (exception.Message == CreateStrategyUseCase.DuplicateNameMessage)
        {
            ValidationErrorMessage = CreateStrategyUseCase.DuplicateNameMessage;
        }
        catch (ArgumentException)
        {
            ValidationErrorMessage = "Please check the strategy details.";
        }
        catch (Exception)
        {
            SaveErrorMessage = SaveFailureMessage;
        }
        finally
        {
            IsSaving = false;
        }
    }

    private bool CanToggleActive(StrategyListItem? strategy) =>
        strategy is not null && !IsBusy && !IsCreateFormVisible;

    private async Task ToggleActiveAsync(
        StrategyListItem? strategy,
        CancellationToken cancellationToken)
    {
        if (strategy is null)
        {
            return;
        }

        ValidationErrorMessage = null;
        SaveErrorMessage = null;
        SuccessMessage = null;
        IsChangingStatus = true;

        try
        {
            await _strategyLifecycleUseCase.ExecuteAsync(
                new SetStrategyActiveStateCommand(strategy.Id, !strategy.IsActive),
                cancellationToken);

            SuccessMessage = strategy.IsActive
                ? "Strategy deactivated."
                : "Strategy activated.";
            _ = await LoadAsync(forceRefresh: true, CancellationToken.None);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (KeyNotFoundException)
        {
            SaveErrorMessage = LifecycleMissingMessage;
        }
        catch (Exception)
        {
            SaveErrorMessage = LifecycleFailureMessage;
        }
        finally
        {
            IsChangingStatus = false;
        }
    }

    private bool IsBusy => IsLoading || IsSaving || IsChangingStatus;

    private void ResetDraft()
    {
        NameText = string.Empty;
        DescriptionText = string.Empty;
    }

    private void ClearMessages()
    {
        ValidationErrorMessage = null;
        SaveErrorMessage = null;
        SuccessMessage = null;
    }

    private void SetMessage(ref string? field, string? value, string hasPropertyName)
    {
        if (SetProperty(ref field, value))
        {
            OnPropertyChanged(hasPropertyName);
        }
    }

    private void NotifyCanExecuteChanged()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        ShowCreateCommand.NotifyCanExecuteChanged();
        CancelCreateCommand.NotifyCanExecuteChanged();
        CreateCommand.NotifyCanExecuteChanged();
        ToggleActiveCommand.NotifyCanExecuteChanged();
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
                Strategies = await _strategyReader.GetAllAsync(cancellationToken);
                _hasLoadedSuccessfully = true;
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                ListErrorMessage = ListFailureMessage;
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
