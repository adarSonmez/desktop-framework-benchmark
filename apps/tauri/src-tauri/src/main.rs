#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use std::path::PathBuf;
use std::time::{SystemTime, UNIX_EPOCH};
use tauri::ipc::Response;

struct Cfg {
    start_ts: u64,
    bench_out: Option<String>,
    idle_ms: u64,
    data_file: PathBuf,
}

#[derive(serde::Serialize)]
#[serde(rename_all = "camelCase")]
struct CfgDto {
    start_ts: u64,
    bench_out: Option<String>,
    idle_ms: u64,
}

#[tauri::command]
fn config(cfg: tauri::State<Cfg>) -> CfgDto {
    CfgDto { start_ts: cfg.start_ts, bench_out: cfg.bench_out.clone(), idle_ms: cfg.idle_ms }
}

/// Returns raw file bytes (empty when the file does not exist) to avoid double JSON encoding.
#[tauri::command]
fn load_text(cfg: tauri::State<Cfg>) -> Response {
    Response::new(std::fs::read(&cfg.data_file).unwrap_or_default())
}

#[tauri::command]
fn save_text(cfg: tauri::State<Cfg>, text: String) -> Result<(), String> {
    std::fs::write(&cfg.data_file, text).map_err(|e| e.to_string())
}

#[tauri::command]
fn ping(n: i64) -> i64 {
    n + 1
}

#[tauri::command]
fn write_file(path: String, content: String) -> Result<(), String> {
    std::fs::write(path, content).map_err(|e| e.to_string())
}

#[tauri::command]
fn exit(app: tauri::AppHandle) {
    std::thread::spawn(move || {
        std::thread::sleep(std::time::Duration::from_millis(50));
        app.exit(0);
    });
}

fn main() {
    let start_ts = SystemTime::now().duration_since(UNIX_EPOCH).unwrap().as_millis() as u64;
    let data_file = std::env::var("BENCH_DATA").map(PathBuf::from).unwrap_or_else(|_| {
        PathBuf::from(std::env::var("LOCALAPPDATA").unwrap_or_default()).join("DesktopBench").join("items.json")
    });
    let cfg = Cfg {
        start_ts,
        bench_out: std::env::var("BENCH_OUT").ok(),
        idle_ms: std::env::var("BENCH_IDLE_MS").ok().and_then(|v| v.parse().ok()).unwrap_or(3000),
        data_file,
    };
    tauri::Builder::default()
        .manage(cfg)
        .invoke_handler(tauri::generate_handler![config, load_text, save_text, ping, write_file, exit])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
