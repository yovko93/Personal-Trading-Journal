using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Views.PeriodReviews;
using PersonalTradingJournal.Desktop.ViewModels.PeriodReviews;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Application.PeriodReviews;
using System.Text.Json.Nodes;

namespace PersonalTradingJournal.Desktop.Tests.PeriodReviews;

public sealed class PeriodViewTests
{
    private static readonly Lazy<Task> Host = new(() => IsolatedTestProcess.RunSuiteAsync(typeof(PeriodViewTests),
        "period-review-layout", "PTJ_PERIOD_REVIEW_HOST", TimeSpan.FromMinutes(2), caseHangTimeout: TimeSpan.FromSeconds(30)));
    [Theory] [InlineData("Light", 1100, 96)] [InlineData("Dark", 1100, 96)] [InlineData("Light", 480, 240)] [InlineData("Dark", 480, 240)]
    public async Task BoundReportCardsWrapScrollAndKeepActionsAccessible(string theme, int width, int dpi)
    {
        if (Environment.GetEnvironmentVariable("PTJ_PERIOD_REVIEW_HOST") != "1") { await Host.Value; return; }
        var f = new PeriodFixture();
        f.Provider.Handler = (p, _) =>
        {
            var reply = PeriodFixture.ProviderFake.Success(p); var json = JsonNode.Parse(reply.ResponseJson!)!;
            foreach (var name in new[] { "strengths", "areasForImprovement" })
            {
                var finding = json[name]!["findings"]![0]!;
                finding["text"] = "Recorded participation is visible. The summary covers all closed Trades, while examples describe only the identified sources.";
                var second = finding.DeepClone(); second["text"] = "Keep currency boundaries clear. Evaluate each Account and currency separately; no exchange-rate conversion is available.";
                var third = finding.DeepClone(); third["text"] = "Net coverage needs attention. Missing costs remain unknown and must not be read as zero.";
                json[name]!["findings"]!.AsArray().Add(second); json[name]!["findings"]!.AsArray().Add(third);
            }
            return Task.FromResult(reply with { ResponseJson = json.ToJsonString() });
        };
        await f.Vm.ActivateAsync(); await f.Vm.GenerateCommand.ExecuteAsync(null); Assert.NotNull(f.Vm.Report);
        var before = new PeriodFixture(); await before.Vm.ActivateAsync();
        var failed = new PeriodFixture(); await failed.Vm.ActivateAsync();
        failed.Provider.Handler = (_, _) => Task.FromResult(new CoachingProviderReply(CoachingGenerationStatus.InputTooLarge, null,
            new("Groq", "synthetic", "client", InputBudget: new(500, 626, 6000, 83, 721, 512, 6800, 1000))));
        await failed.Vm.GenerateCommand.ExecuteAsync(null);
        var loading = new PeriodFixture(); var entered = new TaskCompletionSource();
        var pending = new TaskCompletionSource<PeriodComparisonEvidenceResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        loading.Reader.Handler = (_, _) => { entered.TrySetResult(); return pending.Task; };
        var activation = loading.Vm.ActivateAsync(); await entered.Task;
        var generating = new PeriodFixture(); await generating.Vm.ActivateAsync();
        var pendingGeneration = new TaskCompletionSource<CoachingProviderReply>(TaskCreationOptions.RunContinuationsAsynchronously);
        generating.Provider.Handler = (_, _) => pendingGeneration.Task;
        var generation = generating.Vm.GenerateCommand.ExecuteAsync(null); await generating.Provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await CalendarStaTest.RunAsync(() =>
        {
            var app = System.Windows.Application.Current ?? new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
            foreach (string brush in new[] { "PtjReviewStrengthSurfaceBrush", "PtjReviewImproveSurfaceBrush", "PtjReviewProgressSurfaceBrush", "PtjReviewPlanSurfaceBrush" })
                Assert.True(Contrast(((SolidColorBrush)app.Resources["PtjTextPrimaryBrush"]).Color, ((SolidColorBrush)app.Resources[brush]).Color) >= 4.5);
            foreach (string accent in new[] { "PtjReviewStrengthAccentBrush", "PtjReviewImproveAccentBrush" })
                Assert.True(Contrast(((SolidColorBrush)app.Resources[accent]).Color, ((SolidColorBrush)app.Resources["PtjBackgroundBrush"]).Color) >= 4.5);
            var view = new PeriodReviewView { DataContext = f.Vm }; var root = new Border { Child = view };
            root.SetResourceReference(Border.BackgroundProperty, "PtjBackgroundBrush");
            void Layout() { root.Measure(new(width, 760)); root.Arrange(new(0, 0, width, 760)); root.UpdateLayout(); Flush(); }
            Layout(); var scroll = (ScrollViewer)view.FindName("PeriodScroll");
            foreach (var (vm, state) in new[] { (before.Vm, "before"), (failed.Vm, "too-large"), (loading.Vm, "loading"), (generating.Vm, "generating") })
            {
                view.DataContext = vm; Layout();
                Assert.Equal(0, scroll.ScrollableWidth);
                Assert.Equal(state == "before" ? Visibility.Visible : Visibility.Collapsed, ((Border)view.FindName("BeforeReport")).Visibility);
                Assert.Equal(vm.IsLoading, Descendants(view).OfType<Button>().Any(b => b.Visibility == Visibility.Visible && Equals(b.Content, "Cancel loading")));
                Assert.DoesNotContain(Descendants(view).OfType<Button>(), b => Equals(b.Content, "Generate AI Review") || Equals(b.Content, "Cancel generation"));
                Assert.False(((Expander)view.FindName("HistorySection")).IsExpanded);
                if (state == "too-large") { Assert.Contains("6,800", vm.Message); Assert.Empty(vm.History); Assert.Null(vm.Report); }
                Render(root, theme, width, dpi, state);
            }
            view.DataContext = f.Vm; Layout();
            Assert.Equal(0, scroll.ScrollableWidth); Assert.True(scroll.ScrollableHeight > 0);
            var texts = Descendants(view).OfType<TextBlock>().Select(t => t.Text).ToArray();
            foreach (string title in new[] { "Strengths", "Areas for Improvement", "Mistakes Observed", "Progress", "Action Plan" }) Assert.Contains(title, texts);
            Assert.DoesNotContain(texts, s => s.Contains(f.Provider.Packet!.PacketId) || s.Contains("current:statistics") || s.Contains("Source details"));
            foreach (var button in Descendants(view).OfType<Button>().Where(b => b.IsVisible && b.Content is string))
            { Assert.True(button.Focusable && button.IsTabStop); Assert.InRange(button.TranslatePoint(new(button.ActualWidth, 0), view).X, 0, width); }
            Assert.Equal("Prepare ChatGPT Prompt locally", AutomationProperties.GetName(Assert.Single(Descendants(view).OfType<Button>(), b => Equals(b.Content, "Prepare ChatGPT Prompt"))));
            Assert.Contains("New York", ((TextBlock)view.FindName("PeriodHeading")).Text);
            Assert.DoesNotContain(Descendants(view).OfType<Button>(), b => b.Visibility == Visibility.Visible && Equals(b.Content, "Cancel generation"));
            Render(root, theme, width, dpi, "top");
            var cards = Descendants((ContentControl)view.FindName("SavedReport")).OfType<ReviewCardsPanel>().Single();
            Assert.Equal(3, f.Vm.Report!.Cards[0].Findings.Count); Assert.Equal(3, f.Vm.Report.Cards[1].Findings.Count);
            Assert.Equal(Visibility.Collapsed, ((Border)view.FindName("BeforeReport")).Visibility);
            var first = (FrameworkElement)VisualTreeHelper.GetChild(cards, 0); var second = (FrameworkElement)VisualTreeHelper.GetChild(cards, 1);
            if (width >= 840) Assert.Equal(first.TranslatePoint(new(), cards).Y, second.TranslatePoint(new(), cards).Y);
            else Assert.True(second.TranslatePoint(new(), cards).Y > first.TranslatePoint(new(), cards).Y);
            scroll.ScrollToVerticalOffset(((ContentControl)view.FindName("SavedReport")).TranslatePoint(new(), scroll).Y); Layout(); Render(root, theme, width, dpi, "cards");
            var citationExpander = Descendants(view).OfType<Expander>().First(e => Equals(e.Header, "Saved evidence citations")); citationExpander.IsExpanded = true; Layout();
            var citation = Descendants(citationExpander).OfType<Button>().First(b => b.DataContext is PeriodCitation);
            Assert.True(citation.IsEnabled); Assert.Contains("saved citation", AutomationProperties.GetName(citation)); citation.Command!.Execute(citation.CommandParameter); Layout(); Assert.NotNull(f.Vm.Citation);
            foreach (var expander in Descendants(view).OfType<Expander>().Where(e => e.Header is string s && s.StartsWith("Current ")).ToArray()) expander.IsExpanded = true;
            ((Expander)view.FindName("HistorySection")).IsExpanded = true;
            Layout(); Assert.Equal(0, scroll.ScrollableWidth); scroll.ScrollToEnd(); Layout(); Assert.InRange(scroll.VerticalOffset, scroll.ScrollableHeight - 1, scroll.ScrollableHeight + 1);
            Render(root, theme, width, dpi, "history");
            f.Vm.Deactivate(); before.Vm.Deactivate(); failed.Vm.Deactivate(); loading.Vm.Deactivate(); generating.Vm.Deactivate(); view.DataContext = null; root.Child = null;
        }, shutdownDispatcher: false);
        pending.TrySetResult(new(PeriodEvidenceStatus.Ready, PeriodFixture.Pair(loading.Vm.Query!), null)); await activation;
        pendingGeneration.TrySetResult(PeriodFixture.ProviderFake.Success(generating.Provider.Packet!)); await generation;
        Assert.Equal(0, generating.Store.Saves);
    }
    private static void Flush() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static double Contrast(Color a, Color b)
    {
        static double Linear(byte value) { double c = value / 255d; return c <= .04045 ? c / 12.92 : Math.Pow((c + .055) / 1.055, 2.4); }
        static double L(Color c) => .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B);
        return (Math.Max(L(a), L(b)) + .05) / (Math.Min(L(a), L(b)) + .05);
    }
    internal static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    { for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); yield return child; foreach (var c in Descendants(child)) yield return c; } }
    private static void Render(Border root, string theme, int width, int dpi, string section)
    {
        var bitmap = new RenderTargetBitmap(width * dpi / 96, 760 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32); bitmap.Render(root);
        Assert.Equal(width * dpi / 96, bitmap.PixelWidth);
        if (Environment.GetEnvironmentVariable("PTJ_PERIOD_RENDER_DIRECTORY") is not { Length: > 0 } directory) return;
        Directory.CreateDirectory(directory); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(directory, $"period-{theme}-{width}-{dpi}-{section}.png")); png.Save(file);
    }
}
