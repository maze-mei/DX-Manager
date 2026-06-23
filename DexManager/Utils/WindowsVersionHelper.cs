using System;

namespace DexManager.Utils
{
    public static class WindowsVersionHelper
    {
        public static Version CurrentVersion
        {
            get { return Environment.OSVersion.Version; }
        }

        public static bool RequiresLegacyAdb
        {
            get
            {
                var version = CurrentVersion;
                return version.Major < 10;
            }
        }

        public static string GetDisplayName()
        {
            var version = CurrentVersion;

            if (version.Major == 6 && version.Minor == 1) return "Windows 7";
            if (version.Major == 6 && version.Minor == 2) return "Windows 8";
            if (version.Major == 6 && version.Minor == 3) return "Windows 8.1";
            if (version.Major >= 10 && version.Build >= 22000) return "Windows 11";
            if (version.Major >= 10) return "Windows 10";

            return "Windows " + version;
        }
    }
}
