using DexManager.Platform;
using DexManager.Utils;

namespace DexManager.Mac.Platform;

public sealed class MacPlatformService : IPlatformService
{
    public bool IsAdministrator() => AdminHelper.IsAdministrator();

    public string GetOperatingSystemDisplayName() => PlatformHelper.GetDisplayName();

    public Version GetOperatingSystemVersion() => PlatformHelper.CurrentVersion;

    public bool RequiresLegacyAdb => false;

    public bool IsWindow(IntPtr handle) => handle != IntPtr.Zero;

    public bool IsWindowVisible(IntPtr handle) => handle != IntPtr.Zero;

    public bool IsIconic(IntPtr handle) => false;

    public void ShowWindow(IntPtr handle, bool restore)
    {
        // Handled via macOS process / AppleScript activation when needed
    }

    public void SetForegroundWindow(IntPtr handle)
    {
        // Handled via macOS window / process activation
    }

    public IntPtr GetForegroundWindow() => IntPtr.Zero;

    public bool GetClientRect(IntPtr handle, out int left, out int top, out int right, out int bottom)
    {
        left = 0;
        top = 0;
        right = 1920;
        bottom = 1080;
        return false;
    }

    public bool GetWindowRect(IntPtr handle, out int left, out int top, out int right, out int bottom)
    {
        left = 0;
        top = 0;
        right = 1920;
        bottom = 1080;
        return false;
    }

    public void SuppressNativeCrashDialogs()
    {
        // No-op on macOS
    }

    public bool IsDirectoryInProcessPath(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return false;
        var norm = Path.GetFullPath(directory).TrimEnd('/');
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var part in path.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(Path.GetFullPath(part).TrimEnd('/'), norm, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    public bool IsDirectoryInSystemPath(string directory) => IsDirectoryInProcessPath(directory);

    public bool TryRegisterDirectoryInSystemPath(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return false;
        if (!IsAdministrator()) return false;

        try
        {
            const string pathsD = "/etc/paths.d";
            if (Directory.Exists(pathsD))
            {
                var targetFile = Path.Combine(pathsD, "dxmanager");
                File.WriteAllText(targetFile, Path.GetFullPath(directory) + "\n");
                return true;
            }
        }
        catch
        {
            // Suppress system path registration errors
        }

        return false;
    }
}
