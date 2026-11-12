using System;
using System.Collections.Generic;
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
        private readonly HashSet<string> _native120ApprovedDevices =
            new HashSet<string>(StringComparer.Ordinal);

        private sealed class ExperimentalFpsOption
        {
            public override string ToString() { return StoreUiText.Get("ExperimentalFpsOption", "120 (Experimental)"); }
        }

        private void MaxFpsSelectionChanged(object sender, EventArgs e)
        {
            if (_loadingRunSettings) return;
            var next = GetSelectedMaxFps();
            if (_selectedMode == 0)
                _native120ApprovedDevices.Remove(_selectedDeviceIdentity ?? string.Empty);
            if(_selectedMode==0&&FrameRateRules.RequiresScreenOn(next)&&!PrepareNative120Transition()){
                _loadingRunSettings=true;
                try{_maxFpsBox.SelectedItem=_displayedMaxFps==120?_maxFpsBox.Items[2]:(object)_displayedMaxFps;}
                finally{_loadingRunSettings=false;}
                return;
            }
            if (_selectedMode > 0 && FrameRateRules.RequiresScreenOn(next))
                ShowNativeSingleWindow120Notice();
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
            MarkRunSettingsDirty();
        }

        private bool PrepareNative120Transition(DeviceRunSettingsProfile target=null)
        {
            if(_exitInProgress||IsDisposed||Disposing||_experimentalFpsDialogOpen)return false;
            var identity=target==null?_selectedDeviceIdentity:target.DeviceIdentity;
            var profile=target??GetSelectedDeviceRunSettings();
            if(profile.VirtualDisplay.ActivationMode!=DexDisplayMode.Native)return true;
            var approvalKey=identity??string.Empty;
            bool remember=_settings.Features.AutoSwitchToLegacyFor120Fps;
            if(remember||_native120ApprovedDevices.Contains(approvalKey))return true;
            if(!remember){
                if(_experimentalFpsDialogOpen||!IsHandleCreated)return false;
                _experimentalFpsDialogOpen=true;
                try{
                if(!Visible||WindowState==FormWindowState.Minimized)RequestWindowActivation();
                using(var dialog=new DesktopDialog(LocalizationService.Get("Fps.NativeTransitionTitle"),LocalizationService.Get("Fps.NativeTransitionBody"),false,MessageBoxButtons.OKCancel,MessageBoxDefaultButton.Button2,LocalizationService.Get("Fps.NativeTransitionRemember"))){
                    dialog.SetPrimaryButtonText(LocalizationService.Get("Fps.NativeTransitionAccept"));
                    if(dialog.ShowDialog(this)!=DialogResult.OK||_exitInProgress||IsDisposed)return false;
                    remember=dialog.OptionChecked;
                }
                }catch(Exception error){_logService.Error("120 FPS transition confirmation could not be shown; change cancelled.",error);return false;}
                finally{_experimentalFpsDialogOpen=false;}
            }
            if(target==null&&!string.Equals(identity,_selectedDeviceIdentity,StringComparison.Ordinal))return false;
            try{
                if(_settings.Features.AutoSwitchToLegacyFor120Fps!=remember)
                    _settingsService.UpdateAndSave(_settings,s=>s.Features.AutoSwitchToLegacyFor120Fps=remember);
                _native120ApprovedDevices.Add(approvalKey);
                return true;
            }catch(Exception error){_logService.Error("120 FPS transition preference could not be saved.",error);return false;}
        }
        private DeviceRunSettingsProfile PrepareNative120Start(DeviceRunSettingsProfile profile)
        {
            // Only the launch snapshot changes backend. Never mutate a saved phone profile.
            var launch=new DeviceRunSettingsProfile {
                DeviceIdentity=profile.DeviceIdentity,
                VirtualDisplay=AppSettings.CloneVirtualDisplay(profile.VirtualDisplay),
                Scrcpy=AppSettings.CloneScrcpy(profile.Scrcpy)
            };
            if(FrameRateRules.RequiresScreenOn(launch.Scrcpy.MaxFps)&&launch.VirtualDisplay.ActivationMode==DexDisplayMode.Native){
                if(!PrepareNative120Transition(profile))return null;
                launch.VirtualDisplay.ActivationMode=DexDisplayMode.LegacyOverlay;
                _logService.Progress(LocalizationService.Get("Log.Fps.TemporaryLegacy"));
            }
            return launch;
        }

        private static bool HasActiveNativeDisplay(DexOrchestrator orchestrator)
        {
            if(orchestrator==null)return false;
            var session=orchestrator.CurrentSession;
            if(orchestrator.IsRunning&&session!=null&&session.DisplayLease!=null&&session.DisplayLease.IsNativeDex)return true;
            var retained=orchestrator.RetainedNativeLease;
            return retained!=null&&retained.IsNativeDex&&retained.NativeStartRequested;
        }

        private void ShowNativeSingleWindow120Notice()
        {
            if(!HasActiveNativeDisplay(_orchestrator)||_experimentalFpsDialogOpen||_exitInProgress||IsDisposed||!IsHandleCreated)return;
            _experimentalFpsDialogOpen=true;
            try {
                DesktopDialogs.Show(this,LocalizationService.Get("Fps.SingleNativeLimitBody"),
                    LocalizationService.Get("Fps.SingleNativeLimitTitle"),MessageBoxButtons.OK,MessageBoxIcon.Information);
            }
            finally {_experimentalFpsDialogOpen=false;}
        }

        // All UI start/restart paths use this one gate. No device calls occur in the dialog.
        private Task<bool> ConfirmExperimentalFrameRateAsync(int fps)
        {
            return ConfirmExperimentalFrameRateCore(fps, false);
        }

        private Task<bool> ConfirmSingleWindowFrameRateAsync(int fps, DexOrchestrator orchestrator)
        {
            return ConfirmExperimentalFrameRateCore(fps, HasActiveNativeDisplay(orchestrator));
        }

        private Task<bool> ConfirmExperimentalFrameRateCore(int fps, bool nativeSingleWindowLimit)
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
                if (nativeSingleWindowLimit)
                    body = LocalizationService.Get("Fps.SingleNativeLimitBody") + Environment.NewLine + Environment.NewLine + body;
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

    }
}
