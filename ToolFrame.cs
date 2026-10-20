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
        readonly TextBlock file = Theme.Text("", 12, Theme.Ink), detail = Theme.Text("", 11, Theme.Muted);
        readonly ProgressBar progress = new ProgressBar { Height = 5, IsIndeterminate = true, Foreground = Theme.Accent, Margin = new Thickness(0, 12, 0, 0) };
        readonly Button close, reveal;
        readonly Action cancel;
        bool complete;
        readonly DispatcherTimer dismiss = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        public FloatingJob(string title, Action cancelAction) {
            Title = title; cancel = cancelAction; Width = 410; Height = 215; MinWidth = 320; MinHeight = 180; ShowInTaskbar = false; ShowActivated = false; ResizeMode = ResizeMode.NoResize;
            file.MaxHeight = 30; file.TextTrimming = TextTrimming.CharacterEllipsis; detail.MaxHeight = 44; detail.TextTrimming = TextTrimming.CharacterEllipsis;
            var template = new ControlTemplate(typeof(ProgressBar)); var trackGrid = new FrameworkElementFactory(typeof(Grid));
            var track = new FrameworkElementFactory(typeof(Border)); track.Name = "PART_Track"; track.SetValue(Border.BackgroundProperty, Theme.Line); track.SetValue(Border.CornerRadiusProperty, new CornerRadius(2)); trackGrid.AppendChild(track);
            var indicator = new FrameworkElementFactory(typeof(Border)); indicator.Name = "PART_Indicator"; indicator.SetValue(Border.BackgroundProperty, Theme.Accent); indicator.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left); indicator.SetValue(Border.CornerRadiusProperty, new CornerRadius(2)); trackGrid.AppendChild(indicator); template.VisualTree = trackGrid; progress.Template = template;
            progress.BeginAnimation(UIElement.OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(.4, 1, TimeSpan.FromMilliseconds(650)) { AutoReverse = true, RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever });
            var content = new StackPanel { Margin = new Thickness(18, 13, 18, 10) }; content.Children.Add(file); content.Children.Add(detail); content.Children.Add(progress);
            var footer = new Grid(); close = Theme.Button("Cancel", false); close.Padding = new Thickness(12, 5, 12, 5); close.MinHeight = 26; close.HorizontalAlignment = HorizontalAlignment.Right; close.Click += delegate { Close(); }; footer.Children.Add(close);
            reveal = Theme.Button("Show result", false); reveal.Padding = new Thickness(8, 5, 8, 5); reveal.MinHeight = 26; reveal.HorizontalAlignment = HorizontalAlignment.Left; reveal.Visibility = Visibility.Collapsed; footer.Children.Add(reveal);
            ToolFrame.Build(this, title, content, footer);
            Closing += delegate(object s, System.ComponentModel.CancelEventArgs e) { if (!complete) { e.Cancel = true; if (cancel != null) cancel(); detail.Text = Ui.T("Cancelling…"); } };
            dismiss.Tick += delegate { dismiss.Stop(); Close(); }; Closed += delegate { dismiss.Stop(); };
        }
        public void Report(string filename, int item, int count, string message) { file.Text = filename; detail.Text = item + " / " + count + (string.IsNullOrEmpty(message) ? "" : " · " + message); }
        public void Finish(string message, string output, bool success) {
            complete = true; progress.IsIndeterminate = false; progress.Value = success ? 100 : 0; detail.Text = message; close.Content = Ui.T("Close");
            progress.BeginAnimation(UIElement.OpacityProperty, null); progress.Opacity = 1;
            detail.ToolTip = message;
            if (output != null) { reveal.Visibility = Visibility.Visible; reveal.Click += delegate { MainWindow.ShowOutput(output); }; }
            if (success) dismiss.Start();
        }
    }
}
