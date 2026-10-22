# Git、工作树与 PR

> 历史归档：保留原报告当时的结论、实验、失败与限制。整理于 2026-10-21。
> 当前能力与未完成方案统一维护在[功能清单](../FEATURE_STATUS_AND_IMPLEMENTATION_PLAN.md)；
> 原文中的计划、主机状态、测试数字及“正在进行”等措辞均为历史检查点。
> 代码块和仓库相对产物路径保留原意；本机忽略的 `artifacts/` 不随克隆分发。

[文档入口](../README.md)

## 原报告索引

- [Diff review acceptance](#diff-review-acceptance) — `DIFF_REVIEW_ACCEPTANCE.md`
- [文件编辑快照与无变化写入优化验收](#file-change-snapshot-acceptance) — `FILE_CHANGE_SNAPSHOT_ACCEPTANCE.md`
- [文件快照增删行统计](#file-change-statistics-acceptance) — `FILE_CHANGE_STATISTICS_ACCEPTANCE.md`
- [文件变更摘要与刷新优化验收](#file-change-summary-acceptance) — `FILE_CHANGE_SUMMARY_ACCEPTANCE.md`
- [受保护文件撤销](#file-change-undo-acceptance) — `FILE_CHANGE_UNDO_ACCEPTANCE.md`
- [Windows snapshot-reader and atomic-edit coexistence](#file-snapshot-sharing-acceptance) — `FILE_SNAPSHOT_SHARING_ACCEPTANCE.md`
- [Staged Git commit workflow](#git-commit-acceptance) — `GIT_COMMIT_ACCEPTANCE.md`
- [Git 差异临时输入会话](#git-diff-input-session-acceptance) — `GIT_DIFF_INPUT_SESSION_ACCEPTANCE.md`
- [Git 进程所有权验收](#git-process-ownership-acceptance) — `GIT_PROCESS_OWNERSHIP_ACCEPTANCE.md`
- [Confirmed single-branch Git push](#git-push-acceptance) — `GIT_PUSH_ACCEPTANCE.md`
- [Git file staging workflow](#git-staging-acceptance) — `GIT_STAGING_ACCEPTANCE.md`
- [Pull request workflow: scope and implementation plan](#pull-request-workflow-plan) — `PULL_REQUEST_WORKFLOW_PLAN.md`
- [Repository status read ownership](#repository-status-lifetime-acceptance) — `REPOSITORY_STATUS_LIFETIME_ACCEPTANCE.md`
- [Restored worktree operation authorization](#restored-worktree-authorization-acceptance) — `RESTORED_WORKTREE_AUTHORIZATION_ACCEPTANCE.md`
- [Source-account connection checkpoint](#source-account-connection-acceptance) — `SOURCE_ACCOUNT_CONNECTION_ACCEPTANCE.md`
- [Verified PR inline comments and keyboard location](#source-pr-inline-comments-acceptance) — `SOURCE_PR_INLINE_COMMENTS_ACCEPTANCE.md`
- [Version-bound PR files, comments and checks](#source-pr-inspection-acceptance) — `SOURCE_PR_INSPECTION_ACCEPTANCE.md`
- [Version-bound, opt-in PR model reads](#source-pr-on-demand-reading-acceptance) — `SOURCE_PR_ON_DEMAND_READING_ACCEPTANCE.md`
- [Source repository authorization and PR reading](#source-pr-reading-acceptance) — `SOURCE_PR_READING_ACCEPTANCE.md`
- [PR snapshot review chat and investigation drafts](#source-pr-review-chat-acceptance) — `SOURCE_PR_REVIEW_CHAT_ACCEPTANCE.md`
- [Explicit PR URL opening](#source-pr-url-acceptance) — `SOURCE_PR_URL_ACCEPTANCE.md`
- [Explicit worktree base reference candidate](#worktree-base-reference-acceptance) — `WORKTREE_BASE_REFERENCE_ACCEPTANCE.md`
- [New-conversation worktree selection](#worktree-conversation-acceptance) — `WORKTREE_CONVERSATION_ACCEPTANCE.md`
- [任务工作树对象身份（进行中）](#worktree-object-authorization-acceptance) — `WORKTREE_OBJECT_AUTHORIZATION_ACCEPTANCE.md`
- [Managed worktree path and repository boundaries](#worktree-path-safety-acceptance) — `WORKTREE_PATH_SAFETY_ACCEPTANCE.md`

---

<a id="diff-review-acceptance"></a>

## DIFF_REVIEW_ACCEPTANCE.md

<!-- source: DIFF_REVIEW_ACCEPTANCE.md -->
### Diff review acceptance

#### Design and safety self-check (2026-10-09)

1. Tools.BuiltIn owns Git arguments, authorized path preflight, process execution
   and bounded output. App owns selection and localized loading/failure/empty
   presentation. No infrastructure or UI dependency is added to the tool modules.
2. Review continues through `IToolExecutor`, `git.status` and `git.diff` rather
   than reading files or starting processes in UI. Untracked single-file review
   extends the existing diff contract and does not stage or modify user files.
3. Existing authorization, read-only approval, task cancellation, process-tree
   cleanup and output limits remain in the standard pipeline. New untracked mode
   requires one authorized file, rejects staged/directory requests, and disables
   external diff/textconv helpers. The Git runner accepts diff exit code one only
   for the explicit mode and when stderr is empty. Command-local `core.safecrlf=false`
   suppresses irrelevant read-only LF/CRLF conversion warnings without ignoring real
   errors or modifying the repository/user configuration.
4. Changes stay in separate single-type C# files and existing wrapper controls.
   Loading/empty/failure rendering reuses the shared empty state; no per-view
   colors, dimensions or native visual controls are added.
5. Focused tests must cover actual untracked text/binary Git output, missing paths,
   outside-root requests, staged/directory rejection, literal pathspecs, selection
   identity and late results. Native acceptance must use temporary real Git/SQLite
   data, preserve the draft and index, and exercise both themes/window sizes.

#### Implemented behavior

- `git.status` lists individual nested untracked files rather than directory
  placeholders. An explicit `git.diff` untracked mode uses a no-index comparison
  against the platform null device. Added text receives real Git hunk/line data;
  binary additions retain Git's binary-file presentation. Neither mode stages files.
- Git paths are literal, Unicode header names remain readable and external
  diff/textconv helpers are disabled. An empty/missing file path, directory, staged
  combination or outside-root request cannot turn into unrestricted recursive review.
  A missing file that returns Git exit code one on stderr is still a failure.
- Refresh captures selection after the status response and preserves the current
  path if it remains listed. Equal-valued replacement records rebind by reference;
  unlisted instances cannot initiate tool reads. Filter changes update counts and
  retain the selected path when it also exists in the destination filter.
- Actual diff text is separate from localized loading, failure and empty messages.
  Errors no longer appear as fake code. Existing task/selection cancellation and
  version checks continue rejecting late responses. File preview still uses the
  existing authorized snapshot tool/reader and preserves the conversation draft.

#### Verification evidence

Evidence root: `%TEMP%\MossAgent-diff-review-20261009`.
Native harness code, Python capture/comparison scripts, Git/SQLite fixtures and
screenshots remain outside the repository; build output is in ignored artifacts.
No paid provider, user project/index or user database participates in acceptance.

Four initial App selection tests fail red, covering refresh, pending selection,
unlisted records and the former untracked placeholder. The tool red run captures
untracked text/binary/directory/missing-path gaps and nested directory placeholders.
The tracked literal/textconv failure is reproduced separately after fixing only
the test fixture's Windows read-only Git-object cleanup. A later real CRLF probe
and red test expose valid diff exit code one being incorrectly rejected because
Git emits an LF/CRLF warning. The final command-local warning suppression fixes
that case while retaining the missing-file stderr rejection.

New tests contribute 18 cases: App selection (4), App states (3) and real Git
tools/pipeline behavior (11). The focused App run passes 146 tests including
existing inspector/file-preview/parser regressions. The final focused tool run
passes 15 tests including existing Git status/preflight boundaries.

The final full solution run passes 3286/3286 tests with zero failures or skips:
App 2483, Tools 188, Providers 283, Infrastructure 136, Browser 76, Agent 63
and Platform.Windows 57. `full-verified.log` and the seven independent TRX files
under `full-verified/` record this run. Final build logs are
`solution-build-verified.log` and `native-verified-build.log`; scoped formatting
and structural checks are recorded in `format-verified.log` and `source-verified.log`.

Scoped whitespace verification and the Roslyn/XML audit cover the nine changed/new
C# files and the inspector markup. Every handwritten C# file contains one type;
the largest is 179 lines, markup is 266 lines and the longest method is 45 lines.
Native and solution builds report zero warnings/errors. No persistence schema,
serialization, AOT or release-packaging change is introduced.

#### Native interaction and pixels

The real App runs under ordinary permissions/JIT in Light 1280x820 and Dark
980x640 at render scaling one. A separate repository contains an actual tracked
file with different baseline, staged and working contents, a nested Unicode
addition and a binary addition. All review/preview calls use the production tool
executor and persist actual SQLite audit records.

Mouse interaction in both themes verifies selecting the added file, refresh
retention, authorized file preview, returning to the same selected path and
staged/unstaged filtering with counts three/one. Horizontal scrolling confirms
the staged content is baseline-to-staged and the unstaged content is
staged-to-working, rather than a cosmetic toggle. The small dark panel retains
reachable preview/refresh/filter controls and the existing compact preview toolbar.

The Light fixture also shows the real binary result. Removing only its synthetic
untracked file externally produces the localized empty/error state, not raw Git
stderr. Restoring that fixture file and refreshing restores the added patch.
The single failed Light audit is this intentional missing-file negative case;
Dark has no failed audit. Continuous checks confirm the draft remains unchanged.
Final Git cached diffs equal their pre-UI values and both cases exit normally with
`QA_INDEX_UNCHANGED: true` and `QA_EXIT: 0`.

The Light/Dark interaction runs precede the final command-local `core.safecrlf`
argument; their UI/renderer sources are unchanged by that tool-only repair.
`verified-CRLF-Light-1280-820` reruns the latest production assemblies against
the synthetic repository with `core.autocrlf=true`. Selecting the nested addition
and refreshing both return the real patch; all five ReadOnly audits succeed,
cached diff content remains unchanged, stderr is empty and the process exits zero.
After exit, the repository still has `core.autocrlf=true` and no local
`core.safecrlf` setting, confirming the read-only command does not persist it.
`QA_INDEX_UNCHANGED` compares cached diff content, not byte identity of Git's index.

Passive Python PrintWindow validates non-black output and exact dimensions.
`diff-review-before-after.png` uses unscaled 455x776 inspector crops. Before uses
the preceding App/tool assemblies and shows the former directory placeholder;
after uses the latest CRLF-case capture and shows the real nested-file patch.
It is a MossAgent before/after comparison,
not a same-state Codex reference. Individual captures include untracked, refreshed,
preview, staged, horizontal-scroll and negative/recovery states.

The first standalone QA harness captured the stopped Avalonia synchronization
context while synchronously waiting for its final external Git check after window
close. The harness-only async helper now uses `ConfigureAwait(false)`; two initial
cases are excluded from shutdown acceptance. Corrected baseline and final cases
exit normally. No production App shutdown behavior is changed by this repair.

#### Remaining limits

A same-state Codex diff reference is still needed for pixel-fidelity acceptance.
Higher-DPI/user-font combinations, native loading/empty states, disabled/keyboard
focus/hover geometry, split-view and sticky-gutter behavior are not newly accepted
by these screenshots. Pending/empty/late-response states are covered by focused
tests instead. These reads do not implement staging, discard, commit or other
mutation actions. Existing managed/JIT development and Windows x64 single-file
release settings remain unchanged; nothing is staged, committed, pushed or published.
<!-- /source: DIFF_REVIEW_ACCEPTANCE.md -->


---

<a id="file-change-snapshot-acceptance"></a>

## FILE_CHANGE_SNAPSHOT_ACCEPTANCE.md

<!-- source: FILE_CHANGE_SNAPSHOT_ACCEPTANCE.md -->
### 文件编辑快照与无变化写入优化验收

#### 2026-10-11 实现

- 文件写入和文本替换复用现有原子写入器，在成功提交后报告可选的前后快照引用。
  引用只有文件名、字节长度和 SHA-256；快照原始字节放在任务 artifact 目录，
  不写入 SQLite，也不追加到模型对话。未修改的旧载荷无需迁移。
- 原文快照保留 BOM、编码及换行；新增文件没有原文快照。后快照来自待提交
  暂存文件，而非提交后重新读取的实时文件，避免误记提交后的外部改动。
- 每份快照最大 2 MiB，恰好上限允许保存；更大文件仍可以按原有规则写入，
  但不生成对应快照。没有 artifact 目录的旧调用继续可用，不生成历史。
- 文件名仅允许 `edit-<32 位十六进制>.snapshot`。读取使用当前上下文的任务
  artifact 目录，检查祖先/目标重解析点、长度、哈希及取消；快照缺失或篡改时
  明确失败，不回退读取实时文件，不从引用接受任意磁盘路径。
- 快照生命周期与一次原子写入绑定。提交失败会清理未提交快照，成功的有效
  修改才保留历史。元数据输出仍使用既有整记录预算，不截半引用或哈希。

#### 优化与设计自检

1. 契约归 Tools.Abstractions；快照读写和原子提交归 Tools.BuiltIn。
   Agent 只传播记录，App 只持久化原始元数据，不移动文件 I/O 到 UI。
2. 复用原有工具契约、任务上下文、版本缓存、路径解析器和原子写入器。
   不新建用户工具，不增加恢复快捷通道，不添加全局可变状态。
3. 授权、审批、超时、取消、输出限制和审计顺序不变。原文和暂存后文在
   同一写入锁内捕获，最终提交仍重新校验原文件版本和授权路径。
4. 暂存编码完成后先比较哈希，只有确有修改时才捕获快照。无变化写入/替换
   不创建 artifact 目录，不再对原文和后文分别读取、复制、再删除。
   编码失败同样不先备份原文；原子提交及并发保护没有跳过。
5. 新增类型各自单独文件。专项测试覆盖真实前后字节、无变化零历史、提交失败
   清理及先前已提交历史保留、预取消、审批/越界拒绝、上限和超限、非法引用、任务目录隔离、
   丢失/篡改、junction 拒绝及整记录输出预算。

#### 真实运行证据

原生 QA 宿主、独立数据、日志和 Python 被动截图均保留在仓库外：

`C:\Users\wzheng\AppData\Local\Temp\MossAgent-file-snapshots-20261011`

- 普通权限宿主使用 loopback SSE 和合成项目，通过生产 Agent/工具管线
  修改四个预存源文件，模型请求两次。`completed.json` 确认四份记录、
  `SnapshotHistoryValid=true`、`PersistedHashesMatch=true`。
- 八份实际快照分别保存原文 `Value => -1` 和已提交的 `Value => 0/1/2/3`。
  外部 QA 使用生产快照读取器验证原文字节和后文，而非只检查文件存在。
- 浅色窗口实际截图为 1280×820；打开真实文件预览后，`preview.json` 确认
  `PreviewReady=true` 和历史有效。初始最大化日志不能替代实际截图尺寸。
- 同一数据目录 `--restore` 重新启动后请求数为零，四文件摘要和八份快照
  恢复有效，没有重放写入。`before-restore-snapshots.json` 与
  `after-restore-snapshots.json` 校验八份名称、哈希和 UTC 时间戳均不变。
- 恢复后的深色窗口实际为 1280×820，通过原生拖动缩窄至 980×640，
  正常滚动查看摘要并打开 `Sample0.cs` 真实语法预览。`restored-preview.json`
  确认实际尺寸、零请求、预览就绪和前后快照验证通过。
- 图片为 `native-light.png`、`native-light-preview.png`、
  `native-dark-restored.png` 和 `native-dark-narrow-preview.png`；没有缩放伪造。
- 最终源码重新构建 QA 宿主后，再以普通权限和全新 `native-final-data`
  运行实际修改链路：两次请求、四份文件变更、八份有效快照，落盘哈希匹配。
  `native-final-light.png` 是该次实际 1280×820 浅色窗口，缩放为 1；
  初始化时的 1920×1032 日志不能替代截图尺寸。
- 本轮没有操作 Codex/ChatGPT 界面，没有新 Codex 参考图。
  这些证据仅覆盖当前切片，不是全界面像素一致性证明。

#### 回归验证

- Tools 快照/文件编辑/输出专项通过 59/59，App 编辑与持久化专项通过 39/39，
  Agent 变更记录专项通过 19/19。无变化写入和替换均断言不创建 artifact 目录。
- 同一任务后续无变化、编码失败或提交失败均保留先前已提交的两份快照，
  不清理已有历史。子代理独立全量回归通过 69/69。
- 最终独立串行全量通过 4580/4580，八个程序集失败和跳过均为零。
  使用 `dotnet test MossAgent.slnx --no-build --no-restore -m:1`、详细日志及
  90 秒无进展守卫；计数只取 `full-verified` 的八份完整 TRX，
  `full-verified-summary.json` 保存独立汇总，不拼接专项或失败尝试。
- 解决方案及最终外部 QA 宿主构建均为零警告、零错误；13 个相关 C# 文件的
  专项格式、Roslyn 单类型/文件/方法规模审计和最终差异空白检查通过。
  原生验收与测试子进程均已退出，暂存区为空；已有无关改动未清理或暂存。
- 首次全量的子代理测试出现 `tool_schema_invalid`、进度断言失败和主机超时
  中止，共记录 23 项失败，不计为通过。未修改生产 schema 或超时逻辑的
  独立子代理全量与最终完整重跑均通过；首次异常原因尚未确认。
  失败日志、TRX 和转储保留在 `full-final`，不能因重跑成功宣称已修复该问题。
- 一次同时构建与 App 回归因测试 DLL 文件锁失败，记录保留为
  `build-during-tests.log`。最终等待测试正常退出后串行构建通过，没有为
  构建强行停止正常运行的 UI 回归。

##### 2026-10-11 异常确诊跟进

上面记录的首次子代理 schema 异常现已通过独立冷启动探针确诊：
JsonSchema.Net 的 BuildOptions 默认使用全局非线程安全 SchemaRegistry，
导致生产工具管线并发写入同一个字典。显式创建每调用私有注册表后，
64 个冷进程、16,384 次调用未出现失败。重复根 `$id`、合法/非法参数和
真实工作区 MCP 双任务均有回归覆盖，详见 [TOOL_SCHEMA_ISOLATION_ACCEPTANCE.md](TERMINAL_TOOLS.md#tool-schema-isolation-acceptance)。
保留首次 23 项失败及当时原因未明的事实；独立重跑成功不再作为根因证据。

#### 本轮记录时尚未接入

- 快照读取器是底层基础，没有注册成用户工具，没有接入 UI 撤销。
  恢复必须经标准管线并校验当前版本，不能直接覆盖或删除现有文件。
- 撤销按钮保持禁用；提示说明恢复和并发保护尚未接入，而不是误称无原文。
- 没有任务精确 diff 或行数统计，Git 检查器仍显示当前工作区差异。
  Shell/MCP 自行修改尚不由这些文件工具记录归因。
- 调试保持 JIT，发布保持 Windows x64 单文件；不恢复 AOT 或 JSON 源生成。
  本轮没有提交、推送、发布，不代表完整产品目标完成。

#### 2026-10-11 任务快照审查跟进

快照读取器现已用于注册的 `filesystem.review_change` 工具。文件摘要中的
查看变更和文件行打开前后快照的只读差异，不再读取当前文件或混入 Git
工作区既有改动。连续写入合并最初/最终版本，不连续历史明确提示，净变化
为零的文件不展示。当前 Git 检查器仍独立保留工作区审查功能。

详见 [TASK_SNAPSHOT_REVIEW_ACCEPTANCE.md](CONVERSATIONS.md#task-snapshot-review-acceptance)。撤销和受保护恢复仍未实现；
Shell/MCP 自行修改仍不归因，不能将此只读审查等同于完整工作区变更追踪。
<!-- /source: FILE_CHANGE_SNAPSHOT_ACCEPTANCE.md -->


---

<a id="file-change-statistics-acceptance"></a>

## FILE_CHANGE_STATISTICS_ACCEPTANCE.md

<!-- source: FILE_CHANGE_STATISTICS_ACCEPTANCE.md -->
### 文件快照增删行统计

#### 缺口与安排

2026-10-11 的 Codex 被动截图显示文件摘要总增删行数、逐文件统计及撤销图标；
MossAgent 只有路径和任务范围说明。本轮接通可信前后快照统计，再连接共享
卡片，验证连续编辑、撤销、历史恢复和布局。缺失或不完整记录不显示伪造零值；
不以单次编辑统计相加冒充任务最初到最终版本的差异。

#### 设计自检

1. Tools.BuiltIn 拥有快照验证及 Git numstat，App 服务只通过 IToolExecutor 端口
   调用。App 负责取消、任务身份、可观察统计和共享卡片，依赖方向不变。
2. 新只读 UI 工具复用 FileChangeRequest、FileChangeSnapshotReader 和任务拥有的
   Git runner；预览与统计共享临时文本准备/清理，不相互调用工具实现。
3. 标准授权、可信 ReviewedChange、长度/摘要/链接检查、审批、超时、取消、
   输出限制和审计保持启用。不读取当前文件或 Git 工作区、不运行模型、
   不写对话或修改用户文件。后台查询串行，新版本取消旧版本，关闭等待收尾。
4. 每个 C# 文件仅一类型，保持 JIT JSON 和 Windows x64 单文件配置。颜色、
   图标和尺寸放在共享变体，页面不覆盖外观；不新增数据库迁移。
5. 回归覆盖真实增删行、新建/删除、连续写入首末快照、外部修改不影响历史
   统计、非法/跨任务/链接快照、审批/取消/限流、迟到结果、任务切换、退出收尾，
   以及明暗/窄宽几何和真实命令。原生运行验证卡片数值及重启不重放。

#### 验证记录

##### 接续精简安排

- 保留已运行回归，串行完成最终构建与测试，不重新实现已接通的工具。
- 总计刷新从四次集合扫描改为一次；仅发布实际改变的数值与状态，重复行结果不再触发绑定刷新。
- 行按钮显式使用显示路径作为可访问名称，覆盖展开前后与明暗/窄宽布局。
- 原生复测使用独立数据及复制输出，不操作日常项目或重放历史模型请求。

2026-10-11，证据根目录：
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-chat-layout-20261011`。

##### 自动回归

- 保留上一版 `full` 的 8 份 TRX：4783/4783；它不包含接续的绑定及刷新精简，
  不计作最终版本验收。
- 最终 App 专项 `app-final-passed`：76/76；工具专项 `tools-final-passed`：162/162。
  新增两项测试验证一致的通知快照、重复结果不通知、已解析行不重复查询。
  路径名称与真实打开命令在明暗、280/740 宽度、展开前后均验证。
- `solution-final-build.log`：0 警告、0 错误。
- 最终 `full-final` 的 8 份 TRX 独立计数：Agent 83、App 3450、Analyzers 8、
  Browser 76、Infrastructure 171、Windows 137、Providers 377、Tools 483；
  合计 4785/4785，失败和跳过均为 0，命令退出 0。
- `format-final.log`：17 个相关 C# 文件的限定格式验证退出 0。
  结构审计确认每文件一类型、最长文件 497 行、最长方法 62 行；
  3 个 AXAML 为 9/56/103 行，无语法错误或尾随空白。`git diff --check` 通过。
- 保留 `app-final` 的 42/43 失败记录：新测试预先赋值的行在首次设置任务身份时
  被正确清除；测试改为先建立空任务上下文，再验证同上下文的行缓存，
  未放宽生产任务切换保护。更早的编译与布局测试失败证据也未删除。

##### 普通权限原生验收

- 初次运行使用独立 SQLite 与四个既有合成文件，经真实模型 loopback、
  标准写入工具和 Git numstat，逐文件 +1/-1、总计 +4/-4。
  共享行按钮新增 Grid 后可访问名称原为 `Avalonia.Controls.Grid`，
  已改为显式路径绑定，最终 UIA 记录为 `button src/Sample0.cs` 等。
- 最终浅色 1280×820、深色 980×640，缩放 1、日志 `QA_ELEVATED=False`。
  原生验证展开第 4 行、通过文件行打开真实快照补丁、右栏自动打开及上下文
  卡片隐藏；最终浅色历史加载模型请求为 0，统计仍 +4/-4，预览只读且就绪。
- 深色撤销确认明确 4 个文件、0 个新建文件；先取消，统计与字节不变，再恢复。
  未操作文件删除。恢复后 Files/VisibleFiles 为 0，HasTotals/HasError 为 false，
  无确认或待保存状态。重新启动只加载反向历史，模型请求仍为 0。
- 仓库外 `check-native-store.py` 使用 SQLite 只读连接：写入审计始终 4，
  恢复成功审计 4，正向/反向记录共 8、反向摘要交换匹配；四文件字节及 SHA
  与合成原始内容完全一致，重新加载不新增写入或恢复审计。
- 最终三次原生运行日志均含正常退出及 `QA_REQUEST_COUNT=0`，无 `QA_FAILURE`。
  初版观察器因读取元数据时未共享 Delete，与原子替换竞争而停止；该失败日志
  保留，最终读取使用 ReadWrite/Delete 共享，未改动生产存储或隐藏错误。
  最后关闭时 Computer Use 曾报告坐标几何不可用；重新绑定并观察后正常关闭，
  未强制结束进程。交付检查无 QA 窗口或 testhost，暂存区仍为空。

##### 截图与范围

- `native-light-final.png`、`native-dark-final.png`、`native-dark-expanded.png`、
  `native-light-preview-final.png`、`native-dark-undo-confirm.png` 由仓库外 Python
  被动截图生成。原生 UIA 和 `.json` 状态与截图分开留存。
- `file-statistics-comparison.png` 将当前 Codex 与最终 MossAgent 卡片按原始像素
  并排裁切，未缩放，合成内容不同。Codex 仅被动截图，未自动化操作其界面。
- 卡片路径文本、“再显示”入口与图标光学校正仍可继续对照优化；本轮没有
  宣称全页像素一致。所有 DPI、真实提供商、子代理及完整产品验收仍需独立验证。
  保持 JIT 调试、Windows x64 单文件发布；未提交、推送或发布，整体目标未完成。
<!-- /source: FILE_CHANGE_STATISTICS_ACCEPTANCE.md -->


---

<a id="file-change-summary-acceptance"></a>

## FILE_CHANGE_SUMMARY_ACCEPTANCE.md

<!-- source: FILE_CHANGE_SUMMARY_ACCEPTANCE.md -->
### 文件变更摘要与刷新优化验收

#### 2026-10-11 实现

- `filesystem.write_file` 和文本替换工具在原子提交成功后报告实际路径及前后
  SHA-256；内容相同不产生变更。失败、拒绝、冲突和提交前取消不伪造写入。
- 元数据与文本、子代理对话共享既有输出预算，优先保留完整合法变更记录。
  路径和哈希不截半；超预算或无效记录显式标记记录不完整。
- 子代理成功、模型失败、取消、超时及观察回调失败都保留已提交的子工具变更。
  Agent 仅汇总数据，不直接读写文件或调用操作系统。
- 实时消息与持久化使用同一原始载荷映射。无变更的旧 JSON 继续保留六字段形状，
  可选字段不需要数据库迁移；仍使用普通 JIT JSON 序列化。
- 共享 `AppFileChangeSummary` 使用统一卡片、图标、行高、圆角和浅色阴影，
  默认显示三个文件，可展开全部。C# 和 AXAML 门禁拒绝页面直接覆盖组件外观。
- 文件预览复用既有授权读取管线；查看变更打开真实 Git 检查器，校验当前任务身份，
  取消可恢复刷新，任务切换不接受迟到结果。恢复记录不重放工具写入。

#### 优化与设计自检

- 行为分别由 Tools.Abstractions/BuiltIn、Agent、Infrastructure、App 现有职责
  所属模块实现，依赖方向不变，不添加服务定位器或跨任务可变缓存。
- 摘要仅是内存投影：订阅按消息增删替换更新，不再每次增添消息都对所有行逐一
  `Contains` 扫描。普通文本和无文件变更的子代理进度不触发摘要刷新。
- 同位置、同路径、同哈希的文件行保留原实例，不清空并重建整个可观察集合。
  `VisibleFiles` 不在每次 getter 中创建数组；记录未变化时保留折叠快照。
- 保留路径大小写去重、最新写入优先、消息重排、重复行引用、重置、任务切换和
  Dispose 行为。真实更新只替换受影响的文件行，旧折叠快照保持原内容。
- 新建 Git 仓库的分支使用 `symbolic-ref --quiet --short HEAD` 读取，
  不再将没有首次提交的仓库误判为非 Git。仅 detached HEAD 使用原分支查询回退；
  超时、取消、截断、进程清理和显式工作目录仍由已有 Git runner 负责。
- 文件和 Git 操作仍经过既有授权、审批、超时、取消、输出限制与审计端口。
  不伪造精确差异，不新增后台文件扫描，也不改变发布或审批设置。
- 本轮审计的手写 C# 文件每文件一类型、均不超过 500 行/方法 80 行，相关 AXAML
  不超过 500 行。已有 `WorkspaceViewModel` 为 485 行，不继续向其堆放功能。

#### 原生交互证据

所有测试数据、外部 QA 宿主、Python 截图脚本和原始图片保留在仓库外：

`C:\Users\wzheng\AppData\Local\Temp\MossAgent-file-changes-20261011-083951`

- 普通权限宿主使用独立 SQLite、授权合成项目和 loopback SSE，无真实账号或付费
  请求。通过生产 Agent/工具管线写入四个合成源文件，模型请求共两次。
- 浅色实际截图为 1280×820、缩放 1；初始宿主日志虽为最大化 1920×1032，
  截图时实际已还原，不能用最初日志替代截图尺寸。
- 鼠标展开更多后四文件可见；点击文件打开实际语法高亮预览；查看变更打开
  Git 状态与未跟踪文件差异。`light/expanded.json`、`preview.json`、`review.json`
  和对应 `native-light-*.png` 记录真实状态。
- 优化后使用同一数据目录 `--restore`，请求数为零、哈希匹配，没有重放写入。
  四个文件的哈希和 UTC 写入时间均不变，见 `restore-file-verification.json`。
- 深色还原窗口实际为 1280×820；通过原生拖动缩窄至 980×640，再复测滚动、
  查看差异、展开和文件预览。`narrow-restored.json`、`restored-review.json`、
  `restored-preview.json` 及 `native-dark-*.png` 记录实际尺寸和可用状态。
- 最小窗口收起检查器时，浮动上下文卡片压缩对话视口，摘要需要正常滚动查看。
  打开检查器后卡片隐藏，摘要标题、文件行和操作可见，窄副标题按设计截断。
- 新仓库已真实显示 `master`，未在合成仓库创建提交来掩盖原错误。
  两个验收进程均通过关闭信号正常退出；恢复模式最终请求数仍为零。
- 本轮未操作 Codex/ChatGPT 界面。新 Codex 被动截屏因目标不是前台而未成功，
  仅参考此前真实 `MossAgent-main-fidelity-20261010-1530/codex-passive-reference.png`。
  因此这不是对当前版本 Codex 的全界面像素一致性验收。

#### 回归范围

- 文件写入/替换、元数据输出预算和子代理终态包含成功、拒绝、冲突、取消、超时
  与异常记录保留测试；编辑链路同时校验落盘消息及摘要投影。
- App 覆盖旧载荷恢复、非法路径拒绝、查看差异的任务身份/取消/迟到保护，
  明暗与 280/740-DIP 组件布局，以及共享外观边界的正负向测试。
- 七个新增刷新回归覆盖无关消息零发布、等价结果实例复用、单行更新、重复引用、
  消息重排、重置/替换订阅和变更元数据移除。不是整机延迟或内存占用基准。
- Git 回归覆盖 unborn 分支、detached 成功/失败、取消与截断禁止回退；
  原进程 runner 的流预算、超时和清理验证继续保留。
- App 专项通过 50/50，Git 专项通过 20/20；之后增强了真实越界文件读取负向用例，
  增补 Git 查询后取消不启动回退的用例。独立完整 App 回归通过 3385/3385。
- 33 个相关 C# 文件与三个 AXAML 的规模/单类型审计通过；专项格式检查通过，
  解决方案构建零警告、零错误。测试不要求真实付费 API。
- 首次最小日志全量运行在 App 阶段被人工中断，不作为完整成功结果，也不据此
  宣称产品存在或修复了阻塞。详细日志的独立 App 回归正常完成，耗时约 4.7 分钟。
  最终全量结果须取自独立完整串行重跑，不能拼接首次运行与独立 App 的计数。
- 最终 `dotnet test MossAgent.slnx --no-build --no-restore -m:1` 带详细日志及
  90 秒无进展守卫完成：八个程序集共 4555/4555，通过数取自 `full-final` 八份 TRX，
  失败和跳过均为零。`full-final-summary.json` 保存独立汇总，不包含首次中断结果。
  格式、规模和 `git diff --check` 通过；最终暂存区为空，QA 与测试子进程均已退出。

#### 明确未完成的能力

- 后续切片已增加有界原文及提交后快照，见 [FILE_CHANGE_SNAPSHOT_ACCEPTANCE.md](GIT_WORKTREES.md#file-change-snapshot-acceptance)；
  受保护的恢复流程尚未接入工具管线和 UI，撤销按钮仍明确禁用。
  本文前面的验证记录属于摘要/刷新切片，没有伪造新增/删除行数统计。
- 只统计该任务文件工具实际提交的写入，包括子代理；Shell/MCP 自行修改暂不归因。
- Git 显示当前工作区差异，可能包含原有或外部改动，不等于本任务精确变更快照。
- 本轮只完成该功能与优化切片，不代表完整产品及 Codex 一比一目标完成。
  调试保持 JIT，发布保持 Windows x64 单文件；没有提交、推送或发布。
<!-- /source: FILE_CHANGE_SUMMARY_ACCEPTANCE.md -->


---

<a id="file-change-undo-acceptance"></a>

## FILE_CHANGE_UNDO_ACCEPTANCE.md

<!-- source: FILE_CHANGE_UNDO_ACCEPTANCE.md -->
### 受保护文件撤销

#### 设计自检与实施顺序

1. Tools.BuiltIn 拥有快照校验、版本前提检查、原子替换和新建文件删除，复用
   AuthorizedPathResolver、FileChangeSnapshotReader 和共享 AtomicTextFileWriter。
   App 只调用 IToolExecutor/持久化端口，不直接操作文件、进程或 SQL。
2. 恢复与删除分别为独立工具。与只读审查共享 schema 和可信记录校验；模型
   catalog 不宣传这些工具。参数必须完整匹配每次请求的 ReviewedChange，不能
   借自由模型参数恢复或删除任意文件。没有静态服务定位或可变全局状态。
3. 所有请求经 schema、能力、路径、审批、期限、取消、输出限制和审计管线。
   恢复是 Mutation，删除是 HighRisk，FullAccess 不自动放行高风险删除。
   当前 FullAccess 会拒绝该删除；AskEveryTime 必须取得真实批准，不绕过策略。
   不覆盖外部改动、不回退到当前文件当作历史原文，不递归删除或修改授权根。
4. 恢复保留原始字节、BOM、编码和换行，不转换为 UTF-8。复用已校验的前后
   快照引用来报告反向变更，不再拷贝同样的历史文件。不存在版本的 SHA-256
   用 null 表示，删除另须显式 AfterDeleted=true；两个版本都不存在仍是无效
   记录。旧记录不增加 false 字段，旧无效 null-after 不被重新解释为删除。
   每个手写 C# 文件只有一个类型，文件/方法保持既有硬上限；不增加迁移或 AOT。
5. 聚焦回归将覆盖原字节恢复、新建文件删除、删除记录再恢复、空文件、审批
   拒绝、只读策略、超时/取消、输出限制、被修改/缺失/跨任务快照、路径穿越、
   链接、外部改写和同一路径并发写入，并检查反向记录及审计恢复不重放。

完整接入顺序为：底层两个工具和缺失版本语义 → 确认/取消交互、运行状态与
任务切换保护 → 每个已提交反向记录的持久化与摘要更新 → 原生点击、明暗主题
及重启不重放验收。不能仅因底层测试通过就把“撤销”按钮当作已完成。

#### 当前边界

恢复/删除底层能力已经连接文件摘要的“撤销”按钮，执行前先确认完整文件列表。
运行中任务、只读策略、不完整记录或缺失快照不会允许开始；确认后逐项执行，
不是多文件事务。取消、切换任务/项目、策略改变或关闭应用会取消请求，已经
提交的反向记录仍使用独立五秒 cleanup token 保存，不以已取消的请求 token 丢掉历史。
App 独立服务只调用执行器和仓库端口，不直接操作文件或 SQL，不增加迁移或 AOT。

FullAccess 下删除新建文件的这一请求收紧为 AskEveryTime，仍须真实高风险审批；
不修改任务策略，不放宽 ReadOnly。内部结果限额固定为 64 KiB，以保留完整反向
元数据，再经标准执行器限流和审计。确认后的 scope、当前剩余记录和原始版本
持续校验；旧任务结果只保存到原任务，不能发布到新任务的消息列表。

每项结果先经共享工具消息序列化保存，随后更新当前摘要。保存失败保留原消息
对象并停止后续撤销，禁用新撤销；“重试保存记录”只核对并写入相同历史 ID，
不再次执行文件操作。ID 冲突且内容不同时不覆盖。退出时先等待运行结果并重试
保存；仍失败则阻止正常关闭。沿用现有显式强制退出入口，其仍可能丢失内存记录。
旧的手工端口测试只证明端口；新增命令链路和原生验收结果必须分别报告。
相同字节恢复采用现有工具的版本检查/共享互斥和原子替换语义；不是多文件
事务或操作系统级跨进程 compare-and-swap。不能自动撤销 Shell/MCP 自行修改。
#### 底层阶段验证记录（2026-10-11）

仓库外证据位于 `C:\Users\wzheng\AppData\Local\Temp\MossAgent-file-undo-20261011`。
聚焦工具回归 `tools-final/undo-final.trx` 为 123/123，App 端口集成回归
`app-verified/undo-app-verified.trx` 为 28/28。解决方案构建零警告、零错误；
18 份 C# 的限定范围 whitespace format verify 与 git diff whitespace 检查通过。
文件行数审计最长为 AppComposition 的 215 行；不把文件行数当作方法行数验证。

全量运行进程退出码为 0，独立解析 `full-final` 的八份 TRX，合计 4695/4695，
无失败或未执行项：Agent 83、App 3395、Visual 8、Browser 76、Infrastructure
171、Platform.Windows 137、Providers 377、Tools 448。解析明细保留在
`full-final-counts.json`。此前编译和格式失败的日志保留，不计入通过结果。

底层阶段没有原生撤销点击验收，当时 UI 尚未接线。以上测试不代表完整产品已验收，
也不证明已经解决此前偶发的 HTTP MCP fixture 超时。

#### 确认与保存阶段（2026-10-11）

上一轮全量中恢复并发用例一次报告 tool_failed，而非 file_changed。原因是恢复
在共享写锁之外读取当前文件，FileShare.Read 会与 Windows 原子替换竞争。本轮
直接把可信 after 版本作为 writer 的前提，实际文件读取与版本/cache 校验统一
在共享锁内进行；缺失版本仍要求当前文件不存在，摘要大小写按既有语义归一化。
两个底层工具不再重复读取目标文件；没有减弱授权、快照校验或并发前提。

证据目录为 `C:\Users\wzheng\AppData\Local\Temp\MossAgent-undo-ui-20261011`。
新增恢复回归为 53/53；新增 App 命令/持久化回归为 20/20，相邻回归为 40/40。
共享组件布局覆盖明暗主题和 280/740 DIP 宽度，确认/取消按钮使用真实命令及
28 DIP 共享尺寸。首次全量发现新增布局测试用了异步 Action，已改用等待完整
回调的 DispatchAsync；失败日志保留，不把该次运行记为通过。

原生普通权限窗口使用真实 loopback 模型、工具执行器和 SQLite 保存链，在
仓库外四个既有合成源文件上验收。1280×820 浅色窗口先取消，文件摘要仍为
4、反向历史为 0、模型请求为 2；重新确认后，四个文件原始内容全部恢复，
摘要净零、反向历史为 4、没有待保存记录，模型请求仍为 2。正常关闭后，
980×640 深色窗口加载同一历史，反向记录仍为 4、摘要净零、原始内容不变，
本次模型请求为 0；正常退出日志再次确认请求为 0，没有自动重放。

原生窗口均为 1 倍缩放且未提升权限；Python 仅被动截图，未操作其他窗口。
截图为 native-light-confirm-clean.png、native-light-complete.png 和
native-dark-restored.png，观测值保存在 native-light-complete.json 和
native-dark-restored.json。原生夹具没有新建文件，删除审批只由真实执行器的
自动回归验收；没有在用户界面中执行删除。整个 Codex 界面像素级对照、真实
提供商及子代理完整验收不属于这些证据。保存失败时的原生退出交互已在后续
[SHUTDOWN_RECOVERY_ACCEPTANCE.md](TERMINAL_TOOLS.md#shutdown-recovery-acceptance) 单独验收，包括失败重试、存储恢复后退出和
重启不重放；不把后续结果计入本阶段 4730 项回归。

18 份本轮 C# 的 Roslyn 结构审计通过：每文件一个类型、文件最多 496 行，
方法最多 55 行，无语法错误或尾随空白；三份 AXAML 也低于硬上限。
限定范围 whitespace format verify 和 git diff whitespace 检查通过；最终完整
解决方案构建零警告、零错误，布局及异步等待入口回归为 9/9。恢复/删除并发
竞争两用例重复十轮，独立解析十份 TRX 为 20/20，没有覆盖或删除并发写入。
这只证明共享 writer 的已覆盖竞争，不代表操作系统级跨进程 CAS 或所有文件锁情形。

首次全量独立解析为 4729/4730，仅异步等待入口检查失败；修正后重新构建并
复测，进程退出码为 0。独立解析 full-verified 的八份 TRX 为 4730/4730，
结果行数与计数器一致，没有失败或未执行项：Agent 83、App 3419、Visual 8、
Browser 76、Infrastructure 171、Platform.Windows 137、Providers 377、Tools 459。
明细保存在 full-verified-counts.json；首次失败、格式失败和后续成功日志均保留。
JIT 调试与 Windows x64 单文件发布配置未改变，没有提交、推送或发布；验收
结束后本轮原生 QA、dotnet 和 testhost 进程均已退出，暂存区为空。
不把 Headless 测试当作原生点击或整个产品验收。
<!-- /source: FILE_CHANGE_UNDO_ACCEPTANCE.md -->


---

<a id="file-snapshot-sharing-acceptance"></a>

## FILE_SNAPSHOT_SHARING_ACCEPTANCE.md

<!-- source: FILE_SNAPSHOT_SHARING_ACCEPTANCE.md -->
### Windows snapshot-reader and atomic-edit coexistence

#### Confirmed defect

`FileSnapshotReader.Open` was opening internal read handles with `FileShare.Read`
only. On Windows, a still-open internal snapshot/preview handle therefore blocked
`File.Replace` in the atomic writer. The standard tool pipeline returned
`tool_failed` even though the task was authorized and the file version matched.

Two deterministic write/replace cases hold the production snapshot reader open
while invoking the real tools through `FileEditingFixture.ExecuteAsync`. Both
fail with the original production reader. The original baseline is retained at
`artifacts/snapshot-sharing-20261018/results/baseline` and its accompanying log.

That baseline also contains an initial external-save control using
`File.Move(overwrite: true)`. On this Windows host it fails even after adding
delete sharing, so it is not evidence about `File.Replace`. The final external-
save control explicitly uses `File.Replace`, the primitive this change supports.
The intermediate failed test runs are preserved, not relabeled as passes.

This independently proves a real internal reader/atomic-writer conflict. It does
**not** prove that it caused the two older full-App authorization failures:
those reports have no captured exception stack identifying their direct cause.

#### Ownership and safety review

Tools.BuiltIn owns the change. The existing reader, writer and shared execution
pipeline suffice; no application port, UI access, dependency or platform API is
added. The read handle now uses `FileShare.Read | FileShare.Delete`, not
`FileShare.Write`. Atomic replacement can publish a new version while the held
handle continues reading the original bytes. In-place mutation remains blocked
on Windows, and hash validation still observes the current path before editing.

Authorized-path validation, risk/approval checks, task-owned version caches,
timeouts, cancellation, staging, history bounds and audit/output handling are
unchanged. External handles explicitly denying delete sharing still reject the
commit; no retries or forced unlocks bypass that restriction. Existing
`AtomicTextFileWriterTests.TargetLockRejectsCommitWithoutTruncatingOriginal`
continues to cover it.

Two history-cleanup tests previously used the internal reader to manufacture
an external commit lock. They now create an explicit `FileStream` with
`FileShare.Read`. Their exception, original-byte, retained-history, uncommitted-
snapshot cleanup and temporary-file assertions remain unchanged. The first
full-tools run exposed these outdated fixture assumptions (497 passes / 2
failures); its report is retained under `results/all-tools`.

New tests stay in one type/file and cover:

- Both write and text replacement with an open snapshot, exact old/new bytes,
  bounded history enabled and advancement of the task's observed version.
- Windows refusal of in-place write access while the snapshot is open.
- An external atomic replacement followed by rejection of a stale task write;
  the original handle still returns the original bytes.
- A task with no authorized roots being denied despite an already-open handle.

#### Validation and remaining scope

The isolated final candidate is `artifacts/snapshot-sharing-fixed-20261018`.

- `results/focused-tools-final`: **56/56 passed**, no skips.
- `results/all-tools-final`: **499/499 passed**, no skips, including existing
  traversal, links, authorization, approvals, cancellation, concurrent edits,
  external locks, history cleanup, preview and schema coverage.
- `results/expanded-app`: **113/113 passed**, no skips; includes all eight
  production-composition authorization cases, model initialization/reloads,
  independent draft/background persistence and isolated allocation controls.
- Solution build completed with **0 warnings and 0 errors**; log is the sibling
  `artifacts/snapshot-sharing-fixed-build-final-20261018.log`.
- `source-shape.json`: **157** changed/untracked handwritten C# files, one type
  each, maximum 499 file lines / 67 method lines, no syntax/shape violations.

These expanded App checks are not a complete solution regression. The older
frozen full run was still executing at 13:19 +08:00, with its original reported
failures; this latest candidate has not had a complete regression yet. This is
deterministic Windows filesystem/pipeline
evidence, not a native preview-click or real-provider test. No normal user window,
credentials, approval settings, AOT/trimming, release, commit or push is changed.

The old frozen full run has since ended: all eight authorization cases pass,
before this reader change, while model initialization and allocation are its
only two App failures. Therefore this fix remains an independently reproduced
reader/edit defect, not a claimed explanation of the older authorization reports.
The later thumbnail candidate includes it in ordinary full session 95889; that
run's final result must be checked independently.
<!-- /source: FILE_SNAPSHOT_SHARING_ACCEPTANCE.md -->


---

<a id="git-commit-acceptance"></a>

## GIT_COMMIT_ACCEPTANCE.md

<!-- source: GIT_COMMIT_ACCEPTANCE.md -->
### Staged Git commit workflow

#### Plan and required design self-check

1. Tools.BuiltIn owns independent preview and commit tools, a bounded index/HEAD/
   branch snapshot reader and typed failures. Reuse the Infrastructure repository
   authorization port; App coordinates tools and never accesses files/processes.
2. Use the existing owned Git runner, task-workspace guard, shared approval/audit
   pipeline and shared modal controls. Do not add a shell bypass or a second index.
3. Read-only preview captures repository identity, symbolic/detached/unborn HEAD
   and the complete bounded staged-index representation. Confirm a snapshot token
   after ordinary write/process approval. Reject changed state, conflicts, empty
   index changes and invalid messages. Run ordinary staged-only `git commit`,
   retaining configured hooks/signing: no automatic stage-all, amend, identity
   changes, hook bypass, push or retry. Hooks are approved effects, not OS-sandboxed.
4. Keep types/methods split within repository limits. A dedicated dialog view
   model owns generation/cancellation/cleanup; do not grow the 499-line files.
5. Verify real initial/normal/detached commits, staged-only content, branch/HEAD/
   index changes including approval-time mutation, denial/ReadOnly, hook failure,
   cancellation/deadline/output/audit and task/draft isolation. Exercise production
   UI/keyboard confirmation with real Git, shared light/dark Headless controls and
   focused regression plus the solution build.

#### Status

Implementation is present; expanded regression of the frozen candidate is in
progress. This does not complete push, Pull Request, hunk/group staging,
native-window/restart acceptance or the whole product. Snapshot checks do not
claim native handle-level TOCTOU protection.

#### Runtime behavior

- Preview authorizes the complete repository and metadata through the existing
  port. Two bounded reads capture HEAD, symbolic/unborn/detached branch and the
  full staged-index representation around reauthorization. Queries disable
  optional locks/fsmonitor and reject incomplete or oversized output. The staged
  file count and repository/metadata/index/HEAD/branch token are returned together.
- Confirm uses the captured authorization roots and state token. The standard
  pipeline rechecks the task guard after approval; the tool reauthorizes and
  rejects changed HEAD, branch, index, conflicts and empty staged changes before
  executing ordinary staged-only `git commit` with a literal bounded message.
  Messages cannot become command switches. No stage-all, amend, identity edits,
  hook/signing bypass, push or automatic retry is added.
- The production inspector owns an accessible shared confirmation dialog with
  multiline message, staged count, branch/HEAD, refresh, cancel and busy states.
  Success refreshes actual Git status; failure/cancellation retains the message,
  invalidates the preview and requires explicit refresh/reconfirmation. A cancelled
  commit may already have taken effect: users must inspect history before retry.
  Per-task message drafts are in memory only, not persisted across restarts.
  Navigation rejects obsolete results and asynchronous disposal awaits actual
  tool and refresh completion, including noncooperative cleanup.
- Both dedicated Git runners strip `GIT_` child environment variables, after
  inherited values and explicit overrides are combined. This closes inherited
  index/object/config/trace redirection without mutating the parent process.
  Windows validates prefix inputs. Environment-only identity/config/SSH settings
  are intentionally not inherited; normal repository/user/system configuration,
  hooks and signing still apply. Shell tools retain their existing environment
  semantics, and hooks are not operating-system-sandboxed.
- The shared dialog now returns focus when its trigger is reenabled by the close
  command, deferring only if needed. Newer focus, hidden/disabled targets, reopened
  dialogs and detached views prevent delayed focus theft. No per-view appearance
  overrides or additional native visual controls are introduced.

#### Focused evidence

The isolated candidate is `artifacts/git-commit-20261018`. No real user repository,
window, credentials, API or existing database is used for test side effects.

| Check | Terminal result |
| --- | --- |
| Commit/Git environment/runner tools | 35/35 in `git-commit-3.trx` |
| Commit UI, shared dialog, caption focus | 87/87 in `git-commit-app-2.trx` |
| Windows process input/environment | 11/11 in `git-commit-windows-env-1.trx` |
| Changed C# single-type, syntax and size audit | 293 files, no violations |
| Largest audited C#/AXAML file; method | 499 lines; 72 lines |

The tool cases include actual normal/initial/detached commits, staged bytes vs
later working edits, unselected-file preservation, conflict/empty-index rejection,
approval-time branch/HEAD/index changes, hooks rejecting commits, missing identity,
denial/ReadOnly, guard revocation, cancellation/deadline cleanup, output bounds and
audit completion. An injected alternate index and trace destination remain
untouched by real Git stage/commit. Windows cases cover inherited and explicitly
overridden variable removal, invalid prefixes and unchanged parent environment.

Production App composition with temporary SQLite and Git exercises the actual
inspector entry and keyboard confirmation: HEAD advances, only staged bytes are
committed, the unselected file stays uncommitted and conversation input survives.
View-model probes additionally cover task/message isolation, failure recovery and
waiting for real execution/refresh cleanup. Four light/dark, 280/640-DIP Headless
cases generate eight ready/interrupted images; inspected narrow-light and
wide-dark output has readable controls and no clipping. This is not native UI or
screen-capture acceptance.

The first expanded focused App run retained four genuine focus-return failures
(9 passed/4 failed). Closing the modal changed visibility before the disabled
open command could update, so immediate focus restoration skipped the trigger.
Shared deferred restoration and six negative/reenabled-trigger regressions now
pass. The original failed report is preserved, not overwritten. Earlier focused
passes overlap the final grouping and must not be added as separate coverage.

#### Expanded frozen checkpoint

The original candidate's `Run-Validation.ps1` has now terminated with actual exit
zero. All seven commands succeeded and all six reports are Completed:

| Scope | Passed/total |
| --- | --- |
| Complete Tools | 565/565 |
| Complete Infrastructure | 442/442 |
| Complete Agent | 140/140 |
| Related App/settings/automation/navigation, excluding names containing Native | 1,927/1,927 |
| Complete shared Visual | 300/300 |
| Complete Windows | 146/146 |
| Total, without overlapping focused reports | 3,520/3,520 |

The solution build has zero warnings/errors. There are no failed or skipped cases
in these reports. `validation-terminal.json` records the actual exits, times and
scope. All 2,158 source/configuration and 2,439 runtime entries, source ZIP bytes
and source/runtime path sets agreed at terminal. Only documentation changed
during the run. This is expanded related regression, not whole-solution or native
acceptance. This snapshot is frozen and will not be rebuilt.

A subsequent candidate, `artifacts/git-commit-completion-20261018`, adds explicit
success-state and production focus assertions. Its source changes are not part
of the earlier frozen snapshot; the earlier 3,520-case pass must not be relabeled
as full regression of later code.

#### Success-completion follow-up

This change belongs only to App's existing commit view model: no new port, tool,
authorization, approval or persistence behavior. Tool and refresh ownership still
last until actual completion; failure, cancellation, task generations and bounded
output retain their earlier semantics. File/method limits are unchanged.

Four deterministic combinations of immediate/delayed commit and refresh initially
failed: observers received `IsOpen == false` while `IsBusy` was still true. The
production real-Git keyboard/focus case passed in that same red run (1 passed,
4 failed), so this is not claimed as a reproduced native focus-loss defect.
Success now records its close intent and publishes non-busy/closed state together
in final UI completion, after operation ownership has been released. Generation
checks prevent old completion from closing a newer task's dialog. No new command
can enter between the two earlier dispatcher updates.

The focused green run passes 91/91, including those four state combinations,
production inspector focus, Escape restoration, cancellation/cleanup, per-task
draft isolation and failure requiring renewed confirmation. It overlaps the later
related regression and must not be added to that report's total. The solution
build is again zero-warning/error; the 293-file single-type/syntax/size audit and
changed AXAML limits pass. The original red report is preserved. The follow-up's
final related run has now exited zero: 373/373, no failures or skips. Its filter
covers Git commit/index, diff, inspector, shared dialogs and caption focus. All
2,158 source/configuration and 2,439 runtime identities, source ZIP bytes and
path sets agree at terminal; `validation-terminal.json` records this check.
Compared with the earlier 3,520-case checkpoint, exactly three source files
changed: the commit view model and its two state/production workflow test files.
That relationship is recorded in `baseline-source-delta.json`; it does not make
373 tests into a new full-solution run or authorize summing overlapping passes.

#### Remaining limits

Push/PR, group/hunk staging, Git identity/configuration UI, native restart and
real signing/interactive-hook acceptance remain incomplete or unverified.
Success-path production Headless focus and Escape-close restoration now have
explicit assertions; actual native-window focus under load remains unverified.
Hooks and Git configuration are approved external effects, not path-constrained OS
sandbox operations. Snapshot validation cannot eliminate changes after its final
read. No rollback/retry is inferred from cancellation, timeout or a hook failure.
The development checkout is not staged, committed, pushed, packaged or released.
<!-- /source: GIT_COMMIT_ACCEPTANCE.md -->


---

<a id="git-diff-input-session-acceptance"></a>

## GIT_DIFF_INPUT_SESSION_ACCEPTANCE.md

<!-- source: GIT_DIFF_INPUT_SESSION_ACCEPTANCE.md -->
### Git 差异临时输入会话

#### 设计自查和实施计划

1. Tools.BuiltIn 负责文本解码及 Git diff/numstat 编排，Tools.Abstractions 增加专用
   临时只读输入会话端口，Platform.Windows 实现目录/文件句柄固定，App 仅注册。
   不改变差异算法、统一 diff、编码、统计或最终产物协议。
2. 复用现有授权目录创建及目录句柄租约；新文件以相对父句柄创建，持有读写/删除
   权限但只共享读取，原生创建时设置 delete-on-close。端口仅暴露路径，不暴露写流。
   文件和祖先目录保持固定直到 Git 进程停止和输出读取结束，清理使用原文件句柄。
3. 沿用所属工具的 schema、路径、风险/审批、任务取消/超时、输出上限和审计。
   每个输入最多 4 MiB，覆盖最大 2 MiB 文本快照解码后转 UTF-8 的扩展；不把它当作
   Git 进程内存或文件系统隔离。最终授权复核失败不发布补丁/统计，不自动重试。
4. 每个接口/会话/工厂和测试辅助类型单独文件；保持 500/80 行硬限制。
5. 先用实际 Git 验证共享协议，再覆盖创建/越界/链接/写入替换拒绝、取消/超时、
   两输入释放、失败清理、并发、UTF 编码、统一 diff 和 numstat。最后通过生产 App
   的保存模型 → 编辑 → 统计/审阅 → 撤销 → SQLite 重载链路及 Debug/Release 验证。

回归发现的审批状态发布增量由 App 的 `ApprovalViewModel` 负责：先发布请求 ID、
工具名称、队列数等决定所需信息，最后发布 `IsPending`。不改请求匹配、风险和审批
规则，不改变超时。使用确定性的属性通知观察者验证立即批准和拒绝，不增加新类型。

本机共享探针 `artifacts/git-diff-input-safety-20261021/share-probe.log` 已确认 Git
2.52.0.windows.1 可读取持续打开、仅共享读取且 delete-on-close 的文件，同时外部
写入和删除被拒绝。该探针尚未覆盖原生相对创建、目录租约或生产工具管线，不作为
整个功能的完成证据；其 CRLF 提示由生产已有显式 Git 配置关闭。

#### 生产链路和边界

- `AppComposition` 注册 `IDiffInputFileSessionFactory` →
  `WindowsDiffInputFileSessionFactory`；`FileChangeReviewTool` 的补丁生成和
  `FileChangeStatisticsTool` 的 numstat 均注入同一端口。原生工厂拒绝时不回退。
  没有注入端口的兼容宿主保留原路径分支，不具有新保证。
- 工厂只接受完全限定、叶名称匹配 task GUID 的产物根；沿用 App 配置父目录的信任
  边界。先通过已有目录创建端口取得租约，再持有独立目录链。原文件通过父句柄
  `FILE_CREATE`、no-follow 和 `FILE_DELETE_ON_CLOSE` 创建，只共享读取。
  权限为读写/删除及 NT 同步 I/O 明确需要的 SYNCHRONIZE，不申请修改 ACL 或所有权。
  同句柄验证物理范围，写入、flush、复核授权/取消后才返回只暴露路径的会话。
- 两输入保持存活直到 `GitCommandRunner` 已等待进程终止和输出读取结束；最终工作区
  复核后，using/finally 才释放会话。第二个创建、Git 启动、输出上限、取消/超时和
  授权拒绝均释放已有输入。清理按原句柄设置删除并释放，真实拒绝通过安全错误反馈；
  一个输入清理失败不会跳过另一个输入。
- 保留原有解码规则：UTF-8/BOM、UTF-16/32 两种字节序、CRLF 和缺少末尾换行。
  Git 参数继续关闭 ext-diff/textconv/color/autocrlf；统一 diff 仍重写项目路径标题，
  numstat 仍验证三列非负整数，不把失败报告为零行变更。
- 标准管线及策略没有改变：这两项属于既有明确只读 Git 辅助查询，ReadOnly 允许，
  AskEveryTime 不为只读查询额外弹窗，显式审批服务拒绝则不创建输入。
  这不授权任意 shell，也不允许修改项目内容；内部缓存只存在任务产物根。

#### 分层验证

证据目录 `artifacts/git-diff-input-safety-20261021/` 保留原始失败及后续成功记录。

| 层级 | 实际覆盖 | 限制 |
| --- | --- | --- |
| Windows 37 项专项 | 原生创建/读取/清理、外部写入/删除/重命名/父目录替换拒绝、4 MiB 边界、错误 task、junction、预取消、第二输入拒绝、工作区拒绝、Git 启动失败、输出限制、清理拒绝、并发、运行中取消/超时、五种 UTF 编码和 2 MiB 级 UTF-16 转换 | 普通路径用真实 Git；运行中取消/超时用进程端口 gate，验证先停止再释放，不冒充真实 Git 慢操作 |
| App 3 项专项 | 保存提供商/模型 → loopback Responses → Agent read/write 或 replace → 审批 → 原生快照 → 实际 Git 统计/审阅 → 预览 → 撤销 → 服务重建；失败展示、手动统计重试/预览刷新、保留草稿和根外哨兵 | 模型是模拟服务；UI 为 Avalonia Headless，非原生窗口 |
| 原生产物/进程 + 生产 App 组合 CLI | 正常读取/编辑、实际 Git diff/numstat、审批确认撤销、服务重建后 5 条完整审计及补丁内容/hash、无临时输入残留 | 无模型、无 GUI、未终止进程；只针对正常流程 |

CLI 原程序为 `GitDiffProbe.csproj`、`GitDiffProbe.cs`、`GitDiffProgram.cs`，
初始成功日志 `production-cli.log`，最终源码的复验日志为 `production-cli-final.log`。
加载的 8 个生产程序集逐个与指定 Debug App 输出
核对 SHA256，2 次修改审批真实完成。它不是 Release 包或原生交互验收。
最终复验使用 `explicit-probe-bin/` 和独立 obj 输出；探针禁止默认候选程序集扫描，
显式从生产 HintPath 解析。此前两次复验被 hash 守卫正确拒绝，日志为
`production-cli-stale-module.log`、`production-cli-stale-candidate.log`：MSBuild 的
`CandidateAssemblyFiles` 优先选中了同证据目录内旧探针复制的 App 程序集，即使换
独立 obj 仍会发生。修正探针的项目默认项/搜索顺序后，8 个模块身份全部匹配。
这些失败属于验收探针装载不一致，不作为产品通过证据，未修改生产装载逻辑。

失败记录说明：首批测试误将明确只读 Git 查询当作 ReadOnly 禁止操作，四项失败
保留在 `first-results/`；核对既有策略后改为验证只读允许和显式拒绝，未改策略。
`encoding-results/` 的大内容替换先被既有请求 schema 正确拒绝，随后改为读取已有
2 MiB 级 UTF-16 原文并作小内容编辑，验证同一输入扩展边界，没有放宽请求限额。
权限收窄初稿缺少 NT 同步 I/O 的显式 SYNCHRONIZE，导致原生创建和 App 统计失败，
见 `minimal-access-results/`、`app-first-results/`、`app-diagnostic-results/`。
补齐该位后 `synchronize-results/` 为 37/37，`app-complete-results/` 为 3/3。

首轮扩大回归 `accepted-*` 中 Debug 2,434/2,434，Release 2,433/2,434；Release
目录移动成功用例在原 20 秒期限内未结束。未改源码的独立移动 6 项、同范围 App
427 项复验都通过，分别见 `move-diagnostic-first-results/`、`rechecked-Release-App-results/`。
这不自动撤销原失败。继续核对发现 `ApprovalViewModel` 在请求 ID、工具名称和队列数
更新前先发布 `IsPending=true`；观察者可拿到 null ID，决定被匹配守卫正确忽略。
`approval-publication-before.log` 的真实程序集 CLI 探针确定性记录 null/空名称/零队列/
不可批准，且即时决定未应用；`approval-publication-before-results/` 两项 Headless 回归
也均在该状态失败。发布顺序修正后，同两项及原移动六项通过
（`approval-publication-fixed-results/`，8/8），原超时和审批策略保持不变。
初始超时没有保存当时请求 ID，不能断言此竞态是该次超时的唯一原因。
修复后的生产程序集探针另见 `approval-publication-final.log`，同一待处理通知时
请求 ID、工具名称、队列数和可批准状态均已就绪，立即决定真实完成；该探针没有原生窗口。

最终扩大回归使用 `verified-*` 单独保存，不覆盖 `accepted-*` 的失败证据；范围扩大到
所有名称包含 Approval 的 App 用例，以覆盖旧手势、错误 ID、取消、排队及管线拒绝。

##### 最终构建与回归（2026-10-21）

| 范围 | Debug | Release |
| --- | --- | --- |
| 完整解决方案构建 | 成功，0 警告/0 错误 | 成功，0 警告/0 错误 |
| Tools 完整套件 | 691/691 | 691/691 |
| Platform.Windows 完整套件 | 408/408 | 408/408 |
| Infrastructure 完整套件 | 795/795 | 795/795 |
| Browser 完整套件 | 113/113 | 113/113 |
| App 精选回归 | 552/552 | 552/552 |
| 各配置相关测试合计 | 2,559/2,559 | 2,559/2,559 |

`review-verification.ps1` 逐份复核 10 份最终 TRX，均 Completed、无失败/跳过/中止；
生成 `accepted-verification.json` 和 `accepted-verification-review.log`。
两次构建/测试前后及当前共 2,611 项源码/构建输入的路径与 SHA256 完全一致。
最终生产 CLI 的 8 个模块与 Debug App 输出逐项匹配；审批发布 CLI 的 App 模块也匹配。
结构检查覆盖 2,418 个手写 C# 文件、21,556 个方法，最长文件 496 行、方法 78 行，
单文件单类型及规模限制无违反。`git diff --check` 通过，6 份关联文档的本地链接有效。

App 范围包括文件编辑/预览/复制/移动/删除、快照统计/审阅/撤销、浏览器产物、审批及
恢复相关入口；它是精选回归，不能称为整个解决方案测试全套。专项 37 项、3 项及
2 项审批发布用例包含在对应扩大范围中，不能重复相加为独立产品覆盖率。
此处没有生成正式发布包、运行原生 GUI、调用真实模型或执行宿主崩溃/MXC 验收。

#### 未完成范围和详细方案

1. **Git 进程文件系统隔离仍阻断。** 当前仅固定本次输入文件及目录，不阻止 Git
   访问进程令牌允许的其他文件。按 R01 在受支持独立 Windows 实现原生 MXC，覆盖
   Git 本体、hooks/helpers、SDK、PTY、网络/私有 temp，以及绝对路径、链接、子进程和
   继承句柄拒绝。当前机器不能用 DACL fallback 宣称完成，独立环境尚未就绪。
2. **持久根身份与授权代际。** 目前仍信任 App 的产物父目录/任务 ID，使用操作期间
   的工作区复核。后续以向前迁移绑定根对象身份和授权版本，撤权时取消所属运行，
   对审批、原生创建、Git 启动和结果发布间的每个竞争点设置确定性 gate。
3. **崩溃与未知清理。** 原生 delete-on-close 帮助进程退出时处理临时文件，但尚未
   验证输入写入中、Git 读取中、结果已生成/审计未提交时的真实宿主崩溃。
   后续使用独立宿主在各阶段终止，核实 Job/输入句柄、原快照和其他任务，恢复只显示
   中断/未知，不重跑命令。针对清理未确认、进程仍持有读取和异常终止建立持久状态，
   不能根据临时名称自动删除；最终 Retain/SQLite 的原子发布继续单独处理。
4. **外部别名及环境组合。** 该共享协议固定当前文件对象，不是所有硬链接、内核/
   管理员访问或已经存在映射的全系统隔离。继续验证受支持 Git 版本/共享参数、长路径、
   不支持卷、大小写目录和用户配置组合，拒绝路径不能回退到无保护读取。
5. **实机与完整产品。** 当前仅 CLI/确定性服务/Headless；原生窗口和真实模型未验证。
   浏览器缓存配额及清理未知状态、产物持久发布/引用回收、其他差距清单继续有效。

本增量不涉及数据库迁移、系统配置修改或提交/推送/发布，整体目标保持进行中。
<!-- /source: GIT_DIFF_INPUT_SESSION_ACCEPTANCE.md -->


---

<a id="git-process-ownership-acceptance"></a>

## GIT_PROCESS_OWNERSHIP_ACCEPTANCE.md

<!-- source: GIT_PROCESS_OWNERSHIP_ACCEPTANCE.md -->
### Git 进程所有权验收

日期：2026-10-10。范围是 Git 调用的进程管理优化，不是完整产品或界面验收。

#### 问题和职责

- 原 Git 工具直接启动进程，正常命令退出后，钩子启动的后台进程仍可能存活。
- 原 Infrastructure worktree 服务在取消等待时没有回收 Git 和钩子进程。
- 原分支查询没有消费 stderr，诊断输出可能填满管道，阻塞 Git 退出。
- `GitCommandRunner` 保留工具结果及 diff 退出码规则，必须注入已有的
  `ITaskProcessLauncher`；不提供无所有权的生产回退。
- `GitProcessRunner` 属于 Infrastructure，服务后台分支查询和 managed worktree。
  它复用 `Tools.Abstractions.Processes` 的端口，不访问 Windows 类型。
- App 只负责注册，三条路径都使用 `WindowsTaskProcessLauncher`。
  Windows 的原子 Job 绑定继续由 Platform.Windows 实现，没有另造启动器。

#### 边界与不变量

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

#### 有效基线

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

#### 定向验证

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

#### 完整回归

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

#### 结构检查

- 解决方案构建：零警告、零错误。
- 对本轮 19 个 C# 文件执行 scoped whitespace 检查，无格式错误。
- Roslyn 检查：每文件一个类型、文件最多 500 行、方法最多 80 行，全部通过。
- 本轮没有提交、推送或发布；保留已有未提交工作。

#### 未覆盖范围

- App 被外部强制终止、提权代理或系统服务创建的进程，以及资源耗尽压力。
- Git 钩子的任意系统副作用或超时后 worktree 的自动修复。Job 不是安全沙箱。
- 所有 Git 版本、网络认证助手、PTY、浏览器及 MCP 的全场景生命周期。
  它们的历史验收范围仍各自独立。
- 全页面像素一致性、各 DPI、真实远端提供商及子代理完整交互。

本记录只验证这次 Git 进程管理优化，不表示整个 MossAgent 目标已完成。
<!-- /source: GIT_PROCESS_OWNERSHIP_ACCEPTANCE.md -->


---

<a id="git-push-acceptance"></a>

## GIT_PUSH_ACCEPTANCE.md

<!-- source: GIT_PUSH_ACCEPTANCE.md -->
### Confirmed single-branch Git push

#### Plan and required design self-check

1. Tools.BuiltIn owns separate local-only preview and side-effecting push tools,
   query/state and endpoint validation. Tools.Abstractions owns immutable preview
   data and a local-destination authorization port; Infrastructure implements
   bare/non-bare destination inspection with existing Git path checks. App owns
   confirmation and task selection, never filesystem/process access.
2. Reuse the owned Git runner and standard schema/capability/task-guard/approval/
   deadline/cancellation/output/audit pipeline. No shell or direct tool bypass.
3. Preview binds source repository/HEAD/branch, complete bounded Git configuration,
   configured remote, effective single push URL, target branch and local target
   identity. Reauthorize and compare after approval. Push the captured commit ID
   to exactly one heads ref; explicitly disable force/mirror/prune/follow-tags,
   recursive submodule pushes and implicit push options. Keep pre-push hooks and
   signing/credential configuration. Never auto-commit, retry, force, push tags,
   update upstream configuration or use a user remote in tests. Multiple push
   URLs/custom remote helpers are explicit unsupported errors, not hidden success.
4. Permit standard HTTP(S), SSH/SCP, git and explicitly authorized local file
   transports. Reject other helpers/protocols and path aliases/reparse points.
   Raw configuration, credential-bearing URLs and Git push stdout/stderr stay out
   of tool output/audit/UI; display only a sanitized transport host/local label.
   This is not an OS sandbox for configured hooks/credential helpers or a native
   handle/network-race guarantee. Cancellation may follow a successful remote
   mutation; inspect before any explicit retry.
5. Keep one type per C# file and files/methods within limits. Verify real pushes
   against temporary bare repositories: exact captured commit/target, dirty and
   unselected data excluded, non-fast-forward rejection, denied approval/ReadOnly,
   approval-time state/URL/path changes, secret suppression and owned cancellation.
   Verify production inspector, task/input isolation, themes/keyboard, focused
   regression and a solution build. No user window, credentials or paid API.

#### Status

The tools, destination port and inspector confirmation are implemented. Current
focused terminal reports pass 48 Tools (`git-push-4.trx`) and 35 App cases
(`git-push-app-4.trx`) without failures/skips. These overlap the upcoming related
regression and must not be added to its total. The complete solution build
passes with zero warnings/errors. Session 40586 is terminal with exit 0 and all
six Completed reports: 613 Tools, 442 Infrastructure, 140 Agent, 1,966 related App,
300 Visual and 146 Windows cases, totaling 3,607/3,607 without failures/skips.
This is related regression, not all solution tests or native/live-remote acceptance.
The 320-file C# type/size and 16-file AXAML parse/size audits pass. Final light/dark
rendered dialog artifacts were inspected separately from native-window behavior.

The terminal gate confirms all 2,187 source/configuration and 2,439 runtime file
hashes/path sets agree with their pre-test identities and source ZIP contents.
Snapshot SHA-256: `407C0C9EFEC56E52BC5F3DD3BCCAB260332BD0534A8F8E03F83A4986173E6B9B`.
The candidate is frozen and must not be rebuilt. Subsequent source changes need
a separate candidate and are not covered by this result. Original red reports
remain in the candidate; no development-worktree commit, push or release occurs.

Push is a coding-workflow slice, not PR, remote configuration/credential UI,
native/restart or whole-product completion. Tracking configuration remains a
separate unimplemented workflow; ordinary successful push may update Git's
remote-tracking refs. Local destinations containing alternate object stores or
links anywhere in bounded objects/refs/logs trees are explicitly rejected.
Standard network parsing is deterministic, not a live remote-authentication test.

#### Evidence and fixes

- A real temporary repository exposed `remote.origin.mirror=true` overriding
  `--no-mirror` and failing with `--mirror can't be combined with refspecs`.
  Process-local `-c remote.<selected>.mirror=false` corrects this without changing
  configuration; broad tag/submodule defaults and extra refs remain excluded.
- The first production AppComposition test failed to resolve the concrete
  repository authorization dependency. Destination authorization now consumes
  the already-registered `IGitRepositoryAuthorization` port. The same real
  inspector test then passes through SQLite, shared controls, keyboard preview
  refresh/confirmation, Git push and audit. It verifies exact target commit and
  committed bytes, excludes dirty/staged bytes, retains the chat draft, refreshes
  actual diff status and returns focus. This is Headless, not native-window QA.
- Endpoint tests first fail three cases: undefined `:-1` ports on SSH/git labels
  and query-like SCP host text leaking into the label. Standard server-only URI
  formatting and SCP host validation fix these while suppressing userinfo,
  query/path values and raw failure output. Original red reports are retained.
- Source/destination changes during approval, pre-push hook refusal, object-tree
  junctions before/after preview, detached HEAD/file URI, tracking defaults,
  ReadOnly/denial/revoked guard, non-fast-forward history, cancellation/deadline,
  cleanup/audit and oversized output/configuration have focused coverage.
- App tests cover selection edits, delayed previews, task isolation/root snapshots,
  actual tool/refresh cleanup, secret-free errors and atomic busy/close ordering.
  Shared dialog keyboard/focus/bounds checks cover light/dark at 280/640 DIP widths
  and 560/700 DIP heights, with ready/busy/interrupted/failed rendered artifacts.
  Representative renders were inspected; no user window or remote was operated.
<!-- /source: GIT_PUSH_ACCEPTANCE.md -->


---

<a id="git-staging-acceptance"></a>

## GIT_STAGING_ACCEPTANCE.md

<!-- source: GIT_STAGING_ACCEPTANCE.md -->
### Git file staging workflow

#### Plan and required self-check

1. Tools.BuiltIn owns separate stage/unstage tools; Infrastructure implements a
   portable repository-authorization port using the existing bounded Git identity
   inspector. App coordinates commands without accessing files or processes.
2. Reuse the standard tool pipeline, owned process launcher, worktree guard,
   shared controls, status parser and diff pane. Add no shell-command bypass.
3. Require write/process capability and normal approval. Authorize the complete
   repository and metadata, capture native Git directory arguments, validate
   literal file paths and rename sources, deny directory/metadata/link/path
   escapes, bound operations and await cancellation cleanup. Never stage all,
   commit, push or delete working files implicitly.
4. Keep ports, scopes, tools, helpers, view models and test fixtures in separate
   one-type files. Do not grow the 499-line WorkspaceViewModel or App.axaml.
5. Prove real temporary-repository stage/unstage including unborn HEAD, deletion,
   rename and literal names; test schema, policy, denial, external metadata,
   links, cancellation/timeout, output/audit and selection lifetime. Verify the
   shared-control UI in light/dark Headless rendering and build the solution.

#### Implemented workflow

- The diff inspector offers stage or unstage for the selected file in its current
  working-tree/index filter. It uses the production tool executor, not a direct
  UI process call, and refreshes real status after success. ReadOnly and
  projectless/disabled-tool contexts cannot invoke the action.
- `git.stage_file` uses `git add -- <literal files>`; `git.unstage_file` uses
  `git reset --quiet -- <literal files>`, including before the first commit.
  Neither requests a working-file reset or a commit. A rename includes its source
  only on the selected rename side; a copy never unstages independent source edits.
- Infrastructure authorizes the complete discovered repository and its metadata
  under a cooperative thirty-second ceiling. Nested directory access alone does
  not grant the parent repository. External standalone metadata requires explicit
  additional authorization; linked worktrees reuse the existing identity checks.
- Commands pin the captured Git/worktree directories and disable pathspec magic.
  Explicit files and optional rename sources are validated before approval and
  again before native execution. Directories, `.git` paths, path escapes, link
  ancestors, ADS, trailing aliases and DOS device names are rejected.
- Both tools declare write/process capability and external-side-effect risk.
  Git filters or hooks may execute programs; this is approval classification,
  not an operating-system sandbox. The standard task-workspace guard runs on both
  approval boundaries; timeout, cancellation, output bounds and audit remain in
  the shared pipeline. Authorization timeouts receive the tool-timeout result.
- The action view model snapshots roots, prevents duplicate execution, exposes
  cancellation, ignores obsolete-task results and awaits actual tool/refresh
  cleanup on asynchronous disposal. Interrupted operations explicitly warn that
  the index may already have changed and require checking status before retry.
  Failures show localized guidance rather than raw process/exception output.

#### Deterministic evidence

Tests use temporary repositories, production App composition, real SQLite/audit,
owned Git processes, controlled process/authorization stubs and Avalonia Headless.
They do not operate user windows, use real credentials or invoke paid APIs.

- Real index checks cover committed/unborn repositories, Chinese and bracketed
  literal names, rename, deletion, source-copy isolation and external file removal.
  Unselected files, working bytes and HEAD remain unchanged by the core operations.
- Negative checks cover denied approval, ReadOnly, malformed schemas, unsafe
  destination/rename source, independently stored metadata and repository scope.
  Real junctions are denied even when both destinations are authorized and when
  the link appears during approval. Fixtures unlink junctions before cleanup.
- Controlled cancellation/deadline cases wait for actual authorization/process
  cleanup before returning or completing audit; no index command starts after
  interrupted authorization or a revoked task-workspace guard. Both streams stay
  bounded and captured command arguments preserve literal repository/file identity.
- UI checks cover localized failures, root snapshots, duplicate prevention,
  task switching, non-cooperative operation/refresh disposal and source selection.
  Production keyboard actions drive real stage/unstage and verify filter refresh,
  index contents and successful persisted audits; failed Git writes persist failure.
- Light/dark Headless frames at 280 and 640 DIP verify keyboard activation,
  busy/disabled controls, cancellation and wrapped interrupted-state feedback.
  Eight images are component renders, not native desktop screenshots.

#### Validation status

The first focused tools run retained four test-fixture failures caused by a
duplicated temporary-directory separator; the corrected run passed 38/38.
Expanded UI rendering initially checked a bound button before the asynchronous
relay command delivered its completion notification. The bounded eventual-state
check passed 22/22, including production Git and four visual cases. Original
reports remain in `artifacts/git-staging-20261018/results`.

The final expanded checkpoint in `artifacts/git-staging-20261018` has terminated
with all seven recorded commands exiting zero and six Completed test reports:

| Validation | Result |
| --- | --- |
| Complete Tools assembly | 549/549 |
| Complete Infrastructure assembly | 442/442 |
| Complete Agent assembly | 140/140 |
| Related App workflows, excluding names containing Native | 1,124/1,124 |
| Complete shared Visual assembly | 300/300 |
| Real Windows Git process-ownership cases | 8/8 |
| Total above, without overlapping focused runs | 2,563/2,563 |
| Complete solution build | Zero warnings/errors |
| Changed handwritten C# one-type/size/syntax audit | 261 files, no violations |
| Maximum checked C#/AXAML file and method length | 499 / 72 lines |

There are no failed or skipped cases in the six final reports. A separate
current-binary focused App run passes 24/24, including real copy-source
preservation and externally removed-file failure; these cases are already in
the related-App total and are not added again. Original failing/intermediate
reports remain separately available. This is not a complete-solution test run.

`validation-terminal.json` retains actual command exits, arguments, timestamps
and counters. All 2,135 source/configuration entries and 2,439 binary/runtime
entries retain their SHA-256 identities. The source ZIP's entry names, lengths
and bytes match the source manifest, and `identity-path-set-check.json` checks
that neither source nor runtime paths were added or removed. Documentation is
outside the tested code/configuration manifest. The earlier restored-worktree
candidate's 7,355-case full pass uses pre-staging binaries and is not counted as
complete regression of this candidate.

#### Remaining limits

File-level staging is not the still-missing commit/push/PR, stage-all or hunk
workflow. Git index locks are surfaced as failures rather than silently removed.
Cancellation does not roll back an index mutation that already happened, nor
automatically replay it. Checks do not pin native filesystem identities and do
not prove handle-level TOCTOU protection or isolation of repository hooks.
Native-window/restart acceptance and the overall product remain incomplete.
No changes are staged in this checkout, committed, pushed, packaged or released.
<!-- /source: GIT_STAGING_ACCEPTANCE.md -->


---

<a id="pull-request-workflow-plan"></a>

## PULL_REQUEST_WORKFLOW_PLAN.md

<!-- source: PULL_REQUEST_WORKFLOW_PLAN.md -->
### Pull request workflow: scope and implementation plan

#### Reference and current state

Official reference inspected on 2026-10-18:
[Code review](https://learn.chatgpt.com/docs/code-review). It describes source
account connection, personal review inboxes, opening a PR URL, summary/activity,
changed files with inline comments, checks, pinned reviews, revision-specific
viewed markers, related stacks, review chat and explicit feedback submission.
Commenting in a PR and submitting a review are remote writes; chatting about a
PR does not post a comment, approve or merge it. Failing-check context prepares a
draft instead of silently sending a message. Local Git review is a separate flow.

Current `src`/`tests` inventory includes independently persisted GitHub token
connections and bounded identity reauthentication through shared settings. See
[SOURCE_ACCOUNT_CONNECTION_ACCEPTANCE.md](GIT_WORKTREES.md#source-account-connection-acceptance); this is not repository authorization
or a complete PR workflow. Schema 19 now adds explicit repository read grants,
source-only task scope, standard PR read tools and a configured-repository page
with open-PR pagination, description and commit IDs. See
[SOURCE_PR_READING_ACCEPTANCE.md](GIT_WORKTREES.md#source-pr-reading-acceptance) for that frozen slice. The subsequent
[SOURCE_PR_INSPECTION_ACCEPTANCE.md](GIT_WORKTREES.md#source-pr-inspection-acceptance) adds version-checked file patches, discussion
and inline-comment pages, check runs and legacy statuses. The current
[SOURCE_PR_INLINE_COMMENTS_ACCEPTANCE.md](GIT_WORKTREES.md#source-pr-inline-comments-acceptance) adds verified current-head inline cards,
exact-side line/range validation and keyboard location on the two loaded pages;
historical or unsupported locations remain explicit rather than being guessed.
Schema 20 adds a separate ordinary projectless chat per grant/PR/base/head over
the first bounded, explicitly loaded snapshot, plus draft-only failed-check
investigation through the existing composer cache. Opening does not call a model;
manual sends reuse the production model/history/cancellation chain with all tools
disabled. Agent reserves the full untrusted user snapshot separately from
evictable history. Capacity refusal is persisted by schema 21 and displayed in
the shared task-failure card with repair steps; it never silently drops the
snapshot or retries. Revocation blocks new turns and later observed output, not local history.
See [SOURCE_PR_REVIEW_CHAT_ACCEPTANCE.md](GIT_WORKTREES.md#source-pr-review-chat-acceptance) for the preceding snapshot validation.
Schema 22 adds a separate explicitly opened ReadOnlySource conversation per
grant/PR/base/head/mode. Only get/files/comments/checks may query its bound PR and
commits through the existing pipeline; lists, local roots, browser, MCP, subagents
and writes stay unavailable. Source and tool results are untrusted observations,
not instructions, and opening never sends or fetches more pages. Mode/draft/history
isolation, post-HTTP access/revision checks and controlled model-tool-source-model
evidence are recorded in [SOURCE_PR_ON_DEMAND_READING_ACCEPTANCE.md](GIT_WORKTREES.md#source-pr-on-demand-reading-acceptance). The other
workflows below and native/live acceptance remain open.
The subsequent explicit URL form resolves a chosen account and existing grant,
reads only normalized PR detail through the common pipeline, and reuses inspection
and review chats without a silent list query, model send or authorization. Common
web page suffixes identify the current PR, not a historical snapshot. Candidate
validation, strict origin/port mapping and limits are in [SOURCE_PR_URL_ACCEPTANCE.md](GIT_WORKTREES.md#source-pr-url-acceptance).
[TOOL_SCOPE_ACCEPTANCE.md](TERMINAL_TOOLS.md#tool-scope-acceptance) adds
immutable tool allowlists to the executor and main/child Agent chains, not PR
account access. Existing Git push is not PR support. This document preserves
the complete implementation/acceptance plan, not a claim of functional parity. GitLab,
cloud reviews, OAuth/connector compatibility and plugin-ecosystem compatibility
remain explicit gaps until their own running chains exist; they are not silently
removed from the product objective.

#### Required design self-check

1. Domain owns source-account identities and PR/revision values; Application owns
   persistence/client ports and workflow coordination. Infrastructure implements
   SQLite and GitHub HTTP. Tools.BuiltIn implements separate shared-contract read
   and mutation tools; App owns view models/shared controls and calls application
   operations, never HTTP, SQL, filesystem or a GitHub CLI directly. Agent consumes
   ports and bounded review context without referring to concrete clients.
2. Reuse the standard schema/capability/approval/deadline/cancellation/output/
   artifact/audit pipeline and proxy configuration. The AI-provider HTTP factory
   takes an `AiProvider`, so do not disguise a source account as an AI provider to
   reuse it. Add a source-client factory/port sharing proxy semantics instead.
3. Source-account access is an explicit grant to a server/repository, not a grant
   to a filesystem directory or an arbitrary URL. Add and enforce a task-owned
   allowed-tool/source scope before review-chat tools are enabled. Plain projectless
   chats keep `ToolsEnabled=false`; do not invent a project, reuse another task's
   context or enable all tools merely to implement a global PR page. Read operations
   need the configured account/scope; writes require ordinary policy approval and
   current identity/revision revalidation. Each operation owns cancellation and a
   bounded deadline, and waits for actual HTTP/body cleanup.
4. Keep one handwritten type per C# file, files below 500 lines and methods below
   80 lines. Split identity validation, client transport, pagination, parsing,
   per-action tools, connection editing, inbox, details, diff/comments and review
   submission. Reuse shared controls/styles and existing preview/diff components.
5. Deterministic HTTP/SQLite/pipeline/Headless integration must prove settings to
   actual requests and visible results. Include denial, revoked scope/account,
   stale revision, cancellation, concurrency, oversized/malformed responses,
   redirects/credential suppression and restart/no-replay negatives. Separate
   simulated transport, loopback HTTP, real-source-account and native-window proof.

#### Implementation phases

##### 1. Connections, source authorization and runtime boundary

- Persist separately named source accounts with provider/server identity, an
  explicit credential reference/value and proxy selection. Use a forward migration
  after current schema version 17; preserve all existing user data. Maintain the
  project's explicit plaintext-SQLite secret-storage decision without exposing
  credentials in records' `ToString`, logs, diagnostics, lists, exports or tool
  arguments/results. Connection failure cannot silently switch account or proxy.
- Offer an explicit GitHub token connection and validate its identity/permissions
  with a real bounded request; a status refresh must actually reauthenticate.
  Token support is not OAuth/ChatGPT-connector parity. Do not read the user's Git
  credential manager or existing CLI/browser account implicitly. Implement OAuth
  separately where application registration/provider requirements are available.
- Bind credentials to the configured HTTPS API authority and path prefix. Disable
  automatic redirects/cookies and never follow an API-provided pagination/content
  URL using an authenticated client. Resolve only validated relative endpoints;
  generate bounded pagination locally. GitHub.com has known API/web authorities;
  enterprise servers require explicit configuration and validation. Never disable
  production TLS checks. Tests inject a controlled handler or local test transport.
- Retain direct/default/explicit proxy choices and unavailable-proxy failures;
  prevent deletion of a proxy still referenced by a source account. Never silently
  downgrade to direct access when a selected proxy disappears.
- Define an account/repository/revision review scope distinct from plain chat.
  Every tool call, including an unsolicited model call, must enforce its exact
  allowlist. A review-only task must reject file reads/writes, shell, screenshots,
  unrelated MCP/browser actions and another source account, even under FullAccess.
  Account deletion, permission changes and task switching invalidate pending work.

##### 2. Inbox, PR details, diff, feedback, checks and chat

- Fetch authenticated personal inboxes (requested from me/team, authored by me),
  configured-repository lists and a validated explicitly entered PR URL. Preserve
  account/repository/filter/page selection; expose pagination, empty, loading,
  rate-limit, authentication, permission and retry states instead of fake rows.
- Show description, author/state/draft, base/head revisions, activity, issue and
  inline review comments, reviews and checks. Parse fixed bounded data shapes;
  treat all source text as untrusted data. Remote Markdown links/images/scripts
  cannot authorize a request, tool, filesystem access or navigation automatically.
- Display changed files/patches, additions/deletions and comments bound to valid
  diff lines and the displayed revision. Report missing/truncated/binary/oversized
  patches explicitly; do not present an incomplete patch as a complete review.
  Persist pin/viewed state scoped to account/repository/PR/head commit; changing
  revision clears or marks old viewed state. Resolve related stacks only from
  bounded source evidence, not inferred dependency claims.
- Create an associated review chat with the selected configured model, explicit
  instructions and bounded PR context. No automatic checkout, clone, fetch, commit
  or push occurs when opening a PR. Attaching local coding work requires a real
  project and the existing authorization/worktree flows. Review context never
  grants local roots or shares unrelated task data.
- Preparing a failing-check investigation puts a clearly identified summary in
  the selected chat's draft without sending or overwriting an existing draft.
  Do not follow third-party check/log URLs or include raw CI logs by default;
  those may contain credentials and require their own explicit access boundary.
  Manual chat review does not impersonate Codex cloud automatic reviews.

##### 3. Creation, comments, review decisions and follow-up

- A create-PR dialog previews connection/repository, base/head branch, title/body
  and draft status before confirmation. Do not stage, commit, push or configure
  tracking automatically. Confirm branches exist on the source service; report
  missing publication with a separate explicit Git push workflow. Detect existing
  PRs and show the server's result rather than blindly retrying a write.
- Issue comments, inline comments and a submitted review are separate operations.
  Review submission explicitly chooses Comment, Approve or Request changes and
  previews the decision/body/line comments and head revision. Draft findings and
  chat messages never post automatically. A merged/closed/unavailable PR or a
  changed head requires refreshed inspection and confirmation.
- Bind review comments to the displayed commit and valid file/line/side context.
  Revalidate after approval, and pass a commit ID where the provider API supports
  it. Do not claim a check-then-write operation eliminates a server-side race;
  creation by a moving head ref and endpoints without conditional writes retain
  explicit limits. Verify returned identities/revisions and expose conflicts.
- Cancellation/timeout/network failure may follow a successful remote write.
  Clear confirmation state, preserve drafts and offer inspection before an
  explicit retry; do not automatically resubmit. Restart restores interrupted
  state/history, never replays incomplete comments, reviews or PR creation.
- Refresh actual inbox/details/checks and display the returned remote result.
  Keep local conversation input, another account/task's selection and concurrent
  runs intact. Merge/delete/reopen are separately authorized source operations,
  not accidental side effects of approval or navigation.

#### Acceptance ledger to implement

| Requirement | Required evidence | Current status |
| --- | --- | --- |
| Connection/proxy persistence and reauthentication | Version-17 forward-migration fixture, restart, actual identity requests and proxy failures | Token connection settings, schema 18, revision checks and bounded identity requests implemented; SQLite reopen/controlled HTTP/Headless validation in SOURCE_ACCOUNT_CONNECTION_ACCEPTANCE.md; schema 19 read grants are separate from server permission verification; native/live account and OAuth remain open |
| Source-only scope enforcement | Direct/unsolicited tool negatives for filesystem/process/screen/MCP/other accounts; denial and revocation before/after approval | Exact account/repository/revision read grants and pre/post-approval/after-response validation implemented. Snapshot chats still disable all tools. A separate opt-in ReadOnlySource chat exposes only get/files/comments/checks for its exact PR/base/head, never local roots, lists, browser, MCP or subagents. Access changes reject later output/provider requests; source writes remain open. Current acceptance: SOURCE_PR_ON_DEMAND_READING_ACCEPTANCE.md |
| Inbox/URL/details/changes/comments/checks | Production composition through bounded HTTP responses to shared controls, pagination and error paths | Configured-repository lists/details, version-checked patches, comments, checks/statuses and validated current inline diff cards/location implemented; same-side ranges target the end line, only loaded pages participate. Explicit account-bound PR URL opening now reuses the detail/inspection/chat chain; validation and mapping limits: SOURCE_PR_URL_ACCEPTANCE.md. Inbox/filter and reviews/activity remain open |
| Review chat and draft-only check context | Selected model sees only pinned PR data; no source write/local grant; existing draft survives | Immutable first-loaded snapshot chat and empty-draft-only investigation remain unchanged. Schema 22 adds a separate read-only query chat with model-tool-source-model roundtrips, visible/persisted results, pinned context and manual cancellation recovery. Opening never sends/queries; same-mode reopening preserves its first snapshot. Evidence and native/live limits: SOURCE_PR_ON_DEMAND_READING_ACCEPTANCE.md; full PR parity remains open |
| Pin/viewed/related-stack revision behavior | Persistent reopening, changed-head and incomplete-response tests | Not implemented |
| Confirmed PR creation and feedback | Pipeline approval to exact captured HTTP mutation, response/audit/UI refresh; no implicit Git action or retry | Not implemented |
| Concurrency/cancel/restart and safety | Delayed transport/cleanup, stale result rejection, secret-free diagnostics and no replay | Token settings/identity slice has focused coverage; complete PR operation lifecycle not implemented |
| Keyboard/themes/native and live source | Focus/bounds/shared-control checks plus separate explicitly authorized native/account runs | Shared connection settings have Headless keyboard/theme coverage; native/live source not verified |

The existing Git push regression is frozen and terminal in `artifacts/git-push-20261018`.
New PR source changes require a separate
candidate; do not relabel earlier Git passes as PR or current full-product proof.
<!-- /source: PULL_REQUEST_WORKFLOW_PLAN.md -->


---

<a id="repository-status-lifetime-acceptance"></a>

## REPOSITORY_STATUS_LIFETIME_ACCEPTANCE.md

<!-- source: REPOSITORY_STATUS_LIFETIME_ACCEPTANCE.md -->
### Repository status read ownership

#### Scope and verified defect

The original `WorkspaceRepositoryStatusViewModel` starts metadata reads with
`CancellationToken.None` and has no disposal contract. Workspace selection can
launch an unawaited refresh, and neither workspace shutdown nor service-provider
disposal joins that operation. A controlled noncooperative Git port proves that
disposal can finish while its reads remain active; late reads can still publish.

The fix remains in App and reuses `IGitRepositoryInfoService`, the Infrastructure
Git runner and `SettingsOperationLifetime`. It does not run processes or access
files in UI, add a service locator/cache, or change tool authorization/approval.
Each read has the existing 30-second operation deadline. Cancellation does not
abandon ownership: closing waits for the port to return, including cleanup of
native children already owned by Infrastructure.

#### Behavior

- Every read is reserved before invoking the port and released only after its
  cancellation source and completion are settled. Shutdown/disposal rejects new
  work, cancels all pending reads and shares one stable completion across retries.
- Generation and shutdown checks reject stale results and a reentrant selection
  or close before dispatch. New selections show their own directory and unknown
  Git status while pending, instead of retaining another project's branch.
- Service failures remain unknown and can recover on a later refresh; raw
  exception text is never displayed. Successful null branches still mean non-Git,
  and a selected task's worktree still takes precedence over the project root.
- Workspace synchronous disposal, asynchronous disposal and normal shutdown all
  connect to the owner. Shutdown starts metadata cancellation alongside existing
  cleanup and joins it even when another cleanup fails. The four existing labels
  move to localized workspace resources without changing their Chinese values.
- Git runner, branch lookup and owned-read waits do not capture the caller's UI
  synchronization context. Final status publication explicitly dispatches to the
  UI with the operation token; close can cancel a queued publication without
  needing a stopped message pump to resume a library continuation.

#### Deterministic verification

- The first ten regression cases on the original implementation pass 2/10 and
  fail 8/10: uncancelled tokens, premature disposal, missing ownership and unsafe
  error propagation are retained in `lifetime-red.trx`.
- Final coverage adds callback-failure ownership, three publication/reentrancy
  cases and two real Workspace composition cases: 16 new regressions. Together
  with operation-lifetime, conversation, shutdown and preference-lifetime tests,
  the ownership-focused group passes 36/36. No assertion, cleanup delay or allocation
  threshold is weakened. An intermediate test build catches a missing xUnit test
  cancellation token; the final project-creation call passes the active test token.
- Three additional Infrastructure regressions prove symbolic lookup, detached
  HEAD fallback and cancellation complete under a caller context that does not
  pump queued callbacks. The Git-focused group passes 24/24. After the context
  repair, the App-focused group passes 36/36, including all 16 ownership cases,
  the eight layout cases involved in the stalled run and adjacent lifetime tests.
- Owned noncooperative reads deliberately ignore cancellation until the fixture
  releases them. Close stays pending, all their tokens are cancelled and neither
  old labels nor new reads can be published. Another owner's token stays usable.
- All eleven changed/new C# files have exactly one type, no syntax errors and stay
  below hard limits. The status owner is 146 lines/27-line longest body; the
  existing Workspace remains 489 lines/57-line longest body. Resource keys have
  no duplicates. Scoped whitespace verification passes; solution build has zero
  warnings/errors.

#### Full-run diagnosis and final verification

The first App full run stalls during teardown of
`FileChangeSummaryLayoutTests.Summary_UsesBoundedRowsSharedGeometryAndExplicitDisabledUndo`.
The process remains alive. Thread stacks and read-only ClrMD task-chain evidence
show the headless consumer waiting on an empty queue while the Git runner has
finished and its callers await the stopped UI synchronization context. This is
an independently verified wait-chain defect, not a timeout inferred from silence.
Only after recording the evidence and checking the exact test process identity
is that run terminated. Its `full.trx` contains just 2,478 results, with 2,477
passed and one failed; it is incomplete and is not counted as a full pass.

That recorded failure is the unchanged
`ProviderCatalogLookupTests.WarmProviderCommandAndAvailabilityChecks_DoNotAllocate(missing: false)`:
5,856 allocated bytes against the existing zero-byte assertion. The same case
passes in the later focused and final runs. Neither production lookup code nor
the allocation assertion is changed, and the intermittent allocation cause is
not claimed to be repaired.

After the wait-chain repair and final build, one complete run of all eight test
assemblies produces the following parsed `final.trx` results:

| Assembly | Passed / total |
| --- | --- |
| Agent | 112 / 112 |
| App | 3,894 / 3,894 |
| App.Visual | 8 / 8 |
| Tools | 483 / 483 |
| Platform.Windows | 137 / 137 |
| Browser | 76 / 76 |
| Infrastructure | 199 / 199 |
| Providers | 516 / 516 |
| **Total** | **5,425 / 5,425** |

All eight summaries are completed, their result counts match, and there are zero
failed or not-executed cases. This final success does not erase the historical
incomplete run or establish a fix for earlier intermittent allocation/deletion
failures. No test-run restart is used as a substitute for the wait-chain repair.

#### Prior directory-delete failures and limits

The earlier complete App runs failed at temporary-root deletion in
`WorkspaceConversationFlowTests`, not at a model/result assertion. This turn's
unmodified native-flow baseline is replayed twenty times: all 40 cases pass.
A read-only 90-second observer sees 194 Git process IDs under 38 synthetic test
directories; it records only PID, authorized temporary cwd and relative timing,
not command lines, environment, credentials or user-project data.

These observations establish that real Git metadata processes participate, but
do not identify the process holding a directory at an earlier failed deletion.
The ownership defect is independently proved and fixed; it is not presented as
conclusive attribution or universal repair of that intermittent OS-level failure.
Historical failed runs remain in [UI_HOTPATH_OPTIMIZATION_ACCEPTANCE.md](ENGINEERING.md#ui-hotpath-optimization-acceptance).

No fresh native window, DPI or screenshot parity check is performed for this
lifecycle slice. Tests use synthetic/local services and no paid provider or user
database. Debug remains JIT; release remains Windows x64 single-file without AOT
or JSON source generation. No staging, commit, push or publish is performed; the
full product goal remains active.

Evidence is outside Git at
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-repository-status-lifetime-20261012-133140`:
source/resource/architecture snapshots, `flow-baseline-*.trx`,
`git-baseline-observations.json`, `lifetime-red.trx`, `lifetime-focused.trx`,
`lifetime-green.trx`, `context-green.trx`, `git-context.trx`,
`app-hang-stacks.log`, `app-hang-async-verified.log`, `full/*/full.trx`,
`final/*/final.trx`, `solution-final-build.log`, `whitespace-final.log`,
`source-final-audit.log` and `diff-check-final.log`.
<!-- /source: REPOSITORY_STATUS_LIFETIME_ACCEPTANCE.md -->


---

<a id="restored-worktree-authorization-acceptance"></a>

## RESTORED_WORKTREE_AUTHORIZATION_ACCEPTANCE.md

<!-- source: RESTORED_WORKTREE_AUTHORIZATION_ACCEPTANCE.md -->
### Restored worktree operation authorization

#### Plan and required self-check

1. Application owns binding a persisted task/project to worktree authorization;
   Infrastructure verifies paths and Git identity. Tools.Abstractions owns the
   portable per-context guard contract. No UI filesystem/process access.
2. Extend the existing Git worktree port with read-only validation and reuse the
   standard executor for previews, diff, statistics, undo, terminal and browser
   operations. Existing contexts without managed worktrees retain their behavior.
3. Verify the exact task-GUID path, authorization roots and matching registration
   before approval and again before execution. Reject stale/mismatched contexts,
   keep a bounded cooperative deadline and await cleanup. Validate before model
   execution/resume persistence and before direct branch discovery as well.
4. Keep guards and validation helpers in separate one-type files; do not grow the
   499-line WorkspaceViewModel or App.axaml. No cache of successful path checks.
5. Prove valid restored operation, forged/foreign paths, revocation, reparse
   replacement after context construction/approval, cancellation/deadline, safe
   audit/error output, no provider call and no consumed draft on denied resume.

#### Status

The final candidate's expanded related regression passes. Complete current
solution regression and native acceptance are not established by this
checkpoint. This is not whole-product acceptance.

#### Operation boundaries

- A saved `WorktreePath` is not a directory grant. Infrastructure's read-only
  validator requires the exact owning task's canonical GUID directory, current
  project authorization snapshot, matching repository metadata and registration.
  Validation neither creates application folders nor removes worktrees.
- Application binds the validator to immutable project/root snapshots in
  `ManagedWorktreeWorkspaceGuard`. The context must retain its working directory
  and authorized roots; successful validation is never cached across operations.
- The standard tool executor checks the guard after schema/capability validation,
  before approval and again before execution. Validation has a cooperative
  thirty-second ceiling, honors shorter tool timeouts and caller cancellation,
  awaits cleanup, and returns bounded localized failures without raw exceptions.
  Ordinary contexts without a managed-worktree guard keep their previous path.
- Resume validates the stored task against the repository's project before
  creating attachments/messages or persisting Running state. Run preparation
  validates before history hydration/provider invocation. Direct branch reads
  fail closed without a validator, with a mismatched project, or on rejection.
- Previews, Git diff, change statistics, undo, terminal controls and browser
  toolbar mutations retain their context guard through the existing tool
  pipeline. Child-agent context copies retain it through their own approval and
  execution checks. Task attachment creates only a lazy browser-session handle.
  Preview file picking is explicit user selection; opening the selected path
  still uses the guarded pipeline. Stored artifacts retain independent task
  ownership checks rather than acquiring a grant from a saved working directory.
- Explicit worktree cleanup also checks task/project identity and validates the
  owning task's path before removal. A valid worktree belonging to another task
  must remain registered/on disk, and rejected cleanup must not clear SQLite
  metadata. Dirty-directory retention and explicit confirmation are unchanged.
- Terminal guard rejection uses the existing actionable path-authorization
  message instead of the generic execution-failed label.

#### Deterministic evidence

The fixtures use isolated temporary Git repositories, real SQLite restoration,
production App composition, loopback model responses and fake agent-model
streams; they do not access user windows, credentials or paid providers.

- Restored invalid source/foreign-task/relative/empty paths and project
  authorization revoked before reopening deny tools, preview/diff/terminal
  operations, branch discovery and continuation. Draft/history/status remain
  intact, no terminal session starts and no provider request is made. The valid
  restored task reads its own file and continues only on explicit submission.
- Real filesystem tests cover task-GUID/canonical-path rejection before native
  discovery, changed roots/working directory, replacement with a junction after
  earlier success, and cancellation/deadline process cleanup without deletion.
- Tool-pipeline tests cover both approval boundaries, safe audit completion,
  schema/capability ordering and cleanup completion before cancelled/timed-out
  validation returns. Child-agent tests independently reject at its pre-approval
  or post-approval boundary, and exercise the successful child-tool path.
- Cleanup's two task/project mismatch cases failed before the fix because no
  exception was raised. The reports are retained as
  `cleanup-authorization-red.trx`; the production fix adds task-bound validation,
  not a force-delete or relaxed test expectation.
- The first expanded App candidate found a stale branch-status test that created
  an arbitrary worktree path without any validator. The updated test creates an
  explicitly owned probe worktree; separate missing/rejecting-validator cases
  assert that no branch discovery starts. The original failure remains recorded.

#### Final related checkpoint

`artifacts/restored-worktree-authorization-final-20261018` contains terminal
reports from the same frozen source/configuration and binary candidate:

| Validation | Result |
| --- | --- |
| Complete Tools assembly | 506/506 |
| Complete Infrastructure assembly | 442/442 |
| Complete Agent assembly | 140/140 |
| Related App workflows, excluding names containing Native | 799/799 |
| Real Windows Git process-ownership cases | 8/8 |
| Total above, without adding overlapping focused runs | 1,895/1,895 |
| Complete solution build | Zero warnings/errors |
| Changed handwritten C# type/size/syntax audit | 238 files, no violations |
| Maximum checked C#/AXAML file length | 499 lines |
| Maximum checked method length | 72 lines |

All five test reports have terminal Completed outcomes, no failed/skipped cases,
and successful command exits. `terminal-results-summary.json` retains commands
and counters; logs and individual TRX files remain alongside it.
`validated-identity-check.json` verifies all 2,070 source/configuration entries,
1,601 binary/native dependencies and the exact source ZIP contents without
drift. Documentation is separate from the tested code/configuration manifest.
The first expanded candidate remains 794/795 App cases, alongside 506 Tools,
442 Infrastructure and 140 Agent cases; its stale branch-status expectation is
not erased or counted as a pass. The focused cleanup red/green reports are
retained separately and are subsets, not additional final coverage.

The older `worktree-path-safety-final-20261018` complete run has now terminated:
7,326/7,326, including 5,127 App cases. Its frozen source predates the external
metadata and restored-task fixes, so that result is not current-candidate full
regression evidence. Any new full run must use this candidate's frozen binaries,
preserve its own reports and be awaited to a real terminal result.

The subsequent complete run of this restored-authorization candidate has now
terminated with exit code zero: 7,355/7,355 across eight Completed TRX reports,
including 5,137 App cases, without failed or skipped tests. All 1,601 recorded
binary/native dependencies still match their frozen identities.
`full-test-terminal.json` and `full-regression-summary.json` retain the real
command exit, timestamps and all counters. These are pre-Git-staging binaries;
source-policy tests may also inspect the live worktree. This result does not
establish complete regression coverage of the later staging candidate or native
user-window/restart acceptance.

#### Remaining limits

Per-operation checks do not pin native filesystem identities and are not proof
against replacement during a native Git/file/process operation. Authorization
is an operation/task-context snapshot, not a demonstrated live revocation
subscription for already-running work. Cached display-only data may remain
visible after an external filesystem change; new tool actions are revalidated.
Creation-time crash recovery, native EXE restart, existing user-window acceptance,
and the complete product remain incomplete or unverified. No staging, commit,
push, packaging or publishing is performed.
<!-- /source: RESTORED_WORKTREE_AUTHORIZATION_ACCEPTANCE.md -->


---

<a id="source-account-connection-acceptance"></a>

## SOURCE_ACCOUNT_CONNECTION_ACCEPTANCE.md

<!-- source: SOURCE_ACCOUNT_CONNECTION_ACCEPTANCE.md -->
### Source-account connection checkpoint

#### Scope and entry point

Settings → 源代码账号 now creates, edits, enables/disables and deletes independently
named GitHub personal-token connections. The shared dialog selects an explicitly
configured proxy or direct access. Each row's 刷新身份 action reauthenticates with
the stored configuration; 刷新列表 only reloads local metadata and does not claim
to refresh authentication. This is not a PR inbox, repository grant, OAuth login,
Git credential manager integration or a claim of Codex connector compatibility.

#### Design self-check

1. Domain owns immutable account/identity values and endpoint validation;
   Application owns the repository/identity/HTTP ports and revision-checked
   connection refresh. Infrastructure implements SQLite and GitHub HTTP. App
   calls these ports and uses the shared settings controls, not SQL or HTTP.
2. Reuse proxy profiles and the existing settings lifetime/shared dialog; do not
   fabricate an AiProvider or reuse its authenticated headers for source access.
3. Saving credentials and explicitly testing their identity are user settings
   operations, not Agent tools. They create no task roots or tool grants.
   Subsequent PR reads/writes must implement source/repository authorization and
   use the shared execution pipeline; account verification is not that grant.
4. Every new handwritten C# file has one type. Account editing, catalog lifetime,
   safe row metadata, migration, transport, parsing and ports are separate files;
   no partial-class workaround or per-page visual style is introduced.
5. Controlled HTTP, real SQLite, raw loopback sockets and production-composed
   Headless UI tests cover persistence to actual requests and displayed identity,
   with rejection, cancellation, stale results and credential-leak negatives.

#### Runtime and safety boundary

- Schema 18 adds source_accounts without deleting or reinterpreting existing
  projects, tasks, drafts or providers. Migration failure rolls back creation;
  retry and repeated initialization preserve existing data. Revision-checked
  saves/deletes reject stale editors and cannot resurrect a deleted saved row.
- Proxy references prevent deleting an in-use profile. Enabled connections reject
  missing/disabled proxies. Disabling an account can preserve its existing
  disabled proxy; revocation does not depend on a working network configuration.
  Missing references and reenabling with a disabled proxy still fail. Runtime
  creation rechecks the selected profile and never
  falls back to direct or another account. Proxy-list refresh preserves the
  selected unavailable identity and the current editor draft.
- API addresses require HTTPS, no credentials/query/fragment/ambiguous escaped
  paths, and an API root or /api/v3/ prefix. GitHub.com uses api.github.com, not
  its website. An explicitly configured Enterprise server keeps its authority,
  port and prefix. This validation does not establish that a custom server is
  trustworthy; configuring it explicitly chooses the credential recipient.
- GET user is built from that pinned base, not response URLs. Authorization is
  added to this request only. Production handlers disable redirects, cookies and
  inherited system proxies; direct access and explicit proxy access are distinct.
  Ordinary platform HTTPS certificate validation remains enabled.
- Identity requests have a 30-second deadline and linked cancellation; headers
  are bounded, decompressed bodies stop at 64 KiB, and JSON depth is limited to
  16. Non-success bodies and Location URLs are not exported. The displayed
  numeric ID/login shape is validated and direct credential reflection rejected.
  Authentication, forbidden/rate-limit, redirect, timeout and network errors use
  fixed localized messages, without the original response or exception chain.
- Credentials follow the existing plaintext-SQLite product decision. Account
  and editor JSON serialization excludes the credential field, ToString does
  not print it, and list rows contain metadata only. The dialog masks the token
  and clears it on closing/disposal. No logging/export path is added.
- Refresh reads the account before and after the request and requires the
  captured revision to match. A changed/deleted/disabled connection cannot
  publish the old successful identity. This is a consistency check, not an atomic
  remote permission guarantee or the still-missing live PR grant revocation.
- Late reads/saves cannot replace a newer form. A save may commit before
  cancellation; the UI warns to reload and does not replay it on startup.
  Cancellation awaits request/body cleanup before releasing the operation.

#### Validation candidate

Candidate: artifacts/source-accounts-20261018. Earlier Git push and tool-scope
candidates remain separate, unchanged historical evidence. Initial diagnostics
retain a stale restore-assets build failure and a test-only false-positive where
the body string [] matched Byte[] in a stack trace; neither is hidden as a pass.
Restoring the existing declared dependencies fixes the former; the latter checks
the bounded public message while separate credential tests inspect ToString.

This first candidate has five Completed, zero-failure/skip reports: 490
Infrastructure, 624 Tools, 145 Agent, 99 related App and 300 Visual checks,
totaling 1,658/1,658, plus a complete zero-warning/error solution build. The 2,220
source/configuration and 2,259 runtime file hashes/path sets agree before/after
the run; no source ZIP or recoverable runtime snapshot is claimed. The shape
audit passes 360 C# files. Twelve Headless renders cover both themes, compact and
ordinary viewports and 1/1.25 scaling; selected renders were visually inspected.
Focused passes overlap broader reports and are not added again.

A separate source-account-disable-20261018 candidate follows this first version.
Its two new SQLite/UI regressions first fail because an unavailable proxy also
blocks disabling the account. The fix separates revocation from enabled-proxy
validation, preserves the selected proxy and retains the missing-reference and
reenable denials. Earlier broad results do not validate this later code; its
current build and regression have separate terminal evidence.

The follow-up now terminates with a clean complete solution build and two
Completed reports: 493 Infrastructure and 100 related App checks, totaling
593/593 without failures/skips. All 2,221 selected source/configuration and 2,259
runtime file hashes/path sets match before/after testing. The 361-file changed/
new C# type/size/syntax audit and git diff whitespace check pass. The five focused
green cases overlap these reports. Neither the earlier 1,658 cases nor earlier
focused UI runs are added to this later total. Native/live-account and complete
solution-test acceptance remain separate.

#### Still incomplete

Repository-specific grants, PR search/inbox/URL/details/diff/comments/checks,
review chat, revision-bound feedback and create-PR tools remain to implement.
OAuth, GitLab, cloud review and plugin compatibility are not substituted with
token settings. No real account, paid model, production GitHub authentication,
native user window or EXE restart has been used for this checkpoint.
Controlled loopback forwarding deliberately replaces only the HTTP factory in
UI tests; it proves requests/identity presentation, not production TLS trust or
a real GitHub account's permissions. Raw production-handler tests separately
exercise redirects/cookies/direct access and the selected HTTPS CONNECT proxy.
<!-- /source: SOURCE_ACCOUNT_CONNECTION_ACCEPTANCE.md -->


---

<a id="source-pr-inline-comments-acceptance"></a>

## SOURCE_PR_INLINE_COMMENTS_ACCEPTANCE.md

<!-- source: SOURCE_PR_INLINE_COMMENTS_ACCEPTANCE.md -->
### Verified PR inline comments and keyboard location

#### Implemented workflow

The authorized-repository PR inspection page now connects the loaded file and
inline-comment pages. After explicitly fetching both, a current-head comment on
an exact file path and a verified LEFT/RIGHT patch line appears below that line
in the shared code preview. Its location button selects the file, switches back
to changes, scrolls the card into view and focuses the actual card. Repeated
activation of the same comment can locate it again without another HTTP request.
Same-side multi-line comments require the entire range to be present in loaded
patch hunks and attach to the verified end line; their list label retains the
start and end. Context lines retain distinct old/new line numbers.

Only the two currently loaded pages participate. Historical commits, missing
line/side metadata, unloaded or case-different paths, renamed previous paths,
unavailable/incomplete patches, absent lines, gaps in multi-line context and
cross-side ranges do not receive guessed anchors. Their text and an explicit
localized explanation remain available in the inline list. No files are fetched
implicitly, and comment content is untrusted selectable text, not executable
markup, a model instruction or a URL to follow.

#### Design and safety self-check

1. Domain owns `PullRequestPatchMap`, hunk validation and comment-location states.
   Infrastructure reuses the map when validating file responses and preserves
   optional start-line/side metadata in its bounded comment parser. App owns
   page reconciliation and presentation through shared controls and styles;
   it performs no HTTP, SQL, filesystem or process operation for location.
2. The existing source read ports, five exact read tools, common executor and
   revision-bound source scope remain the only remote read path. This adds no
   tool, credential storage, database schema, directory grant or source write.
3. Existing approval, thirty-second read deadlines, cancellation, bounded output,
   audit and before/after revision/grant checks remain unchanged. Patch validation
   limits are 32768 characters and 1000 newlines per patch, with the existing
   per-page budget. ASCII hunk counters reject malformed, overlapping and
   overflowing ranges; additions/deletions must match the complete patch.
4. Collection changes, file/comment pagination, selection, scope replacement,
   observed head/grant changes, leaving and disposal clear or rebuild markers.
   Generation checks invalidate saved navigation commands. The preview applies
   annotations only in diff mode, preserves ordinary search behavior, keeps
   cards in the horizontal viewport and focuses only a currently visible target.
   Subscriptions are detached on disposal. Handwritten types remain separate,
   with shared appearance variants rather than per-view constants.
5. Tests cover exact side/context/range mapping and refusal cases, production
   response parsing through view-model location, real Headless keyboard Space
   activation and focus, stale pages/commands, observed head/revocation changes,
   ordinary-preview isolation and long-code horizontal scrolling.

#### Candidate and intermediate evidence

Current candidate: `artifacts/pr-inline-comments-20261018`. The earlier
`artifacts/pr-inspection-20261018` remains untouched historical evidence; its
source/runtime hashes and 1982 passes do not validate this implementation.

- `infrastructure-focused-current.trx`: 82 passed, zero failures/skips.
- `app-focused-current.trx`: 59 passed, zero failures/skips, including the complete
  multi-line metadata-to-location chain. These focused totals are not added to
  the later regression total.
- `app-scroll-focused-third.trx`: two LEFT/RIGHT cases passed. Targets initially
  lie below the viewport, two cards share the target line, and both first and
  repeated location must scroll and focus the exact live selected card fully
  within the viewport. The first attempt used an obsolete bitmap-save overload;
  the second compared a detached pre-selection template instance. The corrected
  assertion resolves the current visual card and retains the scroll/focus checks;
  those earlier build/failure logs are preserved, not counted as navigation bugs.
- An early fixture incorrectly treated a shortened context-line body as an
  incomplete patch; removing the entire context row correctly tests incomplete
  hunk shape. The initial failing report is retained.
- Initial Headless frames exposed an inline-list visibility binding evaluated
  against the wrong child data context. A parent-context visibility container
  corrected the implementation, and a focused effective-visibility assertion
  now covers it. Initial frames are not final visual evidence.
- Compact light and ordinary dark `ui-focused-second` frames were visually
  inspected: the duplicate list is hidden, the verified card and target line are
  visible, and focused text remains readable. These are component Headless
  frames, not native screenshots or whole-product visual acceptance.

#### Terminal validation

- Complete solution Debug build succeeded with zero warnings/errors in
  `build-final.log`. It includes the final offscreen-navigation tests.
- Five disjoint terminal reports all completed with process exit code zero:
  Tools 644, Infrastructure 587, Agent 146, related App 354 and visual-component
  300: **2031 passed, zero failures/skips**. This is not all App or all solution
  tests; focused and earlier-candidate results are not added to this total.
- App's selected regression took 5 minutes 7 seconds. The original live session
  was awaited to completion without cancellation or restart; observation waits
  were not interpreted as failures. Visual regression took 1 minute 37 seconds.
- All 2263 selected source/root configuration files and 1426 selected test-runtime
  files retain their actual pre-test SHA-256 identities and exact path sets.
  Scope is recorded in `validation-terminal.json` and `validate-current.ps1`:
  Git-listed `src`/`tests` plus present root engineering/build configuration, and
  all files under the five test projects' `bin/Debug` directories. Docs and App's
  standalone output are not covered by these manifests. This is identity
  verification, not a restorable source/runtime archive; no ZIP was created.
- A Roslyn audit of 442 present changed/untracked C# files, including 2535
  methods/accessors, found no single-type, file-size or method-size violations;
  the largest inspected method/accessor is 78 lines. All 26 changed/untracked
  AXAML files satisfy the hard file limit. `git diff --check` passes.
- `ui-terminal` contains 28 Headless frames: 16 PR/annotation frames and 12
  source-account regression frames. Compact-light and ordinary-dark inline
  cards, the LEFT offscreen target and compact-light/ordinary-dark full PR pages
  were visually inspected. The focused target is fully visible after actual
  scrolling, and the inline list is not duplicated in the patch section. Other
  frames were generated but are not claimed to have been manually inspected.

#### Remaining scope

This is the frozen inline-comment candidate's ledger. Subsequent schema-20
snapshot review chat and investigation-draft work is recorded separately in
[SOURCE_PR_REVIEW_CHAT_ACCEPTANCE.md](GIT_WORKTREES.md#source-pr-review-chat-acceptance); the 2031 results above do not validate that
later source or change the original evidence scope.

The complete goal is still active. Inbox/URL/filter workflows, reviews/activity,
review chat, investigation drafts, pin/viewed revision persistence, related
stacks, confirmed remote feedback/creation, OAuth, GitLab and cloud review remain
open. Cross-side/historical comments are readable but not current-patch anchors;
this is not full historical diff navigation or a complete multi-line highlight.
Separate reads and revision rechecks are not server-side snapshot isolation and
cannot detect an unseen ABA revision change or guarantee comment immutability.

Evidence uses synthetic credentials, controlled HTTP, temporary SQLite and
Headless rendering. Real GitHub accounts/permissions, production TLS, native
high-DPI input, user windows, actual EXE restart and Release packaging are not
validated here. No commit, push or release is part of this slice.
<!-- /source: SOURCE_PR_INLINE_COMMENTS_ACCEPTANCE.md -->


---

<a id="source-pr-inspection-acceptance"></a>

## SOURCE_PR_INSPECTION_ACCEPTANCE.md

<!-- source: SOURCE_PR_INSPECTION_ACCEPTANCE.md -->
### Version-bound PR files, comments and checks

This document preserves the historical `pr-inspection-20261018` slice and its
terminal evidence. The later inline-card/line-validation implementation and its
separate candidate are recorded in [SOURCE_PR_INLINE_COMMENTS_ACCEPTANCE.md](GIT_WORKTREES.md#source-pr-inline-comments-acceptance).
Limitations below describe this frozen slice, not a reclassification of its
1982 passing tests as evidence for later source changes.

#### Implemented slice

The existing authorized-repository PR page now reads changed-file pages and
patches, conversation comments, inline comments, check runs and legacy combined
commit statuses. Select a PR, read its details, choose a content category, and
explicitly fetch that category. Pages have independent loading, cancellation,
previous/next, empty and limit states. Reading never posts a comment, opens a
server-supplied URL, downloads CI logs, checks out a repository or invokes a model.

Wide layouts reserve a narrow PR list and give remaining width to the details.
Compact layouts switch between list and details with a real back action, rather
than reserving half of a small viewport for an inactive pane. Shared responsive
columns also collapse hidden compact panes. Patch selection does not implicitly
scroll the enclosing details away from its navigation controls.

This is not complete PR parity. Inbox/URL/filter workflows, reviews/activity,
inline diff annotations and line validation, review chat, investigation drafts,
pin/viewed revision persistence, related stacks, confirmed writes, OAuth, GitLab
and cloud review remain open. Historical inline comments retain their original
commit/path/line/side metadata; displaying that metadata is not a claim that it
matches a valid line in the current patch.

#### Design and safety self-check

1. Domain owns revision and result values. Application coordinates source-account
   grants and client ports; Infrastructure implements fixed HTTP endpoints and
   separate file/comment/check parsers. Separate built-in tools use the existing
   common executor. App consumes results through small view models and shared
   controls, not direct HTTP, SQL, filesystem or process calls.
2. Reuse the existing source transport, proxy selection, audit sessions, async
   prevalidation and approval/deadline/cancellation/output pipeline. A shared
   repository-access resolver preserves the older list/detail authorization
   semantics. No schema migration or new credential storage is introduced.
3. Each inspection is bound to the exact locally granted account/repository and
   the PR number/base/head already displayed. Local revision/grant validation
   happens before approval without HTTP. The reader checks live PR revisions
   before and after content HTTP and rechecks grants between requests and before
   publishing. Observed scope/account/revision changes clear all visible content
   categories and disable stale reads until fresh PR details are read.
   Navigation, selection, parent operations and disposal cancel outstanding work;
   cleanup is awaited and generations reject late results. Source scope permits
   only the five exact read tools, even without an explicit allowlist. The new
   tools remain hidden from model descriptors until review-chat isolation exists.
4. Each HTTP response keeps the existing 30-second deadline, 2-MiB decompressed
   cap, bounded JSON depth, no redirects/cookies/retries and local pagination.
   The standard tool has a 30-second deadline for the complete three-request
   inspection; each page has an outer 40-second cancellation window. App's source
   context allows 1 MiB of output. Serialized inspection output exceeding its
   context limit fails explicitly instead of returning a successful truncated
   JSON document. All new handwritten C# files contain one type and stay below
   file/method hard limits. HTTP dispatch is off the UI thread.
5. Deterministic tests cover production HTTP parsing, temporary SQLite grants,
   standard source tools and production App composition/Headless controls.
   Negative coverage includes denied approval, foreign source scope, revoked
   grants/accounts, changes to either base or head, cancellation and late cleanup,
   bounded output, invalid/duplicate/oversized data and reflected credentials.
   Shared-layout tests preserve existing ordinary columns and exercise compact
   hidden panes and the new fixed-primary variant.

#### Data completeness and trust boundaries

- File requests use locally constructed `/pulls/{number}/files`, 20 files/page,
  at most 150 pages (GitHub's 3000-file ceiling). A full final allowed page is
  marked limited; an earlier page is not a promise that the whole PR was read.
- Patch states are available, missing, incomplete or too large. Missing does not
  mean binary or unchanged. Available requires consistent unified-hunk ranges
  and additions/deletions. This validates shape/counts, not source-code semantics
  or correctness. Oversized patches retain file metadata but no partial text.
  Limits are 32768 characters and 1000 newlines/file, 65536 retained patch
  characters/page. Incomplete but bounded patches are explicitly labeled.
- Comments use fixed issue or PR-comment endpoints. Each body is bounded to
  16384 characters, the page to 65536 body characters, and a page to 20 records.
  Oversized pages fail visibly rather than silently truncating commentary.
  Text is selectable plain text: no remote Markdown images/scripts/navigation.
- Check runs and legacy statuses are separate categories so legacy CI is not
  silently omitted. Endpoints use the expected head commit; returned head IDs
  must match. Only metadata is returned, never third-party log/output URLs.
- Comment/check pagination is locally bounded to 1000 pages and reports the cap.
  No authenticated request follows Link, raw/content/comment/check URLs from the
  response. Optional comment owner URLs, if supplied, must match the fixed scope.
- Before/after revision checks are not a server-side atomic snapshot. They cannot
  exclude ABA changes or prove comments/checks remained unchanged between calls.
  No immediate revocation guarantee is claimed for a request already dispatched.

#### Validation candidate

Current candidate: `artifacts/pr-inspection-20261018`. Earlier source-account,
Git and `pr-reading-20261018` candidates remain frozen and are not reused as
verification of these changes. Intermediate failed build/test reports are kept;
only separately named terminal reports establish the final candidate result.

Initial comment-fixture failures came from public GitHub owner URLs paired with
an intentionally enterprise-prefixed account; fixture URLs now follow that
configured API origin/prefix. New UI markup initially referenced a nonexistent
card wrapper and a style binding without type context; both were corrected to
existing shared cards and explicit reflection binding. Full-page renders exposed
compact detail compression and selection-driven outer scrolling; the shared
layout and explicit selection-scroll behavior were corrected and tested.

Tests use synthetic credentials and controlled handlers, temporary databases and
Headless rendering. They do not verify real GitHub permissions, production TLS,
native windows/high-DPI input, actual EXE restart or a Release package. No user
window/account was operated, and no commit, push or release is part of this slice.

#### Terminal validation

- Complete solution Debug build: zero warnings/errors (`build-terminal.log`).
- Five disjoint terminal reports: Tools 644, Infrastructure 557, Agent 146,
  related App 335 and visual-component 300: **1982 passed, zero failures/skips**.
  These are not cumulative totals from the intermediate focused runs or older
  candidates. The App selection covers PR/source accounts, responsive columns,
  automations, navigation, approvals and code previews; it is not all App tests.
- App's expanded selection took about five minutes. Its live test handle and
  testhost/child processes were inspected while awaiting completion; it finished
  successfully without cancellation or restart. A temporary external stack
  diagnostic was uninstalled afterward. No UI-test hang or defect was inferred
  merely from the longer runtime.
- All 2242 selected source/project/resource files and 1426 runtime files match
  their actual pre-test hashes and path sets after the terminal runs. This is
  identity verification, not a restorable source/runtime archive. No ZIP was
  created. `validation-terminal.json` contains numeric counters and scope.
- Architecture tests and a Roslyn audit of present changed/untracked C# files
  found no single-type/file/method hard-limit violations (1796 methods inspected).
  `git diff --check` passes.
- `ui-terminal` contains ten PR frames (grant dialogs, inspection content and
  full pages) plus twelve source-account regression frames. Compact light and
  ordinary dark full PR pages and the ordinary dark inspection frame were
  visually inspected. These are controlled Headless frames, not native-window
  screenshots or full-product visual acceptance.

The overall functional-parity goal remains active. The next PR work still
includes review chat, validated inline diff annotations, inbox/URL workflows and
explicit confirmed feedback, not a relabeling of this read-only slice as parity.
<!-- /source: SOURCE_PR_INSPECTION_ACCEPTANCE.md -->


---

<a id="source-pr-on-demand-reading-acceptance"></a>

## SOURCE_PR_ON_DEMAND_READING_ACCEPTANCE.md

<!-- source: SOURCE_PR_ON_DEMAND_READING_ACCEPTANCE.md -->
### Version-bound, opt-in PR model reads

#### Design and safety self-check

1. Domain owns immutable chat mode and PR revision. Application keeps the existing
   source access and revision-checked inspection use cases. Infrastructure owns
   forward schema 22, preserving old chats as Snapshot. App only selects a mode
   and builds task context; Agent consumes context-aware tool metadata through
   its existing catalog port, never concrete tools or HTTP.
2. Reuse the source read tools, standard executor, source account/proxy clients,
   task audit, history, cancellation and pinned snapshot budget. A distinct
   ReadOnlySource conversation per grant/PR/base/head/mode avoids changing the
   permissions of an existing snapshot chat or another running conversation.
3. Only source.pr_get/files/comments/checks are advertised in connected mode.
   Exact number and base/head constraints are checked before and after approval
   and inside execution. Source use cases recheck account/grant after HTTP and
   inspection reads compare remote revisions before/after. Lists, arbitrary URLs,
   writes, filesystem, shell, browser, MCP and subagents remain unavailable.
   Every query uses task cancellation, a 30-second tool deadline, bounded pages
   and output, and ordinary audit/artifact handling. No opening sends a model
   request or fetches additional source data; only explicit send starts execution.
4. Keep one type per C# file and existing size limits. Add shared wrapper actions
   and localized permission disclosure, not per-page appearance or UI SQL/HTTP.
5. Prove schema preservation/rollback and mode isolation; safe catalog projection;
   exact request scope, denial, approval, revocation, output limits, timeout and
   cancellation; real model-tool-source-model serialization, visible tool results,
   multiple turns, pinned context, reload and manual recovery. Headless shared
   controls and synthetic transports are distinct from native/live acceptance.

#### Implementation and evidence

The implementation adds an explicit "打开只读查询对话" action with localized
permission disclosure and accessible name/help. It uses shared controls and a
shared scroll-content gutter, not a per-view appearance override. Schema 22
includes mode in the review-chat key, preserves existing conversations as
Snapshot, and retains the no-project/no-worktree/ReadOnly persistence boundary.
Opening/reopening neither sends nor fetches source pages; modes keep independent
drafts/history and reopening keeps the first pinned snapshot.

Context-aware metadata exposes exactly four source tools only to ReadOnlySource
tasks. Their existing pipeline owns schema, scope, approval, deadline,
cancellation, output limiting and audit. Get verifies the returned revision;
files/comments/checks verify remote base/head before and after the page. Oversized
bound Get JSON is refused without partial Content; legacy UI Get keeps its
existing pipeline truncation behavior. Fixed localized errors explain revision,
scope and output failures to the model without exposing exception/HTTP text.

Agent tool execution now starts on a worker instead of inheriting the caller's
UI synchronization context. Bounded progress, task cancellation and awaited
cleanup retain the same operation identity. If access changes while progress is
being consumed, the review guard can reject the event and cancel the in-flight
operation before the reader's post-HTTP denial completes. The audit then records
either source_scope_denied or tool_cancelled, while the conversation is Failed
with a source-scope hint. Both paths must finish audit, discard content and refuse
the next provider request; the tests assert those effects, not a race-specific
choice of audit code.

##### Focused evidence

`artifacts/pr-source-reads-20261018` preserves all intermediate build/test reports:

- Solution builds third/fourth completed with zero warnings/errors. Before the
  worker change, all six model-to-source roundtrip variants failed a direct
  UI-thread assertion in app-ui-thread-red.trx; afterward the original 18 source
  workflow/safety cases passed in app-focused-second.trx.
- The expanded app-focused-fourth.trx passed 36 cases: explicit no-send opening,
  six Responses-model/tool/source/model roundtrips, real tool output JSON, pinned
  user context, audit, visible tool cards, reload, multiple turns, mode isolation,
  PR/SHA/other-tool refusal, before-send/during-HTTP revocation and credential
  changes, before/after-page head changes, cancellation and explicit continuation,
  and four shared-control keyboard/theme/viewport cases.
- tools-focused-third.trx passed 42 source/PR cases including approval-time
  revocation and bound/legacy Get output behavior; agent-focused.trx passed all
  155 Agent tests. infrastructure-focused.trx passed 14 review/context migration
  cases, including v21 preservation, two-mode identity, invalid mode and atomic
  migration rollback/retry. These runs overlap and are not a full-suite total.
- The disclosure geometry test originally selected an unarranged nested
  scrollbar; those exploratory failures remain recorded, not used as product
  proof. After selecting the owning viewer's template scrollbar, the unmodified
  page still failed all four app-disclosure-corrected-red.trx cases: text occupied
  the overlay scrollbar lane. The shared content gutter fixes that boundary;
  app-disclosure-corrected-green.trx records the post-fix check.

These tests instantiate production App composition, temporary SQLite, real
provider request/response serialization and source HTTP handlers. The model uses
synthetic Responses SSE and source responses are controlled; no paid provider or
real GitHub account was used. They do not establish live service compatibility,
native Windows interaction or Release acceptance.

##### Frozen candidate

`artifacts/pr-source-reads-final-20261018` contains the final build, structural
audit and eight-project unfiltered regression runner. It captures selected
src/tests/root configuration hashes and every file in the eight Debug test
runtime directories before and after testing. Source/tests/build configuration
are not changed during that run. Read validation-terminal.json for the actual
terminal result; a started run is not a pass. The preceding snapshot/context
candidate remains frozen at artifacts/pr-review-chat-context-final-20261018 and
does not validate this slice.

The eight-project run above reached terminal: 7,906 passed and two App structural
assertions failed, with no skips. Selected source/configuration (2,297 files) and
Debug runtime (2,035 files) had zero path/hash changes. Keep its Failed result:

- SurfaceVariants_DeclareSharedCardAndFieldStyles assumed one literal selector
  per style and did not recognize the shared comma-separated modal/content-gutter
  rule. The follow-up assertion splits selector alternatives and checks both
  variants; no padding/style is duplicated or removed.
- SettingsOverlay_UsesSharedNavigationComponent still expected seven buttons
  despite the earlier source-account settings integration adding the eighth.
  The follow-up checks the exact unique 0..7 command destinations and the
  source-account label, while retaining all shared-component/property checks.

Only these two test contracts change after that frozen run. Production behavior
is unchanged. artifacts/pr-source-reads-verified-20261019 is the separate candidate
for focused contracts, a new solution build/structural audit and another complete
eight-project run. Its own validation-terminal.json, not the preceding results,
must establish whether the follow-up candidate passes.

##### Verified follow-up result

artifacts/pr-source-reads-verified-20261019/validation-terminal.json is terminal
Passed. The new solution build has zero warnings/errors; focused contracts/source
tests passed 57/57 before the frozen run. The unfiltered eight-project regression
passed 7,908/7,908, with no failures, skips or aborts:

| Project | Passed |
| --- | ---: |
| Tools | 666 |
| Infrastructure | 602 |
| Providers | 600 |
| Agent | 155 |
| App | 5,351 |
| Visual | 300 |
| Browser | 88 |
| Windows | 146 |

The source/configuration identity set (2,297 files) and Debug runtime set (2,035
files) both have zero path/hash differences before/after. The structural audit
covered 484 changed/new C# files, 27 markup files and 2,738 methods, with one type
per C# file, no size violations and a maximum method length of 78 lines.
Thirty-six Headless frames were saved, including the four new-entry
light/dark/526x601/980x740 cases. The four new-entry frames were visually inspected;
permission text stays outside the overlay scrollbar lane and keyboard focus is
visible. This does not claim every saved frame has received a new visual audit.

The preceding 7,906-pass/two-failure run remains Failed and unchanged. The new
result validates only this current implementation and its tested workflows, not
full PR parity, live GitHub/provider/native behavior, Release or overall product
completion. No commit, push, release or user-window operation was performed.

#### Remaining scope

This slice does not implement PR writes, inbox/URL opening, reviews/activity,
OAuth, GitLab, cloud auto-review or CI-log downloads. Even matching base/head
checks are not server-side atomic snapshot isolation. A changed revision requires
explicitly refreshing the PR and opening its matching conversation; no automatic
repinning or retry is allowed. The full functional-parity goal remains active.
<!-- /source: SOURCE_PR_ON_DEMAND_READING_ACCEPTANCE.md -->


---

<a id="source-pr-reading-acceptance"></a>

## SOURCE_PR_READING_ACCEPTANCE.md

<!-- source: SOURCE_PR_READING_ACCEPTANCE.md -->
### Source repository authorization and PR reading

This document records the frozen initial list/detail candidate. The current
files/comments/checks extension and its separate validation are described in
[SOURCE_PR_INSPECTION_ACCEPTANCE.md](GIT_WORKTREES.md#source-pr-inspection-acceptance); the historical counters below do not
validate that extension.

#### Implemented scope (2026-10-18)

The navigation rail now opens **Pull Request**, independently of project/chat
selection. Connect an enabled GitHub source account in settings, refresh accounts
and grants, choose **Authorize repository**, enter owner/name, then explicitly
fetch open PRs. The page provides local pagination, selected-PR description and
base/head commit IDs, cancellation and repository revocation. It uses shared
dialogs, controls, responsive columns and localized feedback in both themes.
Editing a source account invalidates its existing grants; revoke/re-authorize
instead of silently applying an old grant to new credentials. Granting is local
consent, not proof that GitHub permits the operation; service authorization errors
remain visible.

This is a first read workflow, not full PR parity: personal inboxes, PR URLs,
closed filters, diff/files, comments/reviews/checks/activity, pinned/viewed revision
state, review chat, draft check investigation, confirmed remote writes, OAuth,
GitLab, cloud review and native/live-account acceptance remain open. PR reads do
not checkout/clone/fetch, run shell, post feedback or invoke a model.

#### Design self-check

1. Domain owns immutable source scope and bounded result shapes. Application owns
   grant/session/client ports and read coordination. Infrastructure implements
   SQLite/HTTP; Tools.BuiltIn depends only on the reader abstraction, and App
   calls repositories/standard executor rather than SQL, HTTP or tools directly.
2. Reuse source HTTP/proxy configuration, shared schema/capability/approval/
   timeout/output/audit execution, and shared UI components. New source tools are
   deliberately hidden from model descriptors until a distinct review-chat
   runtime is implemented; plain projectless chats remain tools-disabled.
3. Grants bind account ID, credential revision and exact canonical owner/repo.
   A source-scoped context rejects filesystem/process/screen/browser/MCP tools
   even under FullAccess or an absent allowlist. Async preflight validates before
   approval and again before execution. Reads revalidate after HTTP; revoked or
   changed scope cannot publish a completed response observed after revocation.
   This does not claim atomic exclusion of a concurrent revocation and an already
   dispatched network read. Every HTTP operation owns a 30-second deadline,
   cancellation and awaited response/client cleanup, 2-MiB decompressed body cap,
   bounded JSON depth and field/record counts. The page uses an outer 40-second
   operation deadline. No automatic retries, cookies, redirects, pagination URL
   following or TLS bypass are introduced.
4. Schema 19 migrates forward and retains user accounts/tasks. Read-operation
   sessions create a separate audit-owned task identity with no local roots and
   no ordinary conversation entry. Reopening does not dispatch requests. Explicit
   grant revocations retain ID tombstones so that old IDs cannot be recreated.
   New classes keep one type per file and files under the repository hard limit.
   SQLite entry points and source-operation setup run off the UI dispatcher;
   only final observable updates return to the UI. Request-capture tests assert
   that HTTP dispatch does not execute on the UI thread.
5. Deterministic tests exercise the production client, parser, repositories,
   reader, source tools and App composition. Negatives cover cross-repository
   response substitution, wrong PR number, credential reflection, malformed/large
   bodies, auth/permission/rate-limit/redirect errors, stale/revoked grants,
   in-flight account changes, cancellation, denied unrelated tools, pre/post-
   approval revalidation, output limits and schema injection. Headless tests use
   keyboard activation of real shared buttons and theme/viewport renders.

#### Evidence boundaries

Candidate: `artifacts/pr-reading-20261018`. Original failed/initial reports are
retained separately from final reports. The first HTTP test expected a public
endpoint while its fixture intentionally configured an enterprise API prefix;
the expectation was corrected to the configured authority/path. The initial UI
test used a raised Click event instead of actual Button keyboard activation;
keyboard events now exercise command dispatch. Neither is relabeled as a
production-network or native-window defect.

The broader regression also exposed an unintended extra timestamp check on old
non-source tools, causing late-completion restore tests to stop before their
committed inverse snapshots. The extra deadline check now applies only to tools
using the new async prevalidator; old execution timing is unchanged. The new rail
entry now follows automations so that adding PR does not move that established
navigation target. The old tests were retained unchanged; both repairs have a
focused rerun in `tools-repair.trx` and `app-repair.trx`. Final reports below are
separate from these earlier failed/repair candidates.

The tests use synthetic credentials, controlled HTTP handlers, temporary SQLite
and Headless UI. They do not establish real GitHub token permissions, production
TLS, real-window/high-DPI operation or EXE restart. Previously frozen source
account/Git candidates are not new PR evidence or additive totals. Final counters
and current validation reports are recorded alongside this candidate.

#### Terminal validation

- Complete solution Debug build: zero warnings/errors (`build-terminal-second.log`).
- Tools 635, Infrastructure 519, Agent 146, related App 146 and visual-component
  300 tests: **1,746 passed, zero failures/skips**. These are the five
  `*-terminal.trx` reports, not totals added to earlier or overlapping runs.
- Agent architecture tests check handwritten single-type/file limits; a focused
  Roslyn method-size audit also checks 50 methods in the new coordinator and
  affected execution/persistence/MainWindow owners. Whitespace validation passes.
- Four compact/ordinary light/dark grant-dialog Headless frames are retained in
  `ui-terminal`; compact light and ordinary dark frames were visually inspected
  during this slice. These are not native-window screenshots or full PR-page
  visual acceptance.
- `validation-terminal.json` records numeric counters and source/runtime identity
  checks. Source hashes cover selected C#/AXAML/project/resource files at both
  ends of the terminal run. Runtime hashes are observed during the run and
  rechecked afterward; they are not a pre-run runtime snapshot or a restorable
  source/runtime archive.

The overall functional-parity goal remains active. No commit, push or release
was requested or performed in this slice.
<!-- /source: SOURCE_PR_READING_ACCEPTANCE.md -->


---

<a id="source-pr-review-chat-acceptance"></a>

## SOURCE_PR_REVIEW_CHAT_ACCEPTANCE.md

<!-- source: SOURCE_PR_REVIEW_CHAT_ACCEPTANCE.md -->
### PR snapshot review chat and investigation drafts

#### Design and safety self-check

1. Domain owns an immutable revision/scope/context binding. Application owns the
   source-chat repository port, bounded snapshot builder, opening use case and
   access validation. Infrastructure owns forward schema 20 and transactional
   SQLite persistence. App only calls ports/use cases and reuses ordinary
   conversation, model selection, attachment, streaming, draft and cancellation
   components. The dependency direction is unchanged.
2. Reuse `IWorkspaceRepository`, `IConversationDraftRepository`, `IAgentRunner`
   and current source repository-access checks. Opening creates or restores a
   distinct ordinary projectless task per grant/PR/base/head, not the shared
   repository audit task. Source data is captured only from already explicitly
   loaded bounded inspection pages. Creation never calls a provider or source
   server and never checks out/clones/fetches Git data.
3. The source grant/account must still match at creation, before an existing
   conversation consumes a prompt, before model execution and before presenting
   each model event. Source chats always have empty roots/working directory,
   ReadOnly policy and disabled tools/subagents/browser sessions. The fixed
   snapshot is user-role untrusted data, not system instructions. A 24,576
   UTF-16-code-unit context budget and explicit patch/text clipping bound storage;
   ordinary model-aware budgeting and streaming cancellation still apply.
   Opening has a 30-second deadline. Investigation uses only loaded check names,
   status and conclusions; it follows no log URLs and never sends automatically.
4. Each handwritten C# type is separate; no existing 500-line Workspace/Main
   view model is expanded. Source writes stay in Infrastructure. Draft seeding
   uses the existing compare-and-swap revision contract and refuses any existing
   text or attachment. This is not a distributed transaction with UI draft state;
   concurrent local edits must retain their existing conflict protection.
5. Focused tests must prove SQLite migration/rollback/reopen/revision uniqueness,
   credential/revocation and environment refusal, context bounds/untrusted role,
   selected model/reasoning and actual provider transport, manual sending,
   cancellation/history/restart and no tool or unrelated-draft access. Headless
   keyboard/navigation tests and current build/regression results are recorded
   separately from real-source/native/Release acceptance.

The long-history correction is owned by Agent's existing context builder, with
an optional fixed user-context field on Application's run request. App supplies
the immutable PR binding through that field rather than the oldest chat turn.
The builder reserves its complete user-role message before selecting history,
including metadata and a minimum 256-character latest-turn budget. It never
promotes source text to system instructions or silently clips/omits the snapshot.
Capacity refusal happens before provider transport, uses a dedicated Application
exception and a localized actionable failure-card hint, and requires manual retry.
Schema 21 extends the existing stable failure-category allowlist transactionally
so this diagnostic survives persistence, catalog reload and reopening. No new
permission, HTTP/file access or automatic external side effect is added.
Focused Agent tests cover budget/role/tool metadata and bounded allocation;
production-App tests cover actual serialized requests after long saved history
and refusal/recovery. No large view model is expanded.

#### Current status

The production PR page now opens revision-bound ordinary projectless model
conversations and prepares failed-check investigation drafts without sending.
The current candidate is `artifacts/pr-review-chat-context-final-20261018`; its
long-history and persistent visible-error corrections have separate terminal
verification below. The preceding `pr-review-chat-context-20261018` reached
2830/2830 selected passes with unchanged source/runtime paths and hashes, but
its Activity-only capacity error was not sufficient visible-error acceptance.
The preceding `artifacts/pr-review-chat-ready-20261018` reached terminal state:
2819/2819 selected tests passed, with unchanged source/runtime paths and hashes.
`pr-review-chat-20261018` retains the earlier focused/debugging evidence and its
terminal 2819-test expanded run. The ready candidate additionally waits for
ordinary conversation-model preference restoration before synthetic send actions;
this does not change product readiness/approval policy or weaken assertions.
The previous `pr-inline-comments-20261018` remains frozen historical evidence,
not validation of these changes. The overall functional-parity goal remains active.

#### Implemented chain and focused evidence

- `PullRequestsViewModel` captures only currently loaded inspection items.
  `SourceReviewChatCreator` validates source access; `SqliteSourceReviewChats`
  transactionally creates/restores the task and version binding. SHA casing does
  not create a duplicate task. Reopening an archived version explicitly restores
  it; changing base/head creates another conversation rather than repinning it.
- The existing catalog navigates to that task, preserving ordinary new-chat and
  other task drafts. `WorkspaceTaskFactory` revalidates access before persisting
  another user prompt. `WorkspaceRunExecutor` resolves the saved binding and
  passes its snapshot as fixed user context, never as a system message. Saved user
  messages stay free of the injected snapshot. The selected provider/model and
  reasoning effort reach the real Responses request builder/stream parser.
- `WorkspaceComposerDraftsViewModel.TrySeedAsync` seeds the correct cached draft
  through its existing background writer. A loaded empty cache is updated along
  with SQLite; unsaved text, whitespace, attachments, failed reads/writes and
  revision conflicts are not overwritten. A newer user edit during a seed save
  wins; the caller cannot report that its original seed was retained.
- `SourceReviewChatPersistenceTests` covers repository reopen, immutable context,
  revision uniqueness, invalid local environments, revoked/changed credentials,
  forward migration, rollback and preservation of preexisting user data.
  `SourceReviewContextBuilderTests` covers context budgets, omitted/clipped data,
  failure filtering and the smaller investigation budget.
- The production-App controlled-HTTP tests cover manual multi-turn sends, saved
  user/assistant history, different heads, cancellation and explicit continuation,
  access refusal before sending, revocation before a reply and between two actual
  SSE deltas. The latter preserves the first authorized delta but neither presents
  nor persists the second. Unsolicited local/source tool calls produce no tool
  execution/audit record or additional source HTTP request.
- Real shared PR-page buttons are activated with Space. Light/dark 526x601 and
  980x740 checks verify target visibility/focus, empty-cache draft seeding,
  preservation of another draft and no implicit provider/source request. Other
  tests refuse successful/unloaded checks and late navigation after leaving.
  Separate draft tests cover cancellation, disposal and CAS conflict behavior.
  These are controlled transport/temporary SQLite/Headless checks, not native UI,
  production TLS, paid model calls or real source-account verification.

#### Debugging record

The first App compile exposed an array `Count`/`Length` typo in draft seeding;
it was corrected without changing the draft protection condition. An earlier
infrastructure expectation was also corrected to the repository's earlier
environment-reassignment refusal, rather than pretending the SQLite trigger was
the first failing boundary.

The first runnable App fixture omitted `HttpClient.BaseAddress`. The request
failed before entering its synthetic transport, while a test awaited an unbounded
`Started` signal. Async/exception inspection proved the invalid-request-URI
failure and the already-terminal product run. Only that identified test child
was stopped; its aborted TRX is not passing evidence. The fake client now uses
the configured base URI and Started/cancellation waits have explicit bounds.
Text async/exception evidence remains in the earlier candidate; temporary local
diagnostic tools and the synthetic-process dump are not product dependencies.
Their cleanup command was rejected by the execution policy; these temporary
files remain only under the ignored earlier artifact directory, not in the
product or a global tool installation.

A patch assertion originally compared JSON-escaped text with literal `+new`;
it now decodes the stored JSON patch and checks its exact content. An initial
stream fixture timed out before observing its first delta. Correct SSE metadata,
explicit command preconditions and model-readiness waits were added; the original
timeout remains in `app-review-chat-focused-fourth.trx`. Metadata alone is not
claimed as a proven root cause. Subsequent complete runs, not the earlier timeout
or a single isolated pass, establish the current result.

#### Final verification

The final candidate reached terminal state successfully. `build-final.log`
records the complete Debug solution build with zero warnings/errors.
`structural-terminal.json` audits the 467 changed/new C# and 26 AXAML files,
2656 methods/accessors, maximum method length 78, no size/type/whitespace violations.

| Selected group | Passed | Failed/skipped |
| --- | --- | --- |
| Tools (all) | 644 | 0/0 |
| Infrastructure (all) | 597 | 0/0 |
| Providers (all) | 600 | 0/0 |
| Agent (all) | 155 | 0/0 |
| App (PR/source, workspace, drafts/model/history/failure, automation, approval/navigation/preview filters) | 586 | 0/0 |
| Visual (all) | 300 | 0/0 |
| Total selected | 2882 | 0/0 |

`validation-terminal.json` records all six process exit codes as zero and each
TRX as completed. Before/after identities and path sets match for 2285 selected
source/root-configuration files and 1583 files in the six test runtime directories.
The source selection includes Git-listed tracked/untracked `src` and `tests`
files plus the present root engineering/build configuration. It does not freeze
all ignored artifacts, external services, user data or documentation.
Do not combine overlapping focused or earlier runs into this total. This is
controlled transport/temporary SQLite/Headless evidence, not a full App test run,
native input/EXE restart, real GitHub, paid-model, Release or full-product acceptance.

#### Long-history context correction

The pre-fix standalone probe used the real Debug Agent context builder with a
512-character snapshot, 40,000-character old user and assistant messages, a
4096-token model context and 1024-token output reserve. Its terminal result in
`artifacts/pr-review-chat-context-20261018/context-before.log` was
`SnapshotRetained:false, LatestPromptRetained:true`. The oldest user-role
snapshot had been evicted by ordinary newest-turn selection.

The corrected probe supplies the snapshot through `PinnedUserContext` instead
of normal history. `context-after.log` records both snapshot and latest prompt
retained. Nine new Agent cases plus the existing builder cases passed 18/18 in
`agent-context-focused.trx`; 27 review-chat/draft cases passed in
`app-context-focused-second.trx`, including actual Responses JSON with an intact
first snapshot after long persisted history and a second manual turn. Capacity
refusal calls no provider, retains the submitted prompt in history and shows the
localized repair action. Refreshing a larger model context does not send; an
explicit retry succeeds with the same binding. That first context candidate
persisted `Unknown` and checked only Activity, which was insufficient; the
visible/persistent correction below supersedes that limitation.

The initial test build missed the nullable project-ID argument on the repository
test call; the following build corrected it. The first App assertion also assumed
Responses text content was an array, but this text-only request correctly uses
a string. Both failed reports are retained, and the corrected assertion compares
the exact decoded user string, not a JSON substring. These were test defects,
not provider failures or a relaxation of snapshot/role assertions.

#### Visible and persistent capacity diagnostics

Tracing the real conversation view established that Activity is visible only
while a run is active. Its failed-task card instead binds TaskFailureHint to the
persisted failure category. The added red assertion in
`pr-review-chat-context-ui-20261018/app-visible-hint-red.trx` therefore failed:
expected the specific context repair action, received the generic task failure.
The previous green counts did not establish visible feedback.

The corrected chain uses Domain's stable `ModelContextCapacity = 16`, exact
Application code `model_context_capacity`, schema 21's transactional allowlist
extension, and the existing WorkspaceFailureText/AppTaskFailureNotice path.
Raw exception/source/provider text never becomes the hint. The same category
survives SQLite reads and conversation catalog reload, is isolated to the task,
and clears on manual successful continuation. No large Workspace model or
per-page visual appearance is added. Schema 20 remains the PR-chat binding
migration; schema 21 only extends persisted failure metadata.

The focused terminal reports show 35 Infrastructure cases and 68 App cases
passing, without combining them into the final expanded total. They cover v20
history/pinned/archive preservation, idempotence, repository reopen, rollback
after a deliberately rejected version insert, explicit migration retry,
allowlisted category roundtrips and invalid metadata. App checks exact versus
spoofed codes, safe presentation/observers, the real persisted selected-task
hint after catalog reload, no implicit retry, and shared-card wrapping at
320/640 widths in both themes. The implementation follows ui-ux-pro-max's
error-feedback/near-context and wrapping guidance, not a new visual system.

The first expanded context-ui run reached terminal state with unchanged source
and runtime identities: all groups except one Infrastructure fixture passed.
That v19 rollback fixture deleted only schema marker 20 and retained marker 21,
so its expected 19 historical versions actually numbered 20. The correction
deletes all markers from 20 onward when constructing that historical fixture;
the rollback assertion remains 19 rather than being weakened to 20. The failed
terminal report is retained; the final candidate reruns the selected groups.

#### Explicit remaining scope

This starts a manual review conversation over an immutable, bounded initial
snapshot; reopening the same version retains the first snapshot, not newly
loaded pages. Refreshing the PR to a different base/head creates a different
conversation. A model cannot fetch more PR data in this mode; source-only model
read tools remain a separate gap. Historical local messages remain available
after revocation, but revocation blocks new review turns and observed in-flight
output. This is not cloud auto-review, immediate remote revocation, a server-side
atomic snapshot, an inbox/URL workflow, reviews/activity, viewed/pinned PR state,
or confirmed source writes. Native input, real GitHub and Release validation
are not established by controlled fixtures.
<!-- /source: SOURCE_PR_REVIEW_CHAT_ACCEPTANCE.md -->


---

<a id="source-pr-url-acceptance"></a>

## SOURCE_PR_URL_ACCEPTANCE.md

<!-- source: SOURCE_PR_URL_ACCEPTANCE.md -->
### Explicit PR URL opening

#### Scope and ownership

This adds an explicit account-and-URL dialog to the existing PR page. It opens
one authorized GitHub PR directly, including closed PRs that are absent from the
open-PR list, and attaches the existing revision-bound inspection and review-chat
flows. It is not the complete Codex PR workflow or a personal inbox.

- Domain owns `SourcePullRequestLink` and the resolved scope/number value.
- Application's `SourcePullRequestLinkResolver` reads existing account/grant
  ports and reuses `SourceRepositoryAccess`; it does not create authorizations,
  task sessions or HTTP requests.
- App's independent `PullRequestLinkViewModel` owns only form state and operation
  lifetime. `PullRequestsViewModel` performs detail reading through the same
  standard `source.pr_get` executor used by list selection. Views use shared
  controls and resources; they do not use SQL, files, processes or HTTP.
- No schema or publication-mode changes are needed. One handwritten type per
  C# file, module direction and existing size limits remain applicable.

#### User path and interpretation

1. Open Pull Request, then choose **从 PR 链接打开**. Opening the dialog makes no
   source/model request, and does not change an unfinished repository grant form.
2. Explicitly select a saved source account, identified by name and API endpoint.
   A first-time dialog does not silently select an account. A subsequent opening
   retains its choice only if the metadata identity/revision still exists.
3. Paste an HTTPS web PR URL and submit. Invalid input, wrong account/platform,
   missing authorization and remote failures remain visible in the dialog, with
   unchanged editable input and explicit repair/retry instructions.
4. The returned PR becomes the single visible result and selected detail. The
   status explains that this is not an open-PR page. **获取开放 PR** explicitly
   returns to the normal open-PR list; no list query is silently added.
5. Existing file/comment/check reads, snapshot review, read-only source review
   and manual model sends retain their existing restrictions and persistence.

Recognized paths are `/owner/repo/pull/number`, optionally with a trailing slash
or `files`, `changes`, `commits`, `checks`. File/change/commit suffixes may include
one 40- or 64-hex commit. Query strings and fragments are ignored, never requested,
stored as task context, or used as instructions. These forms identify the PR;
they do not select a check, locate a comment, or pin a historical commit. The
dialog explicitly says that current PR details will be read.

`api.github.com` maps only to web `github.com` on its normal HTTPS port.
For a configured Enterprise endpoint, the web host and port must equal the
configured API authority; the existing API path prefix is retained when building
the request. No alternate web/API host mapping is guessed. Instances requiring
separate web/API authorities need a future explicit configuration extension.

#### Safety and lifecycle

- Reject userinfo, non-HTTPS links, blank/control characters, oversized input,
  backslashes, encoded authority/path, raw dot segments, unsupported paths and
  invalid/nonpositive/overflowing PR numbers before any HTTP request.
- Domain matching is exact, not suffix-based. No request follows the pasted URL,
  query, fragment, redirect or provider-supplied link. Only normalized owner,
  repository and number reach the configured source API client.
- The selected account must still be enabled and at its captured revision.
  Its exact repository must already have a current explicit grant. Another
  account on the same host cannot borrow that grant. Missing grants require the
  separate existing authorization flow, not a silent expansion of scope.
- Detail reads retain standard capability/schema checks, ReadOnly policy,
  source scope, bounded output, 30-second tool execution, cancellation and audit.
  Application still revalidates access after HTTP before releasing source data.
  The form additionally has a linked 40-second operation deadline.
- Previous inspection work is invalidated and drained before requesting another
  linked PR. Closing, canceling, navigating or disposing cancels the same
  operation; cleanup is awaited and stale results cannot populate another page.
  Reopening or editing never retries automatically.
- No local roots, worktree, browser session, child agent, source write or model
  permission is added by a URL. A linked review chat remains a separate ordinary
  projectless conversation bound to its exact grant/PR/revision/mode.
- Errors use fixed localized resources, not exception text, URL parameters,
  response bodies or credential-bearing account objects.

#### Validation

Verified candidate: `artifacts/pr-links-verified-20261019`. Its terminal reports
confirm the full Debug solution build with zero warnings/errors and 2,952 passing
tests, no failures or skips. This is seven unfiltered test projects plus filtered
App PR/source/shared-component/navigation tests, not all App or solution tests:

| Project | Passed |
| --- | ---: |
| Infrastructure | 660 |
| Tools | 666 |
| Agent | 155 |
| App, selected regression scope | 337 |
| Visual | 300 |
| Providers | 600 |
| Browser | 88 |
| Windows | 146 |

The new link-specific tests account for 58 Infrastructure and 26 App cases within
those totals. The selected 2,309 source/config paths and all 2,035 files across
the eight test projects' Debug directories have identical path sets and SHA-256
values before and after testing. Structural audit of the 496 changed/new C#
files, 27 AXAML files and 2,804 methods/accessors finds no violations; the largest
method is 78 lines. Eight new link-error/detail frames were visually inspected
after the terminal run, covering both themes and both window sizes.

The prior candidate `artifacts/pr-links-20261019` is retained unchanged: 2,947
passed and four grant-dialog tests failed because they counted all dialogs as
one after adding the link dialog. Source/runtime identities were stable there
too. The subsequent assertion targets the grant dialog by its actual DataContext
and close command, checks exactly one visible dialog and verifies both directions
of grant/link exclusivity; it does not relax the visible footer/control checks.
A missing-account recovery test and corresponding localized settings hint were
also added before rebuilding and rerunning the same broader regression scope.
Neither candidate establishes full-product completion.

Focused tests cover common URL forms and origin/port attacks, explicit grant and
account selection, stale/revoked/disabled/deleted accounts, cancellation without
session creation, actual pipeline HTTP and audit, remote failure/manual retry,
post-HTTP revocation, delayed cleanup, navigation/disposal, and waiting for prior
inspection cleanup. A separate controlled model/source roundtrip opens PR #42
from a link rather than an existing PR #7 list result and preserves #7's draft.

The rendering tests retain assertions for real shared controls, editable errors,
input/button/status bounds, keyboard Escape and returned opener focus. They save
error-dialog and linked-detail frames at 526x601 and 980x740 in both themes. An
initial wrapper incorrectly bound visibility and delayed inner-dialog attachment;
the final view mounts the shared dialog directly, without weakening focus/bounds
checks or changing global dialog behavior.

Evidence uses production DI, temporary SQLite, controlled source HTTP and model
SSE, and Avalonia Headless. It is not native Windows interaction, live GitHub or
Enterprise account verification, paid-model acceptance, Release validation or a
published executable. Inbox filters, reviews/activity, viewed/pinned markers,
related stacks and confirmed remote writes remain separate implementation gaps.
<!-- /source: SOURCE_PR_URL_ACCEPTANCE.md -->


---

<a id="worktree-base-reference-acceptance"></a>

## WORKTREE_BASE_REFERENCE_ACCEPTANCE.md

<!-- source: WORKTREE_BASE_REFERENCE_ACCEPTANCE.md -->
### Explicit worktree base reference candidate

Status: the independently validated candidate in `artifacts/worktree-ref-20261019`
was integrated through 22 exact paths with verified backups and zero unrelated
source differences. Original Debug/Release solution builds have zero warnings/errors;
each passes Infrastructure 743 and related App 109. Full Debug regression 14404
is terminal: 8,182 passed, one failed, no skipped/aborted tests. All eight reports
and unchanged 2,432-source/2,429-runtime identities were reviewed; its freeze ended.
The failure is the environment flyout retaining DismissOnButtonClick after adding
a reference input. The original failure remains in regression-summary.json.
The prior original complete regression 54927 passed 8,153 tests;
its eight reports and unchanged source/runtime identities were checked and its freeze ended.
The initial candidate Debug statistics failure remains unclassified. A separately
reproduced observation defect has been corrected and tested, including a current
failure rejection case. Passing checks do not recover the missing historical code.

#### Behavior and boundaries

The existing environment flyout has a localized shared input for a local branch,
tag or commit. Empty input retains HEAD. Selection is captured with the send
request, locked during preparation, ignored for project-directory sends and
refused as an existing-task override. Infrastructure resolves the supplied
revision with rev-parse --verify --end-of-options and commit peeling, then passes
only the verified immutable hash to worktree creation. A branch move after
resolution cannot substitute another commit. Invalid/noncommit/option-like,
oversized, control or internal-whitespace input refuses creation without falling
back, sending a model request or discarding the draft/reference.

This extends the existing Application port, factory and send snapshot. Existing
root/link/repository guards, cancellation, total timeout, bounded output,
task-owned native process trees, fixed conversation directory and persistence
rollback remain in place. No schema change: Git retains the actual checkout and
the task retains its managed worktree path. No fetch, remote operation, branch
discovery or command sandbox is introduced. Unsent environment choice persistence
is not added by this slice.

#### Current evidence

- Candidate Debug/Release solution builds: zero warnings/errors after correcting
  a wrapper name and obsolete placeholder/PNG APIs in the new code/tests. Original
  failed build logs remain separately named in the candidate directory.
- Initial Debug feature selection: Infrastructure 60 and App 45 pass. Expanded
  feature selection, including keyboard and resolution lifetime negatives:
  Infrastructure 65 and App 51 pass. These are overlapping selections.
- Full candidate Infrastructure in both configurations: 743 pass each.
- Expanded App: Release 107 pass; Debug 106 pass / 1 fail. The failure is
  `WorkspaceFollowupToolBoundaryTests.QueuedWritesUseTheirSavedPolicyAndPublishRealResultsAndAudits(AskEveryTime,true)`
  at its background-statistics IsSuccess assertion. Its queued write and file
  content assertions pass. The log does not record the audit error code, so no
  specific cancellation/tool/root cause is established. Diagnostic replay adds
  audit metadata to the existing assertion message without weakening it.
- Diagnostic replay stops at iteration 10 on a different failure: the refused
  write waits 30 seconds after sending its denial before CurrentRequestId was
  published. The artifact-only Headless publication probe deterministically sees
  IsPending before a resolvable identity. The candidate now awaits the exact write
  tool, request ID and both commands' readiness; original policy/allow/deny/result
  checks remain. Six observation/tool-boundary checks pass. The probe is retained
  as a diagnostic text source, not a shipping test that freezes property order.
  This does not explain the earlier background-statistics failure. The revised
  readiness check then passes 20 independent processes / 60 overlapping checks;
  no statistics error code was observed in those replays.
- Final matching Debug/Release solution builds have zero warnings/errors. Both
  configurations pass full Infrastructure 743 and related App 107 checks, with
  no skips/aborts and unchanged before/after source/runtime path/hash sets. Four
  complete TRX hashes are retained in `candidate-verification-final.json`.
  These later passes do not identify or erase the first statistics failure.
- A subsequent artifact-only actual-Git/SQLite/production-executor Headless
  diagnostic gates the first statistics process completion until the next
  queued message replaces its file-card projection. At that replacement it
  records exactly the successful approved write and a completed statistics
  audit with `IsSuccess=false`, `ErrorCode=tool_cancelled`; a later query succeeds,
  the UI shows +1 and the write remains once. Both auditable snapshots are in
  `diagnostics/statistics-lifetime-observation.json`. This proves the old
  first-completed-audit predicate can observe superseded work. It still does
  not recover the error code missing from the original failure, so the original
  failure remained unclassified and the integration gate stayed closed at that
  checkpoint. No
  existing success assertion or approval policy was weakened. The first
  diagnostic without a Headless dispatcher timed out awaiting UI totals; its
  failed report is retained, followed by the passing dispatcher-backed report.
  Temporary probe types and fixture configuration are retained as text under
  diagnostics and removed from shipping test compilation; the original 2,428
  candidate source/configuration identities are checked after restoration.
- Expanded checks retain unchanged before/after path/hash sets for 2,428
  source/configuration and 2,429 executable/runtime files per configuration;
  generated test PNGs are excluded explicitly from that runtime identity set.
- Real temporary Git + SQLite + production Agent/ToolExecutor + simulated Responses
  requests cover an older branch, tag and commit, actual read result in the next
  model request, unchanged source dirt, later messages and history reopening.
  Negative cases cover unauthorized/link paths, invalid revisions, multiple roots,
  branch movement, cancellation and the original total deadline during resolution.
- Four shared-control Headless keyboard/render cases at 526/1000 widths in
  light/dark themes pass. Narrow images were inspected for readability/fit; these
  are not native Windows picker/focus acceptance.

#### Revised observation and integration decision (2026-10-19)

The old assertion required every background query to succeed and the total audit
count to be exactly two. The controlled real-Git/executor/SQLite lifecycle proves
that this is not a valid invariant: replacing the file-card projection may cancel
its old query before the current query succeeds. This observation defect is
established independently of the unclassified historical failure.

The shipping test now awaits the final Queue/run state and the current statistics
projection, drains its owner, and validates every completed audit. There must be
exactly one write; other records must be correctly authorized read-only statistics
calls. Earlier failures may only be tool_cancelled, must precede the final query,
and the final query and UI totals must succeed. The bounded reader must not be
full, and duplicate, interrupted or incomplete records fail. Existing saved-policy,
approval/denial, write-once, visible/persisted result and draft checks remain.

Two new controlled Headless cases prove old-query cancellation followed by success
and current-query failure. The latter injects a nonzero exit through the test
process port after real Git execution, obtaining snapshot_review_failed through
the production pipeline/UI; the same assertion rejects it. Snapshot mutations
also prove rejection of an unexpected old error and a cancelled final query.
This changes test observation only, not the production statistics lifecycle or
approval policy. The optional fixture seam uses the existing production DI.

The 22-path candidate has zero structural violations. Matching complete Debug and
Release builds have zero warnings/errors; both configurations pass Infrastructure
743 and related App 109 with four complete TRX reports. All 2,432 source/configuration
and 2,429 runtime path/hash sets per configuration remain unchanged. See
`statistics-observation-20261019/reviewed-candidate.json` for report identities and
`DESIGN.md` in that directory for the required self-check and evidence limits.

Engineering decision: replace the invalid observation gate with these explicit
lifecycle and failure checks and permit guarded integration. This is a revision
of the previous gate, not a claim that the historical failure was explained.
The original regression is terminal and reviewed. The old 16/17-path manifests,
all failed reports and diagnostic logs remain. Before copying, recheck all 22
original/candidate hashes and back up originals; then validate builds and runtime
tests in the original checkout. No unrelated path may be overwritten.

The guarded copy record is `artifacts/worktree-ref-integrated-20261019/integration-result.json`:
2,421 original source/configuration files become 2,432; only the expected 22 paths
differ. The first integration script attempt stopped at a PowerShell parse error
before any mutation. A baseline build inadvertently ran after that refusal; its
logs are explicitly named build-Debug-preintegration and are not integration
evidence. The corrected script then passed all guards and retained originals.
Both subsequent original-checkout solution builds passed with zero warnings/errors.

Four complete post-integration TRX reports pass: Debug and Release each contain
Infrastructure 743 and related App 109, with no skipped/aborted/failed cases.
Both configurations retain identical before/after sets of 2,432 source/configuration
and 2,429 runtime files (test-generated PNG artifacts explicitly excluded). Report
hashes are in `artifacts/worktree-ref-integrated-20261019/related-reviewed.json`.
These related checks overlap the candidate checks and are not additional feature
coverage. Full Debug regression 14404 ended with 8,182 passes and one interactive
flyout guard failure. All eight reports and before/after path/hash sets were
reviewed; its freeze ended. No historical green result substitutes for this failure.

#### Interactive reference flyout correction (2026-10-20)

The new reference input makes this a multi-step flyout. The App markup now uses
the existing shared default instead of opting into dismissal on every button
click. The existing selection commands still close it explicitly. No shared
component default, authorization, port, timeout, persistence or dependency changes.
The markup remains 28 lines and the single-type test stays below the file/method
limits. The existing guard is unchanged; the focused Headless test now types into
the real input, selects both environments with Space, verifies explicit dismissal,
reopens with the same reference and checks preparation locking in both themes at
526/1000 widths. This verifies keyboard behavior, not native Windows interaction.

Before the correction, a fresh check matched all 2,432 source/configuration and
2,429 Debug runtime paths/hashes to the reviewed failed regression. Backups and
new validation belong in `artifacts/worktree-reference-flyout-20261020`; the earlier
failure reports are retained. Debug/Release solution builds have zero warnings/errors.
Each configuration passes Infrastructure 743 and related App 112, including the
unchanged failing guard and all four enhanced keyboard cases. Four complete TRX
reports, counters, hashes and unchanged source/runtime sets are recorded in
`related-reviewed.json`. Full Debug regression 26906 is terminal and reviewed:
all 8,183 tests pass across eight distinct complete TRX reports, including App
5,450, with zero failures/skips/aborts and exit code zero. Before/after path/hash
sets match for all 2,432 source/configuration and 2,429 runtime files; the freeze
has ended. `regression-summary.json` records the counters and report hashes. This
full run covers the original checkout's flyout correction, not the isolated
Steer candidate. The earlier 14404 failure evidence remains unchanged.

#### Concurrent runtime check

An isolated CLI harness in `artifacts/worktree-concurrency-20261019` loads the
original compiled App assemblies, with matching hashes checked before running
and at completion. Both Debug and Release pass. It starts two conversations in
one production App composition, from a branch at baseline A and a tag at baseline B.
Both actual Agents read their own real Git checkout through the standard file
tool, and the next local simulated model requests contain only the expected
baseline. Both runs are simultaneously tracked. An explicit cross-worktree read
through the standard executor returns path_denied with a completed persisted
audit and no target contents; source uncommitted changes remain intact.

Cancelling A through the selected conversation's real primary command leaves B
running. B then completes; task states are Cancelled/Completed, and both independent
drafts/history/fixed worktree environments survive service reconstruction with
the model request counts unchanged. The harness does not instantiate a native
window or prove simultaneous Git worktree creation. It does not test shell
confinement or change approval policy. Its source/type/method audit has zero
violations and both 2,429-file product runtime sets remain unchanged.

A separate synchronized creation scenario also passes against original Debug and
Release assemblies. Two factory preparations share one real repository; a
test-only launcher barrier requires both worktree-add requests to arrive before
delegating to the unchanged native Windows task launcher. Both creations succeed
with different paths, their fixed selected commits and checkout contents, and
exactly one persisted task/message each. Source dirt stays unchanged. Reports and
hashes are in `artifacts/worktree-concurrency-20261019/parallel-reviewed.json`.
This is a positive service-level parallel-creation case, not GUI concurrency or
acceptance of cancellation, crash or partial-failure combinations during creation.

The first two harness attempts failed explicit model selection because its
lane-B provider was accidentally saved as disabled. Adding a readiness wait alone
did not fix that. Correcting the harness's named IsEnabled argument permitted
the selected model; no product fallback or policy was added. Both failed logs,
the initial passing run and the final matching Debug/Release runs remain.

#### Limits

Native interaction, live AI providers, other worktree lifecycle gaps and strict
command filesystem isolation remain open. This candidate is not whole-product
completion. Availability in the original App requires the integration record and
its matching validation, not candidate-only evidence.
<!-- /source: WORKTREE_BASE_REFERENCE_ACCEPTANCE.md -->


---

<a id="worktree-conversation-acceptance"></a>

## WORKTREE_CONVERSATION_ACCEPTANCE.md

<!-- source: WORKTREE_CONVERSATION_ACCEPTANCE.md -->
### New-conversation worktree selection

#### Scope and ownership

The composer now offers **项目目录** and **独立工作树** for a new conversation
with a project. Project-directory execution remains the default. Worktree
selection delegates through `WorkspaceTaskFactory` to the existing Application
`IGitWorktreeService` port and Infrastructure implementation; UI performs no
filesystem, SQL or process operations. This is an explicit application action,
not a model tool and not a grant of FullAccess.

The picker is hidden without a project. Existing conversations display their
persisted environment but cannot change it. Preparing a send locks and closes
the picker. A choice survives repeated notifications of the same context, but
resets when project/task context changes; unsent environment choice is not
persisted as a draft or restored after application restart.

Git creates a managed directory and `mossagent/{taskId:N}` branch from the source
repository's current HEAD. It requires an existing commit and does not copy
uncommitted source changes. Existing conversations reuse their saved worktree.
The task's primary authorized root becomes the worktree; other explicitly
authorized directories remain. Projectless conversations gain no roots.

#### Required design self-check

1. **Owner/dependencies:** App owns selection and preparation coordination;
   Application owns the Git port and Infrastructure owns native Git execution.
   No dependency direction, database schema or provider protocol changes.
2. **Existing contracts:** reuse `IGitWorktreeService`, `IWorkspaceRepository`,
   attachment snapshots and the existing task-context/tool-execution pipeline.
3. **Safety/lifetime:** retain task approval policy and standard tool gates.
   Git keeps its existing owned-process, 30-second and bounded-output behavior.
   Failed preparation recovery has a separate 30-second cancellation source so
   a cancelled caller does not prevent safe cleanup. Never use forced removal.
4. **Size:** selection and typed recovery exception have separate files;
   handwritten C# stays one type per file. The changed-source audit covers
   188 files, with a maximum of 500 file lines and 72 method lines, no failures.
5. **Evidence:** selection/locking, persistence ambiguity and cleanup failure
   unit tests; real temporary Git/SQLite and loopback model workflow; standard
   executor authorization negatives; Headless theme/width/keyboard checks,
   including a real send held at persistence to prove preparation locking.

#### Failed preparation and recovery

If message snapshot or task/message persistence fails after a worktree was
created, first query both active and archived tasks with the independent
deadline. Remove the new worktree only when neither contains that task ID.
Successful cleanup preserves the original preparation failure. If a task may
already have committed, lookup fails, or Git refuses removal, preserve the
directory and raise `WorkspaceWorktreePreparationException` with its path.

The composer shows a localized recovery notice advising the user to refresh and
inspect the retained task/path before retrying. It does not display raw inner
exceptions or Git stderr. SQLite is not a distributed transaction with Git or
file-backed snapshots: this is cautious recovery, not guaranteed atomicity.
Explicit cleanup failures also use a fixed localized recovery message instead
of displaying arbitrary exception/Git output; task metadata is not replaced by
an assumed successful cleanup.

#### Current evidence, 2026-10-18

Candidate: `artifacts/worktree-conversation-20261018`.

| Check | Terminal result |
| --- | --- |
| Initial selection/preparation unit checks | 18/18 passed |
| Final worktree/environment cases within related App checks | 29/29 passed, no skips |
| Final related App checks, including the six sidebar allocation cases | 97/97 passed, no skips |
| Final complete shared visual/component suite | 300/300 passed, no skips |
| Final Infrastructure Git worktree/process/repository checks | 17/17 passed, no skips |
| Complete solution build | 0 warnings, 0 errors |
| Changed/untracked handwritten C# shape audit | 188 files, no violations |

The final three test runs total 414 cases; the 29 worktree cases are included
in the 97 App cases, not an additional 29. Final TRX files are under
`results/final-app`, `results/final-visual` and `results/final-git`.
`terminal-results-summary.json`, `validated-binary-identity.json` and
`validated-source-identity.json` record the bounded checks and tested candidate.

The initial expanded run passed 25/26: its sole failure compared LF against
Windows Git's CRLF checkout. The test now normalizes line endings when checking
the committed baseline; it does not change global Git settings or production
checkout behavior. A later 27/27 run includes an additional real SQLite
rejection case; the final related run also includes the additional authorized-
root/resume and narrow long-label checks. Initial test API/analyzer build errors were corrected before
the successful solution build; failed reports are not erased.

Real workflow tests use only disposable temporary repositories. They explicitly
commit a fixture baseline, leave a source edit uncommitted, select a worktree,
send through the production composition and a loopback SSE provider, follow up
without creating another task/worktree, and reopen a fresh composition over the
same SQLite store. They verify the branch, original dirty file, task history,
persisted worktree and absence of automatic replay.

Using the standard executor, reading the worktree succeeds, reading the
unselected source root is rejected, and ReadOnly rejects writing in the
worktree. Cancelling a running model call retains the worktree. Explicit
two-step cleanup refuses dirty worktrees without losing the task/path; after
the fixture removes its own dirty file, confirmed cleanup removes the worktree
but preserves conversation history. A real SQLite first-message rejection
rolls back the task, cleans the unowned worktree, retains the input and sends no
provider request.

Headless renders the production composer in light/dark themes at 526/1000
pixels. Tests verify inherited business bindings, button bounds, Space-key
opening, option commands/labels, popup close on selection/locking, and the
projectless hidden state. Additional checks keep long project/agent names and
the environment button separate while resizing through 526/360/740/360 pixels.
The first resize test observed stale Headless window bounds: draining queued
dispatcher events before layout fixes the test timing without changing
production layout or relaxing its bounds/overlap assertions. Its failed reports
remain under `focused-expanded` and `narrow-before`.
One later 96/97 related run exposed another invalid fixture assumption:
manually setting `Catalog.IsLocked` can legitimately be overwritten by workspace
draft-state notifications. The control test now starts a real send and holds
its test repository's save, verifies the actual preparation-owned lock, then releases
and joins shutdown. It no longer simulates ownership by toggling the catalog
flag. All six control cases pass after that correction; the failed report is
retained under `validated-app`.
Four screenshots under the candidate's `images`
were generated; light/narrow and dark/wide were inspected directly. This is
not native window interaction or Codex pixel-fidelity evidence.

The previous frozen full-solution regression, session 95889, is now terminal:
7,154 passed and 1 failed across 7,155 tests. App's sole failure is the recorded
1,176-byte sidebar filter allocation assertion in that older binary. The other
seven projects passed. This candidate's focused passes do not turn that older
full regression green; related current-source checks are recorded separately.
The current six sidebar allocation cases pass in the related App run. No new
full-solution test run was substituted for the older frozen result.

#### Remaining limits

- No native EXE restart, original-window operation, real model service or paid
  API call was performed. No commit, push or release was made.
- This frozen candidate did not establish preparation-stage cancellation.
  The later [SEND_PREPARATION_CANCELLATION_ACCEPTANCE.md](CONVERSATIONS.md#send-preparation-cancellation-acceptance) records the new
  per-preparation lifetime, pre/post-commit recovery and partial Git creation
  checks separately; its results do not rewrite the counts above.
- Later Git creation recovery can report an existing managed directory after
  cancellation/failure, but does not prove every partial side effect is known
  or healthy. Normal removal retains the task branch; automatic branch deletion
  is not implemented.
- Existing worktree service path-prefix validation is unchanged. These checks
  do not establish complete worktree-link/reparse-point/metadata race safety.
- Branch/base selection, uncommitted-change transfer, parallel worktree startup,
  and crash-time recovery during worktree creation remain incomplete or unverified.
- Functional parity with the whole Codex Desktop product remains incomplete;
  see [FUNCTIONAL_PARITY_MATRIX.md](OVERVIEW.md#functional-parity-matrix) rather than treating test counts as completion.
<!-- /source: WORKTREE_CONVERSATION_ACCEPTANCE.md -->


---

<a id="worktree-object-authorization-acceptance"></a>

## WORKTREE_OBJECT_AUTHORIZATION_ACCEPTANCE.md

<!-- source: WORKTREE_OBJECT_AUTHORIZATION_ACCEPTANCE.md -->
### 任务工作树对象身份（进行中）

#### 本轮设计与工程自查

1. Domain 增加任务、项目、grant 和目录对象身份的绑定。Application 定义工作树授权端口，
   Infrastructure 负责真实 Git 验证与 SQLite 登记；Windows 继续实现既有根身份会话端口。
   App 仅组合服务和展示明确确认操作，不执行文件或数据库操作。
2. 复用 `IGitWorktreeService` 的创建/恢复/清理和标准 `ITaskWorkspaceLeaseProvider`。
   项目租约与工作树租约组合，保持 Agent、统一工具管线、长驻 PTY 原有消费入口。
3. schema 28 独立保存不可变的工作树 grant；创建校验成功时持有根句柄登记。
   旧工作树没有记录，必须在任务空闲时明确确认。确认核对已保存任务、项目版本、路径、
   状态和更新时间，持有目录链直到条件事务完成。已登记对象不能由确认操作替换；
   原对象丢失时应恢复原对象或新建任务，不能自动认领替换对象。
4. 复用 no-follow、本地 NTFS 身份、取消和迟到会话清理；Git 保留 30 秒总期限、
   有界输出、所属进程树。目录 grant 不授予 shell 隔离能力，也不改变审批策略。
5. 所有新增手写 C# 文件一个类型；按持久化、登记、租约和 UI 分文件，遵守 500/80 硬限制。
   验证迁移及回滚、旧记录不登记、复制 Git 元数据的同路径替换拒绝、租约释放、
   两任务隔离、取消、旧工作树确认、真实工具/PTY 与恢复。

#### 清理与其他保留边界

工作树运行租约禁止目录被移走。Git 清理必须删除该目录，因此不能在调用
`git worktree remove` 时继续持有禁止删除共享的目标句柄。本轮清理前复核保存的身份，
但释放身份会话到 Git 删除之间仍有竞态；不将此称为句柄级删除或严格安全清理完成。
后续需要句柄所有权交接、限定目标的 Git 注册清理及崩溃阶段记录，拒绝路径回退。
Git 子目录/元数据并未被全部固定；命令隔离、最终名称 CAS、严格资源 drain 和
MXC 独立环境验收仍未完成。

#### 验证

最终候选的完整解决方案 Debug、Release 构建均零警告/错误。两配置各 3,558 项通过，
无失败、跳过或中止；范围是下表七套完整自动化测试及 App 精选 414 项，
**不是 App 全套重跑通过，也不是全解决方案测试全套通过。**

| 套件 | Debug | Release | 范围 |
| --- | --- | --- | --- |
| Infrastructure | 835 | 835 | 完整；其中工作树对象持久化、迁移与取消专项共 10 项 |
| Agent | 162 | 162 | 完整 |
| Tools | 695 | 695 | 完整 |
| Platform.Windows | 422 | 422 | 完整 |
| Browser | 113 | 113 | 完整 |
| Providers | 617 | 617 | 完整；确定性服务/记录，不调用付费提供商 |
| App | 414 | 414 | 工作树、项目/根授权、终端、准备/取消、历史、撤销、原子运行、附件、统计及错误分类精选；含工作树生产专项 8 项 |
| App.Visual | 300 | 300 | 完整；自动化组件检查 |

`verify-focused.ps1` 保存构建日志、过滤器、八份 TRX、输入快照及终态。
Debug 使用 `--artifacts-path` 独立输出，避免覆盖首轮仍运行的旧程序集。
两配置各 2,688 项源码/配置的运行前后、配置之间及当前工作区路径/hash 一致。
独立读取 TRX 后逐项核对原 28 个失败的完整测试名称，最终两配置均已通过；
复核结果在 `independent-evidence-review.json`，不能仅凭类名过滤器推断覆盖。

结构审计：2,494 个手写 C# 文件、22,002 个方法/访问器/lambda，文件最大 500 行、
方法最大 78 行，单文件单类型及规模约束零违规。`git diff --check` 通过，
相关五份文档的本地链接检查通过；本轮相对起始快照改变 59 个源码/测试/文档路径，
没有删除路径；验证检查点的暂存区为空。后续提交身份以 Git 历史为准，未推送或发布。

| 层次 | 实际入口与证据 |
| --- | --- |
| SQLite/契约自动化 | `WorktreeObjectAuthorizationTests`、`WorktreeObjectCancellationTests`、`WorktreeObjectMigrationTests`；真实 Git/SQLite，身份 reader 是可控端口。覆盖 27→28、失败回滚/重试、历史不变、旧记录不登记、任务/项目陈旧与 Running 拒绝、持久化失败保留目录、取消后迟到会话释放、两任务独立租约 |
| 生产 App + 原生 NTFS/Git | `WorktreeObjectRuntimeTests`；实际生产 DI、统一管线及保存的卷/FileId。复制 `.git` 到替换目录后，旧 Git/路径检查仍通过，新守卫拒绝读取、恢复和清理；恢复原目录后原 grant 可读。审计终结；并发任务租约独立，模型流取消后释放，重开不续跑 |
| 真实 ConPTY | `WorktreeObjectTerminalTests`；`terminal.start` 通过统一管线，返回后工作树仍不可移走；项目重新授权撤销后所属终端停止、工作树释放，旧 context 再启动被拒绝 |
| Avalonia Headless + loopback | `WorktreeObjectUiWorkflowTests`；Light/Dark × 526×601/1280×820，实际 Space 激活确认、命中测试、完整路径展示、草稿和任务保留、确认不自动续跑、明确发送后沿原工作树完成多轮。不是原生 GUI 或实际 AI 提供商 |

原始记录在 `artifacts/worktree-object-authorization-20261021/`。初次 App 专项编译缺少
测试的 Avalonia 扩展命名空间；补齐后首轮 8 项中 4 项原生链路通过、4 项 UI 等待超时，
原因是测试把仅更新列表的 `UpsertTask` 当作历史加载入口。改为保存到列表后明确选择，
8 项通过。保留 `app-focused.log`、`app-focused-r2.log/trx` 和 `app-focused-r3.log/trx`。
随后扩大到 App 全套，发现旧共享准备继续使用保存前无 grant 的项目对象，影响错误分类、
历史、原子提交、撤销和统计；另有 Git 组合准备未明确列出主目录、附件恢复假定撤销全部根
后仍可发送。修正为读取保存快照并断言 grant，显式列出主目录，附件移除后明确恢复授权再
发送；没有放宽守卫或删除失败断言。定向修复不冒充再次完整运行 App 全套。
第一轮全套继续使用已编译测试程序集时，先备份四份测试准备源码并记录程序集 hash，再
修改测试准备和补充确认后的真实工具读取/确认取消负例；该轮输入差异为明确的测试修复，
因此不作为最终候选的无漂移证据。最终候选以 reviewed 两配置记录为准。
首轮 App 全套尝试使用修复前程序集，已明确停止本轮所属测试进程；TRX 终态为 Failed，
已报告 5,074 项通过、28 项失败、合计 5,102 项，不代表计划中的 App 用例全部执行。
`first-debug-stop-note.json` 保存中止时间、进程身份和失败清单，`current-Debug-terminal.json`
保留原报告与五份测试源码的运行前后差异。未中止或关闭用户已有应用；本轮测试进程均已退出。
最终两配置各输出 8 张工作树 Headless 截图。人工检查了 Release 浅色窄窗确认前和
Debug 深色宽窗确认后：完整路径可读，窄窗换行，确认/刷新按钮在可滚动输入区可达，
草稿保留；这不构成原生窗口、DPI 切换或真实模型验收。
另一次结构审计误用了旧检查点脚本的输出路径；原 JSON 已从本任务原始工具输出准确
恢复，新结果另存本轮目录。恢复来源和时间记录于 `audit-recovery-note.json`，未改写旧测试结果。

#### 尚未完成及详细实施方案

1. **创建对象归属与原子认领。** 当前登记的是 Git 成功返回后打开并复核的对象，
   不证明 Git 创建到打开之间没有被替换。下一步研究由 Windows 受控目录会话独占
   创建空目标、先记录身份并持有至 Git 和认领结束；先用真实 Git 验证预创建空目录
   的兼容性和失败语义，不能采用会接受已存在目标的普通 `CreateDirectory` 代替。
   引入 task/repository/base-commit/target/identity 的 intent 向前迁移，创建及任务/消息
   保存完成后条件认领；失败保留可查看的阶段和目录，启动不自动删除或重建。用可控
   断点覆盖目录创建、Git 退出、登记、消息提交前后宿主终止及两任务取消/部分失败。
2. **清理最终对象边界。** 先拒绝新运行并确认所属工具、PTY、Git 和浏览器资源已退出；
   通过受控删除会话打开目标，比较其身份与保存 grant，再按原句柄删除；打开时发现
   替换必须拒绝。需要研究限定到该 worktree 注册槽的元数据清理，避免删除目标后
   新同名目录被 Git 再次按路径处理，也不使用会影响其他任务的全局 prune。保存清理
   intent/阶段/未知状态，在对象删除、注册处理和任务元数据保存间强制终止验证恢复。
3. **已登记对象的重新授权。** 本轮 grant 不可变，已有对象被替换时要求恢复原对象
   或新建任务。后续单独设计工作树授权代际和 CAS 确认：展示 task/project/完整路径，
   核对用户打开的旧版本，提交新 grant 并撤销旧任务租约；旧 context 不得自动借用
   新版本。确认后仍不续跑。覆盖同时运行、陈旧确认、跨实例通知、关闭及迟到操作。
4. **严格资源 drain 和元数据固定。** 目前为合作取消与根链持有。增加 task/授权代际
   的资源注册、关闭新入口、有界退出和 cleanup-unknown；保存成功与资源退出分别
   展示。将 Git common-dir/注册槽/指针也纳入身份固定和操作前复核，结合最终名称
   CAS 研究处理内部元数据变更；普通根句柄不能证明整个子树不变。
5. **范围与性能。** 仅生产 Windows 根身份工厂建立本地普通 NTFS 保证；未注入身份
   和持久化端口的兼容宿主仍是旧检查，不得当作安全验收。验证项目删除/重建、跨卷、
   目录 ID 重用及别名，不支持的组合保守拒绝。聚合项目校验和轮询以控制多任务
   Git/SQLite 开销，保持执行前复核，验证长数据库锁、通知乱序和多实例关闭。
6. **原生与系统验收。** 仍需独立 EXE 的旧工作树确认/取消/重开、真实 provider、
   子代理、browser/MCP、Queue/Steer 和定时任务组合验证。MXC 独立 Windows 环境
   尚未就绪，本机不提权、不改 ACL，不复用一次性管理员许可。环境到位后按现有
   MXC 方案验证 shell/Git/PTY 的根外拒绝、继承句柄、子进程及恢复。

整体产品目标未完成。R01 命令可访问任务授权根外文件仍是已确认的阻断问题。
<!-- /source: WORKTREE_OBJECT_AUTHORIZATION_ACCEPTANCE.md -->


---

<a id="worktree-path-safety-acceptance"></a>

## WORKTREE_PATH_SAFETY_ACCEPTANCE.md

<!-- source: WORKTREE_PATH_SAFETY_ACCEPTANCE.md -->
### Managed worktree path and repository boundaries

#### Plan and required self-check

1. Infrastructure owns managed-path and Git metadata validation. Keep the
   Application Git port and App dependency direction unchanged; no UI filesystem
   access or native Windows interop is introduced.
2. Reuse `IGitWorktreeService`, `GitProcessRunner` and the existing owned process
   launcher. Git queries are bounded and mutations remain explicit application
   actions, not a shortcut around the model tool pipeline.
3. Validate canonical absolute paths, authorized repository scope, reparse/link
   ancestry and Git registration before mutation. Keep cancellation, a total
   30-second cooperative deadline, bounded output and ordinary non-force removal.
   Preserve partial/ambiguous directories and never delete a branch automatically.
4. Separate path policy, repository identity and inspection into one-type files;
   keep each handwritten file below 500 lines and each method below 80 lines.
5. Verify real temporary Git repositories, clean/dirty behavior, multiple roots,
   traversal/device aliases, junctions, foreign registration, malformed metadata,
   truncated discovery, cancellation and deterministic discovery/mutation changes.

#### Intended behavior

- Managed targets must be direct GUID-named children of the configured worktree
  directory. Reject the root itself, nested paths, sibling-prefix escapes,
  device/stream aliases, ambiguous Windows names and linked ancestry.
- Do not create unrelated artifact/browser directories as a side effect of
  worktree creation. Validate before directory creation and revalidate before
  invoking a mutating Git command.
- The discovered repository top level must be in an explicitly authorized root;
  selecting only a nested folder cannot implicitly authorize the whole repository
  through a checkout. Explicit additional roots can authorize that repository.
- A standalone `.git` pointer cannot implicitly authorize external metadata.
  Separate Git directories outside the working root require an explicit additional
  authorization. Registered linked-worktree sources retain their existing
  backlink-validated common-directory support.
- Use explicit discovered repository arguments and a captured source HEAD commit
  for creation. A project rooted in a linked worktree must use its HEAD, not
  silently switch to the original repository's HEAD.
- Read bounded ordinary `.git`, `commondir` and registration backlink files before
  native discovery. Compare the returned root/common/private identities with
  that preflight identity. Require complete NUL-delimited worktree registration
  records; malformed, inconsistent or truncated output cannot authorize removal.
- Removal requires a registered linked worktree in the same Git common directory,
  with consistent top-level/private metadata and ordinary, non-linked paths.
  Never remove the project's own primary repository or an ancestor of it.
- Filesystem inspection and Git work run off the UI message pump. Cancellation
  retains process ownership until it returns; errors do not clear saved metadata
  or pretend a retained directory was removed.

#### Validation

Initial candidate: `artifacts/worktree-path-safety-20261018`.
The first test build exposed two xUnit assertion-style diagnostics; after fixing
the fixture, the first boundary run passed 21/27. Four failures came from trying
to overwrite Windows-hidden `.git` files in negative-test setup. Two exposed a
real path-policy gap: Windows canonicalization had already consumed terminal
dots/spaces before the policy examined them. Setup now explicitly clears only
the test-owned pointer's attributes, and production validates raw components
before canonicalization. The next boundary run passed 41/41.

The solution subsequently built with zero warnings/errors. Its complete test
attempt was interrupted: exec handle 80765 disappeared and no candidate-owned
test process remained; `full-test-terminal.json` was not produced. Six completed
TRX reports record 1,896 passes and three failures, but App and Visual reports
are missing, so this is not a completed solution regression. Infrastructure
passed all 433 checks, including all 42 current worktree checks. Two Windows
Git ownership failures still expected a raw cancellation/timeout, contrary to
the retained-directory exception contract. Their fixture also rethrew that
expected recovery exception during disposal. The unrelated PTY failure is a
sharing violation reading its test-owned `child.pid`; no PTY code is changed.
`interrupted-run-observation.json` and the original reports preserve these facts.

Follow-up candidate: `artifacts/worktree-path-safety-final-20261018`.
The eight real Windows Git ownership checks pass after adapting assertions to
the typed retained-directory exception, its exact managed path and inner caller
token/timeout. Descendant-exit and absent-late-write assertions remain, and the
fixture accepts only these expected failures during cleanup. The service and
runner both receive the controllable deadline clock. This changes no production
process-launcher or tool approval behavior.

That candidate's complete App regression is still running on frozen assemblies.
Its pre-test source identity was checked unchanged before archiving all 2,064
source/configuration files to `tested-sources.zip`. Do not describe its result as
covering later changes.

A subsequent source review found a separate plain `.git` pointer gap: the claimed
work root could be authorized while its standalone metadata belonged elsewhere.
`artifacts/worktree-external-metadata-20261018/results/external-metadata-red.trx`
records two expected failures and one passing explicit-authorization control. A
real temporary Git repository successfully created a worktree through the
unauthorized external pointer before the fix; a recording launcher also proved
native discovery was reached. Production now checks that standalone metadata
scope before launching Git. Explicit metadata authorization is supported; removing
that authorization also blocks cleanup before native discovery and retains the
existing worktree. Registered linked-source HEAD behavior remains covered.

#### Current candidate terminal checks

Candidate: `artifacts/worktree-external-metadata-20261018`.

| Check | Terminal result |
| --- | --- |
| Complete Infrastructure suite | 437/437 passed, including 46 worktree cases |
| Related App worktree/preparation/environment suite | 69/69 passed |
| Real Windows Git process ownership suite | 8/8 passed |
| Complete solution build | 0 warnings, 0 errors |
| Changed/untracked handwritten C# shape | 219 files; no violations |
| Changed AXAML shape | 10 files; maximum 499 lines |

The three current test runs cover 514 distinct cases. The 46 worktree cases are
already included in Infrastructure, not an additional run to add to that total.
Handwritten C# remains one type per file; the maximum file is 499 lines and the
maximum method is 72. `results/current`, the build/test terminal JSON files and
the source/binary identity manifests identify the tested candidate. The initial
negative pointer report and previous candidates are retained unchanged.

This candidate has not completed a new full-solution regression. The preceding
candidate's pending complete run uses older assemblies and cannot establish that
the later external-pointer fix passed every suite. Headless App, real disposable
Git/SQLite and owned Windows hook subprocess evidence are not an EXE restart,
real-provider run or user-window acceptance. No user window, credential, approval
setting, commit, push or release was changed. These records do not establish
complete worktree safety or overall Codex feature parity.

#### Explicit remaining limits

- Portable ancestry checks and explicit Git identity reduce exposure but do not
  pin directories with native handles. Hostile replacement during a native Git
  operation is not proven safe; handle-level race protection remains required.
- Restored task tool/panel authorization needs a separate end-to-end audit; this
  creation/removal service is not proof that every persisted worktree path is
  validated before every read or command.
- This is not a sandbox for arbitrary Git hooks, configuration/includes or object
  alternates. A user-authorized Git repository can run hooks during mutation.
- Native restart, hard-crash recovery during creation and branch/base UI selection
  remain separate unfinished work. Ordinary cleanup deliberately retains branches;
  automatic branch deletion is not implemented.
<!-- /source: WORKTREE_PATH_SAFETY_ACCEPTANCE.md -->
