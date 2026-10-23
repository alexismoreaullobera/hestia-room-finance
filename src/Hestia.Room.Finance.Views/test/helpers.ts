import { vi } from "vitest";
import type { HostApp } from "../src/shared/view";
import { element } from "../src/shared/view";

/** The page skeleton every view has (see views/*.html), built with the kit itself. */
export function pageSkeleton(): HTMLElement {
  const content = element("main", { attributes: { id: "content" } });
  content.hidden = true;
  document.body.replaceChildren(
    element("button", { attributes: { id: "previous" } }),
    element("h1", {}, element("span", { attributes: { id: "month" } })),
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
  const app = {
    ontoolresult: undefined as ((result: ToolResultLike) => void) | undefined,
    onhostcontextchanged: undefined as ((context: { theme?: string }) => void) | undefined,
    connect: vi.fn(async () => {}),
    callServerTool: vi.fn<(params: { name: string; arguments: { month: string } }) => Promise<ToolResultLike>>(),
    getHostContext: vi.fn(() => ({ theme: "dark" }) as { theme?: string } | undefined),
    /** The host sends a tool result, as after a tools/call of the agent. */
    sendResult(result: ToolResultLike) {
      app.ontoolresult?.(result);
    },
  };
  return app;
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
export function plain(text: string | null | undefined): string {
  return (text ?? "").replace(/[  ]/g, " ");
}
