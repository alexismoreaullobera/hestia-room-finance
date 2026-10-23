// Shared by every view of the finance room (injected by the server in place of its marker, at the top of the
// view module). The ext-apps App SDK is loaded from unpkg: this domain is declared in the resource CSP.
// TODO(#2): bundle the SDK into the views before production (no third-party code at runtime, no external domain).
import { App } from "https://unpkg.com/@modelcontextprotocol/ext-apps@2.0.3/app-with-deps";

/**
 * Connects the view to its host and renders every tool result.
 * @param {{ name: string, render: (data: object) => void }} view
 */
async function startView(view) {
  const status = document.getElementById("status");
  const app = new App({ name: `Hestia Finance - ${view.name}`, version: "0.2.0" });

  // Handlers must be registered before connecting, so an early result is not missed.
  app.ontoolresult = (result) => {
    if (result.isError || !result.structuredContent) {
      status.textContent = "Données indisponibles.";
      return;
    }
    status.textContent = "";
    view.render(result.structuredContent);
  };
  app.onerror = console.error;

  await app.connect();
}
