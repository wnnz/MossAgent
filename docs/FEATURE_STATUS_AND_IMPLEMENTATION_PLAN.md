# MossAgent 功能完成情况与后续实现方案

盘点日期：2026-10-21。代码基线：`7949b87f115c71dfb04596775748be8f1439ce1e`。
分支：`feature/initial-mossagent`。本文件是该提交的产品能力快照与后续开发计划。

## 1. 当前结论和阅读方式

MossAgent 已具备桌面 Coding Agent 的主要本地运行链路：项目/无项目对话、流式与历史、
文件工具、Git、终端、提供商/模型/代理、浏览器、MCP、子代理、定时任务及只读 PR 审阅。
但这些大类仍有功能缺口、安全边界或实机验收缺口，**整个产品目标尚未完成，也尚不满足发布验收条件**。

目前最重要的阻断是：**命令和终端仍能访问任务授权根之外的文件**。已有审批、工作目录
验证、进程树和文件工具的句柄保护，均不能替代命令文件系统隔离。工作树创建/清理的
最终对象交接、严格撤权排空、当前版本全套与关键原生工作流，也仍需完成。

本文使用以下状态，不能互相替代：

| 状态 | 含义 |
| --- | --- |
| 已完成的限定能力 | 有生产入口、实际运行链路和对应证据；只表示表中写明的范围，不表示整类功能全部完成 |
| 部分完成 | 基础链路存在，但仍缺少明确列出的子能无用力或安全边界 |
| 未实现 | 当前盘点没有完整生产链路；设计文档、可保存配置或同名按钮不计为完成 |
| 未验证 | 实现或方案存在，但缺少当前候选、实际平台、真实服务或指定时序的验收证据 |
| 外部依赖待就绪/核实 | 受独立系统、官方接口、账号计划、注册权限或硬件限制，不能据此宣称已完成或擅自排除范围 |

