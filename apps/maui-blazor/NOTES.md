# maui-blazor notes
- UI is Razor rendered into WebView2 via `BlazorWebView`; C# runs in the host process, so data I/O is plain .NET.
- `ipc` step: .NET → JS (`bench.ping`) → .NET via `IJSRuntime` — the real bridge in this architecture (the direction is reversed compared to Electron/Tauri, where JS calls the host).
- List virtualization uses Blazor's `<Virtualize ItemSize="32">` (rows in flow + spacers); styling is the shared `styles.css`.
