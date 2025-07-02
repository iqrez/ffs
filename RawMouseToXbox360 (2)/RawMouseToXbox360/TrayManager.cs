using System;
using System.Drawing;
using System.Windows.Forms;

namespace RawMouseToXbox360
{
    public static class TrayManager
    {
        private static NotifyIcon trayIcon;

        public static void Initialize()
        {
            trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "RawMouseToXbox360",
                Visible = true
            };

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Exit", null, (s, e) => Application.Exit());

            trayIcon.ContextMenuStrip = contextMenu;
        }

        public static void ShowBalloon(string title, string text)
        {
            if (trayIcon != null)
            {
                trayIcon.BalloonTipTitle = title;
                trayIcon.BalloonTipText = text;
                trayIcon.ShowBalloonTip(2000);
            }
        }

        public static void Dispose()
        {
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }
        }
    }
}