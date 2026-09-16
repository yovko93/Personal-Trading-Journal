using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Desktop.Dialogs;
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
    private const string DetailErrorMessageFallback = "Account details could not be loaded.";
    private const string DetailNotFoundMessage = "Account no longer exists. The list was refreshed.";
    private const string UpdateErrorMessageFallback = "Account changes could not be saved.";
    private const string UpdateReloadErrorMessage =
        "Account changes were saved, but the list could not be refreshed.";
    private const string DeleteErrorMessageFallback = "Account could not be deleted.";
    private const string DeleteReloadErrorMessage =
        "Account was deleted, but the list could not be refreshed.";
    private const string DeleteBlockedTitle = "Cannot delete account";
    private const string DeleteBlockedMessage =
        "This account is used by existing trades and cannot be deleted. " +
        "Deactivate it instead to preserve historical data.";

    private readonly ITradingAccountReader _accountReader;
    private readonly CreateTradingAccountUseCase _createTradingAccountUseCase;
    private readonly TradingAccountLifecycleUseCase _tradingAccountLifecycleUseCase;
    private readonly GetTradingAccountDetailsUseCase _getTradingAccountDetailsUseCase;
    private readonly UpdateTradingAccountUseCase _updateTradingAccountUseCase;
    private readonly DeleteTradingAccountUseCase _deleteTradingAccountUseCase;
    private readonly IDialogService _dialogService;
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
    private bool _isAccountNameInvalid;
    private bool _isCurrencyInvalid;
    private bool _isStartingBalanceInvalid;
    private string? _lifecycleErrorMessage;
    private string? _errorMessage;
    private string _providerName = string.Empty;
    private TradingAccountType _selectedAccountType = TradingAccountType.Personal;
    private string _startingBalanceText = string.Empty;
    private TradingAccountDetails? _selectedAccount;
    private bool _isLoadingAccountDetails;
    private bool _isEditFormVisible;
    private bool _isUpdating;
    private bool _isDeleting;
    private string _editAccountName = string.Empty;
    private TradingAccountType _editSelectedAccountType = TradingAccountType.Personal;
    private string _editProviderName = string.Empty;
    private string _editExternalAccountId = string.Empty;
    private string _editCurrency = string.Empty;
    private string _editStartingBalanceText = string.Empty;
    private bool _isEditAccountNameInvalid;
    private bool _isEditCurrencyInvalid;
    private bool _isEditStartingBalanceInvalid;
    private string? _editErrorMessage;
    private string? _actionErrorMessage;

    public AccountsViewModel(
        ITradingAccountReader accountReader,
        CreateTradingAccountUseCase createTradingAccountUseCase,
        TradingAccountLifecycleUseCase tradingAccountLifecycleUseCase,
        GetTradingAccountDetailsUseCase getTradingAccountDetailsUseCase,
        UpdateTradingAccountUseCase updateTradingAccountUseCase,
        DeleteTradingAccountUseCase deleteTradingAccountUseCase,
        IDialogService dialogService)
    {
        ArgumentNullException.ThrowIfNull(accountReader);
        ArgumentNullException.ThrowIfNull(createTradingAccountUseCase);
        ArgumentNullException.ThrowIfNull(tradingAccountLifecycleUseCase);
        ArgumentNullException.ThrowIfNull(getTradingAccountDetailsUseCase);
        ArgumentNullException.ThrowIfNull(updateTradingAccountUseCase);
        ArgumentNullException.ThrowIfNull(deleteTradingAccountUseCase);
        ArgumentNullException.ThrowIfNull(dialogService);

        _accountReader = accountReader;
        _createTradingAccountUseCase = createTradingAccountUseCase;
        _tradingAccountLifecycleUseCase = tradingAccountLifecycleUseCase;
        _getTradingAccountDetailsUseCase = getTradingAccountDetailsUseCase;
        _updateTradingAccountUseCase = updateTradingAccountUseCase;
        _deleteTradingAccountUseCase = deleteTradingAccountUseCase;
        _dialogService = dialogService;
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
        ViewAccountCommand = new AsyncRelayCommand<Guid>(ViewAccountAsync, CanUseAccountAction);
        EditAccountCommand = new AsyncRelayCommand<Guid>(EditAccountAsync, CanUseAccountAction);
        CloseAccountDetailsCommand = new RelayCommand(CloseAccountDetails, CanCloseAccountDetails);
        CancelEditCommand = new RelayCommand(CancelEdit, CanCancelEdit);
        SaveChangesCommand = new AsyncRelayCommand(SaveChangesAsync, CanSaveChanges);
        DeleteAccountCommand = new AsyncRelayCommand<Guid>(DeleteAccountAsync, CanUseAccountAction);
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
                OnPropertyChanged(nameof(SelectedAccountListItem));
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
        set
        {
            if (SetProperty(ref _accountName, value) &&
                IsAccountNameInvalid &&
                !string.IsNullOrWhiteSpace(value))
            {
                IsAccountNameInvalid = false;
                CreateErrorMessage = null;
            }
        }
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
        set
        {
            if (SetProperty(ref _currency, value) &&
                IsCurrencyInvalid &&
                !string.IsNullOrWhiteSpace(value))
            {
                IsCurrencyInvalid = false;
                CreateErrorMessage = null;
            }
        }
    }

    public string StartingBalanceText
    {
        get => _startingBalanceText;
        set
        {
            if (SetProperty(ref _startingBalanceText, value) &&
                IsStartingBalanceInvalid &&
                IsStartingBalanceValid(value))
            {
                IsStartingBalanceInvalid = false;
                CreateErrorMessage = null;
            }
        }
    }

    public bool IsAccountNameInvalid
    {
        get => _isAccountNameInvalid;
        private set => SetProperty(ref _isAccountNameInvalid, value);
    }

    public bool IsCurrencyInvalid
    {
        get => _isCurrencyInvalid;
        private set => SetProperty(ref _isCurrencyInvalid, value);
    }

    public bool IsStartingBalanceInvalid
    {
        get => _isStartingBalanceInvalid;
        private set => SetProperty(ref _isStartingBalanceInvalid, value);
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

    public TradingAccountDetails? SelectedAccount
    {
        get => _selectedAccount;
        private set
        {
            if (SetProperty(ref _selectedAccount, value))
            {
                OnPropertyChanged(nameof(HasSelectedAccount));
                OnPropertyChanged(nameof(SelectedAccountListItem));
                NotifyOperationCanExecuteChanged();
            }
        }
    }

    public bool HasSelectedAccount => SelectedAccount is not null;

    public AccountListItem? SelectedAccountListItem => SelectedAccount is null
        ? null
        : Accounts.FirstOrDefault(account => account.Id == SelectedAccount.Id);

    public bool IsLoadingAccountDetails
    {
        get => _isLoadingAccountDetails;
        private set
        {
            if (SetProperty(ref _isLoadingAccountDetails, value))
            {
                NotifyOperationCanExecuteChanged();
            }
        }
    }

    public bool IsEditFormVisible
    {
        get => _isEditFormVisible;
        private set
        {
            if (SetProperty(ref _isEditFormVisible, value))
            {
                NotifyOperationCanExecuteChanged();
            }
        }
    }

    public bool IsUpdating
    {
        get => _isUpdating;
        private set
        {
            if (SetProperty(ref _isUpdating, value))
            {
                NotifyOperationCanExecuteChanged();
            }
        }
    }

    public bool IsDeleting
    {
        get => _isDeleting;
        private set
        {
            if (SetProperty(ref _isDeleting, value))
            {
                NotifyOperationCanExecuteChanged();
            }
        }
    }

    public string EditAccountName
    {
        get => _editAccountName;
        set
        {
            if (SetProperty(ref _editAccountName, value) &&
                IsEditAccountNameInvalid &&
                !string.IsNullOrWhiteSpace(value))
            {
                IsEditAccountNameInvalid = false;
                EditErrorMessage = null;
            }
        }
    }

    public TradingAccountType EditSelectedAccountType
    {
        get => _editSelectedAccountType;
        set => SetProperty(ref _editSelectedAccountType, value);
    }

    public string EditProviderName
    {
        get => _editProviderName;
        set => SetProperty(ref _editProviderName, value);
    }

    public string EditExternalAccountId
    {
        get => _editExternalAccountId;
        set => SetProperty(ref _editExternalAccountId, value);
    }

    public string EditCurrency
    {
        get => _editCurrency;
        set
        {
            if (SetProperty(ref _editCurrency, value) &&
                IsEditCurrencyInvalid &&
                !string.IsNullOrWhiteSpace(value))
            {
                IsEditCurrencyInvalid = false;
                EditErrorMessage = null;
            }
        }
    }

    public string EditStartingBalanceText
    {
        get => _editStartingBalanceText;
        set
        {
            if (SetProperty(ref _editStartingBalanceText, value) &&
                IsEditStartingBalanceInvalid &&
                IsStartingBalanceValid(value))
            {
                IsEditStartingBalanceInvalid = false;
                EditErrorMessage = null;
            }
        }
    }

    public bool IsEditAccountNameInvalid
    {
        get => _isEditAccountNameInvalid;
        private set => SetProperty(ref _isEditAccountNameInvalid, value);
    }

    public bool IsEditCurrencyInvalid
    {
        get => _isEditCurrencyInvalid;
        private set => SetProperty(ref _isEditCurrencyInvalid, value);
    }

    public bool IsEditStartingBalanceInvalid
    {
        get => _isEditStartingBalanceInvalid;
        private set => SetProperty(ref _isEditStartingBalanceInvalid, value);
    }

    public string? EditErrorMessage
    {
        get => _editErrorMessage;
        private set
        {
            if (SetProperty(ref _editErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasEditError));
            }
        }
    }

    public bool HasEditError => EditErrorMessage is not null;

    public string? ActionErrorMessage
    {
        get => _actionErrorMessage;
        private set
        {
            if (SetProperty(ref _actionErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasActionError));
            }
        }
    }

    public bool HasActionError => ActionErrorMessage is not null;

    public IAsyncRelayCommand RefreshCommand { get; }

    public IRelayCommand ShowCreateFormCommand { get; }

    public IRelayCommand CancelCreateCommand { get; }

    public IAsyncRelayCommand CreateAccountCommand { get; }

    public IAsyncRelayCommand<AccountListItem> ActivateAccountCommand { get; }

    public IAsyncRelayCommand<AccountListItem> DeactivateAccountCommand { get; }

    public IAsyncRelayCommand<Guid> ViewAccountCommand { get; }

    public IAsyncRelayCommand<Guid> EditAccountCommand { get; }

    public IRelayCommand CloseAccountDetailsCommand { get; }

    public IRelayCommand CancelEditCommand { get; }

    public IAsyncRelayCommand SaveChangesCommand { get; }

    public IAsyncRelayCommand<Guid> DeleteAccountCommand { get; }

    public async Task EnsureLoadedAsync()
    {
        _ = await LoadAsync(forceRefresh: false, CancellationToken.None);
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        _ = await LoadAsync(forceRefresh: true, cancellationToken);
    }

    private bool CanRefresh() =>
        !IsAnyOperationInProgress;

    private void ShowCreateForm()
    {
        CloseAccountDetails();
        CreateErrorMessage = null;
        IsCreateFormVisible = true;
    }

    private bool CanShowCreateForm() =>
        !IsCreateFormVisible &&
        !IsAnyOperationInProgress &&
        !IsEditFormVisible;

    private void CancelCreate()
    {
        ResetCreateForm();
        IsCreateFormVisible = false;
    }

    private bool CanCancelCreate() => IsCreateFormVisible && !IsCreating;

    private bool CanCreateAccount() =>
        IsCreateFormVisible &&
        !IsAnyOperationInProgress;

    private async Task CreateAccountAsync(CancellationToken cancellationToken)
    {
        CreateErrorMessage = null;

        if (string.IsNullOrWhiteSpace(AccountName))
        {
            IsAccountNameInvalid = true;
            CreateErrorMessage = "Account name is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Currency))
        {
            IsCurrencyInvalid = true;
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
                IsStartingBalanceInvalid = true;
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
        IsAccountNameInvalid = false;
        IsCurrencyInvalid = false;
        IsStartingBalanceInvalid = false;
        CreateErrorMessage = null;
    }

    private static bool IsStartingBalanceValid(string value) =>
        string.IsNullOrWhiteSpace(value) ||
        decimal.TryParse(
            value,
            NumberStyles.Number,
            CultureInfo.CurrentCulture,
            out _);

    private bool CanUseAccountAction(Guid accountId) =>
        accountId != Guid.Empty &&
        !IsAnyOperationInProgress &&
        !IsCreateFormVisible &&
        !IsEditFormVisible;

    private async Task ViewAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        _ = await LoadAccountDetailsAsync(accountId, cancellationToken);
    }

    private async Task EditAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        TradingAccountDetails? account = await LoadAccountDetailsAsync(
            accountId,
            cancellationToken);
        if (account is null)
        {
            return;
        }

        PopulateEditForm(account);
        IsEditFormVisible = true;
    }

    private async Task<TradingAccountDetails?> LoadAccountDetailsAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        ActionErrorMessage = null;
        IsLoadingAccountDetails = true;

        try
        {
            TradingAccountDetails? account =
                await _getTradingAccountDetailsUseCase.ExecuteAsync(
                    accountId,
                    cancellationToken);

            if (account is null)
            {
                if (SelectedAccount?.Id == accountId)
                {
                    SelectedAccount = null;
                }

                ActionErrorMessage = DetailNotFoundMessage;
                _ = await LoadAsync(forceRefresh: true, cancellationToken);
                return null;
            }

            SelectedAccount = account;
            return account;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            ActionErrorMessage = DetailErrorMessageFallback;
            return null;
        }
        finally
        {
            IsLoadingAccountDetails = false;
        }
    }

    private void PopulateEditForm(TradingAccountDetails account)
    {
        EditAccountName = account.Name;
        EditSelectedAccountType = account.AccountType;
        EditProviderName = account.ProviderName ?? string.Empty;
        EditExternalAccountId = account.ExternalAccountId ?? string.Empty;
        EditCurrency = account.Currency;
        EditStartingBalanceText = account.StartingBalance?.ToString(
            CultureInfo.CurrentCulture) ?? string.Empty;
        IsEditAccountNameInvalid = false;
        IsEditCurrencyInvalid = false;
        IsEditStartingBalanceInvalid = false;
        EditErrorMessage = null;
    }

    private void CloseAccountDetails()
    {
        if (IsEditFormVisible)
        {
            return;
        }

        SelectedAccount = null;
        ActionErrorMessage = null;
    }

    private bool CanCloseAccountDetails() =>
        SelectedAccount is not null &&
        !IsAnyOperationInProgress &&
        !IsEditFormVisible;

    private void CancelEdit()
    {
        ResetEditValidation();
        IsEditFormVisible = false;
    }

    private bool CanCancelEdit() =>
        IsEditFormVisible && !IsUpdating;

    private bool CanSaveChanges() =>
        IsEditFormVisible &&
        SelectedAccount is not null &&
        !IsAnyOperationInProgress;

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        EditErrorMessage = null;

        if (SelectedAccount is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(EditAccountName))
        {
            IsEditAccountNameInvalid = true;
            EditErrorMessage = "Account name is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(EditCurrency))
        {
            IsEditCurrencyInvalid = true;
            EditErrorMessage = "Currency is required.";
            return;
        }

        decimal? startingBalance = null;
        if (!string.IsNullOrWhiteSpace(EditStartingBalanceText))
        {
            if (!decimal.TryParse(
                EditStartingBalanceText,
                NumberStyles.Number,
                CultureInfo.CurrentCulture,
                out decimal parsedStartingBalance))
            {
                IsEditStartingBalanceInvalid = true;
                EditErrorMessage = "Starting balance must be a valid number.";
                return;
            }

            startingBalance = parsedStartingBalance;
        }

        IsUpdating = true;

        try
        {
            UpdateTradingAccountResult result =
                await _updateTradingAccountUseCase.ExecuteAsync(
                    new UpdateTradingAccountCommand(
                        SelectedAccount.Id,
                        EditAccountName,
                        EditSelectedAccountType,
                        EditProviderName,
                        EditExternalAccountId,
                        EditCurrency,
                        startingBalance),
                    cancellationToken);

            SelectedAccount = result.Account;
            ReplaceListItem(result.Account);
            ResetEditValidation();
            IsEditFormVisible = false;

            if (result.WasChanged &&
                !await LoadAsync(forceRefresh: true, cancellationToken))
            {
                ErrorMessage = UpdateReloadErrorMessage;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (KeyNotFoundException)
        {
            IsEditFormVisible = false;
            SelectedAccount = null;
            ActionErrorMessage = DetailNotFoundMessage;
            _ = await LoadAsync(forceRefresh: true, cancellationToken);
        }
        catch (ArgumentException)
        {
            EditErrorMessage = InvalidAccountDetailsMessage;
        }
        catch (Exception)
        {
            EditErrorMessage = UpdateErrorMessageFallback;
        }
        finally
        {
            IsUpdating = false;
        }
    }

    private async Task DeleteAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        AccountListItem? listItem = Accounts.FirstOrDefault(item => item.Id == accountId);
        string accountName = listItem?.Name ?? SelectedAccount?.Name ?? "this account";
        bool confirmed = _dialogService.Confirm(new ConfirmationDialogRequest(
            title: "Delete account?",
            message: $"Delete \"{accountName}\"? This action cannot be undone.",
            confirmButtonText: "Delete Account",
            isDestructive: true));

        if (!confirmed)
        {
            return;
        }

        ActionErrorMessage = null;
        IsDeleting = true;

        try
        {
            DeleteTradingAccountResult result =
                await _deleteTradingAccountUseCase.ExecuteAsync(
                    accountId,
                    cancellationToken);

            if (result == DeleteTradingAccountResult.Referenced)
            {
                _dialogService.ShowInformation(new InformationDialogRequest(
                    DeleteBlockedTitle,
                    DeleteBlockedMessage));
                return;
            }

            Accounts = Accounts.Where(account => account.Id != accountId).ToArray();
            if (SelectedAccount?.Id == accountId)
            {
                IsEditFormVisible = false;
                SelectedAccount = null;
            }

            if (!await LoadAsync(forceRefresh: true, cancellationToken))
            {
                ErrorMessage = DeleteReloadErrorMessage;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (KeyNotFoundException)
        {
            Accounts = Accounts.Where(account => account.Id != accountId).ToArray();
            if (SelectedAccount?.Id == accountId)
            {
                IsEditFormVisible = false;
                SelectedAccount = null;
            }

            ActionErrorMessage = DetailNotFoundMessage;
            _ = await LoadAsync(forceRefresh: true, cancellationToken);
        }
        catch (Exception)
        {
            ActionErrorMessage = DeleteErrorMessageFallback;
        }
        finally
        {
            IsDeleting = false;
        }
    }

    private void ReplaceListItem(TradingAccountDetails account)
    {
        var replacement = new AccountListItem(
            account.Id,
            account.Name,
            account.AccountType,
            account.ProviderName,
            account.ExternalAccountId,
            account.Currency,
            account.StartingBalance,
            account.IsActive);
        Accounts = Accounts
            .Select(item => item.Id == account.Id ? replacement : item)
            .ToArray();
    }

    private void ResetEditValidation()
    {
        IsEditAccountNameInvalid = false;
        IsEditCurrencyInvalid = false;
        IsEditStartingBalanceInvalid = false;
        EditErrorMessage = null;
    }

    private bool IsAnyOperationInProgress =>
        IsLoading ||
        IsCreating ||
        IsChangingAccountState ||
        IsLoadingAccountDetails ||
        IsUpdating ||
        IsDeleting;

    private bool CanActivateAccount(AccountListItem? account) =>
        account is { IsActive: false } && CanChangeAccountState();

    private bool CanDeactivateAccount(AccountListItem? account) =>
        account is { IsActive: true } && CanChangeAccountState();

    private bool CanChangeAccountState() =>
        !IsAnyOperationInProgress &&
        !IsCreateFormVisible &&
        !IsEditFormVisible;

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

            if (SelectedAccount?.Id == account.Id)
            {
                _ = await LoadAccountDetailsAsync(account.Id, cancellationToken);
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

            if (SelectedAccount?.Id == account.Id)
            {
                _ = await LoadAccountDetailsAsync(account.Id, cancellationToken);
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
        ViewAccountCommand.NotifyCanExecuteChanged();
        EditAccountCommand.NotifyCanExecuteChanged();
        CloseAccountDetailsCommand.NotifyCanExecuteChanged();
        CancelEditCommand.NotifyCanExecuteChanged();
        SaveChangesCommand.NotifyCanExecuteChanged();
        DeleteAccountCommand.NotifyCanExecuteChanged();
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
                if (SelectedAccount is not null &&
                    accounts.All(account => account.Id != SelectedAccount.Id))
                {
                    IsEditFormVisible = false;
                    SelectedAccount = null;
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
