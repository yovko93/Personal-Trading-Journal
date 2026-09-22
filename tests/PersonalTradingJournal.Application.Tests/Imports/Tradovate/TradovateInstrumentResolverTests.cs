using PersonalTradingJournal.Application.Imports.Tradovate;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Tests.Imports.Tradovate;

public sealed class TradovateInstrumentResolverTests
{
    [Theory]
    [InlineData("MNQU6", "MNQ")]
    [InlineData("MNQZ6", "MNQ")]
    [InlineData("NQH7", "NQ")]
    [InlineData("ESH27", "ES")]
    [InlineData("MESM6", "MES")]
    [InlineData("6EU6", "6E")]
    [InlineData("mnqu6", "MNQ")]
    public void ParsesSupportedContractSuffixWithoutChangingSourceText(
        string brokerSymbol,
        string expectedRoot)
    {
        Assert.True(TradovateFuturesContractSymbolParser.TryGetCanonicalSymbol(
            brokerSymbol,
            out string? root));
        Assert.Equal(expectedRoot, root);
    }

    [Theory]
    [InlineData("XF1", "X")]
    [InlineData("XG1", "X")]
    [InlineData("XH1", "X")]
    [InlineData("XJ1", "X")]
    [InlineData("XK1", "X")]
    [InlineData("XM1", "X")]
    [InlineData("XN1", "X")]
    [InlineData("XQ1", "X")]
    [InlineData("XU1", "X")]
    [InlineData("XV1", "X")]
    [InlineData("XX1", "X")]
    [InlineData("XZ1", "X")]
    public void RecognizesEveryCmeMonthCode(string source, string expectedRoot)
    {
        Assert.True(TradovateFuturesContractSymbolParser.TryGetCanonicalSymbol(
            source,
            out string? root));
        Assert.Equal(expectedRoot, root);
    }

    [Theory]
    [InlineData("MNQ")]
    [InlineData("MNQU")]
    [InlineData("MNQA6")]
    [InlineData("MNQU666")]
    [InlineData("MNQ U6")]
    [InlineData("6U6")]
    [InlineData("U6")]
    [InlineData("something-unexpected")]
    [InlineData("")]
    public void DoesNotStripUnsupportedOrAmbiguousSuffixes(string source)
    {
        Assert.False(TradovateFuturesContractSymbolParser.TryGetCanonicalSymbol(
            source,
            out string? root));
        Assert.Null(root);
    }

    [Fact]
    public async Task MissingMnqCreatesOneCompleteProposalForTwoContractSymbols()
    {
        var reader = new FakeInstrumentReader();
        var resolver = new TradovateInstrumentResolver(reader);
        TradovateExecutionReconstructionResult reconstruction = Reconstructed(
            ("MNQZ6", 0.25m),
            ("MNQU6", 0.25m));

        TradovateInstrumentResolutionResult result = await resolver.ResolveAsync(reconstruction);

        Assert.Equal(TradovateInstrumentResolutionOverallStatus.ReadyForPreview, result.Status);
        Assert.True(result.IsReadyForPreview);
        Assert.Equal(1, reader.ReadCount);
        Assert.Equal(2, result.BrokerSymbolMappings.Count);
        Assert.Equal(["MNQU6", "MNQZ6"],
            result.BrokerSymbolMappings.Select(mapping => mapping.BrokerSymbol));
        Assert.All(result.BrokerSymbolMappings, mapping =>
        {
            Assert.Equal("MNQ", mapping.CanonicalSymbol);
            Assert.Equal(TradovateInstrumentResolutionStatus.ProposedCreation, mapping.Status);
            Assert.Null(mapping.ExistingInstrumentId);
        });

        TradovateCanonicalInstrumentResolution canonical =
            Assert.Single(result.CanonicalInstrumentResolutions);
        Assert.Equal("MNQ", canonical.CanonicalSymbol);
        Assert.Equal(0.25m, canonical.SourceTickSize);
        Assert.Equal(["MNQU6", "MNQZ6"], canonical.SourceBrokerSymbols);
        TradovateInstrumentCreationProposal proposal = Assert.Single(result.CreationProposals);
        Assert.Same(proposal, canonical.CreationProposal);
        Assert.Equal("MNQ", proposal.CanonicalSymbol);
        Assert.Equal("Micro E-mini Nasdaq-100", proposal.DisplayName);
        Assert.Equal(AssetClass.Futures, proposal.AssetClass);
        Assert.Equal("CME", proposal.Exchange);
        Assert.Equal("USD", proposal.Currency);
        Assert.Equal(0.25m, proposal.TickSize);
        Assert.Equal(0.50m, proposal.TickValue);
        Assert.Equal(["MNQU6", "MNQZ6"], proposal.SourceBrokerSymbols);
        Assert.Contains("CME Group", proposal.MetadataSource, StringComparison.Ordinal);
        Assert.Empty(result.Diagnostics);
        Assert.Equal("MNQZ6", reconstruction.Executions[0].BrokerSymbol);
    }

