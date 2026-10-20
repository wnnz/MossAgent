# MossAgent 阶段交付清单与后续实施方案

日期：2026-10-20。分支：`feature/initial-mossagent`。

本次收尾保存此前尚未提交的产品代码、测试和文档，不推送、不发布。下文描述的是
当前仓库的累计能力，并非每项都在本次新增。提交前基线为 `991b796`；提交身份以
Git 历史为准。原始测试、诊断、数据库副本和冻结候选保留在被忽略的 `artifacts/`
或各专项文档注明的临时目录，不加入源码提交。

**这是阶段交付，整个产品目标尚未完成。命令能够访问任务授权根目录之外的文件，
是已确认且尚未修复的安全阻断。** 工作目录校验、审批和 Job Object 不能证明文件系统隔离。
另有功能缺失和当前版本原生实机验收缺口，不能用测试数量表示产品完成度。

## 状态和证据如何阅读

- 已实现：指定范围有生产入口、实际运行链路及对应证据，不表示整类功能全部完成。
- 部分实现：基本链路存在，明确列出的子能力仍缺失。
- 未实现：当前盘点未发现完整的生产运行链路；研究文档、同名按钮或配置存储不算实现。
- 未验证：已有实现或设计，但对应场景缺少当前版本的可靠证据，不能直接推断成功或失败。
- 自动化测试、真实本地 Git/Chrome/进程、模拟 HTTP、Avalonia Headless、原生 EXE 交互和
  真实外部账号分别标注。历史原生证据只证明其记录的版本和场景。
- 功能总表见 [FUNCTIONAL_PARITY_MATRIX.md](FUNCTIONAL_PARITY_MATRIX.md)，模块边界见
  [ARCHITECTURE.md](ARCHITECTURE.md)。两者和专项验收共同使用；旧记录中的“正在运行”
  或“尚未实现”描述仅代表其检查点，不覆盖本文件明确列出的后续结果。

## 一、已经做出的可运行能力

