using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

[Collection(ExclusiveWpfCollection.Name)]
[Trait(ExclusiveWpfCollection.Trait, ExclusiveWpfCollection.Value)]
public sealed class NotebookMouseReadinessEvidenceTests
{
    [Theory]
    [InlineData("capture")]
    [InlineData("write")]
    [InlineData("directory")]
    public void DiagnosticErrorsPreserveReadinessAssertionAndCleanup(string failure)
    {
        string directory = EvidenceDirectory();
        Directory.CreateDirectory(directory);
        string blocked = Path.Combine(directory, "not-a-directory");
        File.WriteAllText(blocked, "occupied");
        bool cleaned = false; bool writeAttempted = false;
        var error = Assert.Throws<Xunit.Sdk.TrueException>(() =>
        {
            try
            {
                NotebookMouseReadinessEvidence.AssertReady(false, () => NotebookMouseReadinessEvidence.Retain(
                    failure == "directory" ? blocked : directory,
                    () => failure == "capture" ? throw new InvalidOperationException("capture probe") : new() { ["probe"] = failure },
                    (_, _) => { writeAttempted = true; throw new IOException("write probe"); }));
            }
            finally { cleaned = true; }
        });
        Assert.Equal(NotebookMouseReadinessEvidence.FailureMessage, error.Message);
        Assert.True(cleaned);
        Assert.Equal(failure == "write", writeAttempted);
        Assert.Empty(Directory.GetFiles(directory, "readiness-failure-*.json"));
    }

    [Fact]
    public void SuccessfulReadinessDoesNotCollectOrWriteEvidence()
    {
        bool collected = false;
        NotebookMouseReadinessEvidence.AssertReady(true, () => collected = true);
        Assert.False(collected);
    }

