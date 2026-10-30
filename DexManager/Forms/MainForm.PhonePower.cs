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
                if (context.Runtime.Dex.RequiresPhoneScreenOn || context.Runtime.Scrcpy.RequiresPhoneScreenOn ||
                    context.Runtime.SingleWindows.RequiresPhoneScreenOn) continue;
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
                var dexSession = context.Runtime.Scrcpy
                    .GetSessionSnapshot();
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
                if (HasStayAwakeOriginalValue(serial)) continue;
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
                        StringComparison.Ordinal)) continue;
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

                    SetStayAwakeOriginalValue(serial, original);
                    PublishPhonePowerState(serial);
                    _logService.Info(DeviceLogFormatter.ForSerial(
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

        private void ReleaseStayAwakeOverride(string serial)
        {
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
                _logService.Info(DeviceLogFormatter.ForSerial(
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
            _launchCoordinator.RunExclusive(delegate
            {
                foreach (var serial in serials)
                {
                    if (!IsScreenOffRequestedForSerial(serial) &&
                        WakePhoneScreen(serial))
                    {
                        woken.Add(serial);
                    }
                }
            });
            return woken;
        }

        private bool WakePhoneScreen(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial) ||
                IsSerialMarkedDisconnected(serial)) return false;
            try
            {
                var result = _adbService.ShellForSerial(
                    serial,
                    "input keyevent 224",
                    true);
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
