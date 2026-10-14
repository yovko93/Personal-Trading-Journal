using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.Navigation;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Validation;
using PersonalTradingJournal.Desktop.Views.Rules;

namespace PersonalTradingJournal.Desktop.Tests.Rules;

public sealed class RulesViewTests
{
    private sealed class TestPresentationSource(Visual visual) : PresentationSource
    {
        public override Visual RootVisual { get; set; } = visual;
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }
    private const string Host = "PTJ_RULES_VIEW_HOST";
    [Fact]
    public async Task CompiledThemesNarrowHighDpiBindingsValidationHistoryAndWindowClose()
    {
        if (Environment.GetEnvironmentVariable(Host) != "1")
        {
            await IsolatedTestProcess.RunSuiteAsync(typeof(RulesViewTests), "rules-view", Host, TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30)); return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (string theme in new[] { "Light", "Dark" })
            {
                app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
                foreach (var (width, dpi) in new[] { (960, 96), (480, 240) })
                {
                    var f = new RulesFixture(); f.Data.Seed(12);
                    var id = Guid.Parse("00000000-0000-0000-0000-000000000001");
                    for (int i = 0; i < f.Data.Rows.Count; i++)
                    {
                        var old = f.Data.Rows[i];
                        f.Data.Rows[i] = PersonalTradingJournal.Domain.Rules.TradingRuleRevision.Rehydrate(id, old.Revision, old.Definition, old.IsActive, old.RecordedAtUtc);
                    }
                    f.Data.Rows[^1] = PersonalTradingJournal.Domain.Rules.TradingRuleRevision.Rehydrate(id, 12,
                        new("A longer synthetic rule name to check wrapping", string.Join("\n", Enumerable.Repeat("A synthetic Criterion remains readable and retains its complete optional text.", 36)), "Retained evidence",
                            new(PersonalTradingJournal.Domain.Rules.RuleProfitBasis.StrictNet, 123.456m, "EUR", capability: PersonalTradingJournal.Domain.Rules.RuleCheckCapability.ClosedDayLoss)),
                        true, DateTimeOffset.UtcNow.AddDays(-1));
                    var secondId = Guid.Parse("00000000-0000-0000-0000-000000000002");
                    var thirdId = Guid.Parse("00000000-0000-0000-0000-000000000003");
                    foreach (var other in new[] { secondId, thirdId })
                        f.Data.Rows.Add(PersonalTradingJournal.Domain.Rules.TradingRuleRevision.Create(other, new("Another synthetic principle",
                            other == thirdId ? "Context line\nConfirmation line\nLesson line" : "Leave these rows unchanged"), DateTimeOffset.UtcNow.AddDays(-1)));
                    for (int number = 4; number <= 12; number++)
                        f.Data.Rows.Add(PersonalTradingJournal.Domain.Rules.TradingRuleRevision.Create(Guid.Parse($"00000000-0000-0000-0000-{number:D12}"),
                            new("Another synthetic principle"), DateTimeOffset.UtcNow.AddDays(-1)));
                    Await(f.Open(id));
                    var view = new RulesView { DataContext = f.Vm }; FocusManager.SetIsFocusScope(view, true);
                    VisualTreeHelper.SetRootDpi(view, new DpiScale(dpi / 96d, dpi / 96d));
                    void Layout() { view.Measure(new Size(width, 400)); view.Arrange(new Rect(0, 0, width, 400)); view.UpdateLayout(); }
                    Layout();
                    var scroller = (ScrollViewer)view.FindName("RulesScroller");
                    Assert.Equal(12, ((ItemsControl)view.FindName("RuleRows")).Items.Count);
                    Assert.Null(view.FindName("RuleSelector")); Assert.Null(view.FindName("RevisionHistory"));
                    Assert.Null(view.FindName("AccountSelector")); Assert.Null(view.FindName("TargetSelector"));
                    var rows = (ItemsControl)view.FindName("RuleRows");
                    var rowGrid = Descendants(rows).OfType<Grid>().Single(g => g.Name == "RuleRowLayout" && g.DataContext is PersonalTradingJournal.Desktop.ViewModels.Rules.RuleRow r && r.Id == id);
                    var rowButtons = Descendants(rowGrid).OfType<Button>().Where(b => b.Name != "RuleDragHandle").ToArray();
                    Assert.Equal(new[] { "Edit", "Delete" }, rowButtons.Select(b => b.Content));
                    foreach (var button in rowButtons)
                    {
                        Assert.True(button.Focusable); Assert.NotEmpty(AutomationProperties.GetName(button));
                        Assert.InRange(button.TranslatePoint(new Point(button.ActualWidth, 0), view).X, 0, width);
                    }
                    Assert.DoesNotContain(Descendants(view).OfType<TextBlock>(), t => t.IsVisible && (t.Text.Contains("Revision") || t.Text.Contains("Account scope")));

                    Assert.InRange(rowGrid.ActualWidth - rowButtons[1].TranslatePoint(new Point(rowButtons[1].ActualWidth, 0), rowGrid).X, 0, 1);
                    Assert.Equal(rowButtons[0].TranslatePoint(new Point(), rowGrid).Y, rowButtons[1].TranslatePoint(new Point(), rowGrid).Y);
                    Assert.True(rowButtons[0].TranslatePoint(new Point(), rowGrid).X > rowGrid.ActualWidth / 2);
                    var condition = Descendants(rowGrid).OfType<TextBlock>().Single(t => t.Text == f.Vm.SelectedRule!.AutomaticCondition);
                    Assert.Equal(Visibility.Visible, condition.Visibility); Assert.Contains("EUR", condition.Text); Assert.Contains("123.456", condition.Text);
                    Assert.InRange(condition.TranslatePoint(new Point(condition.ActualWidth, 0), rowGrid).X, 0, rowButtons[0].TranslatePoint(new Point(), rowGrid).X - 1);
                    Assert.Null(view.FindName("RulePagination"));
                    Assert.DoesNotContain(Descendants(view).OfType<Button>(), b => Equals(b.Content, "Previous rules") || Equals(b.Content, "Next rules"));
                    Assert.DoesNotContain(Descendants(view).OfType<TextBlock>(), t => t.Text == "Rule editor" || t.Text.StartsWith("Write a general trading principle."));
                    var marker = Find<TextBlock>(rowGrid, "RuleMarker");
                    Assert.False(marker.Focusable); Assert.False(marker.IsHitTestVisible);
                    Assert.Equal("1", marker.Text); Assert.Equal("Rule list position 1", AutomationProperties.GetName(marker));
                    Assert.True(condition.TranslatePoint(new Point(), rowGrid).X >= marker.ActualWidth);
                    var feedback = (Border)view.FindName("FeedbackBox");
                    Assert.True(feedback.TranslatePoint(new Point(0, feedback.ActualHeight), view).Y <= scroller.TranslatePoint(new Point(), view).Y);
                    Assert.Equal(new Thickness(1), feedback.BorderThickness);
                    Assert.Equal(((SolidColorBrush)app.Resources["PtjSuccessSurfaceBrush"]).Color, ((SolidColorBrush)feedback.Background).Color);
                    Render(view, theme, width, dpi, "catalog");
                    var motto = (TextBlock)view.FindName("RulesMotto");
                    Assert.Equal("Правилата са, за да се спазват!", motto.Text);
                    Assert.Equal(TextAlignment.Center, motto.TextAlignment); Assert.Equal(TextWrapping.Wrap, motto.TextWrapping);
                    Assert.True(motto.TranslatePoint(new Point(0, motto.ActualHeight), view).Y <= ((Button)view.FindName("CreateRule")).TranslatePoint(new Point(), view).Y);
                    Assert.Equal(((SolidColorBrush)app.Resources["PtjTextPrimaryBrush"]).Color, ((SolidColorBrush)motto.Foreground).Color);
                    Assert.True(Contrast(((SolidColorBrush)motto.Foreground).Color, ((SolidColorBrush)view.Background).Color) >= 7);
                    var surface = (Border)VisualTreeHelper.GetParent(rowGrid);
                    Assert.Equal(new CornerRadius(8), surface.CornerRadius); Assert.Equal(new Thickness(1), surface.BorderThickness);
                    Assert.True(scroller.AllowDrop); // Stationary drop zone includes the animated empty slots.
                    Assert.Equal(10, surface.Margin.Bottom);
                    Assert.Equal(((SolidColorBrush)app.Resources["PtjSurfaceBrush"]).Color, ((SolidColorBrush)surface.Background).Color);
                    var cardShadow = Assert.IsType<System.Windows.Media.Effects.DropShadowEffect>(surface.Effect);
                    Assert.Equal((Color)app.Resources["PtjCardShadowColor"], cardShadow.Color);
                    Assert.InRange(cardShadow.Opacity, .05, .12); Assert.InRange(cardShadow.BlurRadius, 4, 8);
                    var grip = Find<Canvas>(rowGrid, "RuleGrip");
                    Assert.Equal(6, grip.Children.OfType<System.Windows.Shapes.Ellipse>().Count());
                    Assert.False(grip.IsHitTestVisible); Assert.False(grip.Focusable);
                    Assert.True(grip.TranslatePoint(new Point(grip.ActualWidth, 0), rowGrid).X < marker.TranslatePoint(new Point(), rowGrid).X);
                    var criterion = Find<TextBlock>(rowGrid, "RuleCriterion");
                    Assert.Equal(TextTrimming.None, criterion.TextTrimming); Assert.Equal(double.PositiveInfinity, criterion.MaxHeight);
                    Assert.Equal(TextWrapping.Wrap, criterion.TextWrapping);
                    var title = Find<TextBlock>(rowGrid, "RuleTitle");
                    Assert.Equal(TextTrimming.None, title.TextTrimming); Assert.Equal(TextWrapping.Wrap, title.TextWrapping);
                    Assert.Equal(f.Vm.SelectedRule!.Name, title.Text);
                    var textScroller = Find<ScrollViewer>(rowGrid, "RuleTextScroller");
                    Assert.Equal(320, textScroller.MaxHeight); Assert.InRange(textScroller.ActualHeight, 1, 320);
                    Assert.Equal(Visibility.Visible, textScroller.ComputedVerticalScrollBarVisibility);
                    Assert.True(textScroller.ScrollableHeight > 0); Assert.True(textScroller.Focusable && textScroller.IsTabStop);
                    Assert.Contains("Page Up", AutomationProperties.GetHelpText(textScroller));
                    Assert.Equal(criterion.Text, AutomationProperties.GetName(criterion));
                    Assert.True(criterion.Text.Length > 500);
                    Assert.Equal(criterion.Text, Assert.IsType<string>(criterion.ToolTip));
                    Assert.Equal(criterion.Text, UIElementAutomationPeer.CreatePeerForElement(criterion)!.GetName());
                    Assert.DoesNotContain(Descendants(rows).OfType<Expander>(), e => e.Name == "CriterionDetails" || Equals(e.Header, "Full criterion"));
                    Assert.DoesNotContain(Descendants(rows).OfType<FrameworkElement>(), e => e.Name == "FullCriterion");
                    Assert.True(criterion.ActualHeight > textScroller.ViewportHeight);
                    var shortCriterion = Descendants(rows).OfType<TextBlock>().Single(t => t.Name == "RuleCriterion" &&
                        t.DataContext is PersonalTradingJournal.Desktop.ViewModels.Rules.RuleRow r && r.Id == secondId);
                    Assert.Equal("Leave these rows unchanged", shortCriterion.Text);
                    Assert.Equal(shortCriterion.Text, shortCriterion.ToolTip);
                    Assert.Equal(shortCriterion.Text, UIElementAutomationPeer.CreatePeerForElement(shortCriterion)!.GetName());
                    var shortScroller = Find<ScrollViewer>(Descendants(rows).OfType<Grid>().Single(g => g.Name == "RuleRowLayout" &&
                        g.DataContext is PersonalTradingJournal.Desktop.ViewModels.Rules.RuleRow r && r.Id == secondId), "RuleTextScroller");
                    Assert.Equal(Visibility.Collapsed, shortScroller.ComputedVerticalScrollBarVisibility);
                    Assert.Equal(0, shortScroller.ScrollableHeight); Assert.True(shortScroller.ActualHeight < 320);
                    var multilineCriterion = Descendants(rows).OfType<TextBlock>().Single(t => t.Name == "RuleCriterion" &&
                        t.DataContext is PersonalTradingJournal.Desktop.ViewModels.Rules.RuleRow r && r.Id == thirdId);
                    Assert.Equal("Context line\nConfirmation line\nLesson line", multilineCriterion.Text);
                    Assert.True(multilineCriterion.ActualHeight >= 3 * 22);
                    var emptyCriterion = Descendants(rows).OfType<TextBlock>().First(t => t.Name == "RuleCriterion" &&
                        t.DataContext is PersonalTradingJournal.Desktop.ViewModels.Rules.RuleRow r && !r.HasStatement);
                    Assert.Equal(Visibility.Collapsed, emptyCriterion.Visibility); Assert.Equal(0, emptyCriterion.ActualHeight);
                    // The text column contains the complete title, Criterion and typed condition.
                    Assert.Equal(3, Assert.IsType<StackPanel>(VisualTreeHelper.GetParent(criterion)).Children.Count);
                    foreach (var button in rowButtons)
                        Assert.InRange(button.TranslatePoint(new Point(), rowGrid).Y, 0, 1);
                    Assert.False(f.Vm.IsEditing); Render(view, theme, width, dpi, "complete-text");
                    double pageOffset = scroller.VerticalOffset;
                    var localWheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120) { RoutedEvent = UIElement.PreviewMouseWheelEvent };
                    criterion.RaiseEvent(localWheel); Flush(); Layout();
                    Assert.True(localWheel.Handled); Assert.True(textScroller.VerticalOffset > 0);
                    Assert.Equal(pageOffset, scroller.VerticalOffset);
                    textScroller.ScrollToEnd(); Flush(); Layout();
                    Assert.Equal(textScroller.ScrollableHeight, textScroller.VerticalOffset, 3);
                    Assert.True(condition.TranslatePoint(new Point(0, condition.ActualHeight), textScroller).Y <= textScroller.ViewportHeight + 1);
                    var boundaryWheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120) { RoutedEvent = UIElement.PreviewMouseWheelEvent };
                    criterion.RaiseEvent(boundaryWheel); Flush(); Layout();
                    Assert.True(boundaryWheel.Handled); Assert.True(scroller.VerticalOffset > pageOffset);
                    Assert.Equal(textScroller.ScrollableHeight, textScroller.VerticalOffset, 3);
                    condition.BringIntoView(); Flush(); Layout();
                    Assert.InRange(condition.TranslatePoint(new Point(), scroller).Y, 0, scroller.ViewportHeight);
                    Assert.True(condition.TranslatePoint(new Point(0, condition.ActualHeight), scroller).Y <= scroller.ViewportHeight + 1);
                    Render(view, theme, width, dpi, "complete-text-end");
                    textScroller.ScrollToHome(); scroller.ScrollToHome(); Flush(); Layout();
                    Assert.True(surface.Focusable); Assert.NotNull(surface.FocusVisualStyle);
                    Assert.Contains("Reorder rule", AutomationProperties.GetName(surface));
                    Assert.Contains("Alt+Up", AutomationProperties.GetHelpText(surface));
                    var peer = UIElementAutomationPeer.CreatePeerForElement(surface);
                    Assert.NotNull(peer); Assert.Equal(AutomationControlType.ListItem, peer.GetAutomationControlType());
                    Assert.Equal(AutomationProperties.GetName(surface), peer.GetName());
                    Assert.Contains("Alt+Up", peer.GetHelpText());
                    Assert.Equal("Rule list position 1", peer.GetItemStatus());
                    Assert.True(peer.IsKeyboardFocusable());
                    Assert.Equal(2, surface.ContextMenu!.Items.Count);
                    Assert.DoesNotContain(Descendants(rowGrid).OfType<TextBlock>(), t => t.Text == "↕");
                    Assert.DoesNotContain(Descendants(rowGrid).OfType<FrameworkElement>(), e => e.Name is "RuleDragHandle" or "InsertionIndicator");
                    // Card surfaces remain draggable; overflowing text and scrollbar controls scroll instead.
                    foreach (var hit in new DependencyObject[] { surface, rowGrid, grip, grip.Children[0], marker })
                        Assert.True(RulesView.CanStartRowDrag(hit, surface));
                    foreach (var hit in new DependencyObject[] { textScroller, criterion, title, condition })
                    {
                        Assert.False(RulesView.CanStartRowDrag(hit, surface)); Assert.Null(view.RowCursor(hit, surface));
                    }
                    Assert.All(Descendants(textScroller).OfType<System.Windows.Controls.Primitives.ScrollBar>(), bar => Assert.False(RulesView.CanStartRowDrag(bar, surface)));
                    var shortSurface = (Border)VisualTreeHelper.GetParent(VisualTreeHelper.GetParent(shortScroller));
                    Assert.True(RulesView.CanStartRowDrag(shortCriterion, shortSurface));
                    criterion.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
                    Assert.Same(Cursors.Hand, view.RowCursor(surface, surface)); // No row drag candidate was armed.
                    foreach (var button in rowButtons)
                    {
                        Assert.False(RulesView.CanStartRowDrag(button, surface));
                        Assert.False(RulesView.CanStartRowDrag(Descendants(button).First(), surface));
                        Assert.Null(view.RowCursor(button, surface));
                    }
                    foreach (var hit in new DependencyObject[] { surface, rowGrid, marker })
                        Assert.Same(Cursors.Hand, view.RowCursor(hit, surface));
                    var query = new QueryCursorEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.QueryCursorEvent };
                    surface.RaiseEvent(query); Assert.True(query.Handled); Assert.Same(Cursors.Hand, query.Cursor);
                    surface.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
                    Assert.Same(RuleDragCursor.Grabbing, view.RowCursor(surface, surface));
                    surface.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent });
                    Assert.Same(Cursors.Hand, view.RowCursor(surface, surface));
                    Assert.Null(Mouse.OverrideCursor);

                    var overlay = (Canvas)view.FindName("DragOverlay");
                    var lift = (Border)view.FindName("LiftedRule");
                    var liftImage = (Image)view.FindName("LiftedRuleImage");
                    // Inspect drag surfaces, not unrelated native button/scrollbar focus templates.
                    Assert.DoesNotContain(rowGrid.Children.OfType<System.Windows.Shapes.Shape>(), s => s.StrokeDashArray.Count > 0);
                    Assert.DoesNotContain(Descendants(view).OfType<FrameworkElement>(), e => e.Name is "DragOriginMarker" or "DropHighlight");
                    Point start = surface.TranslatePoint(new Point(70, 20), overlay);
                    Assert.True(view.BeginRowLift(surface, start)); Layout();
                    var snapshot = Assert.IsType<RenderTargetBitmap>(liftImage.Source);
                    Assert.InRange(snapshot.DpiX, dpi, dpi + 0.01);
                    Assert.True(snapshot.IsFrozen); Assert.InRange(snapshot.PixelWidth * (long)snapshot.PixelHeight, 1, 8_100_000);
                    Assert.Equal(surface.ActualWidth, snapshot.Width, 0); Assert.Equal(surface.ActualHeight, snapshot.Height, 0);
                    var markerBadge = (Border)VisualTreeHelper.GetParent(marker);
                    Point badgePixel = markerBadge.TranslatePoint(new Point(5, markerBadge.ActualHeight / 2), surface);
                    var capturedBadge = new byte[4];
                    snapshot.CopyPixels(new Int32Rect((int)(badgePixel.X * snapshot.DpiX / 96), (int)(badgePixel.Y * snapshot.DpiY / 96), 1, 1), capturedBadge, 4, 0);
                    var badgeColor = ((SolidColorBrush)markerBadge.Background).Color;
                    Assert.Equal(255, capturedBadge[3]);
                    byte[] expectedBadge = [badgeColor.B, badgeColor.G, badgeColor.R];
                    for (int channel = 0; channel < 3; channel++)
                        Assert.InRange(Math.Abs(expectedBadge[channel] - capturedBadge[channel]), 0, 2); // Raster/effect rounding only; a displaced badge samples the distinctly different card background.
                    Assert.Equal(Visibility.Visible, lift.Visibility);
                    Assert.Equal(0, surface.Opacity); Assert.False(overlay.IsHitTestVisible); Assert.True(overlay.ClipToBounds);
                    var liftShadow = Assert.IsType<System.Windows.Media.Effects.DropShadowEffect>(lift.Effect);
                    Assert.True(liftShadow.Opacity > cardShadow.Opacity); Assert.True(liftShadow.ShadowDepth > cardShadow.ShadowDepth);
                    Assert.InRange(liftImage.Width / surface.ActualWidth, 0.9, SystemParameters.ClientAreaAnimation ? 1.0151 : 1.0001);
                    Assert.Equal(Transform.Identity, surface.LayoutTransform); Assert.IsType<TranslateTransform>(surface.RenderTransform);
                    Assert.Equal(new Thickness(2), lift.BorderThickness);
                    double firstY = ((TranslateTransform)lift.RenderTransform).Y;
                    view.MoveRowLift(new Point(start.X, start.Y + 24)); Layout();
                    Assert.Equal(firstY + 24, ((TranslateTransform)lift.RenderTransform).Y); Assert.Same(snapshot, liftImage.Source);
                    Render(view, theme, width, dpi, "lifted-row");
                    view.HideRowLift(); Layout(); // same cleanup used by leaving the valid drop area
                    Assert.Equal(Visibility.Collapsed, lift.Visibility); Assert.Equal(1, surface.Opacity);
                    view.MoveRowLift(start); Layout(); Assert.Equal(Visibility.Visible, lift.Visibility); // re-enter without recapturing
                    var cancelDrag = (QueryContinueDragEventArgs)Activator.CreateInstance(typeof(QueryContinueDragEventArgs),
                        BindingFlags.Instance | BindingFlags.NonPublic, null, [true, DragDropKeyStates.LeftMouseButton], null)!;
                    cancelDrag.Action = DragAction.Cancel; cancelDrag.RoutedEvent = DragDrop.QueryContinueDragEvent;
                    surface.RaiseEvent(cancelDrag);
                    Assert.Equal(Visibility.Collapsed, lift.Visibility); Assert.Equal(Transform.Identity, surface.RenderTransform);
                    Assert.Null(liftImage.Source); Assert.Equal(1, surface.Opacity); Assert.Null(Mouse.OverrideCursor);
                    // Capture a non-first row as well: parent layout offsets must not crop its bitmap.
                    var nextSurface = Descendants(rows).OfType<Border>().Single(b => b.Name == "RuleRowSurface" &&
                        b.DataContext is PersonalTradingJournal.Desktop.ViewModels.Rules.RuleRow r && r.Id == secondId);
                    Assert.True(view.BeginRowLift(nextSurface, start));
                    var nextSnapshot = Assert.IsType<RenderTargetBitmap>(liftImage.Source);
                    var interiorPixel = new byte[4]; int inset = (int)(12 * nextSnapshot.DpiX / 96);
                    nextSnapshot.CopyPixels(new Int32Rect(inset, inset, 1, 1), interiorPixel, 4, 0);
                    Assert.Equal(255, interiorPixel[3]); // Opaque card interior, not rounded corner/shadow or an offset blank visual.
                    view.AnimateReordering = false;
                    view.SetProspectiveIndex(0); Layout();
                    Assert.True(((TranslateTransform)surface.RenderTransform).Y > 0);
                    var announceDrop = (QueryContinueDragEventArgs)Activator.CreateInstance(typeof(QueryContinueDragEventArgs),
                        BindingFlags.Instance | BindingFlags.NonPublic, null, [false, DragDropKeyStates.None], null)!;
                    announceDrop.Action = DragAction.Drop; announceDrop.RoutedEvent = DragDrop.QueryContinueDragEvent;
                    nextSurface.RaiseEvent(announceDrop);
                    Assert.Equal(0, view.ProspectiveIndex); Assert.Equal(Visibility.Visible, lift.Visibility);
                    Render(view, theme, width, dpi, "prospective-order");
                    view.EndRowDrag(); Layout(); Assert.Equal(Transform.Identity, surface.RenderTransform);
                    Assert.True(view.BeginRowLift(nextSurface, start)); view.SetProspectiveIndex(0);
                    string feedbackBeforeMove = f.Vm.Message;
                    Await(view.DropProspectiveOrderAsync()); Layout();
                    Assert.Equal(Visibility.Collapsed, lift.Visibility); Assert.Null(liftImage.Source); Assert.Equal(1, surface.Opacity);
                    Assert.Equal(feedbackBeforeMove, f.Vm.Message); // Quiet move keeps the prior message on its original timer.
                    Assert.All(Descendants(view).OfType<RuleReorderRow>(), b => Assert.Equal(Transform.Identity, b.RenderTransform));
                    Assert.Equal(secondId, f.Vm.Rules[0].Id); Assert.Equal(1, f.Vm.Rules[0].DisplayNumber);
                    var movedRow = Descendants(rows).OfType<Border>().Single(b => b.Name == "RuleRowSurface" && b.DataContext is PersonalTradingJournal.Desktop.ViewModels.Rules.RuleRow r && r.Id == secondId);
                    SyntheticKeyboardState.WithAlt(() =>
                    {
                        var key = new KeyEventArgs(Keyboard.PrimaryDevice, new TestPresentationSource(view), 0, Key.Down) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                        movedRow.RaiseEvent(key); Assert.True(key.Handled);
                    });
                    Await(f.Vm.MoveDownCommand.ExecutionTask!); Layout();
                    Assert.Equal(feedbackBeforeMove, f.Vm.Message);
                    Assert.Equal(id, f.Vm.Rules[0].Id); Assert.Equal(secondId, f.Vm.Rules[1].Id);
                    // Failure clears temporary feedback without hiding the actionable error.
                    rowGrid = Descendants(rows).OfType<Grid>().Single(g => g.Name == "RuleRowLayout" && g.DataContext is PersonalTradingJournal.Desktop.ViewModels.Rules.RuleRow r && r.Id == id);
                    f.Data.Failure = "failure";
                    var failedSource = (Border)VisualTreeHelper.GetParent(rowGrid);
                    Assert.True(view.BeginRowLift(failedSource, start)); view.SetProspectiveIndex(1);
                    Await(view.DropProspectiveOrderAsync()); Layout(); f.Data.Failure = null;
                    Assert.Equal(Visibility.Collapsed, lift.Visibility); Assert.Null(liftImage.Source); Assert.Equal(1, failedSource.Opacity);
                    Assert.Contains("could not be moved", f.Vm.Message); Assert.Equal(id, f.Vm.Rules[0].Id);
                    Assert.All(Descendants(view).OfType<RuleReorderRow>(), b => Assert.Equal(Transform.Identity, b.RenderTransform));
                    Render(view, theme, width, dpi, "move-error");
                    Assert.Equal(12, f.Data.Rows.Count(r => r.RuleId == id)); // No revisions for moves.
                    rowGrid = Descendants(rows).OfType<Grid>().Single(g => g.Name == "RuleRowLayout" && g.DataContext is PersonalTradingJournal.Desktop.ViewModels.Rules.RuleRow r && r.Id == id);
                    var markers = Descendants(rows).OfType<TextBlock>().Where(t => t.Name == "RuleMarker").ToArray();
                    Assert.Equal(Enumerable.Range(1, 12).Select(n => n.ToString()), markers.Select(t => t.Text));
                    var tenth = markers.Single(t => t.Text == "10");
                    var badge = (Border)VisualTreeHelper.GetParent(tenth);
                    Assert.Equal(badge.ActualWidth, badge.ActualHeight);
                    Assert.InRange(tenth.ActualWidth, 1, badge.ActualWidth - 12);
                    Assert.Equal(Visibility.Collapsed, Find<TextBlock>(Descendants(rows).OfType<Grid>().Single(g => g.Name == "RuleRowLayout" &&
                        g.DataContext is PersonalTradingJournal.Desktop.ViewModels.Rules.RuleRow r && r.DisplayNumber == 10), "RuleCriterion").Visibility);
                    tenth.BringIntoView(); Flush(); Layout(); Render(view, theme, width, dpi, "double-digit");
                    scroller.ScrollToHome(); Flush(); Layout();
                    f.Vm.EditCommand.Execute(null); Layout();
                    Assert.Equal(((SolidColorBrush)app.Resources["PtjInformationSurfaceBrush"]).Color, ((SolidColorBrush)feedback.Background).Color);
                    var inline = Descendants(rowGrid).OfType<ContentControl>().Single(c => c.Name == "InlineEditorHost");
                    Assert.Same(f.Vm, inline.Content);
                    Assert.False(((ContentControl)view.FindName("CreateEditorHost")).HasContent);
                    Assert.InRange(inline.TranslatePoint(new Point(), rowGrid).Y, rowGrid.RowDefinitions[0].ActualHeight, rowGrid.ActualHeight);
                    var nextRow = Descendants(rows).OfType<Grid>().Single(g => g.Name == "RuleRowLayout" && g.DataContext is PersonalTradingJournal.Desktop.ViewModels.Rules.RuleRow r && r.Id == secondId);
                    Assert.True(inline.TranslatePoint(new Point(0, inline.ActualHeight), rows).Y < nextRow.TranslatePoint(new Point(), rows).Y);
                    foreach (string name in new[] { "CheckCapability", "CheckBasis", "CheckLimit", "CheckCurrency" })
                    {
                        Assert.Null(view.FindName(name));
                    }
                    Assert.Empty(Descendants(view).OfType<CheckBox>());
                    Assert.DoesNotContain(Descendants(view).OfType<Label>(), l => Equals(l.Content, "Required evidence"));
                    var fields = Descendants(Find<ItemsControl>(view, "EditorFields")).OfType<TextBox>().ToArray();
                    surface = (Border)VisualTreeHelper.GetParent(rowGrid);
                    Assert.All(fields, field => Assert.False(RulesView.CanStartRowDrag(field, surface)));
                    Assert.All(fields, field => Assert.Null(view.RowCursor(field, surface)));
                    Assert.False(RulesView.CanStartRowDrag(inline, surface));
                    Assert.Equal(2, fields.Length); Assert.Equal(100, fields[0].ActualHeight); Assert.Equal(100, fields[1].ActualHeight);
                    Assert.Equal(fields[0].ActualWidth, fields[1].ActualWidth);
                    Assert.Equal("Rule", AutomationProperties.GetName(fields[0]));
                    Assert.True(AutomationProperties.GetIsRequiredForForm(fields[0]));
                    Assert.False(AutomationProperties.GetIsRequiredForForm(fields[1]));
                    Assert.All(fields, field => { Assert.True(field.AcceptsReturn); Assert.Equal(TextWrapping.Wrap, field.TextWrapping); Assert.Equal(ScrollBarVisibility.Auto, field.VerticalScrollBarVisibility); });
                    fields[0].SetCurrentValue(TextBox.TextProperty, ""); Await(f.Vm.SaveCommand.ExecuteAsync(null)); Layout();
                    Assert.Equal(((SolidColorBrush)app.Resources["PtjDangerSurfaceBrush"]).Color, ((SolidColorBrush)feedback.Background).Color);
                    Assert.True(FormValidation.GetIsInvalid(fields[0])); Assert.Equal("This field is required.", AutomationProperties.GetItemStatus(fields[0]));
                    Assert.Equal(((SolidColorBrush)app.Resources["PtjDangerBrush"]).Color, ((SolidColorBrush)fields[0].BorderBrush).Color);
                    Assert.Equal(Visibility.Visible, Find<Border>(view, "ValidationSummary").Visibility);
                    fields[0].BringIntoView(); Flush(); Layout();
                    Assert.InRange(fields[0].TranslatePoint(new Point(), scroller).Y, -1, scroller.ViewportHeight);
                    Render(view, theme, width, dpi, "validation");
                    fields[0].SetCurrentValue(TextBox.TextProperty, "Required Rule\nwith multiple lines"); Layout();
                    Assert.False(FormValidation.GetIsInvalid(fields[0])); Assert.True(AutomationProperties.GetIsRequiredForForm(fields[0]));
                    fields[1].SetCurrentValue(TextBox.TextProperty, "Unsaved criterion");
                    foreach (var name in new[] { "CreateRule", "SaveRule" })
                    {
                        var button = Find<Button>(view, name); Assert.True(button.Focusable); Assert.NotEmpty(AutomationProperties.GetName(button));
                        Assert.NotNull(button.FocusVisualStyle);
                        Assert.InRange(button.TranslatePoint(new Point(button.ActualWidth, 0), view).X, 0, width);
                    }
                    Assert.InRange(scroller.ExtentWidth, 0, width); Assert.Equal(ScrollBarVisibility.Disabled, scroller.HorizontalScrollBarVisibility);
                    scroller.ScrollToHome(); Flush(); Layout();
                    var wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120) { RoutedEvent = UIElement.PreviewMouseWheelEvent };
                    fields[1].RaiseEvent(wheel); Flush(); Layout();
                    Assert.True(wheel.Handled); Assert.True(scroller.VerticalOffset > 0);
                    double offset = scroller.VerticalOffset; scroller.PageDown(); Flush(); Layout(); Assert.True(scroller.VerticalOffset > offset);
                    var save = Find<Button>(view, "SaveRule"); save.BringIntoView(); Flush(); Layout();
                    Assert.Same(f.Vm.SaveCommand, save.Command);
                    Assert.InRange(save.TranslatePoint(new Point(save.ActualWidth, save.ActualHeight), scroller).Y, 0, scroller.ViewportHeight + 1);
                    Render(view, theme, width, dpi, "editor");
                    // Guarded switching leaves this exact inline form and draft intact on veto.
                    Await(f.Vm.EditRowCommand.ExecuteAsync(f.Vm.Rules.Single(r => r.Id == secondId))); Layout();
                    Assert.Same(f.Vm, inline.Content); Assert.Equal("Unsaved criterion", fields[1].Text);
                    f.Dialogs.ConfirmationResult = true;
                    Await(f.Vm.EditRowCommand.ExecuteAsync(f.Vm.Rules.Single(r => r.Id == secondId))); Layout();
                    Assert.False(inline.HasContent);
                    var nextHost = Descendants(nextRow).OfType<ContentControl>().Single(c => c.Name == "InlineEditorHost");
                    Assert.Same(f.Vm, nextHost.Content);
                    Assert.Single(Descendants(view).OfType<StackPanel>(), p => p.Name == "Editor");
                    f.Vm.CancelEditCommand.Execute(null); Layout(); Assert.False(nextHost.HasContent);
                    f.Vm.CreateCommand.Execute(null); Layout();
                    var create = (ContentControl)view.FindName("CreateEditorHost");
                    Assert.Same(f.Vm, create.Content);
                    Assert.True(create.TranslatePoint(new Point(0, create.ActualHeight), view).Y <= rows.TranslatePoint(new Point(), view).Y);
                    Assert.All(f.Vm.Fields, field => Assert.Empty(field.Text));
                    scroller.ScrollToHome(); Flush(); Layout(); Render(view, theme, width, dpi, "create");
                    f.Fill(); Await(f.Vm.SaveCommand.ExecuteAsync(null)); Layout();
                    Assert.False(create.HasContent); Assert.DoesNotContain(Descendants(view).OfType<StackPanel>(), p => p.Name == "Editor");
                    f.Vm.Deactivate();
                }
            }
            // Logical field focus and bring-into-view on a real WPF presentation source,
            // without asserting ownership of the Windows foreground keyboard device.
            var clock = new FeedbackClock(); var focus = new RulesFixture(clock); Await(focus.Vm.ActivateAsync());
            var focusView = new RulesView { DataContext = focus.Vm };
            var focusWindow = new Window { Content = focusView, Width = 760, Height = 640, ShowActivated = false };
            focusWindow.Show();
            try
            {
                // A connected presentation source receives Application resource invalidation;
                // detached render-only roots above do not represent live theme switching.
                var loadedMotto = (TextBlock)focusView.FindName("RulesMotto");
                foreach (string changedTheme in new[] { "Light", "Dark" })
                {
                    app.Resources = CalendarViewLayoutTests.SharedThemeResources(changedTheme);
                    Flush(); focusWindow.UpdateLayout();
                    Assert.Equal(((SolidColorBrush)app.Resources["PtjTextPrimaryBrush"]).Color, ((SolidColorBrush)loadedMotto.Foreground).Color);
                    Assert.True(Contrast(((SolidColorBrush)loadedMotto.Foreground).Color, ((SolidColorBrush)focusView.Background).Color) >= 7);
                }
                focus.Vm.CreateCommand.Execute(null); Flush(); focusWindow.UpdateLayout();
                focus.Vm.Fields[1].Text = "Optional text cannot replace the required Rule";
                Await(focus.Vm.SaveCommand.ExecuteAsync(null)); Flush(); focusWindow.UpdateLayout();
                var invalid = Descendants(focusView).OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == "Rule");
                Assert.True(FormValidation.GetIsInvalid(invalid));
                Assert.Same(invalid, FocusManager.GetFocusedElement(focusWindow));
                clock.Advance(10); Await(focus.Vm.FeedbackExpiryTask); Flush(); focusWindow.UpdateLayout();
                Assert.False(focus.Vm.HasMessage); Assert.True(FormValidation.GetIsInvalid(invalid));
                Assert.Same(invalid, FocusManager.GetFocusedElement(focusWindow));
                var scroll = (ScrollViewer)focusView.FindName("RulesScroller");
                Assert.InRange(invalid.TranslatePoint(new Point(), scroll).Y, 0, scroll.ViewportHeight - invalid.ActualHeight + 1);
                focus.Fill(); focus.Vm.Fields[1].Text = string.Join("\n", Enumerable.Repeat("Complete saved Criterion line", 45));
                Await(focus.Vm.SaveCommand.ExecuteAsync(null)); Flush(); focusWindow.UpdateLayout();
                var shownCriterion = Find<TextBlock>(focusView, "RuleCriterion");
                Assert.True(shownCriterion.IsVisible);
                Assert.Equal(shownCriterion.Text, shownCriterion.ToolTip);
                Assert.Equal(shownCriterion.Text, UIElementAutomationPeer.CreatePeerForElement(shownCriterion)!.GetName());
                var shownTextScroller = Find<ScrollViewer>(focusView, "RuleTextScroller");
                shownTextScroller.Focus(); Assert.Same(shownTextScroller, FocusManager.GetFocusedElement(focusWindow));
                var pageDown = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(focusView)!, 0, Key.PageDown) { RoutedEvent = Keyboard.KeyDownEvent };
                shownTextScroller.RaiseEvent(pageDown); Flush(); focusWindow.UpdateLayout();
                Assert.True(pageDown.Handled); Assert.True(shownTextScroller.VerticalOffset > 0);
                Await(focus.Vm.EditRowCommand.ExecuteAsync(Assert.Single(focus.Vm.Rules))); Flush(); focusWindow.UpdateLayout();
                var selectedRow = Descendants(focusView).OfType<Grid>().Single(g => g.Name == "RuleRowLayout");
                Assert.InRange(selectedRow.TranslatePoint(new Point(), scroll).Y, 0, scroll.ViewportHeight);
                // A clean Edit -> Create transition keeps IsEditing true but changes the form host.
                focus.Vm.CreateCommand.Execute(null); Flush(); focusWindow.UpdateLayout();
                var createName = Descendants((ContentControl)focusView.FindName("CreateEditorHost")).OfType<TextBox>().First();
                Assert.Empty(createName.Text); Assert.Same(createName, FocusManager.GetFocusedElement(focusWindow));
                Assert.InRange(createName.TranslatePoint(new Point(), scroll).Y, 0, scroll.ViewportHeight - createName.ActualHeight + 1);
            }
            finally { focusWindow.Close(); focus.Vm.Deactivate(); }
            // Exercise the actual MainWindow.OnClosing override, without native activation assumptions.
            var dirty = new RulesFixture(); using var shell = MainWindowViewModelTests.RulesShell(dirty.Vm);
            shell.NavigateCommand.Execute(NavigationDestination.Rules); shell.CurrentContentViewModel.ToString(); Await(dirty.Vm.LoadTask);
            dirty.Vm.CreateCommand.Execute(null); dirty.Fill();
            var window = new MainWindow(shell);
            var closing = typeof(MainWindow).GetMethod("OnClosing", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var veto = new CancelEventArgs(); closing.Invoke(window, [veto]); Assert.True(veto.Cancel); Assert.True(dirty.Vm.IsEditing);
            dirty.Dialogs.ConfirmationResult = true; var accepted = new CancelEventArgs(); closing.Invoke(window, [accepted]);
            Assert.False(accepted.Cancel); Assert.Equal("Recorded criterion", dirty.Vm.Fields[1].Text); window.Close();
        }, "Rules Light/Dark normal/high-DPI and guarded window close");
    }
    private static T Find<T>(DependencyObject root, string name) where T : FrameworkElement =>
        Descendants(root).OfType<T>().Single(e => e.Name == name);

    private static double Contrast(Color foreground, Color background)
    {
        static double Luminance(Color color)
        {
            static double Channel(byte value) => value / 255d <= .04045 ? value / 255d / 12.92 : Math.Pow((value / 255d + .055) / 1.055, 2.4);
            return .2126 * Channel(color.R) + .7152 * Channel(color.G) + .0722 * Channel(color.B);
        }
        double a = Luminance(foreground), b = Luminance(background);
        return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }

    private static void Await(Task task)
    {
        // Await actual completion while servicing the same UI dispatcher; no sleeps or retries.
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            var dispatcher = Dispatcher.CurrentDispatcher;
            _ = task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)), TaskScheduler.Default);
            Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult(); // The existing supervised 30-second STA deadline still applies.
    }

    private static void Flush()
    {
        var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame);
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
    internal static void Render(FrameworkElement view, string theme, int width, int dpi, string state)
    {
        var bitmap = new RenderTargetBitmap(width * dpi / 96, (int)view.ActualHeight * dpi / 96, dpi, dpi, PixelFormats.Pbgra32); bitmap.Render(view);
        if (Environment.GetEnvironmentVariable("PTJ_RULES_RENDER_DIRECTORY") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(output, $"rules-{theme}-{width}-{dpi}-{state}.png")); encoder.Save(stream);
    }
}
