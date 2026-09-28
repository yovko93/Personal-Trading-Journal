using PersonalTradingJournal.Application.Common.Time;
using PersonalTradingJournal.Application.Imports.Tradovate;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Tests.Imports.Tradovate;

public sealed class TradovateImportPreviewBuilderTests
{
    [Fact]
    public void BuildProducesDeterministicTradeEconomicsSummaryAndOrdering()
    {
        PreviewFixture fixture = CreateFixture();

        TradovateImportPreview preview = new TradovateImportPreviewBuilder().Build(
            "fills.csv",
            fixture.Parse,
            fixture.Reconstruction,
            fixture.Preparation);

        Assert.True(preview.IsStructurallyReady);
        Assert.True(preview.IsReadyForConfirmation);
        Assert.Equal("fills.csv", preview.Summary.FileName);
        Assert.Equal("Primary", preview.Summary.AccountName);
        Assert.Equal(2, preview.Summary.SourceRecordCount);
        Assert.Equal(1, preview.Summary.CandidateCount);
        Assert.Equal(1, preview.Summary.CanonicalInstrumentCount);
        Assert.Equal(1, preview.Summary.ProposedInstrumentCount);
        Assert.Equal(TradingTimePolicy.TradovateSourceTimeZoneId, preview.Summary.SourceTimeZoneId);
        Assert.Equal("UTC", preview.Summary.CanonicalTimeZoneId);
        Assert.Equal(TradingTimePolicy.TradingTimeZoneId, preview.Summary.PreviewTimeZoneId);

        TradovateImportPreviewTradeItem trade = Assert.Single(preview.Trades);
        Assert.Equal(1, trade.CandidateIndex);
        Assert.Equal("MNQU6", trade.BrokerSymbol);
        Assert.Equal("MNQ", trade.CanonicalSymbol);
        Assert.Equal(3m, trade.OpeningQuantity);
        Assert.Equal(24002m, trade.WeightedAverageEntryPrice);
        Assert.Equal(24010m, trade.WeightedAverageExitPrice);
        Assert.Equal(15.50m, trade.SourceReportedPnL);
        Assert.Equal(
            TradovateInstrumentResolutionStatus.ProposedCreation,
            trade.InstrumentResolutionStatus);
        TradovateImportPreviewInstrumentItem proposed = Assert.Single(preview.Instruments);
        Assert.Null(proposed.ExistingInstrumentId);
        Assert.Equal("Micro E-mini Nasdaq-100", proposed.DisplayName);
        Assert.Equal("Futures", proposed.AssetClass);
        Assert.Equal("CME", proposed.Exchange);
        Assert.Equal("USD", proposed.Currency);
        Assert.Equal(0.25m, proposed.TickSize);
        Assert.Equal(0.50m, proposed.TickValue);
        Assert.Equal("Verified profile", proposed.MetadataSource);
    }

    [Fact]
    public void BuildAggregatesDiagnosticsByStageAndAddsPreviewLimitations()
    {
        PreviewFixture fixture = CreateFixture(includeDiagnostics: true);

        TradovateImportPreview preview = new TradovateImportPreviewBuilder().Build(
            "fills.csv",
            fixture.Parse,
            fixture.Reconstruction,
            fixture.Preparation);

        Assert.Contains(preview.Diagnostics, item =>
            item.Stage == TradovateImportPreviewDiagnosticStage.Csv &&
            item.Code == "CSV_WARNING");
        Assert.Contains(preview.Diagnostics, item =>
            item.Stage == TradovateImportPreviewDiagnosticStage.Reconstruction &&
            item.Code == "RECON_WARNING");
        Assert.Contains(preview.Diagnostics, item =>
            item.Stage == TradovateImportPreviewDiagnosticStage.Instrument &&
            item.Code == "INSTRUMENT_WARNING");
        Assert.Contains(preview.Diagnostics, item =>
            item.Stage == TradovateImportPreviewDiagnosticStage.Preparation &&
            item.Code == "PREPARATION_WARNING");
        Assert.Contains(preview.Diagnostics, item =>
            item.Stage == TradovateImportPreviewDiagnosticStage.Preview &&
            item.Code == TradovateReconstructionDiagnosticCodes.SourceCompletenessUnverified);
        Assert.Contains(preview.Diagnostics, item =>
            item.Stage == TradovateImportPreviewDiagnosticStage.Preview &&
            item.Code == "COSTS_UNAVAILABLE");
        Assert.Equal(preview.Diagnostics.Count, preview.Summary.WarningCount);
        Assert.Equal(0, preview.Summary.ErrorCount);
    }

