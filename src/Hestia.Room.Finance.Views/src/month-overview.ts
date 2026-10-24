import type { BudgetActualOutput, LineActualOutput, MonthSummaryOutput, SummaryFigures } from "./shared/outputs";
import { amount, card, element } from "./shared/dom";
import { formatDay, formatMonth } from "./shared/format";
import { amountForm, type FormAction } from "./shared/form";
import type { ViewContext, ViewDefinition } from "./shared/view";
import "./month-overview.css";

/** The input tools of the actuals, written as literals: a contract with the server (tested on both sides). */
export const SET_BUDGET_ACTUAL = "set_budget_actual";
export const SET_LINE_ACTUAL = "set_line_actual";

/** The savings capacity of one reading, as falling due this month and on an average month. */
function savings(title: string, figures: SummaryFigures): HTMLElement {
  return card(title, amount(figures.savingsCapacity),
    element("span", {}, "Sur un mois moyen : ", amount(figures.smoothedSavingsCapacity)));
}

function row(label: string, planned: number, actual: number, className = ""): HTMLElement {
  return element("tr", { className },
    element("td", { text: label }), amount(planned, "td"), amount(actual, "td"));
}

/** What an editable row shows and how it is entered: one budget, or one recurring line due this month. */
interface EditableRow {
  /** The cells before the button: the row as read. */
  cells: () => HTMLElement[];
  /** The form that replaces the row, opened by Modifier. */
  form: (close: () => void, focusAfterSave: () => void) => ReturnType<typeof amountForm>;
}

/**
 * A row of a table of actuals: its cells and a Modifier button, which turns the row into the amount form of the kit
 * (on the whole width of the table). Annuler or Escape gives the row back; a save redraws the whole view from the result.
 */
function editableRow(table: HTMLElement, index: number, columns: number, { cells, form }: EditableRow): HTMLElement {
  const node = element("tr");
  // After a save the view is redrawn, this row with it: the focus goes to the Modifier button of the same row.
  const focusAfterSave = () => {
    document.querySelectorAll<HTMLElement>(`.${table.className} tbody tr`)[index]?.querySelector<HTMLButtonElement>(".edit")?.focus();
  };

  function showValue(focus = false): void {
    const edit = element("button", { className: "edit", attributes: { type: "button" } }, "Modifier");
    edit.addEventListener("click", showForm);
    node.classList.remove("editing");
    node.replaceChildren(...cells(), element("td", { className: "action" }, edit));
    if (focus) edit.focus();
  }

  function showForm(): void {
    const { form: opened, focus } = form(() => { showValue(true); }, focusAfterSave);
    node.classList.add("editing");
    const cell = element("td", {}, opened);
    cell.colSpan = columns;
    node.replaceChildren(cell);
    focus();
  }

  showValue();
  return node;
}

/** Calls an input tool, then puts the focus back once its result is shown. */
function saving(call: ViewContext["call"], focusAfterSave: () => void, tool: string, args: Readonly<Record<string, unknown>>) {
  return async () => {
    const shown = await call(tool, args);
    if (shown) focusAfterSave();
    return shown;
  };
}

function budgetsTable(data: MonthSummaryOutput, call: ViewContext["call"]): HTMLElement[] {
  if (data.budgets.length === 0) return [];
  const body = element("tbody");
  const table = element("table", { className: "budgets" },
    element("thead", {}, element("tr", {},
      element("th", { text: "Budget" }),
      element("th", { className: "amount", text: "Prévu" }),
      element("th", { className: "amount", text: "Réel" }),
      element("th", {}))),
    body);
  body.append(...data.budgets.map((budget: BudgetActualOutput, index) => editableRow(table, index, 4, {
    cells: () => [
      element("td", { text: budget.name }),
      amount(budget.planned, "td"),
      budget.entered ? amount(budget.actual, "td") : element("td", { className: "amount muted", text: "comme prévu" }),
    ],
    form: (close, focusAfterSave) => amountForm({
      title: budget.name,
      label: `Réel de ${budget.name} en ${formatMonth(data.month)}`,
      value: budget.actual,
      submit: (actualAmount) =>
        saving(call, focusAfterSave, SET_BUDGET_ACTUAL, { month: data.month, budgetId: budget.id, actualAmount })(),
      cancel: close,
      actions: budget.entered
        ? [{ label: "Comme prévu", run: saving(call, focusAfterSave, SET_BUDGET_ACTUAL, { month: data.month, budgetId: budget.id, actualAmount: null }) }]
        : [],
    }),
  })));
  return [element("h2", { text: "Budgets" }), table];
}

const statusBadges: Readonly<Record<LineActualOutput["status"], string>> = {
  AsPlanned: "",
  Corrected: "corrigé",
  Skipped: "n'a pas eu lieu",
};

function linesTable(data: MonthSummaryOutput, call: ViewContext["call"]): HTMLElement[] {
  if (data.lines.length === 0) return [];
  const body = element("tbody");
  const table = element("table", { className: "lines" },
    element("thead", {}, element("tr", {},
      element("th", { text: "Date" }),
      element("th", { text: "Ligne" }),
      element("th", { className: "amount", text: "Prévu" }),
      element("th", { className: "amount", text: "Réel" }),
      element("th", {}))),
    body);
  body.append(...data.lines.map((line: LineActualOutput, index) => editableRow(table, index, 5, {
    cells: () => [
      element("td", { text: formatDay(line.date) }),
      element("td", {}, line.name, ...(line.kind === "Income" ? [element("span", { className: "detail", text: "entrée" })] : [])),
      amount(line.planned, "td"),
      line.status === "AsPlanned"
        ? element("td", { className: "amount muted", text: "comme prévu" })
        : element("td", { className: "amount" },
          amount(line.actual),
          element("span", { className: `badge ${line.status === "Skipped" ? "skipped" : "corrected"}`, text: statusBadges[line.status] })),
    ],
    form: (close, focusAfterSave) => {
      const send = (args: Readonly<Record<string, unknown>>) =>
        saving(call, focusAfterSave, SET_LINE_ACTUAL, { month: data.month, lineId: line.id, ...args });
      const actions: FormAction[] = [];
      if (line.status !== "Skipped") actions.push({ label: "N'a pas eu lieu", run: send({ status: "Skipped" }) });
      if (line.status !== "AsPlanned") actions.push({ label: "Comme prévu", run: send({ status: "AsPlanned" }) });
      return amountForm({
        title: line.name,
        label: `Réel de ${line.name} le ${formatDay(line.date)}`,
        // A skipped line is prefilled with its planned amount: typing a correction starts from the plan.
        value: line.status === "Skipped" ? line.planned : line.actual,
        submit: (actualAmount) => send({ status: "Corrected", actualAmount })(),
        cancel: close,
        actions,
      });
    },
  })));
  return [element("h2", { text: "Lignes du mois" }), table];
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
