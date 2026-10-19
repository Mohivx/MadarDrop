using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace TangerineWindows
{
    public sealed class CropSurface : FrameworkElement
    {
        public readonly BitmapSource Source;
        public Rect Selection;
        public double Aspect;
        public Action Changed;
        Rect initial;
        Point origin;
        int handle;
        bool dragging;
        public CropSurface(BitmapSource source)
        {
            Source = source; Selection = new Rect(0, 0, source.PixelWidth, source.PixelHeight); MinHeight = 100;
            MouseLeftButtonDown += Start; MouseMove += Move; MouseLeftButtonUp += End;
            LostMouseCapture += delegate { dragging = false; };
        }
        public Rect ImageBounds()
        {
            double scale = Math.Min(Math.Max(1, ActualWidth - 12) / Source.PixelWidth, Math.Max(1, ActualHeight - 12) / Source.PixelHeight);
            double w = Source.PixelWidth * scale, h = Source.PixelHeight * scale;
            return new Rect((ActualWidth - w) / 2, (ActualHeight - h) / 2, w, h);
        }
        Rect ScreenSelection() { var b = ImageBounds(); return new Rect(b.X + Selection.X * b.Width / Source.PixelWidth, b.Y + Selection.Y * b.Height / Source.PixelHeight, Selection.Width * b.Width / Source.PixelWidth, Selection.Height * b.Height / Source.PixelHeight); }
        Point Pixel(Point p) { var b = ImageBounds(); return new Point(Math.Max(0, Math.Min(Source.PixelWidth, (p.X - b.X) * Source.PixelWidth / b.Width)), Math.Max(0, Math.Min(Source.PixelHeight, (p.Y - b.Y) * Source.PixelHeight / b.Height))); }
        Point[] Points(Rect r) { return new[] { r.TopLeft, r.TopRight, r.BottomRight, r.BottomLeft, new Point(r.X + r.Width / 2, r.Top), new Point(r.Right, r.Y + r.Height / 2), new Point(r.X + r.Width / 2, r.Bottom), new Point(r.Left, r.Y + r.Height / 2) }; }
        int Hit(Point p)
        {
            Point[] points = Points(ScreenSelection()); for (int i = 0; i < points.Length; i++) if ((p - points[i]).Length <= 10) return i + 1;
            return ScreenSelection().Contains(p) ? 0 : 9;
        }
        public void Reset() { Aspect = 0; Selection = new Rect(0, 0, Source.PixelWidth, Source.PixelHeight); Notify(); }
        public void SetAspect(double ratio)
        {
            Aspect = ratio;
            if (ratio > 0) {
                double w = Selection.Width, h = w / ratio; if (h > Source.PixelHeight) { h = Source.PixelHeight; w = h * ratio; }
                if (w > Source.PixelWidth) { w = Source.PixelWidth; h = w / ratio; }
                Selection = new Rect(Math.Max(0, Math.Min(Selection.X + (Selection.Width - w) / 2, Source.PixelWidth - w)), Math.Max(0, Math.Min(Selection.Y + (Selection.Height - h) / 2, Source.PixelHeight - h)), w, h);
            }
            Notify();
        }
        public void SetDimensions(double width, double height, bool widthChanged)
        {
            width = Math.Max(1, width); height = Math.Max(1, height);
            if (Aspect > 0) { if (widthChanged) height = width / Aspect; else width = height * Aspect; }
            if (Aspect > 0) { double factor = Math.Min(1, Math.Min(Source.PixelWidth / width, Source.PixelHeight / height)); width *= factor; height *= factor; }
            else { width = Math.Min(Source.PixelWidth, width); height = Math.Min(Source.PixelHeight, height); }
            Selection = new Rect(Math.Max(0, Math.Min(Selection.X, Source.PixelWidth - width)), Math.Max(0, Math.Min(Selection.Y, Source.PixelHeight - height)), width, height); Notify();
        }
        public void ResizeFrom(int which, Rect before, Point pixel)
        {
            if (which >= 1 && which <= 4) {
                bool west = which == 1 || which == 4, north = which == 1 || which == 2;
                double ax = west ? before.Right : before.Left, ay = north ? before.Bottom : before.Top;
                double w = Math.Max(1, west ? ax - pixel.X : pixel.X - ax), h = Math.Max(1, north ? ay - pixel.Y : pixel.Y - ay);
                if (Aspect > 0) { if (w / h > Aspect) h = w / Aspect; else w = h * Aspect; }
                double maxW = west ? ax : Source.PixelWidth - ax, maxH = north ? ay : Source.PixelHeight - ay;
                if (Aspect > 0) { double factor = Math.Min(1, Math.Min(maxW / w, maxH / h)); w *= factor; h *= factor; }
                else { w = Math.Min(maxW, w); h = Math.Min(maxH, h); }
                Selection = new Rect(west ? ax - w : ax, north ? ay - h : ay, w, h);
            } else if (which == 6 || which == 8) {
                bool west = which == 8; double anchor = west ? before.Right : before.Left;
                double w = Math.Max(1, west ? anchor - pixel.X : pixel.X - anchor), center = before.Y + before.Height / 2;
                double h = Aspect > 0 ? w / Aspect : before.Height;
                if (Aspect > 0) { double factor = Math.Min(1, Math.Min((west ? anchor : Source.PixelWidth - anchor) / w, Math.Min(center, Source.PixelHeight - center) * 2 / h)); w *= factor; h *= factor; }
                else w = Math.Min(west ? anchor : Source.PixelWidth - anchor, w);
                Selection = new Rect(west ? anchor - w : anchor, center - h / 2, w, h);
            } else if (which == 5 || which == 7) {
                bool north = which == 5; double anchor = north ? before.Bottom : before.Top;
                double h = Math.Max(1, north ? anchor - pixel.Y : pixel.Y - anchor), center = before.X + before.Width / 2;
                double w = Aspect > 0 ? h * Aspect : before.Width;
                if (Aspect > 0) { double factor = Math.Min(1, Math.Min((north ? anchor : Source.PixelHeight - anchor) / h, Math.Min(center, Source.PixelWidth - center) * 2 / w)); w *= factor; h *= factor; }
                else h = Math.Min(north ? anchor : Source.PixelHeight - anchor, h);
                Selection = new Rect(center - w / 2, north ? anchor - h : anchor, w, h);
            }
            Notify();
        }
        void Notify() { InvalidateVisual(); if (Changed != null) Changed(); }
        void Start(object sender, MouseButtonEventArgs e)
        {
            Point p = e.GetPosition(this); if (!ImageBounds().Contains(p)) return;
            handle = Hit(p); origin = Pixel(p); initial = Selection; dragging = true; CaptureMouse(); e.Handled = true;
        }
        void Move(object sender, MouseEventArgs e)
        {
            Point point = e.GetPosition(this);
            if (!dragging) {
                int over = Hit(point); Cursor = over == 0 ? Cursors.SizeAll : over == 1 || over == 3 ? Cursors.SizeNWSE : over == 2 || over == 4 ? Cursors.SizeNESW : over == 5 || over == 7 ? Cursors.SizeNS : over == 6 || over == 8 ? Cursors.SizeWE : Cursors.Cross;
                return;
            }
            Point p = Pixel(point);
            if (handle == 0) { double x = Math.Max(0, Math.Min(Source.PixelWidth - initial.Width, initial.X + p.X - origin.X)), y = Math.Max(0, Math.Min(Source.PixelHeight - initial.Height, initial.Y + p.Y - origin.Y)); Selection = new Rect(x, y, initial.Width, initial.Height); Notify(); }
            else if (handle == 9) {
                bool west = p.X < origin.X, north = p.Y < origin.Y; double w = Math.Max(1, Math.Abs(p.X - origin.X)), h = Math.Max(1, Math.Abs(p.Y - origin.Y));
                if (Aspect > 0) { if (w / h > Aspect) h = w / Aspect; else w = h * Aspect; }
                double maxW = west ? origin.X : Source.PixelWidth - origin.X, maxH = north ? origin.Y : Source.PixelHeight - origin.Y;
                double factor = Math.Min(1, Math.Min(maxW / w, maxH / h)); w *= factor; h *= factor;
                Selection = new Rect(west ? origin.X - w : origin.X, north ? origin.Y - h : origin.Y, w, h); Notify();
            } else ResizeFrom(handle, initial, p);
        }
        void End(object sender, MouseButtonEventArgs e) { if (!dragging) return; Move(sender, e); dragging = false; ReleaseMouseCapture(); Notify(); }
        public BitmapSource Crop()
        {
            int x = Math.Min(Source.PixelWidth - 1, Math.Max(0, (int)Math.Round(Selection.X))), y = Math.Min(Source.PixelHeight - 1, Math.Max(0, (int)Math.Round(Selection.Y)));
            int w = Math.Max(1, Math.Min(Source.PixelWidth - x, (int)Math.Round(Selection.Width))), h = Math.Max(1, Math.Min(Source.PixelHeight - y, (int)Math.Round(Selection.Height)));
            var bitmap = new CroppedBitmap(Source, new Int32Rect(x, y, w, h)); bitmap.Freeze(); return bitmap;
        }
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc); var b = ImageBounds(); var r = ScreenSelection(); dc.DrawImage(Source, b);
            var mask = new SolidColorBrush(Color.FromArgb(150, 47, 52, 55));
            dc.DrawRectangle(mask, null, new Rect(b.X, b.Y, b.Width, Math.Max(0, r.Top - b.Top)));
            dc.DrawRectangle(mask, null, new Rect(b.X, r.Bottom, b.Width, Math.Max(0, b.Bottom - r.Bottom)));
            dc.DrawRectangle(mask, null, new Rect(b.X, r.Top, Math.Max(0, r.Left - b.Left), r.Height));
            dc.DrawRectangle(mask, null, new Rect(r.Right, r.Top, Math.Max(0, b.Right - r.Right), r.Height));
            dc.DrawRectangle(null, new Pen(Theme.Accent, 1.5), r);
            if (dragging) {
                var grid = new Pen(new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)), .7);
                for (int i = 1; i < 3; i++) { dc.DrawLine(grid, new Point(r.X + r.Width * i / 3, r.Y), new Point(r.X + r.Width * i / 3, r.Bottom)); dc.DrawLine(grid, new Point(r.X, r.Y + r.Height * i / 3), new Point(r.Right, r.Y + r.Height * i / 3)); }
            }
            Point[] points = Points(r);
            for (int i = 0; i < points.Length; i++) { double w = i < 4 ? 7 : i == 4 || i == 6 ? 15 : 4, h = i < 4 ? 7 : i == 4 || i == 6 ? 4 : 15; dc.DrawRoundedRectangle(Theme.Accent, null, new Rect(points[i].X - w / 2, points[i].Y - h / 2, w, h), 1.4, 1.4); }
        }
    }
    public sealed class CropWindow : Window
    {
        public string Output;
        public readonly CropSurface Surface;
        readonly string path;
        readonly TextBox width = new TextBox(), height = new TextBox();
        readonly System.Collections.Generic.List<Button> ratios = new System.Collections.Generic.List<Button>();
        bool updating;
        public CropWindow(string file)
        {
            path = file; Surface = new CropSurface(ImageEdits.Load(file));
            Title = "Crop Image"; Width = 360; Height = 520; MinWidth = 320; MinHeight = 420; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize; FontFamily = new FontFamily("Segoe UI");
            Resources.Add(typeof(Button), MainWindow.ButtonStyle());
            var glass = new Border { Margin = new Thickness(10), CornerRadius = new CornerRadius(20), BorderBrush = Theme.Brush("#65FFFFFF"), BorderThickness = new Thickness(1), Background = new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#F2FFD17F"), (Color)ColorConverter.ConvertFromString("#F2FF9E88"), 90), Effect = new DropShadowEffect { BlurRadius = 16, ShadowDepth = 3, Opacity = .22 } };
            Content = glass; var root = new Grid { Margin = new Thickness(14, 0, 14, 0) }; glass.Child = root;
            foreach (double row in new[] { 34.0, -1, 42, 32, 48 }) root.RowDefinitions.Add(new RowDefinition { Height = row < 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(row) });
            var bar = new Grid { Background = Brushes.Transparent }; var title = Theme.Text("Crop Image", 12, Theme.Ink, FontWeights.SemiBold); title.HorizontalAlignment = HorizontalAlignment.Center; title.VerticalAlignment = VerticalAlignment.Center; bar.Children.Add(title);
            var close = Theme.Button("×", false); close.Background = Brushes.Transparent; close.BorderThickness = new Thickness(0); close.Padding = new Thickness(3); close.MinHeight = 20; close.Width = 22; close.Height = 22; close.FontSize = 17; close.HorizontalAlignment = HorizontalAlignment.Left; close.VerticalAlignment = VerticalAlignment.Center; close.Margin = new Thickness(0); close.Click += delegate { Close(); }; bar.Children.Add(close);
            bar.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }; root.Children.Add(bar);
            Grid.SetRow(Surface, 1); root.Children.Add(Surface);
            var aspect = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center }; var label = Theme.Text("Aspect ratio", 9, Theme.Brush("#725335")); label.Width = 67; label.Margin = new Thickness(0, 0, 8, 0); label.VerticalAlignment = VerticalAlignment.Center; aspect.Children.Add(label);
            string[] labels = { "Free", "1:1", "16:9", "9:16", "4:3", "3:4" }; double[] values = { 0, 1, 16.0 / 9, 9.0 / 16, 4.0 / 3, 3.0 / 4 };
            for (int i = 0; i < labels.Length; i++) { int index = i; var button = Theme.Button(labels[i], false); button.Padding = new Thickness(5, 4, 5, 4); button.MinHeight = 22; button.FontSize = 9; button.Margin = new Thickness(0, 0, 2, 0); button.BorderThickness = new Thickness(0); button.Click += delegate { Surface.SetAspect(values[index]); SelectRatio(index); }; ratios.Add(button); aspect.Children.Add(button); }
            Grid.SetRow(aspect, 2); root.Children.Add(aspect); SelectRatio(0);
            var dimensions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var wl = Theme.Text("W", 10, Theme.Brush("#725335")); wl.VerticalAlignment = VerticalAlignment.Center; wl.Margin = new Thickness(0, 0, 6, 0); dimensions.Children.Add(wl); PrepareField(width); dimensions.Children.Add(width);
            var hl = Theme.Text("H", 10, Theme.Brush("#725335")); hl.VerticalAlignment = VerticalAlignment.Center; hl.Margin = new Thickness(10, 0, 6, 0); dimensions.Children.Add(hl); PrepareField(height); dimensions.Children.Add(height);
            var px = Theme.Text("px", 10, Theme.Brush("#725335")); px.VerticalAlignment = VerticalAlignment.Center; px.Margin = new Thickness(6, 0, 0, 0); dimensions.Children.Add(px); Grid.SetRow(dimensions, 3); root.Children.Add(dimensions);
            width.LostKeyboardFocus += delegate { Dimensions(true); }; height.LostKeyboardFocus += delegate { Dimensions(false); };
            width.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) Dimensions(true); }; height.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) Dimensions(false); };
            var footer = new Grid(); var reset = Theme.Button("Reset", false); reset.Background = Brushes.Transparent; reset.BorderThickness = new Thickness(0); reset.FontSize = 11; reset.Padding = new Thickness(0); reset.HorizontalAlignment = HorizontalAlignment.Left; reset.Margin = new Thickness(0); reset.Click += delegate { Surface.Reset(); SelectRatio(0); }; footer.Children.Add(reset);
            var apply = Theme.Button("Apply", true); apply.Padding = new Thickness(18, 5, 18, 5); apply.MinHeight = 26; apply.FontSize = 11; apply.HorizontalAlignment = HorizontalAlignment.Right; apply.VerticalAlignment = VerticalAlignment.Center; apply.Margin = new Thickness(0); footer.Children.Add(apply);
            apply.Click += delegate {
                try { var crop = Surface.Crop(); string extension = System.IO.Path.GetExtension(path).ToLowerInvariant(); Output = ImageEdits.Save(new ImageState { Source = crop }, path, extension == ".jpg" || extension == ".jpeg" ? "JPG" : "PNG"); DialogResult = true; }
                catch (Exception e) { MessageBox.Show(this, e.Message, "Crop Image"); }
            };
            Grid.SetRow(footer, 4); root.Children.Add(footer); Surface.Changed = Fields; Fields();
            PreviewKeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) Close(); };
            Ui.Attach(this, delegate { glass.Background = new LinearGradientBrush((Color)ColorConverter.ConvertFromString(Ui.Dark ? "#F22B3038" : "#F2FFD17F"), (Color)ColorConverter.ConvertFromString(Ui.Dark ? "#F2181B20" : "#F2FF9E88"), 90); });
        }
        void PrepareField(TextBox field) { field.Width = 60; field.Height = 24; field.Padding = new Thickness(6, 2, 2, 2); field.FontSize = 11; field.BorderThickness = new Thickness(0); field.Background = Theme.Brush("#DFFFFFFF"); field.Foreground = Theme.Ink; field.VerticalContentAlignment = VerticalAlignment.Center; }
        void SelectRatio(int index) { for (int i = 0; i < ratios.Count; i++) { ratios[i].Background = i == index ? Theme.Accent : Theme.Brush("#22A25931"); ratios[i].Foreground = i == index ? Brushes.White : Theme.Brush("#694B32"); } }
        void Fields() { updating = true; width.Text = Math.Max(1, (int)Math.Round(Surface.Selection.Width)).ToString(); height.Text = Math.Max(1, (int)Math.Round(Surface.Selection.Height)).ToString(); updating = false; }
        void Dimensions(bool widthChanged) { if (updating) return; double w, h; if (double.TryParse(width.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out w) && double.TryParse(height.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out h) && w > 0 && h > 0) Surface.SetDimensions(w, h, widthChanged); else Fields(); }
        public void ExportPreview(string file)
        {
            Fields(); Ui.Apply(this); Show(); UpdateLayout(); var bitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(this); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var output = File.Create(file)) encoder.Save(output); Close();
        }
    }
}
