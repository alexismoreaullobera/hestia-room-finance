// Shared by every view of the finance room. The App SDK comes from the npm package (exact version, content hash in
// the lock file) and is inlined into each view by the build: no third-party code is loaded at runtime.
import { App } from "@modelcontextprotocol/ext-apps";
import "./view.css";

// Formats: French, as the text summaries. The amounts come already rounded to the cent by the server.
const euros = new Intl.NumberFormat("fr-FR", { style: "currency", currency: "EUR" });
const monthNames = new Intl.DateTimeFormat("fr-FR", { month: "long", year: "numeric", timeZone: "UTC" });

/** 1234.5 -> "1 234,50 €". */
export function formatEuros(amount: number): string {
  return euros.format(amount);
}

/** "2026-10" -> "Octobre 2026". */
export function formatMonth(month: string): string {
  const [year = 0, monthNumber = 1] = month.split("-").map(Number);
  // setUTCFullYear, not Date.UTC: Date.UTC reads the years 0 to 99 as 1900 to 1999.
  const date = new Date(0);
  date.setUTCFullYear(year, monthNumber - 1, 1);
  const name = monthNames.format(date);
  return name.charAt(0).toUpperCase() + name.slice(1);
}

/** "2026-10-05" -> "05/10". */
export function formatDay(date: string): string {
  const [, month, day] = date.split("-");
  return `${day}/${month}`;
}

/** "2026-10-05" -> "05/10/2026". */
export function formatDate(date: string): string {
  const [year, month, day] = date.split("-");
  return `${day}/${month}/${year}`;
}

/** "2026-12", +1 -> "2027-01": the month before or after, as YYYY-MM. */
export function shiftMonth(month: string, delta: number): string {
  const [year = 0, monthNumber = 1] = month.split("-").map(Number);
  const index = year * 12 + (monthNumber - 1) + delta;
  return `${String(Math.floor(index / 12)).padStart(4, "0")}-${String((index % 12) + 1).padStart(2, "0")}`;
}

/** A translation from a table, or the value itself: own keys only, never an inherited one such as "constructor". */
export function translate(table: Readonly<Record<string, string>>, value: string): string {
  return Object.hasOwn(table, value) ? (table[value] ?? value) : value;
}

// The only attributes the views may set: presentation and accessibility, never a URL (href, src), a style or an
// event handler (on*), so that no name typed by the user can ever become code or a link.
const SAFE_ATTRIBUTES: ReadonlySet<string> = new Set([
  "role", "aria-label", "id", "class", "viewBox", "x", "y", "x1", "x2", "y1", "y2", "cx", "cy", "r",
  "width", "height", "points", "text-anchor", "clip-path",
]);

export type Attributes = Readonly<Record<string, string | number>>;

function setAttributes(node: Element, attributes: Attributes | undefined): void {
  for (const [name, value] of Object.entries(attributes ?? {})) {
    if (!SAFE_ATTRIBUTES.has(name)) throw new Error(`Attribute not allowed in a view: ${name}`);
    node.setAttribute(name, String(value));
  }
}

export interface ElementOptions {
  className?: string;
  text?: string;
  attributes?: Attributes;
}

/**
 * Creates an element. Text is set with textContent only (strings among the children become text nodes), never as
 * HTML: the data comes from tools and is untrusted (XSS, prompt injection through a name typed by the user).
 */
export function element<K extends keyof HTMLElementTagNameMap>(
  tag: K, { className, text, attributes }: ElementOptions = {}, ...children: (Node | string)[]): HTMLElementTagNameMap[K] {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  setAttributes(node, attributes);
  node.append(...children);
  return node;
}

/** Creates an SVG element, with the same rules as element(): safe attributes only, strings as text nodes. */
export function svg<K extends keyof SVGElementTagNameMap>(
  tag: K, attributes: Attributes = {}, ...children: (Node | string)[]): SVGElementTagNameMap[K] {
  const node = document.createElementNS("http://www.w3.org/2000/svg", tag);
  setAttributes(node, attributes);
  node.append(...children);
  return node;
}

