using System;
using System.Collections.Generic;
using System.Diagnostics;
using DexManager.Models;

namespace DexManager.Services
{
    public sealed class ForegroundAppIdentity
    {
        public string PackageName { get; private set; }
        public int WindowId { get; private set; }
        public string WindowStateClass { get; private set; }
        public string WindowTitle { get; private set; }

        public ForegroundAppIdentity(string package, int windowId = -1,
            string stateClass = null, string title = null)
        {
            PackageName = package;
            WindowId = windowId;
            WindowStateClass = stateClass ?? "";
            WindowTitle = AppKeyProfile.NormalizeWindowTitle(title);
        }
    }

    // One instance per physical-device runtime. Readers use one immutable snapshot.
    public sealed class ForegroundAppCache
    {
        private sealed class Snapshot
        {
            public long Generation, Sequence, LastSeen;
            public bool Available;
            public Dictionary<int, ForegroundAppIdentity> Apps;
        }

        private readonly object _sync = new object();
        private readonly Func<long> _milliseconds;
        private volatile Snapshot _snapshot;
        private const long HeartbeatExpiryMs = 8000;

        public ForegroundAppCache() : this(delegate
        {
            return (long)(Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency);
        }) { }

        internal ForegroundAppCache(Func<long> milliseconds)
        {
            _milliseconds = milliseconds;
            _snapshot = Empty(0, 0);
        }

        public long BeginConnection()
        {
            lock (_sync)
            {
                var next = Empty(_snapshot.Generation + 1, _milliseconds());
                _snapshot = next;
                return next.Generation;
            }
        }

        public void Disconnect(long generation)
        {
            lock (_sync)
            {
                if (_snapshot.Generation != generation) return;
                _snapshot = Empty(generation + 1, 0);
            }
        }

        public void Invalidate()
        {
            lock (_sync) _snapshot = Empty(_snapshot.Generation + 1, 0);
        }

        public bool SetAvailable(long generation, long sequence, bool available)
        {
            lock (_sync)
            {
                var old = _snapshot;
                if (old.Generation != generation || sequence <= old.Sequence)
                    return false;
                _snapshot = new Snapshot
                {
                    Generation = generation, Sequence = sequence,
                    Available = available, LastSeen = _milliseconds(),
                    // A service status transition starts a fresh snapshot.
                    Apps = new Dictionary<int, ForegroundAppIdentity>()
                };
                return true;
            }
        }

        public bool Update(long generation, long sequence, int displayId, string package)
        {
            if (displayId < 0 || (!string.IsNullOrEmpty(package) &&
                !AppKeyRuleResolver.IsPackageName(package))) return false;
            lock (_sync)
            {
                var old = _snapshot;
                if (old.Generation != generation || !old.Available ||
                    sequence <= old.Sequence) return false;
                var apps = new Dictionary<int, ForegroundAppIdentity>(old.Apps);
                if (string.IsNullOrEmpty(package)) apps.Remove(displayId);
                else apps[displayId] = new ForegroundAppIdentity(package);
                _snapshot = new Snapshot
                {
                    Generation = generation, Sequence = sequence,
                    Available = true, LastSeen = _milliseconds(), Apps = apps
                };
                return true;
            }
        }

        internal bool UpdateWindow(long generation, ForegroundWindowDiagnostic window)
        {
            if (window == null || window.DisplayId < 0 || window.WindowId < 0) return false;
            lock (_sync)
            {
                var old = _snapshot;
                ForegroundAppIdentity current;
                // Package and window frames share one ordered authenticated stream.
                // Metadata must not revive a stale/cleared package or refresh liveness.
                if (old.Generation != generation || !old.Available ||
                    _milliseconds() - old.LastSeen > HeartbeatExpiryMs ||
                    !old.Apps.TryGetValue(window.DisplayId, out current) ||
                    current.PackageName != window.Package) return false;
                var next = window.Focused && window.WindowType == 1
                    ? new ForegroundAppIdentity(window.Package, window.WindowId, window.StateClass, window.Title)
                    : new ForegroundAppIdentity(window.Package);
                if (current.WindowId == next.WindowId && current.WindowStateClass == next.WindowStateClass &&
                    current.WindowTitle == next.WindowTitle) return false;
                var apps = new Dictionary<int, ForegroundAppIdentity>(old.Apps);
                apps[window.DisplayId] = next;
                _snapshot = new Snapshot { Generation = generation, Sequence = old.Sequence,
                    Available = true, LastSeen = old.LastSeen, Apps = apps };
                return true;
            }
        }

        public void Heartbeat(long generation)
        {
            lock (_sync)
            {
                var old = _snapshot;
                if (old.Generation != generation) return;
                _snapshot = new Snapshot
                {
                    Generation = generation, Sequence = old.Sequence,
                    Available = old.Available, LastSeen = _milliseconds(),
                    // Liveness must never resurrect rules that already expired.
                    Apps = _milliseconds() - old.LastSeen > HeartbeatExpiryMs
                        ? new Dictionary<int, ForegroundAppIdentity>() : old.Apps
                };
            }
        }

        public string GetPackage(int displayId)
        {
            var app = GetIdentity(displayId);
            return app == null ? string.Empty : app.PackageName;
        }

        public ForegroundAppIdentity GetIdentity(int displayId)
        {
            var snapshot = _snapshot;
            if (!snapshot.Available || displayId < 0 ||
                _milliseconds() - snapshot.LastSeen > HeartbeatExpiryMs)
                return null;
            ForegroundAppIdentity app;
            return snapshot.Apps.TryGetValue(displayId, out app) ? app : null;
        }

        private static Snapshot Empty(long generation, long time)
        {
            return new Snapshot
            {
                Generation = generation, Sequence = -1, LastSeen = time,
                Apps = new Dictionary<int, ForegroundAppIdentity>()
            };
        }
    }
}
