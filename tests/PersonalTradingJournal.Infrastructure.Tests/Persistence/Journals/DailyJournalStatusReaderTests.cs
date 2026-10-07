using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Journals;
using PersonalTradingJournal.Infrastructure.Journals;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Journals;

public sealed class DailyJournalStatusReaderTests
{
    private static readonly DateOnly GridStart = new(2026, 8, 31);
    private static readonly DailyReviewAnswers CompleteAnswers = new("Followed plan", "Be patient", "Wait for confirmation");

    [Fact]
    public async Task VisibleGridIncludesBothAdjacentMonthsAndAllScopesUnlessAccountIsSelected()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid active = await SeedAccountAsync(database, "Active", true);
        Guid inactive = await SeedAccountAsync(database, "Historical", false);
        var repository = new DailyJournalRepository(database.ContextFactory);
        DateOnly gridEnd = GridStart.AddDays(34);
        foreach (DateOnly date in new[] { GridStart.AddDays(-1), GridStart, new DateOnly(2026, 9, 15), gridEnd, gridEnd.AddDays(1) })
        {
            await repository.CreateAsync(new(date, null, "All accounts private content"));
            await repository.CreateAsync(new(date, active, "Active private content"));
            await repository.CreateAsync(new(date, inactive, "Historical private content", false, CompleteAnswers));
        }

        var reader = new DailyJournalStatusReader(database.ContextFactory);
        var allAccounts = await reader.GetAsync(GridStart, gridEnd);
        var activeResults = await reader.GetAsync(GridStart, gridEnd, active);
        var inactiveResults = await reader.GetAsync(GridStart, gridEnd, inactive);

