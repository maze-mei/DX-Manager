package io.github.mazemei.dxmnativedex;

import com.scrcpydex.server.DisplayWatch;
import java.io.ByteArrayOutputStream;
import java.nio.charset.StandardCharsets;
import java.util.*;
import java.util.concurrent.TimeUnit;
import java.util.function.*;
import java.util.regex.*;

/** Creation-only owned overrides. Never restore defaults on a live display before removal. */
public final class NativeMetrics {
    private static long now(){return System.nanoTime()/1000000L;}
    private static String wm(long deadline,Consumer<String> log,String... args) throws Exception {
        long remaining=deadline-now();if(remaining<=0)throw new IllegalStateException("Native metrics deadline elapsed.");
        List<String> command=new ArrayList<>();command.add("/system/bin/wm");command.addAll(Arrays.asList(args));
        String action=String.join(" ",command);log.accept("Metrics command "+action);
        long started=now();Process p=new ProcessBuilder(command).redirectErrorStream(true).start();
        if(!p.waitFor(Math.min(1200,remaining),TimeUnit.MILLISECONDS)){p.destroy();log.accept("Metrics warning timeout "+action);throw new IllegalStateException("wm timed out.");}
        ByteArrayOutputStream out=new ByteArrayOutputStream();byte[] buffer=new byte[1024];int n;
        while((n=p.getInputStream().read(buffer))>0){if(out.size()+n>16384)throw new IllegalStateException("wm output limit.");out.write(buffer,0,n);}
        String text=out.toString(StandardCharsets.UTF_8.name());
        log.accept("Metrics response exit="+p.exitValue()+" elapsedMs="+(now()-started)+" action="+action+" output="+text.replace('\r',' ').replace('\n','/').trim());
        if(p.exitValue()!=0)throw new IllegalStateException("wm rejected display override: "+text.trim());
        return text;
    }
    private static String extract(String text,String regex,String fallback){Matcher m=Pattern.compile(regex).matcher(text);return m.find()?m.group(1):fallback;}
    public static int effectiveDensity(String output){return Integer.parseInt(extract(output,"Override density:\\s*([0-9]+)",extract(output,"Physical density:\\s*([0-9]+)","-1")));}
    public static NativeMetricsVerifier.Metrics verifyInitial(Properties record,DisplayWatch watch,int width,int height,int dpi,
            BooleanSupplier canceled,Consumer<String> log) throws Exception {
        final int id=watch.getDetectedDisplayId();final String display=Integer.toString(id);final long deadline=now()+NativeMetricsVerifier.BUDGET_MS;
        BooleanSupplier owned=()->id>0&&!canceled.getAsBoolean()&&watch.isDisplayPresent()&&watch.getDetectedDisplayId()==id;
        if(!owned.getAsBoolean())throw new IllegalStateException("Owned native display is unavailable.");
        final int initialDpi=NativeInitialMode.initialDpi(dpi);
        record.setProperty("display",display);record.setProperty("initial.metrics","true");record.setProperty("initial.dpi",Integer.toString(initialDpi));NativeRecovery.save(record);
        wm(deadline,log,"size","-d",display); // Read-only physical-mode evidence, distinct from the desktop logical size.
        return NativeMetricsVerifier.initialize(new NativeMetricsVerifier.Platform(){
            public long now(){return NativeMetrics.now();}
            public boolean owned(){return owned.getAsBoolean();}
            public void pause(long ms)throws Exception{Thread.sleep(ms);}
            public void size(int w,int h){throw new UnsupportedOperationException("Initial verification never resizes.");}
            public void density(int d)throws Exception{
                if(dpi>=initialDpi||d!=dpi)throw new UnsupportedOperationException("Only the requested lower DPI may follow initial verification.");
                if(!owned())throw new IllegalStateException("Native ownership lost before final DPI application.");
                wm(deadline,log,"density",Integer.toString(d),"-d",display);
            }
            public NativeMetricsVerifier.Metrics read()throws Exception{
                String result=wm(deadline,log,"density","-d",display);
                if(!owned())throw new IllegalStateException("Native ownership lost during initial verification.");
                return new NativeMetricsVerifier.Metrics(id,watch.getWidth(),watch.getHeight(),watch.getDpi(),effectiveDensity(result));
            }
            public void note(String kind,String text){log.accept("Metrics "+kind+" "+text);}
        },id,width,height,dpi);
    }
    public static NativeMetricsVerifier.Metrics apply(Properties record,DisplayWatch watch,int width,int height,int dpi,
            BooleanSupplier canceled,Consumer<String> log) throws Exception {
        final int id=watch.getDetectedDisplayId();final String display=Integer.toString(id);final long deadline=now()+NativeMetricsVerifier.BUDGET_MS;
        BooleanSupplier owned=()->id>0&&!canceled.getAsBoolean()&&watch.isDisplayPresent()&&watch.getDetectedDisplayId()==id;
        if(!owned.getAsBoolean())throw new IllegalStateException("Owned native display is unavailable.");
        record.setProperty("display",display);
        record.setProperty("sizeBefore",extract(wm(deadline,log,"size","-d",display),"Override size:\\s*([0-9]+x[0-9]+)","reset"));
        record.setProperty("densityBefore",extract(wm(deadline,log,"density","-d",display),"Override density:\\s*([0-9]+)","reset"));
        NativeRecovery.save(record);
        return NativeMetricsVerifier.apply(new NativeMetricsVerifier.Platform(){
            public long now(){return NativeMetrics.now();}
            public boolean owned(){return owned.getAsBoolean();}
            public void pause(long ms) throws Exception {Thread.sleep(ms);}
            public void size(int w,int h) throws Exception {if(!owned())throw new IllegalStateException("Native ownership lost.");wm(deadline,log,"size",w+"x"+h,"-d",display);}
            public void density(int d) throws Exception {if(!owned())throw new IllegalStateException("Native ownership lost.");wm(deadline,log,"density",Integer.toString(d),"-d",display);}
            public NativeMetricsVerifier.Metrics read() throws Exception {
                String result=wm(deadline,log,"density","-d",display);
                int effective=effectiveDensity(result);
                if(!owned())throw new IllegalStateException("Native ownership lost during readback.");
                return new NativeMetricsVerifier.Metrics(id,watch.getWidth(),watch.getHeight(),watch.getDpi(),effective);
            }
            public void note(String kind,String text){log.accept("Metrics "+kind+" "+text);}
        },id,width,height,dpi);
    }
}
