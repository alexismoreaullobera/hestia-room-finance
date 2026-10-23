import { afterEach, describe, expect, it } from "vitest";
import {
  amount, applyHostTheme, card, element, formatDate, formatDay, formatEuros, formatMonth, shiftMonth, svg, translate,
} from "../src/shared/view";
import { plain } from "./helpers";

describe("formats", () => {
  it.each([
    ["2026-12", 1, "2027-01"],
    ["2026-01", -1, "2025-12"],
    ["2026-10", -10, "2025-12"],
    ["0001-01", -1, "0000-12"],
    ["9999-12", 1, "10000-01"],
  ])("shiftMonth(%s, %i) is %s", (month, delta, expected) => {
    // The only values a view sends to the server: the server rejects the out-of-range ones (0000-12, 10000-01).
    expect(shiftMonth(month, delta)).toBe(expected);
  });

  it("formats months, days, dates and euros in French", () => {
    expect(formatMonth("2026-10")).toBe("Octobre 2026");
    expect(formatMonth("2027-08")).toBe("Août 2027");
    // Not 1901: Date.UTC would read the years 0 to 99 as 1900 to 1999.
    expect(formatMonth("0001-01")).toBe("Janvier 1");
    expect(formatDay("2026-10-05")).toBe("05/10");
    expect(formatDate("2026-12-31")).toBe("31/12/2026");
    expect(plain(formatEuros(-1192.41))).toBe("-1 192,41 €");
  });

  it("translate never returns an inherited property", () => {
    const table = { Monthly: "mensuel" };

    expect(translate(table, "Monthly")).toBe("mensuel");
    expect(translate(table, "constructor")).toBe("constructor");
    expect(translate(table, "Weekly")).toBe("Weekly");
  });
});

describe("element and svg", () => {
  const trap = `<img src=x onerror="alert(1)">`;

  it("element writes text as text, never as markup", () => {
    const node = element("p", { text: trap }, element("span", {}, trap), trap);

    expect(node.querySelector("img")).toBeNull();
    expect(node.textContent).toBe(trap + trap + trap);
  });

  it("svg writes text as text, never as markup", () => {
    const node = svg("text", {}, trap);

    expect(node.querySelector("img")).toBeNull();
    expect(node.textContent).toBe(trap);
  });

  it.each(["href", "src", "style", "onclick", "onerror", "xlink:href", "srcdoc", "formaction"])(
    "refuses the attribute %s", (name) => {
      expect(() => element("a", { attributes: { [name]: "javascript:alert(1)" } })).toThrow(/not allowed/);
      expect(() => svg("a", { [name]: "javascript:alert(1)" })).toThrow(/not allowed/);
    });

  it("accepts presentation and accessibility attributes", () => {
    const node = svg("rect", { x: 1, width: 2, "aria-label": "Courbe", role: "img" });

    expect(node.getAttribute("x")).toBe("1");
    expect(node.getAttribute("aria-label")).toBe("Courbe");
  });

  it("amount is red when negative", () => {
    expect(amount(-1).className).toBe("amount negative");
    expect(amount(0).className).toBe("amount");
    expect(amount(5, "td").tagName).toBe("TD");
  });

  it("card has its detail only when given", () => {
    expect(card("Par mois", "91,37 €").querySelector(".detail")).toBeNull();
    expect(card("Point bas", "-1 €", "le 27/10").querySelector(".detail")?.textContent).toBe("le 27/10");
  });
});

describe("applyHostTheme", () => {
  afterEach(() => delete document.documentElement.dataset["theme"]);

  it("follows a light or dark host theme", () => {
    applyHostTheme({ theme: "dark" });
    expect(document.documentElement.dataset["theme"]).toBe("dark");
  });

  it("ignores a missing or unknown theme", () => {
    applyHostTheme(undefined);
    applyHostTheme({ theme: "sepia" });
    expect(document.documentElement.dataset["theme"]).toBeUndefined();
  });
});
