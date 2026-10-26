using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Text.Json;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

internal static class NotebookOverflowEvidence
{
    // Inspect the actual button bitmap, not just the shared geometry: parent clipping,
    // alignment and foreground contrast can hide otherwise valid vector figures.
    public static void Verify(Button button, FrameworkElement root, int dpi, string state)
    {
        var path = Assert.IsType<System.Windows.Shapes.Path>(button.Content);
        var bounds = path.RenderedGeometry.Bounds;
        var origin = path.TranslatePoint(bounds.TopLeft, button);
        var rect = new Rect(origin, bounds.Size);
        Assert.InRange(rect.Left, 0, button.ActualWidth);
        Assert.InRange(rect.Right, 0, button.ActualWidth);
        Assert.InRange(rect.Top, 0, button.ActualHeight);
        Assert.InRange(rect.Bottom, 0, button.ActualHeight);
        double scale = dpi / 96d;
        // Render the arranged root: rendering an attached child on a small target retains
        // its ancestor offset and can produce a wholly transparent diagnostic bitmap.
        var surface = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth * scale),
            (int)Math.Ceiling(root.ActualHeight * scale), dpi, dpi, PixelFormats.Pbgra32);
        surface.Render(root);
        var atRoot = button.TranslatePoint(new Point(0, 0), root);
        var bitmap = new CroppedBitmap(surface, new Int32Rect((int)Math.Round(atRoot.X * scale),
            (int)Math.Round(atRoot.Y * scale), (int)Math.Floor(button.ActualWidth * scale),
            (int)Math.Floor(button.ActualHeight * scale)));
        int stride = bitmap.PixelWidth * 4;
        var pixels = new byte[stride * bitmap.PixelHeight]; bitmap.CopyPixels(pixels, stride, 0);
        int y = (int)((rect.Top + rect.Height / 2) * scale);
        int background = y * stride + (int)((rect.Left - 2) * scale) * 4;
        Assert.Equal(255, pixels[background + 3]);
        var runs = new List<int>(); bool inside = false;
        for (int x = (int)Math.Floor(rect.Left * scale); x < Math.Ceiling(rect.Right * scale); x++)
        {
            int at = y * stride + x * 4;
            bool ink = Enumerable.Range(0, 3).Max(c => Math.Abs(pixels[at + c] - pixels[background + c])) > 40;
            if (ink && !inside) runs.Add(x);
            inside = ink;
        }
        string directory = Path.Combine(Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY")!, "overflow-evidence");
        Directory.CreateDirectory(directory);
        string name = AutomationProperties.GetName(button);
        File.AppendAllText(Path.Combine(directory, "geometry.jsonl"), JsonSerializer.Serialize(new
        {
            state, dpi, name, button.ActualWidth, button.ActualHeight,
            PathWidth = path.ActualWidth, PathHeight = path.ActualHeight,
            Geometry = rect.ToString(System.Globalization.CultureInfo.InvariantCulture), Runs = runs
        }) + Environment.NewLine);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var file = File.Create(Path.Combine(directory, state + ".png"))) encoder.Save(file);
        Assert.True(runs.Count == 3, $"{name} / {state}: expected three separated visible dots, found {runs.Count}.");
    }
}
