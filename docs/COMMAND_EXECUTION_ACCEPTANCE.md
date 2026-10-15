# 命令执行链路验收

命令行为仍由 `MossAgent.Tools.BuiltIn/Processes/ShellRunTool` 拥有，
从 App 的既有主操作进入标准工具管线。进程契约保持平台中立，Windows 原生实现
隔离在 `MossAgent.Platform.Windows`；未引入 UI 执行进程或新的持久化结构。
运行要求包含 PowerShell 7（`pwsh`）。

## 自动验收

`WorkspaceCommandFlowTests` 使用真实 App 服务装配、设置 ViewModel、SQLite、
OpenAI Responses 适配器、AgentRunner、审批队列和 PowerShell 进程。
只有模型由本机随机回环端口的确定性服务替代，不调用付费模型或生产文件。

- 通过提供商和模型设置的保存命令写入配置，再通过工作区主操作创建任务。
- 审批到达时标记文件不存在，命令正文默认隐藏；展开后能查看原始命令。
- 批准后，真实命令在授权目录写入标记文件，返回原始 `shell-ok` 输出；
  模型收到工具结果，完成第二轮回复。
- 拒绝或在审批阶段停止，不启动命令，不产生标记文件；停止后没有下一轮模型请求。
- 只读策略或越权工作目录在审批前拒绝，授权目录和外部目录均不产生标记文件。
- 在真实父、子 PowerShell 进程都运行时停止任务，核对两个已绑定进程均退出，
  不产生后续标记文件；任务、工具卡片和审计保存为已取消，不再请求模型。
- 批准后等待后台子进程真实启动，再让父命令正常退出；工作区结束时子进程已退出，
  工具、会话和审计仍保存正常完成结果，模型收到原始 `shell-ok`。
- 非零退出保存 `process_failed`，错误输出作为可展开详情保存，退出码保留在简短摘要中。
- 大量无换行输出保持在共享 64 KiB 输出预算内，模型收到的是受限结果，而非完整输出。
- 重新装配服务读取 SQLite，核对任务状态、工具结果、审批、完成时间和输出字符计数；
  当前可见工具卡片与恢复出的卡片内容一致，没有遗留运行状态或审批请求。

Windows 测试项目中的 `ShellRunToolTests` 直接验证真实 PowerShell 的 stdout、stderr、中文输出、
无换行输出、0/16/512 字符预算及超时后的进程退出。
同时验证 `using namespace` 和 `param` 等必须位于脚本开头的语法；
输出编码初始化在原始脚本之外执行，不让初始化语句破坏合法命令的解析。

```powershell
dotnet test tests/MossAgent.App.Tests/MossAgent.App.Tests.csproj --no-restore --disable-build-servers -m:1 --filter FullyQualifiedName~WorkspaceCommandFlowTests
dotnet test tests/MossAgent.Platform.Windows.Tests/MossAgent.Platform.Windows.Tests.csproj --no-restore --disable-build-servers -m:1 --filter "FullyQualifiedName~ShellRunToolTests|FullyQualifiedName~ShellProcessOwnershipTests|FullyQualifiedName~WindowsTaskProcessLauncherTests"
dotnet test MossAgent.slnx --no-restore --disable-build-servers -m:1
dotnet build MossAgent.slnx --no-restore --disable-build-servers -m:1
```

2026-10-05 最终源码完整测试 881 项通过（App 590 项、Windows 25 项、Tools 112 项），解决方案构建
0 警告、0 错误。相关 C# 空白格式检查和 `git diff --check` 通过；
新增手写 C# 文件均只有一个类型声明且低于 500 行。真实 PowerShell 测试从
平台中立的 Tools 测试项目迁移到 Windows 测试项目，没有减少原来的测试用例。

## 本轮发现并修复

- 按行事件读取会强行补换行，且在到达输出缓冲区前可能积累巨大的单行。
  改为每次最多 4096 字符的异步读取，两个输出流分别持续排空。
- Windows 下直接 Console 中文输出可能采用非 UTF-8 编码，导致替换字符乱码。
  为 PowerShell 输出和读取端明确使用 UTF-8，保留命令实际输出的换行。
- 首轮取消处理只在父进程仍存活时终止进程树，漏掉父进程退出后仍存活的后台进程。
  当前实现改为独立持有整棵 Job 进程树，回收不再依赖父进程是否仍存活。
- 命令说明和退出摘要转为本地化资源，失败详情不再混入元数据摘要。

## Windows 进程归属复核

- 使用真实 PowerShell 复现原实现的问题：正常完成的命令返回后，后台子进程仍存活。
- `ITaskProcessLauncher` 和 `ITaskProcess` 放在 `Tools.Abstractions.Processes`，
  不暴露 Windows 类型；App 仅注册 Windows 实现，工具仍在审批后启动进程。
