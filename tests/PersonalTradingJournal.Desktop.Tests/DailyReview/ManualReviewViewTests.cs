using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.DailyReview;
using PersonalTradingJournal.Desktop.Views.DailyReview;

namespace PersonalTradingJournal.Desktop.Tests.DailyReview;

public sealed class ManualReviewViewTests
{
    private static readonly Lazy<Task> Host = new(() => IsolatedTestProcess.RunSuiteAsync(typeof(ManualReviewViewTests),
        "manual-review-layout", "PTJ_MANUAL_REVIEW_HOST", TimeSpan.FromMinutes(2), caseHangTimeout: TimeSpan.FromSeconds(30)));
    [Theory]
    [InlineData("Light", 1100, 96)] [InlineData("Dark", 1100, 96)] [InlineData("Light", 480, 240)] [InlineData("Dark", 480, 240)]
    public async Task ManualCardsClipboardErrorsAndLocalActionsWrap(string theme, int width, int dpi)
    {
        if (Environment.GetEnvironmentVariable("PTJ_MANUAL_REVIEW_HOST") != "1") { await Host.Value; return; }
        foreach (var kind in Enum.GetValues<ManualReviewKind>()) await Verify(theme, width, dpi, kind);
    }

    internal static Task Verify(string theme, int width, int dpi, ManualReviewKind kind = ManualReviewKind.Daily) => CalendarStaTest.RunAsync(() =>
    {
        var app = System.Windows.Application.Current ?? new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
        var store = new ManualReviewWorkspaceTests.Store(); var clip = new ManualReviewWorkspaceTests.Clip();
        var vm = new ManualReviewViewModel(store, kind, () => new(kind, ReviewFixture.Day, null, "All accounts"), new FakeDialogService(), clip, () => Task.CompletedTask);
        vm.LoadAsync().GetAwaiter().GetResult();
        var view = new ManualReviewView { DataContext = vm }; var scroll = new ScrollViewer { Content = view, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var root = new Border { Child = scroll, Padding = new Thickness(16) }; root.SetResourceReference(Border.BackgroundProperty, "PtjBackgroundBrush");
        void Layout() { root.Measure(new(width, 760)); root.Arrange(new(0, 0, width, 760)); root.UpdateLayout(); Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); }
        Button Button(string text) => Assert.Single(Descendants(view).OfType<Button>(), b => Equals(b.Content, text));
        Layout(); Assert.True(Button("Prepare ChatGPT Prompt").IsEnabled); Assert.Equal(0, clip.Reads + clip.Writes);
        Assert.DoesNotContain(Descendants(view).OfType<Button>(), b => Equals(b.Content, "Generate AI Review"));
        Assert.Contains("locally", AutomationProperties.GetName(Button("Prepare ChatGPT Prompt")));
        vm.PrepareCommand.ExecuteAsync(null).GetAwaiter().GetResult(); Layout();
        Assert.True(Button("Copy Prompt").IsEnabled); Assert.False(Button("Save Review").IsEnabled);
        var preview = Assert.Single(Descendants(view).OfType<Expander>(), e => Equals(e.Header, "Exact prompt preview"));
        preview.IsExpanded = true; Layout();
        Assert.Equal(vm.Prompt, Assert.Single(Descendants(view).OfType<TextBox>(), t => t.IsReadOnly).Text);
        clip.Fail = true; Button("Copy Prompt").Command!.Execute(null); Assert.Contains("Clipboard unavailable", vm.Message); clip.Fail = false;
        Button("Copy Prompt").Command!.Execute(null); Assert.Equal(vm.Prompt, clip.Text);
        var response = (TextBox)view.FindName("Response"); response.SetCurrentValue(TextBox.TextProperty, "bad JSON");
        response.GetBindingExpression(TextBox.TextProperty)!.UpdateSource(); Assert.False(vm.TryLeave());
        vm.ValidateCommand.ExecuteAsync(null).GetAwaiter().GetResult(); Layout(); Assert.Null(vm.Preview); Assert.Equal("bad JSON", response.Text);
        response.SetCurrentValue(TextBox.TextProperty, ManualReviewWorkspaceTests.Response(store.Draft!)); response.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        vm.ValidateCommand.ExecuteAsync(null).GetAwaiter().GetResult(); Layout(); Assert.NotNull(vm.Preview); Assert.True(Button("Save Review").IsEnabled);
        Assert.Equal(kind == ManualReviewKind.Daily ? 1 : 5, vm.PreviewCards.Count);
        foreach (var b in Descendants(view).OfType<Button>().Where(b => b.ActualWidth > 0 && b.Visibility == Visibility.Visible))
        { Assert.True(b.Focusable && b.IsTabStop); Assert.InRange(b.TranslatePoint(new(b.ActualWidth, 0), root).X, 0, width + 1); }
        Assert.Equal(0, scroll.ScrollableWidth);
        Render(root, theme, width, dpi, kind, "preview");
        scroll.ScrollToEnd(); Layout(); Assert.InRange(scroll.VerticalOffset, scroll.ScrollableHeight - 1, scroll.ScrollableHeight + 1);
        Render(root, theme, width, dpi, kind, "cards");
        store.FailSave = true; vm.SaveCommand.ExecuteAsync(null).GetAwaiter().GetResult(); Assert.Contains("failed", vm.Message); Assert.DoesNotContain("synthetic-private", vm.Message); Assert.NotNull(vm.Preview);
        store.FailSave = false; vm.SaveCommand.ExecuteAsync(null).GetAwaiter().GetResult(); Layout(); Assert.Equal(1, store.Saves); Assert.False(Button("Save Review").IsEnabled); Assert.False(response.IsEnabled);
        root.Child = null;
    }, shutdownDispatcher: false);

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    { for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var c = VisualTreeHelper.GetChild(root, i); yield return c; foreach (var nested in Descendants(c)) yield return nested; } }
    private static void Render(Border root, string theme, int width, int dpi, ManualReviewKind kind, string state)
    {
        string? directory = Environment.GetEnvironmentVariable("PTJ_MANUAL_REVIEW_RENDER_DIRECTORY"); if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory); var bitmap = new RenderTargetBitmap((int)(width * dpi / 96d), (int)(760 * dpi / 96d), dpi, dpi, PixelFormats.Pbgra32); bitmap.Render(root);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(directory, $"manual-{kind}-{theme}-{width}-{dpi}-{state}.png")); encoder.Save(file);
    }
}
