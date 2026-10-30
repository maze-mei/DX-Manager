# DX Manager v2.5.0 - Third-Party Notices

DX Manager is an independently developed utility. The third-party binaries
listed below are distributed without modification and remain under their
respective licenses. DX Manager's MIT License does not replace or alter those
licenses.

## Experimental loopback DeX helper

`tools/loopback/dxm-loopback.jar` is a modified Android helper based on the
Samsung loopback activation and RTSP negotiation method in ScrcpyDeX.
Copyright 2026 Aureliano Peixoto and ScrcpyDeX Contributors.
DX Manager hidden-preview integration and session-lifecycle modifications:
Copyright 2026 asyouplz (https://github.com/asyouplz).
It is distributed under Apache License 2.0, separately from DX Manager's MIT
license. The helper directory includes `LICENSE`, `NOTICE`, and its checksum;
source and build instructions are in `DXLoopback/` in this repository.

Reference project: https://github.com/Aureliano021/ScrcpyDex

## scrcpy

DX Manager includes and uses scrcpy 5.0.1 to display and control Android
devices.

Copyright (C) 2018 Genymobile  
Copyright (C) 2018-2026 Romain Vimont

scrcpy is licensed under the Apache License, Version 2.0. The complete
license text is provided in `scrcpy-LICENSE.txt` in this directory.

Official project: https://github.com/Genymobile/scrcpy

Bundled version: 5.0.1

## Android Debug Bridge (ADB)

DX Manager includes Android Debug Bridge binaries from the Android Open
Source Project. The scrcpy runtime contains platform-tools
37.0.1-15733141, and the Windows 7/8.1 fallback contains platform-tools
34.0.1-9979309.

ADB source is distributed under the Apache License, Version 2.0. The complete
Apache 2.0 license text is provided in `scrcpy-LICENSE.txt` in this directory.

Official source: https://android.googlesource.com/platform/packages/modules/adb/

## SDL

The Windows scrcpy client includes SDL 3.4.18, distributed under the zlib
license.

Copyright (C) 1997-2026 Sam Lantinga

Official project and license:
https://github.com/libsdl-org/SDL/blob/release-3.4.18/LICENSE.txt

The complete notice is provided in `SDL3-LICENSE.txt` in this directory.

## FFmpeg libraries

The Windows scrcpy client dynamically loads FFmpeg libraries including
libavcodec 63.1.102, libavformat 63.1.102, libavutil 61.1.102, and
libswresample. The bundled build reports `LGPL version 2.1 or later` and does
not report GPL components.

Official project: https://ffmpeg.org/

License and source information: https://ffmpeg.org/legal.html

The complete LGPL 2.1 text is provided in `LGPL-2.1-LICENSE.txt` in this
directory. The FFmpeg build also uses zlib; its license is provided in
`zlib-LICENSE.txt`. Its AV1 decoder support uses dav1d, distributed under the
BSD 2-Clause license; that notice is provided in `dav1d-LICENSE.txt`.

Official dav1d project and source:
https://code.videolan.org/videolan/dav1d/

## libusb

The Windows scrcpy client includes libusb 1.0.30. libusb is distributed under
the GNU Lesser General Public License, version 2.1 or later.

Official project and source: https://github.com/libusb/libusb/tree/v1.0.30

## MinGW-w64 runtime

The Windows 7/8.1 fallback ADB includes `libwinpthread-1.dll` from the
MinGW-w64 runtime. Its MIT/BSD-style notices are provided in
`MinGW-w64-winpthreads-LICENSE.txt` in this directory.

Official project and source: https://www.mingw-w64.org/source/

## DX Companion / Pretendard

The bundled DX Companion includes the unmodified Pretendard typeface under the
SIL Open Font License 1.1. Its complete copyright and license text is distributed
as `tools\companion\Pretendard-LICENSE.txt`.

## Samsung DeX trademark notice

DX Manager works with features provided by Samsung DeX on compatible
Samsung devices. DX Manager is not affiliated with, sponsored by, endorsed
by, or distributed by Samsung Electronics.

Samsung and Samsung DeX are trademarks of Samsung Electronics Co., Ltd.
All trademarks are the property of their respective owners.

## Source availability

DX Manager does not modify scrcpy, ADB, SDL, FFmpeg, libusb, or MinGW-w64.
Corresponding source code and build information are available from the
official project links above. Recipients may replace the dynamically loaded
compatible libraries in the portable distribution, subject to the relevant
license terms.

## Embedded portable UI font

The Windows UI embeds static weight-400/600 instances derived from the Store Pretendard Variable font. Glyph outlines and metrics are retained. The derivatives use DX Store UI family names because Pretendard is an OFL reserved font name. See Pretendard-LICENSE.txt. No system font installation is performed.

## Fluent UI System Icons

DX Manager embeds Regular icon alpha masks derived from Microsoft's Fluent UI System Icons, source commit a563cf9166f4f91aa617557ed272612b7f0a2f72. Copyright (c)2020 Microsoft Corporation; MIT License. The complete notice is included in Fluent-System-Icons-LICENSE.txt.
https://github.com/microsoft/fluentui-system-icons
The mouse asset is modified from Keyboard Mouse20Regular by retaining its mouse component only. Other assets retain the official geometry and are converted to transparent masks for theme coloring. Original SVGs/provenance are retained in the source repository.