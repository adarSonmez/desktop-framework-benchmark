# DesktopBench results (20261107-141147)

Machine: VICTUS / 16 CPUs / Microsoft Windows NT 10.0.26300.0  
Runs: 1 cold + 10 warm per variant (round-robin), idle window 3000 ms. Values are medians of warm runs.

## Startup, memory, CPU

| Variant | Cold start ms | Warm start ms | Window ms | Idle WS MB | Idle private MB | Peak WS MB | Processes | Total CPU ms |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| electron | 725 | 546 | 327 | 347.6 | 220.1 | 449.5 | 4 | 3586 |
| tauri | 1162 | 614 | 45 | 407.2 | 212.3 | 515.3 | 7 | 6531 |
| flutter | 3067 | 2336 | 2382 | 197.0 | 207.2 | 218.5 | 1 | 6531 |
| react-native | 502 | 458 | 208 | 142.5 | 132.7 | 208.1 | 1 | 5773 |
| maui-xaml | 3005 | 1618 | 920 | 267.4 | 209.3 | 359.9 | 1 | 20484 |
| maui-blazor | 5030 | 1366 | 808 | 603.4 | 343.4 | 784.4 | 7 | 11836 |

## Package size

| Variant | On disk MB | Zipped MB | Files | Installer MB |
|---|---:|---:|---:|---:|
| electron | 357.1 | 155.0 | 75 | 115.7 |
| tauri | 4.1 | 1.6 | 1 | 1.3 |
| flutter | 26.5 | 11.2 | 12 | – |
| react-native | 206.8 | 72.1 | 301 | – |
| maui-xaml | 272.9 | 105.4 | 616 | – |
| maui-blazor | 285.8 | 107.3 | 794 | – |

## Interaction steps (ms, median)

| Variant | load | filter | sort | scroll | crud | save | cpuPrimes | cpuJson | ipc |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| electron | 76.7 | 74.4 | 73.0 | 864.7 | 195.2 | 10.0 | 243.1 | 54.4 | 145.2 |
| tauri | 50.0 | 62.1 | 76.0 | 851.8 | 190.8 | 50.5 | 247.8 | 59.3 | 1629.2 |
| flutter | 125.6 | 132.2 | 143.0 | 2354.2 | 213.0 | 40.3 | 254.8 | 413.9 | 62.9 |
| react-native | 139.8 | 632.6 | 746.9 | 8.6 | 1259.9 | 29.3 | 1577.5 | 188.1 | 17.2 |
| maui-xaml | 501.8 | 1426.3 | 1344.0 | 4791.1 | 852.9 | 46.9 | 224.6 | 492.8 | 0.7 |
| maui-blazor | 73.0 | 120.6 | 148.2 | 1132.0 | 2956.6 | 25.4 | 230.3 | 320.8 | 290.5 |

Notes: `filter`, `sort`, `scroll` and `crud` wait for two rendered frames per step, so they are bounded below by the display refresh cadence each framework achieves. `ipc` in maui-xaml is a direct in-process call (no UI/host boundary exists). See spec/BENCH_PROTOCOL.md.
