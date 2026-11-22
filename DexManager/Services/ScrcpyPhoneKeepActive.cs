using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using DexManager.Utils;

namespace DexManager.Services
{
    internal sealed class ScrcpyPhoneKeepActive
    {
        // The bundled scrcpy server checks for activity every four seconds.
        internal const string Arguments = "--display-id=0 --no-window --no-video --no-audio " +
            "--keep-active --no-power-on --no-clipboard-autosync " +
            "--keyboard=disabled --mouse=disabled --verbosity=debug";

        private readonly object _sync = new object();
        private readonly Process _process;
        private readonly string _serial;
        private readonly LogService _log;
        private readonly TaskCompletionSource<bool> _connected =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _started;
        private bool _active;
        private bool _stopping;
        private bool _transportLost;
        private bool _closed;

        internal ScrcpyPhoneKeepActive(Process process, Process viewer, string serial, LogService log)
        {
            _process = process;
            Viewer = viewer;
            _serial = serial;
            _log = log;
        }

        internal Process Viewer { get; private set; }

        internal void Start()
        {
            lock (_sync)
            {
                _process.Exited += OnExited;
                _process.OutputDataReceived += OnOutput;
                _process.ErrorDataReceived += OnOutput;
                if (!_process.Start()) throw new InvalidOperationException("Could not start scrcpy.");
                _started = true;
                _process.EnableRaisingEvents = true;
                if (_closed) return;
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
            }
        }

        internal bool WaitForConnection(int timeoutMs)
        {
            return _connected.Task.Wait(timeoutMs) && _connected.Task.Result;
        }

        internal void Stop(bool transportLost)
        {
            lock (_sync)
            {
                if (_closed) return;
                _stopping = true;
                _transportLost |= transportLost;
                _connected.TrySetResult(false);
                if (!_started)
                {
                    CloseProcess();
                    return;
                }

                try
                {
                    if (!_process.HasExited) _process.Kill();
                }
                catch (InvalidOperationException)
                {
                    // The process exited while the stop was being requested.
                }
                catch (Win32Exception error)
                {
                    _log.Warning(DeviceLogFormatter.ForSerial(_serial, LocalizationService.Format(
                        "Log.NativeWifiKeepActive.StopFailed", error.Message)));
                }
            }
        }

        private void OnOutput(object sender, DataReceivedEventArgs args)
        {
            var line = args.Data;
            if (string.IsNullOrWhiteSpace(line)) return;
            if (line.Trim().Equals("DEBUG: Server connected", StringComparison.Ordinal))
            {
                lock (_sync)
                {
                    if (_stopping || _closed || _active) return;
                    _active = true;
                    _log.Progress(DeviceLogFormatter.ForSerial(_serial,
                        LocalizationService.Get("Log.NativeWifiKeepActive.Started")));
                    _connected.TrySetResult(true);
                }
            }
            else if (line.IndexOf("ERROR:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     line.IndexOf("WARN:", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _log.ToolOutput("WARN", DeviceLogFormatter.ForSerial(_serial, "[keep-active scrcpy] " + line));
            }
        }

        private void OnExited(object sender, EventArgs args)
        {
            lock (_sync)
            {
                if (_closed) return;
                var disconnected = _transportLost || _process.ExitCode == 2;
                if (_active)
                    _log.Progress(DeviceLogFormatter.ForSerial(_serial, LocalizationService.Get(disconnected
                        ? "Log.NativeWifiKeepActive.Disconnected"
                        : "Log.NativeWifiKeepActive.Stopped")));
                _connected.TrySetResult(false);
                CloseProcess();
            }
        }

        private void CloseProcess()
        {
            _closed = true;
            _process.Exited -= OnExited;
            _process.OutputDataReceived -= OnOutput;
            _process.ErrorDataReceived -= OnOutput;
            _process.Dispose();
        }
    }
}