    [Fact]
    public async Task FailureSnapshotRetainsPopupStateBeforeCleanupWithoutInputOrDispatcherWork()
    {
        const string host = "PTJ_NOTEBOOK_READINESS_EVIDENCE_HOST";
        if (Environment.GetEnvironmentVariable(host) != "1")
        {
            await IsolatedTestProcess.RunSuiteAsync(typeof(NotebookMouseReadinessEvidenceTests), "notebook-readiness-evidence", host,
                TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30),
                testCaseFilter: $"FullyQualifiedName={typeof(NotebookMouseReadinessEvidenceTests).FullName}.{nameof(FailureSnapshotRetainsPopupStateBeforeCleanupWithoutInputOrDispatcherWork)}");
            return;
        }
        string directory = EvidenceDirectory();
        await CalendarStaTest.RunAsync(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var item = new MenuItem { Name = "DuplicateNoteButton", Header = "Duplicate" };
            var menu = new ContextMenu { StaysOpen = true }; menu.Items.Add(item);
            var button = new Button { Content = "Fixture", ContextMenu = menu };
            var window = new Window { Content = button, Width = 180, Height = 100, ShowActivated = false, ShowInTaskbar = false };
            bool queuedWorkRan = false; int mouseEvents = 0;
            DispatcherOperation? queuedProbe = null;
            void Input(object sender, ProcessInputEventArgs args) => mouseEvents++;
            try
            {
                window.Show(); menu.PlacementTarget = button; menu.IsOpen = true; menu.UpdateLayout();
                Assert.True(item.IsLoaded); Assert.IsType<HwndSource>(PresentationSource.FromVisual(item));
                var point = item.PointToScreen(new Point(item.ActualWidth / 2, item.ActualHeight / 2));
                Assert.True(GetCursorPos(out var cursor));
                var capture = Mouse.Captured; var directlyOver = Mouse.DirectlyOver;
                var foreground = GetForegroundWindow();
                var observedAt = DateTimeOffset.UtcNow;
                queuedProbe = window.Dispatcher.BeginInvoke(DispatcherPriority.Send, () => queuedWorkRan = true);
                InputManager.Current.PostProcessInput += Input;
                using var movement = new NotebookMouseMovementTrace(item, directory);
                var error = Assert.Throws<Xunit.Sdk.TrueException>(() => NotebookMouseReadinessEvidence.AssertReady(false,
                    () => NotebookMouseReadinessEvidence.Retain(directory, () => NotebookMouseReadinessEvidence.Capture(
                        item, window, menu, "synthetic-boundary-probe", "probe", 180, 100, 96, point, true,
                        observedAt, TimeSpan.FromSeconds(3)))));
                Assert.Equal(NotebookMouseReadinessEvidence.FailureMessage, error.Message);
                Assert.False(queuedWorkRan); Assert.Equal(0, mouseEvents);
                Assert.True(GetCursorPos(out var after)); Assert.Equal(cursor, after);
                Assert.Same(capture, Mouse.Captured); Assert.Same(directlyOver, Mouse.DirectlyOver);
                Assert.Equal(foreground, GetForegroundWindow()); Assert.True(menu.IsOpen); Assert.True(window.IsVisible);
                using var json = JsonDocument.Parse(File.ReadAllText(Assert.Single(Directory.GetFiles(directory, "readiness-failure-*.json"))));
                var root = json.RootElement;
                Assert.Equal("synthetic-boundary-probe", root.GetProperty("identity").GetString());
                Assert.Equal("180", root.GetProperty("widthDip").GetString());
                Assert.Equal("100", root.GetProperty("heightDip").GetString());
                Assert.Equal(Environment.ProcessId.ToString(), root.GetProperty("processId").GetString());
                Assert.Equal(observedAt.ToString("O"), root.GetProperty("observedAtUtc").GetString());
                Assert.Equal("True", root.GetProperty("menuOpen").GetString());
                Assert.Equal("True", root.GetProperty("targetLoaded").GetString());
                Assert.Contains("DuplicateNoteButton", root.GetProperty("target").GetString());
                Assert.Contains("PopupRoot", root.GetProperty("sourceRoot").GetString());
                Assert.Contains("open=True", root.GetProperty("popup").GetString());
                Assert.Contains($"pid={Environment.ProcessId}", root.GetProperty("expectedWindow").GetString());
                foreach (string field in new[] { "nativeCursorScreenPoint", "targetScreenCorners", "targetDpiScale",
                    "transformToDevice", "transformFromDevice", "targetIsMouseOver", "targetIsMouseDirectlyOver",
                    "mouseDirectlyOver", "mouseCaptured", "wpfInputHitAtCursor", "wpfVisualHitAtCursor", "foregroundWindow",
                    "managedMousePositionInTarget", "managedMousePositionInSourceRoot", "managedCaptureMode" })
                    Assert.DoesNotContain("unavailable", root.GetProperty(field).GetString());
            }
            finally
            {
                InputManager.Current.PostProcessInput -= Input;
                queuedProbe?.Abort();
                // The probe intentionally did not pump during collection. Let its own
                // animated popup finish closing before tearing down the dispatcher.
                var closed = new DispatcherFrame();
                var deadline = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromSeconds(3) };
                void Closed(object sender, RoutedEventArgs args) => closed.Continue = false;
                void Expired(object? sender, EventArgs args) => closed.Continue = false;
                menu.Closed += Closed; deadline.Tick += Expired;
                try
                {
                    deadline.Start(); menu.IsOpen = false;
                    if (closed.Continue) Dispatcher.PushFrame(closed);
                }
                finally
                {
                    deadline.Stop(); deadline.Tick -= Expired; menu.Closed -= Closed;
                    window.Close(); app.Shutdown();
                }
            }
        });
    }

    private static string EvidenceDirectory() => Path.Combine(
        Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY") ?? Path.Combine(AppContext.BaseDirectory, "TestResults"),
        "readiness-diagnostic-probes", Guid.NewGuid().ToString("N"));

    [StructLayout(LayoutKind.Sequential)] private readonly record struct NativePoint(int X, int Y);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
}
