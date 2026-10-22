using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TangerineWindows
{
    public static class ToolModelTests
    {
        static void Render(Window window, string path) { window.ShowActivated = false; window.Show(); window.UpdateLayout(); var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(path)) png.Save(stream); }
        static bool Reject(Action action) { try { action(); return false; } catch (ArgumentException) { return true; } }
        public static int Run(string root) {
            var report = new List<string>(); Action<bool, string> check = (ok, message) => { if (!ok) throw new Exception(message); report.Add("PASS " + message); };
            try {
                root = Path.GetFullPath(root); if (Directory.Exists(root)) throw new IOException("Use a new tool model test folder."); Directory.CreateDirectory(root); Ui.TestStore(Path.Combine(root, "preferences.json")); Ui.SetLanguage("en"); Ui.SetDark(false);
                string source = Path.Combine(root, "fixture.mp4"); var expected = new[] { "compress", "metadata", "mute", "trim", "video-crop", "speed", "snapshot", "video-split", "video-redact" };
                var ring = new Radial(); ring.Configure(new[] { source }, 1); check(ring.Items.Select(c => c.Id).SequenceEqual(expected), "All nine video tools are visible directly in the wheel");
                for (int i = 0; i < 9; i++) { double angle = (-90 + 40 * i) * Math.PI / 180; check(ring.ChoiceAt(new Point(184 + 111 * Math.Cos(angle), 176 + 111 * Math.Sin(angle))).Id == expected[i], "Wheel hit testing selects " + expected[i]); }
                check(new[] { "metadata", "video-split", "video-redact" }.All(op => MediaTools.NeedsEngine(new[] { source }, op)), "Media metadata, split and redaction request the media engine");
                check(MediaTime.Parse("٠١:٠٢٫٥") == 62.5 && MediaTime.Parse("27:00:00") == 97200 && MediaTime.Format(97200).StartsWith("27:"), "Time fields support Arabic digits, fractions and recordings over a day");
                check(Reject(() => MediaTime.Parse("-1")) && Reject(() => MediaTime.Parse("00:70:00")) && Reject(() => MediaTime.Parse("NaN")), "Invalid and nonfinite time input is rejected");
                check(VideoOperations.Boundaries(3, new[] { 1.0, 2.0 }).SequenceEqual(new[] { 0.0, 1.0, 2.0, 3.0 }), "Split intervals cover the entire recording without gaps");
                check(Reject(() => VideoOperations.Boundaries(3, new[] { 2.0, 1.0 })) && Reject(() => VideoOperations.Boundaries(3, new[] { 3.0 })) && Reject(() => VideoOperations.Boundaries(3, new[] { double.NaN })), "Unordered, out-of-range and nonfinite split points are rejected");
                var parsed = MediaMetadata.Parse(";FFMETADATA1\ntitle=a\\=b\\#c\\\\d\ncomment=first\\\nsecond\n[CHAPTER]\ntitle=ignored\n"); check(parsed["title"] == "a=b#c\\d" && parsed["comment"] == "first\nsecond", "Metadata parsing preserves escaped text and excludes chapter titles");
                var bar = new TimeRangeBar { Duration = 4, Start = 1, End = 2 }; bar.Adjust(1, 1, 2, -10); check(bar.Start == 0 && bar.End == 2, "Dragging a range start clamps to the recording"); bar.Adjust(3, 1, 2, 10); check(bar.Start == 3 && bar.End == 4, "Moving a range retains its length at the end boundary");
                var mask = new VideoMask { Area = new Rect(20, 20, 50, 40), Start = 0, End = 2 };
                check(Reject(() => VideoOperations.ValidateMasks(new List<VideoMask> { new VideoMask { Area = new Rect(300, 20, 50, 40), Start = 0, End = 2 } }, 320, 240, 4)) && Reject(() => VideoOperations.ValidateMasks(new List<VideoMask> { new VideoMask { Area = mask.Area, Start = 2, End = 1 } }, 320, 240, 4)), "Invalid redaction geometry and reversed time ranges are rejected");
                var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) { dc.DrawRectangle(new LinearGradientBrush(Colors.DarkSlateBlue, Colors.Coral, 40), null, new Rect(0, 0, 320, 240)); for (int x = 0; x < 320; x += 20) dc.DrawLine(new Pen(Brushes.White, 2), new Point(x, 0), new Point(320 - x, 240)); } var image = new RenderTargetBitmap(320, 240, 96, 96, PixelFormats.Pbgra32); image.Render(visual); image.Freeze();
                var split = new VideoToolWindow(source, "video-split", image, 4, 24); Render(split, Path.Combine(root, "split-en-light.png")); check(split.BuildValue().SplitPoints.SequenceEqual(new[] { 2.0 }) && split.Width <= 450, "Split editor previews the video and defaults to two equal clips"); split.Close();
                var trim = new VideoToolWindow(source, "trim", image, 4, 24); Render(trim, Path.Combine(root, "trim-en-light.png")); check(trim.BuildValue().Start == 0 && trim.BuildValue().Duration == 4, "Visual trim starts with the entire recording selected"); trim.Close();
                var speed = new VideoToolWindow(source, "speed", image, 4, 24); check(speed.BuildValue().Speed == 2, "Visual speed editor maps the selected multiplier"); speed.Close();
                var redact = new VideoToolWindow(source, "video-redact", image, 4, 24); Render(redact, Path.Combine(root, "redact-en-light.png")); var value = redact.BuildValue(); value.Redactions[0].Color = "FFFFFF"; check(redact.Masks.Count == 1 && redact.Masks[0].Color == "000000" && !redact.Surface.ShowOutsideMask, "Redaction preview uses an independent mask snapshot and no crop shading"); redact.Close();
                Ui.SetLanguage("ar"); Ui.SetDark(true); var arabic = new VideoToolWindow(source, "video-redact", image, 4, 24); Render(arabic, Path.Combine(root, "redact-ar-dark.png")); check(arabic.Title == "طمس الفيديو" && arabic.BuildValue().Redactions.Count == 1, "Video redaction supports Arabic and dark appearance"); arabic.Close();
                ring = new Radial(); ring.Configure(new[] { source }, 1); var wheel = new Window { Width = 398, Height = 410, Content = ring, Background = Theme.Paper, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize }; Render(wheel, Path.Combine(root, "video-tools-ar-dark.png")); wheel.Close();
                var initial = new MetadataInfo { metadata = new Dictionary<string, string> { { "Title", "عنوان تجريبي" }, { "Artist", "فنان" } }, details = "Generated fixture" }; var metadata = new MetadataEditor(source, initial); Render(metadata, Path.Combine(root, "metadata-ar-dark.png")); check(metadata.IsVisible && MediaTools.NeedsEngine(new[] { source }, "metadata"), "Media metadata editor opens with the media fields"); metadata.Close();
                check(FloatingJob.Fraction("Fraction 0.75", 0) == .75 && !FloatingJob.Fraction("Fraction NaN", 0).HasValue, "Multi-clip progress accepts a real aggregate fraction");
                Ui.SetBehavior(false, false, true); Ui.Preferences = new UiPreferences(); Ui.Reload(); check(!Ui.Preferences.WheelEnabled && !Ui.Preferences.Notify && Ui.Preferences.OpenFolder, "Wheel, notification and reveal preferences survive a settings reload"); Ui.SetBehavior(true, true, false);
                report.Add("RESULT: " + report.Count + " tool model/UI checks passed."); File.WriteAllLines(Path.Combine(root, "tool-models-test-results.txt"), report, Encoding.UTF8); return 0;
            } catch (Exception e) { Directory.CreateDirectory(root); report.Add("FAIL " + e); File.WriteAllLines(Path.Combine(root, "tool-models-test-results.txt"), report, Encoding.UTF8); return 1; }
        }
    }
}
