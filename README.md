# CLD Android Hub 2.1

A Windows desktop dashboard for Android, with scrcpy 4.1 video mirroring.

## Run

Open `build\win-x64\CLD Android Hub.exe`. Keep the entire `RuntimeAssets` directory beside it. The release includes the .NET runtime; no separate .NET installation is needed.

1. Enable USB debugging under Android Developer options.
2. Connect the phone over USB and accept its authorization prompt.
3. Click **Scan devices** and select your phone.
4. Choose a stream preset, then **Launch mirror**.

The live mirror opens in a separate desktop window. **Start fullscreen** is enabled by default. Press **F11** or **Alt+F** in that window to switch between fullscreen and windowed playback. **Alt+Q** closes the mirror. The dashboard's **Stop** button ends the stream and allows recordings to finalize.

## Features

- H.264 video streaming, native mouse/keyboard control and automatic clipboard synchronization through scrcpy.
- Balanced (1280 px, 60 FPS, 8 Mbps), High detail (1920 px, 60 FPS, 12 Mbps), Wi-Fi smooth (1024 px, 60 FPS, 4 Mbps, 50 ms buffer), High refresh (1920 px, 120 FPS, 16 Mbps).
- Actual FPS readout and session diagnostics. Resolution values cap the longest screen dimension; FPS values are limits. Static screens, video frame rate, phone refresh rate, encoder performance and network conditions affect measured FPS.
- Phone audio, fullscreen, always on top, keep awake while plugged in, and phone display off while mirroring.
- Session recording to MKV. Choose Record session before launch. Close the mirror normally or click Stop to finalize the file.
- Save screenshots, send files to Downloads, install APKs, and navigation/volume controls.
- Android 11+ wireless pairing: enter the pairing address/port and six-digit code, pair, then enter the separate connection address/port from the main Wireless debugging screen.
- Local saved stream preferences in `%LOCALAPPDATA%\CLD Android Hub\settings.json`. Pairing codes are not saved.

## Playback tips

Use Balanced over USB first. Try Wi-Fi smooth if wireless motion stutters. High refresh requires suitable phone hardware and content; it does not force a 60 Hz device to output 120 FPS. Fullscreen preserves the phone's aspect ratio, so portrait screens have side borders. Rotate the phone or use Alt+Left/Right to rotate the view. Audio forwarding requires Android 11+; some applications block audio or protected video capture.

## Build

Requires the .NET 10 SDK on Windows:

```powershell
dotnet build
dotnet publish -c Release -o build/win-x64
```

The project uses local bundled runtime assets instead of depending on files in a parent folder.

## Third-party software

Bundled scrcpy/ADB/media libraries were copied from the existing `scrcpy-win64-v4.1` installation on this PC. See `RuntimeAssets\LICENSE.txt` and the upstream project for notices and source: https://github.com/Genymobile/scrcpy .

## CLD branding

The CLD monogram, cyan/violet palette, aurora background, slim scrollbars and icon controls are used across the dashboard. The executable, mirror window, dialogs, export filenames and app metadata use CLD Android Hub branding. Existing preferences migrate from the previous settings folder.

Brand assets: `BrandAssets/cld-logo.svg` (vector), `BrandAssets/cld-hub.ico` (Windows icon), and `BrandAssets/scrcpy.png` (mirror icon). `BrandAssets/Brand.xaml` is the native WPF vector logo. Re-export raster icons with `tools/Export-BrandIcon.ps1` after changing the vector geometry. Third-party notices retain their original attribution.
