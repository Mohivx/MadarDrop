using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
    public sealed class TimeRangeBar : FrameworkElement
    {
        public double Duration, Start, End;
        public Action Changed;
        int handle; double origin, beforeStart, beforeEnd;
        public TimeRangeBar() {
            Height = 25; MinWidth = 100; ToolTip = Ui.T("Drag the ends to adjust time");
            MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { double x = e.GetPosition(this).X, left = Start / Duration * ActualWidth, right = End / Duration * ActualWidth; handle = Math.Abs(x - left) < 8 ? 1 : Math.Abs(x - right) < 8 ? 2 : x >= left && x <= right ? 3 : 0; if (handle == 0 || Duration <= 0) return; origin = x; beforeStart = Start; beforeEnd = End; CaptureMouse(); e.Handled = true; };
            MouseMove += delegate(object s, MouseEventArgs e) { if (!IsMouseCaptured || ActualWidth <= 0) return; Adjust(handle, beforeStart, beforeEnd, (e.GetPosition(this).X - origin) / ActualWidth * Duration); e.Handled = true; };
            MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e) { if (IsMouseCaptured) { ReleaseMouseCapture(); e.Handled = true; } };
        }
        public void Adjust(int which, double start, double end, double delta) {
            if (Duration < .04) return;
            if (which == 1) { Start = Math.Max(0, Math.Min(end - .04, start + delta)); End = end; }
            else if (which == 2) { Start = start; End = Math.Min(Duration, Math.Max(start + .04, end + delta)); }
            else { double shift = Math.Max(-start, Math.Min(Duration - end, delta)); Start = start + shift; End = end + shift; }
            InvalidateVisual(); if (Changed != null) Changed();
        }
        protected override void OnRender(DrawingContext dc) {
            base.OnRender(dc); dc.DrawRoundedRectangle(Theme.Line, null, new Rect(0, 6, ActualWidth, 13), 4, 4); if (Duration <= 0) return;
            double left = Math.Max(0, Start / Duration * ActualWidth), right = Math.Min(ActualWidth, End / Duration * ActualWidth); dc.DrawRoundedRectangle(Theme.Accent, null, new Rect(left, 3, Math.Max(0, right - left), 19), 4, 4);
            foreach (double x in new[] { left + 3, right - 3 }) dc.DrawLine(new Pen(Brushes.White, 1.5), new Point(x, 7), new Point(x, 18));
        }
    }
    public sealed class VideoToolWindow : Window
    {
        public Parameters Value;
        public CropSurface Surface { get; private set; }
        public readonly List<VideoMask> Masks = new List<VideoMask>();
        readonly string source, operation;
        readonly Grid preview = new Grid { Background = Brushes.Black };
        readonly Slider timeline = new Slider { Minimum = 0, Maximum = 1, IsEnabled = false, Margin = new Thickness(0, 9, 0, 6) };
        readonly TextBlock clock = Theme.Text("", 10, Theme.Muted), error = Theme.Text("", 11, Theme.Muted);
        readonly TextBox start = Input(), end = Input(), points = Input(), parts = Input(), color = Input();
        readonly CheckBox equal = new CheckBox { Content = "Equal clips", IsChecked = true, VerticalAlignment = VerticalAlignment.Center };
        readonly ComboBox style = new ComboBox(), speed = new ComboBox();
        readonly Button chooseColor = Theme.Button("Color…", false);
        readonly Border swatch = new Border { Width = 17, Height = 13, CornerRadius = new CornerRadius(3), Background = Brushes.Black, Margin = new Thickness(0, 0, 7, 0) };
        readonly ListBox areas = new ListBox { Height = 100, Background = Brushes.Transparent, BorderThickness = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Stretch };
        readonly Button apply = Theme.Button("Apply", true);
        readonly DispatcherTimer seek = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        readonly Dictionary<VideoMask, BitmapSource> blurred = new Dictionary<VideoMask, BitmapSource>();
        CancellationTokenSource request;
        double duration, fps = 30;
        bool updating, closed;
        int selected = -1;
        public VideoToolWindow(string file, string mode, BitmapSource initial = null, double seconds = 0, double frameRate = 30) {
            source = file; operation = mode; Title = mode == "video-split" ? "Split Video" : mode == "video-redact" ? "Redact Video" : mode == "trim" ? "Trim Video" : "Change Video Speed";
            Width = 430; Height = mode == "video-redact" ? 800 : 690; MinWidth = 370; MinHeight = 560; WindowStartupLocation = WindowStartupLocation.CenterOwner; FontFamily = new FontFamily("Segoe UI"); Resources.Add(typeof(Button), MainWindow.ButtonStyle());
            var body = new Grid { Margin = new Thickness(15, 6, 15, 8) }; body.RowDefinitions.Add(new RowDefinition()); body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); body.Children.Add(preview);
            var controls = new StackPanel(); Grid.SetRow(controls, 1); body.Children.Add(controls); controls.Children.Add(timeline);
            var scrub = Row(); scrub.Children.Add(Small("◁", delegate { timeline.Value = Math.Max(0, timeline.Value - 1 / fps); })); clock.HorizontalAlignment = HorizontalAlignment.Center; clock.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(clock, 1); scrub.Children.Add(clock);
            var next = Small("▷", delegate { timeline.Value = Math.Min(timeline.Maximum, timeline.Value + 1 / fps); }); Grid.SetColumn(next, 2); scrub.Children.Add(next); controls.Children.Add(scrub);
            if (mode == "video-split") {
                var equalRow = new Grid { Margin = new Thickness(0, 9, 0, 7) }; equalRow.ColumnDefinitions.Add(new ColumnDefinition()); equalRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(65) }); equalRow.Children.Add(equal); parts.Text = "2"; Grid.SetColumn(parts, 1); equalRow.Children.Add(parts); controls.Children.Add(equalRow);
                controls.Children.Add(Theme.Text("Split points (seconds or timecodes, separated by ;)", 10, Theme.Muted)); points.Text = ""; points.IsEnabled = false; controls.Children.Add(points);
                equal.Click += delegate { points.IsEnabled = equal.IsChecked != true; parts.IsEnabled = equal.IsChecked == true; };
                controls.Children.Add(Small("Add split here", delegate { try { var cuts = string.IsNullOrWhiteSpace(points.Text) ? new double[0] : ReadPoints(); double at = timeline.Value; if (at < .04 || at >= duration - .04) throw new ArgumentException(Ui.T("Choose a point inside the video.")); points.Text = string.Join("; ", cuts.Concat(new[] { at }).Distinct().OrderBy(t => t).Select(MediaTime.Format)); equal.IsChecked = false; points.IsEnabled = true; parts.IsEnabled = false; error.Text = ""; } catch (Exception e) { error.Text = e.Message; } }));
            } else if (mode == "video-redact") {
                var look = new Grid { Margin = new Thickness(0, 7, 0, 4) }; look.ColumnDefinitions.Add(new ColumnDefinition()); look.ColumnDefinitions.Add(new ColumnDefinition()); style.Items.Add(Ui.T("Solid")); style.Items.Add(Ui.T("Blur")); style.SelectedIndex = 0; look.Children.Add(style); color.Text = "000000";
                var colorLabel = new StackPanel { Orientation = Orientation.Horizontal }; Ui.Preserve(swatch); colorLabel.Children.Add(swatch); colorLabel.Children.Add(Theme.Text("Color…", 11, Theme.Ink)); chooseColor.Content = colorLabel; chooseColor.Margin = new Thickness(8, 0, 0, 0); chooseColor.Padding = new Thickness(8, 4, 8, 4); chooseColor.MinHeight = 30; Grid.SetColumn(chooseColor, 1); look.Children.Add(chooseColor); controls.Children.Add(look);
                chooseColor.Click += delegate { if (selected < 0) return; using (var picker = new System.Windows.Forms.ColorDialog { FullOpen = true, Color = System.Drawing.ColorTranslator.FromHtml("#" + Masks[selected].Color) }) if (picker.ShowDialog() == System.Windows.Forms.DialogResult.OK) { color.Text = picker.Color.R.ToString("X2") + picker.Color.G.ToString("X2") + picker.Color.B.ToString("X2"); Commit(); RefreshFields(selected); } };
                var buttons = new StackPanel { Orientation = Orientation.Horizontal }; buttons.Children.Add(Small("Add area", delegate { try { Commit(); AddMask(); } catch (Exception e) { error.Text = e.Message; } })); buttons.Children.Add(Small("Remove area", delegate { if (selected >= 0) { Masks.RemoveAt(selected); RefreshMasks(Math.Min(selected, Masks.Count - 1)); } })); controls.Children.Add(buttons); ScrollViewer.SetHorizontalScrollBarVisibility(areas, ScrollBarVisibility.Disabled); controls.Children.Add(areas);
                areas.SelectionChanged += delegate { if (updating || areas.SelectedIndex == selected) return; int nextIndex = areas.SelectedIndex; try { Commit(); RefreshMasks(nextIndex); } catch (Exception e) { error.Text = e.Message; updating = true; areas.SelectedIndex = selected; updating = false; } };
                style.SelectionChanged += delegate { if (!updating && selected >= 0) { Masks[selected].Blur = style.SelectedIndex == 1; chooseColor.IsEnabled = style.SelectedIndex == 0; Surface.InvalidateVisual(); } };
                color.LostKeyboardFocus += delegate { try { Commit(); } catch (Exception e) { error.Text = e.Message; } };
                AddRangeFields(controls, false);
            } else if (mode == "trim") AddRangeFields(controls, true);
            else {
                controls.Children.Add(Theme.Text("Playback speed", 11, Theme.Ink)); foreach (double rate in new[] { .25, .5, .75, 1.0, 1.25, 1.5, 2.0, 3.0, 4.0 }) speed.Items.Add(rate.ToString("0.##", CultureInfo.InvariantCulture) + "×"); speed.SelectedIndex = 6; controls.Children.Add(speed); controls.Children.Add(Theme.Text("Audio pitch is preserved", 11, Theme.Muted));
            }
            error.TextWrapping = TextWrapping.Wrap; error.MaxHeight = 45; error.Margin = new Thickness(0, 5, 0, 0); controls.Children.Add(error);
            var footer = new Grid(); apply.HorizontalAlignment = HorizontalAlignment.Right; apply.Padding = new Thickness(16, 5, 16, 5); apply.MinHeight = 28; apply.IsEnabled = false; apply.Click += delegate { try { Value = BuildValue(); DialogResult = true; } catch (Exception e) { error.Text = e.Message; } }; footer.Children.Add(apply); ToolFrame.Build(this, Title, body, footer);
            timeline.ValueChanged += delegate { if (!updating && !closed) { clock.Text = MediaTime.Format(timeline.Value) + " / " + MediaTime.Format(duration); seek.Stop(); seek.Start(); } };
            seek.Tick += async delegate { seek.Stop(); await LoadFrame(); };
            if (initial != null) { duration = seconds; fps = frameRate > 0 ? frameRate : 30; SetFrame(initial); Initialize(); }
            else Loaded += async delegate { var ct = lifetime.Token; try { duration = await Task.Run(() => VideoFrames.Duration(source, ct)); fps = await Task.Run(() => VideoFrames.FrameRate(source, ct)); if (!closed) { await LoadFrame(); if (!closed && Surface != null) Initialize(); } } catch (OperationCanceledException) { } catch (Exception e) { if (!closed) error.Text = e.Message; } };
            Closed += delegate { closed = true; seek.Stop(); lifetime.Cancel(); if (request != null) request.Cancel(); lifetime.Dispose(); };
            PreviewKeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
        }
        static TextBox Input() { return new TextBox { Padding = new Thickness(6), Margin = new Thickness(0, 3, 0, 5), FontSize = 11 }; }
        static Grid Row() { var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); return grid; }
        Button Small(string title, Action action) { var b = Theme.Button(title, false); b.Padding = new Thickness(8, 4, 8, 4); b.MinHeight = 25; b.FontSize = 11; b.Click += delegate { action(); }; return b; }
        void AddRangeFields(Panel target, bool setters) {
            var row = new Grid { Margin = new Thickness(0, 6, 0, 0) }; row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition());
            var first = new StackPanel { Margin = new Thickness(0, 0, 6, 0) }; var last = new StackPanel { Margin = new Thickness(6, 0, 0, 0) }; first.Children.Add(Theme.Text("Start", 10, Theme.Muted)); first.Children.Add(start); last.Children.Add(Theme.Text("End", 10, Theme.Muted)); last.Children.Add(end); row.Children.Add(first); Grid.SetColumn(last, 1); row.Children.Add(last); target.Children.Add(row);
            if (setters) { first.Children.Add(Small("Start here", delegate { start.Text = MediaTime.Format(timeline.Value); })); last.Children.Add(Small("End here", delegate { end.Text = MediaTime.Format(timeline.Value); })); }
            else { start.LostKeyboardFocus += delegate { try { Commit(); RefreshMasks(selected); } catch (Exception e) { error.Text = e.Message; } }; end.LostKeyboardFocus += delegate { try { Commit(); RefreshMasks(selected); } catch (Exception e) { error.Text = e.Message; } }; }
        }
        void Initialize() { start.Text = MediaTime.Format(0); end.Text = MediaTime.Format(duration); if (operation == "video-redact" && Masks.Count == 0) AddMask(); apply.IsEnabled = duration > .04; }
        void SetFrame(BitmapSource frame) {
            blurred.Clear(); if (Surface == null) { Surface = new CropSurface(frame) { ShowOutsideMask = false, SelectionVisible = operation == "video-redact", IsHitTestVisible = operation == "video-redact" }; preview.Children.Add(Surface); Surface.PreviewOverlay = DrawMasks; Surface.Changed = delegate { if (!updating && selected >= 0) { Masks[selected].Area = Surface.Selection; blurred.Remove(Masks[selected]); Surface.InvalidateVisual(); } }; } else Surface.ReplaceFrame(frame);
            updating = true; timeline.Maximum = Math.Max(0, duration - .000001); timeline.IsEnabled = duration > 0; updating = false; clock.Text = MediaTime.Format(timeline.Value) + " / " + MediaTime.Format(duration);
        }
        async Task LoadFrame() {
            if (closed) return; if (request != null) request.Cancel(); var current = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); request = current; double at = timeline.Value; apply.IsEnabled = false; error.Text = Ui.T("Loading preview…");
            try { var frame = await Task.Run(() => VideoFrames.Read(source, at, current.Token)); if (!closed && request == current) { SetFrame(frame); apply.IsEnabled = duration > .04; error.Text = ""; } }
            catch (OperationCanceledException) { } catch (Exception e) { if (!closed && request == current) { error.Text = e.Message; apply.IsEnabled = Surface != null && duration > .04; } }
            finally { if (request == current) request = null; current.Dispose(); }
        }
        double[] ReadPoints() { return points.Text.Split(new[] { ';', '؛', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(MediaTime.Parse).ToArray(); }
        public Parameters BuildValue() {
            if (Surface == null || duration <= 0) throw new InvalidOperationException(Ui.T("Wait for the preview."));
            if (operation == "video-split") { int number; double[] cuts; if (equal.IsChecked == true) { if (!int.TryParse(MediaTime.Digits(parts.Text), out number) || number < 2 || number > 100) throw new ArgumentException(Ui.T("Choose between 2 and 100 clips.")); cuts = Enumerable.Range(1, number - 1).Select(i => duration * i / number).ToArray(); } else cuts = ReadPoints(); VideoOperations.Boundaries(duration, cuts); return new Parameters { SplitPoints = cuts }; }
            if (operation == "video-redact") { Commit(); VideoOperations.ValidateMasks(Masks, Surface.Source.PixelWidth, Surface.Source.PixelHeight, duration); return new Parameters { Redactions = Masks.Select(m => m.Clone()).ToList() }; }
            if (operation == "trim") { double a = MediaTime.Parse(start.Text), b = MediaTime.Parse(end.Text); if (a < 0 || b - a < .04 || b > duration + .001) throw new ArgumentException(Ui.T("Choose a valid area and time range.")); return new Parameters { Start = a, Duration = b - a }; }
            return new Parameters { Speed = new[] { .25, .5, .75, 1.0, 1.25, 1.5, 2.0, 3.0, 4.0 }[speed.SelectedIndex] };
        }
        void AddMask() { if (Surface == null || Masks.Count >= 12) return; Masks.Add(new VideoMask { Area = new Rect(Surface.Source.PixelWidth / 4.0, Surface.Source.PixelHeight / 4.0, Surface.Source.PixelWidth / 2.0, Surface.Source.PixelHeight / 2.0), Start = 0, End = duration }); RefreshMasks(Masks.Count - 1); }
        void Commit() {
            if (updating || operation != "video-redact" || selected < 0) return; var copy = Masks[selected].Clone(); copy.Start = MediaTime.Parse(start.Text); copy.End = MediaTime.Parse(end.Text); copy.Color = color.Text.Trim().TrimStart('#'); copy.Blur = style.SelectedIndex == 1;
            VideoOperations.ValidateMasks(new List<VideoMask> { copy }, Surface.Source.PixelWidth, Surface.Source.PixelHeight, duration); Masks[selected] = copy; blurred.Clear(); Surface.InvalidateVisual();
        }
        void RefreshMasks(int index) {
            updating = true; areas.Items.Clear(); selected = Math.Max(-1, Math.Min(index, Masks.Count - 1));
            for (int i = 0; i < Masks.Count; i++) {
                int row = i; var mask = Masks[i]; var stack = new StackPanel(); stack.Children.Add(Theme.Text((Ui.Arabic ? "منطقة " : "Area ") + (i + 1), 10, Theme.Muted));
                var bar = new TimeRangeBar { Duration = duration, Start = mask.Start, End = mask.End }; bar.Changed = delegate { Masks[row].Start = bar.Start; Masks[row].End = bar.End; RefreshFields(row); Surface.InvalidateVisual(); }; stack.Children.Add(bar); areas.Items.Add(new ListBoxItem { Content = stack, Padding = new Thickness(3, 0, 3, 0) });
            }
            areas.SelectedIndex = selected; updating = false; RefreshFields(selected); Surface.SelectionVisible = selected >= 0; Surface.InvalidateVisual();
        }
        void RefreshFields(int index) {
            if (index < 0 || index >= Masks.Count) return; updating = true; selected = index; areas.SelectedIndex = index; var mask = Masks[index]; start.Text = MediaTime.Format(mask.Start); end.Text = MediaTime.Format(mask.End); style.SelectedIndex = mask.Blur ? 1 : 0; color.Text = mask.Color; chooseColor.IsEnabled = !mask.Blur; swatch.Background = Theme.Brush("#" + mask.Color); Surface.Selection = mask.Area; updating = false; Surface.InvalidateVisual();
        }
        void DrawMasks(DrawingContext dc, Rect bounds) {
            if (operation != "video-redact") return; double sx = bounds.Width / Surface.Source.PixelWidth, sy = bounds.Height / Surface.Source.PixelHeight;
            foreach (var mask in Masks.Where(m => timeline.Value >= m.Start && timeline.Value < m.End)) {
                var r = mask.Area; var visible = new Rect(bounds.X + r.X * sx, bounds.Y + r.Y * sy, r.Width * sx, r.Height * sy);
                if (!mask.Blur) dc.DrawRectangle(Theme.Brush("#" + mask.Color), null, visible);
                else { BitmapSource image; if (!blurred.TryGetValue(mask, out image)) { image = BlurPreview(Surface.Source, r); blurred[mask] = image; } dc.DrawImage(image, visible); }
            }
        }
        static BitmapSource BlurPreview(BitmapSource source, Rect r) {
            int x = Math.Max(0, (int)r.X), y = Math.Max(0, (int)r.Y), w = Math.Max(1, Math.Min(source.PixelWidth - x, (int)Math.Ceiling(r.Width))), h = Math.Max(1, Math.Min(source.PixelHeight - y, (int)Math.Ceiling(r.Height)));
            var crop = new CroppedBitmap(source, new Int32Rect(x, y, w, h)); double scale = Math.Min(1, 180.0 / Math.Max(w, h)); var small = new TransformedBitmap(crop, new ScaleTransform(scale, scale)); w = small.PixelWidth; h = small.PixelHeight; var bytes = new byte[w * h * 4]; new FormatConvertedBitmap(small, PixelFormats.Bgra32, null, 0).CopyPixels(bytes, w * 4, 0); int radius = Math.Max(1, Math.Min(6, (int)Math.Round(12 * scale)));
            for (int pass = 0; pass < 4; pass++) { var next = new byte[bytes.Length]; for (int yy = 0; yy < h; yy++) for (int xx = 0; xx < w; xx++) { int offset = (yy * w + xx) * 4; for (int c = 0; c < 3; c++) { int sum = 0; for (int d = -radius; d <= radius; d++) { int px = pass % 2 == 0 ? Math.Max(0, Math.Min(w - 1, xx + d)) : xx, py = pass % 2 == 1 ? Math.Max(0, Math.Min(h - 1, yy + d)) : yy; sum += bytes[(py * w + px) * 4 + c]; } next[offset + c] = (byte)(sum / (2 * radius + 1)); } next[offset + 3] = 255; } bytes = next; }
            var bitmap = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, bytes, w * 4); bitmap.Freeze(); return bitmap;
        }
    }
}
