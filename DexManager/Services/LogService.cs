using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DexManager.Services
{
    public sealed class LogService
    {
        private readonly object _syncRoot = new object();
        private readonly List<string> _sessionEntries = new List<string>();
        private string _logDirectory;

        public LogService()
        {
            _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        }

        public event EventHandler<LogEventArgs> EntryWritten;

        public string LogDirectory
        {
            get { return _logDirectory; }
        }

        public void SetLogDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) return;
            _logDirectory = Path.GetFullPath(directory);
            DeleteLegacyLogs();
        }

        public string[] GetSessionEntries()
        {
            lock (_syncRoot)
            {
                return _sessionEntries.ToArray();
            }
        }

        public void SaveSession(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Log save path is empty.", "path");

            string[] entries;
            lock (_syncRoot)
            {
                entries = _sessionEntries.ToArray();
            }

            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllLines(path, entries, new UTF8Encoding(true));
        }

        public void Info(string message)
        {
            Write("INFO", message, null);
        }

        public void Warning(string message)
        {
            Write("WARN", message, null);
        }

        public void Error(string message, Exception exception)
        {
            Write("ERROR", message, exception);
        }

        private void Write(string level, string message, Exception exception)
        {
            var timestamp = DateTime.Now;
            var builder = new StringBuilder();
            builder.Append(timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            builder.Append(" [");
            builder.Append(level);
            builder.Append("] ");
            builder.Append(message ?? string.Empty);

            if (exception != null)
            {
                builder.AppendLine();
                builder.Append(exception);
            }

            var line = builder.ToString();

            lock (_syncRoot)
            {
                _sessionEntries.Add(line);
            }

            var handler = EntryWritten;
            if (handler != null)
            {
                handler(this, new LogEventArgs(timestamp, level, line));
            }
        }

        private void DeleteLegacyLogs()
        {
            try
            {
                if (!Directory.Exists(_logDirectory)) return;

                foreach (var path in Directory.GetFiles(_logDirectory, "*.log"))
                    File.Delete(path);
            }
            catch
            {
                // Old logs must never prevent application startup.
            }
        }
    }

    public sealed class LogEventArgs : EventArgs
    {
        public LogEventArgs(DateTime timestamp, string level, string message)
        {
            Timestamp = timestamp;
            Level = level;
            Message = message;
        }

        public DateTime Timestamp { get; private set; }
        public string Level { get; private set; }
        public string Message { get; private set; }
    }
}
