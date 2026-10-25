// The editable tables of the views: one row per item, edited in place with a form of the kit, optional other actions
// with their own form (such as Terminer), an optional deletion in two steps and an optional row to add an item. Each
// input redraws the whole view from the result of the server.
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

/** Another change of a row than Modifier, with its own form: such as Terminer, which asks for an end date. */
export interface RowAction {
  label: string;
  /** The class of its button: the focus finds it again after a redraw, or when its form is cancelled. */
  className: string;
  form: (closing: Closing) => FormHandle;
}

/** What an editable row shows and how it is entered. */
export interface EditableRow {
  /** What identifies the item in its table (its id): the focus finds its row again after a redraw, wherever it moved. */
  key: string;
  /** The cells before the buttons: the row as read. */
  cells: () => HTMLElement[];
  /** The form that replaces the row, opened by Modifier. */
  form: (closing: Closing) => FormHandle;
  /** Shown between Modifier and Supprimer. */
  actions?: readonly RowAction[];
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

/** The id of the row of an item: unique in the view, as the class of a table is. */
const rowId = (table: string, key: string) => `${table}-${key}`;

/** A row on the whole width of its table: a form, or a confirmation. Returns its only cell. */
function wholeWidth(node: HTMLElement, columns: number, ...children: HTMLElement[]): HTMLElement {
  const cell = element("td", {}, ...children);
  cell.colSpan = columns;
  node.replaceChildren(cell);
  return cell;
}

/**
 * A row of an editable table: its cells, Modifier (the row turns into the form, on the whole width of the table), its
 * other actions (each with its form) and, when the item can be deleted, Supprimer (the row turns into a confirmation).
 * Annuler or Escape gives the row back, the focus on the button that opened the form.
 */
export function editableRow(table: string, columns: number, { key, cells, form, actions = [], remove }: EditableRow): HTMLElement {
  const id = rowId(table, key);
  const node = element("tr", { attributes: { id } });
  // After a save the view is redrawn, this row with it: the focus goes to the same button of the same item, found by
  // its id (a rename may move it in the sorted list).
  const focusAfterShown = (className: string) => () => {
    document.getElementById(id)?.querySelector<HTMLElement>(`.${className}`)?.focus();
  };

  function showValue(focus: string | null = null): void {
    const edit = button("Modifier", "edit");
    edit.addEventListener("click", () => { showForm(form, "edit"); });
    const buttons: HTMLElement[] = [edit];
    for (const action of actions) {
      const opener = button(action.label, action.className);
      opener.addEventListener("click", () => { showForm(action.form, action.className); });
      buttons.push(opener);
    }
    if (remove) {
      const removal = button("Supprimer", "remove");
      removal.addEventListener("click", () => { showConfirmation(remove); });
      buttons.push(removal);
    }
    node.classList.remove("editing", "confirming");
    node.replaceChildren(...cells(), element("td", { className: "action" }, ...buttons));
    if (focus) node.querySelector<HTMLButtonElement>(`.${focus}`)?.focus();
  }

  /** The row turned into a form; its button (by class) takes the focus back on Annuler, and after a save. */
  function showForm(open: (closing: Closing) => FormHandle, opener: string): void {
    const { form: opened, focus } = open({ cancel: () => { showValue(opener); }, afterShown: focusAfterShown(opener) });
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
    node.classList.add("confirming");
    const cell = wholeWidth(node, columns,
      element("p", { className: "confirmation", attributes: { role: "alert" }, text: confirmation }),
      element("div", { className: "actions" }, confirm, back));
    // On the cell of the confirmation, gone with it: never a listener left on the row once the confirmation is closed.
    cell.addEventListener("keydown", (event) => {
      if (event.key === "Escape" && !busy) showValue("remove");
    });
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
      element("tbody", {}, ...items.map(item => editableRow(className, columns, rowOf(item)))),
      ...(newRow ? [element("tfoot", {}, newItemRow(className, columns, newRow))] : [])),
  ];
}
