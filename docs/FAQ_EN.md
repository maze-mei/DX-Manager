# DX Manager Frequently Asked Questions

[한국어 Q&A](FAQ_KO.md) · [English user guide](USER_GUIDE_EN.md)

## Q1. A small screen (secondary display) remains on the phone.

Android's virtual-display setting may remain if DX Manager is terminated
forcibly, or if the USB or wireless connection is completely lost before the
cleanup command can be sent. DX Manager removes this setting automatically
when it exits normally.

If the screen remains, remove it on the phone as follows:

1. Open **Developer options**.
2. Select **Simulate secondary displays**.
3. Select any resolution.
4. Open the menu again and turn off the selected item, or select **None**.

On some One UI versions, you may need to select the same resolution again to
turn it off. The remaining virtual screen disappears when the setting is
disabled.

## Q2. Why use Force-stop selected app when launching Samsung Internet automatically?

On some systems, Korean/English input switching may stop working if Samsung
Internet is already running on the phone and is reopened on the DeX display.
When **Force-stop selected app** is enabled, DX Manager first stops the
existing Samsung Internet process on the phone and then launches it again on
the DeX display, avoiding this issue.

This does not delete browser data or bookmarks, but unsaved text and work in
progress may be lost. Enable it for other apps only when needed.

## Q3. Which device is used when two or more phones are connected?

DX Manager v1 manages one phone at a time. The first phone selected after the
program starts is pinned as the target for that run.

- Phones connected later are ignored.
- If the pinned phone is disconnected, DX Manager does not switch to another
  phone automatically.
- Reconnecting the original phone identifies it as the same device.
- Switching the same phone between USB and wireless ADB is allowed.

To use a different phone, exit DX Manager completely, leave only the desired
phone connected, and start DX Manager again. Device selection and simultaneous
control of multiple phones are candidates for a future v2 release.

## Q4. Parts of the DeX interface are clipped below 1600×900.

Samsung DeX has a standard minimum resolution of 1600×900. DX Manager can
create smaller virtual resolutions, but some DeX elements may not adapt to
the smaller screen. For example, the top of the app drawer may be clipped.

Apps themselves may still work, but 1600×900 or higher is recommended for the
complete DeX interface. This is a Samsung DeX limitation at low resolutions;
DX Manager is not cropping the image.

## Q5. The desktop icons and wallpaper changed after adjusting resolution or DPI.

Samsung DeX may choose different scaling and layout profiles for different
resolution and DPI combinations. Even at the same resolution, changing DPI
can alter icon size, widget placement, and whether desktop items can be
edited. The wallpaper may also be stored separately for each profile.

Therefore, a different desktop after changing settings does not necessarily
mean that DX Manager selected the wrong virtual display. For the most stable
layout, choose the resolution and DPI combination you normally use and arrange
the desktop under that combination.

## Q6. The Recents screen looks wrong and I cannot return to Desktop 1.

With some resolution and DPI combinations, the desktop selector in Recents
may be misaligned after DeX starts for the first time. To recover:

1. On the Recents screen, select the **+** button inside the monitor image to
   create a new desktop.
2. Select **Desktop 1**, which may be only partly visible at the left edge.

Once the normal screen returns, the problem generally does not recur during
the same DeX session. The layout and names may differ by Samsung firmware and
One UI version.

## Q7. Why can DPI not be set below 120?

On the tested Samsung DeX environment, 120 is the lowest DPI at which a
virtual display can be created. At 119 or below, the overlay is not created
and no new display ID can be found.

DX Manager changes values below 120 back to 120 and displays a notice. Very
high DPI values may create a display successfully, but text and interface
elements can become too large for practical use.

## Q8. The scrcpy-server transfer speed in the log is lower than usual.

A line such as the following reports the momentary rate calculated while
scrcpy sends the small `scrcpy-server` file to the phone:

```text
scrcpy-server: 1 file pushed, 0 skipped. 42.9 MB/s
```

Because the file is small and the transfer completes very quickly, the value
can vary significantly. It is not an exact measurement of the USB link or the
subsequent video-streaming speed. A low number by itself does not indicate a
connection failure.

