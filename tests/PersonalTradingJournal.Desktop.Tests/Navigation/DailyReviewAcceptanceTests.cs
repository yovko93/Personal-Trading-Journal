using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.Settings;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.DailyReview;
using PersonalTradingJournal.Desktop.Tests.Journals;
using PersonalTradingJournal.Desktop.Tests.Settings;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.DailyReview;
using PersonalTradingJournal.Desktop.Views.DailyReview;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.DailyReview.Coaching;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;

namespace PersonalTradingJournal.Desktop.Tests.Navigation;

public sealed partial class MainWindowViewModelTests
{
    private static readonly Lazy<Task> ReviewAcceptanceHost = new(() => IsolatedTestProcess.RunSuiteAsync(
        typeof(MainWindowViewModelTests), "daily-review-acceptance", "PTJ_REVIEW_ACCEPTANCE_HOST",
        TimeSpan.FromMinutes(2), testCaseFilter:
        $"FullyQualifiedName~{typeof(MainWindowViewModelTests).FullName}.DailyReviewAcceptance",
        caseHangTimeout: TimeSpan.FromSeconds(30)));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DailyReviewAcceptanceCompiledShellKeepsCapturedEvidenceOrCancelsOnSourceNotification(bool notifyChange)
    {
        if (Environment.GetEnvironmentVariable("PTJ_REVIEW_ACCEPTANCE_HOST") != "1") { await ReviewAcceptanceHost.Value; return; }
        await using var database = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        var day = ReviewFixture.Day;
        var account = new TradingAccount("Synthetic USD", TradingAccountType.Personal, null, null, "USD", 0m, ReviewFixture.Now);
        var other = new TradingAccount("Synthetic EUR", TradingAccountType.Personal, null, null, "EUR", 0m, ReviewFixture.Now);
        var instrument = new Instrument("SYNTH", "Synthetic only", AssetClass.Futures, "TEST", "USD", .25m, .25m, ReviewFixture.Now);
        await database.Provider.GetRequiredService<ITradingAccountStore>().AddAsync(account);
        await database.Provider.GetRequiredService<ITradingAccountStore>().AddAsync(other);
        await database.Provider.GetRequiredService<IInstrumentStore>().AddAsync(instrument);
        var close = new DailyReviewQuery(day).FromUtc.AddHours(12);
        Trade MakeTrade(Guid scope, string currency, decimal? fees)
        {
            var id = Guid.NewGuid();
            return Trade.Rehydrate(id, scope, instrument.Id, new(1, currency), null,
                [new(id, 1, close.AddHours(-1), ExecutionSide.Buy, 1, 100, 1, 0, null, null, null),
                 new(id, 2, close, ExecutionSide.Sell, 1, 110, 1, fees, null, null, null)],
                ReviewFixture.Now, ReviewFixture.Now);
        }
        var trades = new[] { MakeTrade(account.Id, "USD", 1), MakeTrade(other.Id, "EUR", null) };
        await using (var db = await database.ContextFactory.CreateDbContextAsync())
        {
            db.Trades.AddRange(trades.Select(TradePersistenceMapper.ToRecord));
            db.TradeExecutions.AddRange(trades.SelectMany(t => t.Executions).Select(TradeExecutionPersistenceMapper.ToRecord));
            db.TradeBrowse.AddRange(trades.Select(TradeBrowsePersistenceMapper.ToRecord));
            await db.SaveChangesAsync();
        }
        var journal = (await database.Repository.CreateAsync(new(day, account.Id, "Synthetic original observation."))).Journal!.Entry;
        await database.Repository.CreateAsync(new(day, null, "Synthetic aggregate observation."));
        var reader = database.Provider.GetRequiredService<IDailyReviewEvidenceReader>();
        var history = database.Provider.GetRequiredService<ICoachingAnalysisRepository>();
        var packet = CoachingEvidencePacketBuilder.Build(await reader.GetAsync(new(day, account.Id))).Packet!;
        using var secrets = new CoachingCredentialsTests.SecretFixture();
        var groq = new ProtectedCoachingCredentials(secrets.Paths.GroqCredentialsPath, () => null);
        var configuration = new CoachingConfiguration(secrets.Paths.CoachingProviderPath, secrets.Store, groq, false);
        using var settings = CoachingProviderSelectionTests.Settings(configuration);
        settings.SaveKey("synthetic-acceptance-only");
        using var handler = new AcceptanceGroqHandler(packet);
        using var client = new HttpClient(handler);
        var unusedOpenAi = new DailyReviewGenerationTests.Provider();
        var provider = new SelectedCoachingProvider(configuration, unusedOpenAi, new GroqCoachingProvider(client, new(), groq.Resolve));
        var dialogs = new FakeDialogService();
        var vm = new DailyReviewViewModel(reader, history, database.Provider.GetRequiredService<ITradingAccountReader>(),
            dialogs, new FixedTimeProvider(), new(new(provider, new(), TimeProvider.System), history, TimeProvider.System), configuration);
        vm.SelectedDate = day.ToDateTime(TimeOnly.MinValue);
        ViewModelFixture fixture = null!;
        MainWindow window = null!;
        FrameworkElement root = null!;
        DailyReviewView view = null!;
        Task generation = Task.CompletedTask;
        static Task Ui(Action action) => CalendarStaTest.RunAsync(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            action();
        }, shutdownDispatcher: false);
        try
        {
            await Ui(() =>
            {
                var app = System.Windows.Application.Current ?? new() { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.Resources = CalendarViewLayoutTests.SharedThemeResources("Light");
                fixture = CreateFixture(dailyReview: vm);
                fixture.Main.NavigateCommand.Execute(NavigationDestination.DailyReview);
                window = new MainWindow(fixture.Main);
                root = (FrameworkElement)window.Content;
                Layout(1280, 96);
                var host = ReviewDescendants(root).OfType<ContentControl>().Single(c => ReferenceEquals(c.Content, vm));
                // Host the compiled view directly; do not replace its XAML namescope with a test template.
                view = new DailyReviewView { DataContext = vm };
                host.Content = view;
                Layout(1280, 96);
                Assert.Same(view, ReviewDescendants(root).OfType<DailyReviewView>().Single());
            });
            await vm.LoadTask;
            Assert.Equal(2, vm.Current!.Metrics.Count);
            Assert.Equal(2, vm.Current.Journals.Count);
            Assert.Equal(7m, vm.Current.Metrics.Single(m => m.Currency == "USD").Net.Metrics.Total);
            Assert.Null(vm.Current.Metrics.Single(m => m.Currency == "EUR").Net.Metrics.Total);
            Assert.Contains("fees missing", vm.Current.Metrics.Single(m => m.Currency == "EUR").Coverage);
            await Ui(() =>
            {
                RenderStates("aggregate");
                ((ComboBox)view.FindName("ReviewAccount")).SelectedItem = vm.Accounts.Single(a => a.Id == account.Id);
            });
            await vm.LoadTask;
            Assert.Single(vm.Current!.Metrics); Assert.Single(vm.Current.Journals);
            Assert.Equal(0, handler.Calls);
            await Ui(() =>
            {
                Layout(1280, 96);
                var generate = (Button)view.FindName("GenerateReview");
                Assert.True(generate.IsEnabled && generate.Focusable && generate.IsTabStop);
                Assert.Contains("selected New York date", AutomationProperties.GetName(generate));
                Assert.Contains("Groq", ((TextBlock)view.FindName("ProviderSummary")).Text);
                generate.Command!.Execute(null);
                generation = vm.GenerateCommand.ExecutionTask!;
            });
            await Task.WhenAny(handler.Started.Task, generation).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.True(handler.Started.Task.IsCompleted, "Request did not reach synthetic transport: " + vm.GenerationMessage);
            await Ui(() =>
            {
                Layout(1280, 96);
                Assert.False(((Button)view.FindName("GenerateReview")).IsEnabled);
                Assert.Equal(Visibility.Visible, ((Button)view.FindName("CancelGeneration")).Visibility);
                vm.GenerateCommand.Execute(null);
            });
            Assert.Equal(1, handler.Calls);
            var changed = await database.Repository.UpdateAsync(new(journal.Id, journal.Revision, "Synthetic later observation."));
            Assert.Equal(DailyJournalWriteStatus.Updated, changed.Status);
            if (notifyChange) await Ui(vm.OnDataCommitted);
            handler.Release.TrySetResult();
            await generation.WaitAsync(TimeSpan.FromSeconds(15));
            await vm.LoadTask;
            await using (var db = await database.ContextFactory.CreateDbContextAsync())
                Assert.Equal(notifyChange ? 0 : 1, await db.CoachingAnalyses.CountAsync());
            if (notifyChange)
            {
                Assert.Null(vm.Snapshot);
                Assert.Contains("cancelled", vm.GenerationMessage);
            }
            else
            {
                var row = Assert.Single(vm.Analyses);
                var saved = (await history.GetAsync(row.Source.Id))!;
                Assert.True(packet.Json == saved.EvidenceJson, "Saved evidence must equal the complete supplied packet.");
                Assert.Equal("Groq", saved.Summary.Provider);
                Assert.Equal(80, saved.Metadata.Usage!.TotalTokens);
                Assert.Equal(journal.Revision, Assert.Single(vm.Snapshot!.Evidence.Journals).Source.Revision);
                await Ui(() => RenderStates("saved"));
                Task refresh = Task.CompletedTask;
                await Ui(() => refresh = vm.RefreshCommand.ExecuteAsync(null)); await refresh;
                Assert.Equal(journal.Revision + 1, Assert.Single(vm.Current!.Journals).Source.Revision);
                Task open = Task.CompletedTask;
                await Ui(() => open = vm.OpenAnalysisCommand.ExecuteAsync(vm.Analyses.Single())); await open;
                Assert.Equal(journal.Revision, Assert.Single(vm.Snapshot!.Evidence.Journals).Source.Revision);
                Task deletion = Task.CompletedTask;
                await Ui(() => deletion = vm.DeleteAnalysisCommand.ExecuteAsync(vm.Analyses.Single())); await deletion;
                Assert.Single(vm.Analyses); // Declining confirmation writes nothing.
                dialogs.ConfirmationResult = true;
                await Ui(() => deletion = vm.DeleteAnalysisCommand.ExecuteAsync(vm.Analyses.Single())); await deletion;
                Assert.Empty(vm.Analyses); Assert.Null(vm.Snapshot);
                Assert.Equal(journal.Revision + 1, (await database.Repository.GetAsync(day, account.Id))!.Entry.Revision);
            }
            Assert.Equal(1, handler.Calls); Assert.Equal(0, unusedOpenAi.Calls);
            await using var final = await database.ContextFactory.CreateDbContextAsync();
            Assert.Equal(2, await final.Trades.CountAsync());
            Assert.Equal(2, await final.DailyJournals.CountAsync());
        }
        finally
        {
            handler.Release.TrySetResult();
            await Ui(() => { vm.Deactivate(); if (window is not null) { window.Content = null; window.Close(); } fixture?.Main.Dispose(); });
            await generation; await vm.LoadTask;
        }
        void Layout(int width, int dpi)
        {
            VisualTreeHelper.SetRootDpi(root, new(dpi / 96d, dpi / 96d));
            root.Measure(new(width, 800)); root.Arrange(new(0, 0, width, 800)); root.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        }
        void RenderStates(string state)
        {
            foreach (var (theme, width, dpi) in new[] { ("Light", 1280, 96), ("Dark", 1280, 96), ("Light", 1000, 240), ("Dark", 1000, 240) })
            {
                System.Windows.Application.Current.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
                Layout(width, dpi);
                var content = (Grid)window.FindName("PageContentArea");
                var title = (TextBlock)window.FindName("PageTitleText");
                Assert.Equal("Daily Review", title.Text);
                Assert.InRange(Math.Abs(title.TranslatePoint(new(title.ActualWidth / 2, 0), content).X - content.ActualWidth / 2), 0, .6);
                Assert.Equal(AutomationHeadingLevel.Level1, AutomationProperties.GetHeadingLevel(title));
                // The sidebar navigation label is not a duplicate heading inside the page.
                Assert.Single(ReviewDescendants(content).OfType<TextBlock>(), t => t.Text == "Daily Review");
                var scroll = (ScrollViewer)view.FindName("ReviewScroll");
                scroll.ScrollToHome(); Layout(width, dpi);
                Assert.Equal(0, scroll.ScrollableWidth);
                var generate = (Button)view.FindName("GenerateReview");
                Assert.InRange(generate.TranslatePoint(new(generate.ActualWidth, 0), view).X, 0, view.ActualWidth);
                Render("top");
                scroll.ScrollToEnd(); Layout(width, dpi);
                Assert.InRange(scroll.VerticalOffset, scroll.ScrollableHeight - 1, scroll.ScrollableHeight + 1);
                Render("bottom");
                void Render(string position)
                {
                    if (Environment.GetEnvironmentVariable("PTJ_REVIEW_RENDER_DIRECTORY") is not { Length: > 0 } path) return;
                    Directory.CreateDirectory(path);
                    var bitmap = new RenderTargetBitmap(width * dpi / 96, 800 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
                    bitmap.Render(root);
                    var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
                    using var file = File.Create(Path.Combine(path, $"shell-{state}-{position}-{theme}-{width}-{dpi}.png")); png.Save(file);
                }
            }
        }
    }

    private static IEnumerable<DependencyObject> ReviewDescendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        { var child = VisualTreeHelper.GetChild(root, i); yield return child; foreach (var nested in ReviewDescendants(child)) yield return nested; }
    }

    private sealed class AcceptanceGroqHandler(CoachingEvidencePacket packet) : HttpMessageHandler
    {
        internal int Calls;
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Interlocked.Increment(ref Calls);
            Assert.Equal("api.groq.com", request.RequestUri!.Host);
            Assert.True(request.Headers.Authorization?.Parameter == "synthetic-acceptance-only");
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            Assert.True(packet.Json == body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString());
            Started.TrySetResult(); await Release.Task.WaitAsync(token);
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new {
                id = "chatcmpl-synthetic", model = GroqCoachingOptions.SupportedModel, @object = "chat.completion",
                choices = new[] { new { finish_reason = "stop", message = new { role = "assistant",
                    content = DailyReviewGenerationTests.Provider.Json(packet, source: "trade:" + packet.Content.RecordedTradeFacts[0].TradeId.ToString("N")) } } },
                usage = new { prompt_tokens = 50, completion_tokens = 30, total_tokens = 80 } })) };
        }
    }
}
