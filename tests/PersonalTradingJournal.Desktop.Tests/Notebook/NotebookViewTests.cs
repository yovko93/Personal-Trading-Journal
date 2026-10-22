using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;
using PersonalTradingJournal.Desktop.Views.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

public sealed class NotebookViewTests
{
    private const string Host = "PTJ_NOTEBOOK_WORKSPACE_HOST";
    [Theory]
    [InlineData("Light", 1280, 96)]
    [InlineData("Dark", 1280, 96)]
    [InlineData("Light", 480, 240)]
    [InlineData("Dark", 480, 240)]
    public async Task CompiledWorkspaceProjectsSafeContentAndKeepsPanelsActionsReachable(string theme, int width, int dpi)
    {
        if (Environment.GetEnvironmentVariable(Host) != "1")
        {
            await IsolatedTestProcess.RunSuiteAsync(typeof(NotebookViewTests), "notebook-workspace", Host,
                TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30));
            return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            var app = System.Windows.Application.Current ?? new() { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
            var prior = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var data = new NotebookFake(); var folder = new NotebookNamedItem(Guid.NewGuid(), "Market research", 1); data.Folders.Add(folder);
            var longFolder = new NotebookNamedItem(Guid.NewGuid(), "Long folder name for market research and trading observations", 1); data.Folders.Add(longFolder);
            data.Notes.Add(NotebookFake.Note("NQ — Liquidity sweep study", folder.Id) with { IsPinned = true });
            data.Notes.Add(NotebookFake.Note("London range observations", folder.Id));
            data.Notes.Add(NotebookFake.Note("Weekly market themes", folder.Id));
            data.Notes.Add(NotebookFake.Note("Unfiled observation"));
            data.Notes.Add(NotebookFake.Note("Archived observation", state: Domain.Notebook.NotebookNoteState.Archived));
            data.Notes.Add(NotebookFake.Note("Trashed observation", state: Domain.Notebook.NotebookNoteState.Trashed));
            using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService());
            var view = new NotebookWorkspaceView { DataContext = vm };
            view.Resources.MergedDictionaries.Add(CalendarViewLayoutTests.SharedThemeResources(theme));
            VisualTreeHelper.SetRootDpi(view, new(dpi / 96d, dpi / 96d));
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            try
            {
                Pump(vm.ActivateAsync()); Layout();
                var workspace = (ListBox)view.FindName("ScopeList");
                var folders = (ListBox)view.FindName("FolderScopeList");
                var overflow = (Button)view.FindName("NotebookOverflow");
                var help = (Expander)view.FindName("ScopeHelp");
                Assert.Equal(new[] { NotebookScope.AllNotes, NotebookScope.Pinned }, workspace.Items.Cast<NotebookScopeOption>().Select(s => s.Scope));
                Assert.Equal(new[] { folder.Id, longFolder.Id }, folders.Items.Cast<NotebookScopeOption>().Select(s => s.FolderId!.Value));
                Assert.Same(vm.Scope, workspace.SelectedItem); Assert.Null(folders.SelectedItem);
                Assert.Equal(4, ((NotebookScopeOption)workspace.Items[0]).Count);
                Assert.Equal(1, ((NotebookScopeOption)workspace.Items[1]).Count);
                Assert.Equal(3, ((NotebookScopeOption)folders.Items[0]).Count);
                Assert.False(help.IsExpanded);
                Assert.Contains("All selected tags are required.", ((TextBlock)help.Content).Text);
                Assert.Equal("More Notebook views", AutomationProperties.GetName(overflow));
                Assert.True(overflow.Focusable); Assert.NotNull(overflow.ToolTip);
                vm.Panel = 0; Layout(); Render("navigation");
                foreach (var (list, index, icon) in new[] { (workspace, 0, "PtjIconDocument"), (workspace, 1, "PtjIconPin"), (folders, 0, "PtjIconFolder") })
                {
                    var row = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(index);
                    Assert.Equal(((NotebookScopeOption)list.Items[index]).Label, AutomationProperties.GetName(row));
                    Assert.Equal(((Geometry)view.FindResource(icon)).ToString(), Descendants<System.Windows.Shapes.Path>(row).Single().Data.ToString());
                }
                var longRow = (ListBoxItem)folders.ItemContainerGenerator.ContainerFromIndex(1);
                Assert.Equal(TextWrapping.Wrap, Descendants<TextBlock>(longRow).Single(t => t.Text == longFolder.Name).TextWrapping);
                help.IsExpanded = true; Layout(); Render("count-help");
                // This fixture renders a detached visual: IsVisible requires a presentation source.
                Assert.Contains((TextBlock)help.Content, Descendants<TextBlock>(help));
                Assert.Equal(Visibility.Visible, ((UIElement)VisualTreeHelper.GetParent((TextBlock)help.Content)).Visibility);
                Assert.True(((TextBlock)help.Content).ActualHeight > 0); help.IsExpanded = false;
                workspace.SetCurrentValue(ListBox.SelectedItemProperty, workspace.Items[1]); Pump(vm.LoadTask); Layout();
                Assert.Equal(NotebookScope.Pinned, vm.Scope!.Scope); Assert.Single(vm.Notes); Assert.Equal("Pinned", vm.ScopeName);
                foreach (var scope in new[] { NotebookScope.Unfiled, NotebookScope.Archived, NotebookScope.Trash })
                {
                    vm.Panel = 0; Layout();
                    overflow.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    var menu = overflow.ContextMenu!;
                    try
                    {
                        Assert.True(menu.IsOpen);
                        Assert.Same(overflow, menu.PlacementTarget);
                        // Verify the opener, then render the menu detached like the workspace.
                        // There is no native owner window to retain popup focus in this fixture.
                        menu.IsOpen = false;
                        menu.Measure(new Size(300, double.PositiveInfinity));
                        menu.Arrange(new Rect(menu.DesiredSize)); menu.UpdateLayout();
                        Assert.Equal(new[] { NotebookScope.Unfiled, NotebookScope.Archived, NotebookScope.Trash }, menu.Items.Cast<NotebookScopeOption>().Select(s => s.Scope));
                        if (scope == NotebookScope.Unfiled) Render("overflow", menu);
                        var option = menu.Items.Cast<NotebookScopeOption>().Single(s => s.Scope == scope);
                        var item = (MenuItem)menu.ItemContainerGenerator.ContainerFromItem(option);
                        Assert.Equal(option.Label, item.Header); Assert.True(item.Focusable); Assert.True(item.IsEnabled);
                        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Pump(vm.LoadTask); Layout();
                        Assert.Equal(scope, vm.Scope!.Scope); Assert.Equal(scope, data.Queries.Last().Scope);
                        Assert.Equal(option.Name, vm.ScopeName); Assert.Single(vm.Notes);
                        Assert.Null(workspace.SelectedItem); Assert.Null(folders.SelectedItem);
                    }
                    finally { menu.IsOpen = false; }
                }
                folders.SetCurrentValue(ListBox.SelectedItemProperty, folders.Items[0]); Pump(vm.LoadTask); Layout();
                Assert.Equal(folder.Id, vm.Scope!.FolderId); Assert.Equal(folder.Name, vm.ScopeName);
                Assert.Same(vm.Scope, folders.SelectedItem); Assert.Null(workspace.SelectedItem);
                Pump(vm.RefreshCommand.ExecuteAsync(null)); Layout();
                Assert.Same(vm.Scope, folders.SelectedItem); Assert.Equal(folder.Id, vm.Scope!.FolderId);
                vm.SelectedNote = vm.Notes.Single(n => n.Title.StartsWith("NQ")); Pump(vm.LoadTask); Layout();
                var savedTitle = vm.DraftTitle; vm.DraftTitle = "Retained dirty draft";
                workspace.SetCurrentValue(ListBox.SelectedItemProperty, workspace.Items[0]); Pump(vm.LoadTask); Layout();
                Assert.Equal(folder.Id, vm.Scope!.FolderId); Assert.Same(vm.Scope, folders.SelectedItem); Assert.Null(workspace.SelectedItem);
                Assert.Equal("Retained dirty draft", vm.DraftTitle); Assert.True(vm.IsDirty);
                vm.DraftTitle = savedTitle;
                var preview = (RichTextBox)view.FindName("Preview");
                string text = new TextRange(preview.Document.ContentStart, preview.Document.ContentEnd).Text;
                Assert.Contains("Observation", text); Assert.Contains("Checked", text);
                Assert.Single(NotebookEditorAdapter.Read(preview.Document).ImageNodes);
                Assert.False(preview.IsReadOnly); Assert.NotNull(new RichTextBoxAutomationPeer(preview).GetPattern(PatternInterface.Text));
                Assert.Equal("Note content", AutomationProperties.GetName(preview));
                Assert.Equal(0, data.Writes);
                Assert.Same(vm.SelectedNote, ((ListBox)view.FindName("NoteList")).SelectedItem);
                Assert.Equal(width < 1000 ? Visibility.Collapsed : Visibility.Visible, ((Border)view.FindName("OrganizationPanel")).Visibility);
                Render("content");
                if (width < 1000)
                {
                    foreach (string panel in new[] { "Organization", "Notes", "Content" })
                    {
                        Descendants<Button>(view).Single(b => Equals(b.Content, panel)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Layout(); Assert.Equal(Visibility.Visible, ((Border)view.FindName(panel == "Organization" ? "OrganizationPanel" : panel == "Notes" ? "NotesPanel" : "ContentPanel")).Visibility);
                        Render(panel.ToLowerInvariant());
                    }
                }
                foreach (var button in Descendants<Button>(view).Where(b => b.IsVisible))
                {
                    var origin = button.TranslatePoint(new(0, 0), view);
                    Assert.InRange(origin.X, -1, width); Assert.InRange(origin.X + button.ActualWidth, 0, width + 1);
                    Assert.True(button.Focusable);
                }
                data.OnLoad = (_, _) => Task.FromResult(new NotebookLoadResult(NotebookLoadStatus.UnsupportedContent, OriginalContent: "untouched"));
                Pump(vm.RefreshCommand.ExecuteAsync(null)); Layout();
                Assert.Contains("newer application", vm.ContentMessage); Assert.Null(vm.Note); Render("unavailable");
                data.OnList = (_, _) => throw new IOException("private source");
                Pump(vm.RefreshCommand.ExecuteAsync(null)); Layout(); Assert.DoesNotContain("private", vm.Message); Render("error");
                var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var pending = new TaskCompletionSource<NotebookPage>(TaskCreationOptions.RunContinuationsAsynchronously);
                data.OnList = (_, _) => { entered.TrySetResult(); return pending.Task; };
                var loading = vm.RefreshCommand.ExecuteAsync(null);
                try
                {
                    Pump(entered.Task); Layout(); Assert.True(vm.IsBusy); Render("loading");
                }
                finally { pending.TrySetResult(data.Page(new())); Pump(loading); }
                data.OnList = null; data.Notes.Clear(); Pump(vm.RefreshCommand.ExecuteAsync(null)); Layout();
                Assert.Empty(vm.Notes); Assert.Contains("No notes", vm.EmptyMessage); Render("empty");
                Assert.Equal(0, data.Writes);
            }
            finally { vm.Deactivate(); Pump(vm.DeactivationTask); view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); SynchronizationContext.SetSynchronizationContext(prior); }
            void Layout() { view.Measure(new(width, 800)); view.Arrange(new Rect(0, 0, width, 800)); view.UpdateLayout(); }
            void Render(string state, FrameworkElement? target = null)
            {
                var directory = Path.Combine(AppContext.BaseDirectory, "notebook-renders"); Directory.CreateDirectory(directory);
                var bitmap = new RenderTargetBitmap((int)Math.Ceiling((target?.ActualWidth ?? width) * dpi / 96), (int)Math.Ceiling((target?.ActualHeight ?? 800) * dpi / 96), dpi, dpi, PixelFormats.Pbgra32);
                bitmap.Render(target ?? view); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
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
            var child = VisualTreeHelper.GetChild(parent, i); if (child is T found) yield return found;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
}
