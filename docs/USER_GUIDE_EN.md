# DX Manager User Guide

## 1. Requirements

### PC

- Windows 7 SP1, 8.1, 10, or 11
- .NET Framework 4.6.2 or later
- Enough disk space to extract the complete release archive

### Phone

- A Samsung Galaxy device that supports Samsung DeX
- Developer options enabled
- USB debugging enabled
- A data-capable USB cable

Approve the RSA debugging prompt shown on the phone during the first
connection. You may select **Always allow from this computer** on a trusted
PC.

## 2. Installation and First Run

1. Extract the complete release ZIP to a folder.
2. Run `DXManager.exe`.
3. If Windows displays a security warning, verify the file source before
   allowing it to run.
4. Connect the phone over USB and approve the RSA debugging prompt.
5. Wait for the device-connected status.
6. Select **Start DeX**.

Do not copy `DXManager.exe` by itself. Keep the complete distribution,
including the `tools` directory, scrcpy DLLs, `scrcpy-server`, and license
files.

## 3. DeX Mode

Select **DeX** in the left navigation.

### Display Settings

- **Resolution**: select a preset or enter a custom size
- **DPI**: controls Android display scaling
- **Bitrate**: controls video quality and network usage
- **Maximum FPS**: select 30 or 60fps

Custom resolution values are stored separately from the presets. Selecting a
preset does not overwrite the last custom width and height.

### Run Options

- **Turn phone screen off (`-S`)**: turns off the physical screen while
  scrcpy is running
- **HID keyboard (`-K`)**: sends the keyboard as an Android input device
- **HID mouse (`-M`)**: sends the mouse as an Android input device
- **Force-stop selected app**: stops the existing app process before launch
- **Reuse existing virtual display**: reuses a compatible DeX display
- **Keep active (`-w`)**: keeps the virtual display active during the session

When settings have changed, selecting the run button cleans up the existing
DeX session when necessary and restarts it with the new values.

## 4. Single-Window Mode

Select **Window 1**, **Window 2**, or **Window 3** in the left navigation.
Each slot stores its own resolution, DPI, bitrate, FPS, app, and run options.

1. Select **Load app list**.
2. Choose the app to launch.
3. Configure the display and run options.
4. Select **Start window**.

Choose **None** at the top of the app list to start without automatically
launching an app.

Apps that were launched successfully are kept in a shared recent-app list.
They can be selected in another slot or after restarting DX Manager without
loading the complete device app list first.

Single-window mode does not reuse the DeX overlay display. Each slot uses
scrcpy's new virtual display feature, so DeX and three app windows can run at
the same time.

- **Flex display (`-x`)**: adjusts the virtual display when the window changes
  size
- Single-window title: `DX Manager - App name`
- DeX window title: `DX Manager - DeX Station`

## 5. USB Connection

USB is the default connection mode.

1. Enable USB debugging on the phone.
2. Connect the phone to the PC with a USB cable.
3. Approve the RSA debugging prompt.
4. Confirm the connected status in DX Manager.

If the device does not appear, check the cable, USB port, Samsung USB driver,
and RSA authorization. Then run
**Settings > Diagnostics > Run environment check**.

## 6. Wireless Connection

### Prepare Wireless ADB over USB

This is the simplest method for the first connection or after a phone reboot.

1. Connect the PC and phone to the same local network.
2. Connect the authorized phone over USB.
3. Open **Settings > Connection** and select **Use wireless ADB**.
4. Leave the phone IP address empty to let DX Manager detect the Wi-Fi
   address.
5. Confirm the connection port. The default is `5555`.
6. Select **Prepare over USB**.
7. After the wireless connection succeeds, disconnect the USB cable.

If automatic address detection fails, find the phone's IPv4 address in its
Wi-Fi details and enter it manually.

### Android 11+ Pairing

1. Enable **Developer options > Wireless debugging** on the phone.
2. Open **Pair device with pairing code**.
3. Enter the phone IP address, pairing port, and six-digit code in DX Manager.
4. Select **Pair**.
5. After pairing, enter the separate **connection port** shown on the main
   Wireless debugging screen.
6. Select **Connect wirelessly**.

The pairing port and connection port may be different. The pairing code is
not stored in settings or written to the log.

### Wireless Troubleshooting

- Devices using the same Wi-Fi name may still be unable to communicate.
- Check guest Wi-Fi, AP/client isolation, VLAN rules, and corporate firewalls.
- USB preparation may be required again after the phone restarts.
- If the first connection fails on 5GHz, check the router configuration or
  initialize the connection on 2.4GHz before retrying.

## 7. Capture

The capture hotkey only works while a scrcpy window is active.

1. Press `F8` to bring the scrcpy window forward and show the capture hint.
2. Press `F8` again to capture only the scrcpy client area.
3. Drag the mouse to capture a selected region.
4. Press `Esc` to cancel.

Captures are saved to the `screenshot` folder by default. Enable
**Send captures to phone** to also transfer them to the configured phone
folder.

## 8. Keyboard Correction

Keyboard correction is only applied while a scrcpy window is active.

- **Korean/English key correction** sends the key as Android Shift+Space.
- **Right Windows key correction** improves Android input compatibility.
- Enter conversion starts in normal Enter mode.
- `Scroll Lock` toggles between normal Enter and Shift+Enter.
- Direct Shift+Space input can optionally be ignored.

The default capture shortcut is `F8`, and the default exit shortcut is
`Left Alt+F8`. To change one, select its field under **Settings > Keyboard**
and press the desired key or key combination.

## 9. Automatic Hiding and System Tray

When automatic hiding is enabled, DX Manager hides the running scrcpy windows
and its own UI after the configured idle period, then remains in the system
tray.

Keyboard or mouse activity does not automatically restore the windows.
Double-click the tray icon or select the open command from its menu to restore
them.

The window close button hides DX Manager to the tray. Use the exit shortcut
or the tray menu to terminate it completely.

## 10. Logs and Diagnostics

### Session Log

The log contains only the current program session. Select **Save log** when a
copy is needed.

The log may include:

- Selected ADB path and version
- Device connection state
- DeX and single-window launch results
- Display ID detection results
- scrcpy output and errors

### Environment Check

Use **Settings > Diagnostics > Run environment check** to verify ADB, scrcpy,
Windows, device connectivity, and important folders.

Before sharing a saved log, verify that it does not contain private network
information or other sensitive data.

## 11. Language, Theme, and Reset

- Display language: automatic, Korean, or English
- App theme: follow Windows, light, or dark
- Theme changes are applied as soon as they are saved.
- Some language and path changes may require a restart to apply everywhere.

Use **Settings > Diagnostics > Restore all defaults** to reset all profiles,
connections, and keyboard options.

Settings are stored in `config/settings.json` next to the application.

## 12. Removal

DX Manager does not use an installer.

1. Exit DX Manager completely.
2. Back up any screenshots or logs you want to keep.
3. Delete the extracted DX Manager folder.

Disable **Start with Windows** before removal if it was enabled.

## Trademarks and Third-Party Components

DX Manager is independently developed and is not affiliated with, sponsored
by, endorsed by, or distributed by Samsung Electronics or Genymobile.

Samsung and Samsung DeX are trademarks of Samsung Electronics Co., Ltd.
scrcpy and bundled dependencies remain under their respective licenses. See
[`THIRD_PARTY_NOTICES.md`](../DexManager/licenses/THIRD_PARTY_NOTICES.md).
