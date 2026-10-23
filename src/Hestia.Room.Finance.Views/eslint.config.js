import js from "@eslint/js";
import { defineConfig } from "eslint/config";
import noUnsanitized from "eslint-plugin-no-unsanitized";
import tseslint from "typescript-eslint";

// The data of the views comes from tools and is untrusted (a name typed by the user): these rules forbid every way of
// turning a string into markup, code or an attribute outside the allowlist of the kit (element() and svg()).
const sinks = [
  { selector: "CallExpression[callee.property.name=/^setAttribute(NS)?$/]", message: "Set attributes through element() or svg() of the kit (allowlist)." },
  { selector: "CallExpression[callee.property.name=/^(setHTMLUnsafe|createContextualFragment|parseFromString)$/]", message: "No markup from strings in a view." },
  { selector: "NewExpression[callee.name='DOMParser']", message: "No markup from strings in a view." },
];

export default defineConfig(
  { ignores: ["dist/", "node_modules/"] },
  js.configs.recommended,
  tseslint.configs.strictTypeChecked,
  noUnsanitized.configs.recommended,
  {
    languageOptions: { parserOptions: { projectService: true, tsconfigRootDir: import.meta.dirname } },
    rules: {
      "no-eval": "error",
      "no-implied-eval": "off",
      "@typescript-eslint/no-implied-eval": "error",
      "no-new-func": "error",
      "no-restricted-properties": ["error",
        { property: "innerHTML", message: "Use textContent (element() of the kit)." },
        { property: "outerHTML", message: "Use textContent (element() of the kit)." },
        { property: "srcdoc", message: "No markup from strings in a view." }],
      "no-restricted-syntax": ["error", ...sinks],
      // Template literals with numbers are the norm in the views (amounts, coordinates).
      "@typescript-eslint/restrict-template-expressions": ["error", { allowNumber: true }],
    },
  },
  {
    // The single place allowed to call setAttribute: behind the allowlist.
    files: ["src/shared/view.ts"],
    rules: { "no-restricted-syntax": ["error", ...sinks.slice(1)] },
  },
  {
    // Tests read nodes that they know exist, and build fakes of the SDK.
    files: ["test/**/*.ts"],
    rules: {
      "@typescript-eslint/no-non-null-assertion": "off",
      "@typescript-eslint/no-unsafe-type-assertion": "off",
    },
  },
  { files: ["**/*.js"], extends: [tseslint.configs.disableTypeChecked] },
);
