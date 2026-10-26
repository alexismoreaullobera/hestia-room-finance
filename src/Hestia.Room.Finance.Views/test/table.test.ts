import { beforeEach, describe, expect, it, vi } from "vitest";
import { element } from "../src/shared/dom";
import { recordForm, type FormValues } from "../src/shared/form";
import { editableTable, type EditableRow, type NewRow, type RowAction } from "../src/shared/table";

// The editable table of the kit with two items, the second one deletable, and a row to add an item.
let page: HTMLElement;
beforeEach(() => {
  page = document.createElement("main");
  document.body.replaceChildren(page);
});

const pending = () => new Promise<boolean>(() => {});
const items = ["Courses", "Sorties"];

function render(options: { run?: () => Promise<boolean>; newRow?: NewRow | null; list?: string[]; actions?: RowAction[] } = {}) {
  const run = options.run ?? vi.fn<() => Promise<boolean>>(pending);
  const rowOf = (name: string): EditableRow => ({
    key: name.toLowerCase(),
    cells: () => [element("td", { text: name })],
    form: closing => recordForm({ ...closing, title: name, fields: [{ kind: "text", name: "name", label: "Nom", value: name }], submit: pending }),
    ...(name === "Sorties" ? { remove: { confirmation: `Supprimer « ${name} » ?`, run }, actions: options.actions ?? [] } : {}),
  });
  const newRow: NewRow | undefined = options.newRow === null ? undefined : options.newRow ?? {
    label: "Nouveau budget",
    form: closing => recordForm({ ...closing, title: "Nouveau budget", fields: [{ kind: "text", name: "name", label: "Nom", value: "" }], submit: pending }),
  };
  page.replaceChildren(...editableTable("budgets", "Budgets", [{ text: "Budget" }], options.list ?? items, rowOf, newRow));
  return run;
}

const row = (index: number) => page.querySelectorAll(".budgets tbody tr")[index]!;
const buttonIn = (node: Element, name: string) => [...node.querySelectorAll("button")].find(b => b.textContent === name)!;
const names = (node: Element) => [...node.querySelectorAll("button")].map(b => b.textContent);
const newRowNode = () => page.querySelector(".budgets tfoot tr")!;

