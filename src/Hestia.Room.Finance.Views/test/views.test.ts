import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { ViewContext } from "../src/shared/view";
import { cashFlowForecast, chart, MIN_WIDTH } from "../src/cash-flow-forecast";
import { monthOverview, SET_BUDGET_ACTUAL, SET_LINE_ACTUAL } from "../src/month-overview";
import type {
  BudgetActualOutput, CashFlowForecastOutput, LineActualOutput, MonthSummaryOutput, PlanBudgetOutput, PlanOutput, SubscriptionOutput, SubscriptionsOutput,
} from "../src/shared/outputs";
import { formatEuros } from "../src/shared/format";
import { subscriptions } from "../src/subscriptions";
import forecastFixture from "./fixtures/cash-flow-forecast.json";
import monthFixture from "./fixtures/month-overview.json";
import subscriptionsFixture from "./fixtures/subscriptions.json";
import planFixture from "./fixtures/plan.json";
import { CREATE_BUDGET, CREATE_LINE, DELETE_BUDGET, DELETE_LINE, END_LINE, plan, UPDATE_BUDGET, UPDATE_LINE } from "../src/plan";
import { startView } from "../src/shared/view";
import { asHostApp, at, fakeApp, ManualResizeObserver, pageSkeleton, plain, toolResult } from "./helpers";

