using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Net.Sockets;
using DexManager.Models;

namespace DexManager.Services
{
    /// <summary>Private Samsung native DeX backend. Foreground-app/key processing never calls this service.</summary>
    public sealed class NativeDexSessionService
    {
        private readonly AdbService _adb;
        private readonly LogService _log;
        private readonly DeviceRuntimeSessionRegistry _runtimeSessions;
        private readonly string _baseDirectory;
        private readonly object _sync = new object();
        private NativeDexProcessController _active;
        private VirtualDisplayLease _lease;
        private int _wakeWaitRevision;
        private string _wakeWaitSerial;

        public NativeDexSessionService(AdbService adb, LogService log, DeviceRuntimeSessionRegistry runtimeSessions,
            string baseDirectory = null)
        {
            _adb = adb;
            _log = log;
            _runtimeSessions = runtimeSessions;
            _baseDirectory = Path.GetFullPath(baseDirectory ?? AppDomain.CurrentDomain.BaseDirectory);
        }

        public event Action<string> SessionEnded;
        public event Action DisplayReleased;

        private void NotifyDisplayReleased() { var handler=DisplayReleased;if(handler!=null)handler(); }

        private void Trace(string phase,VirtualDisplayLease lease,string detail)
        {
            _log.Diagnostic("NativeDeX", "phase="+phase+"; serial="+(lease==null?"<none>":lease.Serial)+
                "; display="+(lease==null?-1:lease.DisplayId)+"; pending="+(lease!=null&&lease.NativeStartRequested)+
                "; companion="+(lease!=null&&lease.NativeCompanionConfigured)+"; "+detail);
        }

