namespace PersonalTradingJournal.Application.DailyReview.Coaching;

public enum CoachingCredentialSource { None, Saved, Environment, Unreadable }

/// <summary>Local configuration only. Never contacts the provider; never cache the resolved secret.</summary>
public interface ICoachingCredentials
{
    CoachingCredentialSource GetSource();
    string? Resolve();
}

/// <summary>Sanitized boundary exception. Must not include the secret or the underlying exception.</summary>
public sealed class CoachingCredentialException() : Exception("AI credentials cannot be read. Replace or remove the saved key in Settings.");
