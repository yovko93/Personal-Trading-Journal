using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Screenshots;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Screenshots;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ScreenshotPersistenceCollection
{
    public const string Name = "Screenshot metadata persistence";
}

internal static class ScreenshotPersistenceTestData
{
    public static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 13, 10, 0, 0, TimeSpan.Zero);

    public static async Task<Guid> PersistTradeAsync(
        ReaderTestDatabase database,
        bool referencesAreActive = true,
        Guid? tradeId = null)
    {
        Guid accountId = Guid.NewGuid();
        Guid instrumentId = Guid.NewGuid();
        Guid persistedTradeId = tradeId ?? Guid.NewGuid();

        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        context.TradingAccounts.Add(new TradingAccountRecord
        {
            Id = accountId,
            Name = "Screenshot Test Account",
            AccountType = TradingAccountType.Demo,
            ProviderName = null,
            ExternalAccountId = null,
            Currency = "USD",
            StartingBalance = null,
            IsActive = referencesAreActive,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
        });
        context.Instruments.Add(new InstrumentRecord
        {
            Id = instrumentId,
            Symbol = "NQ",
            DisplayName = "Nasdaq-100 E-mini",
            AssetClass = AssetClass.Futures,
            Exchange = "CME",
            Currency = "USD",
            TickSize = 0.25m,
            TickValue = 5m,
            IsActive = referencesAreActive,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
        });
        context.Trades.Add(new TradeRecord
        {
            Id = persistedTradeId,
            TradingAccountId = accountId,
            InstrumentId = instrumentId,
            PricingPointValue = 20m,
            PricingCurrency = "USD",
            StrategyId = null,
            TradingSetupId = null,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
        });
        await context.SaveChangesAsync();

        return persistedTradeId;
    }

    public static TradeScreenshot CreateScreenshot(
        Guid tradeId,
        Guid? screenshotId = null,
        TradeScreenshotType type = TradeScreenshotType.Entry,
        string storageKey = "opaque/screenshots/chart-01.png",
        string fileName = "nq-entry.png",
        DateTimeOffset? capturedAtUtc = null,
        string? timeframe = "5m",
        string? description = "Screenshot context.",
        DateTimeOffset? createdAtUtc = null,
        DateTimeOffset? updatedAtUtc = null)
    {
        DateTimeOffset created = createdAtUtc ?? CreatedAtUtc;

        return TradeScreenshot.Rehydrate(
            screenshotId ?? Guid.NewGuid(),
            tradeId,
            type,
            storageKey,
            fileName,
            capturedAtUtc,
            timeframe,
            description,
            created,
            updatedAtUtc ?? created);
    }
}
