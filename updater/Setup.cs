using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: AssemblyTitle("MadarDrop Setup")]
[assembly: AssemblyVersion("0.4.4.0")]
[assembly: AssemblyFileVersion("0.4.4.0")]
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
namespace TangerineUpdate
{
    public sealed class SetupForm : Form
    {
        readonly TextBox target = new TextBox { ReadOnly = true, Width = 520, RightToLeft = RightToLeft.No };
        readonly Label heading = new Label { AutoSize = true, Font = new Font("Segoe UI", 18, FontStyle.Bold), Margin = new Padding(0, 0, 0, 14) };
        readonly Label found = new Label { Width = 525, Height = 54 };
        readonly Label note = new Label { Width = 525, Height = 66, Margin = new Padding(0, 14, 0, 5) };
        readonly Label status = new Label { Width = 525, Height = 100, AutoEllipsis = true };
        readonly Button apply = new Button { Width = 195, Height = 42, BackColor = Color.FromArgb(255, 101, 31), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        readonly Button browse = new Button { AutoSize = true };
        readonly CheckBox launch = new CheckBox { Checked = true, AutoSize = true };
        readonly CheckBox desktop = new CheckBox { Checked = false, AutoSize = true };
        readonly ComboBox language = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 135 };
        bool busy, done;
        bool Arabic { get { return language.SelectedIndex != 1; } }
        string T(string ar, string en) { return Arabic ? ar : en; }
        public SetupForm(string initial, bool english) {
            Text = "MadarDrop Setup 0.4.4"; ClientSize = new Size(610, 600); StartPosition = FormStartPosition.CenterScreen; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; Font = new Font("Segoe UI", 10); BackColor = Color.FromArgb(245, 247, 247);
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); BackColor = Color.FromArgb(247, 245, 242);
            var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(28), AutoScroll = true };
            language.Items.AddRange(new object[] { "العربية", "English" }); language.SelectedIndex = english ? 1 : 0;
            panel.Controls.Add(language); panel.Controls.Add(heading); panel.Controls.Add(found); panel.Controls.Add(target); panel.Controls.Add(browse); panel.Controls.Add(note); panel.Controls.Add(desktop); panel.Controls.Add(launch); panel.Controls.Add(apply); panel.Controls.Add(status); Controls.Add(panel);
            target.Text = initial ?? SetupSupport.Detect() ?? SetupSupport.DefaultFolder;
            browse.Click += delegate { using (var dialog = new FolderBrowserDialog { SelectedPath = target.Text, Description = T("اختر مجلد النسخة الحالية أو مجلدًا فارغًا للتثبيت", "Choose the existing application folder or an empty folder") }) if (dialog.ShowDialog(this) == DialogResult.OK) { target.Text = dialog.SelectedPath; RefreshText(); } };
            language.SelectedIndexChanged += delegate { RefreshText(); }; apply.Click += Apply;
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (busy) e.Cancel = true; }; RefreshText();
        }
        void RefreshText() {
            RightToLeft = Arabic ? RightToLeft.Yes : RightToLeft.No; RightToLeftLayout = Arabic;
            string action = SetupSupport.ActionFor(target.Text);
            heading.Text = T("مدار دروب", "MadarDrop");
            found.Text = action == "install" ? T("جاهز لتثبيت MadarDrop 0.4.4 على جهازك.", "Ready to install MadarDrop 0.4.4 on this PC.") : T("وجدنا نسختك الحالية: ", "Existing installation found: ") + SetupSupport.InstalledVersion(target.Text) + (action == "update" ? T(" — يتوفر تحديث إلى 0.4.4", " — update available: 0.4.4") : action == "newer" ? T(" — أحدث من هذا المثبّت", " — newer than this installer") : T(" — لديك أحدث نسخة من هذه الحزمة", " — this package is already installed"));
            browse.Text = T("تغيير المجلد…", "Change folder…"); desktop.Text = T("إنشاء اختصار على سطح المكتب", "Create a desktop shortcut"); launch.Text = T("تشغيل التطبيق بعد الانتهاء", "Launch the app when finished");
            note.Text = T("احفظ تعديلاتك قبل المتابعة. يحتفظ التحديث بملفاتك وإعداداتك ويصنع نسخة احتياطية. التثبيت خاص بحسابك ولا يحتاج صلاحية مسؤول.", "Save your edits before continuing. Updates keep your files and preferences and create a backup. Installation is for your account and needs no administrator access.");
            apply.Text = done ? T("إغلاق", "Close") : action == "update" ? T("تحديث الآن", "Update now") : action == "repair" ? T("إصلاح النسخة الحالية", "Repair installation") : T("تثبيت الآن", "Install now");
            apply.Enabled = action != "newer" && !busy;
            if (!busy && !done) status.Text = T("أدوات الصور وPDF مضمنة. أدوات تحويل الوسائط الموجودة تُحفظ، ويمكن تنزيلها من الإعدادات إذا احتجتها.", "Image and PDF tools are included. Existing media tools are kept; additional media tools can be downloaded from Settings when needed.");
        }
        void Status(string message) { if (IsHandleCreated) BeginInvoke(new Action(delegate { status.Text = message; })); }
        async void Apply(object sender, EventArgs args) {
            if (done) { Close(); return; }
            string folder = Path.GetFullPath(target.Text), executable = Path.Combine(folder, "TangerineWindows.exe");
            bool existing = SetupSupport.IsApp(folder), start = launch.Checked, shortcut = desktop.Checked, stopped = false;
            if (SetupSupport.ActionFor(folder) == "newer") return;
            busy = true; apply.Enabled = browse.Enabled = launch.Enabled = desktop.Enabled = language.Enabled = false; status.Text = T("جارٍ التحقق من الحزمة…", "Verifying the package…");
            try {
                UpdateResult result = await Task.Run(delegate {
                    using (var payload = SetupProgram.Payload()) {
                        var changed = UpdateCore.Install(payload, folder, existing, path => { if (existing && !stopped) { SetupSupport.Stop(executable); stopped = true; } Status(T("جارٍ التثبيت: ", "Installing: ") + path); });
                        SetupSupport.WriteMarker(folder, payload.Manifest); return changed;
                    }
                });
                string warning = ""; try { SetupSupport.Register(folder, shortcut); } catch (Exception e) { warning = "\n" + T("اكتمل التثبيت، لكن تعذّر إنشاء بعض الاختصارات: ", "Installed, but some shortcuts could not be created: ") + e.Message; }
                done = true; status.Text = T("اكتمل التثبيت بنجاح.", "Installation completed.") + (result.Backup == null ? "" : "\n" + T("النسخة الاحتياطية: ", "Backup: ") + SetupSupport.Normal(result.Backup)) + warning;
                if (start) try { Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true }); } catch (Exception e) { status.Text += "\n" + e.Message; }
            } catch (Exception e) {
                status.Text = T("لم تكتمل العملية. ", "The operation did not complete. ") + e.Message;
                MessageBox.Show(this, e.Message, "MadarDrop Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (stopped && existing && File.Exists(executable)) try { Process.Start(new ProcessStartInfo(executable, "--background") { UseShellExecute = true }); } catch { }
            } finally { busy = false; browse.Enabled = launch.Enabled = desktop.Enabled = language.Enabled = !done; apply.Enabled = true; if (done) apply.Text = T("إغلاق", "Close"); }
        }
    }
    public static class SetupProgram
    {
        public static StagedPayload Payload() {
            var assembly = Assembly.GetExecutingAssembly(); PayloadManifest manifest;
            using (var input = assembly.GetManifestResourceStream("manifest.json")) using (var reader = new StreamReader(input)) manifest = new JavaScriptSerializer { MaxJsonLength = 20000000 }.Deserialize<PayloadManifest>(reader.ReadToEnd());
            using (var stream = assembly.GetManifestResourceStream("payload.zip")) return UpdateCore.Stage(stream, manifest);
        }
        static int Test(string root, string oldExecutable) {
            root = Path.GetFullPath(root); var report = new List<string>(); Action<bool, string> check = (ok, text) => { if (!ok) throw new Exception(text); report.Add("PASS " + text); };
            try {
                if (Directory.Exists(root)) throw new IOException("Use a new test folder."); Directory.CreateDirectory(root);
                string target = Path.Combine(root, "portable-at-the-same-depth-as-the-users-original-application"), preserved = Path.Combine(target, "my-document.txt");
                using (var payload = Payload()) {
                    check(!payload.Manifest.Files.Any(f => f.Path.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase)), "Public installer does not redistribute FFmpeg");
                    UpdateCore.Install(payload, target, false, null); SetupSupport.WriteMarker(target, payload.Manifest); File.WriteAllText(preserved, "my file");
                    check(SetupSupport.IsApp(target) && SetupSupport.ActionFor(target) == "repair", "Fresh install is detected and offers repair instead of a duplicate install");
                    check(SetupSupport.Find(new[] { root, target }) == target, "Existing portable application is discovered from candidates");
                    check(UpdateCore.Apply(payload, target, null).Changed == 0, "Full payload is idempotent at a deep path");
                    string damaged = UpdateCore.Child(target, "README.ar.md"); File.WriteAllText(damaged, "changed");
                    var repaired = UpdateCore.Apply(payload, target, null); check(repaired.Changed == 1 && File.Exists(repaired.Backup), "Repair replaces a damaged managed file and backs it up");
                    File.WriteAllText(damaged, "keep my custom notes");
                    SetupSupport.Shortcut(Path.Combine(root, "test-start-menu.lnk"), Path.Combine(target, "TangerineWindows.exe")); check(File.Exists(Path.Combine(root, "test-start-menu.lnk")), "Windows shortcut creation succeeds in the isolated fixture");
                    int removed = SetupSupport.RemoveManaged(target);
                    check(removed > 200 && !File.Exists(Path.Combine(target, "TangerineWindows.exe")), "Uninstaller removes verified application files");
                    check(File.ReadAllText(preserved) == "my file" && File.ReadAllText(damaged) == "keep my custom notes" && File.Exists(repaired.Backup), "Uninstall preserves user files, modified managed files and backups");
                    if (oldExecutable != null) {
                        string legacy = Path.Combine(root, "Tangerine-Windows-Preview-0.1"); Directory.CreateDirectory(legacy); File.Copy(oldExecutable, Path.Combine(legacy, "TangerineWindows.exe")); File.WriteAllText(Path.Combine(legacy, "ffmpeg.exe"), "existing external media tool"); File.WriteAllText(Path.Combine(legacy, "my-output.png"), "user output");
                        check(SetupSupport.ActionFor(legacy) == "update", "Legacy preview is recognized and offered an update");
                        var updated = UpdateCore.Apply(payload, legacy, null); SetupSupport.WriteMarker(legacy, payload.Manifest);
                        check(SetupSupport.InstalledVersion(legacy) == new Version("0.4.4.0") && File.Exists(updated.Backup), "Full legacy upgrade succeeds in place with a backup");
                        check(File.ReadAllText(Path.Combine(legacy, "ffmpeg.exe")) == "existing external media tool" && File.ReadAllText(Path.Combine(legacy, "my-output.png")) == "user output", "Legacy upgrade keeps the existing media engine and converted files");
                    }
                }
                report.Add("RESULT: " + report.Count + " full installer checks passed."); File.WriteAllLines(Path.Combine(root, "setup-test-results.txt"), report); return 0;
            } catch (Exception e) { report.Add("FAIL " + e); Directory.CreateDirectory(root); File.WriteAllLines(Path.Combine(root, "setup-test-results.txt"), report); return 1; }
        }
        [STAThread] public static int Main(string[] args) {
            UpdateCore.EnableLongPaths();
            if (args.Length > 1 && args[0] == "--test-package") return Test(args[1], args.Length > 2 ? args[2] : null);
            if (args.Length > 1 && args[0] == "--verify-payload") { try { using (var payload = Payload()) File.WriteAllText(args[1], "PASS " + payload.Manifest.Files.Count + " files verified"); return 0; } catch (Exception e) { File.WriteAllText(args[1], "FAIL " + e); return 1; } }
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length > 1 && args[0] == "--render-ui") { using (var form = new SetupForm(args.Length > 2 ? args[2] : null, args.Contains("--english"))) { form.Show(); Application.DoEvents(); using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(args[1]); } } return 0; }
            Application.Run(new SetupForm(null, args.Contains("--english"))); return 0;
        }
    }
}
