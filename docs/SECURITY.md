# 安全边界与 Windows 隔离

本文件集中记录安全设计和已确定的隔离路线。功能项、当前证据与详细实施步骤见
[功能清单 R01/R02/R04](FEATURE_STATUS_AND_IMPLEMENTATION_PLAN.md)。
以下结论沿用截至 2026-10-21 的检查点，本次文档整理没有重新提权、修改系统或运行隔离实验。

## 尚未关闭的阻断

**命令工具和授权交互终端已落入每任务 AppContainer，但其余代码执行入口仍可访问
任务授权根之外的文件。** 工作目录校验、Job Object、审批和文件工具句柄安全都不能
替代操作系统级文件隔离。GitProcessRunner、应用后台 Git、stdio MCP server 与未走
授权路径的终端入口仍按宿主身份运行；不能只限制 Shell 和 terminal.start 就宣称
整个任务隔离完成。

原生产管线反例与研究保存在[命令隔离记录](archive/SECURITY.md#command-filesystem-isolation-plan)。
在该缺口和关键工作流验收关闭前，不宣称产品安全完成或具备发布条件。

## 统一安全规则

- 所有工具经过查找、schema、能力/路径、风险、审批、超时/取消、执行、输出限制/产物及审计。
  UI 快捷入口和子代理不得旁路调用实现；未知 MCP 默认有副作用。
- 无项目聊天不隐式授权文件、Shell 或目录。项目支持多根授权，必须校验穿越、链接、
  junction、重解析点和 Windows 大小写；操作使用实际对象身份，不能只比较字符串。
- ReadOnly 拒绝变更、Shell、页面修改及有副作用的 MCP；AskEveryTime 逐次审批相应动作；
  FullAccess 仍不允许授权根越界、系统破坏或接管不属于任务的浏览器。
- 每任务独立拥有取消、事件、进程树和浏览器会话。取消和超时之后仍须等待所属资源排空，
  清理失败要可见；不能把停止等待当作进程或远端副作用已结束。
- 文件写入保留原子替换和外部并发变更检测。删除、撤销和失败清理绑定原对象，
  不能按已被替换的名称删除后来出现的数据。
- 不自动重放中断命令、写入、浏览器操作或其他未知副作用。恢复先展示已知效果、失败与待核实状态。
- 不在日志、错误、审计或导出中记录 API key、代理凭据、授权头、Cookie。
  本地 SQLite 中明文保存密钥是现有产品决定，不能据此放宽日志和导出约束。

## 已实现的本地文件边界及限制

| 机制 | 已有范围 | 仍需完成 |
| --- | --- | --- |
| 项目授权版本 | schema 26 条件保存、旧租约撤销、跨实例检测 | 严格撤权排空、旧资源迟到结果与提交屏障 |
| 项目根对象 grant | schema 27 绑定卷/目录文件 ID，旧项目显式确认 | ABA、别名及完整平台/文件系统支持矩阵 |
| 工作树对象 grant | schema 28 绑定 task/project/目录对象，恢复拒绝同路径替换 | 创建认领、清理交接、替换对象重新授权与崩溃恢复 |
| 文件与产物会话 | 已接入入口按句柄校验、父链固定、原子写、并发检查与原对象清理 | 最终名称条件替换、剩余枚举入口、链接/并发矩阵及产物崩溃回收 |

这些是 Windows 生产装配中的有界能力；可选端口未接入的兼容宿主不自动具备相同保证。
受控负例和具体限制见[安全归档](archive/SECURITY.md)及[工作树归档](archive/GIT_WORKTREES.md)。

## 已确定的命令隔离路线

采用 **Windows 原生 AppContainer 进程沙箱**。此前的 MXC 路线已被替换；
Microsoft.Mxc.Sdk 引用已从 `MossAgent.Platform.Windows` 与集中包管理中移除。
历史决策记录与探针细节保留在[安全归档](archive/SECURITY.md#windows-isolation-backend-decision)，
不作为生产实现。

当前实现（`MossAgent.Platform.Windows/Sandbox` + `Processes`）：

- 任务进程统一经过 `ITaskProcessLauncher`；`ProcessIsolationSpec` 由
  `ToolExecutionContextIsolation` 从任务上下文派生，携带授权可写根、只读运行时路径、
  私有 temp 目录与网络开关。
- 容器身份按 `mossagent-<taskId>-<rootsHash>` 复用，`CreateAppContainerProfile`
  幂等创建；引用计数归零的 profile 与授权共享 5 分钟宽限，由后台扫描或 Dispose
  `DeleteAppContainerProfile` 回收（立即删除会让同 key 在途 CreateProcess 失靶）。
- ACL 授权经引用计数台账（`WindowsSandboxGrantLedger`/`Entry`/`Release`）：授予
  Modify（目录/文件）或 ReadAndExecute（可执行），宽限期 5 分钟、扫描与 Dispose 回收；
  启动失败按序回滚已授予项并释放 SID 引用。
- `CreateProcess` 挂起创建，`PROC_THREAD_ATTRIBUTE_SECURITY_CAPABILITIES` 附带
  容器与 capability SID，同时保留继承句柄列表与 Job 列表属性；全部 setup 成功后才
  `ResumeThread`。Job kill-on-close 覆盖超时、取消、释放与失败路径。
- 交互终端（`terminal.start` 授权路径）使用自实现 ConPTY：两条匿名管道 +
  `CreatePseudoConsole`，子进程经 `PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE`（lpValue 直接
  传 HPCON，而非指向它的指针）挂到伪控制台，同时附带 `SECURITY_CAPABILITIES` 与
  `JOB_LIST`；`STARTF_USESTDHANDLES` 置位且标准句柄留空，否则子进程继承宿主 stdio、
  ConPTY 管道被完全旁路。授权失败同样 fail-closed，不回退 `PtyProvider.SpawnAsync`。
- 终端与命令 launcher 共享同一个 `WindowsSandboxGrantLedger` 单例（DI 注入），
  同 key 的 profile/ACL 授予不会因某一侧提前释放而被对侧剥离。
- 私有 temp 目录按任务创建并获 Modify 授权，启动时叠加 TMP/TEMP 环境覆盖；
  实际运行中命名 profile 还会把 TMP/TEMP 重定向到 `Packages\<key>`（平台行为覆盖）。
- 启动失败 fail-closed：沙箱授权或资源准备失败直接抛错，不回退到普通主机进程。
- `ShellRunTool`、`GitCommandRunner`（含 `FileChangeDiffRunner` 的 diff 临时输入）、
  `SearchTextTool` 与授权 `terminal.start` 已走沙箱；`GitProcessRunner`、`StdioMcpClient`、
  应用后台 Git 与无授权根的 `terminal.start`（宿主兼容路径）因缺少任务上下文切片
  保持原样，不走沙箱。

## 已验证行为与当前主机限制

- 已验证：容器内执行、授权根内读写、根外写入拒绝且外部标记不变、私有 temp、
  子孙进程随 Job 终止、取消/超时/释放路径、缺失授权目标与不可授权可执行 fail-closed。
- **Git 不可沙箱**：本机 `D:\Program Files\Git` 的 DACL 无包可读项且当前用户无权
  改写，task-path Git 在沙箱内无法启动；`MissingGrantTarget_*` 等用例直接断言该
  fail-closed 行为，需要真实沙箱 Git 的工作流用例按环境前置显式跳过，不做弱化。
- **pwsh 7 受限**：未注册 AppContainer 中 PowerShell 7 命令发现退化（标准 cmdlet 缺失），
  `ShellRunTool` 与沙箱化终端因此改用收件 `powershell.exe`（Windows PowerShell 5.1，
  容器内功能完整）；无授权根的终端兼容路径仍用 `pwsh` 经 `PtyProvider.SpawnAsync`。
- **容器内 FileSystem provider 受限**：`Set-Location`、`Set-Content`、`Move-Item` 等
  cmdlet 对已授予 DACL 的路径仍返回拒绝访问（PowerShell provider 路径解析在未注册
  AppContainer 中退化）；`[IO.File]`/`[IO.Directory]` 原始 API 正常。进程的 NT 工作目录
  正确落在授权根，但 PowerShell `$PWD` 只显示 `C:\`（provider 表象，非真实 cwd）。
- 已实测：junction 逃逸到根外被拒（`SandboxBoundaryTests`）、多授权根同时可达、
  `filesystem.search_text` 的真实 rg 在容器内运行且不能越出授权根（rg 目录不可授予时
  按环境前置跳过）、网络 capability SID 打包与禁网容器 socket 拒绝（`SandboxNetworkTests`）。
- 待验矩阵：UNC 路径、
  发布单文件包内运行时依赖定位，以及在可授权 Git/shell 机器上的完整验收。

系统预置如确有需要，应先准备可审阅的具体变更和回收方案，再按实际范围取得授权。
