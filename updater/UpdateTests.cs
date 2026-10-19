using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace TangerineUpdate
{
    public static class UpdateTests
    {
        static List<string> report = new List<string>();
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); report.Add("PASS " + message); }
        public static int Main(string[] args)
        {
            UpdateCore.EnableLongPaths();
            string root = Path.GetFullPath(args[0]);
            try {
                if (Directory.Exists(root)) throw new IOException("Use a new test directory."); Directory.CreateDirectory(root);
                string target = Path.Combine(root, "installed"), package = Path.Combine(root, "package"); Directory.CreateDirectory(target); Directory.CreateDirectory(package);
                File.Copy(args[1], Path.Combine(target, "TangerineWindows.exe")); File.Copy(args[2], Path.Combine(package, "TangerineWindows.exe"));
                File.WriteAllText(Path.Combine(target, "README.ar.md"), "old notes"); File.WriteAllText(Path.Combine(target, "my-photo.jpg"), "user data");
                Directory.CreateDirectory(Path.Combine(package, "runtime")); File.WriteAllText(Path.Combine(package, "README.ar.md"), "new notes"); File.WriteAllText(Path.Combine(package, "runtime", "helper.dat"), "new helper"); File.WriteAllText(Path.Combine(package, "runtime", "new-first.dat"), "new file");
                string zipPath = Path.Combine(root, "payload.zip"); ZipFile.CreateFromDirectory(package, zipPath);
                var manifest = new PayloadManifest { Version = "0.2.1", Files = Directory.GetFiles(package, "*", SearchOption.AllDirectories).OrderBy(f => f.EndsWith("helper.dat") ? "zzzz" : f).Select(f => new PayloadFile { Path = f.Substring(package.Length + 1).Replace('\\', '/'), Hash = UpdateCore.Hash(f), Length = new FileInfo(f).Length }).ToList() };
                string oldHash = UpdateCore.Hash(Path.Combine(target, "TangerineWindows.exe"));
                using (var stream = File.OpenRead(zipPath)) using (var staged = UpdateCore.Stage(stream, manifest)) {
                    Check(File.Exists(Path.Combine(staged.Root, "runtime", "helper.dat")), "Staging extracts only verified managed files");
                    var result = UpdateCore.Apply(staged, target, null); Check(result.Changed == 4 && File.Exists(result.Backup), "Update installs changed files and creates a backup");
                    using (var zip = ZipFile.OpenRead(result.Backup)) { var entry = zip.GetEntry("TangerineWindows.exe"); Check(entry != null && zip.GetEntry("restore.json") != null, "Backup includes original executable and restoration manifest"); }
                    Check(UpdateCore.Hash(Path.Combine(target, "TangerineWindows.exe")) == UpdateCore.Hash(args[2]), "Installed executable matches the new build");
                    Check(File.ReadAllText(Path.Combine(target, "my-photo.jpg")) == "user data", "Unmanaged user files are preserved");
                    Check(UpdateCore.Apply(staged, target, null).Changed == 0, "Applying the same update again makes no changes");
                    File.Copy(args[1], Path.Combine(target, "TangerineWindows.exe"), true); File.WriteAllText(Path.Combine(target, "README.ar.md"), "old notes"); File.Delete(Path.Combine(target, "runtime", "helper.dat")); File.Delete(Path.Combine(target, "runtime", "new-first.dat"));
                    bool failed = false; try { UpdateCore.Apply(staged, target, path => { if (path.Replace('\\', '/') == "runtime/helper.dat") throw new IOException("Injected write failure"); }); } catch (IOException) { failed = true; }
                    Check(failed && UpdateCore.Hash(Path.Combine(target, "TangerineWindows.exe")) == oldHash && File.ReadAllText(Path.Combine(target, "README.ar.md")) == "old notes", "A failed update restores previous executable and files");
                    Check(!File.Exists(Path.Combine(target, "runtime", "helper.dat")) && !File.Exists(Path.Combine(target, "runtime", "new-first.dat")) && File.ReadAllText(Path.Combine(target, "my-photo.jpg")) == "user data", "Rollback removes newly added files and preserves unrelated user files");
                }
                var record = manifest.Files[0]; string hash = record.Hash; record.Hash = new string('0', 64); bool badHash = false;
                try { using (var stream = File.OpenRead(zipPath)) using (UpdateCore.Stage(stream, manifest)) { } } catch (InvalidDataException) { badHash = true; } record.Hash = hash;
                Check(badHash, "Corrupted payload checksums are rejected before installation");
                bool traversal = false; try { UpdateCore.Child(target, "../escape.txt"); } catch (InvalidDataException) { traversal = true; } Check(traversal, "Paths outside the installation folder are rejected");
                bool ads = false; try { UpdateCore.Child(target, "file:stream"); } catch (InvalidDataException) { ads = true; } Check(ads, "Alternate stream paths are rejected");
                bool wrong = false; try { UpdateCore.ValidateTarget(Path.Combine(target, "my-photo.jpg")); } catch (IOException) { wrong = true; } Check(wrong, "Updater rejects a non-application destination");
                string deep = UpdateCore.Long(Path.Combine(root, new string('d', 70), new string('e', 60), new string('f', 45)));
                Directory.CreateDirectory(deep); File.Copy(args[1], Path.Combine(deep, "TangerineWindows.exe"));
                Check(deep.Length > 260, "Regression fixture exceeds the legacy Windows directory limit");
                using (var stream = File.OpenRead(zipPath)) using (var staged = UpdateCore.Stage(stream, manifest)) {
                    var result = UpdateCore.Apply(staged, deep, null);
                    Check(result.Changed == 4 && UpdateCore.Hash(Path.Combine(deep, "runtime", "helper.dat")) == UpdateCore.Hash(Path.Combine(package, "runtime", "helper.dat")), "Update and checksum verification succeed beyond 260 characters");
                    File.Copy(args[1], Path.Combine(deep, "TangerineWindows.exe"), true); File.Delete(Path.Combine(deep, "runtime", "helper.dat"));
                    bool reverted = false; try { UpdateCore.Apply(staged, deep, path => { if (path.Contains("helper.dat")) throw new IOException("Injected long-path failure"); }); } catch (IOException) { reverted = true; }
                    Check(reverted && UpdateCore.Hash(Path.Combine(deep, "TangerineWindows.exe")) == oldHash, "Rollback also succeeds on long installation paths");
                    string fresh = Path.Combine(root, "fresh"); UpdateCore.Install(staged, fresh, false, null);
                    Check(UpdateCore.Hash(Path.Combine(fresh, "TangerineWindows.exe")) == UpdateCore.Hash(args[2]), "Fresh installation uses the same verified payload");
                    File.WriteAllText(Path.Combine(root, "occupied.txt"), "user"); bool occupied = false; try { UpdateCore.Install(staged, root, false, null); } catch (IOException) { occupied = true; }
                    Check(occupied && File.ReadAllText(Path.Combine(root, "occupied.txt")) == "user", "New installation refuses a nonempty unrelated folder");
                }
                report.Add("RESULT: " + report.Count + " update checks passed."); File.WriteAllLines(Path.Combine(root, "update-test-results.txt"), report, Encoding.UTF8); return 0;
            } catch (Exception e) { report.Add("FAIL " + e); Directory.CreateDirectory(root); File.WriteAllLines(Path.Combine(root, "update-test-results.txt"), report, Encoding.UTF8); return 1; }
        }
    }
}
