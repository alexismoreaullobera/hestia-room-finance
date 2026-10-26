// French names of the enums of the outputs: a Record over the union types, so a value added to outputs.ts without a
// translation does not compile; a C# test checks that the unions of outputs.ts equal the domain enums.
import type { Category, FlowKind, Frequency } from "./outputs";

export const frequencies: Readonly<Record<Frequency, string>> = { Monthly: "mensuel", Quarterly: "trimestriel", Yearly: "annuel" };

export const categories: Readonly<Record<Category, string>> = {
  Other: "Autre", Income: "Revenu", Housing: "Logement", Utilities: "Énergie", Telecom: "Télécom",
  Insurance: "Assurance", Transport: "Transport", Health: "Santé", Food: "Alimentation", Leisure: "Loisirs",
  Education: "Éducation", Savings: "Épargne", Taxes: "Impôts",
};

export const kinds: Readonly<Record<FlowKind, string>> = { Income: "Entrée", Expense: "Dépense" };

/** The months of the year, January first: the name of month n is at index n - 1. */
export const monthNames: readonly string[] = [
  "janvier", "février", "mars", "avril", "mai", "juin", "juillet", "août", "septembre", "octobre", "novembre", "décembre",
];
