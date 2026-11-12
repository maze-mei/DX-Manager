using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using DexManager.Utils;

namespace DexManager.Services
{
    internal enum DisplayCleanupPermissionState
    {
        NoDevice,
        NotInstalled,
        VerificationFailed,
        Ready,
        Granted,
        Error
    }

    internal sealed class DisplayCleanupPermissionStatus
    {
        public DisplayCleanupPermissionState State { get; set; }
        public string Detail { get; set; }
        public string Serial { get; set; }
        public int VersionCode { get; set; }
        public bool PackageInstalled { get; set; }
    }

    internal enum BundledCompanionState
    {
        Missing,
        VerificationFailed,
        Ready
    }

    internal sealed class BundledCompanionStatus
    {
        public BundledCompanionState State { get; set; }
        public string ApkPath { get; set; }
        public string Detail { get; set; }
        public int VersionCode { get; set; }
        public string VersionName { get; set; }
    }

    internal sealed class DisplayCleanupPermissionService
    {
        public const string PackageName = DXCompanionShared.CompanionRelease.PackageName;
        public const string PermissionName =
            "android.permission.WRITE_SECURE_SETTINGS";
        public const int BundledVersionCode = DXCompanionShared.CompanionRelease.VersionCode;
        public const string BundledVersionName = DXCompanionShared.CompanionRelease.VersionName;

        internal const string ExpectedCertificateSha256 = DXCompanionShared.CompanionRelease.CertificateSha256;
        private const string ExpectedBundledApkSha256 = DXCompanionShared.CompanionRelease.ApkSha256;
        private const string BundledApkRelativePath =
            @"tools\companion\DX-Companion.apk";

        private readonly AdbService _adbService;

        public DisplayCleanupPermissionService(AdbService adbService)
        {
            _adbService = adbService;
        }

        public DisplayCleanupPermissionStatus Inspect(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial) ||
                !_adbService.IsAuthorizedDeviceConnected(serial))
            {
                return Status(
                    DisplayCleanupPermissionState.NoDevice,
                    string.Empty,
                    serial);
            }

            try
            {
                var packageDump = _adbService.ShellForSerial(
                    serial,
                    "dumpsys package " + PackageName,
                    false);
                var packagePath = _adbService.ShellForSerial(
                    serial,
                    "pm path " + PackageName,
                    false);
                if (!packagePath.IsSuccess ||
                    string.IsNullOrWhiteSpace(packagePath.StandardOutput))
                {
                    return Status(
                        DisplayCleanupPermissionState.NotInstalled,
                        CombineError(packageDump, packagePath),
                        serial,
                        0,
                        false);
                }
                if (!packageDump.IsSuccess)
                {
                    return Status(
                        DisplayCleanupPermissionState.Error,
                        CombineOutput(packageDump),
                        serial,
                        0,
                        true);
                }

                var remoteApkPath = ParseBaseApkPath(
                    packagePath.StandardOutput);
                if (string.IsNullOrWhiteSpace(remoteApkPath) ||
                    !Regex.IsMatch(
                        packageDump.StandardOutput ?? string.Empty,
                        @"\bapkSigningVersion=2\b",
                        RegexOptions.CultureInvariant))
                {
                    return Status(
                        DisplayCleanupPermissionState.VerificationFailed,
                        "Installed package is not the expected v2-signed APK.",
                        serial,
                        ParseVersionCode(packageDump.StandardOutput),
                        true);
                }

                var certificate = ReadInstalledCertificate(
                    serial,
                    remoteApkPath);
                if (!string.Equals(
                    certificate,
                    ExpectedCertificateSha256,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return Status(
                        DisplayCleanupPermissionState.VerificationFailed,
                        "Installed APK signing certificate does not match.",
                        serial,
                        ParseVersionCode(packageDump.StandardOutput),
                        true);
                }

                return Status(
                    HasPermission(packageDump.StandardOutput)
                        ? DisplayCleanupPermissionState.Granted
                        : DisplayCleanupPermissionState.Ready,
                    string.Empty,
                    serial,
                    ParseVersionCode(packageDump.StandardOutput),
                    true);
            }
            catch (Exception ex)
            {
                return Status(
                    DisplayCleanupPermissionState.Error,
                    ex.Message,
                    serial);
            }
        }

        public BundledCompanionStatus InspectBundledApk()
        {
            var apkPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                BundledApkRelativePath);
            if (!File.Exists(apkPath))
            {
                return BundledStatus(
                    BundledCompanionState.Missing,
                    apkPath,
                    "The bundled DX Companion APK was not found.");
            }