| 范围 | 实际入口、链路及已实现行为 | 证据及适用边界 |
| --- | --- | --- |
| 项目和无项目对话 | 工作台新对话 → `WorkspaceTaskFactory` → 任务执行器 → Agent → 所选提供商。支持无项目文本/明确选定附件；普通无项目任务不获得任意目录、shell 或其他工具权限 | [PROJECTLESS_CHAT.md](PROJECTLESS_CHAT.md)、[PROJECTLESS_ATTACHMENTS_ACCEPTANCE.md](PROJECTLESS_ATTACHMENTS_ACCEPTANCE.md)；附件选择不等于目录授权 |
| 流式、多轮和历史 | 提供商流 → Agent 事件 → 时间线与 SQLite；历史参与下一轮上下文，任务切换/重新打开恢复消息和状态 | [HISTORY_CONTEXT_ACCEPTANCE.md](HISTORY_CONTEXT_ACCEPTANCE.md)、[PROVIDER_STREAM_COMPLETION_ACCEPTANCE.md](PROVIDER_STREAM_COMPLETION_ACCEPTANCE.md)；Responses/Anthropic 显式失败、不完整终态及缺失终态 EOF 已有拒绝处理，其他变体仍见 R06 |
| 并发、取消和中断 | 每任务运行控制器持有取消、事件、进程及浏览器资源；发送准备也可取消，提交后取消有可查看终态；启动将未完成任务标记中断，不自动重放 | [SEND_PREPARATION_CANCELLATION_ACCEPTANCE.md](SEND_PREPARATION_CANCELLATION_ACCEPTANCE.md)、[SHUTDOWN_RECOVERY_ACCEPTANCE.md](SHUTDOWN_RECOVERY_ACCEPTANCE.md)；不是所有副作用时序的崩溃验收 |
| 独立草稿与原子发送 | 任务/项目新会话/无项目草稿各自保存；版本冲突保护，后台保存串行；消息提交与草稿消费使用同一 SQLite 事务，保留发送期间的新输入 | [CONVERSATION_DRAFT_ACCEPTANCE.md](CONVERSATION_DRAFT_ACCEPTANCE.md)、[DRAFT_BACKGROUND_PERSISTENCE_ACCEPTANCE.md](DRAFT_BACKGROUND_PERSISTENCE_ACCEPTANCE.md)、[CONVERSATION_SUBMISSION_ACCEPTANCE.md](CONVERSATION_SUBMISSION_ACCEPTANCE.md) |
| Queue 续发 | 活动对话 Enter 入队；可编辑、排序、移除、手动继续。保存模型/强度/代理提示词/授权和附件快照；准备校验后事务复核并分派至原任务。仅成功的活动链自动续发，取消、失败或重启后需手动继续 | schema 23；[CONVERSATION_FOLLOWUP_RUNTIME_ACCEPTANCE.md](CONVERSATION_FOLLOWUP_RUNTIME_ACCEPTANCE.md)、[CONVERSATION_FOLLOWUP_DISPATCH_ACCEPTANCE.md](CONVERSATION_FOLLOWUP_DISPATCH_ACCEPTANCE.md)、[CONVERSATION_FOLLOWUP_PROCESS_RECOVERY.md](CONVERSATION_FOLLOWUP_PROCESS_RECOVERY.md) |
| 文本 Steer | 活动 Ctrl+Enter/“引导当前运行” → `ConversationSteeringSession` → 草稿/receipt 原子接收 → 当前工具批次结束后的模型边界注入。保持原模型、提示词、工具根和审批策略；有界 mailbox、幂等 ID、可查历史及中断状态 | schema 25；[CONVERSATION_STEERING_ACCEPTANCE.md](CONVERSATION_STEERING_ACCEPTANCE.md)。接收和应用均复核授权；仅空边界兼容追加目录且不扩大原工具根。附件和用户默认行为配置尚缺 |
| 项目与授权目录 | 项目设置保存 → 仓库端口 → 任务授权快照 → 工具路径守卫；多授权根、路径穿越、链接/重解析点及恢复任务归属检查已有实现 | [PROJECT_AUTHORIZATION_UI_ACCEPTANCE.md](PROJECT_AUTHORIZATION_UI_ACCEPTANCE.md)、[RESTORED_WORKTREE_AUTHORIZATION_ACCEPTANCE.md](RESTORED_WORKTREE_AUTHORIZATION_ACCEPTANCE.md)；句柄级竞态及即时撤权仍见 R02 |
| 文件编码工具 | 文件搜索/查找、读取、写入、编辑、复制、移动、删除 → 统一工具管线 → 授权路径验证与有界结果；写入使用原子替换和先前读取版本冲突检查 | [FILE_EDITING_ACCEPTANCE.md](FILE_EDITING_ACCEPTANCE.md)、[FILE_SNAPSHOT_SHARING_ACCEPTANCE.md](FILE_SNAPSHOT_SHARING_ACCEPTANCE.md)；已修复内部读句柄阻断原子编辑的问题 |
| 工具过程、审批及审计 | `ToolExecutor` 统一处理查找、schema、能力/路径、风险、审批、取消/超时、输出/产物和审计；共享界面显示等待/执行/结果；主/子代理均受工具 allowlist 限制 | [TOOL_SCHEMA_ACCEPTANCE.md](TOOL_SCHEMA_ACCEPTANCE.md)、[TOOL_SCOPE_ACCEPTANCE.md](TOOL_SCOPE_ACCEPTANCE.md)、[GLOBAL_APPROVAL_PRESENTATION_ACCEPTANCE.md](GLOBAL_APPROVAL_PRESENTATION_ACCEPTANCE.md) |
| 中断工具结果 | 启动恢复在事务中结束遗留进行中审计，共享审计行显示结果未知和手动核实要求；不把未知副作用显示为成功，也不自动重做 | schema 24；[AUDIT_INTERRUPTION_RECOVERY_ACCEPTANCE.md](AUDIT_INTERRUPTION_RECOVERY_ACCEPTANCE.md)，含旧 schema 数据库副本迁移 |
| 命令及交互终端基础 | PowerShell 命令、ConPTY 输入/输出、VT、剪贴板/输入法、任务所属进程树和停止链路已实现；工具仍受工作目录、审批、时限、输出限制约束 | [COMMAND_EXECUTION_ACCEPTANCE.md](COMMAND_EXECUTION_ACCEPTANCE.md)、[TERMINAL_INTERACTION_ACCEPTANCE.md](TERMINAL_INTERACTION_ACCEPTANCE.md)；**当前不具备严格的命令文件系统隔离** |
| 变更审阅与撤销 | 侧栏真实 Git 状态、已暂存/未暂存差异、任务文件快照/统计/补丁预览及确认撤销，经过工具而非静态数据 | [DIFF_REVIEW_ACCEPTANCE.md](DIFF_REVIEW_ACCEPTANCE.md)、[TASK_SNAPSHOT_REVIEW_ACCEPTANCE.md](TASK_SNAPSHOT_REVIEW_ACCEPTANCE.md)、[FILE_CHANGE_UNDO_ACCEPTANCE.md](FILE_CHANGE_UNDO_ACCEPTANCE.md) |
| Git 暂存、提交、推送 | 所选文件暂存/取消暂存；`git.commit` 对已暂存内容预览确认；`git.push` 对捕获的 commit 向单个 heads ref 预览确认。审批后复核仓库/HEAD/分支/索引或远端状态；不自动 stage-all、强推、amend 或重试 | [GIT_STAGING_ACCEPTANCE.md](GIT_STAGING_ACCEPTANCE.md)、[GIT_COMMIT_ACCEPTANCE.md](GIT_COMMIT_ACCEPTANCE.md)、[GIT_PUSH_ACCEPTANCE.md](GIT_PUSH_ACCEPTANCE.md)；真实临时 Git/bare 仓库和生产 Headless 入口，非真实远端认证验收。配置 hooks/helpers 仍有 OS 隔离缺口 |
| 工作树 | 项目新对话显式选择独立工作树和手动分支/标签/提交，解析为固定 commit；创建、连续对话、重开、逐操作守卫及确认清理贯通。校验 GUID 目录、链接祖先、仓库与外部 Git 元数据归属 | [WORKTREE_BASE_REFERENCE_ACCEPTANCE.md](WORKTREE_BASE_REFERENCE_ACCEPTANCE.md)、[WORKTREE_PATH_SAFETY_ACCEPTANCE.md](WORKTREE_PATH_SAFETY_ACCEPTANCE.md)；已有真实 Git/并发任务/跨工作树拒绝，剩余见 R04 |
| 提供商与代理配置 | 多提供商、多 HTTP/SOCKS5 代理及默认代理；可明确直连，失效代理不静默替换。共享分组/展开/逐项编辑、发现/手工模型录入、默认与启用状态已有持久化和真实请求链路 | [PROVIDER_UI_WORKFLOW_ACCEPTANCE.md](PROVIDER_UI_WORKFLOW_ACCEPTANCE.md)、[PROVIDER_PROXY_ACCEPTANCE.md](PROVIDER_PROXY_ACCEPTANCE.md)、[AUTHENTICATED_PROVIDER_PROXY_ACCEPTANCE.md](AUTHENTICATED_PROVIDER_PROXY_ACCEPTANCE.md) |
| 协议及模型参数 | OpenAI Responses、兼容 Chat、Gemini GenerateContent、Anthropic Messages；流式与多轮工具调用。模型上下文/输出上限、可选或自定义思考强度、Temperature、Top-P、能力覆盖及高级 JSON 参与请求或拒绝校验 | [MODEL_REASONING_CAPABILITY_ACCEPTANCE.md](MODEL_REASONING_CAPABILITY_ACCEPTANCE.md)、[GEMINI_CONTINUATION_ACCEPTANCE.md](GEMINI_CONTINUATION_ACCEPTANCE.md)、[MODEL_CONFIGURATION_NATIVE_ACCEPTANCE.md](MODEL_CONFIGURATION_NATIVE_ACCEPTANCE.md)；远端支持不能仅从本地配置推断 |
| 文本/图片附件 | 明确选择 UTF-8 和静态 PNG/JPEG/GIF/WebP → 真实解码和限额 → 文件快照 + SQLite 元数据 → 草稿/历史 → 对应原生请求块。图像单独成轮可用；模型能力不符保留草稿并拒绝发送；缩略图后台生成，JPEG EXIF 八方向处理 | [IMAGE_ATTACHMENT_WORKFLOW_ACCEPTANCE.md](IMAGE_ATTACHMENT_WORKFLOW_ACCEPTANCE.md)、[PROJECTLESS_ATTACHMENTS_ACCEPTANCE.md](PROJECTLESS_ATTACHMENTS_ACCEPTANCE.md)、[IMAGE_ORIENTATION_ACCEPTANCE.md](IMAGE_ORIENTATION_ACCEPTANCE.md)。源文件删除不影响已保存快照，缺失/篡改快照拒绝请求 |
| 浏览器基础及取消修复 | 内嵌 WebView2、受控外部 Chrome/Edge、显式 CDP、任务标签页、DOM 操作、截图/下载和接管栏；取消/超时使旧会话失效，等待实际 SDK 操作和所属资源清理，不允许超时后旧点击继续 | [BROWSER_OPERATION_CANCELLATION_ACCEPTANCE.md](BROWSER_OPERATION_CANCELLATION_ACCEPTANCE.md)、[QUEUE_BROWSER_RUNTIME_ACCEPTANCE.md](QUEUE_BROWSER_RUNTIME_ACCEPTANCE.md)、[BROWSER_COMMITTED_EFFECT_RECOVERY_ACCEPTANCE.md](BROWSER_COMMITTED_EFFECT_RECOVERY_ACCEPTANCE.md)；借用 context 的 cookie/storage 共享，不能称配置完全隔离 |
| MCP | stdio/Streamable HTTP 配置、headers/代理、发现和动态工具注册；MCP 工具走同一管线。错误反馈、显式重连、重新加载配置/代理目录及关闭期间迟到结果抑制已有链路 | [MCP_EXECUTION_ACCEPTANCE.md](MCP_EXECUTION_ACCEPTANCE.md)、[MCP_RECONNECT_RECOVERY_ACCEPTANCE.md](MCP_RECONNECT_RECOVERY_ACCEPTANCE.md)、[MCP_UI_RECOVERY_ACCEPTANCE.md](MCP_UI_RECOVERY_ACCEPTANCE.md)；显式重连不等于自动续传 |
| 自定义代理/子代理 | 名称、触发条件、系统提示词、模型、强度和上下文共享保存 → 父模型发布/触发委托 → 选定子模型 → 子工具 → 父对话过程和结果。子工具继承并收窄主任务权限、重新进入审批管线，不允许递归委托 | [SUBAGENT_SETTINGS_TO_RUNTIME_ACCEPTANCE.md](SUBAGENT_SETTINGS_TO_RUNTIME_ACCEPTANCE.md)、[SUBAGENT_LIFECYCLE_NATIVE_ACCEPTANCE.md](SUBAGENT_LIFECYCLE_NATIVE_ACCEPTANCE.md)；历史独立原生实例 + 模拟模型已验证一组设置到运行及重开链路，不能扩大为当前全部分支原生通过 |
| 定时任务 | 定时任务页创建/编辑、保存模型/提示词/授权、启停、立即运行、停止、历史及打开结果。独立后台任务不消费前台草稿；启动跳过过期触发并标记中断，不补跑未完成副作用 | [AUTOMATION_UI_WORKFLOW_ACCEPTANCE.md](AUTOMATION_UI_WORKFLOW_ACCEPTANCE.md)、[AUTOMATION_MUTATION_ACCEPTANCE.md](AUTOMATION_MUTATION_ACCEPTANCE.md)；Headless 已验证保存参数进入真实适配器请求，关闭/恢复组合见 R10 |
| GitHub PR 只读链路 | 独立来源账号/token/代理、显式仓库只读授权 → 开放 PR/URL → 描述/分页补丁/讨论/行内评论/checks。评论按当前 commit、路径和有效差异行定位；有固定快照审阅对话及独立只读按需查询对话 | [SOURCE_ACCOUNT_CONNECTION_ACCEPTANCE.md](SOURCE_ACCOUNT_CONNECTION_ACCEPTANCE.md)、[SOURCE_PR_INLINE_COMMENTS_ACCEPTANCE.md](SOURCE_PR_INLINE_COMMENTS_ACCEPTANCE.md)、[SOURCE_PR_ON_DEMAND_READING_ACCEPTANCE.md](SOURCE_PR_ON_DEMAND_READING_ACCEPTANCE.md)、[SOURCE_PR_URL_ACCEPTANCE.md](SOURCE_PR_URL_ACCEPTANCE.md)；撤权/版本变化拒绝后续查询，不隐式启用本地工具 |
| UI 与工程基础 | Avalonia/.NET 10，共享包装控件、集中主题/样式、本地化资源、编译期组件约束、模块依赖检查、SQLite 向前迁移；普通构建 JIT，Windows x64 自包含单文件方案保持，无 AOT/裁剪 | [UI_COMPONENTS.md](UI_COMPONENTS.md)、[ARCHITECTURE.md](ARCHITECTURE.md)、[../scripts/publish.ps1](../scripts/publish.ps1)；界面整体可用性和当前单文件发布验收仍见 R23/R24 |

