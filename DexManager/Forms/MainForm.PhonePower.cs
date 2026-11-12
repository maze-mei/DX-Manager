using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;

namespace DexManager.Forms
{
    public sealed partial class MainForm : Form
    {
        private void NativeDisplayReleased(object sender, EventArgs e)
        {
            RunOnUi(delegate
            {
                if (_exitInProgress || _systemShutdownInProgress || IsDisposed) return;
                // A retained, control-only display can end without any scrcpy RunningChanged event.
                // Reuse the existing per-phone/other-window guards and the user's restore preference.
                QueueDeviceStayAwakeUpdate();
            });
        }

        private void ScrcpyService_RunningChanged(object sender, EventArgs e)
        {
            var updateSelectedUi = IsActiveRuntimeSender(sender);
            RunOnUi(delegate
            {
                HandleScrcpyRunningChanged(updateSelectedUi);
            });
        }

        private void SingleWindowService_RunningChanged(object sender, EventArgs e)
        {
            var updateSelectedUi = IsActiveRuntimeSender(sender);
            RunOnUi(delegate
            {
                HandleScrcpyRunningChanged(updateSelectedUi);
            });
        }

        private void HandleScrcpyRunningChanged(bool updateSelectedUi)
        {
            var generation = System.Threading.Interlocked.Increment(
                ref _screenOffReapplyGeneration);

            foreach (var serial in GetScreenOffSerials())
                RememberManagedSerial(serial);
            foreach (var serial in GetManagedSerials())
                PublishPhonePowerState(serial);

            if (updateSelectedUi) UpdateRunningState();
            QueueDeviceStayAwakeUpdate();
            UpdatePhoneScreenWakeSchedule();
            if (ShouldReapplyScreenOff(generation))
            {
                ScheduleScreenOffReapply(generation);
            }
        }

        private int GetManagedScrcpyCount()
        {
            var count = 0;
            foreach (var context in GetAllDeviceContexts())
            {
                count += (context.Runtime.Scrcpy.IsRunning ? 1 : 0) +
                    context.Runtime.SingleWindows.RunningCount;
            }
            return count;
        }

        private bool IsScreenOffRequested()
        {
            return GetScreenOffSerials().Count > 0;
        }

        private IList<string> GetManagedSerials()
        {
            var serials = new List<string>();
            foreach (var context in GetAllDeviceContexts())
            {
                var dexSession = context.Runtime.Scrcpy
                    .GetSessionSnapshot();
                if (dexSession.IsRunning)
                    AddSerial(serials, dexSession.Serial);
                foreach (var serial in context.Runtime.SingleWindows
                    .GetRunningSerials())
                {
                    AddSerial(serials, serial);
                }
            }
            return serials;
        }

        private IList<string> GetScreenOffSerials()
        {
            var serials = new List<string>();
            foreach (var context in GetAllDeviceContexts())
            {
                if (context.Runtime.Scrcpy.RequiresPhoneScreenOn || context.Runtime.SingleWindows.RequiresPhoneScreenOn) continue;
                var dexSession = context.Runtime.Scrcpy
                    .GetSessionSnapshot();
                if (dexSession.ScreenOffRequested)
                    AddSerial(serials, dexSession.Serial);
                foreach (var serial in context.Runtime.SingleWindows
                    .GetScreenOffSerials())
                {
                    AddSerial(serials, serial);
                }
            }
            return serials;
        }

        private static void AddSerial(IList<string> serials, string serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return;
            foreach (var existing in serials)
            {
                if (string.Equals(
                    existing,
                    serial,
                    StringComparison.OrdinalIgnoreCase)) return;
            }
            serials.Add(serial);
        }

        private void PublishPhonePowerState(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return;
            string original;
            var overrideApplied = TryGetStayAwakeOriginalValue(
                serial,
                out original);
            _runtimeSessions.SetPhonePowerState(
                serial,
                IsScreenOffRequestedForSerial(serial),
                overrideApplied,
                overrideApplied ? original : string.Empty,
                _managedSerialHistory.Contains(serial) ||
                    _deferredPhoneWakeSerials.Contains(serial) ||
                    _phoneScreenWakeInProgress.Contains(serial));
        }

        private void RememberManagedSerial(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return;
            if (IsSerialMarkedDisconnected(serial))
            {
                _deferredPhoneWakeSerials.Add(serial);
                return;
            }
            _managedSerialHistory.Add(serial);
        }

