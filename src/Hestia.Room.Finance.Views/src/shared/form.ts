// The forms of the views: fields (texts and amounts), Enregistrer, optional other actions (such as "Comme prévu"),
// Annuler. Built with the DOM kit; every value the user types goes through it, so the forms all behave the same way.
import { element } from "./dom";
import { formatAmount, parseEuros } from "./format";
import "./form.css";

/** Another way to answer than the fields, such as "Comme prévu" or "N'a pas eu lieu": a button with its own input call. */
export interface FormAction {
  label: string;
  /** Calls the input tool; resolves to whether its result was shown (see ViewContext.call). */
  run: () => Promise<boolean>;
}

/** A text field, such as a name: trimmed; its check gives the message of a value refused, or null. */
export interface TextField {
  kind: "text";
  label: string;
  value: string;
  check?: (text: string) => string | null;
}

/** An amount field: prefilled with formatAmount, read back by parseEuros. */
export interface AmountField {
  kind: "amount";
  label: string;
  value: number;
}

export type Field = TextField | AmountField;

/** What a form sends: a text for a text field (trimmed), a number for an amount field, in the order of the fields. */
export type FieldValue = string | number;

export interface FormOptions {
  /** Visible title of the form. */
  title: string;
  fields: readonly Field[];
  /** Calls the input tool with the values typed; resolves to whether its result was shown. */
  submit: (values: FieldValue[]) => Promise<boolean>;
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

/**
 * A form. A wrong value is refused here, without any call, the focus on its field. While a call is pending, every
 * control is disabled (a double click never sends twice) and Escape is ignored. A result shown redraws the view, this
 * form with it; otherwise (refused, failed, overtaken by a newer result) the form is enabled again with what was typed,
 * the status of the view saying why.
 */
export function recordForm({ title, fields, submit, cancel, actions = [], afterShown }: FormOptions): FormHandle {
  // One field: the field alone, named by its accessible label (the title says the rest). Several: each with its
  // visible label.
  const alone = fields.length === 1;
  const entries = fields.map((field) => {
    const input = element("input", {
      attributes: field.kind === "amount"
        ? { type: "text", inputmode: "decimal", autocomplete: "off", "aria-label": field.label }
        : { type: "text", autocomplete: "off", "aria-label": field.label },
    });
    input.value = field.kind === "amount" ? formatAmount(field.value) : field.value;
    return { field, input };
  });
  const inputs = entries.map(entry => entry.input);
  const save = element("button", { attributes: { type: "submit" } }, "Enregistrer");
  const others = actions.map((action) => {
    const button = element("button", { attributes: { type: "button" } }, action.label);
    button.addEventListener("click", () => { send(action.run); });
    return button;
  });
  const cancelButton = element("button", { attributes: { type: "button" } }, "Annuler");
  const problem = element("div", { className: "field-error", attributes: { role: "alert" } });
  const controls = [...inputs, save, ...others, cancelButton];
  const form = element("form", { className: "amount-form" },
    element("div", { className: "title", text: title }),
    ...(alone
      ? inputs
      : entries.map(({ field, input }) => element("label", { className: "field" }, element("span", { text: field.label }), input))),
    element("div", { className: "actions" }, save, ...others, cancelButton),
    problem);

  let busy = false;
  function setBusy(value: boolean): void {
    busy = value;
    for (const control of controls) control.disabled = value;
  }

  function send(call: () => Promise<boolean>): void {
    problem.textContent = "";
    setBusy(true);
    void call().then((shown) => {
      if (shown) {
        afterShown?.();
        return;
      }
      setBusy(false);
      inputs[0]?.focus();
    });
  }

  /** The values typed, or the first refusal (its message, and the field to focus). */
  function read(): { values: FieldValue[] } | { problem: string; input: HTMLInputElement } {
    const values: FieldValue[] = [];
    for (const { field, input } of entries) {
      if (field.kind === "amount") {
        const amount = parseEuros(input.value);
        if (amount === null) return { problem: "Montant attendu, par exemple 1 250,50.", input };
        values.push(amount);
      } else {
        const text = input.value.trim();
        const refused = field.check?.(text) ?? null;
        if (refused !== null) return { problem: refused, input };
        values.push(text);
      }
    }
    return { values };
  }

  form.addEventListener("submit", (event) => {
    // Never the default submission: it would navigate the frame of the view.
    event.preventDefault();
    const typed = read();
    if ("problem" in typed) {
      problem.textContent = typed.problem;
      typed.input.focus();
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
      inputs[0]?.focus();
      inputs[0]?.select();
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
  /** Calls the input tool with the amount typed; resolves to whether its result was shown. */
  submit: (amount: number) => Promise<boolean>;
  cancel: () => void;
  actions?: readonly FormAction[];
  afterShown?: () => void;
}

/** A form of a single amount (the starting balance, an actual): the field alone, as before the generic forms. */
export function amountForm({ label, value, submit, ...options }: AmountFormOptions): FormHandle {
  return recordForm({
    ...options,
    fields: [{ kind: "amount", label, value }],
    // The only field is an amount: its value is a number.
    submit: ([amount]) => submit(amount as number),
  });
}
