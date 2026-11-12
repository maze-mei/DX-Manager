using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DexManager.Services {
 internal sealed class NativeDexProcessController:IDisposable {
  private readonly string _token;
  private readonly LogService _log;
  private readonly TcpClient _client;
  private readonly StreamWriter _output;
  private readonly ManualResetEventSlim _wake=new ManualResetEventSlim(false);
  private readonly TaskCompletionSource<NativeDexReady> _ready=new TaskCompletionSource<NativeDexReady>();
  private readonly TaskCompletionSource<bool> _stopped=new TaskCompletionSource<bool>();
  private int _stopRequested,_disposed;
  private readonly object _viewerSync=new object();
  private int _viewerVersion,_sentViewerVersion,_viewer=-1,_power;
  internal NativeDexProcessController(int port,string token,LogService log=null) {
   NativeDexProtocol.ValidateToken(token);_token=token;_log=log;_client=new TcpClient();
   try {
    var connect=_client.ConnectAsync("127.0.0.1",port);
    if(!connect.Wait(1500))throw new IOException("Native loopback connection timed out.");
    connect.GetAwaiter().GetResult();_client.NoDelay=true;
    _output=new StreamWriter(_client.GetStream(),new UTF8Encoding(false)){AutoFlush=true};
    _output.WriteLine("ATTACH "+token);
    Task.Run((Action)Read);Task.Run((Action)WriteControl);
   }catch{_client.Close();throw;}
  }
  internal NativeDexReady AwaitReady(int timeoutMs,Func<bool> canceled) {
   var watch=System.Diagnostics.Stopwatch.StartNew();
   while(watch.ElapsedMilliseconds<timeoutMs) {
    if(canceled!=null&&canceled())throw new OperationCanceledException();
    if(_ready.Task.IsCompleted)return _ready.Task.GetAwaiter().GetResult();Thread.Sleep(25);
   }
   throw new TimeoutException("Native DeX activation/reattachment timed out.");
  }
  internal Task<bool> Completion{get{return _stopped.Task;}}
  internal bool StopRequested{get{return Interlocked.CompareExchange(ref _stopRequested,0,0)!=0;}}
  internal void RequestStop(){Interlocked.Exchange(ref _stopRequested,1);_wake.Set();}
  internal void SetViewer(bool active,bool power) {
   lock(_viewerSync){int value=active?1:0,awake=power?1:0;if(_viewer==value&&_power==awake)return;_viewer=value;_power=awake;_viewerVersion++;}
   _wake.Set();
  }
  internal bool WaitStopped(int timeoutMs){RequestStop();try{return _stopped.Task.Wait(timeoutMs)&&_stopped.Task.Result;}catch(AggregateException){return false;}}
  private void WriteControl() {
   try {
    while(Interlocked.CompareExchange(ref _disposed,0,0)==0) {
     if(StopRequested){if(_log!=null)_log.Diagnostic("NativeControl","Sending STOP on existing channel.");_output.WriteLine("STOP "+_token);return;}
     // Wait for READY before sending heartbeat, which starts at an attached session.
     if(_ready.Task.Status==TaskStatus.RanToCompletion) {
      int version,viewer,power;lock(_viewerSync){version=_viewerVersion;viewer=_viewer;power=_power;}
      if(version!=_sentViewerVersion){_output.WriteLine("VIEWER "+_token+" "+viewer+" "+power);_sentViewerVersion=version;}
      _output.WriteLine("PING "+_token);
     }
     _wake.Wait(500);_wake.Reset();
    }
   }catch(Exception e){if(_log!=null)_log.Diagnostic("NativeControl","Write failed: "+e.GetType().Name+": "+e.Message);_ready.TrySetException(new IOException("Native control connection ended.",e));}
  }
  private void Read() {
   try {
    using(var input=new StreamReader(_client.GetStream(),Encoding.UTF8)) {
     string line;
     while((line=ReadBounded(input))!=null) {
      string kind;string[] data;if(!NativeDexProtocol.TryRead(_token,line,out kind,out data))continue;
      if(_log!=null&&(kind=="READY"||kind=="ERROR"||kind=="STOPPED"))_log.Diagnostic("NativeControl","Received "+kind+"; fields="+(kind=="ERROR"?NativeDexProtocol.Error(data):string.Join("|",data)));
      if(kind=="READY")_ready.TrySetResult(NativeDexProtocol.Ready(data));
      else if(kind=="ERROR")_ready.TrySetException(new InvalidOperationException(NativeDexProtocol.Error(data)));
      else if(kind=="STOPPED"&&data.Length==1)_stopped.TrySetResult(data[0]=="1");
      else if(kind=="POLICY_LOG"&&data.Length==1&&_log!=null)try{
       var text=Encoding.UTF8.GetString(Convert.FromBase64String(data[0]));
       if(text.Contains(" Metrics ")){
        _log.Diagnostic("NativeMetrics",text);
        if(text.Contains(" Metrics error "))_log.Error(text,null);
        else if(text.Contains(" Metrics warning "))_log.Warning(text);
        else if(text.Contains(" Metrics command "))_log.Command(text);
        else _log.Response(text);
       }else _log.Diagnostic(text.Contains(" BluetoothStartup ")?"NativeAudio":"CompanionPolicy",text);
      }catch(FormatException){}
     }
    }
   }catch(Exception e){if(_log!=null)_log.Diagnostic("NativeControl","Read failed: "+e.GetType().Name+": "+e.Message);_ready.TrySetException(new IOException("Native control disconnected.",e));}
   finally{if(_log!=null)_log.Diagnostic("NativeControl","Read channel ended; stopRequested="+StopRequested);_ready.TrySetException(new IOException("Native control disconnected before READY."));_stopped.TrySetResult(false);}
  }
  private static string ReadBounded(TextReader input){var text=new StringBuilder();int ch;while((ch=input.Read())!=-1){if(ch=='\n')return text.ToString();if(ch=='\r')continue;if(text.Length>=8192)throw new IOException("Native frame limit.");text.Append((char)ch);}return text.Length==0?null:text.ToString();}
  public void Dispose(){if(Interlocked.Exchange(ref _disposed,1)!=0)return;_wake.Set();_client.Close();}
 }
}
