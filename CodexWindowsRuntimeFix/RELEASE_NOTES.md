# Codex Windows Runtime Fix v1.0.0

首个整理为可发布形式的版本。

## 这个版本是做什么的？

它用于处理一种特定的 Windows Codex Desktop 启动故障：

- 点击 Codex 后后台会出现 `ChatGPT.exe`；
- 但 Codex 主窗口始终不出现；
- `MainWindowHandle` 可能保持为 `0`；
- `%LOCALAPPDATA%\OpenAI\Codex\runtimes\cua_node` 下不断生成不完整的 `.staging-*`；
- staging runtime 可能缺少 `node_repl.exe` 或极深层 Node.js 依赖文件。

## 修复方式

脚本自动读取当前安装的 Codex MSIX 包，并根据本次失败启动产生的 staging 目录识别当前 runtime ID。随后从本机已经安装好的 Codex 包中重建用户目录里的 `cua_node` runtime，使用 `xcopy /G /H`、`robocopy` 和 `\\?\` 长路径字节复制补齐文件，最后进行完整性检查并重新启动 Codex。

## 使用

下载 `CodexWindowsRuntimeFix-v1.0.0.zip`，解压后双击：

```text
Repair-CodexDesktop.cmd
```

若需要先做非破坏性自测试：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Repair-CodexDesktop.ps1 -SelfTest
```

## 安全范围

- 不包含或重新分发 OpenAI/Codex 二进制文件；
- 不从互联网下载 runtime；
- 不向 `C:\Program Files\WindowsApps` 写文件；
- 不修改注册表、服务、防火墙或账号凭据；
- 只处理当前用户的 Codex runtime 缓存及日志；
- 只终止属于当前 Codex MSIX 安装路径的 `ChatGPT.exe`。

## 免责声明

这是非官方社区 workaround，与 OpenAI 无隶属、赞助或官方背书关系。它只针对 README 中描述的 runtime-staging 故障，不保证解决所有 Codex Desktop 启动问题。
