using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Views.DailyReview;

namespace PersonalTradingJournal.Desktop.Tests.DailyReview;

public sealed class DailyReviewViewTests
{
    [Fact]
    public async Task ProviderSelectionAndDisclosureWrapAcrossThemesWithoutSendingOrRevealingKeys()
    {
        if (Environment.GetEnvironmentVariable("PTJ_DAILY_REVIEW_TEST_HOST") != "1") { await Host.Value; return; }
        using var secret = new Settings.CoachingCredentialsTests.SecretFixture();
        var config = new PersonalTradingJournal.Desktop.Settings.CoachingConfiguration(secret.Paths.CoachingProviderPath, secret.Store,
            new(secret.Paths.GroqCredentialsPath, () => null), false);
        using var settings = Settings.CoachingProviderSelectionTests.Settings(config);
        var f = new ReviewFixture();
        var review = new PersonalTradingJournal.Desktop.ViewModels.DailyReview.DailyReviewViewModel(
            f.Reader, f.History, f.Accounts, f.Dialogs, TimeProvider.System, credentials: config);
        await review.ActivateAsync();
        await CalendarStaTest.RunAsync(() =>
        {
            var app = System.Windows.Application.Current ?? new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (var (theme, width, dpi) in new[] { ("Light", 960, 96), ("Dark", 960, 96), ("Light", 480, 240), ("Dark", 480, 240) })
            {
                var resources = CalendarViewLayoutTests.SharedThemeResources(theme); app.Resources = resources;
                var view = new PersonalTradingJournal.Desktop.Views.Settings.SettingsView { DataContext = settings };
                var root = new Border { Resources = resources, Child = view };
                root.SetResourceReference(Border.BackgroundProperty, "PtjBackgroundBrush");
                root.Measure(new Size(width, 760)); root.Arrange(new Rect(0, 0, width, 760)); root.UpdateLayout(); Flush();
                var selector = (ComboBox)view.FindName("ProviderSelector");
                Assert.Same(resources["PtjComboBoxStyle"], selector.Style);
                var key = (PasswordBox)view.FindName("ApiKeyEntry");
                foreach (var provider in settings.AvailableProviders)
                {
                    selector.SelectedItem = provider; Flush();
                    Assert.Equal(provider, config.SelectedProvider);
                    Assert.Contains(provider.ToString(), AutomationProperties.GetName(key));
                    key.Password = "synthetic-unsubmitted";
                    selector.SelectedItem = provider == PersonalTradingJournal.Application.DailyReview.Coaching.CoachingProviderKind.Groq
                        ? PersonalTradingJournal.Application.DailyReview.Coaching.CoachingProviderKind.OpenAI
                        : PersonalTradingJournal.Application.DailyReview.Coaching.CoachingProviderKind.Groq;
                    Flush(); Assert.Empty(key.Password);
                }
                selector.SelectedItem = PersonalTradingJournal.Application.DailyReview.Coaching.CoachingProviderKind.Groq; Flush();
                Assert.True(selector.Focusable && selector.ActualWidth > 100);
                Assert.Equal(0, ((ScrollViewer)view.FindName("SettingsScroll")).ScrollableWidth);
                Render(root, theme, width, dpi, "provider-settings"); root.Child = null;
                var workspace = new DailyReviewView { DataContext = review }; root.Child = workspace;
                root.Measure(new Size(width, 760)); root.Arrange(new Rect(0, 0, width, 760)); root.UpdateLayout(); Flush();
                Assert.Contains(Descendants(workspace).OfType<TextBlock>(), t => t.Text.Contains("Groq Free tier"));
                Assert.Equal(0, ((ScrollViewer)workspace.FindName("ReviewScroll")).ScrollableWidth);
                Render(root, theme, width, dpi, "groq-disclosure"); root.Child = null;
            }
            review.Deactivate();
        }, shutdownDispatcher: false);
        Assert.Empty(f.History.Items);
        Assert.False(File.Exists(secret.Paths.GroqCredentialsPath));
        Assert.False(File.Exists(secret.Paths.CoachingCredentialsPath));
    }
    private static readonly Lazy<Task> Host = new(() => IsolatedTestProcess.RunSuiteAsync(
        typeof(DailyReviewViewTests), "daily-review-layout", "PTJ_DAILY_REVIEW_TEST_HOST",
        TimeSpan.FromMinutes(2), caseHangTimeout: TimeSpan.FromSeconds(30)));

