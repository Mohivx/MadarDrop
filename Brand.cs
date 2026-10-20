using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TangerineWindows
{
    // Original vector artwork shared by the dashboard, tray, taskbar and Windows icon.
    public sealed class LemonMark : FrameworkElement
    {
        public LemonMark(double size) { Width = size; Height = size; }
        protected override void OnRender(DrawingContext dc) { base.OnRender(dc); Brand.Draw(dc, new Rect(0, 0, ActualWidth, ActualHeight)); }
    }
    public static class Brand
    {
        static Point Slice(double r, double angle) { double a = angle * Math.PI / 180; return new Point(50 + r * Math.Cos(a), 50 + r * Math.Sin(a)); }
        public static void Draw(DrawingContext dc, Rect bounds) {
            dc.PushTransform(new TranslateTransform(bounds.X, bounds.Y));
            dc.PushTransform(new ScaleTransform(bounds.Width / 100, bounds.Height / 100));
            dc.DrawRoundedRectangle(new LinearGradientBrush(Colors.White, Color.FromRgb(235, 237, 233), 90), new Pen(new SolidColorBrush(Color.FromRgb(224, 227, 219)), .8), new Rect(3, 3, 94, 94), 23, 23);
            for (int i = 0; i < 8; i++) {
                double start = i * 45 - 105, end = start + 39;
                var g = new StreamGeometry(); using (var c = g.Open()) {
                    c.BeginFigure(Slice(10, start), true, true);
                    c.LineTo(Slice(34, start), true, false);
                    c.QuadraticBezierTo(Slice(39, start), Slice(39, start + 7), true, false);
                    c.ArcTo(Slice(39, end - 7), new Size(39, 39), 0, false, SweepDirection.Clockwise, true, false);
                    c.QuadraticBezierTo(Slice(39, end), Slice(34, end), true, false);
                    c.LineTo(Slice(10, end), true, false);
                    c.QuadraticBezierTo(Slice(4, (start + end) / 2), Slice(10, start), true, false);
                }
                dc.DrawGeometry(new LinearGradientBrush(Color.FromRgb(255, (byte)(226 + i % 3 * 7), 93), Color.FromRgb(250, 190, 28), i * 45 + 90), new Pen(new SolidColorBrush(Color.FromRgb(255, 222, 65)), .8), g);
            }
            dc.Pop(); dc.Pop();
        }
        public static BitmapSource Raster(int size) {
            var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) Draw(dc, new Rect(0, 0, size, size));
            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); bitmap.Freeze(); return bitmap;
        }
        static byte[] Png(int size) {
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(Raster(size)));
            using (var stream = new MemoryStream()) { encoder.Save(stream); return stream.ToArray(); }
        }
        public static void WriteAssets(string folder) {
            Directory.CreateDirectory(folder); File.WriteAllBytes(Path.Combine(folder, "lemon-preview.png"), Png(512));
            int[] sizes = { 16, 24, 32, 48, 64, 128, 256 }; var frames = new List<byte[]>(); foreach (int size in sizes) frames.Add(Png(size));
            using (var output = new BinaryWriter(File.Create(Path.Combine(folder, "MadarDrop.ico")))) {
                output.Write((ushort)0); output.Write((ushort)1); output.Write((ushort)sizes.Length); int offset = 6 + sizes.Length * 16;
                for (int i = 0; i < sizes.Length; i++) { output.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); output.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); output.Write((byte)0); output.Write((byte)0); output.Write((ushort)1); output.Write((ushort)32); output.Write(frames[i].Length); output.Write(offset); offset += frames[i].Length; }
                foreach (var frame in frames) output.Write(frame);
            }
        }
    }
}
