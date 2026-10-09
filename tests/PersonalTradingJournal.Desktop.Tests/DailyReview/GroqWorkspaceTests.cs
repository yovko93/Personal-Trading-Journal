using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Desktop.Settings;
using PersonalTradingJournal.Desktop.Tests.Journals;
using PersonalTradingJournal.Desktop.Tests.Settings;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.DailyReview;
using PersonalTradingJournal.Infrastructure.DailyReview.Coaching;

namespace PersonalTradingJournal.Desktop.Tests.DailyReview;

public sealed class GroqWorkspaceTests
{
    [Theory]
    [InlineData(200, false)]
    [InlineData(200, true)]
    [InlineData(403, false)]
    [InlineData(429, false)]
    public async Task SettingsGroqCommandSavesOnlyValidatedOutputAndHistorySurvivesSwitchAndKeyRemoval(int status, bool invalidCitation)
    {
        await using var database = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        await database.Repository.CreateAsync(new(ReviewFixture.Day, null, "Synthetic journal, wait for confirmation."));
        using var secrets = new CoachingCredentialsTests.SecretFixture();
        var groq = new ProtectedCoachingCredentials(secrets.Paths.GroqCredentialsPath, () => null);
        var configuration = new CoachingConfiguration(secrets.Paths.CoachingProviderPath, secrets.Store, groq, false);
        using var settings = CoachingProviderSelectionTests.Settings(configuration);
        var evidence = database.Provider.GetRequiredService<IDailyReviewEvidenceReader>();
        var packet = CoachingEvidencePacketBuilder.Build(await evidence.GetAsync(new(ReviewFixture.Day))).Packet!;
        using var handler = new Handler((request, _) =>
        {
            Assert.Equal("api.groq.com", request.RequestUri!.Host);
            Assert.True(request.Headers.Authorization?.Parameter == "synthetic-groq-key");
            var json = status == 200 ? Envelope(packet, invalidCitation) : JsonSerializer.Serialize(new {
                error = new { code = status == 403 ? "model_permission_blocked" : "rate_limit_exceeded", type = "invalid_request_error" } });
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(json) });
        });
        using var client = new HttpClient(handler);
        var other = new DailyReviewGenerationTests.Provider();
        var router = new SelectedCoachingProvider(configuration, other, new GroqCoachingProvider(client, new(), groq.Resolve));
        var history = database.Provider.GetRequiredService<ICoachingAnalysisRepository>();
        var vm = new DailyReviewViewModel(evidence, history, database.Provider.GetRequiredService<ITradingAccountReader>(),
            new FakeDialogService(), new FixedTimeProvider(), new(new(router, new(), TimeProvider.System), history, TimeProvider.System), configuration);
        settings.SaveKey("synthetic-groq-key");
        vm.SelectedDate = ReviewFixture.Day.ToDateTime(TimeOnly.MinValue);
        await vm.ActivateAsync(); await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Contains("Groq Free tier", vm.ProviderDisclosure);
        Assert.Equal(0, handler.Calls); Assert.Equal(0, other.Calls);
        await vm.GenerateCommand.ExecuteAsync(null);
        Assert.Equal(1, handler.Calls); Assert.Equal(0, other.Calls);
        bool saved = status == 200 && !invalidCitation;
        await using var db = await database.ContextFactory.CreateDbContextAsync();
        Assert.Equal(saved ? 1 : 0, await db.CoachingAnalyses.CountAsync());
        Assert.Equal(saved, vm.Snapshot is not null);
        if (saved)
        {
            var row = Assert.Single(vm.Analyses);
            var record = (await history.GetAsync(row.Source.Id))!;
            Assert.Equal("Groq", record.Summary.Provider);
            Assert.Equal(GroqCoachingOptions.SupportedModel, record.Summary.Model);
            Assert.Equal(packet.Json, record.EvidenceJson);
            Assert.Equal(80, record.Metadata.Usage!.TotalTokens);
            settings.RemoveKeyCommand.Execute(null);
            settings.SelectedProvider = CoachingProviderKind.OpenAI;
            vm.Deactivate(); await vm.ActivateAsync();
            Assert.Contains("OpenAI", vm.ProviderDisclosure);
            await vm.OpenAnalysisCommand.ExecuteAsync(Assert.Single(vm.Analyses));
            Assert.NotNull(vm.Snapshot);
            Assert.Contains("Groq", vm.Snapshot.Metadata);
            Assert.Equal(1, handler.Calls); Assert.Equal(0, other.Calls);
        }
        vm.Deactivate();
    }

    [Fact]
    public async Task DoubleClickCapturesProviderBeforeEvidenceReadAndNeverFallsBack()
    {
        using var secrets = new CoachingCredentialsTests.SecretFixture();
        var configuration = new CoachingConfiguration(secrets.Paths.CoachingProviderPath, secrets.Store,
            new(secrets.Paths.GroqCredentialsPath, () => null), false);
        var groq = new DailyReviewGenerationTests.Provider(); var openAi = new DailyReviewGenerationTests.Provider();
        var f = new ReviewFixture(provider: new SelectedCoachingProvider(configuration, openAi, groq));
        f.Reader.Handler = (q, _) => Task.FromResult(ReviewFixture.Evidence(q));
        await f.Vm.ActivateAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Reader.Handler = async (q, token) => { started.SetResult(); await release.Task.WaitAsync(token); return ReviewFixture.Evidence(q); };
        var generation = f.Vm.GenerateCommand.ExecuteAsync(null);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(configuration.Select(CoachingProviderKind.OpenAI));
        await f.Vm.GenerateCommand.ExecuteAsync(null);
        release.SetResult(); await generation;
        Assert.Equal(1, groq.Calls); Assert.Equal(0, openAi.Calls); Assert.Equal(1, f.History.Writes);
        f.Vm.Deactivate();
    }

    [Fact]
    public async Task GroqCancellationDoesNotSaveOrTryOpenAi()
    {
        using var secrets = new CoachingCredentialsTests.SecretFixture();
        var configuration = new CoachingConfiguration(secrets.Paths.CoachingProviderPath, secrets.Store,
            new(secrets.Paths.GroqCredentialsPath, () => "synthetic-groq"), false);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new Handler(async (_, token) => {
            started.SetResult(); await Task.Delay(Timeout.Infinite, token); return new(HttpStatusCode.OK); });
        using var client = new HttpClient(handler);
        var openAi = new DailyReviewGenerationTests.Provider();
        var f = new ReviewFixture(provider: new SelectedCoachingProvider(configuration, openAi,
            new GroqCoachingProvider(client, new(), () => configuration.CredentialsFor(CoachingProviderKind.Groq).Resolve())));
        f.Reader.Handler = (q, _) => Task.FromResult(new DailyReviewEvidence(q, [], [ReviewFixture.Journal(null, "All accounts", q.Date)]));
        await f.Vm.ActivateAsync();
        var generation = f.Vm.GenerateCommand.ExecuteAsync(null);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        f.Vm.Deactivate();
        await generation.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, handler.Calls); Assert.Equal(0, openAi.Calls); Assert.Equal(0, f.History.Writes);
    }

    [Fact]
    public async Task OversizedWholeEvidenceIsRejectedWithoutTruncationOrOtherProvider()
    {
        var query = new DailyReviewQuery(ReviewFixture.Day);
        var journal = ReviewFixture.Journal(null, "All accounts", query.Date) with {
            Text = string.Join(" ", Enumerable.Range(0, 4000).Select(i => $"Synthetic evidence {i}.")) };
        var packet = CoachingEvidencePacketBuilder.Build(new(query, [], [journal])).Packet!;
        Assert.NotNull(packet);
        string before = packet.Json;
        using var handler = new Handler((_, _) => throw new InvalidOperationException("Must not send oversized evidence."));
        using var client = new HttpClient(handler);
        var result = await new DailyCoachingGenerationService(new GroqCoachingProvider(client, new(), () => "synthetic"),
            new(), TimeProvider.System).GenerateAsync(packet);
        Assert.Equal(CoachingGenerationStatus.InputTooLarge, result.Status);
        Assert.Equal(before, packet.Json); Assert.Equal(0, handler.Calls);
    }

    private static string Envelope(CoachingEvidencePacket packet, bool invalid) => JsonSerializer.Serialize(new {
        id = "chatcmpl-synthetic", model = GroqCoachingOptions.SupportedModel, @object = "chat.completion",
        choices = new[] { new { finish_reason = "stop", message = new { role = "assistant",
            content = DailyReviewGenerationTests.Provider.Json(packet, source: invalid ? "not-supplied" : "calculated:day") } } },
        usage = new { prompt_tokens = 50, completion_tokens = 30, total_tokens = 80 } });
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        internal int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { Calls++; return send(request, token); }
    }
}
