using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Imports.Topstep;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Infrastructure.Accounts;
using PersonalTradingJournal.Infrastructure.Imports.Topstep;
using PersonalTradingJournal.Infrastructure.Instruments;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;
using PersonalTradingJournal.Infrastructure.Tests.Persistence;

namespace PersonalTradingJournal.Infrastructure.Tests.Imports.Topstep;

public sealed class TopstepReferencePipelineTests
{
    private static readonly Guid AccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RealReadersResolveAndReconcileUsingReadOnlySqliteWithoutImportWrites(bool existingInstrument)
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        TopstepReferencePreparationService service = await SeedAndCreateReadOnlyService(database, existingInstrument);
        TopstepTradeReconstructionResult source = await ParseCandidates();

        TopstepReferencePreparationResult result = await Prepare(service, source);

        Assert.True(result.IsReadyForPreview);
        Assert.Equal(!existingInstrument, result.RequiresInstrumentCreationApproval);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal([5m, 5m], result.Rows.Select(r => r.Economics.CalculatedGrossPnL));
        Assert.Equal([2.56m, 2.56m], result.Rows.Select(r => r.Economics.NetPnL));
        Assert.Equal([2m, 2m], result.Rows.Select(r => r.Economics.Candidate.Quantity));
        Assert.Equal(source.Candidates, result.Rows.Select(r => r.Economics.Candidate));
        Assert.Same(source, result.Economics.Source);
        Assert.Contains(result.Economics.Source.Diagnostics, d => d.Code == TopstepReconstructionDiagnosticCodes.PositionGroupingAmbiguous);
        Assert.All(result.Rows, r => Assert.Equal(AccountId, r.DestinationTradingAccountId));

        await using JournalDbContext verify = await database.ContextFactory.CreateDbContextAsync();
        Assert.Equal(existingInstrument ? 1 : 0, await verify.Instruments.CountAsync());
        Assert.Equal(1, await verify.TradingAccounts.CountAsync());
        Assert.True((await verify.TradingAccounts.SingleAsync()).IsActive);
        Assert.Equal(DateTimeOffset.UnixEpoch, (await verify.TradingAccounts.SingleAsync()).UpdatedAtUtc);
        Assert.False(await verify.Trades.AnyAsync());
        Assert.False(await verify.TradeExecutions.AnyAsync());
        Assert.False(await verify.TradeBrowse.AnyAsync());
        Assert.False(await verify.TradovateImportedExecutions.AnyAsync());
    }

    [Fact]
    public async Task SameServiceRereadsSqliteAndBlocksNewAmbiguityThenDeletedAccount()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        TopstepReferencePreparationService service = await SeedAndCreateReadOnlyService(database, true);
        TopstepTradeReconstructionResult source = await ParseCandidates();
        TopstepReferencePreparationResult original = await Prepare(service, source);
        Assert.True(original.IsReadyForPreview);

        // These are explicit isolated fixture changes, not writes by preparation.
        await using (JournalDbContext change = await database.ContextFactory.CreateDbContextAsync())
        {
            InstrumentRecord duplicate = Instrument();
            duplicate.IsActive = false;
            change.Instruments.Add(duplicate);
            await change.SaveChangesAsync();
        }
        TopstepReferencePreparationResult ambiguous = await Prepare(service, source);
        Assert.False(ambiguous.IsReadyForPreview);
        Assert.Contains(ambiguous.Diagnostics, d => d.Code == TopstepReferenceDiagnosticCodes.MultipleInstrumentMatches);
        Assert.Equal(2, ambiguous.Instruments[0].MatchingInstruments.Count);
        Assert.Single(original.Instruments[0].MatchingInstruments);

        await using (JournalDbContext change = await database.ContextFactory.CreateDbContextAsync())
        {
            change.TradingAccounts.Remove(await change.TradingAccounts.SingleAsync());
            await change.SaveChangesAsync();
        }
        TopstepReferencePreparationResult deleted = await Prepare(service, source);
        Assert.False(deleted.IsReadyForPreview);
        Assert.Contains(deleted.Diagnostics, d => d.Code == TopstepReferenceDiagnosticCodes.AccountNotFound);
        Assert.Empty(deleted.Rows);
        Assert.NotNull(original.Account);
    }

    [Fact]
    public async Task RealReaderPipelineHonorsPreCancellation()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        TopstepReferencePreparationService service = await SeedAndCreateReadOnlyService(database, true);
        TopstepTradeReconstructionResult source = await ParseCandidates();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.PrepareAsync(source, AccountId,
            TopstepCostInterpretation.SeparateReportedRoundTurnTotalsUsd, cancellationToken: cancellation.Token));
        await using JournalDbContext verify = await database.ContextFactory.CreateDbContextAsync();
        Assert.False(await verify.Trades.AnyAsync());
    }

    private static async Task<TopstepReferencePreparationService> SeedAndCreateReadOnlyService(ReaderTestDatabase database, bool instrument)
    {
        await using JournalDbContext seed = await database.ContextFactory.CreateDbContextAsync();
        seed.TradingAccounts.Add(new TradingAccountRecord
        {
            Id = AccountId, Name = "Synthetic Topstep", AccountType = TradingAccountType.PropFunded,
            ProviderName = "Topstep", Currency = "USD", IsActive = true,
            CreatedAtUtc = DateTimeOffset.UnixEpoch, UpdatedAtUtc = DateTimeOffset.UnixEpoch,
        });
        if (instrument) seed.Instruments.Add(Instrument());
        await seed.SaveChangesAsync();
        var connection = new SqliteConnectionStringBuilder(seed.Database.GetDbConnection().ConnectionString)
        {
            Mode = SqliteOpenMode.ReadOnly,
        };
        var factory = new ReadOnlyFactory(new DbContextOptionsBuilder<JournalDbContext>().UseSqlite(connection.ToString()).Options);
        return new(new InstrumentReader(factory), new TradingAccountReader(factory));
    }

    private static InstrumentRecord Instrument() => new()
    {
        Id = Guid.NewGuid(), Symbol = "MNQ", DisplayName = "Micro E-mini Nasdaq-100", AssetClass = AssetClass.Futures,
        Exchange = "CME", Currency = "USD", TickSize = 0.25m, TickValue = 0.50m, IsActive = true,
        CreatedAtUtc = DateTimeOffset.UnixEpoch, UpdatedAtUtc = DateTimeOffset.UnixEpoch,
    };

    private static Task<TopstepReferencePreparationResult> Prepare(TopstepReferencePreparationService service, TopstepTradeReconstructionResult source) =>
        service.PrepareAsync(source, AccountId, TopstepCostInterpretation.SeparateReportedRoundTurnTotalsUsd);

    private static async Task<TopstepTradeReconstructionResult> ParseCandidates()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(TopstepCsvFixtures.WithRows(
            TopstepCsvFixtures.Row, TopstepCsvFixtures.Replace(0, "SYNTH-2"))));
        return new TopstepTradeCandidateReconstructor().Reconstruct(await new TopstepCsvParser().ParseAsync(stream));
    }

    private sealed class ReadOnlyFactory(DbContextOptions<JournalDbContext> options) : IDbContextFactory<JournalDbContext>
    {
        public JournalDbContext CreateDbContext() => new(options);
        public Task<JournalDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }
}