    [Fact]
    public void BuildRepresentsExistingInstrumentWithoutCreationProposal()
    {
        PreviewFixture fixture = CreateFixture(useExistingInstrument: true);

        TradovateImportPreview preview = new TradovateImportPreviewBuilder().Build(
            "fills.csv",
            fixture.Parse,
            fixture.Reconstruction,
            fixture.Preparation);

        TradovateImportPreviewInstrumentItem instrument = Assert.Single(preview.Instruments);
        Assert.Equal(TradovateInstrumentResolutionStatus.ExistingInstrument, instrument.Status);
        Assert.NotNull(instrument.ExistingInstrumentId);
        Assert.Null(instrument.MetadataSource);
        Assert.Equal("User-entered MNQ", instrument.DisplayName);
        Assert.Equal("Futures", instrument.AssetClass);
        Assert.Equal("User Exchange", instrument.Exchange);
        Assert.Equal("USD", instrument.Currency);
        Assert.Equal(0.25m, instrument.TickSize);
        Assert.Equal(0.50m, instrument.TickValue);
        Assert.Equal(1, preview.Summary.ExistingInstrumentCount);
        Assert.Equal(0, preview.Summary.ProposedInstrumentCount);
    }

    [Fact]
    public void BuildDoesNotFabricateUnknownInstrumentEconomics()
    {
        PreviewFixture fixture = CreateFixture(unknownInstrument: true);

        TradovateImportPreview preview = new TradovateImportPreviewBuilder().Build(
            "fills.csv",
            fixture.Parse,
            fixture.Reconstruction,
            fixture.Preparation);

        TradovateImportPreviewInstrumentItem instrument = Assert.Single(preview.Instruments);
        Assert.Equal(TradovateInstrumentResolutionStatus.RequiresUserInput, instrument.Status);
        Assert.Null(instrument.ExistingInstrumentId);
        Assert.Null(instrument.DisplayName);
        Assert.Null(instrument.AssetClass);
        Assert.Null(instrument.Exchange);
        Assert.Null(instrument.Currency);
        Assert.Null(instrument.TickValue);
    }