## 二、当前版本的验证结果

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
[CONVERSATION_STEERING_ACCEPTANCE.md](CONVERSATION_STEERING_ACCEPTANCE.md) 入库。
最初候选的 18 项失败及后续六组主动中止回归均保留，不改写为成功；旧超时和一次
Queue 选择/草稿断言失败的精确原因未证明，不能归因于负载，也不能宣称已修复串扰。

历史原生证据包括部分提供商/模型、子代理及综合编码链路，详见各专项文档；
[NATIVE_SUBMISSION_ACCEPTANCE.md](NATIVE_SUBMISSION_ACCEPTANCE.md) 另记录一次无项目
实际输入/发送/流式观察/强制终止重启。其截图黑帧、激活失败和 `GetCursorPos` 拒绝
仍是自动化限制，既不是通过的视觉验收，也不是已证明的 MossAgent 输入缺陷。
当前 Steer、全部关键工作流原生复验及真实外部提供商/PR 账号验收仍未完成。

## 三、未完成范围及优先顺序

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

## 四、逐项实施方案

### 所有方案共同遵守的工程与验收要求

行为放在下列指定模块，复用 Application/Tools.Abstractions 端口；App 仅协调和展示。
每个工具独立实现 `IAgentTool`，按查找 → schema → 能力/路径 → 风险 → 审批 →
取消/超时 → 执行 → 输出限制/产物 → 审计的顺序运行，不能直接互调绕过管线。
默认未知外部工具有副作用，不因名称或不可信元数据自动判为只读。

