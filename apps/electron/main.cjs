const startTs = Date.now();
const { app, BrowserWindow, ipcMain } = require("electron");
const fs = require("node:fs/promises");
const path = require("node:path");

const dataFile = process.env.BENCH_DATA || path.join(process.env.LOCALAPPDATA, "DesktopBench", "items.json");

ipcMain.handle("loadText", async () => {
  try { return await fs.readFile(dataFile, "utf8"); } catch { return null; }
});
ipcMain.handle("saveText", (_e, text) => fs.writeFile(dataFile, text, "utf8"));
ipcMain.handle("ping", (_e, n) => n + 1);
ipcMain.handle("writeFile", (_e, p, c) => fs.writeFile(p, c, "utf8"));
ipcMain.handle("exit", () => {
  setTimeout(() => app.exit(0), 50);
});

app.whenReady().then(() => {
  const win = new BrowserWindow({
    width: 1280,
    height: 800,
    useContentSize: true,
    center: true,
    title: "DesktopBench – electron",
    webPreferences: {
      preload: path.join(__dirname, "preload.cjs"),
      backgroundThrottling: false,
      additionalArguments: [`--bench-start=${startTs}`],
    },
  });
  win.setMenu(null);
  win.loadFile(path.join(__dirname, "dist", "index.html"));
});
app.on("window-all-closed", () => app.quit());
