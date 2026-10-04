using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Imports.Tradovate;
using PersonalTradingJournal.Application.Imports.Topstep;
using PersonalTradingJournal.Infrastructure.Imports.Topstep;
using PersonalTradingJournal.Infrastructure.Imports.Csv;
using System.Text;
using PersonalTradingJournal.Application.Mistakes;
using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Application.Trades;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.Imports;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Theming;
using PersonalTradingJournal.Desktop.ViewModels;
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
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Desktop.Tests.Navigation;

public sealed partial class MainWindowViewModelTests
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
    public void NavigateToImport_UsesRetainedImportViewModelAndLoadsAccountsOnly()
    {
        ViewModelFixture fixture = CreateFixture();

        fixture.Main.NavigateCommand.Execute(NavigationDestination.Import);

        Assert.Equal(NavigationDestination.Import, fixture.Main.CurrentDestination);
        Assert.Equal("Import", fixture.Main.PageTitle);
        Assert.Same(fixture.Import, fixture.Main.CurrentContentViewModel);
        Assert.Equal(1, fixture.AccountReader.CallCount);
        Assert.Equal(ImportWorkflowPhase.Idle, fixture.Import.Phase);
        Assert.Null(fixture.Import.SelectedFileName);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DashboardViewWaitsForTradeListAndOpensExactRowOutsideCurrentPage(bool navigateAway)
    {
        ViewModelFixture fixture = CreateFixture();
        fixture.TradeListReader.HoldRead = true;
        var row = new TradeListItem(Guid.NewGuid(), Guid.NewGuid(), "Recent", Guid.NewGuid(), "RECENT",
            TradeDirection.Long, TradeStatus.Open, DateTimeOffset.UtcNow, null, 1m, 100m, null, null, null, null, "EUR", 1m);
        fixture.TradeDetailReader.EnqueueResult(CreateTradeDetail(row));
        var dashboard = Assert.IsType<DashboardViewModel>(fixture.Main.CurrentContentViewModel);
        Task action = dashboard.ViewTradeCommand.ExecuteAsync(row);
        await fixture.TradeListReader.ReadStarted;
        Assert.False(action.IsCompleted);
        if (navigateAway) fixture.Main.NavigateCommand.Execute(NavigationDestination.Accounts);
        fixture.TradeListReader.ReleaseRead();
        await action;
        if (navigateAway)
        {
            Assert.Equal(NavigationDestination.Accounts, fixture.Main.CurrentDestination);
            Assert.Empty(fixture.TradeDetailReader.RequestedTradeIds);
            return;
        }
        Assert.Equal(NavigationDestination.Trades, fixture.Main.CurrentDestination);
        Assert.Equal(row.Id, fixture.Trades.SelectedTradeDetail!.Id);
        Assert.Equal(row.Id, Assert.Single(fixture.TradeDetailReader.RequestedTradeIds));
        Assert.DoesNotContain(fixture.Trades.RecentTrades, item => item.Id == row.Id);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CalendarViewStaysInlineAndDiscardsDetailsAfterNavigationAway(bool navigateAway)
    {
        var reader = new FakeTradingCalendarDayReader
        {
            Handler = (q, ct) => Task.FromResult(TradingCalendarDayDetails.Create(q.Date,
                [CalendarPage.CalendarDayDetailsTests.Row(q.Date, 10m, 9m)], ct))
        };
        var inlineReader = new FakeTradeDetailReader { HoldRead = true };
        var inlineEditor = Trades.TradesViewModelTests.CreateViewModel(tradeDetailReader: inlineReader);
        ViewModelFixture fixture = CreateFixture(calendarDayReader: reader, calendarEditor: inlineEditor);
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Calendar);
        var calendar = Assert.IsType<CalendarViewModel>(fixture.Main.CurrentContentViewModel);
        await calendar.LoadTask;
        await calendar.SelectDayCommand.ExecuteAsync(calendar.Weeks[0].Days[5]);
        TradeListItem row = Assert.Single(calendar.DayTrades).Trade;
        inlineReader.EnqueueResult(CreateTradeDetail(row));
        Task view = calendar.ViewTradeCommand.ExecuteAsync(row);
        await inlineReader.ReadStarted.WaitAsync(TimeSpan.FromSeconds(10));
        if (navigateAway) fixture.Main.NavigateCommand.Execute(NavigationDestination.Accounts);
        inlineReader.ReleaseRead();
        await view;
        if (navigateAway)
        {
            Assert.Null(inlineEditor.SelectedTradeDetail);
            Assert.Equal(NavigationDestination.Accounts, fixture.Main.CurrentDestination);
            return;
        }
        Assert.Equal(NavigationDestination.Calendar, fixture.Main.CurrentDestination);
        Assert.Equal(row.Id, inlineEditor.SelectedTradeDetail!.Id);
        Assert.Equal(row.Id, Assert.Single(inlineReader.RequestedTradeIds));
        Assert.Empty(fixture.TradeDetailReader.RequestedTradeIds);
        Assert.DoesNotContain(fixture.Trades.RecentTrades, t => t.Id == row.Id);
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
    public async Task CalendarNavigationUsesConcreteViewModelAndLoadsCurrentNewYorkMonth()
    {
        ViewModelFixture fixture = CreateFixture();
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Calendar);
        var calendar = Assert.IsType<CalendarViewModel>(fixture.Main.CurrentContentViewModel);
        await calendar.LoadTask;
        Assert.Equal(NavigationDestination.Calendar, fixture.Main.CurrentDestination);
        Assert.Equal("Calendar", fixture.Main.PageTitle);
        Assert.NotNull(calendar.MonthData);
        Assert.InRange(calendar.Weeks.Count, 4, 6);
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Notebook);
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Calendar);
        Assert.Same(calendar, fixture.Main.CurrentContentViewModel);
        await calendar.LoadTask;
        Assert.NotNull(calendar.MonthData);
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

    [Theory]
    [InlineData(TopstepImportStatus.Imported, 1, 2, 2)]
    [InlineData(TopstepImportStatus.Imported, 0, 2, 1)]
    [InlineData(TopstepImportStatus.NoChanges, 0, 1, 1)]
    [InlineData(TopstepImportStatus.Blocked, 0, 1, 1)]
    public async Task TopstepCommitGenerationsInvalidateOnlyAffectedRetainedDataBeforeReuse(
        TopstepImportStatus status, int created, int expectedTradeReads, int expectedInstrumentReads)
    {
        var changes = new TopstepImportChangeTracker();
        ViewModelFixture fixture = CreateFixture(topstepChanges: changes);
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Trades);
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Instruments);
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Import);
        var accounts = new FakeTradingAccountReader();
        Guid accountId = Guid.NewGuid();
        accounts.EnqueueDetailResult(new(accountId, "Synthetic Topstep", TradingAccountType.PropFunded,
            "Topstep", null, "USD", null, true, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch));
        var builder = new TopstepImportPreviewBuilder(new TopstepCsvParser(), new TopstepTradeCandidateReconstructor(),
            new(new FakeInstrumentReader(), accounts));
        const string csv = "Id,ContractName,EnteredAt,ExitedAt,EntryPrice,ExitPrice,Fees,PnL,Size,Type,TradeDay,TradeDuration,Commissions\n" +
            "SYNTH-1,MNQZ6,07/10/2026 17:00:00 +03:00,07/10/2026 17:01:00 +03:00,20000,20001,0.72,2,1,Long,07/10/2026 00:00:00 -05:00,00:01:00,0.50";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        TopstepImportPreview preview = await builder.BuildAsync("synthetic.csv", input, accountId,
            TopstepCostInterpretation.SeparateReportedRoundTurnTotalsUsd);
        var useCase = new ImportTopstepTradesUseCase(new TopstepResultStore(new(status, 1, 0, created, [], [], [])), TimeProvider.System, changes);
        using var confirmationSource = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        await useCase.ImportAsync(preview, new(preview.SnapshotFingerprint, preview.CreationProposals.Select(r => r.CanonicalSymbol).ToArray()),
            "synthetic.csv", confirmationSource);
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Trades);
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Instruments);
        Assert.Equal(expectedTradeReads, fixture.TradeListReader.CallCount);
        Assert.Equal(expectedInstrumentReads, fixture.InstrumentReader.CallCount);
    }

    [Theory]
    [InlineData(NavigationDestination.Trades, false, false, false)]
    [InlineData(NavigationDestination.Instruments, false, false, false)]
    [InlineData(NavigationDestination.Trades, true, false, false)]
    [InlineData(NavigationDestination.Instruments, true, false, false)]
    [InlineData(NavigationDestination.Trades, true, true, false)]
    [InlineData(NavigationDestination.Instruments, true, true, false)]
    [InlineData(NavigationDestination.Trades, true, false, true)]
    [InlineData(NavigationDestination.Instruments, true, false, true)]
    public async Task TopstepCommitRefreshesActiveDestinationWithoutNavigationAndDiscardsOldRead(
        NavigationDestination destination, bool delayedRead, bool clearPresentation, bool oldReadFails)
    {
        var (fixture, store) = await CreateDelayedTopstepFixture();
        using var main = fixture.Main;
        await fixture.Trades.EnsureLoadedAsync();
        await fixture.Instruments.EnsureLoadedAsync();
        TradeListItem committedTrade = fixture.Trades.RecentTrades.Single() with { Id = Guid.NewGuid() };
        TradeListItem staleTrade = committedTrade with { Id = Guid.NewGuid() };
        var committedInstrument = new InstrumentListItem(Guid.NewGuid(), "MNQ", "Committed MNQ",
            AssetClass.Futures, "CME", "USD", 0.25m, 0.5m, 2m, true);
        var staleInstrument = committedInstrument with { Id = Guid.NewGuid() };
        var readStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldTradeRead = new TaskCompletionSource<IReadOnlyList<TradeListItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldInstrumentRead = new TaskCompletionSource<IReadOnlyList<InstrumentListItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var publishedIds = new List<Guid>();
        fixture.Trades.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(fixture.Trades.RecentTrades)) return;
            publishedIds.AddRange(fixture.Trades.RecentTrades.Select(t => t.Id));
            if (fixture.Trades.RecentTrades.Any(t => t.Id == committedTrade.Id)) refreshed.TrySetResult();
        };
        fixture.Instruments.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(fixture.Instruments.Instruments)) return;
            publishedIds.AddRange(fixture.Instruments.Instruments.Select(i => i.Id));
            if (fixture.Instruments.Instruments.Any(i => i.Id == committedInstrument.Id)) refreshed.TrySetResult();
        };
        if (destination == NavigationDestination.Trades)
        {
            fixture.Trades.InvalidateLoadedDataAfterExternalImport();
            fixture.TradeListReader.EnqueueBehavior(_ =>
            {
                readStarted.TrySetResult();
                return delayedRead ? oldTradeRead.Task : Task.FromResult<IReadOnlyList<TradeListItem>>([]);
            });
            fixture.TradeListReader.EnqueueResult([committedTrade]);
        }
        else
        {
            fixture.Instruments.InvalidateLoadedDataAfterExternalImport();
            fixture.InstrumentReader.EnqueueBehavior(_ =>
            {
                readStarted.TrySetResult();
                return delayedRead ? oldInstrumentRead.Task : Task.FromResult<IReadOnlyList<InstrumentListItem>>([]);
            });
            fixture.InstrumentReader.EnqueueResult([committedInstrument]);
        }

        Task confirmation = fixture.Import.ConfirmImportCommand.ExecuteAsync(null);
        await store.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        main.NavigateCommand.Execute(destination);
        await readStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        // Model commit completing before the store returns to its caller/dispatcher.
        store.Commit(new(TopstepImportStatus.Imported, 1, 0, 1, [committedTrade.Id], [], [committedInstrument.Id]));
        if (clearPresentation)
        {
            main.NavigateCommand.Execute(NavigationDestination.Import); // Cancels transient presentation.
            Assert.True(store.Token.IsCancellationRequested);
            main.NavigateCommand.Execute(destination);
            Assert.False(fixture.Import.HasPreview);
        }
        store.ReturnResult.TrySetResult();
        await confirmation.WaitAsync(TimeSpan.FromSeconds(10));
        if (delayedRead)
        {
            if (oldReadFails)
            {
                if (destination == NavigationDestination.Trades) oldTradeRead.SetException(new IOException("Old read failed"));
                else oldInstrumentRead.SetException(new IOException("Old read failed"));
            }
            else if (destination == NavigationDestination.Trades) oldTradeRead.SetResult([staleTrade]);
            else oldInstrumentRead.SetResult([staleInstrument]);
        }
        await refreshed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.DoesNotContain(staleTrade.Id, publishedIds);
        Assert.DoesNotContain(staleInstrument.Id, publishedIds);
        if (destination == NavigationDestination.Trades)
            Assert.Equal(committedTrade.Id, Assert.Single(fixture.Trades.RecentTrades).Id);
        else
            Assert.Equal(committedInstrument.Id, Assert.Single(fixture.Instruments.Instruments).Id);
        if (clearPresentation) Assert.Null(fixture.Import.ImportResultStatus);
        int reads = destination == NavigationDestination.Trades ? fixture.TradeListReader.CallCount : fixture.InstrumentReader.CallCount;
        Assert.Equal(3, reads); // Initial retained data, pre-commit read, one post-commit read.
        main.NavigateCommand.Execute(NavigationDestination.Dashboard);
        main.NavigateCommand.Execute(destination);
        Assert.Equal(reads, destination == NavigationDestination.Trades ? fixture.TradeListReader.CallCount : fixture.InstrumentReader.CallCount);
    }

    [Theory]
    [InlineData(NavigationDestination.Trades, "NoChanges")]
    [InlineData(NavigationDestination.Instruments, "NoChanges")]
    [InlineData(NavigationDestination.Trades, "Blocked")]
    [InlineData(NavigationDestination.Instruments, "Blocked")]
    [InlineData(NavigationDestination.Trades, "Rollback")]
    [InlineData(NavigationDestination.Instruments, "Rollback")]
    [InlineData(NavigationDestination.Trades, "Failure")]
    [InlineData(NavigationDestination.Instruments, "Failure")]
    [InlineData(NavigationDestination.Instruments, "ImportedWithoutInstrument")]
    public async Task TopstepNonCommitOrUnaffectedDestinationDoesNotRefresh(NavigationDestination destination, string outcome)
    {
        var (fixture, store) = await CreateDelayedTopstepFixture();
        using var main = fixture.Main;
        Task confirmation = fixture.Import.ConfirmImportCommand.ExecuteAsync(null);
        await store.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        main.NavigateCommand.Execute(destination);
        int reads = destination == NavigationDestination.Trades ? fixture.TradeListReader.CallCount : fixture.InstrumentReader.CallCount;
        if (outcome == "Rollback")
        {
            fixture.Import.CancelOperationCommand.Execute(null);
            store.ReturnResult.SetCanceled(store.Token);
        }
        else if (outcome == "Failure") store.ReturnResult.SetException(new IOException("Transaction rolled back"));
        else
        {
            TopstepImportStatus status = outcome == "ImportedWithoutInstrument" ? TopstepImportStatus.Imported
                : Enum.Parse<TopstepImportStatus>(outcome);
            store.Commit(new(status, status == TopstepImportStatus.Imported ? 1 : 0, 0, 0, [], [], []));
            store.ReturnResult.SetResult();
        }
        await confirmation.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(reads, destination == NavigationDestination.Trades ? fixture.TradeListReader.CallCount : fixture.InstrumentReader.CallCount);
    }

    [Theory]
    [InlineData(NavigationDestination.Trades)]
    [InlineData(NavigationDestination.Instruments)]
    [InlineData(NavigationDestination.Calendar)]
    public async Task BackgroundCommitNotificationRefreshesOnOwningWpfDispatcher(NavigationDestination destination)
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(dispatcher));
            dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    var (fixture, store) = await CreateDelayedTopstepFixture();
                    using var main = fixture.Main;
                    int uiThread = Environment.CurrentManagedThreadId;
                    // Intentionally return the Desktop notification from a worker to exercise marshaling.
                    Task confirmation = Task.Run(() => fixture.Import.ConfirmImportCommand.ExecuteAsync(null));
                    await store.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    main.NavigateCommand.Execute(destination);
                    if (destination == NavigationDestination.Calendar)
                        await Assert.IsType<CalendarViewModel>(main.CurrentContentViewModel).LoadTask;
                    var refreshed = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                    if (destination == NavigationDestination.Trades)
                    {
                        var row = fixture.Trades.RecentTrades.Single() with { Id = Guid.NewGuid() };
                        fixture.TradeListReader.EnqueueResult([row]);
                        fixture.Trades.PropertyChanged += (_, e) =>
                        {
                            if (e.PropertyName == nameof(fixture.Trades.RecentTrades))
                                refreshed.TrySetResult(Environment.CurrentManagedThreadId);
                        };
                    }
                    else if (destination == NavigationDestination.Instruments)
                    {
                        fixture.InstrumentReader.EnqueueResult([new(Guid.NewGuid(), "MNQ", "MNQ", AssetClass.Futures,
                            "CME", "USD", .25m, .5m, 2m, true)]);
                        fixture.Instruments.PropertyChanged += (_, e) =>
                        {
                            if (e.PropertyName == nameof(fixture.Instruments.Instruments))
                                refreshed.TrySetResult(Environment.CurrentManagedThreadId);
                        };
                    }
                    else
                    {
                        var calendar = Assert.IsType<CalendarViewModel>(main.CurrentContentViewModel);
                        calendar.PropertyChanged += (_, e) =>
                        {
                            if (e.PropertyName == nameof(calendar.MonthData) && calendar.MonthData is not null)
                                refreshed.TrySetResult(Environment.CurrentManagedThreadId);
                        };
                    }
                    store.Commit(new(TopstepImportStatus.Imported, 1, 0, 1, [], [], []));
                    store.ReturnResult.SetResult();
                    await confirmation.WaitAsync(TimeSpan.FromSeconds(10));
                    Assert.Equal(uiThread, await refreshed.Task.WaitAsync(TimeSpan.FromSeconds(10)));
                    completed.TrySetResult();
                }
                catch (Exception error) { completed.TrySetException(error); }
                finally { dispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Background); }
            }));
            System.Windows.Threading.Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    [Theory]
    [InlineData(TopstepImportStatus.Imported, 2)]
    [InlineData(TopstepImportStatus.NoChanges, 1)]
    [InlineData(TopstepImportStatus.Blocked, 1)]
    public async Task DashboardActiveDuringTopstepConfirmationRefreshesOnlyAfterCommit(TopstepImportStatus status, int reads)
    {
        var (fixture, store) = await CreateDelayedTopstepFixture();
        Task confirmation = fixture.Import.ConfirmImportCommand.ExecuteAsync(null);
        await store.Started.Task;
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Dashboard);
        var old = new TaskCompletionSource<PersonalTradingJournal.Application.Analytics.DashboardAnalyticsSnapshot>();
        var dashboardReadStarted = new TaskCompletionSource();
        fixture.DashboardReader.Read = (_, _) => { dashboardReadStarted.SetResult(); return old.Task; };
        Task loading = fixture.Dashboard.ActivateAsync(); // DashboardView.Loaded in production.
        await dashboardReadStarted.Task;
        fixture.DashboardReader.Read = (_, _) => Task.FromResult(PersonalTradingJournal.Application.Analytics.DashboardMetricCalculator.Calculate([]));
        store.Commit(new(status, status == TopstepImportStatus.Imported ? 1 : 0, 0, 0, [], [], []));
        store.ReturnResult.SetResult();
        await confirmation;
        old.SetResult(PersonalTradingJournal.Application.Analytics.DashboardMetricCalculator.Calculate([]));
        await loading;
        await fixture.Dashboard.LoadTask;
        Assert.Equal(reads, fixture.DashboardReader.Queries.Count);
        Assert.False(fixture.Dashboard.IsLoading);
    }

    [Theory]
    [InlineData("Imported", false)]
    [InlineData("Imported", true)]
    [InlineData("NoChanges", false)]
    [InlineData("Blocked", false)]
    [InlineData("Rollback", false)]
    [InlineData("Failure", false)]
    public async Task ActiveCalendarRefreshesBothReadsOnlyForCommittedImportAndRejectsPreCommitResponses(
        string outcome, bool cancelledPresentationAfterCommit)
    {
        var reader = new CommitCalendarReader();
        var dayReader = new FakeTradingCalendarDayReader();
        var (fixture, store) = await CreateDelayedTopstepFixture(reader, dayReader);
        using var main = fixture.Main;
        Task confirmation = fixture.Import.ConfirmImportCommand.ExecuteAsync(null);
        await store.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        main.NavigateCommand.Execute(NavigationDestination.Calendar);
        var calendar = Assert.IsType<CalendarViewModel>(main.CurrentContentViewModel);
        await calendar.LoadTask;
        CalendarDayCell selected = calendar.Weeks[0].Days[5];
        await calendar.SelectDayCommand.ExecuteAsync(selected);
        TradingCalendarMonth staleMonth = calendar.MonthData!;
        TradingCalendarDayDetails staleDay = calendar.DayDetails!;
        var oldMonth = new TaskCompletionSource<TradingCalendarMonth>(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldDay = new TaskCompletionSource<TradingCalendarDayDetails>(TaskCreationOptions.RunContinuationsAsynchronously);
        var monthStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dayStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        reader.Read = _ => { monthStarted.TrySetResult(); return oldMonth.Task; };
        dayReader.Handler = (_, _) => { dayStarted.TrySetResult(); return oldDay.Task; };
        Task preCommit = calendar.RefreshAsync();
        await Task.WhenAll(monthStarted.Task, dayStarted.Task).WaitAsync(TimeSpan.FromSeconds(10));
        reader.Read = null;
        TradeListItem committed = CalendarPage.CalendarDayDetailsTests.Row(selected.Date, -285m, null);
        dayReader.Handler = (q, token) => Task.FromResult(TradingCalendarDayDetails.Create(q.Date, [committed], token));
        if (outcome == "Rollback")
        {
            fixture.Import.CancelOperationCommand.Execute(null);
            store.ReturnResult.SetCanceled(store.Token);
        }
        else if (outcome == "Failure") store.ReturnResult.SetException(new IOException("Synthetic rollback"));
        else
        {
            TopstepImportStatus status = Enum.Parse<TopstepImportStatus>(outcome);
            store.Commit(new(status, status == TopstepImportStatus.Imported ? 1 : 0, 0, 0, [], [], []));
            if (cancelledPresentationAfterCommit)
            {
                fixture.Import.ResetTransientState();
                Assert.True(store.Token.IsCancellationRequested);
            }
            store.ReturnResult.SetResult();
        }
        await confirmation.WaitAsync(TimeSpan.FromSeconds(10));
        if (outcome == "Imported") await calendar.LoadTask.WaitAsync(TimeSpan.FromSeconds(10));
        oldMonth.SetResult(staleMonth);
        oldDay.SetResult(staleDay);
        await preCommit;
        Assert.Equal(outcome == "Imported" ? 3 : 2, reader.ReadCount);
        Assert.Equal(outcome == "Imported" ? 3 : 2, dayReader.Calls.Count);
        Assert.Equal(selected.Date, calendar.SelectedDate);
        Assert.Same(selected, calendar.Weeks[0].Days[5]);
        Assert.False(calendar.IsBusy);
        if (outcome == "Imported")
        {
            Assert.Equal(committed.Id, Assert.Single(calendar.DayTrades).Trade.Id);
            Assert.Equal(-285m, Assert.Single(calendar.DaySummaries).Amount);
        }
        else Assert.Empty(calendar.DayTrades);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingManualDeletionRefreshesActiveCalendarAndSelectedDayOnlyAfterCommit(bool cancel)
    {
        var deletion = new FakeTradeDeletionStore { HoldDelete = true };
        var monthReader = new CommitCalendarReader();
        var dayReader = new FakeTradingCalendarDayReader();
        var fixture = CreateFixture(calendarReader: monthReader, calendarDayReader: dayReader,
            tradeDeletionStore: deletion);
        using var main = fixture.Main;
        main.NavigateCommand.Execute(NavigationDestination.Trades);
        await fixture.Trades.EnsureLoadedAsync();
        TradeListItem row = Assert.Single(fixture.Trades.RecentTrades);
        deletion.Result = new(row.Id, []);
        dayReader.Handler = (q, ct) => Task.FromResult(TradingCalendarDayDetails.Create(q.Date, [row], ct));
        Task write = fixture.Trades.DeleteTradeCommand.ExecuteAsync(row);
        await deletion.DeleteStarted.WaitAsync(TimeSpan.FromSeconds(10));
        main.NavigateCommand.Execute(NavigationDestination.Calendar);
        var calendar = Assert.IsType<CalendarViewModel>(main.CurrentContentViewModel);
        await calendar.LoadTask;
        CalendarDayCell selected = calendar.Weeks.SelectMany(w => w.Days).Single(d => d.Date.Day == 10 && d.IsInDisplayedMonth);
        await calendar.SelectDayCommand.ExecuteAsync(selected);
        Assert.Equal(row.Id, Assert.Single(calendar.DayTrades).Trade.Id);
        dayReader.Handler = (q, ct) => Task.FromResult(TradingCalendarDayDetails.Create(q.Date, [], ct));
        if (cancel) fixture.Trades.DeleteTradeCommand.Cancel();
        else deletion.ReleaseDelete();
        if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => write);
        else await write;
        await calendar.LoadTask;
        Assert.Equal(cancel ? 1 : 2, monthReader.ReadCount);
        Assert.Equal(cancel ? 1 : 2, dayReader.Calls.Count);
        Assert.Equal(selected.Date, calendar.SelectedDate);
        Assert.Equal(!cancel, calendar.IsSelectedDayEmpty);
        if (cancel) Assert.Equal(row.Id, Assert.Single(calendar.DayTrades).Trade.Id);
        else Assert.Empty(calendar.DayTrades);
    }

    private sealed class CommitCalendarReader : ITradingCalendarReader
    {
        public int ReadCount;
        public Func<TradingCalendarQuery, Task<TradingCalendarMonth>>? Read { get; set; }
        public Task<TradingCalendarMonth> GetAsync(TradingCalendarQuery query, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref ReadCount);
            return Read?.Invoke(query) ?? new EmptyCalendarReader().GetAsync(query, cancellationToken);
        }
    }

    private static async Task<(ViewModelFixture Fixture, DelayedTopstepStore Store)> CreateDelayedTopstepFixture(
        ITradingCalendarReader? calendarReader = null, ITradingCalendarDayReader? calendarDayReader = null)
    {
        var changes = new TopstepImportChangeTracker();
        var store = new DelayedTopstepStore();
        var accountReader = new FakeTradingAccountReader();
        Guid id = Guid.NewGuid();
        accountReader.EnqueueResult([new(id, "Synthetic Topstep", TradingAccountType.PropFunded, "Topstep", null, "USD", null, true)]);
        accountReader.EnqueueResult([new(id, "Synthetic Topstep", TradingAccountType.PropFunded, "Topstep", null, "USD", null, true)]);
        accountReader.EnqueueDetailResult(new(id, "Synthetic Topstep", TradingAccountType.PropFunded,
            "Topstep", null, "USD", null, true, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch));
        var instruments = new FakeInstrumentReader();
        var preparation = new TradovateImportPreparationService(accountReader);
        var vm = new ImportViewModel(accountReader, new NeverCalledTradovateCsvParser(),
            new NeverCalledTradovateExecutionReconstructor(), new TradovateInstrumentResolver(instruments),
            preparation, new TradovateImportPreviewBuilder(), new SyntheticTopstepPicker(),
            new ImportTradovateTradesUseCase(preparation, new NeverCalledTradovateImportStore(), TimeProvider.System),
            new FakeDialogService { ConfirmationResult = true }, new ImportCsvFormatDetector(),
            new TopstepCsvParser(), new TopstepTradeCandidateReconstructor(),
            new TopstepImportPreviewBuilder(new TopstepCsvParser(), new TopstepTradeCandidateReconstructor(), new(instruments, accountReader)),
            new ImportTopstepTradesUseCase(store, TimeProvider.System, changes));
        ViewModelFixture fixture = CreateFixture(topstepChanges: changes, importViewModel: vm,
            calendarReader: calendarReader, calendarDayReader: calendarDayReader);
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Import);
        await vm.EnsureLoadedAsync();
        vm.SelectedSource = vm.Sources.Single(s => s.Name == "TopstepX");
        await vm.SelectCsvCommand.ExecuteAsync(null);
        vm.SelectedAccount = Assert.Single(vm.Accounts);
        await vm.BuildPreviewCommand.ExecuteAsync(null);
        Assert.True(vm.ConfirmImportCommand.CanExecute(null));
        return (fixture, store);
    }

    private sealed class DelayedTopstepStore : ITopstepImportStore
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReturnResult { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken Token { get; private set; }
        private TopstepImportResult? _committedResult;
        public void Commit(TopstepImportResult result) => _committedResult = result;
        public async Task<TopstepImportResult> ImportAsync(TopstepImportRequest request, CancellationToken cancellationToken = default)
        {
            Token = cancellationToken;
            Started.SetResult();
            // Deliberately do not cancel the committed result when presentation is subsequently cancelled.
            await ReturnResult.Task;
            return _committedResult!;
        }
    }

    private sealed class SyntheticTopstepPicker : ITradovateCsvFilePicker
    {
        public TradovateCsvFileSelection Pick() => new("synthetic.csv", Open(), Open);
        private static Stream Open() => new MemoryStream(Encoding.UTF8.GetBytes(
            "Id,ContractName,EnteredAt,ExitedAt,EntryPrice,ExitPrice,Fees,PnL,Size,Type,TradeDay,TradeDuration,Commissions\n" +
            "SYNTH-1,MNQZ6,07/10/2026 17:00:00 +03:00,07/10/2026 17:01:00 +03:00,20000,20001,0.72,2,1,Long,07/10/2026 00:00:00 -05:00,00:01:00,0.50"));
    }


    private sealed class TopstepResultStore(TopstepImportResult result) : ITopstepImportStore
    {
        public Task<TopstepImportResult> ImportAsync(TopstepImportRequest request, CancellationToken cancellationToken = default) => Task.FromResult(result);
    }

    private sealed class EmptyCalendarReader : ITradingCalendarReader
    {
        public Task<TradingCalendarMonth> GetAsync(TradingCalendarQuery query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DateOnly[] dates = Enumerable.Range(0, query.GridEnd.DayNumber - query.GridStart.DayNumber + 1)
                .Select(offset => query.GridStart.AddDays(offset)).ToArray();
            return Task.FromResult(new TradingCalendarMonth(query.MonthStart, query.GridStart,
                query.GridEnd, dates, []));
        }
    }

    private static ViewModelFixture CreateFixture(
        AppTheme preferredTheme = AppTheme.System,
        AppTheme? effectiveTheme = null,
        TopstepImportChangeTracker? topstepChanges = null,
        ImportViewModel? importViewModel = null,
        ITradingCalendarDayReader? calendarDayReader = null,
        ITradingCalendarReader? calendarReader = null,
        FakeTradeDeletionStore? tradeDeletionStore = null,
        TradesViewModel? calendarEditor = null,
        JournalViewModel? journalViewModel = null)
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
            "USD",
            Size: 2.5m);
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
        var dashboardReader = new FakeDashboardAnalyticsReader();
        var dashboard = new DashboardViewModel(dashboardReader, TimeProvider.System, new FakeTradeListReader(), new FakeTradingAccountReader());
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
        var import = importViewModel ?? new ImportViewModel(
            accountReader,
            new NeverCalledTradovateCsvParser(),
            new NeverCalledTradovateExecutionReconstructor(),
            new TradovateInstrumentResolver(instrumentReader),
            new TradovateImportPreparationService(accountReader),
            new TradovateImportPreviewBuilder(),
            new EmptyTradovateCsvFilePicker(),
            new ImportTradovateTradesUseCase(
                new TradovateImportPreparationService(accountReader),
                new NeverCalledTradovateImportStore(),
                timeProvider),
            new FakeDialogService(), new TradovateOnlyFormatDetector(), null!, null!, null!, null!);
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
            new FakeTradeScreenshotDeleteConfirmation(),
            deleteTradeUseCase: tradeDeletionStore is null ? null : new DeleteTradeUseCase(tradeDeletionStore, tradeScreenshotFileStorage),
            dialogService: new FakeDialogService { ConfirmationResult = true });
        var main = new MainWindowViewModel(
            dashboard,
            new CalendarViewModel(calendarReader ?? new EmptyCalendarReader(), timeProvider, calendarDayReader ?? new FakeTradingCalendarDayReader(), new FakeTradingAccountReader(), tradeEditor: calendarEditor),
            journalViewModel ?? new JournalViewModel(new NavigationJournalRepository(), new FakeTradingAccountReader(), new FakeDialogService(), timeProvider),
            accounts,
            instruments,
            import,
            mistakes,
            setups,
            trades,
            settings,
            themeService,
            topstepChanges);

        return new ViewModelFixture(
            main,
            dashboard,
            accounts,
            instruments,
            import,
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
            settingsStore,
            dashboardReader);
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
        ImportViewModel Import,
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
        FakeDesktopSettingsStore SettingsStore,
        FakeDashboardAnalyticsReader DashboardReader);

    private sealed class NeverCalledTradovateCsvParser : ITradovateCsvParser
    {
        public Task<TradovateCsvParseResult> ParseAsync(
            Stream source,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The navigation test must not parse a CSV.");
    }

    private sealed class NeverCalledTradovateExecutionReconstructor :
        ITradovateExecutionReconstructor
    {
        public TradovateExecutionReconstructionResult Reconstruct(
            TradovateCsvParseResult parseResult,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The navigation test must not reconstruct executions.");
    }

    private sealed class NeverCalledTradovateImportStore : ITradovateImportStore
    {
        public Task<TradovateImportResult> ImportAsync(
            TradovateImportRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The navigation test must not import trades.");
    }

    private sealed class EmptyTradovateCsvFilePicker : ITradovateCsvFilePicker
    {
        public TradovateCsvFileSelection? Pick() => null;
    }
}
