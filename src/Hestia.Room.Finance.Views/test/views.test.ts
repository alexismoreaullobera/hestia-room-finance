import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { ViewContext } from "../src/shared/view";
import { cashFlowForecast, chart, MIN_WIDTH } from "../src/cash-flow-forecast";
import { monthOverview, SET_BUDGET_ACTUAL, SET_LINE_ACTUAL } from "../src/month-overview";
import type {
  BudgetActualOutput, CashFlowForecastOutput, LineActualOutput, MonthSummaryOutput, PlanOutput, SubscriptionOutput, SubscriptionsOutput,
} from "../src/shared/outputs";
import { formatEuros } from "../src/shared/format";
import { subscriptions } from "../src/subscriptions";
import forecastFixture from "./fixtures/cash-flow-forecast.json";
import monthFixture from "./fixtures/month-overview.json";
import subscriptionsFixture from "./fixtures/subscriptions.json";
import planFixture from "./fixtures/plan.json";
import { plan } from "../src/plan";
import { startView } from "../src/shared/view";
import { asHostApp, at, fakeApp, ManualResizeObserver, pageSkeleton, plain, toolResult } from "./helpers";

// The fixtures are the structured content of the view tools on the demo plan, written and checked by a C# test.
// Typed by annotation, not by assertion: the compiler checks that they match the interfaces. The month and the
// subscriptions need an assertion, since JSON gives their enums as plain strings, not as the union types.
const demoMonth = monthFixture as MonthSummaryOutput;
const demoForecast: CashFlowForecastOutput = forecastFixture;
const demoSubscriptions = subscriptionsFixture as SubscriptionsOutput;
const demoPlan: PlanOutput = planFixture;

let content: HTMLElement;
beforeEach(() => {
  content = document.createElement("main");
  document.body.replaceChildren(content);
});

const texts = (selector: string) => [...content.querySelectorAll(selector)].map(node => plain(node.textContent));
/** Every row of a table body (all tables by default), every cell as the texts of its parts (a value and its detail). */
const rows = (table = "") => [...content.querySelectorAll(`${table} tbody tr`)]
  .map(row => [...row.children].map(cell => [...cell.childNodes].map(part => plain(part.textContent))));
const euros = (value: number) => plain(formatEuros(value));

describe("view definitions", () => {
  it.each([
    [monthOverview.tool, "show_month_overview"],
    [cashFlowForecast.tool, "show_cash_flow_forecast"],
    [subscriptions.tool, "show_subscriptions"],
    [plan.tool, "show_plan"],
  ])("a view changes month through its own tool: %s", (tool, expected) => {
    // Written as literals: the view tool names are a contract with the server (tested on the server side too).
    expect(tool).toBe(expected);
  });
});

describe("month overview", () => {
  // On the demo plan nothing is entered yet, so planned and actual are equal: different here, so that a column or a
  // card showing the wrong reading fails.
  const planned = demoMonth.planned;
  const actual = {
    ...planned, incomes: 2300, recurringExpenses: 1300.5, budgets: 480.25, savingsCapacity: 519.25,
    smoothedSavingsCapacity: 600,
  };

  it("shows the planned and actual savings capacity, as due this month and on an average month", () => {
    monthOverview.render({ ...demoMonth, actual }, content, at(640));

    expect(texts(".card .title")).toEqual(["Capacité d'épargne prévue", "Capacité d'épargne réelle"]);
    expect(texts(".card .big")).toEqual([euros(planned.savingsCapacity), euros(actual.savingsCapacity)]);
    expect(texts(".card .detail")).toEqual([
      `Sur un mois moyen : ${euros(planned.smoothedSavingsCapacity)}`, `Sur un mois moyen : ${euros(600)}`]);
  });

  it("shows the figures side by side, planned then actual", () => {
    monthOverview.render({ ...demoMonth, actual }, content, at(640));

    expect(rows(".totals")).toEqual([
      [["Entrées"], [euros(planned.incomes)], [euros(2300)]],
      [["Dépenses récurrentes"], [euros(planned.recurringExpenses)], [euros(1300.5)]],
      [["Budgets"], [euros(planned.budgets)], [euros(480.25)]],
      [["Capacité d'épargne"], [euros(planned.savingsCapacity)], [euros(519.25)]],
    ]);
    expect(content.querySelector(".totals tbody tr:last-child")?.className).toBe("total");
    expect(content.querySelector(".note")).not.toBeNull();
  });
});