describe("editableTable", () => {
  it("shows a title, one row per item with its buttons, and the row to add an item", () => {
    render();

    expect(page.querySelector("h2")?.textContent).toBe("Budgets");
    expect(names(row(0))).toEqual(["Modifier"]);
    // Supprimer only for an item that can be deleted.
    expect(names(row(1))).toEqual(["Modifier", "Supprimer"]);
    expect(names(newRowNode())).toEqual(["Nouveau budget"]);
    expect(newRowNode().querySelector("td")?.colSpan).toBe(2);
  });

  it("is nothing at all without items and without a row to add one", () => {
    render({ list: [], newRow: null });

    expect(page.childNodes).toHaveLength(0);
  });

  it("keeps the row to add an item when the table has no item yet", () => {
    render({ list: [] });

    expect(page.querySelectorAll(".budgets tbody tr")).toHaveLength(0);
    expect(names(newRowNode())).toEqual(["Nouveau budget"]);
  });

  it("turns the row to add into a form, given back with the focus on its button on Annuler", () => {
    render();
    buttonIn(newRowNode(), "Nouveau budget").click();

    expect(newRowNode().classList.contains("editing")).toBe(true);
    expect(document.activeElement).toBe(newRowNode().querySelector("input"));

    buttonIn(newRowNode(), "Annuler").click();

    expect(newRowNode().querySelector("input")).toBeNull();
    expect(document.activeElement).toBe(buttonIn(newRowNode(), "Nouveau budget"));
  });

  it("asks a confirmation before deleting, the safe answer focused", () => {
    const run = render();

    buttonIn(row(1), "Supprimer").click();

    expect(row(1).classList.contains("confirming")).toBe(true);
    expect(row(1).querySelector(".confirmation")?.textContent).toBe("Supprimer « Sorties » ?");
    expect(row(1).querySelector(".confirmation")?.getAttribute("role")).toBe("alert");
    expect(row(1).querySelector("td")?.colSpan).toBe(2);
    expect(names(row(1))).toEqual(["Confirmer la suppression", "Annuler"]);
    // Styled like every button of the kit, never left to the default of the browser.
    expect([...row(1).querySelectorAll("button")].map(b => b.className)).toEqual(["confirm", "cancel"]);
    expect(document.activeElement).toBe(buttonIn(row(1), "Annuler"));
    expect(run).not.toHaveBeenCalled();
  });

  it.each([
    ["Annuler", () => { buttonIn(row(1), "Annuler").click(); }],
    // Pressed where the focus is: on Annuler, inside the confirmation.
    ["Escape", () => { document.activeElement?.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true })); }],
  ])("gives the row back on %s, the focus on Supprimer, without deleting", (_, back) => {
    const run = render();
    buttonIn(row(1), "Supprimer").click();

    back();

    expect(row(1).classList.contains("confirming")).toBe(false);
    expect(document.activeElement).toBe(buttonIn(row(1), "Supprimer"));
    expect(run).not.toHaveBeenCalled();
  });

  it("leaves nothing of a cancelled confirmation: Escape in the form then gives the focus back to Modifier", () => {
    render();
    buttonIn(row(1), "Supprimer").click();
    buttonIn(row(1), "Annuler").click();
    buttonIn(row(1), "Modifier").click();

    row(1).querySelector("input")!.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true }));

    expect(row(1).querySelector("form")).toBeNull();
    expect(document.activeElement).toBe(buttonIn(row(1), "Modifier"));
  });

  it("shows the other actions between Modifier and Supprimer, each opening its own form", () => {
    const ending = vi.fn<RowAction["form"]>(closing => recordForm({
      ...closing, title: "Terminer Sorties", fields: [{ kind: "date", name: "endDate", label: "Dernier jour", value: "2027-03-31" }], submit: pending,
    }));
    render({ actions: [{ label: "Terminer", className: "end", form: ending }] });

    expect(names(row(1))).toEqual(["Modifier", "Terminer", "Supprimer"]);
    // Styled like every button of the kit, never left to the default of the browser.
    expect([...row(1).querySelectorAll("button")].map(b => b.className)).toEqual(["edit", "end row-action", "remove"]);
    buttonIn(row(1), "Terminer").click();

    expect(ending).toHaveBeenCalledOnce();
    expect(row(1).classList.contains("editing")).toBe(true);
    expect(row(1).querySelector(".title")?.textContent).toBe("Terminer Sorties");
    expect(document.activeElement).toBe(row(1).querySelector("input"));
  });

  it("gives the row back on Annuler of another action, the focus on its button", () => {
    render({ actions: [{ label: "Terminer", className: "end", form: closing => recordForm({
      ...closing, title: "Terminer", fields: [{ kind: "date", name: "endDate", label: "Dernier jour", value: null }], submit: pending,
    }) }] });
    buttonIn(row(1), "Terminer").click();

    buttonIn(row(1), "Annuler").click();

    expect(row(1).querySelector("form")).toBeNull();
    expect(document.activeElement).toBe(buttonIn(row(1), "Terminer"));
  });

  it("gives the row back on Escape in the date of another action, the focus on its button", () => {
    render({ actions: [{ label: "Terminer", className: "end", form: closing => recordForm({
      ...closing, title: "Terminer", fields: [{ kind: "date", name: "endDate", label: "Dernier jour", value: "2027-03-31" }], submit: pending,
    }) }] });
    buttonIn(row(1), "Terminer").click();

    row(1).querySelector("input")!.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true }));

    expect(row(1).querySelector("form")).toBeNull();
    expect(document.activeElement).toBe(buttonIn(row(1), "Terminer"));
  });

  it("puts the focus on the button of the action in the new rendering, once its result is shown", async () => {
    const actions: RowAction[] = [{ label: "Terminer", className: "end", form: closing => recordForm({
      ...closing, title: "Terminer", fields: [{ kind: "date", name: "endDate", label: "Dernier jour", value: "2027-03-31" }],
      // The view redraws from the result: a new table, the same rows by their id.
      submit: () => { render({ actions }); return Promise.resolve(true); },
    }) }];
    render({ actions });
    buttonIn(row(1), "Terminer").click();

    buttonIn(row(1), "Enregistrer").click();

    await vi.waitFor(() => { expect(document.activeElement).toBe(buttonIn(row(1), "Terminer")); });
  });

  it("deletes on Confirmer, the buttons disabled meanwhile and Escape ignored", () => {
    const run = render();
    buttonIn(row(1), "Supprimer").click();

    buttonIn(row(1), "Confirmer la suppression").click();
    buttonIn(row(1), "Annuler").dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true }));

    expect(run).toHaveBeenCalledOnce();
    expect([...row(1).querySelectorAll("button")].map(b => b.disabled)).toEqual([true, true]);
    expect(row(1).classList.contains("confirming")).toBe(true);
  });

  it("enables the confirmation again when the deletion is not shown", async () => {
    render({ run: () => Promise.resolve(false) });
    buttonIn(row(1), "Supprimer").click();

    buttonIn(row(1), "Confirmer la suppression").click();

    await vi.waitFor(() => { expect(buttonIn(row(1), "Confirmer la suppression").disabled).toBe(false); });
    expect(document.activeElement).toBe(buttonIn(row(1), "Confirmer la suppression"));
  });

  it("puts the focus on the button to add once the deletion is shown", async () => {
    render({ run: () => Promise.resolve(true) });
    buttonIn(row(1), "Supprimer").click();

    buttonIn(row(1), "Confirmer la suppression").click();

    // The view would redraw; here the same table stays, its button to add takes the focus.
    await vi.waitFor(() => { expect(document.activeElement).toBe(buttonIn(newRowNode(), "Nouveau budget")); });
  });
});

