using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TangerineWindows
{
    public static class VideoCropTests
    {
        static string Hash(string file) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))); }
        static double Difference(BitmapSource a, BitmapSource b) {
            int stride = a.PixelWidth * 4; byte[] first = new byte[stride * a.PixelHeight], second = new byte[first.Length];
            new FormatConvertedBitmap(a, PixelFormats.Bgra32, null, 0).CopyPixels(first, stride, 0); new FormatConvertedBitmap(b, PixelFormats.Bgra32, null, 0).CopyPixels(second, stride, 0);
            double difference = 0; for (int i = 0; i < first.Length; i++) if (i % 4 != 3) difference += Math.Abs(first[i] - second[i]); return difference / (first.Length * .75);
        }
        static void Render(Window window, string file) {
            window.Show(); window.UpdateLayout(); var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(file)) encoder.Save(stream);
        }
        public static int Run(string root) {
            var report = new List<string>(); Action<bool, string> check = (ok, text) => { if (!ok) throw new Exception(text); report.Add("PASS " + text); };
            try {
                if (Directory.Exists(root)) throw new IOException("Use a new video crop test directory."); Directory.CreateDirectory(root); Ui.TestStore(Path.Combine(root, "preferences.json")); Ui.SetLanguage("en"); Ui.SetDark(false);
                string source = Path.Combine(root, "لقطة تجربة.mp4");
                Engine.Run("ffmpeg", new[] { "-v", "error", "-nostdin", "-n", "-f", "lavfi", "-i", "testsrc2=size=320x240:rate=24", "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000", "-t", "2", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", source }, CancellationToken.None, null);
                string before = Hash(source); double duration = VideoFrames.Duration(source, CancellationToken.None); var first = VideoFrames.Read(source, 0, CancellationToken.None); var later = VideoFrames.Read(source, 1.2, CancellationToken.None);
                check(duration > 1.9 && duration < 2.2 && first.PixelWidth == 320 && first.PixelHeight == 240, "Video metadata and full-size preview read Unicode paths without ffprobe");
                var surface = new CropSurface(first); surface.Selection = new Rect(31, 21, 159, 119); var selected = surface.Selection; surface.ReplaceFrame(later); check(surface.Selection == selected, "Seeking to another frame preserves the selected crop");
                var crop = VideoFrames.Crop(selected, 320, 240); check(crop.X == 30 && crop.Y == 20 && crop.CropWidth == 158 && crop.CropHeight == 118, "Odd crop coordinates and dimensions normalize for video encoding");
                var edge = VideoFrames.Crop(new Rect(315, 235, 50, 50), 320, 240); check(edge.X + edge.CropWidth <= 320 && edge.Y + edge.CropHeight <= 240, "Crop stays inside the frame at the lower-right edge");
                bool rejected = false; try { VideoFrames.Crop(new Rect(1, 1, 1, 1), 320, 240); } catch (ArgumentException) { rejected = true; } check(rejected, "Invalid one-pixel video crops are rejected");
                check(Catalog.Choices(new[] { source }, 1).Any(c => c.Id == "video-crop") && !Catalog.Choices(new[] { source, source }, 1).Any(c => c.Id == "video-crop"), "Visual crop is offered for exactly one video");
                string output = Engine.Convert(new[] { source }, "video-crop", crop, CancellationToken.None, null); var decoded = VideoFrames.Read(output, 0, CancellationToken.None); check(decoded.PixelWidth == 158 && decoded.PixelHeight == 118, "Exported video decodes with the chosen crop dimensions");
                check(Difference(decoded, new CroppedBitmap(first, new Int32Rect(crop.X, crop.Y, crop.CropWidth, crop.CropHeight))) < 20, "Decoded crop content matches the selected source region within encoding tolerance");
                string audio = Path.Combine(root, "retained-audio.wav"); Engine.Run("ffmpeg", new[] { "-v", "error", "-nostdin", "-n", "-i", output, "-map", "0:a:0", audio }, CancellationToken.None, null);
                check(new FileInfo(audio).Length > 10000 && Math.Abs(VideoFrames.Duration(output, CancellationToken.None) - duration) < .15, "Crop retains audio and recording duration");
                string silent = Path.Combine(root, "silent.mp4"); Engine.Run("ffmpeg", new[] { "-v", "error", "-nostdin", "-n", "-i", source, "-an", "-c:v", "copy", silent }, CancellationToken.None, null);
                check(File.Exists(Engine.Convert(new[] { silent }, "video-crop", crop, CancellationToken.None, null)), "Silent videos also crop successfully");
                using (var cancellation = new CancellationTokenSource()) { cancellation.Cancel(); bool cancelled = false; try { Engine.Convert(new[] { source }, "video-crop", crop, cancellation.Token, null); } catch (OperationCanceledException) { cancelled = true; } check(cancelled && !Directory.GetFiles(root, ".tangerine-*").Any(), "Cancelled exports leave no partial output"); }
                using (var active = new CancellationTokenSource()) { int count = Directory.GetFiles(root, "*.mp4").Length; bool cancelled = false; try { Engine.Convert(new[] { source }, "video-crop", crop, active.Token, delegate { active.Cancel(); }); } catch (OperationCanceledException) { cancelled = true; } check(cancelled && Directory.GetFiles(root, "*.mp4").Length == count && !Directory.GetFiles(root, ".tangerine-*").Any(), "Cancellation during media processing publishes no result and removes the temporary file"); }
                check(Hash(source) == before, "The original video remains byte-for-byte unchanged");
                var dialog = new VideoCropWindow(source, later, duration); dialog.Surface.Selection = selected; dialog.Surface.SetDimensions(selected.Width, selected.Height, true); Render(dialog, Path.Combine(root, "video-crop-en-light.png")); check(dialog.Width <= 450 && dialog.Surface.ImageBounds().Height > 150, "Compact video crop has a visible preview and dedicated controls"); dialog.Close();
                Ui.SetLanguage("ar"); Ui.SetDark(true); var dark = new VideoCropWindow(source, later, duration); Render(dark, Path.Combine(root, "video-crop-ar-dark.png")); check(dark.Title == "قص الفيديو", "Video crop supports Arabic and dark appearance"); dark.Close();
                bool requested = false; var job = new FloatingJob("Processing · MP4", delegate { requested = true; }); job.Report(Path.GetFileName(source), 1, 1, "00:00:01"); Render(job, Path.Combine(root, "floating-progress-ar-dark.png")); job.Close(); check(requested && job.IsVisible, "Closing a running progress popup requests cancellation and keeps the job visible"); job.Finish("Cancelled", null, false); job.Close(); check(!job.IsVisible, "Completed or cancelled progress popups can close normally");
                Ui.SetLanguage("en"); Ui.SetDark(false); var editor = new ImageEditor(Path.Combine(root, "still.png"), "edit", later); editor.FocusTool("edit"); Render(editor, Path.Combine(root, "compact-edit-en-light.png")); check(editor.Width == 450 && editor.ToolOnly, "Photo tools use a compact vertical window"); editor.Close();
                report.Add("RESULT: " + report.Count + " video/workflow checks passed."); File.WriteAllLines(Path.Combine(root, "video-crop-test-results.txt"), report, Encoding.UTF8); return 0;
            } catch (Exception e) { Directory.CreateDirectory(root); report.Add("FAIL " + e); File.WriteAllLines(Path.Combine(root, "video-crop-test-results.txt"), report, Encoding.UTF8); return 1; }
        }
    }
}
