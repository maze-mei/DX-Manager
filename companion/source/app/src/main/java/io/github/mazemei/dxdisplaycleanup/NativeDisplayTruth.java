package io.github.mazemei.dxdisplaycleanup;

/** A saved recovery token is not evidence of a running Android display. */
final class NativeDisplayTruth {
    enum State { ACTIVE, ABSENT, UNKNOWN }
    static State resolve(boolean authenticatedActive,boolean authenticatedStopped,
            boolean displayMatched,boolean scanAvailable,boolean previouslyVisible) {
        if(displayMatched||authenticatedActive)return State.ACTIVE;
        if(authenticatedStopped||(scanAvailable&&previouslyVisible))return State.ABSENT;
        return State.UNKNOWN;
    }
}
