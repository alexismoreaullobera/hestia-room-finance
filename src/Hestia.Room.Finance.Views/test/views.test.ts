import { beforeEach, describe, expect, it } from "vitest";
import { cashFlowForecast, chart, MIN_WIDTH } from "../src/cash-flow-forecast";
import { monthOverview } from "../src/month-overview";
import type { CashFlowForecastOutput, MonthSummaryOutput, SubscriptionsOutput } from "../src/shared/outputs";
import { formatEuros } from "../src/shared/format";
import { subscriptions } from "../src/subscriptions";
import forecastFixture from "./fixtures/cash-flow-forecast.json";
import monthFixture from "./fixtures/month-overview.json";
import subscriptionsFixture from "./fixtures/subscriptions.json";
import { plain } from "./helpers";

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
  it("shows the planned and actual savings capacity, then the figures side by side", () => {
    monthOverview.render(demoMonth, content, 640);

    expect(texts(".card .big")).toEqual([
      plain(formatEuros(demoMonth.planned.savingsCapacity)), plain(formatEuros(demoMonth.actual.savingsCapacity))]);
    expect(texts("tbody tr td:first-child")).toEqual(["Entrées", "Dépenses récurrentes", "Budgets", "Capacité d'épargne"]);
    expect(content.querySelector(".note")).not.toBeNull();
  });
});

describe("cash flow forecast", () => {
  it("shows the alert with the buffer to start with, the figures, the chart and the debits", () => {
    cashFlowForecast.render(demoForecast, content, 640);

    expect(demoForecast.hasAlert).toBe(true);
    expect(plain(content.querySelector(".alert")?.textContent)).toContain(`au moins ${plain(formatEuros(demoForecast.cashBuffer))}`);
    expect(content.querySelectorAll(".card")).toHaveLength(3);
    expect(content.querySelectorAll("svg.chart rect.day")).toHaveLength(demoForecast.days.length);
    expect(texts("tbody tr td:nth-child(2)")).toEqual(demoForecast.contributingDebits.map(d => d.name));
  });

  it("says the month starts overdrawn instead of a buffer of 0 €", () => {
    cashFlowForecast.render({ ...demoForecast, startsOverdrawn: true, cashBuffer: 0 }, content, 640);

    const alert = plain(content.querySelector(".alert")?.textContent);
    expect(alert).toContain("démarre déjà à découvert");
    expect(alert).not.toContain("au moins");
  });

  it("shows a calm note and no debits table when there is no alert and no debit", () => {
    cashFlowForecast.render({ ...demoForecast, hasAlert: false, contributingDebits: [] }, content, 640);

    expect(content.querySelector(".calm")?.textContent).toContain("Pas d'alerte");
    expect(content.querySelector(".alert")).toBeNull();
    expect(content.querySelector("h2")).toBeNull();
  });

  it("keeps a name typed by the user as text", () => {
    const trap = `<img src=x onerror="alert(1)">`;
    const debit = { name: trap, date: "2027-03-05", amount: 720 };

    cashFlowForecast.render({ ...demoForecast, contributingDebits: [debit] }, content, 640);

    expect(content.querySelector("img")).toBeNull();
    expect(texts("tbody tr td:nth-child(2)")).toEqual([trap]);
  });
});

describe("chart", () => {
  const day = (date: string, balance: number) => ({ date, balance });
  const numbers = (node: SVGElement) => [...node.querySelectorAll("*")]
    .flatMap(child => [...child.attributes].map(attribute => attribute.value))
    .filter(value => /^-?[\d.]+$/.test(value));

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

    expect(numbers(node).every(value => Number.isFinite(Number(value)))).toBe(true);
    expect(node.querySelector("polyline")?.getAttribute("points")).not.toContain("NaN");
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
    subscriptions.render(demoSubscriptions, content, 640);

    expect(texts(".card .big")).toEqual([
      plain(formatEuros(demoSubscriptions.monthlyTotal)), plain(formatEuros(demoSubscriptions.yearlyTotal)),
      String(demoSubscriptions.subscriptions.length)]);
    expect(content.querySelectorAll("tbody tr")).toHaveLength(demoSubscriptions.subscriptions.length);
    expect(texts("tbody tr td:nth-child(2) .detail")).toContain("mensuel");
    expect(texts("tbody tr td:nth-child(5)")).toContain("sans");
  });

  it("says when there is no subscription", () => {
    subscriptions.render({ ...demoSubscriptions, subscriptions: [] }, content, 640);

    expect(content.textContent).toBe("Aucun abonnement en cours ce mois-ci.");
  });

  it("says when a subscription is not debited this month", () => {
    const first = { ...demoSubscriptions.subscriptions[0]!, dueDate: null };

    subscriptions.render({ ...demoSubscriptions, subscriptions: [first] }, content, 640);

    expect(texts("tbody tr td:nth-child(4)")).toEqual(["pas ce mois-ci"]);
  });
});
