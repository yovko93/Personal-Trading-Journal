using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels;
using PersonalTradingJournal.Desktop.ViewModels.Accounts;
using PersonalTradingJournal.Desktop.ViewModels.Common;
using PersonalTradingJournal.Desktop.ViewModels.Dashboard;
using PersonalTradingJournal.Desktop.ViewModels.Instruments;
using PersonalTradingJournal.Desktop.ViewModels.Trades;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Desktop.Tests.Navigation;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void Constructor_UsesSuppliedDashboardAsInitialContent()
    {
        ViewModelFixture fixture = CreateFixture();

        Assert.Equal(NavigationDestination.Dashboard, fixture.Main.CurrentDestination);
        Assert.Equal("Dashboard", fixture.Main.PageTitle);
        Assert.Same(fixture.Dashboard, fixture.Main.CurrentContentViewModel);
    }

    [Fact]
    public void NavigateToAccounts_UsesRetainedAccountsViewModelAndStartsLoading()
    {
        ViewModelFixture fixture = CreateFixture();

        fixture.Main.NavigateCommand.Execute(NavigationDestination.Accounts);

        Assert.Equal(NavigationDestination.Accounts, fixture.Main.CurrentDestination);
        Assert.Equal("Accounts", fixture.Main.PageTitle);
        Assert.Same(fixture.Accounts, fixture.Main.CurrentContentViewModel);
        Assert.Equal(1, fixture.AccountReader.CallCount);
    }

    [Fact]
    public void NavigateToInstruments_UsesRetainedInstrumentsViewModelAndStartsLoading()
    {
        ViewModelFixture fixture = CreateFixture();

        fixture.Main.NavigateCommand.Execute(NavigationDestination.Instruments);

        Assert.Equal(NavigationDestination.Instruments, fixture.Main.CurrentDestination);
        Assert.Equal("Instruments", fixture.Main.PageTitle);
        Assert.Same(fixture.Instruments, fixture.Main.CurrentContentViewModel);
        Assert.Equal(1, fixture.InstrumentReader.CallCount);
    }

    [Fact]
    public void NavigateToTrades_UsesRetainedTradesViewModelAndStartsLoading()
    {
        ViewModelFixture fixture = CreateFixture();

        fixture.Main.NavigateCommand.Execute(NavigationDestination.Trades);

        Assert.Equal(NavigationDestination.Trades, fixture.Main.CurrentDestination);
        Assert.Equal("Trades", fixture.Main.PageTitle);
        Assert.Same(fixture.Trades, fixture.Main.CurrentContentViewModel);
        Assert.Equal(1, fixture.TradeReferenceDataReader.CallCount);
    }

    [Fact]
    public void NavigateAwayAndBackToTrades_RetainsViewModelAndManualEntryState()
    {
        ViewModelFixture fixture = CreateFixture();
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Trades);
        fixture.Trades.ShowManualEntryCommand.Execute(null);
        var selectedAccount = new ManualTradeAccountOption(
            Guid.NewGuid(),
            "Primary Account",
            TradingAccountType.Personal,
            "Broker",
            "ACCOUNT-1",
            "USD",
            true);
        var selectedInstrument = new ManualTradeInstrumentOption(
            Guid.NewGuid(),
            "NQ",
            "Nasdaq-100 E-mini",
            AssetClass.Futures,
            "CME",
            "USD",
            0.25m,
            5m,
            20m,
            true);
        fixture.Trades.SelectedAccount = selectedAccount;
        fixture.Trades.SelectedInstrument = selectedInstrument;
        fixture.Trades.SelectedDirection = TradeDirection.Short;
        fixture.Trades.QuantityText = "2.5";
        fixture.Trades.EntryExecutedAtUtcText = "2026-09-10 13:30:00";
        fixture.Trades.EntryPriceText = "23950.25";
        fixture.Trades.EntryCommissionText = "1.50";
        fixture.Trades.EntryFeesText = "0.25";
        fixture.Trades.HasExit = true;
        fixture.Trades.ExitExecutedAtUtcText = "2026-09-10 14:15:00";
        fixture.Trades.ExitPriceText = "23900.00";
        fixture.Trades.ExitCommissionText = "1.50";
        fixture.Trades.ExitFeesText = "0.25";

        fixture.Main.NavigateCommand.Execute(NavigationDestination.Accounts);
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Trades);

        Assert.Same(fixture.Trades, fixture.Main.CurrentContentViewModel);
        Assert.True(fixture.Trades.IsManualEntryVisible);
        Assert.Same(selectedAccount, fixture.Trades.SelectedAccount);
        Assert.Same(selectedInstrument, fixture.Trades.SelectedInstrument);
        Assert.Equal(TradeDirection.Short, fixture.Trades.SelectedDirection);
        Assert.Equal("2.5", fixture.Trades.QuantityText);
        Assert.Equal("2026-09-10 13:30:00", fixture.Trades.EntryExecutedAtUtcText);
        Assert.Equal("23950.25", fixture.Trades.EntryPriceText);
        Assert.Equal("1.50", fixture.Trades.EntryCommissionText);
        Assert.Equal("0.25", fixture.Trades.EntryFeesText);
        Assert.True(fixture.Trades.HasExit);
        Assert.Equal("2026-09-10 14:15:00", fixture.Trades.ExitExecutedAtUtcText);
        Assert.Equal("23900.00", fixture.Trades.ExitPriceText);
        Assert.Equal("1.50", fixture.Trades.ExitCommissionText);
        Assert.Equal("0.25", fixture.Trades.ExitFeesText);
        Assert.Equal(1, fixture.TradeReferenceDataReader.CallCount);
    }

    [Fact]
    public void NavigateToPlaceholder_UsesPlaceholderViewModel()
    {
        ViewModelFixture fixture = CreateFixture();

        fixture.Main.NavigateCommand.Execute(NavigationDestination.Notebook);

        Assert.Equal(NavigationDestination.Notebook, fixture.Main.CurrentDestination);
        Assert.Equal("Notebook", fixture.Main.PageTitle);
        var placeholder = Assert.IsType<PlaceholderViewModel>(
            fixture.Main.CurrentContentViewModel);
        Assert.Equal("Notebook content will appear here.", placeholder.Message);
    }

    [Fact]
    public void NavigateBackToDashboard_ReusesOriginalDashboardViewModel()
    {
        ViewModelFixture fixture = CreateFixture();
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Notebook);

        fixture.Main.NavigateCommand.Execute(NavigationDestination.Dashboard);

        Assert.Equal(NavigationDestination.Dashboard, fixture.Main.CurrentDestination);
        Assert.Equal("Dashboard", fixture.Main.PageTitle);
        Assert.Same(fixture.Dashboard, fixture.Main.CurrentContentViewModel);
    }

    [Fact]
    public void NavigateToCurrentDestination_DoesNotReplaceContentViewModel()
    {
        ViewModelFixture fixture = CreateFixture();
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Notebook);
        object originalContent = fixture.Main.CurrentContentViewModel;

        fixture.Main.NavigateCommand.Execute(NavigationDestination.Notebook);

        Assert.Same(originalContent, fixture.Main.CurrentContentViewModel);
    }

    private static ViewModelFixture CreateFixture()
    {
        var accountReader = new FakeTradingAccountReader();
        accountReader.EnqueueResult([]);
        var accountStore = new FakeTradingAccountStore();
        var instrumentReader = new FakeInstrumentReader();
        instrumentReader.EnqueueResult([]);
        var instrumentStore = new FakeInstrumentStore();
        var tradeReferenceDataReader = new FakeManualTradeReferenceDataReader();
        tradeReferenceDataReader.EnqueueResult(new ManualTradeReferenceData([], []));
        var timeProvider = new FixedTimeProvider();
        var dashboard = new DashboardViewModel();
        var accounts = new AccountsViewModel(
            accountReader,
            new CreateTradingAccountUseCase(accountStore, timeProvider),
            new TradingAccountLifecycleUseCase(accountStore, timeProvider));
        var instruments = new InstrumentsViewModel(
            instrumentReader,
            new CreateInstrumentUseCase(instrumentStore, timeProvider),
            new InstrumentLifecycleUseCase(instrumentStore, timeProvider));
        var trades = new TradesViewModel(tradeReferenceDataReader);
        var main = new MainWindowViewModel(dashboard, accounts, instruments, trades);

        return new ViewModelFixture(
            main,
            dashboard,
            accounts,
            instruments,
            trades,
            accountReader,
            instrumentReader,
            tradeReferenceDataReader);
    }

    private sealed record ViewModelFixture(
        MainWindowViewModel Main,
        DashboardViewModel Dashboard,
        AccountsViewModel Accounts,
        InstrumentsViewModel Instruments,
        TradesViewModel Trades,
        FakeTradingAccountReader AccountReader,
        FakeInstrumentReader InstrumentReader,
        FakeManualTradeReferenceDataReader TradeReferenceDataReader);
}
