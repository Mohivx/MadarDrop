using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Drawing = System.Drawing;

namespace TangerineWindows
{
    public sealed class Choice
    {
        public string Id, Label, Hint;
        public Choice(string id, string label, string hint) { Id = id; Label = Ui.T(label); Hint = Ui.T(hint); }
    }

    public static class Catalog
    {
        public static readonly string[] Images = { ".png", ".jpg", ".jpeg", ".webp", ".tif", ".tiff", ".bmp", ".avif", ".gif", ".ico" };
        public static readonly string[] Videos = { ".mp4", ".mov", ".mkv", ".webm", ".avi", ".wmv", ".m4v" };
        public static readonly string[] Audio = { ".mp3", ".m4a", ".wav", ".flac", ".ogg", ".opus", ".aiff", ".aif", ".wma", ".aac" };
        public static string Kind(string path)
        {
            string e = Path.GetExtension(path).ToLowerInvariant();
            if (Images.Contains(e)) return "image";
            if (Videos.Contains(e)) return "video";
            if (Audio.Contains(e)) return "audio";
            if (e == ".srt" || e == ".vtt") return "subtitle";
            if (new[] { ".txt", ".md", ".html", ".htm", ".docx" }.Contains(e)) return "document";
            if (new[] { ".csv", ".tsv", ".xlsx" }.Contains(e)) return "sheet";
            if (e == ".zip") return "archive";
            if (e == ".pdf") return "pdf";
            return "file";
        }
        public static List<Choice> Choices(string[] files, int mode)
        {
            if (mode == 2) return new List<Choice> {
                new Choice("copy-path", "COPY PATH", "Copy paths to the clipboard"),
                new Choice("folder", "FOLDER", "Open the containing folder"),
                new Choice("info", "INFO", "View file information"),
                new Choice("zip", "ZIP", "Create a ZIP archive") };
            string[] kinds = files.Length == 0 ? new[] { "image" } : files.Select(Kind).Distinct().ToArray();
            if (mode == 1)
            {
                var r = new List<Choice>();
                if (kinds.All(k => k == "image" || k == "video" || k == "audio")) r.Add(new Choice("compress", "COMPRESS", "Create a smaller copy"));
                if (kinds.Length == 1 && kinds[0] == "image") {
                    r.Add(new Choice("metadata", "METADATA", "Inspect, edit, or remove metadata"));
                    r.Add(new Choice("edit", "EDIT", "Adjust light, color, and detail"));
                    r.Add(new Choice("annotate", "ANNOTATE", "Add arrows, text, and shapes"));
                    r.Add(new Choice("background", "ADD BG", "Add a background and rounded corners"));
                    r.Add(new Choice("crop-editor", "CROP", "Crop visually, rotate, or flip"));
                    r.Add(new Choice("redact", "REDACT", "Cover or pixelate private areas"));
                    r.Add(new Choice("resize", "RESIZE", "Resize to a maximum width"));
                    r.Add(new Choice("crop", "EXACT CROP", "Crop using exact dimensions"));
                    r.Add(new Choice("pdf", "MAKE PDF", "Combine images into one PDF"));
                    if (files.Length > 1) r.Add(new Choice("collage", "COLLAGE", "Arrange these images in a collage"));
                }
                if (kinds.Length == 1 && kinds[0] == "pdf") {
                    r.Add(new Choice("pdf-compress", "COMPRESS", "Compress PDF streams without rasterizing text"));
                    r.Add(new Choice("metadata", "METADATA", "Edit PDF title, author, and metadata"));
                    r.Add(new Choice("pdf-split", "SPLIT", "Save each PDF page as a separate document"));
                    r.Add(new Choice("pdf-edit", "ORGANIZE", "Reorder, rotate, duplicate, or remove pages"));
                    r.Add(new Choice("pdf-merge", "MERGE", "Combine PDFs and organize their pages"));
                }
                if (files.Length == 1 && new[] { "video", "audio" }.Contains(kinds[0])) r.Add(new Choice("metadata", "METADATA", "Inspect, edit, or remove metadata"));
                if (files.Length > 1 && kinds.Length == 1 && kinds[0] == "video") r.Add(new Choice("video-join", "JOIN", "Arrange clips and save one video"));
                if (kinds.Length == 1 && kinds[0] == "video") r.Add(new Choice("mute", "MUTE", "Save a silent copy without changing video quality"));
                if (kinds.All(k => k == "video" || k == "audio")) r.Add(new Choice("trim", "TRIM", "Keep a section of the recording"));
                if (files.Length == 1 && kinds[0] == "video") r.Add(new Choice("video-crop", "CROP", "Crop video visually with a frame preview"));
                if (kinds.Length == 1 && kinds[0] == "video") r.Add(new Choice("speed", "SPEED", "Change playback speed"));
                if (files.Length == 1 && kinds[0] == "video") r.Add(new Choice("snapshot", "SNAPSHOT", "Choose video frames and save full-resolution PNGs"));
                if (files.Length == 1 && kinds[0] == "video") {
                    r.Add(new Choice("video-split", "SPLIT", "Split at chosen points or into equal clips"));
                    r.Add(new Choice("video-redact", "REDACT", "Cover or blur areas during chosen time ranges"));
                }
                r.Add(new Choice("info", "INFO", "View file information"));
                r.Add(new Choice("zip", "ZIP", "Create a ZIP archive"));
                return r;
            }
            if (kinds.Length != 1) return new List<Choice> { new Choice("zip", "ZIP", "Bundle these files into one archive"), new Choice("info", "INFO", "View file information") };
            string[] formats;
            switch (kinds[0]) {
                case "image": formats = new[] { "png", "webp", "jpg", "tiff", "avif", "bmp", "pdf", "gif" }; break;
                case "video": formats = new[] { "mp4", "mov", "mkv", "webm", "avi", "gif", "mp3" }; break;
                case "audio": formats = new[] { "mp3", "m4a", "wav", "flac", "ogg", "opus", "aiff" }; break;
                case "subtitle": formats = new[] { "srt", "vtt", "txt" }; break;
                case "document": formats = new[] { "txt", "md", "html", "docx" }; break;
                case "sheet": formats = new[] { "csv", "tsv", "xlsx" }; break;
                case "archive": return new List<Choice> { new Choice("extract", "EXTRACT", "Extract ZIP into a new folder"), new Choice("info", "INFO", "View file information") };
                case "pdf": formats = new[] { "jpg", "png" }; break;
                default: return new List<Choice> { new Choice("zip", "ZIP", "Create a ZIP archive"), new Choice("info", "INFO", "View file information"), new Choice("copy-path", "COPY PATH", "Copy file paths") };
            }
            return formats.Select(f => new Choice(f, f.ToUpperInvariant(), f == "pdf" ? "Create a PDF from these images" : "Convert to " + f.ToUpperInvariant())).ToList();
        }
    }

