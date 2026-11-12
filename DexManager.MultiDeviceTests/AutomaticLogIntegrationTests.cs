using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DexManager.Services;

// Product LogService plus real files/child processes. No phone or installed settings are used.
internal static class AutomaticLogIntegrationTests
{
    static int checks;
    static readonly string Root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"state");
    static readonly string Self=Assembly.GetExecutingAssembly().Location;
    static readonly Type Options=typeof(LogService).Assembly.GetType("DexManager.Services.AutomaticLogOptions",true);
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    static string Folder(string name){var path=Path.Combine(Root,name);Directory.CreateDirectory(path);return path;}
    static void Configure(LogService log,string folder,params object[] values)
    {
        var options=Activator.CreateInstance(Options,true);
        for(int i=0;i<values.Length;i+=2)Options.GetField((string)values[i],BindingFlags.Instance|BindingFlags.NonPublic).SetValue(options,values[i+1]);
        typeof(LogService).GetMethod("EnableAutomaticLogs",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(log,new[]{(object)folder,options});
    }
    static string Text(string folder,string stream,string id)
    {
        var name=Path.Combine(folder,"logs","dxm-"+stream+"-"+id+".log");
        using(var input=new FileStream(name,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
        using(var reader=new StreamReader(input,Encoding.UTF8))return reader.ReadToEnd();
    }
    static int Main(string[] args)
    {
        if(args.Length==2 && args[0]=="--child")return Child(args[1]);
        try{TestLive();TestConcurrent();TestLimits();TestUnavailable();TestBlockedWriter();TestChildCrash();Console.WriteLine("All automatic log integration checks passed: "+checks);return 0;}
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
    static int Child(string folder)
    {
        using(var log=new LogService())
        {
            log.EnableAutomaticLogs(folder);log.Info("Child live entry: 한글");log.Diagnostic("Shutdown","Child detail before any graceful shutdown.");
            if(!log.FlushAutomaticLogs(3000))return 2;
            Console.WriteLine(log.AutomaticSessionId);Console.Out.Flush();Console.ReadLine();
            log.Diagnostic("Shutdown","Child graceful final marker.");return 0;
        }
    }
    static void TestLive()
    {
        var folder=Folder("live");var custom=Folder("manual-directory");
        using(var log=new LogService())
        {
            log.Info("Before automatic saving was enabled.");log.SetLogDirectory(custom);log.EnableAutomaticLogs(folder);
            var id=log.AutomaticSessionId;
            log.Info("일반 세션 로그 Ω 日本語");log.Diagnostic("Wake","detail-only marker; command=input keyevent 224");
            log.Warning("warning-to-both");log.Error("error-to-both",new IOException("test exception"));
            log.Info("adb --es native_token 0123456789abcdef0123456789abcdef --es token \"Secret with spaces\"");
            log.Info("adb pair 192.0.2.1:12345 \"654321\"");log.Diagnostic("Security","{\"auth_token\":\"very-secret-value\"}");
            log.Diagnostic("Security","Authorization: Bearer bearer-secret-value");
            Check(log.FlushAutomaticLogs(3000),"Automatic files flush while the application remains open; persistenceError="+log.AutomaticLogError);
            var session=Text(folder,"session",id);var detail=Text(folder,"diagnostic",id);
            Check(Directory.Exists(Path.Combine(folder,"logs")),"The portable app's own logs folder is created automatically");
            using(var input=new FileStream(Path.Combine(folder,"logs","dxm-session-"+id+".log"),FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
                Check(input.ReadByte()==0xef&&input.ReadByte()==0xbb&&input.ReadByte()==0xbf,"UTF-8 BOM keeps Korean logs readable in older Windows Notepad");
            Check(log.AutomaticLogDirectory==Path.Combine(folder,"logs"),"Automatic files stay under the app folder despite a custom manual-save directory");
            Check(session.Contains("Before automatic saving")&&session.Contains("일반 세션 로그 Ω 日本語"),"Startup backlog and Unicode session entries are persisted");
            Check(detail.Contains("detail-only marker")&&!session.Contains("detail-only marker"),"Detailed diagnostics use their separate file");
            Check(!log.GetSessionEntries().Any(x=>x.Contains("detail-only marker")),"Detailed diagnostics do not flood the existing session-log UI");
            Check(session.Contains("warning-to-both")&&detail.Contains("warning-to-both")&&detail.Contains("test exception"),"Warnings/errors and exception evidence are available in diagnostics");
            Check(!session.Contains("0123456789abcdef0123456789abcdef")&&!session.Contains("Secret with spaces")&&!session.Contains("654321"),"Native/auth tokens and six-digit pairing codes are redacted on disk");
            Check(!detail.Contains("very-secret-value")&&!detail.Contains("bearer-secret-value"),"JSON authentication fields and bearer values are redacted");
            Check(log.GetSessionEntries().Any(x=>x.Contains("Secret with spaces")),"Automatic redaction leaves existing in-memory/manual logging behavior unchanged");
            Check(session.Contains("session="+id)&&detail.Contains("session="+id),"Session and diagnostic files share a correlation identifier");
            log.Diagnostic("Shutdown","normal-exit-final-marker");log.CompleteAutomaticLogs(2000);
            Check(Text(folder,"diagnostic",id).Contains("normal-exit-final-marker"),"Normal exit drains the final shutdown entries before closing files");
            log.Info("late callback after completion");log.CompleteAutomaticLogs(10);
            Check(true,"Late callbacks and repeated completion do not throw");
        }
    }
    static void TestConcurrent()
    {
        var folder=Folder("concurrent");
        using(var log=new LogService())
        {
            Configure(log,folder,"QueueCapacity",4096);
            var work=Enumerable.Range(0,8).Select(thread=>Task.Run(()=>{for(int i=0;i<100;i++)log.Info("concurrent-record="+thread+":"+i);})).ToArray();
            Task.WaitAll(work);Check(log.FlushAutomaticLogs(5000),"Concurrent device writers can be flushed together");
            var text=Text(folder,"session",log.AutomaticSessionId);
            Check(text.Split(new[]{"concurrent-record="},StringSplitOptions.None).Length-1==800,"Concurrent log producers preserve complete record boundaries");
            Check(!text.Contains("omitted records"),"Normal concurrent traffic does not overflow the bounded queue");
        }
    }
    static void TestLimits()
    {
        var folder=Folder("limits");var logFolder=Path.Combine(folder,"logs");Directory.CreateDirectory(logFolder);
        File.WriteAllText(Path.Combine(logFolder,"manual-user.log"),"preserve-manual");
        File.WriteAllText(Path.Combine(logFolder,"dxm-session-not-owned.log"),"preserve-unrecognized");
        var ids=new List<string>();
        for(int run=0;run<3;run++)
        {
            using(var log=new LogService())
            {
                Configure(log,folder,"RetainedSessions",2,"SessionBytes",1024,"DiagnosticBytes",1024,"QueueCapacity",1024);
                ids.Add(log.AutomaticSessionId);
                for(int n=0;n<40;n++){log.Info("run="+run+" item="+n+" "+new string('한',90));log.Diagnostic("Bounds","run="+run+" item="+n+" "+new string('글',90));}
                Check(log.FlushAutomaticLogs(3000),"Bounded stream completes run "+run);
            }
            Thread.Sleep(5);
        }
        var files=Directory.GetFiles(logFolder,"dxm-*.log");
        Check(!files.Any(x=>x.Contains(ids[0]))&&files.Any(x=>x.Contains(ids[1]))&&files.Any(x=>x.Contains(ids[2])),"Retention keeps the most recent configured sessions and removes only older owned sessions");
        Check(files.Where(x=>!x.Contains("not-owned")).All(x=>new FileInfo(x).Length<=1024),"Both streams and their rotation files obey byte limits, including Unicode");
        Check(files.Count(x=>!x.Contains("not-owned"))<=8,"Each stream has at most one previous part per retained session");
        Check(Text(folder,"session",ids[2]).Contains("item=39")&&Text(folder,"diagnostic",ids[2]).Contains("item=39"),"Rotation retains the latest session and detail records");
        Check(File.ReadAllText(Path.Combine(logFolder,"manual-user.log"))=="preserve-manual"&&File.ReadAllText(Path.Combine(logFolder,"dxm-session-not-owned.log"))=="preserve-unrecognized","Retention preserves manual and unrecognized user log files");
    }
    static void TestUnavailable()
    {
        var folder=Folder("unavailable");File.WriteAllText(Path.Combine(folder,"logs"),"occupied-by-user-file");
        using(var log=new LogService())
        {
            log.EnableAutomaticLogs(folder);log.Info("still usable");
            SpinWait.SpinUntil(()=>!string.IsNullOrEmpty(log.AutomaticLogError),3000);
            SpinWait.SpinUntil(()=>log.GetSessionEntries().Any(x=>x.Contains("Automatic log saving is unavailable")),3000);
            Check(!string.IsNullOrEmpty(log.AutomaticLogError),"Unavailable storage is reported without crashing the app");
            log.Info("after file failure");log.Warning("after failure warning");
            Check(log.GetSessionEntries().Any(x=>x.Contains("after file failure")),"The in-memory log continues after persistence fails");
            Check(log.GetSessionEntries().Count(x=>x.Contains("Automatic log saving is unavailable"))==1,"A storage failure is reported once, without a retry or logging loop");
            Check(File.ReadAllText(Path.Combine(folder,"logs"))=="occupied-by-user-file","Automatic logging never replaces an unrelated path occupant");
        }
    }
    sealed class BlockedStream : MemoryStream
    {
        readonly ManualResetEventSlim entered,release;
        internal BlockedStream(ManualResetEventSlim entered,ManualResetEventSlim release){this.entered=entered;this.release=release;}
        public override void Write(byte[] buffer,int offset,int count){entered.Set();release.Wait();base.Write(buffer,offset,count);}
    }
    static void TestBlockedWriter()
    {
        var folder=Folder("blocked-writer");var entered=new ManualResetEventSlim();var release=new ManualResetEventSlim();
        var streams=new List<BlockedStream>();
        using(var log=new LogService())
        {
            Func<string,Stream> open=path=>{var stream=new BlockedStream(entered,release);lock(streams)streams.Add(stream);return stream;};
            Configure(log,folder,"QueueCapacity",8,"OpenStream",open);
            try
            {
                Check(entered.Wait(3000),"A blocked disk writer is isolated on its own worker");
                var watch=Stopwatch.StartNew();for(int n=0;n<500;n++)log.Info("overflow-record="+n);
                Check(watch.ElapsedMilliseconds<1500,"A blocked disk never makes log producers wait for file I/O");
                watch.Restart();log.CompleteAutomaticLogs(20);
                Check(watch.ElapsedMilliseconds<500,"Application shutdown has a bounded logging wait even if storage stalls");
            }
            finally{release.Set();log.CompleteAutomaticLogs(3000);}
            var text=string.Join("\n",streams.Select(x=>Encoding.UTF8.GetString(x.ToArray())));
            Check(text.Contains("omitted records="),"Queue overflow is visible instead of growing memory without a limit");
        }
    }
    static void TestChildCrash()
    {
        var folder=Folder("child-crash");
        using(var child=Process.Start(new ProcessStartInfo{FileName=Self,Arguments="--child \""+folder+"\"",UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardInput=true,RedirectStandardError=true}))
        {
            try
            {
            var firstLine=child.StandardOutput.ReadLineAsync();
            Check(firstLine.Wait(5000)&&!string.IsNullOrWhiteSpace(firstLine.Result),"An actual child process persists its live logs before exit");
            var id=firstLine.Result;
            Check(Text(folder,"session",id).Contains("Child live entry: 한글")&&Text(folder,"diagnostic",id).Contains("Child detail before"),"Log files are readable while the producer process is running");
            child.Kill();Check(child.WaitForExit(3000),"The diagnostic fixture simulates forced process termination");
            Check(Text(folder,"diagnostic",id).Contains("Child detail before"),"Already-written diagnostics survive a forced exit without a graceful save");
            }
            finally{if(!child.HasExited){child.Kill();child.WaitForExit(3000);}}
        }
        var graceful=Folder("child-graceful");
        using(var child=Process.Start(new ProcessStartInfo{FileName=Self,Arguments="--child \""+graceful+"\"",UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardInput=true,RedirectStandardError=true}))
        {
            try
            {
            var firstLine=child.StandardOutput.ReadLineAsync();Check(firstLine.Wait(5000),"The graceful child starts logging");var id=firstLine.Result;
            child.StandardInput.WriteLine("exit");child.StandardInput.Flush();Check(child.WaitForExit(3000)&&child.ExitCode==0,"A normal child exit finishes its file writer");
            Check(Text(graceful,"diagnostic",id).Contains("Child graceful final marker"),"Graceful process exit preserves the last shutdown marker");
            }
            finally{if(!child.HasExited){child.Kill();child.WaitForExit(3000);}}
        }
    }
}
