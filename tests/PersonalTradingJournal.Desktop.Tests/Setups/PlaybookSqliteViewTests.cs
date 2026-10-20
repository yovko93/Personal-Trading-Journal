using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Media;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Storage;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.Journals;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Setups;
using PersonalTradingJournal.Desktop.Views.Setups;
using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Desktop.Screenshots;

namespace PersonalTradingJournal.Desktop.Tests.Setups;

public sealed class PlaybookSqliteViewTests
{
    private const string Host = "PTJ_PLAYBOOK_SQLITE_VIEW_HOST";

    [Fact]
    public async Task CompiledSaveCreatesAndRevisesWithRealSqliteAndSurvivesReactivation()
    {
        if (Environment.GetEnvironmentVariable(Host) != "1")
        {
            await using var database = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
            await database.Provider.GetRequiredService<ITradingSetupStore>().AddAsync(
                new TradingSetup("Synthetic NQ", "SQLite-backed UI reproduction", DateTimeOffset.UtcNow.AddDays(-1)));
            await using var context = await database.ContextFactory.CreateDbContextAsync();
            string root = Path.GetDirectoryName(Path.GetDirectoryName(context.Database.GetDbConnection().DataSource))!;
            foreach (string phase in new[] { "write", "restart-read" })
            {
                string results = IsolatedTestProcess.CreateResultsDirectory("playbook-sqlite-" + phase);
                Directory.CreateDirectory(results);
                var start = new ProcessStartInfo("dotnet");
                start.ArgumentList.Add("vstest"); start.ArgumentList.Add(typeof(PlaybookSqliteViewTests).Assembly.Location);
                start.ArgumentList.Add("/TestCaseFilter:FullyQualifiedName=" + typeof(PlaybookSqliteViewTests).FullName + "." + nameof(CompiledSaveCreatesAndRevisesWithRealSqliteAndSurvivesReactivation));
                start.ArgumentList.Add("/Logger:trx;LogFileName=playbook-sqlite.trx");
                start.ArgumentList.Add("/ResultsDirectory:" + results);
                start.ArgumentList.Add("/Diag:" + Path.Combine(results, "vstest.log"));
                start.ArgumentList.Add("/Blame:CollectDump;DumpType=Mini");
                start.Environment[Host] = "1"; start.Environment[Host + "_ROOT"] = root; start.Environment[Host + "_PHASE"] = phase;
                var result = await IsolatedTestProcess.RunAsync(start, "playbook-sqlite-" + phase, TimeSpan.FromSeconds(90), resultsDirectory: results);
                Assert.True(result.ExitCode == 0, result.StandardOutput + result.StandardError);
            }
            return;
        }
        await using var provider = new ServiceCollection().AddPersistence(new LocalApplicationPaths(
            Environment.GetEnvironmentVariable(Host + "_ROOT") ?? throw new InvalidOperationException("Synthetic root required"))).BuildServiceProvider();
        var setup = Assert.Single(await provider.GetRequiredService<ITradingSetupReader>().GetAllAsync());
        Thread? nativeOwner = null;
        Dispatcher? nativeDispatcher = null;
        await CalendarStaTest.RunAsync(() =>
        {
            nativeOwner = Thread.CurrentThread;
            nativeDispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown,
                Resources = CalendarViewLayoutTests.SharedThemeResources("Light") };
            var images = new TrackedImages(new PlaybookExampleImages(provider.GetRequiredService<IPlaybookImageStorage>(),
                provider.GetRequiredService<ITradeScreenshotContentReader>()));
            var vm = Create(provider, images: images);
            var view = new PlaybookView { DataContext = vm };
            var window = new Window { Content = view, Width = 1000, Height = 750 };
            window.Show();
            try
            {
                Await(vm.ActivateAsync());
                var selector = (ComboBox)view.FindName("SetupSelector");
                selector.SetCurrentValue(ComboBox.SelectedItemProperty, vm.Setups.Single());
                Await(vm.LoadTask);
                if (Environment.GetEnvironmentVariable(Host + "_PHASE") == "restart-read")
                {
                    Assert.Equal(2, vm.DisplayedRevision?.Revision);
                    Assert.Equal("Synthetic Market context, revision 2", vm.DisplayedRevision?.Guidance.MarketContext);
                    Assert.Single(vm.DisplayedRevision!.Guidance.Examples);
                    Assert.Empty(vm.Revisions);
                    Assert.Equal("Current revision 2", ((TextBlock)view.FindName("Version")).Text);
                    return;
                }
                VerifyCompletedTaskReuseAndDeferredCompletion();
                for (int revision = 1; revision <= 2; revision++)
                {
                    vm.EditCommand.Execute(null);
                    Assert.True(vm.IsEditing);
                    window.UpdateLayout();
                    var editors = Descendants((ItemsControl)view.FindName("EditorFields")).OfType<TextBox>().ToArray();
                    Assert.Equal(5, editors.Length);
                    for (int index = 0; index < editors.Length; index++)
                        editors[index].SetCurrentValue(TextBox.TextProperty, $"Synthetic {vm.Fields[index].Label}, revision {revision}");
                    if (revision == 1)
                    {
                        byte[] bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
                        vm.EditorExamples.Add(new("synthetic.png", bytes, PlaybookExampleImages.Decode(bytes, true))
                            { Kind = PlaybookExampleKind.Valid, Annotation = "Synthetic condition and lesson" });
                    }
                    var saveButton = (Button)view.FindName("SavePlaybook");
                    Assert.True(saveButton.IsEnabled && saveButton.IsVisible);
                    if (revision == 1)
                    {
                        editors[1].SetCurrentValue(TextBox.TextProperty, "");
                        saveButton.Focus();
                        InvokeOnce(saveButton);
                        window.UpdateLayout();
                        Assert.True(vm.Fields[1].IsInvalid); Assert.True(vm.IsEditing);
                        Assert.Same(editors[1], System.Windows.Input.FocusManager.GetFocusedElement(window));
                        Assert.Single(vm.EditorExamples);
                        Assert.Null(vm.DisplayedRevision);
                        var scroll = (ScrollViewer)view.FindName("PageScroll");
                        double y = editors[1].TransformToAncestor(scroll).Transform(new Point()).Y;
                        Assert.InRange(y, 0, scroll.ViewportHeight - editors[1].ActualHeight + 1);
                        editors[1].SetCurrentValue(TextBox.TextProperty, "Synthetic Entry conditions, revision 1");
                        Assert.False(vm.Fields[1].IsInvalid);
                        var chart = vm.EditorExamples.Single(); chart.Kind = PlaybookExampleKind.Unspecified; chart.Annotation = "";
                        InvokeOnce(saveButton); window.UpdateLayout();
                        var kind = Descendants((ItemsControl)view.FindName("ExampleRows")).OfType<ComboBox>().Single();
                        var notes = Descendants((ItemsControl)view.FindName("ExampleRows")).OfType<TextBox>().Single();
                        Assert.Same(kind, System.Windows.Input.FocusManager.GetFocusedElement(window));
                        Assert.InRange(kind.TransformToAncestor(scroll).Transform(new Point()).Y, 0, scroll.ViewportHeight - kind.ActualHeight + 1);
                        Assert.True(chart.HasKindError && chart.HasAnnotationError); Assert.Null(vm.DisplayedRevision);
                        kind.SelectedItem = PlaybookExampleKind.Valid;
                        InvokeOnce(saveButton); window.UpdateLayout();
                        Assert.Same(notes, System.Windows.Input.FocusManager.GetFocusedElement(window));
                        Assert.InRange(notes.TransformToAncestor(scroll).Transform(new Point()).Y, 0, scroll.ViewportHeight - notes.ActualHeight + 1);
                        notes.SetCurrentValue(TextBox.TextProperty, "Synthetic condition and lesson");
                        Assert.False(vm.HasValidationErrors); Assert.Single(vm.EditorExamples);
                    }
                    var previousTask = vm.SaveCommand.ExecutionTask;
                    WpfStepDiagnostics.Write($"Playbook before Invoke: revision={revision}; task={previousTask?.Status}; running={vm.SaveCommand.IsRunning}; canExecute={vm.SaveCommand.CanExecute(null)}; enabled={saveButton.IsEnabled}; active={window.IsActive}; focus={System.Windows.Input.Keyboard.FocusedElement?.GetType().Name}; loading={vm.IsLoading}; writing={vm.IsWriting}; validation={vm.ValidationAttempt}");
                    using var invocationTiming = CalendarStaTest.Timing("Playbook Invoke through idle and Save completion");
                    InvokeOnce(saveButton);
                    WpfStepDiagnostics.Write($"Playbook after Invoke: revision={revision}; sameTask={ReferenceEquals(previousTask, vm.SaveCommand.ExecutionTask)}; task={vm.SaveCommand.ExecutionTask?.Status}; running={vm.SaveCommand.IsRunning}; canExecute={vm.SaveCommand.CanExecute(null)}; enabled={saveButton.IsEnabled}; active={window.IsActive}; focus={System.Windows.Input.Keyboard.FocusedElement?.GetType().Name}; loading={vm.IsLoading}; writing={vm.IsWriting}; validation={vm.ValidationAttempt}; editing={vm.IsEditing}; displayedRevision={vm.DisplayedRevision?.Revision}");
                    Assert.NotNull(vm.SaveCommand.ExecutionTask);
                    // Task.CompletedTask may be reused when every await completes synchronously.
                    // The click, exact next SQLite revision and independent restart read are the oracle.
                    Await(vm.SaveCommand.ExecutionTask);
                    window.UpdateLayout();
                    Assert.Equal("Playbook saved and verified.", ((TextBlock)view.FindName("Status")).Text);
                    Assert.False(vm.IsEditing, vm.Message);
                    Assert.Equal(setup.Id, vm.SelectedSetup?.Id);
                    Assert.Equal(revision, vm.DisplayedRevision?.Revision);
                    Assert.Equal($"Current revision {revision}", ((TextBlock)view.FindName("Version")).Text);
                    Assert.Empty(vm.Revisions);
                    using (var persisted = provider.GetRequiredService<IDbContextFactory<JournalDbContext>>().CreateDbContext())
                    { Assert.Equal(1, persisted.CurrentPlaybooks.Count()); Assert.Empty(persisted.PlaybookRevisions); }
                    Assert.Equal("Edit guidance", vm.EditLabel);
                    Assert.Single(vm.DisplayedRevision!.Guidance.Examples);
                    Assert.True(vm.TryLeave()); vm.Deactivate(); Await(vm.ActivateAsync());
                    Assert.Equal(revision, vm.DisplayedRevision?.Revision);
                }
            }
            finally
            {
                // Cancellation alone is not completion of image/file work. Keep its UI
                // continuation owner alive until every started operation has settled.
                vm.Deactivate();
                try
                {
                    Await(images.DrainAsync());
                    Await(Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Task);
                }
                finally { view.DataContext = null; window.Close(); }
                Assert.True(images.IsIdle);
                Assert.True(vm.LoadTask.IsCompleted);
                Assert.False(vm.SaveCommand.IsRunning);
                Assert.Empty(app.Windows.Cast<Window>());
                WpfStepDiagnostics.Write("Playbook cleanup: windows=0; imageWork=0; loadComplete=True; saveRunning=False; native STA retained until child exit");
            }
        }, shutdownDispatcher: false);
        // Match the app's process-owned UI lifetime. Returning a short-lived text-input
        // STA can invoke native callbacks after CoreCLR thread teardown (CI #95).
        Assert.True(nativeOwner!.IsAlive);
        Assert.False(nativeDispatcher!.HasShutdownStarted);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RealSqliteWriteRejectionPreservesEveryFieldAndExampleThenCanSave(bool existing)
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        var setup = new TradingSetup("Synthetic failure", null, DateTimeOffset.UtcNow.AddDays(-1));
        await db.Provider.GetRequiredService<ITradingSetupStore>().AddAsync(setup);
        var vm = Create(db.Provider);
        await vm.ActivateAsync(); vm.TrySelectSetup(vm.Setups.Single()); await vm.LoadTask;
        if (existing)
        {
            vm.EditCommand.Execute(null); foreach (var field in vm.Fields) field.Text = "Original " + field.Label;
            await vm.SaveCommand.ExecuteAsync(null); Assert.Equal(1, vm.DisplayedRevision?.Revision);
        }
        vm.EditCommand.Execute(null);
        foreach (var field in vm.Fields) field.Text = "Keep entered " + field.Label;
        vm.EditorExamples.Add(new(new PlaybookExample(Guid.NewGuid(), "Keep example notes")));
        var example = vm.EditorExamples.Single();
        await using var context = await db.ContextFactory.CreateDbContextAsync();
        await context.Database.ExecuteSqlRawAsync("CREATE TRIGGER synthetic_reject BEFORE INSERT ON CurrentPlaybookExamples BEGIN SELECT RAISE(ABORT, 'Synthetic write rejection'); END;");
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.True(vm.IsEditing); Assert.Contains("could not be saved", vm.Message);
        Assert.All(vm.Fields, field => Assert.Equal("Keep entered " + field.Label, field.Text));
        Assert.Same(example, Assert.Single(vm.EditorExamples));
        Assert.Equal(existing ? 1 : 0, await context.CurrentPlaybooks.CountAsync()); Assert.Empty(await context.PlaybookRevisions.ToArrayAsync());
        await context.Database.ExecuteSqlRawAsync("DROP TRIGGER synthetic_reject;");
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.False(vm.IsEditing, vm.Message); Assert.Equal(existing ? 2 : 1, vm.DisplayedRevision?.Revision);
        Assert.Equal("Keep example notes", Assert.Single(vm.DisplayedRevision!.Guidance.Examples).Annotation);

