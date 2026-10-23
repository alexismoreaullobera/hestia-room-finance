// Shared by every view of the finance room (injected by the server in place of its marker, at the top of the
// view module). The ext-apps App SDK is loaded from unpkg: this domain is declared in the resource CSP.
// TODO(#17): bundle the SDK into the views, before T8 (no third-party code at runtime, no external domain).
import { App } from "https://unpkg.com/@modelcontextprotocol/ext-apps@2.0.3/app-with-deps";

// Formats: French, as the text summaries. The amounts come already rounded to the cent by the server.
const euros = new Intl.NumberFormat("fr-FR", { style: "currency", currency: "EUR" });
const monthNames = new Intl.DateTimeFormat("fr-FR", { month: "long", year: "numeric", timeZone: "UTC" });

/** 1234.5 -> "1 234,50 €". */
function formatEuros(amount) {
  return euros.format(amount);
}

/** "2026-10" -> "Octobre 2026". */
function formatMonth(month) {
  const [year, monthNumber] = month.split("-").map(Number);
  const name = monthNames.format(new Date(Date.UTC(year, monthNumber - 1, 1)));
  return name.charAt(0).toUpperCase() + name.slice(1);
}

/** "2026-10-05" -> "05/10". */
function formatDay(date) {
  const [, month, day] = date.split("-");
  return `${day}/${month}`;
}

/** "2026-10-05" -> "05/10/2026". */
function formatDate(date) {
  const [year, month, day] = date.split("-");
  return `${day}/${month}/${year}`;
}

/** "2026-12", +1 -> "2027-01": the month before or after, as YYYY-MM. */
function shiftMonth(month, delta) {
  const [year, monthNumber] = month.split("-").map(Number);
  const index = year * 12 + (monthNumber - 1) + delta;
  return `${String(Math.floor(index / 12)).padStart(4, "0")}-${String((index % 12) + 1).padStart(2, "0")}`;
}

/** A translation from a table, or the value itself: own keys only, never an inherited one such as "constructor". */
function translate(table, value) {
  return Object.hasOwn(table, value) ? table[value] : value;
}

// The only attributes the views may set: presentation and accessibility, never a URL (href, src), a style or an
// event handler (on*), so that no name typed by the user can ever become code or a link.
const SAFE_ATTRIBUTES = new Set([
  "role", "aria-label", "id", "class", "viewBox", "x", "y", "x1", "x2", "y1", "y2", "cx", "cy", "r",
  "width", "height", "points", "text-anchor", "clip-path",
]);

function setAttributes(node, attributes) {
  for (const [name, value] of Object.entries(attributes ?? {})) {
    if (!SAFE_ATTRIBUTES.has(name)) throw new Error(`Attribute not allowed in a view: ${name}`);
    node.setAttribute(name, String(value));
  }
}

/**
 * Creates an element. Text is set with textContent only (strings among the children become text nodes), never as
 * HTML: the data comes from tools and is untrusted (XSS, prompt injection through a name typed by the user).
 * @param {string} tag
 * @param {{ className?: string, text?: string, attributes?: Record<string, string> }} [options]
 * @param {...(Node|string)} children
 */
function element(tag, { className, text, attributes } = {}, ...children) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  setAttributes(node, attributes);
  node.append(...children);
  return node;
}

/** Creates an SVG element, with the same rules as element(): safe attributes only, strings as text nodes. */
function svg(tag, attributes = {}, ...children) {
  const node = document.createElementNS("http://www.w3.org/2000/svg", tag);
  setAttributes(node, attributes);
  node.append(...children);
  return node;
}

/** An amount cell or span, red when negative. */
function amount(value, tag = "span") {
  return element(tag, { className: value < 0 ? "amount negative" : "amount", text: formatEuros(value) });
}

/** A key figure: its title, its value (a node or a text) and an optional detail. Laid out by .cards. */
function card(title, value, detail) {
  return element("div", { className: "card" },
    element("div", { className: "title", text: title }),
    element("div", { className: "big" }, value),
    ...(detail === undefined ? [] : [element("div", { className: "detail" }, detail)]));
}

/** Follows the theme of the host (light or dark) when it gives one; the system setting otherwise. */
function applyHostTheme(context) {
  const theme = context?.theme;
  if (theme === "light" || theme === "dark") {
    document.documentElement.dataset.theme = theme;
  }
}

/**
 * Connects the view to its host and renders every result of its tool, including the months asked through the
 * previous and next buttons: the view calls its own tool with the month, without going through the agent.
 * render(data, content, width) receives the width of the content, so that a view can draw at the real size.
 * The page must contain #previous, #month, #next, #status and #content.
 * @param {{ name: string, tool: string, render: (data: object, content: HTMLElement, width: number) => void }} view
 */
async function startView(view) {
  const previous = document.getElementById("previous");
  const next = document.getElementById("next");
  const monthTitle = document.getElementById("month");
  const status = document.getElementById("status");
  const content = document.getElementById("content");
  const app = new App({ name: `Hestia Finance - ${view.name}`, version: "0.2.0" });
  let shown = null;
  let lastData = null;
  let renderedWidth = 0;

  function setStatus(text, isError = false) {
    status.textContent = text;
    status.classList.toggle("error", isError);
    status.hidden = text === "";
  }

  function setNavigation(enabled) {
    previous.disabled = !enabled || shown === null;
    next.disabled = !enabled || shown === null;
  }

  function show(result) {
    if (result.isError || !result.structuredContent) {
      setStatus("Données indisponibles pour ce mois.", true);
      return;
    }
    const data = result.structuredContent;
    shown = data.month;
    lastData = data;
    monthTitle.textContent = formatMonth(data.month);
    setStatus("");
    render();
  }

  function render() {
    content.replaceChildren();
    // Visible before measuring: a hidden element has no width.
    content.hidden = false;
    renderedWidth = content.clientWidth;
    view.render(lastData, content, renderedWidth);
  }

  // Drawn again when the width changes (window resized, browser zoom): only the width, since a new rendering
  // changes the height, which would otherwise trigger another rendering.
  new ResizeObserver(() => {
    if (lastData !== null && content.clientWidth !== renderedWidth) {
      render();
    }
  }).observe(content);

  async function goTo(month) {
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

  previous.addEventListener("click", () => goTo(shiftMonth(shown, -1)));
  next.addEventListener("click", () => goTo(shiftMonth(shown, +1)));
  setNavigation(false);

  // Handlers must be registered before connecting, so an early result is not missed.
  app.ontoolresult = (result) => {
    show(result);
    setNavigation(true);
  };
  app.onhostcontextchanged = applyHostTheme;
  app.onerror = console.error;

  try {
    await app.connect();
  } catch (error) {
    console.error(error);
    setStatus("Connexion à l'hôte impossible.", true);
    return;
  }
  applyHostTheme(app.getHostContext?.());
}
