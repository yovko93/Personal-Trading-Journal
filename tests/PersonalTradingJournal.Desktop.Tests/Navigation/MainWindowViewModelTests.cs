using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Mistakes;
using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Application.Trades;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Theming;
using PersonalTradingJournal.Desktop.ViewModels;
using PersonalTradingJournal.Desktop.ViewModels.Accounts;
using PersonalTradingJournal.Desktop.ViewModels.Common;
using PersonalTradingJournal.Desktop.ViewModels.Dashboard;
using PersonalTradingJournal.Desktop.ViewModels.Instruments;
using PersonalTradingJournal.Desktop.ViewModels.Mistakes;
using PersonalTradingJournal.Desktop.ViewModels.Setups;
using PersonalTradingJournal.Desktop.ViewModels.Settings;
using PersonalTradingJournal.Desktop.ViewModels.Trades;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Desktop.Tests.Navigation;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void NavigationDestinationsContainNineteenEntries() =>
        Assert.Equal(19, Enum.GetValues<NavigationDestination>().Length);

    [Fact]
    public void NavigationCatalogCoversEveryDestinationExactlyOnce()
    {
        ViewModelFixture fixture = CreateFixture();

        Assert.Equal(
            Enum.GetValues<NavigationDestination>(),
            fixture.Main.AllNavigationItems.Select(item => item.Destination));
        Assert.Equal(19, fixture.Main.AllNavigationItems.Count);
        Assert.Equal(
            fixture.Main.AllNavigationItems.Count,
            fixture.Main.AllNavigationItems.Select(item => item.Destination).Distinct().Count());
        Assert.All(fixture.Main.AllNavigationItems, item => Assert.False(string.IsNullOrWhiteSpace(item.IconKey)));
        Assert.Equal(
            NavigationDestination.Dashboard,
            Assert.Single(fixture.Main.AllNavigationItems, item => item.IsSelected).Destination);
    }

    [Fact]
    public void NavigationCatalogUsesExpectedHierarchy()
    {
        ViewModelFixture fixture = CreateFixture();

        Assert.Equal(
            [NavigationDestination.Dashboard, NavigationDestination.Notebook],
            fixture.Main.TopNavigationItems.Select(item => item.Destination));
        Assert.Equal(["TRADING", "ANALYSIS", "PLANNING", "REVIEW"],
            fixture.Main.NavigationSections.Select(section => section.Title));
        Assert.Equal(
            [
                NavigationDestination.Trades,
                NavigationDestination.Journal,
                NavigationDestination.Calendar,
                NavigationDestination.Import,
            ],
            fixture.Main.NavigationSections[0].Items.Select(item => item.Destination));
        Assert.Equal(
            [
                NavigationDestination.Performance,
                NavigationDestination.Setups,
                NavigationDestination.Mistakes,
                NavigationDestination.Breakdown,
            ],
            fixture.Main.NavigationSections[1].Items.Select(item => item.Destination));
        Assert.Equal(
            [
                NavigationDestination.Playbook,
                NavigationDestination.TradingPlan,
                NavigationDestination.Rules,
            ],
            fixture.Main.NavigationSections[2].Items.Select(item => item.Destination));
        Assert.Equal(
            [
                NavigationDestination.DailyReview,
                NavigationDestination.WeeklyReview,
                NavigationDestination.MonthlyReview,
            ],
            fixture.Main.NavigationSections[3].Items.Select(item => item.Destination));
        Assert.Equal(
            [NavigationDestination.Accounts, NavigationDestination.Instruments, NavigationDestination.Settings],
            fixture.Main.BottomNavigationItems.Select(item => item.Destination));
        Assert.All(fixture.Main.NavigationSections, section =>
        {
            Assert.True(section.IsCollapsible);
            Assert.True(section.IsExpanded);
        });
    }

    [Fact]
    public void SectionToggleChangesOnlyItsExpandedState()
    {
        ViewModelFixture fixture = CreateFixture();
        NavigationSectionViewModel trading = fixture.Main.NavigationSections[0];

        trading.ToggleCommand.Execute(null);

        Assert.False(trading.IsExpanded);
        Assert.Equal(NavigationDestination.Dashboard, fixture.Main.CurrentDestination);
        Assert.All(fixture.Main.NavigationSections.Skip(1), section => Assert.True(section.IsExpanded));

        trading.ToggleCommand.Execute(null);

        Assert.True(trading.IsExpanded);
    }

    [Fact]
    public void NavigateSelectsExactlyOneItemAndExpandsItsSection()
    {
        ViewModelFixture fixture = CreateFixture();
        NavigationSectionViewModel analysis = fixture.Main.NavigationSections[1];
        analysis.ToggleCommand.Execute(null);
        Assert.False(analysis.IsExpanded);

        fixture.Main.NavigateCommand.Execute(NavigationDestination.Mistakes);

        Assert.True(analysis.IsExpanded);
        NavigationItemViewModel selected = Assert.Single(
            fixture.Main.AllNavigationItems,
            item => item.IsSelected);
        Assert.Equal(NavigationDestination.Mistakes, selected.Destination);
    }

    [Theory]
    [MemberData(nameof(AllDestinations))]
    public void EveryDestinationCanBecomeTheSingleSelectedItem(
        NavigationDestination destination)
    {
        ViewModelFixture fixture = CreateFixture();

        fixture.Main.NavigateCommand.Execute(destination);

        Assert.Equal(destination, fixture.Main.CurrentDestination);
        Assert.Equal(
            destination,
            Assert.Single(fixture.Main.AllNavigationItems, item => item.IsSelected).Destination);
    }

    public static TheoryData<NavigationDestination> AllDestinations =>
        new(Enum.GetValues<NavigationDestination>());

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
        Assert.Equal(1, fixture.TradeListReader.CallCount);
    }

    [Fact]
    public void NavigateToSetupsUsesRetainedViewModelAndLoadsOnlyOnce()
    {
        ViewModelFixture fixture = CreateFixture();
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Setups);
        object content = fixture.Main.CurrentContentViewModel;
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Accounts);
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Setups);

        Assert.Equal("Trading Setups", fixture.Main.PageTitle);
        Assert.Same(fixture.Setups, content);
        Assert.Same(content, fixture.Main.CurrentContentViewModel);
        Assert.Equal(1, fixture.SetupReader.CallCount);
    }

    [Fact]
    public void NavigateToMistakesUsesRetainedViewModelAndLoadsOnlyOnce()
    {
        ViewModelFixture fixture = CreateFixture();
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Mistakes);
        object content = fixture.Main.CurrentContentViewModel;
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Accounts);
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Mistakes);
        Assert.Equal("Trading Mistakes", fixture.Main.PageTitle);
        Assert.Same(fixture.Mistakes, content); Assert.Same(content, fixture.Main.CurrentContentViewModel);
        Assert.Equal(1, fixture.MistakeReader.CallCount);
    }

    [Fact]
    public void NavigateAwayAndBackToTrades_ResetsManualEntryStateWithoutReloading()
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
        fixture.Trades.EntryExecutedAtNewYorkText = "2026-09-10 13:30:00";
        fixture.Trades.EntryPriceText = "23950.25";
        fixture.Trades.EntryCommissionText = "1.50";
        fixture.Trades.EntryFeesText = "0.25";
        fixture.Trades.HasExit = true;
        fixture.Trades.ExitExecutedAtNewYorkText = "2026-09-10 14:15:00";
        fixture.Trades.ExitPriceText = "23900.00";
        fixture.Trades.ExitCommissionText = "1.50";
        fixture.Trades.ExitFeesText = "0.25";

        fixture.Main.NavigateCommand.Execute(NavigationDestination.Accounts);
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Trades);

        Assert.Same(fixture.Trades, fixture.Main.CurrentContentViewModel);
        Assert.False(fixture.Trades.IsManualEntryVisible);
        Assert.Null(fixture.Trades.SelectedAccount);
        Assert.Null(fixture.Trades.SelectedInstrument);
        Assert.Null(fixture.Trades.SelectedDirection);
        Assert.Empty(fixture.Trades.QuantityText);
        Assert.Empty(fixture.Trades.EntryExecutedAtNewYorkText);
        Assert.Empty(fixture.Trades.EntryPriceText);
        Assert.Equal("0", fixture.Trades.EntryCommissionText);
        Assert.Equal("0", fixture.Trades.EntryFeesText);
        Assert.False(fixture.Trades.HasExit);
        Assert.Empty(fixture.Trades.ExitExecutedAtNewYorkText);
        Assert.Empty(fixture.Trades.ExitPriceText);
        Assert.Equal("0", fixture.Trades.ExitCommissionText);
        Assert.Equal("0", fixture.Trades.ExitFeesText);
        Assert.Equal(1, fixture.TradeReferenceDataReader.CallCount);
        Assert.Equal(1, fixture.TradeListReader.CallCount);
        Assert.True(fixture.Trades.HasTrades);
    }

    [Fact]
    public async Task NavigateAwayAndBackToTrades_ClosesDetailWithoutReloadingList()
    {
        ViewModelFixture fixture = CreateFixture();
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Trades);
        TradeListItem listItem = Assert.Single(fixture.Trades.RecentTrades);
        TradeDetail detail = CreateTradeDetail(listItem);
        fixture.TradeDetailReader.EnqueueResult(detail);
        await fixture.Trades.ShowTradeDetailCommand.ExecuteAsync(listItem);

        fixture.Main.NavigateCommand.Execute(NavigationDestination.Accounts);
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Trades);

        Assert.Same(fixture.Trades, fixture.Main.CurrentContentViewModel);
        Assert.False(fixture.Trades.IsTradeDetailVisible);
        Assert.Null(fixture.Trades.SelectedTradeDetail);
        Assert.Equal(1, fixture.TradeDetailReader.CallCount);
        Assert.Equal(1, fixture.TradeScreenshotReader.CallCount);
        Assert.Equal(1, fixture.TradeListReader.CallCount);
        Assert.True(fixture.Trades.HasTrades);
    }

    [Fact]
    public async Task NavigateAwayAndBackToAccounts_ResetsDetailEditAndCreateStateWithoutReloading()
    {
        ViewModelFixture fixture = CreateFixture();
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Accounts);
        IReadOnlyList<AccountListItem> loadedAccounts = fixture.Accounts.Accounts;
        var details = new TradingAccountDetails(
            Guid.NewGuid(), "Primary", TradingAccountType.Personal, "Broker", "A-1",
            "USD", 1000m, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        fixture.AccountReader.EnqueueDetailResult(details);
        await fixture.Accounts.EditAccountCommand.ExecuteAsync(details.Id);

        fixture.Main.NavigateCommand.Execute(NavigationDestination.Dashboard);
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Accounts);

        Assert.Null(fixture.Accounts.SelectedAccount);
        Assert.False(fixture.Accounts.IsEditFormVisible);
        Assert.Empty(fixture.Accounts.EditAccountName);
        fixture.Accounts.ShowCreateFormCommand.Execute(null);
        fixture.Accounts.AccountName = "Draft";
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Dashboard);
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Accounts);
        Assert.False(fixture.Accounts.IsCreateFormVisible);
        Assert.Empty(fixture.Accounts.AccountName);
        Assert.Same(loadedAccounts, fixture.Accounts.Accounts);
        Assert.Equal(1, fixture.AccountReader.CallCount);
        Assert.Equal(1, fixture.AccountReader.DetailCallCount);
    }

    [Fact]
    public async Task NavigateAwayAndBackToInstruments_ResetsDetailEditAndCreateStateWithoutReloading()
    {
        ViewModelFixture fixture = CreateFixture();
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Instruments);
        IReadOnlyList<InstrumentListItem> loadedInstruments = fixture.Instruments.Instruments;
        var details = new InstrumentDetails(
            Guid.NewGuid(), "ES", "E-mini S&P 500", AssetClass.Futures, "CME",
            "USD", 0.25m, 12.50m, 50m, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        fixture.InstrumentReader.DetailsToReturn = details;
        await fixture.Instruments.EditInstrumentCommand.ExecuteAsync(details.Id);

        fixture.Main.NavigateCommand.Execute(NavigationDestination.Dashboard);
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Instruments);

        Assert.Null(fixture.Instruments.SelectedInstrument);
        Assert.False(fixture.Instruments.IsEditFormVisible);
        Assert.Empty(fixture.Instruments.EditSymbol);
        fixture.Instruments.ShowCreateFormCommand.Execute(null);
        fixture.Instruments.Symbol = "Draft";
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Dashboard);
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Instruments);
        Assert.False(fixture.Instruments.IsCreateFormVisible);
        Assert.Empty(fixture.Instruments.Symbol);
        Assert.Same(loadedInstruments, fixture.Instruments.Instruments);
        Assert.Equal(1, fixture.InstrumentReader.CallCount);
        Assert.Equal(1, fixture.InstrumentReader.DetailsCallCount);
    }

    [Fact]
    public async Task NavigateAwayAndBackToSetups_ResetsDetailEditAndCreateStateWithoutReloading()
    {
        ViewModelFixture fixture = CreateFixture();
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Setups);
        IReadOnlyList<TradingSetupListItem> loadedSetups = fixture.Setups.TradingSetups;
        var details = new TradingSetupDetails(
            Guid.NewGuid(), "Breakout", "Opening range", true,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        fixture.SetupReader.DetailsToReturn = details;
        await fixture.Setups.EditCommand.ExecuteAsync(details.Id);

        fixture.Main.NavigateCommand.Execute(NavigationDestination.Dashboard);
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Setups);

        Assert.Null(fixture.Setups.SelectedTradingSetup);
        Assert.False(fixture.Setups.IsEditFormVisible);
        Assert.Empty(fixture.Setups.EditNameText);
        fixture.Setups.ShowCreateCommand.Execute(null);
        fixture.Setups.NameText = "Draft";
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Dashboard);
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Setups);
        Assert.False(fixture.Setups.IsCreateFormVisible);
        Assert.Empty(fixture.Setups.NameText);
        Assert.Same(loadedSetups, fixture.Setups.TradingSetups);
        Assert.Equal(1, fixture.SetupReader.CallCount);
        Assert.Equal(1, fixture.SetupReader.DetailsCallCount);
    }

    [Fact]
    public async Task NavigateAwayAndBackToMistakes_ResetsDetailEditAndCreateStateWithoutReloading()
    {
        ViewModelFixture fixture = CreateFixture();
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Mistakes);
        IReadOnlyList<TradingMistakeListItem> loadedMistakes = fixture.Mistakes.TradingMistakes;
        var details = new TradingMistakeDetails(
            Guid.NewGuid(), "FOMO", "Late entry", true,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        fixture.MistakeReader.DetailsToReturn = details;
        await fixture.Mistakes.EditCommand.ExecuteAsync(details.Id);

        fixture.Main.NavigateCommand.Execute(NavigationDestination.Dashboard);
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Mistakes);

        Assert.Null(fixture.Mistakes.SelectedTradingMistake);
        Assert.False(fixture.Mistakes.IsEditFormVisible);
        Assert.Empty(fixture.Mistakes.EditNameText);
        fixture.Mistakes.ShowCreateCommand.Execute(null);
        fixture.Mistakes.NameText = "Draft";
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Dashboard);
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Mistakes);
        Assert.False(fixture.Mistakes.IsCreateFormVisible);
        Assert.Empty(fixture.Mistakes.NameText);
        Assert.Same(loadedMistakes, fixture.Mistakes.TradingMistakes);
        Assert.Equal(1, fixture.MistakeReader.CallCount);
        Assert.Equal(1, fixture.MistakeReader.DetailsCallCount);
    }

    [Fact]
    public void NavigateToCurrentRecordDestination_DoesNotResetTransientState()
    {
        ViewModelFixture fixture = CreateFixture();
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Accounts);
        fixture.Accounts.ShowCreateFormCommand.Execute(null);
        fixture.Accounts.AccountName = "Draft";

        fixture.Main.NavigateCommand.Execute(NavigationDestination.Accounts);

        Assert.True(fixture.Accounts.IsCreateFormVisible);
        Assert.Equal("Draft", fixture.Accounts.AccountName);
        Assert.Equal(1, fixture.AccountReader.CallCount);
    }

    [Fact]
    public void ResetTransientState_CanBeCalledRepeatedlyWithoutClearingLoadedAccounts()
    {
        ViewModelFixture fixture = CreateFixture();
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Accounts);
        IReadOnlyList<AccountListItem> loadedAccounts = fixture.Accounts.Accounts;
        fixture.Accounts.ShowCreateFormCommand.Execute(null);
        fixture.Accounts.AccountName = "Draft";

        fixture.Accounts.ResetTransientState();
        fixture.Accounts.ResetTransientState();

        Assert.False(fixture.Accounts.IsCreateFormVisible);
        Assert.Empty(fixture.Accounts.AccountName);
        Assert.Null(fixture.Accounts.SelectedAccount);
        Assert.Same(loadedAccounts, fixture.Accounts.Accounts);
        Assert.Equal(1, fixture.AccountReader.CallCount);
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
    public void NavigateToSettingsUsesRetainedConcreteSettingsViewModel()
    {
        ViewModelFixture fixture = CreateFixture();

        fixture.Main.NavigateCommand.Execute(NavigationDestination.Settings);

        Assert.Equal(NavigationDestination.Settings, fixture.Main.CurrentDestination);
        Assert.Equal("Settings", fixture.Main.PageTitle);
        Assert.Same(fixture.Settings, fixture.Main.CurrentContentViewModel);
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

    [Theory]
    [InlineData(AppTheme.System, AppTheme.Dark, AppTheme.Light)]
    [InlineData(AppTheme.System, AppTheme.Light, AppTheme.Dark)]
    [InlineData(AppTheme.Dark, AppTheme.Dark, AppTheme.Light)]
    [InlineData(AppTheme.Light, AppTheme.Light, AppTheme.Dark)]
    public async Task HeaderToggleUsesEffectiveThemeAndPersistsOppositeExplicitPreference(
        AppTheme preferredTheme,
        AppTheme effectiveTheme,
        AppTheme expectedPreference)
    {
        ViewModelFixture fixture = CreateFixture(preferredTheme, effectiveTheme);

        await fixture.Main.ToggleThemeCommand.ExecuteAsync(null);

        Assert.Equal(expectedPreference, fixture.ThemeService.PreferredTheme);
        Assert.Equal(expectedPreference, fixture.ThemeService.EffectiveTheme);
        Assert.Equal(
            expectedPreference,
            Assert.Single(fixture.SettingsStore.SavedSettings).Theme);
    }

    [Fact]
    public async Task SettingsSelectionSynchronizesHeaderToggleState()
    {
        ViewModelFixture fixture = CreateFixture(AppTheme.Dark, AppTheme.Dark);
        Assert.False(fixture.Main.IsLightTheme);
        Assert.Equal("Switch to light theme", fixture.Main.ThemeToggleToolTip);

        await fixture.Settings.ChangeThemeCommand.ExecuteAsync(AppTheme.Light);

        Assert.True(fixture.Main.IsLightTheme);
        Assert.Equal("Switch to dark theme", fixture.Main.ThemeToggleToolTip);
    }

    [Fact]
    public void SystemThemeChangeSynchronizesHeaderToggleWithoutChangingPreference()
    {
        ViewModelFixture fixture = CreateFixture(AppTheme.System, AppTheme.Dark);

        fixture.ThemeService.SimulateSystemThemeChange(AppTheme.Light);

        Assert.True(fixture.Main.IsLightTheme);
        Assert.Equal(AppTheme.System, fixture.ThemeService.PreferredTheme);
        Assert.True(fixture.Settings.IsSystemSelected);
    }

    [Fact]
    public async Task RepeatedHeaderToggleProducesOneExplicitTransitionPerInvocation()
    {
        ViewModelFixture fixture = CreateFixture(AppTheme.System, AppTheme.Dark);

        await fixture.Main.ToggleThemeCommand.ExecuteAsync(null);
        await fixture.Main.ToggleThemeCommand.ExecuteAsync(null);

        Assert.Equal([AppTheme.Light, AppTheme.Dark], fixture.ThemeService.SetPreferences);
        Assert.Equal(2, fixture.SettingsStore.SavedSettings.Count);
        Assert.Equal(AppTheme.Dark, fixture.ThemeService.PreferredTheme);
        Assert.False(fixture.Main.IsLightTheme);
    }

    [Fact]
    public void DisposeUnsubscribesHeaderFromThemeService()
    {
        ViewModelFixture fixture = CreateFixture();
        Assert.Equal(2, fixture.ThemeService.SubscriberCount);

        fixture.Main.Dispose();

        Assert.Equal(1, fixture.ThemeService.SubscriberCount);
    }

    private static ViewModelFixture CreateFixture(
        AppTheme preferredTheme = AppTheme.System,
        AppTheme? effectiveTheme = null)
    {
        var accountReader = new FakeTradingAccountReader();
        accountReader.EnqueueResult([]);
        var accountStore = new FakeTradingAccountStore();
        var instrumentReader = new FakeInstrumentReader();
        instrumentReader.EnqueueResult([]);
        var instrumentStore = new FakeInstrumentStore();
        var setupReader = new FakeTradingSetupReader();
        setupReader.EnqueueResult([]);
        var setupStore = new FakeTradingSetupStore();
        var setupNameChecker = new FakeTradingSetupNameChecker();
        var mistakeReader = new FakeTradingMistakeReader(); mistakeReader.EnqueueResult([]);
        var mistakeStore = new FakeTradingMistakeStore(); var mistakeChecker = new FakeTradingMistakeNameChecker();
        var tradeReferenceDataReader = new FakeManualTradeReferenceDataReader();
        tradeReferenceDataReader.EnqueueResult(new ManualTradeReferenceData([], []));
        var tradeListReader = new FakeTradeListReader();
        var tradeDetailReader = new FakeTradeDetailReader();
        DateTimeOffset openedAtUtc =
            new(2026, 9, 10, 13, 30, 0, TimeSpan.Zero);
        var tradeListItem = new TradeListItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Primary Account",
            Guid.NewGuid(),
            "NQ",
            TradeDirection.Long,
            TradeStatus.Closed,
            openedAtUtc,
            openedAtUtc.AddHours(1),
            0m,
            23950.25m,
            23975.50m,
            3.50m,
            1262.50m,
            1259m,
            "USD");
        tradeListReader.EnqueueResult(
        [
            tradeListItem,
        ]);
        var tradeStore = new FakeTradeStore();
        var tradeScreenshotReader = new FakeTradeScreenshotReader();
        var tradeExistenceReader = new FakeTradeExistenceReader();
        var tradeScreenshotFileStorage = new FakeTradeScreenshotFileStorage();
        var tradeScreenshotStore = new FakeTradeScreenshotStore();
        var tradeScreenshotFilePicker = new FakeTradeScreenshotFilePicker();
        var tradeScreenshotContentReader = new FakeTradeScreenshotContentReader();
        var tradeScreenshotImageDecoder = new FakeTradeScreenshotImageDecoder();
        var timeProvider = new FixedTimeProvider();
        var dashboard = new DashboardViewModel();
        var themeService = new FakeThemeService(preferredTheme, effectiveTheme);
        var settingsStore = new FakeDesktopSettingsStore();
        var settings = new SettingsViewModel(
            themeService,
            settingsStore,
            NullLogger<SettingsViewModel>.Instance);
        var accounts = new AccountsViewModel(
            accountReader,
            new CreateTradingAccountUseCase(accountStore, timeProvider),
            new TradingAccountLifecycleUseCase(accountStore, timeProvider),
            new GetTradingAccountDetailsUseCase(accountReader),
            new UpdateTradingAccountUseCase(accountStore, timeProvider),
            new DeleteTradingAccountUseCase(
                accountStore,
                new FakeTradingAccountDeletionStore()),
            new FakeDialogService());
        var instruments = new InstrumentsViewModel(
            instrumentReader,
            new CreateInstrumentUseCase(instrumentStore, timeProvider),
            new InstrumentLifecycleUseCase(instrumentStore, timeProvider),
            new GetInstrumentDetailsUseCase(instrumentReader),
            new UpdateInstrumentUseCase(
                instrumentStore,
                new FakeInstrumentDeletionStore(),
                timeProvider),
            new DeleteInstrumentUseCase(
                instrumentStore,
                new FakeInstrumentDeletionStore()),
            new FakeDialogService());
        var setups = new TradingSetupsViewModel(
            setupReader,
            new CreateTradingSetupUseCase(setupStore, setupNameChecker, timeProvider),
            new TradingSetupLifecycleUseCase(setupStore, timeProvider),
            new GetTradingSetupDetailsUseCase(setupReader),
            new UpdateTradingSetupUseCase(setupStore, setupNameChecker, timeProvider),
            new DeleteTradingSetupUseCase(
                setupStore,
                new FakeTradingSetupDeletionStore()),
            new FakeDialogService());
        var mistakes = new TradingMistakesViewModel(mistakeReader,
            new CreateTradingMistakeUseCase(mistakeStore, mistakeChecker, timeProvider),
            new TradingMistakeLifecycleUseCase(mistakeStore, timeProvider),
            new GetTradingMistakeDetailsUseCase(mistakeReader),
            new UpdateTradingMistakeUseCase(mistakeStore, mistakeChecker, timeProvider),
            new DeleteTradingMistakeUseCase(
                mistakeStore,
                new FakeTradingMistakeDeletionStore()),
            new FakeDialogService());
        var trades = new TradesViewModel(
            tradeReferenceDataReader,
            setupReader,
            mistakeReader,
            new FakeTradeMistakeReader(),
            tradeListReader,
            tradeDetailReader,
            new CreateManualTradeUseCase(
                accountStore,
                instrumentStore,
                setupStore,
                tradeStore,
                timeProvider),
            new SetTradeTradingSetupUseCase(
                new FakeTradeMutationStore(),
                setupStore,
                timeProvider),
            new AssignTradeMistakeUseCase(
                tradeExistenceReader,
                mistakeStore,
                new FakeTradeMistakeStore(),
                timeProvider),
            new RemoveTradeMistakeUseCase(new FakeTradeMistakeStore()),
            new CloseManualTradeUseCase(
                new FakeTradeMutationStore(),
                timeProvider),
            tradeScreenshotReader,
            new AddTradeScreenshotUseCase(
                tradeExistenceReader,
                tradeScreenshotFileStorage,
                tradeScreenshotStore,
                timeProvider),
            tradeScreenshotFilePicker,
            tradeScreenshotContentReader,
            tradeScreenshotImageDecoder,
            new DeleteTradeScreenshotUseCase(
                new FakeTradeScreenshotDeletionStore(),
                tradeScreenshotFileStorage),
            new FakeTradeScreenshotDeleteConfirmation());
        var main = new MainWindowViewModel(
            dashboard,
            accounts,
            instruments,
            mistakes,
            setups,
            trades,
            settings,
            themeService);

        return new ViewModelFixture(
            main,
            dashboard,
            accounts,
            instruments,
            mistakes,
            setups,
            trades,
            settings,
            accountReader,
            instrumentReader,
            mistakeReader,
            setupReader,
            tradeReferenceDataReader,
            tradeListReader,
            tradeDetailReader,
            tradeScreenshotReader,
            themeService,
            settingsStore);
    }

    private static TradeDetail CreateTradeDetail(TradeListItem listItem)
    {
        return new TradeDetail(
            listItem.Id,
            listItem.TradingAccountId,
            listItem.TradingAccountName,
            listItem.InstrumentId,
            listItem.InstrumentSymbol,
            "Nasdaq-100 E-mini",
            null,
            null,
            null,
            listItem.Direction,
            listItem.Status,
            listItem.OpenedAtUtc,
            listItem.ClosedAtUtc,
            listItem.OpenQuantity,
            listItem.AverageEntryPrice,
            listItem.AverageExitPrice,
            listItem.TotalCosts,
            listItem.GrossPnL,
            listItem.NetPnL,
            20m,
            listItem.Currency,
            []);
    }

    private sealed record ViewModelFixture(
        MainWindowViewModel Main,
        DashboardViewModel Dashboard,
        AccountsViewModel Accounts,
        InstrumentsViewModel Instruments,
        TradingMistakesViewModel Mistakes,
        TradingSetupsViewModel Setups,
        TradesViewModel Trades,
        SettingsViewModel Settings,
        FakeTradingAccountReader AccountReader,
        FakeInstrumentReader InstrumentReader,
        FakeTradingMistakeReader MistakeReader,
        FakeTradingSetupReader SetupReader,
        FakeManualTradeReferenceDataReader TradeReferenceDataReader,
        FakeTradeListReader TradeListReader,
        FakeTradeDetailReader TradeDetailReader,
        FakeTradeScreenshotReader TradeScreenshotReader,
        FakeThemeService ThemeService,
        FakeDesktopSettingsStore SettingsStore);
}
