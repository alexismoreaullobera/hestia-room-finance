import { vi } from "vitest";
import { element } from "../src/shared/dom";
import type { HostApp, ToolResult } from "../src/shared/view";

/** The page skeleton every view has (see views/*.html), built with the kit itself. */
export function pageSkeleton(): HTMLElement {
  const content = element("main", { attributes: { id: "content" } });
  content.hidden = true;
  document.body.replaceChildren(
    element("button", { attributes: { id: "previous" } }),
    element("h1", {}, element("span", { attributes: { id: "label" } }), element("span", { attributes: { id: "month" } })),
    element("button", { attributes: { id: "next" } }),
    element("p", { attributes: { id: "status" } }),
    content);
  return content;
}

/**
 * Gives an element a width: happy-dom does no layout, every clientWidth is 0. As in a browser, a hidden element has
 * no width: a view that measures before showing its content gets 0.
 */
export function setWidth(node: HTMLElement, width: number): void {
  Object.defineProperty(node, "clientWidth", { configurable: true, get: () => (node.hidden ? 0 : width) });
}

/** A tool result with no text content: what a view reads is its structured content or its error flag. */
export function toolResult(fields: Omit<ToolResult, "content"> = {}): ToolResult {
  return { content: [], ...fields };
}

/**
 * A fake App SDK: the test sends the tool results and decides what the host answers. The methods a view calls are
 * typed on the SDK signatures, so a change of the SDK breaks the tests at compile time.
 */
export function fakeApp() {
  const listeners = new Map<string, ((params: unknown) => void)[]>();
  const emit = (event: string, params: unknown) => { for (const listener of listeners.get(event) ?? []) listener(params); };
  return {
    addEventListener: (event: string, handler: (params: unknown) => void) => {
      listeners.set(event, [...(listeners.get(event) ?? []), handler]);
    },
    connect: vi.fn<HostApp["connect"]>(async () => {}),
    callServerTool: vi.fn<HostApp["callServerTool"]>(),
    getHostContext: vi.fn<HostApp["getHostContext"]>(() => ({ theme: "dark" })),
    /** The host sends a tool result, as after a tools/call of the agent. */
    sendResult: (result: ToolResult) => { emit("toolresult", result); },
    /** The host changes its context, such as its theme. */
    changeHostContext: (context: { theme?: string }) => { emit("hostcontextchanged", context); },
  };
}

export function asHostApp(app: ReturnType<typeof fakeApp>): HostApp {
  return app as unknown as HostApp;
}

/** A ResizeObserver whose callback the test triggers, since happy-dom does no layout. */
export class ManualResizeObserver {
  static last: ManualResizeObserver | undefined;
  readonly observed: Element[] = [];
  constructor(readonly callback: () => void) {
    ManualResizeObserver.last = this;
  }
  observe(target: Element): void {
    this.observed.push(target);
  }
  disconnect(): void {}
  /** As the browser would after a resize. */
  trigger(): void {
    this.callback();
  }
}

/** French formats use narrow and regular no-break spaces: normalized to plain spaces in the assertions. */
const NARROW_NO_BREAK_SPACE = String.fromCharCode(0x202f);
const NO_BREAK_SPACE = String.fromCharCode(0xa0);

export function plain(text: string | null | undefined): string {
  return (text ?? "").replaceAll(NARROW_NO_BREAK_SPACE, " ").replaceAll(NO_BREAK_SPACE, " ");
}
