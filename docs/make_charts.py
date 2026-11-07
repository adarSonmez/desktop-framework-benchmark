"""Renders the article/README charts from results/latest/summary.json.

Usage: python docs/make_charts.py   (needs matplotlib)
Output: docs/images/*.png
"""
import json
from pathlib import Path

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.colors import LinearSegmentedColormap

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "docs" / "images"
OUT.mkdir(parents=True, exist_ok=True)
S = json.loads((ROOT / "results" / "latest" / "summary.json").read_text())["variants"]

# Fixed identity color per variant (validated categorical order, light mode) – same in every chart.
COLORS = {
    "electron": "#2a78d6",
    "tauri": "#eb6834",
    "flutter": "#1baf7a",
    "react-native": "#eda100",
    "maui-xaml": "#e87ba4",
    "maui-blazor": "#008300",
}
LABEL = {
    "electron": "Electron",
    "tauri": "Tauri",
    "flutter": "Flutter",
    "react-native": "React Native",
    "maui-xaml": "MAUI XAML",
    "maui-blazor": "MAUI Blazor",
}
SURFACE, TEXT, TEXT2, GRID = "#fcfcfb", "#0b0b0b", "#52514e", "#e6e5e0"

plt.rcParams.update({
    "font.family": ["Segoe UI", "DejaVu Sans"],
    "font.size": 12,
    "text.color": TEXT,
    "axes.labelcolor": TEXT2,
    "xtick.color": TEXT2,
    "ytick.color": TEXT,
    "axes.edgecolor": GRID,
    "figure.facecolor": SURFACE,
    "axes.facecolor": SURFACE,
    "savefig.facecolor": SURFACE,
})


def med(v, key):
    return S[v]["metrics"][key]["median"]


def size_mb(v, field):
    b = S[v]["size"].get(field)
    return None if b is None else b / 1048576


def hbar(ax, values, fmt, title, subtitle, xlabel, highlight_min=True):
    """Sorted horizontal bars, best on top, value labels at bar ends."""
    items = sorted(values.items(), key=lambda kv: kv[1], reverse=True)
    names = [LABEL[k] for k, _ in items]
    vals = [v for _, v in items]
    bars = ax.barh(names, vals, color=[COLORS[k] for k, _ in items], height=0.62, edgecolor=SURFACE, linewidth=2)
    vmax = max(vals)
    for b, v in zip(bars, vals):
        ax.text(b.get_width() + vmax * 0.012, b.get_y() + b.get_height() / 2, fmt(v), va="center", ha="left",
                color=TEXT, fontsize=11.5, fontweight="semibold")
    ax.set_xlim(0, vmax * 1.18)
    ax.set_title(title, loc="left", fontsize=16, fontweight="bold", pad=26)
    ax.text(0, 1.02, subtitle, transform=ax.transAxes, color=TEXT2, fontsize=11)
    ax.set_xlabel(xlabel)
    ax.grid(axis="x", color=GRID, linewidth=1)
    ax.set_axisbelow(True)
    for s in ("top", "right", "left"):
        ax.spines[s].set_visible(False)
    ax.tick_params(axis="y", length=0)


def save(fig, name):
    fig.text(0.99, -0.03, "github.com/adarSonmez/desktop-framework-benchmark", ha="right", va="top",
             fontsize=9, color=TEXT2)
    fig.savefig(OUT / name, dpi=200, bbox_inches="tight", pad_inches=0.3)
    plt.close(fig)
    print("wrote", OUT / name)


V = list(COLORS)

# 1. Hero: four small multiples
fig, axes = plt.subplots(2, 2, figsize=(13, 8.2))
hbar(axes[0][0], {v: med(v, "startupMs") for v in V}, lambda x: f"{x:,.0f} ms", "Startup", "launch → list painted (warm, median)", "ms")
hbar(axes[0][1], {v: med(v, "idlePrivateMb") for v in V}, lambda x: f"{x:,.0f} MB", "Idle memory", "private bytes, whole process tree", "MB")
hbar(axes[1][0], {v: size_mb(v, "zipBytes") for v in V}, lambda x: f"{x:,.1f} MB", "Download size", "app folder, zipped", "MB")
hbar(axes[1][1], {v: med(v, "cpuMs") / 1000 for v in V}, lambda x: f"{x:,.1f} s", "CPU time", "all processes, full scenario", "seconds")
fig.suptitle("Same app, six frameworks — shorter is better", x=0.01, ha="left", fontsize=20, fontweight="bold", y=1.02)
fig.tight_layout(h_pad=3.0, w_pad=3.0)
save(fig, "hero.png")

# 2. Startup: dumbbell, warm (filled, variant color) → cold (hollow)
fig, ax = plt.subplots(figsize=(10, 5))
warm = {v: med(v, "startupMs") for v in V}
order = [k for k, _ in sorted(warm.items(), key=lambda kv: kv[1], reverse=True)]
ys = list(range(len(order)))
for y, v in zip(ys, order):
    w, c = warm[v], S[v]["coldStartupMs"]
    ax.plot([w, c], [y, y], color=GRID, linewidth=4, solid_capstyle="round", zorder=1)
    ax.scatter([c], [y], s=110, facecolor=SURFACE, edgecolor=COLORS[v], linewidth=2.5, zorder=3)
    ax.scatter([w], [y], s=130, color=COLORS[v], edgecolor=SURFACE, linewidth=2, zorder=4)
    ax.text(max(w, c) + 120, y, f"warm {w:,.0f} ms", va="center", ha="left", fontsize=11.5, fontweight="semibold")
    ax.text(max(w, c) + 1250, y, f"cold {c:,.0f} ms", va="center", ha="left", fontsize=10, color=TEXT2)
