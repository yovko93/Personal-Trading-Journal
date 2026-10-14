using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PersonalTradingJournal.Desktop.Views.Rules;

/// <summary>One DPI-scaled, high-contrast closed-hand cursor. No disk file or native handle ownership.</summary>
internal static class RuleDragCursor
{
    private static Cursor? _grabbing;
    internal static Cursor Grabbing => _grabbing ??= Create();
    private static Cursor Create()
    {
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            var hand = Geometry.Parse("M 9,25 L 5,18 Q 3,14 6,13 Q 8,13 10,17 L 10,9 Q 10,6 13,7 L 13,6 Q 16,4 17,8 Q 20,5 21,9 Q 25,7 26,11 L 26,19 Q 26,23 22,26 L 22,29 L 11,29 L 11,26 Z");
            drawing.DrawGeometry(Brushes.White, new Pen(Brushes.Black, 1.6), hand);
            foreach (double x in new[] { 13d, 17d, 21d }) drawing.DrawLine(new Pen(Brushes.Black, 1.2), new Point(x, 10), new Point(x, 16));
        }
        var bitmap = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var png = new MemoryStream(); encoder.Save(png);
        using var cursor = new MemoryStream();
        using (var writer = new BinaryWriter(cursor, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            // CUR directory: one 32x32 PNG, hotspot on the palm, payload at byte 22.
            writer.Write((ushort)0); writer.Write((ushort)2); writer.Write((ushort)1);
            writer.Write((byte)32); writer.Write((byte)32); writer.Write((byte)0); writer.Write((byte)0);
            writer.Write((ushort)16); writer.Write((ushort)17); writer.Write((uint)png.Length); writer.Write((uint)22);
            writer.Write(png.ToArray());
        }
        cursor.Position = 0;
        return new Cursor(cursor, scaleWithDpi: true);
    }
}
