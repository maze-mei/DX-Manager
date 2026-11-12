using System;
using System.Threading;
using DexManager.Utils;
namespace DexManager.Services {
 internal static class PhonePowerKeyDispatcher {
  internal static bool Queue(IntPtr window,ScrcpyService scrcpy,SingleWindowService single,AdbService adb,LogService log) {
   if(adb==null)return false;
   string serial;int pid;
   if(!(scrcpy.TryGetWindowTarget(window,out serial,out pid)||single.TryGetWindowTarget(window,out serial,out pid)))return false;
   var targetSerial=serial;var targetPid=pid;
   ThreadPool.QueueUserWorkItem(delegate {
    string currentSerial;int currentPid;
    if(!(scrcpy.TryGetWindowTarget(window,out currentSerial,out currentPid)||single.TryGetWindowTarget(window,out currentSerial,out currentPid))||currentPid!=targetPid||!string.Equals(currentSerial,targetSerial,StringComparison.OrdinalIgnoreCase))return;
    try{var result=adb.ShellForSerial(targetSerial,"input keyevent 26",true);if(!result.IsSuccess)log.Warning("Power key could not be sent to the captured phone.");}
    catch(Exception error){log.Warning("Power key: "+error.Message);}
   });
   return true;
  }
 }
}
