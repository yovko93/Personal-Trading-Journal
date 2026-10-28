using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Views.Notebook;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;
using PersonalTradingJournal.Application.Notebook;

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
                // Review actual arranged geometry offscreen; no native input or focus prerequisites.
                vm.Panel = 0; Layout();
                Assert.Equal(new[] { NotebookScope.AllNotes, NotebookScope.Pinned, NotebookScope.Trash },
                    ((ListBox)view.FindName("ScopeList")).Items.Cast<NotebookScopeOption>().Select(s => s.Scope));
                Assert.Equal("Refresh Notebook", AutomationProperties.GetName((Button)view.FindName("NotebookRefresh")));
                var newTag = (Button)view.FindName("NewTagButton");
                var heading = Descendants<TextBlock>((Grid)newTag.Parent).Single(t => t.Text == "TAGS");
                Centered(heading, newTag); Render("default");
                var refresh = (Button)view.FindName("NotebookRefresh");
                Assert.Same(vm.RefreshCommand, refresh.Command); Assert.True(refresh.IsEnabled);
                var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                fixture.Data.OnList = async (query, ct) =>
                {
                    entered.TrySetResult(); await release.Task.WaitAsync(ct); return fixture.Data.Page(query);
                };
                var refreshing = vm.RefreshCommand.ExecuteAsync(null);
                try
                {
                    Pump(entered.Task.WaitAsync(TimeSpan.FromSeconds(10))); Layout();
                    Assert.True(vm.IsBusy); Assert.False(refresh.IsEnabled); Render("refresh-busy");
                }
                finally { release.TrySetResult(); Pump(refreshing.WaitAsync(TimeSpan.FromSeconds(10))); fixture.Data.OnList = null; }
                Layout(); Assert.False(vm.IsBusy); Assert.True(refresh.IsEnabled); Render("refresh-ready");
                vm.ShowNotification("An operation needs attention. Your existing notes, tag filters and recovery actions remain available while this wrapped message is displayed.", NotebookNotificationKind.Warning);
                Layout(); Render("notification-visible"); vm.DismissNotificationCommand.Execute(null); Layout(); Render("notification-dismissed");
                Assert.True(vm.BeginTagCreation()); Layout();
                var add = (Button)view.FindName("AddNewTagButton"); var cancel = (Button)view.FindName("CancelTagCreationButton");
                Assert.Equal(Visibility.Collapsed, add.Visibility); Assert.Equal(Visibility.Visible, cancel.Visibility);
                Assert.True(cancel.ActualWidth > 0 && cancel.ActualHeight > 0); // Offscreen trees have no native IsVisible surface.
                var cancelOrigin = cancel.TranslatePoint(new(), view); double formHeight = ((FrameworkElement)view.FindName("TagCreation")).ActualHeight;
                Render("creation-empty"); vm.TagName = " \t "; Layout(); Assert.Equal(Visibility.Collapsed, add.Visibility);
                vm.TagName = "New global tag"; Layout(); Assert.Equal(Visibility.Visible, add.Visibility);
                Assert.Equal(cancelOrigin, cancel.TranslatePoint(new(), view));
                Assert.Equal(formHeight, ((FrameworkElement)view.FindName("TagCreation")).ActualHeight);
                Centered(add, cancel); Bounds(add); Bounds(cancel); Bounds((TextBox)view.FindName("NewTagName")); Render("creation-populated");
                vm.TagName = " "; Pump(vm.CreateTagCommand.ExecuteAsync(null)); Layout();
                Assert.True(vm.IsTagCreationOpen); Assert.NotEmpty(vm.TagMessage); Assert.Equal(" ", vm.TagName);
                Assert.Equal(0, fixture.Data.Writes); Render("creation-error"); vm.CancelTagForm();
                vm.Panel = 2; Layout();
                Assert.Equal("Saved locally", ((TextBlock)view.FindName("NoteSaveStatus")).Text);
                Assert.Equal(Visibility.Visible, ((FrameworkElement)view.FindName("NoteSavedCheck")).Visibility); Render("saved-content");
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
                foreach (var filter in filters)
                {
                    var action = Descendants<Button>((Grid)filter.Parent).Single();
                    Centered(filter, action); Bounds(filter); Bounds(action);
                    Assert.True(filter.TranslatePoint(new(filter.ActualWidth, 0), view).X <= action.TranslatePoint(new(), view).X);
                }
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
            void Layout() { view.Measure(new(width, Height())); view.Arrange(new Rect(0, 0, width, Height())); view.UpdateLayout(); Assert.Null(PresentationSource.FromVisual(view)); }
            void Centered(FrameworkElement left, FrameworkElement right) => Assert.InRange(Math.Abs(
                left.TranslatePoint(new(0, left.ActualHeight / 2), view).Y - right.TranslatePoint(new(0, right.ActualHeight / 2), view).Y), 0, 1);
            void Bounds(FrameworkElement element)
            {
                var p = element.TranslatePoint(new(), view);
                Assert.InRange(p.X, 0, width); Assert.InRange(p.X + element.ActualWidth, 0, width + 1);
            }
            void Render(string state)
            {
                var directory = Path.Combine(Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY") ?? AppContext.BaseDirectory, "notebook-tag-renders"); Directory.CreateDirectory(directory);
                var refreshIcon = Assert.IsType<System.Windows.Shapes.Path>(((Button)view.FindName("NotebookRefresh")).Content);
                string before = refreshIcon.Stroke?.ToString() ?? "null";
                // Finish deferred bindings before the offscreen snapshot, as in the A16 fixture.
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); view.UpdateLayout();
                File.AppendAllText(Path.Combine(directory, "render-readiness.log"), $"{theme}/{width}/{dpi}/{state}: Refresh stroke before={before}, after={refreshIcon.Stroke}\n");
                Assert.NotNull(refreshIcon.Stroke);
                var bitmap = new RenderTargetBitmap(width * dpi / 96, Height() * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
                bitmap.Render(view);
                var refresh = (Button)view.FindName("NotebookRefresh");
                var buttonBounds = refresh.TransformToAncestor(view).TransformBounds(new Rect(refresh.RenderSize));
                var iconBounds = refreshIcon.TransformToAncestor(view).TransformBounds(new Rect(refreshIcon.RenderSize));
                var organization = (Border)view.FindName("OrganizationPanel"); var scroll = Assert.IsType<ScrollViewer>(organization.Child);
                int foregroundPixels = 0;
                if (state is "default" or "refresh-busy" or "refresh-ready")
                {
                    Assert.Equal(0, vm.Panel); Assert.Equal(Visibility.Visible, organization.Visibility);
                    Assert.Equal(Visibility.Visible, refresh.Visibility); Assert.True(refresh.IsMeasureValid && refresh.IsArrangeValid);
                    Assert.InRange(buttonBounds.Left, 0, width); Assert.InRange(buttonBounds.Right, 0, width);
                    Assert.InRange(buttonBounds.Top, 0, Height()); Assert.InRange(buttonBounds.Bottom, 0, Height());
                    Assert.True(refresh.ActualWidth >= 36 && refresh.ActualHeight >= 36);
                    double scale = dpi / 96d; int stride = bitmap.PixelWidth * 4; var pixels = new byte[stride * bitmap.PixelHeight]; bitmap.CopyPixels(pixels, stride, 0);
                    var color = Assert.IsType<SolidColorBrush>(refreshIcon.Stroke).Color;
                    for (int y = (int)Math.Ceiling(iconBounds.Top * scale); y < Math.Floor(iconBounds.Bottom * scale); y++)
                        for (int x = (int)Math.Ceiling(iconBounds.Left * scale); x < Math.Floor(iconBounds.Right * scale); x++)
                        {
                            int offset = y * stride + x * 4;
                            if (Math.Abs(pixels[offset] - color.B) <= 8 && Math.Abs(pixels[offset + 1] - color.G) <= 8 && Math.Abs(pixels[offset + 2] - color.R) <= 8 && pixels[offset + 3] == 255) foregroundPixels++;
                        }
                    Assert.True(foregroundPixels >= 10, $"Refresh stroke must be present in the icon bounds: {theme}/{width}/{dpi}/{state}, pixels={foregroundPixels}.");
                }
                File.AppendAllText(Path.Combine(directory, "refresh-state.jsonl"), System.Text.Json.JsonSerializer.Serialize(new
                {
                    theme, width, dpi, state, Panel = vm.Panel, vm.IsBusy, Enabled = refresh.IsEnabled,
                    Visibility = refresh.Visibility.ToString(), EffectiveVisible = refresh.IsVisible, OffscreenRendering = PresentationSource.FromVisual(view) is null,
                    ButtonBounds = buttonBounds.ToString(CultureInfo.InvariantCulture), IconBounds = iconBounds.ToString(CultureInfo.InvariantCulture),
                    IconGeometry = refreshIcon.Data.ToString(CultureInfo.InvariantCulture), GeometryBounds = refreshIcon.Data.Bounds.ToString(CultureInfo.InvariantCulture),
                    RenderedGeometryBounds = refreshIcon.RenderedGeometry.Bounds.ToString(CultureInfo.InvariantCulture),
                    Foreground = refresh.Foreground.ToString(), Background = refresh.Background.ToString(), Stroke = refreshIcon.Stroke.ToString(),
                    ForegroundPixels = foregroundPixels, scroll.VerticalOffset, scroll.HorizontalOffset, scroll.ViewportWidth, scroll.ViewportHeight,
                    Clip = refreshIcon.Clip?.ToString(), LayoutClip = System.Windows.Controls.Primitives.LayoutInformation.GetLayoutClip(refreshIcon)?.ToString(),
                    MeasureValid = refreshIcon.IsMeasureValid, ArrangeValid = refreshIcon.IsArrangeValid
                }) + Environment.NewLine);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
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
