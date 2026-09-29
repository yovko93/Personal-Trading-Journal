using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Imports.Topstep;

/// <summary>
/// One reported closed quantity, with row-local entry/exit economics. This is
/// not evidence that the account became flat or that the row describes two raw fills.
/// </summary>
public sealed class TopstepTradeCandidate
{
    public TopstepTradeCandidate(TopstepSourceRow sourceRow)
    {
        ArgumentNullException.ThrowIfNull(sourceRow);
        if (sourceRow.Size <= 0m || !Enum.IsDefined(sourceRow.Type) ||
            sourceRow.ExitedAtUtc < sourceRow.EnteredAtUtc)
        {
            throw new ArgumentException("A candidate requires a positive closed quantity, supported direction, and ordered instants.", nameof(sourceRow));
        }

        SourceRow = sourceRow;
    }

    // Keep all reported costs, PnL, date labels, offsets, duration and source identity
    // on the original immutable row. Never turn its Id into an execution identifier.
    public TopstepSourceRow SourceRow { get; }
    public string ContractName => SourceRow.ContractName;
    public TradeDirection Direction => SourceRow.Type == TopstepTradeType.Long ? TradeDirection.Long : TradeDirection.Short;
    public decimal Quantity => SourceRow.Size;
    public decimal EntryPrice => SourceRow.EntryPrice;
    public decimal ExitPrice => SourceRow.ExitPrice;
    public DateTimeOffset OpenedAtUtc => SourceRow.EnteredAtUtc;
    public DateTimeOffset ClosedAtUtc => SourceRow.ExitedAtUtc;
    public bool ArePositionBoundariesVerified => false;
}
