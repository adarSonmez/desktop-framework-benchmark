# Benchmark Protocol

## Environment variables
| Variable | Meaning |
|---|---|
| `BENCH_OUT` | Path of the result JSON. **Benchmark mode is on when this is set.** |
| `BENCH_DATA` | Data file (the harness copies a fresh seed before every run). |
| `BENCH_IDLE_MS` | Idle wait after `ready`, before the scenario (default 3000). The harness measures idle memory in this window. |

## Timestamps
- Every `*Ts` field is **Unix epoch milliseconds** (wall clock: `Date.now()` / `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()` or equivalent).
- Step durations use a high-resolution monotonic clock (`performance.now()`, `Stopwatch`, `std::time::Instant`), in fractional ms.
- "Wait for render" = **wait two frames** (web: `requestAnimationFrame` ×2; Flutter: `endOfFrame` ×2; React Native: `requestAnimationFrame` ×2; MAUI XAML: two `CompositionTarget.Rendering` ticks; Blazor: render batch flushed, then JS `requestAnimationFrame` ×2).

## Flow
1. The app starts, reads the data in the native layer, draws the list, **waits for render**.
2. `ready`: `readyTs` is recorded and written (plain text) to `<BENCH_OUT>.ready`.
3. Wait `BENCH_IDLE_MS`.
4. The scenario steps run **in this order**, each timed separately:

| Step | Definition |
|---|---|
| `load` | Request data → read + parsed + list drawn (the duration of flow step 1; not repeated during the scenario). |
| `filter` | Apply the search texts `"a"`, `"silver"`, `"ridge 0"`, `""` to UI state in turn; wait for render after each. Total time. |
| `sort` | Apply `Name ↑`, `Price ↑`, `Price ↓`, `ID ↑` in turn; wait for render after each. Total time. |
| `scroll` | From the top, scroll to the bottom in 60 steps (`offset = maxOffset * i / 60`, i = 1..60); wait for render after each step. Total time (mean frame = total / 60). Then scroll back to the top (not timed). |
| `crud` | Using the normal UI handlers (the state functions behind New/Save/Delete, **excluding the file save**): 500 × `New`, then update the 500 added items (`name += " *"`, `price = round(price * 1.1, 2)`), then delete the 500 added items. Wait for render at the end of each phase. Total time. |
| `save` | Send the whole list to the native layer and write it to the file as JSON (once). |
| `cpuPrimes` | In the UI language (JS/Dart/C#): count the primes from 2 to 2,000,000 by **trial division** (odd divisors, `d*d <= n`). Expected result `148933`. |
| `cpuJson` | In the UI language: serialize + parse the whole list 10 times. |
| `ipc` | 1,000 sequential UI → native → UI round trips: `ping(i)` returns `i+1` on the native side. Variants where this is a direct call (MAUI XAML) say so in NOTES.md. |

5. The result is written to `BENCH_OUT`, then the app **closes itself** (`exitTs` right before exiting).

## Result schema
```json
{
  "variant": "electron",
  "startTs": 0,          // epoch ms when app code first ran
  "readyTs": 0,
  "steps": { "load": 0, "filter": 0, "sort": 0, "scroll": 0, "crud": 0, "save": 0, "cpuPrimes": 0, "cpuJson": 0, "ipc": 0 },
  "checks": { "itemCount": 10000, "primeCount": 148933, "visibleAfterFilterA": 6802, "ipcFinal": 1000 },
  "exitTs": 0
}
```
The harness rejects a run unless `itemCount` is 10000 after `crud`, `primeCount` is 148933, `visibleAfterFilterA` is 6802 and `ipcFinal` is 1000.
