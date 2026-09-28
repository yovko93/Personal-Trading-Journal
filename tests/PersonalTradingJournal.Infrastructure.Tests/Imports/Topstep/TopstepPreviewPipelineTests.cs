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

public sealed class TopstepPreviewPipelineTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task CompletePreviewWithRealStagesAndReadOnlySqlitePreservesRowsAndMakesNoWrites(int matches)
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid accountId = Guid.NewGuid();
        await using JournalDbContext seed = await database.ContextFactory.CreateDbContextAsync();
        seed.TradingAccounts.Add(new TradingAccountRecord
        {
            Id = accountId, Name = "Synthetic Topstep", AccountType = TradingAccountType.PropFunded,
            ProviderName = "Topstep", Currency = "USD", IsActive = true,
            CreatedAtUtc = DateTimeOffset.UnixEpoch, UpdatedAtUtc = DateTimeOffset.UnixEpoch,
        });
        for (int i = 0; i < matches; i++) seed.Instruments.Add(new InstrumentRecord
        {
            Id = Guid.NewGuid(), Symbol = "MNQ", DisplayName = "Micro E-mini Nasdaq-100", AssetClass = AssetClass.Futures,
            Exchange = "CME", Currency = "USD", TickSize = .25m, TickValue = .5m, IsActive = i == 0,
            CreatedAtUtc = DateTimeOffset.UnixEpoch, UpdatedAtUtc = DateTimeOffset.UnixEpoch,
        });
        await seed.SaveChangesAsync();
        var connection = new SqliteConnectionStringBuilder(seed.Database.GetDbConnection().ConnectionString) { Mode = SqliteOpenMode.ReadOnly };
        var factory = new ReadOnlyFactory(new DbContextOptionsBuilder<JournalDbContext>().UseSqlite(connection.ToString()).Options);
        var builder = new TopstepImportPreviewBuilder(new TopstepCsvParser(), new TopstepTradeCandidateReconstructor(),
            new(new InstrumentReader(factory), new TradingAccountReader(factory)));
        string csv = TopstepCsvFixtures.WithRows(TopstepCsvFixtures.Row, TopstepCsvFixtures.Replace(0, "SYNTH-2"));
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        TopstepImportPreview preview = await builder.BuildAsync("synthetic.csv", source, accountId,
            TopstepCostInterpretation.SeparateReportedRoundTurnTotalsUsd);

        Assert.Equal(matches < 2, preview.IsEligibleForReview);
        Assert.Equal(2, preview.Summary.AcceptedRowCount);
        Assert.Equal(0, preview.Summary.RejectedRowCount);
        Assert.Equal(2, preview.Summary.ClosedRowCandidateCount);
        Assert.Equal([1, 2], preview.Candidates.Select(c => c.SourceRecordIndex));
        Assert.Equal([2, 3], preview.Candidates.Select(c => c.SourceLineNumber));
        Assert.All(preview.Candidates, c => Assert.Equal(20000.125m, c.EntryPrice));
        Assert.Contains(preview.Diagnostics, d => d.Code == TopstepReconstructionDiagnosticCodes.PositionBoundariesUnverified && d.SourceReferences.Count == 2);
        Assert.Contains(preview.Diagnostics, d => d.Code == TopstepReconstructionDiagnosticCodes.PositionGroupingAmbiguous && d.SourceReferences.Count == 2);
        Assert.False(preview.MeetsReviewRequirements(new(preview.SnapshotFingerprint, [])));
        Assert.Equal(matches < 2, preview.MeetsReviewRequirements(new(preview.SnapshotFingerprint, preview.ReviewRequirements.Select(r => r.Key).ToArray())));
        if (matches < 2) Assert.Equal(new TopstepPreviewTotals("USD", 10m, 10m, 2.88m, 2m, 5.12m), preview.Summary.ReconciledTotals);
        else Assert.Null(preview.Summary.ReconciledTotals);
        Assert.Equal(matches == 0 ? 1 : 0, preview.Summary.ProposedInstrumentCount);
        using var repeat = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        TopstepImportPreview rebuilt = await builder.BuildAsync("synthetic.csv", repeat, accountId,
            TopstepCostInterpretation.SeparateReportedRoundTurnTotalsUsd);
        Assert.Equal(preview.SnapshotFingerprint, rebuilt.SnapshotFingerprint);

        await using JournalDbContext verify = await database.ContextFactory.CreateDbContextAsync();
        Assert.Equal(matches, await verify.Instruments.CountAsync());
        Assert.Equal(1, await verify.TradingAccounts.CountAsync());
        Assert.Equal(DateTimeOffset.UnixEpoch, (await verify.TradingAccounts.SingleAsync()).UpdatedAtUtc);
        Assert.False(await verify.Trades.AnyAsync());
        Assert.False(await verify.TradeExecutions.AnyAsync());
        Assert.False(await verify.TradeBrowse.AnyAsync());
        Assert.False(await verify.TradovateImportedExecutions.AnyAsync());
    }

    [Fact]
    public async Task RealParserRejectsMixedValidAndInvalidRowsWithoutPartialEligiblePreview()
    {
        // An invalid source must short-circuit reference access entirely.
        var builder = new TopstepImportPreviewBuilder(new TopstepCsvParser(), new TopstepTradeCandidateReconstructor(),
            new(new UnusedInstrumentReader(), new UnusedAccountReader()));
        string invalid = TopstepCsvFixtures.Replace(8, "0", TopstepCsvFixtures.Replace(0, "SYNTH-2"));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(TopstepCsvFixtures.WithRows(TopstepCsvFixtures.Row, invalid)));
        TopstepImportPreview preview = await builder.BuildAsync("invalid.csv", stream, Guid.NewGuid());
        Assert.Equal(1, preview.Summary.AcceptedRowCount);
        Assert.Equal(1, preview.Summary.RejectedRowCount);
        Assert.Empty(preview.Candidates);
        Assert.False(preview.IsEligibleForReview);
        Assert.Contains(preview.Diagnostics, d => d.Stage == TopstepPreviewDiagnosticStage.Csv && d.SourceLineNumber == 3 && d.FieldName == "Size");
    }

    private sealed class ReadOnlyFactory(DbContextOptions<JournalDbContext> options) : IDbContextFactory<JournalDbContext>
    {
        public JournalDbContext CreateDbContext() => new(options);
        public Task<JournalDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(CreateDbContext()); }
    }
    private sealed class UnusedInstrumentReader : PersonalTradingJournal.Application.Instruments.IInstrumentReader
    {
        public Task<IReadOnlyList<PersonalTradingJournal.Application.Instruments.InstrumentListItem>> GetAllAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<PersonalTradingJournal.Application.Instruments.InstrumentDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }
    private sealed class UnusedAccountReader : PersonalTradingJournal.Application.Accounts.ITradingAccountReader
    {
        public Task<IReadOnlyList<PersonalTradingJournal.Application.Accounts.AccountListItem>> GetAllAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<PersonalTradingJournal.Application.Accounts.TradingAccountDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }
}
