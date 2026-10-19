using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;

namespace TangerineWindows
{
    public sealed class GitHubRelease { public string tag_name { get; set; } public string html_url { get; set; } public bool prerelease { get; set; } }
    public static class UpdateCheck
    {
        public static bool IsNewer(string tag, Version current) { Version latest; if (!Version.TryParse((tag ?? "").TrimStart('v', 'V'), out latest)) return false; return new Version(latest.Major, latest.Minor, Math.Max(0, latest.Build), Math.Max(0, latest.Revision)) > new Version(current.Major, current.Minor, Math.Max(0, current.Build), Math.Max(0, current.Revision)); }
        public static async void Check(Window owner) {
            var window = new Window { Title = Ui.Arabic ? "تحديثات مدار دروب" : "MadarDrop updates", Width = 470, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterScreen, Background = Theme.Paper, Foreground = Theme.Ink };
            if (owner != null && owner.IsVisible) window.Owner = owner;
            var panel = new StackPanel { Margin = new Thickness(24) }; window.Content = panel;
            var status = Theme.Text(Ui.Arabic ? "جارٍ البحث عن تحديث على GitHub…" : "Checking GitHub for an update…", 14, Theme.Ink); panel.Children.Add(status);
            var download = Theme.Button(Ui.Arabic ? "فتح صفحة الإصدار" : "Open release page", true); download.Visibility = Visibility.Collapsed; download.Margin = new Thickness(0, 16, 0, 0); panel.Children.Add(download); Ui.Attach(window); window.Show();
            try {
                string json = await Task.Run(delegate {
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                    var request = (HttpWebRequest)WebRequest.Create("https://api.github.com/repos/Mohivx/MadarDrop/releases/latest"); request.UserAgent = "MadarDrop/0.3.1"; request.Timeout = 15000; request.ReadWriteTimeout = 15000;
                    using (var response = request.GetResponse()) using (var reader = new StreamReader(response.GetResponseStream())) return reader.ReadToEnd();
                });
                var release = new JavaScriptSerializer().Deserialize<GitHubRelease>(json); var current = Assembly.GetExecutingAssembly().GetName().Version;
                bool newer = IsNewer(release.tag_name, current);
                status.Text = newer ? (Ui.Arabic ? "يتوفر تحديث: " : "Update available: ") + release.tag_name : (Ui.Arabic ? "لديك أحدث إصدار منشور." : "You have the latest published release.");
                download.Visibility = Visibility.Visible; download.Click += delegate { Process.Start(new ProcessStartInfo("https://github.com/Mohivx/MadarDrop/releases/latest") { UseShellExecute = true }); };
            } catch (Exception e) { status.Text = (Ui.Arabic ? "تعذّر البحث. حاول لاحقًا.\n" : "Could not check for updates. Try again later.\n") + e.Message; }
        }
    }
}
