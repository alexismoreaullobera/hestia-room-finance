// Shared by every view of the finance room (injected by the server in place of its marker, at the top of the
// view module). The ext-apps App SDK is loaded from unpkg: this domain is declared in the resource CSP.
// TODO(#2): bundle the SDK into the views before production (no third-party code at runtime, no external domain).
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

/**
 * Creates an element. Text is set with textContent only, never as HTML: the data comes from tools and is
 * untrusted (XSS, prompt injection through a name typed by the user).
 * @param {string} tag
 * @param {{ className?: string, text?: string, attributes?: Record<string, string> }} [options]
 * @param {...Node} children
 */
function element(tag, { className, text, attributes } = {}, ...children) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  for (const [name, value] of Object.entries(attributes ?? {})) node.setAttribute(name, value);
  node.append(...children);
  return node;
}

/** An amount cell or span, red when negative. */
function amount(value, tag = "span") {
  return element(tag, { className: value < 0 ? "amount negative" : "amount", text: formatEuros(value) });
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
 * The page must contain #previous, #month, #next, #status and #content.
 * @param {{ name: string, tool: string, render: (data: object, content: HTMLElement) => void }} view
 */
async function startView(view) {
  const previous = document.getElementById("previous");
  const next = document.getElementById("next");
  const monthTitle = document.getElementById("month");
  const status = document.getElementById("status");
  const content = document.getElementById("content");
  const app = new App({ name: `Hestia Finance - ${view.name}`, version: "0.2.0" });
  let shown = null;

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
    monthTitle.textContent = formatMonth(data.month);
    setStatus("");
    content.replaceChildren();
    view.render(data, content);
    content.hidden = false;
  }

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

  await app.connect();
  applyHostTheme(app.getHostContext?.());
}
