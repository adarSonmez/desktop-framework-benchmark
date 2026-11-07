# tauri notes
- Shared React UI in WebView2; host commands in Rust (`src-tauri/src/main.rs`). `load_text` returns raw bytes (`tauri::ipc::Response`) to avoid double JSON encoding.
- Release profile: LTO, codegen-units=1, strip, panic=abort. Distribution size = single exe (WebView2 runtime is part of Windows 11).