        public VirtualDisplayLease RetainedLease { get { lock(_sync)return _lease; } }
        private int _forwardPort;
        private string _forwardSerial;
        public bool HasActiveControl { get { lock(_sync)return _active!=null&&!_active.Completion.IsCompleted; } }
        public bool HasRetainedDisplay { get { var lease=RetainedLease;return lease!=null&&lease.NativeStartRequested; } }
        public void SetViewerState(bool active,bool powerRequested) {
            lock(_sync) {
                if(_lease==null||(_lease.NativeProtocolVersion<7&&!_lease.NativeCompanionPolicyEnabled)||_lease.NativeProtocolVersion<4)return;
                _lease.NativeViewerActive=active&&_active!=null&&!_active.Completion.IsCompleted;
                if(_active!=null)_active.SetViewer(_lease.NativeViewerActive,powerRequested);
            }
        }
        public bool HasPendingForSerial(string serial) {
            lock(_sync) {
                if(_lease!=null)return _lease.NativeStartRequested;
                _lease=ReadJournal(_runtimeSessions.ResolveIdentity(serial));return _lease!=null&&_lease.NativeStartRequested;
            }
        }
        public void RefreshRetainedPresence() {
            var lease=RetainedLease;
            if(lease==null||_adb.IsProcessShutdownRequested||!CanUseTransport(lease,lease.Serial))return;
            try {
                var result=_adb.ShellForSerial(lease.Serial,Command(lease.NativeRemoteJar,"status",lease.NativeSessionToken),false,5000);
                if(result.IsSuccess&&(result.StandardOutput??"").Contains("|PRESENCE|ABSENT")) {
                    Trace("presence-absent",lease,"Owned display is no longer present.");
                    ClearJournal(lease);lease.NativeStartRequested=false;lock(_sync)_lease=null;
                    NotifyDisplayReleased();
                }
            }catch(Exception error){_log.Warning("[Native DeX] Presence recheck deferred: "+error.Message);}
        }
        public void Suspend() {
            NativeDexProcessController process;
            lock(_sync){process=_active;_active=null;if(_lease!=null)_lease.NativeViewerActive=false;}
            if(process!=null)process.Dispose();
            ReleaseForward();
            Trace("control-detached",RetainedLease,"hadControl="+(process!=null));
            if(process!=null&&HasRetainedDisplay)_log.Progress(LocalizationService.Get("Log.Native.ReconnectGrace"));
        }
        public VirtualDisplayLease Start(string serial,VirtualDisplaySettings settings,Func<bool> canceled,
            bool stayAwake=false,bool restoreStayAwake=true,bool resumeOnly=false) {
            if(canceled!=null&&canceled())throw new OperationCanceledException();
            var identity=_runtimeSessions.ResolveIdentity(serial);
            _log.Diagnostic("NativeDeX","Start requested; serial="+serial+"; identity="+identity+
                "; requested="+settings.Width+"x"+settings.Height+"@"+settings.Dpi+"; resumeOnly="+resumeOnly+
                "; stayAwake="+stayAwake+"; restoreStayAwake="+restoreStayAwake);
            new VirtualDisplayService(_adb, _log).RemoveOverlayForNativeDex(serial, canceled);
            lock (_sync)
            {
                if (_lease != null && _lease.NativeDeviceIdentity == identity && _lease.Serial == serial &&
                    _lease.NativeStartRequested && _active != null && !_active.Completion.IsCompleted)
                {
                    // Reusing a restored control connection must not briefly detach an immediate-cleanup session.
                    _lease.NativeResumed = true;
                    _log.Progress(LocalizationService.Format("Log.Native.Reuse",_lease.DisplayId,_lease.NativeWidth,_lease.NativeHeight,_lease.NativeDpi));
                    Trace("reuse-live-control",_lease,"Existing metrics preserved: "+_lease.NativeWidth+"x"+_lease.NativeHeight+"@"+_lease.NativeDpi);
                    return _lease;
                }
            }
            _log.Progress(LocalizationService.Get("Log.Native.CheckExisting"));
            var previous=ReadJournal(identity);
            if(previous!=null&&previous.NativeStartRequested) {
                if(!CanUseTransport(previous,serial))throw new InvalidOperationException("Native reattachment requires the same physical phone.");
                var presence=_adb.ShellForSerial(serial,Command(ResolveRecoveryHelper(previous, serial),"status",previous.NativeSessionToken),false,8000);
                Trace("presence-query",previous,"success="+presence.IsSuccess+"; response="+presence.StandardOutput+"; stderr="+presence.StandardError);
                if(presence.IsSuccess&&(presence.StandardOutput??"").Contains("|PRESENCE|PRESENT")) {
                    if(IsPrivateCompanionOwner(previous)||previous.NativeProtocolVersion!=NativeDexProtocol.Version)throw new InvalidOperationException(LocalizationService.Get("Error.Native.OlderOwner"));
                    previous.Serial=serial;previous.NativeResumed=true;
                    lock(_sync)_lease=previous;
                    return Attach(previous,serial,canceled);
                }
                if(!presence.IsSuccess)throw new IOException("Pending native state could not be verified; it was preserved.");
                if((presence.StandardOutput??"").Contains("|PRESENCE|STALE")&&!Stop(previous,serial))
                    throw new IOException("Failed native owner needs recovery; no replacement display was created.");
                else if(!(presence.StandardOutput??"").Contains("|PRESENCE|ABSENT")&&!(presence.StandardOutput??"").Contains("|PRESENCE|STALE"))
                    throw new IOException("Unknown native presence; saved session preserved.");
                var wasRequested=previous.NativeStartRequested;
                ClearJournal(previous);ClearCompanionBinding(previous,serial);previous.NativeStartRequested=false;lock(_sync)_lease=null;
                if(wasRequested)NotifyDisplayReleased();
            }
            if(resumeOnly)return null;
            WaitForPhoneAwake(serial,identity,canceled);
            _log.Progress(LocalizationService.Format("Log.Native.Creating",settings.Width,settings.Height,settings.Dpi));
            var lease=new VirtualDisplayLease {IsNativeDex=true,Serial=serial,NativeDeviceIdentity=identity,
                NativeSessionToken=NativeDexProtocol.NewToken(),NativeProtocolVersion=NativeDexProtocol.Version,NativeStayAwakeRequested=stayAwake,
                NativeKeepBluetoothAudioOnStart=settings.KeepBluetoothAudioOnDexStart,
                NativeRestoreStayAwake=stayAwake&&restoreStayAwake,OwnsOverlaySetting=false};
            lease.NativePeerAddress=NativeDexProtocol.PeerAddress(lease.NativeSessionToken);
            try {
                if(canceled!=null&&canceled())throw new OperationCanceledException();
                var runtime=_runtimeSessions.Current.FindByIdentity(identity);
                if(runtime!=null&&runtime.PhonePower.StayAwakeOverrideApplied)lease.NativeOriginalStayAwake=runtime.PhonePower.OriginalStayAwakeValue;
                else {
                    var original=_adb.ShellForSerial(serial,"settings get global stay_on_while_plugged_in",false);
                    if(!original.IsSuccess)throw new IOException("Could not capture the original phone power setting.");
                    lease.NativeOriginalStayAwake=(original.StandardOutput??"").Trim();
                }
                var delay=ConfigureCompanion(lease,serial);
                PrepareInitialMetrics(lease,serial,settings);
                Trace("launch-plan",lease,"requested="+settings.Width+"x"+settings.Height+"@"+settings.Dpi+
                    "; cleanupDelaySeconds="+delay+"; restorePower="+lease.NativeRestoreStayAwake+"; originalPower="+lease.NativeOriginalStayAwake+
                    "; keepBluetoothAudio="+lease.NativeKeepBluetoothAudioOnStart);
                var powerOriginal=PowerRecoveryValue(lease.NativeOriginalStayAwake);
                lease.NativeRemoteJar=EnsureHelper(serial);WriteJournal(lease);
                if(canceled!=null&&canceled())throw new OperationCanceledException();
                lease.NativeStartRequested=true;WriteJournal(lease);lock(_sync)_lease=lease;
                var command=Command(lease.NativeRemoteJar,"launch",lease.NativeSessionToken)+" "+settings.Width+" "+settings.Height+" "+settings.Dpi+" "+delay+
                    " "+(lease.NativeCompanionConfigured?"companion":"basic")+" "+(lease.NativeRestoreStayAwake?"1":"0")+" "+powerOriginal+" "+(lease.NativeCompanionPolicyEnabled?"1":"0")+
                    " "+(lease.NativeKeepBluetoothAudioOnStart?"1":"0")+(lease.NativeInitialMetricsPrepared?" 1":"");
                var launched=_adb.ShellForSerial(serial,command,false,18000);
                Trace("launch-result",lease,"success="+launched.IsSuccess+"; exitCode="+launched.ExitCode+
                    "; timedOut="+launched.TimedOut+"; elapsedMs="+launched.Duration.TotalMilliseconds+"; stderr="+launched.StandardError);
                if(!launched.IsSuccess||!(launched.StandardOutput??"").Contains("DXND1|"+lease.NativeSessionToken+"|LAUNCHED|1"))
                    throw new IOException("The detached Android native owner did not launch. Check the native helper log.");
                return Attach(lease,serial,canceled);
            }catch(Exception error){if(!lease.NativeStartRequested)ClearCompanionBinding(lease,serial);Trace("start-exception",lease,error.GetType().Name+": "+error.Message);error.Data[VirtualDisplayService.RetainedLeaseDataKey]=lease;throw;}
        }
        // Cancel only a pre-creation wait. Existing retained displays and control connections are unaffected.
        public void CancelPendingStart(string serial=null) {
            lock(_sync)if(serial==null||string.Equals(serial,_wakeWaitSerial,StringComparison.OrdinalIgnoreCase))_wakeWaitRevision++;
        }
        private void WaitForPhoneAwake(string serial,string identity,Func<bool> canceled) {
            int revision;
            lock(_sync){revision=_wakeWaitRevision;_wakeWaitSerial=serial;}
            Func<bool> interrupted=delegate {
                lock(_sync)return revision!=_wakeWaitRevision||_adb.IsProcessShutdownRequested||(canceled!=null&&canceled());
            };
            try {
                NativeStartReadiness.Wait(delegate {
                    if(!string.Equals(_runtimeSessions.ResolveIdentity(serial),identity,StringComparison.OrdinalIgnoreCase))throw new OperationCanceledException();
                    var power=_adb.ShellForSerial(serial,"dumpsys power",false,1500);
                    if(interrupted())throw new OperationCanceledException();
                    if(!power.IsSuccess)throw new IOException(LocalizationService.Get("Error.Native.WakeQueryFailed"));
                    return NativeStartReadiness.Parse(power.StandardOutput);
                },interrupted,System.Threading.Thread.Sleep,delegate(string state) {
                    if(state=="waiting")_log.Progress(LocalizationService.Format("Log.Native.WaitForAwake",serial));
                    else if(state=="awake")_log.Progress(LocalizationService.Format("Log.Native.PhoneAwake",serial));
                    else _log.Warning(LocalizationService.Format("Log.Native.WakeUnknown",serial));
                });
            }finally{lock(_sync)if(_wakeWaitSerial==serial)_wakeWaitSerial=null;}
        }
        private VirtualDisplayLease Attach(VirtualDisplayLease lease,string serial,Func<bool> canceled) {
            NativeDexProcessController process = null, previous;
            int previousPort, port = 0; string previousSerial;
            lock (_sync) { previous = _active; previousPort = _forwardPort; previousSerial = _forwardSerial; }
            try {
                var forward=_adb.ForwardNativeForSerial(serial);
                if(!forward.IsSuccess||!int.TryParse((forward.StandardOutput??"").Trim(),out port)||port<1||port>65535)
                    throw new IOException("Could not open the serial-scoped native control tunnel.");
                Trace("control-attach",lease,"serial="+serial+"; localForward="+port+"; replacingControl="+(previous!=null));
                process=new NativeDexProcessController(port,lease.NativeSessionToken,_log);
                var ready=process.AwaitReady(45000,canceled);
                lease.DisplayId=ready.DisplayId;lease.NativeWidth=ready.Width;lease.NativeHeight=ready.Height;lease.NativeDpi=ready.Dpi;
                lease.NativeHelperPid=ready.HelperPid;lease.NativeGuardPid=ready.GuardPid;
                Trace("ready",lease,"reported="+ready.Width+"x"+ready.Height+"@"+ready.Dpi+
                    "; resumed="+lease.NativeResumed+"; ownerPid="+ready.HelperPid+"; guardPid="+ready.GuardPid);
                var delay = ConfigureCompanion(lease,serial);
                ConfigurePhonePolicy(port, lease, delay);
                Trace("policy-acknowledged",lease,"delaySeconds="+delay+"; protocol="+lease.NativeProtocolVersion);
                if(canceled!=null&&canceled())throw new OperationCanceledException();
                WriteJournal(lease);
                lock(_sync){_active=process;_lease=lease;_lease.NativeViewerActive=false;_forwardPort=port;_forwardSerial=serial;}
                if(lease.NativeProtocolVersion>=7||lease.NativeCompanionPolicyEnabled)process.SetViewer(false,false);
                // Attach the new epoch before closing the old one; zero-delay policies must not see a false disconnect.
                if(previous!=null)previous.Dispose();
                if(previousPort!=port||previousSerial!=serial)RemoveForward(previousSerial,previousPort);
                _log.Progress(LocalizationService.Format(lease.NativeResumed?"Log.Native.Reattached":"Log.Native.Ready",lease.DisplayId,ready.Width,ready.Height,ready.Dpi));
                process.Completion.ContinueWith(delegate {
                    lock(_sync){if(!ReferenceEquals(_active,process)||process.StopRequested)return;}
                    var handler=SessionEnded;if(handler!=null)handler(lease.NativeSessionToken);
                },System.Threading.Tasks.TaskScheduler.Default);
                return lease;
            }catch(Exception error){
                Trace("attach-exception",lease,error.GetType().Name+": "+error.Message);
                if(process!=null) {
                    process.Dispose();
                    // ATTACH may already have advanced the phone's owner epoch. The old socket can no
                    // longer be considered healthy if publishing the replacement failed afterwards.
                    if(previous!=null)previous.Dispose();
                    lock(_sync){if(ReferenceEquals(_active,previous))_active=null;}
                    RemoveForward(previousSerial,previousPort);
                    _forwardPort=0;_forwardSerial=null;
                }
                if(port!=previousPort||serial!=previousSerial)RemoveForward(serial,port);
                error.Data[VirtualDisplayService.RetainedLeaseDataKey]=lease;throw;
            }
        }
        private void PrepareInitialMetrics(VirtualDisplayLease lease,string serial,VirtualDisplaySettings settings) {
            bool supported=settings.Width==1600&&settings.Height==900||settings.Width==1920&&settings.Height==1080;
            if(!lease.NativeCompanionConfigured||lease.NativeCompanionVersion<DisplayCleanupPermissionService.BundledVersionCode||!supported){
                _log.Warning(LocalizationService.Format("Log.Native.InitialCompatibility",
                    DisplayCleanupPermissionService.BundledVersionCode));
                return;
            }
            int initialDpi=Math.Max(160,settings.Dpi);
            var command="am broadcast --receiver-foreground -n "+DisplayCleanupPermissionService.PackageName+"/io.github.mazemei.dxdisplaycleanup.NativeSessionReceiver -a "+DisplayCleanupPermissionService.PackageName+".PREPARE_NATIVE_DENSITY --es native_token "+lease.NativeSessionToken+" --ei density "+initialDpi;
            var result=_adb.ShellForSerial(serial,command,false,8000);
            if(!result.IsSuccess||!(result.StandardOutput??"").Contains("DXNDPI1|"+initialDpi))throw new InvalidOperationException(LocalizationService.Get("Error.Native.InitialPreparation"));
            lease.NativeInitialMetricsPrepared=true;
            _log.Progress(LocalizationService.Format("Log.Native.InitialPrepared",settings.Width,settings.Height,initialDpi));
            if(settings.Dpi<initialDpi)_log.Progress(LocalizationService.Format("Log.Native.InitialLowerDpi",initialDpi,settings.Dpi));
        }
        private int ConfigureCompanion(VirtualDisplayLease lease,string serial) {
            var permission=new DisplayCleanupPermissionService(_adb);var status=permission.Inspect(serial);
            lease.NativeCompanionVersion=status.VersionCode;
            if(status.State!=DisplayCleanupPermissionState.Granted||status.VersionCode<DisplayCleanupPermissionService.BundledVersionCode) {
                // Optional integration: never grant permission or send the native token to an unverified APK.
                if(status.State==DisplayCleanupPermissionState.Ready&&status.VersionCode>=DisplayCleanupPermissionService.BundledVersionCode)ClearCompanionBinding(lease,serial);
                lease.NativeCompanionConfigured=false;
                lease.NativeCompanionPolicyEnabled=false;
                lease.NativeBrowserCleanupEnabled=false;
                _log.Info("[Native DeX] Companion integration unavailable ("+status.State+"); using basic mode with a fixed30-second reconnect grace.");
                return NativeDexProtocol.DisconnectGraceSeconds;
            }
            var original=PowerRecoveryValue(lease.NativeOriginalStayAwake);
            lease.NativeCompanionPolicyEnabled=lease.NativeProtocolVersion>=7;
            lease.NativeBrowserCleanupEnabled=lease.NativeCompanionPolicyEnabled;
            if(!lease.NativeCompanionPolicyEnabled)_log.InfoOnce("native-retained-policy-"+serial,
                "[Native DeX] Update Companion for native grace integration. Basic local audio/power handling remains active.");
            var command="am broadcast --receiver-foreground -n "+DisplayCleanupPermissionService.PackageName+"/io.github.mazemei.dxdisplaycleanup.NativeSessionReceiver -a "+DisplayCleanupPermissionService.PackageName+".CONFIGURE_NATIVE_SESSION --es native_token "+lease.NativeSessionToken+" --ez restore "+(lease.NativeRestoreStayAwake?"true":"false")+" --es original "+original+
                " --ez retained_policy "+(lease.NativeCompanionPolicyEnabled?"true":"false")+
                " --ez browser_policy "+(lease.NativeBrowserCleanupEnabled?"true":"false");
            lease.NativeCompanionConfigured=true;
            try {
                var result=_adb.ShellForSerial(serial,command,false,8000);
                var match=System.Text.RegularExpressions.Regex.Match(result.StandardOutput??"",@"DXNP2\|(-?\d+)");int delay;
                if(result.IsSuccess&&match.Success&&int.TryParse(match.Groups[1].Value,out delay)&&
                    (delay==NativeDexProtocol.DisconnectGraceSeconds||(!lease.NativeCompanionPolicyEnabled&&Array.IndexOf(new[]{-1,0,60,300,600,1800},delay)>=0)))
                    return NativeDexProtocol.DisconnectGraceSeconds;
                _log.Warning("[Native DeX] Companion integration unavailable; the fixed30-second grace remains active.");
            }catch(Exception error){
                _log.Warning("[Native DeX] Optional Companion policy check failed; basic mode will be used: "+error.Message);
            }
            ClearCompanionBinding(lease,serial);lease.NativeCompanionConfigured=false;lease.NativeCompanionPolicyEnabled=false;lease.NativeBrowserCleanupEnabled=false;
            return NativeDexProtocol.DisconnectGraceSeconds;
        }
        private static string PowerRecoveryValue(string value) {
            if(value=="null"||value=="<missing>"||string.IsNullOrEmpty(value))return "missing";
            int parsed;
            if(!int.TryParse(value,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out parsed)||parsed<0)
                throw new InvalidDataException("Invalid native power recovery value.");
            return parsed.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        private static void ConfigurePhonePolicy(int port,VirtualDisplayLease lease,int delay) {
            using(var client=new TcpClient()) {
                var connect=client.ConnectAsync("127.0.0.1",port);
                if(!connect.Wait(1500))throw new IOException("Native policy connection timed out.");
                connect.GetAwaiter().GetResult();client.ReceiveTimeout=3000;client.SendTimeout=3000;
                var command=lease.NativeProtocolVersion>=3
                    ? "CONFIGURE "+lease.NativeSessionToken+" "+delay+" "+(lease.NativeCompanionConfigured?"companion":"basic")+" "+(lease.NativeRestoreStayAwake?"1":"0")+" "+PowerRecoveryValue(lease.NativeOriginalStayAwake)
                    : "POLICY "+lease.NativeSessionToken+" "+delay;
                if(lease.NativeProtocolVersion>=4)command+=" "+(lease.NativeCompanionPolicyEnabled?"1":"0");
                if(lease.NativeProtocolVersion>=6)command+=" "+(lease.NativeBrowserCleanupEnabled?"1":"0");
                using(var output=new StreamWriter(client.GetStream(),new UTF8Encoding(false)){AutoFlush=true})
                using(var input=new StreamReader(client.GetStream(),Encoding.UTF8)) {
                    output.WriteLine(command);
                    var line=new StringBuilder();int ch;
                    while((ch=input.Read())!=-1&&ch!='\n'){if(ch!='\r')line.Append((char)ch);if(line.Length>256)throw new IOException("Native policy reply limit.");}
                    string kind;string[] data;
                    if(!NativeDexProtocol.TryRead(lease.NativeSessionToken,line.ToString(),out kind,out data)||
                        kind!=(lease.NativeProtocolVersion>=3?"CONFIGURED":"POLICY")||data.Length!=1||data[0]!=delay.ToString(System.Globalization.CultureInfo.InvariantCulture))
                        throw new IOException("Native cleanup policy was not acknowledged.");
                }
            }
        }
        private void ReleaseForward() {
            var port=_forwardPort;var serial=_forwardSerial;_forwardPort=0;_forwardSerial=null;
            RemoveForward(serial,port);
        }
        private void RemoveForward(string serial,int port) {
            if(port>0&&!_adb.IsProcessShutdownRequested&&!string.IsNullOrWhiteSpace(serial))
                try{_adb.RemoveNativeForwardForSerial(serial,port);}catch(Exception){}
        }

        public bool Stop(VirtualDisplayLease lease, string recoverySerial = null)
        {
            if (lease == null || !lease.IsNativeDex) return true;
            if(recoverySerial!=null&&!string.Equals(_runtimeSessions.ResolveIdentity(recoverySerial),lease.NativeDeviceIdentity,StringComparison.OrdinalIgnoreCase))return false;
            if (!lease.NativeStartRequested) { ClearJournal(lease);ClearCompanionBinding(lease,recoverySerial??lease.Serial);return true; }
            var stopWatch=System.Diagnostics.Stopwatch.StartNew();
            Trace("stop-request",lease,"recoverySerial="+recoverySerial+"; processShutdown="+_adb.IsProcessShutdownRequested);
            NativeDexProcessController process = null;
            lock (_sync)
            {
                if (_lease != null && _lease.NativeSessionToken == lease.NativeSessionToken) process = _active;
            }
            bool clean = process != null && process.WaitStopped(12000);
            Trace("stop-control-result",lease,"confirmed="+clean+"; elapsedMs="+stopWatch.ElapsedMilliseconds);
            var serial = recoverySerial ?? lease.Serial;
            if (!clean && !_adb.IsProcessShutdownRequested && CanUseTransport(lease, serial))
            {
                try
                {
                    var remoteJar = ResolveRecoveryHelper(lease, serial);
                    var result = _adb.ShellForSerial(serial, Command(remoteJar, "recover", lease.NativeSessionToken), false, 18000);
                    clean = result.IsSuccess && NativeDexProtocol.Recovered(lease.NativeSessionToken, result.StandardOutput);
                    Trace("recovery-result",lease,"success="+result.IsSuccess+"; confirmed="+clean+
                        "; exitCode="+result.ExitCode+"; timedOut="+result.TimedOut+"; elapsedMs="+stopWatch.ElapsedMilliseconds);
                }
                catch (Exception e) { _log.Warning("[Native DeX] Recovery remains pending: " + e.Message); }
            }
            if (!clean)
            {
                Trace("stop-unconfirmed",lease,"elapsedMs="+stopWatch.ElapsedMilliseconds+"; recovery ownership preserved.");
                _log.Warning("[Native DeX] Cleanup is not yet confirmed; the phone guard/recovery marker remains active.");
                return false;
            }
            if (process != null) process.Dispose();
            ReleaseForward();
            lock (_sync)
            {
                if (_lease != null && _lease.NativeSessionToken == lease.NativeSessionToken)
                { _active = null; _lease = null; }
            }
            ClearJournal(lease);
            ClearCompanionBinding(lease,serial);
            lease.NativeStartRequested = false;
            Trace("stop-confirmed",lease,"elapsedMs="+stopWatch.ElapsedMilliseconds);
            NotifyDisplayReleased();
            _log.Progress("[Native DeX] The owned Samsung DeX display was removed; other receivers were left untouched.");
            return true;
        }

        private void ClearCompanionBinding(VirtualDisplayLease lease,string serial) {
            if(!lease.NativeCompanionConfigured||_adb.IsProcessShutdownRequested||!CanUseTransport(lease,serial))return;
            try {
                var result=_adb.ShellForSerial(serial,"am broadcast --receiver-foreground -n "+DisplayCleanupPermissionService.PackageName+"/io.github.mazemei.dxdisplaycleanup.NativeSessionReceiver -a "+DisplayCleanupPermissionService.PackageName+".CLEAR_NATIVE_SESSION --es native_token "+lease.NativeSessionToken,false,5000);
                if(result.IsSuccess)lease.NativeCompanionConfigured=false;
            }catch(Exception error){_log.Warning("[Native DeX] Companion status clear deferred: "+error.Message);}
        }
        public bool RecoverForSerial(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return true;
            var previous = ReadJournal(_runtimeSessions.ResolveIdentity(serial));
            return previous == null || Stop(previous, serial);
        }

        public bool SignalStopWithoutAdb()
        {
            NativeDexProcessController process;
            lock (_sync) process = _active;
            if (process == null) return false;
            Trace("stop-existing-channel",RetainedLease,"No new ADB process requested.");
            process.RequestStop();
            return true;
        }

        private bool CanUseTransport(VirtualDisplayLease lease, string serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return false;
            if (!string.Equals(_runtimeSessions.ResolveIdentity(serial), lease.NativeDeviceIdentity, StringComparison.OrdinalIgnoreCase))
                return false;
            return _adb.IsAuthorizedDeviceConnected(serial);
        }

        private string EnsureHelper(string serial)
        {
            var path = Path.Combine(_baseDirectory, "tools", "native-dex", "dxm-native-dex.jar");
            if (!File.Exists(path)) throw new FileNotFoundException("The private native DeX helper is missing; rebuild the test preview.", path);
            string hash;
            using (var algorithm = SHA256.Create())
            using (var input = File.OpenRead(path))
                hash = BitConverter.ToString(algorithm.ComputeHash(input)).Replace("-", "");
            if (!string.Equals(hash, NativeDexHelperRelease.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The native DeX helper hash does not match this test executable.");
            var remote = "/data/local/tmp/dxm-github-native-" + hash.Substring(0, 16).ToLowerInvariant() + ".jar";
            var push = _adb.PushForSerial(serial, path, remote);
            if (!push.IsSuccess) throw new IOException("Could not upload the native DeX helper: " + push.StandardError);
            var verify = _adb.ShellForSerial(serial, "sha256sum " + remote, false, 8000);
            var output = (verify.StandardOutput ?? string.Empty).Trim();
            if (!verify.IsSuccess || !output.StartsWith(hash, StringComparison.OrdinalIgnoreCase) ||
                output.Length < hash.Length + 1 || !char.IsWhiteSpace(output[hash.Length]))
                throw new IOException("The uploaded native DeX helper failed integrity verification.");
            return remote;
        }

        private static bool IsPrivateCompanionOwner(VirtualDisplayLease lease)
        {
            return (lease.NativeRemoteJar ?? "").StartsWith("/data/local/tmp/dxm-ai-native-", StringComparison.Ordinal);
        }

        private string ResolveRecoveryHelper(VirtualDisplayLease lease, string serial)
        {
            // The previous helper can still stop its own display on the old port.
            return IsPrivateCompanionOwner(lease) ? lease.NativeRemoteJar : EnsureHelper(serial);
        }

        internal static string Command(string remoteJar, string mode, string token)
        {
            NativeDexProtocol.ValidateToken(token);
            if ((mode != "launch" && mode != "recover" && mode != "status") || remoteJar == null ||
                !System.Text.RegularExpressions.Regex.IsMatch(remoteJar, "^/data/local/tmp/dxm-(?:ai|github)-native-[0-9a-f]{16}\\.jar$"))
                throw new ArgumentException("Invalid native helper invocation.");
            return "CLASSPATH=" + remoteJar + " app_process / io.github.mazemei.dxmnativedex.NativeDexServer " + mode + " " + token;
        }

        private string JournalPath(string identity)
        {
            if (string.IsNullOrWhiteSpace(identity)) throw new InvalidOperationException("A physical device identity is required.");
            byte[] hash;
            using (var algorithm = SHA256.Create()) hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(identity));
            return Path.Combine(_baseDirectory, "config", "native-dex", BitConverter.ToString(hash).Replace("-", "") + ".json");
        }

        private VirtualDisplayLease ReadJournal(string identity)
        {
            var path = JournalPath(identity);
            if (!File.Exists(path)) return null;
            try
            {
                using (var input = File.OpenRead(path))
                {
                    var lease = (VirtualDisplayLease)new DataContractJsonSerializer(typeof(VirtualDisplayLease)).ReadObject(input);
                    NativeDexProtocol.ValidateToken(lease.NativeSessionToken);
                    if (!lease.IsNativeDex || lease.NativeDeviceIdentity != identity ||
                        !NativeDexProtocol.PeerAddress(lease.NativeSessionToken).Equals(lease.NativePeerAddress, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Invalid native recovery ownership.");
                    return lease;
                }
            }
            catch (Exception e) { throw new InvalidDataException("Native DeX recovery data could not be verified; it was preserved.", e); }
        }

        private void WriteJournal(VirtualDisplayLease lease)
        {
            var path = JournalPath(lease.NativeDeviceIdentity);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temp = path + ".new";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                new DataContractJsonSerializer(typeof(VirtualDisplayLease)).WriteObject(stream, lease);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }

        private void ClearJournal(VirtualDisplayLease lease)
        {
            var existing = ReadJournal(lease.NativeDeviceIdentity);
            if (existing != null && existing.NativeSessionToken == lease.NativeSessionToken)
                File.Delete(JournalPath(lease.NativeDeviceIdentity));
        }
    }
}
