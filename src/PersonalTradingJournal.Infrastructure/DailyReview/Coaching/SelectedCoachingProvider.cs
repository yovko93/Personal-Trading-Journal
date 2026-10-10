using PersonalTradingJournal.Application.DailyReview.Coaching;

namespace PersonalTradingJournal.Infrastructure.DailyReview.Coaching;

/// <summary>Capture selects one immutable route; a missing key or failure never tries the other provider.</summary>
public sealed class SelectedCoachingProvider(ICoachingConfiguration configuration,
    ICoachingProvider openAi, ICoachingProvider groq) : ICoachingProvider
{
    public ICoachingProvider Capture() => configuration.SelectedProvider switch
    {
        CoachingProviderKind.OpenAI => openAi, CoachingProviderKind.Groq => groq,
        _ => new UnavailableProvider(),
    };
    public Task<CoachingProviderReply> GenerateAsync(CoachingEvidencePacket packet, CancellationToken token) =>
        Capture().GenerateAsync(packet, token);
    private sealed class UnavailableProvider : ICoachingProvider
    {
        public Task<CoachingProviderReply> GenerateAsync(CoachingEvidencePacket packet, CancellationToken token) =>
            Task.FromResult(new CoachingProviderReply(CoachingGenerationStatus.InvalidConfiguration, null, null));
    }
}
