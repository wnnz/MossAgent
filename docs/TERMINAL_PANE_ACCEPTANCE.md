# 终端面板操作与输出验收

日期：2026-10-09。接续 `TERMINAL_LIFECYCLE_ACCEPTANCE.md`，本轮补齐 App 的
操作所有权、可取消入口、输出队列预算和共享面板，不声称已实现终端模拟器。

## 设计自检

1. App 拥有可见任务的操作协调和输出投影；Windows 后端的 ConPTY、Job、
   启动与关闭职责不迁入 UI，Agent 和平台中立契约没有新增 UI 依赖。
2. 所有启动、写入和停止仍经 `IToolExecutor`，使用捕获的任务执行上下文。
   “取消请求”只取消当前管线调用的令牌，不直接调用平台管理器或擅自修改审批。
3. 取消、超时和上下文切换均不保证撤回已经执行的命令。
   非合作执行器未返回前保留操作 owner；同任务不能重复启动，其他任务可独立操作。
   本轮只限定 transient UI 输出，不改变已有工具输出预算、审计或持久化结构。
4. 面板、输出缓冲、操作 owner 和 ViewModel 各自拆分；手写 C# 一文件一类型，
   新生产文件少于 200 行。布局与尺寸由共享控件及外观 variants 拥有。
5. 针对队列、Unicode、拒绝、取消、非合作返回、任务切换、释放和重入补测试；
   同时验证 C#/AXAML 不能替换共享面板，以及明暗主题、窄面板和实际原生窗口。

## 操作所有权

- 原面板只有一个可重置的 busy 标志。切换任务会清除它，但旧命令仍可能执行，
  且命令自身的默认串行状态又会阻止其他任务。现在每个任务保留独立操作 owner，
  共享命令允许并发，实际准入由当前任务的 owner 决定。
- 启动、发送和停止具有独立本地化进度文字。忙碌时显示“取消请求”，
  取消后变为禁用的“取消中…”；只有执行管线返回才解除本任务 busy。
- 切换任务撤销旧上下文的令牌并隔离结果，其他任务仍可操作；返回同一任务时，
  非合作旧操作尚未结束会继续阻止重复调用。旧结果不能清除新草稿或修改新任务状态。
- 拒绝、授权路径、超时和取消使用 allowlist 本地化提示，不能显示执行器的原始异常、
  error code 或潜在密钥。拒绝保留输入，成功只清除尚未被修改的同一份输入。
- 取消后非合作执行器返回成功时，明确提示“操作可能已生效”，不假装命令已撤回。
  释放会取消全部 owner 并解除事件订阅；已开始的非合作调用仍由各自异步链持有。

## 有界输出投影

- 原实现为每次输出分配一个 dispatcher 回调。显示字符串虽然有限，待处理队列
  却可能无限积累。现在每个面板仅保留一个待处理输出回调，后台事件合并到有界缓冲。
- 等待队列和最终显示分别最多 200,000 UTF-16 code units；超过时保留最新内容，
  用独立提示说明较早输出已省略。一次超大输入也不先复制完整内容到等待缓冲。
- 50,000 次连续输出只调度一个回调。发布期间新输出至多排队下一批，
  切换任务复用已排队回调但清除旧数据；释放后排队回调不再发布。
- 不在截断点保留 emoji 的孤立低代理项；未截断的代理对可以跨 UI 批次拼接。
  输出通知引发重入任务切换时，也不会把旧任务的截断提示带到新任务。
- 不剥离 VT 转义序列，不用正则过滤假装实现终端模拟器。投影是有界原始文本，
  不是字符网格、颜色、光标或完整滚屏。大文本 shaping 成本仍需终端渲染方案处理。

## 共享组件与外观

- `AppTerminalPane` 集中拥有工具条、进度、输出提示、空状态和输入行；
  `InspectorPane` 仅绑定任务 ViewModel 和可见性，不再复制这些视觉元素。
- 所有视觉元素使用现有 wrappers，尺寸与间距在共享 variants 中定义。
  Roslyn 和 App 的 AXAML 构建 gate 禁止替换面板 Content、DataTemplates 和几何，
  同时允许正常 DataContext、可见性和父布局绑定。
- 原生截图发现 danger 按钮在禁用时仍像可点击的红色动作；共享 danger 禁用
  variant 改用当前主题的中性按钮、次要文字和边框资源，而非终端专用色值。
