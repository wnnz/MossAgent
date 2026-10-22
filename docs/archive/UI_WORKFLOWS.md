# 界面布局、导航与原生检查

> 历史归档：保留原报告当时的结论、实验、失败与限制。整理于 2026-10-21。
> 当前能力与未完成方案统一维护在[功能清单](../FEATURE_STATUS_AND_IMPLEMENTATION_PLAN.md)；
> 原文中的计划、主机状态、测试数字及“正在进行”等措辞均为历史检查点。
> 代码块和仓库相对产物路径保留原意；本机忽略的 `artifacts/` 不随克隆分发。

[文档入口](../README.md)

## 原报告索引

- [全页面 UI 一致性验收](#all-pages-ui-polish-acceptance) — `ALL_PAGES_UI_POLISH_ACCEPTANCE.md`
- [对话标题栏验收](#chat-header-acceptance) — `CHAT_HEADER_ACCEPTANCE.md`
- [Code preview gutter acceptance](#code-preview-gutter-acceptance) — `CODE_PREVIEW_GUTTER_ACCEPTANCE.md`
- [Compact navigation](#compact-navigation-acceptance) — `COMPACT_NAVIGATION_ACCEPTANCE.md`
- [Compact empty-conversation heading](#compact-welcome-layout-acceptance) — `COMPACT_WELCOME_LAYOUT_ACCEPTANCE.md`
- [Shared content scrollbar acceptance](#content-scrollbar-acceptance) — `CONTENT_SCROLLBAR_ACCEPTANCE.md`
- [文件卡片视觉细节接续](#file-card-fidelity-acceptance) — `FILE_CARD_FIDELITY_ACCEPTANCE.md`
- [Task-owned file previews and rendered diffs](#file-preview-acceptance) — `FILE_PREVIEW_ACCEPTANCE.md`
- [File preview fidelity acceptance](#file-preview-fidelity-acceptance) — `FILE_PREVIEW_FIDELITY_ACCEPTANCE.md`
- [File preview workflow acceptance](#file-preview-workflow-acceptance) — `FILE_PREVIEW_WORKFLOW_ACCEPTANCE.md`
- [检查器概览共享布局与原生对照验收](#inspector-overview-fidelity-acceptance) — `INSPECTOR_OVERVIEW_FIDELITY_ACCEPTANCE.md`
- [Task-owned inspector presentation](#inspector-task-state-acceptance) — `INSPECTOR_TASK_STATE_ACCEPTANCE.md`
- [Narrow workspace layout](#narrow-workspace-layout-acceptance) — `NARROW_WORKSPACE_LAYOUT_ACCEPTANCE.md`
- [原生 DPI 切换诊断准备](#native-dpi-transition-preparation) — `NATIVE_DPI_TRANSITION_PREPARATION.md`
- [Native resize diagnostic progress](#native-resize-diagnostic-progress) — `NATIVE_RESIZE_DIAGNOSTIC_PROGRESS.md`
- [Native resize message progress](#native-resize-message-progress) — `NATIVE_RESIZE_MESSAGE_PROGRESS.md`
- [Native settings interaction progress](#native-settings-interaction-progress) — `NATIVE_SETTINGS_INTERACTION_PROGRESS.md`
- [Navigation action states](#navigation-action-acceptance) — `NAVIGATION_ACTION_ACCEPTANCE.md`
- [文件预览工具栏图标验收](#preview-icon-acceptance) — `PREVIEW_ICON_ACCEPTANCE.md`
- [Settings preview scroll and syntax fidelity](#preview-scroll-syntax-acceptance) — `PREVIEW_SCROLL_SYNTAX_ACCEPTANCE.md`
- [项目编辑弹层优化验收](#project-editor-viewport-acceptance) — `PROJECT_EDITOR_VIEWPORT_ACCEPTANCE.md`
- [项目编辑与原生导航验收](#project-management-acceptance) — `PROJECT_MANAGEMENT_ACCEPTANCE.md`
- [Project prompt responsive acceptance](#project-prompt-responsive-acceptance) — `PROJECT_PROMPT_RESPONSIVE_ACCEPTANCE.md`
- [项目会话预览与展开收起](#project-task-preview-acceptance) — `PROJECT_TASK_PREVIEW_ACCEPTANCE.md`
- [最近会话侧栏验收](#recent-sidebar-acceptance) — `RECENT_SIDEBAR_ACCEPTANCE.md`
- [共享回复正文与渲染精简验收](#response-content-fidelity-acceptance) — `RESPONSE_CONTENT_FIDELITY_ACCEPTANCE.md`
- [Settings catalog viewport acceptance](#settings-catalog-viewport-acceptance) — `SETTINGS_CATALOG_VIEWPORT_ACCEPTANCE.md`
- [Settings dialog reopen acceptance](#settings-dialog-reopen-acceptance) — `SETTINGS_DIALOG_REOPEN_ACCEPTANCE.md`
- [Settings entry and navigation-rail tooltip acceptance](#settings-entry-acceptance) — `SETTINGS_ENTRY_ACCEPTANCE.md`
- [Settings navigation interaction acceptance](#settings-navigation-interaction-acceptance) — `SETTINGS_NAVIGATION_INTERACTION_ACCEPTANCE.md`
- [Shared settings page headers](#settings-page-header-acceptance) — `SETTINGS_PAGE_HEADER_ACCEPTANCE.md`
- [设置页主题及差异预览对照验收](#settings-preview-fidelity-acceptance) — `SETTINGS_PREVIEW_FIDELITY_ACCEPTANCE.md`
- [设置页共享外观对照验收](#settings-shared-appearance-acceptance) — `SETTINGS_SHARED_APPEARANCE_ACCEPTANCE.md`
- [Settings shell alignment progress](#settings-shell-alignment-progress) — `SETTINGS_SHELL_ALIGNMENT_PROGRESS.md`
- [Settings status overflow acceptance](#settings-status-overflow-acceptance) — `SETTINGS_STATUS_OVERFLOW_ACCEPTANCE.md`
- [Settings titlebar navigation acceptance](#settings-titlebar-navigation-acceptance) — `SETTINGS_TITLEBAR_NAVIGATION_ACCEPTANCE.md`
- [Settings typography acceptance](#settings-typography-acceptance) — `SETTINGS_TYPOGRAPHY_ACCEPTANCE.md`
- [Settings viewport focus acceptance](#settings-viewport-focus-acceptance) — `SETTINGS_VIEWPORT_FOCUS_ACCEPTANCE.md`
- [侧栏品牌标题与箭头核验](#sidebar-brand-fidelity-acceptance) — `SIDEBAR_BRAND_FIDELITY_ACCEPTANCE.md`
- [Sidebar compose entry acceptance - 2026-10-10](#sidebar-compose-acceptance) — `SIDEBAR_COMPOSE_ACCEPTANCE.md`
- [Sidebar fidelity acceptance - 2026-10-07](#sidebar-fidelity-acceptance) — `SIDEBAR_FIDELITY_ACCEPTANCE.md`
- [侧边栏分组标题间距验收](#sidebar-heading-acceptance) — `SIDEBAR_HEADING_ACCEPTANCE.md`
- [侧边栏项目拖动与生命周期验收](#sidebar-project-drag-acceptance) — `SIDEBAR_PROJECT_DRAG_ACCEPTANCE.md`
- [Sidebar row reconciliation optimization](#sidebar-reconciliation-acceptance) — `SIDEBAR_RECONCILIATION_ACCEPTANCE.md`
- [Sidebar storage ownership during shutdown](#sidebar-shutdown-acceptance) — `SIDEBAR_SHUTDOWN_ACCEPTANCE.md`
- [侧边栏任务文字与溢出渐隐验收](#sidebar-task-text-acceptance) — `SIDEBAR_TASK_TEXT_ACCEPTANCE.md`
- [Compact interface polish](#ui-compact-layout-acceptance) — `UI_COMPACT_LAYOUT_ACCEPTANCE.md`
- [Shared metadata and approval controls](#ui-metadata-polish-acceptance) — `UI_METADATA_POLISH_ACCEPTANCE.md`
- [Window caption contrast](#window-caption-contrast-acceptance) — `WINDOW_CAPTION_CONTRAST_ACCEPTANCE.md`
- [Window caption fidelity and native evidence (2026-10-13)](#window-caption-fidelity-acceptance) — `WINDOW_CAPTION_FIDELITY_ACCEPTANCE.md`
- [Window caption focus acceptance](#window-caption-focus-acceptance) — `WINDOW_CAPTION_FOCUS_ACCEPTANCE.md`
- [共享工作区外框验收](#window-frame-acceptance) — `WINDOW_FRAME_ACCEPTANCE.md`
- [Window frame shadow progress and native resize failure](#window-frame-shadow-acceptance) — `WINDOW_FRAME_SHADOW_ACCEPTANCE.md`
- [Window sidebar glyph and native replay (2026-10-13)](#window-sidebar-icon-acceptance) — `WINDOW_SIDEBAR_ICON_ACCEPTANCE.md`
- [工作区页面导航与检查器空状态验收](#workspace-navigation-acceptance) — `WORKSPACE_NAVIGATION_ACCEPTANCE.md`

---

<a id="all-pages-ui-polish-acceptance"></a>

## ALL_PAGES_UI_POLISH_ACCEPTANCE.md

<!-- source: ALL_PAGES_UI_POLISH_ACCEPTANCE.md -->
### 全页面 UI 一致性验收

#### 范围

2026-10-17，沿用现有 Codex 风格，不引入另一套配色、图标或卡片体系。
按 ui-ux-pro-max 的表单标签、内容可见性和一致性建议复核：

- 对话页、定时任务页。
- 外观、AI 提供商与模型、网络代理、浏览器、MCP、自定义代理、子代理七类设置页。
- 项目概览、变更审查、终端、浏览器、审计五类检查器页面。
- 提供商、代理、MCP、自定义代理和子代理五类新建弹框。

“全部页面”表示上述页面统一审视并复用共享样式，不表示每页重复加入视觉改动，
也不表示所有有数据、错误、运行中和原生窗口状态均已完成验收。

#### 实际优化

1. 新增共享 `AppSettingsRow`：宽窗口保持标签和输入两列，空间不足时上下排列。
   浏览器的类型、资料模式、代理、资料目录、CDP 地址，以及外观高级字体均复用此组件。
   调整宽度不重建控件，保留输入、选项和焦点；尺寸参数拒绝非有限值、负数和无效零值。
2. 五类设置目录空状态统一为 `AppEmptyState.settings-empty`，图标、标题、说明和间距一致。
   移除提供商空卡片内重复的新建按钮，保留页头的真实新建入口。
3. 浏览器页头和定时任务页头复用共享页头。定时任务保留 20-DIP 标题，窄窗口操作区换行，
   不挤压说明；浏览器表单说明完整换行，不再被固定 240-DIP 列裁切。
4. 普通列表项和标签项统一为 8-DIP 圆角矩形；专用导航、窗口标题按钮等独立变体不变。
5. 设置长页面使用按需滚动条，说明和错误/警告文本共享换行规则。内置浏览器空状态
   使用已有本地化标题、说明和共享组件，不再仅显示一行孤立文字。

#### 设计与安全复核

1. 改动仅属于 App 控件、样式和视图；没有新增跨模块依赖。
2. 复用既有页头、空状态、表单控件和命令。自适应行只是布局，不直接访问文件、SQL、
   进程、网络或模型提供商，也没有服务定位器和新持久化结构。
3. 不改变工具授权、审批策略、超时、取消、任务快照或输出限制。
4. 新手写 C# 每文件一个类型；最终七份相关 C# 审计最大文件 479 行、最大方法 69 行。
   局部外观值保留在共享变体中，视图继续使用包装组件。
5. 新增测试覆盖全页切换、五类弹框的真实键盘打开、三种浏览器资料模式、说明宽度、
   窗口调整后的草稿/焦点保留，以及无效尺寸参数的拒绝路径。

#### 渲染和复现

使用独立 Headless/Skia、临时 SQLite 和本机合成模型夹具。
没有操作、重启或截取用户当前 Codex/MossAgent 桌面窗口，没有付费模型请求。
全页回归明确核对合成模型服务没有收到请求；选择历史任务的正常只读统计保持原行为。

- 明暗主题分别覆盖 526×601、980×700、1440×900。
- 改动前新测试为 6/8：两项失败均复现紧凑浏览器说明需要约 212 DIP、只获 204 DIP。
- 修改后浏览器的隔离、日常资料和已有 CDP 三种模式均检查文字可见性。
- 共享行首次调整窗口测试未等待 Dispatcher 布局任务，产生旧尺寸/行位置误报；
  按既有自适应测试方式等待布局后通过，没有放宽间距断言或改写正常生产布局。
- 紧凑定时任务标题的样式加载顺序已核对，20-DIP 字号有运行时断言，不只检查 AXAML。
- 展开外观高级项并滚动到两类字体行，核对控件未超出窗口、标签与选择器不重叠。
- 本轮明确命名的最终页面截图为 126 份：14 类主页面、5 类新建弹框和高级外观，
  各覆盖两主题、三尺寸；另有两主题下的三种浏览器模式。完整 App 回归的其他截图
  不计入此数字，逐帧文件名保存在 `page-frame-manifest.json`。

原始报告、前后 PNG、全页明暗主题总览和未入仓库的拼图脚本保存于：
`%TEMP%/MossAgent-all-pages-ui-20261017`。
构建输出隔离在 `artifacts/all-pages-ui-20261017`，不覆盖默认调试输出。

#### 验证记录

- 完整解决方案构建：零警告、零错误。隔离输出首次缺少其他测试项目的 assets，
  保留诊断日志，正常还原后构建通过；没有修改项目依赖来绕过还原。
- scoped whitespace 检查通过，最终格式报告为空；七份 C# 单类型/规模审计、相关
  AXAML 规模审计和 diff 检查通过。
- 完整 Visual 程序集：300/300，无跳过。
- 完整 App 首轮：4,893/4,898，五项失败，无跳过或中止，原始报告保留。
- 同步三项旧结构断言后，最终专项：44/44，无跳过或中止。包括本轮新增的 20 项、
  既有提供商行布局和相关结构检查；这是回归子集，不与完整回归累计或冒充完整全绿。
- 最终生产 App、App 测试和 Visual 测试引用的 App DLL SHA-256 一致：
  `1D11AEBCD6E4746E769AD7B7086BBF852C0AFCB8170AB120F9B406EBB4701167`。
  与完整 Visual 和 App 首轮候选相同；后续只同步测试和文档。

完整 App 首轮发现三项与新设计相关的旧结构断言：代理/高级字体选择器的局部右对齐、
隐藏滚动条。断言已同步为共享自适应行的 Stretch 变体和 Auto 滚动条，并保留对组件
所有权、字体行变体和实际布局的检查；最终专项均通过。
另有既有 `HeadlessDispatchUsageTests` 失败，指出未改动的 `ApprovalTaskControlTests` 三处
异步 Action；`AppFlyoutBehaviorTests.InteractiveFlyoutsDoNotDismissDuringMultiStepInput`
也失败，旧断言预期三个内含输入的弹出层、实际扫描到两个。这两项在旧候选
`project-auth-20261016` 的相同守卫专项中同样失败；两项守卫读取当前工作树源文件，
该验证不等同于恢复整个旧源码的干净基线。相关异步测试和弹出层结构未在本轮修改，
也没有放宽无关守卫来制造全绿结果。最终没有再次运行完整 App 程序集。

#### 边界

Headless 渲染不是原生 Windows 输入、DWM、高 DPI、原生目录选择器或当前版本 Codex
同状态逐像素对比。总览拼图经过缩小，仅供页面审视；原尺寸 PNG 才用于细节检查。
本轮没有提交、推送、发布、安装，也没有改变 JIT、JSON、单文件或 Windows x64 策略。
<!-- /source: ALL_PAGES_UI_POLISH_ACCEPTANCE.md -->


---

<a id="chat-header-acceptance"></a>

## CHAT_HEADER_ACCEPTANCE.md

<!-- source: CHAT_HEADER_ACCEPTANCE.md -->
### 对话标题栏验收

日期：2026-10-05。范围：对话右上角的操作区、浮动项目上下文与停靠检查器。
这不是整个应用的 Codex 一比一验收，也不覆盖真实提供商或浏览器执行。

#### 改动与边界

- 右上角包含更多操作、项目上下文与右侧面板切换；新增入口使用现有
  `ToggleRightPanelCommand`，检查器内的关闭入口仍使用同一命令。
- `AppIconButton` 继续拥有图标、点击区域、提示与可访问名称。面板入口的
  “展开”和“收起”标签来自资源，并随面板状态更新，不拼装独立原生按钮。
- 原始像素裁切显示初版图标偏大、上下文多了一行、面板入口图案不同。
  `ChatHeaderVariants.axaml` 将三项操作统一为 32×32 点击区域、10-DIP 图标槽、
  1-DIP 描边，上下文选中背景改为居中的 28×28 圆形。
- 收起时入口显示圆角方框加号，展开后通过按钮的已有交替图标属性显示面板图案。
  分隔线也属于共享变体，而非视图内的颜色和尺寸补丁。
- 展开时隐藏浮动卡片；收起时保留用户此前的卡片隐藏偏好。
  上下文操作仍可从其他检查器标签恢复卡片；没有选中任务时隐藏整个操作区。

约束自检：行为由 App 的现有视图模型所有，外观由共享组件和样式所有；
不新增运行时端口，不改变工具管线、授权、审批、超时、取消或输出策略，
也没有数据库迁移或任务自动重放。新增测试仅声明一个类型；涉及的 C#、
AXAML 与方法均未超过硬上限。`App.axaml` 当前为 500 行，后续扩展需先拆分。

#### 自动化证据

`ChatHeaderInspectorTests` 使用真实 App 样式、主窗口和临时 SQLite 数据：

- 980、1280、1920 宽度，Light、Dark 两种主题下检查三个按钮的大小、
  图标槽与描边、垂直对齐、互不重叠、分隔线和上下文选中背景。
- 检查标题入口的真实命令绑定、状态类、交替图标、提示和可访问名称更新；
  通过控件的空格键路由事件执行标题入口和检查器内的关闭入口。
- 同时覆盖卡片默认显示、显式隐藏偏好、其他检查器标签、未选中任务，
  并检查停靠面板不覆盖输入区。
- 临时移除新增面板按钮后，11 个用例全部失败；恢复后全部通过。

测试夹具显式等待 Headless 异步回调的内层任务，不能只等待外层分派结束。
完整窗口的 Headless 平台按键辅助方法曾触发 Avalonia 静态文字装饰的跨线程
渲染错误，因此自动化按键验证使用控件路由事件，不修改生产文字样式。
真实 Windows 空格键操作另有下述初版运行证据。

最终结果：全量 1,247 个测试通过，解决方案构建零警告、零错误，
涉及 C# 的空白格式检查通过。期间一次构建因隔离预览仍占用 App EXE 而失败；
回收该确认身份的预览后重新构建成功，不将文件锁失败当作代码缺陷。

```powershell
dotnet test tests/MossAgent.App.Tests/MossAgent.App.Tests.csproj --no-restore --disable-build-servers -m:1 --filter FullyQualifiedName~ChatHeaderInspectorTests
dotnet test MossAgent.slnx --no-restore --disable-build-servers -m:1
dotnet build MossAgent.slnx --no-restore --disable-build-servers -m:1
```

#### 原生运行证据与限制

预览使用已完成的合成子代理历史的 SQLite 备份，运行于普通权限。
确认没有 MCP 配置，不提交新任务、不调用保存的已停止测试端点，
也不重放历史工具。截图工具在仓库外，使用 PID、`--passive` 与
`--include-popups`；未使用其点击、置顶或缩放功能。

初版原生预览实际验证了：标题按钮打开面板并隐藏卡片；检查器按钮关闭
后恢复卡片；显式隐藏卡片后重新开关面板仍保持隐藏；聚焦标题按钮后用
空格键关闭面板；Ctrl+Shift+G 打开审查页，再用上下文按钮恢复浮动卡片。
也实际通过视图菜单切换到浅色主题。

原始像素对比后修正图案和尺寸。最终版本获得了 1920×1032 的浅色和深色
被动截图，以及 1280×820 的浅色普通窗口截图。最终版部分原生交互尚未
重验：Windows 操作工具报告 `failed to activate captured window`，重新选择
并绑定当前窗口后仍失败，随后停止点击。未用重启宣称该工具已修复；
清理仅针对本轮启动且已校验 EXE 路径的隔离预览。

截图目录：`%TEMP%\MossAgent-header-qa-8665c3b8a4754e87bc214f132f3cb896`。

| 文件 | 证据 |
| --- | --- |
| `header-expanded-dark.png` | 初版面板展开、卡片隐藏、输入区不被覆盖 |
| `header-hidden-preference-dark.png` | 初版空格键关闭后保持卡片隐藏 |
| `header-final-light.png` | 最终图标与默认卡片，1920×1032 |
| `header-final-normal-light.png` | 最终普通窗口，1280×820；不是面板展开图 |
| `header-final-dark.png` | 最终深色默认状态，1920×1032 |
| `header-native-crop-comparison-final.png` | Codex、初版、修正后的原始像素裁切 |

参考为 `%TEMP%\codex-header-reference-20261005.png`，仅被动获取，没有操作
Codex 界面。裁切移除参考窗口外围的 8 像素边界，不缩放或拉伸任一截图。
参考与合成对话内容不同，因此这些图不能证明全页像素一致；原生关闭动作
也不能仅凭已发送点击或观察超时就认定成功。
<!-- /source: CHAT_HEADER_ACCEPTANCE.md -->


---

<a id="code-preview-gutter-acceptance"></a>

## CODE_PREVIEW_GUTTER_ACCEPTANCE.md

<!-- source: CODE_PREVIEW_GUTTER_ACCEPTANCE.md -->
### Code preview gutter acceptance

#### Scope (2026-10-09)

Horizontal scrolling previously moved line numbers and diff markers out of the
viewport along with long code lines. The shared `AppCodePreview` now keeps its
gutter fixed for both authorized text snapshots and real Git review patches.
This is a shared-component correction, not per-view geometry or a new file reader.

#### Design and safety self-check

1. App owns rendered layout and scrolling over the already authorized in-memory
   snapshot or bounded Git result. No module dependency, persistence schema or
   scheduler/provider behavior changes.
2. Existing file-preview and Git tool paths remain authoritative. Scrolling and
   search do not reread files, execute tools, stage files or invoke model requests.
3. One scroller and one row tree own both axes. Each preview owns its own mutable
   translation; previews cannot move another instance's gutter. Opaque gutter
   surfaces block input on code behind them. Content replacement resets both axes;
   search brings a match into vertical view without changing horizontal position.
   Existing snapshot lifetime, cancellation, late-result rejection and rendering
   limits remain unchanged.
4. All visual elements remain shared wrappers. Styles own gutter layering,
   background and transform bindings. Row states are the single source for added,
   removed, hunk and search colors, rather than duplicating semantic classes on
   children. No event subscription to a global owner is added.
5. Sixteen focused cases cover both themes, plain/diff modes, 300/640-DIP preview
   widths, number/marker positions, actual code movement, search navigation,
   content replacement, multi-preview isolation, opaque hit testing and semantic
   row/gutter alignment. Existing search/preview regressions are retained.

#### Tests and source checks

Evidence root: `%TEMP%\MossAgent-code-gutter-20261009`.

- The initial behavioral red run executes 12 cases: ten fail and two pass.
  `red/gutter-red.trx` records real position/missing-gutter failures. An earlier
  compile-only attempt is preserved separately as `compile-red.log` and is not
  counted as a behavioral reproduction.
- The first shared styling attempt is rejected by the existing markup appearance
  gate. Transform bindings remain in shared Styles rather than weakening that gate.
- A broad preview run then exposes four existing search assertions counting the
  duplicated child semantic classes. Keeping semantic classes only on the row and
  deriving the gutter surface from ancestor selectors fixes the composition, not
  the existing assertions. The final focused preview filter passes 145/145 cases,
  including all 16 additions, in `focused-final/preview-focused-final.trx`.
- `dotnet format whitespace --verify-no-changes` passes for the four scoped C#
  files. The source check finds one type per file; they are 68, 57, 82 and 79 lines.
  Component/style markup parses as XML and remains 30/176 lines. Solution and
  separate ordinary native QA builds report zero warnings/errors.
- The first full run passes 3301/3302: the existing workspace cancellation test
  throws `ObjectDisposedException` for `SQLitePCL.sqlite3` while creating its
  synthetic task. An isolated rerun of all five cancellation-flow cases passes.
  No unrelated SQLite implementation or fixture is changed. The final identical
  full command passes 3302/3302 with zero failures/skips across seven independent
  TRX reports: App 2499, Tools 188, Providers 283, Infrastructure 136, Browser 76,
  Agent 63 and Platform.Windows 57. Both initial and final logs are retained in
  `full/`, `full-final/`, `full.log` and `full-final.log`.

#### Native interaction and pixels

The standalone QA host uses the real App startup/shutdown and production tool
executor, separate SQLite/project data and an actual temporary Git repository.
A nested Unicode file contains 93 lines, including long synthetic C# declarations.
It is untracked and reviewed ReadOnly, then opened through the authorized snapshot
workflow. No paid provider, user file, user database or user settings participate.

- A separately built pre-change Light 1280x820 host reproduces disappearing line
  numbers using native horizontal input. Its baseline image is
  `before-Light-1280-820/window-horizontal.png`.
- The final Light 1280x820 and Dark 980x640 hosts run under ordinary permissions
  with render scaling one. Actual horizontal and combined horizontal/vertical
  input keeps numbers/markers fixed and aligned while code moves. Both Git review
  and ordinary file snapshot presentation are checked, including the compact
  dark toolbar. Backgrounds remain opaque and match added/plain surfaces.
- In Light, native mouse input opens and focuses the real search input, then types
  `Value60`. The match is brought to line 62 and highlighted across its gutter and
  content; the horizontal position remains unchanged. The draft is continuously
  checked by the QA host and is not consumed by review, preview or search.
- Both final Light and Dark hosts record four successful ReadOnly audits and exit normally
  with `QA_INDEX_UNCHANGED: true` and `QA_EXIT: 0`. The index check compares cached
  diff content, not byte identity of Git's index. Baseline selection replacement
  cancels one initial read; it is not a Git-result or shutdown failure.
- Passive Python PrintWindow checks non-black output and exact native dimensions.
  `code-gutter-before-after.png` compares 414x427 native-size Light code-pane crops
  at the same horizontal-scroll state without scaling. It is MossAgent before/after,
  not a same-state Codex reference. Capture/comparison Python, QA sources and
  synthetic data stay outside the repository; QA binaries are ignored artifacts.

#### Remaining limits

Same-state Codex review/preview screenshots are still required for final fidelity
acceptance. Native higher DPI, user-font substitutions, keyboard selection/copy
across lines and all hover/focus states are not newly verified by this slice.
Diff metadata/hunk text retains its existing horizontal-scroll behavior; only
the line-number/marker gutter is fixed. Split diff and virtualized large-file
rendering are not introduced. Existing parser/output limits still apply.

Managed/JIT development and Windows x64 single-file publishing remain unchanged.
No commit, push, publish or staging operation is performed.
<!-- /source: CODE_PREVIEW_GUTTER_ACCEPTANCE.md -->


---

<a id="compact-navigation-acceptance"></a>

## COMPACT_NAVIGATION_ACCEPTANCE.md

<!-- source: COMPACT_NAVIGATION_ACCEPTANCE.md -->
### Compact navigation

#### Design and required self-check

The fresh passive Codex reference is a 526x601 conversation with a collapsed
sidebar. Before this slice, MossAgent's 980x640 window minimum prevented that
state. Both shells independently reserved a fixed 264-DIP sidebar; simply
lowering the window minimum would have hidden primary actions and settings.

1. App owns a shared navigation-pane layout control consumed by both shells.
   No dependency, platform integration, persistence or provider change is needed.
2. Reuse the actual sidebar and primary controls, existing selection commands,
   shared surfaces and the window's sidebar toggle. Preserve the wide-window
   visibility preference separately from temporary compact drawer state.
3. Layout and navigation invoke no tools or model requests. An open drawer blocks
   underlying pointer/keyboard input; dismissal restores eligible prior focus.
   Escape, outside click and explicit navigation selection close only the drawer.
   Hidden, disabled and detached hosts cannot execute stale toggle commands.
4. Use one type per handwritten C# file and shared appearance variants. Do not
   recreate settings editors, conversation controls or drafts during resize.
5. Cover exact layout boundaries, both themes, preference/draft/focus retention,
   selection and dismissal, disabled/hidden/detached commands and real consumers.
   Run related tests and a solution build, then native screenshots and replay.

The shared compact breakpoint is 264 + 360 = 624 DIP in the navigation host,
not a claimed Codex breakpoint. MainWindow now permits 526x601 while keeping
the default 1280x820 size. Wide sidebar preference remains independent of the
temporary compact drawer. Both real shells use the same control and the same
toolbar toggle; the primary/editor instances are not recreated during resize.

AppResponsiveColumns stacks the actual automation list and history below its
300 + 260 + 20 DIP breakpoint, retaining both scrollable views. Shared settings
dialogs constrain both dimensions to the host minus their 24-DIP margins. The
existing extent converter is renamed to describe its width/height use, including
approval cards; approval policies, commands and persistence remain unchanged.

#### Execution evidence

External evidence is stored at
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-compact-navigation-20261014`;
the passive Python capture/comparison and independent launch/audit tools are
outside the repository. Debug/JIT, JSON adapters and single-file release settings
are unchanged. No commit, push or release is performed.

- Initial real-consumer tests exposed an unconstrained provider dialog and an
  incorrectly injected Escape on the window rather than its focused control.
  The dialog now fits its actual host. Routed keyboard input plus retained
  primary focus cover the production toolbar, not just direct command execution.
- An additional real keyboard case exposed Escape from the toolbar while the
  drawer is open. The attached host now handles unconsumed root Escape and
  unsubscribes on detach. Outside left click, Tab containment, unavailable hosts,
  replacement, dynamic metrics and retained expansion preference are covered.
- Initial compact pixel checks sampled a palette card instead of the empty
  page margin and assumed integer DIP bounds at 125 percent scaling. The final
  guards compare primary width with its actual host and sample empty margins;
  exact palette checks and all pre-existing pixel guards remain intact.
- Ordinary-permission native App PID 4792 verifies 526x601, 96-DPI light/dark
  settings/dialogs, workspace drawer/Escape, settings selection/outside dismissal,
  the compact automation list/history and the scrollable editor with fixed
  footer. Returning to 1920x1032 restores the wide sidebar and side-by-side
  automation history. No plan is saved, enabled or executed during this replay.
- App and observer exit zero after ordinary close. The final read-only audit
  matches all nineteen table fingerprints, three original artifact checks,
  four-request history and restored-Light UI bytes; observer port 51792 is free.
- The first complete App run passes 4,316 cases and fails three old fixed-grid
  source guards. They now assert shared navigation ownership, actual-visibility
  tint binding and active-shell toolbar routing. The window geometry guard moves
  out of the formerly 494-line file rather than exceeding the file limit. The
  remaining palette, rail, titlebar, frame and dialog assertions are retained.
- Final `verified/gate-exit.json` exits zero: all 86 related cases, 72 pixel cases,
  seven architecture cases and the complete 4,319-case App run pass with no
  failures/skips/aborts. Six complete non-UI groups pass 1,538 cases; their DLL
  hashes stay unchanged after the final build. `verified/verified-checkpoint.json`
  independently checks TRX counters, eight assembly identities and native audit
  ownership: 5,929 total passes, excluding duplicate related/architecture subsets.
  Scoped formatting, zero-warning/error solution build and full diff whitespace
  checks pass. The final App hash matches the native replay's candidate.

`compact-native-reference.png` contains unscaled 526x601 native Codex and
MossAgent screenshots. Their conversations/context states differ; it proves the
new matching viewport, not complete page or component pixel equality. Codex is
only passively captured. The end-of-run `codex-end-passive.png` is a valid
600x750, 96-DPI conversation capture, still not an equivalent Appearance page.

The separate native DPI/clipping issue, equivalent Appearance/PI-Desktop pages,
full same-state surface matrix and overall product objective remain open.
<!-- /source: COMPACT_NAVIGATION_ACCEPTANCE.md -->


---

<a id="compact-welcome-layout-acceptance"></a>

## COMPACT_WELCOME_LAYOUT_ACCEPTANCE.md

<!-- source: COMPACT_WELCOME_LAYOUT_ACCEPTANCE.md -->
### Compact empty-conversation heading

#### Reproduced defect: 2026-10-14

Ordinary-permission native Python captures of the previous production App show
the same defect in both themes at 526x601 and 96 DPI: the selected empty task's
three-line welcome heading extends underneath the composer context strip. The
brand icon also intersects the floating project-context card. These are layout
defects, not evidence that the separate native DPI/geometry issue is repaired.

The preceding native surface matrix also exercises Appearance, provider/model,
browser, proxy, MCP, agent and subagent settings, retained/cancelled model drafts,
empty-form validation and inspector surfaces. It runs against the earlier App
hash, not this fix; it is retained as prior-version evidence only. No real model,
tool, terminal or browser execution is added by either matrix.

#### Design self-check

1. App's shared layout controls own this behavior. Module references and all
   Domain/Application/infrastructure/platform contracts remain unchanged.
2. AppProjectPrompt keeps its bindings, retained project button, command, tooltip,
   accessible name and flyout. Its layout panel measures styled content: one row
   when it fits, prefix/project plus suffix on two rows otherwise, and a bounded
   three-row fallback only when even the prefix cannot share a row. Long names
   use the existing ellipsis rather than making an unnecessary third row.
3. AppEmptySessionLayout replaces the view's centering StackPanel. Shared styles
   still own the preferred 32-DIP brand offset. The panel accounts for this offset
   in arrangement and reduces the gap only when the available height requires it.
   Sufficiently tall viewports preserve the previous brand and heading positions.
   No context card is hidden, font reduced, native window size forced or DPI
   message intercepted to conceal the problem.
4. Each new handwritten C# file contains one type and remains below 250 lines;
   methods remain below the hard limit. No subscription, cache, package, service
   locator, native control bypass or persistence migration is introduced. This
   is Debug/JIT; Windows x64 single-file publishing and JSON behavior are unchanged.
5. MainWindow tests cover actual card/composer/brand bounds, two sizes, both themes
   and short/80-character names, without provider requests or selection changes.
   Component tests retain repeated resizing, matching row centers, non-overlapping
   fragments, long names, Tab/Space, disabled/missing commands, flyout and tooltip
   identity. Additional tests cover odd widths, the extreme-width fallback,
   compact brand containment, exact tall-viewport positions and invalid offsets.

Authorization, approval policies, task cancellation, timeouts, path validation,
output limiting and storage remain outside these synchronous layout components.

#### Test development and limits

- The first eight red results used the stretched InspectorPane host as the card
  boundary; they are not accepted as a product regression baseline. After locating
  the visible inspector-surface AppCard, the unchanged product fails six cases and
  passes two. Four failures expose the actual composer overlap; two long-name
  cases expose the brand/context overlap.
- Old Grid-definition assertions are replaced with actual row/bounds checks,
  retaining adaptive-pane measure/arrange and command safety coverage. Intermediate
  failures remain in the external logs. A real one-pixel row overlap is fixed by
  rounding the shared row origin and child slots on the actual Avalonia layout
  scale. Tests also drain queued resize work before asserting rendered geometry;
  changing a Window property alone is not proof its actual bounds have settled.
- The initial focused run passes 40 cases with zero failed/skipped cases. Complete
  pixel regressions pass 84. Solution build has zero warnings/errors and scoped
  whitespace-format verification passes. Complete App regression is recorded in
  the final checkpoint below; focused cases must not be counted twice.
- The first complete App run passes 4,387 cases and fails one old source guard
  requiring StackPanel Spacing and an icon RenderTransform selector. The corrected
  guard requires the shared layout, style-owned brand offset, no view override,
  retained dedicated icon and unchanged active-conversation header divider.
  This first complete run is not accepted as a passing checkpoint.
- The final focused run includes the related shell source guards and passes all
  53 cases. The production App hash remains unchanged after the guard-only update.

#### Native replay

The final independent App assembly SHA256 is
`A3B7F0C56C86B1E720A8D54C19A7B7D67C357A1014A7169F9E088BB77430FAFA`.
The normal-permission test instance uses only a fresh synthetic database copied
from a closed fixture; no user database or default launch output is replaced.

- Passive Python captures verify light 1920x1032, restored 1280x820, and real grip
  resizing to 980x640 and 526x601. Both themes at 526x601 show the complete two-row
  heading above the composer and the full brand below the context card.
- Clicking the retained project name opens the real picker at the minimum size;
  Escape dismisses it without changing projects. Native long-name interaction is
  not rerun here; it has deterministic real-MainWindow bounds coverage.
- Dark-theme maximize to 1920x1032 restores the single row; native restore to
  526x601 restores two rows. A transient null UIA result after maximize is handled
  by fresh window selection and observation, not a repeated maximize click.
- Restoring Light produces unchanged audits of all nineteen tables, three stored
  artifacts, the existing four-request history and UI-setting bytes. Titlebar
  close terminates App and observer normally with exit zero; both processes and
  the loopback listener disappear. The final closed-fixture audit also passes.
- Unscaled light/dark before/after comparisons retain both 526x601 frames. They
  demonstrate this MossAgent fix, not Codex pixel equivalence. Fresh passive Codex
  remains a maximized light conversation, not Appearance or an equivalent empty
  conversation; no Codex UI input is automated.
- At 1920x1032, the unscaled `(800,380)-(1400,545)` brand/heading crop has zero
  changed pixels against the preceding production assembly. This verifies the
  existing wide welcome appearance, not other areas or Codex equivalence. The
  earlier frame is named light-settings-max but visibly shows the workspace;
  it is used only as workspace evidence, never accepted as an Appearance capture.

Raw captures, scripts, fixture audits and tests remain outside Git:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-compact-welcome-20261014`.
Earlier surface captures and intermediate/red tests remain at
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-ui-matrix-20261014`.
The final build is isolated under `.scratch/compact-welcome-20261014`.

#### Final checkpoint

The final complete App run passes 4,388 cases, complete pixels pass 84 and the six
complete non-UI suites pass 1,538: 6,010 across eight assemblies, without counting
the 53-case focused subset twice. All final drivers exit zero; reports contain no
failed, skipped, aborted or unexecuted cases. External checkpoint verification
binds TRX and assembly hashes to the native App hash, checks all seven native
capture identities/geometries, recomputes the unchanged wide crop, and verifies
restored data, normal cleanup and clean build/format/whitespace results. The
machine-readable record is `verified-checkpoint.json` in the external evidence
directory. The initial failed complete report remains separate and is not merged
into this passing checkpoint. Full-product acceptance, same-state Codex
settings/reference coverage, actual DPI transitions and native geometry diagnosis
remain open. Nothing is staged, committed, pushed, published or installed; the
overall product goal stays active.
<!-- /source: COMPACT_WELCOME_LAYOUT_ACCEPTANCE.md -->


---

<a id="content-scrollbar-acceptance"></a>

## CONTENT_SCROLLBAR_ACCEPTANCE.md

<!-- source: CONTENT_SCROLLBAR_ACCEPTANCE.md -->
### Shared content scrollbar acceptance

#### Scope and design, 2026-10-12

- App owns this presentation-only refactor. No new port, service or native
  wrapper is needed: existing AppScrollViewer/AppListBox templates retain their
  framework ScrollBar, Track, Thumb and RepeatButton parts. Thumb visuals use
  AppBorder, respecting the shared visual-component boundary.
- ScrollBarVariants centralizes transparent tracks, hidden line buttons,
  identity transforms and six-DIP rounded visuals in twelve-DIP hit areas.
  Navigation and terminal styles retain their own palette, not duplicate chrome.
  Diff previews retain sixteen-DIP horizontal bars and visible arrow buttons.
- Native auto-hide/overlay behavior is preserved. Terminal and diff preview
  variants still disable auto-hide explicitly; settings-page bars remain hidden
  without disabling wheel input. No authorization, approval, execution, timeout,
  cancellation, persistence, concurrency or JIT/publish-policy change is made.
- All affected C#/AXAML files remain under 500 lines; the added test file contains
  one type. No application code reads user files or starts processes for this UI
  change. Native validation uses the production App with its existing
  MOSSAGENT_DATA_DIR override and a copied synthetic database, never user data.

#### Baseline evidence

- Actual production Debug App and Codex windows are captured passively with the
  existing external Python PrintWindow script at 1920 x 1032, 96 DPI. The original
  App renders two-pixel Fluent grey thumbs rather than the reference rounded
  six-pixel thumb. Codex's measured resting light colour is RGB (228,228,229);
  the original App's resting colour is RGB (122,122,122).
- Initial component tests run against unchanged production styles: eight of
  sixteen cases fail because the expected shared thumb visual is absent; disabled
  and fitting-content cases already pass. The earlier build-lock and missing-test-
  import failures are not counted as behavioural baselines.
- A first shared-style replay exposes six sidebar width regressions caused by
  changing the default auto-hide mode. Removing that default-mode change fixes
  the implementation rather than loosening the existing geometry assertions.
  The corrected focused slice passes 93/93 before adding theme-switch/list tests.
- Full-window sidebar integration identifies the shared vertical thumb class
  instead of the retired sidebar-only template class. All existing width, corner,
  edge alignment, overflow and scroll-position assertions remain unchanged.

#### Evidence location

Logs, TRX files, metadata hashes and passive captures are outside the repository:
`%TEMP%\MossAgent-production-ui-20261012-164046`.
The Python capture and metadata helpers are not repository changes.

#### Final validation

- Twenty new cases cover both axes/themes, resting/hover/pressed geometry, drag,
  page and wheel input, disabled input, fitting content, in-place theme changes
  and thin-list navigation colours. The expanded final component/window/catalog
  slice passes 129/129; this includes the unchanged allocation test class replay.
- The intermediate sequential solution run executes 5,466 cases, with 5,461
  passes and five failures. Four are the subsequently corrected template-class
  integration selector; one is the unchanged provider-catalog allocation assertion
  (6,632 bytes instead of zero). All five affected cases pass in the final focused
  replay. The full-run log retains every assembly's counters; its explicitly named
  TRX file is overwritten between assemblies and only represents the final assembly.
  No second clean full-solution run or allocation-failure cause is claimed.
- The solution build passes with zero warnings/errors. Scoped dotnet whitespace
  verification, XML/whitespace checks and Roslyn source auditing pass. App.axaml
  is 494 lines; the new shared style is 63. The two changed C# test files contain
  one type each and have 199/150 lines, with maximum method sizes of 38/34 lines.
- The rebuilt production App starts normally without elevation, with its real
  composition root and no substituted runtime service. Binary hashes identify
  this build separately from the baseline. The 1920 x 1032 light capture measures
  a six-pixel thumb with RGB (228,228,229), matching the measured Codex width/colour.
  Thumb length differs with content and the App's right-edge position differs by
  one pixel; this is not a claim of whole-window pixel parity.
- Activation restores the App to 1280 x 820. Real main scrolling changes the
  exposed offset from 1267 to 1079.5. Child-detail content and its thumb then move
  independently while the main offset stays unchanged, verified by captures;
  its UIA value remains zero, so no native numerical child-offset claim is made.
- Actual appearance settings switch dark then light without replacing the window.
  Dark content thumbs measure six pixels with RGB (64,71,77); no current Codex dark
  reference or high-DPI comparison is available. Settings still hide their bar
  while wheel input moves the page; diff preview arrow buttons remain visible.
- The observed window closes normally and its process disappears; the external
  observer does not obtain an exit code. After closure, hashes and counts of the
  synthetic task, eleven messages, six tool executions and artifact records are
  unchanged. No message is sent or tool operation replayed during this UI check.
  User data, JIT debugging, Windows x64 single-file publishing and unrelated
  working-tree changes remain untouched. No commit, push or release is performed.
<!-- /source: CONTENT_SCROLLBAR_ACCEPTANCE.md -->


---

<a id="file-card-fidelity-acceptance"></a>

## FILE_CARD_FIDELITY_ACCEPTANCE.md

<!-- source: FILE_CARD_FIDELITY_ACCEPTANCE.md -->
### 文件卡片视觉细节接续

#### 当前缺口与分阶段安排

上一轮已接通可信快照统计、可访问路径名称及撤销/历史恢复，完整回归 4785/4785。
原始像素并排截图仍显示路径字体、“再显示”入口和剪贴板图标不同，不能据此
宣称视觉一比一。本轮先量测截图，调整共享外观，再验证布局、像素及真实交互。

1. 被动获取当前 Codex 参考，对照上一轮卡片及原生 MossAgent；不操作 Codex UI。
2. 字号、前景色、图标大小/几何和展开入口间距只在共享 FileChangeVariants 中修正。
3. 保持卡片宽高、任务范围、预览/撤销/展开命令与安全管线，补窄宽明暗及像素测试。
4. 独立普通权限原生窗口复测，保存 Python 截图，完成专项、全量构建及测试。

#### 设计自检

1. App 的共享控件/样式拥有视觉细节，不改变模块依赖或业务页的外观边界。
2. 复用 AppFileChangeSummary、AppActionButton、AppIcon 与现有命令，不另建文件功能。
   AppActionButton 新增可样式化的 TrailingIconSpacing，默认仍为 3；只有卡片
   展开入口设为 10，不移除所有按钮的现有本地间距或改动其他外观变体。
3. 不调整授权、审批、超时、取消、输出或持久化。只在仓库外合成数据上操作，
   日常 Codex 仅用 Python --passive 截图；不执行删除或强制退出。
4. 所有手写 C# 单类型、文件和方法遵守硬限制；不扩大 App.axaml 或 WorkspaceViewModel。
   增删行颜色使用卡片专属主题资源，浅色按截图量测，深色保留既有语义颜色；
   不修改其他状态提示使用的全局成功/错误颜色。
5. 断言字形、字号、图标实际布局、截断与点击范围，验证长路径、未知统计、明暗及缩放。
   真实预览和展开仍使用任务拥有的命令，原生重载不重放模型或写入。

#### 验证记录

2026-10-11，证据根目录：
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-file-card-fidelity-20261011`。

##### 当前参考与共享外观

- `codex-reference-current.png`：当前 Codex 普通窗口的 1920×1032 被动截图；
  未自动化点击、切换主题或操作 Codex。参考卡片仍显示真实文件统计及展开入口。
- 路径、展开文字和标题操作改为 13；统计保持 12。行高 36、标题操作高 28、
  图标槽 40、圆角 12 和原命令保持不变。展开箭头为 8、尾随间距为 10。
- 参考摘要图标为独立加/减号文档，不是上一版的大剪贴板加号。
  `glyph-pixel-metrics.json` 按灰度阈值 160 量测 40×40 图标槽：参考像素轮廓
  为 [11,11,28,28]、18×18；上一版为 18×20；最终同为 [11,11,28,28]、18×18。
  初次修正的 16×16 量测留在 `glyph-pixel-iteration.json`，最终控件 Size 为 18。
- 浅色统计采用参考中实心像素的 #00A240 / #BA2623，使用卡片专属主题资源。
  深色保留 #4ADE80 / #F87171；没有修改其他状态提示的全局语义颜色。
  这不等于所有字形、抗锯齿、颜色或全页像素均一致。
- `file-card-comparison.png` 按原始像素并排整理当前参考、上一版、最终版卡片，
  未缩放；内容和窗口宽度不同，不能用它证明整个窗口一比一。

##### 自动验证

- `focused-verified`：125/125，包含新增 11 项卡片测试、既有文件变更及共享图标
  测试。明暗 280/740 宽度验证路径/统计/箭头中心、真实展开命令、截断与边界，
  展开后在同一窗口切换主题验证专属统计资源动态更新。
- 光栅测试在 1、1.25、2 倍验证独立加/减号、紧凑外轮廓和不越入外围区域。
  尾随间距测试验证普通按钮默认 3、卡片 10、本地覆盖及恢复、负值/NaN 拒绝；
  不通过删除所有按钮的既有本地样式来改变其他入口。
- `solution-verified-build.log`：0 警告、0 错误。
- 最终 `full-final` 的 8 份 TRX 独立计数：Agent 83、App 3461、Analyzers 8、
  Browser 76、Infrastructure 171、Windows 137、Providers 377、Tools 483；
  合计 4796/4796，失败和跳过均为 0，命令退出 0。
- `format.log` 限定 3 个相关 C# 文件的格式验证退出 0。
  `source-audit.log`：每文件一类型、132/166/123 行，最长方法 60 行；
  FileChangeVariants 为 123 行，XML 有效，无语法错误或尾随空白。
- 初期编译和测试失败未删除：缺少资源扩展的命名空间，未指定主题导致查询
  返回 Unset，以及动作断言误包含隐藏的撤销通知按钮。最终按两个真实标题命令
  选择入口并保留严格 28 高度断言。中间 `full` 不覆盖最后的图标/配色修正，
  不计作最终版本验收。

##### 普通权限原生验证

- 独立 SQLite、合成既有文件及 loopback 模型；首次真实工具写入仅 4 次，
  本轮不触碰日常项目文件，不执行删除或撤销。
- 最终浅色 1280×820、深色 980×640，缩放 1，均为 `QA_ELEVATED=False`。
  最终运行仅加载历史、模型请求 0，卡片总计 +4/-4，默认 3 行、点击后 4 行。
  宽窗口卡片实际 740×209，展开后 740×211，保留真实文件路径可访问名称。
- 点击浅色文件行打开真实只读快照补丁，PreviewReady 为 true；右栏打开时
  上下文卡片隐藏。深色窄窗口通过既有上下文按钮隐藏卡片后检查标题及展开。
  截图、UIA 文本及原生字号/颜色/图标布局元数据分别保留。
- `store-final.json` 只读检查：写入成功审计与文件变更记录均仍为 4，历史预览
  成功 1，四文件字节匹配。28 次统计查询中 3 次为任务加载替换的 tool_cancelled，
  最终统计完整、无错误状态，不把正常取消计作全部查询成功或写入重放。
- `native-light-verified.log`、`native-dark-verified.log` 均正常退出、请求 0、
  无 QA_FAILURE。Computer Use 曾报告无截图几何；重新绑定并观察截图后仅重试
  一次，未改用注入脚本或强制退出。交付检查无 QA/testhost 进程，暂存区为空。

##### 未完成范围

完整界面一比一、原生其他 DPI、深色 Codex 参考以及提供商/子代理完整产品
验收不由本轮证明。继续保留整体目标；保持 JIT 调试、Windows x64 单文件配置，
没有暂存、提交、推送或发布。
<!-- /source: FILE_CARD_FIDELITY_ACCEPTANCE.md -->


---

<a id="file-preview-acceptance"></a>

## FILE_PREVIEW_ACCEPTANCE.md

<!-- source: FILE_PREVIEW_ACCEPTANCE.md -->
### Task-owned file previews and rendered diffs

#### Scope (2026-10-08)

The inspector opens source attachments, a selected changed file and audited task
artifacts as read-only tabs. Tabs are selectable, closable, refreshable and bounded
to eight. The selected tab scrolls into view, including its close action, inside a
narrow inspector. Returning to the inspector retains tabs; changing tasks or their
authorization scope cancels pending reads and clears content.

UTF-8 text, PNG and baseline/progressive JPEG have real previews. File previews
show line numbers and existing theme-aware syntax tokens. Unified Git diffs show
old/new line numbers, addition/deletion backgrounds, prefixes and hunk headings.
Git envelope and line-ending warnings do not displace the actual changed lines.

#### Design and safety self-check

1. App owns observable tabs and shared visual components. Tools.BuiltIn owns the
   preview tool, Application defines the task-artifact reader port, Infrastructure
   reads and verifies snapshots, and Tools.Abstractions owns bounded format rules.
   No lower module depends on UI. Window chrome actions are extracted to avoid
   extending the already large main window view model beyond its hard limit.
2. Original files use `filesystem.preview_file` through the existing tool executor,
   schema/preflight/capability/approval/timeout/cancellation/output/audit pipeline.
   Audited artifacts use the task-artifact reader after matching the selected task.
   The UI decodes returned memory, never a filesystem path.
3. Preflight canonicalizes the original path against task-authorized roots and
   rechecks it after reading. Snapshot creation rejects existing artifact-directory
   links before creating directories. Readers reject paths outside the task's
   artifact directory, links, changed lengths/hashes and mismatched media types.
   Text is capped at 128 KiB; all snapshots at 2 MiB. PNG/JPEG dimensions are capped
   at 8,192 per axis and eight million pixels. Reads request cancellation after
   30 seconds; noncooperative late completions cannot publish to closed/replaced tabs.
4. Handwritten C# remains one type per file, with scoped AST size checks. Shared
   styles own tab/action geometry, breadcrumbs, image fit, code gutters and diff
   colors. Markup and analyzer gates reject component composition/geometry replacement.
5. Focused tests cover pipeline schema/approval/cancellation, path rejection,
   unsupported/oversized data, safe dimensions, task ownership, changed snapshots,
   junction escape, late completions, equivalent context reattachment, tab limits,
   parser bounds, gutters, themes and component policy. No paid model is invoked.

#### Native evidence

Evidence and Python scripts live outside the repository at
`%TEMP%\MossAgent-file-preview-20261008` and the existing automation QA directory.
The native QA host uses separate synthetic data with paused schedules.

- Passive Python captures inspect light 1280x820 text/diff views and dark 980x640
  PNG preview. Captures assert dimensions and reject black images.
- The first native capture exposed missing back/close icon resource names and an
  active image tab clipped outside the visible strip. Correct resources and owned
  bring-into-view behavior correct both, including the active close button.
- The diff screenshot uses real git.status/git.diff execution against a separate
  synthetic repository, rather than a hard-coded success result.
- Native Ctrl+P switches from preview to audited execution records without replacing
  the draft. Mouse input reports `coordinate input geometry is unavailable`; native
  mouse/hover actions are not accepted by this evidence.
- Final full-solution regression passes 2,925 tests: Agent 58, App 2,222, Browser 76,
  Infrastructure 124, Windows 50, Providers 241 and Tools 154. Final solution and
  ordinary Debug App builds have zero warnings/errors. The 26-file scoped AST audit
  has zero violations; scoped whitespace/diff checks pass. Logs are in the evidence
  root (`regression-final-2.log`, `ast-audit.log`).
- The ordinary, non-elevated managed/JIT application is normally closed while idle,
  rebuilt and restarted; its user's empty draft/data are not replaced by QA data.
  A foreground-only Python capture verifies the actual 1536x816 screen pixels.
  A later 1920x1032 QA request returns different native bounds and is rejected by
  the screenshot size guard. That wide capture is not fidelity evidence; window/DPI
  state must be remeasured rather than scaling or accepting a clipped frame.

#### Remaining limits

This is not a claim of one-to-one Codex fidelity. The passive Codex reference has
an image-preview panel with a richer toolbar; matched-state Codex diff and automation
screenshots are still needed. External editor integration, split diff,
full language grammars and editing in preview are not implemented.
Unsupported formats fail safely. Rendering is capped at 2,000 lines and 128 KiB;
line selections are independent, not a full editor-wide selection model.

Snapshots retain their audited task artifacts. Preview refresh reads a fresh original
file; refreshing an audited artifact rechecks the immutable recorded length/hash.
No data migration, paid-provider acceptance, packaging, AOT or JSON-source-generation
change is part of this phase. Development remains ordinary managed/JIT.

#### Search and image interaction (2026-10-08)

- Each tab owns a literal, ordinal-ignore-case search, bounded to 256 query
  characters and the same 2,000 lines/128 KiB as rendering. Counts identify
  matching lines, not individual occurrences. Enter/Shift+Enter wrap through
  matching lines and scroll the highlighted current row into view. Ctrl+F opens
  and focuses search while focus is inside the preview; Escape closes it.
- Switching tabs preserves their individual search state. Refresh, task/scope
  replacement and disposal clear it. Loading, image and disposed tabs cannot
  open search; no search action invokes a tool or reads another file.
- Image tabs own fit, actual-size and 10%-400% zoom state. The shared image
  viewport maintains aspect ratio, recomputes fit on resize and scrolls oversized
  images. A replaced image resets fit; loading/closed tabs disable zoom actions.
  Zoom starts from 100% when leaving fit mode, not an estimated fit percentage.
- App owns these presentation-only behaviors over the existing authorized
  snapshots; no port, permission, pipeline or persistence change is required.
  Handwritten types remain separate; new image composition is covered by the
  same markup/analyzer boundary. Focused tests cover literal/empty/no-match/bounded
  searches, wrapping, stale tabs, lifecycle, keyboard focus, row scrolling,
  narrow two-theme layouts, zoom limits and resize.
- Passive Python native captures are stored outside the repository in
  `%TEMP%\MossAgent-preview-interactions-20261008`: light 1280x820 search/editor
  and dark 980x640 image fit. A foreground screen-pixel capture confirms native
  Enter selects the second match and scrolls to line 68. UI Automation returns
  stale counts after input; pixels, not that stale tree, establish the outcome.
  A malformed shared refresh geometry seen in those captures is corrected.
- Native Shift+Enter, Escape and Ctrl+F are separately captured: navigation wraps
  back, Escape removes the search strip/highlights and Ctrl+F refocuses the retained
  query. Dark 980x640 zoom and final light 1280x820 fit captures verify actual
  scaled content and the corrected circular refresh icon. The final solution and
  ordinary JIT builds have zero warnings/errors. All 2,938 solution tests pass;
  after the final icon correction and stronger horizontal-scroll/source-reset
  assertions, all 30 focused tests pass again. The 14-file scoped AST audit reports
  zero violations. The ordinary idle app is normally closed and restarted without
  replacing user data; its actual foreground frame is 1280x820 at DPI 96.

This does not accept matching Codex automation states, native mouse/hover input,
external editor integration, whole-editor selection, occurrence-level highlights
or real paid-provider execution. Search is over the rendered snapshot, not a
workspace-wide filesystem search. Final fidelity still needs same-state references.

#### File toolbar actions (2026-10-08)

App's shared preview component now provides snapshot copy, absolute/relative path
copy, a keyboard path-copy shortcut (Ctrl+Shift+C), find, refresh and a real
"show in File Explorer" action. Image tabs default to path copy and disable text
copy/search. The open menu states the actual operation; editor launching and file
association execution are not implied. Missing clipboard, loading, hidden,
disabled or detached owners disable copy. Task/tab/scope changes close menus and
reject late notices; pending noncooperative clipboard writes retain ownership.
Already-started clipboard writes cannot be revoked when the owner changes.

Design and safety self-check:

1. App owns clipboard/presentation over already-authorized snapshots.
   Tools.Abstractions owns the file-reveal port; Tools.BuiltIn owns the tool and
   authorization; Platform.Windows owns STA/COM Shell integration. No lower
   module references App, and App performs no filesystem/process operation.
2. Reveal uses `filesystem.reveal_file` through the existing tool executor, with
   external-side-effect risk and FileRead/Process capabilities. Read-only tasks
   and stored audit snapshots cannot reveal their original files.
3. Exact task/policy/roots and a 30-second cooperative deadline reach the pipeline.
   The path is revalidated before approval, after approval, on the owned worker
   and immediately before Shell selection. Traversal, missing/directory targets,
   escaping junctions and approval/worker-stage junction substitution are denied.
   Validation does not atomically pin the Windows namespace: an adversarial
   replacement between final validation and Shell resolution remains a TOCTOU
   limitation. Selection never executes the file or constructs a command line.
   Started Shell calls cannot be forcibly cancelled; ownership is retained until
   they return, then cancellation is propagated. Output/audit contain no file body.
4. One handwritten type per file and scoped AST size checks remain enforced.
   Shared variants own 34-pixel outer toolbar surfaces, centered icon/button
   geometry and borderless, leading-aligned menu items. The native capture caught
   unstyled bordered menu buttons and constrained pill heights; both are fixed.
5. Tests cover schema/capability/approval, cancellation/timeouts, safe errors,
   junction substitution, persisted audit/output bounds, clipboard snapshot/path
   payloads, actual menu bindings, read-only/image states, late completions and
   self/ancestor visibility/enablement/detachment. Two-theme layouts at 300/640
   pixels assert bounds, centerlines, icons and equal outer surface heights.

Native evidence lives outside the repository in
`%TEMP%\MossAgent-file-toolbar-20261008`. Light 1280x820 and dark 980x640 captures
check the real toolbar without scaling. Native keyboard input copies a snapshot
and opens the copy menu. A separate synthetic FullAccess task's open button runs
the real pipeline and Shell API: File Explorer selects PreviewSample.cs and its
SQLite audit records approved/successful completion. Only that newly opened
Explorer window is closed. No paid provider or real user file is used.

The passive Python selector initially picked Explorer's desktop shell surface.
It now excludes Progman/WorkerW and supports an exact title with uniqueness
checking; the corrected `project` capture verifies the selected file. The desktop
capture and the first popup capture (dismissed by activation) are not acceptance
evidence. Mouse automation still reports unavailable coordinate geometry; keyboard
and raw Python pixels, not stale UIA focus/counts, establish the native outcome.

The final full solution regression passes 2,977 tests (Agent 58, App 2,259, Browser 76,
Infrastructure 124, Windows 54, Providers 241, Tools 165). All 49 focused preview,
interaction and automation-preview tests pass after the toolbar/menu corrections.
The accepted light toolbar/menu screenshots verify borderless leading-aligned
items; native keyboard selection copies the path and dismisses the popup.
The ordinary managed/JIT App is rebuilt with zero warnings/errors and restarted
only after observing its idle, empty draft; user data is not replaced by QA data.
The 19-file final AST audit has zero
violations. Development remains ordinary managed/JIT; no packaging, AOT,
serialization or database schema changes are introduced in this phase. Matched
Codex automation/editor screenshots, native hover/mouse verification and external
editor integration remain outstanding. This is not a one-to-one fidelity claim.
<!-- /source: FILE_PREVIEW_ACCEPTANCE.md -->


---

<a id="file-preview-fidelity-acceptance"></a>

## FILE_PREVIEW_FIDELITY_ACCEPTANCE.md

<!-- source: FILE_PREVIEW_FIDELITY_ACCEPTANCE.md -->
### File preview fidelity acceptance

Verification: 2026-10-08/09. This supplements [FILE_PREVIEW_ACCEPTANCE.md](UI_WORKFLOWS.md#file-preview-acceptance);
it is not whole-product or complete Codex-fidelity acceptance.

#### Design self-check

1. App owns the shared breadcrumb, file-tab and preview presentation. Shared
   styles own fonts, icons, tab dimensions and image spacing. Module dependencies
   are unchanged; no platform API or storage operation is added to a view.
2. Existing preview-file execution and task-artifact reader ports are reused.
   Path presentation derives from the existing display path, not another file read.
3. Authorization, read-only approval, bounded snapshot creation, cancellation,
   task ownership and artifact verification remain in the standard pipeline.
   Zoom commands only change the current decoded snapshot's presentation.
4. Every affected handwritten C# file has one type and stays below 500 lines;
   methods stay below 80 lines. Shared markup/analyzer gates also protect the
   new breadcrumb's composition and geometry against business-view overrides.
5. Tests cover measured filename priority, shrinking/expanding layout, themes,
   compact tabs, inactive/keyboard-focused close actions, empty notices, zoom
   commands, no additional tool calls and popup invalidation on owner changes.

#### Changes and regression evidence

- `AppFileBreadcrumb` preserves the filename first, fades the parent path and
  hides the parent/separator when there is insufficient room. It remeasures on
  resize; natural font measurements, not assumed glyph widths, drive allocation.
- File actions are 30 DIP high inside a 32-DIP selected-tab outline. Image files
  have a dedicated vector icon. Inactive close actions retain their slot but are
  not visible, hit-testable or focusable until the row is hovered/focused.
- Image zoom moves from a permanent extra row to the existing toolbar's image
  options flyout. Minus/plus, actual size and fit use real tab commands. Text
  search keeps its existing action and is mutually exclusive with image options.
- Empty notices no longer consume margin or a row. Image content starts at native
  Y=180 after the header/toolbar and shared viewport padding; the reference image
  starts at Y=188 including its eight-pixel DWM frame offset.
- The image flyout closes on tab/context replacement, hidden or disabled
  ancestors and detachment. It shares the preview's existing subscription lifetime.
- Four initial fidelity cases fail against the old 34-DIP buttons. Those cases
  pass after the compact-tab change. Eight resize cases also exposed a bad test
  assumption that the natural filename width exceeded 180 pixels; the final test
  compares arranged width to measured desired width and retains overflow,
  narrow-layout and parent-restoration assertions.

#### Native screenshot and interaction evidence

Evidence root, outside the repository:
`%TEMP%/MossAgent-preview-fidelity-20261008`.
The Python capture/comparison scripts and native QA host are not product files.

- Passive PrintWindow captures the user's current Codex window without changing
  its UI, and independently captures a production MossAgent window. Pixel capture
  rejects black/uniform results and mismatched or changing native bounds.
- Both apps preview the same synthetic `combo-focus-comparison.png`. The native
  host uses the production App, MainWindow, isolated SQLite configuration and
  `FilePreview.OpenAsync` through the real authorized read-only tool pipeline.
  A loopback port-nine provider is seeded but never called; no model message is sent.
- `codex-current.png` is 1936x1048 with DWM edges; `light-wide/moss-final.png` is
  1920x1032, scale 1 and ordinary permissions. `preview-header-comparison.png`
  places original-pixel 754x238 and 752x238 crops side by side, without scaling.
  Crops align each inspector's own origin and remove the reference frame offset.
  They are not a whole-window pixel-difference score; task and tab counts differ.
- Rebuilt host, Light 1920x1032: click the image options action, then plus. The
  label changes from fit to 125%, the actual image enlarges and horizontal
  scrolling appears. Fit restores the smaller image. Clicking outside dismisses
  the flyout; clicking the text tab then shows the real code/syntax preview.
- Final Light 1280x820 and Dark 980x640 captures use the same synthetic file,
  ordinary permissions and scale 1. Narrow paths truncate within their allocated
  space and retain the complete path in the tooltip/accessibility presentation.
  The dark image-options popup stays inside the narrow window and Escape closes
  it; a fresh accessibility snapshot confirms the zoom actions are gone.
- All four native preview hosts close normally with exit 0. The three final
  host stderr logs are empty; no acceptance process is left running.
- Main-window PrintWindow may omit separate popup HWNDs. Popup interactions are
  also observed directly through Computer Use; they are not inferred from a
  main-window image alone. No Codex UI input is automated.

#### Verification

- Solution build succeeds with zero warnings/errors.
- Final relevant App/component/analyzer filter: 251 passed. It includes eight
  light/dark image-menu owner invalidation cases and eight breadcrumb layouts.
- Full App run: 2398 executed, 2396 passed, two sidebar-heading cases fail only
  during temporary-directory deletion with a file-sharing violation. All 12
  sidebar-heading cases pass on isolated rerun. Unrelated cleanup code is unchanged.
- Other full suites: Agent 63, Browser 76, Infrastructure 124, Platform.Windows
  54, Providers 283 and Tools 174 all pass. Combined latest full-suite evidence is
  3172 executed, 3170 passed and two failed; this is not a clean full-suite result.
  App evidence is in `app-complete`, other full reports in `full-final`, and
  final focused/isolated reruns in `verified-final` beneath the evidence root.
- Scoped whitespace verification, Roslyn single-type/file/method audits and
  AXAML/XML validation pass. No staged files, commit, push or release is created.

#### Remaining fidelity limits

- The Codex reference has more tabs. Add-tab and expand/restore controls now have
  real behavior, covered in [FILE_PREVIEW_WORKFLOW_ACCEPTANCE.md](UI_WORKFLOWS.md#file-preview-workflow-acceptance).
  External-editor selection is not implemented;
  the existing open action only reveals an authorized original file in Explorer.
- The follow-up below corrects missing shared toolbar shadows, rectangular image
  corners and very narrow breadcrumb allocation. The later copy/image optical
  sizes and multilayer Explorer badge are verified in [PREVIEW_ICON_ACCEPTANCE.md](UI_WORKFLOWS.md#preview-icon-acceptance);
  this does not establish pixel equality for every action. Dark/narrow Codex
  same-state references and Codex's image-options menu still require comparison.
- Automation same-state list/editor references remain outstanding. This preview
  slice does not validate scheduling behavior or close the broader product goal.

#### 2026-10-09 toolbar follow-up

##### Design and boundaries

- App's shared `AppFilePreview` owns a read-only `IsToolbarCompact` property.
  Below 400 DIP it preserves the breadcrumb, copy split button and search/image
  options, and puts reveal/refresh in a real localized action menu. Wider panes
  restore the existing visible actions. No action, authorization or port is removed.
- The compact menu uses the existing pane commands; reveal remains disabled for
  read-only tasks and stored snapshots. Refresh still uses the authorized preview
  tool and artifact reader, with their unchanged cancellation/resource limits.
- Resize, tab/context/policy changes, hidden/disabled ancestors and detachment
  close the menu. The preview continues using its existing subscription lifetime.
- Breadcrumbs, split-action borders and outline-button content presenters use
  the same `SharedSurfaceShadow`, with zero horizontal/vertical offset. Toolbar
  action vectors are 14 DIP; chevrons remain 10 DIP, with unchanged hit areas.
  Image options use a dedicated document/magnifier vector rather than text search's
  generic magnifier. The shared image frame clips at an eight-DIP corner radius.
- A trial that replaced grid columns during `SizeChanged` failed 25 relevant
  tests with an Avalonia `Grid.MeasureCellsGroup` index error. The final layout
  instead keeps four columns and groups the wide-only actions in the last column.
  It neither mutates grid definitions during arrangement nor leaves an empty
  column's spacing when compact. This trial is not part of the final implementation.

##### Tests and native evidence

- Final focused preview, workspace-pane and related component filter: 155 passed.
  Twelve new compact-toolbar cases cover 300/399/400/640-DIP transitions, light
  and dark themes, readable filename allocation, no trailing empty-column gap,
  presenter shadows, actual commands, read-only denial and menu invalidation.
  Existing image fit/zoom/resize cases also verify the frame's geometry and clipping.
- Final solution and external native-host builds: zero warnings/errors. Scoped
  whitespace, Roslyn single-type/file/method limits and AXAML/resource XML checks
  are performed on the affected files. No AOT or publication setting is changed.
- Final full solution run executes 3222 tests: 3220 pass and two Windows MCP
  cases fail (`BlockingUiCaller_DoesNotPreventMcpInitialization` with manager enabled,
  and `ConfigurationChange_ReclaimsPreviousServerAndDescendants` with disable).
  App's final 2448 tests all pass, as do Agent 63, Browser 76, Infrastructure 124,
  Providers 283 and Tools 174. A subsequent isolated full Windows suite passes
  all 54 cases. The latest per-suite evidence is green, but the single combined
  run is not; unrelated MCP implementations/tests are unchanged. Reports are in
  `full-final`, with the Windows rerun in `isolated/windows-isolated.trx`.
- Evidence and all Python/QA support files remain outside the repository under
  `%TEMP%/MossAgent-preview-layout-20261009`. The host uses isolated SQLite data,
  ordinary permissions and render scale 1; the loopback test provider is not called.
- A fresh passive Python capture confirms the current 1936x1048 Codex reference
  still previews the same synthetic image. The final Light 1920x1032 capture is
  in `light-verified/moss-final.png`. `preview-header-comparison.png` contains two
  754x238 original-pixel crops, aligned to each inspector origin without scaling.
  Differences in tab counts and available actions remain; this is not a global
  pixel-difference score or proof of exact Codex responsive behavior.
- Native Light interaction narrows the inspector with the real splitter, opens
  the compact menu and refreshes the file. The menu closes and the task's snapshot
  count increases from two to three. Both before and after the fixed-column change,
  the menu's reveal action stays disabled under the seeded read-only policy.
- Native image zoom reaches 125%, creates a scrollable image and returns to fit.
  Dark 980x640 interaction opens/closes the compact menu, expands the inspector
  to the wide toolbar and restores the narrow pane/current tab. These interactions
  precede the fixed-column regrouping; the final rebuilt dark capture independently
  verifies the resulting narrow layout in `dark-verified/moss-final.png`.
- Pixel samples confirm actual image clipping, not just an assigned radius: the
  synthetic source's square corner is RGB 244/244/244, whereas the rendered corner
  retains the light/dark pane background and transitions to that gray within eight
  pixels. Fit, zoom and scrolling still operate on the same decoded snapshot.
- No Codex UI input, real provider call, message send, commit, push or release is
  performed. This follow-up does not close the outstanding whole-product fidelity
  or automation same-state acceptance work.
- All four native hosts close normally with exit 0 and empty stderr. No QA host
  or test process remains running. The checkout retains its prior unrelated work,
  with 939 dirty entries and zero staged paths at handoff.

#### File-tab close chrome (2026-10-09)

The fresh passive Python Codex capture still displays the same synthetic image.
Its selected-tab close mark is substantially smaller than MossAgent's default.
Only the shared file-preview variant changes: the glyph is now six DIP with a
one-DIP stroke and theme-aware muted foreground; the action remains 24x24 DIP.
Inactive visibility, focus reveal, commands, tab selection and reserved width
are unchanged. Views do not add dimensions or bypass the shared component.

Design self-check: App/shared styles own this presentation-only change, using
the existing close command and authorized in-memory preview. There are no new
ports, file reads, tool calls, approval changes or migrations. The style and
test remain below file/method limits. Four theme/width cases assert actual icon,
foreground and hit-target geometry alongside existing empty/focus/menu behavior.
Those assertions fail against the old ten-DIP icon before the style change.
The final preview/isolation filter passes 33 tests.

Native ordinary/JIT hosts use separate synthetic SQLite data, render scale 1,
Light 1920x1032 and Dark 980x640. Actual center clicks close the selected image
tab in both windows, reducing the tab count from two to one and revealing the
remaining code preview. No file or conversation is deleted. All six hosts close
normally with exit 0 and empty stderr; no user provider is called.

Evidence stays outside the repository in
`%TEMP%/MossAgent-mcp-concurrency-20261009`. The 740x186 original-pixel comparison
is `preview-tab-chrome-comparison.png`, using the fresh Codex frame, the original
MossAgent frame and `native-light-normal-final/moss.png`. The final default-state
capture moves the QA pointer away from the tab before launching the light host,
so it is not compared against a hovered Codex tab. Dark default pixels are in
`native-dark-final/moss.png`; both actual click results are separately retained.
The passive screenshot and comparison scripts are not source or release assets.

At luminance below 180, the measured close-mark ink bounding box changes from
10x10 to 6x6 pixels, matching this Codex reference's 6x6 mark. At the lighter
220 threshold MossAgent is 8x8 and Codex 7x7, so the anti-aliasing is not identical.
The different tab count/names and remaining header/action geometry are not a
whole-pane pixel-perfect acceptance result. This slice does not complete the
automation same-state comparison, all DPI/themes/states or real-provider flows.

Final solution/native builds have zero warnings and errors. The scoped AST,
whitespace and diff checks pass. Two earlier broad runs pass all 3,229 tests;
the last run on the final glyph executes 3,229 with one unrelated provider/proxy
fixture cancellation during SQLite initialization, before its routing assertions.
All 12 provider/proxy cases pass in an isolated rerun. The raw last full result
remains 3,228 passed/one failed, not a clean whole-product acceptance. No proxy
implementation or fixture is changed to hide that result. No commit/push/publish
occurs; the checkout remains at 948 dirty entries and zero staged paths.
<!-- /source: FILE_PREVIEW_FIDELITY_ACCEPTANCE.md -->


---

<a id="file-preview-workflow-acceptance"></a>

## FILE_PREVIEW_WORKFLOW_ACCEPTANCE.md

<!-- source: FILE_PREVIEW_WORKFLOW_ACCEPTANCE.md -->
### File preview workflow acceptance

Verification: 2026-10-09. This extends [FILE_PREVIEW_ACCEPTANCE.md](UI_WORKFLOWS.md#file-preview-acceptance) and
[FILE_PREVIEW_FIDELITY_ACCEPTANCE.md](UI_WORKFLOWS.md#file-preview-fidelity-acceptance); it is not complete product acceptance.

#### Design self-check

1. App owns picker coordination and shared preview layout. `AppWorkspacePanes`
   owns expansion, retained content and preferred splitter width; the main window
   view model does not absorb more layout or file-selection responsibilities.
   Conversation and ordinary inspector headers reuse its guarded full-view commands.
2. The existing picker port only supplies paths. Reading reuses
   `FilePreviewPaneViewModel.OpenAsync`, the standard preview-file tool pipeline
   and the task-artifact reader. Selecting a file never authorizes its directory.
3. Task roots, approval policy, bounded snapshots and audit persistence are
   unchanged. Selection has a five-minute deadline for human browsing; individual
   preview reads retain their existing 30-second execution context. Cancellation
   rejects late paths without abandoning ownership of a native dialog still open.
   Full-view commands reject detached, hidden or disabled owners; opening updates
   the existing conversation-owned panel state through the two-way binding.
4. All affected handwritten C# files have one type and remain below 500 lines;
   methods remain below 80 lines. Views use shared controls and appearance variants.
   New labels and safe notices use the existing localized inspector resources.
5. Tests exercise actual authorized and unauthorized reads, deduplication, tab
   limits, missing services, cancellation, task/policy changes, noncooperative
   pickers, late completion, expansion lifecycle and retained draft/tab state.
   Header regressions additionally cover tab aliases, command replacement,
   disabled commands and independent startup/activity state in both themes.

#### Implemented behavior

- The preview header has actual add-tab, expand/restore and close-panel actions.
  Add-tab offers file selection and the existing return-to-sources command.
  Unavailable expansion and an absent task are disabled, not inert text buttons.
- Picker state is separately owned, blocks reentry and remains occupied until
  a noncooperative picker returns. Hiding, disposal and task/policy/root changes
  cancel the request; stale paths cannot populate a subsequently selected task.
- At most eight paths are accepted per selection; empty paths are ignored and
  case-insensitive duplicates share existing tabs. The overall eight-tab limit
  remains enforced by the preview owner. Failures use fixed localized notices,
  never raw exception text or an extra unvalidated file read.
- Expansion hides rather than destroys the primary pane, gives the inspector
  the whole workspace and hides the splitter. Restoring preserves its preferred
  width, subject to the existing small-window minimum/maximum constraints.
- The conversation header can enter full view directly from the collapsed panel.
  Ordinary inspector tabs now offer the same expand/restore action as file preview;
  the labels distinguish entering/exiting full view from opening/closing the panel.
- Shared headers recalculate derived state only for consumed properties. Inspector
  selection reuses one instance-owned tab list and the existing command parameters.
  Workspace panes share presenter creation, lifecycle guards and command notification;
  collapsed and expanded layouts share their non-docked width reset.
- Closing/replacing/detaching the workspace, losing ownership and hidden/disabled
  ancestors reset expansion. Returning from the expanded file preview to sources
  also restores the primary pane, leaving a visible route to normal interaction.
- After a resize or expansion transition, the active file tab is brought back
  into the horizontal viewport. Search Escape returns focus to the visible copy
  action instead of the return action now inside the add-tab menu.

#### Native interaction and screenshot evidence

Evidence is outside the repository:
`%TEMP%/MossAgent-preview-workflow-20261009`.
The Python capture/comparison scripts, isolated fixtures and QA host are not
product files and are not submitted to the repository.

- The host uses the production App/MainWindow, ordinary permissions, scale 1,
  isolated SQLite data and a completed read-only task. Synthetic files are
  previewed through the actual authorized tool executor and artifact reader.
  A loopback port-nine provider is seeded but never called; no message is sent.
- Light 1280x820: add-tab opens the real native picker. Selecting
  `SelectedSample.cs` produces a third tab, syntax-highlighted content and a
  separate bounded artifact snapshot. A previously timed-out selection produces
  a safe cancellation notice and no new tab or snapshot.
- The 30-second selection deadline was too short in native use: one dark picker
  returned at roughly 30.5 seconds and correctly discarded the result, but this
  made normal browsing impractical. Selection is now separately bounded at five
  minutes; deterministic clock tests cover both 45-second success and expiration
  without waiting five minutes. The file-read timeout is not extended.
- Final Dark 980x640: the native picker remains open for about 142 seconds before
  selection, then successfully loads the real third file tab and creates its
  snapshot. Expanding and restoring retain the active tab in view; closing the
  panel restores the floating project card. See `dark-verified/` screenshots.
- Light interaction retains an unsent synthetic draft while dragging the splitter,
  expanding, shrinking the native window from 1280x820 to 1080x720 and restoring.
  Returning to sources while expanded restores the conversation and its draft.
  See `light-final/resized-inspector-draft.png`, `expanded-narrow.png`,
  `restored-narrow-draft.png` and `back-restores-draft.png`.
- Passive PrintWindow captures the current Codex window at 1936x1048, including
  DWM edges, and Light MossAgent at 1920x1032. Both preview the same synthetic
  image. `preview-header-comparison.png` places original-pixel 754x238 and
  752x238 crops side by side without scaling, aligned to each inspector's origin.
  Tab counts differ; this is not a whole-window pixel-difference score.
- Separate native popups are observed through Computer Use; main-window
  PrintWindow alone does not establish popup rendering or interactivity.
  The Codex reference is captured passively; no Codex UI input is automated.
- All five native QA hosts close normally with exit 0 and empty stderr logs;
  no QA process remains running. They include the rejected 30-second selection
  and the final successful longer selection, not just the successful cases.

#### Verification of initial preview workflow

- Final focused preview/workspace/search regression run: 128 passed, including
  real pipeline rejection of a picked file outside the task roots and successful
  reading only after explicit secondary-root authorization.
- Shared-control encapsulation and appearance gates remain enabled. Scoped
  whitespace checks, Roslyn single-type/size audits and AXAML/XML checks pass.
- Final solution build succeeds with zero warnings/errors.
- Latest full App run: 2436 executed, 2433 passed and three failures during
  sidebar-heading temporary-directory deletion with a sharing violation. All
  12 sidebar-heading cases pass on isolated rerun. A preceding full App rerun
  also encountered a SQLite handle-disposal exception during compatible-stream
  fixture initialization; all six compatible-stream cases pass in isolation.
  These are not a clean full-suite result; unrelated cleanup code is unchanged.
- Other complete suites pass: Agent 63, Browser 76, Infrastructure 124,
  Platform.Windows 54, Providers 283 and Tools 174. Combined latest evidence is
  3210 executed, 3207 passed and three failed, not 3210 passing tests.
  Full reports are in `full-tests` and `app-verified`; focused and isolated
  reports are in `verified-final`. Earlier failed attempts are retained too.
- No staged files, commit, push or release is created. Existing unrelated work
  and test cleanup code are preserved.

#### Inspector full-view and shared-header cleanup follow-up

Evidence: `%TEMP%/MossAgent-controls-cleanup-20261009-2310`.
Earlier full-view evidence remains in
`%TEMP%/MossAgent-tool-deadline-20261009-213806/native`.

- The inherited 3805-test run had five failures: four obsolete binding/margin
  expectations and one sharing violation when a command-flow test read its child
  PID file. Binding expectations now specify two-way state and the measured card
  inset is eight DIP. The command-flow implementation/test is unchanged; its
  isolated rerun and the final full suite pass, not proof that the race is fixed.
- Four new header-state cases cover both themes. Final focused regression:
  100 passed. Final solution build: zero warnings/errors. Scoped formatting,
  Roslyn single-type/file/method audits and affected AXAML/XML checks pass.
- Final full solution test run: 3809 passed, zero failures/skips across seven
  suites: App 2876, Agent 63, Browser 76, Infrastructure 144, Platform.Windows 129,
  Providers 302 and Tools 219. Original failures and the rejected cross-thread
  initialization of the new test fixture remain in their separate reports.
- Native Light 1920x1032 and Dark 980x640 both enter inspector full view,
  restore split panes and close back to the floating card without losing the
  synthetic unsent draft. Python captures are passive original pixels; native
  resize/theme/button inputs use Computer Use. DPI remains 96; no global scale
  or forced DPI change is introduced. Screenshots include `moss-light-full-view`,
  `moss-light-restored-draft`, `moss-dark-narrow-full-view`,
  `moss-dark-narrow-restored-draft` and `moss-dark-narrow-final` PNGs.
- The QA process uses isolated synthetic data and a loopback-only provider.
  No message/model request is sent or daily database read. It closes normally
  and stderr is empty. Screenshot scripts, QA data and logs remain outside Git.
- This refactor changes no authorization pipeline, persistence schema,
  model routing, JIT or Windows x64 single-file publishing settings. Fewer
  redundant refreshes/temporary tab arrays are established by the implementation;
  no measured end-to-end speedup or whole-product visual fidelity is claimed.

#### Remaining limits

- Shared toolbar shadows, image clipping and narrow breadcrumb allocation were
  implemented in the later [FILE_PREVIEW_FIDELITY_ACCEPTANCE.md](UI_WORKFLOWS.md#file-preview-fidelity-acceptance) follow-up.
  Copy/image glyph dimensions and the Explorer badge are further verified in
  [PREVIEW_ICON_ACCEPTANCE.md](UI_WORKFLOWS.md#preview-icon-acceptance). Other action semantics and dark/narrow Codex same-state references,
  expanded reference geometry and its add-tab menu remain outstanding.
- External-editor selection is not implemented; the existing open action only
  reveals an authorized original file in Explorer through the tool pipeline.
- This slice does not validate automation scheduling, actual provider calls or
  whole-product one-to-one fidelity. The broader goal remains active.
<!-- /source: FILE_PREVIEW_WORKFLOW_ACCEPTANCE.md -->


---

<a id="inspector-overview-fidelity-acceptance"></a>

## INSPECTOR_OVERVIEW_FIDELITY_ACCEPTANCE.md

<!-- source: INSPECTOR_OVERVIEW_FIDELITY_ACCEPTANCE.md -->
### 检查器概览共享布局与原生对照验收

#### 2026-10-12 实现与自检

- 概览卡片继续使用 `AppCard`、`AppText`、`AppActionButton` 和现有共享图标；
  没有新增另一套视觉组件，也没有改变授权、审批、命令、颜色或阴影。
- `Styles/CardInsetVariants.axaml` 的 `card-inset-inspector` 内边距由
  `20,16,10,16` 调整为 `20,10,10,13`，不在页面覆盖 Padding。
- `Styles/InspectorVariants.axaml` 拥有概览外层、标题/状态、来源标题和来源列表的
  四个共享布局变体。间距分别为 10、4、5 DIP，来源标题的上边距为 6 DIP。
  `InspectorPane` 只组合分组并保留业务绑定，不使用负 margin 光学校正。
- 第一轮校正标题/状态分组和顶部留白，第二轮校正列表节奏及底部留白；
  卡片仍随实际内容变化，不锁定内容高度，也不通过缩放截图制造一致性。

实现前五项自检：

1. 行为归 App 的 Views/Styles，测试归 App.Tests；模块依赖方向不变。
2. 复用现有检查器、附件、项目目录及会话命令，无需增加应用端口或工具。
3. 本次样式调整不执行模型或工具，也不改变超时、取消、输出限制或审批管线。
   原生宿主只有独立合成数据库和临时授权目录，不使用用户凭据或数据库。
4. 新手写 C# 文件只有一个类型、128 个物理行，最长方法声明 42 行，语法错误为 0。
   三个生产 AXAML 文件均未超过 500 行硬上限；布局原语保持纯结构用途。
5. 新增八项真实页面回归覆盖明暗主题、三种宽度、内容边界、来源命令和草稿保留，
   并检查没有当前会话时概览隐藏，不通过占位命令代替真实行为。

#### 被动截图与测量

证据目录在仓库外：
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-main-dark-narrow-20261012-144117`。
临时文件不提交仓库，系统清理临时目录后这些证据可能不再存在。

- `capture_passive.py` 使用 PrintWindow，校验唯一目标进程/PID、稳定窗口边界和非黑像素。
  脚本不激活、移动、缩放或点击窗口；Codex 参考进程的实际名称是 `ChatGPT.exe`。
- 首次旧 ImageGrab 脚本因参考窗口不在前台而拒绝捕获；没有为截图自动操作 Codex，
  改用被动 PrintWindow 后取得 `codex-reference-current.png`。
- 三张测量原图均为 1920×1032，桌面 DPI 为 96。`overview-comparison.png`
  使用相同坐标 `(1600,96,1920,310)` 的原尺寸裁片，没有拉伸。
- `compare_overview.py` 在四个指定 ROI 内寻找暗像素行，判定条件为
  `max(R,G,B) < 185` 且每行至少三个像素满足条件；报告为 `overview-pixels.json`。
  查看全部的 ROI 从 y=252 开始，避免把默认目录文字误计为目标按钮。

| 指定 ROI | 坐标范围，右/下边界不包含 | Codex 首墨迹行 | 原始 MossAgent | 最终 MossAgent |
| --- | --- | --- | --- | --- |
| 项目标题 | `(1620,114,1730,145)` | 123 | 129 | 123 |
| 状态 | `(1654,148,1745,183)` | 152 | 164 | 152 |
| 来源标题 | `(1625,186,1680,221)` | 195 | 201 | 195 |
| 查看全部 | `(1654,252,1750,290)` | 256 | 267 | 256 |

这里一致的仅是指定阈值和 ROI 下的第一墨迹行，不是整张截图逐像素一致。
Codex 参考是对话/MCP 来源，MossAgent 是合成空对话/目录来源；业务内容、字体、
抗锯齿、图标和颜色仍有差异。图中蓝色指针光圈不属于应用阴影。

#### 原生窗口与交互

- 最终真实 App 程序集 SHA256：
  `872BD4FF7CE04196BCA39800E6C6D0569AF8C758B80266D417A32E5134675854`。
  `host-final/MossAgent.App.dll` 与仓库 Debug Windows x64 构建的哈希相同。
- 最终普通权限宿主的 `dark-narrow-final.png` 为 980×640；请求的 560 高度被实际
  MinHeight=640 限幅，不能把这张图称为 980×560 验证。
  `dark-max-final.png` 和 `light-max-after.png` 分别是最终明暗主题 1920×1032 帧。
- 最终宿主实际点击“查看全部”进入来源检查器，收起检查器、最大化，并从实际设置页
  切换浅色主题后返回。来源检查器证据为 `dark-narrow-final-sources.png`。
- 早期/中间真实宿主另验证了更多菜单、Escape、完整视图进入/恢复、检查器及概览
  开关和未发送草稿。`dark-narrow-after-restored-draft-stable.png` 是稳定后的草稿帧；
  立即切换第一帧曾短暂未绘制文本，后续新观察与稳定截图确认恢复。
  这些交互帧不替代最终构建的几何截图，也不代表真实 agent 执行成功。
- 所有输入只作用于任务自建宿主，没有发送草稿、点击删除/归档或自动操作 Codex UI。
  初始 PID 23208、中间 PID 21472 和最终 PID 2836 均已退出，宿主日志记录 `QA_EXIT: 0`；
  最终进程消失还由 `final-qa-exit-check.log` 精准复核，不以滞后窗口绑定认定关闭。

#### 红绿回归与最终验证

- 正确原始基线 `overview-red-final.trx`：八项中六项布局失败、两项行为通过。
  首次校正与既有标题栏/检查器回归合计 `overview-focused.trx` 为 67/67。
- 底部留白第二轮基线 `overview-footer-red.trx`：六项布局失败、两项行为通过，
  预期底部 Padding=13、实际为 8。最终 `overview-final-focused.trx` 为 67/67。
- 第二轮生产修改前的完整 App 回归 `app-full.trx` 为 3902/3902，Visual 为 8/8；
  这是中间结果，不替代最终源码的全量验证。
- 最终八个程序集顺序执行完成，各自的 `final.log` 和 `final.trx` 位于
  `final/<程序集>/`，汇总 `final-results.json`：5433/5433，无失败或未执行项。

| 最终测试程序集 | 通过/总数 |
| --- | --- |
| MossAgent.Agent.Tests | 112/112 |
| MossAgent.App.Tests | 3902/3902 |
| MossAgent.App.Visual.Tests | 8/8 |
| MossAgent.Tools.Tests | 483/483 |
| MossAgent.Platform.Windows.Tests | 137/137 |
| MossAgent.Browser.Tests | 76/76 |
| MossAgent.Infrastructure.Tests | 199/199 |
| MossAgent.Providers.Tests | 516/516 |

- `solution-final-build.log`：解决方案构建 0 警告、0 错误。
  新测试的 `dotnet format whitespace --verify-no-changes --no-restore` 退出码为 0；
  `source-audit.log` 的文件、单类型、方法硬上限检查通过。
  `diff-check-final.log` 记录本轮快照差异、新文件空白和 AXAML XML 检查。

#### 夹具失败记录与验收限制

- 初期夹具引用不存在的 Workspace.Prompt，已改为真实 WorkspaceComposerView 的 AppInput。
- 曾 await Workspace.LoadAsync，Headless 运行没有所需消息泵，测试 PID 20740 仍存活。
  `overview-test-stacks.log` 和 `overview-test-async.log` 保留诊断；核验 PID 和路径后终止。
  该次运行不完整，`overview-red.log` 有 runner crashed -1，不能用作有效布局红基线。
  ClrMD 诊断包含 Metadata error，不被表述为完整准确的 awaiter 链。
- 夹具改为既有测试使用的 Catalog.ReloadAsync；显式 ShutdownAsync 后再 Close 的试验
  又产生五项字体创建错误和三项真实布局失败，共 0/8，通过记录见 `overview-red-catalog.log`。
  最终采用 window.Close 后由 DI async disposal 释放，与已有夹具一致，未修改生产加载逻辑。
  后续正确红绿和全量通过，不等于已查明或修复全局字体间歇问题。
- `overview-red.trx` 被后续八项失败运行覆盖，`overview-fixture-terminated.trx` 也误复制了
  该覆盖结果；两者都不能作为终止运行的完成计数证据。
- 本轮没有高 DPI 原生验收、公网真实提供商验收或全页面/全状态视觉验收。
  Debug 保持普通 JIT；Windows x64 单文件发布配置未改，没有恢复 AOT 或 JSON 源生成。
  未暂存、提交、推送或发布，完整产品与 Codex 一比一还原目标仍未完成。
<!-- /source: INSPECTOR_OVERVIEW_FIDELITY_ACCEPTANCE.md -->


---

<a id="inspector-task-state-acceptance"></a>

## INSPECTOR_TASK_STATE_ACCEPTANCE.md

<!-- source: INSPECTOR_TASK_STATE_ACCEPTANCE.md -->
### Task-owned inspector presentation

#### Scope and design self-check

1. App owns presentation, through `WorkspaceInspectorViewModel`. MainWindow
   retains its existing binding/command surface and forwards notifications.
   Application, Browser, Infrastructure and Windows dependencies are unchanged.
2. The existing catalog selection and archive events identify owners. Existing
   file-preview, page-navigation and browser-session ports remain authoritative;
   no alternate tool execution or browser-navigation path is introduced.
3. Retained state contains only a tab key and two display flags. This change
   performs no file, network, process, database or model operation. Approval,
   authorization, timeout, cancellation and output bounds are unchanged. The
   background-run regression uses an isolated loopback provider and SQLite data.
4. The controller and each handwritten test file have one type, with files and
   methods below the 500/80-line hard limits. MainWindow is smaller, rather than
   extending its near-limit class or using partial files. No visual dimensions,
   AOT, source-generated JSON, packaging or shared-style exceptions are added.
5. Regressions cover successful round trips plus invalid/locked/foreign/null
   navigation, disposal, record replacement, archive, reload, coherent observer
   snapshots and background schedule completion/cancellation.

#### Behavior

- Every conversation retains its selected inspector tab, docked visibility and
  floating-context-card preference separately for this window's lifetime.
- The first visit to a conversation uses the sources/home tab, a closed docked
  inspector and an enabled floating card. The existing view determines when the
  card can actually be displayed; an empty draft does not invent task content.
- Empty drafts retain separate presentation for each project, including the
  no-project context. Starting a conversation does not copy its draft's state.
- Task identity, not record equality or display title, owns state. Title/status
  replacements do not reset it, and a background task cannot select its pane.
- Restoring a state replaces all three fields before publishing notifications,
  preventing observers from seeing another task's tab with the previous flags.
  The shared host continues to own layout measurements and splitter width.
- Archiving discards the retained task state. Restoring the archived conversation
  starts with default presentation. Dispose removes event handlers, clears the
  cache and ignores subsequent mutations/selections.
- This does not persist preferences across application restarts, restore closed
  file-preview tabs, or create/release browser or terminal sessions.

#### Deterministic evidence

Evidence root: `%TEMP%\MossAgent-inspector-ownership-20261009`.

- Before implementation, the 13 new ownership cases reproduce nine failures and
  four passes (`red/ownership-red.trx`), including inherited tabs/docking, draft
  presentation and missing context-card message inset.
- After implementation, 109 related MainWindow, inspector, navigation and
  embedded-browser isolation cases pass (`focused/inspector-focused.trx`).
- The additional safety/background regression passes 31/31, including the 22 new
  ownership/safety cases (`safety/ownership-safety.trx`). The background cases use
  the actual scheduler, App runner, normalized provider stream and repositories,
  hold one loopback model response, then complete or cancel it. The foreground
  task, automation page and retained browser-inspector state remain unchanged.

#### Native window and screenshot evidence

- The production Debug executable runs under ordinary permissions with fresh,
  separate project/task/SQLite data. The two seeded plans stay paused, the
  provider points to an unused loopback port and no real model, user schedule,
  daily browser profile or user database is used. The QA program/scripts/data
  remain outside the repository.
- Windows Computer Use input opens A's browser inspector, visits B's default
  presentation, opens B's terminal inspector and returns to A. Passive Python
  PrintWindow captures confirm A restores browser rather than inheriting terminal.
  `inspector-task-roundtrip.png` collects original-size sidebar/pane crops from
  those four 1920x1020 native frames without resizing their pixels.
- The actual automation page is visited in light and dark themes; selecting the
  already-selected A conversation returns to its retained browser inspector.
  No plan is run in this native window; actual scheduled completion/cancellation
  is covered by the isolated scheduler regression described above.
- Native checks include maximized, restored and pointer-resized windows. After
  resize, one close action is refused because its stale geometry identifies a
  non-target window. No input is sent to that window. Fresh enumeration, binding,
  activation and screenshot observation allow the single retry to succeed.
  Diagnostic capture `dark-small-geometry-before-rebind.png` is retained and is
  not labeled as a successful close. The actual final small frames are 980x642.
- In the small dark window A's inspector is closed, B restores its terminal tab,
  and A restores its closed state and floating card. Original full-window frames
  appear in `inspector-small-state.png`. Native browser/terminal processes are
  not started by these presentation checks. The App closes normally; its owned
  PID is no longer present and native stderr is empty.
- `codex-mossagent-shell-native.png` compares original-size shell crops from the
  passive Codex reference and the initial MossAgent frame. Codex's eight-pixel
  maximized system border is cropped, not rescaled. At sample y=500 the first
  white main-background pixels are x=316 and x=395 respectively. This exposes
  an outstanding physical scaling/geometry difference, not pixel equivalence;
  its DPI/zoom cause has not been established by this presentation fix. The
  conversations, project lists and inspector contents also differ. Exact crop
  rectangles and native sizes are recorded in `native-comparison.json`.

#### Final verification

- All 3,706 tests pass across seven solution test assemblies, without failures
  or skipped cases (`full/`, `full-summary.csv`). The latest solution build has
  zero warnings and errors (`build.log`).
- Scoped whitespace formatting, normal Git diff checking and the Roslyn
  one-type/file/method-size audit pass. MainWindow is 435 lines (largest method
  75), the inspector controller 143 (16), ownership tests 158 (23), and safety
  tests 139 (36). Existing unrelated work stays untouched; nothing is staged,
  committed, pushed or published.

#### Remaining acceptance

- Native screenshots and input evidence must be reported separately from
  Headless state assertions; a tab named browser is not proof of an initialized
  WebView2 session or successful concurrent DOM operations.
- Codex automation/editor screenshots in the same state, full pixel fidelity,
  multiple DPI combinations and two-session native browser release/DOM/capture
  acceptance remain outstanding. Do not mark the overall product goal complete
  based on this presentation fix or its passing regression.
<!-- /source: INSPECTOR_TASK_STATE_ACCEPTANCE.md -->


---

<a id="narrow-workspace-layout-acceptance"></a>

## NARROW_WORKSPACE_LAYOUT_ACCEPTANCE.md

<!-- source: NARROW_WORKSPACE_LAYOUT_ACCEPTANCE.md -->
### Narrow workspace layout

#### Reference and staged design

Fresh passive Python captures of Codex 26.1007.2314.0 show an ordinary-permission,
96-DPI, 526x601 conversation with its navigation sidebar collapsed. This proves an
observed usable size, not Codex's minimum size or its sidebar/inspector breakpoints.
The current MossAgent MainWindow minimum remains 980x640. Do not lower it until
navigation, settings, composer and inspector interaction fit the resulting space.

The first correction addresses shared inspector layout: docking currently reduces
the primary pane below its configured minimum when both panes cannot fit. Reuse the
existing full-width inspector presentation automatically at that point, without
changing the user's explicit expansion or preferred splitter width. Returning to
sufficient width must restore the previous docked split. This is a layout safety
requirement, not a claim of a captured Codex inspector breakpoint.

#### Required design self-check

1. App owns the shared AppWorkspacePanes layout and presentation bindings. No Domain,
   Application, infrastructure, platform or dependency-direction change is needed.
2. Reuse the existing pane presenters, expansion commands, inspector close command
   and splitter preference. Keep both pane instances and unsent primary input intact.
3. Layout invokes no tool, provider, storage or command process. Existing approvals,
   cancellation, path authorization and output limits are unchanged. A compact
   inspector must retain its real close action; disabled expansion cannot change
   state through stale commands.
4. Keep one type per handwritten C# file, no partial split, new view-specific
   dimensions or colors. Expose effective full-width presentation separately from
   the explicit expansion preference.
5. Test real headless window resize and conflicting measure/arrange widths in both
   themes, exact breakpoint boundaries, restoration of user width/expansion/draft,
   property notifications and rejection of unusable expansion commands. Re-run
   existing pane, preview, inspector and solution-build checks.

#### Outstanding acceptance

This preparatory pane correction does not make MainWindow resizable to 526x601.
Native sidebar behavior needs a fresh reference; navigation/settings adaptation,
composer and page coverage, native small-window screenshots, high-DPI/clipping and
the complete product goal remain open. Record executed checks below before handoff.

#### Implemented layout boundary

`AppWorkspacePanes.IsInspectorExpanded` remains the user's explicit preference.
The read-only `IsInspectorFullWidth` reports actual presentation: an open inspector
uses the complete host when explicitly expanded or when the configured minimum
primary/inspector widths and splitter cannot fit. The default host breakpoint is
644 DIP (360 + 280 + 4), not a measured Codex breakpoint. Returning to sufficient
width restores the retained split unless the user explicitly requested expansion.
Neither presenter nor its supplied control is replaced, and unsent input survives.

Ordinary and file-preview headers consume actual presentation. A compact restore
action is disabled, while its real close action restores the primary pane. File
preview cleanup observes the separate explicit `IsExpansionRequested` value rather
than the layout-derived display value: hiding a preview must not toggle expansion
twice while layout notification is pending. Width feedback from a hidden/full-width
column cannot overwrite the user's preferred split. Detach clears effective state
and subscriptions through the existing ownership boundary.

#### Executed checks, October 14, 2026

Evidence lives outside the repository at
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-narrow-workspace-20261014`.
Only `final-gates` belongs to the final test driver. The earlier root-level driver
started with an older test DLL after a test compilation error; it was explicitly
stopped and is not counted as a passing run. The invalid test API was replaced
with `TopLevel.GetTopLevel` before the final build and driver launch.

- The initial ten-case layout baseline fails eight cases. Subsequent real-consumer
  tests expose preview cleanup's mixed intent/display state and a test assumption
  about a detached primary control. Both are corrected before the final gates.
- Sixteen new pane cases cover both themes, 470/643/644 widths, exact/dynamic
  minimum boundaries, preferred split and explicit expansion retention, rejected
  compact toggle commands, conflicting measure/arrange sizes, notifications and
  initially compact primary attachment. Four real InspectorPane/chat cases cover
  both themes and both ordinary/file-preview headers, keyboard close, retained
  controls/draft, disabled restore and no model submission.
- The final 377-case related regression, all 48 pixel cases and seven architecture
  constraints pass, without failures or skips. The complete App-assembly gate
  finishes at 10:15:46 +08 with driver exit zero: all 4,262 cases pass, with zero
  failures, errors, timeouts, skips or aborts. The final TRX counters and all 4,262
  individual result nodes agree. Driver and xUnit child processes have exited.
  This is a newer complete App checkpoint, not a new complete multi-assembly or
  full-product acceptance run.
- `solution-build-final.log` records a zero-warning/error solution build. Scoped
  whitespace verification exits zero (`format-final-exit.json`), as does the
  complete tracked diff whitespace check. No files are staged.

The final production App DLL SHA256 is
`92C72DB210B7B13CCB5098445756D4D8C98E3F5C5BA46697279190C18B9F0FC7`;
the final App test DLL SHA256 is
`0151A99EA18C952A4DA151C520EAB86CBDE57DF669C9B5F7EEB6D051088872FB`.
These are newer assemblies than the preceding 4,242-case font checkpoint.

#### Native replay and independent audit

The same Debug/JIT App runs with ordinary permission and an independent synthetic
data root. Python passive screen capture verifies foreground ownership, process,
96-DPI geometry and non-uniform pixels. Native replay covers 1920x1032 workspace,
docked and explicitly expanded inspectors, then 1280x820 expanded, restored split
and closed inspector states. Restore brings the existing conversation back; close
restores the floating context card. No draft is submitted or model request added.

`native/inspector-expanded-light-normal-clean.png` is the settled ordinary-window
full-width frame. Immediate input frames, including the file named
`inspector-restored-light-normal-clean.png`, contain Computer Use's transient blue
pointer highlight; the closed-inspector frame retains a hovered tooltip. They
verify state/geometry, not clean shadow, hover or palette equivalence. Do not treat
the helper highlight as a product shadow or claim all frames are clean.

Normal caption close produces App and observer exit zero, both processes disappear
and loopback port 51792 is released. `native/final-closed.audit.json` passes database
integrity, all nineteen table fingerprints, three original artifact verifications,
the four-request history hash and the unchanged light UI-settings bytes. No real
user data, approval setting, provider, JSON adapter, AOT or packaging changes occur.

A fresh passive Codex frame after closing the preview still shows the 526x601
conversation with collapsed navigation, not Appearance or an open sidebar.
This replay does not exercise native automatic compact layout: MainWindow's
980x640 minimum is deliberately unchanged. Headless compact behavior, native
ordinary-size replay and Codex breakpoint equivalence remain separate claims.

`shared-chrome-native-comparison.png` places unscaled common titlebar and rail
crops side by side; the Python script and crop provenance stay outside the repo.
Both sources are ordinary, foreground-verified, 96-DPI captures, but their pages,
sidebar and navigation states are not equivalent. Caption ink at a luminance
threshold of 170 occupies the same 10x1 / 10x10 / 10x10 boxes for minimize, maximize
and close. The darkest reference pixel is 31 in each, versus 93 / 130 / 93 in the
candidate. This exposes a remaining contrast/rasterization mismatch rather than
proving button equality or its root cause. Rail feature differences and the missing
same-state sidebar/Appearance references remain visible; no automated Codex input
is used to manufacture the comparison state.
<!-- /source: NARROW_WORKSPACE_LAYOUT_ACCEPTANCE.md -->


---

<a id="native-dpi-transition-preparation"></a>

## NATIVE_DPI_TRANSITION_PREPARATION.md

<!-- source: NATIVE_DPI_TRANSITION_PREPARATION.md -->
### 原生 DPI 切换诊断准备

#### 本轮进展与边界

上一轮 [GLOBAL_APPROVAL_PRESENTATION_ACCEPTANCE.md](TERMINAL_TOOLS.md#global-approval-presentation-acceptance) 完成了生产呈现修改和回归。
本轮继续原生尺寸错配诊断，不把更多健康的 96-DPI 恢复窗口当作修复。
没有改动产品窗口、布局、渲染、依赖、工具、安全政策、JSON 或发布实现。

外部证据位于 `%TEMP%\MossAgent-dpi-transition-20261014`。
生产 App 为上轮已验证的 Debug/JIT 程序，SHA-256：
`E5C485EC9234AB06F546389E88F1CD92FC54F52A324B12492783C7A7A61BB841`。

#### 源码与待证假设

- 当前 Avalonia.Win32 12.1.3 的 NuGet 清单确认提交为
  `8eeda4f6f546165b3f72e63c9f42247abb306905`，与保存的源树一致。
  `WindowImpl.AppWndProc.cs` 的 blob 为
  `63ee3dbd7c8cbfb809d54927beeb0a5442f51448`。
- `WM_DPICHANGED` 更新缩放、调用 `ScalingChanged`，随后 `SetWindowPos`；
  `WM_SIZE` 才把新的逻辑尺寸交给 `Resized`。TopLevel 的缩放处理本身不更新
  缓存的 `ClientSize`。这些是当前框架源码事实，不是已捕获的失败事件历史。
- 待验证的具体路径：缩放变更时建议的物理矩形是否已经等于现有矩形，导致后续
  没有新的尺寸通知，逻辑客户区与原生客户区长期不一致。必须取得真实 DPI 消息、
  建议矩形、后续 resize/scaling/bounds 顺序及失败状态快照，不能靠 Headless
  模拟、历史错配图片或缺少同状态 Codex 图片直接认定原因。
- 未升级包，未复制框架补丁，未在产品中触发 resize 回调、强制 Width/Height、
  `UpdateLayout` 或 `InvalidateMeasure`。

#### 只读诊断工具

1. 外部启动 hook 只绑定真实 MainWindow，记录 WM_SIZE、WM_DPICHANGED、
   WM_DISPLAYCHANGE 和拖动尺寸消息，以及 ClientSize、Bounds、状态、缩放和 resize。
   新增建议矩形和原生矩形，以区分同物理尺寸路径。最多 2,048 条，正常关闭后输出。
2. hook 不设置 `handled`、不改变 native result、不额外排队 UI 工作，不调整窗口。
   事件与回调在关闭时解除。它从 WindowOpened 开始，不覆盖创建前消息，仍可能影响
   时序，不能把有追踪的成功运行推广为无追踪的故障修复。
3. 独立 CLR 快照只取选中进程第一个观测到的 MainWindow 及其直接拥有的 WindowImpl，
   只读明确白名单的几何、有效标记和状态值。最多扫描两百万对象或二十秒，找到后
   即停止，不声称全堆唯一，不读取聊天、配置、凭据或字符串值，不保存 dump 文件。
4. Python 验证器绑定快照 HWND、PID 和捕获元数据；核对当前原生尺寸与 DPI 后，
   比较缓存 ClientSize、Bounds、arrange bounds、布局有效性和缩放。失效窗口、
   尺寸变化、缺少接受的图像、非白名单快照均拒绝，不用黑屏检测代替布局一致性。
5. Python 截图、诊断源文件、快照与合成 fixture 都在仓库外。生产 App 未改。

#### 已取得的起始证据

- 独立生产实例 PID15400、HWND7669100，普通权限、最大化、96 DPI、1920×1032。
  `baseline-clean.png/json` 是不激活窗口的 PrintWindow 原始帧。
- 首次 screen 图有 Codex 系统通知遮挡，不当作干净像素参考；随后 PrintWindow 帧
  没有这个遮挡，标题栏、主区和输入框均可见。没有自动操作或关闭 Codex 通知。
- `managed-baseline-complete.json` 与 `baseline-geometry-verified.json` 确认原生客户区、
  缓存 ClientSize、Bounds、arrange bounds 都为 1920×1032；渲染缩放 1、DPI96，
  measure/arrange 有效。此次快照在扫描 106,975 个对象后找到目标；这不是全堆统计。
- 独立 fixture 起始审计确认 19 个表的结构和内容、3 个历史 artifact、4-request
  历史以及浅色 UI 配置均未变化。没有执行模型、浏览器、终端或审批设置操作。
- 外部 hook 与快照程序构建均为 0 警告、0 错误。

已经询问用户能否手动临时切换系统缩放至 125%，并提示测试窗口已准备好。
助手不自动更改系统显示设置。真实 DPI 切换尚未取得；当前健康基线不修复旧故障。
本轮启动会话和 PID 必须重新检查，不能因保存的 launch 文件推断进程仍存活，
也不能在进程仍活着时复用 fixture 或重启。

原生尺寸错配根因、125%/100% 切换、同状态 Codex 外观/PI-Desktop 参考和完整产品
验收仍未完成。没有暂存、提交、推送或发布，完整 goal 保持 active。

#### 本次实例终态

后续继续工作时重新核实了 PID15400/HWND7669100 和生产程序集身份；原生 DPI
仍为 96。`continued-geometry-verified.json` 再次确认布局有效且原生/缓存/arrange
尺寸一致。这是健康状态核验，不是旧故障修复。

原生标题栏正常关闭后启动会话取得 App/observer 两个零退出，进程和监听端口均
消失。observer 自身有 1,200 秒生存上限，关闭前已结束，未重启或复用该 fixture。
`final-closed.audit.json` 确认十九个表、三个原 artifact、四次历史请求及 UI 字节
未变化。没有模型请求、工具执行或审批政策/决策操作。

`native-dpi.jsonl` 有十四条记录、三个 `WM_SIZE`/resize 配对，未到输出上限。
没有 `WM_DPICHANGED` 或 scaling 事件，不能建立 125%→100% 故障的真实历史。
新增外部 `analyze_dpi_trace.py` 使用每条记录的缩放做物理尺寸比较，拒绝截断、
非法缩放和乱序；有效 bounds 不匹配仅标为需独立稳定快照的候选，不认定根因。
五项分析器测试通过，其中高 DPI 与旧尺寸示例是合成输入，不是原生转换验收。

后续共享侧栏标题与箭头修正单独记录在 [SIDEBAR_BRAND_FIDELITY_ACCEPTANCE.md](UI_WORKFLOWS.md#sidebar-brand-fidelity-acceptance)，
不改变窗口尺寸、原生通知、渲染、依赖或 DPI 处理。真实 DPI 故障仍保持开放。
<!-- /source: NATIVE_DPI_TRANSITION_PREPARATION.md -->


---

<a id="native-resize-diagnostic-progress"></a>

## NATIVE_RESIZE_DIAGNOSTIC_PROGRESS.md

<!-- source: NATIVE_RESIZE_DIAGNOSTIC_PROGRESS.md -->
### Native resize diagnostic progress

#### Scope and status

This investigation follows [WINDOW_FRAME_SHADOW_ACCEPTANCE.md](UI_WORKFLOWS.md#window-frame-shadow-acceptance). No product UI,
window-state, sizing, rendering, authorization or persistence code is changed.
The native restore defect remains open. Successful replays, a new output path,
diagnostic subscriptions and restarts are not a repair.

[NATIVE_RESIZE_MESSAGE_PROGRESS.md](UI_WORKFLOWS.md#native-resize-message-progress) adds a later, separately versioned native
message replay: nine size notifications match actual client geometry, while
callback-time Bounds can temporarily lag. No actual DPI transition or failed
message history is captured; this does not repair the earlier defect.

Evidence stays outside the repository in
`%TEMP%\MossAgent-native-resize-diagnostics-20261013-130044`. Python performs
passive capture and fixture audits only; native inputs target MossAgent through
Computer Use. Codex is captured passively, never clicked or reconfigured.

#### Reproduction and controls

- The original production assembly, SHA256
  `2265AB5B949EE62B775FCE7D4E03D0809ABE9076E97F6A539E5C755B0748BCD1`,
  reproduces the clipped maximized arrangement after a native 1280x820 restore
  in the independent `plain` fixture. This is a new failure, not just the
  earlier retained image. Capture metadata reports non-black content, but the
  caption and composer are missing from their expected locations.
- The real production App composition and MainWindow, launched by an external
  diagnostic entrypoint, reflow correctly in two `current` restores. A matching
  WinExe/manifest replay in `native` also reflows correctly. The reduced
  `minimal-2` probe removes LayoutUpdated subscriptions, background layout
  observations and live JSON output; its restore still reflows correctly.
- A standalone production entrypoint from the diagnostic build also reflows
  correctly, without telemetry (`plain-new`). Its assembly SHA256 is
  `6E10E54B78BE6C11D036ED5D5B8B39EE6FF920AD15FA6A062B05141F2D8C2CD3`.
  Normalized method-instruction comparison reports no differences in eleven
  application assemblies, after excluding generated regex path-dependent
  names. App embedded-resource hashes and native Apphost hashes match. This
  narrows the investigation, but is not complete binary or timing equivalence
  and does not establish a stale-build cause.
- A delayed read-only hook on the original production binary (`hooked`)
  observes a successful restore. A separate original-binary hook that adds
  only a geometry-filtered TraceListener, with no UI dispatcher work or
  geometry subscriptions (`log-only`), also restores successfully. It reports
  no geometry warning during that successful replay; failed-layout exception
  logging has not been obtained.

Successful telemetry shows native ClientSize updating before Window/Root Bounds,
then the bounds settling at 1280x820 and the caption moving inside the client.
WindowState notifications can arrive before or after this sequence. These are
observations of healthy transitions, not the missing sequence from a failed one.

The first reduced `minimal` attempt is excluded: startup telemetry reports
RenderScaling 1.25 and later 1.0; PrintWindow has trailing black padding and
fails the capture guard. There is no recorded transition to Normal from that
attempted input. It is not native restore or higher-DPI acceptance.

#### Screenshot layout guard

##### Later failed-state geometry snapshot

External evidence in `%TEMP%\MossAgent-live-geometry-20261013` captures the actual
PID14468/HWND18483404 failed state, rather than only a healthy telemetry replay.
The assembly is the standalone diagnostic build SHA256
`6E10E54B78BE6C11D036ED5D5B8B39EE6FF920AD15FA6A062B05141F2D8C2CD3`.
A bounded CLR snapshot reads geometry and callback metadata only: no string
contents, secrets or memory-dump file are saved. Its two-million-object bound
is reached, so observed instance counts are not whole-heap uniqueness claims.

| Observed surface | Size | Scaling |
| --- | --- | --- |
| MainWindow cached ClientSize, Bounds, DesiredSize and arrange bounds | 1536x816 | 1 |
| Native client/window bounds | 1920x1020 | 96 DPI, per-monitor-v2 |
| Client/server composition targets and server layer | 1920x1020 | 1 |

MainWindow measure/arrange are valid, and its PlatformImpl reference matches the
observed Win32 instance. Resized still targets Window.HandleResized;
ScalingChanged retains PresentationSource and TopLevel callbacks. LayoutManager's
owner is PresentationSource, not Window; a false owner-equals-Window check is not
an error. A passive post-snapshot frame still has trailing black padding.
This establishes a real managed/native geometry mismatch, not its event history
or a missing-callback root cause.

The official 12.1.3 WM_DPICHANGED path updates scale, calls ScalingChanged and
SetWindowPos, relying on WM_SIZE for Resized. TopLevel's scale handler does not
update cached ClientSize, while PresentationSource uses the platform client size.
An external Headless-only simulation delivers ScalingChanged, sets platform size
without Resized, then explicitly delivers Resized. All eight simulated 1/1.25
and 1/2 transitions in Normal/Maximized preserve the stale cache before the
notification and converge afterward. The first two attempts are excluded: their
incorrect ordering allows Headless to reset the platform size. Corrected output
is `dpi-event-repro-corrected.json`. This synthetic consistency demonstration
does not prove the native event was lost or repair it. No reflection/callback
invocation is added to product code, and the package is not upgraded.

After independently verifying the native owner, a single Computer Use activation
recovers foreground pixels and restores that old preview to a normal 1536x816
window. `focused-screen.png/json` is valid, but is not a maximized/DPI repair.
The old App then closes normally with exit zero. The later native settings replay
and title correction are recorded in [NATIVE_SETTINGS_INTERACTION_PROGRESS.md](UI_WORKFLOWS.md#native-settings-interaction-progress);
their healthy resizes leave this original root-cause gate open.

The external `verify_layout_anchors.py` checks explicitly selected equal-state
light 1280x820 PrintWindow captures against visible close-caption and composer
send-button anchors. Non-black metadata alone is insufficient.

- Both retained clipped frames have `valid=true`, but fail both anchors:
  mean absolute RGB errors are 14.0961 and 24.9508.
- Six independent successful frames match both anchor regions exactly. The
  separately chosen `minimal-2` frame is the reference, not another test case.
- This gate checks two fixture-specific layout invariants only. It does not
  establish whole-window equivalence, other themes, minimum sizes or DPI states.

#### Data and completion boundaries

All eight diagnostic fixtures retain the original nineteen SQLite schemas and
table contents, three original artifact hashes and four-request history. All
remain Light. Each App and local observer exits normally with code zero; the
original process IDs are gone and observer port 51792 is released. No model
request, browser/terminal session or approval-setting action is performed.

The passive Codex reference is currently the maximized light General settings
page. It is not an Appearance-page reference and does not close that comparison.
The diagnosis does not close native narrow/dark/high-DPI/full-state acceptance,
the resize root-cause gate or the complete product goal. No forced Width/Height,
InvalidateMeasure, UpdateLayout, native hook or diagnostic listener is added to
the product; debug output remains separate from the default executable.

The layout/resource-related App test group passes 19/19, and the complete pixel
assembly passes 38/38 without failures or skips. The solution build completes
with zero warnings/errors, and scoped documentation diff checks pass. All eight
test assemblies are not rerun in this investigation. These checks preserve
the existing deterministic contracts, not native resize or product completion.
<!-- /source: NATIVE_RESIZE_DIAGNOSTIC_PROGRESS.md -->


---

<a id="native-resize-message-progress"></a>

## NATIVE_RESIZE_MESSAGE_PROGRESS.md

<!-- source: NATIVE_RESIZE_MESSAGE_PROGRESS.md -->
### Native resize message progress

#### Scope and safety

This continues [NATIVE_RESIZE_DIAGNOSTIC_PROGRESS.md](UI_WORKFLOWS.md#native-resize-diagnostic-progress) and the failed-state
managed/native snapshot. The original clipping defect remains open. No product
window, layout, renderer, dependency, approval, JSON, AOT or packaging code changes.

Evidence is repository-external under
`%TEMP%\MossAgent-native-messages-20261013-235250`. An external startup hook uses
Avalonia's supported Win32Properties callback to observe only WM_SIZE,
WM_DPICHANGED, WM_DISPLAYCHANGE and enter/exit-size messages. It does not set
`handled`, override the native result, resize, invalidate layout or dispatch
additional UI work. Native calls only read client geometry and DPI. It subscribes
to resize/scaling/state events, retains at most 2,048 geometry-only records and
serializes them on normal close. No window text, model content, credentials,
memory dump or user configuration is inspected by this hook.

The observation starts at WindowOpened, not before native window creation.
Its subscriptions/read calls can affect timing; it is not an uninstrumented
failure-history replay. The production App binary is unchanged. Each external
handwritten C# file contains one type. Native input uses Sky; Python captures
passive foreground pixels and performs bounded read-only fixture audits.

#### Versioned native replay

The ordinary-permission production Debug/JIT App launches with independent
synthetic SQLite backup data, not real user configuration. Its assembly SHA256 is
`04DE522716B323CCBCEB3BB8B7BA0907014EC53F1C5C2B89EA8736DE14CD141F`, matching the
provider/model session checkpoint and remaining unchanged after this run's
solution build. PID17772/HWND15862122 and observer PID12692 are verified.

Native input covers the light maximized workspace, restore to 1280x820, light
Appearance at that size, dark Appearance maximize/restore, a real southeast-grip
resize to 980x640, dark workspace return, light Appearance at 980x640, maximize
and restore again. Eight clean passive screen frames have valid owner, ordinary
privilege, 96-DPI metadata and no trailing black padding. Caption, settings
content and the minimum-size workspace composer visibly reflow. The immediate
workspace restore frame has a Windows snap overlay and is excluded from the clean
frame list; the following Appearance interaction dismisses it.

The actual caption close exits the App and observer with code zero. Immediate
window enumeration races shutdown; the waiting launch session, exit records and
fresh process/port checks establish cleanup, not that first enumeration.

#### Message evidence and interpretation

`native-messages.jsonl` contains 29 records without reaching the output bound.
`message-analysis.json` pairs all nine native WM_SIZE messages with subsequent
managed resize notifications. Every reported managed ClientSize, after scaling,
matches both the raw message dimensions and actual native client dimensions.
The callbacks include Application, Layout, User and Unspecified reasons.

Eight paired callbacks still have previous Window.Bounds while ClientSize is
already current. This is a transient observation: later visible frames reflow,
and the final close snapshot converges to native/client/bounds 980x640. Do not
classify Bounds inequality inside a resize callback as a settled clipping failure
or add forced layout based on it. The earlier failed-state snapshot has valid,
persistently stale measure/arrange geometry and remains separate evidence.

The current machine exposes one 1920x1080 monitor with a 1920x1032 work area.
This replay stays at scale 1 and 96 DPI: zero WM_DPICHANGED and zero scaling
events are recorded. It cannot prove the earlier 1.25-to-1 notification history,
native high-DPI behavior or a missing-resize cause. The next resize-specific
evidence must include an actual scale transition and failed-state message history,
not more healthy 96-DPI restores or a Headless-only notification simulation.

Public Avalonia issue [19431](https://github.com/AvaloniaUI/Avalonia/issues/19431)
describes a 175%-scaling restore rounding defect. That report is not evidence
that it caused this separate 96-DPI managed/native mismatch. The inspected
12.1.3/12.1.4 message-handler comparison does not establish a repair, and no
package upgrade or copied framework patch is applied.

#### Validation and remaining gates

- `tests/native-message-related.trx` passes 43 window caption/resize/frame/main
  layout cases without skips. These tests are deterministic, not native DPI proof.
- `solution-build.log` records a zero-warning/error solution build. No fresh
  complete App/eight-assembly suite or full pixel suite is run in this diagnostic
  slice; the preceding checkpoint remains separately versioned.
- `final-closed.audit.json` passes exact comparison of all nineteen table
  schemas/rows, three original artifact hashes, request history and restored light
  UI settings. No model/discovery request, deletion or real-user data write occurs.
- `cleanup.json` records both zero exits; App/observer processes disappear and
  port51792 is released. The production assembly hash stays unchanged.
- The fresh passive `codex-passive-current.png` still shows the conversation,
  not Appearance. No Codex UI input occurs and no equivalent settings comparison
  is claimed.

Native clipping root cause, real DPI transitions, equivalent Appearance/PI-Desktop
references and the whole-product interaction/theme/size matrix remain open.
No script or screenshot is added to the repository; no staging, commit, push,
default output replacement or release occurs. The complete goal stays active.

[NATIVE_DPI_TRANSITION_PREPARATION.md](UI_WORKFLOWS.md#native-dpi-transition-preparation) records a subsequent production-App baseline,
an enhanced read-only message observer with suggested/native rectangles, and a
bounded owned-window geometry snapshot. Current package/source identity is verified;
actual scale switching is requested from the user, not automated. This preparation
does not promote another healthy 96-DPI baseline to failure-history or repair proof.
<!-- /source: NATIVE_RESIZE_MESSAGE_PROGRESS.md -->


---

<a id="native-settings-interaction-progress"></a>

## NATIVE_SETTINGS_INTERACTION_PROGRESS.md

<!-- source: NATIVE_SETTINGS_INTERACTION_PROGRESS.md -->
### Native settings interaction progress

#### Scope and design checks

This continues [SETTINGS_NAVIGATION_INTERACTION_ACCEPTANCE.md](UI_WORKFLOWS.md#settings-navigation-interaction-acceptance). Native evidence
is version-specific; successful new previews do not repair the earlier resize
defect or complete the product goal.

1. App owns the settings title. Change only the shared settings-sidebar-title
   appearance variant, not individual views or platform-neutral modules.
2. Reuse AppText and existing settings/navigation commands. No new port,
   platform hook, layout workaround or duplicate component is needed.
3. No tool execution, model request, approval policy or persistence schema changes.
   Native checks use independent temporary fixture storage. Python captures and
   audits passively; Computer Use inputs target only the verified MossAgent window.
4. Keep one type per handwritten C# file and existing size limits. The shared
   style and extended shell-layout tests remain below 100 lines each.
5. Require a red/green font-weight regression in both themes at three widths,
   pixel regression and solution build. Align title ink without moving search/row geometry,
   then compare a fresh native title crop with the passive Codex reference.

#### Native replay of the navigation candidate

Evidence remains outside Git at
`%TEMP%\MossAgent-settings-shell-20261013-134013\after`.
The candidate is Debug/JIT, App SHA256
`1D35374501158D3B03DF81A9E47963BE4B0C29C14091F6AB746004A49BC8BFBC`,
launched from `.scratch/settings-interactions-frame-20261013` with independent
data. PID13316/HWND15796302 are checked against the executable and actual pixels.

- Empty search hides all groups and shows the localized empty result; searching
  for Browser leaves only its Integration group and retains Appearance content.
  Clearing search restores the seven items and three groups. A set-value timeout
  is reobserved, not retried blindly; the actual query is confirmed before input.
- Ctrl+N leaves settings for a new, unpersisted conversation. A synthetic unsent
  draft survives settings return and history back/forward. Ctrl+Enter from the
  focused settings search does not submit that hidden draft.
- History back/forward buttons leave settings for the requested conversation.
  An attempted Alt+Left does not navigate: that gesture is not registered. Its
  image is retained but excluded from successful history evidence.
- Appearance renders at 1920x1032 in both themes, restores to 1280x820 in Dark,
  resizes through the real grip to 980x760, switches to Light, maximizes and
  restores to 980x760 again. Caption, content and settings anchors visibly reflow.
  These are successful 96-DPI checks, not a native high-DPI or root-cause repair.
  An attempted outward drag is rejected by Computer Use's window bounds; the
  unchanged 980x760 frame is not mislabeled as a successful 1280x820 resize.
- Passive PrintWindow/screen captures are non-black and have no trailing padding.
  Both the old PID14468 preview and this candidate close normally with exit zero;
  their observers also exit zero. No force-stop or duplicate preview is used.
- Read-only hash audits retain all nineteen schemas and original rows, three
  artifact hashes and the four-request history; UI settings return to Light.
  No new task row or model request is persisted. The observer's bounded lifetime
  expiring without requests is not new scheduler/cancellation acceptance.

#### Bounded reference comparison and title correction

`after/native-shell-comparison.png/json` compares two native, light, maximized,
96-DPI 1920x1032 frames without resizing. The reference remains Codex General,
not Appearance. Rail, sidebar, search and selected-row RGB samples match exactly.
Search solid-color bounds are (60,96)-(307,132), and selected-row bounds are
(60,178)-(307,208) in both frames. Matching these bounded anchors does not imply
matching text, icons, content or the whole window.

The comparison exposes a title-weight difference hidden by palette/geometry-only
checks. At RGB channels below140 in the title-only crop, Codex's Settings title
has 354 ink pixels and bounds (68,61)-(104,78); the previous Moss title has
198 pixels and bounds (69,63)-(104,79). The shared title changes from SemiBold
to Bold while retaining its 18px size and 28px line height. A fresh native crop
shows the title still two pixels too low, so its shared top/bottom margins become
10/14 rather than 12/12. Their total remains24: surrounding row/search geometry
does not move. The first Bold crop still differs in glyph rasterization; neither
the weight change nor a matching ink bounding box establishes exact text pixels.
Six strengthened shell tests fail against the old weight before the change.

#### Corrected-title verification

The final Debug/JIT App in `.scratch/settings-title-fidelity-20261013` has SHA256
`CD5FD637D11AADEF8A0379E56E3234B7B044A1E7E0CF66BBC8E50736B8642BC8`.
The final 42-case shell/search/navigation/frame regression and all 48 pixel cases
pass with zero failures/skips. Solution build has zero warnings/errors. Fresh
Headless frames use actual scaling notifications at1/1.25/2, not resized native
screenshots. TRX/frame evidence remains in the external title-tests and
title-rendered-final directories. The previous complete 5,804-case run applies
to the preceding navigation checkpoint, not this final title assembly; all eight
assemblies are not rerun for this two-setter style change.

The independent title-final fixture launches PID23528/HWND6095994 with the
verified final hash. Passive captures confirm Light/Dark at1920x1032, Dark
restore to1280x820, and Light at the restored size, all at96 DPI and ordinary
privilege. Actual pixels and search/theme controls are verified before accepting
the title frame. Earlier workspace frames fail the solid-region comparison gate
and are not reported as settings references. The first Bold-only candidate is
separately versioned; neither it nor the navigation candidate is the final binary.

`title-final/title-ink-comparison.json` records identical title ink bounds
(68,61)-(104,78) without resizing. Mean absolute RGB difference in the fixed
80x35 title crop drops from about16.7 to11.3; dark ink remains258 pixels versus
Codex's354. This improves weight/alignment but does not establish exact glyph
rasterization. The four shell colors and both solid-region bounds still match.
The comparison PNG is `title-final/native-shell-comparison.png`.

The final preview and observer exit normally with code zero. All observed preview
PIDs are absent, observer port51792 is released, final App hash is unchanged,
and the post-close read-only audit retains all original schemas/rows, artifacts,
four requests and Light UI state. The same preservation checks pass for the
first title candidate. No paths are staged. Python scripts and images stay
outside the repository. Scoped whitespace/diff checks pass.

#### Remaining gates

Complete equivalent Codex Appearance and PI-Desktop references, native high-DPI
transitions, unsaved provider-editor/background-update interaction, other key
surface/state combinations and the resize root cause before product sign-off.
The latest passive Codex rechecks show the workspace, not Appearance, and are
not silently substituted. No Codex input, security/approval setting change,
default executable replacement, JSON/AOT/packaging change, commit or release occurs.
<!-- /source: NATIVE_SETTINGS_INTERACTION_PROGRESS.md -->


---

<a id="navigation-action-acceptance"></a>

## NAVIGATION_ACTION_ACCEPTANCE.md

<!-- source: NAVIGATION_ACTION_ACCEPTANCE.md -->
### Navigation action states

#### Shared ownership

Business pages continue to use `MossAgent.App.Controls`; this change does not
introduce another wrapper library or allow native visual controls. Existing
`MOSSUI001`, `MOSSUI002` and AXAML build gates remain mandatory. Structural
layout primitives are still allowed, but not page-local appearance overrides.

`NavigationActionVariants.axaml`, included by the shared surface styles, owns
task options, project collapse controls and project-section actions.
`ConversationTabVariants.axaml` owns the matching tab-option states. Existing
commands, parameters, selection and archive semantics are unchanged.

| State | Expected behavior |
| --- | --- |
| Hidden | Zero opacity and no pointer interception; keyboard navigation remains available |
| Row hover | Action is visible and can receive pointer input |
| Row keyboard focus | Action is visible without selecting the row |
| Focus and hover leave | Action is hidden unless its menu is open or its tab is selected |
| Selected tab | Options remain visible |
| Disabled command | Reveal does not enable or execute the command |
| Open action menu | Anchor remains visible after focus moves into the popup or outside the row |
| Menu action | Existing command executes once and shared flyout dismisses |
| Escape | Menu dismisses without invoking its action |

#### Regression evidence

`NavigationActionStateTests` uses an isolated Headless dispatcher, real App
styles and shared controls in light/dark themes. Its 36 cases cover actual
pointer hit testing, hover, focus, selection, disabled commands, menu dismissal
and keyboard project toggling; they are not just XML selector assertions.

Before the shared-style changes, 20 of the initial 28 cases failed. After the
fixes and additional Escape/toggle coverage, all 36 passed. The focused
navigation/component subset passed 62 tests.

Headless results establish component behavior, not full-product pixel equality
or native DPI coverage. Native screenshots and interactions require separate
acceptance evidence.

The full serial solution run passed 1,561 tests, including 1,177 App tests.
The solution build completed with zero warnings/errors; scoped whitespace
format verification and one-type/file/method-size checks also passed.

#### Native acceptance on 2026-10-06

An ordinary-permission Windows process loaded a separate synthetic data copy
and the production App/MainWindow, in dark theme at 1280×820. Supported native
input verified project collapse/expand with Tab and Space, switching to a
second conversation, keyboard focus on an unselected tab without selecting it,
opening its options with Space, Escape dismissal without closing the tab, and
restoring hidden options after focus moved into the composer. The menu anchor
remained visible while the popup was open and the pointer stayed elsewhere.

The existing external Python helper captured the confirmed process with
passive PrintWindow, validating exact bounds and rejecting uniform/black frames.
Screenshots and QA files remain outside the repository:

`C:\Users\wzheng\AppData\Local\Temp\MossAgent-navigation-action-qa-20261006-102753`

`tab-action-states.png` contains four same-size, unstretched crops in this order:
idle, unselected-tab keyboard focus, open menu anchor, focus left the tab.
Main-window PrintWindow evidence shows the anchor; popup visibility was also
checked through the native UI snapshot. No message was sent, no task was
archived, and the real application data was not modified.

This native pass does not cover light theme, alternate DPI, all window sizes,
every menu, or one-to-one comparison against Codex. Those remain separate
broader UI acceptance requirements.

#### Engineering self-check

- App owns presentation; the module dependency direction is unchanged.
- Existing shared controls and commands suffice; no new port or tool is needed.
- No authorization, approval, timeout, cancellation, persistence or output
  policy changes are introduced.
- Shared styles are split instead of growing the near-limit surface stylesheet;
  handwritten files retain one type and stay below the engineering hard limits.
- Negative cases prove hidden pointer rejection and disabled-command refusal,
  with focus, menu and theme behavior exercised using real styles.
<!-- /source: NAVIGATION_ACTION_ACCEPTANCE.md -->


---

<a id="preview-icon-acceptance"></a>

## PREVIEW_ICON_ACCEPTANCE.md

<!-- source: PREVIEW_ICON_ACCEPTANCE.md -->
### 文件预览工具栏图标验收

日期：2026-10-10/11。上一轮完整回归已结束，本轮继续修正运行窗口中仍存在的
视觉差异，不以三个图标的局部对照代表整体界面一比一还原。

#### 差异与设计自检

- App 的共享 `AppIcon` 和 `FilePreviewVariants.axaml` 拥有绘制与光学尺寸。
  复用 `AppIconGeometryLayout` 的有限值检查、等比缩放和居中，不新增模块依赖。
- `AppIcon.Drawing` 支持多层矢量资源；有 Drawing 时优先绘制，空 Drawing 不绘制，
  清除后恢复原有 Geometry/Fill/Stroke。未选择绘图资源的其他图标保持原实现。
- 打开按钮仍执行现有 `filesystem.reveal_file` 管线，颜色图标不是一个新的打开
  实现。复制、缩放、刷新、审批、授权路径、超时、取消、输出与审计均不改变。
  没有新增文件读取、进程启动、模型请求、数据库迁移或外部编辑器选择功能。
- 五个涉及的手写 C# 文件经 Roslyn 检查：每文件一个类型，无文件/方法硬限失败。
  图标资源、大小和笔画均在共享样式中，不在业务视图内添加颜色或尺寸。
- 编译门禁原先把非 Control 的 DrawingGroup/GeometryDrawing 也当作视觉控件拒绝。
  现在仅允许它们作为 Styles.Resources、Application.Resources 或 ResourceDictionary
  内的媒体资源；在布局子项中仍拒绝它们，资源内的 Button 等原生控件仍拒绝。
  三个资源位置的正/负测试同时检查测试侧规则和真实 MSBuild XPath，未关闭门禁。
- 新的 Skia 像素测试使用独立 Headless 会话，验证真实黄色、蓝色和背景像素，
  负坐标源的等比居中、绘图优先级、空绘图与恢复 Geometry。没有付费 API 调用。

#### 原像素对照

证据、Python 脚本、QA 宿主、独立合成 SQLite 和 TRX 均留在仓库外：
`%TEMP%/MossAgent-preview-toolbar-20261011`。

- `codex-current.png` 是对当前 Codex 的被动 Python 捕获，原始 1936×1048，含 DWM 外框。
  没有向 Codex/ChatGPT 注入输入或改变设置。
- `before-light.png` 和 `after-light.png` 来自生产 App/MainWindow，普通权限，
  浅色 1920×1032，render scale 1。同一合成图片通过真实授权预览工具加载。
  `toolbar-icon-comparison.png` 只裁取工具栏原像素并拼接，未缩放或对齐内部图标。
- 在固定亮度阈值 180 下，复制图标的墨迹包围框从 16×16 改为 14×14，参考为
  14×14；方直角改为两个圆角复制页面。图片选项从 14×16 改为 12×13，参考为
  12×13。源数据、阈值、坐标和实测结果保存在 `toolbar-icon-measurements.json`。
- 复制图标最初尝试 13 DIP，原生墨迹仍为 15×15；保留 `after-light-trial.png`
  及 `toolbar-icon-trial-measurements.json`。最终共享变体为 12 DIP、一 DIP 笔画。
  复制按钮仍为 32×32，图片选项仍为 34×34；缩小绘制不缩小点击区域。
- 打开按钮复用 14-DIP 图标槽，通过共享五层 Drawing 绘制黄色文件夹、蓝色标记
  和浅色方形底，不再用单色轮廓替代 Windows 资源管理器图标。该资源仅作用于
  打开按钮的前导图标；隐藏的尾部图标及其他按钮不受影响。

#### 验证记录

- 最终定向测试 `focused/accepted-icons.trx`：85/85 通过，无跳过；保留此前四个
  新断言失败的 TRX。失败原因是 AppActionButton 还拥有一个隐藏尾部图标；最终
  按实际前导角色定位并另外断言尾部不使用 Drawing，不用 First 忽略重复。
- 初次 QA 宿主编译因 lambda 的 `_` 参数与弃元混淆而失败；改用具名参数。
  新测试编译阶段还修正了资源扩展 using、异步 dispatch 返回值与 PNG 编码器 API。
  未用产品运行时修改或关闭警告掩盖这些验收代码错误。
- 最终解决方案和 QA 宿主构建零警告、零错误；五个 C# 文件的定向 whitespace
  验证及 Roslyn 单类型/规模审计通过。
- 最终浅色窗口中实际点击图片选项、放大至 125%、再适应窗口。原图确实放大并
  出现滚动，再恢复适应布局；两标签和未发送合成草稿始终保留。证据为
  `light-zoom.png`、`light-fit.png` 及 `final-light.log` 的状态变化。
- 适应窗口后 `IsFit=true`，保留的 `Scale` 仍为 1.25；不能把布局恢复描述为
  缩放数值重置为 1。深色 980×640 窗口实际展开、恢复和关闭右栏，两标签及
  草稿保留，关闭后浮动卡恢复。证据为 `after-dark-narrow.png`、
  `after-dark-expanded.png`、`after-dark-restored.png`、`after-dark-closed.png`
  和 `final-dark.log`。这是 MossAgent 交互证据，不是深色 Codex 同状态验收。
- 首次观察及一次点击后的 accessibility 为空；未重复点击。重新观察取得控件树，
  或从当前返回的截图及截图 ID 执行下一步输入。独立 popup 也由 Computer Use
  观察，不能仅凭主窗口 PNG 推断弹框行为。
- 最终完整回归实际结束，八程序集 4,423/4,423 通过，无失败或跳过：Agent 63、
  App 3292、Visual 8、Tools 316、Windows 137、Browser 76、Infrastructure 165、
  Providers 366。原始 TRX 在 `regression` 下；并非根据局部测试推断全量通过。

#### 被动截图守卫与收尾

- 仓库外 `mossagent_window_capture.py` 实际使用 ImageGrab 屏幕裁片，不是
  PrintWindow。最初 `dark-occluded-invalid.png` 捕获了遮挡目标的屏幕内容，
  明确无效，不能作为深色 QA 证据。激活独立 QA 后重拍的深色图片有效。
- 脚本现在捕获前后均验证目标可见、非最小化，前台根 owner 属于目标（允许
  目标所属 popup），且窗口 bounds 未变化。`--passive` 不主动激活任何窗口。
  该守卫不宣称排除所有透明或 topmost 遮挡，也不把屏幕裁片当成离屏窗口捕获。
- Python 语法检查和 `capture_guard_tests.py` 的七个确定性用例通过：目标与
  所属 popup 成功、外部前台/隐藏/最小化拒绝、被动模式不激活及移动不保存。
- 两个独立 QA 宿主复现遮挡：目标首次实际在前台，得到
  `guard-foreground-native.png`，这是成功而不是拒绝。第二个宿主遮挡目标后，
  对目标 PID 的被动捕获返回 exit 1，记录 `guard-rejected-native.log`，拒绝图片
  不存在。只激活独立 QA 目标后重新捕获 exit 0，`guard-recovered-native.png`
  经检查为真实深色 980×640 QA 内容。未向 Codex/ChatGPT 注入输入。
- 全部六个 QA 宿主均正常终止，exit 0、stderr 为空；最后两个通过各自合成
  数据目录的 `close.signal` 关闭，实际等待进程返回后确认没有遗留 QA 进程。
  用户数据、截图脚本和临时验收工程均不纳入提交；没有提交、推送或发布。

#### 剩余范围

本轮没有打开真实外部编辑器、使用用户数据库或调用真实远端模型。其他工具栏
动作的 Codex 语义、打开菜单的编辑器选择、不同 DPI/字体及深色同状态参考仍需
继续核对。右端刷新按钮仍明确执行刷新，未只把图标换成未知语义的参考按钮。
包围框尺寸相同不代表抗锯齿、轮廓和整个工具栏像素相同。整体产品目标仍进行中。
<!-- /source: PREVIEW_ICON_ACCEPTANCE.md -->


---

<a id="preview-scroll-syntax-acceptance"></a>

## PREVIEW_SCROLL_SYNTAX_ACCEPTANCE.md

<!-- source: PREVIEW_SCROLL_SYNTAX_ACCEPTANCE.md -->
### Settings preview scroll and syntax fidelity

#### Scope and design self-check

- The previous input/hit-target slice completed regression, build and native
  gates. This continuation retains the complete product objective as active.
- App owns the existing read-only AppDiffPreview and shared appearance styles.
  Reuse native ScrollViewer/ScrollBar/RepeatButton/Thumb behavior and AppIcon;
  no custom input handler, execution port or platform dependency is needed.
- No authorization, approval, timeout, cancellation, output, persistence,
  credentials, JIT debugging or Windows-x64 single-file publishing change.
- Keep new test files below 250 lines, methods below 40 where practical, and
  exactly one handwritten type per C# file. Shared markup owns appearance.
- Focused tests must prove idle arrow visibility, real overflow rather than
  forced blank scrolling, independent arrow/thumb interaction, disabled-input
  rejection, theme switching and isolation from ordinary inspector syntax.

#### Current evidence and intended correction

- The original clipboard PNG is no longer present. The retained comparison
  sheet under `%TEMP%\MossAgent-settings-preview-20261009` contains its original
  unscaled 768-by-126 diff crop and 248-by-174 theme thumbnails.
- The reference dark thumbnail intentionally contains a white list card;
  this is not a dark-theme bug and its colors/structure must remain unchanged.
- A fresh ordinary-permission production-composition host reproduces both
  horizontal bars with AllowAutoHide=true, IsExpanded=false and line-button
  opacity zero. At wide width, the forced 384-DIP minimum creates a 1.5-DIP
  overflow even though the synthetic source text fits.
- The reference uses visible muted filled arrow glyphs, not a permanently
  drawn inactive thumb. Remove the artificial minimum; keep the existing
  16-DIP native bar and expose real line/page/drag behavior only when needed.
- Reference-crop token samples and the fresh native frame show distinct
  identifier/type/number hues. Preview-only theme resources must own the
  sampled light palette without changing ordinary CodeKeyword/CodeType brushes.
  Preserve the existing dark palette because no new dark Codex reference exists.
- External screenshots, native host, synthetic data, observer and Python helper
  stay outside Git under `%TEMP%\MossAgent-preview-syntax-20261012`.

#### Validation

- The original implementation fails six of 12 new cases; the six already
  passing native arrow/drag/disabled behaviors remain passing. The focused
  preview/markup replay passes 20 cases and the expanded theme, preview,
  settings, inspector, sidebar and native-control-boundary set passes 388.
  Evidence: preview-baseline/fixed/focused.trx.
- Headless font metrics differ from the real code font. Zero-overflow headless
  cases use a 1,100-DIP window, while the real page confirms no horizontal
  overflow with the unchanged 768-DIP content column. Narrow component cases
  exercise actual overflow without adding a synthetic minimum to production.
- The production-composition page runs without elevation, at measured scale 1
  and a captured 1,920-by-1,032 window. Both 382.5-DIP viewports equal their
  horizontal extents; all four 16-DIP native line buttons have visible 8-DIP
  filled glyphs. Zero-range bars are disabled and their thumbs are hidden.
- Actual page clicks select Dark and then Light, updating syntax/selection
  resources in place. The synthetic preference file saves each selection;
  the original dark-thumbnail white list card remains intentionally unchanged.
- A separate real AppDiffPreview control window at 420 by 250 DIP verifies
  overflow: 286.92-DIP extent versus 208.5-DIP viewport. Clicking the left
  column's right arrow moves only that column to 16 DIP; its left arrow restores
  zero. Dragging the right thumb moves only its column to about 56.90 DIP;
  clicking its remaining track reaches the 78.42-DIP range end. This is a
  control-window interaction check, not a supported 420-DIP full-app layout.
- The first control host retained the main window's maximized state. Its
  observation was not counted as narrow coverage; the corrected external host
  restores Normal before applying control-window dimensions. No production
  window behavior is changed. The passive Python helper's old 300-pixel height
  filter also rejected this live small window; exact-PID passive captures now
  allow it. Neither observation issue was treated as application termination.
- All native hosts close normally with QA_UI_EXIT=0 and empty stderr. Parent
  and child model request counts remain zero; no real account/provider request
  or security/approval-setting interaction occurs.
- Unscaled original-position diff crops compare reference, fresh baseline and
  revised frames. Dominant ink hues for all six sampled token kinds now match
  the retained reference: #751ED9, #BD5800, #751ED9, #BD5800, #008809, #0071EA.
  Pixel counts differ, so this is not a whole-frame/font-raster parity claim.
  Evidence: preview-syntax-comparison.png/json and native-final-assertions.log.
- C# whitespace verification passes; Roslyn confirms one type per changed test
  file, with new files below 250 lines and their methods below 40 lines.
  Existing AppearanceDiffPreviewTests retains its pre-existing 44-line method,
  below the 80-line hard limit.
- Full App regression passes 3,679 cases with zero failures/skips. The serial
  solution build completes with zero warnings/errors; post-build architecture
  checks pass seven cases and the final preview replay passes all 20 cases.
  Evidence: preview-app-full.trx, solution-build.log, preview-architecture.trx
  and preview-post-build.trx under the evidence directory above.

This record does not establish whole-window parity, every DPI/state or complete
product acceptance. There is no fresh dark Codex settings reference; the dark
palette preserves prior behavior rather than inventing reference values.
The held fixed-length SSE diagnosis remains a separate open item.
<!-- /source: PREVIEW_SCROLL_SYNTAX_ACCEPTANCE.md -->


---

<a id="project-editor-viewport-acceptance"></a>

## PROJECT_EDITOR_VIEWPORT_ACCEPTANCE.md

<!-- source: PROJECT_EDITOR_VIEWPORT_ACCEPTANCE.md -->
### 项目编辑弹层优化验收

#### 复现与范围

2026-10-16，在独立 Avalonia Headless/Skia 中加载生产 MainWindow、生产组合根和临时 SQLite。
没有操作、重启或截取用户正在使用的 Codex/MossAgent 窗口，未调用付费模型。

- 24 个目录使旧编辑器增长到 420×1402 DIP；普通窗口与 526×601 小窗口均遮住保存入口。
  修正紧凑侧栏的测试打开方式后，六项尺寸/主题/缩放组合均复现，不把测试夹具问题当成产品问题。
- “设为主要”取得键盘焦点后仍为零透明度且不参与命中；明暗主题两项均复现。
- 编辑后按 Escape 退出，再由侧栏“添加项目”进入，仍然编辑旧项目；两个主题均复现。
- 首版布局在 125% 缩放下，图标和文字因分别取整而相差一个物理像素。
  仅目录行保留连续布局坐标，其他页面、标题栏和窗口的取整策略不变。

#### 设计与安全自检

1. 行为归 App：独立 `WorkspaceProjectEditorView`、现有编辑器 VM 与共享 Styles，依赖方向不变。
   采用 ui-ux-pro-max 的焦点不遮挡、显式字段标签和逃离入口建议，按原生桌面控件实现，
   不机械套用网页 CSS、移动触摸尺寸或宣称通过网页 WCAG 认证。
2. 沿用 `IFolderPickerService` 与 `IWorkspaceRepository`。组合根抽出服务注册方法以替换测试 picker，
   原 `CreateServices(string?)` 签名及默认注册不变；生产仍使用原生目录选择器。
3. 不新增文件工具权限、SQL、进程或网络操作。保存的 30 秒协作取消、版本保护和执行管线保持不变。
   取消按钮只关闭并保留草稿，不承诺回滚已提交的存储操作；保存/选目录期间按钮不可用。
4. 手写 C# 各文件恰好一个类型，布局与样式分文件，未扩张接近硬上限的 App.axaml 或 Catalog。
5. 验证成功保存和创建、目录排序/移除、目录选择取消、空名拒绝与修正、锁定拒绝、忙碌/释放边界。

#### 改动

- 弹层按宿主可用尺寸约束；标题/名称与底部操作固定，仅目录列表滚动。
- 名称显示独立标签及自动化名称；没有目录时显示下一步提示；状态有明确可见性。
- 目录行去掉逐项卡片边框，复用共享颜色、图标、焦点和轻量悬停外观。
- 鼠标悬停或键盘进入行时显示“设为主要”；取消/保存具有相同层级和对齐方式。
- “添加项目”明确调用新建命令，不沿用之前编辑的项目；操作忙碌或释放后拒绝切换草稿。

#### 确定性交互与视觉证据

- 真实 MainWindow、小窗口抽屉、项目弹层和键盘输入，使用独立的生产 SQLite。
- 明暗主题，526×601 与 1280×820，100%/125%/200% 组合；验证末行命中、滚动后保存位置不变、
  行内图标/文字中心与无边框样式。Tab/Shift+Tab 在编辑器内循环，Escape 后可重新创建。
- 新建经 picker 端口返回的真实临时目录并落库；编辑 24 个目录时设定主要目录、移除末项、输入名称、
  保存，再核对项目身份、创建时间、目录顺序和目录列表同步。picker 的取消不改变目录或写库。
- 空名保存保留错误和目录，修正后可保存；项目锁定时保存和添加目录禁用，仍可关闭编辑器。
- 扩展回归发现十个沿用旧文字颜色的断言。在上一轮未改动的候选 DLL 上也复现同样十项失败，
  已与现有高对比度配色同步为精确颜色断言；未为本轮修改生产颜色或放宽断言。

原始 PNG、前后对比、失败及成功 TRX、构建/格式日志、结构审计在
`%TEMP%/MossAgent-project-editor-20261016`，脚本和测试数据均不加入仓库。

#### 本轮回归

- 相关 App 回归 488/488，完整像素测试 300/300，无失败、跳过或中止。
  项目专项属于这 488 项的子集，后续重复专项运行不累计为新增覆盖。
- 最终解决方案构建 0 警告、0 错误，输出隔离到 `artifacts/project-editor-20261016`，
  不覆盖用户原有调试输出。运行时仍为 Windows x64、JIT；未修改发布或 JSON 策略。
- 12 份 C# 的单类型及规模审计、scoped whitespace 与 `git diff --check` 通过；
  最大 C# 文件 287 行、最大方法 45 行，新编辑器 AXAML 62 行。
- 生产输出与两个测试输出中的 App DLL 哈希一致；没有暂存、提交或推送。

#### 验证边界

此处运行的是生产控件和确定性测试输入，不是原生 Windows 目录选择器验收，也不是当前 Codex
同状态、DWM 阴影或逐像素一致性证明。本轮没有发送模型请求，不能代替从更新后的授权目录到
真实文件/命令工具调用的验收，亦不代表全量 App/八程序集回归或完整产品目标完成。
<!-- /source: PROJECT_EDITOR_VIEWPORT_ACCEPTANCE.md -->


---

<a id="project-management-acceptance"></a>

## PROJECT_MANAGEMENT_ACCEPTANCE.md

<!-- source: PROJECT_MANAGEMENT_ACCEPTANCE.md -->
### 项目编辑与原生导航验收

#### 本轮缺口与复现

当前分支为 `feature/initial-mossagent`，已有大量未提交工作，未重建已有配置和执行管线。
前轮共享组件、2034 项测试及构建属于已验证进展，但不能证明完整产品目标完成。
本轮继续检查项目目录与真实运行交互，并在原实现中复现三个问题：

1. 存储异常的原始文字直接进入状态提示，可能包含私有路径或凭据。
2. 保存等待期间输入新名字，旧保存完成后仍清空新草稿并关闭编辑器。
3. 保存等待期间切换到另一个项目，旧保存仍清空另一个项目的编辑状态。

`ProjectEditorSaveRegressionTests` 在修改生产代码前的三项测试全部失败。
后续重复调用测试还复现工具包取消原有请求的问题；共享命令代理先检查 CanExecute，
被拒绝的同步或异步重入不再调用内部命令，也不会意外取消正在执行的保存。

#### 设计与安全自检

- 行为归 App 编辑器、目录服务与命令代理；模块依赖方向未改变。
- 使用现有 `IWorkspaceRepository`、`IFolderPickerService` 端口，不新增数据库迁移，
  不修改目录授权、工具审批或执行管线。界面仍使用共享 Controls/Styles。
- 保存捕获不可变输入快照、项目身份、编辑器版本与草稿修订。成功保存的快照刷新目录，
  但只有同一版本且未变动的草稿才能清空；新草稿保留并明确说明尚未保存。
- 保存和目录选择拒绝重叠，忙碌时命令与目录操作不可用；保存入口显示“正在保存…”。
  保存请求具有 30 秒取消截止时间，取消传递至现有存储端口。取消是协作式的，
  不承诺回滚已提交的写入；底层若未响应取消，不释放忙碌状态去启动重叠写入。
- 编辑器释放取消等待操作、移除集合及内部命令订阅、拒绝迟到的发布和弹窗恢复。
  迟到的目录选择也不能修改其他项目，建议名称不能覆盖已编辑又清空的名字。
- 验证失败使用内部允许列表，存储/目录选择异常使用本地化安全提示，不显示异常正文。
  错误外观复用 `status-error`，没有页面私有颜色、尺寸或阴影。
- 每个新增手写 C# 文件只有一个类型，文件和方法保持在硬上限内。

#### 测试证据

- 新增三项保存回归、七项取消/迟到结果/重复执行测试、三项共享命令契约测试。
- 保留原有项目创建、主目录排序、身份和创建时间保留、运行中拒绝修改及空目录拒绝测试。
- 完整 MainWindow 明暗主题测试继续验证共享项目行、真实目录命令、摘要和编辑入口。
- 正常保存、存储拒绝、无效路径、重入拒绝、取消后重试、关闭后的迟到结果均有覆盖。
- 通过 scoped whitespace 格式验证、`git diff --check` 与单类型/文件/方法结构审计。
  最终测试和构建结果另附在本文件末尾；未暂存、提交或推送。

#### 普通权限原生运行

2026-10-06，外部测试程序加载真实 App/MainWindow 与独立的合成 SQLite 数据，
使用合成密钥和不可达的本机模型地址。没有发送消息或执行工具，没有改动真实应用数据。
脚本、数据、日志与 PNG 均位于 `%TEMP%/MossAgent-sidebar-native-qa-20261006-02`。

##### 明亮普通窗口：1280×820

通过受支持的 Windows 输入 API 实际点击项目标题、摘要编辑入口与保存按钮。
名称通过真实输入控件的 UI Automation ValuePattern 设置，不称作物理键盘输入。
保存后侧边栏、欢迎标题、输入区项目标签与上下文名称同步更新，弹窗关闭；
只读 SQLite 核对确认重命名持久化。继续实际点击项目展开按钮，检查会话隐藏和恢复、
文件夹与箭头状态，以及 `QA_SIDEBAR` 真实状态变更。最后点击窗口关闭按钮正常退出。
对应 `light/project-editor.png`、`saved-project.png`、`collapsed-project.png`、`expanded-project.png`。

##### 最终版暗色窄窗口：980×640

重新构建后在 `dark-final` 使用唯一新窗口绑定，实际打开摘要与编辑器。
将名字置空后点击保存，错误文字以共享红色外观显示，编辑器和目录草稿保留。
修正名字再点击保存，弹窗关闭，工作区相关项目标签同步，SQLite 只读检查确认重试成功。
最后通过关闭按钮正常退出，日志 `QA_EXIT_COMPLETED: 0`，目标进程消失。
对应 `project-summary.png`、`project-editor.png`、`validation-error.png`、`saved-project.png`。

仓库外 Python 被动 PrintWindow 捕获验证原始主窗口尺寸、边界稳定与非黑像素。
新增被动脚本只捕获指定 PID 的主窗口和同进程可见弹出窗口，不激活、移动或输入。
弹出窗口保留独立原始 PNG，并在原始主窗口画布中按实际位置合成，不拉伸：
摘要 320×127，编辑器及错误状态 420×275。`validation-error-popup-0.png` 直接证明
真实错误文字的共享红色呈现。临时中间暗色预览通过关闭信号正常退出，没有残留进程。

#### 仍需继续验收

- 保存取消、存储异常、长时间等待、并发修改和关闭时的竞态由确定性测试验证，
  本轮未逐项通过物理输入制造真实磁盘失败或等待完整 30 秒超时。
- 未操作原生目录选择器、改变授权目录或试图绕过权限；未调用真实付费模型。
- 此处原生保存路径使用 UI Automation 设置值与鼠标点击，不证明键盘文字输入和所有快捷键。
- 被动弹出层合成不能恢复窗口矩形外的 DWM 阴影；不是全桌面合成器像素一致性证明。
- 其他页面、不同 DPI、所有悬停/焦点状态、长任务压力测试和完整 Codex 同状态对照仍需继续。
  提供商、代理、子代理、浏览器与 MCP 的整体跨工作流验收不由项目编辑局部测试替代。
  完整 MossAgent 目标保持进行中。

#### 最终验证

- 完整解决方案测试 2,047 项通过，其中 App 1,558 项；没有失败或跳过。
  最终目录建议名称的草稿版本保护已包含在本次全量回归中。
- 在最后的 C# 修改之后重新构建整个解决方案，0 警告、0 错误。
  命令为 `dotnet build MossAgent.slnx --no-restore --disable-build-servers -m:1 -v:minimal`。
- 补充复测组件边界、共享外观、原生控件拒绝与项目编辑专项，402 项通过。
  业务数据、命令与结构布局的允许路径和违规外观的拒绝路径均保留。
- 修改的 C# 空白格式检查通过；915 个 C#/AXAML 文件的文件、方法和单类型
  规模审计均无失败。`git diff --check` 通过，没有暂存、提交或推送。
- 日志在上述仓库外验收目录：`solution-tests-final.log`、`solution-build-final.log`、
  `component-rules-final.log`、`format-final.log`、`audit-final.log`。
  这些构建和回归结果不扩展前述原生输入、截图和 Codex 同状态对照的验收范围。
<!-- /source: PROJECT_MANAGEMENT_ACCEPTANCE.md -->


---

<a id="project-prompt-responsive-acceptance"></a>

## PROJECT_PROMPT_RESPONSIVE_ACCEPTANCE.md

<!-- source: PROJECT_PROMPT_RESPONSIVE_ACCEPTANCE.md -->
### Project prompt responsive acceptance

The 2026-10-14 follow-up in [COMPACT_WELCOME_LAYOUT_ACCEPTANCE.md](UI_WORKFLOWS.md#compact-welcome-layout-acceptance) supersedes the
three-row compact fallback described below. It preserves the original one-row
behavior while fitting saved empty conversations above the composer and accounting
for the brand offset inside the available height. This document's older results
remain evidence for the earlier assembly, not the follow-up build.

#### Scope: 2026-10-10

`AppProjectPrompt` no longer forces three rows whenever its available width is
below 720 DIP. It measures its three styled children with unconstrained width and
compares their combined desired width with the actual available width. Content
that fits stays inline; longer content retains the existing stacked fallback.

This removes a redundant viewport assumption, not controls or functionality.
Font size, label bounds, project-name ellipsis, alignment, theme, menu appearance
and all shared styles remain unchanged. No allocation or latency improvement is
claimed; this is a content-driven layout correction.

#### Design and safety self-check

1. App owns this synchronous shared-control layout. No module dependency changes.
2. Existing project commands, bindings and flyout remain the integration points.
   Reflow changes Grid definitions only when the layout mode changes; it does not
   recreate the project button, text controls, command or flyout.
3. No file, process, provider, browser or tool execution enters the component.
   Authorization, approval, task cancellation, timeouts, output limiting and
   persistence remain in their existing paths. Missing commands disable the
   action; command CanExecute remains authoritative.
4. The production file has 92 lines and one type; its longest method has 25 lines.
   The new test file has 153 lines and one type; its longest method has 32 lines.
   No extra native component, global cache, package or persistence migration is
   introduced. Debug stays JIT; publishing stays Windows x64 single-file, no AOT.
5. Ten new cases cover styled short headings at 600/664/980 DIP in both themes,
   live short/long-name changes, narrow/wide transitions, retained button identity,
   Tab/Space activation and disabled/missing-command rejection. Existing prompt
   tests retain long-name, flyout ownership and tooltip checks; real chat-view
   tests retain narrow-viewport bounds checks.

#### Regression evidence

- Before the fix, the ten-case run fails six cases on inline-layout assertions;
  four pass. Failures cover the 600/664 DIP short headings and live-name cases.
- After the fix, all 17 focused prompt and empty-session-heading cases pass.
- Focused whitespace verification and the two-file Roslyn type/size audit pass.
- The solution build succeeds with zero warnings and errors.
- The subsequent full run passes 4308/4308 cases across eight assemblies, with
  zero failed or skipped cases. Repository-default `git diff --check` passes;
  the index remains empty and pre-existing worktree changes remain uncommitted.

#### Native evidence

The external QA host uses the real App/MainWindow with new synthetic data and a
loopback-only synthetic provider. It sends no model prompt and never opens the
user database. All three hosts report `QA_ELEVATED: False` and close normally with
window/lifetime exit messages. Passive Python captures preserve native pixels.

- At 980x640, the short draft heading is inline in dark and light themes after
  an actual sidebar new-conversation click. The dark before capture from the
  preceding acceptance run is also 980x640, with the same heading code/styles;
  it shows the unnecessary three rows.
- The unscaled crop `(380,265)-(900,375)` changes from three foreground row bands
  to one. The comparison excludes the icon above the heading and does not resize
  either image. This measures heading reflow, not Codex pixel equivalence.
- Clicking the inline project name opens the existing picker; selecting a
  synthetic project updates both the heading and composer project binding.
- Native light-theme maximize/restore changes 980x640 to 1920x1032 and back;
  the short heading remains inline.
- An 80-character project name stays stacked and ellipsized at 980x640, becomes
  inline at 1920x1032, and returns to stacked after native restore. The retained
  stacked project action still opens the picker; choosing a short project name
  restores inline layout without a window resize.
- Read-only post-exit checks of all three synthetic databases confirm nine tasks,
  one pinned task, zero archived tasks and zero messages. No QA host remains.

Logs, TRX, QA source, Python scripts, raw captures and the unscaled comparison
stay outside Git at
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-prompt-fit-20261010`.

This finishes only this shared-control slice. Whole-page Codex fidelity, other
DPI settings and live provider/tool workflows are not accepted by this change.
No staging, commit, push or publish occurs; the overall product goal stays active.
<!-- /source: PROJECT_PROMPT_RESPONSIVE_ACCEPTANCE.md -->


---

<a id="project-task-preview-acceptance"></a>

## PROJECT_TASK_PREVIEW_ACCEPTANCE.md

<!-- source: PROJECT_TASK_PREVIEW_ACCEPTANCE.md -->
### 项目会话预览与展开收起

#### 设计与必要自检

1. App 拥有项目会话的侧栏呈现。新增独立的
   `WorkspaceProjectTaskPreviewViewModel`，继续使用完整项目快照和现有任务行；
   不截断仓库、搜索、历史或当前会话数据，不改变模块依赖。
2. 默认展示前五个未置顶会话，超过五项才显示“展开显示”；展开后可收起。
   搜索显示全部匹配结果，不提供会隐藏结果的折叠入口；清除搜索恢复此前选择。
   每个项目独立保留本次进程中的展开选择，折叠项目组不重建会话行。
3. 新选择的预览外会话会自动显示。选择先于异步加载、或发生在筛选期间时，
   保留一个待显示标识，数据可用后再显示；用户明确收起和重复选择不反复展开。
   无效、置顶和已有可见条目不触发无关展开。这不是工具调用，无新增授权、
   审批、模型请求、超时或输出通道。
4. 全量集合与预览共享相同任务行对象，使用既有集合协调器，只缓存至多五个
   预览引用；等价刷新不重建行或发送集合 Reset。没有新增事件订阅或全局状态。
   新手写 C# 文件各含一个类型，文件和方法均未超过硬上限。
5. 沿用 `AppButton` 和共享样式，本地化文字进入 SidebarStrings。
   验证边界数量、空结果、置顶/无效选择、筛选与异步加载顺序、独立项目、
   更新/移除、行身份及真实侧栏的键盘展开收起；补充两主题和多倍率像素用例。

现有项目文件夹展开图标已经实现，没有重复新增图标逻辑。没有修改审批政策、
持久化结构、JSON、依赖、原生窗口/DPI 处理、Debug/JIT 或 Windows x64 单文件发布。

#### 原尺寸截图比较

Python 截图、合成 fixture、启动/审计脚本、TRX 和比较图片均在仓库外：
`%TEMP%\MossAgent-surface-audit-20261014`。

参考是 PID16828 的 Codex 对话页，仅被动截取。两应用均为普通权限、96 DPI、
1920×1032；`project-preview-reference-comparison.png` 拼接未缩放的项目组裁剪。
不同项目名称和会话内容不是同状态整页参考，不能由此认定整个侧栏像素一致。

“展开显示”使用同一 RGB 阈值 205，在固定文字区域测得：

| 样本 | 墨迹宽×高 | 全窗左边缘 X | 相对组件顶部 Y | 最常见墨迹 RGB |
| --- | --- | --- | --- | --- |
| Codex | 56×12 | 92 | 197 | 139,141,143 |
| 初版预览 | 52×12 | 91 | 196 | 138,138,138 |
| 最终版 | 56×12 | 92 | 197 | 139,141,143 |

共享变体最终使用 14-DIP 字号、30-DIP 点击高度、32-DIP 左内距和既有
SidebarSectionTextBrush。中间版多出一像素上间距，实测后移除；没有通过缩放
截图来调整墨迹。抗锯齿分布仍不同，以上边界和主色相同不是逐像素等同证明。

最终生产 App SHA-256：
`5A28446B06AC62DF4FD4B1513C3DA6B3CAFE233557D30D5FD94BCD632AA5A48F`。
独立实例 PID8864/HWND5047714，observer PID22912，数据目录为 `verified/data`。
它与中间两次原生实例及其中间版测试结果分别记录，不复用已关闭 fixture。

最终版实机验证包括浅色最大化的五项/全部九项、空格收起、从“最近”打开第八项
并自动展开、1280×820 普通窗口、深色普通窗口展开/收起和深色最大化状态保持。
随后恢复浅色并正常关闭。原生检查均为 96 DPI，健康的普通窗口重排不是历史
原生尺寸错配或 DPI 故障的修复证明。

调整大小后的 Sky 截图缓存出现一次 unknown screenshotId，重新绑定后转用
Python 前台截图核对；最大化后再次刷新完整尺寸基准，避免沿用普通窗口坐标。
原生输入只通过 Sky，未操作 Codex 或审批入口。屏幕浮层不作为干净全窗参考。

三个独立实例均经标题栏正常关闭，App/observer 零退出。最终两个 PID 和 51792
监听均消失；`verified/final-closed.audit.json` 核实合成基线的十九张表、三个原
artifact、四次历史请求及恢复浅色后的 UI 字节不变。初版深色审计仅 UI 主题字节
不同，恢复浅色后和关闭后均全项通过；没有发送模型请求或执行工具。

#### 回归状态

- 首次定向测试的两个真实侧栏案例错误假定 fixture 自带会话；修正合成数据后通过。
- 中间版完整 App 回归为 4,372 通过、一个旧源断言失败；412 项相关组也仅该断言
  失败。它仍要求绑定完整 Tasks，现改为 TaskPreview.Items，其余缩进/颜色断言保留。
- 最终十七项定向检查通过，包含十六项新行为/真实侧栏案例及修正后的源断言。
- 最终完整像素程序集 84 项通过，含六项新增两主题及 1、1.25、2 倍率检查；
  这些倍率是离屏渲染，不是原生系统 DPI 切换。
- 最终完整 App 串行回归 4,373 项全部通过，无失败或跳过；十七项定向检查均包含在内。
- 最终六个非 UI 程序集共 1,538 项通过；加上完整 App 和 84 项像素检查，
  不重复计入定向子集，共 5,995 项通过。完整 solution build 零警告、零错误，
  限定文件格式检查和全工作区 diff check 通过。
- 仓库外 `verify_checkpoint.py` 已核对八个完整测试报告的计数、逐项结果及程序集
  身份、原生 App 哈希、截图进程/尺寸/权限、正常退出和最终数据审计，生成
  `verified-checkpoint.json`。本轮通过结果不覆盖或抹除上述中间版失败。

完整同状态 Codex/PI-Desktop 参考、原生 DPI 故障和完整产品表面/交互矩阵仍开放。
完整 goal 保持 active；没有替换默认启动输出，没有暂存、提交、推送或发布。
<!-- /source: PROJECT_TASK_PREVIEW_ACCEPTANCE.md -->


---

<a id="recent-sidebar-acceptance"></a>

## RECENT_SIDEBAR_ACCEPTANCE.md

<!-- source: RECENT_SIDEBAR_ACCEPTANCE.md -->
### 最近会话侧栏验收

日期：2026-10-10。本轮修正真实侧栏行为，不以局部对照代表完整界面还原。

#### 差异与设计自检

当前 Codex 的“最近”包含当前项目的未置顶会话，与项目分组中的条目同时存在。
MossAgent 原先调用 `SetExcludedProject`，排除整个当前项目，导致只有一个项目
有会话时“最近”完全消失；打开跨项目最近会话时，该条目又从列表中消失。
旧代码和旧测试表达的是不同的产品行为，本轮按实际 Codex 参考修正它。

- App 的现有 `WorkspaceCatalogViewModel` 和 `WorkspaceRecentTasksViewModel`
  拥有目录投影；不新增服务、全局缓存或模块依赖。
- 删除排除项目字段、接口及选择项目时的额外筛选。项目/草稿导航只更新选择，
  不替换或重排最近条目；搜索、更新时间排序、任务去重和置顶排除继续存在。
- 复用现有仓储、行控件、选中命令及置顶/归档/恢复流程，不画无法操作的条目。
  同一会话在项目和最近两处显示时，两处独立行实例都正确表示选中状态。
- 没有引入文件访问、命令、浏览器或模型执行；既有授权、审批、期限、取消、输出
  和审计边界不变。失败的置顶持久化不能提前移除最近条目。
- 手写 C# 仍每文件一个类型，七个本轮 C# 文件经 Roslyn 检查无文件/方法硬限失败；
  目录类反而减少代码。不修改共享视觉度量、数据库或用户配置。
- 保持普通 JIT 调试和 Windows x64 单文件发布，不提交、推送或发布。

#### 确定性验证

新增十个 catalog 用例涵盖当前项目、跨项目打开、项目/草稿导航、行实例及通知、
搜索命中/空结果、置顶持久化拒绝、置顶/取消置顶/归档/恢复及锁定导航。
原生产代码下八项失败、两项通过，证据为 `red/recent-catalog-before.trx`。

原生行布局测试原先假定只显示四行；现在检查六行的确切角色、两处选中状态、
唯一置顶条目和每个最近条目的唯一投影。原有按钮 28×28、行高 30、字体 14、
文字和选项按钮垂直中心及不越界断言全部保留，覆盖三种宽度及明暗主题。

首次完整回归暴露三个旧语义 cases：一个重载测试仅允许其他项目进入“最近”，
两个自动任务页面导航测试假定整个侧栏只有一个任务行。更新后分别检查完整
时间顺序/搜索/选择，以及按项目或最近角色准确定位行，没有把 `Single` 换成
忽略重复的 `First`。另加两个最近入口返回工作区的渲染/键盘输入用例，保持
草稿、零模型请求和原有拒绝导航断言。

首次失败结果保留在 `final-regression`；不能把其中通过的 3,278 项当作全量通过。
更新后的最终专项 `accepted-focused` 为 100/100 通过，无跳过。
第二次完整回归的 `accepted-regression` 中，一项既有启动隔离测试在删除临时
目录时出现占用错误，其余 App 3,282 项通过。该启动测试原样定向重跑 1/1 通过，
证据为 `startup-recheck`；本轮没有修改其代码，也不据重跑通过宣称占用问题
根因已修复。所有失败证据都保留，最终完整回归另用独立目录记录。

最终 `verified-regression` 已正常结束，退出码 0。解决方案构建为零警告、零错误；
八个测试项目串行运行，项目内部保持默认并行。八份实际 TRX 共 4,414/4,414 通过，
无失败、无跳过：

| 测试项目 | 通过/总数 |
| --- | --- |
| Agent | 63/63 |
| App | 3,283/3,283 |
| App.Visual | 8/8 |
| Tools | 316/316 |
| Platform.Windows | 137/137 |
| Browser | 76/76 |
| Infrastructure | 165/165 |
| Providers | 366/366 |

这次完整回归包含原样保留的启动隔离测试；最终通过不改变前述临时目录占用
根因尚未修复的限制。专项 100/100 与完整回归分别保留，不能重复相加。

#### 原生截图与交互

证据根：`%TEMP%/MossAgent-recent-sidebar-20261010`。所有 Python 脚本、宿主、
独立 SQLite、合成数据、截图和 TRX 均留在仓库外。宿主运行生产 App/MainWindow，
普通权限，render scale 1；提供商仅为不可达合成地址，没有模型请求或工具执行。

- Python 被动捕获当前 Codex，得到原始 1936×1048 PNG；仅裁去四周八像素窗口外框，
  得到 1920×1032 client。没有操作 Codex/ChatGPT 的界面或安全设置。
- `before-light.png` 与 `after-light-resting.png` 是同尺寸、同合成数据及同置顶选择
  的原生浅色窗口。前者缺少整个“最近”，后者显示三条当前项目未置顶会话。
- `compare_recent.py` 不缩放或重新对齐内部元素，仅裁取相同 client 坐标的侧栏。
  `recent-sidebar-comparison.png` 的三列为 Codex、修改前、修改后。
- 宿主没有参考中的第二项目会话及其他真实最近会话，所以后续项目和最近标题
  的 Y 坐标不同。该图证明信息/行为修正，不证明全部文字、行距或像素相同。
- 浅色 1920×1032 实际点击最近条目，项目与最近两处同时选中，标题/审批上下文
  切换到目标任务，最近列表仍在。再实际打开 know 项目、Escape 关闭摘要，然后
  从最近跨项目重新打开原会话，最近列表仍完整。证据为
  `after-light-project-switch.png` 和 `after-light-cross-project.png`。
- 深色 980×640 实际滚动侧栏到底部并点击最近条目，三条最近会话可见，选中行和
  对话标签正确更新，列表没有消失。证据为 `after-dark-narrow-scrolled.png` 和
  `after-dark-narrow-selected.png`。这不是与深色 Codex 逐像素比较。
- 一次浅色点击后立即读取 accessibility 得到 null，脚本因此报读取错误；没有
  据此重复点击。重新观察和被动 PNG 确认会话已经切换，之后才进行下一项输入。
- 两个交互宿主通过 Alt+F4 正常关闭，保留进程句柄的启动器确认退出码 0；
  最后的同状态截图宿主通过独立关闭信号正常退出 0，stderr 为空。
  基线宿主只有关闭/生命周期返回日志和进程消失证据，不伪造其未保存的退出码。

#### 尚未完成

未进行真实远端模型或用户数据库操作。所有页面的像素匹配、各 DPI/字体、
完整设置/自动任务/检查器和跨工作流验收仍分别需要继续；完整产品目标保持进行中。
<!-- /source: RECENT_SIDEBAR_ACCEPTANCE.md -->


---

<a id="response-content-fidelity-acceptance"></a>

## RESPONSE_CONTENT_FIDELITY_ACCEPTANCE.md

<!-- source: RESPONSE_CONTENT_FIDELITY_ACCEPTANCE.md -->
### 共享回复正文与渲染精简验收

#### 范围与设计自检

2026-10-10，本轮只修改 App 的共享回复组件、样式及外观编译门禁。
不改模型协议、工具执行、SQLite、用户配置或 Windows x64/JIT/单文件发布方案。

1. 职责在 App/Controls、Styles 和 App.Analyzers；不改变模块依赖方向。
2. 复用 `AppResponseContent`、`MarkdownSelectableTextBlock` 及已有解析后的 Blocks，
   不添加新的应用端口、复制命令或页面模板。
3. 不增加文件、进程、网络或工具执行入口；授权、审批、取消、超时和输出预算不变。
   原生验收通过仓库外宿主的合成数据库，不访问用户默认数据，不发送消息。
4. 手写 C# 文件保持单类型及 500 行上限，方法保持 80 行上限；AXAML 仍使用共享包装控件。
5. 回归覆盖正文/列表布局、明暗主题、窄宽窗口、行内代码选择与动态样式，
   并包含禁止业务代码和 AXAML 绕过共享外观的反向测试。

#### 根因与实现

- 原 `controls|AppItemsControl.chat-blocks ContentPresenter` 选择器不匹配包装控件
  保留的原生 ItemsControl StyleKey，实际段落没有期望的间距。移除此失效规则，
  改在共享 `Grid.response-text-block.body` 根节点设置语义间距，不对页面打补丁。
- 普通正文及列表前缀统一为 14-DIP 字号、23-DIP 行高；前缀不再使用 muted 外观，
  列表文本槽为 28 DIP。普通段落 Margin 为 `0,8,0,2`，列表不额外叠加间距。
  compact 正文维持 12.5/19，前缀同步相同尺寸。
- 行内代码保留原生 Run，实现整段连续选择，不使用不可选择的嵌入卡片。
  字号为普通 12、compact 10.5，底色复用主题资源 `CardSubtleBackgroundBrush`。
  字体/字号/背景变化只更新当前代码 Run；替换 Markdown 时清空旧 Run 跟踪。
- 渲染直接枚举 Markdig 段落，不再为每次流式文本刷新建立临时段落数组；
  链接复用已有容器创建逻辑。保留无段落时的原文回退、段落换行及嵌套强调/链接行为。
  这是分配和重复实现的精简，不声称未经测量的端到端性能百分比。
- `CodeFontFamily`、`CodeFontSize`、`CodeBackground` 纳入既有 AXAML 与 C# 外观门禁。
  反向回归覆盖直接赋值、SetValue、Bind、ClearValue，以及普通/所有者限定属性、
  属性元素和在其他包装控件上的所有者限定写法；共享实现和文本选择属性仍允许。

#### 失败基线与专项测试

- 首批 10 项正文回归在修复前全部失败：字号仍为 15、compact 段落缺间距，
  732-DIP 文本列中的对照长路径错误换成两行（48 DIP），而非单行 23 DIP。
- 校正测试的项目 XML 查询后，外观门禁基线为 6 失败、1 通过：三项新代码外观属性
  可被业务 C# 和 AXAML 覆盖；合法共享实现及文本属性已通过，不把测试查询错误当产品缺陷。
- 最终专项组合 133 项通过，包括新增正文 12 项、外观门禁 7 项及渲染行为 5 项。
  全文选择包含代码内容；明暗主题切换及代码样式变化保持选择，旧 Run 不再被更新。
  多段落回归对选择文本规范化行结束符，保留 Windows 原生 LineBreak 的 CRLF 行为。
- 完整回归首轮暴露一条仍要求失效 ItemsControl 选择器的旧样式测试；
  更新为要求新的共享段落规则并禁止旧规则回归，未恢复无效样式来迎合断言。

#### 原生像素和交互证据

证据保存在仓库外 `%TEMP%/MossAgent-main-fidelity-20261010-1530`，脚本、截图、
临时宿主、日志和合成数据库不进入仓库。

- 参考为当前 Codex 的被动屏幕截图（1936×1048），仅裁去四周各 8 像素，
  得到 1920×1032，不缩放图像。没有自动操作 Codex/ChatGPT 的 GUI。
- MossAgent 普通权限的原生 MainWindow 使用相同回复文本。浅色最大化 1920×1032，
  首段起点 `(372,200)`，三条列表文本起点 `(400,225)`、`(400,248)`、`(400,271)`，
  尾段 `(372,302)`，长路径单行高度 23 DIP。正文间距为 25、23、23、31 DIP。
- `body-final-comparison.png` 为参考、修改前、修改后三联原始像素裁片。
  参考墨迹行起点为 337、362、385、408、439；修改前出现六段墨迹，路径错误另起一行。
  文字墨迹边缘受字体光栅化影响，不把它当布局基线或全页像素一致性评分。
- 实际鼠标收起/展开检查器后正文重新布局；确认输入框焦点后键入合成草稿，
  展开检查器后截图仍显示草稿，未点击发送。预览消息数量始终为 2。
- 优化后的浅色实窗通过鼠标定位段落，再以真实 Ctrl+A 全选；仓库外宿主仅观察状态，
  记录 SelectionStart=0、SelectionEnd=77，SelectedText 包含完整路径和段落，未直接设置选择。
- 深色 980×820 实窗验证检查器展开和收起后的正文重排。展开时正文宽 296 DIP、
  列表文本宽 268 DIP；长路径正常换行，不能把此窗口描述为长路径仍单行。
  代码背景实际为浅色 `#F4F4F4`、深色 `#2F2F2F`。

#### 验证边界（正文排版轮次）

- 最终完整七个测试项目共 4,075 项全部通过，0 失败、0 跳过，其中 App 为 3,088 项。
  相比本轮起点新增 24 项。完整解决方案构建 0 警告、0 错误。
- 七个相关 C# 文件通过定向格式检查和 Roslyn 单类型/文件/方法规模检查；
  三个 AXAML 文件分别为 54、62、445 行，未超过上限。14 个相关文件通过
  相对本轮起点的空白检查，不把工作区已有修改混入本轮审计。
- 优化后的三个原生预览均正常关闭并确认退出码 0，没有遗留本轮预览进程。

- 行内代码底色仍为 Run 的矩形背景，尚未复现参考的圆角及内边距。
  原生选择状态已确认，但本次截图中没有清晰的选择高亮，不能声称其外观已与参考一致。
- 正文水平起点与参考仍约差 1 像素；两边真实历史和检查器内容不同，
  未证明整个窗口、所有回复结构、字体配置、DPI 或所有尺寸达到一比一。
- 本轮未验证真实付费模型、浏览器、子代理等端到端流程；原生窗口使用合成离线数据。
  关闭预览通过宿主的关闭信号，不能描述为物理点击关闭按钮的验收证据。
- 没有暂存、提交、推送、发布或修改用户默认数据，完整产品目标仍未完成。

#### 后续：文本选择与门禁精简

以下为 2026-10-10 后续轮次，修复上述选择高亮和矩形代码背景问题；
设计自检仍沿用本文首节。职责限于 App 的共享文本呈现、外观门禁和测试，
不新增工具、端口、网络入口、持久化或运行时订阅。

##### 根因与改动

- `AppSelectableText` 保留具体 StyleKey，却没有显式应用原生选择主题。
  选择起止正常但 SelectionBrush 为 null，普通、Markdown、语法高亮均受影响。
  共享样式为整个派生控件族复用原生 SelectableTextBlock 主题，仍保留具体字号选择器。
- 仅恢复主题不足以解决行内代码：Run.Background 在文字绘制阶段覆盖原生选区。
  代码保持普通 Run；单独跟踪 UTF-16 起点，通过原生 TextLayout 的真实范围绘制
  2-DIP 垂直内边距、3-DIP 圆角背景，然后调用原生选择和文字绘制。
  Emoji、嵌套强调、CRLF 和窄窗软换行不依赖手写字符宽度或坐标估算。
- 绘制复用框架传入的 origin，不重写 padding、纵向对齐或布局舍入计算。
  每次绘制只读取一次代码几何属性和 TextLayout；无代码时不建立额外绘图变换。
  替换 Markdown 时释放旧背景跟踪，样式更新不重建文本或改变复制内容。
- 选择画刷、代码内边距和圆角纳入现有 C#/AXAML 外观门禁，功能性选择属性仍允许。
  16 个共同拥有 Content、Padding 和几何的组合组件改为一份类型名单；
  特殊控件的边界保持独立。门禁文件从本轮起点 159 行降至 133 行。

##### 专项与实际像素

- 修复前 6 项原生选择主题回归全部失败；选择画刷门禁的 4 项反向回归全部失败。
  背景修复后的正文、代码、工具、文件预览和子代理组件专项 168 项通过。
- 新增 8 项 Skia 像素回归，覆盖明暗主题、Emoji 前缀、嵌套强调、CRLF、多代码段、
  长代码软换行、全选、反向部分选择、圆角与内边距、背景更新及旧文本清理。
  与选择门禁和既有外观分析器组合的 60 项专项全部通过。
  测试用透明文字和区分色隔离背景绘制，不以字体抗锯齿像素作为稳定 golden。
- 新像素测试首轮暴露测试宿主问题：框架静态 Underline 装饰属于首次创建它的
  Dispatcher，无法跨独立 Headless 会话直接绘制；最终嵌套用强调容器覆盖，
  不修改生产链接行为。圆角采样避开半透明抗锯齿边缘，重排显式改变文字可用宽度。
- 首次全量回归进一步暴露 Headless/Skia 的字形后端缓存冲突：8 项像素测试出现
  HeadlessGlyphRunStub 到 Skia GlyphRunImpl 的无效转换，其他 4,089 项通过。
  像素回归移入独立的 `MossAgent.App.Visual.Tests` 测试进程，避免切换同一进程的
  绘图后端；共享应用资源宿主移至 `tests/Shared`，两个项目链接同一份源码，
  不复制初始化、不互相引用测试程序集，也不改变生产或其他测试的后端。

仓库外证据目录为 `%TEMP%/MossAgent-text-selection-20261010-1620`。
预览使用真实 MainWindow、普通权限和独立合成 SQLite；离线供应商未发送消息。
Python 仅按明确预览进程 PID 被动截图，没有自动操作 Codex/ChatGPT GUI。

- 浅色最大化 1920×1032：真实鼠标聚焦正文、Ctrl+A 选择 0–77，随后 Ctrl+C；
  宿主只写入 MatchesSelectedText=true、SelectionLength=77，不保存剪贴板内容。
  代码和普通文字均显示蓝色高亮，没有灰底遮挡；反向鼠标拖选记录起止为 73–28。
- 深色 980×820：正文宽 296 DIP、高 69 DIP，行内路径随三行选区呈蓝色；
  真实 Ctrl+A/C 复制比对同样通过。关闭检查器后正文宽 596 DIP、高 46 DIP，
  正常重排；此次面板切换清空了选区，不声称选区跨重建保留。
- `selection-comparison.png` 是失败基线、仅主题修复、最终修复三联原像素裁片。
  同一代码内部 370×12 区域蓝色像素为 0、0、2,113；最终旧灰底像素为 0。
  这只验证选区覆盖代码背景，不是全窗口相似度评分或性能测量。
- 绘制属性读取精简后重新编译普通权限预览，再次真实 Ctrl+A/C 并验证复制一致。
  同一代码区域与精简前最终版本逐像素相同，未以缓存精简掩盖视觉回退。
- 最终三个预览均正常退出码 0，没有遗留本轮预览进程；消息数量始终为 2。

##### 最终验证

- 完整解决方案构建 0 警告、0 错误。八个测试项目共 4,097 项全部通过，
  0 失败、0 跳过，其中 App 为 3,102 项、独立像素回归为 8 项；相较本轮起点新增 22 项。
  最终每个项目使用独立 TRX 目录，逐个计数核对，不依赖被覆盖的首轮结果文件。
- 隔离后的 App 最终专项 70 项、像素回归 8 项均通过；没有通过禁用或跳过测试修复冲突。
- 九个相关手写 C# 文件通过定向格式及 Roslyn 单类型/规模检查，最长方法 63 行；
  两个 AXAML 文件为 64、223 行。修改、新建及移动文件均通过本轮空白检查。
- 可复现命令为 `dotnet build MossAgent.slnx --no-restore`、
  `dotnet test MossAgent.slnx --no-build --no-restore`；像素测试由独立项目进程运行。
  最终验收逐个项目执行后一命令等价的全量回归，以保留八份独立 TRX 证据。

原先正文起点、全页内容、所有 DPI/字体和端到端功能的验证边界仍有效，
不能据此声称 Codex 全界面已一比一还原。未改 Windows x64/JIT/单文件发布方案，
未暂存、提交、推送或发布，完整产品目标仍未完成。
<!-- /source: RESPONSE_CONTENT_FIDELITY_ACCEPTANCE.md -->


---

<a id="settings-catalog-viewport-acceptance"></a>

## SETTINGS_CATALOG_VIEWPORT_ACCEPTANCE.md

<!-- source: SETTINGS_CATALOG_VIEWPORT_ACCEPTANCE.md -->
### Settings catalog viewport acceptance

#### Confirmed failure and correction

Proxy and MCP settings measured their catalog cards in an Auto-height row. A
vertical StackPanel then gave the ListBox unbounded height until its fixed
560-DIP cap, regardless of the actual page viewport. At 601-DIP window height,
the bottom of the scrollbar and list could remain outside the visible page.

All eight original cases fail: both pages, Light/Dark, 486/1024 DIP wide. At the
compact width the list occupies `(37,202,402,560)` and extends to y=762; the wide
case occupies `(152,160,710,560)` and extends to y=720. Neither fits a 601-DIP page.
The failing TRX and original rendered frames are retained without replacement.

Both pages now use Auto header and remaining-height catalog rows. A finite Grid
inside the card constrains the existing ListBox, while a shared top-aligned card
variant preserves natural empty/single-row height. The existing 560-DIP maximum
is moved into one shared ListBox variant; it is a ceiling, not a forced height.
Native scrolling, item virtualization, selection, row commands and row appearance
are retained. No manual item truncation, forced layout or custom scroll handler
is added.

Status feedback now uses the existing shared page-header status slot, including
its 96-DIP scroll bound and complete keyboard-readable text. Proxy error styling
retains its error binding; MCP retains its existing informational presentation.
Modal editor status/notice bindings and footers remain unchanged.

#### Design self-check

1. Ownership stays in App view layout and `SettingsLayoutVariants.axaml`; there
   are no new production C# types, module dependencies or platform APIs.
2. Existing AppCard/AppListBox/AppSettingsPageHeader wrappers and commands are
   reused. Page geometry remains structural; appearance variants remain shared.
3. No new production I/O, approvals, tool execution, cancellation policy, proxy
   transport, configuration schema, provider behavior or credential handling.
4. The modified AXAML files are 96/92/74 physical lines. Five new C# test files
   are 149/103/96/93/83 lines, with one type per file and methods below 80 lines.
5. Thirty-item catalogs prove the failing path; empty/single-item, repeated
   resizing, keyboard selection, pointer hit testing and actual row actions
   protect the successful path. Existing safety suites remain authoritative.

The ui-ux-pro-max Avalonia guidance is applied by retaining semantic selectable
lists and native virtualization rather than replacing them with hand-built rows
or hiding items. This is not a redesign of Codex's established visual direction.

#### Production UI coverage

The focused run passes 47 cases, including all 36 new cases:

- Twenty viewport cases cover two pages/themes/widths, natural empty/single-row
  height, create/cancel and five repeated compact/wide size transitions.
- Eight mutation cases edit the last row using real text input and Save, verify
  the persisted target in probe repositories, then invoke Test/Enable and check
  target identity, unchanged unrelated items and visible status feedback.
- Eight complete MainWindow cases use production composition and isolated SQLite,
  with 30 proxies and 30 disabled MCP profiles. At 526/1280 by 601 DIP in both
  themes, the last row's actual hit target is visible; keyboard edit/save reaches
  SQLite with the same ID and unchanged non-name fields, without adding records.

The initial candidate's focus assumption and the expanded fixture's assumption
that a scrolled-off first item remains realized are corrected in test helpers.
Keyboard traversal now starts on a realized native item and uses End; all
original containment/hit/identity assertions remain. These are fixture corrections,
not additional product defects. Their original candidate logs are retained.

#### Evidence boundaries

Evidence root:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-settings-catalog-20261016`

`baseline/` and `baseline.trx` retain the eight failing page states. `shell/`
contains corrected page, last-row, status and complete-window renders.
`proxy-before-after.png` and `mcp-before-after.png` compare equal-size page renders;
the labels explicitly identify them as off-screen images.

Tests use synthetic data and independent Headless sessions. The MCP profiles are
disabled in the real-composition cases; no external MCP process is started. The
probe tests use a recording proxy tester and deferred MCP manager, not real
network connectivity. Existing authenticated transport tests are separate coverage.
No existing Codex/MossAgent window is operated, captured or restarted. Native
screenshots remain user-postponed; these renders do not prove native DPI behavior,
same-state Codex parity, the separate native resize fix or full-product completion.
Windows x64/JIT single-file publishing, AOT/JSON choices and user data are unchanged.
Nothing is staged, committed, pushed or released.

#### Final regression and coverage audit

The first same-process full App attempt is interrupted after the tool host closes
its output. Its session is subsequently missing and no dotnet/testhost process
remains; no final TRX is produced. `app-full.log` retains an observed untouched
`SidebarFilterAllocationTests` project/matching failure: zero bytes expected,
1,200 measured. This is not a successful full run, a proved application hang or
a causal allocation fix. The later batched pass does not erase that observation.

Fresh discovery lists 4,784 cases across 581 App test classes. Six disjoint
class-prefix partitions run every discovered class; all 4,784 cases pass with no
skips or aborts. `coverage-audit-final.json` verifies all 4,784 distinct TRX test
IDs and exact per-class discovery/result counts, with no class overlap or omission.
Fourteen display-name groups are truncated duplicates, so display-name uniqueness
is not mistaken for test identity. The 36 new cases are included, not added again.
This verifies the complete App case set across partitions, not one uninterrupted
same-process all-green run or resolution of the original allocation observation.

| Final check | Result |
| --- | --- |
| Complete App case set, six partitions | 4,784/4,784 |
| Complete pixel suite | 188/188 |
| Complete Providers suite | 542/542 |
| Infrastructure proxy/HTTP subset | 20/20 |
| Architecture subset | 7/7 |
| Solution build | Zero warnings/errors |
| Scoped whitespace verification | Passed for all six touched C# files |
| Source/document hygiene | Passed, including untracked files |
| Assembly identity after final build | All eight recorded hashes unchanged |

The three runtime App copies are identical. Baseline/focused/partitioned results
are not summed as independent new tests. The final table represents 5,541 checks
within the stated suite/subset scopes, not a new complete eight-assembly total.
The historical 6,495 eight-suite checkpoint remains historical. Other complete
non-UI suites and postponed native/Codex acceptance are not rerun by this slice.
Final logs, discovery/partition manifest, TRXs, build/format logs and before/after
hash CSVs stay in the external evidence root. The product goal remains open.
<!-- /source: SETTINGS_CATALOG_VIEWPORT_ACCEPTANCE.md -->


---

<a id="settings-dialog-reopen-acceptance"></a>

## SETTINGS_DIALOG_REOPEN_ACCEPTANCE.md

<!-- source: SETTINGS_DIALOG_REOPEN_ACCEPTANCE.md -->
### Settings dialog reopen acceptance

#### Native diagnosis: 2026-10-12/13

The current production App was exercised with a copy of a normally closed,
synthetic concurrency database. No user database or real provider account was
used. Dark theme survives a normal close/reopen, completed task B restores its
tool activity and final reply, and opening the browser inspector does not restart
an external browser or replay tools. Passive Python captures record 96-DPI
windows at 1920x1032, 1280x820 and 980x640.

The narrow model editor exposed a real lifecycle defect: after scrolling its
body, cancelling and opening it again, autofocus brings the first input into
view while leaving its preceding label above the viewport. The editor therefore
starts with a clipped `模型 ID` label instead of its initial layout.

The corrected headless baseline reproduces a 20-DIP retained offset in all six
light/dark and window-size cases, plus the nested-scroller case. Seven cases fail
and four preservation cases pass before changing production code. An initial
test-fixture failure attempted to reparent inputs without first removing them
from their original panel; that test-authoring issue is corrected separately.

#### Design self-check

- App's shared `AppSettingsDialog` owns the body viewport and its open/close
  lifecycle. The existing styled open property and shared template suffice;
  no port, dependency or alternative dialog implementation is introduced.
- A new open session resets only the template-owned body before visibility and
  autofocus. Returning to an already-open page retains its position and remembered
  field. Explicit focus chosen after opening can still scroll to a later input.
  Nested content scrollers keep their own positions.
- No authorization, approval, task execution, timeout, cancellation, output,
  persistence, HTTP or provider operation changes. Existing focus revision guards,
  availability checks, host subscriptions, Escape and close-command ownership stay
  in place. No additional event subscription or global state is introduced.
- The template continues using shared visual wrappers and appearance variants.
  Naming the owned body changes no geometry, colors, typography or footer scope.
  All changed files remain below the 500-line hard limit and keep one C# type.
- Eleven new cases cover reopen, labels, focus/drafts, retained open pages,
  explicit lower-field focus and nested-scroller ownership. Existing dialog,
  editor, focus and component-boundary cases must also pass, followed by a solution
  build and an ordinary-permission native replay.

#### Evidence ownership

Artifacts stay outside Git at
`%TEMP%\MossAgent-current-production-matrix-20261012-235235`.

- The before App's managed assembly hash is
  `9F978AD9F19D31444202274AB415F388859D81698167A45FE43C13AF758B9961`.
  `runtime-files.json` records its module identities; an unchanged App-host EXE
  hash alone does not prove the managed App version.
- `dark-narrow-model-editor.png`, `dark-narrow-model-editor-scrolled.png` and
  `dark-narrow-model-editor-reopened-before.png` record the real reproduction.
- Clearing the synthetic model ID and attempting local save produces actionable
  `请选择供应商并填写模型 ID。` guidance. Cancelling retains both saved models.
  The UI value operation initially reports an observation timeout; a fresh binding
  proves it applied, so the operation is not repeated and the App is not restarted.
- `latest-initial.evidence.json`, `latest-browser-panel.evidence.json`,
  `latest-invalid-model-save.evidence.json` and `before-fix-closed.evidence.json`
  retain task A cancelled/task B completed, 15 successful audits, three verified
  artifacts and no task-owned Chrome/Node process. The synthetic database has no
  scheduled tasks or runs. `permission.json` confirms an unelevated App token.
- The before QA instance exits through its actual close button and restores
  `Theme=System` through the real appearance page. It is not killed or replaced
  because an observation timed out.

#### Repair verification: 2026-10-13

The shared template names its existing body `PART_DialogBody`. Opening a new
dialog session synchronously resets that body's offset before visibility and
autofocus, without scheduling a later reset that could override an explicitly
chosen field. Individual settings pages need no reset handlers. Returning to an
already-open page and nested scroll positions retain their existing behavior.

- The eleven new cases and existing dialog/focus cases pass 34/34. The wider
  settings, provider/model, editor and shared visual-control group passes 779/779;
  architecture constraints pass 7/7. The complete serial App suite passes
  4,043/4,043 in 10 minutes 2 seconds using the retained serial settings. None are
  skipped. Results are in `dialog-scroll-focused.trx`, `dialog-scroll-wider.trx`,
  `dialog-scroll-architecture.trx` and `dialog-scroll-app-serial.trx`.
- The solution builds with zero warnings and errors; scoped whitespace
  verification and repository `git diff --check` pass. The complete visual test
  assembly passes 12/12, with no skipped cases. Source review verifies method
  limits, the architecture test verifies one type per C# file and file limits,
  and the changed shared template parses as valid XML.
  Other functional test assemblies are built, not fully rerun in this repair.
- After the full App run, a final solution build again has zero warnings/errors
  and all eleven new cases pass in `dialog-scroll-final-replay.trx`. The final
  managed App hash equals the native replay's recorded hash. The staged set stays
  empty; no commit, push or publication is performed.
- The repaired production App DLL hash is
  `D60261459E5BE56095641242305D55A464DED6866AD359AD96FE9E7F99E3565C`.
  `runtime-files-after.json` records the modules used for the native replay;
  `permission-after.json` confirms its ordinary, unelevated token.
- At 980x640 and 96 DPI, both light and dark model A editors pass the actual
  open/scroll/cancel/reopen sequence. The first `模型 ID` label is fully visible,
  the first input shows its focus outline, and the saved model values remain
  intact. Footer actions remain accessible while the advanced JSON field scrolls.
  Native Escape also closes the editor and returns to the provider list.
- The dark sequence is repeated for model B, matching the model in the original
  failure screenshot. Compare `dark-narrow-model-editor-reopened-before.png`
  with `dark-narrow-model-b-reopened-after.png`; the retained offset no longer
  clips its first label. The light/dark model A captures use the
  `*-narrow-model-editor-after.png`, `*-scrolled-after.png` and
  `*-reopened-after.png` filenames in the same external evidence directory.
- The test instance restores System theme through the real appearance page and
  exits through its actual window close button. Before/after closed snapshots
  contain identical tasks, all 15 successful audits and all three verified
  artifact records. Live snapshots show no task-owned Node or Chrome process.
  No message, task continuation, approval-policy change or tool replay occurs;
  no saved provider/model changes are made during the repaired native replay.
  Read-only catalog counts remain one provider, two models, two tasks and zero
  schedules/runs; the synthetic `ui-settings.json` persists `Theme=System`.

This native sweep is partial. It does not establish a complete current-version
theme/state matrix, same-state Codex or PI-Desktop pixel equivalence, every visible
control's behavior, all protocols or completion of the full product objective.
<!-- /source: SETTINGS_DIALOG_REOPEN_ACCEPTANCE.md -->


---

<a id="settings-entry-acceptance"></a>

## SETTINGS_ENTRY_ACCEPTANCE.md

<!-- source: SETTINGS_ENTRY_ACCEPTANCE.md -->
### Settings entry and navigation-rail tooltip acceptance

#### Scope and design review

App owns this presentation/input change. The shared navigation-rail appearance
places tooltips to the right with an eight-DIP gap and zero vertical offset;
individual views do not override tooltip geometry. MainWindow binds Ctrl+Comma
to the existing idempotent OpenSettingsCommand, not ToggleSettingsCommand. Help
reuses the localized navigation-rail Settings label. No domain, port, execution
pipeline, approval policy, storage, provider, packaging or JSON behavior changes.

Navigation does not authorize a tool or submit a draft. Existing modal-window
input routing remains authoritative. Deterministic tests retain pending approval
identity, its reachable decisions and the embedded-browser presentation block.
No new IO, timeout, cancellation or output mechanism is introduced. All changed
handwritten source files remain below the hard size limits and contain one type.

#### Reproduction and correction

Evidence and Python scripts are outside the repository at
`%TEMP%/MossAgent-rail-navigation-20261015`. The baseline is the previously built
App hash `3AF85B79BD10B41F3BF13926A96AEA18D2DE6A381A15FEC486AFFD870225FD07`.
In its maximized 1920x1032 workspace, the settings button spans x=8..44 and
y=988..1024. Default pointer placement constrains its tooltip to
(25,997)..(67,1027), covering the center (26,1006). A center click does not open
settings; clicking the uncovered button edge at (15,1006) does. The command
binding itself is therefore reachable, rather than missing. The old App also
has no Ctrl+Comma binding.

Final App hash is
`19564864E0806BD8CD4B4EDFF64E34D493808BF6715276CA9EE4ED2006368458`.
Native captures put the tooltip at (52,991)..(94,1021), leaving eight pixels
between it and the button. Seven captured tooltip states prove the same clearance,
including 526x601 light/dark windows. Center clicks open and close settings;
the settings Home action returns to the conversation. Ctrl+Comma opens settings,
and a repeated chord leaves it open. The real help flyout displays Ctrl+, and
Escape dismisses it. Eight DIP is a non-overlap correction, not a measured claim
about current Codex tooltip placement.

#### Native evidence and boundaries

The ordinary-permission final process produces fourteen passive Python captures,
using PID/executable/hash-checked PrintWindow frames plus only its owned popups.
They cover 1920x1032, 1280x820 and 526x601 at 96 DPI, both themes, pointer page
transitions, shortcut opening/repetition, help dismissal and restored maximization.
Both baseline and final processes exit normally with code zero and empty stderr.
Both audits retain nineteen identical table hashes, zero new artifacts and
byte-identical restored Light/default-font preferences. Only synthetic copied
data is used; no provider request is intentionally submitted or security setting
changed. Approval and modal boundaries are deterministic tests, not new native
approval decisions.

`rail-tooltip-comparison.png` contains unscaled before/after workspace crops.
Title, visual-style heading, palette labels and lower palette rows remain
pixel-identical to the baseline settings capture. The entire palette crop is
not identical: 460 changed pixels occupy the previous theme-dropdown focus
outline, at (1282,338)..(1458,366). That interaction-state difference is recorded,
not hidden by a whole-palette equality claim.

Fresh passive Codex remains version 26.1007.9446 in a 481x601 conversation window,
not an equivalent Appearance state. It is not substituted for the saved full-size
reference. Current-version same-state pixel fidelity, other menus and surfaces,
native DPI transitions and overall product acceptance remain open. The preceding
typography acceptance report remains historical evidence, not a claim that all
its residual differences are resolved here.

#### Test evidence

New cases cover every visible rail tooltip and center-click navigation in both
themes and ordinary/compact layouts; actual Ctrl+Comma key events, unmodified
Comma, idempotence, retained selected page/search/draft, modal owner isolation,
and pending-approval identity. A first baseline run correctly fails eight rail
and four shortcut cases. Two additional failures were an invalid test assumption
that a modal owner changes its IsEnabled property; the test instead verifies the
actual owner and modal input route. A later focused run passes 78/80: two compact
shortcut tests incorrectly look for a search field in a closed drawer. Opening
that drawer before testing its focused input corrects the fixture, not production
behavior. All six shortcut cases then pass on the final test assembly.

The final complete solution rerun executes 6,115 cases: 6,111 pass and four fail,
with no skipped or aborted cases. All eighty cases in the related focused subset
pass within this complete run, including all sixteen new cases. App passes
4492/4493; visual 84, Agent 119, Browser 76, Infrastructure 199, Providers 516
and Tools 491 all pass. Windows passes 134/137. The four failures are:

- PtyTerminalResizeOwnershipTests: waiting for native resize times out.
- ShellProcessOwnershipTests: intermediate child cancellation binding raises
  TaskCanceledException.
- McpStdioManagerLifecycleTests: reload initially observes no expected tool.
- AuthenticatedProviderProxyRuntimeTests: Gemini/SOCKS5 rejected-credential case
  observes fewer than two proxy rejection-counter increments at its final
  assertion. This is not an observed successful authentication or direct fallback.

The three Windows failure paths pass a separate five-case isolated rerun; all
twelve rejected-credential protocol/proxy cases also pass an isolated rerun. No
implementation, timeout, security assertion, exclusion or allocation threshold
is changed for these unrelated failures. Isolated results are not added to the
6,115 complete-run total or used to reclassify it as all-green. The preceding
typography run remains historical at 6096/6099; its allocation case passes in
this new full run, but is not claimed fixed by the settings-entry change.

Final solution build has zero warnings/errors. Scoped whitespace formatting,
source size/type checks, source/assembly/native-hash verification and diff checks
pass. Screenshots/scripts are not tracked. Nothing is staged, committed, pushed,
released or installed; the full product goal remains active.
<!-- /source: SETTINGS_ENTRY_ACCEPTANCE.md -->


---

<a id="settings-navigation-interaction-acceptance"></a>

## SETTINGS_NAVIGATION_INTERACTION_ACCEPTANCE.md

<!-- source: SETTINGS_NAVIGATION_INTERACTION_ACCEPTANCE.md -->
### Settings navigation interaction acceptance

#### Scope and design checks

This slice follows [SETTINGS_SHELL_ALIGNMENT_PROGRESS.md](UI_WORKFLOWS.md#settings-shell-alignment-progress). It changes
product UI/behavior and obtains deterministic evidence; it is progress,
not a no-progress or wait turn. The full product objective remains active.

1. App's MainWindowViewModel owns top-level page navigation. Reuse the catalog's
   explicit ConversationRequested event instead of wrapping every task/history
   command or changing Domain/Application contracts.
2. Existing task commands retain lookup/locked-state validation. Search retains
   the existing matching rules and only exposes group visibility to the view.
3. No new tool, model call, approval policy, authorized root or persistence schema
   is introduced. Tests use independent storage and deterministic loopback
   providers; keyboard tests must prove settings does not submit the hidden draft.
4. Keep one type per C# file; split new interaction tests from existing shell
   geometry tests. MainWindowViewModel must stay below 500 physical lines.
5. Prove successful shortcut/menu/history navigation, rejected/null/foreign/locked
   targets, disposed subscription cleanup, background updates without page theft,
   real search bindings and unsaved editor/conversation state preservation.

#### Implemented behavior

- Ctrl+N, the File menu's new-task action, explicit task selection and history
  navigation now leave settings through the existing successful-navigation event.
  MainWindowViewModel removes its subscription on disposal. Locked, foreign and
  null targets retain their existing rejection behavior; rename/reload does not
  take the user out of an unsaved provider editor.
- Search hides headings whose groups contain no matching navigation item. A
  query does not select another content page or clear drafts. Clearing it restores
  seven navigation items and three groups; keyboard activation still uses the
  existing page-selection command.
- Ctrl+Enter from the settings search retains its existing isolated behavior:
  it does not send the hidden composer. No unnecessary global submission guard,
  persistence migration or tool-policy change is added.

#### Deterministic verification

The new nineteen-case baseline fails ten and passes nine: eight successful
navigation cases and two empty-group searches expose the missing behavior.
The finalized focused settings/navigation group passes 58/58 without skips.
An initial keyboard fixture did not wait for restored model selection; that
fixture is corrected before this baseline, not presented as a submission bug.

The first complete serial App run passes 4,212 of 4,218 cases and fails six
existing workspace-frame assertions. Their isolated recheck fails the same six:
they still require the outer shadow frame to clip itself, contrary to the
documented [WINDOW_FRAME_SHADOW_ACCEPTANCE.md](UI_WORKFLOWS.md#window-frame-shadow-acceptance) separation. The assertions now
require a non-clipping outer frame and a clipping inner AppBorder with equal
global bounds/radius. Product frame rendering is not changed or reverted.
All seventeen ConversationHeadingLayoutTests then pass. The corrected test file
remains 250 physical lines; MainWindowViewModel remains 466 and SettingsOverlay
128. Both new test files contain one type and remain below 250 lines.

The latest isolated App output is Debug/JIT
`.scratch/settings-interactions-frame-20261013/bin/MossAgent.App/debug_win-x64`,
SHA256 `1D35374501158D3B03DF81A9E47963BE4B0C29C14091F6AB746004A49BC8BFBC`.
Its solution build has zero warnings/errors; all 48 pixel cases and scoped
whitespace checks pass. Ten shell cases also export fresh compositor frames to
`%TEMP%\MossAgent-settings-interactions-rendered-20261013` using actual Headless
render-scaling notifications, not resized screenshots. External Python produces
an unscaled shell crop and four matching light-color samples against the retained
native Codex General reference. It explicitly rejects native/whole-window
equivalence: the Moss frame is Headless Appearance and the reference is General.
The Python helper and image artifacts remain outside the repository.

The corrected final serial App run passes all 4,218 cases. Together with the
final 48-case pixel run and the six other complete assemblies (Agent 119,
Tools 491, Platform.Windows 137, Browser 76, Infrastructure 199, Providers 516),
the final eight-assembly results pass 5,804/5,804 with zero failures/skips.
Seven architecture cases are included in Agent's 119, not counted twice.
The earlier six-failure App TRX and isolated red recheck remain retained.
App/pixel final replays use `settings-interactions-frame-20261013`; the other
six use `settings-interactions-20261013` against the same checkout, without
promoting either output directory to native production evidence. A final
solution build and scoped whitespace/diff verification also pass. The recorded
latest App assembly remains unchanged during the final App/pixel runs and is
not substituted for the default executable or launched as a native preview.

#### Native observation limit at the original checkpoint

The old preview's launch monitor is still live in session79013. A new passive
PrintWindow attempt again reports 1920x1020 native bounds but only 1536x816 content
and black padding, so the capture is rejected. This is not a settings comparison
or a native renderer root-cause conclusion. No duplicate preview is started and
no native input uses the unsafe prior window binding.

A fresh window-list/get-window/state check again supplies Codex pixels for the
MossAgent window object, so no input follows it. `before/interaction-gate.json`
records the rejected passive frame; no PNG is accepted. `codex-current.png/json`
is a valid 1920x1032, 96-DPI passive capture, but shows the workspace rather than
Appearance. It is not silently substituted for a settings reference.

The old preview still owns its independent data directory and old App SHA256
`6E10E54B78BE6C11D036ED5D5B8B39EE6FF920AD15FA6A062B05141F2D8C2CD3`.
Read-only comparison again verifies all nineteen table schemas/rows, SQLite
integrity, unchanged UI settings, three original artifact hashes/audits and the
four-request history. The legacy automation audit retains its expected false
fresh-cancellation flag because no cancellation is executed. Its bounded local
observer is no longer listening; neither its original PID nor port 51792 is
present. The still-open App is not reported as a successful shutdown or a new
scheduler replay. The user is asked to close only that old MossAgent preview
before a trustworthy new-version replay is attempted.

Normal-size/maximized light/dark native interactions, equivalent Codex Appearance
and PI-Desktop references, restore clipping and the full UI/state/DPI matrix
remain required. Deterministic interaction tests do not replace those gates.
The default executable, JIT/single-file packaging, JSON, AOT and execution policies
remain unchanged. No commit, push or release occurs.

[NATIVE_SETTINGS_INTERACTION_PROGRESS.md](UI_WORKFLOWS.md#native-settings-interaction-progress) supersedes the open-preview/binding
status above: ownership and actual foreground pixels are reestablished, the
old App closes normally, and the recorded candidate receives a fresh independent
native search/navigation/draft/theme/size replay without model requests. This
does not repair the older resize defect or supply an equivalent Appearance
reference. It also records a later shared-title correction found in native crops.
<!-- /source: SETTINGS_NAVIGATION_INTERACTION_ACCEPTANCE.md -->


---

<a id="settings-page-header-acceptance"></a>

## SETTINGS_PAGE_HEADER_ACCEPTANCE.md

<!-- source: SETTINGS_PAGE_HEADER_ACCEPTANCE.md -->
### Shared settings page headers

#### Design self-check

1. App owns the presentation. `AppSettingsPageHeader` composes existing wrappers
   and shared appearance variants without adding dependencies to other modules.
2. Pages pass their existing create, refresh and reconnect commands through the
   dedicated action slot. Provider status/error bindings retain their owner.
3. No new I/O, task capabilities, approvals, cancellation rules or data writes
   are introduced. Tests use independent synthetic repositories and tool managers.
4. Each new C# file declares one type. The component, styles and tests stay below
   the repository's file and method limits; no large class or partial split is used.
5. Production-page keyboard tests cover real commands. Component rendering,
   live resizing, status removal/replacement and negative appearance-policy tests
   protect the shared presentation and its business-code boundary.

#### Reproduced issue and correction

- At 486px page width, the old child-agent description needs 299px but receives
  215px; the custom-agent description needs 312px but receives 228px. Proxy
  settings also clips its 247px description into 244px. Six baseline cases fail
  across both themes; the other fourteen page cases already pass.
- Providers, proxies, MCP, custom agents and child agents now share one header.
  Below 480px actual header width, actions move below the title/description/status.
  Wider headers retain right-aligned actions. Text wraps rather than being clipped.
- Header spacing and action spacing live in shared styles. The three grid-owned
  headers retain their 24px list separation; scroll-stack pages retain their
  existing stack spacing rather than adding a second margin.
- Pixel review catches a candidate style-key mismatch: AppUserControl and
  AppContentControl retain native style keys, so the new variants use stable
  wrapper classes rather than derived-type selectors. Page tests explicitly
  check the separated margin. No base wrapper style keys are changed globally.
- Provider discovery failures still pass through the existing sanitizer into
  the shared error status. A production-view test checks visibility, readable
  wrapping, error color and absence of synthetic secret text in both themes.
- The old model-discovery markup guard checks the page-to-header status/error
  bindings and the component's actual status visibility/error classes. Its
  independent editor-error assertions remain intact.

#### Evidence boundaries

- `SettingsHeaderPageLayoutTests` uses production view models and keyboard input
  on all five pages in both themes at 486/1024px. It opens and closes real editor
  dialogs and awaits the real refresh/reconnect command; it does not call a paid
  provider or modify user data.
- `SettingsPageHeaderRenderingTests` covers 320/480/728px, 100/125/200% scaling,
  error status, cleared status/description/actions, repeated breakpoint crossings
  and action replacement. Empty-slot checks assert cleared content and a
  heading-only extent, not stale retained bounds on an empty presenter.
- Negative C# and AXAML cases reject business overrides of component content,
  padding and dimensions while allowing data and the dedicated action slot.
- Baseline reports, candidate failures and before/after PNGs stay outside the
  repository in `%TEMP%/MossAgent-settings-header-20261015`. They are deterministic
  offscreen renders, not desktop captures or same-state Codex parity evidence.
- Native screenshots remain user-postponed. This slice alone does not complete
  the full product objective or the complete responsive/theme/interaction matrix.

#### Complete validation checkpoint (2026-10-15)

- The first complete binary checkpoint passes 6,431/6,433 across eight assemblies.
  Its two failures are the old `ModelDiscoverySafetyTests` inline-status guard
  and `SharedControlStyleTests` inline-heading guard. Both now inspect the shared
  component without removing their error-visibility or typography assertions.
- The final complete App run finishes normally in 13m30s at 4,686/4,688. All 36
  new App cases pass. All six non-UI suites pass 1,581 cases and complete pixels
  pass 166, including all twelve new header-rendering cases. The eight complete
  suites therefore pass 6,433/6,435, with no skips/aborts. Focused runs are not
  counted again. This is not an all-green checkpoint.
- The untouched `SidebarFilterAllocationTests` recent/missing case reports
  3,288 bytes instead of zero. The untouched conversation-restart case reports
  an `IOException` at line 75 while deleting its owned temporary directory.
  The empty directory remains for inspection; a current locking owner is not
  identified. Cleanup failure does not prove or disprove all earlier restoration
  assertions, and neither underlying cause is claimed fixed.
- Both complete classes pass three independent no-build reruns (nine cases per
  run). These 27 results retain the strict assertions and do not replace the
  original complete-run failures. Neither production lookup/restoration behavior
  nor these two test files is changed by this UI slice.
- The final sequential solution build has zero warnings/errors. Scoped
  whitespace verification, tracked/untracked whitespace checks and eight tested
  assembly identity checks pass. An intermediate overlapping verification build
  emitted an MSB3026 copy-retry warning; it is not represented as a clean build.
  All test processes exit normally; no running user window or data is changed.
- Original complete reports are under `full/`; the final complete reports,
  build log and before/after assembly hashes are under `final/`. `isolated-1.trx`
  through `isolated-3.trx` retain the separate class reruns. `rendered-final.trx`
  and `after/` retain the final header render with fresh build dependencies.
- A separate static style-key inventory flags five other candidate selectors in
  `style-key-candidates.csv`. They require rendered verification before changes;
  this inventory is not a claim that those controls fail or have been fixed.
<!-- /source: SETTINGS_PAGE_HEADER_ACCEPTANCE.md -->


---

<a id="settings-preview-fidelity-acceptance"></a>

## SETTINGS_PREVIEW_FIDELITY_ACCEPTANCE.md

<!-- source: SETTINGS_PREVIEW_FIDELITY_ACCEPTANCE.md -->
### 设置页主题及差异预览对照验收

日期：2026-10-09。本记录覆盖设置页的两个共享预览组件，不代表整个界面已经
与 Codex 一比一一致。标题、导航和设置字段的前次验收见
[SETTINGS_SHARED_APPEARANCE_ACCEPTANCE.md](UI_WORKFLOWS.md#settings-shared-appearance-acceptance)。

#### 设计自检

- 行为归 App 的 `AppThemePreview`、`AppDiffPreview` 和共享
  `ThemePreviewVariants.axaml`；没有改变模块依赖方向。
- 主题选择仍使用原来的 `SelectThemeCommand` 和主题偏好存储；差异预览仍是
  只读合成内容，横向滚动使用既有 `AppScrollViewer`，不读取项目文件。
- 没有改变工具授权、审批、取消、超时、输出限制、调度、数据库或提供商行为。
  原生宿主、Python 截图/对照脚本和隔离数据全部留在仓库外。
- 三个 AXAML 文件为 64、127、255 行。三个相关 C# 测试文件分别为
  122、65、150 行；Roslyn 确认各一类型，最长方法或 lambda 为 44 行。
- 新增 16 个渲染用例覆盖明暗主题、三种选择状态、预览几何和配色、窄内容宽度、
  无纵向溢出，以及两个横向滚动偏移互不影响。只读预览没有新增编辑或执行入口。

#### 共享修正

- 保留每张主题预览的 248×174-DIP 设计尺寸和已有真实主题按钮。
  浅/深色预览的内部窗口改为 202 DIP 宽，列表行步距从 44 改为 35 DIP，
  最后一行不再只露出半条；标题、次级行和背景改用参考取样值。
- 系统预览不再是两列平铺列表。恢复分色窗口、居中两条标题，以及内嵌的
  浅/深色列表卡；移除内部多余的细边框。清掉只属于旧预览结构的未使用样式。
- 差异预览保持 125-DIP 高度，改用约 21.33-DIP 的行高。
  组件局部关闭布局取整，避免每行都向上取整为 22 DIP，三行色带因此保持 64 像素。
- 行号槽与代码区使用各自的浅色红/绿背景，变化行号和四-DIP 边标使用共享语义样式；
  新配色资源仅作用于外观预览，不改变项目代码检查器的配色。
- 两边是真实、相互独立的只读横向滚动视图，不画无法操作的假滚动条。
  窄窗下可查看被裁切的示例内容；主题、按钮尺寸、阴影和字段样式没有在业务页局部覆盖。

#### 原始截图证据

证据目录：`C:\Users\wzheng\AppData\Local\Temp\MossAgent-settings-preview-20261009`。
Codex 参考使用用户提供的 `codex-clipboard-0a8430e8-0091-4d07-a447-4acaa1ba177d.png`。
没有操作 Codex 界面或系统安全设置。

- 参考和本轮生产 App/MainWindow 均为原始 1919×1032 像素，浅色界面、系统主题选择。
  `baseline-System/appearance-before.png` 与 `final-System/system.png` 保留修改前后整窗。
- `compare_previews.py` 只做原图取样与裁片，不缩放截图。
  `preview-comparison-after.png` 的三行依次为 Codex、修改前、修改后；同名 JSON 为取样记录。
- 浅色预览背景参考/修改前/修改后为 `#F3F3F3` / `#F5F5F5` / `#F3F3F3`；
  深色预览背景为 `#5D5D5D` / `#626262` / `#5D5D5D`。
- 差异色带高度参考为 64 像素，修改前为 72，修改后为 64。
  指标来自原始 PNG，不把裁片对齐或几个数值一致当作整窗逐像素相同。
- Python 使用被动 `PrintWindow`，拒绝黑色、近均匀帧和尺寸改变的帧；不激活或移动 Codex。

#### 原生交互

- 普通权限、JIT 独立宿主运行生产启动/关闭路径，实测 render scale 为 1。
  数据仅在上述临时目录，没有添加提供商或定时任务，没有发送模型请求。
- 1919×1032 实际依次点击浅色、深色、系统主题，选择框及应用明暗状态正确改变；
  各次偏好文件分别保存 `Light`、`Dark`、`System`。对应 `light.png`、`dark.png` 及日志已保留。
- 980×640 实际切换深色；左右预览分别横向滚动至 60.5 DIP，再各自恢复 0。
  宿主观察日志和 `left-scrolled.png`、`right-scrolled-dark.png` 证明另一侧不跟随移动。
- 两个最终宿主及中间基线宿主正常退出 0，stderr 为空。没有关闭或修改用户的工作实例。

#### 验证与剩余差异

- 专项 52 项通过；完整解决方案构建及最终原生宿主构建均为 0 警告、0 错误。
- C# scoped whitespace 检查、XML 解析、Roslyn 类型/规模审计和仓库配置下的
  `git diff --check` 通过，未暂存、提交、推送或发布。
- 首次全量回归中，既有 SQLite 迁移测试的 category 11 出现一次
  `ObjectDisposedException: SQLitePCL.sqlite3`；其四项专项原样重跑通过。
  没有为了界面任务修改 SQLite 或迁移实现。
  随后完整解决方案按 `-m:1` 串行重跑，七份 TRX 合计 3,362 项通过，失败和未执行均为 0。
  初次失败日志仍保留；重跑通过不等于 SQLite 句柄问题的根因已经修复。
  后续复现跨库全局清理干扰、定向测试清理和默认并发全量 3,370 项通过的证据见
  [SQLITE_TEST_POOL_ISOLATION_ACCEPTANCE.md](ENGINEERING.md#sqlite-test-pool-isolation-acceptance)；不改变本次界面验收的历史结果。
- 内容列相对参考仍偏左 4 像素、预览顶部偏上 1 像素；代码首行高度、语法色、
  滚动条轮廓及部分缩略图圆角/亚像素细节仍有差异。窄窗预览保留原有缩放策略，
  本轮只验证可用，不声称窄窗或所有 DPI 已一比一。
- 没有新鲜的 Codex 深色设置参考；深色结果仅证明渲染和交互回归。
  设置字段卡、全窗口导航和其他页面还需后续对照，完整产品目标保持进行中。

#### 2026-10-09 内容锚点续修

本次只修正上文明确记录的内容列偏左 4 像素、预览偏上 1 像素。
共享 `SettingsLayoutVariants.axaml` 的内容列左边距从 20 改为 28 DIP；
在宽度被 768-DIP 上限约束时，实际居中内容向右移动 4 DIP。
主题小标题的下边距从 16 改为 17 DIP，只移动下面的预览，不改变已经对齐的标题文字。
没有在业务页补位移，也没有新增端口、数据库访问、迁移、工具执行或偏好结构。

设计与测试自检：

- App 共享样式拥有布局；所有既有内容列复用同一 variant，模块依赖方向不变。
  主题命令、预览滚动、授权、审批、取消、超时和输出限制均使用原路径。
- 新 `SettingsContentAlignmentTests` 每文件一类型；新/更新的三个 C# 文件经
  Roslyn 审计，最长方法或 lambda 68 行，最大文件为既有 `ShellGeometryTests` 的 492 行。
  没有新增生产 C#，共享 AXAML 未超过硬上限。
- 六个新渲染 cases 覆盖 1919、1280、980 宽度的明暗主题，断言内容列/标题的真实位置、
  768-DIP 上限、窄窗 642-DIP 宽度、三张卡垂直对齐、差异预览位置及无横向页面溢出。
- 原样式下六项均失败于真实 X 坐标。Headless 默认字体的文字测量比原生宿主少 2 DIP，
  因此文字后的纵向间隔按实际标题/说明 Bounds 加固定共享间距断言；没有为了测试
  修改生产字体或原生 Y 坐标。绝对像素位置另由下述同尺寸原生帧验证。

证据目录：`%TEMP%\MossAgent-settings-alignment-20261009`。
Python 被动 PrintWindow 截图拒绝黑色/均匀帧和尺寸变化，不操控 Codex。
`compare_alignment.py` 在原始 1919×1032 图片中检测边界/文字墨迹，不缩放或人为对齐裁片。
`settings-alignment-comparison.png` 的三行依次为用户 Codex 参考、续修前、续修后。

| 原始像素指标 | Codex 参考 | 续修前 | 续修后 |
| --- | --- | --- | --- |
| 三张预览左边缘 X | 710 / 970 / 1230 | 706 / 966 / 1226 | 710 / 970 / 1230 |
| 预览顶部 Y | 209 | 208 | 209 |
| 差异预览顶部 Y | 424 | 423 | 424 |
| “外观”墨迹 X / Y / 宽 / 高 | 710 / 106 / 47 / 23 | 706 / 106 / 47 / 23 | 710 / 106 / 47 / 23 |

普通权限 JIT 宿主运行生产启动/关闭路径，实测 render scale 为 1。
宽窗实际依次切换浅色、深色、系统主题；窄窗 980×640 实际切换深色并滚动到深色卡底部。
日志、选中轮廓、保存的 Theme 和被动 PNG 共同验证操作结果。
隔离数据目录不包含用户配置或任务，没有发送模型请求。截图/脚本/宿主源留在仓库外，
二进制仅在忽略的 validation 目录；两个宿主正常关闭，不关闭用户工作实例。

最终专项 115/115 通过；七份独立 TRX 默认并发全量 3,376/3,376 通过，
App 2,565、Infrastructure 144，其余五项目共 667，失败和未执行均为 0。
完整解决方案和原生宿主构建 0 警告、0 错误；scoped whitespace、XML、Roslyn 和
`git diff --check` 通过。没有暂存、提交、推送或发布，保持普通 JIT/单文件发布方案。

这次只证明列/预览锚点修正，不代表设置页逐像素一致。语法色、滚动条、主题说明字色、
缩略图亚像素及字段卡/全窗口导航仍有差异；没有同状态 Codex 窄窗或新深色设置参考。
定时任务列表/编辑弹框及右侧栏仍需各自同状态参考与后续验收，完整目标继续进行中。

后续对预览语法色、闲置箭头及真实横向溢出的专项修正，见
[PREVIEW_SCROLL_SYNTAX_ACCEPTANCE.md](UI_WORKFLOWS.md#preview-scroll-syntax-acceptance)。本记录保留各轮的原始指标和验证范围。
<!-- /source: SETTINGS_PREVIEW_FIDELITY_ACCEPTANCE.md -->


---

<a id="settings-shared-appearance-acceptance"></a>

## SETTINGS_SHARED_APPEARANCE_ACCEPTANCE.md

<!-- source: SETTINGS_SHARED_APPEARANCE_ACCEPTANCE.md -->
### 设置页共享外观对照验收

日期：2026-10-07。此记录证明一次共享外观修正及其回归范围，
不是整个产品或所有页面与 Codex 逐像素一致的完成声明。

#### 设计自检

- 视觉行为属于 App 的共享 Controls、Styles 和主题服务；默认配色值属于
  Application 的平台中立设置类型。模块依赖方向没有改变。
- 继续使用 `IThemeService` 和 `IAppearancePreferenceStore`，没有新建配置存储、
  运行时工具或服务定位器。
- 页面仍只绑定数据、命令和共享语义变体；没有改变工具授权、审批、超时、
  取消、输出限制或审计管线。截图、对照脚本和测试数据均留在仓库外。
- 搜索字段的样式移到 `SettingsLayoutVariants.axaml`，避免继续扩大 App.axaml。
  970 个 C#/AXAML 源文件的文件规模、单类型及方法规模审计没有违规。
- 新增明暗主题及选中/未选中状态的实际样式测试、标题外观测试和旧配色保留测试；
  原有组件门禁、导航命令、外观输入验证及持久化失败测试继续运行。

#### 共享修正

- 七个设置页面的 `settings-title` 统一为 24-DIP 常规字重，不再使用
  28-DIP 半粗标题。外观页第一节的间距由共享 `settings-theme-heading` 管理。
- 设置导航使用共同的 14-DIP 图标槽、1-DIP 描边和 10-DIP 图文间距，
  选中时不额外加粗文字，并移除导航行右侧多余的 10-DIP 留白。
- “外观”使用共享太阳图标，而不是复用窗口设置的齿轮图标。
- 设置选中行及搜索字段使用独立 `SettingsNavigationSelectionBrush`，浅色为
  `#E2E9ED`；工作区的 `SettingsSelectionBrush` 保持原值，避免改错其他表面。
- 浅色默认前景及默认主操作按钮改为参考配色 `#1A1C1F`。
  已保存的配色、强调色和字体不会被新默认值无声覆盖。
- `AppIconLabel` 拥有图标内边距，并由 `Spacing` 更新图标 Margin。
  本轮去掉了实际不会生效的外部 Margin 覆盖，测试检查真实的组件内部间距。

#### 原始像素证据

证据目录：`C:\Users\wzheng\AppData\Local\Temp\MossAgent-visual-qa-20261007`。
Python `PrintWindow` 被动截图不移动、激活或点击 Codex；失败的黑色或均匀帧不保存。
Codex 当前主窗口的可用参考帧为 `codex-current.png`，设置页采用用户提供的
`codex-clipboard-0a8430e8-0091-4d07-a447-4acaa1ba177d.png`。

`compare_settings.py` 在原图上取样及裁片，不缩放截图。
`settings-reference-accepted.png` 和同名 JSON 保存参考、修改前、修改后的对照。
深色文字墨迹采用灰度低于 160 的阈值，以下数值是 PNG 中的像素，不是假定的 DPI 元数据。

| 项目 | Codex 参考 | MossAgent 修改前 | MossAgent 修改后 |
| --- | --- | --- | --- |
| “外观”标题墨迹宽×高 | 47×23 | 54×26 | 47×23 |
| 标题墨迹顶部 Y | 106 | 108 | 106 |
| “主题”文字顶部 Y | 177 | 178 | 177 |
| 导航图标墨迹宽×高 | 14×14 | 14×16 | 14×14 |
| 导航文字起始 X | 40 | 39 | 40 |
| 设置选中行背景 | #E2E9ED | #ECEFF1 | #E2E9ED |
| 标题栏背景 | #EEF4F9 | #EEF4F9 | #EEF4F9 |

标题栏本次测得已经与参考颜色一致，因此没有再改标题栏颜色。

#### 原生交互范围

- 普通权限的独立宿主运行生产 App/MainWindow，数据只写入临时目录。
  1280×820 实测浅色设置页；实际点击深色主题，界面及 JSON 均切换为 Dark。
- 实际点击 AI 提供商导航、展开提供商，模型条目在页面中显示。
  `providers-dark-accepted.png` 和 `models-expanded-dark-accepted.png` 保存结果。
- 小窗口请求 960 DIP，生产窗口的最小宽度约束实际得到 980×820。
  按实际尺寸保存 `settings-narrow-light.png`、`settings-narrow-dark.png`；
  实际切换深色、点击返回应用后保存 `workspace-narrow-dark.png`。
- 工具偶尔返回空或滞后的 UIA 树。输入使用新绑定窗口的当前截图，
  每个动作后刷新；真实结果以被动截图及已保存的主题值确认，不以旧 UIA 树推断。
- 配置数据由宿主准备，不能据此声称已通过实际鼠标创建全部提供商和模型。
  本轮未发送模型请求，临时宿主正常关闭后均报告请求数 0。

#### 自动验证与限制

- 初版 8 项对照回归在修复前失败；最终相关专项 63 项通过。
  新增 7 项测试，覆盖六个主题/状态组合及保存过的旧前景配色保留。
- 最终完整解决方案 2,378 项通过，App 1,786 项；7 份独立 TRX 中失败和未执行均为 0。
  完整构建 0 警告、0 错误。测试结果位于证据目录的 `test-results-final`。
- 图标尺寸一致不代表轮廓及所有文字已像素相同。导航墨迹的起始 X 仍有 1 像素差异，
  “外观”导航文字参考宽 28 像素、当前宽 26 像素；系统字体、字距和其他图标仍需继续对照。
- 未取得新鲜的 Codex 深色设置参考帧；深色截图只能证明运行与布局回归，
  不能证明深色配色已经一比一匹配。
- 本轮没有穷尽所有页面、DPI、浮层、悬停、焦点、错误和运行状态。
  原始窗口宽度、内容和版本不同，不把裁片中若干指标相同当作整窗逐像素一致。
  没有提交或推送，完整产品目标继续保持进行中。

2026-10-09 后续主题及差异预览的同尺寸对照和真实滚动验收见
[SETTINGS_PREVIEW_FIDELITY_ACCEPTANCE.md](UI_WORKFLOWS.md#settings-preview-fidelity-acceptance)，不改变本记录的历史验证范围。
<!-- /source: SETTINGS_SHARED_APPEARANCE_ACCEPTANCE.md -->


---

<a id="settings-shell-alignment-progress"></a>

## SETTINGS_SHELL_ALIGNMENT_PROGRESS.md

<!-- source: SETTINGS_SHELL_ALIGNMENT_PROGRESS.md -->
### Settings shell alignment progress

#### Scope and design checks

This slice aligns the shared settings shell with a passive light Codex General
reference. General content is not an Appearance reference; no whole-page or
whole-product pixel-equivalence claim follows from these shell measurements.

1. App owns view composition and UI navigation. Domain, Application, execution
   adapters and their dependency directions remain unchanged.
2. Reuse AppNavigationRail, existing settings/inspector commands, the automation
   page command and shared window frame variants. No new IO port is needed.
3. Navigation performs no model request or tool execution and changes no approval
   policy, authorized root, timeout or output limit. Fixtures use independent
   temporary storage and deterministic loopback services.
4. Keep one type per handwritten C# file and remain below file/method limits.
   Add shell tests separately instead of expanding the nearly full geometry file.
5. Render both themes at narrow/normal/wide widths. Prove return/automation/audit
   navigation preserves task, project, model and unsent draft; reject invalid tabs
   without state changes and close flyouts when their page becomes hidden.

#### Reference and capture limits

Evidence remains outside Git in
`%TEMP%\MossAgent-settings-shell-20261013-134013`.
The Codex General frame is light, maximized, 1920x1032 and 96 DPI. Its shared shell
has a 52px rail, 264px sidebar, search bounds (60,96,247,36), and the first
navigation row at (60,178) with height 30. Sidebar/selected-row samples are
`#F9FBFD`/`#ECEFF1`; the rail sample remains `#EEF4F9`.

The earlier `before/light-settings.png` actually shows the workspace at 120 DPI,
not settings. The fresh passive PrintWindow attempt reports 1920x1020 native
bounds but only 1536x816 content with black padding and is rejected. Neither is
a settings baseline. Sky also returned Codex pixels for MossAgent metadata;
inputs remain stopped until identity can be established safely. These capture
failures do not establish a renderer root cause or repair the native restore bug.

Appearance content sizing, provider/model editor ownership and full-page dialogs
remain separate from the shell. The native theme/size/state matrix, equivalent
Appearance/PI-Desktop references and full-product acceptance are still open.

#### Implemented and verified

- Settings now uses the shared 52px rail, 264px sidebar and outer frame/inner
  rounded clip. Shared title/search/heading variants reproduce the measured
  shell anchors. Only the settings selection token changes; rail/card palettes
  and existing shared card shadows stay unchanged.
- The settings home action returns without creating a task or sending a draft.
  Automations and valid inspector selections leave settings before navigating;
  invalid tabs leave state untouched. The rail suppresses hidden-page selection
  in settings and closes flyouts when its owning page disappears.
- Appearance's shared 768px maximum stays unchanged. Headless column anchors
  move to x728 at width1919 and x409 at width1280. At width980 they are x344 and
  width598, without horizontal scrolling. These are current shell-layout
  regressions, not measurements from an equivalent Codex Appearance page.
- The final isolated Debug App assembly SHA256 is
  `52DE3D413749348C8752265AF2C62BCEFA0F5C81978849CA4D86D636E271E019`.
  It is built, not natively launched or substituted for the default executable.

Validation sequence and limits:

1. Sixteen new focused cases initially produce 13 failures/3 passes, exposing
   missing shell/navigation behavior while invalid-tab guards already pass.
   Initial test-code import/analyzer errors are repaired before this baseline.
2. Final shell/navigation/alignment/rail/geometry App group passes 71/71.
   The first broader run passes 1463/1466: two assertions still expect the old
   settings color, and the unchanged warm-allocation test reports 5256 bytes.
   The color expectations are updated from the reference; the allocation test
   is not changed and passes an isolated rerun. The final serial wider run
   passes 1466/1466 with no failures or skips.
3. All 48 pixel cases pass. Ten new settings cases cover both themes, 980x640,
   1280x820 and 1920x1032, with 1x/1.25x/2x Headless scaling cases. They verify
   actual render scaling, physical frame size, search/selection/sidebar colors,
   page color, upper/left shadow and rounded corner clipping. The first fixture
   omitted inactive-page flags and its offscreen scaled bitmap distorted cached
   child layers. The corrected fixture uses explicit flags, SetRenderScaling
   notifications and CaptureRenderedFrame. This is test-fixture repair, not a
   native renderer or restore-defect repair.
4. Solution build passes with zero warnings/errors. Scoped dotnet whitespace
   verification and Git diff whitespace checks pass. App.axaml remains 498
   physical lines, MainWindowViewModel 453 and ShellGeometryTests 493.
   Seven architecture constraints pass separately in the Agent assembly.
   Complete runs of the other six test assemblies and the full App assembly are
   not performed here.
5. External Python creates `shell-comparison.png/json`. Codex is a passive native
   General frame; Moss is a Headless Appearance frame. Four flat-color samples
   agree exactly, but labels/content differ and native/whole-window equivalence
   is explicitly false. The screenshot/comparison scripts and PNGs stay outside
   the repository.

The independent old-binary preview's 19 SQLite tables retain their schemas/rows,
integrity passes, Light remains selected, three original artifact hashes/audits
are unchanged and the request history stays at four. The legacy scheduler audit
returns one expected false flag for fresh cancellation disconnect: no cancellation
is executed in this read-only preview, so that audit is not a new scheduler gate.
The preview has not been confirmed closed; its launch monitor remains pending.
No new after-preview is launched while the target binding is unsafe. Native
settings, search/editor/return interactions, theme/size/DPI acceptance and normal
preview shutdown still require a trustworthy target or manual user handoff.
<!-- /source: SETTINGS_SHELL_ALIGNMENT_PROGRESS.md -->


---

<a id="settings-status-overflow-acceptance"></a>

## SETTINGS_STATUS_OVERFLOW_ACCEPTANCE.md

<!-- source: SETTINGS_STATUS_OVERFLOW_ACCEPTANCE.md -->
### Settings status overflow acceptance

#### Scope and reproduced failure

The shared settings-page header previously measured status text at unrestricted
height. A saved provider name repeated in discovery feedback could consume the
entire page, hiding the provider list and its actions. Provider names are only
required to be nonempty; this is valid input, not a test-only invalid setting.

Four production-pane Headless baselines reproduce the problem in Light and Dark
at 486x601 and 1024x601 DIP. The list viewport shrinks from 366 to zero at the
compact width and from 408 to zero at the wider width. All four assertions fail;
their original TRX, logs and frames remain in the external evidence directory.

#### Shared correction and design self-check

- Ownership stays in App's shared `AppSettingsPageHeader` and
  `SettingsPageHeaderVariants.axaml`; no view-local appearance is added.
- The existing `AppScrollViewer` wrapper bounds status feedback to 96 DIP,
  disables horizontal scrolling and retains automatic vertical scrolling.
- Text, wrapping, error styling and complete accessible names remain intact.
  Short messages retain natural height; clearing status restores the original
  header/list geometry. Other page scroll viewers are unaffected.
- The status viewer is keyboard-focusable. Native Shift+Tab traversal, PageDown
  and PageUp expose the complete message without scrolling the provider list.
  Existing focus feedback is retained; no custom key handler is introduced.
- This reuses the production ViewModels, commands and existing discovery port.
  There is no new production I/O, persistence, subscription, dependency, tool or
  approval boundary. Tests use synthetic configuration and deferred discovery;
  they do not contact a paid provider or use personal configuration.
- The two AXAML files are 22 and 35 physical lines. The new fixture and test
  files are 112 and 140 physical lines, each with exactly one type and methods
  below the 80-line hard limit. No production C# is added or changed in this fix.

The ui-ux-pro-max guidance is applied as desktop keyboard operability and
unobscured actions, not as a claim of web WCAG conformance or a visual redesign.
The existing Codex-oriented palette, typography and control system are retained.

#### Focused checks

`SettingsStatusOverflowTests` adds 16 cases: four groups across both widths and
themes. They cover long discovery failures with sanitized errors, pending
discovery followed by normal completion, complete keyboard-readable messages,
and short/cleared feedback with unrelated scroll behavior unchanged. Actual
hit testing and Space/Escape verify that the create action remains usable.

Final validation against the corrected production binary:

| Check | Result |
| --- | --- |
| Related App tests (`related-final.trx`) | 232/232, including all 16 new cases |
| Complete pixel suite (`pixels.trx`) | 188/188 |
| Architecture subset (`architecture-final.trx`) | 7/7 |
| Final solution build | Zero warnings and errors |
| Scoped whitespace format verification | Passed for both new C# files |
| Tested/runtime assembly identity | All six final hashes unchanged after build |

The three test assemblies and three App copies are recorded in
`hashes-before-final-related.csv` and `hashes-after-final.csv`. The earlier pixel
run's visual/architecture assemblies and all App copies also match
`hashes-before-final.csv`; only the App test assembly changed when loading-state
cases were added. Every reported final TRX row passed, with no skips or aborts.

This is 427 checks across those suites/subsets, not a new full-product total.
The historical eight-suite 6,495 checkpoint predates these changes and remains
historical. This slice does not rerun the full App suite or all non-UI suites.

#### Evidence and remaining limits

Evidence root:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-settings-status-20261016`

- `baseline/`: original four failed rendered states.
- `final-related-frames/`: final error, loading and keyboard-reading frames.
- `status-before-after-light.png` and `status-before-after-dark.png`: compact
  before/after comparisons built from baseline and corrected rendered frames.
- TRX/logs, final build/format logs and assembly hash CSVs remain external.

These are independent off-screen component/page renders using production
markup, not current native MainWindow or Codex screenshots. They do not prove
whole-window parity, native DPI behavior or repair the separate native resize
defect. The user's postponed screenshot boundary is respected: no existing
Codex/MossAgent window is operated, captured or restarted for this slice.
No files are staged, committed, pushed or released; the product goal remains open.
<!-- /source: SETTINGS_STATUS_OVERFLOW_ACCEPTANCE.md -->


---

<a id="settings-titlebar-navigation-acceptance"></a>

## SETTINGS_TITLEBAR_NAVIGATION_ACCEPTANCE.md

<!-- source: SETTINGS_TITLEBAR_NAVIGATION_ACCEPTANCE.md -->
### Settings titlebar navigation acceptance

#### Scope and design checks

The wide Appearance reference has back/forward actions followed directly by
File/Edit/View/Help. MossAgent previously retained a third, redundant navigation
toggle, shifting the first menu's ink from reference x=83 to x=122 at 96 DPI.

1. App owns this presentation. MainWindow bindings and shared layout variants
   consume the existing navigation state; no module dependencies change.
2. Existing AppNavigationPanes state and ToggleNavigationCommand remain the
   behavior contract. No new view model, service, cache or state flag is added.
3. No filesystem, network, tool, approval, timeout or output policy changes.
   Native replay uses only an independent synthetic-data instance.
4. Changes remain within the source/type/method limits. All visual elements
   remain shared wrappers; per-view colors or icon dimensions are not added.
5. Eight new light/dark real-window headless cases cover visible/hidden input,
   menu access, pointer/keyboard drawer operation, resize and state isolation.

#### Behavior

Hide the titlebar toggle only while settings are open, navigation is non-compact
and the settings navigation pane is already visible. Preserve it in the workspace,
in compact settings regardless of drawer visibility, and when a wide navigation
pane is closed. The existing handler still targets the active page's navigation.

At this checkpoint the permanent-settings menu variant uses a shared 4-DIP inset
rather than 9. [SETTINGS_TYPOGRAPHY_ACCEPTANCE.md](UI_WORKFLOWS.md#settings-typography-acceptance) records the later 5/10-DIP insets
that compensate for updated menu padding while retaining the first ink position.
Workspace and compact states retain their existing inset, history actions,
caption buttons and independent navigation state. File-menu popup access and
Escape dismissal remain real behavior, not replacement text.

The initial source guard is updated to enforce the new bindings without removing
its geometry, command or menu-order checks. An initial test incorrectly expected
Escape to focus the titlebar button; the existing contract restores the remembered
primary editor instead. That assertion is corrected before the accepted red
baseline: all eight cases then fail at the missing new visibility behavior.
An unsupported AXAML class property-element binding initially fails compilation;
ordinary class bindings and a shared compound selector correct it.

#### Native and pixel evidence

External artifacts: `%TEMP%/MossAgent-settings-titlebar-20261015`.
Independent Debug/JIT build: `.scratch/settings-titlebar-final-20261015`.
App DLL SHA256:
`75B12AD68C895089BFF41F2E2CB7BE15BBB4BAB87520906D1CE42ED99332E8D8`.

The accepted ordinary-permission replay (PID 3884) produces seventeen PrintWindow
captures at 96 DPI: 1920x1032, 1280x820 and 526x601, both themes, real menu popup,
drawer open/Escape, maximize from an open drawer, workspace collapse/reopen and
settings/workspace state independence. It restores the exact original Light/font
preference bytes, preserves nineteen database table hashes and creates no artifacts.
The App exits normally with code zero, disappears, and leaves empty stderr.
`audit.py` verifies these facts independently.

An earlier instance disappears during a runtime/session update before any accepted
capture; its receipt is retained, not described as a normal exit. A coordinate
mistake later minimizes the replay instance. The resulting 160x28 frame numbered
10 is explicitly excluded. Rebinding/activation recovers the same process, then
the observed maximize action succeeds. The external capture helper now rejects
minimized and undersized frames before saving them; no product change is needed.

`titlebar-comparison.png` uses unscaled reference/before/after crops. File ink now
starts at x=83 and Edit at x=136, matching the saved reference starts. View remains
one pixel and Help three pixels farther right; glyph widths/rasterization still
differ. This is a removed extra-action/inset correction, not exact menu-font proof.
Title ink is deliberately unchanged rather than moved by an unverified transform.

The saved full-size Codex reference remains the Appearance frame under
`MossAgent-child-settings-runtime-20261014`. A later passive capture observes a
newer Codex version in a 481x601 conversation window, not equivalent Appearance.
It is not substituted into the comparison or claimed as current full-page proof.

#### Validation and remaining gates

The final focused run passes 60 cases, including the eight new cases. The complete
solution run executes 6,091 cases: 6,090 pass and one fails, with no skipped or
aborted cases. App passes 4,468/4,469; visual 84, Agent 119, Browser 76,
Infrastructure 199, Windows 137, Providers 516 and Tools 491 all pass.

The sole failure is the unmodified `WorkspaceCatalogLookupTests` missing-identity
allocation assertion: 2,552 bytes instead of zero. Three independent reruns of
its complete five-case class all pass on the same assembly. These fifteen rerun
cases and the focused sixty are not added to the complete-run total. The complete
run remains explicitly not all-green; intermittent allocation behavior is not
explained or fixed by this UI change, and no assertion is weakened.

The solution build passes with zero warnings/errors; scoped whitespace formatting,
source size/type checks and workspace diff checks pass. External verification
records report/assembly/source/native identities, the non-green complete-run
result and successful isolated reruns. No JSON, AOT or packaging behavior changes.

Current-version same-state references, residual title/menu typography, other
surfaces/states, real DPI transitions and whole-product acceptance remain open.
Nothing is staged, committed, pushed, released or installed. The full goal remains
active; this bounded UI acceptance does not redefine its scope.
<!-- /source: SETTINGS_TITLEBAR_NAVIGATION_ACCEPTANCE.md -->


---

<a id="settings-typography-acceptance"></a>

## SETTINGS_TYPOGRAPHY_ACCEPTANCE.md

<!-- source: SETTINGS_TYPOGRAPHY_ACCEPTANCE.md -->
### Settings typography acceptance

#### Scope and design checks

The saved 96-DPI Codex Appearance frame has 14-DIP titlebar menus and a
14-DIP visual-style heading. MossAgent used 13 for both, while its ordinary
palette labels correctly used 13. This change is limited to shared presentation.

1. App owns the behavior; no dependency direction or composition changes.
2. Existing menu controls and settings variants remain the contracts. No new
   view model, cache, service, or per-view appearance override is introduced.
3. No tool, authorization, approval, network, timeout, persistence or output
   policy changes. Native replay uses a separate synthetic-data instance.
4. All edited source files stay within the hard size limits. Every edited
   handwritten C# file contains one type, and visual controls remain wrappers.
5. Eight new real-window headless cases cover both themes, Segoe UI and Inter,
   two content widths, inherited fonts, shared typography and unchanged anchors.
   Existing titlebar tests retain pointer, keyboard, Escape, resize, hidden-input
   and workspace/settings navigation-isolation coverage.

#### Shared styles

Menu triggers use font size 14 and horizontal padding 6 instead of 7. They retain
their 40x30 Chinese-label hit areas. The common titlebar-menu inset changes from
9 to 10, and the permanent-wide-settings inset from 4 to 5, compensating for the
padding change without shifting the first menu's ink. The existing 13-DIP spacing,
caption buttons, history actions, flyouts and navigation rules are unchanged.

Only the visual-style heading uses font size 14, line height 20 and margin
0,41,0,16. Its typography override follows the generic settings-section-title
variant, which remains 13 for other sections. Putting this override in the earlier
layout include initially allowed the generic style to overwrite it; rendered
tests caught that cascade defect. The layout variant owns only the margin.

The title remains 28 with its existing font metrics. For Segoe UI the mode picker
still starts at pane y=115, stays 78 high, and precedes the palette card by 16.
Inter keeps its own natural title metrics rather than acquiring a forced Segoe
height. The default font service, Chinese fallback and global antialiasing mode
are not changed. An external probe disproved the idea that setting Avalonia title
LineHeight=34 alone would move its glyphs upward; no title transform is added.

Test drafts contained incorrect assumptions about FontFamily.Name and the full
palette wrapper's status area. Final cases compare FontFamily values and measure
the actual 226-high AppCard. A temporary production-fixture version also allowed
asynchronous default-font application to replace the test's Inter resource; the
final isolated presentation test needs no provider fixture or command execution.
These failed drafts and the cascade failure are retained as development evidence,
not counted as successful acceptance runs or repaired by weakening product code.

#### Native and pixel evidence

External scripts and artifacts: `%TEMP%/MossAgent-settings-typography-20261015`.
Final native receipts and captures are in its `final-native` subdirectory.
Independent Debug/JIT build: `.scratch/settings-typography-final-20261015`.
Final App DLL SHA256:
`3AF85B79BD10B41F3BF13926A96AEA18D2DE6A381A15FEC486AFFD870225FD07`.

Ordinary-permission PID 18296 produces ten accepted settings captures at 96 DPI:
1920x1032, 1280x820 and 526x601, light/dark themes, real File popup, compact drawer,
Escape dismissal and return to maximized Light. An additional workspace frame
records the initial input diagnostic and is not settings-page evidence. The
earlier intermediate process exits zero before the final style build and has no
accepted final captures. The final process also exits zero with empty stderr.

The passive Python helper checks executable/PID/hash, captures only the owned
window and popups, and rejects black, minimized or undersized frames. Its audit
confirms nineteen unchanged database table hashes, zero artifacts, byte-identical
restored Light/default-font preferences and process/window disappearance.

The unscaled comparison uses the saved full-size Codex reference from
`MossAgent-child-settings-runtime-20261014`, the preceding accepted MossAgent frame,
and final `03-light-system-reference.png`. Threshold-192 ink measurements show:

- Visual-style heading: 52x12 at (747,201) becomes 56x12 at (747,202), matching
  reference size and position.
- File menu: 26x12 becomes 28x12 at (83,12), matching reference size and position.
- Mode-label, palette and title crops have zero changed pixels against the
  preceding accepted MossAgent frame; this does not mean they match Codex.

Residual differences remain explicit: Edit starts one pixel later than reference,
View two and Help two; Help ink width differs by one. Title ink is still two
pixels lower and one pixel shorter, and several palette labels remain one pixel
higher. Theme previews, actions, preset choices and other surfaces are not made
equivalent by this typography change.

Fresh passive Codex is version 26.1007.9446 in a 481x601 conversation window,
not an equivalent Appearance state. It is not substituted into the full-size
comparison. Initial native rail-settings clicks and a keyboard shortcut did not
prove a page transition; actual settings access is verified via File > Settings.
This slice does not certify that separate rail-input path.

#### Validation and remaining gates

The earlier broad focused run passes 114 cases. The complete solution then runs
6,099 cases: 6,096 pass and three fail, with no skipped or aborted cases. App passes
4,474/4,477; visual 84, Agent 119, Browser 76, Infrastructure 199, Windows 137,
Providers 516 and Tools 491 all pass.

Two failures are old source guards for menu padding and settings margins. Their
expected values are updated without removing the shared-style, geometry, color
or wrapper-boundary checks. A final 30-case focused run passes on the updated test
assembly, covering both corrected guards, all eight new typography cases and
existing titlebar, appearance and content-alignment cases. The complete App suite
is not rerun after these test-only guard updates; its original result is retained
and is explicitly not all-green.

The third failure is the existing SidebarFilterAllocationTests project/matching
case: 3,112 allocated bytes instead of zero. Three isolated reruns of its entire
six-case class all pass on the final test assembly. No allocation implementation,
assertion, threshold or test exclusion is changed. Those eighteen cases and both
focused subsets are not added to the 6,099-case complete-run total. The allocation
failure remains unexplained, not claimed fixed by a typography change.

The final solution build has zero warnings/errors. Scoped whitespace formatting,
source size/type checks and workspace diff whitespace checks pass. External
verification records the distinct pre/post-guard test-assembly identities,
unchanged seven other suite assemblies, identical final native App hash, capture
metadata, source hashes, normal cleanup and byte-restored preferences.

Current-version same-state references, title/rasterization differences, remaining
menus, other surfaces/states, real DPI transitions and full-product acceptance
remain open. No AOT, JSON or packaging changes are made. Nothing is staged,
committed, pushed, released or installed; the overall goal remains active.
<!-- /source: SETTINGS_TYPOGRAPHY_ACCEPTANCE.md -->


---

<a id="settings-viewport-focus-acceptance"></a>

## SETTINGS_VIEWPORT_FOCUS_ACCEPTANCE.md

<!-- source: SETTINGS_VIEWPORT_FOCUS_ACCEPTANCE.md -->
### Settings viewport focus acceptance

#### Ownership and safety

- App owns page/drawer keyboard focus through AppPageSwitcher and
  AppNavigationPanes. No application port or dependency direction changes.
- Automatic focus excludes controls outside, or partially clipped by, any
  ancestor ScrollContentPresenter. The existing lazy visual-order traversal,
  visible-field preference and eligible remembered target are retained.
- Drawer dismissal falls back to a visible primary control if its remembered
  field is now offscreen. No control/editor instance is replaced and no scroll
  offset is forcibly reset. Explicit user focus and scrolling remain supported.
- No tool, approval, timeout, output, provider, SQL, JSON/AOT or release policy
  changes. No global service or native resize workaround is introduced.
- The changed controls and new tests each contain one type and remain within
  the file/method limits. Negative tests cover clipped and nested viewports;
  positive tests cover retained visible editor focus, scroll and draft.

#### Reproduction

Evidence is outside the repository:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-settings-focus-20261014`.

The initial ordinary-permission Debug/JIT App was verified against SHA-256
`3F90E07CCADEC5D85893D5AEFB22CB5BFB07EB75860724C4BFCF211CE68C1773`.
Opening Appearance at 1280x820 focused the first palette combo. After shrinking
to 526x601, opening the settings drawer and selecting Appearance restored that
offscreen field. The native heading disappeared; a wheel scroll back to the top
restored it without resizing the window. Python passive captures preserve both
states under `native/before-drawer-selection.png` and
`native/before-scroll-restored.png`.

Eight new production-MainWindow regression cases fail before the change with
an actual vertical scroll offset of 96 rather than zero: initially compact
entry plus selection, outside click and Escape dismissal, in both themes.
This establishes a focus/BringIntoView problem for this reproduction, not the
older managed-client/native-client size mismatch.

#### Automated validation

- All sixteen new cases pass in `tests/focus-green.trx`: ten real MainWindow
  cases and six viewport-selection cases, including partial horizontal/vertical
  clipping, nested clipping and explicit user editing.
- The initial broader candidate records 140 passes and one new numeric-test
  failure. That test initially tried to focus the NumericUpDown wrapper instead
  of its real TextBox, then expected the editor as the first automatic target
  although the wrapper retains field precedence. The fixture now uses the actual
  editor for explicit focus and preserves the existing wrapper-precedence check.
  The failing logs/TRX remain; they are not reported as green runs.
- Complete visual suite: 72 passes. Architecture constraints: seven passes.
- Scoped formatting and the complete solution build pass with zero warnings
  and errors. Complete App regression: 4,335 passes with zero failures, skips or
  aborts. The external verifier checks driver termination, individual TRX
  outcomes/counters, assembly hashes and native fixture identity independently.
  The sixteen new cases are already included in the App total, not added again.
  Six non-UI full suites were not rerun for this App-only slice.

#### Native validation

The fresh independent `native-after` fixture uses App SHA-256
`5D7A541B1F7B35B53E13F32B595BF15DF317BA6476218D55CE3DE5059E762BEB`.
Python frames verify compact Appearance retains its heading after selecting
the drawer destination. Dark-theme Escape dismissal likewise retains the top
of the page. Explicit wheel scrolling and focusing the background-color field
remain possible; closing the drawer restores that same field and its scroll
position without changing its value.

Accepted passive frames include 96-DPI 526x602 and 120-DPI 658x753 windows.
They are not interchangeable pixel references. `focus-before-after.png` places
the 96-DPI 526x601 before frame and 526x602 after frame side by side without
resizing, disclosing the one-pixel height and scroll/hover-state differences.
This is bug-reproduction evidence, not Codex pixel equality or a measured
Codex compact breakpoint.

A maximize input produced a wide Sky frame, but the subsequent Python capture
reported a normal, non-maximized 658x753 window. Its evidence is named
`light-after-maximize-request`; this attempt is not credited as native maximize
acceptance. It does not invalidate the independently captured compact title and
editor-restoration checks.

Before and after preview Apps/observers exit zero; port 51792 is released.
All nineteen table fingerprints, three original artifacts, four-request
history and restored-Light UI bytes remain unchanged after process exit.
No provider/model request, automation execution or approval-setting change
was made. The fixture directories are closed and must not be reused.

#### Remaining scope

The last passive Codex reference is maximized at 1920x1020 and 120 DPI, but still
a conversation, not same-state Appearance. Earlier 600x750 conversation frames
are retained rather than relabeled as Appearance.
Full settings/Codex fidelity remains unaccepted. The older native managed/native
geometry mismatch, WM_DPICHANGED event-chain diagnosis and complete product
goal remain open. Healthy frames or a restart are not evidence of their repair.
<!-- /source: SETTINGS_VIEWPORT_FOCUS_ACCEPTANCE.md -->


---

<a id="sidebar-brand-fidelity-acceptance"></a>

## SIDEBAR_BRAND_FIDELITY_ACCEPTANCE.md

<!-- source: SIDEBAR_BRAND_FIDELITY_ACCEPTANCE.md -->
### 侧栏品牌标题与箭头核验

#### 设计与范围

- App 的共享 `ActionButtonVariants.axaml` 拥有此次呈现；继续复用
  `AppActionButton`、`AppIconLabel` 和 `AppIcon`，不增加业务控件或新服务。
- 品牌标题由 16/SemiBold 调整为 18/Bold；专属下拉箭头使用 6-DIP 几何、
  1-DIP 描边和 2.5-DIP 光学下移。没有全局缩小其他按钮图标。
- 实际侧栏标题入口保持 32-DIP 高；新对话文字、30-DIP 行高、搜索、项目列表、
  菜单绑定和动态标签均保留。产品名称仍为 MossAgent，不改为 Codex。
- 不引入端口、持久化迁移、工具调用或授权变化。窗口尺寸、DPI、渲染、依赖、
  审批政策/决策、JIT JSON 和 Windows x64 单文件发布均未修改。
- 两个新手写测试文件分别只含一个类型，文件/方法均未超过硬上限。测试同时覆盖
  正常呈现、两种主题、悬停、布局保留、非品牌图标不变及标签后续更新。

#### 原始像素比较

证据和全部 Python/启动/分析脚本均留在仓库外：
`%TEMP%\MossAgent-dpi-transition-20261014`。

`brand-reference-comparison.png` 仅拼接同为 1920×1032、96-DPI 的原尺寸组件
裁剪，不缩放图片。当前 Codex 被动参考仍为对话页；助手未操作 Codex。

| 样本 | 箭头墨迹宽×高 | 墨迹中心 Y |
| --- | --- | --- |
| Codex 参考 | 6×3 | 70.5 |
| MossAgent 修正前 | 12×6 | 68.0 |
| MossAgent 最终版 | 6×3 | 70.5 |

测量使用固定组件区域和相同的 RGB 阈值 205。第一版的箭头测得 5×3，证据
保存在 `brand-after-attempt1.png/json`；随后只对品牌箭头增大 1 DIP，取得最终
6×3 帧。标题字符串、宽度和有无字母下伸部不同，不能把标题墨迹矩形直接当成
等文案字体证明。这是标题层级和箭头的局部修正，不是整页像素等价验收。

最终生产 Debug/JIT App 程序集 SHA-256：
`342C865E5AB489E6455C5AEC1A3EE2900CBF14365B11A29AC849DA4012F15077`。
独立普通权限实例 PID21988/HWND4458700 与其 exe 路径、截图元数据及白名单
CLR 几何快照一致。最大化帧的 native/ClientSize/Bounds/arrange 均为 1920×1032，
布局有效、scale=1。这个健康状态不修复历史原生尺寸错配。

实机检查覆盖浅色最大化、1280×820 浅色普通窗口、深色外观/对话页和浅/深色
项目菜单打开及 Escape 关闭。菜单仅打开和取消，不选择项目、不执行模型/工具。
PrintWindow 静态帧有效；Sky/screen 原始帧含外部系统通知，不用于全窗干净像素
比较。`final/dark-menu-component.png` 是未覆盖的左上组件原尺寸裁剪。

#### 验证状态

- 旧样式下六个新增呈现用例均失败；最终相关组 327 项通过，包含这六项和先前
  27 项定向组，不重复计数。
- 最终完整像素程序集 78 项通过，含六个新增品牌箭头案例；覆盖浅/深色及
  1、1.25、2 渲染倍率。这些是离屏像素检查，不是原生高 DPI 切换证明。
- 最终架构子集七项、完整 solution build 和限定文件格式检查通过；构建零警告
  零错误。六个完整非 UI 程序集未重跑。
- 第一版另有完整 App 4,357 项和像素 78 项通过，属于箭头 5-DIP 独立程序集
  `D8C64AE2AAA40898CD6815F4EED40AFADCE2AA12C1C8E093AE9F0B7277EF1861`，
  不冒充最终 6-DIP 程序集的完整回归。第一版 App/observer 正常零退出且审计不变。

最终实例已恢复浅色，原生标题栏正常关闭；启动会话取得 App/observer 两个零退出，
两个 PID 和 51792 监听均已消失。observer 在关闭前已结束，没有重启或复用 fixture。
`final/final-closed.audit.json` 确认十九个表、三个原 artifact、四次历史请求和 UI
配置字节均未变化。外部 `brand-final-checkpoint.json` 绑定最终程序集、三份 TRX
哈希、原生几何及退出审计，不把中间版完整回归计入最终版。

完整 Codex 外观/深色与 PI-Desktop 同状态参考、真实 DPI 故障历史、完整表面/
交互矩阵和整个产品验收仍未完成；完整 goal 保持 active。此次独立构建没有替换
默认启动输出，没有暂存、提交、推送或发布。
<!-- /source: SIDEBAR_BRAND_FIDELITY_ACCEPTANCE.md -->


---

<a id="sidebar-compose-acceptance"></a>

## SIDEBAR_COMPOSE_ACCEPTANCE.md

<!-- source: SIDEBAR_COMPOSE_ACCEPTANCE.md -->
### Sidebar compose entry acceptance - 2026-10-10

#### Ownership and self-check

- App owns the existing `UiIconCompose` vector and shared sidebar action styles.
  The business view still uses `AppActionButton` with `Catalog.NewTaskCommand`;
  no native visual control, new runtime port or dependency is introduced.
- The icon's open square, pen contour and stroke alignment are corrected together.
  Its shared viewport is 11 DIP with a one-DIP stroke and optical translation
  `(0.5,-1)`. The global `AppIcon` normalization/rendering code is unchanged.
- Icon-label spacing increases from 10 to 11 DIP, retaining the label's x=92
  client position as the icon becomes smaller. Moving one DIP from the action's
  top margin to its bottom margin raises the label without changing the pinned,
  project or recent section positions. The 30-DIP hit height remains unchanged.
- Changes do not access files, providers, SQL or OS APIs from production UI;
  authorization, approval, timeout, cancellation and output limits are unchanged.
  No security/privacy setting, real credential, project data or provider request
  was changed during native QA. Debug remains JIT and release remains Windows x64
  single-file publishing, without AOT changes.
- Changed handwritten C# files contain one type, with methods below 80 lines.
  App.axaml remains at the 500-line limit; the shared style files remain below it.

#### Native pixel comparison

Python passively captured the actual Codex window and ordinary-permission
production App/MainWindow hosted with independent synthetic SQLite data.
The Codex image is 1936x1048; only its eight-pixel outer frame on each side was
cropped, producing 1920x1032 client pixels. No capture or comparison is resized.

| Measurement, client coordinates | Codex | Before | After |
| --- | --- | --- | --- |
| Icon bounds at luminance <160 | (70,105)-(82,117) | (69,107)-(82,120) | (70,105)-(82,117) |
| Icon size at luminance <210 | 12x12 | 14x14 | 12x12 |
| First label character at <160 | (92,106)-(104,118) | (92,107)-(104,119) | (92,106)-(104,118) |

Final icon bounds also agree at the stricter <100 and looser <210 thresholds.
The pen contour is a vector, not a copied bitmap. Identical bounding boxes are
not a claim that every contour/antialias pixel or the whole sidebar is identical.
Brand names, task contents and product-specific commands intentionally differ.

#### Interaction and regression evidence

- Native light maximized 1920x1032, light restored 1280x820 and dark narrow
  980x640 captures show the shared entry without clipping or row misalignment.
- Actual clicks on New Conversation in light/dark previews clear selected-task
  ownership and the previous task's inspector; both hosts report
  `QA_SELECTED_TASK: none`. No prompt, paid API or tool approval is submitted.
- Each native process uses an asInvoker manifest, reports `QA_ELEVATED: False`
  and exits itself with `QA_WINDOW_CLOSED` and `QA_LIFETIME_RETURNED`.
- Ten new cases cover six theme/viewport combinations, normal action-button
  isolation, keyboard activation and disabled-command behavior. The original
  sidebar fails six measured-margin cases. A scaffold assertion used the raw
  icon-label default 5 instead of the already-styled action spacing 9; it was
  corrected without changing unrelated production styles.
- Twenty-five local geometry/rhythm cases and 91 initial focused component,
  markup, header and navigation cases pass. Existing pinned/project heading
  positions remain asserted, rather than updating their expectations to hide
  a one-DIP layout regression from an intermediate iteration.
- The first full run exposed four old compose-only cases expecting the previous
  12-DIP viewport. Their size, gap, margin and optical-center assertions are
  updated to the measured resource/style geometry; project rows and section
  typography expectations are unchanged.
- The final focused run passes 111 cases. Focused C# whitespace verification and
  size/type audits pass for all four changed test files; each contains one type,
  stays below 500 lines and has no method above 80 lines.
- The final solution build succeeds with zero warnings and errors. The subsequent
  full run passes 4268/4268 cases across eight test assemblies, with zero failures
  or skipped cases. Logs and TRX files are retained in `full-final` under the
  evidence directory below. These results precede the later hot-path optimization
  slice and are not its verification results.

#### Evidence and remaining scope

Scripts, hosts, synthetic data, logs and screenshots stay outside Git:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-sidebar-compose-20261010-01`.

- `sidebar-header-comparison.png` contains native-size reference/before/after crops.
- `sidebar-header-measurements.json` records thresholded bounds and label position.
- Intermediate captures/measurements retain the rejected contour/alignment attempts.
- `native-new-conversation.png`, `native-light-normal.png`,
  `native-dark-narrow.png` and `native-dark-new-conversation.png` record interactions.
- The restored light capture includes unrelated OS maximize-hover chrome near the
  right edge; the measured left entry is unobstructed. No frame-fidelity claim is
  based on that overlay region.

This does not complete the broader Codex fidelity/product goal. Dark Codex colors,
other DPIs, every icon, draft-tab heading behavior and all responsive chat/panel
states remain outside this comparison. No staging, commit, push or publish occurs.
<!-- /source: SIDEBAR_COMPOSE_ACCEPTANCE.md -->


---

<a id="sidebar-fidelity-acceptance"></a>

## SIDEBAR_FIDELITY_ACCEPTANCE.md

<!-- source: SIDEBAR_FIDELITY_ACCEPTANCE.md -->
### Sidebar fidelity acceptance - 2026-10-07

#### 2026-10-10 task-row cleanup

- Project, pinned and recent lists now share `ReplaceTasks` and `SelectTask`.
  Equal immutable task snapshots at the same position retain their existing row;
  identical row sequences produce no collection reset or add/remove notifications.
  Changed title, status or other task fields replace the row rather than keeping
  stale command data. Duplicate project entries still own independent rows.
- Each list retains its own filtering, order, pinning and project-exclusion rules.
  Recent-task deduplication directly chooses `MaxBy(UpdatedAt)` and retains the
  first snapshot on tied timestamps and the previous cross-task tie order.
  The helper does not cache hidden rows or add repositories, mutable global state,
  security decisions, timeouts, schema changes or visual styling.
- Fourteen new regressions cover equivalent refreshes, selection, changed task
  snapshots, empty/no-match results, duplicates and tied timestamps. The narrow
  final selection passed 18 tests. An initial shared fixture compared pinned
  input with deliberately unpinned recent/project rows; its assertion was fixed,
  not the production filter. Full solution verification passed 3843 tests, with
  zero failures/skips; the solution build had zero warnings/errors.
- The rebuilt ordinary-permission App restored the selected project/task in a
  fresh process. Native checks covered dark 980x640, hiding project context and
  reading restored subagent/tool details. Light 1920x1032 checks used the earlier
  host build before this list-only cleanup. These are interaction checks, not an
  allocation benchmark or full Codex pixel-fidelity acceptance.
- Evidence is outside the repository at
  `%TEMP%/MossAgent-subagent-protocols-20261009-2325`: `sidebar-final.log`,
  `solution-build.log`, `solution-tests.log`, `cleanup-format.log`,
  `cleanup-audit.log` and `native/dark-restored-details.png`.
  No staging, commit, push, publish or AOT changes were made.

#### Ownership and scope

- App owns the sidebar composition and shared navigation appearance. Existing catalog,
  task-selection, pin, rename and archive commands remain authoritative.
- Pinned tasks, the projects heading, project groups and recent tasks share one viewport.
  Pinned tasks no longer have a separate 160-pixel scroll limit.
- Shared styles own 30-pixel task rows, one-pixel list spacing, 32-pixel closed-project
  steps, eight-pixel expanded-group separation, title fading and the light rounded thumb.
- Resting titles use the trailing space; hover, focus, open menus and running snapshots
  reserve the action slot. Short titles retain the same viewport and are not faded at
  their natural text width. Full titles remain accessible through names and tooltips.
- Running snapshots show a 12-pixel activity ring. Selection alone never implies a run.
  Target replacement and detachment close stale menus without invoking selection.
- Brushes, transforms and animation keyframes are non-control value objects. Negative
  tests still reject native buttons, text blocks, paths, borders and scroll viewers.
- No ports, migrations, tool approval rules, provider routing or publish settings changed.
  Debug/release remain ordinary JIT; Windows x64 compressed single-file publishing remains.

#### Validation

- Initial five focused regressions failed against the old composition and title layout.
- Final sidebar/navigation/component boundary selection: 137 tests passed.
- The short-title viewport assertion additionally passed all four theme/row variants.
- Full Debug solution build: zero warnings and zero errors.
- Full serial regression: 2638 passed, zero failures or skips. An earlier parallel run
  had a process-lifetime race and a history-flow timeout; both passed the serial rerun.
  No unrelated test or production behavior was changed to conceal those failures.
- Focused C# whitespace verification and diff whitespace checks passed.
- A temporary ordinary-permission host used the real App, composition root, MainWindow
  and isolated synthetic SQLite data. Native checks covered light 1280x820, dark 980x640,
  selecting the pinned conversation, opening/dismissing its menu and scrolling to recent
  tasks. Headless tests also covered sixteen pinned tasks, both themes and viewport sizes.
- The idle user application was then closed normally, rebuilt in the ordinary Debug
  output directory and restarted. Its actual process token was not elevated; the new
  sidebar was visible and `normal-app-after.png` contained a non-black 1920x1032 frame.

#### Pixel evidence and remaining limits

Evidence and Python capture/comparison helpers stay outside the repository at:
`%TEMP%/MossAgent-sidebar-20261007`.

- `sidebar-comparison.png` contains native-size Codex, before and after sidebar crops;
  the comparison removes only window-frame offsets and does not resize the images.
- `verified/sidebar-light.png`, `verified/sidebar-scrolled.png` and
  `verified/sidebar-dark-narrow.png` show the new rendered component states.
- The activity snapshot is synthetic; this round did not send a paid provider request
  or revalidate a real streaming run's sidebar marker.
- Product names, project ordering and task contents differ between the applications.
  Project sorting/reordering, rail workflows and all Codex sidebar interactions are not
  claimed complete. This is a measured layout improvement, not pixel-identical acceptance
  or completion of the overall product goal.

#### Project ordering and menu follow-up - 2026-10-07

##### Design and behavior

- App owns the project-order projection and commands; Application supplies a narrow
  sidebar-preference port; Infrastructure owns file access. Views never read or write
  preferences directly. No tool authorization, provider protocol or database migration
  is involved. All publishing remains Windows x64 JIT single-file.
- The project-section menu no longer renames, archives or pins the selected conversation.
  It now offers real activity/manual sorting with right-aligned checked state. Project
  summaries have aligned shared icons for edit/up/down actions. End-of-list and stale
  move targets are rejected even when a command is invoked directly.
- Activity sorting includes pinned and search-hidden task snapshots. Manual moves persist
  stable project IDs, retain the catalog's source order, and reconcile added/removed
  projects. Sorting preserves selected and expanded row objects. Catalog refresh preserves
  expansion, search and the selected task, including a task hidden by the current search.
- Preferences use a separate `sidebar-settings.json`, ordinary runtime JSON metadata,
  serialized writes and atomic replacement. Invalid existing content is retained and
  blocks writes. Valid unknown keys survive updates. Cancellation before replacement
  leaves the previous file intact. A detected external change rejects replacement.
- Reads and saves have a ten-second cooperative deadline and lifetime cancellation.
  Noncooperative storage keeps command ownership until it returns. Disposal detaches
  source/project subscriptions and rejects late state changes. Errors are localized and
  do not echo raw exception data. Retry is explicit.
- A regression first demonstrated that waiting for slow sidebar preferences blocked
  workspace startup. Preferences now load independently; catalog/model loading does not
  wait for them. A failed preference read disables only ordering mutations.
- New C# files remain below 250 lines, have one type per file and no methods above the
  hard limit. The catalog delegates project-navigation loading rather than growing past
  500 lines. Shared controls/appearance variants retain the native-control boundary.

##### Native evidence

- An ordinary-permission real App host with isolated synthetic SQLite data was checked
  at light 1280x820 and dark 980x640. GUI clicks selected manual sorting, moved a project
  up, closed the summary and retained the selected project/task. A new host restored the
  saved manual order; activity sorting restored the expected activity order.
- The selected marker initially appeared beside the label. A rendered regression caught
  the style-order conflict; the shared menu variant now aligns it at the trailing edge
  in both themes. Project move icons use the existing shared action layout.
- Python passive window capture produced non-black native-resolution evidence under
  `%TEMP%/MossAgent-sidebar-20261007/project-order-verified`. `sidebar-order-comparison.png`
  contains current Codex, pre-ordering MossAgent and updated MossAgent crops without
  resizing. `light-sort-menu.png`, `dark-project-menu.png`, `manual-reordered.png` and
  `restarted-manual.png` record the additional states. Helpers and synthetic data stay
  outside the repository and do not modify the user's database.
- The native running marker remains a synthetic snapshot. No paid model call or real
  streaming-marker chain was revalidated in this follow-up. Drag reordering, navigation
  rail workflows and complete pixel-identical acceptance remain outstanding.

##### Follow-up validation results

- Focused sidebar/startup/action/MCP/proxy selection: 99 passed. Infrastructure sidebar
  preference selection: 11 passed. Both rendered menu theme cases passed after the
  shared trailing-alignment fix; the slow-preference startup regression failed before
  decoupling and passed afterward.
- Final full Debug solution builds in both the validation artifacts directory and the
  ordinary user Debug directory: zero warnings and errors.
- Final serial App regression: all 1998 passed. Agent 58, Browser 76, Infrastructure 92,
  Providers 241 and Tools 147 also passed. Platform's complete serial attempt had one
  PID-file sharing race in `ShellRunToolTests`; the entire Platform project subsequently
  passed all 50 tests. The latest seven project snapshots total 2662 passed, no skips;
  this is not claimed as a single all-green full-suite invocation. No unrelated process
  implementation or test was changed to hide the race.
- Earlier internally parallel collection runs also encountered a five-second provider
  initialization timeout and an HTTP MCP completion timeout. Focused repetitions passed,
  as did the final serial App run. The external runsettings used one test-collection
  thread as well as one MSBuild worker.
- Focused whitespace and diff checks passed after fixing one test object initializer.
  Roslyn audited 18 related C# files: one declaration per file, no file/method hard-limit
  violations. Changed AXAML files also remain below 500 lines.
- The idle user application was closed through its normal Close action, rebuilt and
  restarted from the normal Debug path. The actual process token was non-elevated.
  A fresh window binding exposed the new project sorting menu, and Python recorded
  non-black 1920x1032 `normal-app-after.png` and `normal-app-sort-menu.png` frames.
  No synthetic projects/tasks were injected into the user database, and no publish,
  commit or push was performed.

#### Native project dragging follow-up - 2026-10-07

##### Ownership and boundaries

- App's shared project row owns the six-pixel mouse threshold, native drag lifetime,
  drag opacity, two-pixel themed insertion line and summary dismissal. The business
  view binds scope, command and availability; it supplies no drag geometry or colors.
- Payloads use Avalonia's in-process data format, not external text, files or a
  process-wide mutable registry. Commands check the owning sidebar scope and current
  source/target object identities, rejecting replaced objects even when IDs match.
- A same-row command probe permits drag startup but execution is a no-op. The row
  never accepts a self-drop. Unchanged adjacent placement does not write preferences
  or silently switch the selected sort mode.
- Real before/after drops reuse the sidebar-preference port and switch to manual
  ordering only after storage succeeds. Selection, expansion and source catalog order
  are preserved. Failures keep the previous order and show a safe retry message.
- Guarded generic async commands reject reentry before entering the toolkit command,
  preventing a rejected second drop/move from cancelling the save that already owns
  the editor. The existing ten-second deadline and lifetime/explicit cancellation
  remain effective; noncooperative storage retains ownership until it returns.
- The shared viewport responds immediately in its 32-pixel edge band, then scrolls
  every 16 ms, capped at eight pixels per tick and clamped to the content extent.
  Leaving a child inside the viewport does not stop scrolling; payload invalidation,
  a real viewport exit, drop, scope replacement and detachment do. AllowDrop and event
  subscriptions are restored/removed on detach. Native drag cannot be forcibly
  cancelled through a token; its invalidated payload rejects late drops and errors.
- No database migration, tool-authorization change, provider request or new file
  access in UI code is involved. Debugging and publishing remain ordinary JIT.

##### Native evidence and limits

- Native-size comparison exposed two layout errors independent of drag behavior:
  pinned task text was four pixels too low, and closed project groups accumulated
  one extra pixel per row (the final reference project was at y=750 versus y=759).
  Shared variants now use a 31-pixel closed-group stride, preserve expanded-group
  separation, and correct pinned/project/recent heading gaps. The same final label
  is at y=750, with sampled project text bands within half a pixel of the reference.
  These are measured text-band positions, not a claim that fonts/icons are identical.
- An ordinary-permission real App host with isolated synthetic SQLite data accepted
  a mouse drag of `know` before `MossAgent`, wrote manual order and restored it after
  restarting. The selected conversation and project context were retained.
- Dark 980x640 edge dragging initially produced no offset: the available native
  automation releases immediately, before a delayed timer tick. Immediate edge
  response now produces a recorded `QA_OFFSET: 0, 8` before drop. Timer continuation,
  clamping and stopped-state lifetime are separately covered by headless tests.
- The native project menu displays manual sorting after drag and restores activity
  sorting through its real command. Native captures cover light 1280x820, dark
  980x640, reordered and restarted states. Python rejects black/uniform frames and
  compares native sidebar crops without resizing; helpers and evidence remain under
  `%TEMP%/MossAgent-sidebar-20261007/drag-verified` and `drag-edge-verified`.
- Headless coverage includes both insertion directions, same/foreign/stale targets,
  disabled ancestors, missing commands, external text, expired payloads, thresholded
  click preservation, failure sanitization, cancellation/disposal and rejected reentry.
- Native Escape during a held drag, sustained mouse-dwell scrolling and cross-window
  rejection have not been manually verified. No paid model request or live running
  marker chain was exercised. Rail workflows and full pixel-identical acceptance
  remain incomplete; the overall product goal remains active.

##### Final follow-up validation

- Final full solution builds in both the validation artifacts directory and ordinary
  Debug directory passed with zero warnings/errors. Focused shared-component and
  sidebar checks covered drag/reorder behavior; the final App run also includes the
  two new twelve-project rhythm cases and corrected header-style contract.
- One final serial solution test invocation passed all seven projects: App 2039,
  Agent 58, Browser 76, Infrastructure 92, Platform 50, Providers 241 and Tools 147.
  Total: 2703 passed, zero failed and zero skipped. TRX files are under
  `%TEMP%/MossAgent-sidebar-20261007/drag-tests/confirmed-final`.
- Focused whitespace and diff checks passed. Roslyn audited 14 related C# files:
  one declaration per file and no file/method hard-limit violations. Production
  files in the audit remain below 250 lines; the largest test file has 265 lines.
- `drag-edge-verified/sidebar-drag-comparison.png` shows Codex, the pre-drag baseline
  and final layout as native-size crops. `light-final.png` and `dark-final.png`
  contain the final shared spacing; native drag/menu/restart captures remain in
  the two evidence directories. The reference was captured passively, without
  automating Codex UI or resizing screenshots to hide differences.
- The idle, empty-draft user application was closed through its normal Close action,
  rebuilt and restarted from the ordinary Debug path. Its actual process token was
  not elevated, and passive Python capture returned a non-black 1920x1032 frame.
  All synthetic QA hosts were normally closed. User data was not seeded with test
  projects/tasks; no AOT, publish, staging, commit or push was performed.

#### Scrollbar geometry follow-up - 2026-10-08

##### Design and reproduced cause

- App's shared navigation styles own this change. Existing `AppScrollViewer`
  and framework scroll actions are reused; no new port, persistence migration,
  authorization, approval, cancellation or output policy is needed.
- Passive 96-DPI screenshots found Codex's resting thumb at client x307..312,
  six pixels wide, RGB (231,233,235). The previous MossAgent thumb appeared at
  x303..304, two pixels wide, RGB (242,245,247), despite a four-DIP inner Border.
- The Fluent parent Thumb retained its resting panning transform, including
  scale 0.125 and translation -2. Testing only the inner width missed the actual
  rendered geometry. The new tests failed four cases against that baseline.
- The sidebar-only variant removes the parent transform and transitions, uses a
  six-DIP/three-DIP-radius visual inside a twelve-DIP hit area, and adds dedicated
  resting/hover theme brushes. Transparent track chrome and hidden line-arrow
  buttons do not disable the existing page buttons, wheel or thumb dragging.
- Extending the viewport nine DIP and adding matching content padding positions
  the visual three DIP inside the sidebar's right edge without reducing content
  width or changing row placement. Other scroll viewers and separator brushes
  are unchanged. No handwritten production type was added; the new test file
  has one type and remains below the file/method limits. Packaging stays JIT.

##### Native evidence and limits

- An ordinary-permission real App host with isolated synthetic SQLite data was
  rebuilt after the final one-pixel alignment correction. Python captured native
  light 1280x820 and dark 980x640 frames; helpers and test data remain outside
  the repository in `%TEMP%/MossAgent-sidebar-20261007/scroll-final-20261008`.
- `sidebar-scroll-comparison.png` compares current Codex, the previous search
  baseline and final MossAgent. Only the Codex window's (8,8) frame offset is
  removed; no screenshot is resized. Both reference and final resting thumbs
  measure x307..312 and RGB (231,233,235) at the same client-coordinate sample.
- Native light thumb dragging reached offset 488; transparent-track page-up
  returned to zero and wheel input reached 250. Dark dragging reached 668,
  page-up reached 193 and wheel-up returned to zero. The selected pinned
  conversation and project context remained unchanged in these captures.
- Dark and hover/pressed brushes are theme-consistent but have no matched Codex
  state reference. Only the resting light thumb has the direct pixel measurement.
  Thumb length depends on differing task counts/window heights, not a fixed
  copied size. Complete arrow/rail/icon typography and all sidebar interactions
  are not claimed pixel-identical; the overall product goal remains active.
- Native held-press appearance and sustained project-drag dwell still rely on
  deterministic headless tests rather than a held-input native capture. No paid
  model request or real running-marker chain was exercised in this follow-up.
- A native project drop after the viewport change reordered the synthetic project
  list while retaining the pinned conversation and project context. That capture
  did not establish an incremental edge-scroll offset; timer/edge lifetime remains
  covered by the existing deterministic tests, not a stronger native claim.

##### Validation

- Final focused selection: 31 passed, zero failed/skipped, including ten new
  theme-paired scrollbar cases. Tests cover actual transformed bounds, resting
  and hover/pressed colors, transparent track, hidden line arrows, retained
  page/wheel/drag input, disabled drag rejection, fitting content, unchanged row
  widths, real sidebar placement and existing project edge-scroll lifetime.
- An earlier focused invocation hit an Avalonia headless-session disposal
  `NullReferenceException` in an existing drag-lifetime test. Its unchanged
  eleven-case class rerun and the complete 31-case focused rerun both passed;
  no production or test behavior was weakened to hide that transient failure.
- Full validation-directory solution build passed with zero warnings/errors.
- The idle, empty-draft user application was normally closed, rebuilt from its
  ordinary Debug output and restarted. The actual process token was non-elevated.
  Fresh accessibility state retained its project, empty composer, unselected model
  and disabled Send action; input focus was restored. Python captured a non-black
  1920x1032 `normal-current.png`. Synthetic QA exited normally with code zero.
  No user data was seeded, and no publish, staging, commit or push was performed.
- The first complete serial solution invocation passed 2743/2744, with one
  existing project-row layout case failing in Avalonia session disposal with the
  same `NullReferenceException`. Its unchanged twelve-case class rerun passed.
  A subsequent single complete serial solution invocation passed every project:
  App 2080, Agent 58, Browser 76, Infrastructure 92, Platform 50, Providers 241
  and Tools 147; total 2744 passed, zero failures/skips. Final TRX evidence is in
  `%TEMP%/MossAgent-sidebar-20261007/scroll-tests/confirmed-rerun`. These are
  validation results, not a fix for the framework teardown's intermittent failure.
- Both validation-directory and ordinary Debug solution builds passed with zero
  warnings/errors. Focused whitespace checks passed, and Roslyn audited both
  related C# test files with one declaration each and zero hard-limit violations.
  Shared AXAML files remain within the 500-line limit. Nothing was staged.

#### 2026-10-08 navigation rail follow-up

The rail is now a guarded `AppNavigationRail` rather than repeated MainWindow
button markup. Home ink size/position, light selection color and settings' bottom
inset are measured against native Codex pixels. Running-task animation is scoped
to a dedicated icon theme to avoid overwriting unrelated icon transforms.
Component lifecycle, unavailable actions, negative markup boundaries, raw image
comparisons and complete regression evidence are recorded in
[NAVIGATION_RAIL_COMPONENT_ACCEPTANCE.md](UI_COMPONENTS.md#navigation-rail-component-acceptance). This is not whole-sidebar pixel parity.

#### 2026-10-08 project-row follow-up

Project rows now close their summary on ownership changes, unavailable ancestors
and command rejection, with subscriptions released on detachment. Dedicated
folder geometry and shared optical variants match the measured native open/closed
ink bounds while retaining the label's X position and independent action targets.
Twenty lifecycle cases reproduce sixteen failures before the fix. Final native
light/dark inputs, three-column unscaled screenshot evidence, regression results
and remaining text/heading/DPI differences are documented in
[PROJECT_ROW_COMPONENT_ACCEPTANCE.md](UI_COMPONENTS.md#project-row-component-acceptance). Matching icon bounds is not whole-sidebar
pixel equality, and no synthetic fixture data was added to the user's workspace.

#### 2026-10-08 pinned-heading rhythm follow-up

The pinned heading's four-pixel vertical difference is now fixed by shared group
spacing rather than a text transform across the viewport clip. Pinned tasks and
project anchors remain unchanged; the no-pinned-task layout retains its previous
insets. Native unpin/re-pin menu actions and twelve complete-window theme/width
cases verify the existing data binding and command path. Exact raw-pixel metrics,
intermediate-image exclusions and remaining typography/DPI limits are recorded in
[SIDEBAR_HEADING_ACCEPTANCE.md](UI_WORKFLOWS.md#sidebar-heading-acceptance); this is not whole-sidebar pixel equality.

#### 2026-10-08 task text and conditional fading follow-up

Task text now uses shared 14-DIP size and 18-DIP line height. The opt-in shared
text wrapper tracks actual single-line overflow, so fitting short titles retain
their complete trailing glyphs while long titles still clip/fade beside actions.
Measured light-theme body/group colors are updated without replacing global
font preferences. Theme-paired layout/lifetime tests, unscaled Python comparisons
and remaining baseline/font/DPI differences are in [SIDEBAR_TASK_TEXT_ACCEPTANCE.md](UI_WORKFLOWS.md#sidebar-task-text-acceptance).
This is not whole-sidebar pixel equality.
<!-- /source: SIDEBAR_FIDELITY_ACCEPTANCE.md -->


---

<a id="sidebar-heading-acceptance"></a>

## SIDEBAR_HEADING_ACCEPTANCE.md

<!-- source: SIDEBAR_HEADING_ACCEPTANCE.md -->
### 侧边栏分组标题间距验收

日期：2026-10-08。此记录是侧边栏布局的增量验收，不代表完整产品或整窗逐像素完成。

#### 设计自检

- 归属为 App 的共享 Styles，WorkspaceSidebar 只绑定现有
  `TaskManagement.PinnedTasks.HasItems`，不新增 ViewModel 状态或模块依赖。
- 复用现有 AppText、AppScrollViewer、任务与项目行以及置顶命令；不增加端口、
  数据库迁移或直接 I/O，不改变普通 JIT、Windows x64 单文件发布。
- 置顶仍由现有持久化命令负责；工具授权、审批、超时、取消与输出限制不变。
  QA 使用独立合成数据，Codex 只由仓库外 Python 被动截图，不输入或激活。
- 三个相关 C# 测试文件的 Roslyn 审计各一个类型，无文件/方法硬限制违规；
  ShellGeometryTests 保持在 500 行以内。AXAML 仍使用共享视觉组件，外观门禁未放宽。
- 新增 12 个完整 MainWindow 测试覆盖明暗主题、980/1280-DIP 窗宽、
  有/无置顶，以及真实置顶/取消置顶命令。保护无置顶时的原布局与选中会话。

#### 根因与修正

原视口顶边为 Y149，置顶文字墨迹从 Y152 开始；Codex 的文字从 Y148 开始。
任务行墨迹 Y181 和后续项目分组已经对齐，不能把整个任务区上移，也不能只对文字
做 RenderTransform，否则可能越过滚动视口的裁剪边界。

共享 `sidebar-actions-header` 在有置顶时把下方间距从 21 改为 17 DIP，
使视口起点变为 Y145；置顶标题的下间距从 6 改为 10 DIP，抵消任务行的位移。
无置顶时仍使用 21 DIP，视口及项目组位置保持原值。变体随 HasItems 实际状态更新，
页面不维护另一份真假状态。文字字号、行高、全局字体和项目文件夹资源不变。

完整窗口测试保护以下实际排列坐标，不只是 AXAML 字符串：

| 控件顶边 | 有一个置顶 | 无置顶 |
| --- | --- | --- |
| 导航视口 | 145 | 149 |
| 置顶标题 | 145，视口内 Y0 | 隐藏 |
| 置顶任务行 | 173，高 30 | 不存在 |
| 项目分组头 | 220 | 149 |
| 首项目行 | 253 | 182 |

置顶标题无变换、字号 14、行高 18；通过真实命令取消并恢复置顶后，
选中的项目保持同一实例，最终选中会话的完整不可变记录值与原值一致。
新测试旧生产代码为 8 失败、4 通过，八个失败均为期望 Y145、实际 Y149。
初次修复复测中四个失败来自新测试错误地要求不可变会话记录保持 CLR 引用相同；
改为检查置顶后的会话 ID 和取消置顶后的完整记录值，不改生产记录更新逻辑，
也没有删除或放宽任何几何断言。最终相关专项 58 项全部通过。

#### 原始截图

证据目录为 `%TEMP%/MossAgent-sidebar-20261007/heading-accepted-20261008`。
普通权限 QA 使用生产 App/MainWindow，明亮窗口 1280×820。
参考在同级 `heading-baseline-20261008/codex-current.png`，只裁去外围 8 像素；
三列对照 `sidebar-heading-comparison.png` 无缩放。墨迹阈值为 RGB 最大分量小于 175。

| 指标 | Codex | 修改前 | 最终 |
| --- | --- | --- | --- |
| 置顶标题 | x68..95、y148..159 | x68..95、y152..163 | x68..95、y148..159 |
| 置顶任务文字 | y181..192 | y181..192 | y181..192 |
| 项目标题文字 | y228..239 | y228..239 | y228..239 |
| 首展开文件夹 | x69..82、y261..272 | x69..82、y261..272 | x69..82、y261..272 |

最终源图为等待 QA_READY 后的 `light-ready.png`。早期 `light-resting.png`
捕获发生在项目展开状态尚未准备完毕时，不作最终对照或图标回归证据。
相同墨迹边界只证明这组尺寸/位置，不证明文字抗锯齿和图标轮廓完全一致。

#### 原生行为与限制

- 明亮窗口实际打开侧边栏会话菜单、取消置顶、从项目内会话重新置顶。
  标题随持久化结果消失/恢复，当前会话和项目上下文不变；菜单与结果截图为
  `light-task-menu.png`、`light-unpinned.png`、`light-repinned.png`。
- 深色最小窗口实际为 980×640，初始原图为 `dark-ready.png`。
  实际通过侧边栏菜单取消并重新置顶，结果为 `dark-unpinned.png`、
  `dark-repinned.png`，标题消失/恢复且选中会话保持。主题和窗口尺寸由
  测试宿主准备，不称为鼠标配置主题的证据。
- 没有付费模型调用或真实运行任务验收，合成运行标记不能替代代理执行链路。
  不向用户数据库写合成项目/任务，不暂存、提交、推送或发布。
- 首项目文字仍约差 1 像素，暗色没有同状态 Codex 原图，其他 DPI 和完整侧边栏
  悬停/按下状态未逐像素覆盖。整体目标继续进行。

#### 完整验证

- 最终相关专项 58 项通过，完整串行解决方案同一轮 2811 项通过，
  0 失败、0 跳过：App 2147、Agent 58、Browser 76、Infrastructure 92、
  Platform 50、Providers 241、Tools 147。TRX 位于同级
  `heading-tests-20261008/full-solution`，不混用上一版本结果。
- 完整验证目录解决方案构建通过，0 警告、0 错误；三个测试文件
  whitespace 验证、Roslyn 单类型/规模审计及 `git diff --check` 通过。
- WorkspaceSidebar 为 275 行、共享 NavigationActionVariants 为 241 行；
  Roslyn 审计的新完整窗口测试行计数为 147，两个现有几何测试为 101、493，
  包含末尾空行计数，均符合硬限制。
- 普通 Debug 完整解决方案构建同样为 0 警告、0 错误。用户主程序在确认
  无任务、空草稿后正常关闭并以普通权限更新；新鲜原生状态保留项目、分支、
  每次询问、未选模型、禁用发送和输入焦点，标准错误日志为空。
- 最终用户原图为 `normal-ready.png`，1920×1032、非黑色。与上一项目行版本
  `project-row-accepted-20261008/normal-current.png` 比较当前无置顶侧边栏区域
  `(52,44,316,1032)`，RGB 差图边界为 None，证明这一相同状态下没有布局/像素回退，
  不是无置顶 Codex 参考或其他状态的像素一致声明。独立 QA 正常退出，退出码 0。
<!-- /source: SIDEBAR_HEADING_ACCEPTANCE.md -->


---

<a id="sidebar-project-drag-acceptance"></a>

## SIDEBAR_PROJECT_DRAG_ACCEPTANCE.md

<!-- source: SIDEBAR_PROJECT_DRAG_ACCEPTANCE.md -->
### 侧边栏项目拖动与生命周期验收

#### 2026-10-10 设计复核

1. 行为归 App 的共享 `AppProjectNavigationRow`、拖动控制器和导航滚动组件，
   排序仍通过现有 `WorkspaceSidebarProjectsViewModel.ReorderCommand` 与
   Application 的 `ISidebarPreferenceStore`，不新增 UI 文件访问或工具旁路。
2. 项目拖动、作用域身份、上下插入、自动滚动和排序保存已存在；旧架构文档的
   “拖动未实现”与当前代码不一致，不重复开发。补全原生拖动与重启恢复证据。
3. 拖放仅接受当前 workspace 的有效载荷、当前项目身份与可执行命令；隐藏、
   禁用和不可用祖先不得接受迟到拖放或继续计时滚动。保存保留既有十秒期限、
   取消和失败不发布顺序的机制，不改变授权、审批、工具超时或模型请求。
4. 变更集中于共享组件，继续单类型、500 行文件与 80 行方法限制，不在业务
   页面覆盖行尺寸、插入线、透明度、颜色或圆角。
5. 先增加隐藏行、隐藏/禁用滚动区及祖先的失败测试，再做实际鼠标拖动、保存、
   重启、明暗截图，并运行相关回归与完整解决方案构建。

#### 精简范围

- 排序命令共用单次遍历的当前项目身份校验，执行时直接取得源与目标索引，
  不再重复 `Any`、`First`、`Remove`、`IndexOf` 和整表顺序比较。
- 先计算移除源行后的插入索引；自身和原位置拖放在构造顺序前返回，
  不保存、不切换排序模式、不发布集合变化，也不关闭摘要。
- 只为真正改变顺序的拖放构造项目 ID 列表；仍由现有保存端口、取消与期限管理。
- 拖动事件和计时滚动共用可用性及载荷作用域校验，避免两条路径的边界漂移。
- 这些优化只涉及 App 的列表与共享组件，没有新增工具、平台 API、存储格式或权限。

#### 边界修复与回归

- 修复前的针对性测试为 8/14 通过、6 失败：隐藏行与隐藏祖先仍接受 `Move`；
  隐藏或禁用滚动区及祖先后，计时器继续改变 Offset。
- 行的开始与接受拖放校验现在包含有效可见性。滚动区的事件和计时器同时校验
  可见、启用、有效载荷和相同作用域，不允许迟到事件重新启动不可用视口。
- 不改变已有鼠标阈值、上下插入、外部载荷拒绝、失效项目引用拒绝、卸载与取消行为。
- 原位置测试展开为十种自身/相邻插入组合；验证零保存、零集合通知、保持活动排序
  与摘要状态。原有六种真实重排仍验证选择/展开、原始 catalog 不变及重启恢复。
- 修复后的初始相关回归 105/105；精简后的最终项目行、拖动、排序、生命周期、启动
  及项目导航回归 120/120。
- `dotnet build MossAgent.slnx --no-restore -v:minimal`：0 警告、0 错误。
- `dotnet test MossAgent.slnx --no-build --no-restore`：默认并行运行 3978/3978，
  0 失败、0 跳过，其中 App 为 3030/3030。不通过降低并行度或延长期限获得结果。
  本轮未复现上一轮 MCP 集成等待超时，未修改 MCP 代码、测试或期限；这不证明其
  偶发超时根因已经解决。
- 六个涉及的 C# 文件通过定向 whitespace 格式和 Roslyn 规模检查：每文件一个
  类型、最多 268 行，最大方法 46 行；定向 `git diff --check` 通过。

#### 原生验收与截图

验收使用仓库外宿主引用真实 App、24 个合成项目、独立数据目录，普通权限
（`QA_ELEVATED: False`）、1280×820 原尺寸。没有提供商或模型，发送禁用，
未操作用户数据库、配置、消息、审批或仓库内容。

1. 浅色实际鼠标拖动项目 01 到 05 之后，得到 `02,03,04,05,01,06…`；
   再将 05 插入 02 之前，得到 `05,02,03,04,01,06…`。
2. 直接读取独立根的 `data/sidebar-settings.json`，通过合成项目 ID 索引确认
   `Manual` 与上述最终顺序，保存 `sidebar-after-light.json`。
3. 首个宿主在其 300 秒期限自动退出；期限到达后，补拍浅色向上结果失败，
   不使用失效窗口继续输入，不把失败截图当作有效证据。
4. 使用更新后的精简代码与同一数据根暗色重启，原生侧栏与宿主初始日志均为
   `05,02,03,04,01,06…`；偏好文件与重启前逐字相同。
5. 暗色真实拖动 01 到 02 之前，得到 `05,01,02,03,04,06…` 并保存。
   仍选中项目 01，展开状态和当前未发送草稿在拖动中保持，未误打开摘要。
6. 正常滚轮可查看项目 24，未改变排序、所选项目或当前草稿；暗色宿主通过
   Alt+F4 正常关闭，进程退出码 0，随后无残留验收窗口或进程。

仓库外证据目录：
`C:/Users/wzheng/AppData/Local/Temp/MossAgent-project-drag-20261010`。

- `red/availability-before.trx`、`focused/drag-focused.trx`、
  `focused-final/drag-final.trx`、`full-regression/*.trx` 与日志记录测试结果。
- `solution-build.log`、`format.log`、`structure-check.log` 记录构建及定向检查。
- `drag-light-before.png`、`drag-light-after-down.png`、`drag-dark-restored.png`、
  `drag-dark-after-up.png`、`drag-dark-bottom.png` 为 Python 被动截取的原尺寸图。
- `native-dark.log` 与两份 `sidebar-after-*.json` 记录真实顺序和持久化结果；
  首次浅色控制台有中文编码问题，重建仓库外宿主后改为 UTF-8，不改产品代码。
- `codex-passive-reference.png` 为验收宿主关闭后对当前 Codex 窗口的被动截图，
  没有自动点击、缩放、切换或控制 Codex。

#### 尚未通过的验收范围

- 两个应用的窗口尺寸、数据与所处页面不同，本轮不是全侧栏逐像素一致验收。
  未验证不同 DPI、窄窗口或全部主题状态，也未改变现有图标/行几何。
- 一次暗色短时拖动至视口底边未产生重排；未取得连续停留边缘时自动滚动的
  原生证据。定时器上下方向、夹紧、退出、失效及不可用祖先由确定性回归覆盖，
  不能将其替代为完整原生长距离拖放验收。
- 草稿由宿主每次启动重新赋值；只验证拖动中的保留，不能声称跨重启草稿持久化。
- 没有修改发布配置；仍为普通 JIT 调试和 Windows x64 单文件发布。未恢复 AOT，
  未提交、推送或发布。整个产品 goal 继续 active。
<!-- /source: SIDEBAR_PROJECT_DRAG_ACCEPTANCE.md -->


---

<a id="sidebar-reconciliation-acceptance"></a>

## SIDEBAR_RECONCILIATION_ACCEPTANCE.md

<!-- source: SIDEBAR_RECONCILIATION_ACCEPTANCE.md -->
### Sidebar row reconciliation optimization

#### Design self-check

- App owns this presentation-only refactor. Project, pinned and recent task
  lists retain their existing filtering, ordering, selection and catalog ports.
- One shared collection reconciliation helper replaces the project's repeated
  remove/insert/move loop and the task lists' clear-and-repopulate publication.
  Existing `ReplaceWith` users keep their current reset behavior.
- Equal immutable tasks retain their row instances after position changes;
  changed metadata gets a fresh row so task commands see authoritative state.
  Duplicate task occurrences remain independent, ordered row instances.
- Identical snapshots take the no-publication path without building row queues.
  Only changed snapshots build refresh-local candidate queues; there is no retained
  cross-list cache or mutable global state.
- No authorization, approval, timeout, cancellation, output, persistence or
  module dependency changes. Shared controls/styles and JIT single-file settings
  remain unchanged. Production methods remain below the 80-line hard limit.
- Public-view-model tests cover move/add/remove notifications, unchanged row
  identity, selected/running metadata, filtering, empty/retired tasks, duplicate
  task occurrences and repeated project references.

#### Verification

- Before implementation, all 19 new task-list cases failed: position changes
  recreated rows and publication emitted `Reset`, including metadata-only updates.
- Post-change focused regression passed 99/99, including all 23 new task/project
  cases. The new project cases additionally cover repeated object references and
  removal of their final occurrence without resurrecting detached rows.
- `dotnet build MossAgent.slnx --no-restore -v:minimal` passed with zero warnings
  and errors. Whitespace verification for all five touched C# files passed;
  file/type counts and `git diff --check` passed.
- The first full solution test run passed 4834/4836 with no skipped tests. All
  3501 App, 8 visual, 76 browser, 83 agent, 171 infrastructure, 377 provider and
  483 built-in-tool cases passed. Two unchanged Windows cases failed under the
  concurrent run: `StopRetainsNativeResizeUntilReturnAndBlocksReplacement`
  timed out waiting for its native-resize entry, and
  `Reload_TracksEnabledUpdatedAndDeletedServers` did not discover its fixture
  tool before the startup deadline. Running the Windows project separately
  then passed 137/137, including both cases. No unrelated code was changed;
  the initial full-run failures remain recorded rather than called a clean run.

#### Post-refactor native check

- Rebuilt the repository-external normal-JIT host after the refactor. Its copied
  App DLL SHA256 matched the current repository build. Fresh synthetic data lived
  in the earlier evidence root's `sidebar-post` directory; ordinary permissions,
  a light theme and a 1280-by-820-DIP window were used.
- Opened search through the actual Ctrl+K shortcut and entered `并发任务 B`.
  Project and recent lists showed only B while the A conversation stayed selected.
  Selected B through its retained sidebar row; both lists restored, B's selected
  state and message changed together, and existing conversation tabs remained.
- Opened B's row menu, pinned it and inspected the real pinned section. B left
  project/recent rows while A remained; B stayed selected in the conversation.
  A read-only query of `pinned_agent_tasks` contained precisely B's task identity.
- Used the pinned row's `取消置顶` action. B returned to project/recent lists,
  remained selected, and the pinned table was empty. No task was deleted or
  archived, no model request was sent, and no tools executed in this UI check.
- Passive Python captures saved `search-filtered.png`, `task-b-selected.png`,
  `task-b-pinned.png` and `task-b-unpinned.png`; actual screenshots were inspected.
  Host shutdown awaited the workspace and returned `QA_FINAL_ACTIVE=False`,
  `QA_EXIT=0`. Its PID was absent after exit.
- The helper initially could not click the search trigger because coordinate
  geometry was unavailable; Ctrl+K opened it. Accessibility value-setting reported
  a timeout but fresh observation verified success, so the action was not repeated.
  These are automation limitations, not silently repaired production behavior.
- The preceding external-Chrome concurrency acceptance used the pre-refactor
  build. Its results and the post-refactor native sidebar check have separate
  scope; browser regression tests, not another concurrent Chrome run, validate
  unchanged browser behavior after this App-only refactor.
- This refactor does not establish Codex pixel equivalence or whole-product
  completion. No commit, push or release is performed.
<!-- /source: SIDEBAR_RECONCILIATION_ACCEPTANCE.md -->


---

<a id="sidebar-shutdown-acceptance"></a>

## SIDEBAR_SHUTDOWN_ACCEPTANCE.md

<!-- source: SIDEBAR_SHUTDOWN_ACCEPTANCE.md -->
### Sidebar storage ownership during shutdown

#### Design self-check

1. App owns view-model lifetime and shutdown coordination. Dependency direction
   remains unchanged, and no platform-specific code is added to App.
2. Existing `ISidebarPreferenceStore` and guarded load/sort/move/reorder commands
   retain their behavior. No new port or service is needed.
3. Shutdown retains the existing linked cancellation and ten-second operation
   deadline. It now waits for canceled storage operations to release their owned
   resources instead of treating cancellation alone as completion. There are no
   new capabilities, approval changes, persistence formats or migrations.
4. Production view models remain below the hard file/method limits. Every added
   C# file declares exactly one type; no partial class split is introduced.
5. Production-workspace tests cover shutdown and async disposal for initial load,
   retry, both sort modes, both move commands and drag reorder. A real synthetic
   file lock proves resource ownership; negative checks cover blocked commands,
   canceled writes, late results, source changes and observer notifications.

#### Confirmed issue and correction

- Fourteen new cases fail against the previous implementation. Graceful shutdown
  reports completion without canceling sidebar storage, while async disposal
  cancels it but reports completion before the storage operation releases its
  exclusive file handle. The tests release the blocked operation in `finally`.
- Sidebar projects now expose one shared stop task and async disposal. Stop first
  disables commands/unsubscribes/cancels through the existing synchronous Dispose,
  then joins the initial load and all six guarded command execution tasks.
- Workspace graceful shutdown and async disposal join that stop task alongside
  their existing repository/statistics cleanup. No fire-and-forget file operation
  is promoted to a completed cleanup merely because its token was canceled.
- Four additional cases verify repeated stop identity, synchronous-then-async
  disposal and cancellation of an individual waiter without abandoning storage.
- These tests use independent synthetic repositories, a loopback provider fixture
  and test-owned temporary files. They do not alter user data, call paid services,
  or operate existing desktop windows.

#### Evidence boundary

- The earlier full-run restart cleanup `IOException` leaves an empty owned
  directory; the original failure and directory remain preserved. The current
  inspection does not identify its locking owner. The earlier sidebar allocation
  assertion also remains unchanged, including its strict zero-byte expectation.
- The new deterministic lifetime failure is confirmed independently. It is not
  asserted to explain either earlier complete-run failure without further evidence.
- Reports are outside the repository in
  `%TEMP%/MossAgent-sidebar-shutdown-20261015`; `baseline.trx` retains the fourteen
  failing cases and `fixed.trx` retains their first passing run.
- Native captures remain postponed. This lifetime slice is not full-product or
  current same-state Codex visual acceptance.

#### Validation checkpoint (2026-10-15)

- All 69 related cases pass, including the eighteen new workspace/async-disposal
  cases, the original strict allocation and conversation-restart cases, sidebar
  ordering/drag/lifetime checks, graceful shutdown, close coordination and seven
  architecture checks. The original full-run failures are not replaced by this
  subset's success.
- Solution build has zero warnings/errors. Scoped whitespace verification passes.
- All eight complete suites pass 6,467/6,467 with no skips or aborts: App 4,706,
  Agent 123, Infrastructure 204, Providers 542, Tools 494, Browser 76,
  Platform.Windows 142 and pixel rendering 180. App finishes normally in 13m25s;
  all processes exit. The eight tested assembly hashes remain unchanged.
- The three original conversation-restart cases and six strict sidebar allocation
  cases all pass in this complete run, not only in isolation. The original failing
  reports and retained empty directory remain preserved. Their historical causes
  are still unproven; this successful checkpoint is not attributed to the new
  sidebar lifetime fix without evidence establishing that causal link.
- Baseline/focused runs are not added to the complete-suite count. All eighteen
  new lifetime cases, the preceding header/alignment cases and prior workflow
  regressions participate in the current complete App/pixel suites.
<!-- /source: SIDEBAR_SHUTDOWN_ACCEPTANCE.md -->


---

<a id="sidebar-task-text-acceptance"></a>

## SIDEBAR_TASK_TEXT_ACCEPTANCE.md

<!-- source: SIDEBAR_TASK_TEXT_ACCEPTANCE.md -->
### 侧边栏任务文字与溢出渐隐验收

当前任务文字基线的原生复测与剩余字体差异见
[SIDEBAR_REFRESH_OPTIMIZATION_ACCEPTANCE.md](ENGINEERING.md#sidebar-refresh-optimization-acceptance)。本文保留 2026-10-08 的历史测量。

#### 2026-10-08 设计自检

- 行为属于 App 的共享 `AppText` 与 Styles，不新增应用端口、数据迁移或工具调用。
  授权、审批、超时、取消、输出限制及普通 JIT/单文件发布保持不变。
- 原生截图发现任务字号 13 比参考小，短标题的最后一个字也被渐隐遮罩淡化。
  不是截图缩放问题，也不能只移动文字来修复字体宽度或尾字透明度。
- `AppText.FadeOverflow` 默认关闭；共享任务行变体开启此能力，文字实际溢出且
  不换行时才进入 `:overflowing` 状态，由共享样式提供遮罩。文字、字号、字体、
  宽度和操作区域变化通过原有布局流程重新计算，卸载清除状态，不新增事件订阅。
- 任务字号统一为 14 DIP、行高 18 DIP；固定行高同时消除中英文自然行高不同
  导致的文字与操作按钮中心半像素差异。任务高度仍为 30 DIP，缩进和操作保留
  区域不变，项目图标、置顶分组节奏及其他文字角色不受此字号调整影响。
- 明亮主题侧边栏正文改为 `#3B3E40`，分组标题改为 `#8B8D8F`，对应本次
  Codex 原图的主要实色。暗色颜色、全局默认 Inter 和用户的明暗字体偏好保持不变。
- 不新增类型文件以外的声明；`AppText` 39 行、新测试 223 行，各一个类型，
  Roslyn 检查四个相关 C# 文件，没有文件、类型或方法硬限制违规。

#### 原生截图与字体实验

Python、字体实验宿主和图片均留在仓库外的
`%TEMP%/MossAgent-sidebar-20261007`，不提交仓库。宿主使用生产 App/MainWindow，
独立合成 SQLite 数据，普通权限，不调用模型，不向用户数据库添加测试任务。

- 六组原始字号/字体/行高实验及后续字体回退实验没有直接替换全局字体。
  Segoe 的部分拉丁宽度更接近参考，但基线仍不同；强制缩短行高还可能裁掉
  字形顶部。不能依据单一包围盒就认定应全局换字体或加文字偏移。
- 实验期间一轮尺寸曾变为 1600×1025，尺寸保护拒绝保存；之后成功取得
  1280×820 的六组图。原因未确定，未修改系统缩放，也不宣称修复了 DPI 问题。
- 最终证据目录为 `task-text-final-20261008`：`light-ready.png` 为 1280×820，
  `dark-narrow-ready.png` 为 980×820，`codex-current.png` 为 1936×1048。
  对照图 `sidebar-task-text-comparison.png` 只裁掉 Codex 外围 8 像素并并排裁切，
  所有验收图不缩放。用于看字形的放大诊断图不是验收截图。

下表按最大 RGB 分量小于 190 的墨迹测量同名任务“排查截图工具黑屏问题”：

| 指标 | Codex | 修改前 | 最终 MossAgent |
| --- | --- | --- | --- |
| 墨迹边界 | x92..231，y354..366 | x92..218，y354..365 | x92..231，y355..367 |
| 墨迹宽×高 | 140×13 | 127×12 | 140×13 |
| 正文主要实色 | 59,62,64 | 62,66,72 | 59,62,64 |
| 置顶标题主要实色 | 139,141,143 | 104,106,108 | 139,141,143 |
| 置顶标题墨迹边界 | x68..95，y148..159 | x68..95，y148..159 | x68..95，y148..159 |

最终短任务在 140、190、235 三个阈值下均保留完整 x92..231；原版本末尾
随阈值扩大，反映遮罩淡化，而不只是字形大小不同。分组边界不变，正文主要
实色和短标题宽高已匹配；抗锯齿覆盖率与一像素基线差异仍存在。

#### 回归与运行范围

- 新增 22 项主题配对测试，覆盖短标题不渐隐、长标题裁切渐隐、文字与窗口宽度
  变化、空文字、键盘焦点和运行标记占位、选择不变、字体继承与字号更新、显式
  关闭渐隐及卸载/重新挂载。普通文字不自动套用渐隐。
- 初始 16 项测试对旧代码为 14 失败、2 通过。首次修复后四处失败来自新测试
  选择了仍可容纳文本的边界数据，已修正测试尺寸及字符数，没有放宽溢出断言。
- 实际整窗旧测试的字号 13 断言更新为 14 后，继续暴露中英文中心相差半像素。
  共享 18-DIP 行高修复布局，而不是放宽中心对齐断言。最终专项 84 项全部通过，
  包含任务菜单、项目图标、置顶节奏、三个窗口宽度与两种主题。
- 最终原生明亮窗口实际打开短标题会话的菜单并关闭；短标题末字保持清晰，
  菜单存在真实置顶、重命名与归档入口。本次没有执行归档或改名操作。
  暗色窄窗口取得原图并检查输入区。本次未进行付费模型调用。
- 焦点/运行占位的所有状态切换、字体选择切换与卸载边界主要由确定性测试覆盖；
  不能把原生打开菜单等同于所有边界都完成了真实鼠标验收。
- 完整串行解决方案测试 2833 项全部通过，零失败/跳过：App 2169、Agent 58、
  Browser 76、Infrastructure 92、Platform 50、Providers 241、Tools 147。
  TRX 位于 `task-text-tests-20261008/full-solution`。验证目录与普通 Debug 的
  完整解决方案构建均为零警告/错误；相关 C# 格式、Roslyn 结构及 diff 检查通过。
- 确认用户主程序只有一个项目、无任务、空草稿、未选模型且发送禁用后，正常
  关闭并更新普通 JIT Debug 程序；新进程权限检查为非管理员。最终 `normal-ready.png`
  为完整非黑的 1920×1032，原项目、每次询问策略、空输入和禁用发送状态保留，
  输入框显示光标。所有合成宿主正常退出、退出码零，没有暂存、提交、推送或发布。
- 更新前旧主程序的尺寸断言曾拒绝 1920×1020；不带尺寸断言的诊断捕获有右侧
  和底部黑区，不能当作最终原图。关闭操作第一次因缺少捕获几何而失败，重新
  绑定并取得截图后正常关闭，没有强杀。更新后完整图成功不证明 DPI 或截图
  实现的根因已被修复；未修改系统缩放、截图实现或用户数据。

#### 未完成的视觉范围

任务中文基线仍比参考低一像素，拉丁字体、字形抗锯齿、标题截断长度、品牌、
搜索/通知图标和更多侧边栏状态仍需继续对照。暗色没有同状态 Codex 像素参考，
125%/150% DPI 尚未验收。本轮不宣称侧边栏或整个产品已经一比一完成。
<!-- /source: SIDEBAR_TASK_TEXT_ACCEPTANCE.md -->


---

<a id="ui-compact-layout-acceptance"></a>

## UI_COMPACT_LAYOUT_ACCEPTANCE.md

<!-- source: UI_COMPACT_LAYOUT_ACCEPTANCE.md -->
### Compact interface polish

#### Scope and design review

- App owns these layout changes. No application ports, persistence or platform
  dependencies change; this is presentation of existing data and commands.
- Preserve the Codex-style palette, shared surface shadows, 28px send button,
  12px composer icons and 8px rectangular dropdown corners.
- Keep appearance in shared styles and reuse the existing control wrappers.
  No handwritten file or method exceeds the repository's size limits.
- No new I/O, tool execution, approvals or authorization is introduced. Existing
  projectless restrictions, draft handling, commands and model identity remain.

#### Behavior

- The composer model selector previously capped its entire content at 180px.
  An unbounded reasoning label could consume that space and hide the model name.
  Its shared style now allows up to 320px, bounds the reasoning label and keeps
  the full description in the existing tooltip and accessible name.
- Below 360px of actual toolbar width, project actions and model/send actions
  occupy separate rows. Projectless chat does not reserve an empty action row.
  Resize and project switching restore the appropriate layout without sending.
- Below 480px of settings row width, metadata and status move below the title.
  Actions retain their shared alignment and size. Empty metadata rows collapse.
- Focus inside the composer uses the theme's focus border without changing
  border thickness, bounds or the shared shadow. Blur restores its idle border.

#### Verification

- `CompactSurfaceRenderingTests` renders production components in both themes,
  320/360/460/640/800px cases and 100/125/200% scaling. It checks readable text,
  contained actions, unchanged send geometry and focus/blur layout stability.
- `ComposerResponsiveLayoutTests` uses the production workspace composition and
  a local synthetic provider to verify repeated resizing, project switching,
  retained drafts/policy and zero unintended provider requests.
- `SettingsItemRowLayoutTests` covers long labels, alignment, live resizing and
  removal of metadata/status without empty layout slots. Existing command and
  projectless keyboard tests exercise the unchanged interaction boundaries.
- Set `MOSSAGENT_VISUAL_EVIDENCE_DIR` to save rendered PNGs outside the repository.
  These are deterministic offscreen renders, not live native-window captures or
  evidence of complete pixel equivalence to Codex Desktop.

#### Current validation checkpoint (2026-10-15)

- All six complete non-UI suites pass: Agent 123, Infrastructure 204, Providers
  542, Tools 494, Browser 76 and Platform.Windows 142. Complete pixels pass 136.
  These seven tested assembly hashes remain unchanged after the solution build.
- The first complete App run finishes normally in 13m15s: 4,611/4,615 pass, with
  four failures and no skips/aborts. Its longest case takes 3.654s; this is a long
  complete run, not evidence of a stalled test. The earlier five-minute cutoff
  was not sufficient to establish a complete result.
- The failures are old assertions in `ShellGeometryTests`,
  `SharedControlStyleTests` and the two `ModelSelectorComponentTests` theme cases.
  They still expect the per-view alignment, SurfaceVariants ownership or 180px
  cap replaced by the shared model-selector style. The corrected three classes
  pass all 33 cases without removing cases or weakening containment checks.
- The corrected complete App rerun finishes normally in 13m01s: 4,614/4,615 pass,
  with no skips/aborts. All four shared-style failures above pass in this run.
  The sole failure is the untouched
  `WorkspaceCatalogLookupTests.WarmCatalogLookups_DoNotAllocate(missing: true)`:
  it observes 5,104 allocated bytes instead of zero. Neither that test nor its
  lookup implementation is changed by this UI follow-up; the cause is unproven.
  Its complete five-case class passes three independent no-build reruns, but
  those 15 results do not replace the complete run's failure.
- Across all eight complete suites, 6,331/6,332 pass with no skips/aborts. This
  is not an all-green checkpoint. Focused and isolated reruns are not counted
  again. All eight tested assembly hashes match the checked build outputs.
- Solution build has zero warnings/errors. Scoped whitespace formatting and
  diff checks pass. No user data, running windows, permissions, publishing options,
  commits or remote branches are changed. Native captures remain postponed.

Reports are outside the repository in `%TEMP%/MossAgent-current-goal-tests`:
`app-complete.trx` is the initial complete run, `updated-guards.trx` is the focused
rerun, and `app-final.trx` records the corrected complete rerun.
`catalog-isolated-1.trx` through `catalog-isolated-3.trx` retain the independent
catalog-class reruns. The complete console log is
`%TEMP%/MossAgent-current-goal-app-final.log`. Full-product and current same-state
Codex/native interaction acceptance remain open.

#### Model list follow-up (2026-10-15)

- App owns the changes in `AppModelSettingsRow` and the shared provider-row
  styles. There are no new ports, I/O, permissions, persistence, publishing or
  tool-execution changes. Existing model commands and ownership remain intact.
- Below 480px of actual row width, model name, model ID, context and reasoning
  use separate full-width lines instead of competing for two half-width columns.
  Wider rows retain the existing two-column layout. Live resizing restores both
  layouts without replacing the model or changing command parameters.
- Both detail grids share their column spacing. Provider/model text uses an
  18px minimum alignment slot, while row actions retain their centered 24px
  geometry. No per-view dimensions or colors are introduced.
- Essential model details use the existing muted text brush rather than the
  lower-contrast subtle brush. The rendered control properties are checked for
  at least 4.5:1 foreground/background contrast in both themes. Context and
  reasoning gain full-value tooltips; the existing keyboard-accessible edit
  action still opens their complete configuration.
- Before implementation, the new visual checks reproduce a 101px model-name
  slot at 320px and missing reasoning tooltips. Ten model-row cases fail; eight
  navigation-focus cases already pass, so existing focus styling is retained.
- Regression coverage includes 320/480/640px, 100/125/200% rendering, both themes,
  repeated breakpoint crossings, current model command identity, and visible
  keyboard focus without geometry changes on settings/history/disclosure controls.
- Before/after PNGs and test reports are written outside the repository to
  `%TEMP%/MossAgent-ui-polish-20261015`. These are offscreen renders, not captures
  of the user's desktop or evidence of complete Codex pixel equivalence.
- This follow-up passes 111 focused App cases, all 154 pixel-rendering cases and
  seven architecture cases (272 unique cases, no skips). The solution build has
  zero warnings/errors; scoped whitespace verification and diff checks pass.
  This is not a new complete eight-assembly run and does not erase the earlier
  complete run's allocation-test failure documented above.

#### Shared alignment follow-up (2026-10-15)

##### Design self-check

1. App owns this presentation-only correction; module dependencies are unchanged.
2. Existing shared wrappers, provider commands and task failure hints are reused.
   No new port or service is needed.
3. There is no new I/O, authorization, approval, timeout, cancellation, persistence
   or output-limiting behavior. Evidence uses synthetic values and offscreen windows.
4. The added test file declares one type and stays within the file/method limits.
   Appearance remains in shared styles rather than business views or view models.
5. Rendering tests cover initial geometry, both themes, multiple widths/scales,
   live resizing and shorter hints. Unrelated icons and panels are negative cases
   proving that the corrected selectors do not apply globally.

##### Reproduction and correction

- Fourteen new rendered cases reproduce two inactive derived-type selectors.
  `AppUserControl` retains the native UserControl style key; the provider header's
  text stack therefore remains stretched, and failure notices lose their intended
  4px side/16px bottom margins and first-line warning alignment.
- Before/after renders confirm the warning is vertically centered against the
  whole multiline error before the fix. It now aligns with the first line;
  provider text and its 24px actions share a vertical center.
- Both controls gain stable root classes used by the scoped shared variants.
  Native base style keys, shared icon dimensions and command ownership remain
  unchanged. Other static selector candidates are not treated as confirmed bugs
  or modified without their own rendered evidence.
- The first candidate passes all six provider cases and the failure notices'
  initial checks, but eight resize assertions observe queued headless layout.
  The fixture now drains dispatcher jobs before checking the resized short hint;
  there is no sleep, production workaround or weakened geometry assertion.
- Reports and before/after PNGs stay outside the repository at
  `%TEMP%/MossAgent-shared-alignment-20261015`. These are deterministic offscreen
  renders, not native desktop screenshots or a Codex pixel-equivalence claim.
  Native captures remain postponed; no existing application window is operated.
- Fresh validation passes all 180 pixel-rendering cases and 93 related App cases
  (273 distinct cases, no skips). This includes all fourteen new alignment cases.
  The solution build has zero warnings/errors; scoped whitespace formatting,
  diff checks and production/test-copy App assembly identity checks pass.
- This is a targeted UI checkpoint after the complete 6,433/6,435 header run in
  [SETTINGS_PAGE_HEADER_ACCEPTANCE.md](UI_WORKFLOWS.md#settings-page-header-acceptance). Its untouched allocation and restart
  cleanup failures remain open; passing this subset does not replace them.
<!-- /source: UI_COMPACT_LAYOUT_ACCEPTANCE.md -->


---

<a id="ui-metadata-polish-acceptance"></a>

## UI_METADATA_POLISH_ACCEPTANCE.md

<!-- source: UI_METADATA_POLISH_ACCEPTANCE.md -->
### Shared metadata and approval controls

#### Scope and ownership

This pass improves the existing Codex-oriented desktop layout rather than
introducing a different visual system. It uses the UI/UX skill's accessibility
checklist and its Avalonia keyboard-navigation/shared-style guidance. The compact
consistency search did not return a verified platform-specific UX match; the
measured contrast and spacing requirements below are explicit review criteria,
not an unverified generated design system.

All production changes belong to App. Shared AXAML wrappers and theme tokens own
appearance; `ApprovalTaskControlViewModel` adapts existing workspace commands.
There is no new provider, persistence, filesystem, process, or tool-execution
implementation in UI code. Existing approval policies, task cancellation tokens,
authorized roots, output bounds, audit persistence, JSON and JIT/single-file
publication remain unchanged.

#### Visible changes

- Secondary metadata uses the same semantic foreground across existing surfaces.
  Light subtle text changes from `#8A8A8A` to `#6B6B6B`; sidebar section text uses
  `#6B7075`. Dark variants use `#A0A0A0`. Main surfaces, primary text, titlebar
  colors, corner radii and shadows are retained.
- The former light subtle foreground had 3.45:1 contrast on white and 2.99:1 on
  the selection surface. The new subtle foreground has at least 4.61:1 across
  the six checked light surfaces; sidebar headings exceed 4.5:1 on their two
  checked surfaces. Both dark roles also exceed 4.5:1 on the checked surfaces.
  This is not an assertion that every text/background/opacity pair is compliant.
- Tool activity headers have matching 6-DIP horizontal insets for static and
  expandable rows. Status text uses the shared muted foreground at 11 DIP and
  a 120-DIP maximum width, with ellipsis and the complete status in a tooltip.
  The existing button help text retains the complete status for accessibility.
  Rows remain 32 DIP high and activity glyphs remain 12 DIP.
- At 125% scaling an inset can round by half a physical pixel. Rendering tests
  allow exactly that rounding bound instead of requiring an impossible exact
  logical-pixel result or disabling layout rounding.

#### Approval stop recovery

Production Headless interaction exposed a real compact-layout gap: the expanded
approval card covered the composer's stop button at 526x601. It reproduced in
both themes. The first complete external driver run passed 22/24 scenarios and
retains those two failures; it was not reported as all green.

The approval footer now offers a localized stop-task action when the displayed
request belongs to the currently selected, actively running task. It reuses the
existing workspace stop command, never resolves approval as granted and never
changes an approval policy. It is hidden for missing context, another task,
completed runs and disposal. Request identity is checked against the live
approval queue, and `AppApprovalButton` retains gesture identity. Stale or null
request IDs cannot stop a different task or turn an old stop into a new send.

Focused negative tests cover another task at the head of the approval queue,
queue advancement, stale/null IDs and disposal. Stopping a pending edit leaves
the file unchanged and persists `tool_cancelled`, an unapproved audit and a
cancelled task through the existing pipeline.

#### Verification and limits

- The pre-change eight-assembly checkpoint passed 6,690/6,690: App 4,815,
  Visual 282, Agent 123, Browser 88, Infrastructure 204, Platform.Windows 142,
  Providers 542 and Tools 494. All eight original test DLL hashes remained
  unchanged throughout that checkpoint. This is a baseline, not a full run of
  the later UI candidate.
- The current candidate's complete Visual assembly passes 300/300, with no
  failures, skips or aborted cases. The initial run retained four overly exact
  125%-inset assertions and six old sidebar-color expectations; those explicit
  expectations were updated to the intended design and all tests were rerun.
- The final candidate passes 275/275 focused App cases, with no failures, skips
  or aborts: approval identity/cancellation/disposal, file editing/undo, MainWindow
  lifetime, tool geometry/overflow, shared appearance boundaries and MCP recovery.
  The new nested footer required updating its old two-sibling structural
  expectation; the replacement checks all three commands and their distinct
  columns/request parameters. All 275 cases were rerun, not only that assertion.
- The external production MainWindow driver passes all 24 combinations of
  light/dark, 1280x820/526x601 and success/deny/conflict/cancel/read-only/path
  escape. It types and sends, operates approval/content controls, reviews stored
  changes, cancels then confirms undo, and interacts with restored history.
  Real SQLite, the task runner, loopback model HTTP and the shared executor are
  used. Files, outside sentinels, model request counts, tool results and audits
  are checked; reopening causes no model or tool replay.
- Successful review/undo has five distinct successful audits: read, edit,
  change statistics, snapshot review and restore. The first external helper
  incorrectly expected three and was corrected after inspecting all identities;
  no production audit was suppressed or removed.
- Full solution builds use a separate ignored artifacts directory, with zero
  warnings/errors, and do not overwrite the baseline run's outputs. Eleven
  changed C# files pass Roslyn one-type/file and size checks: at most 473 file
  lines and 72 method lines. Changed AXAML remains below 500 lines.

Reports and before/after frames are under
`%TEMP%/MossAgent-ui-polish-20261016`. The final 24-case interaction report is
`%TEMP%/MossAgent-file-ui-20261016/run-7246991f36904c7b9acc0cbb93c23b1f/result.json`.
That final run also compares the complete audit-call identities before and after
restoration, in addition to unchanged model request counts. The App DLLs in the
build, both test outputs and the external driver have one identical SHA-256.
The source driver and its captures remain outside the repository.

These are independent Skia/Headless frames and real production-composition
interactions, not native Windows input or fresh captures of Codex. No existing
Codex/MossAgent desktop window was operated, restarted or captured. Seeded
project/provider configuration is not acceptance of those editors. Full current
application regression beyond the focused cases, all native DPI/state combinations and same-state Codex
pixel equivalence remain separate gates. No commit, push or release is made.
<!-- /source: UI_METADATA_POLISH_ACCEPTANCE.md -->


---

<a id="window-caption-contrast-acceptance"></a>

## WINDOW_CAPTION_CONTRAST_ACCEPTANCE.md

<!-- source: WINDOW_CAPTION_CONTRAST_ACCEPTANCE.md -->
### Window caption contrast

#### Reference and design self-check

The preceding unscaled passive comparison at 96 DPI shows the same minimize,
maximize and close ink extents, but foreground luminance is 31 in Codex versus
93 / 130 / 93 in the current App. App's caption buttons inherit the ordinary
ghost/close TextMutedBrush (`#5D5D5D` in light); vector rasterization additionally
affects the maximize sample. Treat palette inheritance and rasterization as
separate causes; a brush correction alone must not be reported as pixel equality.

1. App's shared window-caption style owns this presentation. No module dependency,
   native process integration or per-view appearance assignment is needed.
2. Reuse AppIconButton, existing caption commands, hit areas and pointer/focus
   behavior. Dedicated caption palette resources must not recolor navigation,
   ordinary close actions or other ghost buttons.
3. No tool, provider, persistence, approval, cancellation or authorization behavior
   changes. Native replay uses an independent synthetic data root and no message
   submission; Python capture remains passive and outside the repository.
4. Consolidate caption geometry and palette in a shared caption variant loaded
   after generic button styles, keeping markup under its hard limit. Each new
   test type gets its own file.
5. A red baseline must reproduce the muted foreground in both themes and sizes.
   Verify normal/hover/leave, red close hover and white glyph, Arrow cursor,
   retained 46x36 hit areas, non-focusability and unchanged navigation color.
   Then run related tests, pixel tests and a solution build, followed by native
   comparison. Dark caption readability is tested; without a matching dark
   reference, do not claim exact dark Codex equivalence.

#### Shared correction and deterministic evidence

The caption-only resource uses `#1F1F1F` in light and white in dark. Shared
`WindowCaptionVariants` owns normal/hover/disabled palette and the retained button
geometry. It loads after generic ghost styles; a first rule in the earlier
WindowVariants is ineffective because later generic styles overwrite it. That
ineffective rule is removed rather than retained as a competing override.
The existing red close-hover rule and white glyph remain authoritative. Ordinary
navigation and non-caption ghost/close buttons keep their existing muted colors.
App.axaml is 494 physical lines; caption geometry stays in its dedicated variant
and palette resources rather than growing the application markup further.

Evidence lives outside the repository at
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-caption-contrast-20261014`.

- All four baseline cases fail with the old muted caption foreground. The first
  style-only attempt also fails all four and reveals include-order precedence.
  A subsequent test lookup incorrectly requests a theme resource without a theme;
  the test now compares the existing documented light/dark muted colors directly.
  Final caption, icon, focus and window appearance coverage passes all 36 cases.
- Eight real Skia rendering cases exercise both themes at 1 / 1.25 / 1.5 / 2
  render scale. Low-alpha PNG round-tripping exposes over-strict unpremultiplied
  channel assertions, not a palette change. The final guard allows two eight-bit
  premultiplied channel quantization units. Eight negative cases prove the old
  muted colors cannot satisfy that guard at alpha 64 / 128 / 192 / 255. All sixteen
  new cases pass. These are render scales, not native monitor/DPI transitions.
- The first root-level driver exits one on the earlier too-strict pixel guard
  (55/56 pass); it is terminal before the repaired test assembly is built. Its
  files remain failed history, not passing final evidence. `final-gates` belongs
  to the palette-only driver and recorded palette-only assembly hashes.
- Final scoped formatting and a solution build pass with zero warnings/errors.
  All 64 pixel cases and seven architecture constraints pass. The complete App
  assembly gate subsequently finishes with 4,264 passes and two failing source
  ownership guards still looking in WindowVariants. Those guards now follow
  WindowCaptionVariants and also require its include after generic ghost rules.
  This failed historical complete run is not counted as a passing checkpoint.

Palette-only App DLL SHA256:
`F9D569C3A83DDDFE24AA04FAB750E92567B8F8B83FA14E61F7B0DEE855838A1B`.
Palette-only App test DLL SHA256:
`A6022EACC42AEF1CE8D64C674BA8DE3D9778CB80EE3F5541B309FF84EBD00BD1`.
Palette-only pixel test DLL SHA256:
`AF6C38988A92A8F206FFDF6FF0CF19AFB6762CBEF7C1AD538B7627839660855A`.

#### Native replay and actual comparison

The same Debug/JIT App runs at ordinary permission and 96 DPI with an independent
synthetic data root. Passive Python capture verifies PID ownership, foreground,
window geometry, elevation and non-black content. Five settled native frames
cover light max/normal, dark normal/max settings and restored light max settings.
Immediate Computer Use input frames contain a transient blue pointer highlight;
they are not substituted for the settled Python images.

The real restore action returns 1920x1032 to 1280x820. The real minimize command
sets the owned HWND's IsIconic state true while its App process remains alive.
Reactivating that same returned window makes IsIconic false and restores its
maximized settings page. This is native command evidence, not just icon presence.
The actual close command subsequently exits the App and observer zero; both
processes disappear and loopback port 51792 is released.

Open-after-theme-restoration and closed audits both pass integrity, all nineteen
table fingerprints, three original artifacts and the unchanged four-request
history. Original Light UI-settings bytes are restored exactly. No real user
settings, model submission, approval, AOT, JSON adapter, packaging, commit, push
or release changes occur.

`caption-native-before-after.png` contains unscaled 96-DPI ordinary-window caption
crops from fresh Codex, the preceding App and this candidate. All three preserve
10x1 / 10x10 / 10x10 ink boxes at the same comparison threshold. Native darkest
caption luminance changes from 93 / 130 / 93 to 31 / 84 / 31; the reference is
31 / 31 / 31. Minimize and close now reach the reference foreground, while the
maximized-window restore and ordinary maximize rasterization remain separate
unproven gates. In particular, the ordinary maximize rectangle still has no fully
opaque reference-dark edge in this sample. Do not claim palette correction repairs
subpixel geometry or all caption pixels.

The fresh Codex reference remains a 526x601 conversation with collapsed sidebar,
not Appearance. Equal local caption ink bounds do not prove same-state page,
native high-DPI, hover/pressed/inactive or complete interface equivalence. The
separate native resize/clipping root cause, responsive surfaces, same-state
Appearance/PI-Desktop references and full product objective remain open.

#### Ordinary maximize raster correction

The palette-only native comparison leaves ordinary maximize at luminance 84.
Size=9 and half-pixel positioning alone still produce an 11x10 alpha>=64 ink
box in Skia, so neither a different threshold nor a larger allowed box is used
to mask the failure. An external diagnostic renderer records the left edge
split across alpha 103/152 while the opposite edge is alpha 255. Direct geometry
replay isolates the round line cap: flat caps remove that extra column while
changing the join alone does not. These diagnostics are outside the repository.

The shared ordinary-maximize variant now uses Size=9, translation (+0.5,-0.5)
and flat caps. Minimize, close and alternate restore keep their existing geometry
and caps. AppIconButton exposes its existing alternate state as a pseudo-class,
retaining the same button and icon instances. No generic icon renderer changes.
Odd/even-width light/dark layout cases retain 46x36 hit areas and centered icons.
Two raster cases require the unchanged 10x10 ink box, opaque middle pixels on
all four edges and a transparent center, then exercise maximize/restore sizing.
All 18 caption-palette pixel cases and 53 focused App/window cases pass.

The newer candidate App SHA256 is
`4200603DCE4B46855B346BECE3B44FE75CDF0980410D44C6E4CFC56142DB6386`.
`native-raster` uses a fresh independent data root, not the already-closed
palette fixture. Settled passive Python frames cover light normal/max conversation,
light/dark normal/max Appearance and restoration to light. Actual native maximize,
restore and minimize/reactivation are verified at 96 DPI; IsIconic changes true
then false on the same owned HWND while the process remains alive. Open-data
audits retain all nineteen tables, original artifacts and four-request history,
and restore the exact initial Light preference bytes.

`caption-raster-native-comparison.png` preserves unscaled ordinary-window crops.
The reference, palette-only and corrected candidates all measure 10x1 / 10x10 /
10x10 at the existing native threshold. Corrected light caption luminance is now
31/31/31, compared with palette-only 31/84/31 and reference 31/31/31. This proves
the sampled ink extents and peak contrast, not equality of every caption pixel,
restore glyph, dark reference, interaction state or complete page. A new
foreground-only passive Codex capture is correctly refused while another app is
in front. Passive PrintWindow capture subsequently returns a verified non-black
526x601 conversation at 96 DPI from the same installed 26.1007.2314.0 version;
the comparison uses this fresh reference. It is not the requested Appearance
page. No Codex UI input is issued.

The corrected App and observer both exit zero after the actual close action.
The launcher also disappears and port 51792 is released. A further post-exit
audit passes the same table/artifact/request/UI checks. Neither fixture is reused
after closure. No real data, approval, AOT, JSON adapter or packaging changes occur.

The first `raster-final-gates` driver passes formatting/build but exits before
tests because Windows PowerShell cannot resolve Get-FileHash in that inherited
environment. It is terminal failed history. `raster-final-gates-v2` runs under
the verified PowerShell 7 host, records the candidate assembly identity, and
passes formatting, a zero-warning/error solution build, all 66 pixel cases and
seven architecture constraints. Its complete App gate finishes with all 4,271
cases passing, zero failures/skips/aborts, and driver exit zero. Test/pixel DLL
SHA256 values are respectively
`DB78C9AD31CA1ECE9CBC17B639E913AC5F0F180001ACD56931726545D25736D2` and
`E36C9085451CD5F460AD172AC76BDA123D334CC2A5222D82FAE680332D8B3AE8`.

`verified-checkpoint.json` independently parses terminal TRX counters and verifies
current App/test/pixel hashes. The preceding complete six non-UI groups total
1,538 passes (119 Agent, 491 Tools, 516 Providers, 199 Infrastructure, 76 Browser,
137 Platform.Windows); all six test DLL hashes remain unchanged after the final
solution build. Together with the 4,271 App and 66 pixel cases this establishes
a current eight-assembly 5,875-case regression checkpoint, not complete product
acceptance. The seven architecture cases are a subset, not additional totals.
Native post-exit data audits and process/port cleanup also pass. No commit,
push or release occurs. Same-state Appearance/PI-Desktop comparison, original
native clipping/high-DPI investigation, broader narrow-window adaptation and
the remaining product state/function matrix stay open.
<!-- /source: WINDOW_CAPTION_CONTRAST_ACCEPTANCE.md -->


---

<a id="window-caption-fidelity-acceptance"></a>

## WINDOW_CAPTION_FIDELITY_ACCEPTANCE.md

<!-- source: WINDOW_CAPTION_FIDELITY_ACCEPTANCE.md -->
### Window caption fidelity and native evidence (2026-10-13)

This is a bounded caption-style correction and an evidence audit, not complete
Codex visual equivalence. The full-product objective remains active.

#### Design and safety self-check

1. App owns caption presentation. Existing shared `AppIconButton` and style
   variants remain authoritative; no module dependency or native port is added.
2. Existing minimize, maximize/restore and close commands are unchanged. Only
   close-icon size and the existing shared close-hover color are corrected.
3. Authorization, approval, cancellation, persistence and output limits are
   unchanged. Native replay does not send messages or enable schedules. Python
   performs passive capture and read-only metadata checks, without activating,
   moving, resizing or sending input to either reference or target windows.
4. App.axaml remains 496 physical lines, WindowVariants.axaml 58 and the new
   single-type regression file 74. No per-view appearance override is added.
5. Four headless cases cover both themes at 980x640 and 1280x820, real headless
   pointer enter/leave, white hover glyph, Arrow cursor and unchanged hit areas.
   Native pixels remain a separate acceptance gate.

#### Reference and correction

The preceding native comparison uses equal, unscaled 1920x1032 images in
`%TEMP%\MossAgent-window-state-20261013-080834`. Its
`native-region-comparison.json` records matching sampled titlebar/navigation-rail,
sidebar and workspace colors. Minimize ink is 10x1 and restore ink 10x10 in both
images. Close ink is 10x10 in Codex but 8x8 in that MossAgent binary. Different
history-button states and conversation contents do not establish pixel equality.

The passive Codex hover sample in
`%TEMP%\MossAgent-caption-fidelity-20261013-085556\codex-passive-current.png`
contains the close-hover background RGB 232,17,35 (`#E81123`).

- WindowVariants gives only the window close icon Size=10; the other caption
  icons retain Size=8 and StrokeThickness=1. Button hit areas remain 46x36.
- The existing App.axaml close-hover rule now uses `#E81123`, replacing
  `#C42B1B`. White glyph and Arrow cursor behavior are preserved.
- An attempted additional rule in WindowVariants was overridden by the later
  existing shared rule. That ineffective rule is removed; include ordering and
  unrelated styles are unchanged.

#### Deterministic verification

- `baseline/caption-baseline.trx`: all four cases reproduce Size=8 instead of 10.
- `hover-baseline/caption-hover-baseline.trx`: all four reproduce the old hover
  color. The intermediate `focused-final/` run retains these four failures and
  is not a successful final result despite its directory name.
- `green/caption-green.trx`: final caption/window group passes 44/44.
- Final solution build: zero warnings and errors. Completed `full/*.trx` files
  pass 5,655/5,655 across eight assemblies, with no failed or skipped cases:
  App 4,105; visual 12; Agent 119; Tools 491; Providers 516; Infrastructure 199;
  Browser 76; Platform.Windows 137.

Logs and TRX files are in the external caption-fidelity directory above. No
commit, push, release, AOT, JSON or packaging change is part of this slice.

#### Earlier native window-state sweep

The preceding production-App sweep belongs to assembly SHA256
`319A951FBB3562556F23F378B0EB6648059445D29B914560D107217D0E5D4FEC`,
not the caption-corrected assembly. Ordinary-permission, DPI-96 captures and
read-only bounds records in the window-state directory verify:

- Light/dark 980x640 and dark 1280x820 restoration; maximized 1920x1032 inside
  the monitor work area. Cold reopen retains 1280x820 normal bounds while maximized.
- Dark inspector and automation/history surfaces at narrow and maximized sizes;
  completed history opens without a model request. Minimize retains 980x640
  normal bounds and restoring returns to 980x640.
- Persisted replay retains the paused plan, four terminal runs, six tasks,
  original 15 tool audits and three hash-verified artifacts. Recorded requests
  remain the four historical whitelisted loopback requests.
- The earlier PID 3156 disappears after the actual caption close action. Its
  exit code was not observed; this is not evidence of exit code zero.

#### Caption-build checkpoint: native gate remained open

The production Debug/JIT assembly SHA256 at this checkpoint is
`38B1E1992B6280581F6C7634A69DFC1AAC1FCC17436E6B93C66A3733601A2074`.
PID 7884 starts with ordinary privileges and the isolated window-state data.
Cold-start and after-observation audits pass all checks, including no descendant
processes, unchanged tasks/audits/artifacts and no additional model request.
That read-only restoration is not a new automation execution.

Native capture cannot yet validate the corrected pixels:

- Sky returns MossAgent metadata and its accessibility tree, but the displayed
  screenshot shows Codex. A fresh list/get-window retry reproduces the mismatch.
  No coordinate or element input is issued after the mismatch is identified.
- Passive PrintWindow reports 1920x1020 bounds, but the non-black content covers
  only 1536x816; the remaining right and bottom strips are black. The independent
  read-only bounds sample reports a 1920x1032 work area and 1536x816 normal bounds.
  These observations do not establish whether capture, native DPI or startup
  window state owns the discrepancy.
- Explicit screenshot-process/thread DPI initialization does not remove the
  padding. The external passive script now rejects such frames instead of
  accepting them solely because luminance variance is nonzero. The rejected
  sample writes diagnostic JSON with `valid=false`, not an accepted PNG.
- The optional `--method screen` requires the selected window already be in
  foreground and checks ownership/bounds again afterwards; it performs no input
  or activation. A manual foreground handoff is requested for further diagnosis.
  The non-foreground negative case rejects capture without saving a PNG; a fresh
  passive Codex PrintWindow sample passes full 1920x1032 content-extent validation.

`caption-final-readonly.automation-audit.json` in the window-state directory
passes all restoration checks again after this capture diagnosis. The QA App is
still running for the manual foreground handoff at this checkpoint; current-build
normal close/exit and observer shutdown have not been verified. The loopback
observer has its existing 20-minute lifetime bound, not a recurring automation.

No resized/cropped padded image is used to disguise native dimension inequality.
Current native close-hover pixels, minimize/restore, both-theme size matrix,
inspector/context-card interaction, history navigation and normal shutdown still
need reliable current-version observation. Headless success and the earlier
version's native matrix must not be promoted to current-version visual sign-off.

The later sidebar-glyph correction and fresh isolated production replay are in
[WINDOW_SIDEBAR_ICON_ACCEPTANCE.md](UI_WORKFLOWS.md#window-sidebar-icon-acceptance). That separately identified build obtains valid
native frames, actual hover/inspector/minimize/history behavior and normal exit.
Both checkpoint QA instances and observers are then cleaned up with preserved
data audits. The failed frames above remain failures, not retroactive successes.
<!-- /source: WINDOW_CAPTION_FIDELITY_ACCEPTANCE.md -->


---

<a id="window-caption-focus-acceptance"></a>

## WINDOW_CAPTION_FOCUS_ACCEPTANCE.md

<!-- source: WINDOW_CAPTION_FOCUS_ACCEPTANCE.md -->
### Window caption focus acceptance

#### Reproduction and repair

Native production App testing at ordinary privileges, DPI 96, first verifies
1280x820 and 980x640 workspace layout, the light/dark appearance page, model
expansion, model editor scrolling and maximized/minimum-size transitions.
The earlier content-clipping problem is not reproduced in this replay; no
repair or root cause for that earlier instance is claimed.

A different real failure is reproduced: after clicking maximize and restore
while the model editor is open, focus belongs to MaximizeButton. Escape leaves
the model editor open. AppSettingsDialog handles unhandled Escape only on its
own routed input path; the sibling caption button takes input outside that path.

The shared WindowControlButtonTheme now sets Focusable=False. All three caption
actions keep native-titlebar semantics: pointer actions work without taking an
editor's keyboard focus, and they do not become Tab stops. Their accessible
labels, commands, CanExecute checks, sizes, cursor, icons and hover rules remain
unchanged. Dialog Escape/child handling is not replaced by a window-global
shortcut, and no focus trap, service locator or new subscription is introduced.

#### Design self-check

1. App's existing shared caption theme owns the behavior; dependency direction
   remains unchanged.
2. Existing AppIconButton commands and AppSettingsDialog routing are sufficient;
   no new port or per-view workaround is required.
3. No provider, tool, authorization, approval, cancellation, timeout or output
   policy changes. The native sweep uses an independent database backup and
   never submits a message, starts a browser/terminal or changes approval policy.
4. The shared resource dictionary remains below 300 lines. The new 107-line test
   file has one type; methods/lambdas remain below the hard size limit.
5. Successful pointer commands, disabled commands, field focus/draft/selection,
   Escape restoration and production caption keyboard-stop rejection are tested.

#### Test and build evidence

Evidence is under
`%TEMP%\MossAgent-native-resize-20261013-115953`.

- The new sixteen-case baseline passes six disabled-command preservation cases
  and fails ten: six enabled pointer-focus cases and four actual MainWindow
  keyboard-stop cases. This is an observed before/after regression, not a
  test-fixture scheduling error.
- After repair the related caption/icon/dialog/focus/scroll group passes 54/54,
  including all sixteen new cases and existing handled-Escape/disabled-command
  checks. The complete serial App suite passes 4,174/4,174 without failures/skips.
- The final eight-assembly regression passes 5,736/5,736 without failures/skips:
  App 4,174, visual 24, Agent 119, Browser 76, Infrastructure 199,
  Platform.Windows 137, Providers 516 and Tools 491. All eight final TRX files are
  retained separately from the failing baseline.
- Solution build passes with zero warnings/errors. Scoped whitespace formatting,
  shared-theme XML, one-type/file-size and diff whitespace checks pass. The final
  App assembly still matches the repaired native replay hash.

#### Native evidence ownership

Baseline App SHA256:
`D384764AC1C1A3AC60011EC51099249DE3B6B2FDBB6412F14541E523BD0E539B`.
Repaired App SHA256:
`7CFC5F00FBF9A30413BF670D940DAC80FB09486931A610BE0292C6DFBFDCCF6B`.
The repaired App uses ignored `.scratch/caption-focus-20261013` Debug/JIT output;
default output is not silently replaced. Repaired frames/audits are in `fixed/`.

- Both light and dark native model editors open at 980x640, maximize to
  1920x1032 and restore to 980x640 without disappearing or clipping the footer.
  Fresh UIA observations confirm Model ID retains focus; Escape now closes the
  editor. The same model/catalog expansion remains available afterwards.
- The actual repaired close caption button exits the App zero. Both baseline
  and repaired observers exit zero, their original App/observer instances are
  gone, and port 51792 has no listener. Repaired UI preference returns to Light.
- Read-only audits before/after closing prove all nineteen database table schemas
  and rows match the source backup: six tasks, 26 messages, four terminal schedule
  runs, catalogs, saved configuration and audits are unchanged. Three original
  artifacts pass hashes. Only four historical model requests exist; the log hash
  is unchanged and no App descendant process is observed.
- Passive same-state baseline/repaired 1920x1032 cold light frames are exactly
  pixel-identical. This proves unchanged appearance for that one fixture state,
  not full state/DPI equivalence. `caption-focus-before-after.png` places two
  unscaled 980x640 dark frames side by side: baseline Escape leaves the modal
  open; repaired Escape closes it. Python scripts stay outside the repository.

#### Codex comparison and remaining gates

Fresh passive Codex and repaired MossAgent frames are both 1920x1032, DPI 96 and
ordinary privilege. Sampled titlebar, sidebar, main and selected-row colors match.
The rail sample at (40,400) differs by one RGB unit: Codex (237,243,248) versus
MossAgent (238,244,249). This may involve the adjacent frame shadow; no flat
background correction is applied from a single sample without verifying the
surrounding gradient. Comparison points and actual results are retained in
`caption-comparison.json`; no full-window pixel-equivalence claim is made.

Codex remains on its conversation page rather than a matching settings state.
The current native check does not cover all seven repaired settings pages,
all inspector tabs, higher DPI or PI-Desktop equivalence. Previous evidence
belongs to its recorded version; it is not promoted to this repaired assembly.
No AOT, JSON, packaging, commit, push or release change occurs. The full product
objective and complete visual/control/reference acceptance remain active.
<!-- /source: WINDOW_CAPTION_FOCUS_ACCEPTANCE.md -->


---

<a id="window-frame-acceptance"></a>

## WINDOW_FRAME_ACCEPTANCE.md

<!-- source: WINDOW_FRAME_ACCEPTANCE.md -->
### 共享工作区外框验收

#### 2026-10-06 实现与约束

- 生产 App/MainWindow 的侧边栏、对话和检查器统一置于现有 `AppBorder`，
  使用 `WindowVariants.axaml` 的 `window-content-frame`，不在页面定义局部外观。
  外框统一 12-DIP 圆角、右侧和底部 4-DIP 留白、内容裁剪及 SharedSurfaceShadow。
- 导航轨、标题栏、原生浏览器常驻宿主、审批遮罩和窗口缩放命中区留在外框之外，
  不把原生缩放交互裁掉，也不重新创建浏览器会话。
- 修复侧边栏的 DataContext 边界：目录对象不拥有 IsLeftPanelOpen，展开状态显式
  绑定窗口 ViewModel。原生点击修复前仍显示侧栏，修复后释放 264-DIP 侧栏空间。
- 行为和样式由 App 所有，不改变跨模块依赖、工具管线、授权、审批、取消、输出限制
  或持久化结构；继续使用既有控件和窗口命令，不新增运行时端口。
- 外框布局 6 项及标记 1 项新增回归；更新现有壳布局测试以表达外框内外两层网格，
  不削弱原生命中区或功能绑定断言。新增 C# 均为一个类型，文件和方法未超过硬限制。

#### 自动验证

- 外框及相邻组件专项 114 项通过，壳布局专项 13 项通过。
- 最终完整解决方案 1,644 项通过；解决方案构建 0 警告、0 错误。
- 本轮相关 C# 空白格式及差异空白检查通过。测试覆盖 980×640、1280×820、
  1920×1032，明暗主题、收起侧栏和展开检查器，断言外框实际边界和内容空间。
- 现有原生组件禁用、实例外观禁用、共享阴影及浏览器承载回归保持通过。
- 全仓库审计 827 个 C#/AXAML 文件：文件体积和单类型约束无失败。
  另发现两个既有测试方法各 86 行，超过 80 行限制：ModelSettingsViewModelTests:154
  和 SubagentWorkspaceFlowTests:21；均不在本轮外框修改范围，未改动，不声称全仓库方法约束通过。
  后续流式安全验收已拆分这两处测试并保留全部断言；最新全仓库方法审计无失败，
  见 PROVIDER_STREAM_LIMIT_ACCEPTANCE.md。

#### 原生截图与交互

- 仓库外普通权限验收宿主加载生产 App/MainWindow，使用独立临时数据库、
  项目和 loopback 合成模型服务。未修改真实项目、凭据或生产配置。
- Python 只被动截取实际窗口像素，无拉伸。当前 Codex 参考的 1936×1048 原生窗口
  只裁去四周各 8 像素的外框，得到 1920×1032 有效内容；不操作 Codex 原生界面。
- 对照右上、右下、左下角落：修改前工作区铺到窗口边缘，修改后具备完整圆角和
  右侧、底部留白。浅色标题栏/导航轨采样为 #EEF4F9，侧边栏为 #F9FBFD。
- 实际点击验证侧栏收起/展开、工具结果展开、检查器收起及项目上下文卡片恢复。
  普通窗口还原至 1280×820，深色恢复后最大化并收起侧栏，布局保持正确。
- 合成成功场景经过真实输入发送，完成一次 filesystem.read_file 和最终回复。
  临时数据库确认 Completed、4 条消息和唯一成功 chosen-read 审计；合成文件未变。
  成功宿主最终因其 7 分钟验收期限结束，日志保留 TaskCanceledException；
  这不作为正常关闭证据，也不归因于生产 UI。原生视图菜单输入时宿主已退出，
  重新枚举确认窗口不存在后停止输入，没有使用失效索引重复点击。
- 同一数据集以 --reopen 恢复已完成会话，模型请求为零，正常关闭日志无失败标记、
  退出码为零；没有再次发送或自动重放。兼容接口详情见 COMPATIBLE_STREAM_ACCEPTANCE.md。

图片、对照脚本、日志和宿主保留在：
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-window-fidelity-20261006-1125`。

- `window-frame-corner-comparison.png`：Codex、修改前、修改后原始像素裁片。
- `sidebar-collapse-before-fix.png`、`sidebar-collapse-fixed.png`：绑定问题修复对照。
- `native-tool-expanded.png`、`native-context-restored.png`：工具与检查器交互。
- `native-normal-light.png`、`native-reopened-normal-dark.png`：1280×820 明暗主题。
- `native-reopened-maximized-dark.png`、`native-reopened-dark-collapsed.png`：深色最大化。

#### 未完成范围

参考与验收窗口的正文和检查器内容不同，只核对公共外框及指定交互，不声称整页像素相同。
所有 DPI、窗口边缘拖动、悬停状态、设置页及全部图标仍未穷尽；新对话图标等几何仍有差异。
本轮不修复 Codex 截图工具，不将封装、构建通过或局部圆角修正等同于全产品目标完成。
没有暂存、提交或推送；截图和 Python 脚本不进入仓库。
<!-- /source: WINDOW_FRAME_ACCEPTANCE.md -->


---

<a id="window-frame-shadow-acceptance"></a>

## WINDOW_FRAME_SHADOW_ACCEPTANCE.md

<!-- source: WINDOW_FRAME_SHADOW_ACCEPTANCE.md -->
### Window frame shadow progress and native resize failure

#### Root cause and shared repair

Equal-size light native screenshots show a spatial shadow gradient beside the
Codex workspace, not a different flat navigation-rail brush. MossAgent's frame
had ClipToBounds=True, clipping its own outer shadow. Its ancestor AppPageSwitcher
also inherits UserControl's clipping, removing the shadow above the workspace.

WindowVariants now separates the outer shadow frame from an inner rounded
content clip. Both retain the same 12-DIP radius; frame dimensions, margins,
sidebar width, commands and input regions are unchanged. Only the outer window
page switcher stops clipping. Nested page switchers retain their defaults.
The inner clip prevents oversized/transformed children escaping any corner.

SharedWindowFrameShadow owns the measured window-only variant: zero-offset
28-DIP blur at alpha 12, plus a zero-offset one-DIP edge at alpha 4. Existing
SharedSurfaceShadow and all card/dropdown shadows remain unchanged. The build
guard permits the new token only on the exact shared window-frame selector;
other selectors, arbitrary shadows and unknown tokens remain rejected.

#### Design self-check

1. App's existing shared styles own this rendering change; module dependencies
   remain unchanged.
2. Existing AppBorder, AppPageSwitcher and shared resources suffice; no new
   service, global state, native hook or execution port is introduced.
3. No tool, path authorization, approval, timeout, cancellation or output policy
   changes. Native QA uses an independent database backup and sends no request.
4. Changed AXAML remains under its hard limits; both changed test files contain
   one type and stay below 250 lines. No lifecycle subscription is added.
5. Pixel tests cover light/dark, three window sizes and 1/1.25/2 render scaling,
   shadow reach, unchanged distant rail color, upper shadow and rounded overflow
   rejection. Resource/build-guard tests preserve card tokens and reject misuse.

#### Verification and version ownership

Evidence root: `%TEMP%\MossAgent-frame-shadow-20261013-123343`.
Final production App SHA256:
`2265AB5B949EE62B775FCE7D4E03D0809ABE9076E97F6A539E5C755B0748BCD1`.
Output remains ignored Debug/JIT `.scratch/caption-focus-20261013`; the default
output is not substituted. No AOT, JSON, publishing, commit or push changes.

- The initial fourteen-case baseline fails ten shadow-rendering cases and four
  separation-contract cases. Intermediate clipping-only/calibration failures,
  diagnostic measurements and the failed locked-app build are retained, not
  reclassified as successful product verification.
- Final relevant App tests pass 120/120; the entire pixel assembly passes 38/38,
  including all fourteen new rendering cases. Six new resource/guard cases are
  included in the App group. Final solution build has zero warnings/errors;
  scoped whitespace formatting and markup/diff checks pass.
- Fresh passive Codex and final MossAgent light frames are 1920x1032, DPI 96,
  ordinary privileges. At 60 rail-shadow pixels across three rows, mean absolute
  RGB error drops from 2.2833 to 0.3333 and maximum error from 11 to 1. The earlier
  (40,400) difference is corrected without recoloring the distant rail. Upper
  titlebar-shadow samples still differ by up to two RGB units. Unscaled crops,
  coordinates and metrics are retained; whole-window equivalence is not claimed.
- All nineteen SQLite table schemas and rows remain identical to the original
  backup; three original artifact hashes and the four-request log are unchanged.
  No model request, browser/terminal process or approval-setting action occurs.
  The final fixture returns to Light and exits normally with Alt+F4, App/observer
  both exit zero, their original processes are absent and port 51792 is released.

#### Native resize acceptance failed

On the final production binary, clicking restore changes the native window from
1920x1032 to 1280x820, but the content retains its maximized arrangement: caption
buttons, composer and right context card are outside the visible client. Both
passive PrintWindow and foreground screen captures independently confirm this.
The capture metadata's valid flag does not establish successful layout.

The visible View menu remains interactive and switches to Dark and back to Light;
the clipping persists, so this is not merely a frozen screenshot or an unresponsive
process. A post-restore Computer Use geometry error is not treated as evidence
that restore did not execute, and the caption is not blindly clicked again.

This reproduces the earlier open native resize defect but does not establish its
root cause or a repair. Passing Headless tests are not promoted to native resize
acceptance. Narrow/native dark-layout acceptance, complete settings/inspector
states, higher native DPI, equivalent PI-Desktop references and the complete
product objective remain open. The next investigation must distinguish native
client resize delivery, layout invalidation/constraints and rendered-root size;
no speculative forced-width or restart workaround is shipped.

The subsequent controlled replays and external screenshot-anchor guard are
recorded in [NATIVE_RESIZE_DIAGNOSTIC_PROGRESS.md](UI_WORKFLOWS.md#native-resize-diagnostic-progress). The original production
binary reproduces the failure again, while other unmodified/instrumented
replays succeed. These observations narrow the investigation but neither
establish a root cause nor repair the native resize acceptance failure.
<!-- /source: WINDOW_FRAME_SHADOW_ACCEPTANCE.md -->


---

<a id="window-sidebar-icon-acceptance"></a>

## WINDOW_SIDEBAR_ICON_ACCEPTANCE.md

<!-- source: WINDOW_SIDEBAR_ICON_ACCEPTANCE.md -->
### Window sidebar glyph and native replay (2026-10-13)

This corrects a measured titlebar glyph mismatch and supplies fresh production-App
evidence. It does not establish whole-window Codex equivalence or product completion.

#### Design and safety self-check

1. App owns the shared geometry and semantic size variant. Dependencies and
   `AppIconButton` composition remain unchanged; no native implementation is added.
2. The existing left-panel command, accessible label, tooltip and hit area are
   preserved. UiIconPanel has only the titlebar binding as a production consumer.
3. Authorization, approval, models, persistence schemas and execution limits do
   not change. Native replay uses a read-only SQLite backup in a separate data
   directory, a paused plan and a bounded loopback observer. No message, new run,
   plan enabling, permission change, paid request or security control is invoked.
4. App.axaml remains 496 lines and WindowVariants.axaml 58. AppIconButtonTests is
   217 lines; the new single-type pixel test is 102. Methods remain below 80 lines.
5. Twelve Skia cases exercise both themes, 980x640/1280x820 and bitmap scales
   1/1.25/2, plus unchanged hit geometry. Real native capture, pointer movement,
   navigation, window state, data integrity and shutdown are verified separately.

#### Measured mismatch and correction

The earlier native 1920x1032 comparison records a 14x12 MossAgent sidebar-toggle
ink extent against Codex's 12x12. AppIcon intentionally preserves geometry aspect
ratio and applies an unscaled stroke. UiIconPanel's wide rectangular path therefore
cannot become the reference square contour merely by changing the button's hit area.

- UiIconPanel now uses a rounded square contour and its internal left divider.
- The titlebar-panel shared icon Size changes from 12 to 10, retaining stroke 1.
- The existing 32x30 button hit area, centered glyph and toggle command remain
  unchanged. No other icon renderer, resource consumer or per-view style changes.

The final native comparison uses unscaled, equally sized 1920x1032 passive frames:

| Glyph | Codex ink | Current native ink | Position |
| --- | --- | --- | --- |
| Sidebar toggle | 12x12 | 12x12 | Measured ROI bounds identical |
| Minimize | 10x1 | 10x1 | Measured ROI bounds identical |
| Restore | 10x10 | 10x10 | Measured ROI bounds identical |
| Close | 10x10 | 10x10 | Measured ROI bounds identical |

Sampled titlebar/rail RGB 238,244,249, sidebar RGB 249,251,253 and workspace white
also match. Native close-hover RGB is 232,17,35 (`#E81123`) with a white glyph.
Dark normal close-hover capture records the native Arrow cursor, not Hand.
Matching thresholded extents and sampled colors do not imply complete pixel masks,
typography, other states, dark Codex pixels or whole-window equality.

#### Deterministic verification and harness corrections

Evidence root: `%TEMP%\MossAgent-sidebar-icon-fidelity-20261013-0933`.

- The first baseline incorrectly assumes a 28-pixel button height and fails 12
  cases. This is a harness mistake, not twelve reproduced product regressions.
  After preserving the existing 30-pixel hit height, `baseline-corrected/` fails
  all twelve cases on the actual nonsquare ink extent, with width/height differing
  by two pixels at the selected threshold.
- The corrected glyph passes all twelve rendered cases. The first mixed 41-case
  group still fails 24 cases: twenty source-root lookups cannot find the repository
  above the external output path; four caption cases encounter a mixed
  HeadlessPlatformTypeface/SkiaTypeface error. Those failures remain in `focused/`.
- Pixel tests are moved to the existing separate Skia-only App.Visual.Tests
  assembly, reusing SharedPixelTestApplication. Source-inspection outputs move
  beneath ignored repository `.scratch`, so their existing ancestor lookup works.
  No source-root assertion, font-cache implementation or production behavior is
  weakened to make the group pass.
- Final App caption/button/shell group passes 29/29. The complete pixel assembly
  passes 24/24, including the twelve new cases and all existing rendering cases.
- Final serial full regression passes 5,667/5,667 across eight completed TRX files:
  App 4,105; visual 24; Agent 119; Tools 491; Providers 516; Infrastructure 199;
  Browser 76; Platform.Windows 137. None fail or skip.
- Isolated solution build and scoped whitespace formatting verification pass.
  The already-running default-output QA App is not overwritten during testing.
  No commit, push, release, AOT, JSON or packaging change occurs.
- After all QA instances close, the default-output solution build also completes
  with zero warnings/errors. Its App SHA256 is
  `1D1FB84D717C9364B8CE95D8414C1445EF733E44744A44FC77C013D2A2558AAB`.
  That build has not been replayed natively at this checkpoint; the native
  evidence below belongs only to the separately identified isolated assembly.

#### Fresh native production-App replay

The executing isolated Debug/JIT App assembly SHA256 is
`E2A392571F6D94106E6F9C94523ACF04D1597B402FB119CEBCE26FE6B6B16A86`.
Its executable is under ignored `.scratch\sidebar-icon-fidelity-20261013-0933`;
the data backup, helper scripts, PNG/JSON evidence and loopback logs stay outside
repository source under the evidence root's `native/` directory.

- Startup, ordinary privileges and DPI 96 are checked. Cold maximized capture
  contains the full 1920x1032 frame, with 1280x820 normal bounds and no black padding.
- A fresh exact executable/window selection followed by explicit activation
  produces the correct MossAgent pixels. Activation restores the maximized window
  to its 1280x820 normal bounds; this side effect is measured, not hidden by scaling.
  Actual caption maximize/restore actions subsequently produce both native sizes.
- Light maximized close-hover is exercised with a zero-delta wheel pointer move,
  not a close click. Passive PNG verifies the measured red and white glyph.
- Actual inspector expansion hides the floating project-context card. Deep-theme
  switching through the View menu updates the open inspector; collapsing it shows
  the context card again. No browser/tool tab or authorization prompt is activated.
- In dark 1280x820, the actual minimize button makes IsIconic true while preserving
  1280x820 normal bounds. The expected minimized-state observation error is followed
  by fresh selection/activation; restoration returns to the recorded normal size.
- The paused automation list displays four saved terminal histories. Opening the
  newest Completed row shows its persisted synthetic assistant reply, project B,
  saved ReadOnly policy, model B and high reasoning without invoking that model.
  Accessibility occasionally lags the pixels after navigation; stale indexes are
  not used for the history action or as proof of its outcome.
- Before/after read-only audits retain one paused plan, four independent terminal
  runs, six tasks, original fifteen audits, three hash-verified artifacts and only
  four historical whitelisted requests. App descendants are empty.
- After restoring light theme, the actual caption close exits the App with code
  zero, recorded by the live launch-monitor process. The observer is signalled to
  stop and its process/listening socket disappear; no forced termination is used.
- The earlier caption-build QA instance is freshly selected and activated before
  closing its observed real close button. Its PID disappears and its original
  data audit still passes. Its exit code is not observed. All four QA/observer
  PIDs and the observer socket are absent at final cleanup.

The earlier incorrect-target and padded captures in
[WINDOW_CAPTION_FIDELITY_ACCEPTANCE.md](UI_WORKFLOWS.md#window-caption-fidelity-acceptance) remain historical failures. Correct fresh
selection/capture and invalid-frame rejection make this replay possible; they do
not independently establish the cause or a fix of the Windows helper's earlier
wrong-pixel/DPI behavior. Codex is only passively captured, never automatically
operated. No historical automation run is promoted to a new execution.

#### Remaining gates

Native 980x640 and higher-DPI replay of this build, complete surface/state/visible-
control audit, equivalent dark/current Codex and PI-Desktop references, and full
window/font/shadow equivalence remain open. Keep the complete product goal active;
do not promote these bounded titlebar and restored-history checks to all surfaces.

The later default-output responsive replay and shared danger-palette repair are
recorded in [DANGER_BUTTON_THEME_ACCEPTANCE.md](UI_COMPONENTS.md#danger-button-theme-acceptance). Its before default assembly adds
native 980x640 light inspector and dark dialog-reopen evidence; the separately
identified repaired assembly adds native light/dark provider and safe hover
evidence. Transient higher-DPI captures and a rejected black frame remain bounded
observations, not a completed high-DPI sweep or proof of a capture-helper fix.
<!-- /source: WINDOW_SIDEBAR_ICON_ACCEPTANCE.md -->


---

<a id="workspace-navigation-acceptance"></a>

## WORKSPACE_NAVIGATION_ACCEPTANCE.md

<!-- source: WORKSPACE_NAVIGATION_ACCEPTANCE.md -->
### 工作区页面导航与检查器空状态验收

#### 范围与设计自检

- 本轮使用真实 `MossAgent.App.exe`、独立 `MOSSAGENT_DATA_DIR` 和合成数据，
  不读取日常数据库或复制凭据。Python 截图脚本与种子程序保留在仓库外。
- 行为归属 App：目录导航发出明确的对话导航意图，页面协调器决定展示对话
  或定时任务。后台目录刷新、标题更新不是用户导航，不应抢走当前页面。
- 复用现有目录命令、仓库端口和历史记录；不引入数据库迁移、文件操作、
  provider 请求或工具执行旁路。已有锁定状态和未知项目拒绝逻辑保持有效。
- 将历史命令提取为独立 `WorkspaceNavigationController`，防止目录模型突破
  500 行硬限制。每个手写 C# 文件一个类型，订阅随页面协调器释放。
- 共享 `AppEmptyState` 新增 `compact` 外观变体，用于 120 DIP 高的产物区域；
  不在页面中覆盖尺寸、字体或颜色。

#### 实际发现

- 在定时任务页点击侧栏里当前已选中的对话，页面不会返回对话。
- 侧栏“新对话”、切换到没有任务的项目、后退到空草稿也可能停留在定时任务页。
- 后台对话标题更新或目录重新加载反而可能把定时任务页关闭。
- 原生右侧审计面板中，“暂无生成产物”的说明被固定高度区域裁切。
- 修复前新增回归用例 21 项中 11 项失败，覆盖真实侧栏共享按钮、后台更新、
  无任务导航以及窄窗口内的文字几何；其余拒绝/释放边界用例通过。

#### 验证方式

- `WorkspacePageNavigationTests` 覆盖实际 MainWindow 绑定、重复选中、空草稿、
  项目导航、后退、后台刷新、锁定、无效目标与释放订阅。
- `WorkspaceNavigationControllerTests` 覆盖前进后退、可用性通知、锁定后直接
  执行拒绝、历史不被消耗和已移除任务的剪枝。
- `InspectorArtifactEmptyLayoutTests` 在 980×640、1280×820、1920×1032 的
  明暗主题下逐个检查文字的实际边界，而非仅检查控件的 IsVisible。
- 所有交互用例断言没有向本地合成 provider 发出模型请求。

#### 证据与限制

本轮外部证据目录：`%TEMP%\MossAgent-workspace-review-20261009-01`。
普通权限的真实产品程序已复测：定时任务返回当前对话、侧栏新对话、审计
面板、窗口还原、实际拖动到 980×640、菜单切换深色和最大化。截图包含
1920×1032 的明暗主题、1280×820 浅色以及 980×640 的明暗主题。右侧产物
说明均完整显示。交互记录保留在 `native-interactions.json`，两个本轮实例
均正常关闭，stderr 为空。没有执行定时计划或发送模型请求。

对比图：`artifact-empty-before-after.png` 使用原始像素裁切显示本轮修复；
`codex-mossagent-current-layout.png` 为当前两程序的等尺寸全页布局对照。
当前 Codex 使用被动 PrintWindow 获取参考图；没有自动化操作 Codex 界面。
Codex 参考图与 MossAgent 的会话内容和已打开面板不同，不能据此宣称整页
逐像素一致。定时任务页面与编辑弹窗仍需要对应的当前 Codex 参考截图。
本轮不是付费 provider、长时间任务或完整产品目标的最终验收。

#### 检查记录

- 定向回归 73/73 通过；解决方案构建 0 警告、0 错误。
- 六个相关 C# 文件的格式检查、九个源码/AXAML 文件的 Roslyn/XML 拆分检查
  和工作区 `git diff --check` 均通过；没有暂存、提交或推送。
- 首轮全量 3684 项中 3683 项通过，一项旧预览交互测试在
  `HeadlessUnitTestSession.DisposeAsync` 抛出 NullReferenceException。
  原日志和 TRX 保留在 `full`；预览交互类单独复跑 6/6 通过，没有修改该测试。
  全量再次运行的结果单独保留在 `full-retry`，不覆盖首次失败证据。
- 全量第二次运行 3684/3684 通过，7 个 TRX，零失败、零跳过；App 2801、
  Windows 129、Providers 283、Tools 188、Browser 76、Infrastructure 144、
  Agent 63。汇总保留在 `full-retry-summary.csv`。一次清理异常不被宣称已修复。
<!-- /source: WORKSPACE_NAVIGATION_ACCEPTANCE.md -->
