# hestia-room-finance

[![CI](https://github.com/alexismoreaullobera/hestia-room-finance/actions/workflows/ci.yml/badge.svg)](https://github.com/alexismoreaullobera/hestia-room-finance/actions/workflows/ci.yml)
[![CodeQL](https://github.com/alexismoreaullobera/hestia-room-finance/actions/workflows/codeql.yml/badge.svg)](https://github.com/alexismoreaullobera/hestia-room-finance/actions/workflows/codeql.yml)
[![Secrets scan](https://github.com/alexismoreaullobera/hestia-room-finance/actions/workflows/gitleaks.yml/badge.svg)](https://github.com/alexismoreaullobera/hestia-room-finance/actions/workflows/gitleaks.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

The **personal finance room** of Hestia: plan the incomes and expenses of each month, forecast the low point of the account, compare plan and actuals, and track subscriptions, with no bank connection. Hestia is a personal AI assistant built from standalone "rooms", one per area of life. Each room is a self-contained application that also exposes an **MCP server** (Model Context Protocol): tools an AI agent can call, and **widgets** (MCP Apps) a host can display.

![show_balance widget rendered in the MCP Inspector](docs/images/t1-inspector-apps.png)

> **Status: in progress.** The business core is done and tested; persistence (SQL Server, EF Core) is being built. The MCP server still exposes the first spike widget on **fake data**: the real tools come next. The v1 features are defined in the [scope](docs/scope.md).

## Features

**Business core** (`Hestia.Room.Finance.Domain`, pure C#):
- **Monthly plan**: recurring incomes and expenses (monthly, quarterly, yearly), subscriptions with their end of commitment, budgets for variable expenses.
- **Month summary**, planned and actual, in two readings: **cash** (what falls due this month) and **smoothed** (a 360 € yearly insurance weighs 30 € every month), with the savings capacity.
- **Cash flow forecast**: the expected balance of the account day by day, its **low point**, the **cash buffer** the month needs, an **alert** below zero, and the **contributing debits** (which debit to move).
- Amounts in euros with cents, every object valid once created.

**Persistence** (`Hestia.Room.Finance.Infrastructure`, in progress): SQL Server with EF Core, persistence models separate from the domain.

**MCP server** (`Hestia.Room.Finance.Mcp`): Streamable HTTP (stateless); for now the **`show_balance`** spike tool and its **`ui://finance/balance.html`** widget (MCP Apps), on fake data.

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

## Quickstart

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (the exact version is pinned in `global.json`) and [Node.js](https://nodejs.org/) (for the MCP Inspector only).

1. Clone and run the server:
   ```bash
   git clone https://github.com/alexismoreaullobera/hestia-room-finance.git
   cd hestia-room-finance
   dotnet run --project src/Hestia.Room.Finance.Mcp
   ```
   The MCP endpoint listens on `http://localhost:5135/mcp`.

2. In another terminal, start the [MCP Inspector](https://github.com/modelcontextprotocol/inspector):
   ```bash
   npx @modelcontextprotocol/inspector
   ```

3. In the Inspector, connect with transport **Streamable HTTP** and URL `http://localhost:5135/mcp`, then open the **Apps** tab and select `show_balance`.

## Tests

```bash
dotnet test
```

- **Domain**: every calculation rule of the [scope](docs/scope.md), with business-named tests; sensitive rules checked by mutation.
- **Infrastructure**: domain ↔ row conversions, and the database schema (column types, keys, cascades, check constraints) checked offline from the EF Core model.
- **MCP server**: unit tests on the `show_balance` tool, and integration tests on the `/mcp` endpoint with an in-memory server (tool and widget metadata, widget MIME type and CSP, host filtering).

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
│  └─ Hestia.Room.Finance.Mcp/             MCP server: Tools/, Views/ (widgets), Data/ (fake data, temporary)
├─ tests/                                  one test project per project above
├─ docs/                                   scope and technical notes
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