    [Fact]
    public void PreviewReadinessRequiresAnActualCanonicalResolution()
    {
        var result = new TradovateInstrumentResolutionResult(
            [new TradovateBrokerSymbolMapping(
                "MNQU6",
                "MNQ",
                TradovateInstrumentResolutionStatus.ProposedCreation,
                existingInstrumentId: null,
                isExistingInstrumentActive: null,
                diagnosticCodes: [])],
            canonicalInstrumentResolutions: [],
            diagnostics: [],
            TradovateInstrumentResolutionOverallStatus.ReadyForPreview);

        Assert.False(result.IsReadyForPreview);
    }

    [Fact]
    public async Task ExistingMnqIsReusedWithoutProposalOrMetadataOverwrite()
    {
        Guid id = Guid.NewGuid();
        var reader = new FakeInstrumentReader(
            Existing(id, displayName: "My MNQ", exchange: "CME Globex"));
        var resolver = new TradovateInstrumentResolver(reader);

        TradovateInstrumentResolutionResult result = await resolver.ResolveAsync(
            Reconstructed(("MNQU6", 0.25m), ("MNQZ6", 0.25m)));

        Assert.True(result.IsReadyForPreview);
        Assert.Empty(result.CreationProposals);
        Assert.All(result.BrokerSymbolMappings, mapping =>
        {
            Assert.Equal(TradovateInstrumentResolutionStatus.ExistingInstrument, mapping.Status);
            Assert.Equal(id, mapping.ExistingInstrumentId);
            Assert.True(mapping.IsExistingInstrumentActive);
        });
        Assert.Equal("My MNQ", reader.Items[0].DisplayName);
        Assert.Equal("CME Globex", reader.Items[0].Exchange);
    }

