import type { SubscriptionOutput, SubscriptionsOutput } from "./shared/outputs";
import { amount, card, element } from "./shared/dom";
import { formatDate, formatDay, formatEuros, translate } from "./shared/format";
import { categories, frequencies } from "./shared/labels";
import type { ViewDefinition } from "./shared/view";
import "./subscriptions.css";

function subscriptionRow(subscription: SubscriptionOutput): HTMLElement {
  return element("tr", {},
    element("td", {},
      subscription.name,
      element("span", { className: "detail", text: translate(categories, subscription.category) })),
    // The amount as plain text, not amount(): a subscription is never negative, and it shares the cell with its frequency.
    element("td", { className: "amount" },
      formatEuros(subscription.amount),
      element("span", { className: "detail", text: translate(frequencies, subscription.frequency) })),
    amount(subscription.monthlyEquivalent, "td"),
    element("td", {
      className: subscription.dueDate ? "" : "muted",
      text: subscription.dueDate ? formatDay(subscription.dueDate) : "pas ce mois-ci",
    }),
    element("td", {
      className: subscription.commitmentEndDate ? "" : "muted",
      text: subscription.commitmentEndDate ? `jusqu'au ${formatDate(subscription.commitmentEndDate)}` : "sans",
    }));
}

export const subscriptions: ViewDefinition<SubscriptionsOutput> = {
  name: "Abonnements",
  tool: "show_subscriptions",
  render: ({ subscriptions: items, monthlyTotal, yearlyTotal }, content) => {
    if (items.length === 0) {
      content.append(element("p", { className: "muted", text: "Aucun abonnement en cours ce mois-ci." }));
      return;
    }
    content.append(
      element("div", { className: "cards" },
        card("Par mois", formatEuros(monthlyTotal)),
        card("Par an", formatEuros(yearlyTotal)),
        card("Abonnements", String(items.length))),
      element("div", { className: "table-wrap" },
        element("table", {},
          element("thead", {}, element("tr", {},
            element("th", { text: "Abonnement" }),
            element("th", { className: "amount", text: "Montant" }),
            element("th", { className: "amount", text: "Par mois" }),
            element("th", { text: "Débit" }),
            element("th", { text: "Engagement" }))),
          element("tbody", {}, ...items.map(subscriptionRow)))));
  },
};
