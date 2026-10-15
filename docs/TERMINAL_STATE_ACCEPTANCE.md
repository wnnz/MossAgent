# 终端会话状态同步验收

日期：2026-10-09。接续 `TERMINAL_PANE_ACCEPTANCE.md`；本轮范围是只读会话
状态、自然退出后的资源回收、重新启动准入和 App 同步，不实现 VT 模拟器。

## 实现前自检

1. 平台中立状态契约属于 Tools.Abstractions，Windows 拥有进程观察与资源清理；
   App 仅投影状态和协调既有工具调用，依赖方向不变。
2. 状态查询复用管理器的可选只读 `ITerminalStateSource`；启动、输入、停止
   仍通过 `IToolExecutor`、既有路径授权、审批、超时与审计，不增加执行旁路。
3. 自然退出只清理该会话已经拥有的连接和 Job，不启动替代进程或运行命令。
   取消和清理未返回前保留 owner；失败清理不能被报告成已退出或允许替换。
   状态仅含任务、会话身份、递增版本、枚举和退出码，不含目录、命令或异常。
   仅缓存最近 128 份已结束状态；活动和清理失败 owner 的状态不被驱逐。
4. 契约、状态注册表、输出和状态合并队列各自拆分，一文件一类型，所有新增
   文件和方法遵守规模限制；共享终端控件继续拥有视觉布局。
5. 验证自然退出、错过退出事件、读取/清理失败、清理等待、并发任务、旧会话、
   订阅者失败、缓存预算、迟到通知、重入切换、释放及真实进程树回收。

## 状态与生命周期

- 新会话拥有独立 SessionId。Revision 在管理器内递增，订阅者必须按版本拒绝
  乱序通知；快照是不可变元数据，不携带输出、命令、路径或原始异常。
- 查询不存在的任务返回空，代表未启动或已结束快照被预算驱逐，不会创建终端。
  缓存最多保留 128 份已结束快照；活动与清理失败 owner 不因历史预算被移除。
  这些状态只在当前管理器生命周期有效，不持久化，也不重放中断操作。
- 创建前登记 Starting；创建交接后为 Running。订阅 ProcessExited，并用零等待
  探测覆盖订阅前已经退出的竞态。不能把普通输出 EOF 当成 shell 已退出。
- 自然终止等待已产生的读取输出交付，再进入 Stopping，复用已拥有连接的清理。
  Job 回收派生进程，清理返回后发布 Exited 和退出码。不会自动启动替代 shell。
  自动回收是既有资源生命周期的结束，不是绕过审批执行新的停止工具。
- 创建或读取失败在清理成功后为 Failed，可显式重试。清理失败为 CleanupFailed，
  保留 owner，禁止替换；异常不会出现在状态通知中。手动停止完成为 Stopped。
  重复停止与释放共用清理，并等待该 owner 的观察链；创建失败也等待观察链返回。
- 多任务各有独立会话，旧连接、旧观察链和已删除 owner 不得更新重启后的状态。
  一个状态订阅者抛异常不会中断其他订阅者或资源清理。

## App 同步与准入

- 可选只读状态源不破坏旧适配器；不支持状态源的适配器沿用原有工具结果展示。
  原生 Windows 管理器实现该源，App 不直接调用其启动、写入或停止方法。
- Attach 读取当前快照，包括后台已启动的终端。通知只更新当前授权任务，并按
  generation 与 Revision 隔离迟到、乱序和重入切换；释放解除订阅并拒绝排队结果。
- 状态通知最多保留一份最新快照与一个待处理 dispatcher 回调。50,000 次突发
  通知只投递一次；投递失败后可在同版本或新版本通知时重新安排。
- 启动只允许未启动或已安全结束的会话，发送仅允许 Running；Starting/Running
  可请求停止，清理中的动作禁用。执行命令前再次读取状态，不能用尚未更新的
  按钮状态绕过准入。查询失败暂时禁用动作并给出本地化提示。
- 会话状态与操作结果分别展示；自然退出不会清除草稿或覆盖审批拒绝、取消提示。
  成功启动/停止后的重复提示隐藏，当前会话状态保持权威。退出不能提前释放尚未
  返回的管线 owner；按任务忙碌、取消与非合作返回约束保持不变。

## 截图发现的共享布局问题

新状态标题从“终端尚未启动”变为“暂时没有终端输出”后，原生像素显示尾字消失。
绑定值已完整更新，但共享空状态中的居中 StackPanel 仍保留原来按短标题计算的
宽度。原生日志：标题 desired=112、arranged=84；明暗 headless 测试均复现
desired=116、arranged=87，不能把构建通过当成文字正常显示。

