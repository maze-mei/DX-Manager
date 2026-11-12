using System;
using System.IO;
using System.Text;

namespace DexManager.Services
{
    internal static class CompanionGuardianProtocol
    {
        public const int Magic = 0x44584744; // DXGD
        public const int Version = 3;
        public const byte Ping = 1;
        public const byte WindowsShutdown = 2;
        public const byte StopMonitoring = 3;
        public const byte ForegroundApp = 4;
        public const byte AccessibilityStatus = 5;
        public const byte Pong = 6;
        public const byte ForegroundWindow = 7;
        public const int MaxStringBytes = 64 * 1024;

        public static int ReadInt32(Stream stream)
        {
            var buffer = ReadExact(stream, 4);
            return (buffer[0] << 24) |
                (buffer[1] << 16) |
                (buffer[2] << 8) |
                buffer[3];
        }

        public static long ReadInt64(Stream stream)
        {
            var bytes = ReadExact(stream, 8);
            long value = 0;
            foreach (var b in bytes) value = (value << 8) | b;
            return value;
        }

        public static void WriteInt64(Stream stream, long value)
        {
            var bytes = new byte[8];
            for (var i = 7; i >= 0; i--) { bytes[i] = (byte)value; value >>= 8; }
            stream.Write(bytes, 0, bytes.Length);
        }

        public static string ReadString(Stream stream)
        {
            return ReadString(stream, MaxStringBytes);
        }

        public static string ReadString(Stream stream, int maxBytes)
        {
            var length = ReadInt32(stream);
            if (length < 0 || length > maxBytes)
                throw new InvalidDataException(
                    "Invalid guardian string length.");
            return Encoding.UTF8.GetString(ReadExact(stream, length));
        }

        public static void WriteInt32(Stream stream, int value)
        {
            var buffer = new[]
            {
                (byte)(value >> 24),
                (byte)(value >> 16),
                (byte)(value >> 8),
                (byte)value
            };
            stream.Write(buffer, 0, buffer.Length);
        }

        public static void WriteString(Stream stream, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            WriteInt32(stream, bytes.Length);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static byte[] ReadExact(Stream stream, int count)
        {
            var buffer = new byte[count];
            var offset = 0;
            while (offset < count)
            {
                var read = stream.Read(buffer, offset, count - offset);
                if (read <= 0) throw new EndOfStreamException();
                offset += read;
            }
            return buffer;
        }
    }
}
