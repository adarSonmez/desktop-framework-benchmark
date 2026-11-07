import { createRef } from "react";
import { createRoot } from "react-dom/client";
import { flushSync } from "react-dom";
import { App, type AppApi } from "./App";
import { runBench, waitFrames } from "./bench";
import { ItemStore } from "./store";
import type { Bridge } from "./types";
import "./styles.css";

/** Entry point every web shell calls with its own Bridge implementation. */
export async function mount(bridge: Bridge) {
  document.title = `DesktopBench – ${bridge.variant}`;
  const store = new ItemStore();
  const api = createRef<AppApi>();
  const root = createRoot(document.getElementById("root")!);
  flushSync(() => root.render(<App ref={api} bridge={bridge} store={store} />));

  const t0 = performance.now();
  const items = await bridge.loadItems();
  store.setAll(items ?? []);
  await waitFrames();
  const loadMs = +(performance.now() - t0).toFixed(3);
  const readyTs = Date.now();
  api.current!.status(items ? `Loaded ${items.length} items in ${Math.round(loadMs)} ms` : "No data – run scripts/seed.ps1");

  if (bridge.benchOut) {
    await runBench(api.current!, bridge, { startTs: bridge.startTs, readyTs, loadMs });
  }
}

export type { Bridge, Item } from "./types";
