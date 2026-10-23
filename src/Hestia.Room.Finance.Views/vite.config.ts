// From vitest/config: the Vite configuration plus the test section.
import { defineConfig } from "vitest/config";
import { viteSingleFile } from "vite-plugin-singlefile";

// One build per view (see build.js): vite-plugin-singlefile inlines the scripts, the styles and the SDK into a
// single self-contained HTML document, as MCP Apps expects a ui:// resource to be.
export default defineConfig({
  root: "views",
  plugins: [viteSingleFile()],
  build: {
    outDir: "../dist",
    emptyOutDir: false,
    // No source map: the view is served as is to the host.
    sourcemap: false,
  },
  test: {
    root: ".",
    environment: "happy-dom",
    include: ["test/**/*.test.ts"],
  },
});
