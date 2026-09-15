using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Mistakes;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Mistakes;

public sealed class TradeMistakeReaderTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 16, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetByTradeIdAsyncReturnsEmptyWhenNoAssignmentsExist()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();

        IReadOnlyList<TradeMistakeListItem> result =
            await GetReader(database).GetByTradeIdAsync(Guid.NewGuid());

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByTradeIdAsyncProjectsActiveAndInactiveAssignmentsExactly()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid tradeId = await PersistTradeAsync(database);
        Guid activeCatalogId = Guid.NewGuid();
        Guid inactiveCatalogId = Guid.NewGuid();
        Guid activeAssignmentId = Guid.NewGuid();
        Guid inactiveAssignmentId = Guid.NewGuid();
        DateTimeOffset updatedAtUtc = CreatedAtUtc.AddHours(2);
        await SeedAsync(
            database,
            [
                Mistake(activeCatalogId, "FOMO", true),
                Mistake(inactiveCatalogId, "Moved Stop", false),
            ],
            [
                Association(activeAssignmentId, tradeId, activeCatalogId, null),
                Association(
                    inactiveAssignmentId,
                    tradeId,
                    inactiveCatalogId,
                    "Ignored the original invalidation.",
                    updatedAtUtc),
            ]);

        IReadOnlyList<TradeMistakeListItem> result =
            await GetReader(database).GetByTradeIdAsync(tradeId);

        Assert.Equal(2, result.Count);
        TradeMistakeListItem active = Assert.Single(
            result,
            item => item.Id == activeAssignmentId);
        Assert.Equal(tradeId, active.TradeId);
        Assert.Equal(activeCatalogId, active.TradingMistakeId);
        Assert.Equal("FOMO", active.TradingMistakeName);
        Assert.True(active.IsTradingMistakeActive);
        Assert.Null(active.Note);
        Assert.Equal(CreatedAtUtc, active.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, active.UpdatedAtUtc);

        TradeMistakeListItem inactive = Assert.Single(
            result,
            item => item.Id == inactiveAssignmentId);
        Assert.Equal("Moved Stop", inactive.TradingMistakeName);
        Assert.False(inactive.IsTradingMistakeActive);
        Assert.Equal("Ignored the original invalidation.", inactive.Note);
        Assert.Equal(updatedAtUtc, inactive.UpdatedAtUtc);
    }

    [Fact]
    public async Task GetByTradeIdAsyncOrdersByNameCaseInsensitivelyThenCatalogId()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid tradeId = await PersistTradeAsync(database);
        Guid upperId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        Guid lowerId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        Guid laterNameId = Guid.Parse("10000000-0000-0000-0000-000000000003");
        await SeedAsync(
            database,
            [
                Mistake(laterNameId, "Overtrading", true),
                Mistake(lowerId, "fomo", true),
                Mistake(upperId, "FOMO", true),
            ],
            [
                Association(Guid.NewGuid(), tradeId, laterNameId, null),
                Association(Guid.NewGuid(), tradeId, lowerId, null),
                Association(Guid.NewGuid(), tradeId, upperId, null),
            ]);

        IReadOnlyList<TradeMistakeListItem> result =
            await GetReader(database).GetByTradeIdAsync(tradeId);

        Assert.Equal(
            [upperId, lowerId, laterNameId],
            result.Select(item => item.TradingMistakeId));
    }

    [Fact]
    public async Task GetByTradeIdAsyncReturnsOnlyRequestedTrade()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid requestedTradeId = await PersistTradeAsync(database);
        Guid otherTradeId = await PersistTradeAsync(database);
        Guid catalogId = Guid.NewGuid();
        Guid requestedAssignmentId = Guid.NewGuid();
        await SeedAsync(
            database,
            [Mistake(catalogId, "FOMO", true)],
            [
                Association(requestedAssignmentId, requestedTradeId, catalogId, null),
                Association(Guid.NewGuid(), otherTradeId, catalogId, null),
            ]);

        TradeMistakeListItem item = Assert.Single(
            await GetReader(database).GetByTradeIdAsync(requestedTradeId));

        Assert.Equal(requestedAssignmentId, item.Id);
    }

    [Fact]
    public async Task GetByTradeIdAsyncPropagatesPreCancelledToken()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => GetReader(database).GetByTradeIdAsync(
                Guid.NewGuid(),
                cancellationSource.Token));
    }

    internal static async Task<Guid> PersistTradeAsync(ReaderTestDatabase database)
    {
        Guid accountId = Guid.NewGuid();
        Guid instrumentId = Guid.NewGuid();
        Guid tradeId = Guid.NewGuid();
        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        context.TradingAccounts.Add(new TradingAccountRecord
        {
            Id = accountId,
            Name = $"Mistake Account {accountId:N}",
            AccountType = Domain.Accounts.TradingAccountType.Demo,
            Currency = "USD",
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
        });
        context.Instruments.Add(new InstrumentRecord
        {
            Id = instrumentId,
            Symbol = $"NQ{instrumentId:N}"[..12],
            DisplayName = "Nasdaq Futures",
            AssetClass = Domain.Instruments.AssetClass.Futures,
            Currency = "USD",
            TickSize = 0.25m,
            TickValue = 5m,
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
        });
        context.Trades.Add(new TradeRecord
        {
            Id = tradeId,
            TradingAccountId = accountId,
            InstrumentId = instrumentId,
            PricingPointValue = 20m,
            PricingCurrency = "USD",
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
        });
        await context.SaveChangesAsync();
        return tradeId;
    }

    internal static TradingMistakeRecord Mistake(
        Guid id,
        string name,
        bool active) =>
        new()
        {
            Id = id,
            Name = name,
            IsActive = active,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
        };

    internal static TradeMistakeRecord Association(
        Guid id,
        Guid tradeId,
        Guid catalogId,
        string? note,
        DateTimeOffset? updatedAtUtc = null) =>
        new()
        {
            Id = id,
            TradeId = tradeId,
            TradingMistakeId = catalogId,
            Note = note,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = updatedAtUtc ?? CreatedAtUtc,
        };

    internal static async Task SeedAsync(
        ReaderTestDatabase database,
        IReadOnlyCollection<TradingMistakeRecord> mistakes,
        IReadOnlyCollection<TradeMistakeRecord> associations)
    {
        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        context.TradingMistakes.AddRange(mistakes);
        context.TradeMistakes.AddRange(associations);
        await context.SaveChangesAsync();
    }

    private static ITradeMistakeReader GetReader(ReaderTestDatabase database) =>
        database.ServiceProvider.GetRequiredService<ITradeMistakeReader>();
}
