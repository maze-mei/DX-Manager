using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DexManager.Forms;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;

// Real product services/APK certificate verifier plus synthetic ADB metadata and native TCP peer.
// No installed phone, system ADB server, or production configuration is touched.
internal static class NativeDexIntegrationTests {
 static int passed;static string root;static readonly string Self=Assembly.GetExecutingAssembly().Location;
 static readonly string FakeAdbPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"NativeFakeAdb.exe");
 static readonly Type Protocol=typeof(NativeDexSessionService).Assembly.GetType("DexManager.Services.NativeDexProtocol",true);
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
 static Exception Fails(Action action,string name){try{action();}catch(Exception e){Check(true,name);return e;}throw new Exception("Expected rejection: "+name);}
 static object Call(string name,params object[] args){try{return Protocol.GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);}catch(TargetInvocationException e){throw e.InnerException;}}
 static string Hash{get{return (string)typeof(NativeDexSessionService).Assembly.GetType("DexManager.Services.NativeDexHelperRelease",true).GetField("Sha256",BindingFlags.NonPublic|BindingFlags.Static).GetRawConstantValue();}}
 [STAThread]static int Main(string[] args){if(args.Length>0)return FakeAdb(args);try{
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"native-test-state");Directory.CreateDirectory(root);
  LocalizationService.Apply(AppLanguage.English);NativeStartReadinessTests.Run(Check);TestWakeGate();TestInitialPreparation();TestProtocol();TestSettings();TestBrowserNegotiation();TestResumedStartAppCommands();TestBluetoothStartup();TestRetention();TestRecovery();TestFailures();TestOptionalCompanion();TestControlOnlyReconnect();TestViewerPolicy();TestIsolation();TestShutdown();TestOrchestrator();TestResumeLaunchFailure();TestMainReconnectPolicy();
  Console.WriteLine("All native Windows integration tests passed: "+passed);return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 static VirtualDisplaySettings Metrics(){return new VirtualDisplaySettings{Width=1600,Height=900,Dpi=150};}
 static void TestWakeGate(){
  using(var f=new Fixture("wake-gate","sleeping")){
   var start=Task.Run(()=>f.Native.Start("SERIAL-A",Metrics(),()=>false));
   Check(SpinWait.SpinUntil(()=>f.Log.GetSessionEvents().Any(e=>e.Kind==LogEntryKind.Progress&&e.Message.Contains("Wake the phone")),5000),"Sleeping phone emits one highlighted wake request");
   Check(f.Launches==0&&!start.IsCompleted,"No native owner is launched while the phone sleeps");
   File.WriteAllText(Path.Combine(f.Directory,"awake.flag"),"1");
   Check(start.Wait(12000)&&start.Result.NativeDpi==150,"Fresh DeX starts after the phone wakes and stabilizes");
   Check(f.Log.GetSessionEvents().Count(e=>e.Message.Contains("Wake the phone"))==1,"Power polls do not repeat the user wake message");
   var reads=f.Audit.Split('\n').Count(x=>x.Contains("dumpsys power"));
   f.Native.Start("SERIAL-A",Metrics(),()=>false);
   Check(f.Audit.Split('\n').Count(x=>x.Contains("dumpsys power"))==reads,"Retained-display reuse skips the fresh-start wake gate");
   Check(f.Native.Stop(start.Result,"SERIAL-A"),"Wake-gate fixture cleans only its owned display");
  }
  using(var f=new Fixture("wake-cancel","sleeping")){
   var start=Task.Run(()=>{try{f.Native.Start("SERIAL-A",Metrics(),()=>false);return false;}catch(OperationCanceledException){return true;}});
   Check(SpinWait.SpinUntil(()=>f.Log.GetSessionEntries().Any(x=>x.Contains("Wake the phone")),5000),"Cancellation scenario reached the wake wait");
   f.Native.CancelPendingStart("SERIAL-B");Thread.Sleep(100);Check(!start.IsCompleted,"Another phone's stop cannot cancel this wait");
   f.Native.CancelPendingStart("SERIAL-A");
   Check(start.Wait(3000)&&start.Result&&f.Launches==0,"Explicit stop cancels a sleeping start without creating a display");
  }
 }
 static void TestInitialPreparation(){
  using(var f=new Fixture("initial-dpi150","initial150")){
   var requested=Metrics();var lease=f.Native.Start("SERIAL-A",requested,()=>false);
   Check(lease.NativeInitialMetricsPrepared&&lease.NativeDpi==150&&requested.Dpi==150,"Requested150 is preserved after managed160-DPI initialization");
   Check(f.Audit.Contains("PREPARE_NATIVE_DENSITY")&&f.Audit.Contains("--ei density 160"),"Low-DPI launch prepares Samsung160 instead of falling back to213");
   Check(f.Audit.Split('\n').Any(x=>x.Contains("NativeDexServer launch ")&&x.Contains(" 1600 900 150 ")&&x.TrimEnd().EndsWith(" 1")),"The helper receives final150 and the initial-mode flag separately");
   var preparations=f.Audit.Split('\n').Count(x=>x.Contains("PREPARE_NATIVE_DENSITY"));f.Native.Start("SERIAL-A",requested,()=>false);
   Check(f.Audit.Split('\n').Count(x=>x.Contains("PREPARE_NATIVE_DENSITY"))==preparations,"Reusing a150-DPI desktop never reapplies initial160");
   Check(f.Log.GetSessionEvents().Any(e=>e.Kind==LogEntryKind.Progress&&e.Message.Contains("DPI 160")&&e.Message.Contains("DPI 150")),"The user sees the initial160 to final150 plan as a highlighted notice");
   Check(f.Native.Stop(lease,"SERIAL-A"),"Prepared low-DPI session cleans up normally");
  }
  using(var f=new Fixture("initial-mode","initial")){
   var settings=Metrics();settings.Dpi=160;
   var lease=f.Native.Start("SERIAL-A",settings,()=>false);
   Check(lease.NativeInitialMetricsPrepared&&lease.NativeDpi==160,"Trusted shared APK16 negotiates initial native metrics");
   var preparations=f.Audit.Split('\n').Count(x=>x.Contains("PREPARE_NATIVE_DENSITY"));
   Check(preparations==1,"Desktop DPI preparation occurs once before fresh creation");
   f.Native.Start("SERIAL-A",settings,()=>false);
   Check(f.Audit.Split('\n').Count(x=>x.Contains("PREPARE_NATIVE_DENSITY"))==preparations,"Retained native reuse does not reapply global desktop DPI");
   Check(f.Native.Stop(lease,"SERIAL-A"),"Prepared native session cleanup remains acknowledged");
  }
  using(var f=new Fixture("initial-low-preparation-failed","initial-fail")){
   Fails(()=>f.Native.Start("SERIAL-A",Metrics(),()=>false),"A rejected160 preparation cannot silently launch150 from the default213");
   Check(f.Launches==0&&f.Audit.Contains("CLEAR_NATIVE_SESSION"),"Low-DPI preparation failure clears only its binding before launch");
  }
  using(var f=new Fixture("initial-preparation-failed","initial-fail")){
   var settings=Metrics();settings.Dpi=160;
   Fails(()=>f.Native.Start("SERIAL-A",settings,()=>false),"Rejected initial DPI preparation refuses display launch");
   Check(f.Launches==0&&f.Audit.Contains("CLEAR_NATIVE_SESSION"),"Preparation failure clears its Companion binding without creating a display");
  }
 }
 static void TestProtocol(){
  string token=(string)Call("NewToken");Check(token.Length==32,"Canonical session identity");
  Check((string)Call("PeerAddress","0123456789abcdef0123456789abcdef")=="02:01:23:45:67:89","Android/Windows peer identity matches");
  Fails(()=>Call("PeerAddress",";echo invalid"),"Shell fragments cannot become identities");
  object[] data={token,"DXND1|"+token+"|READY|73|1600|900|150|1111|2222",null,null};
  Check((bool)Call("TryRead",data),"Matching versioned READY accepted");Check((string)data[2]=="READY","Frame kind retained");
  var ready=Call("Ready",data[3]);Check((int)ready.GetType().GetField("DisplayId",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ready)==73,"Exact display is selected");
  data=new object[]{token,"DXND1|ffffffffffffffffffffffffffffffff|READY|999|1600|900|150|1|2",null,null};Check(!(bool)Call("TryRead",data),"Stale-session READY rejected");
  data=new object[]{token,"log largest display=999",null,null};Check(!(bool)Call("TryRead",data),"Logs cannot choose a display");
  Fails(()=>Call("Ready",(object)new[]{"0","1600","900","150","1","2"}),"Primary display rejected");
  Fails(()=>Call("Ready",(object)new[]{"73","1600","900","150","1","1"}),"Independent owner PID required");
  Fails(()=>Call("Ready",(object)new[]{"73","20000","900","150","1","2"}),"Invalid metrics rejected");
  Check((bool)Call("Recovered",token,"DXND1|"+token+"|RECOVERED|1"),"Recovery requires matching acknowledgement");
  Check(!(bool)Call("Recovered",token,"DXND1|ffffffffffffffffffffffffffffffff|RECOVERED|1"),"Foreign recovery acknowledgement rejected");
 }
 sealed class Fixture:IDisposable {
  internal readonly string Directory;internal readonly TcpListener Listener;internal readonly int Port;
  internal readonly LogService Log;internal readonly ProcessRunner Runner;internal readonly AdbService Adb;
  internal readonly DeviceRuntimeSessionRegistry Registry=new DeviceRuntimeSessionRegistry();internal readonly NativeDexSessionService Native;
  internal volatile bool Closing;internal volatile string Mode;internal int Attaches,Stops;
  readonly List<TcpClient> clients=new List<TcpClient>();
  readonly List<string> commands=new List<string>();
  internal Fixture(string name,string mode="normal"){
   Directory=Path.Combine(root,name);System.IO.Directory.CreateDirectory(Path.Combine(Directory,"tools","native-dex"));
   File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tools","native-dex","dxm-native-dex.jar"),Path.Combine(Directory,"tools","native-dex","dxm-native-dex.jar"),true);
   File.WriteAllText(Path.Combine(Directory,"audit.txt"),"");Mode=mode;
   Listener=new TcpListener(IPAddress.Loopback,0);Listener.Start();Port=((IPEndPoint)Listener.LocalEndpoint).Port;
   Scope();Log=new LogService();Log.SetLogDirectory(Path.Combine(Directory,"logs"));Runner=new ProcessRunner(Log);Adb=new AdbService(FakeAdbPath,8000,Runner,Log);
   Native=new NativeDexSessionService(Adb,Log,Registry,Directory);
   var accept=new Thread(()=>{while(!Closing)try{var c=Listener.AcceptTcpClient();lock(clients)clients.Add(c);var worker=new Thread(()=>Peer(c)){IsBackground=true};worker.Start();}catch(SocketException){if(!Closing)throw;}}){IsBackground=true};accept.Start();
  }
  internal void Scope(){Environment.SetEnvironmentVariable("DXM_NATIVE_DIR",Directory);Environment.SetEnvironmentVariable("DXM_NATIVE_PORT",Port.ToString());Environment.SetEnvironmentVariable("DXM_NATIVE_MODE",Mode);Environment.SetEnvironmentVariable("DXM_NATIVE_ALIAS",Path.GetFileName(Directory).StartsWith("alias")?"1":"0");}
  internal string Audit{get{using(var stream=new FileStream(Path.Combine(Directory,"audit.txt"),FileMode.Open,FileAccess.Read,FileShare.ReadWrite))using(var reader=new StreamReader(stream))return reader.ReadToEnd();}}
  internal string ControlAudit{get{lock(commands)return string.Join("\n",commands);}}
  internal int Launches{get{return File.ReadAllLines(Path.Combine(Directory,"audit.txt")).Count(x=>x.Contains("NativeDexServer launch "));}}
  internal int Journals{get{var p=Path.Combine(Directory,"config","native-dex");return System.IO.Directory.Exists(p)?System.IO.Directory.GetFiles(p,"*.json").Length:0;}}
  internal void SetMode(string mode){Mode=mode;Scope();}
  internal void Expire(string serial){var p=StatePath(Directory,serial);if(File.Exists(p))File.Delete(p);}
  void Peer(TcpClient client){
   string token=null;bool attached=false;try{using(client){client.NoDelay=true;var input=new StreamReader(client.GetStream(),Encoding.UTF8);var output=new StreamWriter(client.GetStream(),new UTF8Encoding(false)){AutoFlush=true};string line;
    while((line=input.ReadLine())!=null){lock(commands)commands.Add(line);var a=line.Split(' ');if(a.Length<2)break;token=a[1];
     var state=System.IO.Directory.GetFiles(Directory,"phone-*.txt").FirstOrDefault(p=>File.ReadAllText(p)==token);
     if(state==null)break;
     if(a[0]=="ATTACH"){
      attached=true;
      Interlocked.Increment(ref Attaches);
      if(Mode=="error"){output.WriteLine("DXND1|"+token+"|ERROR|"+Convert.ToBase64String(Encoding.UTF8.GetBytes("Receiver left untouched.")));break;}
      if(Mode=="wrong-ready")output.WriteLine("DXND1|ffffffffffffffffffffffffffffffff|READY|999|1600|900|150|1|2");
      output.WriteLine("DXND1|"+token+"|READY|73|1600|900|"+(Mode=="initial"?"160":"150")+"|1111|2222");
      if(Mode=="early-end")break;
     }else if(a[0]=="CONFIGURE"&&(a.Length==6||a.Length==7||a.Length==8)){
      output.WriteLine("DXND1|"+token+"|CONFIGURED|"+a[2]);break;
     }else if(a[0]=="VIEWER"&&a.Length==4){
      output.WriteLine("DXND1|"+token+"|VIEWER|17");
     }else if(a[0]=="POLICY"&&a.Length==3){
      output.WriteLine("DXND1|"+token+"|POLICY|"+a[2]);break;
     }else if(a[0]=="STOP"){
      Interlocked.Increment(ref Stops);if(Mode=="no-ack"||Mode=="recover-fail")break;
      File.Delete(state);output.WriteLine("DXND1|"+token+"|STOPPED|1");break;
     }
    }
   }}catch(IOException){}catch(ObjectDisposedException){}finally{if(attached)lock(commands)commands.Add("DETACH "+token);lock(clients)clients.Remove(client);}
  }
  public void Dispose(){Closing=true;Listener.Stop();lock(clients)foreach(var c in clients)c.Close();}
 }
 static void TestRetention(){using(var f=new Fixture("retention","wrong-ready")){
  var lease=f.Native.Start("SERIAL-A",Metrics(),()=>false,true,true);
  Check(lease.IsNativeDex&&!lease.OwnsOverlaySetting,"Native lease never claims global overlay ownership");
  Check(lease.DisplayId==73&&lease.NativeWidth==1600&&lease.NativeDpi==150,"Actual geometry retained; stale READY ignored");
  Check(lease.NativeProtocolVersion==7&&lease.NativeHelperPid!=lease.NativeGuardPid,"Detached owner protocol and independent guard retained");
  Check(lease.NativeStayAwakeRequested&&lease.NativeOriginalStayAwake=="0","Original power state captured for Companion expiry");
  Check(f.Journals==1,"Live native ownership is durable");
  int launches=f.Launches,stops=f.Stops;f.Native.Suspend();Thread.Sleep(80);
  Check(f.Stops==stops&&f.Journals==1,"Transport suspension does not send STOP or remove its journal");
  var resumed=f.Native.Start("SERIAL-A",new VirtualDisplaySettings{Width=1920,Height=1080,Dpi=200},()=>false,true,true,true);
  Check(resumed.NativeResumed&&resumed.NativeSessionToken==lease.NativeSessionToken&&resumed.DisplayId==lease.DisplayId,"Reattachment reuses the same token and display");
  Check(f.Launches==launches&&f.Attaches==2,"Reattachment creates no second Android owner");
  Check(resumed.NativeWidth==1600&&resumed.NativeDpi==150,"Reattachment preserves the existing display metrics");
  Check(f.Native.Stop(resumed)&&f.Journals==0,"Explicit stop confirms removal and clears ownership");
  int audit=f.Audit.Length;Check(f.Native.Stop(resumed)&&f.Audit.Length==audit,"Repeated confirmed stop issues no extra device commands");
  Check(!f.Audit.Contains("settings delete global overlay")&&!f.Audit.Contains("pkill"),"Native lifecycle never uses global overlay deletion or pkill");
 }}
 static void TestRecovery(){
 using(var f=new Fixture("missing-baseline")){
  f.Registry.SetPhonePowerState("SERIAL-A",false,true,"<missing>",false);
  var lease=f.Native.Start("SERIAL-A",Metrics(),()=>false,true,true);
  Check(lease.NativeOriginalStayAwake=="<missing>"&&f.Audit.Contains("--es original missing"),"Unset original power state remains distinct from zero across the Companion binding");
  Check(f.Native.Stop(lease),"Unset power baseline does not block explicit native cleanup");
 }
 using(var f=new Fixture("alias")){
  var phone=new PhysicalDeviceInfo{Identity="physical-a"};phone.Transports.Add(new DeviceTransportInfo{Serial="SERIAL-A",Status=AdbDeviceStatus.Device,Kind=DeviceTransportKind.Usb});phone.Transports.Add(new DeviceTransportInfo{Serial="SERIAL-B",Status=AdbDeviceStatus.Device,Kind=DeviceTransportKind.Wireless});
  var devices=new DeviceRegistrySnapshot();devices.Devices.Add(phone);f.Registry.Reconcile(devices);
  var lease=f.Native.Start("SERIAL-A",Metrics(),()=>false);f.Native.Suspend();
  var service=new NativeDexSessionService(f.Adb,f.Log,f.Registry,f.Directory);
  var resumed=service.Start("SERIAL-B",Metrics(),()=>false,false,true,true);
  Check(resumed!=null&&resumed.NativeSessionToken==lease.NativeSessionToken&&resumed.Serial=="SERIAL-B","A verified alias of the same phone can reattach after service restart");
  Check(f.Launches==1,"USB/wireless handover does not recreate the display");
  Check(service.Stop(resumed),"Alias connection retains working explicit cleanup");
 }
 using(var f=new Fixture("expired")){
  var lease=f.Native.Start("SERIAL-A",Metrics(),()=>false);f.Native.Suspend();f.Expire("SERIAL-A");
  var resumed=f.Native.Start("SERIAL-A",Metrics(),()=>false,false,true,true);
  Check(resumed==null&&f.Journals==0,"An expired Companion session is not resurrected by reconnect-only mode");
  Check(f.Launches==1,"Automatic reattachment does not create a new display after expiry");
 }
 using(var f=new Fixture("alias-live","immediate")){
  var phone=new PhysicalDeviceInfo{Identity="physical-a"};phone.Transports.Add(new DeviceTransportInfo{Serial="SERIAL-A",Status=AdbDeviceStatus.Device,Kind=DeviceTransportKind.Usb});phone.Transports.Add(new DeviceTransportInfo{Serial="SERIAL-B",Status=AdbDeviceStatus.Device,Kind=DeviceTransportKind.Wireless});
  var devices=new DeviceRegistrySnapshot();devices.Devices.Add(phone);f.Registry.Reconcile(devices);
  var lease=f.Native.Start("SERIAL-A",Metrics(),()=>false);
  var replacement=f.Native.Start("SERIAL-B",Metrics(),()=>false,false,true,true);
  SpinWait.SpinUntil(()=>f.ControlAudit.Contains("DETACH "+lease.NativeSessionToken),1000);
  Check(f.ControlAudit.LastIndexOf("ATTACH ",StringComparison.Ordinal)<f.ControlAudit.IndexOf("DETACH ",StringComparison.Ordinal),"Transport handover attaches the replacement before closing the old epoch");
  Check(f.Launches==1&&replacement.NativeSessionToken==lease.NativeSessionToken,"Immediate cleanup policy handover preserves one native display");
  Check(f.Native.Stop(replacement),"Replacement control remains explicitly stoppable after transport handover");
 }
 using(var f=new Fixture("protocol2-compatibility")){
  var lease=f.Native.Start("SERIAL-A",Metrics(),()=>false);lease.NativeProtocolVersion=2;
  typeof(NativeDexSessionService).GetMethod("WriteJournal",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f.Native,new object[]{lease});f.Native.Suspend();
  var service=new NativeDexSessionService(f.Adb,f.Log,f.Registry,f.Directory);
  var error=Fails(()=>service.Start("SERIAL-A",Metrics(),()=>false,false,true,true),"An older owner cannot bypass fixed native grace by reattaching");
  Check(error.Message.Contains("30-second")&&f.Launches==1,"Old-owner notice explains cleanup before restart without silently replacing the display");
  Check(f.Native.Stop(lease),"An older owner can still be explicitly stopped through its owned recovery path");
 }}
 static void TestFailures(){
  using(var f=new Fixture("partial","error")){
   var error=Fails(()=>f.Native.Start("SERIAL-A",Metrics(),()=>false),"Activation failure propagates");
   var lease=error.Data["DexManager.VirtualDisplayLease"] as VirtualDisplayLease;
   Check(lease!=null&&lease.NativeStartRequested&&error.Message=="Receiver left untouched.","Partial startup retains ownership and the original error");
   f.SetMode("normal");Check(f.Native.Stop(lease),"Partial startup can be explicitly recovered");
  }
  using(var f=new Fixture("unconfirmed","no-ack")){
   var lease=f.Native.Start("SERIAL-A",Metrics(),()=>false);f.SetMode("recover-fail");
   Check(!f.Native.Stop(lease)&&f.Journals==1,"Unconfirmed cleanup is not reported as success and preserves ownership");
   f.SetMode("normal");Check(f.Native.Stop(lease)&&f.Journals==0,"A later explicit recovery can complete");
  }
  using(var f=new Fixture("preflight","overlay")){
   Fails(()=>f.Native.Start("SERIAL-A",Metrics(),()=>false),"Existing overlay blocks native activation");
   Check(f.Launches==0&&!f.Audit.Contains(" push ")&&f.Journals==0,"Overlay preflight leaves phone resources untouched");
  }
  using(var f=new Fixture("tamper")){
   File.WriteAllText(Path.Combine(f.Directory,"tools","native-dex","dxm-native-dex.jar"),"tampered");
   Fails(()=>f.Native.Start("SERIAL-A",Metrics(),()=>false),"Tampered helper is rejected");Check(f.Launches==0&&!f.Audit.Contains(" push "),"Unverified helper is never uploaded");
  }
  using(var f=new Fixture("cancel")){
   Fails(()=>f.Native.Start("SERIAL-A",Metrics(),()=>true),"Canceled startup is rejected");Check(f.Launches==0&&!f.Audit.Contains(" push "),"Cancellation does not launch an Android owner");
  }
 }
 static void TestOptionalCompanion(){
  foreach(var mode in new[]{"missing-apk","old-apk","no-permission","untrusted-apk","bad-policy"})using(var f=new Fixture("optional-"+mode,mode)){
   var lease=f.Native.Start("SERIAL-A",Metrics(),()=>false,true,true);
   Check(lease.DisplayId==73&&!lease.NativeCompanionConfigured,"Basic native DeX starts without usable Companion: "+mode);
   Check(!lease.NativeBrowserCleanupEnabled,"Basic fallback never keeps a browser-cleanup capability: "+mode);
   Check(f.ControlAudit.Contains("CONFIGURE "+lease.NativeSessionToken+" 30 basic 1 0"),"Basic mode authenticates fixed native grace and captured power baseline: "+mode);
   Check(!f.Audit.Contains("pm grant")&&!f.Audit.Contains(" install "),"Basic mode does not install or grant permissions: "+mode);
   if(mode!="bad-policy")Check(!f.Audit.Contains("CONFIGURE_NATIVE_SESSION"),"No native token/baseline is sent to an ineligible APK: "+mode);
   Check(f.Native.Stop(lease)&&f.Journals==0,"Basic mode retains explicit owned cleanup: "+mode);
  }
  using(var f=new Fixture("optional-keep-power","missing-apk")){
   var lease=f.Native.Start("SERIAL-A",Metrics(),()=>false,true,false);
   Check(f.ControlAudit.Contains(" 30 basic 0 0"),"DisableStayAwakeOnStop OFF remains honored in basic mode");
   Check(f.Native.Stop(lease),"Basic mode with power restoration disabled remains stoppable");
  }
 }
 static void TestControlOnlyReconnect(){using(var f=new Fixture("control-only")){
  var lease=f.Native.Start("SERIAL-A",Metrics(),()=>false,true,true);f.Native.Suspend();
  var settings=AppSettings.CreateDefault();settings.Features.AutoStartDexOnDeviceConnected=false;
  var coordinator=new ScrcpyLaunchCoordinator();using(var transfers=new FileTransferCoordinator(Self,settings,f.Log,f.Registry))using(var scrcpy=new ScrcpyService(Path.Combine(f.Directory,"missing-scrcpy.exe"),1000,f.Runner,f.Adb,coordinator,transfers,f.Log)){
   var store=(SettingsService)Activator.CreateInstance(typeof(SettingsService),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{f.Log,f.Directory},null);
   var dex=new DexOrchestrator(f.Adb,new VirtualDisplayService(f.Adb,f.Log),scrcpy,coordinator,store,f.Log,settings,f.Registry,f.Native);
   var gate=typeof(DexOrchestrator).GetField("_operationGate",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(dex);
   var entered=new ManualResetEventSlim();var release=new ManualResetEventSlim();
   var changing=Task.Run(()=>{lock(gate){entered.Set();release.Wait();}});entered.Wait();
   try{var watch=System.Diagnostics.Stopwatch.StartNew();dex.UpdateNativeViewerState();Check(watch.ElapsedMilliseconds<300,"A power callback does not wait for or publish an intermediate native start/stop state");}
   finally{release.Set();changing.GetAwaiter().GetResult();}
   dex.RestoreNativeControlAsync("SERIAL-A",()=>true).GetAwaiter().GetResult();
   Check(f.Native.HasActiveControl&&!dex.IsRunning&&dex.CurrentSession==null,"Reconnect restores heartbeats without launching a PC window");
   Check(f.Launches==1&&f.Native.RetainedLease.NativeSessionToken==lease.NativeSessionToken,"Control-only reconnect reuses the original Android owner");
   int attaches=f.Attaches;
   var resumed=f.Native.Start("SERIAL-A",Metrics(),()=>false);
   Check(f.Attaches==attaches&&resumed.NativeResumed,"A later manual start reuses restored control without a false disconnect");
   int before=f.Audit.Length;dex.RestoreNativeControlAsync("SERIAL-A",()=>false).GetAwaiter().GetResult();
   Check(f.Audit.Length==before&&f.Native.HasActiveControl,"An obsolete reconnect callback cannot replace a newer control connection");
   Check(f.Native.Stop(lease),"A control-only retained session remains explicitly stoppable");
  }
 }}
 static void TestViewerPolicy(){using(var f=new Fixture("viewer-policy")){
  var lease=f.Native.Start("SERIAL-A",Metrics(),()=>false,true,true);
  Check(lease.NativeCompanionPolicyEnabled&&!lease.NativeViewerActive,"Verified code16 enables Companion policy without claiming a viewer is open");
  f.Native.SetViewerState(true,true);
  Check(SpinWait.SpinUntil(()=>f.ControlAudit.Contains("VIEWER "+lease.NativeSessionToken+" 1 1"),1500)&&lease.NativeViewerActive,"Actual viewer and power demand are sent over the existing control channel");
  f.Native.SetViewerState(false,false);
  Check(SpinWait.SpinUntil(()=>f.ControlAudit.Contains("VIEWER "+lease.NativeSessionToken+" 0 0"),1500)&&!lease.NativeViewerActive,"Control-only state can clear viewer/power demand independently");
  f.Native.Suspend();Check(!lease.NativeViewerActive,"Transport suspension clears the PC viewer marker");
  var resumed=f.Native.Start("SERIAL-A",Metrics(),()=>false,true,true,true);
  Check(resumed.NativeCompanionPolicyEnabled&&!resumed.NativeViewerActive,"Retained control recovery waits for a real viewer before enabling DeX policy");
  Check(f.Native.Stop(resumed),"Policy-enabled sessions retain explicit owned cleanup");
 }
 using(var f=new Fixture("old-policy-companion","policy-code5")){
  var lease=f.Native.Start("SERIAL-A",Metrics(),()=>false,true,true);
  Check(!lease.NativeCompanionConfigured&&!lease.NativeCompanionPolicyEnabled,"An older official Companion uses basic native mode");
  Check(f.Native.Stop(lease),"An older Companion session remains normally stoppable");
 }}
 static void Pump(Task task){var until=DateTime.UtcNow.AddSeconds(20);while(!task.IsCompleted&&DateTime.UtcNow<until){Application.DoEvents();Thread.Sleep(10);}if(!task.IsCompleted)throw new Exception("UI reconnect fixture timed out");task.GetAwaiter().GetResult();Application.DoEvents();}
 static void TestMainReconnectPolicy(){using(var f=new Fixture("main-reconnect-policy")){
  f.Log.EnableAutomaticLogs(f.Directory);
  var settings=AppSettings.CreateDefault();settings.Features.AutoStartDexOnDeviceConnected=false;settings.Features.AutoHideEnabled=false;settings.Timing.ConnectedStartDelayMs=0;
  var store=(SettingsService)Activator.CreateInstance(typeof(SettingsService),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{f.Log,f.Directory},null);
  var path=new PathService(store,f.Log,f.Runner);var wifi=new WirelessAdbService(f.Adb,store,settings,f.Log);var devices=new PhysicalDeviceRegistry();var launch=new ScrcpyLaunchCoordinator();
  var factory=new DeviceRuntimeServiceFactory(Path.Combine(f.Directory,"missing-scrcpy.exe"),Self,1000,f.Runner,f.Adb,launch,store,settings,f.Log,f.Registry);
  var runtime=factory.Create();var dex=new DexOrchestrator(f.Adb,runtime.VirtualDisplay,runtime.Scrcpy,launch,store,f.Log,settings,f.Registry,f.Native);
  typeof(DeviceRuntimeServiceSet).GetProperty("Dex").SetValue(runtime,dex,null);
  using(var monitor=new DeviceMonitorService(f.Adb,wifi,devices,f.Log,1000,1000))
  using(var hotkeys=new HotkeyService(f.Log,settings)) {
   var capture=new CaptureService(f.Adb,store,settings,f.Log);
   using(var captures=new CaptureCoordinator(hotkeys,capture,runtime.Scrcpy,runtime.SingleWindows,settings,f.Log))
   using(var autoHide=new AutoHideService(runtime.Scrcpy,runtime.SingleWindows,f.Log,60))
   using(var keymap=new KeyMappingService(runtime.Scrcpy,runtime.SingleWindows,f.Adb,settings,settings.KeyMappings,f.Log,runtime.CompanionGuardian.ForegroundApps)) {
    var environment=new EnvironmentCheckService(f.Adb,runtime.Scrcpy,path,f.Log,store,settings,()=>"SERIAL-A");
    using(var form=new MainForm(store,settings,f.Log,f.Adb,wifi,runtime.Scrcpy,runtime.SingleWindows,runtime.ScreenOff,launch,monitor,dex,captures,autoHide,new AutoStartService(f.Log),environment,keymap,runtime.FileTransfers,runtime.PhoneTransfers,f.Registry,runtime,devices,factory,path,capture,false)) {
     var handle=form.Handle; // Create a dispatcher without Shown/monitoring/hooks or a visible window.
     var hidden=BindingFlags.Instance|BindingFlags.NonPublic;var context=typeof(MainForm).GetField("_initialDeviceContext",hidden).GetValue(form);
     var lease=f.Native.Start("SERIAL-A",Metrics(),()=>false,true,true);f.Native.Suspend();
     var phone=new PhysicalDeviceInfo{Identity=lease.NativeDeviceIdentity,DisplayName="Synthetic phone"};phone.Transports.Add(new DeviceTransportInfo{Serial="SERIAL-A",Status=AdbDeviceStatus.Device,Kind=DeviceTransportKind.Usb});
     context.GetType().GetField("Identity").SetValue(context,phone.Identity);context.GetType().GetField("Device").SetValue(context,phone);
     context.GetType().GetField("ActiveSerial").SetValue(context,"SERIAL-A");context.GetType().GetField("WasConnected").SetValue(context,true);
     typeof(MainForm).GetField("_selectedDeviceContext",hidden).SetValue(form,null);
     var retry=typeof(MainForm).GetMethod("RetryContextCleanupCoreAsync",hidden);
     try {
      Pump((Task)retry.Invoke(form,new object[]{context,"SERIAL-A",true}));
      Check(f.Native.HasActiveControl&&!dex.IsRunning&&dex.CurrentSession==null,"Actual MainForm reconnect with auto-start OFF restores control and keeps the PC window closed");
      Check(!f.Log.GetSessionEntries().Any(s=>s.Contains(LocalizationService.Get("Log.Dex.StartFailed"))),"Auto-start OFF never attempts a scrcpy launch through the retained-session path");
      settings.Features.AutoStartDexOnDeviceConnected=true;
      Pump((Task)retry.Invoke(form,new object[]{context,"SERIAL-A",false}));
      Check(!f.Log.GetSessionEntries().Any(s=>s.Contains("Starting DeX automatically for")),"Existing allowAutoStart=false still suppresses window launch");
      context.GetType().GetField("RequireManualStartAfterTransportLoss").SetValue(context,true);
      Pump((Task)retry.Invoke(form,new object[]{context,"SERIAL-A",true}));
      Check(!f.Log.GetSessionEntries().Any(s=>s.Contains("Starting DeX automatically for")),"Existing manual-start-after-loss gate remains in force");
      context.GetType().GetField("RequireManualStartAfterTransportLoss").SetValue(context,false);
      Pump((Task)retry.Invoke(form,new object[]{context,"SERIAL-A",true}));
      Check(f.Log.GetSessionEntries().Count(s=>s.Contains("Starting DeX automatically for"))==1,"Auto-start ON follows the original launch path after control recovery");
      Check(f.Launches==1&&f.Journals==1,"A synthetic PC-window failure leaves the original retained Android owner intact");
      // The same existing power updater must run after a control-only stop, even without a scrcpy event.
      File.WriteAllText(Path.Combine(f.Directory,"power-SERIAL-A.txt"),"7");
      typeof(MainForm).GetMethod("SetStayAwakeOriginalValue",hidden).Invoke(form,new object[]{"SERIAL-A","0"});
      Pump(dex.StopAsync());
      Pump((Task)typeof(MainForm).GetField("_stayAwakeUpdateTask",hidden).GetValue(form));
      Check(File.ReadAllText(Path.Combine(f.Directory,"power-SERIAL-A.txt"))=="0","Control-only display release triggers the existing original-power restoration path");
      Check(!f.Audit.Contains("-s SERIAL-B shell settings put global stay_on_while_plugged_in"),"Control-only cleanup does not change a different phone's power setting");
      var wake=typeof(MainForm).GetMethod("WakePhoneScreen",hidden);
      typeof(MainForm).GetMethod("ForgetDeviceConnection",hidden).Invoke(form,new object[]{"SERIAL-A"});
      Check(!(bool)wake.Invoke(form,new object[]{"SERIAL-A"}),"Wake diagnostics preserve the disconnected-device skip behavior");
      typeof(MainForm).GetMethod("RecordDeviceConnected",hidden).Invoke(form,new object[]{"SERIAL-A"});
      f.SetMode("wake-fail");Check(!(bool)wake.Invoke(form,new object[]{"SERIAL-A"}),"A synthetic wake command failure is preserved without automatic retries");
      f.SetMode("normal");Check((bool)wake.Invoke(form,new object[]{"SERIAL-A"}),"The original explicit wake command still succeeds through the product path");
      typeof(MainForm).GetField("_exitInProgress",hidden).SetValue(form,true);
      typeof(MainForm).GetMethod("RequestAllRuntimeShutdown",hidden,null,Type.EmptyTypes,null).Invoke(form,null);
      Pump((Task)typeof(MainForm).GetMethod("RunExitCleanupAsync",hidden).Invoke(form,new object[]{new List<string>{"SERIAL-A"},"SERIAL-A"}));
      Check(f.Log.FlushAutomaticLogs(3000),"The actual normal-exit cleanup path persists its final diagnostic tail");
      f.Log.CompleteAutomaticLogs(2000);
      var detail=File.ReadAllText(Path.Combine(f.Directory,"logs","dxm-diagnostic-"+f.Log.AutomaticSessionId+".log"));
      Check(detail.Contains("transport-marked-disconnected")&&detail.Contains("success=False")&&detail.Contains("exitCode=1"),"Wake diagnostics distinguish skipped devices from a failed ADB command");
      Check(detail.Contains("Sending input keyevent 224")&&detail.Contains("success=True")&&detail.Contains("physical panel state was not queried"),"Wake diagnostics distinguish command acceptance from verified physical screen state");
      Check(detail.Contains("Runtime session cleanup returned")&&detail.Contains("Cleanup step completed: ADB server")&&detail.Contains("new process creation is now blocked"),"Exit diagnostics include screen cleanup, wake and the final ADB shutdown boundary");
      Check(detail.Contains("requested=1600x900@150")&&detail.Contains("reported=1600x900@150"),"Native detail records pair requested metrics with the owner's reported metrics");
     } finally {
      typeof(MainForm).GetField("_exitInProgress",hidden).SetValue(form,true);
      ((IDisposable)typeof(MainForm).GetField("_trayService",hidden).GetValue(form)).Dispose();
      dex.StopAsync().GetAwaiter().GetResult();
      f.Log.CompleteAutomaticLogs(2000);
     }
    }
   }
  }
  runtime.PhoneTransfers.Dispose();runtime.FileTransfers.Dispose();
 }}
 static void TestIsolation(){using(var f=new Fixture("isolation")){
  var first=f.Native.Start("SERIAL-A",Metrics(),()=>false);
  var secondService=new NativeDexSessionService(f.Adb,f.Log,f.Registry,f.Directory);var second=secondService.Start("SERIAL-B",Metrics(),()=>false);
  Check(first.NativeSessionToken!=second.NativeSessionToken&&f.Journals==2,"Different phones have independent tokens and journals");
  Check(!f.Native.Stop(first,"SERIAL-B"),"Wrong physical phone cannot stop a retained session");
  Check(f.Native.Stop(first)&&f.Journals==1&&File.Exists(StatePath(f.Directory,"SERIAL-B")),"Stopping phone A preserves phone B");
  Check(secondService.Stop(second),"Phone B still has an independent working cleanup channel");
 }}
 static void TestShutdown(){using(var f=new Fixture("shutdown")){
  var lease=f.Native.Start("SERIAL-A",Metrics(),()=>false);int before=f.Audit.Length;f.Runner.BlockNewProcessesForWindowsShutdown();
  Check(f.Native.SignalStopWithoutAdb(),"Windows shutdown signals an already-established control socket");
  Check(f.Native.Stop(lease),"Confirmed cleanup remains available after process creation is frozen");
  Check(f.Audit.Length==before,"Shutdown cleanup creates no new ADB process");
  Fails(()=>f.Adb.StartPersistentShellForSerial("SERIAL-A","test"),"Persistent process creation still respects the shutdown gate");
 }}
 static void TestOrchestrator(){foreach(bool legacy in new[]{false,true})using(var f=new Fixture("orchestrator-"+legacy)){
  var settings=AppSettings.CreateDefault();settings.VirtualDisplay.ActivationMode=legacy?DexDisplayMode.LegacyOverlay:DexDisplayMode.Native;
  f.Registry.SetSingleWindow("SERIAL-B",2,88,12345,new IntPtr(12345),true,false);
  var coordinator=new ScrcpyLaunchCoordinator();using(var transfers=new FileTransferCoordinator(Self,settings,f.Log,f.Registry))using(var scrcpy=new ScrcpyService(Path.Combine(f.Directory,"missing-scrcpy.exe"),1000,f.Runner,f.Adb,coordinator,transfers,f.Log)){
   var settingsService=(SettingsService)Activator.CreateInstance(typeof(SettingsService),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{f.Log,f.Directory},null);
   var dex=new DexOrchestrator(f.Adb,new VirtualDisplayService(f.Adb,f.Log),scrcpy,coordinator,settingsService,f.Log,settings,f.Registry,f.Native);
   Check(dex.UsesNativeDisplay!=legacy,"Per-phone setting selects the requested backend: "+legacy);
   if(!legacy){Fails(()=>dex.StartAsync("SERIAL-A").GetAwaiter().GetResult(),"Scrcpy launch failure propagates after native startup");Check(f.Journals==0,"A failed initial launch releases its partial native session");Check(!f.Audit.Contains("settings put global overlay"),"Native launch failure does not fall back to legacy creation");}
   else{dex.ShutdownAsync("SERIAL-A").GetAwaiter().GetResult();Check(f.Launches==0,"Legacy cleanup does not require native activation or Companion bridging");Check(f.Audit.Contains("settings delete global overlay_display_devices"),"Legacy overlay cleanup remains present");}
   Check(f.Registry.Current.FindByTransportSerial("SERIAL-B").SingleWindows.Count==1,"Other phone's single-window session survives");
  }
 }}

 static void TestSettings(){
  var settings=AppSettings.CreateDefault();var a=settings.GetOrCreateDeviceRunSettings("phone-a");var b=settings.GetOrCreateDeviceRunSettings("phone-b");
  Check(!settings.VirtualDisplay.KeepBluetoothAudioOnDexStart&&!a.VirtualDisplay.KeepBluetoothAudioOnDexStart&&!b.VirtualDisplay.KeepBluetoothAudioOnDexStart,"Startup Bluetooth preservation is opt-in for defaults and each phone");
  a.VirtualDisplay.KeepBluetoothAudioOnDexStart=true;
  a.VirtualDisplay.ActivationMode=DexDisplayMode.LegacyOverlay;b.VirtualDisplay.ActivationMode=DexDisplayMode.Native;
  Check(settings.VirtualDisplay.ActivationMode==DexDisplayMode.Native,"Adding a legacy phone preserves the default native test setting");
  var json=new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(AppSettings));
  using(var memory=new MemoryStream()){json.WriteObject(memory,settings);memory.Position=0;var copy=(AppSettings)json.ReadObject(memory);copy.EnsureDefaults();
   Check(copy.GetOrCreateDeviceRunSettings("phone-a").VirtualDisplay.ActivationMode==DexDisplayMode.LegacyOverlay,"Legacy choice survives a settings save/load");
   Check(copy.GetOrCreateDeviceRunSettings("phone-b").VirtualDisplay.ActivationMode==DexDisplayMode.Native,"Native choice remains independent on another phone");
   Check(copy.GetOrCreateDeviceRunSettings("phone-a").VirtualDisplay.KeepBluetoothAudioOnDexStart&&!copy.GetOrCreateDeviceRunSettings("phone-b").VirtualDisplay.KeepBluetoothAudioOnDexStart,"Startup Bluetooth choice survives serialization per phone");
   var cloned=(VirtualDisplaySettings)typeof(AppSettings).GetMethod("CloneVirtualDisplay",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{copy.GetOrCreateDeviceRunSettings("phone-a").VirtualDisplay});
   Check(cloned.KeepBluetoothAudioOnDexStart,"Cloning a phone's active virtual-display settings retains its Bluetooth policy");
  }
 }
 static void TestBrowserNegotiation(){
  using(var f=new Fixture("browser-negotiation")){
   var lease=f.Native.Start("SERIAL-A",Metrics(),()=>false);
   Check(lease.NativeBrowserCleanupEnabled&&f.Audit.Contains("--ez browser_policy true"),"Browser cleanup is enabled only for the verified new Companion/helper pair");
   Check(f.ControlAudit.Contains("CONFIGURE "+lease.NativeSessionToken+" 30 companion 0 0 1 1"),"The native owner explicitly acknowledges browser cleanup capability");
   f.Native.Stop(lease);
  }
  using(var f=new Fixture("browser-old-companion","policy-code6")){
   var lease=f.Native.Start("SERIAL-A",Metrics(),()=>false);
   Check(!lease.NativeBrowserCleanupEnabled,"Older trusted Companion does not silently enable browser cleanup");
   Check(!lease.NativeCompanionPolicyEnabled&&lease.NativeProtocolVersion==7,"Older code6 uses local fixed-grace audio/power fallback instead of an unsupported30-second delegate");f.Native.Stop(lease);
  }
  using(var f=new Fixture("browser-old-owner")){
   var lease=f.Native.Start("SERIAL-A",Metrics(),()=>false);
   lease.NativeRemoteJar="/data/local/tmp/dxm-ai-native-f98319091d78aac6.jar";
   var journal=System.IO.Directory.GetFiles(Path.Combine(f.Directory,"config","native-dex"),"*.json").Single();
   using(var output=File.Create(journal))new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(VirtualDisplayLease)).WriteObject(output,lease);
   f.Native.Suspend();
   Fails(()=>f.Native.Start("SERIAL-A",Metrics(),()=>false),"An older retained browser owner must stop before fixed-grace migration");
   Check(f.Native.Stop(lease),"Older retained browser owner cleanup remains token-scoped");
   Check(f.Audit.Split('\n').Any(line=>line.Contains("dxm-ai-native-f98319091d78aac6.jar")&&line.Contains(" recover ")),
    "An earlier private owner is recovered using its own helper and port");
  }
 }
 static void TestResumedStartAppCommands(){
  foreach(bool force in new[]{false,true})using(var f=new Fixture("resume-start-app-"+force)){
   var lease=f.Native.Start("SERIAL-A",Metrics(),()=>false);f.Native.Suspend();
   var settings=AppSettings.CreateDefault();settings.Scrcpy.StartAppPackage="com.sec.android.app.sbrowser";settings.Scrcpy.ForceStopStartApp=force;
   var coordinator=new ScrcpyLaunchCoordinator();using(var transfers=new FileTransferCoordinator(Self,settings,f.Log,f.Registry))using(var scrcpy=new ScrcpyService(FakeAdbPath,1000,f.Runner,f.Adb,coordinator,transfers,f.Log)){
    var store=(SettingsService)Activator.CreateInstance(typeof(SettingsService),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{f.Log,f.Directory},null);
    var dex=new DexOrchestrator(f.Adb,new VirtualDisplayService(f.Adb,f.Log),scrcpy,coordinator,store,f.Log,settings,f.Registry,f.Native);
    try{dex.ResumeAsync("SERIAL-A").GetAwaiter().GetResult();}catch(Exception){}
    var expected="--start-app="+(force?"+":"")+"com.sec.android.app.sbrowser";
    Check(f.Log.GetSessionEntries().Any(line=>line.Contains(expected)),"Retained resume uses configured start-app/force-stop arguments: "+force);
    Check(f.Launches==1,"Applying the start app on resume does not create a second native display");
    Check(settings.Scrcpy.StartAppPackage=="com.sec.android.app.sbrowser"&&settings.Scrcpy.ForceStopStartApp==force,"Resume keeps the stored app profile intact");
   }
   f.Native.Stop(lease);
  }
 }
 static void TestBluetoothStartup(){
  using(var f=new Fixture("bluetooth-startup")){
   var metrics=Metrics();metrics.KeepBluetoothAudioOnDexStart=true;
   var lease=f.Native.Start("SERIAL-A",metrics,()=>false);
   Check(lease.NativeKeepBluetoothAudioOnStart&&f.Audit.Split('\n').Any(line=>line.Contains("NativeDexServer launch ")&&line.TrimEnd().EndsWith(" 1")),"Enabled startup policy reaches the native owner");
   f.Native.Suspend();metrics.KeepBluetoothAudioOnDexStart=false;
   var resumed=f.Native.Start("SERIAL-A",metrics,()=>false);
   Check(resumed.NativeResumed&&f.Launches==1&&resumed.NativeKeepBluetoothAudioOnStart,"Reattachment neither recreates a display nor reapplies a changed startup setting");
   Check(f.Native.Stop(resumed),"Bluetooth-enabled native session keeps ordinary explicit cleanup");
  }
  using(var f=new Fixture("bluetooth-basic","missing-apk")){
   var metrics=Metrics();metrics.KeepBluetoothAudioOnDexStart=true;
   var lease=f.Native.Start("SERIAL-A",metrics,()=>false);
   Check(lease.NativeKeepBluetoothAudioOnStart&&!lease.NativeCompanionConfigured,"Startup-only Bluetooth preservation does not require Companion");
   f.Native.Stop(lease);
  }
 }
 static void TestResumeLaunchFailure(){using(var f=new Fixture("resume-launch-failure")){
  var lease=f.Native.Start("SERIAL-A",Metrics(),()=>false);f.Native.Suspend();
  var settings=AppSettings.CreateDefault();settings.Scrcpy.StartAppPackage="com.example.keep.running";
  var coordinator=new ScrcpyLaunchCoordinator();using(var transfers=new FileTransferCoordinator(Self,settings,f.Log,f.Registry))using(var scrcpy=new ScrcpyService(Path.Combine(f.Directory,"missing-scrcpy.exe"),1000,f.Runner,f.Adb,coordinator,transfers,f.Log)){
   var settingsService=(SettingsService)Activator.CreateInstance(typeof(SettingsService),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{f.Log,f.Directory},null);
   var dex=new DexOrchestrator(f.Adb,new VirtualDisplayService(f.Adb,f.Log),scrcpy,coordinator,settingsService,f.Log,settings,f.Registry,f.Native);
   var attached=f.Native.Start("SERIAL-A",Metrics(),()=>false);
   dex.SuspendForTransportLossAsync("SERIAL-A",()=>false).GetAwaiter().GetResult();
   Check(f.Native.HasActiveControl,"A stale disconnect callback cannot detach a newer active control channel");
   f.Native.Suspend();
   Fails(()=>dex.ResumeAsync("SERIAL-A").GetAwaiter().GetResult(),"A failed replacement PC window reports the resume failure");
   Check(f.Journals==1&&f.Launches==1&&f.Stops==0,"Resume failure preserves the existing Android display and app state");
   Check(settings.Scrcpy.StartAppPackage=="com.example.keep.running","Applying start-app on resume preserves the saved profile");
  }
  Check(f.Native.Stop(lease),"Retained display remains explicitly stoppable after a failed PC window restart");
 }}

 static string StatePath(string dir,string serial){return Path.Combine(dir,"phone-"+serial+".txt");}
 static int FakeAdb(string[] args){
  string dir=Environment.GetEnvironmentVariable("DXM_NATIVE_DIR"),mode=Environment.GetEnvironmentVariable("DXM_NATIVE_MODE")??"normal",all=string.Join(" ",args);
  if(!string.IsNullOrEmpty(dir))for(int retry=0;;retry++)try{File.AppendAllText(Path.Combine(dir,"audit.txt"),all+"\n");break;}catch(IOException){if(retry>=20)throw;Thread.Sleep(10);}
  string serial=args.Length>1&&args[0]=="-s"?args[1]:"SERIAL-A";
  if(args[0]=="devices"){Console.WriteLine("List of devices attached\nSERIAL-A\tdevice model:NativeTest\nSERIAL-B\tdevice model:NativeTestB");return 0;}
  if(all.Contains("get-state")){Console.WriteLine("device");return 0;}
  if(all.Contains("dumpsys power")){if(mode=="power-query-fail")return 1;Console.WriteLine("POWER MANAGER (dumpsys power)\n  mWakefulness="+(mode=="sleeping"&&!File.Exists(Path.Combine(dir,"awake.flag"))?"Asleep":"Awake"));return 0;}
  if(all.Contains("dumpsys package")){Console.WriteLine("versionCode="+(mode=="old-apk"?"3":mode=="policy-code5"?"13":mode=="policy-code6"?"15":"16")+" apkSigningVersion="+(mode=="untrusted-apk"?"1":"2")+"\nandroid.permission.WRITE_SECURE_SETTINGS: granted="+(mode=="no-permission"?"false":"true"));return 0;}
  if(all.Contains("pm path")){if(mode!="missing-apk")Console.WriteLine("package:/data/app/native-fixture/base.apk");return 0;}
  if(args.Contains("pull")){File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tools","companion","DX-Companion.apk"),args.Last(),true);return 0;}
  if(all.Contains("PREPARE_NATIVE_DENSITY")){Console.WriteLine(mode=="initial-fail"?"Broadcast completed: result=1":"Broadcast completed: result=0, data=\"DXNDPI1|160\"");return 0;}
  if(all.Contains("CONFIGURE_NATIVE_SESSION")){Console.WriteLine(mode=="bad-policy"?"Broadcast completed: result=1":"Broadcast completed: result=0, data=\"DXNP2|"+"30"+"\"");return 0;}
  if(all.Contains("settings get global overlay_display_devices")){Console.WriteLine(mode=="overlay"?"1920x1080/160,hdmi":"null");return 0;}
  if(all.Contains("settings get global stay_on_while_plugged_in")){var power=Path.Combine(dir,"power-"+serial+".txt");Console.WriteLine(File.Exists(power)?File.ReadAllText(power):"0");return 0;}
  if(all.Contains("settings put global stay_on_while_plugged_in")){File.WriteAllText(Path.Combine(dir,"power-"+serial+".txt"),args.Last());return 0;}
  if(all.Contains("settings delete global stay_on_while_plugged_in")){var power=Path.Combine(dir,"power-"+serial+".txt");if(File.Exists(power))File.Delete(power);return 0;}
  if(all.Contains("input keyevent 224")&&mode=="wake-fail"){Console.Error.WriteLine("synthetic wake failure");return 1;}
  if(all.Contains("sha256sum")){Console.WriteLine(Hash+"  "+args.Last());return 0;}
  if(all.Contains("forward tcp:0")){Console.WriteLine(Environment.GetEnvironmentVariable("DXM_NATIVE_PORT"));return 0;}
  int server=Array.IndexOf(args,"io.github.mazemei.dxmnativedex.NativeDexServer");
  if(server>=0){string operation=args[server+1],token=args[server+2],state=StatePath(dir,Environment.GetEnvironmentVariable("DXM_NATIVE_ALIAS")=="1"&&serial=="SERIAL-B"?"SERIAL-A":serial);
   if(operation=="launch"){if(args[server+6]!="30"){Console.Error.WriteLine("Native grace must be fixed30");return 1;}if((args.Length!=server+12&&args.Length!=server+13)||(args[server+11]!="0"&&args[server+11]!="1")){Console.Error.WriteLine("Invalid startup Bluetooth flag contract");return 1;}File.WriteAllText(state,token);Console.WriteLine("DXND1|"+token+"|LAUNCHED|1");return 0;}
   if(operation=="status"){Console.WriteLine("DXND1|"+token+"|PRESENCE|"+(File.Exists(state)&&File.ReadAllText(state)==token?"PRESENT":"ABSENT"));return 0;}
   if(operation=="recover"){if(mode!="recover-fail"&&File.Exists(state)&&File.ReadAllText(state)==token)File.Delete(state);Console.WriteLine("DXND1|"+token+"|RECOVERED|"+(mode=="recover-fail"?"0":"1"));return mode=="recover-fail"?1:0;}
  }
  return 0;
 }
}
