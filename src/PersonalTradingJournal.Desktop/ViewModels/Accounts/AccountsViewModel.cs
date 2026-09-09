using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Accounts;

namespace PersonalTradingJournal.Desktop.ViewModels.Accounts;

public sealed class AccountsViewModel : ObservableObject
{
    private const string LoadErrorMessage = "Accounts could not be loaded.";

    private readonly ITradingAccountReader _accountReader;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private IReadOnlyList<AccountListItem> _accounts = [];
    private bool _hasLoadedSuccessfully;
    private bool _isLoading;
    private string? _errorMessage;

    public AccountsViewModel(ITradingAccountReader accountReader)
    {
        ArgumentNullException.ThrowIfNull(accountReader);

        _accountReader = accountReader;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
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
        private set => SetProperty(ref _isLoading, value);
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

    public IAsyncRelayCommand RefreshCommand { get; }

    public Task EnsureLoadedAsync()
    {
        return LoadAsync(forceRefresh: false, CancellationToken.None);
    }

    private Task RefreshAsync(CancellationToken cancellationToken)
    {
        return LoadAsync(forceRefresh: true, cancellationToken);
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