    [Fact]
    public async Task SanitizedRejectionDiagnosticsAreReadableWithoutLosingExistingWorkspace()
    {
        if (Environment.GetEnvironmentVariable("PTJ_DAILY_REVIEW_TEST_HOST") != "1") { await Host.Value; return; }
        var provider = new DailyReviewGenerationTests.Provider { Handler = (_, _) => Task.FromResult(
            new PersonalTradingJournal.Application.DailyReview.Coaching.CoachingProviderReply(
                PersonalTradingJournal.Application.DailyReview.Coaching.CoachingGenerationStatus.ModelUnavailable, null,
                new("OpenAI", "gpt-4.1-mini-2025-04-14", "12345678123412341234123456781234", RequestId: "req_synthetic", HttpStatus: 403,
                    Phase: PersonalTradingJournal.Application.DailyReview.Coaching.CoachingGenerationPhase.HttpResponse,
                    ErrorCode: "model_not_found", ErrorType: "invalid_request_error"))) };
        var f = DailyReviewGenerationTests.Ready(provider);
        await f.Vm.ActivateAsync(); await f.Vm.GenerateCommand.ExecuteAsync(null);
        await CalendarStaTest.RunAsync(() =>
        {
            var app = System.Windows.Application.Current ?? new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (var (theme, width, dpi) in new[] { ("Light", 960, 96), ("Dark", 960, 96), ("Light", 480, 240), ("Dark", 480, 240) })
            {
                var resources = CalendarViewLayoutTests.SharedThemeResources(theme); app.Resources = resources;
                var view = new DailyReviewView { DataContext = f.Vm };
                var root = new Border { Resources = resources, Child = view };
                root.SetResourceReference(Border.BackgroundProperty, "PtjBackgroundBrush");
                root.Measure(new Size(width, 760)); root.Arrange(new Rect(0, 0, width, 760)); root.UpdateLayout(); Flush();
                var diagnostic = Assert.Single(Descendants(view).OfType<TextBlock>(), t => t.Text.Contains("error.code: model_not_found"));
                Assert.Equal(TextWrapping.Wrap, diagnostic.TextWrapping);
                Assert.True(diagnostic.ActualWidth > 100 && diagnostic.ActualHeight > 0);
                Assert.InRange(diagnostic.TranslatePoint(new Point(diagnostic.ActualWidth, 0), view).X, 0, width);
                Assert.Equal(0, ((ScrollViewer)view.FindName("ReviewScroll")).ScrollableWidth);
                Render(root, theme, width, dpi, "rejection"); root.Child = null;
            }
            Assert.NotNull(f.Vm.Current); Assert.Empty(f.History.Items); f.Vm.Deactivate();
        }, shutdownDispatcher: false);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task AiSettingsMaskedEntryActionsAndMissingCredentialLinkWorkAcrossThemesAndDpi()
    {
        if (Environment.GetEnvironmentVariable("PTJ_DAILY_REVIEW_TEST_HOST") != "1") { await Host.Value; return; }
        using var secret = new Settings.CoachingCredentialsTests.SecretFixture();
        using var vm = Settings.CoachingCredentialsTests.Settings(secret.Store);
        var f = new ReviewFixture();
        var review = new PersonalTradingJournal.Desktop.ViewModels.DailyReview.DailyReviewViewModel(
            f.Reader, f.History, f.Accounts, f.Dialogs, TimeProvider.System, credentials: secret.Store);
        await review.ActivateAsync();
        int navigated = 0;
        review.OpenAiSettings = () => navigated++;
        await CalendarStaTest.RunAsync(() =>
        {
            var app = System.Windows.Application.Current ?? new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (var (theme, width, dpi) in new[] { ("Light", 960, 96), ("Dark", 960, 96), ("Light", 480, 240), ("Dark", 480, 240) })
            {
                var resources = CalendarViewLayoutTests.SharedThemeResources(theme); app.Resources = resources;
                var view = new PersonalTradingJournal.Desktop.Views.Settings.SettingsView { DataContext = vm };
                var root = new Border { Resources = resources, Child = view };
                root.SetResourceReference(Border.BackgroundProperty, "PtjBackgroundBrush");
                root.Measure(new Size(width, 760)); root.Arrange(new Rect(0, 0, width, 760)); root.UpdateLayout(); Flush();
                var entry = (PasswordBox)view.FindName("ApiKeyEntry");
                Assert.True(entry.Focusable && entry.ActualWidth > 100);
                Assert.Contains("masked", AutomationProperties.GetName(entry));
                Assert.Equal(string.Empty, entry.Password);
                entry.Password = "synthetic-layout-key";
                ((Button)view.FindName("SaveApiKey")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Flush(); root.UpdateLayout();
                Assert.Equal(string.Empty, entry.Password);
                Assert.Equal(PersonalTradingJournal.Application.DailyReview.Coaching.CoachingCredentialSource.Saved, vm.CredentialSource);
                Assert.DoesNotContain(Descendants(view).OfType<TextBlock>(), t => t.Text.Contains("synthetic-layout-key"));
                var buttons = Descendants(view).OfType<Button>().Where(b => b.Content is string).ToArray();
                Assert.Contains(buttons, b => Equals(b.Content, "Remove saved key") && b.IsEnabled);
                Assert.All(buttons, b => Assert.InRange(b.TranslatePoint(new Point(b.ActualWidth, 0), view).X, 0, width));
                Assert.Equal(0, ((ScrollViewer)view.FindName("SettingsScroll")).ScrollableWidth);
                Render(root, theme, width, dpi, "ai-settings");
                root.Child = null;
                vm.RemoveKeyCommand.Execute(null);
                var reviewView = new DailyReviewView { DataContext = review };
                root.Child = reviewView;
                root.Measure(new Size(width, 760)); root.Arrange(new Rect(0, 0, width, 760)); root.UpdateLayout(); Flush();
                var configure = Assert.Single(Descendants(reviewView).OfType<Button>(), b => Equals(b.Content, "Configure AI in Settings"));
                Assert.Equal(Visibility.Visible, configure.Visibility);
                Assert.True(configure.IsEnabled && configure.Focusable);
                configure.Command!.Execute(null);
                root.Child = null;
            }
            review.Deactivate();
        }, shutdownDispatcher: false);
        Assert.Equal(4, navigated);
        Assert.Empty(f.History.Items);
    }

    [Fact]
    public async Task CompiledWorkspaceSupportsBothThemesNarrowHighDpiAndReachableSavedEvidenceWithoutGeneration()
    {
        if (Environment.GetEnvironmentVariable("PTJ_DAILY_REVIEW_TEST_HOST") != "1") { await Host.Value; return; }
        var f = new ReviewFixture();
        var historicalId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        f.History.ScopeHandler = (q, _) => Task.FromResult(new PersonalTradingJournal.Application.DailyReview.Coaching.HistoricalCoachingAccountPage(
            [new(historicalId, "P 21 saved name")], 26, q.Page, q.PageSize));
        f.Reader.Handler = (q, _) => Task.FromResult(ReviewFixture.Evidence(q));
        f.History.Items.Add(ReviewFixture.Saved(ReviewFixture.Evidence(new(ReviewFixture.Day))));
        await f.Vm.ActivateAsync();
        await f.Vm.OpenAnalysisCommand.ExecuteAsync(f.Vm.Analyses.Single());
        await CalendarStaTest.RunAsync(() =>
        {
            var app = System.Windows.Application.Current ?? new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (var (theme, width, dpi) in new[] { ("Light", 960, 96), ("Dark", 960, 96), ("Light", 480, 240), ("Dark", 480, 240) })
            {
                using var phase = CalendarStaTest.Phase($"Daily Review {theme} {width} DIP / {dpi} DPI");
                var resources = CalendarViewLayoutTests.SharedThemeResources(theme);
                app.Resources = resources;
                var view = new DailyReviewView { DataContext = f.Vm };
                var root = new Border { Resources = resources, Child = view };
                root.SetResourceReference(Border.BackgroundProperty, "PtjBackgroundBrush");
                root.Measure(new Size(width, 760)); root.Arrange(new Rect(0, 0, width, 760)); root.UpdateLayout();
                Flush(); root.UpdateLayout();
                var scroll = (ScrollViewer)view.FindName("ReviewScroll");
                var date = (DatePicker)view.FindName("ReviewDate");
                var account = (ComboBox)view.FindName("ReviewAccount");
                Assert.Equal(f.Vm.SelectedDate, date.SelectedDate);
                Assert.Equal(f.Vm.SelectedAccount, account.SelectedItem);
                var historical = Assert.Single(account.Items.Cast<PersonalTradingJournal.Desktop.ViewModels.DailyReview.ReviewAccount>(), a => a.Id == historicalId);
                Assert.Contains("P 21 saved name", historical.Label);
                Assert.Contains(historicalId.ToString(), historical.Label);
                Assert.Contains("historical / unavailable", historical.Label);
                Assert.Equal(f.Vm.SelectedAccount.Label, account.ToolTip);
                Assert.Contains("New York", AutomationProperties.GetName(date));
                Assert.True(account.Focusable && date.Focusable);
                Assert.True(scroll.ScrollableHeight > 0);
                Assert.Equal(0, scroll.ScrollableWidth);
                var buttons = Descendants(view).OfType<Button>().Where(b => b.Visibility == Visibility.Visible && b.Content is string && b.Command is not null).ToArray();
                Assert.Contains(buttons, b => Equals(b.Content, "Open saved analysis") && b.IsEnabled);
                Assert.Contains(buttons, b => Equals(b.Content, "Delete analysis") && b.IsEnabled);
                Assert.Contains(buttons, b => Equals(b.Content, "Next historical Accounts") && b.IsEnabled);
                Assert.Contains(buttons, b => Equals(b.Content, "Generate AI Review") && !b.IsEnabled);
                Assert.Contains(buttons, b => Equals(b.Content, "Cancel generation") && !b.IsEnabled);
                Assert.Contains(Descendants(view).OfType<TextBlock>(), t => t.Text.Contains("OpenAI and may incur usage charges"));
                Assert.All(buttons, b =>
                {
                    Assert.True(b.Focusable);
                    double right = b.TranslatePoint(new Point(b.ActualWidth, 0), view).X;
                    Assert.InRange(right, 0, width + 1);
                });
                var text = Descendants(view).OfType<TextBlock>().Select(t => t.Text).ToArray();
                Assert.Contains("Saved snapshot · NOT current records", text);
                Assert.Contains(text, t => t.Contains("Net coverage 0/1"));
                Assert.Contains(text, t => t.Contains("All accounts · aggregate analysis"));
                var bodies = Descendants(view).OfType<TextBlock>().Where(t => ReferenceEquals(t.Style, view.Resources["ReviewBody"])).ToArray();
                Assert.True(bodies.Length > 20);
                Assert.All(bodies, t =>
                {
                    Assert.Equal(TextWrapping.Wrap, t.TextWrapping);
                    Assert.Equal(((SolidColorBrush)resources["PtjTextPrimaryBrush"]).Color, ((SolidColorBrush)t.Foreground).Color);
                    Assert.True(t.ActualWidth <= width);
                });
                Assert.All(Descendants(view).OfType<Expander>(), e =>
                    Assert.Equal(((SolidColorBrush)resources["PtjTextPrimaryBrush"]).Color, ((SolidColorBrush)e.Foreground).Color));
                Render(root, theme, width, dpi, "current");
                scroll.ScrollToVerticalOffset(scroll.VerticalOffset + ((ItemsControl)view.FindName("AnalysisHistory")).TranslatePoint(new Point(), scroll).Y);
                Flush(); root.UpdateLayout();
                Render(root, theme, width, dpi, "history");
                scroll.ScrollToVerticalOffset(scroll.VerticalOffset + ((ContentControl)view.FindName("SavedSnapshot")).TranslatePoint(new Point(), scroll).Y);
                Flush(); root.UpdateLayout();
                Render(root, theme, width, dpi, "snapshot");
                scroll.ScrollToEnd(); Flush(); root.UpdateLayout();
                Assert.InRange(scroll.VerticalOffset, scroll.ScrollableHeight - 1, scroll.ScrollableHeight + 1);
                Assert.Equal(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
                Assert.Single(Descendants(view).OfType<ScrollViewer>(), s => s.ScrollableHeight > 0);
                root.Child = null;
            }
        }, shutdownDispatcher: false);
    }

    [Fact]
    public async Task CompiledSelectorOpensHistoricalIdentityAndKeepsFullLabelReadableInBothThemes()
    {
        if (Environment.GetEnvironmentVariable("PTJ_DAILY_REVIEW_TEST_HOST") != "1") { await Host.Value; return; }
        var f = new ReviewFixture();
        f.Accounts.Items = [];
        f.History.ScopeHandler = (q, _) => Task.FromResult(new PersonalTradingJournal.Application.DailyReview.Coaching.HistoricalCoachingAccountPage(
            [new(ReviewFixture.AccountId, "P 21")], 1, q.Page, q.PageSize));
        f.History.Items.Add(ReviewFixture.Saved(new(new(ReviewFixture.Day, ReviewFixture.AccountId), [],
            [ReviewFixture.Journal(ReviewFixture.AccountId, "P 21", ReviewFixture.Day)])));
        await f.Vm.ActivateAsync();
        static Task OnUi(Action action) => CalendarStaTest.RunAsync(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            action();
        }, shutdownDispatcher: false);
        foreach (var (theme, width, dpi) in new[] { ("Light", 960, 96), ("Dark", 960, 96), ("Light", 480, 240), ("Dark", 480, 240) })
        {
            DailyReviewView view = null!;
            Border root = null!;
            await OnUi(() =>
            {
                var app = System.Windows.Application.Current ?? new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                var resources = CalendarViewLayoutTests.SharedThemeResources(theme);
                app.Resources = resources;
                view = new DailyReviewView { DataContext = f.Vm };
                root = new Border { Resources = resources, Child = view };
                root.SetResourceReference(Border.BackgroundProperty, "PtjBackgroundBrush");
                root.Measure(new Size(width, 760)); root.Arrange(new Rect(0, 0, width, 760)); root.UpdateLayout(); Flush();
                var account = (ComboBox)view.FindName("ReviewAccount");
                account.SetCurrentValue(ComboBox.SelectedItemProperty, f.Vm.Accounts.Single(a => a.Id == ReviewFixture.AccountId));
            });
            await f.Vm.LoadTask;
            Task open = Task.CompletedTask;
            await OnUi(() => open = f.Vm.OpenAnalysisCommand.ExecuteAsync(f.Vm.Analyses.Single()));
            await open;
            await OnUi(() =>
            {
                Flush(); root.UpdateLayout();
                Assert.Equal(ReviewFixture.AccountId, f.Vm.SelectedAccount.Id);
                Assert.Empty(f.Vm.Current!.Journals);
                Assert.Equal("P 21", Assert.Single(f.Vm.Snapshot!.Evidence.Journals).Source.AccountName);
                var account = (ComboBox)view.FindName("ReviewAccount");
                Assert.Contains(ReviewFixture.AccountId.ToString(), account.ToolTip.ToString());
                var notice = Assert.Single(Descendants(view).OfType<TextBlock>(), t => t.Text == f.Vm.AccountNotice);
                Assert.True(notice.ActualWidth <= width);
                Assert.Equal(TextWrapping.Wrap, notice.TextWrapping);
                Assert.Contains("historical / unavailable", notice.Text);
                Assert.Equal(0, ((ScrollViewer)view.FindName("ReviewScroll")).ScrollableWidth);
                Render(root, theme, width, dpi, "historical");
                root.Child = null;
            });
        }
        await OnUi(f.Vm.Deactivate);
    }

    private static void Flush() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

    [Fact]
    public async Task CompiledGenerateAndCancelBindingsAreExplicitAccessibleAndResponsiveAcrossThemes()
    {
        if (Environment.GetEnvironmentVariable("PTJ_DAILY_REVIEW_TEST_HOST") != "1") { await Host.Value; return; }
        static Task OnUi(Action action) => CalendarStaTest.RunAsync(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            action();
        }, shutdownDispatcher: false);
        foreach (var (theme, width, dpi) in new[] { ("Light", 960, 96), ("Dark", 960, 96), ("Light", 480, 240), ("Dark", 480, 240) })
        {
            var pending = new TaskCompletionSource<PersonalTradingJournal.Application.DailyReview.Coaching.CoachingProviderReply>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var provider = new DailyReviewGenerationTests.Provider { Handler = (_, _) => pending.Task };
            var f = DailyReviewGenerationTests.Ready(provider);
            await f.Vm.ActivateAsync();
            Border root = null!;
            DailyReviewView view = null!;
            Task generation = Task.CompletedTask;
            await OnUi(() =>
            {
                var app = System.Windows.Application.Current ?? new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                var resources = CalendarViewLayoutTests.SharedThemeResources(theme);
                app.Resources = resources;
                view = new DailyReviewView { DataContext = f.Vm };
                root = new Border { Resources = resources, Child = view };
                root.SetResourceReference(Border.BackgroundProperty, "PtjBackgroundBrush");
                root.Measure(new Size(width, 760)); root.Arrange(new Rect(0, 0, width, 760)); root.UpdateLayout(); Flush();
                var generate = (Button)view.FindName("GenerateReview");
                var cancel = (Button)view.FindName("CancelGeneration");
                Assert.True(generate.IsEnabled && generate.Focusable);
                Assert.False(cancel.IsEnabled);
                Assert.Contains("selected New York date", AutomationProperties.GetName(generate));
                Assert.Same(f.Vm.GenerateCommand, generate.Command);
                Assert.Equal(0, provider.Calls);
                generate.Command.Execute(null);
                generation = f.Vm.GenerateCommand.ExecutionTask!;
            });
            await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await OnUi(() =>
            {
                Flush(); root.UpdateLayout();
                var generate = (Button)view.FindName("GenerateReview");
                var cancel = (Button)view.FindName("CancelGeneration");
                Assert.False(generate.IsEnabled);
                Assert.True(cancel.IsEnabled && cancel.Focusable);
                Assert.True(Assert.Single(Descendants(view).OfType<ProgressBar>()).IsIndeterminate);
                Assert.Contains(Descendants(view).OfType<TextBlock>(), t => t.Text.Contains("OpenAI and may incur usage charges"));
                foreach (var button in new[] { generate, cancel })
                    Assert.InRange(button.TranslatePoint(new Point(button.ActualWidth, 0), view).X, 0, width);
                Assert.Equal(0, ((ScrollViewer)view.FindName("ReviewScroll")).ScrollableWidth);
                Render(root, theme, width, dpi, "generation");
                cancel.Command!.Execute(null);
            });
            await generation.WaitAsync(TimeSpan.FromSeconds(10));
            pending.SetResult(DailyReviewGenerationTests.Provider.Success(provider.Packet!));
            await OnUi(() =>
            {
                Flush(); root.UpdateLayout();
                Assert.Empty(f.History.Items);
                Assert.True(((Button)view.FindName("GenerateReview")).IsEnabled);
                Assert.False(((Button)view.FindName("CancelGeneration")).IsEnabled);
                f.Vm.Deactivate();
                root.Child = null;
            });
        }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    private static void Render(Border root, string theme, int width, int dpi, string section)
    {
        var bitmap = new RenderTargetBitmap(width * dpi / 96, 760 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(root);
        Assert.Equal(width * dpi / 96, bitmap.PixelWidth);
        if (Environment.GetEnvironmentVariable("PTJ_REVIEW_RENDER_DIRECTORY") is not { Length: > 0 } directory) return;
        Directory.CreateDirectory(directory);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(directory, $"review-{section}-{theme}-{width}-{dpi}.png"));
        encoder.Save(file);
    }
}
