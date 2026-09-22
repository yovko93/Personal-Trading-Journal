using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Common.Time;
using PersonalTradingJournal.Application.Imports.Tradovate;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Tests.Imports.Tradovate;

public sealed class TradovateImportPreparationServiceTests
{
    private static readonly Guid AccountId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid InstrumentId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task ActiveExplicitAccountProducesReadyDeterministicPreparation()
    {
        var reader = new RecordingAccountReader(Account());
        var service = new TradovateImportPreparationService(reader);
        TradovateExecutionReconstructionResult reconstruction = Reconstruction(
            EntryAt(2026, 9, 10, 16, 30),
            ExitAt(2026, 9, 10, 17, 0));
        TradovateInstrumentResolutionResult resolution = ExistingResolution();

        TradovateImportPreparationResult result = await service.PrepareAsync(
            reconstruction,
            resolution,
            AccountId);

        Assert.True(result.IsReadyForPreview);
        Assert.Equal(TradovateImportPreparationStatus.ReadyForPreview, result.Status);
        Assert.Equal(AccountId, reader.RequestedId);
        Assert.Equal(TradingTimePolicy.TradovateSourceTimeZoneId, result.SourceTimeZoneId);
        Assert.Equal(TradingTimePolicy.TradingTimeZoneId, result.TradingTimeZoneId);
        Assert.Same(resolution, result.InstrumentResolution);
        Assert.Equal(2, result.PreparedExecutions.Count);
        TradovatePreparedExecution entry = result.PreparedExecutions[0];
        Assert.Equal(new DateTime(2026, 9, 10, 16, 30, 0), entry.SourceLocalTimestamp);
        Assert.Equal(new DateTimeOffset(2026, 9, 10, 13, 30, 0, TimeSpan.Zero), entry.ExecutedAtUtc);
        Assert.Equal(new DateTime(2026, 9, 10, 9, 30, 0), entry.TradingLocalTimestamp);
        Assert.Equal(TimeSpan.FromHours(-4), entry.TradingUtcOffset);
        Assert.Equal("MNQU6", entry.BrokerSymbol);
        Assert.Equal([1], entry.SourceRecordIndices);
        Assert.Equal([2], entry.SourceLineNumbers);

        TradovatePreparedTradeCandidate candidate = Assert.Single(result.PreparedCandidates);
        Assert.Equal("MNQU6", candidate.BrokerSymbol);
        Assert.Equal("MNQ", candidate.CanonicalSymbol);
        Assert.Equal(InstrumentId, candidate.ExistingInstrumentId);
        Assert.Null(candidate.InstrumentCreationProposal);
        Assert.Equal(AccountId, candidate.TradingAccountId);
        Assert.Equal(TradeDirection.Long, candidate.ProvisionalDirection);
        Assert.Equal(result.PreparedExecutions, candidate.OrderedExecutions);
        Assert.Equal(entry.ExecutedAtUtc, candidate.OpenedAtUtc);
        Assert.Equal(new DateTime(2026, 9, 10, 9, 30, 0), candidate.OpenedAtNewYork.DateTime);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task MissingAccountBlocksWithoutSubstitution()
    {
        Guid requestedId = Guid.NewGuid();
        var reader = new RecordingAccountReader(details: null);
        var service = new TradovateImportPreparationService(reader);

        TradovateImportPreparationResult result = await service.PrepareAsync(
            Reconstruction(EntryAt(2026, 9, 10, 16, 30), ExitAt(2026, 9, 10, 17, 0)),
            ExistingResolution(),
            requestedId);

        Assert.False(result.IsReadyForPreview);
        Assert.Equal(TradovateImportPreparationStatus.Blocked, result.Status);
        Assert.Equal(requestedId, reader.RequestedId);
        Assert.Contains(requestedId.ToString(), Assert.Single(result.Diagnostics).Message);
        AssertDiagnostic(result, TradovateImportPreparationDiagnosticCodes.TradingAccountNotFound);
        Assert.Empty(result.PreparedExecutions);
        Assert.Empty(result.PreparedCandidates);
    }

    [Fact]
    public async Task InactiveAccountIsAllowedWithWarningAndSnapshotRemainsInactive()
    {
        var reader = new RecordingAccountReader(Account(isActive: false));
        var service = new TradovateImportPreparationService(reader);

        TradovateImportPreparationResult result = await service.PrepareAsync(
            Reconstruction(EntryAt(2026, 9, 10, 16, 30), ExitAt(2026, 9, 10, 17, 0)),
            ExistingResolution(),
            AccountId);

        Assert.True(result.IsReadyForPreview);
        Assert.False(result.AccountSnapshot!.IsActive);
        AssertDiagnostic(
            result,
            TradovateImportPreparationDiagnosticCodes.SelectedAccountInactive,
            TradovateReconstructionDiagnosticSeverity.Warning);
    }

    [Theory]
    [InlineData(2026, 3, 29, 3, 30,
        TradovateImportPreparationStatus.Blocked,
        TradovateImportPreparationDiagnosticCodes.InvalidSourceLocalTime)]
    [InlineData(2026, 10, 25, 3, 30,
        TradovateImportPreparationStatus.RequiresUserInput,
        TradovateImportPreparationDiagnosticCodes.AmbiguousSourceLocalTime)]
    public async Task SofiaDstUncertaintyProducesNoPartialPreparation(
        int year,
        int month,
        int day,
        int hour,
        int minute,
        TradovateImportPreparationStatus expectedStatus,
        string expectedCode)
    {
        var service = new TradovateImportPreparationService(
            new RecordingAccountReader(Account()));

        TradovateImportPreparationResult result = await service.PrepareAsync(
            Reconstruction(
                EntryAt(year, month, day, hour, minute),
                ExitAt(year, month, day, hour, minute + 1)),
            ExistingResolution(),
            AccountId);

        Assert.Equal(expectedStatus, result.Status);
        Assert.False(result.IsReadyForPreview);
        AssertDiagnostic(result, expectedCode);
        Assert.Empty(result.PreparedExecutions);
        Assert.Empty(result.PreparedCandidates);
    }

    [Fact]
    public async Task InstrumentCreationProposalIsPreservedWithoutInventingId()
    {
        var service = new TradovateImportPreparationService(
            new RecordingAccountReader(Account()));
        TradovateInstrumentResolutionResult resolution = ProposedResolution();

        TradovateImportPreparationResult result = await service.PrepareAsync(
            Reconstruction(EntryAt(2026, 9, 10, 16, 30), ExitAt(2026, 9, 10, 17, 0)),
            resolution,
            AccountId);

        Assert.True(result.IsReadyForPreview);
        TradovatePreparedTradeCandidate candidate = Assert.Single(result.PreparedCandidates);
        Assert.Null(candidate.ExistingInstrumentId);
        Assert.Same(
            Assert.Single(resolution.CreationProposals),
            candidate.InstrumentCreationProposal);
        Assert.Equal("MNQU6", candidate.BrokerSymbol);
        Assert.Equal("MNQ", candidate.CanonicalSymbol);
    }

    [Fact]
    public async Task AccountInstrumentCurrencyDifferenceIsWarningOnly()
    {
        var service = new TradovateImportPreparationService(
            new RecordingAccountReader(Account(currency: "EUR")));

        TradovateImportPreparationResult result = await service.PrepareAsync(
            Reconstruction(EntryAt(2026, 9, 10, 16, 30), ExitAt(2026, 9, 10, 17, 0)),
            ExistingResolution(),
            AccountId);

        Assert.True(result.IsReadyForPreview);
        AssertDiagnostic(
            result,
            TradovateImportPreparationDiagnosticCodes.AccountInstrumentCurrencyDifference,
            TradovateReconstructionDiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task PrerequisiteFailureDoesNotReadAccountOrReturnPartialData()
    {
        var reader = new RecordingAccountReader(Account());
        var service = new TradovateImportPreparationService(reader);
        TradovateExecutionReconstructionResult reconstruction = Reconstruction(
            EntryAt(2026, 9, 10, 16, 30),
            ExitAt(2026, 9, 10, 17, 0),
            TradovateReconstructionStatus.Blocked);

        TradovateImportPreparationResult result = await service.PrepareAsync(
            reconstruction,
            ExistingResolution(),
            AccountId);

        Assert.Equal(0, reader.CallCount);
        Assert.False(result.IsReadyForPreview);
        AssertDiagnostic(result, TradovateImportPreparationDiagnosticCodes.ReconstructionNotEligible);
        Assert.Empty(result.PreparedExecutions);
        Assert.Empty(result.PreparedCandidates);
    }

    [Fact]
    public async Task InstrumentResolutionNotReadyBlocksBeforeAccountRead()
    {
        var reader = new RecordingAccountReader(Account());
        var service = new TradovateImportPreparationService(reader);
        var resolution = new TradovateInstrumentResolutionResult(
            brokerSymbolMappings: [],
            canonicalInstrumentResolutions: [],
            diagnostics: [],
            TradovateInstrumentResolutionOverallStatus.Blocked);

        TradovateImportPreparationResult result = await service.PrepareAsync(
            Reconstruction(EntryAt(2026, 9, 10, 16, 30), ExitAt(2026, 9, 10, 17, 0)),
            resolution,
            AccountId);

        Assert.Equal(0, reader.CallCount);
        Assert.Equal(TradovateImportPreparationStatus.Blocked, result.Status);
        AssertDiagnostic(
            result,
            TradovateImportPreparationDiagnosticCodes.InstrumentResolutionNotReady);
        Assert.Empty(result.PreparedExecutions);
        Assert.Empty(result.PreparedCandidates);
    }

    [Fact]
    public async Task ZeroCandidatesCannotBecomeReady()
    {
        var reader = new RecordingAccountReader(Account());
        var service = new TradovateImportPreparationService(reader);
        var reconstruction = new TradovateExecutionReconstructionResult(
            executions: [],
            candidates: [],
            matchedPairs: [],
            symbolReconciliations: [],
            diagnostics: [],
            sourceRecordCount: 0,
            TradovateReconstructionStatus.Reconstructed);

        TradovateImportPreparationResult result = await service.PrepareAsync(
            reconstruction,
            ExistingResolution(),
            AccountId);

        Assert.False(result.IsReadyForPreview);
        Assert.Equal(TradovateImportPreparationStatus.Blocked, result.Status);
        Assert.Equal(0, reader.CallCount);
        AssertDiagnostic(
            result,
            TradovateImportPreparationDiagnosticCodes.ReconstructionNotEligible);
    }

    [Fact]
    public async Task DecreasingUtcChronologyBlocksWithoutReordering()
    {
        var service = new TradovateImportPreparationService(
            new RecordingAccountReader(Account()));

        TradovateImportPreparationResult result = await service.PrepareAsync(
            Reconstruction(EntryAt(2026, 9, 10, 17, 0), ExitAt(2026, 9, 10, 16, 30)),
            ExistingResolution(),
            AccountId);

        Assert.Equal(TradovateImportPreparationStatus.Blocked, result.Status);
        AssertDiagnostic(
            result,
            TradovateImportPreparationDiagnosticCodes.UtcChronologyInvalid);
        Assert.Empty(result.PreparedExecutions);
        Assert.Empty(result.PreparedCandidates);
    }

    [Fact]
    public async Task CancellationTokenReachesAccountReader()
    {
        using var source = new CancellationTokenSource();
        var reader = new RecordingAccountReader(Account());
        var service = new TradovateImportPreparationService(reader);

        TradovateImportPreparationResult result = await service.PrepareAsync(
            Reconstruction(EntryAt(2026, 9, 10, 16, 30), ExitAt(2026, 9, 10, 17, 0)),
            ExistingResolution(),
            AccountId,
            source.Token);

        Assert.True(result.IsReadyForPreview);
        Assert.Equal(source.Token, reader.CancellationToken);
    }

    [Fact]
    public async Task CancellationIsForwardedAndNotConvertedToDiagnostic()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var reader = new RecordingAccountReader(Account());
        var service = new TradovateImportPreparationService(reader);

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.PrepareAsync(
            Reconstruction(EntryAt(2026, 9, 10, 16, 30), ExitAt(2026, 9, 10, 17, 0)),
            ExistingResolution(),
            AccountId,
            source.Token));
    }

    private static TradingAccountDetails Account(
        bool isActive = true,
        string currency = "USD") => new(
        AccountId,
        "Tradovate Primary",
        TradingAccountType.Personal,
        "Tradovate",
        "A049",
        currency,
        25000m,
        isActive,
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private static TradovateReconstructedExecution EntryAt(
        int year, int month, int day, int hour, int minute) =>
        Execution(ExecutionSide.Buy, "B-1", year, month, day, hour, minute);

    private static TradovateReconstructedExecution ExitAt(
        int year, int month, int day, int hour, int minute) =>
        Execution(ExecutionSide.Sell, "S-1", year, month, day, hour, minute);

    private static TradovateReconstructedExecution Execution(
        ExecutionSide side,
        string externalFillId,
        int year,
        int month,
        int day,
        int hour,
        int minute) => new(
        "MNQU6",
        side,
        externalFillId,
        1m,
        side == ExecutionSide.Buy ? 24000m : 24010m,
        new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified),
        0.25m,
        [1],
        [2]);

    private static TradovateExecutionReconstructionResult Reconstruction(
        TradovateReconstructedExecution entry,
        TradovateReconstructedExecution exit,
        TradovateReconstructionStatus status = TradovateReconstructionStatus.Reconstructed)
    {
        var candidate = new TradovateTradeCandidate(
            "MNQU6",
            TradeDirection.Long,
            [entry, exit],
            entry.SourceLocalTimestamp,
            exit.SourceLocalTimestamp,
            1m,
            1m,
            0m,
            status,
            [1],
            []);
        return new TradovateExecutionReconstructionResult(
            [entry, exit],
            [candidate],
            matchedPairs: [],
            symbolReconciliations: [],
            diagnostics: [],
            sourceRecordCount: 1,
            status);
    }

    private static TradovateInstrumentResolutionResult ExistingResolution()
    {
        var mapping = new TradovateBrokerSymbolMapping(
            "MNQU6",
            "MNQ",
            TradovateInstrumentResolutionStatus.ExistingInstrument,
            InstrumentId,
            true,
            []);
        var canonical = new TradovateCanonicalInstrumentResolution(
            "MNQ",
            ["MNQU6"],
            0.25m,
            TradovateInstrumentResolutionStatus.ExistingInstrument,
            InstrumentId,
            true,
            [InstrumentId],
            creationProposal: null,
            diagnosticCodes: [],
            resolvedCurrency: "USD");
        return new TradovateInstrumentResolutionResult(
            [mapping],
            [canonical],
            diagnostics: [],
            TradovateInstrumentResolutionOverallStatus.ReadyForPreview);
    }

    private static TradovateInstrumentResolutionResult ProposedResolution()
    {
        var proposal = new TradovateInstrumentCreationProposal(
            "MNQ",
            "Micro E-mini Nasdaq-100",
            Domain.Instruments.AssetClass.Futures,
            "CME",
            "USD",
            0.25m,
            0.50m,
            ["MNQU6"],
            "Verified test profile");
        var mapping = new TradovateBrokerSymbolMapping(
            "MNQU6",
            "MNQ",
            TradovateInstrumentResolutionStatus.ProposedCreation,
            existingInstrumentId: null,
            isExistingInstrumentActive: null,
            diagnosticCodes: []);
        var canonical = new TradovateCanonicalInstrumentResolution(
            "MNQ",
            ["MNQU6"],
            0.25m,
            TradovateInstrumentResolutionStatus.ProposedCreation,
            existingInstrumentId: null,
            isExistingInstrumentActive: null,
            matchingInstrumentIds: [],
            proposal,
            diagnosticCodes: [],
            resolvedCurrency: "USD");
        return new TradovateInstrumentResolutionResult(
            [mapping],
            [canonical],
            diagnostics: [],
            TradovateInstrumentResolutionOverallStatus.ReadyForPreview);
    }

    private static void AssertDiagnostic(
        TradovateImportPreparationResult result,
        string code,
        TradovateReconstructionDiagnosticSeverity severity =
            TradovateReconstructionDiagnosticSeverity.Error)
    {
        IReadOnlyList<TradovateImportPreparationDiagnostic> matching = result.Diagnostics
            .Where(item => item.Code == code)
            .ToArray();
        Assert.NotEmpty(matching);
        Assert.All(matching, diagnostic => Assert.Equal(severity, diagnostic.Severity));
    }

    private sealed class RecordingAccountReader(TradingAccountDetails? details)
        : ITradingAccountReader
    {
        public int CallCount { get; private set; }

        public Guid RequestedId { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<IReadOnlyList<AccountListItem>> GetAllAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TradingAccountDetails?> GetByIdAsync(
            Guid accountId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            RequestedId = accountId;
            CancellationToken = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(details);
        }
    }
}
