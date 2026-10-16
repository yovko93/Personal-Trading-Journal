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

public sealed partial class DailyReviewViewTests
{
    [Fact]
    public async Task SimplifiedEvidenceKeepsSummariesAndDirectSavedCitationActionsWithoutTechnicalControls()
    {
        if (Environment.GetEnvironmentVariable("PTJ_DAILY_REVIEW_TEST_HOST") != "1") { await Host.Value; return; }
        var f = new ReviewFixture();
        var evidence = ReviewFixture.Evidence(new(ReviewFixture.Day));
        f.Reader.Handler = (q, _) => Task.FromResult(evidence);
        var saved = ReviewFixture.Saved(evidence, citeAll: true);
        f.History.Items.Add(saved);
        await f.Vm.ActivateAsync();
        await f.Vm.OpenAnalysisCommand.ExecuteAsync(f.Vm.Analyses.Single());
        var empty = new ReviewFixture();
        await empty.Vm.ActivateAsync();
        Guid? opened = null;
        PersonalTradingJournal.Application.DailyReview.DailyReviewJournalEvidence? journal = null;
        f.Vm.OpenTradeAsync = id => { opened = id; return Task.CompletedTask; };
        f.Vm.OpenJournalAsync = value => { journal = value; return Task.CompletedTask; };
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
                var scroll = (ScrollViewer)view.FindName("ReviewScroll");
                Assert.Single(Descendants(view).OfType<Expander>(), e => Equals(e.Header, "Privacy and local draft storage"));
                Assert.DoesNotContain(Descendants(view).OfType<TextBox>(), t => t.IsReadOnly && t.IsVisible && t.Text.Length > 0);
                Assert.DoesNotContain(Descendants(view).OfType<ItemsControl>(),
                    i => i.GetBindingExpression(ItemsControl.ItemsSourceProperty)?.ParentBinding.Path?.Path == "Trades");
                var text = Descendants(view).OfType<TextBlock>().Select(t => t.Text).ToArray();
                Assert.DoesNotContain(text, t => t.Contains("Source details") || t.Contains("Contributing Trades") ||
                    t.Contains("ClosedOnDate") || t.Contains("UnknownNetPnL") || t.Contains("calculated:day") ||
                    t.Contains(saved.PacketId) || t.Contains(evidence.Trades[0].TradeId.ToString()));
                Assert.Contains(text, t => t.Contains("Net: Unavailable (USD)"));
                Assert.Contains(text, t => t.Contains("commissions missing"));
                Assert.Contains(text, t => t.Contains("Gross:") && t.Contains("EUR"));
                Render(root, theme, width, dpi, "simple-summary");
                var citations = Descendants(view).OfType<Button>().Where(b =>
                    b.DataContext is PersonalTradingJournal.Desktop.ViewModels.DailyReview.ReviewCitation &&
                    b.Visibility == Visibility.Visible).ToArray();
                Assert.NotEmpty(citations);
                foreach (var button in citations)
                {
                    var citation = (PersonalTradingJournal.Desktop.ViewModels.DailyReview.ReviewCitation)button.DataContext;
                    Assert.True(button.IsEnabled && button.Focusable && button.IsTabStop);
                    Assert.Contains("citation", AutomationProperties.GetName(button));
                    Assert.InRange(button.TranslatePoint(new Point(button.ActualWidth, 0), view).X, 0, width);
                    button.Command!.Execute(button.CommandParameter);
                    if (citation.Trade is { } trade) Assert.Equal(trade.Id, opened);
                    if (citation.Journal is { } entry) Assert.Equal(entry.Source, journal);
                }
                var snapshot = (ContentControl)view.FindName("SavedSnapshot");
                scroll.ScrollToVerticalOffset(snapshot.TranslatePoint(new Point(), scroll).Y);
                Flush(); root.UpdateLayout(); Render(root, theme, width, dpi, "simple-citations");
                scroll.ScrollToVerticalOffset(scroll.VerticalOffset + citations[0].TranslatePoint(new Point(), scroll).Y - 100);
                Flush(); root.UpdateLayout(); Render(root, theme, width, dpi, "citation-links");
                scroll.ScrollToEnd(); Flush(); root.UpdateLayout();
                Assert.InRange(scroll.VerticalOffset, scroll.ScrollableHeight - 1, scroll.ScrollableHeight + 1);
                Assert.Equal(0, scroll.ScrollableWidth);
                Assert.Single(Descendants(view).OfType<ScrollViewer>(), s => s.ScrollableHeight > 0);
                view.DataContext = empty.Vm;
                root.Measure(new Size(width, 760)); root.Arrange(new Rect(0, 0, width, 760)); root.UpdateLayout(); Flush();
                Assert.StartsWith("No closed trades", ((TextBlock)view.FindName("ScopeCounts")).Text);
                Assert.Null(((ContentControl)view.FindName("SavedSnapshot")).Content);
                Assert.Contains(Descendants(view).OfType<TextBlock>(), t => t.Text.Contains("No Trade or Journal evidence"));
                Assert.Contains(Descendants(view).OfType<TextBlock>(), t => t.Text.Contains("No saved AI"));
                scroll.ScrollToHome(); Flush(); root.UpdateLayout(); Render(root, theme, width, dpi, "empty");
                root.Child = null;
            }
            f.Vm.Deactivate();
            empty.Vm.Deactivate();
        }, shutdownDispatcher: false);
        Assert.Equal(saved.EvidenceJson, Assert.Single(f.History.Items).EvidenceJson);
        Assert.Equal(0, f.History.Writes);
    }

    [Fact]
    public async Task LoadingCancellationIsOnlyVisibleWhileCancellableAndJournalOnlyDayHasNoTradeOutcome()
    {
        if (Environment.GetEnvironmentVariable("PTJ_DAILY_REVIEW_TEST_HOST") != "1") { await Host.Value; return; }
        var pending = new TaskCompletionSource<PersonalTradingJournal.Application.DailyReview.DailyReviewEvidence>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var f = new ReviewFixture();
        f.Reader.Handler = (_, _) => { started.TrySetResult(); return pending.Task; };
        Task load = Task.CompletedTask;
        DailyReviewView view = null!;
        Border root = null!;
        static Task OnUi(Action action) => CalendarStaTest.RunAsync(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            action();
        }, shutdownDispatcher: false);
        await OnUi(() =>
        {
            var app = System.Windows.Application.Current ?? new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources = CalendarViewLayoutTests.SharedThemeResources("Dark");
            load = f.Vm.ActivateAsync();
            view = new DailyReviewView { DataContext = f.Vm };
            root = new Border { Child = view };
            root.Measure(new Size(480, 760)); root.Arrange(new Rect(0, 0, 480, 760)); root.UpdateLayout(); Flush();
        });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await OnUi(() =>
        {
            Flush(); root.UpdateLayout();
            var cancel = (Button)view.FindName("CancelLoading");
            Assert.Equal(Visibility.Visible, cancel.Visibility);
            Assert.True(cancel.IsEnabled && cancel.Focusable);
            cancel.Command!.Execute(null); Flush();
            Assert.Equal(Visibility.Collapsed, cancel.Visibility);
        });
        var evidence = new PersonalTradingJournal.Application.DailyReview.DailyReviewEvidence(new(ReviewFixture.Day), [],
            [ReviewFixture.Journal(null, "All accounts", ReviewFixture.Day)]);
        pending.SetResult(evidence);
        await load.WaitAsync(TimeSpan.FromSeconds(10));
        await OnUi(() =>
        {
            Assert.Null(f.Vm.Current); // Cancelled evidence cannot populate the new summary.
            f.Reader.Handler = (_, _) => Task.FromResult(evidence);
            load = f.Vm.RefreshCommand.ExecuteAsync(null);
        });
        await load;
        await OnUi(() =>
        {
            Flush(); root.UpdateLayout();
            Assert.StartsWith("No closed trades", ((TextBlock)view.FindName("ScopeCounts")).Text);
            Assert.Equal(Visibility.Collapsed, ((Button)view.FindName("CancelLoading")).Visibility);
            Assert.Equal(0, ((ScrollViewer)view.FindName("ReviewScroll")).ScrollableWidth);
            Render(root, "Dark", 480, 240, "journal-only");
            f.Vm.Deactivate(); root.Child = null;
        });
    }

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
                Assert.DoesNotContain(Descendants(workspace).OfType<Button>(), b => Equals(b.Content, "Generate AI Review"));
                Assert.Single(Descendants(workspace).OfType<Button>(), b => Equals(b.Content, "Prepare ChatGPT Prompt"));
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectionAndInputBudgetMessagesRemainVisibleWithoutRawDiagnostics(bool inputLimit)
    {
        if (Environment.GetEnvironmentVariable("PTJ_DAILY_REVIEW_TEST_HOST") != "1") { await Host.Value; return; }
        var store = new ManualReviewWorkspaceTests.Store { TooLarge = inputLimit };
        var vm = new PersonalTradingJournal.Desktop.ViewModels.DailyReview.ManualReviewViewModel(store,
            PersonalTradingJournal.Application.DailyReview.Coaching.ManualReviewKind.Daily,
            () => new(PersonalTradingJournal.Application.DailyReview.Coaching.ManualReviewKind.Daily, ReviewFixture.Day, null, "All accounts"),
            new FakeDialogService(), new ManualReviewWorkspaceTests.Clip(), () => Task.CompletedTask);
        await vm.LoadAsync(); await vm.PrepareCommand.ExecuteAsync(null);
        if (!inputLimit) { vm.ResponseText = "{}"; await vm.ValidateCommand.ExecuteAsync(null); }
        Assert.NotNull(vm.Message); Assert.Null(vm.Preview); Assert.Equal(0, store.Saves);
        if (inputLimit) Assert.Contains("TooLarge", vm.Message);
        await CalendarStaTest.RunAsync(() =>
        {
            var app = System.Windows.Application.Current ?? new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources = CalendarViewLayoutTests.SharedThemeResources("Dark");
            var view = new ManualReviewView { DataContext = vm }; var root = new Border { Child = view };
            root.Measure(new Size(480, 760)); root.Arrange(new Rect(0, 0, 480, 760)); root.UpdateLayout(); Flush();
            var message = Assert.Single(Descendants(view).OfType<TextBlock>(), t => t.Text == vm.Message);
            Assert.Equal(TextWrapping.Wrap, message.TextWrapping);
            root.Child = null;
        }, shutdownDispatcher: false);
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
                Assert.DoesNotContain(Descendants(reviewView).OfType<Button>(), b => Equals(b.Content, "Configure AI in Settings"));
                Assert.Single(Descendants(reviewView).OfType<Button>(), b => Equals(b.Content, "Prepare ChatGPT Prompt"));
                root.Child = null;
            }
            review.Deactivate();
        }, shutdownDispatcher: false);
        Assert.Equal(0, navigated);
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
                Assert.DoesNotContain(buttons, b => Equals(b.Content, "Generate AI Review"));
                Assert.Equal(Visibility.Collapsed, ((Button)view.FindName("CancelLoading")).Visibility);
                Assert.Single(Descendants(view).OfType<Button>(), b => Equals(b.Content, "Prepare ChatGPT Prompt"));
                Assert.DoesNotContain(Descendants(view).OfType<TextBlock>(), t => t.Text == "Daily Review");
                Assert.Contains("08 Oct 2026", ((TextBlock)view.FindName("SelectedScopeHeading")).Text);
                Assert.Contains("All accounts", ((TextBlock)view.FindName("SelectedScopeHeading")).Text);
                Assert.Contains("2 closed Trades", ((TextBlock)view.FindName("ScopeCounts")).Text);
                Assert.Contains("3 Journal entries", ((TextBlock)view.FindName("ScopeCounts")).Text);
                Assert.Contains("Some Net results are unavailable", ((TextBlock)view.FindName("ScopeCoverage")).Text);
                var toolbar = (WrapPanel)view.FindName("ScopeToolbar");
                var summary = (Border)view.FindName("ScopeSummary");
                Assert.True(toolbar.TranslatePoint(new Point(0, toolbar.ActualHeight), view).Y <= summary.TranslatePoint(new Point(), view).Y);
                Assert.True(Assert.Single(Descendants(view).OfType<Button>(), b => Equals(b.Content, "Prepare ChatGPT Prompt")).TranslatePoint(new Point(), view).Y < 760);
                Assert.DoesNotContain(Descendants(view).OfType<Expander>(), e => e.Header?.ToString()?.Contains("Source details") == true);
                Assert.Contains(Descendants(view).OfType<TextBlock>(), t => t.Text.Contains("All accounts · Completed"));
                Assert.All(buttons, b =>
                {
                    Assert.True(b.Focusable);
                    double right = b.TranslatePoint(new Point(b.ActualWidth, 0), view).X;
                    Assert.InRange(right, 0, width + 1);
                });
                var text = Descendants(view).OfType<TextBlock>().Select(t => t.Text).ToArray();
                Assert.Contains("Saved snapshot · NOT current records", text);
                Assert.Contains(text, t => t.Contains("commissions missing for 1 Trade"));
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
                var observations = Descendants(view).OfType<TextBlock>().First(t => t.Text == "Journal observations · user-written");
                scroll.ScrollToVerticalOffset(scroll.VerticalOffset + observations.TranslatePoint(new Point(), scroll).Y);
                Flush(); root.UpdateLayout();
                Render(root, theme, width, dpi, "journals");
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
        foreach (var (theme, width, dpi) in new[] { ("Light", 960, 96), ("Dark", 960, 96), ("Light", 480, 240), ("Dark", 480, 240) })
            await ManualReviewViewTests.Verify(theme, width, dpi);
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
