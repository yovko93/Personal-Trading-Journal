using PersonalTradingJournal.Domain.Instruments;

namespace PersonalTradingJournal.Domain.Trades;

/// <summary>
/// Defines cross-asset quantity rules for workflows that know the Instrument market type.
/// </summary>
public static class TradeQuantityPolicy
{
    public const string FuturesWholeContractsMessage =
        "Futures quantity must be a whole number of contracts.";

    public static void Validate(
        AssetClass assetClass,
        decimal quantity,
        string parameterName = "quantity")
    {
        if (!Enum.IsDefined(assetClass))
        {
            throw new ArgumentOutOfRangeException(
                nameof(assetClass),
                assetClass,
                "The asset class is not defined.");
        }

        if (quantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                quantity,
                "Quantity must be greater than zero.");
        }

        if (assetClass == AssetClass.Futures &&
            decimal.Truncate(quantity) != quantity)
        {
            throw new ArgumentException(
                FuturesWholeContractsMessage,
                parameterName);
        }
    }
}
