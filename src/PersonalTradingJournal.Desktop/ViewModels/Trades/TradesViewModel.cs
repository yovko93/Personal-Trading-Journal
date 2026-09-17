using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Mistakes;
using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.Dialogs;
using PersonalTradingJournal.Desktop.Screenshots;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Screenshots;
using PersonalTradingJournal.Domain.Trades;
using System.Windows.Media;

namespace PersonalTradingJournal.Desktop.ViewModels.Trades;

internal enum ManualTradeInputField
{
    Account,
    Instrument,
    Direction,
    Quantity,
    EntryExecutedAtUtc,
    EntryPrice,
    EntryCommission,
    EntryFees,
    ExitExecutedAtUtc,
    ExitPrice,
    ExitCommission,
    ExitFees,
}

public sealed class TradesViewModel : ObservableObject
{
    private const int RecentTradeLimit = 50;
    private const NumberStyles DecimalNumberStyles =
        NumberStyles.AllowLeadingWhite |
        NumberStyles.AllowTrailingWhite |
        NumberStyles.AllowLeadingSign |
        NumberStyles.AllowDecimalPoint;
    private const string LoadErrorMessage = "Trade reference data could not be loaded.";
    private const string TradeDetailLoadErrorMessage =
        "Trade details could not be loaded.";
    private const string TradeListLoadErrorMessage = "Trades could not be loaded.";
    private const string SaveErrorMessageFallback = "Trade could not be saved.";
    private const string StaleReferenceErrorMessage =
        "The selected trading account or instrument is no longer available. " +
        "Refresh the reference data and try again.";
    private const string MissingTradingSetupErrorMessage =
        "The selected trading setup is no longer available.";
    private const string InactiveTradingSetupErrorMessage =
        "The selected trading setup is inactive.";
    private const string TradingSetupSaveErrorMessageFallback =
        "Trading setup could not be saved.";
    private const string TradingSetupSavedMessage =
        "Trading setup updated successfully.";
    private const string TradeMistakesLoadErrorMessage =
        "Trade mistakes could not be loaded.";
    private const string TradingMistakeOptionsLoadErrorMessage =
        "Trading mistake options could not be loaded.";
    private const string AssignMistakeErrorMessageFallback =
        "Trading mistake could not be assigned.";
    private const string MissingTradingMistakeErrorMessage =
        "The selected trading mistake is no longer available.";
    private const string InactiveTradingMistakeErrorMessage =
        "The selected trading mistake is inactive.";
    private const string DuplicateTradingMistakeErrorMessage =
        "This trading mistake is already assigned to the trade.";
    private const string TradingMistakeAssignedMessage =
        "Trading mistake assigned successfully.";
    private const string RemoveMistakeErrorMessageFallback =
        "Trading mistake could not be removed.";
    private const string MissingTradeMistakeErrorMessage =
        "The selected trade mistake is no longer available.";
    private const string TradingMistakeRemovedMessage =
        "Trading mistake removed successfully.";
    private const string TradeSavedMessage = "Trade saved successfully.";
    private const string CloseTradeSaveErrorMessageFallback =
        "Trade could not be closed.";
    private const string StaleCloseTradeErrorMessage =
        "The selected trade is no longer available.";
    private const string AlreadyClosedTradeErrorMessage =
        "The trade is already closed.";
    private const string CloseTradeChronologyErrorMessage =
        "Exit time cannot be earlier than the latest execution.";
    private const string TradeClosedMessage = "Trade closed successfully.";
    private const string TradeUpdatedMessage = "Trade updated successfully.";
    private const string TradeUpdateErrorMessageFallback =
        "Trade changes could not be saved.";
    private const string TradeDeleteErrorMessageFallback =
        "Trade could not be deleted.";
    private const string TradeDeleteCleanupWarningMessage =
        "Trade deleted, but one or more local screenshot files could not be cleaned up.";
    private const string TradeScreenshotsLoadErrorMessage =
        "Screenshots could not be loaded.";
    private const string ScreenshotSaveErrorMessageFallback =
        "Screenshot could not be added.";
    private const string StaleTradeScreenshotErrorMessage =
        "The selected trade is no longer available.";
    private const string ScreenshotSavedMessage = "Screenshot added successfully.";
    private const string ScreenshotPreviewErrorMessageFallback =
        "Screenshot could not be opened.";
    private const string StaleScreenshotPreviewErrorMessage =
        "The screenshot is no longer available.";
    private const string MissingScreenshotFileErrorMessage =
        "The screenshot file is missing.";
    private const string ScreenshotDeleteErrorMessageFallback =
        "Screenshot could not be deleted.";
    private const string StaleScreenshotDeleteErrorMessage =
        "The screenshot is no longer available.";
    private const string ScreenshotDeletedMessage =
        "Screenshot deleted successfully.";
    private const string ScreenshotCleanupWarningMessage =
        "Screenshot was removed, but its local file could not be cleaned up.";
    private static readonly string[] UtcTimestampFormats =
    [
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-dd'T'HH:mm:ss'Z'",
        "yyyy-MM-dd'T'HH:mm'Z'",
    ];

    private readonly IManualTradeReferenceDataReader _referenceDataReader;
    private readonly ITradingSetupReader _tradingSetupReader;
    private readonly ITradingMistakeReader _tradingMistakeReader;
    private readonly ITradeMistakeReader _tradeMistakeReader;
    private readonly ITradeDetailReader _tradeDetailReader;
    private readonly ITradeListReader _tradeListReader;
    private readonly ITradeScreenshotReader _tradeScreenshotReader;
    private readonly ITradeScreenshotContentReader _tradeScreenshotContentReader;
    private readonly AddTradeScreenshotUseCase _addTradeScreenshotUseCase;
    private readonly ITradeScreenshotFilePicker _tradeScreenshotFilePicker;
    private readonly ITradeScreenshotImageDecoder _tradeScreenshotImageDecoder;
    private readonly DeleteTradeScreenshotUseCase _deleteTradeScreenshotUseCase;
    private readonly ITradeScreenshotDeleteConfirmation
        _tradeScreenshotDeleteConfirmation;
    private readonly CreateManualTradeUseCase _createManualTradeUseCase;
    private readonly SetTradeTradingSetupUseCase _setTradeTradingSetupUseCase;
    private readonly AssignTradeMistakeUseCase _assignTradeMistakeUseCase;
    private readonly RemoveTradeMistakeUseCase _removeTradeMistakeUseCase;
    private readonly CloseManualTradeUseCase _closeManualTradeUseCase;
    private readonly UpdateTradeUseCase? _updateTradeUseCase;
    private readonly DeleteTradeUseCase? _deleteTradeUseCase;
    private readonly IDialogService? _dialogService;
    private readonly SemaphoreSlim _referenceDataLoadGate = new(1, 1);
    private readonly SemaphoreSlim _tradeListLoadGate = new(1, 1);
    private readonly SemaphoreSlim _tradingMistakeOptionsLoadGate = new(1, 1);
    private IReadOnlyList<ManualTradeAccountOption> _accountOptions = [];
    private IReadOnlyList<ManualTradeAccountOption> _allAccountOptions = [];
    private IReadOnlyList<TradingSetupListItem> _allTradingSetups = [];
    private IReadOnlyList<TradingSetupListItem> _availableTradingSetups = [];
    private IReadOnlyList<TradingMistakeListItem> _allTradingMistakes = [];
    private IReadOnlyList<TradingMistakeListItem> _availableTradingMistakes = [];
    private IReadOnlyList<TradeMistakeListItem> _tradeMistakes = [];
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
    private bool _hasTradingMistakeOptionsLoadedSuccessfully;
    private Guid? _loadedTradeMistakesTradeId;
    private Guid? _loadedTradeScreenshotsTradeId;
    private IReadOnlyList<ManualTradeInstrumentOption> _instrumentOptions = [];
    private IReadOnlyList<ManualTradeInstrumentOption> _allInstrumentOptions = [];
    private bool _isLoading;
    private bool _isTradeDetailLoading;
    private bool _isTradeDetailNotFound;
    private bool _isTradeDetailVisible;
    private bool _isTradeListLoading;
    private bool _isManualEntryVisible;
    private bool _isSaving;
    private bool _isAddScreenshotVisible;
    private bool _isAddingScreenshot;
    private bool _isTradeScreenshotsLoading;
    private ManualTradeInputField? _invalidManualTradeInput;
    private string _quantityText = string.Empty;
    private IReadOnlyList<TradeListItem> _recentTrades = [];
    private string? _saveErrorMessage;
    private ManualTradeAccountOption? _selectedAccount;
    private TradeDirection? _selectedDirection;
    private ManualTradeInstrumentOption? _selectedInstrument;
    private TradingSetupListItem? _selectedTradingSetup;
    private IReadOnlyList<TradingSetupListItem> _tradeDetailTradingSetupOptions = [];
    private TradingSetupListItem? _selectedTradeDetailTradingSetup;
    private bool _isTradingSetupSaving;
    private string? _tradingSetupSaveErrorMessage;
    private string? _tradingSetupSuccessMessage;
    private TradingMistakeListItem? _selectedTradingMistake;
    private string _tradeMistakeNoteText = string.Empty;
    private bool _isTradeMistakesLoading;
    private bool _isTradingMistakeOptionsLoading;
    private bool _isAssigningMistake;
    private bool _isRemovingMistake;
    private string? _tradeMistakesErrorMessage;
    private string? _tradingMistakeOptionsErrorMessage;
    private string? _assignMistakeErrorMessage;
    private string? _assignMistakeSuccessMessage;
    private string? _removeMistakeErrorMessage;
    private string? _removeMistakeSuccessMessage;
    private TradeDetail? _selectedTradeDetail;
    private string? _successMessage;
    private string? _tradeDetailErrorMessage;
    private string? _tradeListErrorMessage;
    private string? _validationErrorMessage;
    private IReadOnlyList<TradeScreenshotListItem> _tradeScreenshots = [];
    private string? _tradeScreenshotsErrorMessage;
    private TradeScreenshotType? _selectedScreenshotType;
    private string _screenshotCapturedAtUtcText = string.Empty;
    private string _screenshotTimeframeText = string.Empty;
    private string _screenshotDescriptionText = string.Empty;
    private string? _screenshotValidationErrorMessage;
    private string? _screenshotSaveErrorMessage;
    private string? _screenshotSuccessMessage;
    private Guid? _previewScreenshotId;
    private string? _previewScreenshotFileName;
    private ImageSource? _previewScreenshotImage;
    private bool _isScreenshotPreviewVisible;
    private bool _isScreenshotPreviewLoading;
    private string? _screenshotPreviewErrorMessage;
    private int _previewRequestVersion;
    private bool _isDeletingScreenshot;
    private string? _screenshotDeleteErrorMessage;
    private string? _screenshotDeleteWarningMessage;
    private string? _screenshotDeleteSuccessMessage;
    private bool _isCloseTradeVisible;
    private bool _isClosingTrade;
    private string _closeTradeExecutedAtUtcText = string.Empty;
    private string _closeTradePriceText = string.Empty;
    private string _closeTradeCommissionText = "0";
    private string _closeTradeFeesText = "0";
    private string? _closeTradeValidationErrorMessage;
    private string? _closeTradeSaveErrorMessage;
    private string? _closeTradeSuccessMessage;
    private bool _isTradeEditVisible;
    private bool _isUpdatingTrade;
    private bool _isDeletingTrade;
    private Guid? _editingTradeId;
    private Guid _editingEntryExecutionId;
    private Guid _editingExitExecutionId;
    private string? _tradeUpdateErrorMessage;
    private string? _tradeUpdateSuccessMessage;
    private string? _tradeDeleteErrorMessage;
    private string? _tradeDeleteWarningMessage;

