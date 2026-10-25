import type { PlanBudgetOutput, PlanOutput } from "./shared/outputs";
import { amount, element } from "./shared/dom";
import { recordForm, type Field } from "./shared/form";
import { editableTable, type Closing, type EditableRow } from "./shared/table";
import type { ViewContext, ViewDefinition } from "./shared/view";
import "./plan.css";

/** The input tools of the budgets, written as literals: a contract with the server (tested on both sides). */
export const CREATE_BUDGET = "create_budget";
export const UPDATE_BUDGET = "update_budget";
export const DELETE_BUDGET = "delete_budget";

/** The longest name the server accepts (NameLimit.MaxLength): checked here first, for a clear message. */
const MAX_NAME_LENGTH = 100;

/**
 * The check of a budget name, here for a clear message before any call: the server checks the same rules (and the
 * characters a field cannot hold anyway, such as line breaks), the view may only be faster.
 */
function nameCheck(budgets: readonly PlanBudgetOutput[], except: string | null) {
  return (name: string): string | null => {
    if (name === "") return "Nom attendu.";
    if (name.length > MAX_NAME_LENGTH) return `${String(MAX_NAME_LENGTH)} caractères au plus.`;
    // Ignoring case, as the server: "Courses" and "courses" would read as the same budget.
    const taken = budgets.some(budget => budget.id !== except && budget.name.toLocaleLowerCase("fr") === name.toLocaleLowerCase("fr"));
    return taken ? "Un autre budget porte déjà ce nom." : null;
  };
}

function positive(value: number): string | null {
  return value > 0 ? null : "Montant attendu, plus que 0.";
}

/** The two fields of a budget, prefilled with the budget changed, empty for a new one. */
function budgetFields(budgets: readonly PlanBudgetOutput[], budget: PlanBudgetOutput | null): Field[] {
  return [
    { kind: "text", label: "Nom", value: budget?.name ?? "", check: nameCheck(budgets, budget?.id ?? null) },
    { kind: "amount", label: "Par mois", value: budget?.plannedAmount ?? null, check: positive },
  ];
}

/** What the confirmation of a delete says: the actuals entered for the budget go with it. */
function deleteConfirmation({ name, actualsEntered }: PlanBudgetOutput): string {
  const consequence = actualsEntered === 0 ? ""
    : actualsEntered === 1 ? " Son réel saisi sera aussi supprimé."
      : ` Ses ${String(actualsEntered)} réels saisis seront aussi supprimés.`;
  return `Supprimer « ${name} » ?${consequence}`;
}

function budgetsTable({ month, budgets }: PlanOutput, call: ViewContext["call"]): HTMLElement[] {
  const headers = [{ text: "Budget" }, { text: "Par mois", amount: true }];
  return editableTable("budgets", "Budgets", headers, budgets, (budget): EditableRow => ({
    cells: () => [element("td", { text: budget.name }), amount(budget.plannedAmount, "td")],
    form: (closing: Closing) => recordForm({
      ...closing,
      title: budget.name,
      fields: budgetFields(budgets, budget),
      submit: ([name, plannedAmount]) => call(UPDATE_BUDGET, { month, budgetId: budget.id, name, plannedAmount }),
    }),
    remove: { confirmation: deleteConfirmation(budget), run: () => call(DELETE_BUDGET, { month, budgetId: budget.id }) },
  }), {
    label: "Nouveau budget",
    form: (closing: Closing) => recordForm({
      ...closing,
      title: "Nouveau budget",
      fields: budgetFields(budgets, null),
      submit: ([name, plannedAmount]) => call(CREATE_BUDGET, { month, name, plannedAmount }),
    }),
  });
}

export const plan: ViewDefinition<PlanOutput> = {
  name: "Plan",
  tool: "show_plan",
  render: (data, content, { call }) => {
    content.append(
      ...budgetsTable(data, call),
      element("p", {
        className: "note",
        text: "Un budget vaut pour tous les mois : le modifier change aussi les mois passés. Le supprimer efface ses réels saisis.",
      }));
  },
};