describe("recordForm with several fields", () => {
  function open(submit = vi.fn<(values: FormValues) => Promise<boolean>>(pending)) {
    const { form, focus } = recordForm({
      title: "Nouveau budget",
      fields: [
        { kind: "text", name: "name", label: "Nom", value: "", check: text => text === "" ? "Nom attendu." : null },
        { kind: "amount", name: "plannedAmount", label: "Par mois", value: 0 },
      ],
      submit,
      cancel: vi.fn<() => void>(),
    });
    page.replaceChildren(form);
    focus();
    return submit;
  }
  const inputs = () => [...page.querySelectorAll("input")];
  const submitForm = () => page.querySelector("form")!.dispatchEvent(new Event("submit", { cancelable: true }));

  it("shows each field with its visible label, the first one focused", () => {
    open();

    expect([...page.querySelectorAll(".field span")].map(s => s.textContent)).toEqual(["Nom", "Par mois"]);
    expect(inputs().map(i => i.getAttribute("aria-label"))).toEqual(["Nom", "Par mois"]);
    expect(inputs()[1]?.getAttribute("inputmode")).toBe("decimal");
    expect(inputs()[0]?.getAttribute("inputmode")).toBeNull();
    expect(document.activeElement).toBe(inputs()[0]);
  });

  it("sends each value under the name of its field, the text trimmed and the amount read", () => {
    const submit = open();
    inputs()[0]!.value = "  Cadeaux ";
    inputs()[1]!.value = "40,5";

    submitForm();

    expect(submit).toHaveBeenCalledExactlyOnceWith({ name: "Cadeaux", plannedAmount: 40.5 });
  });

  it("refuses a text by its check, the focus on that field, without any call", () => {
    const submit = open();
    inputs()[1]!.value = "40";
    inputs()[1]!.focus();

    submitForm();

    expect(submit).not.toHaveBeenCalled();
    expect(page.querySelector(".field-error")?.textContent).toBe("Nom attendu.");
    expect(document.activeElement).toBe(inputs()[0]);
  });

  it("refuses an amount of a later field, the focus on that field", () => {
    const submit = open();
    inputs()[0]!.value = "Cadeaux";
    inputs()[1]!.value = "abc";

    submitForm();

    expect(submit).not.toHaveBeenCalled();
    expect(page.querySelector(".field-error")?.textContent).toBe("Montant attendu, par exemple 1 250,50.");
    expect(document.activeElement).toBe(inputs()[1]);
  });

  it("refuses a value by the check across the fields, the focus on the field it names", () => {
    const submit = vi.fn<(values: FormValues) => Promise<boolean>>(pending);
    const { form } = recordForm({
      title: "Ligne",
      fields: [
        { kind: "date", name: "startDate", label: "Début", value: "2027-03-01" },
        { kind: "date", name: "endDate", label: "Fin", value: "2027-02-28", optional: true },
      ],
      check: ({ startDate, endDate }) => endDate !== null && String(endDate) < String(startDate)
        ? { field: "endDate", message: "La fin ne peut pas précéder le début." } : null,
      submit,
      cancel: vi.fn<() => void>(),
    });
    page.replaceChildren(form);

    submitForm();

    expect(submit).not.toHaveBeenCalled();
    expect(page.querySelector(".field-error")?.textContent).toBe("La fin ne peut pas précéder le début.");
    expect(document.activeElement).toBe(inputs()[1]);
  });

  it("disables every field while sending", () => {
    open();
    inputs()[0]!.value = "Cadeaux";
    inputs()[1]!.value = "40";

    submitForm();

    expect(inputs().every(input => input.disabled)).toBe(true);
  });
});

