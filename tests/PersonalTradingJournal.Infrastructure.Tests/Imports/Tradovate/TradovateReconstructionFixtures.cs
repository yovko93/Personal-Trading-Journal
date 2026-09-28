using PersonalTradingJournal.Application.Imports.Tradovate;

namespace PersonalTradingJournal.Infrastructure.Tests.Imports.Tradovate;

internal static class TradovateReconstructionFixtures
{
    public static TradovateMatchedFillRow Row(
        int sourceRecordIndex,
        string brokerSymbol,
        string buyFillId,
        string sellFillId,
        decimal matchedQuantity,
        DateTime boughtLocalTimestamp,
        DateTime soldLocalTimestamp,
        decimal buyPrice = 20123.125m,
        decimal sellPrice = 20124.375m,
        decimal tickSize = 0.25m,
        decimal sourceReportedPnL = 0m) => new(
            sourceRecordIndex,
            sourceRecordIndex + 1,
            brokerSymbol,
            "2",
            "0",
            tickSize,
            buyFillId,
            sellFillId,
            matchedQuantity,
            buyPrice,
            sellPrice,
            sourceReportedPnL,
            boughtLocalTimestamp,
            soldLocalTimestamp,
            "fictional-duration");

    public static TradovateCsvParseResult Complete(
        params TradovateMatchedFillRow[] rows) => new(
            rows,
            [],
            rows.Length,
            rejectedRecordCount: 0,
            isHeaderUsable: true);

    public static DateTime At(int hour, int minute = 0, int second = 0) =>
        new(2026, 9, 10, hour, minute, second, DateTimeKind.Unspecified);
}
