const { contextBridge, ipcRenderer } = require("electron");

const arg = process.argv.find((a) => a.startsWith("--bench-start="));
contextBridge.exposeInMainWorld("host", {
  variant: "electron",
  startTs: arg ? Number(arg.split("=")[1]) : Date.now(),
  benchOut: process.env.BENCH_OUT || null,
  idleMs: Number(process.env.BENCH_IDLE_MS || 3000),
  loadText: () => ipcRenderer.invoke("loadText"),
  saveText: (t) => ipcRenderer.invoke("saveText", t),
  ping: (n) => ipcRenderer.invoke("ping", n),
  writeFile: (p, c) => ipcRenderer.invoke("writeFile", p, c),
  exit: () => ipcRenderer.invoke("exit"),
});
