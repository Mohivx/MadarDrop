using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;

namespace TangerineWindows
{
    public static class ReleaseTests
    {
        public static int Run(string root) {
            var report = new List<string>(); Action<bool, string> check = (ok, text) => { if (!ok) throw new Exception(text); report.Add("PASS " + text); };
            try {
                if (Directory.Exists(root)) throw new IOException("Use a new test folder."); Directory.CreateDirectory(root);
                check(UpdateCheck.IsNewer("v0.3.1", new Version("0.3.0.0")), "Newer stable release tags offer an update");
                check(!UpdateCheck.IsNewer("v0.3.0", new Version("0.3.0.0")) && !UpdateCheck.IsNewer("v0.3.0.0", new Version("0.3.0.0")), "Equal three- and four-component versions do not offer a false update");
                check(!UpdateCheck.IsNewer("v0.2.1", new Version("0.3.0.0")) && !UpdateCheck.IsNewer("invalid", new Version("0.3.0.0")), "Older and invalid release tags are rejected");
                string[] image = { "example.png" };
                check(MediaTools.NeedsEngine(image, "webp") && MediaTools.NeedsEngine(new[] { "sample.mp4" }, "trim"), "Media conversions request the optional engine");
                check(!MediaTools.NeedsEngine(new[] { "a.png", "b.jpg" }, "pdf") && MediaTools.NeedsEngine(new[] { "a.webp" }, "pdf"), "Common images create PDFs offline; unsupported native image formats request FFmpeg");
                check(new[] { "crop-editor", "edit", "metadata", "folder", "copy-path", "zip", "info" }.All(op => !MediaTools.NeedsEngine(image, op)) && !MediaTools.NeedsEngine(new[] { "sample.pdf" }, "pdf-edit"), "Image editing, PDF and file actions work without a media download");
                string archive = Path.Combine(root, "provider.zip");
                using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create)) {
                    using (var stream = zip.CreateEntry("ffmpeg-release/bin/ffmpeg.exe").Open()) { var fixture = new byte[] { 77, 90, 1, 2, 3 }; stream.Write(fixture, 0, fixture.Length); }
                    using (var writer = new StreamWriter(zip.CreateEntry("ffmpeg-release/LICENSE").Open())) writer.Write("fixture license");
                    using (var writer = new StreamWriter(zip.CreateEntry("../../escape.txt").Open())) writer.Write("never extract this");
                }
                string hash; using (var sha = SHA256.Create()) using (var stream = File.OpenRead(archive)) hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                string destination = Path.Combine(root, "media"); MediaTools.InstallArchive(archive, hash + "  provider.zip", destination);
                check(File.ReadAllBytes(Path.Combine(destination, "ffmpeg.exe")).SequenceEqual(new byte[] { 77, 90, 1, 2, 3 }), "Verified provider archive installs only the selected media executable");
                check(File.ReadAllText(Path.Combine(destination, "FFmpeg-LICENSE.txt")) == "fixture license" && File.ReadAllText(Path.Combine(destination, "SOURCE.txt")).Contains(hash), "External media license, provider and checksum notices are retained");
                check(!File.Exists(Path.Combine(root, "escape.txt")) && Directory.GetFiles(destination).Length == 3, "Other archive entries are never extracted");
                bool corrupt = false; try { MediaTools.InstallArchive(archive, new string('0', 64), destination); } catch (InvalidDataException) { corrupt = true; }
                check(corrupt && File.ReadAllBytes(Path.Combine(destination, "ffmpeg.exe")).Length == 5, "Checksum failures preserve the existing media tool");
                report.Add("RESULT: " + report.Count + " release checks passed."); File.WriteAllLines(Path.Combine(root, "release-test-results.txt"), report); return 0;
            } catch (Exception e) { report.Add("FAIL " + e); Directory.CreateDirectory(root); File.WriteAllLines(Path.Combine(root, "release-test-results.txt"), report); return 1; }
        }
    }
}
