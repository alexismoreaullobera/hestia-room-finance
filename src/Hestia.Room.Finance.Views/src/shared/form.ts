// The forms of the views: fields (texts, amounts, whole numbers, dates, lists, check boxes), Enregistrer, optional other
// actions (such as "Comme prévu"), Annuler. Built with the DOM kit; every value the user types goes through it, so the
// forms all behave the same way.
import { element } from "./dom";
import { formatAmount, parseEuros } from "./format";
import type { CallResult } from "./view";
import "./form.css";

/** Another way to answer than the fields, such as "Comme prévu" or "N'a pas eu lieu": a button with its own input call. */
export interface FormAction {
  label: string;
  /** Calls the input tool; resolves as ViewContext.call does. */
  run: () => Promise<CallResult>;
}

/** What a form sends: each value under the name of its field (null for a field not shown, or an optional one left empty). */
export type FieldValue = string | number | boolean | null;
export type FormValues = Readonly<Record<string, FieldValue>>;

interface FieldBase {
  /** The key of its value in what the form sends: the name of the argument of the input tool. */
  name: string;
  label: string;
  /**
   * Whether the field is shown, from the values being typed (such as the month of a yearly line only): checked again at
   * each change. A field not shown is neither checked nor sent (its value is null, false for a check box).
   */
  shownWhen?: (values: FormValues) => boolean;
}

/** A text field, such as a name: trimmed; its check gives the message of a value refused, or null. */
export interface TextField extends FieldBase {
  kind: "text";
  value: string;
  check?: (text: string) => string | null;
}

/** An amount field: prefilled with formatAmount (empty for null, a new item), read back by parseEuros; its check as above. */
export interface AmountField extends FieldBase {
  kind: "amount";
  value: number | null;
  check?: (amount: number) => string | null;
}

/** A whole number between two bounds, such as a day of the month. */
export interface IntegerField extends FieldBase {
  kind: "integer";
  value: number | null;
  min: number;
  max: number;
}

/** A date, as YYYY-MM-DD (the format of the outputs and of the tools); an optional one may be left empty (null). */
export interface DateField extends FieldBase {
  kind: "date";
  value: string | null;
  optional?: boolean;
}

/** One value out of a fixed list, each with its label (such as the kinds or the categories of a line). */
export interface SelectField extends FieldBase {
  kind: "select";
  value: string;
  options: readonly { value: string; label: string }[];
}

export interface CheckboxField extends FieldBase {
  kind: "checkbox";
  value: boolean;
}

export type Field = TextField | AmountField | IntegerField | DateField | SelectField | CheckboxField;

/** A refusal involving several fields (an end before the start): its message, and the field to focus. */
export interface FormProblem {
  field: string;
  message: string;
}

export interface FormOptions {
  /** Visible title of the form. */
  title: string;
  fields: readonly Field[];
  /** A rule across the fields, checked once each field is valid on its own. */
  check?: (values: FormValues) => FormProblem | null;
  /** Calls the input tool with the values typed; resolves as ViewContext.call does. */
  submit: (values: FormValues) => Promise<CallResult>;
  /** Closes the form without saving (Annuler, Escape). */
  cancel: () => void;
  actions?: readonly FormAction[];
  /**
   * Called once a result is shown (the view redrawn, this form with it): typically puts the focus back on the button
   * that opened the form, in the new rendering.
   */
  afterShown?: () => void;
}

export interface FormHandle {
  form: HTMLFormElement;
  /** Focuses the first field with its text selected, once the form is in the page: typing replaces the value at once. */
  focus: () => void;
}

type Control = HTMLInputElement | HTMLSelectElement;

interface Entry {
  field: Field;
  control: Control;
  /** What holds the field and its label: hidden with the field. */
  holder: HTMLElement;
}

const DATE = /^\d{4}-\d{2}-\d{2}$/;

/** A real day of the calendar, written as YYYY-MM-DD (not 2027-02-30). */
function isDate(text: string): boolean {
  if (!DATE.test(text)) return false;
  const [year, month, day] = text.split("-").map(Number) as [number, number, number];
  const date = new Date(Date.UTC(year, month - 1, day));
  return date.getUTCFullYear() === year && date.getUTCMonth() === month - 1 && date.getUTCDate() === day;
}

