# Security policy

## Supported versions

This project is in early development: only the latest version of the `main` branch is supported.

## Reporting a vulnerability

Please **do not open a public issue** for a security problem.

Report it privately through GitHub: **Security** tab > **Report a vulnerability**
([direct link](https://github.com/alexismoreaullobera/hestia-room-finance/security/advisories/new)).

Please include what you found, how to reproduce it, and its potential impact.
This is a personal project maintained on a best-effort basis: you can expect a first answer within a week.

## Known limitations

The MCP endpoint has **no authentication yet** and is meant for local development only.
It must not be exposed publicly. Production hardening is tracked in
[#2](https://github.com/alexismoreaullobera/hestia-room-finance/issues/2).
