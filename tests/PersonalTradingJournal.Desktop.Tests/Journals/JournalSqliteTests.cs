using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Journals;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Initialization;
using PersonalTradingJournal.Infrastructure.Persistence.Records;
using PersonalTradingJournal.Infrastructure.Storage;

namespace PersonalTradingJournal.Desktop.Tests.Journals;

public sealed class JournalSqliteTests
{
    private static readonly DateOnly TradingDate = new(2026, 10, 4);
    private static readonly DateTimeOffset AuditTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task EmptyTradingDayCanSaveExactTextAndFreshEditorsReadCommittedEditsAndHistory()
    {
        await using JournalTestDatabase database = await JournalTestDatabase.CreateAsync();
        JournalViewModel first = await database.OpenEditorAsync(TradingDate);
        const string original = "  Premarket\r\n\nПлан: търпение. 📈\t  ";
        const string revised = "\tReview\n\nKeep patience.\r\nСледващ ден: дисциплина.  ";

        Assert.False(first.IsExisting);
        Assert.False(first.IsDirty);
        Assert.Equal(string.Empty, first.Text);
        first.OpenEditorCommand.Execute(null);
        first.Text = original;
        await first.SaveCommand.ExecuteAsync(null);

        Assert.Null(first.ErrorMessage);
        Assert.True(first.IsExisting);
        Assert.False(first.IsDirty);
        Assert.Equal(1L, first.Revision);
        Assert.Equal(original, first.Text);
        first.Deactivate();

        JournalViewModel reopened = await database.OpenEditorAsync(TradingDate);
        Assert.Equal(original, reopened.Text);
        Assert.Equal(1L, reopened.Revision);
        await reopened.ReopenReviewCommand.ExecuteAsync(null);
        reopened.Text = revised;
        await reopened.SaveCommand.ExecuteAsync(null);

        Assert.Null(reopened.ErrorMessage);
        Assert.False(reopened.IsDirty);
        Assert.Equal(2L, reopened.Revision);
        reopened.Deactivate();
        JournalViewModel latest = await database.OpenEditorAsync(TradingDate);
        Assert.Equal(revised, latest.Text);
        Assert.Equal(2L, latest.Revision);
        Assert.False(latest.IsDirty);

        DailyJournalDetails persisted = Assert.IsType<DailyJournalDetails>(
            await database.Repository.GetAsync(TradingDate));
        Assert.Equal(TradingDate, persisted.Entry.TradingDate);
        Assert.Null(persisted.Entry.TradingAccountId);
        Assert.Equal(revised, persisted.Entry.Text);
        Assert.Collection(await database.Repository.GetHistoryAsync(persisted.Entry.Id),
            revision =>
            {
                Assert.Equal(1L, revision.Revision);
                Assert.Equal(original, revision.Text);
            },
            revision =>
            {
                Assert.Equal(2L, revision.Revision);
                Assert.Equal(revised, revision.Text);
                Assert.False(revision.IsDraft);
            });
        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        Assert.Equal(1, await context.DailyJournals.CountAsync());
        Assert.Equal(2, await context.DailyJournalRevisions.CountAsync());
        Assert.Empty(await context.Trades.ToArrayAsync());
        Assert.Empty(await context.TradeExecutions.ToArrayAsync());
        Assert.Empty(await context.TradeBrowse.ToArrayAsync());
    }

