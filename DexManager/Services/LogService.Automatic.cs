using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DexManager.Services
{
    public sealed partial class LogService
    {
        private volatile AutomaticLogWriter _automaticLogs;
        private int _automaticFailureReported;

        public string AutomaticLogDirectory { get { var writer=_automaticLogs;return writer==null?string.Empty:writer.DirectoryPath; } }
        public string AutomaticSessionId { get { var writer=_automaticLogs;return writer==null?string.Empty:writer.SessionId; } }
        public string AutomaticLogError { get { var writer=_automaticLogs;return writer==null?string.Empty:writer.Error; } }

        public void EnableAutomaticLogs(string applicationDirectory)
        {
            EnableAutomaticLogs(applicationDirectory, new AutomaticLogOptions());
        }

        internal void EnableAutomaticLogs(string applicationDirectory, AutomaticLogOptions options)
        {
            lock (_syncRoot)
            {
                if (_automaticLogs != null) return;
                _automaticLogs = new AutomaticLogWriter(applicationDirectory, options, delegate(Exception error)
                {
                    if (Interlocked.Exchange(ref _automaticFailureReported,1)==0)
                        Warning("Automatic log saving is unavailable; in-memory logging remains active: "+error.Message);
                });
                foreach (var line in _sessionEntries) PersistAutomaticLine("INFO", line);
            }
        }

        private void PersistAutomaticLine(string level, string line)
        {
            var writer = _automaticLogs;
            if (writer != null) writer.Enqueue(line, true,
                level=="WARN" || level=="ERROR" || line.IndexOf("[Native DeX]",StringComparison.Ordinal)>=0);
        }

        // Detail-only records do not flood the session-log UI. No file I/O occurs on the caller.
        public void Diagnostic(string area, string message)
        {
            var writer = _automaticLogs;
            if (writer == null) return;
            writer.Enqueue(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff",CultureInfo.InvariantCulture)+
                " [DETAIL] ["+(area??"General")+"] "+(message??string.Empty), false, true);
        }

        public bool FlushAutomaticLogs(int timeoutMs)
        {
            var writer = _automaticLogs;
            return writer == null || writer.Flush(Math.Max(0,timeoutMs));
        }

        public void CompleteAutomaticLogs(int timeoutMs)
        {
            var writer = _automaticLogs;
            if (writer != null) writer.Complete(Math.Max(0,timeoutMs));
        }

        public void ConfigureAutomaticLogs(bool enabled,string applicationDirectory)
        {
            if(enabled){EnableAutomaticLogs(applicationDirectory);return;}
            AutomaticLogWriter writer;
            lock(_syncRoot){writer=_automaticLogs;_automaticLogs=null;}
            if(writer!=null)writer.Complete(2000);
        }

        public void Dispose() { CompleteAutomaticLogs(2000); }
    }

    internal sealed class AutomaticLogOptions
    {
        internal int RetainedSessions=20, QueueCapacity=2048, SessionBytes=4*1024*1024,
            DiagnosticBytes=2*1024*1024, EntryCharacters=8192;
        internal Func<string,Stream> OpenStream = null;
    }

    internal sealed class AutomaticLogWriter
    {
        private sealed class Record
        {
            internal string Text;
            internal bool Session, Diagnostic;
            internal TaskCompletionSource<bool> Barrier;
        }
        private readonly BlockingCollection<Record> _queue;
        private readonly Thread _worker;
        private readonly AutomaticLogOptions _options;
        private readonly Action<Exception> _failure;
        private readonly int _entryLimit;
        private int _stopped, _dropped;
        private volatile string _error=string.Empty;
        internal string DirectoryPath { get; private set; }
        internal string SessionId { get; private set; }
        internal string Error { get { return _error; } }

        internal AutomaticLogWriter(string applicationDirectory, AutomaticLogOptions options, Action<Exception> failure)
        {
            if(options==null || options.RetainedSessions<1 || options.QueueCapacity<1 ||
                options.SessionBytes<1024 || options.DiagnosticBytes<1024 || options.EntryCharacters<128)
                throw new ArgumentException("Invalid automatic log limits.");
            _options=options;_failure=failure;
            DirectoryPath=Path.Combine(Path.GetFullPath(applicationDirectory),"logs");
            using(var process=Process.GetCurrentProcess())
                SessionId=DateTime.Now.ToString("yyyyMMdd-HHmmssfff",CultureInfo.InvariantCulture)+"-"+
                    process.Id.ToString(CultureInfo.InvariantCulture)+"-"+Guid.NewGuid().ToString("N").Substring(0,8);
            _entryLimit=Math.Min(options.EntryCharacters,(Math.Min(options.SessionBytes,options.DiagnosticBytes)-512)/4);
            _queue=new BlockingCollection<Record>(options.QueueCapacity);
            _worker=new Thread(WriteLoop){IsBackground=true,Name="DXM-Automatic-Logs"};
            _worker.Start();
        }

        internal void Enqueue(string text, bool session, bool diagnostic)
        {
            if(Volatile.Read(ref _stopped)!=0)return;
            var value=text??string.Empty;
            if(value.Length>_entryLimit)value=value.Substring(0,_entryLimit-32)+" [entry truncated]";
            try
            {
                if(!_queue.TryAdd(new Record{Text=value,Session=session,Diagnostic=diagnostic}))
                    Interlocked.Increment(ref _dropped);
            }
            catch(InvalidOperationException) { }
        }

        internal bool Flush(int timeoutMs)
        {
            if(Volatile.Read(ref _stopped)!=0)return false;
            var barrier=new TaskCompletionSource<bool>();
            var elapsed=Stopwatch.StartNew();
            try
            {
                if(!_queue.TryAdd(new Record{Barrier=barrier},Math.Min(timeoutMs,50)))return false;
                return barrier.Task.Wait(Math.Max(0,timeoutMs-(int)elapsed.ElapsedMilliseconds)) && barrier.Task.Result;
            }
            catch(InvalidOperationException) { return false; }
        }

        internal void Complete(int timeoutMs)
        {
            if(Interlocked.Exchange(ref _stopped,1)==0)_queue.CompleteAdding();
            if(Thread.CurrentThread!=_worker)_worker.Join(timeoutMs);
        }

        private void WriteLoop()
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                if((File.GetAttributes(DirectoryPath)&FileAttributes.ReparsePoint)!=0)
                    throw new IOException("The automatic logs folder must not redirect outside the app folder.");
                PruneOwnedSessions();
                var header="# DX Manager private automatic log; session="+SessionId+Environment.NewLine+
                    "# Started="+DateTimeOffset.Now.ToString("o",CultureInfo.InvariantCulture)+
                    "; app="+typeof(LogService).Assembly.GetName().Version+"; CLR="+Environment.Version+Environment.NewLine;
                using(var session=new RollingFile(Path.Combine(DirectoryPath,"dxm-session-"+SessionId+".log"),_options.SessionBytes,header,_options.OpenStream))
                using(var details=new RollingFile(Path.Combine(DirectoryPath,"dxm-diagnostic-"+SessionId+".log"),_options.DiagnosticBytes,header,_options.OpenStream))
                {
                    foreach(var record in _queue.GetConsumingEnumerable())
                    {
                        var dropped=Interlocked.Exchange(ref _dropped,0);
                        if(dropped!=0)
                        {
                            var notice=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff",CultureInfo.InvariantCulture)+
                                " [AUTOLOG] Queue full; omitted records="+dropped.ToString(CultureInfo.InvariantCulture);
                            session.Write(notice);details.Write(notice);
                        }
                        if(record.Barrier!=null)
                        {
                            session.Flush();details.Flush();record.Barrier.TrySetResult(true);continue;
                        }
                        var text=AutomaticLogRedactor.Redact(record.Text);
                        if(record.Session)session.Write(text);
                        if(record.Diagnostic)details.Write(text);
                    }
                }
            }
            catch(Exception error)
            {
                _error=error.GetType().Name+": "+error.Message;
                Interlocked.Exchange(ref _stopped,1);_queue.CompleteAdding();
                if(_failure!=null)try{_failure(error);}catch{ }
            }
            finally
            {
                Interlocked.Exchange(ref _stopped,1);
                Record pending;
                while(_queue.TryTake(out pending))if(pending.Barrier!=null)pending.Barrier.TrySetResult(false);
            }
        }

        private void PruneOwnedSessions()
        {
            var names=new Regex(@"^dxm-(?:session|diagnostic)-(?<id>\d{8}-\d{9}-\d+-[a-f0-9]{8})(?:\.previous)?\.log$",RegexOptions.CultureInvariant);
            var owned=new Dictionary<string,List<string>>(StringComparer.Ordinal);
            foreach(var path in Directory.GetFiles(DirectoryPath,"dxm-*.log",SearchOption.TopDirectoryOnly))
            {
                var match=names.Match(Path.GetFileName(path));
                if(!match.Success || (File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)continue;
                var id=match.Groups["id"].Value;List<string> paths;
                if(!owned.TryGetValue(id,out paths)){paths=new List<string>();owned[id]=paths;}
                paths.Add(path);
            }
            var keep=new HashSet<string>(owned.Keys.Where(id=>id!=SessionId).OrderByDescending(id=>id,StringComparer.Ordinal)
                .Take(_options.RetainedSessions-1),StringComparer.Ordinal){SessionId};
            foreach(var item in owned.Where(item=>!keep.Contains(item.Key)))foreach(var path in item.Value)
                try{File.Delete(path);}catch(IOException){ }catch(UnauthorizedAccessException){ }
        }

        private sealed class RollingFile : IDisposable
        {
            private readonly string _path,_header;
            private readonly long _limit;
            private readonly Func<string,Stream> _open;
            private StreamWriter _output;
            private long _bytes;
            internal RollingFile(string path,long limit,string header,Func<string,Stream> open){_path=path;_limit=limit;_header=header;_open=open;Open();}
            private void Open()
            {
                var stream=_open==null?new FileStream(_path,FileMode.CreateNew,FileAccess.Write,
                    FileShare.ReadWrite|FileShare.Delete):_open(_path);
                var encoding=new UTF8Encoding(true);
                _output=new StreamWriter(stream,encoding){AutoFlush=true};
                _output.Write(_header);_bytes=encoding.GetPreamble().Length+encoding.GetByteCount(_header);
            }
            internal void Write(string text)
            {
                var bytes=Encoding.UTF8.GetByteCount(text)+Encoding.UTF8.GetByteCount(Environment.NewLine);
                if(_bytes+bytes>_limit)
                {
                    _output.Dispose();_output=null;
                    var previous=Path.ChangeExtension(_path,"previous.log");
                    if(File.Exists(previous))File.Delete(previous);
                    File.Move(_path,previous);Open();
                }
                _output.WriteLine(text);_bytes+=bytes;
            }
            internal void Flush(){_output.Flush();}
            public void Dispose(){if(_output!=null)_output.Dispose();}
        }
    }

    internal static class AutomaticLogRedactor
    {
        private static readonly Regex Secret=new Regex(@"(?i)(\b(?:token|auth_token|native_token|session_token|secret|password|pairingCode|authorization)[""']?\s*(?:=|:|\s)\s*)(""[^""\r\n]*""|'[^'\r\n]*'|[^\s,|]+)",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(50));
        private static readonly Regex Authorization=new Regex(@"(?i)(\bauthorization[""']?\s*[:=]\s*)[^\r\n]*",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(50));
        private static readonly Regex Hex=new Regex(@"(?i)(?<![0-9a-z])[0-9a-f]{32,}(?![0-9a-z])",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(50));
        private static readonly Regex Pair=new Regex(@"(?i)(\bpair\s+(?:""[^""]+""|\S+)\s+)(?:""\d{6}""|'\d{6}'|\d{6})",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(50));
        internal static string Redact(string value)
        {
            try{return Hex.Replace(Pair.Replace(Secret.Replace(Authorization.Replace(value??string.Empty,"$1[redacted]"),"$1[redacted]"),"$1[redacted]"),"[redacted]");}
            catch(RegexMatchTimeoutException){return "[AUTOLOG] Entry omitted because secret redaction exceeded its time limit.";}
        }
    }
}
