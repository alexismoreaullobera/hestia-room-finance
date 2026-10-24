// Formats of the views: French, as the text summaries of the tools. Pure functions, no DOM.
// The amounts come already rounded to the cent by the server.

const euros = new Intl.NumberFormat("fr-FR", { style: "currency", currency: "EUR" });
const monthNames = new Intl.DateTimeFormat("fr-FR", { month: "long", year: "numeric", timeZone: "UTC" });

/** 1234.5 -> "1 234,50 €". */
export function formatEuros(amount: number): string {
  return euros.format(amount);
}

const decimal = new Intl.NumberFormat("fr-FR", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/** 1234.5 -> "1 234,50": an amount to edit in a field, without the euro sign; parseEuros reads it back. */
export function formatAmount(amount: number): string {
  return decimal.format(amount);
}

/** "2026-10" -> "Octobre 2026". */
export function formatMonth(month: string): string {
  const [year = 0, monthNumber = 1] = month.split("-").map(Number);
  // setUTCFullYear, not Date.UTC: Date.UTC reads the years 0 to 99 as 1900 to 1999.
  const date = new Date(0);
  date.setUTCFullYear(year, monthNumber - 1, 1);
  const name = monthNames.format(date);
  return name.charAt(0).toUpperCase() + name.slice(1);
}

/** "2026-10-05" -> "05/10". */
export function formatDay(date: string): string {
  const [, month, day] = date.split("-");
  return `${day}/${month}`;
}

/** "2026-10-05" -> "05/10/2026". */
export function formatDate(date: string): string {
  const [year, month, day] = date.split("-");
  return `${day}/${month}/${year}`;
}

/** "2026-12", +1 -> "2027-01": the month before or after, as YYYY-MM. */
export function shiftMonth(month: string, delta: number): string {
  const [year = 0, monthNumber = 1] = month.split("-").map(Number);
  const index = year * 12 + (monthNumber - 1) + delta;
  return `${String(Math.floor(index / 12)).padStart(4, "0")}-${String((index % 12) + 1).padStart(2, "0")}`;
}

// An amount as a user types it: an optional minus (or the minus sign of the French format), whole euros, grouped by
// thousands with spaces or not, at most two decimals after a comma or a point, an optional euro sign. \s covers the
// no-break spaces of formatEuros, so a formatted amount reads back.
const TYPED_AMOUNT = /^([-−])?(\d{1,3}(?:\s\d{3})+|\d+)(?:[.,](\d{1,2}))?(?:\s?€)?$/u;

/**
 * "1 250,50" -> 1250.5, "-300" -> -300, "12.5 €" -> 12.5; null for anything else (empty, letters, three decimals,
 * misplaced spaces). The syntax only: the bounds are the server's (a tool error), a single rule for every client.
 */
export function parseEuros(text: string): number | null {
  const match = TYPED_AMOUNT.exec(text.trim());
  if (match === null) return null;
  const [, sign, whole = "", cents = "0"] = match;
  const value = Number(`${whole.replace(/\s/gu, "")}.${cents}`);
  // No -0: "-0" is 0, written as such in the JSON of the call.
  return sign !== undefined && value !== 0 ? -value : value;
}

/** A translation from a table, or the value itself: own keys only, never an inherited one such as "constructor". */
export function translate(table: Readonly<Record<string, string>>, value: string): string {
  return Object.hasOwn(table, value) ? (table[value] ?? value) : value;
}
