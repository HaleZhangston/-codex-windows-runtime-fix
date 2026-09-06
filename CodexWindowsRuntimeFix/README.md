# Codex Windows Runtime Fix

> **非官方社区修复工具。与 OpenAI 无隶属、赞助或官方背书关系。**  
> **Unofficial community workaround. Not affiliated with, sponsored by, or endorsed by OpenAI.**

一个用于修复 **Windows 版 Codex Desktop 特定启动故障** 的小工具：Codex 后台进程已经启动，但主窗口始终没有出现，同时本地 `cua_node` runtime staging 不完整。

[![Windows](https://img.shields.io/badge/Windows-10%2F11-blue)](https://www.microsoft.com/windows)
[![PowerShell](https://img.shields.io/badge/PowerShell-5.1%2B-blue)](https://learn.microsoft.com/powershell/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## 这个项目为什么存在？

在一台实际受影响的 Windows 机器上，Codex Desktop 在多次更新后重复出现以下现象：

- 点击 Codex 后没有任何可见窗口；
- 任务管理器中却存在一个或多个 `ChatGPT.exe` 进程；
- 这些进程的 `MainWindowHandle` 为 `0`；
- `%LOCALAPPDATA%\OpenAI\Codex\runtimes\cua_node` 下不断新增 `.staging-*` 目录；
- 某些 staging 目录中 `node.exe` 已存在，但 `node_repl.exe` 缺失；
- 某些极深层 Node.js 依赖文件可能因为 Windows 文件保护或超长路径问题未能完整复制；
- Codex 更新后 runtime ID 会变化，因此上一个版本手工补好的 runtime 可能不再适用。

这个项目把手工排查和修复过程整理成脚本，目标是：

> **自动识别当前 Codex 版本对应的 runtime，并把本机已安装的 `cua_node` runtime 完整恢复到 Codex 预期的用户目录。**

它不是通用的 Codex 修复器，也不是破解、补丁或第三方客户端。

## 适用症状

这个工具只建议用于以下症状高度一致的情况：

- Codex Desktop 无窗口；
- 后台存在 `ChatGPT.exe`；
- `MainWindowHandle = 0`；
- `cua_node` 目录持续产生 `.staging-*`；
- staging runtime 文件数明显少于安装包中的源 runtime；
- `node_repl.exe` 或深层依赖文件缺失。

如果你的问题是登录失败、网络错误、模型不可用、MCP 配置错误、显卡渲染异常、CLI 版本过旧等，这个工具**不一定适用**。

## 它是怎么修的？

脚本会按下面的顺序工作：

1. 读取当前安装的 `OpenAI.Codex` MSIX 包；
2. 正常启动一次 Codex，并短暂等待主窗口；
3. 如果窗口没有出现，检查这次启动是否产生了新的 `.staging-*`；
4. 从 staging 名称中解析当前 runtime ID；
5. 从本机已经安装的 Codex 包中读取 `app\resources\cua_node`；
6. 使用以下方式重建用户目录里的 runtime：
   - `xcopy /G /H`：处理受保护/加密属性和隐藏文件；
   - `robocopy`：补齐深层目录树；
   - `\\?\` 长路径 + .NET 字节读写：补齐传统复制工具可能遗漏的超长路径文件；
7. 比较源 runtime 与目标 runtime 的完整文件列表；
8. 确认 `node.exe`、`node_repl.exe` 存在；
9. 只有在完整性检查通过后，才删除当前 runtime ID 对应的失败 `.staging-*`；
10. 重新启动 Codex Desktop。

## 快速使用

### 方法一：双击运行

下载或克隆仓库后，进入：

```text
CodexWindowsRuntimeFix
```

双击：

```text
Repair-CodexDesktop.cmd
```

等待脚本完成即可。修复过程中不要反复点击 Codex。

### 方法二：PowerShell

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Repair-CodexDesktop.ps1
```

## Self-test / 自测试

项目包含一个**非破坏性自测试模式**。它不会修改 Codex runtime，也不要求电脑已安装 Codex Desktop。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Repair-CodexDesktop.ps1 -SelfTest
```

自测试会检查：

- `.staging-*` runtime ID 解析；
- 相对文件路径枚举；
- `\\?\` Windows 长路径字节复制逻辑。

GitHub Actions 也会在 `windows-latest` 上运行这些测试。

## 安全范围

脚本刻意保持较小的权限和修改范围：

- **不会向 `C:\Program Files\WindowsApps` 写文件**，只读取本机已经安装的 Codex 包；
- **不会从互联网下载任何 EXE、DLL、runtime 或 Codex 文件**；
- **不会重新分发 OpenAI/Codex 二进制文件**；
- **不会修改注册表、Windows 服务、防火墙、Microsoft Store 设置或 Codex 账号信息**；
- 只在 `%LOCALAPPDATA%\OpenAI\Codex\runtimes\cua_node` 下重建当前 runtime；
- 只终止可确认属于当前 `OpenAI.Codex` MSIX 安装路径的 `ChatGPT.exe` 进程；
- 只有完整性检查通过后，才删除 `.staging-<current-runtime-id>-*` 临时目录；
- 日志只写入 Codex 本地数据目录。

## 日志位置

如果修复失败，请查看：

```text
%LOCALAPPDATA%\OpenAI\Codex\one-click-runtime-repair.log
```

提交 Issue 时建议附上：

- Windows 版本；
- Codex Desktop 版本；
- 最新 `.staging-*` 目录名；
- `node.exe` / `node_repl.exe` 是否存在；
- 日志最后几十行；
- 是否能看到后台 `ChatGPT.exe`，以及 `MainWindowHandle` 是否为 `0`。

**请不要提交 API Key、Token、密码、账号凭据或无关个人信息。**

## 已观察到的版本

在维护者实际受影响的 Windows 机器上，这一故障模式曾在以下 Codex Desktop 构建中观察到：

- `26.901.1978.0`
- `26.901.4073.0`
- `26.901.5280.0`

这只是实际观察记录，**不代表所有安装这些版本的用户都会遇到问题**，也不代表其他版本一定不会出现类似情况。

## 已知限制

这个项目只针对本文描述的 runtime-staging 故障。

如果出现以下情况，脚本会尽量停止而不是猜测：

- Codex 不再使用当前 MSIX 目录结构；
- 当前失败启动没有生成新的 `.staging-*`；
- staging 命名规则改变；
- 安装包本身缺少关键 runtime 文件；
- 补齐后源/目标文件仍不一致；
- runtime 已完整但 Codex 仍然没有创建窗口。

GitHub Actions 通过只能证明 PowerShell 语法、自测试和静态安全检查通过，**不能代替真实受影响 Windows 机器上的端到端验证**。

## 项目性质 / Disclaimer

这个项目是独立的社区 workaround，只提供原始 PowerShell/CMD 脚本和文档。

它：

- 不包含 OpenAI 二进制文件；
- 不包含 Codex runtime 文件；
- 不包含 OpenAI Logo 或官方视觉资产；
- 不尝试冒充官方工具；
- 不用于绕过 OpenAI 的安全、权限、授权、计费或账号限制。

`OpenAI`、`Codex` 及相关名称和商标属于其各自权利人。这里仅用于说明本项目所针对的软件。

## Contributing

欢迎提交 Bug 报告和改进建议：见 [CONTRIBUTING.md](CONTRIBUTING.md)。

安全相关问题请查看 [SECURITY.md](SECURITY.md)。

## License

MIT，见 [LICENSE](LICENSE)。
