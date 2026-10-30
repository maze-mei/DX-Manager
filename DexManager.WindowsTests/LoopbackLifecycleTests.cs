using System.Diagnostics;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;

namespace DexManager.WindowsTests;

internal static class LoopbackLifecycleTests
{
    internal static readonly Action[] All =
    {
        BundledHelperMatchesPinnedHash,
        MissingOrModifiedHelperIsRejected,
        ReadySessionStopsOnlyItsOwnHelper,
        TwoDeviceSessionsStayIndependent,
        CancellationStopsPendingHelper,
        HelperFailureDoesNotFallBackToOverlay,
        TransferFailureDoesNotStartHelper,
        ExistingOverlayPreventsHiddenModeWithoutMutation,
        ShutdownGatePreventsNewDeviceProcesses,
        UnresponsiveHelperHasBoundedOwnedCleanup,
        DisconnectedDeviceSkipsRemoteFileCleanup,
        ShutdownStopsExistingChannelWithoutNewAdbProcess,
        CleanupUnconfirmedIsRecordedSeparatelyFromChannelExit,
        WrongDeviceIdentityPreventsHelperTransfer,
        DeviceReplacementAfterPushPreventsHelperExecution
    };

    private static void BundledHelperMatchesPinnedHash()
    {
        LoopbackDexService.VerifyHelper(Path.Combine(AppContext.BaseDirectory, "tools", "loopback", "dxm-loopback.jar"));
    }

    private static void MissingOrModifiedHelperIsRejected()
    {
        using var context = new TestContext();
        var path = Path.Combine(context.Directory, "invalid.jar");
        Reject<InvalidOperationException>(() => LoopbackDexService.VerifyHelper(path));
        File.WriteAllText(path, "not the bundled helper");
        Reject<InvalidOperationException>(() => LoopbackDexService.VerifyHelper(path));
        Require(context.Lines.Length == 0, "Integrity validation must not invoke ADB.");
    }

    private static void ReadySessionStopsOnlyItsOwnHelper()
    {
        using var context = new TestContext();
        var display = new VirtualDisplayService(context.Adb, context.Log);
        var lease = display.EnsureVirtualDisplay("dxm-test-first", HiddenSettings(), 1000, () => false, "serial:dxm-test-first");
        try
        {
            Require(lease.IsLoopback && lease.DisplayId == 41 && lease.Serial == "dxm-test-first",
                "The actual service must bind the announced display to the requested device.");
            Require(!lease.OwnsOverlaySetting, "Loopback must not claim ownership of a phone overlay.");
            Require(context.IsRunning("dxm-test-first"), "The helper channel must stay alive while DeX is active.");
        }
        finally { display.Release(lease); }
        Require(!context.IsRunning("dxm-test-first"), "Stop must wait for the owned child process to exit.");
        Require(context.Lines.Any(line => line == "STOP|dxm-test-first|" + lease.LoopbackSessionId),
            "Stop must send the session-bound STOP command.");
        Require(!context.Lines.Any(line => line.Contains("shell rm -f ", StringComparison.Ordinal)),
            "The helper removes its own file; cleanup must not open a shell on a possibly reused endpoint.");
        context.RequireNoOverlayMutation();
        Require(display.Release(lease), "Duplicate stop must be safe.");
    }

    private static void TwoDeviceSessionsStayIndependent()
    {
        using var context = new TestContext();
        var service = new LoopbackDexService(context.Adb, context.Log);
        var first = service.Start("dxm-test-first", 1000, () => false, "serial:dxm-test-first");
        VirtualDisplayLease second = null;
        try
        {
            second = service.Start("dxm-test-second", 1000, () => false, "serial:dxm-test-second");
            Require(first.LoopbackSessionId != second.LoopbackSessionId && second.DisplayId == 42,
                "Concurrent sessions must have unique ownership and IDs.");
            var stale = new VirtualDisplayLease { IsLoopback = true, Serial = "dxm-test-second", LoopbackSessionId = first.LoopbackSessionId };
            Require(!service.Release(stale), "A lease with the wrong serial must not release another device.");
            Require(context.IsRunning("dxm-test-first") && context.IsRunning("dxm-test-second"),
                "An invalid lease must leave both sessions alive.");
            Require(service.Release(first), "First session should stop normally.");
            Require(!context.IsRunning("dxm-test-first") && context.IsRunning("dxm-test-second"),
                "Stopping one device must preserve the other helper.");
        }
        finally
        {
            service.Release(first);
            if (second != null) service.Release(second);
        }
        context.RequireNoOverlayMutation();
    }

