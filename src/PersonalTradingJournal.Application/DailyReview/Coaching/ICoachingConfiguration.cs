namespace PersonalTradingJournal.Application.DailyReview.Coaching;

public enum CoachingProviderKind { Unavailable, Groq, OpenAI }

/// <summary>Local nonsecret choice. Independent credentials; no fallback between providers.</summary>
public interface ICoachingConfiguration : ICoachingCredentials
{
    CoachingProviderKind SelectedProvider { get; }
    ICoachingCredentials CredentialsFor(CoachingProviderKind provider);
}
