using System;
using System.Collections.Generic;
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

namespace TangerineWindows
{
    public sealed class VideoJoinOptions
    {
        public int MaxDimension = 1920;
        public bool KeepAudio = true;
    }

    public static class VideoJoin
    {
        static string N(double value) { return value.ToString("0.######", CultureInfo.InvariantCulture); }
        public static void Validate(string[] files, VideoJoinOptions options) {
            if (files == null || files.Length < 2 || files.Length > 50) throw new ArgumentException(Ui.T("Choose between 2 and 50 videos."));
            if (options == null || !new[] { 1280, 1920, 3840 }.Contains(options.MaxDimension)) throw new ArgumentException("Choose a valid output size.");
            foreach (string file in files) if (Catalog.Kind(file) != "video" || !File.Exists(file)) throw new ArgumentException(Ui.T("Choose existing video files."));
        }
        public static int[] Canvas(int width, int height, int maximum) {
            if (width < 2 || height < 2 || !new[] { 1280, 1920, 3840 }.Contains(maximum)) throw new ArgumentException("Invalid video dimensions.");
            double factor = Math.Min(1, (double)maximum / Math.Max(width, height));
            return new[] { Math.Max(2, (int)(width * factor) / 2 * 2), Math.Max(2, (int)(height * factor) / 2 * 2) };
        }
        public static string Save(string[] files, VideoJoinOptions options, CancellationToken ct, Action<string> progress) {
            ct.ThrowIfCancellationRequested(); Validate(files, options);
            // Snapshot caller-owned data before any long-running work.
            files = files.Select(Path.GetFullPath).ToArray(); bool audio = options.KeepAudio; int maximum = options.MaxDimension;
            var durations = new double[files.Length]; var hasAudio = new bool[files.Length];
            for (int i = 0; i < files.Length; i++) {
                ct.ThrowIfCancellationRequested(); string details = VideoFrames.Describe(files[i], ct);
                if (details.Contains("smpte2084") || details.Contains("arib-std-b67")) throw new InvalidOperationException(Ui.T("HDR video joining is not supported yet."));
                double duration = VideoFrames.Duration(files[i], ct);
                if (double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 0 || duration > 86400) throw new InvalidOperationException(Ui.T("Each video needs a known duration under 24 hours."));
                durations[i] = Math.Ceiling(duration * 30) / 30;
                hasAudio[i] = Regex.IsMatch(details, @"(?m)^\s*Stream #\d+:\d+[^\r\n]*:\s*Audio:");
            }
            var first = VideoFrames.Read(files[0], 0, ct); int[] size = Canvas(first.PixelWidth, first.PixelHeight, maximum);
            string parent = Path.GetDirectoryName(files[0]), stage = Path.Combine(parent, ".madardrop-join-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            try {
                double total = durations.Sum(), done = 0;
                for (int i = 0; i < files.Length; i++) {
                    ct.ThrowIfCancellationRequested(); double before = done, length = durations[i];
                    var args = new List<string> { "-hide_banner", "-v", "error", "-nostdin", "-n", "-i", files[i] };
                    if (audio && !hasAudio[i]) args.AddRange(new[] { "-f", "lavfi", "-i", "anullsrc=r=48000:cl=stereo" });
                    args.AddRange(new[] { "-map", "0:v:0", "-vf", "setpts=PTS-STARTPTS,fps=30,scale=" + size[0] + ":" + size[1] + ":force_original_aspect_ratio=decrease:force_divisible_by=2,pad=" + size[0] + ":" + size[1] + ":(ow-iw)/2:(oh-ih)/2:black,setsar=1,tpad=stop_mode=clone:stop_duration=" + N(length), "-t", N(length), "-c:v", "libx264", "-preset", "fast", "-crf", "20", "-bf", "0", "-pix_fmt", "yuv420p" });
                    if (audio) args.AddRange(new[] { "-map", hasAudio[i] ? "0:a:0" : "1:a:0", "-af", "aresample=48000:async=1:first_pts=0,apad", "-ar", "48000", "-ac", "2", "-c:a", "pcm_s16le" });
                    else args.Add("-an");
                    args.AddRange(new[] { "-map_metadata", "-1", "-map_chapters", "-1", "-progress", "pipe:1", Path.Combine(stage, i.ToString("D3", CultureInfo.InvariantCulture) + ".mkv") });
                    Engine.Run("ffmpeg", args, ct, message => { var value = FloatingJob.Fraction(message, length); if (progress != null && value.HasValue) progress("Fraction " + N(.9 * (before + length * value.Value) / total)); });
                    done += length; if (progress != null) progress("Fraction " + N(.9 * done / total));
                }
                // Only generated ASCII basenames enter the concat script, never user paths.
                File.WriteAllLines(Path.Combine(stage, "clips.txt"), new[] { "ffconcat version 1.0" }.Concat(Enumerable.Range(0, files.Length).SelectMany(i => new[] { "file '" + i.ToString("D3", CultureInfo.InvariantCulture) + ".mkv'", "duration " + N(durations[i]) })), new UTF8Encoding(false));
                string temp = Path.Combine(stage, "joined.mp4");
                var join = new List<string> { "-hide_banner", "-v", "error", "-nostdin", "-n", "-f", "concat", "-safe", "1", "-i", Path.Combine(stage, "clips.txt"), "-map", "0:v:0", "-c:v", "copy" };
                if (audio) join.AddRange(new[] { "-map", "0:a:0", "-c:a", "aac", "-b:a", "192k" }); else join.Add("-an");
                join.AddRange(new[] { "-map_metadata", "-1", "-map_chapters", "-1", "-movflags", "+faststart", "-progress", "pipe:1", temp });
                Engine.Run("ffmpeg", join, ct, message => { var value = FloatingJob.Fraction(message, total); if (progress != null && value.HasValue) progress("Fraction " + N(.9 + .1 * value.Value)); });
                ct.ThrowIfCancellationRequested();
                double actual = File.Exists(temp) && new FileInfo(temp).Length > 0 ? VideoFrames.Duration(temp, ct) : 0;
                if (actual <= 0 || Math.Abs(actual - total) > Math.Max(.25, files.Length / 30.0 + .1)) throw new IOException(Ui.T("The joined video could not be saved."));
                ct.ThrowIfCancellationRequested(); return Engine.Publish(temp, files[0], ".mp4", "joined");
            } finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
        }
    }

