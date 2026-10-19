using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
namespace TangerineUpdate
{
    static class Maintenance
    {
        [STAThread] static int Main(string[] args) {
            UpdateCore.EnableLongPaths(); Application.EnableVisualStyles();
            try {
                if (args.Length == 1 && args[0] == "--uninstall") {
                    string root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
                    if (MessageBox.Show("إزالة MadarDrop؟ ستبقى ملفاتك ونسخ التحديث الاحتياطية وإعداداتك.\n\nRemove MadarDrop? Your files, update backups and preferences will be kept.", "MadarDrop", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return 0;
                    string temp = Path.Combine(Path.GetTempPath(), "MadarDrop-remove-" + Guid.NewGuid().ToString("N") + ".exe"); File.Copy(Process.GetCurrentProcess().MainModule.FileName, temp);
                    Process.Start(new ProcessStartInfo(temp, "--remove \"" + root + "\" " + Process.GetCurrentProcess().Id) { UseShellExecute = false }); return 0;
                }
                if (args.Length == 3 && args[0] == "--remove") {
                    int parent; if (int.TryParse(args[2], out parent)) try { using (var process = Process.GetProcessById(parent)) process.WaitForExit(10000); } catch (ArgumentException) { }
                    string root = Path.GetFullPath(args[1]); SetupSupport.Stop(Path.Combine(root, "TangerineWindows.exe")); int count = SetupSupport.RemoveManaged(root); SetupSupport.Unregister(root);
                    MessageBox.Show("تمت إزالة ملفات التطبيق. بقيت ملفاتك والنسخ الاحتياطية في المجلد.\n\nApplication files removed: " + count + ". Your files and backups were kept.", "MadarDrop"); return 0;
                }
                return 2;
            } catch (Exception e) { MessageBox.Show(e.Message, "MadarDrop", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
        }
    }
}
