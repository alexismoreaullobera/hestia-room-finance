import type { BudgetActualOutput, LineActualOutput, MonthSummaryOutput, SummaryFigures } from "./shared/outputs";
import { amount, card, element } from "./shared/dom";
import { formatDay, formatMonth, translate } from "./shared/format";
import { amountForm, type FormAction } from "./shared/form";
import { editableTable, type EditableRow } from "./shared/table";
import type { ViewContext, ViewDefinition } from "./shared/view";
import "./month-overview.css";

/** The input tools of the actuals, written as literals: a contract with the server (tested on both sides). */
export const SET_BUDGET_ACTUAL = "set_budget_actual";
export const SET_LINE_ACTUAL = "set_line_actual";

/** The classes of the two tables of actuals, also used to find a row again after a redraw. */
const BUDGETS = "budgets";
const LINES = "lines";

/** The savings capacity of one reading, as falling due this month and on an average month. */
function savings(title: string, figures: SummaryFigures): HTMLElement {
  return card(title, amount(figures.savingsCapacity),
    element("span", {}, "Sur un mois moyen : ", amount(figures.smoothedSavingsCapacity)));
}

function row(label: string, planned: number, actual: number, className = ""): HTMLElement {
  return element("tr", { className },
    element("td", { text: label }), amount(planned, "td"), amount(actual, "td"));
}

/** The actual of a budget or a line nothing was entered for. */
function asPlannedCell(): HTMLElement {
  return element("td", { className: "amount muted", text: "comme prévu" });
}

function budgetsTable(data: MonthSummaryOutput, call: ViewContext["call"]): HTMLElement[] {
  const headers = [{ text: "Budget" }, { text: "Prévu", amount: true }, { text: "Réel", amount: true }];
  return editableTable(BUDGETS, "Budgets", headers, data.budgets, (budget: BudgetActualOutput): EditableRow => {
    const save = (actualAmount: number | null) => () =>
      call(SET_BUDGET_ACTUAL, { month: data.month, budgetId: budget.id, actualAmount });
    return {
      key: budget.id,
      cells: () => [
        element("td", { text: budget.name }),
        amount(budget.planned, "td"),
        budget.entered ? amount(budget.actual, "td") : asPlannedCell(),
      ],
      form: (closing) => amountForm({
        ...closing,
        title: budget.name,
        label: `Réel de ${budget.name} en ${formatMonth(data.month)}`,
        value: budget.actual,
        submit: (actualAmount) => save(actualAmount)(),
        // Back to the plan: offered only when an actual was entered.
        actions: budget.entered ? [{ label: "Comme prévu", run: save(null) }] : [],
      }),
    };
  });
}

/** The badge of a line that did not go as planned (none for AsPlanned). */
const statusBadges: Readonly<Record<string, string>> = { Corrected: "corrigé", Skipped: "n'a pas eu lieu" };

function linesTable(data: MonthSummaryOutput, call: ViewContext["call"]): HTMLElement[] {
  const headers = [{ text: "Date" }, { text: "Ligne" }, { text: "Prévu", amount: true }, { text: "Réel", amount: true }];
  return editableTable(LINES, "Lignes du mois", headers, data.lines, (line: LineActualOutput): EditableRow => {
    const save = (args: Readonly<Record<string, unknown>>) => () =>
      call(SET_LINE_ACTUAL, { month: data.month, lineId: line.id, ...args });
    return {
      key: line.id,
      cells: () => [
        element("td", { text: formatDay(line.date) }),
        element("td", {}, line.name, ...(line.kind === "Income" ? [element("span", { className: "detail", text: "entrée" })] : [])),
        amount(line.planned, "td"),
        line.status === "AsPlanned"
          ? asPlannedCell()
          : element("td", { className: "amount" },
            amount(line.actual),
            element("span", {
              className: `badge ${line.status === "Skipped" ? "skipped" : "corrected"}`,
              text: translate(statusBadges, line.status),
            })),
      ],
      form: (closing) => {
        // Only the actions that change something.
        const actions: FormAction[] = [];
        if (line.status !== "Skipped") actions.push({ label: "N'a pas eu lieu", run: save({ status: "Skipped" }) });
        if (line.status !== "AsPlanned") actions.push({ label: "Comme prévu", run: save({ status: "AsPlanned" }) });
        return amountForm({
          ...closing,
          title: line.name,
          label: `Réel de ${line.name} le ${formatDay(line.date)}`,
          // A skipped line is prefilled with its planned amount: typing a correction starts from the plan.
          value: line.status === "Skipped" ? line.planned : line.actual,
          submit: (actualAmount) => save({ status: "Corrected", actualAmount })(),
          actions,
        });
      },
    };
  });
}

export const monthOverview: ViewDefinition<MonthSummaryOutput> = {
  name: "Synthèse du mois",
  tool: "show_month_overview",
  render: (data, content, { call }) => {
    const { planned, actual } = data;
    content.append(
      element("div", { className: "cards" },
        savings("Capacité d'épargne prévue", planned),
        savings("Capacité d'épargne réelle", actual)),
      element("table", { className: "totals" },
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
      }),
      ...budgetsTable(data, call),
      ...linesTable(data, call));
  },
};
