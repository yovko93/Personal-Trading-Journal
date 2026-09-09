using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Domain.Accounts;
using System.Globalization;

namespace PersonalTradingJournal.Desktop.ViewModels.Accounts;

public sealed class AccountsViewModel : ObservableObject
{
    private const string CreateErrorMessageFallback = "Account could not be created.";
    private const string InvalidAccountDetailsMessage = "Please check the account details.";
    private const string LoadErrorMessage = "Accounts could not be loaded.";

    private readonly ITradingAccountReader _accountReader;
    private readonly CreateTradingAccountUseCase _createTradingAccountUseCase;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private IReadOnlyList<AccountListItem> _accounts = [];
    private string _accountName = string.Empty;
    private string _currency = string.Empty;
    private string? _createErrorMessage;
    private string _externalAccountId = string.Empty;
    private bool _hasLoadedSuccessfully;
    private bool _isCreateFormVisible;
    private bool _isCreating;
    private bool _isLoading;
    private string? _errorMessage;
    private string _providerName = string.Empty;
    private TradingAccountType _selectedAccountType = TradingAccountType.Personal;
    private string _startingBalanceText = string.Empty;

    public AccountsViewModel(
        ITradingAccountReader accountReader,
        CreateTradingAccountUseCase createTradingAccountUseCase)
    {
        ArgumentNullException.ThrowIfNull(accountReader);
        ArgumentNullException.ThrowIfNull(createTradingAccountUseCase);

        _accountReader = accountReader;
        _createTradingAccountUseCase = createTradingAccountUseCase;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, CanRefresh);
        ShowCreateFormCommand = new RelayCommand(ShowCreateForm, CanShowCreateForm);
        CancelCreateCommand = new RelayCommand(CancelCreate, CanCancelCreate);
        CreateAccountCommand = new AsyncRelayCommand(CreateAccountAsync, CanCreateAccount);
    }

    public IReadOnlyList<AccountListItem> Accounts
    {
        get => _accounts;
        private set
        {
            if (SetProperty(ref _accounts, value))
            {
                OnPropertyChanged(nameof(HasAccounts));
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
                CreateAccountCommand.NotifyCanExecuteChanged();
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
                ShowCreateFormCommand.NotifyCanExecuteChanged();
                CancelCreateCommand.NotifyCanExecuteChanged();
                CreateAccountCommand.NotifyCanExecuteChanged();
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
                RefreshCommand.NotifyCanExecuteChanged();
                ShowCreateFormCommand.NotifyCanExecuteChanged();
                CancelCreateCommand.NotifyCanExecuteChanged();
                CreateAccountCommand.NotifyCanExecuteChanged();
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

    public IAsyncRelayCommand RefreshCommand { get; }

    public IRelayCommand ShowCreateFormCommand { get; }

    public IRelayCommand CancelCreateCommand { get; }

    public IAsyncRelayCommand CreateAccountCommand { get; }

    public Task EnsureLoadedAsync()
    {
        return LoadAsync(forceRefresh: false, CancellationToken.None);
    }

    private Task RefreshAsync(CancellationToken cancellationToken)
    {
        return LoadAsync(forceRefresh: true, cancellationToken);
    }

    private bool CanRefresh() => !IsCreating;

    private void ShowCreateForm()
    {
        CreateErrorMessage = null;
        IsCreateFormVisible = true;
    }

    private bool CanShowCreateForm() => !IsCreateFormVisible && !IsCreating;

    private void CancelCreate()
    {
        ResetCreateForm();
        IsCreateFormVisible = false;
    }

    private bool CanCancelCreate() => IsCreateFormVisible && !IsCreating;

    private bool CanCreateAccount() =>
        IsCreateFormVisible && !IsCreating && !IsLoading;

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
            await LoadAsync(forceRefresh: true, cancellationToken);

            ResetCreateForm();
            IsCreateFormVisible = false;
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

    private async Task LoadAsync(
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        if (!await _loadGate.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            if (!forceRefresh && _hasLoadedSuccessfully)
            {
                return;
            }

            IsLoading = true;
            ErrorMessage = null;

            try
            {
                IReadOnlyList<AccountListItem> accounts =
                    await _accountReader.GetAllAsync(cancellationToken);

                Accounts = accounts;
                _hasLoadedSuccessfully = true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                ErrorMessage = LoadErrorMessage;
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
