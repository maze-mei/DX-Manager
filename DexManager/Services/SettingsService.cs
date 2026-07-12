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
        {
            _logService = logService;
            BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
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
                        var tempPath = SettingsFilePath + ".tmp";
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

            _logService.Info(LocalizationService.Format(
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

            var tempPath = SettingsFilePath + ".tmp";
            var backupPath = SettingsFilePath + ".bak";
            using (var stream = new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None))
            {
                CreateSerializer().WriteObject(stream, settings);
                stream.Flush(true);
            }

            if (File.Exists(SettingsFilePath))
            {
                if (preserveExistingBackup)
                {
                    File.Replace(
                        tempPath,
                        SettingsFilePath,
                        null,
                        true);
                }
                else
                {
                    ReplaceWithBackupPreserved(
                        tempPath,
                        backupPath);
                }
            }
            else
            {
                File.Move(tempPath, SettingsFilePath);
            }
            _logService.Info(LocalizationService.Format(
                "Log.Settings.Saved",
                SettingsFilePath));
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
        }
    }
}
