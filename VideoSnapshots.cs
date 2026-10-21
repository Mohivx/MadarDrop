using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
    public static class VideoSnapshots
    {
        public static string Save(string source, double[] times, CancellationToken ct, Action<string> progress) {
            ct.ThrowIfCancellationRequested();
            if (Catalog.Kind(source) != "video" || !File.Exists(source)) throw new ArgumentException("Choose a video first.");
            if (times == null || times.Length == 0 || times.Length > 50 || times.Any(t => double.IsNaN(t) || double.IsInfinity(t) || t < 0)) throw new ArgumentException("Choose between 1 and 50 valid frames.");
            double duration = VideoFrames.Duration(source, ct);
            if (duration > 0 && times.Any(t => t >= duration)) throw new ArgumentException("Choose frames inside the recording.");
            string folder = Path.GetDirectoryName(Path.GetFullPath(source)), stage = Path.Combine(folder, ".madardrop-frames-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            try {
                for (int i = 0; i < times.Length; i++) {
                    ct.ThrowIfCancellationRequested(); var frame = VideoFrames.Read(source, times[i], ct);
                    if ((long)frame.PixelWidth * frame.PixelHeight > 64000000) throw new InvalidOperationException("Keep each frame under 64 megapixels.");
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(frame));
                    using (var stream = new FileStream(Path.Combine(stage, "Frame " + (i + 1).ToString("D3") + ".png"), FileMode.CreateNew)) encoder.Save(stream);
                    if (progress != null) progress("Page " + (i + 1) + " of " + times.Length);
                }
                for (int n = 1; n <= 10000; n++) {
                    ct.ThrowIfCancellationRequested(); string target = Path.Combine(folder, Path.GetFileNameWithoutExtension(source) + " (frames" + (n == 1 ? "" : " " + n) + ")");
                    try { Directory.Move(stage, target); return target; } catch (IOException) { if (!Directory.Exists(target) && !File.Exists(target)) throw; }
                }
                throw new IOException("Could not find an unused output folder.");
            } finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
        }
    }

    public sealed class VideoSnapshotWindow : Window
    {
        public string Output;
        public int SelectedCount { get { return selected.Count; } }
        public double Position { get { return timeline.Value; } }
        readonly string source;
        readonly Image preview = new Image { Stretch = Stretch.Uniform };
        readonly Slider timeline = new Slider { Minimum = 0, Maximum = 1, IsEnabled = false, Margin = new Thickness(0, 10, 0, 6) };
        readonly TextBlock clock = Theme.Text("", 10, Theme.Muted), count = Theme.Text("", 11, Theme.Muted), error = Theme.Text("", 11, Theme.Muted);
        readonly WrapPanel strip = new WrapPanel();
        readonly Button add = Theme.Button("Add frame", false), save = Theme.Button("Save frames", true);
        readonly List<KeyValuePair<double, BitmapSource>> selected = new List<KeyValuePair<double, BitmapSource>>();
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        readonly DispatcherTimer seek = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        CancellationTokenSource request;
        double duration, fps = 30, shownAt;
        bool closed, updating, busy;
        public VideoSnapshotWindow(string path, BitmapSource initial = null, double seconds = 0, double frameRate = 30) {
            source = path; Title = "Video Snapshots"; Width = 420; Height = 700; MinWidth = 350; MinHeight = 530; WindowStartupLocation = WindowStartupLocation.CenterOwner; FontFamily = new FontFamily("Segoe UI");
            Resources.Add(typeof(Button), MainWindow.ButtonStyle());
            var body = new Grid { Margin = new Thickness(15, 7, 15, 8) }; body.RowDefinitions.Add(new RowDefinition()); body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            body.Children.Add(new Border { Background = Brushes.Black, CornerRadius = new CornerRadius(11), Child = preview });
            var controls = new StackPanel(); Grid.SetRow(controls, 1); body.Children.Add(controls); controls.Children.Add(timeline);
            var scrub = new Grid(); scrub.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); scrub.ColumnDefinitions.Add(new ColumnDefinition()); scrub.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            scrub.Children.Add(Small("◁", delegate { Step(-1); })); clock.HorizontalAlignment = HorizontalAlignment.Center; clock.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(clock, 1); scrub.Children.Add(clock);
            var next = Small("▷", delegate { Step(1); }); Grid.SetColumn(next, 2); scrub.Children.Add(next); controls.Children.Add(scrub);
            var selection = new Grid { Margin = new Thickness(0, 9, 0, 7) }; selection.ColumnDefinitions.Add(new ColumnDefinition()); selection.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); selection.Children.Add(count); count.VerticalAlignment = VerticalAlignment.Center; count.HorizontalAlignment = HorizontalAlignment.Left; count.Margin = new Thickness(0, 0, 10, 0);
            add.HorizontalAlignment = HorizontalAlignment.Right; add.Padding = new Thickness(9, 4, 9, 4); add.MinHeight = 25; add.FontSize = 11; add.IsEnabled = false; add.Click += delegate { AddCurrent(); }; Grid.SetColumn(add, 1); selection.Children.Add(add); controls.Children.Add(selection);
            controls.Children.Add(new ScrollViewer { Content = strip, Height = 90, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); error.TextWrapping = TextWrapping.Wrap; error.MaxHeight = 45; controls.Children.Add(error);
            var footer = new Grid(); save.HorizontalAlignment = HorizontalAlignment.Right; save.Padding = new Thickness(12, 5, 12, 5); save.MinHeight = 28; save.IsEnabled = false; save.Click += Save; footer.Children.Add(save);
            ToolFrame.Build(this, "Video Snapshots", body, footer);
            timeline.ValueChanged += delegate { if (!updating && !closed) { Clock(); add.IsEnabled = false; seek.Stop(); seek.Start(); } };
            seek.Tick += async delegate { seek.Stop(); await LoadFrame(); };
            if (initial != null) { duration = seconds; fps = frameRate > 0 && !double.IsInfinity(frameRate) ? frameRate : 30; SetFrame(initial, 0); AddCurrent(); }
            else Loaded += async delegate { try { var ct = lifetime.Token; duration = await Task.Run(() => VideoFrames.Duration(source, ct)); fps = await Task.Run(() => VideoFrames.FrameRate(source, ct)); if (!closed) { await LoadFrame(); if (!closed && preview.Source != null) AddCurrent(); } } catch (OperationCanceledException) { } catch (Exception e) { if (!closed) error.Text = e.Message; } };
            PreviewKeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
            Closed += delegate { closed = true; seek.Stop(); lifetime.Cancel(); if (request != null) request.Cancel(); lifetime.Dispose(); };
            Refresh();
        }
        Button Small(string text, Action action) { var b = Theme.Button(text, false); b.Padding = new Thickness(8, 3, 8, 3); b.MinHeight = 25; b.ToolTip = Ui.T(text == "◁" ? "Previous frame" : "Next frame"); b.Click += delegate { action(); }; return b; }
        static string Stamp(double value) { var stamp = TimeSpan.FromSeconds(Math.Max(0, value)); return ((long)stamp.TotalHours).ToString("D2", CultureInfo.InvariantCulture) + stamp.ToString(@"\:mm\:ss\.fff"); }
        void Clock() { clock.Text = Stamp(timeline.Value) + " / " + Stamp(duration); }
        public void Step(int direction) { if (busy || closed) return; timeline.Value = Math.Max(0, Math.Min(timeline.Maximum, timeline.Value + (direction < 0 ? -1 : 1) / fps)); }
        void SetFrame(BitmapSource frame, double at) {
            preview.Source = frame; shownAt = at; updating = true; timeline.Maximum = Math.Max(0, duration - .000001); timeline.IsEnabled = duration > 0 && !busy; updating = false; add.IsEnabled = !busy && selected.Count < 50; Clock();
        }
        public void AddCurrent() {
            var frame = preview.Source as BitmapSource; if (frame == null || busy || selected.Count >= 50 || Math.Abs(shownAt - Position) > .0001 || selected.Any(s => Math.Abs(s.Key - shownAt) < .0001)) return;
            double scale = Math.Min(1, 120.0 / Math.Max(frame.PixelWidth, frame.PixelHeight)); var thumb = new TransformedBitmap(frame, new ScaleTransform(scale, scale)); thumb.Freeze();
            var pixels = new byte[thumb.PixelWidth * thumb.PixelHeight * 4]; new FormatConvertedBitmap(thumb, PixelFormats.Bgra32, null, 0).CopyPixels(pixels, thumb.PixelWidth * 4, 0);
            var small = BitmapSource.Create(thumb.PixelWidth, thumb.PixelHeight, 96, 96, PixelFormats.Bgra32, null, pixels, thumb.PixelWidth * 4); small.Freeze();
            selected.Add(new KeyValuePair<double, BitmapSource>(shownAt, small)); Refresh();
        }
        void Refresh() {
            strip.Children.Clear(); foreach (var item in selected.ToArray()) {
                var card = new Grid { Width = 78, Height = 72, Margin = new Thickness(0, 0, 7, 5) }; card.Children.Add(new Image { Source = item.Value, Stretch = Stretch.UniformToFill });
                var label = new TextBlock { Text = Stamp(item.Key), Foreground = Brushes.White, Background = Theme.Brush("#BF000000"), FontSize = 9, VerticalAlignment = VerticalAlignment.Bottom }; card.Children.Add(label);
                var remove = Theme.Button("×", false); remove.Width = remove.Height = 19; remove.MinHeight = 19; remove.Padding = new Thickness(0); remove.Margin = new Thickness(0); remove.HorizontalAlignment = HorizontalAlignment.Right; remove.VerticalAlignment = VerticalAlignment.Top; remove.IsEnabled = !busy; remove.Click += delegate { selected.Remove(item); Refresh(); }; card.Children.Add(remove); strip.Children.Add(card);
            }
            count.Text = Ui.Arabic ? "اللقطات: " + selected.Count : selected.Count + " selected"; save.Content = Ui.T("Save frames") + " (" + selected.Count + ")"; save.IsEnabled = selected.Count > 0 && !busy; add.IsEnabled = preview.Source != null && selected.Count < 50 && !busy && Math.Abs(shownAt - Position) < .0001;
        }
        async Task LoadFrame() {
            if (closed || busy) return; if (request != null) request.Cancel();
            var current = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); request = current; double at = timeline.Value; error.Text = Ui.T("Loading preview…");
            try { var frame = await Task.Run(() => VideoFrames.Read(source, at, current.Token)); if (!closed && request == current) { SetFrame(frame, at); error.Text = ""; } }
            catch (OperationCanceledException) { }
            catch (Exception e) { if (!closed && request == current) error.Text = e.Message; }
            finally { if (request == current) request = null; current.Dispose(); }
        }
        async void Save(object sender, RoutedEventArgs e) {
            if (busy || selected.Count == 0) return; busy = true; seek.Stop(); if (request != null) request.Cancel(); timeline.IsEnabled = false; Refresh(); var ct = lifetime.Token; var times = selected.Select(s => s.Key).ToArray();
            try { Output = await Task.Run(() => VideoSnapshots.Save(source, times, ct, message => Dispatcher.BeginInvoke(new Action(delegate { if (!closed) error.Text = Ui.T("Saving frames…") + " · " + (Ui.Arabic ? message.Replace("Page ", "لقطة ").Replace(" of ", " من ") : message.Replace("Page ", "Frame ")); })))); if (!closed) DialogResult = true; }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!closed) error.Text = ex.Message; }
            finally { busy = false; if (!closed) { timeline.IsEnabled = duration > 0; Refresh(); } }
        }
    }
}
