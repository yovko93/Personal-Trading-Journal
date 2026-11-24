using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.Notebook;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Views.Setups;

namespace PersonalTradingJournal.Desktop.Tests.Setups;

[Collection(ExclusiveWpfCollection.Name)]
[Trait(ExclusiveWpfCollection.Trait, ExclusiveWpfCollection.Value)]
public sealed class SetupCardInputTests
{
    [Fact]
    public async Task MouseAndSpaceOpenOriginalAndReturnFocusWithoutRemovingExample()
    {
        const string host = "PTJ_SETUP_CARD_INPUT_HOST";
        if (Environment.GetEnvironmentVariable(host) != "1")
        {
            await IsolatedTestProcess.RunSuiteAsync(typeof(SetupCardInputTests), "setup-card-input", host,
                TimeSpan.FromSeconds(60), caseHangTimeout: TimeSpan.FromSeconds(30));
            return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown,
                Resources = CalendarViewLayoutTests.SharedThemeResources("Light") };
            var setup = new TradingSetupDetails(Guid.NewGuid(), "Retest", "Support", true, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
            var images = new SetupExampleViewTests.Images();
            var example = images.AddAsync(setup.Id, "Bounce.png", [1], "1h", default).GetAwaiter().GetResult();
            var dialogs = new FakeDialogService();
            var vm = TradingSetupsViewModelTests.Create(new FakeTradingSetupReader { DetailsToReturn = setup }, dialog: dialogs,
                examples: images, viewer: images);
            vm.ViewCommand.ExecuteAsync(setup.Id).GetAwaiter().GetResult();
            var view = new TradingSetupsView { DataContext = vm };
            var window = NotebookMenuLayout.Window(view, 760, 680);
            try
            {
                window.Show(); window.UpdateLayout();
                var open = SetupExampleViewTests.Children(view).OfType<Button>().Single(b => b.Name == "OpenExampleCard");
                var back = (Button)view.FindName("BackToCards");
                Assert.Equal("Open Bounce.png, 1h", AutomationProperties.GetName(open));
                UiKeyboardProbe.Ready(open);
                Assert.Same(app.Resources["PtjTextPrimaryBrush"], ((Border)open.Template.FindName("FocusIndicator", open)).BorderBrush);
                var hwnd = ((HwndSource)PresentationSource.FromVisual(open)).Handle;
                var point = open.PointToScreen(new Point(open.ActualWidth / 2, open.ActualHeight / 2));
                var screen = new NativePoint((int)point.X, (int)point.Y);
                Assert.Equal(hwnd, WindowFromPoint(screen));
                Assert.True(SetCursorPos(screen.X, screen.Y));
                Until(() => open.IsMouseOver && new Rect(open.RenderSize).Contains(Mouse.GetPosition(open)));
                Assert.Equal(hwnd, WindowFromPoint(screen));
                Assert.Equal(hwnd, GetForegroundWindow());
                Assert.Equal(2u, SendInput(2, [new() { Mouse = new() { Flags = 2 } }, new() { Mouse = new() { Flags = 4 } }], Marshal.SizeOf<NativeInput>()));
                VerifyOpenAndReturn();
                UiKeyboardProbe.Space(open);
                VerifyOpenAndReturn();

                void VerifyOpenAndReturn()
                {
                    Until(() => vm.Preview?.Card.Example.Id == example.Id && back.IsKeyboardFocused);
                    Assert.Null(dialogs.ConfirmationRequest);
                    Assert.Single(images.Rows);
                    UiKeyboardProbe.Space(back);
                    Until(() => vm.Preview is null && open.IsKeyboardFocused);
                }
            }
            finally { window.Close(); app.Shutdown(); }
        });
    }

    private static void Until(Func<bool> predicate)
    {
        var clock = Stopwatch.StartNew();
        while (!predicate() && clock.Elapsed < TimeSpan.FromSeconds(3))
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        Assert.True(predicate(), "The expected card input or focus transition did not complete.");
    }

    [StructLayout(LayoutKind.Sequential)] private readonly record struct NativePoint(int X, int Y);
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeInput { public uint Type; public MouseInput Mouse; }
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(NativePoint point);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint SendInput(uint count, NativeInput[] input, int size);
}
