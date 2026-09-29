using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Imports.Topstep;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;

namespace PersonalTradingJournal.Application.Tests.Imports.Topstep;

public sealed class TopstepReferencePreparationTests
{
    private static readonly Guid AccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid InstrumentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private const TopstepCostInterpretation Costs = TopstepCostInterpretation.SeparateReportedRoundTurnTotalsUsd;
    private readonly Catalog _catalog = new();
    private readonly Accounts _accounts = new();
    private TopstepReferencePreparationService Service => new(_catalog, _accounts);

    [Fact]
    public async Task ExistingVerifiedMnqMapsEveryRowAndFeedsItsPricingIntoEconomics()
    {
        TopstepTradeReconstructionResult source = Source(Row(1), Row(2));
        TopstepReferencePreparationResult result = await Service.PrepareAsync(source, AccountId, Costs);
        Assert.True(result.IsReadyForPreview);
        Assert.False(result.RequiresInstrumentCreationApproval);
        TopstepInstrumentResolution instrument = Assert.Single(result.Instruments);
        Assert.Equal(TopstepInstrumentResolutionStatus.ExistingInstrument, instrument.Status);
        Assert.Equal("MNQ", instrument.Identity!.CanonicalSymbol);
        Assert.Equal('Z', instrument.Identity.MonthCode);
        Assert.Equal("6", instrument.Identity.YearCode);
        Assert.Equal(InstrumentId, instrument.ExistingInstrument!.Id);
        Assert.Equal(2m, instrument.Pricing!.PointValue);
        Assert.Equal("USD", instrument.Pricing.Currency);
        Assert.Equal(2, result.Rows.Count);
        Assert.All(result.Rows, r =>
        {
            Assert.Equal("Topstep", r.SourceProvider);
            Assert.Equal(AccountId, r.DestinationTradingAccountId);
            Assert.Same(instrument, r.Instrument);
            Assert.Equal(2.56m, r.Economics.NetPnL);
            Assert.Same(instrument.Pricing, r.Economics.Pricing);
        });
        Assert.Equal(["SYNTH-1", "SYNTH-2"], result.Rows.Select(r => r.SourceId));
        Assert.Same(source, result.Economics.Source);
        Assert.Single(result.Economics.Source.Diagnostics);
        Assert.Equal(1, _catalog.ReadCount);
        Assert.Equal(AccountId, _accounts.RequestedId);
    }

    [Fact]
    public async Task MissingMnqProducesOneCompleteProposalAcrossDistinctContractsWithoutCreatingAnything()
    {
        _catalog.Items = [];
        TopstepReferencePreparationResult result = await Service.PrepareAsync(Source(Row(1), Row(2) with { ContractName = "MNQU6" }), AccountId, Costs);
        Assert.True(result.IsReadyForPreview);
        Assert.True(result.RequiresInstrumentCreationApproval);
        Assert.Equal(2, result.Rows.Count);
        Assert.All(result.Instruments, i => Assert.Equal(TopstepInstrumentResolutionStatus.ProposedCreation, i.Status));
        TopstepInstrumentCreationProposal proposal = Assert.Single(result.CreationProposals);
        Assert.Equal("MNQ", proposal.CanonicalSymbol);
        Assert.Equal("Micro E-mini Nasdaq-100", proposal.DisplayName);
        Assert.Equal(AssetClass.Futures, proposal.AssetClass);
        Assert.Equal("CME", proposal.Exchange);
        Assert.Equal("USD", proposal.Currency);
        Assert.Equal(0.25m, proposal.TickSize);
        Assert.Equal(0.50m, proposal.TickValue);
        Assert.Equal(2m, proposal.PointValue);
        Assert.Contains("cmegroup.com", proposal.MetadataSource);
        Assert.Empty(_catalog.Items);
        Assert.All(result.Instruments, i => Assert.Empty(i.MatchingInstruments));
    }

    [Fact]
    public async Task AmbiguityIncludesInactiveInvalidAndUnverifiedMatchesBeforeFiltering()
    {
        InstrumentListItem second = Instrument() with { Id = Guid.NewGuid(), IsActive = false, TickValue = 9m };
        _catalog.Items = [Instrument(), second];
        TopstepReferencePreparationResult result = await Service.PrepareAsync(Source(Row(1)), AccountId, Costs,
            [new("MNQZ6", Instrument())]);
        AssertBlocked(result, TopstepReferenceDiagnosticCodes.MultipleInstrumentMatches);
        Assert.Equal(2, Assert.Single(result.Instruments).MatchingInstruments.Count);
        Assert.Null(result.Instruments[0].ExistingInstrument);
        Assert.Null(result.Instruments[0].Pricing);
        Assert.Null(Assert.Single(result.Economics.Rows).NetPnL);
    }

