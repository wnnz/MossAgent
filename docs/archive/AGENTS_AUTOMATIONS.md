# 代理、子代理与定时任务

> 历史归档：保留原报告当时的结论、实验、失败与限制。整理于 2026-10-21。
> 当前能力与未完成方案统一维护在[功能清单](../FEATURE_STATUS_AND_IMPLEMENTATION_PLAN.md)；
> 原文中的计划、主机状态、测试数字及“正在进行”等措辞均为历史检查点。
> 代码块和仓库相对产物路径保留原意；本机忽略的 `artifacts/` 不随克隆分发。

[文档入口](../README.md)

## 原报告索引

- [自定义代理配置验收](#agent-configuration-acceptance) — `AGENT_CONFIGURATION_ACCEPTANCE.md`
- [Automation plan-list acceptance](#automation-list-acceptance) — `AUTOMATION_LIST_ACCEPTANCE.md`
- [Automation mutation acceptance](#automation-mutation-acceptance) — `AUTOMATION_MUTATION_ACCEPTANCE.md`
- [定时任务独立页面布局](#automation-page-layout-acceptance) — `AUTOMATION_PAGE_LAYOUT_ACCEPTANCE.md`
- [Automation editor and row-action workflows](#automation-ui-workflow-acceptance) — `AUTOMATION_UI_WORKFLOW_ACCEPTANCE.md`
- [Automations and inspector acceptance](#automations-inspector-acceptance) — `AUTOMATIONS_INSPECTOR_ACCEPTANCE.md`
- [Production subagent concurrency acceptance](#subagent-concurrency-native-acceptance) — `SUBAGENT_CONCURRENCY_NATIVE_ACCEPTANCE.md`
- [Subagent dialogue projection optimization](#subagent-dialogue-projection-acceptance) — `SUBAGENT_DIALOGUE_PROJECTION_ACCEPTANCE.md`
- [子代理链路验收](#subagent-execution-acceptance) — `SUBAGENT_EXECUTION_ACCEPTANCE.md`
- [Subagent native lifecycle acceptance](#subagent-lifecycle-native-acceptance) — `SUBAGENT_LIFECYCLE_NATIVE_ACCEPTANCE.md`
- [Subagent manual approval and tool-output keyboard acceptance](#subagent-manual-approval-acceptance) — `SUBAGENT_MANUAL_APPROVAL_ACCEPTANCE.md`
- [Subagent runtime configuration acceptance](#subagent-runtime-configuration-acceptance) — `SUBAGENT_RUNTIME_CONFIGURATION_ACCEPTANCE.md`
- [Subagent settings-to-runtime acceptance](#subagent-settings-to-runtime-acceptance) — `SUBAGENT_SETTINGS_TO_RUNTIME_ACCEPTANCE.md`
- [工作区代理选择验收](#workspace-agent-selection-acceptance) — `WORKSPACE_AGENT_SELECTION_ACCEPTANCE.md`

---

<a id="agent-configuration-acceptance"></a>

## AGENT_CONFIGURATION_ACCEPTANCE.md

<!-- source: AGENT_CONFIGURATION_ACCEPTANCE.md -->
### 自定义代理配置验收

#### 2026-10-06 草稿、目录刷新和实际请求

##### 设计自检

- 行为由 App 设置编辑器所有，复用 `IAgentConfigurationRepository`、
  `IConfigurationRepository`、编辑修订号、共享写入执行器与受保护异步命令。
  没有新增 UI 到具体存储、HTTP、文件或进程的依赖，不添加第二套代理执行器。
- 点击保存时先捕获不可变配置和稳定草稿身份；成功保存旧快照不能重置更新的
  字段、另一条配置或新建上下文。删除同样只针对点击时捕获的身份。
- 保存、删除和刷新拒绝重入与交叉写入；两种命令入口都检查可执行状态，
  被拒绝的再次点击不会取消已经接收的操作。窗口释放解除通知并释放编辑器。
- 关联所属生命周期和命令取消令牌，30 秒后请求取消；读取及工作区通知有界等待。
  不配合取消的存储写入仍保有互斥权直至返回，不声称强制终止或回滚写入。
  授权、审批、工具输出、审计与数据库结构不变，无需迁移。
- 原有八档思考强度、16 条新建上限及提示词验证保持不变。手写类型各自独立
  文件，文件和方法未超过硬上限；正常行为及失败、取消、失效目标都有定向测试。

##### 已复现和修复

- 四项新增草稿回归在旧实现实际失败：刷新清空未保存字段、迟到保存覆盖新改动、
  保存重置新建上下文，以及删除覆盖另一条配置的草稿。现均通过。
- 模型配置变化只刷新自定义代理的模型目录，不再调用会重置编辑器的全量加载。
  保留提供商/模型身份和明确的思考选择；被删除或禁用的模型不自动换成另一个。
  列表读取失败保留旧行和草稿，但禁止写入，成功刷新后恢复。
- 等值对象也显式恢复绑定的标准选择引用；未变化的集合保持原实例，避免刷新
  下拉框、列表和滚动位置。真实 Headless 页面覆盖明暗主题及两种窗口尺寸。
- 保存后的工作区通知失败显示“配置已写入”，不把它误报成存储失败；刷新可重试
  通知且不重复写入。另一项实际失败回归证明：保存已提交但后续目录读取失败，
  不能据旧列表显示“已删除”。只有可用、完成的快照才决定目标失效状态。
- 迟到结果、释放、命令取消、读取超时、直接命令重入、模型重载期间编辑、删除
  目标和安全错误提示均有拒绝路径。异常文本不进入用户提示。

##### 从设置到运行的证据

`AgentWorkspaceFlowTests` 复用实际 App 装配、设置命令、SQLite、工作区发送命令、
生产 Responses 提供商与本机确定性 SSE 服务，不是仅断言设置属性。

- 两项参数化成功链路选择非默认模型，实际 HTTP 请求包含所配置的模型、
  high 或 ultra 思考强度及自定义提示标记；回复和完成状态通过工作区仓储保存。
- 运行中编辑并保存代理不会改变已发出的请求，也不会隐藏停止操作。
  当前请求仍使用原模型、high 和旧提示；下一轮真实请求使用新模型、low 和新提示。
- 模型设置命令贯通目录变化、禁用和删除；关闭窗口后不再重载编辑器。
  本轮没有修改子代理调用实现，子代理的独立触发、共享和对话验收另见
  [SUBAGENT_EXECUTION_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#subagent-execution-acceptance)。

##### 普通权限窗口、输入与截图

仓库外宿主使用实际 App/MainWindow、隔离临时 SQLite 与合成代理，日志确认
`QA_ELEVATED: False`。未修改用户配置、真实项目或真实账号，不发送付费请求。

- 浅色 1920×1032：实际聚焦名称、系统键盘全选并输入 `review-fixed`、点击刷新，
  确认新草稿与原模型/high 仍保留；再实际保存，列表、字段和成功提示同步更新。
  对合成 SQLite 的只读查询确认 `review-fixed`、high、启用状态 1。
- 深色 980×640：实际滚轮滚到保存区、点击保存，完整提示位于全宽按钮下方，
  保持原滚动位置，按钮和提示未被挤窄或截断。
- 页面复用共享行、字段、按钮和状态样式，仅将操作及反馈改为两个有限宽度的
  结构行，不添加局部颜色、尺寸、阴影或另一套控件。

宿主、脚本、数据、日志和图片均位于仓库外
`%TEMP%/MossAgent-agent-editor-qa-20261006-01`：

| 图片 | 原始尺寸或含义 |
| --- | --- |
| `agent-light-before.png` | 1920×1032，键盘编辑前 |
| `agent-light-refreshed.png` | 1920×1032，刷新后保留尚未保存的名称和模型 |
| `agent-light-saved.png` | 1920×1032，实际保存后的列表和结果 |
| `agent-refresh-draft-comparison.png` | 同尺寸原图的未缩放并排裁片，不是旧实现修复前截图 |
| `agent-dark-saved-final.png` | 980×640，深色窄窗口实际保存后 |

被动 Python 捕获只查找指定 PID 并获取窗口绘制帧，拒绝黑色/均匀帧、错误尺寸
及捕获期间变化的边界，不移动或拉伸窗口。两个临时宿主均正常关闭、退出码 0，
并复核没有遗留进程。深色就绪日志曾打印旧布局尺寸，以实际原生边界和图片为准。
最后的读取失败/删除事实修复由回归验证，不声称原生窗口进行了存储故障注入。

##### 最终自动化结果与限制

- 新增 32 项回归，编辑器、目录、运行链路及窗口等定向 84 项通过。
- 完整解决方案 2,141 项通过：App 1,652、Agent 47、Browser 32、
  Infrastructure 58、Windows 50、Providers 155、Tools 147；构建 0 警告、0 错误。
- 本轮十一个 C# 文件空白格式检查、931 个 C#/AXAML 的单类型及文件/方法
  硬上限审计、差异空白检查通过。没有暂存、提交或推送。
- 最终日志为 `editor-focused-final.log`、`solution-tests-final.log`、
  `solution-build-final.log`、`format-final.log`、`audit-final.log`；
  五项生产问题失败基线在 `mutation-baseline.log` 和 `post-save-read-baseline.log`。
  验证期间的夹具参数和按钮文字匹配错误不计作生产缺陷复现。
- 本机协议证明请求路由和持久化链路，不证明真实远端模型支持所有思考档位。
  原生输入覆盖上述设置操作，不等于全部任务、DPI、协议、异常交互或 Codex
  全页面逐像素一致。工作区代理目录的异步读取及选择失效仍需独立生命周期验收。
  整个产品目标仍未完成。
<!-- /source: AGENT_CONFIGURATION_ACCEPTANCE.md -->


---

<a id="automation-list-acceptance"></a>

## AUTOMATION_LIST_ACCEPTANCE.md

<!-- source: AUTOMATION_LIST_ACCEPTANCE.md -->
### Automation plan-list acceptance

#### Scope (2026-10-09)

This continuation repairs plan-list presentation and read ownership, not scheduling
or database failures. A pending first read no longer appears as an empty catalog.
Failure has its own safe message and real retry action. Refresh keeps previous cards
visible but blocks snapshot-dependent mutations and history navigation. Stop remains
available for a currently owned running plan. Successful retry clears the read error.

The editor and removal dialog share a list notice with retry; absence of a notice
allocates no extra notice spacing. Retrying does not discard an open editor's draft.
Row actions reject foreign objects and objects replaced by refresh, including direct
command invocation. A pending removal target is rebound only to an equal current
schedule; changed or missing targets dismiss confirmation and report conflict.

#### Design and safety self-check

1. App owns observable list presentation and exact enabled-model catalog resolution.
   Reads reuse `IAutomationRepository`, `IWorkspaceRepository` and
   `IConfigurationRepository`; dependency direction remains unchanged. UI does not
   read SQLite, files or provider HTTP directly.
2. Execution continues through the existing Application scheduler and task-scoped
   runner. No new execution engine, provider fallback or task-creation path is added.
3. Reads coalesce into one owned operation. Scheduler notifications request a
   follow-up read rather than losing a state change during an existing read. The
   entire chain has a linked 30-second cooperative cancellation deadline; disposal
   cancels the owner and rejects late publication. A noncooperative port retains
   ownership until it returns. This does not change tool authorization, approval,
   execution timeouts, output limits or audit policy. Exception details are not
   rendered or logged by the list; history remains limited to 50 records.
4. New handwritten C# files each contain one type and remain below file/method hard
   limits. Views use shared Controls and appearance variants; existing markup and
   analyzer gates remain authoritative. JIT debugging and Windows x64 single-file
   publishing remain unchanged. No JSON/AOT changes or persistence migration occur.
5. Focused tests cover successful recovery, retained selection/snapshots/drafts,
   rejected foreign/replaced commands, pending-read blocking, running-task Stop,
   catch-up reads, reentrant observers, cancellation/disposal and safe error text.
   Execution tests use the existing deterministic loopback SSE provider and inspect
   persisted cancellation, rather than asserting a fake success status.

#### Verification evidence

Evidence root: `%TEMP%\MossAgent-automation-list-20261009`. Python scripts, native QA
source/executable, synthetic data and pixel captures remain outside repository source
or in ignored validation artifacts. Nothing is submitted, pushed or published.

- The reproduced initial red run fails all 11 selected cases. Six added test files
  contribute 24 cases. The final automation-focused run passes 79/79 against the
  rebuilt assembly, including the final test-only formatting refactor.
- Final scoped whitespace verification passes. The solution build reports zero
  warnings and zero errors. The final full regression passes 3,326/3,326 cases in
  seven completed TRX files under `final-full/`: App 2,523, Tools 188, Providers 283,
  Infrastructure 136, Browser 76, Agent 63 and Platform.Windows 57; none fail or skip.
  The scoped Roslyn audit checks 11 C# files with one type each, no syntax errors,
  at most 224 physical lines and at most 34 lines per method/accessor. The three
  scoped AXAML files parse successfully and remain below 300 lines. Tracked and
  17-path scoped diff checks pass using the repository's normal newline conversion;
  no index entries are staged.
- Native QA uses real App startup/shutdown with ordinary privileges, JIT, isolated
  SQLite data and paused read-only schedules. A repository decorator blocks the first
  read and then throws a synthetic I/O exception; retry reads the actual SQLite
  repository. This proves handling of injected failures, not repair of a real SQLite
  or filesystem incident.
- Passive Python PrintWindow captures reject black frames and wrong dimensions.
  Light 1280x820 and dark 980x640 at render scale 1 cover loading, failure, actual
  mouse retry, restored cards and an opened editor with reachable footer. Light Escape
  and dark mouse Cancel dismiss the editor. Both final cases exit normally with plans
  still paused/read-only, zero new runs, zero new conversations and zero decorator
  writes; the workspace draft remains unchanged.
- Same-size light before/after comparisons use native crops without scaling. The
  old page misleadingly shows empty results while loading/failed and retains its
  old error after refresh. The new page shows distinct loading/failure states and
  removes the old error after retry. These compare MossAgent revisions, not Codex.
- Windows Computer Use needed a fresh screenshot-backed observation after a
  geometry-unavailable mouse call. Retry, edit and Cancel outcomes were then
  verified against the actual UI and native persisted-state checks. A stale cached
  tree or null tree alone is not accepted as evidence of the action's result.

#### Remaining limits

- Native failure/retry while an editor is already open is covered by headless tests,
  not by this native run. Noncooperative timeout ownership has deterministic test
  coverage, not a separately timed native database experiment.
- Same-state current Codex reference pixels, higher DPI, user-font combinations,
  exhaustive hover/focus and pixel-perfect automation-page geometry still require
  comparison. No claim of one-to-one Codex fidelity or full product completion is made.
- No real paid model request, plan enabling, saving or removal is performed in native
  QA. Scheduler, migration, authorization and model routing are unchanged here.

#### Metadata projection optimization (2026-10-12)

Design self-check for this focused refactor:

1. App owns row projection, using the existing repository and enabled-model ports;
   no dependency or direct infrastructure access is added.
2. Each successful read uses temporary identity indexes instead of scanning project
   and model catalogs for every row. No shared or cross-refresh cache is introduced.
3. Existing authorization, approval, 30-second cancellation, read ownership, audit
   and output limits remain unchanged. Fresh row identities and selection notices
   must remain intact because they invalidate stale actions and refresh history.
4. The existing list owns the small projection method. Handwritten C# retains one
   type per file and stays within the file and method limits; no style or JSON change.
5. Regression tests cover a bounded single project scan, duplicate first-match
   behavior, composite provider/model identity, unavailable or disabled metadata,
   empty lists, fresh reloads and immutable earlier rows. Existing automation tests
   retain command ownership, history refresh, cancellation and disposal coverage.

Implementation and verification:

- `AutomationPlanListViewModel` projects the current catalog through temporary
  first-match project and composite provider/model indexes, then builds one row
  array in schedule order. Empty plan snapshots skip indexing. Ownership checks
  use the sealed row type's reference equality via `Contains`, without a predicate
  closure. No row cache or collection reconciliation is added: replacement,
  selection rebinding, stale-command rejection and history refresh stay unchanged.
- The reproduced baseline passed 10 new cases and failed the single-scan case:
  32 rows over 64 projects performed 2,048 project-element reads. All 11 new cases
  pass after correction; the same case now requires exactly 64 reads. This proves
  removal of repeated catalog scans, not an application-wide latency improvement.
- The final automation/audit-focused run passed 207/207. The solution build passed
  with zero warnings/errors, and the repository-wide architecture checks passed
  7/7. Scoped whitespace verification passed. Roslyn checked the three scoped C#
  files: one type each, 136/164/24 physical lines, maximum method/accessor lengths
  28/33/5, and no syntax errors.
- The final-build App assembly passed 3,757/3,757, with zero failures/skips and no
  hang dump, in the completed `tests/projection-final-full.trx`. This includes the
  11 new projection cases and the preceding audit-presentation regressions. Test
  workers exited before any further build; no overlapping DLL rebuild was used.

External baseline, TRX and evidence directory:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-automation-projection-20261012-104438`.
This slice does not change visual controls, layout, styles, scheduler execution,
repository contracts, data schemas, packaging or JSON. No new native screenshot,
real-account or paid-provider acceptance is claimed; the prior native evidence and
the full-product goal remain separate.

#### Editor and history refresh work reduction (2026-10-13)

This slice removes repeated presentation work, not scheduler behavior or visual
styles. Editor initialization previously rebuilt and validated its schedule after
each changed field, then rebuilt it again explicitly. History selection cleared
its rows before reading and then cleared them a second time during projection.

Design and safety self-check:

1. App owns editor initialization and history presentation. Existing Application
   and repository ports remain authoritative; dependency directions are unchanged.
2. No new port, execution path, cache, storage contract or migration is introduced.
   Editor preview uses the existing schedule validation and timing calculation.
3. History retains the 30-second linked cancellation deadline, selection/version
   guards, disposal behavior and navigation ownership checks. It publishes at most
   50 matching records in source order, including separate rows for duplicates.
   The change does not execute a schedule or alter approvals, models or tools.
4. An instance-owned initialization scope suppresses only intermediate preview
   calculations. Every changed field still publishes its normal notification and
   advances the draft revision. Context revisions remain unchanged. A finally
   block restores the scope, including after failed or nested initialization.
   The five scoped C# files contain one type each and are all under 200 lines.
5. Eight added cases cover complete-draft initialization/reopening, individual
   invalid edits, recovery after failed initialization, single collection resets,
   old-row rejection, duplicate ordering, foreign records and bounded reads.
   Existing cancellation, disposal, navigation and save-ownership tests remain.

Verification:

- The seven-case baseline fails three cases and passes four. The synthetic editor
  opening performs nine clock/preview calculations instead of one; both empty and
  nonempty histories emit two resets instead of one. The corrected cases require
  one calculation and one reset. This proves reduced work for these operations,
  not an application-wide latency or memory improvement.
- Final automation-focused App tests pass 126/126, including all eight new cases;
  visual smoke tests pass 12/12 and repository architecture checks pass 7/7.
- The solution builds with zero warnings/errors. Repository `git diff --check`
  and explicit whitespace checks for the scoped untracked source files pass.
  Tests use an ignored `.scratch` JIT output directory; the already-running native
  QA application's default-output assembly is not replaced or restarted.
- External logs and TRX results:
  `C:\Users\wzheng\AppData\Local\Temp\MossAgent-code-cleanup-20261013-071648`.

The complete App suite was not rerun in this slice. No new native screenshot,
same-state Codex comparison, real-account or paid-provider result is claimed.
Packaging remains Windows x64 single-file/JIT; JSON and AOT settings are unchanged.
The full-product goal and its remaining native acceptance work remain separate.

#### Production scheduler execution and read-only restoration (2026-10-13)

These are real production-App scheduler/runner executions against a deterministic
loopback Responses service, not paid-provider or real-account acceptance. Original
evidence is in `%TEMP%\MossAgent-automation-production-20261013-0052`.
The executing App assembly SHA256 recorded in `runtime-files.json` is
`D60261459E5BE56095641242305D55A464DED6866AD359AD96FE9E7F99E3565C`.
Later rebuilt UI assemblies are not substituted for this execution identity.

| Run | Local time (UTC+8) | Entry and outcome |
| --- | --- | --- |
| `01116e07-f25c-4b56-bf45-69a413ba1fd4` | 00:54:13 to 00:55:57 | Scheduled execution completes; final synthetic assistant message persists. |
| `e0f213d3-348c-4af5-82b5-e8e55fe6e659` | 00:59:26 to 00:59:44 | Manual execution is cancelled; the server records connection disconnection and no assistant success message. |
| `a830f51c-5f00-4988-a97e-6efd4d3b9872` | 07:08:15 to 07:08:16 | Manual execution receives synthetic HTTP 503 and reaches Failed, with no assistant success message. |
| `6be7fe5e-4716-4051-8901-bb5142c703f4` | 07:09:10 | Explicit new manual execution succeeds after failure; it owns a new task rather than replaying the failed run. |

All times are on 2026-10-13. The four independent tasks use project B and saved
ReadOnly policy. Whitelisted request logs record `concurrency-b`, high reasoning,
stream=true and the synthetic prompt flag for each request; credentials and raw
request bodies are not logged. The final schedule is paused. Database checks
confirm Completed/Cancelled/Failed/Completed run states, manual flags 0/1/1/1,
six total tasks, unchanged original 15 tool audits and three hash-verified
artifacts, and synthetic assistant replies only in the successful runs.

Native screenshots cover light normal automatic execution and history, dark
narrow cancellation, and dark maximized failure/recovery history and opened
conversations. They establish those execution/presentation paths, not equal-state
Codex pixels or the current rebuilt UI's complete theme/size matrix. The first QA
process is later found absent between the cancellation and recovery observations;
the exit cause was not observed, so neither normal close nor crash is inferred.

Subsequent ordinary-permission read-only reopen checks use the production App with
the plan paused and an observer that records then rejects unexpected requests.
The prior window-state assembly is
`319A951FBB3562556F23F378B0EB6648059445D29B914560D107217D0E5D4FEC`;
the latest caption assembly is
`38B1E1992B6280581F6C7634A69DFC1AAC1FCC17436E6B93C66A3733601A2074`.
Their audit files retain all four terminal runs and only four historical requests.
No new scheduler execution is claimed for these rebuilt versions.

[WINDOW_CAPTION_FIDELITY_ACCEPTANCE.md](UI_WORKFLOWS.md#window-caption-fidelity-acceptance) separates the earlier native inspector,
history and minimize/restore sweep from the current caption build's mismatched
Sky screenshot and black-padded PrintWindow frame. Current-version native
interaction acceptance remains open; the full-product objective remains active.
<!-- /source: AUTOMATION_LIST_ACCEPTANCE.md -->


---

<a id="automation-mutation-acceptance"></a>

## AUTOMATION_MUTATION_ACCEPTANCE.md

<!-- source: AUTOMATION_MUTATION_ACCEPTANCE.md -->
### Automation mutation acceptance

#### Scope (2026-10-09)

This continuation fixes real plan-editing and mutation ownership. Previously Save
had no cancellable command token, relinquished busy state before read-back, closed
newer/replaced drafts on late completion and could publish conflict after disposal.
Direct rejected invocation also replaced the command's existing execution task.
The initial seven regression cases fail against that implementation.

App-owned operation state now covers editor loading, save, enable/pause, removal
and manual-run start. Guarded commands reject disabled invocation before touching
the existing operation. Shared notices show meaningful progress and an actual cancel
action; a read-back phase is not presented as cancellable storage rollback.

The visual tests additionally reproduce a broken enabled binding: assigning the
editor's own DataContext caused its `IsBusy` binding to resolve against the wrong
model. It now explicitly resolves the owning dialog model, so pending save really
disables fields while leaving the independent cancel action available.

#### Design and safety self-check

1. App owns observable operation state, editor coordination and draft revisions.
   Existing repository/catalog ports, Application scheduling and task-scoped
   execution remain authoritative. Dependency direction and composition stay valid;
   production UI does not access files, processes, SQL or provider HTTP directly.
2. Existing guarded commands and repository/scheduler contracts are reused. There
   is no new execution engine, provider fallback, serialization contract or migration.
   New plans stay paused/read-only. Manually starting a run still requires an explicit
   action and uses the existing approval, cancellation, output and audit pipeline.
3. Catalog/write work receives linked command and lifetime cancellation plus a
   30-second cooperative deadline through TimeProvider. Cancel reaches the port but
   cannot forcibly interrupt a noncooperative write. Ownership persists until return
   and through read-back; the list owns its separate 30-second read deadline. Busy
   commands block reentry and history navigation, not the running task's Stop.
   Disposal rejects late UI publication and new work. Safe localized failure hints
   never display raw exception text or claim that an uncertain operation rolled back.
4. One type per handwritten C# file remains enforced. The scoped Roslyn audit checks
   14 files with no syntax/size violations: maximum 213 physical lines and maximum
   44 lines per method/accessor. Views reuse shared Controls and existing appearance
   variants. The new notice and modified page remain below AXAML size limits; markup
   and analyzer gates pass. JIT debugging and win-x64 single-file publishing remain
   unchanged; no AOT/JSON migration is introduced.
5. Twenty added cases cover cancellation, noncooperative timeout ownership,
   rejected synchronous/asynchronous reentry, late disposal, superseded contexts,
   retained newer drafts, post-commit external changes, failed read-back and recovery,
   successful cancelled writes, bound/reachable controls and guarded cancellation.
   Existing deterministic execution and safety tests remain in the focused/full runs.

#### Save identity and draft contract

- Success records the committed ID/revision before read-back. A new draft saved
  before cancellation does not become a second plan on the next explicit Save.
- Read-back must match expected revision, content and millisecond timestamp before
  rebasing or closing. External edits are not silently accepted as the draft baseline.
  The known committed identity remains retained even after a conflicting read.
- An unchanged current draft closes only after successful matching read-back. A
  newer draft retains its fields and uses the saved identity/revision for the next
  Save. A replaced/reopened context cannot be closed or receive an old notice,
  including reopening during an observable snapshot notification.
- Failed read-back preserves the editor and records a safe saved-but-unconfirmed
  notice. Successful retry confirms the stored result without another write.
- Cancelled writes may still commit. The UI reports that possibility, retains the
  draft and confirms/rebases only the actual stored result. There is no automatic
  mutation retry or rollback. A started background run must use its existing Stop.

#### Verified evidence

Evidence root: `%TEMP%\MossAgent-automation-mutations-20261009`.

- Initial red run: 7/7 fail. Final automation-focused run: 99/99 pass. Full solution
  regression: 3,346/3,346 in seven completed TRX files: App 2,543, Tools 188, Providers
  283, Infrastructure 136, Browser 76, Agent 63 and Platform.Windows 57. None skip.
- Final solution and native QA builds report zero warnings/errors. Scoped whitespace
  verification passes without a repair iteration. Source AST/XML and diff checks
  preserve the repository constraints and normal newline handling.
- Native QA runs real App startup/shutdown at ordinary privilege, managed/JIT,
  render scale 1, Light 1280x820 and Dark 980x640, with separate SQLite/project data.
  Its provider is deliberately non-executable and the seeded plan remains paused
  and read-only. A repository decorator delays the first save before storage; actual
  mouse cancellation reaches that port. SQLite read-back confirms no first-write
  changes while the editor retains its changed name and enables explicit Save again.
- The second real mouse Save updates the same stored plan and closes the editor.
  Normal shutdown confirms exactly two attempts, one committed write, one revision
  increment, unchanged plan identity, zero runs, zero conversations and intact
  workspace composer draft in both cases. No user schedule or provider is changed.
- Passive Python PrintWindow captures check non-black pixels and exact dimensions.
  Saving/cancelled/saved frames cover disabled fields, live cancel, safe error,
  retained name and restored cards. Footer actions remain reachable at 980x640.
  Python screenshot/montage scripts, QA source, data and captures stay outside source
  or in ignored validation artifacts and are not submitted or published.
- One accessibility SetValue call timed out after successfully changing the light
  field. A fresh observed value verified the outcome; no duplicate input was sent.
  The dark case uses observed field focus and real keyboard text input. Cached or
  null accessibility output is never accepted alone as proof of the input result.

#### Remaining limits

- Same-state current Codex plan/editor pixels, high DPI, user fonts and exhaustive
  hover/focus still need comparison. These frames verify MossAgent behavior and
  rendering, not one-to-one Codex equivalence or full product completion.
- Noncooperative commit/timeout, external conflict, failed read-back and draft
  replacement are deterministic tests, not additional native database experiments.
  No real paid model request, enabling, removal or security-setting change is made
  in native QA. Scheduler, persistence schema, model routing and tool policy do not
  change in this slice.
<!-- /source: AUTOMATION_MUTATION_ACCEPTANCE.md -->


---

<a id="automation-page-layout-acceptance"></a>

## AUTOMATION_PAGE_LAYOUT_ACCEPTANCE.md

<!-- source: AUTOMATION_PAGE_LAYOUT_ACCEPTANCE.md -->
### 定时任务独立页面布局

#### 设计自检

1. 布局属于 App 的 `MainWindow.axaml`，不改变模块依赖方向。
2. 复用 `AppPageSwitcher`、`AppNavigationPanes` 和既有页面命令，不新增端口、控件、
   全局状态或服务定位器。
3. 不改变定时运行、审批、授权、超时、取消、输出或持久化；页面切换不得发送草稿。
4. 生产代码仅调整 AXAML 组合；新增测试单文件单类型，低于文件和方法硬限制。
   共享颜色、尺寸、圆角及阴影保持原定义，JIT/JSON/Windows x64 单文件发布不变。
5. 验证定时任务占用整个主内容框、项目侧栏不可见且不可交互、紧凑抽屉关闭，以及
   设置/对话往返后保留侧栏偏好、项目、草稿和检查器状态。

#### 根因与修改

原定时任务页面位于 `AppNavigationPanes.PrimaryPane` 内，因此与对话页共用项目
侧栏；宽窗口保留了 264 DIP 侧栏，窄窗口还可能保留已打开的抽屉。

将工作区的页面切换器移到导航布局外：对话页仍拥有完整导航/检查器实例，定时任务
则是它的同级页面。页面切换不修改 `IsLeftPanelOpen`，返回对话时恢复原来的展开
偏好；隐藏的紧凑抽屉由既有控件生命周期关闭。最左侧导航轨保留，仍可切换页面。

定时任务页同步隐藏标题栏的项目侧栏按钮及其背景色块，防止显示无效入口或残留
侧栏底色。进入设置后仍按设置自己的宽/窄导航状态显示切换按钮。没有新增页面私有
颜色、尺寸、边框或阴影，也不销毁或重建会话控件。

#### 已完成验证

- 新增 14 个真实 MainWindow Headless 布局/键盘交互用例：明暗主题、526×601、
  1280×820、1920×1032，分别覆盖原侧栏展开/收起及窄窗口抽屉切换后再放宽。
- 修复前 14/14 失败；修复后 14/14 通过。检查主内容框的坐标/宽度、侧栏隐藏与
  命令不可执行、标题栏按钮不可聚焦、焦点不留在隐藏侧栏及页面往返状态。
- 所有新增路径均保持草稿和项目，且本地模型服务请求为零，不创建或执行定时计划。
- 扩展首轮为 138/144：六个旧导航测试仍查找定时任务页中已隐藏的侧栏按钮。
  更新为可见导航轨的新对话入口，以及项目/最近任务数据上的真实显式导航命令，
  继续检查相同任务身份也能返回对话、历史隐藏、草稿保留和零模型请求，不模拟
  点击隐藏控件。原始六项失败报告保留，没有删除用例或放宽原有结果断言。
- 最终同一范围 144/144 通过，无跳过，包含全部 14 项新增，以及定时任务编辑/
  历史、页面切换、窄窗口导航、设置往返、检查器布局和全局审批呈现回归。新增用例
  另检查定时任务 → 窄窗口设置时仍能正常开关设置自己的导航抽屉。14 不重复计数。
- 完整解决方案构建零警告、零错误；新增 C# 的空白格式、单类型/规模和 AXAML
  绑定/视觉封装构建检查通过。

原始日志和 TRX 位于仓库外
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-automation-page-20261015`。
定向结果不与其他测试报告重复计数。没有暂存、提交、推送或发布。
独立脚本检查三份最终/基线 TRX 的计数、唯一用例身份、全部新增用例与初轮失败，
并确认生产 App 和测试依赖副本的程序集哈希一致；`verification.json` 保留结果。

#### 验证边界

用户先前明确选择“稍后再截图”，本次未恢复桌面操作、截取原生窗口或重启用户正在
使用的实例。验证来自真实生产视图的 Headless 布局和交互，不冒充原生截图对比。
当前已打开的旧实例不会自动加载新布局，需要之后运行新构建验证。

本次没有重跑完整解决方案测试；此前完整运行中的无关分配量断言失败仍保留，不能
把本次定向通过解释为全量已经全绿。整个产品的一比一还原与原生 DPI 验收仍未完成。
<!-- /source: AUTOMATION_PAGE_LAYOUT_ACCEPTANCE.md -->


---

<a id="automation-ui-workflow-acceptance"></a>

## AUTOMATION_UI_WORKFLOW_ACCEPTANCE.md

<!-- source: AUTOMATION_UI_WORKFLOW_ACCEPTANCE.md -->
### Automation editor and row-action workflows

#### Scope and design self-check

This slice extends the UI/UX review from appearance to actual visible-control
behavior. It adds four test files, not a production UI or scheduler correction.
The earlier shared focus/disabled-state correction remains documented separately
in [SHARED_INTERACTION_STATES_ACCEPTANCE.md](UI_COMPONENTS.md#shared-interaction-states-acceptance).

1. App owns input and presentation, Infrastructure owns scheduling/persistence,
   and the existing Agent/provider pipeline performs the run. No dependency
   direction changes.
2. Tests reuse the production composition, shared visual controls, repository
   ports, scheduler and protocol adapter; no alternate execution path is added.
3. Each fixture owns isolated SQLite data and a loopback model server. Tests use
   ReadOnly schedules and cancellation; no existing desktop window, user data,
   paid endpoint or approval setting is accessed.
4. The four handwritten C# files each contain one type and remain within file
   and method limits. Evidence and the source-audit utility stay outside the repo.
5. Eight connected cases cover success and rejection/cancellation/deletion in
   Light/Dark at 526x601 and 1280x820 using production MainWindow controls.

#### Actual UI paths

- The navigation-rail entry opens automations. New opens the real editor.
  Keyboard input supplies the name and prompt. F4 opens project/model/frequency
  dropdowns, and hit-tested popup items select their actual options. Native
  numeric text fields receive keyboard input and commit on blur.
- Save persists the chosen nondefault model, two-hour interval, three-minute
  timeout and ReadOnly policy. A new schedule stays paused and sends no request.
- Enable and Pause update the stored schedule. Run Now creates a separate task;
  it does not consume the foreground draft, selected model or current task.
  Duplicate runs and deletion are unavailable while that run is active.
- Recorded Responses requests verify the saved model ID, medium reasoning effort
  and exact saved prompt in the user message. The controlled server completes the
  run; its stored history opens the corresponding completed conversation. The
  final Markdown answer is hit-tested inside the visible window.
- Invalid Save preserves the name/editor, reports an error and creates neither
  a stored schedule nor a model request. Cancel dismisses the editor.
- Stop produces Cancelled run/task records. Opening that history does not make
  another model request. Escape cancels deletion; confirmation removes the
  schedule while preserving its run and task history. The request count stays one.

Compact actions use keyboard Space after focus/scrolling; wide actions use
hit-tested pointer input. These are Headless Avalonia inputs, not operating-system
desktop automation. State waits include asynchronous command and list/history
refresh completion, not only command invocation.

#### Test development and visual observations

Initial failures came from fixture assumptions: Home after F4 did not select a
popup item, and CloseEditorCommand identifies both an icon and a Cancel button.
The fixture now clicks the popup item and disambiguates Cancel by localized text.
An expanded request assertion then incorrectly expected content parts, whereas
the production Responses builder emits string content for these text messages.
It now checks that exact string, retaining model/effort/prompt assertions. The
original 152-case run records 148 passes and those four assertion failures; it
is not a reproduced production-red baseline.

Raw saved/running/editor/history screenshots and ancestor geometry are retained.
In compact mode a card exceeds the finite list viewport: scrolling legitimately
clips its rounded edge. Its actual radius is 12 DIP and shared shadow remains
applied. This observation does not justify disabling viewport clipping or adding
page-local corner/shadow overrides.

#### Validation

- `automation-app-verified.trx`: all 152 App automation cases pass, including
  the eight new connected UI cases. `automation-infrastructure.trx`: all 41
  scheduler/timing/repository/migration cases pass. `architecture.trx`: all seven
  architecture cases pass. These disjoint final scopes total 200, with no
  failures, skipped/not-executed or aborted cases and unique execution identities.
  Earlier eight-case and four-case subsets are not added again.
- After all test processes finish, the complete solution build succeeds with
  zero warnings/errors. Eleven recorded test/runtime DLL hashes remain unchanged
  across that build. Scoped whitespace-format and conflict/trailing-whitespace
  checks pass. Roslyn source audit confirms one type per file, 66-118 physical
  lines per file and a maximum method span of 54 lines.
- Evidence is outside the repository at
  `%TEMP%/MossAgent-automation-ui-20261016`: raw TRX, logs, state PNGs/geometry,
  `verified-test-counts.json`, `source-size-audit.json` and before/after hash CSVs.
  The full pixel suite is not repeated for these test-only changes; its earlier
  208-case result remains separate historical evidence, not part of the 200.

#### Remaining gates

This slice does not restart the composition, establish restart/no-replay behavior,
or exercise every time-zone/DST rule through the UI. Scheduler/repository tests
retain their own bounded evidence. Opening existing history without a new request
is not the same claim as restart recovery.

Native screenshots and existing-window operations remain user-postponed.
Headless rendering does not establish native Windows DPI, same-state Codex parity
or a complete current-product visual/interaction sweep. The earlier App allocation
failure remains unexplained; these subsets do not replace it or a full eight-suite
checkpoint. No packaging, JSON, AOT/JIT, staging, commit, push or release changes
are made; the full product goal remains active.
<!-- /source: AUTOMATION_UI_WORKFLOW_ACCEPTANCE.md -->


---

<a id="automations-inspector-acceptance"></a>

## AUTOMATIONS_INSPECTOR_ACCEPTANCE.md

<!-- source: AUTOMATIONS_INSPECTOR_ACCEPTANCE.md -->
### Automations and inspector acceptance

#### Scope (2026-10-08)

The previously disabled automation entry now opens a retained application page.
It supports new/edit/save, explicit project and model selection, hourly/daily/weekly
timing, time zones, approval policy, execution timeout, enable/pause, manual run,
stop, confirmed removal, bounded history and opening the actual run conversation.
Switching pages does not replace the workspace or consume its draft.

The inspector has persistent sources/diff/terminal/browser/audit tabs, a back action
and a close action. Header actions share 32-pixel hit areas and 15-pixel icons.
The existing floating context card remains visible while docked inspection is closed.
Empty, loading and safe failure states replace unusable blank browser/preview frames.

#### Design and safety self-check

1. Domain owns schedule/run values. Application owns timing and scheduling through
   `IAutomationRepository`, `IAutomationLease` and `IAutomationTaskRunner` ports.
   Infrastructure owns SQLite/lease implementations. App owns observable presentation
   and the bridge to the existing workspace runner; no dependency points back to UI.
2. Background execution reuses workspace task creation, run tracking, provider streams
   and the standard tool pipeline. It does not implement another execution engine.
   Missing/disabled configured projects or models fail instead of silently falling back.
3. New schedules are paused and read-only. An explicit manual run may invoke a paid
   configured model even when the plan is paused. Tool authorization, approvals,
   task-owned cancellation, timeouts, bounded output and artifact storage remain in
   the standard pipeline. Terminal buttons also use that pipeline. No real external
   model request is part of the automated acceptance tests.
4. Each handwritten C# file contains one type. The scoped AST audit checks the
   500-line file and 80-line method limits. New shared appearance variants, markup
   validation and analyzer rules preserve the existing wrapper-only UI boundary.
5. Focused tests cover real editor-to-run-to-conversation flow using a local SSE
   provider, exact model routing, retained drafts/selections, cancellation, lifecycle
   rejection, unavailable configuration, stale edits, atomic claims, single ownership,
   overlapping runs, timeouts, DST, shutdown and forward-migration rollback.

#### Scheduling contract

- Scheduling runs only while MossAgent is open; it is not a Windows service or an
  operating-system scheduled task. Polling is every 15 seconds, not hard real time.
- Missed occurrences while the app is closed are advanced, not replayed. Daily/weekly
  times use the selected time zone. DST gaps advance to a valid local time; repeated
  local times use the later occurrence and do not generate duplicate runs.
- One instance per data directory owns scheduling. A plan has at most one active
  durable claim. Pause affects future starts, not an already-running conversation.
- Stop and application shutdown cancel task-owned execution and await finalization.
  Unfinished records become interrupted on restart. A terminal-persistence failure
  retains the durable claim rather than repeating an uncertain side effect.
- Removal archives the plan and retains its existing run records and conversations.
  Running plans cannot be removed. History queries are bounded; the page displays
  the latest 50 runs for the selected plan.
- Version-nine migration creates separate schedule/run tables transactionally. It
  preserves existing projects, conversations, failures and messages and is idempotent.

#### Verification evidence

Temporary scripts, synthetic data, the native QA executable and screenshot files are
outside the source tree or in ignored validation artifacts. They are not release files.
Evidence root: `%TEMP%\MossAgent-automation-inspector-20261008`.

- Full solution regression passes 2,899 tests across seven test assemblies. After
  the final empty-state class/position fix, all 2,206 App tests pass again against
  the rebuilt assembly. Final solution and ordinary debug App builds report zero
  warnings and errors. The scoped 57-file AST audit has no violations; whitespace
  verification and diff checks pass.
- The ordinary, non-elevated JIT App is rebuilt and restarted. Its automation entry
  is enabled, and Ctrl+P opens the new inspector header and centered no-conversation
  state. No schedule or conversation is created in the user's data during this check.

- Deterministic application flow uses the existing loopback SSE test server, not a
  fake success status. The resulting messages/task IDs are read back from SQLite.
- Native QA runs the real App startup/shutdown lifecycle against separate synthetic
  data directories. Sample plans are paused; its synthetic provider is deliberately
  non-executable. The native diff/audit screenshots use real git.status/git.diff tools
  against a temporary Git repository, not seeded tool-result text.
- Passive Python PrintWindow captures check for black output and unexpected dimensions.
  Light 1280x820, dark 980x820 and the dark 980x640 editor are inspected without scaling
  the original images. The editor scrolls while preserving a reachable footer.
- The first captures exposed a native list-item selection frame and unapplied input
  dimensions. Wrapper styles must target the inherited native StyleKey or the owned
  shared class, and automation variants must load after base dialog styles. The
  corrected card has no native thick frame, uses the shared four-sided shallow shadow,
  and the editor prompt has a 96-pixel minimum height with top-aligned text.
- Empty-state tests inspect the actual heading's position, not just the stretched
  wrapper center. Explicit shared classes preserve styling for UserControl wrappers.
- Native Escape closes the synthetic editor, and Ctrl+J switches the synthetic
  workspace inspector. Mouse automation returned `coordinate input geometry is
  unavailable`; native mouse/hover behavior is therefore not claimed as verified.

#### Remaining limits

The current Codex reference capture shows a file-preview side panel, not the same
automation/editor/inspector state. It is useful for shell geometry but does not prove
pixel-for-pixel equivalence of these new pages. Same-state Codex screenshots are
still needed for final fidelity work. The subsequent file-preview/rendered-diff
phase is documented in [FILE_PREVIEW_ACCEPTANCE.md](UI_WORKFLOWS.md#file-preview-acceptance); all Codex automation workflows
are not implemented by this change.

Real paid-provider execution, browser sessions, native terminal interaction and
Windows-service scheduling are not accepted by these screenshots. Terminal pipeline
authorization and late-result boundaries are covered by focused tests; native capture
only checks its inactive presentation. No packaging/AOT/serialization changes are
part of this work; ordinary development remains managed/JIT.

#### Editor timing preview (2026-10-08)

The editor now shows an expected next occurrence as fields change, using the
same Application timing/DST rules as scheduling and the selected time zone.
Invalid/incomplete settings replace the old preview with an actionable hint.
Saving still recomputes from the actual save time; this is not a continuously
ticking scheduler or a promise that a paused plan will run. Hourly schedules
ignore the hidden daily time field rather than being blocked by its stale value.

This is App-owned presentation over an existing Application calculation. It adds
no repository mutation, model call, authorization change or migration. Deterministic
clock tests cover daily/weekly/hourly timing, invalid fields, no enable/run/storage
side effect and the hourly hidden-field regression. The native light 1280x820
editor capture in `%TEMP%\MossAgent-preview-interactions-20261008` shows the time-zone
preview and reachable footer; same-state Codex editor comparison remains outstanding.

#### Run history presentation and navigation (2026-10-08)

`AutomationHistoryViewModel` and its separate wrapper-only view now own the
selected plan name, bounded recent history, loading/empty/read-failure states,
explicit retry and conversation opening. Rows use shared centered 14-pixel
status icons, elapsed-time labels and actionable localized failure/timeout/
interruption hints. Untrusted stored error codes and repository exceptions are
never echoed into the UI. Open failures preserve the rows and current page.

Design self-check:

1. App owns presentation and navigation through the existing
   `IAutomationRepository` and `IWorkspaceRepository` ports. Domain, scheduling,
   provider routing, approval policies and persistence schemas do not change.
2. History queries request and display at most 50 rows. Foreign-plan rows are
   rejected, and detached/stale row commands cannot navigate. Active project
   identity is checked again against the returned conversation and catalog.
3. Each read has cooperative lifetime cancellation and a 30-second deadline.
   Plan replacement, refresh, page hiding, mutation blocking, catalog locking
   and disposal invalidate pending navigation even after hide/reopen or
   lock/unlock. A noncooperative pending open keeps its opening ownership until
   storage returns; cancellation does not forcibly stop an unresponsive port.
4. The separate view/model remain below the file and method limits and each
   handwritten C# file contains one type. Existing shared wrapper appearance
   variants own status geometry and the reusable thin-scrollbar gutter.
5. Focused tests cover state exclusivity, retry, bounded/foreign results, late
   success/failure, cancelled/disposed scopes, transient locks, unavailable and
   foreign-project conversations, exact command bindings, real page visibility,
   localized safe hints and centered icons/text in both themes.

Native evidence uses separate paused synthetic plans under
`%TEMP%\MossAgent-automation-history-20261008`, not user schedules or model calls.
Python passive window capture inspects light 1280x820 and dark 980x640 pixels.
Actual keyboard interaction verifies that a missing conversation produces an
actionable local error, and that a completed record opens the seeded persistent
conversation without consuming the retained composer draft. The first native
capture exposed an overlay scrollbar clipping trailing text; the shared
thin-scrollbar variant now reserves a content gutter in history and plan lists.
The native acceptance uses keyboard input and passive pixels; mouse/hover
behavior and paid-provider execution are not claimed as verified. Synthetic
previews are closed normally and no user schedule or provider configuration is
changed. Screenshot/comparison Python helpers remain outside the repository.

The current passive Codex capture is still its conversation/file-preview state,
not an automation page. This evidence does not establish pixel-perfect Codex
automation equivalence. Same-state Codex list/editor screenshots remain needed.

Final verification for this slice:

- Solution, ordinary managed/JIT App and native QA builds report zero warnings
  and errors. The final automation/scrollbar filter passes 65 tests. The scoped
  13-file AST audit has zero violations; scoped whitespace verification passes.
- The last full solution run executes 3,011 tests: 3,009 pass and two sidebar
  heading tests fail during temporary-directory deletion with Windows file
  sharing violations, after their layout assertions succeed. A separate rerun
  of all 12 sidebar heading cases passes. This is not a clean full-suite result.
- An earlier broad run also had one command-stop failure; its isolated rerun
  passes and the last broad run passes it. No command execution or unrelated
  test-cleanup code is changed in this slice. Failure logs and the final TRX
  results are retained under the evidence root for investigation.
- Final native screenshots are `history-Light-1280-820/window-accepted.png` and
  `history-Dark-980-640/window-final.png`; `history-scrollbar-comparison.png`
  compares identical synthetic history and native-size crops without scaling.
  The Python helpers, screenshots, synthetic SQLite data and QA host are not
  repository source or release assets. No commit, push or publish is performed.

#### Scheduler shutdown ownership (2026-10-09)

Shutdown previously waited for the scheduler gate before cancelling operations.
A recovery, catalog read or claim that needed cancellation to return could hold
that gate indefinitely. Start, tick and manual-run operations now register with
an Application-owned lifetime and link caller cancellation with shutdown.
Shutdown rejects new work, cancels current/queued operations, drains their
ownership, awaits poll/execution/finalization and only then releases the lease.
Concurrent stop/dispose calls share the same completion task.

Design self-check:

1. Application owns scheduler lifecycle; existing repository/runner/lease/clock
   ports and dependency direction remain unchanged. No UI, SQLite or OS access
   is added to the scheduler.
2. The existing execution path remains authoritative. No parallel execution
   engine, tool bypass, schedule schema or model-routing change is introduced.
3. Caller cancellation preserves its token identity and does not cancel another
   queued owner's operation. Noncooperative ports and runners stay owned until
   return; shutdown is not a forced-termination guarantee. Started terminal
   persistence deliberately completes without the cancelled execution token.
   A late durable claim never invokes the runner, is finalized as cancelled
   when storage permits and otherwise retains the durable claim with only
   `persistence-error`. Uncertain effects are not automatically retried.
4. The nine scoped C# files each contain one type, are at most 195 lines and
   have methods at most 35 lines. No appearance variant, AOT, serialization or
   packaging change is part of this slice; development remains managed/JIT.
5. Twelve new deterministic cases cover cancellation-before-gate shutdown,
   cooperative/noncooperative ports, queued cancellation isolation, repeated
   stop/dispose, late read/claim rejection, safe failed finalization and retained
   runner/terminal-persistence ownership. Tests have bounded cleanup rather than
   leaving a hung scheduler behind.

Evidence root: `%TEMP%\MossAgent-automation-shutdown-20261009`.

- The initial eleven-case reproduction has ten failures and one pass before
  the fix (`red/shutdown-red.trx`). After the fix, those eleven cases pass in
  three consecutive runs. The added terminal-persistence case and all existing
  Infrastructure automation tests pass together: 41/41 in
  `latest/automation-latest.trx`. The scoped Roslyn audit and whitespace check
  pass, and the solution/native QA builds report zero warnings/errors.
- Ordinary, non-elevated native App lifecycle runs use separate temporary
  SQLite/project/provider data and a bounded loopback-only SSE provider. Both
  light 1280x820 and dark 980x640 manually start one plan, stop it, start again
  and close the actual window while running. Both hosts exit normally with
  `QA_EXIT: 0`, exactly two model requests and two persisted cancelled runs
  whose actual workspace tasks are cancelled/read-only. The plan stays paused
  and the retained workspace draft is checked throughout the open window.
- Passive Python capture checks for black output and unexpected native dimensions.
  Each native case saves ready/running/stopped/pre-close PNGs without rescaling.
  The dark window's first input attempt reports unavailable geometry; a fresh
  returned window binding and observation permit the single retry. Successful
  button activation does not establish all mouse-hover/pointer-state behavior.
- No real provider, paid request, tool execution, user schedule or user settings
  participate in the native flow. QA sources/scripts/data remain outside the
  repository, with the executable in ignored validation artifacts. Current Codex
  reference pixels still do not show the same automation state; these captures
  do not claim pixel-perfect Codex automation equivalence.

Final verification for this slice:

- App automation/shutdown regression passes 62/62
  (`latest/app-workflow-latest.trx`). The latest full solution run passes all
  3,241 tests across seven assemblies (`full-latest/`), including the new
  terminal-persistence case. Narrow whitespace verification reports no changes.
- The earlier full run passed 3,239/3,240: the Windows shell timeout test failed
  while reading its synthetic `process.pid` due to a file-sharing violation.
  Its isolated rerun passes, and the latest full run passes all 57 Windows
  cases. The original failure/TRX is retained under `full/`; no unrelated
  shell implementation or test fixture is changed to mask it.
- `native-automation-run-stop-comparison.png` compares original-size plan-card
  crops in both themes and running/stopped states without resizing. It is
  a MossAgent state comparison, not a same-state Codex reference comparison.
  Both QA hosts and test hosts exit; no commit, push or publish is performed.
<!-- /source: AUTOMATIONS_INSPECTOR_ACCEPTANCE.md -->


---

<a id="subagent-concurrency-native-acceptance"></a>

## SUBAGENT_CONCURRENCY_NATIVE_ACCEPTANCE.md

<!-- source: SUBAGENT_CONCURRENCY_NATIVE_ACCEPTANCE.md -->
### Production subagent concurrency acceptance

#### Scope and candidate

This closes the current two-task native subagent cancellation/restoration gate,
not complete-product acceptance or same-state Codex pixel equivalence. No
production behavior, test assertion, timeout, approval setting or packaging is
changed for this branch.

The ordinary-permission production App uses the existing final Debug/JIT
Windows x64 candidate under `.scratch/rail-navigation-final-20261015`. Its App
DLL SHA-256 remains
`19564864E0806BD8CD4B4EDFF64E34D493808BF6715276CA9EE4ED2006368458`.
Both native processes and the subsequent solution build use that identity.

Evidence and Python helpers remain outside Git at
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-child-concurrency-20261015`.
The helpers passively capture the exact PID/executable/HWND, compose owned
popups, reject black images and record DPI/elevation. All Windows input targets
only the independent MossAgent instance through supported Computer Use APIs.
Codex/ChatGPT, user data and permission prompts are not operated.

#### Isolated scenario

An external seed uses the formal SQLite repositories to create two separate
projects and completed synthetic tasks, each initially FullAccess. The tasks
have distinct authorized roots, parent model preferences and context markers;
no existing task's policy is changed. Each root owns `evidence.txt` and
`continued.txt`; a fifth file belongs to an unauthorized foreign directory.

The same saved child profile supplies its name, trigger, system prompt,
`gemini-3-pro-preview`, high effort and enabled context sharing. A loopback-only
model fixture supplies Responses parent and Gemini child frames; actual file
reads, execution auditing, cancellation, persistence and UI projection use
production components. Only synthetic keys and allowlisted request flags/model
parameters are used; no headers, raw requests or credentials are logged.

Unlike the older sequential fixture, request counts are owned by each
task/parent/child route, and every emitted tool-call identity is distinct. The
provider normalizes Gemini child call IDs; audit records retain each unique
child ID beneath its own parent-call prefix.

#### Native execution and isolation

1. Real composer input and the send button start A. Its child performs an
   authorized file read and receives its own file marker before holding a
   second streaming response.
2. Task switching and a second real send start B while A remains running.
   Both tasks are Running, each has one live delegation and one successful
   read audit, and each child receives only its own context and file result.
3. Selecting A and clicking its actual Stop button cancels only A. Its task
   becomes Cancelled, its delegation completes as `tool_cancelled`, and the
   fixture observes A's child connection disconnect. B stays Running with its
   delegation open and without another parent/child request.
4. Expanding and scrolling A's execution history shows the successful read and
   `CHILD-A-PARTIAL-819`. The saved child entries are no longer partial/running;
   cancellation does not discard previously received dialogue.
5. Releasing only B's synthetic response gate causes another real authorized
   read of B's `continued.txt`, then its final child answer and parent answer.
   B becomes Completed while A remains Cancelled. The child Markdown/code block
   and the separate parent answer are reached by actual disclosure and scrolling.

There are eight model POSTs: A parent/child 1/2 and B parent/child 2/3. A ninth
allowlisted log entry records A's disconnect, not another request. Five unique
audits comprise two delegations and three file reads; all finish with timestamps
and FullAccess policy approval. Four succeed, and A's delegation alone is
cancelled. A never executes its second read or reaches a final answer.

Request flags verify the selected parent model/medium effort, selected child
model/HIGH thinking, saved profile prompt, parent trigger and shared own-context.
Foreign context, project root, file marker and foreign-directory marker are
absent. Persisted messages and call prefixes also remain task-owned. All five
synthetic file hashes remain unchanged and artifact count is zero. This is
positive isolation evidence, not a new native traversal-denial test or evidence
of a user manually approving a prompt.

#### Restore, responsive presentation and cleanup

The initial App PID 13488 exits normally with code zero. Restarting the same
data as PID 7112 restores cancelled A and completed B. Actual task switching,
disclosure and child-pane scrolling recover A's partial reply, B's two read
results, child Markdown/code block, parent answer and unchanged 52-second duration.
No send or continuation is requested after restart.

All nineteen table hashes, messages, audits, request records and file hashes are
identical to the completed pre-close snapshot, before restored input, after
expansion, after compact inspection and after final close. Only task state,
messages and execution audits differ from the before-input baseline; sixteen
other tables remain unchanged. The initial pre-launch snapshot helper queried
the wrong audit column (`tool_call_id` instead of `call_id`) and failed before
writing evidence. It is retained as a helper diagnostic; only the corrected
before-input baseline is claimed, not a successfully captured pre-launch seed.

Sixteen passive Python captures cover Dark/96-DPI maximized 1920x1032, normal
1280x820 and minimum 526x601 windows:

- `00` shows idle B while A is already running in the sidebar; its filename is
  not evidence that A's selected dialogue is displayed.
- `01`–`02` show both tasks running with B and A selected respectively.
- `03`–`06` show A cancellation, retained partial dialogue and unaffected B.
- `07`–`08` show B completion and its post-cancellation read/final child answer.
- `09`–`12` show restarted collapsed/expanded A/B history and final child code.
- `13`–`15` show normal/compact history, including the actual project-context
  toggle and child-pane scrolling needed to expose the entire final code block.

Both App processes exit zero through normal window-close actions, with empty
stderr, no surviving native windows and ordinary permissions. The loopback
fixture exits zero and releases its port. One idle HTTP keep-alive read reports
`ConnectionAbortedError`/WinError 10053 after normal App shutdown; this external
fixture diagnostic is retained, not described as an App failure or empty fixture
stderr. A's separately recorded live-stream disconnect establishes cancellation.
Some immediate Sky accessibility snapshots lag behind the already updated
pixels; actions use fresh observations rather than treating stale trees as bugs.

`verify.py` independently checks task/call ownership, model/profile/context flags,
successful/cancelled dialogue, eight requests, five audits, stable nineteen-table
restore, unchanged files, zero artifacts, both normal exits, released endpoint,
and all sixteen non-black captures. `native-verification.json` reports success
while retaining `FullProductComplete=false` and `FullTestRunAllGreen=false`.

#### Regression and remaining gates

The current candidate passes 124 focused App subagent cases and the complete
119-case Agent suite: 243 fresh cases without adding the earlier full-run counts.
Another complete solution build has zero warnings/errors. Native/test dependency
identities remain unchanged after that build; repository whitespace checks pass.
No new product behavior requires an additional test implementation in this
acceptance-only branch; existing deterministic lifetime/gateway/approval tests
remain the regression boundary.

The preceding complete solution test run remains 6,111/6,115 with four failures;
its isolated passing reruns and these 243 cases do not turn it all-green. That
record remains in [SETTINGS_ENTRY_ACCEPTANCE.md](UI_WORKFLOWS.md#settings-entry-acceptance). A fresh complete all-green run
is still a product completion gate.

No real remote provider, manual-approval success, high-DPI transition or complete
current-version theme/size/state sweep is established. Native editor-save/sharing
off/on evidence remains in [SUBAGENT_SETTINGS_TO_RUNTIME_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#subagent-settings-to-runtime-acceptance); this
branch seeds settings and does not claim to repeat that editor workflow. Current
Codex same-state visual references, residual UI fidelity and whole-product
acceptance remain open. Nothing is staged, committed, pushed, released or installed.
<!-- /source: SUBAGENT_CONCURRENCY_NATIVE_ACCEPTANCE.md -->


---

<a id="subagent-dialogue-projection-acceptance"></a>

## SUBAGENT_DIALOGUE_PROJECTION_ACCEPTANCE.md

<!-- source: SUBAGENT_DIALOGUE_PROJECTION_ACCEPTANCE.md -->
### Subagent dialogue projection optimization

#### Design self-check

- App owns presentation through the existing ChatMessageViewModel and reusable
  SubagentDialogueView. Remove the derived row getter from the raw tool payload
  instead of putting mutable cache fields into a record copied with `with`.
- Each message lazily projects the latest published raw dialogue snapshot. Warm
  reads return the same rows. Only changed role/content rows are recreated;
  retained answers keep their parsed Markdown blocks. Rows parse answers only
  when Blocks is read; literal tool output is still never parsed as Markdown.
- New snapshots do not mutate earlier rows or arrays. Metadata-only updates do
  not notify the row binding. Equal rows may retain presentation when IsPartial
  changes because that flag is raw execution metadata, not row presentation.
- No cross-message/task or static cache, new port, filesystem/network operation,
  approval, timeout, output-budget, audit, persistence or JSON-shape change.
  Standard JIT debug and Windows x64 single-file publication remain unchanged.
- The three production C# files each retain one type and stay below 250 lines;
  new methods remain below 40 lines. Existing shared controls, styles, dimensions
  and icon geometry are unchanged.
- Tests cover retained and changed rows, immutable snapshots, independent messages,
  current-value notifications, null/empty histories, role changes, partial flags,
  81 duplicate/reorder/grow/shrink transitions, rendering, serialization and the
  existing cancellation/reopen lifecycle regressions.

#### Evidence

- Four new regressions fail against the original getter: warm reads and metadata
  updates rebuild rows, and progress reparses unchanged answers. With one answer
  containing a heading and C# code block, 1,000 warmed reads allocate 1,624,000
  bytes in the original implementation and zero after this refactor.
- The refactored implementation passes 192 focused cases, including bound-view
  updates, light/dark narrow/wide rendering, raw JSON/legacy loading and live
  chunked/close-delimited child Stop and optional reopen. The bound view now
  inherits its message context instead of rebinding to each raw tool payload.
- Measurement is Windows x64 .NET 10 Debug/JIT with current-thread allocation
  counters after 100 warm-up reads. It measures repeated property reads, not
  complete application memory, rendering cost or general latency.
- TRX, baseline source snapshots and logs remain outside Git under
  `%TEMP%\MossAgent-dialogue-projection-20261012`.

#### Final verification

- Complete App regressions: 3,652/3,652 passed, with no failures or skips.
- Serial solution build after the test host exits: zero warnings/errors.
- Architecture constraints: 7/7 passed. Post-build projection/bound-view replay:
  13/13 passed. Other complete test assemblies were not rerun in this slice.
- Scoped whitespace verification and repository-wide git diff --check pass. One
  initial formatting run identifies nested-loop indentation in the new test;
  it is corrected with apply_patch before the successful final verification.
- The 13-file Roslyn audit passes, with exactly one type per handwritten file,
  maximum 185 lines and maximum 48-line test methods. Changed production methods
  are at most 24 lines; WorkspaceChatView markup is 170 lines. PowerShell's first
  Roslyn load conflicts with its loaded assembly, so the audit uses an isolated
  repository-external .NET 10 executable, not a heuristic type counter.
- Unobserved construction of an answer row allocates 40 bytes in the focused
  measurement; its 1,000-heading answer is parsed on the first Blocks read only.

#### Post-refactor native smoke

- Rebuild the external host with the current App only after test-host exit.
  It runs without elevation on PID 5104, in a new synthetic data/project root.
  Actual stable client geometry is 1280 by 820 DIP, scale 1.25, light theme.
- Actual input/send invokes Gemini parent and Responses child with the configured
  model, high reasoning, prompt and sharing disabled. Held chunked progress shows
  the partial answer in the real WorkspaceChatView after scrolling its detail pane.
- Releasing the response updates the same native dialogue to the complete answer.
  The tool succeeds with no error, parent/child requests are 2/2, child disconnects
  zero, parent replies one and both child/inner-file tool audits finish successfully.
- Passive Python screenshots are retained in the external evidence root's native
  directory: live.png, completed.png and completed-answer.png. The Python image
  shows clear rendered text at physical 1600 by 1025 pixels; the Sky preview at
  this DPI loses some glyph pixels and is not used to infer a production font bug.
- Normal close confirms QA_FINAL_REQUESTS=2,2 and QA_UI_EXIT=0. The broader sharing
  toggle, Stop, persistence and same-process recovery evidence is documented in
  SUBAGENT_LIFECYCLE_NATIVE_ACCEPTANCE and predates the projection refactor.
  This smoke proves the new binding's native live/completed update, not every
  provider, dark theme, DPI, checkbox hit area or long composer wrapping case.

This completes only a presentation optimization slice, not full-product visual
or functional acceptance.
<!-- /source: SUBAGENT_DIALOGUE_PROJECTION_ACCEPTANCE.md -->


---

<a id="subagent-execution-acceptance"></a>

## SUBAGENT_EXECUTION_ACCEPTANCE.md

<!-- source: SUBAGENT_EXECUTION_ACCEPTANCE.md -->
### 子代理链路验收

本记录覆盖可配置子代理、模型设置联动和本机确定性运行链路。
不代表真实远端模型、全部界面状态或 Codex 一比一还原已经通过验收。

#### 2026-10-11 工具载荷精简与 Gemini 主代理完整链路

##### 设计与精简边界

- App 拥有工具对话载荷及其展示投影；原实现序列化 `ToolConversationPayload` 时，
  除原始 `Dialogue` 外，还保存 `HasDialogue`、`HasDetails` 和 `DialogueEntries`。
  最后一项会为保存结果再次创建展示对象、解析子代理 Markdown 并重复保存内容。
- 三项纯展示属性标记 `JsonIgnore`，仍由原始对话计算，页面绑定和展开行为不变。
  没有新增端口、执行器、缓存、SQL、数据库迁移或历史数据重写；不引入 JSON
  源生成或 AOT。工具授权、审批、取消、超时、预算和审计继续走既有管线。
- 新增 16 项序列化回归，原始字段精简四项先失败后通过；成功/失败、中文、
  空值、部分消息、错误码、Markdown 代码和表格恢复及非法载荷拒绝均覆盖。
  旧载荷中的过时展示字段不参与恢复，不丢失原始工具对话或重新执行工具。
- 六个 C# 文件通过限定范围空白格式和 Roslyn 审计：每文件一个类型，最长
  文件 194 行、最长方法 40 行，均符合硬上限。
- 仓库外 Debug/JIT 合成样本验证六项原始字段逐项 JSON 相同。100 条富文本对话
  的序列化大小从 56306 降至 22548 字节，每次分配从 533952 降至 45528 字节。
  这是本机微基准，不代表整个应用速度或所有真实任务；基准不进入仓库。

##### 跨协议自动回归

- 扩展原有协议夹具，以 Gemini 3 作为父提供商，子代理分别使用 Responses、
  OpenAI 兼容、Anthropic Messages、Gemini。四协议各验证共享开/关成功、停用
  提供商/模型拒绝及越界读取拒绝，新增 20 项；加上 Responses 父链路共 40 项。
- 使用实际设置保存命令、生产提供商、标准工具执行器、SQLite 和审计。断言
  子模型/high/提示/触发条件按保存配置路由，父 Gemini/medium 和父上下文不变，
  子工具结果返回子模型并汇总到父任务，恢复对话不重放模型请求。
- 验证父 Gemini 签名和原生 call ID 在同次运行中回传。恢复检查针对已持久化
  对话和工具记录，不要求 `ConversationMessage` 存在并未定义的 continuation 字段。
  首次编译失败是测试断言错误，没有据此修改生产持久化或数据库。
- 停用配置不回退到父模型、不发送子请求；越界文件失败审计、停止后续子轮次，
  不能把外部合成文件内容带回父任务。协议/序列化/内容展示定向 69 项通过。

##### 普通权限原生操作与旧记录恢复

- 真实 App/MainWindow、独立临时 SQLite 和两个 loopback SSE 服务。实际在设置页
  修改触发条件/提示词，把子模型从 Gemini 切换到 Responses 子模型并选择 high，
  点击保存后在会话输入框发送授权合成文件读取请求。共享关闭由隔离种子准备，
  本轮没有原生切换共享或审批控件，没有真实凭据、远端模型调用或用户文件修改。
- 原生链路父/子请求分别 2 次；确认已保存参数进入请求、共享关闭有效、父模型
  不变、父签名/native ID 回传、两个成功且已完成的独立审计及合成文件未修改。
  浅色实际展开/折叠/重新展开并滚动子代理详情，工具证据、子回复和父汇总可见。
- 原生元数据最初直接在转义 JSON 中查找中文，错误报告 DialoguePersisted=false；
  保留原记录后，通过只读 SQLite 和 JSON 解码复核为 true，只修正外部验证器。
  没有重发请求或修改生产序列化以迎合错误断言。
- 精简后重建宿主，用相同含旧展示字段的数据恢复。深色界面仍显示子工具、
  子回复和父汇总，实际隐藏项目上下文并滚动详情，关闭日志请求数为 0,0。
  浅色原始截图 1920×1032、宿主报告 scale=1；深色原始截图 1280×820、宿主
  报告 scale=1.25。按实测记录尺寸，不把预设 980×640 当作实际截图尺寸。
- Python 只对精确 PID 被动裁取可见屏幕像素，不点击/缩放/拉伸。深色首次取帧
  曾捕获遮挡窗口，重新核验 QA 无障碍树并激活独立宿主后才保存验收图片。
  两次成功验收进程均正常 Close、退出码 0；另一次恢复宿主随执行环境中断，
  不把该次未返回关闭日志的尝试视为通过。

##### 证据与尚未覆盖范围

- 最终 `dotnet build MossAgent.slnx --no-restore` 为 0 警告、0 错误。
  原生宿主关闭后，`dotnet test MossAgent.slnx --no-build --no-restore -m:1` 完整
  4470 项通过，0 失败、0 跳过；核对 8 个 TRX：App 3328、Visual 8、Agent 63、
  Browser 76、Infrastructure 165、Windows 137、Providers 377、Tools 316。
- 一次并行全回归出现 4 项 Windows 进程相关超时/发现失败及 MSBuild 子节点
  提前退出，未完成整个套件；保留 `solution-tests-final.log` 和 `final-regression`。
  不修改这些不相关生产实现或放宽测试期限；最终串行复测确认全部通过。
  成功的完整证据为 `solution-tests-serial-final.log`、`serial-final-regression` 和
  `serial-final-summary.log`，不能把中断尝试与成功结果混为同一次运行。
- 证据根 `%TEMP%/MossAgent-gemini-parent-20261011`：`focused/protocol.trx`、
  `serialization-before.log`、`serialization-after.log`、`serialization-benchmark.log`、
  `solution-build-final.log`、`format-final.log`、`audit-final.log`，以及
  `metadata-initial-false-check.json`、`metadata-corrected-check.log`、`light/metadata.json`。
- 原生证据 `native-settings-before.png`、`native-settings-saved.png`、
  `native-completed-details.png`、`native-reexpanded.png`、`native-dark-restored.png`、
  `native-dark-restored-details.png`、`native-light.log`、`native-restored-retry.log`。
- 合成服务验证请求、参数、工具及持久化链路，不证明真实远端模型必定遵循触发
  指导委派。本轮没有截到实时流式中间帧；原生共享开/关及负向边界未逐项重复，
  由自动回归覆盖。完整 Codex 像素一致、全部 DPI/主题/功能仍未完成验收，目标保持进行中。
- 未暂存、提交、推送或发布；调试仍为 JIT，发布仍为 Windows x64 单文件。

#### 2026-10-10 跨提供商协议完整链路

##### 范围与自动回归

- 新增夹具复用既有本机 SSE 服务，使用真实设置保存命令、生产提供商、工作区
  发送命令、工具执行器、SQLite 与审计；没有第二套子代理执行实现或生产协议改动。
- 父任务固定使用 Responses；子任务分别使用 Responses、OpenAI 兼容接口、
  Anthropic Messages、Gemini GenerateContent。四协议各验证共享开/关的成功链路、
  停用所选提供商/模型的拒绝链路及越界文件拒绝，共 20 项回归。
- 断言父模型与 medium 强度不变，子模型、high 参数、提示和上下文按保存配置路由，
  工具结果回传子模型并汇总到父任务；重建服务容器恢复持久化对话不重放请求。
  Gemini 3 同时验证 thought signature 和 native call ID 往返。
- 停用配置不能回退到父模型，也不能发送子请求。越界读取必须失败审计且停止
  后续子轮次，不泄露外部合成文件。保留现有失败工具立即停止的行为。
- 初始失败来自夹具：将失败读取混入成功链路、中文 JSON 转义、Anthropic 结束帧
  以及 nullable 审计断言；修正测试输入和断言，没有据此放宽生产安全边界。
  最终子代理/续轮/工具名定向 124 项通过。

##### 原生操作与恢复

- 普通权限真实 App/MainWindow 使用独立临时数据和两个 loopback 服务。在设置页
  实际把初始 Responses 子模型切换为 Gemini，编辑触发条件和系统提示词，选择
  high 后保存；未操作上下文共享、授权、审批、安全或隐私控件。
- 在实际输入框提交合成请求，看到文件工具记录、渲染后的子回复标题和父汇总；
  折叠、重新展开及详情滚动有效。本机父/子请求各两次，两个审计成功，合成文件不变。
- `metadata.json` 的模型、提示、触发条件、保存/实际 high、上下文隔离、父上下文、
  工具结果、审计和文件检查全部为 true。这里只保存允许列表布尔元数据和请求计数，
  不写请求正文、API Key 或真实凭据。
- 关闭后重建宿主并重开同一数据。通过原生拖动检查深色 980×640，隐藏项目上下文
  后滚动查看完整子回复。父汇总和工具对话恢复，关闭日志请求数为 0,0；两进程均
  非管理员、退出码 0，没有遗留验收进程。恢复检查不再次发送模型请求。
- 仓库外被动 Python 只按实际 PID 获取窗口绘制帧，不点击、移动、缩放或拉伸图片。
  浅色展开/重新展开图片为原始 1920×1032，深色恢复图片为原始 980×640，已检查非黑帧。
  重开时验收脚本曾引用旧窗口变量，重新使用返回的新窗口对象后恢复；不称其为产品修复。

##### 最终结果与限制

- 完整解决方案 3843 项通过，0 失败、0 跳过；App 2910、Agent 63、Browser 76、
  Infrastructure 144、Windows 129、Providers 302、Tools 219。
  构建 0 警告、0 错误；本轮九个 C# 文件空白格式和单类型/文件/方法硬上限审计通过。
- 证据位于 `%TEMP%/MossAgent-subagent-protocols-20261009-2325`：`focused.log`、
  `solution-build.log`、`solution-tests.log`、`cleanup-format.log`、`cleanup-audit.log`、
  `native-light.stdout.log`、`native-restored.stdout.log`、`native/light/metadata.json`，
  以及 `native/light-expanded.png`、`native/light-reexpanded.png`、
  `native/dark-restored.png`、`native/dark-restored-details.png`。
- 合成 SSE 证明协议参数和工具链路，不证明远端模型一定按触发指导委派；未调用付费
  提供商。原生共享开/关及拒绝路径未逐项重复，负向边界由自动测试覆盖。
  未完成全部 DPI、界面状态或 Codex 像素一致验收；整个产品目标仍保持进行中。
  未暂存、提交、推送或发布；调试仍为 JIT，发布仍为 Windows x64 单文件。

#### 2026-10-07 主回复与子代理回复统一组件

##### 设计与修复边界

- 复现子代理最终回复把标题、代码围栏和表格当作原始文字显示的问题，四项明暗、
  280/740-DIP 布局回归先失败后通过。主回复与子回复现共用 `AppResponseContent`
  和 `AppResponseTable`，不建立另一套 Markdown 渲染或直接使用原生视觉组件。
- 组件及共享变体由 App 拥有，复用现有解析器和剪贴板命令；页面只绑定数据和状态。
  代码复制使用精确原始代码，缺失命令禁用，CanExecute 及键盘操作保持有效。
  子代理使用 compact 变体，工具、任务和状态内容保持字面文本。
- 实际布局测试发现 `AppScrollViewer` 的框架 StyleKey 为 ScrollViewer，新代码样式
  使用包装类型选择器未生效；按真实 StyleKey 修正共享选择器，不在页面恢复尺寸覆盖。
  长代码、宽表格局部滚动，外部详情视口不扩宽；代码视口保持 360-DIP 高度上限。
- 展示用 Blocks 明确不序列化，原始 Markdown 随既有工具载荷保存；进度、完成及
  反序列化恢复均使用同一组件。没有新的网络、存储或工具端口，也没有数据库迁移。
  子调用授权、审批、取消、超时、输出预算和审计管线不变。
- `ValidateSharedResponseContent`、`MOSSUI002` 保护组成与外观；属性、属性元素、
  原生所有者限定和隐式子内容均受检查。真实 App 构建分别拒绝 AXAML 尺寸覆盖及
  C# Content 替换探针，验证后已移除探针；合法组件在完整构建中通过。

##### 普通权限原生链路

- 真实 App/MainWindow、隔离 SQLite、合成授权项目和只监听 127.0.0.1 的 Responses
  服务；没有真实凭据、付费请求、用户项目修改或外部转发。
- 原生输入修改触发条件和提示词、用键盘选择 high 并点击保存，再从会话输入框
  实际键入并发送审查请求。子模型在界面显示为 child-rich；该模型和共享关闭
  在隔离种子中准备，不将其表述为本轮原生切换操作。未操作审批或隐私设置控件。
- 四次请求顺序为 parent-rich、child-rich、child-rich、parent-rich。子请求使用 high，
  包含已保存提示词，均不含父对话 PARENT_ONLY_20261007 标记。父请求仍包含该标记。
- 既有 subagent.run 委派后，子模型通过标准执行器读取授权 Sample.cs，随后得到
  原始文件结果并输出标题、列表、代码、表格和引用，父模型完成汇总。
  只读核对两个独立成功且已完成的审计，合成文件未改变，原始 Markdown 已保存，
  展示 Blocks 未写入载荷。日志仅保留允许列表元数据和布尔检查，不记录请求正文或密钥。
- 实际滚动详情并点击代码复制，QA_COPY_EXACT=True 确认剪贴板与预期合成代码完全相同，
  没有记录其他剪贴板内容。浅色 1280×820，深色 980×820 全新进程恢复同一 SQLite，
  代码与表格正确渲染；恢复未增加模型请求。两个宿主和本机夹具均正常关闭且已确认退出。

##### 原始像素与验证结果

仓库外证据位于 `%TEMP%/MossAgent-subagent-rich-content-20261007-01/`。
被动 Python 获取指定 PID 的窗口绘制帧，拒绝黑帧、错误尺寸和捕获期间的边界变化。
`subagent-light-answer.png`、`subagent-light-table.png` 及 `subagent-dark-answer.png`
保留原始尺寸；`codex-current.png` 为 1936×1048，包含 8 像素非客户端边缘。
并排导航裁片只裁剪该边缘，不拉伸图像，不改变 Codex 界面状态。

- 新增组件、组成拒绝、子回复、实时更新/恢复及溢出回归共 27 项；相关专项 92 项通过。
- 七项目完整 2,518 项通过，0 失败、0 跳过，其中 App 1,909、Agent 58、Browser 32、
  Infrastructure 81、Windows 50、Providers 241、Tools 147；各项目独立 final-*.trx。
- 完整解决方案构建 0 警告、0 错误；限定空白格式检查通过，996 个 C#/AXAML 源文件
  单类型、文件及方法硬限制审计均 0 违规。保留无关文件的既有尾随空白。
- 脚本、宿主、截图、日志和合成数据均不入仓库；没有暂存、提交或推送。
  当前 Codex 内容、窗口及检查器状态不同，不能用于宣称子代理回复逐像素一致。
  本轮不覆盖所有 DPI、主题状态、远端模型委派或全部工作流；完整产品目标仍进行中。

#### 实现边界

- 子代理继承父任务的授权目录、审批策略、超时、取消和输出预算。
  子工具重新进入标准执行器并单独审计，不绕过审批，也不能递归委派。
- 主对话展示运行状态、流式回复、工具事件和最终结果。
  流式预览有界；最终对话经过正常输出限制后随父工具结果保存。
- App 复用配置仓库和模型目录变更通知，不直接访问数据库或模型 HTTP。
  提供商或模型修改后刷新子代理选项，保留尚未保存的编辑草稿。
- 删除、停用的选择不静默替换成其他模型；恢复相同模型后可重新匹配。
  保存前重新校验目录，拒绝过期或加载失败的选择；失败消息不回显异常内容。
- 加载版本拒绝迟到快照，窗口释放时解除新增订阅。
  无数据库迁移和授权策略变更，相关文件、类型和方法遵守工程硬限制。

#### 自动回归

- 先用真实 App 装配与 SQLite 设置命令复现六项失败，再修复目录联动。
- 七项设置链路回归覆盖首个模型添加、模型参数修改、模型删除、模型停用、
  提供商停用、过期保存拒绝及释放后的订阅隔离。
- 三项目录回归覆盖刷新乱序、重复刷新及保存不替换失效选择、恢复原选择、
  加载失败保留草稿及快照、拒绝保存并隐藏异常内容。
- 子代理、提供商和模型定向组合 57 项通过；追加目录回归后，
  全部子代理定向测试 25 项通过。最终全量结果以本记录后续结果为准。

#### 2026-10-05 实际桌面验收

- 使用普通权限 MossAgent、独立临时数据和仅监听 127.0.0.1 的合成 Responses
  服务。没有调用付费模型、真实用户项目或生产配置，没有操作审批策略控件。
- 子代理编辑页先修改提示词草稿，再通过提供商页保存子模型显示名。
  返回子代理页立即看到新名称，未保存的提示词保留，思考强度仍为 high。
  随后保存子代理配置并从主对话实际发送审查任务。
- 服务收到父模型 parent-fixture、思考强度 medium，包含触发指导和主对话上下文；
  子请求使用 child-fixture、high，包含修改后的提示词和共享上下文。
- 父模型委派 subagent.run，子模型流式显示回复并执行 filesystem.read_file，
  读取授权目录的 probe.txt。第二轮子请求收到 QA_CHILD_FILE_READ_OK，
  返回最终结果，父请求收到子代理结果并完成汇总。
- 真实窗口看到运行状态、读取记录、最终回复和父任务汇总；完成后可折叠、
  重新展开和滚动详情。工具行复用共享 AppToolActivityRow，而非页面自建按钮。
- 只读打开验收 SQLite：任务为完成状态，父 subagent.run 与子文件读取均有
  已完成且成功的独立审计记录，父工具结果保存七项非预览对话条目及最终回复。
- 初次运行失败是夹具把 Responses 服务配置成兼容 Chat 协议，随后仅修正
  夹具协议和种子消息角色，通过提供商刷新加载；没有重启服务或删除失败历史。
  窗口仍保留最初种子消息的旧呈现缓存，不把它计作消息角色刷新验收。
- 仓库外 Python 被动截图保留原始 1920×1032 像素，没有黑屏或缩放。
  `%TEMP%/MossAgent-subagent-flow-qa-twjfpcsj` 保存
  subagent-model-sync-light.png、subagent-streaming-light.png 和
  subagent-completed-light.png。脚本、图片和合成数据不加入仓库。
- 本轮验收预览正常关闭，拥有的本机服务随后停止，两个进程均已退出。

#### 2026-10-06 子调用生命周期、取消和重开恢复

##### 设计自检

- Agent 拥有进度事件流及其后台子执行的生命周期，继续复用 `IToolExecutor`。
  不引入 Avalonia、数据库或操作系统依赖，不增加新的运行端口。
- 每个子调用使用独立的关联取消令牌。消费者提前退出、展示抛异常或枚举器释放时，
  只取消对应子执行，并等待工具清理与审计完成；不能取消父令牌或其他任务。
- 原有授权目录、审批、超时、输出上限与审计管线不变。正常用户取消仍先发布
  有界最终工具结果，再停止父循环；无需数据库迁移，不自动重放工具。
- 实现保持在已有 AgentRunner 内，以 `try/finally` 收尾，不建立第二套执行器。
  本轮新增类型各自独立文件，方法与文件没有超过工程硬上限。
- 正向覆盖流式结果、正常停止与重开恢复；负向覆盖中途退出、展示异常、
  阻塞的审计收尾、并发任务隔离及异常信息不泄漏。

##### 已复现和修复的缺口

修复前两项新增回归实际失败：`await foreach` 在子代理预览中途 break，
或展示回调抛出异常后，子模型仍在后台等待，枚举器释放没有等待其退出。
现由所属事件流取消子执行，并等待标准工具执行器完整返回。

- 四项新增 Agent 回归证明正常 break、展示异常、审计完成前不能结束释放，
  以及同一 AgentRunner 的另一任务继续运行。父令牌不被局部收尾取消。
- 三项新增 App 回归从提供商、模型和子代理设置命令开始，贯通真实 loopback
  Responses HTTP、工作区停止命令、SQLite 与重新打开服务容器。
- 子模型使用 high 思考强度，返回部分文本后保持 HTTP 响应未完成；停止按钮
  使服务端观察到实际断连。只有父、子两个模型请求，没有继续父轮次。
- 正常停止后任务和工具卡显示已取消，一条工具结果及其非预览部分对话恰好保存
  一次，审计为已批准、失败、`tool_cancelled`。重开后状态和部分对话仍保留。
- 展示异常路径使用真实 Agent、提供商与审计，确认子连接关闭、审计结束、
  父任务失败、运行卡结束，以及合成敏感异常文本不出现在用户提示中。
  此路径事件消费者已退出，不将它表述为完整工具对话的持久化验收。

##### 运行窗口与被动截图

普通权限真实 App/MainWindow、独立临时配置和合成提供商，使用相同本机 SSE
夹具验证运行中预览、实际停止命令及全新进程重开。没有付费模型或真实项目操作。
宿主、脚本、日志和图片均在仓库外：
`%TEMP%/MossAgent-subagent-lifetime-qa-20261006-01`。

| 最终截图 | 原始像素尺寸与状态 |
| --- | --- |
| `subagent-streaming-light-normal-final.png` | 1280×820，浅色普通窗口，状态及部分回复展开 |
| `subagent-cancelled-light-normal-final.png` | 1280×820，真实停止后保留部分对话 |
| `subagent-streaming-dark-max-final.png` | 1920×1032，深色最大化，运行中预览 |
| `subagent-cancelled-dark-max-final.png` | 1920×1032，真实停止后的共享工具详情 |
| `subagent-restored-light-normal-final.png` | 1280×820，全新进程重开已取消任务 |

最终浅色日志使用 `light-normal-final-v3/light-normal.log`；深色日志使用
`dark-max-final-v4/dark-max.log`；最终重开日志为
`light-normal-final-v3/restored-light-normal-stable.log`。
日志确认普通权限、一条工具结果、一条已完成审计、Cancelled 任务；运行场景均为
两次模型请求且子连接实际断开。最终进程均记录窗口关闭、退出完成和退出码 0，
验收结束未遗留宿主进程。五张最终图片已逐张检查，没有拉伸、黑屏或菜单遮挡。

初次屏幕截图被 Windows 开始菜单遮挡，不能作为视觉验收。后续仓库外 Python
改用被动 `PrintWindow` 返回窗口绘制帧，并按已确认 PID 查找新窗口；不点击、激活、
移动或缩放任何应用。最大化首帧曾尚未就绪，重开时原生尺寸也曾在捕获中改变，
造成空白、旧浅色画面或黑色填充。现在有界等待和重新绑定同一目标窗口，拒绝
捕获前后尺寸变化，并只校验预期尺寸，不缩放图片。验收宿主等待界面布局与绘制
稳定，按实际展开状态使用命令，截图完成后正常关闭。

旧普通屏幕图片、v2/v3 失败状态、v4 被尺寸守卫拒绝的重开日志及
`subagent-restored-unstable-v3-rejected.png` 保留为失败证据，不算最终成功。
不能把日志中的 ViewModel 尺寸或就绪标记单独当作正确绘制的证明。
最终窗口绘制帧重拍成功不被表述为修复 Codex 内置截图工具或产品窗口逻辑。

当前 Codex 参考 `codex-reference-window-pixels.png` 也以被动窗口绘制取得，
保留原始 1936×1048，未修改其主题、窗口或对话。它与 MossAgent 内容和主题状态
不同，不用于宣称完整页面、颜色、抗锯齿或所有图标已经逐像素一致。

##### 最终自动化结果与限制

- 子代理 Agent 定向 26 项与新增 App 链路 3 项通过；完整解决方案重复复核
  1,515 项通过：Agent 47、App 1,131、Browser 32、Infrastructure 32、
  Windows 50、Providers 76、Tools 147。解决方案构建 0 警告、0 错误。
- 本轮九个 C# 文件的空白格式检查和 `git diff --check` 通过。
  809 个 C#/AXAML 文件审计无超过 500 行的文件或多类型 C# 文件。
  三个此前已记录的超长方法未在本轮修改，不声称仓库方法规模全部合格。
- 程序化执行真实命令不是物理鼠标、系统键盘或原生文件选择器验收。
  `PrintWindow` 返回窗口绘制帧，不代表全部屏幕合成、全部 DPI、悬停、焦点、
  明暗主题状态及 Codex 一比一还原均已完成。
- 未暂存、提交或推送；整个 MossAgent 目标仍未完成。

#### 2026-10-06 子代理编辑器草稿、绑定与生命周期

##### 设计自检

- 行为由 App 的设置编辑器拥有，复用既有配置仓储端口、编辑修订号、
  `GuardedAsyncRelayCommand` 和 `SettingsMutationRunner`；不增加 UI 到具体存储的依赖。
- 写入前捕获不可变快照。迟到成功只刷新已保存目录，不能覆盖更新的字段或另一个
  编辑上下文；新草稿拥有稳定身份，再次新建才生成另一个身份。
- 保存、删除与刷新互斥，直接调用两种命令入口也不能重入，更不能取消已接收的操作。
  编辑器释放会取消所属操作、解除窗口订阅并拒绝迟到发布。
- 取消令牌传入现有仓储端口，30 秒后请求取消，读取还使用有界等待。
  不配合取消的写入必须继续保有互斥权直至返回；不宣称强制终止或回滚这种写入。
  工具授权、审批、输出限制、持久化结构及子执行管线不变，无需迁移。
- 加载失败保留草稿和旧列表，但禁止写入，成功重试后恢复。缺失模型不自动换选，
  已移除的编辑目标不能因刷新、保存前读取或切换编辑器而复活。
- 界面继续复用共享列表行、字段、按钮、文字与错误样式；底部操作和结果使用
  两个有限宽度的结构行，不添加实例颜色、控件尺寸或另一套包装层。
  手写类型各自独立文件，文件及方法规模检查通过。

##### 实际失败基线和回归

- 四项草稿/刷新/删除回归在修复前失败，随后通过；涵盖迟到写入、切换编辑器、
  新建身份、读取失败、操作重入、取消、释放及安全错误提示等成功与拒绝路径。
- 原生界面刷新后模型下拉显示空白。集合重建后的等值记录没有触发属性通知，
  绑定仍引用旧对象。现重新绑定当前快照中的标准对象并明确通知，绑定更新期间
  不允许反向事件清空选择；四项真实 Headless 页面绑定回归先失败后通过。
- 未变化的目录保留原集合项及选择实例，避免保存或刷新使列表、下拉框和滚动位置跳动。
  新增回归覆盖未变化刷新，并非仅测试 ViewModel 的值相等。
- 两项窄窗口底部布局回归先失败后通过。结果提示改放保存按钮下方，避免挤窄按钮
  或截断文本；复用已有按钮和文本变体，不更改全局尺寸。
- 已有 `SubagentWorkspaceFlowTests` 在本次复核通过：真实设置命令和 SQLite 配置
  贯通本机确定性提供商、触发指导、提示、所选子模型与 high 强度、上下文共享、
  子对话呈现、持久化与审计。本轮没有重复创建另一套子调用实现。

##### 普通权限运行与截图

验收宿主使用实际 App/MainWindow、隔离的临时 SQLite 及合成子代理配置。
实际点击名称字段、系统键盘编辑、刷新和保存确认新草稿与模型仍保留；只读查询
确认保存的名称为 `review-fixed`、强度 high、共享关闭、启用开启。
没有真实账户、付费模型、项目工具操作或用户配置写入。

脚本、宿主、数据、日志及原始图片位于仓库外
`%TEMP%/MossAgent-subagent-editor-qa-20261006-01`。

| 图片 | 原始尺寸与实际状态 |
| --- | --- |
| `subagent-refresh-before.png`、`subagent-refresh-fixed.png` | 1920×1032，浅色，刷新导致模型空白及修复后保留选择 |
| `subagent-refresh-before-after.png` | 同尺寸原图的未缩放并排裁片 |
| `subagent-dark-feedback-before.png`、`subagent-dark-feedback-fixed.png` | 980×640，深色，底部提示布局修复前后 |
| `subagent-light-footer-final.png` | 1920×1032，浅色，实际保存后按钮下方完整提示、原滚动位置保留 |

宿主日志确认非管理员运行。被动 Python 截图仅查找指定进程的窗口并获取绘制帧，
拒绝黑色/均匀帧、错误尺寸及捕获期间变化的窗口边界，不拉伸图像。
深色保存后的最终窄窗口和浅色底部状态均已实际检查，验收宿主正常关闭并复核退出。
宿主复制的预览版本在无关思考选项回退前构建，high 与本轮布局相同；不据此宣称
最终全部选项列表或全部交互均已原生验收。

##### 最终验证与限制

- 定向编辑器、子代理、共享写入与窗口回归 88 项通过。
- 完整解决方案 2,109 项通过：App 1,620、Agent 47、Browser 32、
  Infrastructure 58、Windows 50、Providers 155、Tools 147。
- 最终解决方案构建 0 警告、0 错误；本轮十个 C# 文件空白格式检查通过。
  924 个 C#/AXAML 文件审计没有多类型、文件或方法硬上限违规；`git diff --check` 通过。
- 日志分别为 `subagent-focused-final-02.log`、`solution-tests-final-03.log`、
  `solution-build-final-02.log`、`format-final-03.log`、`audit-final-02.log`。
- 不把 Headless 或合成链路当作全部协议、DPI、主题、原生输入与 Codex 像素一致的证明。
  自定义代理编辑器的并发写入、重载和生命周期不在此次修复验收范围。
  未暂存、提交或推送；整个产品目标仍未完成。

#### 2026-10-05 验证结果

```powershell
dotnet build MossAgent.slnx --no-restore --disable-build-servers -m:1
dotnet test MossAgent.slnx --no-restore --no-build --disable-build-servers -m:1
```

- 解决方案构建 0 警告、0 错误；完整测试 1,213 项通过，包含 App 861 项。
- 本轮五个 C# 文件的空白格式检查、单类型、500 行文件上限和 80 行方法上限通过。
  本轮范围的 diff 空白检查通过；不修改此前已记录的无关 SearchTextTool 行末空白。
- 没有暂存、提交或推送；仓库外验收脚本和图片不加入仓库。

#### 尚未验证

- 本轮桌面操作仅覆盖上述浅色最大化成功链路，不覆盖全部 DPI、明暗主题、
  取消、超时、拒绝或共享关闭的原生界面；这些失败边界另有自动测试。
- 初次链路验收只读核对持久化结果；后续详情布局复核已在新数据副本中重开
  真实窗口，确认最终子代理对话及父任务汇总恢复，没有重跑模型或工具。
- 合成服务证明参数与工具链路贯通，不证明真实远端模型必然按触发指导委派。
- 共享组件和构建门禁防止样式分叉，不等同于 Codex 全页面逐像素一致。

#### 2026-10-11 共享上下文有界拼接

##### 设计自检

1. 行为仍属于 Agent 的 SubagentConversationContextBuilder，不增加项目依赖。
2. 保留既有 Build 入口与模型调用端口；只处理已经在内存中的消息，不引入新服务。
3. 不新增文件、网络、审批或工具操作。仍只选最近 12 条有内容的用户、助手和
   历史观察，输出不超过 12,000 个 UTF-16 单元，不泄露 continuation/signature。
   历史观察仍标注为不可信数据、不重放操作，调用方的取消流程不变。
4. 拼接时就保留有界尾部，而非先创建完整上下文和大字符串再裁剪。
   新辅助方法与原方法同属一个类型，文件和方法不超过既有硬上限。
5. 聚焦测试对照旧算法检查消息筛选、顺序、换行、精确长度、滚动裁剪和代理对；
   2,000,000 字符消息及 12 条同等长度消息的调用内分配必须低于 512,000 字节。

仓库外证据复用本轮目录：
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-task-snapshot-review-20261011`。
`context-red.log` 保留旧实现的两项分配失败：单条 8,064,528 字节、12 条
96,074,832 字节；另外 12 项语义测试通过。

- 优化后 `context-green/context-green.trx` 的 48 项聚焦测试全部通过，包含 14 项
  新回归及历史观察、原生 continuation、子代理执行、流式与生命周期测试。
  两项大消息测试的调用内分配均低于 512,000 字节；不将此合成上限当作真实应用
  工作集或所有模型场景的性能测量。
- `solution-build-complete.log` 为 0 警告、0 错误；`format-complete.log` 的 22 文件
  空白格式检查通过。新增生产文件 66 行、测试文件 141 行，均为单类型；方法
  人工审查未超硬上限。架构约束回归也通过，不扩展依赖或变更序列化配置。
- 本轮原生截图只验证独立的任务快照审查链路，不能用来证明有界共享上下文的
  完整远端模型或全产品验收。
- 相同代码最终串行全量 `full-recheck` 的 8 份 TRX 共 4,641/4,641 通过，无失败
  和跳过。首轮全量有 1 项既有 MCP fixture 等待超时，失败原件保留；该类随后
  单独 8/8 通过，未改动 MCP 代码或放宽期限。不声称偶发超时已修复，详见
  [TASK_SNAPSHOT_REVIEW_ACCEPTANCE.md](CONVERSATIONS.md#task-snapshot-review-acceptance) 的两次独立回归记录。
<!-- /source: SUBAGENT_EXECUTION_ACCEPTANCE.md -->


---

<a id="subagent-lifecycle-native-acceptance"></a>

## SUBAGENT_LIFECYCLE_NATIVE_ACCEPTANCE.md

<!-- source: SUBAGENT_LIFECYCLE_NATIVE_ACCEPTANCE.md -->
### Subagent native lifecycle acceptance

#### Plan and design self-check

- The preceding proxy turn made verified progress. The product goal remains
  active; earlier subagent runs do not prove native shared-context toggling or
  stopping while a child stream is open.
- Use the existing App composition, settings commands, workspace run ownership,
  provider adapters, subagent tool pipeline and persistence. The only new runtime
  code is a repository-external QA host with isolated synthetic data.
- Two bounded loopback fixtures return the established Gemini parent and Responses
  child protocol. A cancellable child-frame gate permits observing live progress
  and network disconnect without modifying production timeouts or bypassing tools.
- The synthetic project alone is authorized. Its existing FullAccess fixture
  policy is seeded, never changed or approved through UI automation. No real
  configuration, credentials, public endpoint or daily browser is accessed.
- Each external handwritten C# file contains one type and stays within file/method
  limits. Observe only allowlisted parameters, booleans and task/tool states;
  never write full provider requests, headers, keys or arbitrary dialogue text.
- Verify actual sharing-on save/request, live child progress, sharing-off save,
  Stop/cancellation/persistence and a successful subsequent send without restarting
  the App. Reuse focused subagent/lifetime tests and build the full solution.
- Any observed defect requires a failing focused regression and a fix in its owning
  module. Screenshots use the existing repository-external passive Python helper.

#### Verification

##### Transport investigation, 2026-10-12

- The preceding sidebar turn made verified progress, including a successful
  3,638-case App run. The product goal remains active; that run is not native
  subagent lifecycle evidence.
- A held fixed-length response reproduces the missing partial text in two new
  workspace cases. A standalone `HttpClient` headers-only probe also blocks, even
  with one-byte reads. A bare TCP probe, without any MossAgent provider, agent or
  UI, receives the 92-byte complete response header but not the already-written
  body. `NoDelay` and coalescing header/body writes do not change that result.
- This places the observed withholding below MossAgent's SSE parser, not in its
  Markdown, dialogue projection or preview threshold. The specific transport or
  environment component responsible is not identified; neither runtime source
  assumptions nor this result prove a general .NET/Windows defect.
- Probe source snapshots and failing TRX remain outside the repository under
  `%TEMP%\MossAgent-subagent-lifecycle-recheck-20261012`. Temporary byte/TCP probes
  are not retained as failing production regression tests or silently counted as
  successes. No provider parser, execution timeout or system setting is changed.
- Actual indefinitely-held SSE now uses valid HTTP chunk framing in the external
  QA server. Ordinary completed fixture responses keep their fixed lengths. The
  repository lifetime regression also covers chunked and close-delimited child
  streams, each with live partial text, Stop, audit completion and optional restart.
  This does not establish fixed-length partial delivery on this environment.

##### Native lifecycle, 2026-10-12

- The ordinary-permission QA host stays on PID 6716 for all three sends, with
  actual client geometry 1920 by 1032 DIP and scale 1 in the light theme. Native
  screenshots use the repository-external passive Python helper.
- Sharing-on is saved through the actual editor. The child receives the configured
  model, high reasoning effort, configured prompt and parent marker on both model
  turns, and the second request includes the authorized file-read result.
- Live partial text is visible after scrolling the child detail viewport. Actual
  collapse/re-expand preserves it. Releasing the held chunked response completes
  the child successfully; the parent reply alone is not used as success evidence.
- Sharing-off is saved without changing model/reasoning. The next two child
  requests omit the parent marker. While the response is held with partial text
  visible, actual Stop closes the child connection, sets the task to Cancelled and
  completes the tool audit with `tool_cancelled`; no task remains running.
- Read-only SQLite checks verify the cancelled partial reply is persisted, all
  dialogue IsPartial flags are false and its inner filesystem.read_file audit
  succeeds. No credentials or raw provider requests are exported.
- A third actual send succeeds without restarting. Aggregate parent/child requests
  are 5/6, child disconnects 1 and parent replies 2. All six child requests use the
  selected model, high effort and configured prompt; the latter four omit shared
  parent context. All six tool audits are complete; the cancelled child remains
  cancelled rather than being retroactively marked successful.
- The host exits through its normal close signal: QA_FINAL_REQUESTS=5,6 and
  QA_UI_EXIT=0. Evidence remains under
  `%TEMP%\MossAgent-subagent-lifecycle-native-20261012`, including sharing-off
  saved/live/cancelled/rerun images, persistence booleans and final assertions.

This verifies this synthetic native lifecycle, not every provider, theme, DPI or
the unresolved fixed-length partial-delivery behavior. The checkbox's wide blank
hit area and long composer wrapping were separate observed UI gaps; their later
reproductions and corrections are documented in INPUT_HIT_TARGET_ACCEPTANCE.

The subsequent fixed-length investigation is recorded in
[FIXED_LENGTH_STREAM_ACCEPTANCE.md](PROVIDERS.md#fixed-length-stream-acceptance). Independent Python probes reproduce only
the plaintext HTTP withholding, while private pinned-TLS provider regressions
verify fixed-length partial delivery. This does not retroactively turn the
earlier failing plaintext probes into successes or add native TLS UI coverage.
<!-- /source: SUBAGENT_LIFECYCLE_NATIVE_ACCEPTANCE.md -->


---

<a id="subagent-manual-approval-acceptance"></a>

## SUBAGENT_MANUAL_APPROVAL_ACCEPTANCE.md

<!-- source: SUBAGENT_MANUAL_APPROVAL_ACCEPTANCE.md -->
### Subagent manual approval and tool-output keyboard acceptance

#### Scope and design self-check

1. App owns the shared tool-output focus appearance. Agent/provider/tool execution
   and module dependencies remain unchanged; the remaining changes are tests.
2. Existing wrappers, native scroll navigation, approval commands, production
   composition, local provider fixtures and SQLite repositories are reused.
3. Only isolated synthetic tasks use AskEveryTime. No user policy, credentials,
   application window, desktop capture, authorized root or browser is modified.
   Existing tool cancellation, timeout, output limits and audit boundaries apply.
4. New handwritten C# files each declare one type and stay below the size limits.
   Appearance belongs to shared styles, not individual business views.
5. Tests cover successful approvals, denied/canceled requests, stale request IDs,
   retained drafts, restart without replay, keyboard access and unrelated controls.

#### Deterministic approval coverage

- Eight success cases use the production offscreen MainWindow, real execution
  pipeline, synthetic SQLite state and local parent/child model servers. Child
  protocols are Responses and Gemini; both context-sharing choices and themes
  are covered. Light is 1280x820; compact Dark is 526x601.
- Before approval, there is one parent request and no child request. Light uses
  mouse hit-testing and Dark uses Space while settings are open. The actual saved
  model, reasoning effort and context-sharing setting reach the child request.
- Approval permits the outer subagent.run. The existing read-only policy permits
  its internal filesystem.read_file automatically; this is not a claim that every
  child read receives a second manual approval.
- Parent/child audits preserve task identity and namespaced child call identity.
  The authorized synthetic file is read; outside content is excluded from the
  child request and the outside file remains unchanged.
- Conversation and tool disclosures open/close using actual buttons. The result
  heading is rendered as Markdown, scrolled through both nested viewports and
  hit-tested on its text, not merely asserted to exist in a hidden visual tree.
- Reopening composition and rebinding the same window restores the result
  without replaying model requests or recreating pending approvals. Existing
  drafts and settings-page state survive approval.
- Eight negative cases cover denial/cancellation across both protocols/themes.
  Unrelated or stale request IDs cannot approve the pending/completed request.
  No child request starts; audits record approval_denied or tool_cancelled.

#### Keyboard usability correction

- Four real-view cases reproduce a separate UI problem: expanded output has
  vertical overflow but the shared ScrollViewer cannot receive keyboard focus.
- The tool-details-scroll shared variant now accepts focus. Tab reaches it from
  the disclosure button; PageDown/PageUp can traverse the entire output. Focus
  outside the card does not continue scrolling its content.
- Focus uses the existing rounded card's border and shared BorderFocusBrush,
  without an extra square adorner or changes to padding, border width or layout.
  Its accessible name follows the existing tool summary. Ordinary scroll viewers
  keep their previous non-focusable behavior.
- Eight pixel cases cover both themes, compact/wide layouts and 1x/1.25x/2x
  rendering. They compare the focused edge, unchanged rounded corner, stable
  geometry and complete restoration after blur.

#### Evidence interpretation

- Evidence remains outside the repository at
  `%TEMP%/MossAgent-child-manual-approval-20261015`.
- `initial.trx` and `namespaced-audit.trx` retain incorrect test assumptions about
  child call IDs, Markdown renderer types and queued settings-close layout. These
  are not production regressions or a product red baseline.
- `visible-result.trx` and `nested-scroll.trx` retain intermediate visibility
  checks: nested scroll offsets must be adjusted independently, and a selectable
  text hit-test must target the text rather than full-row whitespace.
- `keyboard-baseline.trx` reproduces the actual non-focusable output defect.
  Intermediate keyboard runs also assumed unsupported native Home/End behavior;
  final coverage uses the native PageUp/PageDown interaction instead of adding
  application-specific key handling.
- These are deterministic offscreen renders, not native captures or same-state
  Codex pixel equivalence. Native manual-approval success, DPI transitions and
  full-product visual acceptance remain open. The previous 6,467-case complete
  checkpoint predates this slice and must not be increased by adding subset runs.

#### Validation checkpoint (2026-10-15)

- All 317 related App cases and all 188 pixel cases pass with no skips/aborts:
  505 distinct cases. These include sixteen new approval flow/boundary cases,
  four keyboard cases and eight focus-rendering cases.
- The solution build has zero warnings/errors. Production and both tested App
  DLL copies match; the two tested assembly hashes remain unchanged throughout
  the runs. Scoped whitespace formatting and diff checks pass. Full App and the
  other six complete suites were not rerun for this
  slice; the earlier eight-suite checkpoint remains separately identified.
<!-- /source: SUBAGENT_MANUAL_APPROVAL_ACCEPTANCE.md -->


---

<a id="subagent-runtime-configuration-acceptance"></a>

## SUBAGENT_RUNTIME_CONFIGURATION_ACCEPTANCE.md

<!-- source: SUBAGENT_RUNTIME_CONFIGURATION_ACCEPTANCE.md -->
### Subagent runtime configuration acceptance

#### Design self-check

1. Agent owns child configuration resolution through its existing repository
   ports. App settings and shared model capabilities already validate new drafts,
   but a later provider/model edit can invalidate a previously saved child effort.
   The execution boundary must revalidate the actual saved target, without silently
   choosing another provider, model or reasoning effort.
   App owns audit presentation from existing audit records, with exact localized
   error-code categories rather than raw errors or permission-escalation advice.
2. Reuse `IConfigurationRepository`, `ISubagentConfigurationRepository` and
   `ModelReasoningPolicy`; no new execution path, concrete infrastructure reference
   or UI dependency. Match both model identity and provider ownership.
3. Run through the existing schema/capability/approval/timeout/cancellation/output/
   audit pipeline. Reject invalid configuration before a child request or progress
   event. Localize the actionable rejection; never echo keys or exception text.
4. Keep one handwritten type per file and existing file/method hard limits. Do not
   alter JIT, Windows x64 packaging, JSON, persistence schema or tool permissions.
5. Add failing focused pipeline tests for foreign model ownership, unsupported
   saved effort and supported choices/legacy `off`. Exercise actual settings saves,
   SQLite, local adapters, failure audit/presentation/restore and explicit repair.
   Existing successful native child lifecycle evidence is not recreated or claimed
   as fresh negative-case acceptance. Supplement this with actual settings edits,
   native rejection/restore screenshots and audit hint allowlist/layout regressions.

#### Current scope

The preceding model-status continuation is verified progress, not full-product
completion. Current source and acceptance records show that the saved child
configuration, four protocols, live dialogue, sharing toggle, Stop and same-process
recovery already exist. This slice hardens configuration changes between editing
and execution, rather than creating another child runner.

#### Failing evidence and correction

- The first pipeline run has 7 failures and 6 passes. Unsupported efforts reach
  the stub model, and a matching row identity with foreign provider ownership is
  incorrectly accepted. The foreign-row case is a negative port-contract probe,
  not a claim that the production SQLite query returns foreign rows.
- The real App/settings/SQLite baseline has 4 failures and 1 pass. Editing a model
  family or provider protocol leaves the saved child effort intact, but execution
  reports the generic `subagent_model_failed` instead of naming the incompatible
  configuration. Gemini's adapter already prevents a network request in these
  baseline cases; this is not reported as an existing remote-request leak.
- `SubagentTool` now matches both row and provider identity, and checks the current
  shared capability policy before constructing a child run. Supported settings and
  legacy `off` retain their current request semantics. Unsupported settings return
  `subagent_reasoning_unavailable` with an actionable localized explanation, no
  child request/progress, and no fallback or rewrite of the user's configuration.

#### Verification

- Focused Agent checks initially passed 68/68; the subsequent catalog/gateway
  optimization expanded this to 84/84. The final Agent assembly passed 112/112.
- The App `Subagent|ConfigurationMutation|ModelReasoning` run passed 152/152,
  covering actual settings, SQLite, local protocol adapters, explicit repair,
  failure audits/cards, saved-effort retention and restore without replay.
  The final-build full App assembly subsequently passed 3,739/3,739.
- The signed Gemini child fixture now uses the valid `gemini-3-flash-preview`
  model identity, retaining its signed-continuation and parent/child isolation
  assertions. Restored configuration is read from the repository port rather
  than constructing a fresh UI-bound MainWindowViewModel off its dispatcher.
- Scoped whitespace checks, Roslyn type/size checks and the final solution build
  passed with no warnings/errors. No persistence schema or packaging changes.
- Native Gemini model-family invalidation and rendering are now verified below.
  Provider-protocol invalidation retains deterministic coverage rather than native
  negative-case coverage. Gemini model-family repair now also has the native
  success/restoration evidence below.
  Full-product and pixel-parity acceptance remain ongoing.

#### Native negative-case acceptance (2026-10-12)

The previous goal turn changed code and produced completed tests, so it was
progress. This continuation closes the missing native rejection evidence, then
fixes an actually observed audit-presentation gap rather than adding another
child executor.

- Rebuilt the existing external `GeminiParentQa` host against current App code;
  build warnings/errors were zero. Both native starts reported `QA_ELEVATED=False`.
  Only a new synthetic TEMP database/project and loopback protocol servers were
  used. No real provider credentials, paid requests or production task data.
- In the real child settings page, explicitly selected and saved `high` for
  `gemini-3-pro-preview`. In the real expanded provider/model editor, changed that
  same non-default model identity to `gemini-2.0-flash` and explicitly set the
  model's own effort to `none`. The default parent remained
  `gemini-3-flash-preview` / `medium`; the saved child remained `high`, with sharing
  disabled and no automatic fallback.
- The settings page preserved the now-incompatible child effort and displayed
  its existing explicit-choice guidance. A conversation send produced a visible
  failure row with the new actionable reasoning explanation. Normal close logged
  `QA_REQUEST_COUNTS=2,0`. Read-only whitelist inspection of the synthetic SQLite
  database confirmed one approved, completed, failed `subagent.run` audit with
  `subagent_reasoning_unavailable`, a persisted matching failure payload, no child
  dialogue and unchanged authorized evidence file. The parent's synthetic summary
  is not evidence of successful child execution.
- Native inspection also found that the right execution-record card still mapped
  that error to generic permission/configuration advice. `AuditExecutionViewModel`
  now maps the exact known code to localized child-settings guidance. Unknown or
  near-matching codes remain sanitized; success/pending/denied precedence stays
  unchanged. No raw error text, permission changes, new UI controls or local styles.
  The failing presentation baseline was 1 failure / 17 passes; five presentation
  regressions and two actual shared-row wrapping/bounds cases cover the correction.
- Reopened the same saved database normally with the repaired App, without sending
  anything. Deep-dark failure history and the corrected audit hint were visible.
  The restore host initially had an effective 1280x820 window despite requesting
  980x640; only a native border drag produced the measured 980x640 capture. At that
  size the audit hint wraps completely within the existing shared card. Normal
  close logged `QA_REQUEST_COUNTS=0,0`; the read-only verifier confirmed the same
  five messages and unchanged message/audit hashes, preserved child `high`, and
  no replay or history mutation.

External evidence root (no files from it are added to the repository):
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-reasoning-negative-native-20261012-101831`.
Screenshots use the user's existing Python helper in `--passive` mode with the
exact native PID; input and resizing use Sky. Its cursor glow is not App shadow.

- `01-valid-high.png`, `02-model-family-changed.png`, `03-invalid-effort-preserved.png`:
  actual saved settings and retained invalid child choice.
- `04-runtime-rejection-light-wide.png`, `05-audit-before-specific-guidance.png`:
  1920x1032 light native rejection and the observed generic audit defect.
- `06-restored-dark-wide.png`, `07-restored-dark-narrow.png`,
  `08-restored-dark-narrow-specific-audit.png`: actual 1280x820 and 980x640 dark
  restoration and corrected audit guidance. The compact conversation summary
  retains its existing ellipsis; the audit card shows the complete instruction.
- `metadata-negative-before.json`, `metadata-negative-restored.json`, native
  stdout/stderr logs: bounded whitelist evidence, normal exits and zero restore
  requests. Screenshot scripts, verifier and synthetic data remain outside Git.

This is native acceptance of one concrete failure/recovery scenario at 100%
scale, not proof of all protocols, all DPI settings or full Codex visual parity.

The final post-audit-change App assembly passed 3,746/3,746, with zero failures or
skips, in the completed `tests/audit-runtime-final.trx` under the external evidence
root. This includes the seven new presentation/shared-row cases; the preceding
94/94 focused run and zero-warning/error solution build cover the same production
change. No native protocol or DPI acceptance beyond the limits above is implied.

#### Native explicit repair and restoration (2026-10-12)

The preceding automation projection turn changed source and completed regression
checks, so it was progress. This continuation closes an actual native acceptance
gap without adding another execution engine or changing production behavior.

- Created a separate synthetic fixture by SQLite read-only backup of the preserved
  failing history. The external host retargets loopback endpoints and the synthetic
  authorized project through production repository ports. A dedicated Gemini child
  profile/model identity isolates child requests from the Gemini parent; the invalid
  saved `high`, `gemini-2.0-flash` family and prior failed history remain intact before
  UI repair. This is not a same-provider-identity replay of the original fixture.
  The synthetic child's legacy display name still says Responses; actual protocol
  and request checks establish Gemini behavior, rather than relying on that label.
  Original fixture data, real user tasks, accounts and credentials are not modified.
- External setup initially used a nonexistent `BaseUrl` property, then attempted
  to change a model's provider ownership in place. Compilation and the existing
  repository guard rejected those harness mistakes before UI acceptance. Corrected
  only the external host: use `BaseUri` and create the separate child model through
  supported repository operations. Do not weaken production ownership validation.
- Normal-privilege startup reported `QA_ELEVATED=False`. In the actual settings
  page, observed the retained incompatible `high` and its guidance, explicitly
  selected `none` and clicked Save. Sharing remained disabled and the parent model
  remained `gemini-3-flash-preview` / `medium`. A single manual conversation send,
  not automatic replay, requested another authorized synthetic-file inspection.
- The actual Gemini child made two requests targeting `gemini-2.0-flash`, without
  `thinkingConfig`; the Gemini parent made two requests and retained medium. Child
  context excluded the parent marker, its file result reached the second child
  request, and a successful child result reached the parent's continuation. Native
  dialogue displayed the authorized read evidence, child reply and parent summary;
  the inspector displayed the old failure beside two new successful audits.
- Normal close logged `QA_REQUEST_COUNTS=2,2`. A bounded, fixed-root, read-only
  whitelist verifier confirmed the explicitly saved `none`, unchanged selected
  identities from the pre-UI snapshot, one preserved configuration-failure audit,
  new successful `subagent.run` and `filesystem.read_file` audits, persisted child
  dialogue and unchanged evidence bytes. The original five message keys/content
  hashes and original audit tuple remain unchanged; no failure is overwritten.
- Reopened the same repaired fixture without sending. Its actual initial window
  was 1280x820 despite the host requesting 980x640; a native border drag produced
  the measured 980x640 capture. Verified recovered dialogue and the actual collapse
  action after scrolling the completed header into view. An earlier click on a
  clipped header did not collapse it and is not counted as successful interaction.
  Normal close logged `QA_REQUEST_COUNTS=0,0`; message and audit hashes match the
  completed repair snapshot, `none` remains saved and no model/tool replay occurs.

External evidence root:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-reasoning-repair-native-20261012-105830`.
Python captures use only the exact PID in passive mode; Sky owns all app input.

- `01-invalid-high-before-repair.png`, `02-none-explicitly-saved.png`: actual settings.
- `03-native-repair-success-dialogue.png`, `04-prior-failure-and-new-success-audits.png`:
  1280x820 dark successful workflow and retained error guidance.
- `05-restored-dark-narrow-expanded-before-visible-action.png`: the uncollapsed
  observation, not proof of the earlier attempted action.
- `06-restored-dark-narrow-actual-collapsed.png`: measured 980x640, old failure and
  new completed result after the actual collapse.
- `metadata-native-repair.json`, `metadata-repair-before.json`,
  `metadata-repair-after.json`, `metadata-repair-restored.json`, normal-close stdout
  and empty stderr: whitelist evidence, identity/history retention and request counts.
  Harness, verifier, captures and synthetic SQLite data remain outside Git.

This adds native Gemini effort-repair acceptance at 100% scale, not all-provider
negative/repair coverage, real remote-model compliance, all themes/DPI or Codex
pixel parity. The complete product goal remains active.

After both native hosts exited, the solution build again passed with zero
warnings/errors and the runtime-configuration/audit-presentation/shared-row focused
run passed 29/29 in `tests/native-repair-focused.trx`. Scoped whitespace checks
passed; no paths were staged, committed or published. This slice changes only this
repository acceptance record and external synthetic QA assets, not production
source or tests. The preceding goal turn's completed App full run was 3,757/3,757;
it is not represented as a new full-suite command in this native-only slice.
<!-- /source: SUBAGENT_RUNTIME_CONFIGURATION_ACCEPTANCE.md -->


---

<a id="subagent-settings-to-runtime-acceptance"></a>

## SUBAGENT_SETTINGS_TO_RUNTIME_ACCEPTANCE.md

<!-- source: SUBAGENT_SETTINGS_TO_RUNTIME_ACCEPTANCE.md -->
### Subagent settings-to-runtime acceptance

#### Design and scope

1. App owns the real settings editor, production composition and conversation
   presentation; Agent owns configured delegation through existing ports. This
   continuation adds repository-external acceptance assets, not a second executor.
2. Reuse the real configuration/workspace repositories, provider adapters and
   tool pipeline. Verify saved identities, trigger/prompt, selected model/effort,
   context sharing, actual child tools/results and restore without replay.
3. Use a new synthetic task created with FullAccess from the outset and a bounded
   loopback fixture. Do not alter the older AskEveryTime probe, its pending/cancelled
   history, any user data or any approval/privacy setting through UI automation.
   The older manual-approval branch remains separate evidence, not upgraded to a
   successful approved run. Only the new authorized synthetic evidence file is read.
4. Every external handwritten C# file has one type; production dependency, size,
   shared-control, JIT/JSON and Windows x64 packaging constraints remain unchanged.
5. Exercise actual settings edits/saves, native live/completed dialogue, sharing-off
   and sharing-on requests, persistence/audits, normal close and no-replay restore.
   Compare bounded request flags and hashes, never credentials or raw requests.
   Existing deterministic negative authorization, approval, configuration, lifetime
   and cancellation tests remain required alongside this successful native branch.

Evidence root:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-child-settings-success-20261014`.
Current work is not whole-product, all-provider, DPI or Codex pixel-parity sign-off.

#### Candidate and fresh checks

The unchanged production source is built into the separate
`.scratch/child-settings-success-20261014` artifact directory. The native App DLL
SHA-256 is
`705ADF5880D564496E0C7B66FFB2E6FEABB65F4B08FF01239735530F6B781CB9`.
This is not silently identified as the earlier Appearance-selector binary.
`native-assembly-identities.json` records the production dependency identities;
`final-verification.json` checks them again and records current test DLL identities
and matching App/Agent dependencies.

- The solution build succeeds with zero warnings and zero errors.
- The fresh App subset passes 152 cases, selected by
  `FullyQualifiedName~Subagent|FullyQualifiedName~ModelReasoning|FullyQualifiedName~ConfigurationMutation`.
- The fresh complete Agent suite passes 119 cases. Both TRX files have zero failed
  or unexecuted tests: 271 fresh cases, not another complete product-suite run.
- The preceding Appearance-selector checkpoint's 6,050 complete-suite cases
  remain historical evidence; they are not counted as rerun in this continuation.
- No production implementation or repository test source is changed in this
  acceptance slice. It adds this record and updates the product matrix; seed,
  fixture, capture, launch, snapshot and verification scripts remain outside Git.

#### Real settings to production execution

The isolated synthetic task is created with FullAccess before App launch; its
policy remains unchanged throughout. The production App, configuration stores,
Responses parent adapter, Gemini child adapter and execution pipeline are used.
The server is a bounded local deterministic protocol fixture, not a paid Gemini
or OpenAI service and not a second agent executor.

Actual editor interaction changes and saves the child name to `设置贯通审查`,
trigger to a condition containing `SETTINGS_TRIGGER_V2`, system prompt to
`SETTINGS_CHILD_SYSTEM_V2`, and model to `gemini-3-pro-preview`. Selecting that
model exposes the unsupported previous medium effort; explicitly selecting high
and saving produces the expected supported configuration. The enabled child
identity and selected provider/model identities persist rather than being
replaced with another seeded profile.

Two actual composer sends run in the same native App:

1. With sharing disabled, parent requests publish the new name/trigger. Both child
   requests use the newly saved prompt, selected model and Gemini
   `thinkingLevel: HIGH`, without the parent-only context marker or old prompt.
2. The real sharing checkbox is enabled and saved. A second actual send produces
   another delegation/read/result cycle. Both subsequent child requests carry
   the parent context marker, with the same model, HIGH effort and new prompt.

Each child performs a new authorized `filesystem.read_file` through the production
pipeline, displays its tool result and live partial response, and completes with
Markdown containing `SETTINGS-CHILD-ANSWER-OK` and the file marker. The parent
receives both the child answer and file result, then returns its final answer.
The sharing-on request can also contain earlier file/child markers from history;
the second successful file audit, not marker presence alone, proves the new read.

The allowlisted request log contains exactly four parent and four child requests.
Final persistence contains two successful `subagent.run` audits, two successful
file-read audits, two complete child dialogue payloads with actual tool content
and final answers, and two parent answers. No partial dialogue entries remain.
The task is Completed, all four audits have completed timestamps and no error
code, and there are zero artifact records. Fourteen of nineteen seed table hashes
remain unchanged; only child settings, messages, task state, execution audits and
conversation model preferences change. Both synthetic files retain their original
bytes, and the foreign-file marker never appears in the bounded request flags.

#### Native captures and no-replay restore

Fifteen passive Python PrintWindow captures bind the exact expected App executable,
PID and HWND, reject black images, and record 96 DPI and ordinary permissions:

- `00`–`02`: initial task, old child editor and saved sharing-off settings.
- `03`–`05`: live sharing-off dialogue, completed parent, and scrolled final child.
- `06`–`09`: saved sharing-on settings, live dialogue, completed parent, and final
  child answer/tool content in the expanded timeline.
- `10`–`11`: restarted completed history and expanded/scrolled persisted child.
- `12`–`14`: restored 1280x820 and 980x640 windows, including compact historical
  child Markdown reached by actual scrolling. Other captures are 1920x1032.

The first App (PID 9008) exits normally with code zero. The same recorded candidate
restarts as PID 6768; the completed conversation is loaded without a send.
Expanding and scrolling the persisted timeline reveals the saved child answer.
All nineteen table hashes, request records, audits, settings and dialogue summaries
remain identical to the closed-before-restore snapshot, including after normal
close of the restarted App. Both native windows/processes disappear and both
stderr logs are empty. The fixture also exits zero with parent/child counts 4/4,
and its port is no longer listening. There is no model-call or tool replay.

`verify-final.py` independently checks the request parameters/sharing flags,
saved identities, completed audits/payloads, two normal exits, stable restore
snapshots, files, zero artifacts, non-black capture metadata, DLL hashes, fresh
TRX counters and released endpoint. `final-verification.json` reports success.

#### Preserved limitations and diagnostic evidence

- The older AskEveryTime pending/cancelled probe is untouched. FullAccess's
  successful audit approval flag is not evidence of a user approving a prompt;
  the manual-approval success branch remains separate work.
- The fixture uses the same recorded tool-call IDs across two sequential runs;
  it proves those two completed cycles, not unique IDs or concurrent delegation.
- One Sky accessibility `set_value` attempt on the second composer reports a
  timeout while the draft changes; subsequent clicks do not send. Reobserving,
  focusing and using ordinary typed input produces exactly one second send.
  This is not diagnosed as a product bug or reported as a sending fix. Enter
  inserts a newline in this multiline composer and is not treated as send.
- Early snapshots immediately after releasing a held stream or before a send
  starts remain non-final evidence. `running-on-verified` and the completed
  snapshots, not their earlier similarly named probes, establish those states.
- The early `completed-off` verifier incorrectly expects a nonexistent dialogue
  ToolName field. Its corrected `completed-off-verified` snapshot checks Role and
  Content and confirms the stored file result; this was not data loss. The early
  settings-off JSON has a legacy Windows encoding, retained without rewriting
  and explicitly decoded by the final verifier.
- No real provider credentials, paid service, all-protocol native sweep,
  high-DPI/dark-theme matrix or same-state whole-window Codex parity is established
  by this bounded branch. The prior negative tests do not become fresh native
  rejection evidence. Whole-product acceptance remains active.
- Default launch output, user data, approval/privacy settings, packaging,
  JIT/JSON and unrelated dirty files are unchanged. Nothing is staged, committed,
  pushed, released or installed.
<!-- /source: SUBAGENT_SETTINGS_TO_RUNTIME_ACCEPTANCE.md -->


---

<a id="workspace-agent-selection-acceptance"></a>

## WORKSPACE_AGENT_SELECTION_ACCEPTANCE.md

<!-- source: WORKSPACE_AGENT_SELECTION_ACCEPTANCE.md -->
### 工作区代理选择验收

#### 2026-10-07 范围与设计复核

- `WorkspaceAgentPickerViewModel` 属于 App，通过既有代理配置和模型目录端口读取，
  拆分工作区的目录、选择与生命周期职责；领域、工具和基础设施不依赖 UI。
- 复用现有 `ModelPickerViewModel`、`GuardedAsyncRelayCommand`、共享输入区、按钮和滚动组件。
  不增加配置存储格式、数据库迁移或第二套组件；不修改授权、审批和工具执行管线。
- 读取传递生命周期及命令取消令牌，30 秒总读取期限；非协作读取也有有界等待。
  新读取取消旧读取，代次检查拒绝迟到快照；关闭后取消读取并解除订阅。
- 每个新增手写 C# 文件只有一个类型。目录所有者与页面测试分别拆分，
  文件、方法和 AXAML 硬限制审计通过，不以 partial 或嵌套类型隐藏规模。

#### 已复现并修复的问题

新增的四个实际工作区回归在修改前全部失败，而非从静态推测直接宣称修复：

1. 选中代理被停用后，原实现把选择清空；保留身份、显示不可用并拒绝发送。
2. 选择模型不可用的代理时，原实现清空代理但保留之前模型；不再静默使用旧模型发送。
3. 模型目录刷新为不可用时，原实现提前丢弃代理身份；修复后原模型恢复可重新绑定。
4. 激活旧菜单对象会采用旧提示和参数；现在只按身份解析最新目录记录。

读取期间保留旧行与草稿，但禁用新发送和代理菜单；读取失败显示本地化安全提示。
停用或删除不等于选择默认代理。用户明确选择默认代理、其他代理或模型才解除该选择；
迟到刷新不能恢复已放弃的代理。显式模型选择与目录刷新分别发布，避免混淆用户意图。
可用代理不再截断为前 16 项，菜单复用共享的 240-DIP 最大高度滚动变体。
配置不可用时运行中的停止操作保持可用；已捕获的运行配置不会被目录刷新改变。

#### 自动化验证

- 新增 39 项回归：代理身份/当前快照、迟到读取、失败、超时、取消、关闭、
  直接命令重入、全部代理、明暗主题及窄/宽输入区，以及实际本机 HTTP 请求。
- 138 项专项通过。真实 SQLite 和本机 Responses HTTP 验证当前模型、提示与 ultra 参数，
  配置读取失败不产生请求或任务，重试后才发送；读取中及失败时真实暂停任务仍可取消。
- 原有代理运行快照回归继续通过：配置保存不改变已发出的请求，下一轮才使用新配置。
- 组件回归发现新增滚动样式没有匹配包装类的原生 StyleKey，已改为共享样式身份选择器；
  全量规则也拒绝无图标的 AppActionButton，纯文字重试已改用共享 AppButton。
  这两项是本轮实现中修复的中间问题，不作为旧版本缺陷的证据。
- 完整解决方案 2,180 项通过：App 1,691、Agent 47、Browser 32、Infrastructure 58、
  Windows 50、Providers 155、Tools 147。构建 0 警告、0 错误。
- Scoped whitespace 格式检查、939 个 C#/AXAML 文件的单类型/文件/方法体积审计、
  `git diff --check` 通过；未暂存、提交或推送。

#### 普通权限原生验收

仓库外目录：`%TEMP%/MossAgent-workspace-agent-catalog-qa-20261006-01`。
使用真实 App/MainWindow、独立合成 SQLite 和不可达的本机模型端点，未发送原生模型请求。
Python 被动截图校验黑图、原生边界和精确尺寸，不激活、移动或缩放窗口。

- 浅色最大化实际 1920×1032：显示不可用代理、安全提示和禁用发送。
  实际点击菜单中的默认代理后提示消失，思考强度恢复默认，草稿保持，发送变为可用。
- 深色普通窗口实际 980×640：实际点击刷新后失效身份保持；两次实际滚轮操作
  到达第 29 个可用代理，实际选择后菜单关闭，草稿保持并恢复发送。
- 最终纯文字共享按钮版本再次截图并实际选择默认代理，验证失效和恢复两种状态。
  截图 `agent-dark-final-unavailable.png`、`agent-dark-final-default.png` 的同坐标、
  不缩放裁片生成 `agent-dark-state-comparison.png`。它是最终版的两个交互状态，
  不是旧实现与新实现的前后对比，也不是 Codex 参考图。
- 第一次无截图窗口观察不能提供输入几何；重新选择当前唯一窗口并获取截图后恢复操作。
  不把该次失败点击计为成功。三个临时宿主均正常关闭，退出码为 0。

#### 未验证范围

原生输入仅覆盖合成配置的选择、重试与滚动；目录故障、读取竞争、超时和请求发送由
自动化回归验证，未做真实账户或付费接口测试。未覆盖所有 DPI、全部主工作流与
Codex 逐像素一致。统一组件和此选择链路的通过不代表整个产品目标完成。
<!-- /source: WORKSPACE_AGENT_SELECTION_ACCEPTANCE.md -->
