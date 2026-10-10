using System.Net;
using System.Text.Json;
using Microsoft.ML.Tokenizers;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Domain.Journals;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.DailyReview.Coaching;
using Xunit.Abstractions;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.DailyReview;

public sealed class GroqInputBudgetTests(ITestOutputHelper output)
{
    internal static CoachingEvidencePacket Packet(int count)
    {
        Guid Id(int n) => new(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"synthetic-{n}"))[..16]);
        var account = Id(1);
        var close = new DateTimeOffset(2026, 10, 2, 14, 0, 0, TimeSpan.Zero);
        var trades = Enumerable.Range(0, count).Select(i => new DailyReviewTradeEvidence(
            Id(100 + i), new(account, "Synthetic account", true), new(Id(2), "MNQ", true), null, "USD", 2m,
            close.AddHours(-1), close.AddSeconds(i), DailyReviewTradeInclusion.ClosedOnDate,
            new(1, TradeStatus.Closed, TradeDirection.Long, close.AddHours(-1), close.AddSeconds(i), 0, 100, 125, null, 50, null),
            [new(Id(1000 + i * 2), 1, close.AddHours(-1), ExecutionSide.Buy, 1, 100, null, null, null, null, null, null),
             new(Id(1001 + i * 2), 2, close.AddSeconds(i), ExecutionSide.Sell, 1, 125, null, null, null, null, null, null)], [],
            DailyReviewTradeQuality.UnknownCommission | DailyReviewTradeQuality.UnknownFees | DailyReviewTradeQuality.UnknownNetPnL)).ToArray();
        return CoachingEvidencePacketBuilder.Build(new(new(new DateOnly(2026, 10, 2), account), trades, [])).Packet!;
    }

    [Theory]
    [InlineData("empty", true)]
    [InlineData("two", true)]
    [InlineData("thirty-one", false)]
    [InlineData("long-journal", false)]
    [InlineData("mixed", false)]
    public async Task CompleteSyntheticPacketIsEitherValidatedAndSavedOrRejectedBeforeHttp(string scenario, bool fits)
    {
        var packet = Scenario(scenario);
        string before = packet.Json;
        await using var db = await ReaderTestDatabase.CreateAsync();
        var repository = db.ServiceProvider.GetRequiredService<ICoachingAnalysisRepository>();
        using var handler = new Handler(packet);
        using var client = new HttpClient(handler);
        var service = new GenerateAndSaveCoachingService(
            new(new GroqCoachingProvider(client, new(), () => "synthetic-key"), new(), TimeProvider.System),
            repository, TimeProvider.System);
        var result = await service.GenerateAsync(packet);
        var budget = Assert.IsType<CoachingInputBudget>(result.Diagnostics!.InputBudget);
        output.WriteLine($"instructions={budget.InstructionTokens}; schema={budget.SchemaTokens}; evidence={budget.EvidenceTokens}; framing={budget.FramingTokens}; margin={budget.SafetyMarginTokens}; reserve={budget.ReserveTokens}; estimate={budget.EstimatedInputTokens}; input={budget.InputLimit}; output={budget.OutputLimit}");
        Assert.Equal(6800, budget.InputLimit); Assert.Equal(1000, budget.OutputLimit);
        Assert.Equal(512, budget.ReserveTokens);
        Assert.Equal((int)Math.Ceiling((budget.InstructionTokens + budget.SchemaTokens + budget.EvidenceTokens + budget.FramingTokens) * .1), budget.SafetyMarginTokens);
        Assert.Equal(fits, budget.EstimatedInputTokens <= budget.InputLimit);
        Assert.Equal(before, packet.Json);
        var history = await repository.BrowseAsync(new(packet.Content.Query.Date, new(packet.Content.Query.TradingAccountId)));
        Assert.Equal(fits ? 1 : 0, history.TotalCount);
        Assert.Equal(fits ? 1 : 0, handler.Calls);
        if (fits)
        {
            Assert.Equal(SavedCoachingGenerationStatus.Saved, result.Status);
            Assert.Equal(before, result.Analysis!.EvidenceJson);
            Assert.Equal(packet.PacketId, result.Analysis.PacketId);
            var loaded = await repository.GetAsync(result.Analysis.Summary.Id);
            Assert.Equal(before, loaded!.EvidenceJson);
            // Budget diagnostics are not billed token usage and are not part of persisted provider metadata.
            Assert.Equal(new CoachingTokenUsage(50, null, 30, 80), loaded.Metadata.Usage);
            if (scenario == "two")
            {
                var wireTokens = TiktokenTokenizer.CreateForEncoding("o200k_base").CountTokens(handler.Body!);
                output.WriteLine($"wire={wireTokens}; oldEstimate={Math.Ceiling(wireTokens * 1.1) + 512}");
                using var wire = JsonDocument.Parse(handler.Body!);
                var tokenizer = TiktokenTokenizer.CreateForEncoding("o200k_base");
                int escapedInstructions = tokenizer.CountTokens(wire.RootElement.GetProperty("messages")[0].GetProperty("content").GetRawText());
                int escapedEvidence = tokenizer.CountTokens(wire.RootElement.GetProperty("messages")[1].GetProperty("content").GetRawText());
                output.WriteLine($"oldEscapedInstructions={escapedInstructions}; oldEscapedEvidence={escapedEvidence}; oldFramingResidual={wireTokens - escapedInstructions - escapedEvidence - budget.SchemaTokens}");
                Assert.True(Math.Ceiling(wireTokens * 1.1) + 512 > 5500);
                Assert.True(budget.EstimatedInputTokens > 5500);
                Assert.Equal(2, packet.Content.RecordedTradeFacts.Count);
                Assert.All(packet.Content.RecordedTradeFacts, t => {
                    Assert.Null(t.Facts!.NetPnL); Assert.Equal(2, t.Executions.Count);
                    Assert.All(t.Executions, e => Assert.Null(e.Commission));
                });
                Assert.Empty(packet.Content.UntrustedJournalObservations);
            }
        }
        else
        {
            Assert.Equal(CoachingGenerationStatus.InputTooLarge, result.GenerationStatus);
            Assert.Contains("No request was sent", result.Message);
            Assert.DoesNotContain("narrower", result.Message);
            Assert.DoesNotContain("synthetic-key", result.Message);
        }
    }

    [Theory]
    [InlineData("citation")]
    [InlineData("identity")]
    [InlineData("length")]
    [InlineData("cancel-before")]
    [InlineData("cancel-during")]
    public async Task EligibleEvidenceNeverSavesInvalidIncompleteOrCancelledGeneration(string failure)
    {
        var packet = Packet(2);
        await using var db = await ReaderTestDatabase.CreateAsync();
        var repository = db.ServiceProvider.GetRequiredService<ICoachingAnalysisRepository>();
        using var cancel = new CancellationTokenSource();
        if (failure == "cancel-before") cancel.Cancel();
        using var handler = new Handler(packet, failure, cancel);
        using var client = new HttpClient(handler);
        var service = new GenerateAndSaveCoachingService(
            new(new GroqCoachingProvider(client, new(), () => "synthetic-key"), new(), TimeProvider.System),
            repository, TimeProvider.System);
        var result = await service.GenerateAsync(packet, cancel.Token);
        Assert.NotEqual(SavedCoachingGenerationStatus.Saved, result.Status);
        Assert.Null(result.Analysis);
        Assert.Equal(failure.StartsWith("cancel", StringComparison.Ordinal) ? CoachingGenerationStatus.Cancelled :
            failure == "length" ? CoachingGenerationStatus.IncompleteResponse : CoachingGenerationStatus.InvalidResponse,
            result.GenerationStatus);
        Assert.Equal(failure == "cancel-before" ? 0 : 1, handler.Calls);
        Assert.Equal(0, (await repository.BrowseAsync(new(packet.Content.Query.Date, new(packet.Content.Query.TradingAccountId)))).TotalCount);
    }

    [Fact]
    public async Task InclusiveBudgetBoundaryRetainsSafetyReserveAndNeverRetries()
    {
        var packet = Packet(2);
        using var handler = new Handler(packet);
        using var client = new HttpClient(handler);
        var measured = await new GroqCoachingProvider(client, new(), () => "synthetic-key").GenerateAsync(packet, default);
        var estimate = measured.Metadata!.InputBudget!.EstimatedInputTokens;
        foreach (int limit in new[] { 5500, estimate - 1, estimate })
        {
            int before = handler.Calls;
            var reply = await new GroqCoachingProvider(client, new() { InputTokenBudget = limit }, () => "synthetic-key")
                .GenerateAsync(packet, default);
            Assert.Equal(limit == estimate ? CoachingGenerationStatus.Success : CoachingGenerationStatus.InputTooLarge, reply.Status);
            Assert.Equal(before + (limit == estimate ? 1 : 0), handler.Calls);
            Assert.Equal(512, reply.Metadata!.InputBudget!.ReserveTokens);
            Assert.Equal(estimate, reply.Metadata.InputBudget.EstimatedInputTokens);
        }
    }

    private static CoachingEvidencePacket Scenario(string scenario)
    {
        var packet = Packet(scenario == "empty" || scenario == "long-journal" ? 0 : scenario == "thirty-one" ? 31 : 2);
        if (scenario == "mixed")
        {
            var second = packet.Content.RecordedTradeFacts[1] with {
                Account = new(Guid.Parse("830ebccd-ddc3-4025-8f0d-40c71b952da2"), "Other synthetic account", false), PricingCurrency = "EUR" };
            return CoachingEvidencePacketBuilder.Build(new(new(packet.Content.Query.Date),
                [packet.Content.RecordedTradeFacts[0], second], [])).Packet!;
        }
        if (scenario == "long-journal")
        {
            var journal = new DailyReviewJournalEvidence(Guid.Parse("710ebccd-ddc3-4025-8f0d-40c71b952da2"),
                packet.Content.Query.Date, packet.Content.Query.TradingAccountId, "Synthetic account", DailyJournalAccountState.Active,
                string.Join(" ", Enumerable.Range(0, 4000).Select(i => $"Synthetic observation {i}.")), DailyReviewAnswers.Empty,
                true, 1, packet.Content.Query.FromUtc, packet.Content.Query.FromUtc);
            return CoachingEvidencePacketBuilder.Build(new(packet.Content.Query, [], [journal])).Packet!;
        }
        return packet;
    }

    private sealed class Handler(CoachingEvidencePacket packet, string? failure = null,
        CancellationTokenSource? cancel = null) : HttpMessageHandler
    {
        internal int Calls;
        internal string? Body;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            Body = await request.Content!.ReadAsStringAsync(token);
            using var body = JsonDocument.Parse(Body);
            // Compare all evidence bytes after transport decoding, not selected fields only.
            Assert.True(packet.Json == body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString());
            if (failure == "cancel-during") { cancel!.Cancel(); token.ThrowIfCancellationRequested(); }
            string review = JsonSerializer.Serialize(new {
                contractVersion = packet.ContractVersion, packetId = failure == "identity" ? "wrong" : packet.PacketId,
                daySummary = new { text = "Synthetic evidence summary.", sourceIds = new[] { failure == "citation" ? "not-supplied" : "calculated:day" } },
                executionObservations = Array.Empty<object>(), behaviorObservations = Array.Empty<object>(),
                improvementSuggestions = Array.Empty<object>(), uncertainties = Array.Empty<object>() });
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new {
                id = "chatcmpl-synthetic", model = GroqCoachingOptions.SupportedModel, @object = "chat.completion",
                choices = new[] { new { finish_reason = failure == "length" ? "length" : "stop",
                    message = new { role = "assistant", content = review } } },
                usage = new { prompt_tokens = 50, completion_tokens = 30, total_tokens = 80 }
            })) };
        }
    }
}
