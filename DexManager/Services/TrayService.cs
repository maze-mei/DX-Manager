using System;
using System.Drawing;
using System.Windows.Forms;
using DexManager.Utils;

namespace DexManager.Services
{
    public sealed class TrayService : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;

        public TrayService(
            Action showMainWindow,
            Action startDex,
            Action stopDex,
            Action showSettings,
            Action showEnvironmentCheck,
            Action showLogs,
            Action exitApplication)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add(LocalizationService.Get("Tray.Open"), null, delegate { showMainWindow(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(LocalizationService.Get("Tray.StartDex"), null, delegate { startDex(); });
            menu.Items.Add(LocalizationService.Get("Tray.StopDex"), null, delegate { stopDex(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(LocalizationService.Get("Tray.Settings"), null, delegate { showSettings(); });
            menu.Items.Add(LocalizationService.Get("Tray.Environment"), null, delegate { showEnvironmentCheck(); });
            menu.Items.Add(LocalizationService.Get("Tray.Logs"), null, delegate { showLogs(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(LocalizationService.Get("Tray.Exit"), null, delegate { exitApplication(); });

            _notifyIcon = new NotifyIcon
            {
                Icon = AppIconProvider.Current,
                Text = LocalizationService.Get("App.Name"),
                ContextMenuStrip = menu,
                Visible = true
            };
            _notifyIcon.DoubleClick += delegate { showMainWindow(); };
        }

        public void ShowBalloon(string title, string message)
        {
            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText = message;
            _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
            _notifyIcon.ShowBalloonTip(2000);
        }

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            if (_notifyIcon.ContextMenuStrip != null)
                _notifyIcon.ContextMenuStrip.Dispose();
            _notifyIcon.Dispose();
        }
    }
}
