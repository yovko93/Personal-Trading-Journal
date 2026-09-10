using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Trades;

public sealed class ManualTradeReferenceDataReaderTests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetAsyncReturnsOnlyActiveReferencesByDefault()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid activeAccountId = Guid.NewGuid();
        Guid inactiveAccountId = Guid.NewGuid();
        Guid activeInstrumentId = Guid.NewGuid();
        Guid inactiveInstrumentId = Guid.NewGuid();
        await SeedAsync(
            database,
            [
                CreateAccountRecord(activeAccountId, "Active Account", isActive: true),
                CreateAccountRecord(inactiveAccountId, "Inactive Account", isActive: false),
            ],
            [
                CreateInstrumentRecord(activeInstrumentId, "NQ", isActive: true),
                CreateInstrumentRecord(inactiveInstrumentId, "MNQ", isActive: false),
            ]);
        IManualTradeReferenceDataReader reader =
            database.ServiceProvider.GetRequiredService<IManualTradeReferenceDataReader>();

        ManualTradeReferenceData result = await reader.GetAsync();

        Assert.Equal(activeAccountId, Assert.Single(result.Accounts).Id);
        Assert.Equal(activeInstrumentId, Assert.Single(result.Instruments).Id);
        Assert.All(result.Accounts, option => Assert.True(option.IsActive));
        Assert.All(result.Instruments, option => Assert.True(option.IsActive));
    }

    [Fact]
    public async Task GetAsyncIncludesInactiveReferencesWhenRequested()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid activeAccountId = Guid.NewGuid();
        Guid inactiveAccountId = Guid.NewGuid();
        Guid activeInstrumentId = Guid.NewGuid();
        Guid inactiveInstrumentId = Guid.NewGuid();
        await SeedAsync(
            database,
            [
                CreateAccountRecord(activeAccountId, "Active Account", isActive: true),
                CreateAccountRecord(inactiveAccountId, "Inactive Account", isActive: false),
            ],
            [
                CreateInstrumentRecord(activeInstrumentId, "NQ", isActive: true),
                CreateInstrumentRecord(inactiveInstrumentId, "MNQ", isActive: false),
            ]);
        IManualTradeReferenceDataReader reader =
            database.ServiceProvider.GetRequiredService<IManualTradeReferenceDataReader>();

        ManualTradeReferenceData result = await reader.GetAsync(
            includeInactiveReferences: true);

        Assert.Equal(2, result.Accounts.Count);
        Assert.True(Assert.Single(result.Accounts, option => option.Id == activeAccountId).IsActive);
        Assert.False(
            Assert.Single(result.Accounts, option => option.Id == inactiveAccountId).IsActive);
        Assert.Equal(2, result.Instruments.Count);
        Assert.True(
            Assert.Single(result.Instruments, option => option.Id == activeInstrumentId).IsActive);
        Assert.False(
            Assert.Single(result.Instruments, option => option.Id == inactiveInstrumentId).IsActive);
    }

    [Fact]
    public async Task GetAsyncProjectsAccountSelectionFieldsExactly()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid accountId = Guid.NewGuid();
        var record = new TradingAccountRecord
        {
            Id = accountId,
            Name = "Funded Account",
            AccountType = TradingAccountType.PropFunded,
            ProviderName = "Funding Provider",
            ExternalAccountId = "ACCOUNT-42",
            Currency = "EUR",
            StartingBalance = 100000.25m,
            IsActive = true,
            CreatedAtUtc = Timestamp,
            UpdatedAtUtc = Timestamp,
        };
        await SeedAsync(database, [record], []);
        IManualTradeReferenceDataReader reader =
            database.ServiceProvider.GetRequiredService<IManualTradeReferenceDataReader>();

        ManualTradeAccountOption option = Assert.Single(
            (await reader.GetAsync()).Accounts);

        Assert.Equal(accountId, option.Id);
        Assert.Equal("Funded Account", option.Name);
        Assert.Equal(TradingAccountType.PropFunded, option.AccountType);
        Assert.Equal("Funding Provider", option.ProviderName);
        Assert.Equal("ACCOUNT-42", option.ExternalAccountId);
        Assert.Equal("EUR", option.Currency);
        Assert.True(option.IsActive);
        Assert.DoesNotContain(
            typeof(ManualTradeAccountOption).GetProperties(),
            property => property.Name == nameof(TradingAccountRecord.StartingBalance));
    }

    [Fact]
    public async Task GetAsyncProjectsInstrumentSelectionFieldsAndPointValueExactly()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid instrumentId = Guid.NewGuid();
        var record = new InstrumentRecord
        {
            Id = instrumentId,
            Symbol = "NQ",
            DisplayName = "Nasdaq-100 E-mini",
            AssetClass = AssetClass.Futures,
            Exchange = "CME",
            Currency = "USD",
            TickSize = 0.25m,
            TickValue = 5m,
            IsActive = true,
            CreatedAtUtc = Timestamp,
            UpdatedAtUtc = Timestamp,
        };
        await SeedAsync(database, [], [record]);
        IManualTradeReferenceDataReader reader =
            database.ServiceProvider.GetRequiredService<IManualTradeReferenceDataReader>();

        ManualTradeInstrumentOption option = Assert.Single(
            (await reader.GetAsync()).Instruments);

        Assert.Equal(instrumentId, option.Id);
        Assert.Equal("NQ", option.Symbol);
        Assert.Equal("Nasdaq-100 E-mini", option.DisplayName);
        Assert.Equal(AssetClass.Futures, option.AssetClass);
        Assert.Equal("CME", option.Exchange);
        Assert.Equal("USD", option.Currency);
        Assert.Equal(0.25m, option.TickSize);
        Assert.Equal(5m, option.TickValue);
        Assert.Equal(20m, option.PointValue);
        Assert.True(option.IsActive);
    }

    [Fact]
    public async Task GetAsyncOrdersAccountsByActiveStateNameNoCaseAndId()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid firstActiveAlphaId =
            Guid.Parse("10000000-0000-0000-0000-000000000001");
        Guid secondActiveAlphaId =
            Guid.Parse("10000000-0000-0000-0000-000000000002");
        Guid activeBetaId =
            Guid.Parse("10000000-0000-0000-0000-000000000003");
        Guid inactiveAlphaId =
            Guid.Parse("10000000-0000-0000-0000-000000000004");
        Guid inactiveBetaId =
            Guid.Parse("10000000-0000-0000-0000-000000000005");
        await SeedAsync(
            database,
            [
                CreateAccountRecord(inactiveBetaId, "Beta", isActive: false),
                CreateAccountRecord(activeBetaId, "Beta", isActive: true),
                CreateAccountRecord(secondActiveAlphaId, "alpha", isActive: true),
                CreateAccountRecord(inactiveAlphaId, "alpha", isActive: false),
                CreateAccountRecord(firstActiveAlphaId, "Alpha", isActive: true),
            ],
            []);
        IManualTradeReferenceDataReader reader =
            database.ServiceProvider.GetRequiredService<IManualTradeReferenceDataReader>();

        ManualTradeReferenceData result = await reader.GetAsync(
            includeInactiveReferences: true);

        Assert.Equal(
            [
                firstActiveAlphaId,
                secondActiveAlphaId,
                activeBetaId,
                inactiveAlphaId,
                inactiveBetaId,
            ],
            result.Accounts.Select(option => option.Id));
    }

    [Fact]
    public async Task GetAsyncOrdersInstrumentsByActiveStateSymbolAndId()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid firstActiveEsId =
            Guid.Parse("20000000-0000-0000-0000-000000000001");
        Guid secondActiveEsId =
            Guid.Parse("20000000-0000-0000-0000-000000000002");
        Guid activeNqId =
            Guid.Parse("20000000-0000-0000-0000-000000000003");
        Guid inactiveAaplId =
            Guid.Parse("20000000-0000-0000-0000-000000000004");
        InstrumentRecord firstEs = CreateInstrumentRecord(
            firstActiveEsId,
            "ES",
            isActive: true);
        firstEs.DisplayName = "Zulu";
        InstrumentRecord secondEs = CreateInstrumentRecord(
            secondActiveEsId,
            "ES",
            isActive: true);
        secondEs.DisplayName = "Alpha";
        InstrumentRecord nq = CreateInstrumentRecord(activeNqId, "NQ", isActive: true);
        nq.DisplayName = "Aardvark";
        await SeedAsync(
            database,
            [],
            [
                CreateInstrumentRecord(inactiveAaplId, "AAPL", isActive: false),
                nq,
                secondEs,
                firstEs,
            ]);
        IManualTradeReferenceDataReader reader =
            database.ServiceProvider.GetRequiredService<IManualTradeReferenceDataReader>();

        ManualTradeReferenceData result = await reader.GetAsync(
            includeInactiveReferences: true);

        Assert.Equal(
            [firstActiveEsId, secondActiveEsId, activeNqId, inactiveAaplId],
            result.Instruments.Select(option => option.Id));
    }

    [Fact]
    public async Task GetAsyncUsesFreshContextAndObservesCurrentActiveState()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid initiallyActiveAccountId = Guid.NewGuid();
        Guid subsequentlyActiveAccountId = Guid.NewGuid();
        Guid initiallyActiveInstrumentId = Guid.NewGuid();
        Guid subsequentlyActiveInstrumentId = Guid.NewGuid();
        await SeedAsync(
            database,
            [
                CreateAccountRecord(
                    initiallyActiveAccountId,
                    "Initially Active",
                    isActive: true),
                CreateAccountRecord(
                    subsequentlyActiveAccountId,
                    "Subsequently Active",
                    isActive: false),
            ],
            [
                CreateInstrumentRecord(
                    initiallyActiveInstrumentId,
                    "ES",
                    isActive: true),
                CreateInstrumentRecord(
                    subsequentlyActiveInstrumentId,
                    "NQ",
                    isActive: false),
            ]);
        IManualTradeReferenceDataReader reader =
            database.ServiceProvider.GetRequiredService<IManualTradeReferenceDataReader>();

        ManualTradeReferenceData firstResult = await reader.GetAsync();

        await using (JournalDbContext updateContext =
                     await database.ContextFactory.CreateDbContextAsync())
        {
            TradingAccountRecord initiallyActiveAccount = await updateContext.TradingAccounts
                .SingleAsync(record => record.Id == initiallyActiveAccountId);
            TradingAccountRecord subsequentlyActiveAccount = await updateContext.TradingAccounts
                .SingleAsync(record => record.Id == subsequentlyActiveAccountId);
            InstrumentRecord initiallyActiveInstrument = await updateContext.Instruments
                .SingleAsync(record => record.Id == initiallyActiveInstrumentId);
            InstrumentRecord subsequentlyActiveInstrument = await updateContext.Instruments
                .SingleAsync(record => record.Id == subsequentlyActiveInstrumentId);
            initiallyActiveAccount.IsActive = false;
            subsequentlyActiveAccount.IsActive = true;
            initiallyActiveInstrument.IsActive = false;
            subsequentlyActiveInstrument.IsActive = true;
            await updateContext.SaveChangesAsync();
        }

        ManualTradeReferenceData secondResult = await reader.GetAsync();

        Assert.Equal(initiallyActiveAccountId, Assert.Single(firstResult.Accounts).Id);
        Assert.Equal(initiallyActiveInstrumentId, Assert.Single(firstResult.Instruments).Id);
        Assert.Equal(subsequentlyActiveAccountId, Assert.Single(secondResult.Accounts).Id);
        Assert.Equal(subsequentlyActiveInstrumentId, Assert.Single(secondResult.Instruments).Id);
    }

    [Fact]
    public async Task GetAsyncPropagatesCancellation()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        IManualTradeReferenceDataReader reader =
            database.ServiceProvider.GetRequiredService<IManualTradeReferenceDataReader>();
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => reader.GetAsync(cancellationToken: cancellationSource.Token));
    }

    private static async Task SeedAsync(
        ReaderTestDatabase database,
        IEnumerable<TradingAccountRecord> accounts,
        IEnumerable<InstrumentRecord> instruments)
    {
        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        context.TradingAccounts.AddRange(accounts);
        context.Instruments.AddRange(instruments);
        await context.SaveChangesAsync();
    }

    private static TradingAccountRecord CreateAccountRecord(
        Guid id,
        string name,
        bool isActive)
    {
        return new TradingAccountRecord
        {
            Id = id,
            Name = name,
            AccountType = TradingAccountType.Other,
            Currency = "USD",
            IsActive = isActive,
            CreatedAtUtc = Timestamp,
            UpdatedAtUtc = Timestamp,
        };
    }

    private static InstrumentRecord CreateInstrumentRecord(
        Guid id,
        string symbol,
        bool isActive)
    {
        return new InstrumentRecord
        {
            Id = id,
            Symbol = symbol,
            DisplayName = $"{symbol} display name",
            AssetClass = AssetClass.Futures,
            Exchange = "CME",
            Currency = "USD",
            TickSize = 0.25m,
            TickValue = 5m,
            IsActive = isActive,
            CreatedAtUtc = Timestamp,
            UpdatedAtUtc = Timestamp,
        };
    }
}