    public sealed class Parameters
    {
        public int Width = 1920, X = 0, Y = 0, CropWidth = 800, CropHeight = 600;
        public double Start = 0, Duration = 10, Speed = 2;
        public VideoJoinOptions JoinOptions;
        public double[] SplitPoints;
        public List<VideoMask> Redactions;
    }

    public static class Engine
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        public static string Tool(string name)
        {
            string local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name + ".exe");
            if (File.Exists(local)) return local;
            string media = Path.Combine(MediaTools.Folder, name + ".exe");
            if (File.Exists(media)) return media;
            foreach (string p in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';')) {
                try { string test = Path.Combine(p.Trim('"'), name + ".exe"); if (File.Exists(test)) return test; } catch { }
            }
            throw new FileNotFoundException(name + ".exe is missing. Keep it beside the app.");
        }
        public static string Quote(string s)
        {
            // Windows CommandLineToArgvW quoting: preserve quotes and trailing backslashes.
            return "\"" + Regex.Replace(s, "(\\\\*)\"", "$1$1\\\"").TrimEnd('\\') + new string('\\', s.Reverse().TakeWhile(c => c == '\\').Count() * 2) + "\"";
        }
        public static void Run(string tool, IEnumerable<string> args, CancellationToken ct, Action<string> log)
        {
            ct.ThrowIfCancellationRequested();
            var start = new ProcessStartInfo(Tool(tool), string.Join(" ", args.Select(Quote))) {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true,
                StandardErrorEncoding = Encoding.UTF8, StandardOutputEncoding = Encoding.UTF8
            };
            using (var p = new Process { StartInfo = start }) {
                var errors = new StringBuilder();
                p.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) lock (errors) { errors.AppendLine(e.Data); if (errors.Length > 12000) errors.Remove(0, errors.Length - 8000); } };
                p.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) {
                    if (e.Data != null && e.Data.StartsWith("out_time=")) { if (log != null) log("Processed " + e.Data.Substring(9)); }
                };
                p.Start(); p.BeginErrorReadLine(); p.BeginOutputReadLine();
                using (ct.Register(delegate { try { if (!p.HasExited) p.Kill(); } catch { } })) { p.WaitForExit(); }
                ct.ThrowIfCancellationRequested();
                if (p.ExitCode != 0) {
                    string[] lines = errors.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    throw new InvalidOperationException(lines.Length > 0 ? string.Join("\n", lines.Skip(Math.Max(0, lines.Length - 4))) : "The conversion could not be completed.");
                }
            }
        }
        public static string Publish(string temp, string source, string extension, string suffix)
        {
            string stem = Path.GetFileNameWithoutExtension(source);
            for (int i = 1; i <= 10000; i++) {
                string name = stem + " (" + suffix + (i == 1 ? "" : " " + i) + ")" + extension;
                string target = Path.Combine(Path.GetDirectoryName(source), name);
                try { File.Move(temp, target); return target; }
                catch (IOException) { if (!File.Exists(target) && !Directory.Exists(target)) throw; }
            }
            throw new IOException("Could not find an unused output name.");
        }
        static string Temp(string source, string ext) { return Path.Combine(Path.GetDirectoryName(source), ".tangerine-" + Guid.NewGuid().ToString("N") + ext); }
        public static string Convert(string[] files, string operation, Parameters options, CancellationToken ct, Action<string> log)
        {
            if (files.Length == 0) throw new InvalidOperationException("Choose a file first.");
            foreach (string f in files) { if (!File.Exists(f)) throw new FileNotFoundException("The input file no longer exists.", f); }
            if (!Enumerable.Range(0, 3).SelectMany(m => Catalog.Choices(files, m)).Any(c => c.Id == operation && c.Id != "info" && c.Id != "folder" && c.Id != "copy-path")) throw new InvalidOperationException("This operation is not supported for the selected files.");
            if (files.Length > 1 && operation != "zip" && operation != "pdf" && operation != "video-join") throw new InvalidOperationException("This operation processes one file at a time.");
            ct.ThrowIfCancellationRequested();
            if (Catalog.Kind(files[0]) == "pdf" && new[] { "jpg", "png", "pdf-split" }.Contains(operation)) return PdfExport.Save(files[0], operation, ct, log);
            if (operation == "video-join") return VideoJoin.Save(files, options.JoinOptions, ct, log);
            if (operation == "video-split") return VideoOperations.Split(files[0], options.SplitPoints, ct, log);
            if (operation == "video-redact") return VideoOperations.Redact(files[0], options.Redactions, ct, log);
            if (operation == "zip") return Zip(files, ct);
            if (operation == "extract") return Extract(files[0], ct);
            string source = files[0], kind = Catalog.Kind(source), ext = "." + operation;
            bool tool = new[] { "compress", "resize", "crop", "trim", "speed", "video-crop", "mute" }.Contains(operation);
            if (tool) {
                ext = kind == "image" ? (Path.GetExtension(source).ToLowerInvariant() == ".jpg" || Path.GetExtension(source).ToLowerInvariant() == ".jpeg" ? ".jpg" : ".webp") : kind == "video" ? ".mp4" : ".mp3";
            }
            if (operation == "mute") ext = Path.GetExtension(source).ToLowerInvariant();
            string temp = Temp(source, ext);
            try {
                if (operation == "pdf") { MakePdf(files, temp, ct); }
                else if (operation == "mute") {
                    var muted = new List<string> { "-hide_banner", "-v", "error", "-nostdin", "-n", "-i", source, "-map", "0", "-c", "copy", "-an" };
                    if (ext == ".m4v") muted.AddRange(new[] { "-f", "mp4" });
                    muted.AddRange(new[] { "-progress", "pipe:1", temp }); Run("ffmpeg", muted, ct, log);
                }
                else if (kind == "subtitle") { ConvertSubtitle(source, temp, operation); }
                else if (kind == "document") { ConvertDocument(source, temp, operation); }
                else if (kind == "sheet") { WriteTable(temp, operation, ReadTable(source)); }
                else if (kind == "image" || kind == "video" || kind == "audio") {
                    var a = new List<string> { "-hide_banner", "-loglevel", "error", "-nostdin", "-n" };
                    if (operation == "trim") { a.AddRange(new[] { "-ss", options.Start.ToString(Inv) }); }
                    a.AddRange(new[] { "-i", source });
                    if (operation == "trim") a.AddRange(new[] { "-t", options.Duration.ToString(Inv) });
                    if (kind == "image") {
                        a.AddRange(new[] { "-frames:v", "1", "-update", "1" });
                        if (operation == "resize") a.AddRange(new[] { "-vf", "scale=w='min(" + options.Width + ",iw)':h=-1" });
                        if (operation == "crop") a.AddRange(new[] { "-vf", string.Format(Inv, "crop={0}:{1}:{2}:{3}", options.CropWidth, options.CropHeight, options.X, options.Y) });
                        if (ext == ".jpg") a.AddRange(new[] { "-q:v", operation == "compress" ? "5" : "2" });
                        if (ext == ".webp") a.AddRange(new[] { "-quality", operation == "compress" ? "65" : "90" });
                        if (ext == ".avif") a.AddRange(new[] { "-c:v", "libaom-av1", "-crf", "30", "-cpu-used", "6", "-still-picture", "1" });
                    } else {
                        bool audioOnly = kind == "audio" || ext == ".mp3";
                        if (audioOnly) a.Add("-vn");
                        else {
                            if (ext == ".webm") a.AddRange(new[] { "-c:v", "libvpx-vp9", "-crf", "32", "-b:v", "0", "-deadline", "realtime", "-cpu-used", "6", "-c:a", "libopus" });
                            else if (ext == ".gif") a.AddRange(new[] { "-vf", "fps=12,scale=640:-1:flags=lanczos", "-an", "-loop", "0" });
                            else if (ext == ".avi") a.AddRange(new[] { "-c:v", "mpeg4", "-q:v", "4", "-c:a", "libmp3lame" });
                            else a.AddRange(new[] { "-c:v", "libx264", "-preset", "fast", "-crf", operation == "compress" ? "29" : "22", "-pix_fmt", "yuv420p", "-vf", "scale=trunc(iw/2)*2:trunc(ih/2)*2", "-c:a", "aac", "-b:a", "160k" });
                            if (operation == "video-crop") {
                                if (options.X < 0 || options.Y < 0 || options.CropWidth < 2 || options.CropHeight < 2 || options.CropWidth % 2 != 0 || options.CropHeight % 2 != 0) throw new ArgumentException("Choose a crop of at least 2 × 2 pixels with even dimensions.");
                                int filter = a.IndexOf("-vf"); a[filter + 1] = string.Format(Inv, "crop={0}:{1}:{2}:{3}:exact=1,setsar=1", options.CropWidth, options.CropHeight, options.X, options.Y);
                            }
                            if (ext == ".mp4" || ext == ".mov") a.AddRange(new[] { "-movflags", "+faststart" });
                        }
                        if (operation == "speed") {
                            // atempo handles missing audio by using an optional audio map.
                            a.RemoveRange(a.IndexOf("-vf"), 2);
                            a.AddRange(new[] { "-vf", "setpts=PTS/" + options.Speed.ToString(Inv) + ",scale=trunc(iw/2)*2:trunc(ih/2)*2", "-af", VideoOperations.AudioTempo(options.Speed) });
                        }
                        if (audioOnly) {
                            switch (ext) {
                                case ".mp3": a.AddRange(new[] { "-c:a", "libmp3lame", "-b:a", operation == "compress" ? "128k" : "192k" }); break;
                                case ".m4a": a.AddRange(new[] { "-c:a", "aac", "-b:a", "192k" }); break;
                                case ".wav": a.AddRange(new[] { "-c:a", "pcm_s16le" }); break;
                                case ".flac": a.AddRange(new[] { "-c:a", "flac" }); break;
                                case ".ogg": a.AddRange(new[] { "-c:a", "libvorbis", "-q:a", "5" }); break;
                                case ".opus": a.AddRange(new[] { "-c:a", "libopus", "-b:a", "128k" }); break;
                                case ".aiff": a.AddRange(new[] { "-c:a", "pcm_s16be" }); break;
                            }
                        }
                    }
                    a.AddRange(new[] { "-progress", "pipe:1", temp });
                    Run("ffmpeg", a, ct, log);
                } else { throw new InvalidOperationException("This conversion is not available in this preview."); }
                ct.ThrowIfCancellationRequested();
                if (!File.Exists(temp) || new FileInfo(temp).Length == 0) throw new IOException("The converter did not produce an output file.");
                return Publish(temp, source, ext, operation == "mute" ? "muted" : operation == "compress" ? "compressed" : tool ? operation : "converted");
            } finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        static void ConvertSubtitle(string source, string target, string format)
        {
            string t = File.ReadAllText(source).TrimStart('\uFEFF').Replace("\r\n", "\n").Replace("\r", "\n");
            var blocks = Regex.Split(t, "\n[ \t]*\n");
            var cues = new List<string[]>();
            foreach (string b in blocks) {
                string[] lines = b.Split('\n');
                int time = Array.FindIndex(lines, x => x.Contains("-->"));
                if (time < 0 || time + 1 >= lines.Length) continue;
                Match m = Regex.Match(lines[time], @"(?<a>(?:\d{2,}:)?\d{2}:\d{2}[.,]\d{3})\s*-->\s*(?<b>(?:\d{2,}:)?\d{2}:\d{2}[.,]\d{3})");
                if (!m.Success) throw new InvalidDataException("A subtitle timestamp could not be read.");
                cues.Add(new[] { m.Groups["a"].Value, m.Groups["b"].Value, string.Join("\n", lines.Skip(time + 1)) });
            }
            if (cues.Count == 0) throw new InvalidDataException("No subtitle cues were found.");
            var sb = new StringBuilder(format == "vtt" ? "WEBVTT\n\n" : "");
            int n = 0;
            foreach (string[] c in cues) {
                if (format == "txt") { sb.AppendLine(Regex.Replace(c[2], "<[^>]+>", "")); continue; }
                string a = c[0].Replace(',', '.'), b = c[1].Replace(',', '.');
                if (a.Count(x => x == ':') == 1) a = "00:" + a;
                if (b.Count(x => x == ':') == 1) b = "00:" + b;
                if (format == "srt") { a = a.Replace('.', ','); b = b.Replace('.', ','); sb.AppendLine((++n).ToString()); }
                sb.AppendLine(a + " --> " + b).AppendLine(c[2]).AppendLine();
            }
            File.WriteAllText(target, sb.ToString(), new UTF8Encoding(false));
        }
        static XDocument Xml(Stream stream)
        {
            using (var r = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 32000000 })) return XDocument.Load(r);
        }
        static string TextFromDocument(string source)
        {
            string e = Path.GetExtension(source).ToLowerInvariant();
            if (e == ".docx") {
                using (var z = ZipFile.OpenRead(source)) {
                    var entry = z.GetEntry("word/document.xml");
                    if (entry == null) throw new InvalidDataException("The DOCX document has no text body.");
                    using (var s = entry.Open()) {
                        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                        return string.Join(Environment.NewLine, Xml(s).Descendants(w + "p").Select(p => string.Concat(p.Descendants().Where(x => x.Name == w + "t" || x.Name == w + "tab" || x.Name == w + "br").Select(x => x.Name == w + "tab" ? "\t" : x.Name == w + "br" ? "\n" : x.Value))));
                    }
                }
            }
            string text = File.ReadAllText(source);
            if (e == ".html" || e == ".htm") {
                text = Regex.Replace(text, @"<(script|style)\b[^>]*>.*?</\1>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                text = Regex.Replace(text, @"</(?:p|div|h[1-6]|li)>|<br\s*/?>", "\n", RegexOptions.IgnoreCase);
                text = System.Net.WebUtility.HtmlDecode(Regex.Replace(text, "<[^>]+>", ""));
            }
            return text;
        }
        static void Entry(ZipArchive z, string path, string content)
        {
            using (var writer = new StreamWriter(z.CreateEntry(path).Open(), new UTF8Encoding(false))) writer.Write(content);
        }
        static string Escape(string s) { return SecurityElement.Escape(s) ?? ""; }
        static void ConvertDocument(string source, string target, string format)
        {
            string text = TextFromDocument(source);
            if (format == "docx") {
                using (var z = ZipFile.Open(target, ZipArchiveMode.Create)) {
                    Entry(z, "[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
                    Entry(z, "_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
                    Entry(z, "word/document.xml", "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>" + string.Join("", text.Replace("\r", "").Split('\n').Select(l => "<w:p><w:r><w:t xml:space=\"preserve\">" + Escape(l) + "</w:t></w:r></w:p>")) + "</w:body></w:document>");
                }
            } else if (format == "html") File.WriteAllText(target, "<!doctype html><html><head><meta charset=\"utf-8\"><title>" + Escape(Path.GetFileNameWithoutExtension(source)) + "</title></head><body><pre style=\"white-space:pre-wrap;font-family:system-ui\" dir=\"auto\">" + Escape(text) + "</pre></body></html>", new UTF8Encoding(false));
            else File.WriteAllText(target, text, new UTF8Encoding(false));
        }
        public static List<List<string>> ParseDelimited(string text, char delimiter)
        {
            var rows = new List<List<string>>(); var row = new List<string>(); var cell = new StringBuilder(); bool quoted = false;
            for (int i = 0; i < text.Length; i++) {
                char c = text[i];
                if (c == '"') {
                    if (quoted && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else if (quoted || cell.Length == 0) quoted = !quoted; else cell.Append(c);
                } else if (!quoted && c == delimiter) { row.Add(cell.ToString()); cell.Clear(); }
                else if (!quoted && (c == '\r' || c == '\n')) {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    row.Add(cell.ToString()); cell.Clear(); rows.Add(row); row = new List<string>();
                } else cell.Append(c);
            }
            if (quoted) throw new InvalidDataException("The table contains an unclosed quoted field.");
            if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
            return rows;
        }
        static string Col(int n) { string s = ""; do { s = (char)('A' + n % 26) + s; n = n / 26 - 1; } while (n >= 0); return s; }
        static int ColIndex(string s) { int n = 0; foreach (char c in s.TakeWhile(char.IsLetter)) n = n * 26 + char.ToUpperInvariant(c) - 'A' + 1; return n - 1; }
        public static List<List<string>> ReadTable(string source)
        {
            string e = Path.GetExtension(source).ToLowerInvariant();
            if (e != ".xlsx") return ParseDelimited(File.ReadAllText(source).TrimStart('\uFEFF'), e == ".tsv" ? '\t' : ',');
            using (var z = ZipFile.OpenRead(source)) {
                XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                var strings = new List<string>(); var ss = z.GetEntry("xl/sharedStrings.xml");
                if (ss != null) using (var s = ss.Open()) strings = Xml(s).Descendants(ns + "si").Select(x => string.Concat(x.Descendants(ns + "t").Select(t => t.Value))).ToList();
                string sheetPath = "xl/worksheets/sheet1.xml";
                var wb = z.GetEntry("xl/workbook.xml"); var rel = z.GetEntry("xl/_rels/workbook.xml.rels");
                if (wb != null && rel != null) {
                    string id; using (var s = wb.Open()) { var sheet = Xml(s).Descendants(ns + "sheet").FirstOrDefault(); id = sheet == null ? null : (string)sheet.Attribute(XName.Get("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships")); }
                    using (var s = rel.Open()) { var r = Xml(s).Descendants().FirstOrDefault(x => (string)x.Attribute("Id") == id); if (r != null) { string t = (string)r.Attribute("Target"); sheetPath = t.StartsWith("/") ? t.TrimStart('/') : "xl/" + t; } }
                }
                var entry = z.GetEntry(sheetPath); if (entry == null) throw new InvalidDataException("The first worksheet was not found.");
                var rows = new List<List<string>>();
                using (var s = entry.Open()) foreach (var xr in Xml(s).Descendants(ns + "row")) {
                    var row = new List<string>();
                    foreach (var c in xr.Elements(ns + "c")) {
                        string address = (string)c.Attribute("r") ?? Col(row.Count); int index = ColIndex(address);
                        if (index < 0 || index > 16383) throw new InvalidDataException("A worksheet cell address is invalid.");
                        while (row.Count <= index) row.Add("");
                        string type = (string)c.Attribute("t"), value = (string)c.Element(ns + "v") ?? "";
                        if (type == "s") { int j; if (!int.TryParse(value, out j) || j < 0 || j >= strings.Count) throw new InvalidDataException("A shared string is invalid."); value = strings[j]; }
                        if (type == "inlineStr") value = string.Concat(c.Descendants(ns + "t").Select(x => x.Value));
                        row[index] = value;
                    }
                    rows.Add(row);
                }
                return rows;
            }
        }
        static void WriteTable(string target, string format, List<List<string>> rows)
        {
            if (format != "xlsx") {
                char d = format == "tsv" ? '\t' : ',';
                File.WriteAllText(target, string.Join("\r\n", rows.Select(r => string.Join(d.ToString(), r.Select(v => v.IndexOfAny(new[] { d, '"', '\r', '\n' }) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v)))) + "\r\n", new UTF8Encoding(true)); return;
            }
            using (var z = ZipFile.Open(target, ZipArchiveMode.Create)) {
                Entry(z, "[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
                Entry(z, "_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
                Entry(z, "xl/workbook.xml", "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Sheet1\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
                Entry(z, "xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
                var sb = new StringBuilder("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
                for (int r = 0; r < rows.Count; r++) { sb.Append("<row r=\"").Append(r + 1).Append("\">"); for (int c = 0; c < rows[r].Count; c++) sb.Append("<c r=\"").Append(Col(c)).Append(r + 1).Append("\" t=\"inlineStr\"><is><t xml:space=\"preserve\">").Append(Escape(rows[r][c])).Append("</t></is></c>"); sb.Append("</row>"); }
                Entry(z, "xl/worksheets/sheet1.xml", sb.Append("</sheetData></worksheet>").ToString());
            }
        }
        static string Zip(string[] sources, CancellationToken ct)
        {
            string temp = Temp(sources[0], ".zip");
            try {
                using (var z = ZipFile.Open(temp, ZipArchiveMode.Create)) {
                    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (string f in sources) {
                        ct.ThrowIfCancellationRequested(); string n = Path.GetFileName(f); int i = 1;
                        while (!names.Add(n)) n = Path.GetFileNameWithoutExtension(f) + " (" + (++i) + ")" + Path.GetExtension(f);
                        using (var input = File.OpenRead(f)) using (var output = z.CreateEntry(n, CompressionLevel.Optimal).Open()) Copy(input, output, ct);
                    }
                }
                ct.ThrowIfCancellationRequested(); return Publish(temp, sources[0], ".zip", "archive");
            } finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        static void Copy(Stream input, Stream output, CancellationToken ct)
        {
            var b = new byte[81920]; int n;
            while ((n = input.Read(b, 0, b.Length)) > 0) { ct.ThrowIfCancellationRequested(); output.Write(b, 0, n); }
        }
        static string Extract(string source, CancellationToken ct)
        {
            string root = Path.Combine(Path.GetDirectoryName(source), Path.GetFileNameWithoutExtension(source) + " (extracted " + Guid.NewGuid().ToString("N").Substring(0, 8) + ")");
            // Inspect all paths before creating any output directory.
            using (var z = ZipFile.OpenRead(source)) {
                string prefix = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
                long total = 0;
                foreach (var e in z.Entries) {
                    ct.ThrowIfCancellationRequested();
                    string relative = e.FullName.Replace('/', '\\');
                    if (Path.IsPathRooted(relative) || relative.Contains(":")) throw new InvalidDataException("The archive contains an unsafe path.");
                    string path = Path.GetFullPath(Path.Combine(root, relative));
                    if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The archive contains a path outside its folder.");
                    if (((e.ExternalAttributes >> 16) & 0xF000) == 0xA000) throw new InvalidDataException("Symbolic links are not extracted.");
                    total = checked(total + e.Length); if (total > 4L * 1024 * 1024 * 1024) throw new InvalidDataException("This preview extracts ZIP archives up to 4 GB.");
                }
                if (Directory.Exists(root)) throw new IOException("The destination already exists.");
                Directory.CreateDirectory(root);
                try {
                    foreach (var e in z.Entries) {
                        ct.ThrowIfCancellationRequested(); string target = Path.GetFullPath(Path.Combine(root, e.FullName.Replace('/', '\\')));
                        if (e.FullName.EndsWith("/") || e.FullName.EndsWith("\\")) { Directory.CreateDirectory(target); continue; }
                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                        using (var input = e.Open()) using (var output = new FileStream(target, FileMode.CreateNew)) Copy(input, output, ct);
                    }
                } catch { Directory.Delete(root, true); throw; }
            }
            return root;
        }
        static void MakePdf(string[] files, string target, CancellationToken ct)
        {
            // Each image becomes an independent page with a JPEG image stream.
            var offsets = new List<long> { 0 };
            using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write)) {
                Action<string> write = s => { byte[] b = Encoding.ASCII.GetBytes(s); output.Write(b, 0, b.Length); };
                Action<int, string> obj = (n, body) => { offsets.Add(output.Position); write(n + " 0 obj\n" + body + "\nendobj\n"); };
                write("%PDF-1.4\n%MadarDrop\n");
                obj(1, "<< /Type /Catalog /Pages 2 0 R >>");
                obj(2, "<< /Type /Pages /Count " + files.Length + " /Kids [" + string.Join(" ", Enumerable.Range(0, files.Length).Select(i => (3 + i * 3) + " 0 R")) + "] >>");
                for (int i = 0; i < files.Length; i++) {
                    ct.ThrowIfCancellationRequested(); string tempPng = Temp(files[i], ".png");
                    try {
                        string image = files[i];
                        if (!NativePdfImage(image)) { Run("ffmpeg", new[] { "-hide_banner", "-loglevel", "error", "-nostdin", "-n", "-i", files[i], "-frames:v", "1", "-update", "1", tempPng }, ct, null); image = tempPng; }
                        using (var original = new Drawing.Bitmap(image)) using (var rgb = new Drawing.Bitmap(original.Width, original.Height, Drawing.Imaging.PixelFormat.Format24bppRgb)) using (var jpeg = new MemoryStream()) {
                            using (var g = Drawing.Graphics.FromImage(rgb)) { g.Clear(Drawing.Color.White); g.DrawImage(original, 0, 0, original.Width, original.Height); }
                            rgb.Save(jpeg, Drawing.Imaging.ImageFormat.Jpeg); byte[] bytes = jpeg.ToArray();
                            double w = Math.Min(595.0, original.Width * 0.75), h = w * original.Height / original.Width;
                            if (h > 842) { w *= 842 / h; h = 842; }
                            string ws = w.ToString("0.###", Inv), hs = h.ToString("0.###", Inv); int n = 3 + i * 3;
                            obj(n, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 " + ws + " " + hs + "] /Resources << /XObject << /Im0 " + (n + 2) + " 0 R >> >> /Contents " + (n + 1) + " 0 R >>");
                            string command = "q " + ws + " 0 0 " + hs + " 0 0 cm /Im0 Do Q\n";
                            obj(n + 1, "<< /Length " + Encoding.ASCII.GetByteCount(command) + " >>\nstream\n" + command + "endstream");
                            offsets.Add(output.Position);
                            write((n + 2) + " 0 obj\n<< /Type /XObject /Subtype /Image /Width " + original.Width + " /Height " + original.Height + " /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length " + bytes.Length + " >>\nstream\n");
                            output.Write(bytes, 0, bytes.Length); write("\nendstream\nendobj\n");
                        }
                    } finally { if (File.Exists(tempPng)) File.Delete(tempPng); }
                }
                long xref = output.Position; write("xref\n0 " + offsets.Count + "\n0000000000 65535 f \n");
                foreach (long offset in offsets.Skip(1)) write(offset.ToString("0000000000", Inv) + " 00000 n \n");
                write("trailer\n<< /Size " + offsets.Count + " /Root 1 0 R >>\nstartxref\n" + xref + "\n%%EOF\n");
            }
        }
        public static bool NativePdfImage(string file) { return new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff", ".gif", ".ico" }.Contains(Path.GetExtension(file).ToLowerInvariant()); }
    }
}
