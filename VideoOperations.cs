using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;

namespace TangerineWindows
{
    public sealed class VideoMask
    {
        public Rect Area;
        public double Start, End;
        public bool Blur;
        public string Color = "000000";
        public VideoMask Clone() { return (VideoMask)MemberwiseClone(); }
    }
    public static class MediaTime
    {
        public static string Digits(string value) { return new string((value ?? "").Trim().Select(c => c >= '٠' && c <= '٩' ? (char)('0' + c - '٠') : c == '٫' ? '.' : c).ToArray()); }
        public static double Parse(string value) {
            value = Digits(value);
            var parts = value.Split(':'); if (parts.Length > 3 || parts.Length == 0) throw new ArgumentException(Ui.T("Enter a valid time.")); double result = 0;
            for (int i = 0; i < parts.Length; i++) { double n; if (!double.TryParse(parts[i], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out n) || double.IsNaN(n) || double.IsInfinity(n) || n < 0 || (i > 0 && n >= 60)) throw new ArgumentException(Ui.T("Enter a valid time.")); result = result * 60 + n; }
            if (double.IsInfinity(result)) throw new ArgumentException(Ui.T("Enter a valid time.")); return result;
        }
        public static string Format(double value) { var time = TimeSpan.FromSeconds(Math.Max(0, value)); return ((long)time.TotalHours).ToString("D2", CultureInfo.InvariantCulture) + time.ToString(@"\:mm\:ss\.fff"); }
    }
    public static class VideoOperations
    {
        static string N(double value) { return value.ToString("0.######", CultureInfo.InvariantCulture); }
        public static string AudioTempo(double rate) {
            if (double.IsNaN(rate) || double.IsInfinity(rate) || rate < .25 || rate > 4) throw new ArgumentException("Choose a speed between 0.25 and 4.");
            var filters = new List<string>(); while (rate < .5) { filters.Add("atempo=0.5"); rate *= 2; } while (rate > 2) { filters.Add("atempo=2"); rate /= 2; } filters.Add("atempo=" + N(rate)); return string.Join(",", filters);
        }
        public static double[] Boundaries(double duration, double[] points) {
            if (double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 0 || points == null || points.Length < 1 || points.Length > 99) throw new ArgumentException(Ui.T("Choose between 2 and 100 clips."));
            double previous = 0; foreach (double point in points) { if (double.IsNaN(point) || double.IsInfinity(point) || point - previous < .04 || point >= duration) throw new ArgumentException(Ui.T("Split points must be ordered inside the video.")); previous = point; }
            if (duration - previous < .04) throw new ArgumentException(Ui.T("Split points must be ordered inside the video.")); return new[] { 0.0 }.Concat(points).Concat(new[] { duration }).ToArray();
        }
        public static string Split(string source, double[] points, CancellationToken ct, Action<string> progress) {
            ct.ThrowIfCancellationRequested(); if (Catalog.Kind(source) != "video") throw new ArgumentException("Choose a video."); double duration = VideoFrames.Duration(source, ct); var cuts = Boundaries(duration, points);
            string parent = Path.GetDirectoryName(Path.GetFullPath(source)), stage = Path.Combine(parent, ".madardrop-clips-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage);
            try {
                for (int i = 0; i + 1 < cuts.Length; i++) {
                    ct.ThrowIfCancellationRequested(); int part = i; double length = cuts[i + 1] - cuts[i];
                    var args = new List<string> { "-hide_banner", "-v", "error", "-nostdin", "-n", "-ss", N(cuts[i]), "-i", source, "-t", N(length), "-map", "0:v:0", "-map", "0:a?", "-vf", "scale=trunc(iw/2)*2:trunc(ih/2)*2", "-c:v", "libx264", "-preset", "fast", "-crf", "20", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "160k", "-movflags", "+faststart", "-progress", "pipe:1", Path.Combine(stage, "Clip " + (i + 1).ToString("D3") + ".mp4") };
                    Engine.Run("ffmpeg", args, ct, message => { var value = FloatingJob.Fraction(message, length); if (progress != null && value.HasValue) progress("Fraction " + ((part + value.Value) / (cuts.Length - 1)).ToString("0.######", CultureInfo.InvariantCulture)); });
                    VideoFrames.Read(Path.Combine(stage, "Clip " + (i + 1).ToString("D3") + ".mp4"), 0, ct);
                    if (progress != null) progress("Page " + (i + 1) + " of " + (cuts.Length - 1));
                }
                for (int n = 1; n <= 10000; n++) { ct.ThrowIfCancellationRequested(); string target = Path.Combine(parent, Path.GetFileNameWithoutExtension(source) + " (split" + (n == 1 ? "" : " " + n) + ")"); try { Directory.Move(stage, target); return target; } catch (IOException) { if (!Directory.Exists(target) && !File.Exists(target)) throw; } }
                throw new IOException("Could not find an unused output folder.");
            } finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
        }
        public static void ValidateMasks(List<VideoMask> masks, int width, int height, double duration) {
            if (masks == null || masks.Count == 0 || masks.Count > 12) throw new ArgumentException(Ui.T("Choose between 1 and 12 redaction areas."));
            foreach (var mask in masks) {
                Rect r = mask.Area; var numbers = new[] { r.X, r.Y, r.Width, r.Height, mask.Start, mask.End }; if (numbers.Any(n => double.IsNaN(n) || double.IsInfinity(n)) || r.X < 0 || r.Y < 0 || r.Width < 2 || r.Height < 2 || r.Right > width || r.Bottom > height || mask.Start < 0 || mask.End - mask.Start < .04 || mask.End > duration + .001 || mask.Color == null || mask.Color.Length != 6 || mask.Color.Any(c => !Uri.IsHexDigit(c))) throw new ArgumentException(Ui.T("Choose a valid area and time range."));
            }
        }
        public static string Redact(string source, List<VideoMask> masks, CancellationToken ct, Action<string> progress) {
            ct.ThrowIfCancellationRequested(); if (Catalog.Kind(source) != "video") throw new ArgumentException("Choose a video."); double duration = VideoFrames.Duration(source, ct); var frame = VideoFrames.Read(source, 0, ct); ValidateMasks(masks, frame.PixelWidth, frame.PixelHeight, duration);
            var graph = new StringBuilder("[0:v:0]setpts=PTS-STARTPTS,format=yuv444p[v0];");
            for (int i = 0; i < masks.Count; i++) {
                var mask = masks[i]; int x = (int)Math.Floor(mask.Area.X), y = (int)Math.Floor(mask.Area.Y), w = (int)Math.Ceiling(mask.Area.Right) - x, h = (int)Math.Ceiling(mask.Area.Bottom) - y;
                string current = "v" + i, next = "v" + (i + 1), active = "gte(t," + N(mask.Start) + ")*lt(t," + N(mask.End) + ")";
                if (!mask.Blur) graph.Append("[" + current + "]drawbox=x=" + x + ":y=" + y + ":w=" + w + ":h=" + h + ":color=0x" + mask.Color + ":t=fill:enable='" + active + "'[" + next + "];");
                else graph.Append("[" + current + "]split=2[b" + i + "][c" + i + "];[c" + i + "]crop=" + w + ":" + h + ":" + x + ":" + y + ":exact=1,boxblur=luma_radius='min(12,min(w,h)/2)':luma_power=2:chroma_radius='min(6,min(cw,ch)/2)':chroma_power=2[r" + i + "];[b" + i + "][r" + i + "]overlay=" + x + ":" + y + ":format=yuv444:enable='" + active + "'[" + next + "];");
            }
            graph.Append("[v" + masks.Count + "]scale=trunc(iw/2)*2:trunc(ih/2)*2[out]");
            string temp = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(source)), ".tangerine-" + Guid.NewGuid().ToString("N") + ".mp4");
            try {
                var args = new[] { "-hide_banner", "-v", "error", "-nostdin", "-n", "-i", source, "-filter_complex", graph.ToString(), "-map", "[out]", "-map", "0:a?", "-c:v", "libx264", "-preset", "fast", "-crf", "18", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "160k", "-map_metadata", "-1", "-map_metadata:s", "-1", "-map_chapters", "-1", "-movflags", "+faststart", "-progress", "pipe:1", temp };
                Engine.Run("ffmpeg", args, ct, progress); ct.ThrowIfCancellationRequested(); if (!File.Exists(temp) || new FileInfo(temp).Length == 0) throw new IOException("The video was not saved."); return Engine.Publish(temp, source, ".mp4", "redacted");
            } finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
