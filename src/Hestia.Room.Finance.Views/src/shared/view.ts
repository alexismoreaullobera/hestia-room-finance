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

/**
 * The outcome of an input call: true once its result is shown; the message of a refusal or of a failure (also in the
 * status of the view), for the form to show where the user looks; false when the call is not answered (overtaken by a
 * newer result, or another call pending).
 */
export type CallResult = boolean | string;

/** What a rendering may use besides its data. */
export interface ViewContext {
  /** The width of the content, to draw at the real size. */
  width: number;
  /**
   * Calls an input tool, whose result is rendered like any other: an input tool returns the data of the view that
   * calls it (the output schema of its view tool), recalculated by the server. Same guard as the month navigation:
   * the buttons are disabled meanwhile, and an answer arriving after a newer one is ignored. See CallResult.
   */
  call: (tool: string, args: Readonly<Record<string, unknown>>) => Promise<CallResult>;
}

export interface ViewDefinition<T> {
  /** French name of the view: the title of the page and the label of its header. */
  name: string;
  /** The view tool, called again with another month by the previous and next buttons. */
  tool: string;
  /** Renders the structured content of a result. */
  render: (data: T, content: HTMLElement, context: ViewContext) => void;
  /**
   * The French message of a refusal of one of its input tools, from the text of the tool error (written in English by
   * the server), or null for the general message. The checks of a view only see what it shows: some refusals come from
   * the server alone, such as a name taken by a line ended before the month.
   */
  explain?: (refusal: string) => string | null;
}

/** What a view shows while the user is in the middle of an input: a form, or a row waiting for a confirmation. */
const IN_PROGRESS = "form, .confirming";

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
  // Every request and every result of the agent takes a new number: an answer that arrives after a newer one is ignored.
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
    view.render(data, content, { width: renderedWidth, call });
  }

  /** Renders a result; true if it was, false if it was an error (the status then says unavailable). */
  function show(result: ToolResult, unavailable: string): boolean {
    if (result.isError || !result.structuredContent) {
      setStatus(unavailable, true);
      return false;
    }
    // The shape is the output schema of the tool (checked on the server side by the fixtures test).
    const data = result.structuredContent as unknown as T;
    shown = data.month;
    lastData = data;
    monthTitle.textContent = formatMonth(data.month);
    setStatus("");
    render(data);
    return true;
  }

  // Drawn again when the width changes (window resized, browser zoom): only the width, since a new rendering
  // changes the height, which would otherwise trigger another rendering. Never while the user is in the middle of an
  // input (a form open, or a deletion to confirm): a rendering rebuilds the content and would close it. Opening one
  // can itself change the width (a taller content shows a scrollbar), which closed a confirmation at once. The next
  // change of width redraws.
  new ResizeObserver(() => {
    if (lastData !== null && content.clientWidth !== renderedWidth && content.querySelector(IN_PROGRESS) === null) {
      render(lastData);
    }
  }).observe(content);

  interface Messages {
    pending: string;
    /** The tool answered with an error. */
    refused: string;
    /** The call itself failed (host, network). */
    failed: string;
    /** Whether a refusal is explained by the view (an input), rather than said in general (a navigation). */
    explained: boolean;
  }

  /** The text of a tool error: what the server says of the refusal. */
  const refusalText = (result: ToolResult) =>
    result.content.flatMap(block => (block.type === "text" ? [block.text] : [])).join(" ");

  /** A call of a tool whose result is the view data: the month navigation and the inputs go through it. */
  async function request(tool: string, args: Readonly<Record<string, unknown>>, messages: Messages): Promise<CallResult> {
    const current = ++lastRequest;
    loading = true;
    setNavigation();
    setStatus(messages.pending);
    try {
      const result = await app.callServerTool({ name: tool, arguments: { ...args } });
      if (current !== lastRequest) return false;
      if (result.isError || !result.structuredContent) {
        const message = (messages.explained ? view.explain?.(refusalText(result)) : null) ?? messages.refused;
        setStatus(message, true);
        return message;
      }
      return show(result, messages.refused);
    } catch (error) {
      console.error(error);
      if (current !== lastRequest) return false;
      setStatus(messages.failed, true);
      return messages.failed;
    } finally {
      if (current === lastRequest) {
        loading = false;
        setNavigation();
      }
    }
  }

  const navigation: Messages = {
    pending: "Chargement…",
    refused: "Données indisponibles pour ce mois.",
    failed: "Impossible de charger ce mois.",
    explained: false,
  };
  const input: Messages = {
    pending: "Enregistrement…",
    refused: "Valeur refusée : vérifiez la saisie.",
    failed: "Enregistrement impossible, réessayez.",
    explained: true,
  };

  function call(tool: string, args: Readonly<Record<string, unknown>>): Promise<CallResult> {
    // One call at a time, navigation included: the form waits, it is not queued.
    return loading ? Promise.resolve(false) : request(tool, args, input);
  }

  const navigate = (delta: number) => {
    if (shown !== null && !loading) void request(view.tool, { month: shiftMonth(shown, delta) }, navigation);
  };
  previous.addEventListener("click", () => { navigate(-1); });
  next.addEventListener("click", () => { navigate(1); });
  setNavigation();

  // Handlers must be registered before connecting, so an early result is not missed. A result of the agent is the
  // newest intent: it wins over a navigation still loading, whose answer will be ignored.
  app.addEventListener("toolresult", (result) => {
    lastRequest++;
    loading = false;
    show(result, navigation.refused);
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
