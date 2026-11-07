import type { AppApi } from "./App";
import type { Bridge, SortKey } from "./types";

export const waitFrames = () => new Promise<void>((r) => requestAnimationFrame(() => requestAnimationFrame(() => r())));
const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms));

export function countPrimes(limit: number): number {
  let count = limit >= 2 ? 1 : 0;
  for (let n = 3; n <= limit; n += 2) {
    let prime = true;
    for (let d = 3; d * d <= n; d += 2) {
      if (n % d === 0) {
        prime = false;
        break;
      }
    }
    if (prime) count++;
  }
  return count;
}

async function time(fn: () => Promise<void> | void): Promise<number> {
  const t0 = performance.now();
  await fn();
  return +(performance.now() - t0).toFixed(3);
}

export interface StartupInfo {
  startTs: number;
  readyTs: number;
  loadMs: number;
}

export async function runBench(api: AppApi, bridge: Bridge, startup: StartupInfo) {
  const out = bridge.benchOut!;
  await bridge.writeFile(out + ".ready", String(startup.readyTs));
  await sleep(bridge.idleMs);

  const steps: Record<string, number> = { load: startup.loadMs };
  const checks: Record<string, number> = {};
  const store = api.store;

  steps.filter = await time(async () => {
    for (const q of ["a", "silver", "ridge 0", ""]) {
      api.setQuery(q);
      await waitFrames();
      // Read after the render wait: React Native applies state asynchronously (no flushSync).
      if (q === "a") checks.visibleAfterFilterA = api.visibleCount();
    }
  });

  steps.sort = await time(async () => {
    for (const s of ["name", "priceAsc", "priceDesc", "id"] as SortKey[]) {
      api.setSort(s);
      await waitFrames();
    }
  });

  api.scrollTo(0);
  await waitFrames();
  steps.scroll = await time(async () => {
    for (let i = 1; i <= 60; i++) {
      api.scrollTo(i / 60);
      await waitFrames();
    }
  });
  api.scrollTo(0);
  await waitFrames();

  steps.crud = await time(async () => {
    const added: number[] = [];
    for (let i = 0; i < 500; i++) added.push(store.create().id);
    await waitFrames();
    for (const id of added) {
      const it = store.items.find((x) => x.id === id)!;
      store.update({ ...it, name: it.name + " *", price: Math.round(it.price * 1.1 * 100) / 100 });
    }
    await waitFrames();
    for (const id of added) store.remove(id);
    await waitFrames();
  });
  checks.itemCount = store.items.length;

  steps.save = await time(() => bridge.saveItems(store.items));

  steps.cpuPrimes = await time(() => {
    checks.primeCount = countPrimes(2_000_000);
  });

  steps.cpuJson = await time(() => {
    for (let i = 0; i < 10; i++) JSON.parse(JSON.stringify(store.items));
  });

  steps.ipc = await time(async () => {
    let v = 0;
    for (let i = 0; i < 1000; i++) v = await bridge.ping(v);
    checks.ipcFinal = v;
  });

  api.status("Benchmark done");
  const result = {
    variant: bridge.variant,
    startTs: startup.startTs,
    readyTs: startup.readyTs,
    steps,
    checks,
    exitTs: Date.now(),
  };
  await bridge.writeFile(out, JSON.stringify(result, null, 2));
  await bridge.exit();
}
