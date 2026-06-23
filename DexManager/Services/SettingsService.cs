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
                Save(defaults);
                _logService.Info("기본 설정 파일을 생성했습니다: " + SettingsFilePath);
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
                if (originalSchemaVersion != settings.SchemaVersion)
                {
                    Save(settings);
                    _logService.Info(
                        "설정 스키마를 " + originalSchemaVersion +
                        "에서 " + settings.SchemaVersion + "로 갱신했습니다.");
                }

                _logService.Info("설정 파일을 불러왔습니다: " + SettingsFilePath);
                return settings;
            }
            catch (Exception ex)
            {
                var backupPath = SettingsFilePath + ".invalid-" +
                    DateTime.Now.ToString("yyyyMMdd-HHmmss");
                File.Copy(SettingsFilePath, backupPath, true);
                _logService.Error(
                    "설정 파일을 읽지 못해 기본 설정을 사용합니다. 백업: " + backupPath,
                    ex);

                var defaults = AppSettings.CreateDefault();
                Save(defaults);
                return defaults;
            }
        }

        public void Save(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");

            settings.EnsureDefaults();
            var directory = Path.GetDirectoryName(SettingsFilePath);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

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
            _logService.Info("설정 파일을 저장했습니다: " + SettingsFilePath);
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