describe("recordForm, the kinds of field", () => {
  const cancel = vi.fn<() => void>();
  const submitForm = () => page.querySelector("form")!.dispatchEvent(new Event("submit", { cancelable: true }));
  const control = (label: string) => page.querySelector<HTMLInputElement & HTMLSelectElement>(`[aria-label="${label}"]`)!;
  const holder = (label: string) => control(label).closest("label")!;
  const error = () => page.querySelector(".field-error")?.textContent;

  function open(submit = vi.fn<(values: FormValues) => Promise<boolean>>(pending)) {
    const { form, focus } = recordForm({
      title: "Nouvelle ligne",
      fields: [
        { kind: "select", name: "kind", label: "Type", value: "Expense", options: [{ value: "Income", label: "Entrée" }, { value: "Expense", label: "Dépense" }] },
        { kind: "select", name: "frequency", label: "Fréquence", value: "Monthly",
          options: [{ value: "Monthly", label: "Mensuelle" }, { value: "Yearly", label: "Annuelle" }] },
        { kind: "integer", name: "dayOfMonth", label: "Jour", value: 5, min: 1, max: 31 },
        { kind: "integer", name: "anchorMonth", label: "Mois", value: null, min: 1, max: 12, shownWhen: values => values.frequency !== "Monthly" },
        { kind: "date", name: "startDate", label: "Début", value: "2027-03-01" },
        { kind: "date", name: "endDate", label: "Fin", value: null, optional: true },
        { kind: "checkbox", name: "isSubscription", label: "Abonnement", value: false, shownWhen: values => values.kind === "Expense" },
      ],
      submit,
      cancel,
    });
    page.replaceChildren(form);
    focus();
    return submit;
  }

  it("prefills each field, a list with its labels, a date as typed, a check box unchecked", () => {
    open();

    expect([...control("Type").querySelectorAll("option")].map(o => [o.value, o.textContent])).toEqual([["Income", "Entrée"], ["Expense", "Dépense"]]);
    expect(control("Type").value).toBe("Expense");
    expect(control("Jour").value).toBe("5");
    expect(control("Jour").getAttribute("inputmode")).toBe("numeric");
    expect(control("Début").type).toBe("date");
    expect(control("Début").value).toBe("2027-03-01");
    expect(control("Abonnement").type).toBe("checkbox");
    expect(control("Abonnement").checked).toBe(false);
    // The first field takes the focus, even a list.
    expect(document.activeElement).toBe(control("Type"));
  });

  it("sends every value under its name: a whole number, a date, an empty optional date as null, a check box", () => {
    const submit = open();
    control("Abonnement").checked = true;

    submitForm();

    expect(submit).toHaveBeenCalledExactlyOnceWith({
      kind: "Expense", frequency: "Monthly", dayOfMonth: 5, anchorMonth: null, startDate: "2027-03-01", endDate: null, isSubscription: true,
    });
  });

  it("hides a field its condition refuses, and shows it again as soon as the condition holds", () => {
    open();
    expect(holder("Mois").hidden).toBe(true);

    control("Fréquence").value = "Yearly";
    control("Fréquence").dispatchEvent(new Event("change", { bubbles: true }));

    expect(holder("Mois").hidden).toBe(false);
  });

  it("never checks nor sends a field not shown: null, or false for a check box", () => {
    const submit = open();
    control("Abonnement").checked = true;
    control("Type").value = "Income";
    control("Type").dispatchEvent(new Event("change", { bubbles: true }));
    // Hidden with a wrong value typed before: not checked.
    control("Mois").value = "99";

    submitForm();

    expect(holder("Abonnement").hidden).toBe(true);
    expect(submit).toHaveBeenCalledExactlyOnceWith(expect.objectContaining({ kind: "Income", anchorMonth: null, isSubscription: false }));
  });

  it("checks a field shown by its condition", () => {
    const submit = open();
    control("Fréquence").value = "Yearly";
    control("Fréquence").dispatchEvent(new Event("change", { bubbles: true }));

    submitForm();

    expect(submit).not.toHaveBeenCalled();
    expect(error()).toBe("Nombre entier attendu, de 1 à 12.");
    expect(document.activeElement).toBe(control("Mois"));
  });

  it.each(["", "0", "32", "5,5", "-1", "abc"])("refuses the day %j, the focus on its field", (day) => {
    const submit = open();
    control("Jour").value = day;

    submitForm();

    expect(submit).not.toHaveBeenCalled();
    expect(error()).toBe("Nombre entier attendu, de 1 à 31.");
    expect(document.activeElement).toBe(control("Jour"));
  });

  it.each(["", "2027-02-30", "31/03/2027"])("refuses the start date %j", (date) => {
    const submit = open();
    control("Début").value = date;

    submitForm();

    expect(submit).not.toHaveBeenCalled();
    expect(error()).toBe("Date attendue.");
  });

  it("refuses an impossible date where the browser has no date field (a text field instead)", () => {
    const submit = open();
    page.querySelector<HTMLInputElement>('[aria-label="Début"]')!.type = "text";
    control("Début").value = "2027-02-30";

    submitForm();

    expect(submit).not.toHaveBeenCalled();
    expect(error()).toBe("Date attendue.");
  });

  it("refuses a value the list does not offer", () => {
    const submit = open();
    const forged = element("option", { text: "Autre" });
    forged.value = "Other";
    control("Type").append(forged);
    control("Type").value = "Other";

    submitForm();

    expect(submit).not.toHaveBeenCalled();
    expect(error()).toBe("Choix attendu dans la liste.");
  });

  it("disables the lists and the check boxes too while sending", () => {
    open();

    submitForm();

    expect([control("Type"), control("Abonnement"), control("Début")].every(c => c.disabled)).toBe(true);
  });
});
