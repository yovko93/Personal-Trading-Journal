namespace PersonalTradingJournal.Application.Imports.Topstep;

/// <summary>The source contract token is retained; a one/two-digit year is not expanded to a guessed expiry.</summary>
public sealed record TopstepContractIdentity(string SourceContract, string CanonicalSymbol, char MonthCode, string YearCode)
{
    public static TopstepContractIdentity? Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string normalized = value.ToUpperInvariant();
        int digits = normalized.Length >= 3 && char.IsAsciiDigit(normalized[^1]) && char.IsAsciiDigit(normalized[^2]) ? 2 : 1;
        int month = normalized.Length - digits - 1;
        if (month < 1 || !"FGHJKMNQUVXZ".Contains(normalized[month]) ||
            !normalized[(month + 1)..].All(char.IsAsciiDigit)) return null;
        string root = normalized[..month];
        if (!root.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9') ||
            !root.Any(c => c is >= 'A' and <= 'Z')) return null;
        return new(value, root, normalized[month], normalized[(month + 1)..]);
    }
}
