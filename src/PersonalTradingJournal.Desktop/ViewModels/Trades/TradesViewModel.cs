using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Desktop.ViewModels.Trades;

public sealed class TradesViewModel : ObservableObject
{
    private const NumberStyles DecimalNumberStyles =
        NumberStyles.AllowLeadingWhite |
        NumberStyles.AllowTrailingWhite |
        NumberStyles.AllowLeadingSign |
        NumberStyles.AllowDecimalPoint;
    private const string LoadErrorMessage = "Trade reference data could not be loaded.";
    private static readonly string[] UtcTimestampFormats =
    [
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-dd'T'HH:mm:ss'Z'",
        "yyyy-MM-dd'T'HH:mm'Z'",
    ];

    private readonly IManualTradeReferenceDataReader _referenceDataReader;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
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
    private bool _hasLoadedSuccessfully;
    private IReadOnlyList<ManualTradeInstrumentOption> _instrumentOptions = [];
    private bool _isLoading;
    private bool _isManualEntryVisible;
    private string _quantityText = string.Empty;
    private ManualTradeAccountOption? _selectedAccount;
    private TradeDirection? _selectedDirection;
    private ManualTradeInstrumentOption? _selectedInstrument;
    private string? _validationErrorMessage;

    public TradesViewModel(IManualTradeReferenceDataReader referenceDataReader)
    {
        ArgumentNullException.ThrowIfNull(referenceDataReader);

        _referenceDataReader = referenceDataReader;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, CanRefresh);
        ShowManualEntryCommand = new RelayCommand(ShowManualEntry, CanShowManualEntry);
        CancelManualEntryCommand = new RelayCommand(
            CancelManualEntry,
            CanCancelManualEntry);
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

    public bool IsManualEntryVisible
    {
        get => _isManualEntryVisible;
        private set
        {
            if (SetProperty(ref _isManualEntryVisible, value))
            {
                ShowManualEntryCommand.NotifyCanExecuteChanged();
                CancelManualEntryCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IRelayCommand ShowManualEntryCommand { get; }

    public IRelayCommand CancelManualEntryCommand { get; }

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
        _ = await LoadAsync(forceRefresh: false, CancellationToken.None);
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        _ = await LoadAsync(forceRefresh: true, cancellationToken);
    }

    private bool CanRefresh() => !IsLoading;

    private void ShowManualEntry()
    {
        IsManualEntryVisible = true;
    }

    private bool CanShowManualEntry() => !IsManualEntryVisible && !IsLoading;

    private void CancelManualEntry()
    {
        ResetManualEntryForm();
        IsManualEntryVisible = false;
    }

    private bool CanCancelManualEntry() => IsManualEntryVisible;

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
