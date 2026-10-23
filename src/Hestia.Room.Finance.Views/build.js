// Builds every view into dist/, one self-contained HTML each: vite-plugin-singlefile handles a single entry per
// build, so the views are built one after the other.
import { rm } from "node:fs/promises";
import { build } from "vite";

export const views = ["month-overview.html", "cash-flow-forecast.html", "subscriptions.html"];

await rm("dist", { recursive: true, force: true });
for (const view of views) {
  await build({ logLevel: "warn", build: { rollupOptions: { input: `views/${view}` } } });
}
