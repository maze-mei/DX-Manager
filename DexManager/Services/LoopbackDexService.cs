// DX Manager hidden-preview integration: Copyright 2026 asyouplz.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using DexManager.Models;

namespace DexManager.Services
{
    // One instance per device runtime. Never enumerates or kills other sessions.
    public sealed class LoopbackDexService
    {
        // Updated only together with the checked-in, reproducibly built helper.
        internal const string HelperSha256 = "34609c861ff41a878c3ea8249c441f6c88c9dc313a9ed22a18386f03b8c3b15c";
        private readonly AdbService _adb;
        private readonly LogService _log;
        private readonly object _sync = new object();
        private readonly Dictionary<string, Session> _sessions =
            new Dictionary<string, Session>(StringComparer.Ordinal);

        public LoopbackDexService(AdbService adb, LogService log)
        {
            _adb = adb;
            _log = log;
        }

        public VirtualDisplayLease Start(string serial, int creationWaitMs,
            Func<bool> cancellationRequested, string expectedDeviceIdentity)
        {
            ThrowIfCancelled(cancellationRequested);
            var helper = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "tools", "loopback", "dxm-loopback.jar");
            VerifyHelper(helper);
            VerifyDeviceIdentity(serial, expectedDeviceIdentity);
            ThrowIfCancelled(cancellationRequested);
            var token = Guid.NewGuid().ToString("N");
            var session = new Session(serial, token);
            var lease = new VirtualDisplayLease
            {
                Serial = serial,
                IsLoopback = true,
                LoopbackSessionId = token
            };
            lock (_sync) _sessions.Add(token, session);
            try
            {
                var push = _adb.PushForSerial(serial, helper,
                    LoopbackDexProtocol.CreateRemotePath(token));
                if (!push.IsSuccess)
                    throw new InvalidOperationException("Helper transfer failed.");
                ThrowIfCancelled(cancellationRequested);
                // Recheck after transfer: a wireless endpoint may have been
                // reassigned while the push was completing.
                VerifyDeviceIdentity(serial, expectedDeviceIdentity);
                ThrowIfCancelled(cancellationRequested);
                session.Process = _adb.StartShellSession(serial,
                    LoopbackDexProtocol.BuildShellCommand(token),
                    delegate(object sender, DataReceivedEventArgs e)
                    {
                        int id;
                        if (LoopbackDexProtocol.TryParseReady(e.Data, out id))
                            Interlocked.CompareExchange(ref session.DisplayId, id, 0);
                        else if (e.Data != null && e.Data.StartsWith(
                            "DXM_LOOPBACK_ERROR ", StringComparison.Ordinal))
                        {
                            Interlocked.CompareExchange(ref session.Error,
                                e.Data.Substring(19), null);
                            // Process exit only confirms the host channel closed.
                            // Record device cleanup separately, even if startup
                            // already supplied an earlier diagnostic.
                            if (e.Data == "DXM_LOOPBACK_ERROR cleanup_unconfirmed" &&
                                Interlocked.Exchange(ref session.CleanupUnconfirmed, 1) == 0)
                                _log.Warning("Loopback DeX cleanup was not confirmed on the phone. " +
                                    "Check the phone's DeX/Smart View disconnect controls.");
                        }
                    },
                    delegate(object sender, DataReceivedEventArgs e)
                    {
                        // Keep a bounded diagnostic; never print helper tokens.
                        if (!string.IsNullOrWhiteSpace(e.Data))
                            Interlocked.CompareExchange(ref session.Error,
                                e.Data.Length > 240 ? e.Data.Substring(0, 240) : e.Data,
                                null);
                    });
                session.Heartbeat = new Timer(delegate
                {
                    session.SendHeartbeat();
                }, null, 0, 3000);
                var timer = Stopwatch.StartNew();
                // Wireless DeX negotiation can take longer than overlay creation.
                var timeout = Math.Min(60000, Math.Max(30000, creationWaitMs));
                while (Volatile.Read(ref session.DisplayId) == 0)
                {
                    ThrowIfCancelled(cancellationRequested);
                    if (session.Process.HasExited)
                        throw new InvalidOperationException(session.Error ??
                            "The Samsung loopback helper exited before creating a display.");
                    if (timer.ElapsedMilliseconds >= timeout)
                        throw new TimeoutException(LocalizationService.Get(
                            "Error.Loopback.Timeout"));
                    Thread.Sleep(100);
                }
                ThrowIfCancelled(cancellationRequested);
                if (session.Process.HasExited)
                    throw new InvalidOperationException("Loopback display ended during startup.");
                lease.DisplayId = session.DisplayId;
                _log.Info("Experimental loopback DeX ready on display " + lease.DisplayId +
                    "; no phone overlay was created.");
                return lease;
            }
            catch (OperationCanceledException ex)
            {
                if (!Release(lease)) ex.Data[VirtualDisplayService.RetainedLeaseDataKey] = lease;
                throw;
            }
            catch (Exception ex)
            {
                var error = new InvalidOperationException(LocalizationService.Format(
                    "Error.Loopback.StartFailed", ex.Message), ex);
                if (!Release(lease)) error.Data[VirtualDisplayService.RetainedLeaseDataKey] = lease;
                throw error;
            }
        }

