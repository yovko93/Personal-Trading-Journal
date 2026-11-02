using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

internal static class NotebookMouseReadinessEvidence
{
    internal const string FailureMessage = "The intended control did not become hit-testable with settled popup animation and mouse-over.";

    // Only called after the final readiness observation, before cleanup. Never repair input
    // or pump the dispatcher here. Diagnostics must not replace the original assertion.
    internal static void AssertReady(bool ready, Action retainFailure)
    {
        if (!ready) BestEffort(retainFailure);
        Assert.True(ready, FailureMessage);
    }

    internal static void BestEffort(Action action)
    {
        try { action(); }
        catch (Exception) { /* Preserve assertion and normal fixture cleanup, even for failed artifact writes. */ }
    }

    internal static void Retain(string directory, Func<Dictionary<string, string>> capture,
        Action<string, string>? write = null)
    {
        BestEffort(() =>
        {
            var snapshot = capture();
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, $"readiness-failure-{Environment.ProcessId}-{Guid.NewGuid():N}.json");
            (write ?? File.WriteAllText)(path, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        });
    }

    internal static Dictionary<string, string> Capture(FrameworkElement target, Window window, ContextMenu menu,
        string identity, string theme, int width, int height, int dpi, Point intended, bool settled,
        DateTimeOffset observedAt, TimeSpan elapsed)
    {
        var result = new Dictionary<string, string>();
        void Read(string key, Func<object?> read)
        {
            try
            {
                string text = Convert.ToString(read(), System.Globalization.CultureInfo.InvariantCulture) ?? "null";
                result[key] = text.Length > 4096 ? text[..4096] : text;
            }
            catch (Exception error) { result[key] = $"unavailable: {error.GetType().Name}"; }
        }
        var source = PresentationSource.FromVisual(target) as HwndSource;
        bool cursorRead = GetCursorPos(out var cursor);
        var cursorPoint = new Point(cursor.X, cursor.Y);
        Read("observedAtUtc", () => observedAt.ToString("O"));
        Read("capturedAtUtc", () => DateTimeOffset.UtcNow.ToString("O"));
        Read("identity", () => identity);
        Read("processId", () => Environment.ProcessId);
        Read("managedThreadId", () => Environment.CurrentManagedThreadId);
        Read("theme", () => theme); Read("widthDip", () => width); Read("heightDip", () => height); Read("fixtureDpi", () => dpi);
        Read("testAssemblyInformationalVersion", () => typeof(NotebookMouseReadinessEvidence).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
        Read("githubSha", () => Environment.GetEnvironmentVariable("GITHUB_SHA"));
        Read("githubRunId", () => Environment.GetEnvironmentVariable("GITHUB_RUN_ID"));
        Read("readinessElapsed", () => elapsed); Read("settled", () => settled);
        Read("readinessResult", () => false);
        Read("intendedScreenPoint", () => intended);
        Read("injectedIntegerPoint", () => $"{(int)intended.X},{(int)intended.Y}");
        Read("cursorRead", () => cursorRead); Read("nativeCursorScreenPoint", () => cursorRead ? cursorPoint : null);
        Read("target", () => Describe(target));
        Read("targetScreenCorners", () => string.Join("; ", new[] { new Point(), new Point(target.ActualWidth, 0),
            new Point(target.ActualWidth, target.ActualHeight), new Point(0, target.ActualHeight) }.Select(target.PointToScreen)));
        Read("targetRenderSize", () => target.RenderSize);
        Read("targetDpiScale", () => { var d = VisualTreeHelper.GetDpi(target); return $"{d.DpiScaleX},{d.DpiScaleY}"; });
        Read("transformToDevice", () => source?.CompositionTarget?.TransformToDevice);
        Read("transformFromDevice", () => source?.CompositionTarget?.TransformFromDevice);
        Read("targetLoaded", () => target.IsLoaded); Read("targetVisibility", () => target.Visibility);
        Read("targetVisible", () => target.IsVisible); Read("targetEnabled", () => target.IsEnabled);
        Read("targetHitTestVisible", () => target.IsHitTestVisible);
        Read("targetMeasureValid", () => target.IsMeasureValid); Read("targetArrangeValid", () => target.IsArrangeValid);
        Read("targetIsMouseOver", () => target.IsMouseOver); Read("targetIsMouseDirectlyOver", () => target.IsMouseDirectlyOver);
        Read("mouseDirectlyOver", () => Ancestry(Mouse.DirectlyOver as DependencyObject));
        Read("mouseCaptured", () => Ancestry(Mouse.Captured as DependencyObject));
        Read("mouseLeftButton", () => Mouse.LeftButton);
        Read("activeMouseSource", () => Mouse.PrimaryDevice.ActiveSource is HwndSource s ? NativeWindow(s.Handle) : "null/non-HWND");
        Read("nativeCaptureCurrentThread", () => NativeWindow(GetCapture()));
        Read("expectedWindow", () => NativeWindow(source?.Handle ?? IntPtr.Zero));
        Read("windowAtIntendedPoint", () => NativeWindow(WindowFromPoint(new((int)intended.X, (int)intended.Y))));
        Read("windowAtNativeCursor", () => cursorRead ? NativeWindow(WindowFromPoint(cursor)) : "unavailable");
        Read("foregroundWindow", () => NativeWindow(GetForegroundWindow()));
        Read("fixtureWindow", () => NativeWindow(new WindowInteropHelper(window).Handle));
        Read("fixtureWindowDip", () => $"{window.Left},{window.Top}; size={window.ActualWidth},{window.ActualHeight}");
        Read("fixtureActive", () => window.IsActive);
        Read("targetVisualAncestry", () => Ancestry(target));
        Read("sourceRoot", () => Describe(source?.RootVisual));
        Read("cursorInSourceRoot", () => cursorRead && source?.RootVisual is UIElement root ? root.PointFromScreen(cursorPoint) : null);
        Read("cursorInTarget", () => cursorRead ? target.PointFromScreen(cursorPoint) : null);
        // Hit-test the popup's own presentation root, not the owner Window (a different HWND).
        Read("wpfInputHitAtCursor", () => cursorRead && source?.RootVisual is UIElement root
            ? Ancestry(root.InputHitTest(root.PointFromScreen(cursorPoint)) as DependencyObject) : "unavailable");
        Read("wpfVisualHitAtCursor", () => cursorRead && source?.RootVisual is UIElement root
            ? Ancestry(VisualTreeHelper.HitTest(root, root.PointFromScreen(cursorPoint))?.VisualHit) : "unavailable");
        Read("menu", () => Describe(menu)); Read("menuOpen", () => menu.IsOpen);
        Read("menuLoaded", () => menu.IsLoaded); Read("menuVisible", () => menu.IsVisible);
        Read("menuPlacement", () => $"{menu.Placement}; offsets={menu.HorizontalOffset},{menu.VerticalOffset}; target={Describe(menu.PlacementTarget)}");
        Read("popup", () => DescribePopup(source?.RootVisual));
        Read("dispatcherShutdown", () => $"{target.Dispatcher.HasShutdownStarted}/{target.Dispatcher.HasShutdownFinished}");
        return result;
    }

    private static string Describe(DependencyObject? value) => value is null ? "null" :
        $"{value.GetType().FullName}#{(value as FrameworkElement)?.Name}";

    private static string Ancestry(DependencyObject? value)
    {
        if (value is null) return "null";
        var nodes = new List<string>();
        for (int i = 0; value is not null && i < 24; i++)
        {
            nodes.Add(Describe(value) + (value is UIElement element
                ? $"[visible={element.IsVisible},enabled={element.IsEnabled},hitTestVisible={element.IsHitTestVisible},opacity={element.Opacity},transform={element.RenderTransform?.Value},animated={element.RenderTransform?.HasAnimatedProperties}]" : ""));
            value = value is Visual ? VisualTreeHelper.GetParent(value) : LogicalTreeHelper.GetParent(value);
        }
        if (value is not null) nodes.Add("[truncated]");
        return string.Join(" -> ", nodes);
    }

    private static string DescribePopup(DependencyObject? root)
    {
        var popup = root is null ? null : LogicalTreeHelper.GetParent(root) as Popup;
        return popup is null ? "no Popup logical parent" :
            $"open={popup.IsOpen}; loaded={popup.IsLoaded}; animation={popup.PopupAnimation}; staysOpen={popup.StaysOpen}; placement={popup.Placement}; target={Describe(popup.PlacementTarget)}; child={Describe(popup.Child)}";
    }

    private static string NativeWindow(IntPtr hwnd)
    {
        uint tid = GetWindowThreadProcessId(hwnd, out uint pid);
        var cls = new StringBuilder(256); GetClassName(hwnd, cls, cls.Capacity);
        bool rectRead = GetWindowRect(hwnd, out var rect);
        IntPtr owner = GetWindow(hwnd, 4); // GW_OWNER
        uint ownerTid = GetWindowThreadProcessId(owner, out uint ownerPid);
        return $"0x{hwnd:X}; pid={pid}; tid={tid}; class={cls}; owner=0x{owner:X}/pid={ownerPid}/tid={ownerTid}; root=0x{GetAncestor(hwnd, 2):X}; rootOwner=0x{GetAncestor(hwnd, 3):X}; rectRead={rectRead}; rect={rect}; dpi={GetDpiForWindow(hwnd)}";
    }

    [StructLayout(LayoutKind.Sequential)] private readonly record struct NativePoint(int X, int Y);
    [StructLayout(LayoutKind.Sequential)] private readonly record struct NativeRect(int Left, int Top, int Right, int Bottom);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(NativePoint point);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetCapture();
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, uint command);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder text, int maximum);
}
