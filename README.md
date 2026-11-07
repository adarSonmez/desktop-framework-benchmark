# desktop-framework-benchmark

**One desktop app, written six times, measured the same way.**

This repo builds the same data-heavy desktop app with six modern desktop stacks: a 10,000-row virtualized list with search, sort, CRUD, a detail form and JSON file storage. A harness then launches each build on Windows, samples the whole process tree and validates every run. The goal is an apples-to-apples comparison of what users feel (startup, memory, responsiveness) and what developers ship (package size).

| Variant | Stack | UI layer | Host / I/O layer |
|---|---|---|---|
| [`electron`](apps/electron) | Electron 45 | Shared React UI ([`shared/web-ui`](shared/web-ui)) | Node main process (`ipcMain`) |
| [`tauri`](apps/tauri) | Tauri 2 + WebView2 | Shared React UI | Rust commands |
| [`flutter`](apps/flutter) | Flutter 3.47 (Windows, Impeller) | Dart widgets | `dart:io` + C++ `MethodChannel` |
| [`react-native`](apps/react-native) | react-native-windows 0.85 (Fabric, Hermes) | RN components | C++ native module |
| [`maui-xaml`](apps/maui-xaml) | .NET 10 MAUI (WinUI 3) | XAML `CollectionView` | C# ([`shared/dotnet`](shared/dotnet/DesktopBench.Core)) |
| [`maui-blazor`](apps/maui-blazor) | .NET 10 MAUI Blazor Hybrid | Razor in WebView2 | C# |

Electron and Tauri run the **exact same** React bundle, so the gap between them is purely the shell. The two MAUI variants share one C# core (model, I/O, benchmark runner).

---

## Results

> **Machine:** AMD Ryzen 7 7840HS (16 threads), 15 GB RAM, Radeon 780M + RTX 3050 Laptop, 144 Hz display, Windows 11 Pro (build 26300).
> **Method:** 1 cold + 10 warm runs per variant, round-robin; values are **medians of warm runs**. All 60 warm runs passed validation.
> Full data: [`results/latest/report.md`](results/latest/report.md) · [`summary.csv`](results/latest/summary.csv) · [`summary.json`](results/latest/summary.json)

### Startup, memory, CPU

Memory and CPU include **every process in the tree** (WebView2 and Chromium helpers count).

| Variant | Cold start | Warm start | Idle working set | Idle private | Peak working set | Processes | CPU time (whole run) |
|---|---:|---:|---:|---:|---:|---:|---:|
| react-native | 502 ms | **458 ms** | **143 MB** | **133 MB** | **208 MB** | 1 | 5.8 s |
| electron | 725 ms | 546 ms | 348 MB | 220 MB | 450 MB | 4 | **3.6 s** |
| tauri | 1,162 ms | 614 ms | 407 MB | 212 MB | 515 MB | 7 | 6.5 s |
| maui-blazor | 5,030 ms | 1,366 ms | 603 MB | 343 MB | 784 MB | 7 | 11.8 s |
| maui-xaml | 3,005 ms | 1,618 ms | 267 MB | 209 MB | 360 MB | 1 | 20.5 s |
| flutter | 3,067 ms | 2,336 ms | 197 MB | 207 MB | 219 MB | 1 | 6.5 s |

*Start* = process launch → first list painted (reported by the app).

### Package size

| Variant | On disk | Zipped | Files | Installer |
|---|---:|---:|---:|---:|
| tauri | **4.1 MB** | **1.6 MB** | 1 | **1.3 MB** (NSIS) |
| flutter | 26.5 MB | 11.2 MB | 12 | – |
| react-native | 206.8 MB | 72.1 MB | 301 | – |
| maui-xaml | 272.9 MB | 105.4 MB | 616 | – |
| maui-blazor | 285.8 MB | 107.3 MB | 794 | – |
| electron | 357.1 MB | 155.0 MB | 75 | 115.7 MB (NSIS) |

MAUI and React Native are built **self-contained** (the .NET runtime and Windows App SDK ship with the app), so nothing needs to be installed first. Tauri relies on the WebView2 runtime that ships with Windows 11.

### Interaction (ms, median, lower is better)

| Variant | load | filter | sort | scroll | crud | save | cpuPrimes | cpuJson | ipc ×1000 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| electron | 77 | 74 | 73 | 865 | 195 | **10** | 243 | **54** | 145 |
| tauri | **50** | **62** | 76 | 852 | **191** | 51 | 248 | 59 | 1,629 |
| flutter | 126 | 132 | 143 | 2,354 | 213 | 40 | 255 | 414 | 63 |
| react-native | 140 | 633 | 747 | *9*¹ | 1,260 | 29 | 1,578 | 188 | 17 |
| maui-xaml | 502 | 1,426 | 1,344 | 4,791 | 853 | 47 | **225** | 493 | *0.7*² |
| maui-blazor | 73 | 121 | 148 | 1,132 | 2,957 | 25 | 230 | 321 | 291 |

