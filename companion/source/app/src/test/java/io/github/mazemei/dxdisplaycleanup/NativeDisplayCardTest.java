package io.github.mazemei.dxdisplaycleanup;
import org.junit.Test;
import static org.junit.Assert.*;

public final class NativeDisplayCardTest {
 @Test public void mereBindingOrWindowsConnectionDoesNotShowADeXCard(){
  assertEquals(NativeDisplayCard.Mode.HIDDEN,NativeDisplayCard.mode(false,null));
  assertEquals(NativeDisplayCard.Mode.HIDDEN,NativeDisplayCard.mode(false,true));
 }
 @Test public void actualViewerIsShownAsInUse(){assertEquals(NativeDisplayCard.Mode.IN_USE,NativeDisplayCard.mode(true,true));}
 @Test public void retainedDisplayWithoutViewerIsShownAsRetained(){assertEquals(NativeDisplayCard.Mode.RETAINED,NativeDisplayCard.mode(true,false));}
 @Test public void confirmedOlderOwnerCanShowGenericRunningWithoutGuessingViewer(){assertEquals(NativeDisplayCard.Mode.RUNNING,NativeDisplayCard.mode(true,null));}
 @Test public void confirmedRemovalHidesPreviouslyRunningCard(){
  assertEquals(NativeDisplayCard.Mode.IN_USE,NativeDisplayCard.mode(true,true));
  assertEquals(NativeDisplayCard.Mode.HIDDEN,NativeDisplayCard.mode(false,null));
 }
}
