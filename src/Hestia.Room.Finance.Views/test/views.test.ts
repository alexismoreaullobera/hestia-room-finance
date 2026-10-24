import { beforeEach, describe, expect, it } from "vitest";
import { cashFlowForecast, chart, MIN_WIDTH } from "../src/cash-flow-forecast";
import { monthOverview } from "../src/month-overview";
import type { CashFlowForecastOutput, MonthSummaryOutput, SubscriptionOutput, SubscriptionsOutput } from "../src/shared/outputs";
import { formatEuros } from "../src/shared/format";
import { subscriptions } from "../src/subscriptions";
import forecastFixture from "./fixtures/cash-flow-forecast.json";
import monthFixture from "./fixtures/month-overview.json";
import subscriptionsFixture from "./fixtures/subscriptions.json";
import { at, plain } from "./helpers";

// The fixtures are the structured content of the view tools on the demo plan, written and checked by a C# test.
// Typed by annotation, not by assertion: the compiler checks that they match the interfaces. The subscriptions need
// an assertion, since JSON gives their enums as plain strings, not as the union types.
const demoMonth: MonthSummaryOutput = monthFixture;
const demoForecast: CashFlowForecastOutput = forecastFixture;
const demoSubscriptions = subscriptionsFixture as SubscriptionsOutput;

let content: HTMLElement;
beforeEach(() => {
  content = document.createElement("main");
  document.body.replaceChildren(content);
});

const texts = (selector: string) => [...content.querySelectorAll(selector)].map(node => plain(node.textContent));
/** Every row of the table body, every cell as the texts of its parts (a value and its detail, for instance). */
const rows = () => [...content.querySelectorAll("tbody tr")]
  .map(row => [...row.children].map(cell => [...cell.childNodes].map(part => plain(part.textContent))));
const euros = (value: number) => plain(formatEuros(value));

describe("view definitions", () => {
  it.each([
    [monthOverview.tool, "show_month_overview"],
    [cashFlowForecast.tool, "show_cash_flow_forecast"],
    [subscriptions.tool, "show_subscriptions"],
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

    expect(rows()).toEqual([
      [["Entrées"], [euros(planned.incomes)], [euros(2300)]],
      [["Dépenses récurrentes"], [euros(planned.recurringExpenses)], [euros(1300.5)]],
      [["Budgets"], [euros(planned.budgets)], [euros(480.25)]],
      [["Capacité d'épargne"], [euros(planned.savingsCapacity)], [euros(519.25)]],
    ]);
    expect(content.querySelector("tbody tr:last-child")?.className).toBe("total");
    expect(content.querySelector(".note")).not.toBeNull();
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