// The fixtures are the structured content of the view tools on the demo plan, written and checked by a C# test.
// Typed by annotation, not by assertion: the compiler checks that they match the interfaces. The month, the
// subscriptions and the plan need an assertion, since JSON gives their enums as plain strings, not as the union types.
const demoMonth = monthFixture as MonthSummaryOutput;
const demoForecast: CashFlowForecastOutput = forecastFixture;
const demoSubscriptions = subscriptionsFixture as SubscriptionsOutput;
const demoPlan = planFixture as PlanOutput;

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
  const [fuel, groceries, outings] = demoPlan.budgets as [PlanBudgetOutput, PlanBudgetOutput, PlanBudgetOutput];
  const rowOf = (index: number) => content.querySelectorAll(".budgets tbody tr")[index]!;
  const newRow = () => content.querySelector(".budgets tfoot tr")!;
  const buttonIn = (node: Element, name: string) => [...node.querySelectorAll("button")].find(b => b.textContent === name)!;
  const fieldsIn = (node: Element) => [...node.querySelectorAll("input")];
  const submitIn = (node: Element) => { node.querySelector("form")!.dispatchEvent(new Event("submit", { cancelable: true })); };

  function render(call = vi.fn<ViewContext["call"]>(() => new Promise(() => {})), data: PlanOutput = demoPlan) {
    content.replaceChildren();
    plan.render(data, content, at(640, call));
    return call;
  }

  it("shows each budget with its planned amount, Modifier and Supprimer, then the row to add one", () => {
    render();

    expect(rows(".budgets")).toEqual(demoPlan.budgets.map(b => [[b.name], [euros(b.plannedAmount)], ["Modifier", "Supprimer"]]));
    expect(buttonIn(newRow(), "Nouveau budget")).toBeDefined();
    expect(content.querySelector(".note")?.textContent).toContain("change aussi les mois passés");
    expect(content.querySelector(".note")?.textContent).toContain("Terminer une ligne garde son passé");
  });

  it("keeps the row to add a budget when the plan has none", () => {
    render(undefined, { ...demoPlan, budgets: [] });

    expect(content.querySelectorAll(".budgets tbody tr")).toHaveLength(0);
    expect(buttonIn(newRow(), "Nouveau budget")).toBeDefined();
  });

  it("changes a budget: both fields prefilled, sent with its identifier", () => {
    const call = render();
    buttonIn(rowOf(1), "Modifier").click();
    expect(fieldsIn(rowOf(1)).map(f => plain(f.value))).toEqual(["Courses", "320,00"]);
    const [name, plannedAmount] = fieldsIn(rowOf(1)) as [HTMLInputElement, HTMLInputElement];
    name.value = " Alimentation ";
    plannedAmount.value = "350";

    buttonIn(rowOf(1), "Enregistrer").click();

    expect(call).toHaveBeenCalledExactlyOnceWith(UPDATE_BUDGET, { month: "2027-03", budgetId: groceries.id, name: "Alimentation", plannedAmount: 350 });
  });

  it("keeps the name of the budget changed, even in another case", () => {
    const call = render();
    buttonIn(rowOf(1), "Modifier").click();
    fieldsIn(rowOf(1))[0]!.value = "COURSES";

    submitIn(rowOf(1));

    expect(call).toHaveBeenCalledOnce();
  });

  it("adds a budget from empty fields", () => {
    const call = render();
    buttonIn(newRow(), "Nouveau budget").click();
    expect(fieldsIn(newRow()).map(f => f.value)).toEqual(["", ""]);
    const [name, plannedAmount] = fieldsIn(newRow()) as [HTMLInputElement, HTMLInputElement];
    name.value = "Cadeaux";
    plannedAmount.value = "40,50";

    buttonIn(newRow(), "Enregistrer").click();

    expect(call).toHaveBeenCalledExactlyOnceWith(CREATE_BUDGET, { month: "2027-03", name: "Cadeaux", plannedAmount: 40.5 });
  });

  it.each([
    ["", "40", "Nom attendu."],
    ["x".repeat(101), "40", "100 caractères au plus."],
    ["sorties", "40", "Un autre budget porte déjà ce nom."],
    ["Cadeaux", "0", "Montant attendu, plus que 0."],
    ["Cadeaux", "-5", "Montant attendu, plus que 0."],
  ])("refuses the name %j with the amount %j before any call", (name, plannedAmount, message) => {
    const call = render();
    buttonIn(newRow(), "Nouveau budget").click();
    fieldsIn(newRow())[0]!.value = name;
    fieldsIn(newRow())[1]!.value = plannedAmount;

    submitIn(newRow());

    expect(call).not.toHaveBeenCalled();
    expect(newRow().querySelector(".field-error")?.textContent).toBe(message);
  });

  it.each([
    ["", "Nom attendu."],
    ["   ", "Nom attendu."],
    ["sorties", "Un autre budget porte déjà ce nom."],
    [" SORTIES ", "Un autre budget porte déjà ce nom."],
  ])("refuses the name %j for a budget changed, before any call", (name, message) => {
    const call = render();
    buttonIn(rowOf(1), "Modifier").click();
    fieldsIn(rowOf(1))[0]!.value = name;

    submitIn(rowOf(1));

    expect(call).not.toHaveBeenCalled();
    expect(rowOf(1).querySelector(".field-error")?.textContent).toBe(message);
  });

  it("accepts a name of 100 characters", () => {
    const call = render();
    buttonIn(newRow(), "Nouveau budget").click();
    fieldsIn(newRow())[0]!.value = "x".repeat(100);
    fieldsIn(newRow())[1]!.value = "40";

    submitIn(newRow());

    expect(call).toHaveBeenCalledOnce();
  });

  it.each([
    [3, "Supprimer « Sorties » ? Ses 3 réels saisis seront aussi supprimés."],
    [1, "Supprimer « Sorties » ? Son réel saisi sera aussi supprimé."],
    [0, "Supprimer « Sorties » ?"],
  ])("says what goes with a budget deleted (%d actuals)", (actualsEntered, confirmation) => {
    render(undefined, { ...demoPlan, budgets: [fuel, groceries, { ...outings, actualsEntered }] });

    buttonIn(rowOf(2), "Supprimer").click();

    expect(rowOf(2).querySelector(".confirmation")?.textContent).toBe(confirmation);
  });

  it("deletes a budget once confirmed", () => {
    const call = render();
    buttonIn(rowOf(2), "Supprimer").click();

    buttonIn(rowOf(2), "Confirmer la suppression").click();

    expect(call).toHaveBeenCalledExactlyOnceWith(DELETE_BUDGET, { month: "2027-03", budgetId: outings.id, actualsEntered: outings.actualsEntered });
  });
});

