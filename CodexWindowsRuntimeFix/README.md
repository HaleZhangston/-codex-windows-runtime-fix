# Codex Windows Runtime Workaround (Unofficial)

> **Unofficial community workaround. Not affiliated with, sponsored by, or endorsed by OpenAI.**

This small Windows script targets one specific Codex Desktop startup failure pattern:

- `ChatGPT.exe` processes start, but no Codex window appears.
- `MainWindowHandle` remains `0`.
- `%LOCALAPPDATA%\OpenAI\Codex\runtimes\cua_node` repeatedly accumulates `.staging-*` directories.
- A staging runtime may contain `node.exe` but miss `node_repl.exe`, or may miss a deeply nested Node dependency because of Windows file-protection / long-path copy behavior.

The script **does not contain or redistribute any OpenAI/Codex binaries**. It reads the already-installed Microsoft Store/MSIX package on the local PC and reconstructs that same installed runtime under the current user's `%LOCALAPPDATA%` directory.

## What it does

1. Reads the currently installed `OpenAI.Codex` MSIX package dynamically.
2. Starts Codex once and waits briefly for a normal window.
3. If the window does not appear and the current launch creates a new `.staging-*` runtime directory, extracts that runtime ID.
4. Copies the installed `cua_node` runtime with `xcopy /G /H` and `robocopy`.
5. Compares source and destination file lists.
6. Copies any remaining very-long-path files with the Windows `\\?\` path prefix and byte-for-byte I/O.
7. Validates `node.exe`, `node_repl.exe`, and that no source files are missing.
8. Removes only failed staging directories for the **current** runtime ID, then restarts Codex.

## Safety / scope

- **Does not write to `C:\Program Files\WindowsApps`.** That location is read-only for this workaround.
- **Does not download anything from the Internet.**
- **Does not change the registry, Windows services, firewall, Store settings, or Codex account data.**
- Writes only under `%LOCALAPPDATA%\OpenAI\Codex\runtimes\cua_node` and its own log file.
- Stops only `ChatGPT.exe` processes whose executable path is inside the installed `OpenAI.Codex` MSIX package, instead of killing unrelated ChatGPT desktop processes.
- Deletes only `.staging-<current-runtime-id>-*` directories after a successful integrity check.

## Usage

1. Download or clone this folder on Windows.
2. Double-click `Repair-CodexDesktop.cmd`.
3. Wait for the script to finish. Do not repeatedly launch Codex while the copy is in progress.
4. If it fails, inspect:

```text
%LOCALAPPDATA%\OpenAI\Codex\one-click-runtime-repair.log
```

You can also run the PowerShell script directly:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Repair-CodexDesktop.ps1
```

## Self-test

The repository includes a non-destructive self-test mode that does **not** require Codex Desktop. It tests runtime-ID parsing, relative-file enumeration, and the long-path byte-copy fallback:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Repair-CodexDesktop.ps1 -SelfTest
```

GitHub Actions runs this self-test on `windows-latest` for changes to this folder.

## Limitations

This is a workaround for a particular runtime-staging failure, not a general Codex repair utility. If a Codex update changes the package layout, staging naming scheme, or startup architecture, the script intentionally stops rather than guessing. A complete end-to-end test requires an affected Windows PC with Codex Desktop installed; CI only validates PowerShell syntax and the non-destructive helper logic.

## Compliance and trademarks

This repository contains only the workaround scripts and documentation; it does not ship OpenAI binaries, runtime files, logos, API keys, or proprietary assets. `Codex`, `OpenAI`, and related marks belong to their respective owner. Their names are used only to identify the product this workaround targets. Do not present this project as an official OpenAI utility.

OpenAI's Codex CLI repository is Apache-2.0 licensed, but this workaround does not copy source code from that repository. The scripts in this folder are released under the MIT License below.

## License

MIT. See `LICENSE`.
