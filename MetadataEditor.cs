using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;

namespace TangerineWindows
{
    public sealed class MetadataInfo { public Dictionary<string, string> metadata { get; set; } public string details { get; set; } }
    public sealed class MetadataEditor : Window
    {
        public string Output;
        readonly string source;
        readonly bool pdf;
        readonly Dictionary<string, TextBox> fields = new Dictionary<string, TextBox>();
        readonly TextBlock status = Theme.Text("Loading metadata…", 11, Theme.Muted);
        readonly TextBox details = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 130, FontSize = 10, Padding = new Thickness(10) };
        readonly CheckBox strip = new CheckBox { Content = "Remove existing metadata (including hidden EXIF / GPS)", Margin = new Thickness(0, 18, 0, 12) };
        readonly Button save;
        readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        public MetadataEditor(string path)
        {
            Resources.Add(typeof(Button), MainWindow.ButtonStyle());
            source = path; pdf = Catalog.Kind(path) == "pdf";
            Title = "Edit metadata · " + Path.GetFileName(path); Width = 620; Height = 750; WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Theme.Paper; FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
            var stack = new StackPanel { Margin = new Thickness(25) }; Content = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            stack.Children.Add(Theme.Text("The details behind the file.", 24, Theme.Ink, FontWeights.SemiBold));
            var hint = Theme.Text("Edit the fields below, or clear existing metadata. Save creates a new copy.", 12, Theme.Muted); hint.Margin = new Thickness(0, 8, 0, 15); stack.Children.Add(hint);
            foreach (string key in pdf ? new[] { "Title", "Author", "Subject", "Keywords" } : new[] { "Title", "Author", "Copyright", "CameraMake", "CameraModel", "Date" }) {
                stack.Children.Add(Theme.Text(key, 11, Theme.Muted)); var box = new TextBox { Padding = new Thickness(7), Margin = new Thickness(0, 4, 0, 10) }; fields[key] = box; stack.Children.Add(box);
            }
            stack.Children.Add(strip); strip.Click += delegate { if (strip.IsChecked == true) foreach (var box in fields.Values) box.Text = ""; };
            stack.Children.Add(details); status.Margin = new Thickness(0, 10, 0, 10); stack.Children.Add(status);
            save = Theme.Button("Save a copy", true); save.IsEnabled = false; stack.Children.Add(save); save.Click += Save;
            Loaded += async delegate {
                var ct = cancellation.Token;
                try {
                    var info = await Task.Run(() => new JavaScriptSerializer().Deserialize<MetadataInfo>(PdfWorker.Run(new { operation = pdf ? "metadata-info" : "image-metadata-info", files = new[] { path } }, ct)));
                    if (ct.IsCancellationRequested) return;
                    foreach (var pair in fields) { string value; if (info.metadata.TryGetValue(pair.Key, out value)) pair.Value.Text = value; }
                    details.Text = string.IsNullOrWhiteSpace(info.details) ? "No embedded metadata found." : info.details; status.Text = "Original file untouched"; save.IsEnabled = true;
                } catch (OperationCanceledException) { } catch (Exception e) { status.Text = e.Message; }
            };
            Closed += delegate { cancellation.Cancel(); cancellation.Dispose(); };
            Ui.Attach(this);
        }
        async void Save(object sender, RoutedEventArgs args)
        {
            save.IsEnabled = false; var ct = cancellation.Token; var values = fields.ToDictionary(p => p.Key, p => p.Value.Text); bool remove = strip.IsChecked == true;
            string extension = pdf || new[] { ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".webp" }.Contains(Path.GetExtension(source).ToLowerInvariant()) ? Path.GetExtension(source) : ".png";
            string temp = Path.Combine(Path.GetDirectoryName(source), ".tangerine-" + Guid.NewGuid().ToString("N") + extension);
            try {
                if (pdf) Output = await Task.Run(() => PdfWorker.Save(new[] { source }, null, remove, "metadata", values, ct));
                else {
                    await Task.Run(() => PdfWorker.Run(new { operation = "image-metadata-save", files = new[] { source }, output = temp, strip_metadata = remove, metadata = values }, ct));
                    ct.ThrowIfCancellationRequested(); Output = Engine.Publish(temp, source, extension, "metadata");
                }
                if (!ct.IsCancellationRequested) DialogResult = true;
            } catch (OperationCanceledException) { } catch (Exception e) { status.Text = e.Message; }
            finally { if (File.Exists(temp)) File.Delete(temp); if (!ct.IsCancellationRequested) save.IsEnabled = true; }
        }
    }
}
