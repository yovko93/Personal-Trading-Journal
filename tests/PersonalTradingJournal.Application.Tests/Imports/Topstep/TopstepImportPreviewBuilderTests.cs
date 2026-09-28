using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Imports.Topstep;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;

namespace PersonalTradingJournal.Application.Tests.Imports.Topstep;

public sealed class TopstepImportPreviewBuilderTests
{
    private static readonly Guid AccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Parser _parser = new();
    private readonly Reconstructor _reconstructor = new();
    private readonly Catalog _catalog = new();
    private readonly Accounts _accounts = new();
    private TopstepImportPreviewBuilder Builder() => new(_parser, _reconstructor, new(_catalog, _accounts));

    [Fact]
    public async Task ValidPreviewExposesExactClosedRowsCountsIdentityAndExplicitReviewGate()
    {
        byte[] data = Encoding.UTF8.GetBytes("synthetic source bytes");
        using var stream = new MemoryStream(data);
        TopstepImportPreview preview = await Builder().BuildAsync("C:/private/folder/example.csv", stream, AccountId,
            TopstepCostInterpretation.SeparateReportedRoundTurnTotalsUsd);
        Assert.True(stream.CanRead);
        Assert.Equal(data, stream.ToArray());
        Assert.Equal("example.csv", preview.SourceIdentity.FileName);
        Assert.Equal(data.Length, preview.SourceIdentity.ByteCount);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(data)), preview.SourceIdentity.ContentSha256);
        Assert.Equal(AccountId, preview.SelectedAccountId);
        Assert.Equal(_accounts.Item, preview.DestinationAccount);
        Assert.Equal(1, preview.Summary.AcceptedRowCount);
        Assert.Equal(0, preview.Summary.RejectedRowCount);
        Assert.Equal(1, preview.Summary.ClosedRowCandidateCount);
        Assert.Equal(1, preview.Summary.ExistingContractResolutionCount);
        Assert.Equal(new TopstepPreviewTotals("USD", 5m, 5m, 1.44m, 1m, 2.56m), preview.Summary.ReconciledTotals);
        TopstepPreviewCandidate row = Assert.Single(preview.Candidates);
        Assert.Equal("Topstep closed-row record", row.RecordKind);
        Assert.Same(_parser.Rows[0], row.SourceRow);
        Assert.Equal(2, row.SourceLineNumber);
        Assert.Equal(20000.125m, row.EntryPrice);
        Assert.Equal(20001.375m, row.ExitPrice);
        Assert.Equal(2m, row.ClosedRowQuantity);
        Assert.Equal(2.56m, row.CalculatedNet);
        Assert.False(row.ArePositionBoundariesVerified);
        Assert.Equal(TopstepPreviewState.RequiresReview, preview.State);
        Assert.False(preview.MeetsReviewRequirements(null));
        Assert.False(preview.MeetsReviewRequirements(new(preview.SnapshotFingerprint, [])));
        Assert.True(preview.MeetsReviewRequirements(Review(preview)));
        TopstepPreviewDiagnostic warning = Assert.Single(preview.Diagnostics);
        Assert.Equal(TopstepPreviewDiagnosticStage.Reconstruction, warning.Stage);
        Assert.Equal(new TopstepSourceReference(1, 2), Assert.Single(warning.SourceReferences));
        Assert.Single(preview.ReviewRequirements);
    }

    [Fact]
    public async Task ProposalExposesFullSpecificationsAndRequiresSeparateCreationApproval()
    {
        _catalog.Items = [];
        TopstepImportPreview preview = await Build();
        Assert.True(preview.IsEligibleForReview);
        Assert.Equal(1, preview.Summary.ProposedInstrumentCount);
        Assert.Equal(1, preview.Summary.ProposedContractResolutionCount);
        TopstepInstrumentCreationProposal proposal = Assert.Single(preview.CreationProposals);
        Assert.Equal(("MNQ", "CME", "USD", .25m, .5m, 2m),
            (proposal.CanonicalSymbol, proposal.Exchange, proposal.Currency, proposal.TickSize, proposal.TickValue, proposal.PointValue));
        Assert.Equal(AssetClass.Futures, proposal.AssetClass);
        Assert.NotEmpty(proposal.DisplayName);
        Assert.NotEmpty(proposal.MetadataSource);
        Assert.False(preview.MeetsReviewRequirements(new(preview.SnapshotFingerprint,
            preview.ReviewRequirements.Where(r => r.Kind == TopstepPreviewReviewKind.WarningAcknowledgment).Select(r => r.Key).ToArray())));
        Assert.True(preview.MeetsReviewRequirements(Review(preview)));
        Assert.Empty(_catalog.Items);
    }

    [Theory]
    [InlineData("account")]
    [InlineData("instrument")]
    [InlineData("economics")]
    [InlineData("parser")]
    public async Task StageErrorsBlockEvenWithEveryRequirementAccepted(string stage)
    {
        if (stage == "account") _accounts.Item = _accounts.Item! with { ProviderName = "Other" };
        if (stage == "instrument") _catalog.Items = [_catalog.Items[0] with { TickValue = 1m, PointValue = 4m }];
        if (stage == "economics") _parser.Rows = [Row() with { SourceReportedPnL = 6m }];
        if (stage == "parser") _parser.Invalid = true;
        TopstepImportPreview preview = await Build();
        Assert.Equal(TopstepPreviewState.Blocked, preview.State);
        Assert.False(preview.MeetsReviewRequirements(Review(preview)));
        TopstepPreviewDiagnosticStage expected = stage switch
        {
            "account" => TopstepPreviewDiagnosticStage.Account,
            "instrument" => TopstepPreviewDiagnosticStage.Instrument,
            "economics" => TopstepPreviewDiagnosticStage.Economics,
            _ => TopstepPreviewDiagnosticStage.Csv,
        };
        Assert.Contains(preview.Diagnostics, d => d.Stage == expected && d.Severity == TopstepPreviewSeverity.Error);
        Assert.All(preview.Diagnostics, d => Assert.False(string.IsNullOrWhiteSpace(d.RecoveryGuidance)));
        if (stage == "parser")
        {
            Assert.Equal(1, preview.Summary.RejectedRowCount);
            Assert.Empty(preview.Candidates);
            Assert.Null(preview.Summary.ReconciledTotals);
            Assert.Contains(preview.Diagnostics, d => d.SourceLineNumber == 2 && d.FieldName == "Size");
        }
        else Assert.Single(preview.Candidates); // Invalid destination must not hide source rows needing review.
    }

    [Fact]
    public async Task AllAffectedGroupingRowsRemainVisibleAndMustBeAcknowledged()
    {
        _parser.Rows = [Row(), Row() with { Id = "SYNTH-2", SourceRecordIndex = 2, SourceLineNumber = 3 }];
        _reconstructor.GroupingWarning = true;
        TopstepImportPreview preview = await Build();
        Assert.Equal(2, preview.Summary.ClosedRowCandidateCount);
        Assert.Equal(2, preview.Summary.WarningCount);
        Assert.All(preview.Diagnostics, d => Assert.Equal(2, d.SourceReferences.Count));
        Assert.Equal(2, preview.ReviewRequirements.Count);
        Assert.False(preview.MeetsReviewRequirements(new(preview.SnapshotFingerprint, [preview.ReviewRequirements[0].Key])));
        Assert.True(preview.MeetsReviewRequirements(Review(preview)));
    }

    [Fact]
    public async Task IdenticalRebuildsAreStableAcrossCultureAndDoNotCacheReferenceReads()
    {
        TopstepImportPreviewBuilder builder = Builder();
        TopstepImportPreview first = await Build(builder);
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            TopstepImportPreview second = await Build(builder);
            Assert.Equal(first.SnapshotFingerprint, second.SnapshotFingerprint);
            Assert.True(second.MeetsReviewRequirements(Review(first)));
        }
        finally { CultureInfo.CurrentCulture = previous; }
        Assert.Equal(2, _accounts.ReadCount);
        Assert.Equal(2, _catalog.ReadCount);
    }

    [Theory]
    [InlineData("bytes")]
    [InlineData("filename")]
    [InlineData("account")]
    [InlineData("accountFacts")]
    [InlineData("instrumentFacts")]
    [InlineData("ambiguity")]
    public async Task ChangedSourceSelectionOrReferenceSnapshotInvalidatesOldReview(string change)
    {
        TopstepImportPreviewBuilder builder = Builder();
        TopstepImportPreview first = await Build(builder);
        if (change == "account") _accounts.Item = _accounts.Item! with { Id = Guid.NewGuid() };
        if (change == "accountFacts") _accounts.Item = _accounts.Item! with { Name = "Changed name", IsActive = false };
        if (change == "instrumentFacts") _catalog.Items = [_catalog.Items[0] with { IsActive = false }];
        if (change == "ambiguity") _catalog.Items = [_catalog.Items[0], _catalog.Items[0] with { Id = Guid.NewGuid() }];
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(change == "bytes" ? "changed source" : "synthetic"));
        TopstepImportPreview second = await builder.BuildAsync(change == "filename" ? "renamed.csv" : "synthetic.csv", source,
            _accounts.Item!.Id, TopstepCostInterpretation.SeparateReportedRoundTurnTotalsUsd);
        Assert.NotEqual(first.SnapshotFingerprint, second.SnapshotFingerprint);
        Assert.False(second.MeetsReviewRequirements(Review(first)));
        Assert.Equal(change == "bytes", first.SourceIdentity.ContentSha256 != second.SourceIdentity.ContentSha256);
        Assert.True(first.IsEligibleForReview); // Immutable original result is not rewritten by a rerun.
        Assert.Single(first.Instruments[0].MatchingInstruments);
        if (change == "ambiguity") Assert.Equal(TopstepPreviewState.Blocked, second.State);
    }

    [Fact]
    public async Task DeletedOrClearedAccountDoesNotReuseTheLastSuccessfulSelection()
    {
        TopstepImportPreviewBuilder builder = Builder();
        Assert.True((await Build(builder)).IsEligibleForReview);
        _accounts.Item = null;
        TopstepImportPreview deleted = await Build(builder);
        Assert.Contains(deleted.Diagnostics, d => d.Code == TopstepReferenceDiagnosticCodes.AccountNotFound);
        using var stream = new MemoryStream([1]);
        TopstepImportPreview cleared = await builder.BuildAsync("next.csv", stream, null);
        Assert.Null(cleared.SelectedAccountId);
        Assert.False(cleared.IsEligibleForReview);
        Assert.Contains(cleared.Diagnostics, d => d.Code == TopstepReferenceDiagnosticCodes.AccountSelectionRequired);
    }

    [Fact]
    public async Task PreCancellationAndParserFailureReleaseTheBuilderForAnotherRequest()
    {
        TopstepImportPreviewBuilder builder = Builder();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Build(builder, cancellation.Token));
        Assert.Equal(0, _parser.ReadCount);
        _parser.Failure = new IOException("Synthetic read failure");
        Assert.Same(_parser.Failure, await Assert.ThrowsAsync<IOException>(() => Build(builder)));
        _parser.Failure = null;
        Assert.True((await Build(builder)).IsEligibleForReview);
    }

    [Fact]
    public async Task ConcurrentRequestIsRejectedWithoutReadingItsStreamAndCancellationAllowsRetry()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _parser.OnRead = async token => { entered.SetResult(); await release.Task.WaitAsync(token); };
        TopstepImportPreviewBuilder builder = Builder();
        using var cancellation = new CancellationTokenSource();
        Task<TopstepImportPreview> pending = Build(builder, cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using var other = new MemoryStream([1, 2]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => builder.BuildAsync("other.csv", other, AccountId));
        Assert.Equal(0, other.Position);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        _parser.OnRead = null;
        Assert.True((await Build(builder)).IsEligibleForReview);
        Assert.Equal(2, _parser.ReadCount);
    }

    [Fact]
    public async Task CancellationTokenReachesEveryStageAndReaders()
    {
        using var cancellation = new CancellationTokenSource();
        await Build(cancellationToken: cancellation.Token);
        Assert.Equal(cancellation.Token, _parser.Token);
        Assert.Equal(cancellation.Token, _reconstructor.Token);
        Assert.Equal(cancellation.Token, _accounts.Token);
        Assert.Equal(cancellation.Token, _catalog.Token);
    }

    [Fact]
    public async Task PartialSeekableStreamIsRejectedInsteadOfFingerprintingATruncatedFile()
    {
        using var source = new MemoryStream([1, 2]);
        source.Position = 1;
        await Assert.ThrowsAsync<ArgumentException>(() => Builder().BuildAsync("partial.csv", source, AccountId));
        Assert.Equal(0, _parser.ReadCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SummaryOverflowOrPrecisionLossBlocksWithoutRoundingIndividualRows(bool overflow)
    {
        decimal gross = overflow ? 60000000000000000000000000000m : 10000000000000000000000000000m;
        TopstepSourceRow first = Row() with { EntryPrice = 0, ExitPrice = gross / 2, Size = 1,
            SourceReportedPnL = gross, SourceReportedFees = 0, SourceReportedCommissions = 0 };
        TopstepSourceRow second = first with { Id = "SYNTH-2", SourceRecordIndex = 2, SourceLineNumber = 3,
            ExitPrice = overflow ? gross / 2 : .005m, SourceReportedPnL = overflow ? gross : .01m };
        _parser.Rows = [first, second];
        TopstepImportPreview preview = await Build();
        Assert.True(preview.Preparation.Economics.IsEconomicallyReconciled);
        Assert.Equal(second.SourceReportedPnL, preview.Candidates[1].CalculatedNet);
        Assert.False(preview.IsEligibleForReview);
        Assert.Null(preview.Summary.ReconciledTotals);
        Assert.Contains(preview.Diagnostics, d => d.Code == (overflow ? "TOTALS_OVERFLOW" : "TOTALS_PRECISION_LOSS"));
    }

    private async Task<TopstepImportPreview> Build(TopstepImportPreviewBuilder? builder = null, CancellationToken cancellationToken = default)
    {
        using var source = new MemoryStream(Encoding.UTF8.GetBytes("synthetic"));
        return await (builder ?? Builder()).BuildAsync("synthetic.csv", source, AccountId,
            TopstepCostInterpretation.SeparateReportedRoundTurnTotalsUsd, cancellationToken: cancellationToken);
    }
    private static TopstepPreviewReview Review(TopstepImportPreview preview) => new(preview.SnapshotFingerprint,
        preview.ReviewRequirements.Select(r => r.Key).ToArray());
    private static TopstepSourceRow Row() => new(1, 2, "SYNTH-1", "MNQZ6", DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch.AddSeconds(1), 20000.125m, 20001.375m, 1.44m, 5m, 2m, TopstepTradeType.Long,
        DateTimeOffset.UnixEpoch, TimeSpan.FromSeconds(1), "00:00:01", 1m);

    private sealed class Parser : ITopstepCsvParser
    {
        public TopstepSourceRow[] Rows { get; set; } = [Row()];
        public bool Invalid { get; set; }
        public int ReadCount { get; private set; }
        public Exception? Failure { get; set; }
        public Func<CancellationToken, Task>? OnRead { get; set; }
        public CancellationToken Token { get; private set; }
        public async Task<TopstepCsvParseResult> ParseAsync(Stream source, CancellationToken cancellationToken = default)
        {
            Token = cancellationToken; ReadCount++;
            if (OnRead is not null) await OnRead(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (Failure is not null) throw Failure;
            return Invalid ? new([], [new(TopstepCsvDiagnosticSeverity.Error, "INVALID_SIZE", 1, 2, "Size", "Size must be positive.")], 1, 1, true)
                : new(Rows, [], Rows.Length, 0, true);
        }
    }
    private sealed class Reconstructor : ITopstepTradeCandidateReconstructor
    {
        public bool GroupingWarning { get; set; }
        public CancellationToken Token { get; private set; }
        public TopstepTradeReconstructionResult Reconstruct(TopstepCsvParseResult source, CancellationToken cancellationToken = default)
        {
            Token = cancellationToken; cancellationToken.ThrowIfCancellationRequested();
            if (!source.IsCompleteInputValid) return new(source, [], []);
            TopstepSourceReference[] references = source.Rows.Select(r => new TopstepSourceReference(r.SourceRecordIndex, r.SourceLineNumber)).ToArray();
            var warnings = new List<TopstepReconstructionDiagnostic>
            {
                new(TopstepReconstructionDiagnosticSeverity.Warning, TopstepReconstructionDiagnosticCodes.PositionBoundariesUnverified,
                    references, "Position boundaries are unverified; these are separate closed-row records."),
            };
            if (GroupingWarning) warnings.Add(new(TopstepReconstructionDiagnosticSeverity.Warning,
                TopstepReconstructionDiagnosticCodes.PositionGroupingAmbiguous, references, "Overlapping rows do not establish a common position."));
            return new(source, source.Rows.Select(r => new TopstepTradeCandidate(r)), warnings);
        }
    }
    private sealed class Catalog : IInstrumentReader
    {
        public IReadOnlyList<InstrumentListItem> Items { get; set; } = [new(Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "MNQ", "Micro E-mini Nasdaq-100", AssetClass.Futures, "CME", "USD", .25m, .5m, 2m, true)];
        public int ReadCount { get; private set; }
        public CancellationToken Token { get; private set; }
        public Task<IReadOnlyList<InstrumentListItem>> GetAllAsync(CancellationToken cancellationToken = default)
        { Token = cancellationToken; ReadCount++; cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(Items); }
        public Task<InstrumentDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Accounts : ITradingAccountReader
    {
        public TradingAccountDetails? Item { get; set; } = new(AccountId, "Synthetic Topstep", TradingAccountType.PropFunded,
            "Topstep", null, "USD", null, true, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        public int ReadCount { get; private set; }
        public CancellationToken Token { get; private set; }
        public Task<TradingAccountDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        { Token = cancellationToken; ReadCount++; cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(Item?.Id == id ? Item : null); }
        public Task<IReadOnlyList<AccountListItem>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
