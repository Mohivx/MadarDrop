using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;

namespace TangerineWindows
{
    public static class WorkflowTest
    {
        public static int Run(string root)
        {
            var report = new List<string>();
            Action<bool, string> check = (ok, text) => { if (!ok) throw new Exception(text); report.Add("PASS " + text); };
            try {
                if (Directory.Exists(root)) throw new IOException("Use a new workflow test folder."); Directory.CreateDirectory(root);
                var main = new MainWindow(true); check(!main.IsVisible, "Background main window starts hidden");
                var area = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
                var anchor = new Native.POINT { X = area.Right - 10, Y = area.Bottom - 10 };
                var dialog = new Window { Width = 1160, Height = 800, MinWidth = 920, MinHeight = 650, Opacity = 0, Title = "Tangerine workflow fixture" };
                QuickPopup.Place(dialog, anchor);
                check(dialog.Owner == null && !dialog.ShowInTaskbar && dialog.WindowStartupLocation == WindowStartupLocation.Manual, "Quick editor is independent of the main window and taskbar");
                double scale = Native.DpiAt(anchor) / 96.0;
                check(dialog.Left >= area.Left / scale && dialog.Top >= area.Top / scale && dialog.Left + dialog.Width <= area.Right / scale && dialog.Top + dialog.Height <= area.Bottom / scale, "Editor near screen edge stays within the working area");
                bool visibleOnOpen = true; dialog.Loaded += delegate { visibleOnOpen = main.IsVisible; dialog.Close(); }; dialog.ShowDialog();
                check(!visibleOnOpen && !main.IsVisible, "Opening and closing a quick editor never reveals the main app");
                var automatic = new Window { Width = 380, SizeToContent = SizeToContent.Height, Content = new System.Windows.Controls.Border { Height = 610 }, Opacity = 0 };
                QuickPopup.Place(automatic, anchor); bool fits = false; automatic.Loaded += delegate { fits = automatic.Top + automatic.ActualHeight <= area.Bottom / scale && automatic.Left + automatic.ActualWidth <= area.Right / scale; automatic.Close(); }; automatic.ShowDialog();
                check(fits, "Content-sized tool dialogs also stay on screen");
                check(FloatingJob.Fraction("Processed 00:00:02.500000", 10) == .25 && !FloatingJob.Fraction("Processed 00:00:02", 0).HasValue, "Conversion progress uses timestamps without inventing a percentage for unknown duration");
                check(FloatingJob.Fraction("Processed 27:00:00.000000", 194400) == .5 && FloatingJob.Fraction("Processed -00:00:00.100000", 10) == 0, "Progress supports recordings over a day and negative encoder timestamps");
                var ordered = new FloatingJob("Processing", null); ordered.Report("first.mp4", 1, 3, "", .6);
                var bar = FindBar((System.Windows.DependencyObject)ordered.Content);
                ordered.Report("first.mp4", 1, 3, "Processed N/A"); check(Math.Abs(bar.Value - 20) < .001, "Unknown timestamps do not move progress backwards");
                ordered.Report("second.mp4", 2, 3, ""); ordered.Saved(1); ordered.Report("first.mp4", 1, 3, "", .1);
                check(Math.Abs(bar.Value - 200.0 / 3) < .001, "Completed batches advance even after a failed file and ignore older callbacks"); ordered.Finish("Cancelled", null, false); ordered.Close();
                bool cancelled = false; var job = new FloatingJob("Processing", delegate { cancelled = true; }); job.Opacity = 0; QuickPopup.Place(job, anchor); job.Show(); job.Close();
                check(job.Height <= 130 && job.ShowInTaskbar == false, "Quick conversion progress stays compact and out of the taskbar");
                check(cancelled && job.IsVisible && !main.IsVisible, "Closing floating progress requests cancellation without revealing the dashboard");
                job.Finish("Cancelled", null, false); job.Close(); check(!job.IsVisible, "Cancelled progress dismisses normally after processing stops");
                var failedJob = new FloatingJob("Processing", null); failedJob.Opacity = 0; failedJob.Show(); failedJob.Finish("Test error", null, false); check(failedJob.IsVisible, "Failed quick jobs remain visible for inspection"); failedJob.Close();
                var cancelledJob = new FloatingJob("Processing", null); cancelledJob.Opacity = 0; cancelledJob.Show(); cancelledJob.Close(); cancelledJob.Finish("Cancelled", null, false);
                var errorAfterClose = new FloatingJob("Processing", null); errorAfterClose.Opacity = 0; errorAfterClose.Show(); errorAfterClose.Close(); errorAfterClose.Finish("Actual write error", null, false);
                var frame = new System.Windows.Threading.DispatcherFrame(); var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2500) };
                timer.Tick += delegate { timer.Stop(); frame.Continue = false; }; timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
                check(!cancelledJob.IsVisible, "Cancellation requested by closing dismisses automatically after processing stops");
                check(errorAfterClose.IsVisible, "A real error stays visible even if closing had requested cancellation"); errorAfterClose.Close();
                main.Close(); report.Add("RESULT: " + report.Count + " workflow checks passed."); File.WriteAllLines(Path.Combine(root, "workflow-test-results.txt"), report, Encoding.UTF8); return 0;
            } catch (Exception e) { report.Add("FAIL " + e); Directory.CreateDirectory(root); File.WriteAllLines(Path.Combine(root, "workflow-test-results.txt"), report, Encoding.UTF8); return 1; }
        }
        static System.Windows.Controls.ProgressBar FindBar(System.Windows.DependencyObject root) {
            var bar = root as System.Windows.Controls.ProgressBar; if (bar != null) return bar;
            foreach (object child in System.Windows.LogicalTreeHelper.GetChildren(root)) { var dependency = child as System.Windows.DependencyObject; if (dependency != null) { bar = FindBar(dependency); if (bar != null) return bar; } }
            return null;
        }
    }
}
