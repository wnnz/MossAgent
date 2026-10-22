# 终端、工具与审计

> 历史归档：保留原报告当时的结论、实验、失败与限制。整理于 2026-10-21。
> 当前能力与未完成方案统一维护在[功能清单](../FEATURE_STATUS_AND_IMPLEMENTATION_PLAN.md)；
> 原文中的计划、主机状态、测试数字及“正在进行”等措辞均为历史检查点。
> 代码块和仓库相对产物路径保留原意；本机忽略的 `artifacts/` 不随克隆分发。

[文档入口](../README.md)

## 原报告索引

- [审批手势身份验收](#approval-gesture-identity-acceptance) — `APPROVAL_GESTURE_IDENTITY_ACCEPTANCE.md`
- [Asynchronous Windows process pipes and deterministic proxy evidence](#async-process-pipe-acceptance) — `ASYNC_PROCESS_PIPE_ACCEPTANCE.md`
- [Audit inspector acceptance](#audit-inspector-acceptance) — `AUDIT_INSPECTOR_ACCEPTANCE.md`
- [Interrupted tool audit recovery](#audit-interruption-recovery-acceptance) — `AUDIT_INTERRUPTION_RECOVERY_ACCEPTANCE.md`
- [命令执行链路验收](#command-execution-acceptance) — `COMMAND_EXECUTION_ACCEPTANCE.md`
- [全局审批呈现验收](#global-approval-presentation-acceptance) — `GLOBAL_APPROVAL_PRESENTATION_ACCEPTANCE.md`
- [ConPTY test PID publication diagnostic](#pty-pid-publication-diagnostic) — `PTY_PID_PUBLICATION_DIAGNOSTIC.md`
- [Terminal payload and subagent UI observation boundaries](#runtime-observation-boundaries-acceptance) — `RUNTIME_OBSERVATION_BOUNDARIES_ACCEPTANCE.md`
- [退出失败恢复](#shutdown-recovery-acceptance) — `SHUTDOWN_RECOVERY_ACCEPTANCE.md`
- [Terminal selection and clipboard integration](#terminal-clipboard-acceptance) — `TERMINAL_CLIPBOARD_ACCEPTANCE.md`
- [Terminal input-method integration](#terminal-ime-acceptance) — `TERMINAL_IME_ACCEPTANCE.md`
- [Terminal size and input integration](#terminal-interaction-acceptance) — `TERMINAL_INTERACTION_ACCEPTANCE.md`
- [交互式终端生命周期验收](#terminal-lifecycle-acceptance) — `TERMINAL_LIFECYCLE_ACCEPTANCE.md`
- [终端面板操作与输出验收](#terminal-pane-acceptance) — `TERMINAL_PANE_ACCEPTANCE.md`
- [终端会话状态同步验收](#terminal-state-acceptance) — `TERMINAL_STATE_ACCEPTANCE.md`
- [Terminal VT rendering acceptance](#terminal-vt-acceptance) — `TERMINAL_VT_ACCEPTANCE.md`
- [工具审批有限布局与决策区验收](#tool-approval-layout-acceptance) — `TOOL_APPROVAL_LAYOUT_ACCEPTANCE.md`
- [工具输出限流精简](#tool-output-optimization-acceptance) — `TOOL_OUTPUT_OPTIMIZATION_ACCEPTANCE.md`
- [普通工具实时执行状态验收](#tool-progress-acceptance) — `TOOL_PROGRESS_ACCEPTANCE.md`
- [Tool input-schema validation](#tool-schema-acceptance) — `TOOL_SCHEMA_ACCEPTANCE.md`
- [工具 schema 注册表隔离验收](#tool-schema-isolation-acceptance) — `TOOL_SCHEMA_ISOLATION_ACCEPTANCE.md`
- [Explicit task tool scopes](#tool-scope-acceptance) — `TOOL_SCOPE_ACCEPTANCE.md`
- [窗口退出收尾验收](#window-shutdown-acceptance) — `WINDOW_SHUTDOWN_ACCEPTANCE.md`

---

<a id="approval-gesture-identity-acceptance"></a>

## APPROVAL_GESTURE_IDENTITY_ACCEPTANCE.md

<!-- source: APPROVAL_GESTURE_IDENTITY_ACCEPTANCE.md -->
### 审批手势身份验收

#### 设计自检

1. 行为属于 App 输入层：共享 `AppApprovalButton` 继承现有 `AppButton`，只用于
   两个审批决策入口。不修改应用层、工具、基础设施或模块依赖方向。
2. 复用 `ApprovalViewModel` 的请求 ID、队列、取消和命令；不增加端口、服务定位器、
   全局状态、外部订阅或 UI 文件/进程访问。
3. 不更改审批政策、授权目录、风险、超时、取消、输出限制及审计管线。原生截图
   只观察合成待审批请求；不自动点击原生批准、拒绝或隐私控件。
4. 每个新增 C# 文件只有一个类型，均低于 250 行，方法低于 80 行。继续共用按钮
   样式、尺寸、可访问性和 Avalonia 原生按下/释放行为；Debug/JIT 与发布配置不变。
5. 明暗主题测试覆盖旧请求取消后的鼠标/空格释放、批准/拒绝、Enter 连发和交叠键。
   真实主窗口的 Headless 鼠标输入再贯通文件执行、持久化与审计，验证成功及拒绝路径。

#### 根因与修复

原有命令会拒绝旧请求 ID，但按钮的绑定参数在松开前可能已更新：按住请求 A 的按钮，
A 取消后队列切到 B，松开同一个按钮会执行带 B 身份的命令。未修复基线的四个
渲染用例全部复现误决策；它们不是对现有 VM 旧 ID 校验的重复测试。

共享审批按钮在鼠标或空格/Enter 手势开始时记录命令和请求身份。点击发生时，若绑定
已改变、参数不是 Guid 或控件已脱离/隐藏/禁用，则不执行。按住键重复和空格/Enter
交叠保留原身份，直到相关输入全部释放；原生鼠标释放过程中丢失捕获不会提前清除
身份。失焦、脱离和正常输入结束清理控件拥有的状态，不增加外部事件订阅。

`ToolApprovalView` 仅将原来的两个决策按钮替换为该共享封装，命令、参数、布局和
样式不变。原有内容隐私展开入口保持普通按钮；VM 和执行管线没有修改。

#### 定向验证

最终 138 项相关用例通过，其中 18 项新增：

- 8 项明暗主题 × 鼠标/空格 × 批准/拒绝，旧手势不能决定下一项，新手势仍可决定。
- 6 项 Enter 连发和交叠键；没有把第一项决策的重复输入转交给第二项。
- 4 项真实 `MainWindow`、生产服务组合、保存的提供商/模型设置和本地 Responses
  服务。Headless 原始鼠标先验证按钮命中，再使真实文件由 `alpha\n` 变为 `beta\n`，
  或拒绝并保留原文；重开数据库检查历史、审计和快照，保留下一条未发送草稿。

文件在决策前仍为原文，批准/拒绝后各有三次本地模型请求；审批消失且浏览器呈现
屏蔽解除。它们是虚拟输入驱动真实执行管线，不是原生 Windows 鼠标审批证据。

初次贯通测试有 1/4 命中断言失败，诊断重跑有 3/4 失败；Headless 渲染命中缓存尚未
刷新。与现有真实鼠标测试一致，在布局后刷新渲染时钟，再检查命中和输入，最终
18/18 和 138/138 通过。没有放宽命中断言、添加延时重试或修改生产布局来掩盖失败。

#### Python 原始截图

外部脚本、宿主和证据保存在
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-approval-gesture-20261015`，不进入仓库。
宿主加载真实生产 App 资源、`MainWindow` 和服务组合，独立数据库中只合成两个请求，
没有执行文件工具、命令、浏览器、提供商或原生审批。

- 修复前 App SHA256 为
  `64E32C47659F391815866F2775C46EAC16F4F609E8A6F81F2762782677911D5A`。
- 修复后 App SHA256 为
  `52D6AD2026606279DAADF88AF7404CE36449AD43A78C817082E9FE1F9AB32D75`。
- 四张最终 PrintWindow 原始帧为浅色 980×700、深色 526×601，各有修复前后对照。
  捕获核对 PID、可执行路径、生产 App 哈希、普通权限、96 DPI、尺寸和非黑帧。
- 浅色卡片为 `(230,295,520,385)`，深色为 `(24,196,478,385)` DIP；按钮完整可见。
  两组原尺寸全帧比较各有 0 个差异像素，没有缩放、去除文字或遮盖状态区域。
- `comparison-light.png`、`comparison-dark.png` 仅增加说明区并原尺寸并排放置帧。
  四个最终宿主全部取消两项请求、解除呈现屏蔽并正常退出 0，没有完成任何决策。

第一轮浅色宿主也取消并关闭，但原启动脚本未保留进程句柄，退出码记录为 null；
它不计入四个最终宿主。修正外部观察脚本后使用新的独立目录重跑，旧记录保留。

#### 完整验证

完整解决方案八个程序集共 6,193 项，6,192 通过、1 失败，没有跳过或中止：App
4,547/4,548；像素 102、Agent 119、Browser 76、Infrastructure 199、Windows 142、
Providers 516、Tools 491 全部通过。18 项新增回归全部包含在该总数中，定向 138
不是额外计数。App 完整程序集运行 11m58s，期间没有因低 CPU 或静默输出而重启。

唯一失败为本轮未修改的 `SidebarFilterAllocationTests`：`kind="pinned"`、
`noMatches=true` 的 10,000 次暖过滤分配量期望 0、实测 5,016 字节。完整运行结束后，
同一未重建程序集的该六例测试类独立复跑三次，18/18 通过；这些复跑不覆盖或替换
完整运行的失败，不据此断言根因已经修复，也没有放宽断言或禁用测试。

解决方案构建 0 警告、0 错误；修改作用域的空白格式、单类型/文件规模、AXAML 绑定
和差异空白检查通过。独立脚本验证八个 TRX 的计数、唯一身份和每例结果，保留
`full_run_all_green=false`。生产 App、两个 UI 测试依赖及原生候选的 App 哈希一致；
全部测试程序集在完整运行及独立复跑间没有重建或改变身份。原始失败、诊断记录、
完整退出码和截图宿主的退出校验均保留在外部证据目录。

#### 验收边界

这次修复审批的输入身份竞态，不重构审批卡外观。截图证明该修复未改变已有绘制，
并不证明卡片或整窗与 Codex 像素一致。没有取得同状态的 Codex 审批卡参考；原生人工
决策、高 DPI、完整窗口/设置/侧栏/检查器视觉矩阵仍为独立工作。

不修改真实用户数据、安全/隐私设置、AOT、JSON 实现或 Windows x64 单文件发布；
不暂存、提交、推送或发布，完整产品目标继续保持 active。
<!-- /source: APPROVAL_GESTURE_IDENTITY_ACCEPTANCE.md -->


---

<a id="async-process-pipe-acceptance"></a>

## ASYNC_PROCESS_PIPE_ACCEPTANCE.md

<!-- source: ASYNC_PROCESS_PIPE_ACCEPTANCE.md -->
### Asynchronous Windows process pipes and deterministic proxy evidence

#### Scope and design checks

Platform.Windows owns the operating-system transport behind the unchanged
`ITaskProcessLauncher`. Parent stdin/stdout/stderr now use truly asynchronous
named-pipe server endpoints, rather than asynchronous emulation over synchronous
`CreatePipe` handles. Child endpoints stay synchronous and inheritable for
console/stdio compatibility; parent endpoints are not inheritable. Ownership
transfers the actual streams to readers/writers without reopening handles.

Each byte-mode pipe has an unpredictable GUID name, first-instance protection,
one instance, CurrentUserOnly access, 4096-byte buffers and a bounded local-client
connect. The launcher still uses its explicit inherited-handle allowlist and
task-owned kill-on-close Job. Shell, Git and stdio MCP keep their existing ports,
authorization, approval, timeouts, cancellation, bounded output and audit pipeline.
No ThreadPool tuning, global cache, new application dependency or database change
is introduced. Obsolete CreatePipe/security-attribute interop is removed.

The unrelated SOCKS5 fixture race is fixed only in shared test infrastructure:
publish the rejection decision before making its response observable. Production
proxy authentication, routing and security assertions are not weakened.
Eight changed/new C# files each contain one type, are at most 107 physical lines
and have no trailing whitespace. Existing test deadlines/parallelism stay intact.

#### Deterministic red/green probes

External scripts and output remain outside the repository:
`%TEMP%\MossAgent-test-stability-20261015`.

- The old-binary SOCKS5 probe suspends a rejected response write after the client
  can observe it. Both missing-method and wrong-password cases report rejection
  count zero at that point and one only after return; exit 1. The final candidate
  reports one at both checkpoints; exit 0. Five new deterministic cases also cover
  success, malformed greeting and failed response-write/exactly-once publication.
- The old-binary process probe has two idle reads and an unrelated queued marker
  inside an isolated subprocess with at most two workers. Both parent streams are
  synchronous, workers are exhausted and the marker cannot run before pipe closure;
  exit 1. Final candidate streams are asynchronous, both reads remain idle, both
  workers remain available and the marker runs before closure; exit 0. Only this
  disposable probe changes its own worker limits, not the application or suite.
- Five new Windows pipe cases check both directions, Unicode, child-only handle
  inheritance, parent-only async I/O, pending-read cancellation and retry,
  full-buffer write cancellation without child reads, and transferred ownership.

Logs preserve `baseline-*-probe.log`, `fixed-*-probe.log` and
`fixed-final-*-probe.log` separately. This proves a worker-starvation mechanism,
not that every historical timing failure had the same exclusive cause.

#### Production composition and native restoration

Fresh evidence: `%TEMP%\MossAgent-async-pipe-runtime-20261015`.
The accepted non-UI harness loads the candidate's real App composition through
reflection, without replacing any service. Formal repositories create one fresh
synthetic FullAccess project/task and one stdio echo-server profile. Existing
`WorkspaceRunExecutor`, AgentRunner, provider, tool executor, launcher, MCP manager,
audit and conversation persistence execute the scenario. No user policy or data
is edited. No terminal command is submitted through Windows UI automation.

The loopback Responses fixture discovers encoded tool names from actual requests.
One real `shell.run` emits stdout, stderr and Unicode, then one stdio MCP echo
returns its marker. Three model requests, two successful task-owned audits and
the exact persisted output strings are independently verified. No artifact is
created. This is real service/runtime execution, not a native composer-Send test.

The actual production App then opens the same data, expands persisted execution
history and both outputs, closes normally, reopens and repeats those disclosures.
Six passive Python/PrintWindow images cover Light/96 DPI at 1920x1032. Captures
bind the expected executable and PID, reject black/minimized output, and verify
ordinary permissions. No Codex UI or security/approval control is automated.

All nineteen table hashes, task/message/audit rows, script hash and the three
model requests remain unchanged throughout both native runs. Exactly one MCP
`tools/call` exists. The additional two initialization/initialized/tools-list
sequences are expected connection startup, not replay of the tool. Both App
processes exit zero with empty stderr; both owned MCP processes are gone. The
fixture exits normally after three requests. `verify.py` independently checks
the accepted evidence and candidate identities.

App SHA256:
`A5E5CE27C2D1DB93F6592B286DF59ABF407433080938064F4A26A060A014033F`.
Platform.Windows SHA256:
`A63FF27316A63A720219C2D9D340E2E2CF758750C1A8EE32D9922F067FC45E09`.

Two external harness setup failures remain separate: missing SQLite bundle
initialization, then a fixture incorrectly expecting an untruncated echo token
in a length-limited encoded name. The former has separate initialization data;
the latter retains its data/project/request logs. They are not accepted runs or
product fixes. The accepted run uses fresh data and actual MCP-prefix discovery.

#### Build and tests

Candidate artifacts: `.scratch/async-process-pipes-20261015`.
Final solution build has zero warnings/errors. Scoped formatter verification,
tracked diff whitespace and explicit new-file type/size/whitespace checks pass.
Focused proxy/new-fixture cases pass 41/41; focused Windows cases pass 21/21 and
the complete Windows suite passes 142/142. These are not added to full-run totals.

The preceding intermediate complete run, with the fixture fix but old process
pipes, finishes 6,118/6,120: all 4,498 App cases pass, but two Windows PTY timing
cases fail. Its App worker computes for 11m40s; stack samples show different
Avalonia layout/test work, not an established deadlock. The original earlier
6,111/6,115 run and diagnostic reruns remain separate historical evidence.

The final candidate's complete solution test command exits zero: 6,125/6,125
across eight assemblies, with no failures, skips or aborts. The App worker finishes
in 12m25s; its late stack sample is still executing a different UI-layout test.

| Complete assembly | Passed |
| --- | ---: |
| App | 4,498 |
| App.Visual | 84 |
| Agent | 119 |
| Browser | 76 |
| Infrastructure | 199 |
| Platform.Windows | 142 |
| Providers | 516 |
| Tools | 491 |
| Total | 6,125 |

`verify-solution.py` independently validates all eight terminal TRX reports and
individual outcomes, records their test-binary hashes, matches App/Platform DLLs
between tests and native runtime, and checks both final deterministic probes and
the native audit. The complete run supersedes the earlier non-all-green gate;
focused/isolated runs are not counted again. Its output and
`final-verification.json` remain in the external evidence directory.

#### Limits

This slice does not certify native composer Send, manual approval, live paid
providers, cross-user/remote named-pipe attack resistance, every concurrency
combination, DPI transitions or Codex pixel equivalence. Existing negative tests
and explicit current-user/handle restrictions remain, but do not replace those
native acceptance branches. Debug/JIT, JSON and Windows x64 single-file packaging
are unchanged. No default launch output, staging, commit, push, publication or
installation is changed. The complete product goal remains active.
<!-- /source: ASYNC_PROCESS_PIPE_ACCEPTANCE.md -->


---

<a id="audit-inspector-acceptance"></a>

## AUDIT_INSPECTOR_ACCEPTANCE.md

<!-- source: AUDIT_INSPECTOR_ACCEPTANCE.md -->
### Audit inspector acceptance

#### Scope (2026-10-09)

The right-side execution history now uses the shared `AppAuditExecutionRow`.
It distinguishes pending, success, cancellation, denial and failure, with
14-DIP status icons aligned to the tool name and outcome label. Local start time
and elapsed time replace the old UTC-only time. Tool names are single-line,
control-character filtered and limited to 160 UTF-16 units without splitting
surrogate pairs. Narrow rows ellipsize names/metadata and wrap actionable hints.

Raw stored error codes are not displayed. An allowlist maps known failure
categories to localized hints; unknown codes cannot contribute diagnostic text.
In particular, an actual `path_denied` with `ApprovalGranted=false` points to the
project's authorized directories rather than suggesting approval escalation.

Refresh preserves the selected execution, including a selection made while the
read is pending, and rebinds equal-valued replacement records to the new snapshot.
Unlisted record instances cannot replace selection or trigger artifact reads.
The current task's unique execution rows are capped at 200. Selected-execution
artifacts are task/execution filtered, deduplicated and displayed up to 50 with
a visible limit notice. Artifact opening reuses the existing audited snapshot
preview; it does not rerun the tool or consume the conversation draft.

#### Design and safety self-check

1. App owns semantic presentation, selection and shared component appearance.
   The existing tools audit reader contract remains in Tools.Abstractions and
   SQLite remains in Infrastructure. No dependency direction changes.
2. The implementation reuses `IToolExecutionAuditReader` and the existing file
   preview command/reader. The row accepts presentation strings only; it does
   not read files, execute commands, query SQLite or access model providers.
3. Existing task-scope cancellation and generation/version checks reject late
   reads after task or selection changes. No new execution/approval path or
   timeout is introduced. The visible collection caps do not make the underlying
   `GetArtifactsAsync` query bounded; its existing port still returns a full list.
   File snapshot authorization, digest/size checks and cancellation stay in the
   existing reader. Error hints never echo the stored diagnostic code.
4. Each changed handwritten C# file has one type. The scoped Roslyn audit and
   XML parse cover file/method limits. Shared styles own geometry and colors;
   markup and C# appearance gates prevent component content/appearance replacement.
5. Focused tests cover selection identity, refresh races, foreign/unlisted data,
   collection limits, localized failure hints, Unicode truncation, real arranged
   icon/text alignment and both markup/C# component boundaries. Existing inspector
   tests remain part of the focused run to check late-result and disposal behavior.

#### Regression evidence

Evidence root: `%TEMP%\MossAgent-audit-inspector-20261009`.
Temporary QA projects, Python capture/comparison scripts, data and screenshots are
outside the repository. Native build output is in ignored validation artifacts.
No user database, provider credentials, paid API calls or external shell execution
are involved in native acceptance.

The first red runs captured seven failures:

- Refresh replaced the current selection with the first record.
- An oversized/foreign-task history response exceeded the task-owned UI limit.
- An unlisted record could initiate an artifact read.
- Foreign-execution artifacts and an oversized response entered the visible list.
- An equal-valued fresh record did not replace the selected record instance.
- A pending refresh overwrote the user's more recent selection.
- A real pipeline path rejection showed the generic approval hint.

The final focused run passes 77/77 tests. The four new test files contribute
27 cases: selection/bounds (6), semantic presentation (13), arranged visual
alignment (4) and appearance policy (4). Whitespace verification passes on the
eight changed/new C# files; the scoped Roslyn/XML audit passes on ten files.
The largest audited C# file is 154 lines; the largest markup is 266 lines.
The longest audited method is an existing policy method at 71 lines.

Final solution validation passes 3,268/3,268 tests across seven assemblies
(App 2,476; Providers 283; Tools 177; Infrastructure 136; Browser 76; Agent 63;
Platform.Windows 57), with no skips or failures. `dotnet build MossAgent.slnx
--no-restore -v minimal` reports zero warnings and errors before the full
`dotnet test MossAgent.slnx --no-build --no-restore` run. The native QA project
also builds with zero warnings/errors against the final source. Scoped tracked
and new-file diff whitespace checks pass; no QA or test-host process remains.

`solution-build-final.log`, `full-final.log`, `format-final.log` and
`source-final.log` contain the final checks. `full-final.log` preserves all seven
assembly summaries; the shared full-run TRX filename was overwritten between
assemblies and its final file contains only App results. The focused TRX remains
separate in `focused/audit-focused-final.trx`.

#### Native interaction and raw pixels

The QA executable starts the real App with separate temporary SQLite/project/task
directories. Four results are produced by the actual standard tool executor and
SQLite audit: an authorized README preview, path rejection, invalid arguments
and unknown-tool rejection. Pending and unknown-failure rows are explicitly
synthetic layout fixtures, not proof of running or failed real tool execution.
Cancellation presentation is covered by tests, not a native cancelled-tool run.

Final normal-permission JIT runs use fresh cases:

- `verified-Light-1280-820`: Light, 1280x820, render scaling 1.
- `verified-Dark-980-640`: Dark, 980x640, render scaling 1.

In both windows, mouse scrolling reaches the successful tool row; selecting it
shows its single real README artifact. Refresh keeps the successful selection and
artifact. Clicking README opens the actual audited snapshot in the existing file
preview. Closing the preview returns to the same selected execution. The fixture
continuously verifies that the conversation draft remains unchanged. Both windows
close through the real title-bar action, finish App shutdown and report `QA_EXIT: 0`.
Final audit records remain six; no new tool is executed by opening the snapshot.

Passive Python PrintWindow captures validate dimensions and non-black output:
`window-top.png`, `window-refreshed.png` and `window-preview.png` in each final case.
The small dark panel keeps its status, wrapped path hint and artifact button
reachable by scrolling; its existing compact file-preview toolbar remains usable.

`audit-inspector-before-after.png` places unscaled Light inspector crops side by
side. The baseline uses the preceding App assembly and separate seeded data.
It shows raw diagnostic codes and UTC-only times; the final capture shows safe
hints, status icons and local metadata. The image compares MossAgent before/after,
not MossAgent against a same-state Codex reference. Timestamps differ between runs.

#### Remaining limits

This change does not establish pixel-for-pixel Codex equivalence. Same-state Codex
execution-history references, higher-DPI windows, user font preferences, native
hover/focus/disabled states and real cancelled/long-running tools remain outside
this slice's native acceptance. Terminal/browser/provider/subagent workflows and
automation scheduling are not changed or newly accepted by these screenshots.

The repository stays managed/JIT for development and Windows x64 single-file for
release. No AOT, JSON serialization, packaging or persistence schema changes are
part of this slice. Existing unrelated dirty work is preserved; nothing is staged,
committed, pushed or published.
<!-- /source: AUDIT_INSPECTOR_ACCEPTANCE.md -->


---

<a id="audit-interruption-recovery-acceptance"></a>

## AUDIT_INTERRUPTION_RECOVERY_ACCEPTANCE.md

<!-- source: AUDIT_INTERRUPTION_RECOVERY_ACCEPTANCE.md -->
### Interrupted tool audit recovery

Status: integrated through 30 hash-guarded paths; original Debug/Release solution
builds have zero warnings/errors. Original focused checks pass. Complete Debug
regression 54927 is terminal: 8,153 pass, no failures, skips or aborts, with
unchanged source and runtime identities. This is not full-product completion.

#### Reproduction and behavior

The original-assembly Queue/Agent + managed-Chrome probe holds a real navigation
request after explicit AskEveryTime approval. An external controller kills only
the exact owned host after binding its descendants. Eleven descendants exit
automatically and another owned browser instance preserves its clicked page.
The restarted host restores the task as Interrupted, retains its remaining Queue
entry and draft, and sends no automatic model/browser request. Explicit Queue
continuation sends only the saved remaining message with its selected model and
reasoning. The interrupted navigation is not replayed.

The first lifecycle-only run passes. Adding a presentation requirement then
reproduces a product bug: the old incomplete audit remains `pending` / 进行中,
with no explanation, even though its host no longer exists. The raw failure is
`artifacts/queue-browser-crash-20261019/runtime-audit-baseline.log`, case
`2dae5fda242f4badbffca38717b2f459`. Its stored presentation JSON and exception
remain intact; passing lifecycle checks are not a passing presentation check.

Schema 24 adds a separate interruption-observation table. Startup recovery marks
unfinished audits and Running/Waiting tasks in one transaction, preserving the
earliest interruption observation. Original audit IDs, timestamps, approval data,
results and artifacts are retained. It never invents a completion time, approval,
success/failure, output or tool message. Completed operations remain unchanged;
a subsequently received real completion remains authoritative. New executions
start without a historical interruption marker. Recovery invokes no tool, model,
browser or command.

The existing shared audit row now shows 已中断, 执行结果未知 and a localized
instruction to verify actual state before manually continuing. This distinguishes
an unknown effect from live work, cancellation, denial or verified failure. The
central icon style uses the existing warning token; text carries the meaning.

#### Verification and evidence

- Independent Debug/Release complete builds: zero warnings/errors.
- Independent Debug/Release Infrastructure: 729 checks each, covering forward
  migration rollback/retry, legacy messages/audits, artifacts, idempotence,
  completed-vs-unfinished rows, recovery rollback, cancellation and new/late work.
- Independent Debug/Release App: 42 audit checks each. Shared Headless rows cover
  220/360 widths in light/dark themes, the actual recovery text, wrapping and state
  classes. These are not native visual/keyboard acceptance.
- Independent Debug/Release Tools: 44 audit/deadline checks each.
- Original Debug/Release complete builds: zero warnings/errors. Each configuration
  passes Windows 148, Infrastructure 729, Tools audit/deadline 44 and App
  subagent/Headless/audit 206 checks. The original verification manifest retains
  each complete TRX hash. Structural review checks 591 C# files, 30 AXAML files
  and 3,236 methods/accessors, with a maximum method length of 78 and no violations.
- Candidate and original real-Chrome process checks pass in
  `runtime-audit-candidate-final.log` and `runtime-audit-original.log`, cases
  `eba5e9336baa49bc92d51cbe31c8648e` and `d610182885004d67815a6f4dff35ff6e`.
  Both retain one uncertain original audit, one navigation request and three
  unique ordered user messages. Model requests are exactly two before manual
  continuation and three afterward; saved model/reasoning and the unsent draft
  remain intact. All eight loaded module hashes are checked against the intended
  runtime and recorded separately for each checkout.
- A copied real schema-23 crash database also migrates through the original
  executable to schema 24 and shows interrupted/unknown without manual
  continuation. Its two messages, queue, draft and audit identity are preserved.
  `legacy-copy-aed34467e6a54bc7b88ea02167415d15` retains the copy manifest and
  `legacy-recovered.json`; the source database, WAL and SHM hashes stay unchanged.
- An earlier candidate probe correctly fails its assembly identity guard because
  SDK CandidateAssemblyFiles selected the old output DLL ahead of HintPath.
  Resolved-reference JSON and the failed case are preserved. The probe project
  now excludes both output directories from default items; its next build and
  runtime identity checks use the intended candidate. No manual DLL substitution
  or skipped identity check was used.

`artifacts/audit-interruption-recovery-20261019` retains design checks, the
original source baseline, the 30-path integration manifest, backups, structural
review, six complete candidate TRX identities and original build/check logs.
Seventeen existing persistence test files only update their current schema-count
expectation; the follow-up migration fixture also removes later version markers
when reconstructing its old checkpoint. No old failure evidence is overwritten.

Complete Debug regression 54927 is under
`artifacts/audit-interruption-integrated-20261019`. Its terminal exit is zero;
the summarizer verifies eight distinct complete TRX reports, 8,153 passed tests
and zero failures, skips or aborts. Before/after path/hash sets match for all
2,421 source/configuration and 2,035 runtime files. Report hashes and counters
are retained in `regression-summary.json`; the original-source/runtime freeze
ended after that review. Earlier failed regression reports remain preserved.

#### Limits

[BROWSER_COMMITTED_EFFECT_RECOVERY_ACCEPTANCE.md](BROWSER_MCP.md#browser-committed-effect-recovery-acceptance) subsequently adds one original
Debug/Release case where an approved actual-Chrome click has already committed a
flushed synthetic POST effect before host-only termination. It preserves that
effect exactly once, unknown audit results, another instance, draft and Queue
through restart and explicit continuation. That bounded evidence does not extend
to arbitrary browser side effects or power loss.

This is production Queue/Agent composition in separate CLI executables, a local
simulated Responses service, actual Chrome and temporary SQLite. It does not
establish native GUI, real-provider behavior, every browser mode, every launch or
transport instant, arbitrary interrupted clicks/downloads, OS/power-loss recovery or command
filesystem confinement. An interrupted audit cannot establish whether its remote
side effect occurred. Its approval fields may remain unknown after a crash;
the probe separately observes approval before the actual navigation. No automatic
replay or fabricated success is an acceptable substitute for that uncertainty.
<!-- /source: AUDIT_INTERRUPTION_RECOVERY_ACCEPTANCE.md -->


---

<a id="command-execution-acceptance"></a>

## COMMAND_EXECUTION_ACCEPTANCE.md

<!-- source: COMMAND_EXECUTION_ACCEPTANCE.md -->
### 命令执行链路验收

命令行为仍由 `MossAgent.Tools.BuiltIn/Processes/ShellRunTool` 拥有，
从 App 的既有主操作进入标准工具管线。进程契约保持平台中立，Windows 原生实现
隔离在 `MossAgent.Platform.Windows`；未引入 UI 执行进程或新的持久化结构。
运行要求包含 PowerShell 7（`pwsh`）。

#### 自动验收

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

#### 本轮发现并修复

- 按行事件读取会强行补换行，且在到达输出缓冲区前可能积累巨大的单行。
  改为每次最多 4096 字符的异步读取，两个输出流分别持续排空。
- Windows 下直接 Console 中文输出可能采用非 UTF-8 编码，导致替换字符乱码。
  为 PowerShell 输出和读取端明确使用 UTF-8，保留命令实际输出的换行。
- 首轮取消处理只在父进程仍存活时终止进程树，漏掉父进程退出后仍存活的后台进程。
  当前实现改为独立持有整棵 Job 进程树，回收不再依赖父进程是否仍存活。
- 命令说明和退出摘要转为本地化资源，失败详情不再混入元数据摘要。

#### Windows 进程归属复核

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

#### 桌面历史查看

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

#### 2026-10-10 测试夹具就绪发布

并行回归发现 `WorkspaceCommandFixture` 的 PID 就绪文件存在读写竞态：
`File.Exists` 已成立，但 PowerShell 写句柄尚未关闭，测试读取会遇到文件占用。
父、子进程现在通过同一个夹具 helper 先写 `.tmp`，关闭写句柄后再在同目录
重命名发布最终 PID 文件。未增加读取重试、放宽等待断言或吞掉异常，生产命令
执行和 Job 所有权没有改变。

九项真实命令工作流独立重复五轮，45/45 通过；TRX 在
`%TEMP%/MossAgent-header-fidelity-20261010-0800/pid-stress`。
这修复的是测试就绪信号，并不代替后面的进程生命周期验收。

#### 仍需验收

- Git、PTY 终端和浏览器的独立进程生命周期仍需复核。
  stdio MCP 已接入独立连接 Job，其验收范围见 [MCP_EXECUTION_ACCEPTANCE.md](BROWSER_MCP.md#mcp-execution-acceptance)。
  后续 Git 工具、后台分支查询和 worktree 已接入共享进程启动端口，真实 Git
  钩子的正常退出、取消、超时和并发回收验证见 [GIT_PROCESS_OWNERSHIP_ACCEPTANCE.md](GIT_WORKTREES.md#git-process-ownership-acceptance)；
  不扩张本记录原有的 shell 验收范围，也不代表 worktree 操作具有事务回滚。
  后续 PTY 所有权、实际命令执行、自然退出及后台子进程回收验证见
  [TERMINAL_LIFECYCLE_ACCEPTANCE.md](TERMINAL_TOOLS.md#terminal-lifecycle-acceptance)；不改变本记录的历史验收范围，
  也不代表终端模拟器或所有应用异常退出场景已完成。
- App 异常终止时的外部宿主验收、提权或系统服务创建的进程、并发长任务压力、
  资源耗尽、外部修改进程环境，以及不同 PowerShell 和本机命令编码。
- 完整浏览器、MCP 和子代理桌面交互验收及所有页面的像素一致性。

本记录不代表整个产品或所有命令进程生命周期要求已经完成。
<!-- /source: COMMAND_EXECUTION_ACCEPTANCE.md -->


---

<a id="global-approval-presentation-acceptance"></a>

## GLOBAL_APPROVAL_PRESENTATION_ACCEPTANCE.md

<!-- source: GLOBAL_APPROVAL_PRESENTATION_ACCEPTANCE.md -->
### 全局审批呈现验收

#### 设计自检

1. 行为属于 App 呈现层；只调整主窗口的审批视图归属和共享页切换器的键盘导航选项。
   不增加反向依赖、原生控件或页面专用外观。
2. 复用 `ApprovalViewModel` 的队列、请求身份、取消和决策命令，保留现有共享卡片尺寸、
   正文隐私展开与固定决策区，不增加工具或仓储端口。
3. 不改变授权根、审批政策、超时、输出限制或工具执行管线，不自动批准、拒绝或发送。
   待审批时继续屏蔽原生浏览器呈现；关闭遮罩仍具有更高输入优先级。
4. 保留页面实例、未发送草稿、设置搜索、编辑焦点与用户滚动；C# 文件各含一个类型，
   不增加静态状态或订阅。调试仍为 JIT，不修改 JSON/AOT 或 Windows x64 单文件发布。
5. 用真实 `MainWindow` 验证明暗主题下的页面切换、紧凑尺寸、键盘可达性、排队取消、
   旧请求决策拒绝及关闭遮罩，不将独立视图测试当作全局呈现证明。

#### 已复现与修复

- 最初 12 例中 6 例失败：设置页上的请求、返回设置后保留草稿和键盘入口均不可见。
  审批视图位于设置切换器的主页面中，切换到设置会隐藏它，虽然队列仍待确认。
- 将同一个审批视图移到主窗口第 1 行的全局层后，10 例通过；两例键盘回归仍失败。
  原设置页面的 `Cycle` 导航阻止 Tab 离开设置并到达审批卡。
- `AppPageSwitcher.SecondaryTabNavigation` 默认仍为 `Cycle`；主窗口的全页设置显式使用
  `Continue`，不改其他切换器或真正模态对话框的导航策略，不抢走编辑器焦点。
- 新增 14 例真实窗口回归全部通过；两个共享导航选项用例和原归属门禁也随之更新。
  关闭遮罩的输入命中测试等待 Headless 渲染时钟刷新后再检查真实命中及鼠标输入，
  没有为旧渲染缓存放宽断言或增加生产层补丁。

#### Python 原始截图

外部证据保存在 `C:\Users\wzheng\AppData\Local\Temp\MossAgent-global-approval-20261014`。
测试宿主使用真实生产 `MainWindow`、App 资源和服务组合，合成两个待审批请求；
没有调用文件工具、提供商或浏览器，没有执行对应写入，没有自动点击审批按钮。

- `native-before-light-v2/settings-pending.png` 与
  `native-after-light/settings-pending-clean.png` 均为普通权限、96 DPI、980×700 原始帧。
  前者设置页隐藏审批卡；后者保留设置与搜索草稿，显示两项等待确认和固定决策区。
  后者卡片位置为 `(230,295)`、尺寸为 520×385 DIP。
- `native-after-light/workspace-pending.png` 验证真实导航返回工作区后审批卡仍在。
- `native-after-dark-compact/settings-pending.png` 为 96 DPI、526×601 深色初始窗口，
  卡片位于 `(24,196)`、尺寸为 478×385 DIP，两个决策按钮均完整可见。
  此次是按紧凑尺寸启动，不宣称修复旧的原生动态缩放或高 DPI 几何问题。
- `approval-before-after.png` 按原尺寸并排呈现，没有缩放；明确标注光标和悬停差异。
  捕获脚本核对 PID、可执行路径、前台、原生尺寸、DPI、权限及非黑帧，不激活 Codex。
- 最新被动 Codex 帧为 96 DPI、最大化 1920×1032，仍为对话页，不是外观页。
  标题栏、导航轨、侧边栏及主区的四个背景采样一致，只证明这些采样颜色，
  不证明整页同状态像素一致，也没有取得 Codex 审批卡参考。
- 第一版临时宿主取消后未等待异步请求终态，退出校验失败；仅修复外部宿主的等待。
  旧失败日志保留，最终使用新的 `native-before-light-v2`，不复用已有停止信号的目录。
  三个最终宿主均正常退出 0、两请求取消且没有决策完成，浏览器屏蔽解除。

完整产品、同状态 Codex 外观对比、全窗口/主题/交互矩阵及旧原生几何诊断仍未完成。
截图、比较脚本与测试宿主不进入仓库；未暂存、提交、推送或发布。

#### 最终验证

- 最终定向 78 项通过；完整 App 4,351 项、完整像素 72 项、架构约束 7 项通过，
  无失败、跳过或中止。定向项是完整 App 的子集，不重复计数；六个完整非 UI 程序集
  未为此次 App-only 修改重跑，不能称为完整解决方案测试。
- 完整解决方案构建 0 警告、0 错误；作用域空白格式检查和全工作区差异空白检查通过。
  初次格式检查发现新增测试的 try 块缩进，补丁修正后复核，没有修改行为断言。
- 外部核验脚本逐一检查 TRX 计数与结果、测试驱动退出码、程序集 SHA 和三个最终宿主
  的生产 App 身份、截图元数据、取消终态及退出码；最终 `verified-checkpoint.json` 为真。
  App SHA 为 `E5C485EC9234AB06F546389E88F1CD92FC54F52A324B12492783C7A7A61BB841`。
- 所有本轮原生宿主已关闭，没有留下待审批请求或本轮测试进程。工作区仍保留既有改动，
  暂存路径数为零；整个 goal 继续保持未完成。
<!-- /source: GLOBAL_APPROVAL_PRESENTATION_ACCEPTANCE.md -->


---

<a id="pty-pid-publication-diagnostic"></a>

## PTY_PID_PUBLICATION_DIAGNOSTIC.md

<!-- source: PTY_PID_PUBLICATION_DIAGNOSTIC.md -->
### ConPTY test PID publication diagnostic

Status: candidate test-fixture repair verified independently, not yet applied to
the repository fixture or accepted as a production terminal change.

The complete `conversation-followup-storage-20261019` run reports one failure in
`StopOrDisposalReapsRealShellAndItsBackgroundChild(dispose: true)`: `BindAsync`
cannot read `child.pid` because another process has it open. This occurs before
the process-tree-reaping assertions. The fixture already writes a temporary PID
file and renames it, but its reader waits only for destination existence. That is
not an explicit completion handshake with the publishing call. The original
failure does not by itself identify which process held the file or establish a
production process-tree-reaping defect.

#### Controlled reproduction and candidate

`artifacts/pty-pid-handshake-20261019/PidHandshakeProbe.csproj` links the current
fixture, cleanup helper and original lifecycle test source. Raw references use
the already-built production assemblies; the diagnostic does not rebuild or
modify production source/test binaries. It creates only owned temporary roots
and ConPTY shells through the existing full-access tool execution path.

`PidPublicationTests` holds an owned PID file with FileShare.None, containing the
identity of its own shell. Before the publisher finishes, the current fixture
immediately attempts to read it and throws the same IOException. The expected
failure is in `results/original-locked-publisher.trx` (0 passed, 1 failed); it is
retained as a reproduction, not counted as a successful regression.

An artifact-only copy of the fixture makes two changes: publish `name.ready`
after WriteAllText/Move return, and wait for that marker before opening the PID
file. The new test verifies binding remains pending while the publisher owns the
file, then binds the same shell after release and verifies real process exit.
All four original lifecycle cases retain their process ownership, child cleanup,
natural-exit/restart and independent-task assertions. The candidate passes all
five checks in `results/candidate-native-lifecycle.trx`. There is no exception
retry, arbitrary additional sleep or relaxed assertion. Hashes of original and
candidate fixture are recorded in `candidate-identity.json`.

This demonstrates and repairs the reader/publisher protocol gap under a controlled
lock. It does not prove that every historical sharing violation has that cause.
The repository fixture remains unchanged while the original full run finishes;
afterward apply the bounded change and new test, run the real Platform.Windows
project and rebuild. Preserve the original full-run failure and distinguish the
subsequent fixed candidate. This diagnostic is real ConPTY/process automation,
not GUI/screenshot or strict filesystem-sandbox acceptance.

#### Applied after the frozen full run completed

The original frozen run finished with 8,053 passed and one failed out of 8,054.
App contributed 5,391 passing checks. Its 2,379 source/configuration files and
2,035 Debug runtime files had no before/after hash differences. The failed TRX
is retained; this result is not represented as an all-pass run.

The repository fixture hash was checked against `candidate-identity.json` before
copying the two-line readiness handshake. `PtyPidPublicationTests` now includes
the independently reproduced locked-publisher scenario in the actual Windows
test project. The complete project then passed 147/147 in Debug, including real
ConPTY, shell/child reaping, task isolation and the publication test; see
`artifacts/pty-pid-handshake-20261019/repository-results`. No production terminal
code or approval policy changed. Solution build and later product changes have
their own candidate identities and must be reported separately.
<!-- /source: PTY_PID_PUBLICATION_DIAGNOSTIC.md -->


---

<a id="runtime-observation-boundaries-acceptance"></a>

## RUNTIME_OBSERVATION_BOUNDARIES_ACCEPTANCE.md

<!-- source: RUNTIME_OBSERVATION_BOUNDARIES_ACCEPTANCE.md -->
### Terminal payload and subagent UI observation boundaries

Status: five explicit test files integrated after complete original regression
93473 ended: 8,136 passed / 2 failed, 8,138 total, no skipped/aborted tests. Eight
reports, 2,414 source/configuration files and 2,035 runtime files have matching
before/after paths and hashes. Its terminal payload sharing violation and subagent
collection-enumeration failure remain preserved. The original freeze has ended.

#### Terminal payload completion

PtyNativeLifecycleTests waits for executed.txt to exist, then immediately reads
it. Creation precedes the shell's WriteAllText close. A controlled exclusive
writer reconstructs those same two reader statements and fails with the same
IOException while the writer owns the file. The original full-run report does
not identify the exact locking process, so this is boundary evidence rather than
a claim about every historical sharing violation.

The test-only candidate adds a completion marker after the real shell's payload
write returns. Its reader awaits that marker, then checks the unchanged exact
Unicode/retained bytes. A new locked-writer negative case requires the read to
remain pending, then verifies exact content after close/publication. It adds no
IOException retries or extra arbitrary sleep. Real process restart, session
identity, child reaping and independent-task assertions remain intact.

file-baseline.log/TRX under artifacts/runtime-observation-boundaries-20261019
retain the intentional controlled failure. The first candidate Windows suite
passes 147 checks but lacked the already-integrated PID-publication test/readiness
lines in its older independent copy. Those were copied unchanged from the frozen
original; the synchronized candidate passes 148 checks including both publication
boundaries. That earlier 147 result is not described as the current full suite.

#### Subagent observation on the UI thread

The original failure is Collection was modified in the fixture's Messages.Any
predicate while real streamed progress changes the observable UI collection.
Without an Avalonia application, presentation uses its existing direct path;
the plain xUnit polling test can enumerate on a different worker thread.

The candidate moves all five lifetime cases into the existing awaited Headless
session and nonparallel visual collection. Reads and presentation execute on the
UI dispatcher; explicit access assertions guard the partial-dialogue predicate
and the presentation-failure callback. No production locks, exception-swallowing
poll retries or provider behavior changes are introduced. Existing real local
HTTP/child disconnection, selected parent/child models, reasoning, cancellation,
partial-dialogue recovery, safe errors and completed audits remain asserted.

Initial Debug/Release relevant App selections each pass 18 checks; the expanded
Subagent/Headless selections each pass 164, with complete TRX reports. These are
Headless dispatcher plus simulated-provider checks, separate from native UI or
actual provider acceptance. The changed scheduling addresses the identified
unsafe observation; it does not prove that all historical failures share a cause.

#### Ownership and further verification

Only five App.Tests/Platform.Windows.Tests files are candidates; production module
dependencies, ports, authorization, approval, process behavior and SQLite data are
unchanged. Reuse existing fixture deadlines and cancellation. Each C# file has one
type and stays under file/method limits. DESIGN.md and baseline-manifest.json
record required self-checks and original identities. Candidate Debug/Release
solution builds have zero warnings/errors. A Roslyn audit covers the five
candidate files and six newly authored browser probe files: eleven single-type
files, no file/method violations, maximum method length 51 lines. The prepared
five-file integration script defaults to an identity-only plan and refuses
execution without the original regression's reviewed terminal reports and
unchanged source/runtime identities. That gate has passed; all five explicit
files were copied with zero baseline conflicts and matching candidate hashes.
Original combined verification is tracked with the later production audit-recovery
slice in [AUDIT_INTERRUPTION_RECOVERY_ACCEPTANCE.md](TERMINAL_TOOLS.md#audit-interruption-recovery-acceptance).
Command filesystem
isolation remains an open safety blocker; this is not product completion.
<!-- /source: RUNTIME_OBSERVATION_BOUNDARIES_ACCEPTANCE.md -->


---

<a id="shutdown-recovery-acceptance"></a>

## SHUTDOWN_RECOVERY_ACCEPTANCE.md

<!-- source: SHUTDOWN_RECOVERY_ACCEPTANCE.md -->
### 退出失败恢复

#### 当前缺口与实施安排

退出失败时窗口保持打开，但原弹层只有“仍然退出”；Workspace 缓存第一次
失败的收尾任务，重复关闭也不能重新尝试保存已提交文件操作的反向记录。
本轮补齐明确的重试入口、失败任务缓存恢复和实际持久化链路，再验证原生
窗口及回归。全界面像素级对照、真实提供商和子代理完整产品验收仍有缺口，
不以这一轮退出恢复替代整个目标。

#### 设计自检

1. App 拥有退出弹层、窗口协调和工作区收尾。沿用执行器、仓库端口及既有
   停止流程，UI 不直接操作文件、SQL、进程或 HTTP，依赖方向不改变。
2. AppShutdownViewModel 只复用未结束或已成功的收尾任务；失败/取消后允许
   显式重新尝试。WindowCloseCoordinator 保持关闭阻断，重试运行时禁用按钮，
   重复关闭不重复启动收尾。释放窗口后拒绝迟到结果并移除事件订阅。
3. 重试不恢复模型运行、不重新执行文件工具，也不清除执行失败标记。未保存
   的反向记录仍通过既有五秒 cleanup token 和仓库端口保存同一历史 ID；
   失败继续留在错误状态，不自动强制退出或绕过审批。“仍然退出”保留既有
   显式入口并提示可能丢失未保存记录，不把它设为默认按钮。不增加迁移或 AOT。
4. 缓存移至现有退出状态对象，避免继续扩大接近 500 行的 WorkspaceViewModel。
   复用共享弹层和按钮，不新增逐页颜色、尺寸或阴影；C# 文件保持单类型及
   文件/方法硬上限，用户可见文本使用资源。
5. 测试将覆盖失败/取消后的重试、在途与成功任务复用、重复点击、反复失败、
   释放后的迟到结果、明暗和窄窗口布局、真实 SQLite 拒绝与恢复、历史去重及
   无工具重放；原生合成数据验证实际按钮、保存失败与正常退出。

#### 验证记录（2026-10-11）

仓库外证据位于 `C:\Users\wzheng\AppData\Local\Temp\MossAgent-shutdown-recovery-20261011`。
聚焦回归 `focused-passed` 为 20/20，包括在途/成功任务复用、失败/取消后显式
重试、重复点击、释放后的迟到结果和事件解除订阅。真实执行器先恢复文件字节，
SQLite trigger 拒绝反向历史后退出失败；移除合成故障后只保存相同历史 ID，
恢复工具审计仍只有一条，重新加载不重放文件操作。明暗布局覆盖生产最小
980×640 DIP 和既有 800×600 DIP 案例；不把这些 Headless 测试当作原生验收。

首次聚焦运行的 fixture 缺失 using 和布局案例失败日志保留。400×360 DIP
超出共享弹层及生产窗口支持尺寸，改用实际最小 980×640 DIP 验证，没有修改
生产共享弹层尺寸或放宽边界断言。后续 20/20 及全量结果才计为通过。

原生 QA 使用仓库外四个既有合成文件、真实 loopback 模型、标准工具执行器和
SQLite 存储，普通权限、1 倍缩放。没有新建文件，也没有通过 UI 执行文件删除、
点击“仍然退出”或操作其他应用。Python 仅被动截图；QA 程序、截图脚本和
合成数据库均未加入仓库。

- 1280×820 浅色窗口：首项文件恢复后故意拒绝历史保存，剩余变更为 3，内存
  反向记录为 1、数据库为 0。关闭时出现错误弹层，焦点在“重试退出”；回车
  触发一次失败重试且窗口保持打开。修复仅 QA 数据库的 trigger 后点击重试，
  同一条历史保存成功并正常退出。模型请求始终为 2，恢复审计始终为 1，四个
  文件 SHA-256 在退出重试前后相同。
- 980×640 深色窗口：重启加载旧历史，模型请求为 0、变更为 3、反向历史为 1。
  确认提示显示 0 个新建文件；再次只恢复首个剩余项后保存失败，剩余变更为 2。
  关闭及默认回车重试仍保持错误状态。修复合成存储故障后正常退出，反向历史
  为 2，恢复审计仍为 2，文件哈希没有因退出重试改变，模型请求仍为 0。
- 再次纯历史加载：反向历史和可见记录均为 2、剩余变更为 2、已恢复文件为 2，
  没有待保存记录。正常退出后的模型请求为 0，审计、历史 ID 和文件哈希不变。
  本轮刻意验证逐项失败边界，不宣称四个文件全部撤销。

原生截图 `native-light-error.png`、`native-dark-error.png` 均非黑且错误弹层
文字、按钮没有溢出。元数据、存储快照及退出日志分别保存为 native-light-*、
native-dark-*、native-inspect-*。最小窗口中为访问撤销确认隐藏了项目上下文
卡片；不将此交互当作整个界面的像素级验收。

完整解决方案构建零警告、零错误，测试进程退出码为 0。独立解析 `full` 的
八份 TRX，结果行数与计数器一致，合计 4741/4741，无失败或未执行项：Agent
83、App 3430、Visual 8、Browser 76、Infrastructure 171、Platform.Windows
137、Providers 377、Tools 459。明细保留在 `full-counts.json`。

八份本轮 C# 的 Roslyn 结构审计通过：每文件一个类型，最长文件 495 行，
最长方法 58 行，无语法错误或尾随空白；退出弹层 AXAML 为 15 行。限定范围
whitespace format verify 和 git diff whitespace 检查通过。验收结束后原生 QA
窗口和本轮构建/测试进程均已退出，暂存区为空。保持 JIT 调试和 Windows x64
单文件发布配置，没有提交、推送或发布。

强制退出仍可能丢失内存记录；不可恢复的任务收尾失败不会被重试入口掩盖。
测试数据库中的恢复不保证所有真实存储故障都能恢复。全界面 Codex 像素级
对照、真实提供商和子代理完整产品验收仍未由本轮覆盖，整体目标继续进行。
<!-- /source: SHUTDOWN_RECOVERY_ACCEPTANCE.md -->


---

<a id="terminal-clipboard-acceptance"></a>

## TERMINAL_CLIPBOARD_ACCEPTANCE.md

<!-- source: TERMINAL_CLIPBOARD_ACCEPTANCE.md -->
### Terminal selection and clipboard integration

#### Design self-check

1. App owns selection of immutable rendered cells and explicit user clipboard
   gestures, using Avalonia's existing clipboard API, as file preview does. No
   Windows APIs, filesystem operations or model-readable clipboard port are added.
2. Process input reuses TerminalInputController and terminal.write through the
   existing executor, session/path validation, approval and metadata-only audit.
3. Reads occur only after an explicit paste gesture. Copy/paste retain an owner
   until the clipboard operation returns; cancellation, a 30-second deadline,
   visibility/context changes and stale sessions prevent subsequent input. Paste
   has a 32,768-unit encoded limit, rejects control-sequence injection and requires
   explicit confirmation for newline-containing text. Uncertain writes never replay.
4. Selection, clipboard commands and paste encoding are separate single-type
   files. Shared controls/variants own hit geometry, menus and selection brushes.
5. Tests cover Unicode/wide cells, wraps/history, snapshot changes, bounded text,
   cancellation/late operations, stale sessions, ReadOnly/refusal, real ConPTY input
   and native light/dark wide/narrow render checks. These are separate from complete
   Codex workspace and physical keyboard/IME acceptance.

#### Acceptance status

Selection and clipboard integration are implemented. The overall goal remains active;
this does not establish complete Codex workspace or terminal fidelity.

#### Implemented behavior

- Drag selection, Ctrl+Shift+A and Esc use a shared rendered-cell selection model.
  Wide-cell continuations copy their complete glyph exactly once; graphemes and
  supplementary Unicode are not split. Hidden text is not extracted. Copy joins
  soft wraps, retains hard breaks and trims terminal padding. Selected-row mutation,
  task/session/view, alternate-buffer or geometry replacement clears stale selection.
  It never rereads a file or another task's screen. The extraction limit is 32,768 units.
- Ctrl+Shift+C/V and the shared toolbar menu have real copy, paste and select-all
  commands. Ctrl+C still reaches the process; reserved clipboard shortcuts never
  fall through to control characters. Shared variants own selection color; one fill
  per selected row avoids fractional-cell seams seen in the first native capture.
  Hidden/detached/disabled controls reject gestures; scope changes close the menu.
- Clipboard read requires an explicit user gesture, not a model/tool callback. Copy
  takes only the selected immutable snapshot. Ownership, a 30-second deadline and
  visibility/scope/session/mode revalidation suppress late notices and input. Started
  OS clipboard operations cannot be undone and remain busy until return. Cancellation
  or invalidation clears staged paste, never replays it, and exposes only fixed labels.
- Paste rejects ESC/C0/C1/DEL controls other than tab/newlines and rejects malformed
  UTF-16. CRLF and CR normalize consistently. Newline-containing content is held only
  in bounded memory until explicit confirmation; a newline can execute commands when
  the consumer does not support bracketed paste. Confirmation is not a substitute for
  tool approval. Pending content is discarded on scope changes or cancellation.
- Admitted paste is exclusive in TerminalInputController. Typing/legacy Send cannot
  interleave, and paste cannot merge into an outstanding keyboard approval. The existing
  terminal.write tool revalidates session, paths, policy, cancellation and timeout.
  Audit/progress are metadata-only, not clipboard payload logs. Refused/uncertain writes
  require fresh input rather than automatic replay. Encoded input, including envelope
  and Win32 record overhead, stays within 32,768 UTF-16 units.
- Win32 paste emits paired press/release records, including bracketed envelopes.
  Real Console.ReadKey testing exposed duplicated consecutive characters with a
  key-down-only envelope; matching releases preserve the exact payload. Raw VT
  bracketed strings did not reach the Windows console consumer correctly and are
  not used when Win32 input is enabled. Ordinary keyboard encoding is unchanged.

#### Verification (2026-10-09)

Evidence, passive Python scripts and the native test host remain outside the repository:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-terminal-clipboard-20261009`.

- Focused model/controller tests cover wide glyphs, combining/supplementary text,
  hidden content, history/soft wraps, changed snapshots, extraction/encoding limits,
  invalid/control text, ReadOnly/stale scope, refusal/no replay, exclusive writes,
  cancelled/noncooperative reads and same-session scope/mode changes.
- Headless tests route real drag/clipboard shortcuts against its isolated clipboard,
  validate themed selection brushes, actual shared-menu bindings/disabled states,
  multiline staging, ancestor hide/show cancellation and menu dismissal on scope change.
- Real ConPTY tests use the actual tool pipeline: bracketed Unicode/repeated-character
  and normalized newline payloads reach a Windows console consumer exactly; plain
  Unicode/repeated-character paste works in PSReadLine and does not run before Enter.
  These tests do not claim PSReadLine supports an artificially enabled bracketed mode.
  Initial artificial-mode tests and raw-VT experiments failed and remain in the evidence;
  the final consumer and normal-shell checks validate the supported encoding instead.
- Native `accepted-Light-470`, `accepted-Dark-470`, `accepted-Light-280` and
  `accepted-Dark-280` capture Render, Keys, Selection and Menu. Hosts are non-elevated,
  use scale 1, real ConPTY/tool execution and the shared pane, and exit 0 with empty
  stderr. Initial/applied grids settle at 64x26 or 35x26 with no pending/failure notices.
  Programmatic control events/selection are not physical input or IME acceptance.
  Main captures validate PID/title/native bounds and reject black pixels. Popup capture
  additionally checks exact owner and foreground before/after reading its window rectangle.
  Earlier PrintWindow-only popup composites show black transparent corners; the accepted
  on-screen menu pixels preserve the actual Windows rounding without painting a mask.
- `terminal-release-check`: 229/229 terminal tests pass; the final ownership check
  additionally passes 12/12, including clearing old-session selections and late waiting
  notices. `full-accepted`: seven-project default parallel regression passes 3660/3660,
  zero failed/skipped (App 2777, Windows 129,
  Providers 283, Tools 188, Browser 76, Infrastructure 144, Agent 63). TRX aggregation
  uses literal paths, including filenames with brackets. The solution build reports
  zero warnings/errors. Scoped whitespace, one-type/file, 500-line/80-line-method and
  XML checks pass; the largest changed source is 212 lines, maximum method 40 lines.
  These checks preserve the existing dirty checkout without staging any paths.
- `terminal-clipboard-comparison.png` is a crop/contact sheet of native iteration
  captures: the original fractional selection seams and corrected light/dark/narrow
  selected rows/menu. It is not a Codex side-by-side workspace comparison.

#### Remaining boundaries

- These are component/integration checks, not a full Codex workspace comparison.
  Physical keyboard/IME, accessibility, non-100-percent DPI and live splitter dragging
  remain separate. Word/line selection, rectangular selection and drag auto-scroll are
  not added; history can be scrolled before selection.
  [TERMINAL_IME_ACCEPTANCE.md](TERMINAL_TOOLS.md#terminal-ime-acceptance) records the later preedit-client/candidate-geometry
  integration; its programmatic evidence does not replace physical Windows IME checks.
- Clipboard extraction uses Avalonia's existing text API, which obtains OS text before
  the App's limit check; it is not a streaming OS-allocation limit. No clipboard history,
  image/file paste, model clipboard read or automatic clipboard inspection is introduced.
- A consumer that advertises bracketed paste must actually support it. The App cannot
  undo commands once accepted by a process or OS clipboard writes already started.
  Mouse reporting and VT query responses remain disconnected from process input.
- No commit, push, release, AOT configuration change or memory update is performed.
<!-- /source: TERMINAL_CLIPBOARD_ACCEPTANCE.md -->


---

<a id="terminal-ime-acceptance"></a>

## TERMINAL_IME_ACCEPTANCE.md

<!-- source: TERMINAL_IME_ACCEPTANCE.md -->
### Terminal input-method integration

#### Design and safety self-check

1. App owns IME preedit presentation and candidate geometry in the shared terminal
   viewport. Platform-neutral domain/contracts and Windows ConPTY remain unchanged.
2. Avalonia's existing TextInputMethodClient provides composition; only committed
   TextInput continues into TerminalInputController and terminal.write. No second
   process-input path, provider, file or clipboard access is introduced.
3. Preedit stays bounded and in memory, is never sent to a process, stored or audited.
   Missing/rejected commands and unavailable scopes cannot acquire an input client.
   Scope/session/command changes, hiding, disabling, focus loss and detachment reset
   composition and invalidate old clients. Existing tool approval, path/session
   validation, cancellation, deadline and input-output budgets remain authoritative.
4. The input-method adapter is a separately named single type. The existing shared
   viewport owns its overlay; shared styles own text appearance. No business-page
   dimensions, colors, native visual controls or mutable global state are added.
5. Focused tests must cover acquisition, preedit without writes, one committed
   request, accurate/clamped scrolling geometry, live frames, rejected/stale clients,
   bounded input and retained native keyboard/clipboard behavior. Native captures
   and physical Windows IME behavior are separate acceptance evidence.

#### Implemented behavior

- One task-owned Avalonia input-method client presents an underlined, shared-style
  overlay. Preedit is limited to 1,024 UTF-16 units and rejects control characters.
  Caret clamping does not split surrogate pairs. Surrounding terminal text is never
  offered to the input method. This adds no file, clipboard or provider access.
- While composing, terminal key encoding does not send Enter, Backspace, Escape or
  arrows to the child process. Only committed TextInput follows the existing
  TerminalInputController and terminal.write pipeline, including its approval,
  cancellation, session validation and bounded-input behavior.
- Client identity captures task, view, session, alternate screen, scope generation
  and input command. Lost focus, detachment, hidden/disabled ancestors, failed frames
  or command rejection clear preedit and permanently invalidate late callbacks.
  Restored eligibility requests a fresh client rather than reviving an old one.
- Candidate rectangles follow the real cell/scroller geometry, remain within the
  visible viewport, and update when frames, scrolling or window size change.
  Screen/scroller references are resolved once after markup loading; each geometry
  update shares one visible-area measurement, and redundant restoration refreshes
  are removed. This is reduced repeated work, not a measured performance claim.

#### Verification (2026-10-10)

Evidence, the passive Python capture script and the native QA host remain outside
the repository at
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-terminal-ime-20261010`.
No QA code or screenshots are added to application source or release packaging.

- The initial regression reproduces the missing input-method client: ten failures
  and one pass in `before/before.trx`. The first implementation also exposed a native
  Canvas violating shared-component gates, an incorrect requery event argument,
  cross-thread shared text-decoration state and inaccurate focus/detach test setup.
  These are corrected without relaxing visual-component or execution boundaries.
- Headless checks cover both themes, narrow/wide panes, composition without writes,
  exactly one committed request, ownership/eligibility changes, invalid input,
  scrolling/new frames and narrowing/widening while retaining the current client.
- Three real ConPTY tests route programmatic IME/TextInput events through the actual
  viewport/controller/tool executor. FullAccess commits `你好🚀`; AskEveryTime refusal
  and ReadOnly prove no admitted write. These do not invoke a physical Windows IME.
- Initial non-elevated native hosts capture the real App/MainWindow with synthetic
  tasks and an unreachable synthetic provider: dark 980x640 and light 1920x1032.
  Passive captures show the normal terminal, underlined `zhong wen 中文` preedit,
  and clearing it. Both candidate rectangles stay inside the viewport; both hosts
  stop their real ConPTY and close normally with empty stderr.
- Read-only checks of those isolated databases find no terminal.write calls, no
  conversation messages and no unfinished audit records. They contain only terminal
  start, resize and stop. The native preedit is a local programmatic client callback,
  not a keyboard command, provider request or physical candidate-window test.

- The QA host is rebuilt after the final cached-control/layout refactor.
  `final-dark-preedit.png`/`final-dark-cleared.png` cover 980x640 dark mode;
  `final-light-preedit.png`/`final-light-cleared.png` cover 1920x1032 light mode.
  Underlined preedit and clearing render correctly, candidate metadata remains
  viewport-clamped, and both fresh hosts stop the terminal and close normally.
  All four hosts have empty stderr and no remaining QA process. Read-only final
  database audits again find only successful start/resize/stop, zero terminal.write,
  zero messages and zero incomplete records (`native-audit-final.log`).
- `terminal-cached/terminal.trx` passes 255/255 terminal cases, including 26
  input-method cases. Narrowing/widening updates candidate bounds without replacing
  the live client or changing preedit. Shared-component and lifecycle gates remain.
- The initial solution-wide parallel run finishes 4330/4334, with four Windows
  timing failures: cancelled native resize, pending file presentation, shell
  descendant startup and natural terminal cleanup. No platform code, test deadline
  or test parallelism is changed to mask those failures. The evidence is retained
  in `full-final`; it is not a clean full-regression claim.
- The final run executes all eight projects sequentially while preserving default
  in-project parallelism: 4334/4334 pass, zero failed/skipped (`full-serial`).
  App 3246, Visual 8, Windows 129, Providers 366, Tools 297, Browser 76,
  Infrastructure 149 and Agent 63. Literal-path TRX aggregation verifies all eight
  results; the Windows project's 129 cases all pass on this separate run. This
  does not establish that the first run's concurrency-sensitive timing is fixed.
- Solution and rebuilt native host compile with zero warnings/errors. Scoped
  whitespace verification passes after correcting one initializer indent; the
  first formatting failure remains in `format-final.log`. Roslyn auditing checks
  six C# files plus the viewport/style AXAML: one type per C# file, at most 500
  physical lines and at most 80 lines per method. The largest is 243 lines.
  Whole-worktree `git diff --check` passes. Existing changes remain unstaged;
  normal JIT debugging and Windows-x64 single-file release settings are unchanged.

#### Remaining boundaries

- Physical Windows IME key routing, OS candidate-popup placement, international
  keyboard layouts, non-100-percent DPI and screen-reader behavior are not verified.
- Mouse reporting, VT query responses and cursor blinking remain separate work.
- This is terminal integration evidence, not pixel-identical Codex workspaces or
  completion of the full MossAgent goal. No commit, push, release or AOT change.
<!-- /source: TERMINAL_IME_ACCEPTANCE.md -->


---

<a id="terminal-interaction-acceptance"></a>

## TERMINAL_INTERACTION_ACCEPTANCE.md

<!-- source: TERMINAL_INTERACTION_ACCEPTANCE.md -->
### Terminal size and input integration

#### Design self-check

1. Tools.Abstractions owns immutable grid metadata and optional session-bound control
   contracts. Windows owns process mutations; App owns measurement, coalescing and
   keyboard encoding. No platform APIs enter the VT model or business views.
2. Resize and input reuse terminal tools and ToolExecutor. Optional session tags
   prevent an approval granted for an old process from reaching its replacement.
   Snapshot dimensions reflect successfully applied ConPTY changes, not UI intent.
3. Resize is side-effecting and must not bypass ReadOnly/AskEveryTime. Captured task
   scopes, cancellation, deadlines and session revalidation apply to queued work.
   No terminal query callback writes directly to the process. Queues/caches stay bounded.
4. Geometry, session mutation, operation ownership and keyboard encoding remain
   separately named types. Files retain one handwritten type and size limits.
5. Focused tests must prove grid preservation/reflow, resize failure and stale-session
   rejection, policy refusal, request coalescing, cancellation/late completion and
   real process geometry/input; headless/native layout and full regression are separate.

#### Acceptance status

The size/input integration is implemented. The overall MossAgent goal remains
active; this is not a claim of complete Codex workspace or terminal fidelity.

#### Implemented behavior

- Immutable applied grid metadata supports 20-500 columns and 5-200 rows. Windows
  publishes only successfully applied dimensions; failures leave metadata intact.
  Same-session state transitions retain geometry. Native resize, metadata commits
  and stop cleanup are serialized per owner. Cancelled requests waiting for another
  resize cannot mutate the child; started calls retain ownership until return.
- Optional session tags reject replaced processes and mismatched canonical working
  directories. Both write and resize use path prevalidation before approval and
  revalidate roots when execution begins. Resize requires process capability and
  side-effect approval, including ReadOnly/AskEveryTime refusal paths.
- VT resize uses the managed processor's primary/alternate resize operation,
  preserving screen identity and content. History adapts under a 100,000-cell
  per-buffer ceiling, with a 400,000-cell aggregate owner budget and eight-task cap.
- The shared viewport measures real cells/scroller space, clamps to supported
  geometry and coalesces requests with a 150-ms delay. Running requests retain their
  owner; newer measurements produce at most one follow-up. Refusal/failure stops
  automatic retries until an explicit retry. ReadOnly, missing-grid, cancelled or
  mismatched scopes do not automatically resize.
- Focused text and Enter/Backspace/Tab/arrows/Home/End/Insert/Delete/Page/F-keys and
  control shortcuts flow through `terminal.write`, not a manager callback. The frame
  selects classic/application cursor or Win32 key-record encoding. Unicode Win32
  input preserves UTF-16 ordering; printable/AltGr keys are left to TextInput routing.
  Ctrl+Shift+C/V are reserved, not converted to destructive Ctrl-C/V behavior.
- Input bursts coalesce for 15 ms and serialize through task-owned tool operations.
  Pending plus in-flight encoded input is capped at 32,768 UTF-16 units per owner;
  at most eight non-returned owners are retained. Refusal, cancellation, overflow or
  session/scope change discards pending input and exposes a localized safe notice.
  Explicit resume accepts new input only, never replays an uncertain write.
  Noncooperative operations keep same-task ownership; returning to the task displays
  a waiting notice until cleanup returns. Both coordinators use 30-second deadlines.
- The legacy command box remains functional and sends a captured session tag when
  metadata is available. Direct keyboard writes prevent interleaving its Send action.
  The shared 28-DIP toolbar height remains unchanged when cancel icons appear; the
  native/Headless measurement feedback loop reproduced during development is fixed.
- PowerShell initializes console input/output and `$OutputEncoding` to UTF-8 without
  loading user profiles. Passive native captures exposed system-code-page replacement
  of combining marks/emoji; a real-process regression and fresh pixels confirm the fix.

#### Verification (2026-10-09)

Evidence and the passive Python scripts/native host remain outside the repository at
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-terminal-interaction-20261009`.

- Focused tests cover state/grid preservation, extreme geometry and cache budgets,
  coalescing, explicit retry, refusal, stale approvals, revoked roots, session/scope
  replacement, cancelled waiters, deadlines and noncooperative cleanup ownership.
- Seven real ConPTY tests verify applied console geometry, Win32 Up/Ctrl-Left/Enter
  records, Unicode text with Left/Backspace editing in PSReadLine, Ctrl-C cancelling
  a running command without replacing the shell, and combining/supplementary output.
  These use actual tools/processes, not paid providers or mocked terminal output.
- Headless routing/layout checks cover both themes and 280/470-DIP panes, shared
  focus and command binding, text-vs-key routing, hidden controls, stable automatic
  geometry and existing appearance/composition gates. Headless fallback cells can
  exceed available width at the 20-column minimum; horizontal scrolling remains
  intentional there, rather than silently shrinking the supported child dimensions.
- Native `final-Light-470`, `final-Dark-470`, `final-Light-280` and
  `final-Dark-280` cover Render, Keys and Scroll (twelve passive captures total).
  Each host is non-elevated, scale 1, starts a real ConPTY through the tool executor
  and routes programmatic control events through the real shared viewport/controller.
  Capture verifies exact PID, title and window bounds and rejects black pixels.
  All hosts exit 0 with empty stderr. Settled captures report no busy/failure notices;
  wide panes use 64x26 initially/62x26 with history, narrow panes 35x26/33x26.
- `terminal-interaction-comparison.png` compares the live code-page/UTF-8 captures
  with a narrow dark pane. It is a MossAgent iteration comparison, not a Codex
  side-by-side screenshot or a claim of pixel-identical workspaces.
- A repeat full run exposed a measurement test sampling the first idle frame before
  scrollbar/grid catch-up finished. It now requires a bounded stable interval with
  metadata/frame agreement. The controller also skips requests already applied by
  the authoritative state source, avoiding redundant approvals when UI metadata lags.
  The same run had an unrelated sidebar test directory-release IOException; no
  sidebar or cleanup behavior was changed in this slice.
- `terminal-final`: 178/178 terminal cases pass; `settling-regression.log`: 15/15
  resize/routing/layout cases pass; `resize-ownership.log`: 21/21 Windows control,
  timeout/ownership and spawn cases pass. These supplement the full regression.
- `full-final`: default parallel full regression passes 3609/3609 with zero failed
  or skipped (App 2726, Windows 129, Providers 283, Tools 188, Browser 76,
  Infrastructure 144, Agent 63). All seven TRX files, including bracketed names,
  were aggregated with literal-path reads. The sidebar cleanup case passes on this
  final run; the earlier transient remains documented rather than patched here.
- `accepted-build.log`: solution build has zero warnings/errors. `format-final.log`
  verifies scoped whitespace; `source-audit.log` verifies one type per handwritten
  file, 500-line/80-line-method limits and shared-control XML. Normal scoped and
  whole-worktree `git diff --check` pass. Ordinary debugging remains JIT and release
  remains Windows-x64 single-file; no packaging or AOT configuration is changed.

#### Remaining boundaries

- Selection/copy and explicit user clipboard paste are now implemented separately;
  see [TERMINAL_CLIPBOARD_ACCEPTANCE.md](TERMINAL_TOOLS.md#terminal-clipboard-acceptance) for current safety and native evidence.
  Mouse reporting and VT query responses remain disconnected. No host or VT response
  callback can bypass tools. This document's size/input results remain their own baseline.
- Native event-routing tests are programmatic, not proof of physical keyboard,
  international layout, IME preedit/candidate positioning or accessibility behavior.
  Cursor blink, physical IME, non-100-percent DPI and screen-reader acceptance remain.
  The shared preedit client and candidate-geometry integration are now implemented;
  see [TERMINAL_IME_ACCEPTANCE.md](TERMINAL_TOOLS.md#terminal-ime-acceptance) for their separate tests and native captures. That
  evidence does not establish physical Windows IME or actual OS candidate-popup behavior.
- Fixed-size native component hosts prove rendered integration, not the complete
  Codex workspace or dragging a live inspector splitter; that comparison remains.
- Legacy third-party terminal managers without metadata/session-control support do
  not receive automatic resize or direct keyboard input. Tagged tool requests fail
  explicitly instead of silently downgrading session ownership checks.
- No commit, push, package release, AOT change or memory update is performed.
<!-- /source: TERMINAL_INTERACTION_ACCEPTANCE.md -->


---

<a id="terminal-lifecycle-acceptance"></a>

## TERMINAL_LIFECYCLE_ACCEPTANCE.md

<!-- source: TERMINAL_LIFECYCLE_ACCEPTANCE.md -->
### 交互式终端生命周期验收

日期：2026-10-09。范围：Windows ConPTY 后端及现有终端工具执行管线。
本记录不代表右侧终端面板或整个产品已与 Codex Desktop 一比一一致。

#### 实现前自检

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

#### 修复的根因

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

#### Porta.Pty 边界

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

#### 原生运行验收

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

#### 拒绝与失败路径

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

#### 可重复验证

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

#### 未完成范围

- 原始 VT 控制序列的终端渲染、字符网格、颜色、光标、粘贴和键盘交互。
- App 输出目前按事件投递 UI 队列；虽然最终字符串有 200,000 字符上限，
  高输出速率下的待处理队列仍需独立限流或合并，不能声称已解决背压。
  后续有界合并与取消入口的 App 验证见 [TERMINAL_PANE_ACCEPTANCE.md](TERMINAL_TOOLS.md#terminal-pane-acceptance)，
  不追溯更改本记录的后端范围，也不代表已经实现完整终端渲染。
- 自然退出时的 UI 状态自动同步、终端启动期间停止入口、所有状态的布局、
  明暗主题与 Codex 同状态原始截图对照。
- 非合作创建/关闭永不返回、应用被强制终止、关闭故障注入及压力/资源耗尽。
  不能通过提前删除 owner、超时后遗弃连接或启动替代进程来宣称解决。
- 有意加载用户 profile、提权/系统服务及不同 PowerShell 版本的兼容性。

普通调试继续使用 JIT；win-x64 单文件发布约定不变。本轮未提交、推送或发布。

#### 2026-10-10 自然退出测试夹具复核

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
<!-- /source: TERMINAL_LIFECYCLE_ACCEPTANCE.md -->


---

<a id="terminal-pane-acceptance"></a>

## TERMINAL_PANE_ACCEPTANCE.md

<!-- source: TERMINAL_PANE_ACCEPTANCE.md -->
### 终端面板操作与输出验收

日期：2026-10-09。接续 [TERMINAL_LIFECYCLE_ACCEPTANCE.md](TERMINAL_TOOLS.md#terminal-lifecycle-acceptance)，本轮补齐 App 的
操作所有权、可取消入口、输出队列预算和共享面板，不声称已实现终端模拟器。

#### 设计自检

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

#### 操作所有权

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

#### 有界输出投影

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

#### 共享组件与外观

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

#### 原生截图

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

#### 可重复验证

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

#### 剩余范围

- 真实终端模拟器、VT 颜色/光标、字符网格、交互键盘、粘贴及按任务保留滚屏。
- 本记录当时未覆盖自然退出后的状态事件与 UI 自动同步；后续只读状态源、
  自动资源清理和显式重启验收见 [TERMINAL_STATE_ACCEPTANCE.md](TERMINAL_TOOLS.md#terminal-state-acceptance)。
- 整个 Codex Desktop 同状态、同 DPI 的原始终端截图对照，以及完整工作区
  中的鼠标/键盘交互。独立组件宿主不能替代整个产品的一比一验收。
- 持续高输出下的帧耗时、长期内存和字符 shaping 压力；队列有界不等于渲染免费。

本轮不更改 JIT、win-x64 单文件发布约定，不提交、推送或发布。
<!-- /source: TERMINAL_PANE_ACCEPTANCE.md -->


---

<a id="terminal-state-acceptance"></a>

## TERMINAL_STATE_ACCEPTANCE.md

<!-- source: TERMINAL_STATE_ACCEPTANCE.md -->
### 终端会话状态同步验收

日期：2026-10-09。接续 [TERMINAL_PANE_ACCEPTANCE.md](TERMINAL_TOOLS.md#terminal-pane-acceptance)；本轮范围是只读会话
状态、自然退出后的资源回收、重新启动准入和 App 同步，不实现 VT 模拟器。

#### 实现前自检

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

#### 状态与生命周期

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

#### App 同步与准入

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

#### 截图发现的共享布局问题

新状态标题从“终端尚未启动”变为“暂时没有终端输出”后，原生像素显示尾字消失。
绑定值已完整更新，但共享空状态中的居中 StackPanel 仍保留原来按短标题计算的
宽度。原生日志：标题 desired=112、arranged=84；明暗 headless 测试均复现
desired=116、arranged=87，不能把构建通过当成文字正常显示。

为 owner 增加 AffectsMeasure 没能修复这个重排，尝试已撤回。最终改动在共享
`AutomationVariants.axaml`：内容面板使用可用宽度并保留 MaxWidth，文字仍按
TextAlignment 居中、图标仍独立居中，不加终端专用尺寸或强制重开面板。
两项新增布局回归由失败转为通过；原生窄窗 arranged=200、desired=112，
截图完整显示标题。与其他共享空状态的四项既有测试一起 6/6 通过。

#### 原生证据

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

#### 可重复验证

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

#### 剩余范围

- VT 终端模拟、字符网格、颜色、光标、交互键盘、粘贴和按任务保留滚屏。
- 完整工作区与 Codex 同状态、同 DPI 原图对照；合成组件宿主不是一比一验收。
- 高输出持续帧耗时、字符 shaping、长期内存、非合作取消回调阻塞 UI 等压力边界。
- 事件订阅者长期阻塞、非合作读取/创建/关闭永不返回、强制结束应用、资源耗尽
  和所有 PowerShell 版本。当前只证明抛异常订阅者隔离与已覆盖的退出路径。

普通调试继续 JIT，win-x64 单文件发布约定不变；没有 AOT、JSON 迁移、数据库
结构修改、提交、推送或发布。完整产品 goal 仍保持 active。
<!-- /source: TERMINAL_STATE_ACCEPTANCE.md -->


---

<a id="terminal-vt-acceptance"></a>

## TERMINAL_VT_ACCEPTANCE.md

<!-- source: TERMINAL_VT_ACCEPTANCE.md -->
### Terminal VT rendering acceptance

#### Design and ownership

1. App owns a bounded, in-memory VT projection and shared character-grid controls.
   Only the MIT-licensed RoyalTerminal managed VT core (0.6.2) is referenced;
   the RoyalTerminal Avalonia/backend packages are not used. Windows keeps ConPTY.
2. Output and metadata reuse the existing terminal ports. Output gains an optional
   SessionId. Process mutations still require the existing tool execution pipeline.
   No terminal response, clipboard, hyperlink, file/image, title or notification
   callback is connected to the host. This phase does not add keyboard input.
3. Only explicitly attached task scopes are admitted. Scope changes discard cached
   output and block the old session. Late sessions/revisions and unknown tasks are
   rejected. Output is parsed in order on the producer thread with bounded parser
   state, before the existing diagnostic text buffer can truncate it. A single
   background projection worker publishes coalesced immutable frames to the UI.
4. Responsibilities are split into frame/cell values, streaming control-string
   boundary, per-session VT screen, bounded task store, projection and shared
   rendering/scroll controls. Handwritten files retain one type each and the
   repository file/method limits.
5. Tests must cover CR/erase, split CSI, SGR, Unicode/wide cells, alternate screen,
   scrolling, immutable snapshots and task/session isolation. Negative cases cover
   opaque host/graphics strings, scope changes, stale/unregistered output, bounded
   history/cache, disposal and appearance override gates. Native pixels and real
   ConPTY output are required separately from headless layout tests.

#### Phase scope

- Render the existing 120-by-30 ConPTY grid without pretending that viewport
  dimensions have resized the child process. Use real horizontal/vertical scrolling.
- Retain at most eight admitted task screens and 128 history rows per screen.
  Glyph drawing caches are per-control and bounded independently.
- Implement follow-output and per-screen scroll restoration, with alternate-screen
  history isolated from primary-screen history.
- Keep current approved command-line input. Automatic child resize, interactive
  keys, bracketed paste, selections/copy and VT response routing remain separate
  work and must not be described as a complete interactive terminal.

#### Verification (2026-10-09)

Evidence is outside the repository at
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-terminal-vt-20261009`.

- Forty new cases cover the managed grid, streaming boundary, bounded history and
  graphemes, scope/session/cache ownership, immutable frames, dispatcher coalescing,
  source-to-view-model projection, glyph-cache bounds, layout and appearance gates.
- Real ConPTY output flows through ToolExecutor start/write/stop into colored Unicode
  cells. Restart creates a new session/view without old content. ReadOnly policy
  cannot start a shell or write to an existing one. A transient Windows directory
  release race was reproduced during test cleanup and fixed with bounded retries.
- `terminal-final`: 106 terminal tests passed; `app-accepted`: 202 relevant tests
  passed. `full-accepted`: default parallel full
  regression, 3514/3514 passed, zero failed or skipped (App 2654, Windows 106,
  Providers 283, Agent 63, Tools 188, Browser 76, Infrastructure 144).
- `accepted-build.log`: solution build, zero warnings/errors. Scoped whitespace,
  Roslyn one-type/file and size checks, XML checks and `git diff --check` passed.
  Only the managed RoyalTerminal 0.6.2 core and its managed dependencies were added;
  Avalonia remains 12.1.2. JIT debugging and Windows-x64 single-file publishing remain.
- Ordinary-permission native hosts (`QA_ELEVATED False`, scale 1) were captured with
  the existing repository-external passive Python script, exact PID/title/size checks
  and non-black validation. `final-Light-470`, `final-Dark-470`, `final-Light-280`
  each cover render, history, alternate screen and restoration; all hosts exited 0
  with empty stderr. Visual hosts use synthetic output only; real process behavior
  is verified independently by the integration tests.
- Native screenshots exposed the horizontal scrollbar overlapping the last row.
  The shared terminal scroll variant now reserves space, hides native arrow buttons
  and uses the same shallow thumb treatment as the vertical scrollbar. Headless
  regression covers the reserved gutter, measured cells, initial prompt visibility,
  scroll restoration and both themes at 280/470 DIP widths.
- `terminal-vt-comparison.png` juxtaposes the prior plain-text renderer and the new
  light/dark cell renderer using the same synthetic fixture. It was visually reviewed.
  This is a MossAgent before/after comparison, not a Codex-workspace fidelity claim.

#### Remaining boundaries

- This document records the historical fixed-grid phase. Variable child/grid
  dimensions, automatic resize, keyboard routing, Win32 input and explicit UTF-8
  startup are implemented in the subsequent [TERMINAL_INTERACTION_ACCEPTANCE.md](TERMINAL_TOOLS.md#terminal-interaction-acceptance);
  its results, not the baseline counts above, apply to the current integration.
- Clipboard/bracketed-paste integration, VT query responses, selection/copy, cursor
  blinking, screen-reader content and physical IME behavior still need acceptance.
  Approved command-line input remains available beside direct keyboard input.
- OSC/DCS/APC/PM/SOS host/image/hyperlink protocols are deliberately not enabled.
  No process-controlled terminal callback can read files, access the clipboard,
  open links or create a second process-write path.
- Full-workspace Codex side-by-side interaction and non-100-percent DPI checks remain
  unverified. No commit, push, package release or memory update was performed.
<!-- /source: TERMINAL_VT_ACCEPTANCE.md -->


---

<a id="tool-approval-layout-acceptance"></a>

## TOOL_APPROVAL_LAYOUT_ACCEPTANCE.md

<!-- source: TOOL_APPROVAL_LAYOUT_ACCEPTANCE.md -->
### 工具审批有限布局与决策区验收

#### 设计自检

1. 行为属于 App 呈现层：保留 `ToolApprovalView` 和现有共享卡片、滚动与按钮封装，
   尺寸统一放在 `ApprovalVariants.axaml`，不引入运行时反向依赖。
2. 复用 `ApprovalViewModel` 的请求队列、身份绑定和取消，以及现有可用空间转换器。
   不新增工具、仓储、浏览器或网络端口。
3. 不改变授权、审批政策、超时、取消或输出限制。正文仍须主动展开，
   决策只针对当前请求，不自动批准、不提供跨请求批准或弱化权限的入口。
4. 视图和样式保持规模限制；新测试一类一文件，异步验收入口必须真正等待回调。
5. 正向检查明暗主题、有限尺寸、缩放和滚动后固定决策区；负向检查长说明、
   长路径、分页正文、排队请求，以及拒绝旧请求不会批准下一请求。

#### 已复现的问题

- 正确等待异步回调后，新布局测试 10 项失败、2 项通过。980×700 时批准按钮
  顶部达 743 DIP；480 DIP 宽的独立视图中卡片左边界为 -20 DIP。
- 普通权限真实主窗口、独立临时数据与合成审批请求的 Python PrintWindow 截图
  同样确认正文溢出：原始窗口为 980×700，批准按钮顶部为 803 DIP，无法完整看到。
- 初次测试采用无返回值的 async 回调，可能选择 Headless 的 Action 重载，
  回调错误未可靠传回测试。新验收改为显式 `Dispatch<bool>` 并返回结果后才进行复现；
  其他同类测试入口需要一并审计，不能沿用先前绿色结果冒充可靠布局证明。

仓库外证据目录：
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-approval-layout-qa-20261006-01`。
真实窗口只被动捕获，没有通过 Windows 自动化点击权限请求或改变审批政策，
没有执行合成请求对应的文件修改、在线模型或生产用户项目。

#### 修复与可靠复测

- 审批卡片从所属视图取得有限宽高；维持 520-DIP 常规宽度和原有底部位置，
  缩小空间时收缩。正文进入有界滚动区，拒绝与批准按钮留在固定底部区域。
  不添加遮罩、自动批准、跨请求操作或权限策略入口。
- 明暗主题、980×700、980×560、480×640 和 1280×820 的独立视图回归，
  以及缩放后滚动、真实 Headless 拒绝按钮推进队列，共 12 项布局测试通过。
  原有正文隐藏、Unicode 分页、取消、请求身份和浏览器审批遮挡规则继续通过。
- 新增 `DispatchAsync`，显式使用有异步结果的 Headless 重载，真正等待回调，
  将同步异常、await 后异常和取消传回测试。迁移 10 个测试文件中的 15 处入口，
  用语法扫描门禁禁止再次使用容易成为 async Action 的调用。
- 可靠等待后揭示的提供商页面断言需检查命令参与的 `IsEffectivelyEnabled`，
  而非控件原始 `IsEnabled`；检查实际禁用状态，没有放宽命令或存储规则。
- 共享操作按钮的自动可访问名称改为低优先级回退，不能覆盖页面的明确名称或绑定。
  新增 4 项顺序、绑定更新和清除明确名称后的回退回归，模型菜单继续报告“选择模型”。
- 模型选择器的真实回归确认，奇数行高中的逐子项像素取整使徽标中心为 12 DIP，
  图标中心为 13 DIP。共享操作行内部关闭独立布局取整，保留外部按钮布局和点击尺寸；
  原有 0.5-DIP 对齐断言不放宽，更新布局后重取坐标。共享按钮相关 39 项复测通过。

#### Windows 原始截图

- 浅色主窗口为 980×700；修复后卡片 520×608，批准按钮顶部 634、高 29 DIP，
  滚动前后位置不变，两种决策操作均完整位于窗口内。
- 深色主窗口实际为 980×640；卡片 520×548，批准按钮顶部 574、高 29 DIP，
  滚动到正文底部后可查看分页和队列提示，决策区仍固定可见。
- 深色首次按请求的 620 高度校验，捕获器正确拒绝保存：真实主窗口最小高度将其
  限制为 640。重新确认同一 PID 仍属于当前临时宿主后，按真实尺寸捕获，没有重启、
  缩放图片或修改其他应用来掩盖尺寸差异。
- 四张最终图片分别位于 `after-light`、`after-dark-short` 的 `expanded.png` 与
  `scrolled.png`。所有图片非黑屏；两个宿主日志均记录普通权限与正常退出码 0。

这些是合成请求的真实主窗口绘制帧和 Headless 命令回归，不是原生鼠标审批、
真实文件执行或 Codex 审批界面像素一致性证明，也没有验证所有 DPI。完整目标仍未完成。

#### 最终验证

- 最终定向 132 项通过；完整解决方案 1,938 项通过，其中 App 1,449 项，
  无失败或跳过。最终解决方案构建 0 警告、0 错误。
- 本轮 16 个 C# 文件的空白格式检查通过。首次仅发现新布局测试的对象初始化
  换行，使用补丁修正后复核；保留所有布局、行为和失败断言。
- Roslyn 审计 889 个 C#/AXAML 文件，文件规模、单类型与方法规模没有违规；
  全工作区差异空白检查通过，没有暂存、提交或推送。
- 所有临时主窗口均已正常关闭，没有留下本轮运行的原生宿主。
  `approval-before-after.png` 将同规格 980×700 原始浅色帧并排呈现，未缩放。
  Python 截图脚本、对比脚本、图片和合成数据全部保留在仓库外。
<!-- /source: TOOL_APPROVAL_LAYOUT_ACCEPTANCE.md -->


---

<a id="tool-output-optimization-acceptance"></a>

## TOOL_OUTPUT_OPTIMIZATION_ACCEPTANCE.md

<!-- source: TOOL_OUTPUT_OPTIMIZATION_ACCEPTANCE.md -->
### 工具输出限流精简

#### 设计自检

1. 行为仍归 Tools.BuiltIn 的 ToolOutputLimiter，通过现有 ToolExecutor 调用；
   不改变模块依赖，不增加新工具或端口，不调用其它工具实现。
2. 移除 FitsBudget 预检，按文件记录、摘要、正文、对话的既有优先级单遍扣减。
   完整集合直接保留引用，只在首次截断时创建前缀；原结果和消息不被修改。
   结果集合与原有未超限快路径一样，要求调用方发布后不再修改。
3. schema、能力、授权、风险、审批、超时、取消及审计管线不变。无效文件记录
   仍截断整段后缀并标记不完整；不丢弃已提交的有效前缀。正文不能抢占文件预算。
   仍避免拆开 Unicode 代理对；预算耗尽后的空对话也不保留，不计算可能溢出的
   聚合 int 作为判断条件。Artifacts、错误代码及消息 IsPartial 原样保留。
4. 不增加生产类型；每个新增测试文件只有一个类型，文件/方法保持硬上限。
   不改变 UI、存储 JSON、JIT 调试或 Windows x64 单文件发布配置。
5. 聚焦测试经生产 executor 检查单遍读取与集合复用，覆盖有效前缀后跟无效记录、
   首项 null/无效、角色/正文截断、精确预算、原不完整标记及源数据不变。
   复用已有 tiny/negative/int-overflow、代理对、取消/超时、审计和文件快照回归。

#### 验证

仓库外证据目录为 `C:\Users\wzheng\AppData\Local\Temp\MossAgent-output-optimization-20261011`。
分配探针通过反射绑定同一生产 Limit 方法，在改动前后分别启动新进程执行；
使用同一 .NET 10、Debug 生产 DLL、Release 探针，关闭 tiered compilation，
每例预热 2000 次后测量 10000 次 thread-local 分配。只比较局部热路径，
不把分配结果当作全应用性能、内存峰值或 UI 验收。

固定运行条件下，三个输出字符数前后相同，单次分配如下（单位 bytes）：

| 场景 | 改动前 | 改动后 | 减少 |
| --- | ---: | ---: | ---: |
| 全部未超限 | 2112 | 2048 | 3.03% |
| 仅文字截断 | 5008 | 2224 | 55.59% |
| 最后一条对话截断 | 6376 | 3368 | 47.18% |

原始测量及对照保留在 `allocation-before.json`、`allocation-after.json` 和
`allocation-comparison.json`，探针源码只保存在上述临时目录，不进入仓库。

聚焦回归 `focused` 为 70/70，包含新增 9 项集合复用/单遍读取用例。
解决方案构建零警告、零错误；三份 C# 的 scoped whitespace format verify、
git diff whitespace 和含未跟踪文件的尾随空白检查均通过。生产限流文件由
99 行减为 79 行；新增测试文件分别为 143 和 25 行。

全量 `full` 的八份 TRX 为 4703/4704，进程退出码 1。唯一失败为
`FileChangeRestoreConcurrencyTests.AConcurrentCommittedWrite_IsNeverOverwrittenOrDeleted(create: false)`：
预期错误代码 file_changed，实际 tool_failed。其它七个测试程序集全部通过。
该并发类单独复测 `concurrency-recheck` 为 2/2，同一代码的工具程序集完整
复测 `tools-recheck` 为 457/457；两者均退出 0。独立 XML 汇总保留在
`full-counts.json` 和 `validation-summary.json`。

首次失败原件保留，未改并发测试或放宽其错误断言，不声称首次全量全部通过，
也不声称并发问题已根治。本轮未修改界面或进行新的原生界面验收。
<!-- /source: TOOL_OUTPUT_OPTIMIZATION_ACCEPTANCE.md -->


---

<a id="tool-progress-acceptance"></a>

## TOOL_PROGRESS_ACCEPTANCE.md

<!-- source: TOOL_PROGRESS_ACCEPTANCE.md -->
### 普通工具实时执行状态验收

#### 2026-10-08 设计自检

1. Tools.Abstractions 定义可选、仅元数据的阶段通知，Tools.BuiltIn 的既有管线
   报告阶段；Agent 转为任务/调用所属的事件；App 更新现有共享工具行。
   核心契约不引用 UI、SQLite、Playwright 或 Windows。
2. 复用 `IToolExecutor`、`ToolExecutionContext` 和 `AgentEvent`，不新增另一条工具
   执行路径。现有最终工具事件继续负责持久化；阶段事件仅用于实时呈现。
3. 审批、路径/能力/schema 验证、风险、超时、取消、结果预算和审计不改变。
   通知不带参数、命令正文、输出或凭据；观察者异常不改变执行决定。任务/调用
   身份过滤与有界通道隔离并发任务和子工具；关闭读取者时取消并等待所属执行。
4. 新类型各自独立文件，手写 C# 各一个类型；文件/方法遵守 500/80 行硬上限。
   实时行复用 `AppToolActivityRow` 的外观和行为，不新增业务页内的原生视觉控件。
5. 测试需证明审批前已有同一工具行、批准后阶段变化、拒绝/取消不会执行，
   超时和非协作执行仍保留归属；错误观察者、外调用/迟到事件不能影响状态。
   验证最终结果、持久化恢复与子代理对话，补充明暗真实窗口截图。

#### 已确认缺口

- 本轮开始时，Agent 仅在普通工具返回后发出 `AgentToolEvent`。只有子代理有流式详情；
  普通工具等待审批和执行期间只有全局活动文字，对话中没有属于该调用的工具行。
- 工作区命令拒绝/取消的真实设置、SQLite、模型适配器与 PowerShell 链路新增
  审批前工具行断言，用于先复现缺口，不能以结束后的工具详情代替实时状态证明。

#### 验收状态

##### 本轮实现

- 标准工具管线报告请求检查、审批、执行和结果保存阶段。Agent 为普通工具和
  子代理统一建立容量 256 的调用所属通道，保留现有最终结果事件和模型历史。
- 首个阶段先呈现工具行，再进入执行器。若读取者在此处关闭或取消，不启动执行；
  已启动的执行则取消并等待返回。结果保存仍遵守原来的审计完成语义。
- 仅接受本任务、本调用、本工具的合法阶段，忽略过期通知；观察者异常不能改变
  执行结果。阶段通知不携带参数、工具输出或凭据。
- 对话在同一工具行中更新阶段，无详情时不呈现可点击展开按钮；最终结果清除
  运行阶段并允许查看原有详情。子代理首个对话增量展开一次，不覆盖用户折叠。
- 复用 `AppToolActivityRow`，未添加业务页样式、原生视觉控件或持久化 schema。
  阶段本身不保存，恢复会话只显示既有最终工具消息，不复活运行中的状态。

##### 自动化验证

证据根目录：`%TEMP%\MossAgent-tool-progress-20261008`。

- 原实现新增审批前工具行断言：拒绝/取消两例均失败，见 `pending-before.log`。
  后续增加初始阶段后取消断言，先复现仍调用执行器，增加取消检查后通过。
- 定向验证 62 项全部通过：Tools 21、Agent 11、App 30。覆盖标准阶段顺序、
  schema/路径拒绝、未知工具、策略拒绝、审批中取消、执行通知时取消、超时、
  审计完成等待和输出预算、异常观察者、外调用/过期事件、非协作执行归属、
  同行更新、子代理增量以及最终结果恢复。
- 工作区命令测试继续走真实设置、SQLite、本机确定性 SSE 模型服务和 PowerShell；
  批准后观察同一行的执行/结果保存阶段，拒绝与取消不启动进程，停止清理子进程，
  最终消息和审计持久化。恢复的工具行没有临时运行阶段。
- `dotnet build MossAgent.slnx --artifacts-path artifacts/sidebar-validation --no-restore -m:1 -v:quiet`：
  0 警告、0 错误。
- 同一输出的完整解决方案测试：**3,076 项全部通过，0 失败、0 跳过**。
  `regression.log` 和 `regression/*.trx` 保留项目结果；本轮未出现上一轮的
  `SidebarHeadingRhythmTests` Windows 临时目录 sharing violation。
- 本轮 24 个源码/资源路径的 `git diff --check` 和定向
  `dotnet format whitespace --verify-no-changes` 通过，见 `format.log`。
  Roslyn 检查 23 个 C# 文件：单类型、500 行文件和 80 行方法硬限制均通过。

##### 原生窗口与截图

- 仓库外宿主使用真实 `App`、`MainWindow`、共享控件及独立合成数据库，不使用
  用户真实数据或凭据。截图仍使用仓库外 Python 被动 `PrintWindow` 脚本，带 PID、
  原始尺寸和黑图守卫，不激活/调整 Codex 窗口，不提交截图辅助脚本。
- 四组布局状态窗口：1280×820、980×640，分别浅色/深色，缩放均为 1。
  `phases-*/phases.png` 为**合成阶段布局**，不会执行工具或请求审批。
  16 个阶段行均高 32，图标与两段文字几何中心差不超过 0.5，且无展开按钮；
  原始布局及汇总在 `phases-*/phases-layout.json`、`geometry-summary.json`。
- 两组真实只读窗口：`actual-v2-Light-1280`、`actual-v2-Dark-980`。
  本机模型服务提出 `filesystem.read_file`，通过正常 ReadOnly 管线读取合成
  项目的 `README.md`；原生宿主确认四阶段顺序、同一行和成功结果，见
  `actual-stdout.log`。此路径不需要人工审批，不通过 Windows 自动化批准请求。
- 用真实 UI 展开最终详情，保留内容和 SHA-256 元数据，见 `expanded.png`。
  重启同一数据库后确认最终成功状态、无运行阶段，再展开与向上滚动深色窄屏
  详情，见 `restored.png`、`restored-expanded-scrolled.png`、`restore-stdout.log`。
  四个布局、两个真实执行及两个恢复窗口均正常关闭，宿主退出 0。
- 首版原生宿主观测遗漏了集合复制时已存在的初始阶段，导致自身校验失败；
  `actual-*`、`actual-debug` 中保留失败诊断，不计为通过。补齐宿主观测后再以
  新独立数据目录执行 v2，两组均通过，未用产品改动掩盖宿主验证错误。
- 被动捕获当前 Codex 原始窗口 1936×1048，保留 `codex-reference.png`；
  `tool-progress-comparison.png` 是不缩放的工具行裁片对比，不替换原始截图。

##### 限制与后续

- Codex 与验收窗口内容、尺寸及状态不同，当前工具图标、阶段状态和展开信息
  也有产品差异；这些证据不构成整体界面一比一还原验收。
- 原生截图证明布局、真实只读结果与恢复/展开；审批中、超时、取消、审计等待
  的确定性行为由测试证明，不能把合成布局截图称为这些场景的真实运行截图。
- 原生验证仅覆盖当前 Windows、缩放 1、两种窗口尺寸和明暗主题；未验证其他
  DPI、远程付费模型或真实用户任务。本轮没有更改 JIT 调试与单文件发布配置。
- 整体长期目标仍 active。本轮不提交、推送或发布，不宣称完整产品已完成。

#### 2026-10-12 呈现路径精简与共享工具行复验

##### 设计自检与范围

1. App 拥有本次同步呈现行为，继续依赖现有消息和事件；不增加 I/O、数据库
   迁移、执行路径、业务缓存或可变全局状态，模块依赖不变。
2. 三处调用行查询共用 `WorkspaceToolMessageLookup`。阶段匹配工具名与调用 ID；
   最终结果和子代理增量仍匹配首个运行中调用。不擅自统一不同事件的匹配规则。
3. 调用 ID 区分大小写、阶段工具名忽略大小写、重复行首个匹配、集合所属任务、
   完成后忽略迟到阶段均不变。工具名仍先取前 80 个 Rune 再移除控制字符，
   不把后面的可见字符补进被控制字符占用的预算；非法 UTF-16 仍替换为 U+FFFD。
   审批、授权、超时、取消和输出预算继续由既有管线负责。
4. 11 个相关手写 C# 文件各一个类型，均少于 250 行，最长方法含声明 70 行。
   共享 AXAML 为 43 行，SurfaceVariants 为 445 行，App.axaml 为 496 行。
5. 补充 Unicode/预算/空白/异常编码等价、首个匹配、外调用和独立集合、完成后
   迟到通知、命令可用性、明暗/宽窄几何与实际图标墨迹测试。没有调整无关断言。

##### 实现与分配测量

- 移除三处捕获谓词的 `FirstOrDefault`，并去掉结束工具行时的过滤迭代器。
  查询仍是线性扫描，不宣称时间复杂度变成常数。
- 工具名不再通过 LINQ 和逐 Rune 的临时字符串拼接；160 个 UTF-16 单元的
  栈缓冲足以容纳前 80 个 Unicode 标量。无需改写的普通名称复用原字符串。
- 摘要详情检查直接使用 `ReadOnlySpan<char>` 的 TrimEnd/IndexOfAny，避免复制
  尾随空白；原始摘要、详情、对话与保存格式不变，继续使用 JIT JSON。

预热 Debug、1,000 次单线程样本：

| 场景 | 原实现 bytes/operation | 优化后 bytes/operation |
| --- | ---: | ---: |
| `filesystem.read_file` 的本地化阶段摘要 | 872 | 152 |
| 1,000 条普通消息后定位已完成行并忽略迟到阶段 | 128 | 0 |
| 重复查询含 2,000 个尾随空格摘要的详情可用性 | 未记录 | 0 |

原实现新增文本/查找测试为 18 通过、2 个分配断言失败；修改后均通过。
包含 500 个确定性的混合 UTF-16 样本与各合法阶段的比较，不放宽分配上限。
这些是特定呈现路径的分配样本，不代表整个应用无分配或实际延迟改善比例。

##### 图标与真实截图

- 被动 Python 截图取得当前 Codex 1920×1032、96 DPI 原图，不操作其 UI。
  原尺寸 ROI `(744,815,766,839)`、相对背景差值阈值 20 测得终端墨迹 14×14；
  早期放大图估算的 12×12 不采用为最终尺寸依据。
- 共享工具行使用已有 12-DIP `icon-small`，终端与子代理使用同一尺寸。
  14-DIP 结构槽保留描边空间，避免静态内容呈现器裁掉左侧一个像素。
  工具行采用独立圆角方形终端几何，不改其他终端入口。
- 静态行不再保留无效展开箭头槽，摘要左偏移 21 DIP；可展开行保留真实箭头，
  偏移 43 DIP。两种表面保持 32-DIP 最小高度，图标与两段文字中心差不超过
  0.5 DIP。真实 Enter 命令与静态不可交互行为保留。
- 仓库外原生宿主仅加载当前 App 共享资源/控件，显示合成工具行；不启动产品
  工作区、浏览器、工具或模型请求。Light/Dark × 450/900 客户区宽度四组被动
  Python 截图均非黑图，终端墨迹均为 14×14，长摘要省略且不覆盖状态文字。
  全部四个最终宿主正常退出 0，无新宿主残留。
- 原图、未缩放对比及 `glyph-measurements.json` 保留度量来源。原生宿主首次
  资源转移顺序错误仅在宿主中修正；像素测试首次新增并发类造成 Avalonia
  全局初始化冲突，以新增专用非并行集合隔离，不修改既有 Markdown 断言。

##### 测试状态与限制

- 优化代码的串行完整 App 回归通过 4,019/4,019、无跳过；此轮之后仅进一步
  校正共享图标/槽间距和测试格式。最终代码的定向 App 回归为 128/128，
  包含原分配失败类；完整像素程序集为 12/12，架构约束为 7/7。
- 初次默认并行 App 完整运行出现未修改的
  `ProviderCatalogLookupTests.WarmModelCommandAndAvailabilityChecks_DoNotAllocate(missing: true)`
  失败（1,296 bytes 对零断言），随后长时间不再输出。保留线程栈后取消该次
  运行；它不计作完整通过。串行与最终定向复测通过，不宣称修复了其间歇原因。
- 最后几何校正期间曾尝试写入仍由串行 testhost 占用的输出目录，产生复制锁
  错误；等待该运行完成后重建并复测，没有终止进程来把编译失败当作产品修复。
- 最终 scoped whitespace、Roslyn 类型/语法/方法规模和 diff 检查通过。
  最终完整解决方案构建为 0 警告、0 错误；格式整理后的完整像素程序集再次
  通过 12/12。未经修改的其他完整程序集没有在本轮全部重跑。
  本轮不恢复 AOT、不改发布配置、不暂存、提交、推送或发布。
- 合成组件窗口不是生产 App 的当前版本整窗验收；Codex 只有浅色局部参照，
  并不证明深色、其他 DPI、相同内容或全窗口像素一致，也未重复真实模型/工具链。
  本轮未重启已有基线 App 实例，整体产品目标继续 active。

证据均在仓库外：
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-tool-row-fidelity-20261012-221021`。
Python、原生宿主、截图、初始失败日志、最终 TRX 和测量数据不加入仓库；构建
产物位于已忽略的 `.scratch/tool-row-fidelity-20261012`。
<!-- /source: TOOL_PROGRESS_ACCEPTANCE.md -->


---

<a id="tool-schema-acceptance"></a>

## TOOL_SCHEMA_ACCEPTANCE.md

<!-- source: TOOL_SCHEMA_ACCEPTANCE.md -->
### Tool input-schema validation

#### Scope and design review

This slice replaces the incomplete handwritten assertion evaluator with
JsonSchema.Net 8.0.5 (MIT). It is a correctness and maintenance refactor, not a
claim that all schema dialects or arbitrary remote schemas are supported.
The validation entry point shrinks from 236 to 56 physical lines; explicit
security policies and compatibility handlers live in separate small types.
This does not imply a reduction in every file or total dependency footprint.
Dead string-length error resources and the old input-length branch are removed.

The five engineering questions are answered as follows:

1. **Owner/dependencies:** Tools.BuiltIn owns validation within the existing
   ToolExecutor. Domain, Application, Agent and UI dependency directions stay
   unchanged. No Avalonia, network or storage access is added to the validator.
2. **Existing contracts:** Built-in tools and MCP adapters still expose their
   schema through ToolDescriptor and run through IToolExecutor. No alternate
   execution or approval path is introduced.
3. **Safety:** validation completes before capability checks, path preflight,
   approval and invocation. Invalid schemas return `tool_schema_invalid`;
   invalid inputs return `invalid_arguments`. Cancellation returns
   `tool_cancelled` when auditing has already started. Audit-start failures
   retain the existing fail-closed `audit_unavailable` behavior. Existing
   tool-execution deadlines, artifact/output limits and approval policy remain.
4. **Size/lifetime:** each handwritten C# file contains one type. Local dialect
   handlers and their cancellation token are created for each call; no global
   registry mutation, mutable global state or unbounded schema cache is added.
5. **Tests:** assertions cover accepted and rejected inputs, pre-approval
   rejection, no tool invocation, bounded patterns/structure, cancellation,
   audit completion and the real workspace/model/MCP/SQLite restore chain.

#### Compatibility and limits

- Default dialect: draft 2020-12. Explicit draft 06, draft 07, 2019-09 and
  2020-12 declarations are accepted. Mixed nested dialects are rejected.
- Common assertions now include additional-property schemas/refusal, nested
  properties, `const`, compositions, conditionals, array counts/uniqueness and
  local `$ref`. Unsupported formats/vocabularies are not a security guarantee.
- Only acyclic same-document JSON Pointer `$ref` is supported. External/file
  references, anchor references, dynamic/recursive references and nested `$id`
  resources are rejected, including in unused optional subschemas. A root
  `$id` with local pointers works without fetching a remote resource. Literal
  objects in `const` are data, not schema references.
- A per-call SchemaRegistry refuses remote fetching. Per-call handlers preserve
  Unicode scalar counting rather than grapheme counting, including combining
  marks and supplementary characters; length constraints must fit Int64.
- Schema JSON is limited to 262,144 UTF-16 code units. Policy traversal allows
  1,024 expanded schema-node visits and depth 64; reference expansion consumes
  the same budget. JsonDocument also retains its default parsing depth limit.
- Arguments are limited to 2,097,152 raw JSON UTF-16 code units, 8,192 value
  nodes and value depth 64, even if a caller parsed with a larger depth limit.
- Each pattern is at most 4,096 code units. Regex uses .NET ECMAScript with
  CultureInvariant and a **25 ms timeout per match**, including
  `patternProperties`. This is not a hard deadline for the whole validation:
  compilation/library evaluation cannot be preempted mid-call. Cancellation
  is checked around evaluation, during policy traversal and every 256 Unicode
  scalars in length evaluation. The library's pattern-properties loop itself
  does not check the token between successful matches.
- Invalid descriptors or arguments never reach approval or tool invocation.
  Errors use localized safe summaries, not raw input, remote exception text or
  pattern details. A missing root required field may show its ASCII identifier
  only when at most 48 characters long.

#### Reproduction and automated evidence

Evidence remains outside Git in
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-schema-validation-20261010`.

- `red/schema-red.trx`: the 38 new object/composition cases against the old
  evaluator produced 24 failures and 14 passes before the replacement.
- `green/schema-boundaries.trx`: 95 selected schema, pipeline and existing
  string/audit cases pass, including budget boundaries, malicious patterns,
  unused malformed schemas, nested IDs and cancellation.
- `green/workspace-schema-green.trx`: 10 selected workspace MCP cases pass.
  New cases run real loopback model HTTP/SSE and MCP discovery, reject root
  unknown fields, nested unknown fields and invalid `const` values, make no
  approval request or `tools/call`, deliver the failure to the model follow-up,
  persist the visible tool result/audit and restore without replay.
- `solution-build.log`: solution build succeeds with zero warnings/errors.
- `full-tests.log` and `full/*.trx`: all eight test assemblies pass, 4,239
  tests total, with no failures or skips. This is the current workspace result,
  not the earlier 4,157-test baseline. App.Tests accounts for 3,166 tests and
  takes approximately four and a half minutes.
- `format-green.log`: scoped whitespace verification succeeds.
- `code-size.log`: all 19 modified/new handwritten C# files satisfy one type,
  file <=500 lines and method <=80 lines.

#### Native verification

The temporary native QA host and generated data are outside the repository.
It starts the production Avalonia App with synthetic providers and loopback
MCP/model servers, not a manually fabricated ToolResult or a paid API call.

- Log reports `QA_ELEVATED: False` (ordinary permissions).
- Computer Use clicked the actual Send button in the running workspace.
- A model-requested MCP call contained an unknown nested property. The visible
  localized tool failure and the final assistant response appeared normally;
  no approval prompt appeared.
- Log reports `code=invalid_arguments; remoteCall=False; modelRequests=2`.
- Passive Python capture is saved as `native/schema-rejected.png`; it is
  non-black and shows the tool failure and completed model follow-up.
- The native window closed through its Close button. The temporary QA process
  remained without a window and was stopped after checking its exact executable
  path. This slice does not claim successful graceful shutdown of that helper.

Follow-up on 2026-10-10 diagnosed that temporary residual process: the production
App lifetime had already returned, and the MCP test server's asynchronous cleanup
captured the stopped UI context. Context-independent loopback fixtures now pass
deterministic regressions. New native completed and approval-pending runs exit
without termination, with real persisted task/tool/audit verification; see
[WINDOW_SHUTDOWN_ACCEPTANCE.md](TERMINAL_TOOLS.md#window-shutdown-acceptance). The earlier evidence is retained rather than
rewritten as an originally successful helper teardown.

This is a light-theme native error-path check, not full Codex visual-fidelity
acceptance, dark-theme/resizing coverage, or acceptance against real third-party
MCP servers. General recursive/dynamic-resource schemas remain unsupported.
Windows x64, ordinary JIT debugging and single-file release settings are
unchanged; no AOT work, migrations, commits or publication are included.

#### 2026-10-11 registry isolation follow-up

The original per-call-registry claim above described the intended boundary, but
BuildOptions actually defaults to SchemaRegistry.Global. Cold-start tests found
that Build wrote invocation-owned schemas to a shared non-thread-safe dictionary
and the Fetch assignment changed Global.Fetch. The old full regression and native
evidence do not prove absence of this race.

ToolSchemaDialect now explicitly creates a private SchemaRegistry for each call
and still refuses remote fetching. This avoids cross-call IDs, global retention
and concurrent dictionary corruption without locks, warm-up or retry logic.
The original validation limits and pipeline ordering remain unchanged. See
[TOOL_SCHEMA_ISOLATION_ACCEPTANCE.md](TERMINAL_TOOLS.md#tool-schema-isolation-acceptance) for the reproduced failure, regression tests
and independent cold-process evidence. This follow-up does not rewrite the
historical results or claim complete Codex visual fidelity.
<!-- /source: TOOL_SCHEMA_ACCEPTANCE.md -->


---

<a id="tool-schema-isolation-acceptance"></a>

## TOOL_SCHEMA_ISOLATION_ACCEPTANCE.md

<!-- source: TOOL_SCHEMA_ISOLATION_ACCEPTANCE.md -->
### 工具 schema 注册表隔离验收

#### 2026-10-11 根因

上一轮首次全量的子代理误判不是通过重跑解决的。本轮独立冷启动探针使用
生产 ToolExecutor、合法的 `subagent.run` 同形 schema 和无副作用合成工具：
16 个新进程，每进程 64 个并发调用者、每调用者四次调用，八个进程出现
`tool_schema_invalid`。不连接真实提供商，不写用户文件。

仓库外探针通过 first-chance 调用栈定位到
`Json.Schema.SchemaRegistry.RegisterSchema` 的非线程安全 Dictionary 并发操作。
核对已安装 JsonSchema.Net 8.0.5 nuspec 所指源码 commit
`3520d7ac43e5c6c9b91abeac5af992eb82ffbf63`：

- `new BuildOptions()` 的 SchemaRegistry 默认引用 Global，并非新实例。
- 原代码设置 `options.SchemaRegistry.Fetch` 实际改写了全局 Fetch。
- 每次 Build 把请求 schema 写进同一个全局字典，导致并发冲突、相同根 `$id`
  互相覆盖拒绝，以及校验结束后仍全局持有已处置 JsonDocument 的 schema。
- 只并发执行元 schema 检查的两个探针模式，各八个冷进程均没有失败；
  不能将该问题误归因于元 schema 检查，也不能通过重试或预热掩盖。

#### 设计自检与修复

1. 修复归 Tools.BuiltIn 的现有 `ToolSchemaDialect.Create`，依赖方向不变。
2. 使用现有 BuildOptions 的 SchemaRegistry 属性，每次调用显式创建私有实例；
   ToolDescriptor、IToolExecutor、元 schema、方言与兼容处理器不变。
3. 私有 Fetch 仍拒绝远程获取。既有 schema/value 预算、路径/能力验证、审批、
   超时/取消、输出限制与审计顺序不变，不放宽非法 schema 或参数的拒绝规则。
4. 不添加全局锁、预热、重试、静态注册表或无界缓存，不修改依赖版本。
   生产文件保持一个类型且远低于文件/方法上限，所有调用状态仍归请求所有。
5. 十个回归覆盖默认及四个声明方言、重复根 `$id`、本地指针、不同 const、
   每方言 128 个并发请求、合法/非法输入的审批/执行/审计状态，以及 Global
   不出现请求 schema、Global.Fetch 身份保持不变。原代码十例全部失败。

另外新增两个实际工作区回归，不向原有 fixture 堆积逻辑：两个独立任务
通过真实设置保存命令配置 loopback 模型与 MCP 服务，使用相同根 `$id`
和不同 message const，同时从生产主操作命令启动。分别验证双成功、
一个成功而另一个非法参数被拒绝，核对各自可见结果、SQLite 和审计记录。
重建服务恢复后模型请求数不增加，非法调用始终没有远程工具执行。

#### 证据

所有探针、合成日志和测试结果保留在仓库外：

`C:\Users\wzheng\AppData\Local\Temp\MossAgent-schema-cold-start-20261011`

- `red-probe-*.log`：16 进程基线，八个进程失败。
- `stack-probe-1.log`：真实生产调用栈定位全局注册表字典并发访问。
- `meta-flag-*.log`、`meta-tree-*.log`：元 schema 单独并发对照。
- `red/registry-isolation-red.trx`：十个新回归对原实现均失败，包括全局残留。
- `focused/registry-isolation-green.trx`：首次修复后专项通过 140/140。
- `green-probe-1..64.log`：64 个全新进程，共 16,384 次生产管线调用，
  失败进程和失败调用均为零，不通过重试或复用已预热进程获取该结果。
- `app-focused/workspace-isolation-green.trx`：最终双任务/MCP 与相关工作区
  专项通过 16/16，包括两个新回归的实际模型/MCP/持久化/恢复链路。
- `tools-final/registry-isolation-final.trx`：精确选择的 schema/string/ToolExecutor
  专项通过 92/92；该次选择范围与首次 140 例不同，不拼接成全量结果。
- `agent-final/agent-final.trx`：Agent 独立全量通过 69/69。
- `subagent-app-final/subagent-workspace-final.trx`：App 子代理专项通过 105/105，
  覆盖合成提供商协议、子任务运行/事件/对话展示与生命周期链路。
- `format-final.log`：三个本轮 C# 文件的专项空白格式验证通过。
- `code-size-final.log`：Roslyn 审计三个文件各一个类型，最长文件 67 行，
  最长方法 22 行；没有新增全局状态或巨型辅助类。
- `solution-build-final.log`：完整解决方案构建成功，零警告、零错误。
- `full-final.log`、`full-final/*.trx`、`full-final-summary.json`：构建完成后
  独立串行全量通过 4592/4592，八个程序集失败与跳过均为零。
  Agent 69、App 3387、App.Visual 8、Browser 76、Infrastructure 170、
  Platform.Windows 137、Providers 377、Tools 368。计数只读取本次八份
  完整 TRX，没有合并先前失败或专项结果；相对上一轮新增十个工具回归和
  两个真实工作区回归。使用 90 秒无进展守卫，正常等候 UI 回归结束。
- `diff-check-final.log`：工作区已有 tracked 差异的空白检查通过；本轮
  untracked 文件单独验证无尾随空白。暂存区为空，本轮没有提交或发布。

#### 验证范围

本轮实际工作流证据来自生产 App composition、设置/主操作命令、Agent、
loopback HTTP/SSE、MCP 和 SQLite 的集成测试。没有付费调用、真实第三方
账号或原生窗口截图；没有将 headless 测试当作当前界面的像素验收。
审批、安全预算、取消、输出限制和持久化协议保持不变。

本轮不改变界面布局、数据库或发布方式，不代表完整产品目标完成。
安全撤销、任务精确差异及当前 Codex 全界面对照仍需继续推进。
<!-- /source: TOOL_SCHEMA_ISOLATION_ACCEPTANCE.md -->


---

<a id="tool-scope-acceptance"></a>

## TOOL_SCOPE_ACCEPTANCE.md

<!-- source: TOOL_SCOPE_ACCEPTANCE.md -->
### Explicit task tool scopes

#### Purpose and required self-check

PR review requires a source-only task boundary, not ordinary projectless chat
with unrestricted tools. [PULL_REQUEST_WORKFLOW_PLAN.md](GIT_WORKTREES.md#pull-request-workflow-plan) retains the complete PR
requirements; this slice implements only the immutable allowed-tool foundation.

1. Tools.Abstractions owns the context scope/value check. Tools.BuiltIn enforces
   it in the shared executor. Agent filters advertised tools/delegation and passes
   the same restriction into child execution. No infrastructure or UI dependency
   is introduced, and no static global permissions are added.
2. Reuse `ToolExecutionContext`, the existing catalog/registry and ordinary tool
   pipeline. No alternate execution path, direct tool call or fake project.
3. Scope validation follows schema/capability validation and precedes workspace
   inspection, tool prevalidation and approval. Rejection completes the ordinary
   audit with `tool_scope_denied`. Allowed calls still obey policy, approvals,
   path/workspace guards, deadlines, cancellation, output limits and audit. This
   static immutable scope does not replace live account/repository revocation
   checks, which remain to implement for the source workflow.
4. Changes retain one type per C# file and the existing file/method size limits.
   Scope names are frozen with the registry's ordinal-ignore-case semantics;
   context copies share immutable data, never a caller's mutable HashSet.
5. Focused tests cover allowed/denied calls, all major capability families,
   audit/approval ordering, immutable copies, schema/policy/projectless precedence,
   descriptor filtering and parent/child intersection. A synthetic adversarial
   model issues an unadvertised call through the production executor; it receives
   a denial without invoking the underlying tool. This uses no source account,
   native window or paid model.

#### Semantics

- `AllowedToolNames=null` preserves existing behavior; an empty set denies all.
  An allowed name is not a filesystem, network-target, source-account or approval
  grant. `ToolsEnabled=false` still denies tools regardless of the list.
- Registry aliases are case-insensitive, but caller-provided collection comparers
  cannot alter membership semantics. Changing the input collection cannot broaden
  or narrow an already-captured operation's immutable scope. Live revocation needs
  the existing task guard/cancellation pattern and future source-authorization port.
- Main model descriptors refresh per turn without exposing excluded tools or
  delegation profiles. A model-supplied unadvertised name is not trusted: the real
  executor still checks it and records a denied result.
- The child gateway captures the parent restriction, removes recursive delegation
  state, intersects any child list and cannot reenable a disabled parent. Ordinary
  tool schema/approval/output handling is not replaced by a gateway-local executor.
- Existing unscoped tasks retain their behavior. No production UI enables review
  tasks yet; plain projectless chat, source accounts and their credentials are
  unchanged. Account-specific authorization, HTTP and the PR UI/action chain remain
  unimplemented and cannot be inferred from passing scope tests.

#### Current validation

Candidate: `artifacts/pr-review-scope-20261018`, separate from frozen Git push.
Focused tool/workspace checks pass 18 cases; descriptor/gateway/adversarial-model
checks pass 21 cases after correcting the test to inspect the existing failed
tool-feedback JSON shape instead of assuming it contains an ErrorCode property.
The original one-failure test report remains. Focused counts overlap the following
regression and must not be added to its total.

Session 61763 is terminal with exit 0 and three Completed reports: 624 Tools,
145 Agent and 334 related App cases, totaling 1,103/1,103 without failures/skips.
The App scope includes projectless chat, subagents, approval and Git push. The
complete solution build in session 71551 passes with zero warnings/errors;
`solution-build.log` retains its output. The 326-file C# type/size/syntax audit
and `git -c core.autocrlf=false diff --check` pass. No native/source-account/API
acceptance is implied, and this is not all solution tests.

These changes follow the separate frozen Git push candidate's 3,607-case result;
that earlier total is neither rerun for this slice nor added to 1,103. Future
account/client/UI work requires its own validation. Do not rebuild or overwrite
the earlier candidate. The overall product goal remains active; no development
commit, push, release, user-window operation or real credential/API use occurs.
<!-- /source: TOOL_SCOPE_ACCEPTANCE.md -->


---

<a id="window-shutdown-acceptance"></a>

## WINDOW_SHUTDOWN_ACCEPTANCE.md

<!-- source: WINDOW_SHUTDOWN_ACCEPTANCE.md -->
### 窗口退出收尾验收

#### 2026-10-10 原生关闭与验收宿主收尾

上一轮原生窗口已经关闭，但临时进程仍在运行。本轮加入生命周期日志，实际复现
`Window.Closed -> Exit -> StartWithClassicDesktopLifetime 返回 -> 模型夹具释放`，
随后卡在 MCP HTTP 测试服务器释放。这不是 MossAgent 仍在执行任务的证据。
根因是测试服务器的异步取消/网络循环捕获了已经停止的 Avalonia 同步上下文。

修复只涉及 App.Tests 的两种 loopback 服务器：网络等待、取消和释放不再依赖 UI
上下文。没有更改生产窗口关闭、授权、审批或执行逻辑，也没有恢复 AOT。
四项确定性回归使用可记录的同步上下文和可控取消回调，覆盖从 UI/非 UI 上下文
创建两种服务器以及异步释放；旧实现四项均失败，修复后零次回投 UI 上下文。

本轮设计自检：

1. 行为归测试夹具和仓库外原生宿主，不增加产品模块依赖。
2. 复用已有模型/MCP 协议夹具、真实 App 组合根和 SQLite 存储端口。
3. 只使用独立临时目录、合成凭据和 loopback 服务；网络循环由夹具取消源控制。
   原生操作只发送合成请求并关闭窗口，没有点击批准按钮或更改安全设置。
4. 四个修改/新增手写 C# 文件均保持单类型、文件 <=500 行、方法 <=80 行。
5. 确定性上下文回归、真实工作区 MCP/取消/关闭测试与原生过程日志共同验证。

##### 普通权限原生结果

证据保存在 `%TEMP%/MossAgent-native-shutdown-20261010`，宿主和运行数据不进 Git。

- **浅色最大化 1920×1032**：实际点击发送，模型请求的非法参数被拒绝并完成第二轮
  回复；实际点击标题栏关闭后，窗口、应用生命周期及两个服务器均完成释放。
  存储核对为 `Completed / invalid_arguments`，唯一审计已完成、审批字段为空。
- **深色普通窗口 1280×820**：实际点击发送，合法 MCP 请求进入真实审批等待态；
  没有批准或拒绝该请求，而是点击窗口关闭入口。日志与 SQLite 核对为
  `Cancelled / tool_cancelled`，审计已完成、审批字段为空，远端工具从未调用。
- 两个进程均自行退出，无强制终止；日志均记录 `QA_ELEVATED: False`。
- Python 被动截图为 `native/completed-before-close.png` 和
  `native/pending-before-close.png`；图片非黑屏，后者记录真实审批等待状态，
  不是手工赋值给展示模型的弹窗。任务/工具状态和审计使用生产仓库端口核对。

定向 20 项通过，八个测试程序集共 4,243 项全部通过，无失败或跳过；App.Tests
的 3,170 项耗时 5 分 13 秒。解决方案构建零警告、零错误，四个 C# 文件的定向
空白格式检查与单类型/规模检查均通过。完整日志和 TRX 位于证据目录。
本轮不证明运行中的外部浏览器、ConPTY 进程、付费模型或所有 DPI 的退出行为，
也不证明界面已经达到 Codex 的全量像素一致；整个产品目标仍未完成。

#### 所属模块与约束

App 协调窗口关闭、已有任务取消、持久化和浏览器释放，不改变 Domain、
工具执行管线、授权根、审批规则或数据库结构。等待和失败状态复用共享弹窗，
外观只在共享 Styles 中定义。每个新增 C# 文件保持单类型和既有规模约束。

普通关闭先阻止新发送和新任务登记，保留 UI 消息循环，取消所有后台任务并等待
其执行、最终保存和浏览器释放；随后停止启动发现，再允许窗口关闭并释放服务。
此前已经流出的部分回复与终态一起持久化，不因取消源被释放而误判失败。
创建任务正在等待保存时，关闭会等待其结束；创建完成后只记录取消，不启动模型或工具。

收尾失败不显示原始异常或凭据，不自动退出；失败状态才提供“仍然退出”。
该操作是显式放弃正常收尾，不保证失败的存储操作已成功；重启不能自动重放中断任务。
强制终止进程、断电和操作系统强制结束不能通过普通窗口关闭协调器保证收尾。

#### 自动化结果

- 多个执行独立取消，任何一个尚未完成时关闭不能放行；停止后拒绝登记和执行。
- 某个取消回调抛异常仍会取消其他任务、等待全部执行并报告收尾失败。
- 已完成但失败的收尾不会从执行集合移除后被遗忘。
- 实际 loopback 模型请求暂停时执行 ShutdownAsync，真实 SQLite 保存取消状态；
  此后直接执行发送命令也不能再次发送或清空新草稿。
- 创建与关闭竞争时不调用提供商；终态保存失败向关闭协调器传播安全错误。
- 重复关闭只启动一次收尾；完成后排队关闭，避免在 Closing 回调内重入。
  失败保留窗口，只在显式执行失败状态的退出命令后关闭；重复命令不重复调度。
- 协调器释放后拒绝迟到完成并解除窗口订阅。
- 浅色、深色共享弹窗布局有界；等待状态隐藏 X 和退出按钮，Escape 不能关闭。

本轮定向 24 项通过，完整解决方案 1,430 项通过，构建 0 警告、0 错误。
当前仓库仍有 3 个预先存在的超长方法，本轮新增或修改方法没有超限。

#### 原生窗口与截图范围

仓库外宿主运行真实 App/MainWindow，普通权限和独立临时数据。
Python 仅被动截取 1920×1032 浅色最大化、1280×820 深色普通窗口及共享弹窗，
图片和日志位于 `%TEMP%/MossAgent-component-shutdown-qa-20261005-01`。
每个预览定时发起正常 Close，日志依次记录关闭请求返回、Window.Closed 和程序退出；
请求返回时窗口仍可见，说明不是先关 UI 再后台收尾。

等待/失败状态由验收宿主设置用于视觉检查；真实暂停请求与保存链路由自动化测试证明。
本轮没有原生鼠标、键盘退出验收，也没有在真实提供商或运行中的原生浏览器会话上
进行退出测试。不能将程序化状态截图视为这两类交互已验证。
<!-- /source: WINDOW_SHUTDOWN_ACCEPTANCE.md -->