describe("actuals of the month", () => {
  // The demo month with one budget entered and two lines that went otherwise: every state of a row.
  const [fuel, groceries, outings] = demoMonth.budgets as [BudgetActualOutput, BudgetActualOutput, BudgetActualOutput];
  const [transport, gym, rent] = demoMonth.lines as [LineActualOutput, LineActualOutput, LineActualOutput];
  const salary = demoMonth.lines.find(line => line.kind === "Income")!;
  const month: MonthSummaryOutput = {
    ...demoMonth,
    budgets: [fuel, { ...groceries, actual: 347.85, entered: true }, outings],
    lines: demoMonth.lines.map(line =>
      line.id === gym.id ? { ...line, actual: 0, status: "Skipped" as const }
        : line.id === rent.id ? { ...line, actual: 735.5, status: "Corrected" as const }
          : line),
  };
  const buttonIn = (node: Element | null | undefined, name: string) =>
    [...(node?.querySelectorAll("button") ?? [])].find(b => b.textContent === name)!;
  const rowOf = (table: string, index: number) => content.querySelectorAll(`.${table} tbody tr`)[index]!;

  /** A fresh rendering, as startView does after each result (it empties the content first). */
  function render(call = vi.fn<ViewContext["call"]>(() => new Promise(() => {}))) {
    content.replaceChildren();
    monthOverview.render(month, content, at(640, call));
    return call;
  }

  it("shows each budget with its actual, or as planned when none was entered", () => {
    render();

    expect(content.querySelector(".budgets")?.previousElementSibling?.textContent).toBe("Budgets");
    expect(rows(".budgets").map(cells => cells.slice(0, 3))).toEqual([
      [["Carburant"], [euros(90)], ["comme prévu"]],
      [["Courses"], [euros(320)], [euros(347.85)]],
      [["Sorties"], [euros(120)], ["comme prévu"]],
    ]);
  });

  it("shows each line due with what happened, an income marked as such", () => {
    render();

    expect(rows(".lines")[0]).toEqual([["01/03"], ["Abonnement transports"], [euros(transport.planned)], ["comme prévu"], ["Modifier"]]);
    expect(rows(".lines")[1]).toEqual([["03/03"], ["Salle de sport"], [euros(gym.planned)], [euros(0), "n'a pas eu lieu"], ["Modifier"]]);
    expect(rows(".lines")[2]).toEqual([["05/03"], ["Loyer"], [euros(720)], [euros(735.5), "corrigé"], ["Modifier"]]);
    const salaryRow = rows(".lines")[demoMonth.lines.indexOf(salary)];
    expect(salaryRow?.[1]).toEqual(["Salaire", "entrée"]);
    // A line that did not happen stands out (in red), a correction stays neutral.
    expect(rowOf("lines", 1).querySelector(".badge")?.className).toBe("badge skipped");
    expect(rowOf("lines", 2).querySelector(".badge")?.className).toBe("badge corrected");
  });

  it("shows no table without budgets nor lines", () => {
    monthOverview.render({ ...month, budgets: [], lines: [] }, content, at(640));

    expect(content.querySelector(".budgets")).toBeNull();
    expect(content.querySelector(".lines")).toBeNull();
    expect([...content.querySelectorAll("h2")]).toEqual([]);
  });

  it("turns a budget row into the form, on the whole width, prefilled and named", () => {
    render();

    buttonIn(rowOf("budgets", 1), "Modifier").click();

    const row = rowOf("budgets", 1);
    expect(row.classList.contains("editing")).toBe(true);
    expect(row.querySelector("td")?.colSpan).toBe(4);
    expect(plain(row.querySelector("input")?.value)).toBe("347,85");
    expect(row.querySelector("input")?.getAttribute("aria-label")).toBe("Réel de Courses en Mars 2027");
    expect(document.activeElement).toBe(row.querySelector("input"));
  });

  it("saves the total of a budget, or goes back to the plan", () => {
    const call = render();
    buttonIn(rowOf("budgets", 1), "Modifier").click();
    rowOf("budgets", 1).querySelector("input")!.value = "300";

    buttonIn(rowOf("budgets", 1), "Enregistrer").click();
    expect(call).toHaveBeenLastCalledWith(SET_BUDGET_ACTUAL, { month: "2027-03", budgetId: groceries.id, actualAmount: 300 });

    render(call);
    buttonIn(rowOf("budgets", 1), "Modifier").click();
    buttonIn(rowOf("budgets", 1), "Comme prévu").click();
    expect(call).toHaveBeenLastCalledWith(SET_BUDGET_ACTUAL, { month: "2027-03", budgetId: groceries.id, actualAmount: null });
  });

  it("offers Comme prévu only for a budget entered", () => {
    render();

    buttonIn(rowOf("budgets", 0), "Modifier").click();

    expect([...rowOf("budgets", 0).querySelectorAll("button")].map(b => b.textContent)).toEqual(["Enregistrer", "Annuler"]);
  });

  it("corrects a line, marks it as not happened, or goes back to the plan", () => {
    const call = render();
    buttonIn(rowOf("lines", 0), "Modifier").click();
    rowOf("lines", 0).querySelector("input")!.value = "90";

    buttonIn(rowOf("lines", 0), "Enregistrer").click();
    expect(call).toHaveBeenLastCalledWith(SET_LINE_ACTUAL, { month: "2027-03", lineId: transport.id, status: "Corrected", actualAmount: 90 });

    render(call);
    buttonIn(rowOf("lines", 0), "Modifier").click();
    buttonIn(rowOf("lines", 0), "N'a pas eu lieu").click();
    expect(call).toHaveBeenLastCalledWith(SET_LINE_ACTUAL, { month: "2027-03", lineId: transport.id, status: "Skipped" });

    render(call);
    buttonIn(rowOf("lines", 2), "Modifier").click();
    buttonIn(rowOf("lines", 2), "Comme prévu").click();
    expect(call).toHaveBeenLastCalledWith(SET_LINE_ACTUAL, { month: "2027-03", lineId: rent.id, status: "AsPlanned" });
  });

  it.each([
    ["as planned", 0, ["Enregistrer", "N'a pas eu lieu", "Annuler"], "86,40"],
    ["skipped, prefilled with the plan", 1, ["Enregistrer", "Comme prévu", "Annuler"], "34,90"],
    ["corrected", 2, ["Enregistrer", "N'a pas eu lieu", "Comme prévu", "Annuler"], "735,50"],
  ])("offers the actions that change a line %s", (_, index, buttons, prefilled) => {
    render();

    buttonIn(rowOf("lines", index), "Modifier").click();

    expect([...rowOf("lines", index).querySelectorAll("button")].map(b => b.textContent)).toEqual(buttons);
    expect(plain(rowOf("lines", index).querySelector("input")?.value)).toBe(prefilled);
    expect(rowOf("lines", index).querySelector("td")?.colSpan).toBe(5);
  });

  it("gives the row back on Annuler and on Escape, the focus on its button", () => {
    const call = render();
    buttonIn(rowOf("lines", 2), "Modifier").click();
    buttonIn(rowOf("lines", 2), "Annuler").click();

    expect(rowOf("lines", 2).classList.contains("editing")).toBe(false);
    expect(document.activeElement).toBe(buttonIn(rowOf("lines", 2), "Modifier"));

    buttonIn(rowOf("budgets", 1), "Modifier").click();
    rowOf("budgets", 1).querySelector("input")!.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true }));

    expect(rowOf("budgets", 1).querySelector("input")).toBeNull();
    expect(document.activeElement).toBe(buttonIn(rowOf("budgets", 1), "Modifier"));
    expect(call).not.toHaveBeenCalled();
  });
});

