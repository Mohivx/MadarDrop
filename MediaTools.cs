using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace TangerineWindows
{
    public static class MediaTools
    {
        public static string Folder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MadarDrop", "media"); } }
        public static bool Ready { get { try { Engine.Tool("ffmpeg"); return true; } catch (FileNotFoundException) { return false; } } }
        public static bool NeedsEngine(string[] paths, string operation) {
            if (operation == "metadata" && paths.Any(p => new[] { "video", "audio" }.Contains(Catalog.Kind(p)))) return true;
            if (paths.Length == 0 || new[] { "more", "edit", "annotate", "redact", "background", "crop-editor", "collage", "metadata", "info", "copy-path", "folder", "reveal", "zip", "extract" }.Contains(operation)) return false;
            if (operation == "pdf" && paths.All(Engine.NativePdfImage)) return false;
            return paths.Any(p => new[] { "image", "video", "audio" }.Contains(Catalog.Kind(p)));
        }
        public static string VerifyArchive(string archive, string checksum) {
            var match = Regex.Match(checksum.Trim(), @"\A[0-9a-fA-F]{64}(?:\s|\z)");
            if (!match.Success) throw new InvalidDataException("Invalid provider checksum.");
            string actual; using (var sha = SHA256.Create()) using (var file = File.OpenRead(archive)) actual = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "");
            if (!actual.Equals(match.Value.Trim(), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Media download checksum mismatch.");
            return actual;
        }
        public static void InstallArchive(string archive, string checksum, string destination) {
            VerifyArchive(archive, checksum); Directory.CreateDirectory(destination);
            using (var zip = ZipFile.OpenRead(archive)) {
                var exe = zip.Entries.Single(e => e.FullName.Replace('\\', '/').EndsWith("/bin/ffmpeg.exe", StringComparison.OrdinalIgnoreCase));
                if (exe.Length > 800L * 1024 * 1024) throw new InvalidDataException("Unexpected media executable size.");
                string temp = Path.Combine(destination, "ffmpeg-" + Guid.NewGuid().ToString("N") + ".tmp");
                try {
                    using (var input = exe.Open()) using (var output = File.Create(temp)) input.CopyTo(output);
                    using (var input = File.OpenRead(temp)) if (input.ReadByte() != 'M' || input.ReadByte() != 'Z') throw new InvalidDataException("Invalid Windows media executable.");
                    string final = Path.Combine(destination, "ffmpeg.exe"); if (File.Exists(final)) File.Replace(temp, final, null); else File.Move(temp, final);
                    var license = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith("/LICENSE", StringComparison.OrdinalIgnoreCase));
                    if (license != null) using (var reader = new StreamReader(license.Open())) File.WriteAllText(Path.Combine(destination, "FFmpeg-LICENSE.txt"), reader.ReadToEnd());
                    File.WriteAllText(Path.Combine(destination, "SOURCE.txt"), "Downloaded directly from https://www.gyan.dev/ffmpeg/builds/\r\nFFmpeg project and license: https://ffmpeg.org/legal.html\r\nSHA256: " + VerifyArchive(archive, checksum));
                } finally { if (File.Exists(temp)) File.Delete(temp); }
            }
        }
        public static bool Setup(Window owner) {
            bool installed = false, busy = false;
            var window = new Window { Title = Ui.Arabic ? "أدوات الوسائط" : "Media tools", Width = 520, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterScreen, Background = Theme.Paper, Foreground = Theme.Ink };
            if (owner != null && owner.IsVisible) window.Owner = owner;
            var panel = new StackPanel { Margin = new Thickness(24) }; window.Content = panel;
            panel.Children.Add(Theme.Text(Ui.Arabic ? "تحتاج تحويلات الصور والصوت والفيديو إلى FFmpeg. يمكنك تنزيله مباشرة من Gyan.dev أو اختيار نسخة مثبتة. التعديل على الصور وPDF جاهز بدون تنزيل." : "Image, audio and video conversions use FFmpeg. Download it directly from Gyan.dev, or choose your existing copy. Image and PDF editing are ready without this download.", 14, Theme.Ink));
            var label = Theme.Text(Ready ? (Ui.Arabic ? "الأدوات موجودة وجاهزة." : "Media tools are ready.") : (Ui.Arabic ? "التنزيل حوالي 110 ميجابايت ويتطلب الإنترنت." : "The download is about 110 MB and needs internet."), 12, Theme.Muted); label.Margin = new Thickness(0, 14, 0, 12); panel.Children.Add(label);
            var download = Theme.Button(Ui.Arabic ? "تنزيل FFmpeg من مزوّده" : "Download FFmpeg from provider", true); panel.Children.Add(download);
            var choose = Theme.Button(Ui.Arabic ? "اختيار ffmpeg.exe موجود…" : "Choose existing ffmpeg.exe…", false); choose.Margin = new Thickness(0, 10, 0, 0); panel.Children.Add(choose);
            choose.Click += delegate {
                var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "FFmpeg|ffmpeg.exe", CheckFileExists = true };
                if (dialog.ShowDialog(window) == true) try { Directory.CreateDirectory(Folder); string final = Path.Combine(Folder, "ffmpeg.exe"); if (!Path.GetFullPath(dialog.FileName).Equals(final, StringComparison.OrdinalIgnoreCase)) File.Copy(dialog.FileName, final, true); installed = true; window.Close(); } catch (Exception e) { label.Text = e.Message; }
            };
            download.Click += async delegate {
                busy = true; download.IsEnabled = choose.IsEnabled = false;
                string temp = Path.Combine(Path.GetTempPath(), "MadarDrop-media-" + Guid.NewGuid().ToString("N") + ".zip");
                try {
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                    using (var client = new WebClient()) {
                        client.DownloadProgressChanged += delegate(object sender, DownloadProgressChangedEventArgs e) { label.Text = (Ui.Arabic ? "جارٍ التنزيل… " : "Downloading… ") + e.ProgressPercentage + "%"; };
                        string checksum = await client.DownloadStringTaskAsync("https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip.sha256");
                        await client.DownloadFileTaskAsync(new Uri("https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip"), temp);
                        await Task.Run(delegate { InstallArchive(temp, checksum, Folder); });
                    }
                    installed = true; busy = false; window.Close();
                } catch (Exception e) { label.Text = (Ui.Arabic ? "تعذّر التنزيل. يمكنك اختيار نسخة موجودة.\n" : "Download failed. You can select an existing copy.\n") + e.Message; }
                finally { busy = false; download.IsEnabled = choose.IsEnabled = true; if (File.Exists(temp)) File.Delete(temp); }
            };
            window.Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { if (busy) e.Cancel = true; };
            Ui.Attach(window); window.ShowDialog(); return installed || Ready;
        }
    }
}