        Assert.Equal(9, allAccounts.Count);
        Assert.Equal(new[] { GridStart, new DateOnly(2026, 9, 15), gridEnd }, allAccounts.Select(s => s.TradingDate).Distinct());
        Assert.Equal(allAccounts.Select(s => s.TradingDate).Distinct(), activeResults.Select(s => s.TradingDate));
        Assert.Equal(allAccounts.Select(s => s.TradingDate).Distinct(), inactiveResults.Select(s => s.TradingDate));
        Assert.All(allAccounts.Where(s => s.TradingAccountId != inactive).Concat(activeResults), s => Assert.True(s.IsDraft));
        Assert.All(inactiveResults, s => Assert.False(s.IsDraft));
        Assert.All(activeResults, s => { Assert.Equal(active, s.TradingAccountId); Assert.Contains(s, allAccounts); });
        Assert.All(inactiveResults, s => { Assert.Equal(inactive, s.TradingAccountId); Assert.Contains(s, allAccounts); });
        Assert.Empty(await reader.GetAsync(GridStart, gridEnd, Guid.NewGuid()));
        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        Assert.Empty(await context.Trades.ToArrayAsync());
    }

    [Fact]
    public async Task StatusReflectsNewDraftCompletionAndReopeningOnEveryRead()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        var repository = new DailyJournalRepository(database.ContextFactory);
        var reader = new DailyJournalStatusReader(database.ContextFactory);
        Assert.Empty(await reader.GetAsync(GridStart, GridStart));

        var created = Assert.IsType<DailyJournalDetails>((await repository.CreateAsync(new(GridStart, null, ""))).Journal);
        var draft = Assert.Single(await reader.GetAsync(GridStart, GridStart));
        Assert.Equal(new DailyJournalStatus(created.Entry.Id, GridStart, true, 1), draft);

        await repository.UpdateAsync(new(created.Entry.Id, 1, "Journal", false, CompleteAnswers));
        Assert.Equal(new DailyJournalStatus(created.Entry.Id, GridStart, false, 2), Assert.Single(await reader.GetAsync(GridStart, GridStart)));

        await repository.UpdateAsync(new(created.Entry.Id, 2, "Journal", true, CompleteAnswers));
        Assert.Equal(new DailyJournalStatus(created.Entry.Id, GridStart, true, 3), Assert.Single(await reader.GetAsync(GridStart, GridStart)));
        Assert.Equal(3, (await repository.GetHistoryAsync(created.Entry.Id)).Count);
    }

    [Fact]
    public async Task OneProjectionReadsFullSixWeekGridWithoutTextAnswersOrWrites()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        var repository = new DailyJournalRepository(database.ContextFactory);
        for (int day = 0; day < IDailyJournalStatusReader.MaximumVisibleDays; day++)
            await repository.CreateAsync(new(GridStart.AddDays(day), null, "Private text", false, CompleteAnswers));
        var commands = new StatusCommandInterceptor();
        var factory = await InterceptingFactory.CreateAsync(database, commands, new RejectSaveInterceptor());
        var reader = new DailyJournalStatusReader(factory);

        var statuses = await reader.GetAsync(GridStart, GridStart.AddDays(41));

        Assert.Equal(42, statuses.Count);
        string query = Assert.Single(commands.Queries);
        Assert.Contains("SELECT", query, StringComparison.OrdinalIgnoreCase);
        foreach (string projected in new[] { "Id", "TradingDate", "IsDraft", "Revision" })
            Assert.Contains($"\"{projected}\"", query);
        foreach (string excluded in new[] { "Text", "WentWell", "NeedsImprovement", "NextTradingDay", "CreatedAtUtc", "UpdatedAtUtc" })
            Assert.DoesNotContain($"\"{excluded}\"", query);
        Assert.Equal(1, factory.CreatedContexts);
        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        Assert.Equal(42, await context.DailyJournals.CountAsync());
        Assert.Equal(42, await context.DailyJournalRevisions.CountAsync());
        Assert.Empty(await context.Trades.ToArrayAsync());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(42)]
    [InlineData(500)]
    public async Task InvalidRangeIsRejectedBeforeCreatingContext(int dayOffset)
    {
        var factory = new RejectCreateFactory();
        var reader = new DailyJournalStatusReader(factory);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => reader.GetAsync(GridStart, GridStart.AddDays(dayOffset)));
        Assert.Equal(0, factory.Attempts);
    }

    [Fact]
    public async Task EmptyAccountIdentifierIsNotAllAccountsAndNeverQueries()
    {
        var factory = new RejectCreateFactory();
        var reader = new DailyJournalStatusReader(factory);
        await Assert.ThrowsAsync<ArgumentException>(() => reader.GetAsync(GridStart, GridStart, Guid.Empty));
        Assert.Equal(0, factory.Attempts);
    }

    [Fact]
    public async Task PreCancelledReadDoesNotCreateContext()
    {
        var factory = new RejectCreateFactory();
        var reader = new DailyJournalStatusReader(factory);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            reader.GetAsync(GridStart, GridStart, cancellationToken: new CancellationToken(true)));
        Assert.Equal(0, factory.Attempts);
    }

    [Fact]
    public async Task CancellationDuringQueryLeavesDataIntactAndReaderReusable()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        var repository = new DailyJournalRepository(database.ContextFactory);
        DailyJournalDetails created = Assert.IsType<DailyJournalDetails>((await repository.CreateAsync(new(GridStart, null, "Keep"))).Journal);
        using var cancellation = new CancellationTokenSource();
        var commands = new StatusCommandInterceptor(cancellation);
        var factory = await InterceptingFactory.CreateAsync(database, commands, new RejectSaveInterceptor());
        var reader = new DailyJournalStatusReader(factory);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.GetAsync(GridStart, GridStart, cancellationToken: cancellation.Token));
        commands.CancelOnRead = false;
        Assert.Equal(new DailyJournalStatus(created.Entry.Id, GridStart, true, 1), Assert.Single(await reader.GetAsync(GridStart, GridStart)));
        Assert.Equal(2, factory.CreatedContexts);
        Assert.Equal("Keep", Assert.IsType<DailyJournalDetails>(await repository.GetAsync(GridStart)).Entry.Text);
        Assert.Single(await repository.GetHistoryAsync(created.Entry.Id));
    }

    private static async Task<Guid> SeedAccountAsync(ReaderTestDatabase database, string name, bool isActive)
    {
        var account = new TradingAccountRecord
        {
            Id = Guid.NewGuid(), Name = name, Currency = "USD", AccountType = TradingAccountType.Personal,
            IsActive = isActive, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        context.TradingAccounts.Add(account);
        await context.SaveChangesAsync();
        return account.Id;
    }

    private sealed class InterceptingFactory(DbContextOptions<JournalDbContext> options) : IDbContextFactory<JournalDbContext>
    {
        public int CreatedContexts { get; private set; }
        public JournalDbContext CreateDbContext()
        {
            CreatedContexts++;
            return new(options);
        }

        public static async Task<InterceptingFactory> CreateAsync(ReaderTestDatabase database, params IInterceptor[] interceptors)
        {
            await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
            return new(new DbContextOptionsBuilder<JournalDbContext>()
                .UseSqlite(context.Database.GetConnectionString()).AddInterceptors(interceptors).Options);
        }
    }

    private sealed class RejectCreateFactory : IDbContextFactory<JournalDbContext>
    {
        public int Attempts { get; private set; }
        public JournalDbContext CreateDbContext()
        {
            Attempts++;
            throw new InvalidOperationException("Invalid/cancelled status request must not create a context.");
        }
    }

    private sealed class StatusCommandInterceptor(CancellationTokenSource? cancellation = null) : DbCommandInterceptor
    {
        public List<string> Queries { get; } = [];
        public bool CancelOnRead { get; set; } = cancellation is not null;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Queries.Add(command.CommandText);
            if (CancelOnRead)
            {
                cancellation!.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class RejectSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Status retrieval must not save changes.");
    }
}