    private static PreviewFixture CreateFixture(
        bool includeDiagnostics = false,
        bool useExistingInstrument = false,
        bool unknownInstrument = false)
    {
        DateTime sourceOpen = new(2026, 9, 14, 16, 30, 0, DateTimeKind.Unspecified);
        DateTime sourceClose = sourceOpen.AddMinutes(10);
        TradovateMatchedFillRow[] rows =
        [
            Row(1, "buy-1", "sell-1", 10m, sourceOpen, sourceClose),
            Row(2, "buy-2", "sell-2", 5.50m, sourceOpen.AddMinutes(1), sourceClose),
        ];
        var parse = new TradovateCsvParseResult(
            rows,
            includeDiagnostics
                ? [new TradovateCsvDiagnostic(
                    TradovateCsvDiagnosticSeverity.Warning,
                    "CSV_WARNING",
                    1,
                    2,
                    null,
                    "CSV warning")]
                : [],
            sourceRecordCount: 2,
            rejectedRecordCount: 0,
            isHeaderUsable: true);

        TradovateReconstructedExecution entry1 = Execution(
            ExecutionSide.Buy, "buy-1", 1m, 24000m, sourceOpen, [1]);
        TradovateReconstructedExecution entry2 = Execution(
            ExecutionSide.Buy, "buy-2", 2m, 24003m, sourceOpen.AddMinutes(1), [2]);
        TradovateReconstructedExecution exit = Execution(
            ExecutionSide.Sell, "sell-1", 3m, 24010m, sourceClose, [1, 2]);
        var candidate = new TradovateTradeCandidate(
            "MNQU6",
            TradeDirection.Long,
            [entry1, entry2, exit],
            sourceOpen,
            sourceClose,
            3m,
            3m,
            0m,
            TradovateReconstructionStatus.Reconstructed,
            [1, 2],
            []);
        var reconstruction = new TradovateExecutionReconstructionResult(
            [entry1, entry2, exit],
            [candidate],
            matchedPairs: [],
            symbolReconciliations: [],
            includeDiagnostics
                ? [new TradovateReconstructionDiagnostic(
                    TradovateReconstructionDiagnosticSeverity.Warning,
                    "RECON_WARNING",
                    "MNQU6",
                    [1],
                    [],
                    "Reconstruction warning")]
                : [],
            sourceRecordCount: 2,
            TradovateReconstructionStatus.Reconstructed);

        Guid instrumentId = Guid.NewGuid();
        TradovateInstrumentCreationProposal? proposal = useExistingInstrument || unknownInstrument
            ? null
            : new TradovateInstrumentCreationProposal(
                "MNQ",
                "Micro E-mini Nasdaq-100",
                Domain.Instruments.AssetClass.Futures,
                "CME",
                "USD",
                0.25m,
                0.50m,
                ["MNQU6"],
                "Verified profile");
        TradovateInstrumentResolutionStatus resolutionStatus = unknownInstrument
            ? TradovateInstrumentResolutionStatus.RequiresUserInput
            : useExistingInstrument
                ? TradovateInstrumentResolutionStatus.ExistingInstrument
                : TradovateInstrumentResolutionStatus.ProposedCreation;
        Guid? existingId = useExistingInstrument ? instrumentId : null;
        var existingSnapshot = useExistingInstrument
            ? new TradovateExistingInstrumentSnapshot(
                "User-entered MNQ",
                Domain.Instruments.AssetClass.Futures,
                "User Exchange",
                "USD",
                0.25m,
                0.50m)
            : null;
        var resolution = new TradovateInstrumentResolutionResult(
            [new TradovateBrokerSymbolMapping(
                "MNQU6", "MNQ", resolutionStatus, existingId,
                useExistingInstrument ? true : null, [])],
            [new TradovateCanonicalInstrumentResolution(
                "MNQ", ["MNQU6"], 0.25m, resolutionStatus, existingId,
                useExistingInstrument ? true : null,
                useExistingInstrument ? [instrumentId] : [],
                proposal,
                [],
                unknownInstrument ? null : "USD",
                existingSnapshot)],
            includeDiagnostics
                ? [new TradovateInstrumentResolutionDiagnostic(
                    TradovateReconstructionDiagnosticSeverity.Warning,
                    "INSTRUMENT_WARNING",
                    "MNQ",
                    ["MNQU6"],
                    [],
                    "Instrument warning")]
                : [],
            unknownInstrument
                ? TradovateInstrumentResolutionOverallStatus.RequiresUserInput
                : TradovateInstrumentResolutionOverallStatus.ReadyForPreview);

        DateTimeOffset utcOpen = new(2026, 9, 14, 13, 30, 0, TimeSpan.Zero);
        TradovatePreparedExecution preparedEntry1 = Prepared(
            ExecutionSide.Buy, "buy-1", 1m, 24000m, sourceOpen,
            utcOpen, [1]);
        TradovatePreparedExecution preparedEntry2 = Prepared(
            ExecutionSide.Buy, "buy-2", 2m, 24003m, sourceOpen.AddMinutes(1),
            utcOpen.AddMinutes(1), [2]);
        TradovatePreparedExecution preparedExit = Prepared(
            ExecutionSide.Sell, "sell-1", 3m, 24010m, sourceClose,
            utcOpen.AddMinutes(10), [1, 2]);
        Guid accountId = Guid.NewGuid();
        var account = new TradovateImportAccountSnapshot(
            accountId,
            "Primary",
            TradingAccountType.Personal,
            "Tradovate",
            "SIM-1",
            "USD",
            true);
        var preparedCandidate = new TradovatePreparedTradeCandidate(
            "MNQU6",
            "MNQ",
            existingId,
            proposal,
            accountId,
            TradeDirection.Long,
            [preparedEntry1, preparedEntry2, preparedExit],
            utcOpen,
            utcOpen.AddMinutes(10),
            new DateTimeOffset(2026, 9, 14, 9, 30, 0, TimeSpan.FromHours(-4)),
            new DateTimeOffset(2026, 9, 14, 9, 40, 0, TimeSpan.FromHours(-4)),
            [1, 2]);
        var preparation = new TradovateImportPreparationResult(
            account,
            resolution,
            [preparedEntry1, preparedEntry2, preparedExit],
            [preparedCandidate],
            includeDiagnostics
                ? [new TradovateImportPreparationDiagnostic(
                    TradovateReconstructionDiagnosticSeverity.Warning,
                    "PREPARATION_WARNING",
                    "Preparation warning")]
                : [],
            unknownInstrument
                ? TradovateImportPreparationStatus.RequiresUserInput
                : TradovateImportPreparationStatus.ReadyForPreview);

        return new PreviewFixture(parse, reconstruction, preparation);
    }

    private static TradovateMatchedFillRow Row(
        int index,
        string buyId,
        string sellId,
        decimal pnl,
        DateTime bought,
        DateTime sold) =>
        new(index, index + 1, "MNQU6", "0.00", "Decimal", 0.25m,
            buyId, sellId, 1m, 24000m, 24010m, pnl, bought, sold, "00:10:00");

    private static TradovateReconstructedExecution Execution(
        ExecutionSide side,
        string id,
        decimal quantity,
        decimal price,
        DateTime timestamp,
        IReadOnlyList<int> indices) =>
        new("MNQU6", side, id, quantity, price, timestamp, 0.25m, indices, [2]);

    private static TradovatePreparedExecution Prepared(
        ExecutionSide side,
        string id,
        decimal quantity,
        decimal price,
        DateTime sourceTimestamp,
        DateTimeOffset utc,
        IReadOnlyList<int> indices) =>
        new("MNQU6", side, id, quantity, price, sourceTimestamp,
            TradingTimePolicy.TradovateSourceTimeZoneId,
            utc,
            utc.ToOffset(TimeSpan.FromHours(-4)).DateTime,
            TimeSpan.FromHours(-4),
            TradingTimePolicy.TradingTimeZoneId,
            indices,
            [2]);

    private sealed record PreviewFixture(
        TradovateCsvParseResult Parse,
        TradovateExecutionReconstructionResult Reconstruction,
        TradovateImportPreparationResult Preparation);
}
