import { vi } from "vitest";
import { element } from "../src/shared/dom";
import type { HostApp } from "../src/shared/view";

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

/** Gives an element a width: happy-dom does no layout, every clientWidth is 0. */
export function setWidth(node: HTMLElement, width: number): void {
  Object.defineProperty(node, "clientWidth", { configurable: true, get: () => width });
}

export interface ToolResultLike {
  isError?: boolean;
  structuredContent?: unknown;
}

/** A fake App SDK: the test sends the tool results and decides what the host answers. */
export function fakeApp() {
  const listeners = new Map<string, ((params: unknown) => void)[]>();
  const emit = (event: string, params: unknown) => { for (const listener of listeners.get(event) ?? []) listener(params); };
  return {
    addEventListener: (event: string, handler: (params: unknown) => void) => {
      listeners.set(event, [...(listeners.get(event) ?? []), handler]);
    },
    connect: vi.fn(async () => {}),
    callServerTool: vi.fn<(params: { name: string; arguments: { month: string } }) => Promise<ToolResultLike>>(),
    getHostContext: vi.fn<() => { theme?: string } | undefined>(() => ({ theme: "dark" })),
    /** The host sends a tool result, as after a tools/call of the agent. */
    sendResult: (result: ToolResultLike) => { emit("toolresult", result); },
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
  constructor(readonly callback: () => void) {
    ManualResizeObserver.last = this;
  }
  observe(): void {}
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
