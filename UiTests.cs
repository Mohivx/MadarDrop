using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TangerineWindows
{
    public static class UiTests
    {
        static string Pixels(BitmapSource bitmap) { var b = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0); byte[] pixels = new byte[b.PixelWidth * b.PixelHeight * 4]; b.CopyPixels(pixels, b.PixelWidth * 4, 0); using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(pixels)); }
        public static int Run(string root, string sample)
        {
            var report = new List<string>(); Action<bool, string> check = (ok, text) => { if (!ok) throw new Exception(text); report.Add("PASS " + text); };
            try {
                if (Directory.Exists(root)) throw new IOException("Use a new UI test folder."); Directory.CreateDirectory(root); Ui.TestStore(Path.Combine(root, "preferences.json"));
                Ui.SetLanguage("en"); Ui.SetDark(false);
                var state = new ImageState { Source = ImageEdits.Load(sample) }; string before = Pixels(ImageEdits.Render(state, 0));
                var crop = new CropWindow(sample); check(crop.Title == "Crop Image", "English labels are available");
                Ui.SetLanguage("ar"); check(crop.Title == "قص الصورة" && Ui.T("Aspect ratio") == "نسبة القص", "Open windows switch to Arabic immediately");
                Ui.SetLanguage("en"); check(crop.Title == "Crop Image", "Switching back restores original English text");
                Ui.SetLanguage("ar"); Ui.SetDark(true); check(((SolidColorBrush)Theme.Ink).Color.R > 200 && ((SolidColorBrush)Theme.Surface).Color.R < 60, "Dark appearance uses readable light text on a dark surface");
                crop.Surface.Selection = new Rect(0, 0, state.Source.PixelWidth, state.Source.PixelHeight * .58); crop.ExportPreview(Path.Combine(root, "crop-ar-dark.png"));
                check(Pixels(ImageEdits.Render(state, 0)) == before, "Changing language and theme never alters exported image pixels");
                Ui.Preferences = new UiPreferences { Language = "en", Dark = false }; Ui.Reload(); check(Ui.Arabic && Ui.Dark, "Language and appearance survive a settings reload");
                var choices = Catalog.Choices(new[] { sample }, 1).Take(7).ToArray(); check(choices.Select(c => c.Id).SequenceEqual(new[] { "compress", "metadata", "edit", "annotate", "background", "crop-editor", "redact" }) && choices[5].Label == "قص", "Arabic tool wheel preserves stable actions and reference ordering");
                var ring = new Radial(); ring.Configure(new[] { sample }, 1); check(ring.Items.Count == 7 && ring.HasMore && ring.ChoiceAt(new Point(184, 176)).Id == "more", "Seven tool sectors match the reference; the center opens additional tools");
                var mainAr = new MainWindow(true); mainAr.RenderTo(root); File.Move(Path.Combine(root, "tangerine-windows-preview.png"), Path.Combine(root, "main-ar-dark.png"));
                var taskEditor = new ImageEditor(sample, "redact"); taskEditor.FocusTool("redact"); check(taskEditor.ToolOnly && taskEditor.Title == "طمس الصورة", "Wheel tools open task-specific windows without unrelated tabs");
                var combos = Descendants(taskEditor).OfType<System.Windows.Controls.ComboBox>().ToArray(); check(combos.Length >= 2 && combos.All(c => c.Background is SolidColorBrush && ((SolidColorBrush)c.Background).Color.R < 60 && object.ReferenceEquals(c.Style, taskEditor.Resources[typeof(System.Windows.Controls.ComboBox)])), "Editor format and tool selectors use the dark appearance correctly"); taskEditor.ExportPreview(root);
                var settings = new MainWindow(true); settings.RenderTo(root, 2); File.Move(Path.Combine(root, "tangerine-windows-preview.png"), Path.Combine(root, "settings-ar-dark.png"));
                ring.Hover = 5; ring.Measure(new Size(368, 368)); ring.Arrange(new Rect(0, 0, 368, 368)); ring.UpdateLayout();
                var image = new RenderTargetBitmap(368, 368, 96, 96, PixelFormats.Pbgra32); image.Render(ring); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using (var output = File.Create(Path.Combine(root, "tools-ar-dark.png"))) encoder.Save(output);
                Ui.SetLanguage("en"); Ui.SetDark(false); var light = new CropWindow(sample); light.Surface.Selection = new Rect(0, 0, state.Source.PixelWidth, state.Source.PixelHeight * .58); light.ExportPreview(Path.Combine(root, "crop-en-light.png"));
                var mainEn = new MainWindow(true); mainEn.RenderTo(root); File.Move(Path.Combine(root, "tangerine-windows-preview.png"), Path.Combine(root, "main-en-light.png"));
                Ui.SetLanguage("ar"); var mainArLight = new MainWindow(true); mainArLight.RenderTo(root); File.Move(Path.Combine(root, "tangerine-windows-preview.png"), Path.Combine(root, "main-ar-light.png"));
                ring.Configure(new[] { sample }, 1); ring.Hover = 5; ring.Measure(new Size(368, 368)); ring.Arrange(new Rect(0, 0, 368, 368)); ring.UpdateLayout();
                var lightRing = new RenderTargetBitmap(368, 368, 96, 96, PixelFormats.Pbgra32); lightRing.Render(ring); var lightEncoder = new PngBitmapEncoder(); lightEncoder.Frames.Add(BitmapFrame.Create(lightRing)); using (var output = File.Create(Path.Combine(root, "tools-ar-light.png"))) lightEncoder.Save(output);
                check(File.Exists(Path.Combine(root, "main-ar-dark.png")) && File.Exists(Path.Combine(root, "main-en-light.png")), "Both language and appearance combinations render successfully");
                report.Add("RESULT: " + report.Count + " language/appearance checks passed."); File.WriteAllLines(Path.Combine(root, "ui-test-results.txt"), report, Encoding.UTF8); return 0;
            } catch (Exception e) { report.Add("FAIL " + e); Directory.CreateDirectory(root); File.WriteAllLines(Path.Combine(root, "ui-test-results.txt"), report, Encoding.UTF8); return 1; }
        }
        static IEnumerable<DependencyObject> Descendants(DependencyObject root) { yield return root; foreach (object child in LogicalTreeHelper.GetChildren(root)) if (child is DependencyObject) foreach (var item in Descendants((DependencyObject)child)) yield return item; }
    }
}