function controlOf(field: Field): Control {
  // Always named by its accessible label: alone in its form (the title says the rest), or next to its visible label.
  const label = { "aria-label": field.label };
  switch (field.kind) {
    case "select": {
      const select = element("select", { attributes: label },
        ...field.options.map(option => {
          const node = element("option", { text: option.label });
          node.value = option.value;
          return node;
        }));
      select.value = field.value;
      return select;
    }
    case "checkbox": {
      const box = element("input", { attributes: { type: "checkbox", ...label } });
      box.checked = field.value;
      return box;
    }
    case "date": {
      const date = element("input", { attributes: { type: "date", ...label } });
      date.value = field.value ?? "";
      return date;
    }
    case "integer": {
      const number = element("input", { attributes: { type: "text", inputmode: "numeric", autocomplete: "off", ...label } });
      number.value = field.value === null ? "" : String(field.value);
      return number;
    }
    case "amount": {
      const amount = element("input", { attributes: { type: "text", inputmode: "decimal", autocomplete: "off", ...label } });
      amount.value = field.value === null ? "" : formatAmount(field.value);
      return amount;
    }
    case "text": {
      const text = element("input", { attributes: { type: "text", autocomplete: "off", ...label } });
      text.value = field.value;
      return text;
    }
  }
}

/** The value of a field as typed, before any check: what decides which fields are shown. */
function peek({ field, control }: Entry): FieldValue {
  if (field.kind === "checkbox") return (control as HTMLInputElement).checked;
  if (field.kind === "amount") return parseEuros(control.value);
  if (field.kind === "integer") return /^\d+$/.test(control.value.trim()) ? Number(control.value.trim()) : null;
  const text = field.kind === "text" ? control.value.trim() : control.value;
  return text === "" && field.kind === "date" ? null : text;
}

/** The value of a field shown, or the message of its refusal. */
function readField({ field, control }: Entry): { value: FieldValue } | { problem: string } {
  switch (field.kind) {
    case "amount": {
      const amount = parseEuros(control.value);
      if (amount === null) return { problem: "Montant attendu, par exemple 1 250,50." };
      const refused = field.check?.(amount) ?? null;
      return refused === null ? { value: amount } : { problem: refused };
    }
    case "integer": {
      const text = control.value.trim();
      const number = /^\d+$/.test(text) ? Number(text) : null;
      return number !== null && number >= field.min && number <= field.max
        ? { value: number }
        : { problem: `Nombre entier attendu, de ${String(field.min)} à ${String(field.max)}.` };
    }
    case "date":
      // A date field of the browser gives an empty value for an incomplete or impossible date; one without date fields
      // falls back to a text field, hence the check of the text too.
      if (control.value === "" && field.optional) return { value: null };
      return isDate(control.value) ? { value: control.value } : { problem: "Date attendue." };
    case "select":
      // A value the list offers, never another one set by a script.
      return field.options.some(option => option.value === control.value)
        ? { value: control.value }
        : { problem: "Choix attendu dans la liste." };
    case "checkbox":
      return { value: (control as HTMLInputElement).checked };
    case "text": {
      const text = control.value.trim();
      const refused = field.check?.(text) ?? null;
      return refused === null ? { value: text } : { problem: refused };
    }
  }
}

/**
 * A form. A wrong value is refused here, without any call, the focus on its field. While a call is pending, every
 * control is disabled (a double click never sends twice) and Escape is ignored. A result shown redraws the view, this
 * form with it; otherwise (refused, failed, overtaken by a newer result) the form is enabled again with what was typed,
 * the status of the view saying why.
 */
