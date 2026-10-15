# 交互式终端生命周期验收

日期：2026-10-09。范围：Windows ConPTY 后端及现有终端工具执行管线。
本记录不代表右侧终端面板或整个产品已与 Codex Desktop 一比一一致。

## 实现前自检

1. 生命周期和 UTF-8 管道解码属于 `MossAgent.Platform.Windows`；
   任务协调继续使用平台中立的 `ITerminalSessionManager`。
   App 不启动进程，Agent 不引用 Windows、Avalonia 或具体基础设施。
2. 复用 `terminal.start/write/stop`、现有工具执行器和授权路径解析器；
   不新增绕过审批的 UI 执行入口，不改变 schema、风险分类或数据库结构。
3. 创建和写入使用调用取消令牌；停止及释放必须等待已取得资源清理。
   非合作 SDK 创建或写入未返回时，不把取消、超时或窗口关闭报告为清理完成。
   现有工具输出限制、审计和 UI 展示预算没有因本轮修改而扩大。
4. 将每任务所有权拆为独立 `PtyTerminalOwner`；生产文件均少于 150 行，
   每个手写 C# 文件仅一个类型，方法均低于 80 行。
5. 确定性测试覆盖迟到创建、取消、重复调用、关闭失败、分块 Unicode、
   输出隔离和审批拒绝；原生测试用实际文件和绑定的进程句柄确认执行与回收。

## 修复的根因

- 原管理器在原生创建返回后才登记终端。停止或释放可以提前结束，随后迟到的
  创建继续交接；同任务并发创建也可能启动两份终端。现在先登记任务 owner，
  在后台共用一次创建，停止和释放保留 owner 至创建及清理返回。
- 已释放管理器拒绝新工作；同任务不能静默复用另一个工作目录。
  首创建者取消会回收迟到连接，重复调用者的取消不会停止首创建者的终端。
- 原读取端对每块字节独立调用 UTF-8 解码。跨管道块的中文、emoji 和组合字符
  可能损坏。现在使用持续持有解码状态的 `StreamReader`，逐块交付字符。
- 原关闭先取消读取、再单独调用根进程 Kill，异常可能跳过连接释放。
  现在保持读取排空，在后台释放 ConPTY 连接，再取消并等待读取及在途写入。
  多次关闭共享一次完成任务；关闭失败仍是失败，其他任务照常尝试清理。
- 原生创建默认 `-NoLogo -NoProfile`，不隐式运行用户 PowerShell profile；
  启用 Porta.Pty 的异步 I/O。一个订阅者抛出异常不再终止其他订阅者或读取循环。
- 当前 owner 的停止或替换会拒绝旧连接随后发布的输出。已进入订阅者的回调
  无法撤回；App 仍负责检查当前任务与界面 generation。

## Porta.Pty 边界

使用现有 `Porta.Pty 2.2.2`，没有引入新平台包或自行替换 ConPTY 启动器。
核对 NuSpec 对应源码 commit：
`016b6ea14d18aa1f44537aaf78e7c145dae88c76`。

- Windows `PtyProvider` 先挂起创建进程，再归入启用 `KILL_ON_JOB_CLOSE` 的 Job，
  最后恢复线程。后台子进程继承该 Job；连接拥有 Job 的生命周期。
- `PseudoConsoleConnection.Kill()` 只终止根进程；本轮不再依赖它回收派生进程。
- 连接 Dispose 关闭 pseudoconsole、管道、原生进程/线程句柄和 Job。
  应用持续读取期间执行关闭，避免先停止排空而妨碍 ConPTY 关闭。
- 关闭 Job 不等于系统权限沙箱，也不包含提权代理、系统服务或任意外部进程。
  本轮没有证明所有应用异常终止、原生句柄继承或资源耗尽场景。

## 原生运行验收

`PtyNativeLifecycleTests` 的四个用例使用真实 PowerShell、ConPTY、工具注册器、
`ToolExecutor`、授权路径解析和 FullAccess 策略；没有调用付费模型。

- 启动、写入和手动停止均通过工具执行器。PowerShell 在独立合成目录原子写入
  PID 文件，测试绑定进程句柄；命令回显不作为执行成功证据。
- 手动停止与管理器释放分别回收已绑定的根 PowerShell 和后台子 PowerShell。
  子进程默认隐藏窗口，停止后未生成延迟标记文件。
- 根 shell 自然退出后，显式停止仍回收后台子进程；同任务可再次启动，
  执行后在文件中核对完整中文与 emoji，而非只寻找输入回显。
- 并发两个任务时，停止第一个会回收它的根进程和子进程，第二个保持存活，
  并能继续执行命令生成正确标记文件。
- 退出断言在测试最终清理前进行。最终清理仅对合成 PID 文件已绑定的进程
  使用有界等待和测试失败兜底；不扫描或终止任意用户进程。

