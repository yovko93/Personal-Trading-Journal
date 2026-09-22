namespace PersonalTradingJournal.Application.Imports.Tradovate;

/// <summary>
/// Recognizes a futures root followed by a CME month code and one or two year digits.
/// Syntax alone does not verify that the root is a listed product.
/// </summary>
public static class TradovateFuturesContractSymbolParser
{
    private const string MonthCodes = "FGHJKMNQUVXZ";

    public static bool TryGetCanonicalSymbol(
        string? brokerSymbol,
        out string? canonicalSymbol)
    {
        canonicalSymbol = null;
        if (string.IsNullOrEmpty(brokerSymbol))
        {
            return false;
        }

        string value = brokerSymbol.ToUpperInvariant();
        int yearDigitCount = value.Length >= 3 &&
                             IsAsciiDigit(value[^1]) &&
                             IsAsciiDigit(value[^2])
            ? 2
            : 1;
        int monthIndex = value.Length - yearDigitCount - 1;
        if (monthIndex < 1 ||
            !MonthCodes.Contains(value[monthIndex], StringComparison.Ordinal) ||
            !value[(monthIndex + 1)..].All(IsAsciiDigit))
        {
            return false;
        }

        string root = value[..monthIndex];
        if (!root.All(character =>
                character is >= 'A' and <= 'Z' or >= '0' and <= '9') ||
            !root.Any(character => character is >= 'A' and <= 'Z'))
        {
            return false;
        }

        canonicalSymbol = root;
        return true;
    }

    private static bool IsAsciiDigit(char character) => character is >= '0' and <= '9';
}
