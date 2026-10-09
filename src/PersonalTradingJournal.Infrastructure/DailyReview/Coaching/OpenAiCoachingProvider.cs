using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PersonalTradingJournal.Application.DailyReview.Coaching;

namespace PersonalTradingJournal.Infrastructure.DailyReview.Coaching;

/// <summary>One non-streaming Responses POST, no retries, tools, conversation state or body logging.
/// HttpClient is owned by DI/caller. Credential accessor runs only on explicit generation.</summary>
public sealed class OpenAiCoachingProvider(HttpClient client, OpenAiCoachingOptions options,
    Func<string?> credentialAccessor) : ICoachingProvider
{
    private const int MaximumEnvelopeBytes = 1_048_576;
    private static readonly Uri Endpoint = new("https://api.openai.com/v1/responses");

    public async Task<CoachingProviderReply> GenerateAsync(CoachingEvidencePacket packet, CancellationToken cancellationToken)
    {
        var metadata = new CoachingRequestMetadata("OpenAI", OpenAiCoachingOptions.SupportedModel, Guid.NewGuid().ToString("N"));
        CoachingProviderReply Reply(CoachingGenerationStatus status, string? body = null) => new(status, body, metadata);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!options.IsValid) return Reply(CoachingGenerationStatus.InvalidConfiguration);
            string? key;
            try { key = credentialAccessor(); }
            catch (CoachingCredentialException) { return Reply(CoachingGenerationStatus.MissingCredentials); }
            if (string.IsNullOrWhiteSpace(key)) return Reply(CoachingGenerationStatus.MissingCredentials);
            if (key.Any(char.IsWhiteSpace) || key.Any(c => c < 33 || c > 126))
                return Reply(CoachingGenerationStatus.AuthenticationFailed);
            metadata = metadata with { Phase = CoachingGenerationPhase.EvidencePreflight };
            string body = OpenAiCoachingContract.RequestJson(packet, options);
            // Conservative local ceiling: request UTF-8 bytes (including schema and escaped data)
            // plus 16K framing reserve. Not a tokenizer count; server context rejection is also handled.
            if ((long)Encoding.UTF8.GetByteCount(body) + 16_384 > options.InputTokenBudget)
                return Reply(CoachingGenerationStatus.InputTooLarge);
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            request.Headers.Add("X-Client-Request-Id", metadata.ClientRequestId);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            metadata = metadata with { Phase = CoachingGenerationPhase.HttpRequest };
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            metadata = metadata with
            {
                HttpStatus = (int)response.StatusCode,
                Phase = CoachingGenerationPhase.HttpResponse,
                RequestId = response.Headers.TryGetValues("x-request-id", out var values)
                    ? SafeId(values.FirstOrDefault(), "req_", key) : null,
                RetryAfter = RetryDelay(response),
            };
            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    using var rejected = JsonDocument.Parse(await ReadBounded(response.Content, cancellationToken, 65_536), new() { MaxDepth = 16 });
                    if (rejected.RootElement.ValueKind == JsonValueKind.Object && rejected.RootElement.TryGetProperty("error", out var error))
                        metadata = metadata with { ErrorCode = CoachingSafeDiagnostics.Code(String(error, "code")),
                            ErrorType = CoachingSafeDiagnostics.Type(String(error, "type")) };
                }
                catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException)
                { /* Preserve HTTP status; malformed/oversized/unreadable error details remain unknown. */ }
                return Reply(ClassifyFailure((int)response.StatusCode, metadata.ErrorCode, metadata.ErrorType));
            }
            metadata = metadata with { Phase = CoachingGenerationPhase.ProviderResponseValidation };
            byte[] bytes = await ReadBounded(response.Content, cancellationToken);
            using var document = JsonDocument.Parse(bytes, new() { MaxDepth = 64 });
            var root = document.RootElement;
            metadata = metadata with { ResponseId = SafeId(String(root, "id"), "resp_", key), Usage = Usage(root) };
            if (String(root, "model") != options.Model) return Reply(CoachingGenerationStatus.InvalidResponse);
            string? status = String(root, "status");
            if (status == "incomplete") return Reply(CoachingGenerationStatus.IncompleteResponse);
            if (status != "completed") return Reply(CoachingGenerationStatus.ProviderFailure);
            if (root.TryGetProperty("error", out var providerError) && providerError.ValueKind != JsonValueKind.Null)
                return Reply(CoachingGenerationStatus.ProviderFailure);
            if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array || output.GetArrayLength() != 1)
                return Reply(CoachingGenerationStatus.InvalidResponse);
            var message = output[0];
            if (String(message, "type") != "message" || String(message, "role") != "assistant" ||
                String(message, "status") != "completed")
                return Reply(CoachingGenerationStatus.IncompleteResponse);
            if (!message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array || content.GetArrayLength() != 1)
                return Reply(CoachingGenerationStatus.InvalidResponse);
            var part = content[0];
            if (String(part, "type") == "refusal") return Reply(CoachingGenerationStatus.Refused);
            if (String(part, "type") != "output_text" || String(part, "text") is not { } json)
                return Reply(CoachingGenerationStatus.InvalidResponse);
            if (Encoding.UTF8.GetByteCount(json) > CoachingContract.MaximumResponseBytes)
                return Reply(CoachingGenerationStatus.InvalidResponse);
            cancellationToken.ThrowIfCancellationRequested();
            return Reply(CoachingGenerationStatus.Success, json);
        }
        catch (OperationCanceledException) { return Reply(CoachingGenerationStatus.Cancelled); }
        catch (HttpRequestException) { return Reply(CoachingGenerationStatus.ServiceUnavailable); }
        catch (InvalidDataException) { return Reply(CoachingGenerationStatus.InvalidResponse); }
        catch (IOException) { return Reply(CoachingGenerationStatus.ServiceUnavailable); }
        catch (Exception)
        {
            // Never surface exception messages or raw provider bodies (including echoed secrets).
            return Reply(metadata.HttpStatus == 429 ? CoachingGenerationStatus.RateLimited : CoachingGenerationStatus.InvalidResponse);
        }
    }

    internal static CoachingGenerationStatus ClassifyFailure(int status, string? code, string? type) => code switch
    {
        "invalid_api_key" or "invalid_authentication" => CoachingGenerationStatus.AuthenticationFailed,
        "model_not_found" or "model_not_available" => CoachingGenerationStatus.ModelUnavailable,
        "permission_denied" or "insufficient_permissions" or "model_permission_blocked" or "model_permission_denied" => CoachingGenerationStatus.AccessDenied,
        "insufficient_quota" or "credit_balance_exhausted" or "organization_spend_limit_exceeded" or
        "project_spend_limit_exceeded" or "organization_usage_limit_exceeded" or "billing_hard_limit_reached" => CoachingGenerationStatus.QuotaExceeded,
        "context_length_exceeded" or "input_too_large" => CoachingGenerationStatus.InputTooLarge,
        "rate_limit_exceeded" => CoachingGenerationStatus.RateLimited,
        "invalid_json_schema" or "invalid_request" or "invalid_request_error" or "unsupported_parameter" or "invalid_value" or "json_validate_failed" => CoachingGenerationStatus.InvalidRequest,
        "server_error" or "server_is_overloaded" => CoachingGenerationStatus.ServiceUnavailable,
        _ => status switch
        {
            401 => CoachingGenerationStatus.AuthenticationFailed,
            402 => CoachingGenerationStatus.QuotaExceeded,
            403 => CoachingGenerationStatus.AccessDenied,
            413 => CoachingGenerationStatus.InputTooLarge,
            429 when type == "insufficient_quota" => CoachingGenerationStatus.QuotaExceeded,
            429 => CoachingGenerationStatus.RateLimited,
            400 or 422 when type == "invalid_request_error" => CoachingGenerationStatus.InvalidRequest,
            >= 500 => CoachingGenerationStatus.ServiceUnavailable,
            _ => CoachingGenerationStatus.ProviderFailure,
        },
    };

    internal static async Task<byte[]> ReadBounded(HttpContent content, CancellationToken token, int maximumBytes = MaximumEnvelopeBytes)
    {
        if (content.Headers.ContentLength > maximumBytes) throw new InvalidDataException("Envelope exceeds limit.");
        await using var stream = await content.ReadAsStreamAsync(token);
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[8192];
        while (true)
        {
            int read = await stream.ReadAsync(chunk, token);
            if (read == 0) return buffer.ToArray();
            if (buffer.Length + read > maximumBytes) throw new InvalidDataException("Envelope exceeds limit.");
            buffer.Write(chunk, 0, read);
        }
    }

    internal static string? String(JsonElement item, string property) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;
    internal static string? SafeId(string? value, string prefix, string key) =>
        value is { Length: <= 128 } && value.StartsWith(prefix, StringComparison.Ordinal) &&
        !value.Contains(key, StringComparison.Ordinal) && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-')
            ? value : null;
    internal static TimeSpan? RetryDelay(HttpResponseMessage response)
    {
        var value = response.Headers.RetryAfter;
        var delay = value?.Delta ?? (value?.Date is { } date ? date - DateTimeOffset.UtcNow : (TimeSpan?)null);
        return delay is { } duration && duration > TimeSpan.Zero ? duration : null;
    }
    private static CoachingTokenUsage? Usage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object) return null;
        static long? Count(JsonElement item, string name) => item.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long number) && number >= 0 ? number : null;
        long? input = Count(usage, "input_tokens"), output = Count(usage, "output_tokens"), total = Count(usage, "total_tokens");
        long? cached = usage.TryGetProperty("input_tokens_details", out var details) && details.ValueKind == JsonValueKind.Object
            ? Count(details, "cached_tokens") : null;
        if (cached > input || input is { } i && output is { } o && total is { } t && (decimal)i + o != t) return null;
        return new(input, cached, output, total);
    }
}
