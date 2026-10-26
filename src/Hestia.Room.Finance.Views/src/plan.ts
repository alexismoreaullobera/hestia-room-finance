import type { PlanBudgetOutput, PlanLineOutput, PlanOutput } from "./shared/outputs";
import { amount, element } from "./shared/dom";
import { formatDate, translate } from "./shared/format";
import { recordForm, type Field, type FormValues } from "./shared/form";
import { categories, frequencies, kinds, monthNames } from "./shared/labels";
import { editableTable, type Closing, type EditableRow } from "./shared/table";
import type { ViewContext, ViewDefinition } from "./shared/view";
import "./plan.css";

/** The input tools of the budgets, written as literals: a contract with the server (tested on both sides). */
export const CREATE_BUDGET = "create_budget";
export const UPDATE_BUDGET = "update_budget";
export const DELETE_BUDGET = "delete_budget";

/** The input tools of the recurring lines, literals too. */
export const CREATE_LINE = "create_line";
export const UPDATE_LINE = "update_line";
export const END_LINE = "end_line";
export const DELETE_LINE = "delete_line";

/** The longest name the server accepts (NameLimit.MaxLength): checked here first, for a clear message. */
const MAX_NAME_LENGTH = 100;

/**
 * The check of a name of the plan, here for a clear message before any call: the server checks the same rules (and the
 * characters a field cannot hold anyway, such as line breaks), the view may only be faster. Among the items shown only:
 * for the lines, an ended one is out of the view, the server refusing its name.
 */
function nameCheck(items: readonly { id: string; name: string }[], except: string | null, taken: string) {
  return (name: string): string | null => {
    if (name === "") return "Nom attendu.";
    if (name.length > MAX_NAME_LENGTH) return `${String(MAX_NAME_LENGTH)} caractères au plus.`;
    // Ignoring case, as the server: "Courses" and "courses" would read as the same item.
    const used = items.some(item => item.id !== except && item.name.toLocaleLowerCase("fr") === name.toLocaleLowerCase("fr"));
    return used ? taken : null;
  };
}

function positive(value: number): string | null {
  return value > 0 ? null : "Montant attendu, plus que 0.";
}

/** The two fields of a budget, prefilled with the budget changed, empty for a new one. */
function budgetFields(budgets: readonly PlanBudgetOutput[], budget: PlanBudgetOutput | null): Field[] {
  return [
    { kind: "text", name: "name", label: "Nom", value: budget?.name ?? "",
      check: nameCheck(budgets, budget?.id ?? null, "Un autre budget porte déjà ce nom.") },
    { kind: "amount", name: "plannedAmount", label: "Par mois", value: budget?.plannedAmount ?? null, check: positive },
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
    key: budget.id,
    cells: () => [element("td", { text: budget.name }), amount(budget.plannedAmount, "td")],
    form: (closing: Closing) => recordForm({
      ...closing,
      title: budget.name,
      fields: budgetFields(budgets, budget),
      submit: ({ name, plannedAmount }) => call(UPDATE_BUDGET, { month, budgetId: budget.id, name, plannedAmount }),
    }),
    remove: {
      confirmation: deleteConfirmation(budget),
      // The number of actuals the confirmation announced: the server refuses when it changed meanwhile.
      run: () => call(DELETE_BUDGET, { month, budgetId: budget.id, actualsEntered: budget.actualsEntered }),
    },
  }), {
    label: "Nouveau budget",
    form: (closing: Closing) => recordForm({
      ...closing,
      title: "Nouveau budget",
      fields: budgetFields(budgets, null),
      submit: ({ name, plannedAmount }) => call(CREATE_BUDGET, { month, name, plannedAmount }),
    }),
  });
}

/** The options of a list, from a table of labels (in its order). */
function options<T extends string>(labels: Readonly<Record<T, string>>): { value: string; label: string }[] {
  return Object.entries<string>(labels).map(([value, label]) => ({ value, label }));
}

const monthOptions = monthNames.map((name, index) => ({ value: String(index + 1), label: name }));

/** "2027-02" -> "2027-02-28": the day Terminer proposes, the last one of the month shown. */
function lastDayOf(month: string): string {
  const [year = 0, monthNumber = 1] = month.split("-").map(Number);
  const date = new Date(0);
  date.setUTCFullYear(year, monthNumber, 0);
  return `${month}-${String(date.getUTCDate()).padStart(2, "0")}`;
}

/** When a quarterly or yearly line falls due: "en mars" or "en mars, juin, septembre, décembre". */
function dueMonths({ frequency, anchorMonth }: PlanLineOutput): string | null {
  if (frequency === "Monthly" || anchorMonth === null) return null;
  const months = frequency === "Yearly" ? [anchorMonth] : [0, 3, 6, 9].map(gap => ((anchorMonth - 1 + gap) % 12) + 1).sort((a, b) => a - b);
  return `en ${months.map(month => monthNames[month - 1] ?? String(month)).join(", ")}`;
}

/** What the confirmation of a delete says: the corrections entered for the line go with it. */
function lineDeleteConfirmation({ name, overridesEntered }: PlanLineOutput): string {
  const consequence = overridesEntered === 0 ? ""
    : overridesEntered === 1 ? " Sa correction saisie sera aussi supprimée."
      : ` Ses ${String(overridesEntered)} corrections saisies seront aussi supprimées.`;
  return `Supprimer « ${name} » ?${consequence}`;
}

