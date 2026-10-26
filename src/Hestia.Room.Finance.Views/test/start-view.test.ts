import { afterEach, beforeEach, describe, expect, it, vi, type MockInstance } from "vitest";
import { startView, type ToolResult, type ViewContext, type ViewDefinition } from "../src/shared/view";
import { asHostApp, fakeApp, ManualResizeObserver, pageSkeleton, setWidth, toolResult } from "./helpers";

interface Data {
  month: string;
}

const result = (month: string) => toolResult({ structuredContent: { month } });
const byId = (id: string) => document.getElementById(id)!;
const button = (id: string) => byId(id) as HTMLButtonElement;
/** Lets the pending promise callbacks run: what a late answer would do has been done. */
const flush = () => new Promise(resolve => setTimeout(resolve, 0));

/** An answer of the host that the test gives when it wants. */
function deferred() {
  let resolve!: (value: ToolResult) => void;
  const promise = new Promise<ToolResult>(r => { resolve = r; });
  return { promise, resolve };
}

describe("startView", () => {
  let content: HTMLElement;
  let render: ReturnType<typeof vi.fn<(data: Data, content: HTMLElement, context: ViewContext) => void>>;
  let view: ViewDefinition<Data>;
  let consoleError: MockInstance<typeof console.error>;

  beforeEach(() => {
    ManualResizeObserver.last = undefined;
    vi.stubGlobal("ResizeObserver", ManualResizeObserver);
    consoleError = vi.spyOn(console, "error").mockImplementation(() => {});
    content = pageSkeleton();
    render = vi.fn<(data: Data, content: HTMLElement, context: ViewContext) => void>();
    view = { name: "Test", tool: "show_test", render };
  });

  afterEach(() => {
    // A test that expects an error checks it, then clears the spy: any other error is a failure.
    expect(consoleError).not.toHaveBeenCalled();
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
    delete document.documentElement.dataset["theme"];
  });

  /** Checks the single error logged by the view, so that afterEach does not count it. */
  function expectOneLoggedError(): void {
    expect(consoleError).toHaveBeenCalledOnce();
    consoleError.mockClear();
  }

  it("connects, follows the host theme and keeps the buttons disabled until the first result", async () => {
    const app = fakeApp();

    await startView(view, asHostApp(app));

    expect(app.connect).toHaveBeenCalledOnce();
    expect(document.documentElement.dataset["theme"]).toBe("dark");
    expect(document.title).toBe("Test");
    expect(byId("label").textContent).toBe("Test");
    expect(button("previous").disabled).toBe(true);
    expect(button("next").disabled).toBe(true);
    expect(ManualResizeObserver.last?.observed).toEqual([content]);
    expect(render).not.toHaveBeenCalled();
  });

  it("renders a result of the agent at the width of the content, measured once visible", async () => {
    const app = fakeApp();
    await startView(view, asHostApp(app));
    // setWidth gives 0 while the content is hidden, as a browser does.
    setWidth(content, 640);

    app.sendResult(result("2026-10"));

    expect(byId("month").textContent).toBe("Octobre 2026");
    expect(render).toHaveBeenCalledWith({ month: "2026-10" }, content, expect.objectContaining({ width: 640 }));
    expect(content.hidden).toBe(false);
    expect(byId("status").hidden).toBe(true);
    expect(button("previous").disabled).toBe(false);
  });

  it("renders a result sent while connecting", async () => {
    // The host may send the result of the agent as soon as the view is connected, before connect() returns.
    const app = fakeApp();
    app.connect.mockImplementation(() => {
      app.sendResult(result("2026-10"));
      return Promise.resolve();
    });

    await startView(view, asHostApp(app));

    expect(render).toHaveBeenCalledOnce();
    expect(byId("month").textContent).toBe("Octobre 2026");
  });

  it("changes month through its own tool, disabling the buttons while loading", async () => {
    const app = fakeApp();
    const answer = deferred();
    app.callServerTool.mockReturnValue(answer.promise);
    await startView(view, asHostApp(app));
    app.sendResult(result("2026-10"));

    button("previous").click();

    expect(app.callServerTool).toHaveBeenCalledWith({ name: "show_test", arguments: { month: "2026-09" } });
    expect(byId("status").textContent).toBe("Chargement…");
    expect(button("previous").disabled).toBe(true);
    expect(button("next").disabled).toBe(true);

    answer.resolve(result("2026-09"));
    await vi.waitFor(() => { expect(byId("month").textContent).toBe("Septembre 2026"); });
    expect(button("next").disabled).toBe(false);
    expect(byId("status").hidden).toBe(true);
  });

  it("makes a single call for two clicks while loading", async () => {
    // A disabled button gets no click in a browser; the guard of navigate does not rely on it.
    const app = fakeApp();
    app.callServerTool.mockReturnValue(deferred().promise);
    await startView(view, asHostApp(app));
    app.sendResult(result("2026-10"));

    button("next").click();
    button("next").disabled = false;
    button("next").click();

    expect(app.callServerTool).toHaveBeenCalledOnce();
  });

  it("keeps the month shown when the tool answers with an error, until a month loads", async () => {
    const app = fakeApp();
    app.callServerTool.mockResolvedValueOnce(toolResult({ isError: true })).mockResolvedValueOnce(result("2026-11"));
    await startView(view, asHostApp(app));
    app.sendResult(result("2026-10"));

    button("previous").click();

    await vi.waitFor(() => { expect(byId("status").textContent).toBe("Données indisponibles pour ce mois."); });
    expect(byId("status").classList.contains("error")).toBe(true);
    expect(byId("month").textContent).toBe("Octobre 2026");
    expect(render).toHaveBeenCalledTimes(1);
    expect(button("previous").disabled).toBe(false);

    button("next").click();

    await vi.waitFor(() => { expect(byId("month").textContent).toBe("Novembre 2026"); });
    expect(byId("status").hidden).toBe(true);
    expect(byId("status").classList.contains("error")).toBe(false);
  });

  it("says the data is unavailable for a result without structured content", async () => {
    const app = fakeApp();
    await startView(view, asHostApp(app));
    app.sendResult(result("2026-10"));

    app.sendResult(toolResult());

    expect(byId("status").textContent).toBe("Données indisponibles pour ce mois.");
    expect(byId("month").textContent).toBe("Octobre 2026");
    expect(render).toHaveBeenCalledOnce();
  });

  it("keeps the buttons disabled when the first result is an error", async () => {
    const app = fakeApp();
    await startView(view, asHostApp(app));

    app.sendResult(toolResult({ isError: true }));

    expect(byId("status").textContent).toBe("Données indisponibles pour ce mois.");
    expect(byId("status").classList.contains("error")).toBe(true);
    expect(content.hidden).toBe(true);
    expect(button("previous").disabled).toBe(true);
    expect(button("next").disabled).toBe(true);
    expect(render).not.toHaveBeenCalled();
  });

  it("says so when the call fails, and enables the buttons again", async () => {
    const app = fakeApp();
    app.callServerTool.mockRejectedValue(new Error("network"));
    await startView(view, asHostApp(app));
    app.sendResult(result("2026-10"));

    button("next").click();

    await vi.waitFor(() => { expect(byId("status").textContent).toBe("Impossible de charger ce mois."); });
    expect(byId("month").textContent).toBe("Octobre 2026");
    expect(button("next").disabled).toBe(false);
    expectOneLoggedError();
  });

  it("lets a result of the agent win over a navigation still loading, and ignores its late answer", async () => {
    const app = fakeApp();
    const answer = deferred();
    app.callServerTool.mockReturnValue(answer.promise);
    await startView(view, asHostApp(app));
    app.sendResult(result("2026-10"));
    button("next").click();

    app.sendResult(result("2027-01"));
    expect(byId("month").textContent).toBe("Janvier 2027");
    expect(button("next").disabled).toBe(false);

    answer.resolve(result("2026-11"));
    await flush();
    expect(byId("month").textContent).toBe("Janvier 2027");
    expect(render).toHaveBeenCalledTimes(2);
  });

  it.each(["before", "after"])("ignores a stale answer arriving %s the newer one", async (order) => {
    // A navigation, a result of the agent, a second navigation: only the answer of the second one is shown, and the
    // stale answer neither shows its month nor enables the buttons while the second one is loading.
    const app = fakeApp();
    const stale = deferred();
    const newer = deferred();
    app.callServerTool.mockReturnValueOnce(stale.promise).mockReturnValueOnce(newer.promise);
    await startView(view, asHostApp(app));
    app.sendResult(result("2026-10"));
    button("next").click();
    app.sendResult(result("2027-01"));
    button("next").click();

    if (order === "before") {
      stale.resolve(result("2026-11"));
      await flush();
      expect(byId("month").textContent).toBe("Janvier 2027");
      expect(button("next").disabled).toBe(true);
      newer.resolve(result("2027-02"));
    } else {
      newer.resolve(result("2027-02"));
      await flush();
      stale.resolve(result("2026-11"));
    }

    await flush();
    expect(app.callServerTool).toHaveBeenLastCalledWith({ name: "show_test", arguments: { month: "2027-02" } });
    expect(byId("month").textContent).toBe("Février 2027");
    expect(button("next").disabled).toBe(false);
    expect(render).toHaveBeenCalledTimes(3);
  });

  it("says so when the connection to the host fails", async () => {
    const app = fakeApp();
    app.connect.mockRejectedValue(new Error("no host"));

    await startView(view, asHostApp(app));

    expect(byId("status").textContent).toBe("Connexion à l'hôte impossible.");
    expect(byId("status").classList.contains("error")).toBe(true);
    expectOneLoggedError();
  });

  it("follows a theme change of the host", async () => {
    const app = fakeApp();
    await startView(view, asHostApp(app));

    app.changeHostContext({ theme: "light" });

    expect(document.documentElement.dataset["theme"]).toBe("light");
  });

  it("renders again when the width changes, never when only the height does", async () => {
    const app = fakeApp();
    await startView(view, asHostApp(app));
    setWidth(content, 640);
    app.sendResult(result("2026-10"));

    ManualResizeObserver.last!.trigger();
    expect(render).toHaveBeenCalledTimes(1);

    setWidth(content, 480);
    ManualResizeObserver.last!.trigger();
    expect(render).toHaveBeenCalledTimes(2);
    expect(render).toHaveBeenLastCalledWith({ month: "2026-10" }, content, expect.objectContaining({ width: 480 }));
  });

  describe("call, for the input tools", () => {
    /** The call given to the last rendering, as a view uses it from a form. */
    const lastCall = () => render.mock.lastCall![2].call;

    it("calls the tool, disabling the buttons meanwhile, then renders its result", async () => {
      const app = fakeApp();
      const answer = deferred();
      app.callServerTool.mockReturnValue(answer.promise);
      await startView(view, asHostApp(app));
      app.sendResult(result("2026-10"));

      const saved = lastCall()("set_test", { month: "2026-10", value: 12.5 });

      expect(app.callServerTool).toHaveBeenCalledWith({ name: "set_test", arguments: { month: "2026-10", value: 12.5 } });
      expect(byId("status").textContent).toBe("Enregistrement…");
      expect(button("previous").disabled).toBe(true);
      answer.resolve(result("2026-10"));
      expect(await saved).toBe(true);
      expect(render).toHaveBeenCalledTimes(2);
      expect(byId("status").hidden).toBe(true);
      expect(button("previous").disabled).toBe(false);
    });

    it("says the value was refused when the tool answers with an error, keeping the rendering", async () => {
      const app = fakeApp();
      app.callServerTool.mockResolvedValue(toolResult({ isError: true }));
      await startView(view, asHostApp(app));
      app.sendResult(result("2026-10"));

      // The message given back too, for the form to show it where the user looks.
      expect(await lastCall()("set_test", {})).toBe("Valeur refusée : vérifiez la saisie.");

      expect(byId("status").textContent).toBe("Valeur refusée : vérifiez la saisie.");
      expect(byId("status").classList.contains("error")).toBe(true);
      expect(render).toHaveBeenCalledOnce();
      expect(button("next").disabled).toBe(false);
    });

    it("says a refusal as the view explains it, from the text of the tool error", async () => {
      const app = fakeApp();
      app.callServerTool.mockResolvedValue(toolResult({ isError: true, content: [{ type: "text", text: "Error: name taken." }] }));
      view = { ...view, explain: refusal => (refusal.includes("name taken") ? "Nom déjà pris." : null) };
      await startView(view, asHostApp(app));
      app.sendResult(result("2026-10"));

      expect(await lastCall()("set_test", {})).toBe("Nom déjà pris.");

      expect(byId("status").textContent).toBe("Nom déjà pris.");
      expect(byId("status").classList.contains("error")).toBe(true);
    });

    it("keeps the general message for a refusal the view does not explain, and for a navigation", async () => {
      const app = fakeApp();
      app.callServerTool.mockResolvedValue(toolResult({ isError: true, content: [{ type: "text", text: "Error: something else." }] }));
      view = { ...view, explain: refusal => (refusal.includes("name taken") ? "Nom déjà pris." : null) };
      await startView(view, asHostApp(app));
      app.sendResult(result("2026-10"));

      expect(await lastCall()("set_test", {})).toBe("Valeur refusée : vérifiez la saisie.");

      app.callServerTool.mockResolvedValue(toolResult({ isError: true, content: [{ type: "text", text: "Error: name taken." }] }));
      button("next").click();
      await vi.waitFor(() => { expect(byId("status").textContent).toBe("Données indisponibles pour ce mois."); });
    });

    it("says the save failed when the call fails", async () => {
      const app = fakeApp();
      app.callServerTool.mockRejectedValue(new Error("network"));
      await startView(view, asHostApp(app));
      app.sendResult(result("2026-10"));

      expect(await lastCall()("set_test", {})).toBe("Enregistrement impossible, réessayez.");

      expect(byId("status").textContent).toBe("Enregistrement impossible, réessayez.");
      expect(button("next").disabled).toBe(false);
      expectOneLoggedError();
    });

    it("makes no call while a navigation is loading", async () => {
      const app = fakeApp();
      app.callServerTool.mockReturnValue(deferred().promise);
      await startView(view, asHostApp(app));
      app.sendResult(result("2026-10"));
      const call = lastCall();
      button("next").click();

      expect(await call("set_test", {})).toBe(false);

      expect(app.callServerTool).toHaveBeenCalledOnce();
      // The status still speaks of the navigation: the refused call changes nothing.
      expect(byId("status").textContent).toBe("Chargement…");
    });

    it("lets a result of the agent win over a save still pending, and ignores its late answer", async () => {
      const app = fakeApp();
      const answer = deferred();
      app.callServerTool.mockReturnValue(answer.promise);
      await startView(view, asHostApp(app));
      app.sendResult(result("2026-10"));
      const saved = lastCall()("set_test", {});

      app.sendResult(result("2027-01"));
      answer.resolve(result("2026-10"));

      expect(await saved).toBe(false);
      expect(byId("month").textContent).toBe("Janvier 2027");
      expect(render).toHaveBeenCalledTimes(2);
    });
  });

  it("never renders on a resize while a form is open, which would close it", async () => {
    // As the starting balance card in edit mode: a form in the content.
    render.mockImplementation((_, node) => { node.append(document.createElement("form")); });
    const app = fakeApp();
    await startView(view, asHostApp(app));
    setWidth(content, 640);
    app.sendResult(result("2026-10"));

    setWidth(content, 480);
    ManualResizeObserver.last!.trigger();
    expect(render).toHaveBeenCalledOnce();

    // Form closed: the next resize redraws at the new width.
    content.querySelector("form")!.remove();
    ManualResizeObserver.last!.trigger();
    expect(render).toHaveBeenCalledTimes(2);
  });

  it("never renders on a resize while a deletion waits for its confirmation", async () => {
    // Opening the confirmation makes the content taller: a scrollbar appears and the width changes, which closed it.
    render.mockImplementation((_, node) => {
      const row = document.createElement("tr");
      row.className = "confirming";
      node.append(row);
    });
    const app = fakeApp();
    await startView(view, asHostApp(app));
    setWidth(content, 640);
    app.sendResult(result("2026-10"));

    setWidth(content, 623);
    ManualResizeObserver.last!.trigger();

    expect(render).toHaveBeenCalledOnce();
  });

  it("does not render on a resize before the first result", async () => {
    await startView(view, asHostApp(fakeApp()));
    setWidth(content, 480);

    ManualResizeObserver.last!.trigger();

    expect(render).not.toHaveBeenCalled();
  });
});
