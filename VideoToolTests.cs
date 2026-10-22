using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;

namespace TangerineWindows
{
    public static class VideoToolTests
    {
        static string Hash(string path) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))); }
        static string Payload(string source, string stream, string output) { Engine.Run("ffmpeg", new[] { "-v", "error", "-nostdin", "-n", "-i", source, "-map", stream, "-c", "copy", "-f", "hash", "-hash", "sha256", output }, CancellationToken.None, null); return File.ReadAllText(output).Trim(); }
        static byte[] Pixels(BitmapSource frame) { byte[] bytes = new byte[frame.PixelWidth * frame.PixelHeight * 4]; new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0).CopyPixels(bytes, frame.PixelWidth * 4, 0); return bytes; }
        static double Difference(BitmapSource first, BitmapSource last, Rect area) { var a = Pixels(first); var b = Pixels(last); double sum = 0; int count = 0; for (int y = (int)area.Top; y < area.Bottom; y++) for (int x = (int)area.Left; x < area.Right; x++) for (int c = 0; c < 3; c++) { int i = (y * first.PixelWidth + x) * 4 + c; sum += Math.Abs(a[i] - b[i]); count++; } return sum / count; }
        static double Brightness(BitmapSource frame, Rect area) { var bytes = Pixels(frame); double sum = 0; int count = 0; for (int y = (int)area.Top; y < area.Bottom; y++) for (int x = (int)area.Left; x < area.Right; x++) for (int c = 0; c < 3; c++) { sum += bytes[(y * frame.PixelWidth + x) * 4 + c]; count++; } return sum / count; }
        static double Detail(BitmapSource frame, Rect area) { var bytes = Pixels(frame); double sum = 0; int count = 0; for (int y = (int)area.Top + 2; y < area.Bottom - 2; y++) for (int x = (int)area.Left + 2; x < area.Right - 2; x++) { int i = (y * frame.PixelWidth + x) * 4; sum += Math.Abs(bytes[i] - bytes[i - 4]); count++; } return sum / count; }
        static double Pitch(string source, string output) { Engine.Run("ffmpeg", new[] { "-v", "error", "-nostdin", "-n", "-i", source, "-ss", "0.2", "-t", "0.5", "-map", "0:a:0", "-ac", "1", "-ar", "8000", "-f", "s16le", output }, CancellationToken.None, null); var bytes = File.ReadAllBytes(output); int crossings = 0; short previous = 0; for (int i = 0; i + 1 < bytes.Length; i += 2) { short sample = BitConverter.ToInt16(bytes, i); if (previous < -3 && sample >= 0) crossings++; previous = sample; } return crossings * 16000.0 / bytes.Length; }
        public static int Run(string root) {
            var report = new List<string>(); Action<bool, string> check = (ok, message) => { if (!ok) throw new Exception(message); report.Add("PASS " + message); };
            try {
                root = Path.GetFullPath(root); if (Directory.Exists(root)) throw new IOException("Use a new video tool test folder."); Directory.CreateDirectory(root); var ct = CancellationToken.None;
                string checker = Path.Combine(root, "checker.png"); using (var bitmap = new Drawing.Bitmap(320, 240)) { using (var g = Drawing.Graphics.FromImage(bitmap)) for (int y = 80; y < 240; y += 8) for (int x = 160; x < 320; x += 8) g.FillRectangle((x / 8 + y / 8) % 2 == 0 ? Drawing.Brushes.White : Drawing.Brushes.Black, x, y, 8, 8); bitmap.Save(checker, Drawing.Imaging.ImageFormat.Png); }
                string source = Path.Combine(root, "تجربة فيديو.mp4");
                Engine.Run("ffmpeg", new[] { "-hide_banner", "-v", "error", "-nostdin", "-n", "-f", "lavfi", "-i", "testsrc2=size=320x240:rate=24:duration=3", "-loop", "1", "-i", checker, "-f", "lavfi", "-i", "sine=frequency=440:duration=3", "-f", "lavfi", "-i", "sine=frequency=880:duration=3", "-filter_complex", "[0:v][1:v]overlay=0:0[v]", "-map", "[v]", "-map", "2:a", "-map", "3:a", "-t", "3", "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", "-c:a", "aac", "-metadata", "title=Original title", "-metadata", "location=+12.34+056.78/", "-movflags", "use_metadata_tags", source }, ct, null);
                string original = Hash(source); double duration = VideoFrames.Duration(source, ct);
                string videoPayload = Payload(source, "0:v:0", Path.Combine(root, "video-before.txt")); string audioPayload = Payload(source, "0:a:1", Path.Combine(root, "audio-before.txt"));
                var values = new Dictionary<string, string> { { "Title", "عنوان ;#=\\ عربي" }, { "Artist", "فنان" }, { "Comment", "first\nsecond #=\\;" } };
                string tagged = MediaMetadata.Save(source, values, false, ct); var info = MediaMetadata.Read(tagged, ct);
                check(values.All(p => info.metadata[p.Key] == p.Value), "Media tags round-trip Arabic, escaped punctuation and multiline text");
                check(Payload(tagged, "0:v:0", Path.Combine(root, "video-after.txt")) == videoPayload && Payload(tagged, "0:a:1", Path.Combine(root, "audio-after.txt")) == audioPayload, "Metadata edits preserve compressed video and both audio tracks without re-encoding");
                string stripped = MediaMetadata.Save(tagged, new Dictionary<string, string>(), true, ct); var clean = MediaMetadata.Read(stripped, ct); check(clean.metadata["Title"] == "" && !clean.details.Contains("+12.34+056.78/"), "Removing media metadata clears title and embedded location");
                foreach (string extension in new[] { ".mkv", ".mp3" }) {
                    string input = Path.Combine(root, "metadata-input" + extension); Engine.Run("ffmpeg", extension == ".mp3" ? new[] { "-v", "error", "-nostdin", "-n", "-i", source, "-map", "0:a:0", "-c:a", "libmp3lame", input } : new[] { "-v", "error", "-nostdin", "-n", "-i", source, "-map", "0", "-c", "copy", input }, ct, null);
                    string saved = MediaMetadata.Save(input, new Dictionary<string, string> { { "Title", "بيانات جديدة" }, { "Artist", "مؤلف" } }, false, ct); check(MediaMetadata.Read(saved, ct).metadata["Title"] == "بيانات جديدة", "Metadata supports the " + extension + " container");
                }
                using (var cancel = new CancellationTokenSource()) { cancel.Cancel(); bool stopped = false; try { MediaMetadata.Save(source, values, false, cancel.Token); } catch (OperationCanceledException) { stopped = true; } check(stopped && !Directory.GetFiles(root, ".tangerine-*").Any(), "Cancelled metadata edit leaves no temporary or published result"); }
                string folder = Engine.Convert(new[] { source }, "video-split", new Parameters { SplitPoints = new[] { 1.0, 2.0 } }, ct, null); var clips = Directory.GetFiles(folder).OrderBy(p => p).ToArray();
                check(clips.Length == 3 && clips.All(p => Math.Abs(VideoFrames.Duration(p, ct) - 1) < .2), "Custom split writes every expected clip with the chosen duration");
                check(clips.All(p => Regex.Matches(VideoFrames.Describe(p, ct), @"Stream #[^\r\n]*Audio:").Count == 2), "Every split clip retains both audio tracks");
                var expected = VideoFrames.Read(source, 2.25, ct); var decoded = VideoFrames.Read(clips[2], .25, ct); check(Difference(expected, decoded, new Rect(0, 0, 320, 240)) < 15, "Split content begins at the requested source time instead of a previous keyframe");
                string secondFolder = VideoOperations.Split(source, new[] { 1.5 }, ct, null); check(secondFolder != folder && Directory.GetFiles(folder).Length == 3, "Repeated splitting never merges into an existing output folder");
                using (var cancel = new CancellationTokenSource()) { int before = Directory.GetDirectories(root).Length; bool stopped = false; try { VideoOperations.Split(source, new[] { 1.0, 2.0 }, cancel.Token, message => { if (message.StartsWith("Page ")) cancel.Cancel(); }); } catch (OperationCanceledException) { stopped = true; } check(stopped && Directory.GetDirectories(root).Length == before, "Cancelling after an actual clip is produced removes the entire staged batch"); }
                var masks = new List<VideoMask> { new VideoMask { Area = new Rect(40, 40, 100, 80), Start = .5, End = 1.5 }, new VideoMask { Area = new Rect(181, 101, 100, 100), Start = 1, End = 2.5, Blur = true } };
                string redacted = Engine.Convert(new[] { source }, "video-redact", new Parameters { Redactions = masks }, ct, null);
                var active = VideoFrames.Read(redacted, .75, ct); check(Brightness(active, new Rect(44, 44, 92, 72)) < 8, "Solid redaction is flattened into the actual exported video frames");
                var beforeMask = VideoFrames.Read(source, .25, ct); var afterMask = VideoFrames.Read(source, 1.75, ct); check(Difference(beforeMask, VideoFrames.Read(redacted, .25, ct), masks[0].Area) < 12 && Difference(afterMask, VideoFrames.Read(redacted, 1.75, ct), masks[0].Area) < 12, "Solid redaction is absent before and after its chosen time range");
                var blurInput = VideoFrames.Read(source, 1.25, ct); var blurOutput = VideoFrames.Read(redacted, 1.25, ct); check(Detail(blurInput, masks[1].Area) > 15 && Detail(blurOutput, masks[1].Area) < Detail(blurInput, masks[1].Area) * .35, "Blur removes checkerboard detail inside an odd-coordinate area");
                check(Difference(blurInput, blurOutput, new Rect(5, 5, 25, 25)) < 12 && Regex.Matches(VideoFrames.Describe(redacted, ct), @"Stream #[^\r\n]*Audio:").Count == 2 && Math.Abs(VideoFrames.Duration(redacted, ct) - duration) < .15, "Redaction preserves unselected content, audio tracks and recording duration");
                using (var cancel = new CancellationTokenSource()) { int before = Directory.GetFiles(root, "*redacted*.mp4").Length; bool stopped = false; try { VideoOperations.Redact(source, masks, cancel.Token, message => cancel.Cancel()); } catch (OperationCanceledException) { stopped = true; } check(stopped && Directory.GetFiles(root, "*redacted*.mp4").Length == before && !Directory.GetFiles(root, ".tangerine-*").Any(), "Cancelling a redaction during processing publishes no partial video"); }
                string silent = Path.Combine(root, "silent.mp4"); Engine.Run("ffmpeg", new[] { "-v", "error", "-nostdin", "-n", "-i", source, "-map", "0:v:0", "-c", "copy", silent }, ct, null); check(File.Exists(VideoOperations.Redact(silent, masks, ct, null)) && Directory.GetFiles(VideoOperations.Split(silent, new[] { 1.0 }, ct, null)).Length == 2, "Silent videos support both redaction and splitting");
                foreach (double rate in new[] { .25, 4.0 }) { string output = Engine.Convert(new[] { source }, "speed", new Parameters { Speed = rate }, ct, null); double pitch = Pitch(output, Path.Combine(root, "pitch-" + rate.ToString(CultureInfo.InvariantCulture) + ".pcm")); check(Math.Abs(VideoFrames.Duration(output, ct) - 3 / rate) < .25 && Math.Abs(pitch - 440) < 25, "Speed " + rate + " changes duration while retaining audio pitch"); }
                check(Hash(source) == original && !Directory.GetFiles(root, ".tangerine-*").Any() && !Directory.GetDirectories(root, ".madardrop-clips-*").Any(), "All video tools preserve source bytes and remove private staging");
                report.Add("RESULT: " + report.Count + " video tool checks passed."); File.WriteAllLines(Path.Combine(root, "video-tools-test-results.txt"), report, Encoding.UTF8); return 0;
            } catch (Exception e) { Directory.CreateDirectory(root); report.Add("FAIL " + e); File.WriteAllLines(Path.Combine(root, "video-tools-test-results.txt"), report, Encoding.UTF8); return 1; }
        }
    }
}
