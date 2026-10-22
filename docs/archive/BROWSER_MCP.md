# 浏览器与 MCP

> 历史归档：保留原报告当时的结论、实验、失败与限制。整理于 2026-10-21。
> 当前能力与未完成方案统一维护在[功能清单](../FEATURE_STATUS_AND_IMPLEMENTATION_PLAN.md)；
> 原文中的计划、主机状态、测试数字及“正在进行”等措辞均为历史检查点。
> 代码块和仓库相对产物路径保留原意；本机忽略的 `artifacts/` 不随克隆分发。

[文档入口](../README.md)

## 原报告索引

- [浏览器产物会话安全](#browser-artifact-session-acceptance) — `BROWSER_ARTIFACT_SESSION_ACCEPTANCE.md`
- [Browser effect committed before host termination](#browser-committed-effect-recovery-acceptance) — `BROWSER_COMMITTED_EFFECT_RECOVERY_ACCEPTANCE.md`
- [External browser concurrency acceptance](#browser-concurrency-acceptance) — `BROWSER_CONCURRENCY_ACCEPTANCE.md`
- [Managed browser cancellation at profile creation](#browser-creation-cancellation-acceptance) — `BROWSER_CREATION_CANCELLATION_ACCEPTANCE.md`
- [浏览器运行验收](#browser-execution-acceptance) — `BROWSER_EXECUTION_ACCEPTANCE.md`
- [Browser operation cancellation and timeout](#browser-operation-cancellation-acceptance) — `BROWSER_OPERATION_CANCELLATION_ACCEPTANCE.md`
- [Managed browser host-process termination and explicit recovery](#browser-process-recovery-acceptance) — `BROWSER_PROCESS_RECOVERY_ACCEPTANCE.md`
- [Managed Chrome session ownership runtime check](#browser-session-ownership-runtime-acceptance) — `BROWSER_SESSION_OWNERSHIP_RUNTIME_ACCEPTANCE.md`
- [浏览器工具栏统一执行入口](#browser-toolbar-acceptance) — `BROWSER_TOOLBAR_ACCEPTANCE.md`
- [CDP settings-to-Agent conversation acceptance](#cdp-agent-workflow-acceptance) — `CDP_AGENT_WORKFLOW_ACCEPTANCE.md`
- [CDP task-page ownership acceptance](#cdp-task-isolation-acceptance) — `CDP_TASK_ISOLATION_ACCEPTANCE.md`
- [MCP 链路验收](#mcp-execution-acceptance) — `MCP_EXECUTION_ACCEPTANCE.md`
- [MCP reconnect and catalog recovery](#mcp-reconnect-recovery-acceptance) — `MCP_RECONNECT_RECOVERY_ACCEPTANCE.md`
- [MCP 工具身份与注册精简](#mcp-tool-identity-acceptance) — `MCP_TOOL_IDENTITY_ACCEPTANCE.md`
- [MCP settings accessibility and disconnected recovery](#mcp-ui-recovery-acceptance) — `MCP_UI_RECOVERY_ACCEPTANCE.md`
- [Queue browser cancellation and reconstruction check](#queue-browser-runtime-acceptance) — `QUEUE_BROWSER_RUNTIME_ACCEPTANCE.md`

---

<a id="browser-artifact-session-acceptance"></a>

## BROWSER_ARTIFACT_SESSION_ACCEPTANCE.md

<!-- source: BROWSER_ARTIFACT_SESSION_ACCEPTANCE.md -->
### 浏览器产物会话安全

#### 设计自查与计划

1. Browser 拥有截图/下载的产物编排，Tools.Abstractions 既有产物文件会话作为端口，
   Platform.Windows 提供原生实现，App 继续只负责注册。不会新增 UI 文件 I/O。
2. 截图 bytes 写入新会话；现有下载端口已返回 SDK 流，保持协议，通过有界只写流写入
   未完成的受控暂存会话，得到建议名称后复制到最终会话并 hash，不按 SDK 路径重开。
   这是为保留文件扩展名及现有下载协议所需的两阶段写入，不将网页名称作为目录授权。
3. Browser 工具保持原审批/能力及统一管线，全程只使用上下文显式绑定的会话；最终
   复核工作区和取消后保留。
   导出设 32 MiB 限额，与既有内置下载限额一致；不等于 SDK 网络或浏览器磁盘配额。
   中途失败按原句柄清理；SDK 临时文件通过原 IDownload 对象清理并反馈未知结果。
4. 会话、流限制、名称规则、下载传输和各工具分文件；每个 C# 文件一个类型，500/80
   行硬限制不变。无数据库变更，不自动重试或重放下载。
5. 先实现并补充代理 SDK/真实临时文件测试，再核对生产 App、模型请求/审批/审计与
   本地页面的真实 Chrome 截图/下载。验证拒绝、取消、超时、并发、输出大小及重载。
   Debug/Release 完整构建及相关回归后更新矩阵；GUI、崩溃、MXC 单列。

#### 已贯通的生产入口

- `BrowserScreenshotTool`、`BrowserDownloadTool` 由生产 App 注册的
  `IArtifactFileSessionFactory` 自动注入，继续经过标准 ToolExecutor 的能力、审批、
  取消/超时、输出和 SQLite 审计管线。审批拒绝或 ReadOnly 不进入 SDK、不创建文件。
  注入的会话拒绝时不会回退到路径实现；没有注入端口的兼容宿主仍使用旧路径分支，
  不能将新保证外推到这些宿主。
- `BrowserArtifactWriter.ScreenshotAsync` 在调用 SDK 前创建自有会话，拒绝空截图和
  超过 32 MiB 的结果。写入和 Complete 后再次复核工作区和取消，然后才 Retain。
- 下载既有端口本来就使用 `IDownload.CreateReadStreamAsync`，本轮保持接口。
  `browser-transfer` 会话仅作未提交暂存；接收流只写、不可 seek，并在每次写入前检查
  32 MiB。网页建议名称仅用作去控制字符、限制长度的显示名及 ASCII 扩展名提示，
  不决定存储路径。最终会话复制、hash、Complete 后仍保持所有权，暂存释放成功和
  最终复核通过后才 Retain；暂存清理失败会继续释放并清理最终文件。
- `PlaywrightDownloadTransfer` 只使用原 `IDownload` 和 SDK 流，不按 SDK 路径重开。
  成功尝试 Delete；失败尝试 Cancel，Cancel 失败也继续尝试 Delete。
  清理无法确认时返回本地化、有副作用标记的错误，SDK 原始错误不进入提示。
  `PlaywrightBrowserOperation` 在取消/超时后等待所属会话释放和 pending SDK 结束，
  保留下载清理未知信息，旧会话不能继续使用，不自动重试下载。
  若关闭会话本身也失败，仍等待 pending SDK 完成；有下载清理错误时保留其错误码并
  补充关闭未确认提示，否则给出独立的关闭未确认状态，不透出关闭异常原文。

#### 分层验证与证据

证据目录为 `artifacts/browser-artifact-safety-20261021/`，保留失败和后续成功记录，
不覆盖此前专项目录，也不将检查点结果视为新源码验证。

| 层级 | 实际覆盖 | 边界 |
| --- | --- | --- |
| Windows 原生临时文件，26 项专项 | 审批成功/拒绝、ReadOnly、错误任务根、junction、建议名、空截图、32 MiB 越限、完成后取消/超时/授权拒绝、真实暂存清理拒绝、两任务并发隔离 | 浏览器 SDK 为模拟会话；临时目录内核句柄是真实实现 |
| Playwright SDK 代理，新增 7 项 | 原流读取、禁用 PathAsync、源/目标错误、Cancel/Delete 拒绝、取消后 drain 和不复用旧会话、敏感错误不透出 | 代理调用计数，不是 Chrome |
| 取消关闭失败，新增 2 项 | 关闭失败仍 drain pending；关闭/下载清理同时失败不丢失任一安全提示；旧会话拒绝复用 | 直接运行生产 operation，注入确定性失败，不是关闭真实 Chrome 失败 |
| App 8 项 | 保存提供商/模型 → 本地 Responses 模拟服务 → Agent → 审批 → 原生产物文件 → 工具消息/审计 → 服务重建 → 文本/图片预览；拒绝/端口拒绝/取消；保留草稿和根外哨兵 | 浏览器会话替换为测试实现；图片预览为 Avalonia Headless，不是原生窗口 |
| 生产 App 组合 + 实际 Chrome CLI | 真实截图、文本和 4096 字节二进制下载、并发、取消、超时、旧会话拒绝、新批准恢复、SQLite 重载和内容/hash 核对 | 没有真实提供商或原生 Avalonia GUI；没有终止进程 |

App 首次图片预览失败（`App-first-results/`）；加入正确 Headless 环境仍失败
（`app-headless-results/`）。检查 PNG chunk CRC 确认原测试数据 IDAT 错误，改用有效
1×1 PNG 后 `app-valid-png-results/` 为 8/8。未修改产品 2 MiB、8M 像素和媒体校验。
Windows 专项为 `windows-complete-results/` 26/26；SDK 首批为 `sdk-results/`。
取消关闭失败补充后，一项旧断言仍要求原始 IOException，见 `final-Debug-Browser-results/`；
已改为核实安全错误码、有副作用标记及不泄露原文，`browser-corrected-results/` 为 113/113。
失败检查点不作为最终验证，最终记录使用 `verified-` 前缀。

实际 Chrome 在独立测试数据/资料目录运行，只关闭其自身两个任务的浏览器。
`real-chrome-first.log` 记录探针预期审计条数写错的失败；纠正探针断言后的
`real-chrome-accepted.log` 成功。随后增加实际超时，
`real-chrome-timeout-accepted.log` 记录 10 条已完成审计、4 个最终产物，
其中慢下载请求恰好两次（各一次明确取消和超时），没有恢复时重放旧请求。
8 个生产模块的加载 SHA256 与指定 App 输出逐个核对；环境记录在
`runtime-environment.json`（Windows build 22631、Chrome 155.0.8059.85）。
补齐关闭失败处理后，`real-chrome-final.log` 再次通过同样实际流程；加载的 Browser
模块 SHA256 为 `C1716E1A2EBB789A2E416A31B3526D4BB3B49F7075D994E81EC54A96AB05908B`。
这些实际 Chrome 检查使用 Debug 生产程序集，不是 Release 发布包验收。

实际超时结果携带“浏览器下载暂存清理未获确认”。原生受控任务产物没有留下额外文件，
但不能据此宣称 SDK 缓存已全部删除。二进制下载核对了内容/hash；当前预览限制不会
因此扩大，也不宣称所有下载格式都支持预览。

##### 最终冻结回归

`accepted-verification.json` 汇总最终 `verified-Debug-*` / `verified-Release-*`，
`review-verification.log` 保存复核过程。两配置完整解决方案构建均为零警告、零错误；
相关测试结果如下，每项无失败、跳过或中止：

| 范围 | Debug | Release |
| --- | ---: | ---: |
| Tools 全套 | 691 | 691 |
| Platform.Windows 全套 | 371 | 371 |
| Infrastructure 全套 | 795 | 795 |
| Browser 全套 | 113 | 113 |
| App 精选 | 424 | 424 |
| 每配置合计 | 2,394 | 2,394 |

App 精选涵盖浏览器设置/工具栏/嵌入式会话/产物，以及前轮读写、复制、移动、删除、
预览、快照审阅/撤销、审批和恢复。不是完整解决方案测试全套，不是发布包验收。
新增 43 项专项包含 26 Windows、9 SDK/operation 和 8 App；不与上表重复累加。

10 份最终 TRX 均已复核。两个配置的构建前后、配置之间及当前工作区的 2,595 个
源码/项目/脚本输入完全一致；最终真实 Chrome 的 8 个模块 hash 与当前 Debug 输出
一致。结构检查覆盖 2,402 个 C# 文件、21,448 个方法/访问器/局部函数/表达式，
文件最大 496 行、方法最大 78 行，单文件单类型和规模规则零违规。
没有数据库迁移、系统权限修改或正式发布；当前增量未提交。

#### 未完成范围和具体方案

1. **浏览器网络与缓存配额。** SDK `FailureAsync` 等待完整下载，32 MiB 只限制向
   受控文件导出；截图数组也在 SDK 完成后才检查大小。下一步分别验证 SDK 流式接口
   的背压与取消语义、任务资料目录配额和截屏尺寸预检。通过本地分块/无限流页面核实
   网络、缓存、内存、取消和多任务边界；未证明前不称作整体资源限额。
2. **SDK 清理未知状态。** 关闭会话和传输结束存在竞争，不能保证每次 Cancel/Delete
   都能获得确认。后续用持久阶段记录保存任务/原对象归属及未知状态，提供人工核实
   入口；重新批准后使用新会话，不能自动重试或按猜测路径删除浏览器缓存。
3. **崩溃与原子发布。** 文件 Retain 和 SQLite 审计元数据不是同一原子事务。
   应通过向前迁移记录创建、Complete、Retain、元数据发布阶段和持久根身份；分别在
   下载接收、Complete 后、Retain 后和审计前后终止独立宿主。恢复只显示中断/未知，
   核对引用、内容、草稿及另一任务，不重放网页点击或下载。
4. **授权代际与回收。** 目前复核 App 提供的工作区，仍信任配置的产物父目录和任务 ID；
   没有持久根身份或即时撤权代际。后续在端口/运行租约中传递授权版本，撤销时取消
   受影响任务；回收依赖数据库引用、运行租约和宽限期，并校验同一对象身份。
5. **其他入口与平台。** 本检查点未覆盖 Git 临时 diff 输入，其后续专用会话和证据见
   [GIT_DIFF_INPUT_SESSION_ACCEPTANCE.md](GIT_WORKTREES.md#git-diff-input-session-acceptance)，
   不属于本浏览器专项的验证范围；兼容宿主的旧分支不受本轮会话保护。
   原生 GUI、真实模型、日常/CDP/嵌入式组合、下载中进程崩溃及其他卷/
   大小写/长路径组合尚未验收。命令根目录逃逸阻断仍依赖受支持独立 Windows 的
   MXC 实现与验收，用户已明确环境尚未就绪；本轮未进行提权或系统变更。

整个产品目标保持进行中，本专项结果不代替产品功能清单验收。
<!-- /source: BROWSER_ARTIFACT_SESSION_ACCEPTANCE.md -->


---

<a id="browser-committed-effect-recovery-acceptance"></a>

## BROWSER_COMMITTED_EFFECT_RECOVERY_ACCEPTANCE.md

<!-- source: BROWSER_COMMITTED_EFFECT_RECOVERY_ACCEPTANCE.md -->
### Browser effect committed before host termination

Status: the original Debug and Release App assemblies pass one controlled
post-effect/pre-result crash timing. No production code changes in this check.
This extends schema-24 audit recovery evidence, not full-product completion.

#### Actual workflow

The independent executable uses production DI, Workspace Queue, Agent, standard
tool execution, AskEveryTime approvals and temporary SQLite. A local simulated
Responses service returns a navigation followed by a click. Actual managed Chrome
loads a generated form. The separately approved click submits a POST; the local
page server increments and flushes a synthetic counter, atomically publishes its
file, and withholds the response. The navigation audit is complete/successful;
the click audit remains incomplete. The controller independently reads counter=1
and its hash before terminating only its exact owned host.

All eleven bound descendants exit automatically; the controller does not kill the
tree to manufacture that result. Another separately owned Debug instance retains
its clicked page. A new executable opens the same target database and restores an
idle Interrupted task, the pending Queue entry and the unsent draft. The click
audit presents 已中断 / 执行结果未知 with the instruction to verify actual effects.
No model or page request occurs automatically. Explicit Queue continuation sends
only the remaining prompt using the saved model/medium reasoning.

The durable effect stays exactly 1, with unchanged file hash, after restart and
after continuation. Model requests are 3 before restart/continuation and 4 after
manual continuation; the form is loaded once. Three user messages remain unique
and ordered. Both audit identities and every original result/approval field are
unchanged; only the incomplete click gains its first InterruptedAt. The completed
navigation gains no interruption marker. Continuation does not rewrite that first
observation, fabricate success or replay the click. Actual approval was observed
before execution; its incomplete persisted approval field remains unknown.

#### Evidence

`artifacts/queue-browser-effect-crash-20261019` retains:

- Debug case `56ac81f91bb34ddf813b93341acb5e51`, `runtime.log` and `build.log`.
- Release case `eba8b78a3b3048dfa2f2c73c998e5fe9`, `runtime-release.log` and
  `build-release.log`. Both artifact executable builds have zero warnings/errors.
- Each case's controller result, bound descendants, before/after audit JSON,
  recovered presentation, durable counter, model/page statistics and eight
  actual loaded module hashes checked against its selected original runtime.
- Source provenance and structural review: twelve handwritten/linked C# files,
  one type each, no file/method size violation. No original source, configuration,
  tested Debug runtime, existing window, project or user profile is modified.

#### Limits

This is actual Chrome and process termination/restart, production composition,
local simulated model HTTP and a synthetic flushed page-server effect. It is not
native GUI or live AI/provider/platform acceptance. One POST form with a withheld
response does not establish all browser action/download/creation/transport
timings, embedded/daily/CDP modes, OS power loss or command filesystem isolation.
The application correctly retains uncertainty; this probe's independent counter
must never be used to infer arbitrary interrupted tool results in production.
The simulated model deliberately emits no new action for the remaining prompt;
this proves that composition does not replay the interrupted call. Whether a live
model proposes another operation after manual continuation is separate behavior.
<!-- /source: BROWSER_COMMITTED_EFFECT_RECOVERY_ACCEPTANCE.md -->


---

<a id="browser-concurrency-acceptance"></a>

## BROWSER_CONCURRENCY_ACCEPTANCE.md

<!-- source: BROWSER_CONCURRENCY_ACCEPTANCE.md -->
### External browser concurrency acceptance

#### Plan and design self-check

- Previous goal work made concrete progress: search projection/subscription
  changes and current 4813-case regression evidence. The wider goal remains active.
- This slice verifies a remaining runtime boundary: two concurrent agent tasks
  using real external Chrome sessions, cancelling one without affecting the other,
  and releasing owned browsers/drivers after task completion and app shutdown.
- Browser owns session/profile lifetime; App keeps its actual composition root,
  MainWindow, settings, task navigation and agent runner. Existing tool contracts,
  provider protocol and approval/audit/output pipeline remain authoritative.
- Use fresh synthetic SQLite, authorized project roots, managed profiles and a
  loopback-only model/page fixture. Never attach user Chrome/CDP or daily profiles,
  change safety policies through UI, use paid endpoints, or log credentials.
- The repository-external host observes public view-model state and owns its
  synthetic setup only. Real task start, switching and stop must use normal UI.
  Host fixture gates may release synthetic provider responses; they do not invoke
  browser tools, execute models or change task state on the App's behalf.
- First verify simultaneous browser/process ownership and isolated local storage;
  then cancel A, execute further B browser tools, complete B and verify cleanup.
  Preserve failures, screenshots, audits and exact scope; fix genuine failures
  with focused negative tests before repeating the affected runtime scenario.
- No production feature is added merely for QA. Any required change must stay
  within file/type/method limits and shared UI wrapper/style boundaries. Keep JIT
  debug, Windows x64 single-file release and all unrelated dirty work unchanged.

#### Native runtime evidence

On 2026-10-11, a repository-external ordinary-JIT host used the real App composition
root, MainWindow, browser settings and task controls at 1280 by 820 DIP. It reported
`QA_ELEVATED=False`; its build completed with zero warnings and errors. All pages
and synthetic provider responses stayed on loopback. No user database or daily
browser profile was opened.

- The real settings UI selected and saved Chrome. A read-only query confirmed
  `default_browser=1`, `profile_preference=0` (Managed), and no profile directory,
  CDP endpoint or proxy override. No security policy was changed through the UI.
- Real composer sends started A, then B without stopping A. Each task executed
  navigate, type, click, snapshot and screenshot through the standard tool pipeline.
  Both tasks remained Running with distinct Chrome/Node processes and managed
  directories named for their task identity.
- Same-origin pages displayed and stored `VALUE-A` and `VALUE-B` independently.
  The screenshot artifacts were actually opened and inspected, not merely hashed.
- The real A stop button cancelled A and released its Chrome/Node processes.
  B remained Running, using the same Chrome PID 20452 and Node PID 8244 with
  unchanged process creation times. No B restart or direct tool invocation occurred.
- Releasing only the synthetic response gate let B execute five more tools. Its
  second screenshot visibly showed `VALUE-B-FINAL` in the input and localStorage.
  A remained Cancelled. All 15 tool audits succeeded with approval granted; all
  three screenshot artifact lengths and SHA256 values matched persisted metadata.
- B completed through the normal runner, rendered its final reply and restored
  the send action. SQLite confirmed A Cancelled (5) and B Completed (3). No owned
  Chrome/Node descendants remained; the original four process IDs were absent.
- Request metadata recorded 6 A and 11 B turns, no invalid task route and no
  `VALUE-A` in any B request. Only route/value-presence flags were logged, not raw
  requests, API keys or headers.
- Normal host shutdown awaited `Workspace.ShutdownAsync()` and returned
  `QA_FINAL_ACTIVE=False`, `QA_EXIT=0`. The loopback fixture also exited normally.

Local, untracked evidence is under
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-browser-concurrency-20261011-145244`:
`both-running`, `a-cancelled`, `b-continued`, `b-completed` PNG/state/evidence sets,
three browser tool PNGs, bounded request flags and the isolated host build log.
The screenshots and Python/native QA helpers are not repository files.

#### Limits

- On A cancellation, the fixture's keepalive write threw Windows
  `ConnectionAbortedError` (10053), outside its narrow disconnect counter handler.
  Thus its final `disconnections=0` is not proof that A stayed connected, and no
  disconnect signal was produced. Cancellation, browser cleanup and B survival
  are established independently by App/SQLite/process/artifact evidence.
- This native scenario precedes the sidebar reconciliation refactor. It is not
  post-refactor browser runtime acceptance. The later Browser test project passed
  76/76 and the solution build passed with zero warnings/errors. Separate native
  sidebar evidence is recorded in [SIDEBAR_RECONCILIATION_ACCEPTANCE.md](UI_WORKFLOWS.md#sidebar-reconciliation-acceptance).
- No full-product completion, same-state Codex pixel equivalence, native multi-DPI
  acceptance, daily-profile/CDP validation or hostile-site compatibility is claimed.

#### Current production App acceptance: 2026-10-12

The latest ordinary-JIT `MossAgent.App.exe` uses its unmodified production
composition root, not the earlier repository-external QA host. Its separate build
includes the sidebar, context and incremental timeline changes. Assembly SHA-256:

- App: `13DA45B41CB17628E0D65DBE3BD4A6BE39E6F6AAEA3DBE82BBF082988E3E6F38`.
- Agent: `5E3AD0FF3FCB6D16ACBE44A77F0D015A67FEA92C480B098B3B06DB8690197E85`.

The already-live older synthetic App was closed normally before starting this
build; its fixture had received no model requests or tool executions. The same
new synthetic SQLite store and authorized project roots were retained. Both tasks
already had FullAccess and Chrome/Managed configuration, so no safety policy,
permission request, user database or daily browser profile was changed through UI.
All provider/page traffic stays at `127.0.0.1:51792`; request logging remains a
bounded whitelist of task/model/effort and value/root-presence flags.

##### Native execution and isolation

- Real composer sends start A with `concurrency-a`/medium and B with
  `concurrency-b`/high. Two independent stored conversation choices reach the real
  Responses adapter; the model fixture receives six A and eleven B requests.
- Both tasks simultaneously reach Running (1), each after navigate, type, click,
  snapshot and screenshot. Chrome roots 20580/24332 have separate task-named
  managed profiles; their Node owners are 23004/2320. The two same-origin screenshot
  artifacts visibly show independent `VALUE-A` and `VALUE-B` storage and inputs.
- The real A stop button transitions A to Cancelled (5). A's Chrome/Node disappear,
  while B remains Running with unchanged PID, creation time and managed profile.
  The fixture also records A's actual disconnect; its Windows
  `ConnectionAbortedError` branch is now handled rather than producing a traceback.
- Releasing B's synthetic response gate performs five further browser tools using
  those same process identities. Its third screenshot visibly shows
  `VALUE-B-FINAL` in the input and localStorage. A remains Cancelled throughout.
- B reaches Completed (3), renders its final reply and measured duration, collapses
  execution activities by default and restores the composer send state. All 15
  tool audits report success and approval granted. Three PNG lengths and SHA-256
  values match their persisted artifact metadata; all images were actually opened.
- B completion leaves no owned Chrome/Node descendants. Whitelisted request flags
  report no invalid route, foreign authorized root or A value in a B request.

##### Restoration, presentation and cleanup

- Normal UI close releases the production App. Reopening the same synthetic data
  restores A Cancelled/B Completed, the final reply and its unchanged duration.
  Clicking the real duration disclosure expands all ten B activities while the
  final reply remains outside them. Model requests remain 17, tool audits 15 and
  screenshot artifacts three: no model/tool/browser replay occurs.
- The restored App's token is read using its explicit current PID 24204 and confirms
  `elevated=false`. The external token-check script initially ignored its arguments
  and reread a stale identity file; that helper was corrected outside Git. Only
  the explicit-PID restored token result is used as direct elevation evidence.
- Passive Python captures record running, cancelled, continued and completed
  light-theme views at 1280x820/96 DPI, plus restored collapsed/expanded views at
  maximized 1920x1032/96 DPI. The helpers do not send UI input; native interactions
  use only the supported Computer Use API against MossAgent.
- Final normal UI close leaves the App, fixture and all original browser/driver
  PIDs absent; App stdout/stderr and fixture stderr remain empty. A supplementary externally
  attached `Get-Process` observer returned a null exit code, so zero App exit status
  is not asserted. Cleanup is established by process, task, audit and artifact
  evidence instead. Fixture final counts are A=6, B=11, pages=2, disconnections=1.

Evidence and helpers stay outside Git in
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-production-concurrency-20261012-195930`.
`runtime-identity-latest.json`, `permission-restored.json`, stage evidence JSON,
passive PNGs, three browser artifacts and `requests.jsonl` retain exact scope.
No production exception, new feature, schema change or packaging change was needed.
The older host evidence above remains historical, not a substitute for this run.
The current Browser suite passes 76/76 and Agent suite 119/119, including their
ownership, cancellation and context regressions. Another isolated solution build
passes with zero warnings/errors; repository and scoped whitespace checks pass.
This closes the current production two-task browser cancellation gate; it does not
prove full same-state Codex fidelity, dark/narrow state coverage, daily profiles,
arbitrary sites or the whole product objective.
<!-- /source: BROWSER_CONCURRENCY_ACCEPTANCE.md -->


---

<a id="browser-creation-cancellation-acceptance"></a>

## BROWSER_CREATION_CANCELLATION_ACCEPTANCE.md

<!-- source: BROWSER_CREATION_CANCELLATION_ACCEPTANCE.md -->
### Managed browser cancellation at profile creation

The artifact-only JIT probe under artifacts/browser-creation-cancellation-20261019
uses the original App outputs, production DI/ToolExecutor, temporary SQLite,
AskEveryTime and two separately owned tasks with actual managed Chrome. Its local
page counts only requests to the cancelled task's target path. No provider,
credentials, existing user profile/window or arbitrary browser attachment is used.

#### Boundary and observed behavior

A FileSystemWatcher requests cancellation when the previously absent task profile
directory appears, at the factory's profile-create/SDK-launch boundary. The
pending executor is awaited using an independent watchdog. Cancellation does not
simply abandon the caller's wait. The completed result must be tool_cancelled
after exactly one explicit approval, with no request reaching the target page.

Before explicit recovery, the parent verifies its worker's exact executable,
process and generated root. Native process inspection finds zero Chrome processes
with the cancelled profile. The other task retains one browser root and seven
auxiliary Chrome processes and still returns its clicked button "1" through the
standard approved pipeline. Its page/run is not closed by the other task's
creation cancellation.

The parent then signals explicit recovery. Manager release and reattachment use
the same task/profile. Four newly approved calls navigate once to the previously
unreached local page, read button "0", click, then read button "1". The final exact
audit counts are five for that task, including the one cancelled creation, and
four for the surviving task. All nine audits are complete and all nine calls had
explicit approvals. Both sessions close; no generated-profile Chrome process
remains after worker exit.

runtime-final.log and case-10891144f3c74619af78ee36c657b3bc contain the result,
checkpoint counts and module identities. All eight loaded modules match the
supplied original App directory, including App
258440CD42E0BA015C0A0CA49537A7B27BBF72308612ABEE50A07EBEE66CBD86 and Browser
FEE25149AE1ECFA6BC4D1CDB357282AD0E27FD18A91EE22B5B64264D5EF7882D.
Build has zero warnings/errors; original source/test/runtime files are not rebuilt.

#### Preserved failure and limits

runtime.log retains the first harness failure: it expected exactly one Chrome
process containing a profile path, but Chromium propagates that argument to its
auxiliary processes. Its checkpoint already records zero cancelled-profile
processes and eight survivor processes. The corrected check requires zero for
the cancelled profile and exactly one main browser root, counted separately from
auxiliaries; it retains the original script and manifest. No production behavior
or approval policy changed for this correction.

Browser retains ownership of creation/cleanup; the existing shared pipeline owns
approval, deadline/cancellation, output limits and audit. This adds a real runtime
schedule to the deterministic API-proxy creation tests. It does not identify every
SDK-internal creation instant, indefinite transport hangs, cancellation during an
already-completed network/page side effect, embedded/daily/CDP modes or native GUI
and Agent/Queue continuation. BROWSER_PROCESS_RECOVERY_ACCEPTANCE.md separately
checks host termination after completed tools. Neither establishes command
filesystem isolation, all browser recovery cases or full-product completion.
<!-- /source: BROWSER_CREATION_CANCELLATION_ACCEPTANCE.md -->


---

<a id="browser-execution-acceptance"></a>

## BROWSER_EXECUTION_ACCEPTANCE.md

<!-- source: BROWSER_EXECUTION_ACCEPTANCE.md -->
### 浏览器运行验收

#### 初次承载修复范围

本轮修复归属于 `MossAgent.App` 的 Windows 原生视图承载，不更改 Domain、Application
或 Browser 的依赖方向。复用 `AppEmbeddedBrowserView` 和共享 `AppBorder`，
不允许业务页面直接构造第三方视觉控件，也不添加页面独立样式。

浏览器工具继续经过原有授权、风险、审批、超时、取消、输出限制和审计管线。
本轮不修改这些策略、数据库结构、真实用户配置或付费提供商。
两个新增手写 C# 测试文件各声明一个类型，文件和方法均不超过工程硬限制。

#### 已复现原因

- 原先通过 `AppContentControl.Content` 动态承载时，外层有有效尺寸，
  但实际原生视图保持 `0×0`，没有完成 Measure 和 Arrange。
  库返回 Ready 不等于已经附加原生窗口；导航工具随后超时，没有请求本机页面。
- 改为共享 `AppBorder.Child` 后，原生视图获得布局，暴露出第二个独立故障：
  Windows `.NET Runtime` 日志记录 Avalonia 原生子窗口创建异常，
  明确指出需要应用清单中的支持系统列表；应用进程实际退出。
- Windows App 现在嵌入 `app.manifest`，声明 Windows 10/11 支持、
  `PerMonitorV2,PerMonitor` DPI，以及 `asInvoker`、`uiAccess=false`。
  不通过管理员权限规避问题；已检查实际生成的 apphost 包含这些清单内容。

#### 2026-10-05 普通权限运行复测

使用独立临时数据目录、合成项目与合成提供商、随机端口的 loopback HTTP 服务。
测试页面和模型响应均来自本机；MCP、代理和日常浏览器资料未启用。
已存在的合成验收任务使用 FullAccess，本轮没有通过界面修改审批或隐私设置。

从生产应用可执行文件打开检查器，输入合成任务后执行：

1. `browser.navigate` 导航至本机页面；服务实际收到页面请求。
2. `browser.type` 向 `#name` 填写 `MossAgent-native`。
3. `browser.select` 在 `#choice` 选择 `two`。
4. `browser.click` 点击 `#apply`。
5. `browser.snapshot` 返回实际页面状态 `MossAgent-native:two`。
6. `browser.screenshot` 保存实际 WebView2 页面 PNG。

六项结果均为成功，六条工具审计均已完成、无错误码。
生产窗口实际显示页面及结果；工具截图为 `345×754`、16,302 字节，
目视检查有标题、输入值、下拉选项和最终结果，不是全黑或空白图。
SQLite 只记录 PNG 路径、长度和 SHA-256 等元数据，图片保存在任务 artifact 目录。

验收目录为仓库外 `%TEMP%/MossAgent-native-browser-qa-5j7cralk`，
包含 `events.jsonl`、`native-browser-live.png` 和任务截图。
Python 使用 `--passive --include-popups` 截取 `1920×1020` 的真实窗口像素，
未缩放、点击或修改窗口。脚本和图片不加入仓库。

模型流在检查期间保持打开，允许确认活跃的 WebView2 进程和页面。
释放检查等待后，任务持久化为 Completed，界面显示会话已关闭，
该隔离数据目录对应的 WebView2 进程不再存在。
随后正常关闭预览与本机服务，不让验收进程占用后续构建输出。

#### 自动化回归

- `EmbeddedBrowserHostingTests` 覆盖三种尺寸、动态附加、缩放和移除，
  以及业务 AXAML 使用直接共享宿主并保留空状态。
- `WindowsManifestTests` 覆盖清单嵌入声明、兼容系统、普通权限与 DPI 设置。
- 与浏览器设置运行链路一起执行的定向测试 10 项通过。
  布局测试不创建实际操作系统窗口，不能单独证明 WebView2 可用。
- 当前完整解决方案 1,101 项测试通过，其中 App 750 项；解决方案构建
  0 警告、0 错误。两个新增测试文件通过单类型、文件规模和方法规模检查，
  浏览器 AXAML 为 38 行；`git diff --check` 通过。

#### 2026-10-05 任务归属与常驻共享宿主

- 原有单例浏览器面板会被后台任务覆盖；隐藏检查器还会阻止新原生视图获得
  首次附加和布局。现在 `BrowserLaunchOptions.TaskId` 传递实际任务归属，
  App 共享工作区维护每个会话独立的面板、标签页和原生视图。
  选择任务只切换呈现，不主动启动浏览器；旧会话释放不能移除替代会话。
- `AppBrowserHost` 常驻窗口根布局，`AppBrowserViewport` 提供当前显示矩形。
  所有视图始终附加并获得非零布局，后台视图在窗口范围外停放，禁止命中和焦点。
  隐藏检查器、打开设置和切换任务不通过重建或重新挂载视图实现。
- 检查过实际 NativeWebView 12.0.4.9 与 Avalonia 12.0.4 的原生承载代码。
  Windows GPU/offscreen 模式的帧源返回合成图，不能用它替代真实网页截图。
- 所有权自检：原生承载归 App，任务 ID 路由归 Browser 和既有工具契约，
  依赖方向不变。复用会话工厂和标准工具管线，不添加数据库迁移、
  新的授权、审批、超时或输出策略。新增文件均为一个类型且符合规模硬限制。

##### 普通权限双任务运行

使用仓库外 `%TEMP%/MossAgent-native-browser-qa-uwoopb9_` 的独立合成数据，
启动隔离通过 `MOSSAGENT_DATA_DIR`，不是应用不识别的 `--data-root` 参数。
随机 loopback 服务提供测试页面和模型响应，不调用付费服务或真实账号。

- 检查器默认隐藏时，A 和 B 各完成 14 项真实浏览器工具，包括导航、
  填写、选择、点击、快照、截图、创建、切换和关闭标签页；28 项均成功，
  工具审计全部完成且无错误码。
- A 的模型流保持打开，随后运行 B；两次运行重叠，两个 WebView2 会话和
  任务资料目录同时存在。没有证明两条工具链逐步交错执行。
- 页面分别保留 `TASK-A:two` 和 `TASK-B:two`，切换所选任务后工具栏和页面
  归属正确。取消 A 后其父进程消失，B 的页面和进程继续存在。
- 打开外观设置时网页没有覆盖设置页；切换深色主题并返回、恢复
  1280×820 普通窗口后，B 页面状态仍保留。释放 B 后会话关闭，
  最后正常关闭预览和本机服务；对应的 WebView2 进程均已结束。
- 两张工具 PNG 均为实际网页内容、1024×768，分别 14,598 和 14,529 字节。
  SQLite 保存产物元数据，PNG 留在任务 artifact 目录。
- Python 被动窗口截图包含 `task-a-live.png`、`task-b-live.png`、
  `task-b-after-a-cancel.png`、`settings-with-background-browser.png`
  和 `task-b-dark-normal.png`；最大化窗口为 1920×1032，普通窗口为
  1280×820。脚本和图片不加入仓库，截图不拉伸。

另一次 `%TEMP%/MossAgent-native-browser-qa-4kyxbliu` 验收在首次隐藏检查器
期间完成 14 项工具，展开检查器后仍显示 `MossAgent-native:two`。

##### 自动回归与审批呈现边界

- 任务隔离、布局和标签页测试不初始化操作系统浏览器，验证后台任务不抢占
  所选面板、隐藏时非零布局、停放和命中、独立释放及标签关闭后的常驻承载。
- 审批等待时共享宿主停放网页但不释放会话，防止原生子窗口覆盖审批层。
  协调器在 UI 线程更新最终呈现状态，释放时只解除订阅。
  六项新增用例覆盖批准、拒绝、取消、队列、创建窗口前已有审批及后台线程释放。
- App 定向 62 项和任务会话管理 6 项测试通过；串行全量 1,172 项通过，
  其中 App 820 项。解决方案构建 0 警告、0 错误。
- 上述真实网页截图先于最终审批状态订阅修复；审批遮挡保护只完成结构和
  状态回归，没有在真实窗口中触发审批并重新截图，不把两者混为运行证据。

#### 2026-10-05 共享工具栏与导航事件

- `AppBrowserToolbar` 统一地址双向绑定、Enter 导航和返回、前进、刷新、开发工具
  命令，地址与操作分为两行；图标按钮、地址字段和标签选择器尺寸来自共享变体。
  240/277/345/600 宽度布局及组件内容所有权拒绝用法通过回归。
- 原生验收发现历史导航后地址与加载状态不更新。实际 NativeWebView 12.0.4.9
  转发后台实现作为导航事件 sender，旧代码把它当成原生控件而忽略事件。
  现在订阅捕获所属控件，卸载解除对应委托；后台事件、历史导航、替换/卸载后的
  旧事件与重复附加四项回归均通过，不放宽任务和标签页归属检查。
- 最终构建在 `%TEMP%/MossAgent-native-browser-qa-aun12f7c` 中使用同一个
  loopback 服务与独立合成数据重新执行 14 项工具，全部成功，无付费调用。
  Enter 导航实际请求 `/page?task=A&manual=final-enter`，返回恢复原地址，
  前进恢复编辑后地址，三次操作后均显示“页面加载完成”。
- Python 被动截图 `toolbar-final-back-dark.png`、`toolbar-final-forward-dark.png`
  和 `toolbar-final-normal-dark.png` 覆盖最大化及 1280×820 普通窗口，工具栏
  不再溢出；长地址在输入框内横向编辑，而不是撑开操作行。
  `selected-third-final-dark.png` 确认第三个选中会话标签滚动到完全可见位置。
- 合成模型的 300 秒检查等待结束后释放会话；浅色最终截图记录关闭后的空状态。
  预览正常关闭，服务已停止，相关 WebView2 进程已结束，没有为延长观察而重启服务。
- 工具栏绑定测试使用隔离 Headless UI 线程，避免 Avalonia 12.1.2 的首个
  调度线程与 xUnit 工作线程不一致造成未处理绑定。保留 Enter、双向绑定和
  实际附加后的按钮禁用断言；这不是原生网页截图或运行验收的替代品。
- 最终解决方案构建 0 警告、0 错误；串行全量 1,203 项测试通过，App 851 项。

#### 2026-10-06 共享面板位置与原生审批呈现

##### 设计自检与先红后绿回归

1. 位置同步归 App 共享呈现组件，依赖方向不变，不在 ViewModel 中计算偏移。
2. 复用 `AppWorkspacePanes`、`AppPaneSplitter`、`AppBrowserHost` 和现有会话契约。
3. 不改变授权、审批、超时、取消、输出限制或持久化；验收仅使用独立合成数据。
4. 修复在已有视口中订阅祖先属性，脱离时解除；新测试独立一类一文件，不超过规模限制。
5. 覆盖拖动、缩放、收起重开、审批阻塞时缩放、祖先隐藏及脱离重附加，包括明暗主题。

新增的明暗主题收起重开回归在修复前均失败：预期原生视图
`728,0,872,820`，实际停留在 `1284,58,872,820`。原因是视口自身的局部
Bounds 未变，而祖先已从浮动卡片移到停靠列；只观察视口 Bounds 和
LayoutUpdated 不足以反映这一位置变化。`AppBrowserViewport` 现在观察祖先
Bounds、IsVisible、RenderTransform 和 RenderTransformOrigin，不放宽布局断言。

##### 普通权限原生复测

仓库外 `%TEMP%/MossAgent-native-browser-qa-oubxn86r` 使用独立 SQLite、合成提供商
和随机 loopback 端口。临时原生宿主复用真实 App 资源、组合根和主窗口，
利用已有测试友元程序集，不修改生产可见性。`QA_ELEVATED: False`。

- 检查器初始隐藏，A/B 各通过模型和标准工具管线完成 14 项浏览器工具；
  按 call ID 去重后 28 项全部成功，SQLite 中 28 条对应审计全部完成、无错误。
  A 模型流保持打开时执行 B，两个会话同时存在；不声称工具链逐步交错。
- 浅色 1280×820 窗口中，B 原生视图实际矩形从 `837,181,428,580`
  随真实分隔条向左拖动 100 DIP 变为 `737,181,528,580`。收起时停放在
  `-560,-612,528,580`，不能命中；重开后恢复原位置及 `TASK-B:two` 页面状态。
  两个原生视图始终由同一宿主保留，没有重建会话。
- 任务 C 通过真实 `IToolExecutor` 发起 AskEveryTime 文件写入审批。等待期间
  原生浏览器停放，Python 被动窗口截图确认审批卡片和按钮没有被 WebView2 覆盖。
  验收宿主仅取消请求 token，不自动点击安全决策；结果为 `tool_cancelled`，
  审计已完成，`must-not-be-written.txt` 不存在，同一 B 视图和页面恢复。
- 切换到 A 确认 `TASK-A:two`，实际点击停止 A 后原生视图数从两个变为一个，
  A 所属 WebView2 父进程消失；再选 B 仍显示原页面和 `TASK-B:two`。
- `capture-1.json` 工具 PNG 为 428×580，拖动后五张工具 PNG 均为 528×580；
  长度和 SHA-256 均与产物元数据一致。取消审批后的 capture-5 与取消 A 后的
  capture-6 哈希相同，目视为真实页面，不是黑屏。capture-4 在审批取消前生成，
  不将它作为取消后的证据。窗口 PNG 保留在同一临时目录，脚本和图片不加入仓库。
- 预览到达 12 分钟预设时限，验收宿主记录 TaskCanceledException 后执行 ShutdownAsync
  并关闭窗口，App 退出代码为 0；没有为延长观察而重启。loopback 服务通过停止信号
  结束，已复核两个验收进程和该数据目录对应的 WebView2 进程均不再存在。

##### 最终自动检查与范围

- 新增五项布局回归及既有承载、任务隔离、审批呈现与分隔条测试，定向 31 项通过。
  Headless 使用共享表面代替系统浏览器，不以此代替原生截图。
- 完整解决方案 2,007 项通过，其中 App 1,518 项，无失败或跳过；构建 0 警告、0 错误。
  904 个 C#/AXAML 文件的文件、方法和单类型规模审计无违规；修改的 C# 空白格式检查通过。
- 本次原生复测仅浅色 1280×820；明暗主题缩放断言来自 Headless。真实审批/拒绝按钮
  未通过 Windows 自动化点击，多 DPI 和真实网站兼容性仍未完成，不声称 Codex 像素一致。

#### 尚未证明的范围

- 大量任务、快速切换标签页及异步标签刷新竞争的压力验收。
- 导航失败及真实审批/拒绝后页面恢复的完整运行验收；遮挡保护和取消恢复已如上复测。
- 日常资料、外部 CDP、代理、开发工具、内置浏览器下载和各 DPI 下的完整运行验收。
- 超长地址、超长标签以及最小窗口和多个 DPI 组合的完整视觉验收。
- 原生网页可访问树仍需完善，活跃原生视图期间部分窗口状态不返回可访问树。
- 本机合成验收成功不代表真实网站兼容、全产品可用或 Codex 逐像素还原完成。

#### 2026-10-07 外部 Chrome 与取消所有权

##### 设计边界与先红后绿

1. SDK 资源所有权归 Browser 工厂和会话；App 仍通过既有组合根使用它们。
2. 复用 `IBrowserSessionFactory`、`IBrowserSession` 和既有任务会话管理，不添加新端口。
3. 不修改授权、风险、审批、工具超时、输出限制或审计。验收只用隔离资料和本机服务。
4. 新手写测试文件各一个类型；生产工厂和会话继续符合文件、方法规模限制。
5. 覆盖预取消、晚到 runtime/上下文/页面、SDK 失败、清理等待及借用会话的非破坏性释放。

`Playwright.CreateAsync().WaitAsync(token)` 会在取消时丢失晚到 runtime 的所有权。
独立生产工厂 probe 使用已取消 token：修复前返回取消后仍有一个所属 Node 驱动存活；
修复后不创建驱动或资料目录。初始化测试先复现失败，再验证取消 token 身份和资源回收。

进一步发现 launch/CDP 的 `WaitAsync` 同样会提前结束等待，不能证明晚到的资源已回收；
原先上下文 `CloseAsync` 抛异常时也跳过 runtime Dispose。新增 11 项测试在修复前
6 项失败、5 项通过；补足成功交接、SDK 原始失败与清理等待后共 15 项全部通过。
与初始化、策略、惰性会话和任务管理一起执行的定向回归共 40 项通过。

工厂现在观察不可取消 SDK 操作的实际完成，再检查取消并回收拥有的资源。启动失败或
取消会关闭自己的上下文；连接已有 CDP 只断开自己的 runtime，不关闭已有浏览器、
上下文或页面。取消期间新建且尚未交接的页面单独回收。会话的 runtime Dispose 放在
finally 中，上下文关闭失败仍释放驱动并保留失败结果。普通 Debug/Release 均为 JIT，
测试代理也使用普通运行时接口代理，不引入 AOT、JSON 源生成或编译绑定迁移。

##### 更新后普通权限真实链路

使用真实 App、组合根和 MainWindow 的临时宿主，独立 SQLite、合成项目、合成任务，
随机 loopback 端口提供页面及模型响应。没有真实账号、付费请求或用户浏览器资料。
`QA_ELEVATED: False`。已有合成任务预设 FullAccess，不通过自动化修改安全或隐私设置。

第一次实测完成后正常关闭宿主与服务；所有权修复后重新构建宿主，并用新的独立数据
重跑以下链路，未因观察超时而重启仍运行的进程：

- 从实际浏览器设置页把 Embedded 改为 Chrome，保留 Managed，点击保存并看到成功提示。
  独立 SQLite 确认 `default_browser=1`、`profile_preference=0`，资料/CDP/代理字段均为空。
- 返回真实会话，输入合成请求并点击发送。七项标准管线工具依次为 navigate、type、
  select、click、snapshot、download、screenshot；SQLite 七条审计全部成功、已完成、无错误码。
- 真实 Chrome 页面为 `MossAgent-JIT:two`。下载为 31 字节的合成文本，建议名称
  `external-probe.txt`，SHA-256 为 `C5BC70D7FF54D0AB8F901D21D2B8A57DB27671F7FB0A87E7BDE495D38DB29704`。
  页面截图及下载保存在任务 artifact 目录，SQLite 仅存路径、大小、媒体类型与哈希。
- 模型流在第八轮等待时检查运行状态、工具完成列表和停止按钮。释放等待后，任务持久化
  为 Completed，实际窗口显示最终回复，停止按钮恢复发送状态，所属 Chrome/Node 不再存活。
  正常停止宿主与本机服务后分别记录 `QA_EXIT_COMPLETED: 0`、`FIXTURE_EXIT`。

仓库外证据根目录为 `%TEMP%/MossAgent-external-browser-lifetime-b713d9838d1f4effb84f1c72d4e7109b`；
更新后复测在其 `after-ownership` 子目录。包括设置保存截图、真实窗口/页面 PNG、
SQLite 审计、请求计数、构建和测试日志。临时脚本、数据库和截图不加入仓库。

##### 视觉检查与剩余范围

- 最终 Debug/Release 解决方案构建均 0 警告、0 错误；完整 Debug 回归 2,618 项全部通过，
  无失败或跳过。六个相关 C# 文件的空白格式检查及相关 diff 检查通过；1,015 个 C#/AXAML
  文件的单类型、文件规模和方法规模审计无违规。未提交、推送或重新发布。
- 第一次运行实际检查浅色 1280×820、深色 1280×820、深色 996×820，以及窄窗口打开检查器。
  运行列表、输入框、停止/发送按钮保持可见；打开检查器时浮动上下文卡片隐藏。
- 更新后再次检查设置保存、浅色运行会话和最大化 1920×1032。使用 Python 被动 PrintWindow
  同时保存 Codex 参考窗口；不自动操作 Codex，不通过模拟图代替实际像素。
- 对照图仅裁剪参考窗口的系统外框，不缩放像素。两边会话内容和检查器状态不同，
  局部外观核对不能证明全部组件逐像素一致，也不能代替多 DPI 的完整验收。
  1920×1032 客户区截图的 rail、侧边栏和对话背景采样分别匹配 RGB
  `(238,244,249)`、`(249,251,253)`、`(255,255,255)`；标题栏采样均值相差小于 0.04。
  保留未缩放的 `shell-comparison.png` 和记录采样矩形的 `shell-comparison.json`，
  不把不同内容区域的差异归零或宣称整窗一致。
- SDK 不接收这些调用的 CancellationToken。取消会保持所有权并等待 SDK 交接和清理，
  不承诺停止立即返回；SDK 卡死/断链压力场景尚未完成真实运行验收。
- 外部 CDP、日常资料和失败关闭用可控接口回归验证边界，不声称真实用户浏览器或网络
  故障已实测。真实网站、多任务外部浏览器并发及各 DPI 组合仍需继续验收。
<!-- /source: BROWSER_EXECUTION_ACCEPTANCE.md -->


---

<a id="browser-operation-cancellation-acceptance"></a>

## BROWSER_OPERATION_CANCELLATION_ACCEPTANCE.md

<!-- source: BROWSER_OPERATION_CANCELLATION_ACCEPTANCE.md -->
### Browser operation cancellation and timeout

#### Confirmed production failure

The artifact-only CLI under artifacts/browser-timeout-runtime-20261019 uses
production App DI, ToolExecutor, AskEveryTime, explicit approvals, temporary SQLite
and its own actual managed Chrome profile. A local deterministic page withholds
the target button until a click has returned tool_timeout. The old Playwright
WaitAsync cancellation only ends the caller's wait: releasing the button then
executes the old click and changes the real page to "late clicked".
runtime-baseline.log retains that failure, completed audits and eight original
module hashes. No user browser, credentials, provider or existing file is used.
The baseline source copies are preserved beside the log.

Twelve deterministic API-proxy cases independently fail before the change:
cancelled click/fill/navigation/snapshot leave the owned runtime open, and an
already-cancelled token still enters the SDK. These are simulated SDK fixtures,
separate from the actual Chrome reproduction.

#### Change and ownership

Browser owns PlaywrightBrowserOperation. Every public session operation now
checks cancellation/disposal before entering the SDK. On cancellation or pipeline
deadline, it invalidates the task session, awaits existing owned-resource cleanup
and observes the original pending task before returning. It does not detach a
live action by cancelling only its wait. Disposal remains idempotent and closes
only the managed context or task-owned CDP pages/popups before disconnecting its
runtime; it does not close a borrowed user context or another task's connection.
The standard executor still owns approval, timeout, bounded results and audits.

Normal operation failures retain their original failure. Cleanup failures are
observed after runtime disposal and pending-task observation; they are not
converted into successful cleanup. A stale session is refused. Explicit release
and a new task-session attachment use the existing manager/factory recovery path.
There is no automatic replay or replacement inside a failed tool call.

The seven-file guarded integration includes this helper/session, cancellation
fixtures, two existing CDP ownership tests adapted to require invalidation while
retaining other user/task pages, and the separate queued-write audit assertion
fix from QUEUE_TOOL_AUDIT_ASSERTION_ACCEPTANCE.md. Baseline hashes and original
file backups are in integration-manifest.json and baseline/. No migration, new
module dependency, process launcher, UI appearance or approval-policy change.

#### Current evidence

The independent candidate passes Debug/Release complete solution builds with zero
warnings/errors, each configuration's complete Browser 104 and relevant App 212
checks. Sixteen added cases cover owned/borrowed task pages, four operation shapes,
pre-cancel refusal, awaited cleanup, stale refusal and cleanup/ordinary failure.
An initial expanded run retains fourteen failures: eleven missing copied publish
script fixtures, two old post-cancellation reuse expectations and one deliberate
cleanup failure rethrown by test teardown. The script was copied unchanged, the
two expectations now require invalidation plus user/other-task preservation, and
the deliberate teardown error is separately asserted and marked expected.
Debug-browser-final and Release-browser retain the complete passing runs.

runtime-candidate.log confirms actual Chrome returns tool_timeout without a late
click, refuses the stale session and permits an explicitly reattached session's
new approved click. All seven calls have completed task audits and approvals;
eight loaded module hashes match the supplied independent App directory.
queue-runtime-candidate.log repeats Queue/Agent navigation cancellation with a
concurrent task's exact approval/run/draft intact and manual reconstruction, using
simulated model HTTP and actual Chrome. The structural check covers nineteen
selected candidate/harness C# files and 110 methods/accessors, maximum 56 lines,
one type each and no violations.

Original regression 55845 has now terminated: 8,122 total, 8,121 pass, one queued
audit assertion failure, no skips/aborts. All eight reports and 2,411 source/
configuration plus 2,035 Debug runtime paths/hashes match. The old failure is
retained and its freeze ended before seven explicit files were merged with zero
baseline conflicts. Original-checkout Debug/Release builds pass with zero warnings
and errors, and each complete Browser suite passes 104 checks. Related App checks
pass 212 in Release; the first Debug run passes 211 and fails one Queue agent
snapshot completion assertion. That failed report is retained and investigated
separately in QUEUE_SNAPSHOT_COMPLETION_ACCEPTANCE.md; no full-pass claim is made.

runtime-original.log now repeats the actual Chrome timeout/stale-session/explicit
recovery workflow against the original App assemblies: no late mutation, stale
refusal, successful newly approved recovery and seven completed task audits.
queue-runtime-original.log also repeats Queue/Agent cancellation, other-task
approval/draft preservation and manual service reconstruction. Module hashes in
both records match the supplied original directory. The first harness rebuild
failed because its default compile glob included baseline C# backups. The harness
now excludes baseline/**/*.cs without deleting those backups; both the original
failure log and corrected build/runtime records remain preserved. These are CLI
checks with real Chrome, separate from native GUI and paid-provider acceptance.

The subsequent guarded test-only completion-observation correction passes both
original configurations' full builds and expanded App 212 checks. Complete Debug
regression 93473 under artifacts/browser-cancellation-integrated-20261019 has ended:
8,136 passed / 2 failed, 8,138 total, no skipped/aborted tests. Eight reports and
2,414 source/configuration plus 2,035 runtime identities match; the freeze has
ended. The two preserved observation failures and guarded test-only fixes are
documented in RUNTIME_OBSERVATION_BOUNDARIES_ACCEPTANCE.md.

#### Limits

This prevents a still-pending SDK action from continuing after reported completion
of cancellation/timeout cleanup. It cannot roll back a page action or network
request already completed before cancellation. Cleanup drains SDK operations;
creation/transport hangs and process-crash recovery remain separate acceptance
work. Production CDP creates task-owned pages; arbitrary user-page attachment is
not authorized by this change. Stale-session refusal currently uses tool_failed;
a more specific localized manual-recovery hint remains an interaction gap.
Native GUI, daily-profile/CDP live combinations and real AI providers are not
validated by these CLI/proxy checks. Command filesystem isolation remains an open
safety blocker; the whole product goal is not complete.

BROWSER_PROCESS_RECOVERY_ACCEPTANCE.md subsequently adds one bounded actual
host-process termination/restart case after completed browser operations. It
establishes automatic cleanup of the eleven bound managed-browser descendants,
independent-instance preservation and explicit newly approved recovery for those
module identities. Creation/in-flight-side-effect crash and native/Agent recovery
remain outside that check.

[AUDIT_INTERRUPTION_RECOVERY_ACCEPTANCE.md](TERMINAL_TOOLS.md#audit-interruption-recovery-acceptance) subsequently adds actual production
Queue/Agent host termination during a held navigation and manual continuation in
a new executable. Schema 24 preserves uncertain audit results and presents an
explicit interrupted state; it does not determine whether a remote side effect
already occurred or establish other browser/native/live-provider combinations.
<!-- /source: BROWSER_OPERATION_CANCELLATION_ACCEPTANCE.md -->


---

<a id="browser-process-recovery-acceptance"></a>

## BROWSER_PROCESS_RECOVERY_ACCEPTANCE.md

<!-- source: BROWSER_PROCESS_RECOVERY_ACCEPTANCE.md -->
### Managed browser host-process termination and explicit recovery

#### Controlled actual-runtime check

artifacts/browser-process-recovery-20261019 builds an artifact-only JIT executable
against original App outputs without rebuilding the frozen source/test/runtime.
Two owned worker processes use independent generated roots, temporary SQLite and
managed Chrome profiles, direct browser settings and local deterministic pages.
Production App DI, ToolExecutor and AskEveryTime approvals remain active. No
provider, credentials, existing user profile/window or arbitrary CDP port is used.

Before termination, the target performs approved navigation, one real DOM click
and a snapshot that reads button "1". All three audits are complete. Its one user
message, task/project identity, managed profile and eight module hashes are saved.
The parent validates the exact new executable/profile and binds handles to eleven
descendants before killing only its own host process. This differs from killing
the whole process tree, which would not test automatic descendant cleanup.

#### Observed result

runtime.log and case-159b4dfc91ac4321803f48569fbcb6e5/result.json record:

- All eleven bound target descendants exit within five seconds. No surviving
  descendant requires diagnostic cleanup before recovery.
- The other owned worker still returns its exact clicked page through a newly
  approved snapshot and then closes gracefully.
- A new executable process, using the same saved target data, restores the task
  as Interrupted and idle. Its original three completed audit CallIds and one
  user message remain unchanged before manual action.
- Four explicitly approved manual operations recreate the task's browser from
  its saved managed profile: navigate to a new unchanged page, snapshot button
  "0", click, and snapshot button "1". Seven audits in total are complete; the
  user message is not duplicated. Explicit cleanup releases the recreated session.

The recovered record verifies all eight loaded module hashes against the supplied
original App directory. App is
258440CD42E0BA015C0A0CA49537A7B27BBF72308612ABEE50A07EBEE66CBD86;
Browser is FEE25149AE1ECFA6BC4D1CDB357282AD0E27FD18A91EE22B5B64264D5EF7882D.
The harness build has zero warnings/errors; failed stages would remain as logs
with bounded finally cleanup restricted to owned workers and bound handles.

#### Ownership and limits

Infrastructure already owns startup interruption; Browser owns managed sessions
and Playwright cleanup. This check changes no production port, policy, dependency,
schema or behavior. It adds actual process evidence to
BROWSER_OPERATION_CANCELLATION_ACCEPTANCE.md and QUEUE_BROWSER_RUNTIME_ACCEPTANCE.md.

This is a direct-tool CLI host-process restart, not native desktop interaction or
Agent/Queue continuation. The host is terminated after completed tool audits;
termination during browser creation or an unfinished page/network side effect is
not tested. It does not establish embedded WebView2, daily-profile/CDP recovery,
manual takeover, transport hangs, boot-time orphan handling after OS/power failure,
network confinement, command filesystem isolation or full-product completion.
Complete original regression 93473 has ended with 8,136 passed / 2 failed, 8,138
total and unchanged source/runtime identities. The freeze has ended; this probe
does not erase those failures. AUDIT_INTERRUPTION_RECOVERY_ACCEPTANCE.md adds a
later production Queue/Agent process restart during a held real-Chrome navigation,
including explicit unknown audit state and no replay before manual continuation.
<!-- /source: BROWSER_PROCESS_RECOVERY_ACCEPTANCE.md -->


---

<a id="browser-session-ownership-runtime-acceptance"></a>

## BROWSER_SESSION_OWNERSHIP_RUNTIME_ACCEPTANCE.md

<!-- source: BROWSER_SESSION_OWNERSHIP_RUNTIME_ACCEPTANCE.md -->
### Managed Chrome session ownership runtime check

Status: a controlled CLI check passes through production App composition,
ToolExecutor, SQLite audit and actual managed Chrome/Playwright. It does not
exercise Queue/model calls, Avalonia windows, native keyboard/pointer interaction,
embedded WebView2, daily profiles or an existing browser/CDP attachment.

The separate JIT harness under artifacts/browser-ownership-runtime-20261019
references existing original-checkout assemblies; it never rebuilds src/tests.
It saves Chrome/Managed/explicit-direct browser settings in generated temporary
data, creates two persisted task identities and explicitly attaches their own
manager-created sessions. Only a bounded local HTTP page is used. No credentials,
existing tabs or user profiles participate. Cleanup releases only these sessions.

#### Established behavior

- Both sessions navigate through the shared pipeline and show their own page.
- Task two creates a second tab; task one's real tab ID is refused by task two's
  browser.switch_tab execution, with tool_failed rather than a schema rejection.
- A real DOM button click changes task two's accessible button name to exactly
  "1"; the bounded local-page snapshot is retained in the generated case folder.
- Releasing task one refuses subsequent operations on its captured session.
  Task two's page and button change remain readable and its session closes normally.
- ReadOnly navigation is refused with capability_denied. AskEveryTime is unchanged:
  eleven operations receive explicit harness approval decisions. All twelve calls
  have completed, task-scoped audit records (four for task one, eight for task two).

The final record is runtime-identity.log. The earlier runtime-initial.log has a
weaker click assertion and covers fewer operations; runtime-final.log adds exact
button/tab checks. The final identity run also asserts the ReadOnly error code
and verifies all eight loaded MossAgent module hashes against the supplied
original App directory, avoiding stale referenced-assembly evidence. App:
22BD6CDF717B9CE31C83725C0AC5BB49F1F909F1A1A3861BBFDF5F1A03230CE1.
Browser: 67D540875E324C6E2F23A8C6B636CA8D0D842651973397173DD45CEBCACE45C2.
The complete module identities and probe source manifest are retained beside it.
The harness builds with zero warnings/errors and uses ordinary JIT.

#### Limits and next step

This is one controlled runtime workflow, not twelve independent test cases or
a complete browser acceptance run. It is real Chrome API execution, separate
from Headless rendering, native GUI operation or a live AI-provider workflow.
The later QUEUE_BROWSER_RUNTIME_ACCEPTANCE.md adds a separate production Queue/
Agent check with actual Chrome, cancellation during navigation, concurrent
approval/draft preservation and service reconstruction with manual continuation.
Browser creation cancellation, process crash/restart, embedded/external switching
and manual takeover still require their own end-to-end checks. No filesystem sandbox, arbitrary
network confinement or full product completion is established.

Original complete regression 55845 is terminal: 8,121 pass/one queued audit
assertion failure, 8,122 total, no skips or aborts. All eight reports and 2,411
source/configuration plus 2,035 runtime identities were checked before its freeze
ended. This record describes the earlier assemblies identified above. The later
BROWSER_OPERATION_CANCELLATION_ACCEPTANCE.md records guarded integration and new
original-assembly actual-Chrome timeout/recovery and Queue runtime checks; neither
changes the failed full regression into a pass.
<!-- /source: BROWSER_SESSION_OWNERSHIP_RUNTIME_ACCEPTANCE.md -->


---

<a id="browser-toolbar-acceptance"></a>

## BROWSER_TOOLBAR_ACCEPTANCE.md

<!-- source: BROWSER_TOOLBAR_ACCEPTANCE.md -->
### 浏览器工具栏统一执行入口

#### 设计自检

1. 手动操作状态归 App 的 `BrowserPaneActionsViewModel`，浏览器开发工具能力归
   Tools.Abstractions，会话实现和工具适配分别留在 App 与 Browser；依赖方向不变。
2. 导航、历史、新建、关闭、切换标签和开发工具全部通过既有 `IToolExecutor`。
   不保留直接调用会话的后备执行入口。只读标签元数据及原生事件仍为展示投影。
3. 工具复用任务授权、审批、执行期限、输出限制和审计。工具栏额外拥有五分钟的
   审批及操作总期限；取消、撤销任务上下文和会话替换取消当前操作。非协作操作
   返回前保留 busy，旧操作不能覆盖新任务状态。未选中任务和无上下文时禁用入口。
4. 每个手写 C# 文件一个类型，拆分执行生命周期与原生视图投影，不分拆 partial
   或添加页面局部视觉样式；调试与发布配置不变。
5. 定向测试覆盖三种策略、实际管线审计、无/错任务上下文、取消、期限、
   不协作返回、任务隔离、标签拒绝后的真实选中状态及开发工具不支持状态。

#### 验证记录

首次普通权限运行发现检查器的浏览器页为空：其 `DataContext` 已经切换为浏览器
面板，`IsVisible` 却仍从面板读取主窗口路径 `EmbeddedBrowsers.CurrentPane.HasBrowser`。
改为读取面板自身的 `HasBrowser`，并增加替换工作区面板后的实际布局回归。

实际绑定的标签选择器还会缓存被拒绝的选项，仅通知模型属性并不能恢复选择。
恢复操作排到绑定更新之后，重新设置真实活动项；模型和可见选择器均由会话
元数据决定，不提前提交待审批的标签。增加真实 ComboBox 绑定回归。

##### 原生窗口

- 使用仓库外验收宿主及独立 SQLite、本机 HTTP 页面和三个合成任务；不读取
  用户配置、登录账号或调用收费模型。两个正式验收窗口均为普通权限，日志
  `QA_ELEVATED=False`，关闭释放后 `QA_EXIT=0`，无遗留验收 WebView2 进程。
- 浅色 1400×820：实际操作导航、后退、前进、刷新、新建、切换、关闭标签及
  开发工具；独立开发工具窗口可打开和关闭。慢页面导航显示 busy 和取消状态，
  取消后工具返回 `tool_cancelled`，恢复按钮可用。
- 只读任务的新建和切换标签由管线拒绝；切任务保持各自地址和标签。
  AskEveryTime 任务显示真实审批卡，取消工具栏操作移除待审批卡且不新增标签；
  未自动点击批准或拒绝权限请求。
- 最新深色 980×820：只读切换拒绝后，快照中的 `SelectedTab`、真实控件的
  `UiSelectedTab` 和会话活动标签 ID 保持一致；新建标签拒绝后数量不变。
  Python 以 `--passive` 保存窗口截图，工具栏未被原生视图遮挡。
- 只查询合成数据库的工具名、任务 ID、批准状态、成功、错误码和完成状态：
  浅色共 13 条审计（含初始化导航），深色共 3 条审计（含初始化导航），全部
  已完成；允许、能力拒绝和取消分别与实际操作一致。

原生截图、快照、日志及测试结果保存在仓库外：
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-browser-toolbar-20261010-171839`。

##### 自动化检查

- 最终浏览器定向测试 124 项通过，覆盖完整执行管线、三种审批策略、期限、
  取消、非协作返回、任务及会话隔离、原生事件、绑定选择和共享工具栏。
- 8 个测试项目分别独立执行，全量 4157 项通过，无失败和跳过；App 像素测试
  独立于普通 Headless 测试进程。完整解决方案重建零警告、零错误。
- 本轮 23 个 C# 文件通过 `dotnet format --verify-no-changes --include ...`；
  Roslyn 规模检查均为单类型、少于 500 行、方法不超过 80 行。两个 AXAML
  文件不超过 500 行，本轮差异通过空白检查。
- 构建与测试顺序执行，测试使用 `--no-build`。一次额外定向复测与构建并行时
  出现了缺失预编译 XAML 的产物冲突；顺序重建后 124 项复测通过，不为此修改
  产品实现。首次全量运行被人工提前停止，最终计数只来自完成的独立测试运行。

##### 验收边界

取消不是回滚：服务端已经收到的网络请求、已经发生的浏览器副作用可能继续
完成。自动测试使用合成会话验证审批批准与拒绝；原生审批验收只取消等待。
没有验收真实账号、远程网站、外部浏览器开发工具或整个产品的 Codex 一比一效果。

不把 Headless 布局测试当作真实 WebView2 或整套 Codex 界面的视觉验收。
<!-- /source: BROWSER_TOOLBAR_ACCEPTANCE.md -->


---

<a id="cdp-agent-workflow-acceptance"></a>

## CDP_AGENT_WORKFLOW_ACCEPTANCE.md

<!-- source: CDP_AGENT_WORKFLOW_ACCEPTANCE.md -->
### CDP settings-to-Agent conversation acceptance

#### Scope and design

This closes the previous CDP slice's direct-tool-only evidence gap. A test-owned
headless Chrome and loopback scripted Responses service exercise production App
composition, real Agent orchestration, the provider adapter, task session manager,
tool execution/audit pipeline, SQLite and rendered conversation controls.

The driver and its synthetic data stay outside the repository. No production
implementation changes are required by this acceptance slice. Browser owns page
lifetimes; App owns conversation presentation and commands; the existing ports
remain unchanged. No authorization, approval, timeout, persistence schema,
publication target, JIT or JSON behavior is changed. Driver helpers have one type
per file and pass the 500-line/80-line-method limits.

Existing native MossAgent/Codex windows are not opened, operated, restarted or
captured. Headless Avalonia windows and the browser's own page screenshot are not
desktop captures. Chrome has a unique test profile; its exact loopback endpoint
comes from that profile's DevToolsActivePort, never from scanning debugging ports.
The driver closes only its own process tree afterward.

#### Configuration and actual UI input

- The production BrowserSettingsPane receives keyboard selection of Chrome and
  Existing CDP, endpoint text entry and Space activation of Save. The real SQLite
  repository is checked for all three saved values before starting conversations.
- A project, authorized directory and enabled default provider/model are seeded
  through their production repository ports. Provider/model editor creation and
  discovery are not repeated by this scenario.
- The production MainWindow runs at Light 1280x820. Actual composer typing and
  send-button keyboard activation create two project tasks. Real new-task/task-
  selection navigation commands switch between them without replacing runners.
- FullAccess applies only to this isolated synthetic project's roots and attached
  test browser. No existing user's policy or approval dialog is changed.
- Every recorded model request reaches the real Responses adapter with model
  fixture-cdp, medium effort, temperature 0.3 and Top-P 0.8. The local service
  responds using the actually advertised encoded tool names. This is scripted
  protocol validation, not a paid model or proof of autonomous reasoning quality.

#### Concurrent workflow

1. Task A navigates to /a, clicks the local popup button, enumerates its tabs and
   reads a real snapshot. It has an initial page and a popup, neither of which is
   the pre-existing synthetic sentinel page. Its next model request is held.
2. Task B navigates to /b. Its model requests switching to A's real tab ID; the
   standard pipeline returns a failure, the UI retains tool_failed and B's next
   model request receives IsSuccess=false. A read-only observer supplies that ID
   to generate the negative request; no browser mutation bypasses the pipeline.
3. B reads its own /b snapshot and creates a real browser screenshot. Its next
   model request is held while both tasks remain active.
4. Selecting A and activating its visible Stop button cancels only A. The task
   persists as Cancelled and its two pages close. B remains Running on /b; the
   sentinel and attached Chrome survive. Page count moves from four to two.
5. B's held request is released. The Agent navigates to /b-after-stop and reads
   the actual page snapshot before receiving its final Markdown response. B
   persists as Completed and its own page closes, leaving only the sentinel.

One complete scenario makes 12 actual model HTTP requests and completes ten
browser audits: four successful A tools, five successful B tools and one expected
foreign-tab refusal. All audit records have completion timestamps. These are
workflow observations, not ten additional unit-test cases.

#### Presentation, artifacts and restoration

- The completed turn keeps its normal collapsed default. Keyboard activation of
  the rendered turn summary reveals B's six tool records. The actual snapshot
  row button opens the stored /b-after-stop details; the Markdown reply renders
  through MarkdownSelectableTextBlock rather than a placeholder text control.
- Settings/save/send/stop/disclosure controls and the final reply are brought into
  view and input-hit-tested, not merely found in the visual tree. The snapshot
  details have visible selectable text after real button activation.
- Screenshot execution has one image/png artifact belonging to B. Its stored
  path, 3,943-byte length and SHA-256 match the real PNG bytes and signature.
  The final /b screenshot was visually inspected as a nonblank local page.
- A fresh production service provider reopens the same database in a Dark 526x601
  MainWindow. A remains Cancelled and B Completed. B's Markdown, expected failed
  tool and snapshot details restore and remain operable, with no new model
  request or browser page. The unchanged sentinel title/body and live attached
  browser are verified before the driver terminates its own Chrome.

#### Final verification and evidence

Two complete final driver runs pass independently with the same 12-request,
ten-audit result. The repeated runs are not summed into a larger product test
count. The latest fresh regression scopes also pass without skips or aborts:

- Browser: 88/88.
- Agent: 123/123, including architecture constraints.
- Related App browser/embedded/shutdown scope: 195/195, matching discovery.

These disjoint regression scopes total 406. Solution build has zero warnings and
errors; scoped driver whitespace/source checks and documentation hygiene pass.
Nine production/test/driver assembly identities remain equal through the runs and
final build. No test-owned Chrome remains afterward.

Evidence is under `%TEMP%\MossAgent-cdp-agent-20261016`: final driver sources,
`live-verified-one.log`, `live-verified-two.log`, current TRX reports,
`source-audit.json`, build/format/identity reports and per-run `result.json`/PNG.
The final runs are `run-31dcf226fb224973b8fcc13b8fbeae03` and
`run-f938e96b606940d9b39b780aa53f99b2`.

Earlier driver attempts are retained. Their assumptions about model error-code
serialization, collapsed turn visibility and Markdown control type are corrected
to match production contracts. Input-hit-test attempts initially omit the normal
Headless dispatcher drain; processing queued jobs fixes the fixture's stale hit
testing without changing production scrolling or weakening the hit assertions.
An oversized driver method is split before final source-limit verification.

#### Remaining scope

This is not a native desktop/Codex same-state visual comparison, an all-protocol
or full current App/all-suite checkpoint, or a resolution of the unrelated
historical allocation assertion. Native embedded-browser concurrency and the
complete responsive/DPI/state sweep remain separate. Borrowed CDP contexts still
share cookies, login and other storage; independent page ownership does not mean
storage isolation. The complete product objective remains active.
<!-- /source: CDP_AGENT_WORKFLOW_ACCEPTANCE.md -->


---

<a id="cdp-task-isolation-acceptance"></a>

## CDP_TASK_ISOLATION_ACCEPTANCE.md

<!-- source: CDP_TASK_ISOLATION_ACCEPTANCE.md -->
### CDP task-page ownership acceptance

#### Design and reproduced defect

Browser owns task page identity, popup discovery and disposal through the existing
IBrowserSession contract. Application, tool execution, settings persistence and
approval policies do not change. Each handwritten type stays in its own file;
the nine affected C# files pass the 500-line/80-line-method limits.

The original factory attaches every task to the first pre-existing CDP page.
Session tab synchronization then imports every page in the borrowed context.
Two tasks can consequently navigate the same page and enumerate unrelated pages.
All six initial isolation cases fail against that unchanged implementation.
Cancellation of NewTabAsync can also abandon a late page, and repeated disposal
can release the same runtime more than once.

#### Change and safety boundary

- Each explicit loopback CDP attachment creates a new task-owned page. The
  pre-existing page, context and attached browser are not owned by that task.
- PlaywrightBrowserTabSet tracks stable IDs for the initial attached page,
  task-created pages and popups descended from those pages, including nested
  popups. Borrowed contexts never import the context's entire page collection.
  Managed contexts retain their existing full-context discovery behavior.
- An explicitly supplied borrowed initial page remains borrowed by default.
  Factory-created CDP pages opt into initial-page ownership.
- Creation and disposal share a session-owned gate. Cancellation waits for the
  SDK page handoff, closes an unpublished late page and retains the caller's
  cancellation token. Disposal waits for creation and rejects new work.
- Disposal closes owned pages only, detaches popup subscriptions and releases
  runtime state once. A failed close does not prevent attempts on other owned
  pages; the first failure is still reported. Popups arriving during cleanup are
  included without republishing state after shutdown.
- This is page-operation and lifetime isolation, **not storage isolation**.
  Tasks attached to the same configured CDP context still share its cookies,
  login state and other context-level storage. No arbitrary debugging-port scan,
  new incognito context or daily-browser attachment is introduced.

#### Deterministic verification

Final, non-overlapping runs have no failures, skipped or aborted cases:

- Browser: 88/88, including the 12 new ownership/isolation boundary cases.
- Tools: 494/494, retaining the standard policy, approval and audit pipeline.
- Agent: 123/123, including architecture constraints.
- Related App: 195/195; discovered and executed counts match.

The solution builds with zero warnings/errors. Scoped whitespace verification
and the nine-file Roslyn source audit pass. Nine assembly hashes remain unchanged
through these runs. The real-driver Browser assembly matches the production and
test Browser assembly hash. No test-owned Chrome process remains afterward.

#### Real settings-to-tool execution

An external driver hosts the production BrowserSettingsPane in Headless Avalonia
and uses real keyboard selection, endpoint entry and Save. The isolated SQLite
configuration is checked before creating real project/task records. Subsequent
operations use the production task session manager and standard IToolExecutor,
not direct page calls that bypass approval or auditing.

The driver starts its own headless Chrome with a unique temporary profile and
loopback random-port endpoint read only from that profile's DevToolsActivePort.
Local deterministic HTTP pages verify independent navigation, task-only popups,
foreign-tab rejection, ReadOnly navigation rejection, a real PNG screenshot and
continued operation of task B after releasing task A. All 12 audits finish:
10 successes and two expected foreign-ID/policy refusals. After releasing B,
only the pre-existing synthetic sentinel page remains unchanged; Chrome is still
alive until the driver closes its own process tree in finally.

Evidence is under `%TEMP%\MossAgent-cdp-isolation-20261016`, including TRX reports,
`live-final.log`, `source-audit.json` and the final `live-result.json`/PNG in
`run-a0522f1703af4d54a896969b68ad18b6`. The PNG was visually checked as nonblank.
An earlier external-driver attempt fails with audit_unavailable because its
synthetic tasks were missing; the corrected fixture creates them rather than
weakening production auditing.

#### Remaining limits

[CDP_AGENT_WORKFLOW_ACCEPTANCE.md](BROWSER_MCP.md#cdp-agent-workflow-acceptance) adds a subsequent production Agent/Responses
conversation workflow driven through actual Headless settings/send/stop controls,
with task cancellation, foreign-tab rejection and fresh-composition history.
Its scripted local provider and Headless UI do not replace native/paid-model or
whole-product acceptance. That separate checkpoint must not be summed into this
slice's regression counts.

These 900 deterministic cases and the real driver are not a current complete App
or all-suite checkpoint. No paid model initiated this workflow. Existing native
MossAgent/Codex windows were not operated, restarted or captured. Native embedded
browser concurrency, same-state Codex visual comparison and whole-product
acceptance remain open. Earlier full-App and pixel runs are not added to these
counts or presented as current end-to-end coverage.
<!-- /source: CDP_TASK_ISOLATION_ACCEPTANCE.md -->


---

<a id="mcp-execution-acceptance"></a>

## MCP_EXECUTION_ACCEPTANCE.md

<!-- source: MCP_EXECUTION_ACCEPTANCE.md -->
### MCP 链路验收

本记录覆盖 stdio 与 Streamable HTTP MCP 工具连接及本机确定性服务，
不代表 MCP 所有协议能力、浏览器、终端生命周期或整个 Codex 界面已经完成验收。

#### 实现与安全边界

- 连接通过 `ITaskProcessLauncher` 启动；MCP 不直接依赖 Windows 或 App。
  Windows 为每个服务独立创建 kill-on-close Job，启动时保留标准输入，并传入
  经过校验的 Unicode 环境快照。覆盖和移除只影响子进程，不修改宿主环境。
- 服务是应用拥有的长期连接，不是一个共享的 shell 命令进程。
  初始化失败或取消、父进程退出、重新加载、停用、删除和释放连接会清理进程树。
- stdout 单条协议消息和出站消息上限均为 2,097,152 个字符，不能称为 2 MiB 字节。
  stderr 按固定大小缓冲区排空，不写入会话结果或日志；并发写入串行化。
- 取消完整发送的请求会发送 `notifications/cancelled`，忽略迟到结果并移除等待项；
  初始化请求不发送取消通知，而是关闭连接。发送中的帧失败或取消会关闭连接，
  不复用可能被破坏的协议流。通知是协议建议，不能保证不合作的远端停止副作用。
- JSON-RPC 错误使用安全通用消息，不回显服务提供的异常正文。
  MCP 配置的 `ToString()` 只返回名称，不把参数、环境、请求头或 URL 放进列表名称。
- 未声明只读的 MCP 工具按外部副作用处理，通过标准执行器审批、取消、输出限制
  和审计。未知参数不开放原始 JSON 预览，保留既有秘密保护边界。

#### 自动验收

- 真正运行 PowerShell JSON-RPC 服务，不访问付费模型或真实用户项目。
- 验证 Unicode 输入、配置参数、工作目录、环境覆盖和大小写不敏感的移除；
  PowerShell 自己会重建 PATH，因此移除测试使用不会被它重建的 ComSpec。
- 验证父进程退出、连接释放、初始化取消、超时和配置停用、删除、重新加载后的
  父子进程退出；背景子进程不能在连接结束后继续写入迟到文件。
- 24 个并发 Unicode 调用保持完整帧；超限且无换行的 stdout 被拒绝；
  3 MiB stderr 不堵塞调用；协议错误中的合成秘密不进入异常消息。
- 非泵送同步上下文测试真实复现启动死锁；修复后客户端及管理器无需 UI 回调即可初始化。
- `WorkspaceMcpFlowTests` 从提供商、模型和 MCP 设置命令出发，经过真实 AgentRunner、
  工具发现、模型工具名映射、审批与 stdio 调用，验证成功、拒绝、审批中停止、
  ReadOnly 拒绝、运行中停止、输出截断及安全失败。
- 重新打开 SQLite 数据验证工具记录、任务状态、会话展示和审计结果一致；
  成功配置的参数、工作目录和环境实际到达服务，而不是只断言保存字段。

#### 实际桌面复核

使用独立合成配置启动普通权限应用。启用 stdio MCP 时，旧版在创建主窗口前死锁；
修复后窗口显示，服务进程保持连接。重开成功验收会话，实际展开保存的工具结果，
看到中文和 emoji 回复；检查设置列表、编辑弹窗及 Escape 关闭。没有从桌面
发送或重跑任务，没有修改生产配置和审批策略。正常关闭应用后，服务进程退出。

仓库外 Python 被动截图保留原始 1920×1032 像素，包含
`mcp-history-restored.png`、`mcp-tool-expanded.png`、`mcp-settings-list.png`
和 `mcp-settings-editor.png`；不是逐像素 Codex 对照证明。

默认测试清理临时数据。显式设置 `MOSSAGENT_MCP_QA_ROOT` 可保留成功场景的
`approved/data` 和验收项目，路径必须是新目录。仅用于检查历史；模型夹具端口
在测试结束后关闭，不应向这些保存的模型发送任务。

#### 2026-10-05 Streamable HTTP 安全与工作区验收

- 请求和响应均限制为 2 MiB 字节，不与 stdio 的字符限制混淆。
  声明长度超限时不读取响应正文；未知长度、无换行 SSE 和连续心跳同样有界，
  超限探测最多读取限制外一个字节。正常结果仍经过工具执行器的输出字符预算。
- SSE 增量处理心跳、多行 data、进度和其他请求标识；收到当前请求结果后立即返回，
  不等待服务关闭流。JSON 和 SSE 均校验 JSON-RPC 版本、请求标识和唯一结果或错误。
  仅接受 JSON/SSE 内容类型与严格 UTF-8，允许 UTF-8 BOM，不自动转换 UTF-16。
- 已开始调用的取消会在一秒预算内尝试发送 notifications/cancelled，携带原 requestId。
  通知失败不覆盖调用取消；预先取消不发请求。初始化失败或取消会释放连接，
  已分配的会话尝试 DELETE。释放连接还会取消在途调用，重复释放是安全的。
- HTTP 错误、JSON-RPC 错误及工具 isError 正文不会成为错误摘要；合成秘密不进入
  会话记录、工具结果或下一轮模型请求。成功的正常工具内容仍被保留并限长。
- 默认不跟随 HTTP 重定向，不将自定义请求头转发到未配置的目标。
  真实本机 307 服务先复现转发，修复后目标服务不再收到请求头。
  未选择代理时不隐式继承系统代理，明确选择代理时保留现有代理路由。
- 新增 30 项工具层回归，覆盖已知/未知响应长度、SSE 开放流、UTF-8、协议拒绝、
  请求长度、并发请求标识、初始化和运行取消、连接释放、错误保护及真实重定向。
- 新增 8 项 HTTP 工作区验收，从提供商、模型及 MCP 设置命令出发，
  经真实 HTTP 服务、工具发现、AgentRunner、审批、调用、取消和 SQLite 持久化，
  验证批准、拒绝、审批中停止、ReadOnly 拒绝、运行中停止、截断及两类安全错误。
  成功回复含中文和 emoji；服务保持 SSE 开放，工作区仍完成会话和审计。
  应用服务释放后，服务实际收到会话 DELETE。
- stdio 与 HTTP 工作区回归合计 15 项通过。本轮完整解决方案 1021 项测试通过，
  其中 App 670 项、Tools 147 项；解决方案构建 0 警告、0 错误。
  本轮 C# 空白格式检查和相关 git diff --check 通过。
- 全部服务仅监听本机临时端口，使用独立临时数据，不访问付费模型、生产配置
  或真实用户项目。本轮没有重新操作桌面和截图，不视为新增视觉验收。

#### 2026-10-05 后台启动与桌面退出复核

- 初次 MCP 加载移到主窗口打开后的后台任务；连接期间保留输入草稿并禁止提前发送。
  完成状态通过 UI 队列发布，失败只显示安全通用警告，并提供打开 MCP 设置的操作。
- 启动服务拥有取消令牌，不接管 MCP 管理器的所有权。退出先取消并等待后台加载，
  再释放服务容器；已排队的完成回调不会修改已经关闭的界面。
- 普通权限应用使用独立合成数据和挂起 initialize 的真实 stdio 服务。
  主窗口在约 1.9 到 2.1 秒出现，不等待每个服务的 15 秒初始化超时；
  可访问树和非黑屏截图均显示连接提示、恢复的会话及禁用的发送按钮。
  这些时间只描述本机验收配置，不是所有数据规模和设备的启动保证。
- 单服务超时后，警告操作实际打开 MCP 设置，并显示安全的服务超时状态。
  多服务加载期间通过“文件 → 设置”打开外观页，键盘 Tab 也能移动真实焦点。
  左下角设置图标的加载期单击没有得到页面切换证据，不能把该路径记为通过。
- 配置 12 个挂起初始化的合成服务，在启动约 130 秒、仍有服务及其子进程存活时
  从窗口关闭按钮正常退出；应用及所有已启动的夹具进程退出，没有遗留迟到文件。
  没有修改生产配置、凭据或审批策略，也没有发送保存的模型任务。
- 仓库外 Python 被动截图保留原始 1920×1032 像素，包括
  `startup-timeout.png`、`startup-mcp-settings.png`、`startup-loading.png`
  和 `startup-settings-open.png`；截图及夹具准备脚本不加入仓库。
- 启动定向测试 11 项通过，包含成功与失败完成、取消、过期 UI 回调、服务所有权、
  设置导航、编辑弹窗和草稿保持；启动加 stdio/HTTP 工作区回归共 26 项通过。
  装配方法按职责拆分，保持注册顺序、生命周期与审批管线不变。
- 最后一次装配拆分后，完整解决方案 1032 项测试通过，其中 App 681 项；
  构建 0 警告、0 错误。启动相关 C# 空白格式检查与 git diff --check 通过。
  启动相关 10 个文件的单类型、文件规模和方法规模检查通过。
- 全仓库 662 个手写 C# 文件的单类型和 500 行上限检查通过，但还存在三个超出
  80 行的现有方法：OpenAiCompatibleProvider 的流处理方法、
  ModelSettingsViewModelTests 与 SubagentWorkspaceFlowTests 的一个测试方法。
  不能把本轮启动范围检查通过表述为整个仓库已经满足全部方法规模约束。

#### 2026-10-05 加载期间设置导航后续复核

- 原版本在同一组 12 个挂起服务的加载期，两次物理坐标点击实际打开设置，
  但不能据此断言之前的偶发无响应已经解决。后续版本仍观察到一次坐标点击未切换，
  按当前可访问元素定位后可打开设置，加载完成后的坐标点击也可用。
- 运行检查另行复现了明确的页面焦点问题：设置遮罩未隐藏工作区，
  Tab 能进入遮挡的项目按钮。主窗口现使用共享页面切换组件隐藏非活动页；
  复测设置搜索初始焦点、设置内部 Tab 循环、返回焦点和未发送草稿保持通过。
- 加载期间打开设置不依赖 MCP 就绪；加载超时完成后正常返回工作区，
  警告显示安全文本，草稿未被清空。本次没有发送模型任务或修改生产配置。
- 隔离预览已正常关闭，全部已记录的夹具进程退出，没有迟到子进程文件。
  本轮新增 14 项组件回归，完整解决方案 1046 项测试通过，其中 App 695 项。
- 页面切换修复与鼠标命中是不同验收项，后者继续保留在待查范围中。

#### 2026-10-09 Reload 与关闭所有权

- 修复关闭与正在加载、等锁 reload 的竞态。关闭先拒绝新请求，取消已登记操作，
  等待所有操作返回，再清理连接、仅移除 `mcp.` 工具并释放锁。重复关闭共享同一个
  完成任务；非合作配置读取不会让关闭提前成功，也不能在关闭后启动迟到服务。
- 排队请求的调用者取消只取消自身，保留原始取消令牌，不误取消正在进行的读取。
  所有新增等待均不依赖 UI synchronization context，不改变工具审批和审计管线。
- 原两项 stdio 偶发失败使用三秒真实时间同时限制进程启动、初始化和工具发现，
  在全量并发负载下可能先超时再检查已退出 PID。夹具改用可控 `TimeProvider`，
  仍运行真实 PowerShell 父子进程并检查退出；测试外层保留三十秒真实截止时间。
  默认十五秒限制未放宽，单独验证推进 14.999 秒不超时，再推进一毫秒超时回收。
- 关闭负例先得到两失败、一通过的有界报告，修复后管理器定向五项通过。
  stdio 二十项定向通过；包含新增默认截止时间的压力筛选五轮，每轮十项通过。
  初始化中关闭、发现成功后关闭、配置停用/删除/重载、同步上下文和父子进程
  回收均使用本机合成服务，不访问用户提供商或实际 MCP 配置。

设计自检：

1. 所有权属于 MCP 模块，仍依赖配置仓库、工具目录和进程启动端口，不依赖 App
   或 Windows 具体实现。关闭协调和操作 lease 拆为独立单类型文件。
2. 复用既有 `IMcpManager`、配置仓库与进程端口；时钟仅作为可选构造参数，
   生产默认 `TimeProvider.System`，不增加新的工具执行入口。
3. 初始化/发现仍默认十五秒，已启动的 stdio 服务继续由原进程所有权机制回收。
   非合作读取必须返回才能完成关闭，不宣称能强制终止任意外部副作用。
4. 新增与修改的 C# 文件维持单类型、文件和方法规模限制，生产 lease/lifetime
   与测试时钟分别独立，不以 partial 或嵌套类型隐藏规模。
5. 定向测试覆盖成功发现、排队取消隔离、迟到配置拒绝、新请求拒绝、重复关闭、
   默认截止时间、正常/取消关闭及真实父子进程退出。原非 MCP 工具保持注册。

本轮第一次全量运行的 App 项目有 734 项级联失败，根异常是多个未隔离控件测试
并发触发 Avalonia 全局 RoutedEvent 注册，污染 TextBox 静态初始化；不能计为
734 个独立产品缺陷。新进程隔离复测原 App 2448 项通过，不等于原全量全绿。
九个直接构造控件的测试类纳入既有视觉 collection，未关闭整个程序集的并行。
新增 AST 门禁只针对测试类中的显式视觉构造，先复现九类遗漏，修复后含门禁的
八十三项定向通过。字符串中的分析器样例和纯业务测试不被串行化；此门禁不声称
静态分析能追踪任意间接工厂或 target-typed 构造。

证据位于仓库外 `%TEMP%/MossAgent-mcp-concurrency-20261009`，包括
`tests/shutdown-red-bounded.trx`、`tests/manager-green.trx`、
`tests/stdio-focused.trx`、`stress/stress-1.trx` 至 `stress-5.trx`、
`full-final`、`app-isolated` 以及 `visual-isolation-red/green`。
本段只验证 MCP 和测试初始化边界，不替代提供商、子代理、定时任务或全界面验收。

最终复核：解决方案构建零警告、零错误，二十四个范围内源码文件的 AST 规模/
单类型审计、C# 空白检查和相关 `git diff --check` 通过。隔离改动后两次全量
各 3229 项通过；最后一次包含文件标签关闭图标校准的全量为 3228/3229 通过。
唯一失败是 `ProviderProxyRuntimeTests` 的 Gemini 停用代理案例，在进入路由
断言前，SQLite 初始化被夹具五秒取消令牌取消；不是本轮 MCP 或视觉断言失败。
隔离复测全部十二项提供商/代理案例通过。未修改其实现/夹具，也不能把复测绿灯
当作最后一次全量全绿。相关结果保留在 `full-after-isolation`、`full-accepted`、
`full-final-verified` 与 `provider-proxy-isolated`。完整产品 goal 继续保持未完成。

#### 仍需完成

- 更多 MCP 协议能力与会话恢复，例如独立 GET SSE、断线事件恢复及服务端请求。
  实际代理路由与连接失败、更多并发及慢服务的桌面可用性仍需继续验收。
- 不合作服务的副作用、提权或服务创建的外部进程、应用异常终止的独立宿主验收，
  以及更多并发场景。正常结束和父进程退出测试不能代替所有异常退出情形。
- 数据库初始化和初始服务装配仍在显示主窗口前同步完成。
  加载期侧栏设置图标的鼠标命中和页面切换路径需要进一步验证与定位。
- 所有页面、主题、DPI、状态和关键交互的完整视觉验收。
<!-- /source: MCP_EXECUTION_ACCEPTANCE.md -->


---

<a id="mcp-reconnect-recovery-acceptance"></a>

## MCP_RECONNECT_RECOVERY_ACCEPTANCE.md

<!-- source: MCP_RECONNECT_RECOVERY_ACCEPTANCE.md -->
### MCP reconnect and catalog recovery

#### Scope and design review

- App owns the settings command and publication of repository snapshots. The
  existing `IMcpConfigurationRepository`, `IConfigurationRepository`,
  `IMcpManager` and tool catalog remain the boundaries; no new runtime port,
  persistence schema, provider adapter or platform dependency is introduced.
- Reconnect already resets the configured MCP connections. It now also reads
  both server and proxy catalogs before publishing a successful refresh. It
  does not save/delete settings or execute a tool or model request.
- The reconnect command has cancellation linked to the settings lifetime.
  Existing MCP connection deadlines, tool approvals, authorization, audit and
  output limits are unchanged. Cancelling a reconnect is not a transaction
  rollback of connections already changed by the manager.
- Busy commands disable competing settings actions; an open editor blocks
  reconnect, preserving its draft. Shared control wrappers and resource labels
  remain in use. The cancel action replaces, rather than extends, the reconnect
  button's header slot. No colors, icon sizes or shadows are added per view.
- At the initial checkpoint, the largest changed C# file is 458 lines; each
  handwritten file still has one type. Architecture tests enforce the
  repository-wide file/type limits.

#### Reproduced failure and correction

The old reconnect command reloads only the manager, not the visible catalogs.
All three initial regression cases fail: retry after an initial repository error
leaves an empty list, a failed read is hidden by a success message, and changed
server records never reach the page.

The corrected command reloads both snapshots and reports success only after
their reads finish. Failed/cancelled reads retain the previous snapshot and
offer another reconnect. Generation checks reject an older initial load;
lifetime cancellation and disposed-state checks suppress late publication.
Connection errors remain distinct from a repository failure or cancellation.

#### Verification: 2026-10-15

- Eight deterministic command tests cover initial recovery, changed catalogs,
  connection diagnostics, cancellation during connection and repository reads,
  disposal, atomic publication after proxy-read failure, and an open draft.
- Four production-page cases in light/dark at 728/980px exercise real keyboard
  activation, busy/disabled state, Cancel and successful retry. They use Avalonia
  Headless, not native-window automation or a new desktop screenshot.
- A production-composition test persists an unavailable loopback HTTP endpoint,
  reconnects and observes removal of its stale tools, then restores the endpoint
  and reconnects. Only after an explicit synthetic approval does the recovered
  tool run. The real HTTP response, two model requests, headers, conversation,
  completed audit and unchanged final configuration are verified. No paid API,
  real user settings, arbitrary endpoint or live approval setting is used.
- The final related App run passes 73 cases, component guards 27, complete pixel
  tests 136, MCP protocol/tool tests 56 and architecture tests 7: 299 distinct
  cases, zero failures/skips. Earlier focused/isolated runs are not added again.
- The first broader 72-case run records two 30-second HTTP completion timeouts.
  Both affected branches pass the four-case isolated run and the final broader
  73-case run. Their cause is not isolated; the failed report is retained rather
  than described as repaired by rerunning.
- Solution build has zero warnings/errors; scoped formatting and whitespace
  checks pass. This is not a new complete eight-assembly run. The preceding
  complete checkpoint's allocation failure remains recorded separately in
  [UI_COMPACT_LAYOUT_ACCEPTANCE.md](UI_WORKFLOWS.md#ui-compact-layout-acceptance).

Evidence is outside Git at `%TEMP%/MossAgent-mcp-reload-tests`: `baseline.trx`,
`mcp-regressions.trx`, `real-reconnect.trx`, `mcp-final.trx`, `visual-final.trx`,
`tools-mcp-final.trx`, `component-guards-final.trx` and `architecture-final.trx`.
Native screenshots remain user-postponed. This bounded recovery scenario does
not prove every configuration race, native recovery or whole-product/Codex
visual equivalence. Nothing is staged, committed, pushed, published or installed.

#### Mutation and editor-lifetime follow-up (2026-10-15)

Eight additional baseline cases reproduce late writes/reconnects after disposal,
saved data replacing newer input, a closed editor reopening on connection failure,
and an externally removed record being silently recreated. The failed baseline
is retained in `%TEMP%/MossAgent-mcp-mutation-tests/baseline.trx`.

The App now reuses guarded async commands and its existing editor-revision base.
Preflight reads, writes, deletes and the subsequent manager reload share the
command/lifetime cancellation token. Checks between awaits prevent a new write
after cancelled preflight and prevent reconnect/publication after disposal.
A write already started can still commit; cancellation is not presented as a
rollback. The saved configuration remains inspectable after cancelled reconnect.

Catalog reads/publication and immutable editor snapshots have separate small
types. Selection feedback during catalog publication cannot reset the editor.
Canonical row/proxy references update without loading over typed fields, and a
stable new-record identity lets a later save update the same created record.
Revision ownership prevents an older result from closing/reopening a newer or
dismissed editor. Closing the dialog also clears pending delete confirmation.
Missing edit targets are rejected at preflight, not silently recreated; this is
not a cross-process compare-and-swap guarantee between validation and SQL write.

Dependencies, repository contracts/schema, tool approval/authorization/audit,
manager ownership, JIT/JSON and Windows x64 single-file publishing stay unchanged.
No direct I/O enters a view, no new static service state is introduced, and each
handwritten C# file retains one type. The main settings model is now 454 lines;
the catalog and profile factory are separate responsibilities, not partial files.

Final verification adds 22 cases: 13 deterministic mutation/draft cases, eight
production-page keyboard cases in both themes at 728/980px, and one production
stdio process case. That last case starts a real hanging MCP server and child;
disposing settings cancels the save/reconnect and releases both processes before
the service container is disposed. The committed configuration remains present,
no model request/tool execution occurs, and no late UI notification is published.

The final related App group passes 122, complete pixels 136, MCP protocol/tool
checks 56 and architecture checks 7: 321 distinct cases, zero failures/skips.
The focused and isolated results are subsets, not added again. Solution build
has zero warnings/errors; scoped formatting and tracked/new-file whitespace pass.
This is not a complete eight-assembly run or a native-window acceptance sweep.

Final reports are `%TEMP%/MossAgent-mcp-mutation-tests/app-related-final.trx`,
`visual-final.trx`, `tools-final.trx` and `architecture-final.trx`.
`mutation-page-boundaries.trx` and `owned-process-final.trx` retain intermediate
focused evidence. Existing user changes remain intact. No staging, commit, push,
release, installation, user-window operation or native screenshot occurs.
<!-- /source: MCP_RECONNECT_RECOVERY_ACCEPTANCE.md -->


---

<a id="mcp-tool-identity-acceptance"></a>

## MCP_TOOL_IDENTITY_ACCEPTANCE.md

<!-- source: MCP_TOOL_IDENTITY_ACCEPTANCE.md -->
### MCP 工具身份与注册精简

#### 设计自检

1. 工具注册归 MCP 模块，通过现有 `IMutableToolCatalog` 注册适配器。
   将命名和整批校验集中到 `McpToolRegistration`，管理器仅负责连接生命周期；
   不增加跨模块依赖、静态可变状态或新的服务定位器。
2. 复用现有配置 GUID、`IMcpClient` 和 `McpToolAdapter`，不新增端口或数据库字段。
   工具实际调用仍传入远端原始名称，不把归一化名称发给远端。
3. 注册时保留取消检查，初始化与发现仍共享原有超时预算。
   执行仍经过 schema、能力/路径、风险、审批、超时/取消、输出限流和审计管线。
   失败只显示安全摘要，释放失败连接，不回显远端异常、请求头或凭据。
4. 新生产文件只有一个类型、33 行；管理器保持 160 行。
   新增测试类型独立成文件。不改变普通 JIT 调试、JSON 序列化或 Windows x64 单文件发布。
5. 使用真实本机 HTTP/stdio 服务验证同名、大小写、标点、中文和纯符号碰撞；
   覆盖不同风险、ReadOnly 拒绝、配置顺序变化、删除、重复定义、批准与拒绝、
   模型工具映射、会话重开和 SQLite 工具/审计记录。

#### 原因与实现

原注册名仅为 `mcp.<归一化服务名>.<归一化远端名>`。
工具注册表按大小写不敏感匹配并允许替换，因此不同服务或不同远端工具可能互相覆盖。
例如 `same service`/`same/service`、`read.file`/`read/file` 会被合并，
改变配置加载顺序还可能把相同名称绑定到另一个服务。

新注册格式为：

```text
mcp.<归一化服务名>.<配置完整 GUID:N>.<归一化远端名>__<原始名称 UTF-8 SHA-256 前 12 位小写十六进制>
```

完整配置 GUID 区分服务；原始远端名称摘要区分归一化后相同的名称。
摘要不是无碰撞保证，整个服务批次还按注册表相同的大小写不敏感规则校验。
重复或碰撞身份拒绝整个批次，不发布部分适配器；失败客户端由既有生命周期释放。
这次精简集中注册职责与校验，不宣称应用整体性能或分配量改善。

#### 兼容性边界

- 已存配置、会话、审计和用户数据不迁移、不重写。
- 新发现的工具采用新名称；旧的模糊名称不创建别名，旧调用会不可用，
  而不是悄悄绑定到另一个服务。模型的下一次新调用应使用当前发现的工具目录。
- 名称含归一化标签，重命名配置也可能改变注册名；这不是配置更名后的稳定别名机制。
- 配置 GUID 区分不同配置，但不锁定同一配置的 URL、命令或工具风险。
  本轮不实现运行中修改配置后的会话版本快照，也不改变 MCP 元数据的信任政策。

#### 专项验证

证据目录为 `C:\Users\wzheng\AppData\Local\Temp\MossAgent-workflow-audit-20261012-173156`。

- 原实现的身份回归为 8/8 失败，修复后 MCP 工具专项 56/56 通过。
- App MCP、设置和命令专项 49/49 通过；Windows stdio 生命周期专项 21/21 通过。
- 新增 App 两项完整链路：通过真实设置保存 `workflow` 和 `workflow!`，
  模型请求原配置工具，审批前两端均无调用；批准只调用原服务，拒绝两端均无调用。
  重开后验证会话、工具记录和审计仍绑定原配置。
- stdio 成功场景额外 1/1 通过，保留独立合成数据用于原生应用复核。
- 解决方案构建零警告、零错误；本轮 12 个 C# 文件空白格式校验通过。
  Roslyn 审计全部 1523 个手写 C# 文件，类型数量、文件 500 行及方法 80 行硬约束无违规。

#### 原生应用复核

2026-10-12，以普通权限运行实际 Debug `MossAgent.App.exe`，
使用保留的合成数据，不发送新消息，也不访问付费模型、真实项目或生产配置。
重开完成会话并展开 MCP 结果，实际看到中文和 emoji 内容；
进入 MCP 设置列表，显示已保存的 stdio 服务和发现的一个工具。

仓库外 Python 被动截图保存 `mcp-native-tool-result.png` 与 `mcp-native-settings.png`，
均为非黑屏 1280×820 像素、96 DPI。窗口激活从最大化恢复为该尺寸，
这只是本机工具行为的记录，不是新界面改动或逐像素 Codex 验收。

正常关闭应用后，应用、stdio PowerShell 服务及 conhost 子进程均退出。
前后任务、消息、工具执行和工具 artifact 的数量与 SHA-256 摘要完全一致，
没有启动重放；stdout/stderr 均为空。本轮桌面复核不涉及审批或安全/隐私设置操作。

#### 完整回归与限制

修改后执行 `dotnet test MossAgent.slnx --no-build --no-restore -m:1`，
八个测试程序集共 5476/5476 通过，零失败、零跳过，命令退出码 0。
独立 TRX 保存在 `full-after-mcp/`，汇总为 `mcp-full-final-summary.json`。

| 程序集 | 通过 |
| --- | ---: |
| Agent | 112 |
| App | 3937 |
| App.Visual | 8 |
| Browser | 76 |
| Infrastructure | 199 |
| Platform.Windows | 137 |
| Providers | 516 |
| Tools | 491 |

本轮改动前的全量原件仍保存在 `full-suite/`，结果为 5464/5466。
两条失败是既有分配断言：`ProviderCatalogLookupTests` 的 missing-provider 场景
出现 1848 bytes，`SidebarFilterAllocationTests` 的 unchanged-project 场景出现
5448 bytes，预期均为 0。修改后完整回归包含这两条且通过；
没有更改其实现或放宽断言，不将这次通过宣称为偶发分配问题已经根治。

本轮 12 个 C# 文件最终空白格式验证退出 0；包含新文件和文档的尾随空白检查
及相关 `git diff --check` 通过。未暂存、提交、推送或发布。
结果仅验收本轮 MCP 身份修复与注册职责精简，不代表完整产品目标、所有 MCP
扩展协议能力、真实远端服务或 Codex 界面一比一复现已经完成。
<!-- /source: MCP_TOOL_IDENTITY_ACCEPTANCE.md -->


---

<a id="mcp-ui-recovery-acceptance"></a>

## MCP_UI_RECOVERY_ACCEPTANCE.md

<!-- source: MCP_UI_RECOVERY_ACCEPTANCE.md -->
### MCP settings accessibility and disconnected recovery

#### Scope and design review

- App owns field labels and status severity. Nine editor fields now share their
  visible localized labels with persistent automation names. Names do not depend
  on values, placeholders or the selected transport. Existing shared wrappers,
  dialog layout, semantic error color and header reflow remain the visual system.
- `McpSettingsViewModel.HasStatusError` explicitly distinguishes failed reads,
  validation, mutations and connections from loading, cancellation and success.
  Both the page header and dialog footer use the same shared error variant.
  Opening a new editor or recovering a connection clears the old error severity.
  No behavior is inferred from translated status text.
- Existing repository, manager, provider, approval, audit and tool contracts are
  unchanged. UI code gains no file, HTTP, process or SQL access. No migration,
  packaging, AOT or JSON-contract changes are made.
- Reconnect retains the existing cancellation/lifetime and connection deadlines.
  It refreshes registration; it never retries a failed tool side effect. Unknown
  MCP tools still require the standard side-effect policy and approval pipeline.
  Cancellation may remove old registrations; retaining the configuration list
  does not imply those connections remain usable.
- Each changed handwritten C# file contains one type and remains below 500 lines;
  changed methods remain below 80 lines. Helpers are separately named files.

#### Reproduced failures

- Before adding the automation names, all four light/dark × stdio/HTTP form
  checks fail to find named editor fields. All four pass after the label change.
- Before binding explicit severity, the four real HTTP failure/recovery UI cases
  reach the correct safe connection failure, but the page header still presents
  it as an ordinary status. The other eight workflow cases pass. The correction
  uses the existing error style, not local colors, dimensions or shadows.

#### Production-composition workflow

`McpUiRecoveryTests` runs the production `MainWindow`, settings, workspace, Agent,
HTTP provider, MCP client, tool executor and SQLite repositories in Avalonia
Headless. The loopback model and MCP server are deterministic test-owned services;
no paid model, real credentials or user's settings are accessed.

Light and dark each cover both 1280×820 and 526×601:

1. Activate Add, type the server name, choose HTTP with the keyboard, enter the
   endpoint and headers, and activate Save and connect. The actual client
   initializes, discovers and registers the tool with its persisted identity.
2. Make the endpoint unavailable, type in the composer and activate Send. The
   tool request waits for the real approval UI; no tool HTTP call occurs before
   approval. Approving produces one failed HTTP call and a safe persisted result.
3. Activate Reconnect while offline. The old tool disappears from registration,
   safe error feedback is visible, and neither tool nor model is retried.
4. Restore the endpoint and activate Reconnect. The same tool identity returns,
   the error presentation clears, and there is still no automatic tool/model call.
5. Explicitly create and send a new task, approve it, and receive the real MCP
   echo. Each full recovery scenario has four model requests and two tool HTTP
   attempts: one expected failure and one success. Header/session configuration,
   both completed audits and persisted result/error state are checked.
6. Expand the completed turn and tool row using keyboard actions. Verify the
   actual selectable output and Markdown reply are on screen and hit-testable.
   Recreate the service composition and window, restore the selected task, repeat
   these interactions and assert no further tool or model request.

Four additional rejection cases cover actual Deny and ReadOnly policy in both
themes, split across the two sizes: no tool HTTP call is made. The persisted audit
and conversation retain `approval_denied` or `capability_denied` as appropriate.
Four cancellation cases hold the real HTTP initialization, activate Cancel,
release the late response, verify it cannot register a tool, then reconnect
successfully. Five focused status tests cover initial read recovery, invalid
input, saved-but-disconnected retry, disabling an unreachable server and neutral
cancellation. Four form tests cover all nine accessible names.

All failures are checked for absence of a synthetic secret marker in UI status,
persisted tool/audit records and subsequent model input. HTTP failure bodies are
not used as user-facing error details.

#### Rendering and test-harness limits

Optional evidence is written outside the repository using
`MOSSAGENT_MCP_RECOVERY_QA_ROOT`. Editor, offline, reconnected, expanded output,
completed reply and restored frames cover both themes and sizes. These are
Headless/Skia renders at the default scale, not desktop captures or Codex comparisons.

The initial output check omitted a render pulse and hit an old visual. A later
restored compact-window check still hit the old scroll presenter even though a
captured frame already showed the correct control. Diagnostic output confirms
the same coordinate hits the control after capture. The test now synchronizes
against a captured current render before hit testing; it retains the original
on-screen and actual-hit assertions. No production scrolling change is inferred
from these harness failures, and failed reports are retained.

#### Final verification: 2026-10-16

- The final related App run passes 266 cases, including all 21 new tests and
  existing MCP workflow, catalog, header and shared-component guards. The
  complete pixel suite passes 282, MCP protocol/tool tests 56 and architecture
  tests 7: 611 distinct cases, no failures, skips or aborted tests. Earlier
  focused runs and red baselines are not added again to that count.
- The solution build completes with zero warnings and zero errors. Scoped
  whitespace formatting, `git diff --check` and nine-file AST size/type/method
  audits pass. Production App and both test output copies have the same App DLL
  hash; the recorded App and App-test binaries remain unchanged during the final
  no-build test runs.
- Twenty-four final Headless frames cover six states across both themes and
  sizes. Compact error/recovery, editor and restored-output frames are visually
  reviewed. Final reports are `app-final.trx`, `pixels-final.trx`, `mcp-final.trx`
  and `architecture-final.trx`; frames are in `frames-final`. Original red
  reports and harness diagnostics remain beside them.
- This is a focused regression/build checkpoint, not a new complete solution
  test run or whole-product sign-off. Unrelated existing work is preserved; no
  staging, commit, push, publish or existing desktop-window action is performed.

#### Remaining acceptance

- This is explicit HTTP reconnect, not automatic transport resumption, independent
  GET SSE support, arbitrary server requests or automatic side-effect replay.
- Provider/model configuration is seeded through repositories here; its editor
  workflow is covered separately. A scripted local model does not establish live
  model reasoning quality or arbitrary third-party MCP behavior.
- Fresh-composition history restoration is tested, not native desktop startup,
  every startup handshake race, process crash recovery or every stdio scenario.
- Native window interactions, assistive-technology announcements, fractional DPI,
  same-state Codex comparison and whole-product visual acceptance remain open.
  Existing user desktop windows are neither operated nor captured by this slice.

Evidence root: `%TEMP%/MossAgent-mcp-recovery-20261016`.
<!-- /source: MCP_UI_RECOVERY_ACCEPTANCE.md -->


---

<a id="queue-browser-runtime-acceptance"></a>

## QUEUE_BROWSER_RUNTIME_ACCEPTANCE.md

<!-- source: QUEUE_BROWSER_RUNTIME_ACCEPTANCE.md -->
### Queue browser cancellation and reconstruction check

Status: one controlled CLI workflow passes using production App composition,
saved browser/model configuration, Queue dispatch, Agent, the real ToolExecutor,
SQLite and actual managed Chrome. The model service and web page are local,
deterministic fixtures. This is not native GUI or real-provider acceptance.

#### Established chain

The independent JIT harness in artifacts/queue-browser-runtime-20261019 references
the existing original App assemblies; it does not rebuild src/tests. It saves a
Chrome/Managed/explicit-direct browser configuration and a Responses model with
medium reasoning, then sends and queues prompts through Workspace commands.
All eight actual HTTP requests use that saved model and reasoning. Both tasks
retain AskEveryTime and receive four explicit harness browser approvals.

The first queued navigation reaches a local page that intentionally withholds its
response. The second task's own browser loads another page and waits for click
approval. Cancelling the first completes its navigation audit as tool_cancelled,
pauses its remaining Queue entry and preserves its draft. The second run and the
exact pending approval identity remain intact. Its real click and snapshot then
complete; the presented Tool.Details contains the heading and button "1".
The first task has one completed cancelled audit; the second has exactly three
completed successful navigate/click/snapshot audits. All retain their own task,
saved AskEveryTime policy and approval result.

After normal shutdown, a new production service composition opens the same
generated SQLite data. The cancelled tool result, successful snapshot and both
unsent drafts remain visible in reconstructed Workspace view models. Reading and
switching those conversations makes no HTTP request. The first Queue remains
paused until Continue is explicitly invoked; that sends exactly the remaining
prompt and does not repeat the cancelled navigation. The held page is requested
once in total. This is service reconstruction within one CLI process, not an EXE
crash or native restart.

The terminal record is runtime-details.log, with original loaded hashes for all
eight participating MossAgent modules. App is
22BD6CDF717B9CE31C83725C0AC5BB49F1F909F1A1A3861BBFDF5F1A03230CE1.
The harness build has zero warnings/errors. runtime.log preserves an earlier
harness timeout: it attempted a second send before the new-conversation command
became available. runtime-gated.log preserves a later assertion error that read
the summary instead of the presented tool details. The final harness waits for
command readiness and inspects the actual Details property; neither issue changed
production code or approval policy.

#### Limits

This extends BROWSER_SESSION_OWNERSHIP_RUNTIME_ACCEPTANCE.md with Queue/Agent,
in-flight navigation cancellation, concurrent approval/run/draft preservation and
manual reconstruction. It does not verify browser creation cancellation,
browser-process crash recovery, embedded WebView2, external/daily profiles, CDP
attachment, manual takeover, native GUI, full restart or network confinement.
Only generated data and manager-created sessions are used and disposed. No
existing user browser/window, credentials, paid service, source/Debug runtime
modification, elevation, commit, push or release occurs.

Original complete regression 55845 is terminal: 8,121 pass/one queued audit
assertion failure, 8,122 total, no skips or aborts; all eight reports and all source/
runtime identities were checked before its freeze ended. After the seven-file
browser cancellation/audit assertion integration, queue-runtime-original.log under
artifacts/browser-timeout-runtime-20261019 repeats this workflow against the new
original App module identities. This does not erase the old failed run. The
command filesystem safety blocker remains open; this browser check does not
establish full-product completion.
<!-- /source: QUEUE_BROWSER_RUNTIME_ACCEPTANCE.md -->