describe("plan, the recurring lines", () => {
  const lineRow = (index: number) => content.querySelectorAll(".lines tbody tr")[index]!;
  const newLine = () => content.querySelector(".lines tfoot tr")!;
  const buttonIn = (node: Element, name: string) => [...node.querySelectorAll("button")].find(b => b.textContent === name)!;
  const control = (node: Element, label: string) => node.querySelector<HTMLInputElement & HTMLSelectElement>(`[aria-label="${label}"]`)!;
  const holder = (node: Element, label: string) => control(node, label).closest("label")!;
  const submitIn = (node: Element) => { node.querySelector("form")!.dispatchEvent(new Event("submit", { cancelable: true })); };
  const choose = (node: Element, label: string, value: string) => {
    control(node, label).value = value;
    control(node, label).dispatchEvent(new Event("change", { bubbles: true }));
  };
  const lines = demoPlan.lines;
  const named = (name: string) => lines.find(line => line.name === name)!;
  const indexOf = (name: string) => lines.findIndex(line => line.name === name);

  function render(call = vi.fn<ViewContext["call"]>(() => new Promise(() => {}))) {
    content.replaceChildren();
    plan.render(demoPlan, content, at(640, call));
    return call;
  }

  it("shows each line with its day, its details, its frequency and its amount, then the row to add one", () => {
    render();

    expect(content.querySelector(".lines")?.previousElementSibling?.textContent).toBe("Lignes récurrentes");
    expect(rows(".lines")[0]).toEqual([["28"], ["Salaire", "entrée"], ["mensuel"], [euros(2250)], ["Modifier", "Terminer", "Supprimer"]]);
    expect(rows(".lines")[indexOf("Salle de sport")]?.[1]).toEqual(["Salle de sport", "abonnement", "engagement jusqu'au 31/05/2027"]);
    expect(rows(".lines")[indexOf("Assurance habitation")]?.[1]).toEqual(["Assurance habitation", "en novembre"]);
    expect(rows(".lines")[indexOf("Assurance auto")]?.slice(1, 3)).toEqual([["Assurance auto", "en mars, juin, septembre, décembre"], ["trimestriel"]]);
    expect(rows(".lines")[indexOf("Loyer")]?.[1]).toEqual(["Loyer"]);
    expect(buttonIn(newLine(), "Nouvelle ligne")).toBeDefined();
  });

  it("gives the months of a quarterly line across the end of the year, in the order of the year", () => {
    content.replaceChildren();
    const quarterly = { ...named("Assurance auto"), anchorMonth: 11 };
    plan.render({ ...demoPlan, lines: [quarterly] }, content, at(640, vi.fn<ViewContext["call"]>(() => new Promise(() => {}))));

    expect(rows(".lines")[0]?.[1]).toEqual(["Assurance auto", "en février, mai, août, novembre"]);
  });

  it.each([["Salaire"], ["Salle de sport"], ["Assurance habitation"], ["ended"]])(
    "sends the line %s back unchanged when nothing is touched", (name) => {
      const line = name === "ended" ? { ...named("Loyer"), endDate: "2027-06-30" } : named(name);
      const call = vi.fn<ViewContext["call"]>(() => new Promise(() => {}));
      content.replaceChildren();
      plan.render({ ...demoPlan, lines: [line] }, content, at(640, call));
      buttonIn(lineRow(0), "Modifier").click();

      submitIn(lineRow(0));

      expect(call).toHaveBeenCalledExactlyOnceWith(UPDATE_LINE, {
        month: "2027-03", lineId: line.id, kind: line.kind, name: line.name, amount: line.amount, category: line.category,
        frequency: line.frequency, dayOfMonth: line.dayOfMonth, anchorMonth: line.anchorMonth, startDate: line.startDate,
        endDate: line.endDate, isSubscription: line.isSubscription, commitmentEndDate: line.commitmentEndDate,
      });
    });

  it("says when a line ends, and when a line saved for later starts", () => {
    content.replaceChildren();
    const ended = { ...named("Loyer"), endDate: "2027-06-30" };
    const later = { ...named("Box internet"), isSubscription: false, startDate: "2027-05-01" };
    plan.render({ ...demoPlan, lines: [ended, later] }, content, at(640, vi.fn<ViewContext["call"]>(() => new Promise(() => {}))));

    expect(rows(".lines")[0]?.[1]).toEqual(["Loyer", "jusqu'au 30/06/2027"]);
    expect(rows(".lines")[1]?.[1]).toEqual(["Box internet", "à partir du 01/05/2027"]);
  });

  it("changes a line: every field prefilled, sent with its identifier, the month of a list as a number", () => {
    const call = render();
    const row = () => lineRow(indexOf("Assurance auto"));
    buttonIn(row(), "Modifier").click();
    expect([control(row(), "Type").value, control(row(), "Nom").value, control(row(), "Catégorie").value, control(row(), "Fréquence").value,
      control(row(), "Jour du mois").value, control(row(), "Mois de la première échéance").value, control(row(), "Début").value])
      .toEqual(["Expense", "Assurance auto", "Insurance", "Quarterly", "15", "3", "2026-03-01"]);
    control(row(), "Montant").value = "170";

    submitIn(row());

    expect(call).toHaveBeenCalledExactlyOnceWith(UPDATE_LINE, {
      month: "2027-03", lineId: named("Assurance auto").id, kind: "Expense", name: "Assurance auto", amount: 170, category: "Insurance",
      frequency: "Quarterly", dayOfMonth: 15, anchorMonth: 3, startDate: "2026-03-01", endDate: null, isSubscription: false, commitmentEndDate: null,
    });
  });

  it("adds a line from its defaults: an expense starting the month shown, no month nor commitment asked", () => {
    const call = render();
    buttonIn(newLine(), "Nouvelle ligne").click();
    expect(control(newLine(), "Début").value).toBe("2027-03-01");
    expect(holder(newLine(), "Mois de la première échéance").hidden).toBe(true);
    expect(holder(newLine(), "Fin d'engagement (facultative)").hidden).toBe(true);
    control(newLine(), "Nom").value = "Cadeaux";
    control(newLine(), "Montant").value = "40";
    control(newLine(), "Jour du mois").value = "10";

    submitIn(newLine());

    expect(call).toHaveBeenCalledExactlyOnceWith(CREATE_LINE, {
      month: "2027-03", kind: "Expense", name: "Cadeaux", amount: 40, category: "Other", frequency: "Monthly", dayOfMonth: 10,
      anchorMonth: null, startDate: "2027-03-01", endDate: null, isSubscription: false, commitmentEndDate: null,
    });
  });

  it("adds a yearly income: its month asked and sent, never a subscription", () => {
    const call = render();
    buttonIn(newLine(), "Nouvelle ligne").click();
    choose(newLine(), "Type", "Income");
    choose(newLine(), "Fréquence", "Yearly");
    choose(newLine(), "Mois de la première échéance", "6");
    control(newLine(), "Nom").value = "Prime";
    control(newLine(), "Montant").value = "800";
    control(newLine(), "Jour du mois").value = "30";

    submitIn(newLine());

    expect(holder(newLine(), "Abonnement").hidden).toBe(true);
    expect(call).toHaveBeenCalledExactlyOnceWith(CREATE_LINE,
      expect.objectContaining({ kind: "Income", frequency: "Yearly", anchorMonth: 6, isSubscription: false, commitmentEndDate: null }));
  });

  it("adds a subscription with its end of commitment, asked once Abonnement is checked", () => {
    const call = render();
    buttonIn(newLine(), "Nouvelle ligne").click();
    control(newLine(), "Abonnement").checked = true;
    control(newLine(), "Abonnement").dispatchEvent(new Event("change", { bubbles: true }));
    expect(holder(newLine(), "Fin d'engagement (facultative)").hidden).toBe(false);
    control(newLine(), "Fin d'engagement (facultative)").value = "2027-12-31";
    control(newLine(), "Nom").value = "Musique";
    control(newLine(), "Montant").value = "10,99";
    control(newLine(), "Jour du mois").value = "12";

    submitIn(newLine());

    expect(call).toHaveBeenCalledExactlyOnceWith(CREATE_LINE,
      expect.objectContaining({ name: "Musique", amount: 10.99, isSubscription: true, commitmentEndDate: "2027-12-31" }));
  });

  it.each([
    ["Nom", "loyer", "Une autre ligne porte déjà ce nom sur la même période."],
    ["Nom", "", "Nom attendu."],
    ["Montant", "0", "Montant attendu, plus que 0."],
    ["Jour du mois", "32", "Nombre entier attendu, de 1 à 31."],
    ["Fin (facultative)", "2027-02-28", "La fin ne peut pas précéder le début."],
  ])("refuses the field %s at %j before any call", (label, value, message) => {
    const call = render();
    buttonIn(newLine(), "Nouvelle ligne").click();
    control(newLine(), "Nom").value = "Cadeaux";
    control(newLine(), "Montant").value = "40";
    control(newLine(), "Jour du mois").value = "10";
    control(newLine(), label).value = value;

    submitIn(newLine());

    expect(call).not.toHaveBeenCalled();
    expect(newLine().querySelector(".field-error")?.textContent).toBe(message);
  });

  it("drops the subscription and its commitment when the line becomes an income", () => {
    const call = render();
    buttonIn(newLine(), "Nouvelle ligne").click();
    control(newLine(), "Abonnement").checked = true;
    control(newLine(), "Abonnement").dispatchEvent(new Event("change", { bubbles: true }));
    control(newLine(), "Fin d'engagement (facultative)").value = "2027-12-31";
    choose(newLine(), "Type", "Income");
    control(newLine(), "Nom").value = "Prime";
    control(newLine(), "Montant").value = "800";
    control(newLine(), "Jour du mois").value = "30";

    submitIn(newLine());

    expect(holder(newLine(), "Fin d'engagement (facultative)").hidden).toBe(true);
    expect(call).toHaveBeenCalledExactlyOnceWith(CREATE_LINE,
      expect.objectContaining({ kind: "Income", isSubscription: false, commitmentEndDate: null }));
  });

  it.each([
    ["an end on its start", "2027-03-01"],
    ["no end", ""],
  ])("accepts %s", (_, end) => {
    const call = render();
    buttonIn(newLine(), "Nouvelle ligne").click();
    control(newLine(), "Nom").value = "Cadeaux";
    control(newLine(), "Montant").value = "40";
    control(newLine(), "Jour du mois").value = "10";
    control(newLine(), "Fin (facultative)").value = end;

    submitIn(newLine());

    expect(call).toHaveBeenCalledOnce();
  });

  it("accepts the name of a line once the other one has ended, refuses it while both run", () => {
    content.replaceChildren();
    const call = vi.fn<ViewContext["call"]>(() => new Promise(() => {}));
    plan.render({ ...demoPlan, lines: [{ ...named("Loyer"), endDate: "2027-03-31" }] }, content, at(640, call));
    buttonIn(newLine(), "Nouvelle ligne").click();
    control(newLine(), "Nom").value = "Loyer";
    control(newLine(), "Montant").value = "750";
    control(newLine(), "Jour du mois").value = "5";

    // The default start, 2027-03-01: both run in March.
    submitIn(newLine());
    expect(newLine().querySelector(".field-error")?.textContent).toBe("Une autre ligne porte déjà ce nom sur la même période.");
    expect(document.activeElement).toBe(control(newLine(), "Nom"));

    control(newLine(), "Début").value = "2027-04-01";
    submitIn(newLine());

    expect(call).toHaveBeenCalledExactlyOnceWith(CREATE_LINE, expect.objectContaining({ name: "Loyer", startDate: "2027-04-01" }));
  });

  it("accepts the name of a budget: two lists apart", () => {
    const call = render();
    buttonIn(newLine(), "Nouvelle ligne").click();
    control(newLine(), "Nom").value = demoPlan.budgets[0]!.name;
    control(newLine(), "Montant").value = "40";
    control(newLine(), "Jour du mois").value = "10";

    submitIn(newLine());

    expect(call).toHaveBeenCalledOnce();
  });

  it("ends a line on the day chosen, prefilled with the last day of the month shown", () => {
    const call = render();
    const row = () => lineRow(indexOf("Loyer"));
    buttonIn(row(), "Terminer").click();
    expect(control(row(), "Dernier jour").value).toBe("2027-03-31");
    control(row(), "Dernier jour").value = "2027-04-30";

    submitIn(row());

    expect(call).toHaveBeenCalledExactlyOnceWith(END_LINE, { month: "2027-03", lineId: named("Loyer").id, endDate: "2027-04-30" });
  });

  it.each([["2027-04", "2027-04-30"], ["2028-02", "2028-02-29"]])("proposes the last day of %s to end a line", (month, day) => {
    content.replaceChildren();
    plan.render({ ...demoPlan, month }, content, at(640, vi.fn<ViewContext["call"]>(() => new Promise(() => {}))));

    buttonIn(lineRow(indexOf("Loyer")), "Terminer").click();

    expect(control(lineRow(indexOf("Loyer")), "Dernier jour").value).toBe(day);
  });

  it("proposes its own end to end an ended line again, never a later day", () => {
    content.replaceChildren();
    plan.render({ ...demoPlan, lines: [{ ...named("Loyer"), endDate: "2027-03-15" }] },
      content, at(640, vi.fn<ViewContext["call"]>(() => new Promise(() => {}))));

    buttonIn(lineRow(0), "Terminer").click();

    expect(control(lineRow(0), "Dernier jour").value).toBe("2027-03-15");
  });

  it("ends a line on its start, and refuses to end it so late that it meets the next line of the same name", () => {
    content.replaceChildren();
    const call = vi.fn<ViewContext["call"]>(() => new Promise(() => {}));
    const next = { ...named("Loyer"), id: "00000000-0000-0000-0000-000000000099", startDate: "2027-07-01" };
    plan.render({ ...demoPlan, lines: [{ ...named("Loyer"), endDate: "2027-06-30" }, next] }, content, at(640, call));
    const row = () => lineRow(0);

    buttonIn(row(), "Terminer").click();
    control(row(), "Dernier jour").value = "2027-07-01";
    submitIn(row());
    expect(row().querySelector(".field-error")?.textContent).toBe("Une autre ligne porte déjà ce nom sur la même période.");

    control(row(), "Dernier jour").value = "2026-03-01";
    submitIn(row());
    expect(call).toHaveBeenCalledExactlyOnceWith(END_LINE, { month: "2027-03", lineId: named("Loyer").id, endDate: "2026-03-01" });
  });

  it("refuses an end before the start of the line", () => {
    const call = render();
    const row = () => lineRow(indexOf("Loyer"));
    buttonIn(row(), "Terminer").click();
    control(row(), "Dernier jour").value = "2026-02-28";

    submitIn(row());

    expect(call).not.toHaveBeenCalled();
    expect(row().querySelector(".field-error")?.textContent).toBe("La fin ne peut pas précéder le début de la ligne.");
  });

  it("says what goes with a line deleted, and sends the number of corrections announced", () => {
    const call = render();
    const row = () => lineRow(indexOf("Électricité"));
    buttonIn(row(), "Supprimer").click();
    expect(row().querySelector(".confirmation")?.textContent).toBe("Supprimer « Électricité » ? Sa correction saisie sera aussi supprimée.");

    buttonIn(row(), "Confirmer la suppression").click();

    expect(call).toHaveBeenCalledExactlyOnceWith(DELETE_LINE, { month: "2027-03", lineId: named("Électricité").id, overridesEntered: 1 });
  });

  it("deletes a line without corrections, sending 0", () => {
    const call = render();
    const row = () => lineRow(indexOf("Loyer"));
    buttonIn(row(), "Supprimer").click();

    buttonIn(row(), "Confirmer la suppression").click();

    expect(call).toHaveBeenCalledExactlyOnceWith(DELETE_LINE, { month: "2027-03", lineId: named("Loyer").id, overridesEntered: 0 });
  });

  it("says nothing more for a line without corrections, and the plural for several", () => {
    content.replaceChildren();
    const loyer = { ...named("Loyer"), overridesEntered: 3 };
    plan.render({ ...demoPlan, lines: [named("Salaire"), loyer] }, content, at(640, vi.fn<ViewContext["call"]>(() => new Promise(() => {}))));

    buttonIn(lineRow(0), "Supprimer").click();
    buttonIn(lineRow(1), "Supprimer").click();

    expect(lineRow(0).querySelector(".confirmation")?.textContent).toBe("Supprimer « Salaire » ?");
    expect(lineRow(1).querySelector(".confirmation")?.textContent).toBe("Supprimer « Loyer » ? Ses 3 corrections saisies seront aussi supprimées.");
  });
});