describe("actuals saved through startView", () => {
  afterEach(() => { vi.unstubAllGlobals(); });

  const groceries = demoMonth.budgets[1]!;
  const transport = demoMonth.lines[0]!;
  const button = (row: Element, name: string) => [...row.querySelectorAll("button")].find(b => b.textContent === name)!;

  /** The month shown through startView, the server answering every input with the given month. */
  async function shown(page: () => HTMLElement, answer: MonthSummaryOutput, first: MonthSummaryOutput = demoMonth) {
    vi.stubGlobal("ResizeObserver", ManualResizeObserver);
    page();
    const app = fakeApp();
    app.callServerTool.mockResolvedValue(toolResult({ structuredContent: { ...answer } }));
    await startView(monthOverview, asHostApp(app));
    app.sendResult(toolResult({ structuredContent: { ...first } }));
    return app;
  }

  it("redraws the month from the figures of the server, the focus back on the same row", async () => {
    const page = pageSkeleton();
    // The server answers with its own figure, not the one typed: the view shows the server's.
    const saved = { ...demoMonth, budgets: demoMonth.budgets.map(b => b.id === groceries.id ? { ...b, actual: 347.85, entered: true } : b) };
    const app = await shown(() => page, saved);
    const row = () => page.querySelectorAll(".budgets tbody tr")[1]!;
    button(row(), "Modifier").click();
    row().querySelector("input")!.value = "300";

    button(row(), "Enregistrer").click();

    await vi.waitFor(() => { expect(row().querySelector("input")).toBeNull(); });
    expect(app.callServerTool).toHaveBeenCalledWith({
      name: "set_budget_actual", arguments: { month: "2027-03", budgetId: groceries.id, actualAmount: 300 },
    });
    expect(plain(row().querySelectorAll("td")[2]?.textContent)).toBe(euros(347.85));
    expect(document.activeElement).toBe(row().querySelector(".edit"));
  });

  it("puts the focus back on the row after Comme prévu too", async () => {
    const page = pageSkeleton();
    const entered = { ...demoMonth, budgets: demoMonth.budgets.map(b => b.id === groceries.id ? { ...b, actual: 347.85, entered: true } : b) };
    const app = await shown(() => page, demoMonth, entered);
    const row = () => page.querySelectorAll(".budgets tbody tr")[1]!;
    button(row(), "Modifier").click();

    button(row(), "Comme prévu").click();

    await vi.waitFor(() => { expect(row().querySelector("input")).toBeNull(); });
    expect(app.callServerTool).toHaveBeenCalledWith({
      name: "set_budget_actual", arguments: { month: "2027-03", budgetId: groceries.id, actualAmount: null },
    });
    expect(document.activeElement).toBe(row().querySelector(".edit"));
  });

  it("puts the focus back on the same row of the lines table", async () => {
    const page = pageSkeleton();
    const corrected = { ...demoMonth, lines: demoMonth.lines.map(l => l.id === transport.id ? { ...l, actual: 90, status: "Corrected" as const } : l) };
    await shown(() => page, corrected);
    const row = () => page.querySelectorAll(".lines tbody tr")[0]!;
    button(row(), "Modifier").click();
    row().querySelector("input")!.value = "90";

    button(row(), "Enregistrer").click();

    await vi.waitFor(() => { expect(row().querySelector("input")).toBeNull(); });
    expect(document.activeElement).toBe(row().querySelector(".edit"));
  });
});

