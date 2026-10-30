using System.Runtime.Serialization.Json;
using System.Text;
using DexManager.Models;
using DexManager.Services;

namespace DexManager.WindowsTests;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "-s") return FakeAdb.Run(args);
        var tests = new Action[]
        {
            NewSettingsKeepExistingPhonePreview,
            LegacySettingsKeepExistingPhonePreview,
            MissingVirtualDisplayKeepsExistingPhonePreview,
            PhonePreviewSettingSurvivesNormalization,
            PhonePreviewSettingSurvivesClone,
            DevicePhonePreviewSettingsAreIndependent,
            PhonePreviewSettingsRoundTrip,
            PhonePreviewSettingCanReturnToExistingMode
        }.Concat(LoopbackProtocolTests.All).Concat(LoopbackLifecycleTests.All).ToArray();
        var failed = 0;
        foreach (var test in tests)
        {
            try
            {
                test();
                Console.WriteLine("PASS " + test.Method.Name);
            }
            catch (Exception ex)
            {
                failed++;
                Console.Error.WriteLine("FAIL " + test.Method.Name + ": " + ex);
            }
        }
        Console.WriteLine($"Windows source regression tests: {tests.Length - failed}/{tests.Length} passed.");
        return failed == 0 ? 0 : 1;
    }

    private static void NewSettingsKeepExistingPhonePreview()
    {
        var settings = AppSettings.CreateDefault();
        Require(!settings.VirtualDisplay.HidePhonePreview, "New installs must retain the overlay mode.");
        Require(!settings.GetOrCreateDeviceRunSettings("phone-a").VirtualDisplay.HidePhonePreview,
            "New device profiles must retain the overlay mode.");
    }

    private static void LegacySettingsKeepExistingPhonePreview()
    {
        const string json = "{\"SchemaVersion\":25,\"VirtualDisplay\":{\"Width\":1920,\"Height\":1080,\"Dpi\":240,\"Suffix\":\"hdmi\",\"ReuseExistingDisplay\":true},\"DeviceRunSettingsProfiles\":[{\"DeviceIdentity\":\"phone-a\",\"VirtualDisplay\":{\"Width\":1600,\"Height\":900,\"Dpi\":150,\"Suffix\":\"hdmi\"}}]}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var settings = (AppSettings)new DataContractJsonSerializer(typeof(AppSettings)).ReadObject(stream);
        settings.EnsureDefaults();
        Require(!settings.VirtualDisplay.HidePhonePreview, "An absent setting must deserialize to false.");
        Require(!settings.GetOrCreateDeviceRunSettings("phone-a").VirtualDisplay.HidePhonePreview,
            "Legacy per-device settings must not opt into experimental mode.");
        Require(settings.VirtualDisplay.Width == 1920 && settings.VirtualDisplay.Dpi == 240,
            "Existing dimensions must be preserved.");
    }

    private static void MissingVirtualDisplayKeepsExistingPhonePreview()
    {
        var settings = new AppSettings();
        settings.EnsureDefaults();
        Require(!settings.VirtualDisplay.HidePhonePreview, "Missing display settings must use safe defaults.");
    }

    private static void PhonePreviewSettingSurvivesNormalization()
    {
        var settings = AppSettings.CreateDefault();
        settings.VirtualDisplay.HidePhonePreview = true;
        settings.GetOrCreateDeviceRunSettings("phone-a").VirtualDisplay.HidePhonePreview = true;
        settings.EnsureDefaults();
        Require(settings.VirtualDisplay.HidePhonePreview, "Normalization must retain explicit opt-in.");
        Require(settings.GetOrCreateDeviceRunSettings("phone-a").VirtualDisplay.HidePhonePreview,
            "Normalization must retain device opt-in.");
    }

    private static void PhonePreviewSettingSurvivesClone()
    {
        var original = AppSettings.CreateDefault().VirtualDisplay;
        original.HidePhonePreview = true;
        var clone = AppSettings.CloneVirtualDisplay(original);
        Require(clone.HidePhonePreview, "Cloning must preserve the selected mode.");
        clone.HidePhonePreview = false;
        Require(original.HidePhonePreview, "A clone must not mutate the original.");
    }

    private static void DevicePhonePreviewSettingsAreIndependent()
    {
        var settings = AppSettings.CreateDefault();
        var first = settings.GetOrCreateDeviceRunSettings("phone-a");
        var second = settings.GetOrCreateDeviceRunSettings("phone-b");
        first.VirtualDisplay.HidePhonePreview = true;
        Require(!second.VirtualDisplay.HidePhonePreview, "Changing one phone must not change another.");
        Require(!settings.VirtualDisplay.HidePhonePreview, "Changing one phone must not change the global defaults.");
        Require(settings.GetOrCreateDeviceRunSettings("PHONE-A").VirtualDisplay.HidePhonePreview,
            "The same identity must retain its mode.");
    }

    private static void PhonePreviewSettingsRoundTrip()
    {
        WithTemporarySettings(service =>
        {
            var settings = AppSettings.CreateDefault();
            settings.VirtualDisplay.HidePhonePreview = true;
            settings.GetOrCreateDeviceRunSettings("phone-a").VirtualDisplay.HidePhonePreview = true;
            settings.GetOrCreateDeviceRunSettings("phone-b").VirtualDisplay.HidePhonePreview = false;
            service.Save(settings);
            var loaded = service.Load();
            Require(loaded.VirtualDisplay.HidePhonePreview, "Global mode must survive save/load.");
            Require(loaded.GetOrCreateDeviceRunSettings("phone-a").VirtualDisplay.HidePhonePreview,
                "First device mode must survive save/load.");
            Require(!loaded.GetOrCreateDeviceRunSettings("phone-b").VirtualDisplay.HidePhonePreview,
                "Second device mode must remain independent after save/load.");
        });
    }

    private static void PhonePreviewSettingCanReturnToExistingMode()
    {
        WithTemporarySettings(service =>
        {
            var settings = AppSettings.CreateDefault();
            settings.VirtualDisplay.HidePhonePreview = true;
            settings.VirtualDisplay.Width = 2560;
            settings.VirtualDisplay.Height = 1440;
            settings.VirtualDisplay.Dpi = 220;
            settings.Scrcpy.TurnScreenOff = true;
            service.Save(settings);
            var loaded = service.Load();
            loaded.VirtualDisplay.HidePhonePreview = false;
            service.Save(loaded);
            var restored = service.Load();
            Require(!restored.VirtualDisplay.HidePhonePreview,
                "Turning the option off must be persisted.");
            Require(restored.VirtualDisplay.Width == 2560 && restored.VirtualDisplay.Height == 1440 &&
                    restored.VirtualDisplay.Dpi == 220 && restored.Scrcpy.TurnScreenOff,
                "Returning to overlay mode must preserve dimensions, DPI and screen-off preference.");
        });
    }

    private static void WithTemporarySettings(Action<SettingsService> test)
    {
        var directory = Path.Combine(Path.GetTempPath(), "dxm-windows-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            test(new SettingsService(new LogService(), directory));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
