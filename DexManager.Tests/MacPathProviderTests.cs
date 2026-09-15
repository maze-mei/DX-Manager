using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using DexManager.Mac.Platform;
using DexManager.Models;
using DexManager.Platform;
using DexManager.Services;
using DexManager.Utils;
using Xunit;

namespace DexManager.Tests
{
    public class MacPathProviderTests
    {
        private readonly MacPathProvider _provider;

        public MacPathProviderTests()
        {
            _provider = new MacPathProvider();
        }

        [Fact]
        public void BaseDirectory_IsNotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(_provider.BaseDirectory));
            Assert.True(Directory.Exists(_provider.BaseDirectory));
        }

        [Fact]
        public void DefaultSettingsFilePath_PointsToConfigSettingsJson()
        {
            var path = _provider.DefaultSettingsFilePath;
            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.EndsWith(Path.Combine("config", "settings.json"), path);
            Assert.StartsWith(_provider.BaseDirectory, path);
        }

        [Fact]
        public void DefaultLogDirectory_PointsToLogsSubfolder()
        {
            var path = _provider.DefaultLogDirectory;
            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.EndsWith("logs", path);
            Assert.StartsWith(_provider.BaseDirectory, path);
        }

        [Fact]
        public void DefaultScreenshotFolder_ContainsPicturesDXManager()
        {
            var path = _provider.DefaultScreenshotFolder;
            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.Contains(Path.Combine("Pictures", "DXManager"), path);
        }

        [Fact]
        public void DefaultProxyExecutablePath_PointsToAdbProxy()
        {
            var path = _provider.DefaultProxyExecutablePath;
            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.Contains(Path.Combine("tools", "adb-proxy"), path);
        }

        [Fact]
        public void GetCandidateAdbPaths_ContainsHomebrewAndAndroidSdkLocations()
        {
            var candidates = _provider.GetCandidateAdbPaths();
            Assert.NotNull(candidates);
            Assert.NotEmpty(candidates);

            Assert.Contains(candidates, p => p.Contains("/opt/homebrew/bin/adb"));
            Assert.Contains(candidates, p => p.Contains("/usr/local/bin/adb"));
            Assert.Contains(candidates, p => p.Contains(Path.Combine("Library", "Android", "sdk", "platform-tools", "adb")));
            Assert.Contains(candidates, p => p.Contains(Path.Combine(".android-sdk", "platform-tools", "adb")));
            Assert.Contains(candidates, p => p.Contains("/opt/android-sdk/platform-tools/adb"));
        }

        [Fact]
        public void GetCandidateScrcpyPaths_ContainsHomebrewAndLocalLocations()
        {
            var candidates = _provider.GetCandidateScrcpyPaths();
            Assert.NotNull(candidates);
            Assert.NotEmpty(candidates);

            Assert.Contains(candidates, p => p.Contains("/opt/homebrew/bin/scrcpy"));
            Assert.Contains(candidates, p => p.Contains("/usr/local/bin/scrcpy"));
            Assert.Contains(candidates, p => p.Contains("/usr/bin/scrcpy"));
        }

        [Fact]
        public void ResolveDefaultAdbPath_ReturnsValidString()
        {
            var resolved = _provider.ResolveDefaultAdbPath();
            Assert.False(string.IsNullOrWhiteSpace(resolved));
            // Either an existing binary or fallback /opt/homebrew/bin/adb
            Assert.True(File.Exists(resolved) || resolved == "/opt/homebrew/bin/adb");
        }

        [Fact]
        public void ResolveDefaultScrcpyPath_ReturnsValidString()
        {
            var resolved = _provider.ResolveDefaultScrcpyPath();
            Assert.False(string.IsNullOrWhiteSpace(resolved));
            Assert.True(File.Exists(resolved) || resolved == "/opt/homebrew/bin/scrcpy");
        }

        [Fact]
        public void ResolveWin7AdbPath_ReturnsSameAsDefaultAdbPath()
        {
            var win7Adb = _provider.ResolveWin7AdbPath();
            var defaultAdb = _provider.ResolveDefaultAdbPath();
            Assert.Equal(defaultAdb, win7Adb);
        }

        [Fact]
        public void PathService_IsAdbDirectoryInProcessPath_HandlesUnixColonSeparator()
        {
            var logService = new LogService();
            var settingsService = new SettingsService(logService);
            var processRunner = new ProcessRunner(logService);
            var pathService = new PathService(settingsService, logService, processRunner, _provider);

            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var segments = pathEnv.Split(':', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length > 0)
            {
                var firstDir = segments[0].Trim();
                var mockAdbPath = Path.Combine(firstDir, "adb");
                Assert.True(pathService.IsAdbDirectoryInProcessPath(mockAdbPath));
            }

            Assert.False(pathService.IsAdbDirectoryInProcessPath(null));
            Assert.False(pathService.IsAdbDirectoryInProcessPath(string.Empty));
            Assert.False(pathService.IsAdbDirectoryInProcessPath("/nonexistent_folder_xyz_12345/adb"));
        }

        [Fact]
        public void PathService_SelectAdbPath_ThrowsOnNullSettings()
        {
            var logService = new LogService();
            var settingsService = new SettingsService(logService);
            var processRunner = new ProcessRunner(logService);
            var pathService = new PathService(settingsService, logService, processRunner, _provider);

            Assert.Throws<ArgumentNullException>(() => pathService.SelectAdbPath(null, 3000));
        }

        [Fact]
        public void PathService_SelectAdbPath_ThrowsWhenManualAdbNotFound()
        {
            var logService = new LogService();
            var settingsService = new SettingsService(logService);
            var processRunner = new ProcessRunner(logService);
            var pathService = new PathService(settingsService, logService, processRunner, _provider);

            var settings = AppSettings.CreateDefault();
            settings.Paths.AdbSelectionMode = AdbSelectionMode.Manual;
            settings.Paths.AdbPath = "/nonexistent/path/to/adb";

            Assert.Throws<FileNotFoundException>(() => pathService.SelectAdbPath(settings, 3000));
        }

        [Fact]
        public void GetCandidateScrcpyPaths_PrefersBundledArchitectureDirectory()
        {
            var archDirectory = MacPathProvider.GetMacScrcpyDirectoryName();
            if (string.IsNullOrEmpty(archDirectory)) return;

            var candidates = _provider.GetCandidateScrcpyPaths();
            var architectureIndex = Array.FindIndex(
                candidates,
                p => string.Equals(
                    p,
                    Path.Combine(_provider.BaseDirectory, "tools", archDirectory, "scrcpy"),
                    StringComparison.Ordinal));
            var legacyIndex = Array.FindIndex(
                candidates,
                p => string.Equals(
                    p,
                    Path.Combine(_provider.BaseDirectory, "tools", "scrcpy", "scrcpy"),
                    StringComparison.Ordinal));

            Assert.True(architectureIndex >= 0);
            Assert.True(
                legacyIndex < 0 || architectureIndex < legacyIndex,
                "The architecture specific scrcpy path must be tried before the legacy x86_64 build.");
        }

        [Fact]
        public void ResolveDefaultScrcpyPath_ReturnsRunnableBinary()
        {
            var resolved = _provider.ResolveDefaultScrcpyPath();

            Assert.True(
                MacExecutableInspector.IsRunnableOnThisMachine(resolved),
                resolved + " cannot run on this Mac.");
        }

        [Fact]
        public void DefaultProxyExecutablePath_IsRunnableOnThisMachine()
        {
            var path = _provider.DefaultProxyExecutablePath;

            Assert.True(
                MacExecutableInspector.IsRunnableOnThisMachine(path) ||
                path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase),
                path + " cannot run on this Mac.");
        }

        [Fact]
        public void MacExecutableInspector_RejectsMissingFile()
        {
            Assert.False(MacExecutableInspector.IsRunnableOnThisMachine(null));
            Assert.False(MacExecutableInspector.IsRunnableOnThisMachine(string.Empty));
            Assert.False(MacExecutableInspector.IsRunnableOnThisMachine(
                Path.Combine(_provider.BaseDirectory, "tools", "does-not-exist")));
        }

        [Fact]
        public void MacExecutableInspector_AcceptsCurrentArchitectureSlice()
        {
            var cpuType = RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? 0x0100000Cu
                : 0x01000007u;
            var path = WriteTemporaryBinary(BuildThinMachO(cpuType));

            try
            {
                Assert.True(MacExecutableInspector.IsRunnableOnThisMachine(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void MacExecutableInspector_RejectsOtherArchitectureSlice()
        {
            var otherCpuType = RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? 0x01000007u
                : 0x0100000Cu;
            var path = WriteTemporaryBinary(BuildThinMachO(otherCpuType));

            try
            {
                Assert.False(MacExecutableInspector.IsRunnableOnThisMachine(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void MacExecutableInspector_AcceptsUniversalBinary()
        {
            var path = WriteTemporaryBinary(BuildFatMachO(0x0100000Cu, 0x01000007u));

            try
            {
                Assert.True(MacExecutableInspector.IsRunnableOnThisMachine(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void MacExecutableInspector_TreatsNonMachOFileAsRunnable()
        {
            var path = WriteTemporaryBinary("#!/bin/sh\necho scrcpy\n"u8.ToArray());

            try
            {
                Assert.True(MacExecutableInspector.IsRunnableOnThisMachine(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void MacExecutableInspector_MatchesBundledScrcpyArchitecture()
        {
            var arm64 = Path.Combine(
                _provider.BaseDirectory,
                "tools",
                "scrcpy-macos-aarch64-v3.3.4",
                "scrcpy");
            var x64 = Path.Combine(
                _provider.BaseDirectory,
                "tools",
                "scrcpy-macos-x86_64-v3.3.4",
                "scrcpy");

            if (File.Exists(arm64))
            {
                Assert.Equal(
                    RuntimeInformation.ProcessArchitecture == Architecture.Arm64,
                    MacExecutableInspector.IsRunnableOnThisMachine(arm64));
            }

            if (File.Exists(x64))
            {
                Assert.Equal(
                    RuntimeInformation.ProcessArchitecture == Architecture.X64,
                    MacExecutableInspector.IsRunnableOnThisMachine(x64));
            }
        }

        [Fact]
        public void AdbProxyLocator_PrefersMacArchitectureDirectory()
        {
            if (!OperatingSystem.IsMacOS()) return;
            var archDirectory = MacPathProvider.GetMacArchDirectoryName();
            if (string.IsNullOrEmpty(archDirectory)) return;

            var root = CreateTemporaryDirectory();
            var proxyDirectory = Path.Combine(root, "tools", "adb-proxy");
            var archBinary = Path.Combine(proxyDirectory, archDirectory, "DXMAdbProxy");
            Directory.CreateDirectory(Path.Combine(proxyDirectory, archDirectory));
            File.WriteAllText(archBinary, "arch");
            File.WriteAllText(Path.Combine(proxyDirectory, "DXMAdbProxy"), "legacy");
            File.WriteAllText(Path.Combine(proxyDirectory, "DXMAdbProxy.dll"), "dll");

            try
            {
                Assert.Equal(archBinary, AdbProxyLocator.Resolve(proxyDirectory, root));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void AdbProxyLocator_FallsBackToDllWhenNoNativeProxyExists()
        {
            if (OperatingSystem.IsWindows()) return;

            var root = CreateTemporaryDirectory();
            var proxyDirectory = Path.Combine(root, "tools", "adb-proxy");
            Directory.CreateDirectory(proxyDirectory);
            var dll = Path.Combine(proxyDirectory, "DXMAdbProxy.dll");
            File.WriteAllText(dll, "dll");

            try
            {
                Assert.Equal(dll, AdbProxyLocator.Resolve(proxyDirectory, root));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static byte[] BuildThinMachO(uint cpuType)
        {
            var data = new byte[32];
            data[0] = 0xCF;
            data[1] = 0xFA;
            data[2] = 0xED;
            data[3] = 0xFE;
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), cpuType);
            return data;
        }

        private static byte[] BuildFatMachO(params uint[] cpuTypes)
        {
            var data = new byte[8 + (cpuTypes.Length * 20)];
            data[0] = 0xCA;
            data[1] = 0xFE;
            data[2] = 0xBA;
            data[3] = 0xBE;
            BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(4), (uint)cpuTypes.Length);
            for (var index = 0; index < cpuTypes.Length; index++)
            {
                BinaryPrimitives.WriteUInt32BigEndian(
                    data.AsSpan(8 + (index * 20)),
                    cpuTypes[index]);
            }

            return data;
        }

        private static string WriteTemporaryBinary(byte[] content)
        {
            var path = Path.Combine(
                Path.GetTempPath(),
                "dxm-macho-" + Guid.NewGuid().ToString("N"));
            File.WriteAllBytes(path, content);
            return path;
        }

        private static string CreateTemporaryDirectory()
        {
            var path = Path.Combine(
                Path.GetTempPath(),
                "dxm-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
