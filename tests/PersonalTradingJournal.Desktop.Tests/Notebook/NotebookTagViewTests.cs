using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Views.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

public sealed class NotebookTagViewTests
{
    private const string Host = "PTJ_NOTEBOOK_TAG_HOST";
    [Theory]
    [InlineData("Light", 1280, 96)]
    [InlineData("Dark", 1280, 96)]
    [InlineData("Light", 480, 240)]
    [InlineData("Dark", 480, 240)]
    public async Task BoundTagsDistinguishFiltersAssignmentsAndGlobalDeletionWithRetainedEditor(string theme, int width, int dpi)
    {
        if (Environment.GetEnvironmentVariable(Host) != "1")
        {
            await IsolatedTestProcess.RunSuiteAsync(typeof(NotebookTagViewTests), "notebook-tags", Host,
                TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30)); return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            var app = System.Windows.Application.Current ?? new() { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var open = NotebookTagTests.Fixture.Open(); Pump(open);
            var fixture = open.Result; var vm = fixture.Vm;
            var view = new NotebookWorkspaceView { DataContext = vm };
            VisualTreeHelper.SetRootDpi(view, new(dpi / 96d, dpi / 96d));
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            try
            {
                vm.ManagedTag = vm.Tags[0]; vm.TagName = "Long research observations with multiple words and context";
                Pump(vm.RenameTagCommand.ExecuteAsync(null));
                Pump(vm.ToggleTagFilterCommand.ExecuteAsync(vm.Tags[0])); Pump(vm.ToggleTagFilterCommand.ExecuteAsync(vm.Tags[1]));
                vm.SearchText = "observation"; Pump(vm.SearchCommand.ExecuteAsync(null));
                Assert.Single(vm.Notes); Assert.Equal(2, vm.SelectedTagFilters.Count);
                vm.Panel = 0; Assert.Null(view.FindName("TagManagement"));
                Layout(); Render("filters");
                Assert.Equal(Visibility.Visible, ((Button)view.FindName("NewTagButton")).Visibility);
                var management = Descendants<Button>((ItemsControl)view.FindName("TagFilters")).ToArray();
                Assert.Equal(2, management.Length);
                Assert.All(management, b => Assert.Contains("Manage global tag:", AutomationProperties.GetName(b)));
                Assert.All(management, b => Assert.Contains("Does not change", AutomationProperties.GetHelpText(b)));
                var filters = Descendants<CheckBox>((ItemsControl)view.FindName("TagFilters")).ToArray();
                Assert.Equal(2, filters.Length); Assert.All(filters, c => Assert.True(c.IsChecked));
                Assert.All(filters, c => Assert.Contains("Filter", AutomationProperties.GetHelpText(c)));
                vm.TagName = " "; Pump(vm.CreateTagCommand.ExecuteAsync(null)); Layout(); Render("validation");
                Assert.Contains(Descendants<TextBlock>(view), t => t.Text == vm.TagMessage && AutomationProperties.GetLiveSetting(t) == AutomationLiveSetting.Polite);
                Assert.True(vm.BeginTagRename(vm.Tags[0])); Layout(); Render("management"); vm.CancelTagForm();
                vm.Panel = 1; Layout(); Render("combined-filters");
                vm.Panel = 2; Layout();
                var addTag = Descendants<Expander>(view).Single(e => AutomationProperties.GetName(e) == "Assign an existing tag to this note");
                addTag.IsExpanded = true; vm.AssignmentTag = vm.Tags[0];
                Layout(); Render("assignments");
                var editor = (RichTextBox)view.FindName("Preview"); var document = editor.Document;
                long revision = vm.EditorRevision;
                var assignment = vm.AssignedTags[0];
                Pump(vm.RemoveTagCommand.ExecuteAsync(assignment)); Layout(); Render("assignment-removed");
                Assert.Same(document, editor.Document); Assert.Equal(revision, vm.EditorRevision);
                Assert.Single(vm.AssignedTags); Assert.Empty(vm.Notes); Assert.NotNull(vm.Note);
                var remove = Descendants<Button>((ItemsControl)view.FindName("AssignedTagList")).Single();
                Assert.Contains("from this note", AutomationProperties.GetName(remove));
                Assert.Contains("global tag", AutomationProperties.GetHelpText(remove));
                foreach (var button in Descendants<Button>(view).Where(b => b.IsVisible))
                {
                    var p = button.TranslatePoint(new(0, 0), view);
                    Assert.InRange(p.X, -1, width); Assert.InRange(p.X + button.ActualWidth, 0, width + 1);
                }
                Pump(vm.ClearFiltersCommand.ExecuteAsync(null)); Layout(); Render("cleared");
                Assert.Single(vm.Notes); Assert.Empty(vm.SelectedTagFilters);
            }
            finally
            {
                Pump(fixture.DisposeAsync().AsTask());
                view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                SynchronizationContext.SetSynchronizationContext(previous);
            }
            int Height() => width < 1000 ? 760 : 960;
            void Layout() { view.Measure(new(width, Height())); view.Arrange(new Rect(0, 0, width, Height())); view.UpdateLayout(); }
            void Render(string state)
            {
                var directory = Path.Combine(AppContext.BaseDirectory, "notebook-tag-renders"); Directory.CreateDirectory(directory);
                var bitmap = new RenderTargetBitmap(width * dpi / 96, Height() * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
                bitmap.Render(view); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var output = File.Create(Path.Combine(directory, $"{theme}-{width}-{dpi}-{state}.png")); encoder.Save(output);
            }
        });
    }
    private static void Pump(Task task)
    {
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            _ = task.ContinueWith(_ => Dispatcher.CurrentDispatcher.BeginInvoke(() => frame.Continue = false), CancellationToken.None,
                TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
            Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
}
