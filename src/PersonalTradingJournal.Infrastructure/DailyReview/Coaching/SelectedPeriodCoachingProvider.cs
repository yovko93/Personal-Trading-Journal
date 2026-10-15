using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Application.PeriodReviews.Coaching;

namespace PersonalTradingJournal.Infrastructure.DailyReview.Coaching;

public sealed class SelectedPeriodCoachingProvider(ICoachingConfiguration configuration,
    IPeriodCoachingProvider openAi, IPeriodCoachingProvider groq) : IPeriodCoachingProvider
{
    public IPeriodCoachingProvider Capture() => configuration.SelectedProvider switch
    {
        CoachingProviderKind.OpenAI => openAi, CoachingProviderKind.Groq => groq, _ => new Unavailable(),
    };
    public Task<CoachingProviderReply> GenerateAsync(PeriodCoachingPacket packet, CancellationToken token) => Capture().GenerateAsync(packet, token);
    private sealed class Unavailable : IPeriodCoachingProvider
    {
        public Task<CoachingProviderReply> GenerateAsync(PeriodCoachingPacket packet, CancellationToken token) =>
            Task.FromResult(new CoachingProviderReply(CoachingGenerationStatus.InvalidConfiguration, null, null));
    }
}
