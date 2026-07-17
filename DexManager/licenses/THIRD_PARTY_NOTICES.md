# DX Manager v1.0.0 - Third-Party Notices

DX Manager is an independently developed utility. The third-party binaries
listed below are distributed without modification and remain under their
respective licenses. DX Manager's MIT License does not replace or alter those
licenses.

## scrcpy

DX Manager includes and uses scrcpy 4.0 to display and control Android
devices.

Copyright (C) 2018 Genymobile  
Copyright (C) 2018-2026 Romain Vimont

scrcpy is licensed under the Apache License, Version 2.0. The complete
license text is provided in `scrcpy-LICENSE.txt` in this directory.

Official project: https://github.com/Genymobile/scrcpy

Bundled version: 4.0

## Android Debug Bridge (ADB)

DX Manager includes Android Debug Bridge binaries from the Android Open
Source Project. The scrcpy runtime contains platform-tools 37.0.0, and the
Windows 7/8.1 fallback contains platform-tools 34.0.1.

ADB source is distributed under the Apache License, Version 2.0. The complete
Apache 2.0 license text is provided in `scrcpy-LICENSE.txt` in this directory.

Official source: https://android.googlesource.com/platform/packages/modules/adb/

## SDL

The Windows scrcpy client includes SDL 3.4.8, distributed under the zlib
license.

Copyright (C) 1997-2026 Sam Lantinga

Official project and license:
https://github.com/libsdl-org/SDL/blob/release-3.4.8/LICENSE.txt

The complete notice is provided in `SDL3-LICENSE.txt` in this directory.

## FFmpeg libraries

The Windows scrcpy client dynamically loads FFmpeg libraries including
libavcodec 62.28.101, libavformat 62.12.101, libavutil 60.26.101, and
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

The Windows scrcpy client includes libusb 1.0.29. libusb is distributed under
the GNU Lesser General Public License, version 2.1 or later.

Official project and source: https://github.com/libusb/libusb/tree/v1.0.29

## MinGW-w64 runtime

The Windows 7/8.1 fallback ADB includes `libwinpthread-1.dll` from the
MinGW-w64 runtime. Its MIT/BSD-style notices are provided in
`MinGW-w64-winpthreads-LICENSE.txt` in this directory.

Official project and source: https://www.mingw-w64.org/source/

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
