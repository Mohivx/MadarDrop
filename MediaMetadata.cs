using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace TangerineWindows
{
    public static class MediaMetadata
    {
        static readonly Dictionary<string, string> names = new Dictionary<string, string> { { "Title", "title" }, { "Artist", "artist" }, { "Album", "album" }, { "Date", "date" }, { "Copyright", "copyright" }, { "Comment", "comment" } };
        public static MetadataInfo Read(string source, CancellationToken ct) {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct)) {
                timeout.CancelAfter(30000);
                var start = new ProcessStartInfo(Engine.Tool("ffmpeg"), string.Join(" ", new[] { "-hide_banner", "-v", "error", "-nostdin", "-i", source, "-f", "ffmetadata", "pipe:1" }.Select(Engine.Quote))) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
                using (var p = new Process { StartInfo = start }) {
                    timeout.Token.ThrowIfCancellationRequested(); p.Start(); var output = p.StandardOutput.ReadToEndAsync(); var error = p.StandardError.ReadToEndAsync();
                    using (timeout.Token.Register(delegate { try { if (!p.HasExited) p.Kill(); } catch { } })) p.WaitForExit();
                    timeout.Token.ThrowIfCancellationRequested(); if (p.ExitCode != 0) throw new InvalidOperationException(error.Result.Trim());
                    var tags = Parse(output.Result); var values = new Dictionary<string, string>(); foreach (var pair in names) { string value; values[pair.Key] = tags.TryGetValue(pair.Value, out value) ? value : ""; }
                    return new MetadataInfo { metadata = values, details = string.Join(Environment.NewLine, tags.Select(p2 => p2.Key + ": " + p2.Value)) + Environment.NewLine + Environment.NewLine + VideoFrames.Describe(source, timeout.Token) };
                }
            }
        }
        public static Dictionary<string, string> Parse(string text) {
            var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var lines = new StringReader(text)) {
                string line; while ((line = lines.ReadLine()) != null) {
                    int trailing = line.Reverse().TakeWhile(c => c == '\\').Count();
                    while (trailing % 2 == 1) { string next = lines.ReadLine(); line = line.Substring(0, line.Length - 1) + "\n" + (next ?? ""); if (next == null) break; trailing = next.Reverse().TakeWhile(c => c == '\\').Count(); }
                    if (line.StartsWith("[")) break; if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;
                    int split = -1; for (int i = 0; i < line.Length; i++) { if (line[i] == '\\') i++; else if (line[i] == '=') { split = i; break; } }
                    if (split > 0) tags[Unescape(line.Substring(0, split))] = Unescape(line.Substring(split + 1));
                }
            }
            return tags;
        }
        static string Unescape(string text) { var result = new StringBuilder(); for (int i = 0; i < text.Length; i++) { if (text[i] == '\\' && i + 1 < text.Length) i++; result.Append(text[i]); } return result.ToString(); }
        public static string Save(string source, Dictionary<string, string> values, bool strip, CancellationToken ct) {
            ct.ThrowIfCancellationRequested(); if (!new[] { "video", "audio" }.Contains(Catalog.Kind(source))) throw new ArgumentException("Choose an audio or video file.");
            if (values == null || values.Any(p => !names.ContainsKey(p.Key) || (p.Value != null && (p.Value.Length > 16000 || p.Value.Contains("\0"))))) throw new ArgumentException("Invalid metadata field.");
            string extension = Path.GetExtension(source).ToLowerInvariant(), temp = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(source)), ".tangerine-" + Guid.NewGuid().ToString("N") + extension);
            try {
                var args = new List<string> { "-hide_banner", "-v", "error", "-nostdin", "-n", "-i", source, "-map", "0", "-c", "copy" };
                args.AddRange(strip ? new[] { "-map_metadata", "-1", "-map_metadata:s:v", "-1", "-map_metadata:s:a", "-1", "-map_chapters", "-1" } : new[] { "-map_metadata", "0" });
                foreach (var pair in values) args.AddRange(new[] { "-metadata", names[pair.Key] + "=" + (pair.Value ?? "") });
                if (new[] { ".mp4", ".mov", ".m4v" }.Contains(extension)) args.AddRange(new[] { "-movflags", "use_metadata_tags" });
                if (extension == ".m4v") args.AddRange(new[] { "-f", "mp4" }); args.Add(temp); Engine.Run("ffmpeg", args, ct, null);
                var saved = Read(temp, ct); foreach (var pair in values) if (saved.metadata[pair.Key].Replace("\r\n", "\n") != (pair.Value ?? "").Replace("\r\n", "\n")) throw new InvalidOperationException("This format cannot store the selected field: " + Ui.T(pair.Key));
                ct.ThrowIfCancellationRequested(); return Engine.Publish(temp, source, extension, "metadata");
            } finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
