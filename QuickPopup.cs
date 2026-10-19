using System;
using System.Windows;
using Forms = System.Windows.Forms;

namespace TangerineWindows
{
    public static class QuickPopup
    {
        public static void Place(Window dialog, Native.POINT anchor)
        {
            dialog.Owner = null;
            dialog.ShowInTaskbar = false;
            dialog.WindowStartupLocation = WindowStartupLocation.Manual;
            double scale = Native.DpiAt(anchor) / 96.0;
            var area = Forms.Screen.FromPoint(new System.Drawing.Point(anchor.X, anchor.Y)).WorkingArea;
            double left = area.Left / scale, top = area.Top / scale, width = area.Width / scale, height = area.Height / scale;
            double availableWidth = Math.Max(280, width - 32), availableHeight = Math.Max(240, height - 32);
            dialog.MinWidth = Math.Min(dialog.MinWidth, availableWidth); dialog.MinHeight = Math.Min(dialog.MinHeight, availableHeight);
            dialog.MaxWidth = Math.Min(dialog.MaxWidth, availableWidth); dialog.MaxHeight = Math.Min(dialog.MaxHeight, availableHeight);
            if (!double.IsNaN(dialog.Width)) dialog.Width = Math.Min(dialog.Width, Math.Min(980, availableWidth));
            if (!double.IsNaN(dialog.Height)) dialog.Height = Math.Min(dialog.Height, Math.Min(720, availableHeight));
            double w = double.IsNaN(dialog.Width) ? Math.Min(430, availableWidth) : dialog.Width;
            double h = double.IsNaN(dialog.Height) ? Math.Min(440, availableHeight) : dialog.Height;
            dialog.Left = Math.Max(left + 16, Math.Min(anchor.X / scale - w / 2, left + width - w - 16));
            dialog.Top = Math.Max(top + 16, Math.Min(anchor.Y / scale - h * .3, top + height - h - 16));
            dialog.Loaded += delegate {
                dialog.Left = Math.Max(left + 16, Math.Min(dialog.Left, left + width - dialog.ActualWidth - 16));
                dialog.Top = Math.Max(top + 16, Math.Min(dialog.Top, top + height - dialog.ActualHeight - 16));
            };
        }
    }
}