    [Theory]
    [InlineData("size")]
    [InlineData("value")]
    [InlineData("point")]
    [InlineData("currency")]
    [InlineData("exchange")]
    [InlineData("asset")]
    [InlineData("incomplete")]
    public async Task MnqCatalogSpecificationsMustAgreeWithIndependentProfile(string mismatch)
    {
        _catalog.Items = [mismatch switch
        {
            "size" => Instrument() with { TickSize = 0.5m, TickValue = 1m },
            "value" => Instrument() with { TickValue = 1m, PointValue = 4m },
            "point" => Instrument() with { PointValue = 3m },
            "currency" => Instrument() with { Currency = "EUR" },
            "exchange" => Instrument() with { Exchange = "OTHER" },
            "asset" => Instrument() with { AssetClass = AssetClass.Equity },
            _ => Instrument() with { DisplayName = " " },
        }];
        AssertBlocked(await Service.PrepareAsync(Source(Row(1)), AccountId, Costs),
            TopstepReferenceDiagnosticCodes.InstrumentSpecificationMismatch);
    }

    [Fact]
    public async Task ExplicitInactiveReferencesAreWarnedAndNeverReactivated()
    {
        _catalog.Items = [Instrument() with { IsActive = false }];
        _accounts.Item = Account() with { IsActive = false };
        TopstepReferencePreparationResult result = await Service.PrepareAsync(Source(Row(1)), AccountId, Costs);
        Assert.True(result.IsReadyForPreview);
        Assert.Contains(result.Diagnostics, d => d.Code == TopstepReferenceDiagnosticCodes.AccountInactive && d.Severity == TopstepReferenceDiagnosticSeverity.Warning);
        Assert.Contains(result.Diagnostics, d => d.Code == TopstepReferenceDiagnosticCodes.InstrumentInactive && d.Severity == TopstepReferenceDiagnosticSeverity.Warning);
        Assert.False(result.Account!.IsActive);
        Assert.False(result.Instruments[0].ExistingInstrument!.IsActive);
        Assert.False(_accounts.Item.IsActive);
        Assert.False(_catalog.Items[0].IsActive);
    }

    [Theory]
    [InlineData("MNQ")]
    [InlineData("MNQZ2026")]
    [InlineData("MNQ1!")]
    [InlineData("MNQF6")]
    [InlineData("MNQZ6 extra")]
    public async Task UnverifiedOrUnsupportedContractCodesAreNotGuessed(string contract)
    {
        AssertBlocked(await Service.PrepareAsync(Source(Row(1) with { ContractName = contract }), AccountId, Costs),
            TopstepReferenceDiagnosticCodes.UnrecognizedContract);
    }

    [Fact]
    public async Task LowerCaseAndTwoDigitYearRetainExactSourceIdentityWithoutExpandingExpiry()
    {
        TopstepReferencePreparationResult result = await Service.PrepareAsync(Source(Row(1) with { ContractName = "mnqz26" }), AccountId, Costs);
        Assert.True(result.IsReadyForPreview);
        TopstepContractIdentity identity = result.Instruments[0].Identity!;
        Assert.Equal("mnqz26", identity.SourceContract);
        Assert.Equal("MNQ", identity.CanonicalSymbol);
        Assert.Equal("26", identity.YearCode);
    }

