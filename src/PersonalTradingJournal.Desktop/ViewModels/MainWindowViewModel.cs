using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Imports.Topstep;
using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.Theming;
using PersonalTradingJournal.Desktop.ViewModels.Accounts;
using PersonalTradingJournal.Desktop.ViewModels.Common;
using PersonalTradingJournal.Desktop.ViewModels.Dashboard;
using PersonalTradingJournal.Desktop.ViewModels.Breakdown;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;
using PersonalTradingJournal.Desktop.ViewModels.Instruments;
using PersonalTradingJournal.Desktop.ViewModels.Import;
using PersonalTradingJournal.Desktop.ViewModels.Journals;
using PersonalTradingJournal.Desktop.ViewModels.Mistakes;
using PersonalTradingJournal.Desktop.ViewModels.Setups;
using PersonalTradingJournal.Desktop.ViewModels.Rules;
using PersonalTradingJournal.Desktop.ViewModels.Settings;
using PersonalTradingJournal.Desktop.ViewModels.Trades;
using PersonalTradingJournal.Desktop.ViewModels.TradingPlans;

namespace PersonalTradingJournal.Desktop.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly AccountsViewModel _accountsViewModel;
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly BreakdownViewModel? _breakdownViewModel;
    private readonly CalendarViewModel _calendarViewModel;
    private readonly JournalViewModel _journalViewModel;
    private readonly PersonalTradingJournal.Desktop.ViewModels.DailyReview.DailyReviewViewModel? _dailyReviewViewModel;
    private readonly InstrumentsViewModel _instrumentsViewModel;
    private readonly ImportViewModel _importViewModel;
    private readonly TradingMistakesViewModel _tradingMistakesViewModel;
    private readonly TradingSetupsViewModel _tradingSetupsViewModel;
    private readonly PlaybookViewModel? _playbookViewModel;
    private readonly RulesViewModel? _rulesViewModel;
    private readonly TradingPlanWorkspaceViewModel? _tradingPlan;
    private readonly PersonalTradingJournal.Desktop.ViewModels.Notebook.NotebookWorkspaceViewModel? _notebook;
    private bool _planNavigation;
    public Task PlanNavigationTask { get; private set; } = Task.CompletedTask;
    private readonly PersonalTradingJournal.Desktop.ViewModels.PeriodReviews.PeriodReviewWorkspaces? _periodReviews;
    private PersonalTradingJournal.Desktop.ViewModels.PeriodReviews.PeriodReviewViewModel? ActivePeriod => CurrentDestination switch
    {
        NavigationDestination.WeeklyReview => _periodReviews?.Weekly,
        NavigationDestination.MonthlyReview => _periodReviews?.Monthly,
        _ => null,
    };
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
        PersonalTradingJournal.Desktop.ViewModels.DailyReview.DailyReviewViewModel? dailyReviewViewModel = null,
        PlaybookViewModel? playbookViewModel = null,
        RulesViewModel? rulesViewModel = null,
        PersonalTradingJournal.Desktop.ViewModels.PeriodReviews.PeriodReviewWorkspaces? periodReviews = null,
        BreakdownViewModel? breakdownViewModel = null,
        TradingPlanWorkspaceViewModel? tradingPlan = null,
        PersonalTradingJournal.Desktop.ViewModels.Notebook.NotebookWorkspaceViewModel? notebook = null)
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
        _breakdownViewModel = breakdownViewModel;
        _calendarViewModel = calendarViewModel;
        _journalViewModel = journalViewModel;
        _dailyReviewViewModel = dailyReviewViewModel;
        _accountsViewModel = accountsViewModel;
        _instrumentsViewModel = instrumentsViewModel;
        _importViewModel = importViewModel;
        _tradingMistakesViewModel = tradingMistakesViewModel;
        _tradingSetupsViewModel = tradingSetupsViewModel;
        _playbookViewModel = playbookViewModel;
        _rulesViewModel = rulesViewModel;
        _tradingPlan = tradingPlan;
        _notebook = notebook;
        if (_notebook is not null) _notebook.OpenLinkedTarget = OpenNotebookTargetAsync;
        ReturnToNotebookCommand = new RelayCommand(() => Navigate(NavigationDestination.Notebook), () => CanReturnToNotebook);
        if (tradingPlan is not null)
        {
            tradingPlan.OpenPlaybook = OpenPlanPlaybookAsync;
            tradingPlan.OpenRule = OpenPlanRuleAsync;
            tradingPlan.OpenDailyReview = OpenPlanDailyReviewAsync;
        }
        _periodReviews = periodReviews;
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
        if (_breakdownViewModel is not null) _breakdownViewModel.OpenTradesAsync = OpenBreakdownTradesAsync;
        _tradesViewModel.ReturnToBreakdownAsync = ReturnToBreakdownAsync;
        _calendarViewModel.TradeDataCommitted += OnCalendarTradeCommitted;
        _calendarViewModel.OpenJournalAsync = OpenCalendarJournalAsync;
        _journalViewModel.JournalDataCommitted += OnJournalDataCommitted;
        foreach (var period in _periodReviews?.All ?? [])
        {
            period.OpenTradeAsync = OpenReviewTradeAsync;
            period.OpenPreparationAsync = OpenReviewPreparationAsync;
            period.OpenJournalAsync = OpenPeriodJournalAsync;
            period.OpenSettings = () => Navigate(NavigationDestination.Settings);
        }
        if (_dailyReviewViewModel is not null)
        {
            _dailyReviewViewModel.OpenTradeAsync = OpenReviewTradeAsync;
            _dailyReviewViewModel.OpenPreparationAsync = OpenReviewPreparationAsync;
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
        if (!CanCloseWindow()) return false;
        CompleteWindowClose();
        return true;
    }

    internal bool CanCloseWindow()
    {
        if (CurrentDestination == NavigationDestination.Notebook && _notebook?.TryLeave() == false) return false;
        if (_tradingPlan?.HasLeaveConsent == false) return false;
        if (CurrentDestination == NavigationDestination.Trades && !_tradesViewModel.TryLeaveBreakdownScope()) return false;
        if (_settingsViewModel.DataBackups?.TryLeave() == false) return false;
        if (ActivePeriod?.TryLeave() == false) return false;
        if (CurrentDestination == NavigationDestination.Playbook)
            return _playbookViewModel?.TryLeave() != false;
        if (CurrentDestination == NavigationDestination.Rules)
            return _rulesViewModel?.TryLeave() != false;
        if (CurrentDestination == NavigationDestination.DailyReview)
        {
            return _dailyReviewViewModel?.TryLeave() != false;
        }
        if (CurrentDestination == NavigationDestination.Calendar)
            return _calendarViewModel.CanCloseDayDialog();

        if (CurrentDestination != NavigationDestination.Journal) return true;
        return _journalViewModel.TryLeave();
    }

    internal void CompleteWindowClose()
    {
        _notebook?.CompleteLeave();
        _notebook?.Deactivate();
        _tradingPlan?.CompleteLeave();
        _tradesViewModel.DeactivateBrowse();
        _breakdownViewModel?.Deactivate();
        ActivePeriod?.Deactivate();
        if (CurrentDestination == NavigationDestination.Calendar) _calendarViewModel.CompleteDayDialogClose();
        if (CurrentDestination == NavigationDestination.Journal) _journalViewModel.Deactivate();
        if (CurrentDestination == NavigationDestination.DailyReview) _dailyReviewViewModel?.Deactivate();
    }

    public void Dispose()
    {
        if (_notebook is not null) _notebook.OpenLinkedTarget = null;
        _notebook?.Dispose();
        if (_tradingPlan is not null) { _tradingPlan.OpenDailyReview = null; _tradingPlan.ReturnToReview = null; }
        _tradingPlan?.Dispose();
        _tradesViewModel.DisposeBrowse(); _tradesViewModel.ReturnToBreakdownAsync = null;
        if (_breakdownViewModel is not null) _breakdownViewModel.OpenTradesAsync = null;
        _breakdownViewModel?.Dispose();
        foreach (var period in _periodReviews?.All ?? [])
        { period.Deactivate(); period.OpenTradeAsync = null; period.OpenJournalAsync = null; period.OpenSettings = null; period.OpenPreparationAsync = null; }
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
        _playbookViewModel?.Deactivate();
        _rulesViewModel?.Deactivate();
        if (_dailyReviewViewModel is not null)
        {
            _dailyReviewViewModel.OpenTradeAsync = null;
            _dailyReviewViewModel.OpenJournalAsync = null;
            _dailyReviewViewModel.OpenAiSettings = null;
            _dailyReviewViewModel.OpenPreparationAsync = null;
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
    private async Task OpenBreakdownTradesAsync(BreakdownTradeContext context)
    {
        if (_disposed || CurrentDestination != NavigationDestination.Breakdown) return;
        Navigate(NavigationDestination.Trades, null, context);
        if (!_disposed && CurrentDestination == NavigationDestination.Trades) await _tradeNavigationLoad;
    }
    private Task ReturnToBreakdownAsync()
    {
        if (_disposed || CurrentDestination != NavigationDestination.Trades) return Task.CompletedTask;
        Navigate(NavigationDestination.Breakdown);
        return CurrentDestination == NavigationDestination.Breakdown ? _breakdownViewModel?.LoadTask ?? Task.CompletedTask : Task.CompletedTask;
    }

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
        if (CurrentDestination == NavigationDestination.DailyReview && _dailyReviewViewModel?.TryLeave() == false) return;
        if (_disposed || !_journalViewModel.TryOpenScope(journal.TradingDate, journal.TradingAccountId,
            journal.AccountName ?? "Unavailable account")) return;
        Navigate(NavigationDestination.Journal);
        if (!_disposed && CurrentDestination == NavigationDestination.Journal)
            await _journalViewModel.LoadTask;
    }

    private async Task OpenPeriodJournalAsync(PersonalTradingJournal.Application.DailyReview.DailyReviewJournalEvidence journal)
    {
        if (ActivePeriod?.TryLeave() == false) return;
        if (_disposed || !_journalViewModel.TryOpenScope(journal.TradingDate, journal.TradingAccountId,
            journal.AccountName ?? "Unavailable account")) return;
        // Carry identity through the editor's independent read, not only the citation precheck.
        Navigate(NavigationDestination.Journal, journal.JournalId);
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

    private void Navigate(NavigationDestination destination) => Navigate(destination, null);

    internal bool NeedsPlanClosePreparation => _tradingPlan?.NeedsClosePreparation == true ||
        CurrentDestination == NavigationDestination.Notebook && _notebook?.NeedsClosePreparation == true;
    internal async Task<bool> PreparePlanCloseAsync()
    {
        if (_tradingPlan is not null && !await _tradingPlan.PrepareLeaveAsync()) return false;
        if (CurrentDestination == NavigationDestination.Notebook && _notebook is not null && !await _notebook.PrepareLeaveAsync())
        { _tradingPlan?.ClearLeaveConsent(); return false; }
        return true;
    }
    internal void CancelPlanClose() { _tradingPlan?.ClearLeaveConsent(); _notebook?.ClearLeaveConsent(); }

    private void Navigate(NavigationDestination destination, Guid? expectedJournalId, BreakdownTradeContext? breakdownScope = null)
    {
        if (_planNavigation) return;
        if (CurrentDestination == NavigationDestination.Notebook && destination != CurrentDestination && _notebook?.NeedsClosePreparation == true)
        { PlanNavigationTask = NavigateWithNotebookGuardAsync(destination, expectedJournalId, breakdownScope); return; }
        if (destination != NavigationDestination.TradingPlan && destination != CurrentDestination && _tradingPlan?.NeedsClosePreparation == true)
        { PlanNavigationTask = NavigateWithPlanGuardAsync(destination, expectedJournalId, breakdownScope); return; }
        NavigateCore(destination, expectedJournalId, breakdownScope);
    }
    private async Task NavigateWithNotebookGuardAsync(NavigationDestination destination, Guid? journal, BreakdownTradeContext? scope)
    {
        _planNavigation = true;
        try
        {
            if (!await PreparePlanCloseAsync() || _disposed) return;
            NavigateCore(destination, journal, scope);
            if (CurrentDestination == destination) { _notebook!.CompleteLeave(); _tradingPlan?.CompleteLeave(); }
        }
        finally { CancelPlanClose(); _planNavigation = false; }
    }
    private async Task NavigateWithPlanGuardAsync(NavigationDestination destination, Guid? journal, BreakdownTradeContext? scope)
    {
        _planNavigation = true;
        try
        {
            if (!await _tradingPlan!.PrepareLeaveAsync() || _disposed) return;
            NavigateCore(destination, journal, scope);
            if (CurrentDestination == destination) _tradingPlan.CompleteLeave();
            else _tradingPlan.ClearLeaveConsent();
        }
        finally { _planNavigation = false; }
    }
    // Related-page navigation deliberately retains the very same editor, including malformed raw text.
    private async Task<bool> OpenPlanPlaybookAsync(Guid id, CancellationToken ct)
    {
        if (_disposed || CurrentDestination != NavigationDestination.TradingPlan || _playbookViewModel is null) return false;
        ct.ThrowIfCancellationRequested(); NavigateCore(NavigationDestination.Playbook, null);
        if (CurrentDestination != NavigationDestination.Playbook) return false;
        await _playbookViewModel.LoadTask; ct.ThrowIfCancellationRequested();
        var setup = _playbookViewModel.Setups.FirstOrDefault(s => s.Id == id);
        if (setup is null || !_playbookViewModel.TrySelectSetup(setup)) return false;
        await _playbookViewModel.LoadTask;
        return _playbookViewModel.SelectedSetup?.Id == id;
    }
    private async Task<bool> OpenPlanRuleAsync(Guid id, CancellationToken ct)
    {
        if (_disposed || CurrentDestination != NavigationDestination.TradingPlan || _rulesViewModel is null) return false;
        ct.ThrowIfCancellationRequested(); NavigateCore(NavigationDestination.Rules, null);
        if (CurrentDestination != NavigationDestination.Rules) return false;
        await _rulesViewModel.LoadTask; ct.ThrowIfCancellationRequested();
        var rule = _rulesViewModel.Rules.FirstOrDefault(r => r.Id == id);
        return rule is not null && _rulesViewModel.TrySelect(rule);
    }

    private async Task<bool> OpenReviewPreparationAsync(PersonalTradingJournal.Application.TradingPlans.ReviewPlanRevision revision)
    {
        if (_disposed || _tradingPlan is null || _tradingPlan.IsBusy) return false;
        var origin = CurrentDestination;
        if (origin != NavigationDestination.DailyReview && ActivePeriod is null) return false;
        NavigateCore(NavigationDestination.TradingPlan, null, retainReview: true);
        if (CurrentDestination != NavigationDestination.TradingPlan) return false;
        await _tradingPlan.ContextTask;
        if (_disposed || CurrentDestination != NavigationDestination.TradingPlan) return false;
        bool opened = await _tradingPlan.OpenReviewRevisionAsync(revision);
        if (_disposed || CurrentDestination != NavigationDestination.TradingPlan) return false;
        if (!opened) { NavigateCore(origin, null, retainReview: true); return false; }
        _tradingPlan.ReturnToReview = () => NavigateCore(origin, null, retainReview: true);
        return true;
    }

    private Task OpenPlanDailyReviewAsync(PersonalTradingJournal.Domain.TradingPlans.TradingPlanKey key, string label)
    {
        if (_disposed || _dailyReviewViewModel is null || CurrentDestination != NavigationDestination.TradingPlan) return Task.CompletedTask;
        // Related navigation retains the same Plan editor. Existing Review draft and destination guards still run.
        if (!_dailyReviewViewModel.TryLeave()) return Task.CompletedTask;
        _dailyReviewViewModel.SelectedDate = key.NewYorkDate.ToDateTime(TimeOnly.MinValue);
        _dailyReviewViewModel.SelectedAccount = new(key.AccountId, label);
        NavigateCore(NavigationDestination.DailyReview, null);
        return _dailyReviewViewModel.LoadTask;
    }

    private void NavigateCore(NavigationDestination destination, Guid? expectedJournalId, BreakdownTradeContext? breakdownScope = null, bool retainReview = false)
    {
        if (_disposed) return;
        if (destination == NavigationDestination.Notebook && destination == CurrentDestination) return;
        if (destination != CurrentDestination && CurrentDestination == NavigationDestination.Notebook && _notebook?.TryLeave() == false) return;
        if (destination != CurrentDestination && CurrentDestination == NavigationDestination.Trades && !_tradesViewModel.TryLeaveBreakdownScope()) return;
        if (destination == CurrentDestination && ActivePeriod is not null) return;
        if (destination != CurrentDestination && _settingsViewModel.DataBackups?.TryLeave() == false) return;
        if (destination != CurrentDestination && ActivePeriod?.TryLeave() == false) return;
        if (destination != CurrentDestination && CurrentDestination == NavigationDestination.DailyReview && _dailyReviewViewModel?.TryLeave() == false) return;
        if (destination != CurrentDestination && CurrentDestination == NavigationDestination.Playbook &&
            _playbookViewModel?.TryLeave() == false) return;
        if (destination != CurrentDestination && CurrentDestination == NavigationDestination.Rules &&
            _rulesViewModel?.TryLeave() == false) return;
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

        bool returningToBreakdown = destination == NavigationDestination.Breakdown && CurrentDestination == NavigationDestination.Trades && _tradesViewModel.HasBreakdownScope;
        if (breakdownScope is not null && !_tradesViewModel.SetBreakdownScope(breakdownScope)) return;
        if (CurrentDestination == NavigationDestination.Trades) _tradesViewModel.DeactivateBrowse();
        if (CurrentDestination == NavigationDestination.Dashboard) _dashboardViewModel.Deactivate();
        if (CurrentDestination == NavigationDestination.Breakdown) _breakdownViewModel?.Deactivate();
        ActivePeriod?.Deactivate(retainReview);
        if (CurrentDestination == NavigationDestination.Calendar) _calendarViewModel.Deactivate();
        if (CurrentDestination == NavigationDestination.Journal) _journalViewModel.Deactivate(resetOnNextActivation: true);
        if (CurrentDestination == NavigationDestination.DailyReview) _dailyReviewViewModel?.Deactivate(retainReview);
        if (CurrentDestination == NavigationDestination.Playbook) _playbookViewModel?.Deactivate();
        if (CurrentDestination == NavigationDestination.Rules) _rulesViewModel?.Deactivate();
        if (CurrentDestination == NavigationDestination.Notebook) _notebook?.Deactivate();
        CurrentDestination = destination;
        NotifyNotebookReturn();
        UpdateNavigationSelection(destination);
        CurrentContentViewModel = destination switch
        {
            NavigationDestination.Dashboard => _dashboardViewModel,
            NavigationDestination.Notebook when _notebook is not null => _notebook,
            NavigationDestination.Breakdown when _breakdownViewModel is not null => _breakdownViewModel,
            NavigationDestination.Calendar => _calendarViewModel,
            NavigationDestination.Journal => _journalViewModel,
            NavigationDestination.DailyReview when _dailyReviewViewModel is not null => _dailyReviewViewModel,
            NavigationDestination.WeeklyReview when _periodReviews is not null => _periodReviews.Weekly,
            NavigationDestination.MonthlyReview when _periodReviews is not null => _periodReviews.Monthly,
            NavigationDestination.Accounts => _accountsViewModel,
            NavigationDestination.Instruments => _instrumentsViewModel,
            NavigationDestination.Import => _importViewModel,
            NavigationDestination.Mistakes => _tradingMistakesViewModel,
            NavigationDestination.Setups => _tradingSetupsViewModel,
            NavigationDestination.Playbook when _playbookViewModel is not null => _playbookViewModel,
            NavigationDestination.TradingPlan when _tradingPlan is not null => _tradingPlan,
            NavigationDestination.Rules when _rulesViewModel is not null => _rulesViewModel,
            NavigationDestination.Trades => _tradesViewModel,
            NavigationDestination.Settings => _settingsViewModel,
            _ => new PlaceholderViewModel(ContentPlaceholder),
        };

        if (destination == NavigationDestination.Calendar)
            _ = _calendarViewModel.ActivateAsync();
        if (destination == NavigationDestination.Notebook && _notebook is not null)
            _ = _notebook.ActivateAsync();
        if (destination == NavigationDestination.Breakdown && _breakdownViewModel is not null)
        {
            _ = _breakdownViewModel.ActivateAsync();
            if (returningToBreakdown) _ = _breakdownViewModel.RefreshAppliedAsync();
        }
        if (ActivePeriod is { } period) { if (retainReview) period.ResumeFromPreparation(); else _ = period.ActivateAsync(); }

        if (destination == NavigationDestination.Playbook && _playbookViewModel is not null)
            _ = _playbookViewModel.ActivateAsync();

        if (destination == NavigationDestination.TradingPlan && _tradingPlan is not null)
            _ = _tradingPlan.ActivateAsync();

        if (destination == NavigationDestination.Rules && _rulesViewModel is not null)
            _ = _rulesViewModel.ActivateAsync();

        if (destination == NavigationDestination.Journal)
            _ = _journalViewModel.ActivateAsync(loadTradeContext: true, expectedJournalId: expectedJournalId);

        if (destination == NavigationDestination.DailyReview && _dailyReviewViewModel is not null)
            if (retainReview) _dailyReviewViewModel.ResumeFromPreparation(); else _ = _dailyReviewViewModel.ActivateAsync();

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
            _tradesViewModel.ActivateBrowse();
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
        _breakdownViewModel?.OnDataCommitted();
        _accountsViewModel.InvalidateBalances(CurrentDestination == NavigationDestination.Accounts);
        _journalViewModel.TradeContext.OnDataCommitted();
        // Calendar's dedicated editor refreshes the modal after its save/reload completes.
        _dailyReviewViewModel?.OnDataCommitted();
        ActivePeriod?.OnDataCommitted();
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
        _accountsViewModel.InvalidateBalances(CurrentDestination == NavigationDestination.Accounts && sender != _accountsViewModel);
        _dashboardViewModel.OnDataCommitted();
        _breakdownViewModel?.OnDataCommitted();
        _calendarViewModel.OnDataCommitted();
        _journalViewModel.TradeContext.OnDataCommitted();
        _dailyReviewViewModel?.OnDataCommitted();
        ActivePeriod?.OnDataCommitted();
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
        ActivePeriod?.OnDataCommitted();
    }
}
