using System.Collections.Immutable;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Notebook;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;
using PersonalTradingJournal.Desktop.Views.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

[Collection(ExclusiveWpfCollection.Name)]
[Trait(ExclusiveWpfCollection.Trait, ExclusiveWpfCollection.Value)]
public sealed class NotebookPresentationViewTests
{
    private const string Host = "PTJ_NOTEBOOK_PRESENTATION_HOST";
    [Theory]
    [InlineData("Light", 1280, 96)]
    [InlineData("Dark", 1280, 96)]
    [InlineData("Light", 480, 240)]
    [InlineData("Dark", 480, 240)]
    public async Task CompactToolbarChipsAndPopulatedRowsPreserveEditing(string theme, int width, int dpi)
    {
        if (Environment.GetEnvironmentVariable(Host) != "1")
        {
            string expected = string.Create(CultureInfo.InvariantCulture,
                $"{typeof(NotebookPresentationViewTests).FullName}.{nameof(CompactToolbarChipsAndPopulatedRowsPreserveEditing)}(theme: \"{theme}\", width: {width}, dpi: {dpi})");
            await IsolatedTestProcess.RunSuiteAsync(typeof(NotebookPresentationViewTests), "notebook-presentation", Host,
                TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30),
                testCaseFilter: IsolatedTestProcess.ExactDisplayNameFilter(expected), expectedTestName: expected);
            return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
            var prior = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var codec = new NotebookJsonCodec(); var data = new NotebookFake();
            var folder = new NotebookNamedItem(Guid.NewGuid(), "Market research", 1); data.Folders.Add(folder);
            data.Tags.AddRange([new(Guid.NewGuid(), "NQ", 1), new(Guid.NewGuid(), "ICT", 1), new(Guid.NewGuid(), "Long research observations and patient confirmation", 1)]);
            var image = NotebookNode.Image(Guid.NewGuid(), Guid.NewGuid(), "Illustrative chart · NQ, 5m", "Synthetic chart");
            var doc = new NotebookDocument([
                NotebookNode.Heading(Guid.NewGuid(), 2, [NotebookNode.Run(Guid.NewGuid(), "Observation")]),
                NotebookNode.Paragraph(Guid.NewGuid(), [NotebookNode.Run(Guid.NewGuid(), "A sweep"), NotebookNode.Run(Guid.NewGuid(), " of the session low becomes more useful when followed by clear displacement and a return to the imbalance.")]),
                NotebookNode.Heading(Guid.NewGuid(), 2, [NotebookNode.Run(Guid.NewGuid(), "What I want to review")]),
                NotebookNode.List(Guid.NewGuid(), NotebookNodeKind.Checklist, [NotebookNode.Item(Guid.NewGuid(), [NotebookNode.Paragraph(Guid.NewGuid(), [NotebookNode.Run(Guid.NewGuid(), "Mark the liquidity level before the session")])], false)]), image]);
            var note = NotebookFake.Note("NQ — Liquidity sweep study", folder.Id) with { Document = doc, IsPinned = true,
                CreatedAtUtc = new(2023, 1, 1, 12, 0, 0, TimeSpan.Zero), UpdatedAtUtc = new(2024, 10, 5, 3, 30, 0, TimeSpan.Zero),
                TagIds = [.. data.Tags.Select(t => t.Id)], Images = [NotebookImage.Owned(image.ImageId!.Value, 0, "chart.png", 1, new string('A', 64), image.Caption, image.AlternativeText)],
                Links = [new(NotebookTargetKind.Trade, Guid.NewGuid(), "Trade #057"), new(NotebookTargetKind.TradingSetup, Guid.NewGuid(), "Setup · Liquidity sweep")] };
            data.Notes.Add(note);
            data.OnResolve = (links, _) => Task.FromResult<ImmutableArray<NotebookResolvedLink>>([.. links.Select(l => new NotebookResolvedLink(l, NotebookTargetAvailability.Available, l.LastKnownLabel))]);
            using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService(), new NotebookChoices { Choice = NotebookLeaveChoice.Save }, codec,
                images: new NotebookImageEditorTests.Imports(data), imagePresentation: new NotebookImageEditorTests.Presentation(image.ImageId.Value));
            var view = new NotebookWorkspaceView { DataContext = vm, Width = width, Height = 1050, ImageInput = new NotebookImageEditorTests.Input() };
            var window = new Window { Content = view, SizeToContent = SizeToContent.WidthAndHeight, ShowActivated = false, ShowInTaskbar = false };
            try
            {
                window.Show(); Pump(vm.ActivateAsync()); vm.SelectedNote = vm.Notes[0]; Pump(vm.LoadTask); Pump(vm.EditModeCommand.ExecuteAsync(null)); Pump(view.ImageLoadsTask); vm.Panel = 2; Layout();
                var editor = (RichTextBox)view.FindName("Preview");
                Assert.Equal("Oct 04, 2024", vm.Notes[0].Updated);
                Assert.Equal("Active · Updated Oct 04, 2024", vm.Metadata);
                Assert.Equal(2, vm.Notes[0].TagChips.Count()); Assert.Equal("+1", vm.Notes[0].RemainingTags);
                Render("populated"); Render("linked-items"); vm.Panel = 1; Layout(); Render("rows"); vm.Panel = 2; Layout();
                // The same note supplies the organization and content viewports; no composite.
                vm.Panel = 0; Layout(); Render("organization");
                Assert.True(vm.BeginFolderCreation()); Assert.True(vm.BeginTagCreation()); Layout(); Render("creation-empty");
                vm.FolderName = "Session research"; vm.TagName = "Patience"; Layout(); Render("creation-populated");
                vm.CancelFolderCreation(); vm.CancelTagForm(); vm.Panel = 2; Layout();
                foreach (var name in new[] { "BoldButton", "ItalicButton", "UnderlineButton", "BulletsButton", "NumberingButton", "ChecklistButton", "ToggleCheckButton", "HyperlinkButton", "InsertImageButton", "UndoButton", "RedoButton" })
                {
                    var button = Button(name); Assert.Equal(name is not ("UndoButton" or "RedoButton"), button.IsEnabled); Assert.True(button.Focusable);
                    Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(button))); Assert.NotNull(button.ToolTip);
                    var point = button.TranslatePoint(new(0, 0), view); Assert.InRange(point.X, 0, width); Assert.InRange(point.X + button.ActualWidth, 0, width + 1);
                }
                var body = (Paragraph)editor.Document.Blocks.ElementAt(1);
                editor.Selection.Select(body.Inlines.FirstInline!.ContentStart, body.Inlines.FirstInline.ContentEnd);
                UiKeyboardProbe.Space(Button("BoldButton")); Layout();
                Assert.Equal("A sweep", editor.Selection.Text); Assert.Equal(FontWeights.Bold, editor.Selection.GetPropertyValue(TextElement.FontWeightProperty));
                Assert.False(NotebookEditorAdapter.Read(editor.Document).Blocks[1].Children.Last().Marks.HasFlag(NotebookTextMarks.Bold));
                UiKeyboardProbe.Space(Button("UndoButton")); UiKeyboardProbe.Space(Button("RedoButton")); Assert.True(vm.IsDirty); UiKeyboardProbe.Space(Button("UndoButton"));
                foreach (var name in new[] { "ItalicButton", "UnderlineButton" })
                {
                    body = (Paragraph)editor.Document.Blocks.ElementAt(1);
                    editor.Selection.Select(body.Inlines.FirstInline!.ContentStart, body.Inlines.FirstInline.ContentEnd);
                    UiKeyboardProbe.Space(Button(name));
                    var marks = NotebookEditorAdapter.Read(editor.Document).Blocks[1].Children.First().Marks;
                    Assert.True(marks.HasFlag(name == "ItalicButton" ? NotebookTextMarks.Italic : NotebookTextMarks.Underline));
                    UiKeyboardProbe.Space(Button("UndoButton"));
                }
                foreach (var (name, kind) in new[] { ("BulletsButton", NotebookNodeKind.BulletedList), ("NumberingButton", NotebookNodeKind.NumberedList), ("ChecklistButton", NotebookNodeKind.Checklist) })
                {
                    body = (Paragraph)editor.Document.Blocks.ElementAt(1); editor.Selection.Select(body.ContentStart, body.ContentEnd);
                    UiKeyboardProbe.Space(Button(name)); Assert.Equal(kind, NotebookEditorAdapter.Read(editor.Document).Blocks[1].Kind);
                    UiKeyboardProbe.Space(Button("UndoButton")); Assert.Equal(NotebookNodeKind.Paragraph, NotebookEditorAdapter.Read(editor.Document).Blocks[1].Kind);
                }
                body = (Paragraph)editor.Document.Blocks.ElementAt(1);
                editor.Selection.Select(body.Inlines.FirstInline!.ContentStart, body.Inlines.FirstInline.ContentEnd);
                UiKeyboardProbe.Space(Button("HyperlinkButton")); Layout(); Assert.True(((Expander)view.FindName("LinkActions")).IsExpanded);
                ((TextBox)view.FindName("LinkUrl")).Text = "https://example.com/research";
                // Moving the live caret while the URL field owns focus must not retarget the captured range.
                editor.CaretPosition = editor.Document.ContentEnd;
                UiKeyboardProbe.Space(Descendants<Button>(view).Single(b => Equals(b.Content, "Apply link"))); Layout();
                var link = Assert.Single(((Paragraph)editor.Document.Blocks.ElementAt(1)).Inlines.OfType<Hyperlink>());
                Assert.Equal("A sweep", new TextRange(link.ContentStart, link.ContentEnd).Text); Assert.Equal("https://example.com/research", link.NavigateUri.AbsoluteUri);
                string linked = Json(); UiKeyboardProbe.Space(Descendants<Button>(view).Single(b => AutomationProperties.GetName(b) == "Dismiss hyperlink actions")); Assert.Equal(linked, Json());
                UiKeyboardProbe.Space(Button("UndoButton"));
                var checklist = (System.Windows.Documents.List)editor.Document.Blocks.ElementAt(3);
                editor.CaretPosition = checklist.ListItems.FirstListItem!.Blocks.FirstBlock!.ContentStart;
                UiKeyboardProbe.Space(Button("ToggleCheckButton"));
                var checkedList = NotebookEditorAdapter.Read(editor.Document).Blocks[3]; Assert.Single(checkedList.Children); Assert.True(checkedList.Children[0].IsChecked); UiKeyboardProbe.Space(Button("UndoButton"));
                string beforeImage = Json(); UiKeyboardProbe.Space(Button("InsertImageButton")); Pump(view.ImageOperationTask); Assert.Equal(beforeImage, Json());
                UiKeyboardProbe.Space(Button("AddTagButton")); Layout(); Assert.True(((Expander)view.FindName("TagAssignment")).IsExpanded); Render("tag-picker");
                var originalDocument = editor.Document;
                int globalCount = data.Tags.Count; var chip = Descendants<Button>((ItemsControl)view.FindName("AssignedTagList")).First();
                var removedTag = vm.AssignedTags.Single(t => ReferenceEquals(t, chip.DataContext));
                Pump(UiKeyboardProbe.SpaceCommand(chip)); Layout(); Assert.Equal(globalCount, data.Tags.Count); Assert.Equal(2, vm.AssignedTags.Count); Assert.Same(originalDocument, editor.Document);
                vm.AssignmentTag = vm.Tags.Single(t => t.Id == removedTag.Id);
                Pump(UiKeyboardProbe.SpaceCommand(Descendants<Button>(view).Single(b => Equals(b.Content, "Assign tag")))); Layout(); Assert.Equal(3, vm.AssignedTags.Count); Assert.Equal(globalCount, data.Tags.Count);
                Assert.Same(originalDocument, editor.Document);
                // A document replacement closes transient selection-owned panels.
                UiKeyboardProbe.Space(Button("HyperlinkButton"));
                data.Notes.Add(NotebookFake.Note("Another note")); Pump(vm.RefreshCommand.ExecuteAsync(null)); vm.SelectedNote = vm.Notes.Single(n => n.Title == "Another note"); Pump(vm.LoadTask); Layout();
                Assert.False(((Expander)view.FindName("LinkActions")).IsExpanded); Assert.False(((Expander)view.FindName("TagAssignment")).IsExpanded);
            }
            finally { window.Close(); vm.Deactivate(); Pump(vm.DeactivationTask); app.Shutdown(); SynchronizationContext.SetSynchronizationContext(prior); }
            Button Button(string name) => (Button)view.FindName(name);
            string Json() => codec.Encode(NotebookEditorAdapter.Read(((RichTextBox)view.FindName("Preview")).Document)).Json;
            void Layout() { view.UpdateLayout(); Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); }
            void Render(string state)
            {
                var snapshot = new NotebookWorkspaceView { DataContext = vm };
                VisualTreeHelper.SetRootDpi(snapshot, new(dpi / 96d, dpi / 96d)); snapshot.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                try
                {
                    Pump(snapshot.ImageLoadsTask); snapshot.Measure(new Size(width, 1100)); snapshot.Arrange(new Rect(0, 0, width, 1100)); snapshot.UpdateLayout();
                    if (state is "populated" or "rows")
                    {
                        string expectedDate = state == "rows" ? "Oct 04, 2024" : "Active · Updated Oct 04, 2024";
                        var label = Descendants<TextBlock>(snapshot).Single(t => t.Text == expectedDate);
                        var position = label.TranslatePoint(new Point(0, 0), snapshot);
                        Assert.InRange(position.X, 0, width); Assert.InRange(position.X + label.ActualWidth, 0, width + 1);
                        Assert.InRange(position.Y, 0, 1100); Assert.InRange(position.Y + label.ActualHeight, 0, 1100);
                    }
                    if (state == "tag-picker") { ((Expander)snapshot.FindName("TagAssignment")).IsExpanded = true; snapshot.UpdateLayout(); }
                    if (state == "linked-items")
                    {
                        var scroll = (ScrollViewer)snapshot.FindName("ContentScroll");
                        scroll.ScrollToBottom(); snapshot.UpdateLayout();
                        var items = (ItemsControl)snapshot.FindName("LinkedItemList");
                        Assert.Equal(2, items.Items.Count);
                        foreach (var item in items.Items)
                        {
                            var row = (FrameworkElement)items.ItemContainerGenerator.ContainerFromItem(item);
                            var at = row.TranslatePoint(new Point(0, 0), snapshot);
                            Assert.InRange(at.Y, 0, 1100); Assert.InRange(at.Y + row.ActualHeight, 0, 1100);
                        }
                    }
                    string directory = Path.Combine(Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY")!, "presentation-renders"); Directory.CreateDirectory(directory);
                    if (state == "populated")
                        NotebookOverflowEvidence.Verify((Button)snapshot.FindName("NoteOverflow"), snapshot, dpi, $"{theme}-{width}-{dpi}-note");
                    if (state == "organization")
                    {
                        Assert.Null(snapshot.FindName("NotebookOverflow")); Assert.NotNull(snapshot.FindName("NotebookRefresh"));
                        var tags = Descendants<Button>(snapshot).Where(b => AutomationProperties.GetName(b).StartsWith("Manage global tag:")).ToArray();
                        Assert.Equal(3, tags.Length);
                        for (int i = 0; i < tags.Length; i++)
                            NotebookOverflowEvidence.Verify(tags[i], snapshot, dpi, $"{theme}-{width}-{dpi}-tag-{i}");
                    }
                    var bitmap = new RenderTargetBitmap(width * dpi / 96, 1100 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32); bitmap.Render(snapshot);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(directory, $"{theme}-{width}-{dpi}-{state}.png")); encoder.Save(file);
                }
                finally { snapshot.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); }
            }
        });
    }
    private static void Pump(Task task)
    {
        if (!task.IsCompleted) { var frame = new DispatcherFrame(); _ = task.ContinueWith(_ => Dispatcher.CurrentDispatcher.BeginInvoke(() => frame.Continue = false), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext()); Dispatcher.PushFrame(frame); }
        task.GetAwaiter().GetResult();
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { var child = VisualTreeHelper.GetChild(parent, i); if (child is T match) yield return match; foreach (var nested in Descendants<T>(child)) yield return nested; }
    }
}