新增状态通过当前 schema 25 之后的向前迁移保存；消息/配置/产物归属和版本关系明确，
不删除或重解释旧数据。每任务独立取消、事件、进程树和浏览器会话；取消后等待实际
资源结束，结果未知时保留状态，恢复不重放。文件型大输出只存元数据与有界预览到 SQLite。
诊断不保存密钥、授权头、代理凭据或 Cookie。拆分类型和方法，继续执行单文件单类型、
500 行文件/80 行方法硬限制及共享组件规则。

每项交付至少覆盖成功、拒绝/权限撤销、失败、取消/超时、两任务并发和持久化恢复；
涉及外部副作用时增加“已生效但未拿到结果”的断点。先确定性测试和真实本地资源，
再用独立原生实例验关键入口；需要真实账号时另做明确授权的验收。窄测试和完整构建
通过后做阶段全量，最终同时完成 Debug/Release 和正式包验证，不重复累加覆盖数。

### R01：Windows 命令文件系统隔离（最高优先级）

- **缺口与已否决路线：** 生产普通用户令牌可读根外标记；AppContainer/LPAC 的包组暴露不满足严格 allowlist。KnownDlls 的 44 个对象在真实管理员预检仍全部拒绝 WRITE_DAC，已确认 WinTcb-Light；临时改系统 ACL 路线否决。此前两次单次管理员授权均已消费，不能复用。
- **模块与契约：** Tools.Abstractions 增加不可变任务执行范围：task/run、规范化授权根、明确只读运行时依赖、任务私有 temp/cache、网络策略及后端身份。Platform.Windows 实现隔离适配器；复用现有进程和终端端口、Job/管道/审计。盘点 Git hooks、凭据 helpers、stdio MCP 等代码执行入口，避免只修 shell。
- **后端方案：** 优先在真正支持原生 MXC ProcessContainer 的独立 Windows 环境验证 SDK；必须按每次请求的实际 tier 强制隔离，拒绝 AppContainerDacl 等弱回退，不仅检查 IsSupported。现有 23H2 只检测到 DACL fallback；若保留该系统完整命令能力，需要独立 Windows Hyper-V 容器或 VM 后端，明确镜像/运行时/工具链、逐任务实例、授权目录映射、宿主与来宾路径、网络和凭据供给。现有 Linux Docker、单实例 Windows Sandbox 不能算替代完成。
- **实施步骤：** 先做 pwsh7/.NET SDK/Git/ConPTY 的正常命令兼容探针；再落实 launch 与 capability 变化间的竞态、真实存活句柄/晚启动回收、取消后 drain。SDK 的停止等待不能等同终止进程；PTY 若落到要求无限网络的隔离层必须拒绝并另实现合格桥接。所需系统预置拆成可审阅、可清理的一次安装步骤，失败显式报告，不改全盘 ACL、不自动切换后端。
- **验收：** 同一生产管线复现必须变为根内读写成功、绝对/相对路径/链接/子进程/继承句柄访问根外失败，外部标记不变；多根、多任务互相拒绝；测试启动失败、后端消失、超时/取消、崩溃恢复、正常 Git/SDK 和原生终端。决策依赖是可用的受支持 MXC 验收机，以及是否为 23H2 预置 Windows 容器/VM；未决不代表放弃 23H2。

详见 [COMMAND_FILESYSTEM_ISOLATION_PLAN.md](COMMAND_FILESYSTEM_ISOLATION_PLAN.md)、
[WINDOWS_ISOLATION_BACKEND_DECISION.md](WINDOWS_ISOLATION_BACKEND_DECISION.md)。

### R02：授权撤销、路径竞态与统一执行边界

