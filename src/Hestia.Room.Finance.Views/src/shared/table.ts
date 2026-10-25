// The editable tables of the views: one row per item, edited in place with a form of the kit, an optional deletion in
// two steps and an optional row to add an item. Each input redraws the whole view from the result of the server.
import { element } from "./dom";
import type { FormHandle } from "./form";
import "./table.css";

/** How a form opened from a table closes: cancelled (the row given back), or saved (the view redrawn). */
export interface Closing {
  cancel: () => void;
  afterShown: () => void;
}

/** Deleting an item: a confirmation first (never window.confirm, which the sandbox of the view may block). */
export interface RowRemoval {
  /** What the confirmation says, such as what goes with the item. */
  confirmation: string;
  /** Calls the input tool that deletes; resolves to whether its result was shown. */
  run: () => Promise<boolean>;
}

/** What an editable row shows and how it is entered. */
export interface EditableRow {
  /** The cells before the buttons: the row as read. */
  cells: () => HTMLElement[];
  /** The form that replaces the row, opened by Modifier. */
  form: (closing: Closing) => FormHandle;
  remove?: RowRemoval;
}

/** The last row of a table, to add an item: a button that turns into the form of a new item. */
export interface NewRow {
  /** Such as "Nouveau budget". */
  label: string;
  form: (closing: Closing) => FormHandle;
}

export interface Header {
  text: string;
  amount?: boolean;
}

function button(label: string, className = ""): HTMLButtonElement {
  return element("button", { className, attributes: { type: "button" } }, label);
}

/** After a redraw, the focus on the element of the new rendering (the same row, or the button to add). */
function focusOn(selector: string): void {
  document.querySelector<HTMLElement>(selector)?.focus();
}

/** A row on the whole width of its table: a form, or a confirmation. */
function wholeWidth(node: HTMLElement, columns: number, ...children: HTMLElement[]): void {
  const cell = element("td", {}, ...children);
  cell.colSpan = columns;
  node.replaceChildren(cell);
}

/**
 * A row of an editable table: its cells, Modifier (the row turns into the form, on the whole width of the table) and,
 * when the item can be deleted, Supprimer (the row turns into a confirmation). Annuler or Escape gives the row back.
 */
export function editableRow(table: string, index: number, columns: number, { cells, form, remove }: EditableRow): HTMLElement {
  const node = element("tr");
  // After a save the view is redrawn, this row with it: the focus goes to the Modifier button of the same row.
  const afterShown = () => { focusOn(`.${table} tbody tr:nth-child(${index + 1}) .edit`); };

  function showValue(focus: "edit" | "remove" | null = null): void {
    const edit = button("Modifier", "edit");
    edit.addEventListener("click", showForm);
    const buttons: HTMLElement[] = [edit];
    if (remove) {
      const removal = button("Supprimer", "remove");
      removal.addEventListener("click", () => { showConfirmation(remove); });
      buttons.push(removal);
    }
    node.classList.remove("editing", "confirming");
    node.replaceChildren(...cells(), element("td", { className: "action" }, ...buttons));
    if (focus) node.querySelector<HTMLButtonElement>(`.${focus}`)?.focus();
  }

  function showForm(): void {
    const { form: opened, focus } = form({ cancel: () => { showValue("edit"); }, afterShown });
    node.classList.add("editing");
    wholeWidth(node, columns, opened);
    focus();
  }

  function showConfirmation({ confirmation, run }: RowRemoval): void {
    const confirm = button("Confirmer la suppression", "confirm");
    const back = button("Annuler", "cancel");
    let busy = false;
    const setBusy = (value: boolean) => {
      busy = value;
      confirm.disabled = value;
      back.disabled = value;
    };
    confirm.addEventListener("click", () => {
      setBusy(true);
      void run().then((shown) => {
        // Deleted: the row is gone with the redraw, the focus goes to the button to add, if the table has one.
        if (shown) {
          focusOn(`.${table} .new`);
          return;
        }
        setBusy(false);
        confirm.focus();
      });
    });
    back.addEventListener("click", () => { showValue("remove"); });
    node.addEventListener("keydown", function escape(event) {
      if (event.key !== "Escape" || busy) return;
      node.removeEventListener("keydown", escape);
      showValue("remove");
    });
    node.classList.add("confirming");
    wholeWidth(node, columns,
      element("p", { className: "confirmation", attributes: { role: "alert" }, text: confirmation }),
      element("div", { className: "actions" }, confirm, back));
    // The safe answer first: Enter on the focused button never deletes by mistake.
    back.focus();
  }

  showValue();
  return node;
}

/** The row to add an item, at the end of the table. */
function newItemRow(table: string, columns: number, { label, form }: NewRow): HTMLElement {
  const node = element("tr", { className: "new-row" });

  function showButton(focus = false): void {
    const add = button(label, "new");
    add.addEventListener("click", showForm);
    node.classList.remove("editing");
    wholeWidth(node, columns, add);
    if (focus) add.focus();
  }

  function showForm(): void {
    // Saved: the view is redrawn, the focus back on the button to add (the new item is somewhere in the list).
    const { form: opened, focus } = form({ cancel: () => { showButton(true); }, afterShown: () => { focusOn(`.${table} .new`); } });
    node.classList.add("editing");
    wholeWidth(node, columns, opened);
    focus();
  }

  showButton();
  return node;
}

/**
 * A titled editable table: one row per item, plus the column of the buttons, and the row to add an item when given.
 * Nothing at all without items and without a way to add one.
 */
export function editableTable<T>(
  className: string, title: string, headers: readonly Header[], items: readonly T[], rowOf: (item: T) => EditableRow,
  newRow?: NewRow): HTMLElement[] {
  if (items.length === 0 && !newRow) return [];
  const columns = headers.length + 1;
  return [
    element("h2", { text: title }),
    element("table", { className },
      element("thead", {}, element("tr", {},
        ...headers.map(header => element("th", { className: header.amount ? "amount" : "", text: header.text })),
        element("th"))),
      element("tbody", {}, ...items.map((item, index) => editableRow(className, index, columns, rowOf(item)))),
      ...(newRow ? [element("tfoot", {}, newItemRow(className, columns, newRow))] : [])),
  ];
}
