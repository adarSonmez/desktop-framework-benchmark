# electron notes
- Shared React UI; host I/O in the main process via `ipcMain.handle` + `contextBridge` preload. `backgroundThrottling: false`.
- Packaged with electron-builder (asar, NSIS). Size = `release/win-unpacked` + installer.
