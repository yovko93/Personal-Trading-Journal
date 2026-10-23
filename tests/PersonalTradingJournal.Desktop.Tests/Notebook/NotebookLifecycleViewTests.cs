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
            using var vm=new NotebookWorkspaceViewModel(new NotebookFake(),dialogs,new NotebookChoices{Choice=NotebookLeaveChoice.Save},new NotebookJsonCodec());
            var view=new NotebookWorkspaceView{DataContext=vm,Width=width,Height=900};
            var window=new Window{Content=view,SizeToContent=SizeToContent.WidthAndHeight,ShowInTaskbar=false,ShowActivated=false};
            VisualTreeHelper.SetRootDpi(view,new(dpi/96d,dpi/96d));
            try
            {
                window.Show();Pump(vm.ActivateAsync());Layout();
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
                Click("ArchiveNoteButton");Select(NotebookScope.Archived);Assert.True(vm.ShowUnarchive);Assert.True(((RichTextBox)view.FindName("Preview")).IsReadOnly);Render("archived");
                Click("TrashNoteButton");Select(NotebookScope.Trash);Assert.True(vm.ShowRestore);Assert.True(((TextBox)view.FindName("TitleEditor")).IsReadOnly);Render("trash");
                Click("RestoreNoteButton");Select(NotebookScope.Archived);Assert.True(vm.ShowUnarchive);
                Click("UnarchiveNoteButton");Select(NotebookScope.AllNotes);Assert.True(vm.CanEditBody);Assert.False(((RichTextBox)view.FindName("Preview")).IsReadOnly);
                var original=vm.Note!.Id;Click("DuplicateNoteButton");Assert.NotEqual(original,vm.Note!.Id);Assert.Equal("Copy of Market Study",vm.Note.Title);Render("duplicated");
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
            void Click(string name){var button=(Button)view.FindName(name);Assert.True(button.IsVisible);Pump(UiKeyboardProbe.SpaceCommand(button));Layout();Assert.False(button.IsPressed);}
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