以上是后端 API 验收，没有通过桌面自动化在终端输入或执行 shell 命令。
本轮没有新增 UI 样式，也没有将被动截图、构建或输入回显算作交互式终端像素验收。

## 拒绝与失败路径

- 只读和审批拒绝不调用创建函数；活动终端的写入和停止同样遵守策略，
  拒绝后写入流为空且连接未释放。
- 越权目录在审批前拒绝；空、相对或驱动器相对目录在创建前拒绝；
  已取消请求不调用创建函数，释放后不能重新启动。
- 停止、取消和释放等待非合作的迟到创建；同任务复用一次创建，其他任务
  不等待它的慢创建。重复停止/释放共用清理，重复调用者取消不撤销原终端。
- 工具超时会保持迟到创建的所有权，清理后才返回 `tool_timeout`，
  然后允许显式重试。不能用返回超时掩盖仍在创建的进程。
- 非合作在途写入保持关闭所有权至返回；关闭失败不返回成功，
  管理器仍拒绝新工作，其他任务资源也被尝试回收。
- 1、2、3、5 字节读取块均完整恢复 UTF-8；订阅者失败、旧连接迟到输出
  不会破坏其他订阅者或污染重启后的任务。

## 可重复验证

```powershell
dotnet test tests/MossAgent.Platform.Windows.Tests/MossAgent.Platform.Windows.Tests.csproj --no-restore --filter FullyQualifiedName~MossAgent.Platform.Windows.Tests.Pty
dotnet build MossAgent.slnx --no-restore
dotnet test MossAgent.slnx --no-restore --no-build
```

2026-10-09 终端专项 39/39 通过，其中新增 37 项，既有 2 项。
原生四项先单独通过，再纳入终端专项和 Windows 全项目回归。
App 检查器生命周期与终端工具消息专项 14/14 通过。
默认并发全量回归 3413/3413 通过、0 跳过；Windows 94 项、App 2565 项。
未使用 `-m:1` 或关闭测试并行来掩盖所有权竞态。
解决方案构建 0 警告、0 错误；相关 C# 空白格式检查及 Roslyn
单类型、文件和方法规模审计通过。

本机证据位于仓库外：
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-terminal-ownership-20261009`。
包括 `native/terminal-native.trx`、`focused-final/terminal-final.trx`、
`app-focused/terminal-app.trx`、`full`、`solution-build.log`、
`format-check.log` 和 `source-audit.log`。
初始红灯日志保留在 `red`；并发假连接共享导致的清理异常不作为原生 SDK 复现。

## 未完成范围

- 原始 VT 控制序列的终端渲染、字符网格、颜色、光标、粘贴和键盘交互。
- App 输出目前按事件投递 UI 队列；虽然最终字符串有 200,000 字符上限，
  高输出速率下的待处理队列仍需独立限流或合并，不能声称已解决背压。
  后续有界合并与取消入口的 App 验证见 `TERMINAL_PANE_ACCEPTANCE.md`，
  不追溯更改本记录的后端范围，也不代表已经实现完整终端渲染。
- 自然退出时的 UI 状态自动同步、终端启动期间停止入口、所有状态的布局、
  明暗主题与 Codex 同状态原始截图对照。
- 非合作创建/关闭永不返回、应用被强制终止、关闭故障注入及压力/资源耗尽。
  不能通过提前删除 owner、超时后遗弃连接或启动替代进程来宣称解决。
- 有意加载用户 profile、提权/系统服务及不同 PowerShell 版本的兼容性。

普通调试继续使用 JIT；win-x64 单文件发布约定不变。本轮未提交、推送或发布。

## 2026-10-10 自然退出测试夹具复核

- 并行全量测试曾出现自然清理断言预期 `Stopping`、实际 `Exited`。测试夹具的
  `release.Wait(Deadline)` 忽略超时返回值，会在未显式释放时自行放行清理，
  违背原测试语义；独立基线复测该类九项通过，保留全量失败和独立通过证据。
- 改为等待显式释放或捕获的测试取消 Token。启动和退出也纳入 `try/finally`，
  失败路径仍释放门闩；进入与结束等待保留原 10 秒期限，不放宽断言、不接受
  提前退出，也不禁用测试并行。此处没有修改终端生产逻辑。
- 修正后该类九项通过，并独立重复十次共 90 项通过；Platform 专项 129 项通过。
  证据在 `C:/Users/wzheng/AppData/Local/Temp/MossAgent-provider-read-boundary-20261010`，
  包括 `terminal-state-before.trx`、`terminal-state-final.trx`、十份压力检查 TRX 和
  `platform-final.trx`。这不构成新的 ConPTY 原生验收或所有计时问题已解决的证明。
- 最终一次完整并行回归七个项目共 3,900 项全部通过，Platform 129 项通过，
  结果在同目录 `full-final`。完整构建零警告、零错误；没有通过关闭并行或扩大
  生产期限取得通过结果。
