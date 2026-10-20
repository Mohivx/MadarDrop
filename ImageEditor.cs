using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace TangerineWindows
{
    public sealed class Mark
    {
        public string Kind, Text, Color = "#FF6320";
        public double X, Y, W, H, Thickness = 0.006;
        public List<Point> Points = new List<Point>();
        public Mark Clone() { var copy = (Mark)MemberwiseClone(); copy.Points = new List<Point>(Points); return copy; }
    }
    public sealed class ImageState
    {
        public BitmapSource Source, Background;
        public double Exposure, Contrast, Saturation, Warmth, Sharpness, Blur;
        public int OutputWidth;
        public bool Grayscale, Frame, Gradient, Shadow;
        public double Padding = 0.08, Corners = 0.04;
        public string Color = "#E8E0D5", Color2 = "#D3E4E0";
        public List<Mark> Marks = new List<Mark>();
        public ImageState Clone() { var s = (ImageState)MemberwiseClone(); s.Marks = Marks.Select(x => x.Clone()).ToList(); return s; }
    }
    public static class ImageEdits
    {
        public static BitmapSource Load(string path)
        {
            try { return Read(path); }
            catch {
                string temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tangerine-" + Guid.NewGuid().ToString("N") + ".png");
                try { Engine.Run("ffmpeg", new[] { "-v", "error", "-nostdin", "-n", "-protocol_whitelist", "file,pipe", "-i", path, "-frames:v", "1", "-update", "1", temp }, CancellationToken.None, null); return Read(temp); }
                finally { if (File.Exists(temp)) File.Delete(temp); }
            }
        }
        static BitmapSource Read(string path)
        {
            using (var stream = File.OpenRead(path)) {
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                BitmapSource source = decoder.Frames[0];
                if ((long)source.PixelWidth * source.PixelHeight > 64000000) throw new InvalidOperationException("This editor supports images up to 64 megapixels.");
                source.Freeze(); return source;
            }
        }
        static byte Byte(double x) { return (byte)Math.Max(0, Math.Min(255, Math.Round(x))); }
        public static BitmapSource Collage(string[] files, int columns)
        {
            if (files.Length == 0 || files.Length > 100) throw new InvalidOperationException("Choose between 1 and 100 images.");
            int cols = Math.Min(columns, files.Length), rows = (files.Length + cols - 1) / cols, cell = 500, gap = 18;
            int w = cols * cell + (cols + 1) * gap, h = rows * cell + (rows + 1) * gap;
            if ((long)w * h > 64000000) throw new InvalidOperationException("Choose fewer images or a wider grid.");
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen()) {
                dc.DrawRectangle(Theme.Brush("#F5F0E9"), null, new Rect(0, 0, w, h));
                for (int i = 0; i < files.Length; i++) {
                    var source = Load(files[i]); var brush = new ImageBrush(source) { Stretch = Stretch.UniformToFill };
                    dc.DrawRoundedRectangle(brush, null, new Rect(gap + (i % cols) * (cell + gap), gap + (i / cols) * (cell + gap), cell, cell), 20, 20);
                }
            }
            var image = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32); image.Render(visual); image.Freeze(); return image;
        }
        public static BitmapSource Render(ImageState state, int limit)
        {
            BitmapSource original = state.Source;
            int requested = state.OutputWidth > 0 ? state.OutputWidth : original.PixelWidth;
            if (requested > 16384 || requested < 1) throw new InvalidOperationException("Image width must be between 1 and 16384 pixels.");
            int w = limit > 0 ? Math.Min(requested, limit) : requested;
            int h = Math.Max(1, (int)Math.Round(original.PixelHeight * w / (double)original.PixelWidth));
            if ((long)w * h > 64000000) throw new InvalidOperationException("The output would exceed 64 megapixels.");
            BitmapSource scaled = original;
            if (w != original.PixelWidth || h != original.PixelHeight) scaled = new TransformedBitmap(original, new ScaleTransform(w / (double)original.PixelWidth, h / (double)original.PixelHeight));
            var formatted = new FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0);
            byte[] pixels = new byte[w * h * 4]; formatted.CopyPixels(pixels, w * 4, 0);
            double exposure = Math.Pow(2, state.Exposure), contrast = (100 + state.Contrast) / 100.0, saturation = (100 + state.Saturation) / 100.0;
            for (int i = 0; i < pixels.Length; i += 4) {
                double r = pixels[i + 2] * exposure, g = pixels[i + 1] * exposure, b = pixels[i] * exposure;
                r = (r - 128) * contrast + 128 + state.Warmth * 0.7;
                g = (g - 128) * contrast + 128;
                b = (b - 128) * contrast + 128 - state.Warmth * 0.7;
                double lum = r * .2126 + g * .7152 + b * .0722, sat = state.Grayscale ? 0 : saturation;
                pixels[i + 2] = Byte(lum + (r - lum) * sat); pixels[i + 1] = Byte(lum + (g - lum) * sat); pixels[i] = Byte(lum + (b - lum) * sat);
            }
            if (state.Sharpness > 0) {
                byte[] copy = (byte[])pixels.Clone(); double amount = state.Sharpness / 100.0;
                for (int y = 1; y < h - 1; y++) for (int x = 1; x < w - 1; x++) for (int c = 0; c < 3; c++) {
                    int i = (y * w + x) * 4 + c;
                    double neighbor = (copy[i - 4] + copy[i + 4] + copy[i - w * 4] + copy[i + w * 4]) / 4.0;
                    pixels[i] = Byte(copy[i] + amount * (copy[i] - neighbor));
                }
            }
            BitmapSource adjusted = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, w * 4); adjusted.Freeze();
            int pad = state.Frame ? (int)Math.Round(Math.Max(w, h) * state.Padding) : 0;
            int cw = w + pad * 2, ch = h + pad * 2;
            if ((long)cw * ch > 80000000) throw new InvalidOperationException("The framed output is too large.");
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen()) {
                if (state.Frame) {
                    Brush background = state.Gradient ? (Brush)new LinearGradientBrush((Color)ColorConverter.ConvertFromString(state.Color), (Color)ColorConverter.ConvertFromString(state.Color2), 45) : Theme.Brush(state.Color);
                    dc.DrawRectangle(background, null, new Rect(0, 0, cw, ch));
                    if (state.Background != null) { var ib = new ImageBrush(state.Background) { Stretch = Stretch.UniformToFill }; dc.DrawRectangle(ib, null, new Rect(0, 0, cw, ch)); }
                    if (state.Shadow) dc.DrawRoundedRectangle(Theme.Brush("#36000000"), null, new Rect(pad + w * .012, pad + h * .02, w, h), w * state.Corners, w * state.Corners);
                }
                dc.PushClip(new RectangleGeometry(new Rect(pad, pad, w, h), state.Frame ? w * state.Corners : 0, state.Frame ? w * state.Corners : 0));
                dc.DrawImage(adjusted, new Rect(pad, pad, w, h)); dc.Pop();
            }
            var baseImage = new RenderTargetBitmap(cw, ch, 96, 96, PixelFormats.Pbgra32); baseImage.Render(visual); baseImage.Freeze();
            var marked = new DrawingVisual();
            using (var dc = marked.RenderOpen()) {
                dc.DrawImage(baseImage, new Rect(0, 0, cw, ch));
                foreach (Mark m in state.Marks) DrawMark(dc, m, cw, ch, baseImage);
            }
            var result = new RenderTargetBitmap(cw, ch, 96, 96, PixelFormats.Pbgra32); result.Render(marked); result.Freeze(); return result;
        }
        static void DrawMark(DrawingContext dc, Mark mark, int w, int h, BitmapSource baseImage)
        {
            var p = new Point(mark.X * w, mark.Y * h); var end = new Point((mark.X + mark.W) * w, (mark.Y + mark.H) * h);
            var rect = new Rect(p, end); Brush color = Theme.Brush(mark.Color); double thickness = Math.Max(1, mark.Thickness * w);
            var pen = new Pen(color, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            if (mark.Kind == "cover") { dc.DrawRectangle(color, null, rect); return; }
            if (mark.Kind == "pixelate") {
                int x = Math.Max(0, (int)Math.Floor(rect.X)), y = Math.Max(0, (int)Math.Floor(rect.Y));
                int rw = Math.Min(w - x, Math.Max(1, (int)Math.Ceiling(rect.Width))), rh = Math.Min(h - y, Math.Max(1, (int)Math.Ceiling(rect.Height)));
                if (rw < 1 || rh < 1) return;
                var crop = new CroppedBitmap(baseImage, new Int32Rect(x, y, rw, rh));
                double scale = 1 / Math.Max(8, Math.Min(rw, rh) / 7.0);
                var small = new TransformedBitmap(crop, new ScaleTransform(scale, scale)); small.Freeze();
                var dv = new DrawingVisual(); RenderOptions.SetBitmapScalingMode(dv, BitmapScalingMode.NearestNeighbor);
                using (var ctx = dv.RenderOpen()) ctx.DrawImage(small, new Rect(0, 0, rw, rh));
                var expanded = new RenderTargetBitmap(rw, rh, 96, 96, PixelFormats.Pbgra32); expanded.Render(dv); expanded.Freeze();
                dc.DrawImage(expanded, new Rect(x, y, rw, rh)); return;
            }
            if (mark.Kind == "rectangle") { dc.DrawRectangle(null, pen, rect); return; }
            if (mark.Kind == "ellipse") { dc.DrawEllipse(null, pen, new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2), rect.Width / 2, rect.Height / 2); return; }
            if (mark.Kind == "text" || mark.Kind == "number") {
                string text = mark.Text ?? ""; double font = Math.Max(12, w * mark.Thickness * 5);
                bool rtl = text.Any(c => c >= 0x0590 && c <= 0x08FF);
                var ft = new FormattedText(text, CultureInfo.CurrentCulture, rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight, new Typeface("Segoe UI"), font, mark.Kind == "number" ? Brushes.White : color, 1.0);
                if (mark.Kind == "number") { double radius = Math.Max(font, ft.Width) * .75; dc.DrawEllipse(color, null, p, radius, radius); dc.DrawText(ft, new Point(p.X - ft.Width / 2, p.Y - ft.Height / 2)); }
                else dc.DrawText(ft, p);
                return;
            }
            if (mark.Kind == "pen" || mark.Kind == "highlight") {
                Brush stroke = mark.Kind == "highlight" ? new SolidColorBrush(Color.FromArgb(85, ((SolidColorBrush)color).Color.R, ((SolidColorBrush)color).Color.G, ((SolidColorBrush)color).Color.B)) : color;
                var pp = new Pen(stroke, mark.Kind == "highlight" ? thickness * 4 : thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
                for (int i = 1; i < mark.Points.Count; i++) dc.DrawLine(pp, new Point(mark.Points[i - 1].X * w, mark.Points[i - 1].Y * h), new Point(mark.Points[i].X * w, mark.Points[i].Y * h));
                return;
            }
            dc.DrawLine(pen, p, end);
            if (mark.Kind == "arrow") {
                Vector vector = p - end; if (vector.Length < .1) return; vector.Normalize(); Vector ortho = new Vector(-vector.Y, vector.X); double size = thickness * 4;
                dc.DrawLine(pen, end, end + vector * size + ortho * size * .55); dc.DrawLine(pen, end, end + vector * size - ortho * size * .55);
            }
        }
        public static string Save(ImageState state, string sourcePath, string format)
        {
            BitmapSource image = Render(state, 0); string ext = format == "JPG" ? ".jpg" : ".png";
            string temp = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(sourcePath), ".tangerine-" + Guid.NewGuid().ToString("N") + ext);
            try {
                BitmapEncoder encoder = format == "JPG" ? (BitmapEncoder)new JpegBitmapEncoder { QualityLevel = 95 } : new PngBitmapEncoder();
                if (format == "JPG") {
                    var white = new DrawingVisual(); using (var dc = white.RenderOpen()) { dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, image.PixelWidth, image.PixelHeight)); dc.DrawImage(image, new Rect(0, 0, image.PixelWidth, image.PixelHeight)); }
                    var rgb = new RenderTargetBitmap(image.PixelWidth, image.PixelHeight, 96, 96, PixelFormats.Pbgra32); rgb.Render(white); image = rgb;
                }
                encoder.Frames.Add(BitmapFrame.Create(image)); using (var stream = new FileStream(temp, FileMode.CreateNew)) encoder.Save(stream);
                return Engine.Publish(temp, sourcePath, ext, "edited");
            } finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }

    public sealed class ImageEditor : Window
    {
        public ImageState Model;
        public string Output;
        public bool ToolOnly { get; private set; }
        Grid layoutRoot;
        Grid editorBody;
        Border previewBoard, controlsPanel;
        StackPanel headerButtons;
        Button saveCopy;
        StackPanel modeTabs;
        TextBlock heading;
        readonly string sourcePath;
        readonly BitmapSource initialSource;
        readonly Image image = new Image { Stretch = Stretch.Fill, IsHitTestVisible = false };
        readonly Canvas canvas = new Canvas { Background = Brushes.Transparent, ClipToBounds = true };
        readonly StackPanel controls = new StackPanel { Margin = new Thickness(18) };
        readonly TextBlock status = Theme.Text("", 11, Theme.Muted);
        readonly Stack<ImageState> undo = new Stack<ImageState>(), redo = new Stack<ImageState>();
        readonly DispatcherTimer refresh;
        readonly ComboBox color = new ComboBox(), format = new ComboBox();
        readonly Rectangle selection = new Rectangle { Stroke = Theme.Accent, StrokeThickness = 2, StrokeDashArray = new DoubleCollection { 4, 3 }, Fill = Theme.Brush("#20FF6320"), IsHitTestVisible = false };
        string section, tool = "arrow";
        Point start;
        Mark pending;
        bool dragging, loading;
        public ImageEditor(string path, string sectionName, BitmapSource custom = null)
        {
            Resources.Add(typeof(Button), MainWindow.ButtonStyle());
            sourcePath = path; Model = new ImageState { Source = custom ?? ImageEdits.Load(path) }; initialSource = Model.Source;
            Title = "Edit · " + System.IO.Path.GetFileName(path); Width = 1160; Height = 800; MinWidth = 920; MinHeight = 650;
            WindowStartupLocation = WindowStartupLocation.CenterOwner; FontFamily = new FontFamily("Segoe UI"); Background = Theme.Paper;
            var root = new Grid { Background = Theme.Paper }; layoutRoot = root; root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(76) }); root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(54) }); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) }); Content = root;
            var top = new Grid { Margin = new Thickness(24, 0, 18, 0) }; top.ColumnDefinitions.Add(new ColumnDefinition()); top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var title = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; heading = Theme.Text("Make it your own.", 23, Theme.Ink, FontWeights.SemiBold); title.Children.Add(heading); title.Children.Add(Theme.Text(System.IO.Path.GetFileName(path), 11, Theme.Muted)); top.Children.Add(title);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center }; headerButtons = buttons;
            AddButton(buttons, "Undo", delegate { Undo(false); }); AddButton(buttons, "Redo", delegate { Undo(true); });
            AddButton(buttons, "Reset", delegate { Remember(); Model = new ImageState { Source = ImageEdits.Load(sourcePath) }; SetSection(section); UpdatePreview(); });
            format.Items.Add("PNG"); format.Items.Add("JPG"); format.SelectedIndex = 0; format.Width = 70; format.Margin = new Thickness(4, 0, 8, 0); format.VerticalContentAlignment = VerticalAlignment.Center; buttons.Children.Add(format);
            var save = Theme.Button("Save a copy", true); saveCopy = save; save.Click += delegate {
                try { Output = ImageEdits.Save(Model, sourcePath, (string)format.SelectedItem); DialogResult = true; } catch (Exception e) { MessageBox.Show(this, e.Message, "Could not save", MessageBoxButton.OK, MessageBoxImage.Error); }
            }; buttons.Children.Add(save); Grid.SetColumn(buttons, 1); top.Children.Add(buttons); root.Children.Add(top);
            var tabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(24, 4, 0, 8) }; modeTabs = tabs;
            string[] names = { "Adjust", "Annotate", "Redact", "Background", "Crop & rotate" }; string[] ids = { "edit", "annotate", "redact", "background", "crop" };
            for (int i = 0; i < names.Length; i++) { string id = ids[i]; AddButton(tabs, names[i], delegate { SetSection(id); }); } Grid.SetRow(tabs, 1); root.Children.Add(tabs);
            var body = new Grid { Margin = new Thickness(18, 0, 18, 0) }; editorBody = body; body.ColumnDefinitions.Add(new ColumnDefinition()); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(278) }); Grid.SetRow(body, 2); root.Children.Add(body);
            var board = new Border { CornerRadius = new CornerRadius(14), Background = Checker(), Padding = new Thickness(22), Margin = new Thickness(0, 0, 14, 0), ClipToBounds = true };
            previewBoard = board;
            canvas.Children.Add(image); canvas.Children.Add(selection); selection.Visibility = Visibility.Collapsed;
            board.Child = new Viewbox { Child = canvas, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.Both }; body.Children.Add(board);
            var panel = Theme.Card(new ScrollViewer { Content = controls, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); Grid.SetColumn(panel, 1); body.Children.Add(panel);
            controlsPanel = panel;
            status.Margin = new Thickness(25, 0, 0, 0); status.VerticalAlignment = VerticalAlignment.Center; Grid.SetRow(status, 3); root.Children.Add(status);
            refresh = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) }; refresh.Tick += delegate { refresh.Stop(); UpdatePreview(); };
            canvas.MouseLeftButtonDown += BeginMark; canvas.MouseMove += MoveMark; canvas.MouseLeftButtonUp += EndMark;
            PreviewKeyDown += delegate(object s, KeyEventArgs e) {
                if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z) { Undo(false); e.Handled = true; }
                if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y) { Undo(true); e.Handled = true; }
                if (e.Key == Key.Escape && dragging) { dragging = false; canvas.ReleaseMouseCapture(); selection.Visibility = Visibility.Collapsed; }
            };
            Closed += delegate { refresh.Stop(); };
            SetSection(sectionName); UpdatePreview();
            Ui.Attach(this, delegate { board.Background = Checker(); });
        }
        static Brush Checker()
        {
            var group = new DrawingGroup(); group.Children.Add(new GeometryDrawing(Theme.Brush(Ui.Dark ? "#24282D" : "#E8EBE6"), null, new RectangleGeometry(new Rect(0, 0, 20, 20))));
            group.Children.Add(new GeometryDrawing(Theme.Brush(Ui.Dark ? "#30353B" : "#F0F2EE"), null, new RectangleGeometry(new Rect(0, 0, 10, 10))));
            group.Children.Add(new GeometryDrawing(Theme.Brush(Ui.Dark ? "#30353B" : "#F0F2EE"), null, new RectangleGeometry(new Rect(10, 10, 10, 10))));
            return new DrawingBrush(group) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 20, 20), ViewportUnits = BrushMappingMode.Absolute };
        }
        public void FocusTool(string id)
        {
            if (ToolOnly) return;
            ToolOnly = true; Width = 450; Height = 730; MinWidth = 390; MinHeight = 540;
            string name = id == "annotate" ? "Annotate photo" : id == "redact" ? "Redact photo" : id == "background" ? "Add background" : "Edit photo";
            Title = name;
            editorBody.Children.Clear(); headerButtons.Children.Remove(format); headerButtons.Children.Remove(saveCopy);
            var body = new Grid { Margin = new Thickness(14, 8, 14, 0) };
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 160 });
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 145 });
            previewBoard.Margin = new Thickness(0, 0, 0, 8); previewBoard.Padding = new Thickness(10); body.Children.Add(previewBoard);
            Grid.SetColumn(controlsPanel, 0); Grid.SetRow(controlsPanel, 1); controls.Margin = new Thickness(12, 4, 12, 10); body.Children.Add(controlsPanel);
            var footer = new Grid(); footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var history = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            AddButton(history, "↶", delegate { Undo(false); }); AddButton(history, "↷", delegate { Undo(true); });
            foreach (Button b in history.Children) { b.Padding = new Thickness(7, 4, 7, 4); b.MinHeight = 26; b.ToolTip = Ui.T(b.Content.ToString() == "↶" ? "Undo" : "Redo"); }
            var reset = Theme.Button("Reset", false); reset.Padding = new Thickness(5, 4, 5, 4); reset.MinHeight = 26; reset.FontSize = 11; reset.Click += delegate { Remember(); Model = new ImageState { Source = initialSource }; SetSection(section); UpdatePreview(); }; history.Children.Add(reset);
            footer.Children.Add(history);
            var output = new StackPanel { Orientation = Orientation.Horizontal }; format.Height = 29; format.Width = 64; output.Children.Add(format); saveCopy.Padding = new Thickness(10, 6, 10, 6); saveCopy.MinHeight = 29; output.Children.Add(saveCopy); Grid.SetColumn(output, 1); footer.Children.Add(output);
            ToolFrame.Build(this, name, body, footer); Ui.Apply(this);
        }
        void AddButton(Panel panel, string title, Action action) { var b = Theme.Button(title, false); b.Padding = new Thickness(12, 8, 12, 8); b.MinHeight = 31; b.Click += delegate { action(); }; panel.Children.Add(b); }
        void Header(string title, string detail) { controls.Children.Add(Theme.Text(title, 18, Theme.Ink, FontWeights.SemiBold)); var d = Theme.Text(detail, 11, Theme.Muted); d.LineHeight = 18; d.Margin = new Thickness(0, 9, 0, 16); controls.Children.Add(d); }
        void Slider(string title, double min, double max, double value, Action<double> changed)
        {
            var label = Theme.Text(title, 12, Theme.Ink); label.Margin = new Thickness(0, 11, 0, 7); controls.Children.Add(label);
            var s = new Slider { Minimum = min, Maximum = max, Value = value, TickFrequency = (max - min) / 100 };
            s.PreviewMouseLeftButtonDown += delegate { Remember(); }; s.PreviewKeyDown += delegate { Remember(); };
            s.ValueChanged += delegate { if (!loading) { changed(s.Value); refresh.Stop(); refresh.Start(); } }; controls.Children.Add(s);
        }
        void Check(string label, bool value, Action<bool> changed)
        {
            var c = new CheckBox { Content = label, IsChecked = value, Margin = new Thickness(0, 15, 0, 0) }; c.Click += delegate { Remember(); changed(c.IsChecked == true); UpdatePreview(); }; controls.Children.Add(c);
        }
        void Palette(Action<string> changed, string initial)
        {
            var palette = new WrapPanel { Margin = new Thickness(0, 6, 0, 6) };
            foreach (string hex in new[] { "#FF6320", "#111111", "#FFFFFF", "#EC4545", "#2A9C6C", "#377BE5", "#F8D44A", "#E8E0D5", "#D3E4E0", "#B9A8DE" }) {
                string selected = hex; var b = new Button { Background = Theme.Brush(hex), BorderBrush = Theme.Line, BorderThickness = new Thickness(1), Width = 30, Height = 30, Margin = new Thickness(3), ToolTip = hex }; Ui.Preserve(b); b.Click += delegate { changed(selected); }; palette.Children.Add(b);
            }
            controls.Children.Add(palette);
        }
        string markColor = "#FF6320"; double lineWidth = .006;
        void SetSection(string name)
        {
            section = name; controls.Children.Clear(); loading = true; canvas.Cursor = name == "edit" || name == "background" ? Cursors.Arrow : Cursors.Cross;
            if (name == "edit") {
                Header("Photo adjustments", "Changes appear in the preview. Save exports a new image.");
                Slider("Exposure", -2, 2, Model.Exposure, v => Model.Exposure = v);
                Slider("Contrast", -80, 100, Model.Contrast, v => Model.Contrast = v);
                Slider("Saturation", -100, 100, Model.Saturation, v => Model.Saturation = v);
                Slider("Warmth", -80, 80, Model.Warmth, v => Model.Warmth = v);
                Slider("Sharpness", 0, 100, Model.Sharpness, v => Model.Sharpness = v);
                Check("Black and white", Model.Grayscale, v => Model.Grayscale = v);
                var label = Theme.Text("Output width (pixels)", 12, Theme.Ink); label.Margin = new Thickness(0, 22, 0, 6); controls.Children.Add(label);
                var width = new TextBox { Text = (Model.OutputWidth > 0 ? Model.OutputWidth : Model.Source.PixelWidth).ToString(), Padding = new Thickness(8) }; controls.Children.Add(width);
                AddButton(controls, "Apply size", delegate { int n; if (int.TryParse(width.Text, out n) && n > 0 && n <= 16384) { Remember(); Model.OutputWidth = n; UpdatePreview(); } else status.Text = "Width must be 1–16384 pixels."; });
            } else if (name == "annotate" || name == "redact") {
                Header(name == "redact" ? "Cover private areas" : "Draw on your image", name == "redact" ? "Drag a box to cover or pixelate an area. Export flattens the result into image pixels." : "Choose a tool, then drag on the image. Click to add text or a numbered callout.");
                string[] kinds = name == "redact" ? new[] { "cover", "pixelate" } : new[] { "arrow", "line", "rectangle", "ellipse", "pen", "highlight", "text", "number" };
                var choices = new ComboBox { Margin = new Thickness(0, 0, 0, 15), Padding = new Thickness(7) }; foreach (string kind in kinds) choices.Items.Add(new ComboBoxItem { Content = kind, Tag = kind }); choices.SelectedIndex = 0; tool = kinds[0]; choices.SelectionChanged += delegate { tool = (string)((ComboBoxItem)choices.SelectedItem).Tag; }; controls.Children.Add(choices);
                if (name == "redact") markColor = "#111111";
                Palette(v => markColor = v, markColor); Slider("Stroke / text size", .002, .024, lineWidth, v => lineWidth = v);
                AddButton(controls, "Remove last mark", delegate { if (Model.Marks.Count > 0) { Remember(); Model.Marks.RemoveAt(Model.Marks.Count - 1); UpdatePreview(); } });
            } else if (name == "background") {
                Header("A new background", "Add a border or place the image on another photo.");
                Check("Enable background", Model.Frame, v => Model.Frame = v);
                Palette(v => { Remember(); Model.Color = v; Model.Frame = true; UpdatePreview(); }, Model.Color);
                Check("Gradient", Model.Gradient, v => { Model.Gradient = v; Model.Frame = true; });
                Check("Drop shadow", Model.Shadow, v => Model.Shadow = v);
                Slider("Spacing", 0, .35, Model.Padding, v => { Model.Padding = v; Model.Frame = true; });
                Slider("Rounded corners", 0, .25, Model.Corners, v => { Model.Corners = v; Model.Frame = true; });
                AddButton(controls, "Use a background image…", delegate {
                    var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Images|*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.tiff;*.avif|All files|*.*" };
                    if (dialog.ShowDialog(this) == true) { try { var background = ImageEdits.Load(dialog.FileName); Remember(); Model.Background = background; Model.Frame = true; UpdatePreview(); } catch (Exception e) { status.Text = e.Message; } }
                });
                AddButton(controls, "Remove background image", delegate { Remember(); Model.Background = null; UpdatePreview(); });
            } else {
                Header("Crop & rotate", "Drag a rectangle over the image to crop. Undo brings the previous image back.");
                AddButton(controls, "Rotate left ↶", delegate { Transform(new RotateTransform(-90)); });
                AddButton(controls, "Rotate right ↷", delegate { Transform(new RotateTransform(90)); });
                AddButton(controls, "Flip horizontally", delegate { Transform(new ScaleTransform(-1, 1)); });
                AddButton(controls, "Flip vertically", delegate { Transform(new ScaleTransform(1, -1)); });
                controls.Children.Add(Theme.Text("Crop is applied to the current visible image, including any marks or background.", 11, Theme.Muted));
            }
            loading = false;
            Ui.Apply(this);
        }
        void Remember() { undo.Push(Model.Clone()); redo.Clear(); }
        void Undo(bool forward)
        {
            var from = forward ? redo : undo; var to = forward ? undo : redo; if (from.Count == 0) return;
            to.Push(Model.Clone()); Model = from.Pop(); SetSection(section); UpdatePreview();
        }
        void Transform(Transform transform)
        {
            try { var rendered = ImageEdits.Render(Model, 0); var transformed = new TransformedBitmap(rendered, transform); transformed.Freeze(); Remember(); Model = new ImageState { Source = transformed }; SetSection(section); UpdatePreview(); } catch (Exception e) { status.Text = e.Message; }
        }
        public void UpdatePreview()
        {
            try {
                var preview = ImageEdits.Render(Model, 1000); image.Source = preview; image.Width = canvas.Width = preview.PixelWidth; image.Height = canvas.Height = preview.PixelHeight;
                status.Text = Model.Source.PixelWidth + " × " + Model.Source.PixelHeight + " source · " + Model.Marks.Count + " marks · Original file untouched";
            } catch (Exception e) { status.Text = e.Message; }
        }
        Point Position(MouseEventArgs e) { Point p = e.GetPosition(canvas); return new Point(Math.Max(0, Math.Min(1, p.X / canvas.Width)), Math.Max(0, Math.Min(1, p.Y / canvas.Height))); }
        void BeginMark(object sender, MouseButtonEventArgs e)
        {
            if (section == "edit" || section == "background") return;
            start = Position(e);
            if (section == "annotate" && (tool == "text" || tool == "number")) {
                string value = tool == "number" ? (Model.Marks.Count(m => m.Kind == "number") + 1).ToString() : TextPrompt.Ask(this, "Add text", "Your text");
                if (string.IsNullOrEmpty(value)) return; Remember(); Model.Marks.Add(new Mark { Kind = tool, X = start.X, Y = start.Y, Text = value, Color = markColor, Thickness = lineWidth }); UpdatePreview(); return;
            }
            dragging = true; canvas.CaptureMouse(); pending = new Mark { Kind = section == "crop" ? "crop" : tool, X = start.X, Y = start.Y, Color = markColor, Thickness = lineWidth }; pending.Points.Add(start);
            selection.Visibility = Visibility.Visible; selection.Width = selection.Height = 0;
        }
        void MoveMark(object sender, MouseEventArgs e)
        {
            if (!dragging) return; Point p = Position(e); pending.W = p.X - start.X; pending.H = p.Y - start.Y; pending.Points.Add(p);
            Canvas.SetLeft(selection, Math.Min(start.X, p.X) * canvas.Width); Canvas.SetTop(selection, Math.Min(start.Y, p.Y) * canvas.Height);
            selection.Width = Math.Abs(p.X - start.X) * canvas.Width; selection.Height = Math.Abs(p.Y - start.Y) * canvas.Height;
        }
        void EndMark(object sender, MouseButtonEventArgs e)
        {
            if (!dragging) return; MoveMark(sender, e); dragging = false; canvas.ReleaseMouseCapture(); selection.Visibility = Visibility.Collapsed;
            if (Math.Abs(pending.W) < .003 && Math.Abs(pending.H) < .003) return;
            if (pending.Kind == "crop") {
                try {
                    BitmapSource rendered = ImageEdits.Render(Model, 0); int x = (int)(Math.Min(pending.X, pending.X + pending.W) * rendered.PixelWidth), y = (int)(Math.Min(pending.Y, pending.Y + pending.H) * rendered.PixelHeight);
                    int w = Math.Max(1, Math.Min(rendered.PixelWidth - x, (int)(Math.Abs(pending.W) * rendered.PixelWidth))), h = Math.Max(1, Math.Min(rendered.PixelHeight - y, (int)(Math.Abs(pending.H) * rendered.PixelHeight)));
                    var crop = new CroppedBitmap(rendered, new Int32Rect(x, y, w, h)); crop.Freeze(); Remember(); Model = new ImageState { Source = crop }; SetSection(section);
                } catch (Exception ex) { status.Text = ex.Message; }
            } else { Remember(); Model.Marks.Add(pending); }
            pending = null; UpdatePreview();
        }
        public void ExportPreview(string folder)
        {
            Directory.CreateDirectory(folder); Show(); UpdateLayout();
            var surface = (FrameworkElement)Content; var bitmap = new RenderTargetBitmap((int)surface.ActualWidth, (int)surface.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(surface);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(System.IO.Path.Combine(folder, "image-editor.png"))) png.Save(stream); Close();
        }
    }
    public sealed class TextPrompt : Window
    {
        public static string Ask(Window owner, string title, string initial, Native.POINT? anchor = null)
        {
            var window = new TextPrompt { Owner = owner, Title = title, Width = 370, Height = 200, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, Background = Theme.Paper };
            if (anchor.HasValue) QuickPopup.Place(window, anchor.Value);
            var panel = new StackPanel { Margin = new Thickness(20) }; var box = new TextBox { Text = Ui.T(initial), Padding = new Thickness(9), FontSize = 15 }; panel.Children.Add(box);
            var ok = Theme.Button("Add", true); ok.Margin = new Thickness(0, 18, 0, 0); ok.IsDefault = true; ok.Click += delegate { window.DialogResult = true; }; panel.Children.Add(ok); window.Content = panel;
            Ui.Attach(window); window.Loaded += delegate { box.Focus(); box.SelectAll(); }; return window.ShowDialog() == true ? box.Text : null;
        }
    }
}
