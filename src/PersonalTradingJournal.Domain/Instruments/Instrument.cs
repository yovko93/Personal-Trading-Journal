using PersonalTradingJournal.Domain.Common;

namespace PersonalTradingJournal.Domain.Instruments;

/// <summary>
/// Represents a canonical trading instrument rather than a contract-specific symbol.
/// </summary>
public sealed class Instrument : AuditableEntity
{
    private const int MaximumSymbolLength = 32;
    private const int MaximumDisplayNameLength = 128;
    private const int MaximumExchangeLength = 64;
    private const int MaximumCurrencyLength = 8;

    public Instrument(
        string symbol,
        string displayName,
        AssetClass assetClass,
        string? exchange,
        string currency,
        decimal tickSize,
        decimal tickValue,
        DateTimeOffset createdAtUtc)
        : base(createdAtUtc)
    {
        Symbol = NormalizeRequired(
            symbol,
            MaximumSymbolLength,
            nameof(symbol),
            useUppercase: true);
        DisplayName = NormalizeRequired(
            displayName,
            MaximumDisplayNameLength,
            nameof(displayName),
            useUppercase: false);
        AssetClass = ValidateAssetClass(assetClass);
        Exchange = NormalizeExchange(exchange);
        Currency = NormalizeRequired(
            currency,
            MaximumCurrencyLength,
            nameof(currency),
            useUppercase: true);
        TickSize = ValidatePositive(tickSize, nameof(tickSize));
        TickValue = ValidatePositive(tickValue, nameof(tickValue));
        IsActive = true;
    }

    private Instrument(
        Guid id,
        string symbol,
        string displayName,
        AssetClass assetClass,
        string? exchange,
        string currency,
        decimal tickSize,
        decimal tickValue,
        bool isActive,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
        : base(id, createdAtUtc, updatedAtUtc)
    {
        Symbol = NormalizeRequired(
            symbol,
            MaximumSymbolLength,
            nameof(symbol),
            useUppercase: true);
        DisplayName = NormalizeRequired(
            displayName,
            MaximumDisplayNameLength,
            nameof(displayName),
            useUppercase: false);
        AssetClass = ValidateAssetClass(assetClass);
        Exchange = NormalizeExchange(exchange);
        Currency = NormalizeRequired(
            currency,
            MaximumCurrencyLength,
            nameof(currency),
            useUppercase: true);
        TickSize = ValidatePositive(tickSize, nameof(tickSize));
        TickValue = ValidatePositive(tickValue, nameof(tickValue));
        IsActive = isActive;
    }

    public string Symbol { get; }

    public string DisplayName { get; }

    public AssetClass AssetClass { get; }

    public string? Exchange { get; }

    public string Currency { get; }

    /// <summary>
    /// Gets the minimum valid price increment for the instrument.
    /// </summary>
    public decimal TickSize { get; }

    /// <summary>
    /// Gets the monetary value of one tick for one standard trading unit or contract.
    /// </summary>
    public decimal TickValue { get; }

    /// <summary>
    /// Gets the monetary value of one full price point.
    /// </summary>
    public decimal PointValue => TickValue / TickSize;

    public bool IsActive { get; private set; }

    public static Instrument Rehydrate(
        Guid id,
        string symbol,
        string displayName,
        AssetClass assetClass,
        string? exchange,
        string currency,
        decimal tickSize,
        decimal tickValue,
        bool isActive,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        return new Instrument(
            id,
            symbol,
            displayName,
            assetClass,
            exchange,
            currency,
            tickSize,
            tickValue,
            isActive,
            createdAtUtc,
            updatedAtUtc);
    }

    public void Activate(DateTimeOffset updatedAtUtc)
    {
        if (IsActive)
        {
            return;
        }

        SetUpdatedAtUtc(updatedAtUtc);
        IsActive = true;
    }

    public void Deactivate(DateTimeOffset updatedAtUtc)
    {
        if (!IsActive)
        {
            return;
        }

        SetUpdatedAtUtc(updatedAtUtc);
        IsActive = false;
    }

    private static string NormalizeRequired(
        string value,
        int maximumLength,
        string parameterName,
        bool useUppercase)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        string normalized = value.Trim();
        if (useUppercase)
        {
            normalized = normalized.ToUpperInvariant();
        }

        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"The value cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }

    private static string? NormalizeExchange(string? exchange)
    {
        if (string.IsNullOrWhiteSpace(exchange))
        {
            return null;
        }

        string normalized = exchange.Trim();
        if (normalized.Length > MaximumExchangeLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(exchange),
                exchange,
                $"The exchange cannot exceed {MaximumExchangeLength} characters.");
        }

        return normalized;
    }

    private static AssetClass ValidateAssetClass(AssetClass assetClass)
    {
        if (!Enum.IsDefined(assetClass))
        {
            throw new ArgumentOutOfRangeException(
                nameof(assetClass),
                assetClass,
                "The asset class is not defined.");
        }

        return assetClass;
    }

    private static decimal ValidatePositive(decimal value, string parameterName)
    {
        if (value <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "The value must be greater than zero.");
        }

        return value;
    }
}