- Infrastructure/Platform.Windows 将字符串检查延伸到打开后的句柄、最终路径、文件/目录身份及 Git 元数据；原子替换前再验证目标和父目录，复用当前授权/工作树端口，不在 UI 增加 I/O。
- Application 给授权快照增加可比版本/代际，撤销时让匹配任务停止接受新工具并取消所属操作；审批后、执行前及长 I/O 返回后复核。已发生的外部副作用显示未知/需检查，不能声称撤销等于回滚。
- 审计所有内置、浏览器、MCP、子代理和辅助统计入口的 schema/范围/风险/审批顺序，防止后台 UI 查询或子工具走旁路；评估可撤销资源是否需要更细的能力租约。
- 用确定性 gate 在检查/打开/替换/审批间替换 junction、父目录和元数据，验证根外不读不写、保留并发外部修改；增加撤销与工具执行、终端及另一任务并发负例，再做原生恢复。

### R03：Queue/Steer 的剩余能力和竞争恢复

- Application 复用草稿/附件/Queue/Steer 端口，将附件 Steer 表达为不可变的任务所属快照引用，验证模型能力和完整上下文预算后原子接收；附件不足或拒绝不消费草稿，不扩大原运行权限。
- App 增加本地化的默认/替代发送方式设置和明确的 Queue/引导提示，保存选择且约束只在支持的状态生效；快捷键与鼠标共用命令。多行输入、输入法组合、长草稿/队列/历史焦点保持单独验收。
- 用运行身份、队列/草稿 revision 和 receipt ID 控制 enqueue/编辑/排序/dispatch/complete/close 竞争；覆盖授权/模型变化、提交后取消、最后一轮终态与新引导竞争。新增数据库断点与事务前后真实宿主终止，不把任意 sleep 当事务中断证据。
- 对工具已生效但 receipt 尚未应用、receipt 已应用但 UI 未刷新分别终止/恢复，证明不重放、不重收草稿，保留明确状态；项目/无项目、并发任务、原生输入及真实提供商各自记录证据。延续现有文本行为，不改写旧失败结论。

### R04：工作树完整生命周期

- Infrastructure 通过现有受管 Git runner 做有界本地分支/标签查询和分页搜索，App 显示实际候选、解析 commit 及错误，不用自由输入框冒充发现。远端 ref 获取必须是另一个显式动作。
- 在创建前落库 intent/task/仓库/基准 commit/目标目录身份，成功后原子认领；启动只核对未完成 intent 并展示人工恢复/清理，不自动重建或删除归属未知目录。迁移不修改既有工作树。
- 复用 R02 处理句柄竞态和即时撤权；在创建和清理前后校验 `.git` 指针、外部元数据、双向归属及链接祖先。两任务同时创建时各自取消和清理，保留另一任务成功结果。
- 增加实际 Git 创建中各断点崩溃、并行取消/部分失败、分支不存在/移动、磁盘占用、清理失败及原生 EXE 重启；验收草稿/消息/授权保留和根外不变。已有同步并行成功不能覆盖这些组合。

### R05：PDF、其他附件、全图与安全回收

- Domain/Application 定义格式、来源、MIME/内容校验、大小/页数/像素/展开量限额和模型能力矩阵；Infrastructure 负责快照/解析，Providers 负责原生文件块或明确选择的有界文本提取。两种语义必须在 UI 说明，不能把抽取文字说成完整 PDF 理解。
- App 用共享预览容器增加全图缩放/适应窗口和 PDF 分页；后台解码、取消、内存缓存限额，不从历史卡片重新读取原文件，不自动执行附件脚本或加载远端资源。
- 子代理图像共享复用有归属的只读快照并显式纳入共享开关、能力和预算；不共享时请求中不能出现图像。回收采用数据库引用集合 + 租约/宽限期 + 删除前复核，覆盖草稿、历史、Queue、Steer、子代理和执行中引用，不按目录年龄直接删除。
- 验收损坏/伪装/超大/压缩炸弹、缺失/篡改、无项目明确选择、外部文件变化、共享关闭、并发回收和恢复；另做原生选择器/全图/EXE 重启及真实模型图片/PDF理解，不能用 loopback 请求形状代替理解。

### R06：提供商、模型、代理的完整兼容与恢复

- 保持现有四协议和清晰的提供商/模型分组，Providers 按请求构建、解析、事件与错误映射分工；为每协议补齐空流、显式 failed/incomplete、EOF、工具片段缺失、重复终态、取消和续轮 fixtures，防止错误完成后 Queue 继续。
- 模型能力以逐模型覆盖与协议支持共同判定；未知自定义强度保留输入并给明确校验，不静默替代模型/参数。补 PDF/文件 capability 到请求前校验和 UI 修复路径；模型发现不得覆盖用户设置。
- Infrastructure 代理路径验证默认/明确直连/凭据代理、禁用/删除、编辑和请求并发、连接失败/超时/证书/重定向边界；失败不静默直连。必要兼容选项显式配置，不降低全局 TLS 或日志保护。
- 用本地 JSON/SSE/认证代理做完整确定性矩阵，再在获准账号上分别验证四协议、图片/PDF、思考参数和断线恢复；真实请求只保存脱敏摘要和能力结论。当前本地兼容性不能等同所有远端服务已验证。

### R07：浏览器模式、接管与崩溃矩阵

- Browser 将 session/context/page、profile、DOM、截图/下载分别管理；App 选择内嵌/受管外部/显式 CDP 时预览任务归属与会话策略。切换先停止旧动作，等待任务资源清理再绑定新会话，不扫描任意端口。
- 为人工接管建立明确状态和排他 gate：交给用户时暂停自动操作，用户显式交回后刷新页面状态再继续。日常 profile 和桌面截图按敏感审批，借用 CDP context 的 cookie/storage 共享边界明确展示，未经授权不复制或清空。
- 已修复的取消/drain 路径扩展到 SDK 创建/launch/新标签/下载的其他时序；任务只关闭自己拥有的页面/context，不能关闭借用用户页面或另一实例。崩溃恢复只展示待重新附着，不重放 click/POST。
- 本地页面记录有界副作用计数，覆盖内外切换、创建取消、接管中停止、下载限额、host/browser 崩溃、受管/内嵌/日常/CDP 重启以及并发任务；最后用独立原生实例验证接管栏和真实窗口行为。

