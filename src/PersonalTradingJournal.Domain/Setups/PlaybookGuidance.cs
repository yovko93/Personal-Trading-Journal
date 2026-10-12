namespace PersonalTradingJournal.Domain.Setups;

/// <summary>User-authored guidance, not executable rules or evidence of compliance.</summary>
public sealed class PlaybookGuidance
{
    public const int MaximumFieldLength = 4000;
    public const int MaximumExamples = 12;

    public PlaybookGuidance(string marketContext, string entryConditions, string confirmations,
        string invalidation, string executionGuidance, IEnumerable<PlaybookExample>? examples = null)
    {
        MarketContext = RequiredText(marketContext, nameof(marketContext));
        EntryConditions = RequiredText(entryConditions, nameof(entryConditions));
        Confirmations = RequiredText(confirmations, nameof(confirmations));
        Invalidation = RequiredText(invalidation, nameof(invalidation));
        ExecutionGuidance = RequiredText(executionGuidance, nameof(executionGuidance));
        PlaybookExample[] copy = (examples ?? []).Take(MaximumExamples + 1).ToArray();
        if (copy.Length > MaximumExamples || copy.Any(example => example is null))
            throw new ArgumentException("Supply at most 12 non-null examples.", nameof(examples));
        if (copy.Select(example => example.Id).Distinct().Count() != copy.Length)
            throw new ArgumentException("Example identities must be unique within a revision.", nameof(examples));
        Examples = Array.AsReadOnly(copy);
    }

    public string MarketContext { get; }
    public string EntryConditions { get; }
    public string Confirmations { get; }
    public string Invalidation { get; }
    public string ExecutionGuidance { get; }
    public IReadOnlyList<PlaybookExample> Examples { get; }

    internal static string RequiredText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Meaningful Playbook text is required.", parameterName);
        string normalized = value.Trim();
        if (normalized.Length > MaximumFieldLength)
            throw new ArgumentOutOfRangeException(parameterName, "Playbook text exceeds 4000 characters.");
        return normalized;
    }
}