    [Fact]
    public async Task DateAndAllOrAccountScopesRemainIndependentIncludingInactiveAccountsWithoutChangingTrades()
    {
        await using JournalTestDatabase database = await JournalTestDatabase.CreateAsync();
        var active = new TradingAccount("Journal active", TradingAccountType.Personal, null, null, "USD", 0m, AuditTime);
        var inactive = new TradingAccount("Journal archive", TradingAccountType.Personal, null, null, "USD", 0m, AuditTime);
        inactive.Deactivate(AuditTime.AddDays(1));
        var accountStore = database.Provider.GetRequiredService<ITradingAccountStore>();
        await accountStore.AddAsync(active);
        await accountStore.AddAsync(inactive);
        var instrument = new Instrument("JRN", "Journal synthetic instrument", AssetClass.Futures, "CME", "USD", 1m, 1m, AuditTime);
        await database.Provider.GetRequiredService<IInstrumentStore>().AddAsync(instrument);
        Guid tradeId = Guid.NewGuid();
        DateTimeOffset close = new(2026, 10, 4, 16, 0, 0, TimeSpan.Zero);
        var trade = Trade.Rehydrate(tradeId, active.Id, instrument.Id, new(1m, "USD"), null,
            [new TradeExecution(tradeId, 1, close.AddHours(-1), ExecutionSide.Buy, 2m, 100m, 1m, 0.5m, "entry", "order-1", "JRN"),
             new TradeExecution(tradeId, 2, close, ExecutionSide.Sell, 2m, 104m, 1m, 0.5m, "exit", "order-2", "JRN")], AuditTime, AuditTime);
        await database.Provider.GetRequiredService<ITradeStore>().AddAsync(trade);
        TradeDataSnapshot before = await database.ReadTradeDataAsync();
        Assert.Single(before.Trades);
        Assert.Equal(2, before.Executions.Length);
        Assert.Single(before.Browse);
        JournalViewModel editor = await database.OpenEditorAsync(TradingDate);

        editor.OpenEditorCommand.Execute(null);
        editor.Text = "All accounts for the day";
        await editor.SaveCommand.ExecuteAsync(null);
        Assert.Null(editor.ErrorMessage);
        editor.SelectedAccount = editor.Accounts.Single(account => account.Id == active.Id);
        await editor.LoadTask;
        editor.OpenEditorCommand.Execute(null);
        Assert.False(editor.IsExisting);
        Assert.Equal(string.Empty, editor.Text);
        editor.Text = "Active account only";
        await editor.SaveCommand.ExecuteAsync(null);
        Assert.Null(editor.ErrorMessage);
        editor.SelectedAccount = editor.Accounts.Single(account => account.Id == inactive.Id);
        await editor.LoadTask;
        editor.OpenEditorCommand.Execute(null);
        Assert.True(editor.SelectedAccount.IsAvailable);
        Assert.False(editor.IsExisting);
        Assert.Equal(string.Empty, editor.Text);
        editor.Text = "Inactive account history";
        await editor.SaveCommand.ExecuteAsync(null);
        Assert.Null(editor.ErrorMessage);
        editor.SelectedDate = TradingDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        await editor.LoadTask;
        editor.OpenEditorCommand.Execute(null);
        Assert.False(editor.IsExisting);
        Assert.Equal(string.Empty, editor.Text);
        editor.Text = "Inactive account next day";
        await editor.SaveCommand.ExecuteAsync(null);
        Assert.Null(editor.ErrorMessage);

        editor.SelectedAccount = editor.Accounts.Single(account => account.Id is null);
        await editor.LoadTask;
        editor.OpenEditorCommand.Execute(null);
        Assert.False(editor.IsExisting);
        Assert.Equal(string.Empty, editor.Text);
        editor.SelectedDate = TradingDate.ToDateTime(TimeOnly.MinValue);
        await editor.LoadTask;
        editor.OpenEditorCommand.Execute(null);
        Assert.Equal("All accounts for the day", editor.Text);
        editor.SelectedAccount = editor.Accounts.Single(account => account.Id == active.Id);
        await editor.LoadTask;
        editor.OpenEditorCommand.Execute(null);
        Assert.Equal("Active account only", editor.Text);
        editor.SelectedAccount = editor.Accounts.Single(account => account.Id == inactive.Id);
        await editor.LoadTask;
        editor.OpenEditorCommand.Execute(null);
        Assert.Equal("Inactive account history", editor.Text);
        editor.SelectedDate = TradingDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        await editor.LoadTask;
        editor.OpenEditorCommand.Execute(null);
        Assert.Equal("Inactive account next day", editor.Text);

        DailyJournalDetails allJournal = Assert.IsType<DailyJournalDetails>(await database.Repository.GetAsync(TradingDate));
        DailyJournalDetails activeJournal = Assert.IsType<DailyJournalDetails>(await database.Repository.GetAsync(TradingDate, active.Id));
        DailyJournalDetails inactiveJournal = Assert.IsType<DailyJournalDetails>(await database.Repository.GetAsync(TradingDate, inactive.Id));
        DailyJournalDetails nextDayJournal = Assert.IsType<DailyJournalDetails>(await database.Repository.GetAsync(TradingDate.AddDays(1), inactive.Id));
        Assert.Equal(4, new[] { allJournal, activeJournal, inactiveJournal, nextDayJournal }.Select(journal => journal.Entry.Id).Distinct().Count());
        Assert.Equal(DailyJournalAccountState.AllAccounts, allJournal.AccountState);
        Assert.Equal(DailyJournalAccountState.Active, activeJournal.AccountState);
        Assert.Equal(DailyJournalAccountState.Inactive, inactiveJournal.AccountState);
        Assert.Null(await database.Repository.GetAsync(TradingDate.AddDays(1)));
        Assert.Equivalent(before, await database.ReadTradeDataAsync(), strict: true);
        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        Assert.Equal(4, await context.DailyJournals.CountAsync());
        Assert.Equal(4, await context.DailyJournalRevisions.CountAsync());
    }

