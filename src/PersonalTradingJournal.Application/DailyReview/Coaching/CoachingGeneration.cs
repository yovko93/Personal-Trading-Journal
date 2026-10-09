namespace PersonalTradingJournal.Application.DailyReview.Coaching;

public enum CoachingGenerationStatus
{
    Success, MissingCredentials, InvalidConfiguration, AuthenticationFailed, AccessDenied,
    InputTooLarge, RateLimited, QuotaExceeded, ServiceUnavailable, ProviderFailure,
    Refused, IncompleteResponse, InvalidResponse, Cancelled, TimedOut,
}

public sealed record CoachingTokenUsage(long? InputTokens, long? CachedInputTokens,
    long? OutputTokens, long? TotalTokens);

/// <summary>Only allowlisted operational fields; never prompts, source text or error bodies.
/// Null usage/cost means unknown, not zero. No verified pricing configuration is installed in M15.4.</summary>
public sealed record CoachingRequestMetadata(string Provider, string Model, string ClientRequestId,
    string? RequestId = null, string? ResponseId = null, CoachingTokenUsage? Usage = null,
    int? HttpStatus = null, TimeSpan? RetryAfter = null)
{
    public decimal? MonetaryCost => null;
    public string CostStatus => "Unknown: no verified pricing configuration.";
}

/// <summary>Internal provider boundary. Success means transport completed, not validated coaching.
/// Consumers should invoke DailyCoachingGenerationService, never publish this raw response.</summary>
public sealed record CoachingProviderReply(CoachingGenerationStatus Status, string? ResponseJson,
    CoachingRequestMetadata? Metadata)
{
    public override string ToString() => $"CoachingProviderReply: {Status}";
}

public interface ICoachingProvider
{
    Task<CoachingProviderReply> GenerateAsync(CoachingEvidencePacket packet, CancellationToken cancellationToken);
}

public sealed record CoachingGenerationResult(CoachingGenerationStatus Status, CoachingResponse? Review,
    CoachingRequestMetadata? Metadata, string Message)
{
    public override string ToString() => $"CoachingGenerationResult: {Status}";
}

public sealed record CoachingGenerationOptions
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(90);
}

/// <summary>No startup, read, refresh or write hook. Each explicit invocation submits at most once.
/// Local cancellation cannot guarantee that an already accepted provider request was not billed.</summary>
public sealed class DailyCoachingGenerationService(ICoachingProvider provider,
    CoachingGenerationOptions options, TimeProvider timeProvider)
{
    public async Task<CoachingGenerationResult> GenerateAsync(CoachingEvidencePacket packet,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if (cancellationToken.IsCancellationRequested) return Result(CoachingGenerationStatus.Cancelled);
        if (options.Timeout <= TimeSpan.Zero || options.Timeout > TimeSpan.FromMinutes(3))
            return Result(CoachingGenerationStatus.InvalidConfiguration);
        using var deadline = new CancellationTokenSource(options.Timeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        CoachingRequestMetadata? metadata = null;
        try
        {
            linked.Token.ThrowIfCancellationRequested();
            var pending = provider.GenerateAsync(packet, linked.Token);
            // Observe late faults even for a noncooperative adapter; never publish its late result.
            _ = pending.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            var reply = await pending.WaitAsync(linked.Token);
            metadata = reply.Metadata;
            linked.Token.ThrowIfCancellationRequested();
            if (reply.Status != CoachingGenerationStatus.Success) return Result(reply.Status, metadata);
            if (reply.ResponseJson is null) return Result(CoachingGenerationStatus.InvalidResponse, metadata);
            var validation = CoachingResponseValidator.Validate(reply.ResponseJson, packet, linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            return validation.IsValid
                ? new(CoachingGenerationStatus.Success, validation.Response, metadata, "Review generated and source references validated.")
                : Result(CoachingGenerationStatus.InvalidResponse, metadata);
        }
        catch (OperationCanceledException)
        {
            return Result(cancellationToken.IsCancellationRequested ? CoachingGenerationStatus.Cancelled :
                deadline.IsCancellationRequested ? CoachingGenerationStatus.TimedOut : CoachingGenerationStatus.ProviderFailure, metadata);
        }
        catch (Exception)
        {
            // This external-service boundary deliberately does not log/return exception messages:
            // a provider exception may embed credentials, request text or response bodies.
            return Result(CoachingGenerationStatus.ProviderFailure, metadata);
        }
    }

    private static CoachingGenerationResult Result(CoachingGenerationStatus status, CoachingRequestMetadata? metadata = null) =>
        new(status, null, metadata, status switch
        {
            CoachingGenerationStatus.MissingCredentials => "Configure AI in Settings. The OpenAI key is missing or its saved storage cannot be read.",
            CoachingGenerationStatus.InvalidConfiguration => "Check the supported model, input/output budget and positive timeout (at most 180 seconds).",
            CoachingGenerationStatus.AuthenticationFailed => "The provider rejected the credential. Check or replace the API key.",
            CoachingGenerationStatus.AccessDenied => "Check API project permissions and access to the configured model.",
            CoachingGenerationStatus.InputTooLarge => "Complete evidence exceeds the configured input budget. Select an explicit narrower scope; nothing was truncated.",
            CoachingGenerationStatus.RateLimited => "The provider rate-limited this request. Respect RetryAfter before manually generating again.",
            CoachingGenerationStatus.QuotaExceeded => "Check the provider's billing balance and project/organization limits before generating again.",
            CoachingGenerationStatus.ServiceUnavailable => "Check the network or provider availability before manually generating again.",
            CoachingGenerationStatus.Refused => "The provider declined this request. No coaching review was accepted.",
            CoachingGenerationStatus.IncompleteResponse => "The provider did not finish the review. No partial review was accepted.",
            CoachingGenerationStatus.InvalidResponse => "The response failed structure, packet identity or citation validation. No review was accepted.",
            CoachingGenerationStatus.Cancelled => "Generation cancelled locally. An accepted provider request may still incur a charge; it was not retried.",
            CoachingGenerationStatus.TimedOut => "Generation timed out. The provider may still have processed the request; it was not retried.",
            _ => "Generation failed. Check provider configuration/availability before manually trying again; no automatic retry was made.",
        });
}
