package io.github.mazemei.dxmnativedex;

/** Named executor for Companion or owned basic fallback; no firmware Binder numbers. */
public final class NativeAudioRouter {
    public interface Platform {
        boolean bluetoothAudioConnected() throws Exception;
        int mediaDevice() throws Exception;
        int select(int device,String address) throws Exception;
    }
    private final Platform platform;
    private final java.util.function.BooleanSupplier owned;
    public NativeAudioRouter(Platform platform){this(platform,()->true);}
    public NativeAudioRouter(Platform platform,java.util.function.BooleanSupplier owned){this.platform=platform;this.owned=owned;}
    public static boolean bluetoothType(int type){return type==7||type==8||type==23||type==26||type==27||type==30||type==33||type==34||type==35;}
    public synchronized String select(boolean dex) throws Exception {
        if(!owned.getAsBoolean())return "NOT_OWNED";
        // Recheck immediately before the Binder call, even if Companion already checked Bluetooth.
        if(platform.bluetoothAudioConnected())return "BLUETOOTH";
        int target=dex?32768:2;
        if(platform.mediaDevice()==target)return "UNCHANGED";
        return platform.select(target,dex?"0":"")==0?"CHANGED":"REJECTED";
    }
    public static NativeAudioRouter samsung(java.util.function.BooleanSupplier owned){return new NativeAudioRouter(new Samsung(),owned);}
    public static NativeBluetoothPreserver.Platform samsungBluetooth(){return new Samsung();}
    private static final class Samsung implements Platform, NativeBluetoothPreserver.Platform {
        private Object service;
        private java.lang.reflect.Method mask,select,devices,type;
        private Object manager,listener,attributes;
        private java.lang.reflect.Method routes,unlisten;
        private synchronized void prepare() throws Exception {
            if(service!=null)return;
            Class<?> binder=Class.forName("android.os.IBinder");
            Object raw=Class.forName("android.os.ServiceManager").getMethod("getService",String.class).invoke(null,"audio");
            Object audio=Class.forName("android.media.IAudioService$Stub").getMethod("asInterface",binder).invoke(null,raw);
            if(audio==null)throw new IllegalStateException("Audio service unavailable.");
            Class<?> api=Class.forName("android.media.IAudioService");
            mask=api.getMethod("getDeviceMaskForStream",int.class);
            select=api.getMethod("setDeviceToForceByUser",int.class,String.class,boolean.class);
            devices=Class.forName("android.media.AudioManager").getDeclaredMethod("getDevicesStatic",int.class);
            devices.setAccessible(true);type=Class.forName("android.media.AudioDeviceInfo").getMethod("getType");
            service=audio;
        }
        public boolean bluetoothAudioConnected() throws Exception {
            prepare();Object[] outputs=(Object[])devices.invoke(null,2);
            if(outputs==null||outputs.length==0)throw new IllegalStateException("Output device list unavailable.");
            for(Object output:outputs)if(bluetoothType((Integer)type.invoke(output)))return true;
            return false;
        }
        public int mediaDevice() throws Exception {prepare();return (Integer)mask.invoke(service,3);}
        public int select(int device,String address) throws Exception {prepare();return (Integer)select.invoke(service,device,address,false);}
        private void prepareRoutes() throws Exception {
            prepare(); if(attributes!=null)return;
            routes=Class.forName("android.media.IAudioService").getMethod("getDevicesForAttributes",android.media.AudioAttributes.class);
            attributes=new android.media.AudioAttributes.Builder().setUsage(android.media.AudioAttributes.USAGE_MEDIA)
                .setContentType(android.media.AudioAttributes.CONTENT_TYPE_MUSIC).build();
        }
        public NativeBluetoothPreserver.Route[] routes() throws Exception {
            prepareRoutes(); java.util.List<?> values=(java.util.List<?>)routes.invoke(service,attributes);
            if(values==null)throw new IllegalStateException("Media route list unavailable.");
            Class<?> api=Class.forName("android.media.AudioDeviceAttributes");
            java.lang.reflect.Method t=api.getMethod("getType"),i=api.getMethod("getInternalType"),a=api.getMethod("getAddress");
            NativeBluetoothPreserver.Route[] result=new NativeBluetoothPreserver.Route[values.size()];
            for(int n=0;n<result.length;n++){Object value=values.get(n);result[n]=new NativeBluetoothPreserver.Route((Integer)t.invoke(value),(Integer)i.invoke(value),(String)a.invoke(value));}
            return result;
        }
        public NativeBluetoothPreserver.Route[] connected() throws Exception {
            prepare(); Object[] values=(Object[])devices.invoke(null,2);
            if(values==null)throw new IllegalStateException("Output list unavailable.");
            Class<?> api=Class.forName("android.media.AudioDeviceInfo");
            java.lang.reflect.Method internal=api.getMethod("getInternalType"),address=api.getMethod("getAddress");
            NativeBluetoothPreserver.Route[] result=new NativeBluetoothPreserver.Route[values.length];
            for(int n=0;n<result.length;n++)result[n]=new NativeBluetoothPreserver.Route((Integer)type.invoke(values[n]),(Integer)internal.invoke(values[n]),(String)address.invoke(values[n]));
            return result;
        }
        public void listen(Runnable changed) throws Exception {
            prepareRoutes(); Class<?> api=Class.forName("android.media.AudioManager");
            manager=api.getDeclaredConstructor().newInstance();
            Class<?> callback=Class.forName("android.media.AudioManager$OnDevicesForAttributesChangedListener");
            listener=java.lang.reflect.Proxy.newProxyInstance(callback.getClassLoader(),new Class<?>[]{callback},(proxy,method,args)->{
                if(method.getName().equals("hashCode"))return System.identityHashCode(proxy);
                if(method.getName().equals("equals"))return proxy==args[0];
                if(method.getName().equals("toString"))return "DXM startup Bluetooth listener";
                if(method.getName().equals("onDevicesForAttributesChanged"))changed.run();
                return null;
            });
            unlisten=api.getMethod("removeOnDevicesForAttributesChangedListener",callback);
            api.getMethod("addOnDevicesForAttributesChangedListener",android.media.AudioAttributes.class,java.util.concurrent.Executor.class,callback)
                .invoke(manager,attributes,(java.util.concurrent.Executor)Runnable::run,listener);
        }
        public void unlisten() throws Exception {
            if(manager!=null&&listener!=null&&unlisten!=null)try{unlisten.invoke(manager,listener);}finally{listener=null;manager=null;}
        }
    }
}
