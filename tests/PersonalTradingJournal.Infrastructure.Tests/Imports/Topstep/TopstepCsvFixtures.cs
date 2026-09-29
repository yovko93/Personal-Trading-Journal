namespace PersonalTradingJournal.Infrastructure.Tests.Imports.Topstep;

internal static class TopstepCsvFixtures
{
    public const string Header = "Id,ContractName,EnteredAt,ExitedAt,EntryPrice,ExitPrice,Fees,PnL,Size,Type,TradeDay,TradeDuration,Commissions";
    public const string Row = "000SYNTH01,MNQZ6,07/10/2026 17:00:00 +03:00,07/10/2026 17:00:02 +03:00,20000.125000000,20001.375000000,1.44,5.000000000,2,Long,07/10/2026 00:00:00 -05:00,00:00:01.1234567,1.00";

    public static string WithRows(params string[] rows) => Header + "\n" + string.Join('\n', rows);

    public static string Replace(int column, string value, string row = Row)
    {
        string[] fields = row.Split(',');
        fields[column] = value;
        return string.Join(',', fields);
    }
}
