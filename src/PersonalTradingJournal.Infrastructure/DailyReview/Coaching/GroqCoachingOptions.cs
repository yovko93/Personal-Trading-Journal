namespace PersonalTradingJournal.Infrastructure.DailyReview.Coaching;

/// <summary>Conservative Free-plan profile; account limits may be lower or shared with other clients.</summary>
public sealed record GroqCoachingOptions
{
    public const string SupportedModel = "openai/gpt-oss-120b";
    public string Model { get; init; } = SupportedModel;
    public int InputTokenBudget { get; init; } = 5_500;
    public int MaximumOutputTokens { get; init; } = 2_000;
    internal bool IsValid => Model == SupportedModel && InputTokenBudget > 0 && MaximumOutputTokens > 0
        && (long)InputTokenBudget + MaximumOutputTokens <= 7_500;
}