describe("plan saved through startView", () => {
  afterEach(() => { vi.unstubAllGlobals(); });

  it("redraws the plan from the server, the focus back on the button to add", async () => {
    vi.stubGlobal("ResizeObserver", ManualResizeObserver);
    const page = pageSkeleton();
    const app = fakeApp();
    const added = { ...demoPlan, budgets: [...demoPlan.budgets, { id: "00000000-0000-0000-0000-000000000009", name: "Cadeaux", plannedAmount: 40.5, actualsEntered: 0 }] };
    app.callServerTool.mockResolvedValue(toolResult({ structuredContent: { ...added } }));
    await startView(plan, asHostApp(app));
    app.sendResult(toolResult({ structuredContent: { ...demoPlan } }));
    const addRow = () => page.querySelector(".budgets tfoot tr")!;
    [...addRow().querySelectorAll("button")].find(b => b.textContent === "Nouveau budget")!.click();
    addRow().querySelectorAll("input")[0]!.value = "Cadeaux";
    addRow().querySelectorAll("input")[1]!.value = "40,50";

    [...addRow().querySelectorAll("button")].find(b => b.textContent === "Enregistrer")!.click();

    await vi.waitFor(() => { expect(page.querySelectorAll(".budgets tbody tr")).toHaveLength(4); });
    expect(app.callServerTool).toHaveBeenCalledWith({ name: "create_budget", arguments: { month: "2027-03", name: "Cadeaux", plannedAmount: 40.5 } });
    expect(addRow().querySelector("input")).toBeNull();
    expect(document.activeElement).toBe(addRow().querySelector(".new"));
  });
});

