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
    public static class SnapshotTests
    {
        static string Hash(string path) { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))); }
        static byte[] Pixels(BitmapSource image) { var bytes = new byte[image.PixelWidth * image.PixelHeight * 4]; new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0).CopyPixels(bytes, image.PixelWidth * 4, 0); return bytes; }
        static void Render(Window window, string file) { window.Show(); window.UpdateLayout(); var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(file)) encoder.Save(stream); }
        static IEnumerable<TextBlock> Labels(DependencyObject root) { var label = root as TextBlock; if (label != null) yield return label; foreach (object child in LogicalTreeHelper.GetChildren(root)) { var dependency = child as DependencyObject; if (dependency != null) foreach (var found in Labels(dependency)) yield return found; } }
        public static int Run(string root) {
            var results = new List<string>(); Action<bool, string> check = (ok, message) => { if (!ok) throw new Exception(message); results.Add("PASS " + message); };
            try {
                root = Path.GetFullPath(root); if (Directory.Exists(root)) throw new IOException("Use a new snapshot test directory."); Directory.CreateDirectory(root); Ui.TestStore(Path.Combine(root, "settings.json")); Ui.SetLanguage("en"); Ui.SetDark(false);
                string source = Path.Combine(root, "فيديو لقطات.mp4"); Engine.Run("ffmpeg", new[] { "-v", "error", "-nostdin", "-n", "-f", "lavfi", "-i", "testsrc2=size=320x240:rate=24:duration=2", "-c:v", "libx264", "-pix_fmt", "yuv420p", source }, CancellationToken.None, null); string original = Hash(source);
                double fps = VideoFrames.FrameRate(source, CancellationToken.None); check(Math.Abs(fps - 24) < .001, "Frame navigation reads the actual rational video frame rate");
                check(Catalog.Choices(new[] { source }, 1).Any(c => c.Id == "snapshot") && MediaTools.NeedsEngine(new[] { source }, "snapshot"), "Snapshot is available from video tools with the required engine");
                var first = VideoFrames.Read(source, 0, CancellationToken.None); var later = VideoFrames.Read(source, 1.25, CancellationToken.None);
                string folder = VideoSnapshots.Save(source, new[] { 1.25, 0.0 }, CancellationToken.None, null); var files = Directory.GetFiles(folder).OrderBy(f => f).ToArray();
                check(files.Length == 2 && files.All(f => Path.GetExtension(f) == ".png"), "Selected frames are saved as separate PNGs in one new folder");
                var decoded = ImageEdits.Load(files[0]); check(decoded.PixelWidth == 320 && decoded.PixelHeight == 240 && Pixels(decoded).SequenceEqual(Pixels(later)), "Saved frame retains every decoded pixel at original dimensions");
                check(Pixels(ImageEdits.Load(files[1])).SequenceEqual(Pixels(first)) && !Pixels(later).SequenceEqual(Pixels(first)), "Output order follows the selected times and preserves different frame content");
                string collision = VideoSnapshots.Save(source, new[] { 0.0 }, CancellationToken.None, null); check(collision != folder && Directory.GetFiles(folder).Length == 2, "Repeated saves never overwrite an existing output folder");
                using (var cancelled = new CancellationTokenSource()) { int before = Directory.GetDirectories(root).Length; cancelled.Cancel(); bool stopped = false; try { VideoSnapshots.Save(source, new[] { 0.0 }, cancelled.Token, null); } catch (OperationCanceledException) { stopped = true; } check(stopped && Directory.GetDirectories(root).Length == before, "Pre-cancel creates no output or staging folder"); }
                using (var cancelled = new CancellationTokenSource()) { int before = Directory.GetDirectories(root).Length; bool stopped = false; try { VideoSnapshots.Save(source, new[] { 0.0, 1.25 }, cancelled.Token, message => cancelled.Cancel()); } catch (OperationCanceledException) { stopped = true; } check(stopped && Directory.GetDirectories(root).Length == before, "Cancellation after a real frame is written removes all staged images"); }
                int directories = Directory.GetDirectories(root).Length; bool invalid = false; try { VideoSnapshots.Save(source, new[] { double.NaN }, CancellationToken.None, null); } catch (ArgumentException) { invalid = true; } check(invalid && Directory.GetDirectories(root).Length == directories, "Invalid timestamps are rejected before generating output");
                invalid = false; try { VideoSnapshots.Save(source, new[] { 3.0 }, CancellationToken.None, null); } catch (ArgumentException) { invalid = true; } check(invalid, "Frames outside the recording are rejected");
                check(Hash(source) == original, "Snapshot extraction leaves the source video unchanged");
                var window = new VideoSnapshotWindow(source, first, 2, fps); window.AddCurrent(); Render(window, Path.Combine(root, "snapshots-en-light.png")); check(window.SelectedCount == 1 && window.Width <= 450 && window.Title == "Video Snapshots", "Compact snapshot editor previews the video and avoids duplicate selections");
                window.Step(1); check(Math.Abs(window.Position - 1.0 / 24) < .000001, "Next-frame control moves by the stream frame interval"); window.AddCurrent(); check(window.SelectedCount == 1, "An unloaded preview cannot be added as a different timestamp"); window.Close();
                Ui.SetLanguage("ar"); Ui.SetDark(true); var arabic = new VideoSnapshotWindow(source, first, 2, fps); Render(arabic, Path.Combine(root, "snapshots-ar-dark.png")); check(arabic.Title == "لقطات الفيديو" && arabic.SelectedCount == 1, "Snapshot editor supports Arabic and dark appearance"); arabic.Close();
                check(FloatingJob.Fraction("Page 2 of 4", 0) == .5 && !FloatingJob.Fraction("Page 5 of 4", 0).HasValue, "Page-based export progress reflects completed outputs");
                Ui.SetLanguage("en"); var job = new FloatingJob("Converting to PNG", null); job.Finish("Saved", folder, true, 4); check(Labels((DependencyObject)job.Content).Any(label => label.Text == "4 of 4 saved"), "Folder exports show the actual saved-file count"); job.Close();
                results.Add("RESULT: " + results.Count + " snapshot/progress checks passed."); File.WriteAllLines(Path.Combine(root, "snapshot-test-results.txt"), results, Encoding.UTF8); return 0;
            } catch (Exception e) { Directory.CreateDirectory(root); results.Add("FAIL " + e); File.WriteAllLines(Path.Combine(root, "snapshot-test-results.txt"), results, Encoding.UTF8); return 1; }
        }
    }
}
