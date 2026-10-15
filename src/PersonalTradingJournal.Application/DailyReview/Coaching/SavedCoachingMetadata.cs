namespace PersonalTradingJournal.Application.DailyReview.Coaching;

/// <summary>Shared successful-generation metadata allowlist. Never persists diagnostics or raw HTTP data.</summary>
internal static class SavedCoachingMetadata
{
    internal static SavedCoachingProviderMetadata Create(CoachingRequestMetadata metadata)
    {
        static bool Identifier(string? value, int maximum) => value is { Length: > 0 } && value.Length <= maximum &&
            value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':');
        static bool ModelIdentifier(string? value) => Identifier(value, 128) ||
            value is { Length: <= 128 } && value.Split('/') is [var owner, var model] && Identifier(owner, 128) && Identifier(model, 128);
        if (!Identifier(metadata.Provider, 64) || !ModelIdentifier(metadata.Model) || !Identifier(metadata.ClientRequestId, 128) ||
            metadata.RequestId is not null && !Identifier(metadata.RequestId, 128) ||
            metadata.ResponseId is not null && !Identifier(metadata.ResponseId, 128))
            throw new ArgumentException("Provider metadata contains invalid identifiers.");
        var usage = metadata.Usage;
        if (usage is not null && (usage.InputTokens < 0 || usage.CachedInputTokens < 0 || usage.OutputTokens < 0 ||
            usage.TotalTokens < 0 || usage.CachedInputTokens > usage.InputTokens ||
            usage.InputTokens is { } input && usage.OutputTokens is { } output && usage.TotalTokens is { } total &&
            (decimal)input + output != total)) throw new ArgumentException("Provider usage is inconsistent.");
        return new(metadata.Provider, metadata.Model, metadata.ClientRequestId, metadata.RequestId, metadata.ResponseId, usage);
    }
}