    [Fact]
    public async Task StaleEditorPreservesLocalDraftUntilExplicitReloadIsConfirmed()
    {
        await using JournalTestDatabase database = await JournalTestDatabase.CreateAsync();
        await database.Repository.CreateAsync(new(TradingDate, null, "Initial saved journal", true));
        JournalViewModel first = await database.OpenEditorAsync(TradingDate);
        first.OpenEditorCommand.Execute(null);
        Assert.Null(first.ErrorMessage);
        var dialogs = new FakeDialogService();
        JournalViewModel stale = await database.OpenEditorAsync(TradingDate, dialogs: dialogs);
        Assert.Equal(1L, stale.Revision);
        stale.OpenEditorCommand.Execute(null);
        stale.Text = "  Keep this local draft.\r\n\t ";
        first.OpenEditorCommand.Execute(null);
        first.Text = "Newer saved journal";
        await first.SaveCommand.ExecuteAsync(null);
        Assert.Equal(2L, first.Revision);

        await stale.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(stale.ErrorMessage);
        Assert.Equal("  Keep this local draft.\r\n\t ", stale.Text);
        Assert.True(stale.IsDirty);
        Assert.Equal(1L, stale.Revision);
        Assert.False(stale.SaveCommand.CanExecute(null));
        DailyJournalDetails saved = Assert.IsType<DailyJournalDetails>(await database.Repository.GetAsync(TradingDate));
        Assert.Equal("Newer saved journal", saved.Entry.Text);
        Assert.Equal(2L, saved.Entry.Revision);
        Assert.Equal(2, (await database.Repository.GetHistoryAsync(saved.Entry.Id)).Count);

        dialogs.ConfirmationResult = false;
        await stale.ReloadCommand.ExecuteAsync(null);
        Assert.NotNull(dialogs.ConfirmationRequest);
        Assert.Equal("  Keep this local draft.\r\n\t ", stale.Text);
        Assert.True(stale.IsDirty);
        Assert.Equal(1L, stale.Revision);

        dialogs.ConfirmationResult = true;
        await stale.ReloadCommand.ExecuteAsync(null);
        Assert.Null(stale.ErrorMessage);
        Assert.Equal("Newer saved journal", stale.Text);
        Assert.Equal(2L, stale.Revision);
        Assert.False(stale.IsDirty);
        Assert.Equal(2, (await database.Repository.GetHistoryAsync(saved.Entry.Id)).Count);
    }