        public bool Release(VirtualDisplayLease lease)
        {
            if (lease == null || !lease.IsLoopback) return true;
            Session session;
            lock (_sync)
            {
                if (!_sessions.TryGetValue(lease.LoopbackSessionId ?? string.Empty,
                    out session)) return true;
                // A stale lease must never release another device/session.
                if (!string.Equals(session.Serial, lease.Serial, StringComparison.Ordinal))
                    return false;
            }
            Interlocked.Exchange(ref session.Stopping, 1);
            if (session.Heartbeat != null) session.Heartbeat.Dispose();
            // A blocked stdin/heartbeat must not hold normal shutdown forever.
            // Closing/killing the owned ADB client below does not take _inputGate.
            var stopInput = Task.Run(delegate
            {
                session.Send("STOP " + session.Token);
                session.CloseInput();
            });
            stopInput.Wait(500);
            var process = session.Process;
            var closed = process == null;
            if (process != null)
            {
                try
                {
                    if (!process.WaitForExit(3000))
                    {
                        _log.Warning("Loopback helper did not acknowledge exit; " +
                            "closing only this ADB client. Device watchdog expires within 15 seconds.");
                        process.Kill();
                        process.WaitForExit(1000);
                    }
                    closed = process.HasExited;
                }
                catch (Exception ex)
                {
                    _log.Warning("Loopback channel cleanup: " + ex.Message);
                }
                if (closed)
                {
                    _adb.ForgetShellSession(process);
                    process.Dispose();
                }
            }
            if (!closed) return false;
            lock (_sync) _sessions.Remove(lease.LoopbackSessionId);
            if (!string.IsNullOrEmpty(session.Error))
                _log.Warning("Loopback helper diagnostic: " + session.Error);
            // Never open a new shell against the captured serial here: a USB
            // or Wi-Fi endpoint may now belong to another physical phone.
            // The helper removes its own unique jar on exit over its original
            // connection. No broad cleanup and no host-side rm fallback.
            if (process == null)
                _log.Warning("The loopback helper channel was not started. " +
                    "A session-specific jar may remain in the original phone's " +
                    "/data/local/tmp; no reconnect cleanup was attempted.");
            return true;
        }

        private void ThrowIfCancelled(Func<bool> cancellationRequested)
        {
            if (_adb.IsProcessShutdownRequested ||
                (cancellationRequested != null && cancellationRequested()))
                throw new OperationCanceledException();
        }

        private void VerifyDeviceIdentity(string serial, string expectedDeviceIdentity)
        {
            if (string.IsNullOrWhiteSpace(expectedDeviceIdentity) ||
                (!expectedDeviceIdentity.StartsWith("serial:", StringComparison.OrdinalIgnoreCase) &&
                 !expectedDeviceIdentity.StartsWith("android:", StringComparison.OrdinalIgnoreCase)) ||
                !string.Equals(_adb.GetDeviceIdentity(serial), expectedDeviceIdentity,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(LocalizationService.Get(
                    "Error.Loopback.DeviceIdentity"));
        }

        internal static void VerifyHelper(string path)
        {
            if (File.Exists(path))
            {
                using (var stream = File.OpenRead(path))
                using (var sha = SHA256.Create())
                {
                    var digest = BitConverter.ToString(sha.ComputeHash(stream))
                        .Replace("-", string.Empty);
                    if (string.Equals(digest, HelperSha256,
                        StringComparison.OrdinalIgnoreCase)) return;
                }
            }
            throw new InvalidOperationException(LocalizationService.Get(
                "Error.Loopback.BundleMissing"));
        }

        private sealed class Session
        {
            private readonly object _inputGate = new object();
            private bool _inputClosed;
            internal readonly string Serial;
            internal readonly string Token;
            internal Process Process;
            internal Timer Heartbeat;
            internal int DisplayId;
            internal string Error;
            internal int Stopping;
            internal int CleanupUnconfirmed;
            private int _heartbeatInFlight;

            internal Session(string serial, string token)
            {
                Serial = serial;
                Token = token;
            }

            internal void SendHeartbeat()
            {
                if (Volatile.Read(ref Stopping) != 0 ||
                    Interlocked.CompareExchange(ref _heartbeatInFlight, 1, 0) != 0)
                    return;
                try
                {
                    if (!Send("PING " + Token)) CloseInput();
                }
                finally { Interlocked.Exchange(ref _heartbeatInFlight, 0); }
            }

            internal bool Send(string message)
            {
                lock (_inputGate)
                {
                    if (_inputClosed || Process == null) return false;
                    try
                    {
                        Process.StandardInput.WriteLine(message);
                        Process.StandardInput.Flush();
                        return true;
                    }
                    catch (Exception) { return false; }
                }
            }

            internal void CloseInput()
            {
                lock (_inputGate)
                {
                    if (_inputClosed) return;
                    _inputClosed = true;
                    try { if (Process != null) Process.StandardInput.Close(); }
                    catch (Exception) { }
                }
            }
        }
    }
}
