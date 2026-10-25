import { beforeEach, describe, expect, it, vi } from "vitest";
import { amountForm, type AmountFormOptions } from "../src/shared/form";
import { plain } from "./helpers";

// The paths shared with the starting balance card (submit, wrong amount, refusal, Escape, focus) are tested through
// that card in views.test.ts; here, what the kit adds for the next forms: the other actions.

let page: HTMLElement;
beforeEach(() => {
  page = document.createElement("main");
  document.body.replaceChildren(page);
});

const pending = () => new Promise<boolean>(() => {});

function open(options: Partial<AmountFormOptions> = {}) {
  const { form, focus } = amountForm({
    title: "Courses",
    label: "Réel de Courses en mars 2027",
    value: 320,
    submit: vi.fn<AmountFormOptions["submit"]>(() => Promise.resolve(true)),
    cancel: vi.fn<AmountFormOptions["cancel"]>(),
    ...options,
  });
  page.replaceChildren(form);
  focus();
  return form;
}

const field = () => page.querySelector("input")!;
const buttonNamed = (name: string) => [...page.querySelectorAll("button")].find(b => b.textContent === name)!;
const buttonNames = () => [...page.querySelectorAll("button")].map(b => b.textContent);
const buttonTypes = () => [...page.querySelectorAll("button")].map(b => b.getAttribute("type"));

describe("amountForm", () => {
  it("shows its title, the field prefilled and named, then Enregistrer, the other actions and Annuler", () => {
    open({ actions: [{ label: "Comme prévu", run: pending }, { label: "N'a pas eu lieu", run: pending }] });

    expect(page.querySelector(".title")?.textContent).toBe("Courses");
    expect(plain(field().value)).toBe("320,00");
    expect(field().getAttribute("aria-label")).toBe("Réel de Courses en mars 2027");
    expect(buttonNames()).toEqual(["Enregistrer", "Comme prévu", "N'a pas eu lieu", "Annuler"]);
    // Only Enregistrer submits: another action never sends the amount typed.
    expect(buttonTypes()).toEqual(["submit", "button", "button", "button"]);
  });

  it("runs another action without the amount, disabling every control meanwhile", () => {
    const submit = vi.fn<AmountFormOptions["submit"]>(pending);
    const run = vi.fn<() => Promise<boolean>>(pending);
    open({ submit, actions: [{ label: "Comme prévu", run }] });

    buttonNamed("Comme prévu").click();

    expect(run).toHaveBeenCalledOnce();
    expect(submit).not.toHaveBeenCalled();
    expect([field(), ...page.querySelectorAll("button")].every(control => control.disabled)).toBe(true);
  });

  it("enables everything again, the focus on the field, when the action is not shown", async () => {
    open({ actions: [{ label: "Comme prévu", run: () => Promise.resolve(false) }] });
    buttonNamed("Annuler").focus();

    buttonNamed("Comme prévu").click();

    await vi.waitFor(() => { expect(field().disabled).toBe(false); });
    expect([...page.querySelectorAll("button")].every(button => !button.disabled)).toBe(true);
    expect(document.activeElement).toBe(field());
  });

  it("clears the message of a wrong amount when another action is chosen", () => {
    open({ actions: [{ label: "Comme prévu", run: pending }] });
    field().value = "abc";
    page.querySelector("form")!.dispatchEvent(new Event("submit", { cancelable: true }));
    expect(page.querySelector(".field-error")?.textContent).not.toBe("");

    buttonNamed("Comme prévu").click();

    expect(page.querySelector(".field-error")?.textContent).toBe("");
  });

  it("sends the amount typed, not the one prefilled", () => {
    const submit = vi.fn<AmountFormOptions["submit"]>(pending);
    open({ submit });
    field().value = "347,85";

    buttonNamed("Enregistrer").click();

    expect(submit).toHaveBeenCalledExactlyOnceWith(347.85);
  });

  it("cancels on Annuler and on Escape, never on another key", () => {
    const cancel = vi.fn<AmountFormOptions["cancel"]>();
    open({ cancel });

    field().dispatchEvent(new KeyboardEvent("keydown", { key: "a", bubbles: true }));
    expect(cancel).not.toHaveBeenCalled();

    field().dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true }));
    buttonNamed("Annuler").click();
    expect(cancel).toHaveBeenCalledTimes(2);
  });

  it("ignores Escape while a call is pending, since the save would still land", () => {
    const cancel = vi.fn<AmountFormOptions["cancel"]>();
    open({ cancel, submit: pending });
    buttonNamed("Enregistrer").click();

    field().dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true }));

    expect(cancel).not.toHaveBeenCalled();
  });

  it("accepts Escape again once a call is refused", async () => {
    const cancel = vi.fn<AmountFormOptions["cancel"]>();
    open({ cancel, submit: () => Promise.resolve(false) });
    buttonNamed("Enregistrer").click();
    await vi.waitFor(() => { expect(field().disabled).toBe(false); });

    field().dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true }));

    expect(cancel).toHaveBeenCalledOnce();
  });

  it("calls afterShown once a result is shown, by Enregistrer or another action, never on a refusal", async () => {
    const afterShown = vi.fn<() => void>();
    let shown = true;
    open({ afterShown, submit: () => Promise.resolve(shown), actions: [{ label: "Comme prévu", run: () => Promise.resolve(shown) }] });

    buttonNamed("Enregistrer").click();
    await vi.waitFor(() => { expect(afterShown).toHaveBeenCalledOnce(); });

    // A new form, as after the redraw: the other action, then a refusal.
    open({ afterShown, submit: () => Promise.resolve(shown), actions: [{ label: "Comme prévu", run: () => Promise.resolve(shown) }] });
    buttonNamed("Comme prévu").click();
    await vi.waitFor(() => { expect(afterShown).toHaveBeenCalledTimes(2); });

    shown = false;
    open({ afterShown, submit: () => Promise.resolve(shown) });
    buttonNamed("Enregistrer").click();
    await vi.waitFor(() => { expect(field().disabled).toBe(false); });
    expect(afterShown).toHaveBeenCalledTimes(2);
  });

  it("focuses the field with its text selected", () => {
    open();

    expect(document.activeElement).toBe(field());
    expect([field().selectionStart, field().selectionEnd]).toEqual([0, field().value.length]);
  });
});
