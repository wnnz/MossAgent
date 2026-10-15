# 文件编辑链路验收

`WorkspaceFileEditingFlowTests` 使用真实 App 服务装配、设置 ViewModel、
SQLite、OpenAI Responses 适配器、AgentRunner、工具管线、审批队列与对话呈现器。
仅模型端由监听随机回环端口的 `FileEditingModelServer` 替代，不调用付费模型。

## 自动覆盖范围

- 从提供商和模型设置的保存命令写入配置，再从工作区主操作命令创建并运行任务。
- 模型先读取授权目录中的文件，从返回摘要获取完整文件 SHA-256，再请求写入或精确替换。
- 等待批准时目标不变，正文默认隐藏；批准后更新文件，包括空文本写入。
- 拒绝保持原文件；审批期间外部改动触发 `file_changed`，保留外部版本。
- 审批期间用工作区停止命令取消，正文清空、队列清空、任务持久化为已取消，
  工具持久化为 `tool_cancelled`，不再请求下一轮模型。
- 只读策略或越权目标在提示审批前拒绝；授权文件和外部哨兵文件均不变。
- 使用重新打开的服务装配核对保存的任务、工具对话卡片及已完成审计，
  与当前工作区卡片的成功状态和错误码保持一致；不残留临时写入文件。
- 独立执行器测试验证审批服务异常不泄露信息，以及批准与取消竞争时，
  即使工具不自行检查取消也不能开始执行。

```powershell
dotnet test tests/MossAgent.App.Tests/MossAgent.App.Tests.csproj --no-restore --disable-build-servers -m:1 --filter FullyQualifiedName~WorkspaceFileEditingFlowTests
dotnet test tests/MossAgent.Tools.Tests/MossAgent.Tools.Tests.csproj --no-restore --disable-build-servers -m:1 --filter FullyQualifiedName~ToolApprovalCancellationTests
dotnet test MossAgent.slnx --no-restore --disable-build-servers -m:1
dotnet build MossAgent.slnx --no-restore --disable-build-servers -m:1
```

## 运行界面验收边界

自动链路测试运行真实 ViewModel 命令，但不等同于鼠标、键盘和像素验收。
实际应用仍需使用独立数据目录与本机模型端，检查输入、审批正文切换、
批准、拒绝、停止和重启后的对话展示；不得使用真实提供商凭据或生产文件。
截图需保持原始窗口尺寸与 DPI，分别检查普通和最大化窗口及明暗主题。

## 2026-10-04 实际界面验收

使用正常权限的真实 `MossAgent.App.exe`、独立临时数据目录和本机 Responses 模型端，
通过鼠标和键盘输入读取、精确替换测试文件的请求。未使用真实凭据、生产文件或付费模型。

- 到达替换审批后，文件仍为 `alpha\n`；操作正文默认隐藏，展开后显示原文本与替换文本。
- 点击“批准本次调用”后文件为 `beta\n`；模型完成读取、替换和最终回复三轮调用。
- 只读核对 SQLite：任务状态为 Completed，保存四条消息，两个成功工具结果，
  两条已完成且批准的审计；文件 SHA-256 与保存的替换结果摘要一致。
- 关闭并重开同一数据目录后，对话、工具成功状态与最终回复恢复；没有重新执行模型或工具。
- 共享工具行使用单行摘要和省略号，不再因路径、哈希撑高；展开后保留完整摘要、
  SHA-256 和原有工具正文。运行中窗口的可访问名称为实际摘要，不再是 Grid 类型名。
- 用仓库外 Python 截图脚本的 `--passive --include-popups` 捕获审批、重开会话、
  普通窗口与最大化窗口，并检查浅色和深色主题。截图保持 1280×820、1920×1032
  原始尺寸，不缩放或拉伸截图；本轮捕获没有黑屏。

### 共享工具行的拒绝路径复测

另建独立临时数据目录，使用同一本机模型端，通过真实输入和鼠标点击拒绝替换审批。

- 审批前后文件字节均为 `616C7068610A`，保持 `alpha\n`，没有执行替换。
- 读取行保留可展开按钮；无详情的拒绝结果显示静态行，不显示展开箭头或禁用按钮。
  可访问树中拒绝摘要与失败状态为文本，读取按钮仍使用实际摘要作为名称。
- SQLite 保存两个工具结果；替换结果与已完成审计均为失败，审批未批准，
  错误码为 `approval_denied`，没有原始详情。模型完成三轮调用并显示最终回复。
- 用仓库外 Python 脚本被动捕获并检查 1280×820 普通窗口和 1920×1032 最大化窗口，
  工具图标、摘要和状态保持对齐，无详情行不占用展开交互。
- 最终组件修正后，完整解决方案 824 项测试通过；解决方案构建零警告、零错误。

本次实际输入覆盖成功批准和拒绝路径。审批中取消、外部文件冲突等仍由自动测试验证，
尚未在本轮逐项鼠标验收；多 DPI 和 Codex 全界面逐像素对照也未完成。
本机模型端验证工具管线与真实 UI，不代表真实服务商或付费模型行为已验收。