describe("cash flow forecast", () => {
  it("shows the alert with the buffer to start with, the figures, the chart and the debits", () => {
    cashFlowForecast.render(demoForecast, content, at(640));

    expect(demoForecast.hasAlert).toBe(true);
    expect(plain(content.querySelector(".alert")?.textContent)).toContain(`au moins ${plain(formatEuros(demoForecast.cashBuffer))}`);
    expect(texts(".card .big")).toEqual([
      euros(demoForecast.startingBalance), euros(demoForecast.lowPoint.balance), euros(demoForecast.cashBuffer)]);
    expect(texts(".card .detail")).toEqual(["au début du mois", "le 27/03", "pour ne jamais passer sous zéro"]);
    expect(content.querySelectorAll("svg.chart rect.day")).toHaveLength(demoForecast.days.length);
    expect(texts("tbody tr td:nth-child(2)")).toEqual(demoForecast.contributingDebits.map(d => d.name));
    expect(rows()[0]).toEqual([["01/03"], ["Abonnement transports"], [euros(86.4)]]);
  });

  it("says the month starts overdrawn instead of a buffer of 0 €", () => {
    cashFlowForecast.render({ ...demoForecast, startsOverdrawn: true, cashBuffer: 0 }, content, at(640));

    const alert = plain(content.querySelector(".alert")?.textContent);
    expect(alert).toContain("démarre déjà à découvert");
    expect(alert).not.toContain("au moins");
  });

  it("shows a calm note and no debits table when there is no alert and no debit", () => {
    cashFlowForecast.render({ ...demoForecast, hasAlert: false, contributingDebits: [] }, content, at(640));

    expect(content.querySelector(".calm")?.textContent).toContain("Pas d'alerte");
    expect(content.querySelector(".alert")).toBeNull();
    expect(content.querySelector("h2")).toBeNull();
  });

  it("keeps a name typed by the user as text", () => {
    const trap = `<img src=x onerror="alert(1)">`;
    const debit = { name: trap, date: "2027-03-05", amount: 720 };

    cashFlowForecast.render({ ...demoForecast, contributingDebits: [debit] }, content, at(640));

    expect(content.querySelector("img")).toBeNull();
    expect(texts("tbody tr td:nth-child(2)")).toEqual([trap]);
  });
});

