package io.github.mazemei.dxdisplaycleanup;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;

/** Shell-only entry point, protected by android.permission.DUMP in the manifest. */
public final class NativeSessionReceiver extends BroadcastReceiver {
    @Override public void onReceive(Context context,Intent intent) {
        String token=intent.getStringExtra("native_token");
        if(token==null || !token.matches("[0-9a-f]{32}")){setResultCode(1);return;}
        if((context.getPackageName()+".PREPARE_NATIVE_DENSITY").equals(intent.getAction())) {
            NativeSessionBridge.prepareDensity(context,token,intent.getIntExtra("density",0),goAsync());return;
        }
        if((context.getPackageName()+".NATIVE_SESSION_STATE").equals(intent.getAction())) {
            PendingResult pending=goAsync();
            NativeSessionBridge.onStateEvent(context,token,pending::finish);return;
        }
        if((context.getPackageName()+".NATIVE_SESSION_COMPLETE").equals(intent.getAction()) || (context.getPackageName()+".CLEAR_NATIVE_SESSION").equals(intent.getAction())) {
            NativeSessionBridge.complete(context,token,intent.getBooleanExtra("expired",false));
            if(intent.getBooleanExtra("expired",false)&&!SessionGuardianService.isPcConnected())
                SessionGuardianService.stopAfterManualCleanupIfIdle(context,CleanupCoordinator.inspect(context));
            CleanupWidgetProvider.updateAll(context);CleanupTileService.requestRefresh(context);setResultCode(0);return;
        }
        if(!(context.getPackageName()+".CONFIGURE_NATIVE_SESSION").equals(intent.getAction())){setResultCode(1);return;}
        NativeSessionBridge.configure(context,token,intent.getBooleanExtra("restore",false),("missing".equals(intent.getStringExtra("original"))?"":intent.getStringExtra("original")),intent.getBooleanExtra("retained_policy",false),intent.getBooleanExtra("browser_policy",false));
        NativeSessionBridge.updatePolicyAsync(context);
        setResultCode(0);setResultData("DXNP2|"+NativeSessionRules.DISCONNECT_GRACE_SECONDS);
    }
}