ax.set_yticks(ys, [LABEL[v] for v in order])
ax.set_xlim(0, max(S[v]["coldStartupMs"] for v in V) * 1.5)
ax.set_title("Time to first painted list", loc="left", fontsize=16, fontweight="bold", pad=26)
ax.text(0, 1.02, "● warm start (median of 10 runs)   ○ cold start (first run)", transform=ax.transAxes, color=TEXT2, fontsize=11)
ax.set_xlabel("milliseconds")
ax.grid(axis="x", color=GRID, linewidth=1)
ax.set_axisbelow(True)
for s in ("top", "right", "left"):
    ax.spines[s].set_visible(False)
ax.tick_params(axis="y", length=0)
save(fig, "startup.png")

# 3. Memory: working set
fig, ax = plt.subplots(figsize=(10, 5))
hbar(ax, {v: med(v, "idleWorkingSetMb") for v in V}, lambda x: f"{x:,.0f} MB", "Idle memory (working set)",
     "summed over every process the app spawns — WebView2 and Chromium helpers included", "MB")
save(fig, "memory.png")

# 4. Package size (zipped) + installer annotation
fig, ax = plt.subplots(figsize=(10, 5))
hbar(ax, {v: size_mb(v, "diskBytes") for v in V}, lambda x: f"{x:,.1f} MB", "What you ship",
     "release output on disk — nothing else to install on Windows 11", "MB")
save(fig, "size.png")

# 5. IPC
ipc = {v: med(v, "step.ipc") for v in V if v != "maui-xaml"}
fig, ax = plt.subplots(figsize=(10, 4.4))
hbar(ax, ipc, lambda x: f"{x:,.0f} ms", "1,000 UI ↔ native round trips",
     "sequential ping(n) → n+1 over each framework's bridge (MAUI XAML has none, omitted)", "milliseconds")
save(fig, "ipc.png")

# 6. CPU primes
fig, ax = plt.subplots(figsize=(10, 4.8))
hbar(ax, {v: med(v, "step.cpuPrimes") for v in V}, lambda x: f"{x:,.0f} ms", "Raw compute in the UI language",
     "count primes to 2,000,000 by trial division — JS (V8 / Hermes), Dart, C#", "milliseconds")
save(fig, "cpu.png")

# 7. Interaction heatmap: slowdown vs the fastest variant per step
steps = ["load", "filter", "sort", "scroll", "crud", "save", "cpuJson"]
step_names = ["load", "filter", "sort", "scroll", "CRUD ×1500", "save", "JSON ×10"]
na = {("react-native", "scroll")}
rows = []
for v in V:
    rows.append([med(v, "step." + s) for s in steps])
best = [min(rows[i][j] for i, v in enumerate(V) if (v, steps[j]) not in na) for j in range(len(steps))]
ratio = [[None if (v, steps[j]) in na else rows[i][j] / best[j] for j in range(len(steps))] for i, v in enumerate(V)]
cmap = LinearSegmentedColormap.from_list("seq", ["#cde2fb", "#86b6ef", "#3987e5", "#256abf", "#104281", "#0d366b"])
import math
fig, ax = plt.subplots(figsize=(11, 5.2))
for i, v in enumerate(V):
    for j in range(len(steps)):
        r = ratio[i][j]
        if r is None:
            ax.add_patch(plt.Rectangle((j, i), 1, 1, facecolor="#f0efec", edgecolor=SURFACE, linewidth=3))
            ax.text(j + 0.5, i + 0.5, "n/a¹", ha="center", va="center", color=TEXT2, fontsize=10)
            continue
        t = min(1.0, math.log10(r) / math.log10(40))
        ax.add_patch(plt.Rectangle((j, i), 1, 1, facecolor=cmap(t), edgecolor=SURFACE, linewidth=3))
        ax.text(j + 0.5, i + 0.42, f"{rows[i][j]:,.0f} ms", ha="center", va="center", fontsize=10.5,
                color="white" if t > 0.3 else TEXT, fontweight="semibold")
        ax.text(j + 0.5, i + 0.7, "fastest" if r < 1.005 else f"{r:.1f}×", ha="center", va="center", fontsize=9,
                color="white" if t > 0.3 else TEXT2)
ax.set_xlim(0, len(steps))
ax.set_ylim(len(V), 0)
ax.set_xticks([j + 0.5 for j in range(len(steps))], step_names)
ax.set_yticks([i + 0.5 for i in range(len(V))], [LABEL[v] for v in V])
ax.xaxis.tick_top()
ax.tick_params(length=0)
for s in ax.spines.values():
    s.set_visible(False)
ax.set_title("Interaction steps — time and slowdown vs. the fastest", loc="left", fontsize=16, fontweight="bold", pad=34)
fig.text(0.01, -0.02, "Darker = slower relative to the best variant for that step. Median of 10 runs.\n"
         "¹ react-native-windows' requestAnimationFrame isn't tied to compositor frames, so its scroll number isn't comparable.",
         fontsize=9.5, color=TEXT2)
save(fig, "interaction.png")
