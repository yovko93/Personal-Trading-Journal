using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Desktop.Dialogs;
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
    private const string CreateReloadErrorMessage =
        "Instrument was created, but the list could not be refreshed. Refresh to see the latest data.";
    private const string InvalidInstrumentDetailsMessage = "Please check the instrument details.";
    private const string LifecycleErrorMessageFallback = "Instrument status could not be changed.";
    private const string LifecycleNotFoundMessage = "Instrument no longer exists. Refresh the list.";
    private const string LifecycleReloadErrorMessage =
        "Instrument status changed, but the list could not be refreshed. Refresh to see the latest status.";
    private const string LoadErrorMessage = "Instruments could not be loaded.";
    private const string DetailErrorMessageFallback = "Instrument details could not be loaded.";
    private const string DetailNotFoundMessage = "Instrument no longer exists. The list was refreshed.";
    private const string UpdateErrorMessageFallback = "Instrument changes could not be saved.";
    private const string UpdateReloadErrorMessage =
        "Instrument changes were saved, but the list could not be refreshed.";
    private const string AssetClassChangeBlockedMessage =
        "Asset class cannot be changed because this instrument is used by existing trades.";
    private const string DeleteErrorMessageFallback = "Instrument could not be deleted.";
    private const string DeleteReloadErrorMessage =
        "Instrument was deleted, but the list could not be refreshed.";
    private const string DeleteBlockedTitle = "Cannot delete instrument";
    private const string DeleteBlockedMessage =
        "This instrument is used by existing trades and cannot be deleted. " +
        "Deactivate it instead to preserve historical data.";

    private readonly IInstrumentReader _instrumentReader;
    private readonly CreateInstrumentUseCase _createInstrumentUseCase;
    private readonly InstrumentLifecycleUseCase _instrumentLifecycleUseCase;
    private readonly GetInstrumentDetailsUseCase _getInstrumentDetailsUseCase;
    private readonly UpdateInstrumentUseCase _updateInstrumentUseCase;
    private readonly DeleteInstrumentUseCase _deleteInstrumentUseCase;
    private readonly IDialogService _dialogService;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private string _currency = string.Empty;
    private string? _createErrorMessage;
    private string _displayName = string.Empty;
    private string? _errorMessage;
    private string _exchange = string.Empty;
    private bool _hasLoadedSuccessfully;
    private IReadOnlyList<InstrumentListItem> _instruments = [];
    private bool _isCreateFormVisible;
    private bool _isChangingInstrumentState;
    private bool _isCreating;
    private bool _isLoading;
    private bool _isCurrencyInvalid;
    private bool _isDisplayNameInvalid;
    private bool _isSymbolInvalid;
    private bool _isTickSizeInvalid;
    private bool _isTickValueInvalid;
    private string? _lifecycleErrorMessage;
    private AssetClass _selectedAssetClass = AssetClass.Futures;
    private string _symbol = string.Empty;
    private string _tickSizeText = string.Empty;
    private string _tickValueText = string.Empty;
    private InstrumentDetails? _selectedInstrument;
    private bool _isLoadingInstrumentDetails;
    private bool _isEditFormVisible;
    private bool _isUpdating;
    private bool _isDeleting;
    private string _editSymbol = string.Empty;
    private string _editDisplayName = string.Empty;
    private AssetClass _editSelectedAssetClass = AssetClass.Futures;
    private string _editExchange = string.Empty;
    private string _editCurrency = string.Empty;
    private string _editTickSizeText = string.Empty;
    private string _editTickValueText = string.Empty;
    private bool _isEditSymbolInvalid;
    private bool _isEditDisplayNameInvalid;
    private bool _isEditCurrencyInvalid;
    private bool _isEditTickSizeInvalid;
    private bool _isEditTickValueInvalid;
    private string? _editErrorMessage;
    private string? _actionErrorMessage;

    public InstrumentsViewModel(
        IInstrumentReader instrumentReader,
        CreateInstrumentUseCase createInstrumentUseCase,
        InstrumentLifecycleUseCase instrumentLifecycleUseCase,
        GetInstrumentDetailsUseCase getInstrumentDetailsUseCase,
        UpdateInstrumentUseCase updateInstrumentUseCase,
        DeleteInstrumentUseCase deleteInstrumentUseCase,
        IDialogService dialogService)
    {
        ArgumentNullException.ThrowIfNull(instrumentReader);
        ArgumentNullException.ThrowIfNull(createInstrumentUseCase);
        ArgumentNullException.ThrowIfNull(instrumentLifecycleUseCase);
        ArgumentNullException.ThrowIfNull(getInstrumentDetailsUseCase);
        ArgumentNullException.ThrowIfNull(updateInstrumentUseCase);
        ArgumentNullException.ThrowIfNull(deleteInstrumentUseCase);
        ArgumentNullException.ThrowIfNull(dialogService);

        _instrumentReader = instrumentReader;
        _createInstrumentUseCase = createInstrumentUseCase;
        _instrumentLifecycleUseCase = instrumentLifecycleUseCase;
        _getInstrumentDetailsUseCase = getInstrumentDetailsUseCase;
        _updateInstrumentUseCase = updateInstrumentUseCase;
        _deleteInstrumentUseCase = deleteInstrumentUseCase;
        _dialogService = dialogService;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, CanRefresh);
        ShowCreateFormCommand = new RelayCommand(ShowCreateForm, CanShowCreateForm);
        CancelCreateCommand = new RelayCommand(CancelCreate, CanCancelCreate);
        CreateInstrumentCommand = new AsyncRelayCommand(
            CreateInstrumentAsync,
            CanCreateInstrument);
        ActivateInstrumentCommand = new AsyncRelayCommand<InstrumentListItem>(
            ActivateInstrumentAsync,
            CanActivateInstrument);
        DeactivateInstrumentCommand = new AsyncRelayCommand<InstrumentListItem>(
            DeactivateInstrumentAsync,
            CanDeactivateInstrument);
        ViewInstrumentCommand = new AsyncRelayCommand<Guid>(ViewInstrumentAsync, CanUseInstrumentAction);
        EditInstrumentCommand = new AsyncRelayCommand<Guid>(EditInstrumentAsync, CanUseInstrumentAction);
        CloseInstrumentDetailsCommand = new RelayCommand(
            CloseInstrumentDetails,
            CanCloseInstrumentDetails);
        CancelEditCommand = new RelayCommand(CancelEdit, CanCancelEdit);
        SaveChangesCommand = new AsyncRelayCommand(SaveChangesAsync, CanSaveChanges);
        DeleteInstrumentCommand = new AsyncRelayCommand<Guid>(
            DeleteInstrumentAsync,
            CanUseInstrumentAction);
    }

    public IReadOnlyList<InstrumentListItem> Instruments
    {
        get => _instruments;
        private set
        {
            if (SetProperty(ref _instruments, value))
            {
                OnPropertyChanged(nameof(HasInstruments));
                ActivateInstrumentCommand.NotifyCanExecuteChanged();
                DeactivateInstrumentCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(SelectedInstrumentListItem));
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
        set
        {
            if (SetProperty(ref _symbol, value) &&
                IsSymbolInvalid &&
                !string.IsNullOrWhiteSpace(value))
            {
                IsSymbolInvalid = false;
                CreateErrorMessage = null;
            }
        }
    }

    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (SetProperty(ref _displayName, value) &&
                IsDisplayNameInvalid &&
                !string.IsNullOrWhiteSpace(value))
            {
                IsDisplayNameInvalid = false;
                CreateErrorMessage = null;
            }
        }
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

    public string TickSizeText
    {
        get => _tickSizeText;
        set
        {
            if (SetProperty(ref _tickSizeText, value) &&
                IsTickSizeInvalid &&
                TryParsePositiveDecimal(value, out _))
            {
                IsTickSizeInvalid = false;
                CreateErrorMessage = null;
            }
        }
    }

    public string TickValueText
    {
        get => _tickValueText;
        set
        {
            if (SetProperty(ref _tickValueText, value) &&
                IsTickValueInvalid &&
                TryParsePositiveDecimal(value, out _))
            {
                IsTickValueInvalid = false;
                CreateErrorMessage = null;
            }
        }
    }

    public bool IsSymbolInvalid { get => _isSymbolInvalid; private set => SetProperty(ref _isSymbolInvalid, value); }
    public bool IsDisplayNameInvalid { get => _isDisplayNameInvalid; private set => SetProperty(ref _isDisplayNameInvalid, value); }
    public bool IsCurrencyInvalid { get => _isCurrencyInvalid; private set => SetProperty(ref _isCurrencyInvalid, value); }
    public bool IsTickSizeInvalid { get => _isTickSizeInvalid; private set => SetProperty(ref _isTickSizeInvalid, value); }
    public bool IsTickValueInvalid { get => _isTickValueInvalid; private set => SetProperty(ref _isTickValueInvalid, value); }

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

    public bool IsChangingInstrumentState
    {
        get => _isChangingInstrumentState;
        private set
        {
            if (SetProperty(ref _isChangingInstrumentState, value))
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

    public InstrumentDetails? SelectedInstrument
    {
        get => _selectedInstrument;
        private set
        {
            if (SetProperty(ref _selectedInstrument, value))
            {
                OnPropertyChanged(nameof(HasSelectedInstrument));
                OnPropertyChanged(nameof(SelectedInstrumentListItem));
                NotifyOperationCanExecuteChanged();
            }
        }
    }

    public bool HasSelectedInstrument => SelectedInstrument is not null;

    public InstrumentListItem? SelectedInstrumentListItem => SelectedInstrument is null
        ? null
        : Instruments.FirstOrDefault(item => item.Id == SelectedInstrument.Id);

    public bool IsLoadingInstrumentDetails
    {
        get => _isLoadingInstrumentDetails;
        private set
        {
            if (SetProperty(ref _isLoadingInstrumentDetails, value))
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

    public string EditSymbol
    {
        get => _editSymbol;
        set
        {
            if (SetProperty(ref _editSymbol, value) &&
                IsEditSymbolInvalid &&
                !string.IsNullOrWhiteSpace(value))
            {
                IsEditSymbolInvalid = false;
                EditErrorMessage = null;
            }
        }
    }

    public string EditDisplayName
    {
        get => _editDisplayName;
        set
        {
            if (SetProperty(ref _editDisplayName, value) &&
                IsEditDisplayNameInvalid &&
                !string.IsNullOrWhiteSpace(value))
            {
                IsEditDisplayNameInvalid = false;
                EditErrorMessage = null;
            }
        }
    }

    public AssetClass EditSelectedAssetClass
    {
        get => _editSelectedAssetClass;
        set => SetProperty(ref _editSelectedAssetClass, value);
    }

    public string EditExchange
    {
        get => _editExchange;
        set => SetProperty(ref _editExchange, value);
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

    public string EditTickSizeText
    {
        get => _editTickSizeText;
        set
        {
            if (!SetProperty(ref _editTickSizeText, value))
            {
                return;
            }

            OnPropertyChanged(nameof(EditPointValuePreview));
            if (IsEditTickSizeInvalid && TryParsePositiveDecimal(value, out _))
            {
                IsEditTickSizeInvalid = false;
                EditErrorMessage = null;
            }
        }
    }

    public string EditTickValueText
    {
        get => _editTickValueText;
        set
        {
            if (!SetProperty(ref _editTickValueText, value))
            {
                return;
            }

            OnPropertyChanged(nameof(EditPointValuePreview));
            if (IsEditTickValueInvalid && TryParsePositiveDecimal(value, out _))
            {
                IsEditTickValueInvalid = false;
                EditErrorMessage = null;
            }
        }
    }

    public decimal? EditPointValuePreview =>
        TryParsePositiveDecimal(EditTickSizeText, out decimal tickSize) &&
        TryParsePositiveDecimal(EditTickValueText, out decimal tickValue)
            ? tickValue / tickSize
            : null;

    public bool IsEditSymbolInvalid { get => _isEditSymbolInvalid; private set => SetProperty(ref _isEditSymbolInvalid, value); }
    public bool IsEditDisplayNameInvalid { get => _isEditDisplayNameInvalid; private set => SetProperty(ref _isEditDisplayNameInvalid, value); }
    public bool IsEditCurrencyInvalid { get => _isEditCurrencyInvalid; private set => SetProperty(ref _isEditCurrencyInvalid, value); }
    public bool IsEditTickSizeInvalid { get => _isEditTickSizeInvalid; private set => SetProperty(ref _isEditTickSizeInvalid, value); }
    public bool IsEditTickValueInvalid { get => _isEditTickValueInvalid; private set => SetProperty(ref _isEditTickValueInvalid, value); }

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

    public IAsyncRelayCommand CreateInstrumentCommand { get; }

    public IAsyncRelayCommand<InstrumentListItem> ActivateInstrumentCommand { get; }

    public IAsyncRelayCommand<InstrumentListItem> DeactivateInstrumentCommand { get; }

    public IAsyncRelayCommand<Guid> ViewInstrumentCommand { get; }

    public IAsyncRelayCommand<Guid> EditInstrumentCommand { get; }

    public IRelayCommand CloseInstrumentDetailsCommand { get; }

    public IRelayCommand CancelEditCommand { get; }

    public IAsyncRelayCommand SaveChangesCommand { get; }

    public IAsyncRelayCommand<Guid> DeleteInstrumentCommand { get; }

    public async Task EnsureLoadedAsync()
    {
        _ = await LoadAsync(forceRefresh: false, CancellationToken.None);
    }

    public void InvalidateLoadedDataAfterExternalImport()
    {
        _hasLoadedSuccessfully = false;
    }

    public void ResetTransientState()
    {
        ViewInstrumentCommand.Cancel();
        EditInstrumentCommand.Cancel();

        ResetCreateForm();
        IsCreateFormVisible = false;

        EditSymbol = string.Empty;
        EditDisplayName = string.Empty;
        EditSelectedAssetClass = AssetClass.Futures;
        EditExchange = string.Empty;
        EditCurrency = string.Empty;
        EditTickSizeText = string.Empty;
        EditTickValueText = string.Empty;
        ResetEditValidation();
        IsEditFormVisible = false;

        SelectedInstrument = null;
        LifecycleErrorMessage = null;
        ActionErrorMessage = null;
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        _ = await LoadAsync(forceRefresh: true, cancellationToken);
    }

    private bool CanRefresh() => !IsAnyOperationInProgress;

    private void ShowCreateForm()
    {
        CloseInstrumentDetails();
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

    private bool CanCreateInstrument() =>
        IsCreateFormVisible &&
        !IsAnyOperationInProgress;

    private async Task CreateInstrumentAsync(CancellationToken cancellationToken)
    {
        CreateErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Symbol))
        {
            IsSymbolInvalid = true;
            CreateErrorMessage = "Symbol is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            IsDisplayNameInvalid = true;
            CreateErrorMessage = "Display name is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Currency))
        {
            IsCurrencyInvalid = true;
            CreateErrorMessage = "Currency is required.";
            return;
        }

        if (!TryParsePositiveDecimal(TickSizeText, out decimal tickSize))
        {
            IsTickSizeInvalid = true;
            CreateErrorMessage = "Tick size must be greater than zero.";
            return;
        }

        if (!TryParsePositiveDecimal(TickValueText, out decimal tickValue))
        {
            IsTickValueInvalid = true;
            CreateErrorMessage = "Tick value must be greater than zero.";
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

    private static bool TryParsePositiveDecimal(string text, out decimal value) =>
        TryParseDecimal(text, out value) && value > 0m;

    private void ResetCreateForm()
    {
        Symbol = string.Empty;
        DisplayName = string.Empty;
        SelectedAssetClass = AssetClass.Futures;
        Exchange = string.Empty;
        Currency = string.Empty;
        TickSizeText = string.Empty;
        TickValueText = string.Empty;
        IsSymbolInvalid = false;
        IsDisplayNameInvalid = false;
        IsCurrencyInvalid = false;
        IsTickSizeInvalid = false;
        IsTickValueInvalid = false;
        CreateErrorMessage = null;
    }

    private bool CanUseInstrumentAction(Guid instrumentId) =>
        instrumentId != Guid.Empty &&
        !IsAnyOperationInProgress &&
        !IsCreateFormVisible &&
        !IsEditFormVisible;

    private async Task ViewInstrumentAsync(
        Guid instrumentId,
        CancellationToken cancellationToken)
    {
        _ = await LoadInstrumentDetailsAsync(instrumentId, cancellationToken);
    }

    private async Task EditInstrumentAsync(
        Guid instrumentId,
        CancellationToken cancellationToken)
    {
        InstrumentDetails? instrument = await LoadInstrumentDetailsAsync(
            instrumentId,
            cancellationToken);
        if (instrument is null)
        {
            return;
        }

        PopulateEditForm(instrument);
        IsEditFormVisible = true;
    }

    private async Task<InstrumentDetails?> LoadInstrumentDetailsAsync(
        Guid instrumentId,
        CancellationToken cancellationToken)
    {
        ActionErrorMessage = null;
        IsLoadingInstrumentDetails = true;
        try
        {
            InstrumentDetails? instrument =
                await _getInstrumentDetailsUseCase.ExecuteAsync(
                    instrumentId,
                    cancellationToken);
            if (instrument is null)
            {
                if (SelectedInstrument?.Id == instrumentId)
                {
                    SelectedInstrument = null;
                }

                ActionErrorMessage = DetailNotFoundMessage;
                _ = await LoadAsync(forceRefresh: true, cancellationToken);
                return null;
            }

            SelectedInstrument = instrument;
            return instrument;
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
            IsLoadingInstrumentDetails = false;
        }
    }

    private void PopulateEditForm(InstrumentDetails instrument)
    {
        EditSymbol = instrument.Symbol;
        EditDisplayName = instrument.DisplayName;
        EditSelectedAssetClass = instrument.AssetClass;
        EditExchange = instrument.Exchange ?? string.Empty;
        EditCurrency = instrument.Currency;
        EditTickSizeText = instrument.TickSize.ToString(CultureInfo.CurrentCulture);
        EditTickValueText = instrument.TickValue.ToString(CultureInfo.CurrentCulture);
        ResetEditValidation();
    }

    private void CloseInstrumentDetails()
    {
        if (IsEditFormVisible)
        {
            return;
        }

        SelectedInstrument = null;
        ActionErrorMessage = null;
    }

    private bool CanCloseInstrumentDetails() =>
        SelectedInstrument is not null &&
        !IsAnyOperationInProgress &&
        !IsEditFormVisible;

    private void CancelEdit()
    {
        ResetEditValidation();
        IsEditFormVisible = false;
    }

    private bool CanCancelEdit() => IsEditFormVisible && !IsUpdating;

    private bool CanSaveChanges() =>
        IsEditFormVisible &&
        SelectedInstrument is not null &&
        !IsAnyOperationInProgress;

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        EditErrorMessage = null;
        if (SelectedInstrument is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(EditSymbol))
        {
            IsEditSymbolInvalid = true;
            EditErrorMessage = "Symbol is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(EditDisplayName))
        {
            IsEditDisplayNameInvalid = true;
            EditErrorMessage = "Display name is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(EditCurrency))
        {
            IsEditCurrencyInvalid = true;
            EditErrorMessage = "Currency is required.";
            return;
        }

        if (!TryParsePositiveDecimal(EditTickSizeText, out decimal tickSize))
        {
            IsEditTickSizeInvalid = true;
            EditErrorMessage = "Tick size must be greater than zero.";
            return;
        }

        if (!TryParsePositiveDecimal(EditTickValueText, out decimal tickValue))
        {
            IsEditTickValueInvalid = true;
            EditErrorMessage = "Tick value must be greater than zero.";
            return;
        }

        IsUpdating = true;
        try
        {
            UpdateInstrumentResult result = await _updateInstrumentUseCase.ExecuteAsync(
                new UpdateInstrumentCommand(
                    SelectedInstrument.Id,
                    EditSymbol,
                    EditDisplayName,
                    EditSelectedAssetClass,
                    EditExchange,
                    EditCurrency,
                    tickSize,
                    tickValue),
                cancellationToken);

            SelectedInstrument = result.Instrument;
            ReplaceListItem(result.Instrument);
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
            SelectedInstrument = null;
            ActionErrorMessage = DetailNotFoundMessage;
            _ = await LoadAsync(forceRefresh: true, cancellationToken);
        }
        catch (InstrumentAssetClassChangeBlockedException)
        {
            EditErrorMessage = AssetClassChangeBlockedMessage;
        }
        catch (ArgumentException)
        {
            EditErrorMessage = InvalidInstrumentDetailsMessage;
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

    private async Task DeleteInstrumentAsync(
        Guid instrumentId,
        CancellationToken cancellationToken)
    {
        InstrumentListItem? listItem = Instruments.FirstOrDefault(item => item.Id == instrumentId);
        string symbol = listItem?.Symbol ?? SelectedInstrument?.Symbol ?? "this instrument";
        bool confirmed = _dialogService.Confirm(new ConfirmationDialogRequest(
            title: "Delete instrument?",
            message: $"Delete \"{symbol}\"? This action cannot be undone.",
            confirmButtonText: "Delete Instrument",
            isDestructive: true));
        if (!confirmed)
        {
            return;
        }

        ActionErrorMessage = null;
        IsDeleting = true;
        try
        {
            DeleteInstrumentResult result = await _deleteInstrumentUseCase.ExecuteAsync(
                instrumentId,
                cancellationToken);
            if (result == DeleteInstrumentResult.Referenced)
            {
                _dialogService.ShowInformation(new InformationDialogRequest(
                    DeleteBlockedTitle,
                    DeleteBlockedMessage));
                return;
            }

            Instruments = Instruments.Where(item => item.Id != instrumentId).ToArray();
            if (SelectedInstrument?.Id == instrumentId)
            {
                IsEditFormVisible = false;
                SelectedInstrument = null;
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
            Instruments = Instruments.Where(item => item.Id != instrumentId).ToArray();
            if (SelectedInstrument?.Id == instrumentId)
            {
                IsEditFormVisible = false;
                SelectedInstrument = null;
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

    private void ReplaceListItem(InstrumentDetails instrument)
    {
        var replacement = new InstrumentListItem(
            instrument.Id,
            instrument.Symbol,
            instrument.DisplayName,
            instrument.AssetClass,
            instrument.Exchange,
            instrument.Currency,
            instrument.TickSize,
            instrument.TickValue,
            instrument.PointValue,
            instrument.IsActive);
        Instruments = Instruments
            .Select(item => item.Id == instrument.Id ? replacement : item)
            .ToArray();
    }

    private void ResetEditValidation()
    {
        IsEditSymbolInvalid = false;
        IsEditDisplayNameInvalid = false;
        IsEditCurrencyInvalid = false;
        IsEditTickSizeInvalid = false;
        IsEditTickValueInvalid = false;
        EditErrorMessage = null;
    }

    private bool IsAnyOperationInProgress =>
        IsLoading ||
        IsCreating ||
        IsChangingInstrumentState ||
        IsLoadingInstrumentDetails ||
        IsUpdating ||
        IsDeleting;

    private bool CanActivateInstrument(InstrumentListItem? instrument) =>
        instrument is { IsActive: false } && CanChangeInstrumentState();

    private bool CanDeactivateInstrument(InstrumentListItem? instrument) =>
        instrument is { IsActive: true } && CanChangeInstrumentState();

    private bool CanChangeInstrumentState() =>
        !IsAnyOperationInProgress &&
        !IsCreateFormVisible &&
        !IsEditFormVisible;

    private async Task ActivateInstrumentAsync(
        InstrumentListItem? instrument,
        CancellationToken cancellationToken)
    {
        if (instrument is null)
        {
            return;
        }

        LifecycleErrorMessage = null;
        IsChangingInstrumentState = true;

        try
        {
            await _instrumentLifecycleUseCase.ActivateAsync(
                instrument.Id,
                cancellationToken);
            bool wasReloaded = await LoadAsync(forceRefresh: true, cancellationToken);

            if (!wasReloaded)
            {
                ErrorMessage = LifecycleReloadErrorMessage;
            }

            if (SelectedInstrument?.Id == instrument.Id)
            {
                _ = await LoadInstrumentDetailsAsync(instrument.Id, cancellationToken);
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
            IsChangingInstrumentState = false;
        }
    }

    private async Task DeactivateInstrumentAsync(
        InstrumentListItem? instrument,
        CancellationToken cancellationToken)
    {
        if (instrument is null)
        {
            return;
        }

        LifecycleErrorMessage = null;
        IsChangingInstrumentState = true;

        try
        {
            await _instrumentLifecycleUseCase.DeactivateAsync(
                instrument.Id,
                cancellationToken);
            bool wasReloaded = await LoadAsync(forceRefresh: true, cancellationToken);

            if (!wasReloaded)
            {
                ErrorMessage = LifecycleReloadErrorMessage;
            }

            if (SelectedInstrument?.Id == instrument.Id)
            {
                _ = await LoadInstrumentDetailsAsync(instrument.Id, cancellationToken);
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
            IsChangingInstrumentState = false;
        }
    }

    private void NotifyOperationCanExecuteChanged()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        ShowCreateFormCommand.NotifyCanExecuteChanged();
        CreateInstrumentCommand.NotifyCanExecuteChanged();
        ActivateInstrumentCommand.NotifyCanExecuteChanged();
        DeactivateInstrumentCommand.NotifyCanExecuteChanged();
        ViewInstrumentCommand.NotifyCanExecuteChanged();
        EditInstrumentCommand.NotifyCanExecuteChanged();
        CloseInstrumentDetailsCommand.NotifyCanExecuteChanged();
        CancelEditCommand.NotifyCanExecuteChanged();
        SaveChangesCommand.NotifyCanExecuteChanged();
        DeleteInstrumentCommand.NotifyCanExecuteChanged();
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
                IReadOnlyList<InstrumentListItem> instruments =
                    await _instrumentReader.GetAllAsync(cancellationToken);

                Instruments = instruments;
                if (SelectedInstrument is not null &&
                    instruments.All(item => item.Id != SelectedInstrument.Id))
                {
                    IsEditFormVisible = false;
                    SelectedInstrument = null;
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
