using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using DexManager.FileTransfer;

namespace DexManager.AdbProxy;

internal static class Program
{
    private const int PipeConnectTimeoutMs = 5000;

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0] == "--self-test")
            {
                var version = Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion ?? "unknown";
                Console.WriteLine(
                    $"DX Manager ADB proxy {version} self-test passed.");
                return 0;
            }

            var realAdbPath = Environment.GetEnvironmentVariable(
                FileTransferEnvironment.RealAdbPath);
            if (string.IsNullOrWhiteSpace(realAdbPath) ||
                !File.Exists(realAdbPath))
            {
                return Fail("DX Manager: the real ADB executable is unavailable.");
            }

            var currentProcessPath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(currentProcessPath) &&
                PathsReferToSameFile(realAdbPath, currentProcessPath))
            {
                return Fail("DX Manager: recursive ADB proxy configuration was rejected.");
            }

            if (IsManagedTransferEnabled() &&
                TryParseManagedPush(args, out var push))
            {
                return RequestManagedTransfer(push);
            }

            return ForwardToRealAdb(realAdbPath, args);
        }
        catch (Exception ex)
        {
            return Fail("DX Manager ADB proxy failed: " + ex.Message);
        }
    }

    private static bool IsManagedTransferEnabled()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable(
                FileTransferEnvironment.Enabled),
            "1",
            StringComparison.Ordinal);
    }

    private static bool TryParseManagedPush(
        string[] args,
        out PushRequest request)
    {
        request = null;
        if (args == null || args.Length == 0) return false;

        string serial = null;
        var index = 0;
        while (index < args.Length)
        {
            var argument = args[index] ?? string.Empty;
            if (argument is "-s" or "--serial")
            {
                if (++index >= args.Length) return false;
                serial = args[index++];
                continue;
            }
            if (argument.StartsWith("--serial=", StringComparison.Ordinal))
            {
                serial = argument["--serial=".Length..];
                index++;
                continue;
            }
            if (argument is "-H" or "-P" or "-L" or "-t" or "--one-device")
            {
                index += 2;
                if (index > args.Length) return false;
                continue;
            }
            if (argument is "-d" or "-e" or "-a")
            {
                index++;
                continue;
            }
            if (argument.StartsWith("-", StringComparison.Ordinal))
                return false;
            break;
        }

        if (index >= args.Length ||
            !string.Equals(args[index], "push",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        index++;

        while (index < args.Length && IsPushOption(args[index])) index++;
        if (args.Length - index != 2) return false;

        var localPath = args[index];
        var remotePath = args[index + 1];
        if (string.IsNullOrWhiteSpace(localPath) ||
            !IsManagedRemoteDirectory(remotePath))
        {
            return false;
        }

        serial ??= Environment.GetEnvironmentVariable(FileTransferEnvironment.SessionSerial);
        if (string.IsNullOrWhiteSpace(serial)) return false;

        request = new PushRequest(serial, localPath, remotePath);
        return true;
    }

    private static bool IsPushOption(string value) =>
        value is "-n" or "-p" or "-q" or "-Z" or "--sync";

    private static bool IsManagedRemoteDirectory(string value)
    {
        var configured = Environment.GetEnvironmentVariable(
            FileTransferEnvironment.RemoteDirectory);
        if (string.IsNullOrWhiteSpace(configured))
            configured = FileTransferEnvironment.DefaultRemoteDirectory;
        return string.Equals(
            NormalizeRemoteDirectory(value),
            NormalizeRemoteDirectory(configured),
            StringComparison.Ordinal);
    }

    private static string NormalizeRemoteDirectory(string value) =>
        (value ?? string.Empty)
            .Replace('\\', '/')
            .TrimEnd('/');

    private static int RequestManagedTransfer(PushRequest push)
    {
        var pipeName = Environment.GetEnvironmentVariable(
            FileTransferEnvironment.PipeName);
        var token = Environment.GetEnvironmentVariable(
            FileTransferEnvironment.PipeToken);
        var sessionId = Environment.GetEnvironmentVariable(
            FileTransferEnvironment.SessionId);
        if (string.IsNullOrWhiteSpace(pipeName) ||
            string.IsNullOrWhiteSpace(token) ||
            string.IsNullOrWhiteSpace(sessionId))
        {
            return Fail("DX Manager file transfer is not available for this session.");
        }

        try
        {
            using var pipe = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.None);
            pipe.Connect(PipeConnectTimeoutMs);
            FileTransferWire.Write(pipe, new FileTransferRequestMessage
            {
                Version = FileTransferEnvironment.ProtocolVersion,
                Token = token,
                SessionId = sessionId,
                RequestId = Guid.NewGuid().ToString("N"),
                Serial = push.Serial,
                LocalPath = push.LocalPath,
                RemoteDirectory = push.RemoteDirectory
            });

            var response = FileTransferWire.Read<FileTransferResponseMessage>(pipe);
            if (response == null ||
                response.Version != FileTransferEnvironment.ProtocolVersion)
            {
                return Fail("DX Manager file transfer protocol mismatch.");
            }
            if (!response.Success)
            {
                return Fail(string.IsNullOrWhiteSpace(response.Message)
                    ? "DX Manager file transfer failed."
                    : response.Message,
                    response.ExitCode == 0 ? 1 : response.ExitCode);
            }
            return 0;
        }
        catch (TimeoutException)
        {
            return Fail("DX Manager file transfer service did not respond.");
        }
        catch (IOException ex)
        {
            return Fail("DX Manager file transfer connection failed: " + ex.Message);
        }
    }

    private static int ForwardToRealAdb(
        string realAdbPath,
        IEnumerable<string> args)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = realAdbPath,
                Arguments = WindowsCommandLine.Build(args),
                WorkingDirectory = Path.GetDirectoryName(realAdbPath),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.Start();
        var standardOutput = StartStreamPump(
            process.StandardOutput.BaseStream,
            Console.OpenStandardOutput(),
            false,
            () => TryKill(process));
        var standardError = StartStreamPump(
            process.StandardError.BaseStream,
            Console.OpenStandardError(),
            false,
            () => TryKill(process));
        StartStreamPump(
            Console.OpenStandardInput(),
            process.StandardInput.BaseStream,
            true,
            null);
        process.WaitForExit();
        standardOutput.Join();
        standardError.Join();
        return process.ExitCode;
    }

    private static Thread StartStreamPump(
        Stream source,
        Stream destination,
        bool closeDestination,
        Action failed)
    {
        var thread = new Thread(() =>
        {
            try
            {
                var buffer = new byte[8192];
                int read;
                while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    destination.Write(buffer, 0, read);
                    destination.Flush();
                }
            }
            catch (IOException)
            {
                failed?.Invoke();
            }
            catch (ObjectDisposedException)
            {
            }
            finally
            {
                if (closeDestination)
                {
                    try { destination.Close(); }
                    catch { }
                }
            }
        })
        {
            IsBackground = true,
            Name = "DX Manager ADB stream relay"
        };
        thread.Start();
        return thread;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (process is { HasExited: false })
                process.Kill();
        }
        catch
        {
        }
    }

    private static bool PathsReferToSameFile(string left, string right)
    {
        try
        {
            var leftNorm = Path.GetFullPath(left).TrimEnd('/', '\\');
            var rightNorm = Path.GetFullPath(right).TrimEnd('/', '\\');
            return string.Equals(
                leftNorm,
                rightNorm,
                OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static int Fail(string message, int exitCode = 1)
    {
        try { Console.Error.WriteLine(message); }
        catch { }
        return exitCode;
    }

    private sealed record PushRequest(string Serial, string LocalPath, string RemoteDirectory);
}
