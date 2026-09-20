namespace PersonalTradingJournal.Infrastructure.Tests.Imports.Tradovate;

internal static class TradovateCsvFixtures
{
    public const string Header =
        "symbol,_priceFormat,_priceFormatType,_tickSize,buyFillId,sellFillId,qty,buyPrice,sellPrice,pnl,boughtTimestamp,soldTimestamp,duration";

    public const string LongLikeRow =
        "MNQU6,2,0,0.25,000BUY01,000SELL01,2,20123.125,20124.375,$125.00,09/10/2026 16:30:03,09/10/2026 16:30:15,12sec";

    public const string ShortLikeRow =
        "MNQZ6,2,0,0.25,000BUY02,000SELL02,1,20200.50,20210.25,$(97.50),09/10/2026 16:32:00,09/10/2026 16:31:42,18sec";

    public const string MultiRow = Header + "\r\n" +
        LongLikeRow + "\r\n" +
        "MNQU6,2,0,0.25,000BUY01,000SELL03,1,20123.125,20125.00,$0.00,09/10/2026 16:30:03,09/10/2026 16:31:00,57sec\r\n" +
        "MNQZ6,2,0,0.25,000BUY04,000SELL02,3,20201.125,20210.25,$-273.75,09/10/2026 16:33:00,09/10/2026 16:31:42,1min 18s\r\n" +
        "MNQZ6,2,0,0.25,000BUY05,000SELL02,1,20202.875,20210.25,$73.75,09/10/2026 16:34:00,09/10/2026 16:31:42,2min 18s";

    public static string WithRows(params string[] rows) =>
        string.Join('\n', new[] { Header }.Concat(rows));
}
