# flutter notes
- Data I/O via `dart:io` (Dart is the host language). `ipc` is a `MethodChannel` round trip to a C++ handler in `windows/runner/flutter_window.cpp`.
- Runner modified to size the *client* area to 1280×800 DIPs and center the window (`win32_window.cpp`).
- "Wait for render" = `scheduleFrame()` + `endOfFrame` twice.
