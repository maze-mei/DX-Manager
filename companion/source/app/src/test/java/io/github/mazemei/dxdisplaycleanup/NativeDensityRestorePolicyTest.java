package io.github.mazemei.dxdisplaycleanup;
import org.junit.Test;import static org.junit.Assert.*;
public class NativeDensityRestorePolicyTest {
 @Test public void neverRestoreWhileOwnedDisplayIsVisible(){assertFalse(NativeDensityRestorePolicy.mayRestore(true,false,true,true,false,true,90000));}
 @Test public void foreignExternalDisplayDefersRestoration(){assertFalse(NativeDensityRestorePolicy.mayRestore(false,true,true,true,false,false,90000));}
 @Test public void observedDisplayRemovalAllowsRestoration(){assertTrue(NativeDensityRestorePolicy.mayRestore(false,false,true,false,false,true,1000));}
 @Test public void explicitCompletionAllowsPrecreationFailureRecovery(){assertTrue(NativeDensityRestorePolicy.mayRestore(false,false,false,true,false,false,1000));}
 @Test public void pendingCreationIsNotRestoredPrematurely(){assertFalse(NativeDensityRestorePolicy.mayRestore(false,false,false,false,false,false,59000));}
 @Test public void neverStartedOwnerTimesOut(){assertTrue(NativeDensityRestorePolicy.mayRestore(false,false,false,false,false,false,60001));}
 @Test public void invisibleConfirmedDisplayIsNotAssumedAbsent(){assertFalse(NativeDensityRestorePolicy.mayRestore(false,false,false,false,false,true,90000));}
 @Test public void rebootCanRecoverPersistedPreference(){assertTrue(NativeDensityRestorePolicy.mayRestore(false,false,false,false,true,false,100));}
}
