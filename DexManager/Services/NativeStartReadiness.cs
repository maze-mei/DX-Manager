using System;
using System.Text.RegularExpressions;

namespace DexManager.Services
{
    internal enum NativePhoneWakeState { Unknown, Sleeping, Awake }

    // Fresh creation only: wait before creating an Android owner or starting its time budgets.
    internal static class NativeStartReadiness
    {
        internal static NativePhoneWakeState Parse(string output)
        {
            var match=Regex.Match(output??string.Empty,@"^\s*mWakefulness\s*=\s*(\w+)\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase);
            if(match.Success) {
                var value=match.Groups[1].Value;
                if(value.Equals("Awake",StringComparison.OrdinalIgnoreCase)||value.Equals("Dreaming",StringComparison.OrdinalIgnoreCase))return NativePhoneWakeState.Awake;
                if(value.Equals("Asleep",StringComparison.OrdinalIgnoreCase)||value.Equals("Dozing",StringComparison.OrdinalIgnoreCase))return NativePhoneWakeState.Sleeping;
                return NativePhoneWakeState.Unknown;
            }
            match=Regex.Match(output??string.Empty,@"^\s*mInteractive\s*=\s*(true|false)\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase);
            return !match.Success?NativePhoneWakeState.Unknown:match.Groups[1].Value.Equals("true",StringComparison.OrdinalIgnoreCase)?NativePhoneWakeState.Awake:NativePhoneWakeState.Sleeping;
        }

        internal static void Wait(Func<NativePhoneWakeState> read,Func<bool> canceled,Action<int> pause,Action<string> notice)
        {
            bool waited=false;int awakeSamples=0;
            Action check=delegate {if(canceled!=null&&canceled())throw new OperationCanceledException();};
            while(true) {
                check();var state=read();check();
                if(state==NativePhoneWakeState.Unknown) {
                    if(waited)throw new InvalidOperationException("Phone wake state became unavailable while waiting; native DeX was not created.");
                    notice("unknown");return; // Keep older firmware usable; actual DPI must still pass strict verification.
                }
                if(state==NativePhoneWakeState.Awake) {
                    if(!waited||++awakeSamples>=2){if(waited)notice("awake");return;}
                }else {
                    awakeSamples=0;
                    if(!waited){waited=true;notice("waiting");}
                }
                // At most one power query per second; cancellation does not wait the full interval.
                for(int ms=0;ms<1000;ms+=50){check();pause(50);}
            }
        }
    }
}