¹ In react-native-windows, `requestAnimationFrame` and `scrollToOffset` are not tied to compositor frames, so this number measures only the JS call. It is not comparable.
² MAUI XAML has no UI/host boundary; `ipc` is a direct in-process call.

### What stands out

- **React Native for Windows** had the fastest start and the smallest memory footprint (one process, no browser engine). Hermes has no JIT, though, so pure-JS compute (`cpuPrimes`) is about 6× slower than V8 or .NET.
- **Electron vs Tauri** (same React bundle): Tauri is ~87× smaller on disk. At runtime the two are close: WebView2 is Chromium too, and Tauri's tree had more processes and a larger working set here. Tauri's IPC round trip was the slowest measured (~1.6 ms each).
- **Flutter** spends ~2.2 s before Dart `main()` even runs (engine and GPU initialization on this machine); from Dart `main()` to the first frame takes only ~130 ms. Its frame-bound steps are paced at 60 Hz, while the WebView variants ran at the display's 144 Hz.
- **MAUI XAML** is fine for compute but slow at re-binding a 10k-item `CollectionView` (filter, sort and scroll each take seconds).
- **MAUI Blazor** renders quickly through `<Virtualize>`, but bulk state churn (`crud`, 1,500 operations) is the slowest of all six.

Treat these as one machine's numbers, not universal truths. Run the harness on your own hardware.

---

## How the benchmark works

```
bench/Harness (.NET)  ──launch──▶  app.exe  (BENCH_OUT, BENCH_DATA, BENCH_IDLE_MS)
      │                               │
      │ every 100 ms: process tree    │ load → paint → write <out>.ready
      │ (Toolhelp32) → WS, private,   │ idle (memory window)
      │ CPU time of every descendant  │ filter → sort → scroll → crud → save → cpu → ipc
      │                               │ write <out>.json, exit
      ◀──────────── validate checks, aggregate (median / p95 / CV) ─────────────
```

- **Spec:** every variant follows [`spec/APP_SPEC.md`](spec/APP_SPEC.md): the same layout, design tokens, 1280×800 client area and behavior.
- **Protocol:** [`spec/BENCH_PROTOCOL.md`](spec/BENCH_PROTOCOL.md) defines the scenario step by step. UI steps wait for **two rendered frames** using each framework's own frame signal.
- **Same data:** a deterministic 10,000-item dataset ([`spec/gen-data.mjs`](spec/gen-data.mjs)), freshly copied before every run.
- **Same work:** the benchmark logic is shared wherever the language allows: one TypeScript runner for Electron, Tauri and React Native; one C# runner for both MAUI variants; a Dart port for Flutter.
- **Validation:** a run only counts if item count, prime count, filter result and IPC result all match.
- **Known deviations:** each variant documents its deviations in `apps/<variant>/NOTES.md`.

## Repository layout

```
spec/                 app spec, benchmark protocol, deterministic dataset
shared/web-ui/        React UI + store + bench runner (Electron, Tauri; store/bench also used by React Native)
shared/dotnet/        DesktopBench.Core: model, I/O, bench runner (both MAUI variants)
apps/<variant>/       one folder per technology, each with NOTES.md
bench/Harness/        .NET harness + variants.json
scripts/              build-all, bench-all, run-once, seed
results/              raw runs + aggregated reports
```

## Run it yourself

**Prerequisites (Windows 10/11 x64):** Node 22+, Rust (stable, MSVC), Flutter SDK, .NET 10 SDK with the `maui-windows` workload, and Visual Studio with *Desktop development with C++* and Windows SDK 10.0.26100.

```powershell
scripts/build-all.ps1               # release builds of all six variants + the harness
scripts/bench-all.ps1 -Runs 10      # full benchmark → results/<timestamp>/ and results/latest/
scripts/run-once.ps1 <path-to-exe>  # one benchmark-mode run of a single build
scripts/seed.ps1                    # seed %LOCALAPPDATA%\DesktopBench for using the apps by hand
```

Close other apps and keep the laptop on AC power while benchmarking.

> **Note:** Electron is packaged to `../desktopbench-out/electron` (outside the repo). Editors and tools built on Electron, such as the Claude desktop app, can hold `.asar` files in an open workspace and block electron-builder.

## Adding a variant

1. Implement [`spec/APP_SPEC.md`](spec/APP_SPEC.md) and [`spec/BENCH_PROTOCOL.md`](spec/BENCH_PROTOCOL.md) under `apps/<name>/`, and document any deviations in `NOTES.md`.
2. Add the build to [`scripts/build-all.ps1`](scripts/build-all.ps1) and the exe/output folder to [`bench/variants.json`](bench/variants.json).
3. Check one run with `scripts/run-once.ps1`, then run the full benchmark.
