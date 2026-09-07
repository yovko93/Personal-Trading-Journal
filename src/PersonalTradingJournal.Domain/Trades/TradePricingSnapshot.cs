namespace PersonalTradingJournal.Domain.Trades;

/// <summary>
/// Captures the immutable historical pricing economics required for trade P&amp;L.
/// </summary>
public sealed class TradePricingSnapshot
{
    private const int MaximumCurrencyLength = 8;

    public TradePricingSnapshot(decimal pointValue, string currency)
    {
        if (pointValue <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pointValue),
                pointValue,
                "The point value must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("A currency is required.", nameof(currency));
        }

        string normalizedCurrency = currency.Trim().ToUpperInvariant();
        if (normalizedCurrency.Length > MaximumCurrencyLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currency),
                currency,
                $"The currency cannot exceed {MaximumCurrencyLength} characters.");
        }

        PointValue = pointValue;
        Currency = normalizedCurrency;
    }

    public decimal PointValue { get; }

    public string Currency { get; }
}