            try
            {
                var hash = ComputeSha256(apkPath);
                if (!string.Equals(
                    hash,
                    ExpectedBundledApkSha256,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return BundledStatus(
                        BundledCompanionState.VerificationFailed,
                        apkPath,
                        "The bundled DX Companion APK hash does not match.");
                }

                var certificate = ApkSigningCertificateReader
                    .ReadSingleV2CertificateSha256(apkPath);
                if (!string.Equals(
                    certificate,
                    ExpectedCertificateSha256,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return BundledStatus(
                        BundledCompanionState.VerificationFailed,
                        apkPath,
                        "The bundled DX Companion signing certificate does not match.");
                }

                return BundledStatus(
                    BundledCompanionState.Ready,
                    apkPath,
                    string.Empty);
            }
            catch (Exception ex)
            {
                return BundledStatus(
                    BundledCompanionState.VerificationFailed,
                    apkPath,
                    ex.Message);
            }
        }

        public DisplayCleanupPermissionStatus InstallAndGrant(
            string serial)
        {
            var bundled = InspectBundledApk();
            if (bundled.State != BundledCompanionState.Ready)
            {
                return Status(
                    DisplayCleanupPermissionState.Error,
                    bundled.Detail,
                    serial);
            }
            if (string.IsNullOrWhiteSpace(serial) ||
                !_adbService.IsAuthorizedDeviceConnected(serial))
            {
                return Status(
                    DisplayCleanupPermissionState.NoDevice,
                    string.Empty,
                    serial);
            }

            var current = Inspect(serial);
            if (current.State ==
                    DisplayCleanupPermissionState.VerificationFailed)
            {
                return current;
            }
            if (current.State != DisplayCleanupPermissionState.NotInstalled &&
                current.State != DisplayCleanupPermissionState.Ready &&
                current.State != DisplayCleanupPermissionState.Granted)
            {
                return current;
            }
            if (current.PackageInstalled &&
                current.VersionCode > BundledVersionCode)
            {
                return Status(
                    DisplayCleanupPermissionState.Error,
                    "A newer DX Companion version is already installed.",
                    serial,
                    current.VersionCode,
                    true);
            }

            var install = _adbService.InstallPackageForSerial(
                serial,
                bundled.ApkPath,
                current.PackageInstalled);
            var installed = WaitForInstalledPackage(serial, 20000);
            if (!install.IsSuccess && !IsExpectedInstalledPackage(installed))
            {
                return Status(
                    DisplayCleanupPermissionState.Error,
                    CombineOutput(install),
                    serial,
                    current.VersionCode,
                    current.PackageInstalled);
            }

            if (!IsExpectedInstalledPackage(installed))
            {
                return Status(
                    DisplayCleanupPermissionState.Error,
                    "Post-install package, version, or signing verification failed.",
                    serial,
                    installed.VersionCode,
                    installed.PackageInstalled);
            }
            return installed.State == DisplayCleanupPermissionState.Granted
                ? installed
                : Grant(installed);
        }

        private DisplayCleanupPermissionStatus WaitForInstalledPackage(
            string serial,
            int timeoutMs)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(
                Math.Max(timeoutMs, 0));
            DisplayCleanupPermissionStatus current;
            do
            {
                current = Inspect(serial);
                if (IsExpectedInstalledPackage(current) ||
                    current.State ==
                        DisplayCleanupPermissionState.VerificationFailed ||
                    !_adbService.IsAuthorizedDeviceConnected(serial))
                {
                    return current;
                }
                if (DateTime.UtcNow >= deadline) return current;
                Thread.Sleep(500);
            }
            while (true);
        }

        private static bool IsExpectedInstalledPackage(
            DisplayCleanupPermissionStatus status)
        {
            return status != null &&
                (status.State == DisplayCleanupPermissionState.Ready ||
                 status.State == DisplayCleanupPermissionState.Granted) &&
                status.VersionCode == BundledVersionCode;
        }