    public TradesViewModel(
        IManualTradeReferenceDataReader referenceDataReader,
        ITradingSetupReader tradingSetupReader,
        ITradingMistakeReader tradingMistakeReader,
        ITradeMistakeReader tradeMistakeReader,
        ITradeListReader tradeListReader,
        ITradeDetailReader tradeDetailReader,
        CreateManualTradeUseCase createManualTradeUseCase,
        SetTradeTradingSetupUseCase setTradeTradingSetupUseCase,
        AssignTradeMistakeUseCase assignTradeMistakeUseCase,
        RemoveTradeMistakeUseCase removeTradeMistakeUseCase,
        CloseManualTradeUseCase closeManualTradeUseCase,
        ITradeScreenshotReader tradeScreenshotReader,
        AddTradeScreenshotUseCase addTradeScreenshotUseCase,
        ITradeScreenshotFilePicker tradeScreenshotFilePicker,
        ITradeScreenshotContentReader tradeScreenshotContentReader,
        ITradeScreenshotImageDecoder tradeScreenshotImageDecoder,
        DeleteTradeScreenshotUseCase deleteTradeScreenshotUseCase,
        ITradeScreenshotDeleteConfirmation tradeScreenshotDeleteConfirmation,
        UpdateTradeUseCase? updateTradeUseCase = null,
        DeleteTradeUseCase? deleteTradeUseCase = null,
        IDialogService? dialogService = null)
    {
        ArgumentNullException.ThrowIfNull(referenceDataReader);
        ArgumentNullException.ThrowIfNull(tradingSetupReader);
        ArgumentNullException.ThrowIfNull(tradingMistakeReader);
        ArgumentNullException.ThrowIfNull(tradeMistakeReader);
        ArgumentNullException.ThrowIfNull(tradeListReader);
        ArgumentNullException.ThrowIfNull(tradeDetailReader);
        ArgumentNullException.ThrowIfNull(createManualTradeUseCase);
        ArgumentNullException.ThrowIfNull(setTradeTradingSetupUseCase);
        ArgumentNullException.ThrowIfNull(assignTradeMistakeUseCase);
        ArgumentNullException.ThrowIfNull(removeTradeMistakeUseCase);
        ArgumentNullException.ThrowIfNull(closeManualTradeUseCase);
        ArgumentNullException.ThrowIfNull(tradeScreenshotReader);
        ArgumentNullException.ThrowIfNull(addTradeScreenshotUseCase);
        ArgumentNullException.ThrowIfNull(tradeScreenshotFilePicker);
        ArgumentNullException.ThrowIfNull(tradeScreenshotContentReader);
        ArgumentNullException.ThrowIfNull(tradeScreenshotImageDecoder);
        ArgumentNullException.ThrowIfNull(deleteTradeScreenshotUseCase);
        ArgumentNullException.ThrowIfNull(tradeScreenshotDeleteConfirmation);

        _referenceDataReader = referenceDataReader;
        _tradingSetupReader = tradingSetupReader;
        _tradingMistakeReader = tradingMistakeReader;
        _tradeMistakeReader = tradeMistakeReader;
        _tradeListReader = tradeListReader;
        _tradeDetailReader = tradeDetailReader;
        _createManualTradeUseCase = createManualTradeUseCase;
        _setTradeTradingSetupUseCase = setTradeTradingSetupUseCase;
        _assignTradeMistakeUseCase = assignTradeMistakeUseCase;
        _removeTradeMistakeUseCase = removeTradeMistakeUseCase;
        _closeManualTradeUseCase = closeManualTradeUseCase;
        _tradeScreenshotReader = tradeScreenshotReader;
        _addTradeScreenshotUseCase = addTradeScreenshotUseCase;
        _tradeScreenshotFilePicker = tradeScreenshotFilePicker;
        _tradeScreenshotContentReader = tradeScreenshotContentReader;
        _tradeScreenshotImageDecoder = tradeScreenshotImageDecoder;
        _deleteTradeScreenshotUseCase = deleteTradeScreenshotUseCase;
        _tradeScreenshotDeleteConfirmation = tradeScreenshotDeleteConfirmation;
        _updateTradeUseCase = updateTradeUseCase;
        _deleteTradeUseCase = deleteTradeUseCase;
        _dialogService = dialogService;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, CanRefresh);
        ShowManualEntryCommand = new RelayCommand(ShowManualEntry, CanShowManualEntry);
        CancelManualEntryCommand = new RelayCommand(
            CancelManualEntry,
            CanCancelManualEntry);
        SaveManualTradeCommand = new AsyncRelayCommand(
            SaveManualTradeAsync,
            CanSaveManualTrade);
        SaveTradingSetupCommand = new AsyncRelayCommand(
            SaveTradingSetupAsync,
            CanSaveTradingSetup);
        ClearTradingSetupCommand = new AsyncRelayCommand(
            ClearTradingSetupAsync,
            CanClearTradingSetup);
        AssignMistakeCommand = new AsyncRelayCommand(
            AssignMistakeAsync,
            CanAssignMistake);
        RemoveMistakeCommand = new AsyncRelayCommand<TradeMistakeListItem>(
            RemoveMistakeAsync,
            CanRemoveMistake);
        ShowTradeDetailCommand = new AsyncRelayCommand<TradeListItem>(
            ShowTradeDetailAsync,
            CanShowTradeDetail);
        CloseTradeDetailCommand = new RelayCommand(
            CloseTradeDetail,
            CanCloseTradeDetail);
        ShowCloseTradeCommand = new RelayCommand(
            ShowCloseTrade,
            CanShowCloseTrade);
        CancelCloseTradeCommand = new RelayCommand(
            CancelCloseTrade,
            CanCancelCloseTrade);
        SaveCloseTradeCommand = new AsyncRelayCommand(
            SaveCloseTradeAsync,
            CanSaveCloseTrade);
        ShowAddScreenshotCommand = new RelayCommand(
            ShowAddScreenshot,
            CanShowAddScreenshot);
        CancelAddScreenshotCommand = new RelayCommand(
            CancelAddScreenshot,
            CanCancelAddScreenshot);
        SaveScreenshotCommand = new AsyncRelayCommand(
            SaveScreenshotAsync,
            CanSaveScreenshot);
        OpenScreenshotPreviewCommand =
            new AsyncRelayCommand<TradeScreenshotListItem>(
                OpenScreenshotPreviewAsync,
                CanOpenScreenshotPreview);
        CloseScreenshotPreviewCommand = new RelayCommand(
            CloseScreenshotPreview,
            CanCloseScreenshotPreview);
        DeleteScreenshotCommand =
            new AsyncRelayCommand<TradeScreenshotListItem>(
                DeleteScreenshotAsync,
                CanDeleteScreenshot);
        ShowTradeEditCommand = new AsyncRelayCommand<TradeListItem>(
            ShowTradeEditAsync,
            CanShowTradeEdit);
        ShowSelectedTradeEditCommand = new AsyncRelayCommand(
            ShowSelectedTradeEditAsync,
            CanShowSelectedTradeEdit);
        CancelTradeEditCommand = new RelayCommand(
            CancelTradeEdit,
            CanCancelTradeEdit);
        SaveTradeEditCommand = new AsyncRelayCommand(
            SaveTradeEditAsync,
            CanSaveTradeEdit);
        DeleteTradeCommand = new AsyncRelayCommand<TradeListItem>(
            DeleteTradeAsync,
            CanDeleteTrade);
        DeleteSelectedTradeCommand = new AsyncRelayCommand(
            DeleteSelectedTradeAsync,
            CanDeleteSelectedTrade);
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

    public IReadOnlyList<TradingSetupListItem> AvailableTradingSetups
    {
        get => _availableTradingSetups;
        private set => SetProperty(ref _availableTradingSetups, value);
    }

    public TradingSetupListItem? SelectedTradingSetup
    {
        get => _selectedTradingSetup;
        set => SetProperty(ref _selectedTradingSetup, value);
    }

    public IReadOnlyList<TradingSetupListItem> TradeDetailTradingSetupOptions
    {
        get => _tradeDetailTradingSetupOptions;
        private set => SetProperty(ref _tradeDetailTradingSetupOptions, value);
    }

