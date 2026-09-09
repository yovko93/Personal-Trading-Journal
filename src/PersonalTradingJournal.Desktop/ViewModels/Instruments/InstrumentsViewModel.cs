using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Domain.Instruments;
using System.Globalization;
using CreateInstrumentRequest = PersonalTradingJournal.Application.Instruments.CreateInstrumentCommand;

namespace PersonalTradingJournal.Desktop.ViewModels.Instruments;

public sealed class InstrumentsViewModel : ObservableObject
{
    private const NumberStyles DecimalNumberStyles =
        NumberStyles.AllowLeadingWhite |
        NumberStyles.AllowTrailingWhite |
        NumberStyles.AllowLeadingSign |
        NumberStyles.AllowDecimalPoint;
    private const string CreateErrorMessageFallback = "Instrument could not be created.";
    private const string InvalidInstrumentDetailsMessage = "Please check the instrument details.";
    private const string LoadErrorMessage = "Instruments could not be loaded.";

    private readonly IInstrumentReader _instrumentReader;
    private readonly CreateInstrumentUseCase _createInstrumentUseCase;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private string _currency = string.Empty;
    private string? _createErrorMessage;
    private string _displayName = string.Empty;
    private string? _errorMessage;
    private string _exchange = string.Empty;
    private bool _hasLoadedSuccessfully;
    private IReadOnlyList<InstrumentListItem> _instruments = [];
    private bool _isCreateFormVisible;
    private bool _isCreating;
    private bool _isLoading;
    private AssetClass _selectedAssetClass = AssetClass.Futures;
    private string _symbol = string.Empty;
    private string _tickSizeText = string.Empty;
    private string _tickValueText = string.Empty;

    public InstrumentsViewModel(
        IInstrumentReader instrumentReader,
        CreateInstrumentUseCase createInstrumentUseCase)
    {
        ArgumentNullException.ThrowIfNull(instrumentReader);
        ArgumentNullException.ThrowIfNull(createInstrumentUseCase);

        _instrumentReader = instrumentReader;
        _createInstrumentUseCase = createInstrumentUseCase;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, CanRefresh);
        ShowCreateFormCommand = new RelayCommand(ShowCreateForm, CanShowCreateForm);
        CancelCreateCommand = new RelayCommand(CancelCreate, CanCancelCreate);
        CreateInstrumentCommand = new AsyncRelayCommand(
            CreateInstrumentAsync,
            CanCreateInstrument);
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

    public bool HasError => ErrorMessage is not null;

    public bool HasInstruments => Instruments.Count > 0;

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

    public string Symbol
    {
        get => _symbol;
        set => SetProperty(ref _symbol, value);
    }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public IReadOnlyList<AssetClass> AssetClasses { get; } =
        Enum.GetValues<AssetClass>();

    public AssetClass SelectedAssetClass
    {
        get => _selectedAssetClass;
        set => SetProperty(ref _selectedAssetClass, value);
    }

    public string Exchange
    {
        get => _exchange;
        set => SetProperty(ref _exchange, value);
    }

    public string Currency
    {
        get => _currency;
        set => SetProperty(ref _currency, value);
    }

    public string TickSizeText
    {
        get => _tickSizeText;
        set => SetProperty(ref _tickSizeText, value);
    }

    public string TickValueText
    {
        get => _tickValueText;
        set => SetProperty(ref _tickValueText, value);
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

    public IAsyncRelayCommand RefreshCommand { get; }

    public IRelayCommand ShowCreateFormCommand { get; }

    public IRelayCommand CancelCreateCommand { get; }

    public IAsyncRelayCommand CreateInstrumentCommand { get; }

    public Task EnsureLoadedAsync()
    {
        return LoadAsync(forceRefresh: false, CancellationToken.None);
    }

    private Task RefreshAsync(CancellationToken cancellationToken)
    {
        return LoadAsync(forceRefresh: true, cancellationToken);
    }

    private bool CanRefresh() => !IsLoading && !IsCreating;

    private void ShowCreateForm()
    {
        CreateErrorMessage = null;
        IsCreateFormVisible = true;
    }

    private bool CanShowCreateForm() =>
        !IsCreateFormVisible &&
        !IsLoading &&
        !IsCreating;

    private void CancelCreate()
    {
        ResetCreateForm();
        IsCreateFormVisible = false;
    }

    private bool CanCancelCreate() => IsCreateFormVisible && !IsCreating;

    private bool CanCreateInstrument() =>
        IsCreateFormVisible &&
        !IsCreating &&
        !IsLoading;

    private async Task CreateInstrumentAsync(CancellationToken cancellationToken)
    {
        CreateErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Symbol))
        {
            CreateErrorMessage = "Symbol is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            CreateErrorMessage = "Display name is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Currency))
        {
            CreateErrorMessage = "Currency is required.";
            return;
        }

        if (!TryParseDecimal(TickSizeText, out decimal tickSize))
        {
            CreateErrorMessage = "Tick size must be a valid number.";
            return;
        }

        if (!TryParseDecimal(TickValueText, out decimal tickValue))
        {
            CreateErrorMessage = "Tick value must be a valid number.";
            return;
        }

        IsCreating = true;

        try
        {
            var command = new CreateInstrumentRequest(
                Symbol,
                DisplayName,
                SelectedAssetClass,
                Exchange,
                Currency,
                tickSize,
                tickValue);

            await _createInstrumentUseCase.ExecuteAsync(command, cancellationToken);
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
            CreateErrorMessage = InvalidInstrumentDetailsMessage;
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

    private static bool TryParseDecimal(string text, out decimal value)
    {
        return decimal.TryParse(
                text,
                DecimalNumberStyles,
                CultureInfo.CurrentCulture,
                out value) ||
            decimal.TryParse(
                text,
                DecimalNumberStyles,
                CultureInfo.InvariantCulture,
                out value);
    }

    private void ResetCreateForm()
    {
        Symbol = string.Empty;
        DisplayName = string.Empty;
        SelectedAssetClass = AssetClass.Futures;
        Exchange = string.Empty;
        Currency = string.Empty;
        TickSizeText = string.Empty;
        TickValueText = string.Empty;
        CreateErrorMessage = null;
    }

    private void NotifyOperationCanExecuteChanged()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        ShowCreateFormCommand.NotifyCanExecuteChanged();
        CreateInstrumentCommand.NotifyCanExecuteChanged();
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