describe("starting balance form", () => {
  const card = () => content.querySelector<HTMLElement>(".starting-balance")!;
  const field = () => card().querySelector("input")!;
  const buttonNamed = (name: string) => [...card().querySelectorAll("button")].find(b => b.textContent === name)!;
  /** Submits the form as the Enregistrer button or the Enter key would; returns the event to check its default. */
  const submit = () => {
    const event = new Event("submit", { cancelable: true });
    card().querySelector("form")!.dispatchEvent(event);
    return event;
  };

  function open(call = vi.fn<ViewContext["call"]>(() => Promise.resolve(true))) {
    cashFlowForecast.render(demoForecast, content, at(640, call));
    buttonNamed("Modifier").click();
    return call;
  }

  it("shows the balance with a button to change it, and no field", () => {
    cashFlowForecast.render(demoForecast, content, at(640));

    expect(plain(card().querySelector(".big")?.textContent)).toBe(euros(demoForecast.startingBalance));
    expect(buttonNamed("Modifier").getAttribute("type")).toBe("button");
    expect(card().querySelector("input")).toBeNull();
  });

  it("opens a field prefilled with the balance, named after the month, focused and selected", () => {
    open();

    expect(card().classList.contains("editing")).toBe(true);
    expect(plain(field().value)).toBe("400,00");
    expect(field().getAttribute("inputmode")).toBe("decimal");
    expect(field().getAttribute("aria-label")).toBe("Solde de départ de Mars 2027");
    expect(document.activeElement).toBe(field());
    // Selected: typing replaces the value at once.
    expect([field().selectionStart, field().selectionEnd]).toEqual([0, field().value.length]);
  });

  it("sends the month and the amount typed when Enregistrer is clicked, disabling the form meanwhile", () => {
    const call = open(vi.fn<ViewContext["call"]>(() => new Promise(() => {})));
    field().value = "-1 250,50";

    // The real path: the button submits its form (a type="button" would do nothing here).
    buttonNamed("Enregistrer").click();

    expect(call).toHaveBeenCalledExactlyOnceWith("set_starting_balance", { month: "2027-03", startingBalance: -1250.5 });
    expect(field().disabled).toBe(true);
    expect(buttonNamed("Enregistrer").disabled).toBe(true);
    expect(buttonNamed("Annuler").disabled).toBe(true);
  });

  it("never lets the form be submitted by default, which would navigate the frame of the view", () => {
    open(vi.fn<ViewContext["call"]>(() => new Promise(() => {})));

    expect(submit().defaultPrevented).toBe(true);
  });

  it.each(["", "abc", "12,345"])("refuses %j without calling the tool, keeping the form open", (typed) => {
    const call = open();
    field().value = typed;

    submit();

    expect(call).not.toHaveBeenCalled();
    expect(card().querySelector(".field-error")?.textContent).toBe("Montant attendu, par exemple 1 250,50.");
    expect(card().querySelector(".field-error")?.getAttribute("role")).toBe("alert");
    expect(field().value).toBe(typed);
  });

  it("brings the focus back to the field on a wrong amount", () => {
    open();
    field().value = "abc";
    buttonNamed("Annuler").focus();

    submit();

    expect(document.activeElement).toBe(field());
  });

  it("enables the form again with what was typed when the save is not shown", async () => {
    const call = open(vi.fn<ViewContext["call"]>(() => Promise.resolve(false)));
    field().value = "300";

    submit();

    await vi.waitFor(() => { expect(field().disabled).toBe(false); });
    expect(call).toHaveBeenCalledOnce();
    expect(field().value).toBe("300");
    expect(buttonNamed("Enregistrer").disabled).toBe(false);
    expect(buttonNamed("Annuler").disabled).toBe(false);
    expect(document.activeElement).toBe(field());
  });

  it("clears the message of a wrong amount once a right one is sent", () => {
    open();
    field().value = "abc";
    submit();

    field().value = "300";
    submit();

    expect(card().querySelector(".field-error")?.textContent).toBe("");
  });

  it.each([
    ["Annuler", () => { buttonNamed("Annuler").click(); }],
    ["Échap", () => { field().dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true })); }],
  ])("closes the form without saving on %s, back to the button", (_, close) => {
    const call = open();
    field().value = "999";

    close();

    expect(call).not.toHaveBeenCalled();
    expect(card().classList.contains("editing")).toBe(false);
    expect(plain(card().querySelector(".big")?.textContent)).toBe(euros(demoForecast.startingBalance));
    expect(document.activeElement).toBe(buttonNamed("Modifier"));
  });

  it.each(["a", "Enter", "Tab"])("keeps the form open on the key %s", (key) => {
    const call = open();

    field().dispatchEvent(new KeyboardEvent("keydown", { key, bubbles: true }));

    expect(card().querySelector("form")).not.toBeNull();
    expect(call).not.toHaveBeenCalled();
  });
});