        // A second real author wins; a stale screen must keep its unsaved fields/examples.
        vm.EditCommand.Execute(null); vm.Fields[0].Text = "Stale editor retains this";
        var other = Create(db.Provider); await other.ActivateAsync(); other.TrySelectSetup(other.Setups.Single()); await other.LoadTask;
        other.EditCommand.Execute(null); other.Fields[0].Text = "Other author committed"; await other.SaveCommand.ExecuteAsync(null);
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.True(vm.IsEditing); Assert.Contains("changed", vm.Message);
        Assert.Equal("Stale editor retains this", vm.Fields[0].Text); Assert.Single(vm.EditorExamples);
        Assert.Equal(existing ? 3 : 2, (await context.CurrentPlaybooks.AsNoTracking().SingleAsync()).Version);
        Assert.Empty(await context.PlaybookRevisions.ToArrayAsync());
        vm.Deactivate(); other.Deactivate();
    }

    private static PlaybookViewModel Create(IServiceProvider provider, IPlaybookExampleImages? images = null)
    {
        var save = new SaveTradingSetupPlaybookUseCase(provider.GetRequiredService<ITradingSetupStore>(),
            provider.GetRequiredService<ITradingSetupPlaybookStore>(), TimeProvider.System);
        return new(provider.GetRequiredService<ITradingSetupReader>(), provider.GetRequiredService<ITradingSetupPlaybookStore>(),
            provider.GetRequiredService<ITradingSetupPlaybookHistoryReader>(), save,
            new TradingSetupLifecycleUseCase(provider.GetRequiredService<ITradingSetupStore>(), TimeProvider.System), new FakeDialogService(),
            exampleImages: images,
            saveExamples: new SavePlaybookExamplesUseCase(save, provider.GetRequiredService<IPlaybookImageStorage>()));
    }

    private sealed class TrackedImages(IPlaybookExampleImages inner) : IPlaybookExampleImages
    {
        private readonly List<Task> _work = [];
        internal bool IsIdle => _work.All(task => task.IsCompleted);
        public Task<PlaybookExampleItem?> PickAsync(CancellationToken token) => Track(inner.PickAsync(token));
        public Task<ImageSource> ThumbnailAsync(PlaybookExampleItem example, CancellationToken token) => Track(inner.ThumbnailAsync(example, token));
        public Task ShowAsync(PlaybookExampleItem example, CancellationToken token)
        { Task task = inner.ShowAsync(example, token); _work.Add(task); return task; }
        private Task<T> Track<T>(Task<T> task) { _work.Add(task); return task; }
        internal async Task DrainAsync()
        {
            foreach (Task task in _work)
            {
                try { await task; }
                catch (OperationCanceledException) when (task.IsCanceled) { }
            }
        }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void InvokeOnce(Button button)
    {
        var clicked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int count = 0;
        long started = Stopwatch.GetTimestamp();
        RoutedEventHandler handler = (_, _) => { count++; clicked.TrySetResult(); };
        button.Click += handler;
        try
        {
            using var phase = CalendarStaTest.Phase("Playbook automation click dispatch");
            Assert.True(button.IsEnabled);
            Assert.True(button.Command?.CanExecute(button.CommandParameter));
            ((IInvokeProvider)new ButtonAutomationPeer(button)).Invoke();
            // Event coordination, not an idle marker standing in for invocation. The existing
            // 30-second STA / 90-second child supervisor still bounds a missing Click.
            Await(clicked.Task);
            Await(Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Task);
            Assert.Equal(1, count);
        }
        finally
        {
            button.Click -= handler;
            WpfStepDiagnostics.Write($"Playbook automation: clicks={count}; elapsedMs={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F3}; enabled={button.IsEnabled}; dispatcherShutdown={button.Dispatcher.HasShutdownStarted}");
        }
    }

    private static void VerifyCompletedTaskReuseAndDeferredCompletion()
    {
        int executions = 0;
        var command = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(() => { executions++; return Task.CompletedTask; });
        var button = new Button { Command = command };
        InvokeOnce(button);
        Task? first = command.ExecutionTask;
        InvokeOnce(button);
        Assert.Equal(2, executions);
        Assert.Same(first, command.ExecutionTask); // The old NotSame oracle rejects valid invocations.
        WpfStepDiagnostics.Write($"Playbook task reuse regression: executions={executions}; sameTask={ReferenceEquals(first, command.ExecutionTask)}");

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        command = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(() =>
        {
            executions++;
            Dispatcher.CurrentDispatcher.BeginInvoke(() => completion.SetResult(), DispatcherPriority.Background);
            return completion.Task;
        });
        button.Command = command;
        InvokeOnce(button);
        Await(command.ExecutionTask!);
        Assert.Equal(3, executions);
        Assert.True(completion.Task.IsCompletedSuccessfully);
    }

    // Keep real asynchronous SQLite continuations on the UI dispatcher without blocking it.
    private static void Await(Task task)
    {
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            var dispatcher = Dispatcher.CurrentDispatcher;
            _ = task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
            Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }
}
