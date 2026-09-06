# HaleZhangston Projects

这个仓库用于存放一些个人工具、自动化脚本和问题修复实验。

## 项目列表

### Codex Windows Runtime Fix

> **非官方社区修复工具，与 OpenAI 无隶属、赞助或官方背书关系。**

[`CodexWindowsRuntimeFix/`](CodexWindowsRuntimeFix/) 是一个针对 **Windows 版 Codex Desktop 特定启动故障** 的修复工具。

#### 它解决什么问题？

在部分 Windows 环境中，Codex Desktop 更新或冷启动后可能出现这样的故障模式：

- 点击 Codex 后，任务管理器里能看到一个或多个 `ChatGPT.exe` 进程；
- Codex 主窗口始终没有出现；
- 相关进程的 `MainWindowHandle` 为 `0`；
- `%LOCALAPPDATA%\OpenAI\Codex\runtimes\cua_node` 下反复产生 `.staging-*` 临时目录；
- 临时 runtime 可能已经有 `node.exe`，但缺少 `node_repl.exe`；
- 某些很深的 Node.js 依赖路径可能因为 Windows 文件保护或超长路径问题没有被完整复制。

这个项目就是为了自动识别并修复 **这种特定的 `cua_node` runtime staging 不完整问题**。

#### 它会做什么？

修复脚本会：

1. 自动读取当前安装的 `OpenAI.Codex` MSIX 包；
2. 启动一次 Codex，确认是否真的属于“后台进程存在但无窗口”的情况；
3. 从本次生成的 `.staging-*` 目录中自动识别当前 runtime ID；
4. 从用户电脑上**已经安装好的 Codex 包**读取 `cua_node` runtime；
5. 使用 `xcopy /G /H`、`robocopy` 和 `\\?\` 长路径方式补齐文件；
6. 比较源目录和目标目录，确认没有文件缺失；
7. 检查 `node.exe` 和 `node_repl.exe`；
8. 校验成功后只清理当前 runtime ID 对应的失败 staging 目录；
9. 重新启动 Codex Desktop。

#### 它不会做什么？

- 不上传或重新分发 OpenAI/Codex 二进制文件；
- 不从互联网下载 Codex runtime；
- 不向 `C:\Program Files\WindowsApps` 写文件；
- 不修改注册表、系统服务、防火墙或 Microsoft Store 设置；
- 不读取或修改 API Key、账号密码等登录凭据；
- 不用于绕过 Codex 的权限、安全或账号限制。

完整说明、使用方法、已知限制和日志位置请查看：

**[CodexWindowsRuntimeFix/README.md](CodexWindowsRuntimeFix/README.md)**

---

### CampusNetAutoLogin

Windows 校园网自动登录相关工具。

项目目录：[`CampusNetAutoLogin/`](CampusNetAutoLogin/)

## License

不同子项目可能使用不同许可证，请查看各项目目录中的 `LICENSE` 或 README。
