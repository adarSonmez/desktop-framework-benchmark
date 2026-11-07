# DesktopBench – App Specification

Every variant implements this document **exactly**. If a variant cannot meet an item, it says so, with the reason, in `apps/<name>/NOTES.md`.

## Window
- Title: `DesktopBench – <variant>` (variant names: `electron`, `tauri`, `flutter`, `react-native`, `maui-xaml`, `maui-blazor`)
- Size: 1280 × 800 (client area, in DIPs), resizable, centered on screen.

## Data
- Model: `{ id: int, name: string, category: string, price: number (2 decimals), stock: int, createdAt: "YYYY-MM-DD" }`
- File: the path in the `BENCH_DATA` environment variable if set, otherwise `%LOCALAPPDATA%\DesktopBench\items.json`.
  If the file does not exist: empty list, and the status bar shows `No data – run scripts/seed.ps1`.
- Reading and writing happen in the **native/host layer** (main/Rust process for web variants, a native module in React Native, C# in MAUI, `dart:io` in Flutter).
- Saving writes the whole list as a single JSON array (no indentation).
- Categories: Electronics, Books, Garden, Kitchen, Sports, Toys, Office, Health.

## Design tokens
| Token | Value |
|---|---|
| font | Segoe UI, 14px (headings 16px semibold) |
| bg | `#F6F7F9` |
| surface | `#FFFFFF` |
| border | `#E3E6EA` |
| text | `#1F2937` |
| muted | `#6B7280` |
| accent | `#2563EB` (selected row background `#DBEAFE`) |
| danger | `#DC2626` |

## Layout (top to bottom)
1. **Top bar** – 56px tall, surface, bottom border. Left to right:
   - App name `DesktopBench` (16px semibold)
   - Search box (280px wide, placeholder `Search name…`) – case-insensitive `contains` on the name
   - Sort selector: `ID ↑`, `Name ↑`, `Price ↑`, `Price ↓`
   - `New` button (accent, white text) – creates a new empty item and selects it
   - Right-aligned stats: `<visible> / <total> items`
2. **Body** – remaining height, two columns:
   - **List** (flexible width): header row (32px, muted, bold) + **virtualized** rows.
     Row height **32px**, columns: `ID` 70px · `Name` flexible · `Category` 130px · `Price` 100px right-aligned (`0.00`) · `Stock` 80px right-aligned · `Created` 110px.
     No zebra striping; 1px bottom border. Click → select (selected row `#DBEAFE`).
   - **Detail panel** (fixed 360px, left border, 16px padding): heading `Item #<id>` or `No selection`;
     form fields (label above): Name (text), Category (selector), Price (number), Stock (integer), Created (read-only text).
     Buttons: `Save` (accent), `Delete` (danger). The form is hidden when nothing is selected.
3. **Status bar** – 28px, surface, top border, muted 12px text: last action message (`Loaded 10000 items in 123 ms`, `Saved`, `Deleted #42` …).

## Behavior
- `Save`: applies the form values to the list and saves the file.
- `Delete`: removes the item from the list, clears the selection, saves the file.
- `New`: `id = max(id)+1`, `name = "New item"`, `category = "Books"`, `price = 0`, `stock = 0`, `createdAt = today`; appended to the list and selected (not saved yet).
- Filter and sort apply together; visible list = filter(sort(all)). Name sorting is ordinal.

## Benchmark mode
If the `BENCH_OUT` environment variable is set, the app runs the [BENCH_PROTOCOL.md](BENCH_PROTOCOL.md) scenario automatically.
