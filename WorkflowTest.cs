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
                main.Close(); report.Add("RESULT: " + report.Count + " workflow checks passed."); File.WriteAllLines(Path.Combine(root, "workflow-test-results.txt"), report, Encoding.UTF8); return 0;
            } catch (Exception e) { report.Add("FAIL " + e); Directory.CreateDirectory(root); File.WriteAllLines(Path.Combine(root, "workflow-test-results.txt"), report, Encoding.UTF8); return 1; }
        }
    }
}