/** The fields of a line, prefilled with the line changed, or with the defaults of a new one starting the month shown. */
function lineFields(lines: readonly PlanLineOutput[], line: PlanLineOutput | null, month: string): Field[] {
  return [
    { kind: "select", name: "kind", label: "Type", value: line?.kind ?? "Expense", options: options(kinds) },
    { kind: "text", name: "name", label: "Nom", value: line?.name ?? "",
      check: nameCheck(lines, line?.id ?? null, "Une autre ligne porte déjà ce nom.") },
    { kind: "amount", name: "amount", label: "Montant", value: line?.amount ?? null, check: positive },
    { kind: "select", name: "category", label: "Catégorie", value: line?.category ?? "Other", options: options(categories) },
    { kind: "select", name: "frequency", label: "Fréquence", value: line?.frequency ?? "Monthly", options: options(frequencies) },
    { kind: "integer", name: "dayOfMonth", label: "Jour du mois", value: line?.dayOfMonth ?? null, min: 1, max: 31 },
    { kind: "select", name: "anchorMonth", label: "Mois de la première échéance", value: String(line?.anchorMonth ?? 1),
      options: monthOptions, shownWhen: values => values.frequency !== "Monthly" },
    { kind: "date", name: "startDate", label: "Début", value: line?.startDate ?? `${month}-01` },
    { kind: "date", name: "endDate", label: "Fin (facultative)", value: line?.endDate ?? null, optional: true },
    { kind: "checkbox", name: "isSubscription", label: "Abonnement", value: line?.isSubscription ?? false,
      shownWhen: values => values.kind === "Expense" },
    { kind: "date", name: "commitmentEndDate", label: "Fin d'engagement (facultative)", value: line?.commitmentEndDate ?? null,
      optional: true, shownWhen: values => values.kind === "Expense" && values.isSubscription === true },
  ];
}

/** Dates as YYYY-MM-DD compare as text. */
function endBeforeStart({ startDate, endDate }: FormValues) {
  return typeof endDate === "string" && typeof startDate === "string" && endDate < startDate
    ? { field: "endDate", message: "La fin ne peut pas précéder le début." }
    : null;
}

/** The arguments of create_line and update_line: the month of the list as a number, the fields not shown as null. */
function lineArguments(values: FormValues): Readonly<Record<string, unknown>> {
  return { ...values, anchorMonth: values.anchorMonth === null ? null : Number(values.anchorMonth) };
}

function lineCells(line: PlanLineOutput): HTMLElement[] {
  const details = [
    line.kind === "Income" ? "entrée" : null,
    dueMonths(line),
    line.endDate ? `jusqu'au ${formatDate(line.endDate)}` : null,
    line.commitmentEndDate ? `engagement jusqu'au ${formatDate(line.commitmentEndDate)}` : null,
  ].filter(detail => detail !== null);
  return [
    element("td", { text: String(line.dayOfMonth) }),
    element("td", {},
      line.name,
      ...(line.isSubscription ? [element("span", { className: "badge subscription", text: "abonnement" })] : []),
      ...(details.length > 0 ? [element("span", { className: "detail", text: details.join(" · ") })] : [])),
    element("td", { text: translate(frequencies, line.frequency) }),
    amount(line.amount, "td"),
  ];
}

function linesTable({ month, lines }: PlanOutput, call: ViewContext["call"]): HTMLElement[] {
  const headers = [{ text: "Jour" }, { text: "Ligne" }, { text: "Fréquence" }, { text: "Montant", amount: true }];
  return editableTable("lines", "Lignes récurrentes", headers, lines, (line): EditableRow => ({
    key: line.id,
    cells: () => lineCells(line),
    form: (closing: Closing) => recordForm({
      ...closing,
      title: line.name,
      fields: lineFields(lines, line, month),
      check: endBeforeStart,
      submit: values => call(UPDATE_LINE, { month, lineId: line.id, ...lineArguments(values) }),
    }),
    actions: [{
      label: "Terminer",
      className: "end",
      form: (closing: Closing) => recordForm({
        ...closing,
        title: `Terminer ${line.name}`,
        fields: [{ kind: "date", name: "endDate", label: "Dernier jour", value: lastDayOf(month) }],
        check: ({ endDate }) => typeof endDate === "string" && endDate < line.startDate
          ? { field: "endDate", message: "La fin ne peut pas précéder le début de la ligne." }
          : null,
        submit: ({ endDate }) => call(END_LINE, { month, lineId: line.id, endDate }),
      }),
    }],
    remove: {
      confirmation: lineDeleteConfirmation(line),
      // The number of corrections the confirmation announced: the server refuses when it changed meanwhile.
      run: () => call(DELETE_LINE, { month, lineId: line.id, overridesEntered: line.overridesEntered }),
    },
  }), {
    label: "Nouvelle ligne",
    form: (closing: Closing) => recordForm({
      ...closing,
      title: "Nouvelle ligne",
      fields: lineFields(lines, null, month),
      check: endBeforeStart,
      submit: values => call(CREATE_LINE, { month, ...lineArguments(values) }),
    }),
  });
}

export const plan: ViewDefinition<PlanOutput> = {
  name: "Plan",
  tool: "show_plan",
  render: (data, content, { call }) => {
    content.append(
      ...budgetsTable(data, call),
      ...linesTable(data, call),
      element("p", {
        className: "note",
        text: "Un budget ou une ligne vaut pour tous les mois : le modifier change aussi les mois passés. Terminer une ligne "
          + "garde son passé ; supprimer efface aussi ses réels ou ses corrections saisis.",
      }));
  },
};