describe("plan renamed through startView", () => {
  afterEach(() => { vi.unstubAllGlobals(); });

  it("puts the focus back on Modifier of the budget renamed, wherever the new name sorts it", async () => {
    vi.stubGlobal("ResizeObserver", ManualResizeObserver);
    const [fuel, groceries, outings] = demoPlan.budgets as [PlanBudgetOutput, PlanBudgetOutput, PlanBudgetOutput];
    const page = pageSkeleton();
    const app = fakeApp();
    // "Alimentation": first once sorted by the server, no longer second.
    const renamed = { ...demoPlan, budgets: [{ ...groceries, name: "Alimentation" }, fuel, outings] };
    app.callServerTool.mockResolvedValue(toolResult({ structuredContent: { ...renamed } }));
    await startView(plan, asHostApp(app));
    app.sendResult(toolResult({ structuredContent: { ...demoPlan } }));
    const row = (index: number) => page.querySelectorAll(".budgets tbody tr")[index]!;
    [...row(1).querySelectorAll("button")].find(b => b.textContent === "Modifier")!.click();
    row(1).querySelectorAll("input")[0]!.value = "Alimentation";

    [...row(1).querySelectorAll("button")].find(b => b.textContent === "Enregistrer")!.click();

    await vi.waitFor(() => { expect(row(0).querySelector("td")?.textContent).toBe("Alimentation"); });
    expect(document.activeElement).toBe(row(0).querySelector(".edit"));
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
