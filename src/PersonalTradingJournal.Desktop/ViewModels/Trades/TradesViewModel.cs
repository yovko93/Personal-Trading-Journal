using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Desktop.ViewModels.Trades;

public sealed class TradesViewModel : ObservableObject
{
    private const int RecentTradeLimit = 50;
    private const NumberStyles DecimalNumberStyles =
        NumberStyles.AllowLeadingWhite |
        NumberStyles.AllowTrailingWhite |
        NumberStyles.AllowLeadingSign |
        NumberStyles.AllowDecimalPoint;
    private const string LoadErrorMessage = "Trade reference data could not be loaded.";
    private const string TradeListLoadErrorMessage = "Trades could not be loaded.";
    private const string SaveErrorMessageFallback = "Trade could not be saved.";
    private const string StaleReferenceErrorMessage =
        "The selected trading account or instrument is no longer available. " +
        "Refresh the reference data and try again.";
    private const string TradeSavedMessage = "Trade saved successfully.";
    private static readonly string[] UtcTimestampFormats =
    [
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-dd'T'HH:mm:ss'Z'",
        "yyyy-MM-dd'T'HH:mm'Z'",
    ];

    private readonly IManualTradeReferenceDataReader _referenceDataReader;
    private readonly ITradeListReader _tradeListReader;
    private readonly CreateManualTradeUseCase _createManualTradeUseCase;
    private readonly SemaphoreSlim _referenceDataLoadGate = new(1, 1);
    private readonly SemaphoreSlim _tradeListLoadGate = new(1, 1);
    private IReadOnlyList<ManualTradeAccountOption> _accountOptions = [];
    private string _entryCommissionText = "0";
    private string _entryExecutedAtUtcText = string.Empty;
    private string _entryFeesText = "0";
    private string _entryPriceText = string.Empty;
    private string? _errorMessage;
    private string _exitCommissionText = "0";
    private string _exitExecutedAtUtcText = string.Empty;
    private string _exitFeesText = "0";
    private string _exitPriceText = string.Empty;
    private bool _hasExit;
    private bool _hasReferenceDataLoadedSuccessfully;
    private bool _hasTradeListLoadedSuccessfully;
    private IReadOnlyList<ManualTradeInstrumentOption> _instrumentOptions = [];
    private bool _isLoading;
    private bool _isTradeListLoading;
    private bool _isManualEntryVisible;
    private bool _isSaving;
    private string _quantityText = string.Empty;
    private IReadOnlyList<TradeListItem> _recentTrades = [];
    private string? _saveErrorMessage;
    private ManualTradeAccountOption? _selectedAccount;
    private TradeDirection? _selectedDirection;
    private ManualTradeInstrumentOption? _selectedInstrument;
    private string? _successMessage;
    private string? _tradeListErrorMessage;
    private string? _validationErrorMessage;

