using System.Text;
using PersonalTradingJournal.Application.Imports;
using PersonalTradingJournal.Infrastructure.Imports.Csv;

namespace PersonalTradingJournal.Infrastructure.Tests.Imports;

public sealed class ImportCsvFormatDetectorTests
{
    public const string TopstepHeader = "Id,ContractName,EnteredAt,ExitedAt,EntryPrice,ExitPrice,Fees,PnL,Size,Type,TradeDay,TradeDuration,Commissions";
    public const string TradovateHeader = "symbol,_priceFormat,_priceFormatType,_tickSize,buyFillId,sellFillId,qty,buyPrice,sellPrice,pnl,boughtTimestamp,soldTimestamp,duration";

    [Theory]
    [InlineData(TopstepHeader, ImportCsvFormat.Topstep)]
    [InlineData(TradovateHeader, ImportCsvFormat.Tradovate)]
    [InlineData(TopstepHeader + ",Comment", ImportCsvFormat.Topstep)]
    [InlineData(TopstepHeader + ",buyFillId", ImportCsvFormat.Unknown)]
    [InlineData(TradovateHeader + ",Fees", ImportCsvFormat.Unknown)]
    [InlineData(TopstepHeader + "," + TradovateHeader, ImportCsvFormat.Unknown)]
    [InlineData("Id,ContractName", ImportCsvFormat.Unknown)]
    [InlineData("hello", ImportCsvFormat.Unknown)]
    [InlineData("", ImportCsvFormat.Unknown)]
    [InlineData(TopstepHeader + ",Id", ImportCsvFormat.Unknown)]
    [InlineData("\"unterminated", ImportCsvFormat.Unknown)]
    public async Task RoutesOnlyOneCompleteNonMixedHeader(string csv, ImportCsvFormat expected)
    {
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        ImportCsvFormatResult result = await new ImportCsvFormatDetector().DetectAsync(source);
        Assert.Equal(expected, result.Format);
        Assert.Equal(expected == ImportCsvFormat.Unknown, result.Message is not null);
        Assert.True(source.CanRead);
    }

    [Fact]
    public async Task QuotedReorderedBomHeaderIsRecognizedAndCancellationPropagates()
    {
        string csv = "\uFEFF\n" + string.Join(',', TopstepHeader.Split(',').Reverse().Select(h => $"\"{h}\"")) + "\nnot a valid data row";
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        Assert.Equal(ImportCsvFormat.Topstep, (await new ImportCsvFormatDetector().DetectAsync(source)).Format);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ImportCsvFormatDetector().DetectAsync(source, cancelled.Token));
    }
}
