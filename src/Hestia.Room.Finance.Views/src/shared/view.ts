// The life of a view in its host: connection, tool results, month navigation, host theme. The App SDK comes from
// the npm package (exact version, content hash in the lock file) and is inlined into each view by the build: no
// third-party code is loaded at runtime.
import { App } from "@modelcontextprotocol/ext-apps";
import { formatMonth, shiftMonth } from "./format";
import "./view.css";

/** Follows the theme of the host (light or dark) when it gives one; the system setting otherwise. */
export function applyHostTheme(context: { theme?: string } | undefined): void {
  const theme = context?.theme;
  if (theme === "light" || theme === "dark") {
    document.documentElement.dataset["theme"] = theme;
  }
}

/** The part of the App SDK a view uses: the tests give a fake one, typed on the same SDK signatures. */
export type HostApp = Pick<App, "addEventListener" | "connect" | "callServerTool" | "getHostContext">;

/** A tool result, as the host sends it after a call of the agent or as callServerTool returns it. */
export type ToolResult = Awaited<ReturnType<HostApp["callServerTool"]>>;

export interface ViewDefinition<T> {
  /** French name of the view: the title of the page and the label of its header. */
  name: string;
  /** The view tool, called again with another month by the previous and next buttons. */
  tool: string;
  /** Renders the structured content of a result; width is the width of the content, to draw at the real size. */
  render: (data: T, content: HTMLElement, width: number) => void;
}

/** The ids every view page has (views/*.html), checked on the built views by a C# test. */
export const PAGE_IDS = ["label", "previous", "month", "next", "status", "content"] as const;

function required(id: (typeof PAGE_IDS)[number]): HTMLElement {
  const node = document.getElementById(id);
  if (node === null) throw new Error(`A view must contain #${id}.`);
  return node;
}

/**
 * Connects the view to its host and renders every result of its tool, including the months asked through the
 * previous and next buttons: the view calls its own tool with the month, without going through the agent.
 */
export async function startView<T extends { month: string }>(
  view: ViewDefinition<T>, app: HostApp = new App({ name: `Hestia Finance - ${view.name}`, version: "0.3.0" })): Promise<void> {
  const previous = required("previous") as HTMLButtonElement;
  const next = required("next") as HTMLButtonElement;
  const monthTitle = required("month");
  const status = required("status");
  const content = required("content");
  document.title = view.name;
  required("label").textContent = view.name;

  let shown: string | null = null;
  let lastData: T | null = null;
  let renderedWidth = 0;
  // Every result shown takes a new number: a navigation answer that arrives after a newer result is ignored.
  let lastRequest = 0;
  let loading = false;

  function setStatus(text: string, isError = false): void {
    status.textContent = text;
    status.classList.toggle("error", isError);
    status.hidden = text === "";
  }

  function setNavigation(): void {
    previous.disabled = loading || shown === null;
    next.disabled = loading || shown === null;
  }

  function render(data: T): void {
    content.replaceChildren();
    // Visible before measuring: a hidden element has no width.
    content.hidden = false;
    renderedWidth = content.clientWidth;
    view.render(data, content, renderedWidth);
  }

  function show(result: ToolResult): void {
    if (result.isError || !result.structuredContent) {
      setStatus("Données indisponibles pour ce mois.", true);
      return;
    }
    // The shape is the output schema of the tool (checked on the server side by the fixtures test).
    const data = result.structuredContent as unknown as T;
    shown = data.month;
    lastData = data;
    monthTitle.textContent = formatMonth(data.month);
    setStatus("");
    render(data);
  }

  // Drawn again when the width changes (window resized, browser zoom): only the width, since a new rendering
  // changes the height, which would otherwise trigger another rendering.
  new ResizeObserver(() => {
    if (lastData !== null && content.clientWidth !== renderedWidth) {
      render(lastData);
    }
  }).observe(content);

  async function goTo(month: string): Promise<void> {
    const request = ++lastRequest;
    loading = true;
    setNavigation();
    setStatus("Chargement…");
    try {
      const result = await app.callServerTool({ name: view.tool, arguments: { month } });
      if (request === lastRequest) show(result);
    } catch (error) {
      console.error(error);
      if (request === lastRequest) setStatus("Impossible de charger ce mois.", true);
    } finally {
      if (request === lastRequest) {
        loading = false;
        setNavigation();
      }
    }
  }

  const navigate = (delta: number) => {
    if (shown !== null && !loading) void goTo(shiftMonth(shown, delta));
  };
  previous.addEventListener("click", () => { navigate(-1); });
  next.addEventListener("click", () => { navigate(1); });
  setNavigation();

  // Handlers must be registered before connecting, so an early result is not missed. A result of the agent is the
  // newest intent: it wins over a navigation still loading, whose answer will be ignored.
  app.addEventListener("toolresult", (result) => {
    lastRequest++;
    loading = false;
    show(result);
    setNavigation();
  });
  app.addEventListener("hostcontextchanged", applyHostTheme);

  try {
    await app.connect();
  } catch (error) {
    console.error(error);
    setStatus("Connexion à l'hôte impossible.", true);
    return;
  }
  applyHostTheme(app.getHostContext());
}
