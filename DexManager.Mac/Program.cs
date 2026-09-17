using System.Reflection;
using System.Text;
using DexManager.Mac.Hosting;

namespace DexManager.Mac;

internal static class Program
{
    internal static string Version =>
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "unknown";

    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Length > 0)
        {
            var firstArg = args[0].ToLowerInvariant();
            if (firstArg is "-h" or "--help")
            {
                PrintHelp();
                return 0;
            }
            if (firstArg is "-v" or "--version")
            {
                Console.WriteLine($"DX Manager for macOS - Version {Version}");
                return 0;
            }
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cts.Cancel();
        };

        try
        {
            using var host = new InteractiveHost();

            if (args.Length > 0)
            {
                var cmd = args[0].ToLowerInvariant();
                switch (cmd)
                {
                    case "--diag" or "-d":
                        await host.RunDiagnosticsAsync(cts.Token);
                        return 0;

                    case "--dex" or "-x":
                        if (!await host.StartDexAsync(cts.Token))
                        {
                            return 1;
                        }
                        AnsiConsole.Info("DeX launched. Press Ctrl+C to stop...");
                        try
                        {
                            while (!cts.Token.IsCancellationRequested &&
                                   host.IsDexRunning)
                            {
                                await Task.Delay(500, cts.Token);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            // Expected on Ctrl+C
                        }
                        if (!cts.Token.IsCancellationRequested)
                        {
                            try
                            {
                                if (await host.WaitForDexCleanupAsync(cts.Token))
                                {
                                    return 0;
                                }
                                if (!cts.Token.IsCancellationRequested)
                                {
                                    return 1;
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                // Continue through the atomic stop/cleanup path.
                            }
                        }
                        return await host.StopDexAsync() ? 0 : 1;

                    case "--stop-dex":
                        return await host.StopDexAsync(
                            cleanupUntrackedOverlay: true,
                            cancellationToken: cts.Token)
                            ? 0
                            : 1;

                    default:
                        AnsiConsole.Warning($"Unknown option: '{args[0]}'. Showing help:");
                        PrintHelp();
                        return 1;
                }
            }

            await host.RunAsync(cts.Token);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.Error($"Fatal error: {ex.Message}");
            return 1;
        }
    }

    private static void PrintHelp()
    {
        AnsiConsole.Header($"DX MANAGER for macOS (.NET 8) - v{Version}");
        Console.WriteLine("Usage:");
        Console.WriteLine("  DXManager.Mac              # Start Interactive Dashboard");
        Console.WriteLine("  DXManager.Mac --dex         # Launch DeX immediately (Ctrl+C to stop)");
        Console.WriteLine("  DXManager.Mac --stop-dex    # Stop active DeX session");
        Console.WriteLine("  DXManager.Mac --diag        # Run system diagnostics");
        Console.WriteLine("  DXManager.Mac --version     # Display version info");
        Console.WriteLine("  DXManager.Mac --help        # Show this help message");
        Console.WriteLine();
        Console.WriteLine("Source developers may use: dotnet run --project DexManager.Mac -- [option]");
        Console.WriteLine();
    }
}
