using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Imports.Topstep;
using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.Theming;
using PersonalTradingJournal.Desktop.ViewModels.Accounts;
using PersonalTradingJournal.Desktop.ViewModels.Common;
using PersonalTradingJournal.Desktop.ViewModels.Dashboard;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;
using PersonalTradingJournal.Desktop.ViewModels.Instruments;
using PersonalTradingJournal.Desktop.ViewModels.Import;
using PersonalTradingJournal.Desktop.ViewModels.Journals;
using PersonalTradingJournal.Desktop.ViewModels.Mistakes;
using PersonalTradingJournal.Desktop.ViewModels.Setups;
using PersonalTradingJournal.Desktop.ViewModels.Settings;
using PersonalTradingJournal.Desktop.ViewModels.Trades;

namespace PersonalTradingJournal.Desktop.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly AccountsViewModel _accountsViewModel;
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly CalendarViewModel _calendarViewModel;
    private readonly JournalViewModel _journalViewModel;
    private readonly PersonalTradingJournal.Desktop.ViewModels.DailyReview.DailyReviewViewModel? _dailyReviewViewModel;
    private readonly InstrumentsViewModel _instrumentsViewModel;
    private readonly ImportViewModel _importViewModel;
    private readonly TradingMistakesViewModel _tradingMistakesViewModel;
    private readonly TradingSetupsViewModel _tradingSetupsViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly IThemeService _themeService;
    private readonly TradesViewModel _tradesViewModel;
    private readonly TopstepImportChangeTracker? _topstepChanges;
    private long _observedTopstepTradesVersion;
    private long _observedTopstepInstrumentsVersion;
    private bool _disposed;
    private readonly System.Windows.Threading.Dispatcher? _dispatcher =
        SynchronizationContext.Current is System.Windows.Threading.DispatcherSynchronizationContext
            ? System.Windows.Threading.Dispatcher.CurrentDispatcher : null;
    private NavigationDestination _currentDestination = NavigationDestination.Dashboard;
    private ObservableObject _currentContentViewModel;

    public MainWindowViewModel(
        DashboardViewModel dashboardViewModel,
        CalendarViewModel calendarViewModel,
        JournalViewModel journalViewModel,
        AccountsViewModel accountsViewModel,
        InstrumentsViewModel instrumentsViewModel,
        ImportViewModel importViewModel,
        TradingMistakesViewModel tradingMistakesViewModel,
        TradingSetupsViewModel tradingSetupsViewModel,
        TradesViewModel tradesViewModel,
        SettingsViewModel settingsViewModel,
        IThemeService themeService,
        TopstepImportChangeTracker? topstepChanges = null,
        PersonalTradingJournal.Desktop.ViewModels.DailyReview.DailyReviewViewModel? dailyReviewViewModel = null)
    {
        ArgumentNullException.ThrowIfNull(dashboardViewModel);
        ArgumentNullException.ThrowIfNull(calendarViewModel);
        ArgumentNullException.ThrowIfNull(journalViewModel);
        ArgumentNullException.ThrowIfNull(accountsViewModel);
        ArgumentNullException.ThrowIfNull(instrumentsViewModel);
        ArgumentNullException.ThrowIfNull(importViewModel);
        ArgumentNullException.ThrowIfNull(tradingMistakesViewModel);
        ArgumentNullException.ThrowIfNull(tradingSetupsViewModel);
        ArgumentNullException.ThrowIfNull(tradesViewModel);
        ArgumentNullException.ThrowIfNull(settingsViewModel);
        ArgumentNullException.ThrowIfNull(themeService);

        _dashboardViewModel = dashboardViewModel;
        _calendarViewModel = calendarViewModel;
        _journalViewModel = journalViewModel;
        _dailyReviewViewModel = dailyReviewViewModel;
        _accountsViewModel = accountsViewModel;
        _instrumentsViewModel = instrumentsViewModel;
        _importViewModel = importViewModel;
        _tradingMistakesViewModel = tradingMistakesViewModel;
        _tradingSetupsViewModel = tradingSetupsViewModel;
        _tradesViewModel = tradesViewModel;
        _settingsViewModel = settingsViewModel;
        _themeService = themeService;
        _topstepChanges = topstepChanges;
        _currentContentViewModel = dashboardViewModel;
        NavigateCommand = new RelayCommand<NavigationDestination>(Navigate);
        ToggleThemeCommand = new AsyncRelayCommand(ToggleThemeAsync);
        TopNavigationItems =
        [
            CreateNavigationItem("Dashboard", NavigationDestination.Dashboard, "PtjIconDashboard"),
            CreateNavigationItem("Notebook", NavigationDestination.Notebook, "PtjIconNotebook"),
        ];
        NavigationSections =
        [
            new NavigationSectionViewModel(
                "TRADING",
                [
                    CreateNavigationItem("Trades", NavigationDestination.Trades, "PtjIconTrades"),
                    CreateNavigationItem("Journal", NavigationDestination.Journal, "PtjIconJournal"),
                    CreateNavigationItem("Calendar", NavigationDestination.Calendar, "PtjIconCalendar"),
                    CreateNavigationItem("Import", NavigationDestination.Import, "PtjIconImport"),
                ]),
            new NavigationSectionViewModel(
                "ANALYSIS",
                [
                    CreateNavigationItem("Performance", NavigationDestination.Performance, "PtjIconPerformance"),
                    CreateNavigationItem("Trading Setups", NavigationDestination.Setups, "PtjIconSetups"),
                    CreateNavigationItem("Mistakes", NavigationDestination.Mistakes, "PtjIconMistakes"),
                    CreateNavigationItem("Breakdown", NavigationDestination.Breakdown, "PtjIconBreakdown"),
                ]),
            new NavigationSectionViewModel(
                "PLANNING",
                [
                    CreateNavigationItem("Playbook", NavigationDestination.Playbook, "PtjIconPlaybook"),
                    CreateNavigationItem("Trading Plan", NavigationDestination.TradingPlan, "PtjIconTradingPlan"),
                    CreateNavigationItem("Rules", NavigationDestination.Rules, "PtjIconRules"),
                ]),
            new NavigationSectionViewModel(
                "REVIEW",
                [
                    CreateNavigationItem("Daily Review", NavigationDestination.DailyReview, "PtjIconDailyReview"),
                    CreateNavigationItem("Weekly Review", NavigationDestination.WeeklyReview, "PtjIconWeeklyReview"),
                    CreateNavigationItem("Monthly Review", NavigationDestination.MonthlyReview, "PtjIconMonthlyReview"),
                ]),
        ];
        BottomNavigationItems =
        [
            CreateNavigationItem("Accounts", NavigationDestination.Accounts, "PtjIconAccounts"),
            CreateNavigationItem("Instruments", NavigationDestination.Instruments, "PtjIconInstruments"),
            CreateNavigationItem("Settings", NavigationDestination.Settings, "PtjIconSettings"),
        ];
        AllNavigationItems =
        [
            .. TopNavigationItems,
            .. NavigationSections.SelectMany(section => section.Items),
            .. BottomNavigationItems,
        ];
        UpdateNavigationSelection(CurrentDestination);
        _themeService.ThemeChanged += OnThemeChanged;
        _importViewModel.ImportCommitted += OnImportCommitted;
        _importViewModel.TopstepImportCommitted += OnTopstepImportCommitted;
        _tradesViewModel.TradeDataCommitted += OnTradeDataCommitted;
        _accountsViewModel.TradeDataCommitted += OnAccountTradesDeleted;
        _dashboardViewModel.OpenTradeAsync = OpenReadOnlyTradeAsync;
        _calendarViewModel.TradeDataCommitted += OnCalendarTradeCommitted;
        _calendarViewModel.OpenJournalAsync = OpenCalendarJournalAsync;
        _journalViewModel.JournalDataCommitted += OnJournalDataCommitted;
        if (_dailyReviewViewModel is not null)
        {
            _dailyReviewViewModel.OpenTradeAsync = OpenReviewTradeAsync;
            _dailyReviewViewModel.OpenJournalAsync = OpenReviewJournalAsync;
            _dailyReviewViewModel.OpenAiSettings = () => Navigate(NavigationDestination.Settings);
        }
    }

    public string ApplicationTitle => "Personal Trading Journal";

    public NavigationDestination CurrentDestination
    {
        get => _currentDestination;
        private set
        {
            if (SetProperty(ref _currentDestination, value))
            {
                OnPropertyChanged(nameof(PageTitle));
                OnPropertyChanged(nameof(ContentPlaceholder));
            }
        }
    }

    public string PageTitle => CurrentDestination switch
    {
        NavigationDestination.Dashboard => "Dashboard",
        NavigationDestination.Notebook => "Notebook",
        NavigationDestination.Trades => "Trades",
        NavigationDestination.Journal => "Journal",
        NavigationDestination.Calendar => "Calendar",
        NavigationDestination.Import => "Import",
        NavigationDestination.Performance => "Performance",
        NavigationDestination.Setups => "Trading Setups",
        NavigationDestination.Mistakes => "Trading Mistakes",
        NavigationDestination.Breakdown => "Breakdown",
        NavigationDestination.Playbook => "Playbook",
        NavigationDestination.TradingPlan => "Trading Plan",
        NavigationDestination.Rules => "Rules",
        NavigationDestination.DailyReview => "Daily Review",
        NavigationDestination.WeeklyReview => "Weekly Review",
        NavigationDestination.MonthlyReview => "Monthly Review",
        NavigationDestination.Accounts => "Accounts",
        NavigationDestination.Instruments => "Instruments",
        NavigationDestination.Settings => "Settings",
        _ => throw new InvalidOperationException(
            $"Unsupported navigation destination: {CurrentDestination}.")
    };

    public string ContentPlaceholder => $"{PageTitle} content will appear here.";

    public ObservableObject CurrentContentViewModel
    {
        get => _currentContentViewModel;
        private set => SetProperty(ref _currentContentViewModel, value);
    }

    public IRelayCommand<NavigationDestination> NavigateCommand { get; }

    public IReadOnlyList<NavigationItemViewModel> TopNavigationItems { get; }

    public IReadOnlyList<NavigationSectionViewModel> NavigationSections { get; }

    public IReadOnlyList<NavigationItemViewModel> BottomNavigationItems { get; }

    public IReadOnlyList<NavigationItemViewModel> AllNavigationItems { get; }

    public bool IsLightTheme => _themeService.EffectiveTheme == AppTheme.Light;

    public string ThemeToggleToolTip => IsLightTheme
        ? "Switch to dark theme"
        : "Switch to light theme";

    public IAsyncRelayCommand ToggleThemeCommand { get; }

    public bool TryCloseWindow()
    {
        if (CurrentDestination == NavigationDestination.DailyReview)
        {
            _dailyReviewViewModel?.Deactivate();
            return true;
        }
        if (CurrentDestination == NavigationDestination.Calendar)
            return _calendarViewModel.TryCloseDayDialog();

        if (CurrentDestination != NavigationDestination.Journal) return true;
        if (!_journalViewModel.TryLeave()) return false;

        _journalViewModel.Deactivate();
        return true;
    }

    public void Dispose()
    {
        _disposed = true;
        _themeService.ThemeChanged -= OnThemeChanged;
        _importViewModel.ImportCommitted -= OnImportCommitted;
        _importViewModel.TopstepImportCommitted -= OnTopstepImportCommitted;
        _tradesViewModel.TradeDataCommitted -= OnTradeDataCommitted;
        _accountsViewModel.TradeDataCommitted -= OnAccountTradesDeleted;
        _dashboardViewModel.OpenTradeAsync = null;
        _calendarViewModel.TradeDataCommitted -= OnCalendarTradeCommitted;
        _calendarViewModel.OpenJournalAsync = null;
        _journalViewModel.JournalDataCommitted -= OnJournalDataCommitted;
        _dashboardViewModel.Deactivate();
        _calendarViewModel.Deactivate();
        _journalViewModel.Deactivate();
        _dailyReviewViewModel?.Deactivate();
        if (_dailyReviewViewModel is not null)
        {
            _dailyReviewViewModel.OpenTradeAsync = null;
            _dailyReviewViewModel.OpenJournalAsync = null;
            _dailyReviewViewModel.OpenAiSettings = null;
        }
    }

    private (bool Trades, bool Instruments) InvalidateTopstepChanges()
    {
        bool tradesChanged = false, instrumentsChanged = false;
        if (_topstepChanges is not null)
        {
            long tradesVersion = _topstepChanges.TradesVersion;
            long instrumentsVersion = _topstepChanges.InstrumentsVersion;
            if (tradesVersion != _observedTopstepTradesVersion)
            {
                _tradesViewModel.InvalidateLoadedDataAfterExternalImport();
                _observedTopstepTradesVersion = tradesVersion;
                tradesChanged = true;
            }
            if (instrumentsVersion != _observedTopstepInstrumentsVersion)
            {
                _instrumentsViewModel.InvalidateLoadedDataAfterExternalImport();
                _observedTopstepInstrumentsVersion = instrumentsVersion;
                instrumentsChanged = true;
            }
        }
        return (tradesChanged, instrumentsChanged);
    }

    private void OnTopstepImportCommitted(object? sender, EventArgs e)
    {
        // Database work only advances generations; Desktop dispatches after the use case returns.
        if (_dispatcher is not null && !_dispatcher.CheckAccess())
        {
            _ = _dispatcher.BeginInvoke(RefreshActiveTopstepDestination);
            return;
        }
        RefreshActiveTopstepDestination();
    }

    private void RefreshActiveTopstepDestination()
    {
        if (_disposed) return;
        var changed = InvalidateTopstepChanges();
        // Consume each generation once, including when navigation already observed the commit.
        if (changed.Trades) OnTradeDataCommitted(this, EventArgs.Empty);
        // Load methods handle errors and reread an invalidated in-flight result under their gates.
        if (changed.Trades && CurrentDestination == NavigationDestination.Trades)
            _ = _tradesViewModel.EnsureLoadedAsync();
        if (changed.Instruments && CurrentDestination == NavigationDestination.Instruments)
            _ = _instrumentsViewModel.EnsureLoadedAsync();
    }

    private Task _tradeNavigationLoad = Task.CompletedTask;

    private async Task OpenReviewTradeAsync(Guid id)
    {
        if (_disposed) return;
        Navigate(NavigationDestination.Trades);
        await _tradeNavigationLoad;
        if (!_disposed && CurrentDestination == NavigationDestination.Trades)
            await _tradesViewModel.ShowTradeByIdAsync(id);
    }

    private async Task OpenReviewJournalAsync(PersonalTradingJournal.Application.DailyReview.DailyReviewJournalEvidence journal)
    {
        if (_disposed || !_journalViewModel.TryOpenScope(journal.TradingDate, journal.TradingAccountId,
            journal.AccountName ?? "Unavailable account")) return;
        Navigate(NavigationDestination.Journal);
        if (!_disposed && CurrentDestination == NavigationDestination.Journal)
            await _journalViewModel.LoadTask;
    }

    private async Task OpenCalendarJournalAsync(DateOnly date, CalendarAccountOption account)
    {
        if (_disposed || !_journalViewModel.TryOpenScope(date, account.Id, account.Name)) return;
        Navigate(NavigationDestination.Journal);
        if (!_disposed && CurrentDestination == NavigationDestination.Journal)
        {
            await _journalViewModel.LoadTask;
            // Calendar Add/Continue is an explicit request to edit, unlike history browsing.
            if (!_disposed && CurrentDestination == NavigationDestination.Journal &&
                _journalViewModel.SelectedDate == date.ToDateTime(TimeOnly.MinValue) &&
                _journalViewModel.SelectedAccount.Id == account.Id)
                _journalViewModel.OpenEditorCommand.Execute(null);
        }
    }

    private async Task OpenReadOnlyTradeAsync(PersonalTradingJournal.Application.Trades.TradeListItem trade)
    {
        Navigate(NavigationDestination.Trades);
        await _tradeNavigationLoad;
        if (!_disposed && CurrentDestination == NavigationDestination.Trades && _tradesViewModel.ShowTradeDetailCommand.CanExecute(trade))
            await _tradesViewModel.ShowTradeDetailCommand.ExecuteAsync(trade);
    }

    private void Navigate(NavigationDestination destination)
    {
        if (destination != CurrentDestination && CurrentDestination == NavigationDestination.Calendar &&
            !_calendarViewModel.TryCloseInlineJournal()) return;
        if (destination != CurrentDestination &&
            CurrentDestination == NavigationDestination.Journal &&
            !_journalViewModel.TryLeave())
            return;

        var changed = InvalidateTopstepChanges();
        if (changed.Trades) OnTradeDataCommitted(this, EventArgs.Empty);
        ExpandContainingSection(destination);

        if (destination == CurrentDestination)
        {
            if (changed.Trades && destination == NavigationDestination.Trades)
                _ = _tradesViewModel.EnsureLoadedAsync();
            if (changed.Instruments && destination == NavigationDestination.Instruments)
                _ = _instrumentsViewModel.EnsureLoadedAsync();
            return;
        }

        if (CurrentDestination == NavigationDestination.Dashboard) _dashboardViewModel.Deactivate();
        if (CurrentDestination == NavigationDestination.Calendar) _calendarViewModel.Deactivate();
        if (CurrentDestination == NavigationDestination.Journal) _journalViewModel.Deactivate(resetOnNextActivation: true);
        if (CurrentDestination == NavigationDestination.DailyReview) _dailyReviewViewModel?.Deactivate();
        CurrentDestination = destination;
        UpdateNavigationSelection(destination);
        CurrentContentViewModel = destination switch
        {
            NavigationDestination.Dashboard => _dashboardViewModel,
            NavigationDestination.Calendar => _calendarViewModel,
            NavigationDestination.Journal => _journalViewModel,
            NavigationDestination.DailyReview when _dailyReviewViewModel is not null => _dailyReviewViewModel,
            NavigationDestination.Accounts => _accountsViewModel,
            NavigationDestination.Instruments => _instrumentsViewModel,
            NavigationDestination.Import => _importViewModel,
            NavigationDestination.Mistakes => _tradingMistakesViewModel,
            NavigationDestination.Setups => _tradingSetupsViewModel,
            NavigationDestination.Trades => _tradesViewModel,
            NavigationDestination.Settings => _settingsViewModel,
            _ => new PlaceholderViewModel(ContentPlaceholder),
        };

        if (destination == NavigationDestination.Calendar)
            _ = _calendarViewModel.ActivateAsync();

        if (destination == NavigationDestination.Journal)
            _ = _journalViewModel.ActivateAsync();

        if (destination == NavigationDestination.DailyReview && _dailyReviewViewModel is not null)
            _ = _dailyReviewViewModel.ActivateAsync();

        if (destination == NavigationDestination.Accounts)
        {
            _accountsViewModel.ResetTransientState();
            _ = _accountsViewModel.EnsureLoadedAsync();
        }

        if (destination == NavigationDestination.Instruments)
        {
            _instrumentsViewModel.ResetTransientState();
            _ = _instrumentsViewModel.EnsureLoadedAsync();
        }

        if (destination == NavigationDestination.Import)
        {
            _importViewModel.ResetTransientState();
            _ = _importViewModel.EnsureLoadedAsync();
        }

        if (destination == NavigationDestination.Mistakes)
        {
            _tradingMistakesViewModel.ResetTransientState();
            _ = _tradingMistakesViewModel.EnsureLoadedAsync();
        }

        if (destination == NavigationDestination.Setups)
        {
            _tradingSetupsViewModel.ResetTransientState();
            _ = _tradingSetupsViewModel.EnsureLoadedAsync();
        }

        if (destination == NavigationDestination.Trades)
        {
            _tradesViewModel.ResetTransientState();
            _tradeNavigationLoad = _tradesViewModel.EnsureLoadedAsync();
        }
    }

    private static NavigationItemViewModel CreateNavigationItem(
        string title,
        NavigationDestination destination,
        string iconKey)
    {
        return new NavigationItemViewModel(title, destination, iconKey);
    }

    private void ExpandContainingSection(NavigationDestination destination)
    {
        NavigationSections.FirstOrDefault(section => section.Contains(destination))?.Expand();
    }

    private void UpdateNavigationSelection(NavigationDestination destination)
    {
        foreach (NavigationItemViewModel item in AllNavigationItems)
        {
            item.SetSelected(item.Destination == destination);
        }
    }

    private async Task ToggleThemeAsync()
    {
        AppTheme explicitTheme = _themeService.EffectiveTheme == AppTheme.Dark
            ? AppTheme.Light
            : AppTheme.Dark;
        await _settingsViewModel.ChangeThemeCommand.ExecuteAsync(explicitTheme);
    }

    private void OnThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsLightTheme));
        OnPropertyChanged(nameof(ThemeToggleToolTip));
    }

    private void OnImportCommitted(object? sender, ImportCommittedEventArgs e)
    {
        OnTradeDataCommitted(sender, EventArgs.Empty);
        _tradesViewModel.InvalidateLoadedDataAfterExternalImport();
        if (e.CreatedInstrumentCount > 0)
        {
            _instrumentsViewModel.InvalidateLoadedDataAfterExternalImport();
        }
    }

    private void OnCalendarTradeCommitted(object? sender, EventArgs e)
    {
        if (_disposed) return;
        if (_dispatcher is not null && !_dispatcher.CheckAccess())
        {
            _ = _dispatcher.BeginInvoke(() => OnCalendarTradeCommitted(sender, e));
            return;
        }
        _tradesViewModel.InvalidateLoadedDataAfterExternalImport();
        _dashboardViewModel.OnDataCommitted();
        _journalViewModel.TradeContext.OnDataCommitted();
        // Calendar's dedicated editor refreshes the modal after its save/reload completes.
        _dailyReviewViewModel?.OnDataCommitted();
    }

    private void OnAccountTradesDeleted(object? sender, EventArgs e)
    {
        _tradesViewModel.InvalidateLoadedDataAfterExternalImport();
        OnTradeDataCommitted(sender, e);
    }

    private void OnTradeDataCommitted(object? sender, EventArgs e)
    {
        if (_disposed) return;
        if (_dispatcher is not null && !_dispatcher.CheckAccess())
        {
            _ = _dispatcher.BeginInvoke(() => OnTradeDataCommitted(sender, e));
            return;
        }
        _dashboardViewModel.OnDataCommitted();
        _calendarViewModel.OnDataCommitted();
        _journalViewModel.TradeContext.OnDataCommitted();
        _dailyReviewViewModel?.OnDataCommitted();
    }

    private void OnJournalDataCommitted(object? sender, EventArgs e)
    {
        if (_disposed) return;
        if (_dispatcher is not null && !_dispatcher.CheckAccess())
        {
            _ = _dispatcher.BeginInvoke(() => OnJournalDataCommitted(sender, e));
            return;
        }
        _calendarViewModel.OnJournalCommitted();
        _dailyReviewViewModel?.OnDataCommitted();
    }
}
