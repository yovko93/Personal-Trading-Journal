using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.ML.Tokenizers;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using static PersonalTradingJournal.Infrastructure.DailyReview.Coaching.OpenAiCoachingProvider;

namespace PersonalTradingJournal.Infrastructure.DailyReview.Coaching;

/// <summary>Dedicated Chat Completions wire protocol. No tools, streaming, retries or cross-provider fallback.</summary>
public sealed class GroqCoachingProvider(HttpClient client, GroqCoachingOptions options,
    Func<string?> credentialAccessor) : ICoachingProvider
{
    // Vocabulary ships with the application. Token counting never downloads data or contacts a provider.
    private static readonly Lazy<TiktokenTokenizer> Tokenizer = new(() => TiktokenTokenizer.CreateForEncoding("o200k_base"));
    public async Task<CoachingProviderReply> GenerateAsync(CoachingEvidencePacket packet, CancellationToken cancellationToken)
    {
        var metadata = new CoachingRequestMetadata("Groq", GroqCoachingOptions.SupportedModel, Guid.NewGuid().ToString("N"));
        CoachingProviderReply Reply(CoachingGenerationStatus status, string? json = null) => new(status, json, metadata);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!options.IsValid) return Reply(CoachingGenerationStatus.InvalidConfiguration);
            string? key;
            try { key = credentialAccessor(); }
            catch (CoachingCredentialException) { return Reply(CoachingGenerationStatus.MissingCredentials); }
            if (string.IsNullOrWhiteSpace(key)) return Reply(CoachingGenerationStatus.MissingCredentials);
            if (key.Any(c => c < 33 || c > 126)) return Reply(CoachingGenerationStatus.AuthenticationFailed);
            metadata = metadata with { Phase = CoachingGenerationPhase.EvidencePreflight };
            string body = JsonSerializer.Serialize(new
            {
                model = options.Model,
                messages = new[] { new { role = "system", content = OpenAiCoachingContract.Instructions },
                    new { role = "user", content = packet.Json } },
                response_format = new { type = "json_schema", json_schema = new {
                    name = "daily_coaching_v1", strict = true, schema = OpenAiCoachingContract.Schema() } },
                max_completion_tokens = options.MaximumOutputTokens, reasoning_effort = "low", stream = false, n = 1,
            });
            // HTTP JSON escaping is transport, not model input. Count decoded message content and
            // schema once, plus the complete remaining request framing, 10% and 512 tokens.
            // This remains an estimate, not Groq's Harmony accounting or remaining organization TPM.
            var budget = MeasureInput(body);
            metadata = metadata with { InputBudget = budget };
            if (budget.EstimatedInputTokens > options.InputTokenBudget)
                return Reply(CoachingGenerationStatus.InputTooLarge);
            cancellationToken.ThrowIfCancellationRequested();
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            request.Headers.Add("X-Client-Request-Id", metadata.ClientRequestId);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            metadata = metadata with { Phase = CoachingGenerationPhase.HttpRequest };
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            metadata = metadata with { HttpStatus = (int)response.StatusCode, Phase = CoachingGenerationPhase.HttpResponse,
                RequestId = response.Headers.TryGetValues("x-request-id", out var ids) ? SafeId(ids.FirstOrDefault(), "req_", key) : null,
                RetryAfter = RetryDelay(response) };
            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    using var rejected = JsonDocument.Parse(await ReadBounded(response.Content, cancellationToken, 65_536), new() { MaxDepth = 16 });
                    if (rejected.RootElement.ValueKind == JsonValueKind.Object && rejected.RootElement.TryGetProperty("error", out var error))
                        metadata = metadata with { ErrorCode = CoachingSafeDiagnostics.Code(String(error, "code")),
                            ErrorType = CoachingSafeDiagnostics.Type(String(error, "type")) };
                }
                catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException) { }
                return Reply(ClassifyFailure((int)response.StatusCode, metadata.ErrorCode, metadata.ErrorType));
            }
            metadata = metadata with { Phase = CoachingGenerationPhase.ProviderResponseValidation };
            using var document = JsonDocument.Parse(await ReadBounded(response.Content, cancellationToken), new() { MaxDepth = 64 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                root.TryGetProperty("error", out var providerError) && providerError.ValueKind != JsonValueKind.Null)
                return Reply(CoachingGenerationStatus.InvalidResponse);
            metadata = metadata with { ResponseId = SafeId(String(root, "id"), "chatcmpl-", key), Usage = GroqUsage(root) };
            if (String(root, "model") != options.Model || String(root, "object") != "chat.completion")
                return Reply(CoachingGenerationStatus.InvalidResponse);
            if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() != 1)
                return Reply(CoachingGenerationStatus.InvalidResponse);
            var choice = choices[0];
            if (String(choice, "finish_reason") == "content_filter") return Reply(CoachingGenerationStatus.Refused);
            if (String(choice, "finish_reason") != "stop") return Reply(CoachingGenerationStatus.IncompleteResponse);
            if (!choice.TryGetProperty("message", out var message) || String(message, "role") != "assistant")
                return Reply(CoachingGenerationStatus.InvalidResponse);
            if (message.TryGetProperty("refusal", out var refusal) && refusal.ValueKind != JsonValueKind.Null)
                return Reply(CoachingGenerationStatus.Refused);
            if (message.TryGetProperty("tool_calls", out var calls) && calls.ValueKind != JsonValueKind.Null)
                return Reply(CoachingGenerationStatus.InvalidResponse);
            if (String(message, "content") is not { } json || Encoding.UTF8.GetByteCount(json) > CoachingContract.MaximumResponseBytes)
                return Reply(CoachingGenerationStatus.InvalidResponse);
            cancellationToken.ThrowIfCancellationRequested();
            return Reply(CoachingGenerationStatus.Success, json);
        }
        catch (OperationCanceledException) { return Reply(CoachingGenerationStatus.Cancelled); }
        catch (HttpRequestException) { return Reply(CoachingGenerationStatus.ServiceUnavailable); }
        catch (IOException) { return Reply(CoachingGenerationStatus.ServiceUnavailable); }
        catch (Exception) { return Reply(CoachingGenerationStatus.InvalidResponse); }
    }

    private CoachingInputBudget MeasureInput(string body)
    {
        var request = System.Text.Json.Nodes.JsonNode.Parse(body)!;
        var messages = request["messages"]!;
        int instructions = Tokenizer.Value.CountTokens(messages[0]!["content"]!.GetValue<string>());
        int evidence = Tokenizer.Value.CountTokens(messages[1]!["content"]!.GetValue<string>());
        var schema = request["response_format"]!["json_schema"]!;
        int schemaTokens = Tokenizer.Value.CountTokens(schema["schema"]!.ToJsonString());
        messages[0]!["content"] = "";
        messages[1]!["content"] = "";
        schema["schema"] = null;
        int framing = Tokenizer.Value.CountTokens(request.ToJsonString());
        int total = instructions + evidence + schemaTokens + framing;
        return new(instructions, schemaTokens, evidence, framing, (int)Math.Ceiling(total * .1),
            512, options.InputTokenBudget, options.MaximumOutputTokens);
    }

    private static CoachingTokenUsage? GroqUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object) return null;
        static long? Count(JsonElement item, string name) => item.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long n) && n >= 0 ? n : null;
        long? input = Count(usage, "prompt_tokens"), output = Count(usage, "completion_tokens"), total = Count(usage, "total_tokens");
        if (input is { } i && output is { } o && total is { } t && (decimal)i + o != t) return null;
        return new(input, null, output, total);
    }
}
