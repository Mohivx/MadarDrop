using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace TangerineUpdate
{
    public sealed class PayloadFile { public string Path { get; set; } public string Hash { get; set; } public long Length { get; set; } }
    public sealed class PayloadManifest { public string Version { get; set; } public List<PayloadFile> Files { get; set; } }
    public sealed class RestoreFile { public string Path { get; set; } public bool Existed { get; set; } }
    public sealed class UpdateResult { public string Backup; public int Changed; }
    public sealed class StagedPayload : IDisposable
    {
        public string Root;
        public PayloadManifest Manifest;
        public void Dispose() {
            string parent = UpdateCore.Long(System.IO.Path.GetTempPath()).TrimEnd('\\');
            if (Root != null && System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Root)).Equals(parent, StringComparison.OrdinalIgnoreCase) && System.IO.Path.GetFileName(Root).StartsWith("TangerineUpdate-", StringComparison.Ordinal) && Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
    public static class UpdateCore
    {
        static UpdateCore() { EnableLongPaths(); }
        public static void EnableLongPaths() {
            AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling", false);
            AppContext.SetSwitch("Switch.System.IO.BlockLongPaths", false);
        }
        public static string Long(string path) {
            string full = System.IO.Path.GetFullPath(path);
            if (full.StartsWith(@"\\?\", StringComparison.Ordinal)) return full;
            return full.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\?\UNC\" + full.Substring(2) : @"\\?\" + full;
        }
        public static string Hash(string file) { using (var sha = SHA256.Create()) using (var stream = File.OpenRead(file)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""); }
        public static string Child(string root, string relative)
        {
            string normalized = relative.Replace('/', '\\');
            if (string.IsNullOrWhiteSpace(normalized) || System.IO.Path.IsPathRooted(normalized) || normalized.Contains(":") || normalized.Split('\\').Any(x => x == ".." || x == "." || x == "" || x.EndsWith(" ") || x.EndsWith("."))) throw new InvalidDataException("Unsafe update path: " + relative);
            string full = Long(System.IO.Path.Combine(Long(root), normalized));
            string prefix = Long(root).TrimEnd('\\') + "\\";
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update path is outside the application folder.");
            return full;
        }
        public static void RejectLinks(string root, string file)
        {
            string fullRoot = Long(root).TrimEnd('\\'); string current = Long(file);
            while (current != null && current.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) {
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked files or folders cannot be updated: " + current);
                if (current.Equals(fullRoot, StringComparison.OrdinalIgnoreCase)) break;
                current = System.IO.Path.GetDirectoryName(current);
            }
        }
        public static void ValidateTarget(string executable)
        {
            executable = Long(executable);
            if (!File.Exists(executable) || !System.IO.Path.GetFileName(executable).Equals("TangerineWindows.exe", StringComparison.OrdinalIgnoreCase)) throw new IOException("Select the existing TangerineWindows.exe file.");
            if (new FileInfo(executable).Length > 32000000) throw new IOException("Unexpected application size.");
            string metadataCopy = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "md-check-" + Guid.NewGuid().ToString("N") + ".exe");
            try {
                File.Copy(executable, metadataCopy);
                if (AssemblyName.GetAssemblyName(metadataCopy).Name != "TangerineWindows") throw new IOException("This is not a MadarDrop / legacy Tangerine Windows application.");
            } finally { if (File.Exists(metadataCopy)) File.Delete(metadataCopy); }
            RejectLinks(System.IO.Path.GetDirectoryName(executable), executable);
        }
        public static StagedPayload Stage(Stream stream, PayloadManifest manifest)
        {
            if (manifest == null || manifest.Files == null || manifest.Files.Count == 0 || !manifest.Files.Any(f => f.Path == "TangerineWindows.exe")) throw new InvalidDataException("Invalid update manifest.");
            var staged = new StagedPayload { Root = Long(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TangerineUpdate-" + Guid.NewGuid().ToString("N"))), Manifest = manifest };
            Directory.CreateDirectory(staged.Root);
            try {
                var expected = new Dictionary<string, PayloadFile>(StringComparer.OrdinalIgnoreCase);
                foreach (var f in manifest.Files) { Child(staged.Root, f.Path); if (expected.ContainsKey(f.Path.Replace('\\', '/'))) throw new InvalidDataException("Duplicate update path."); expected.Add(f.Path.Replace('\\', '/'), f); }
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, true)) foreach (var entry in zip.Entries) {
                    if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\")) continue;
                    string key = entry.FullName.Replace('\\', '/'); PayloadFile record;
                    if (!expected.TryGetValue(key, out record) || !seen.Add(key)) throw new InvalidDataException("Unexpected update file: " + key);
                    if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000) throw new InvalidDataException("Links are not allowed in updates.");
                    if (entry.Length != record.Length || entry.Length > 1024L * 1024 * 1024) throw new InvalidDataException("Incorrect update file length.");
                    string path = Child(staged.Root, key); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                    using (var input = entry.Open()) using (var output = new FileStream(path, FileMode.CreateNew)) input.CopyTo(output);
                    if (!Hash(path).Equals(record.Hash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update checksum mismatch: " + key);
                }
                if (seen.Count != expected.Count) throw new InvalidDataException("The update package is incomplete.");
                ValidateTarget(Child(staged.Root, "TangerineWindows.exe")); return staged;
            } catch { staged.Dispose(); throw; }
        }
        static void ReplaceFromStream(Stream input, string destination)
        {
            string temp = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(destination), ".md-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            try {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination));
                using (var output = new FileStream(temp, FileMode.CreateNew)) input.CopyTo(output);
                if (File.Exists(destination)) File.Replace(temp, destination, null); else File.Move(temp, destination);
            } finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public static UpdateResult Apply(StagedPayload payload, string target, Action<string> beforeWrite)
        { return Install(payload, target, true, beforeWrite); }
        public static UpdateResult Install(StagedPayload payload, string target, bool existing, Action<string> beforeWrite)
        {
            target = Long(target);
            if (existing) ValidateTarget(System.IO.Path.Combine(target, "TangerineWindows.exe"));
            else if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any()) throw new IOException("Choose an empty folder for a new installation.");
            Directory.CreateDirectory(target); RejectLinks(target, target);
            var changed = payload.Manifest.Files.Where(f => { string p = Child(target, f.Path); RejectLinks(target, p); return !File.Exists(p) || !Hash(p).Equals(f.Hash, StringComparison.OrdinalIgnoreCase); }).ToList();
            if (changed.Count == 0) return new UpdateResult { Changed = 0 };
            string backup = System.IO.Path.Combine(target, "Tangerine-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".zip");
            var restore = changed.Select(f => new RestoreFile { Path = f.Path, Existed = File.Exists(Child(target, f.Path)) }).ToList();
            using (var stream = new FileStream(backup, FileMode.CreateNew)) using (var zip = new ZipArchive(stream, ZipArchiveMode.Create)) {
                foreach (var item in restore.Where(r => r.Existed)) { using (var input = File.OpenRead(Child(target, item.Path))) using (var output = zip.CreateEntry(item.Path.Replace('\\', '/'), CompressionLevel.Optimal).Open()) input.CopyTo(output); }
                using (var writer = new StreamWriter(zip.CreateEntry("restore.json").Open(), new UTF8Encoding(false))) writer.Write(new JavaScriptSerializer().Serialize(new { Target = target, Files = restore }));
            }
            var applied = new List<RestoreFile>();
            string writing = "";
            try {
                foreach (var record in restore) {
                    writing = record.Path;
                    if (beforeWrite != null) beforeWrite(record.Path);
                    string destination = Child(target, record.Path); RejectLinks(target, destination);
                    using (var input = File.OpenRead(Child(payload.Root, record.Path))) ReplaceFromStream(input, destination);
                    applied.Add(record);
                }
                foreach (var record in changed) if (!Hash(Child(target, record.Path)).Equals(record.Hash, StringComparison.OrdinalIgnoreCase)) throw new IOException("Post-update verification failed.");
            } catch (Exception original) {
                try {
                    using (var zip = ZipFile.OpenRead(backup)) foreach (var item in applied.AsEnumerable().Reverse()) {
                        string destination = Child(target, item.Path); RejectLinks(target, destination);
                        if (item.Existed) { using (var input = zip.GetEntry(item.Path.Replace('\\', '/')).Open()) ReplaceFromStream(input, destination); }
                        else if (File.Exists(destination)) File.Delete(destination);
                    }
                } catch (Exception rollback) { throw new IOException("Update failed. Restore the backup at " + backup + ". " + rollback.Message, original); }
                throw new IOException("Update failed; original files were restored. File: " + writing + ". Backup: " + backup + ". " + original.Message, original);
            }
            return new UpdateResult { Backup = backup, Changed = changed.Count };
        }
    }
}
