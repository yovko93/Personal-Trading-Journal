using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Domain.Accounts;
using System.Globalization;

namespace PersonalTradingJournal.Desktop.ViewModels.Accounts;

public sealed class AccountsViewModel : ObservableObject
{
    private const string CreateErrorMessageFallback = "Account could not be created.";
    private const string CreateReloadErrorMessage =
        "Account was created, but the list could not be refreshed. Refresh to see the latest data.";
    private const string InvalidAccountDetailsMessage = "Please check the account details.";
    private const string LifecycleErrorMessageFallback = "Account status could not be changed.";
    private const string LifecycleNotFoundMessage = "Account no longer exists. Refresh the list.";
    private const string LifecycleReloadErrorMessage =
        "Account status changed, but the list could not be refreshed. Refresh to see the latest status.";
    private const string LoadErrorMessage = "Accounts could not be loaded.";

    private readonly ITradingAccountReader _accountReader;
    private readonly CreateTradingAccountUseCase _createTradingAccountUseCase;
    private readonly TradingAccountLifecycleUseCase _tradingAccountLifecycleUseCase;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private IReadOnlyList<AccountListItem> _accounts = [];
    private string _accountName = string.Empty;
    private string _currency = string.Empty;
    private string? _createErrorMessage;
    private string _externalAccountId = string.Empty;
    private bool _hasLoadedSuccessfully;
    private bool _isCreateFormVisible;
    private bool _isChangingAccountState;
    private bool _isCreating;
    private bool _isLoading;
    private string? _lifecycleErrorMessage;
    private string? _errorMessage;
    private string _providerName = string.Empty;
    private TradingAccountType _selectedAccountType = TradingAccountType.Personal;
    private string _startingBalanceText = string.Empty;

