using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using DexManager.Models;

namespace DexManager.Services
{
    public sealed class SettingsService
    {
        private readonly LogService _logService;
        private readonly object _saveSync = new object();

        public SettingsService(LogService logService)
            : this(logService, AppDomain.CurrentDomain.BaseDirectory)
        {
        }

        internal SettingsService(
            LogService logService,
            string baseDirectory)
        {
            if (logService == null)
                throw new ArgumentNullException("logService");
            if (string.IsNullOrWhiteSpace(baseDirectory))
                throw new ArgumentException(
                    "Base directory is empty.",
                    "baseDirectory");

            _logService = logService;
            BaseDirectory = Path.GetFullPath(baseDirectory);
            SettingsFilePath = Path.Combine(BaseDirectory, "config", "settings.json");
        }

        public string BaseDirectory { get; private set; }
        public string SettingsFilePath { get; private set; }

        public AppSettings Load()
        {
            AppSettings settings;
            int originalSchemaVersion;
            bool settingsNormalized;
            Exception primaryException;
            var sourcePath = SettingsFilePath;
            if (!TryLoadCandidate(
                SettingsFilePath,
                out settings,
                out originalSchemaVersion,
                out settingsNormalized,
                out primaryException))
            {
                ThrowIfUnsupportedSchema(primaryException);
                if (primaryException != null)
                {
                    var invalidBackup = PreserveInvalidSettingsFile();
                    _logService.Error(LocalizationService.Format(
                        "Log.Settings.PrimaryInvalid",
                        invalidBackup),
                        primaryException);
                }

                Exception recoveryException;
                var backupPath = SettingsFilePath + ".bak";
                if (TryLoadCandidate(
                    backupPath,
                    out settings,
                    out originalSchemaVersion,
                    out settingsNormalized,
                    out recoveryException))
                {
                    sourcePath = backupPath;
                }
                else
                {
                    ThrowIfUnsupportedSchema(recoveryException);
                    var preservedBackupPath = backupPath + ".previous";
                    if (TryLoadCandidate(
                        preservedBackupPath,
                        out settings,
                        out originalSchemaVersion,
                        out settingsNormalized,
                        out recoveryException))
                    {
                        sourcePath = preservedBackupPath;
                    }
                    else
                    {
                        ThrowIfUnsupportedSchema(recoveryException);
                        var tempPath = FindRecoveryTempPath();
                        if (TryLoadCandidate(
                            tempPath,
                            out settings,
                            out originalSchemaVersion,
                            out settingsNormalized,
                            out recoveryException))
                        {
                            sourcePath = tempPath;
                        }
                        else
                        {
                            ThrowIfUnsupportedSchema(recoveryException);
                            var defaults = AppSettings.CreateDefault();
                            LocalizationService.Apply(defaults.Language);
                            Save(defaults);
                            _logService.Info(LocalizationService.Format(
                                "Log.Settings.CreatedDefaults",
                                SettingsFilePath));
                            return defaults;
                        }
                    }
                }
            }

            LocalizationService.Apply(settings.Language);
            if (!string.Equals(
                sourcePath,
                SettingsFilePath,
                StringComparison.OrdinalIgnoreCase))
            {
                _logService.Warning(LocalizationService.Format(
                    "Log.Settings.Recovered",
                    sourcePath));
                try
                {
                    SaveRecovered(settings);
                    if (sourcePath.EndsWith(
                        ".tmp",
                        StringComparison.OrdinalIgnoreCase))
                        TryDeleteFile(sourcePath);
                }
                catch (Exception ex)
                {
                    _logService.Error(
                        LocalizationService.Get(
                            "Log.Settings.RecoverySaveFailed"),
                        ex);
                }
            }
            else if (originalSchemaVersion != settings.SchemaVersion ||
                settingsNormalized)
            {
                try
                {
                    Save(settings);
                    if (originalSchemaVersion != settings.SchemaVersion)
                    {
                        _logService.Info(LocalizationService.Format(
                            "Log.Settings.SchemaUpdated",
                            originalSchemaVersion,
                            settings.SchemaVersion));
                    }
                    else
                    {
                        _logService.Info(LocalizationService.Get(
                            "Log.Settings.Normalized"));
                    }
                }
                catch (Exception ex)
                {
                    _logService.Error(
                        LocalizationService.Get(
                            "Log.Settings.SchemaSaveFailed"),
                        ex);
                }
            }

            _logService.Progress(LocalizationService.Format(
                "Log.Settings.Loaded",
                sourcePath));
            return settings;
        }

        public void Save(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");

            lock (_saveSync)
            {
                SaveCore(settings);
            }
        }

        public AppSettings Clone(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            lock (_saveSync)
            {
                return CloneCore(settings);
            }
        }

