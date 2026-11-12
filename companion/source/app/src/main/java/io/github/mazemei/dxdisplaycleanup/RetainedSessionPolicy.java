package io.github.mazemei.dxdisplaycleanup;

/** Pure policy. Companion owns decisions; the shell-UID native owner executes audio requests. */
final class RetainedSessionPolicy {
    static final class State {
        final long revision;
        final boolean connected,viewer,seen,power,enabled;
        final int delay;
        State(long revision,boolean connected,boolean viewer,boolean seen,boolean power,boolean enabled,int delay){
            this.revision=revision;this.connected=connected;this.viewer=viewer;this.seen=seen;this.power=power;this.enabled=enabled;this.delay=delay;
        }
        boolean applicable(){return enabled&&seen&&delay!=0;}
        boolean dex(){return connected&&viewer;}
    }
    interface Access {
        State read() throws Exception;
        boolean bluetoothConnected() throws Exception;
        String awake(boolean enabled) throws Exception;
        String route(boolean dex,long revision) throws Exception;
        void report(long revision,String result) throws Exception;
        default String stopBrowser(long revision) throws Exception {return "DISABLED";}
    }
    static State parse(String frame) {
        String[] parts=frame.split("\\|",-1);
        if(parts.length!=9||!parts[0].equals("SESSION")||!parts[1].equals("4"))throw new IllegalArgumentException("Unsupported native session state.");
        long revision=Long.parseLong(parts[2]);if(revision<0)throw new IllegalArgumentException("Invalid native revision.");
        for(int i=3;i<=7;i++)if(!parts[i].equals("0")&&!parts[i].equals("1"))throw new IllegalArgumentException("Invalid native flag.");
        int delay=Integer.parseInt(parts[8]);
        if(delay!=-1&&delay!=0&&delay!=30&&delay!=60&&delay!=300&&delay!=600&&delay!=1800)throw new IllegalArgumentException("Invalid native delay.");
        return new State(revision,parts[3].equals("1"),parts[4].equals("1"),parts[5].equals("1"),parts[6].equals("1"),parts[7].equals("1"),delay);
    }
    static void apply(Access access) throws Exception {
        // A late old clear cannot be the final result after a new viewer has already started.
        // Re-read and reconcile once; any further transition has its own coalesced notification.
        for(int pass=0;pass<2;pass++) {
            State state=access.read();if(!state.applicable())return;
            String power="KEEP",audio="KEEP",browser="KEEP";
            if(state.connected&&state.power)power=access.awake(true);
            else if(!state.dex())power=access.awake(false);
            try {
                if(access.bluetoothConnected())audio="BLUETOOTH";
                else audio=access.route(state.dex(),state.revision);
            }catch(Exception unknown){audio="SKIP_UNKNOWN";}
            if(!state.connected)try{browser=access.stopBrowser(state.revision);}catch(Exception failure){browser="ERROR";}
            try{access.report(state.revision,"connected="+(state.connected?1:0)+" viewer="+(state.viewer?1:0)+" power="+power+" audio="+audio+" browser="+browser);}catch(Exception ignored){ }
            State after=access.read();if(after.revision==state.revision||!after.applicable())return;
        }
    }
    static boolean bluetoothType(int type){return type==7||type==8||type==23||type==26||type==27||type==30||type==33||type==34||type==35;}
}