    public TradesViewModel(
        IManualTradeReferenceDataReader referenceDataReader,
        ITradeListReader tradeListReader,
        CreateManualTradeUseCase createManualTradeUseCase)
    {
        ArgumentNullException.ThrowIfNull(referenceDataReader);
        ArgumentNullException.ThrowIfNull(tradeListReader);
        ArgumentNullException.ThrowIfNull(createManualTradeUseCase);

        _referenceDataReader = referenceDataReader;
        _tradeListReader = tradeListReader;
        _createManualTradeUseCase = createManualTradeUseCase;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, CanRefresh);
        ShowManualEntryCommand = new RelayCommand(ShowManualEntry, CanShowManualEntry);
        CancelManualEntryCommand = new RelayCommand(
            CancelManualEntry,
            CanCancelManualEntry);
        SaveManualTradeCommand = new AsyncRelayCommand(
            SaveManualTradeAsync,
            CanSaveManualTrade);
    }

    public IReadOnlyList<ManualTradeAccountOption> AccountOptions
    {
        get => _accountOptions;
        private set
        {
            if (SetProperty(ref _accountOptions, value))
            {
                OnPropertyChanged(nameof(HasAccountOptions));
                OnPropertyChanged(nameof(HasReferenceData));
            }
        }
    }

    public IReadOnlyList<ManualTradeInstrumentOption> InstrumentOptions
    {
        get => _instrumentOptions;
        private set
        {
            if (SetProperty(ref _instrumentOptions, value))
            {
                OnPropertyChanged(nameof(HasInstrumentOptions));
                OnPropertyChanged(nameof(HasReferenceData));
            }
        }
    }

    public IReadOnlyList<TradeListItem> RecentTrades
    {
        get => _recentTrades;
        private set
        {
            if (SetProperty(ref _recentTrades, value))
            {
                OnPropertyChanged(nameof(HasTrades));
            }
        }
    }

    public bool HasTrades => RecentTrades.Count > 0;

    public ManualTradeAccountOption? SelectedAccount
    {
        get => _selectedAccount;
        set => SetProperty(ref _selectedAccount, value);
    }

    public ManualTradeInstrumentOption? SelectedInstrument
    {
        get => _selectedInstrument;
        set => SetProperty(ref _selectedInstrument, value);
    }

    public IReadOnlyList<TradeDirection> DirectionOptions { get; } =
        [TradeDirection.Long, TradeDirection.Short];

    public TradeDirection? SelectedDirection
    {
        get => _selectedDirection;
        set => SetProperty(ref _selectedDirection, value);
    }

    public string QuantityText
    {
        get => _quantityText;
        set => SetProperty(ref _quantityText, value);
    }

    public string EntryExecutedAtUtcText
    {
        get => _entryExecutedAtUtcText;
        set => SetProperty(ref _entryExecutedAtUtcText, value);
    }

    public string EntryPriceText
    {
        get => _entryPriceText;
        set => SetProperty(ref _entryPriceText, value);
    }

    public string EntryCommissionText
    {
        get => _entryCommissionText;
        set => SetProperty(ref _entryCommissionText, value);
    }

    public string EntryFeesText
    {
        get => _entryFeesText;
        set => SetProperty(ref _entryFeesText, value);
    }

    public bool HasExit
    {
        get => _hasExit;
        set
        {
            if (SetProperty(ref _hasExit, value) && !value)
            {
                ResetExitFields();
            }
        }
    }

    public string ExitExecutedAtUtcText
    {
        get => _exitExecutedAtUtcText;
        set => SetProperty(ref _exitExecutedAtUtcText, value);
    }

    public string ExitPriceText
    {
        get => _exitPriceText;
        set => SetProperty(ref _exitPriceText, value);
    }

    public string ExitCommissionText
    {
        get => _exitCommissionText;
        set => SetProperty(ref _exitCommissionText, value);
    }

    public string ExitFeesText
    {
        get => _exitFeesText;
        set => SetProperty(ref _exitFeesText, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                ShowManualEntryCommand.NotifyCanExecuteChanged();
                SaveManualTradeCommand.NotifyCanExecuteChanged();
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
                RefreshCommand.NotifyCanExecuteChanged();
                ShowManualEntryCommand.NotifyCanExecuteChanged();
                CancelManualEntryCommand.NotifyCanExecuteChanged();
                SaveManualTradeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsTradeListLoading
    {
        get => _isTradeListLoading;
        private set
        {
            if (SetProperty(ref _isTradeListLoading, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                SaveManualTradeCommand.NotifyCanExecuteChanged();
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

    public string? TradeListErrorMessage
    {
        get => _tradeListErrorMessage;
        private set
        {
            if (SetProperty(ref _tradeListErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasTradeListError));
            }
        }
    }

    public bool HasTradeListError => TradeListErrorMessage is not null;

    public bool HasAccountOptions => AccountOptions.Count > 0;

    public bool HasInstrumentOptions => InstrumentOptions.Count > 0;

    public bool HasReferenceData => HasAccountOptions && HasInstrumentOptions;

    public string? ValidationErrorMessage
    {
        get => _validationErrorMessage;
        private set
        {
            if (SetProperty(ref _validationErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasValidationError));
            }
        }
    }

    public bool HasValidationError => ValidationErrorMessage is not null;

    public string? SaveErrorMessage
    {
        get => _saveErrorMessage;
        private set
        {
            if (SetProperty(ref _saveErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasSaveError));
            }
        }
    }

    public bool HasSaveError => SaveErrorMessage is not null;

    public string? SuccessMessage
    {
        get => _successMessage;
        private set
        {
            if (SetProperty(ref _successMessage, value))
            {
                OnPropertyChanged(nameof(HasSuccessMessage));
            }
        }
    }

    public bool HasSuccessMessage => SuccessMessage is not null;

    public bool IsManualEntryVisible
    {
        get => _isManualEntryVisible;
        private set
        {
            if (SetProperty(ref _isManualEntryVisible, value))
            {
                ShowManualEntryCommand.NotifyCanExecuteChanged();
                CancelManualEntryCommand.NotifyCanExecuteChanged();
                SaveManualTradeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IRelayCommand ShowManualEntryCommand { get; }

    public IRelayCommand CancelManualEntryCommand { get; }

    public IAsyncRelayCommand SaveManualTradeCommand { get; }

    public bool TryBuildManualTradeCommand(
        out CreateManualTradeCommand? command)
    {
        ValidationErrorMessage = null;
        command = null;

        if (SelectedAccount is not { } selectedAccount)
        {
            return FailValidation("Trading account is required.");
        }

        if (SelectedInstrument is not { } selectedInstrument)
        {
            return FailValidation("Instrument is required.");
        }

        if (SelectedDirection is not { } direction)
        {
            return FailValidation("Direction is required.");
        }

        if (!Enum.IsDefined(direction))
        {
            return FailValidation("Direction is invalid.");
        }

        if (!TryParseDecimal(QuantityText, out decimal quantity))
        {
            return FailValidation("Quantity must be a valid number.");
        }

        if (quantity <= 0)
        {
            return FailValidation("Quantity must be greater than zero.");
        }

        if (!TryParseUtcTimestamp(
                EntryExecutedAtUtcText,
                out DateTimeOffset entryExecutedAtUtc))
        {
            return FailValidation("Entry time must be a valid UTC timestamp.");
        }

        if (!TryParseDecimal(EntryPriceText, out decimal entryPrice))
        {
            return FailValidation("Entry price must be a valid number.");
        }

        if (!TryParseNonNegativeCost(
                EntryCommissionText,
                out decimal entryCommission))
        {
            return FailValidation(
                "Entry commission must be a valid non-negative number.");
        }

        if (!TryParseNonNegativeCost(EntryFeesText, out decimal entryFees))
        {
            return FailValidation(
                "Entry fees must be a valid non-negative number.");
        }

        var entry = new ManualTradeExecutionInput(
            entryExecutedAtUtc,
            entryPrice,
            entryCommission,
            entryFees);
        ManualTradeExecutionInput? exit = null;

        if (HasExit)
        {
            if (!TryParseUtcTimestamp(
                    ExitExecutedAtUtcText,
                    out DateTimeOffset exitExecutedAtUtc))
            {
                return FailValidation("Exit time must be a valid UTC timestamp.");
            }

            if (exitExecutedAtUtc < entryExecutedAtUtc)
            {
                return FailValidation(
                    "Exit time cannot be earlier than entry time.");
            }

            if (!TryParseDecimal(ExitPriceText, out decimal exitPrice))
            {
                return FailValidation("Exit price must be a valid number.");
            }

            if (!TryParseNonNegativeCost(
                    ExitCommissionText,
                    out decimal exitCommission))
            {
                return FailValidation(
                    "Exit commission must be a valid non-negative number.");
            }

            if (!TryParseNonNegativeCost(ExitFeesText, out decimal exitFees))
            {
                return FailValidation(
                    "Exit fees must be a valid non-negative number.");
            }

            exit = new ManualTradeExecutionInput(
                exitExecutedAtUtc,
                exitPrice,
                exitCommission,
                exitFees);
        }

        command = new CreateManualTradeCommand(
            selectedAccount.Id,
            selectedInstrument.Id,
            direction,
            quantity,
            entry,
            exit);
        return true;
    }

    public async Task EnsureLoadedAsync()
    {
        _ = await LoadReferenceDataAsync(forceRefresh: false, CancellationToken.None);
        _ = await LoadTradeListAsync(forceRefresh: false, CancellationToken.None);
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        _ = await LoadReferenceDataAsync(forceRefresh: true, cancellationToken);
        _ = await LoadTradeListAsync(forceRefresh: true, cancellationToken);
    }

    private bool CanRefresh() =>
        !IsLoading && !IsTradeListLoading && !IsSaving;

    private void ShowManualEntry()
    {
        SuccessMessage = null;
        SaveErrorMessage = null;
        ValidationErrorMessage = null;
        IsManualEntryVisible = true;
    }

    private bool CanShowManualEntry() =>
        !IsManualEntryVisible && !IsLoading && !IsSaving;

    private void CancelManualEntry()
    {
        ResetManualEntryForm();
        IsManualEntryVisible = false;
    }

    private bool CanCancelManualEntry() => IsManualEntryVisible && !IsSaving;

    private bool CanSaveManualTrade() =>
        IsManualEntryVisible && !IsLoading && !IsTradeListLoading && !IsSaving;

    private async Task SaveManualTradeAsync(CancellationToken cancellationToken)
    {
        SaveErrorMessage = null;
        SuccessMessage = null;

        if (!TryBuildManualTradeCommand(out CreateManualTradeCommand? command) ||
            command is null)
        {
            return;
        }

        IsSaving = true;

        try
        {
            _ = await _createManualTradeUseCase.ExecuteAsync(
                command,
                cancellationToken);

            ResetManualEntryForm();
            IsManualEntryVisible = false;
            SuccessMessage = TradeSavedMessage;

            // The write is committed, so command cancellation must not turn a
            // best-effort projection reload into a cancelled save outcome.
            _ = await LoadTradeListAsync(
                forceRefresh: true,
                CancellationToken.None);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (KeyNotFoundException)
        {
            SaveErrorMessage = StaleReferenceErrorMessage;
        }
        catch (Exception)
        {
            SaveErrorMessage = SaveErrorMessageFallback;
        }
        finally
        {
            IsSaving = false;
        }
    }

    private bool FailValidation(string message)
    {
        ValidationErrorMessage = message;
        return false;
    }

    private static bool TryParseDecimal(string? text, out decimal value)
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

    private static bool TryParseNonNegativeCost(
        string? text,
        out decimal value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = 0;
            return true;
        }

        return TryParseDecimal(text, out value) && value >= 0;
    }

    private static bool TryParseUtcTimestamp(
        string? text,
        out DateTimeOffset value)
    {
        return DateTimeOffset.TryParseExact(
            text,
            UtcTimestampFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out value);
    }

    private void ResetManualEntryForm()
    {
        SelectedAccount = null;
        SelectedInstrument = null;
        SelectedDirection = null;
        QuantityText = string.Empty;
        EntryExecutedAtUtcText = string.Empty;
        EntryPriceText = string.Empty;
        EntryCommissionText = "0";
        EntryFeesText = "0";
        ValidationErrorMessage = null;
        SaveErrorMessage = null;
        if (HasExit)
        {
            HasExit = false;
        }
        else
        {
            ResetExitFields();
        }
    }

    private void ResetExitFields()
    {
        ExitExecutedAtUtcText = string.Empty;
        ExitPriceText = string.Empty;
        ExitCommissionText = "0";
        ExitFeesText = "0";
    }

    private async Task<bool> LoadReferenceDataAsync(
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        if (!await _referenceDataLoadGate.WaitAsync(0, cancellationToken))
        {
            return false;
        }

        try
        {
            if (!forceRefresh && _hasReferenceDataLoadedSuccessfully)
            {
                return true;
            }

            IsLoading = true;
            ErrorMessage = null;

            try
            {
                ManualTradeReferenceData referenceData =
                    await _referenceDataReader.GetAsync(
                        includeInactiveReferences: false,
                        cancellationToken);

                Guid? selectedAccountId = SelectedAccount?.Id;
                Guid? selectedInstrumentId = SelectedInstrument?.Id;
                AccountOptions = referenceData.Accounts;
                InstrumentOptions = referenceData.Instruments;
                SelectedAccount = selectedAccountId.HasValue
                    ? AccountOptions.SingleOrDefault(
                        option => option.Id == selectedAccountId.Value)
                    : null;
                SelectedInstrument = selectedInstrumentId.HasValue
                    ? InstrumentOptions.SingleOrDefault(
                        option => option.Id == selectedInstrumentId.Value)
                    : null;
                _hasReferenceDataLoadedSuccessfully = true;
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
            _referenceDataLoadGate.Release();
        }
    }

    private async Task<bool> LoadTradeListAsync(
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        if (!await _tradeListLoadGate.WaitAsync(0, cancellationToken))
        {
            return false;
        }

        try
        {
            if (!forceRefresh && _hasTradeListLoadedSuccessfully)
            {
                return true;
            }

            IsTradeListLoading = true;
            TradeListErrorMessage = null;

            try
            {
                IReadOnlyList<TradeListItem> recentTrades =
                    await _tradeListReader.GetRecentAsync(
                        RecentTradeLimit,
                        cancellationToken);

                RecentTrades = recentTrades;
                _hasTradeListLoadedSuccessfully = true;
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                TradeListErrorMessage = TradeListLoadErrorMessage;
                return false;
            }
            finally
            {
                IsTradeListLoading = false;
            }
        }
        finally
        {
            _tradeListLoadGate.Release();
        }
    }
}
