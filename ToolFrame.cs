using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace TangerineWindows
{
    public static class ToolFrame
    {
        public static Brush Glass { get { return new SolidColorBrush(Ui.Dark ? Color.FromArgb(246, 35, 37, 42) : Color.FromArgb(240, 250, 249, 246)); } }
        public static void Build(Window window, string title, UIElement body, UIElement footer) {
            window.WindowStyle = WindowStyle.None; window.AllowsTransparency = true; window.Background = Brushes.Transparent;
            var shell = new Border { Margin = new Thickness(10), CornerRadius = new CornerRadius(19), BorderBrush = Theme.Brush("#60FFFFFF"), BorderThickness = new Thickness(1), Background = Glass, Effect = new DropShadowEffect { BlurRadius = 15, ShadowDepth = 3, Opacity = .2 } };
            var grid = new Grid(); grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) }); grid.RowDefinitions.Add(new RowDefinition()); grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); shell.Child = grid;
            var bar = new Grid { Background = Brushes.Transparent, FlowDirection = FlowDirection.LeftToRight };
            var label = Theme.Text(title, 13, Theme.Ink, FontWeights.SemiBold); label.HorizontalAlignment = HorizontalAlignment.Center; label.VerticalAlignment = VerticalAlignment.Center; bar.Children.Add(label);
            var close = Theme.Button("×", false); close.Width = close.Height = 24; close.MinHeight = 24; close.Padding = new Thickness(0); close.Margin = new Thickness(12, 0, 0, 0); close.HorizontalAlignment = HorizontalAlignment.Left; close.VerticalAlignment = VerticalAlignment.Center; close.BorderThickness = new Thickness(0); close.FontSize = 17; close.Click += delegate { window.Close(); }; bar.Children.Add(close);
            bar.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { if (!close.IsMouseOver && e.LeftButton == MouseButtonState.Pressed) window.DragMove(); };
            var header = new Border { BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 0, 0, 1), Child = bar }; grid.Children.Add(header);
            Grid.SetRow(body, 1); grid.Children.Add(body);
            var bottom = new Border { BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(14, 9, 14, 9), Child = footer }; Grid.SetRow(bottom, 2); grid.Children.Add(bottom);
            window.Content = shell;
            EventHandler refresh = delegate { shell.Background = Glass; header.BorderBrush = bottom.BorderBrush = Theme.Line; }; Ui.Changed += refresh; window.Closed += delegate { Ui.Changed -= refresh; }; Ui.Attach(window);
        }
    }
    public sealed class FloatingJob : Window
    {
        readonly TextBlock file = Theme.Text("", 11, Theme.Muted), detail = Theme.Text("", 11, Theme.Muted);
        readonly ProgressBar progress = new ProgressBar { Height = 4, Foreground = Theme.Accent, Margin = new Thickness(0, 7, 0, 0) };
        readonly Button close;
        readonly Action cancel;
        bool complete, cancelling;
        int total = 1, current;
        string result;
        readonly DispatcherTimer dismiss = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        public FloatingJob(string title, Action cancelAction) {
            Title = title; cancel = cancelAction; Width = 350; Height = 116; MinWidth = 290; MinHeight = 105; ShowInTaskbar = false; ShowActivated = false; Topmost = true; ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            file.TextWrapping = TextWrapping.NoWrap; file.TextTrimming = TextTrimming.CharacterEllipsis; detail.Visibility = Visibility.Collapsed; detail.MaxHeight = 60; detail.TextTrimming = TextTrimming.CharacterEllipsis;
            var template = new ControlTemplate(typeof(ProgressBar)); var trackGrid = new FrameworkElementFactory(typeof(Grid));
            var track = new FrameworkElementFactory(typeof(Border)); track.Name = "PART_Track"; track.SetValue(Border.BackgroundProperty, Theme.Line); track.SetValue(Border.CornerRadiusProperty, new CornerRadius(2)); trackGrid.AppendChild(track);
            var indicator = new FrameworkElementFactory(typeof(Border)); indicator.Name = "PART_Indicator"; indicator.SetValue(Border.BackgroundProperty, Theme.Accent); indicator.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left); indicator.SetValue(Border.CornerRadiusProperty, new CornerRadius(2)); trackGrid.AppendChild(indicator); template.VisualTree = trackGrid; progress.Template = template;
            var shell = new Border { Margin = new Thickness(9), Padding = new Thickness(13, 8, 13, 12), CornerRadius = new CornerRadius(15), Background = ToolFrame.Glass, BorderBrush = Theme.Brush("#60FFFFFF"), BorderThickness = new Thickness(1), Effect = new DropShadowEffect { BlurRadius = 13, ShadowDepth = 2, Opacity = .2 } }; Content = shell;
            var content = new StackPanel(); shell.Child = content;
            var header = new Grid { Background = Brushes.Transparent, FlowDirection = FlowDirection.LeftToRight, Height = 25, Margin = new Thickness(0, 0, 0, 3) };
            var label = Theme.Text(title, 12, Theme.Ink, FontWeights.SemiBold); label.Margin = new Thickness(23, 0, 0, 0); label.VerticalAlignment = VerticalAlignment.Center; header.Children.Add(label);
            close = Theme.Button("×", false); close.ToolTip = Ui.T("Cancel"); close.Width = close.Height = 20; close.MinHeight = 20; close.Padding = new Thickness(0); close.Margin = new Thickness(0); close.HorizontalAlignment = HorizontalAlignment.Left; close.VerticalAlignment = VerticalAlignment.Center; close.BorderThickness = new Thickness(0); close.Background = Brushes.Transparent; close.FontSize = 16; close.Click += delegate { Close(); }; header.Children.Add(close);
            header.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { if (!close.IsMouseOver && e.LeftButton == MouseButtonState.Pressed) DragMove(); };
            content.Children.Add(header); content.Children.Add(file); content.Children.Add(detail); content.Children.Add(progress);
            file.MouseLeftButtonUp += delegate { if (result != null) MainWindow.ShowOutput(result); };
            Closing += delegate(object s, System.ComponentModel.CancelEventArgs e) { if (!complete) { e.Cancel = true; cancelling = true; if (cancel != null) cancel(); file.Text = Ui.T("Cancelling…"); } };
            PreviewKeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
            EventHandler refresh = delegate { shell.Background = ToolFrame.Glass; }; Ui.Changed += refresh; Closed += delegate { Ui.Changed -= refresh; }; Ui.Attach(this);
            dismiss.Tick += delegate { dismiss.Stop(); Close(); }; Closed += delegate { dismiss.Stop(); };
        }
        public void Report(string filename, int item, int count, string message, double? fraction = null) {
            if (complete || cancelling || item < current) return; total = Math.Max(1, count); file.Text = filename; file.ToolTip = filename;
            double value = 100 * (item - 1 + (fraction.HasValue ? Math.Max(0, Math.Min(1, fraction.Value)) : 0)) / total;
            // An unknown (N/A) or out-of-order timestamp must not move the bar back within the same item.
            progress.Value = item == current ? Math.Max(progress.Value, value) : value; current = item;
        }
        // The caller passes the success count; after an earlier failed item that is lower than the finished item, so use whichever is further.
        public void Saved(int completed) { if (complete || cancelling) return; progress.IsIndeterminate = false; progress.Value = 100.0 * Math.Min(total, Math.Max(completed, current)) / total; }
        public static double? Fraction(string message, double duration) {
            if (message != null && message.StartsWith("Fraction ")) { double reportedFraction; return double.TryParse(message.Substring(9), System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out reportedFraction) && !double.IsNaN(reportedFraction) && !double.IsInfinity(reportedFraction) ? (double?)Math.Max(0, Math.Min(1, reportedFraction)) : null; }
            if (message != null && message.StartsWith("Page ")) { var pageParts = message.Split(' '); int done, count; if (pageParts.Length == 4 && pageParts[2] == "of" && int.TryParse(pageParts[1], out done) && int.TryParse(pageParts[3], out count) && done >= 0 && count > 0 && done <= count) return (double)done / count; return null; }
            if (duration <= 0 || message == null || !message.StartsWith("Processed ")) return null;
            // ffmpeg writes out_time as [-]H:MM:SS.ffffff with unbounded hours; TimeSpan rejects hours above 23.
            string value = message.Substring(10).Trim(); bool negative = value.StartsWith("-"); string[] parts = value.TrimStart('-').Split(':'); double hours, minutes, seconds;
            var style = System.Globalization.NumberStyles.AllowDecimalPoint; var culture = System.Globalization.CultureInfo.InvariantCulture;
            if (parts.Length != 3 || !double.TryParse(parts[0], style, culture, out hours) || !double.TryParse(parts[1], style, culture, out minutes) || !double.TryParse(parts[2], style, culture, out seconds)) return null;
            return negative ? 0 : Math.Max(0, Math.Min(1, (hours * 3600 + minutes * 60 + seconds) / duration));
        }
        public void Finish(string message, string output, bool success, int savedFiles = 0) {
            bool wasCancelled = !success && (message == "Cancelled" || message == "أُلغي");
            complete = true; progress.IsIndeterminate = false; progress.Value = success ? 100 : 0; close.ToolTip = Ui.T("Close"); result = output;
            int saved = savedFiles > 0 ? savedFiles : total;
            file.Text = success ? Ui.Arabic ? "حُفظ " + saved + " من " + saved : saved + " of " + saved + " saved" : message;
            file.ToolTip = output == null ? message : Ui.T("Show result"); if (output != null) file.Cursor = Cursors.Hand;
            if (!success && !wasCancelled) { file.Text = Ui.T("Could not complete"); detail.Text = message; detail.ToolTip = message; detail.Visibility = Visibility.Visible; Height = 182;
                if (IsVisible) { var dpi = VisualTreeHelper.GetDpi(this); var area = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle).WorkingArea; Top = Math.Max(area.Top / dpi.DpiScaleY + 12, Math.Min(Top, area.Bottom / dpi.DpiScaleY - Height - 12)); }
            }
            // The user already asked to close this job; dismiss once cancellation has finished instead of leaving a topmost popup behind.
            if (success || (cancelling && wasCancelled)) dismiss.Start();
        }
    }
}
