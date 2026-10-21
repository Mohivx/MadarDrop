using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace TangerineWindows
{
    public static class MuteTests
    {
        static string Hash(string file) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))); }
        static string[] Frames(string file, string target) {
            Engine.Run("ffmpeg", new[] { "-v", "error", "-nostdin", "-n", "-i", file, "-map", "0:v:0", "-an", "-f", "framemd5", target }, CancellationToken.None, null);
            return File.ReadAllLines(target).Where(l => l.Length > 0 && !l.StartsWith("#")).Select(l => l.Split(',').Last().Trim()).ToArray();
        }
        static bool Audio(string file) {
            var args = new[] { "-hide_banner", "-nostdin", "-i", file };
            using (var process = new Process { StartInfo = new ProcessStartInfo(Engine.Tool("ffmpeg"), string.Join(" ", args.Select(Engine.Quote))) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true, StandardErrorEncoding = Encoding.UTF8 } }) {
                process.Start(); var errors = process.StandardError.ReadToEndAsync(); var output = process.StandardOutput.ReadToEndAsync();
                bool exited = process.WaitForExit(15000); if (!exited) { process.Kill(); process.WaitForExit(); }
                string text = errors.GetAwaiter().GetResult(); output.GetAwaiter().GetResult();
                if (!exited) throw new TimeoutException("The test media probe timed out.");
                if (!Regex.IsMatch(text, @"Stream #[^\r\n]*Video:")) throw new InvalidOperationException("The probe did not find valid video: " + text);
                return Regex.IsMatch(text, @"Stream #[^\r\n]*Audio:");
            }
        }
        static IEnumerable<DependencyObject> Tree(DependencyObject root) { yield return root; foreach (object child in LogicalTreeHelper.GetChildren(root)) if (child is DependencyObject) foreach (var item in Tree((DependencyObject)child)) yield return item; }
        static void Render(Window window, string target) { window.Show(); window.UpdateLayout(); var image = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); image.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using (var stream = File.Create(target)) encoder.Save(stream); }
        public static int Run(string root) {
            var report = new List<string>(); Action<bool, string> check = (ok, text) => { if (!ok) throw new Exception(text); report.Add("PASS " + text); };
            try {
                if (Directory.Exists(root)) throw new IOException("Use a new mute test directory."); Directory.CreateDirectory(root); Ui.TestStore(Path.Combine(root, "preferences.json")); Ui.SetLanguage("en"); Ui.SetDark(false);
                string source = Path.Combine(root, "فيديو بصوتين.mp4");
                Engine.Run("ffmpeg", new[] { "-v", "error", "-nostdin", "-n", "-f", "lavfi", "-i", "testsrc2=size=320x240:rate=24", "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000", "-f", "lavfi", "-i", "sine=frequency=880:sample_rate=48000", "-map", "0:v:0", "-map", "1:a:0", "-map", "2:a:0", "-t", "2", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", source }, CancellationToken.None, null);
                check(Audio(source), "The generated input contains a real audio stream"); string original = Hash(source); double duration = VideoFrames.Duration(source, CancellationToken.None);
                var choices = Catalog.Choices(new[] { source }, 1); check(choices.Any(c => c.Id == "mute") && !Catalog.Choices(new[] { "sample.mp3" }, 1).Any(c => c.Id == "mute") && MediaTools.NeedsEngine(new[] { source }, "mute"), "Mute is a video tool and requests the media engine");
                foreach (string extension in new[] { ".mp4", ".mov", ".mkv", ".webm", ".m4v" }) {
                    string input = source;
                    if (extension != ".mp4") {
                        input = Path.Combine(root, "sample" + extension);
                        var args = new List<string> { "-v", "error", "-nostdin", "-n", "-i", source };
                        args.AddRange(extension == ".webm" ? new[] { "-c:v", "libvpx-vp9", "-deadline", "realtime", "-cpu-used", "8", "-c:a", "libopus" } : new[] { "-map", "0", "-c", "copy" }); if (extension == ".m4v") args.AddRange(new[] { "-f", "mp4" }); args.Add(input); Engine.Run("ffmpeg", args, CancellationToken.None, null);
                    }
                    string output = Engine.Convert(new[] { input }, "mute", new Parameters(), CancellationToken.None, null);
                    check(Path.GetExtension(output) == extension && !Audio(output), "Mute removes every audio stream and retains the " + extension + " container");
                    check(Frames(input, Path.Combine(root, extension.Substring(1) + "-before.md5")).SequenceEqual(Frames(output, Path.Combine(root, extension.Substring(1) + "-after.md5"))) && Math.Abs(VideoFrames.Duration(output, CancellationToken.None) - duration) < .15, "Mute retains every decoded frame and duration for " + extension);
                }
                string silent = Directory.GetFiles(root, "*muted*.mp4").Single(); check(File.Exists(Engine.Convert(new[] { silent }, "mute", new Parameters(), CancellationToken.None, null)), "Already silent video still produces a valid copy");
                string captions = Path.Combine(root, "captions.srt"), subtitled = Path.Combine(root, "subtitled.mkv"), extracted = Path.Combine(root, "retained-captions.srt");
                File.WriteAllText(captions, "1\r\n00:00:00,000 --> 00:00:01,500\r\nKeep this caption\r\n", Encoding.UTF8);
                Engine.Run("ffmpeg", new[] { "-v", "error", "-nostdin", "-n", "-i", source, "-i", captions, "-map", "0", "-map", "1:0", "-c", "copy", subtitled }, CancellationToken.None, null);
                string mutedCaptions = Engine.Convert(new[] { subtitled }, "mute", new Parameters(), CancellationToken.None, null);
                Engine.Run("ffmpeg", new[] { "-v", "error", "-nostdin", "-n", "-i", mutedCaptions, "-map", "0:s:0", "-c:s", "srt", extracted }, CancellationToken.None, null);
                check(!Audio(mutedCaptions) && File.ReadAllText(extracted).Contains("Keep this caption"), "Muting preserves existing subtitle tracks while removing audio");
                check(Hash(source) == original, "Muting leaves the original byte-for-byte unchanged");
                using (var cts = new CancellationTokenSource()) { int before = Directory.GetFiles(root, "*muted*").Length; cts.Cancel(); bool cancelled = false; try { Engine.Convert(new[] { source }, "mute", new Parameters(), cts.Token, null); } catch (OperationCanceledException) { cancelled = true; } check(cancelled && Directory.GetFiles(root, "*muted*").Length == before && Directory.GetFiles(root, ".tangerine-*").Length == 0, "Cancelled mute publishes no output or partial file"); }
                using (var active = new CancellationTokenSource()) { int before = Directory.GetFiles(root, "*muted*").Length; bool cancelled = false; try { Engine.Convert(new[] { source }, "mute", new Parameters(), active.Token, delegate { active.Cancel(); }); } catch (OperationCanceledException) { cancelled = true; } check(cancelled && Directory.GetFiles(root, "*muted*").Length == before && Directory.GetFiles(root, ".tangerine-*").Length == 0, "Cancelling active mute removes the temporary file before publishing"); }
                check(FloatingJob.Fraction("Processed 00:00:02.500000", 10) == .25 && FloatingJob.Fraction("Processed 00:00:30.000000", 10) == 1 && !FloatingJob.Fraction("invalid", 10).HasValue && !FloatingJob.Fraction("Processed 00:00:01.000000", 0).HasValue, "Progress uses actual media timestamps and handles unknown duration");
                var job = new FloatingJob(MainWindow.JobTitle("mp4"), null); job.Report("weekend.mov", 1, 1, "", .4); Render(job, Path.Combine(root, "progress-en-light.png"));
                var bar = Tree(job).OfType<ProgressBar>().Single(); check(job.Height <= 130 && job.Title == "Converting to MP4" && bar.Value == 40 && Tree(job).OfType<Button>().Count() == 1, "Progress is compact with one close control and a real proportional bar");
                job.Finish("Saved", source, true); check(Tree(job).OfType<TextBlock>().Any(t => t.Text == "1 of 1 saved"), "Completion shows the saved count in place"); job.Report("late callback", 1, 1, "", 0); check(bar.Value == 100, "Late progress callbacks cannot reset a completed job"); job.Close();
                Ui.SetLanguage("ar"); Ui.SetDark(true); var arabic = new FloatingJob(MainWindow.JobTitle("mute"), null); arabic.Report("فيديو.mp4", 1, 1, "", .6); Render(arabic, Path.Combine(root, "progress-ar-dark.png")); check(arabic.Title == "جارٍ كتم الصوت", "Mute progress supports Arabic and dark appearance"); arabic.Finish("Saved", source, true); arabic.Close();
                report.Add("RESULT: " + report.Count + " mute/progress checks passed."); File.WriteAllLines(Path.Combine(root, "mute-test-results.txt"), report, Encoding.UTF8); return 0;
            } catch (Exception e) { Directory.CreateDirectory(root); report.Add("FAIL " + e); File.WriteAllLines(Path.Combine(root, "mute-test-results.txt"), report, Encoding.UTF8); return 1; }
        }
    }
}
