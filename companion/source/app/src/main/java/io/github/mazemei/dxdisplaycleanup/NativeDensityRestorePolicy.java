package io.github.mazemei.dxdisplaycleanup;
final class NativeDensityRestorePolicy {
    static boolean mayRestore(boolean ownedVisible,boolean foreignVisible,boolean observed,
            boolean finishRequested,boolean bootChanged,boolean helperConfirmed,long ageMs){
        if(ownedVisible||foreignVisible)return false;
        return observed||finishRequested||bootChanged||(!helperConfirmed&&ageMs>=60000);
    }
}
