# HaleZhangston Projects

A small collection of personal utilities and experiments.

## Projects

### Codex Windows Runtime Fix

Unofficial Windows workaround for a specific Codex Desktop startup failure where background `ChatGPT.exe` processes appear but no application window is created because the local `cua_node` runtime staging is incomplete.

- Automatically detects the installed Codex Desktop MSIX package.
- Detects the current runtime ID instead of hard-coding a version.
- Reconstructs the local runtime from the already-installed package.
- Handles protected files and very long Windows paths.
- Verifies runtime completeness before cleaning failed staging directories.
- Does not redistribute Codex/OpenAI binaries.

See: [`CodexWindowsRuntimeFix/`](CodexWindowsRuntimeFix/)

### CampusNetAutoLogin

Windows utility for campus-network auto-login workflows.

See: [`CampusNetAutoLogin/`](CampusNetAutoLogin/)

## License

Each project may carry its own license. See the license file inside the relevant project directory.
