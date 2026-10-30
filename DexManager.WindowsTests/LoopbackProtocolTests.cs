using DexManager.Models;
using DexManager.Services;

namespace DexManager.WindowsTests;

internal static class LoopbackProtocolTests
{
    internal static readonly Action[] All =
    {
        ShellCommandIsBoundToUniqueSafeToken,
        InvalidTokensAreRejected,
        ReadyMessageRequiresExactPositiveDisplayId,
        PowerControlArgumentsAreRejected,
        NonPowerArgumentsAreAllowed,
        LoopbackSettingsKeepPhoneOnWithoutChangingSavedPreference
    };

    private static void ShellCommandIsBoundToUniqueSafeToken()
    {
        const string token = "0123456789abcdef0123456789abcdef";
        var path = LoopbackDexProtocol.CreateRemotePath(token);
        Require(path == "/data/local/tmp/dxm-loopback-" + token + ".jar",
            "Each helper must have a bounded session-specific path.");
        Require(LoopbackDexProtocol.BuildShellCommand(token) ==
                "CLASSPATH=" + path + " app_process / com.dxmanager.loopback.Main " + token,
            "The command must include only the validated path/token and the fixed entry point.");
        Require(path != LoopbackDexProtocol.CreateRemotePath("11111111111111111111111111111111"),
            "Two sessions must never use the same helper path.");
    }

    private static void InvalidTokensAreRejected()
    {
        var invalid = new[] { null, "", "123", new string('a', 31), new string('a', 33),
            "ABCDEF0123456789ABCDEF0123456789", new string('g', 32),
            "0123456789abcdef0123456789abcdef\n", "../../etc/passwd", "a;reboot", "$(id)" };
        foreach (var token in invalid)
        {
            Reject<ArgumentException>(() => LoopbackDexProtocol.CreateRemotePath(token), token);
            Reject<ArgumentException>(() => LoopbackDexProtocol.BuildShellCommand(token), token);
        }
    }

    private static void ReadyMessageRequiresExactPositiveDisplayId()
    {
        Require(LoopbackDexProtocol.TryParseReady("DXM_LOOPBACK_READY 1", out var one) && one == 1,
            "The first valid display ID must parse.");
        Require(LoopbackDexProtocol.TryParseReady("DXM_LOOPBACK_READY 2147483647", out var maximum) && maximum == int.MaxValue,
            "The largest valid signed display ID must parse.");
        var invalid = new[] { null, "", "DXM_LOOPBACK_READY 0", "DXM_LOOPBACK_READY -1",
            "DXM_LOOPBACK_READY 01", "DXM_LOOPBACK_READY +1", "DXM_LOOPBACK_READY 2147483648",
            "DXM_LOOPBACK_READY 9\n", "DXM_LOOPBACK_READY 9 ", " DXM_LOOPBACK_READY 9",
            "log: DXM_LOOPBACK_READY 9", "DXM_LOOPBACK_READY 1;reboot", "DXM_LOOPBACK_READY 1 2" };
        foreach (var line in invalid)
            Require(!LoopbackDexProtocol.TryParseReady(line, out var id) && id == 0,
                "Malformed or ambiguous display ID must be rejected: " + line);
    }

    private static void PowerControlArgumentsAreRejected()
    {
        var invalid = new[] { "-S", "-fS", "-Sf", "--turn-screen-off", "--turn-screen-off=true",
            "--turn-s", "--power-off-on-close", "--power-off", "--no-power-on", "--no-p",
            "--video-codec=h264 -S", "\"--turn-screen-off\"" };
        foreach (var arguments in invalid)
            Reject<InvalidOperationException>(() => LoopbackDexProtocol.ValidateAdditionalArguments(arguments), arguments);
    }

    private static void NonPowerArgumentsAreAllowed()
    {
        foreach (var arguments in new[] { null, "", "--video-codec=h264 --max-fps=60", "--no-audio", "-f", "--stay-awake" })
            LoopbackDexProtocol.ValidateAdditionalArguments(arguments);
    }

    private static void LoopbackSettingsKeepPhoneOnWithoutChangingSavedPreference()
    {
        var source = AppSettings.CreateDefault().Scrcpy;
        source.TurnScreenOff = true;
        source.BitRate = "12M";
        source.MaxFps = 90;
        source.AdditionalArguments = "--no-audio";
        var effective = LoopbackDexProtocol.CreateScrcpySettings(source);
        Require(!ReferenceEquals(source, effective), "Effective settings must be a copy.");
        Require(!effective.TurnScreenOff && source.TurnScreenOff,
            "The phone must stay on without changing the ordinary mode preference.");
        Require(effective.BitRate == "12M" && effective.MaxFps == 90 && effective.AdditionalArguments == "--no-audio",
            "Unrelated settings must be preserved.");
        Reject<ArgumentNullException>(() => LoopbackDexProtocol.CreateScrcpySettings(null), "null source");
        source.AdditionalArguments = "-S";
        Reject<InvalidOperationException>(() => LoopbackDexProtocol.CreateScrcpySettings(source), "power override");
    }

    private static void Reject<T>(Action action, string input) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name + " for " + input);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
