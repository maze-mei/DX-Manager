using System.Diagnostics;

namespace DexManager.WindowsTests;

// Only the external ADB process is replaced. All Windows service code is linked
// unchanged and exercises real redirected processes, stdout, stdin and teardown.
internal static class FakeAdb
{
    internal const string LogVariable = "DXM_TEST_FAKE_ADB_LOG";

    internal static int Run(string[] args)
    {
        var log = Environment.GetEnvironmentVariable(LogVariable);
        if (string.IsNullOrEmpty(log) || args.Length < 3 || args[0] != "-s") return 90;
        var serial = args[1];
        if (!serial.StartsWith("dxm-test-", StringComparison.Ordinal)) return 91;
        var command = string.Join(" ", args.Skip(2));
        Record(log, "COMMAND|" + serial + "|" + command);
        if (args[2] == "push") return serial == "dxm-test-push-failure" ? 1 : 0;
        if (args[2] == "get-state")
        {
            if (serial == "dxm-test-disconnected") return 1;
            Console.WriteLine("device");
            return 0;
        }
        if (command == "shell getprop ro.serialno")
        {
            var changedAfterPush = serial == "dxm-test-replaced" &&
                File.ReadAllText(log).Contains("|push ", StringComparison.Ordinal);
            Console.WriteLine(changedAfterPush ? "different-phone" : serial);
            return 0;
        }
        if (command == "shell settings get global overlay_display_devices")
        {
            Console.WriteLine(serial == "dxm-test-overlay" ? "1600x900/150,hdmi" : "null");
            return 0;
        }
        if (command.StartsWith("shell rm -f /data/local/tmp/dxm-loopback-", StringComparison.Ordinal))
            return 0;
        if (command.Contains(" app_process / com.dxmanager.loopback.Main ", StringComparison.Ordinal))
        {
            var token = args[args.Length - 1].Split(' ').Last();
            Record(log, "START|" + serial + "|" + Environment.ProcessId + "|" + token);
            if (serial == "dxm-test-early-exit")
            {
                Console.WriteLine("DXM_LOOPBACK_ERROR fake unsupported device");
                return 2;
            }
            if (serial != "dxm-test-no-ready")
            {
                Console.WriteLine("DXM_LOOPBACK_READY " + (serial == "dxm-test-second" ? "42" : "41"));
                Console.Out.Flush();
            }
            if (serial == "dxm-test-stubborn")
            {
                Thread.Sleep(60000);
                return 0;
            }
            string line;
            while ((line = Console.ReadLine()) != null)
            {
                if (line == "STOP " + token)
                {
                    Record(log, "STOP|" + serial + "|" + token);
                    if (serial == "dxm-test-unconfirmed")
                        Console.WriteLine("DXM_LOOPBACK_ERROR cleanup_unconfirmed");
                    Console.WriteLine("DXM_LOOPBACK_STOPPED");
                    return 0;
                }
            }
            Record(log, "EOF|" + serial + "|" + token);
            return 0;
        }
        Console.Error.WriteLine("Unexpected fake ADB command: " + command);
        return 92;
    }

    private static void Record(string path, string line)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.AppendAllText(path, line + Environment.NewLine);
                return;
            }
            catch (IOException) when (attempt < 20) { Thread.Sleep(10); }
        }
    }
}
