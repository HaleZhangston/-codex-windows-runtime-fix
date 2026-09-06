# Contributing

Thanks for helping improve this workaround.

## Good bug reports

Please include:

- Windows version;
- Codex Desktop version;
- whether background `ChatGPT.exe` processes appear;
- whether `MainWindowHandle` remains `0`;
- the newest `.staging-*` directory name under `%LOCALAPPDATA%\OpenAI\Codex\runtimes\cua_node`;
- whether `bin\node.exe` and `bin\node_repl.exe` exist in that staging directory;
- the relevant end of `%LOCALAPPDATA%\OpenAI\Codex\one-click-runtime-repair.log`.

Do **not** include API keys, tokens, passwords, cookies, account credentials, or unrelated personal information.

## Pull requests

Keep changes focused on this specific Windows runtime-staging failure.

Before opening a PR:

1. Run the non-destructive self-test:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Repair-CodexDesktop.ps1 -SelfTest
```

2. Avoid adding network downloads or bundled OpenAI/Codex binaries.
3. Avoid changing registry, services, firewall, Microsoft Store, or WindowsApps contents.
4. Document any new filesystem writes or deletions clearly.
5. Prefer failing safely over guessing when Codex package layout changes.

## Code style

- Keep PowerShell compatible with Windows PowerShell 5.1 where practical.
- Use explicit error handling for destructive steps.
- Keep runtime detection dynamic; do not hard-code Codex build numbers or runtime IDs.
- Add comments where Windows path/protection behavior is non-obvious.

## Scope

This project is not intended to become a general Windows cleanup utility or a broad Codex launcher. Changes should stay tied to the documented `cua_node` staging failure pattern.
