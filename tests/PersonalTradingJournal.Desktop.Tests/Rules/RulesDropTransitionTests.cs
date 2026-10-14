using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Views.Rules;
using PersonalTradingJournal.Desktop.ViewModels.Rules;
using PersonalTradingJournal.Domain.Rules;

namespace PersonalTradingJournal.Desktop.Tests.Rules;

public sealed class RulesDropTransitionTests
{
    private const string Host = "PTJ_RULES_DROP_HOST";
    [Fact]
    public async Task PendingDropRetainsLiftAndVerifiedMoveRetainsCardsFocusAndBothScrollPositions()
    {
        if (Environment.GetEnvironmentVariable(Host) != "1")
        {
            await IsolatedTestProcess.RunSuiteAsync(typeof(RulesDropTransitionTests), "rules-drop", Host,
                TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30)); return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (string theme in new[] { "Light", "Dark" })
            foreach (var (width, dpi) in new[] { (960, 96), (480, 240) })
            {
                app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
                var f = new RulesFixture();
                for (int i = 0; i < 5; i++) f.Data.Rows.Add(TradingRuleRevision.Create(Guid.NewGuid(),
                    new($"Synthetic Rule {i}", i % 2 == 0 ? string.Join("\n", Enumerable.Repeat("Complete synthetic Criterion line.", 50)) : "Short Criterion"), DateTimeOffset.UtcNow));
                Await(f.Vm.ActivateAsync());
                var view = new RulesView { DataContext = f.Vm, AnimateReordering = false };
                VisualTreeHelper.SetRootDpi(view, new DpiScale(dpi / 96d, dpi / 96d));
                FocusManager.SetIsFocusScope(view, true);
                void Layout() { view.Measure(new Size(width, 600)); view.Arrange(new Rect(0, 0, width, 600)); view.UpdateLayout(); }
                Layout();
                var list = (ItemsControl)view.FindName("RuleRows");
                var page = (ScrollViewer)view.FindName("RulesScroller");
                var lift = (Border)view.FindName("LiftedRule");
                var cards = Descendants(view).OfType<RuleReorderRow>().ToArray();
                var source = cards[0];
                var text = cards.Select(c => Descendants(c).OfType<ScrollViewer>().Single(s => s.Name == "RuleTextScroller")).ToArray();
                text[0].ScrollToVerticalOffset(110); text[2].ScrollToVerticalOffset(80); page.ScrollToVerticalOffset(120); Layout();
                var offsets = text.Select(s => s.VerticalOffset).ToArray(); double pageOffset = page.VerticalOffset;
                Assert.True(offsets[0] > 0); Assert.True(offsets[2] > 0); Assert.True(pageOffset > 0);
                FocusManager.SetFocusedElement(view, source);
                var ids = f.Vm.Rules.Select(r => r.Id).ToArray(); var items = f.Vm.Rules;
                var events = new List<NotifyCollectionChangedAction>();
                ((INotifyCollectionChanged)list.Items).CollectionChanged += (_, e) => events.Add(e.Action);
                int reads = f.Data.CatalogCalls.Count;
                Assert.True(view.BeginRowLift(source, new Point(180, 170))); view.SetProspectiveIndex(1); Layout();
                double top = list.TranslatePoint(new Point(), view).Y;
                var readStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var readRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                f.Data.BeforeCatalogPage = async (_, _) => { readStarted.TrySetResult(); await readRelease.Task; };
                f.Data.WriteGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
                Task drop = view.DropProspectiveOrderAsync();
                view.EndRowDrag(); // Native DoDragDrop finally runs before the async drop finishes.
                view.LeaveRowDrag(new Point(-1, -1)); Layout();
                AssertPending();
                f.Data.WriteGate.SetResult(); Await(readStarted.Task); Layout(); AssertPending();
                RulesViewTests.Render(view, theme, width, dpi, "drop-pending");
                readRelease.SetResult(); Await(drop); Layout();
                f.Data.WriteGate = null; f.Data.BeforeCatalogPage = null;
                Assert.Same(items, f.Vm.Rules); Assert.Equal([NotifyCollectionChangedAction.Move], events);
                Assert.Equal(new[] { ids[1], ids[0], ids[2], ids[3], ids[4] }, f.Vm.Rules.Select(r => r.Id));
                Assert.Equal(Enumerable.Range(1, 5), f.Vm.Rules.Select(r => r.DisplayNumber));
                Assert.Equal(1, f.Data.MoveCalls); Assert.Equal(reads + 1, f.Data.CatalogCalls.Count);
                AssertRetained(); AssertClean();
                Assert.Equal(top, list.TranslatePoint(new Point(), view).Y, 4);
                var currentCards = Descendants(view).OfType<RuleReorderRow>().ToArray();
                Assert.Same(cards[1], currentCards[0]); Assert.Same(source, currentCards[1]);
                RulesViewTests.Render(view, theme, width, dpi, "drop-committed");

                // Same position produces no busy transition, read, write, reset or card replacement.
                events.Clear(); reads = f.Data.CatalogCalls.Count;
                Assert.True(view.BeginRowLift(source, new Point(180, 170)));
                Await(view.DropProspectiveOrderAsync()); Layout();
                Assert.Empty(events); Assert.Equal(1, f.Data.MoveCalls); Assert.Equal(reads, f.Data.CatalogCalls.Count);
                AssertRetained(); AssertClean();

                foreach (string failure in new[] { "conflict", "failure", "cancel" })
                {
                    f.Data.Failure = failure; events.Clear(); reads = f.Data.CatalogCalls.Count;
                    var order = f.Vm.Rules.Select(r => r.Id).ToArray();
                    Assert.True(view.BeginRowLift(source, new Point(180, 170))); view.SetProspectiveIndex(3);
                    Await(view.DropProspectiveOrderAsync()); Layout();
                    Assert.Equal(order, f.Vm.Rules.Select(r => r.Id)); Assert.Empty(events);
                    Assert.Equal(reads + 1, f.Data.CatalogCalls.Count); Assert.Contains("persisted order", f.Vm.Message);
                    AssertRetained(); AssertClean();
                }
                Console.WriteLine($"RulesDrop retained=5/5; textScrollers=5/5; reset=0; validMoveEvents=1; writes=4; reads=4; theme={theme}; width={width}; dpi={dpi}");
                Assert.Equal(4, f.Data.MoveCalls); f.Vm.Deactivate();

                void AssertPending()
                {
                    Assert.False(drop.IsCompleted); Assert.Equal(Visibility.Visible, lift.Visibility); Assert.Equal(0, source.Opacity);
                    Assert.Same(items, f.Vm.Rules); Assert.Equal(ids, f.Vm.Rules.Select(r => r.Id)); Assert.Empty(events);
                    Assert.Equal(top, list.TranslatePoint(new Point(), view).Y, 4);
                }
                void AssertRetained()
                {
                    var retained = Descendants(view).OfType<RuleReorderRow>().ToArray();
                    Assert.Equal(5, retained.Length); Assert.All(cards, c => Assert.Contains(c, retained));
                    for (int i = 0; i < cards.Length; i++)
                    {
                        Assert.Same(text[i], Descendants(cards[i]).OfType<ScrollViewer>().Single(s => s.Name == "RuleTextScroller"));
                        Assert.Equal(offsets[i], text[i].VerticalOffset, 4);
                    }
                    Assert.Equal(pageOffset, page.VerticalOffset, 4); Assert.Same(source, FocusManager.GetFocusedElement(view));
                }
                void AssertClean()
                {
                    Assert.Equal(Visibility.Collapsed, lift.Visibility); Assert.Equal(1, source.Opacity);
                    Assert.Null(((Image)view.FindName("LiftedRuleImage")).Source);
                    Assert.All(cards, c => Assert.Equal(Transform.Identity, c.RenderTransform));
                }
            }
            // Connected-window focus is checked independently of whether this runner grants
            // foreground activation. Logical focus must always survive; native focus, when
            // granted initially, must remain on the very same retained row.
            var focused = new RulesFixture(); focused.Data.Seed(1); focused.Data.Seed(1);
            Await(focused.Vm.ActivateAsync());
            var connected = new RulesView { DataContext = focused.Vm, AnimateReordering = false };
            var window = new Window { Content = connected, Width = 800, Height = 600, ShowInTaskbar = false };
            try
            {
                window.Show(); window.UpdateLayout();
                var row = Descendants(connected).OfType<RuleReorderRow>().First();
                row.Focus(); FocusManager.SetFocusedElement(window, row);
                bool keyboardFocused = row.IsKeyboardFocused;
                Assert.True(connected.BeginRowLift(row, new Point(180, 180))); connected.SetProspectiveIndex(1);
                Await(connected.DropProspectiveOrderAsync()); window.UpdateLayout();
                Assert.Same(row, FocusManager.GetFocusedElement(window));
                if (keyboardFocused) Assert.True(row.IsKeyboardFocused);
                Console.WriteLine($"RulesDrop native focus initiallyGranted={keyboardFocused}; retained={row.IsKeyboardFocused}; logicalRetained={ReferenceEquals(row, FocusManager.GetFocusedElement(window))}");
                awaitGuardedDrop();
                void awaitGuardedDrop()
                {
                    Await(focused.Vm.EditRowCommand.ExecuteAsync((RuleRow)row.DataContext));
                    focused.Vm.Fields[1].Text = "Synthetic unsaved content"; window.UpdateLayout();
                    int writes = focused.Data.MoveCalls; var order = focused.Vm.Rules.Select(r => r.Id).ToArray();
                    Assert.True(connected.BeginRowLift(row, new Point(180, 180))); connected.SetProspectiveIndex(0);
                    Await(connected.DropProspectiveOrderAsync()); window.UpdateLayout();
                    Assert.Equal(writes, focused.Data.MoveCalls); Assert.Equal(order, focused.Vm.Rules.Select(r => r.Id));
                    Assert.True(focused.Vm.IsEditing); Assert.Equal("Synthetic unsaved content", focused.Vm.Fields[1].Text);
                    Assert.Equal(Visibility.Collapsed, ((Border)connected.FindName("LiftedRule")).Visibility);
                }
            }
            finally { window.Close(); focused.Vm.Deactivate(); }
        }, "Rules drop retained cards and scrolling");
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    private static void Await(Task task)
    {
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame(); var dispatcher = Dispatcher.CurrentDispatcher;
            _ = task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)), TaskScheduler.Default);
            Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }
}
