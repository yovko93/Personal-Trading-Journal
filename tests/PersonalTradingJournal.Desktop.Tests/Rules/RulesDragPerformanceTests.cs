using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.Diagnostics;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Views.Rules;
using PersonalTradingJournal.Domain.Rules;

namespace PersonalTradingJournal.Desktop.Tests.Rules;

public sealed class RulesDragPerformanceTests
{
    private const string Host = "PTJ_RULES_DRAG_HOST";
    [Fact]
    public async Task PointerMovementKeepsCatalogStableAndReportsVisualWork()
    {
        if (Environment.GetEnvironmentVariable(Host) != "1")
        {
            await IsolatedTestProcess.RunSuiteAsync(typeof(RulesDragPerformanceTests), "rules-drag", Host,
                TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30)); return;
        }
        Environment.SetEnvironmentVariable("PTJ_RULES_DRAG_DIAGNOSTICS", "1");
        await CalendarStaTest.RunAsync(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (int dpi in new[] { 96, 240 })
            foreach (bool expanded in new[] { false, true })
            {
                app.Resources = CalendarViewLayoutTests.SharedThemeResources(dpi == 96 ? "Light" : "Dark");
                var f = new RulesFixture();
                for (int i = 0; i < 12; i++)
                    f.Data.Rows.Add(TradingRuleRevision.Create(Guid.NewGuid(), new($"Synthetic rule {i:00}",
                        string.Join(" ", Enumerable.Repeat("Synthetic long Criterion for measuring drag layout.", 12))), DateTimeOffset.UtcNow));
                Await(f.Vm.ActivateAsync());
                var clock = new DragClock(); var view = new RulesView { DataContext = f.Vm, DragClock = clock, AnimateReordering = false };
                VisualTreeHelper.SetRootDpi(view, new DpiScale(dpi / 96d, dpi / 96d));
                void Layout() { view.Measure(new Size(800, 600)); view.Arrange(new Rect(0, 0, 800, 600)); view.UpdateLayout(); }
                Layout();
                var rows = Descendants(view).OfType<RuleReorderRow>().ToArray();
                var source = rows[1]; var target = (Grid)rows[0].Child;
                Descendants(target).OfType<Expander>().Single().IsExpanded = expanded; Layout();
                var catalog = f.Vm.Rules; var ids = catalog.Select(r => r.Id).ToArray();
                int reads = f.Data.CatalogCalls.Count;
                Assert.True(view.BeginRowLift(source, new Point(180, 220))); Layout();
                var diagnostics = Assert.IsType<RulesDragDiagnostics>(f.Vm.DragDiagnostics); diagnostics.Reset();
                var overlay = (Canvas)view.FindName("DragOverlay");
                Point firstMidpoint = rows[0].TranslatePoint(new Point(120, rows[0].ActualHeight / 2), overlay);
                for (int i = 0; i < 60; i++)
                {
                    view.UpdateRowDrag(new Point(firstMidpoint.X + i % 4, firstMidpoint.Y - 1), 200);
                    view.UpdateLayout();
                }
                Console.WriteLine($"RulesDrag dpi={dpi}; expanded={expanded}; {diagnostics.Snapshot()}");
                Assert.Same(catalog, f.Vm.Rules); Assert.Equal(ids, f.Vm.Rules.Select(r => r.Id));
                Assert.Equal(reads, f.Data.CatalogCalls.Count);
                foreach (var counter in new[] { RuleDragCounter.CatalogReads, RuleDragCounter.MoveWrites,
                    RuleDragCounter.CatalogPublishes, RuleDragCounter.PropertyNotifications, RuleDragCounter.CollectionChanges,
                    RuleDragCounter.PreviewCaptures, RuleDragCounter.Animations }) Assert.Equal(0, diagnostics[counter]);
                Assert.Equal(1, diagnostics[RuleDragCounter.TargetChanges]); Assert.Equal(0, diagnostics[RuleDragCounter.TreeScans]);
                Assert.InRange(diagnostics[RuleDragCounter.ViewLayouts], 0, 1);
                Assert.Equal(0, diagnostics[RuleDragCounter.RowMeasures]); Assert.Equal(0, diagnostics[RuleDragCounter.RowArranges]);
                // Moving between descendants/rows within the viewport must not flicker the source slot.
                view.LeaveRowDrag(new Point(200, 240));
                Assert.Equal(Visibility.Visible, ((Border)view.FindName("LiftedRule")).Visibility); Assert.Equal(0, source.Opacity);
                // Cross one and several original midpoints in both directions. Variable-height
                // expanded Criteria retain their layout; every affected card occupies its final slot.
                var list = (ItemsControl)view.FindName("RuleRows");
                var tops = rows.Select(r => r.TranslatePoint(new Point(), list).Y - ((TranslateTransform)r.RenderTransform).Y).ToArray();
                void CheckSlots(int index)
                {
                    Assert.Equal(index, view.ProspectiveIndex);
                    var order = rows.Where(r => r != source).ToList(); order.Insert(index, source);
                    double top = tops[0];
                    foreach (var r in order)
                    {
                        Assert.Equal(top, r.TranslatePoint(new Point(), list).Y, 4);
                        top += r.ActualHeight + r.Margin.Bottom + r.Margin.Top;
                    }
                }
                CheckSlots(0);
                foreach (int index in new[] { 2, 6, 11, 4, 1, 0 })
                {
                    double y = index == 0 ? tops[0] - 1 : tops[index] + rows[index].ActualHeight / 2 + 1;
                    view.UpdateProspectiveOrder(list.TranslatePoint(new Point(120, y), overlay));
                    CheckSlots(index);
                }
                Assert.Same(catalog, f.Vm.Rules); Assert.Equal(reads, f.Data.CatalogCalls.Count); Assert.Equal(0, f.Data.MoveCalls);
                // One easing clock per target change, not one per pointer event. Seek the real
                // WPF clock rather than sleeping; non-source rows never overlap during retargeting.
                view.AnimateReordering = true; diagnostics.Reset(); view.SetProspectiveIndex(8);
                var animation = Assert.IsType<AnimationClock>(view.ShiftClock);
                animation.Controller!.SeekAlignedToLastTick(TimeSpan.FromMilliseconds(40), TimeSeekOrigin.BeginTime);
                AssertNonOverlapping(rows, source, list);
                for (int i = 0; i < 60; i++) view.SetProspectiveIndex(8);
                Assert.Same(animation, view.ShiftClock); Assert.Equal(1, diagnostics[RuleDragCounter.Animations]);
                view.SetProspectiveIndex(3);
                Assert.NotSame(animation, view.ShiftClock);
                view.ShiftClock!.Controller!.SeekAlignedToLastTick(TimeSpan.FromMilliseconds(60), TimeSeekOrigin.BeginTime);
                AssertNonOverlapping(rows, source, list);
                view.ShiftClock.Controller.SeekAlignedToLastTick(TimeSpan.FromMilliseconds(120), TimeSeekOrigin.BeginTime);
                CheckSlots(3);
                view.UpdateLayout();
                Console.WriteLine($"RulesDrag animated dpi={dpi}; expanded={expanded}; {diagnostics.Snapshot()}");
                Assert.Equal(2, diagnostics[RuleDragCounter.TargetChanges]); Assert.Equal(2, diagnostics[RuleDragCounter.Animations]);
                foreach (var counter in new[] { RuleDragCounter.CatalogReads, RuleDragCounter.MoveWrites,
                    RuleDragCounter.CatalogPublishes, RuleDragCounter.PropertyNotifications, RuleDragCounter.CollectionChanges,
                    RuleDragCounter.RowsLoaded, RuleDragCounter.RowsUnloaded, RuleDragCounter.RowMeasures, RuleDragCounter.RowArranges })
                    Assert.Equal(0, diagnostics[counter]);
                Assert.InRange(diagnostics[RuleDragCounter.ViewLayouts], 0, 1);
                view.AnimateReordering = false;
                diagnostics.Reset();
                for (int i = 0; i < 60; i++)
                {
                    view.UpdateRowDrag(new Point(180, 590), 590);
                    view.UpdateLayout();
                }
                Console.WriteLine($"RulesDrag edge dpi={dpi}; expanded={expanded}; {diagnostics.Snapshot()}");
                Assert.Equal(1, diagnostics[RuleDragCounter.ScrollRequests]);
                clock.Advance(TimeSpan.FromMilliseconds(49)); view.UpdateRowDrag(new Point(180, 590), 590);
                Assert.Equal(1, diagnostics[RuleDragCounter.ScrollRequests]);
                clock.Advance(TimeSpan.FromMilliseconds(1)); view.UpdateRowDrag(new Point(180, 590), 590);
                Assert.Equal(2, diagnostics[RuleDragCounter.ScrollRequests]);
                view.LeaveRowDrag(new Point(-1, -1));
                Assert.Equal(Visibility.Collapsed, ((Border)view.FindName("LiftedRule")).Visibility); Assert.Equal(1, source.Opacity);
                Assert.Null(view.ShiftClock); Assert.All(rows, r => Assert.Equal(0, ((TranslateTransform)r.RenderTransform).Y));
                view.EndRowDrag();
                Assert.Equal(Visibility.Collapsed, ((Border)view.FindName("LiftedRule")).Visibility);
                Assert.Equal(1, source.Opacity);
                Assert.All(rows, b => Assert.Equal(Transform.Identity, b.RenderTransform)); Assert.Null(view.ShiftClock);
                Assert.Same(catalog, f.Vm.Rules); Assert.Equal(reads, f.Data.CatalogCalls.Count); Assert.Equal(0, f.Data.MoveCalls);

                // A no-op drop, a valid drop, then a failed drop use the same UI entry point.
                Assert.True(view.BeginRowLift(rows[0], new Point(180, 200)));
                diagnostics.Reset(); Await(view.DropProspectiveOrderAsync());
                Assert.Equal(0, diagnostics[RuleDragCounter.MoveWrites]); Assert.Equal(0, diagnostics[RuleDragCounter.CatalogReads]);
                Assert.True(view.BeginRowLift(rows[1], new Point(180, 200))); view.SetProspectiveIndex(0);
                Await(view.DropProspectiveOrderAsync()); Layout();
                Assert.Equal(1, diagnostics[RuleDragCounter.MoveWrites]); Assert.Equal(1, diagnostics[RuleDragCounter.CatalogReads]);
                Assert.Equal(1, diagnostics[RuleDragCounter.CatalogPublishes]); Assert.Equal(1, diagnostics[RuleDragCounter.CollectionChanges]);
                Assert.Equal(ids[1], f.Vm.Rules[0].Id); Assert.Equal(Enumerable.Range(1, 12), f.Vm.Rules.Select(r => r.DisplayNumber));
                var persisted = f.Vm.Rules.Select(r => r.Id).ToArray(); f.Data.Failure = "failure"; diagnostics.Reset();
                var failedSource = Descendants(view).OfType<RuleReorderRow>().First();
                Assert.True(view.BeginRowLift(failedSource, new Point(180, 200)));
                view.SetProspectiveIndex(3); Await(view.DropProspectiveOrderAsync()); Layout();
                Assert.Equal(persisted, f.Vm.Rules.Select(r => r.Id)); Assert.Equal(1, diagnostics[RuleDragCounter.MoveWrites]);
                Assert.Equal(1, diagnostics[RuleDragCounter.CatalogReads]); Assert.Equal(1, diagnostics[RuleDragCounter.CatalogPublishes]);
                Assert.Equal(Visibility.Collapsed, ((Border)view.FindName("LiftedRule")).Visibility);
                Assert.Null(((Image)view.FindName("LiftedRuleImage")).Source); Assert.Equal(1, failedSource.Opacity);
                Assert.Contains("could not be moved", f.Vm.Message); f.Vm.Deactivate();
            }
        }, "Rules pointer movement numeric diagnostics");
    }
    private static void AssertNonOverlapping(RuleReorderRow[] rows, RuleReorderRow source, ItemsControl list)
    {
        var others = rows.Where(r => r != source).ToArray();
        for (int i = 1; i < others.Length; i++)
            Assert.True(others[i - 1].TranslatePoint(new Point(0, others[i - 1].ActualHeight), list).Y <=
                others[i].TranslatePoint(new Point(), list).Y + .01, "Displaced cards must not overlap while a shared clock is retargeted.");
    }
    private sealed class DragClock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        internal void Advance(TimeSpan time) => _ticks += time.Ticks;
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var next in Descendants(child)) yield return next;
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
