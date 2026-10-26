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

/**
 * The refusals of the input tools of the plan, as the server writes them (ToolErrors, checked equal by a C# test), and
 * what the view says of each: some come from the server alone, the view seeing only the lines of the month and after.
 */
export const REFUSALS: readonly (readonly [server: string, view: string])[] = [
  ["Another recurring line already has this name over the same period.",
    "Une autre ligne porte déjà ce nom sur la même période, peut-être une ligne terminée avant ce mois."],
  ["Another budget already has this name.", "Un autre budget porte déjà ce nom."],
  ["The actuals of this budget changed since the confirmation",
    "Des réels ont été saisis entre-temps : rien n'a été supprimé. Changez de mois puis revenez pour voir le plan à jour."],
  ["The corrections of this line changed since the confirmation",
    "Des corrections ont été saisies entre-temps : rien n'a été supprimé. Changez de mois puis revenez pour voir le plan à jour."],
];

/** The longest name the server accepts (NameLimit.MaxLength): checked here first, for a clear message. */
const MAX_NAME_LENGTH = 100;

/**
 * The checks of a name of the plan, here for a clear message before any call: the server checks the same rules (and the
 * characters a field cannot hold anyway, such as line breaks), the view may only be faster.
 */
function nameRule(name: string): string | null {
  if (name === "") return "Nom attendu.";
  return name.length > MAX_NAME_LENGTH ? `${String(MAX_NAME_LENGTH)} caractères au plus.` : null;
}

/** Ignoring case, as the server: "Courses" and "courses" would read as the same item. */
const sameName = (name: string, other: string) => name.toLocaleLowerCase("fr") === other.toLocaleLowerCase("fr");

function budgetNameCheck(budgets: readonly PlanBudgetOutput[], except: string | null) {
  return (name: string): string | null =>
    nameRule(name) ?? (budgets.some(budget => budget.id !== except && sameName(budget.name, name)) ? "Un autre budget porte déjà ce nom." : null);
}

function positive(value: number): string | null {
  return value > 0 ? null : "Montant attendu, plus que 0.";
}

/** The two fields of a budget, prefilled with the budget changed, empty for a new one. */
function budgetFields(budgets: readonly PlanBudgetOutput[], budget: PlanBudgetOutput | null): Field[] {
  return [
    { kind: "text", name: "name", label: "Nom", value: budget?.name ?? "", check: budgetNameCheck(budgets, budget?.id ?? null) },
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
function lineFields(line: PlanLineOutput | null, month: string): Field[] {
  return [
    { kind: "select", name: "kind", label: "Type", value: line?.kind ?? "Expense", options: options(kinds) },
    { kind: "text", name: "name", label: "Nom", value: line?.name ?? "", check: nameRule },
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

/** Dates as YYYY-MM-DD compare as text; a line without end runs forever. */
function runTogether(start: string, end: string | null, otherStart: string, otherEnd: string | null): boolean {
  return start <= (otherEnd ?? "9999-12-31") && otherStart <= (end ?? "9999-12-31");
}

/**
 * The checks of a line across its fields: an end never before the start, and a name no other line shown uses at the
 * same time (two lines share a name only one after the other, such as a rent ended in December and one from January).
 */
function lineCheck(lines: readonly PlanLineOutput[], except: string | null) {
  return ({ name, startDate, endDate }: FormValues) => {
    if (typeof startDate !== "string" || typeof name !== "string") return null;
    const end = typeof endDate === "string" ? endDate : null;
    if (end !== null && end < startDate) return { field: "endDate", message: "La fin ne peut pas précéder le début." };
    const taken = lines.some(other => other.id !== except && sameName(other.name, name)
      && runTogether(startDate, end, other.startDate, other.endDate));
    return taken ? { field: "name", message: "Une autre ligne porte déjà ce nom sur la même période." } : null;
  };
}

/** The arguments of create_line and update_line: the month of the list as a number, the fields not shown as null. */
function lineArguments(values: FormValues): Readonly<Record<string, unknown>> {
  return { ...values, anchorMonth: values.anchorMonth === null ? null : Number(values.anchorMonth) };
}

function lineCells(line: PlanLineOutput, lastDay: string): HTMLElement[] {
  const details = [
    line.kind === "Income" ? "entrée" : null,
    // Saved for later: in sight from the month shown on, with its start.
    line.startDate > lastDay ? `à partir du ${formatDate(line.startDate)}` : null,
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
    cells: () => lineCells(line, lastDayOf(month)),
    form: (closing: Closing) => recordForm({
      ...closing,
      title: line.name,
      fields: lineFields(line, month),
      check: lineCheck(lines, line.id),
      submit: values => call(UPDATE_LINE, { month, lineId: line.id, ...lineArguments(values) }),
    }),
    actions: [{
      label: "Terminer",
      className: "end",
      form: (closing: Closing) => recordForm({
        ...closing,
        title: `Terminer ${line.name}`,
        // An ended line keeps its end: a quick Enregistrer never extends it.
        fields: [{ kind: "date", name: "endDate", label: "Dernier jour", value: line.endDate ?? lastDayOf(month) }],
        check: ({ endDate }) => {
          if (typeof endDate !== "string") return null;
          if (endDate < line.startDate) return { field: "endDate", message: "La fin ne peut pas précéder le début de la ligne." };
          return lineCheck(lines, line.id)({ name: line.name, startDate: line.startDate, endDate });
        },
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
      fields: lineFields(null, month),
      check: lineCheck(lines, null),
      submit: values => call(CREATE_LINE, { month, ...lineArguments(values) }),
    }),
  });
}

export const plan: ViewDefinition<PlanOutput> = {
  name: "Plan",
  tool: "show_plan",
  explain: refusal => REFUSALS.find(([server]) => refusal.includes(server))?.[1] ?? null,
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
