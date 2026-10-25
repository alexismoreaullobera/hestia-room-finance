import { beforeEach, describe, expect, it, vi } from "vitest";
import { element } from "../src/shared/dom";
import { recordForm } from "../src/shared/form";
import { editableTable, type EditableRow, type NewRow } from "../src/shared/table";

// The editable table of the kit with two items, the second one deletable, and a row to add an item.
let page: HTMLElement;
beforeEach(() => {
  page = document.createElement("main");
  document.body.replaceChildren(page);
});

const pending = () => new Promise<boolean>(() => {});
const items = ["Courses", "Sorties"];

function render(options: { run?: () => Promise<boolean>; newRow?: NewRow | null; list?: string[] } = {}) {
  const run = options.run ?? vi.fn<() => Promise<boolean>>(pending);
  const rowOf = (name: string): EditableRow => ({
    cells: () => [element("td", { text: name })],
    form: closing => recordForm({ ...closing, title: name, fields: [{ kind: "text", label: "Nom", value: name }], submit: pending }),
    ...(name === "Sorties" ? { remove: { confirmation: `Supprimer « ${name} » ?`, run } } : {}),
  });
  const newRow: NewRow | undefined = options.newRow === null ? undefined : options.newRow ?? {
    label: "Nouveau budget",
    form: closing => recordForm({ ...closing, title: "Nouveau budget", fields: [{ kind: "text", label: "Nom", value: "" }], submit: pending }),
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
    ["Escape", () => { row(1).dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true })); }],
  ])("gives the row back on %s, the focus on Supprimer, without deleting", (_, back) => {
    const run = render();
    buttonIn(row(1), "Supprimer").click();

    back();

    expect(row(1).classList.contains("confirming")).toBe(false);
    expect(document.activeElement).toBe(buttonIn(row(1), "Supprimer"));
    expect(run).not.toHaveBeenCalled();
  });

  it("deletes on Confirmer, the buttons disabled meanwhile and Escape ignored", () => {
    const run = render();
    buttonIn(row(1), "Supprimer").click();

    buttonIn(row(1), "Confirmer la suppression").click();
    row(1).dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true }));

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
  function open(submit = vi.fn<(values: (string | number)[]) => Promise<boolean>>(pending)) {
    const { form, focus } = recordForm({
      title: "Nouveau budget",
      fields: [
        { kind: "text", label: "Nom", value: "", check: text => text === "" ? "Nom attendu." : null },
        { kind: "amount", label: "Par mois", value: 0 },
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

  it("sends the values in the order of the fields, the text trimmed and the amount read", () => {
    const submit = open();
    inputs()[0]!.value = "  Cadeaux ";
    inputs()[1]!.value = "40,5";

    submitForm();

    expect(submit).toHaveBeenCalledExactlyOnceWith(["Cadeaux", 40.5]);
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

  it("disables every field while sending", () => {
    open();
    inputs()[0]!.value = "Cadeaux";
    inputs()[1]!.value = "40";

    submitForm();

    expect(inputs().every(input => input.disabled)).toBe(true);
  });
});