    private static void CancellationStopsPendingHelper()
    {
        using var context = new TestContext();
        var service = new LoopbackDexService(context.Adb, context.Log);
        var started = Stopwatch.StartNew();
        Reject<OperationCanceledException>(() => service.Start("dxm-test-no-ready", 1000,
            () => started.ElapsedMilliseconds > 1200, "serial:dxm-test-no-ready"));
        Require(context.Lines.Any(line => line.StartsWith("START|dxm-test-no-ready|", StringComparison.Ordinal)),
            "This test must cancel after the helper was started.");
        Require(!context.IsRunning("dxm-test-no-ready"), "Startup cancellation must stop the owned helper.");
        context.RequireNoOverlayMutation();
    }

    private static void HelperFailureDoesNotFallBackToOverlay()
    {
        using var context = new TestContext();
        var display = new VirtualDisplayService(context.Adb, context.Log);
        Reject<InvalidOperationException>(() => display.EnsureVirtualDisplay("dxm-test-early-exit", HiddenSettings(), 1000, () => false, "serial:dxm-test-early-exit"));
        Require(!context.IsRunning("dxm-test-early-exit"), "Failed startup must leave no live child process.");
        context.RequireNoOverlayMutation();
    }

    private static void TransferFailureDoesNotStartHelper()
    {
        using var context = new TestContext();
        var service = new LoopbackDexService(context.Adb, context.Log);
        Reject<InvalidOperationException>(() => service.Start("dxm-test-push-failure", 1000, () => false, "serial:dxm-test-push-failure"));
        Require(!context.Lines.Any(line => line.StartsWith("START|", StringComparison.Ordinal)),
            "A failed transfer must not execute the helper.");
        context.RequireNoOverlayMutation();
    }

    private static void ExistingOverlayPreventsHiddenModeWithoutMutation()
    {
        using var context = new TestContext();
        var display = new VirtualDisplayService(context.Adb, context.Log);
        Reject<InvalidOperationException>(() => display.EnsureVirtualDisplay("dxm-test-overlay", HiddenSettings(), 1000, () => false, "serial:dxm-test-overlay"));
        Require(!context.Lines.Any(line => line.StartsWith("START|", StringComparison.Ordinal) || line.Contains("|push ", StringComparison.Ordinal)),
            "An existing overlay must be preserved and block hidden startup before helper transfer.");
        context.RequireNoOverlayMutation();
    }

    private static void ShutdownGatePreventsNewDeviceProcesses()
    {
        using var context = new TestContext();
        context.Runner.BlockNewProcessesForWindowsShutdown();
        Reject<OperationCanceledException>(() => new LoopbackDexService(context.Adb, context.Log)
            .Start("dxm-test-first", 1000, () => false, "serial:dxm-test-first"));
        Require(context.Lines.Length == 0, "Windows shutdown must not launch new ADB clients.");
    }

    private static void UnresponsiveHelperHasBoundedOwnedCleanup()
    {
        using var context = new TestContext();
        var service = new LoopbackDexService(context.Adb, context.Log);
        var lease = service.Start("dxm-test-stubborn", 1000, () => false, "serial:dxm-test-stubborn");
        var timer = Stopwatch.StartNew();
        Require(service.Release(lease), "An unresponsive owned child should be terminated.");
        Require(timer.ElapsedMilliseconds < 7000 && !context.IsRunning("dxm-test-stubborn"),
            "Cleanup must be bounded and confirm the owned child is no longer running.");
        context.RequireNoOverlayMutation();
    }

    private static void DisconnectedDeviceSkipsRemoteFileCleanup()
    {
        using var context = new TestContext();
        var service = new LoopbackDexService(context.Adb, context.Log);
        var lease = service.Start("dxm-test-disconnected", 1000, () => false, "serial:dxm-test-disconnected");
        Require(service.Release(lease), "A disconnected device must not block local cleanup.");
        Require(!context.IsRunning("dxm-test-disconnected"), "Disconnect cleanup must stop the owned channel.");
        Require(!context.Lines.Any(line => line.Contains("shell rm -f ", StringComparison.Ordinal)),
            "Disconnected transport must not receive a cleanup shell command.");
        context.RequireNoOverlayMutation();
    }

    private static void ShutdownStopsExistingChannelWithoutNewAdbProcess()
    {
        using var context = new TestContext();
        var service = new LoopbackDexService(context.Adb, context.Log);
        var lease = service.Start("dxm-test-first", 1000, () => false, "serial:dxm-test-first");
        var commandsBeforeShutdown = context.Lines.Count(line => line.StartsWith("COMMAND|", StringComparison.Ordinal));
        context.Runner.BlockNewProcessesForWindowsShutdown();
        Require(service.Release(lease), "Shutdown should release the existing helper channel.");
        Require(!context.IsRunning("dxm-test-first"), "The helper must stop over the existing channel.");
        Require(context.Lines.Count(line => line.StartsWith("COMMAND|", StringComparison.Ordinal)) == commandsBeforeShutdown,
            "Shutdown cleanup must not start an ADB subprocess.");
    }