R01–R24 沿用[历史阶段交付清单](archive/OVERVIEW.md#delivery-status-and-roadmap)的编号；本文件统一展开当前缺口，
尤其补入 schema 26–28 和文件/产物句柄安全增量，不把这些已经实现的部分重新列为待开发。
历史专项中的“未提交”“正在运行”等描述只代表其检查点，当前提交身份以上述基线为准。
其他 Codex 入口沿用[功能矩阵](archive/OVERVIEW.md#functional-parity-matrix)中 2026-10-19 的参考盘点；
本轮没有重新操作 Codex 窗口或核实所有外部接口，实施前应重新确认服务和 Windows 支持范围。

## 2. 已完成的限定能力

下表列举可保留并继续发展的能力。“证据”链接包含实际入口、运行结果和限制；部分是
历史专项的有界证据，不能推断为当前版本每项实机组合均已通过。剩余编号指向后文计划。

### 2.1 对话、任务和草稿

| 编号 | 已完成的能力及实际链路 | 证据与剩余范围 |
| --- | --- | --- |
| C01 | 项目对话和无项目对话：新对话 → `WorkspaceTaskFactory` → 所选提供商 → Agent；无项目普通聊天不隐式获取目录、shell 或通用工具权限 | [无项目对话](archive/CONVERSATIONS.md#projectless-chat)、[无项目附件](archive/CONVERSATIONS.md#projectless-attachments-acceptance)；原生全流程见 R23 |
| C02 | 流式响应、多轮上下文、消息落库、历史重开和任务切换；历史消息参与下一轮请求 | [历史上下文](archive/CONVERSATIONS.md#history-context-acceptance)、[流终态](archive/PROVIDERS.md#provider-stream-completion-acceptance)；完整协议/断线矩阵见 R06 |
| C03 | 每任务运行控制、独立取消及事件流；发送准备可取消，失败保留草稿，提交后取消有可查看终态；启动恢复为中断，不自动重放 | [发送准备取消](archive/CONVERSATIONS.md#send-preparation-cancellation-acceptance)、[关闭恢复](archive/TERMINAL_TOOLS.md#shutdown-recovery-acceptance)；其他崩溃断点见 R03/R04/R23 |
| C04 | 对话、新项目会话和无项目草稿隔离；文本/附件草稿持久化、导航恢复、版本冲突保护；消息提交与草稿消费共用 SQLite 事务，保留发送时的新输入 | [草稿持久化](archive/CONVERSATIONS.md#draft-background-persistence-acceptance)、[原子发送](archive/CONVERSATIONS.md#conversation-submission-acceptance)；事务中真实终止及原生组合见 R03/R23 |
| C05 | Queue：运行中 Enter 入队，编辑/排序/移除/手动继续；保存模型、强度、提示词、授权和附件快照，按原任务分派；仅成功活动链自动续发 | [Queue 运行](archive/CONVERSATIONS.md#conversation-followup-runtime-acceptance)、[分派](archive/CONVERSATIONS.md#conversation-followup-dispatch-acceptance)、[进程恢复](archive/CONVERSATIONS.md#conversation-followup-process-recovery)；其余竞争见 R03 |
| C06 | 文本与附件 Steer：活动 Ctrl+Enter 接收草稿及 receipt，附件元数据随接收原子转移（schema 39），在当前工具批次后的模型边界应用为携带附件记录的用户消息；保持原运行模型/权限、有界 mailbox、幂等和历史状态 | [文本引导](archive/CONVERSATIONS.md#conversation-steering-acceptance)；附件引导已落地但仍要求非空文字，竞争/崩溃矩阵见 R03 |

### 2.2 授权、文件、Git 和终端

| 编号 | 已完成的能力及实际链路 | 证据与剩余范围 |
| --- | --- | --- |
| C07 | 项目和多授权根管理；schema 26 条件保存及授权版本撤销旧 Agent、工具、审批和长驻 PTY，跨实例有界轮询，Queue 暂停，保留前台草稿 | [授权撤销](archive/SECURITY.md#project-authorization-revocation-acceptance)；本机租约准入排空已补；跨进程及完整资源排空仍缺，见 R02 |
| C08 | schema 27 绑定项目根的 grant、卷和目录文件 ID；旧项目待明确确认，同路径替换不继承授权；重新授权推进版本并撤销旧租约 | [项目根对象身份](archive/SECURITY.md#root-object-authorization-acceptance)；ABA、别名/支持范围、严格撤权见 R02 |
| C09 | 文件搜索、读取、预览和编辑校验通过标准工具管线；读取/预览等已接入入口在生产 Windows 打开后校验文件句柄，防止已覆盖场景的链接/重解析点越界 | [读取句柄保护](archive/SECURITY.md#read-handle-path-safety-acceptance)、[编辑链路](archive/CONVERSATIONS.md#file-editing-acceptance)；其余枚举入口和完整竞态审计见 R02 |
| C10 | 本地常规 NTFS 原子文本写入：固定父目录链、独占暂存、相对句柄提交、外部修改检查及失败清理；不支持的元数据保守拒绝 | [原子写入](archive/SECURITY.md#write-handle-path-safety-acceptance)；最终名称条件替换尚缺，见 R02 |
| C11 | 普通/递归删除、撤销新建文件按受控句柄执行；删除前有界预检，部分完成和取消结果可查看 | [删除与撤销](archive/SECURITY.md#delete-handle-path-safety-acceptance)；不是树事务或崩溃回滚，见 R02 |
| C12 | 文件/目录树复制和显式目录创建：源树句柄预检、目标父目录租约、逐文件原子提交；同卷移动/重命名绑定源对象，跨卷明确拒绝 | [复制](archive/SECURITY.md#copy-handle-path-safety-acceptance)、[移动](archive/SECURITY.md#move-handle-path-safety-acceptance)；元数据完整克隆、跨卷安全方案及最终竞争仍见 R02 |
| C13 | 任务产物读取、预览、编辑前后快照和最终补丁使用共享会话；完成与调用方保留分离，失败按原句柄清理，复制后校验 hash | [产物读取](archive/SECURITY.md#artifact-handle-path-safety-acceptance)、[共享产物会话](archive/SECURITY.md#owned-artifact-session-acceptance)；会话阶段期刊已由 schema 37 覆盖，引用回收见 R05 |
| C14 | Git 状态、已暂存/未暂存差异、任务快照统计、补丁审阅及确认撤销；Git diff/numstat 临时输入持续固定、只共享读取并按句柄清理 | [差异审阅](archive/GIT_WORKTREES.md#diff-review-acceptance)、[撤销](archive/GIT_WORKTREES.md#file-change-undo-acceptance)、[Git 临时输入](archive/GIT_WORKTREES.md#git-diff-input-session-acceptance)；不是 Git 进程隔离，见 R01/R11 |
| C15 | 文件级暂存/取消暂存、已暂存内容预览确认提交、捕获 commit 的单分支预览确认推送；审批后复核 HEAD/索引/远端，不自动 stage-all 或强推 | [暂存](archive/GIT_WORKTREES.md#git-staging-acceptance)、[提交](archive/GIT_WORKTREES.md#git-commit-acceptance)、[推送](archive/GIT_WORKTREES.md#git-push-acceptance)；区块、远端配置和真实认证见 R11 |
| C16 | 独立工作树创建、手工选择分支/标签/提交并固定基准 commit、有界本地分支/标签搜索分页及 commit 预览、连续对话、重开、工具守卫和确认清理；失败目录保留供核实 | [基准选择](archive/GIT_WORKTREES.md#worktree-base-reference-acceptance)、[工作树路径](archive/GIT_WORKTREES.md#worktree-path-safety-acceptance)；远端获取、创建/清理竞态及崩溃见 R04 |
| C17 | schema 28 绑定工作树自身的 task/project/grant/目录对象身份；恢复拒绝同路径替换，即使复制 Git 元数据；Agent/工具/PTY 持续持有根链；旧工作树明确确认，不消耗草稿或续跑 | [工作树对象授权](archive/GIT_WORKTREES.md#worktree-object-authorization-acceptance)；替换对象重新授权、最终交接见 R02/R04 |
| C18 | PowerShell 命令及 ConPTY 交互终端基础：输入/输出、VT、剪贴板/输入法、任务所属进程树、时限、有界输出和停止链路 | [命令](archive/TERMINAL_TOOLS.md#command-execution-acceptance)、[终端](archive/TERMINAL_TOOLS.md#terminal-interaction-acceptance)；**不具备严格文件系统隔离**，见 R01 |

### 2.3 提供商、工具、浏览器、MCP 和扩展代理

| 编号 | 已完成的能力及实际链路 | 证据与剩余范围 |
| --- | --- | --- |
| C19 | 多提供商、多 HTTP/SOCKS5 代理、默认代理及明确直连；设置分组、展开、逐项编辑、持久化和真实适配器请求；代理失效不静默直连 | [提供商 UI](archive/PROVIDERS.md#provider-ui-workflow-acceptance)、[认证代理](archive/PROVIDERS.md#authenticated-provider-proxy-acceptance)；真实远端与配置并发矩阵见 R06 |
| C20 | OpenAI Responses、兼容 Chat、Gemini GenerateContent、Anthropic Messages；模型发现/手工添加、上下文/输出限制、选项或自定义思考强度及其他参数参与请求或校验 | [思考与能力](archive/PROVIDERS.md#model-reasoning-capability-acceptance)、[Gemini 续轮](archive/PROVIDERS.md#gemini-continuation-acceptance)、[模型配置](archive/PROVIDERS.md#model-configuration-native-acceptance)；完整协议和远端支持见 R06 |
| C21 | UTF-8 及静态 PNG/JPEG/GIF/WebP 明确选择、解码/限额、任务快照、草稿和历史恢复、能力不足拒绝；后台缩略图及 JPEG EXIF 八方向处理，源文件删除不破坏已保存快照 | [图像附件](archive/CONVERSATIONS.md#image-attachment-workflow-acceptance)、[方向](archive/CONVERSATIONS.md#image-orientation-acceptance)、[历史缩略图](archive/CONVERSATIONS.md#attachment-thumbnail-acceptance--sent-message-and-history-extension)；PDF、全图、共享与回收见 R05 |
| C22 | 统一工具查找/schema/能力与路径/风险/审批/取消与超时/输出限制/产物/审计；主/子代理工具 allowlist，UI 展示等待、审批、执行和结果 | [工具 schema](archive/TERMINAL_TOOLS.md#tool-schema-acceptance)、[工具范围](archive/TERMINAL_TOOLS.md#tool-scope-acceptance)、[审批展示](archive/TERMINAL_TOOLS.md#global-approval-presentation-acceptance)；入口全面审计及 OS 边界见 R01/R02 |
| C23 | schema 24 启动恢复遗留进行中工具审计为结果未知，保留人工核实提示；不显示为成功、不自动重做 | [中断审计](archive/TERMINAL_TOOLS.md#audit-interruption-recovery-acceptance)；全副作用时序仍见 R02/R03/R07 |
| C24 | 内嵌 WebView2、受管外部 Chrome/Edge、显式 CDP 和任务标签页；DOM 操作/截图/下载与接管栏；取消/超时后旧会话失效并清理实际 SDK 资源 | [浏览器取消](archive/BROWSER_MCP.md#browser-operation-cancellation-acceptance)、[Queue 浏览器链路](archive/BROWSER_MCP.md#queue-browser-runtime-acceptance)、[已提交副作用恢复](archive/BROWSER_MCP.md#browser-committed-effect-recovery-acceptance)；模式切换/接管完整矩阵见 R07 |
| C25 | 浏览器截图/下载进入任务所属产物会话，审批后创建、有界导出、hash、SDK 暂存清理及最终复核；清理未知不伪装成功 | [浏览器产物](archive/BROWSER_MCP.md#browser-artifact-session-acceptance)；产物会话未知状态持久化已由 schema 37 覆盖，下载暂存尺寸中途轮询与有界流出已落地，真实网络配额仍缺，见 R02/R07 |
| C26 | MCP stdio/Streamable HTTP 配置、headers/代理、发现、动态注册和标准管线执行；故障提示、显式重连、重载及迟到结果抑制 | [MCP 执行](archive/BROWSER_MCP.md#mcp-execution-acceptance)、[UI 恢复](archive/BROWSER_MCP.md#mcp-ui-recovery-acceptance)；认证挑战流程、配置代际和传输中断见 R08（认证/会话/进程退出分类提示已落地） |
| C27 | 自定义子代理名称、触发指导、提示词、模型、强度和上下文共享；`subagent.run` 选定子模型执行，子工具继承并收窄权限，父对话展示过程和结果，禁止递归委托 | [设置到运行](archive/AGENTS_AUTOMATIONS.md#subagent-settings-to-runtime-acceptance)、[生命周期](archive/AGENTS_AUTOMATIONS.md#subagent-lifecycle-native-acceptance)；触发依赖主模型选择，非确定规则引擎；完整状态矩阵及图片共享见 R09/R05 |

### 2.4 定时任务、PR 和桌面基础

| 编号 | 已完成的能力及实际链路 | 证据与剩余范围 |
| --- | --- | --- |
| C28 | 定时任务创建/编辑、启用/暂停、手动运行/停止、历史/打开结果；调度认领 → 保存的模型选择/提示词/审批策略 → 独立后台 task，不消费前台草稿；启动跳过过期触发并标记中断 | [定时 UI](archive/AGENTS_AUTOMATIONS.md#automation-ui-workflow-acceptance)、[计划修改](archive/AGENTS_AUTOMATIONS.md#automation-mutation-acceptance)；运行授权根默认钉住保存时快照目录集，计划可显式开启跟随项目授权变更；关闭/DST/并发恢复见 R10 |
| C29 | 独立 GitHub 来源账号/token/代理和仓库只读授权；开放 PR、URL 直达、描述/分页文件补丁/评论/checks；当前提交有效行内评论定位；固定快照及按需只读查询对话 | [来源账号](archive/GIT_WORKTREES.md#source-account-connection-acceptance)、[行内评论](archive/GIT_WORKTREES.md#source-pr-inline-comments-acceptance)、[查询对话](archive/GIT_WORKTREES.md#source-pr-on-demand-reading-acceptance)、[URL](archive/GIT_WORKTREES.md#source-pr-url-acceptance)；不是完整 PR 写工作流，见 R12 |
| C30 | Windows 优先的 Avalonia/.NET 10 模块化桌面；共享包装控件、集中明暗主题、本地化、键盘与紧凑布局基础；编译/测试约束组件与依赖方向 | [架构](ARCHITECTURE.md)、[共享组件](UI_COMPONENTS.md)；当前原生/DPI/焦点全覆盖及已知 restore 裁切见 R23 |
| C31 | SQLite 向前迁移、结构化任务状态及文件型大产物；JIT 构建和 Windows x64 自包含单文件配置，明确关闭 AOT/裁剪 | [迁移及架构](ARCHITECTURE.md)、[发布配置](../src/MossAgent.App/Properties/PublishProfiles/WindowsX64.pubxml)；当前正式包验收见 R24 |

## 3. 当前验证证据及限制

2026-10-22 代码精简与结构整理后，完整 Debug 解决方案构建零警告、零错误，1,483 项相关
测试通过，36 条构建规则及实际拒绝路径验证通过。验证时记录曾保存在
`artifacts/latest-validation/`，本机原始产物目前已清理，入库保留本节摘要；
本次未运行全部解决方案测试、原生 GUI、真实提供商账号或 Release 发布验证。

最近的产品运行检查点原路径为 `artifacts/worktree-object-authorization-20261021/`，提交记录确认该候选
代码进入 `7949b87`。Debug 使用独立输出目录。原始产物是本机忽略文件，不随 Git 克隆分发；
入库摘要见[工作树授权验收](archive/GIT_WORKTREES.md#worktree-object-authorization-acceptance)。
该目录及其他历史产物曾汇总到本机 `artifacts/history-20261022.zip`，该归档随后也已移除。
下表记录的是该产品运行检查点，不能将其验证范围与后续维护测试合并为一次全量验收。

| 检查 | 已有结果 | 不能推断的内容 |
| --- | --- | --- |
| Debug / Release 完整解决方案构建 | 两者均零警告、零错误 | 不等于当前发布包可用或整产品完成 |
| 每配置七套完整测试 | Infrastructure 835、Agent 162、Tools 695、Windows 422、Browser 113、Providers 617、Visual 300，合计 3,144 | 不是八套完整解决方案测试 |
| 每配置 App 相关精选 | 414 通过；加上上述为各 3,558，无跳过/失败/中止 | 没有再次完整运行 App 全套，不覆盖全部 UI、自动化、PR 和真实账号组合 |
| 首轮 App 全套尝试 | 修复前程序集手动停止；已报告 5,074 通过、28 失败、合计 5,102；28 个完整测试名称在最终两配置均复验通过 | 不将中止记录或后续精选写为全量通过，也不把测试准备修复说成所有产品失败均已解决 |
| 输入和结构 | 两配置前后/配置间及当时工作区 2,688 项源码配置一致；2,494 个 C# 文件单类型/规模检查零违规，文件最大 500 行、方法最大 78 行 | 不用输入一致和结构检查替代功能验收 |
| 本地真实资源 | 对应专项使用真实 NTFS、Git、SQLite、Chrome、ConPTY 和独立宿主进程，具体组合分项记录 | “原生 NTFS/ConPTY”不等于原生 GUI；模拟模型不等于真实 AI 服务 |
| UI 证据 | 组件/Headless 明暗、窄/宽窗口及键盘检查；历史有部分独立 EXE 记录 | 当前全部关键原生窗口、高 DPI、真实模型、真实 PR 账号及 MXC 仍待验收 |

本轮为文档盘点：读取代码入口和已有证据，没有重新执行付费服务、系统提权、原生窗口操作、
发布或全套测试；上述构建/测试数字属于明确记录的运行检查点。

## 4. 未完成范围总表

P0 是安全阻断，P1 是现有核心闭环，P2 是编码协作及扩展，P3 是其他参考功能，P4 是最终交付。
未实现和未验证同时出现时，应先证明现有行为，再补缺失实现，不能将两者混成“只差测试”。

| 编号 | 当前状态 | 未完成的功能或边界 | 优先级/依赖 |
| --- | --- | --- | --- |
| R01 | 部分完成；安全阻断 | 命令/PTY/Git hooks/helpers/stdio MCP 等代码执行的真实文件系统隔离 | P0；原生 MXC 独立环境尚未就绪 |
| R02 | 部分完成、部分未验证 | 产物/diff/SDK 暂存输入会话恢复期刊已落地（schema 37–38：创建即登记、阶段分级、持久化尺寸配额、启动对账、恢复面板人工核实），附件快照引用回收已落地（schema 33）；浏览器下载已落地暂存尺寸中途轮询取消与 64 KiB 有界流出复制（字节级网络配额 Playwright 无 API，仍不可能）；本进程撤权排空状态已可观察并按项目行分层显示（撤销中/退出状态未知，释放失败仍阻断准入，30 秒排空限时超时归 CleanupUnknown，根会话释放失败经 2 秒延迟单次有界重试自愈瞬时失败）；跨实例排空聚合已落地（schema 48 `project_drain_states` 共享板：本机三态转移即在锁外尽力发布、Idle 删行、实例退出清行，徽标取本机与板上最坏态，超 120 秒未更新按 CleanupUnknown 计入并由任一观察者顺手回收该行——崩溃实例的排空随下一次观察收敛而非永久未知，徽标对非 Idle 行每约 30 秒做一次有界延迟再观察，读失败回退本机——是尽力告示板而非共识协议）；剩余缺口为真实网络/缓存配额、持续型 CleanupUnknown 的人工核实及 R01 依赖项 | P0；复用已有句柄端口和 schema 26–28 |
| R03 | 附件 Steer 与发送偏好已落地；其他矩阵未验证 | schema 39 附件引导已接入（原子 receipt/草稿消费、模型图像能力门控、上下文预算）；附件接收回滚、重启中断与 Steer/入队草稿消费竞争去重已覆盖，陈旧快照重排/移除/编辑 CAS 冲突的 VM 端用例已补齐，完整宿主终止矩阵及 IME 验收仍缺 | P1；R02/R05 |
| R04 | 部分完成 | 创建/清理 intent 生命周期、恢复面板、重试移除、注册槽级元数据清理、Windows 句柄级删除守卫、清理前新运行准入联锁（drain 门）与替换对象 CAS 重新授权已落地；非 Windows 守卫及完整崩溃生命周期仍缺 | P1；R01/R02 |
| R05 | 部分完成 | 子代理图像共享已落地（ShareContext 开关 + 子模型×协议能力门控，不支持即明确失败）；消息附件全图查看已落地（共享预览面板：图像/PDF/文本只读快照，FilePreviewFormat 头校验 + 8192/8Mpx/2MiB 有界解码，AttachmentImage 摄取校验，AttachmentSnapshotReferences 四类引用核查）；其余原生文件类型预览形态仍缺 | P1；模型协议与产物会话 |
| R06 | 部分完成、部分未验证 | 流终态夹具与能力门控已落地并修复六个真实缺陷；代理配置快照矩阵已补"同时编辑和请求"并发格（单查询快照，撕裂混合即失败）；剩显式重试边界与真实账号验证 | P1；获准真实服务账号 |
| R07 | 部分完成、部分未验证 | 人工接管排他门已落地；浏览器会话身份与待决副作用期刊已落地（schema 43：任务归属会话登记 + 十类变更操作 intent 仅存 16 位 SHA-256 目标前缀，启动对账将残留 attached/committed 置 interrupted，审计面板投影会话与操作终态，绝不自动重放）；内外/CDP 模式切换时序与其他创建/副作用崩溃组合仍缺 | P1；R01/R02 |
| R08 | 部分完成 | MCP 认证/会话失效/stdio 进程退出的失败分类与可操作建议已落地（开发分支）；认证恢复/OAuth 挑战、配置 CAS/代际、断流/并发取消的完整闭环仍缺 | P1；R01/R02 |
| R09 | 部分完成、部分未验证 | 父子 run 期刊已落地（subagent_runs v41 快照/五态投影/启动核对 interrupted，绝不重放）；UI 历史投影已落地（审计面板五态子代理运行列表+快照详情，畸形快照回退 profile+状态）；可控模型触发验证、禁用模型失败验收、权限继承收窄矩阵与其余触发/审批/共享/并发/恢复验收仍待 | P1；R05/R06 |
| R10 | 部分完成；其他组合待验 | 定时计划授权/参数版本、关闭/中断/并发/多实例、时区与 DST，以及关闭后的行为实机验收。授权快照已落地且认领时固定（run 行记录 snapshot_generation v42、历史按代际标注、保存时在途运行如实提示沿用旧快照）；编辑后自动停止在途运行仍非默认行为，实机验收仍缺 | P1；R02/R06 |
| R11 | 部分完成 | 单文件区块暂存/取消暂存已落地（rename/copy/二进制/新增/删除/仅模式/冲突明确拒绝）；整组暂存/取消暂存已落地（git.stage_paths/unstage_paths，1–64 路径单命令原子、越界/链接/目录逐项拒绝）；提交消息草稿独立版本持久化（git_commit_drafts v40，编辑去抖 CAS 写入、重启恢复）与提交未知结果核对（复核 HEAD/暂存数后消费或保留草稿）已落地；推送非确定性失败后以远端跟踪引用核对实际结果（已推送→明示，未证实→保留原错误，核对失败→明示未知）；预览绑定已落地（git.diff 输出尾部绑定 diff-sha256 与各文件 file-sha256，hunk 工具 required expectedDiff 执行期哈希复核、漂移即拒绝）；远端/上游配置读取与凭据方式展示已落地（git.remotes 只读工具：脱敏 scheme://host 端点、local/ssh/https/embedded/unsupported 凭据方式分类、credential.helper 值链、当前分支上游；推送对话框凭据行揭示 URL 内嵌凭据并警示）；远端/上游条件变更端口已落地（git.remote_set_url 按脱敏端点+凭据方式做语义级 CAS、本地目的地复用推送授权端口、永不回显内嵌凭据，git.branch_upstream 按原文 CAS 写 branch.*.remote/merge 并如实报告部分应用）；推送非快进拒绝分类引导已落地（合并诊断分类为确定性 git_push_non_ff、提示先合并/变基远端更新再刷新预览、原始输出不回显）；真实远端核对部分落地：git.ls_remote 已落地（只读、脱敏端点、有界 64 条、非交互、凭据永不回显），供模型主动核对远端 refs；推送失败后自动核对仍走本地远端跟踪引用（离线、不触网络）；原生验收仍缺 | P2；R01/R02 |
| R12 | 部分完成 | GitHub reviews/activity 只读已端到端落地（审阅面板分区 + 版本绑定工具）；按账号个人 PR 收件箱只读已落地（search/issues 固定端点、角色筛选、分页、incomplete 标记，双解析 revision 看守，不升级 grant、无写入）；已读/版本标记持久化已落地（pull_request_marks v44，按 account/repository/PR 记录查看时 head，列表批量取标记后与条目在同一代际原子发布，head 漂移明示“已更新”，打开详情按当前 head 尽力落账，标记不随账号删除清理）；按 grant 显式写授权模型已落地（v45 can_write，独立写解析端口，存量行只读、无升级通道）；确认写工具已落地五个（source.pr_comment_create：写授权+版本核对+两段式面板确认，绝不重试，未知结果经最近评论精确匹配核实；source.pr_review_submit：approve/request_changes/comment 三态结论+可选正文，POST 固定 commit_id 到已核对的 head，未知结果按状态/提交/规范化正文升序分页核实评审（≤10 页×20 条，服务端按时间升序返回），面板评审分区需显式选择结论并两段确认；source.pr_close：PATCH pulls/{n} {state:"closed"}，响应校验 url 归属与 number、返回 state 由写入器再核，未知结果经一次 GET 摘要核实 state=="closed"，面板顶部两段确认+只读提示+成功后重读详情；source.pr_merge：PUT pulls/{n}/merge，负载以 sha 字段钉住已核对的 head、merge_method 为 merge/squash/rebase 闭集三选（默认拒绝），响应校验 merged==true 且 sha 为 40/64 位 hex 提交，未知结果经一次 GET 详情核实 merged==true 且 head.sha 相同（合并后 base 必前进故不比 base），命中回执带 merge_commit_sha，面板顶部须显式选择合并方式并两段确认、草稿 PR 显示提示而非按钮、成功后重读详情；source.pr_create：POST pulls 新建 PR，无既有 revision 可钉故按无 revision 的写预校验，分支列表走只读端点（resolve→GET→resolve 双重核查，≤100 项直接枚举、重名/坏 sha 一律拒绝），base/head 必须显式选择且不得相同，两段确认后单次 POST，服务端 422 拒绝重复 PR，未知结果经一次有界 GET 按 head/base/标题精确匹配核实，命中返回已核实回执并由既有链接加载路径打开新 PR，绝不二次 POST）；OAuth/GitLab/云审阅仍缺 | P2；公开接口与后续写工具 |
| R13 | 本地包预览、安装、同名升级替换、生命周期、skill 提示词注入、单技能启停、按会话技能选择与信任修订计数已落地；MCP 等接入未实现 | 本地插件包预览管线（归档安全校验+清单/组件解析+整包 SHA-256）、安装基础设施（plugin_packages v46 行、重校验提取到不可变 hash 目录、启动 reconcile 清 staging 残留并报告 orphan final 不删、同名升级替换即同 name 异 version 安装在单事务删除全部同名行并写入新行与基线、新行恒 Installed 须显式重新启用、被替换载荷目录提交后尽力删除、settings/hashes 随父行级联、task_skill_overrides 旧 id 行惰性失效）与生命周期操作（启用/禁用状态迁移仅持久化标记、卸载删行后尽力删载荷、源包不动、残留变 orphan 上报）及设置页本地插件 UI（选包→组件清单预览→确认安装、启停、两段确认卸载）已落地；skill 组件注入也已落地（Enabled 插件 SKILL.md 派发时物化：路径复核、≤8KiB/文件、≤24K 字符总量、护栏前带标签段、按任务落 task_skill_activations v47 快照、source-review 聊天不注入、审计页可查看激活行与注入文本节选）；单技能启停已落地（plugin_component_settings v49：(plugin,component) 最后一次显式选择、无行即默认启用、随插件行 ON DELETE CASCADE 清除、重装换新 id 即重置为默认——语义上有意为之；派发端按插件批量读取覆盖并跳过停用技能、停用不留激活行；设置页插件行内嵌技能列表逐个启停，仅影响后续派发、不回改已派发任务快照）；组件载荷信任基线也已落地（plugin_component_hashes v50：安装时按清单组件逐文件 SHA-256 随插件行原子落库、只插不改、随行级联，派发时对每个启用技能先验字节哈希再解码，缺行或不一致即置 Drifted 信任态并跳过该插件其余组件；state CHECK 经表重建放宽，启用层以 plugin_payload_drifted 拒绝漂移行、停用/卸载仍可用，启动 reconcile 为存量行按磁盘现状回填缺失基线、文件缺失即不留行——恢复仅可卸载重装）；按会话技能选择也已落地（task_skill_overrides v51：(task,plugin,component) 最后一次显式选择、随 agent_tasks ON DELETE CASCADE、plugin_id 有意不设外键——卸载重装换新 id 后陈旧行保持惰性历史；派发端按任务读一次覆盖并按 任务行→全局行→默认启用 合并，任务行可重开全局停用的技能且同样过基线漂移校验；作曲区"技能"选择器按插件分组列出生效态、未建任务先记草稿、任务创建即序列化落库、可一键重置为全局）；MCP 运行时接入（stdio 等 R01 沙箱）、市场、OAuth、hooks 仍未实现 | P2；R01/R08及目录/账号接口 |
| R14 | 部分基础，完整操作未实现 | 目标窗口绑定的桌面指针、键盘、接管和恢复 | P2；Windows 原生输入与 R02 |
| R15 | 未实现专用链路 | Web 搜索、来源正文获取及答案引用 | P3；搜索服务/模型原生能力 |
| R16 | 未实现专用链路 | 图像生成/编辑、任务状态、产物和结果展示 | P3；模型服务/费用与能力 |
| R17 | 未实现专用链路 | 语音输入、实时会话/输出、可关闭输入预测 | P3；音频设备和服务接口 |
| R18 | 部分任务基础，专用链路待实现/验证 | 系统通知、持续目标、预算、检查点及后台生命周期 | P3；后台宿主需单独设计 |
| R19 | 部分预览基础 | PDF/Office/交互可视化的可信预览、生成文件保存/导出 | P3；R02/R05 |
| R20 | 部分浏览器基础；兼容范围待核实 | Appshots 语义、浏览器扩展协议、配对和跨进程接管 | P3；R07/公开协议 |
| R21 | 未实现；外部依赖待核实 | 宠物交互、Codex Micro 外设与 Windows 支持 | P3；协议和真实硬件 |
| R22 | 未实现接入或未验证 | Sites/dots/Space、ChatGPT 登录、远程/云/移动与跨端同步 | P3；官方接口/账号计划/范围决策 |
| R23 | 部分完成、实机待验证且有已知问题 | 原生 restore 裁切、DPI/焦点/键盘/主题及所有关键工作流 | 各阶段持续；最终总验收 |
| R24 | 配置已有；当前包未验收 | 固定提交的完整回归、JIT 单文件 Release 干净机和迁移验收 | P4；核心阻断与约定范围关闭 |

## 5. 后续实现的共同约束

1. Domain 只放领域值；Application/Tools.Abstractions 定义用例和端口；Agent 依赖抽象。
   Windows 句柄、进程、ConPTY 和设备放在 Platform.Windows；Providers/Browser/MCP/
   Infrastructure 各自实现端口；App 仅组合、状态协调和共享控件，不直接访问 SQL、文件、进程或 HTTP。
2. 所有工具继续经过统一执行管线，包括后台辅助入口和子代理工具；能力校验、审批、
   取消/超时、输出/产物、审计和任务归属不能绕过。新增扩展不获得隐式授权。
3. 数据库在当前 schema 28 之后使用向前迁移。每个切片实施时分配实际版本号，保存
   revision/所有权/未知状态，测试旧库保留；不预先假定每项计划只需一次迁移。
4. 每任务独立拥有取消、事件、资源和浏览器会话；取消必须收尾，无法确认时显示未知。
   重启恢复状态和可查看结果，不自动重放命令、文件变更、点击、发布、收费请求或远端写入。
5. 大产物落任务文件存储，SQLite 只放元数据、hash、大小与有界预览。当前密钥存储遵循
   已明确的产品决策；日志、导出、错误和本文件均不包含密钥、认证头、代理凭据或 Cookie。
6. 单手写 C# 文件一个类型，文件/方法不超过 500/80 行；继续共享控件、集中样式和本地化。
   当前 `WorkspaceCatalogViewModel.cs` 已到 500 行，后续行为应拆到独立职责服务，不能继续堆入。
7. 每个交付切片覆盖成功、拒绝/撤权、失败、取消/超时、并发及恢复；副作用增加“已生效但
   结果尚未返回”的断点。先确定性测试和真实临时资源，再独立原生实例和获准真实服务。
   相关测试与完整解决方案构建通过后，按阶段做完整回归，不重复累加重叠测试数量。

## 6. 未完成功能的详细实现方案

### R01：Windows 命令、终端及其他代码执行隔离

**现状：** 生产链路已复现普通命令读取授权根外标记。用户选定原生 MXC，支持它的独立
Windows 验收环境尚未就绪；本机已有诊断只获得较弱 DACL 层，不能作为通过结果。

1. 在 Tools.Abstractions 定义不可变执行范围：task/run、授权根及访问模式、只读运行时
   依赖、私有 temp/cache、网络策略和后端身份。Platform.Windows 实现适配器，统一供 shell、
   PTY、Git hooks/helpers 和 stdio MCP 使用；不得只修一条命令入口。
2. 环境到位后先做实际 SDK/tier 探针和 pwsh7/.NET SDK/Git/ConPTY 正常用例；按**每次启动**
   实际授予的隔离层验证，拒绝 AppContainerDacl 弱回退，不能只依赖 `IsSupported`。
3. 将启动、能力变更、任务进程树、继承句柄和 PTY 桥接纳入同一所有权交接；失败禁止
   退回普通宿主进程。若某 PTY 桥接要求超出网络策略，应拒绝该组合并设计合格桥接。
4. 用限时 drain 等待子进程及晚启动资源结束。SDK 的停止等待不能直接当作进程已终止；
   后端失效显示可操作错误。必要的系统预置另做可审阅安装/撤销方案，不修改全盘或 KnownDlls ACL。

**验收：** 根内命令/SDK/Git/PTY可用；绝对/相对路径、链接、子进程和继承句柄的根外访问
均拒绝且外部标记不变；多根、多任务互不越权。覆盖启动失败、后端消失、撤权、取消/超时和崩溃。
**依赖：** 可访问的受支持 MXC 验收机。本方案不复用已消费的一次性管理员授权，也不自动改换后端。
参考：[隔离方案](archive/SECURITY.md#command-filesystem-isolation-plan)、[后端决策](archive/SECURITY.md#windows-isolation-backend-decision)。

### R02：最终路径边界、严格撤权、产物与恢复

**现状：** 读取/写入/删除/复制/同卷移动及产物会话、授权版本和对象 grant 已实现，
但它们不是所有文件入口、整棵子树不变、最终名称条件提交或同步撤销屏障的证明。

当前开发分支增量：本机授权监视器按项目记录单调递增的已知授权版本；旧版本租约或同版本
已撤销租约未成功释放前，拒绝新操作准入。读库发现新版本与本机变更通知共用撤销入口，
迟到的旧读取/注册不能重新激活旧授权。根身份句柄、工作树环境租约及 ConPTY 清理成功后
才释放项目租约；清理失败保留本机准入阻断。该机制不是跨进程排空协议，也不证明所有
浏览器/MCP/产物资源已纳入登记；崩溃后的持久化未知状态及 OS 文件系统隔离仍未完成。

当前开发分支增量：schema 37 `artifact_session_records` 期刊已落地——任务产物会话与
Git diff 临时输入会话在创建即登记（记录失败则拒绝创建并回滚文件），完成/保留/丢弃/
清理未知分级推进；清理失败先记 CleanupUnknown 再抛出原有错误码。schema 38 为
ExternalSdkTemp 放宽 kind 约束：Playwright 下载在流出前登记 SDK 暂存路径（记录失败
同样拒绝传输并取消+删除下载），解析到的暂存文件超过 32 MiB 即取消并拒绝流出；SDK
取消+删除成功后记 Discarded，清理失败先记 CleanupUnknown 再抛原错。启动对账只处理
陈旧非终态记录（默认宽限 5 分钟），按任务产物目录与文件名复核归属：文件缺失归
Discarded、存在归 Orphaned 并按原阶段记 staged/completed/cleanup_unknown 子码，路径
复核未通过记 unverified；SDK 暂存记录不落在任务产物目录内属合法，存在即记 sdk_temp；
对账从不删除文件、不自动确认。编辑器区新增产物恢复面板（与 worktree 恢复面板同构），
按当前项目列出 Orphaned 记录并提供条件化“标记已核实”（并发变化给出 stale 提示并
刷新）。schema 33 附件快照引用回收已实现，R05 的引用核查经 AttachmentSnapshotReferences
覆盖草稿/消息/Queue/Steer 四类引用。浏览器下载配额已补两层：下载事件于下载开始即
返回 IDownload，配额观察循环按 250ms 轮询 SDK 暂存路径，尺寸超限即取消并确定性
转为 browser_artifact_too_large（取消与普通中止不可区分时以观察器标记为准），且
产物流出改用 64 KiB 分块有界复制，写入严格不超过 32 MiB——后者在 SDK 不提供
暂存路径时仍是硬上限。Playwright 的 PathAsync 在本版本下载完成后才解析，中途轮询
仅在 SDK/版本提前暴露路径时生效；字节级实时网络配额 API 仍不存在，跨进程排空也
未覆盖，R02 保持部分完成。

当前开发分支增量：本进程撤权排空状态已可观察。授权监视器按项目暴露
Idle/Draining/CleanupUnknown 三态（任一租约根会话或清理步骤释放失败且租约仍登记
即 CleanupUnknown，存在已撤销未释放租约即 Draining），状态变化经 DrainStateChanged
在同步锁外推送，观察器异常被隔离、不影响清理或准入。租约根身份句柄等释放失败时
标记 ReleaseFailed 并保留准入阻断（fail-closed 语义不变）。侧栏项目行据此显示
小字徽标，把"授权已保存"与"资源是否已退出"分层呈现：撤销中显示"授权已更新，
相关资源仍在退出"，清理失败显示"运行资源退出状态未知，重启后请核实"。排空已补
30 秒累计限时（DrainTimeout，注入 TimeProvider 可测）：撤销回调挂起或根会话等
同步释放阻塞超过限时即置 ReleaseFailed 并按 CleanupUnknown 呈现、准入阻断保留，
超时对调用方如实抛 TimeoutException。根会话释放另带一次有界自动恢复：首次
Dispose 失败（非超时）先经 2 秒稳定延迟后在共享 deadline 内重试一次——句柄尚未
关完/I/O 抖动类瞬时失败自愈为正常排空，持续失败仍标记 ReleaseFailed 阻断准入
并提示重启核实（轮询/取消任务的确定性挂起不重试，重试挂起无意义）。

当前开发分支增量：跨实例排空聚合经共享 SQLite 的 `project_drain_states` 板落地
（schema 48）。每个进程的授权监视器持有随机实例 ID，在本机三态实际变化时才在
同步锁外排队发布（Idle 删除本行、Draining/CleanupUnknown 按 项目+实例 主键
upsert，发布串行排序避免失序覆盖，异常被隔离不影响撤权、准入或清理），实例
正常退出时尽力清除自身全部行。侧栏徽标改读聚合视图：本机状态与板上所有实例
行的最坏值取较差者（CleanupUnknown>Draining>Idle），超过 120 秒未更新的非
Idle 行按 CleanupUnknown 计入——所有者不可达即无法确认清理完成；板读取失败
回退本机状态。本机即时状态仍是本进程权威（发布为异步），聚合覆盖其他实例
含中途崩溃者。陈旧行不会被永远悬挂：行只追踪进程内排空屏障，进程一死屏障
即随 OS 释放，因此聚合读路径顺手删除本项目内超过宽限窗的行（ReapStaleAsync
提供全表回收），崩溃实例的排空随下一次观察解析而非永久显示未知；板本身不
推送事件，但徽标对仍为非 Idle 的项目行每约 30 秒做一次延迟再观察（每项目
至多一个在途、解析为 Idle 即停、随跟踪器释放取消），外部行变化无需本机
事件即收敛到徽标。该机制是尽力告示板而非共识协议——真实网络/缓存配额与
R01 依赖项仍未实现；持续型 CleanupUnknown 仍需人工核实，R02 保持部分完成。

1. Application 建立 task/run/授权代际的资源登记与状态：可接收新操作、撤销中、资源已排空、
   清理未知。撤权先关闭新入口，再取消 Agent/工具/PTY/Git/Browser/MCP，限时等待实际资源退出；
   授权已保存和资源已退出分开显示。跨实例聚合订阅/轮询，旧 context 不能借用新 grant。
   本机三态、项目行分层显示、30 秒排空限时与共享排空板的跨实例聚合已落地（见上段）；
   外部行变化经非 Idle 行约 30 秒延迟再观察收敛到徽标，陈旧板行由观察者回收；
   根会话释放失败经一次有界延迟重试自愈瞬时失败，持续型 CleanupUnknown 仍需人工核实。
   工具审批在途取消与租约撤销竞速时（变更通知订阅者可先于监视器撤销处理运行），
   已取消 token 在租约撤销 token 上等待至多 50 毫秒归因：窗内撤销落地即归
   workspace_authorization_changed，纯调用方取消仍归 tool_cancelled。
2. Platform.Windows 复用现有会话补齐最终目标比较与提交协议：写入/覆盖/移动在受控断点
   核对对象、目录项和内容版本。研究可证明的最终名称 CAS；不能先做字符串检查再无条件覆盖，
   不能宣称普通 rename API 已提供“仅当仍为旧对象”的原子语义。不支持时保守拒绝并保留原文件。
3. 按入口清单审计搜索/枚举、后台统计、快照/预览、浏览器产物、Git 元数据和 MCP/子代理辅助路径，
   检查审批前后与执行边界；将不可取消原生调用隔离到受管 worker，迟到资源必须被接收并释放。
4. 通过向前迁移记录产物 staging/完成/保留/清理未知阶段；启动只识别所属残留并展示恢复。
   浏览器下载的实际字节级网络配额（Playwright 暂无可限速 API，现有暂存轮询/有界复制
   只是缓存侧约束）仍需等待 SDK 能力或另择执行通道；Git 临时输入增加崩溃发布/回收记录。
   引用回收与 R05 合作，禁止按年龄或目录名直接删除可能仍被使用的文件。
5. 完善项目删除重建、同路径 ABA、目录 ID 重用/别名及不支持卷的策略。区分已验证的本地普通
   NTFS 与其他组合；无身份工厂的兼容宿主不能当作生产安全保证。用长数据库锁、多实例乱序通知和多任务负载验证开销。
6. 如增加跨卷移动，使用双端授权的受控复制、目标提交/hash确认和源对象条件删除，持久化
   各阶段并明确它不是原子重命名；目标未确认成功或状态未知时保留源，冲突/失败展示部分完成。
   完整元数据克隆与特殊卷支持分别定义能力，不静默降级成只复制内容。

**验收：** 在检查/审批/打开/提交/释放前后设置确定性 gate，替换 junction、父目录、文件和
Git 元数据，根外不读不写，外部并发修改不丢失。验证撤权后不再接收新操作、所属资源退出或显示未知，
另一任务不受影响；崩溃恢复不删除归属不明对象、不重放副作用。已有副作用不承诺自动回滚。
参考：[读取](archive/SECURITY.md#read-handle-path-safety-acceptance)、[写入](archive/SECURITY.md#write-handle-path-safety-acceptance)、
[产物会话](archive/SECURITY.md#owned-artifact-session-acceptance)、[项目根对象](archive/SECURITY.md#root-object-authorization-acceptance)。

### R03：附件 Steer、发送偏好和 Queue/Steer 竞争

**现状：** Queue 与文本 Steer 已贯通；附件引导和可保存的默认/替代发送方式已在开发分支落地，
事务中止、分派/终态与引导竞争和原生输入组合尚未全部验证。

当前开发分支增量：运行中 Enter 的默认行为可在「设置 → 对话与发送」选择入队或引导，
Ctrl+Enter 使用另一种行为，Shift+Enter 始终换行；偏好经独立 JSON 原子保存，读取失败时
回退默认并提示，键盘与共享按钮仍共用同一命令与可用性判断。

附件引导增量（schema 39）：`conversation_steering_input_attachments` 只存元数据（display path、
byte count、sha256、media type、attachment id），字节继续放在 draft-attachments 快照；接收时在同一
SQLite 事务内转移附件元数据、写入 receipt 并消费草稿，重复 receipt 按附件标识幂等返回且不再消费新草稿。
应用时 `conversation_attachments` 行与用户消息同事务写入；Agent 边界经 `IConversationAttachmentStore`
读取快照（draft 目录兜底），文本/PDF 进入文档上下文，图像转为原生模型图像。图像能力（模型+协议）、
每轮 10 张/10 MiB 与上下文预算在应用前失败关闭；引导仍要求非空文字，附件单独不构成输入。
引导记录显示本地化附件数标记；附件引导的中止回滚（receipt 与附件元数据行随事务回滚、
草稿及其附件行保持不变、遗留快照按无引用候选安全回收）、重启中断恢复读取（中断输入
不自动重放、附件行保留供审计并按原元数据还原）、Steer 与入队对同一草稿消费竞争的
恰好一次去重已覆盖；陈旧快照重排/移除/编辑对外部版本提升（另一窗口或实例写入、派发认领出队）经 revision CAS
报告冲突而不覆盖、不复活已认领首项、保留编辑中文字的 VM 端到端用例已补齐；完整宿主终止矩阵和原生 IME 验收仍缺，
R03 保持部分完成。

1. Domain/Application 将附件引导表示为任务所属不可变快照引用；请求前验证 MIME、模型能力、
   hash 和整轮预算，再将 receipt 接收与草稿消费原子提交。拒绝时保留草稿，不能扩大活动任务的原权限。
2. App 使用现有共享输入组件增加本地化发送方式设置和明确的入队/引导状态，持久保存偏好。
   Enter、Ctrl+Enter、鼠标共用相同命令和可用性判断；多行输入、IME 组合和独立停止按钮保持可用。
3. 用 run ID、草稿/队列 revision 和 receipt ID 保护编辑/排序/认领/分派/完成/关闭竞争；
   最后一轮终态与新引导同时发生时，明确接收、拒绝或保留草稿，不能丢失或重复消费。
4. 在 SQLite 事务前/后和工具生效前/后增加受控断点，真实终止宿主后重建服务验证；
   重启仅显示待手动继续和已接收/已应用/中断状态，不重收草稿或重放工具。

**验收：** 项目/无项目、附件能力不足、队列重排与模型/授权变化、取消/超时、并发对话、
已提交副作用后崩溃全部有记录；另做独立原生键盘/IME和真实提供商测试。旧不明原因失败继续保留，
不能以新用例通过推断其根因。参考：[Queue](archive/CONVERSATIONS.md#conversation-followup-runtime-acceptance)、[Steer](archive/CONVERSATIONS.md#conversation-steering-acceptance)。

### R04：工作树创建、清理和完整生命周期

**现状：** 工作树对象 grant 与旧任务确认已完成。基准选择器已并入本地分支/标签发现：
受管 runner 列本地 refs、按名称搜索/分页并预览固定 commit，须授权完整仓库，窗口有界且
截断明示；远端获取为用户显式按钮动作，非自动网络行为；手工输入仍独立保留。创建已改为 Windows 受控目录会话
独占创建空目标（NtCreateFile FILE_CREATE，不接受已存在对象）并持有其卷/文件身份到
Git 与登记结束，登记前重开名称核对身份；预创建空目录对 `git worktree add` 的兼容性与
失败语义已用真实 Git 验证。登记前身份核对拒绝被替换的对象并保留目录。schema 29 新增
`worktree_creation_intents`：创建在任何目录变更前持久化 task/project/repository/基准与
目标及 claim 身份，授权修订不匹配或任务已落库则拒绝；首个任务保存与消息写入同事务
按 intent 原子认领，无对应 intent 的陌生工作树任务被拒，既有任务通过迁移回填保持可写。
schema 30 为 intent 增加生命周期阶段（Recorded/TargetClaimed/GitCreated/Failed/Consumed/
Orphaned/Acknowledged）、错误码与更新时间：claim 身份、Git 完成核验各推进一阶，创建
失败尽力标记 Failed+错误码，任务落库认领置 Consumed；`IWorktreeCreationIntents` 端口
支持读取未完成/确认，启动时尽力对账——有任务行置 Consumed、无任务行置 Orphaned，
不自动重建或删除；孤儿判定要求 intent 超过 5 分钟无更新，避免并发实例对账终止
在途创建；对账失败走独立启动警告而非 MCP 设置通道。孤儿 intent 经 `error_code`
分类标注：missing（目标已消失）/untracked（无 `.git` 指针）/pointer（gitdir 指向
其他 worktree id）/unregistered（注册槽目录缺失）/foreign（common-dir 指向其他
仓库）/unlinked（槽内 gitdir 不回指目标）/retained（指针+common-dir+注册槽核验
一致）/unverified（复核异常），供恢复列表区分处置。启动恢复展示面板已落地：输入区按当前项目列出 Failed/Orphaned intent，按 error_code 本地化说明原因并展示目标路径与基准，用户核对后可显式“标记已核实”（置 Acknowledged，条件更新，状态已变则提示并重读）；面板不删除目录、不清理 Git 注册、不重建也不续跑任务。schema 31 新增
`worktree_cleanup_intents` 清理侧阶段（Recorded/GitRemoved/Detached/Failed/Orphaned/
Acknowledged）：`git worktree remove` 前按授权修订 CAS 记录 intent，任务已摘挂或另有
在途清理则拒绝、Failed/Orphaned 后允许重试重置；Git 删除成功推进 GitRemoved，任务
保存清除 worktree_path 在同一事务推进 Detached，删除失败尽力标记 Failed+错误码。
启动对账：任务行已摘挂置 Detached，GitRemoved 且任务行缺失（创建回滚路径）同样判
完成，其余陈旧在途置 Orphaned 并按目标是否存在标注 removed/retained/unverified；
恢复面板同时列出清理 intent（清理失败/清理中断）并沿用显式“标记已核实”；对
Failed/Orphaned 且目标仍存在（非 removed）的清理 intent，面板另提供“重试安全
移除”，完整复用 `RemoveAsync` 的授权范围/受管路径/任务 ID/授权 grant/源仓库校验与
intent 记录链路，成功推进 GitRemoved 并刷新列表，授权过期提示陈旧并重读，其它失败
给不含细节的可操作提示。最终删除窗口在 Windows 已闭合：`IWorktreeRemovalGuard`
端口由 `WindowsWorktreeRemovalGuard` 实现——释放钉住租约后以 FILE_SHARE_DELETE 按
相对名重开目标目录并核对卷/文件身份与登记 grant 一致才放行 `git worktree remove`；
目标被替换则守卫开启失败，git 不执行；Git 成功后用 FILE_STANDARD_INFO.DeletePending
确认被删的正是登记对象，若替身被删而登记对象存活则记 Failed 不误标 GitRemoved
（真实 NTFS 测试覆盖改名替换攻击）。worktree 注册槽级元数据清理已落地：
`GitWorktreeRegistrationSlot` 在仓库公共目录 worktrees 下定位 gitdir 回指目标的
唯一注册槽（零或多个匹配均拒绝），删除前再做保守内容检查（须为直系子目录、拒绝
链接、限制条目数量与深度）；`IGitWorktreeService.RemoveStaleRegistrationAsync`
仅删除该已证实单槽（不做全局 prune），目标仍存在或位于项目主仓库内时拒绝，行为经
`WorktreeStaleRegistrationTests` 覆盖；恢复面板对 Orphaned 且目标已消失（removed）
的清理 intent 提供显式“清理注册项”操作，不推进 intent 阶段、失败可重试。
清理前新运行准入已由清理 intent 联锁关闭：阶段 Recorded/GitRemoved/Failed/Orphaned
视为在途，`AcquireAsync`/`ValidateAsync`/`ConfirmLegacyAsync` 经持久化活性检查
拒绝准入，`Detached`/`Acknowledged` 终态放行；进程内以每任务 drain 门把活性检查
与 intent 记录/git 删除串行化，消除检查-记录交错窗口。跨进程仍以持久 intent 行与
启动对账兜底；调用方"任务未在运行"先验检查与准入本身的交错不在本层闭合。
替换对象重新授权已落地：`ProbeAuthorizationAsync` 尽力分类 Unconfirmed/Enrolled/
Replaced/Unverifiable（探测失败不冒称已授权）；`ReauthorizeReplacedAsync` 要求任务
非运行态、无在途清理 intent，重开根身份会话并要求对象确实已变（SameObject 命中即
拒），`ValidateTargetAsync` 证明替身仍为本仓库注册工作树，`ReplaceAsync` 以旧身份+
项目修订+任务快照 CAS 重绑新 grant——并发重授权或任务编辑即失；授权面板对替换态
显示独立状态与"重新授权"操作。无 `IWorktreeRemovalGuardFactory` 注册的平台（当前
仅 Windows 提供实现）最终删除窗口竞态仍存在。

1. 已落地：受管 runner 的本地分支/标签发现、搜索/分页和固定 commit 预览已并入基准
   选择器，自动发现与手工输入分别保留。远端获取已做成单独显式动作：基准选择器内的
“获取远端更新”按钮经 `IGitWorktreeReferenceDiscovery.FetchAsync` 执行有界
`git fetch --all --tags`（受管 runner 30 秒超时，剥离 GIT_* 环境），不改工作区、
不做凭据管理；refs/remotes 发现项标注“远端”并过滤符号 HEAD，选定后由既有
rev-parse 链路固定 commit。网络失败给本地化提示，不静默退化、不自动重试。
2. Platform.Windows 已落地独占创建空目标并持有身份的会话，预创建空目录与真实 Git
   `worktree add` 的兼容性已验证；未采用接受已有目标的 `CreateDirectory`。Infrastructure
   在目录变更前持久化 task/repository/base commit/target/identity intent（schema 29，
   授权修订条件插入、既有工作树任务回填为已认领），成功后首个任务/消息保存同事务
   条件认领；无 intent 的任务保存被拒绝。
3. 按保存 grant 的受控删除会话已落地（Windows 删除共享守卫 + DeletePending 复核，
   见上）；恢复面板重试移除已落地；清理前新运行准入联锁已落地（在途 intent 阶段
   拒绝 Acquire/Validate/ConfirmLegacy，进程内 drain 门串行化，见上）。R02 的撤权
   drain 仍未实现。研究仅处理该 worktree 注册槽的 Git 元数据清理，避免对象删除后
   Git 再按同名路径删除替换目录，不采用全局 prune。
4. 创建侧阶段已落地（schema 30：阶段列+错误码，claim/Git/失败/认领逐阶持久化，启动
   对账标记 Orphaned/Consumed，`IWorktreeCreationIntents` 读口与确认）；对账复核已
   解析 `.git` 指针并核对注册槽 commondir 归属本仓库与 gitdir 回指目标。启动恢复展示
   面板已落地（`WorkspaceWorktreeRecoveryViewModel`）。清理侧阶段已落地（schema 31
   `worktree_cleanup_intents`：`IWorktreeCleanupIntents` 端口、记录/推进/失败标记、
   摘挂同事务完成、启动对账与 orphan 标注、面板同源展示与确认、保留对象的重试
   安全移除）；逐句柄最终删除已在 Windows 以守卫闭合；注册槽级元数据清理已落地
   （保守槽核验与单槽删除，恢复面板显式操作，见上）；清理前的 drain、替换对象重新授权、
   非 Windows 守卫实现与完整崩溃组合矩阵仍缺。
5. 对已登记但被替换的对象，另设计显式重新授权代际和 CAS：完整展示任务/项目/路径，
   提交新 grant 时撤销旧租约，旧 context 不得复用新版本；确认后仍需手动续跑。

**验收：** 创建空目标、Git 退出、登记、消息提交、对象删除、注册清理等断点分别终止恢复；
覆盖并行创建一成功一失败/取消、基准移动、被占目录、陈旧确认及清理时同路径替换，保留另一任务和草稿。
另验独立 EXE 重启和旧工作树确认。依赖 R01/R02，详细研究边界见[工作树授权方案](archive/GIT_WORKTREES.md#worktree-object-authorization-acceptance)。

### R05：PDF、其他附件、全图查看和快照回收

**现状：** 文本和静态图像链路已有实现。子代理图片共享已落地：`ShareContext` 开启时，
共享窗口内图像按新到旧、≤10 张/≤10MiB 选取并以 Base64 内联进子请求用户消息；
共享关闭时子请求不携带图片，子模型或协议不支持图像时在发起任何模型调用前以
`subagent_images_unsupported` 明确失败，不静默丢弃也不换用父模型。
PDF/其他原生文件、全图查看、任务归属快照引用传递、按子模型预算选取和安全回收尚缺。

1. Domain/Application 定义格式、MIME/真实内容、来源、大小/页数/像素/解压展开量限额。
   Providers 为每协议明确原生文件块支持；不支持时仅允许用户明确选择有界文字提取，UI 说明语义差异。
2. Infrastructure 将解析和快照写入放在后台可取消操作；拒绝脚本、损坏和伪装文件。
   App 复用共享预览容器增加全图适应/缩放、PDF 分页、加载/错误/禁用状态和有界解码缓存；历史只读快照。
   已落地：消息附件芯片点击打开共享预览面板——图像解码为可缩放位图、PDF 显示提取文本
   与页数统计、其他类型显示 UTF-8 文本；读取前经 `ConversationAttachmentValidation`
   校验并只经 `IConversationAttachmentStore` 读取任务归属快照，禁止写入与文件管理器
   揭示，标签页按快照 id 去重且刷新重新读取；快照缺失/篡改显示本地化的不可用状态，
   无身份的可选附件退回渲染后的缩略图。仍缺：原生文件类型的更多预览形态与有界解码缓存。
3. 已落地：图像共享复用 `ShareContext` 开关，分别验证共享开关与子模型×协议联合能力；
   不共享时请求中不得包含图片，能力不足（`SupportsImages`/`AcceptsImages` 任一不满足）
   明确失败且子模型零调用，不偷偷换用父模型。仍缺：任务归属的只读快照引用
   （当前以 Base64 内联传递，不走附件快照）、按子模型上下文预算选取。
4. 用数据库引用集合、活动租约、宽限期及删除前复核回收快照，覆盖草稿/消息/Queue/Steer/
   子代理/正在解析和导出的引用；按原句柄删除，不按目录年龄清空。恢复扫描与 R02 的产物阶段记录共用。

**验收：** 损坏、伪装、超限、压缩炸弹、缺失/篡改、源文件变化、无项目明确选择、共享关闭、
并发回收和重启均覆盖；另验证原生选择器/预览和真实模型图片/PDF理解。请求块形状正确不等于理解成功。
依赖 R02/R06/R09；参考：[图像附件](archive/CONVERSATIONS.md#image-attachment-workflow-acceptance)、[无项目附件](archive/CONVERSATIONS.md#projectless-attachments-acceptance)。

### R06：提供商、模型参数、代理和断流恢复

**现状：** 四协议和设置到运行已通，仍需补齐所有终态变体、真实服务能力及配置竞争矩阵。

1. 流终态夹具已逐协议补齐（四协议×空流/failed/incomplete/缺失终态 EOF/工具片段缺失/
   重复终态/取消矩阵审计后补缺）；并修复五个真实解析缺陷：OpenAI Responses 收到
   `[DONE]` 无终态事件时静默"成功"、Responses/Anthropic 畸形 JSON 帧泄漏原始异常、
   OpenAI 事件映射对缺失 name/call_id/参数静默丢弃或伪造 call_id、Anthropic
   `message_stop` 静默丢弃未闭合 tool_use、`message_delta` 忽略 max_tokens/refusal；
   四协议失败模式各验证请求次数恰为 1（无隐藏重发），`ModelFailed`→`AgentFailureEvent`
   为终态、不推动 Queue 自动续发。
2. 已落地：逐模型能力覆盖与协议支持共同用于输入校验（`ModelCapabilityGate` +
   `ModelProtocolCapabilities`）；图像仅当模型与协议同时支持才发送，不支持的思考强度和
   非法高级参数在请求离开进程前即失败并给出修复说明；未知模型 fail-closed，模型发现
   不覆盖用户编辑值；能力违例映射为独立失败原因（18/19，schema v35 扩宽 CHECK），
   不再重复发出泛化 `run_failed` 事件。PDF 仍以提取文本形式发送（R05 语义）。
   待验收：真实协议端点上核对各模型能力声明与实际行为矩阵。
3. 已落地：请求开始时的提供商/代理配置快照——`ProviderHttpClientFactory.CreateAsync` 每次
   请求单次查询代理行构造独立 handler，禁用/删除即 `ProviderProxyUnavailableException`
   拒绝且不回落直连；测试覆盖默认代理、显式直连、带凭据代理、禁用/删除、同时编辑和
   请求（并发编辑竞态断言每请求恰得一份完整快照或 fail-closed，无撕裂混合）。失败不
   自动直连，不关闭全局 TLS；重定向不跟随也不转发凭据。
4. 已落地：失败按进度分级（未提交/可能已提交计费/已执行工具/未知），由持久化历史与
   类型化失败原因推导、无需 schema；失败通知给出显式「重试」入口——未提交直接重发原文，
   其余状态先经内联确认（区分计费/副作用/未知文案）；重试走正常发送路径、按发送时配置
   版本建立新运行，绝不自动重发。

**验收：** 先用确定性 HTTP/SSE/认证代理验证参数、能力、限流、证书、超时、断流和并发；
再在获准账号上分别验证四协议、模型发现、图片/PDF、思考强度和续轮。只记录脱敏摘要，明确未支持模型。
参考：[模型能力](archive/PROVIDERS.md#model-reasoning-capability-acceptance)、[终态](archive/PROVIDERS.md#provider-stream-completion-acceptance)、[代理](archive/PROVIDERS.md#authenticated-provider-proxy-acceptance)。

### R07：浏览器模式切换、人工接管和其他崩溃时序

**现状：** 已有任务浏览器与取消修复；完整模式切换、接管排他性和全部创建/副作用时序未验收。

当前开发分支增量：每任务浏览器控制状态机（自动执行/交给用户/交回中/已断开）已落地，
由 `ITaskBrowserSessionManager` 实现的 `IBrowserTakeoverController` 统一管理；任务会话经
`TakeoverBrowserSession` 装饰，非自动状态下包括读取在内的全部自动化操作以
`browser_takeover_active`/`browser_session_disconnected` 快速拒绝，接管即取消在途自动化
调用；交回时对内层会话做状态重读（ListTabs+CurrentUri），重读失败回到交给用户、对象已
销毁置已断开，调用方取消也完整回落而不卡在交回中；敏感 profile（日常 profile 与既有
CDP 连接）在启动前经独立显式审批，批准按任务会话条目缓存、拒绝不缓存。检查器浏览器区
新增接管栏显示状态与交回操作（仅在有会话或非自动状态时出现，覆盖非内嵌会话），日常
profile/CDP 审批为独立确认浮层；`ToolExecutor` 映射三个新错误码，工具栏状态同步本地化。
当前开发分支增量：schema 43 `browser_session_records`/`browser_action_intents` 期刊已
落地——会话在工厂创建成功后即登记（engine/profile_mode/state，不落 profile 路径、CDP
端点或 URL；登记失败即弃置新会话并拒绝创建）；十类变更操作（导航/前进/后退/刷新/
新建/关闭标签页/点击/输入/选择/下载）在内层调用前以 committed 落账，成功/取消/失败
分别记 resolved/cancelled/failed，登记失败则不发出副作用；只读调用不入账。目标仅写
16 位 SHA-256 前缀（选择器按原文、导航按 scheme://host[:port] 归一化），输入文本、完整
URL 与原始选择器绝不落库。启动对账仅把残留 attached 会话与 committed intent 置
interrupted，审计面板新增“浏览器会话”区投影会话终态与操作明细并给出待决警示，绝不
自动重放。模式切换时序与原生窗口验收仍缺，R07 保持部分完成。

1. Browser 按 session/context/page/profile 分离所有权，App 显示任务、所选模式和持有/借用关系。
   内嵌、受管外部和显式 CDP 切换前先停止旧动作、等待清理，再绑定新代际；不扫描任意调试端口。
2. Application/Browser 建立“自动执行、交给用户、等待显式交回、已断开”状态及排他 gate。
   用户接管时不再派发自动点击，交回后重新读取页面状态；敏感日常 profile 访问独立审批。
3. 将现有取消/drain 延伸到创建 profile、launch/context、标签页、下载和切换的其他时序。
   只关闭本任务拥有的资源，不能关闭借用用户页面或另一实例；借用 context 的 cookie/storage 共享应明确展示。
4. 已落地：持久化会话标识（engine/profile_mode/状态）与十类变更操作的未知结果，
   启动仅对账为 interrupted 并交由人工核实，不重新执行；下载未知清理状态接 R02 的
   配额和恢复方案。

**验收：** 本地确定性页面记录副作用次数，覆盖内外/CDP切换、创建取消、接管时停止、标签关闭、
host/browser 崩溃和多任务；另一会话与草稿不受影响。独立原生 EXE 验证真实窗口、接管栏和恢复。
参考：[取消修复](archive/BROWSER_MCP.md#browser-operation-cancellation-acceptance)、[副作用恢复](archive/BROWSER_MCP.md#browser-committed-effect-recovery-acceptance)。

### R08：MCP 认证、配置版本和连接恢复

**现状：** stdio/HTTP、发现、执行和显式重连已实现，不能视为自动续传或完整认证恢复。

1. 已落地：配置持久化带 revision（schema v36），保存为 compare-and-swap，陈旧编辑获
   类型化冲突并保留草稿；日志不含 headers/凭据。
2. 已落地：工具注册绑定单一客户端代际，重连期间先摘除工具再排干——在途调用在其
   准入代际上完成，排干后到达的调用返回显式「重新连接中」结果，绝不重定向到新 schema。
   当前开发分支增量：401/403（含 WWW-Authenticate scheme）、已分配会话的 404 及 stdio
   进程退出按类型映射为可操作建议（设置页 Errors 与工具结果 mcp_authentication_failed/
   mcp_session_invalid），均为分类提示，不自动重试或续传。
   待补：OAuth 挑战流程未实现；会话失效后自动恢复；断流/并发取消闭环；stdio 启动
   所有权仍属 R01。
3. 明确共享服务进程与每任务调用的取消归属；stdio 启动服从 R01。单任务取消不能杀掉另一任务的
   共享服务，传输/进程无法确认结束时保留未知状态，关闭时抑制迟到注册与 UI 结果。
4. 所有发现工具保留来源、代际和信任范围，沿既有 adapter 进入统一执行管线；
   只有经可信配置验证的只读信息可影响风险判定，名称和未验证 annotation 不授予免审批。

**验收：** 可控 stdio/HTTP 服务覆盖认证刷新、配置竞争、工具清单变化、输出过大、断流、
拒绝、取消与重连交错、多任务调用及重启；独立原生记录“配置→发现→审批执行→失败→人工恢复”。
参考：[MCP 执行](archive/BROWSER_MCP.md#mcp-execution-acceptance)、[重连](archive/BROWSER_MCP.md#mcp-reconnect-recovery-acceptance)、[UI 恢复](archive/BROWSER_MCP.md#mcp-ui-recovery-acceptance)。

### R09：子代理触发、共享及完整生命周期

**现状：** 保存配置到子模型/工具/父对话已有链路；父子 run 期刊已落地（subagent_runs v41）。
触发条件目前是交给主模型选择的指导，不能宣传为确定规则匹配；
当前版本完整审批、并发、失败和恢复组合仍待验。

1. Agent/Application 复用 `SubagentTool` 和 gateway 的持久化部分已完成：`subagent_runs`
   表（schema v41）按 run_id 记录 task_id 父子关联、parent_call_id、profile 名与有界
   snapshot_json（选定 provider/model/思考强度/共享开关/任务预览），running→completed/
   failed/cancelled/interrupted 五态投影幂等迁移；启动核对仅把遗留 running 标为
   interrupted，绝不重放。UI 历史投影已落地：审计面板按任务列出子代理运行（五态映射
   shared 终态色阶），选中行展示防御性解析的 snapshot 摘要，畸形快照回退 profile 名
   +状态，期刊读取失败不影响执行记录主路径。仍未完成：用可控模型真实选择
   `subagent.run` 验证触发、禁用模型明确失败的验收。
2. 明确文本共享与 R05 图像共享边界，按子模型预算构造请求；子工具权限仅可继承并收窄，
   重新走审批，审计关联父子身份。需要确定规则触发时另设计显式策略，不能暗改现有模型委托语义。
3. 父取消等待子模型和子工具退出；导航或关闭后迟到进度不得写进另一任务。
   历史分别显示运行、失败、取消、中断终态：审计面板子代理运行区已落地（running/
   failed/cancelled/interrupted 可查看终态，含快照详情与本地化提示），未知工具结果
   仍走执行记录的 pending/中断呈现；重试创建新 run，不重放旧工具。
4. 通过保存后的配置验证名称/触发指导/提示词/非默认模型/自定义强度/共享开关进入实际请求，
   然后执行真实临时文件工具并返回父对话，不能只检查配置数据库。

**验收：** 允许/拒绝审批、能力不足、模型禁用、错误/超时、父子独立取消、两个委托并发、
授权变更、EXE 重启及共享开/关都保留可查看终态；分别记录模拟模型、真实提供商及原生窗口证据。
参考：[设置到运行](archive/AGENTS_AUTOMATIONS.md#subagent-settings-to-runtime-acceptance)、[手动审批](archive/AGENTS_AUTOMATIONS.md#subagent-manual-approval-acceptance)。

### R10：定时任务的关闭、并发、时间和恢复

**现状：** 创建/编辑/启停/手动运行/停止/结果已通；应用关闭后不是持续后台执行服务。
启动会跳过过期触发并标记遗留中断，原生关闭及全部时间组合尚未完成验收。
当前 `AutomationSchedule` 保存项目/提供商/模型 ID、提示词和审批策略，并在保存时捕获
`AutomationAuthorizationSnapshot`，固定项目授权 revision、目录授权集合、提供商/模型
标识与显示名、审批策略和提示词；`AutomationAuthorizationGate` 在每次认领时对照 live
状态评审后才交给 `WorkspaceAutomationRunner`/`WorkspaceBackgroundRunCoordinator` 执行。
思考强度等模型运行参数仍取模型当前配置，不属于快照固定范围。

1. Application 的调度器和 Infrastructure 仓库继续使用事务认领及实例租约，明确计划 revision、
   触发时间/时区、错过时间策略和单次 run 所有权；活动运行固定启动快照，编辑仅影响后续触发。
   已落地：认领在同一事务内把计划授权快照代际写入 run 行（schema v42
   `automation_runs.snapshot_generation`，旧行保持 NULL 不回填），运行记录展示所用代际并对
   早于当前代际的行标注"已在旧授权快照下运行"；保存时若该计划在途运行，提示本次运行沿用
   认领时快照、变更从下次运行生效。编辑后自动停止在途运行仍非默认行为，实机验收仍缺。
2. 增加计划级授权/参数版本记录：保存项目授权 revision、根 grant 集合、审批策略及明确的模型
   参数采用策略。默认授权变化阻止旧计划运行并提示重新确认；如支持跟随项目变更，应由计划级
   显式选项授予，不能隐式扩权。认领时事务复核，运行中变更继续撤销旧租约。
   已落地：计划 payload 新增序列化字段 `FollowAuthorizationChanges`（默认 false；旧载荷
   反序列化即钉住语义，无需 schema 迁移；编辑器提供显式勾选，列表行标注"跟随授权"）。
   关闭（默认）时认领评审照旧，通过后 gate 把快照目录集作为 `AutomationRunContext.
   AuthorizedRoots` 沿执行链钉入 `ToolExecutionContext`——收窄按构造成立，而非仅靠评审；
   开启时评审跳过 revision 与目录两项检查、运行沿用项目当前授权根，但快照缺失/不匹配、
   项目/提供商/模型缺失仍阻止。运行中的授权变更仍由 `ProjectAuthorizationMonitor`
   按旧租约撤销（扩缩一致处理）。实机验收仍缺。
3. 暂停仅影响未来计划，停止仅取消本次运行，分别展示；关闭先禁止新认领，再取消并等待已有
   runner 和工具资源，之后释放调度租约。未知副作用显示中断记录，不自动补跑。
4. 覆盖定时与“立即运行”碰撞、重复 tick、多实例、运行中编辑/删除、时钟前后跳和 DST。
   使用可控时钟验证时间算法；数据库认领失败不得产生模型请求或丢失前台输入。
5. 若需要关闭应用后继续执行，另建可选后台宿主及 IPC/单实例所有权、安装/更新/退出方案，
   与现有前台调度共享状态但不重复认领；该能力在单独设计与验收前保持未实现。

**验收：** 保存非默认模型/提示词/授权，实际请求和文件工具使用该快照；另一任务、当前选项及
未发送草稿保留。保存后模型参数/项目根变化覆盖拒绝或显式采用策略，不自动扩大旧计划范围。
独立原生正常关闭/强制终止/重启查看结果，零自动补跑；新手动运行使用新 run ID。
参考：[定时 UI](archive/AGENTS_AUTOMATIONS.md#automation-ui-workflow-acceptance)、[修改行为](archive/AGENTS_AUTOMATIONS.md#automation-mutation-acceptance)。

### R11：Git 区块、远端、凭据和提交草稿

**现状：** 文件级暂存及确认提交/推送可用；整组暂存/取消暂存已落地（git.stage_paths /
git.unstage_paths，1–64 个路径一次调用单命令原子生效、不隐式 stage-all，任一元素越界、
链接或目录即整组拒绝且零副作用）；单文件区块暂存/取消暂存已落地（git.stage_hunks /
git.unstage_hunks，经 stdin 补丁写入索引，rename/copy/二进制/新增/删除/仅模式/冲突差异明确拒绝）；
提交消息草稿经 git_commit_drafts（schema v40）独立版本持久化，编辑去抖写入、重启/切换任务后恢复、
提交成功或核对已落盘时消费；非确定性提交失败（超时/取消/执行错误）后复核 HEAD 与暂存数决定
消费或保留。预览绑定已落地：git.diff 输出尾部附 --- mossagent --- 绑定块（diff-sha256 =
完整差异文本哈希、file-sha256 = 各文件差异段落哈希），git.stage_hunks / git.unstage_hunks 以
required expectedDiff 携带模型所见哈希，应用前对实时捕获的差异复核，不一致即 git_diff_changed
拒绝且零副作用。远端/凭据管理的读取与展示侧已落地（git.remotes 只读工具与推送对话框凭据方式/内嵌凭据警示），set-url/上游条件变更端口仍缺。

1. ~~Tools.BuiltIn 为整组/区块暂存和取消暂存分别实现工具~~ 整组暂存/取消暂存
   （git.stage_paths / git.unstage_paths，1–64 路径单命令原子、越界/链接/目录逐项拒绝）
   与区块暂存/取消暂存工具均已落地；预览绑定已落地（git.diff 尾部 file-sha256 绑定、
   expectedDiff 必填执行期复核、漂移拒绝），审批后复核即执行期哈希复核；已支持选定范围
   而不隐式 stage-all；rename/copy/二进制/截断差异明确处理；HEAD/index 级复核与原生验收仍缺。
2. ~~Application/Infrastructure 提供远端和上游配置读取、预览与条件变更端口；App 展示脱敏
   endpoint、凭据方式和错误。~~ 读取+预览侧已落地：git.remotes 只读工具列出已配置远端、
   脱敏 scheme://host[:port] 端点、local/ssh/https/embedded/unsupported 凭据方式分类
   （embedded 揭示 URL 内嵌凭据风险但绝不回显 userinfo）、credential.helper 值链与
   当前分支上游；git.push_preview 携带凭据方式，推送对话框凭据行对内嵌凭据追加警示。
   条件变更端口已落地：git.remote_set_url 以 expectedFetch/expectedPush 声明最近一次
   git.remotes 的脱敏端点与凭据方式（endpoint 为 null 断言该侧无已配置 URL），执行期重读真实
   get-url 分类逐项比对、不符即 git_remote_changed 且不写；新 URL 复用同一形状分类，unsupported
   拒绝、embedded 允许、local 目标复用推送目的地授权端口；两侧顺序写并如实报告部分应用。
   git.branch_upstream 以可空 expectedRemote/expectedMergeRef 原文 CAS 后顺序写
   branch.<b>.remote/merge（游离 HEAD 必须显式指定已存在分支、remote 必须已配置、mergeRef
   限 refs/heads/）。真实远端核对部分落地：git.ls_remote 只读探测已配置远端
   （脱敏端点、有界 64 条、非交互、本地目标复用推送目的地授权、凭据永不回显），
   供模型主动核对；推送后自动核对仍为离线核对，不接入网络；原生验收仍缺。
   账号或系统凭据集成须明确选择，不扫描任意浏览器/CLI 凭据。
3. ~~提交消息用独立版本草稿持久化；失败、取消或断线后先核对实际 HEAD 再保留或消费草稿~~
   提交侧已落地（v40 表、CAS 写入、失败/超时后以 git.commit_preview 复核 HEAD 与暂存数：
   HEAD 变且暂存空→消费草稿，HEAD 同→保留，HEAD 变且仍有暂存→保留并提示外部变更，核对
   失败→保留并明示结果未知）。推送侧核对已落地：非确定性失败（超时/取消回包/执行错误）
   后以同一 operation token 再取 git.push_preview，比对 refs/remotes/<remote>/<target>
   与被推 HEAD（该引用由 git push 成功时本地刷新，不触网络）；一致→明示可能已推送并保持
   对话框，不一致或缺失→保留原错误呈现，核对调用失败→明示结果未知；确定性失败与取消
   路径不核对。非快进引导已落地：推送失败的合并诊断在工具内分类（non-fast-forward/
   stale info/fetch first），归为确定性 git_push_non_ff 跳过结果核对，呈现合并或变基远端
   更新后再刷新预览推送的下一步；原始输出可能含凭据不回显。真实远端核对部分落地：
   git.ls_remote 已落地只读有界探测（仅 refs/heads 与 refs/tags、脱敏端点、非交互、
   凭据永不回显），供模型主动核对远端实际 refs；推送失败后自动核对仍走本地远端
   跟踪引用（离线），不接入网络。未知提交/推送结果不自动重做。
4. 保留 hooks/signing/helper 的正常语义并接 R01 隔离，不为兼容测试禁用；提交和推送是独立
   明确动作，创建 PR 不隐式触发二者。

**验收：** 真实临时仓库/bare 远端验证区块、冲突、审批期间索引变更、hook失败、提交已生效
但回包取消、非快进、两任务和重启；随后获准真实远端认证及原生键盘预览确认。参考：[Git 提交](archive/GIT_WORKTREES.md#git-commit-acceptance)、[Git 推送](archive/GIT_WORKTREES.md#git-push-acceptance)。

### R12：完整 Pull Request 工作流

**现状：** GitHub 只读 PR 页面、行内定位、评审意见/活动事件分区（source.pr_reviews、
source.pr_activity，同一版本看守与有界解析）及审阅对话已有实现；按账号个人收件箱已落地
（GitHub search/issues 只读端点，involves/author/assignee/review-requested/mentions 角色筛选、
20 项分页、incomplete_results 标记、repository_url 端点与单段 owner/repo 校验、id 去重、
读取前后 account revision 双核对；仅 VM 经 Application 端口调用，不暴露模型工具）；已读/版本
标记持久化已落地（pull_request_marks v44，主键 (account,owner,repository,number) 记录查看时
head 与 UTC Unix 秒，不随账号删除清理；列表/URL 直达加载后批量取标记并与条目在同一代际原子
发布，绝不混合新 head 条目与旧代际标记；无标记/同 head/异 head 分别本地化标“未读/已读/
已更新”，打开详情按当前 head 单语句 upsert 尽力落账且失败不影响详情）；按 grant 显式写
授权模型已落地（schema 45：source_repository_grants.can_write NOT NULL DEFAULT 0，存量
行保持只读且不提供改写通道；授权编辑器以本地化勾选框显式选择写权限、列表行标注只读/读写，
变更权限只能撤销后重新授权；独立 ResolveWriteAsync 端口在读解析双重核查之上再核对 grant 行
can_write，伪造或陈旧 scope 一律 source_scope_denied 拒绝）；确认写工具已落地五个：
source.pr_comment_create（GitHub issues/{n}/comments 会话评论，模型不可见，仅面板入口）、
source.pr_review_submit（GitHub pulls/{n}/reviews 评审提交，模型不可见，仅面板入口）、
source.pr_close（GitHub PATCH pulls/{n} {"state":"closed"}，模型不可见，仅面板入口）、
source.pr_merge（GitHub PUT pulls/{n}/merge，模型不可见，仅面板入口）与
source.pr_create（GitHub POST pulls 新建 PR，模型不可见，仅仓库级工具栏入口；配套的
branches 分支列表是只读端点 GET repos/{o}/{r}/branches?per_page=100，走 ResolveAsync
读解析而非写解析，resolve→GET→resolve 双重核查；直接枚举根数组、上限 100 项（不复用
20 项上限的 Items 解析器），name 限 1–255 无控制字符与 ".."、commit.sha 须 40/64 位
hex、重名即拒；创建对话框 base/head 无任何预选，必须显式选择且两者不得相同，标题
1–256、正文可选 ≤65536 仅非空时随包发送、draft 布尔原样透传；创建无既有 revision
可钉，故只做无 revision 的写预校验 ResolveWriteAsync→单次 POST，POST 负载仅
title/head/base/draft（+可选 body）字段，响应校验 url 精确归属 pulls/{n}、number>0、
head/base sha 均为 40/64 位 hex，服务端 422 对重复 head/base 开放 PR 报
source_write_rejected；未知结果经一次有界 GET pulls?state=open&head={owner}:{head}&
per_page=5 核实，逐项要求 url 前缀归属、number>0、双 sha 有效且 head.ref/base.ref/
标题精确匹配（他人 head 分支的 PR 绝不误算），命中返回已核实回执，未命中报
source_write_unconfirmed，核实失败报 source_write_unknown，绝不二次 POST；成功后
按回执 number 走既有链接加载路径重读新 PR 详情，读取失败仍如实报告已创建）；
写入链路为写授权 ResolveWriteAsync→GET 摘要核对捕获的 base/head revision（漂移即
source_revision_changed）→再次 ResolveWriteAsync→单次远端写并校验响应归属；
绝不自动重试，timeout/transport/unavailable 等未知结果只做有界 GET 核实——评论按近 5 条
正文精确匹配；评审端点按时间升序返回且无排序参数，故升序分页遍历（每页 20 条、至多 10 页，
遇短页即止）按 state/commit_id/规范化正文（缺失正文两侧归一为空串）匹配；关闭不产生可查的
新建资源，以一次 GET 摘要为权威——同 revision 且 state=="closed" 即核实落地，open/其他状态或
revision 漂移报 source_write_unconfirmed；命中返回带“经核实已落地”警示的成功回执，
未命中报 source_write_unconfirmed，核实失败报 source_write_unknown；评审 POST 还固定
commit_id 为捕获的 head，服务端对漂移 head 直接拒绝；关闭 PATCH 仅含 state="closed" 字段，
响应校验 url 精确归属 pulls/{n}、number 相等、返回 state 由写入器复核为 closed；合并 PUT
负载仅含 sha（钉住已核对 head，服务端对漂移 head 直接拒绝）与 merge_method（merge/squash/
rebase 闭集映射、默认拒绝）两字段，响应校验 merged==true 且 sha 为 40/64 位 hex 提交；
合并不产生可查的新建资源且落地后 base 必然前进，故核实不复用摘要比对——以一次 GET 详情
校验 url 归属与 number 后要求 merged==true 且 head.sha 与捕获 head 相同，命中返回
merge_commit_sha 回执（服务端缺省则如实置空），未命中报 source_write_unconfirmed；
面板正文上限 4096、两段式确认按钮（先进入确认态再执行），评审须先显式选择
批准/请求修改/评论结论（正文可选），合并须显式选择三种合并方式之一（无预选），
只读 grant 显示本地化只读提示，成功后回源刷新对应首页
且不插入本地伪条目；PR 关闭与合并在审阅面板顶部按两段确认呈现，取消文案如实说明请求可能已发出，
草稿 PR 显示本地化草稿提示而非合并按钮，成功后重读 PR 详情（已关闭的 PR 不再显示关闭或合并入口）。
OAuth/GitLab/云审阅仍缺，有外部接口与账号依赖。

1. Application 来源控制端口与 Infrastructure 客户端继续扩展关联信息、限流反馈等剩余只读
   能力；viewed/标记持久化已落地（见现状），后续读取路径必须保持标记与条目原子发布、
   不混合新 head 条目与旧代际标记。
2. 写授权模型已落地（见现状）：将创建 PR、普通/行内评论、提交 review、关闭和合并拆成独立
   工具，经 ResolveWriteAsync 端口解析写权限。App 先准备账号、仓库、base/head、正文/决策
   和精确行位供审阅，再请求该动作的明确确认；只读 grant 不升级为写授权。
3. 写入前复核身份、权限和最新 revision，使用服务支持的条件/幂等机制；结果未知先查询核实。
   不自动重试远端写入，不因 PR 响应授权任意日志 URL；创建 PR 所需分支发布走独立 Git 推送流程。
4. OAuth 先完成官方客户端注册/回调/刷新/撤销设计；GitLab 做独立语义适配器，云审阅单列
   服务范围。公开接口、权限或账号不足时记录缺口，不用 token 输入框替代完整 OAuth。

**验收：** 确定性 HTTP 覆盖版本变化、撤权、分页、限流、行位过期、写入已生效但回包丢失、
取消与恢复；获准测试仓库完成原生“读取→讨论→预览→确认写入→返回查询”，保留其他草稿和任务。
参考：[完整 PR 方案](archive/GIT_WORKTREES.md#pull-request-workflow-plan)、[只读查询边界](archive/GIT_WORKTREES.md#source-pr-on-demand-reading-acceptance)。

### R13：插件、技能和扩展

**现状：** 有包格式与兼容性设计；本地包预览管线已落地——`ZipPluginPackagePreviewer`
（Infrastructure/Plugins 经 Application `IPluginPackagePreviewer` 端口）流式校验 zip 归档
（绝对路径/穿越/链接与重解析点/重名条目/解压限额一律 fail-closed 拒绝）、有界解析根
`plugin.json` 与 `mcp.json`、按 `extensions.com.openai` 对象整覆盖 `.codex-plugin/plugin.json`
的规则判定 overlay、枚举 skill/MCP/资产组件并输出整包 SHA-256 预览，不落地解压、不持久化。
安装基础设施也已落地：`PluginPackageInstaller`（经 Application `IPluginPackageInstaller` 端口）
安装时独立重跑完整预览校验，绝不信任调用方预览；载荷先提取到 `_staging/{guid}`（逐条目
重规范化、拼路径前缀复核、CreateNew 有界复制、CRC 与声明长度双比对），再原子移入不可变的
`{name}-{version}-{hash8}` 目录，最后经 `IInstalledPluginStore` CAS 写 `plugin_packages`
行（schema v46；同 name+version+hash 幂等返回既有行，不同 hash 即 `plugin_install_conflict`）。
任何中途失败或取消清理 staging、不落行；行写失败则尽力删除 final，遗留由 `ReconcileAsync`
报告（清 `_staging` 残留、列出 orphan final，均不自动执行/删除）。`AppStartupService`
已在启动恢复链中接线 reconcile——失败或存在 orphan final 时计入存储恢复警示。
同名升级替换也已落地：同 name 不同 version 的安装在同一事务内删除该 name 全部既有行
（component_settings/component_hashes 子行随 plugin_id 级联清除）并写入新行与其载荷哈希
基线，提交后尽力删除被替换行的载荷目录——删不掉即留作 orphan 由 reconcile 报告；新行恒以
Installed 落库而不继承 Enabled——载荷已是新字节，须显式重新启用才恢复信任，两个同名版本
因此永不共存、不会向派发提示词重复注入技能文本；同名同版本同内容仍幂等返回既有行、同版本
不同内容仍是 plugin_install_conflict 须先卸载，`task_skill_overrides` 对旧 plugin id 的行
因无外键自然惰性失效，无需处理。
生命周期操作也已落地：`EnableAsync`/`DisableAsync` 迁移状态标记（Installed/Enabled/Disabled
全转换幂等），`UninstallAsync` 先删行再尽力删载荷——
删不掉时行已删、载荷留作 orphan 由 reconcile 报告，源包文件从不动。设置页"本地插件"面板
已落地（设置 tab 9：文件选择→清单预览（名称/版本/描述/组件/MCP 声明/hash/来源）→两段确认
安装、行级启停与两段确认卸载，错误码映射本地化文案）。skill 组件注入也已落地：
`PluginSkillActivator` 在任务派发时物化 Enabled 插件的 skill 组件——component.RelativePath
重新规范化并做安装目录前缀包容复核（拒绝空/根化/'..' 与任何逃逸），文件须存在且 ≤8KiB
（读取失败即跳过而非失败），条目按 (plugin,component) 序累计、总量 ≤24K 字符、放不下的
skill 整体跳过不留行，注入为护栏行之前的带标签段落；`WorkspaceRunExecutor` 仅在非
source-review 聊天派发时调用，注入的确切 skill 文本逐条落 `task_skill_activations` 行
（schema v47，task_id ON DELETE CASCADE、整体替换式写入），因此后续禁用/卸载不能改变
已派发运行的提示词；任务审计页"插件技能激活"分区可查看激活行（插件版本/组件路径/hash
前缀/时间）与注入文本节选（best-effort，读取失败不影响执行记录）。单技能启停也已落地：
`IPluginComponentSettings` 端口（Application/Plugins）仅覆盖 skill 组件，`plugin_component_settings`
行（schema v49，(plugin_id,component_path) 主键、ON DELETE CASCADE 随 plugin_packages 清除）
记录最后一次显式选择，无行即默认启用——卸载清行、重装换新 plugin id 即回到默认，此为有意语义；
`PluginSkillActivator` 对每个 Enabled 插件批量读取一次覆盖表、停用技能在文件读取前即跳过且不留
激活行（覆盖读取失败与插件列表读取失败一样向上传播，不静默降级）；设置页"本地插件"在每个
插件行内嵌该包的技能列表与独立开关，写前经 `FindAsync` 复核插件仍在，写失败回滚行内勾选并
提示刷新。开关只影响后续派发——已派发任务的 `task_skill_activations` 快照不回改。载荷信任基线也已落地：
`plugin_component_hashes`（schema v50，(plugin_id,component_path) 主键、随 plugin_packages ON DELETE
CASCADE）按清单组件记录安装时逐文件 SHA-256，与插件行在同一事务原子写入且只插不改（INSERT OR
IGNORE，已建立的基线永不被覆盖）；因 `state` CHECK 需容纳 Drifted 值，v50 同时以 foreign_keys=OFF
的重建迁移把 `plugin_packages` 放宽到 (0,1,2,3) 并保留既有行与 component_settings 子行，重放仅由
版本行门控。`PluginSkillActivator` 对每个 Enabled 插件随覆盖表一并读一次基线：技能组件先读磁盘
字节算 SHA-256 与基线比对，通过后才 UTF-8 解码并沿用既有 ≤8KiB/≤24K 限额与激活行流程；缺基线行、
不一致、组件文件缺失或不可读即经 `SetStateAsync` 置 Drifted 并中断该插件其余组件（其他插件不受
影响），漂移仅影响本次及后续派发、不回改已派发快照。`PluginPackageInstaller.EnableAsync` 对 Drifted 行以
plugin_payload_drifted 拒绝——漂移载荷只能卸载后重装，停用与卸载仍照常；设置页行徽标显示
“载荷已漂移”且启停开关禁用。启动 `ReconcileAsync` 也为缺基线的存量插件行回填：仅对按同一包容
规则解析到且仍在安装目录内的组件文件计算 SHA-256 入库，文件缺失即不留行，使旧库升级到基线语义
而不重写已验证内容。按会话技能选择也已落地：`ITaskSkillOverrides` 端口与 `task_skill_overrides`
表（schema v51，(task_id,plugin_id,component_path) 主键、随 agent_tasks ON DELETE CASCADE，
plugin_id 有意不设外键——卸载/重装换新 plugin id 后陈旧行保持惰性历史而不被清理或复活）记录单个
会话对某技能的最后一次显式选择，无行即回落全局层；`PluginSkillActivator` 派发时对每个任务读取一次
覆盖并按 任务行→全局行→默认启用 合并生效态，任务行既可跳过全局启用的技能、也可重开全局停用的
技能（重开后仍过同一基线 SHA-256 校验，漂移即置 Drifted 且该插件本次不注入任何文本），读取失败
与停用技能不留激活行的语义同全局层一致；作曲区工具栏"技能"选择器按插件分组列出 Enabled 插件的
技能组件与生效态，会话未建时改动记入内存草稿、首个任务创建时经单一序列化排空落库且失败保留并
提示，写失败回滚勾选，"重置为全局"经 `ClearForTaskAsync` 清任务行回到全局层，已派发任务的
`task_skill_activations` 快照同样不回改。显式信任修订计数也已落地：`plugin_packages.trust_revision`
（schema v52，两列 ALTER 各由列存在单独门控、版本行门控与之并联，故仅丢版本行可安全重放、
v50 重建式重放丢列后亦能按列门控修复）是随行单调递增的信任代际——插入与同名替换新行恒写
1、`SetStateAsync` 仅在真实状态迁移时 +1（同状态幂等调用返回原行不产生新代际，Drifted→Drifted
不再递增），存量行升级一律取诚实基线 1；`task_skill_activations.trust_revision` 记录派发时插件的
代际，激活行由此持有确切版本与信任 revision 二元组；任务审计页激活行显示记录的 r{N}，插件行
已删除时标"插件已移除"、当前代际与记录不同时标"信任已变化（r旧→r新）"，当前插件行查询与
激活读取同为 best-effort、失败不影响执行记录。MCP 运行时接入（stdio 等 R01 沙箱）、市场/官方
目录、OAuth 与 hooks 执行链路仍缺——目录→安装→启用→执行→卸载的完整生产链路仍不完整。
2026-10-19 入库的官方记录将相关 App Server 插件方法列为尚不供生产调用；实施前应重新核实，不能假定目录接口可用。

1. Domain 保存包身份、来源/版本、组件及信任状态；Application 定义目录/安装/启停/移除端口；
   Infrastructure 实现有界 manifest 解析、本地包与来源适配、受管 staging 和不可变安装版本。
   按已核实的 `plugin.json`/skills/`mcp.json` 和 OpenAI overlay 整体替换规则处理，legacy 导入独立测试。
2. 首先交付本地包“预览来源/hash/能力→确认安装→启用→真实 skill 影响模型请求或 MCP 工具执行
   →停用→卸载”。市场、官方目录、账号和工具 UI 各自建立生产链路，不能由本地导入宣称全部支持。
3. 包不授予项目目录或浏览器权限；拒绝 archive traversal、绝对路径、重解析点、冲突文件和
   解压炸弹。skill 是有来源的指令，MCP/hooks/代码各自审查、审批及 R01 隔离，不自动执行安装脚本。
4. 升级原子切换配置；活动任务持有确切版本与信任 revision，停用阻止新调用，卸载只清理
   自有文件。失败可回滚配置，重启只显示中断安装/清理，不重放 hook。

**验收：** 真实技能提示词或 MCP 调用进入普通管线；覆盖拒绝包、停用/撤权、升级失败、并发运行、
取消、重启和用户文件保留。目录/官方兼容只按获准公开接口验收。参考：[插件方案](archive/OVERVIEW.md#plugin-workflow-plan)。

### R14：目标窗口绑定的桌面控制

**现状：** 有敏感桌面截图基础，完整指针/键盘控制、排他接管和目标会话链路尚未实现。

1. Application/Tools.Abstractions 定义 task/window/session 绑定及截图、坐标、指针、键盘端口，
   Platform.Windows 实现；每动作独立工具，经敏感审批、取消/时限、产物限制与审计。
2. 绑定窗口句柄、进程身份和会话代际，明确 DPI/屏幕与客户区坐标空间；操作前复核。
   失焦、窗口关闭/替换、UAC 安全桌面或用户接管时停止，不能向当前任意前台窗口补发输入。
3. 窗口输入使用受控排他策略，避免两个任务同时操纵一个目标；用户接管后等待显式交回。
   导航、取消或宿主关闭使旧代际失效，迟到事件不能重定向到新窗口。

**验收：** 用独立本地测试窗口验证截图→操作→状态变化，以及拒绝、失焦、DPI变化、目标替换、
取消/超时、多任务竞争和恢复；日常窗口只在具体授权场景验收。浏览器 DOM 工具不能当作桌面控制完成。

### R15：Web 搜索和可核对引用

**现状：** 未实现专用搜索链路；普通页面导航不等于搜索。具体参考客户端语义仍需核实。

1. Application 定义查询、来源、摘要/正文、引用位置和预算端口；区分 provider 原生搜索、
   明确搜索服务和 MCP 适配，Providers/独立工具实现，App 显示来源及服务错误。
2. 搜索和正文取回都经过统一管线。原生搜索若随模型请求触发外部动作，也要先获得对应
   能力/审批并审计，不能直接开启参数旁路；设置查询、页数、字节、时间及费用上限。
3. 请求凭据仅发送给配置服务，正文抓取限制目的地和重定向，不能获得本地文件/内网的隐式权限。
   搜索结果作为不可信数据进入预算，回答引用绑定实际已读取来源，不能让页面内容覆盖系统权限。

**验收：** 查询→结果→正文→回答引用形成闭环；覆盖拒绝、服务不可用、限流、取消、伪造/重复来源、
恶意页面及超大结果。先可控服务，再获准真实服务，引用能回溯，失败不伪造答案来源。依赖服务/模型能力核实。

### R16：图像生成和编辑

**现状：** 已有图片输入和缩略图，但没有专用生成/编辑请求、任务状态和结果产物链路。

1. Application/Providers 定义生成/编辑能力、服务任务 ID、输入快照归属、尺寸/数量/格式
   及费用预算；生成与编辑各自工具化，经统一审批和审计，不复用聊天请求假装成功。
2. 贯通提交→进度/轮询→下载→解码/hash→任务产物→预览/保存。App 使用共享结果组件，
   明确等待、失败和可恢复状态；文件存储复用 R02，会话不把大图二进制写入 SQLite。
3. 输入外传与收费动作明确展示；取消时区分未提交、服务已接收和结果未知。
   有 task ID 时查询已有任务，未知时不能自动重发收费请求；恢复不触发新生成。
4. 输出校验下载 origin、重定向、MIME与真实解码、总字节及像素数，禁止授权头跟随任意地址，
   保存/覆盖走 R19 的目的路径授权和原子写入。

**验收：** 模拟协议覆盖完整流程、损坏/超大结果、拒绝、限流、超时、并发和恢复；
获准真实服务验证实际生成与编辑效果，记录明确支持的模型/格式，不用缩略图渲染当生成证据。

### R17：语音输入、实时会话/输出及输入预测

**现状：** 对应专用链路未实现；转写、实时音频、朗读和输入预测是不同子能力，需要分别验收。

1. Application 分别定义录音会话、转写、实时音频、合成/播放和草稿预测端口；
   Platform.Windows 管设备与播放，Providers 适配协议，App 通过共享控件显示录音/停止/错误及预测开关。
2. 录音显式授权并持续可见，导航/取消/关闭及时释放设备；转写先进入草稿供确认，
   不自动发送或触发工具。实时音频处理需要清楚的会话生命周期和有界音频缓冲。
3. 输入预测使用独立代际/取消和防抖，保存是否允许向远端发送草稿的偏好；
   旧建议不能覆盖新输入、IME组合或用户选择，不隐式提交消息。
4. 音频大小、时长、并发和费用有上限，原始敏感音频不写诊断；恢复仅显示中断会话，
   不自动重开麦克风、播放或发送旧内容。

**验收：** 可控音频和模拟服务覆盖设备拒绝/丢失、断流、取消/超时、并发、旧建议和输入法；
独立原生麦克风/扬声器与获准真实音频服务另验。文本 SSE 通过不能证明实时语音可用。

### R18：系统通知、持续目标和预算

**现状：** 已有普通运行任务与定时任务，专用系统通知、持续目标/预算和应用关闭后的后台宿主未建立完整链路。

1. Domain/Application 定义 objective/run/checkpoint/budget 和通知事件端口，Infrastructure
   保存目标、消耗和状态，Platform.Windows 适配系统通知；不能用无限循环或定时任务同名入口替代。
2. 明确开始、运行、需人工、完成、失败、取消、中断及用户暂停状态，设置时间/token/成本
   上限与停止条件。在模型轮次和工具派发边界检查预算，超限停止新副作用并完成资源清理。
3. 通知遵循用户偏好、静默时段和合并规则；点击只定位对应任务，不展示密钥或敏感正文。
   通知注册、回调、任务删除后的点击和多实例路由各自有所有权。
4. 崩溃只恢复检查点和待人工状态，不重做工具；应用关闭后持续运行须与 R10 的可选
   后台宿主统一认领/退出/安装方案，前台与后台不能同时执行同一目标。

**验收：** 预算耗尽、需人工、用户暂停/继续、通知关闭/点击、并发前台草稿、进程重启和实际 Windows
toast 全覆盖；目标完成需有可检查结果和终止条件，不能仅靠模型自述成功。依赖后台范围与费用策略明确。

### R19：可信文档预览、交互可视化及保存/导出

**现状：** 有文本/代码/差异和图片缩略图；完整 PDF/Office、交互内容与通用生成文件导出尚缺。

1. Application/Infrastructure 统一 task/message/tool/artifact 所有权、MIME、hash、大小与来源；
   App 共享预览宿主按文件类型分派，格式不支持明确说明，不把代码窗口当作所有文档预览。
2. PDF/Office 使用只读安全渲染或明确的外部打开动作；交互 HTML/脚本放在隔离 WebView，
   默认关闭网络、文件访问和宿主桥。需要的额外能力逐项授予，生成内容不自动成为可信应用代码。
3. 保存/导出先明确目标路径授权、格式和覆盖预览，再用原子写入及外部修改检测提交；
   取消/失败保留原目标文件。正在预览/导出的产物持有租约，防止 R05 回收中途删除。
4. 缺失/篡改产物、渲染进程异常、超大资源及外部程序不可用提供恢复路径；
   重启恢复元数据和明确选择，不自动执行附件脚本或加载远端资源。

**验收：** 实际生成→预览→保存→关闭重开形成闭环；覆盖恶意内容、网络/文件越权、超大资源、
被占目标、覆盖冲突、取消与并发回收。独立原生测试文件打开和键盘可达性。依赖 R02/R05。

### R20：Appshots、浏览器扩展和跨进程接管

**现状：** 现有截图/CDP不证明兼容 Appshots 或官方扩展；具体快照和扩展协议/平台范围待核实。

1. 先核实快照的页面、视口、DOM、图像、元数据和脱敏语义及公开格式。
   Browser/Application 定义任务所属快照端口，文件型内容落产物，明确不支持的字段和平台。
2. 扩展独立设计 manifest、origin/extension 身份校验、一次配对、task/tab/会话代际和能力协商；
   通信有大小/频率/超时限制，不任意开放调试端口或接受其他网页伪造消息。
3. 与 R07 共用人工接管 gate、只关闭自有标签和显式交回流程；连接被替换或浏览器重启时
   撤销旧代际，恢复需要重新配对/附着，不重放点击。
4. 官方协议未公开时保留兼容缺口，可提出 MossAgent 独立扩展方案并明确差异，
   不能自动视为官方功能已完成；安装扩展和访问日常 profile 单独获得具体授权。

**验收：** 标签切换/关闭、恶意页面消息、配对撤销、版本不兼容、断连、取消、多任务和浏览器重启，
再做真实扩展/原生接管。依赖参考语义与公开协议核实，截图工具改名不计交付。

### R21：宠物交互与 Codex Micro 外设

**现状：** 没有对应生产链路；装饰宠物和硬件是两个独立范围，参考 Windows 支持与协议仍待核实。

1. 宠物先明确动画、任务活动反馈、点击行为、多窗口和关闭方式；App 实现独立可关闭的
   展示服务、受管动画资源及可访问说明，不读取任意其他窗口或让装饰点击直接触发工具。
2. 外设先确认公开硬件协议、Windows驱动、配对、权限和固件范围；Platform.Windows
   提供设备端口，Application 只接受白名单设备事件，敏感动作仍走普通授权/审批。
3. 两者分别保存用户偏好、资源/设备版本与错误状态；资源丢失或设备断连可降为明确不可用，
   不伪造连接，不在重启后自动执行缓存的设备命令。

**验收：** 宠物验证动画、交互、活动状态、多窗口、资源失败、重启及无障碍关闭；
外设先协议模拟覆盖伪造/断连/取消，再真实硬件验证。缺少公开协议或硬件时保持未完成。

### R22：Sites/dots/Space、ChatGPT 登录及远程/云/移动

**现状：** 对应服务接入未完成或未验证。参考目录存在入口，不表示有可用第三方 API，
也不表示本地模型密钥、定时任务和项目已实现这些服务的语义。

1. 建逐服务依赖表：功能定义、Windows/跨端入口、公开接口与使用限制、账号计划、客户端
   注册、权限范围、费用、数据流向及可实施范围。分别核实 Sites 构建/发布/协作、dots 持续责任代理、
   Space 共享页面、Sign in with ChatGPT，以及远程运行、云审阅和移动访问；未核实语义标为待核实。
2. Application 分离身份、部署、共享/同步与远程运行端口；只对接官方公开允许的接口。
   登录设计回调、PKCE/适用授权流程、刷新与撤销，API key 不能冒充 ChatGPT 登录；不调用私有端点或提取浏览器 Cookie。
3. 接口开放时，远程任务保存独立 run/权限/服务任务 ID，提供有界日志、状态、产物取回和
   明确确认的发布/分享/写入动作；本地授权根不自动外传或变成远程工作区。
4. 同步先定义项目/对话/产物/草稿的选择范围、冲突、撤权、删除和离线策略，明确密钥
   是否跨端及其保护方案。断网/重启先查询已有远程任务，不重复提交、计费或发布。
5. 无公开接口的子能力保留缺口，提出独立本地/自托管实现的可审阅方案及语义差异；
   由用户决定是否接受替代范围。实施前需要具体服务、账号注册权限、后端与费用/运维决策，不能擅自隐藏入口。

**验收：** 每个服务独立完成模拟和获准真实账号链路，覆盖权限不足、过期/撤销、断网、取消、
结果未知、跨端冲突和恢复；外部部署/分享/写入按具体动作授权。服务未开放时不能承诺完整官方兼容。

### R23：当前原生界面、窗口恢复和可访问性

**现状：** 共享控件、主题和紧凑布局已有证据；原生 restore 裁切缺陷仍开放，当前版本
全部工作流、DPI/焦点/输入并未完成原生验收。截图黑帧和输入访问拒绝也存在测试工具限制，不能一概归为产品缺陷。

1. App 按关键入口列加载/空/忙/禁用/错误/恢复及键盘焦点路径，覆盖 526×601 最小窗口、
   常见窗口、最大化、明暗主题和多 DPI；继续共享控件/集中样式/本地化，补可访问名称和屏幕阅读器语义。
2. 原生 restore/DPI 调查先锁定可复现的 EXE/依赖、Windows消息、实际客户区和稳定布局，
   分离回调中的暂时 Bounds 滞后与稳定后裁切。捕获失败态后才定位并修复，不能以重启、健康截图或强制布局当作修复证明。
3. 独立临时数据和 EXE 窗口逐项验：项目/无项目发送、历史/草稿、Queue/Steer、附件、Git/工作树、
   提供商/项目授权、审批、浏览器、MCP、子代理、定时及 PR。记录请求/数据库断言，不操作或关闭用户已有窗口。
4. 先用 Headless 确认逻辑布局，再原生确认输入、焦点、弹层和窗口行为，真实账号请求另列。
   截图只验遮挡、裁切、对齐和可读性，不以 Codex 像素相等验收；遇到工具黑帧/访问拒绝记录环境限制并补验。

**验收：** 每条原生工作流有成功、拒绝、取消和恢复记录，明暗/窗口切换不丢草稿或焦点；
restore 裁切有修复前后同场景证据，DPI转换与稳定后布局通过。参考：[原生诊断](archive/UI_WORKFLOWS.md#native-resize-diagnostic-progress)、
[原生消息](archive/UI_WORKFLOWS.md#native-resize-message-progress)、[紧凑布局](archive/UI_WORKFLOWS.md#compact-navigation-acceptance)。

### R24：固定提交全量回归和 JIT 单文件发布验收

**现状：** 发布配置已是 Windows x64/JIT/自包含/单文件，无 AOT/裁剪；当前候选只有明确的
分层相关回归，尚无当前正式包干净机验收，不能称为发布就绪。

1. 在固定提交的独立候选完成相关专项和完整解决方案 Debug/Release 构建，再执行八套完整
   测试并核对终态、失败与跳过；保留源码/配置及运行文件身份，不使用旧输出或其他候选结果拼接通过。
2. 运行结构/依赖/共享控件约束，复验安全拒绝、并发及恢复。历史失败留档，修复后的全量
   另有独立记录；R01/R02安全阻断、约定功能范围和R23关键原生问题未关闭时不能宣布整体完成。
3. 使用现有 `scripts/publish.ps1` staging/验证/替换流程生成测试包，不覆盖已交付版本。
   检查 EXE/ZIP 内容、版本、签名方案、解包目录权限及 JIT/单文件设置，保留可恢复的旧包。
4. 干净 Windows 验首次启动、WebView2/Playwright/ConPTY/native依赖、离线/代理、正常退出/
   崩溃/手动恢复；用已有数据库副本逐版本向前迁移，核实任务/历史/授权/草稿保留且副作用不重放。
5. 本地打包与外部发布分开记录；推送、发行版上传和安装只在对应明确要求下执行。
   真实服务与硬件验收按所承诺支持矩阵登记，未支持组合继续显式说明。

**验收：** 约定范围有运行闭环、关键原生路径通过、完整构建/测试与正式包验证通过，且没有
阻断问题；才可以标记整个目标完成。参考：[发布脚本](../scripts/publish.ps1)、[架构](ARCHITECTURE.md)。

## 7. 建议执行顺序与交付物

| 阶段 | 工作 | 交付物和退出条件 |
| --- | --- | --- |
| S0：闭合本地安全边界 | 环境未就绪时继续 R02，并优先研究 R04 的创建认领/清理交接；同时准备 R01 独立环境探针 | 端口/阶段设计、迁移、受控竞态反例和生产管线验证；未证明的边界仍明确开放 |
| S1：命令隔离 | 支持 MXC 的独立环境到位后实现 R01，并复验所有代码执行入口 | pwsh/SDK/Git/PTY 正常，根外/子进程/继承句柄越权拒绝，无弱回退；本地文件工具安全不被误当替代 |
| S2：核心工作流完整性 | R03–R10，结合 R05/R06 的文件和协议能力；分支发现与工作树恢复收尾 | 每项设置/保存→真实运行→结果→取消/并发→手动恢复闭环；逐项原生及真实依赖证据 |
| S3：协作和扩展 | R11–R14：Git细粒度操作、PR、插件和桌面控制 | 预览/明确确认/写入/失败核实及权限边界可验证；本地基础能力与官方/远端接入分别验收 |
| S4：其他参考入口 | R15–R22 的接口核实与分项实施；只读调研可提前 | 每入口有语义/依赖结论；开放能力真实运行，封闭接口保留缺口与待决策方案，不自行删范围 |
| S5：整体验收和包 | R23贯穿各阶段，最后完成 R24 | 固定提交全套与干净机包验证，无阻断，再形成可发布结论；发布动作另按要求执行 |

每个切片都应提交可审阅的设计自查、生产入口、迁移/兼容策略、针对性成功与负例测试、
实际运行证据、失败记录和尚未验证范围。代码量、按钮数量和测试通过率不能作为功能完成度。

## 8. 当前外部依赖与待决策项

| 项目 | 已知情况 | 推进方式 |
| --- | --- | --- |
| 原生 MXC 验收环境 | 路线已由用户选定，独立环境尚未就绪 | 保留 R01 开放，继续本地 R02/R04；环境就绪后先读系统/SDK实际层级，再实施兼容及隔离测试 |
| 真实提供商/代理/PR账号 | 当前已有确定性与部分历史原生证据，不能覆盖所有当前真实服务 | 按具体协议、模型、仓库和授权动作准备验收，使用临时数据和脱敏结论 |
| 官方目录、登录、云与跨产品接口 | 参考盘点是历史记录，开放程度和 Windows 支持需逐项复核 | 先形成公开接口/注册/计划/数据流表；缺接口时给独立方案及差异，待范围决策，不能默认替代 |
| 麦克风、真实外设、原生自动化环境 | 需要设备/权限；原生黑帧/输入拒绝会限制证据 | 可控模拟先行，实际设备和独立 Windows 实例另验；不修改用户日常窗口或系统配置来凑通过 |
| 后台持续运行和同步 | 不属于当前前台调度器的既有保证 | 先设计宿主、IPC、单次认领、安装/更新和数据范围，再决定实现，不擅自创建系统常驻服务 |

## 9. 维护和验收记录规则

- 关闭 R 项时更新本文件；旧功能矩阵和阶段交付报告保留为历史归档，不再重复维护状态，
  写明版本、入口、链路、数据传递、证据种类和剩余范围，不只修改“已完成”文字。
- 各项真实副作用的恢复均要求人工核实；“能查看历史”与“能再次安全运行”分别证明。
- 自动化/模拟服务、Headless、真实本地资源、原生 GUI、真实外部账号和发布包分别计证据，
  不以任何一种替代另一种；完整回归只对运行时的固定候选成立。
- 这份文档列举的是可交付能力和未关闭事项，不提供没有依据的整体完成百分比。
  当前仍以“核心链路已有、产品未完成、命令隔离为阻断”作为交付结论。
