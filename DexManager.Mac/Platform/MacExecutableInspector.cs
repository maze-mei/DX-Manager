using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace DexManager.Mac.Platform;

/// <summary>
/// Inspects Mach-O headers so DX Manager only selects bundled tools that the
/// current Mac can execute. The repository ships scrcpy and the ADB proxy per
/// CPU architecture, and picking the other slice (for example the x86_64 build
/// on an Apple Silicon Mac without Rosetta) makes the process exit immediately.
/// </summary>
public static class MacExecutableInspector
{
    private const uint CpuTypeX86 = 7;
    private const uint CpuTypeX86_64 = 0x01000007;
    private const uint CpuTypeArm64 = 0x0100000C;

    private const int FatArchSize32 = 20;
    private const int FatArchSize64 = 32;

    /// <summary>
    /// Returns true when <paramref name="path"/> exists and contains a slice
    /// for the running CPU. Files that are not Mach-O binaries (for example
    /// shell scripts or Windows executables) are treated as runnable so this
    /// check never rejects something it cannot judge.
    /// </summary>
    public static bool IsRunnableOnThisMachine(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;

        var supported = GetSupportedCpuTypes();
        if (supported == null) return true;

        try
        {
            byte[] header;
            using (var stream = new FileStream(
                       path,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.ReadWrite))
            {
                var buffer = new byte[4096];
                var read = stream.Read(buffer, 0, buffer.Length);
                if (read <= 0) return true;
                header = read == buffer.Length ? buffer : buffer[..read];
            }

            var slices = ReadMachOCpuTypes(header);
            if (slices == null) return true;

            foreach (var cpuType in slices)
            {
                foreach (var expected in supported)
                {
                    if (cpuType == expected) return true;
                }
            }

            return false;
        }
        catch
        {
            // A file we cannot read is handled by the caller's own error path.
            return false;
        }
    }

    private static uint[] GetSupportedCpuTypes()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => [CpuTypeArm64],
            Architecture.X64 => [CpuTypeX86_64, CpuTypeX86],
            Architecture.X86 => [CpuTypeX86],
            _ => null
        };
    }

    private static IReadOnlyList<uint> ReadMachOCpuTypes(byte[] data)
    {
        if (data.Length < 8) return null;

        // Thin Mach-O, little endian (0xFEEDFACE / 0xFEEDFACF stored little endian)
        if (MatchesMagic(data, 0xCE, 0xFA, 0xED, 0xFE) ||
            MatchesMagic(data, 0xCF, 0xFA, 0xED, 0xFE))
        {
            return [ReadUInt32(data, 4, false)];
        }

        // Thin Mach-O, big endian
        if (MatchesMagic(data, 0xFE, 0xED, 0xFA, 0xCE) ||
            MatchesMagic(data, 0xFE, 0xED, 0xFA, 0xCF))
        {
            return [ReadUInt32(data, 4, true)];
        }

        // Universal (fat) binary, big endian: FAT_MAGIC / FAT_MAGIC_64
        if (MatchesMagic(data, 0xCA, 0xFE, 0xBA, 0xBE) ||
            MatchesMagic(data, 0xCA, 0xFE, 0xBA, 0xBF))
        {
            var is64 = data[3] == 0xBF;
            return ReadFatCpuTypes(data, true, is64 ? FatArchSize64 : FatArchSize32);
        }

        // Universal (fat) binary, swapped endianness
        if (MatchesMagic(data, 0xBE, 0xBA, 0xFE, 0xCA) ||
            MatchesMagic(data, 0xBF, 0xBA, 0xFE, 0xCA))
        {
            var is64 = data[0] == 0xBF;
            return ReadFatCpuTypes(data, false, is64 ? FatArchSize64 : FatArchSize32);
        }

        return null;
    }

    private static IReadOnlyList<uint> ReadFatCpuTypes(
        byte[] data,
        bool bigEndian,
        int entrySize)
    {
        var count = ReadUInt32(data, 4, bigEndian);
        if (count == 0 || count > 64) return null;

        var cpuTypes = new List<uint>((int)count);
        for (var index = 0; index < count; index++)
        {
            var offset = 8 + (index * entrySize);
            if (offset + 4 > data.Length) break;
            cpuTypes.Add(ReadUInt32(data, offset, bigEndian));
        }

        return cpuTypes.Count == 0 ? null : cpuTypes;
    }

    private static bool MatchesMagic(byte[] data, byte b0, byte b1, byte b2, byte b3)
    {
        return data[0] == b0 && data[1] == b1 && data[2] == b2 && data[3] == b3;
    }

    private static uint ReadUInt32(byte[] data, int offset, bool bigEndian)
    {
        if (offset + 4 > data.Length) return 0;
        var span = data.AsSpan(offset, 4);
        return bigEndian
            ? BinaryPrimitives.ReadUInt32BigEndian(span)
            : BinaryPrimitives.ReadUInt32LittleEndian(span);
    }
}