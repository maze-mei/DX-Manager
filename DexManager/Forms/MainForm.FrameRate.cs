using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Services;

namespace DexManager.Forms
{
    public sealed partial class MainForm
    {
        private bool? _turnScreenOffBeforeHighFps;
        private int _displayedMaxFps = 60;
        private bool _experimentalFpsDialogOpen;

        private sealed class ExperimentalFpsOption
        {
            public override string ToString() { return StoreUiText.Get("ExperimentalFpsOption", "120 (Experimental)"); }
        }

        private void MaxFpsSelectionChanged(object sender, EventArgs e)
        {
            if (_loadingRunSettings) return;
            var next = GetSelectedMaxFps();
            if (FrameRateRules.RequiresScreenOn(next))
            {
                if (!FrameRateRules.RequiresScreenOn(_displayedMaxFps))
                    _turnScreenOffBeforeHighFps = _turnScreenOffBox.Checked;
                _turnScreenOffBox.Checked = false;
            }
            else if (FrameRateRules.RequiresScreenOn(_displayedMaxFps))
            {
                _turnScreenOffBox.Checked = _turnScreenOffBeforeHighFps ?? true;
                _turnScreenOffBeforeHighFps = null;
            }
            _displayedMaxFps = next;
            _turnScreenOffBox.Enabled = !FrameRateRules.RequiresScreenOn(next);
            UpdatePhonePreviewControls();
            MarkRunSettingsDirty();
        }

        // All UI start/restart paths use this one gate. No device calls occur in the dialog.
        private Task<bool> ConfirmExperimentalFrameRateAsync(int fps)
        {
            if (_exitInProgress || IsDisposed || Disposing) return Task.FromResult(false);
            if (!FrameRateRules.RequiresScreenOn(fps)) return Task.FromResult(true);
            if (_experimentalFpsDialogOpen || !IsHandleCreated) return Task.FromResult(false);
            _experimentalFpsDialogOpen = true;
            try
            {
                if (!Visible || WindowState == FormWindowState.Minimized) RequestWindowActivation();
                var keys = new[] { "Screen", "Risk", "Care", "Limit" };
                var body = string.Join(Environment.NewLine + Environment.NewLine,
                    keys.Select(key => StoreUiText.Get("ExperimentalFps" + key + "Warning", "")));
                using (var dialog = new DesktopDialog(
                    StoreUiText.Get("ExperimentalFpsWarningTitle", "Experimental 120 FPS"),
                    body, false, MessageBoxButtons.OKCancel, MessageBoxDefaultButton.Button2))
                {
                    dialog.SetPrimaryButtonText(StoreUiText.Get("ExperimentalFpsWarningAccept", "Start at 120 FPS"));
                    return Task.FromResult(dialog.ShowDialog(this) == DialogResult.OK &&
                        !_exitInProgress && !IsDisposed && Visible);
                }
            }
            catch (Exception exception)
            {
                _logService.Error("120 FPS confirmation could not be shown; start cancelled.", exception);
                return Task.FromResult(false);
            }
            finally { _experimentalFpsDialogOpen = false; }
        }

        private void ShowLocalUpdateNotice(bool existingInstallation, string directory)
        {
            try
            {
                var notices = new PortableUpdateNoticeService(directory);
                if (notices.ShouldShowNotice(existingInstallation))
                {
                    var text = string.Join(Environment.NewLine + Environment.NewLine,
                        new[] { "Intro", "Fps", "ScreenOff", "Scrcpy", "Companion" }
                        .Select(key => string.Format(StoreUiText.Get("UpdateNotice" + key, ""),
                            DisplayCleanupPermissionService.BundledVersionName)));
                    using (var dialog = new DesktopDialog(
                        string.Format(StoreUiText.Get("UpdateNoticeTitle", "What's new in DX Manager {0}"),
                            typeof(MainForm).Assembly.GetName().Version.ToString(3)),
                        text, false, MessageBoxButtons.OK, MessageBoxDefaultButton.Button1))
                    {
                        dialog.SetPrimaryButtonText(StoreUiText.Get("UpdateNoticeConfirm", "Got it"));
                        if (dialog.ShowDialog(this) != DialogResult.OK || _exitInProgress || !Visible) return;
                    }
                }
                string error;
                if (!notices.TryMarkCurrentVersionSeen(out error))
                    _logService.Warning("Update notice state could not be saved: " + error);
            }
            catch (Exception exception) { _logService.Error("Local update notice failed.", exception); }
        }
    }
}
