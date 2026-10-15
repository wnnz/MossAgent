# Git 进程所有权验收

日期：2026-10-10。范围是 Git 调用的进程管理优化，不是完整产品或界面验收。

## 问题和职责

- 原 Git 工具直接启动进程，正常命令退出后，钩子启动的后台进程仍可能存活。
- 原 Infrastructure worktree 服务在取消等待时没有回收 Git 和钩子进程。
- 原分支查询没有消费 stderr，诊断输出可能填满管道，阻塞 Git 退出。
- `GitCommandRunner` 保留工具结果及 diff 退出码规则，必须注入已有的
  `ITaskProcessLauncher`；不提供无所有权的生产回退。
- `GitProcessRunner` 属于 Infrastructure，服务后台分支查询和 managed worktree。
  它复用 `Tools.Abstractions.Processes` 的端口，不访问 Windows 类型。
- App 只负责注册，三条路径都使用 `WindowsTaskProcessLauncher`。
  Windows 的原子 Job 绑定继续由 Platform.Windows 实现，没有另造启动器。

## 边界与不变量

- 每次调用具有显式工作目录、独立参数列表和独立进程所有权。
  正常退出也回收钩子后代；不能让后台服务器借 Git 命令留在调用之外。
- 等待退出后先终止剩余 Job 进程，再观察两路输出任务，最后释放进程。
  取消、超时、读取失败及清理失败不能返回成功；预取消不启动进程。
  managed worktree 预取消也不先创建应用数据目录。
- 工具仍经过原来的 schema、路径、能力、审批、期限、输出和审计管线。
  diff 的退出码 1 仅在允许此模式且没有 stderr 时作为成功。
- Infrastructure 同时消费 stdout/stderr，各最多保留 16,384 个 UTF-16 字符；
  超额仍继续排空并标记截断。截断的分支名不作为有效结果返回。
- Infrastructure 单次调用期限为 30 秒；用户取消保留原 cancellation token，
  期限耗尽则抛出 `TimeoutException`。可注入 TimeProvider 仅供确定性期限测试。
- managed worktree 保留现有管理目录检查及脏目录拒绝行为，不强制删除用户修改。
  Git 缺失、非仓库、空分支等查询行为没有改为伪造成功。
- 进程回收不是事务回滚：取消或超时前可能已经创建分支或 worktree 文件。
  本轮不自动删除这些修改，也不自动重放中断的操作。
- 不改变数据库、凭据、模型接口、UI 样式和发布策略；仍用普通 JIT 调试，
  发布保留 Windows x64 单文件，不重新启用 AOT 或 JSON 源生成。

## 有效基线

证据位于仓库外 `%TEMP%/MossAgent-git-ownership-20261010`。
原实现快照为 `GitCommandRunner.before.cs`、`GitWorktreeService.before.cs`
和 `GitRepositoryInfoService.before.cs`。

`before-clean/git-ownership-before-clean.trx` 的五项测试有三项失败、两项通过。
三个失败都明确报告 `A Git hook descendant survived its owning command.`：

- 工具路径正常退出。
- Infrastructure worktree 正常退出。
- Infrastructure worktree 取消。

更早的两次基线运行存在夹具的只读文件清理和 PID 文件目录错误，不能用作
生产缺陷证据。修正后的钩子先完整写入 PID 临时文件，再 rename 发布；
同时设置 PowerShell 位置和 .NET CurrentDirectory，避免信号写到 worktree。

## 定向验证

最终定向结果在 `final-focused` 下，61/61 通过，无跳过：

| 项目 | 通过 | 重点 |
| --- | ---: | --- |
| Tools.Tests | 35 | 参数、双流、输出预算、审批/schema/路径、取消、清理失败、diff 回归 |
| Infrastructure.Tests | 17 | 分支语义、截断、stderr、期限、预取消、managed worktree |
| Platform.Windows.Tests | 8 | 真实 Git/钩子子进程的退出、取消、超时、并发隔离、ReadOnly 拒绝 |
| App.Tests | 1 | 真实 AppComposition、Windows 启动器、工具管线和 SQLite 审计贯通 |

原生病例使用临时真实仓库、git.exe、post-checkout 钩子和普通权限 PowerShell。
钩子启动真实后台子进程；持有真实进程句柄，在夹具 Dispose 手工清理之前
断言后代已经退出。并发病例取消一个调用后，另一个调用的子进程仍正常存活，
随后仅在它自己的操作完成时回收。期限病例先绑定真实子进程，再推进测试时钟，
不靠增加等待时间掩盖生产问题。

App 病例以独立数据目录初始化 SQLite，真实调用 `git.status`、`git.diff`、
`git.log` 和 `git.create_worktree`，检查四条完整成功审计；后台分支查询及
managed worktree 创建/移除也贯通。它不访问用户数据库，不请求付费模型，
也不代表桌面窗口交互或截图验收。

portable 测试共享的 managed Process 夹具仅服务原有真实 Git 数据测试；
recording/stub 夹具仅验证协议、读取和清理行为。它们不是生产 fallback，
不能用来证明 Windows 的原子 Job 绑定；该证据来自上述原生病例。

## 完整回归

最终构建执行 `dotnet build MossAgent.slnx --no-restore -v minimal`，零警告、
零错误。随后八个测试项目逐个执行
`dotnet test <project.csproj> --no-build --no-restore`，项目内部保留默认并行。
根据 `final-regression` 下八份独立 TRX 汇总，4,402/4,402 通过，无跳过：

| 项目 | 通过 |
| --- | ---: |
| Agent.Tests | 63 |
| App.Tests | 3,271 |
| App.Visual.Tests | 8 |
| Tools.Tests | 316 |
| Platform.Windows.Tests | 137 |
| Browser.Tests | 76 |
| Infrastructure.Tests | 165 |
| Providers.Tests | 366 |

App.Tests 此次耗时约 4 分 34 秒，期间没有为了缩短等待而禁用项目内部并行，
也没有重启或跳过测试。完整回归包含新增 Git 病例；不把定向的 61 项重复计入总数。

## 结构检查

- 解决方案构建：零警告、零错误。
- 对本轮 19 个 C# 文件执行 scoped whitespace 检查，无格式错误。
- Roslyn 检查：每文件一个类型、文件最多 500 行、方法最多 80 行，全部通过。
- 本轮没有提交、推送或发布；保留已有未提交工作。

## 未覆盖范围

- App 被外部强制终止、提权代理或系统服务创建的进程，以及资源耗尽压力。
- Git 钩子的任意系统副作用或超时后 worktree 的自动修复。Job 不是安全沙箱。
- 所有 Git 版本、网络认证助手、PTY、浏览器及 MCP 的全场景生命周期。
  它们的历史验收范围仍各自独立。
- 全页面像素一致性、各 DPI、真实远端提供商及子代理完整交互。

本记录只验证这次 Git 进程管理优化，不表示整个 MossAgent 目标已完成。
