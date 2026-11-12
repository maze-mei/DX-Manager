package io.github.mazemei.dxdisplaycleanup;
import org.junit.Test;
import static org.junit.Assert.*;
public class NativeDisplayTruthTest {
 @Test public void savedTokenAndSocketFailureDoNotProveActive(){assertEquals(NativeDisplayTruth.State.UNKNOWN,NativeDisplayTruth.resolve(false,false,false,false,false));}
 @Test public void publicDisplayAbsenceRequiresPriorVisibility(){assertEquals(NativeDisplayTruth.State.UNKNOWN,NativeDisplayTruth.resolve(false,false,false,true,false));}
 @Test public void removedPreviouslyObservedDisplayIsAbsent(){assertEquals(NativeDisplayTruth.State.ABSENT,NativeDisplayTruth.resolve(false,false,false,true,true));}
 @Test public void realDisplayCanSurviveTemporaryControlLoss(){assertEquals(NativeDisplayTruth.State.ACTIVE,NativeDisplayTruth.resolve(false,false,true,true,true));}
 @Test public void authoritativeHelperCanConfirmAnUnenumeratedDisplay(){assertEquals(NativeDisplayTruth.State.ACTIVE,NativeDisplayTruth.resolve(true,false,false,false,false));}
 @Test public void failedDisplayEnumerationDoesNotDeleteOwnership(){assertEquals(NativeDisplayTruth.State.UNKNOWN,NativeDisplayTruth.resolve(false,false,false,false,true));}
 @Test public void knownStoppedHelperConfirmsAbsence(){assertEquals(NativeDisplayTruth.State.ABSENT,NativeDisplayTruth.resolve(false,true,false,false,false));}
 @Test public void stillVisibleOwnedDisplayIsNotAssumedGone(){assertEquals(NativeDisplayTruth.State.ACTIVE,NativeDisplayTruth.resolve(false,true,true,true,true));}
}
