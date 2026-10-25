// Structured content of the tools, as the MCP server writes it (Tools/Outputs/*.cs): amounts in euros rounded to the
// cent, months as YYYY-MM, dates as YYYY-MM-DD. The JSON fixtures of test/fixtures are produced from the server and
// checked by a C# test, so these interfaces cannot drift from the output schemas without a test failing.

export interface SummaryFigures {
  incomes: number;
  recurringExpenses: number;
  budgets: number;
  savingsCapacity: number;
  smoothedIncomes: number;
  smoothedRecurringExpenses: number;
  smoothedSavingsCapacity: number;
}

/** A budget of the month: actual is the planned amount when nothing was entered (entered: false). */
export interface BudgetActualOutput {
  id: string;
  name: string;
  planned: number;
  actual: number;
  entered: boolean;
}

export type FlowKind = "Income" | "Expense";

export type LineActualStatus = "AsPlanned" | "Corrected" | "Skipped";

/** A recurring line due this month: actual is the planned amount, the corrected one, or 0 when skipped. */
export interface LineActualOutput {
  id: string;
  name: string;
  kind: FlowKind;
  date: string;
  planned: number;
  actual: number;
  status: LineActualStatus;
}

/** show_month_overview (and get_month_summary). */
export interface MonthSummaryOutput {
  month: string;
  planned: SummaryFigures;
  actual: SummaryFigures;
  /** By name. */
  budgets: BudgetActualOutput[];
  /** The lines due this month, by date. */
  lines: LineActualOutput[];
}

export interface DailyBalanceOutput {
  date: string;
  balance: number;
}

export interface DebitOutput {
  name: string;
  date: string;
  amount: number;
}

/** show_cash_flow_forecast (and get_cash_flow_forecast). */
export interface CashFlowForecastOutput {
  month: string;
  startingBalance: number;
  lowPoint: DailyBalanceOutput;
  cashBuffer: number;
  hasAlert: boolean;
  startsOverdrawn: boolean;
  contributingDebits: DebitOutput[];
  days: DailyBalanceOutput[];
}

export type Frequency = "Monthly" | "Quarterly" | "Yearly";

export type Category =
  | "Other" | "Income" | "Housing" | "Utilities" | "Telecom" | "Insurance" | "Transport"
  | "Health" | "Food" | "Leisure" | "Education" | "Savings" | "Taxes";

export interface SubscriptionOutput {
  id: string;
  name: string;
  amount: number;
  frequency: Frequency;
  category: Category;
  monthlyEquivalent: number;
  yearlyCost: number;
  dueDate: string | null;
  commitmentEndDate: string | null;
}

/** A budget of the plan; actualsEntered is the number of months an actual was entered for. */
export interface PlanBudgetOutput {
  id: string;
  name: string;
  plannedAmount: number;
  actualsEntered: number;
}

/** A recurring line of the plan; overridesEntered is the number of months a correction was entered for. */
export interface PlanLineOutput {
  id: string;
  name: string;
  kind: FlowKind;
  amount: number;
  frequency: Frequency;
  dayOfMonth: number;
  /** 1 to 12 for a quarterly or yearly line, null for a monthly one. */
  anchorMonth: number | null;
  startDate: string;
  endDate: string | null;
  category: Category;
  isSubscription: boolean;
  commitmentEndDate: string | null;
  overridesEntered: number;
}

/** show_plan (and get_plan). */
export interface PlanOutput {
  month: string;
  /** By name. */
  budgets: PlanBudgetOutput[];
  /** The lines running during the month: incomes first, then by day and by name. */
  lines: PlanLineOutput[];
}

/** show_subscriptions (and list_subscriptions). */
export interface SubscriptionsOutput {
  month: string;
  subscriptions: SubscriptionOutput[];
  monthlyTotal: number;
  yearlyTotal: number;
}
