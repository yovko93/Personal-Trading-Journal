using System.Text;
using PersonalTradingJournal.Application.Imports.Topstep;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Imports.Topstep;

namespace PersonalTradingJournal.Infrastructure.Tests.Imports.Topstep;

public sealed class TopstepEconomicsPipelineTests
{
    [Fact]
    public async Task ParseReconstructReconcilePreservesRowsWarningsAndExactFinancialComponents()
    {
        string second = TopstepCsvFixtures.Replace(0, "SYNTH-2");
        second = TopstepCsvFixtures.Replace(9, "Short", second);
        second = TopstepCsvFixtures.Replace(7, "-5", second);
        byte[] bytes = Encoding.UTF8.GetBytes(TopstepCsvFixtures.WithRows(TopstepCsvFixtures.Row, second));
        await using var stream = new MemoryStream(bytes, writable: false);
        TopstepCsvParseResult parsed = await new TopstepCsvParser().ParseAsync(stream);
        TopstepTradeReconstructionResult candidates = new TopstepTradeCandidateReconstructor().Reconstruct(parsed);
        TopstepSourceRow[] before = parsed.Rows.ToArray();

        TopstepEconomicsReconciliationResult result = Reconcile(candidates);

        Assert.True(result.IsEconomicallyReconciled);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal([5m, -5m], result.Rows.Select(r => r.CalculatedGrossPnL));
        Assert.Equal([2.56m, -7.44m], result.Rows.Select(r => r.NetPnL));
        Assert.Equal(2.88m, result.Rows.Sum(r => r.SourceReportedFees));
        Assert.Equal(2m, result.Rows.Sum(r => r.SourceReportedCommissions));
        Assert.Same(candidates, result.Source);
        Assert.Equal(before, result.Rows.Select(r => r.Candidate.SourceRow));
        Assert.Equal([1, 2], result.Rows.Select(r => r.Candidate.SourceRow.SourceRecordIndex));
        Assert.Contains(result.Source.Diagnostics, d => d.Code == TopstepReconstructionDiagnosticCodes.PositionGroupingAmbiguous);
        Assert.False(result.Source.IsPositionGroupingVerified);
        Assert.Equal(bytes, stream.ToArray());
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(6, "Fees")]
    [InlineData(12, "Commissions")]
    public async Task MissingCostValueBlocksAtParserAndProvidesSpecificEconomicDiagnostic(int column, string field)
    {
        TopstepEconomicsReconciliationResult result = await Pipeline(
            TopstepCsvFixtures.WithRows(TopstepCsvFixtures.Replace(column, "")));

        Assert.False(result.IsEconomicallyReconciled);
        Assert.Empty(result.Rows);
        Assert.Empty(result.Source.Candidates);
        Assert.Equal(1, result.Source.Source.RejectedRecordCount);
        TopstepEconomicsDiagnostic error = Assert.Single(result.Diagnostics, d => d.Code == TopstepEconomicsDiagnosticCodes.MissingReportedCost);
        Assert.Equal(field, error.FieldName);
        Assert.Equal(1, error.SourceRecordIndex);
        Assert.Equal(2, error.SourceLineNumber);
        Assert.Contains(result.Source.Source.Diagnostics, d => d.FieldName == field && d.Code == TopstepCsvDiagnosticCodes.RequiredValue);
    }

    [Theory]
    [InlineData("Fees")]
    [InlineData("Commissions")]
    public async Task MissingCostColumnCannotBecomeZeroEvenWhenOtherRowsLookValid(string field)
    {
        string header = string.Join(',', TopstepCsvFixtures.Header.Split(',').Where(name => name != field));
        TopstepEconomicsReconciliationResult result = await Pipeline(header + "\n" + TopstepCsvFixtures.Row);
        Assert.False(result.IsEconomicallyReconciled);
        Assert.Empty(result.Rows);
        Assert.Contains(result.Diagnostics, d => d.Code == TopstepEconomicsDiagnosticCodes.MissingReportedCost && d.FieldName == field);
    }

    [Theory]
    [InlineData(6, "bad", "INVALID_DECIMAL")]
    [InlineData(12, "bad", "INVALID_DECIMAL")]
    [InlineData(7, "", "REQUIRED_VALUE")]
    public async Task InvalidSourceFinancialValuesKeepTheirSpecificParserDiagnostic(int column, string value, string code)
    {
        TopstepEconomicsReconciliationResult result = await Pipeline(
            TopstepCsvFixtures.WithRows(TopstepCsvFixtures.Replace(column, value)));
        Assert.False(result.IsEconomicallyReconciled);
        Assert.Empty(result.Rows);
        Assert.Contains(result.Source.Source.Diagnostics, d => d.Code == code && d.FieldName == TopstepCsvFixtures.Header.Split(',')[column]);
    }

    [Fact]
    public async Task InconsistentReportedPnlBlocksTheBatchWhileRetainingPerRowComparison()
    {
        string second = TopstepCsvFixtures.Replace(0, "SYNTH-2");
        second = TopstepCsvFixtures.Replace(7, "2.56", second);
        TopstepEconomicsReconciliationResult result = await Pipeline(TopstepCsvFixtures.WithRows(TopstepCsvFixtures.Row, second));
        Assert.False(result.IsEconomicallyReconciled);
        Assert.Equal(2, result.Rows.Count);
        Assert.True(result.Rows[0].IsReconciled);
        Assert.Null(result.Rows[1].NetPnL);
        Assert.Equal(2.56m, result.Rows[1].SourceReportedPnL);
        Assert.Equal(5m, result.Rows[1].CalculatedGrossPnL);
        TopstepEconomicsDiagnostic error = Assert.Single(result.Diagnostics);
        Assert.Equal(TopstepEconomicsDiagnosticCodes.GrossPnLMismatch, error.Code);
        Assert.Equal(2, error.SourceRecordIndex);
        Assert.Equal(3, error.SourceLineNumber);
    }

    private static async Task<TopstepEconomicsReconciliationResult> Pipeline(string csv)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        TopstepCsvParseResult parsed = await new TopstepCsvParser().ParseAsync(stream);
        return Reconcile(new TopstepTradeCandidateReconstructor().Reconstruct(parsed));
    }

    private static TopstepEconomicsReconciliationResult Reconcile(TopstepTradeReconstructionResult source) =>
        new TopstepEconomicsReconciler().Reconcile(source,
            new Dictionary<string, TradePricingSnapshot> { ["MNQZ6"] = new(2m, "USD") },
            TopstepCostInterpretation.SeparateReportedRoundTurnTotalsUsd);
}
