using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;
using PersonalTradingJournal.Desktop.Views.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

public sealed partial class NotebookHeaderViewTests
{
    [Theory]
    [InlineData("Light", 1280, 96)]
    [InlineData("Dark", 1280, 96)]
    [InlineData("Light", 480, 240)]
    [InlineData("Dark", 480, 240)]
    public async Task CreationButtonsMatchDimensionsTypographyAndStateOffscreen(string theme, int width, int dpi)
    {
        if (Environment.GetEnvironmentVariable(Host) != "1")
        {
            string expected = string.Create(CultureInfo.InvariantCulture,
                $"{typeof(NotebookHeaderViewTests).FullName}.{nameof(CreationButtonsMatchDimensionsTypographyAndStateOffscreen)}(theme: \"{theme}\", width: {width}, dpi: {dpi})");
            await IsolatedTestProcess.RunSuiteAsync(typeof(NotebookHeaderViewTests), "notebook-creation-buttons", Host,
                TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30),
                testCaseFilter: IsolatedTestProcess.ExactDisplayNameFilter(expected), expectedTestName: expected);
            return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown,
                Resources = CalendarViewLayoutTests.SharedThemeResources(theme) };
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var data = new NotebookFake(); data.Folders.Add(new(Guid.NewGuid(), "Market research", 1)); data.Tags.Add(new(Guid.NewGuid(), "NQ", 1));
            using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService());
            var view = new NotebookWorkspaceView { DataContext = vm };
            VisualTreeHelper.SetRootDpi(view, new(dpi / 96d, dpi / 96d));
            var release = new TaskCompletionSource<NotebookWriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task pending = Task.CompletedTask;
            try
            {
                Pump(vm.ActivateAsync()); view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent)); vm.Panel = 0; Layout();
                var folder = (Button)view.FindName("NewFolderButton"); var tag = (Button)view.FindName("NewTagButton");
                Assert.Equal("New folder", AutomationProperties.GetName(folder)); Assert.Equal("New tag", AutomationProperties.GetName(tag));
                Assert.Same(folder.Style, tag.Style); Assert.Same(folder.Template, tag.Template);
                Assert.Equal(folder.RenderSize, tag.RenderSize); Assert.Equal(new Size(112, 34), tag.RenderSize);
                Assert.Equal(folder.FontFamily, tag.FontFamily); Assert.Equal(folder.FontSize, tag.FontSize); Assert.Equal(folder.FontWeight, tag.FontWeight);
                Assert.Equal(folder.Padding, tag.Padding); Assert.Equal(folder.BorderThickness, tag.BorderThickness);
                Assert.Equal(folder.TranslatePoint(new(), view).X, tag.TranslatePoint(new(), view).X);
                Assert.True(folder.Focusable); Assert.True(tag.Focusable);
                foreach (var button in new[] { folder, tag })
                {
                    Assert.True(button.IsEnabled); Assert.NotNull(button.ToolTip);
                    var text = new FormattedText((string)button.Content, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        new Typeface(button.FontFamily, button.FontStyle, button.FontWeight, button.FontStretch), button.FontSize, Brushes.Black, dpi / 96d);
                    Assert.True(text.WidthIncludingTrailingWhitespace + button.Padding.Left + button.Padding.Right + 2 <= button.ActualWidth);
                    var chrome = (Border)button.Template.FindName("ActionChrome", button);
                    Assert.Equal(view.FindResource("PtjCornerRadiusSm"), chrome.CornerRadius);
                    Assert.Contains(button.Template.Triggers.OfType<Trigger>(), t => t.Property == UIElement.IsKeyboardFocusedProperty && t.Setters.OfType<Setter>().Any(s => s.TargetName == "ActionFocus" && Equals(s.Value, Visibility.Visible)));
                    var p = button.TranslatePoint(new(), view); Assert.InRange(p.X + button.ActualWidth, 0, width); Assert.InRange(p.Y + button.ActualHeight, 0, 900);
                }
                Assert.Same(folder.Background, tag.Background); Assert.Same(folder.Foreground, tag.Foreground); Render("enabled");
                var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                data.OnWrite = (_, _) => { entered.TrySetResult(); return release.Task; };
                pending = vm.NewNoteCommand.ExecuteAsync(null); Pump(entered.Task.WaitAsync(TimeSpan.FromSeconds(10))); Layout();
                Assert.False(folder.IsEnabled); Assert.False(tag.IsEnabled);
                Assert.Equal(folder.RenderSize, tag.RenderSize); Assert.Same(folder.Background, tag.Background); Assert.Same(folder.Foreground, tag.Foreground); Render("busy-disabled");
                release.SetResult(new(NotebookWriteStatus.Cancelled)); Pump(pending); Layout();
                Assert.True(folder.IsEnabled); Assert.True(tag.IsEnabled);
            }
            finally
            {
                release.TrySetResult(new(NotebookWriteStatus.Cancelled)); Pump(pending);
                vm.Deactivate(); Pump(vm.DeactivationTask); view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                app.Shutdown(); SynchronizationContext.SetSynchronizationContext(previous);
            }
            void Layout() { view.Measure(new Size(width, 900)); view.Arrange(new Rect(0, 0, width, 900)); view.UpdateLayout(); Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); }
            void Render(string state)
            {
                string directory = Path.Combine(Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY")!, "creation-button-renders"); Directory.CreateDirectory(directory);
                var bitmap = new RenderTargetBitmap(width * dpi / 96, 900 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32); bitmap.Render(view);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(directory, $"{theme}-{width}-{dpi}-{state}.png")); encoder.Save(file);
            }
        });
    }
}
