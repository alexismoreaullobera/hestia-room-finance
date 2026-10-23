// From vitest/config: the Vite configuration plus the test section.
import { defineConfig } from "vitest/config";
import { viteSingleFile } from "vite-plugin-singlefile";

// One build per view (see build.js): vite-plugin-singlefile inlines the scripts, the styles and the SDK into a
// single self-contained HTML document, as MCP Apps expects a ui:// resource to be.
export default defineConfig({
  root: "views",
  // No .env file read: nothing from the environment may end up inlined in a view.
  envDir: false,
  plugins: [viteSingleFile()],
  build: {
    outDir: "../dist",
    // build.js empties dist/ once, then builds the views one after the other into it.
    emptyOutDir: false,
    // No source map: the view is served as is to the host.
    sourcemap: false,
    // No module preload polyfill: a single-file view preloads nothing (the polyfill is dead code holding a fetch).
    modulePreload: { polyfill: false },
  },
  test: {
    // The tests live next to views/, not in it (root above is for the builds).
    root: ".",
    environment: "happy-dom",
    include: ["test/**/*.test.ts"],
  },
});
