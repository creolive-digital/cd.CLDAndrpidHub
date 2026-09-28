# Validation — 2026-09-28

## Passed

- .NET 10 build and self-contained Windows x64 publish; zero build warnings/errors.
- Published executable starts and locates its adjacent RuntimeAssets directory.
- Connected Samsung SM-A226B detected and authorized; Android 13 and battery information read.
- Launch mirror opens a native Direct3D 11 stream; the fullscreen window measured 1920 × 1080 at desktop origin.
- FPS telemetry showed approximately 22–29 FPS during the sampled phone content. These measurements do not establish a 60/120 FPS benchmark.
- Device selection disabled during mirroring; Stop closes the stream gracefully and restores launch controls.
- Recording through the dashboard Save dialog produced a 4,916,066-byte MKV; scrcpy reported Recording complete after Stop.
- Standalone engine recording also completed with a five-second time limit.
- Dashboard inspected through Windows UI Automation and a rendered window capture.

## Fixes found during validation

- Single-file executable asset lookup corrected to use the executable directory.
- Selected-device label corrected for the custom ComboBox template.
- Repeated launch blocked before opening the recording Save dialog.
- Scanning and wireless controls guarded against changes during an active stream.

## Not verified

- Wireless pairing on a real Wi-Fi connection; unauthorized/offline-device recovery on hardware.
- 120 FPS performance, other Android devices, or protected-content capture.
- Audio quality by listening; screenshot export, APK installation, and file transfer were retained but not exercised end to end.

Temporary test recordings were removed after validation.