- 每个命令单独创建启用 `KILL_ON_JOB_CLOSE` 的 Job，通过进程启动属性原子绑定 Job。
  第一条线程先保持挂起，持有进程、管道和 Job 成功后才恢复执行；启动失败不降级为
  无归属进程。Job 不允许 breakaway，后台派生进程继承同一 Job。
- 启动时显式限制继承句柄为该命令的 stdin、stdout、stderr，避免其他并发任务的
  管道或 Job 句柄被继承。父命令结束、取消、超时或释放后，终止并等待 Job 进程清空，
  再排空输出读取和释放资源。
- 真实测试覆盖直接后台子进程、多级派生且中间进程先退出、取消多级派生进程，
  并发 Job 互不影响、无关可继承管道不泄露，以及原始参数和 UTF-8 输出。
- 参数往返覆盖空字符串、空格、中文、引号、末尾反斜杠和特殊字符；非法工作目录、
  参数或工作目录中的空字符、已取消的启动及缺失程序均拒绝，不启动替代进程。
- 测试目录使用有界清理重试处理 Windows 退出后的短暂目录句柄释放延迟；
  进程退出断言仍在清理前检查，不用目录重试替代进程生命周期断言。
- `shell.run` 是有界命令调用；它结束后不保留后台服务器。工具说明明确这一行为。
  本轮没有验证提权代理或系统服务创建的进程；Job 不是任意命令的系统权限沙箱。

## 桌面历史查看

可将 `MOSSAGENT_COMMAND_QA_ROOT` 临时设为一个新的独立目录，再仅运行上述
工作区成功和失败测试。默认不保留验收数据；显式指定时保留 `approved` 和
`failed` 子目录及各自的 `data`，方便使用 `MOSSAGENT_DATA_DIR` 打开历史查看。
目录必须是新的，不能覆盖已有验收数据。数据仅含合成提供商配置。

本机模型服务在测试结束后已经停止，桌面查看只验证历史恢复、卡片展开和显示，
不能发送新任务或把查看历史说成通过鼠标、键盘执行了命令。
截图使用仓库外的被动 Python 脚本，保持原始尺寸，不拉伸或提交图片。

2026-10-04 分别打开成功和失败验收数据的真实桌面应用：

- 成功历史恢复退出码 0 和“完成”；点击摘要后，展开正文显示 `shell-ok`。
- 失败历史恢复退出码 7 和“失败”；点击摘要后，展开正文显示 `stderr-marker`，
  不再把错误输出塞进摘要文字。
- 捕获并检查 1920×1032 最大化窗口及 1280×820 普通窗口，未出现黑屏。
  普通窗口右上角存在其他窗口局部遮挡，不据此证明全部窗口控件像素。
- 未从桌面发送或重新运行命令；只查看和展开已保存历史，验收应用随后关闭。

## 2026-10-10 测试夹具就绪发布

并行回归发现 `WorkspaceCommandFixture` 的 PID 就绪文件存在读写竞态：
`File.Exists` 已成立，但 PowerShell 写句柄尚未关闭，测试读取会遇到文件占用。
父、子进程现在通过同一个夹具 helper 先写 `.tmp`，关闭写句柄后再在同目录
重命名发布最终 PID 文件。未增加读取重试、放宽等待断言或吞掉异常，生产命令
执行和 Job 所有权没有改变。

九项真实命令工作流独立重复五轮，45/45 通过；TRX 在
`%TEMP%/MossAgent-header-fidelity-20261010-0800/pid-stress`。
这修复的是测试就绪信号，并不代替后面的进程生命周期验收。

## 仍需验收

- Git、PTY 终端和浏览器的独立进程生命周期仍需复核。
  stdio MCP 已接入独立连接 Job，其验收范围见 `MCP_EXECUTION_ACCEPTANCE.md`。
  后续 Git 工具、后台分支查询和 worktree 已接入共享进程启动端口，真实 Git
  钩子的正常退出、取消、超时和并发回收验证见 `GIT_PROCESS_OWNERSHIP_ACCEPTANCE.md`；
  不扩张本记录原有的 shell 验收范围，也不代表 worktree 操作具有事务回滚。
  后续 PTY 所有权、实际命令执行、自然退出及后台子进程回收验证见
  `TERMINAL_LIFECYCLE_ACCEPTANCE.md`；不改变本记录的历史验收范围，
  也不代表终端模拟器或所有应用异常退出场景已完成。
- App 异常终止时的外部宿主验收、提权或系统服务创建的进程、并发长任务压力、
  资源耗尽、外部修改进程环境，以及不同 PowerShell 和本机命令编码。
- 完整浏览器、MCP 和子代理桌面交互验收及所有页面的像素一致性。

本记录不代表整个产品或所有命令进程生命周期要求已经完成。
