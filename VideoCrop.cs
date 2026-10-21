using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace TangerineWindows
{
    public static class VideoFrames
    {
        static string Probe(string file, CancellationToken ct, bool frame) {
            var args = frame ? new[] { "-hide_banner", "-nostdin", "-i", file, "-map", "0:v:0", "-vf", "showinfo", "-frames:v", "1", "-f", "null", "-" } : new[] { "-hide_banner", "-nostdin", "-i", file };
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct)) {
                timeout.CancelAfter(30000);
                using (var process = new Process { StartInfo = new ProcessStartInfo(Engine.Tool("ffmpeg"), string.Join(" ", args.Select(Engine.Quote))) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true, StandardErrorEncoding = Encoding.UTF8 } }) {
                    timeout.Token.ThrowIfCancellationRequested(); process.Start();
                    var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
                    using (timeout.Token.Register(delegate { try { if (!process.HasExited) process.Kill(); } catch { } })) process.WaitForExit();
                    timeout.Token.ThrowIfCancellationRequested(); output.Wait(); string text = errors.Result;
                    return text;
                }
            }
        }
        public static double Duration(string file, CancellationToken ct) {
            var match = Regex.Match(Probe(file, ct, false), @"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)");
            return match.Success ? double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) * 3600 + double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) * 60 + double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture) : 0;
        }
        public static double FrameRate(string file, CancellationToken ct) {
            var match = Regex.Match(Probe(file, ct, true), @"frame_rate:\s*(\d+)/(\d+)");
            if (match.Success) { double numerator = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), denominator = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture); if (numerator > 0 && denominator > 0) return numerator / denominator; }
            return 30;
        }
        public static BitmapSource Read(string file, double seconds, CancellationToken ct) {
            string temp = Path.Combine(Path.GetTempPath(), "madardrop-frame-" + Guid.NewGuid().ToString("N") + ".png");
            try {
                Engine.Run("ffmpeg", new[] { "-hide_banner", "-v", "error", "-nostdin", "-n", "-ss", Math.Max(0, seconds).ToString("0.######", CultureInfo.InvariantCulture), "-i", file, "-map", "0:v:0", "-frames:v", "1", "-update", "1", temp }, ct, null);
                ct.ThrowIfCancellationRequested(); if (!File.Exists(temp)) throw new IOException("Could not read this video frame."); return ImageEdits.Load(temp);
            } finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public static Parameters Crop(Rect selection, int width, int height) {
            if (width < 2 || height < 2 || selection.Width < 2 || selection.Height < 2) throw new ArgumentException("Choose a crop of at least 2 × 2 pixels.");
            int x = Math.Max(0, Math.Min(width - 2, (int)Math.Floor(selection.X))) / 2 * 2;
            int y = Math.Max(0, Math.Min(height - 2, (int)Math.Floor(selection.Y))) / 2 * 2;
            int w = Math.Min(width - x, (int)Math.Floor(selection.Width)) / 2 * 2, h = Math.Min(height - y, (int)Math.Floor(selection.Height)) / 2 * 2;
            if (w < 2 || h < 2) throw new ArgumentException("Choose a larger crop.");
            return new Parameters { X = x, Y = y, CropWidth = w, CropHeight = h };
        }
    }
    public sealed class VideoCropWindow : Window
    {
        public Parameters Value;
        public CropSurface Surface { get; private set; }
        readonly string file;
        readonly Grid preview = new Grid { Background = Brushes.Black, Margin = new Thickness(0, 0, 0, 8) };
        readonly TextBlock result = Theme.Text("Loading preview…", 11, Theme.Muted), time = Theme.Text("", 11, Theme.Muted), error = Theme.Text("", 11, Theme.Muted);
        readonly Slider timeline = new Slider { Minimum = 0, Maximum = 1, IsEnabled = false }, width = new Slider { Minimum = 1, Maximum = 100, Value = 100 }, height = new Slider { Minimum = 1, Maximum = 100, Value = 100 };
        readonly ComboBox ratios = new ComboBox { MinWidth = 110, Width = 130 };
        readonly Button apply = Theme.Button("Apply", true);
        readonly DispatcherTimer seek = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        CancellationTokenSource frameRequest;
        double duration;
        bool updating, closed;
        public VideoCropWindow(string path, BitmapSource initial = null, double seconds = 0) {
            file = path; Title = "Crop Video"; Width = 420; Height = 710; MinWidth = 370; MinHeight = 560; WindowStartupLocation = WindowStartupLocation.CenterOwner; FontFamily = new FontFamily("Segoe UI");
            Resources.Add(typeof(Button), MainWindow.ButtonStyle());
            var body = new Grid { Margin = new Thickness(16, 6, 16, 0) }; body.RowDefinitions.Add(new RowDefinition()); body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); body.Children.Add(preview);
            var controls = new StackPanel { Margin = new Thickness(0, 4, 0, 8) }; Grid.SetRow(controls, 1); body.Children.Add(controls);
            var ratioRow = new Grid(); ratioRow.ColumnDefinitions.Add(new ColumnDefinition()); ratioRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            foreach (string label in new[] { "Free", "1:1", "16:9", "9:16", "4:3", "3:4" }) ratios.Items.Add(label); ratios.SelectedIndex = 0; ratios.HorizontalAlignment = HorizontalAlignment.Left; ratioRow.Children.Add(ratios);
            result.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(result, 1); ratioRow.Children.Add(result); controls.Children.Add(ratioRow);
            AddSlider(controls, "Width", width); AddSlider(controls, "Height", height);
            timeline.Margin = new Thickness(0, 12, 0, 6); controls.Children.Add(timeline);
            var scrub = new Grid(); scrub.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); scrub.ColumnDefinitions.Add(new ColumnDefinition()); scrub.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var back = Small("−1s", delegate { timeline.Value = Math.Max(0, timeline.Value - 1); }); scrub.Children.Add(back); time.HorizontalAlignment = HorizontalAlignment.Center; time.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(time, 1); scrub.Children.Add(time);
            var next = Small("+1s", delegate { timeline.Value = Math.Min(timeline.Maximum, timeline.Value + 1); }); Grid.SetColumn(next, 2); scrub.Children.Add(next); controls.Children.Add(scrub); controls.Children.Add(error);
            var footer = new Grid(); var reset = Small("Reset", delegate { if (Surface != null) { ratios.SelectedIndex = 0; Surface.Reset(); } }); reset.HorizontalAlignment = HorizontalAlignment.Left; footer.Children.Add(reset);
            apply.HorizontalAlignment = HorizontalAlignment.Right; apply.Padding = new Thickness(18, 5, 18, 5); apply.MinHeight = 28; apply.IsEnabled = false; footer.Children.Add(apply);
            apply.Click += delegate { try { Value = VideoFrames.Crop(Surface.Selection, Surface.Source.PixelWidth, Surface.Source.PixelHeight); DialogResult = true; } catch (Exception e) { error.Text = e.Message; } };
            ratios.SelectionChanged += delegate { if (Surface != null) Surface.SetAspect(new[] { 0, 1, 16.0 / 9, 9.0 / 16, 4.0 / 3, 3.0 / 4 }[ratios.SelectedIndex]); };
            width.ValueChanged += delegate { if (!updating && Surface != null) Surface.SetDimensions(Surface.Source.PixelWidth * width.Value / 100, Surface.Selection.Height, true); };
            height.ValueChanged += delegate { if (!updating && Surface != null) Surface.SetDimensions(Surface.Selection.Width, Surface.Source.PixelHeight * height.Value / 100, false); };
            timeline.ValueChanged += delegate { if (updating || Surface == null) return; UpdateTime(); seek.Stop(); seek.Start(); };
            seek.Tick += async delegate { seek.Stop(); await LoadFrame(); };
            ToolFrame.Build(this, "Crop Video", body, footer);
            if (initial != null) { duration = seconds; SetFrame(initial); }
            else Loaded += async delegate { try { duration = await Task.Run(() => VideoFrames.Duration(file, lifetime.Token)); if (!closed) await LoadFrame(); } catch (OperationCanceledException) { } catch (Exception e) { if (!closed) error.Text = e.Message; } };
            PreviewKeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
            Closed += delegate { closed = true; seek.Stop(); lifetime.Cancel(); if (frameRequest != null) frameRequest.Cancel(); lifetime.Dispose(); };
        }
        Button Small(string text, Action action) { var b = Theme.Button(text, false); b.Padding = new Thickness(7, 4, 7, 4); b.MinHeight = 25; b.FontSize = 11; b.Click += delegate { action(); }; return b; }
        static void AddSlider(Panel panel, string title, Slider slider) { var label = Theme.Text(title, 11, Theme.Ink); label.Margin = new Thickness(0, 10, 0, 4); panel.Children.Add(label); panel.Children.Add(slider); }
        void UpdateTime() { time.Text = TimeSpan.FromSeconds(timeline.Value).ToString(@"hh\:mm\:ss") + " / " + TimeSpan.FromSeconds(duration).ToString(@"hh\:mm\:ss"); }
        void SetFrame(BitmapSource frame) {
            if (Surface == null) { Surface = new CropSurface(frame); Surface.Changed = Fields; preview.Children.Add(Surface); }
            else Surface.ReplaceFrame(frame);
            updating = true; timeline.Maximum = Math.Max(.001, duration - .05); timeline.IsEnabled = duration > 0; updating = false; apply.IsEnabled = true; Fields(); UpdateTime();
        }
        void Fields() {
            if (Surface == null) return; updating = true;
            width.Value = Surface.Selection.Width * 100 / Surface.Source.PixelWidth; height.Value = Surface.Selection.Height * 100 / Surface.Source.PixelHeight;
            try { var p = VideoFrames.Crop(Surface.Selection, Surface.Source.PixelWidth, Surface.Source.PixelHeight); result.Text = p.CropWidth + " × " + p.CropHeight + " px"; } catch { result.Text = "—"; }
            updating = false;
        }
        async Task LoadFrame() {
            if (closed) return; if (frameRequest != null) frameRequest.Cancel();
            var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); frameRequest = request; double at = timeline.Value; apply.IsEnabled = false; error.Text = Ui.T("Loading preview…");
            try { var frame = await Task.Run(() => VideoFrames.Read(file, at, request.Token)); if (!closed && frameRequest == request) { SetFrame(frame); error.Text = ""; } }
            catch (OperationCanceledException) { }
            catch (Exception e) { if (!closed && frameRequest == request) { error.Text = e.Message; apply.IsEnabled = Surface != null; } }
            finally { if (frameRequest == request) frameRequest = null; request.Dispose(); }
        }
    }
}
