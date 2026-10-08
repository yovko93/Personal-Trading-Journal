namespace PersonalTradingJournal.Infrastructure.DailyReview.Coaching;

/// <summary>Nonsecret configuration. Model/profile is pinned to documentation verified 2026-10-08.
/// Adding another model requires verifying its limits and wire compatibility, not just changing a name.</summary>
public sealed record OpenAiCoachingOptions
{
    public const string SupportedModel = "gpt-4.1-mini-2025-04-14";
    public const int ContextTokens = 1_047_576;
    public string Model { get; init; } = SupportedModel;
    public int MaximumOutputTokens { get; init; } = 8_192;
    public int InputTokenBudget { get; init; } = 300_000;
    internal bool IsValid => Model == SupportedModel && MaximumOutputTokens is > 0 and <= 32_768 &&
        InputTokenBudget > 0 && (long)InputTokenBudget + MaximumOutputTokens <= ContextTokens;
}
