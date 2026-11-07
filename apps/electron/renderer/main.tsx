import { mount, type Bridge } from "@ui/mount";

declare global {
  interface Window {
    host: Omit<Bridge, "loadItems" | "saveItems"> & {
      loadText(): Promise<string | null>;
      saveText(text: string): Promise<void>;
    };
  }
}

const h = window.host;
const bridge: Bridge = {
  ...h,
  loadItems: async () => {
    const t = await h.loadText();
    return t == null ? null : JSON.parse(t);
  },
  saveItems: (items) => h.saveText(JSON.stringify(items)),
};
mount(bridge);
