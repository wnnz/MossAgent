# MossAgent

MossAgent 是 Windows 优先、基于 Avalonia 12 和 .NET 10 的桌面 Coding Agent。
目标是覆盖 Codex Desktop 的主要功能与工作流，优先保证实际可用、安全和稳定。

## 当前状态与文档

已具备对话/历史/流式、项目与文件工具、Git/工作树/终端、提供商/模型/代理、
浏览器、MCP、子代理、定时任务和只读 PR 审阅等主要运行链路。
**产品尚未完成，当前不满足发布验收条件**；命令/终端的授权根外文件访问仍是安全阻断，
原生 MXC 方案需要尚未就绪的独立 Windows 验收环境。

- [文档入口](docs/README.md)：核心指南与按主题整理的历史证据。
- [功能清单与详细实施方案](docs/FEATURE_STATUS_AND_IMPLEMENTATION_PLAN.md)：已完成的限定能力、未完成项和验证范围。
- [架构](docs/ARCHITECTURE.md)、[安全边界](docs/SECURITY.md)、[共享组件](docs/UI_COMPONENTS.md)。
- [AGENTS.md](AGENTS.md)：修改前必须阅读的工程约束。

## 构建与运行

需要 Windows 10 19041 或更新版本、.NET SDK 10、PowerShell 7（`pwsh`）。
浏览器功能需要对应的 Chrome/Edge 或可用的 WebView2 运行环境。

```powershell
dotnet restore MossAgent.slnx
dotnet build MossAgent.slnx
dotnet test MossAgent.slnx --no-build
dotnet run --project src/MossAgent.App/MossAgent.App.csproj
```

这些是开发命令，不代表当前候选已完整通过所有测试；实际结果见功能清单的证据与限制。
普通 Debug/Release 调试及运行使用 JIT。桌面项目默认并限定为 Windows x64，
Domain、Application 等核心模块保持平台中立。

## 数据与独立实例

默认数据目录为 `%LOCALAPPDATA%\MossAgent`。测试或预览请使用独立目录，避免影响日常数据：

```powershell
$env:MOSSAGENT_DATA_DIR = Join-Path $env:TEMP 'MossAgent-preview'
dotnet run --project src/MossAgent.App/MossAgent.App.csproj
```

API key 和代理凭据按当前产品决定明文保存在本地 SQLite 中。
不要将数据库、密钥、授权头或 Cookie 放入文档、日志、诊断导出或 Git。

## Windows 单文件发布

完成对应 Release 验证后运行：

```powershell
.\scripts\publish.ps1 -Runtime win-x64 -Version 0.1.0
```

脚本使用 `WindowsX64` 配置，生成 `artifacts/publish/win-x64/MossAgent.App.exe`
及 `artifacts/publish` 下的 ZIP。只在已有等效 Release 测试结果时使用 `-SkipTests`。
编译、单文件验证和压缩完成后才替换已有包；失败保留旧包，清理本次暂存。

发布保持 **JIT、自包含、单文件，关闭 AOT 和裁剪**。包内包括 .NET 运行时、原生依赖
和 Playwright 的 Windows x64 Node 驱动/共享 JavaScript/许可证；浏览器需单独安装。
首次启动会将依赖解压到 .NET bundle 缓存，默认 `%TEMP%\.net`，可用
`DOTNET_BUNDLE_EXTRACT_BASE_DIR` 调整。缓存必须可写，提取的原生/Node 依赖必须可执行；
应用或所属浏览器运行时不要删除该缓存。用户数据仍在独立应用数据目录。
