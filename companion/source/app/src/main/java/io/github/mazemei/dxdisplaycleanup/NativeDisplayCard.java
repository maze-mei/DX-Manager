package io.github.mazemei.dxdisplaycleanup;

/** Connection to Windows alone does not prove that a native display exists. */
final class NativeDisplayCard {
    enum Mode { HIDDEN, RUNNING, IN_USE, RETAINED }
    static Mode mode(boolean confirmed,Boolean inUse) {
        if(!confirmed)return Mode.HIDDEN;
        return inUse==null?Mode.RUNNING:(inUse?Mode.IN_USE:Mode.RETAINED);
    }
}
