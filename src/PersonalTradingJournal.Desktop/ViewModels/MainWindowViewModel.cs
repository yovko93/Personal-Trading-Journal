using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.ViewModels.Accounts;
using PersonalTradingJournal.Desktop.ViewModels.Common;
using PersonalTradingJournal.Desktop.ViewModels.Dashboard;
using PersonalTradingJournal.Desktop.ViewModels.Instruments;
using PersonalTradingJournal.Desktop.ViewModels.Trades;

namespace PersonalTradingJournal.Desktop.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly AccountsViewModel _accountsViewModel;
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly InstrumentsViewModel _instrumentsViewModel;
    private readonly TradesViewModel _tradesViewModel;
    private NavigationDestination _currentDestination = NavigationDestination.Dashboard;
    private ObservableObject _currentContentViewModel;

    public MainWindowViewModel(
        DashboardViewModel dashboardViewModel,
        AccountsViewModel accountsViewModel,
        InstrumentsViewModel instrumentsViewModel,
        TradesViewModel tradesViewModel)
    {
        ArgumentNullException.ThrowIfNull(dashboardViewModel);
        ArgumentNullException.ThrowIfNull(accountsViewModel);
        ArgumentNullException.ThrowIfNull(instrumentsViewModel);
        ArgumentNullException.ThrowIfNull(tradesViewModel);

        _dashboardViewModel = dashboardViewModel;
        _accountsViewModel = accountsViewModel;
        _instrumentsViewModel = instrumentsViewModel;
        _tradesViewModel = tradesViewModel;
        _currentContentViewModel = dashboardViewModel;
        NavigateCommand = new RelayCommand<NavigationDestination>(Navigate);
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
        NavigationDestination.Strategies => "Strategies",
        NavigationDestination.Mistakes => "Mistakes",
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

    private void Navigate(NavigationDestination destination)
    {
        if (destination == CurrentDestination)
        {
            return;
        }

        CurrentDestination = destination;
        CurrentContentViewModel = destination switch
        {
            NavigationDestination.Dashboard => _dashboardViewModel,
            NavigationDestination.Accounts => _accountsViewModel,
            NavigationDestination.Instruments => _instrumentsViewModel,
            NavigationDestination.Trades => _tradesViewModel,
            _ => new PlaceholderViewModel(ContentPlaceholder),
        };

        if (destination == NavigationDestination.Accounts)
        {
            _ = _accountsViewModel.EnsureLoadedAsync();
        }

        if (destination == NavigationDestination.Instruments)
        {
            _ = _instrumentsViewModel.EnsureLoadedAsync();
        }

        if (destination == NavigationDestination.Trades)
        {
            _ = _tradesViewModel.EnsureLoadedAsync();
        }
    }
}
