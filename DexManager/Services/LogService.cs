using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DexManager.Services
{
    public enum LogEntryKind { General, Command, Response, Progress, ToolOutput }

    public sealed partial class LogService : IDisposable
    {
        private readonly object _syncRoot = new object();
        private readonly List<string> _sessionEntries = new List<string>();
        private readonly List<LogEventArgs> _sessionEvents = new List<LogEventArgs>();
        private readonly HashSet<string> _onceKeys =
            new HashSet<string>(StringComparer.Ordinal);
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
        }

        public string[] GetSessionEntries()
        {
            lock (_syncRoot)
            {
                return _sessionEntries.ToArray();
            }
        }

        public LogEventArgs[] GetSessionEvents() {
            lock (_syncRoot) return _sessionEvents.ToArray();
        }
        public void Command(string message) { Write("INFO",message,null,LogEntryKind.Command); }
        public void Response(string message) { Write("INFO",message,null,LogEntryKind.Response); }
        public void Progress(string message) { Write("INFO",message,null,LogEntryKind.Progress); }
        public void ProgressOnce(string key,string message) {
            if(string.IsNullOrWhiteSpace(key)){Progress(message);return;}
            lock(_syncRoot){if(!_onceKeys.Add(key))return;}
            Write("INFO",message,null,LogEntryKind.Progress);
        }
        public void ToolOutput(string level,string message) { Write(level,message,null,LogEntryKind.ToolOutput); }

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

        public void InfoOnce(string key, string message)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                Info(message);
                return;
            }
            lock (_syncRoot)
            {
                if (!_onceKeys.Add(key)) return;
            }
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

        private void Write(string level, string message, Exception exception, LogEntryKind kind = LogEntryKind.General)
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
            var args = new LogEventArgs(timestamp,level,line,kind);

            lock (_syncRoot)
            {
                _sessionEntries.Add(line);
                _sessionEvents.Add(args);
                PersistAutomaticLine(level, line);
            }

            var handler = EntryWritten;
            if (handler != null)
            {
                foreach (var subscriber in handler.GetInvocationList())
                {
                    try
                    {
                        ((EventHandler<LogEventArgs>)subscriber)(this, args);
                    }
                    catch
                    {
                        // Logging subscribers must not change application state.
                    }
                }
            }
        }

    }

    public sealed class LogEventArgs : EventArgs
    {
        public LogEventArgs(DateTime timestamp, string level, string message) : this(timestamp,level,message,LogEntryKind.General) { }
        public LogEventArgs(DateTime timestamp, string level, string message, LogEntryKind kind)
        {
            Kind=kind;
            Timestamp = timestamp;
            Level = level;
            Message = message;
        }

        public LogEntryKind Kind { get; private set; }
        public DateTime Timestamp { get; private set; }
        public string Level { get; private set; }
        public string Message { get; private set; }
    }
}