    [Fact]
    public async Task InactiveExistingMnqIsReusedWithWarningAndWithoutActivation()
    {
        Guid id = Guid.NewGuid();
        var reader = new FakeInstrumentReader(Existing(id, isActive: false));
        var resolver = new TradovateInstrumentResolver(reader);

        TradovateInstrumentResolutionResult result = await resolver.ResolveAsync(
            Reconstructed(("MNQU6", 0.25m)));

        Assert.True(result.IsReadyForPreview);
        Assert.Empty(result.CreationProposals);
        TradovateBrokerSymbolMapping mapping = Assert.Single(result.BrokerSymbolMappings);
        Assert.Equal(id, mapping.ExistingInstrumentId);
        Assert.False(mapping.IsExistingInstrumentActive);
        Assert.False(reader.Items[0].IsActive);
        AssertDiagnostic(result,
            TradovateInstrumentResolutionDiagnosticCodes.ExistingInstrumentInactive,
            TradovateReconstructionDiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task DuplicateExistingCanonicalSymbolIsBlockedWithBothIds()
    {
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        var reader = new FakeInstrumentReader(
            Existing(firstId),
            Existing(secondId, symbol: "mnq"));
        var resolver = new TradovateInstrumentResolver(reader);

        TradovateInstrumentResolutionResult result = await resolver.ResolveAsync(
            Reconstructed(("MNQU6", 0.25m)));

        Assert.Equal(TradovateInstrumentResolutionOverallStatus.Blocked, result.Status);
        Assert.False(result.IsReadyForPreview);
        Assert.Empty(result.CreationProposals);
        Assert.Equal(TradovateInstrumentResolutionStatus.Blocked,
            Assert.Single(result.BrokerSymbolMappings).Status);
        TradovateInstrumentResolutionDiagnostic diagnostic = AssertDiagnostic(
            result,
            TradovateInstrumentResolutionDiagnosticCodes.MultipleExistingInstruments);
        Assert.Equal(new[] { firstId, secondId }.Order(), diagnostic.MatchingInstrumentIds);
    }

    [Theory]
    [InlineData("asset", "INSTRUMENT_ASSET_CLASS_MISMATCH")]
    [InlineData("tick-size", "INSTRUMENT_TICK_SIZE_MISMATCH")]
    [InlineData("currency", "INSTRUMENT_CURRENCY_MISMATCH")]
    [InlineData("tick-value", "INSTRUMENT_TICK_VALUE_MISMATCH")]
    public async Task MaterialExistingMnqMismatchBlocksResolution(
        string mismatch,
        string expectedCode)
    {
        InstrumentListItem item = Existing(
            Guid.NewGuid(),
            assetClass: mismatch == "asset" ? AssetClass.Equity : AssetClass.Futures,
            tickSize: mismatch == "tick-size" ? 0.5m : 0.25m,
            tickValue: mismatch == "tick-value" ? 1m : 0.5m,
            currency: mismatch == "currency" ? "EUR" : "USD");
        var resolver = new TradovateInstrumentResolver(new FakeInstrumentReader(item));

        TradovateInstrumentResolutionResult result = await resolver.ResolveAsync(
            Reconstructed(("MNQU6", 0.25m)));

        Assert.Equal(TradovateInstrumentResolutionOverallStatus.Blocked, result.Status);
        Assert.Empty(result.CreationProposals);
        AssertDiagnostic(result, expectedCode);
    }

    [Fact]
    public async Task ConflictingSourceTickSizesBlockCanonicalRootAndDoNotProposeMnq()
    {
        var resolver = new TradovateInstrumentResolver(new FakeInstrumentReader());

        TradovateInstrumentResolutionResult result = await resolver.ResolveAsync(
            Reconstructed(("MNQU6", 0.25m), ("MNQZ6", 0.5m)));

        Assert.Equal(TradovateInstrumentResolutionOverallStatus.Blocked, result.Status);
        Assert.Empty(result.CreationProposals);
        Assert.Null(Assert.Single(result.CanonicalInstrumentResolutions).SourceTickSize);
        AssertDiagnostic(result,
            TradovateInstrumentResolutionDiagnosticCodes.SourceTickSizeConflict);
    }

    [Fact]
    public async Task ProfileDoesNotOverrideContradictoryMnqSourceTickSize()
    {
        var resolver = new TradovateInstrumentResolver(new FakeInstrumentReader());

        TradovateInstrumentResolutionResult result = await resolver.ResolveAsync(
            Reconstructed(("MNQU6", 0.5m)));

        Assert.Equal(TradovateInstrumentResolutionOverallStatus.Blocked, result.Status);
        Assert.Empty(result.CreationProposals);
        AssertDiagnostic(result,
            TradovateInstrumentResolutionDiagnosticCodes.SourceProfileTickSizeMismatch);
    }

    [Fact]
    public async Task MissingSyntacticallyValidUnknownRootRequiresUserMetadata()
    {
        var resolver = new TradovateInstrumentResolver(new FakeInstrumentReader());

        TradovateInstrumentResolutionResult result = await resolver.ResolveAsync(
            Reconstructed(("NQH7", 0.25m)));

        Assert.Equal(TradovateInstrumentResolutionOverallStatus.RequiresUserInput,
            result.Status);
        Assert.False(result.IsReadyForPreview);
        Assert.Empty(result.CreationProposals);
        TradovateCanonicalInstrumentResolution canonical =
            Assert.Single(result.CanonicalInstrumentResolutions);
        Assert.Equal("NQ", canonical.CanonicalSymbol);
        Assert.Equal(TradovateInstrumentResolutionStatus.RequiresUserInput,
            canonical.Status);
        AssertDiagnostic(result,
            TradovateInstrumentResolutionDiagnosticCodes.InstrumentMetadataRequired,
            TradovateReconstructionDiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task ExistingUnknownRootMayBeReusedWhenItsAvailableFactsMatch()
    {
        Guid id = Guid.NewGuid();
        var reader = new FakeInstrumentReader(Existing(id, symbol: "NQ"));
        var resolver = new TradovateInstrumentResolver(reader);

        TradovateInstrumentResolutionResult result = await resolver.ResolveAsync(
            Reconstructed(("NQH7", 0.25m)));

        Assert.True(result.IsReadyForPreview);
        Assert.Equal(id, Assert.Single(result.BrokerSymbolMappings).ExistingInstrumentId);
        Assert.Empty(result.CreationProposals);
    }

    [Fact]
    public async Task UnrecognizedBrokerSymbolRequiresManualMappingWithoutGuessingRoot()
    {
        var resolver = new TradovateInstrumentResolver(new FakeInstrumentReader());

        TradovateInstrumentResolutionResult result = await resolver.ResolveAsync(
            Reconstructed(("unexpected-symbol", 0.25m)));

        Assert.Equal(TradovateInstrumentResolutionOverallStatus.RequiresUserInput,
            result.Status);
        TradovateBrokerSymbolMapping mapping = Assert.Single(result.BrokerSymbolMappings);
        Assert.Equal("unexpected-symbol", mapping.BrokerSymbol);
        Assert.Null(mapping.CanonicalSymbol);
        Assert.Empty(result.CanonicalInstrumentResolutions);
        Assert.Empty(result.CreationProposals);
        AssertDiagnostic(result,
            TradovateInstrumentResolutionDiagnosticCodes.UnrecognizedContractSymbol,
            TradovateReconstructionDiagnosticSeverity.Warning);
    }

    [Theory]
    [InlineData(TradovateReconstructionStatus.Blocked)]
    [InlineData(TradovateReconstructionStatus.Ambiguous)]
    [InlineData(TradovateReconstructionStatus.Incomplete)]
    public async Task IneligibleReconstructionDoesNotReadOrPropose(
        TradovateReconstructionStatus status)
    {
        var reader = new FakeInstrumentReader();
        var resolver = new TradovateInstrumentResolver(reader);

        TradovateInstrumentResolutionResult result = await resolver.ResolveAsync(
            Reconstructed(("MNQU6", 0.25m), status));

        Assert.Equal(TradovateInstrumentResolutionOverallStatus.Blocked, result.Status);
        Assert.Empty(result.BrokerSymbolMappings);
        Assert.Empty(result.CreationProposals);
        Assert.Equal(0, reader.ReadCount);
        AssertDiagnostic(result,
            TradovateInstrumentResolutionDiagnosticCodes.ReconstructionNotEligible);
    }

    [Fact]
    public async Task ZeroCandidateReconstructionDoesNotReadOrPropose()
    {
        var reader = new FakeInstrumentReader();
        var resolver = new TradovateInstrumentResolver(reader);
        var empty = new TradovateExecutionReconstructionResult(
            [], [], [], [], [], 0, TradovateReconstructionStatus.Reconstructed);

        TradovateInstrumentResolutionResult result = await resolver.ResolveAsync(empty);

        Assert.Equal(TradovateInstrumentResolutionOverallStatus.Blocked, result.Status);
        Assert.Equal(0, reader.ReadCount);
        Assert.Empty(result.CreationProposals);
    }

    [Fact]
    public async Task CandidateAndExecutionSymbolCoverageMustAgree()
    {
        var reader = new FakeInstrumentReader();
        var resolver = new TradovateInstrumentResolver(reader);
        TradovateExecutionReconstructionResult valid = Reconstructed(("MNQU6", 0.25m));
        TradovateReconstructedExecution orphan = Execution(
            "NQH7", ExecutionSide.Buy, 2, 0.25m);
        var malformed = new TradovateExecutionReconstructionResult(
            valid.Executions.Concat([orphan]),
            valid.Candidates,
            [], [], [], 1,
            TradovateReconstructionStatus.Reconstructed);

        TradovateInstrumentResolutionResult result = await resolver.ResolveAsync(malformed);

        Assert.Equal(TradovateInstrumentResolutionOverallStatus.Blocked, result.Status);
        Assert.Equal(0, reader.ReadCount);
        AssertDiagnostic(result,
            TradovateInstrumentResolutionDiagnosticCodes.ReconstructionSymbolCoverageMismatch);
    }

    [Fact]
    public async Task ReorderedInputProducesSameMappingsProposalAndDiagnostics()
    {
        var resolver = new TradovateInstrumentResolver(new FakeInstrumentReader());
        TradovateExecutionReconstructionResult firstInput = Reconstructed(
            ("MNQZ6", 0.25m), ("MNQU6", 0.25m));
        TradovateExecutionReconstructionResult secondInput = Reconstructed(
            ("MNQU6", 0.25m), ("MNQZ6", 0.25m));

        TradovateInstrumentResolutionResult first = await resolver.ResolveAsync(firstInput);
        TradovateInstrumentResolutionResult second = await resolver.ResolveAsync(secondInput);

        Assert.Equal(
            first.BrokerSymbolMappings.Select(mapping =>
                (mapping.BrokerSymbol, mapping.CanonicalSymbol, mapping.Status)),
            second.BrokerSymbolMappings.Select(mapping =>
                (mapping.BrokerSymbol, mapping.CanonicalSymbol, mapping.Status)));
        Assert.Equal(
            Assert.Single(first.CreationProposals).SourceBrokerSymbols,
            Assert.Single(second.CreationProposals).SourceBrokerSymbols);
        Assert.Equal(first.Diagnostics.Select(diagnostic => diagnostic.Code),
            second.Diagnostics.Select(diagnostic => diagnostic.Code));
    }

    [Fact]
    public async Task CancellationIsForwardedToReaderAndNotConvertedToDiagnostic()
    {
        var reader = new FakeInstrumentReader();
        var resolver = new TradovateInstrumentResolver(reader);
        using var source = new CancellationTokenSource();

        _ = await resolver.ResolveAsync(Reconstructed(("MNQU6", 0.25m)), source.Token);

        Assert.Equal(source.Token, reader.LastCancellationToken);
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            resolver.ResolveAsync(Reconstructed(("MNQU6", 0.25m)), source.Token));
        Assert.Equal(1, reader.ReadCount);
    }

    private static TradovateInstrumentResolutionDiagnostic AssertDiagnostic(
        TradovateInstrumentResolutionResult result,
        string code,
        TradovateReconstructionDiagnosticSeverity severity =
            TradovateReconstructionDiagnosticSeverity.Error)
    {
        TradovateInstrumentResolutionDiagnostic diagnostic = Assert.Single(
            result.Diagnostics,
            item => item.Code == code);
        Assert.Equal(severity, diagnostic.Severity);
        return diagnostic;
    }

    private static InstrumentListItem Existing(
        Guid id,
        string symbol = "MNQ",
        string displayName = "Micro E-mini Nasdaq-100",
        AssetClass assetClass = AssetClass.Futures,
        string? exchange = "CME",
        string currency = "USD",
        decimal tickSize = 0.25m,
        decimal tickValue = 0.50m,
        bool isActive = true) => new(
            id, symbol, displayName, assetClass, exchange, currency,
            tickSize, tickValue, tickValue / tickSize, isActive);

    private static TradovateExecutionReconstructionResult Reconstructed(
        params (string Symbol, decimal TickSize)[] symbols) =>
        Reconstructed(symbols, TradovateReconstructionStatus.Reconstructed);

    private static TradovateExecutionReconstructionResult Reconstructed(
        (string Symbol, decimal TickSize) symbol,
        TradovateReconstructionStatus status) =>
        Reconstructed([symbol], status);

    private static TradovateExecutionReconstructionResult Reconstructed(
        (string Symbol, decimal TickSize)[] symbols,
        TradovateReconstructionStatus status)
    {
        var executions = new List<TradovateReconstructedExecution>();
        var candidates = new List<TradovateTradeCandidate>();
        for (int index = 0; index < symbols.Length; index++)
        {
            (string symbol, decimal tickSize) = symbols[index];
            int recordIndex = index + 1;
            TradovateReconstructedExecution buy = Execution(
                symbol, ExecutionSide.Buy, recordIndex, tickSize);
            TradovateReconstructedExecution sell = Execution(
                symbol, ExecutionSide.Sell, recordIndex, tickSize);
            executions.AddRange([buy, sell]);
            candidates.Add(new TradovateTradeCandidate(
                symbol,
                TradeDirection.Long,
                [buy, sell],
                buy.SourceLocalTimestamp,
                sell.SourceLocalTimestamp,
                openingQuantity: 1m,
                closingQuantity: 1m,
                signedPositionAtEnd: 0m,
                TradovateReconstructionStatus.Reconstructed,
                [recordIndex],
                []));
        }

        return new TradovateExecutionReconstructionResult(
            executions, candidates, [], [], [], symbols.Length, status);
    }

    private static TradovateReconstructedExecution Execution(
        string symbol,
        ExecutionSide side,
        int recordIndex,
        decimal tickSize) => new(
            symbol,
            side,
            $"{side}-{recordIndex}",
            1m,
            100m,
            new DateTime(2026, 9, 10, 9, side == ExecutionSide.Buy ? 0 : 1, 0,
                DateTimeKind.Unspecified),
            tickSize,
            [recordIndex],
            [recordIndex + 1]);

    private sealed class FakeInstrumentReader(params InstrumentListItem[] items) : IInstrumentReader
    {
        public IReadOnlyList<InstrumentListItem> Items { get; } = Array.AsReadOnly(items);

        public int ReadCount { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public Task<IReadOnlyList<InstrumentListItem>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            ReadCount++;
            LastCancellationToken = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Items);
        }

        public Task<InstrumentDetails?> GetByIdAsync(
            Guid instrumentId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
