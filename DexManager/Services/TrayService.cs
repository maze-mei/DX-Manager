using System;
using System.Drawing;
using System.Windows.Forms;

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
            menu.Items.Add("메인 창 열기", null, delegate { showMainWindow(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("DeX 시작", null, delegate { startDex(); });
            menu.Items.Add("DeX 중지", null, delegate { stopDex(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("설정 열기", null, delegate { showSettings(); });
            menu.Items.Add("환경 점검", null, delegate { showEnvironmentCheck(); });
            menu.Items.Add("로그 보기", null, delegate { showLogs(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("프로그램 종료", null, delegate { exitApplication(); });

            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "DEX Manager",
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