### R08：MCP 认证、配置竞争和传输恢复

- MCP 继续通过既有 manager/adapter 和统一工具管线，补认证策略及凭据引用、服务身份/传输/schema/tool 清单代际。配置写入在 repository 端使用 revision/CAS，覆盖预检后外部修改，不能只靠编辑器 preflight。
- 重连期间旧注册表要可识别且不可误用；发现结果仅按当前连接代际发布，失败保留可诊断状态。认证挑战、401/403、stdio 退出、HTTP 会话失效分别提供配置修复或显式重连，不把断开后的调用自动续传。
- 明确共享 stdio 服务进程与每任务调用的取消归属；R01 约束服务启动环境。取消/超时等待实际传输/子进程收尾，未知远端副作用保留审计，重启不重发。只读 annotation 仍需可信配置和校验。
- 确定性 stdio/HTTP 服务覆盖配置保存竞争、认证刷新、发现时工具变化、拒绝、输出过大、连接中断、取消重连与并行调用；原生编辑→发现→审批执行→故障→人工恢复形成一条完整记录。

### R09：子代理完整状态和当前版本验收

- Agent/Application 复用现有委托与子工具 gateway，补齐父子 run 身份、配置快照、终态和历史投影；触发条件是主模型可使用的委托指导，需验证实际选中而非宣称确定规则自动触发。失效/禁用模型不能静默换用父模型。
- R05 完成图像共享；文本/图片共享开关分别落实到子请求和预算。子工具只能继承并收窄主任务授权/allowlist，审批和审计保持子任务关联，取消父任务等待子模型和子工具收尾。
- 历史以运行/失败/取消/中断及未知工具结果展示，恢复可查看已完成片段；手动再次委托创建新 run，不重放旧工具。检查导航/销毁期间迟到进度，防止写入另一个对话。
- 当前版本重复设置保存→触发→选定模型/强度/提示词/共享→真实文件工具→父对话结果，覆盖手工允许与拒绝、错误/超时、父子取消、两子任务并发、EXE 重启和能力不足；本地模拟与真实提供商证据分开。

### R10：定时任务关闭、并发与恢复

- Infrastructure 调度器复用现有仓库和独立 task runner，明确保存配置版本、触发时间/时区、错过时间策略及单次运行所有权；编辑只影响后续触发，活动运行保留启动快照，不改前台模型/草稿。
- 启动继续跳过过期触发、标记遗留运行中断；应用关闭先阻止新认领，再取消并等待已有任务。崩溃或未知副作用只提供记录查看/手动新运行，不能恢复为自动重试。
- 现有暂停与停止保持不同语义：暂停未来计划，停止本次运行。针对重复触发/手动与定时碰撞、删除运行中计划、超时/时区/DST/系统时钟变化采用明确状态和事务认领，保留历史。
- 在独立原生实例编辑保存非默认模型/提示词/授权，验证实际请求和工具政策；并行保留另一任务及未发送草稿，关闭/杀进程后重启查看记录，确认零自动补跑。时间计算用可控时钟，真实关闭另验。

### R11：Git 区块操作、远端管理和原生工作流

- Tools.BuiltIn 增加独立整组/区块暂存和取消暂存工具，预览绑定 HEAD/index/工作区文件版本及精确补丁；审批后复核，拒绝混淆 rename/copy、外部索引变化和不完整差异，不能用 stage-all 替代用户选区。
- Application/Infrastructure 定义远端/上游配置预览与确认端口；App 提供真实读取、变更结果和凭据方式说明，使用明确账号/系统凭据集成，不读取任意浏览器或 CLI 凭据。只显示脱敏 endpoint，不回显原始 Git 配置或 push stderr。
- 提交消息当前只在内存按任务隔离；若补重启恢复，用独立版本化草稿保存，成功/失败与取消后核实实际 HEAD 再处理。保留 hooks/signing，不因测试失败跳过；hooks/helpers 服从 R01 隔离，不声称 Git 参数约束就是 OS 沙箱。
- 临时仓库验证区块选取、冲突/空变更、审批期变更、hook失败/已提交但回包取消、非快进拒绝及两任务；再做原生键盘预览确认、重启草稿恢复和获准真实远端认证。推送是单独显式动作，不随本次开发提交执行。

### R12：完整 Pull Request 工作流

- SourceControl Application 端口与 Infrastructure 来源客户端补个人收件箱、reviews/activity、过滤/分页/限流和版本刷新协调；按 account/repository/PR/head 保存 viewed/标记，版本变化明确标旧并重新加载，不混合不同 head 的评论和补丁。
- Tools.BuiltIn 为创建 PR、普通评论、行内评论、提交 review、关闭/合并等分别定义工具和授权；App 先准备可审阅的目标、base/head、正文、决策和差异位置，再独立确认。写权限必须显式新增，不能从只读 grant 推断。
- 写入前复核账号、仓库、权限和最新 revision，使用服务支持的幂等/条件请求；网络结果未知先查询并核实，不自动重试。创建 PR 不隐式 commit/push；发布分支缺失由独立 Git 推送流程解决。检查日志/第三方 URL 不因 PR 响应自动获得读取权限。
- OAuth 需要正式客户端注册和回调设计；GitLab 实现独立适配器及原生语义，不能只改 URL；云审阅另列接口/账号依赖。确定性 HTTP 验证竞态、拒绝、失败和重启，再用明确获准的测试仓库/账号作原生及真实服务验收。

