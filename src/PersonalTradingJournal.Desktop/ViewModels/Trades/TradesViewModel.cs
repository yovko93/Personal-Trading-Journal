using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.Screenshots;
using PersonalTradingJournal.Domain.Screenshots;
using PersonalTradingJournal.Domain.Trades;
using System.Windows.Media;

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
    private const string TradeDetailLoadErrorMessage =
        "Trade details could not be loaded.";
    private const string TradeListLoadErrorMessage = "Trades could not be loaded.";
    private const string SaveErrorMessageFallback = "Trade could not be saved.";
    private const string StaleReferenceErrorMessage =
        "The selected trading account or instrument is no longer available. " +
        "Refresh the reference data and try again.";
    private const string TradeSavedMessage = "Trade saved successfully.";
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
    private Guid? _loadedTradeScreenshotsTradeId;
    private IReadOnlyList<ManualTradeInstrumentOption> _instrumentOptions = [];
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
    private string _quantityText = string.Empty;
    private IReadOnlyList<TradeListItem> _recentTrades = [];
    private string? _saveErrorMessage;
    private ManualTradeAccountOption? _selectedAccount;
    private TradeDirection? _selectedDirection;
    private ManualTradeInstrumentOption? _selectedInstrument;
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

    public TradesViewModel(
        IManualTradeReferenceDataReader referenceDataReader,
        ITradeListReader tradeListReader,
        ITradeDetailReader tradeDetailReader,
        CreateManualTradeUseCase createManualTradeUseCase,
        ITradeScreenshotReader tradeScreenshotReader,
        AddTradeScreenshotUseCase addTradeScreenshotUseCase,
        ITradeScreenshotFilePicker tradeScreenshotFilePicker,
        ITradeScreenshotContentReader tradeScreenshotContentReader,
        ITradeScreenshotImageDecoder tradeScreenshotImageDecoder,
        DeleteTradeScreenshotUseCase deleteTradeScreenshotUseCase,
        ITradeScreenshotDeleteConfirmation tradeScreenshotDeleteConfirmation)
    {
        ArgumentNullException.ThrowIfNull(referenceDataReader);
        ArgumentNullException.ThrowIfNull(tradeListReader);
        ArgumentNullException.ThrowIfNull(tradeDetailReader);
        ArgumentNullException.ThrowIfNull(createManualTradeUseCase);
        ArgumentNullException.ThrowIfNull(tradeScreenshotReader);
        ArgumentNullException.ThrowIfNull(addTradeScreenshotUseCase);
        ArgumentNullException.ThrowIfNull(tradeScreenshotFilePicker);
        ArgumentNullException.ThrowIfNull(tradeScreenshotContentReader);
        ArgumentNullException.ThrowIfNull(tradeScreenshotImageDecoder);
        ArgumentNullException.ThrowIfNull(deleteTradeScreenshotUseCase);
        ArgumentNullException.ThrowIfNull(tradeScreenshotDeleteConfirmation);

        _referenceDataReader = referenceDataReader;
        _tradeListReader = tradeListReader;
        _tradeDetailReader = tradeDetailReader;
        _createManualTradeUseCase = createManualTradeUseCase;
        _tradeScreenshotReader = tradeScreenshotReader;
        _addTradeScreenshotUseCase = addTradeScreenshotUseCase;
        _tradeScreenshotFilePicker = tradeScreenshotFilePicker;
        _tradeScreenshotContentReader = tradeScreenshotContentReader;
        _tradeScreenshotImageDecoder = tradeScreenshotImageDecoder;
        _deleteTradeScreenshotUseCase = deleteTradeScreenshotUseCase;
        _tradeScreenshotDeleteConfirmation = tradeScreenshotDeleteConfirmation;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, CanRefresh);
        ShowManualEntryCommand = new RelayCommand(ShowManualEntry, CanShowManualEntry);
        CancelManualEntryCommand = new RelayCommand(
            CancelManualEntry,
            CanCancelManualEntry);
        SaveManualTradeCommand = new AsyncRelayCommand(
            SaveManualTradeAsync,
            CanSaveManualTrade);
        ShowTradeDetailCommand = new AsyncRelayCommand<TradeListItem>(
            ShowTradeDetailAsync,
            CanShowTradeDetail);
        CloseTradeDetailCommand = new RelayCommand(
            CloseTradeDetail,
            CanCloseTradeDetail);
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

    public TradeDetail? SelectedTradeDetail
    {
        get => _selectedTradeDetail;
        private set
        {
            if (SetProperty(ref _selectedTradeDetail, value))
            {
                ShowAddScreenshotCommand.NotifyCanExecuteChanged();
                SaveScreenshotCommand.NotifyCanExecuteChanged();
                OpenScreenshotPreviewCommand.NotifyCanExecuteChanged();
                DeleteScreenshotCommand.NotifyCanExecuteChanged();
            }
        }
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
                ShowTradeDetailCommand.NotifyCanExecuteChanged();
                ShowAddScreenshotCommand.NotifyCanExecuteChanged();
                SaveScreenshotCommand.NotifyCanExecuteChanged();
                DeleteScreenshotCommand.NotifyCanExecuteChanged();
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
            }
        }
    }

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

    public IAsyncRelayCommand<TradeListItem> ShowTradeDetailCommand { get; }

    public IRelayCommand CloseTradeDetailCommand { get; }

    public IRelayCommand ShowAddScreenshotCommand { get; }

    public IRelayCommand CancelAddScreenshotCommand { get; }

    public IAsyncRelayCommand SaveScreenshotCommand { get; }

    public IAsyncRelayCommand<TradeScreenshotListItem>
        OpenScreenshotPreviewCommand { get; }

    public IRelayCommand CloseScreenshotPreviewCommand { get; }

    public IAsyncRelayCommand<TradeScreenshotListItem> DeleteScreenshotCommand { get; }

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

        if (IsTradeDetailVisible && SelectedTradeDetail is { } detail)
        {
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
        !IsSaving &&
        !IsAddingScreenshot &&
        !IsDeletingScreenshot;

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
        IsManualEntryVisible &&
        !IsLoading &&
        !IsTradeListLoading &&
        !IsTradeDetailLoading &&
        !IsSaving &&
        !IsAddingScreenshot;

    private bool CanShowTradeDetail(TradeListItem? item) =>
        item is not null &&
        !IsTradeDetailLoading &&
        !IsTradeScreenshotsLoading &&
        !IsTradeListLoading &&
        !IsSaving &&
        !IsAddingScreenshot &&
        !IsDeletingScreenshot;

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
        ClearTradeScreenshotState();
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
        ClearTradeScreenshotState();
    }

    private bool CanCloseTradeDetail() =>
        IsTradeDetailVisible &&
        !IsTradeDetailLoading &&
        !IsTradeScreenshotsLoading &&
        !IsAddingScreenshot &&
        !IsDeletingScreenshot;

    private bool CanOpenScreenshotPreview(TradeScreenshotListItem? item) =>
        item is not null &&
        IsTradeDetailVisible &&
        SelectedTradeDetail is { } detail &&
        item.TradeId == detail.Id &&
        !IsTradeDetailLoading &&
        !IsTradeScreenshotsLoading &&
        !IsAddingScreenshot &&
        !IsScreenshotPreviewLoading &&
        !IsDeletingScreenshot;

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
        !IsSaving;

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
        !IsDeletingScreenshot;

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
        !IsDeletingScreenshot;

    private bool CanSaveScreenshot() =>
        IsAddScreenshotVisible &&
        IsTradeDetailVisible &&
        SelectedTradeDetail is not null &&
        !IsTradeDetailLoading &&
        !IsTradeScreenshotsLoading &&
        !IsAddingScreenshot &&
        !IsSaving &&
        !IsDeletingScreenshot;

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

    private void ResetScreenshotDraft()
    {
        SelectedScreenshotType = null;
        ScreenshotCapturedAtUtcText = string.Empty;
        ScreenshotTimeframeText = string.Empty;
        ScreenshotDescriptionText = string.Empty;
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
