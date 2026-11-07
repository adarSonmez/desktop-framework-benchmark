import { invoke } from "@tauri-apps/api/core";
import { mount, type Bridge } from "@ui/mount";

interface Cfg { startTs: number; benchOut: string | null; idleMs: number }

const decoder = new TextDecoder();
const cfg = await invoke<Cfg>("config");
const bridge: Bridge = {
  variant: "tauri",
  ...cfg,
  loadItems: async () => {
    const buf = await invoke<ArrayBuffer>("load_text");
    return buf.byteLength === 0 ? null : JSON.parse(decoder.decode(buf));
  },
  saveItems: (items) => invoke("save_text", { text: JSON.stringify(items) }),
  ping: (n) => invoke<number>("ping", { n }),
  writeFile: (path, content) => invoke("write_file", { path, content }),
  exit: () => invoke("exit"),
};
mount(bridge);
