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
            if (!File.Exists(SettingsFilePath))
            {
                var defaults = AppSettings.CreateDefault();
                LocalizationService.Apply(defaults.Language);
                Save(defaults);
                _logService.Info(LocalizationService.Format(
                    "Log.Settings.CreatedDefaults",
                    SettingsFilePath));
                return defaults;
            }

            try
            {
                AppSettings settings;
                using (var stream = File.OpenRead(SettingsFilePath))
                {
                    var serializer = CreateSerializer();
                    settings = (AppSettings)serializer.ReadObject(stream);
                }

                var originalSchemaVersion = settings.SchemaVersion;
                settings.EnsureDefaults();
                LocalizationService.Apply(settings.Language);
                if (originalSchemaVersion != settings.SchemaVersion)
                {
                    Save(settings);
                    _logService.Info(LocalizationService.Format(
                        "Log.Settings.SchemaUpdated",
                        originalSchemaVersion,
                        settings.SchemaVersion));
                }

                _logService.Info(LocalizationService.Format(
                    "Log.Settings.Loaded",
                    SettingsFilePath));
                return settings;
            }
            catch (Exception ex)
            {
                var backupPath = SettingsFilePath + ".invalid-" +
                    DateTime.Now.ToString("yyyyMMdd-HHmmss");
                File.Copy(SettingsFilePath, backupPath, true);
                _logService.Error(LocalizationService.Format(
                    "Log.Settings.LoadFailed",
                    backupPath),
                    ex);

                var defaults = AppSettings.CreateDefault();
                Save(defaults);
                return defaults;
            }
        }

        public void Save(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");

            lock (_saveSync)
            {
                settings.EnsureDefaults();
                var directory = Path.GetDirectoryName(SettingsFilePath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var tempPath = SettingsFilePath + ".tmp";
                using (var stream = File.Create(tempPath))
                {
                    CreateSerializer().WriteObject(stream, settings);
                }

                if (File.Exists(SettingsFilePath))
                {
                    File.Delete(SettingsFilePath);
                }

                File.Move(tempPath, SettingsFilePath);
                _logService.Info(LocalizationService.Format(
                    "Log.Settings.Saved",
                    SettingsFilePath));
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
    }
}
