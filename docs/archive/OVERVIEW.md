# 历史盘点、交付与集成验收

> 历史归档：保留原报告当时的结论、实验、失败与限制。整理于 2026-10-21。
> 当前能力与未完成方案统一维护在[功能清单](../FEATURE_STATUS_AND_IMPLEMENTATION_PLAN.md)；
> 原文中的计划、主机状态、测试数字及“正在进行”等措辞均为历史检查点。
> 代码块和仓库相对产物路径保留原意；本机忽略的 `artifacts/` 不随克隆分发。

[文档入口](../README.md)

## 原报告索引

- [MossAgent 阶段交付清单与后续实施方案](#delivery-status-and-roadmap) — `DELIVERY_STATUS_AND_ROADMAP.md`
- [功能与工作流对齐清单](#functional-parity-matrix) — `FUNCTIONAL_PARITY_MATRIX.md`
- [Integrated production workflow acceptance](#integrated-workflow-acceptance) — `INTEGRATED_WORKFLOW_ACCEPTANCE.md`
- [Plugin and skill workflow plan](#plugin-workflow-plan) — `PLUGIN_WORKFLOW_PLAN.md`
- [Product acceptance matrix](#product-acceptance-matrix) — `PRODUCT_ACCEPTANCE_MATRIX.md`
- [Product interaction audit and complete regression checkpoint](#product-interaction-audit) — `PRODUCT_INTERACTION_AUDIT.md`

---

<a id="delivery-status-and-roadmap"></a>

## DELIVERY_STATUS_AND_ROADMAP.md

<!-- source: DELIVERY_STATUS_AND_ROADMAP.md -->
### MossAgent 阶段交付清单与后续实施方案

阶段收尾日期：2026-10-20，后续增量更新至 2026-10-21。分支：`feature/initial-mossagent`。

本次收尾保存此前尚未提交的产品代码、测试和文档，不推送、不发布。下文描述的是
当前仓库的累计能力，并非每项都在本次新增。提交前基线为 `991b796`；提交身份以
Git 历史为准。原始测试、诊断、数据库副本和冻结候选保留在被忽略的 `artifacts/`
或各专项文档注明的临时目录，不加入源码提交。

2026-10-21 后续提交范围为文件句柄安全、共享产物会话、项目授权撤销及项目/工作树
对象身份的累计增量，提交前基线为 `be767de7b6fd0c483adc1ab34a06265f472320a6`。
包含对应测试、schema 26–28 向前迁移和下列专项文档；历史检查点中的未提交状态
仅描述当时状态。当前验证范围与未关闭的安全缺口保留，提交本身不表示产品验收完成。

**这是阶段交付，整个产品目标尚未完成。命令能够访问任务授权根目录之外的文件，
是已确认且尚未修复的安全阻断。** 工作目录校验、审批和 Job Object 不能证明文件系统隔离。
另有功能缺失和当前版本原生实机验收缺口，不能用测试数量表示产品完成度。

后续增量：用户已选择在支持原生 MXC 的独立 Windows 环境验收命令隔离。本机只读复检
仍为 AppContainerDacl；用户已确认独立环境尚未就绪，先继续本地安全修复。
原子文本写入已增加目录句柄租约、相对提交和同句柄快照，当前证据、支持限制及剩余
竞态见 [WRITE_HANDLE_PATH_SAFETY_ACCEPTANCE.md](SECURITY.md#write-handle-path-safety-acceptance)。
文件读取的打开后句柄校验及其检查点证据见
[READ_HANDLE_PATH_SAFETY_ACCEPTANCE.md](SECURITY.md#read-handle-path-safety-acceptance)；这只推进
R02 的一部分，不关闭 R01，也不把下列历史全量结果视为该增量的验证。
2026-10-21 的普通删除、递归目录及撤销新建文件句柄化增量和剩余方案见
[DELETE_HANDLE_PATH_SAFETY_ACCEPTANCE.md](SECURITY.md#delete-handle-path-safety-acceptance)。
后续复制文件/目录树和显式创建目录已接入源树句柄预检、目标目录租约和原子文件提交，
反例及当前验证边界见 [COPY_HANDLE_PATH_SAFETY_ACCEPTANCE.md](SECURITY.md#copy-handle-path-safety-acceptance)。
同卷移动与重命名继续接入源对象和目标父目录句柄；跨卷明确拒绝，实际链路、
证据及未完成方案见 [MOVE_HANDLE_PATH_SAFETY_ACCEPTANCE.md](SECURITY.md#move-handle-path-safety-acceptance)。
任务产物读取及文件预览缓存的原生写入/清理边界继续推进，详见
[ARTIFACT_HANDLE_PATH_SAFETY_ACCEPTANCE.md](SECURITY.md#artifact-handle-path-safety-acceptance)；
编辑快照、预览和最终补丁的共享会话已继续实现：内容完成与调用方成功保留分离，
编辑提交失败会清理 before/after，复制后核对 hash；真实清理拒绝不阻断其他流释放。
链路、证据和下一步见 [OWNED_ARTIFACT_SESSION_ACCEPTANCE.md](SECURITY.md#owned-artifact-session-acceptance)。
浏览器截图和下载已继续接入共享产物会话；审批后才创建文件，下载有界暂存后复制、
hash，暂存清理与最终复核通过后才保留。实际 Chrome 的截图/文本及二进制下载、
并发、取消/超时和显式会话恢复见
[BROWSER_ARTIFACT_SESSION_ACCEPTANCE.md](BROWSER_MCP.md#browser-artifact-session-acceptance)。
超时时 SDK 缓存清理可能未获确认，错误保留该状态；网络/缓存配额和崩溃回收尚未完成。
Git diff/numstat 临时输入也已接入专用会话，输入文件和父目录持续固定，真实 Git 可读而
外部写入/替换被拒绝，进程结束和输出读取后按原句柄清理；注入失败不回退。
详见 [GIT_DIFF_INPUT_SESSION_ACCEPTANCE.md](GIT_WORKTREES.md#git-diff-input-session-acceptance)。
该检查点尚未包括任务工作树对象身份；后续 schema 28 增量见下文。Git 进程隔离和崩溃发布/回收仍需分别处理。
项目配置现已增加持久授权版本及条件保存；Agent、统一工具管线和长驻终端持有撤销
租约，保存授权变更会结束旧运行/审批并暂停 Queue，跨实例通过有界轮询发现。
保存返回本次事务提交的快照，防止跨窗口重读借用较高版本覆盖外部授权；终端已补
专用撤权提示。Debug/Release 完整构建零警告/错误，各 2,506 项分层相关回归通过
（五套完整基础测试 2,196 项 + 修正后 App 310 项），各 2,641 项输入前后无漂移。
此前首轮 4 项失败、Release 统计观察的 2 项失败及确定性修复证据完整保留；这不等于
全解决方案测试、原生窗口或真实提供商验收。
当前生产组合、真实 ConPTY、Headless 恢复及剩余方案见
[PROJECT_AUTHORIZATION_REVOCATION_ACCEPTANCE.md](SECURITY.md#project-authorization-revocation-acceptance)。
以上版本撤销检查点不构成同步撤销屏障或命令隔离完成。
后续 schema 27 已保存项目主根/附加根的卷和目录文件 ID，并登记每次明确授权的
grant ID。旧项目保持待确认；普通改名不能接受同路径替换对象；明确重新授权更新
版本并撤销旧运行。生产任务、工具和长驻终端的根目录链持续持有至所属清理结束；
项目授权快照原位刷新，保留当前任务、编辑名称和前台草稿。
链路、迁移/失败边界和当前验证见
[ROOT_OBJECT_AUTHORIZATION_ACCEPTANCE.md](SECURITY.md#root-object-authorization-acceptance)。
后续工作树身份增量见下文；严格排空、最终名称竞争、崩溃恢复及原生实机仍需分别验收。
根对象切片最终 R4 的 Debug/Release 完整构建均零警告/错误，各 713 项定向回归通过
（完整 Windows 422、App 授权/根对象/统计/终端精选 291），该检查点两配置的 2,670 项输入一致。
修复前 R2 的扩大回归为 Debug 2,912 通过、Release 2,911 通过及 1 项 PTY 撤权状态失败；
真实租约的受控反例复现后已修正分类，并修复额外纳入的 5 项旧取消测试的保存快照准备。
所有失败和定向修复记录保留；R4 不是又一次六套基础及 App 扩大回归，也不是原生/真实提供商验收。
后续 schema 28 已登记任务工作树自身的 task/project/grant/卷/文件 ID，恢复及统一管线
拒绝同路径替换，组合租约让 Agent/工具/长驻 PTY 持有工作树根链。旧工作树输入区显示
完整路径，空闲时可明确确认；确认不消费草稿、不续跑、不重放，也不能认领已登记对象的
替换目录。10 项持久化/迁移/取消专项和 8 项生产 App 专项通过。最终 Debug/Release
完整解决方案构建均零警告/错误，各 3,558 项相关回归通过（七套完整 3,144 + App 精选 414），
两配置和当前 2,688 项输入一致。首轮 App 全套使用修复前程序集，停止前报告 5,074 通过、
28 失败；28 项均在最终两配置逐名核对通过，未再次完整运行 App 全套，原失败和中止记录保留。
真实 NTFS/Git/ConPTY、Headless 键盘操作与 loopback 模型分别记录；不等于原生桌面或真实提供商验收。
链路、证据和详细剩余方案见 [WORKTREE_OBJECT_AUTHORIZATION_ACCEPTANCE.md](GIT_WORKTREES.md#worktree-object-authorization-acceptance)。
Git 临时输入会话检查点的扩大回归另发现审批待处理标志先于请求 ID 发布；现已调整为最后发布标志，
确定性覆盖即时批准/拒绝，策略和超时不变。原 Release 超时、未改源码复验和修复前后
证据均保留在 [GIT_DIFF_INPUT_SESSION_ACCEPTANCE.md](GIT_WORKTREES.md#git-diff-input-session-acceptance)，不能只用复验通过抹去原失败。
2026-10-21 Git 临时输入会话检查点的最终 Debug/Release 完整构建均零警告/错误，各 2,559 项相关回归
通过（含 App 精选 552 项，非全解决方案测试全套）。两配置和当前 2,611 项输入一致，
生产 CLI 的 8 个模块身份匹配；专项文档保留范围、失败记录及待验事项。

#### 状态和证据如何阅读

- 已实现：指定范围有生产入口、实际运行链路及对应证据，不表示整类功能全部完成。
- 部分实现：基本链路存在，明确列出的子能力仍缺失。
- 未实现：当前盘点未发现完整的生产运行链路；研究文档、同名按钮或配置存储不算实现。
- 未验证：已有实现或设计，但对应场景缺少当前版本的可靠证据，不能直接推断成功或失败。
- 自动化测试、真实本地 Git/Chrome/进程、模拟 HTTP、Avalonia Headless、原生 EXE 交互和
  真实外部账号分别标注。历史原生证据只证明其记录的版本和场景。
- 功能总表见 [FUNCTIONAL_PARITY_MATRIX.md](OVERVIEW.md#functional-parity-matrix)，模块边界见
  [ARCHITECTURE.md](../ARCHITECTURE.md)。两者和专项验收共同使用；旧记录中的“正在运行”
  或“尚未实现”描述仅代表其检查点，不覆盖本文件明确列出的后续结果。

#### 一、已经做出的可运行能力

| 范围 | 实际入口、链路及已实现行为 | 证据及适用边界 |
| --- | --- | --- |
| 项目和无项目对话 | 工作台新对话 → `WorkspaceTaskFactory` → 任务执行器 → Agent → 所选提供商。支持无项目文本/明确选定附件；普通无项目任务不获得任意目录、shell 或其他工具权限 | [PROJECTLESS_CHAT.md](CONVERSATIONS.md#projectless-chat)、[PROJECTLESS_ATTACHMENTS_ACCEPTANCE.md](CONVERSATIONS.md#projectless-attachments-acceptance)；附件选择不等于目录授权 |
| 流式、多轮和历史 | 提供商流 → Agent 事件 → 时间线与 SQLite；历史参与下一轮上下文，任务切换/重新打开恢复消息和状态 | [HISTORY_CONTEXT_ACCEPTANCE.md](CONVERSATIONS.md#history-context-acceptance)、[PROVIDER_STREAM_COMPLETION_ACCEPTANCE.md](PROVIDERS.md#provider-stream-completion-acceptance)；Responses/Anthropic 显式失败、不完整终态及缺失终态 EOF 已有拒绝处理，其他变体仍见 R06 |
| 并发、取消和中断 | 每任务运行控制器持有取消、事件、进程及浏览器资源；发送准备也可取消，提交后取消有可查看终态；启动将未完成任务标记中断，不自动重放 | [SEND_PREPARATION_CANCELLATION_ACCEPTANCE.md](CONVERSATIONS.md#send-preparation-cancellation-acceptance)、[SHUTDOWN_RECOVERY_ACCEPTANCE.md](TERMINAL_TOOLS.md#shutdown-recovery-acceptance)；不是所有副作用时序的崩溃验收 |
| 独立草稿与原子发送 | 任务/项目新会话/无项目草稿各自保存；版本冲突保护，后台保存串行；消息提交与草稿消费使用同一 SQLite 事务，保留发送期间的新输入 | [CONVERSATION_DRAFT_ACCEPTANCE.md](CONVERSATIONS.md#conversation-draft-acceptance)、[DRAFT_BACKGROUND_PERSISTENCE_ACCEPTANCE.md](CONVERSATIONS.md#draft-background-persistence-acceptance)、[CONVERSATION_SUBMISSION_ACCEPTANCE.md](CONVERSATIONS.md#conversation-submission-acceptance) |
| Queue 续发 | 活动对话 Enter 入队；可编辑、排序、移除、手动继续。保存模型/强度/代理提示词/授权和附件快照；准备校验后事务复核并分派至原任务。仅成功的活动链自动续发，取消、失败或重启后需手动继续 | schema 23；[CONVERSATION_FOLLOWUP_RUNTIME_ACCEPTANCE.md](CONVERSATIONS.md#conversation-followup-runtime-acceptance)、[CONVERSATION_FOLLOWUP_DISPATCH_ACCEPTANCE.md](CONVERSATIONS.md#conversation-followup-dispatch-acceptance)、[CONVERSATION_FOLLOWUP_PROCESS_RECOVERY.md](CONVERSATIONS.md#conversation-followup-process-recovery) |
| 文本 Steer | 活动 Ctrl+Enter/“引导当前运行” → `ConversationSteeringSession` → 草稿/receipt 原子接收 → 当前工具批次结束后的模型边界注入。保持原模型、提示词、工具根和审批策略；有界 mailbox、幂等 ID、可查历史及中断状态 | schema 25；[CONVERSATION_STEERING_ACCEPTANCE.md](CONVERSATIONS.md#conversation-steering-acceptance)。接收和应用均复核授权；schema 26 后任何项目目录变更均撤销旧运行，包括空边界。附件和用户默认行为配置尚缺 |
| 项目与授权目录 | 项目设置保存 → 仓库端口 → 任务授权快照 → 工具路径守卫；多授权根、路径穿越、链接/重解析点及恢复任务归属检查已有实现 | [PROJECT_AUTHORIZATION_UI_ACCEPTANCE.md](SECURITY.md#project-authorization-ui-acceptance)、[RESTORED_WORKTREE_AUTHORIZATION_ACCEPTANCE.md](GIT_WORKTREES.md#restored-worktree-authorization-acceptance)；句柄级竞态及即时撤权仍见 R02 |
| 文件编码工具 | 文件搜索/查找、读取、写入、编辑、复制、移动、删除 → 统一工具管线 → 授权路径验证与有界结果；写入使用原子替换和先前读取版本冲突检查 | [FILE_EDITING_ACCEPTANCE.md](CONVERSATIONS.md#file-editing-acceptance)、[FILE_SNAPSHOT_SHARING_ACCEPTANCE.md](GIT_WORKTREES.md#file-snapshot-sharing-acceptance)；已修复内部读句柄阻断原子编辑的问题 |
| 工具过程、审批及审计 | `ToolExecutor` 统一处理查找、schema、能力/路径、风险、审批、取消/超时、输出/产物和审计；共享界面显示等待/执行/结果；主/子代理均受工具 allowlist 限制 | [TOOL_SCHEMA_ACCEPTANCE.md](TERMINAL_TOOLS.md#tool-schema-acceptance)、[TOOL_SCOPE_ACCEPTANCE.md](TERMINAL_TOOLS.md#tool-scope-acceptance)、[GLOBAL_APPROVAL_PRESENTATION_ACCEPTANCE.md](TERMINAL_TOOLS.md#global-approval-presentation-acceptance) |
| 中断工具结果 | 启动恢复在事务中结束遗留进行中审计，共享审计行显示结果未知和手动核实要求；不把未知副作用显示为成功，也不自动重做 | schema 24；[AUDIT_INTERRUPTION_RECOVERY_ACCEPTANCE.md](TERMINAL_TOOLS.md#audit-interruption-recovery-acceptance)，含旧 schema 数据库副本迁移 |
| 命令及交互终端基础 | PowerShell 命令、ConPTY 输入/输出、VT、剪贴板/输入法、任务所属进程树和停止链路已实现；工具仍受工作目录、审批、时限、输出限制约束 | [COMMAND_EXECUTION_ACCEPTANCE.md](TERMINAL_TOOLS.md#command-execution-acceptance)、[TERMINAL_INTERACTION_ACCEPTANCE.md](TERMINAL_TOOLS.md#terminal-interaction-acceptance)；**当前不具备严格的命令文件系统隔离** |
| 变更审阅与撤销 | 侧栏真实 Git 状态、已暂存/未暂存差异、任务文件快照/统计/补丁预览及确认撤销，经过工具而非静态数据 | [DIFF_REVIEW_ACCEPTANCE.md](GIT_WORKTREES.md#diff-review-acceptance)、[TASK_SNAPSHOT_REVIEW_ACCEPTANCE.md](CONVERSATIONS.md#task-snapshot-review-acceptance)、[FILE_CHANGE_UNDO_ACCEPTANCE.md](GIT_WORKTREES.md#file-change-undo-acceptance) |
| Git 暂存、提交、推送 | 所选文件暂存/取消暂存；`git.commit` 对已暂存内容预览确认；`git.push` 对捕获的 commit 向单个 heads ref 预览确认。审批后复核仓库/HEAD/分支/索引或远端状态；不自动 stage-all、强推、amend 或重试 | [GIT_STAGING_ACCEPTANCE.md](GIT_WORKTREES.md#git-staging-acceptance)、[GIT_COMMIT_ACCEPTANCE.md](GIT_WORKTREES.md#git-commit-acceptance)、[GIT_PUSH_ACCEPTANCE.md](GIT_WORKTREES.md#git-push-acceptance)；真实临时 Git/bare 仓库和生产 Headless 入口，非真实远端认证验收。配置 hooks/helpers 仍有 OS 隔离缺口 |
| 工作树 | 项目新对话显式选择独立工作树和手动分支/标签/提交，解析为固定 commit；创建、连续对话、重开、逐操作守卫及确认清理贯通。校验 GUID 目录、链接祖先、仓库与外部 Git 元数据归属 | [WORKTREE_BASE_REFERENCE_ACCEPTANCE.md](GIT_WORKTREES.md#worktree-base-reference-acceptance)、[WORKTREE_PATH_SAFETY_ACCEPTANCE.md](GIT_WORKTREES.md#worktree-path-safety-acceptance)；已有真实 Git/并发任务/跨工作树拒绝，剩余见 R04 |
| 提供商与代理配置 | 多提供商、多 HTTP/SOCKS5 代理及默认代理；可明确直连，失效代理不静默替换。共享分组/展开/逐项编辑、发现/手工模型录入、默认与启用状态已有持久化和真实请求链路 | [PROVIDER_UI_WORKFLOW_ACCEPTANCE.md](PROVIDERS.md#provider-ui-workflow-acceptance)、[PROVIDER_PROXY_ACCEPTANCE.md](PROVIDERS.md#provider-proxy-acceptance)、[AUTHENTICATED_PROVIDER_PROXY_ACCEPTANCE.md](PROVIDERS.md#authenticated-provider-proxy-acceptance) |
| 协议及模型参数 | OpenAI Responses、兼容 Chat、Gemini GenerateContent、Anthropic Messages；流式与多轮工具调用。模型上下文/输出上限、可选或自定义思考强度、Temperature、Top-P、能力覆盖及高级 JSON 参与请求或拒绝校验 | [MODEL_REASONING_CAPABILITY_ACCEPTANCE.md](PROVIDERS.md#model-reasoning-capability-acceptance)、[GEMINI_CONTINUATION_ACCEPTANCE.md](PROVIDERS.md#gemini-continuation-acceptance)、[MODEL_CONFIGURATION_NATIVE_ACCEPTANCE.md](PROVIDERS.md#model-configuration-native-acceptance)；远端支持不能仅从本地配置推断 |
| 文本/图片附件 | 明确选择 UTF-8 和静态 PNG/JPEG/GIF/WebP → 真实解码和限额 → 文件快照 + SQLite 元数据 → 草稿/历史 → 对应原生请求块。图像单独成轮可用；模型能力不符保留草稿并拒绝发送；缩略图后台生成，JPEG EXIF 八方向处理 | [IMAGE_ATTACHMENT_WORKFLOW_ACCEPTANCE.md](CONVERSATIONS.md#image-attachment-workflow-acceptance)、[PROJECTLESS_ATTACHMENTS_ACCEPTANCE.md](CONVERSATIONS.md#projectless-attachments-acceptance)、[IMAGE_ORIENTATION_ACCEPTANCE.md](CONVERSATIONS.md#image-orientation-acceptance)。源文件删除不影响已保存快照，缺失/篡改快照拒绝请求 |
| 浏览器基础及取消修复 | 内嵌 WebView2、受控外部 Chrome/Edge、显式 CDP、任务标签页、DOM 操作、截图/下载和接管栏；取消/超时使旧会话失效，等待实际 SDK 操作和所属资源清理，不允许超时后旧点击继续 | [BROWSER_OPERATION_CANCELLATION_ACCEPTANCE.md](BROWSER_MCP.md#browser-operation-cancellation-acceptance)、[QUEUE_BROWSER_RUNTIME_ACCEPTANCE.md](BROWSER_MCP.md#queue-browser-runtime-acceptance)、[BROWSER_COMMITTED_EFFECT_RECOVERY_ACCEPTANCE.md](BROWSER_MCP.md#browser-committed-effect-recovery-acceptance)；借用 context 的 cookie/storage 共享，不能称配置完全隔离 |
| MCP | stdio/Streamable HTTP 配置、headers/代理、发现和动态工具注册；MCP 工具走同一管线。错误反馈、显式重连、重新加载配置/代理目录及关闭期间迟到结果抑制已有链路 | [MCP_EXECUTION_ACCEPTANCE.md](BROWSER_MCP.md#mcp-execution-acceptance)、[MCP_RECONNECT_RECOVERY_ACCEPTANCE.md](BROWSER_MCP.md#mcp-reconnect-recovery-acceptance)、[MCP_UI_RECOVERY_ACCEPTANCE.md](BROWSER_MCP.md#mcp-ui-recovery-acceptance)；显式重连不等于自动续传 |
| 自定义代理/子代理 | 名称、触发条件、系统提示词、模型、强度和上下文共享保存 → 父模型发布/触发委托 → 选定子模型 → 子工具 → 父对话过程和结果。子工具继承并收窄主任务权限、重新进入审批管线，不允许递归委托 | [SUBAGENT_SETTINGS_TO_RUNTIME_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#subagent-settings-to-runtime-acceptance)、[SUBAGENT_LIFECYCLE_NATIVE_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#subagent-lifecycle-native-acceptance)；历史独立原生实例 + 模拟模型已验证一组设置到运行及重开链路，不能扩大为当前全部分支原生通过 |
| 定时任务 | 定时任务页创建/编辑、保存模型/提示词/授权、启停、立即运行、停止、历史及打开结果。独立后台任务不消费前台草稿；启动跳过过期触发并标记中断，不补跑未完成副作用 | [AUTOMATION_UI_WORKFLOW_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#automation-ui-workflow-acceptance)、[AUTOMATION_MUTATION_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#automation-mutation-acceptance)；Headless 已验证保存参数进入真实适配器请求，关闭/恢复组合见 R10 |
| GitHub PR 只读链路 | 独立来源账号/token/代理、显式仓库只读授权 → 开放 PR/URL → 描述/分页补丁/讨论/行内评论/checks。评论按当前 commit、路径和有效差异行定位；有固定快照审阅对话及独立只读按需查询对话 | [SOURCE_ACCOUNT_CONNECTION_ACCEPTANCE.md](GIT_WORKTREES.md#source-account-connection-acceptance)、[SOURCE_PR_INLINE_COMMENTS_ACCEPTANCE.md](GIT_WORKTREES.md#source-pr-inline-comments-acceptance)、[SOURCE_PR_ON_DEMAND_READING_ACCEPTANCE.md](GIT_WORKTREES.md#source-pr-on-demand-reading-acceptance)、[SOURCE_PR_URL_ACCEPTANCE.md](GIT_WORKTREES.md#source-pr-url-acceptance)；撤权/版本变化拒绝后续查询，不隐式启用本地工具 |
| UI 与工程基础 | Avalonia/.NET 10，共享包装控件、集中主题/样式、本地化资源、编译期组件约束、模块依赖检查、SQLite 向前迁移；普通构建 JIT，Windows x64 自包含单文件方案保持，无 AOT/裁剪 | [UI_COMPONENTS.md](../UI_COMPONENTS.md)、[ARCHITECTURE.md](../ARCHITECTURE.md)、[../scripts/publish.ps1](../../scripts/publish.ps1)；界面整体可用性和当前单文件发布验收仍见 R23/R24 |

#### 二、2026-10-20 阶段收尾检查点的验证结果

冻结候选：`artifacts/conversation-steering-20261020/candidate-r4`。该候选不再重建。

| 检查 | 已确认结果 | 证据性质 |
| --- | --- | --- |
| 候选完整 Debug | 8,282/8,282；8 份 Completed TRX，零失败/跳过/中止，构建零警告/错误 | 自动化、Headless、本地确定性服务和真实本地资源按各测试组合运行；不是全产品原生验收 |
| 候选完整 Release | 同上，8,282/8,282；与 Debug 分别保存报告 | 不是单文件发布或真实账号验收 |
| 全量组成，每配置 | Agent 160、Providers 617、Browser 104、Windows 148、Infrastructure 788、Tools 666、Visual 300、App 5,499 | 每配置互不重复；不与专项重叠累加 |
| 候选冻结身份 | 每配置 2,490 项输入和 2,429 份运行文件的路径/hash 前后一致；收尾分类确认输入含 2,455 份源码/配置/脚本及 35 份旧的忽略输出，并非 2,490 项全是源码。仅测试生成 PNG 排除在运行文件身份外 | 两份 `Full-*-repair4-summary.json`，原始清单不修改 |
| 原仓库合入验证 | Debug/Release 解决方案构建均零警告/错误；各 147 项专项通过：App 97、Infrastructure 45、Agent 5 | `Integrated-reviewed-repair4.json`；不是又一次原仓库完整测试 |
| 本次收尾构建复核 | 原仓库再次执行 Debug/Release 解决方案构建，两者均零警告/错误；删除 PID 测试文件末尾空行后，每配置的该项实际进程测试再次通过 | `artifacts/delivery-20261020/` 的构建日志和两份 TRX；没有重复运行完整测试 |
| 提交内容独立构建 | 将暂存树导出到无既有 bin/obj 的独立目录，Debug/Release 解决方案构建均零警告/错误，各再通过同一项 PID 进程测试；2,455 份源码/配置/脚本全部进入索引且与导出文件内容一致（仅统一比较 CRLF/LF） | `index-*-build.log`、`IndexTestResults/`；证明源码提交不依赖本地忽略的三份接口或旧输出，仍不是另一次全量回归 |
| 原仓库进程终止/恢复 | 每配置 4 场景：项目/无项目 × 已接收未应用/已应用；下一条草稿保留，重启无模型请求，显式新回合不重用旧 receipt | 实际独立子进程、生产 DI/Agent/Providers/SQLite、本地模拟 HTTP；四个加载程序集 hash 已核对 |
| 结构检查 | 2,263 个 C#、20,589 个方法/访问器/lambda；最大文件 496 行、最大方法 78 行，零违规 | `structure-audit-repair4.json`；共享控件/架构测试另在全量内 |
| 本次收尾完整性 | 初核原仓库上述 2,490 项输入与已验证候选全部一致；随后仅删除 `PtyPidPublicationTests.cs` 和两份接口文件的末尾空行，其他生产/测试源码和脚本不变。另修正 Git 忽略规则，纳入原已存在且参与验证的三份 Application/Artifacts 接口，继续排除其子目录的旧生成输出 | `artifacts/delivery-20261020/verification.json`；三份原始文件备份可核对，不能把最后的空行清理也称为候选字节完全相同 |

全部原始证据位于 `artifacts/conversation-steering-20261020/`，摘要已在
[CONVERSATION_STEERING_ACCEPTANCE.md](CONVERSATIONS.md#conversation-steering-acceptance) 入库。
最初候选的 18 项失败及后续六组主动中止回归均保留，不改写为成功；旧超时和一次
Queue 选择/草稿断言失败的精确原因未证明，不能归因于负载，也不能宣称已修复串扰。

历史原生证据包括部分提供商/模型、子代理及综合编码链路，详见各专项文档；
[NATIVE_SUBMISSION_ACCEPTANCE.md](CONVERSATIONS.md#native-submission-acceptance) 另记录一次无项目
实际输入/发送/流式观察/强制终止重启。其截图黑帧、激活失败和 `GetCursorPos` 拒绝
仍是自动化限制，既不是通过的视觉验收，也不是已证明的 MossAgent 输入缺陷。
当前 Steer、全部关键工作流原生复验及真实外部提供商/PR 账号验收仍未完成。

#### 三、未完成范围及优先顺序

| 编号 | 状态 | 剩余工作 | 阶段 |
| --- | --- | --- | --- |
| R01 | 部分实现，安全阻断 | Windows 命令/终端及其他代码执行的真实文件系统隔离 | P0 |
| R02 | 部分实现/未验证 | 路径句柄竞态、运行中授权撤销、统一管线边界完整审计 | P0 |
| R03 | 部分实现/未验证 | 附件 Steer、发送行为配置、Queue/Steer 竞争及事务/副作用中崩溃 | P1 |
| R04 | 部分实现/未验证 | 分支发现、工作树创建中崩溃、并行取消/部分失败和原生恢复 | P1 |
| R05 | 部分实现 | PDF/其他文件、全图查看、快照安全回收、子代理图像共享 | P1 |
| R06 | 部分实现/未验证 | 全协议终态/恢复与真实提供商、代理和模型能力矩阵 | P1 |
| R07 | 部分实现/未验证 | 浏览器各模式切换、人工接管、其他创建/副作用崩溃时序 | P1 |
| R08 | 部分实现/未验证 | MCP 认证、配置竞争、传输中断、取消和恢复完整闭环 | P1 |
| R09 | 部分实现/未验证 | 子代理当前版本完整触发/审批/失败/共享/恢复矩阵 | P1 |
| R10 | 部分实现/未验证 | 定时任务原生关闭、中断、并发、恢复和时区组合 | P1 |
| R11 | 部分实现 | Git 区块/整组暂存、远端与凭据界面、提交草稿恢复和原生验收 | P2 |
| R12 | 部分实现 | 完整 PR 收件箱、reviews/activity、版本协调、确认写入、OAuth/GitLab | P2 |
| R13 | 未实现 | 插件/技能/扩展目录、安装、启停、实际执行、升级与卸载 | P2 |
| R14 | 部分实现 | 敏感截图之外的目标绑定桌面指针/键盘链路 | P2 |
| R15–R17 | 未实现/参考范围待核实 | Web 搜索、图像生成、语音、输入预测的独立链路 | P3 |
| R18 | 部分基础/未验证 | 系统通知、长任务目标/预算/生命周期 | P3 |
| R19 | 部分基础/未验证 | 文档和交互内容可信预览、产物保存/导出 | P3 |
| R20 | 部分基础/未验证 | Appshots、浏览器扩展协议及跨进程接管 | P3 |
| R21 | 未实现/依赖待核实 | 宠物和 Codex Micro 外设 | P3 |
| R22 | 未实现/接口待核实 | Sites、dots、Space、ChatGPT 登录、远程/云/移动服务 | P3，先做接口与范围决策 |
| R23 | 未验证/部分问题待定位 | 关键原生窗口、DPI/resize、键盘焦点、主题与可访问性 | 各阶段随功能验证，最终总验收 |
| R24 | 未验证 | 当前版本 Windows x64 JIT 自包含单文件正式发布前验收 | P4 |

实施顺序：P0 先解决安全阻断和授权边界；P1 收紧已有核心闭环；P2 补齐完整编码协作及
扩展；P3 按核实后的功能依赖推进其他入口；P4 完成整体验收和发布准备。P3 的外部接口
调研可以提前进行，不能用其替代 P0。各功能完成前仍保留明确的缺口说明，不隐藏入口
或以降级行为冒充完成。实际安装、系统预置、外部写入和发布需要各自明确授权；方案本身不授权这些操作。

#### 四、逐项实施方案

##### 所有方案共同遵守的工程与验收要求

行为放在下列指定模块，复用 Application/Tools.Abstractions 端口；App 仅协调和展示。
每个工具独立实现 `IAgentTool`，按查找 → schema → 能力/路径 → 风险 → 审批 →
取消/超时 → 执行 → 输出限制/产物 → 审计的顺序运行，不能直接互调绕过管线。
默认未知外部工具有副作用，不因名称或不可信元数据自动判为只读。

新增状态通过当前 schema 28 之后的向前迁移保存；消息/配置/产物归属和版本关系明确，
不删除或重解释旧数据。每任务独立取消、事件、进程树和浏览器会话；取消后等待实际
资源结束，结果未知时保留状态，恢复不重放。文件型大输出只存元数据与有界预览到 SQLite。
诊断不保存密钥、授权头、代理凭据或 Cookie。拆分类型和方法，继续执行单文件单类型、
500 行文件/80 行方法硬限制及共享组件规则。

每项交付至少覆盖成功、拒绝/权限撤销、失败、取消/超时、两任务并发和持久化恢复；
涉及外部副作用时增加“已生效但未拿到结果”的断点。先确定性测试和真实本地资源，
再用独立原生实例验关键入口；需要真实账号时另做明确授权的验收。窄测试和完整构建
通过后做阶段全量，最终同时完成 Debug/Release 和正式包验证，不重复累加覆盖数。

##### R01：Windows 命令文件系统隔离（最高优先级）

- **缺口与已否决路线：** 生产普通用户令牌可读根外标记；AppContainer/LPAC 的包组暴露不满足严格 allowlist。KnownDlls 的 44 个对象在真实管理员预检仍全部拒绝 WRITE_DAC，已确认 WinTcb-Light；临时改系统 ACL 路线否决。此前两次单次管理员授权均已消费，不能复用。
- **模块与契约：** Tools.Abstractions 增加不可变任务执行范围：task/run、规范化授权根、明确只读运行时依赖、任务私有 temp/cache、网络策略及后端身份。Platform.Windows 实现隔离适配器；复用现有进程和终端端口、Job/管道/审计。盘点 Git hooks、凭据 helpers、stdio MCP 等代码执行入口，避免只修 shell。
- **后端方案（用户已选定）：** 在真正支持原生 MXC ProcessContainer 的独立 Windows 环境验证 SDK；必须按每次请求的实际 tier 强制隔离，拒绝 AppContainerDacl 等弱回退，不仅检查 IsSupported。现有 23H2 只检测到 DACL fallback；本轮不预置容器/VM，不升级本机，不移除现有入口，不把弱回退视为完成。现有 Linux Docker、单实例 Windows Sandbox 不能算替代完成。
- **实施步骤：** 先做 pwsh7/.NET SDK/Git/ConPTY 的正常命令兼容探针；再落实 launch 与 capability 变化间的竞态、真实存活句柄/晚启动回收、取消后 drain。SDK 的停止等待不能等同终止进程；PTY 若落到要求无限网络的隔离层必须拒绝并另实现合格桥接。所需系统预置拆成可审阅、可清理的一次安装步骤，失败显式报告，不改全盘 ACL、不自动切换后端。
- **验收：** 同一生产管线复现必须变为根内读写成功、绝对/相对路径/链接/子进程/继承句柄访问根外失败，外部标记不变；多根、多任务互相拒绝；测试启动失败、后端消失、超时/取消、崩溃恢复、正常 Git/SDK 和原生终端。当前依赖是可访问的受支持 MXC 验收机；后端优先路线已确认，不表示放弃 23H2 或已授权系统变更。

详见 [COMMAND_FILESYSTEM_ISOLATION_PLAN.md](SECURITY.md#command-filesystem-isolation-plan)、
[WINDOWS_ISOLATION_BACKEND_DECISION.md](SECURITY.md#windows-isolation-backend-decision)。

##### R02：授权撤销、路径竞态与统一执行边界

- **产物会话增量：** 普通预览、编辑 before/after 和最终审阅补丁已接入保留所有权至调用方成功的原生会话，失败按句柄清理。详见 [OWNED_ARTIFACT_SESSION_ACCEPTANCE.md](SECURITY.md#owned-artifact-session-acceptance)。浏览器截图/下载继续接入受控会话与 SDK 原对象清理，范围和剩余方案见 [BROWSER_ARTIFACT_SESSION_ACCEPTANCE.md](BROWSER_MCP.md#browser-artifact-session-acceptance)。Git 临时 diff/numstat 输入已接入仅共享读取、相对创建与原句柄清理的专用会话，证据及后续方案见 [GIT_DIFF_INPUT_SESSION_ACCEPTANCE.md](GIT_WORKTREES.md#git-diff-input-session-acceptance)。后续分别处理浏览器缓存未知状态/配额、持久授权代际、崩溃阶段记录与引用回收；不把输入固定或最终补丁写入保护当作 Git 进程隔离完成。
- **本轮增量：** Windows 读取守卫接入生产工具执行器，文件读取/预览、编辑前哈希、快照捕获/审阅和复制源在读取前校验已打开句柄。真实 junction 切换复现、管线和生产 DI 证据及限制见 [READ_HANDLE_PATH_SAFETY_ACCEPTANCE.md](SECURITY.md#read-handle-path-safety-acceptance)。其余 R02 方案继续有效。
- **后续写入增量：** 生产原子文本写入器使用 Windows 目录租约、独占暂存句柄及相对目录句柄提交；变更后快照直接读取暂存流，失败按句柄清理。普通权限/属性保留和不支持元数据的拒绝已有负例。只覆盖本地常规 NTFS；最终名称条件替换、其他文件入口、授权代际及崩溃清理仍未关闭，详见 [WRITE_HANDLE_PATH_SAFETY_ACCEPTANCE.md](SECURITY.md#write-handle-path-safety-acceptance)。
- Infrastructure/Platform.Windows 将字符串检查延伸到打开后的句柄、最终路径、文件/目录身份及 Git 元数据；原子替换前再验证目标和父目录，复用当前授权/工作树端口，不在 UI 增加 I/O。
- Application 的项目配置版本、保存冲突保护和撤销租约已实现，详见 [PROJECT_AUTHORIZATION_REVOCATION_ACCEPTANCE.md](SECURITY.md#project-authorization-revocation-acceptance)；schema 27 登记主根/附加根，schema 28 继续登记任务工作树独立对象身份，详见两份对象授权专项文档。下一步完善项目删除/重建与同路径 ABA、创建认领/清理句柄交接、资源 drain/未确认状态、不可取消原生调用 worker、跨实例聚合订阅和规模测试。继续保留审批后/执行边界复核；已发生的外部副作用需检查，不能声称撤销等于回滚。
- 审计所有内置、浏览器、MCP、子代理和辅助统计入口的 schema/范围/风险/审批顺序，防止后台 UI 查询或子工具走旁路；评估可撤销资源是否需要更细的能力租约。
- 用确定性 gate 在检查/打开/替换/审批间替换 junction、父目录和元数据，验证根外不读不写、保留并发外部修改；增加撤销与工具执行、终端及另一任务并发负例，再做原生恢复。

##### R03：Queue/Steer 的剩余能力和竞争恢复

- Application 复用草稿/附件/Queue/Steer 端口，将附件 Steer 表达为不可变的任务所属快照引用，验证模型能力和完整上下文预算后原子接收；附件不足或拒绝不消费草稿，不扩大原运行权限。
- App 增加本地化的默认/替代发送方式设置和明确的 Queue/引导提示，保存选择且约束只在支持的状态生效；快捷键与鼠标共用命令。多行输入、输入法组合、长草稿/队列/历史焦点保持单独验收。
- 用运行身份、队列/草稿 revision 和 receipt ID 控制 enqueue/编辑/排序/dispatch/complete/close 竞争；覆盖授权/模型变化、提交后取消、最后一轮终态与新引导竞争。新增数据库断点与事务前后真实宿主终止，不把任意 sleep 当事务中断证据。
- 对工具已生效但 receipt 尚未应用、receipt 已应用但 UI 未刷新分别终止/恢复，证明不重放、不重收草稿，保留明确状态；项目/无项目、并发任务、原生输入及真实提供商各自记录证据。延续现有文本行为，不改写旧失败结论。

##### R04：工作树完整生命周期

- **已做增量：** schema 28 的不可变工作树目录 grant、旧工作树明确确认、恢复拒绝替换和执行租约已实现；不代表下面创建 intent、清理最终边界、崩溃和原生验收已经完成。详见 [WORKTREE_OBJECT_AUTHORIZATION_ACCEPTANCE.md](GIT_WORKTREES.md#worktree-object-authorization-acceptance)。
- Infrastructure 通过现有受管 Git runner 做有界本地分支/标签查询和分页搜索，App 显示实际候选、解析 commit 及错误，不用自由输入框冒充发现。远端 ref 获取必须是另一个显式动作。
- 在创建前落库 intent/task/仓库/基准 commit/目标目录身份，成功后原子认领；启动只核对未完成 intent 并展示人工恢复/清理，不自动重建或删除归属未知目录。迁移不修改既有工作树。
- 复用 R02 处理句柄竞态和即时撤权；在创建和清理前后校验 `.git` 指针、外部元数据、双向归属及链接祖先。两任务同时创建时各自取消和清理，保留另一任务成功结果。
- 增加实际 Git 创建中各断点崩溃、并行取消/部分失败、分支不存在/移动、磁盘占用、清理失败及原生 EXE 重启；验收草稿/消息/授权保留和根外不变。已有同步并行成功不能覆盖这些组合。

##### R05：PDF、其他附件、全图与安全回收

- Domain/Application 定义格式、来源、MIME/内容校验、大小/页数/像素/展开量限额和模型能力矩阵；Infrastructure 负责快照/解析，Providers 负责原生文件块或明确选择的有界文本提取。两种语义必须在 UI 说明，不能把抽取文字说成完整 PDF 理解。
- App 用共享预览容器增加全图缩放/适应窗口和 PDF 分页；后台解码、取消、内存缓存限额，不从历史卡片重新读取原文件，不自动执行附件脚本或加载远端资源。
- 子代理图像共享复用有归属的只读快照并显式纳入共享开关、能力和预算；不共享时请求中不能出现图像。回收采用数据库引用集合 + 租约/宽限期 + 删除前复核，覆盖草稿、历史、Queue、Steer、子代理和执行中引用，不按目录年龄直接删除。
- 验收损坏/伪装/超大/压缩炸弹、缺失/篡改、无项目明确选择、外部文件变化、共享关闭、并发回收和恢复；另做原生选择器/全图/EXE 重启及真实模型图片/PDF理解，不能用 loopback 请求形状代替理解。

##### R06：提供商、模型、代理的完整兼容与恢复

- 保持现有四协议和清晰的提供商/模型分组，Providers 按请求构建、解析、事件与错误映射分工；为每协议补齐空流、显式 failed/incomplete、EOF、工具片段缺失、重复终态、取消和续轮 fixtures，防止错误完成后 Queue 继续。
- 模型能力以逐模型覆盖与协议支持共同判定；未知自定义强度保留输入并给明确校验，不静默替代模型/参数。补 PDF/文件 capability 到请求前校验和 UI 修复路径；模型发现不得覆盖用户设置。
- Infrastructure 代理路径验证默认/明确直连/凭据代理、禁用/删除、编辑和请求并发、连接失败/超时/证书/重定向边界；失败不静默直连。必要兼容选项显式配置，不降低全局 TLS 或日志保护。
- 用本地 JSON/SSE/认证代理做完整确定性矩阵，再在获准账号上分别验证四协议、图片/PDF、思考参数和断线恢复；真实请求只保存脱敏摘要和能力结论。当前本地兼容性不能等同所有远端服务已验证。

##### R07：浏览器模式、接管与崩溃矩阵

- Browser 将 session/context/page、profile、DOM、截图/下载分别管理；App 选择内嵌/受管外部/显式 CDP 时预览任务归属与会话策略。切换先停止旧动作，等待任务资源清理再绑定新会话，不扫描任意端口。
- 为人工接管建立明确状态和排他 gate：交给用户时暂停自动操作，用户显式交回后刷新页面状态再继续。日常 profile 和桌面截图按敏感审批，借用 CDP context 的 cookie/storage 共享边界明确展示，未经授权不复制或清空。
- 已修复的取消/drain 路径扩展到 SDK 创建/launch/新标签/下载的其他时序；任务只关闭自己拥有的页面/context，不能关闭借用用户页面或另一实例。崩溃恢复只展示待重新附着，不重放 click/POST。
- 本地页面记录有界副作用计数，覆盖内外切换、创建取消、接管中停止、下载限额、host/browser 崩溃、受管/内嵌/日常/CDP 重启以及并发任务；最后用独立原生实例验证接管栏和真实窗口行为。

##### R08：MCP 认证、配置竞争和传输恢复

- MCP 继续通过既有 manager/adapter 和统一工具管线，补认证策略及凭据引用、服务身份/传输/schema/tool 清单代际。配置写入在 repository 端使用 revision/CAS，覆盖预检后外部修改，不能只靠编辑器 preflight。
- 重连期间旧注册表要可识别且不可误用；发现结果仅按当前连接代际发布，失败保留可诊断状态。认证挑战、401/403、stdio 退出、HTTP 会话失效分别提供配置修复或显式重连，不把断开后的调用自动续传。
- 明确共享 stdio 服务进程与每任务调用的取消归属；R01 约束服务启动环境。取消/超时等待实际传输/子进程收尾，未知远端副作用保留审计，重启不重发。只读 annotation 仍需可信配置和校验。
- 确定性 stdio/HTTP 服务覆盖配置保存竞争、认证刷新、发现时工具变化、拒绝、输出过大、连接中断、取消重连与并行调用；原生编辑→发现→审批执行→故障→人工恢复形成一条完整记录。

##### R09：子代理完整状态和当前版本验收

- Agent/Application 复用现有委托与子工具 gateway，补齐父子 run 身份、配置快照、终态和历史投影；触发条件是主模型可使用的委托指导，需验证实际选中而非宣称确定规则自动触发。失效/禁用模型不能静默换用父模型。
- R05 完成图像共享；文本/图片共享开关分别落实到子请求和预算。子工具只能继承并收窄主任务授权/allowlist，审批和审计保持子任务关联，取消父任务等待子模型和子工具收尾。
- 历史以运行/失败/取消/中断及未知工具结果展示，恢复可查看已完成片段；手动再次委托创建新 run，不重放旧工具。检查导航/销毁期间迟到进度，防止写入另一个对话。
- 当前版本重复设置保存→触发→选定模型/强度/提示词/共享→真实文件工具→父对话结果，覆盖手工允许与拒绝、错误/超时、父子取消、两子任务并发、EXE 重启和能力不足；本地模拟与真实提供商证据分开。

##### R10：定时任务关闭、并发与恢复

- Infrastructure 调度器复用现有仓库和独立 task runner，明确保存配置版本、触发时间/时区、错过时间策略及单次运行所有权；编辑只影响后续触发，活动运行保留启动快照，不改前台模型/草稿。
- 启动继续跳过过期触发、标记遗留运行中断；应用关闭先阻止新认领，再取消并等待已有任务。崩溃或未知副作用只提供记录查看/手动新运行，不能恢复为自动重试。
- 现有暂停与停止保持不同语义：暂停未来计划，停止本次运行。针对重复触发/手动与定时碰撞、删除运行中计划、超时/时区/DST/系统时钟变化采用明确状态和事务认领，保留历史。
- 在独立原生实例编辑保存非默认模型/提示词/授权，验证实际请求和工具政策；并行保留另一任务及未发送草稿，关闭/杀进程后重启查看记录，确认零自动补跑。时间计算用可控时钟，真实关闭另验。

##### R11：Git 区块操作、远端管理和原生工作流

- Tools.BuiltIn 增加独立整组/区块暂存和取消暂存工具，预览绑定 HEAD/index/工作区文件版本及精确补丁；审批后复核，拒绝混淆 rename/copy、外部索引变化和不完整差异，不能用 stage-all 替代用户选区。
- Application/Infrastructure 定义远端/上游配置预览与确认端口；App 提供真实读取、变更结果和凭据方式说明，使用明确账号/系统凭据集成，不读取任意浏览器或 CLI 凭据。只显示脱敏 endpoint，不回显原始 Git 配置或 push stderr。
- 提交消息当前只在内存按任务隔离；若补重启恢复，用独立版本化草稿保存，成功/失败与取消后核实实际 HEAD 再处理。保留 hooks/signing，不因测试失败跳过；hooks/helpers 服从 R01 隔离，不声称 Git 参数约束就是 OS 沙箱。
- 临时仓库验证区块选取、冲突/空变更、审批期变更、hook失败/已提交但回包取消、非快进拒绝及两任务；再做原生键盘预览确认、重启草稿恢复和获准真实远端认证。推送是单独显式动作，不随本次开发提交执行。

##### R12：完整 Pull Request 工作流

- SourceControl Application 端口与 Infrastructure 来源客户端补个人收件箱、reviews/activity、过滤/分页/限流和版本刷新协调；按 account/repository/PR/head 保存 viewed/标记，版本变化明确标旧并重新加载，不混合不同 head 的评论和补丁。
- Tools.BuiltIn 为创建 PR、普通评论、行内评论、提交 review、关闭/合并等分别定义工具和授权；App 先准备可审阅的目标、base/head、正文、决策和差异位置，再独立确认。写权限必须显式新增，不能从只读 grant 推断。
- 写入前复核账号、仓库、权限和最新 revision，使用服务支持的幂等/条件请求；网络结果未知先查询并核实，不自动重试。创建 PR 不隐式 commit/push；发布分支缺失由独立 Git 推送流程解决。检查日志/第三方 URL 不因 PR 响应自动获得读取权限。
- OAuth 需要正式客户端注册和回调设计；GitLab 实现独立适配器及原生语义，不能只改 URL；云审阅另列接口/账号依赖。确定性 HTTP 验证竞态、拒绝、失败和重启，再用明确获准的测试仓库/账号作原生及真实服务验收。

详见 [PULL_REQUEST_WORKFLOW_PLAN.md](GIT_WORKTREES.md#pull-request-workflow-plan)。

##### R13：插件、技能与扩展

- Application 定义目录/安装/启停/版本与组件端口，Infrastructure 实现本地文件包、配置与锁定版本；按已核实的 `plugin.json`、skills、`mcp.json` 和 OpenAI overlay 整体替换优先级解析，单独测试 legacy 导入。
- 先打通本地包预览→来源/哈希/能力说明→确认安装→启用→真实 skill 提示词或 MCP 工具执行→停用→卸载；目录/市场作为独立来源适配器，来源信任和账号授权不由包自行声明。zip traversal、链接、覆盖冲突和下载限额必须拒绝。
- skill 内容作为有来源的指令；MCP、hooks、代码和工具 UI 分别建立信任/审批/隔离。升级原子切换，运行中租约保留原版本；失败回滚配置，不自动执行安装脚本，不自动重放未完成 hook。停用立即阻止新调用，卸载只清理自己的受管文件。
- 官方 App Server 的 plugin list/read/install/uninstall 仍标为禁止生产客户端调用，不能把它作为可用目录。先评估公开支持的目录/connector/认证接口；依赖未开放就保持明确缺口。验收实际技能影响请求、MCP执行/审批、升级失败、并发与重启，不能只测包能保存。

详见 [PLUGIN_WORKFLOW_PLAN.md](OVERVIEW.md#plugin-workflow-plan)。

##### R14：目标绑定的桌面操作

- Application/Tools.Abstractions 定义任务绑定的窗口/应用会话和截图/坐标/键盘端口，Platform.Windows 实现；每种动作独立工具，复用敏感审批、超时、输出/截图产物和审计，不使用全局未绑定输入。
- 明确目标窗口身份、焦点和 DPI/坐标空间，操作前复核窗口/PID/会话代际；失焦、窗口关闭、UAC 安全桌面或用户接管时停止并报告，不向其他窗口“尽力点击”。
- 用独立本地测试窗口验指针/键盘、权限拒绝、失焦/窗口替换、取消与并发；人工接管后必须显式交回。日常用户窗口和账号只在具体授权场景验收，截图基础不算完整桌面控制。

##### R15：Web 搜索

- 先核实参考入口语义，区分 provider 原生搜索、指定搜索服务和显式 MCP 适配；Application 定义结果/引用/预算端口，Providers 或独立工具实现，不能把普通网页导航冒充搜索。
- 若 provider 原生搜索会触发网络动作，必须先纳入同一授权/审批和审计，不能直接在请求里开启绕过工具政策；凭据、允许目的地、查询长度、页数/字节/时间和费用边界显式配置，搜索结果是非可信数据。
- 贯通查询→结果/来源→正文取回→回答引用，覆盖服务不可用、拒绝、取消、重复/虚假来源和恶意页面；真实服务证据另做，未知私有搜索接口保留缺口。

##### R16：图像生成与编辑

- Providers/工具端口定义生成/编辑模型能力、输入图片归属、尺寸/数量、格式和预算，产物落文件存储并记录 hash/大小；App 显示进度、结果预览、保存及失败恢复，不能用输入缩略图代替生成。
- 服务请求经过统一管线；明确计费/外传输入与审批，取消结果未知时查已有任务/产物，不自动重发收费请求。校验下载来源、MIME/真实解码、输出字节/像素限制，禁止带授权头跟随任意重定向。
- 模拟协议验证完整创建/轮询/下载/展示与拒绝/限流/损坏/超大/取消/恢复；再用获准图像服务证明实际生成和编辑，保留服务依赖和未支持模型清单。

##### R17：语音和输入预测

- 分别核实语音输入/实时会话/语音输出和输入预测的产品范围。Application 拆开音频会话、转写/合成、预测端口；Platform.Windows 管麦克风/播放，Providers 适配协议，App 提供录音/停止/确认发送及可关闭预测。
- 麦克风显式授权且可见录音状态，导航/关闭立即释放；转写先进入草稿，不自动发送或触发工具。预测有独立取消/代际，不能覆盖新输入，明确是否向远端发送草稿；不记录原始敏感音频。
- 可控音频与模拟服务测试拒绝、设备丢失、断流/超时、输入法、陈旧建议和并发会话；独立原生麦克风/扬声器及真实服务另验。不能以文本 SSE 通过冒充实时音频可用。

##### R18：通知和长任务生命周期

- Application 定义通知事件/订阅、长任务 objective/run/budget/checkpoint；Platform.Windows 提供系统通知适配，Infrastructure 保存目标、预算和可查看状态，不以无限循环或定时任务同名入口代替。
- 通知按用户偏好、合并频率及静默时段发出，点击只定位对应任务；不显示密钥或敏感正文。目标具有开始/运行/需人工/完成/失败/取消/中断状态和时间/token/成本上限，暂停或恢复均遵循显式用户意图。
- 崩溃只恢复状态和检查点，不重放副作用；需要应用关闭后继续运行时必须先明确后台宿主/系统调度安装与权限，不能默认已有。验收预算耗尽、通知关闭/点击、并发前台草稿、进程重启和原生 toast。

##### R19：可信文档预览、可视化与产物导出

- Application/Infrastructure 以 task/message/tool/artifact 所有权、MIME、hash、大小和来源管理生成文件；App 通过共享预览宿主按文本/代码/图片/PDF/Office/交互 HTML 分派，格式不支持时明确说明。
- PDF/Office 优先只读安全渲染或显式外部打开；交互内容在隔离 WebView 中禁默认网络、文件访问和宿主桥，所需权限逐项授予，不能把生成 HTML 当可信应用代码。远端链接/脚本不自动加载。
- 保存/导出由明确目的路径授权、原子写入和覆盖冲突检查完成；取消/失败保留原文件，产物缺失/篡改显示修复路径。验收生成→预览→保存→重启、恶意内容、超大资源、被占文件和并发回收，不能只验附件缩略图。

##### R20：Appshots、浏览器扩展和人工接管协议

- 先逐项核实 Appshots 实际快照内容、支持平台和可公开格式；Browser/Application 定义任务所属的页面/视口/DOM/图片快照及脱敏策略，存产物元数据，不把现有截图工具直接改名算实现。
- 扩展需要明确 manifest、浏览器安装/版本、origin/extension 身份校验、一次配对和 task/tab 绑定；使用有界协议与能力协商，不任意打开调试端口。官方协议未公开时记录兼容缺口，独立扩展方案不能声称官方兼容。
- 与 R07 共享接管状态，测试标签切换/关闭、浏览器重启、连接被替换、消息伪造、拒绝和取消；实际安装及日常 profile 访问另获明确授权，原生扩展/接管体验单独验收。

##### R21：宠物和 Codex Micro 外设

- 宠物先核实动画/活动反馈/点击入口/窗口跟随的实际语义，App 实现独立可关闭的展示服务和受管资源；不读取任意窗口或让装饰动作触发工具，资源缺失/重启/多窗口及无障碍可关闭可测试。
- Micro 先确认公开硬件/协议、Windows驱动、设备配对和权限。Platform.Windows 提供设备端口，Application 只接受白名单事件，敏感命令仍进入普通授权/审批；未公开协议、无硬件就保留未验证，不伪造可用设备。
- 宠物用状态/动画/交互与原生窗口验证；外设用协议模拟覆盖断连/伪造/取消，再用真实硬件验证。两项独立列范围、依赖和进展，不以静态图标交付。

##### R22：Sites、dots、Space、账号和远程/云/移动能力

- 先建立逐入口语义/公开接口/账号计划/凭据/平台限制/可实施边界表：Sites 的构建发布协作、dots 的持续责任代理、Space 的共享页面、Sign in with ChatGPT 的登录授权，以及远程运行、云审阅和移动访问分别核实；当前目录信息不能当 Windows 实机入口完整清单。
- Application 为身份、远程运行、部署、共享/同步分别建端口；只有官方公开允许的 API/SDK 才实现服务适配。ChatGPT 登录需要注册、授权范围、token刷新/撤销和回调设计，不能把本地 API key 当同等登录，也不调用内部私有端点或读取浏览器 Cookie。
- 接口开放时实现可取消的提交/状态/产物/日志和明确授权的写动作；远程任务与本地任务范围分离，凭据最小化，防重复发布/支付/分享。没有公开接口时提出独立本地/自托管对接方案并标明语义差异，由用户确认范围，不能擅自作为专有服务已完成。
- 明确待决策项：需对接的具体官方服务、可用账号/注册权限、是否接受独立后端、同步数据范围和运维/费用。先做只读能力发现与可审阅设计，外部账号/部署写入在具体结果就绪后单独确认；完成模拟与获准真实服务/跨端恢复验收。

##### R23：当前版本原生界面与可访问性

- App 继续共享控件/集中样式/本地化，列每个关键入口的加载/空/忙/禁用/失败/恢复和键盘路径；按实际最小尺寸、常见尺寸、最大化、明暗主题及多 DPI 组合验收，检查焦点、弹层、滚动、长路径/长文本和屏幕阅读器语义。
- 在独立测试数据和独立 EXE 窗口完成对话/Queue/Steer、附件、Git/工作树、设置、审批、浏览器、子代理、定时和 PR 端到端操作，记录 App/依赖身份及数据/请求断言；不操作或关闭用户现有窗口，不为通过而放宽审批。
- 原生 resize/DPI 诊断先区分工具宿主、操作系统消息和产品布局问题；截图黑帧/输入访问拒绝就记录限制，恢复可用测试环境后补验。截图用于遮挡、裁切、对齐和可读性，像素接近不作为验收门槛。
- 组件/Headless 测试先验证逻辑和布局，原生测试再验证实际窗口/焦点/输入；真实账号请求另列，三种证据不能互换。已发现产品问题才做针对修复和回归，不用调整测试断言掩盖交互失败。

##### R24：JIT 单文件 Release 发布前验收

- 保持现有 Windows x64、自包含、单文件、无 AOT/无裁剪；用 `scripts/publish.ps1` 的 staging/验证/替换流程，不直接覆盖已有包。当前用户只要求本地提交，本次不制作或发布新版本。
- 功能与安全阻断关闭后，在固定提交的独立候选运行相关测试、完整 Debug/Release、结构/依赖/共享组件检查，核对源码与二进制身份，保留全部原始失败和修复后的新证据。
- 在干净 Windows 环境检查 EXE/ZIP 内容、版本、签名方案、解包目录权限、首次启动、已有数据库逐版本迁移、WebView2/Playwright/ConPTY/native依赖、离线/代理、正常退出/崩溃和手动恢复；此前包的可恢复性也要验证。
- 只有约定范围、关键原生工作流和真实依赖验收通过，且不存在阻断，才能标整个目标完成。发布到外部平台、推送或安装仍需用户明确要求；当前交付不作已完成或发布可用声明。

#### 五、下一轮开始位置

先从 R01 的受支持隔离后端与验收环境决策继续，复用既有诊断证据，不重复 KnownDll ACL
实验。并行性在设计上按任务隔离落实，不复用一次性管理员许可。之后逐项关闭 P1 的真实
恢复/并发/原生缺口，再推进 P2/P3。每关闭一项同步更新本表和功能矩阵，附实际入口、
运行数据与证据边界；无法完成的外部依赖保持明确的未完成状态。
<!-- /source: DELIVERY_STATUS_AND_ROADMAP.md -->


---

<a id="functional-parity-matrix"></a>

## FUNCTIONAL_PARITY_MATRIX.md

<!-- source: FUNCTIONAL_PARITY_MATRIX.md -->
### 功能与工作流对齐清单

2026-10-20 阶段收尾的累计实现、当前验证边界和逐项后续方案见
[阶段交付清单与后续实施方案](OVERVIEW.md#delivery-status-and-roadmap)。
该文档不将未关闭的命令隔离阻断或其他缺口标为完成；以下专项历史记录继续保留。

2026-10-20 后续增量：生产文件工具的打开后句柄校验、可控 junction 竞态复现和
证据边界见 [READ_HANDLE_PATH_SAFETY_ACCEPTANCE.md](SECURITY.md#read-handle-path-safety-acceptance)。
后续原子文本写入的目录句柄租约、相对创建/提交及失败清理见
[WRITE_HANDLE_PATH_SAFETY_ACCEPTANCE.md](SECURITY.md#write-handle-path-safety-acceptance)。
2026-10-21 删除与撤销增量见
[DELETE_HANDLE_PATH_SAFETY_ACCEPTANCE.md](SECURITY.md#delete-handle-path-safety-acceptance)：普通删除、
递归目录及撤销新建文件接入相对句柄删除、有界预检和部分完成反馈。
复制文件/目录树及显式目录创建的目标替换反例、源树预检和父目录租约见
[COPY_HANDLE_PATH_SAFETY_ACCEPTANCE.md](SECURITY.md#copy-handle-path-safety-acceptance)。
同卷文件/目录移动的句柄会话与跨卷限制见
[MOVE_HANDLE_PATH_SAFETY_ACCEPTANCE.md](SECURITY.md#move-handle-path-safety-acceptance)。
任务产物读取和文件预览产物原子写入的增量见
[ARTIFACT_HANDLE_PATH_SAFETY_ACCEPTANCE.md](SECURITY.md#artifact-handle-path-safety-acceptance)，
编辑 before/after 快照、预览和最终审阅补丁已共用明确 Complete/Retain 的产物会话；
项目提交或最终检查失败时按原句柄清理，复制后 hash、真实清理拒绝和并发隔离见
[OWNED_ARTIFACT_SESSION_ACCEPTANCE.md](SECURITY.md#owned-artifact-session-acceptance)。
浏览器截图和下载也已接入该会话，最终保留前清理暂存并复核工作区/取消；
SDK 原对象清理、32 MiB 导出限制、失败反馈及各层验证范围见
[BROWSER_ARTIFACT_SESSION_ACCEPTANCE.md](BROWSER_MCP.md#browser-artifact-session-acceptance)。
实际 Chrome 超时场景可报告 SDK 缓存清理未确认；不等于缓存配额或崩溃回收完成。
Git diff/numstat 的临时输入已增加生产 Windows 专用会话：固定目录和文件句柄、
只共享读取、Git 终止及输出读取后按原句柄清理；真实 Git、生产 App CLI 和
Headless 的证据及后续方案见
[GIT_DIFF_INPUT_SESSION_ACCEPTANCE.md](GIT_WORKTREES.md#git-diff-input-session-acceptance)。
项目授权持久版本（schema 26）、保存冲突保护及 Agent/工具/长驻终端的撤销租约已继续实现，
新建/编辑保存返回自己的事务快照；跨窗口抢先保存不会被旧草稿借用版本。当前切片的
Debug/Release 完整构建通过，各 2,506 项分层相关回归通过（基础完整套件 2,196 + App
选择 310），2,641 项输入在配置内及配置间无漂移。旧失败记录保留，不声称全量或实机验收。
生产保存、队列暂停、并发草稿、真实 ConPTY 退出及 Headless 恢复的证据和剩余方案见
[PROJECT_AUTHORIZATION_REVOCATION_ACCEPTANCE.md](SECURITY.md#project-authorization-revocation-acceptance)。
以上是 schema 26 检查点。后续 schema 27 的项目主根/附加根对象身份、旧项目待确认、
明确重新授权及持续根句柄已接入生产；具体链路、当前验证和剩余方案见
[ROOT_OBJECT_AUTHORIZATION_ACCEPTANCE.md](SECURITY.md#root-object-authorization-acceptance)。
该切片最终 Debug/Release 完整构建均零警告/错误，各 713 项定向回归通过
（完整 Windows 422、App 精选 291），两配置与当前 2,670 项输入一致；先前 R2 Release
的 PTY 状态失败、受控反例和 R3 旧测试快照失败均保留在专项文档，不标为全量或原生验收。
以上是 schema 27 检查点；后续 schema 28 已增加工作树自身的不可变对象 grant、旧任务明确
确认和 Agent/工具/PTY 的组合租约。恢复会拒绝同路径替换，即使 Git 注册文件被复制。
10 项持久化/迁移/取消专项及 8 项生产 App 专项通过。最终 Debug/Release 完整解决方案
构建均零警告/错误，各 3,558 项相关回归通过（七套完整 + App 精选 414），
两配置与当前 2,688 项输入一致；首轮 App 全套的 28 项失败和手动中止记录保留，
最终逐名验证修复，未再次完整执行 App 全套。证据和范围见
[WORKTREE_OBJECT_AUTHORIZATION_ACCEPTANCE.md](GIT_WORKTREES.md#worktree-object-authorization-acceptance)。
创建认领/清理最终交接、替换对象重新授权、Git 进程隔离及崩溃发布/回收仍未完成。
这些增量只推进明确入口；最终写入目录项替换竞争、其他文件操作、严格即时撤销屏障和 R01 仍未完成。
R01 已由用户选定原生 MXC + 受支持独立 Windows 验收环境；环境尚未就绪，先推进本地修复。

#### 当前目标（2026-10-19）

MossAgent 的目标改为还原 Codex Desktop 的功能和主要工作流，不要求界面一比一。
功能完整性、真实可用性、安全性和稳定性优先；共享组件、清晰反馈、键盘可用性、
明暗主题和窗口适配仍需保证。截图用于发现功能性界面问题，不以像素相等验收。
Windows、Avalonia、.NET 10、JIT 和 Windows x64 单文件发布决策不变。

旧的 [产品验收记录](OVERVIEW.md#product-acceptance-matrix) 与各专项文档保留原始证据。
其中尚未完成的视觉相似度对照不再阻断产品完成，但测试失败、原生交互未验证和
功能链路缺口不会因此自动变为通过。

#### 参考范围与状态定义

- 2026-10-19 重新读取官方 [功能目录](https://developers.openai.com/codex/app/features)，包含项目、
  对话、定时任务、长任务、浏览器、插件、图像输入和其他入口。
- 该目录同时包含 ChatGPT 相关产品能力；没有逐项核验的账号、云端及跨产品功能
  登记为待核实，不擅自排除，也不声称本地实现能够替代其专有服务。
- 本次未操作现有 Codex 或 MossAgent 用户窗口；公开目录不是当前 Windows 客户端
  的完整实机入口清单。此前延后的原生截图仍未执行。
- “已实现”只描述明确范围内已有代码和贯通证据；“部分实现”表示仍有子能力缺失；
  “未实现”表示当前审阅范围没有对应运行链路；“未验证”表示仍需当前候选的证据。
- 历史测试只作为定位线索，不视为本次候选的全量验证；本轮结果见
  [附件选择隔离验收](CONVERSATIONS.md#attachment-selection-isolation-acceptance)。

#### 核心能力盘点

原仓库已合入 schema 24 工具中断审计恢复；对应 Debug/Release 完整构建和专项检查通过。
基准选择合入前的完整 Debug 回归 54927 已终态，8,153 项全部通过，无跳过或中止；八份完整报告及
2,421 份源码/配置、2,035 份运行文件的前后路径/hash 已核对一致，冻结已解除。已提交网页副作用后宿主终止的
Debug/Release 实际 Chrome 恢复检查通过，模型服务是本地模拟，原生界面仍未验收。

[WORKTREE_BASE_REFERENCE_ACCEPTANCE.md](GIT_WORKTREES.md#worktree-base-reference-acceptance) 记录手动分支/标签/提交选择的 22 路径受控合入；
原仓库 Debug/Release 完整构建零警告/错误，各基础设施 743、相关 App 109 项通过；
独立 CLI 的两种配置实际并发运行/跨工作树读取拒绝/独立取消及草稿历史恢复检查通过，
另有同步双工作树创建的服务级正向证据，取消/崩溃/部分失败组合仍待验，
使用真实 Git/Agent/工具管线/SQLite 和本地模拟模型，未验收原生 GUI。
完整 Debug 回归 14404 已终态：8,182 通过、1 失败，无跳过或中止；八份报告及
2,432 份源码/配置、2,429 份运行文件前后路径/hash 一致，冻结已解除。
失败为新增基准输入所在弹层仍声明按钮点击关闭，违反已有多步输入约束。
本次移除这一处配置，保留原 guard 并补充两种环境选择的真实键盘关闭/重开保值检查；
新构建和验证见 [WORKTREE_BASE_REFERENCE_ACCEPTANCE.md](GIT_WORKTREES.md#worktree-base-reference-acceptance)，旧失败报告保留。旧统计审计失败
的分类状态不变。修复后的原仓库完整 Debug 回归 26906 已终态并逐份审查：八个程序集
8,183 项全部通过，无失败、跳过或中止；2,432 份源码/配置和 2,429 份运行文件的
前后路径/hash 一致，冻结已解除。此回归不包含独立的 Steer 候选。旧统计审计失败
缺少错误码，仍未分类；另外的受控反例证明旧观察条件错误，修正后的观察检查所有
审计并要求最终查询成功，也会拒绝注入的当前查询失败，不据后续通过追认历史原因。
命令仍可读取授权根外文件，是当前安全
阻断；一次管理员 ACL 诊断许可已消费，另一次新授权的只读管理员预检也已执行。
预检确认真实提权后 44 个对象仍全部拒绝 WRITE_DAC；普通权限原生查询读出 WinTcb-Light
信任标签，临时修改 KnownDll 系统 ACL 的实验路线已否决，未修改系统保护或生产启动器。
[WINDOWS_ISOLATION_BACKEND_DECISION.md](SECURITY.md#windows-isolation-backend-decision) 补充公开 MXC .NET SDK 的实际普通权限探测：
本机 23H2 仅提供 AppContainerDacl 回退层，原生 BaseContainer 不可用，未执行任何系统
准备或命令。用户已选择原生 MXC 独立验收环境，尚未就绪；不自动改选其他后端。
不能以 IsSupported=true、Linux 容器或单实例 Windows Sandbox 宣称修复。

| 能力 | 当前状态、入口和链路 | 证据与下一步 |
| --- | --- | --- |
| 项目及无项目聊天 | 已有实现：`WorkspaceCatalogViewModel` → `WorkspaceTaskFactory` → Agent；无项目可明确选择文本/静态图片，但不授予目录或工具权限 | `ProjectlessChat*Tests`、[PROJECTLESS_ATTACHMENTS_ACCEPTANCE.md](CONVERSATIONS.md#projectless-attachments-acceptance)；已覆盖真实文件/SQLite/loopback，原生交互仍待验 |
| 流式、多轮、任务切换、并发及取消 | 已有实现；发送准备新增独立取消、保留草稿和提交后安全终结，完整当前版本验收仍未完成：`WorkspaceViewModel` → run controller/executor → provider | `WorkspaceConversationFlowTests`、`WorkspaceCancellationFlowTests`、`WorkspaceHistoryContextTests`、[SEND_PREPARATION_CANCELLATION_ACCEPTANCE.md](CONVERSATIONS.md#send-preparation-cancellation-acceptance)；需要当前全套与原生交互检查 |
| 草稿按对话隔离及恢复 | 已补齐文本、未发送 UTF-8 和静态图像附件的作用域、导航恢复、持久化和共同版本冲突处理；前台消息提交与草稿消费/提升现共用 SQLite 事务，序列化后台保存并保留提交期间的新输入；完整原生交互仍待验 | 原记录保留；新增 [CONVERSATION_SUBMISSION_ACCEPTANCE.md](CONVERSATIONS.md#conversation-submission-acceptance)：四种组合在修复前复现旧草稿，修复后贯通真实 SQLite/模型 loopback，覆盖冲突、回滚、取消及恢复。当前完整 Debug 回归 8,021 项通过，Debug/Release 各 32 项专项通过。四项 CLI 提交确认后强制终止恢复通过；另有同 hash 原生 EXE 的无项目发送、流式请求和终止/重启恢复检查通过，消息只存一次且不自动续跑，见 [NATIVE_SUBMISSION_ACCEPTANCE.md](CONVERSATIONS.md#native-submission-acceptance)。黑帧和原生输入访问拒绝使草稿续写/停止/视觉验收未完成；提交瞬间崩溃仍未验收。文件快照/Git 不属于 SQLite 事务 |
| 项目及授权目录 | 已有编辑/保存/工具授权链路；schema 26 条件保存及版本租约撤销旧运行，schema 27 绑定主根/附加根的卷与文件对象身份。旧项目必须明确确认，同路径替换与普通改名不能获得授权；确认后保留当前任务和草稿，手动新运行取得新授权 | [PROJECT_AUTHORIZATION_UI_ACCEPTANCE.md](SECURITY.md#project-authorization-ui-acceptance)、[PROJECT_AUTHORIZATION_REVOCATION_ACCEPTANCE.md](SECURITY.md#project-authorization-revocation-acceptance)、[ROOT_OBJECT_AUTHORIZATION_ACCEPTANCE.md](SECURITY.md#root-object-authorization-acceptance)；含真实 SQLite/文件/ConPTY、loopback 和 Headless。工作树对象身份和旧任务明确确认的后续链路见 [WORKTREE_OBJECT_AUTHORIZATION_ACCEPTANCE.md](GIT_WORKTREES.md#worktree-object-authorization-acceptance)；同步撤销屏障、原生选择和完整恢复仍未完成或未验证 |
| 文件搜索、读取、写入及编辑 | 已有工具和标准管线实现；读前最终句柄守卫覆盖读取/预览/编辑校验/快照/复制源；原子文本写入增加本地 NTFS 目录租约、独占暂存及相对句柄提交。最终目录项竞争、其他写入入口、即时撤权及完整原生交互仍未完成 | [READ_HANDLE_PATH_SAFETY_ACCEPTANCE.md](SECURITY.md#read-handle-path-safety-acceptance)、[WRITE_HANDLE_PATH_SAFETY_ACCEPTANCE.md](SECURITY.md#write-handle-path-safety-acceptance) 分别记录范围和当前证据；历史 [FILE_EDITING_ACCEPTANCE.md](CONVERSATIONS.md#file-editing-acceptance)、[FILE_SNAPSHOT_SHARING_ACCEPTANCE.md](GIT_WORKTREES.md#file-snapshot-sharing-acceptance) 不代表全部授权边界已关闭 |
| 终端与命令执行 | 部分实现且存在已确认安全阻断：已有 ConPTY、任务所属进程及取消能力，但命令只验证工作目录，普通用户令牌仍能访问授权根外文件；完整原生交互未验证 | [COMMAND_FILESYSTEM_ISOLATION_PLAN.md](SECURITY.md#command-filesystem-isolation-plan) 记录生产 DI/统一管线/临时标记的越界复现及 AppContainer 实验。文件工具拒绝同一路径，ReadOnly/禁用工具也正确拒绝，不能据此宣称命令文件隔离。优先完成真实沙箱及 pwsh/终端/Git 兼容验证；旧 [TERMINAL_INTERACTION_ACCEPTANCE.md](TERMINAL_TOOLS.md#terminal-interaction-acceptance)、[COMMAND_EXECUTION_ACCEPTANCE.md](TERMINAL_TOOLS.md#command-execution-acceptance) 保留原范围 |
| Git、差异、审阅及撤销 | 部分实现：状态/diff/log/worktree 工具和变更审阅、撤销已有链路；侧栏支持所选文件暂存/取消暂存、已暂存内容确认提交和单分支预览确认推送，均经过真实 Git 与统一审批审计管线 | [DIFF_REVIEW_ACCEPTANCE.md](GIT_WORKTREES.md#diff-review-acceptance)、[FILE_CHANGE_UNDO_ACCEPTANCE.md](GIT_WORKTREES.md#file-change-undo-acceptance)、[GIT_STAGING_ACCEPTANCE.md](GIT_WORKTREES.md#git-staging-acceptance)、[GIT_COMMIT_ACCEPTANCE.md](GIT_WORKTREES.md#git-commit-acceptance)、[GIT_PUSH_ACCEPTANCE.md](GIT_WORKTREES.md#git-push-acceptance)；推送冻结候选六组扩大回归 3,607/3,607 通过，完整构建零警告/错误，源码/运行文件/ZIP 身份及路径集合一致；真实 Inspector 写入临时 bare 仓库并保留未提交内容与草稿。不视为完整或原生验收，后续改动另行验证。完整 PR 工作流、整组/区块暂存和远端配置/凭据界面仍未实现，不自动执行远端变更 |
| 工作树 | 部分实现：项目新对话可显式选择独立工作树，支持手动指定本地分支/标签/提交或默认 HEAD，以解析后的固定提交创建，并贯通连续对话、持久化重开、工具授权和确认清理；准备支持取消并保留归属不明或创建失败的目录供检查。创建/清理校验 GUID 子目录、授权范围、链接祖先和 Git 元数据；恢复任务增加逐操作守卫，清理必须匹配任务归属；schema 28 继续绑定工作树对象身份并提供旧任务明确确认及持续租约 | 基准选择及两配置 CLI 并发运行、跨工作树读取拒绝、独立取消和草稿/历史恢复，以及同步并行创建服务级正向检查见 [WORKTREE_BASE_REFERENCE_ACCEPTANCE.md](GIT_WORKTREES.md#worktree-base-reference-acceptance)；原仓库该切片完整 Debug 回归已通过 8,183 项，含 Steer 的冻结候选两配置各通过 8,282 项。[WORKTREE_CONVERSATION_ACCEPTANCE.md](GIT_WORKTREES.md#worktree-conversation-acceptance) 保留历史结果；取消及失败恢复见 [SEND_PREPARATION_CANCELLATION_ACCEPTANCE.md](CONVERSATIONS.md#send-preparation-cancellation-acceptance)；路径边界见 [WORKTREE_PATH_SAFETY_ACCEPTANCE.md](GIT_WORKTREES.md#worktree-path-safety-acceptance)，恢复后工具/侧栏/子代理和清理授权见 [RESTORED_WORKTREE_AUTHORIZATION_ACCEPTANCE.md](GIT_WORKTREES.md#restored-worktree-authorization-acceptance)。对象授权增量、生产入口及当前分层证据见 [WORKTREE_OBJECT_AUTHORIZATION_ACCEPTANCE.md](GIT_WORKTREES.md#worktree-object-authorization-acceptance)。自动分支发现、工作树创建/清理的原生句柄级竞态防护、授权撤销的严格排空屏障、创建中崩溃恢复、并行创建的取消/部分失败组合及原生重启仍未完成或未验证 |
| 提供商、模型与代理 | 已有设置到运行的链路，仍有协议/恢复组合未验证 | [PROVIDER_UI_WORKFLOW_ACCEPTANCE.md](PROVIDERS.md#provider-ui-workflow-acceptance)、[AUTHENTICATED_PROVIDER_PROXY_ACCEPTANCE.md](PROVIDERS.md#authenticated-provider-proxy-acceptance)；验证实际请求参数、直连/默认代理、不替换失效模型 |
| 思考强度及模型能力 | 部分实现：可选/自定义参数和能力数据已有实现；Agent 和四种协议校验图像能力，编辑后的图像能力已贯通草稿禁发/切换恢复及真实 loopback 请求 | [MODEL_REASONING_CAPABILITY_ACCEPTANCE.md](PROVIDERS.md#model-reasoning-capability-acceptance)、[IMAGE_ATTACHMENT_WORKFLOW_ACCEPTANCE.md](CONVERSATIONS.md#image-attachment-workflow-acceptance)；继续核验真实提供商的格式支持、图像理解及 PDF 等文件能力，不能只以能力声明验收 |
| 文本附件 | 已补齐项目/无项目显式选择、已发送快照/历史及未发送 UTF-8 附件草稿恢复；完整附件能力仍部分实现 | [ATTACHMENT_HISTORY_ACCEPTANCE.md](CONVERSATIONS.md#attachment-history-acceptance)、[ATTACHMENT_DRAFT_ACCEPTANCE.md](CONVERSATIONS.md#attachment-draft-acceptance)、[PROJECTLESS_ATTACHMENTS_ACCEPTANCE.md](CONVERSATIONS.md#projectless-attachments-acceptance)；原文件变化不改快照，缺失/篡改拒绝请求。原生 EXE 重启、PDF及安全回收未引用快照仍需完成 |
| 图像、PDF及原生文件输入 | 部分实现：真实静态图片解码、项目/无项目选择、快照、草稿/历史恢复和原生图片请求块已有链路；输入区、草稿和已发送/历史消息已有后台限额缩略图，JPEG EXIF 八方向已校正 | [IMAGE_ATTACHMENT_WORKFLOW_ACCEPTANCE.md](CONVERSATIONS.md#image-attachment-workflow-acceptance)、[PROJECTLESS_ATTACHMENTS_ACCEPTANCE.md](CONVERSATIONS.md#projectless-attachments-acceptance)、[ATTACHMENT_THUMBNAIL_ACCEPTANCE.md](CONVERSATIONS.md#attachment-thumbnail-acceptance)、[IMAGE_ORIENTATION_ACCEPTANCE.md](CONVERSATIONS.md#image-orientation-acceptance) 记录分阶段结果；PDF、全图查看、其他格式/实机图片方向、子代理图片共享、原生选择/EXE 重启和真实提供商理解仍未完成 |
| 工具审批、事件、结果及产物 | 已有标准执行管线和共享审批 UI；新增修正待处理标志先于请求 ID 发布的竞态，匹配身份和策略不变；完整当前交互审计未完成 | [TOOL_SCHEMA_ACCEPTANCE.md](TERMINAL_TOOLS.md#tool-schema-acceptance)、[GLOBAL_APPROVAL_PRESENTATION_ACCEPTANCE.md](TERMINAL_TOOLS.md#global-approval-presentation-acceptance)；[GIT_DIFF_INPUT_SESSION_ACCEPTANCE.md](GIT_WORKTREES.md#git-diff-input-session-acceptance) 记录确定性即时批准/拒绝及修复前后证据。持续验证身份隔离、拒绝、取消、超时、输出和审计 |
| 浏览器 | 部分实现：内嵌、外部、显式 CDP、任务标签页、截图及接管已有链路；已修复工具超时返回后旧点击仍继续执行的问题 | [CDP_AGENT_WORKFLOW_ACCEPTANCE.md](BROWSER_MCP.md#cdp-agent-workflow-acceptance)、[BROWSER_SESSION_OWNERSHIP_RUNTIME_ACCEPTANCE.md](BROWSER_MCP.md#browser-session-ownership-runtime-acceptance) 保留原范围。[BROWSER_OPERATION_CANCELLATION_ACCEPTANCE.md](BROWSER_MCP.md#browser-operation-cancellation-acceptance) 记录生产 DI/审批/审计/真实 Chrome 的晚执行复现及原仓库修复复验：取消后清理任务资源并拒绝旧会话，显式重新附着后新审批可恢复；[QUEUE_BROWSER_RUNTIME_ACCEPTANCE.md](BROWSER_MCP.md#queue-browser-runtime-acceptance) 贯通 Queue/Agent、另一任务审批/草稿保留和服务重建后手动继续。另有 [BROWSER_CREATION_CANCELLATION_ACCEPTANCE.md](BROWSER_MCP.md#browser-creation-cancellation-acceptance) 的资料目录创建边界取消及 [BROWSER_PROCESS_RECOVERY_ACCEPTANCE.md](BROWSER_MCP.md#browser-process-recovery-acceptance) 的工具完成后宿主强制终止/进程重启证据，均保留另一任务或实例并要求显式恢复；另有 [AUDIT_INTERRUPTION_RECOVERY_ACCEPTANCE.md](TERMINAL_TOOLS.md#audit-interruption-recovery-acceptance) 的 Agent/Queue 执行中导航崩溃恢复，以及 [BROWSER_COMMITTED_EFFECT_RECOVERY_ACCEPTANCE.md](BROWSER_MCP.md#browser-committed-effect-recovery-acceptance) 中原仓库 Debug/Release 已提交 POST 副作用但点击结果未返回的宿主终止/手动续跑检查：副作用保持一次，审计保留未知结果，另一实例和草稿/队列保留。原生 GUI、其他创建/副作用时序、内外切换及人工接管仍待验；借用 context 的 cookie/storage 仍共享，不能声称浏览器配置完全隔离 |
| 浏览器截图与下载产物 | 已实现生产 Windows 受控导出会话：`BrowserScreenshotTool` / `BrowserDownloadTool` → 统一执行管线 → 任务 SDK 会话 → 原生产物暂存/最终会话 → hash/保留 → 审计；超时和清理未知不会返回成功产物。完整资源配额/恢复仍部分实现 | [BROWSER_ARTIFACT_SESSION_ACCEPTANCE.md](BROWSER_MCP.md#browser-artifact-session-acceptance) 区分原生临时文件、SDK 代理、模拟模型 + Headless App 和实际 Chrome CLI；已覆盖拒绝、取消/超时、并发和重载。下一步处理网络/缓存配额、SDK 清理未知的持久状态、崩溃阶段及引用回收；原生 GUI、真实提供商和其他浏览器组合仍待验 |
| Git diff/numstat 临时输入 | 已实现生产 Windows 受控输入会话：`FileChangeReviewTool` / `FileChangeStatisticsTool` → 统一管线 → `FileChangeDiffRunner` → 原生工厂 → 实际 Git → 最终复核 → 原句柄清理；4 MiB 上限、外部写入/替换拒绝，注入失败不回退 | [GIT_DIFF_INPUT_SESSION_ACCEPTANCE.md](GIT_WORKTREES.md#git-diff-input-session-acceptance)：37 项 Windows 专项、3 项模拟模型 + Headless App 专项及生产 App CLI；取消/超时的受控进程端口与真实 Git 正常流程分开标注。兼容宿主旧分支不具有新保证。项目主根/附加根的后续身份登记见 [ROOT_OBJECT_AUTHORIZATION_ACCEPTANCE.md](SECURITY.md#root-object-authorization-acceptance)；工作树自身身份的后续链路见 [WORKTREE_OBJECT_AUTHORIZATION_ACCEPTANCE.md](GIT_WORKTREES.md#worktree-object-authorization-acceptance)；下一步处理宿主崩溃、创建/清理交接、严格撤销和环境组合；Git 进程任意文件访问仍由 R01/MXC 解决 |
| MCP | 部分实现：stdio/HTTP、发现、执行、显式重连已有链路 | [MCP_UI_RECOVERY_ACCEPTANCE.md](BROWSER_MCP.md#mcp-ui-recovery-acceptance)；传输恢复、认证及配置竞争仍需逐项盘点，不能将显式重连等同自动续传 |
| 自定义代理及子代理 | 已有保存、模型/强度/上下文、调用及对话展示链路，部分原生审批分支未验证 | [SUBAGENT_SETTINGS_TO_RUNTIME_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#subagent-settings-to-runtime-acceptance)、[SUBAGENT_MANUAL_APPROVAL_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#subagent-manual-approval-acceptance)；核验触发到真实请求、工具审批、取消、并发及恢复 |
| 定时任务 | 已有编辑、启停、立即运行、停止及历史链路；关闭/恢复分支需当前回归 | [AUTOMATION_UI_WORKFLOW_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#automation-ui-workflow-acceptance)、`AutomationScheduler`；应用启动跳过过期触发、标记中断，不自动补跑副作用 |
| 设置及会话管理 | 已有共享设置、持久化和中断恢复；完整入口行为仍未验证 | [PRODUCT_INTERACTION_AUDIT.md](OVERVIEW.md#product-interaction-audit) 的结构/连线扫描不是所有动作实际运行，需逐条验收 |

#### 其他功能，不以隐藏入口代替实现

文本 Steer 已从生产配置根贯通共享按钮/活动 Ctrl+Enter、草稿原子消费、同一运行
边界注入及持久记录，并受控合入原仓库 96 个路径（56 新增、40 修改、无删除），
保留原文件备份。实际接收/应用在 App 与 SQLite 事务中复核项目、grant、账号版本
和绑定；该历史候选仅空引导边界允许追加项目目录，原运行工具根不扩张。当前 schema 26 已改为任何目录变更撤销旧运行，包括空边界。原有四项授权流程
断言完整保留。长草稿、Queue 和引导记录并存的滚动/键盘路径有两种尺寸与主题的
Headless 证据。关闭期间排队等待和迟到读取错误的反例及修复均保留。
准确的 r4 Debug/Release 全量现已各 8,282 项全部通过，每组八份完整报告、零失败/
跳过/中止，2,490 项清单输入和 2,429 份运行文件前后无漂移。收尾分类确认输入含
2,455 份源码/配置/脚本及 35 份旧的忽略输出，原始清单保留。原仓库两配置完整构建
零警告/错误，各 147 项专项通过（App 97、基础设施 45、Agent 5），输入路径/hash
与候选一致且测试前后不变。首个候选 18 项失败及六组中止旧回归不追认为通过。
候选和原仓库各配置四种 CLI 进程终止恢复场景均通过，使用生产 DI/SQLite 和本地
模拟 HTTP；原仓库实际加载的四个生产程序集 hash 已复核。
详见 [引导验收](CONVERSATIONS.md#conversation-steering-acceptance)。原生/真实提供商、副作用与
事务中途崩溃、附件引导及默认/替代行为配置仍有缺口，完整 Steer 与产品均未完成。

| 能力 | 当前状态 | 实现依赖及下一步 |
| --- | --- | --- |
| Pull Request | 部分实现：独立账号、仓库只读授权、标准管线、开放 PR/描述及版本绑定的文件补丁、讨论/行内评论、检查运行/传统状态已有页面链路；当前提交、精确路径和完整补丁有效行支持差异内评论及键盘定位。同版本固定快照审阅、失败检查调查草稿和完整快照预算保护保留；另增显式独立只读查询对话，模型只可查询绑定 PR/base/head 的描述、文件、评论和检查，无本地工具或远程写权限。可选择账号并从 PR 链接直接打开已授权仓库的当前详情（包括已关闭 PR），接续原有检查与对话，不隐式授权或查询列表 | 旧冻结证据保留。查询对话切片见 [SOURCE_PR_ON_DEMAND_READING_ACCEPTANCE.md](GIT_WORKTREES.md#source-pr-on-demand-reading-acceptance)：schema 22 迁移保留快照模式，查询结果贯通模型/工具/HTTP/审计/界面/历史，撤权与版本变化不返回后续数据，取消后不自动重放。链接切片及其域名/端口映射、取消清理、错误恢复和候选验证见 [SOURCE_PR_URL_ACCEPTANCE.md](GIT_WORKTREES.md#source-pr-url-acceptance)。重开仍保留首次快照，不是完整或原子实时 PR；个人收件箱、reviews/activity、版本标记和确认写操作继续实现。OAuth、GitLab、云审阅和真实账号/原生验收仍是缺口；[PULL_REQUEST_WORKFLOW_PLAN.md](GIT_WORKTREES.md#pull-request-workflow-plan) 保留完整范围 |
| 插件、技能或扩展 | 未实现完整目录、安装、启停和执行链路；已核实便携包、兼容清单及官方目录/账号边界 | [PLUGIN_WORKFLOW_PLAN.md](OVERVIEW.md#plugin-workflow-plan) 记录官方包格式、整体 overlay 优先级、hooks 独立信任和本地/市场/账号各链路。App Server 的 plugin list/read/install/uninstall 明确仍在开发且禁止生产客户端调用，不能借此冒充目录接入；安装代码/MCP/技能仍需真实运行与统一安全边界 |
| 运行中追加、排队或引导消息 | 部分实现：Queue 和文本 Steer 已接入生产 DI、共享控件与真实运行；Enter 入队、活动 Ctrl+Enter 引导同一运行，独立停止按钮保留；附件引导及默认/替代行为配置未实现 | schema 23/25 分别负责 Queue 原子消费/分派和引导接收/应用；两者保留保存的模型、强度、代理提示词和原授权范围。编辑/排序/移除/继续、失败/取消/并发/恢复及拒绝路径见 [CONVERSATION_FOLLOWUP_RUNTIME_ACCEPTANCE.md](CONVERSATIONS.md#conversation-followup-runtime-acceptance) 和 [CONVERSATION_STEERING_ACCEPTANCE.md](CONVERSATIONS.md#conversation-steering-acceptance)。本地模拟 HTTP、Headless 和受控 CLI 进程恢复分开记录；原生及真实提供商、分派竞态和工具副作用中途崩溃仍需验收 |
| 桌面操作 | 部分实现：有桌面截图；未发现完整指针/键盘操作工具链路 | 敏感桌面访问需审批、目标窗口绑定、任务隔离及取消；浏览器 DOM 操作不能替代桌面控制 |
| Web 搜索、图像生成、语音及输入预测 | 未实现对应专用链路，具体参考客户端范围未验证 | 区分 provider 原生功能、MCP/插件和本地实现，明确服务、凭据、费用和能力协商后推进 |
| 通知及长任务目标 | 当前有运行任务和定时任务；专用系统通知、持续目标/预算链路未验证 | 盘点实际入口；先定义生命周期、通知权限、人工暂停和中断恢复，不以持续循环代替可靠长任务 |
| 交互式可视化及生成文件预览 | 已有文本、代码、差异和图像缩略图；官方 Visualizations、Work with files 所含完整交互/文档预览范围未验证 | 按文件类型核实生成、产物归属、可信预览、保存/导出及权限；不把代码预览视为完整文档或交互内容支持 |
| Appshots、浏览器扩展及人工接管 | 有任务浏览器和截图基础；官方命名入口的快照内容、扩展协议及接管范围未验证 | 分别核实平台支持、显式授权、窗口/标签页绑定、数据最小化及失败恢复；当前 CDP 支持不能证明兼容官方扩展 |
| 宠物及 Codex Micro 外设 | 当前审阅未发现对应运行链路；官方功能目录列有入口，Windows 客户端实际可用范围未验证 | 分别核实宠物动画/活动交互、外设协议及系统依赖，作为独立缺口登记，不能由静态图标代替 |
| Sites、dots、Space 及 Sign in with ChatGPT | 官方功能目录包含的跨产品/云端入口；MossAgent 对应服务接入和账号授权未实现或未验证 | 分别核实站点构建/发布/协作、持续责任代理、共享页面和账号计划授权的官方依赖及开放接口；本地项目、定时任务或模型密钥不视为这些服务的替代完成 |
| 账号/远程/云端、移动端及跨产品能力 | 未验证；本地实现不代表兼容专有服务 | 按用户目标继续评估依赖和可实现路径，需要范围确认时列出明确选项，不自行标记“不需要”或“已完成” |

#### 推进顺序与验收

当前最高优先级为已受控复现的命令根目录越界；当前发送切片的权限 UI 等待竞态已修复并完成下述回归。
[COMMAND_FILESYSTEM_ISOLATION_PLAN.md](SECURITY.md#command-filesystem-isolation-plan) 记录隔离设计前置条件、实验通过和失败的边界，
并优先于 [CONVERSATION_FOLLOWUP_PLAN.md](CONVERSATIONS.md#conversation-followup-plan) 中尚未完成的运行隔离验收和 Steer。不能因旧回归通过
或工作目录已校验而将命令文件安全边界标为完成。

队列存储基础候选 `conversation-followup-storage-20261019` 已终态：8,053 通过、
1 项 PID 文件占用失败，共 8,054 项；2,379 份源码/配置和 2,035 份 Debug
运行文件 hash 前后一致。PID 就绪协议修复已合入测试辅助文件，原仓库
Debug/Release Windows 项目各 147 项通过，见 [PTY_PID_PUBLICATION_DIAGNOSTIC.md](TERMINAL_TOOLS.md#pty-pid-publication-diagnostic)。

当前 Queue 运行候选 `conversation-followup-runtime-20261019` 已接入生产 DI 和
真实续发；Debug/Release 完整构建均零警告/错误，Release 的 23 项 Queue、
607 项 Providers 和 147 项 Windows 检查通过。完整 Debug 回归 session 14700
已经终态：8,085 项中 8,070 通过、15 失败、无跳过；14 项旧视觉选择器和
1 项附件导航等待超时的原始失败记录保留。2,401 份源码/配置和 2,035 份
Debug 运行文件的路径与哈希前后一致。解除冻结后，10 个断流修复、队列审批及
测试辅助文件已按基线校验合入，未碰原 Windows PID 修复。原仓库 Debug/Release
完整构建零警告/错误，两种配置各 App 38、Providers 617、Debug Visual 300
及 4 个 CLI 强制终止恢复场景通过。完整 Debug 回归 session 97039 已终态：
8,102 项中 8,101 通过、1 失败、无跳过；2,404 份源码/配置及 2,035 份
Debug 运行文件身份一致，冻结已解除。旧视觉失败及附件导航超时本轮未重现。
详见 [PROVIDER_STREAM_COMPLETION_ACCEPTANCE.md](PROVIDERS.md#provider-stream-completion-acceptance)；新的结果不改写旧回归。
该新回归已报告任务查找原始分配计数用例 3,480 字节失败，具体原因未确认。
独立副本另复现 Queue 准备期间项目授权/模型配置变化后仍续发，事务核验候选
及单独的分配测量隔离调整见 [CONVERSATION_FOLLOWUP_DISPATCH_ACCEPTANCE.md](CONVERSATIONS.md#conversation-followup-dispatch-acceptance)，
两种构建及各 52 项 App、723 项基础设施检查通过，并在真实 Windows 进程中
验证取消一个队列任务会回收其父子进程，另一个任务继续完成、草稿保留。
17 个明确文件已按哈希基线无冲突合入，原仓库两种构建、各 52 项 App 和
723 项基础设施检查及 4 个 CLI 强制终止恢复场景通过。新完整 Debug 回归
session 55845 已终态：8,122 项中 8,121 通过、1 项队列写入审计断言失败，
无跳过或中止；八份报告与 2,411 份源码/配置、2,035 份运行文件的路径/hash
均已核对，冻结已解除。[QUEUE_TOOL_AUDIT_ASSERTION_ACCEPTANCE.md](CONVERSATIONS.md#queue-tool-audit-assertion-acceptance) 独立复现并
修正为精确写入及后台只读统计审计身份、数量和完成状态检查，保留旧失败。
随后浏览器取消修复及两文件测试等待修正已按基线 hash 无冲突合入；原仓库
Debug/Release 完整构建均零警告/错误，Browser 各 104 项、相关 App 最终各
212 项通过。第一次 Debug App 的 211 通过/1 失败也保留；受控完成事件证明
旧等待可在队列接管前提前断言，见 [QUEUE_SNAPSHOT_COMPLETION_ACCEPTANCE.md](CONVERSATIONS.md#queue-snapshot-completion-acceptance)。
完整 Debug 回归 session 93473 已终态：8,138 项中 8,136 通过、2 失败，无跳过或中止；
八份报告、2,414 份源码/配置和 2,035 份运行文件的路径与 hash 前后一致，冻结已解除。
两项终端文件就绪/子代理界面观察失败保留，五项测试修正已按基线合入，见
[RUNTIME_OBSERVATION_BOUNDARIES_ACCEPTANCE.md](TERMINAL_TOOLS.md#runtime-observation-boundaries-acceptance)。真实 Queue/Agent 的浏览器导航执行中
宿主终止/独立进程恢复另发现旧未完成审计显示“进行中”；schema 24 原子标记中断，
共享审计行明确结果未知并要求核实后手动继续。该修复及保留草稿/队列、不重放导航的
原仓库证据见 [AUDIT_INTERRUPTION_RECOVERY_ACCEPTANCE.md](TERMINAL_TOOLS.md#audit-interruption-recovery-acceptance)。原仓库 Debug/Release 完整构建
均零警告/错误，每种配置的 Windows 148、基础设施 729、工具审计 44 和相关 App 206 项
均通过；真实 schema-23 崩溃数据库副本升级保留消息、队列、草稿和审计，源库未改。
完整 Debug 回归 54927 已终态：8,153 项全部通过，无失败、跳过或中止，退出码零。
八份报告完整且互不重复，2,421 份源码/配置及 2,035 份运行文件路径/hash 前后一致；
汇总和报告 hash 见 `artifacts/audit-interruption-integrated-20261019/regression-summary.json`，冻结已解除。
Steer、原生/真实提供商验收和命令隔离阻断仍未完成。

本轮已终态的完整候选 `conversation-submission-bound-20261019` 为 8,014 通过、6 失败、
共 8,020 项，无跳过；2,359 份源码/配置和 2,035 份运行文件路径/hash 前后一致。
两项权限等待竞态已独立复现；另有两项提供商回答、两项审批按钮命中失败，单独重放
分别为 6/6、4/4 通过。新增移动控件复现证明 Headless 只更新布局时命中场景可能仍旧，
已补齐帧同步并保留实际点击和审批/文件/审计断言。新候选
`conversation-submission-waits-20261019` 完整 Debug 回归已终态：八份 TRX 均为
Completed，8,021 项全部通过（App 5,391 项），无失败、跳过或中止，退出码零；
2,361 份源码/配置和 2,035 份 Debug 运行文件路径/hash 前后一致。Debug/Release
各 32 项专项通过，两种配置的完整解决方案构建均零警告/错误。四项独立进程在提交
确认后强制终止并恢复的检查通过；另有同版本原生 EXE 无项目发送、流式观察和终止/
重启恢复检查通过，但黑帧和后续原生输入被拒绝使完整交互/视觉验收仍未完成。这不是完整 Release 回归。旧失败
原样保留，不能据此认定每条历史失败的根因相同。命令隔离新诊断已定位到 KnownDlls
对象目录权限；一次获准的 RunAs 实验在句柄预检即被拒绝，未添加系统 ACE，见 [COMMAND_BOOTSTRAP_PROBE.md](SECURITY.md#command-bootstrap-probe)。
以下历史切片的“最新”描述只代表其记录当时的状态。

PR 链接切片的当前候选为 `pr-links-verified-20261019`：完整 Debug 解决方案构建零警告/错误，七个项目不筛选加 App 的 PR/账号/共享组件/导航专项共 2,952 项通过，无失败或跳过；不是当前整套 App/解决方案测试全量通过。2,309 份源码/配置和 2,035 份运行文件测试前后路径及 hash 一致，八张新入口错误态/详情图已查看。前一 `pr-links-20261019` 中 2,947 项通过及四条旧授权弹框数量断言失败的证据原样保留；修正为精确弹框身份、唯一可见及双向互斥检查，补缺少账号的设置恢复提示后重新构建并扩大验证。范围与限制见 [SOURCE_PR_URL_ACCEPTANCE.md](GIT_WORKTREES.md#source-pr-url-acceptance)，不改写旧完整回归的含义。

1. 先修复已复现的核心/安全阻断。最新普通完整回归为 `pr-source-reads-verified-20261019`：八个项目不筛选的 7,908 项全部通过，无失败、跳过或中止，构建零警告/错误，2,297 份源码/配置及 2,035 份 Debug 运行文件测试前后路径/hash 不变。前一候选 `pr-source-reads-final-20261018` 的 7,906 通过、两条结构断言失败仍原样保留；后续只更新组合选择器和源码账号导航的断言，保留共享组件、精确导航目的地检查，再完整重跑。更早的 `restored-worktree-authorization-final-20261018` 为 7,355 项通过，只证明其旧二进制；不将新的通过当作所有历史失败根因或完整产品完成的证明。当前切片与原生/真实服务/Release 限制见 [SOURCE_PR_ON_DEMAND_READING_ACCEPTANCE.md](GIT_WORKTREES.md#source-pr-on-demand-reading-acceptance)。
2. 补齐编码与对话闭环：草稿、附件多轮/恢复及多模态、Git/工作树完整用户路径。
3. 推进 PR、插件及其他参考功能；逐项明确依赖，不能靠增加入口宣称支持。
4. 进行当前候选的专项、完整构建和回归，随后补齐隔离原生实例的关键交互验证。

此前隔离候选 `workspace-regression-20261018`：99 项专项通过，模型搜索单例在十个
独立 xUnit 进程复测通过，解决方案构建零警告/错误，148 份 C# 的单类型及规模审计通过。
会话 89190 因 .NET 10 诊断钩子污染 .NET 9 PowerShell 子进程而作废，已核对身份并
停止其独立测试进程树；普通模式下对应 MCP/终端四项复测通过，不认定为产品修复。
会话 54206 随后在同一冻结候选上进行普通完整回归，最终 exit 1：App 5021 通过、
2 失败（模型禁用用例初始化、目录查询 736 字节/预期零），其余七个程序集通过。
八条授权用例均通过，没有挂起、中止或跳过；原二进制与报告保留，不改写成绿色结果。

后续候选补齐两条模型测试的完整 Workspace 初始化等待；本机独立探针复现后台 GC
期间空区间计数非零，两个目录查询测试族改用短期、串行的无 GC 测量，零字节断言
和正分配对照均保留。[ALLOCATION_MEASUREMENT_ACCEPTANCE.md](ENGINEERING.md#allocation-measurement-acceptance) 记录 113 项专项通过；
它不是每个历史分配失败的根因证明。最新 `snapshot-sharing-fixed-20261018` 另修复
可确定复现的内部读句柄/原子编辑冲突，工具全套 499 项、App 专项 113 项通过，构建零警告/错误，
157 份 C# 规模审计通过。原授权 `tool_failed` 的具体异常仍未由旧报告证实，旧冻结
完整运行与最新候选必须分别验收，不能将本次文件修正直接当成旧失败已关闭。

新增 `attachment-thumbnails-20261018` 候选贯通选择/草稿恢复 → 后台限额缩略图 →
共享附件卡片，取消/卸载及失败回退不影响原附件权限和内容。基础设施全套 344 项、
App 附件专项 82 项通过，构建零警告/错误，168 份 C# 规模审计通过；已检查浅/深色
Headless 渲染图，未操作用户窗口。完整回归会话 95889 在旧运行终态后启动，二进制
已冻结，仍须读取终态结果；记录见 [ATTACHMENT_THUMBNAIL_ACCEPTANCE.md](CONVERSATIONS.md#attachment-thumbnail-acceptance)。
该新完整运行已报告另一条仍用原始计数 API 的侧栏筛选分配失败（1176 字节/预期零），
尚未终态；不能认定全量已恢复。后续独立候选已将该同步侧栏测试族接入同一隔离测量，
保留六种情况的功能和零字节断言；其他分配测试仍须核验测量边界及串行集合，
保留真实分配对照与功能/零字节断言，不将所有历史失败直接归因于运行时问题。

`history-thumbnails-20261018` 补齐已发送消息和重新打开的历史图片缩略图：复用共享
附件卡片，通过既有存储端口惰性读取带归属/长度/hash 校验的任务快照，不读原文件。
项目及无项目发送 → loopback → 删除源文件 → 新生产组合重新打开的两条链路通过；
包含负例、明暗主题和分配控制的专项 31 项通过，构建零警告/错误，174 份 C# 规模
审计通过。更广 App 专项随后终态为 409 项全部通过，无跳过或中止。旧完整回归
95889 已终态为 7,154 通过、1 失败、共 7,155 项；其中旧二进制的 1176 字节失败
保留，不能据此宣称全量通过或整个产品完成。

`image-orientation-20261018` 随后修复 JPEG EXIF 缩略图方向：改动前 32 个受控样例中
28 个失败，改动后八方向、双字节序及大小图像的像素检查通过。额外 16 条真实
文件/SQLite 草稿和历史路径验证原始字节保持、删除源文件后预览及真正无项目任务。
基础设施全套 392 项、App 相关专项 84 项通过，最终构建零警告/错误，178 份 C#
规模审计通过；原生/付费服务和完整新候选回归仍未由此建立。

`send-preparation-cancellation-20261018` 补齐准备阶段的取消入口、Enter 防重复发送、
草稿/附件保留、提交后 Cancelled 状态落库和归属不明时的安全恢复提示。附件/状态
写入和工作树回收在取消后仍持有实际操作，避免后台写入未结束就释放资源；共享
运行时中的其他对话不会被连带取消。窄窗口上下文栏改为分行，欢迎区适应剩余高度。
当前候选相关 App 319 项、共享控件全套 300 项、基础设施全套 392 项、附件边界
48 项均通过，构建零警告/错误，205 份 C# 及修改的 AXAML 规模检查通过；这些是
重叠的分组结果，不是完整解决方案测试。创建中崩溃恢复、完整工作树路径安全审计
及原生 EXE 重启仍未建立，详见 [SEND_PREPARATION_CANCELLATION_ACCEPTANCE.md](CONVERSATIONS.md#send-preparation-cancellation-acceptance)。

`worktree-external-metadata-20261018` 补齐工作树创建/清理的直接 GUID 子目录、
原始 Windows 路径别名、链接祖先、授权仓库范围和 Git 元数据双向归属校验。
独立 `.git` 指针借用未授权外部元数据的负例在修复前真实失败；修复后显式额外授权
仍可用，撤销元数据授权后禁止清理，合法 linked source 保留其自身 HEAD。
基础设施全套 437 项（含工作树 46 项）、App 相关 69 项、真实 Windows Git
进程归属 8 项通过，共 514 项；完整构建零警告/错误，219 份 C# 的单类型及规模
审计通过。前一冻结候选 `worktree-path-safety-final-20261018` 的完整回归已终态为
7,326 项全部通过（其中 App 5,127 项）；其冻结源码/二进制不含后续外部元数据和
恢复授权修复，不能冒充新候选全量验证。更早中断的全量尝试和三条 Windows 失败报告
均保留；两条旧 Git 异常断言已适配保留目录契约，PTY 共享冲突未修改生产代码。
原生句柄级路径竞态保护、恢复任务授权链路完整审计、创建中崩溃恢复和原生重启
仍未完成，见 [WORKTREE_PATH_SAFETY_ACCEPTANCE.md](GIT_WORKTREES.md#worktree-path-safety-acceptance)。

`restored-worktree-authorization-final-20261018` 补齐恢复工作树的逐操作授权：
准确绑定任务 GUID、仓库归属和授权快照，在审批前后重新校验；继续发送前拒绝
无效环境，不消耗草稿、不追加消息或启动模型。预览、差异、终端和子代理保留
守卫，显式清理拒绝借用另一任务的合法工作树或错误项目，保留目录及数据库记录。
清理负例先真实失败再修复；首轮扩大 App 回归的旧无校验器断言记录为 794/795，
更新为合法归属夹具并增加缺失/拒绝校验器负例后，最终 Tools 506、基础设施 442、
Agent 140、相关 App 799 和真实 Windows Git 进程 8 项均通过，共 1,895 项。
构建零警告/错误，238 份 C# 规模检查通过，源码/配置与冻结 ZIP、二进制身份无漂移。
这不是完整新候选回归、即时撤权、原生句柄级竞态或原生重启验收，详见
[RESTORED_WORKTREE_AUTHORIZATION_ACCEPTANCE.md](GIT_WORKTREES.md#restored-worktree-authorization-acceptance)。

上述恢复授权冻结版本的完整回归随后已终态：八份 TRX 全部 Completed，7,355 项
全部通过（App 5,137 项），无失败或跳过，真实进程退出码为零，1,601 份冻结
二进制及原生依赖身份仍一致。此运行使用新增 Git 暂存功能之前的二进制，部分
源文件策略测试也会读取实时工作区，因此不能代替暂存候选的完整回归。

`git-staging-20261018` 增加文件级暂存/取消暂存及真实侧栏动作：保留未选文件和
HEAD，正确区分重命名与复制来源，授权完整仓库和元数据，拒绝路径别名与审批期间
出现的链接，等待实际取消清理。Tools 全套 549、基础设施全套 442、Agent 全套
140 项、相关 App 1,124、共享视觉全套 300 及真实 Windows Git 进程 8 项均已
终态通过，共 2,563 项，无失败或跳过。最新独立 App 暂存/解析/Headless 专项
24 项包含真实索引、键盘操作、复制来源隔离及 Git 失败审计，已包含在上述 App
总数，不重复累加。构建零警告/错误，261 份手写 C# 及修改 AXAML 的规模检查
无违规；2,135 份源码/配置与 ZIP、2,439 份运行文件的身份和路径集合一致。
这不是暂存候选的完整解决方案测试。
证据与未实现范围见 [GIT_STAGING_ACCEPTANCE.md](GIT_WORKTREES.md#git-staging-acceptance)；提交/推送/PR 和原生实机验收
仍未完成。

`pr-inline-comments-20261018` 已补齐当前提交、精确路径和完整差异有效行上的
行内评论卡片与键盘定位，同侧多行校验整个范围并定位结束行；历史/跨侧/未加载
位置保留明确原因，不猜测位置。文件及评论换页、观察到撤权或提交变化、离开和
释放会清除旧标记及命令。长补丁重复定位验证真实滚动与焦点，不只是命令执行。
本轮完整 Debug 构建零警告/错误；Tools 644、基础设施 587、Agent 146、相关 App
354 和共享视觉 300，共 2,031 项终态通过，无失败/跳过。这不是完整解决方案测试。
2,263 份选定源码/根配置及 1,426 份选定测试运行文件在实际测试前后哈希与路径
集合一致；未生成可恢复归档。28 张 Headless 图中已检查主要行内卡片、长补丁
目标及紧凑/普通 PR 页。详见 [SOURCE_PR_INLINE_COMMENTS_ACCEPTANCE.md](GIT_WORKTREES.md#source-pr-inline-comments-acceptance)；该冻结版本的审阅对话、
个人收件箱/URL、reviews/activity、版本标记和确认写操作仍待实现，真实账号、原生
窗口、EXE 重启与 Release 未验收，不能将本切片视为完整 PR 支持或目标完成。

保持整个目标未完成；本文件是可持续更新的盘点，不是缩小后的完成定义。
新增功能必须从设置/入口贯通真实运行、取消/失败/并发和恢复，安全边界需负例。
任何新测试结果都不抹除历史失败，也不把 Headless/loopback 证据写成原生实机或付费服务验证。
<!-- /source: FUNCTIONAL_PARITY_MATRIX.md -->


---

<a id="integrated-workflow-acceptance"></a>

## INTEGRATED_WORKFLOW_ACCEPTANCE.md

<!-- source: INTEGRATED_WORKFLOW_ACCEPTANCE.md -->
### Integrated production workflow acceptance

#### Scope

The actual Debug/JIT `MossAgent.App.exe` starts through its production composition
root against fresh synthetic SQLite data and local project roots. Loopback
Responses, Gemini, MCP and HTML fixtures replace remote services, not App views,
repositories, orchestration or tool execution. No paid endpoint, real credential,
daily browser profile or user database participates.

The positive run uses App assembly SHA-256
`5FAAF93F347F3B9B10CB712900AD10360D77EE87EF051911BAFDDB2880B921DB`
and Agent assembly SHA-256
`47B2D1364F886E25EAC9D6DD1FA24F936266F7D5F2AC574CE5DE9FF9EF5001D0`.
Both native starts independently report an unelevated token. Native input uses
Sky only; Python reads state and passively captures stable, non-black pixels.

#### Successful combined flow

The real conversation input and send button start this sequence:

1. File write, read and replace produce `workflow.txt` with `status=edited`.
2. Parent shell execution reads the authorized file and returns a success marker.
3. Browser navigate, type, click, snapshot and screenshot operate on the attached
   managed local page. Its embedded WebView2 shows the actual input and result.
4. Configured Gemini child reads the edited file, executes its own project-local
   shell check and returns Markdown with a code block through `subagent.run`.
5. MCP echo runs through the registered standard tool pipeline.
6. The parent receives child/MCP results and renders the final response. SQLite
   records the task as Completed; the send control returns to its idle state.

The parent makes twelve Responses requests using its selected model, medium
effort, temperature 0.6 and Top-P 0.85. The child makes three Gemini requests;
each carries the configured system prompt, shared parent marker and HIGH thinking.
The parent's subsequent request contains the actual child answer and shell result,
not merely a synthetic final-summary assertion. Signed child tool continuations
remain in the real adapter path.

Native screenshots show child-running status, both child tool results, third
model turn, successful completion and the rendered child Markdown/code block.
The raw persisted tool payload independently contains successful child dialogue.
The file card opens the task-owned immutable snapshot patch through the actual
`filesystem.review_change` tool and shows the two added lines.

After review there are sixteen completed audits: fifteen successful and one
`tool_cancelled` old `filesystem.change_statistics` request superseded by the
later edit. Its replacement succeeds and the final card displays +2/-0. This
cancelled auxiliary request is not reclassified as success or a child failure.
The browser PNG and review patch both pass recorded length and SHA-256 checks.

#### Restore and shutdown

Normal close removes the first App process and task WebView2 processes. Restarting
the same production executable/data directory, then opening the saved conversation,
restores the completed parent response and child dialogue, including Markdown.
The timeline disclosure, inner dialogue scrolling and project-context toggle work
through native input. Models, proxies and child settings retain their recorded
table hashes; the foreign project's synthetic file is unchanged.

Model request counts remain exactly twelve parent/three child after restart:
no incomplete operation or model call is replayed. A new read-only file-statistics
request brings the restored audit count to seventeen, with the same single earlier
cancelled record. The task remains Completed and has fourteen saved messages.
The restored App and loopback service then close normally, leaving no matching
task WebView2 processes; service counters finish at 12 parent, 3 child, 1 page,
and 1 MCP call. Stderr is empty.

#### Separate negative branch

The preceding production-App run requested a child read outside the task root.
The inner read and outer `subagent.run` fail with `path_denied`; the denied inner
read has no granted approval. The child stops after two requests, not three.
The parent continues through MCP and completes. This is isolation evidence,
not a successful child run. No foreign content appears in whitelisted request
observations. Its completed snapshot review and artifact hashes are also verified.
That run predates the subsequent context-building optimization; the positive
combined run above uses the optimized Agent assembly.

#### Passive Codex comparison and limits

A passive current Codex capture and the production App capture agree on sampled
light-theme RGB values: title/rail (238,244,249), sidebar (249,251,253), main
(255,255,255). This establishes those color samples, not whole-window equality.
Codex is 1920x1032 and MossAgent is 1280x820; their task data and panel states
also differ. No Codex UI input or same-state pixel-parity claim is made.

The run does not replace provider/proxy editor acceptance, multi-task cancellation,
all protocol/transport checks or the full light/dark/window-state visual matrix.
Settings are deliberately seeded before startup, not edited during this scenario.
No production code, permissions, persistence schema or publishing options change.

Scripts, databases, requests and screenshots remain outside the repository:

- `%TEMP%/MossAgent-integrated-success-20261012-192953`
- `%TEMP%/MossAgent-integrated-workflow-20261012-185830`

The preceding same-source gate has 5,511 passing tests across eight assemblies;
one initial parallel Windows process-test timeout passes in the isolated rerun.
The current continuation also passes a fresh zero-warning/error solution build,
119/119 Agent tests and 134/134 focused App child/payload/snapshot-review tests;
documentation whitespace checks pass. The App assembly hash and its copied Agent
assembly still match the native-run identities after that build. This is fresh
composition evidence and focused regression, not another full-suite run.
The complete product objective remains active.
<!-- /source: INTEGRATED_WORKFLOW_ACCEPTANCE.md -->


---

<a id="plugin-workflow-plan"></a>

## PLUGIN_WORKFLOW_PLAN.md

<!-- source: PLUGIN_WORKFLOW_PLAN.md -->
### Plugin and skill workflow plan

Status: design only. No plugin directory, installation, enablement or runtime
integration is implemented by this document. MCP settings alone do not establish
plugin support. Command filesystem confinement remains the higher-priority blocker.

#### Verified reference semantics (2026-10-19)

Official sources read directly: [plugin packaging](https://developers.openai.com/plugins/build/plugins)
and [App Server](https://learn.chatgpt.com/docs/app-server).

- New portable packages use root `plugin.json`, the Agent Plugins 1.0 schema,
  `skills/`, root `mcp.json` and optional assets. Portable MCP entries explicitly
  declare their transport `type`; renaming legacy `.mcp.json` is insufficient.
- `extensions.com.openai`, when present as an object, replaces the entire
  `.codex-plugin/plugin.json` compatibility overlay. Their OpenAI settings are not
  merged. Root portable identity/components remain canonical. Legacy compatibility
  is a separate import path whose behavior must be tested explicitly.
- Public directory, local/repository marketplaces, registered connector mappings,
  MCP authentication and optional tool UI have distinct distribution, account and
  runtime requirements. A local package importer does not complete those workflows.
- Installation/enablement does not automatically trust bundled hooks. The current
  hook definitions require review. Package manifests describe authentication and
  must not contain credentials such as OAuth client secrets or bearer tokens.
- App Server marks `plugin/list`, `plugin/read`, `plugin/install` and
  `plugin/uninstall` under development and says production clients must not call
  them yet. They are not a supported shortcut to the official directory. This
  limitation does not establish that another public directory API is available.

#### Ownership and required self-check

1. Domain owns immutable package identity, component descriptions, source/revision
   and trust state. Application owns discovery/install/enable/remove use cases and
   ports. Infrastructure owns bounded parsing, source fetching, staged package
   storage and persistence; MCP retains its transport implementation. App owns
   shared controls and localized lifecycle/error feedback. No UI SQL/process/file
   access, reverse dependency or global registry is introduced.
2. Reuse the shared tool contract/executor and existing model/context budgets.
   A plugin is an origin of skills/tools, not an alternative executor. Every MCP
   operation and any supported hook command must retain its source identity and
   run through authorization, approval, cancellation, deadline, output and audit.
3. Package paths are relative to their package root. Reject absolute paths,
   traversal, links/reparse escapes, archive collisions and extraction bombs.
   Installation grants no project roots or browser attachment. Stdio launch/hook
   execution also depends on the unresolved command sandbox; do not integrate a
   launch path that silently inherits arbitrary host file or credential access.
4. Split manifest formats, catalog sources, extraction, immutable installed
   versions, task activation snapshots, trust and recovery into separate types.
   Preserve single-type files and existing file/method limits. Configuration uses
   forward migrations, revision checks and bounded metadata rather than binaries.
5. Test valid/invalid portable and legacy manifests, whole-overlay precedence,
   path/link/archive denial, cancellation/partial install rollback, version and
   trust conflicts, disabled/revoked components, task isolation, exact selected
   model/skill context, shared MCP execution, audits and restart without replay.

#### Delivery sequence and completion evidence

First implement an explicit local package workflow with source preview, validation,
immutable installed copy, enable/disable, component inventory, actionable failures
and uninstall preserving user-owned data. Mark that slice as local-only while
marketplaces, public directory, OAuth, registered apps, hooks and optional tool UI
remain separately visible gaps. Do not label unsupported components as enabled.

Then connect selected skills to bounded model instructions and plugin MCP tools to
the task's normal catalog/pipeline. Persist the exact installed version and reviewed
trust revision. Define disable/uninstall behavior for active tasks before allowing
those mutations; do not change a running task's components behind its back. Recovery
may clean staged files and show interrupted state; it never executes an interrupted
hook, tool, setup instruction or install script automatically.

Finally add and verify catalog/distribution and authentication routes where public
interfaces and permissions permit. Keep account-dependent unavailable routes
explicit. Completion requires installation through actual skill/tool invocation,
failure/cancellation/recovery, independent tasks, native shared UI and security
negative cases; manifest parsing or a same-named button is insufficient. Live
accounts, simulated transports, Headless UI and native interaction remain distinct
evidence categories.
<!-- /source: PLUGIN_WORKFLOW_PLAN.md -->


---

<a id="product-acceptance-matrix"></a>

## PRODUCT_ACCEPTANCE_MATRIX.md

<!-- source: PRODUCT_ACCEPTANCE_MATRIX.md -->
### Product acceptance matrix

#### Current objective (2026-10-18)

The product goal is now functional and workflow parity with Codex Desktop, not
one-to-one visual fidelity. [FUNCTIONAL_PARITY_MATRIX.md](OVERVIEW.md#functional-parity-matrix) owns the current gap
inventory and priorities. Existing feature, safety, real-interaction, theme,
viewport and keyboard requirements remain; exact Codex geometry, artwork, colors
and pixel equality are no longer completion gates.

The records below preserve their original scope, results and dates. Historical
statements about an open pixel-parity gate do not reintroduce that requirement;
removing it does not turn unverified functional behavior or failed tests into a
pass. Historical scheduler/goal-status statements are not current runtime status.

#### Historical implementation and evidence records

[SOURCE_PR_READING_ACCEPTANCE.md](GIT_WORKTREES.md#source-pr-reading-acceptance) records the schema-19 read-only source slice:
explicit credential-revision-bound repository grants, revocation tombstones,
source-operation audit sessions, standard pipeline PR list/detail tools and an
independent shared-control page. Configured-repository pagination, description,
base/head commits, cancellation and error feedback now have a running chain.
This does not complete personal inbox/URL/diff/comments/checks, review chat,
revision view state, remote mutations, OAuth, GitLab or native/live-source gates.

[WORKTREE_PATH_SAFETY_ACCEPTANCE.md](GIT_WORKTREES.md#worktree-path-safety-acceptance) records the managed worktree creation/removal
boundary slice: raw Windows path aliases, GUID direct children, link/reparse
ancestry, explicit repository scope, bounded pointer preflight and matching Git
identities/backlinks. A real standalone external-metadata authorization bypass
has red/green temporary-repository evidence, including explicit authorization and
revocation. The latest candidate passes Infrastructure 437, related App 69 and
real Windows Git ownership 8 checks, with a clean solution build and 219-file C#
shape audit. These 514 checks are not a new complete regression; the preceding
frozen candidate's full App run is pending. Native handle-level race protection,
restored-task authorization and native restart remain incomplete. Earlier failed
and interrupted reports are retained, not relabeled as green.

[APP_REGRESSION_RECOVERY_ACCEPTANCE.md](ENGINEERING.md#app-regression-recovery-acceptance) records a confirmed Headless test-harness
deadlock, five corrected awaiting window helpers and a strengthened existing
dispatch guard. The failed reproducer and explicitly retired old test processes
remain recorded. A 74-case focused run and final solution build pass; the expanded
complete regression exposed four settings/new-task
draft-scope assertions. Their follow-up checks the real destination draft and
retains both scopes through SQLite and round-trip navigation, without changing
production scope semantics. That follow-up passes 31 focused cases and a fresh
solution build; it is not a replacement full-suite result. That expanded run
subsequently aborted after 3,812 App passes during fixture cleanup; its other
seven test projects passed. [DRAFT_BACKGROUND_PERSISTENCE_ACCEPTANCE.md](CONVERSATIONS.md#draft-background-persistence-acceptance) records
the separate background-persistence fix, a 118-case focused pass, and the current
full/final candidates. The new full candidate has exposed a model-lookup
allocation failure and an agent-selection race; neither a focused pass nor the
additional load-serialization fix establishes a green complete regression.
Earlier allocation failures and whole-product acceptance are not declared resolved.

[PROJECTLESS_ATTACHMENTS_ACCEPTANCE.md](CONVERSATIONS.md#projectless-attachments-acceptance) extends explicit text/static-image inputs
to plain chats through a separate Application reader port, not a fake project or
directory grant. Version-17 migration, independent draft/history reopen, actual
loopback requests without tools, stale selection refusal and theme/keyboard
checks are covered there. It also records the initial six App regression failures
and their fixes; its verification scope does not establish native or whole-product
acceptance.

[IMAGE_ATTACHMENT_WORKFLOW_ACCEPTANCE.md](CONVERSATIONS.md#image-attachment-workflow-acceptance) records the preceding static-image
workflow checkpoint: real bounded codecs, authorized reads, immutable binary
draft/message snapshots, version-16 metadata migration and rollback, typed
unavailable chips, selected-model capability feedback and image-only sending.
Independent production compositions restore unsent and submitted images after
source deletion; follow-up loopback requests retain native image blocks without
Base64 text context. At that checkpoint Infrastructure 316, Providers 600, Agent 132 and
related App 150 checks pass, as does the solution build. These are not native
picker/EXE-restart, real-provider reasoning, a current complete App suite or
whole-product acceptance. Earlier pending processes/failures are not erased.

[ATTACHMENT_HISTORY_ACCEPTANCE.md](CONVERSATIONS.md#attachment-history-acceptance) records submitted UTF-8 attachment snapshots,
message-owned SQLite metadata, restored chips and original-turn context across
follow-up requests and independent App compositions. Source changes do not alter
submitted data; missing/tampered snapshots block provider calls with a safe,
persisted recovery diagnostic. Ownership, junction, size, cancellation, timeout,
atomic metadata and forward-migration checks use temporary data and scripted
providers, not native selector clicks, an EXE restart or multimodal inputs.
Unsent attachment drafts were not covered at that historical checkpoint; later
draft/image/projectless records supersede that gap. The overall functional-parity
goal remains incomplete.

[PROJECT_EDITOR_VIEWPORT_ACCEPTANCE.md](UI_WORKFLOWS.md#project-editor-viewport-acceptance) records the bounded project-editor slice:
long directory lists no longer push Save outside the owner viewport, keyboard
focus reveals primary-directory actions, and Add Project starts a new draft after
an edit is dismissed. Production MainWindow/SQLite tests cover ordered directory
updates, actual create/save input, canceled selection, validation recovery and
locked/busy state boundaries. Headless/Skia theme and DPI evidence does not replace
native picker, same-state Codex comparison or updated-root-to-tool execution checks.

[CDP_AGENT_WORKFLOW_ACCEPTANCE.md](BROWSER_MCP.md#cdp-agent-workflow-acceptance) extends direct-tool isolation evidence through
the real production Agent/provider/App composition. Actual browser-settings Save,
composer Send and targeted Stop operate against a test-owned headless Chrome.
Two final runs independently make 12 model requests and ten complete browser
audits: A/popup release cannot disturb B, a foreign tab is rejected and B completes
with its actual snapshot/PNG and rendered Markdown. Fresh composition restores
Cancelled/Completed state and keyboard-operable history at compact Dark size
without model/browser replay. Controls/reply receive real input-hit checks.
Current Browser 88, Agent 123 and related App 195 cases pass, with matching App
discovery and no skips/aborts. Build, driver format/source and assembly identities
pass. This 406-case regression scope and scripted-model workflow are not native
Codex parity, paid-model reasoning, a complete current all-suite checkpoint or
whole-product completion.

[CHECKBOX_FOCUS_APPEARANCE_ACCEPTANCE.md](UI_COMPONENTS.md#checkbox-focus-appearance-acceptance) completes the shared rounded keyboard
focus language for checkboxes. Eighteen unchanged shape baselines fail on the
native square frame; two more fail on missing shared feedback, while two explicit
ownership cases already pass. A first candidate reveals four fractional-DPI glyph
shifts; preserving check-surface thickness fixes them without changing hit areas
or native three-state/disabled behavior. Final complete pixels pass 282, related
App passes 424 with matching discovery, and architecture passes seven. This
713-case scope has no failures/skips/aborts. Build, scoped format/source/hygiene
and assembly identities pass. Headless before/after evidence does not replace
postponed native/Codex comparison, a complete current App/all-suite checkpoint,
the earlier allocation investigation or whole-product acceptance.

[CDP_TASK_ISOLATION_ACCEPTANCE.md](BROWSER_MCP.md#cdp-task-isolation-acceptance) records the preceding browser checkpoint:
explicit CDP attachment creates a task-owned page instead of taking the user's
first page, and borrowed contexts expose only task-owned/attached pages and their
descendant popups. Cancellation cleans up late pages; disposal waits and releases
only owned pages/runtime state once. Context-level cookies/login/storage still
remain shared. Six original isolation cases fail before the correction. Final
Browser 88, Tools 494, Agent 123 and related App 195 cases pass without skips or
aborts; the 195-case discovery matches. Nine source/assembly identities and build
checks pass. A test-owned headless Chrome and production settings-to-standard-tool
pipeline verify independent navigation, foreign-ID/ReadOnly refusal, real PNG,
surviving-task operations and sentinel preservation through 12 complete audits.
This separate 900-case checkpoint precedes the checkbox appearance revision;
overlapping suites and old full-App/pixel runs are not summed into current
coverage. Native embedded concurrency, paid-model invocation and whole-product
or Codex parity remain open.

[SELECTOR_FOCUS_APPEARANCE_ACCEPTANCE.md](UI_COMPONENTS.md#selector-focus-appearance-acceptance) corrects reproduced square keyboard-focus
frames in ordinary/compact dropdown items using a native-based shared container
theme. Existing pill/local focus ownership remains intact: a first global-style
candidate breaks two unchanged theme tests, and the corrected theme default
passes them without weakening assertions. The final baseline passes eight
preservation cases and fails all sixteen new defect/adorner cases. Complete
pixels pass 260; related App passes 198 with matching discovery; architecture
passes seven. This 465-case scope has no failures/skips/aborts. Build, scoped
format/source and assembly-identity checks pass. A separately completed 4,794-case
App process retains the preceding App DLL and does not establish a full run of
this new appearance revision. Its unchanged filtering/GC-pressure investigation
does not reproduce or causally fix historical allocation failures. Native/Codex
and whole-product acceptance remain open.

[BUTTON_FOCUS_APPEARANCE_ACCEPTANCE.md](UI_COMPONENTS.md#button-focus-appearance-acceptance) fixes reproduced square keyboard-focus
frames on shared rounded buttons. All 24 unchanged-appearance shape baselines
fail; the final shared adorner follows the native target radius without moving
content or intercepting input. Explicit component focus ownership stays intact.
The nonvisual Template factory is recognized without permitting native visual
children or page-owned focus templates. Final complete pixels pass 236; related
App passes 1,649 with matching discovery; architecture passes seven. This 1,892-
case scope has no failures/skips/aborts. Build has zero warnings/errors, scoped
format/source checks pass and six DLL identities remain unchanged. It is not a
complete App/eight-suite checkpoint, allocation-failure repair, native/Codex parity
or product completion.

[AUTOMATION_UI_WORKFLOW_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#automation-ui-workflow-acceptance) adds eight connected production-UI cases
across Light/Dark and compact/wide windows. Actual editor/dropdown/numeric input,
save, enable/pause, run/stop, visible history, invalid drafts and confirmed/canceled
deletion reach the real SQLite/scheduler/provider composition. Saved model, effort
and prompt reach the recorded request; background runs preserve foreground drafts,
and deletion preserves run/task history. All 152 App automation, 41 Infrastructure
automation and seven architecture cases pass. Build has zero warnings/errors;
scoped format, source limits and eleven DLL identities pass. This slice adds tests,
not a production fix. It does not replace the earlier allocation failure, repeat
the full pixel/eight-suite checkpoint, or establish native/Codex/product parity.

[SHARED_INTERACTION_STATES_ACCEPTANCE.md](UI_COMPONENTS.md#shared-interaction-states-acceptance) fixes two reproduced shared-appearance
defects: pointer hover erased focused input borders, and Dark-mode disabled
primary/send buttons used hardcoded Light colors. Twelve focus and eight button
baseline cases fail before the correction and pass afterward. Final complete
pixels pass 208 cases (including the 20 new cases); focused App passes 437.
The build has zero warnings/errors; scoped format/hygiene and assembly identities
pass. This 645-case scope is not a complete App/eight-suite checkpoint, a repair
of the previous allocation assertion, native/Codex parity or product completion.

[SETTINGS_CATALOG_VIEWPORT_ACCEPTANCE.md](UI_WORKFLOWS.md#settings-catalog-viewport-acceptance) fixes reproduced proxy/MCP catalog
clipping in all eight compact/wide Light/Dark baselines. Finite remaining-height
lists retain native scrolling and natural empty/single-row height; status uses
the shared bounded header. Thirty-item production MainWindow/SQLite cases verify
visible last-row hit targets and actual edit/save identity. All 36 new cases pass
within 4,784 App cases across six disjoint partitions, with exact discovery/class
counts and unique TRX IDs. Complete pixels 188, Providers 542, Infrastructure
proxy/HTTP subset 20 and architecture subset seven also pass. Build has zero
warnings/errors; scoped format/hygiene and eight assembly identities pass. The
earlier same-process App attempt is interrupted without a final report after an
untouched sidebar allocation assertion observes 1,200 bytes instead of zero;
batched success does not erase or explain that failure. This is not an all-green
same-process/eight-suite checkpoint, native/Codex parity or product completion.

[SETTINGS_STATUS_OVERFLOW_ACCEPTANCE.md](UI_WORKFLOWS.md#settings-status-overflow-acceptance) records a production shared-header fix:
long discovery feedback previously reduced the provider-list viewport to zero
in all four Light/Dark and compact/wide baselines. A shared 96-DIP scroll bound
now preserves actions and the list while retaining complete keyboard-readable
text. All 232 related App cases (including 16 new overflow/loading/keyboard/short
status cases), 188 pixel cases and seven architecture cases pass without skips
or aborts. Build has zero warnings/errors; scoped format and assembly-identity
checks pass. Off-screen before/after evidence does not replace postponed native
screenshots, same-state Codex comparison or full-product acceptance. The earlier
6,495 complete checkpoint remains historical, not increased by adding subsets.

[PROVIDER_UI_WORKFLOW_ACCEPTANCE.md](PROVIDERS.md#provider-ui-workflow-acceptance) extends the entry audit with six connected
production-UI/loopback workflows across Responses, compatible Chat and Gemini,
each in actual Light/wide and Dark/compact states. Real actions exercise discovery
503 recovery, model validation/save/reopen, repeated-fetch customization retention,
provider disable/re-enable and plain-chat submission. Stored settings reach the
actual adapter request; the completed Markdown answer is hit-tested on-screen.
All 217 related App cases and seven architecture cases pass; scoped whitespace,
source-size, assembly-identity checks and the zero-warning/error solution build
pass. This adds tests, not a production correction. The previous 6,495 complete
checkpoint is not increased by adding subsets or these six new cases. Native
screenshots, same-state references and broader product gates remain open.

[PRODUCT_INTERACTION_AUDIT.md](OVERVIEW.md#product-interaction-audit) records the latest complete regression and bounded
cross-page entry review. All eight complete suites pass 6,495/6,495 with no
failures/skips/aborts (App 4,726; pixels 188; other suites 1,581), including all
28 new manual-approval/keyboard/focus cases. After all test processes finish,
the solution build has zero warnings/errors; eight tested assembly hashes and
four matching App copies stay unchanged. A separate synthetic Headless driver
reviews 4,038 button occurrences across 160 page/state combinations, including
40 new-editor states, with no unresolved enabled wiring. Search/dialog keyboard
paths and numeric pointer actions also pass, while most commands are not invoked.
These are repeated occurrences, not distinct controls or additional xUnit cases.
This audit changes no production code and does not replace postponed native
screenshots, same-state Codex comparison or full-product acceptance.

[SUBAGENT_MANUAL_APPROVAL_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#subagent-manual-approval-acceptance) records deterministic production-UI
approval-to-child execution, rejection/cancellation, dialogue visibility and
restart-without-replay coverage. It also fixes keyboard-inaccessible expanded
tool output using shared rounded focus appearance and native paging. Native
captures remain postponed; this slice is not native manual-approval success,
same-state Codex visual acceptance or a new complete eight-suite checkpoint.
That slice's targeted checks pass 317 related App cases and all 188 pixel cases with
no skips/aborts; the solution build has zero warnings/errors.

[SIDEBAR_SHUTDOWN_ACCEPTANCE.md](UI_WORKFLOWS.md#sidebar-shutdown-acceptance) records the preceding lifetime correction and
complete checkpoint. Graceful shutdown/async disposal cancel and drain sidebar
loads and all sort/move/reorder commands before reporting storage release.
Eighteen new cases include real synthetic file locks, repeated stop identity,
canceled waiters and late-result/notification rejection. All eight complete
suites pass 6,467/6,467 with no skips/aborts (App 4,706; pixels 180; other suites
1,581). Build has zero warnings/errors, and tested assembly hashes remain stable.
The earlier allocation and directory-cleanup failures do not recur in this full
run; their original evidence is retained and their historical causes are not
claimed fixed. Native/Codex visual and full-product acceptance remain open.

The shared-alignment follow-up in [UI_COMPACT_LAYOUT_ACCEPTANCE.md](UI_WORKFLOWS.md#ui-compact-layout-acceptance) corrects
render-confirmed inactive selectors on provider headers and task failure notices.
Fresh checks pass all 180 pixel cases plus 93 related App cases, including fourteen
new theme/width/scaling/resize/scope cases. Build has zero warnings/errors; scoped
format, whitespace and App assembly-copy identity checks pass. This targeted
checkpoint does not replace the complete-run evidence or explain its failures.

[SETTINGS_PAGE_HEADER_ACCEPTANCE.md](UI_WORKFLOWS.md#settings-page-header-acceptance) records the preceding shared-header and complete
validation checkpoint. Five settings pages reuse responsive title/description/
status/action composition, while existing commands and sanitized errors remain
functional. All 48 new page/policy/rendering cases pass. That eight-suite
run passes 6,433/6,435 with no skips/aborts: an untouched sidebar zero-allocation
assertion reports 3,288 bytes, and an untouched restart test fails during owned
temporary-directory cleanup. Three isolated nine-case reruns pass without
replacing either complete failure. The earlier complete run's two inline-markup
guards are updated to the shared component and pass in the final run. Build has
zero warnings/errors; format, whitespace and tested-assembly identity checks pass.
Native captures remain postponed and the whole product goal stays open.

[MCP_RECONNECT_RECOVERY_ACCEPTANCE.md](BROWSER_MCP.md#mcp-reconnect-recovery-acceptance) records the current settings recovery and
mutation-lifetime follow-up. Reconnect refreshes both catalogs; guarded mutations
share cancellation, reject missing targets and preserve newer editor input.
Closing the editor resets delete confirmation. A real hanging stdio server and
child exit on settings disposal before container shutdown, while committed data
remains inspectable. Original HTTP recovery/approval/audit coverage is retained.
Final related App/component/pixel/MCP/architecture checks pass 321 distinct cases;
the original three reconnect failures, later eight mutation failures and earlier
HTTP timeout evidence remain recorded. Build/format/whitespace pass. This is a
targeted checkpoint, not a new complete eight-assembly or native/Codex gate.

[UI_COMPACT_LAYOUT_ACCEPTANCE.md](UI_WORKFLOWS.md#ui-compact-layout-acceptance) records the preceding uncommitted composer and
settings-row follow-up. Fresh complete non-UI suites pass 1,581 cases and complete
pixels pass 136. The first complete App run finishes normally at 4,611/4,615:
four old shared-style assertions fail, with no skips/aborts. After correcting
those assertions, the complete App rerun passes 4,614/4,615; the one failure is
an untouched catalog lookup zero-allocation assertion (5,104 bytes, cause
unproven). Its five-case class passes three isolated no-build reruns, without
replacing the full-run failure. All eight complete suites total 6,331/6,332,
with no skips/aborts; focused and isolated results are not double-counted.
Build/format/whitespace and eight tested-assembly identity checks pass, but this
is not an all-green checkpoint. Native screenshots remain user-postponed;
no whole-product or same-state Codex acceptance is claimed. Older complete-run
records below are historical rather than results for this current UI revision.

[AUTOMATION_PAGE_LAYOUT_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#automation-page-layout-acceptance) records the scoped layout follow-up:
automations now occupy the full content frame beside the navigation rail, outside
the conversation sidebar/inspector layout. Conversation sidebar preferences and
drafts survive page/settings round trips, while automation-page titlebar navigation
and sidebar tint are hidden. Fourteen new theme/size/input cases reproduce the old
layout and pass after the change. Six old tests are adapted to visible rail or
explicit catalog navigation rather than clicking the now-hidden sidebar, retaining
identity/draft/request assertions. The final related run passes all 144 cases,
including the fourteen new ones; solution build and scoped formatting pass.
Native screenshots and live-window restart remain postponed by the user. This
targeted gate does not replace the historical complete run below or complete
the broader Codex-fidelity/product objective.

[THEME_THUMBNAIL_SHADOW_ACCEPTANCE.md](UI_COMPONENTS.md#theme-thumbnail-shadow-acceptance) records a shared-scene clipping correction:
the existing inner-screen shadow now has a noninteractive sibling owner instead
of being clipped by its own rounded content. Baseline pixels fail all twelve new
rendering cases; focused pixels 30/30 and layout 33/33 pass. The complete run is
6,210/6,211 across eight assemblies with all eighteen new cases passing. One
untouched model-picker allocation assertion fails at 5,864 bytes instead of zero;
three isolated five-case reruns pass without replacing that complete failure.
Build has zero warnings/errors; scoped format and independent evidence checks pass.
Twelve accepted candidate native frames verify three sizes and theme actions.
Historical 80x60 crop mean errors improve, but exact differing-pixel counts worsen;
this is not pixel parity. Current Codex is now maximized, with its right preview
still open, and remains a conversation rather than an equivalent Appearance page.
Manual provider configuration invalidates the old instance's final data audit;
that data is preserved. A separate clean instance verifies nineteen unchanged
tables and zero artifacts. User-postponed screenshots leave both restored windows
open, with restart Appearance, compact Light titlebar rendering and broader gates
explicitly unverified. No production-data, JIT/JSON or packaging changes are made.

[APPROVAL_GESTURE_IDENTITY_ACCEPTANCE.md](TERMINAL_TOOLS.md#approval-gesture-identity-acceptance) records a subsequent approval-input
correction: cancelling request A while its decision button is held can no longer
transfer that gesture to request B through a rebound parameter. The 138-case
focused run includes 18 new gesture/repeat/rendered-file-and-audit cases. Four
passive native Light/Dark captures preserve exact before/after pixels at 96 DPI;
they observe synthetic pending requests and do not automate native decisions or
establish Codex approval-card equivalence. The complete eight-assembly run passes
6,192/6,193, including every new case; one untouched sidebar-allocation assertion
fails (0 expected, 5,016 bytes observed). Its six-case class passes three isolated
reruns without rebuilding, but the original full run remains not all-green.

[COMPOSER_KEYBOARD_ACCEPTANCE.md](CONVERSATIONS.md#composer-keyboard-acceptance) records the preceding all-green gate: 6,175/6,175
across eight assemblies, no failures/skips/aborts, including 32 new cases. Focused
Enter/Ctrl+Enter submission and Shift+Enter newline reuse the existing command;
preparation/run, preedit, held-key and focus guards prevent unintended side effects.
Eleven Light/96-DPI native captures verify text-only loopback sends, running-draft
retention, explicit Stop and three sizes. Two requests, exact persisted messages,
sixteen unchanged tables/preferences, zero tools/artifacts and normal exits are
independently checked. Native Dark/high-DPI/real IME timing and a same-state Codex
reference remain open; this is not full-product or whole-window acceptance.

[THEME_THUMBNAIL_ARTWORK_ACCEPTANCE.md](UI_COMPONENTS.md#theme-thumbnail-artwork-acceptance) records the preceding complete gate:
6,143/6,143 across all eight assemblies, including 18 new compositor pixel cases,
with no failures/skips. The shared preview corrects conversation/composer anchors
and light/dark artwork ink. Fixed historical-reference crops improve in all three
modes; nine accepted native captures cover theme switching, keyboard activation,
three window sizes and System restoration after normal close/reopen. This is not
a current-version whole-page comparison; inner-screen shadow/border, branding,
title ink, theme action semantics/catalog and native high-DPI parity remain open.

[ASYNC_PROCESS_PIPE_ACCEPTANCE.md](TERMINAL_TOOLS.md#async-process-pipe-acceptance) records the preceding complete gate: 6,125/6,125
cases across all eight assemblies, zero skips/failures, a zero-warning/error
solution build, scoped format and independent report/binary/native-data checks.
Old/new probes reproduce and remove idle-pipe worker starvation and a test-only
SOCKS5 rejection-publication race without changing existing deadlines or disabling
parallelism. Production composition executes real shell/stdio MCP calls; six
native Light/96-DPI frames verify output history and no-replay restoration, not
native composer Send. This supersedes earlier non-all-green gates without erasing
their historical records. Full Codex fidelity and complete-product gates stay open.

[SETTINGS_TITLEBAR_NAVIGATION_ACCEPTANCE.md](UI_WORKFLOWS.md#settings-titlebar-navigation-acceptance) records removal of the redundant
wide-settings titlebar action and the matching first-menu inset, while retaining
compact drawer access and independent workspace navigation. Native light/dark
replay and seventeen accepted captures preserve synthetic data and restore exact
preference bytes. Residual menu/title typography and current-version same-state
references remain separate open gates; the complete goal is not marked complete.
This slice's full run passes 6,090/6,091 cases, including all 84 visual cases;
one untouched catalog-allocation assertion fails, then its five-case class passes
three isolated reruns. The original full run is retained as not all-green rather
than replaced by the reruns. Focused 60 and rerun 15 are not double-counted.

[APPEARANCE_THEME_PICKER_ACCEPTANCE.md](UI_COMPONENTS.md#appearance-theme-picker-acceptance) records the shared default/current-custom
palette pill, real selection/reset/save behavior and 96-DPI native close/reopen
evidence. Its final complete solution run passes 6,083 cases across eight
assemblies; build, formatting and native data/exit audits pass. Labels/action
glyphs and the full theme catalog remain separate fidelity
gates; this slice does not establish whole-window or complete-product equivalence.

The full Windows-first Avalonia/.NET 10 objective remains active. Existing modules
and passing tests are not substitutes for end-to-end or rendered acceptance.
This matrix distinguishes historical independent evidence, fresh combination
evidence and remaining completion gates; it does not mark the product complete.

#### Explicit functional requirements

| Requirement | Existing independent evidence | Current combined evidence / remaining scope |
| --- | --- | --- |
| Multiple proxies, endpoint/port/default and explicit provider direct routing | [AUTHENTICATED_PROVIDER_PROXY_ACCEPTANCE.md](PROVIDERS.md#authenticated-provider-proxy-acceptance), [PROXY_EDITOR_SELECTION_ACCEPTANCE.md](PROVIDERS.md#proxy-editor-selection-acceptance), [SETTINGS_CATALOG_VIEWPORT_ACCEPTANCE.md](UI_WORKFLOWS.md#settings-catalog-viewport-acceptance) | The latest App partitions rerun deterministic proxy regressions. New production MainWindow/SQLite cases preserve a 30-proxy catalog while editing/saving its visible last row in both themes and widths. Earlier native authenticated routing remains separate; this does not repeat every native protocol recovery branch. |
| Multiple providers and OpenAI/Gemini/compatible protocols | [MODEL_CONFIGURATION_NATIVE_ACCEPTANCE.md](PROVIDERS.md#model-configuration-native-acceptance), [COMPATIBLE_STREAM_ACCEPTANCE.md](PROVIDERS.md#compatible-stream-acceptance), [GEMINI_CONTINUATION_ACCEPTANCE.md](PROVIDERS.md#gemini-continuation-acceptance) | Production Responses parent and Gemini child both execute; all protocols retain deterministic regressions. Remote credentials and paid services are not needed by ordinary tests. |
| Discover/manual models, context/reasoning and model parameters | [MODEL_CONFIGURATION_NATIVE_ACCEPTANCE.md](PROVIDERS.md#model-configuration-native-acceptance), [PROVIDER_DIALOG_NATIVE_ACCEPTANCE.md](PROVIDERS.md#provider-dialog-native-acceptance), [MODEL_REASONING_CAPABILITY_ACCEPTANCE.md](PROVIDERS.md#model-reasoning-capability-acceptance) | Positive combined requests verify actual saved model/effort/temperature/Top-P. This scenario seeds settings and does not replace the prior real editor-save/discovery checks. |
| Provider page, expandable groups and individual model editors | [PROVIDER_CATALOG_ACCEPTANCE.md](PROVIDERS.md#provider-catalog-acceptance), [PROVIDER_DIALOG_NATIVE_ACCEPTANCE.md](PROVIDERS.md#provider-dialog-native-acceptance), [PROVIDER_ROW_COMPONENT_ACCEPTANCE.md](PROVIDERS.md#provider-row-component-acceptance) | Existing native editor cases cover save/reopen/errors, with deterministic late-result and ownership guards. Full current-state PI-Desktop visual equivalence remains unproven. |
| Child name/trigger/prompt/model/effort/sharing | [SUBAGENT_RUNTIME_CONFIGURATION_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#subagent-runtime-configuration-acceptance), [SUBAGENT_LIFECYCLE_NATIVE_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#subagent-lifecycle-native-acceptance), [SUBAGENT_SETTINGS_TO_RUNTIME_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#subagent-settings-to-runtime-acceptance) | Latest native run edits and saves name/trigger/prompt/model/high effort through the actual editor, then executes with sharing off and on. Four child requests verify the selected Gemini model/HIGH effort, new prompt and context distinction. Invalid settings/Stop retain independent coverage. |
| Child tools, progress, completion and visible dialogue | [SUBAGENT_EXECUTION_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#subagent-execution-acceptance), [SUBAGENT_DIALOGUE_PROJECTION_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#subagent-dialogue-projection-acceptance), [SUBAGENT_SETTINGS_TO_RUNTIME_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#subagent-settings-to-runtime-acceptance), [SUBAGENT_MANUAL_APPROVAL_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#subagent-manual-approval-acceptance) | Prior native live/completed/restored captures show authorized child reads, Markdown answers and parent results. New deterministic production-UI cases verify manual approval, saved child routing, visible restored results and denial/cancellation without replay. Native manual-approval success remains open; deterministic coverage does not replace it. |
| Projects and authorized roots | [PROJECT_MANAGEMENT_ACCEPTANCE.md](UI_WORKFLOWS.md#project-management-acceptance), [FILE_EDITING_ACCEPTANCE.md](CONVERSATIONS.md#file-editing-acceptance) | Positive operations stay inside the selected task root; foreign synthetic file and request-leak flags remain unchanged. Link/reparse/concurrent-write negatives retain dedicated tests. |
| Conversation, streaming and restored state | [MESSAGE_SEND_ACCEPTANCE.md](CONVERSATIONS.md#message-send-acceptance), [HISTORY_CONTEXT_ACCEPTANCE.md](CONVERSATIONS.md#history-context-acceptance), [CONVERSATION_TIMELINE_ACCEPTANCE.md](CONVERSATIONS.md#conversation-timeline-acceptance) | Production send, final Markdown, child dialogue and completed history restore without model calls or mutating-tool replay; file statistics refresh read-only. Fixed-length/TLS native streaming limits remain described in [FIXED_LENGTH_STREAM_ACCEPTANCE.md](PROVIDERS.md#fixed-length-stream-acceptance). |
| File read/edit/write and review | [FILE_CHANGE_SNAPSHOT_ACCEPTANCE.md](GIT_WORKTREES.md#file-change-snapshot-acceptance), [TASK_SNAPSHOT_REVIEW_ACCEPTANCE.md](CONVERSATIONS.md#task-snapshot-review-acceptance), [FILE_CHANGE_UNDO_ACCEPTANCE.md](GIT_WORKTREES.md#file-change-undo-acceptance) | Real writes, replacement, +2/-0 card and immutable snapshot diff; hashes verified. Undo/removal and storage-recovery acceptance remain separate, not exercised by this read-only review. |
| Shell execution, policy, cancellation and limits | [COMMAND_EXECUTION_ACCEPTANCE.md](TERMINAL_TOOLS.md#command-execution-acceptance), [TOOL_APPROVAL_LAYOUT_ACCEPTANCE.md](TERMINAL_TOOLS.md#tool-approval-layout-acceptance), [TOOL_PROGRESS_ACCEPTANCE.md](TERMINAL_TOOLS.md#tool-progress-acceptance) | Both parent/child commands use production execution/audit. Prior negative child read is denied before approval. This positive run deliberately does not automate permission prompts or exercise a manual approval click. |
| Embedded/external browser selection and task isolation | [BROWSER_EXECUTION_ACCEPTANCE.md](BROWSER_MCP.md#browser-execution-acceptance), [BROWSER_CONCURRENCY_ACCEPTANCE.md](BROWSER_MCP.md#browser-concurrency-acceptance), [BROWSER_TOOLBAR_ACCEPTANCE.md](BROWSER_MCP.md#browser-toolbar-acceptance), [CDP_TASK_ISOLATION_ACCEPTANCE.md](BROWSER_MCP.md#cdp-task-isolation-acceptance), [CDP_AGENT_WORKFLOW_ACCEPTANCE.md](BROWSER_MCP.md#cdp-agent-workflow-acceptance) | Earlier native managed Chrome evidence retains independent processes/profiles, targeted cancellation and restoration. New Headless production-UI settings/Agent evidence attaches two tasks to one explicit test-owned CDP context: foreign tabs are rejected, cancelling A closes its initial/popup pages only, B continues through real snapshot/PNG to completion, and fresh composition restores operable history without another model call. The latest workflow has 12 scripted model requests and ten complete audits; it does not replace native daily-profile/CDP or arbitrary-site acceptance. Context storage remains shared. |
| MCP and session management | [MCP_EXECUTION_ACCEPTANCE.md](BROWSER_MCP.md#mcp-execution-acceptance), [MCP_TOOL_IDENTITY_ACCEPTANCE.md](BROWSER_MCP.md#mcp-tool-identity-acceptance), [MCP_UI_RECOVERY_ACCEPTANCE.md](BROWSER_MCP.md#mcp-ui-recovery-acceptance), [WINDOW_SHUTDOWN_ACCEPTANCE.md](TERMINAL_TOOLS.md#window-shutdown-acceptance), [ASYNC_PROCESS_PIPE_ACCEPTANCE.md](TERMINAL_TOOLS.md#async-process-pipe-acceptance) | Earlier native stdio discovery/echo/restoration evidence remains. New production Headless MainWindow cases cover real editor/Send/approval, offline HTTP failure, explicit reconnect, safe error styling, late initialization cancellation and operable restored tool output/Markdown without tool/model replay. Both themes cover 1280×820 and 526×601; rejection branches retain zero tool HTTP calls. This does not establish native disconnected recovery, automatic transport resumption or every configuration race. |

#### UI and engineering gates

| Gate | Evidence and status |
| --- | --- |
| Layout, navigation/sidebar, conversation, composer, inspector and settings | Shared-control component tests and individual native acceptance files exist. Fresh production screenshots verify the child/file/browser/history combination. This is not a complete same-state visual sweep. |
| Geometry, icons/text alignment, colors and shadows | Component-specific measurements remain in sidebar/header/composer/settings/file-card acceptance records. Current passive samples exactly match title/rail/sidebar/main light-theme colors; complete pixel equality is not proven. |
| Hover/focus/disabled/loading/empty/error/expanded states | Independent rendered and interactive checks exist, including invalid settings and child/runtime errors. Timeline checks add running/cancelled/completed and restored disclosure states. [SETTINGS_STATUS_OVERFLOW_ACCEPTANCE.md](UI_WORKFLOWS.md#settings-status-overflow-acceptance) covers bounded long/loading/keyboard/short feedback. [SETTINGS_CATALOG_VIEWPORT_ACCEPTANCE.md](UI_WORKFLOWS.md#settings-catalog-viewport-acceptance) adds long/empty/single-row proxy/MCP catalogs, repeated resizing, native End navigation, actual last-row actions and visible feedback. [BUTTON_FOCUS_APPEARANCE_ACCEPTANCE.md](UI_COMPONENTS.md#button-focus-appearance-acceptance) adds rounded shared keyboard-focus rings across action, send, history, selected rail, conversation and settings variants, preserving pointer/disabled behavior and explicit focus ownership. No single current-version matrix proves every key surface/state; complete that sweep before visual sign-off. |
| Different window sizes and light/dark themes | Native component evidence covers narrow/normal/maximized and both themes. The integrated scenario covers light 1280x820; production browser concurrency/restoration adds light 1920x1032. [SUBAGENT_CONCURRENCY_NATIVE_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#subagent-concurrency-native-acceptance) adds Dark/96-DPI child history at 1920x1032, 1280x820 and 526x601, including actual compact scrolling/context toggle. These bounded branches do not prove a complete current-version responsive/theme/DPI matrix. |
| Visible controls have behavior | Prior combined/concurrency scenarios exercise send, inspectors, dialogue, snapshot review, task switching and targeted stop. [PRODUCT_INTERACTION_AUDIT.md](OVERVIEW.md#product-interaction-audit) adds 4,038 wiring occurrences across 160 synthetic page/state combinations plus search/dialog/numeric input paths. [PROVIDER_UI_WORKFLOW_ACCEPTANCE.md](PROVIDERS.md#provider-ui-workflow-acceptance) connects actual provider/model actions to discovery recovery, saved configuration, disabled submission rejection and one real protocol request/visible answer. [AUTOMATION_UI_WORKFLOW_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#automation-ui-workflow-acceptance) adds real editor/save/enable/pause/run/stop/history/delete controls, exact saved-request routing, retained foreground drafts and invalid/canceled/deleted-history boundaries in both themes and sizes. Remaining commands, menus and save/delete branches still require a complete whole-product behavior audit. |
| Dependency direction and shared tool pipeline | Current App composition and `SubagentToolGateway` use existing ports and executor. The combined audits independently show parent/child/MCP requests, including denial/cancellation records. |
| Secrets, approval, bounds, persistence and interrupted work | Loopback logs record only whitelisted flags/parameters. Artifact hashes, denied foreign read and no-replay restore are checked. Dedicated schema/output/path/timeout/cancellation/storage tests remain necessary and do not become native evidence automatically. |
| Modular files, shared controls and JIT/Windows x64 single-file output | Repository constraints remain binding; no changes to architecture boundaries, native wrappers, JSON or packaging in the combined acceptance work. Build/analyzer/component gates remain in place. |
| Build/tests/format | Latest case-set validation: all 4,784 discovered App cases pass across six disjoint class partitions; 581 class/count matches and unique TRX IDs verify coverage. Complete pixels 188 and Providers 542 plus Infrastructure proxy/HTTP subset 20 and architecture subset seven pass. Build has zero warnings/errors; scoped format/hygiene and eight test/runtime hashes pass. The same-process App attempt is interrupted without final TRX after an untouched sidebar allocation failure (0 expected, 1,200 measured); partitions do not replace that observation or prove a causal fix. The latest complete eight-assembly checkpoint remains historical 6,495/6,495 in [PRODUCT_INTERACTION_AUDIT.md](OVERVIEW.md#product-interaction-audit), predating subsequent changes. This is not a current all-green same-process/eight-suite checkpoint. Subsets and external audit occurrences are not added to historical totals. Native/Codex and full-product acceptance remain open. |

#### Next completion work

The historical same-state Codex Appearance reference and the first compact mode-row
correction are recorded in [APPEARANCE_MODE_PICKER_ACCEPTANCE.md](UI_COMPONENTS.md#appearance-mode-picker-acceptance). Matching 80x60
choice extents does not complete that page. Subsequent detail records correct
column/row geometry, and [THEME_THUMBNAIL_ARTWORK_ACCEPTANCE.md](UI_COMPONENTS.md#theme-thumbnail-artwork-acceptance) narrows miniature
artwork differences. Remaining title ink, thumbnail border/shadow/branding and
current-version references are separate gates. The following palette slice
addresses basic rows and default-collapsed advanced ownership, not whole-page parity.
The earlier AskEveryTime child-settings probe verifies real save/rejection and
parent catalog publication, but stops at unapproved delegation and explicit
cancellation. It remains unchanged, not promoted to successful approval. The
subsequent independent FullAccess settings-to-runtime branch below supplies
successful delegation evidence. Do not carry forward the earlier lack of any
Appearance reference, or promote either bounded scenario to full parity.

The final compact mode-picker checkpoint passes the complete 4,395-case App,
84-case pixel and fresh 119-case Agent suites: 4,598 without double-counting the
480-case related subset. The solution build has zero warnings/errors; scoped
formatting, explicit tracked/untracked whitespace checks and independent
TRX/assembly/native/data/normal-exit audits pass. Five other non-UI suites are
not rerun for this App-only slice. The independent candidate does not replace
the default launch output; nothing is staged, committed, pushed or released.

[APPEARANCE_PALETTE_LAYOUT_ACCEPTANCE.md](UI_COMPONENTS.md#appearance-palette-layout-acceptance) records the next hierarchy correction:
one effective-theme basic palette and a real default-collapsed advanced disclosure
replace the always-visible Diff and two full editors. Both theme drafts, actual
save/reset/font commands and native keyboard/compact access remain intact. Native
96-DPI center-line samples match the 226-pixel card height and row intervals, not
its absolute position, width, divider color, pills or complete page. A synthetic
ordinary-permission App restores Light, exits zero and preserves all nineteen
table hashes. The full product and successful current child-run gates remain open.

This palette slice's final complete App run passes 4,408 cases, with 1,622 passing
cases across the other seven complete assemblies: 6,030 without counting the
82-case focused subset twice. Both earlier complete runs that fail the obsolete
notification guard remain documented. The final solution build has zero
warnings/errors; scoped formatting, explicit tracked/untracked whitespace and
independent TRX/assembly/native/normal-exit checks pass. The seven other test DLLs
retain their tested identities after the guard rebuild. No default launch output,
staging, commit, push, release or installation is changed. Whole-page parity and
fresh successful child execution are not established by this checkpoint.

[APPEARANCE_DETAIL_FIDELITY_ACCEPTANCE.md](UI_COMPONENTS.md#appearance-detail-fidelity-acceptance) records the subsequent shared settings
column/heading correction, compact editable color pills and reusable conversation
thumbnails. Same-state 96-DPI native samples now match the selected divider
positions/colors and 96x28 background-pill outline. The title's horizontal ink
bounds match, but its top remains two pixels lower and raster height one pixel
smaller. Accent/font selectors, thumbnail artwork, preset actions, remaining
states/DPI and whole-page/product parity are not signed off.

Final complete App 4,414 and the other seven complete assemblies 1,622 pass:
6,036 without counting the 466-case focused subset twice. Solution build has
zero warnings/errors; scoped formatting and explicit tracked/untracked whitespace
checks pass. Independent TRX/assembly/native/hash verification confirms the final
candidate rather than reusing the previous checkpoint. Eleven passive captures
cover both themes, maximized/normal/minimum windows, real keyboard invalid-color
rejection/reset and compact advanced/font scrolling. The App exits normally,
restores Light and preserves all nineteen synthetic table hashes. No approval
settings, user data, JIT/JSON, packaging or default launch output changes. Nothing
is staged, committed, pushed, released or installed. Successful current child
execution and the complete product acceptance gates remain open.

[APPEARANCE_SELECTOR_ACCEPTANCE.md](UI_COMPONENTS.md#appearance-selector-acceptance) records the next shared pill-selector slice.
The accent swatch now belongs to the selector's hit area; native keyboard/popup,
accessibility and preference/error behavior are retained. The closed default font
uses a localized compact label while the popup keeps full labels. Same-state
96-DPI sampled accent/default-font outlines now match 78x28 and 60x28 respectively.
Native keyboard navigation exposed Avalonia's thick fallback focus frame; an
explicit item-container theme removes that frame without hiding keyboard focus.
Text, arrows and complete-page pixels are not asserted equal.

Final complete App 4,428 and the other seven complete assemblies 1,622 pass:
6,050, excluding the 582-case focused subset from the total. Fourteen new cases
cover geometry, keyboard focus, swatch/disabled input, independent theme choices
and save rejection. The final solution build has zero warnings/errors; scoped
formatting, explicit untracked whitespace checks, physical source-size limits and
independent TRX/assembly/native/hash audits pass. Nineteen passive main captures
and five full popup images cover real saves/resets in both themes, maximized,
normal and minimum-size windows. Normal close preserves all nineteen synthetic
table hashes and produces zero artifacts. Final Light settings explicitly retain
Default font entries for both themes; they are not described as byte-for-byte
original preferences. No approval/user-data/JIT/JSON/packaging/default-launch
change, staging, commit, push, release or installation occurs. At that checkpoint,
high DPI, title/thumbnail details, preset actions, whole-product states and current
successful child-runtime/PI-Desktop gates remain open; the full objective stays active.

[SUBAGENT_SETTINGS_TO_RUNTIME_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#subagent-settings-to-runtime-acceptance) closes the next bounded current
successful child-settings/runtime gate without changing production code. The real
editor saves name, trigger, system prompt, model and high effort, then two actual
sends exercise sharing off and on through the production Responses/Gemini adapters
and tool pipeline against a deterministic loopback fixture. Each child performs a
new authorized read and returns live/final dialogue to its parent. The old manual
approval probe and all existing policies remain untouched; the new task is created
with FullAccess from the outset.

This candidate's solution build has zero warnings/errors; a fresh 152-case related
App subset and complete 119-case Agent suite pass (271, not a rerun of 6,050).
Fifteen passive ordinary-permission 96-DPI captures cover settings/live/completed
states and no-replay restore at maximized, normal and minimum sizes. Four successful
audits, two final child payloads, two parent answers, zero artifacts, unchanged
foreign-file bytes and released App/fixture processes are verified. All nineteen
tables and eight request records remain stable across normal close/reopen and
restored dialogue interaction. This does not close paid-service/all-protocol,
manual-approval, concurrent-child, dark/high-DPI, PI-Desktop or whole-product
visual/interaction gates; nothing is staged, committed, pushed or released.

[APPEARANCE_THUMBNAIL_ACCEPTANCE.md](UI_COMPONENTS.md#appearance-thumbnail-acceptance) records the next shared rendered correction.
The real 80x60 mode buttons now contain full-size scenes instead of 72x52 artwork;
the mixed System seam is square inside one rounded frame, and focus/selection
overlays do not resize content. Native keyboard verification exposes and repairs
low contrast on dark artwork, with explicit indicators for each depicted mode.
Same-state three-region mean RGB errors drop from 21.44/11.74/56.16 to
3.61/4.15/4.22; these regional metrics are not whole-window equivalence.

The final related App group passes 834 cases and the other seven final complete
assemblies pass 1,622 (2,456 fresh, not a complete App run). The final solution
build has zero warnings/errors; scoped formatting, whitespace and source-size
checks pass. Twelve passive ordinary-permission 96-DPI captures cover keyboard
focus/selection, both themes, maximized/normal/minimum windows and actual saved
mode changes. Normal close preserves nineteen table hashes, zero artifacts and
the exact original Light/default-font preferences. The earlier candidate's serial
complete App run also finishes successfully with 4,437 cases and verified original
identities; it is not promoted to the final binary or added to its test total.
Whole-page/state/DPI, presets/actions and PI-Desktop gates remain open.

1. Complete the current-version interactive surface/state matrix in light/dark
   and narrow/normal/maximized windows, including disabled and failure recovery.
2. Obtain equivalent reference states for whole-window Codex comparisons and the
   custom provider/model page's PI-Desktop reference. Passive mismatched screenshots
   and matching component samples alone cannot prove one-to-one visual fidelity.
3. Complete the whole-product interactive-control audit rather than promoting
   the verified combined and concurrency controls to all visible surfaces.
4. Re-run final relevant tests/build/format after any resulting product changes,
   then audit each explicit requirement and these remaining gates before declaring
   the objective complete. Preserve dirty work; do not stage/push/release implicitly.

Fresh successful and rejected integrated scenarios, artifact/version details and
their limitations are in [INTEGRATED_WORKFLOW_ACCEPTANCE.md](OVERVIEW.md#integrated-workflow-acceptance).
The current production concurrency gate is verified separately in
[BROWSER_CONCURRENCY_ACCEPTANCE.md](BROWSER_MCP.md#browser-concurrency-acceptance); no older host or binary is promoted silently.

The subsequent progress-text/message-lookup optimization and shared tool-row
geometry are recorded in [TOOL_PROGRESS_ACCEPTANCE.md](TERMINAL_TOOLS.md#tool-progress-acceptance). The code optimization
passes a serial 4,019-case App run; after the last icon/spacing calibration, the
final focused App group passes 128 cases, the complete pixel assembly passes 12,
and architecture constraints pass seven. Four synthetic native component captures
match the light reference's 14-by-14 terminal ink extent. They do not replace the
production concurrency binary or establish whole-window/theme/state equivalence;
the initial default-parallel test failure and cancelled run remain documented.

The subsequent workspace lookup/tab reconciliation refactor is recorded in
[WORKSPACE_LOOKUP_OPTIMIZATION_ACCEPTANCE.md](ENGINEERING.md#workspace-lookup-optimization-acceptance): 13 new focused cases, 491 wider
workspace/sidebar/history/header cases, a complete serial 4,032-case App run and
seven architecture cases pass, followed by a zero-warning/error solution build.
It changes no visual markup or execution policy and supplies no fresh native
matrix or reference screenshots; the visual completion gates remain open.

The subsequent native sweep and shared dialog-body reopen repair are recorded in
[SETTINGS_DIALOG_REOPEN_ACCEPTANCE.md](UI_WORKFLOWS.md#settings-dialog-reopen-acceptance). Fresh ordinary-permission light/dark
980x640 model editors pass open/scroll/cancel/reopen, including a same-model dark
before/after comparison and native Escape. The repaired replay preserves task,
audit and artifact records, restores System theme and closes normally. The new
and existing dialog/focus group passes 34 cases, the wider settings/editor/shared
control group passes 779, the complete serial App suite passes 4,043, the visual
assembly passes 12 and architecture passes seven; solution build and scoped
whitespace checks pass. Earlier normal/maximized captures belong to the before
binary, not this repaired version. This is a bounded lifecycle repair and partial
state sweep, not whole-window Codex equivalence or full-product completion.

The subsequent production scheduler evidence is recorded in
[AUTOMATION_LIST_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#automation-list-acceptance): one automatic success, manual cancellation with
observed server disconnect, synthetic HTTP 503 failure and explicit new-run
recovery. All four belong to their recorded original App assembly and saved
ReadOnly project/model configuration. Read-only restoration on subsequent UI
builds preserves terminal runs, audits, artifacts and request counts; it is not
new execution evidence for those builds.

The latest caption correction is recorded in
[WINDOW_CAPTION_FIDELITY_ACCEPTANCE.md](UI_WORKFLOWS.md#window-caption-fidelity-acceptance). Close-icon size and hover red are corrected
through shared styles; final focused tests pass 44 and the complete eight-suite
regression passes 5,655 with no failures/skips, plus a zero-warning/error solution
build. The earlier native light/dark window-state sweep belongs to a different
assembly. At that checkpoint Sky pixels disagree with target metadata; passive PrintWindow
has black padding and unequal native dimensions. Inputs stop and invalid frames
are rejected, so the latest native theme/size/hover/interaction gate is still
open. Neither full-product completion nor one-to-one visual fidelity is claimed.

The subsequent shared rounded-square sidebar glyph and fresh production replay
are recorded in [WINDOW_SIDEBAR_ICON_ACCEPTANCE.md](UI_WORKFLOWS.md#window-sidebar-icon-acceptance). Native equal-size light frames
now match measured toggle/minimize/restore/close ink bounds and sampled shell colors;
native light/dark close hover, inspector/context-card toggling, normal/maximize and
minimize/restore, saved Completed history navigation without replay, unchanged
audits/artifacts/requests and normal zero-exit cleanup are verified on the recorded
isolated App build. Final App focus group passes 29, pixel assembly 24 and the full
eight-suite regression 5,667 without failures/skips; build and format pass. The
default-output checkpoint App is not substituted for that isolated native binary.
Current native 980x640/high-DPI, complete interactive state coverage, dark Codex and
PI-Desktop reference equivalence, and full-product completion gates remain open.

The subsequent shared danger-button palette correction is recorded in
[DANGER_BUTTON_THEME_ACCEPTANCE.md](UI_COMPONENTS.md#danger-button-theme-acceptance). The before default-output assembly adds
980x640 light inspector and dark settings/dialog-reopen evidence. The repaired
assembly independently verifies native light/dark provider rows and safe light
hover; equal-size unscaled crops show the corrected light palette, unchanged
model metadata and unchanged selected dark button pixels. Its 152-case relevant
group, 24-case pixel assembly, seven architecture cases, solution build and scoped
formatting pass. Earlier complete-regression/native binaries are not promoted to
this repair, and transient DPI changes plus a rejected black screen frame do not
complete the higher-DPI gate. Catalogs, audits/artifacts and request counts remain
unchanged; both QA Apps exit zero and observers stop normally. Complete native
state/reference/control coverage and the full product goal remain open.

The subsequent shared task-menu lifetime correction is recorded in
[TASK_MENU_LIFETIME_ACCEPTANCE.md](CONVERSATIONS.md#task-menu-lifetime-acceptance). Owner/context/command invalidation and
reattachment pass twenty cases within a 420-case relevant group. Current-build
native pin/unpin, search no-match/Escape/reopen/clear, sidebar dismissal/recovery,
unchanged data/audits/artifacts/requests and normal zero-exit cleanup are verified
on an independent backup. Equal-size light Codex shell crops match five sampled
colors and selected icon/heading bounds, with a remaining project-heading ink
edge difference; content/state equivalence is not claimed. The first complete
eight-suite regression has one existing allocation-assertion failure; its
separate five-case recheck passes. That failure remains recorded, not concealed.
The final sequential eight-suite recheck passes 5,697/5,697 without failures or
skips, including all twenty new lifetime cases and the existing allocation test.
The final solution build has zero warnings/errors and scoped formatting passes;
the production assembly still matches the recorded native replay hash.
Full-product and complete native theme/DPI/control/reference gates remain open.

The subsequent shared page-focus selection optimization is recorded in
[PAGE_FOCUS_OPTIMIZATION_ACCEPTANCE.md](ENGINEERING.md#page-focus-optimization-acceptance). One lazy traversal replaces eager
whole-page candidate materialization without changing focus priority or visual
markup. Five selector and eighteen Headless window-resize cases are added;
the related group passes 66 and the final eight-assembly regression passes
5,720/5,720 without failures/skips. The first parallel App run's existing
104-byte allocation failure is preserved separately, not recast as a pass.
Solution build has zero warnings/errors and scoped formatting passes. The
recorded isolated JIT binary verifies fourteen native light/dark settings-page
states, editor cancellation/reopen, search, scrolling and five dark inspector
tabs plus light Audit. All nineteen saved database tables and original artifacts
remain unchanged, no new request is sent, and App/observer exit zero. The earlier
native resize clipping has no established root cause; fresh normal-size replay
and Headless resize tests do not establish a repair. Default output is not
updated. Equivalent references, native narrow/high-DPI/full-state acceptance
and the complete product objective remain open.

The subsequent caption keyboard-focus repair is recorded in
[WINDOW_CAPTION_FOCUS_ACCEPTANCE.md](UI_WORKFLOWS.md#window-caption-focus-acceptance). Native minimum-size replay discovers that
maximize/restore takes focus out of the open model editor, preventing Escape
closing. The shared caption theme disables keyboard focus without changing
pointer commands, disabled-command rules, labels, hit areas or appearance.
The sixteen-case baseline fails ten and passes six preservation cases; the
repaired related group passes 54/54. Current-build native light/dark editors
retain input focus across 980x640/maximized/980x640 and close with Escape; the
actual close caption exits zero. Both QA observers stop normally, nineteen
saved tables and original artifacts remain unchanged, and no new request is
sent. Final eight-assembly regression passes 5,736/5,736 without failures/skips;
solution build has zero warnings/errors and scoped formatting passes. One paired
cold light state remains pixel-identical before/after. Fresh Codex shell sampling
matches four colors but finds a one-RGB-unit rail/shadow difference, which remains
open rather than being hidden by a flat-color workaround. The earlier native
content-clipping cause remains unproven; complete references, repaired-build
page/state/high-DPI acceptance and the full objective remain open. Default output,
AOT, JSON and packaging are unchanged; no commit, push or release occurs.

The subsequent shared workspace-frame shadow correction is recorded in
[WINDOW_FRAME_SHADOW_ACCEPTANCE.md](UI_WORKFLOWS.md#window-frame-shadow-acceptance). Own-frame and ancestor clipping are separated
from rounded content clipping; a measured window-only shared token leaves all
card/dropdown shadows unchanged and remains restricted by the build guard.
Final focused App tests pass 120/120, the pixel assembly 38/38, and solution
build/format pass. Fresh equal-size light native rail samples reduce maximum
RGB error from 11 to 1, with remaining upper-shadow differences; no whole-window
equivalence is claimed. The final native restore reproduces the earlier defect:
1280x820 client bounds still display clipped maximized content in both PrintWindow
and screen captures. Theme switching remains responsive but does not repair it.
Native resize acceptance fails and its root cause remains open; Headless passes
and prior full-suite evidence do not close this gate or the complete objective.

[NATIVE_RESIZE_DIAGNOSTIC_PROGRESS.md](UI_WORKFLOWS.md#native-resize-diagnostic-progress) records a fresh original-binary restore
failure, healthy full/reduced telemetry and standalone/hook controls, unchanged
fixture data and a screenshot-anchor gate that rejects two non-black but clipped
frames. Normalized method/resource comparisons do not establish a build or
timing cause; healthy replays are not a repair. Native resize and complete
reference/state/DPI acceptance remain open. Diagnostic scripts remain external,
and no product sizing or execution-policy workaround is introduced.

The subsequent settings shell/navigation correction is recorded in
[SETTINGS_SHELL_ALIGNMENT_PROGRESS.md](UI_WORKFLOWS.md#settings-shell-alignment-progress): 71 focused App cases, a serial 1,466-case
wider settings/navigation/inspector/shared-boundary group and all 48 pixel cases
pass, plus seven architecture constraints, a zero-warning/error solution build
and scoped whitespace verification.
Ten new pixel cases use actual Headless DPI notifications and compositor frames,
not enlarged 96-DPI bitmaps. Four light shell color samples match the passive
Codex General reference exactly. The Moss frame is Headless Appearance, not a
same-state native comparison. Fresh native input stops after pixels disagree
with target metadata; black-padded captures are rejected. The prior restore
defect, equivalent Appearance/PI-Desktop references and complete native matrix
remain open. No default executable, JSON, AOT, packaging or execution policy is
changed; no commit, push or release occurs.

The subsequent settings conversation/search correction is recorded in
[SETTINGS_NAVIGATION_INTERACTION_ACCEPTANCE.md](UI_WORKFLOWS.md#settings-navigation-interaction-acceptance). Successful shortcut/menu/task/
history navigation leaves settings through the existing catalog event; rejected
targets, background updates, unsent messages and provider drafts remain protected.
Search hides empty groups without selecting another page. The final complete
eight-assembly results pass 5,804/5,804 with no failures/skips, including all 4,218
App and 48 pixel cases; solution build and scoped formatting pass. The first
complete App run's six stale outer-frame clipping expectations remain recorded;
their corrected tests prove equal-bounds inner clipping without reverting the
shadow repair. Fresh exported frames again match four light General-shell samples,
not native Appearance or whole-window equivalence. Target pixels still disagree
with MossAgent metadata, black-padded captures are rejected, and the old preview
has not been confirmed closed. Native restore, equivalent references, complete
interactive/DPI state coverage and the full product objective remain open.

[NATIVE_SETTINGS_INTERACTION_PROGRESS.md](UI_WORKFLOWS.md#native-settings-interaction-progress) supersedes that preview-status limit:
verified foreground binding is recovered, the old preview closes normally, and
the recorded navigation candidate passes independent native search, new/return,
history back/forward, hidden-draft submission isolation and 96-DPI theme/size
checks without any model request or stored-row change. A two-native-frame shell
comparison matches four palette samples and search/selection bounds, but retains
the General-versus-Appearance limitation. It exposes a title-weight mismatch and
drives a shared Bold title regression/correction. Failed-state CLR geometry and
an eight-case synthetic notification simulation are recorded separately; neither
healthy replay nor synthetic Resized delivery repairs the native resize defect.
Equivalent references, provider draft/background-update native checks and the
complete high-DPI/state matrix remain open; the product goal is still active.
The final shared-title checkpoint passes 42 focused and all 48 pixel cases,
plus a zero-warning/error solution build and native light/dark max/normal replay.
Its title ink bounds align with the reference without moving search/selection;
glyph rasterization remains different. This newer title assembly is not promoted
to the previous complete 5,804-case checkpoint or whole-page pixel equivalence.

The subsequent provider/model edit-session correction is recorded in
[PROVIDER_MODEL_EDITOR_SESSION_ACCEPTANCE.md](PROVIDERS.md#provider-model-editor-session-acceptance). Native cancellation/reopen reveals
discarded fields and stale provider validation; shared catalog-validated edit
entry points now reload saved values only for a fresh session. Twelve focused
cases, 311 related cases, seven architecture constraints and all 48 pixel cases
pass, plus a zero-warning/error solution build and scoped formatting. This is
not a new complete App/eight-suite run. Independent ordinary-permission native
replay verifies page/theme draft and focus retention, provider/model cancellation
recovery and a usable 96-DPI 1280x820 dialog. App/observer exit zero, all nineteen
tables and original artifacts remain unchanged, no request is added and light UI
settings are restored. The earlier native resize cause is not repaired; equivalent
Appearance/PI-Desktop references, native background-update/high-DPI/full-state
checks and the full objective remain open. Default output, packaging, AOT, JSON
and approval behavior are unchanged; no commit, push or release occurs.

[NATIVE_RESIZE_MESSAGE_PROGRESS.md](UI_WORKFLOWS.md#native-resize-message-progress) adds a same-binary external observational hook,
29 bounded geometry records and eight clean native light/dark frames spanning
1920x1032, 1280x820 and 980x640. Nine WM_SIZE notifications match actual client
geometry; callback-time Bounds lag converges. Zero DPI/scaling notifications
occur, so this remains healthy 96-DPI evidence, not the earlier failure history,
native high-DPI acceptance or a repair. Forty-three focused window/layout cases
and a zero-warning/error solution build pass; no fresh full-suite run is claimed.
Independent data/artifact/request/UI audits pass and both processes exit zero.
The original native clipping and equivalent settings/full-product gates stay open.

[DEFAULT_UI_FONT_FIDELITY_ACCEPTANCE.md](UI_COMPONENTS.md#default-ui-font-fidelity-acceptance) adds installed-Codex CSS evidence for the
Windows system-font default and changes shared UI/uncustomized content resources
from implicit Inter to Segoe UI. Explicit Inter remains selectable; existing
preference identities, JSON adapter and saved default files stay intact. The
shared layout host now resolves real Skia fonts rather than substituting a
monospaced BareMinimum system face; the failing metrics guard and complete-run
font-dependent fixture failures remain documented, not hidden by larger controls.
Focused font/layout and metrics/scroll/toolbar corrections pass 54 and 17 cases;
all 48 pixel and seven architecture cases pass. Final complete App verification
also passes all 4,242 cases with no failures/skips/aborts; its bounded recovery driver
exits zero and the TRX agrees. The earlier missing-terminal-result attempt is not
counted as a pass. This is a complete App-assembly checkpoint, not a new complete
multi-assembly or full-product acceptance run. Two ordinary-permission independent native runs verify
96-DPI default light/dark max/min, dark normal and explicit light Inter max/normal
states and expected independent persistence. Both App/observer pairs exit zero;
all 19 tables, three original artifacts and four-request history stay unchanged.
Only the option fixture saves its expected light Inter preference. Nine unscaled
native crops distinguish font change from full equivalence; the fresh Codex
reference remains a conversation and the cached settings title is from General.
Equivalent Appearance/PI-Desktop pages, glyph rasterization, high-DPI/clipping,
the complete current surface/state matrix and the product objective remain open.
No AOT, packaging, execution-policy, commit, push or release change occurs.

A subsequent fresh passive reference (`codex-recovery-passive.png` in the external
font evidence root) shows Codex's conversation at 526x601, 96 DPI with its sidebar
collapsed. Current MainWindow's declared minimum remains 980x640. Add real narrow
window/sidebar/header/composer/settings and inspector adaptation to the remaining
responsive work; the healthy 980x640 samples do not satisfy this observed reference
or repair the separate native DPI/clipping issue.

[NARROW_WORKSPACE_LAYOUT_ACCEPTANCE.md](UI_WORKFLOWS.md#narrow-workspace-layout-acceptance) starts that responsive work with the shared
pane host, not a reduced MainWindow minimum. Effective full-width inspector state
is separate from user expansion preference; compact layout retains the draft and
preferred split, rejects unusable restore toggles and keeps real header close
actions available. Sixteen pane and four real-consumer cases join the passing
377-case related group, all 48 pixel cases and seven architecture constraints.
The final solution build has zero warnings/errors, scoped formatting and diff
whitespace checks pass. The complete newer App run finishes with all 4,262 cases
passing and zero failures/skips/aborts; final driver exit zero and TRX counters
agree. This is an App-assembly checkpoint, not a new complete multi-assembly run.
Independent ordinary-permission native light replay verifies max/normal dock,
explicit expand, restore and close. App/observer exit zero, all nineteen table
fingerprints and original artifacts/request history/UI bytes remain unchanged.
Input highlights/hover tooltips in immediate frames are excluded from visual
equivalence claims. Native compact navigation/settings/composer coverage,
same-state reference pages, high-DPI/clipping and the full objective remain open.

[WINDOW_CAPTION_CONTRAST_ACCEPTANCE.md](UI_WORKFLOWS.md#window-caption-contrast-acceptance) follows with a shared caption-only palette
and corrected variant load order, retaining hit areas, commands, cursor and focus
behavior. Native palette-only evidence leaves ordinary maximize at luminance 84.
External alpha profiles isolate the round-cap raster fringe; its shared normal
variant now uses Size=9, flat caps and half-pixel alignment, while alternate
restore/minimize/close and generic icon rendering retain their own presentation.
All 53 focused App/window cases, 66 pixel cases and seven architecture constraints
pass, with zero-warning/error solution build and scoped formatting. The newer
complete App gate finishes with 4,271 passes and zero failures/skips/aborts;
terminal driver exit zero and TRX counters agree. The six complete non-UI groups
also pass 1,538 cases, and their DLL hashes remain unchanged after the final
build. Candidate identities and independent counter checks establish an eight-
assembly 5,875-case regression checkpoint; architecture is already included.
Fresh independent native light/dark normal/max replay verifies maximize/restore,
real minimize/reactivation and normal close. Table/artifact/request/restored-Light
audits pass after process exit; App/observer exit zero and the port is released.
Unscaled ordinary-caption ink stays 10x1 / 10x10 / 10x10 and corrected light
luminance is 31/31/31 against fresh passive Codex reference 31/31/31, rather than
palette-only 31/84/31. These are sampled extents/contrast, not full caption pixel
equality or a completed same-state Appearance/high-DPI/full-product checkpoint.

[COMPACT_NAVIGATION_ACCEPTANCE.md](UI_WORKFLOWS.md#compact-navigation-acceptance) replaces the workspace/settings fixed sidebar
grids with one shared navigation host. MainWindow now permits 526x601; the
624-DIP host breakpoint derives from 264-DIP navigation and 360-DIP primary
minimums, not a claimed Codex measurement. Compact drawers retain wide sidebar
preference, editor/control instances, draft/focus and explicit inspector intent.
Selection, outside left click, contained Tab and unconsumed root Escape use real
interaction paths; hidden/disabled/detached or replaced hosts invalidate commands
and pending focus. Shared dialogs constrain both dimensions and retain fixed
footers, while automation list/history stack without replacing their scrollable
views. Existing approval, provider, JSON/JIT and single-file-release boundaries
remain unchanged.

The first complete App run records three old fixed-grid guard failures, not a
pass. Updated shared-ownership guards and the final full run pass all 4,319 App
cases with zero failures/skips/aborts; 86 related, 72 pixel and seven architecture
cases also pass. Six complete non-UI groups pass 1,538 cases with hashes unchanged
after final build. Independent driver/TRX/identity checks establish an eight-
assembly 5,929-case checkpoint; subsets are not counted twice. Scoped formatting,
zero-warning/error solution build and diff whitespace checks pass. Ordinary-
permission native replay verifies 526x601 light/dark navigation/settings/dialogs,
automation list/history and scrolled editor/footer, then widening to 1920x1032
restores the sidebar and horizontal history layout. App/observer exit zero, port
51792 is free and table/artifact/request/restored-Light UI audits remain unchanged.
Unscaled initial 526x601 Codex/MossAgent frames show matching viewports but
different conversation states. The later passive Codex reference is 600x750 and
still a conversation, not Appearance. Equivalent Appearance/PI-Desktop states,
the separate native clipping/high-DPI issue, full surface matrix and overall
product acceptance remain open; this checkpoint does not complete the goal.

[SETTINGS_VIEWPORT_FOCUS_ACCEPTANCE.md](UI_WORKFLOWS.md#settings-viewport-focus-acceptance) isolates compact Appearance heading loss
from the older native geometry mismatch. Automatic restoration of an offscreen
palette field caused a 96-DIP scroll jump; eight production-window cases first
fail, then the shared page/drawer policy restricts automatic targets to every
ancestor viewport. Sixteen new tests cover initial/dismissal paths in both
themes, partial and nested clipping, and retained visible editor draft/scroll.
Explicit user focus and scrolling remain available; no UI control is rebuilt,
scroll position forcibly reset, tool/approval/persistence policy changed, or
native resize workaround added.

Final complete App and visual suites pass 4,335 and 72 cases respectively with
zero failures/skips/aborts; seven architecture constraints, scoped formatting,
zero-warning/error solution build and diff whitespace checks also pass.
External driver/TRX/hash/native-identity checks agree; focused cases are subsets
and the six complete non-UI suites were not rerun for this App-only slice.
Passive native captures verify compact headings and visible-editor restoration
at recorded 96/120-DPI viewports, with unchanged table/artifact/request/restored-
Light audits and ordinary App/observer exit zero. A wide Sky maximize frame is
not accepted because the subsequent native Python frame reports normal bounds.
The unscaled before/after title comparison discloses the one-pixel height and
scroll/hover differences. Final passive Codex is maximized but remains a
conversation, not Appearance. Same-state settings fidelity, separate native
geometry/DPI diagnosis and the complete product goal remain open.

[GLOBAL_APPROVAL_PRESENTATION_ACCEPTANCE.md](TERMINAL_TOOLS.md#global-approval-presentation-acceptance) adds real-window coverage for approval
requests that arrive while settings are open. The one retained approval view now
belongs to the global window layer, not the hidden workspace page. Full-page
settings allow Tab to reach it without closing settings or clearing drafts;
other switchers and modal dialogs keep their existing navigation behavior.
Fourteen new real-window cases cover both themes, workspace/settings/automations/
inspector, compact resize, queued cancellation, stale identities and shutdown
input priority. Synthetic native captures confirm normal light and compact dark
presentation with unchanged request policy; all final hosts cancel both requests
and exit zero without tools or automated native approval clicks. Latest Codex
still shows a conversation, not Appearance: four matching surface-color samples
do not establish whole-page equivalence. The full goal and geometry gaps stay open.

This slice's final serial complete App run passes 4,351 cases; complete pixels pass
72, and the architecture subset passes seven. The final focused 78 cases are
already included in App. Solution build has zero warnings/errors; scoped format,
diff whitespace, TRX/driver/hash/native identity and cancellation/exit checks pass.
Six full non-UI assemblies are not rerun for this UI-only slice. Nothing is staged,
committed, pushed or released, and this checkpoint does not complete the goal.

[SIDEBAR_BRAND_FIDELITY_ACCEPTANCE.md](UI_WORKFLOWS.md#sidebar-brand-fidelity-acceptance) records the later shared brand-heading and
chevron correction. Unscaled native light crops now match the selected reference
chevron's 6x3 ink size and vertical center; different title strings are not treated
as equal-font proof. Final related App tests pass 327, complete pixels 78 and the
architecture subset seven, with clean build/format checks. The preceding 4,357-case
complete App run belongs to the separate 5-DIP intermediate assembly, not the final
6-DIP build. Native light/dark menu and normal/maximized evidence is bounded to this
surface. The final App closes normally; App/observer exit zero, their processes
and listener disappear, and the restored-Light table/artifact/request/UI audit
remains unchanged. This independent build does not replace the default launch
output. Real DPI history, same-state full references and the whole goal stay open.

[PROJECT_TASK_PREVIEW_ACCEPTANCE.md](UI_WORKFLOWS.md#project-task-preview-acceptance) adds the missing per-project five-task preview
and real expand/collapse action while preserving complete catalog/history data,
row identities, search results and newly selected hidden-task visibility. Native
light/dark and normal/maximized replay verifies this surface with unchanged
table/artifact/request/restored-Light audits and normal zero-exit cleanup. The
unscaled expansion-label sample now matches reference ink size, inset, relative
baseline and dominant color, not all pixels or whole-window content. Final complete
App 4,373, pixels 84 and six non-UI suites 1,538 pass: 5,995 without counting the
17-case focused subset twice. External checkpoint verification confirms report
and assembly identities, the native App hash, capture metadata, normal cleanup
and restored data audits. Solution build, scoped formatting and workspace diff
checks pass. The earlier stale-binding guard failure remains documented.
Full-product, equivalent-reference and native DPI/geometry gates remain open.

[COMPACT_WELCOME_LAYOUT_ACCEPTANCE.md](UI_WORKFLOWS.md#compact-welcome-layout-acceptance) records the next real minimum-window defect:
saved empty conversations placed their three-row heading under the composer and
their render-shifted brand against the context card. Shared content-driven prompt
layout now uses two rows when needed; a shared empty-session panel accounts for
the preferred brand offset without changing normal-size positions. It preserves
the project action, font sizes, context card, approvals, data and JIT/JSON behavior.

An initially incorrect stretched-host test boundary is corrected before accepting
the red baseline. Intermediate geometry failures and a complete-run failure of an
old StackPanel source guard remain documented; none is counted as a passing run.
Final complete App 4,388, pixels 84 and six non-UI suites 1,538 pass: 6,010 across
eight assemblies, excluding the 53-case focused subset from the total. The final
solution build has zero warnings/errors; scoped format and diff whitespace checks
pass. External TRX/assembly/native identity verification produces a separate
versioned checkpoint, not a reuse of the preceding 5,995-case result.

Ordinary-permission native replay verifies 526x601 in both themes, the project
picker, real grip resize and maximize/restore. All seven captures use the final
App hash; the unscaled wide brand/heading crop has zero changed pixels against
the preceding production version. Restored-Light table/artifact/request/UI audits
and normal App/observer cleanup pass. Fresh passive Codex still shows a conversation,
not equivalent Appearance or empty-conversation content. This fixes the compact
heading only: actual DPI transitions, native geometry diagnosis, same-state full
references and overall product acceptance remain open. No default launch output,
user data, staging, commit, push, release or installation is changed.

[SETTINGS_TYPOGRAPHY_ACCEPTANCE.md](UI_WORKFLOWS.md#settings-typography-acceptance) records shared 14-DIP titlebar-menu and
visual-style-heading typography, compensating insets/padding and unchanged card
anchors. Native Python comparisons verify heading size/position and File ink
against the saved Appearance reference, not whole-page equality. Ten settings
captures cover both themes, three sizes and real menu/drawer behavior; a separate
workspace input diagnostic does not certify the rail-settings click path.
Normal zero-exit cleanup and unchanged table/artifact/restored-preference audits
pass, as do the zero-warning/error build, scoped format and source checks.

The complete run remains 6,096/6,099, not all-green. Two old source guards are
corrected and pass in the final 30-case focused run; the unrelated 3,112-byte
allocation failure passes three isolated six-case reruns without weakening its
assertion. The earlier 114-case subset and all reruns are not added to the total.
Complete App is not rerun after test-only guard updates. External verification
keeps these assembly/report identities distinct and confirms the native App hash.
Residual title/menu/palette typography, current-version same-state references,
rail-input verification, DPI transitions and full-product acceptance remain open;
no staging, commit, push, release or installation occurs. The goal stays active.

[SETTINGS_ENTRY_ACCEPTANCE.md](UI_WORKFLOWS.md#settings-entry-acceptance) records the follow-up to that rail-input gap:
native screenshots reproduce a pointer-positioned tooltip covering the settings
button center while an uncovered edge remains clickable. A shared right-side
tooltip placement leaves eight DIP clear of the entire button. Fourteen final
native frames verify center-click settings/workspace transitions, Home return,
idempotent Ctrl+Comma, real shortcut help, both themes and three window sizes.
Both independent processes exit normally; table hashes, artifacts and restored
preferences remain unchanged. Deterministic tests retain modal routing and
pending approval identity. This resolves that entry-path evidence gap, not
remaining same-state Codex references, DPI transitions or full-product acceptance.

This follow-up's final complete run is 6111/6115, with four failures, no skips or
aborts, and all eighty related cases (sixteen new) passing within that total.
The unrelated failures concern native resize waiting, child-process cancellation,
MCP stdio reload and Gemini/SOCKS5 rejected-credential observation. The three
Windows paths pass a separate five-case rerun without changing tests or code.
All twelve rejected-credential protocol/proxy cases also pass an isolated rerun.
Those diagnostics do not make the complete run all-green. Final build has zero
warnings/errors; scoped format, source/assembly/hash and native data audits pass.

[SUBAGENT_CONCURRENCY_NATIVE_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#subagent-concurrency-native-acceptance) closes the current production
two-task child cancellation/restoration gate. Independent roots, saved model
preferences and shared-context markers reach actual Responses-parent/Gemini-child
requests and file reads. Both children run simultaneously; stopping A disconnects
only A and retains its partial dialogue, while B remains running, reads another
authorized file and completes. Eight model POSTs, five unique task-owned audits,
zero artifacts, unchanged files and nineteen-table/no-request-replay restoration
are independently checked. Sixteen passive Dark/96-DPI native captures cover
maximized/normal/minimum windows. Both App processes exit zero; a loopback idle
keep-alive disconnect diagnostic is retained separately from App stderr.

The same candidate passes 124 App subagent cases and all 119 Agent cases, plus
another zero-warning/error solution build and whitespace/identity checks. These
243 fresh cases are not added to or substituted for the preceding non-all-green
6,111/6,115 full run. Current same-state Codex fidelity, complete responsive/DPI
coverage, manual-approval success and complete-product acceptance remain open.
No production code, policy, user data, JIT/JSON or packaging changes are made.

#### Shared metadata and compact approval checkpoint

[UI_METADATA_POLISH_ACCEPTANCE.md](UI_WORKFLOWS.md#ui-metadata-polish-acceptance) records a pre-change full checkpoint of
6,690/6,690 across eight assemblies, with no failures/skips and unchanged test
DLL hashes. This does not replace the historical failed runs or establish a
full regression of the subsequent UI changes.

The later candidate improves shared secondary-text contrast, tool status insets
and bounded overflow, and adds a request-identity/selected-task guarded stop
action inside the approval footer. The complete Visual assembly passes 300/300.
The final candidate also passes all 275 focused App cases, including approval
queue identity, cancellation, disposal and the new footer's structural guard.
An external production MainWindow driver first reproduces two compact stop-hit
failures in 24 attempts, then passes all 24 after the footer fix. Both themes and
1280x820/526x601 cover actual send, approve/deny/stop, external edit conflict,
read-only/path denial, snapshot review, undo and operable no-replay restoration.
Five successful review/undo audits retain statistics/review calls rather than
suppressing them to match an incorrect helper count.

These are independent Headless/Skia and loopback-runtime results, not native
desktop input, project/provider-editor acceptance, current all-assembly
regression or same-state Codex pixel parity. Current user windows, data and
approval settings are untouched; JIT, JSON and packaging decisions are retained.

#### Project authorization and compact-drawer checkpoint

[PROJECT_AUTHORIZATION_UI_ACCEPTANCE.md](SECURITY.md#project-authorization-ui-acceptance) records real MainWindow-to-SQLite-to-tool
checks using an isolated picker port and loopback model. It reproduces compact
approval obstruction and immediately dismissed project summaries, then adjusts
App-only presentation and shared project-row dismissal. Permission policy, active
task snapshots and persistence remain unchanged. New and removed roots, resumed
tasks, cross-project rejection and no-replay restoration are verified separately
from native desktop/picker and current Codex pixel-equivalence acceptance.

The final ordinary-mode expanded App run passes 1,726/1,726; the 28 focused
cases are a subset, not additional coverage. Complete Visual, Agent and Tools
assemblies pass 300/300, 123/123 and 494/494 respectively, all without skips or
aborts. The solution build has zero warnings/errors; scoped whitespace and
eight-file source audits pass. Production/test App DLL hashes match the focused
capture candidate, and forty independent Headless frames are retained.

Earlier Blame-enabled expanded runs remain recorded as 1,718/1,720 and
1,725/1,726. The old approval structure guard is synchronized; the unchanged
zero-allocation guard passes three isolated six-case runs on each of the old
and current candidates, as well as the final ordinary run. This does not prove
the diagnostic collector caused the earlier allocation failures. These results
are not a complete App/eight-assembly regression or native/current-Codex visual
acceptance. No staging, commit, push, release or packaging changes occur.

#### All-pages UI consistency checkpoint

[ALL_PAGES_UI_POLISH_ACCEPTANCE.md](UI_WORKFLOWS.md#all-pages-ui-polish-acceptance) records the adaptive settings rows, shared
catalog empty states, readable feedback, page headings and compact list corners.
Fourteen page categories, five create dialogs and advanced appearance are reviewed
in light/dark themes at three viewport sizes; browser profile modes are checked
separately. There are 126 explicitly named independent Headless frames, not native
desktop or current-Codex pixel-equivalence evidence.

The solution build has zero warnings/errors; the complete Visual assembly passes
300/300. The initial complete App run is 4,893/4,898 with no skips or aborts.
Three stale structure checks are synchronized with the adaptive design and pass
in the final 44/44 focused run; two unrelated source guards remain recorded.
The focused result is a subset, not a new complete-App pass. App DLL identities,
scoped whitespace, seven-file type/size and relevant markup-size audits agree.
Current user windows, data, permissions and packaging policy are untouched.

#### Restored worktree operation authorization checkpoint

[RESTORED_WORKTREE_AUTHORIZATION_ACCEPTANCE.md](GIT_WORKTREES.md#restored-worktree-authorization-acceptance) records task-bound, read-only Git
validation for resumed tasks, provider preparation, direct branch reads and
standard tool execution before/after approval. Preview/diff/statistics/undo,
terminal/browser actions and child-agent context copies retain the guard.
Explicit cleanup now rejects a different task's valid registered worktree or a
mismatched project instead of removing it and clearing persisted metadata.
Isolated red/green tests retain their original failures. The final related
checkpoint passes 506 Tools, 442 Infrastructure, 140 Agent, 799 App and 8 real
Windows Git process cases (1,895 total), with a zero-warning/error solution
build and a 238-file handwritten C# audit. Frozen code/configuration, binary
and source-ZIP identities agree; reports and limits are in the linked document.
This is snapshot
authorization, not native handle-level race protection, live authorization
revocation, native-window/restart acceptance or whole-product completion.

The restored-authorization candidate's subsequent complete run now has a real
zero exit and eight Completed reports: 7,355/7,355, including 5,137 App cases,
with no failed/skipped tests. All 1,601 frozen binary identities remain unchanged.
The terminal and per-assembly counters are retained in its candidate directory.
This uses pre-staging binaries; source-policy tests can also read live source.
It is not full regression evidence for later Git staging work.

#### Git file-staging checkpoint

[GIT_STAGING_ACCEPTANCE.md](GIT_WORKTREES.md#git-staging-acceptance) records selected-file stage/unstage through the
standard pipeline and production App composition, real index/filter refresh,
unborn/deleted/renamed/literal file handling, copy-source isolation and failed
Git audit. Complete repository/metadata authorization, DOS aliases, junctions
appearing during approval, task guards, bounded output and real cancellation
cleanup have deterministic coverage. Headless keyboard and light/dark narrow/
wide component checks are not native-window acceptance. Commit/push/PR and
group/hunk staging remain missing, and repository hooks are not OS-sandboxed.
The final candidate has seven zero-exit recorded commands and six Completed
reports: 549 Tools, 442 Infrastructure, 140 Agent, 1,124 related App, 300 Visual
and 8 real Windows Git cases, totaling 2,563/2,563 with no failures or skips.
The separate 24-case focused App pass overlaps that total and is not added again.
The solution build is clean, the 261-file C# and changed-markup audits pass, and
all 2,135 source/configuration and 2,439 runtime identities/path sets agree with
the frozen source ZIP and manifests. This is related regression, not complete
solution tests, native acceptance or whole-product completion.

#### Staged-only Git commit checkpoint

[GIT_COMMIT_ACCEPTANCE.md](GIT_WORKTREES.md#git-commit-acceptance) records separate preview/commit tools, state-bound
confirmation, ordinary staged-only commits and the production inspector dialog.
Real temporary Git/SQLite composition verifies new HEAD, exact staged bytes,
unselected-file preservation and unchanged conversation input. Approval-time
index/HEAD/branch changes, conflicts, denial, guard revocation, hook/identity
failure and cancellation/output/audit paths have focused coverage. Both dedicated
Git runners strip `GIT_` child variables to prevent inherited index/config/object/
trace redirection; ordinary Git configuration/hooks remain active, not sandboxed.
Shared dialog focus restoration now handles a trigger reenabled during closing
without stealing newer focus. Focused terminal reports pass 35 Tools, 87 App and
11 Windows environment checks, and the 293-file C# audit passes. Expanded frozen
candidate regression has terminated with seven zero-exit commands and six
Completed reports: 565 Tools, 442 Infrastructure, 140 Agent, 1,927 related App,
300 Visual and 146 Windows checks, totaling 3,520/3,520 without failures/skips.
Source/runtime hashes, path sets and the source ZIP agree at terminal (2,158
source/configuration and 2,439 runtime files). Overlapping focused passes are not
added to that total. A separate completion-state/focus candidate follows this
snapshot; the earlier result is not full regression of later code. Push/PR,
group/hunk staging, native/restart and
interactive signing/hook acceptance remain incomplete. No development-worktree
commit, push or release occurs.

The separate `git-commit-completion-20261018` candidate now closes successful
commit dialogs only after releasing busy/operation ownership in the same UI
update. Four immediate/delayed commit/refresh state tests first fail; the real
production focus assertion passes even in that red run, so native focus loss is
not claimed as reproduced. After the fix, focused 91 and related 373 cases pass,
with no failures/skips in the final related report and a clean solution build.
The 91 cases overlap the 373 total. All 2,158 source and 2,439 runtime identities,
path sets and frozen ZIP bytes agree; only the view model and its two test files
differ from the earlier 3,520-case snapshot. Current full-solution and native
acceptance remain separate; earlier broad passes are not relabeled as new runs.

#### Confirmed single-branch Git push checkpoint

[GIT_PUSH_ACCEPTANCE.md](GIT_WORKTREES.md#git-push-acceptance) records local preview plus approved state-bound push,
credential-free endpoint labels, authorized local bare/non-bare destinations and
the actual production inspector confirmation. Real temporary Git/SQLite tests
verify exact captured commit/target, preserved dirty/staged content and composer
draft, real status refresh and keyboard focus. Scoped process settings override
mirror/tag/submodule/push-option defaults without changing upstream configuration;
ordinary hooks and credentials are not bypassed. Explicit unsupported cases and
remaining native/TOCTOU/hook sandbox limits are retained.

The frozen `git-push-20261018` candidate has seven zero-exit commands and six
Completed reports: 613 Tools, 442 Infrastructure, 140 Agent, 1,966 related App,
300 Visual and 146 Windows cases, totaling 3,607/3,607 without failures/skips.
Complete solution build has zero warnings/errors. Terminal manifests verify all
2,187 source/configuration and 2,439 runtime hashes/path sets and source ZIP bytes;
the source snapshot SHA-256 is
`407C0C9EFEC56E52BC5F3DD3BCCAB260332BD0534A8F8E03F83A4986173E6B9B`.
The 320-file C# and 16-file markup audits pass. Light/dark shared-dialog renders
at 280/640 by 560/700 DIP cover ready/busy/interrupted/failed states; Headless
geometry and production composition are not native-window/live-account proof.
Earlier focused passes overlap this total. PR, remote/credential configuration UI,
group/hunk staging, native/restart and complete product acceptance remain open.
No development-worktree commit, push or release occurs. PR requirements and
source-only security design are in [PULL_REQUEST_WORKFLOW_PLAN.md](GIT_WORKTREES.md#pull-request-workflow-plan), not implemented
functionality or proof that this overall goal is complete.

#### Task tool-scope foundation checkpoint

[TOOL_SCOPE_ACCEPTANCE.md](TERMINAL_TOOLS.md#tool-scope-acceptance) records an immutable allowed-name scope enforced by
the shared executor before authorization/approval side effects. Agent filters
per-turn descriptors/delegation and child gateways intersect parent/child scopes;
an unsolicited model call is still denied through the real execution pipeline.
This does not enable tools for plain projectless chat or implement source-account
authorization, PR HTTP, account connection or a working PR page.

The separate `pr-review-scope-20261018` candidate has three zero-exit Completed
reports: 624 Tools, 145 Agent and 334 related App cases, totaling 1,103/1,103 with
no failures/skips. Its complete solution build has zero warnings/errors, and the
326-file C# type/size/syntax audit passes. Earlier focused 18 and 21 checks overlap
the total. These are current related checks, not a repeated 3,607-case Git push
run, whole-solution regression, native-window QA or real-source-account proof.
The complete PR chain in [PULL_REQUEST_WORKFLOW_PLAN.md](GIT_WORKTREES.md#pull-request-workflow-plan) remains to implement.

#### GitHub token connection checkpoint

[SOURCE_ACCOUNT_CONNECTION_ACCEPTANCE.md](GIT_WORKTREES.md#source-account-connection-acceptance) records independent named source
accounts, schema-18 persistence, proxy selection, shared settings dialogs and
actual identity reauthentication. List rows never retain token fields. Bounded
GitHub requests pin the configured HTTPS authority/API prefix, disable redirects
and cookies, fail closed on unavailable proxies and suppress stale identities
after account changes. Production-composed shared controls, real SQLite and
controlled loopback HTTP verify keyboard save/refresh/edit and two separate
identity requests in both themes; this grants no roots, tools or repository
permissions and is not a PR inbox or OAuth connection.

The first `source-accounts-20261018` candidate terminates with five Completed
reports: 490 Infrastructure, 624 Tools, 145 Agent, 99 related App and 300 Visual
cases, totaling 1,658/1,658 without failures/skips. Its full solution build has
zero warnings/errors; 2,220 source/configuration and 2,259 runtime file hashes and
path sets agree before/after testing. The 360-file C# audit passes. Twelve
Headless renders cover compact/ordinary layouts, both themes and 1/1.25 scaling;
selected renders were inspected. This is related regression, not full-solution
tests, native-window/live-account proof or a source/runtime archive.

A separate `source-account-disable-20261018` candidate fixes a subsequently
reproduced revocation problem: an unavailable proxy must not prevent disabling
its account. Both SQLite and UI tests first fail, then pass. Disabling preserves
the proxy reference; missing proxies and reenabling with a disabled proxy are
still rejected. Account disable/delete during an identity request invalidates
the old result. Its current complete solution build has zero warnings/errors;
493 Infrastructure and 100 related App checks pass, totaling 593/593 without
failures/skips. All 2,221 selected source/configuration and 2,259 runtime hashes/
path sets agree before/after testing; the 361-file changed/new C# audit and diff
whitespace check pass. Earlier 1,658-case results and overlapping focused runs
are not relabeled or added as regression of these later changes.
PR repository grants, inbox/details/review/writes, OAuth, GitLab, cloud reviews,
plugin compatibility and native/restart acceptance remain incomplete. No commit,
push, release, real credentials or paid provider request occurs.
<!-- /source: PRODUCT_ACCEPTANCE_MATRIX.md -->


---

<a id="product-interaction-audit"></a>

## PRODUCT_INTERACTION_AUDIT.md

<!-- source: PRODUCT_INTERACTION_AUDIT.md -->
### Product interaction audit and complete regression checkpoint

#### Scope (2026-10-16)

This follow-up validates the existing uncommitted UI revision, including shared
tool-output keyboard focus and subagent manual-approval coverage. It does not
introduce another production change or replace native/Codex visual acceptance.

The UI/UX skill's cross-platform focus-state guidance informs the review: keep
visible keyboard feedback, usable escape routes and native disabled semantics.
Web-only WCAG examples and mobile target sizes are not asserted as desktop
implementation requirements or compliance results.

#### Complete regression results

All eight existing test assemblies finish normally with every result Passed:

| Assembly | Passed / total |
| --- | --- |
| App | 4,726 / 4,726 |
| App.Visual | 188 / 188 |
| Agent | 123 / 123 |
| Infrastructure | 204 / 204 |
| Providers | 542 / 542 |
| Tools | 494 / 494 |
| Browser | 76 / 76 |
| Platform.Windows | 142 / 142 |
| Total | 6,495 / 6,495 |

- Each TRX reports zero failures, skipped/not-executed or aborted cases; its
  individual result count matches its total. Related subset runs are not added.
- The complete App run includes all eight manual-approval flow cases, eight
  approval boundary cases and four tool-output keyboard cases. Complete pixels
  include all eight new tool-output focus-rendering cases.
- All test processes finish before the final solution build. The build succeeds
  with zero warnings/errors using `dotnet build MossAgent.slnx --no-restore
  -v:minimal -m:1 /nodeReuse:false`.
- SHA-256 identities of all eight tested assemblies stay unchanged across the
  tests and build. Production, App.Tests, App.Visual.Tests and the external
  driver's four App DLL copies match and remain unchanged after the build.
- The earlier 6,467-case complete checkpoint remains historical evidence. Its
  earlier allocation/cleanup failures do not recur here; this is not proof of
  their original causes or a new causal fix.

#### Static and rendered entry review

The external driver uses the production MainWindow and composition through an
existing test fixture, isolated synthetic storage and a local model server. It
references existing output DLLs rather than rebuilding the repository. No user
database, credentials, existing window or desktop capture is accessed.

- A markup inventory finds 258 selected button declarations: 233 command, three
  Click event, 21 flyout and one toggle entry. This is syntax, not behavior proof.
- The final Headless review records 4,038 wrapper-button occurrences across 160
  scanned page/state combinations, including 40 new-editor combinations. These
  are repeated appearances, not 4,038 distinct controls or xUnit tests.
- Combinations cover project/plain-chat scope, requested Light/Dark themes and
  526x601/1280x820 geometry. Pages include conversation, six inspector states,
  automations, seven settings pages and five kinds of new-item editor.
- No enabled button is unresolved by the driver's wiring checks. Those checks
  recognize commands, flyouts, toggles and known event-backed controls; they do
  not invoke most commands. Command-disabled controls are not visually enabled.
  There are 376 disabled occurrences, not 376 distinct unavailable features.
- Twenty-eight search paths exercise Space opening and routed Escape dismissal
  from the search input. All forty new-editor paths use Space to open and Escape
  to dismiss provider, proxy, MCP, subagent or custom-agent dialogs.
- Sixteen native numeric increment/decrement paths use actual pointer hit-tests
  and down/up input, verify the value change, then restore the draft value.
- Navigation retains the original composer draft and project selection. The
  fixture's model server records zero requests throughout these UI-only paths.

#### Evidence and interpretation

Evidence is outside the repository at
`%TEMP%/MossAgent-product-audit-20261016`. The directory label is not an execution
timestamp; the checks span the local 2026-10-15/16 midnight boundary.

- `app.trx` and the other seven assembly-named TRX files are complete-run results;
  `complete-suite-summary.json` records their independently parsed counters.
- `markup-actions.json` is the static declaration inventory.
- `rendered-actions-editors-verified.json` is the final driver result;
  `interaction-summary.json` records its occurrence/path counts.
- `driver/` holds the external audit source and output, not repository code.
- `hashes-before.csv`, `hashes-after-tests.csv`, `hashes-after-build.csv` and
  `app-copy-hashes-after-*.csv` record tested/build identity checks;
  `solution-build.log` records the final build.
- Earlier `rendered-actions*.json` files retain audit-development observations.
  SearchTrigger opens through a component Click handler after taking ownership
  of its flyout; native numeric spinner buttons use event wiring, not commands.
  These initial wiring warnings are not confirmed product defects. Snapshotting
  the visual collection fixes the driver's enumeration failure, not production UI.

#### Remaining boundaries

- Most occurrences receive binding/enabled checks only. Visibility flags do not
  establish unobscured pixels or actual pointer hit areas; only the explicit
  numeric paths above receive pointer hit-test verification in this audit.
- The audit does not execute save/delete, model discovery, tools, terminal or
  browser launch, every menu, arbitrary user data, or all loading/error states.
- Requested theme settings do not prove the complete preference/theme workflow
  or pixel contrast. These are Headless checks, not native-window screenshots.
- Native screenshots and existing-window operations remain postponed by the
  user. Same-state Codex comparison, native DPI/resize diagnosis and whole-product
  interactive/visual sign-off remain open; no goal completion is claimed.

No production/test source, user settings, JIT/JSON behavior or Windows x64
single-file packaging is changed by this audit. Existing uncommitted work is
preserved; nothing is staged, committed, pushed or released.
<!-- /source: PRODUCT_INTERACTION_AUDIT.md -->
