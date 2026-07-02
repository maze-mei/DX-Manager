using System;
using System.Reflection;
using Microsoft.Win32;

namespace DexManager.Services
{
    public sealed class AutoStartService
    {
        private const string RunKeyPath =
            @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "DXManager";
        private const string LegacyValueName = "DexManager";
        private readonly LogService _logService;

        public AutoStartService(LogService logService)
        {
            _logService = logService;
        }

        public bool IsRegistered()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
            {
                return key != null &&
                    (key.GetValue(ValueName) != null ||
                     key.GetValue(LegacyValueName) != null);
            }
        }

        public void Apply(bool enabled)
        {
            if (enabled)
                Register();
            else if (IsRegistered())
                Unregister();
        }

        public void Register()
        {
            var executablePath = Assembly.GetEntryAssembly().Location;
            var command = "\"" + executablePath + "\" --autorun";

            using (var key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
            {
                if (key == null)
                    throw new InvalidOperationException("자동 실행 레지스트리를 열 수 없습니다.");
                key.SetValue(ValueName, command, RegistryValueKind.String);
                key.DeleteValue(LegacyValueName, false);
            }

            _logService.Info("Windows 자동 실행을 등록했습니다.");
        }

        public void Unregister()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
            {
                if (key != null)
                {
                    key.DeleteValue(ValueName, false);
                    key.DeleteValue(LegacyValueName, false);
                }
            }

            _logService.Info("Windows 자동 실행 등록을 해제했습니다.");
        }
    }
}
