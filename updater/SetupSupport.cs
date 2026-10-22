using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace TangerineUpdate
{
    public static class SetupSupport
    {
        public const string Version = "0.4.4";
        public const string Marker = "MadarDrop.install.json";
        public const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\MadarDrop";
        public static string Normal(string path) { return path.StartsWith(@"\\?\UNC\") ? @"\\" + path.Substring(8) : path.StartsWith(@"\\?\") ? path.Substring(4) : path; }
        public static string DefaultFolder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "MadarDrop"); } }
        public static bool IsApp(string folder) { try { UpdateCore.ValidateTarget(Path.Combine(folder, "TangerineWindows.exe")); return true; } catch { return false; } }
        public static Version InstalledVersion(string folder) {
            string temp = Path.Combine(Path.GetTempPath(), "md-version-" + Guid.NewGuid().ToString("N") + ".exe");
            try { File.Copy(UpdateCore.Child(folder, "TangerineWindows.exe"), temp); return AssemblyName.GetAssemblyName(temp).Version; }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public static string Find(IEnumerable<string> candidates) { foreach (string folder in candidates.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase)) if (IsApp(folder)) return Normal(Path.GetFullPath(folder)); return null; }
        public static string Detect() {
            var candidates = new List<string>();
            using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath)) if (key != null) candidates.Add(key.GetValue("InstallLocation") as string);
            foreach (var process in Process.GetProcessesByName("TangerineWindows")) using (process) try { candidates.Add(Path.GetDirectoryName(process.MainModule.FileName)); } catch { }
            string beside = AppDomain.CurrentDomain.BaseDirectory; candidates.Add(beside); candidates.Add(DefaultFolder);
            try { candidates.AddRange(Directory.GetDirectories(beside).Where(p => Path.GetFileName(p).StartsWith("Tangerine-Windows-Preview-", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(p).StartsWith("MadarDrop-", StringComparison.OrdinalIgnoreCase))); } catch { }
            return Find(candidates);
        }
        public static string ActionFor(string target) {
            if (!IsApp(target)) return "install";
            var current = InstalledVersion(target); var latest = new Version(Version + ".0");
            return current > latest ? "newer" : current == latest ? "repair" : "update";
        }
        public static void Stop(string executable) {
            foreach (var process in Process.GetProcessesByName("TangerineWindows")) using (process) {
                string path; try { path = process.MainModule.FileName; } catch { continue; }
                if (!Path.GetFullPath(path).Equals(Path.GetFullPath(executable), StringComparison.OrdinalIgnoreCase)) continue;
                process.Kill(); if (!process.WaitForExit(10000)) throw new IOException("Close MadarDrop and try again.");
            }
        }
        public static void WriteMarker(string target, PayloadManifest manifest) {
            string path = UpdateCore.Child(target, Marker), temp = UpdateCore.Child(target, ".md-install-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            try { File.WriteAllText(temp, new JavaScriptSerializer { MaxJsonLength = 20000000 }.Serialize(manifest), new UTF8Encoding(false)); if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public static PayloadManifest ReadMarker(string target) { return new JavaScriptSerializer { MaxJsonLength = 20000000 }.Deserialize<PayloadManifest>(File.ReadAllText(UpdateCore.Child(target, Marker))); }
        public static string ShortcutPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "MadarDrop.lnk"); } }
        public static void Shortcut(string path, string executable) {
            object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")), link = null;
            try {
                link = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
                link.GetType().InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, new object[] { Normal(executable) });
                link.GetType().InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, link, new object[] { Normal(Path.GetDirectoryName(executable)) });
                link.GetType().InvokeMember("Description", BindingFlags.SetProperty, null, link, new object[] { "MadarDrop file tools" });
                link.GetType().InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
            } finally { if (link != null) Marshal.FinalReleaseComObject(link); Marshal.FinalReleaseComObject(shell); }
        }
        public static void Register(string target, bool desktop) {
            target = Normal(Path.GetFullPath(target)); string executable = Path.Combine(target, "TangerineWindows.exe");
            using (var key = Registry.CurrentUser.CreateSubKey(RegistryPath)) {
                key.SetValue("DisplayName", "MadarDrop"); key.SetValue("DisplayVersion", Version); key.SetValue("Publisher", "MadarDrop contributors"); key.SetValue("InstallLocation", target);
                key.SetValue("DisplayIcon", executable); key.SetValue("UninstallString", "\"" + Path.Combine(target, "MadarDrop.Uninstall.exe") + "\" --uninstall");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord); key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key.SetValue("EstimatedSize", 45000, RegistryValueKind.DWord);
            }
            Shortcut(ShortcutPath, executable);
            if (desktop) Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "MadarDrop.lnk"), executable);
        }
        public static int RemoveManaged(string target) {
            UpdateCore.ValidateTarget(Path.Combine(target, "TangerineWindows.exe")); var manifest = ReadMarker(target);
            if (manifest == null || manifest.Files == null || !manifest.Files.Any(f => f.Path == "TangerineWindows.exe")) throw new InvalidDataException("Invalid installation record.");
            var remove = new List<string>();
            foreach (var file in manifest.Files) { string path = UpdateCore.Child(target, file.Path); UpdateCore.RejectLinks(target, path); if (File.Exists(path) && UpdateCore.Hash(path).Equals(file.Hash, StringComparison.OrdinalIgnoreCase)) remove.Add(path); }
            foreach (string path in remove) File.Delete(path);
            File.Delete(UpdateCore.Child(target, Marker));
            return remove.Count;
        }
        static void RemoveShortcut(string path, string target) {
            if (!File.Exists(path)) return;
            object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")), link = null;
            try { link = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path }); string executable = (string)link.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, link, null); if (Path.GetDirectoryName(executable).Equals(Normal(Path.GetFullPath(target)), StringComparison.OrdinalIgnoreCase)) File.Delete(path); }
            finally { if (link != null) Marshal.FinalReleaseComObject(link); Marshal.FinalReleaseComObject(shell); }
        }
        public static void Unregister(string target) {
            using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath)) if (key != null && string.Equals(key.GetValue("InstallLocation") as string, Normal(Path.GetFullPath(target)), StringComparison.OrdinalIgnoreCase)) { key.Close(); Registry.CurrentUser.DeleteSubKey(RegistryPath); }
            RemoveShortcut(ShortcutPath, target); RemoveShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "MadarDrop.lnk"), target);
        }
    }
}
