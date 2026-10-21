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
            using var vm=new NotebookWorkspaceViewModel(new NotebookFake(),new FakeDialogService{ConfirmationResult=true},new NotebookChoices{Choice=NotebookLeaveChoice.Save},new NotebookJsonCodec());
            var view=new NotebookWorkspaceView{DataContext=vm,Width=width,Height=900};
            var window=new Window{Content=view,SizeToContent=SizeToContent.WidthAndHeight,ShowInTaskbar=false,ShowActivated=false};
            VisualTreeHelper.SetRootDpi(view,new(dpi/96d,dpi/96d));
            try
            {
                window.Show();Pump(vm.ActivateAsync());Layout();
                var picker=(ComboBox)view.FindName("TemplatePicker");Assert.Equal(5,picker.Items.Count);Assert.Equal("New note template",AutomationProperties.GetName(picker));
                Assert.Same(vm.NewNoteCommand,view.InputBindings.OfType<KeyBinding>().Single(x=>x.Key==Key.N&&x.Modifiers==ModifierKeys.Control).Command);
                picker.SelectedItem=vm.Templates.Single(x=>x.Id==NotebookTemplate.MarketStudy);Layout();
                Click("CreateTemplateButton");Assert.Equal("Market Study",vm.Note!.Title);Assert.Equal("Context\nObservation\nExamples\nConclusion\nOpen questions\n",vm.DraftDocument.PlainText);
                Assert.False(((TextBox)view.FindName("TitleEditor")).IsReadOnly);Render("template-active");
                Click("ArchiveNoteButton");Select(NotebookScope.Archived);Assert.True(vm.ShowUnarchive);Assert.True(((RichTextBox)view.FindName("Preview")).IsReadOnly);Render("archived");
                Click("TrashNoteButton");Select(NotebookScope.Trash);Assert.True(vm.ShowRestore);Assert.True(((TextBox)view.FindName("TitleEditor")).IsReadOnly);Render("trash");
                Click("RestoreNoteButton");Select(NotebookScope.Archived);Assert.True(vm.ShowUnarchive);
                Click("UnarchiveNoteButton");Select(NotebookScope.AllNotes);Assert.True(vm.CanEditBody);Assert.False(((RichTextBox)view.FindName("Preview")).IsReadOnly);
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
                    string directory=Path.Combine(Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY")!,"m2210a-renders");Directory.CreateDirectory(directory);
                    var image=new RenderTargetBitmap(width*dpi/96,900*dpi/96,dpi,dpi,PixelFormats.Pbgra32);image.Render(snapshot);
                    var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(Path.Combine(directory,$"{theme}-{width}-{dpi}-{state}.png"));encoder.Save(file);
                }
                finally {snapshot.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));}
                Assert.Same(document,vm.DraftDocument);Assert.Equal(revision,vm.EditorRevision);Assert.Equal(dirty,vm.IsDirty);
            }
        });
    }
    private static void Pump(Task task)
    {
        if(!task.IsCompleted){var frame=new DispatcherFrame();_=task.ContinueWith(_=>Dispatcher.CurrentDispatcher.BeginInvoke(()=>frame.Continue=false),CancellationToken.None,TaskContinuationOptions.None,TaskScheduler.FromCurrentSynchronizationContext());Dispatcher.PushFrame(frame);}
        task.GetAwaiter().GetResult();
    }
}
