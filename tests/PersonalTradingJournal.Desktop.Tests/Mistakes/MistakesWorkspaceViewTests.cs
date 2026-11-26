using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Application.Mistakes;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.Setups;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Views.Mistakes;

namespace PersonalTradingJournal.Desktop.Tests.Mistakes;

public sealed class MistakesWorkspaceViewTests
{
    [Fact]
    public async Task BoundColumnsMenusCancelAndDetailsAcrossThemesAndDpi()
    {
        const string host = "PTJ_MISTAKES_LAYOUT_HOST";
        if (Environment.GetEnvironmentVariable(host) != "1")
        {
            await IsolatedTestProcess.RunSuiteAsync(typeof(MistakesWorkspaceViewTests), "mistakes-layout", host,
                TimeSpan.FromSeconds(60), caseHangTimeout: TimeSpan.FromSeconds(30));
            return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            try
            {
                foreach (string theme in new[] { "Light", "Dark" })
                foreach (var (width, dpi) in new[] { (1000, 96), (460, 240) })
                {
                    app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
                    var now = DateTimeOffset.UnixEpoch;
                    TradingMistakeListItem[] catalog = [
                        new(Guid.NewGuid(), "Early entry", "Entered before confirmation", true, now, now),
                        new(Guid.NewGuid(), "Late exit", null, false, now, now)];
                    var reader = new FakeTradingMistakeReader();
                    reader.EnqueueResult(catalog);
                    var store = new FakeTradingMistakeStore();
                    var vm = TradingMistakesViewModelTests.Create(reader, store);
                    vm.EnsureLoadedAsync().GetAwaiter().GetResult();
                    var view = new TradingMistakesView { DataContext = vm, Background = (Brush)app.Resources["PtjBackgroundBrush"] };
                    VisualTreeHelper.SetRootDpi(view, new DpiScale(dpi / 96d, dpi / 96d));
                    void Layout()
                    {
                        view.Measure(new Size(width, 740));
                        view.Arrange(new Rect(0, 0, width, 740));
                        view.UpdateLayout();
                        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                    }
                    Layout();
                    var headers = (Grid)view.FindName("MistakeHeaders");
                    var scroll = (ScrollViewer)view.FindName("MistakeTableScroll");
                    Assert.Equal(new[] { "Name", "Description", "Status", "Actions" },
                        headers.Children.OfType<TextBlock>().Select(t => t.Text));
                    Assert.Equal(width < 590, scroll.ScrollableWidth > 0);
                    Render("list");
                    scroll.ScrollToRightEnd(); Layout();
                    var rows = SetupExampleViewTests.Children(view).OfType<Grid>().Where(g => g.Name == "MistakeRow").ToArray();
                    Assert.Equal(2, rows.Length);
                    foreach (var row in rows)
                    {
                        for (int column = 0; column < 4; column++)
                        {
                            Assert.Equal(headers.ColumnDefinitions[column].ActualWidth, row.ColumnDefinitions[column].ActualWidth, 1);
                            Assert.InRange(Math.Abs(headers.TranslatePoint(new(headers.ColumnDefinitions[column].Offset, 0), scroll).X -
                                row.TranslatePoint(new(row.ColumnDefinitions[column].Offset, 0), scroll).X), 0, 1);
                        }
                        var buttons = SetupExampleViewTests.Children(row).OfType<Button>().ToArray();
                        Assert.Equal(2, buttons.Length);
                        Assert.Same(vm.ViewCommand, buttons[0].Command);
                        var item = (TradingMistakeListItem)row.DataContext;
                        Assert.Equal(item.Id, buttons[0].CommandParameter);
                        var more = buttons[1];
                        Assert.True(more.Focusable);
                        Assert.True(PersonalTradingJournal.Desktop.Interactions.EntityActionMenu.GetOpensContextMenu(more));
                        Assert.InRange(more.TranslatePoint(new(more.ActualWidth, 0), scroll).X, 0, scroll.ViewportWidth + 1);
                        var menu = more.ContextMenu;
                        menu.PlacementTarget = more;
                        menu.Measure(new Size(260, 300)); menu.Arrange(new Rect(menu.DesiredSize)); menu.UpdateLayout();
                        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                        var edit = menu.Items.OfType<MenuItem>().Single(m => Equals(m.Header, "Edit"));
                        Assert.Same(vm.EditCommand, edit.Command);
                        Assert.Equal(item.Id, edit.CommandParameter);
                        Assert.True(edit.Focusable && edit.Command.CanExecute(edit.CommandParameter));
                        Assert.Contains(menu.Items.OfType<MenuItem>(), m => Equals(m.Header, "Delete") && ReferenceEquals(m.Command, vm.DeleteCommand));
                        reader.DetailsToReturn = new(item.Id, item.Name, item.Description, item.IsActive, now, now);
                        vm.EditCommand.ExecuteAsync(edit.CommandParameter).GetAwaiter().GetResult();
                        Assert.Equal(item.Id, reader.RequestedId);
                        vm.EditNameText = "Draft";
                        vm.CancelEditCommand.Execute(null); Layout();
                        Assert.Equal(Visibility.Collapsed, ((Border)view.FindName("MistakeDetails")).Visibility);
                        Assert.Equal(Visibility.Collapsed, ((Border)view.FindName("MistakeEditor")).Visibility);
                        Assert.Null(vm.SelectedTradingMistake);
                    }
                    reader.DetailsToReturn = new(catalog[0].Id, catalog[0].Name, catalog[0].Description, true, now, now);
                    vm.ViewCommand.ExecuteAsync(catalog[0].Id).GetAwaiter().GetResult(); Layout();
                    var details = (Border)view.FindName("MistakeDetails");
                    Assert.Equal(Visibility.Visible, details.Visibility);
                    Assert.Equal(2, SetupExampleViewTests.Children(details).OfType<Separator>().Count());
                    Render("details");
                    vm.EditCommand.ExecuteAsync(catalog[0].Id).GetAwaiter().GetResult(); Layout();
                    vm.EditDescriptionText = "Discard";
                    vm.CancelEditCommand.Execute(null); Layout();
                    Assert.Equal(Visibility.Collapsed, details.Visibility);
                    Assert.Equal(Visibility.Collapsed, ((Border)view.FindName("MistakeEditor")).Visibility);
                    Assert.Equal(0, store.UpdateCalls);
                    Assert.Equal("Early entry", vm.TradingMistakes[0].Name);

                    void Render(string state)
                    {
                        string directory = Path.Combine(Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY")!, "renders");
                        Directory.CreateDirectory(directory);
                        var bitmap = new RenderTargetBitmap(width * dpi / 96, 740 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
                        bitmap.Render(view);
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using var file = File.Create(Path.Combine(directory, $"{theme}-{width}-{dpi}-{state}.png"));
                        encoder.Save(file);
                    }
                }
            }
            finally { app.Shutdown(); }
        });
    }
}

