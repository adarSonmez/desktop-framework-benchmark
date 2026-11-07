# react-native notes
- react-native-windows 0.85, New Architecture (Fabric, Hermes), C++ app template, unpackaged exe.
- Host I/O is a C++ attributed native module (`windows/DesktopBenchRN/BenchHost.h`) exposed as `NativeModules.BenchHost`.
- Reuses `shared/web-ui/src/{store,bench,types}.ts` through Metro `watchFolders`.
- Deviation: react-native core has no dropdown, so the sort selector and the Category field are segmented controls instead of `<select>`.
- "Wait for render" = `requestAnimationFrame` ×2 in JS; with Fabric the native mount happens on the UI thread, so this approximates (does not guarantee) on-screen paint.
- Caveat on frame-bound steps: in react-native-windows, `requestAnimationFrame` and `scrollToOffset` are not tied to compositor frames, so `scroll` mostly measures the JS call cost (FlatList renders new rows asynchronously afterwards). Treat RN's `scroll` number as not comparable.
- Hermes has no JIT, which shows up in `cpuPrimes`.
- Windows App SDK is bundled (`WindowsAppSDKSelfContained`, see `windows/Directory.Build.props`) because the template's runtime version was newer than the one installed system-wide.
- Release builds log React Native errors to `%TEMP%\desktopbench-rn.log`.