If the DeX display also stutters, or direct scrcpy runs consistently show low
values, try another data-capable USB cable and another USB port on the PC. A
charging-oriented or poor-quality cable may be the cause.

## Q9. Wireless ADB connection or automatic reconnection fails.

Check the following in order:

- Confirm that the PC and phone are on a local network where they can
  communicate directly.
- Check guest Wi-Fi, AP/client isolation, VLAN rules, and corporate firewalls.
- Make sure the pairing port is not being confused with the connection port.
- Check whether the phone's IP address has changed.
- After restarting the phone, prepare wireless ADB over USB again.
- Pair Android 11+ wireless debugging again when necessary.

If the wireless address appears in `adb devices` as `offline`, disconnect and
reconnect it. Using the same Wi-Fi name does not by itself guarantee that
devices can communicate with each other.

## Q10. The device status is unauthorized or offline.

`unauthorized` means that the phone has not approved the PC's ADB key. Turn on
the phone screen and approve the RSA debugging prompt. If the prompt does not
appear, revoke USB debugging authorizations and reconnect the cable, or turn
USB debugging off and on again.

`offline` means that the device is listed but cannot accept ADB commands.
Check the cable, USB port, or wireless network and reconnect. If a USB device
continues to be missing, also check the Samsung USB driver and the phone's
**Auto Blocker** setting.

## Q11. Why is right Shift corrected to left Shift?

In the SDL3 environment used by scrcpy 4.0 for Windows, the physical right
Shift key may be detected correctly by Windows but not delivered to Android.
The same behavior was not observed with scrcpy 3.3.4/SDL2.

DX Manager converts right Shift to left Shift only while an SDL3-based scrcpy
window is active. Normal Shift input remains available, but Android apps
cannot distinguish the two Shift sides during that session. The correction is
not applied to other Windows applications or SDL2-based scrcpy versions.

## Q12. What is the difference between device start delay and process timeout?

**Device start delay** is an intentional pause after DX Manager confirms the
phone's connection state and device name, but before it sends the actual DeX
or single-window start command. Its range is 0–60 seconds and the default is
1 second. A value of 0 starts immediately after connection confirmation.

**Process timeout** is the maximum time DX Manager allows an ADB or scrcpy
helper process to respond or create its window. A command that finishes
normally does not wait for the full timeout. The advanced default normally
does not need to be changed unless timeouts repeatedly occur on a slow PC or
device.

## Q13. What happens if USB is unexpectedly disconnected during use?

After confirming the disconnection, DX Manager cleans up the DeX and
single-window sessions that were running on that phone. If the phone is no
longer reachable, overlay, screen-state, and stay-awake restoration commands
that cannot be sent immediately may be retried when the same phone reconnects.

When the original phone reconnects, DX Manager applies the configured start
delay and restarts the session if automatic start is enabled. In v1, it does
not switch to another connected phone.

## Q14. What is the difference between Turn phone screen off and Stay awake?

**Turn phone screen off (`-S`)** turns off the phone's physical panel while a
scrcpy session is running. The DeX virtual display and scrcpy video continue
to operate while the phone screen is off.

**Stay awake** keeps Android active so that it does not sleep or interrupt the
connection during a session. If any DeX or single-window session requests
screen-off or stay-awake behavior, DX Manager manages it using the combined
state of all sessions. When all related sessions stop, or DX Manager exits
normally, it attempts to restore the phone screen and stay-awake state.

## Q15. What is required on Windows 7, and why can DXManager.exe not be copied by itself?

DX Manager supports 64-bit Windows 7 SP1 with .NET Framework 4.6.2 or later.
32-bit Windows is not supported. On Windows 7/8.1, the Universal CRT update
required by the bundled legacy ADB and the Samsung USB driver may also be
needed.

`DXManager.exe` depends on the following adjacent files and will not work
correctly when copied by itself:

- ADB and scrcpy executables under `tools`
- scrcpy DLLs and `scrcpy-server`
- Korean resources and configuration files
- License and third-party notice files

Keep the folder structure from the release ZIP and run the program from a
writable location where it can save settings, logs, and screenshots.
