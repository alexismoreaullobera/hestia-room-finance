// The DOM kit of the views: the only way they create nodes. Text is never written as markup, and attributes go
// through an allowlist; this file is the only one ESLint allows to call setAttribute (see eslint.config.js).
import { formatEuros } from "./format";

// The only attributes the views may set: presentation, accessibility and the kind of a form field, never a link
// (href, src), a form target (action, formaction), a style or an event handler (on*), so that no name typed by the
// user can ever become code or a link. clip-path takes a url(), only a local url(#id) written by the code (never
// data). The value of a field is set through its value property, never as an attribute.
const SAFE_ATTRIBUTES: ReadonlySet<string> = new Set([
  "role", "aria-label", "id", "class", "viewBox", "x", "y", "x1", "x2", "y1", "y2", "cx", "cy", "r",
  "width", "height", "points", "text-anchor", "clip-path", "type", "inputmode", "autocomplete",
]);

export type Attributes = Readonly<Record<string, string | number>>;

function setAttributes(node: Element, attributes: Attributes | undefined): void {
  for (const [name, value] of Object.entries(attributes ?? {})) {
    if (!SAFE_ATTRIBUTES.has(name)) throw new Error(`Attribute not allowed in a view: ${name}`);
    node.setAttribute(name, String(value));
  }
}

export interface ElementOptions {
  className?: string;
  text?: string;
  attributes?: Attributes;
}

/**
 * The tags a view may create: never one that runs or loads something (a script, a style, a frame, an embedded object,
 * a base URL, a link or a meta tag), so that no text given to the kit can become code, style or a navigation.
 */
export type SafeTag = Exclude<keyof HTMLElementTagNameMap,
  "script" | "style" | "iframe" | "frame" | "frameset" | "object" | "embed" | "base" | "link" | "meta">;

/**
 * Creates an element. Text is set with textContent only (strings among the children become text nodes), never as
 * HTML: the data comes from tools and is untrusted (XSS, prompt injection through a name typed by the user).
 */
export function element<K extends SafeTag>(
  tag: K, { className, text, attributes }: ElementOptions = {}, ...children: (Node | string)[]): HTMLElementTagNameMap[K] {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  setAttributes(node, attributes);
  node.append(...children);
  return node;
}

/** Creates an SVG element, with the same rules as element(): safe attributes only, strings as text nodes. */
export function svg<K extends keyof SVGElementTagNameMap>(
  tag: K, attributes: Attributes = {}, ...children: (Node | string)[]): SVGElementTagNameMap[K] {
  const node = document.createElementNS("http://www.w3.org/2000/svg", tag);
  setAttributes(node, attributes);
  node.append(...children);
  return node;
}

/** An amount cell or span, red when negative. */
export function amount(value: number, tag: "span" | "td" = "span"): HTMLElement {
  return element(tag, { className: value < 0 ? "amount negative" : "amount", text: formatEuros(value) });
}

/** The content of a card: its title, its value (a node or a text) and an optional detail. */
export function cardContent(title: string, value: Node | string, detail?: Node | string): HTMLElement[] {
  return [
    element("div", { className: "title", text: title }),
    element("div", { className: "big" }, value),
    ...(detail === undefined ? [] : [element("div", { className: "detail" }, detail)]),
  ];
}

/** A key figure: its title, its value (a node or a text) and an optional detail. Laid out by .cards. */
export function card(title: string, value: Node | string, detail?: Node | string): HTMLElement {
  return element("div", { className: "card" }, ...cardContent(title, value, detail));
}