        public DisplayCleanupPermissionStatus Uninstall(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial) ||
                !_adbService.IsAuthorizedDeviceConnected(serial))
            {
                return Status(
                    DisplayCleanupPermissionState.NoDevice,
                    string.Empty,
                    serial);
            }

            var current = Inspect(serial);
            if (!current.PackageInstalled)
                return current;
            var uninstall = _adbService.UninstallPackageForSerial(
                serial,
                PackageName);
            if (!uninstall.IsSuccess)
            {
                return Status(
                    DisplayCleanupPermissionState.Error,
                    CombineOutput(uninstall),
                    serial,
                    current.VersionCode,
                    true);
            }

            var verified = Inspect(serial);
            return verified.State == DisplayCleanupPermissionState.NotInstalled
                ? verified
                : Status(
                    DisplayCleanupPermissionState.Error,
                    "DX Companion is still installed after the uninstall command.",
                    serial,
                    verified.VersionCode,
                    verified.PackageInstalled);
        }

        public DisplayCleanupPermissionStatus Grant(
            DisplayCleanupPermissionStatus verifiedStatus)
        {
            if (verifiedStatus == null ||
                verifiedStatus.State !=
                    DisplayCleanupPermissionState.Ready ||
                string.IsNullOrWhiteSpace(verifiedStatus.Serial))
            {
                return Status(
                    DisplayCleanupPermissionState.VerificationFailed,
                    "The installed cleanup app has not been verified.",
                    verifiedStatus == null
                        ? string.Empty
                        : verifiedStatus.Serial);
            }

            var serial = verifiedStatus.Serial;
            if (!_adbService.IsAuthorizedDeviceConnected(serial))
            {
                return Status(
                    DisplayCleanupPermissionState.NoDevice,
                    string.Empty,
                    serial);
            }

            // Re-verify immediately before granting so a package replacement
            // or target-device change cannot reuse an earlier UI state.
            var current = Inspect(serial);
            if (current.State == DisplayCleanupPermissionState.Granted)
                return current;
            if (current.State != DisplayCleanupPermissionState.Ready ||
                !string.Equals(
                    current.Serial,
                    serial,
                    StringComparison.OrdinalIgnoreCase))
            {
                return current;
            }

            var grantResult = _adbService.ShellForSerial(
                serial,
                "pm grant " + PackageName + " " + PermissionName,
                true);
            if (!grantResult.IsSuccess)
            {
                return Status(
                    DisplayCleanupPermissionState.Error,
                    CombineOutput(grantResult),
                    serial,
                    current.VersionCode,
                    true);
            }

            var verified = Inspect(serial);
            if (verified.State != DisplayCleanupPermissionState.Granted)
            {
                // Close the package-replacement race window: if the app no
                // longer verifies immediately after pm grant, revoke the
                // permission before reporting failure.
                _adbService.ShellForSerial(
                    serial,
                    "pm revoke " + PackageName + " " + PermissionName,
                    true);
                return Status(
                    DisplayCleanupPermissionState.Error,
                    "Post-grant verification failed. The permission was revoked.",
                    serial,
                    verified.VersionCode,
                    verified.PackageInstalled);
            }
            return verified;
        }

        private string ReadInstalledCertificate(
            string serial,
            string remoteApkPath)
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "DXManager",
                "apk-verification",
                Guid.NewGuid().ToString("N"));
            var localApkPath = Path.Combine(directory, "base.apk");
            try
            {
                Directory.CreateDirectory(directory);
                var pull = _adbService.PullForSerial(
                    serial,
                    remoteApkPath,
                    localApkPath,
                    false);
                if (!pull.IsSuccess || !File.Exists(localApkPath))
                    throw new InvalidOperationException(
                        "Could not read the installed cleanup APK: " +
                        CombineOutput(pull));
                return ApkSigningCertificateReader
                    .ReadSingleV2CertificateSha256(localApkPath);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(directory))
                        Directory.Delete(directory, true);
                }
                catch
                {
                    // Verification result must not be changed by temp cleanup.
                }
            }
        }

        private static string ParseBaseApkPath(string output)
        {
            if (string.IsNullOrWhiteSpace(output)) return string.Empty;
            var lines = output.Split(
                new[] { "\r\n", "\n" },
                StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (!line.StartsWith(
                    "package:",
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var path = line.Substring("package:".Length).Trim();
                if (path.EndsWith(
                    "/base.apk",
                    StringComparison.OrdinalIgnoreCase))
                {
                    return path;
                }
            }
            return string.Empty;
        }

        private static bool HasPermission(string packageDump)
        {
            if (string.IsNullOrWhiteSpace(packageDump)) return false;
            return Regex.IsMatch(
                packageDump,
                @"android\.permission\.WRITE_SECURE_SETTINGS\s*:\s*granted=true\b",
                RegexOptions.CultureInvariant |
                RegexOptions.IgnoreCase);
        }

        private static int ParseVersionCode(string packageDump)
        {
            if (string.IsNullOrWhiteSpace(packageDump)) return 0;
            var match = Regex.Match(
                packageDump,
                @"\bversionCode=(\d+)\b",
                RegexOptions.CultureInvariant |
                RegexOptions.IgnoreCase);
            int value;
            return match.Success &&
                int.TryParse(match.Groups[1].Value, out value)
                ? value
                : 0;
        }

        private static DisplayCleanupPermissionStatus Status(
            DisplayCleanupPermissionState state,
            string detail,
            string serial,
            int versionCode = 0,
            bool packageInstalled = false)
        {
            return new DisplayCleanupPermissionStatus
            {
                State = state,
                Detail = detail ?? string.Empty,
                Serial = serial ?? string.Empty,
                VersionCode = versionCode,
                PackageInstalled = packageInstalled
            };
        }

        private static BundledCompanionStatus BundledStatus(
            BundledCompanionState state,
            string apkPath,
            string detail)
        {
            return new BundledCompanionStatus
            {
                State = state,
                ApkPath = apkPath ?? string.Empty,
                Detail = detail ?? string.Empty,
                VersionCode = BundledVersionCode,
                VersionName = BundledVersionName
            };
        }

        private static string ComputeSha256(string path)
        {
            using (var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read))
            using (var sha256 = SHA256.Create())
            {
                return BitConverter.ToString(
                    sha256.ComputeHash(stream)).Replace("-", string.Empty);
            }
        }

        private static string CombineError(params Models.ProcessResult[] results)
        {
            foreach (var result in results)
            {
                var value = CombineOutput(result);
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
            return string.Empty;
        }

        private static string CombineOutput(Models.ProcessResult result)
        {
            if (result == null) return string.Empty;
            var value = string.IsNullOrWhiteSpace(result.StandardError)
                ? result.StandardOutput
                : result.StandardError;
            return (value ?? string.Empty).Trim();
        }
    }
}