    public AccountsViewModel(
        ITradingAccountReader accountReader,
        CreateTradingAccountUseCase createTradingAccountUseCase,
        TradingAccountLifecycleUseCase tradingAccountLifecycleUseCase)
    {
        ArgumentNullException.ThrowIfNull(accountReader);
        ArgumentNullException.ThrowIfNull(createTradingAccountUseCase);
        ArgumentNullException.ThrowIfNull(tradingAccountLifecycleUseCase);

        _accountReader = accountReader;
        _createTradingAccountUseCase = createTradingAccountUseCase;
        _tradingAccountLifecycleUseCase = tradingAccountLifecycleUseCase;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, CanRefresh);
        ShowCreateFormCommand = new RelayCommand(ShowCreateForm, CanShowCreateForm);
        CancelCreateCommand = new RelayCommand(CancelCreate, CanCancelCreate);
        CreateAccountCommand = new AsyncRelayCommand(CreateAccountAsync, CanCreateAccount);
        ActivateAccountCommand = new AsyncRelayCommand<AccountListItem>(
            ActivateAccountAsync,
            CanActivateAccount);
        DeactivateAccountCommand = new AsyncRelayCommand<AccountListItem>(
            DeactivateAccountAsync,
            CanDeactivateAccount);
    }

    public IReadOnlyList<AccountListItem> Accounts
    {
        get => _accounts;
        private set
        {
            if (SetProperty(ref _accounts, value))
            {
                OnPropertyChanged(nameof(HasAccounts));
                ActivateAccountCommand.NotifyCanExecuteChanged();
                DeactivateAccountCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                NotifyOperationCanExecuteChanged();
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasAccounts => Accounts.Count > 0;

    public bool HasError => ErrorMessage is not null;

    public bool IsCreateFormVisible
    {
        get => _isCreateFormVisible;
        private set
        {
            if (SetProperty(ref _isCreateFormVisible, value))
            {
                CancelCreateCommand.NotifyCanExecuteChanged();
                NotifyOperationCanExecuteChanged();
            }
        }
    }

    public string AccountName
    {
        get => _accountName;
        set => SetProperty(ref _accountName, value);
    }

    public IReadOnlyList<TradingAccountType> AccountTypes { get; } =
        Enum.GetValues<TradingAccountType>();

    public TradingAccountType SelectedAccountType
    {
        get => _selectedAccountType;
        set => SetProperty(ref _selectedAccountType, value);
    }

    public string ProviderName
    {
        get => _providerName;
        set => SetProperty(ref _providerName, value);
    }

    public string ExternalAccountId
    {
        get => _externalAccountId;
        set => SetProperty(ref _externalAccountId, value);
    }

    public string Currency
    {
        get => _currency;
        set => SetProperty(ref _currency, value);
    }

    public string StartingBalanceText
    {
        get => _startingBalanceText;
        set => SetProperty(ref _startingBalanceText, value);
    }

    public bool IsCreating
    {
        get => _isCreating;
        private set
        {
            if (SetProperty(ref _isCreating, value))
            {
                CancelCreateCommand.NotifyCanExecuteChanged();
                NotifyOperationCanExecuteChanged();
            }
        }
    }

    public string? CreateErrorMessage
    {
        get => _createErrorMessage;
        private set
        {
            if (SetProperty(ref _createErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasCreateError));
            }
        }
    }

    public bool HasCreateError => CreateErrorMessage is not null;

    public bool IsChangingAccountState
    {
        get => _isChangingAccountState;
        private set
        {
            if (SetProperty(ref _isChangingAccountState, value))
            {
                NotifyOperationCanExecuteChanged();
            }
        }
    }

    public string? LifecycleErrorMessage
    {
        get => _lifecycleErrorMessage;
        private set
        {
            if (SetProperty(ref _lifecycleErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasLifecycleError));
            }
        }
    }

    public bool HasLifecycleError => LifecycleErrorMessage is not null;

    public IAsyncRelayCommand RefreshCommand { get; }

    public IRelayCommand ShowCreateFormCommand { get; }

    public IRelayCommand CancelCreateCommand { get; }

    public IAsyncRelayCommand CreateAccountCommand { get; }

    public IAsyncRelayCommand<AccountListItem> ActivateAccountCommand { get; }

    public IAsyncRelayCommand<AccountListItem> DeactivateAccountCommand { get; }

    public async Task EnsureLoadedAsync()
    {
        _ = await LoadAsync(forceRefresh: false, CancellationToken.None);
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        _ = await LoadAsync(forceRefresh: true, cancellationToken);
    }

    private bool CanRefresh() =>
        !IsLoading && !IsCreating && !IsChangingAccountState;

    private void ShowCreateForm()
    {
        CreateErrorMessage = null;
        IsCreateFormVisible = true;
    }

    private bool CanShowCreateForm() =>
        !IsCreateFormVisible &&
        !IsLoading &&
        !IsCreating &&
        !IsChangingAccountState;

    private void CancelCreate()
    {
        ResetCreateForm();
        IsCreateFormVisible = false;
    }

    private bool CanCancelCreate() => IsCreateFormVisible && !IsCreating;

    private bool CanCreateAccount() =>
        IsCreateFormVisible &&
        !IsCreating &&
        !IsLoading &&
        !IsChangingAccountState;

    private async Task CreateAccountAsync(CancellationToken cancellationToken)
    {
        CreateErrorMessage = null;

        if (string.IsNullOrWhiteSpace(AccountName))
        {
            CreateErrorMessage = "Account name is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Currency))
        {
            CreateErrorMessage = "Currency is required.";
            return;
        }

        decimal? startingBalance = null;
        if (!string.IsNullOrWhiteSpace(StartingBalanceText))
        {
            if (!decimal.TryParse(
                StartingBalanceText,
                NumberStyles.Number,
                CultureInfo.CurrentCulture,
                out decimal parsedStartingBalance))
            {
                CreateErrorMessage = "Starting balance must be a valid number.";
                return;
            }

            startingBalance = parsedStartingBalance;
        }

        IsCreating = true;

        try
        {
            var command = new CreateTradingAccountCommand(
                AccountName,
                SelectedAccountType,
                ProviderName,
                ExternalAccountId,
                Currency,
                startingBalance);

            await _createTradingAccountUseCase.ExecuteAsync(command, cancellationToken);

            bool wasReloaded;
            try
            {
                wasReloaded = await LoadAsync(forceRefresh: true, cancellationToken);
            }
            finally
            {
                ResetCreateForm();
                IsCreateFormVisible = false;
            }

            if (!wasReloaded)
            {
                ErrorMessage = CreateReloadErrorMessage;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ArgumentException)
        {
            CreateErrorMessage = InvalidAccountDetailsMessage;
        }
        catch (Exception)
        {
            CreateErrorMessage = CreateErrorMessageFallback;
        }
        finally
        {
            IsCreating = false;
        }
    }

    private void ResetCreateForm()
    {
        AccountName = string.Empty;
        SelectedAccountType = TradingAccountType.Personal;
        ProviderName = string.Empty;
        ExternalAccountId = string.Empty;
        Currency = string.Empty;
        StartingBalanceText = string.Empty;
        CreateErrorMessage = null;
    }

    private bool CanActivateAccount(AccountListItem? account) =>
        account is { IsActive: false } && CanChangeAccountState();

    private bool CanDeactivateAccount(AccountListItem? account) =>
        account is { IsActive: true } && CanChangeAccountState();

    private bool CanChangeAccountState() =>
        !IsLoading &&
        !IsCreating &&
        !IsChangingAccountState &&
        !IsCreateFormVisible;

    private async Task ActivateAccountAsync(
        AccountListItem? account,
        CancellationToken cancellationToken)
    {
        if (account is null)
        {
            return;
        }

        LifecycleErrorMessage = null;
        IsChangingAccountState = true;

        try
        {
            await _tradingAccountLifecycleUseCase.ActivateAsync(
                account.Id,
                cancellationToken);
            bool wasReloaded = await LoadAsync(forceRefresh: true, cancellationToken);

            if (!wasReloaded)
            {
                ErrorMessage = LifecycleReloadErrorMessage;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (KeyNotFoundException)
        {
            LifecycleErrorMessage = LifecycleNotFoundMessage;
        }
        catch (Exception)
        {
            LifecycleErrorMessage = LifecycleErrorMessageFallback;
        }
        finally
        {
            IsChangingAccountState = false;
        }
    }

    private async Task DeactivateAccountAsync(
        AccountListItem? account,
        CancellationToken cancellationToken)
    {
        if (account is null)
        {
            return;
        }

        LifecycleErrorMessage = null;
        IsChangingAccountState = true;

        try
        {
            await _tradingAccountLifecycleUseCase.DeactivateAsync(
                account.Id,
                cancellationToken);
            bool wasReloaded = await LoadAsync(forceRefresh: true, cancellationToken);

            if (!wasReloaded)
            {
                ErrorMessage = LifecycleReloadErrorMessage;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (KeyNotFoundException)
        {
            LifecycleErrorMessage = LifecycleNotFoundMessage;
        }
        catch (Exception)
        {
            LifecycleErrorMessage = LifecycleErrorMessageFallback;
        }
        finally
        {
            IsChangingAccountState = false;
        }
    }

    private void NotifyOperationCanExecuteChanged()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        ShowCreateFormCommand.NotifyCanExecuteChanged();
        CreateAccountCommand.NotifyCanExecuteChanged();
        ActivateAccountCommand.NotifyCanExecuteChanged();
        DeactivateAccountCommand.NotifyCanExecuteChanged();
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
            ErrorMessage = null;

            try
            {
                IReadOnlyList<AccountListItem> accounts =
                    await _accountReader.GetAllAsync(cancellationToken);

                Accounts = accounts;
                _hasLoadedSuccessfully = true;
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                ErrorMessage = LoadErrorMessage;
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
