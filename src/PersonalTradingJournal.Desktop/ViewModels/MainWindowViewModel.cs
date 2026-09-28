using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.Theming;
using PersonalTradingJournal.Desktop.ViewModels.Accounts;
using PersonalTradingJournal.Desktop.ViewModels.Common;
using PersonalTradingJournal.Desktop.ViewModels.Dashboard;
using PersonalTradingJournal.Desktop.ViewModels.Instruments;
using PersonalTradingJournal.Desktop.ViewModels.Import;
using PersonalTradingJournal.Desktop.ViewModels.Mistakes;
using PersonalTradingJournal.Desktop.ViewModels.Setups;
using PersonalTradingJournal.Desktop.ViewModels.Settings;
using PersonalTradingJournal.Desktop.ViewModels.Trades;

namespace PersonalTradingJournal.Desktop.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly AccountsViewModel _accountsViewModel;
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly InstrumentsViewModel _instrumentsViewModel;
    private readonly ImportViewModel _importViewModel;
    private readonly TradingMistakesViewModel _tradingMistakesViewModel;
    private readonly TradingSetupsViewModel _tradingSetupsViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly IThemeService _themeService;
    private readonly TradesViewModel _tradesViewModel;
    private NavigationDestination _currentDestination = NavigationDestination.Dashboard;
    private ObservableObject _currentContentViewModel;

    public MainWindowViewModel(
        DashboardViewModel dashboardViewModel,
        AccountsViewModel accountsViewModel,
        InstrumentsViewModel instrumentsViewModel,
        ImportViewModel importViewModel,
        TradingMistakesViewModel tradingMistakesViewModel,
        TradingSetupsViewModel tradingSetupsViewModel,
        TradesViewModel tradesViewModel,
        SettingsViewModel settingsViewModel,
        IThemeService themeService)
    {
        ArgumentNullException.ThrowIfNull(dashboardViewModel);
        ArgumentNullException.ThrowIfNull(accountsViewModel);
        ArgumentNullException.ThrowIfNull(instrumentsViewModel);
        ArgumentNullException.ThrowIfNull(importViewModel);
        ArgumentNullException.ThrowIfNull(tradingMistakesViewModel);
        ArgumentNullException.ThrowIfNull(tradingSetupsViewModel);
        ArgumentNullException.ThrowIfNull(tradesViewModel);
        ArgumentNullException.ThrowIfNull(settingsViewModel);
        ArgumentNullException.ThrowIfNull(themeService);

        _dashboardViewModel = dashboardViewModel;
        _accountsViewModel = accountsViewModel;
        _instrumentsViewModel = instrumentsViewModel;
        _importViewModel = importViewModel;
        _tradingMistakesViewModel = tradingMistakesViewModel;
        _tradingSetupsViewModel = tradingSetupsViewModel;
        _tradesViewModel = tradesViewModel;
        _settingsViewModel = settingsViewModel;
        _themeService = themeService;
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

    public void Dispose()
    {
        _themeService.ThemeChanged -= OnThemeChanged;
        _importViewModel.ImportCommitted -= OnImportCommitted;
    }

    private void Navigate(NavigationDestination destination)
    {
        ExpandContainingSection(destination);

        if (destination == CurrentDestination)
        {
            return;
        }

        CurrentDestination = destination;
        UpdateNavigationSelection(destination);
        CurrentContentViewModel = destination switch
        {
            NavigationDestination.Dashboard => _dashboardViewModel,
            NavigationDestination.Accounts => _accountsViewModel,
            NavigationDestination.Instruments => _instrumentsViewModel,
            NavigationDestination.Import => _importViewModel,
            NavigationDestination.Mistakes => _tradingMistakesViewModel,
            NavigationDestination.Setups => _tradingSetupsViewModel,
            NavigationDestination.Trades => _tradesViewModel,
            NavigationDestination.Settings => _settingsViewModel,
            _ => new PlaceholderViewModel(ContentPlaceholder),
        };

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
            _ = _tradesViewModel.EnsureLoadedAsync();
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
        _tradesViewModel.InvalidateLoadedDataAfterExternalImport();
        if (e.CreatedInstrumentCount > 0)
        {
            _instrumentsViewModel.InvalidateLoadedDataAfterExternalImport();
        }
    }
}
