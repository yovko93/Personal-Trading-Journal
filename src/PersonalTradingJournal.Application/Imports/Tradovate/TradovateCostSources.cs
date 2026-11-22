using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Imports.Tradovate;

public sealed record TradovateCostFill(string Id, string Account, string Symbol,
    ExecutionSide Side, decimal Quantity, decimal Price, DateTime LocalTimestamp,
    DateTimeOffset UtcTimestamp, DateOnly TradingDate, decimal Commission);

public sealed record TradovateCashEntry(string Id, string Account, string Currency,
    string Symbol, DateTime LocalTimestamp, DateOnly TradingDate, string Type,
    decimal Delta, decimal Balance)
{
    public bool IsManualAdjustment => Type == "Manual Adjustment";

    public void ValidateMovement()
    {
        switch (Type)
        {
            case "Commission":
            case "Trade Paired":
                if (string.IsNullOrWhiteSpace(Symbol))
                    throw new TradovateCostSourceException($"Cash History {Type} requires a Contract.");
                break;
            case "Manual Adjustment":
                if (!string.IsNullOrEmpty(Symbol) || Delta != decimal.Round(Delta, 2))
                    throw new TradovateCostSourceException("Cash History Manual Adjustment requires an empty Contract and a whole-cent Delta.");
                break;
            default:
                string label = new(Type.Take(64).Where(c => !char.IsControl(c)).ToArray());
                throw new TradovateCostSourceException($"Unsupported Cash History movement '{label}'. Supported types are Commission, Trade Paired and Manual Adjustment.");
        }
    }
}

public sealed class TradovateCostSources(
    IEnumerable<TradovateCostFill> fills, IEnumerable<TradovateCashEntry> cash)
{
    public IReadOnlyList<TradovateCostFill> Fills { get; } = Array.AsReadOnly(fills.ToArray());
    public IReadOnlyList<TradovateCashEntry> Cash { get; } = Array.AsReadOnly(cash.ToArray());
}

public interface ITradovateCostCsvParser
{
    Task<IReadOnlyList<TradovateCostFill>> ParseFillsAsync(Stream source, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TradovateCashEntry>> ParseCashAsync(Stream source, CancellationToken cancellationToken = default);
}

public sealed class TradovateCostSourceException(string message) : Exception(message);

public sealed record TradovateCostAllocation(int CandidateIndex, string FillId, int AllocationIndex, decimal Commission);
public sealed record TradovateExcludedFillsDay(DateOnly Date, int Count, decimal Commission);

public sealed record TradovateVerifiedCosts(decimal Gross, decimal Costs, DateOnly Start, DateOnly End,
    IReadOnlyList<TradovateCostAllocation> Allocations, int ManualAdjustmentCount, decimal ManualAdjustmentTotal,
    bool UsesFixedOffsetDisplay, IReadOnlyList<TradovateExcludedFillsDay> ExcludedFills)
{
    public decimal Net => Gross - Costs;
    public decimal TotalCashMovement => Net + ManualAdjustmentTotal;
    private readonly IReadOnlyDictionary<(string, int), decimal> _byIdentity = Allocations.ToDictionary(
        a => (a.FillId, a.AllocationIndex), a => a.Commission);
    public decimal For(TradovatePreparedExecution execution) => For(execution.ExternalFillId, execution.AllocationIndex);
    public decimal For(string fillId, int allocationIndex) => _byIdentity[(fillId, allocationIndex)];
}
