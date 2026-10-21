using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace TangerineWindows
{
    /// <summary>
    /// Offline PDF page export: every page to a 300 DPI JPG/PNG, or one standalone PDF per page.
    /// Output is staged in a hidden GUID sibling folder and published with one atomic folder move.
    /// </summary>
    public static class PdfExport
    {
        public const int MaxPages = 300;
        public const int Dpi = 300;
        const string StagingPrefix = ".madardrop-pages-";
        const int PollMilliseconds = 60;
        static readonly string[] formats = { "jpg", "png", "pdf-split" };

        /// <summary>Supported format ids: jpg and png (format wheel), pdf-split (tool wheel).</summary>
        public static string[] Formats { get { return (string[])formats.Clone(); } }

        public static bool IsSupported(string format) { return Normalize(format) != null; }

        /// <summary>
        /// Exports all pages of <paramref name="source"/> and returns the new sibling folder holding numbered page files.
        /// Progress receives "Page {done} of {total}" while pages are produced. The source is never modified.
        /// </summary>
        public static string Save(string source, string format, CancellationToken ct, Action<string> progress)
        {
            string kind = Normalize(format);
            if (kind == null) throw new ArgumentException("Choose JPG, PNG, or split PDF pages.", "format");
            if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Choose a PDF to export.", "source");
            string full = Path.GetFullPath(source);
            if (!File.Exists(full)) throw new FileNotFoundException("The PDF could not be found.", full);
            ct.ThrowIfCancellationRequested();
            string folder = Path.GetDirectoryName(full);
            string staging = Path.Combine(folder, StagingPrefix + Guid.NewGuid().ToString("N"));
            if (Directory.Exists(staging) || File.Exists(staging)) throw new IOException("Could not reserve a private working folder next to the PDF.");
            bool created = false;
            try {
                Directory.CreateDirectory(staging); created = true;
                File.SetAttributes(staging, File.GetAttributes(staging) | FileAttributes.Hidden);
                string pages = Path.Combine(staging, "pages"), request = Path.Combine(staging, "request.json");
                string response = Path.Combine(staging, "response.json"), progressFile = Path.Combine(staging, "progress.jsonl");
                Directory.CreateDirectory(pages);
                File.WriteAllText(request, new JavaScriptSerializer().Serialize(new { operation = "export-pages", format = kind, files = new[] { full }, output = pages, progress_file = progressFile }), new UTF8Encoding(false));
                ct.ThrowIfCancellationRequested();
                int reported = RunWorker(request, response, progressFile, ct, progress);
                ct.ThrowIfCancellationRequested();
                Validate(pages, kind, response, reported);
                ct.ThrowIfCancellationRequested();
                return Publish(pages, folder, Path.GetFileNameWithoutExtension(full), kind, ct);
            } finally {
                if (created) DeleteStaging(staging);
            }
        }

        static string Normalize(string format)
        {
            if (format == null) return null;
            string value = format.ToLowerInvariant();
            return Array.IndexOf(formats, value) >= 0 ? value : null;
        }

        static string Extension(string kind) { return kind == "pdf-split" ? ".pdf" : "." + kind; }

        static Exception Incomplete() { return new InvalidOperationException("The exported pages could not be verified, so nothing was saved."); }

        static int RunWorker(string request, string response, string progressFile, CancellationToken ct, Action<string> progress)
        {
            string root = AppDomain.CurrentDomain.BaseDirectory;
            string python = Path.Combine(root, "runtime", "python.exe"), script = Path.Combine(root, "pdf_worker.py");
            if (!File.Exists(python) || !File.Exists(script)) throw new InvalidOperationException("The bundled PDF engine is missing. Reinstall MadarDrop.");
            var start = new ProcessStartInfo(python, string.Join(" ", new[] { script, request, response }.Select(Engine.Quote))) {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true,
                StandardErrorEncoding = Encoding.UTF8, StandardOutputEncoding = Encoding.UTF8
            };
            start.EnvironmentVariables["PYTHONUTF8"] = "1";
            var reader = new ProgressReader(progressFile);
            using (var p = new Process { StartInfo = start }) {
                ct.ThrowIfCancellationRequested();
                try { p.Start(); } catch (Win32Exception e) { throw new InvalidOperationException("The PDF engine could not start: " + e.Message, e); }
                // Drain both pipes from the start so a chatty worker can never block on a full pipe.
                Task<string> err = p.StandardError.ReadToEndAsync(), stdout = p.StandardOutput.ReadToEndAsync();
                try {
                    using (ct.Register(delegate { Kill(p); })) {
                        while (!p.WaitForExit(PollMilliseconds)) {
                            if (ct.IsCancellationRequested) break;
                            reader.Report(progress, ct);
                        }
                    }
                    ct.ThrowIfCancellationRequested();
                    p.WaitForExit();
                    reader.Report(progress, ct);
                    ct.ThrowIfCancellationRequested();
                    if (p.ExitCode != 0) {
                        string message = err.Wait(5000) ? (err.Result ?? "").Trim() : "";
                        throw new InvalidOperationException(message.Length > 0 ? message : "The PDF pages could not be exported.");
                    }
                    return reader.Done;
                } finally {
                    Kill(p);
                    try { p.WaitForExit(15000); } catch { }
                    try { Task.WaitAll(new Task[] { err, stdout }, 5000); } catch { }
                }
            }
        }

        static void Kill(Process p)
        {
            try { if (!p.HasExited) p.Kill(); } catch { }
        }

        static void Validate(string pages, string kind, string responsePath, int reported)
        {
            if (!File.Exists(responsePath)) throw Incomplete();
            Dictionary<string, object> result;
            try { result = new JavaScriptSerializer { MaxJsonLength = 10000000 }.Deserialize<Dictionary<string, object>>(File.ReadAllText(responsePath, Encoding.UTF8)); }
            catch (ArgumentException) { throw Incomplete(); }
            catch (InvalidOperationException) { throw Incomplete(); }
            object value, files;
            if (result == null || !result.TryGetValue("page_count", out value) || !result.TryGetValue("files", out files)) throw Incomplete();
            int count = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            if (count < 1 || count > MaxPages || reported != count) throw Incomplete();
            var expected = new List<string>();
            for (int i = 1; i <= count; i++) expected.Add("page-" + i.ToString("D3", CultureInfo.InvariantCulture) + Extension(kind));
            var listed = files is IEnumerable && !(files is string) ? ((IEnumerable)files).Cast<object>().Select(f => Convert.ToString(f, CultureInfo.InvariantCulture)).ToList() : new List<string>();
            if (!listed.SequenceEqual(expected, StringComparer.Ordinal)) throw Incomplete();
            var actual = Directory.GetFileSystemEntries(pages).Select(Path.GetFileName).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            if (!actual.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase)) throw Incomplete();
            foreach (string name in expected) CheckFile(Path.Combine(pages, name), kind);
        }

        static void CheckFile(string path, string kind)
        {
            var info = new FileInfo(path);
            if ((info.Attributes & FileAttributes.Directory) != 0 || info.Length < 16) throw Incomplete();
            byte[] head = new byte[8], tail = new byte[(int)Math.Min(1024, info.Length)];
            using (var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                ReadFully(s, head);
                s.Seek(-tail.Length, SeekOrigin.End);
                ReadFully(s, tail);
            }
            int n = tail.Length;
            bool ok;
            if (kind == "png")
                ok = head.SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })
                    && tail.Skip(n - 8).SequenceEqual(new byte[] { 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 });
            else if (kind == "jpg")
                ok = head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF && tail[n - 2] == 0xFF && tail[n - 1] == 0xD9;
            else
                ok = Encoding.ASCII.GetString(head, 0, 5) == "%PDF-" && Encoding.ASCII.GetString(tail).Contains("%%EOF");
            if (!ok) throw Incomplete();
        }

        static void ReadFully(Stream s, byte[] buffer)
        {
            int read = 0;
            while (read < buffer.Length) {
                int n = s.Read(buffer, read, buffer.Length - read);
                if (n <= 0) throw Incomplete();
                read += n;
            }
        }

        static string Publish(string pages, string folder, string stem, string kind, CancellationToken ct)
        {
            if (stem.Length > 80) stem = stem.Substring(0, char.IsHighSurrogate(stem[79]) ? 79 : 80);
            stem = stem.TrimEnd(' ', '.');
            if (stem.Length == 0) stem = "PDF";
            string label = kind == "jpg" ? "JPG pages" : kind == "png" ? "PNG pages" : "split pages";
            for (int n = 1; n < 10000; n++) {
                ct.ThrowIfCancellationRequested();
                string target = Path.Combine(folder, stem + " - " + label + (n == 1 ? "" : " (" + n.ToString(CultureInfo.InvariantCulture) + ")"));
                if (Directory.Exists(target) || File.Exists(target)) continue;
                try {
                    // Same-volume rename: atomic, and fails instead of merging if the name was taken meanwhile.
                    Directory.Move(pages, target);
                    return target;
                } catch (IOException) {
                    if (Directory.Exists(target) || File.Exists(target)) continue;
                    throw;
                }
            }
            throw new IOException("Could not find a free folder name next to the PDF.");
        }

        static void DeleteStaging(string path)
        {
            for (int attempt = 0; attempt < 25; attempt++) {
                try {
                    if (!Directory.Exists(path)) return;
                    Directory.Delete(path, true);
                    return;
                } catch (IOException) {
                } catch (UnauthorizedAccessException) { }
                Thread.Sleep(40 + attempt * 20);
            }
        }

        /// <summary>Reads complete JSON lines appended by the worker ({"done": n, "total": t}) without locking the file.</summary>
        sealed class ProgressReader
        {
            readonly string path;
            readonly StringBuilder pending = new StringBuilder();
            readonly JavaScriptSerializer json = new JavaScriptSerializer();
            long position;
            public int Done { get; private set; }

            public ProgressReader(string path) { this.path = path; }

            public void Report(Action<string> progress, CancellationToken ct)
            {
                string text;
                try {
                    if (!File.Exists(path)) return;
                    using (var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
                        long available = s.Length - position;
                        if (available <= 0) return;
                        s.Seek(position, SeekOrigin.Begin);
                        var buffer = new byte[(int)Math.Min(available, 1 << 20)];
                        int read = s.Read(buffer, 0, buffer.Length);
                        if (read <= 0) return;
                        position += read;
                        text = Encoding.UTF8.GetString(buffer, 0, read);
                    }
                } catch (IOException) { return; } catch (UnauthorizedAccessException) { return; }
                pending.Append(text);
                string all = pending.ToString();
                int end = all.LastIndexOf('\n');
                if (end < 0) return;
                pending.Clear().Append(all.Substring(end + 1));
                foreach (string line in all.Substring(0, end).Split('\n')) {
                    if (ct.IsCancellationRequested) return;
                    int done, total;
                    if (!TryParse(line, out done, out total) || done != Done + 1 || total < done || total > MaxPages) continue;
                    Done = done;
                    if (progress != null) progress("Page " + done.ToString(CultureInfo.InvariantCulture) + " of " + total.ToString(CultureInfo.InvariantCulture));
                }
            }

            bool TryParse(string line, out int done, out int total)
            {
                done = total = 0;
                try {
                    var row = json.DeserializeObject(line.Trim()) as Dictionary<string, object>;
                    if (row == null || !row.ContainsKey("done") || !row.ContainsKey("total")) return false;
                    done = Convert.ToInt32(row["done"], CultureInfo.InvariantCulture);
                    total = Convert.ToInt32(row["total"], CultureInfo.InvariantCulture);
                    return true;
                } catch { return false; }
            }
        }
    }
}