describe("starting balance saved through startView", () => {
  // The form and the kit together, the SDK being the only fake: what a user sees after Enregistrer.
  afterEach(() => { vi.unstubAllGlobals(); });

  it("redraws the card with the figure of the server, the focus back on Modifier", async () => {
    vi.stubGlobal("ResizeObserver", ManualResizeObserver);
    const page = pageSkeleton();
    const app = fakeApp();
    // The server answers with its own figure (rounded, recalculated): the view shows it, never what was typed.
    app.callServerTool.mockResolvedValue(toolResult({ structuredContent: { ...demoForecast, startingBalance: 300 } }));
    await startView(cashFlowForecast, asHostApp(app));
    app.sendResult(toolResult({ structuredContent: { ...demoForecast } }));
    const balanceCard = () => page.querySelector<HTMLElement>(".starting-balance")!;
    [...balanceCard().querySelectorAll("button")].find(b => b.textContent === "Modifier")!.click();
    balanceCard().querySelector("input")!.value = "300";

    [...balanceCard().querySelectorAll("button")].find(b => b.textContent === "Enregistrer")!.click();

    await vi.waitFor(() => { expect(balanceCard().querySelector("input")).toBeNull(); });
    expect(app.callServerTool).toHaveBeenCalledWith({ name: "set_starting_balance", arguments: { month: "2027-03", startingBalance: 300 } });
    expect(balanceCard().classList.contains("editing")).toBe(false);
    expect(plain(balanceCard().querySelector(".big")?.textContent)).toBe(euros(300));
    expect(document.activeElement).toBe(balanceCard().querySelector(".edit"));
  });
});

