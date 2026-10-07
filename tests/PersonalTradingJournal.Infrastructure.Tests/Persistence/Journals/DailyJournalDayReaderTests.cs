using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Infrastructure.Journals;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Journals;

public sealed partial class DailyJournalRepositoryTests
{
    [Theory]
    [InlineData(2026, 10, 1)]
    [InlineData(2026, 3, 8)]
    [InlineData(2026, 11, 1)]
    public async Task DayJournalsMatchIndicatorDatesAndAggregateAllScopesWithoutTrades(int year, int month, int day)
    {
        await using var database = await ReaderTestDatabase.CreateAsync();
        var repository = GetRepository(database);
        Guid active = await SeedAccountAsync(database, "P 21");
        Guid inactive = await SeedAccountAsync(database, "Archive", false);
        var date = new DateOnly(year, month, day);
        foreach (var d in new[] { date.AddDays(-1), date, date.AddDays(1) })
        {
            await repository.CreateAsync(new(d, null, "All scopes", false, CompleteReview));
            await repository.CreateAsync(new(d, active, "Account text", false));
            await repository.CreateAsync(new(d, inactive, "Draft"));
        }
        var reader = new DailyJournalRepository(await InterceptingFactory.CreateAsync(database, new RejectSaveInterceptor()));
        var rows = await reader.GetDayAsync(date);
        Assert.Equal(3, rows.Count);
        Assert.Null(rows[0].Entry.TradingAccountId);
        Assert.All(rows, r => Assert.Equal(date, r.Entry.TradingDate));
        Assert.Equal(CompleteReview, rows[0].Entry.Review);
        Assert.Equal(rows.Select(r => r.Entry.Id), (await reader.GetDayAsync(date)).Select(r => r.Entry.Id));
        var statuses = await new DailyJournalStatusReader(database.ContextFactory).GetAsync(date, date);
        Assert.Equal(statuses.Select(s => s.JournalId).Order(), rows.Select(r => r.Entry.Id).Order());
        Assert.Equal("P 21", Assert.Single(await reader.GetDayAsync(date, active)).AccountName);
        Assert.Equal(DailyJournalAccountState.Inactive, Assert.Single(await reader.GetDayAsync(date, inactive)).AccountState);
        Assert.Empty(await reader.GetDayAsync(date.AddDays(2)));
        await using var db = await database.ContextFactory.CreateDbContextAsync();
        Assert.Empty(await db.Trades.ToArrayAsync());
        Assert.Equal(9, await db.DailyJournals.CountAsync());
        Assert.Equal(9, await db.DailyJournalRevisions.CountAsync());
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.GetDayAsync(date, cancellationToken: cancellation.Token));
        await Assert.ThrowsAsync<ArgumentException>(() => reader.GetDayAsync(date, Guid.Empty));
    }
}