export function recordForm({ title, fields, check, submit, cancel, actions = [], afterShown }: FormOptions): FormHandle {
  const alone = fields.length === 1;
  const entries: Entry[] = fields.map((field) => {
    const control = controlOf(field);
    const holder = alone
      ? control
      : element("label", { className: field.kind === "checkbox" ? "field check" : "field" },
        ...(field.kind === "checkbox" ? [control, element("span", { text: field.label })] : [element("span", { text: field.label }), control]));
    return { field, control, holder };
  });
  const save = element("button", { attributes: { type: "submit" } }, "Enregistrer");
  const others = actions.map((action) => {
    const button = element("button", { attributes: { type: "button" } }, action.label);
    button.addEventListener("click", () => { send(action.run); });
    return button;
  });
  const cancelButton = element("button", { attributes: { type: "button" } }, "Annuler");
  const problem = element("div", { className: "field-error", attributes: { role: "alert" } });
  const controls = [...entries.map(entry => entry.control), save, ...others, cancelButton];
  const form = element("form", { className: "amount-form" },
    element("div", { className: "title", text: title }),
    ...entries.map(entry => entry.holder),
    element("div", { className: "actions" }, save, ...others, cancelButton),
    problem);

  const shown = (entry: Entry) => !entry.holder.hidden;
  const firstShown = () => entries.find(shown)?.control;

  /** Shows the fields their condition allows, from what is typed now. */
  function showFields(): void {
    const values = Object.fromEntries(entries.map(entry => [entry.field.name, peek(entry)]));
    for (const entry of entries) entry.holder.hidden = !(entry.field.shownWhen?.(values) ?? true);
  }

  let busy = false;
  function setBusy(value: boolean): void {
    busy = value;
    for (const control of controls) control.disabled = value;
  }

  function send(call: () => Promise<CallResult>): void {
    problem.textContent = "";
    setBusy(true);
    void call().then((result) => {
      if (result === true) {
        afterShown?.();
        return;
      }
      setBusy(false);
      // A refusal said here too: the status of the view may be out of sight, above a long table.
      if (typeof result === "string") problem.textContent = result;
      firstShown()?.focus();
    });
  }

  /** The values typed, or the first refusal (its message, and the field to focus). */
  function read(): { values: FormValues } | { problem: string; control: Control } {
    const values: Record<string, FieldValue> = {};
    for (const entry of entries) {
      if (!shown(entry)) {
        values[entry.field.name] = entry.field.kind === "checkbox" ? false : null;
        continue;
      }
      const typed = readField(entry);
      if ("problem" in typed) return { problem: typed.problem, control: entry.control };
      values[entry.field.name] = typed.value;
    }
    const across = check?.(values) ?? null;
    if (across !== null) {
      const control = entries.find(entry => entry.field.name === across.field)?.control ?? firstShown() ?? save;
      return { problem: across.message, control: control as Control };
    }
    return { values };
  }

  showFields();
  form.addEventListener("input", showFields);
  form.addEventListener("change", showFields);
  form.addEventListener("submit", (event) => {
    // Never the default submission: it would navigate the frame of the view.
    event.preventDefault();
    const typed = read();
    if ("problem" in typed) {
      problem.textContent = typed.problem;
      typed.control.focus();
      return;
    }
    send(() => submit(typed.values));
  });
  cancelButton.addEventListener("click", cancel);
  form.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && !busy) cancel();
  });

  return {
    form,
    focus: () => {
      const first = firstShown();
      first?.focus();
      if (first instanceof HTMLInputElement && first.type === "text") first.select();
    },
  };
}

export interface AmountFormOptions {
  /** Visible title of the form. */
  title: string;
  /** Accessible name of the field: says which figure of which month is changed. */
  label: string;
  /** The figure shown, prefilled (formatAmount, read back by parseEuros). */
  value: number;
  /** Calls the input tool with the amount typed; resolves as ViewContext.call does. */
  submit: (amount: number) => Promise<CallResult>;
  cancel: () => void;
  actions?: readonly FormAction[];
  afterShown?: () => void;
}

/** A form of a single amount (the starting balance, an actual): the field alone, as before the generic forms. */
export function amountForm({ label, value, submit, ...options }: AmountFormOptions): FormHandle {
  return recordForm({
    ...options,
    fields: [{ kind: "amount", name: "amount", label, value }],
    // The only field is an amount: its value is a number.
    submit: ({ amount }) => submit(amount as number),
  });
}
