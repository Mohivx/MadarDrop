using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace TangerineWindows
{
    public static class Theme
    {
        public static readonly Color Orange = Color.FromRgb(28, 139, 138);
        public static readonly Brush Accent = new SolidColorBrush(Orange);
        public static Brush Ink { get { return Brush(Ui.Dark ? "#ECEFEC" : "#232624"); } }
        public static Brush Muted { get { return Brush(Ui.Dark ? "#A3AAA5" : "#7B817D"); } }
        public static Brush Line { get { return Brush(Ui.Dark ? "#3D4440" : "#E5E8E5"); } }
        public static Brush Paper { get { return Brush(Ui.Dark ? "#181A1D" : "#F5F6F3"); } }
        public static Brush Surface { get { return Brush(Ui.Dark ? "#25282D" : "#FFFFFF"); } }
        public static Brush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
        public static TextBlock Text(string text, double size, Brush color, FontWeight weight)
        {
            var block = new TextBlock { Text = text, FontSize = size, Foreground = color, FontWeight = weight, TextWrapping = TextWrapping.Wrap }; Ui.Translate(block, TextBlock.TextProperty); return block;
        }
        public static TextBlock Text(string text, double size, Brush color) { return Text(text, size, color, FontWeights.Normal); }
        public static Button Button(string title, bool primary)
        {
            var button = new Button { Content = title, Padding = new Thickness(19, 11, 19, 11), Background = primary ? Accent : Surface, Foreground = primary ? Brushes.White : Ink, BorderBrush = primary ? Accent : Line, BorderThickness = new Thickness(1), FontSize = 13, Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 8, 0), MinHeight = 38 }; Ui.Translate(button, ContentControl.ContentProperty); return button;
        }
        public static Border Card(UIElement child) { return new Border { Background = Surface, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Child = child }; }
    }

    public sealed class Radial : FrameworkElement
    {
        public List<Choice> Items = Catalog.Choices(new string[0], 0);
        public int Hover = -1;
        public string Center = "Drop a file", Caption = "Choose a format";
        public bool HasMore, HasFiles;
        int menuMode;
        public Action<Choice> Picked;
        public Radial()
        {
            Width = 368; Height = 368; Focusable = true;
            MouseMove += delegate(object s, MouseEventArgs e) { int index = At(e.GetPosition(this)); if (index != Hover) { Hover = index; InvalidateVisual(); } };
            MouseLeave += delegate { Hover = -1; InvalidateVisual(); };
            MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e) { Choice choice = ChoiceAt(e.GetPosition(this)); if (choice != null && Picked != null) Picked(choice); };
            KeyDown += delegate(object s, KeyEventArgs e) {
                if (e.Key == Key.Right || e.Key == Key.Down) { Hover = (Hover + 1) % Items.Count; e.Handled = true; InvalidateVisual(); }
                if (e.Key == Key.Left || e.Key == Key.Up) { Hover = (Hover + Items.Count - 1) % Items.Count; e.Handled = true; InvalidateVisual(); }
                if (e.Key == Key.Enter && Hover >= 0 && Picked != null) { Picked(Items[Hover]); e.Handled = true; }
            };
        }
        public int At(Point p)
        {
            double dx = p.X - 184, dy = p.Y - 176, r = Math.Sqrt(dx * dx + dy * dy);
            if (r < 54 && HasMore) return Items.Count;
            if (r < 65 || r > 160 || Items.Count == 0) return -1;
            double step = 360.0 / Items.Count, a = Math.Atan2(dy, dx) * 180 / Math.PI + 90 + step / 2;
            a = (a + 360) % 360; int i = (int)(a / step);
            double within = a % step; if (within < 1.6 || within > step - 1.6) return -1;
            return Math.Min(i, Items.Count - 1);
        }
        public Choice ChoiceAt(Point p) { int index = At(p); return index >= 0 && index < Items.Count ? Items[index] : HasMore && index == Items.Count ? new Choice("more", "MORE", "Open all file tools") : null; }
        Point Polar(double radius, double degrees) { double a = degrees * Math.PI / 180; return new Point(184 + radius * Math.Cos(a), 176 + radius * Math.Sin(a)); }
        Geometry Segment(double start, double end)
        {
            // Rounded annular sectors: curved edges, a small gap, and soft corners.
            var g = new StreamGeometry();
            using (var c = g.Open()) {
                c.BeginFigure(Polar(71, start), true, true);
                c.QuadraticBezierTo(Polar(65, start), Polar(65, start + 4), true, false);
                c.ArcTo(Polar(65, end - 4), new Size(65, 65), 0, false, SweepDirection.Clockwise, true, false);
                c.QuadraticBezierTo(Polar(65, end), Polar(71, end), true, false);
                c.LineTo(Polar(147, end), true, false);
                c.QuadraticBezierTo(Polar(158, end), Polar(158, end - 4), true, false);
                c.ArcTo(Polar(158, start + 4), new Size(158, 158), 0, false, SweepDirection.Counterclockwise, true, false);
                c.QuadraticBezierTo(Polar(158, start), Polar(147, start), true, false);
                c.LineTo(Polar(71, start), true, false);
            }
            g.Freeze(); return g;
        }
        void Label(DrawingContext dc, string text, Point point, double size, Brush brush, bool bold)
        {
            text = Ui.T(text);
            bool rtl = text.Any(c => c >= 0x0600 && c <= 0x06FF);
            var ft = new FormattedText(text, CultureInfo.GetCultureInfo(rtl ? "ar-SA" : "en-US"), rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(ft, new Point(point.X + (rtl ? ft.Width / 2 : -ft.Width / 2), point.Y - ft.Height / 2));
        }
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            dc.DrawEllipse(Theme.Brush(Ui.Dark ? "#252A31" : "#EFF2EF"), null, new Point(184, 178), 165, 165);
            dc.DrawEllipse(Theme.Brush(Ui.Dark ? "#24282D" : "#F8FAF8"), new Pen(Theme.Line, 1), new Point(184, 176), 162, 162);
            double step = 360.0 / Math.Max(1, Items.Count);
            for (int i = 0; i < Items.Count; i++) {
                bool active = i == Hover;
                var gradient = new LinearGradientBrush(active ? Theme.Orange : Ui.Dark ? Color.FromRgb(58, 64, 70) : Color.FromRgb(251, 253, 251), active ? Color.FromRgb(255, 119, 41) : Ui.Dark ? Color.FromRgb(42, 47, 53) : Color.FromRgb(235, 240, 236), 90);
                double angle = -90 + i * step;
                dc.DrawGeometry(gradient, new Pen(active ? Theme.Accent : Theme.Line, 1.4), Segment(angle - step / 2 + 1.7, angle + step / 2 - 1.7));
                bool tool = new[] { "compress", "metadata", "edit", "annotate", "background", "crop-editor", "redact" }.Contains(Items[i].Id);
                Point textAt = Polar(111, angle);
                if (tool) { Icon(dc, Items[i].Id, new Point(textAt.X, textAt.Y - 9), active ? Brushes.White : Theme.Ink); textAt.Y += 12; }
                Label(dc, Items[i].Label, textAt, tool ? Ui.Arabic ? 12 : 9.5 : Items[i].Label.Length > 7 ? 10 : 12.5, active ? Brushes.White : Theme.Ink, true);
            }
            dc.DrawEllipse(Theme.Surface, new Pen(Theme.Line, 1), new Point(184, 176), 54, 54);
            if (!HasFiles) Label(dc, "↧", new Point(184, 161), 27, Theme.Accent, false);
            string center = menuMode == 1 && HasFiles ? Center : Hover >= 0 && Hover < Items.Count ? Items[Hover].Label : Center;
            Label(dc, center.Length > 16 ? center.Substring(0, 14) + "…" : center, new Point(184, HasFiles ? 176 : 193), center.Length > 11 ? 10 : 12, Theme.Ink, false);
            string caption = HasMore && Hover == Items.Count ? Ui.T("Open all file tools") : Hover >= 0 && Hover < Items.Count ? Items[Hover].Hint : Caption;
            Label(dc, caption, new Point(184, 354), 11, Theme.Muted, false);
        }
        public void Configure(string[] paths, int mode)
        {
            Items = Catalog.Choices(paths, mode); Hover = -1;
            menuMode = mode; HasFiles = paths.Length > 0; HasMore = mode == 1 && Items.Count > 7;
            if (HasMore) Items = Items.Take(7).ToList();
            else if (Items.Count > 8) Items = Items.Take(7).Concat(new[] { new Choice("more", "MORE", "Open all file tools") }).ToList();
            long size = 0; foreach (string path in paths) try { size += new FileInfo(path).Length; } catch { }
            Center = paths.Length == 0 ? "Drop a file" : mode == 1 ? size >= 1048576 ? (size / 1048576.0).ToString("0.#") + " MB" : (size / 1024.0).ToString("0.#") + " KB" : paths.Length == 1 ? Path.GetFileName(paths[0]) : paths.Length + " files";
            Caption = mode == 0 ? "Drop on a format to convert" : mode == 1 ? "Choose a file tool" : "Choose an action";
            InvalidateVisual();
        }
        void Icon(DrawingContext dc, string id, Point center, Brush color)
        {
            var p = new Pen(color, 1.6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            dc.PushTransform(new TranslateTransform(center.X, center.Y));
            if (id == "edit") { for (int i = 0; i < 3; i++) { double y = -5 + i * 5; dc.DrawLine(p, new Point(-7, y), new Point(7, y)); dc.DrawEllipse(Theme.Surface, p, new Point(i == 1 ? -3 : 3, y), 1.8, 1.8); } }
            else if (id == "background") { dc.DrawRoundedRectangle(null, p, new Rect(-7, -6, 14, 12), 1, 1); dc.DrawEllipse(null, p, new Point(3, -2), 1.5, 1.5); dc.DrawLine(p, new Point(-6, 5), new Point(-1, -1)); dc.DrawLine(p, new Point(-1, -1), new Point(3, 4)); }
            else if (id == "crop-editor") { dc.DrawLine(p, new Point(-4, -8), new Point(-4, 4)); dc.DrawLine(p, new Point(-4, 4), new Point(8, 4)); dc.DrawLine(p, new Point(-8, -4), new Point(4, -4)); dc.DrawLine(p, new Point(4, -4), new Point(4, 8)); }
            else if (id == "annotate") { dc.DrawLine(p, new Point(-6, 6), new Point(4, -6)); dc.DrawLine(p, new Point(-3, 7), new Point(7, -5)); dc.DrawLine(p, new Point(4, -6), new Point(7, -5)); dc.DrawLine(p, new Point(-6, 6), new Point(-3, 7)); dc.DrawLine(p, new Point(-6, 6), new Point(-7, 9)); }
            else if (id == "redact") { dc.DrawEllipse(null, p, new Point(0, 0), 8, 4.5); dc.DrawEllipse(null, p, new Point(0, 0), 2.5, 2.5); dc.DrawLine(p, new Point(-6, -7), new Point(6, 7)); }
            else if (id == "metadata") { var g = new StreamGeometry(); using (var c = g.Open()) { c.BeginFigure(new Point(-7, 1), false, true); c.LineTo(new Point(1, -7), true, false); c.LineTo(new Point(7, -7), true, false); c.LineTo(new Point(7, -1), true, false); c.LineTo(new Point(-1, 7), true, false); } dc.DrawGeometry(null, p, g); dc.DrawEllipse(null, p, new Point(4, -4), 1, 1); }
            else { for (int i = 0; i < 4; i++) { dc.PushTransform(new RotateTransform(i * 90)); dc.DrawLine(p, new Point(-6, -6), new Point(-1, -1)); dc.DrawLine(p, new Point(-1, -1), new Point(-5, -1)); dc.DrawLine(p, new Point(-1, -1), new Point(-1, -5)); dc.Pop(); } }
            dc.Pop();
        }
    }

    public sealed class WheelWindow : Window
    {
        public readonly Radial Ring;
        string[] paths = new string[0]; int mode; readonly MainWindow host;
        public bool Demo;
        public WheelWindow(MainWindow main)
        {
            host = main; Width = 398; Height = 410; WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            ShowInTaskbar = false; Topmost = true; ShowActivated = false; ResizeMode = ResizeMode.NoResize; Title = "MadarDrop wheel";
            var grid = new Grid { Background = new SolidColorBrush(Color.FromArgb(1, 255, 255, 255)) };
            Ring = new Radial { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            Ring.Effect = new DropShadowEffect { BlurRadius = 15, ShadowDepth = 4, Opacity = 0.14, Color = Colors.Black };
            grid.Children.Add(Ring); Content = grid; AllowDrop = true;
            PreviewDragEnter += OnDrag; PreviewDragOver += OnDrag;
            PreviewDrop += delegate(object s, DragEventArgs e) {
                e.Handled = true; e.Effects = DragDropEffects.Copy;
                string[] dropped = ReadPaths(e.Data); int selected = Ring.At(e.GetPosition(Ring));
                Choice pick = Ring.ChoiceAt(e.GetPosition(Ring));
                Native.POINT anchor; Native.GetCursorPos(out anchor);
                Hide(); if (pick != null && dropped.Length > 0) host.Execute(dropped, pick, true, anchor);
            };
            Ring.Picked = delegate(Choice c) { if (paths.Length > 0) { Native.POINT anchor; Native.GetCursorPos(out anchor); Hide(); host.Execute(paths, c, true, anchor); } };
            PreviewKeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Escape) Hide(); };
            Ui.Attach(this, delegate { Ring.Configure(paths, mode); });
        }
        public static string[] ReadPaths(IDataObject data)
        {
            try { if (data.GetDataPresent(DataFormats.FileDrop)) return ((string[])data.GetData(DataFormats.FileDrop)).Where(File.Exists).ToArray(); } catch { }
            return new string[0];
        }
        void OnDrag(object sender, DragEventArgs e)
        {
            string[] p = ReadPaths(e.Data);
            if (!paths.SequenceEqual(p)) { paths = p; Ring.Configure(paths, mode); }
            Ring.Hover = Ring.At(e.GetPosition(Ring)); Ring.InvalidateVisual();
            e.Effects = paths.Length > 0 ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true;
        }
        public void OpenAt(Native.POINT p, int m, bool demo)
        {
            mode = m; paths = new string[0]; Demo = demo; Ring.Configure(paths, mode);
            var screen = Forms.Screen.FromPoint(new Drawing.Point(p.X, p.Y));
            // Translate physical screen pixels into this WPF window's logical coordinates.
            var dpi = Native.DpiAt(p); double scale = dpi / 96.0;
            Left = Math.Max(screen.WorkingArea.Left / scale, Math.Min((p.X / scale) - Width / 2, screen.WorkingArea.Right / scale - Width));
            Top = Math.Max(screen.WorkingArea.Top / scale, Math.Min((p.Y / scale) - Height / 2, screen.WorkingArea.Bottom / scale - Height));
            Show(); Native.NoActivate(new WindowInteropHelper(this).Handle);
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(110)));
        }
        public void ChangeMode(int m) { if (m != mode) { mode = m; Ring.Configure(paths, mode); } }
    }

    public static class Native
    {
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT point);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int height, uint flags);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int index);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int index, int value);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(POINT point, uint flags);
        [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
        public static double DpiAt(POINT p) { try { uint x, y; if (GetDpiForMonitor(MonitorFromPoint(p, 2), 0, out x, out y) == 0) return x; } catch { } return 96; }
        public static bool Down(int key) { return (GetAsyncKeyState(key) & 0x8000) != 0; }
        public static bool ExplorerForeground()
        {
            try { uint pid; GetWindowThreadProcessId(GetForegroundWindow(), out pid); using (var p = Process.GetProcessById((int)pid)) return p.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase); } catch { return false; }
        }
        public static void NoActivate(IntPtr h) { SetWindowLong(h, -20, GetWindowLong(h, -20) | 0x08000000 | 0x00000080); SetWindowPos(h, new IntPtr(-1), 0, 0, 0, 0, 0x0010 | 0x0002 | 0x0001); }
    }

    public sealed class MainWindow : Window
    {
        public readonly Radial Ring = new Radial();
        readonly StackPanel recent = new StackPanel();
        readonly TextBlock state = Theme.Text("Ready when you are", 13, Theme.Ink);
        readonly TextBlock selected = Theme.Text("No files selected", 12, Theme.Muted);
        readonly TextBlock engine = Theme.Text("All files are processed on this PC", 11, Theme.Muted);
        readonly Grid pages = new Grid();
        readonly List<UIElement> pageList = new List<UIElement>();
        readonly List<Button> nav = new List<Button>();
        readonly List<Button> modes = new List<Button>();
        readonly ProgressBar progress = new ProgressBar { Height = 3, Foreground = Theme.Accent, Visibility = Visibility.Collapsed, IsIndeterminate = true };
        readonly Button cancel = Theme.Button("Cancel", false);
        readonly DispatcherTimer watcher;
        readonly WheelWindow wheel;
        Forms.NotifyIcon tray; Drawing.Icon appIcon;
        CancellationTokenSource cancellation;
        string[] files = new string[0];
        bool quit, enabled = true, notify = true, openFolder, wasLeft, wasShift, originatedInExplorer, modal;
        Native.POINT origin;
        DateTime releasedAt = DateTime.MinValue;
        int mode = 0, completed = 0, currentPage = 0;
        public MainWindow(bool render)
        {
            Title = "MadarDrop 0.3.0"; Width = 1050; Height = 740; MinWidth = 940; MinHeight = 690;
            WindowStartupLocation = WindowStartupLocation.CenterScreen; Background = Theme.Paper; Foreground = Theme.Ink;
            FontFamily = new FontFamily("Segoe UI"); FontSize = 13;
            Resources.Add(typeof(Button), ButtonStyle());
            var root = new Grid { Background = Theme.Paper }; root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(78) }); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) }); Content = root;
            var header = new Grid { Margin = new Thickness(28, 0, 28, 0) }; header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            brand.Children.Add(OrangeMark(35)); brand.Children.Add(new TextBlock { Text = "MadarDrop", FontSize = 24, FontWeight = FontWeights.SemiBold, Margin = new Thickness(11, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            brand.Children.Add(new Border { Child = Theme.Text("WINDOWS PREVIEW", 9, Theme.Muted, FontWeights.SemiBold), Background = Theme.Brush("#E9EDE7"), Padding = new Thickness(8, 5, 8, 5), CornerRadius = new CornerRadius(6), Margin = new Thickness(12, 4, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            header.Children.Add(brand); var local = Theme.Text("●  Everything stays on your PC", 12, Theme.Brush("#5B7460")); local.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(local, 1); header.Children.Add(local); root.Children.Add(header);
            var body = new Grid { Margin = new Thickness(16, 0, 20, 0) }; body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(165) }); body.ColumnDefinitions.Add(new ColumnDefinition()); Grid.SetRow(body, 1); root.Children.Add(body);
            var sidebar = new StackPanel { Margin = new Thickness(0, 11, 16, 0) };
            sidebar.Children.Add(Theme.Text("WORKSPACE", 10, Theme.Muted, FontWeights.SemiBold));
            foreach (string label in new[] { "◉   Convert files", "◷   Activity", "⚙   Settings" }) {
                int index = nav.Count; var b = Theme.Button(label, false); b.HorizontalContentAlignment = HorizontalAlignment.Left; b.Margin = new Thickness(0, 10, 0, 0); b.Padding = new Thickness(13, 12, 4, 12); b.BorderThickness = new Thickness(0); b.FontSize = 12;
                b.Click += delegate { SetPage(index); }; nav.Add(b); sidebar.Children.Add(b);
            }
            sidebar.Children.Add(new TextBlock { Text = "SHIFT + DRAG\n\nA little shortcut.\nA lot less busywork.", FontSize = 11, Foreground = Theme.Muted, LineHeight = 18, Margin = new Thickness(11, 40, 0, 0) }); body.Children.Add(sidebar);
            Grid.SetColumn(pages, 1); body.Children.Add(pages);
            BuildConvert(); BuildActivity(); BuildSettings(); SetPage(0);
            var footer = new Grid { Margin = new Thickness(28, 0, 28, 0) }; footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            engine.VerticalAlignment = VerticalAlignment.Center; footer.Children.Add(engine); var version = Theme.Text("MadarDrop · v0.3.0", 10, Theme.Muted); version.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(version, 1); footer.Children.Add(version); Grid.SetRow(footer, 2); root.Children.Add(footer);
            wheel = new WheelWindow(this);
            watcher = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) }; watcher.Tick += Watch;
            cancel.Click += delegate { if (cancellation != null) cancellation.Cancel(); }; cancel.Visibility = Visibility.Collapsed;
            Closing += delegate(object s, CancelEventArgs e) { if (!quit && !render) { e.Cancel = true; Hide(); } };
            Closed += delegate { watcher.Stop(); wheel.Close(); if (tray != null) tray.Dispose(); if (appIcon != null) appIcon.Dispose(); if (cancellation != null) cancellation.Cancel(); };
            if (!render) { InitTray(); watcher.Start(); }
            AllowDrop = true;
            DragOver += delegate(object s, DragEventArgs e) { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
            Drop += delegate(object s, DragEventArgs e) { string[] p = WheelWindow.ReadPaths(e.Data); if (p.Length > 0) SelectFiles(p); e.Handled = true; };
            PreviewKeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Escape) wheel.Hide(); };
            Ring.Picked = delegate(Choice c) {
                if (files.Length == 0) { ChooseFiles(); return; }
                Execute(files, c);
            };
            Ui.Attach(this, delegate { SetMode(mode); SetPage(currentPage); });
        }
        public static Style ButtonStyle()
        {
            var s = new Style(typeof(Button));
            var template = new ControlTemplate(typeof(Button)); var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetBinding(FrameworkElement.HorizontalAlignmentProperty, new System.Windows.Data.Binding("HorizontalContentAlignment") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            cp.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            cp.SetBinding(FrameworkElement.MarginProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.AppendChild(cp); template.VisualTree = border; s.Setters.Add(new Setter(Control.TemplateProperty, template));
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true }; hover.Setters.Add(new Setter(UIElement.OpacityProperty, 0.82)); s.Triggers.Add(hover);
            var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false }; disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.4)); s.Triggers.Add(disabled);
            return s;
        }
        public static FrameworkElement OrangeMark(int size)
        {
            var canvas = new System.Windows.Controls.Canvas { Width = size, Height = size };
            var orbit = new System.Windows.Shapes.Ellipse { Width = size - 3, Height = size - 13, Stroke = Theme.Accent, StrokeThickness = 2.5, RenderTransform = new RotateTransform(-35, size / 2.0, size / 2.0) };
            System.Windows.Controls.Canvas.SetTop(orbit, 7); canvas.Children.Add(orbit);
            var core = new System.Windows.Shapes.Ellipse { Width = 10, Height = 10, Fill = Theme.Accent };
            System.Windows.Controls.Canvas.SetLeft(core, size / 2.0 - 5); System.Windows.Controls.Canvas.SetTop(core, size / 2.0 - 5); canvas.Children.Add(core);
            var dot = new System.Windows.Shapes.Ellipse { Width = 7, Height = 7, Fill = Theme.Accent };
            System.Windows.Controls.Canvas.SetLeft(dot, size - 9); System.Windows.Controls.Canvas.SetTop(dot, 2); canvas.Children.Add(dot);
            return canvas;
        }
        void BuildConvert()
        {
            var stack = new StackPanel(); stack.Children.Add(Theme.Text("Less busywork. More flow.", 30, Theme.Ink, FontWeights.SemiBold));
            var sub = Theme.Text("Convert files right where you are. Drag, choose, and drop.", 13, Theme.Muted); sub.Margin = new Thickness(0, 7, 0, 20); stack.Children.Add(sub);
            var cardGrid = new Grid { Margin = new Thickness(24, 17, 20, 20) }; cardGrid.ColumnDefinitions.Add(new ColumnDefinition()); cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(368) });
            var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
            var dropIcon = Theme.Text("↧", 47, Theme.Accent); dropIcon.Margin = new Thickness(0, 0, 0, 10); left.Children.Add(dropIcon);
            left.Children.Add(Theme.Text("Your files.\nA new format.", 25, Theme.Ink, FontWeights.SemiBold));
            var desc = Theme.Text("Drop files here, or choose them below. Your original files stay safe.", 12, Theme.Muted); desc.LineHeight = 20; desc.Margin = new Thickness(0, 12, 8, 19); left.Children.Add(desc);
            var choose = Theme.Button("Choose files…", true); choose.HorizontalAlignment = HorizontalAlignment.Left; choose.Click += delegate { ChooseFiles(); }; left.Children.Add(choose);
            selected.Margin = new Thickness(0, 14, 0, 0); selected.MaxWidth = 220; selected.HorizontalAlignment = HorizontalAlignment.Left; left.Children.Add(selected);
            var clear = Theme.Button("Clear selection", false); clear.FontSize = 10; clear.Padding = new Thickness(8, 4, 8, 4); clear.MinHeight = 23; clear.HorizontalAlignment = HorizontalAlignment.Left; clear.Margin = new Thickness(0, 9, 0, 0); clear.Click += delegate { SelectFiles(new string[0]); }; left.Children.Add(clear);
            cardGrid.Children.Add(left);
            var right = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            var tabs = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 2) };
            foreach (string title in new[] { "Convert", "Tools", "Actions" }) { int i = modes.Count; var b = Theme.Button(title, false); b.Padding = new Thickness(14, 7, 14, 7); b.MinHeight = 30; b.FontSize = 11; b.Margin = new Thickness(3, 0, 3, 0); b.Click += delegate { SetMode(i); }; modes.Add(b); tabs.Children.Add(b); }
            right.Children.Add(tabs); right.Children.Add(Ring); Grid.SetColumn(right, 1); cardGrid.Children.Add(right); stack.Children.Add(Theme.Card(cardGrid));
            var statusArea = new StackPanel { Margin = new Thickness(3, 15, 3, 0) }; var statusGrid = new Grid(); statusGrid.ColumnDefinitions.Add(new ColumnDefinition()); statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            state.VerticalAlignment = VerticalAlignment.Center; statusGrid.Children.Add(state); Grid.SetColumn(cancel, 1); statusGrid.Children.Add(cancel); statusArea.Children.Add(statusGrid); progress.Margin = new Thickness(0, 9, 0, 0); statusArea.Children.Add(progress);
            var hint = Theme.Text("In File Explorer: drag a file, then press Shift.\nShift + Alt opens tools · Shift + Ctrl opens actions · Esc cancels", 11, Theme.Muted); hint.LineHeight = 19; hint.Margin = new Thickness(0, 10, 0, 0); statusArea.Children.Add(hint); stack.Children.Add(statusArea);
            pageList.Add(stack); pages.Children.Add(stack); SetMode(0);
        }
        void BuildActivity()
        {
            var stack = new StackPanel(); stack.Children.Add(Theme.Text("A little history.", 30, Theme.Ink, FontWeights.SemiBold));
            var text = Theme.Text("Results from this session. Click an output to show it in its folder.", 13, Theme.Muted); text.Margin = new Thickness(0, 8, 0, 22); stack.Children.Add(text);
            recent.Children.Add(Theme.Text("Your completed files will appear here.", 13, Theme.Muted));
            stack.Children.Add(new ScrollViewer { Content = recent, MaxHeight = 450, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); pageList.Add(stack); pages.Children.Add(stack);
        }
        void BuildSettings()
        {
            var stack = new StackPanel(); stack.Children.Add(Theme.Text("Make it yours.", 30, Theme.Ink, FontWeights.SemiBold));
            var text = Theme.Text("A few small preferences for this session.", 13, Theme.Muted); text.Margin = new Thickness(0, 8, 0, 23); stack.Children.Add(text);
            var prefs = new StackPanel { Margin = new Thickness(24) };
            prefs.Children.Add(Theme.Text("Language", 12, Theme.Ink, FontWeights.SemiBold));
            var languages = new ComboBox { Padding = new Thickness(8), Margin = new Thickness(0, 7, 0, 14), Width = 210, HorizontalAlignment = HorizontalAlignment.Left };
            var ar = new ComboBoxItem { Content = "العربية", Tag = "ar" }; var en = new ComboBoxItem { Content = "English", Tag = "en" }; Ui.Preserve(ar); Ui.Preserve(en); languages.Items.Add(ar); languages.Items.Add(en); languages.SelectedIndex = Ui.Arabic ? 0 : 1;
            languages.SelectionChanged += delegate { Ui.SetLanguage((string)((ComboBoxItem)languages.SelectedItem).Tag); }; prefs.Children.Add(languages);
            prefs.Children.Add(Theme.Text("Appearance", 12, Theme.Ink, FontWeights.SemiBold));
            var appearance = new ComboBox { Padding = new Thickness(8), Margin = new Thickness(0, 7, 0, 18), Width = 210, HorizontalAlignment = HorizontalAlignment.Left };
            appearance.Items.Add(new ComboBoxItem { Content = "Light", Tag = false }); appearance.Items.Add(new ComboBoxItem { Content = "Dark", Tag = true }); appearance.SelectedIndex = Ui.Dark ? 1 : 0;
            appearance.SelectionChanged += delegate { Ui.SetDark((bool)((ComboBoxItem)appearance.SelectedItem).Tag); }; prefs.Children.Add(appearance);
            var global = new CheckBox { Content = "Show the wheel with Shift while dragging in File Explorer", IsChecked = true, Margin = new Thickness(0, 0, 0, 21) }; global.Checked += delegate { enabled = true; }; global.Unchecked += delegate { enabled = false; wheel.Hide(); }; prefs.Children.Add(global);
            var notifications = new CheckBox { Content = "Show a notification when conversion finishes", IsChecked = true, Margin = new Thickness(0, 0, 0, 21) }; notifications.Checked += delegate { notify = true; }; notifications.Unchecked += delegate { notify = false; }; prefs.Children.Add(notifications);
            var folder = new CheckBox { Content = "Open the output folder after conversion", IsChecked = false, Margin = new Thickness(0, 0, 0, 22) }; folder.Checked += delegate { openFolder = true; }; folder.Unchecked += delegate { openFolder = false; }; prefs.Children.Add(folder);
            var media = Theme.Button("Set up media tools…", false); media.Click += delegate { MediaTools.Setup(this); }; prefs.Children.Add(media);
            var updates = Theme.Button("Check for updates…", false); updates.Click += delegate { UpdateCheck.Check(this); }; prefs.Children.Add(updates);
            prefs.Children.Add(Theme.Text("Formats in this preview", 15, Theme.Ink, FontWeights.SemiBold));
            var formats = Theme.Text("Images · Video · Audio · Image-to-PDF · Subtitles\nText & DOCX · CSV / TSV / XLSX values · ZIP archives", 12, Theme.Muted); formats.Margin = new Thickness(0, 8, 0, 20); formats.LineHeight = 23; prefs.Children.Add(formats);
            prefs.Children.Add(Theme.Text("NEW: Photo editor, annotations, redaction, backgrounds,\nmetadata, collage, and PDF page editing.\nPDF-to-Office is still in development.\nThis independent prototype is not affiliated with Tangerine for Mac.", 11, Theme.Muted));
            stack.Children.Add(Theme.Card(prefs));
            var quitButton = Theme.Button("Quit MadarDrop", false); quitButton.HorizontalAlignment = HorizontalAlignment.Left; quitButton.Margin = new Thickness(0, 18, 0, 0); quitButton.Click += delegate { Quit(); }; stack.Children.Add(quitButton);
            var scroll = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; pageList.Add(scroll); pages.Children.Add(scroll);
        }
        void SetPage(int index)
        {
            currentPage = index;
            for (int i = 0; i < pageList.Count; i++) pageList[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
            for (int i = 0; i < nav.Count; i++) { nav[i].Background = i == index ? Theme.Brush(Ui.Dark ? "#472D22" : "#FDE8DB") : Brushes.Transparent; nav[i].Foreground = i == index ? Theme.Accent : Theme.Muted; }
        }
        void SetMode(int m)
        {
            mode = m; Ring.Configure(files, mode);
            for (int i = 0; i < modes.Count; i++) { modes[i].Background = i == mode ? Theme.Accent : Theme.Surface; modes[i].Foreground = i == mode ? Brushes.White : Theme.Muted; }
        }
        void ChooseFiles()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Choose files to convert", Multiselect = true, CheckFileExists = true };
            if (dialog.ShowDialog(this) == true) SelectFiles(dialog.FileNames);
        }
        public void SelectFiles(string[] p)
        {
            files = p; selected.Text = files.Length == 0 ? "No files selected" : files.Length == 1 ? Path.GetFileName(files[0]) : files.Length + " files selected";
            Ring.Configure(files, mode); SetPage(0);
            Ui.Apply(this);
        }
        public void Reveal() { Show(); if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal; Activate(); }
        void InitTray()
        {
            using (var bmp = new Drawing.Bitmap(40, 40)) {
                using (var g = Drawing.Graphics.FromImage(bmp)) {
                    g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias; g.Clear(Drawing.Color.Transparent);
                    using (var b = new Drawing.SolidBrush(Drawing.Color.FromArgb(28, 139, 138))) g.FillEllipse(b, 2, 2, 36, 36);
                    using (var pen = new Drawing.Pen(Drawing.Color.White, 2.2f)) g.DrawEllipse(pen, 8, 13, 24, 14);
                    g.FillEllipse(Drawing.Brushes.White, 16, 16, 8, 8); g.FillEllipse(Drawing.Brushes.White, 29, 10, 5, 5);
                }
                IntPtr handle = bmp.GetHicon(); using (var icon = Drawing.Icon.FromHandle(handle)) appIcon = (Drawing.Icon)icon.Clone(); Native.DestroyIcon(handle);
            }
            using (var stream = new MemoryStream()) { appIcon.Save(stream); stream.Position = 0; Icon = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad); }
            tray = new Forms.NotifyIcon { Icon = appIcon, Text = "MadarDrop — Shift + drag to convert", Visible = true };
            var menu = new Forms.ContextMenuStrip(); var openItem = menu.Items.Add(Ui.T("Open MadarDrop"), null, delegate { Dispatcher.Invoke(new Action(Reveal)); });
            var pauseItem = menu.Items.Add(Ui.T("Pause / resume wheel"), null, delegate { Dispatcher.Invoke(new Action(delegate { enabled = !enabled; if (!enabled) wheel.Hide(); tray.Text = Ui.T(enabled ? "MadarDrop — Shift + drag to convert" : "MadarDrop — wheel paused"); })); });
            menu.Items.Add(new Forms.ToolStripSeparator()); var quitItem = menu.Items.Add(Ui.T("Quit"), null, delegate { Dispatcher.Invoke(new Action(Quit)); });
            EventHandler updateMenu = delegate { openItem.Text = Ui.T("Open MadarDrop"); pauseItem.Text = Ui.T("Pause / resume wheel"); quitItem.Text = Ui.T("Quit"); menu.RightToLeft = Ui.Arabic ? Forms.RightToLeft.Yes : Forms.RightToLeft.No; tray.Text = Ui.T(enabled ? "MadarDrop — Shift + drag to convert" : "MadarDrop — wheel paused"); };
            Ui.Changed += updateMenu; Closed += delegate { Ui.Changed -= updateMenu; }; updateMenu(null, EventArgs.Empty);
            tray.ContextMenuStrip = menu; tray.DoubleClick += delegate { Dispatcher.Invoke(new Action(Reveal)); };
        }
        void Quit() { quit = true; if (cancellation != null) cancellation.Cancel(); Close(); Application.Current.Shutdown(); }
        void Watch(object sender, EventArgs e)
        {
            if (Program.RevealSignal != null && Program.RevealSignal.WaitOne(0)) Reveal();
            bool left = Native.Down(1); Native.POINT p; Native.GetCursorPos(out p);
            if (left && !wasLeft) { origin = p; originatedInExplorer = Native.ExplorerForeground(); }
            if (!left && wasLeft) releasedAt = DateTime.UtcNow;
            if (Native.Down(0x1B)) { wheel.Hide(); originatedInExplorer = false; }
            if (enabled && !modal && cancellation == null && left && originatedInExplorer && Native.Down(0x10) && !wheel.IsVisible && (Math.Abs(p.X - origin.X) > 8 || Math.Abs(p.Y - origin.Y) > 8)) {
                int m = Native.Down(0x11) ? 2 : Native.Down(0x12) ? 1 : 0; wheel.OpenAt(p, m, false);
            }
            if (left && wheel.IsVisible) {
                if (Native.Down(0x11)) wheel.ChangeMode(2);
                else if (Native.Down(0x12)) wheel.ChangeMode(1);
                else if (Native.Down(0x10) && !wasShift) wheel.ChangeMode(0);
            }
            // Give OLE Drop time to arrive before hiding on mouse release.
            if (!left && wheel.IsVisible && !wheel.Demo && (DateTime.UtcNow - releasedAt).TotalMilliseconds > 200) wheel.Hide();
            wasLeft = left;
            wasShift = Native.Down(0x10);
        }
        void AddHistory(string title, string detail, string output, bool success)
        {
            if (completed == 0) recent.Children.Clear(); completed++;
            var stack = new StackPanel { Margin = new Thickness(16, 11, 16, 11) };
            stack.Children.Add(Theme.Text((success ? "✓  " : "!  ") + title, 13, success ? Theme.Ink : Theme.Brush("#B33A2E"), FontWeights.SemiBold));
            var d = Theme.Text(detail, 11, Theme.Muted); d.Margin = new Thickness(0, 5, 0, 0); stack.Children.Add(d);
            var card = Theme.Card(stack); card.Margin = new Thickness(0, 0, 0, 8);
            if (output != null) { card.Cursor = Cursors.Hand; card.MouseLeftButtonUp += delegate { ShowOutput(output); }; }
            recent.Children.Insert(0, card);
        }
        public static void ShowOutput(string output)
        {
            if (Directory.Exists(output)) Process.Start(new ProcessStartInfo("explorer.exe", Engine.Quote(output)) { UseShellExecute = true });
            else if (File.Exists(output)) Process.Start(new ProcessStartInfo("explorer.exe", "/select," + Engine.Quote(output)) { UseShellExecute = true });
        }
        void RecordEdited(string output)
        {
            if (string.IsNullOrEmpty(output)) return;
            state.Text = "Saved " + Path.GetFileName(output);
            AddHistory(Path.GetFileName(output), DateTime.Now.ToShortTimeString() + " · Edited copy · " + Path.GetDirectoryName(output), output, true);
            if (openFolder) ShowOutput(output);
            Ui.Apply(this);
        }
        bool ShowModal(Window dialog, bool quick = false, Native.POINT? anchor = null) {
            modal = true;
            try {
                if (quick) { Native.POINT p; if (anchor.HasValue) p = anchor.Value; else Native.GetCursorPos(out p); QuickPopup.Place(dialog, p); }
                else dialog.Owner = this;
                return dialog.ShowDialog() == true;
            } finally { modal = false; }
        }
        void ShowInfo(string[] p)
        {
            var sb = new StringBuilder();
            foreach (string f in p.Take(20)) { var i = new FileInfo(f); sb.AppendLine(i.Name).AppendLine("Type: " + Catalog.Kind(f) + " · " + i.Length.ToString("N0") + " bytes").AppendLine("Modified: " + i.LastWriteTime.ToString("g")).AppendLine(i.DirectoryName).AppendLine(); }
            if (p.Length > 20) sb.AppendLine("… and " + (p.Length - 20) + " more files.");
            MessageBox.Show(this, sb.ToString(), "File information", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        public async void Execute(string[] p, Choice choice, bool quick = false, Native.POINT? anchor = null)
        {
            if (MediaTools.NeedsEngine(p, choice.Id) && !MediaTools.Ready && !MediaTools.Setup(this)) return;
            if (cancellation != null) { state.Text = "A conversion is running. Cancel it or wait for it to finish."; return; }
            if (p.Length == 0) return;
            if (choice.Id == "more") {
                if (!quick) Reveal(); var picker = new ToolPicker(p);
                if (ShowModal(picker, quick, anchor) && picker.Selected != null) Execute(p, picker.Selected, quick, anchor);
                return;
            }
            if (choice.Id == "crop-editor") {
                try { var crop = new CropWindow(p[0]); if (ShowModal(crop, quick, anchor)) RecordEdited(crop.Output); }
                catch (Exception e) { MessageBox.Show(e.Message, "Crop Image"); }
                return;
            }
            if (new[] { "edit", "annotate", "redact", "background", "crop-editor", "collage" }.Contains(choice.Id)) {
                if (!quick) Reveal();
                try {
                    BitmapSource collage = null;
                    if (choice.Id == "collage") { string value = TextPrompt.Ask(quick ? null : this, "Collage columns", "2", quick ? anchor : null); int columns; if (value == null) return; if (!int.TryParse(value, out columns) || columns < 1 || columns > 12) throw new InvalidOperationException("Choose between 1 and 12 columns."); collage = ImageEdits.Collage(p, columns); }
                    var editor = new ImageEditor(p[0], choice.Id == "collage" ? "edit" : choice.Id == "crop-editor" ? "crop" : choice.Id, collage);
                    if (quick) editor.FocusTool(choice.Id);
                    if (ShowModal(editor, quick, anchor)) RecordEdited(editor.Output);
                } catch (Exception e) { MessageBox.Show(this, e.Message, "Could not open image editor", MessageBoxButton.OK, MessageBoxImage.Error); }
                return;
            }
            if (choice.Id == "pdf-edit" || choice.Id == "pdf-merge") {
                if (!quick) Reveal(); try { var editor = new PdfEditor(p); if (ShowModal(editor, quick, anchor)) RecordEdited(editor.Output); } catch (Exception e) { MessageBox.Show(e.Message, "PDF editor"); } return;
            }
            if (choice.Id == "metadata") {
                if (!quick) Reveal(); try { var editor = new MetadataEditor(p[0]); if (ShowModal(editor, quick, anchor)) RecordEdited(editor.Output); } catch (Exception e) { MessageBox.Show(e.Message, "Metadata"); } return;
            }
            if (choice.Id == "copy-path") { try { Clipboard.SetText(string.Join(Environment.NewLine, p)); state.Text = "Paths copied to clipboard"; } catch (Exception e) { state.Text = e.Message; } return; }
            if (choice.Id == "folder") { ShowOutput(p[0]); return; }
            if (choice.Id == "info") { if (!quick) Reveal(); ShowInfo(p); return; }
            var options = new Parameters();
            if (new[] { "resize", "crop", "trim", "speed" }.Contains(choice.Id)) { if (!quick) Reveal(); var dialog = new OptionsDialog(choice.Id); if (!ShowModal(dialog, quick, anchor)) return; options = dialog.Value; }
            cancellation = new CancellationTokenSource(); var ct = cancellation.Token;
            progress.Visibility = Visibility.Visible; cancel.Visibility = Visibility.Visible; Ring.IsEnabled = false;
            int success = 0, failed = 0; string lastOutput = null;
            bool grouped = choice.Id == "pdf" || choice.Id == "zip";
            string[][] batches = grouped ? new[] { p } : p.Select(f => new[] { f }).ToArray();
            try {
                for (int i = 0; i < batches.Length; i++) {
                    ct.ThrowIfCancellationRequested(); string[] batch = batches[i]; int item = i + 1;
                    state.Text = "Processing " + item + " of " + batches.Length + " · " + Path.GetFileName(batch[0]);
                    try {
                        string output = await Task.Run(() => choice.Id == "pdf-compress" ? PdfWorker.Save(batch, null, false, "compress", null, ct) : Engine.Convert(batch, choice.Id, options, ct, detail => Dispatcher.BeginInvoke(new Action(delegate { state.Text = item + " / " + batches.Length + " · " + detail; }))));
                        lastOutput = output; success++; AddHistory(Path.GetFileName(output), DateTime.Now.ToShortTimeString() + " · " + choice.Label + " · " + Path.GetDirectoryName(output), output, true);
                    } catch (OperationCanceledException) { throw; }
                    catch (Exception e) { failed++; AddHistory(Path.GetFileName(batch[0]), e.Message, null, false); }
                }
                state.Text = success + " completed" + (failed > 0 ? " · " + failed + " failed — see Activity" : " · Saved beside the original" );
                if (notify && tray != null) { tray.BalloonTipTitle = "MadarDrop"; tray.BalloonTipText = state.Text; tray.ShowBalloonTip(3000); }
                if (openFolder && lastOutput != null) ShowOutput(lastOutput);
            } catch (OperationCanceledException) { state.Text = "Cancelled · " + success + " completed files kept"; }
            finally { cancellation.Dispose(); cancellation = null; progress.Visibility = Visibility.Collapsed; cancel.Visibility = Visibility.Collapsed; Ring.IsEnabled = true; }
        }
        public void RenderTo(string folder, int page = 0)
        {
            Directory.CreateDirectory(folder); SetPage(page); Show(); UpdateLayout(); Ring.Hover = 0; Ring.InvalidateVisual(); UpdateLayout();
            var surface = (FrameworkElement)Content;
            SaveVisual(surface, Path.Combine(folder, "tangerine-windows-preview.png"), (int)surface.ActualWidth, (int)surface.ActualHeight);
            SaveVisual(Ring, Path.Combine(folder, "conversion-wheel.png"), 368, 368);
            quit = true; Close();
        }
        static void SaveVisual(Visual visual, string path, int w, int h)
        {
            var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(path)) encoder.Save(stream);
        }
    }

    public sealed class ToolPicker : Window
    {
        public Choice Selected;
        public ToolPicker(string[] files) {
            Title = "File tools"; Width = 470; Height = 620; WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Theme.Paper; FontFamily = new FontFamily("Segoe UI");
            var stack = new StackPanel { Margin = new Thickness(22) }; stack.Children.Add(Theme.Text("A tool for every little task.", 22, Theme.Ink, FontWeights.SemiBold));
            foreach (Choice choice in Catalog.Choices(files, 1)) { Choice pick = choice; var b = Theme.Button(choice.Label + "  ·  " + choice.Hint, false); b.FontSize = 11; b.Margin = new Thickness(0, 9, 0, 0); b.HorizontalContentAlignment = HorizontalAlignment.Left; b.Click += delegate { Selected = pick; DialogResult = true; }; stack.Children.Add(b); }
            Content = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Ui.Attach(this);
        }
    }

    public sealed class OptionsDialog : Window
    {
        public Parameters Value = new Parameters(); readonly List<TextBox> fields = new List<TextBox>();
        public OptionsDialog(string operation)
        {
            Title = operation.ToUpperInvariant(); Width = 380; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Theme.Paper; FontFamily = new FontFamily("Segoe UI");
            var stack = new StackPanel { Margin = new Thickness(24) }; Content = stack; stack.Children.Add(Theme.Text(operation == "crop" ? "Crop image" : operation == "resize" ? "Resize image" : operation == "trim" ? "Trim recording" : "Playback speed", 23, Theme.Ink, FontWeights.SemiBold));
            string[] labels = operation == "crop" ? new[] { "Left (pixels)", "Top (pixels)", "Width (pixels)", "Height (pixels)" } : operation == "resize" ? new[] { "Maximum width (pixels)" } : operation == "trim" ? new[] { "Start (seconds)", "Duration (seconds)" } : new[] { "Speed (0.5 to 2.0)" };
            string[] defaults = operation == "crop" ? new[] { "0", "0", "800", "600" } : operation == "resize" ? new[] { "1920" } : operation == "trim" ? new[] { "0", "10" } : new[] { "2" };
            for (int i = 0; i < labels.Length; i++) { var label = Theme.Text(labels[i], 12, Theme.Muted); label.Margin = new Thickness(0, 17, 0, 5); stack.Children.Add(label); var tb = new TextBox { Text = defaults[i], FontSize = 14, Padding = new Thickness(8), BorderBrush = Theme.Line }; fields.Add(tb); stack.Children.Add(tb); }
            var error = Theme.Text("", 11, Theme.Brush("#B33A2E")); error.Margin = new Thickness(0, 10, 0, 0); stack.Children.Add(error);
            var apply = Theme.Button("Create a copy", true); apply.Margin = new Thickness(0, 16, 0, 0); apply.IsDefault = true; stack.Children.Add(apply);
            apply.Click += delegate {
                try {
                    double[] n = fields.Select(f => double.Parse(f.Text, CultureInfo.InvariantCulture)).ToArray();
                    if (n.Any(x => double.IsNaN(x) || double.IsInfinity(x) || x < 0)) throw new FormatException();
                    if (operation == "crop" || operation == "resize") { if (n.Any(x => x > 32768 || x != Math.Floor(x))) throw new FormatException(); }
                    if (operation == "crop") { if (n[2] < 1 || n[3] < 1) throw new FormatException(); Value.X = (int)n[0]; Value.Y = (int)n[1]; Value.CropWidth = (int)n[2]; Value.CropHeight = (int)n[3]; }
                    if (operation == "resize") { if (n[0] < 1) throw new FormatException(); Value.Width = (int)n[0]; }
                    if (operation == "trim") { if (n[1] <= 0) throw new FormatException(); Value.Start = n[0]; Value.Duration = n[1]; }
                    if (operation == "speed") { if (n[0] < 0.5 || n[0] > 2) throw new FormatException(); Value.Speed = n[0]; }
                    DialogResult = true;
                } catch { error.Text = "Enter valid values within the range shown."; }
            };
            Ui.Attach(this);
        }
    }

    public static class Program
    {
        public static EventWaitHandle RevealSignal;
        [STAThread]
        public static int Main(string[] args)
        {
            AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling", false);
            AppContext.SetSwitch("Switch.System.IO.BlockLongPaths", false);
            if (args.Length > 1 && args[0] == "--test-release") return ReleaseTests.Run(args[1]);
            if (args.Length > 1 && args[0] == "--self-test") return SelfTest.Run(args[1]);
            if (args.Length > 1 && args[0] == "--test-editors") return EditorTest.Run(args[1]);
            if (args.Length > 1 && args[0] == "--test-workflow") return WorkflowTest.Run(args[1]);
            if (args.Length > 1 && args[0] == "--test-crop") return CropTests.Run(args[1]);
            if (args.Length > 2 && args[0] == "--test-ui") return UiTests.Run(args[1], args[2]);
            if (args.Length > 2 && args[0] == "--convert") {
                try { string output = Engine.Convert(args.Skip(2).ToArray(), args[1], new Parameters(), CancellationToken.None, null); File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "last-conversion.txt"), output); return 0; }
                catch (Exception e) { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "last-conversion.txt"), e.ToString()); return 1; }
            }
            bool render = args.Length > 1 && args[0] == "--render";
            if (!render) RevealSignal = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\TangerineWindowsPreview01Reveal");
            bool created; using (var mutex = new Mutex(true, "Local\\TangerineWindowsPreview01" + (render ? "Render" : ""), out created)) {
                if (!created) { RevealSignal.Set(); return 0; }
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e) { MessageBox.Show(e.Exception.Message, "MadarDrop", MessageBoxButton.OK, MessageBoxImage.Error); e.Handled = true; };
                var main = new MainWindow(render); app.MainWindow = main;
                if (render) main.Loaded += delegate { main.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(delegate { main.RenderTo(args[1]); app.Shutdown(); })); };
                if (!args.Contains("--background") || render) main.Show(); app.Run(); return 0;
            }
        }
    }
}