详见 [PULL_REQUEST_WORKFLOW_PLAN.md](PULL_REQUEST_WORKFLOW_PLAN.md)。

### R13：插件、技能与扩展

- Application 定义目录/安装/启停/版本与组件端口，Infrastructure 实现本地文件包、配置与锁定版本；按已核实的 `plugin.json`、skills、`mcp.json` 和 OpenAI overlay 整体替换优先级解析，单独测试 legacy 导入。
- 先打通本地包预览→来源/哈希/能力说明→确认安装→启用→真实 skill 提示词或 MCP 工具执行→停用→卸载；目录/市场作为独立来源适配器，来源信任和账号授权不由包自行声明。zip traversal、链接、覆盖冲突和下载限额必须拒绝。
- skill 内容作为有来源的指令；MCP、hooks、代码和工具 UI 分别建立信任/审批/隔离。升级原子切换，运行中租约保留原版本；失败回滚配置，不自动执行安装脚本，不自动重放未完成 hook。停用立即阻止新调用，卸载只清理自己的受管文件。
- 官方 App Server 的 plugin list/read/install/uninstall 仍标为禁止生产客户端调用，不能把它作为可用目录。先评估公开支持的目录/connector/认证接口；依赖未开放就保持明确缺口。验收实际技能影响请求、MCP执行/审批、升级失败、并发与重启，不能只测包能保存。

详见 [PLUGIN_WORKFLOW_PLAN.md](PLUGIN_WORKFLOW_PLAN.md)。

### R14：目标绑定的桌面操作

- Application/Tools.Abstractions 定义任务绑定的窗口/应用会话和截图/坐标/键盘端口，Platform.Windows 实现；每种动作独立工具，复用敏感审批、超时、输出/截图产物和审计，不使用全局未绑定输入。
- 明确目标窗口身份、焦点和 DPI/坐标空间，操作前复核窗口/PID/会话代际；失焦、窗口关闭、UAC 安全桌面或用户接管时停止并报告，不向其他窗口“尽力点击”。
- 用独立本地测试窗口验指针/键盘、权限拒绝、失焦/窗口替换、取消与并发；人工接管后必须显式交回。日常用户窗口和账号只在具体授权场景验收，截图基础不算完整桌面控制。

### R15：Web 搜索

- 先核实参考入口语义，区分 provider 原生搜索、指定搜索服务和显式 MCP 适配；Application 定义结果/引用/预算端口，Providers 或独立工具实现，不能把普通网页导航冒充搜索。
- 若 provider 原生搜索会触发网络动作，必须先纳入同一授权/审批和审计，不能直接在请求里开启绕过工具政策；凭据、允许目的地、查询长度、页数/字节/时间和费用边界显式配置，搜索结果是非可信数据。
- 贯通查询→结果/来源→正文取回→回答引用，覆盖服务不可用、拒绝、取消、重复/虚假来源和恶意页面；真实服务证据另做，未知私有搜索接口保留缺口。

### R16：图像生成与编辑

- Providers/工具端口定义生成/编辑模型能力、输入图片归属、尺寸/数量、格式和预算，产物落文件存储并记录 hash/大小；App 显示进度、结果预览、保存及失败恢复，不能用输入缩略图代替生成。
- 服务请求经过统一管线；明确计费/外传输入与审批，取消结果未知时查已有任务/产物，不自动重发收费请求。校验下载来源、MIME/真实解码、输出字节/像素限制，禁止带授权头跟随任意重定向。
- 模拟协议验证完整创建/轮询/下载/展示与拒绝/限流/损坏/超大/取消/恢复；再用获准图像服务证明实际生成和编辑，保留服务依赖和未支持模型清单。

### R17：语音和输入预测

- 分别核实语音输入/实时会话/语音输出和输入预测的产品范围。Application 拆开音频会话、转写/合成、预测端口；Platform.Windows 管麦克风/播放，Providers 适配协议，App 提供录音/停止/确认发送及可关闭预测。
- 麦克风显式授权且可见录音状态，导航/关闭立即释放；转写先进入草稿，不自动发送或触发工具。预测有独立取消/代际，不能覆盖新输入，明确是否向远端发送草稿；不记录原始敏感音频。
- 可控音频与模拟服务测试拒绝、设备丢失、断流/超时、输入法、陈旧建议和并发会话；独立原生麦克风/扬声器及真实服务另验。不能以文本 SSE 通过冒充实时音频可用。

### R18：通知和长任务生命周期

- Application 定义通知事件/订阅、长任务 objective/run/budget/checkpoint；Platform.Windows 提供系统通知适配，Infrastructure 保存目标、预算和可查看状态，不以无限循环或定时任务同名入口代替。
- 通知按用户偏好、合并频率及静默时段发出，点击只定位对应任务；不显示密钥或敏感正文。目标具有开始/运行/需人工/完成/失败/取消/中断状态和时间/token/成本上限，暂停或恢复均遵循显式用户意图。
- 崩溃只恢复状态和检查点，不重放副作用；需要应用关闭后继续运行时必须先明确后台宿主/系统调度安装与权限，不能默认已有。验收预算耗尽、通知关闭/点击、并发前台草稿、进程重启和原生 toast。

### R19：可信文档预览、可视化与产物导出

- Application/Infrastructure 以 task/message/tool/artifact 所有权、MIME、hash、大小和来源管理生成文件；App 通过共享预览宿主按文本/代码/图片/PDF/Office/交互 HTML 分派，格式不支持时明确说明。
- PDF/Office 优先只读安全渲染或显式外部打开；交互内容在隔离 WebView 中禁默认网络、文件访问和宿主桥，所需权限逐项授予，不能把生成 HTML 当可信应用代码。远端链接/脚本不自动加载。
- 保存/导出由明确目的路径授权、原子写入和覆盖冲突检查完成；取消/失败保留原文件，产物缺失/篡改显示修复路径。验收生成→预览→保存→重启、恶意内容、超大资源、被占文件和并发回收，不能只验附件缩略图。

