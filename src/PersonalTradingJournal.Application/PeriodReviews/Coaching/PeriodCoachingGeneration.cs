using PersonalTradingJournal.Application.DailyReview.Coaching;

namespace PersonalTradingJournal.Application.PeriodReviews.Coaching;

public interface IPeriodCoachingProvider
{
    IPeriodCoachingProvider Capture() => this;
    Task<CoachingProviderReply> GenerateAsync(PeriodCoachingPacket packet, CancellationToken token);
}
public enum PeriodCoachingStatus
{
    Success, TooLarge, NoEvidence, InvalidEvidence, EvidenceFailure, CalculationOverflow,
    MissingCredentials, InvalidConfiguration, AuthenticationFailed, AccessDenied, RateLimited,
    QuotaExceeded, ServiceUnavailable, ProviderFailure, Refused, IncompleteResponse,
    InvalidResponse, Cancelled, TimedOut, ModelUnavailable, InvalidRequest,
}
public sealed record PeriodCoachingResult(PeriodCoachingStatus Status, PeriodCoachingResponse? Review,
    PeriodCoachingPacket? Packet, CoachingRequestMetadata? Metadata, string Message)
{
    public override string ToString() => $"PeriodCoachingResult: {Status}";
}

/// <summary>Explicit invocation only. Captures provider before reading; one read pair and at most one POST.
/// No persistence dependency. Deadline covers read, packet construction, transport and validation.</summary>
public sealed class PeriodCoachingGenerationService(IPeriodComparisonEvidenceReader reader, IPeriodCoachingProvider provider,
    CoachingGenerationOptions options, TimeProvider clock)
{
    public async Task<PeriodCoachingResult> GenerateAsync(PeriodReviewQuery query, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (token.IsCancellationRequested) return Failure(PeriodCoachingStatus.Cancelled);
        if (options.Timeout <= TimeSpan.Zero || options.Timeout > TimeSpan.FromMinutes(3)) return Failure(PeriodCoachingStatus.InvalidConfiguration);
        using var deadline = new CancellationTokenSource(options.Timeout, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, deadline.Token);
        CoachingRequestMetadata? metadata = null;
        bool reading = true;
        try
        {
            var route = provider.Capture();
            var evidence = await Await(reader.ReadComparisonAsync(query, linked.Token), linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            if (evidence.Status == PeriodEvidenceStatus.LimitExceeded) return Failure(PeriodCoachingStatus.TooLarge);
            if (evidence.Evidence is not { } pair || pair.Current.Query != query) return Failure(PeriodCoachingStatus.InvalidEvidence);
            if (pair.Current.ClosedTrades.Count + pair.Current.ActivityContext.Count + pair.Current.Assessments.Count == 0 &&
                !pair.Current.Journals.Any(j => new[] { j.Text, j.Answers.WentWell, j.Answers.NeedsImprovement, j.Answers.NextTradingDay }.Any(s => !string.IsNullOrWhiteSpace(s))))
                return Failure(PeriodCoachingStatus.NoEvidence);
            var build = PeriodCoachingPacketBuilder.Build(pair, linked.Token);
            if (build.Packet is not { } packet) return Failure(build.Status switch
            {
                CoachingPacketBuildStatus.TooLarge => PeriodCoachingStatus.TooLarge,
                CoachingPacketBuildStatus.CalculationOverflow => PeriodCoachingStatus.CalculationOverflow,
                _ => PeriodCoachingStatus.InvalidEvidence,
            });
            reading = false;
            var reply = await Await(route.GenerateAsync(packet, linked.Token), linked.Token);
            metadata = reply.Metadata; linked.Token.ThrowIfCancellationRequested();
            if (reply.Status != CoachingGenerationStatus.Success)
                return Failure(reply.Status == CoachingGenerationStatus.InputTooLarge ? PeriodCoachingStatus.TooLarge :
                    Enum.Parse<PeriodCoachingStatus>(reply.Status.ToString()), metadata);
            metadata = metadata is null ? null : metadata with { Phase = CoachingGenerationPhase.ResponseValidation };
            if (reply.ResponseJson is null) return Failure(PeriodCoachingStatus.InvalidResponse, metadata);
            var validation = PeriodCoachingResponseValidator.Validate(reply.ResponseJson, packet, linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            return validation.IsValid ? new(PeriodCoachingStatus.Success, validation.Response, packet, metadata,
                "Period review generated and source references validated. It has not been saved.") : Failure(PeriodCoachingStatus.InvalidResponse, metadata);
        }
        catch (OperationCanceledException)
        {
            return Failure(token.IsCancellationRequested ? PeriodCoachingStatus.Cancelled : deadline.IsCancellationRequested
                ? PeriodCoachingStatus.TimedOut : PeriodCoachingStatus.ProviderFailure, metadata);
        }
        catch (Exception) { return Failure(reading ? PeriodCoachingStatus.EvidenceFailure : PeriodCoachingStatus.ProviderFailure, metadata); }
    }

    private static async Task<T> Await<T>(Task<T> pending, CancellationToken token)
    {
        _ = pending.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return await pending.WaitAsync(token);
    }
    private static PeriodCoachingResult Failure(PeriodCoachingStatus status, CoachingRequestMetadata? metadata = null)
    {
        string message = status switch
        {
            PeriodCoachingStatus.NoEvidence => "No usable Trade, Journal or recorded assessment evidence exists in the selected period. Nothing was sent.",
            PeriodCoachingStatus.InvalidEvidence => "Period evidence or source consistency is invalid. Nothing was sent; refresh current data before trying again.",
            PeriodCoachingStatus.EvidenceFailure => "Period evidence could not be read. No review was generated. Refresh before trying again.",
            PeriodCoachingStatus.CalculationOverflow => "Period calculations exceed decimal capacity. Nothing was sent.",
            PeriodCoachingStatus.TooLarge when metadata is null => "The local snapshot or projection exceeds a safety limit (2 MiB snapshot, 256 KiB per source/projection, 10,000 sources, or calculation limits). Nothing was sent. Keep this period review local.",
            PeriodCoachingStatus.TooLarge when metadata.InputBudget is { } b => $"This projection needs about {b.EstimatedInputTokens:N0} input tokens; {metadata.Provider}’s limit is {b.InputLimit:N0}. Nothing was sent. Keep this review local, or choose a higher-budget provider in Settings.",
            _ => DailyCoachingGenerationService.Result(status == PeriodCoachingStatus.TooLarge ? CoachingGenerationStatus.InputTooLarge :
                Enum.Parse<CoachingGenerationStatus>(status.ToString()), metadata).Message.Replace("complete day", "complete period", StringComparison.OrdinalIgnoreCase),
        };
        return new(status, null, null, metadata, message);
    }
}
