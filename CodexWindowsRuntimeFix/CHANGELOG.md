# Changelog

All notable changes to **Codex Windows Runtime Fix** are documented here.

## [1.0.0] - 2026-09-06

### Added

- First public-ready release of the Windows runtime-staging workaround.
- Automatic detection of the currently installed `OpenAI.Codex` MSIX package.
- Automatic detection of the runtime ID from a fresh `.staging-*` directory.
- Runtime reconstruction using `xcopy /G /H` plus `robocopy`.
- `\\?\` long-path byte-copy fallback for deeply nested dependencies that normal copy tools may omit.
- Integrity checks for the complete source/target file list, `node.exe`, and `node_repl.exe`.
- Scoped process handling that targets only `ChatGPT.exe` processes belonging to the installed Codex package.
- Non-destructive `-SelfTest` mode and Windows GitHub Actions checks.
- Chinese documentation describing the failure pattern, scope, safety properties, limitations, and troubleshooting information.

### Scope

This release targets one specific Windows Codex Desktop failure pattern: background processes start but no visible window is created, while `%LOCALAPPDATA%\OpenAI\Codex\runtimes\cua_node` repeatedly accumulates incomplete `.staging-*` directories.

It is not a general Codex repair tool and does not bypass authentication, authorization, billing, or security controls.