    private static void WrongDeviceIdentityPreventsHelperTransfer()
    {
        using var context = new TestContext();
        var service = new LoopbackDexService(context.Adb, context.Log);
        Reject<InvalidOperationException>(() => service.Start("dxm-test-first", 1000,
            () => false, "serial:another-phone"));
        Reject<InvalidOperationException>(() => service.Start("dxm-test-first", 1000,
            () => false, "transport:dxm-test-first"));
        Require(!context.Lines.Any(line => line.Contains("|push ", StringComparison.Ordinal) ||
                line.StartsWith("START|", StringComparison.Ordinal)),
            "A mismatched or temporary identity must prevent transfer and activation.");
    }

    private static void DeviceReplacementAfterPushPreventsHelperExecution()
    {
        using var context = new TestContext();
        var service = new LoopbackDexService(context.Adb, context.Log);
        Reject<InvalidOperationException>(() => service.Start("dxm-test-replaced", 1000,
            () => false, "serial:dxm-test-replaced"));
        Require(context.Lines.Any(line => line.Contains("|push ", StringComparison.Ordinal)),
            "The fake endpoint must change identity after a real push invocation.");
        Require(!context.Lines.Any(line => line.StartsWith("START|", StringComparison.Ordinal) ||
                line.Contains("shell rm -f ", StringComparison.Ordinal)),
            "Endpoint reuse must not execute or clean files on the replacement phone.");
        context.RequireNoOverlayMutation();
    }

    private static VirtualDisplaySettings HiddenSettings()
    {
        var settings = AppSettings.CreateDefault().VirtualDisplay;
        settings.HidePhonePreview = true;
        return settings;
    }

    private static void CleanupUnconfirmedIsRecordedSeparatelyFromChannelExit()
    {
        using var context = new TestContext();
        var service = new LoopbackDexService(context.Adb, context.Log);
        var lease = service.Start("dxm-test-unconfirmed", 1000, () => false, "serial:dxm-test-unconfirmed");
        Require(service.Release(lease), "An exited local channel must not block shutdown.");
        Require(SpinWait.SpinUntil(() => context.Log.GetSessionEntries().Any(
                entry => entry.Contains("cleanup was not confirmed on the phone", StringComparison.Ordinal)), 2000),
            "Local process exit must not hide unconfirmed cleanup on the phone.");
        context.RequireNoOverlayMutation();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Reject<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }

    private sealed class TestContext : IDisposable
    {
        private readonly string _previousLog;
        private readonly string _logPath;
        internal readonly string Directory;
        internal readonly LogService Log = new LogService();
        internal readonly ProcessRunner Runner;
        internal readonly AdbService Adb;

        internal TestContext()
        {
            Directory = Path.Combine(Path.GetTempPath(), "dxm-loopback-tests-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(Directory);
            _logPath = Path.Combine(Directory, "fake-adb.log");
            _previousLog = Environment.GetEnvironmentVariable(FakeAdb.LogVariable);
            Environment.SetEnvironmentVariable(FakeAdb.LogVariable, _logPath);
            Runner = new ProcessRunner(Log);
            var executable = Path.Combine(AppContext.BaseDirectory,
                "DexManager.WindowsTests" + (OperatingSystem.IsWindows() ? ".exe" : ""));
            Adb = new AdbService(executable, 5000, Runner, Log);
        }

        internal string[] Lines => File.Exists(_logPath) ? File.ReadAllLines(_logPath) : Array.Empty<string>();

        internal bool IsRunning(string serial)
        {
            var start = Lines.LastOrDefault(line => line.StartsWith("START|" + serial + "|", StringComparison.Ordinal));
            if (start == null) return false;
            try
            {
                using var process = Process.GetProcessById(int.Parse(start.Split('|')[2]));
                return !process.HasExited;
            }
            catch (ArgumentException) { return false; }
        }

        internal void RequireNoOverlayMutation()
        {
            Require(!Lines.Any(line => line.Contains("settings put global overlay_display_devices", StringComparison.Ordinal) ||
                line.Contains("settings delete global overlay_display_devices", StringComparison.Ordinal)),
                "Hidden mode must never create, overwrite or delete a phone overlay.");
        }

        public void Dispose()
        {
            // This terminates only child processes registered by this runner,
            // never every process with a shared executable name.
            Runner.BeginShutdown();
            Environment.SetEnvironmentVariable(FakeAdb.LogVariable, _previousLog);
            System.IO.Directory.Delete(Directory, true);
        }
    }
}
