using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;

// Uses the actual product assembly. Installation metadata is synthetic, while the
// bundled APK verifier and TCP status protocol execute their real implementations.
// The runner blocks every ADB launch; no phone or system ADB server is accessed.
internal static class PhoneTransferIntegrationTests
{
    private const string Serial = "fixture-phone";
    private static readonly BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private static int passed;
    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        passed++; Console.WriteLine("PASS " + name);
    }
    private static void Set(object instance, string name, object value)
    {
        instance.GetType().GetField(name, Hidden).SetValue(instance, value);
    }
    private static object Get(object instance, string name)
    {
        return instance.GetType().GetField(name, Hidden).GetValue(instance);
    }
    private static bool Eligible(string state, int version, string serial = Serial)
    {
        var type = typeof(AdbService).Assembly.GetType("DexManager.Services.DisplayCleanupPermissionStatus");
        var status = Activator.CreateInstance(type);
        var stateProperty = type.GetProperty("State");
        stateProperty.SetValue(status, Enum.Parse(stateProperty.PropertyType, state), null);
        type.GetProperty("VersionCode").SetValue(status, version, null);
        type.GetProperty("Serial").SetValue(status, serial, null);
        return (bool)typeof(PhoneTransferReceiver).GetMethod("CanConfigureTransfer",
            BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { status, (object)Serial });
    }
    private static PhoneTransferReceiver Receiver(AppSettings settings, LogService log)
    {
        var runner = new ProcessRunner(log); runner.BlockNewProcessesForWindowsShutdown();
        var adb = new AdbService(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "unused-adb.exe"), 1000, runner, log);
        return new PhoneTransferReceiver(adb, new SettingsService(log), settings, log, new DeviceRuntimeSessionRegistry());
    }
    private static void Int(Stream stream, int value)
    {
        stream.Write(new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value }, 0, 4);
    }
    private static void String(Stream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value); Int(stream, bytes.Length); stream.Write(bytes, 0, bytes.Length);
    }
    private static void Read(Stream stream, byte[] bytes)
    {
        for (int offset = 0; offset < bytes.Length;)
        {
            int count = stream.Read(bytes, offset, bytes.Length - offset);
            if (count <= 0) throw new EndOfStreamException(); offset += count;
        }
    }
    private static bool Probe(int port, string token)
    {
        using (var client = new TcpClient())
        {
            client.Connect(IPAddress.Loopback, port);
            using (var stream = client.GetStream())
            {
                stream.ReadTimeout = 3000; stream.WriteTimeout = 3000;
                Int(stream, 0x44584D52); Int(stream, 1); String(stream, token);
                String(stream, "__DXM_STATUS_PROBE__"); stream.Flush();
                int status = stream.ReadByte(); var lengthBytes = new byte[4]; Read(stream, lengthBytes);
                int length = (lengthBytes[0] << 24) | (lengthBytes[1] << 16) | (lengthBytes[2] << 8) | lengthBytes[3];
                if (status < 0 || length < 0 || length > 1024 * 1024) throw new IOException("Invalid status response.");
                Read(stream, new byte[length]); return status == 1;
            }
        }
    }
    private static bool RejectsToken(int port, string token)
    {
        try { return !Probe(port, token); }
        catch (EndOfStreamException) { return true; }
        catch (IOException ex)
        {
            var socket = ex.InnerException as SocketException;
            if (socket != null && (socket.SocketErrorCode == SocketError.ConnectionReset ||
                socket.SocketErrorCode == SocketError.ConnectionAborted)) return true;
            throw;
        }
    }
    private static int Main()
    {
        try
        {
            var log = new LogService();
            var runner = new ProcessRunner(log); runner.BlockNewProcessesForWindowsShutdown();
            var adb = new AdbService(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "unused-adb.exe"), 1000, runner, log);
            var verifierType = typeof(AdbService).Assembly.GetType("DexManager.Services.DisplayCleanupPermissionService");
            var verifier = Activator.CreateInstance(verifierType, new object[] { adb });
            var bundle = verifierType.GetMethod("InspectBundledApk").Invoke(verifier, null);
            Check(bundle.GetType().GetProperty("State").GetValue(bundle, null).ToString() == "Ready",
                "actual verifier accepts the signed shared Companion bundle");
            Check((int)verifierType.GetField("BundledVersionCode").GetRawConstantValue() == 16,
                "release bundle uses official Companion code16");
            Check(Eligible("Granted", 16), "verified shared Companion code16 is eligible for transfer registration");
            Check(Eligible("Ready", 16), "file transfer can connect when cleanup permission has not been granted");
            Check(Eligible("Granted", 17), "a newer verified official version remains eligible");
            Check(!Eligible("Granted", 15) && !Eligible("Granted", 7) && !Eligible("Ready", 0), "older official versions stay rejected");
            foreach (var state in new[] { "NoDevice", "NotInstalled", "VerificationFailed", "Error" })
                Check(!Eligible(state, 16), "untrusted or unavailable installation remains rejected: " + state);
            Check(!Eligible("Granted", 16, "another-phone"), "another phone cannot configure the current transfer session");
            var certificateReader = typeof(AdbService).Assembly.GetType("DexManager.Utils.ApkSigningCertificateReader");
            var foreignCertificate = (string)certificateReader.GetMethod("ReadSingleV2CertificateSha256",
                BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "foreign-signer.apk") });
            Check(foreignCertificate != (string)verifierType.GetField("ExpectedCertificateSha256",
                BindingFlags.NonPublic | BindingFlags.Static).GetRawConstantValue(),
                "private APK signer is separate from the accepted official identity");
            var settings = AppSettings.CreateDefault(); settings.Features.PhoneToPcTransferEnabled = true;
            using (var receiver = Receiver(settings, log))
            {
                // Simulate the session installed after successful configuration; all calls
                // below use the product's actual listener/authentication/probe path.
                Set(receiver, "_serial", Serial); Set(receiver, "_token", "fixture-token-one");
                typeof(PhoneTransferReceiver).GetMethod("EnsureListener", Hidden).Invoke(receiver, null);
                int port = ((IPEndPoint)((TcpListener)Get(receiver, "_listener")).LocalEndpoint).Port;
                Check(Probe(port, "fixture-token-one"), "actual Companion DXMR status probe reports connected");
                Check(RejectsToken(port, "wrong-fixture-token"), "wrong token cannot report a ready connection");
                var detach = typeof(PhoneTransferReceiver).GetMethod("DetachInternal", Hidden, null,
                    new[] { typeof(bool), typeof(bool) }, null);
                detach.Invoke(receiver, new object[] { false, false });
                Check(RejectsToken(port, "fixture-token-one"), "detach rejects the old session token");
                Set(receiver, "_serial", Serial); Set(receiver, "_token", "fixture-token-two");
                Check(Probe(port, "fixture-token-two") && RejectsToken(port, "fixture-token-one"),
                    "reconnection uses a fresh token and restores the ready status");
                receiver.RequestWindowsShutdown();
            }
            settings.Features.PhoneToPcTransferEnabled = false;
            using (var receiver = Receiver(settings, log))
            {
                receiver.AttachAsync(Serial).GetAwaiter().GetResult();
                Check(Get(receiver, "_serial") == null, "transfer OFF does not start an attachment");
                receiver.RequestWindowsShutdown();
            }
            Console.WriteLine("PASS actual Companion transfer integration: " + passed);
            return 0;
        }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }
}
