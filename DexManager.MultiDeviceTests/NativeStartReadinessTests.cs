using System;
using System.Collections.Generic;
using DexManager.Services;

internal static class NativeStartReadinessTests
{
    internal static void Run(Action<bool,string> check)
    {
        Func<string,NativePhoneWakeState> parse=NativeStartReadiness.Parse;
        check(parse("POWER MANAGER (dumpsys power)\r\n  mWakefulness=Asleep\r\n  mInteractive=false")==NativePhoneWakeState.Sleeping,"Realistic CRLF power dump detects sleeping");
        check(parse("  mWakefulness=Awake\n  mInteractive=true")==NativePhoneWakeState.Awake,"Awake power state accepted");
        check(parse("mWakefulness=Dozing")==NativePhoneWakeState.Sleeping,"Always-on dozing still waits for wake");
        check(parse("mWakefulness=Dreaming")==NativePhoneWakeState.Awake,"Interactive dreaming does not deadlock startup");
        check(parse("  mInteractive=false")==NativePhoneWakeState.Sleeping,"Older explicit interactive flag fallback");
        check(parse("  mInteractive=true")==NativePhoneWakeState.Awake,"Interactive fallback accepts wake");
        check(parse("   mWakefulnessChanging=true")==NativePhoneWakeState.Unknown,"Similar field names cannot pretend to be wakefulness");
        check(parse("mWakefulness=FutureMode")==NativePhoneWakeState.Unknown,"Unknown Android state is not guessed");
        check(parse("mWakefulness=Asleep\nmInteractive=true")==NativePhoneWakeState.Sleeping,"Explicit global asleep state wins over conflicting fallback");
        check(parse(null)==NativePhoneWakeState.Unknown,"Missing output is not treated as confirmed wake");
        var notices=new List<string>();int reads=0,pauses=0;
        NativeStartReadiness.Wait(()=>{reads++;return NativePhoneWakeState.Awake;},()=>false,ms=>pauses++,notices.Add);
        check(reads==1&&pauses==0&&notices.Count==0,"Already-awake startup has no added delay or wake message");
        int elapsed=0;reads=0;notices.Clear();
        NativeStartReadiness.Wait(()=>{reads++;return elapsed<61000?NativePhoneWakeState.Sleeping:NativePhoneWakeState.Awake;},()=>false,ms=>elapsed+=ms,notices.Add);
        check(elapsed==62000&&reads==63,"Manual sleep wait exceeds old 40/45-second startup budget without creating an owner");
        check(notices.Count==2&&notices[0]=="waiting"&&notices[1]=="awake","Only sleep/wake transitions are announced");
        int polls=0;elapsed=0;notices.Clear();
        NativeStartReadiness.Wait(()=>{polls++;return polls==1||polls==3?NativePhoneWakeState.Sleeping:NativePhoneWakeState.Awake;},()=>false,ms=>elapsed+=ms,notices.Add);
        check(polls==5&&elapsed==4000,"Wake must stay awake for two samples after sleeping again");
        bool canceled=false;reads=0;elapsed=0;notices.Clear();bool rejected=false;
        try{NativeStartReadiness.Wait(()=>{reads++;return NativePhoneWakeState.Sleeping;},()=>canceled,ms=>{elapsed+=ms;canceled=true;},notices.Add);}catch(OperationCanceledException){rejected=true;}
        check(rejected&&reads==1&&elapsed==50,"Shutdown/stop cancels within one short pause, without another ADB query");
        rejected=false;reads=0;
        try{NativeStartReadiness.Wait(()=>{reads++;return NativePhoneWakeState.Awake;},()=>true,ms=>{},notices.Add);}catch(OperationCanceledException){rejected=true;}
        check(rejected&&reads==0,"Canceled startup never launches a power query");
        rejected=false;canceled=false;reads=0;
        try{NativeStartReadiness.Wait(()=>{reads++;canceled=true;return NativePhoneWakeState.Awake;},()=>canceled,ms=>{},notices.Add);}catch(OperationCanceledException){rejected=true;}
        check(rejected&&reads==1,"Cancellation during a read cannot proceed into creation");
        notices.Clear();NativeStartReadiness.Wait(()=>NativePhoneWakeState.Unknown,()=>false,ms=>{},notices.Add);
        check(notices.Count==1&&notices[0]=="unknown","Unsupported initial power output delegates to strict existing DPI verification");
        reads=0;rejected=false;
        try{NativeStartReadiness.Wait(()=>++reads==1?NativePhoneWakeState.Sleeping:NativePhoneWakeState.Unknown,()=>false,ms=>{},s=>{});}catch(InvalidOperationException){rejected=true;}
        check(rejected,"Losing readable power state after confirmed sleep cannot silently start");
        rejected=false;
        try{NativeStartReadiness.Wait(()=>{throw new System.IO.IOException("disconnected");},()=>false,ms=>{},s=>{});}catch(System.IO.IOException){rejected=true;}
        check(rejected,"Disconnected or failed power query aborts before native creation");
    }
}
