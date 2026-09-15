using System.Runtime.InteropServices;
using DexManager.Platform;

namespace DexManager.Mac.Platform;

public sealed class MacPathProvider : IPathProvider
{
    public string BaseDirectory => AppDomain.CurrentDomain.BaseDirectory;

    public string DefaultSettingsFilePath =>
        Path.Combine(BaseDirectory, "config", "settings.json");

    public string DefaultScreenshotFolder
    {
        get
        {
            var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (string.IsNullOrWhiteSpace(pictures) || !Directory.Exists(pictures))
            {
                var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                pictures = Path.Combine(userHome, "Pictures");
            }
            return Path.Combine(pictures, "DXManager");
        }
    }

    public string DefaultLogDirectory =>
        Path.Combine(BaseDirectory, "logs");

    public string DefaultProxyExecutablePath
    {
        get
        {
            var proxyDir = Path.Combine(BaseDirectory, "tools", "adb-proxy");
            var archDir = GetMacArchDirectoryName();
            if (!string.IsNullOrEmpty(archDir))
            {
                var archNative = Path.Combine(proxyDir, archDir, "DXMAdbProxy");
                if (MacExecutableInspector.IsRunnableOnThisMachine(archNative))
                    return archNative;
            }
            var native = Path.Combine(proxyDir, "DXMAdbProxy");
            if (MacExecutableInspector.IsRunnableOnThisMachine(native))
                return native;
            var dllInProxy = Path.Combine(proxyDir, "DXMAdbProxy.dll");
            if (File.Exists(dllInProxy)) return dllInProxy;
            var dllInBase = Path.Combine(BaseDirectory, "DXMAdbProxy.dll");
            if (File.Exists(dllInBase)) return dllInBase;
            return Path.Combine(proxyDir, "DXMAdbProxy.exe");
        }
    }

    public string ResolveDefaultAdbPath()
    {
        foreach (var path in GetCandidateAdbPaths())
        {
            if (MacExecutableInspector.IsRunnableOnThisMachine(path))
                return Path.GetFullPath(path);
        }

        var pathAdb = FindInPath("adb");
        return !string.IsNullOrWhiteSpace(pathAdb) ? pathAdb : "/opt/homebrew/bin/adb";
    }

    public string ResolveDefaultScrcpyPath()
    {
        foreach (var path in GetCandidateScrcpyPaths())
        {
            if (MacExecutableInspector.IsRunnableOnThisMachine(path))
                return Path.GetFullPath(path);
        }

        var pathScrcpy = FindInPath("scrcpy");
        return !string.IsNullOrWhiteSpace(pathScrcpy) ? pathScrcpy : "/opt/homebrew/bin/scrcpy";
    }

    public string ResolveWin7AdbPath() => ResolveDefaultAdbPath();

    public string[] GetCandidateAdbPaths()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new List<string>
        {
            Path.Combine(BaseDirectory, "tools", "adb", "adb"),
            Path.Combine(BaseDirectory, "tools", "scrcpy", "adb"),
            Path.Combine(home, "Downloads", "scrcpy-macos-x86_64-v3.3.4", "adb"),
            Path.Combine(home, "Downloads", "scrcpy-macos-aarch64-v3.3.4", "adb"),
            Path.Combine(home, "Library", "Android", "sdk", "platform-tools", "adb"),
            "/opt/homebrew/bin/adb",
            "/usr/local/bin/adb",
            Path.Combine(home, ".android-sdk", "platform-tools", "adb"),
            "/opt/android-sdk/platform-tools/adb",
            Path.Combine(BaseDirectory, "tools", "adb", "adb.exe")
        };

        var inPath = FindInPath("adb");
        if (!string.IsNullOrWhiteSpace(inPath) && !candidates.Contains(inPath))
        {
            candidates.Insert(0, inPath);
        }

        return [.. candidates];
    }

    public string[] GetCandidateScrcpyPaths()
    {
        var scrcpyDir = GetMacScrcpyDirectoryName();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new List<string>();
        if (!string.IsNullOrEmpty(scrcpyDir))
        {
            candidates.Add(Path.Combine(BaseDirectory, "tools", scrcpyDir, "scrcpy"));
        }
        candidates.Add(Path.Combine(BaseDirectory, "tools", "scrcpy", "scrcpy"));
        if (!string.IsNullOrEmpty(scrcpyDir))
        {
            candidates.Add(Path.Combine(home, "Downloads", scrcpyDir, "scrcpy"));
        }
        candidates.Add(Path.Combine(home, "Downloads", "scrcpy-macos-x86_64-v3.3.4", "scrcpy"));
        candidates.Add(Path.Combine(home, "Downloads", "scrcpy-macos-aarch64-v3.3.4", "scrcpy"));
        candidates.Add("/opt/homebrew/bin/scrcpy");
        candidates.Add("/usr/local/bin/scrcpy");
        candidates.Add("/usr/bin/scrcpy");
        candidates.Add(Path.Combine(BaseDirectory, "tools", "scrcpy", "scrcpy.exe"));

        var inPath = FindInPath("scrcpy");
        if (!string.IsNullOrWhiteSpace(inPath) && !candidates.Contains(inPath))
        {
            candidates.Insert(0, inPath);
        }

        return [.. candidates];
    }

    /// <summary>
    /// Name of the bundled macOS scrcpy folder that matches the running CPU.
    /// </summary>
    public static string GetMacScrcpyDirectoryName() =>
        RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "scrcpy-macos-aarch64-v3.3.4",
            Architecture.X64 => "scrcpy-macos-x86_64-v3.3.4",
            _ => string.Empty,
        };

    /// <summary>
    /// Name of the bundled ADB proxy folder that matches the running CPU.
    /// Must stay in sync with <see cref="DexManager.Utils.AdbProxyLocator"/>.
    /// </summary>
    public static string GetMacArchDirectoryName() =>
        RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "osx-arm64",
            Architecture.X64 => "osx-x64",
            _ => string.Empty,
        };

    private static string FindInPath(string binaryName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv)) return null;

        foreach (var segment in pathEnv.Split([':'], StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var full = Path.Combine(segment.Trim(), binaryName);
                if (File.Exists(full))
                    return Path.GetFullPath(full);
            }
            catch
            {
                // Suppress path inspection errors
            }
        }

        return null;
    }
}
