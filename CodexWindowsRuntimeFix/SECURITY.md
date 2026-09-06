# Security Policy

## Supported scope

This project is a small local repair script. Security reports should relate to the behavior of the scripts in this repository, such as unintended file modification, unsafe process termination, path handling, privilege assumptions, or accidental exposure of sensitive data.

## Reporting a vulnerability

Please avoid posting secrets or sensitive system information in a public issue.

When reporting a security concern, include only the minimum information needed to reproduce the problem:

- affected script/version or commit;
- Windows version;
- Codex Desktop version if relevant;
- exact file/path behavior involved;
- a minimal reproduction.

Do not include API keys, access tokens, cookies, passwords, account credentials, or unrelated personal files.

## Design constraints

The repair utility is intentionally designed to:

- read, but not modify, the installed `C:\Program Files\WindowsApps\OpenAI.Codex_*` package;
- avoid network downloads;
- avoid registry, service, firewall, and account changes;
- write only to the current user's Codex runtime cache/log area;
- stop only processes associated with the detected installed Codex package;
- remove only failed staging directories for the detected current runtime after integrity checks pass.

A change that broadens these permissions or behaviors should be treated as security-sensitive and reviewed carefully.