为 owner 增加 AffectsMeasure 没能修复这个重排，尝试已撤回。最终改动在共享
`AutomationVariants.axaml`：内容面板使用可用宽度并保留 MaxWidth，文字仍按
TextAlignment 居中、图标仍独立居中，不加终端专用尺寸或强制重开面板。
两项新增布局回归由失败转为通过；原生窄窗 arranged=200、desired=112，
截图完整显示标题。与其他共享空状态的四项既有测试一起 6/6 通过。

## 原生证据

- 后端四项 ConPTY 用例经真实 ToolExecutor 运行，使用独立合成目录与绑定进程
  句柄。自然退出用例不再手动调用停止：确认退出码 23、末尾 FINAL-OUTPUT
  的真实输出、后台子进程自动结束，并可在同任务显式启动新 SessionId。
  输入回显和最终兜底清理不作为成功证据，其他任务仍保持可执行状态。
- 视觉宿主加载真实 App 资源与共享终端控件，但只发布合成状态和输出。它不执行
  shell、不调用模型、不读取用户配置，不通过桌面输入执行命令。
- 旧版 App 程序集与相同合成状态构成基线；修改前 Running/Exited 时启动、停止
  和执行按钮仍显示为可用。修改后运行状态禁用重复启动，退出显示退出码并禁用输入执行。
- 最终 accepted 宿主验证 Light/Dark 470×620 客户区及 Light 280×620 窄面板，
  原图分别为 486×659 和 296×659；普通权限、100% DPI、六种状态各一张截图。
  状态由后端合成信号驱动，Python 仅按 PID、确切标题和尺寸被动 PrintWindow，
  不注入鼠标或键盘。所有验收宿主均退出，exit 0、stderr 空。
- 旧截图辅助脚本筛掉宽度不足 400 的窗口，导致首次窄窗无匹配目标。仅在同时
  提供 PID 和确切标题时放开该宽度过滤，仍检查尺寸、唯一窗口和非黑像素。
  修复的是仓库外截图脚本，不是应用截图功能；失败日志保留，不计为有效原图。

本机证据：`C:\Users\wzheng\AppData\Local\Temp\MossAgent-terminal-state-20261009`。
有效原图位于 `before-Light-470`、`accepted-Light-470`、`accepted-Dark-470`
与 `accepted-Light-280`。中间 `final/inspected/labels` 图片和标题红灯日志保留，
不替代最终验收。比较图只拼接原图，不缩放，不把 MossAgent 前后对比说成 Codex 对照。
宿主、截图、布局审计及图像脚本均位于仓库外。

## 可重复验证

```powershell
dotnet test tests/MossAgent.Platform.Windows.Tests/MossAgent.Platform.Windows.Tests.csproj --no-restore --filter FullyQualifiedName~Pty
dotnet test tests/MossAgent.App.Tests/MossAgent.App.Tests.csproj --no-restore --filter "FullyQualifiedName~TerminalState|FullyQualifiedName~TerminalPane|FullyQualifiedName~TerminalOutputBuffer|FullyQualifiedName~InspectorComponentTests|FullyQualifiedName~InspectorPaneLifetimeTests|FullyQualifiedName~InspectorEmptyStateTests"
dotnet build MossAgent.slnx --no-restore
dotnet test MossAgent.slnx --no-restore --no-build
```

Windows 终端专项 52/52 通过，标题修复专项 6/6 通过，最终 App 相关专项 73/73
通过。本轮新增 32 项；最终默认并发完整回归 3474/3474 通过、0 跳过：App 2614、
Windows 106、Providers 283、Agent 63、Tools 188、Browser 76、Infrastructure 144。
没有使用 `-m:1` 或关闭测试并行。最终解决方案构建 0 警告、0 错误，相关 C# 空白
格式、Roslyn 单类型/文件/方法规模、AXAML/XML 与 scoped `git diff --check` 均通过。
有效结果使用独立目录 `windows-final`、`app-accepted`、`full-accepted`、
`accepted-build.log`、`source-accepted.log` 与 `format-accepted.log`；不混计早期
未修复标题布局的红灯、中间 3472 项全量结果或上一轮测试证据。
原生前后对比为 `terminal-state-comparison.png`；窄窗标题同状态对比为
`terminal-title-comparison.png`。最终确认没有遗留视觉宿主、没有暂存修改。

## 剩余范围

- VT 终端模拟、字符网格、颜色、光标、交互键盘、粘贴和按任务保留滚屏。
- 完整工作区与 Codex 同状态、同 DPI 原图对照；合成组件宿主不是一比一验收。
- 高输出持续帧耗时、字符 shaping、长期内存、非合作取消回调阻塞 UI 等压力边界。
- 事件订阅者长期阻塞、非合作读取/创建/关闭永不返回、强制结束应用、资源耗尽
  和所有 PowerShell 版本。当前只证明抛异常订阅者隔离与已覆盖的退出路径。

普通调试继续 JIT，win-x64 单文件发布约定不变；没有 AOT、JSON 迁移、数据库
结构修改、提交、推送或发布。完整产品 goal 仍保持 active。