        public void SaveAndApply(
            AppSettings liveSettings,
            AppSettings candidate)
        {
            if (liveSettings == null)
                throw new ArgumentNullException("liveSettings");
            if (candidate == null)
                throw new ArgumentNullException("candidate");

            lock (_saveSync)
            {
                SaveCore(candidate);
                CopySettings(liveSettings, candidate);
            }
        }

        public void UpdateAndSave(
            AppSettings liveSettings,
            Action<AppSettings> update)
        {
            if (liveSettings == null)
                throw new ArgumentNullException("liveSettings");
            if (update == null) throw new ArgumentNullException("update");

            lock (_saveSync)
            {
                var candidate = CloneCore(liveSettings);
                update(candidate);
                SaveCore(candidate);
                CopySettings(liveSettings, candidate);
            }
        }

        public void UpdateInMemory(
            AppSettings liveSettings,
            Action<AppSettings> update)
        {
            if (liveSettings == null)
                throw new ArgumentNullException("liveSettings");
            if (update == null) throw new ArgumentNullException("update");
            lock (_saveSync)
            {
                update(liveSettings);
            }
        }

        public string ResolvePath(string configuredPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath)) return string.Empty;
            if (Path.IsPathRooted(configuredPath)) return Path.GetFullPath(configuredPath);
            return Path.GetFullPath(Path.Combine(BaseDirectory, configuredPath));
        }

        private static DataContractJsonSerializer CreateSerializer()
        {
            return new DataContractJsonSerializer(
                typeof(AppSettings),
                new DataContractJsonSerializerSettings
                {
                    UseSimpleDictionaryFormat = true
                });
        }

        private bool TryLoadCandidate(
            string path,
            out AppSettings settings,
            out int originalSchemaVersion,
            out bool settingsNormalized,
            out Exception error)
        {
            settings = null;
            originalSchemaVersion = 0;
            settingsNormalized = false;
            error = null;
            if (!File.Exists(path)) return false;

            try
            {
                using (var stream = File.Open(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read))
                {
                    settings = (AppSettings)CreateSerializer().ReadObject(
                        stream);
                }
                if (settings == null)
                    throw new InvalidDataException(
                        "The settings file contains no settings object.");
                if (settings.SchemaVersion >
                    AppSettings.CurrentSchemaVersion)
                {
                    throw new NotSupportedException(
                        LocalizationService.Format(
                            "Error.Settings.NewerSchema",
                            settings.SchemaVersion,
                            AppSettings.CurrentSchemaVersion));
                }
                var beforeNormalization = Serialize(settings);
                originalSchemaVersion = settings.SchemaVersion;
                settings.EnsureDefaults();
                settingsNormalized = !ByteArraysEqual(
                    beforeNormalization,
                    Serialize(settings));
                return true;
            }
            catch (Exception ex)
            {
                settings = null;
                originalSchemaVersion = 0;
                settingsNormalized = false;
                error = ex;
                return false;
            }
        }

        private string PreserveInvalidSettingsFile()
        {
            var backupPath = SettingsFilePath + ".invalid-" +
                DateTime.Now.ToString("yyyyMMdd-HHmmss");
            try
            {
                File.Copy(SettingsFilePath, backupPath, true);
                return backupPath;
            }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Get(
                        "Log.Settings.BackupFailed"),
                    ex);
                return SettingsFilePath;
            }
        }

        private static void ThrowIfUnsupportedSchema(Exception error)
        {
            var unsupported = error as NotSupportedException;
            if (unsupported != null)
                throw new NotSupportedException(
                    unsupported.Message,
                    unsupported);
        }

        private void SaveRecovered(AppSettings settings)
        {
            lock (_saveSync)
            {
                SaveCore(settings, true);
            }
        }

        private void SaveCore(AppSettings settings)
        {
            SaveCore(settings, false);
        }

        private void SaveCore(
            AppSettings settings,
            bool preserveExistingBackup)
        {
            if (settings.SchemaVersion >
                AppSettings.CurrentSchemaVersion)
            {
                throw new InvalidDataException(
                    "Settings from a newer DX Manager version cannot " +
                    "be overwritten.");
            }
            settings.EnsureDefaults();
            var directory = Path.GetDirectoryName(SettingsFilePath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var backupPath = SettingsFilePath + ".bak";
            using (AcquireSaveFileLock())
            {
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    var tempPath = CreateUniqueTempPath();
                    try
                    {
                        WriteTempSettings(tempPath, settings);
                        CommitTempSettings(
                            tempPath,
                            backupPath,
                            preserveExistingBackup);
                        break;
                    }
                    catch (FileNotFoundException)
                    {
                        if (attempt != 0) throw;
                    }
                    finally
                    {
                        TryDeleteFile(tempPath);
                    }
                }
            }
            _logService.Info(LocalizationService.Format(
                "Log.Settings.Saved",
                SettingsFilePath));
        }

        private FileStream AcquireSaveFileLock()
        {
            var lockPath = SettingsFilePath + ".lock";
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (true)
            {
                try
                {
                    return new FileStream(
                        lockPath,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None);
                }
                catch (IOException)
                {
                    if (DateTime.UtcNow >= deadline) throw;
                    System.Threading.Thread.Sleep(25);
                }
            }
        }

        private string CreateUniqueTempPath()
        {
            return SettingsFilePath + "." +
                Guid.NewGuid().ToString("N") + ".tmp";
        }

        private void WriteTempSettings(
            string tempPath,
            AppSettings settings)
        {
            using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                CreateSerializer().WriteObject(stream, settings);
                stream.Flush(true);
            }
        }

        private void CommitTempSettings(
            string tempPath,
            string backupPath,
            bool preserveExistingBackup)
        {
            if (!File.Exists(SettingsFilePath))
            {
                File.Move(tempPath, SettingsFilePath);
                return;
            }

            if (preserveExistingBackup)
            {
                File.Replace(
                    tempPath,
                    SettingsFilePath,
                    null,
                    true);
                return;
            }

            ReplaceWithBackupPreserved(tempPath, backupPath);
        }

        private string FindRecoveryTempPath()
        {
            var legacyTempPath = SettingsFilePath + ".tmp";
            if (File.Exists(legacyTempPath)) return legacyTempPath;

            var directory = Path.GetDirectoryName(SettingsFilePath);
            if (string.IsNullOrWhiteSpace(directory) ||
                !Directory.Exists(directory))
                return legacyTempPath;

            try
            {
                var pattern = Path.GetFileName(SettingsFilePath) + ".*.tmp";
                FileInfo newest = null;
                foreach (var path in Directory.GetFiles(directory, pattern))
                {
                    var candidate = new FileInfo(path);
                    if (newest == null ||
                        candidate.LastWriteTimeUtc > newest.LastWriteTimeUtc)
                        newest = candidate;
                }
                return newest == null ? legacyTempPath : newest.FullName;
            }
            catch (IOException)
            {
                return legacyTempPath;
            }
            catch (UnauthorizedAccessException)
            {
                return legacyTempPath;
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
            }
        }

        private void ReplaceWithBackupPreserved(
            string tempPath,
            string backupPath)
        {
            var preservedPath = backupPath + ".previous";
            var hadBackup = File.Exists(backupPath);
            if (hadBackup)
                File.Copy(backupPath, preservedPath, true);

            var replaced = false;
            var restored = false;
            try
            {
                if (hadBackup) File.Delete(backupPath);
                File.Replace(
                    tempPath,
                    SettingsFilePath,
                    backupPath,
                    true);
                replaced = true;
            }
            catch
            {
                if (hadBackup && File.Exists(preservedPath))
                {
                    try
                    {
                        File.Copy(preservedPath, backupPath, true);
                        restored = true;
                    }
                    catch (Exception ex)
                    {
                        _logService.Error(
                            LocalizationService.Get(
                                "Log.Settings.BackupFailed"),
                            ex);
                    }
                }
                throw;
            }
            finally
            {
                if (File.Exists(preservedPath) &&
                    (replaced || restored))
                {
                    try
                    {
                        File.Delete(preservedPath);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static AppSettings CloneCore(AppSettings settings)
        {
            using (var stream = new MemoryStream())
            {
                var serializer = CreateSerializer();
                serializer.WriteObject(stream, settings);
                stream.Position = 0;
                var copy = (AppSettings)serializer.ReadObject(stream);
                copy.EnsureDefaults();
                return copy;
            }
        }

        private static byte[] Serialize(AppSettings settings)
        {
            using (var stream = new MemoryStream())
            {
                CreateSerializer().WriteObject(stream, settings);
                return stream.ToArray();
            }
        }

        private static bool ByteArraysEqual(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null ||
                left.Length != right.Length) return false;
            for (var index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index]) return false;
            }
            return true;
        }

        private static void CopySettings(
            AppSettings target,
            AppSettings source)
        {
            target.SchemaVersion = source.SchemaVersion;
            target.Paths = source.Paths;
            target.VirtualDisplay = source.VirtualDisplay;
            target.Scrcpy = source.Scrcpy;
            target.Timing = source.Timing;
            target.Features = source.Features;
            target.KeyMappings = source.KeyMappings;
            target.LastSuccess = source.LastSuccess;
            target.SingleWindowSlots = source.SingleWindowSlots;
            target.Connection = source.Connection;
            target.Language = source.Language;
            target.Theme = source.Theme;
            target.RememberedApps = source.RememberedApps;
            target.SingleWindowAppProfiles =
                source.SingleWindowAppProfiles;
            target.DeviceRunSettingsProfiles =
                source.DeviceRunSettingsProfiles;
            target.DeviceWirelessConnectionProfiles =
                source.DeviceWirelessConnectionProfiles;
            target.RecentCustomResolutions =
                source.RecentCustomResolutions;
        }
    }
}
