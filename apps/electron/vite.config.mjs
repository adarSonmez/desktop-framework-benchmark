import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { fileURLToPath } from "node:url";

export default defineConfig({
  root: "renderer",
  base: "./",
  plugins: [react()],
  resolve: {
    alias: { "@ui": fileURLToPath(new URL("../../shared/web-ui/src", import.meta.url)) },
    dedupe: ["react", "react-dom"],
  },
  build: { outDir: "../dist", emptyOutDir: true },
});
