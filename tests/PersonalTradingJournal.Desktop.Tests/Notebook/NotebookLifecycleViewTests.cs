using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;
using PersonalTradingJournal.Desktop.Views.Notebook;
using PersonalTradingJournal.Infrastructure.Notebook;
using PersonalTradingJournal.Desktop.Dialogs;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

[Collection(ExclusiveWpfCollection.Name)]
[Trait(ExclusiveWpfCollection.Trait, ExclusiveWpfCollection.Value)]
public sealed class NotebookLifecycleViewTests
{
    private const string Host="PTJ_NOTEBOOK_LIFECYCLE_HOST";
    [Theory]
    [InlineData("Light",1280,96)] [InlineData("Dark",1280,96)] [InlineData("Light",480,240)] [InlineData("Dark",480,240)]
    public async Task TemplateAndReversibleLifecycleControls(string theme,int width,int dpi)
    {
        if(Environment.GetEnvironmentVariable(Host)!="1")
        {
            string name=string.Create(CultureInfo.InvariantCulture,$"{typeof(NotebookLifecycleViewTests).FullName}.{nameof(TemplateAndReversibleLifecycleControls)}(theme: \"{theme}\", width: {width}, dpi: {dpi})");
            await IsolatedTestProcess.RunSuiteAsync(typeof(NotebookLifecycleViewTests),"notebook-lifecycle",Host,TimeSpan.FromSeconds(90),
                caseHangTimeout:TimeSpan.FromSeconds(30),testCaseFilter:IsolatedTestProcess.ExactDisplayNameFilter(name),expectedTestName:name);return;
        }
        await CalendarStaTest.RunAsync(()=>
        {
            Assert.Null(System.Windows.Application.Current);
            var app=new System.Windows.Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};app.Resources=CalendarViewLayoutTests.SharedThemeResources(theme);
            var previous=SynchronizationContext.Current;SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var dialogs=new FakeDialogService{ConfirmationResult=true};
            var data=new NotebookFake(); var folder=new NotebookNamedItem(Guid.NewGuid(),"Research folder",1);data.Folders.Add(folder);
            using var vm=new NotebookWorkspaceViewModel(data,dialogs,new NotebookChoices{Choice=NotebookLeaveChoice.Save},new NotebookJsonCodec());
            var view=new NotebookWorkspaceView{DataContext=vm,Width=width,Height=900};
            var window=new Window{Content=view,SizeToContent=SizeToContent.WidthAndHeight,ShowInTaskbar=false,ShowActivated=false};
            VisualTreeHelper.SetRootDpi(view,new(dpi/96d,dpi/96d));
            try
            {
                window.Show();Pump(vm.ActivateAsync());vm.Panel=2;Layout();
                Assert.False(((Grid)view.FindName("NoteEditorContent")).IsVisible);Assert.True(((TextBlock)view.FindName("NoteSelectionPrompt")).IsVisible);Render("empty");
                var templates=(Button)view.FindName("NewNoteMenuButton");Assert.Equal("New note templates",AutomationProperties.GetName(templates));
                Assert.Same(vm.NewNoteCommand,view.InputBindings.OfType<KeyBinding>().Single(x=>x.Key==Key.N&&x.Modifiers==ModifierKeys.Control).Command);
                UiKeyboardProbe.Space(templates);Layout();Assert.True(templates.ContextMenu!.IsOpen);Assert.Equal(5,templates.ContextMenu.Items.Count);
                var definition=vm.Templates.Single(x=>x.Id==NotebookTemplate.MarketStudy);
                var item=(MenuItem)templates.ContextMenu.ItemContainerGenerator.ContainerFromItem(definition);
                Assert.True(item.Focus());Assert.Same(item,Keyboard.FocusedElement);
                item.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(item),Environment.TickCount,Key.Enter){RoutedEvent=Keyboard.KeyDownEvent});
                Layout();Pump(vm.CreateTemplateCommand.ExecutionTask!);Layout();Assert.False(templates.ContextMenu.IsOpen);
                Assert.Equal("Market Study",vm.Note!.Title);Assert.Equal("Context\nObservation\nExamples\nConclusion\nOpen questions\n",vm.DraftDocument.PlainText);
                Assert.False(((TextBox)view.FindName("TitleEditor")).IsReadOnly);Render("template-active");
                var pin=(Button)view.FindName("PinNoteButton");Assert.Equal("Pin note",AutomationProperties.GetName(pin));
                Pump(UiKeyboardProbe.SpaceCommand(pin));Layout();Assert.True(vm.Note.IsPinned);Assert.Equal("Unpin",AutomationProperties.GetName(pin));
                data.NextWrite=NotebookWriteStatus.StorageFailure;Pump(UiKeyboardProbe.SpaceCommand(pin));Layout();Assert.True(vm.Note.IsPinned);Assert.Equal("Unpin",AutomationProperties.GetName(pin));
                data.NextWrite=NotebookWriteStatus.Success;Pump(UiKeyboardProbe.SpaceCommand(pin));Layout();Assert.False(vm.Note.IsPinned);
                var title=(TextBox)view.FindName("TitleEditor");title.Text="A long research title with detailed observations and several scenarios to review before the next trading session";Layout();
                Assert.True(vm.IsDirty);Assert.Equal(title.Text,vm.DraftTitle);Assert.Equal("Unsaved changes",vm.CompactSaveState);Render("long-title");
                data.NextWrite=NotebookWriteStatus.StorageFailure;Click("SaveNoteMenuItem");Assert.True(vm.IsDirty);Assert.NotEmpty(vm.SaveState);Render("save-error");
                Assert.True(((StackPanel)view.FindName("DraftActions")).IsVisible);data.NextWrite=NotebookWriteStatus.Success;
                Click("DiscardNoteMenuItem");Assert.False(vm.IsDirty);Assert.Equal("Market Study",vm.DraftTitle);
                var overflow=(Button)view.FindName("NoteOverflow");UiKeyboardProbe.Space(overflow);Layout();var menu=overflow.ContextMenu!;
                Assert.True(menu.IsOpen);Assert.Equal(0,menu.Items.OfType<MenuItem>().Count(i=>i.Visibility==Visibility.Visible&&i.Name=="DeleteNoteButton"));
                Save(menu,"note-menu",menu.ActualWidth,menu.ActualHeight);int beforeDismiss=data.Writes;
                PressKey(menu,Key.Escape);Layout();Assert.False(menu.IsOpen);Assert.Equal(beforeDismiss,data.Writes);
                Click("MoveNoteMenuItem");Assert.True(((Border)view.FindName("MoveNotePicker")).IsVisible);
                vm.MoveFolder=vm.Folders.Single(f=>f.Id==folder.Id);UiKeyboardProbe.Space((Button)view.FindName("ConfirmMoveNote"));Pump(vm.MoveCommand.ExecutionTask!);Layout();
                Assert.Equal(folder.Id,vm.Note!.FolderId);Assert.Equal(Visibility.Collapsed,((Border)view.FindName("MoveNotePicker")).Visibility);
                // A retained popup command cannot act on a new selection (or a later return).
                UiKeyboardProbe.Space(overflow);Layout();var stale=((MenuItem)view.FindName("TrashNoteButton")).Command!;
                var first=vm.Note.Id;data.Notes.Add(NotebookFake.Note("Other"));Pump(vm.RefreshCommand.ExecuteAsync(null));
                vm.SelectedNote=vm.Notes.Single(n=>n.Id!=first);Pump(vm.LoadTask);Layout();int writes=data.Writes;
                Assert.False(stale.CanExecute(null));stale.Execute(null);Assert.Equal(writes,data.Writes);Assert.False(menu.IsOpen);
                vm.SelectedNote=vm.Notes.Single(n=>n.Id==first);Pump(vm.LoadTask);Layout();Assert.False(stale.CanExecute(null));
                Click("MoveNoteMenuItem");vm.SelectedNote=vm.Notes.Single(n=>n.Id!=first);Pump(vm.LoadTask);Layout();Assert.Equal(Visibility.Collapsed,((Border)view.FindName("MoveNotePicker")).Visibility);
                vm.SelectedNote=vm.Notes.Single(n=>n.Id==first);Pump(vm.LoadTask);data.Notes.RemoveAll(n=>n.Id!=first);Pump(vm.RefreshCommand.ExecuteAsync(null));Layout();
                // Seed an existing archived record; the retired Archive UI must not be needed to read it.
                var legacy=data.Notes.Single() with{State=NotebookNoteState.Archived,FolderId=null,Version=vm.Note!.Version+1};data.Notes[0]=legacy;
                Pump(vm.RefreshCommand.ExecuteAsync(null));Select(NotebookScope.AllNotes);
                Assert.Null(view.FindName("ArchiveNoteButton"));Assert.Null(view.FindName("UnarchiveNoteButton"));
                Assert.True(((RichTextBox)view.FindName("Preview")).IsReadOnly);Assert.False(pin.IsEnabled);
                Assert.Equal("No folder",vm.Breadcrumb);Assert.Contains("Duplicate",((TextBlock)view.FindName("LegacyReadOnlyNotice")).Text);Render("legacy-folderless");
                UiKeyboardProbe.Space(overflow);Layout();Assert.True(menu.IsOpen);
                Assert.False(((MenuItem)view.FindName("MoveNoteMenuItem")).IsEnabled);Assert.True(((MenuItem)view.FindName("DuplicateNoteButton")).IsEnabled);
                Save(menu,"legacy-note-menu",menu.ActualWidth,menu.ActualHeight);PressKey(menu,Key.Escape);Layout();
                Click("TrashNoteButton");Select(NotebookScope.Trash);Assert.True(vm.ShowRestore);Assert.True(((TextBox)view.FindName("TitleEditor")).IsReadOnly);Render("trash");
                Click("RestoreNoteButton");Select(NotebookScope.AllNotes);Assert.Equal(NotebookNoteState.Archived,vm.Note!.State);Assert.False(vm.CanEditBody);
                Assert.Equal(legacy.Document.PlainText,vm.Note.Document.PlainText);Assert.Equal(legacy.TagIds,vm.Note.TagIds);Assert.Equal(legacy.Links,vm.Note.Links);Assert.Equal(legacy.Images,vm.Note.Images);
                var original=vm.Note!.Id;Click("DuplicateNoteButton");Assert.NotEqual(original,vm.Note!.Id);Assert.Equal("Copy of Market Study",vm.Note.Title);Assert.True(vm.CanEditBody);Assert.False(((RichTextBox)view.FindName("Preview")).IsReadOnly);Render("duplicated");
                Click("TrashNoteButton");Select(NotebookScope.Trash);Render("delete-ready");
                dialogs.ConfirmationResult=false;Click("DeleteNoteButton");Assert.NotNull(vm.Note);Assert.Contains(vm.Note.Title,dialogs.ConfirmationRequest!.Message);
                var dialog=new PtjDialogWindow(dialogs.ConfirmationRequest);
                Assert.True(((Button)dialog.FindName("CancelButton")).IsDefault);
                var surface=(FrameworkElement)dialog.Content;surface.Margin=new Thickness(0);VisualTreeHelper.SetRootDpi(surface,new(dpi/96d,dpi/96d));
                surface.Measure(new Size(Math.Min(width,440),600));surface.Arrange(new Rect(0,0,Math.Min(width,440),surface.DesiredSize.Height));surface.UpdateLayout();
                Save(surface,"delete-confirmation",Math.Min(width,440),surface.ActualHeight);dialog.Close();
                dialogs.ConfirmationResult=true;Click("DeleteNoteButton");Assert.Null(vm.Note);Assert.Empty(vm.DraftDocument.Blocks);Assert.Empty(vm.Notes);Assert.False(((Button)view.FindName("UndoButton")).IsEnabled);
                Assert.False(app.Dispatcher.HasShutdownStarted);
            }
            finally { window.Close();vm.Deactivate();Pump(vm.DeactivationTask);app.Shutdown();SynchronizationContext.SetSynchronizationContext(previous); }
            void Layout(){view.UpdateLayout();Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle);}
            void PressKey(UIElement target,Key key){target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(target),Environment.TickCount,key){RoutedEvent=Keyboard.KeyDownEvent});}
            void Click(string name)
            {
                var overflow=(Button)view.FindName("NoteOverflow");UiKeyboardProbe.Space(overflow);Layout();
                var item=(MenuItem)view.FindName(name);Assert.True(item.IsVisible);Assert.True(item.IsEnabled);Assert.True(item.Focus());Assert.Same(item,Keyboard.FocusedElement);
                PressKey(item,Key.Enter);Layout();Pump(vm.CompletionTask);Layout();Assert.False(overflow.ContextMenu!.IsOpen);
            }
            void Select(NotebookScope scope){vm.Scope=vm.Scopes.Single(x=>x.Scope==scope);Pump(vm.LoadTask);vm.SelectedNote=Assert.Single(vm.Notes);Pump(vm.LoadTask);vm.Panel=2;Layout();}
            void Render(string state)
            {
                // Native assertions use the live window. A separate render surface avoids its
                // desktop-sized layout clip without detaching or resetting the live editor.
                var document=vm.DraftDocument;var revision=vm.EditorRevision;var dirty=vm.IsDirty;
                var snapshot=new NotebookWorkspaceView{DataContext=vm};
                VisualTreeHelper.SetRootDpi(snapshot,new(dpi/96d,dpi/96d));
                snapshot.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                try
                {
                    snapshot.Measure(new Size(width,900));snapshot.Arrange(new Rect(0,0,width,900));snapshot.UpdateLayout();
                    Save(snapshot,state,width,900);
                }
                finally {snapshot.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));}
                Assert.Same(document,vm.DraftDocument);Assert.Equal(revision,vm.EditorRevision);Assert.Equal(dirty,vm.IsDirty);
            }
            void Save(Visual surface,string state,double w,double h)
            {
                string directory=Path.Combine(Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY")!,"m2210b-renders");Directory.CreateDirectory(directory);
                var image=new RenderTargetBitmap((int)Math.Ceiling(w*dpi/96),(int)Math.Ceiling(h*dpi/96),dpi,dpi,PixelFormats.Pbgra32);image.Render(surface);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(Path.Combine(directory,$"{theme}-{width}-{dpi}-{state}.png"));encoder.Save(file);
            }
        });
    }
    private static void Pump(Task task)
    {
        if(!task.IsCompleted){var frame=new DispatcherFrame();_=task.ContinueWith(_=>Dispatcher.CurrentDispatcher.BeginInvoke(()=>frame.Continue=false),CancellationToken.None,TaskContinuationOptions.None,TaskScheduler.FromCurrentSynchronizationContext());Dispatcher.PushFrame(frame);}
        task.GetAwaiter().GetResult();
    }
}
