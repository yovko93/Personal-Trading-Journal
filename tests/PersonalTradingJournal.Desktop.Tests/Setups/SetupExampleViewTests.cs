using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Desktop.Screenshots;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Setups;
using PersonalTradingJournal.Desktop.Views.Setups;
using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Desktop.Tests.Setups;

public sealed class SetupExampleViewTests
{
    [Fact]
    public async Task BoundGalleryAndRowActionsWorkAcrossThemesAndDpi()
    {
        const string host = "PTJ_SETUP_GALLERY_HOST";
        if (Environment.GetEnvironmentVariable(host) != "1")
        {
            await IsolatedTestProcess.RunSuiteAsync(typeof(SetupExampleViewTests), "setup-gallery", host,
                TimeSpan.FromSeconds(60), caseHangTimeout: TimeSpan.FromSeconds(30));
            return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (string theme in new[] { "Light", "Dark" })
            foreach (var (width, dpi) in new[] { (1000, 96), (460, 240) })
            {
                app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
                var fixture = new Images();
                var dialog = new FakeDialogService();
                var setup = new TradingSetupDetails(Guid.NewGuid(), "Opening range", "First pullback", true, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
                var reader = new FakeTradingSetupReader { DetailsToReturn = setup };
                TradingSetupListItem[] catalog = [new(setup.Id, setup.Name, setup.Description, true, setup.CreatedAtUtc, setup.UpdatedAtUtc),
                    new(Guid.NewGuid(), "Range reversal", null, false, setup.CreatedAtUtc, setup.UpdatedAtUtc)];
                reader.EnqueueResult(catalog);
                reader.EnqueueResult(catalog);
                var vm = TradingSetupsViewModelTests.Create(reader, dialog: dialog, examples: fixture, viewer: fixture);
                vm.EnsureLoadedAsync().GetAwaiter().GetResult();
                var view = new TradingSetupsView { DataContext = vm, Background = (Brush)app.Resources["PtjBackgroundBrush"] };
                VisualTreeHelper.SetRootDpi(view, new DpiScale(dpi / 96d, dpi / 96d));
                double height = 740;
                void Layout() { view.Measure(new Size(width, height)); view.Arrange(new Rect(0,0,width,height)); view.UpdateLayout(); }
                Layout();
                var headers = (Grid)view.FindName("SetupHeaders");
                Assert.Equal(new[] { "Name","Description","Status","Actions" }, headers.Children.OfType<TextBlock>().Select(t => t.Text));
                var scroller = (ScrollViewer)view.FindName("SetupTableScroll");
                Assert.Equal(width < 590, scroller.ScrollableWidth > 0);
                Render(view, theme, width, dpi, "table");
                scroller.ScrollToRightEnd(); Layout();
                var rows = Children(view).OfType<Grid>().Where(g => g.Name == "SetupRow").ToList();
                Assert.Equal(2, rows.Count);
                foreach (var row in rows)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        Assert.Equal(headers.ColumnDefinitions[i].ActualWidth, row.ColumnDefinitions[i].ActualWidth, 1);
                        Assert.InRange(Math.Abs(headers.TranslatePoint(new(headers.ColumnDefinitions[i].Offset,0), scroller).X -
                            row.TranslatePoint(new(row.ColumnDefinitions[i].Offset,0), scroller).X), 0, 1);
                    }
                    var buttons = Children(row).OfType<Button>().ToArray();
                    Assert.Equal(2, buttons.Length);
                    var viewButton = buttons[0]; var more = buttons[1];
                    Assert.Equal(new Size(78,36), viewButton.RenderSize);
                    Assert.Equal(viewButton.RenderSize, more.RenderSize);
                    Assert.Same(vm.ViewCommand, viewButton.Command);
                    Assert.Equal(viewButton.TranslatePoint(new(),row).Y, more.TranslatePoint(new(),row).Y, 2);
                    var label = Children(viewButton).OfType<TextBlock>().Single(x => x.Text == "View");
                    // DesiredSize includes the margin between the eye icon and the text.
                    Assert.True(label.ActualWidth > 0);
                    Assert.InRange(label.DesiredSize.Width - label.Margin.Left - label.Margin.Right, 0, label.ActualWidth + 0.01);
                    Assert.InRange(label.TranslatePoint(new(label.ActualWidth,0),viewButton).X, 0, viewButton.ActualWidth);
                    Assert.InRange(more.TranslatePoint(new(more.ActualWidth,0),scroller).X, 0, scroller.ViewportWidth + 1);
                    var menu = more.ContextMenu;
                    menu.PlacementTarget = more;
                    // Materialize the detached popup template before inspecting its item bindings.
                    menu.Measure(new Size(260, 300)); menu.Arrange(new Rect(menu.DesiredSize)); menu.UpdateLayout();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
                    Assert.Same(more, menu.DataContext);
                    var edit = menu.Items.OfType<MenuItem>().Single(x => Equals(x.Header,"Edit"));
                    Assert.Same(vm.EditCommand, edit.Command);
                    Assert.Equal(((TradingSetupListItem)row.DataContext).Id,edit.CommandParameter);
                    Assert.True(edit.Focusable && edit.Command.CanExecute(edit.CommandParameter));
                }

                vm.ShowCreateCommand.Execute(null);
                vm.NameText = "Unpublished";
                vm.StageExamplesCommand.ExecuteAsync(null).GetAwaiter().GetResult();
                Assert.Equal(2, vm.StagedExamples.Count);
                vm.RemoveStagedCommand.Execute(vm.StagedExamples[0]);
                Assert.Equal("Exit.png", Assert.Single(vm.StagedExamples).FileName);
                vm.CancelCreateCommand.Execute(null);
                Assert.Empty(vm.StagedExamples);
                Assert.Empty(fixture.Rows);
                vm.ShowCreateCommand.Execute(null);
                vm.NameText = "Range break";
                vm.StageExamplesCommand.ExecuteAsync(null).GetAwaiter().GetResult();
                vm.StagedExamples[0].Timeframe = "15 min";
                Layout();
                Render(view, theme, width, dpi, "create");
                fixture.FailSave = true;
                vm.CreateCommand.ExecuteAsync(null).GetAwaiter().GetResult();
                Assert.True(vm.IsCreateFormVisible);
                Assert.Equal(2, vm.StagedExamples.Count);
                Assert.Empty(fixture.Rows);
                fixture.FailSave = false;
                vm.CreateCommand.ExecuteAsync(null).GetAwaiter().GetResult();
                Assert.Empty(vm.StagedExamples);
                Assert.False(vm.IsCreateFormVisible);
                Assert.Equal(new string?[] { "15 min", null }, fixture.Created.Select(image => image.Timeframe));
                vm.ViewCommand.ExecuteAsync(setup.Id).GetAwaiter().GetResult();
                vm.UploadExampleCommand.ExecuteAsync(null).GetAwaiter().GetResult();
                Assert.Empty(fixture.Rows);
                Assert.False(vm.EditCommand.CanExecute(setup.Id));
                vm.CancelExampleCommand.Execute(null);
                Assert.Null(vm.PendingExample);
                Assert.Empty(vm.ExampleCards);
                vm.UploadExampleCommand.ExecuteAsync(null).GetAwaiter().GetResult();
                vm.ExampleTimeframe = "15 min";
                fixture.FailSave = true;
                vm.SaveExampleCommand.ExecuteAsync(null).GetAwaiter().GetResult();
                Assert.Empty(fixture.Rows);
                Assert.NotNull(vm.PendingExample);
                fixture.FailSave = false;
                vm.SaveExampleCommand.ExecuteAsync(null).GetAwaiter().GetResult();
                Layout();
                var card = Assert.Single(vm.ExampleCards);
                Assert.Equal(setup.Id, card.Example.SetupId);
                Assert.Equal("15 min", card.Example.Timeframe);
                var gallery = (ItemsControl)view.FindName("SetupExampleCards");
                Assert.Same(fixture.Thumbnail, Assert.Single(Children(gallery).OfType<Image>()).Source);
                var actions = Children(gallery).OfType<Button>().ToArray();
                Assert.Same(vm.ViewExampleCommand, actions[0].Command);
                Assert.Same(card, actions[0].CommandParameter);
                Assert.Same(vm.RemoveExampleCommand, actions[1].Command);
                const string longName = "Opening range reversal after the first confirmation on a long timeframe.png";
                var portrait = fixture.AddAsync(setup.Id, longName, [2], null, default).Result;
                var page = (ScrollViewer)view.FindName("PageScroll");
                height = 420; Layout(); page.ScrollToBottom(); Layout();
                Assert.True(page.VerticalOffset > 0);
                vm.ViewCommand.ExecuteAsync(setup.Id).GetAwaiter().GetResult(); Layout();
                Assert.Equal(0, page.VerticalOffset);
                var cards = Children(gallery).OfType<Button>().Where(b => b.Name == "OpenExampleCard").ToArray();
                Assert.Equal(2, cards.Length);
                var tiles = cards.Select(b => (StackPanel)b.Parent).ToArray();
                Assert.Equal(tiles[0].RenderSize, tiles[1].RenderSize);
                Assert.Equal(cards[0].RenderSize, cards[1].RenderSize);
                var frames = cards.Select(b => Children(b).OfType<Grid>().Single(g => g.Name == "ThumbnailArea")).ToArray();
                Assert.Equal(frames[0].RenderSize, frames[1].RenderSize);
                Assert.All(frames, g => Assert.Equal(Stretch.Uniform, ((Image)g.Children[0]).Stretch));
                var removes = tiles.Select(tile => (Button)tile.Children[1]).ToArray();
                Assert.Equal(removes[0].TranslatePoint(new(), tiles[0]).Y, removes[1].TranslatePoint(new(), tiles[1]).Y);
                Assert.Equal(longName, cards[1].ToolTip);
                var cardLabel = Children(cards[1]).OfType<TextBlock>().Single(label => label.Text == longName);
                Assert.Equal(TextTrimming.CharacterEllipsis, cardLabel.TextTrimming);
                foreach (int delta in new[] { -120, -24, 24, 120 })
                {
                    double before = page.VerticalOffset;
                    var wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, delta) { RoutedEvent = UIElement.PreviewMouseWheelEvent };
                    cards[1].RaiseEvent(wheel); Layout();
                    Assert.True(wheel.Handled);
                    Assert.True(delta < 0 ? page.VerticalOffset > before : page.VerticalOffset < before);
                }
                height = 740; Layout();
                Render(view, theme, width, dpi, "details");
                card = vm.ExampleCards[0];
                vm.ViewExampleCommand.ExecuteAsync(card).GetAwaiter().GetResult();
                Assert.Equal(card.Example.File, fixture.Opened);
                Assert.NotSame(card.Thumbnail, vm.Preview!.Original);
                Layout();
                Assert.Equal(Visibility.Visible, ((StackPanel)view.FindName("GalleryPanel")).Visibility);
                var inline = (StackPanel)view.FindName("InlineExample");
                Assert.True(inline.TranslatePoint(new(), view).Y >= gallery.TranslatePoint(new(0, gallery.ActualHeight), view).Y - 1);
                Assert.Equal("Close Preview", ((Button)view.FindName("BackToCards")).Content);
                Assert.Same(vm.Preview!.Original, ((Image)view.FindName("OriginalExample")).Source);
                Assert.Equal("Open Opening range.png, 15 min", System.Windows.Automation.AutomationProperties.GetName(actions[0]));
                Render(view, theme, width, dpi, "inline");
                vm.BackToCardsCommand.Execute(null); Layout();
                Assert.Null(vm.Preview);
                Assert.Equal(Visibility.Visible, ((StackPanel)view.FindName("GalleryPanel")).Visibility);
                vm.RemoveExampleCommand.ExecuteAsync(card).GetAwaiter().GetResult();
                Assert.True(dialog.ConfirmationRequest!.IsDestructive);
                Assert.Equal(2, vm.ExampleCards.Count);
                dialog.ConfirmationResult = true;
                vm.RemoveExampleCommand.ExecuteAsync(card).GetAwaiter().GetResult();
                Assert.Equal(portrait.Id, Assert.Single(fixture.Rows).Id);
                Assert.Equal(portrait.Id, Assert.Single(vm.ExampleCards).Example.Id);
                vm.EditCommand.ExecuteAsync(setup.Id).GetAwaiter().GetResult(); Layout();
                var editCards = Children(view).OfType<ItemsControl>().Single(ic => ic.Name == "StagedExampleCards" && ic.ActualWidth > 0 &&
                    ic.IsVisible == view.IsVisible && IsVisibleInTree(ic, view));
                var timeframeBox = Children(editCards).OfType<TextBox>().Single();
                timeframeBox.Text = "Daily";
                Assert.Equal("Daily", Assert.Single(vm.StagedExamples).Timeframe);
                vm.StageExamplesCommand.ExecuteAsync(null).GetAwaiter().GetResult(); Layout();
                Render(view, theme, width, dpi, "edit");
                vm.CancelEditCommand.Execute(null); Layout();
                Assert.Null(vm.SelectedTradingSetup);
                Assert.Null(Assert.Single(fixture.Rows).Timeframe);
                Assert.Empty(vm.StagedExamples);

            }
            app.Shutdown();
        });
    }

    private static bool IsVisibleInTree(DependencyObject element, FrameworkElement view)
    {
        while (element != view)
        {
            if (element is UIElement { Visibility: Visibility.Collapsed }) return false;
            element = VisualTreeHelper.GetParent(element);
        }
        return true;
    }

    internal static IEnumerable<DependencyObject> Children(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent,i); yield return child;
            foreach (var next in Children(child)) yield return next;
        }
    }

    private static void Render(FrameworkElement view, string theme, int width, int dpi, string state)
    {
        if (Environment.GetEnvironmentVariable("PTJ_SETUP_RENDER_DIRECTORY") is not { Length: > 0 } directory) return;
        Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap(width*dpi/96, 740*dpi/96, dpi,dpi,PixelFormats.Pbgra32);
        bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory,$"{theme}-{width}-{dpi}-{state}.png"));
        encoder.Save(stream);
    }

    internal sealed class Images : IWorkspaceImageViewer, ISetupExampleStore
    {
        internal ImageSource Thumbnail { get; } = new DrawingImage(new GeometryDrawing(Brushes.Teal, new Pen(Brushes.Navy, 1),
            Geometry.Parse("M0,18 L6,11 10,15 19,4 23,8 32,1 32,22 0,22 Z")));
        internal List<SetupExample> Rows { get; } = [];
        internal bool FailSave;
        internal PlaybookExampleImage? Opened;
        private readonly ImageSource _original = new DrawingImage(new GeometryDrawing(Brushes.Teal, new Pen(Brushes.Navy, 2),
            Geometry.Parse("M0,155 L98,88 165,121 298,35 385,67 578,4 578,176 0,176 Z")));
        public Task<PlanImageSelection?> PickAsync(string title, CancellationToken token) =>
            Task.FromResult<PlanImageSelection?>(new("Opening range.png", [1], Thumbnail));
        public Task<ImageSource> ThumbnailAsync(PlaybookExampleImage image, CancellationToken token) => Task.FromResult<ImageSource>(image.FileName.StartsWith("Opening range reversal", StringComparison.Ordinal)
                ? new DrawingImage(new GeometryDrawing(Brushes.DarkGoldenrod, null, new RectangleGeometry(new Rect(0, 0, 24, 88))))
                : Thumbnail);
        public Task<ImageSource> OriginalAsync(PlaybookExampleImage image, CancellationToken token)
        { Opened = image; return Task.FromResult(_original); }
        public Task<IReadOnlyList<PlanImageSelection>> PickMultipleAsync(string title, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<PlanImageSelection>>([new("Entry.png", [1], Thumbnail), new("Exit.png", [2], Thumbnail)]);
        internal IReadOnlyList<NewSetupExample> Created { get; private set; } = [];
        public Task<bool> UpdateWithExamplesAsync(TradingSetup setup, bool detailsChanged, SetupImageChanges changes, CancellationToken token)
        {
            if (FailSave) throw new IOException();
            foreach (var edit in changes.Edited)
            {
                var index = Rows.FindIndex(item => item.SetupId == setup.Id && item.Id == edit.Id);
                if (edit.Remove) Rows.RemoveAt(index);
                else Rows[index] = Rows[index] with { Timeframe = edit.Timeframe };
            }
            foreach (var image in changes.Added)
                _ = AddAsync(setup.Id, image.FileName, image.Bytes, image.Timeframe, token);
            return Task.FromResult(true);
        }
        public Task CreateWithExamplesAsync(TradingSetup setup, IReadOnlyList<NewSetupExample> examples, CancellationToken token)
        {
            if (FailSave) throw new IOException();
            Created = examples.ToArray();
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<SetupExample>> ListAsync(Guid owner, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<SetupExample>>(Rows.Where(x => x.SetupId == owner).ToArray());
        public Task<SetupExample> AddAsync(Guid owner, string name, byte[] bytes, string? timeframe, CancellationToken token)
        {
            if (FailSave) throw new IOException();
            var image = new SetupExample(Guid.NewGuid(),owner,timeframe,new("example.png",name,bytes.Length,new string('A',64)));
            Rows.Add(image); return Task.FromResult(image);
        }
        public Task RemoveAsync(Guid owner, Guid id, CancellationToken token)
        { Rows.Remove(Rows.Single(x => x.Id == id && x.SetupId == owner)); return Task.CompletedTask; }
    }
}
