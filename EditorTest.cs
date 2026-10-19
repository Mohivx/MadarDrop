using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;

namespace TangerineWindows
{
    public sealed class PdfSummary { public int page_count { get; set; } public int[] rotations { get; set; } public Dictionary<string, string> metadata { get; set; } }
    public static class EditorTest
    {
        static readonly List<string> results = new List<string>();
        static void Check(bool value, string message) { if (!value) throw new Exception(message); results.Add("PASS " + message); }
        static byte[] Pixel(BitmapSource source, int x, int y) {
            var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0); byte[] pixel = new byte[4]; converted.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0); return pixel;
        }
        static string Hash(string file) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))); }
        static PdfSummary Pdf(string file) { return new JavaScriptSerializer().Deserialize<PdfSummary>(PdfWorker.Run(new { operation = "verify", files = new[] { file } }, CancellationToken.None)); }
        public static int Run(string root)
        {
            try {
                root = Path.GetFullPath(root); if (Directory.Exists(root)) throw new IOException("Use a new test directory."); Directory.CreateDirectory(root);
                string source = Path.Combine(root, "صورة محرر test.png");
                using (var b = new Drawing.Bitmap(240, 160)) { using (var g = Drawing.Graphics.FromImage(b)) { g.Clear(Drawing.Color.CornflowerBlue); g.FillRectangle(Drawing.Brushes.OrangeRed, 0, 0, 80, 160); g.FillRectangle(Drawing.Brushes.MediumSeaGreen, 160, 0, 80, 160); for (int y = 0; y < 160; y += 8) for (int x = 80; x < 160; x += 8) if ((x + y) % 16 == 0) g.FillRectangle(Drawing.Brushes.White, x, y, 8, 8); } b.Save(source, Drawing.Imaging.ImageFormat.Png); }
                string original = Hash(source); BitmapSource bitmap = ImageEdits.Load(source);
                var gray = new ImageState { Source = bitmap, Grayscale = true }; byte[] gp = Pixel(ImageEdits.Render(gray, 0), 20, 20); Check(gp[0] == gp[1] && gp[1] == gp[2], "Grayscale changes output color channels");
                var exposure = new ImageState { Source = bitmap, Exposure = 1, Contrast = 10, Saturation = -30, Warmth = 20, Sharpness = 30 };
                Check(!Pixel(ImageEdits.Render(exposure, 0), 20, 20).SequenceEqual(Pixel(bitmap, 20, 20)), "Light, color, warmth, and detail adjustments change pixels");
                var resize = new ImageState { Source = bitmap, OutputWidth = 120 }; var resized = ImageEdits.Render(resize, 0); Check(resized.PixelWidth == 120 && resized.PixelHeight == 80, "Editor resizes while preserving aspect ratio");
                var redacted = new ImageState { Source = bitmap }; redacted.Marks.Add(new Mark { Kind = "cover", X = .1, Y = .1, W = .3, H = .3, Color = "#111111" });
                string cover = ImageEdits.Save(redacted, source, "PNG"); var cp = Pixel(ImageEdits.Load(cover), 40, 30); Check(cp[0] == 17 && cp[1] == 17 && cp[2] == 17 && cp[3] == 255, "Solid redaction is flattened into opaque exported pixels");
                var pixelated = new ImageState { Source = bitmap }; pixelated.Marks.Add(new Mark { Kind = "pixelate", X = .33, Y = 0, W = .34, H = 1 });
                var pr = ImageEdits.Render(pixelated, 0); int differences = 0; for (int x = 85; x < 155; x += 3) if (!Pixel(pr, x, 40).SequenceEqual(Pixel(bitmap, x, 40))) differences++; Check(differences > 0, "Pixelation replaces pixels in the selected area");
                foreach (string kind in new[] { "arrow", "line", "rectangle", "ellipse", "text", "number", "pen", "highlight" }) {
                    var marked = new ImageState { Source = bitmap }; marked.Marks.Add(new Mark { Kind = kind, X = .1, Y = .2, W = .7, H = .5, Text = "مرحبا", Color = "#FFFFFF", Thickness = .015, Points = new List<Point> { new Point(.1, .1), new Point(.5, .7), new Point(.8, .2) } });
                    string output = ImageEdits.Save(marked, source, "PNG"); Check(Hash(output) != original, "Annotation export supports " + kind);
                }
                var framed = new ImageState { Source = bitmap, Frame = true, Gradient = true, Shadow = true, Padding = .1, Corners = .1 };
                var frame = ImageEdits.Render(framed, 0); Check(frame.PixelWidth == 288 && frame.PixelHeight == 208, "Background adds spacing, gradient, corners, and shadow");
                var collage = ImageEdits.Collage(new[] { source, cover, source }, 2); Check(collage.PixelWidth == 1054 && collage.PixelHeight == 1054, "Collage lays out multiple images in a grid");
                string jpg = ImageEdits.Save(framed, source, "JPG"); using (var b = new Drawing.Bitmap(jpg)) Check(b.Width == 288, "JPEG editor export decodes successfully");
                var snapshot = redacted.Clone(); snapshot.Marks[0].Color = "#FFFFFF"; Check(redacted.Marks[0].Color == "#111111", "Undo snapshots retain independent annotation state");
                Check(Hash(source) == original, "All image edits preserve original bytes");
                // JPEG embeds directly: this PDF fixture also works without the optional media engine.
                string pdf = Engine.Convert(new[] { jpg, jpg }, "pdf", new Parameters(), CancellationToken.None, null);
                string pdfOriginalHash = Hash(pdf);
                string thumbs = Path.Combine(root, "page-previews"); var pages = PdfWorker.Inspect(new[] { pdf }, thumbs, CancellationToken.None);
                Check(pages.Count == 2 && pages.All(p => File.Exists(p.thumbnail)), "PDF editor renders actual page thumbnails offline");
                var rotation = pages[1].Clone(); rotation.rotation = 90; var edited = new List<PdfPage> { rotation, pages[0].Clone(), pages[0].Clone() };
                string saved = PdfWorker.Save(new[] { pdf }, edited, false, "save", null, CancellationToken.None); var summary = Pdf(saved);
                Check(summary.page_count == 3 && summary.rotations.SequenceEqual(new[] { 90, 0, 0 }), "PDF page reorder, rotation, duplication, and saving preserve document structure");
                string removed = PdfWorker.Save(new[] { pdf }, pages.Take(1).ToList(), false, "save", null, CancellationToken.None); Check(Pdf(removed).page_count == 1, "PDF pages can be removed");
                string merged = PdfWorker.Save(new[] { pdf, removed }, pages.Concat(PdfWorker.Inspect(new[] { removed }, Path.Combine(root, "extra-previews"), CancellationToken.None)).ToList(), false, "save", null, CancellationToken.None); Check(Pdf(merged).page_count == 3, "PDF merge combines selected documents");
                string compressed = PdfWorker.Save(new[] { saved }, null, false, "compress", null, CancellationToken.None); Check(Pdf(compressed).page_count == 3, "PDF stream compression retains all pages");
                string tagged = PdfWorker.Save(new[] { pdf }, null, true, "metadata", new Dictionary<string, string> { { "Title", "عنوان عربي" }, { "Author", "Test author" } }, CancellationToken.None);
                Check(Pdf(tagged).metadata["Title"] == "عنوان عربي", "PDF metadata editing preserves Unicode");
                string stripped = PdfWorker.Save(new[] { tagged }, null, true, "metadata", null, CancellationToken.None); Check(!Pdf(stripped).metadata.ContainsKey("Title"), "PDF metadata can be removed");
                string metadataImage = Path.Combine(root, "metadata.png"); PdfWorker.Run(new { operation = "image-metadata-save", files = new[] { source }, output = metadataImage, strip_metadata = false, metadata = new Dictionary<string, string> { { "Author", "مؤلف" }, { "Title", "Test photo" } } }, CancellationToken.None);
                var info = new JavaScriptSerializer().Deserialize<MetadataInfo>(PdfWorker.Run(new { operation = "image-metadata-info", files = new[] { metadataImage } }, CancellationToken.None)); Check(info.metadata["Author"] == "مؤلف", "Image metadata can be edited with Arabic values");
                string strippedImage = Path.Combine(root, "stripped.png"); PdfWorker.Run(new { operation = "image-metadata-save", files = new[] { metadataImage }, output = strippedImage, strip_metadata = true, metadata = new Dictionary<string, string>() }, CancellationToken.None);
                var clean = new JavaScriptSerializer().Deserialize<MetadataInfo>(PdfWorker.Run(new { operation = "image-metadata-info", files = new[] { strippedImage } }, CancellationToken.None)); Check(string.IsNullOrEmpty(clean.metadata["Author"]), "Image metadata removal clears author information");
                string jpegMetadata = Path.Combine(root, "jpeg-metadata.jpg"); PdfWorker.Run(new { operation = "image-metadata-save", files = new[] { jpg }, output = jpegMetadata, strip_metadata = false, metadata = new Dictionary<string, string> { { "Author", "مؤلف عربي" }, { "Title", "صورة" } } }, CancellationToken.None);
                var jpegInfo = new JavaScriptSerializer().Deserialize<MetadataInfo>(PdfWorker.Run(new { operation = "image-metadata-info", files = new[] { jpegMetadata } }, CancellationToken.None)); Check(jpegInfo.metadata["Author"] == "مؤلف عربي" && jpegInfo.metadata["Title"] == "صورة", "JPEG Unicode metadata uses Windows EXIF fields");
                Check(Pdf(pdf).page_count == 2 && Hash(pdf) == pdfOriginalHash, "PDF editing leaves its source intact");
                var cancelled = new CancellationTokenSource(); cancelled.Cancel(); bool didCancel = false; try { PdfWorker.Inspect(new[] { pdf }, Path.Combine(root, "cancel"), cancelled.Token); } catch (OperationCanceledException) { didCancel = true; } Check(didCancel, "PDF operations respect cancellation");
                var preview = new ImageEditor(source, "annotate"); preview.Model = framed; preview.Model.Marks.Add(new Mark { Kind = "arrow", X = .15, Y = .2, W = .5, H = .5, Color = "#FFFFFF", Thickness = .008 }); preview.UpdatePreview(); preview.ExportPreview(root);
                Check(File.Exists(Path.Combine(root, "image-editor.png")), "Actual image editor layout exports successfully");
                results.Add("RESULT: " + results.Count + " editor checks passed."); File.WriteAllLines(Path.Combine(root, "editor-test-results.txt"), results, Encoding.UTF8); return 0;
            } catch (Exception e) { results.Add("FAIL " + e); try { Directory.CreateDirectory(root); File.WriteAllLines(Path.Combine(root, "editor-test-results.txt"), results, Encoding.UTF8); } catch { } return 1; }
        }
    }
}