    public sealed class VideoJoinWindow : Window
    {
        public string[] Files;
        public VideoJoinOptions Value;
        readonly List<string> paths;
        readonly ListBox list = new ListBox { Height = 168, Background = Brushes.Transparent, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        readonly Image preview = new Image { Stretch = Stretch.Uniform };
        readonly TextBlock status = Theme.Text("", 11, Theme.Muted), error = Theme.Text("", 11, Theme.Muted);
        readonly ComboBox output = new ComboBox { MinWidth = 160 };
        readonly CheckBox audio = new CheckBox { Content = "Keep sound", IsChecked = true, Margin = new Thickness(0, 10, 0, 0) };
        readonly Button apply = Theme.Button("Join videos", true);
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        CancellationTokenSource request;
        bool closed;
        public VideoJoinWindow(string[] files) {
            paths = new List<string>(files); Title = "Join Videos"; Width = 470; Height = 730; MinWidth = 400; MinHeight = 650; WindowStartupLocation = WindowStartupLocation.CenterOwner; FontFamily = new FontFamily("Segoe UI");
            Resources.Add(typeof(Button), MainWindow.ButtonStyle());
            var body = new StackPanel { Margin = new Thickness(16, 8, 16, 12) };
            body.Children.Add(Theme.Text("Clips play in the order below", 12, Theme.Ink, FontWeights.SemiBold)); list.Margin = new Thickness(0, 8, 0, 6); body.Children.Add(list);
            var buttons = new WrapPanel(); buttons.Children.Add(Small("Move up", () => Move(-1))); buttons.Children.Add(Small("Move down", () => Move(1))); buttons.Children.Add(Small("Remove", Remove)); buttons.Children.Add(Small("Add videos…", Add)); body.Children.Add(buttons);
            var canvas = new Border { Height = 165, Background = Brushes.Black, Child = preview, Margin = new Thickness(0, 10, 0, 6) }; Ui.Preserve(canvas); body.Children.Add(canvas); body.Children.Add(status);
            body.Children.Add(Theme.Text("Output size", 11, Theme.Ink));
            foreach (string label in new[] { "Up to 720p", "Up to 1080p", "Up to 4K" }) output.Items.Add(label); output.SelectedIndex = 1; output.Margin = new Thickness(0, 4, 0, 0); body.Children.Add(output); body.Children.Add(audio);
            var note = Theme.Text("MP4 · 30 fps · Fit to the first clip with black bars\nOne sound track per clip; silent clips keep their timing.\nOriginal files stay unchanged.", 11, Theme.Muted); note.Margin = new Thickness(0, 10, 0, 7); body.Children.Add(note); body.Children.Add(error);
            var scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var footer = new Grid(); var cancel = Theme.Button("Cancel", false); cancel.HorizontalAlignment = HorizontalAlignment.Left; cancel.Click += delegate { Close(); }; footer.Children.Add(cancel); apply.HorizontalAlignment = HorizontalAlignment.Right; footer.Children.Add(apply);
            ToolFrame.Build(this, "Join Videos", scroll, footer);
            list.SelectionChanged += async delegate { await LoadPreview(); };
            apply.Click += delegate { try { var options = new VideoJoinOptions { MaxDimension = new[] { 1280, 1920, 3840 }[output.SelectedIndex], KeepAudio = audio.IsChecked == true }; VideoJoin.Validate(paths.ToArray(), options); Files = paths.ToArray(); Value = options; DialogResult = true; } catch (Exception e) { error.Text = e.Message; } };
            Loaded += delegate { Refresh(0); };
            PreviewKeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
            Closed += delegate { closed = true; lifetime.Cancel(); if (request != null) request.Cancel(); lifetime.Dispose(); };
        }
        Button Small(string label, Action action) { var button = Theme.Button(label, false); button.Margin = new Thickness(0, 0, 5, 0); button.Padding = new Thickness(7, 4, 7, 4); button.MinHeight = 26; button.FontSize = 11; button.Click += delegate { action(); }; return button; }
        void Refresh(int index) {
            list.Items.Clear(); for (int i = 0; i < paths.Count; i++) { var row = new TextBlock { Text = (i + 1) + ". " + Path.GetFileName(paths[i]), ToolTip = paths[i], TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(4), Foreground = Theme.Ink }; Ui.Preserve(row); list.Items.Add(row); }
            apply.IsEnabled = paths.Count >= 2 && paths.Count <= 50; list.SelectedIndex = paths.Count == 0 ? -1 : Math.Max(0, Math.Min(paths.Count - 1, index));
            if (paths.Count == 0) { preview.Source = null; status.Text = ""; }
        }
        void Move(int delta) { int index = list.SelectedIndex, next = index + delta; if (index < 0 || next < 0 || next >= paths.Count) return; string path = paths[index]; paths.RemoveAt(index); paths.Insert(next, path); Refresh(next); }
        void Remove() { int index = list.SelectedIndex; if (index < 0) return; paths.RemoveAt(index); Refresh(index); }
        void Add() {
            var dialog = new Microsoft.Win32.OpenFileDialog { Multiselect = true, CheckFileExists = true, Title = Ui.T("Add videos…"), Filter = "Video|*.mp4;*.mov;*.mkv;*.webm;*.avi;*.wmv;*.m4v" };
            if (dialog.ShowDialog(this) != true) return;
            if (paths.Count + dialog.FileNames.Length > 50) { error.Text = Ui.T("Choose between 2 and 50 videos."); return; }
            paths.AddRange(dialog.FileNames); error.Text = ""; Refresh(paths.Count - 1);
        }
        async Task LoadPreview() {
            if (request != null) request.Cancel(); int index = list.SelectedIndex; preview.Source = null;
            if (closed || index < 0 || index >= paths.Count) return;
            string path = paths[index]; var current = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); request = current; status.Text = Ui.T("Loading preview…");
            try { var frame = await Task.Run(() => VideoFrames.Read(path, 0, current.Token)); if (!closed && request == current) { preview.Source = frame; status.Text = Path.GetFileName(path); } }
            catch (OperationCanceledException) { }
            catch (Exception e) { if (!closed && request == current) status.Text = e.Message; }
            finally { if (request == current) request = null; current.Dispose(); }
        }
    }
}
