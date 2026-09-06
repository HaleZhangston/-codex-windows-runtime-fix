# Codex Windows Runtime Fix

> **Unofficial community workaround. Not affiliated with, sponsored by, or endorsed by OpenAI.**

A small Windows repair utility for a specific **Codex Desktop startup failure** where Codex launches background `ChatGPT.exe` processes but never creates a visible window.

[![Windows](https://img.shields.io/badge/Windows-10%2F11-blue)](https://www.microsoft.com/windows)
[![PowerShell](https://img.shields.io/badge/PowerShell-5.1%2B-blue)](https://learn.microsoft.com/powershell/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## What problem does this fix?

This project targets the following symptom pattern:

- Codex Desktop starts one or more `ChatGPT.exe` processes, but no window appears.
- `MainWindowHandle` remains `0`.
- `%LOCALAPPDATA%\OpenAI\Codex\runtimes\cua_node` repeatedly accumulates `.staging-*` directories.
- A staging runtime may contain `node.exe` but miss `node_repl.exe`.
- A very deeply nested Node dependency may be missing because normal Windows copy tools can fail on protected or very long paths.

On an affected machine this was observed across multiple Codex Desktop updates, with runtime IDs changing after updates. The script therefore **does not hard-code a Codex version or runtime ID**.

## How it works

The repair script:

1. Reads the currently installed `OpenAI.Codex` MSIX package.
2. Starts Codex once and waits briefly for a normal window.
3. If no window appears, checks whether that launch created a fresh `.staging-*` runtime directory.
4. Extracts the current runtime ID from the staging directory name.
5. Reconstructs the installed `cua_node` runtime under the current user's `%LOCALAPPDATA%` using:
   - `xcopy /G /H` for protected files;
   - `robocopy` for deep directory trees;
   - `\\?\` long-path byte-copy fallback for any remaining files.
6. Compares source and destination file lists.
7. Verifies `node.exe` and `node_repl.exe` exist.
8. Removes only failed staging directories for the detected **current runtime ID** after integrity checks pass.
9. Restarts Codex Desktop.

## Quick start

### Option 1 — double-click

Download or clone this repository, open `CodexWindowsRuntimeFix`, then double-click:

```text
Repair-CodexDesktop.cmd
```

Wait for the script to finish. Do not repeatedly launch Codex while the repair is copying files.

### Option 2 — PowerShell

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Repair-CodexDesktop.ps1
```

## Self-test

A non-destructive self-test is included. It does **not** require Codex Desktop and does not touch the Codex runtime cache.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Repair-CodexDesktop.ps1 -SelfTest
```

The self-test checks:

- runtime-ID parsing;
- relative file enumeration;
- the `\\?\` long-path byte-copy fallback.

GitHub Actions runs the self-test on `windows-latest`.

## Safety and scope

The script intentionally keeps a narrow scope:

- **Does not write to `C:\Program Files\WindowsApps`.** It only reads the already-installed Codex package.
- **Does not download executables, binaries, or runtime files from the Internet.**
- **Does not redistribute OpenAI/Codex binaries.**
- **Does not modify the registry, Windows services, firewall, Microsoft Store settings, or Codex account data.**
- Writes only under `%LOCALAPPDATA%\OpenAI\Codex\runtimes\cua_node` plus its own log file.
- Stops only `ChatGPT.exe` processes whose executable path belongs to the installed `OpenAI.Codex` package.
- Deletes only `.staging-<current-runtime-id>-*` directories after a successful integrity check.

## Logs

If the repair fails, check:

```text
%LOCALAPPDATA%\OpenAI\Codex\one-click-runtime-repair.log
```

When opening an issue, please include:

- Windows version;
- Codex Desktop version;
- the latest `.staging-*` directory name;
- whether `node.exe` / `node_repl.exe` exist;
- the relevant end of the log file.

**Do not post API keys, tokens, account credentials, or unrelated personal data.**

## Known limitations

This is **not** a general Codex repair tool. It only targets the runtime-staging failure described above.

The script intentionally stops instead of guessing when:

- Codex no longer uses the expected MSIX layout;
- no fresh `.staging-*` directory is created;
- the staging naming format changes;
- the runtime still fails integrity checks after copying.

A GitHub Actions pass validates syntax and helper logic, but a true end-to-end repair still requires an affected Windows PC with Codex Desktop installed.

## Observed affected versions

This failure pattern has been observed on the maintainer's affected Windows machine across several Codex Desktop builds, including:

- `26.901.1978.0`
- `26.901.4073.0`
- `26.901.5280.0`

This list is **not** a claim that every installation of those versions is affected.

## Contributing

Bug reports and improvements are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md).

For security-sensitive reports, see [SECURITY.md](SECURITY.md).

## Compliance and trademarks

This repository contains only original workaround scripts and documentation. It does not ship OpenAI binaries, runtime files, logos, API keys, or proprietary assets.

`OpenAI`, `Codex`, and related marks belong to their respective owners. Their names are used only to identify the product this workaround targets. Do not present this project as an official OpenAI utility.

## License

MIT. See [LICENSE](LICENSE).
