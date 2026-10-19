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
        static readonly Geometry Body = Geometry.Parse("M 10,47 C 17,46 16,37 27,30 C 45,18 68,23 78,37 C 81,41 81,45 88,46 Q 96,50 88,54 C 81,55 82,63 73,69 C 54,83 31,77 21,64 C 17,60 17,55 10,54 Q 3,51 10,47 Z");
        static readonly Geometry Leaf = Geometry.Parse("M 53,25 C 49,12 59,4 76,7 C 75,21 65,29 53,25 Z");
        static readonly Geometry Shine = Geometry.Parse("M 26,41 C 34,31 47,29 56,32");
        public static void Draw(DrawingContext dc, Rect bounds) {
            dc.PushTransform(new TranslateTransform(bounds.X, bounds.Y));
            dc.PushTransform(new ScaleTransform(bounds.Width / 100, bounds.Height / 100));
            dc.PushTransform(new TranslateTransform(4, 4)); dc.PushTransform(new ScaleTransform(.92, .92));
            dc.PushTransform(new RotateTransform(-28, 50, 50));
            dc.DrawGeometry(new LinearGradientBrush(Color.FromRgb(255, 233, 112), Color.FromRgb(246, 191, 27), 85), new Pen(new SolidColorBrush(Color.FromRgb(218, 162, 27)), 1.5), Body);
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 252, 202)), 4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, Shine);
            dc.DrawGeometry(new LinearGradientBrush(Color.FromRgb(111, 169, 70), Color.FromRgb(58, 126, 55), 90), null, Leaf);
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(69, 114, 42)), 1.6), new Point(53, 27), new Point(67, 13));
            dc.Pop(); dc.Pop(); dc.Pop(); dc.Pop(); dc.Pop();
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