        private bool ShouldReapplyScreenOff(int generation)
        {
            return generation == System.Threading.Interlocked.CompareExchange(
                    ref _screenOffReapplyGeneration,
                    0,
                    0) &&
                System.Threading.Interlocked.CompareExchange(
                    ref _phoneScreenWakeSuppression,
                    0,
                    0) == 0 &&
                GetManagedScrcpyCount() > 0 &&
                IsScreenOffRequested();
        }

        private void ScheduleScreenOffReapply(int generation)
        {
            Task.Run(delegate
            {
                System.Threading.Thread.Sleep(750);
                if (!ShouldReapplyScreenOff(generation)) return;

                try
                {
                    foreach (var serial in GetScreenOffSerials())
                    {
                        var targetSerial = serial;
                        GetScreenOffServiceForSerial(targetSerial).Reapply(
                            targetSerial,
                            delegate
                            {
                                return ShouldReapplyScreenOff(generation) &&
                                    IsScreenOffRequestedForSerial(
                                        targetSerial);
                            });
                    }
                }
                catch (Exception ex)
                {
                    _logService.Error(
                        LocalizationService.Get(
                            "Log.Main.ScreenOffReapplyFailed"),
                        ex);
                }
            });
        }

        private bool IsScreenOffRequestedForSerial(string serial)
        {
            foreach (var candidate in GetScreenOffSerials())
            {
                if (string.Equals(
                    candidate,
                    serial,
                    StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private Task QueueDeviceStayAwakeUpdate()
        {
            lock (_stayAwakeTaskLock)
            {
                _stayAwakeUpdateTask = _stayAwakeUpdateTask.ContinueWith(
                    delegate
                    {
                        UpdateDeviceStayAwakeStateCore();
                    },
                    System.Threading.CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default);
                return _stayAwakeUpdateTask;
            }
        }

        private void UpdateDeviceStayAwakeStateCore()
        {
            var requestedSerials = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var context in GetAllDeviceContexts())
            {
                var native=context.Runtime.Dex.RetainedNativeLease;
                if(native!=null&&native.NativeStartRequested&&native.NativeStayAwakeRequested&&
                    (!native.NativeCompanionPolicyEnabled||native.NativeViewerActive))requestedSerials.Add(native.Serial);
                var dexSession = context.Runtime.Scrcpy
                    .GetSessionSnapshot();
                context.Runtime.Dex.UpdateNativeViewerState();
                if (dexSession.StayAwakeRequested)
                    requestedSerials.Add(dexSession.Serial);
                foreach (var serial in context.Runtime.SingleWindows
                    .GetStayAwakeSerials())
                {
                    requestedSerials.Add(serial);
                }
            }
            requestedSerials.RemoveWhere(string.IsNullOrWhiteSpace);

            foreach (var serial in requestedSerials)
            {
                if (IsSerialMarkedDisconnected(serial)) continue;
                var hadOriginal=HasStayAwakeOriginalValue(serial);
                try
                {
                    var originalResult = _adbService.ShellForSerial(
                        serial,
                        "settings get global stay_on_while_plugged_in",
                        false);
                    if (!originalResult.IsSuccess) continue;

                    var original = NormalizeStayAwakeValue(
                        originalResult.StandardOutput);
                    if (original == null) continue;
                    if (string.Equals(
                        original,
                        "7",
                        StringComparison.Ordinal)) {
                        var retainedOriginal=GetRetainedNativePowerOriginal(serial);
                        if(!hadOriginal&&retainedOriginal!=null&&retainedOriginal!="7") {
                            SetStayAwakeOriginalValue(serial,retainedOriginal);PublishPhonePowerState(serial);
                        }
                        continue;
                    }
                    if(!IsStayAwakeRequestedNow(serial))continue;
                    var result = _adbService.ShellForSerial(
                        serial,
                        "settings put global stay_on_while_plugged_in 7",
                        true);
                    if (!result.IsSuccess)
                    {
                        _logService.Warning(DeviceLogFormatter.ForSerial(
                            serial,
                            LocalizationService.Format(
                                "Log.Main.StayAwakeCommandFailed",
                                result.StandardError)));
                        continue;
                    }

                    var applied=_adbService.ShellForSerial(serial,"settings get global stay_on_while_plugged_in",false);
                    if(!applied.IsSuccess||NormalizeStayAwakeValue(applied.StandardOutput)!="7") {
                        _logService.Warning(DeviceLogFormatter.ForSerial(serial,"Stay awake command completed but its setting did not remain enabled; recheck deferred."));
                        continue;
                    }
                    if(!hadOriginal) {
                        var saved=GetRetainedNativePowerOriginal(serial);
                        SetStayAwakeOriginalValue(serial,saved??original);
                    }
                    PublishPhonePowerState(serial);
                    _logService.Progress(DeviceLogFormatter.ForSerial(
                        serial,
                        LocalizationService.Get(
                            "Log.Main.StayAwakeEnabled")));
                }
                catch (Exception ex)
                {
                    _logService.Error(
                        DeviceLogFormatter.ForSerial(
                            serial,
                            LocalizationService.Get(
                                "Log.Main.StayAwakeChangeFailed")),
                        ex);
                }
            }

            var releases = new List<string>();
            foreach (var serial in GetStayAwakeOriginalSerials())
            {
                if (!requestedSerials.Contains(serial)) releases.Add(serial);
            }
            foreach (var serial in releases)
            {
                if (!IsSerialMarkedDisconnected(serial))
                    ReleaseStayAwakeOverride(serial);
            }
        }

        private bool IsStayAwakeRequestedNow(string serial) {
            var identity=_runtimeSessions.ResolveIdentity(serial);
            foreach(var context in GetAllDeviceContexts()) {
                if(!string.Equals(context.Identity,identity,StringComparison.OrdinalIgnoreCase))continue;
                if(context.PendingSessionStarts>0)return true;
                var native=context.Runtime.Dex.RetainedNativeLease;
                if(native!=null&&native.NativeStartRequested&&native.NativeStayAwakeRequested&&
                    (!native.NativeCompanionPolicyEnabled||native.NativeViewerActive))return true;
                if(context.Runtime.Scrcpy.GetSessionSnapshot().StayAwakeRequested||context.Runtime.SingleWindows.GetStayAwakeSerials().Count>0)return true;
            }
            return false;
        }
        private string GetRetainedNativePowerOriginal(string serial) {
            var identity=_runtimeSessions.ResolveIdentity(serial);
            foreach(var context in GetAllDeviceContexts()) {
                var lease=context.Runtime.Dex.RetainedNativeLease;
                if(lease!=null&&lease.NativeStartRequested&&lease.NativeDeviceIdentity==identity) {
                    if(lease.NativeOriginalStayAwake==MissingStayAwakeValue)return MissingStayAwakeValue;
                    var original=NormalizeStayAwakeValue(lease.NativeOriginalStayAwake);if(original!=null)return original;
                }
            }
            return null;
        }
        private void ReleaseStayAwakeOverride(string serial)
        {
            if(IsStayAwakeRequestedNow(serial))return;
            string original;
            if (!TryGetStayAwakeOriginalValue(serial, out original))
                return;

            if (!_settings.Features.DisableStayAwakeOnStop)
            {
                RemoveStayAwakeOriginalValue(serial);
                PublishPhonePowerState(serial);
                return;
            }

            try
            {
                var currentResult = _adbService.ShellForSerial(
                    serial,
                    "settings get global stay_on_while_plugged_in",
                    false);
                if (!currentResult.IsSuccess)
                {
                    _logService.Warning(DeviceLogFormatter.ForSerial(
                        serial,
                        LocalizationService.Format(
                            "Log.Main.StayAwakeCommandFailed",
                            currentResult.StandardError)));
                    return;
                }

                var current = NormalizeStayAwakeValue(
                    currentResult.StandardOutput);
                if (!string.Equals(
                    current,
                    "7",
                    StringComparison.Ordinal))
                {
                    RemoveStayAwakeOriginalValue(serial);
                    PublishPhonePowerState(serial);
                    _logService.Warning(DeviceLogFormatter.ForSerial(
                        serial,
                        LocalizationService.Format(
                            "Log.Main.StayAwakeRestoreSkipped",
                            current ?? string.Empty)));
                    return;
                }

                if(IsStayAwakeRequestedNow(serial))return;
                var command = original == MissingStayAwakeValue
                    ? "settings delete global stay_on_while_plugged_in"
                    : "settings put global stay_on_while_plugged_in " +
                        original;
                var result = _adbService.ShellForSerial(
                    serial,
                    command,
                    true);
                if (!result.IsSuccess)
                {
                    _logService.Warning(DeviceLogFormatter.ForSerial(
                        serial,
                        LocalizationService.Format(
                            "Log.Main.StayAwakeCommandFailed",
                            result.StandardError)));
                    return;
                }

                RemoveStayAwakeOriginalValue(serial);
                PublishPhonePowerState(serial);
                _logService.Progress(DeviceLogFormatter.ForSerial(
                    serial,
                    LocalizationService.Get(
                        "Log.Main.StayAwakeDisabled")));
            }
            catch (Exception ex)
            {
                _logService.Error(
                    DeviceLogFormatter.ForSerial(
                        serial,
                        LocalizationService.Get(
                            "Log.Main.StayAwakeChangeFailed")),
                    ex);
            }
        }

        private static string NormalizeStayAwakeValue(string value)
        {
            var normalized = (value ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(normalized) ||
                string.Equals(
                    normalized,
                    "null",
                    StringComparison.OrdinalIgnoreCase))
            {
                return MissingStayAwakeValue;
            }

            int parsed;
            return int.TryParse(normalized, out parsed)
                ? parsed.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : null;
        }

        private bool TryGetStayAwakeOriginalValue(
            string serial,
            out string original)
        {
            lock (_stayAwakeValuesSync)
            {
                return _stayAwakeOriginalValues.TryGetValue(
                    serial,
                    out original);
            }
        }

        private bool HasStayAwakeOriginalValue(string serial)
        {
            lock (_stayAwakeValuesSync)
                return _stayAwakeOriginalValues.ContainsKey(serial);
        }

        private void SetStayAwakeOriginalValue(
            string serial,
            string original)
        {
            lock (_stayAwakeValuesSync)
                _stayAwakeOriginalValues[serial] = original;
        }

        private void RemoveStayAwakeOriginalValue(string serial)
        {
            lock (_stayAwakeValuesSync)
                _stayAwakeOriginalValues.Remove(serial);
        }

        private IList<string> GetStayAwakeOriginalSerials()
        {
            lock (_stayAwakeValuesSync)
                return new List<string>(_stayAwakeOriginalValues.Keys);
        }

        private void UpdatePhoneScreenWakeSchedule()
        {
            _phoneScreenWakeTimer.Stop();
            var unavailable = new List<string>();
            foreach (var serial in _managedSerialHistory)
            {
                if (IsSerialMarkedDisconnected(serial))
                    unavailable.Add(serial);
            }
            foreach (var serial in unavailable)
            {
                _managedSerialHistory.Remove(serial);
                _phoneScreenWakeInProgress.Remove(serial);
            }
            if (_managedSerialHistory.Count == 0 ||
                System.Threading.Interlocked.CompareExchange(
                    ref _phoneScreenWakeSuppression,
                    0,
                    0) > 0)
            {
                return;
            }

            _phoneScreenWakeTimer.Start();
        }

        private void PhoneScreenWakeTimer_Tick(object sender, EventArgs e)
        {
            _phoneScreenWakeTimer.Stop();
            if (System.Threading.Interlocked.CompareExchange(
                    ref _phoneScreenWakeSuppression,
                    0,
                    0) > 0)
            {
                return;
            }

            var serials = new List<string>();
            foreach (var serial in _managedSerialHistory)
            {
                if (!IsSerialMarkedDisconnected(serial) &&
                    !IsScreenOffRequestedForSerial(serial) &&
                    !_phoneScreenWakeInProgress.Contains(serial))
                {
                    AddSerial(serials, serial);
                    _phoneScreenWakeInProgress.Add(serial);
                }
            }
            if (serials.Count > 0)
            {
                Task.Run(delegate
                {
                    IList<string> woken;
                    try
                    {
                        woken = WakePhoneScreens(serials);
                    }
                    catch (Exception ex)
                    {
                        _logService.Error(
                            LocalizationService.Get(
                                "Log.Main.PhoneScreenWakeFailed"),
                            ex);
                        woken = new List<string>();
                    }
                    RunOnUi(delegate
                    {
                        foreach (var serial in serials)
                            _phoneScreenWakeInProgress.Remove(serial);
                        foreach (var serial in woken)
                            _managedSerialHistory.Remove(serial);
                        foreach (var serial in serials)
                            PublishPhonePowerState(serial);
                        _phoneScreenWakeTimer.Interval =
                            woken.Count == serials.Count ? 600 : 3000;
                        UpdatePhoneScreenWakeSchedule();
                    });
                });
            }
        }

        private IList<string> WakePhoneScreens(IList<string> serials)
        {
            var woken = new List<string>();
            var watch=System.Diagnostics.Stopwatch.StartNew();
            _logService.Diagnostic("Wake","Batch queued; targetCount="+serials.Count+"; serials="+string.Join(",",serials));
            _launchCoordinator.RunExclusive(delegate
            {
                _logService.Diagnostic("Wake","Launch gate entered; waitMs="+watch.ElapsedMilliseconds);
                foreach (var serial in serials)
                {
                    if (IsScreenOffRequestedForSerial(serial))
                    {
                        _logService.Diagnostic("Wake","Skipped serial="+serial+"; reason=another-managed-window-still-requests-screen-off");
                        continue;
                    }
                    if (WakePhoneScreen(serial))
                    {
                        woken.Add(serial);
                    }
                }
            });
            _logService.Diagnostic("Wake","Batch returned; accepted="+woken.Count+"/"+serials.Count+"; elapsedMs="+watch.ElapsedMilliseconds+
                "; physical panel state was not queried.");
            return woken;
        }

        private bool WakePhoneScreen(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial))
            {
                _logService.Diagnostic("Wake","Skipped wake; reason=empty-serial");return false;
            }
            if (IsSerialMarkedDisconnected(serial))
            {
                _logService.Diagnostic("Wake","Skipped serial="+serial+"; reason=transport-marked-disconnected");return false;
            }
            var watch=System.Diagnostics.Stopwatch.StartNew();
            _logService.Diagnostic("Wake","Sending input keyevent 224; serial="+serial+"; exitInProgress="+_exitInProgress);
            try
            {
                var result = _adbService.ShellForSerial(
                    serial,
                    "input keyevent 224",
                    true);
                _logService.Diagnostic("Wake","Command returned; serial="+serial+"; success="+result.IsSuccess+
                    "; exitCode="+result.ExitCode+"; timedOut="+result.TimedOut+"; elapsedMs="+watch.ElapsedMilliseconds+
                    "; stderr="+result.StandardError+"; success means command acceptance, not verified physical wake.");
                if (result.IsSuccess)
                {
                    _logService.Info(DeviceLogFormatter.ForSerial(
                        serial,
                        LocalizationService.Get(
                            "Log.Main.PhoneScreenWoken")));
                    return true;
                }
                else
                    _logService.Warning(DeviceLogFormatter.ForSerial(
                        serial,
                        LocalizationService.Format(
                            "Log.Main.PhoneScreenWakeCommandFailed",
                            result.StandardError)));
            }
            catch (Exception ex)
            {
                _logService.Diagnostic("Wake","Command exception; serial="+serial+"; elapsedMs="+watch.ElapsedMilliseconds+"; "+ex.GetType().Name+": "+ex.Message);
                _logService.Error(
                    DeviceLogFormatter.ForSerial(
                        serial,
                        LocalizationService.Get(
                            "Log.Main.PhoneScreenWakeFailed")),
                    ex);
            }
            return false;
        }

        private void BeginPhoneScreenWakeSuppression()
        {
            System.Threading.Interlocked.Increment(
                ref _phoneScreenWakeSuppression);
            System.Threading.Interlocked.Increment(
                ref _screenOffReapplyGeneration);
            _phoneScreenWakeTimer.Stop();
        }

        private void EndPhoneScreenWakeSuppression()
        {
            if (System.Threading.Interlocked.Decrement(
                ref _phoneScreenWakeSuppression) < 0)
            {
                System.Threading.Interlocked.Exchange(
                    ref _phoneScreenWakeSuppression,
                    0);
            }
            var generation = System.Threading.Interlocked.Increment(
                ref _screenOffReapplyGeneration);
            if (ShouldReapplyScreenOff(generation))
                ScheduleScreenOffReapply(generation);
            UpdatePhoneScreenWakeSchedule();
        }
    }
}
