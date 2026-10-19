using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Drawing = System.Drawing;

namespace TangerineWindows
{
    public static class SelfTest
    {
        static readonly List<string> report = new List<string>();
        static void Check(bool condition, string description) { if (!condition) throw new Exception(description); report.Add("PASS " + description); }
        static string Hash(string path) { using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream)); }
        static string Convert(string source, string op, Parameters p) { return Engine.Convert(new[] { source }, op, p ?? new Parameters(), CancellationToken.None, null); }
        static void Decode(string file) { Engine.Run("ffmpeg", new[] { "-v", "error", "-nostdin", "-i", file, "-f", "null", "-" }, CancellationToken.None, null); }
        public static int Run(string root)
        {
            try {
                root = Path.GetFullPath(root);
                if (Directory.Exists(root)) throw new IOException("Use a new, empty test directory.");
                Directory.CreateDirectory(root);
                string image = Path.Combine(root, "صورة space & sample.png");
                using (var bmp = new Drawing.Bitmap(160, 120)) { using (var g = Drawing.Graphics.FromImage(bmp)) { g.Clear(Drawing.Color.OrangeRed); g.FillRectangle(Drawing.Brushes.MediumSeaGreen, 30, 20, 60, 60); } bmp.Save(image, Drawing.Imaging.ImageFormat.Png); }
                string imageHash = Hash(image);
                foreach (string format in new[] { "jpg", "webp", "tiff", "bmp", "avif", "gif" }) {
                    string output = Convert(image, format, null); Decode(output);
                    Check(new FileInfo(output).Length > 0, "Image conversion " + format + " decodes correctly");
                }
                string first = Convert(image, "jpg", null), second = Convert(image, "jpg", null);
                Check(first != second && File.Exists(first) && File.Exists(second), "Duplicate output names preserve both files");
                string resized = Convert(image, "resize", new Parameters { Width = 80 });
                string decoded = Convert(resized, "png", null);
                using (var b = new Drawing.Bitmap(decoded)) Check(b.Width == 80 && b.Height == 60, "Resize preserves aspect ratio");
                string cropped = Convert(image, "crop", new Parameters { X = 10, Y = 20, CropWidth = 50, CropHeight = 40 });
                string cropPng = Convert(cropped, "png", null);
                using (var b = new Drawing.Bitmap(cropPng)) Check(b.Width == 50 && b.Height == 40, "Crop produces the requested dimensions");
                Decode(Convert(image, "compress", null)); Check(Hash(image) == imageHash, "Image tools preserve original bytes");
                string pdf = Engine.Convert(new[] { image, first }, "pdf", new Parameters(), CancellationToken.None, null);
                string pdfText = Encoding.ASCII.GetString(File.ReadAllBytes(pdf));
                Check(pdfText.StartsWith("%PDF-1.4") && pdfText.Contains("/Count 2") && pdfText.Contains("xref"), "Image-to-PDF creates a two-page PDF with cross-reference table");
                string video = Path.Combine(root, "movie sample.mp4");
                Engine.Run("ffmpeg", new[] { "-v", "error", "-nostdin", "-n", "-f", "lavfi", "-i", "testsrc2=size=160x120:rate=12", "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=44100", "-t", "1", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", video }, CancellationToken.None, null);
                string videoHash = Hash(video);
                foreach (string format in new[] { "mov", "mkv", "webm", "avi", "gif", "mp3" }) { Decode(Convert(video, format, null)); Check(true, "Video conversion " + format + " decodes correctly"); }
                Decode(Convert(video, "trim", new Parameters { Start = 0.1, Duration = 0.4 })); Check(true, "Video trim completes");
                Decode(Convert(video, "speed", new Parameters { Speed = 2 })); Check(true, "Video speed with audio completes");
                string silent = Path.Combine(root, "silent.mp4");
                Engine.Run("ffmpeg", new[] { "-v", "error", "-nostdin", "-n", "-i", video, "-an", "-c:v", "copy", silent }, CancellationToken.None, null);
                Decode(Convert(silent, "speed", new Parameters { Speed = 0.5 })); Check(true, "Video speed also handles a silent video");
                string audio = Convert(video, "mp3", null);
                foreach (string format in new[] { "m4a", "wav", "flac", "ogg", "opus", "aiff" }) { Decode(Convert(audio, format, null)); Check(true, "Audio conversion " + format + " decodes correctly"); }
                Decode(Convert(audio, "compress", null)); Decode(Convert(audio, "trim", new Parameters { Start = 0.1, Duration = 0.3 })); Check(true, "Audio compression and trim complete");
                Check(Hash(video) == videoHash, "Video tools preserve original bytes");
                string srt = Path.Combine(root, "subtitles.srt"); File.WriteAllText(srt, "1\n00:00:01,000 --> 00:00:02,250\nمرحبا <b>بك</b>\n\n2\n00:00:03,000 --> 00:00:04,000\nHello\n", Encoding.UTF8);
                string vtt = Convert(srt, "vtt", null); Check(File.ReadAllText(vtt).Contains("00:00:01.000 --> 00:00:02.250"), "SRT-to-VTT preserves timing and Arabic text");
                string roundtrip = Convert(vtt, "srt", null); Check(File.ReadAllText(roundtrip).Contains("00:00:01,000 --> 00:00:02,250"), "VTT-to-SRT restores comma timestamps");
                Check(File.ReadAllText(Convert(vtt, "txt", null)).Contains("مرحبا بك"), "Subtitle-to-text removes markup");
                string text = Path.Combine(root, "notes.txt"); File.WriteAllText(text, "مرحبا & <hello>\nA second line", Encoding.UTF8);
                string docx = Convert(text, "docx", null); Check(File.ReadAllText(Convert(docx, "txt", null)).Replace("\r\n", "\n") == File.ReadAllText(text).Replace("\r\n", "\n"), "DOCX round trip preserves text and paragraphs");
                Check(File.ReadAllText(Convert(text, "html", null)).Contains("&lt;hello&gt;"), "HTML conversion escapes text");
                string csv = Path.Combine(root, "table.csv"); File.WriteAllText(csv, "Name,Value,Note\r\nعربي,00123,\"a,b\"\r\nUnicode,\"a\"\"b\",\"line1\nline2\"\r\n", Encoding.UTF8);
                var before = Engine.ReadTable(csv); string xlsx = Convert(csv, "xlsx", null); string tsv = Convert(xlsx, "tsv", null); var after = Engine.ReadTable(tsv);
                Check(before.Count == after.Count && before.Zip(after, (a, b) => a.SequenceEqual(b)).All(x => x), "CSV-XLSX-TSV round trip preserves Unicode, zeros, quotes, delimiters, multiline fields");
                string zip = Engine.Convert(new[] { image, text }, "zip", new Parameters(), CancellationToken.None, null);
                string extracted = Convert(zip, "extract", null);
                Check(Hash(Path.Combine(extracted, Path.GetFileName(image))) == imageHash, "ZIP round trip preserves file bytes");
                string unsafeZip = Path.Combine(root, "unsafe.zip"); using (var z = ZipFile.Open(unsafeZip, ZipArchiveMode.Create)) using (var w = new StreamWriter(z.CreateEntry("../escape.txt").Open())) w.Write("bad");
                bool rejected = false; try { Convert(unsafeZip, "extract", null); } catch (InvalidDataException) { rejected = true; }
                Check(rejected && !File.Exists(Path.Combine(root, "escape.txt")), "ZIP traversal is rejected before writing outputs");
                var token = new CancellationTokenSource(); token.Cancel(); bool cancelled = false;
                try { Engine.Convert(new[] { image }, "jpg", new Parameters(), token.Token, null); } catch (OperationCanceledException) { cancelled = true; }
                Check(cancelled, "Cancellation is honored before processing");
                string corrupt = Path.Combine(root, "corrupt.png"); File.WriteAllText(corrupt, "not an image"); bool failed = false;
                try { Convert(corrupt, "jpg", null); } catch (InvalidOperationException) { failed = true; }
                Check(failed && !Directory.GetFiles(root, ".tangerine-*").Any(), "Failed conversions leave no partial output files");
                Check(Catalog.Choices(new[] { image }, 0).Any(c => c.Id == "pdf") && !Catalog.Choices(new[] { image }, 0).Any(c => c.Id == "mp3"), "Wheel offers operations appropriate for the input");
                Check(!Catalog.Choices(new[] { image, audio }, 0).Any(c => c.Id == "png"), "Mixed selections do not offer incompatible conversions");
                report.Add("RESULT: " + report.Count + " checks passed.");
                File.WriteAllLines(Path.Combine(root, "test-results.txt"), report, Encoding.UTF8); return 0;
            } catch (Exception e) { report.Add("FAIL " + e); try { Directory.CreateDirectory(root); File.WriteAllLines(Path.Combine(root, "test-results.txt"), report, Encoding.UTF8); } catch { } return 1; }
        }
    }
}
