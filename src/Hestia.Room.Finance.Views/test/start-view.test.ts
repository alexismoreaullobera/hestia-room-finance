import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { startView, type ViewDefinition } from "../src/shared/view";
import { asHostApp, fakeApp, ManualResizeObserver, pageSkeleton, setWidth } from "./helpers";

interface Data {
  month: string;
}

const result = (month: string) => ({ structuredContent: { month } });
const byId = (id: string) => document.getElementById(id)!;
const button = (id: string) => byId(id) as HTMLButtonElement;

describe("startView", () => {
  let content: HTMLElement;
  let render: ReturnType<typeof vi.fn<(data: Data, content: HTMLElement, width: number) => void>>;
  let view: ViewDefinition<Data>;

  beforeEach(() => {
    vi.stubGlobal("ResizeObserver", ManualResizeObserver);
    vi.spyOn(console, "error").mockImplementation(() => {});
    content = pageSkeleton();
    render = vi.fn<(data: Data, content: HTMLElement, width: number) => void>();
    view = { name: "Test", tool: "show_test", render };
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
    delete document.documentElement.dataset["theme"];
  });

  it("connects, follows the host theme and keeps the buttons disabled until the first result", async () => {
    const app = fakeApp();

    await startView(view, asHostApp(app));

    expect(app.connect).toHaveBeenCalledOnce();
    expect(document.documentElement.dataset["theme"]).toBe("dark");
    expect(button("previous").disabled).toBe(true);
    expect(button("next").disabled).toBe(true);
    expect(render).not.toHaveBeenCalled();
  });

  it("renders a result of the agent, with the month as title, and enables the buttons", async () => {
    const app = fakeApp();
    await startView(view, asHostApp(app));
    setWidth(content, 640);

    app.sendResult(result("2026-10"));

    expect(byId("month").textContent).toBe("Octobre 2026");
    expect(render).toHaveBeenCalledWith({ month: "2026-10" }, content, 640);
    expect(content.hidden).toBe(false);
    expect(byId("status").hidden).toBe(true);
    expect(button("previous").disabled).toBe(false);
  });

  it("changes month through its own tool, disabling the buttons while loading", async () => {
    const app = fakeApp();
    let answer!: (value: { structuredContent: Data }) => void;
    app.callServerTool.mockReturnValue(new Promise(resolve => { answer = resolve; }));
    await startView(view, asHostApp(app));
    app.sendResult(result("2026-10"));

    button("previous").click();

    expect(app.callServerTool).toHaveBeenCalledWith({ name: "show_test", arguments: { month: "2026-09" } });
    expect(byId("status").textContent).toBe("Chargement…");
    expect(button("next").disabled).toBe(true);

    answer(result("2026-09"));
    await vi.waitFor(() => { expect(byId("month").textContent).toBe("Septembre 2026"); });
    expect(button("next").disabled).toBe(false);
    expect(byId("status").hidden).toBe(true);
  });

  it("keeps the month shown when the tool answers with an error", async () => {
    const app = fakeApp();
    app.callServerTool.mockResolvedValue({ isError: true });
    await startView(view, asHostApp(app));
    app.sendResult(result("0001-01"));

    button("previous").click();

    await vi.waitFor(() => { expect(byId("status").textContent).toBe("Données indisponibles pour ce mois."); });
    expect(byId("status").classList.contains("error")).toBe(true);
    expect(byId("month").textContent).toBe("Janvier 1");
    expect(render).toHaveBeenCalledTimes(1);
    expect(button("previous").disabled).toBe(false);
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
  });

  it("lets a result of the agent win over a navigation still loading, and ignores its late answer", async () => {
    const app = fakeApp();
    let answer!: (value: { structuredContent: Data }) => void;
    app.callServerTool.mockReturnValue(new Promise(resolve => { answer = resolve; }));
    await startView(view, asHostApp(app));
    app.sendResult(result("2026-10"));
    button("next").click();

    app.sendResult(result("2027-01"));
    expect(byId("month").textContent).toBe("Janvier 2027");
    expect(button("next").disabled).toBe(false);

    answer(result("2026-11"));
    await Promise.resolve();
    await Promise.resolve();
    expect(byId("month").textContent).toBe("Janvier 2027");
    expect(render).toHaveBeenCalledTimes(2);
  });

  it("says so when the connection to the host fails", async () => {
    const app = fakeApp();
    app.connect.mockRejectedValue(new Error("no host"));

    await startView(view, asHostApp(app));

    expect(byId("status").textContent).toBe("Connexion à l'hôte impossible.");
    expect(byId("status").classList.contains("error")).toBe(true);
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
    expect(render).toHaveBeenLastCalledWith({ month: "2026-10" }, content, 480);
  });

  it("does not render on a resize before the first result", async () => {
    await startView(view, asHostApp(fakeApp()));
    setWidth(content, 480);

    ManualResizeObserver.last!.trigger();

    expect(render).not.toHaveBeenCalled();
  });
});