    internal sealed record TradeDataSnapshot(TradeRecord[] Trades, TradeExecutionRecord[] Executions, TradeBrowseRecord[] Browse);

    internal sealed class JournalTestDatabase : IAsyncDisposable
    {
        private readonly string _root;
        private readonly LocalApplicationPaths _paths;
        private readonly List<JournalViewModel> _editors = [];
        private readonly List<JournalTradeContextViewModel> _tradeContexts = [];

        private JournalTestDatabase(string root, LocalApplicationPaths paths, ServiceProvider provider)
        {
            _root = root;
            _paths = paths;
            Provider = provider;
        }

        public ServiceProvider Provider { get; }
        public IDailyJournalRepository Repository => Provider.GetRequiredService<IDailyJournalRepository>();
        public IDbContextFactory<JournalDbContext> ContextFactory => Provider.GetRequiredService<IDbContextFactory<JournalDbContext>>();

        public static async Task<JournalTestDatabase> CreateAsync()
        {
            string root = Path.Combine(Path.GetTempPath(), $"PTJ-Journal-Editor-{Guid.NewGuid():N}");
            var paths = new LocalApplicationPaths(root);
            paths.EnsureDirectoriesExist();
            ServiceProvider provider = new ServiceCollection().AddPersistence(paths).BuildServiceProvider();
            var database = new JournalTestDatabase(root, paths, provider);
            try
            {
                await provider.GetRequiredService<JournalDatabaseInitializer>().InitializeAsync();
                return database;
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public async Task<JournalViewModel> OpenEditorAsync(DateOnly date, Guid? accountId = null, FakeDialogService? dialogs = null)
        {
            var editor = new JournalViewModel(Repository, Provider.GetRequiredService<ITradingAccountReader>(),
                dialogs ?? new FakeDialogService(), CreateTradeContext(), new FixedTimeProvider());
            _editors.Add(editor);
            await editor.ActivateAsync();
            editor.OpenEditorCommand.Execute(null);
            editor.SelectedDate = date.ToDateTime(TimeOnly.MinValue);
            await editor.LoadTask;
            editor.OpenEditorCommand.Execute(null);
            editor.SelectedAccount = editor.Accounts.Single(account => account.Id == accountId);
            await editor.LoadTask;
            editor.OpenEditorCommand.Execute(null);
            await editor.TradeContext.LoadTask;
            return editor;
        }

        public JournalTradeContextViewModel CreateTradeContext()
        {
            var tradeContext = new JournalTradeContextViewModel(Provider.GetRequiredService<ITradingCalendarDayReader>(),
                Provider.GetRequiredService<ITradingAccountReader>());
            _tradeContexts.Add(tradeContext);
            return tradeContext;
        }

        public async Task<TradeDataSnapshot> ReadTradeDataAsync()
        {
            await using JournalDbContext context = await ContextFactory.CreateDbContextAsync();
            return new(await context.Trades.AsNoTracking().OrderBy(trade => trade.Id).ToArrayAsync(),
                await context.TradeExecutions.AsNoTracking().OrderBy(execution => execution.Sequence).ToArrayAsync(),
                await context.TradeBrowse.AsNoTracking().OrderBy(trade => trade.TradeId).ToArrayAsync());
        }

        public async ValueTask DisposeAsync()
        {
            foreach (JournalViewModel editor in _editors) editor.Deactivate();
            foreach (JournalTradeContextViewModel tradeContext in _tradeContexts) tradeContext.Deactivate();
            await Task.WhenAll(_tradeContexts.Select(tradeContext => tradeContext.LoadTask));
            await Provider.DisposeAsync();
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = _paths.DatabasePath,
                ForeignKeys = true,
            }.ToString());
            SqliteConnection.ClearPool(connection);
            Directory.Delete(_root, recursive: true);
        }
    }
}
