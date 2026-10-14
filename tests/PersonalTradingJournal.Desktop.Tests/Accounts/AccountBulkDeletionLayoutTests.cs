using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.Dialogs;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Views.Accounts;

namespace PersonalTradingJournal.Desktop.Tests.Accounts;

public sealed class AccountBulkDeletionLayoutTests
{
    private static readonly Lazy<Task> Host = new(() => IsolatedTestProcess.RunSuiteAsync(
        typeof(AccountBulkDeletionLayoutTests), "account-bulk-layout", "PTJ_ACCOUNT_BULK_HOST", TimeSpan.FromMinutes(2),
        caseHangTimeout: TimeSpan.FromSeconds(30)));

    [Theory]
    [InlineData("Light", 960, 96)]
    [InlineData("Dark", 960, 96)]
    [InlineData("Light", 480, 240)]
    [InlineData("Dark", 480, 240)]
    public async Task DestructiveMenuOrderExactBindingKeyboardAndDialogFit(string theme, int width, int dpi)
    {
        if (Environment.GetEnvironmentVariable("PTJ_ACCOUNT_BULK_HOST") != "1") { await Host.Value; return; }
        var store = new AccountBulkDeletionTests.Store();
        var dialogs = new FakeDialogService();
        var reader = new FakeTradingAccountReader();
        reader.EnqueueDetailResult(new(store.Account, "Synthetic exact Account", PersonalTradingJournal.Domain.Accounts.TradingAccountType.Personal,
            null, null, "USD", 0m, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var vm = AccountBulkDeletionTests.Create(store, dialogs, new(), reader);
        await vm.EnsureLoadedAsync();
        await vm.DeleteAllTradesCommand.ExecuteAsync(store.Account); // Capture cancelled confirmation only.
        await CalendarStaTest.RunAsync(() =>
        {
            var app = System.Windows.Application.Current ?? new() { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
            var view = new AccountsView { DataContext = vm };
            var root = new Border { Child = view };
            VisualTreeHelper.SetRootDpi(root, new DpiScale(dpi / 96d, dpi / 96d));
            root.SetResourceReference(Border.BackgroundProperty, "PtjBackgroundBrush");
            root.Measure(new(width, 760)); root.Arrange(new(0, 0, width, 760)); root.UpdateLayout();
            var button = Descendants(view).OfType<Button>().Single(b => b.ContextMenu is not null && b.DataContext is PersonalTradingJournal.Application.Accounts.AccountListItem);
            var menu = button.ContextMenu!;
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(menu.IsOpen); Assert.Same(button, menu.PlacementTarget);
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            // Popup's normal DataContext binding references its PlacementTarget.
            menu.ApplyTemplate(); menu.Measure(new(width, 760)); menu.Arrange(new(0, 0, menu.DesiredSize.Width, menu.DesiredSize.Height));
            var items = menu.Items.OfType<MenuItem>().ToArray();
            var edit = Assert.Single(items, i => Equals(i.Header, "Edit"));
            Assert.Same(vm.EditAccountCommand, edit.Command); Assert.Equal(store.Account, edit.CommandParameter);
            Assert.Equal(vm.EditAccountCommand.CanExecute(store.Account), edit.IsEnabled);
            Assert.True(edit.Focusable); Assert.Contains("Synthetic exact Account", AutomationProperties.GetName(edit));
            Assert.DoesNotContain(Descendants(view).OfType<Button>(), b => b.DataContext is PersonalTradingJournal.Application.Accounts.AccountListItem && ReferenceEquals(b.Command, vm.EditAccountCommand));
            Assert.Contains(Descendants(view).OfType<Button>(), b => b.DataContext is PersonalTradingJournal.Application.Accounts.AccountListItem && ReferenceEquals(b.Command, vm.ViewAccountCommand));
            var bulk = items.Single(i => Equals(i.Header, "Delete All Trades"));
            var delete = items.Single(i => Equals(i.Header, "Delete"));
            Assert.Equal(Array.IndexOf(items, bulk) + 1, Array.IndexOf(items, delete));
            Assert.Same(vm.DeleteAllTradesCommand, bulk.Command);
            Assert.Equal(store.Account, bulk.CommandParameter);
            Assert.True(bulk.Focusable); Assert.True(bulk.IsEnabled);
            Assert.Equal(FontWeights.SemiBold, bulk.FontWeight);
            Assert.NotSame(bulk.Style, delete.Style);
            Assert.Contains("Trades", AutomationProperties.GetName(bulk));
            Assert.True(menu.DesiredSize.Width <= width);
            store.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var busy = vm.DeleteAllTradesCommand.ExecuteAsync(store.Account);
            Assert.True(vm.IsDeleting); Assert.False(edit.IsEnabled);
            store.Pending.SetResult(store.Plan); Await(busy); store.Pending = null;
            Assert.True(edit.IsEnabled); Assert.Equal(0, store.Deletes);
            Render(root, theme, width, dpi, "accounts");
            Render(menu, theme, (int)Math.Ceiling(menu.ActualWidth), dpi, "menu");
            FocusManager.SetFocusedElement(menu, edit); Assert.Same(edit, FocusManager.GetFocusedElement(menu));
            var invoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            edit.Click += (_, _) => invoked.TrySetResult();
            var peer = UIElementAutomationPeer.CreatePeerForElement(edit)!;
            ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)!).Invoke();
            Await(invoked.Task); if (vm.EditAccountCommand.ExecutionTask is { } task) Await(task);
            Assert.Equal(1, reader.DetailCallCount); Assert.Equal(store.Account, vm.SelectedAccount!.Id);
            Assert.True(vm.IsEditFormVisible); Assert.Equal("Synthetic exact Account", vm.EditAccountName);
            vm.CancelEditCommand.Execute(null); Assert.False(vm.IsEditFormVisible);
            menu.IsOpen = false;
            var dialog = new PtjDialogWindow(dialogs.ConfirmationRequest!);
            try
            {
                var panel = (FrameworkElement)dialog.Content;
                panel.Measure(new(440, 760)); panel.Arrange(new(0, 0, 440, panel.DesiredSize.Height)); panel.UpdateLayout();
                var cancel = (Button)dialog.FindName("CancelButton");
                var confirm = (Button)dialog.FindName("PrimaryButton");
                Assert.True(cancel.IsDefault); Assert.True(cancel.IsCancel);
                Assert.True(cancel.Focusable); Assert.True(confirm.Focusable);
                Assert.Same(app.Resources["PtjDangerButtonStyle"], confirm.Style);
                // Logical focus checks do not assume native foreground activation on headless CI.
                FocusManager.SetFocusedElement(dialog, cancel);
                Assert.Same(cancel, FocusManager.GetFocusedElement(dialog));
                Assert.True(cancel.TranslatePoint(new(cancel.ActualWidth, 0), panel).X <= confirm.TranslatePoint(new(0, 0), panel).X);
                Assert.InRange(confirm.TranslatePoint(new(confirm.ActualWidth, 0), panel).X, 0, 440);
                Assert.InRange(panel.ActualHeight, 100, 760);
                Render(panel, theme, 440, dpi, "confirmation");
            }
            finally { dialog.Close(); root.Child = null; }
        }, shutdownDispatcher: false);
        Assert.Equal(0, store.Deletes);
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
    private static void Render(FrameworkElement root, string theme, int width, int dpi, string name)
    {
        var bitmap = new RenderTargetBitmap(Math.Max(1, width * dpi / 96), Math.Max(1, (int)Math.Ceiling(root.ActualHeight * dpi / 96)), dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(root);
        if (Environment.GetEnvironmentVariable("PTJ_ACCOUNT_RENDER_DIRECTORY") is not { Length: > 0 } directory) return;
        Directory.CreateDirectory(directory);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(directory, $"bulk-{name}-{theme}-{width}-{dpi}.png")); encoder.Save(file);
    }
}
