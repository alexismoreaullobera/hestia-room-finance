# Finance room: v1 scope

> Ticket: [#7](https://github.com/alexismoreaullobera/hestia-room-finance/issues/7) · Status: **reference for the v1**

## Purpose

Keep every month **balanced**: plan the incomes and expenses of the month, see when the account will be at its lowest, compare the plan with what really happened, and know how much can be saved.

The room tracks **cash flows** (what comes in and goes out), not wealth. Accounts, valuations and savings goals will be handled by a separate room (Wealth).

## Principles

1. **No bank connection.** French banking APIs are only open to licensed providers (PSD2/DSP2 aggregators). Everything is entered manually, so the room never stores bank credentials.
2. **A plan, not a transaction log.** Recurring lines are entered once, budgets once per month, and the actual figures once at the end of the month. Logging every expense by hand is out of reach and out of scope.
3. **Every month must balance.** A negative low point is fixed by moving a debit date or by planning a cash buffer, not by tolerating an overdraft.
4. **Calculations are done by the room's code, never by the LLM.** Tools compute, the agent explains.
5. **Professional terms, explained.** The UI uses the proper terms (French in the UI) with a short definition on hover.

## Glossary

| Term (UI, French) | Meaning |
|---|---|
| **Recurring line** (ligne récurrente) | An income or an expense that repeats: salary, unemployment benefit (ARE), rent, insurance, subscription |
| **Subscription** (abonnement) | A recurring expense with extra information: category, start date, end of commitment |
| **Budget** (budget prévisionnel) | A planned amount for a variable expense item over a month: groceries, outings |
| **Actual** (réel) | What really happened, entered once a month: one total per budget, corrections on recurring lines |
| **Starting balance** (solde de départ du mois) | Balance assumed at the start of the month, 0 € by default |
| **Cash flow forecast** (prévisionnel de trésorerie) | Expected balance of the account, day by day, from the planned incomes and expenses |
| **Low point** (point bas) | The day of the month when the forecast balance is at its lowest |
| **Cash buffer** (coussin de trésorerie) | Minimum amount to have on the account so the forecast never goes below zero |
| **Savings capacity** (capacité d'épargne) | What is left at the end of the month once every expense is paid |

## Data model

```
RecurringLine
  id, kind (income | expense), name, amount (EUR)
  frequency (monthly | quarterly | yearly), day of month (1-31), month (quarterly/yearly only)
  category, start date, end date (optional)
  subscription fields (optional): is subscription, end of commitment date

Budget
  id, name (category), planned monthly amount, active

MonthPlan
  month (YYYY-MM), starting balance (default 0)

MonthActual
  month, budget, actual amount                       one total per budget
RecurringOverride
  month, recurring line, actual amount | skipped     a recurring line differed or did not happen
```

- Amounts are in **euros** only, stored as decimals.
- The occurrences of a recurring line in a month are **computed**, not stored.
- A recurring line is considered **paid as planned** unless an override says otherwise.
- **Not stored**: bank details (IBAN, account numbers), individual transactions.

## Calculation rules

**Occurrences**
- A line due on day 29, 30 or 31 falls on the **last day** of shorter months.
- Week-ends and bank holidays are ignored: it is a forecast, not a statement.

**Monthly summary**
- Planned: incomes − recurring expenses due this month − budgets.
- Actual: same with the actual figures (budgets actuals, overrides).
- **Savings capacity** = planned (or actual) result of the month.
- Yearly and quarterly expenses are also shown as a **monthly equivalent** (a 360 € yearly insurance weighs 30 € per month), in the subscriptions view and the summary, while the cash flow forecast uses their real debit date.

**Cash flow forecast**
- Daily series of the month, starting from the **starting balance**.
- Recurring lines on their day; **budgets spread evenly** over the days of the month.
- **Low point** = minimum of the series, with its date.
- **Cash buffer** = how far the series goes below zero when starting from 0 € (0 if it never does): the need of the month itself.
- **Alert** when the series, starting from the starting balance, goes below zero.
- **Contributing debits**: the expenses between the last income before the low point and the low point, so the user sees which debit to move.

## Views (widgets) of the v1

| View | Content |
|---|---|
| **Month overview** | Incomes, expenses, budgets, planned vs actual, savings capacity |
| **Cash flow forecast** | Daily curve, low point, cash buffer, alert, contributing debits, editable starting balance |
| **Subscriptions** | List, monthly and yearly total cost, next debits |
| **Forms** | Recurring lines and subscriptions, budgets, monthly actuals, starting balance |

Every view tool also returns a **self-sufficient text summary** (model and voice-only hosts).

## MCP tools of the v1

| Kind | Tools | Who calls them |
|---|---|---|
| **Data** (read-only) | `get_month_summary`, `get_cash_flow_forecast`, `list_recurring_lines`, `list_subscriptions` | The agent |
| **Views** | `show_month_overview`, `show_cash_flow_forecast`, `show_subscriptions` | The agent and the hosts |
| **Input** (`Visibility = App`) | create / update / delete recurring lines, budgets, actuals, starting balance | The widgets only, not the model |

The agent has **no write access** in the v1.

## Out of scope

Bank connection, transaction-level tracking, several bank accounts, currencies other than the euro, several users, wealth tracking (separate room), investment advice.

## Later

- Real balance tracking day by day (forecast from the actual balance).
- Subscriptions views by category and by end of commitment.
- Simulation: "what if I move this debit to the 2nd?".
- History and trends over several months.
- Starting balance proposed from the previous month.
- Reminders through the agent (end-of-month actuals, upcoming end of commitment).
- Export (JSON / CSV).

## Decisions

1. **The room is a pure MCP server.** It exposes its tools, its `ui://` widgets, its calculations and its storage, nothing else: no standalone UI and no mini host page.
   - "Useful without AI" is provided by the **Hestia shell in manual navigation** (no agent involved), not by each room embedding its own host.
   - Until the shell exists, the room is used through any MCP Apps host: the MCP Inspector (development, demo) or Claude Desktop.
2. **No REST API in the v1.** Every input goes through MCP (input tools with `Visibility = App`), so a REST API would have no consumer. It will be added when a real one appears (native mobile app, automated import, external integration). The Hestia room contract makes the REST API optional.

## Open questions

1. **Persistence.** SQLite + EF Core locally; the production database is chosen at deployment.
2. **The T1 spike.** `show_balance` and `FakeAccounts` are replaced by the v1 tools and a realistic demo data set (several months of plan and actuals).
