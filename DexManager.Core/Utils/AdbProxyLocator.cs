using System;
using System.IO;
using System.Runtime.InteropServices;

namespace DexManager.Utils
{
    /// <summary>
    /// Resolves the DXMAdbProxy executable that matches the running platform.
    /// macOS ships the proxy per CPU architecture under
    /// <c>tools/adb-proxy/osx-arm64</c> and <c>tools/adb-proxy/osx-x64</c>, so the
    /// legacy single-folder layout alone is not enough there.
    /// </summary>
    public static class AdbProxyLocator
    {
        public static string Resolve(string proxyDirectory, string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(proxyDirectory)) return null;

            if (OperatingSystem.IsMacOS())
            {
                var archDirectory = GetMacArchitectureDirectoryName();
                if (!string.IsNullOrEmpty(archDirectory))
                {
                    var archNative = Path.Combine(
                        proxyDirectory,
                        archDirectory,
                        "DXMAdbProxy");
                    if (File.Exists(archNative)) return archNative;
                }
            }

            var nativeName = OperatingSystem.IsWindows()
                ? "DXMAdbProxy.exe"
                : "DXMAdbProxy";
            var nativePath = Path.Combine(proxyDirectory, nativeName);
            if (File.Exists(nativePath)) return nativePath;

            var proxyDll = Path.Combine(proxyDirectory, "DXMAdbProxy.dll");
            if (File.Exists(proxyDll)) return proxyDll;

            if (!string.IsNullOrWhiteSpace(baseDirectory))
            {
                var baseDll = Path.Combine(baseDirectory, "DXMAdbProxy.dll");
                if (File.Exists(baseDll)) return baseDll;
            }

            return Path.Combine(proxyDirectory, "DXMAdbProxy.exe");
        }

        private static string GetMacArchitectureDirectoryName()
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "osx-arm64",
                Architecture.X64 => "osx-x64",
                _ => string.Empty
            };
        }
    }
}
