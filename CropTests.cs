using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using Drawing = System.Drawing;

namespace TangerineWindows
{
    public static class CropTests
    {
        static string Hash(string path) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))); }
        public static int Run(string root)
        {
            var report = new List<string>(); Action<bool, string> check = (ok, text) => { if (!ok) throw new Exception(text); report.Add("PASS " + text); };
            try {
                if (Directory.Exists(root)) throw new IOException("Use a new crop test folder."); Directory.CreateDirectory(root);
                string source = Path.Combine(root, "document-sample.png");
                using (var bitmap = new Drawing.Bitmap(600, 820)) {
                    using (var g = Drawing.Graphics.FromImage(bitmap)) {
                        g.Clear(Drawing.Color.White); g.TextRenderingHint = Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                        using (var title = new Drawing.Font("Segoe UI", 22, Drawing.FontStyle.Bold)) using (var text = new Drawing.Font("Segoe UI", 12)) {
                            g.DrawString("Project notes", title, Drawing.Brushes.Black, 44, 40);
                            string[] lines = { "A small tool for everyday files.", "Open a file, choose a format, and keep working.", "Everything happens here, on your computer.", "", "1. Start with a simple idea", "Keep the controls close to the thing you are editing.", "A few precise handles make a useful crop.", "Choose a ratio, drag a corner, or enter a size.", "", "2. Keep the original safe", "Save a fresh copy beside the source document.", "Changing the preview does not change the original.", "Reset restores the full image and free aspect ratio.", "", "3. Make the details feel natural", "The right tool opens directly from the wheel.", "No trip through another screen is needed.", "Apply saves the part inside the orange frame.", "", "Notes and ideas", "Images, documents, and the work between them.", "Small interactions can save a lot of busywork.", "Keep it clear. Keep it local. Keep it simple." };
                            for (int i = 0; i < lines.Length; i++) g.DrawString(lines[i], text, Drawing.Brushes.Black, 44, 100 + i * 27);
                        }
                    }
                    bitmap.Save(source, Drawing.Imaging.ImageFormat.Png);
                }
                string hash = Hash(source); var surface = new CropSurface(ImageEdits.Load(source));
                surface.Measure(new Size(300, 330)); surface.Arrange(new Rect(0, 0, 300, 330));
                check(surface.Selection.Width == 600 && surface.Selection.Height == 820, "Crop starts with the full source image");
                surface.SetAspect(1); check(Math.Abs(surface.Selection.Width - surface.Selection.Height) < .001, "Square aspect preset locks the crop ratio");
                surface.SetAspect(16.0 / 9); check(Math.Abs(surface.Selection.Width / surface.Selection.Height - 16.0 / 9) < .001, "16:9 preset fits inside the original image");
                foreach (int handle in new[] { 1, 2, 3, 4, 5, 6, 7, 8 }) {
                    surface.ResizeFrom(handle, new Rect(120, 140, 300, 168.75), new Point(-80, 1000));
                    var r = surface.Selection; check(r.Width > 0 && r.Height > 0 && r.Left >= -.001 && r.Top >= -.001 && r.Right <= 600.001 && r.Bottom <= 820.001 && Math.Abs(r.Width / r.Height - 16.0 / 9) < .001, "Resize handle " + handle + " respects bounds and locked ratio");
                }
                surface.Reset(); surface.SetDimensions(400, 300, true); check(surface.Crop().PixelWidth == 400 && surface.Crop().PixelHeight == 300, "Pixel dimensions control the exported crop");
                surface.SetDimensions(10000, 100, true); check(surface.Selection.Width == 600 && surface.Selection.Height == 100, "Free aspect mode clamps only the requested out-of-range dimension");
                surface.SetAspect(9.0 / 16); surface.SetDimensions(180, 500, true); check(Math.Abs(surface.Selection.Height - 320) < .001, "Editing width retains a selected portrait ratio");
                surface.Reset(); check(surface.Aspect == 0 && surface.Crop().PixelWidth == 600 && surface.Crop().PixelHeight == 820, "Reset restores image and free aspect mode");
                surface.Selection = new Rect(50, 60, 320, 450); string output = ImageEdits.Save(new ImageState { Source = surface.Crop() }, source, "PNG");
                var exported = ImageEdits.Load(output); check(exported.PixelWidth == 320 && exported.PixelHeight == 450 && Hash(source) == hash, "Apply writes the chosen crop and preserves the original");
                var popup = new CropWindow(source); popup.Surface.Selection = new Rect(0, 0, 600, 460); popup.ExportPreview(Path.Combine(root, "crop-popup.png"));
                check(File.Exists(Path.Combine(root, "crop-popup.png")), "Dedicated compact crop window renders successfully");
                report.Add("RESULT: " + report.Count + " crop checks passed."); File.WriteAllLines(Path.Combine(root, "crop-test-results.txt"), report, Encoding.UTF8); return 0;
            } catch (Exception e) { report.Add("FAIL " + e); Directory.CreateDirectory(root); File.WriteAllLines(Path.Combine(root, "crop-test-results.txt"), report, Encoding.UTF8); return 1; }
        }
    }
}