    public TradingSetupListItem? SelectedTradeDetailTradingSetup
    {
        get => _selectedTradeDetailTradingSetup;
        set
        {
            if (SetProperty(ref _selectedTradeDetailTradingSetup, value))
            {
                SaveTradingSetupCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public IReadOnlyList<TradingMistakeListItem> AvailableTradingMistakes
    {
        get => _availableTradingMistakes;
        private set
        {
            if (SetProperty(ref _availableTradingMistakes, value))
            {
                OnPropertyChanged(nameof(HasAvailableTradingMistakes));
                AssignMistakeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasAvailableTradingMistakes =>
        AvailableTradingMistakes.Count > 0;

    public TradingMistakeListItem? SelectedTradingMistake
    {
        get => _selectedTradingMistake;
        set
        {
            if (SetProperty(ref _selectedTradingMistake, value))
            {
                AssignMistakeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string TradeMistakeNoteText
    {
        get => _tradeMistakeNoteText;
        set => SetProperty(ref _tradeMistakeNoteText, value);
    }

    public IReadOnlyList<TradeMistakeListItem> TradeMistakes
    {
        get => _tradeMistakes;
        private set
        {
            if (SetProperty(ref _tradeMistakes, value))
            {
                OnPropertyChanged(nameof(HasTradeMistakes));
                SynchronizeAvailableTradingMistakes();
                RemoveMistakeCommand.NotifyCanExecuteChanged();
                ShowSelectedTradeEditCommand.NotifyCanExecuteChanged();
                DeleteSelectedTradeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasTradeMistakes => TradeMistakes.Count > 0;

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

    public TradeDetail? SelectedTradeDetail
    {
        get => _selectedTradeDetail;
        private set
        {
            if (SetProperty(ref _selectedTradeDetail, value))
            {
                OnPropertyChanged(nameof(IsSelectedTradeOpen));
                ShowCloseTradeCommand.NotifyCanExecuteChanged();
                SaveCloseTradeCommand.NotifyCanExecuteChanged();
                ShowAddScreenshotCommand.NotifyCanExecuteChanged();
                SaveScreenshotCommand.NotifyCanExecuteChanged();
                OpenScreenshotPreviewCommand.NotifyCanExecuteChanged();
                DeleteScreenshotCommand.NotifyCanExecuteChanged();
                SynchronizeTradeDetailTradingSetupOptions();
                SynchronizeAvailableTradingMistakes();
                SaveTradingSetupCommand.NotifyCanExecuteChanged();
                ClearTradingSetupCommand.NotifyCanExecuteChanged();
                AssignMistakeCommand.NotifyCanExecuteChanged();
                RemoveMistakeCommand.NotifyCanExecuteChanged();
                ShowTradeEditCommand.NotifyCanExecuteChanged();
                ShowSelectedTradeEditCommand.NotifyCanExecuteChanged();
                SaveTradeEditCommand.NotifyCanExecuteChanged();
                DeleteTradeCommand.NotifyCanExecuteChanged();
                DeleteSelectedTradeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsSelectedTradeOpen =>
        SelectedTradeDetail?.Status == TradeStatus.Open;

    public string CloseTradeExecutedAtUtcText
    {
        get => _closeTradeExecutedAtUtcText;
        set => SetProperty(ref _closeTradeExecutedAtUtcText, value);
    }

    public string CloseTradePriceText
    {
        get => _closeTradePriceText;
        set => SetProperty(ref _closeTradePriceText, value);
    }

    public string CloseTradeCommissionText
    {
        get => _closeTradeCommissionText;
        set => SetProperty(ref _closeTradeCommissionText, value);
    }

    public string CloseTradeFeesText
    {
        get => _closeTradeFeesText;
        set => SetProperty(ref _closeTradeFeesText, value);
    }

    public IReadOnlyList<TradeScreenshotListItem> TradeScreenshots
    {
        get => _tradeScreenshots;
        private set
        {
            if (SetProperty(ref _tradeScreenshots, value))
            {
                OnPropertyChanged(nameof(HasTradeScreenshots));
            }
        }
    }

    public bool HasTradeScreenshots => TradeScreenshots.Count > 0;

    public IReadOnlyList<TradeScreenshotType> ScreenshotTypeOptions { get; } =
    [
        TradeScreenshotType.PreTrade,
        TradeScreenshotType.Entry,
        TradeScreenshotType.Management,
        TradeScreenshotType.Exit,
        TradeScreenshotType.PostTrade,
        TradeScreenshotType.Other,
    ];

    public TradeScreenshotType? SelectedScreenshotType
    {
        get => _selectedScreenshotType;
        set => SetProperty(ref _selectedScreenshotType, value);
    }

    public string ScreenshotCapturedAtUtcText
    {
        get => _screenshotCapturedAtUtcText;
        set => SetProperty(ref _screenshotCapturedAtUtcText, value);
    }

    public string ScreenshotTimeframeText
    {
        get => _screenshotTimeframeText;
        set => SetProperty(ref _screenshotTimeframeText, value);
    }

    public string ScreenshotDescriptionText
    {
        get => _screenshotDescriptionText;
        set => SetProperty(ref _screenshotDescriptionText, value);
    }

    public ManualTradeAccountOption? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            if (SetProperty(ref _selectedAccount, value))
            {
                ClearManualTradeInputError(ManualTradeInputField.Account);
            }
        }
    }

    public ManualTradeInstrumentOption? SelectedInstrument
    {
        get => _selectedInstrument;
        set
        {
            if (SetProperty(ref _selectedInstrument, value))
            {
                OnPropertyChanged(nameof(IsSelectedInstrumentFutures));
                OnPropertyChanged(nameof(QuantityLabel));
                OnPropertyChanged(nameof(QuantityHint));
                OnPropertyChanged(nameof(SelectedInstrumentEconomicsText));
                ClearManualTradeInputError(ManualTradeInputField.Instrument);
                ClearManualTradeInputError(ManualTradeInputField.Quantity);
            }
        }
    }

    public bool IsSelectedInstrumentFutures =>
        SelectedInstrument?.AssetClass == AssetClass.Futures;

    public string QuantityLabel =>
        IsSelectedInstrumentFutures ? "Contracts" : "Quantity";

    public string QuantityHint => IsSelectedInstrumentFutures
        ? "Whole contracts only, e.g. 1, 2, 3"
        : "Enter the traded quantity.";

    public string? SelectedInstrumentEconomicsText =>
        SelectedInstrument is { } instrument
            ? $"{instrument.Symbol} — {instrument.DisplayName}{Environment.NewLine}" +
              $"{instrument.PointValue.ToString("G29", CultureInfo.InvariantCulture)} " +
              $"{instrument.Currency} / point"
            : null;

    public IReadOnlyList<TradeDirection> DirectionOptions { get; } =
        [TradeDirection.Long, TradeDirection.Short];

    public TradeDirection? SelectedDirection
    {
        get => _selectedDirection;
        set
        {
            if (SetProperty(ref _selectedDirection, value))
            {
                ClearManualTradeInputError(ManualTradeInputField.Direction);
            }
        }
    }

    public string QuantityText
    {
        get => _quantityText;
        set
        {
            if (SetProperty(ref _quantityText, value))
            {
                ClearManualTradeInputError(ManualTradeInputField.Quantity);
            }
        }
    }

    public string EntryExecutedAtUtcText
    {
        get => _entryExecutedAtUtcText;
        set
        {
            if (SetProperty(ref _entryExecutedAtUtcText, value))
            {
                ClearManualTradeInputError(ManualTradeInputField.EntryExecutedAtUtc);
                ClearManualTradeInputError(ManualTradeInputField.ExitExecutedAtUtc);
            }
        }
    }

    public string EntryPriceText
    {
        get => _entryPriceText;
        set
        {
            if (SetProperty(ref _entryPriceText, value))
            {
                ClearManualTradeInputError(ManualTradeInputField.EntryPrice);
            }
        }
    }

    public string EntryCommissionText
    {
        get => _entryCommissionText;
        set
        {
            if (SetProperty(ref _entryCommissionText, value))
            {
                ClearManualTradeInputError(ManualTradeInputField.EntryCommission);
            }
        }
    }

    public string EntryFeesText
    {
        get => _entryFeesText;
        set
        {
            if (SetProperty(ref _entryFeesText, value))
            {
                ClearManualTradeInputError(ManualTradeInputField.EntryFees);
            }
        }
    }

    public bool HasExit
    {
        get => _hasExit;
        set
        {
            if (SetProperty(ref _hasExit, value) && !value)
            {
                ResetExitFields();
                if (_invalidManualTradeInput is >= ManualTradeInputField.ExitExecutedAtUtc)
                {
                    SetInvalidManualTradeInput(null);
                    ValidationErrorMessage = null;
                }
            }
        }
    }

    public string ExitExecutedAtUtcText
    {
        get => _exitExecutedAtUtcText;
        set
        {
            if (SetProperty(ref _exitExecutedAtUtcText, value))
            {
                ClearManualTradeInputError(ManualTradeInputField.ExitExecutedAtUtc);
            }
        }
    }

    public string ExitPriceText
    {
        get => _exitPriceText;
        set
        {
            if (SetProperty(ref _exitPriceText, value))
            {
                ClearManualTradeInputError(ManualTradeInputField.ExitPrice);
            }
        }
    }

    public string ExitCommissionText
    {
        get => _exitCommissionText;
        set
        {
            if (SetProperty(ref _exitCommissionText, value))
            {
                ClearManualTradeInputError(ManualTradeInputField.ExitCommission);
            }
        }
    }

    public string ExitFeesText
    {
        get => _exitFeesText;
        set
        {
            if (SetProperty(ref _exitFeesText, value))
            {
                ClearManualTradeInputError(ManualTradeInputField.ExitFees);
            }
        }
    }

    public bool IsTradingAccountInvalid => _invalidManualTradeInput == ManualTradeInputField.Account;
    public bool IsInstrumentInvalid => _invalidManualTradeInput == ManualTradeInputField.Instrument;
    public bool IsDirectionInvalid => _invalidManualTradeInput == ManualTradeInputField.Direction;
    public bool IsQuantityInvalid => _invalidManualTradeInput == ManualTradeInputField.Quantity;
    public bool IsEntryExecutedAtUtcInvalid => _invalidManualTradeInput == ManualTradeInputField.EntryExecutedAtUtc;
    public bool IsEntryPriceInvalid => _invalidManualTradeInput == ManualTradeInputField.EntryPrice;
    public bool IsEntryCommissionInvalid => _invalidManualTradeInput == ManualTradeInputField.EntryCommission;
    public bool IsEntryFeesInvalid => _invalidManualTradeInput == ManualTradeInputField.EntryFees;
    public bool IsExitExecutedAtUtcInvalid => _invalidManualTradeInput == ManualTradeInputField.ExitExecutedAtUtc;
    public bool IsExitPriceInvalid => _invalidManualTradeInput == ManualTradeInputField.ExitPrice;
    public bool IsExitCommissionInvalid => _invalidManualTradeInput == ManualTradeInputField.ExitCommission;
    public bool IsExitFeesInvalid => _invalidManualTradeInput == ManualTradeInputField.ExitFees;

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
                NotifyTradeLifecycleCommands();
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
                ShowTradeDetailCommand.NotifyCanExecuteChanged();
                ShowAddScreenshotCommand.NotifyCanExecuteChanged();
                SaveScreenshotCommand.NotifyCanExecuteChanged();
                DeleteScreenshotCommand.NotifyCanExecuteChanged();
                ShowCloseTradeCommand.NotifyCanExecuteChanged();
                SaveCloseTradeCommand.NotifyCanExecuteChanged();
                SaveTradingSetupCommand.NotifyCanExecuteChanged();
                ClearTradingSetupCommand.NotifyCanExecuteChanged();
                AssignMistakeCommand.NotifyCanExecuteChanged();
                RemoveMistakeCommand.NotifyCanExecuteChanged();
                NotifyTradeLifecycleCommands();
            }
        }
    }

    public bool IsTradingSetupSaving
    {
        get => _isTradingSetupSaving;
        private set
        {
            if (SetProperty(ref _isTradingSetupSaving, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                ShowManualEntryCommand.NotifyCanExecuteChanged();
                CancelManualEntryCommand.NotifyCanExecuteChanged();
                SaveManualTradeCommand.NotifyCanExecuteChanged();
                ShowTradeDetailCommand.NotifyCanExecuteChanged();
                CloseTradeDetailCommand.NotifyCanExecuteChanged();
                ShowCloseTradeCommand.NotifyCanExecuteChanged();
                CancelCloseTradeCommand.NotifyCanExecuteChanged();
                SaveCloseTradeCommand.NotifyCanExecuteChanged();
                ShowAddScreenshotCommand.NotifyCanExecuteChanged();
                CancelAddScreenshotCommand.NotifyCanExecuteChanged();
                SaveScreenshotCommand.NotifyCanExecuteChanged();
                OpenScreenshotPreviewCommand.NotifyCanExecuteChanged();
                DeleteScreenshotCommand.NotifyCanExecuteChanged();
                SaveTradingSetupCommand.NotifyCanExecuteChanged();
                ClearTradingSetupCommand.NotifyCanExecuteChanged();
                AssignMistakeCommand.NotifyCanExecuteChanged();
                RemoveMistakeCommand.NotifyCanExecuteChanged();
                NotifyTradeLifecycleCommands();
            }
        }
    }

    public bool IsTradingMistakeOptionsLoading
    {
        get => _isTradingMistakeOptionsLoading;
        private set
        {
            if (SetProperty(ref _isTradingMistakeOptionsLoading, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                AssignMistakeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsTradeMistakesLoading
    {
        get => _isTradeMistakesLoading;
        private set
        {
            if (SetProperty(ref _isTradeMistakesLoading, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                CloseTradeDetailCommand.NotifyCanExecuteChanged();
                AssignMistakeCommand.NotifyCanExecuteChanged();
                RemoveMistakeCommand.NotifyCanExecuteChanged();
                NotifyTradeLifecycleCommands();
            }
        }
    }

    public bool IsAssigningMistake
    {
        get => _isAssigningMistake;
        private set
        {
            if (SetProperty(ref _isAssigningMistake, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                ShowTradeDetailCommand.NotifyCanExecuteChanged();
                CloseTradeDetailCommand.NotifyCanExecuteChanged();
                AssignMistakeCommand.NotifyCanExecuteChanged();
                RemoveMistakeCommand.NotifyCanExecuteChanged();
                NotifyTradeLifecycleCommands();
            }
        }
    }

    public bool IsRemovingMistake
    {
        get => _isRemovingMistake;
        private set
        {
            if (SetProperty(ref _isRemovingMistake, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                ShowTradeDetailCommand.NotifyCanExecuteChanged();
                CloseTradeDetailCommand.NotifyCanExecuteChanged();
                AssignMistakeCommand.NotifyCanExecuteChanged();
                RemoveMistakeCommand.NotifyCanExecuteChanged();
                NotifyTradeLifecycleCommands();
            }
        }
    }

    public bool IsTradeDetailVisible
    {
        get => _isTradeDetailVisible;
        private set
        {
            if (SetProperty(ref _isTradeDetailVisible, value))
            {
                CloseTradeDetailCommand.NotifyCanExecuteChanged();
                ShowAddScreenshotCommand.NotifyCanExecuteChanged();
                SaveScreenshotCommand.NotifyCanExecuteChanged();
                OpenScreenshotPreviewCommand.NotifyCanExecuteChanged();
                DeleteScreenshotCommand.NotifyCanExecuteChanged();
                AssignMistakeCommand.NotifyCanExecuteChanged();
                RemoveMistakeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsTradeDetailLoading
    {
        get => _isTradeDetailLoading;
        private set
        {
            if (SetProperty(ref _isTradeDetailLoading, value))
            {
                ShowTradeDetailCommand.NotifyCanExecuteChanged();
                CloseTradeDetailCommand.NotifyCanExecuteChanged();
                RefreshCommand.NotifyCanExecuteChanged();
                SaveManualTradeCommand.NotifyCanExecuteChanged();
                ShowAddScreenshotCommand.NotifyCanExecuteChanged();
                SaveScreenshotCommand.NotifyCanExecuteChanged();
                OpenScreenshotPreviewCommand.NotifyCanExecuteChanged();
                DeleteScreenshotCommand.NotifyCanExecuteChanged();
                ShowCloseTradeCommand.NotifyCanExecuteChanged();
                SaveCloseTradeCommand.NotifyCanExecuteChanged();
                SaveTradingSetupCommand.NotifyCanExecuteChanged();
                ClearTradingSetupCommand.NotifyCanExecuteChanged();
                AssignMistakeCommand.NotifyCanExecuteChanged();
                RemoveMistakeCommand.NotifyCanExecuteChanged();
                NotifyTradeLifecycleCommands();
            }
        }
    }

    public bool IsTradeScreenshotsLoading
    {
        get => _isTradeScreenshotsLoading;
        private set
        {
            if (SetProperty(ref _isTradeScreenshotsLoading, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                ShowTradeDetailCommand.NotifyCanExecuteChanged();
                CloseTradeDetailCommand.NotifyCanExecuteChanged();
                ShowAddScreenshotCommand.NotifyCanExecuteChanged();
                SaveScreenshotCommand.NotifyCanExecuteChanged();
                OpenScreenshotPreviewCommand.NotifyCanExecuteChanged();
                DeleteScreenshotCommand.NotifyCanExecuteChanged();
                SaveTradingSetupCommand.NotifyCanExecuteChanged();
                ClearTradingSetupCommand.NotifyCanExecuteChanged();
                NotifyTradeLifecycleCommands();
                ShowCloseTradeCommand.NotifyCanExecuteChanged();
                SaveCloseTradeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsAddScreenshotVisible
    {
        get => _isAddScreenshotVisible;
        private set
        {
            if (SetProperty(ref _isAddScreenshotVisible, value))
            {
                ShowAddScreenshotCommand.NotifyCanExecuteChanged();
                CancelAddScreenshotCommand.NotifyCanExecuteChanged();
                SaveScreenshotCommand.NotifyCanExecuteChanged();
                OpenScreenshotPreviewCommand.NotifyCanExecuteChanged();
                DeleteScreenshotCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsAddingScreenshot
    {
        get => _isAddingScreenshot;
        private set
        {
            if (SetProperty(ref _isAddingScreenshot, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                SaveManualTradeCommand.NotifyCanExecuteChanged();
                ShowTradeDetailCommand.NotifyCanExecuteChanged();
                CloseTradeDetailCommand.NotifyCanExecuteChanged();
                ShowAddScreenshotCommand.NotifyCanExecuteChanged();
                CancelAddScreenshotCommand.NotifyCanExecuteChanged();
                SaveScreenshotCommand.NotifyCanExecuteChanged();
                OpenScreenshotPreviewCommand.NotifyCanExecuteChanged();
                ShowCloseTradeCommand.NotifyCanExecuteChanged();
                SaveCloseTradeCommand.NotifyCanExecuteChanged();
                SaveTradingSetupCommand.NotifyCanExecuteChanged();
                ClearTradingSetupCommand.NotifyCanExecuteChanged();
                NotifyTradeLifecycleCommands();
            }
        }
    }

    public bool IsTradeDetailNotFound
    {
        get => _isTradeDetailNotFound;
        private set => SetProperty(ref _isTradeDetailNotFound, value);
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
                ShowTradeDetailCommand.NotifyCanExecuteChanged();
                NotifyTradeLifecycleCommands();
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

    public string? TradeDetailErrorMessage
    {
        get => _tradeDetailErrorMessage;
        private set
        {
            if (SetProperty(ref _tradeDetailErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasTradeDetailError));
            }
        }
    }

    public bool HasTradeDetailError => TradeDetailErrorMessage is not null;

    public string? TradingSetupSaveErrorMessage
    {
        get => _tradingSetupSaveErrorMessage;
        private set
        {
            if (SetProperty(ref _tradingSetupSaveErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasTradingSetupSaveError));
            }
        }
    }

    public bool HasTradingSetupSaveError =>
        TradingSetupSaveErrorMessage is not null;

    public string? TradingSetupSuccessMessage
    {
        get => _tradingSetupSuccessMessage;
        private set
        {
            if (SetProperty(ref _tradingSetupSuccessMessage, value))
            {
                OnPropertyChanged(nameof(HasTradingSetupSuccess));
            }
        }
    }

    public bool HasTradingSetupSuccess => TradingSetupSuccessMessage is not null;

    public string? TradeMistakesErrorMessage
    {
        get => _tradeMistakesErrorMessage;
        private set
        {
            if (SetProperty(ref _tradeMistakesErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasTradeMistakesError));
            }
        }
    }

    public bool HasTradeMistakesError => TradeMistakesErrorMessage is not null;

    public string? TradingMistakeOptionsErrorMessage
    {
        get => _tradingMistakeOptionsErrorMessage;
        private set
        {
            if (SetProperty(ref _tradingMistakeOptionsErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasTradingMistakeOptionsError));
            }
        }
    }

    public bool HasTradingMistakeOptionsError =>
        TradingMistakeOptionsErrorMessage is not null;

    public string? AssignMistakeErrorMessage
    {
        get => _assignMistakeErrorMessage;
        private set
        {
            if (SetProperty(ref _assignMistakeErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasAssignMistakeError));
            }
        }
    }

    public bool HasAssignMistakeError => AssignMistakeErrorMessage is not null;

    public string? AssignMistakeSuccessMessage
    {
        get => _assignMistakeSuccessMessage;
        private set
        {
            if (SetProperty(ref _assignMistakeSuccessMessage, value))
            {
                OnPropertyChanged(nameof(HasAssignMistakeSuccess));
            }
        }
    }

    public bool HasAssignMistakeSuccess =>
        AssignMistakeSuccessMessage is not null;

    public string? RemoveMistakeErrorMessage
    {
        get => _removeMistakeErrorMessage;
        private set
        {
            if (SetProperty(ref _removeMistakeErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasRemoveMistakeError));
            }
        }
    }

    public bool HasRemoveMistakeError => RemoveMistakeErrorMessage is not null;

    public string? RemoveMistakeSuccessMessage
    {
        get => _removeMistakeSuccessMessage;
        private set
        {
            if (SetProperty(ref _removeMistakeSuccessMessage, value))
            {
                OnPropertyChanged(nameof(HasRemoveMistakeSuccess));
            }
        }
    }

    public bool HasRemoveMistakeSuccess =>
        RemoveMistakeSuccessMessage is not null;

    public string? TradeScreenshotsErrorMessage
    {
        get => _tradeScreenshotsErrorMessage;
        private set
        {
            if (SetProperty(ref _tradeScreenshotsErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasTradeScreenshotsError));
            }
        }
    }

    public bool HasTradeScreenshotsError =>
        TradeScreenshotsErrorMessage is not null;

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

    public string? ScreenshotValidationErrorMessage
    {
        get => _screenshotValidationErrorMessage;
        private set
        {
            if (SetProperty(ref _screenshotValidationErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasScreenshotValidationError));
            }
        }
    }

    public bool HasScreenshotValidationError =>
        ScreenshotValidationErrorMessage is not null;

    public string? ScreenshotSaveErrorMessage
    {
        get => _screenshotSaveErrorMessage;
        private set
        {
            if (SetProperty(ref _screenshotSaveErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasScreenshotSaveError));
            }
        }
    }

    public bool HasScreenshotSaveError => ScreenshotSaveErrorMessage is not null;

    public string? ScreenshotSuccessMessage
    {
        get => _screenshotSuccessMessage;
        private set
        {
            if (SetProperty(ref _screenshotSuccessMessage, value))
            {
                OnPropertyChanged(nameof(HasScreenshotSuccessMessage));
            }
        }
    }

    public bool HasScreenshotSuccessMessage =>
        ScreenshotSuccessMessage is not null;

    public Guid? PreviewScreenshotId
    {
        get => _previewScreenshotId;
        private set
        {
            if (SetProperty(ref _previewScreenshotId, value))
            {
                CloseScreenshotPreviewCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string? PreviewScreenshotFileName
    {
        get => _previewScreenshotFileName;
        private set => SetProperty(ref _previewScreenshotFileName, value);
    }

    public ImageSource? PreviewScreenshotImage
    {
        get => _previewScreenshotImage;
        private set => SetProperty(ref _previewScreenshotImage, value);
    }

    public bool IsScreenshotPreviewVisible
    {
        get => _isScreenshotPreviewVisible;
        private set
        {
            if (SetProperty(ref _isScreenshotPreviewVisible, value))
            {
                CloseScreenshotPreviewCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsScreenshotPreviewLoading
    {
        get => _isScreenshotPreviewLoading;
        private set
        {
            if (SetProperty(ref _isScreenshotPreviewLoading, value))
            {
                OpenScreenshotPreviewCommand.NotifyCanExecuteChanged();
                CloseScreenshotPreviewCommand.NotifyCanExecuteChanged();
                DeleteScreenshotCommand.NotifyCanExecuteChanged();
                ShowCloseTradeCommand.NotifyCanExecuteChanged();
                SaveCloseTradeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsCloseTradeVisible
    {
        get => _isCloseTradeVisible;
        private set
        {
            if (SetProperty(ref _isCloseTradeVisible, value))
            {
                ShowCloseTradeCommand.NotifyCanExecuteChanged();
                CancelCloseTradeCommand.NotifyCanExecuteChanged();
                SaveCloseTradeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsClosingTrade
    {
        get => _isClosingTrade;
        private set
        {
            if (SetProperty(ref _isClosingTrade, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                SaveManualTradeCommand.NotifyCanExecuteChanged();
                ShowTradeDetailCommand.NotifyCanExecuteChanged();
                CloseTradeDetailCommand.NotifyCanExecuteChanged();
                ShowCloseTradeCommand.NotifyCanExecuteChanged();
                CancelCloseTradeCommand.NotifyCanExecuteChanged();
                SaveCloseTradeCommand.NotifyCanExecuteChanged();
                ShowAddScreenshotCommand.NotifyCanExecuteChanged();
                CancelAddScreenshotCommand.NotifyCanExecuteChanged();
                SaveScreenshotCommand.NotifyCanExecuteChanged();
                OpenScreenshotPreviewCommand.NotifyCanExecuteChanged();
                DeleteScreenshotCommand.NotifyCanExecuteChanged();
                SaveTradingSetupCommand.NotifyCanExecuteChanged();
                ClearTradingSetupCommand.NotifyCanExecuteChanged();
                NotifyTradeLifecycleCommands();
            }
        }
    }

    public string? CloseTradeValidationErrorMessage
    {
        get => _closeTradeValidationErrorMessage;
        private set
        {
            if (SetProperty(ref _closeTradeValidationErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasCloseTradeValidationError));
            }
        }
    }

    public bool HasCloseTradeValidationError =>
        CloseTradeValidationErrorMessage is not null;

    public string? CloseTradeSaveErrorMessage
    {
        get => _closeTradeSaveErrorMessage;
        private set
        {
            if (SetProperty(ref _closeTradeSaveErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasCloseTradeSaveError));
            }
        }
    }

    public bool HasCloseTradeSaveError => CloseTradeSaveErrorMessage is not null;

    public string? CloseTradeSuccessMessage
    {
        get => _closeTradeSuccessMessage;
        private set
        {
            if (SetProperty(ref _closeTradeSuccessMessage, value))
            {
                OnPropertyChanged(nameof(HasCloseTradeSuccess));
            }
        }
    }

    public bool HasCloseTradeSuccess => CloseTradeSuccessMessage is not null;

    public bool IsDeletingScreenshot
    {
        get => _isDeletingScreenshot;
        private set
        {
            if (SetProperty(ref _isDeletingScreenshot, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                ShowTradeDetailCommand.NotifyCanExecuteChanged();
                CloseTradeDetailCommand.NotifyCanExecuteChanged();
                ShowAddScreenshotCommand.NotifyCanExecuteChanged();
                CancelAddScreenshotCommand.NotifyCanExecuteChanged();
                SaveScreenshotCommand.NotifyCanExecuteChanged();
                OpenScreenshotPreviewCommand.NotifyCanExecuteChanged();
                DeleteScreenshotCommand.NotifyCanExecuteChanged();
                ShowCloseTradeCommand.NotifyCanExecuteChanged();
                SaveCloseTradeCommand.NotifyCanExecuteChanged();
                SaveTradingSetupCommand.NotifyCanExecuteChanged();
                ClearTradingSetupCommand.NotifyCanExecuteChanged();
                NotifyTradeLifecycleCommands();
            }
        }
    }

    public string? ScreenshotPreviewErrorMessage
    {
        get => _screenshotPreviewErrorMessage;
        private set
        {
            if (SetProperty(ref _screenshotPreviewErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasScreenshotPreviewError));
                CloseScreenshotPreviewCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasScreenshotPreviewError =>
        ScreenshotPreviewErrorMessage is not null;

    public string? ScreenshotDeleteErrorMessage
    {
        get => _screenshotDeleteErrorMessage;
        private set
        {
            if (SetProperty(ref _screenshotDeleteErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasScreenshotDeleteError));
            }
        }
    }

    public bool HasScreenshotDeleteError =>
        ScreenshotDeleteErrorMessage is not null;

    public string? ScreenshotDeleteWarningMessage
    {
        get => _screenshotDeleteWarningMessage;
        private set
        {
            if (SetProperty(ref _screenshotDeleteWarningMessage, value))
            {
                OnPropertyChanged(nameof(HasScreenshotDeleteWarning));
            }
        }
    }

    public bool HasScreenshotDeleteWarning =>
        ScreenshotDeleteWarningMessage is not null;

    public string? ScreenshotDeleteSuccessMessage
    {
        get => _screenshotDeleteSuccessMessage;
        private set
        {
            if (SetProperty(ref _screenshotDeleteSuccessMessage, value))
            {
                OnPropertyChanged(nameof(HasScreenshotDeleteSuccess));
            }
        }
    }

    public bool HasScreenshotDeleteSuccess =>
        ScreenshotDeleteSuccessMessage is not null;

    public bool IsManualEntryVisible
    {
        get => _isManualEntryVisible;
        private set
        {
            if (SetProperty(ref _isManualEntryVisible, value))
            {
                OnPropertyChanged(nameof(IsTradeFormVisible));
                OnPropertyChanged(nameof(TradeFormTitle));
                OnPropertyChanged(nameof(TradeFormDescription));
                ShowManualEntryCommand.NotifyCanExecuteChanged();
                CancelManualEntryCommand.NotifyCanExecuteChanged();
                SaveManualTradeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsTradeEditVisible
    {
        get => _isTradeEditVisible;
        private set
        {
            if (SetProperty(ref _isTradeEditVisible, value))
            {
                OnPropertyChanged(nameof(IsTradeFormVisible));
                OnPropertyChanged(nameof(TradeFormTitle));
                OnPropertyChanged(nameof(TradeFormDescription));
                ShowManualEntryCommand.NotifyCanExecuteChanged();
                ShowTradeEditCommand.NotifyCanExecuteChanged();
                ShowSelectedTradeEditCommand.NotifyCanExecuteChanged();
                CancelTradeEditCommand.NotifyCanExecuteChanged();
                SaveTradeEditCommand.NotifyCanExecuteChanged();
                DeleteTradeCommand.NotifyCanExecuteChanged();
                DeleteSelectedTradeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsTradeFormVisible => IsManualEntryVisible || IsTradeEditVisible;

    public string TradeFormTitle =>
        IsTradeEditVisible ? "Edit Trade" : "Manual Trade Entry";

    public string TradeFormDescription => IsTradeEditVisible
        ? "Correct the authoritative trade facts. Derived values will be recalculated."
        : "Enter the trade facts explicitly. Execution times are UTC.";

    public bool IsUpdatingTrade
    {
        get => _isUpdatingTrade;
        private set
        {
            if (SetProperty(ref _isUpdatingTrade, value))
            {
                CancelTradeEditCommand.NotifyCanExecuteChanged();
                SaveTradeEditCommand.NotifyCanExecuteChanged();
                ShowTradeEditCommand.NotifyCanExecuteChanged();
                ShowSelectedTradeEditCommand.NotifyCanExecuteChanged();
                DeleteTradeCommand.NotifyCanExecuteChanged();
                DeleteSelectedTradeCommand.NotifyCanExecuteChanged();
                RefreshCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsDeletingTrade
    {
        get => _isDeletingTrade;
        private set
        {
            if (SetProperty(ref _isDeletingTrade, value))
            {
                DeleteTradeCommand.NotifyCanExecuteChanged();
                DeleteSelectedTradeCommand.NotifyCanExecuteChanged();
                ShowTradeEditCommand.NotifyCanExecuteChanged();
                ShowSelectedTradeEditCommand.NotifyCanExecuteChanged();
                RefreshCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string? TradeUpdateErrorMessage
    {
        get => _tradeUpdateErrorMessage;
        private set
        {
            if (SetProperty(ref _tradeUpdateErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasTradeUpdateError));
            }
        }
    }

    public bool HasTradeUpdateError => TradeUpdateErrorMessage is not null;

    public string? TradeUpdateSuccessMessage
    {
        get => _tradeUpdateSuccessMessage;
        private set
        {
            if (SetProperty(ref _tradeUpdateSuccessMessage, value))
            {
                OnPropertyChanged(nameof(HasTradeUpdateSuccess));
            }
        }
    }

    public bool HasTradeUpdateSuccess => TradeUpdateSuccessMessage is not null;

    public string? TradeDeleteErrorMessage
    {
        get => _tradeDeleteErrorMessage;
        private set
        {
            if (SetProperty(ref _tradeDeleteErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasTradeDeleteError));
            }
        }
    }

    public bool HasTradeDeleteError => TradeDeleteErrorMessage is not null;

    public string? TradeDeleteWarningMessage
    {
        get => _tradeDeleteWarningMessage;
        private set
        {
            if (SetProperty(ref _tradeDeleteWarningMessage, value))
            {
                OnPropertyChanged(nameof(HasTradeDeleteWarning));
            }
        }
    }

    public bool HasTradeDeleteWarning => TradeDeleteWarningMessage is not null;

    public IAsyncRelayCommand RefreshCommand { get; }

    public IRelayCommand ShowManualEntryCommand { get; }

    public IRelayCommand CancelManualEntryCommand { get; }

    public IAsyncRelayCommand SaveManualTradeCommand { get; }

    public IAsyncRelayCommand SaveTradingSetupCommand { get; }

    public IAsyncRelayCommand ClearTradingSetupCommand { get; }

    public IAsyncRelayCommand AssignMistakeCommand { get; }

    public IAsyncRelayCommand<TradeMistakeListItem> RemoveMistakeCommand { get; }

    public IAsyncRelayCommand<TradeListItem> ShowTradeDetailCommand { get; }

    public IRelayCommand CloseTradeDetailCommand { get; }

    public IRelayCommand ShowCloseTradeCommand { get; }

    public IRelayCommand CancelCloseTradeCommand { get; }

    public IAsyncRelayCommand SaveCloseTradeCommand { get; }

    public IRelayCommand ShowAddScreenshotCommand { get; }

    public IRelayCommand CancelAddScreenshotCommand { get; }

    public IAsyncRelayCommand SaveScreenshotCommand { get; }

    public IAsyncRelayCommand<TradeScreenshotListItem>
        OpenScreenshotPreviewCommand { get; }

    public IRelayCommand CloseScreenshotPreviewCommand { get; }

    public IAsyncRelayCommand<TradeScreenshotListItem> DeleteScreenshotCommand { get; }

    public IAsyncRelayCommand<TradeListItem> ShowTradeEditCommand { get; }

    public IAsyncRelayCommand ShowSelectedTradeEditCommand { get; }

    public IRelayCommand CancelTradeEditCommand { get; }

    public IAsyncRelayCommand SaveTradeEditCommand { get; }

    public IAsyncRelayCommand<TradeListItem> DeleteTradeCommand { get; }

    public IAsyncRelayCommand DeleteSelectedTradeCommand { get; }

    private void NotifyTradeLifecycleCommands()
    {
        ShowTradeEditCommand.NotifyCanExecuteChanged();
        ShowSelectedTradeEditCommand.NotifyCanExecuteChanged();
        CancelTradeEditCommand.NotifyCanExecuteChanged();
        SaveTradeEditCommand.NotifyCanExecuteChanged();
        DeleteTradeCommand.NotifyCanExecuteChanged();
        DeleteSelectedTradeCommand.NotifyCanExecuteChanged();
    }

    public bool TryBuildCloseManualTradeCommand(
        out CloseManualTradeCommand? command)
    {
        CloseTradeValidationErrorMessage = null;
        command = null;

        if (SelectedTradeDetail is not { } detail)
        {
            return FailCloseTradeValidation("A selected trade is required.");
        }

        if (detail.Status != TradeStatus.Open)
        {
            return FailCloseTradeValidation(AlreadyClosedTradeErrorMessage);
        }

        if (!TryParseUtcTimestamp(
                CloseTradeExecutedAtUtcText,
                out DateTimeOffset executedAtUtc))
        {
            return FailCloseTradeValidation(
                "Exit time must be a valid UTC timestamp.");
        }

        if (!TryParseDecimal(CloseTradePriceText, out decimal price))
        {
            return FailCloseTradeValidation("Exit price must be a valid number.");
        }

        if (!TryParseNonNegativeCost(
                CloseTradeCommissionText,
                out decimal commission))
        {
            return FailCloseTradeValidation(
                "Commission must be a non-negative number.");
        }

        if (!TryParseNonNegativeCost(CloseTradeFeesText, out decimal fees))
        {
            return FailCloseTradeValidation(
                "Fees must be a non-negative number.");
        }

        command = new CloseManualTradeCommand(
            detail.Id,
            executedAtUtc,
            price,
            commission,
            fees);
        return true;
    }

    public bool TryBuildManualTradeCommand(
        out CreateManualTradeCommand? command)
    {
        ValidationErrorMessage = null;
        command = null;

        if (SelectedAccount is not { } selectedAccount)
        {
            return FailValidation(ManualTradeInputField.Account, "Trading account is required.");
        }

        if (SelectedInstrument is not { } selectedInstrument)
        {
            return FailValidation(ManualTradeInputField.Instrument, "Instrument is required.");
        }

        if (SelectedDirection is not { } direction)
        {
            return FailValidation(ManualTradeInputField.Direction, "Direction is required.");
        }

        if (!Enum.IsDefined(direction))
        {
            return FailValidation(ManualTradeInputField.Direction, "Direction is invalid.");
        }

        if (!TryParseDecimal(QuantityText, out decimal quantity))
        {
            return FailValidation(ManualTradeInputField.Quantity, "Quantity must be a valid number.");
        }

        if (quantity <= 0)
        {
            return FailValidation(ManualTradeInputField.Quantity, "Quantity must be greater than zero.");
        }

        try
        {
            TradeQuantityPolicy.Validate(
                selectedInstrument.AssetClass,
                quantity,
                nameof(QuantityText));
        }
        catch (ArgumentException) when (IsSelectedInstrumentFutures)
        {
            return FailValidation(
                ManualTradeInputField.Quantity,
                TradeQuantityPolicy.FuturesWholeContractsMessage);
        }

        if (!TryParseUtcTimestamp(
                EntryExecutedAtUtcText,
                out DateTimeOffset entryExecutedAtUtc))
        {
            return FailValidation(ManualTradeInputField.EntryExecutedAtUtc, "Entry time must be a valid UTC timestamp.");
        }

        if (!TryParseDecimal(EntryPriceText, out decimal entryPrice))
        {
            return FailValidation(ManualTradeInputField.EntryPrice, "Entry price must be a valid number.");
        }

        if (!TryParseNonNegativeCost(
                EntryCommissionText,
                out decimal entryCommission))
        {
            return FailValidation(
                ManualTradeInputField.EntryCommission,
                "Entry commission must be a valid non-negative number.");
        }

        if (!TryParseNonNegativeCost(EntryFeesText, out decimal entryFees))
        {
            return FailValidation(
                ManualTradeInputField.EntryFees,
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
                return FailValidation(ManualTradeInputField.ExitExecutedAtUtc, "Exit time must be a valid UTC timestamp.");
            }

            if (exitExecutedAtUtc < entryExecutedAtUtc)
            {
                return FailValidation(
                    ManualTradeInputField.ExitExecutedAtUtc,
                    "Exit time cannot be earlier than entry time.");
            }

            if (!TryParseDecimal(ExitPriceText, out decimal exitPrice))
            {
                return FailValidation(ManualTradeInputField.ExitPrice, "Exit price must be a valid number.");
            }

            if (!TryParseNonNegativeCost(
                    ExitCommissionText,
                    out decimal exitCommission))
            {
                return FailValidation(
                    ManualTradeInputField.ExitCommission,
                    "Exit commission must be a valid non-negative number.");
            }

            if (!TryParseNonNegativeCost(ExitFeesText, out decimal exitFees))
            {
                return FailValidation(
                    ManualTradeInputField.ExitFees,
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
            SelectedTradingSetup?.Id,
            direction,
            quantity,
            entry,
            exit);
        return true;
    }

    public async Task EnsureLoadedAsync()
    {
        _ = await LoadReferenceDataAsync(forceRefresh: false, CancellationToken.None);
        _ = await LoadTradingMistakeOptionsAsync(
            forceRefresh: false,
            CancellationToken.None);
        _ = await LoadTradeListAsync(forceRefresh: false, CancellationToken.None);
    }

    public void ResetTransientState()
    {
        ShowTradeDetailCommand.Cancel();
        ShowTradeEditCommand.Cancel();
        ShowSelectedTradeEditCommand.Cancel();
        OpenScreenshotPreviewCommand.Cancel();

        ResetTradeEditState();
        IsManualEntryVisible = false;
        SuccessMessage = null;

        CloseTradeDetail();
        TradingSetupSaveErrorMessage = null;
        TradingSetupSuccessMessage = null;
        TradeDeleteErrorMessage = null;
        TradeDeleteWarningMessage = null;
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        _ = await LoadReferenceDataAsync(forceRefresh: true, cancellationToken);
        _ = await LoadTradingMistakeOptionsAsync(
            forceRefresh: true,
            cancellationToken);
        _ = await LoadTradeListAsync(forceRefresh: true, cancellationToken);

        if (IsTradeDetailVisible && SelectedTradeDetail is { } detail)
        {
            _ = await ReloadTradeDetailAfterTradingSetupAsync(
                detail.Id,
                cancellationToken);
            _ = await LoadTradeMistakesAsync(
                detail.Id,
                forceRefresh: true,
                cancellationToken);
            _ = await LoadTradeScreenshotsAsync(
                detail.Id,
                forceRefresh: true,
                cancellationToken);
        }
    }

    private bool CanRefresh() =>
        !IsLoading &&
        !IsTradeListLoading &&
        !IsTradeDetailLoading &&
        !IsTradeScreenshotsLoading &&
        !IsTradeMistakesLoading &&
        !IsTradingMistakeOptionsLoading &&
        !IsSaving &&
        !IsTradingSetupSaving &&
        !IsAssigningMistake &&
        !IsRemovingMistake &&
        !IsAddingScreenshot &&
        !IsDeletingScreenshot &&
        !IsClosingTrade;

    private void ShowManualEntry()
    {
        SuccessMessage = null;
        SaveErrorMessage = null;
        ValidationErrorMessage = null;
        IsManualEntryVisible = true;
    }

    private bool CanShowManualEntry() =>
        !IsManualEntryVisible &&
        !IsTradeEditVisible &&
        !IsLoading &&
        !IsSaving &&
        !IsTradingSetupSaving;

    private void CancelManualEntry()
    {
        ResetManualEntryForm();
        IsManualEntryVisible = false;
    }

    private bool CanCancelManualEntry() =>
        IsManualEntryVisible && !IsSaving && !IsTradingSetupSaving;

    private bool CanSaveManualTrade() =>
        IsManualEntryVisible &&
        !IsLoading &&
        !IsTradeListLoading &&
        !IsTradeDetailLoading &&
        !IsSaving &&
        !IsTradingSetupSaving &&
        !IsAssigningMistake &&
        !IsRemovingMistake &&
        !IsAddingScreenshot &&
        !IsClosingTrade;

    private bool CanShowTradeEdit(TradeListItem? item) =>
        item is not null &&
        _updateTradeUseCase is not null &&
        !IsTradeEditVisible &&
        !IsManualEntryVisible &&
        !IsTradeDetailLoading &&
        !IsTradeListLoading &&
        !IsTradeScreenshotsLoading &&
        !IsTradeMistakesLoading &&
        !IsUpdatingTrade &&
        !IsDeletingTrade &&
        !IsSaving &&
        !IsTradingSetupSaving &&
        !IsAssigningMistake &&
        !IsRemovingMistake &&
        !IsAddingScreenshot &&
        !IsDeletingScreenshot &&
        !IsClosingTrade;

    private async Task ShowTradeEditAsync(
        TradeListItem? item,
        CancellationToken cancellationToken)
    {
        if (item is null)
        {
            return;
        }

        await LoadTradeEditAsync(item.Id, cancellationToken);
    }

    private bool CanShowSelectedTradeEdit() =>
        SelectedTradeDetail is not null &&
        _updateTradeUseCase is not null &&
        !IsTradeEditVisible &&
        !IsManualEntryVisible &&
        !IsTradeDetailLoading &&
        !IsTradeScreenshotsLoading &&
        !IsTradeMistakesLoading &&
        !IsUpdatingTrade &&
        !IsDeletingTrade &&
        !IsSaving &&
        !IsTradingSetupSaving &&
        !IsAssigningMistake &&
        !IsRemovingMistake &&
        !IsAddingScreenshot &&
        !IsDeletingScreenshot &&
        !IsClosingTrade;

    private Task ShowSelectedTradeEditAsync(CancellationToken cancellationToken) =>
        SelectedTradeDetail is { } detail
            ? LoadTradeEditAsync(detail.Id, cancellationToken)
            : Task.CompletedTask;

    private async Task LoadTradeEditAsync(
        Guid tradeId,
        CancellationToken cancellationToken)
    {
        TradeUpdateErrorMessage = null;
        TradeUpdateSuccessMessage = null;
        IsTradeDetailLoading = true;

        try
        {
            TradeDetail? detail = await _tradeDetailReader.GetByIdAsync(
                tradeId,
                cancellationToken);
            if (detail is null)
            {
                TradeUpdateErrorMessage = "Trade details are no longer available.";
                return;
            }

            if (detail.Executions.Count is < 1 or > 2 ||
                detail.Executions.Any(execution =>
                    execution.Quantity != detail.Executions[0].Quantity))
            {
                TradeUpdateErrorMessage =
                    "This trade cannot be edited with the current manual trade form.";
                return;
            }

            if (!await LoadEditReferenceDataAsync(cancellationToken))
            {
                TradeUpdateErrorMessage = LoadErrorMessage;
                return;
            }

            bool selectedTradeChanged = SelectedTradeDetail?.Id != detail.Id;
            if (selectedTradeChanged)
            {
                OpenScreenshotPreviewCommand.Cancel();
                ClearCloseTradeState();
                ClearTradeScreenshotState();
                ClearTradeMistakeState();
            }

            SelectedTradeDetail = detail;
            IsTradeDetailVisible = true;
            ConfigureHistoricalEditOptions(detail);
            PopulateTradeEditForm(detail);
            IsManualEntryVisible = false;
            IsTradeEditVisible = true;

            _ = await LoadTradeMistakesAsync(
                detail.Id,
                forceRefresh: false,
                cancellationToken,
                clearOnFailure: true);
            _ = await LoadTradeScreenshotsAsync(
                detail.Id,
                forceRefresh: false,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            TradeUpdateErrorMessage = TradeDetailLoadErrorMessage;
        }
        finally
        {
            IsTradeDetailLoading = false;
        }
    }

    private void ConfigureHistoricalEditOptions(TradeDetail detail)
    {
        AccountOptions = _allAccountOptions
            .Where(option => option.IsActive || option.Id == detail.TradingAccountId)
            .ToList();
        InstrumentOptions = _allInstrumentOptions
            .Where(option => option.IsActive || option.Id == detail.InstrumentId)
            .ToList();
        AvailableTradingSetups = _allTradingSetups
            .Where(option => option.IsActive || option.Id == detail.TradingSetupId)
            .ToList();
    }

    private void PopulateTradeEditForm(TradeDetail detail)
    {
        TradeExecutionDetailItem entry = detail.Executions[0];
        TradeExecutionDetailItem? exit = detail.Executions.Count == 2
            ? detail.Executions[1]
            : null;

        _editingTradeId = detail.Id;
        _editingEntryExecutionId = entry.Id;
        _editingExitExecutionId = exit?.Id ?? Guid.Empty;
        SelectedAccount = AccountOptions.Single(option =>
            option.Id == detail.TradingAccountId);
        SelectedInstrument = InstrumentOptions.Single(option =>
            option.Id == detail.InstrumentId);
        SelectedTradingSetup = detail.TradingSetupId is Guid setupId
            ? AvailableTradingSetups.Single(option => option.Id == setupId)
            : null;
        SelectedDirection = detail.Direction;
        QuantityText = FormatDecimal(entry.Quantity);
        EntryExecutedAtUtcText = FormatTimestamp(entry.ExecutedAtUtc);
        EntryPriceText = FormatDecimal(entry.Price);
        EntryCommissionText = FormatDecimal(entry.Commission);
        EntryFeesText = FormatDecimal(entry.Fees);
        HasExit = exit is not null;
        if (exit is not null)
        {
            ExitExecutedAtUtcText = FormatTimestamp(exit.ExecutedAtUtc);
            ExitPriceText = FormatDecimal(exit.Price);
            ExitCommissionText = FormatDecimal(exit.Commission);
            ExitFeesText = FormatDecimal(exit.Fees);
        }

        SetInvalidManualTradeInput(null);
        ValidationErrorMessage = null;
        TradeUpdateErrorMessage = null;
    }

    private void CancelTradeEdit()
    {
        ResetTradeEditState();
    }

    private bool CanCancelTradeEdit() =>
        IsTradeEditVisible && !IsUpdatingTrade && !IsDeletingTrade;

    private bool CanSaveTradeEdit() =>
        IsTradeEditVisible &&
        _updateTradeUseCase is not null &&
        _editingTradeId.HasValue &&
        !IsUpdatingTrade &&
        !IsDeletingTrade &&
        !IsLoading &&
        !IsTradeDetailLoading;

    private async Task SaveTradeEditAsync(CancellationToken cancellationToken)
    {
        TradeUpdateErrorMessage = null;
        TradeUpdateSuccessMessage = null;

        if (_updateTradeUseCase is null ||
            _editingTradeId is not Guid tradeId ||
            !TryBuildManualTradeCommand(out CreateManualTradeCommand? draft) ||
            draft is null)
        {
            return;
        }

        var command = new UpdateTradeCommand(
            tradeId,
            draft.TradingAccountId,
            draft.InstrumentId,
            draft.TradingSetupId,
            draft.Direction,
            draft.Quantity,
            new UpdateTradeExecutionInput(
                _editingEntryExecutionId,
                draft.Entry.ExecutedAtUtc,
                draft.Entry.Price,
                draft.Entry.Commission,
                draft.Entry.Fees),
            draft.Exit is null
                ? null
                : new UpdateTradeExecutionInput(
                    _editingExitExecutionId,
                    draft.Exit.ExecutedAtUtc,
                    draft.Exit.Price,
                    draft.Exit.Commission,
                    draft.Exit.Fees));

        IsUpdatingTrade = true;
        try
        {
            _ = await _updateTradeUseCase.ExecuteAsync(command, cancellationToken);
            ResetTradeEditState();
            TradeUpdateSuccessMessage = TradeUpdatedMessage;

            _ = await ReloadTradeDetailAfterTradingSetupAsync(
                tradeId,
                CancellationToken.None);
            _ = await LoadTradeListAsync(
                forceRefresh: true,
                CancellationToken.None);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (KeyNotFoundException exception)
        {
            TradeUpdateErrorMessage = exception.Message;
        }
        catch (ArgumentException exception)
        {
            TradeUpdateErrorMessage = exception.Message;
        }
        catch (InvalidOperationException exception)
        {
            TradeUpdateErrorMessage = exception.Message;
        }
        catch (Exception)
        {
            TradeUpdateErrorMessage = TradeUpdateErrorMessageFallback;
        }
        finally
        {
            IsUpdatingTrade = false;
        }
    }

    private bool CanDeleteTrade(TradeListItem? item) =>
        item is not null &&
        _deleteTradeUseCase is not null &&
        _dialogService is not null &&
        !IsTradeDetailLoading &&
        !IsTradeListLoading &&
        !IsTradeScreenshotsLoading &&
        !IsTradeMistakesLoading &&
        !IsUpdatingTrade &&
        !IsDeletingTrade &&
        !IsSaving &&
        !IsTradingSetupSaving &&
        !IsAssigningMistake &&
        !IsRemovingMistake &&
        !IsAddingScreenshot &&
        !IsDeletingScreenshot &&
        !IsClosingTrade;

    private Task DeleteTradeAsync(
        TradeListItem? item,
        CancellationToken cancellationToken) =>
        item is null
            ? Task.CompletedTask
            : DeleteTradeCoreAsync(
                item.Id,
                $"{item.InstrumentSymbol} · {item.OpenedAtUtc:yyyy-MM-dd HH:mm} UTC",
                cancellationToken);

    private bool CanDeleteSelectedTrade() =>
        SelectedTradeDetail is not null &&
        _deleteTradeUseCase is not null &&
        _dialogService is not null &&
        !IsTradeDetailLoading &&
        !IsTradeScreenshotsLoading &&
        !IsTradeMistakesLoading &&
        !IsUpdatingTrade &&
        !IsDeletingTrade &&
        !IsSaving &&
        !IsTradingSetupSaving &&
        !IsAssigningMistake &&
        !IsRemovingMistake &&
        !IsAddingScreenshot &&
        !IsDeletingScreenshot &&
        !IsClosingTrade;

    private Task DeleteSelectedTradeAsync(CancellationToken cancellationToken) =>
        SelectedTradeDetail is { } detail
            ? DeleteTradeCoreAsync(
                detail.Id,
                $"{detail.InstrumentSymbol} · {detail.OpenedAtUtc:yyyy-MM-dd HH:mm} UTC",
                cancellationToken)
            : Task.CompletedTask;

    private async Task DeleteTradeCoreAsync(
        Guid tradeId,
        string identity,
        CancellationToken cancellationToken)
    {
        if (_deleteTradeUseCase is null || _dialogService is null)
        {
            return;
        }

        var request = new ConfirmationDialogRequest(
            "Delete trade?",
            $"{identity}{Environment.NewLine}{Environment.NewLine}" +
            "This will permanently delete this trade and its related mistake " +
            "assignments and screenshot records. This action cannot be undone.",
            "Delete Trade",
            isDestructive: true);
        if (!_dialogService.Confirm(request))
        {
            return;
        }

        TradeDeleteErrorMessage = null;
        TradeDeleteWarningMessage = null;
        IsDeletingTrade = true;
        try
        {
            DeleteTradeResult result = await _deleteTradeUseCase.ExecuteAsync(
                tradeId,
                cancellationToken);
            if (result == DeleteTradeResult.DeletedWithFileCleanupWarning)
            {
                TradeDeleteWarningMessage = TradeDeleteCleanupWarningMessage;
            }

            if (SelectedTradeDetail?.Id == tradeId)
            {
                ClearDeletedTradeDetail();
            }

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
            TradeDeleteErrorMessage = "Trade details are no longer available.";
            if (SelectedTradeDetail?.Id == tradeId)
            {
                ClearDeletedTradeDetail();
            }

            _ = await LoadTradeListAsync(
                forceRefresh: true,
                CancellationToken.None);
        }
        catch (Exception)
        {
            TradeDeleteErrorMessage = TradeDeleteErrorMessageFallback;
        }
        finally
        {
            IsDeletingTrade = false;
        }
    }

    private void ClearDeletedTradeDetail()
    {
        ResetTradeEditState();
        OpenScreenshotPreviewCommand.Cancel();
        IsTradeDetailVisible = false;
        SelectedTradeDetail = null;
        ClearCloseTradeState();
        ClearTradeScreenshotState();
        ClearTradeMistakeState();
    }

    private bool CanShowTradeDetail(TradeListItem? item) =>
        item is not null &&
        !IsTradeDetailLoading &&
        !IsTradeScreenshotsLoading &&
        !IsTradeMistakesLoading &&
        !IsTradeListLoading &&
        !IsSaving &&
        !IsTradingSetupSaving &&
        !IsAssigningMistake &&
        !IsRemovingMistake &&
        !IsAddingScreenshot &&
        !IsDeletingScreenshot &&
        !IsClosingTrade;

    private async Task ShowTradeDetailAsync(
        TradeListItem? item,
        CancellationToken cancellationToken)
    {
        if (item is null)
        {
            return;
        }

        OpenScreenshotPreviewCommand.Cancel();
        IsTradeDetailVisible = true;
        SelectedTradeDetail = null;
        TradeDetailErrorMessage = null;
        IsTradeDetailNotFound = false;
        TradingSetupSaveErrorMessage = null;
        TradingSetupSuccessMessage = null;
        ClearCloseTradeState();
        ClearTradeScreenshotState();
        ClearTradeMistakeState();
        IsTradeDetailLoading = true;

        try
        {
            TradeDetail? detail = await _tradeDetailReader.GetByIdAsync(
                item.Id,
                cancellationToken);

            if (detail is null)
            {
                IsTradeDetailNotFound = true;
            }
            else
            {
                SelectedTradeDetail = detail;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            TradeDetailErrorMessage = TradeDetailLoadErrorMessage;
        }
        finally
        {
            IsTradeDetailLoading = false;
        }

        if (SelectedTradeDetail is { } selectedDetail)
        {
            _ = await LoadTradeMistakesAsync(
                selectedDetail.Id,
                forceRefresh: false,
                cancellationToken,
                clearOnFailure: true);
            _ = await LoadTradeScreenshotsAsync(
                selectedDetail.Id,
                forceRefresh: false,
                cancellationToken);
        }
    }

    private void CloseTradeDetail()
    {
        OpenScreenshotPreviewCommand.Cancel();
        IsTradeDetailVisible = false;
        SelectedTradeDetail = null;
        TradeDetailErrorMessage = null;
        IsTradeDetailNotFound = false;
        ClearCloseTradeState();
        ClearTradeScreenshotState();
        ClearTradeMistakeState();
    }

    private bool CanCloseTradeDetail() =>
        IsTradeDetailVisible &&
        !IsTradeDetailLoading &&
        !IsTradingSetupSaving &&
        !IsTradeMistakesLoading &&
        !IsAssigningMistake &&
        !IsRemovingMistake &&
        !IsTradeScreenshotsLoading &&
        !IsAddingScreenshot &&
        !IsDeletingScreenshot &&
        !IsClosingTrade;

    private void ShowCloseTrade()
    {
        if (!CanShowCloseTrade())
        {
            return;
        }

        ResetCloseTradeDraft();
        CloseTradeValidationErrorMessage = null;
        CloseTradeSaveErrorMessage = null;
        CloseTradeSuccessMessage = null;
        IsCloseTradeVisible = true;
    }

    private bool CanShowCloseTrade() =>
        !IsCloseTradeVisible &&
        IsTradeDetailVisible &&
        IsSelectedTradeOpen &&
        !IsTradeDetailLoading &&
        !IsTradeScreenshotsLoading &&
        !IsSaving &&
        !IsTradingSetupSaving &&
        !IsAddingScreenshot &&
        !IsDeletingScreenshot &&
        !IsScreenshotPreviewLoading &&
        !IsClosingTrade;

    private void CancelCloseTrade()
    {
        ResetCloseTradeDraft();
        CloseTradeValidationErrorMessage = null;
        CloseTradeSaveErrorMessage = null;
        IsCloseTradeVisible = false;
    }

    private bool CanCancelCloseTrade() =>
        IsCloseTradeVisible && !IsClosingTrade && !IsTradingSetupSaving;

    private bool CanSaveCloseTrade() =>
        IsCloseTradeVisible &&
        IsTradeDetailVisible &&
        IsSelectedTradeOpen &&
        !IsTradeDetailLoading &&
        !IsTradeScreenshotsLoading &&
        !IsSaving &&
        !IsTradingSetupSaving &&
        !IsAddingScreenshot &&
        !IsDeletingScreenshot &&
        !IsScreenshotPreviewLoading &&
        !IsClosingTrade;

    private async Task SaveCloseTradeAsync(CancellationToken cancellationToken)
    {
        CloseTradeValidationErrorMessage = null;
        CloseTradeSaveErrorMessage = null;
        CloseTradeSuccessMessage = null;

        if (!TryBuildCloseManualTradeCommand(
                out CloseManualTradeCommand? command) ||
            command is null)
        {
            return;
        }

        IsClosingTrade = true;

        try
        {
            await _closeManualTradeUseCase.ExecuteAsync(
                command,
                cancellationToken);

            ResetCloseTradeDraft();
            IsCloseTradeVisible = false;
            CloseTradeSuccessMessage = TradeClosedMessage;

            await ReloadTradeDetailAfterCloseAsync(command.TradeId);
            _ = await LoadTradeListAsync(
                forceRefresh: true,
                CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (KeyNotFoundException)
        {
            CloseTradeSaveErrorMessage = StaleCloseTradeErrorMessage;
        }
        catch (InvalidOperationException)
        {
            CloseTradeSaveErrorMessage = AlreadyClosedTradeErrorMessage;
        }
        catch (ArgumentException)
        {
            CloseTradeSaveErrorMessage = CloseTradeChronologyErrorMessage;
        }
        catch (Exception)
        {
            CloseTradeSaveErrorMessage = CloseTradeSaveErrorMessageFallback;
        }
        finally
        {
            IsClosingTrade = false;
        }
    }

    private async Task ReloadTradeDetailAfterCloseAsync(Guid tradeId)
    {
        IsTradeDetailLoading = true;
        TradeDetailErrorMessage = null;
        IsTradeDetailNotFound = false;

        try
        {
            TradeDetail? detail = await _tradeDetailReader.GetByIdAsync(
                tradeId,
                CancellationToken.None);
            if (detail is null)
            {
                SelectedTradeDetail = null;
                TradeDetailErrorMessage = TradeDetailLoadErrorMessage;
                return;
            }

            SelectedTradeDetail = detail;
        }
        catch (Exception)
        {
            SelectedTradeDetail = null;
            TradeDetailErrorMessage = TradeDetailLoadErrorMessage;
        }
        finally
        {
            IsTradeDetailLoading = false;
        }
    }

    private bool CanSaveTradingSetup() =>
        IsTradeDetailVisible &&
        SelectedTradeDetail is { } detail &&
        SelectedTradeDetailTradingSetup is { } setup &&
        setup.Id != detail.TradingSetupId &&
        !IsTradeDetailLoading &&
        !IsTradeScreenshotsLoading &&
        !IsTradingSetupSaving &&
        !IsSaving &&
        !IsCloseTradeVisible &&
        !IsClosingTrade &&
        !IsAddScreenshotVisible &&
        !IsAddingScreenshot &&
        !IsDeletingScreenshot &&
        !IsScreenshotPreviewLoading;

    private bool CanClearTradingSetup() =>
        IsTradeDetailVisible &&
        SelectedTradeDetail?.TradingSetupId is not null &&
        !IsTradeDetailLoading &&
        !IsTradeScreenshotsLoading &&
        !IsTradingSetupSaving &&
        !IsSaving &&
        !IsCloseTradeVisible &&
        !IsClosingTrade &&
        !IsAddScreenshotVisible &&
        !IsAddingScreenshot &&
        !IsDeletingScreenshot &&
        !IsScreenshotPreviewLoading;

    private Task SaveTradingSetupAsync(CancellationToken cancellationToken) =>
        SetTradingSetupAsync(
            SelectedTradeDetailTradingSetup?.Id,
            cancellationToken);

    private Task ClearTradingSetupAsync(CancellationToken cancellationToken) =>
        SetTradingSetupAsync(null, cancellationToken);

    private async Task SetTradingSetupAsync(
        Guid? tradingSetupId,
        CancellationToken cancellationToken)
    {
        if (SelectedTradeDetail is not { } detail)
        {
            return;
        }

        TradingSetupSaveErrorMessage = null;
        TradingSetupSuccessMessage = null;
        IsTradingSetupSaving = true;

        try
        {
            await _setTradeTradingSetupUseCase.ExecuteAsync(
                new SetTradeTradingSetupCommand(detail.Id, tradingSetupId),
                cancellationToken);
            TradingSetupSuccessMessage = TradingSetupSavedMessage;

            _ = await ReloadTradeDetailAfterTradingSetupAsync(
                detail.Id,
                CancellationToken.None);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (KeyNotFoundException)
        {
            TradingSetupSaveErrorMessage = MissingTradingSetupErrorMessage;
        }
        catch (InvalidOperationException exception) when (
            exception.Message == "The selected trading setup is inactive.")
        {
            TradingSetupSaveErrorMessage = InactiveTradingSetupErrorMessage;
        }
        catch (Exception)
        {
            TradingSetupSaveErrorMessage = TradingSetupSaveErrorMessageFallback;
        }
        finally
        {
            IsTradingSetupSaving = false;
        }
    }

    private async Task<bool> ReloadTradeDetailAfterTradingSetupAsync(
        Guid tradeId,
        CancellationToken cancellationToken)
    {
        IsTradeDetailLoading = true;
        TradeDetailErrorMessage = null;

        try
        {
            TradeDetail? detail = await _tradeDetailReader.GetByIdAsync(
                tradeId,
                cancellationToken);
            if (detail is null)
            {
                TradeDetailErrorMessage = TradeDetailLoadErrorMessage;
                return false;
            }

            SelectedTradeDetail = detail;
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            TradeDetailErrorMessage = TradeDetailLoadErrorMessage;
            return false;
        }
        finally
        {
            IsTradeDetailLoading = false;
        }
    }

    private bool CanAssignMistake() =>
        IsTradeDetailVisible &&
        SelectedTradeDetail is not null &&
        SelectedTradingMistake is not null &&
        !IsTradeDetailLoading &&
        !IsTradeMistakesLoading &&
        !IsTradingMistakeOptionsLoading &&
        !IsAssigningMistake &&
        !IsRemovingMistake;

    private async Task AssignMistakeAsync(CancellationToken cancellationToken)
    {
        if (SelectedTradeDetail is not { } detail ||
            SelectedTradingMistake is not { } mistake)
        {
            return;
        }

        AssignMistakeErrorMessage = null;
        AssignMistakeSuccessMessage = null;
        RemoveMistakeErrorMessage = null;
        RemoveMistakeSuccessMessage = null;
        IsAssigningMistake = true;

        try
        {
            _ = await _assignTradeMistakeUseCase.ExecuteAsync(
                new AssignTradeMistakeCommand(
                    detail.Id,
                    mistake.Id,
                    TradeMistakeNoteText),
                cancellationToken);
            SelectedTradingMistake = null;
            TradeMistakeNoteText = string.Empty;
            AssignMistakeSuccessMessage = TradingMistakeAssignedMessage;
            _ = await LoadTradeMistakesAsync(
                detail.Id,
                forceRefresh: true,
                CancellationToken.None);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (KeyNotFoundException exception) when (
            exception.Message == AssignTradeMistakeUseCase.MissingMistakeMessage)
        {
            AssignMistakeErrorMessage = MissingTradingMistakeErrorMessage;
        }
        catch (InvalidOperationException exception) when (
            exception.Message == AssignTradeMistakeUseCase.InactiveMistakeMessage)
        {
            AssignMistakeErrorMessage = InactiveTradingMistakeErrorMessage;
        }
        catch (InvalidOperationException exception) when (
            exception.Message == AssignTradeMistakeUseCase.DuplicateMistakeMessage)
        {
            AssignMistakeErrorMessage = DuplicateTradingMistakeErrorMessage;
        }
        catch (Exception)
        {
            AssignMistakeErrorMessage = AssignMistakeErrorMessageFallback;
        }
        finally
        {
            IsAssigningMistake = false;
        }
    }

    private bool CanRemoveMistake(TradeMistakeListItem? item) =>
        item is not null &&
        IsTradeDetailVisible &&
        SelectedTradeDetail is { } detail &&
        item.TradeId == detail.Id &&
        !IsTradeDetailLoading &&
        !IsTradeMistakesLoading &&
        !IsAssigningMistake &&
        !IsRemovingMistake;

    private async Task RemoveMistakeAsync(
        TradeMistakeListItem? item,
        CancellationToken cancellationToken)
    {
        if (item is null || SelectedTradeDetail is not { } detail)
        {
            return;
        }

        AssignMistakeErrorMessage = null;
        AssignMistakeSuccessMessage = null;
        RemoveMistakeErrorMessage = null;
        RemoveMistakeSuccessMessage = null;
        IsRemovingMistake = true;

        try
        {
            await _removeTradeMistakeUseCase.ExecuteAsync(
                new RemoveTradeMistakeCommand(detail.Id, item.Id),
                cancellationToken);
            RemoveMistakeSuccessMessage = TradingMistakeRemovedMessage;
            _ = await LoadTradeMistakesAsync(
                detail.Id,
                forceRefresh: true,
                CancellationToken.None);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (KeyNotFoundException)
        {
            RemoveMistakeErrorMessage = MissingTradeMistakeErrorMessage;
        }
        catch (Exception)
        {
            RemoveMistakeErrorMessage = RemoveMistakeErrorMessageFallback;
        }
        finally
        {
            IsRemovingMistake = false;
        }
    }

    private bool CanOpenScreenshotPreview(TradeScreenshotListItem? item) =>
        item is not null &&
        IsTradeDetailVisible &&
        SelectedTradeDetail is { } detail &&
        item.TradeId == detail.Id &&
        !IsTradeDetailLoading &&
        !IsTradeScreenshotsLoading &&
        !IsTradingSetupSaving &&
        !IsAddingScreenshot &&
        !IsScreenshotPreviewLoading &&
        !IsDeletingScreenshot &&
        !IsClosingTrade;

    private async Task OpenScreenshotPreviewAsync(
        TradeScreenshotListItem? item,
        CancellationToken cancellationToken)
    {
        if (item is null ||
            SelectedTradeDetail is not { } detail ||
            !IsTradeDetailVisible ||
            item.TradeId != detail.Id)
        {
            return;
        }

        ResetScreenshotPreviewState();
        int requestVersion = _previewRequestVersion;
        PreviewScreenshotId = item.Id;
        PreviewScreenshotFileName = item.FileName;
        IsScreenshotPreviewLoading = true;

        try
        {
            TradeScreenshotContent? content =
                await _tradeScreenshotContentReader.OpenAsync(
                    item.Id,
                    cancellationToken);

            if (content is null)
            {
                if (IsCurrentPreviewRequest(requestVersion, item))
                {
                    ScreenshotPreviewErrorMessage =
                        StaleScreenshotPreviewErrorMessage;
                }

                return;
            }

            ImageSource decodedImage;
            await using (content.Content)
            {
                if (!IsCurrentPreviewRequest(requestVersion, item))
                {
                    return;
                }

                decodedImage = _tradeScreenshotImageDecoder.Decode(content.Content);
            }

            if (IsCurrentPreviewRequest(requestVersion, item))
            {
                PreviewScreenshotImage = decodedImage;
                IsScreenshotPreviewVisible = true;
            }
        }
        catch (OperationCanceledException)
        {
            if (requestVersion == _previewRequestVersion)
            {
                ResetScreenshotPreviewState();
            }

            throw;
        }
        catch (FileNotFoundException)
        {
            if (IsCurrentPreviewRequest(requestVersion, item))
            {
                ScreenshotPreviewErrorMessage =
                    MissingScreenshotFileErrorMessage;
            }
        }
        catch (Exception)
        {
            if (IsCurrentPreviewRequest(requestVersion, item))
            {
                ScreenshotPreviewErrorMessage =
                    ScreenshotPreviewErrorMessageFallback;
            }
        }
        finally
        {
            if (requestVersion == _previewRequestVersion)
            {
                IsScreenshotPreviewLoading = false;
            }
        }
    }

    private bool IsCurrentPreviewRequest(
        int requestVersion,
        TradeScreenshotListItem item) =>
        requestVersion == _previewRequestVersion &&
        PreviewScreenshotId == item.Id &&
        SelectedTradeDetail?.Id == item.TradeId &&
        IsTradeDetailVisible;

    private void CloseScreenshotPreview()
    {
        OpenScreenshotPreviewCommand.Cancel();
        ResetScreenshotPreviewState();
    }

    private bool CanCloseScreenshotPreview() =>
        PreviewScreenshotId is not null ||
        IsScreenshotPreviewVisible ||
        IsScreenshotPreviewLoading ||
        HasScreenshotPreviewError;

    private bool CanDeleteScreenshot(TradeScreenshotListItem? item) =>
        item is not null &&
        IsTradeDetailVisible &&
        SelectedTradeDetail is { } detail &&
        item.TradeId == detail.Id &&
        !IsTradeDetailLoading &&
        !IsTradeScreenshotsLoading &&
        !IsAddingScreenshot &&
        !IsDeletingScreenshot &&
        !IsScreenshotPreviewLoading &&
        !IsSaving &&
        !IsTradingSetupSaving &&
        !IsClosingTrade;

    private async Task DeleteScreenshotAsync(
        TradeScreenshotListItem? item,
        CancellationToken cancellationToken)
    {
        if (item is null ||
            SelectedTradeDetail is not { } detail ||
            !IsTradeDetailVisible ||
            item.TradeId != detail.Id)
        {
            return;
        }

        if (!_tradeScreenshotDeleteConfirmation.Confirm(item.FileName))
        {
            return;
        }

        ScreenshotDeleteErrorMessage = null;
        ScreenshotDeleteWarningMessage = null;
        ScreenshotDeleteSuccessMessage = null;
        IsDeletingScreenshot = true;

        try
        {
            DeleteTradeScreenshotResult result =
                await _deleteTradeScreenshotUseCase.ExecuteAsync(
                    new DeleteTradeScreenshotCommand(detail.Id, item.Id),
                    cancellationToken);

            if (PreviewScreenshotId == item.Id)
            {
                ResetScreenshotPreviewState();
            }

            ScreenshotDeleteSuccessMessage = ScreenshotDeletedMessage;
            if (!result.FileCleanupSucceeded)
            {
                ScreenshotDeleteWarningMessage =
                    ScreenshotCleanupWarningMessage;
            }

            _ = await LoadTradeScreenshotsAsync(
                detail.Id,
                forceRefresh: true,
                CancellationToken.None,
                clearOnFailure: true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (KeyNotFoundException)
        {
            ScreenshotDeleteErrorMessage = StaleScreenshotDeleteErrorMessage;
            _ = await LoadTradeScreenshotsAsync(
                detail.Id,
                forceRefresh: true,
                CancellationToken.None);
        }
        catch (Exception)
        {
            ScreenshotDeleteErrorMessage = ScreenshotDeleteErrorMessageFallback;
        }
        finally
        {
            IsDeletingScreenshot = false;
        }
    }

    private void ShowAddScreenshot()
    {
        ResetScreenshotDraft();
        ScreenshotValidationErrorMessage = null;
        ScreenshotSaveErrorMessage = null;
        ScreenshotSuccessMessage = null;
        IsAddScreenshotVisible = true;
    }

    private bool CanShowAddScreenshot() =>
        !IsAddScreenshotVisible &&
        IsTradeDetailVisible &&
        SelectedTradeDetail is not null &&
        !IsTradeDetailLoading &&
        !IsTradeScreenshotsLoading &&
        !IsAddingScreenshot &&
        !IsSaving &&
        !IsTradingSetupSaving &&
        !IsDeletingScreenshot &&
        !IsClosingTrade;

    private void CancelAddScreenshot()
    {
        ResetScreenshotDraft();
        ScreenshotValidationErrorMessage = null;
        ScreenshotSaveErrorMessage = null;
        IsAddScreenshotVisible = false;
    }

    private bool CanCancelAddScreenshot() =>
        IsAddScreenshotVisible &&
        !IsAddingScreenshot &&
        !IsDeletingScreenshot &&
        !IsTradingSetupSaving &&
        !IsClosingTrade;

    private bool CanSaveScreenshot() =>
        IsAddScreenshotVisible &&
        IsTradeDetailVisible &&
        SelectedTradeDetail is not null &&
        !IsTradeDetailLoading &&
        !IsTradeScreenshotsLoading &&
        !IsAddingScreenshot &&
        !IsSaving &&
        !IsTradingSetupSaving &&
        !IsDeletingScreenshot &&
        !IsClosingTrade;

    private async Task SaveScreenshotAsync(CancellationToken cancellationToken)
    {
        ScreenshotValidationErrorMessage = null;
        ScreenshotSaveErrorMessage = null;
        ScreenshotSuccessMessage = null;

        if (SelectedTradeDetail is not { } detail)
        {
            ScreenshotValidationErrorMessage =
                "A selected trade is required.";
            return;
        }

        if (SelectedScreenshotType is not { } screenshotType)
        {
            ScreenshotValidationErrorMessage = "Screenshot type is required.";
            return;
        }

        if (!Enum.IsDefined(screenshotType))
        {
            ScreenshotValidationErrorMessage = "Screenshot type is invalid.";
            return;
        }

        DateTimeOffset? capturedAtUtc = null;
        if (!string.IsNullOrWhiteSpace(ScreenshotCapturedAtUtcText))
        {
            if (!TryParseUtcTimestamp(
                    ScreenshotCapturedAtUtcText,
                    out DateTimeOffset parsedCapturedAtUtc))
            {
                ScreenshotValidationErrorMessage =
                    "Captured time must be a valid UTC timestamp.";
                return;
            }

            capturedAtUtc = parsedCapturedAtUtc;
        }

        IsAddingScreenshot = true;

        try
        {
            TradeScreenshotFileSelection? selection =
                _tradeScreenshotFilePicker.Pick();
            if (selection is null)
            {
                return;
            }

            await using (selection)
            {
                var command = new AddTradeScreenshotCommand(
                    detail.Id,
                    screenshotType,
                    selection.Content,
                    selection.FileName,
                    capturedAtUtc,
                    ScreenshotTimeframeText,
                    ScreenshotDescriptionText);

                _ = await _addTradeScreenshotUseCase.ExecuteAsync(
                    command,
                    cancellationToken);
            }

            ResetScreenshotDraft();
            IsAddScreenshotVisible = false;
            ScreenshotSuccessMessage = ScreenshotSavedMessage;

            // The file and metadata write is committed. The projection refresh is
            // authoritative and best-effort, independent from command cancellation.
            _ = await LoadTradeScreenshotsAsync(
                detail.Id,
                forceRefresh: true,
                CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (KeyNotFoundException)
        {
            ScreenshotSaveErrorMessage = StaleTradeScreenshotErrorMessage;
        }
        catch (Exception)
        {
            ScreenshotSaveErrorMessage = ScreenshotSaveErrorMessageFallback;
        }
        finally
        {
            IsAddingScreenshot = false;
        }
    }

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
        catch (KeyNotFoundException exception)
        {
            SaveErrorMessage = exception.Message ==
                "The selected trading setup was not found."
                    ? MissingTradingSetupErrorMessage
                    : StaleReferenceErrorMessage;
        }
        catch (InvalidOperationException exception) when (
            exception.Message == "The selected trading setup is inactive.")
        {
            SaveErrorMessage = InactiveTradingSetupErrorMessage;
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

    private bool FailValidation(ManualTradeInputField field, string message)
    {
        SetInvalidManualTradeInput(field);
        ValidationErrorMessage = message;
        return false;
    }

    private void ClearManualTradeInputError(ManualTradeInputField field)
    {
        if (_invalidManualTradeInput == field && IsManualTradeInputValid(field))
        {
            SetInvalidManualTradeInput(null);
            ValidationErrorMessage = null;
        }
    }

    private bool IsManualTradeInputValid(ManualTradeInputField field) => field switch
    {
        ManualTradeInputField.Account => SelectedAccount is not null,
        ManualTradeInputField.Instrument => SelectedInstrument is not null,
        ManualTradeInputField.Direction => SelectedDirection is { } direction && Enum.IsDefined(direction),
        ManualTradeInputField.Quantity => IsQuantityValid(),
        ManualTradeInputField.EntryExecutedAtUtc => TryParseUtcTimestamp(EntryExecutedAtUtcText, out _),
        ManualTradeInputField.EntryPrice => TryParseDecimal(EntryPriceText, out _),
        ManualTradeInputField.EntryCommission => TryParseNonNegativeCost(EntryCommissionText, out _),
        ManualTradeInputField.EntryFees => TryParseNonNegativeCost(EntryFeesText, out _),
        ManualTradeInputField.ExitExecutedAtUtc => IsExitTimestampValid(),
        ManualTradeInputField.ExitPrice => TryParseDecimal(ExitPriceText, out _),
        ManualTradeInputField.ExitCommission => TryParseNonNegativeCost(ExitCommissionText, out _),
        ManualTradeInputField.ExitFees => TryParseNonNegativeCost(ExitFeesText, out _),
        _ => false,
    };

    private bool IsQuantityValid()
    {
        if (!TryParseDecimal(QuantityText, out decimal quantity) || quantity <= 0)
        {
            return false;
        }

        if (SelectedInstrument is not { } instrument)
        {
            return true;
        }

        try
        {
            TradeQuantityPolicy.Validate(instrument.AssetClass, quantity, nameof(QuantityText));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private bool IsExitTimestampValid() =>
        TryParseUtcTimestamp(EntryExecutedAtUtcText, out DateTimeOffset entry) &&
        TryParseUtcTimestamp(ExitExecutedAtUtcText, out DateTimeOffset exit) &&
        exit >= entry;

    private void SetInvalidManualTradeInput(ManualTradeInputField? value)
    {
        if (_invalidManualTradeInput == value)
        {
            return;
        }

        _invalidManualTradeInput = value;
        OnPropertyChanged(nameof(IsTradingAccountInvalid));
        OnPropertyChanged(nameof(IsInstrumentInvalid));
        OnPropertyChanged(nameof(IsDirectionInvalid));
        OnPropertyChanged(nameof(IsQuantityInvalid));
        OnPropertyChanged(nameof(IsEntryExecutedAtUtcInvalid));
        OnPropertyChanged(nameof(IsEntryPriceInvalid));
        OnPropertyChanged(nameof(IsEntryCommissionInvalid));
        OnPropertyChanged(nameof(IsEntryFeesInvalid));
        OnPropertyChanged(nameof(IsExitExecutedAtUtcInvalid));
        OnPropertyChanged(nameof(IsExitPriceInvalid));
        OnPropertyChanged(nameof(IsExitCommissionInvalid));
        OnPropertyChanged(nameof(IsExitFeesInvalid));
    }

    private bool FailCloseTradeValidation(string message)
    {
        CloseTradeValidationErrorMessage = message;
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
        SelectedTradingSetup = null;
        SelectedDirection = null;
        QuantityText = string.Empty;
        EntryExecutedAtUtcText = string.Empty;
        EntryPriceText = string.Empty;
        EntryCommissionText = "0";
        EntryFeesText = "0";
        SetInvalidManualTradeInput(null);
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

    private void ResetTradeEditState()
    {
        IsTradeEditVisible = false;
        TradeUpdateErrorMessage = null;
        TradeUpdateSuccessMessage = null;
        _editingTradeId = null;
        _editingEntryExecutionId = Guid.Empty;
        _editingExitExecutionId = Guid.Empty;
        ResetManualEntryForm();
        AccountOptions = _allAccountOptions.Where(option => option.IsActive).ToList();
        InstrumentOptions = _allInstrumentOptions.Where(option => option.IsActive).ToList();
        AvailableTradingSetups = _allTradingSetups
            .Where(option => option.IsActive)
            .ToList();
    }

    private static string FormatDecimal(decimal value) =>
        value.ToString("G29", CultureInfo.InvariantCulture);

    private static string FormatTimestamp(DateTimeOffset value) =>
        value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private void ResetExitFields()
    {
        ExitExecutedAtUtcText = string.Empty;
        ExitPriceText = string.Empty;
        ExitCommissionText = "0";
        ExitFeesText = "0";
    }

    private void ResetScreenshotDraft()
    {
        SelectedScreenshotType = null;
        ScreenshotCapturedAtUtcText = string.Empty;
        ScreenshotTimeframeText = string.Empty;
        ScreenshotDescriptionText = string.Empty;
    }

    private void ResetCloseTradeDraft()
    {
        CloseTradeExecutedAtUtcText = string.Empty;
        CloseTradePriceText = string.Empty;
        CloseTradeCommissionText = "0";
        CloseTradeFeesText = "0";
    }

    private void ClearCloseTradeState()
    {
        ResetCloseTradeDraft();
        CloseTradeValidationErrorMessage = null;
        CloseTradeSaveErrorMessage = null;
        CloseTradeSuccessMessage = null;
        IsCloseTradeVisible = false;
    }

    private void ClearTradeScreenshotState()
    {
        TradeScreenshots = [];
        TradeScreenshotsErrorMessage = null;
        _loadedTradeScreenshotsTradeId = null;
        ResetScreenshotDraft();
        ScreenshotValidationErrorMessage = null;
        ScreenshotSaveErrorMessage = null;
        ScreenshotSuccessMessage = null;
        IsAddScreenshotVisible = false;
        ScreenshotDeleteErrorMessage = null;
        ScreenshotDeleteWarningMessage = null;
        ScreenshotDeleteSuccessMessage = null;
        ResetScreenshotPreviewState();
    }

    private void ClearTradeMistakeState()
    {
        TradeMistakes = [];
        _loadedTradeMistakesTradeId = null;
        SelectedTradingMistake = null;
        TradeMistakeNoteText = string.Empty;
        TradeMistakesErrorMessage = null;
        AssignMistakeErrorMessage = null;
        AssignMistakeSuccessMessage = null;
        RemoveMistakeErrorMessage = null;
        RemoveMistakeSuccessMessage = null;
    }

    private async Task<bool> LoadTradeMistakesAsync(
        Guid tradeId,
        bool forceRefresh,
        CancellationToken cancellationToken,
        bool clearOnFailure = false)
    {
        if (!forceRefresh && _loadedTradeMistakesTradeId == tradeId)
        {
            return true;
        }

        IsTradeMistakesLoading = true;
        TradeMistakesErrorMessage = null;

        try
        {
            IReadOnlyList<TradeMistakeListItem> tradeMistakes =
                await _tradeMistakeReader.GetByTradeIdAsync(
                    tradeId,
                    cancellationToken);
            if (SelectedTradeDetail?.Id == tradeId && IsTradeDetailVisible)
            {
                TradeMistakes = tradeMistakes;
                _loadedTradeMistakesTradeId = tradeId;
            }

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            if (SelectedTradeDetail?.Id == tradeId && IsTradeDetailVisible)
            {
                if (clearOnFailure)
                {
                    TradeMistakes = [];
                    _loadedTradeMistakesTradeId = null;
                }

                TradeMistakesErrorMessage = TradeMistakesLoadErrorMessage;
            }

            return false;
        }
        finally
        {
            IsTradeMistakesLoading = false;
        }
    }

    private void ResetScreenshotPreviewState()
    {
        _previewRequestVersion++;
        IsScreenshotPreviewVisible = false;
        IsScreenshotPreviewLoading = false;
        PreviewScreenshotImage = null;
        PreviewScreenshotId = null;
        PreviewScreenshotFileName = null;
        ScreenshotPreviewErrorMessage = null;
    }

    private async Task<bool> LoadTradeScreenshotsAsync(
        Guid tradeId,
        bool forceRefresh,
        CancellationToken cancellationToken,
        bool clearOnFailure = false)
    {
        if (!forceRefresh && _loadedTradeScreenshotsTradeId == tradeId)
        {
            return true;
        }

        IsTradeScreenshotsLoading = true;
        TradeScreenshotsErrorMessage = null;

        try
        {
            IReadOnlyList<TradeScreenshotListItem> screenshots =
                await _tradeScreenshotReader.GetForTradeAsync(
                    tradeId,
                    cancellationToken);

            if (SelectedTradeDetail?.Id == tradeId && IsTradeDetailVisible)
            {
                TradeScreenshots = screenshots;
                _loadedTradeScreenshotsTradeId = tradeId;
            }

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            if (SelectedTradeDetail?.Id == tradeId && IsTradeDetailVisible)
            {
                if (clearOnFailure)
                {
                    TradeScreenshots = [];
                    _loadedTradeScreenshotsTradeId = null;
                }

                TradeScreenshotsErrorMessage = TradeScreenshotsLoadErrorMessage;
            }

            return false;
        }
        finally
        {
            IsTradeScreenshotsLoading = false;
        }
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
                IReadOnlyList<TradingSetupListItem> tradingSetups =
                    await _tradingSetupReader.GetAllAsync(cancellationToken);

                Guid? selectedAccountId = SelectedAccount?.Id;
                Guid? selectedInstrumentId = SelectedInstrument?.Id;
                Guid? selectedTradingSetupId = SelectedTradingSetup?.Id;
                _allAccountOptions = referenceData.Accounts;
                _allInstrumentOptions = referenceData.Instruments;
                AccountOptions = referenceData.Accounts;
                InstrumentOptions = referenceData.Instruments;
                _allTradingSetups = tradingSetups;
                AvailableTradingSetups = tradingSetups
                    .Where(setup => setup.IsActive)
                    .ToList();
                SelectedAccount = selectedAccountId.HasValue
                    ? AccountOptions.SingleOrDefault(
                        option => option.Id == selectedAccountId.Value)
                    : null;
                SelectedInstrument = selectedInstrumentId.HasValue
                    ? InstrumentOptions.SingleOrDefault(
                        option => option.Id == selectedInstrumentId.Value)
                    : null;
                SelectedTradingSetup = selectedTradingSetupId.HasValue
                    ? AvailableTradingSetups.SingleOrDefault(
                        setup => setup.Id == selectedTradingSetupId.Value)
                    : null;
                SynchronizeTradeDetailTradingSetupOptions();
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

    private async Task<bool> LoadEditReferenceDataAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            ManualTradeReferenceData referenceData =
                await _referenceDataReader.GetAsync(
                    includeInactiveReferences: true,
                    cancellationToken);
            IReadOnlyList<TradingSetupListItem> tradingSetups =
                await _tradingSetupReader.GetAllAsync(cancellationToken);

            _allAccountOptions = referenceData.Accounts;
            _allInstrumentOptions = referenceData.Instruments;
            _allTradingSetups = tradingSetups;
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void SynchronizeTradeDetailTradingSetupOptions()
    {
        Guid? currentSetupId = SelectedTradeDetail?.TradingSetupId;
        TradeDetailTradingSetupOptions = _allTradingSetups
            .Where(setup => setup.IsActive || setup.Id == currentSetupId)
            .ToList();
        SelectedTradeDetailTradingSetup = currentSetupId.HasValue
            ? TradeDetailTradingSetupOptions.SingleOrDefault(
                setup => setup.Id == currentSetupId.Value)
            : null;
    }

    private async Task<bool> LoadTradingMistakeOptionsAsync(
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        if (!await _tradingMistakeOptionsLoadGate.WaitAsync(0, cancellationToken))
        {
            return false;
        }

        try
        {
            if (!forceRefresh && _hasTradingMistakeOptionsLoadedSuccessfully)
            {
                return true;
            }

            IsTradingMistakeOptionsLoading = true;
            TradingMistakeOptionsErrorMessage = null;

            try
            {
                IReadOnlyList<TradingMistakeListItem> mistakes =
                    await _tradingMistakeReader.GetAllAsync(cancellationToken);
                _allTradingMistakes = mistakes;
                SynchronizeAvailableTradingMistakes();
                _hasTradingMistakeOptionsLoadedSuccessfully = true;
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                TradingMistakeOptionsErrorMessage =
                    TradingMistakeOptionsLoadErrorMessage;
                return false;
            }
            finally
            {
                IsTradingMistakeOptionsLoading = false;
            }
        }
        finally
        {
            _tradingMistakeOptionsLoadGate.Release();
        }
    }

    private void SynchronizeAvailableTradingMistakes()
    {
        var assignedMistakeIds = TradeMistakes
            .Select(item => item.TradingMistakeId)
            .ToHashSet();
        Guid? selectedMistakeId = SelectedTradingMistake?.Id;
        AvailableTradingMistakes = _allTradingMistakes
            .Where(item => item.IsActive && !assignedMistakeIds.Contains(item.Id))
            .ToList();
        SelectedTradingMistake = selectedMistakeId.HasValue
            ? AvailableTradingMistakes.SingleOrDefault(
                item => item.Id == selectedMistakeId.Value)
            : null;
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
