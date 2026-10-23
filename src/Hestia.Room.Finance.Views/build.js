// Builds every view of views/ into dist/, one self-contained HTML each: vite-plugin-singlefile handles a single
// entry per build, so the views are built one after the other. Paths are relative to this file, not to the current
// directory: the script can be run from anywhere.
import { readdir, rm } from "node:fs/promises";
import { join } from "node:path";
import { build } from "vite";

const root = import.meta.dirname;
const views = (await readdir(join(root, "views"))).filter(file => file.endsWith(".html"));

await rm(join(root, "dist"), { recursive: true, force: true });
for (const view of views) {
  await build({
    configFile: join(root, "vite.config.ts"),
    logLevel: "warn",
    build: { rollupOptions: { input: join(root, "views", view) } },
  });
}
