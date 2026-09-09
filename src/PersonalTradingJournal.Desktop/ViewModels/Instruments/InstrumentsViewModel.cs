using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Instruments;

namespace PersonalTradingJournal.Desktop.ViewModels.Instruments;

public sealed class InstrumentsViewModel : ObservableObject
{
    private const string LoadErrorMessage = "Instruments could not be loaded.";

    private readonly IInstrumentReader _instrumentReader;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private string? _errorMessage;
    private bool _hasLoadedSuccessfully;
    private IReadOnlyList<InstrumentListItem> _instruments = [];
    private bool _isLoading;

    public InstrumentsViewModel(IInstrumentReader instrumentReader)
    {
        ArgumentNullException.ThrowIfNull(instrumentReader);

        _instrumentReader = instrumentReader;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, CanRefresh);
    }

    public IReadOnlyList<InstrumentListItem> Instruments
    {
        get => _instruments;
        private set
        {
            if (SetProperty(ref _instruments, value))
            {
                OnPropertyChanged(nameof(HasInstruments));
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
                RefreshCommand.NotifyCanExecuteChanged();
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

    public bool HasError => ErrorMessage is not null;

    public bool HasInstruments => Instruments.Count > 0;

    public IAsyncRelayCommand RefreshCommand { get; }

    public Task EnsureLoadedAsync()
    {
        return LoadAsync(forceRefresh: false, CancellationToken.None);
    }

    private Task RefreshAsync(CancellationToken cancellationToken)
    {
        return LoadAsync(forceRefresh: true, cancellationToken);
    }

    private bool CanRefresh() => !IsLoading;

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
                IReadOnlyList<InstrumentListItem> instruments =
                    await _instrumentReader.GetAllAsync(cancellationToken);

                Instruments = instruments;
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
