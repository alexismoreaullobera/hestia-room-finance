import type { PlanOutput } from "./shared/outputs";
import { amount, element } from "./shared/dom";
import type { ViewDefinition } from "./shared/view";

// The plan itself: read only for now, its editing (and its visual choice) comes with the next module of T11.
export const plan: ViewDefinition<PlanOutput> = {
  name: "Plan",
  tool: "show_plan",
  render: ({ budgets }, content) => {
    if (budgets.length === 0) {
      content.append(element("p", { className: "muted", text: "Aucun budget dans le plan." }));
      return;
    }
    content.append(
      element("h2", { text: "Budgets" }),
      element("table", { className: "budgets" },
        element("thead", {}, element("tr", {},
          element("th", { text: "Budget" }),
          element("th", { className: "amount", text: "Par mois" }))),
        element("tbody", {}, ...budgets.map(budget => element("tr", {},
          element("td", { text: budget.name }),
          amount(budget.plannedAmount, "td"))))));
  },
};