- 明暗主题及 280×320、470×620 的面板测试检查动作中心线、边界、截断提示、
  取消前后可见/禁用状态和 danger 的主题资源。完整检查器导航还检查面板正确绑定
  `MainWindowViewModel.Terminal`，未向模型发送任务或消耗草稿。
  该集成测试需先保存并选择合成只读任务；没有选择任务时隐藏面板是正确空状态，
  不能把最初未建任务的测试断言失败当成产品可见性缺陷。

## 原生截图

使用仓库外的独立 Avalonia 宿主，加载真实 App 资源和编译后的 `AppTerminalPane`。
合成执行器刻意不合作取消，让“取消中”保持至返回；合成输出源产生普通文本与突发输出。
它不创建 ConPTY、不执行 shell、不调用模型，也不读取用户工作区或现有配置。

- 普通权限 `QA_ELEVATED False`、100% 缩放；Light、Dark 各捕获启动等待、
  取消等待、操作返回和输出截断四种状态，窗口原始尺寸均为 486×659。
- 后端信号驱动这些合成状态，不通过桌面自动化点击终端执行按钮或输入命令。
  窗口和截图用明确 PID 绑定；等待 ready 记录后才捕获。
- 复用仓库外的 Python PrintWindow 脚本，拒绝黑图、均匀帧与尺寸变化。
  修复前后对比使用旧程序集和相同合成数据，原始截图不缩放。
- 明暗主题最终宿主 exit 0、stderr 空，验收结束后均已关闭。
  最初 QA 宿主的资源转移顺序及未带 `.exe` 的截图目标匹配错误已在宿主修正，
  这些失败不计为应用修复，也不把旧空帧作为有效对照。

本机证据：`C:\Users\wzheng\AppData\Local\Temp\MossAgent-terminal-pane-20261009`。
有效对照为 `before-Light-ready/pending.png` 与 `after-Light-final/cancelling.png`；
`terminal-pane-before-after.png` 仅拼接两张原图。其他有效状态在 `after-Light-final`
和 `after-Dark-final`，宿主、截图、比较脚本均不进入仓库。

## 可重复验证

```powershell
dotnet test tests/MossAgent.App.Tests/MossAgent.App.Tests.csproj --no-restore --filter "FullyQualifiedName~TerminalOutputBufferTests|FullyQualifiedName~TerminalPane|FullyQualifiedName~InspectorPaneLifetimeTests|FullyQualifiedName~InspectorComponentTests"
dotnet build MossAgent.slnx --no-restore
dotnet test MossAgent.slnx --no-restore --no-build
```

本轮新增 29 项，相关专项 45/45 通过，完整检查器集成专项 10/10 通过。
最终默认并发全量回归 3442/3442 通过、0 跳过；App 2594 项、Windows 94 项。
解决方案构建 0 警告、0 错误；相关 C# 空白格式、单类型、文件与方法规模
以及 `git diff --check` 均通过。未使用 `-m:1` 或关闭并行来绕过失败。
最终结果以仓库外 `verified-focused`、`clean-full`、`verified-build.log`
及格式/规模日志为准；
完整回归的失败及修正前结果单独保留，最终验证使用新目录，不混计结果。
一次默认并发回归在既有 `ApprovedParentExit_ReclaimsBackgroundChildBeforeConversationCompletes`
测试读取 `child.pid` 时遇到文件占用；单项复测通过，未修改或跳过这项旧测试。
这条不稳定性记录保留在 `verified-full` 和 `existing-command-rerun`，
不能把随后成功复测说成已修复该测试的文件发布竞态。

## 剩余范围

- 真实终端模拟器、VT 颜色/光标、字符网格、交互键盘、粘贴及按任务保留滚屏。
- 本记录当时未覆盖自然退出后的状态事件与 UI 自动同步；后续只读状态源、
  自动资源清理和显式重启验收见 `TERMINAL_STATE_ACCEPTANCE.md`。
- 整个 Codex Desktop 同状态、同 DPI 的原始终端截图对照，以及完整工作区
  中的鼠标/键盘交互。独立组件宿主不能替代整个产品的一比一验收。
- 持续高输出下的帧耗时、长期内存和字符 shaping 压力；队列有界不等于渲染免费。

本轮不更改 JIT、win-x64 单文件发布约定，不提交、推送或发布。