describe("chart", () => {
  const day = (date: string, balance: number) => ({ date, balance });
  const NUMERIC_ATTRIBUTES = ["x", "y", "x1", "x2", "y1", "y2", "cx", "cy", "r", "width", "height"];
  /** Every coordinate and size of the drawing, the points of the curve and of the area included. */
  const numbers = (node: SVGElement) => [...node.querySelectorAll("*")].flatMap(child => [
    ...NUMERIC_ATTRIBUTES.flatMap(name => child.getAttribute(name) ?? []),
    ...(child.getAttribute("points")?.split(/[ ,]/) ?? []),
  ]).map(Number);

  it("is drawn at the given width, and never narrower than its minimum", () => {
    expect(chart(demoForecast.days, demoForecast.lowPoint, 1000).getAttribute("viewBox")).toBe("0 0 1000 240");
    expect(chart(demoForecast.days, demoForecast.lowPoint, 100).getAttribute("viewBox")).toBe(`0 0 ${MIN_WIDTH} 240`);
  });

  it.each([
    ["a single day", [day("2027-03-01", 100)]],
    ["every balance at zero", [day("2027-03-01", 0), day("2027-03-02", 0)]],
    ["every balance below zero", [day("2027-03-01", -50), day("2027-03-02", -80)]],
  ])("stays drawable with %s", (_, days) => {
    const node = chart(days, days[0]!, 640);

    // A NaN or an Infinity (a division by zero) would leave an attribute the browser ignores: a broken drawing.
    expect(numbers(node).length).toBeGreaterThan(20);
    expect(numbers(node).filter(value => !Number.isFinite(value))).toEqual([]);
  });

  it("dates the time axis every week and on the last day, anchored inside the frame", () => {
    const dates = [...chart(demoForecast.days, demoForecast.lowPoint, 640).querySelectorAll("text.axis")]
      .filter(text => /^\d\d\/\d\d$/.test(text.textContent));

    // 31 days: the 29/03 would be too close to the 31/03, it is skipped.
    expect(dates.map(text => text.textContent)).toEqual(["01/03", "08/03", "15/03", "22/03", "31/03"]);
    expect(dates.map(text => text.getAttribute("text-anchor"))).toEqual(["start", "middle", "middle", "middle", "end"]);
  });

  it("marks the low point, and nothing when it is not one of the days", () => {
    expect(chart(demoForecast.days, demoForecast.lowPoint, 640).querySelectorAll("circle.low")).toHaveLength(1);
    expect(chart(demoForecast.days, day("2027-04-01", -1), 640).querySelectorAll("circle.low")).toHaveLength(0);
  });

  it("gives every day its balance as a tooltip", () => {
    const tooltips = [...chart(demoForecast.days, demoForecast.lowPoint, 640).querySelectorAll("rect.day title")];

    expect(plain(tooltips[0]?.textContent)).toBe(`01/03 : ${plain(formatEuros(demoForecast.days[0]!.balance))}`);
  });
});

describe("plan", () => {
  it("shows each budget of the plan with its planned amount, by name", () => {
    plan.render(demoPlan, content, at(640));

    expect(rows(".budgets")).toEqual(demoPlan.budgets.map(b => [[b.name], [euros(b.plannedAmount)]]));
  });

  it("says when the plan has no budget", () => {
    plan.render({ ...demoPlan, budgets: [] }, content, at(640));

    expect(content.textContent).toBe("Aucun budget dans le plan.");
  });
});

describe("subscriptions", () => {
  it("shows the totals and one row per subscription, in French", () => {
    subscriptions.render(demoSubscriptions, content, at(640));

    expect(texts(".card .big")).toEqual([
      euros(demoSubscriptions.monthlyTotal), euros(demoSubscriptions.yearlyTotal),
      String(demoSubscriptions.subscriptions.length)]);
    expect(rows()).toHaveLength(demoSubscriptions.subscriptions.length);
    expect(rows()[0]).toEqual([
      ["Salle de sport", "Loisirs"], ["34,90 €", "mensuel"], ["34,90 €"], ["03/03"], ["jusqu'au 31/05/2027"]]);
  });

  it("shows a quarterly subscription with its monthly equivalent", () => {
    const quarterly: SubscriptionOutput = {
      id: "00000000-0000-0000-0000-000000000099", name: "Assurance habitation", amount: 45, frequency: "Quarterly",
      category: "Insurance", monthlyEquivalent: 15, yearlyCost: 180, dueDate: null, commitmentEndDate: null,
    };

    subscriptions.render({ ...demoSubscriptions, subscriptions: [quarterly] }, content, at(640));

    expect(rows()).toEqual([
      [["Assurance habitation", "Assurance"], ["45,00 €", "trimestriel"], ["15,00 €"], ["pas ce mois-ci"], ["sans"]]]);
  });

  it("says when there is no subscription", () => {
    subscriptions.render({ ...demoSubscriptions, subscriptions: [] }, content, at(640));

    expect(content.textContent).toBe("Aucun abonnement en cours ce mois-ci.");
  });

  it("says when a subscription is not debited this month", () => {
    const first = { ...demoSubscriptions.subscriptions[0]!, dueDate: null };

    subscriptions.render({ ...demoSubscriptions, subscriptions: [first] }, content, at(640));

    expect(texts("tbody tr td:nth-child(4)")).toEqual(["pas ce mois-ci"]);
  });
});
