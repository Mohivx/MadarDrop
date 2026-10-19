using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TangerineWindows
{
    public sealed class PdfPage
    {
        public string file { get; set; }
        public int index { get; set; }
        public int rotation { get; set; }
        public string thumbnail { get; set; }
        public string label { get; set; }
        public PdfPage Clone() { return (PdfPage)MemberwiseClone(); }
    }
    public sealed class PdfInfo { public List<PdfPage> pages { get; set; } }
    public static class PdfWorker
    {
        public static string Run(object request, CancellationToken ct)
        {
            string folder = Path.Combine(Path.GetTempPath(), "tangerine-request-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            try {
                string input = Path.Combine(folder, "request.json"), output = Path.Combine(folder, "response.json");
                File.WriteAllText(input, new JavaScriptSerializer().Serialize(request), new UTF8Encoding(false));
                string root = AppDomain.CurrentDomain.BaseDirectory;
                var start = new ProcessStartInfo(Path.Combine(root, "runtime", "python.exe"), string.Join(" ", new[] { Path.Combine(root, "pdf_worker.py"), input, output }.Select(Engine.Quote))) {
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true, StandardErrorEncoding = Encoding.UTF8
                };
                start.EnvironmentVariables["PYTHONUTF8"] = "1";
                using (var p = new Process { StartInfo = start }) {
                    ct.ThrowIfCancellationRequested(); p.Start(); Task<string> err = p.StandardError.ReadToEndAsync(), stdout = p.StandardOutput.ReadToEndAsync();
                    using (ct.Register(delegate { try { p.Kill(); } catch { } })) p.WaitForExit();
                    ct.ThrowIfCancellationRequested();
                    if (p.ExitCode != 0) throw new InvalidOperationException(err.Result.Trim());
                    return File.ReadAllText(output);
                }
            } finally { Directory.Delete(folder, true); }
        }
        public static List<PdfPage> Inspect(string[] files, string folder, CancellationToken ct)
        {
            return new JavaScriptSerializer { MaxJsonLength = 10000000 }.Deserialize<PdfInfo>(Run(new { operation = "inspect", files = files, folder = folder }, ct)).pages;
        }
        public static string Save(string[] files, List<PdfPage> pages, bool strip, string operation, Dictionary<string, string> metadata, CancellationToken ct)
        {
            string temp = Path.Combine(Path.GetDirectoryName(files[0]), ".tangerine-" + Guid.NewGuid().ToString("N") + ".pdf");
            try {
                Run(new { operation = operation, files = files, pages = pages, strip_metadata = strip, metadata = metadata ?? new Dictionary<string, string>(), output = temp }, ct);
                ct.ThrowIfCancellationRequested(); return Engine.Publish(temp, files[0], ".pdf", operation == "compress" ? "compressed" : "edited");
            } finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
    public sealed class PdfEditor : Window
    {
        public string Output;
        readonly ListBox list = new ListBox { SelectionMode = SelectionMode.Single, BorderThickness = new Thickness(0), Background = Brushes.Transparent };
        readonly TextBlock status = Theme.Text("Loading pages…", 12, Theme.Muted);
        readonly CheckBox strip = new CheckBox { Content = "Remove document metadata", Margin = new Thickness(0, 16, 0, 0) };
        readonly List<PdfPage> pages = new List<PdfPage>();
        readonly string temp = Path.Combine(Path.GetTempPath(), "tangerine-pages-" + Guid.NewGuid().ToString("N"));
        readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        readonly string[] sources;
        readonly Button save;
        bool busy;
        public PdfEditor(string[] files)
        {
            Resources.Add(typeof(Button), MainWindow.ButtonStyle());
            sources = files; Title = "Organize PDF pages"; Width = 1050; Height = 770; MinWidth = 850; MinHeight = 600; WindowStartupLocation = WindowStartupLocation.CenterOwner; FontFamily = new FontFamily("Segoe UI"); Background = Theme.Paper;
            var root = new Grid { Background = Theme.Paper, Margin = new Thickness(24) }; root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(70) }); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) }); Content = root;
            var header = new Grid(); header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var title = new StackPanel(); title.Children.Add(Theme.Text("Every page in its place.", 26, Theme.Ink, FontWeights.SemiBold)); title.Children.Add(Theme.Text("Reorder, rotate, duplicate, remove, or combine PDFs. Save creates a new document.", 12, Theme.Muted)); header.Children.Add(title);
            save = Theme.Button("Save a copy", true); save.IsEnabled = false; save.VerticalAlignment = VerticalAlignment.Top; save.Click += Save; Grid.SetColumn(save, 1); header.Children.Add(save); root.Children.Add(header);
            var body = new Grid(); body.ColumnDefinitions.Add(new ColumnDefinition()); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(215) }); Grid.SetRow(body, 1); root.Children.Add(body);
            var factory = new FrameworkElementFactory(typeof(WrapPanel)); list.ItemsPanel = new ItemsPanelTemplate(factory); ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
            body.Children.Add(new Border { Background = Theme.Brush("#E8EBE6"), CornerRadius = new CornerRadius(14), Padding = new Thickness(10), Margin = new Thickness(0, 0, 15, 0), Child = list });
            var controls = new StackPanel { Margin = new Thickness(17) };
            Button(controls, "Add another PDF…", async delegate { var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "PDF documents|*.pdf", Multiselect = true }; if (dialog.ShowDialog(this) == true) await Load(dialog.FileNames); });
            Button(controls, "Move earlier ↑", delegate { Move(-1); }); Button(controls, "Move later ↓", delegate { Move(1); });
            Button(controls, "Rotate page ↻", delegate { int i = list.SelectedIndex; if (i < 0 || busy) return; pages[i].rotation = (pages[i].rotation + 90) % 360; Refresh(i); });
            Button(controls, "Duplicate page", delegate { int i = list.SelectedIndex; if (i < 0 || busy) return; pages.Insert(i + 1, pages[i].Clone()); Refresh(i + 1); });
            Button(controls, "Remove page", delegate { int i = list.SelectedIndex; if (i < 0 || busy) return; if (pages.Count <= 1) { status.Text = "Keep at least one page."; return; } pages.RemoveAt(i); Refresh(Math.Min(i, pages.Count - 1)); });
            controls.Children.Add(strip); controls.Children.Add(new TextBlock { Text = "The original PDFs stay untouched. Digital signatures are not retained in the edited document.", TextWrapping = TextWrapping.Wrap, FontSize = 11, Foreground = Theme.Muted, Margin = new Thickness(0, 20, 0, 0) });
            var card = Theme.Card(controls); Grid.SetColumn(card, 1); body.Children.Add(card); Grid.SetRow(status, 2); status.VerticalAlignment = VerticalAlignment.Center; root.Children.Add(status);
            Loaded += async delegate { await Load(files); };
            Closed += delegate { cancellation.Cancel(); cancellation.Dispose(); TryCleanup(); };
            Ui.Attach(this);
        }
        void Button(Panel panel, string title, Action action) { var b = Theme.Button(title, false); b.Margin = new Thickness(0, 0, 0, 10); b.HorizontalContentAlignment = HorizontalAlignment.Left; b.Click += delegate { action(); }; panel.Children.Add(b); }
        async Task Load(string[] files)
        {
            if (busy) return; busy = true; save.IsEnabled = false; status.Text = "Rendering page previews…"; var ct = cancellation.Token;
            string sub = Path.Combine(temp, Guid.NewGuid().ToString("N"));
            try {
                List<PdfPage> incoming = await Task.Run(() => PdfWorker.Inspect(files, sub, ct));
                if (ct.IsCancellationRequested) return;
                if (pages.Count + incoming.Count > 300) throw new InvalidOperationException("Keep this editing session under 300 pages.");
                pages.AddRange(incoming); Refresh(0);
            } catch (OperationCanceledException) { TryCleanup(); }
            catch (Exception e) { status.Text = e.Message; }
            finally { busy = false; if (!ct.IsCancellationRequested) save.IsEnabled = pages.Count > 0; }
        }
        void Move(int offset) { int i = list.SelectedIndex, j = i + offset; if (busy || i < 0 || j < 0 || j >= pages.Count) return; var page = pages[i]; pages.RemoveAt(i); pages.Insert(j, page); Refresh(j); }
        void Refresh(int selected)
        {
            list.Items.Clear();
            for (int i = 0; i < pages.Count; i++) {
                var p = pages[i]; var stack = new StackPanel { Width = 160, Margin = new Thickness(9) };
                var image = new Image { Source = ImageEdits.Load(p.thumbnail), Width = 146, Height = 195, Stretch = Stretch.Uniform, LayoutTransform = new RotateTransform(p.rotation) };
                stack.Children.Add(new Border { Background = Brushes.White, Padding = new Thickness(6), Child = image });
                stack.Children.Add(Theme.Text((i + 1) + "  ·  " + Path.GetFileName(p.file) + " / " + (p.index + 1), 10, Theme.Muted)); list.Items.Add(new ListBoxItem { Content = stack, Padding = new Thickness(0), Margin = new Thickness(0) });
            }
            list.SelectedIndex = Math.Min(selected, pages.Count - 1); status.Text = pages.Count + " pages · Select a page to change it";
            Ui.Apply(this);
        }
        async void Save(object sender, RoutedEventArgs args)
        {
            if (busy) return; busy = true; save.IsEnabled = false; status.Text = "Saving PDF…";
            var ct = cancellation.Token; var copy = pages.Select(p => p.Clone()).ToList(); bool remove = strip.IsChecked == true;
            try { Output = await Task.Run(() => PdfWorker.Save(sources, copy, remove, "save", null, ct)); if (!ct.IsCancellationRequested) DialogResult = true; }
            catch (OperationCanceledException) { TryCleanup(); }
            catch (Exception e) { status.Text = e.Message; }
            finally { busy = false; if (!ct.IsCancellationRequested) save.IsEnabled = true; }
        }
        void TryCleanup() { try { if (Directory.Exists(temp)) Directory.Delete(temp, true); } catch { } }
    }
}
