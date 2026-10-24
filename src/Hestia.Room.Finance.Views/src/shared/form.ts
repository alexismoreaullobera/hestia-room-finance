// The amount form of the views: a field, Enregistrer, optional other actions (such as "Comme prévu"), Annuler. Built
// with the DOM kit; every input of the room goes through it, so they all behave the same way.
import { element } from "./dom";
import { formatAmount, parseEuros } from "./format";
import "./form.css";

/** Another way to answer than an amount, such as "Comme prévu" or "N'a pas eu lieu": a button with its own input call. */
export interface FormAction {
  label: string;
  /** Calls the input tool; resolves to whether its result was shown (see ViewContext.call). */
  run: () => Promise<boolean>;
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
  /** Closes the form without saving (Annuler, Escape). */
  cancel: () => void;
  actions?: readonly FormAction[];
}

export interface AmountForm {
  form: HTMLFormElement;
  /** Focuses the field with its text selected, once the form is in the page: typing replaces the value at once. */
  focus: () => void;
}

/**
 * An amount form. A wrong amount is refused here, without any call. While a call is pending, every control is disabled
 * (a double click never sends twice). A result shown redraws the view, this form with it; otherwise (refused, failed,
 * overtaken by a newer result) the form is enabled again with what was typed, the status of the view saying why.
 */
export function amountForm({ title, label, value, submit, cancel, actions = [] }: AmountFormOptions): AmountForm {
  const field = element("input", { attributes: { type: "text", inputmode: "decimal", autocomplete: "off", "aria-label": label } });
  field.value = formatAmount(value);
  const save = element("button", { attributes: { type: "submit" } }, "Enregistrer");
  const others = actions.map((action) => {
    const button = element("button", { attributes: { type: "button" } }, action.label);
    button.addEventListener("click", () => { send(action.run); });
    return button;
  });
  const cancelButton = element("button", { attributes: { type: "button" } }, "Annuler");
  const problem = element("div", { className: "field-error", attributes: { role: "alert" } });
  const controls = [field, save, ...others, cancelButton];
  const form = element("form", { className: "amount-form" },
    element("div", { className: "title", text: title }),
    field,
    element("div", { className: "actions" }, save, ...others, cancelButton),
    problem);

  function setBusy(busy: boolean): void {
    for (const control of controls) control.disabled = busy;
  }

  function send(call: () => Promise<boolean>): void {
    problem.textContent = "";
    setBusy(true);
    void call().then((shown) => {
      if (shown) return;
      setBusy(false);
      field.focus();
    });
  }

  form.addEventListener("submit", (event) => {
    // Never the default submission: it would navigate the frame of the view.
    event.preventDefault();
    const amount = parseEuros(field.value);
    if (amount === null) {
      problem.textContent = "Montant attendu, par exemple 1 250,50.";
      field.focus();
      return;
    }
    send(() => submit(amount));
  });
  cancelButton.addEventListener("click", cancel);
  form.addEventListener("keydown", (event) => {
    if (event.key === "Escape") cancel();
  });

  return {
    form,
    focus: () => {
      field.focus();
      field.select();
    },
  };
}