    [Fact]
    public async Task UnknownRootRequiresCompleteVerifiedExistingSnapshotAndCannotProposeFromObservedPnl()
    {
        TopstepTradeReconstructionResult source = Source(Row(1) with { ContractName = "ESZ6", SourceReportedPnL = 125m });
        _catalog.Items = [];
        AssertBlocked(await Service.PrepareAsync(source, AccountId, Costs), TopstepReferenceDiagnosticCodes.InstrumentMetadataRequired);
        InstrumentListItem es = Instrument() with { Symbol = "ES", TickValue = 12.5m, PointValue = 50m };
        _catalog.Items = [es];
        AssertBlocked(await Service.PrepareAsync(source, AccountId, Costs), TopstepReferenceDiagnosticCodes.InstrumentVerificationRequired);
        TopstepReferencePreparationResult verified = await Service.PrepareAsync(source, AccountId, Costs, [new("ESZ6", es)]);
        Assert.True(verified.IsReadyForPreview);
        Assert.Equal(122.56m, verified.Rows[0].Economics.NetPnL);
        AssertBlocked(await Service.PrepareAsync(source, AccountId, Costs, [new("ESH7", es)]), TopstepReferenceDiagnosticCodes.InstrumentVerificationRequired);
        _catalog.Items = [es with { Exchange = "OTHER" }];
        AssertBlocked(await Service.PrepareAsync(source, AccountId, Costs, [new("ESZ6", es)]), TopstepReferenceDiagnosticCodes.InstrumentVerificationRequired);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingSelectionNeverQueriesOrReusesAnAccount(bool empty)
    {
        TopstepReferencePreparationService service = Service;
        Assert.True((await service.PrepareAsync(Source(Row(1)), AccountId, Costs)).IsReadyForPreview);
        int calls = _accounts.ReadCount;
        TopstepReferencePreparationResult result = await service.PrepareAsync(Source(Row(1)), empty ? Guid.Empty : null, Costs);
        AssertBlocked(result, TopstepReferenceDiagnosticCodes.AccountSelectionRequired);
        Assert.Empty(result.Rows);
        Assert.Null(result.Account);
        Assert.Equal(calls, _accounts.ReadCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Tradovate")]
    [InlineData("TopstepX")]
    [InlineData("NotTopstep")]
    public async Task IncompatibleProviderRequiresExplicitCorrectionNotAliasOrSubstringMatching(string? provider)
    {
        _accounts.Item = Account() with { ProviderName = provider };
        TopstepReferencePreparationResult result = await Service.PrepareAsync(Source(Row(1)), AccountId, Costs);
        AssertBlocked(result, TopstepReferenceDiagnosticCodes.AccountProviderMismatch);
        Assert.Empty(result.Rows);
        Assert.Equal(provider, result.Account!.ProviderName);
    }

    [Fact]
    public async Task ProviderComparisonUsesExistingTrimmedCaseInsensitiveConvention()
    {
        _accounts.Item = Account() with { ProviderName = " topSTEP " };
        Assert.True((await Service.PrepareAsync(Source(Row(1)), AccountId, Costs)).IsReadyForPreview);
    }

    [Fact]
    public async Task AccountCurrencyMismatchBlocksWithoutConvertingAmounts()
    {
        _accounts.Item = Account() with { Currency = "EUR" };
        AssertBlocked(await Service.PrepareAsync(Source(Row(1)), AccountId, Costs), TopstepReferenceDiagnosticCodes.AccountCurrencyMismatch);
    }

    [Fact]
    public async Task EveryRerunReadsCurrentCardinalityIdentityEconomicsAndAccountFacts()
    {
        TopstepReferencePreparationService service = Service;
        TopstepTradeReconstructionResult source = Source(Row(1));
        _catalog.Items = [];
        TopstepReferencePreparationResult proposed = await service.PrepareAsync(source, AccountId, Costs);
        _catalog.Items = [Instrument()];
        TopstepReferencePreparationResult existing = await service.PrepareAsync(source, AccountId, Costs);
        Assert.True(existing.IsReadyForPreview);
        Assert.Equal(TopstepInstrumentResolutionStatus.ProposedCreation, proposed.Instruments[0].Status);
        _catalog.Items = [Instrument(), Instrument() with { Id = Guid.NewGuid() }];
        AssertBlocked(await service.PrepareAsync(source, AccountId, Costs), TopstepReferenceDiagnosticCodes.MultipleInstrumentMatches);
        Assert.Single(existing.Instruments[0].MatchingInstruments);
        _catalog.Items = [Instrument() with { TickValue = 2m, PointValue = 8m }];
        AssertBlocked(await service.PrepareAsync(source, AccountId, Costs), TopstepReferenceDiagnosticCodes.InstrumentSpecificationMismatch);
        _catalog.Items = [Instrument() with { Id = Guid.NewGuid() }];
        TopstepReferencePreparationResult replacement = await service.PrepareAsync(source, AccountId, Costs);
        Assert.NotEqual(existing.Instruments[0].ExistingInstrument!.Id, replacement.Instruments[0].ExistingInstrument!.Id);
        _accounts.Item = null;
        AssertBlocked(await service.PrepareAsync(source, AccountId, Costs), TopstepReferenceDiagnosticCodes.AccountNotFound);
        Assert.NotNull(existing.Account);
        _accounts.Item = Account() with { ProviderName = "Other" };
        AssertBlocked(await service.PrepareAsync(source, AccountId, Costs), TopstepReferenceDiagnosticCodes.AccountProviderMismatch);
        Assert.Equal(7, _accounts.ReadCount);
        Assert.Equal(7, _catalog.ReadCount);
    }

    [Fact]
    public async Task ReconcilerBlocksChangedReportedEconomicsAndUnverifiedCostInterpretation()
    {
        TopstepReferencePreparationResult wrong = await Service.PrepareAsync(Source(Row(1) with { SourceReportedPnL = 99m }), AccountId, Costs);
        Assert.False(wrong.IsReadyForPreview);
        Assert.Contains(wrong.Economics.Diagnostics, d => d.Code == TopstepEconomicsDiagnosticCodes.GrossPnLMismatch && d.SourceRecordIndex == 1);
        Assert.Equal(99m, wrong.Rows[0].Economics.SourceReportedPnL);
        Assert.Null(wrong.Rows[0].Economics.NetPnL);
        TopstepReferencePreparationResult unverified = await Service.PrepareAsync(Source(Row(1)), AccountId);
        Assert.False(unverified.IsReadyForPreview);
        Assert.Contains(unverified.Economics.Diagnostics, d => d.Code == TopstepEconomicsDiagnosticCodes.CostInterpretationUnverified);
    }

    [Fact]
    public async Task InvalidSourceAndPreCancellationAvoidReferenceReads()
    {
        TopstepTradeReconstructionResult invalid = new(new([], [], 0, 0, false), [], []);
        AssertBlocked(await Service.PrepareAsync(invalid, AccountId, Costs), TopstepReferenceDiagnosticCodes.SourceNotReady);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service.PrepareAsync(Source(Row(1)), AccountId, Costs, cancellationToken: cancellation.Token));
        Assert.Equal(0, _accounts.ReadCount);
        Assert.Equal(0, _catalog.ReadCount);
    }

    [Fact]
    public async Task ReaderTokensAndFailuresPropagate()
    {
        using var cancellation = new CancellationTokenSource();
        await Service.PrepareAsync(Source(Row(1)), AccountId, Costs, cancellationToken: cancellation.Token);
        Assert.Equal(cancellation.Token, _accounts.Token);
        Assert.Equal(cancellation.Token, _catalog.Token);
        _accounts.Failure = new IOException("Synthetic account failure");
        Assert.Same(_accounts.Failure, await Assert.ThrowsAsync<IOException>(() => Service.PrepareAsync(Source(Row(1)), AccountId, Costs)));
        _accounts.Failure = null;
        _catalog.Failure = new IOException("Synthetic catalog failure");
        Assert.Same(_catalog.Failure, await Assert.ThrowsAsync<IOException>(() => Service.PrepareAsync(Source(Row(1)), AccountId, Costs)));
    }

    private static void AssertBlocked(TopstepReferencePreparationResult result, string code)
    {
        Assert.False(result.IsReadyForPreview);
        Assert.Contains(result.Diagnostics, d => d.Code == code && d.Severity == TopstepReferenceDiagnosticSeverity.Error);
    }

    private static InstrumentListItem Instrument() => new(InstrumentId, "MNQ", "Micro E-mini Nasdaq-100", AssetClass.Futures,
        "CME", "USD", 0.25m, 0.50m, 2m, true);
    private static TradingAccountDetails Account() => new(AccountId, "Synthetic Topstep", TradingAccountType.Demo,
        "Topstep", "SYNTHETIC-ACCOUNT", "USD", null, true, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
    private static TopstepTradeReconstructionResult Source(params TopstepSourceRow[] rows) => new(new(rows, [], rows.Length, 0, true),
        rows.Select(r => new TopstepTradeCandidate(r)), [new(TopstepReconstructionDiagnosticSeverity.Warning,
            TopstepReconstructionDiagnosticCodes.PositionBoundariesUnverified, rows.Select(r => new TopstepSourceReference(r.SourceRecordIndex, r.SourceLineNumber)), "Unverified boundaries")]);
    private static TopstepSourceRow Row(int index) => new(index, index + 1, $"SYNTH-{index}", "MNQZ6",
        DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddSeconds(1), 20000.125m, 20001.375m, 1.44m, 5m, 2m,
        TopstepTradeType.Long, DateTimeOffset.UnixEpoch, TimeSpan.FromSeconds(1), "00:00:01", 1m);

    private sealed class Catalog : IInstrumentReader
    {
        public IReadOnlyList<InstrumentListItem> Items { get; set; } = [Instrument()];
        public int ReadCount { get; private set; }
        public CancellationToken Token { get; private set; }
        public Exception? Failure { get; set; }
        public Task<IReadOnlyList<InstrumentListItem>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            ReadCount++; Token = cancellationToken; cancellationToken.ThrowIfCancellationRequested();
            if (Failure is not null) throw Failure;
            return Task.FromResult<IReadOnlyList<InstrumentListItem>>(Items.ToArray());
        }
        public Task<InstrumentDetails?> GetByIdAsync(Guid instrumentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Accounts : ITradingAccountReader
    {
        public TradingAccountDetails? Item { get; set; } = Account();
        public Guid? RequestedId { get; private set; }
        public int ReadCount { get; private set; }
        public CancellationToken Token { get; private set; }
        public Exception? Failure { get; set; }
        public Task<TradingAccountDetails?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default)
        {
            ReadCount++; RequestedId = accountId; Token = cancellationToken; cancellationToken.ThrowIfCancellationRequested();
            if (Failure is not null) throw Failure;
            return Task.FromResult(Item?.Id == accountId ? Item : null);
        }
        public Task<IReadOnlyList<AccountListItem>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException("Never infer an account from the list.");
    }
}
