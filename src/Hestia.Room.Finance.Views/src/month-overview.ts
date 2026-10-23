import type { MonthSummaryOutput, SummaryFigures } from "./shared/outputs";
import { amount, card, element } from "./shared/dom";
import type { ViewDefinition } from "./shared/view";
import "./month-overview.css";

/** The savings capacity of one reading, as falling due this month and on an average month. */
function savings(title: string, figures: SummaryFigures): HTMLElement {
  return card(title, amount(figures.savingsCapacity),
    element("span", {}, "Sur un mois moyen : ", amount(figures.smoothedSavingsCapacity)));
}

function row(label: string, planned: number, actual: number, className = ""): HTMLElement {
  return element("tr", { className },
    element("td", { text: label }), amount(planned, "td"), amount(actual, "td"));
}

export const monthOverview: ViewDefinition<MonthSummaryOutput> = {
  name: "Synthèse du mois",
  tool: "show_month_overview",
  render: ({ planned, actual }, content) => {
    content.append(
      element("div", { className: "cards" },
        savings("Capacité d'épargne prévue", planned),
        savings("Capacité d'épargne réelle", actual)),
      element("table", {},
        element("thead", {}, element("tr", {},
          element("th", { text: "Ce mois-ci" }),
          element("th", { className: "amount", text: "Prévu" }),
          element("th", { className: "amount", text: "Réel" }))),
        element("tbody", {},
          row("Entrées", planned.incomes, actual.incomes),
          row("Dépenses récurrentes", planned.recurringExpenses, actual.recurringExpenses),
          row("Budgets", planned.budgets, actual.budgets),
          row("Capacité d'épargne", planned.savingsCapacity, actual.savingsCapacity, "total"))),
      element("p", {
        className: "note",
        text: "Réel : les montants saisis pour ce mois (réels des budgets, corrections des lignes), le prévu ailleurs. "
          + "Mois moyen : les lignes trimestrielles et annuelles lissées sur l'année.",
      }));
  },
};