/** An amount cell or span, red when negative. */
export function amount(value: number, tag: "span" | "td" = "span"): HTMLElement {
  return element(tag, { className: value < 0 ? "amount negative" : "amount", text: formatEuros(value) });
}

/** A key figure: its title, its value (a node or a text) and an optional detail. Laid out by .cards. */
export function card(title: string, value: Node | string, detail?: Node | string): HTMLElement {
  return element("div", { className: "card" },
    element("div", { className: "title", text: title }),
    element("div", { className: "big" }, value),
    ...(detail === undefined ? [] : [element("div", { className: "detail" }, detail)]));
}

/** Follows the theme of the host (light or dark) when it gives one; the system setting otherwise. */
export function applyHostTheme(context: { theme?: string } | undefined): void {
  const theme = context?.theme;
  if (theme === "light" || theme === "dark") {
    document.documentElement.dataset["theme"] = theme;
  }
}

/** The part of the App SDK a view uses: the tests give a fake one. */
export type HostApp = Pick<App, "ontoolresult" | "onhostcontextchanged" | "connect" | "callServerTool" | "getHostContext">;

export interface ViewDefinition<T> {
  /** French name of the view, given to the host. */
  name: string;
  /** The view tool, called again with another month by the previous and next buttons. */
  tool: string;
  /** Renders the structured content of a result; width is the width of the content, to draw at the real size. */
  render: (data: T, content: HTMLElement, width: number) => void;
}

interface ToolResult {
  isError?: boolean | undefined;
  structuredContent?: unknown;
}

function required(id: string): HTMLElement {
  const node = document.getElementById(id);
  if (node === null) throw new Error(`A view must contain #${id}.`);
  return node;
}

/**
 * Connects the view to its host and renders every result of its tool, including the months asked through the
 * previous and next buttons: the view calls its own tool with the month, without going through the agent.
 * The page must contain #previous, #month, #next, #status and #content.
 */
export async function startView<T extends { month: string }>(
  view: ViewDefinition<T>, app: HostApp = new App({ name: `Hestia Finance - ${view.name}`, version: "0.3.0" })): Promise<void> {
  const previous = required("previous") as HTMLButtonElement;
  const next = required("next") as HTMLButtonElement;
  const monthTitle = required("month");
  const status = required("status");
  const content = required("content");
  let shown: string | null = null;
  let lastData: T | null = null;
  let renderedWidth = 0;

  function setStatus(text: string, isError = false): void {
    status.textContent = text;
    status.classList.toggle("error", isError);
    status.hidden = text === "";
  }

  function setNavigation(enabled: boolean): void {
    previous.disabled = !enabled || shown === null;
    next.disabled = !enabled || shown === null;
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
    const data = result.structuredContent as T;
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
    setNavigation(false);
    setStatus("Chargement…");
    try {
      show(await app.callServerTool({ name: view.tool, arguments: { month } }));
    } catch (error) {
      console.error(error);
      setStatus("Impossible de charger ce mois.", true);
    } finally {
      setNavigation(true);
    }
  }

  previous.addEventListener("click", () => { if (shown !== null) void goTo(shiftMonth(shown, -1)); });
  next.addEventListener("click", () => { if (shown !== null) void goTo(shiftMonth(shown, +1)); });
  setNavigation(false);

  // Handlers must be registered before connecting, so an early result is not missed.
  app.ontoolresult = (result) => {
    show(result);
    setNavigation(true);
  };
  app.onhostcontextchanged = applyHostTheme;

  try {
    await app.connect();
  } catch (error) {
    console.error(error);
    setStatus("Connexion à l'hôte impossible.", true);
    return;
  }
  applyHostTheme(app.getHostContext());
}