### R20：Appshots、浏览器扩展和人工接管协议

- 先逐项核实 Appshots 实际快照内容、支持平台和可公开格式；Browser/Application 定义任务所属的页面/视口/DOM/图片快照及脱敏策略，存产物元数据，不把现有截图工具直接改名算实现。
- 扩展需要明确 manifest、浏览器安装/版本、origin/extension 身份校验、一次配对和 task/tab 绑定；使用有界协议与能力协商，不任意打开调试端口。官方协议未公开时记录兼容缺口，独立扩展方案不能声称官方兼容。
- 与 R07 共享接管状态，测试标签切换/关闭、浏览器重启、连接被替换、消息伪造、拒绝和取消；实际安装及日常 profile 访问另获明确授权，原生扩展/接管体验单独验收。

### R21：宠物和 Codex Micro 外设

- 宠物先核实动画/活动反馈/点击入口/窗口跟随的实际语义，App 实现独立可关闭的展示服务和受管资源；不读取任意窗口或让装饰动作触发工具，资源缺失/重启/多窗口及无障碍可关闭可测试。
- Micro 先确认公开硬件/协议、Windows驱动、设备配对和权限。Platform.Windows 提供设备端口，Application 只接受白名单事件，敏感命令仍进入普通授权/审批；未公开协议、无硬件就保留未验证，不伪造可用设备。
- 宠物用状态/动画/交互与原生窗口验证；外设用协议模拟覆盖断连/伪造/取消，再用真实硬件验证。两项独立列范围、依赖和进展，不以静态图标交付。

### R22：Sites、dots、Space、账号和远程/云/移动能力

- 先建立逐入口语义/公开接口/账号计划/凭据/平台限制/可实施边界表：Sites 的构建发布协作、dots 的持续责任代理、Space 的共享页面、Sign in with ChatGPT 的登录授权，以及远程运行、云审阅和移动访问分别核实；当前目录信息不能当 Windows 实机入口完整清单。
- Application 为身份、远程运行、部署、共享/同步分别建端口；只有官方公开允许的 API/SDK 才实现服务适配。ChatGPT 登录需要注册、授权范围、token刷新/撤销和回调设计，不能把本地 API key 当同等登录，也不调用内部私有端点或读取浏览器 Cookie。
- 接口开放时实现可取消的提交/状态/产物/日志和明确授权的写动作；远程任务与本地任务范围分离，凭据最小化，防重复发布/支付/分享。没有公开接口时提出独立本地/自托管对接方案并标明语义差异，由用户确认范围，不能擅自作为专有服务已完成。
- 明确待决策项：需对接的具体官方服务、可用账号/注册权限、是否接受独立后端、同步数据范围和运维/费用。先做只读能力发现与可审阅设计，外部账号/部署写入在具体结果就绪后单独确认；完成模拟与获准真实服务/跨端恢复验收。

### R23：当前版本原生界面与可访问性

- App 继续共享控件/集中样式/本地化，列每个关键入口的加载/空/忙/禁用/失败/恢复和键盘路径；按实际最小尺寸、常见尺寸、最大化、明暗主题及多 DPI 组合验收，检查焦点、弹层、滚动、长路径/长文本和屏幕阅读器语义。
- 在独立测试数据和独立 EXE 窗口完成对话/Queue/Steer、附件、Git/工作树、设置、审批、浏览器、子代理、定时和 PR 端到端操作，记录 App/依赖身份及数据/请求断言；不操作或关闭用户现有窗口，不为通过而放宽审批。
- 原生 resize/DPI 诊断先区分工具宿主、操作系统消息和产品布局问题；截图黑帧/输入访问拒绝就记录限制，恢复可用测试环境后补验。截图用于遮挡、裁切、对齐和可读性，像素接近不作为验收门槛。
- 组件/Headless 测试先验证逻辑和布局，原生测试再验证实际窗口/焦点/输入；真实账号请求另列，三种证据不能互换。已发现产品问题才做针对修复和回归，不用调整测试断言掩盖交互失败。

### R24：JIT 单文件 Release 发布前验收

- 保持现有 Windows x64、自包含、单文件、无 AOT/无裁剪；用 `scripts/publish.ps1` 的 staging/验证/替换流程，不直接覆盖已有包。当前用户只要求本地提交，本次不制作或发布新版本。
- 功能与安全阻断关闭后，在固定提交的独立候选运行相关测试、完整 Debug/Release、结构/依赖/共享组件检查，核对源码与二进制身份，保留全部原始失败和修复后的新证据。
- 在干净 Windows 环境检查 EXE/ZIP 内容、版本、签名方案、解包目录权限、首次启动、已有数据库逐版本迁移、WebView2/Playwright/ConPTY/native依赖、离线/代理、正常退出/崩溃和手动恢复；此前包的可恢复性也要验证。
- 只有约定范围、关键原生工作流和真实依赖验收通过，且不存在阻断，才能标整个目标完成。发布到外部平台、推送或安装仍需用户明确要求；当前交付不作已完成或发布可用声明。

## 五、下一轮开始位置

先从 R01 的受支持隔离后端与验收环境决策继续，复用既有诊断证据，不重复 KnownDll ACL
实验。并行性在设计上按任务隔离落实，不复用一次性管理员许可。之后逐项关闭 P1 的真实
恢复/并发/原生缺口，再推进 P2/P3。每关闭一项同步更新本表和功能矩阵，附实际入口、
运行数据与证据边界；无法完成的外部依赖保持明确的未完成状态。
