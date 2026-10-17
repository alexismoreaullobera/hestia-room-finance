# hestia-room-finance

[![CI](https://github.com/alexismoreaullobera/hestia-room-finance/actions/workflows/ci.yml/badge.svg)](https://github.com/alexismoreaullobera/hestia-room-finance/actions/workflows/ci.yml)
[![CodeQL](https://github.com/alexismoreaullobera/hestia-room-finance/actions/workflows/codeql.yml/badge.svg)](https://github.com/alexismoreaullobera/hestia-room-finance/actions/workflows/codeql.yml)
[![Secrets scan](https://github.com/alexismoreaullobera/hestia-room-finance/actions/workflows/gitleaks.yml/badge.svg)](https://github.com/alexismoreaullobera/hestia-room-finance/actions/workflows/gitleaks.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

The **personal finance room** of Hestia: a personal AI assistant built from standalone "rooms", one per area of life. Each room is a self-contained application that also exposes an **MCP server** (Model Context Protocol): tools an AI agent can call, and **widgets** (MCP Apps) a host can display.

![show_balance widget rendered in the MCP Inspector](docs/images/t1-inspector-apps.png)

> **Status: early stage.** The MCP server and its first widget work end to end, with **fake hard-coded data**. Business features (accounts, transactions, CSV import) come next.

## Features

- **`show_balance`** view tool (read-only): a text summary for the model and voice-only hosts, plus structured data for the widget.
- **`ui://finance/balance.html`** widget (MCP Apps): balances and total, light and dark themes.
- Streamable HTTP transport (stateless), `.NET 10`.

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

- **Unit tests** on the `show_balance` tool (structured data, self-sufficient text summary).
- **Integration tests** on the `/mcp` endpoint, with an in-memory server: tool and widget metadata (`_meta.ui`), widget MIME type and CSP, host filtering.

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
├─ src/Hestia.Room.Finance.Mcp/        MCP server (ASP.NET Core)
│  ├─ Tools/                           MCP tools
│  ├─ Views/                           MCP Apps widgets (ui:// resources)
│  └─ Data/                            fake data (temporary)
├─ tests/Hestia.Room.Finance.Mcp.Tests/ unit and integration tests
├─ docs/                               technical notes
├─ .github/                            CI, CodeQL, secrets scan, Dependabot
└─ .githooks/                          versioned git hooks
```

## Documentation

- [Spike T1: MCP Apps in C#](docs/spike-mcp-apps.md): how the widget pipeline works, findings and pitfalls.

## Security

This is a local development build: the MCP endpoint has **no authentication yet** and must not be exposed as is. The hardening planned before any deployment is tracked in [#2](https://github.com/alexismoreaullobera/hestia-room-finance/issues/2).

## License

[MIT](LICENSE)
