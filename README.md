# hestia-room-finance

[![CI](https://github.com/alexismoreaullobera/hestia-room-finance/actions/workflows/ci.yml/badge.svg)](https://github.com/alexismoreaullobera/hestia-room-finance/actions/workflows/ci.yml)
[![CodeQL](https://github.com/alexismoreaullobera/hestia-room-finance/actions/workflows/codeql.yml/badge.svg)](https://github.com/alexismoreaullobera/hestia-room-finance/actions/workflows/codeql.yml)
[![Secrets scan](https://github.com/alexismoreaullobera/hestia-room-finance/actions/workflows/gitleaks.yml/badge.svg)](https://github.com/alexismoreaullobera/hestia-room-finance/actions/workflows/gitleaks.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

The **personal finance room** of Hestia: plan the incomes and expenses of each month, forecast the low point of the account, compare plan and actuals, and track subscriptions, with no bank connection. Hestia is a personal AI assistant built from standalone "rooms", one per area of life. Each room is a self-contained application that also exposes an **MCP server** (Model Context Protocol): tools an AI agent can call, and **widgets** (MCP Apps) a host can display.

![show_balance widget rendered in the MCP Inspector](docs/images/t1-inspector-apps.png)

> **Status: in progress.** The business core, its persistence (SQL Server, EF Core) and the four read-only **data tools** of the MCP server are done and tested, on a fictitious demo plan. The widgets come next: until then, the server still exposes the first spike widget, on **fake data**. The v1 features are defined in the [scope](docs/scope.md).

## Features

**Business core** (`Hestia.Room.Finance.Domain`, pure C#):
- **Monthly plan**: recurring incomes and expenses (monthly, quarterly, yearly), subscriptions with their end of commitment, budgets for variable expenses.
- **Month summary**, planned and actual, in two readings: **cash** (what falls due this month) and **smoothed** (a 360 € yearly insurance weighs 30 € every month), with the savings capacity.
- **Cash flow forecast**: the expected balance of the account day by day, its **low point**, the **cash buffer** the month needs, an **alert** below zero, and the **contributing debits** (which debit to move).
- Amounts in euros with cents, every object valid once created.

**Persistence** (`Hestia.Room.Finance.Infrastructure`): SQL Server with EF Core, persistence models separate from the domain, versioned migrations, a reader that loads a month as domain objects, and a fictitious demo plan.

**MCP server** (`Hestia.Room.Finance.Mcp`): Streamable HTTP (stateless), read-only tools for the agent:

| Tool | What it returns |
|---|---|
| `get_month_summary` | Planned and actual summary: incomes, recurring expenses, budgets, savings capacity (cash and smoothed) |
| `get_cash_flow_forecast` | Day-by-day forecast: low point, cash buffer, alert below zero, contributing debits |
| `list_recurring_lines` | Recurring lines of the month, with their date in the month and monthly equivalent |
| `list_subscriptions` | Subscriptions with their monthly and yearly cost, commitment end, and the totals |

Each tool takes an optional `month` (`YYYY-MM`, the current month in Paris time by default) and returns a **text summary** in French (for the model and hosts without UI) plus **structured content** with an advertised output schema. The **`show_balance`** spike tool and its **`ui://finance/balance.html`** widget (MCP Apps) stay until the real widgets replace them.

## Architecture

```
Hestia.Room.Finance.Mcp             MCP server: tools and widgets (ASP.NET Core)
        │ uses
Hestia.Room.Finance.Infrastructure  SQL Server through EF Core: persistence rows, conversions
        │ depends on
Hestia.Room.Finance.Domain          business rules and calculations, pure C#: no database, no MCP, no clock
```

- **Dependencies point inward**: the domain depends on nothing; the infrastructure depends on the domain, never the reverse.
- **Persistence models are separate from the domain**: EF Core maps plain rows (`Rows/`), converted explicitly to domain objects, so storage constraints never leak into the business rules, and an invalid row is rejected on read.
- **Calculations are done by the code, never by the LLM**: tools compute, the agent explains.
- **Thin tools**: a tool loads one month through the reader, then projects it into its output; the reader is injected per HTTP request (scoped, with its DbContext), the clock is a singleton.

## Quickstart

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (the exact version is pinned in `global.json`), [Docker](https://www.docker.com/products/docker-desktop/) (local SQL Server and database tests) and [Node.js](https://nodejs.org/) (for the MCP Inspector only).

1. Clone, then choose a password for the local SQL Server (the `.env` file is never committed):
   ```bash
   git clone https://github.com/alexismoreaullobera/hestia-room-finance.git
   cd hestia-room-finance
   cp .env.example .env
   ```
   Then edit `.env`: at least 8 characters, with 3 of uppercase, lowercase, digit and symbol.
2. Start SQL Server (bound to `localhost` only, data kept in a Docker volume):
   ```bash
   docker compose up -d
   ```
3. Give the server its connection string, once, in the .NET user secrets (stored in your user profile, outside the repository). The password is typed at a hidden prompt, so it never lands in the shell history, and goes through the standard input (`printf` is a shell builtin), so it never shows in the process list either:
   ```bash
   read -rsp "SQL Server password: " SA_PASSWORD && echo
   printf '{"ConnectionStrings:Finance":"Server=127.0.0.1,1433;Database=HestiaFinance;User Id=sa;Password=%s;Encrypt=True;TrustServerCertificate=True"}' "$SA_PASSWORD" | dotnet user-secrets set --project src/Hestia.Room.Finance.Mcp
   unset SA_PASSWORD
   ```
   The password is written into JSON and into a connection string: it must not contain `"`, `\` or `;`.
4. Run the server:
   ```bash
   dotnet run --project src/Hestia.Room.Finance.Mcp
   ```
   In Development, it applies the migrations and seeds the fictitious demo plan into an empty database at startup (never in production, where the deployment pipeline migrates). The MCP endpoint listens on `http://localhost:5135/mcp`.
5. In another terminal, start the [MCP Inspector](https://github.com/modelcontextprotocol/inspector):
   ```bash
   npx @modelcontextprotocol/inspector
   ```
6. In the Inspector, connect with transport **Streamable HTTP** and URL `http://localhost:5135/mcp`. In the **Tools** tab, call `get_cash_flow_forecast` without argument: the demo shows the alert of the current month. The **Apps** tab still shows the `show_balance` spike widget.

`docker compose down` stops the database and keeps the data; `docker compose down -v` deletes it.

## Migrations

The server migrates its database at startup in Development. To apply the migrations by hand, or to add one, use the `dotnet-ef` version pinned in `dotnet-tools.json`, with the connection string in the `HESTIA_FINANCE_CONNECTION` environment variable (never written in the code), set for that one command only:

```bash
dotnet tool restore
```
```bash
read -rsp "SQL Server password: " SA_PASSWORD && echo
HESTIA_FINANCE_CONNECTION="Server=127.0.0.1,1433;Database=HestiaFinance;User Id=sa;Password=$SA_PASSWORD;Encrypt=True;TrustServerCertificate=True" dotnet ef database update --project src/Hestia.Room.Finance.Infrastructure
unset SA_PASSWORD
```

## Tests

**Docker must be running**: the infrastructure and MCP tests start a throwaway SQL Server with Testcontainers.

```bash
dotnet test
```

- **Domain**: every calculation rule of the [scope](docs/scope.md), with business-named tests; sensitive rules checked by mutation.
- **Infrastructure**: domain ↔ row conversions and the schema checked offline from the EF Core model; then, on a real SQL Server (Testcontainers, one fresh migrated database per test): migrations, round trip of every entity, constraints, cascades, the month reader and the demo seeder.
- **MCP server**: unit tests of the tool outputs and text summaries on plans built in memory, of the month argument and of the clock (month change in Paris time); then end-to-end tests on the `/mcp` endpoint with an in-memory server, a real SQL Server and a fixed clock: the four data tools (annotations, output schema, figures equal to the domain applied to the demo plan, tool errors), the spike widget metadata (MIME type, CSP) and host filtering.

Tests use xUnit v3 on the Microsoft Testing Platform (declared in `global.json`).

## Contributing

Every change goes through a pull request: `main` is protected, and the CI (build and tests), CodeQL and the secrets scan must pass before merging (squash merge only).

**Enable the local secrets check once per clone** (a git hook that blocks a commit containing a secret):

1. Install [gitleaks](https://github.com/gitleaks/gitleaks):
   - Windows: `winget install Gitleaks.Gitleaks`
   - macOS: `brew install gitleaks`
   - Linux: see the [releases](https://github.com/gitleaks/gitleaks/releases)
2. Point git to the versioned hooks:
   ```bash
   git config core.hooksPath .githooks
   ```

## Project structure

```
.
├─ src/
│  ├─ Hestia.Room.Finance.Domain/          business rules and calculations
│  ├─ Hestia.Room.Finance.Infrastructure/  EF Core: DbContext, Configurations/, Rows/
│  └─ Hestia.Room.Finance.Mcp/             MCP server: Tools/ (and Outputs/), Time/, Views/ (widgets), Data/ (spike fake data)
├─ tests/                                  one test project per project above, plus Hestia.Room.Finance.Testing (shared SQL Server fixture)
├─ docs/                                   scope and technical notes
├─ docker-compose.yml                      local SQL Server (password in .env, see .env.example)
├─ dotnet-tools.json                       pinned .NET tools (dotnet-ef)
├─ .github/                                CI, CodeQL, secrets scan, Dependabot
├─ .githooks/                              versioned git hooks
└─ Directory.Build.props                   settings shared by every project (vulnerable dependencies fail the build)
```

## Documentation

- [Finance room v1 scope](docs/scope.md): purpose, glossary, data model, calculation rules, views and tools.
- [Spike T1: MCP Apps in C#](docs/spike-mcp-apps.md): how the widget pipeline works, findings and pitfalls.

## Security

This is a local development build: the MCP endpoint has **no authentication yet** and must not be exposed as is. The hardening planned before any deployment is tracked in [#2](https://github.com/alexismoreaullobera/hestia-room-finance/issues/2).

Found a vulnerability? Please report it privately, see [SECURITY.md](SECURITY.md).

## License

[MIT](LICENSE)
