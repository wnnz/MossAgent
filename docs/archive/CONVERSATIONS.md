# 对话、草稿、附件与任务

> 历史归档：保留原报告当时的结论、实验、失败与限制。整理于 2026-10-21。
> 当前能力与未完成方案统一维护在[功能清单](../FEATURE_STATUS_AND_IMPLEMENTATION_PLAN.md)；
> 原文中的计划、主机状态、测试数字及“正在进行”等措辞均为历史检查点。
> 代码块和仓库相对产物路径保留原意；本机忽略的 `artifacts/` 不随克隆分发。

[文档入口](../README.md)

## 原报告索引

- [未发送文本附件草稿](#attachment-draft-acceptance) — `ATTACHMENT_DRAFT_ACCEPTANCE.md`
- [文本附件历史与恢复](#attachment-history-acceptance) — `ATTACHMENT_HISTORY_ACCEPTANCE.md`
- [附件选择的上下文隔离与取消](#attachment-selection-isolation-acceptance) — `ATTACHMENT_SELECTION_ISOLATION_ACCEPTANCE.md`
- [Image attachment thumbnail workflow](#attachment-thumbnail-acceptance) — `ATTACHMENT_THUMBNAIL_ACCEPTANCE.md`
- [Composer keyboard submission acceptance](#composer-keyboard-acceptance) — `COMPOSER_KEYBOARD_ACCEPTANCE.md`
- [Conversation drafts](#conversation-draft-acceptance) — `CONVERSATION_DRAFT_ACCEPTANCE.md`
- [Queue configuration checks at atomic dispatch](#conversation-followup-dispatch-acceptance) — `CONVERSATION_FOLLOWUP_DISPATCH_ACCEPTANCE.md`
- [Running conversation follow-ups](#conversation-followup-plan) — `CONVERSATION_FOLLOWUP_PLAN.md`
- [Queue recovery after process termination](#conversation-followup-process-recovery) — `CONVERSATION_FOLLOWUP_PROCESS_RECOVERY.md`
- [Conversation follow-up Queue runtime acceptance](#conversation-followup-runtime-acceptance) — `CONVERSATION_FOLLOWUP_RUNTIME_ACCEPTANCE.md`
- [Conversation follow-up storage foundation](#conversation-followup-storage-acceptance) — `CONVERSATION_FOLLOWUP_STORAGE_ACCEPTANCE.md`
- [会话标题与文本渲染验收](#conversation-heading-acceptance) — `CONVERSATION_HEADING_ACCEPTANCE.md`
- [Conversation preference and selection cleanup acceptance](#conversation-preference-acceptance) — `CONVERSATION_PREFERENCE_ACCEPTANCE.md`
- [Active-run text guidance](#conversation-steering-acceptance) — `CONVERSATION_STEERING_ACCEPTANCE.md`
- [Durable input for an active run](#conversation-steering-plan) — `CONVERSATION_STEERING_PLAN.md`
- [Atomic conversation submission](#conversation-submission-acceptance) — `CONVERSATION_SUBMISSION_ACCEPTANCE.md`
- [Conversation timeline acceptance](#conversation-timeline-acceptance) — `CONVERSATION_TIMELINE_ACCEPTANCE.md`
- [Draft background persistence](#draft-background-persistence-acceptance) — `DRAFT_BACKGROUND_PERSISTENCE_ACCEPTANCE.md`
- [Draft conversation header acceptance](#draft-conversation-header-acceptance) — `DRAFT_CONVERSATION_HEADER_ACCEPTANCE.md`
- [文件编辑链路验收](#file-editing-acceptance) — `FILE_EDITING_ACCEPTANCE.md`
- [历史工具上下文验收](#history-context-acceptance) — `HISTORY_CONTEXT_ACCEPTANCE.md`
- [Image attachment workflow](#image-attachment-workflow-acceptance) — `IMAGE_ATTACHMENT_WORKFLOW_ACCEPTANCE.md`
- [Image input: transport foundation](#image-input-acceptance) — `IMAGE_INPUT_ACCEPTANCE.md`
- [JPEG image-preview orientation](#image-orientation-acceptance) — `IMAGE_ORIENTATION_ACCEPTANCE.md`
- [消息发送与停止验收](#message-send-acceptance) — `MESSAGE_SEND_ACCEPTANCE.md`
- [Native submission and EXE interruption acceptance](#native-submission-acceptance) — `NATIVE_SUBMISSION_ACCEPTANCE.md`
- [Explicit attachments in projectless conversations](#projectless-attachments-acceptance) — `PROJECTLESS_ATTACHMENTS_ACCEPTANCE.md`
- [无项目聊天](#projectless-chat) — `PROJECTLESS_CHAT.md`
- [Queue snapshot completion observation](#queue-snapshot-completion-acceptance) — `QUEUE_SNAPSHOT_COMPLETION_ACCEPTANCE.md`
- [Queued write audit identity assertion](#queue-tool-audit-assertion-acceptance) — `QUEUE_TOOL_AUDIT_ASSERTION_ACCEPTANCE.md`
- [Send preparation cancellation](#send-preparation-cancellation-acceptance) — `SEND_PREPARATION_CANCELLATION_ACCEPTANCE.md`
- [安全失败类别与统一提示验收](#task-failure-acceptance) — `TASK_FAILURE_ACCEPTANCE.md`
- [Task menu lifetime and native sidebar replay](#task-menu-lifetime-acceptance) — `TASK_MENU_LIFETIME_ACCEPTANCE.md`
- [会话菜单写入与原生操作验收](#task-mutation-acceptance) — `TASK_MUTATION_ACCEPTANCE.md`
- [侧边栏会话行验收](#task-navigation-acceptance) — `TASK_NAVIGATION_ACCEPTANCE.md`
- [任务前后快照差异审查](#task-snapshot-review-acceptance) — `TASK_SNAPSHOT_REVIEW_ACCEPTANCE.md`
- [会话标题并发写入验收](#task-title-persistence-acceptance) — `TASK_TITLE_PERSISTENCE_ACCEPTANCE.md`

---

<a id="attachment-draft-acceptance"></a>

## ATTACHMENT_DRAFT_ACCEPTANCE.md

<!-- source: ATTACHMENT_DRAFT_ACCEPTANCE.md -->
### 未发送文本附件草稿

#### 设计自检与推进步骤

1. 先复现选择文本附件后切换对话、独立组合重开时的丢失问题。
2. Domain 草稿增加有稳定选择身份的附件载荷；Application 草稿端口不暴露文件或
   SQL。Infrastructure 把内容放在独立草稿快照目录，SQLite 只保存有作用域归属的
   元数据。文本和附件引用共同参与既有 revision/CAS 事务，不能分别覆盖。
3. App 使用独立的草稿/附件协调器，避免扩大 WorkspaceViewModel。待发附件属于
   任务或项目的新对话，不能进入无项目聊天或另一个项目；迟到读取不得覆盖新选择。
4. 已选择的内容是不可变快照，不重读变化的原文件。恢复不授予源路径工具权限。
   授权目录快照变化必须阻止发送并给出移除/重新选择的明确提示。
5. 丢失/损坏的附件保留可移除的元数据，不默默当作空文件发送；用户可移除不可用
   附件后保存文本和其余有效附件。正常关闭必须等待草稿落盘，失败不能静默丢弃。
6. 沿用十文件、每文件 128 KiB、UTF-8 文本及单任务取消/超时边界；拒绝跨作用域、
   路径穿越、链接、篡改、并发版本冲突，保留失败的本地输入和显式恢复入口。
7. 成功发送只消费已捕获选择身份；保存准备期间新增的下一条文本/附件应提升到
   实际任务作用域，不能被清空或再次自动发送。

遵守模块依赖、单类型、500 行文件和 80 行方法限制，不使用 partial 拆分。
复用共享 composer、附件标签、错误提示和恢复命令，不添加原生视觉控件或页面
私有样式。依据 ui-ux-pro-max 的错误就近反馈、明确恢复方式及键盘可移除原则验收。
使用临时数据、真实文件/SQLite 与 loopback；不操作已有用户窗口、凭据或审批策略。
这个专项不代替图像/PDF 多模态、原生 EXE 重启或整个功能对齐目标的完成验收。

#### 已实现链路

- `ConversationDraftAttachment` 保留稳定选择 ID、源路径元数据、显示路径、字节数、
  授权指纹和有界内存内容。`null` 内容仅表示快照不可用，不等同于空文件。
- `SqliteConversationDraftRepository` 通过原有 Application 端口在后台执行，统一
  30 秒取消边界。文本、revision 和附件引用在同一 SQLite 事务 CAS 中提交。
  版本 15 只增加元数据表；内容位于 `draft-attachments/<scope hash>/<selection>.txt`。
- 快照写入先创建独立临时文件，再原子移动；同一身份不得覆盖不同字节。读取验证
  长度、SHA-256 和严格 UTF-8，拒绝快照目录上的链接/重解析点，不重读源文件。
- `WorkspaceAttachmentDraftCoordinator` 连接现有标签与草稿，屏蔽导航清空的反向写回。
  迟到读取不覆盖新选择；同一作用域刷新及发送期间提升保留有效标签对象身份。
  文本编辑不反复重建未变化的附件标签。
- 草稿读取任务完成前，`LoadCurrentAsync` 等待其最后的状态发布，避免只看到已加载
  标志却漏掉附件恢复。原生 UI 通知使用构造时捕获的 Avalonia 同步上下文。
- 缺失/损坏附件保留可移除标签，发送命令禁用；授权指纹变化要求移除/重新选择。
  本地新编辑遇到不可用内容不能假装保存成功。关闭失败覆盖层提供显式移除已打开
  草稿中的不可用附件入口，保留文本与其他附件，不强制退出、不执行模型或工具。
- 成功发送按捕获的选择 ID 消费附件，而非按源路径清空。准备期间移除后重新选择
  同一个文件时，新身份和下一条正文被提升到实际任务，并可在独立组合重开后恢复。

#### 限制与后续

- 这些检查使用临时文件、真实 SQLite、loopback provider、独立生产服务组合，以及
  明暗主题 Headless 控件；不是原生 EXE 重启、真实文件选择器或付费模型验收。
- 文件系统与 SQLite 不构成分布式事务；失败可能留下未引用快照，暂不擅自回收。
  项目/草稿级联删除只保证元数据引用清理，磁盘快照回收需要独立的安全生命周期设计。
- 消息提交与草稿消费不属于同一跨端口事务；异常退出窗口可能恢复已提交前的草稿。
  不会自动发送恢复内容；突发退出一致性仍是后续缺口，不以正常关闭重开代替证明。
- 已有旧候选全量测试进程仍未终止；本专项结果不覆盖它，也不抹除先前两个分配断言失败。

#### 当前候选验证（2026-10-18）

最终产物根：`artifacts/attachment-drafts-20261018`。旧候选的活跃二进制未被覆盖。

| 检查 | 结果 |
| --- | --- |
| 修改前真实选择附件后导航恢复 | 0/1，附件集合为空，已保留失败 TRX |
| 最终完整解决方案 Debug 构建 | 通过，0 警告、0 错误 |
| 最终 App 相关专项 | 234/234，通过，无跳过 |
| 最终 Infrastructure 完整项目 | 272/272，通过，无跳过 |
| 84 个改动 C# 的语法/单类型/规模 | 通过，最大文件 494 行、最大方法 58 行 |
| 改动 C# whitespace、AXAML 规模及 `git diff --check` | 通过 |

最终 TRX（不叠加重复运行的数量）：

- `results/synchronized-app/wzheng_WZ-MINIPC_2026-10-18_09_23_43_net10.0.trx`
- `results/synchronized-infrastructure/wzheng_WZ-MINIPC_2026-10-18_09_25_22_net10.0.trx`

App 证据包括导航/独立生产组合重开后的真实发送、原文件删除后快照恢复、缺失与
同长度篡改快照拒绝 HTTP 请求、授权变化禁发、显式移除保留新正文、延迟读取与
新选择竞争、CAS 冲突及丢失确认恢复、准备期间同路径重新选择的新身份提升，
以及明暗主题实际关闭覆盖层的恢复按钮。其余用例覆盖受影响的对话/取消/历史、
无项目、失败分类、审批和关闭行为；不是完整 App 或完整解决方案回归通过声明。

Infrastructure 另覆盖 UTF-8/字节/数量边界、源元数据与快照路径安全、外部变更、
junction、取消/人工时钟超时、调用者修改排队输入、相同身份不可变、事务失败与
多作用域 CAS 回滚、任务归属拒绝、级联及版本 15 向前迁移失败重试。
中间测试证实 `InvalidDataException` 不继承 `IOException`；读取边界已显式处理，
保留不可用标签而不是让整个草稿消失。未放宽安全或性能断言。

未修改已有用户窗口、真实配置、审批策略或发布包；未提交、推送或发布。
<!-- /source: ATTACHMENT_DRAFT_ACCEPTANCE.md -->


---

<a id="attachment-history-acceptance"></a>

## ATTACHMENT_HISTORY_ACCEPTANCE.md

<!-- source: ATTACHMENT_HISTORY_ACCEPTANCE.md -->
### 文本附件历史与恢复

#### 设计与工程自检

- Domain 保存任务、消息所属的附件元数据；Application 定义快照存取端口。
  Infrastructure 负责文件和 SQLite；App 只协调端口及展示，不直接读写文件。
- 复用任务产物根目录。附件内容写入文件，SQLite 只存身份、顺序、显示路径、
  字节数和 SHA-256；不存原始绝对路径，不重新读取已变化的源文件。
- 选择仍经过既有授权 reader；历史快照不增加源目录或工具授权。快照读写拒绝
  跨任务、跨消息、路径穿越及链接，保留每条消息十个、每个 128 KiB 的边界，
  支持取消与超时。损坏或缺失时拒绝发出缺失上下文的模型请求。
- 快照先原子落盘，再将任务、消息和附件引用放在同一 SQLite 事务提交。
  文件系统与 SQLite 不构成跨资源事务；失败/崩溃可能留下未引用快照，不擅自
  删除可能已提交的文件，也不声称具有跨资源原子性。
- 历史附件仍是用户消息中的不可信数据，不能升格系统指令或重放工具操作。
  恢复仅加载元数据；实际发送时校验快照并受模型上下文预算约束。
- 新职责独立成文件；不以 partial 扩大 WorkspaceViewModel，不突破单类型、
  500 行文件及 80 行方法上限。
- 验证首发、后续多轮、原文件变化/删除、独立生产组合重开，以及缺失/篡改、
  跨任务/消息、链接、超限、取消及事务失败。测试使用临时数据和 loopback，
  不访问用户窗口、真实凭据或付费模型。

#### 实现与运行边界

- 发送前将选中的 UTF-8 内容保存为任务/消息/附件 GUID 所属的不可变快照；
  显示路径只作为数据，不用于定位原文件或产物。路径来源仍是受信任的任务产物端口。
- 版本 14 向前迁移添加附件元数据表和消息/任务复合外键，并扩展任务失败原因的
  明确整数白名单。新错误有本地化恢复提示；不保存或展示底层异常、原路径或凭据。
- 任务、消息和全部引用同事务提交。独立追加消息也使用事务；拒绝外来任务/消息、
  非用户角色、无项目文件输入、重复引用及超限数据。失败不清空输入和待发附件。
- 历史显示只需元数据。实际请求按所属用户消息恢复不可信 JSON 文本，不重复添加
  到最新一条用户消息，不转成系统消息，也不重放工具调用。
- 历史附件水合预算由模型上下文和输出限制估算，并设置上界；较旧附件无法容纳时
  明确标记省略，不读取其文件。最新用户消息的附件保留既有单条上限，最终仍由 Agent
  的上下文窗口处理。该估算不是精确 tokenizer，也不保证全部旧附件始终进入请求。
- 缺失、长度/哈希变化、非 UTF-8 文本及链接均拒绝正在使用的快照；本次请求在调用
  provider 前失败。恢复正确快照后必须由用户手动重试，不能自动重新执行。
- 所有文件操作在后台进行，使用独立取消源和三十秒超时。人工时钟测试在路径解析
  中推进超时，证明超时后不会打开/创建文件或发布迟到结果；取消同样传播。

#### 当前候选验证

| 检查 | 结果 |
| --- | --- |
| 修复前真实首发 → 第二轮复现 | 0/1，第二轮缺少附件的预期失败 |
| 首次修复及既有发送/历史专项 | 15/15 通过 |
| 最终完整 solution Debug 构建 | 0 警告、0 错误 |
| 最终 App 专项 | 215/215 通过，无失败/跳过 |
| 最终 Infrastructure 全项目 | 245/245 通过，无失败/跳过 |
| C# 语法、单类型、文件及方法规模 | 64 份工作区变更 C# 通过；最大文件 493 行、最大方法 58 行 |
| 指定文件 whitespace 与 `git diff --check` | 通过；已变更 AXAML 的 500 行上限检查也通过 |

最终报告在 `artifacts/attachment-history-20261018-final/results/synchronized-app`
和 `results/synchronized-infrastructure`。此前重叠专项不与这些数量相加。
最后一次 whitespace 调整后重新构建并运行上述专项，没有覆盖活跃回归的二进制。

App 使用真实临时文件、实际 SQLite、生产 Agent/provider 与 loopback HTTP：
首发、后续多轮、修改/删除原文件、任务 A/B 切换和独立生产组合重开均被验证。
缺失或同长度篡改快照阻止后续 HTTP 请求，安全错误原因真实持久化；修复快照后
手动发送恢复成功。另有保存失败保留输入、伪造端口结果拒绝、取消迟到读取、
历史预算省略、无项目/角色/归属拒绝及既有工具历史不重放用例。

Infrastructure 覆盖顺序、空文本、Unicode、精确十文件/128 KiB 边界、超限、
无效文本、任务/消息/重复引用、丢失/篡改、读写 junction 拒绝、取消/人工时钟
超时、事务失败回滚、复合外键、级联和向前迁移失败后的重试。

保留中间失败证据：首次扩展 App 的篡改分支未映射到附件错误，原因是
`InvalidDataException` 并不继承 `IOException`；显式覆盖该类型及解码失败后，
最终损坏/缺失及恢复分支均通过。新增合法错误类别 15 后，旧数据库拒绝测试的
非法值改为 16，并增加 14.5 非整数拒绝，不放宽为任意数值。

#### 完整回归及剩余工作

当前候选的全量回归由 session 11377 排在草稿候选 PID 9992 之后，使用独立的
最终产物根和 `--no-build`。已核实旧进程仍活跃，没有因观察超时停止或重启。
草稿候选已报告未修改的 tool-progress 与 sidebar-filter 分配断言失败；它们不是
附件候选的通过证据，也没有因此修改无关实现或放宽性能断言。全量终态仍待补充。

以上不是系统原生选择器点击、原生 EXE 关闭重启、付费模型或用户窗口截图。
未操作已有用户窗口、数据、审批策略或已发布 EXE；未提交、推送或发布。
该历史检查点尚未实现未发送附件草稿；后续实现与新候选验证见
[ATTACHMENT_DRAFT_ACCEPTANCE.md](CONVERSATIONS.md#attachment-draft-acceptance)。图像/PDF 多模态、PR/插件等缺口继续保留在功能清单。
本专项不缩小原始目标，也不代表整个功能对齐目标已经完成。
<!-- /source: ATTACHMENT_HISTORY_ACCEPTANCE.md -->


---

<a id="attachment-selection-isolation-acceptance"></a>

## ATTACHMENT_SELECTION_ISOLATION_ACCEPTANCE.md

<!-- source: ATTACHMENT_SELECTION_ISOLATION_ACCEPTANCE.md -->
### 附件选择的上下文隔离与取消

#### 范围（2026-10-18）

这是功能优先目标下的安全修复，不是外观复刻。起点为提交 `991b796` 加本轮工作区改动。
附件选择器或 reader 尚未结束时，切换项目/对话、清空或关闭工作区，旧结果不得进入
新的输入上下文；底层错误文本不得原样显示。

修复前新增八项用例全部失败：清空和无项目导航仍收到旧项目附件；销毁未取消端口；
选择/读取异常直接显示合成的 Authorization/Bearer 内容。报告保留在
`artifacts/functional-parity-20261018/results/attachments-before.trx`。

#### 设计和安全自检

1. 行为归 App 的 `WorkspaceAttachmentManagerViewModel`，主工作区只通知选择和关闭。
   文件读取继续使用 Application 的 `IWorkspaceAttachmentReader`，不把文件 I/O 放进 UI。
2. 上下文包含项目 ID、主目录、授权目录快照及任务 ID。授权列表复制后交给 reader，
   防止原始可变列表在异步过程中改变读取依据；项目改名不误取消同一上下文。
3. 新的版本号在清空/导航/授权变更/关闭时使旧结果失效，同时向 picker 和 reader 传递
   取消令牌。等待有五分钟总超时，`WaitAsync` 防止不协作的端口阻塞当前命令和关闭。
4. 发送新任务时保留已捕获之后新增的下一条附件，保持已有原子发送语义；正常导航
   不携带旧附件。过期的成功、失败和清理回调不能覆盖后来选择的内容或状态。
5. 文件数量限制仍为十个，路径先规范化并去重；超限结果不部分发布。真实 reader
   继续实施授权、链接、大小、UTF-8 和二进制拒绝规则；本轮不扩大任何权限。
6. 只显示本地化、可恢复的错误指导，不显示端口异常消息。没有密钥或用户数据写入日志。
   十份相关 C# 源码通过单类型和规模检查，最大文件 490 行、最大方法 57 行。

没有数据库、Agent、provider 协议、工具审批策略或发布方式变更。每文件单类型和
模块依赖边界保留，正常调试仍为 JIT。

#### 真实链路与确定性覆盖

- 实际授权目录中的 UTF-8 文件经生产 reader 读取，进入生产任务/Agent/provider
  适配器的 loopback HTTP 请求，消息显示附件，任务和回答保存到独立 SQLite。
- 实际 reader 拒绝授权目录外的路径；拒绝后普通发送仍可完成，模型请求不含被拒绝
  的路径或附件上下文。
- 在选择器和读取两个阶段切换到无项目对话，释放旧结果，再实际发送普通聊天；
  请求中没有旧项目标记或附件上下文，任务仍为无项目。
- 明确验证清空、手动取消、销毁及工作区关闭传播取消；不响应取消的测试端口不会
  阻止命令结束。旧端口之后的异常不覆盖新附件。
- 人工时钟推进五分钟验证超时，无真实等待；项目/任务/主目录/授权根变更验证失效，
  同上下文刷新不误取消。变更后的原始授权列表不修改 reader 已捕获的快照。
- 重复路径、已有附件、picker/reader 结果超限、外来项目参数、无项目、非法超时、
  缺失/无权限/未知异常均有对应边界用例。
- 原有发送准备失败保留草稿、准备中新增附件、取消任务后保留下一条草稿的测试不变。

这些是生产组合、真实文件和独立存储、脚本化 loopback 模型以及确定性端口的证据，
不是付费模型、系统原生选择器点击或当前用户窗口的操作记录。

#### 已完成的验证

| 检查 | 结果 |
| --- | --- |
| 修复前复现 | 0/8 通过，8 项预期失败，未跳过 |
| 首轮修复与发送草稿专项 | 12/12 通过 |
| 扩展附件/取消/超时/边界及既有草稿 | 34/34 通过 |
| 真实文件/provider 链路、附件、无项目和真实输入框键盘专项 | 59/59 通过，未失败/跳过 |
| 完整 solution Debug 构建 | 0 警告、0 错误 |
| 十份相关 C# 单类型、语法和文件/方法规模 | 通过；使用 PowerShell 已加载的 Roslyn 4.11 重新执行无错误的检查 |
| 指定变更文件 whitespace 格式检查 | 通过；`dotnet format whitespace --verify-no-changes` |

专项结果存在重叠，不相加充当独立测试总数。原始 TRX 位于
`artifacts/functional-parity-20261018/results`。
增加真实提交测试时曾有一次测试代码引用错误的 `Author` 属性导致编译失败；改用
已有 `IsUserMessage` 后才得到最终的 59 项结果，不将编译失败算作通过。
最初审计的 `Add-Type` 发生程序集版本冲突，随后改用已加载 Roslyn 并检查语法诊断，
无错误重跑的结果才作为规模审计依据。

#### 完整回归与测试守卫

完整解决方案回归已经启动，初次运行暴露旧的 Headless 异步调度守卫失败。
三项审批测试改用 `DispatchAsync`，确保框架真正等待异步主体和清理结束。
弹框守卫扫描 Views 和整个 Controls 目录，明确要求模型选择和侧栏搜索两个已知
输入面板存在，并继续检查所有识别到的输入型弹框；不再要求历史上的固定三个面板。
没有删除审批行为断言，也没有放宽多步输入不能自动关闭的规则。

修正后的守卫与附件专项通过 41/41，无失败/跳过；其中包含三项真正等待完成的审批
控制测试。报告在
`artifacts/functional-parity-20261018-final/results/corrected-guards-and-attachments.trx`。
修正候选的完整 solution Debug 构建再次通过，0 警告、0 错误；十份变更源文件的
格式与规模复查通过。

初次完整回归已自然结束：Agent 123、App 4928/4930（上面的两个旧守卫失败）、
Visual 300、Browser 88、Infrastructure 204、Platform 142、Providers 542、Tools 494。
修正候选在前次进程退出后正常启动，现已自然结束；未修改的集合分配性能测试
`ObservableCollectionSynchronizationTests.WarmUnchangedSnapshots_DoNotAllocateOrPublishNotifications`
报告 4664 bytes，而断言要求 0。保留此失败，未放宽性能断言或擅改无关实现。
没有因为观察超时中止或重启前次运行。两次报告保留在两个产物根的 `results/full`。
修正候选结果为 Agent 123、App 4929/4930（上述一项性能失败）、Visual 300、
Browser 88、Infrastructure 204、Platform 142、Providers 542、Tools 494。
不能把专项或成功构建写成全套通过。
后续草稿改动使用独立产物根；本节两次旧回归不验证后续草稿候选。
测试进程使用独立产物目录，未操作已有用户窗口、数据、审批设置或已发布 EXE。

#### 尚未覆盖或尚未实现

- 原生系统选择器实际取消/关闭，以及不同主题、DPI 的实际点击仍未验证。
  对不响应取消的底层选择器，只保证取消信号和拒收结果，不声称强制关闭其窗口。
- 本轮仍只支持授权目录内的文本附件，不代表图像/PDF/原生文件多模态支持。
- 上下文隔离修复本身不解决历史持久化；后续的文本快照、多轮与独立组合恢复
  证据见 [ATTACHMENT_HISTORY_ACCEPTANCE.md](CONVERSATIONS.md#attachment-history-acceptance)。未发送附件草稿和原生重启仍待完成。
- 完整功能与其他缺口继续记录在 [功能对齐清单](OVERVIEW.md#functional-parity-matrix)，
  不以本专项或视觉相似度替代整个产品目标。
- 未提交、推送、发布或重新打包。
<!-- /source: ATTACHMENT_SELECTION_ISOLATION_ACCEPTANCE.md -->


---

<a id="attachment-thumbnail-acceptance"></a>

## ATTACHMENT_THUMBNAIL_ACCEPTANCE.md

<!-- source: ATTACHMENT_THUMBNAIL_ACCEPTANCE.md -->
### Image attachment thumbnail workflow

#### Delivered scope

The composer now displays a bounded image thumbnail in the existing shared
attachment chip for selected static PNG/JPEG/GIF/WebP inputs. Restored project
and projectless image drafts use the same row factory and renderer. Text and
unavailable attachments retain their ordinary removable file chips.

Sent messages now retain their selected immutable image source. Restored history
uses the same chip with a lazy, validated task/message snapshot source, including
projectless conversations. No history image bytes are read just to map its row.

JPEG EXIF preview rotation/mirroring is subsequently verified in
[IMAGE_ORIENTATION_ACCEPTANCE.md](CONVERSATIONS.md#image-orientation-acceptance), without changing original attachment bytes.
This is not complete image-input acceptance: PDF, full-size image viewing,
child-agent image sharing, other-format/native-image orientation acceptance,
native picker/EXE restart and paid-provider interpretation remain open.
No existing user window was operated or captured.

#### Design and safety review

- Application owns `IAttachmentThumbnailRenderer`; Infrastructure implements it
  with Skia. App only invokes the port and decodes its small PNG result into the
  Avalonia bitmap. Dependency direction remains unchanged.
- Rendering uses the already-selected immutable `AttachmentImage` bytes. It
  never opens the original path, grants a directory, invokes a model/tool or
  changes authorization/approval settings. Projectless input remains plain chat.
- The shared `SkiaAttachmentImageSource` retains source format, animation,
  dimensions, pixel-count, cancellation and full-decoding checks for both the
  original image decoder and the thumbnail renderer. The renderer also rejects
  metadata inconsistent with actual decoded dimensions/media type.
- A renderer-instance semaphore admits one source decode at a time. Work runs
  off the UI thread. The gate is retained until the worker actually exits, even
  if its caller stops awaiting it after cancellation.
- Thumbnail output is a static PNG, at most 96 pixels per edge and 64 KiB.
  Aspect ratio is preserved and small inputs are not upscaled by the renderer.
  Original bytes/metadata, provider payloads and database schema remain unchanged.
- The shared control loads only while attached, has a ten-second caller deadline,
  cancels on replacement/detach and rejects late results. It posts only bounded
  final presentation to the UI dispatcher; it never waits for a stopped UI loop.
- PNG signature/header dimensions must match bounded result metadata before the
  UI bitmap decoder runs. Bitmap ownership is local to the attached control;
  clearing, replacement and detach clear its source and dispose the old bitmap.
- There is no global image cache. A failed thumbnail falls back to the existing
  icon/filename/size/removal controls without changing attachment availability.
  No raw image content is logged or newly persisted in SQLite.

The existing application port contracts are reused for selection/persistence;
only image-to-thumbnail rendering needs a new port. New code remains one type
per handwritten C# file, below the file/method hard limits.

#### Interaction and rendering

AppAttachmentChip remains the single visual owner for composer attachments.
Its new image slot, 28-DIP sizing and stretch live in shared styles. The slot
reserves its layout while loading; image completion does not move labels or
removal buttons. Existing text-chip geometry is unchanged. The thumbnail is
noninteractive/decorative in accessibility; the chip retains its accessible
filename/size and the removal button retains its own name/action.

Four isolated Headless PNGs under
`artifacts/attachment-thumbnails-20261018/screenshots` cover light/dark and
removable/read-only chips. Both removable theme images were inspected directly:
thumbnail, readable labels and removal action fit without overlap. Tests verify
unchanged pre/post-load bounds and the shared 28-DIP image size. These are Skia
Headless renders, not screenshots of native desktop windows or a Codex comparison.

#### Validation

Candidate: `artifacts/attachment-thumbnails-20261018`.

| Evidence | Terminal result |
| --- | --- |
| Thumbnail renderer plus existing image-decoder checks | 31/31 passed |
| Full Infrastructure suite, including SQLite/image/draft boundaries | 344/344 passed |
| Final expanded App attachment/chip/projectless/draft checks | 82/82 passed |
| Complete solution build | 0 warnings, 0 errors |
| Changed/untracked handwritten C# shape audit | 168 files, no violations; maximum 499 file lines / 67 method lines |

Renderer tests cover all four source formats, aspect ratios, small/extreme-aspect
images, malformed and oversized actual headers, animated formats, inconsistent
envelopes, cancellation and recovery after a failed request. App tests cover
both themes, stable layout, fallback/removal, no reads while unattached,
replacement/detach cancellation, late completion, detach/reattach, invalid
renderer output, actual production DI and project/projectless draft restoration
without an original file. Draft-coordinator fixtures use a test repository;
the renderer is real, while controlled renderers isolate lifecycle races.

The first expanded App attempt stopped at compilation because its optional PNG
evidence writer used an obsolete save overload. It was corrected to the current
`PngBitmapEncoderOptions` API; no suppression or relaxed assertion was added.
Only `results/expanded-app-final` is the final expanded result.

After the older full run ended, the latest candidate was rebuilt and frozen at
`full-app-binary-identity.json`. Its only complete regression is session **95889**,
started without diagnostic hooks, with output in `full-regression.log` and
`results/full`. It was still executing when recorded; focused passes do not
establish its terminal result. Do not rebuild over these running binaries.

This full run has already reported another unchanged raw-counter case:
`SidebarFilterAllocationTests` for recent/no-matches measured **1,176 bytes**
against zero. That family does not yet use the isolated measurement helper.
Its exact cause is not established merely by the earlier standalone runtime
probe. Keep the failure, audit the remaining raw-counter boundaries and retain
their functional/zero-byte assertions; do not call this full candidate green.

No native interaction, release, commit, push, AOT, trimming or memory update was
performed. The overall Codex workflow-parity objective remains incomplete.

<a id="attachment-thumbnail-acceptance--sent-message-and-history-extension"></a>

#### Sent-message and history extension

Candidate: `artifacts/history-thumbnails-20261018`. This is separate from the
still-running frozen full candidate above; those binaries are not rebuilt.

- `WorkspaceViewModel` transfers the selected thumbnail source into its sent
  attachment row, without transferring a UI bitmap or rereading the source file.
- Production DI supplies `ConversationAttachmentThumbnailFactory` to the catalog.
  Only user-image rows receive a source; text and non-user attachments do not.
- Mapping does not hydrate image bytes. An attached chip invokes the source,
  validates task/message ownership and metadata, and reads exactly its immutable
  snapshot through `IConversationAttachmentStore`. The real store retains its
  internal path/link, byte-length, hash, format and image-decoding checks.
- An instance-local gate bounds active snapshot/read-render operations. Waiting
  requests honor cancellation. An ignoring port keeps the gate until it returns;
  the existing native decoder limitation still applies: bounded synchronous
  decoding may finish after the store's cancellation wrapper returns. This does
  not claim instantaneous native interruption or a process-wide decode gate.
- The same ten-second control deadline, stale-result rejection, small PNG bounds,
  fallback icon, read-only filename/size and bitmap disposal apply to history.
  Missing/corrupt snapshot previews do not discard message text or metadata and
  do not weaken the separate model-history validation before a provider call.
- There is no new model call, original-path read, tool permission, database
  migration, image cache, native-window operation or dependency reversal.

Validation so far:

| Evidence | Result |
| --- | --- |
| Final focused boundary/workflow/component plus allocation controls | 31/31 passed, no skips |
| Expanded App attachment/composer/history/navigation/model checks | 409/409 passed, no skips or abort |
| Complete solution build | 0 warnings, 0 errors |
| Changed/untracked handwritten C# shape audit | 174 files, no violations; maximum 499 file lines / 67 method lines |

The initial focused attempt has its own retained report: 26 passed / 3 failed.
Those three new test expectations mistakenly compared two separately created
image objects by identity. They now assert the exact renderer-returned instance;
no production or zero-byte assertion was relaxed. The final run also adds two
missing-snapshot component cases. Only `results/focused-final` is the 31-case
passing result; `results/focused` remains the initial failure evidence.

The two new production-composition workflow cases select through a controlled
picker and real readers, send to a local loopback provider, reopen the same
SQLite/artifact data in a fresh production DI container, and load the historical
thumbnail after deleting the selected source. Both project and projectless
conversations are covered; this is not a native EXE restart or paid-model test.

Six Headless PNGs under this candidate's `screenshots` cover both themes and
valid/corrupt/missing snapshots in `WorkspaceMessageView`. Light/dark valid images
and the light corrupt fallback were inspected directly. The shared chip binds
the thumbnail, keeps its filename/size and has no removal action in history.
Broader attachment/composer/navigation/model regression ended successfully under
`results/expanded`: 409/409 passed, no skips or abort. This remains a filtered App
run, not a complete regression of the new candidate. The old frozen full run is
still separate and already has a recorded allocation failure.
<!-- /source: ATTACHMENT_THUMBNAIL_ACCEPTANCE.md -->


---

<a id="composer-keyboard-acceptance"></a>

## COMPOSER_KEYBOARD_ACCEPTANCE.md

<!-- source: COMPOSER_KEYBOARD_ACCEPTANCE.md -->
### Composer keyboard submission acceptance

#### Ownership and behavior

The App UI owns this change. `AppComposerInput` extends the shared `AppInput`
wrapper and preserves TextBox templates, editing, wrapping, scrollbars and the
existing IME client. It adds no provider, tool, repository or operating-system
access and introduces no new dependency direction or approval path.

Only the focused, visible, enabled, attached and writable composer can submit.
Enter and Ctrl+Enter invoke the existing primary command only when submission
is enabled, the draft is nonblank, the command can execute and no preedit text
is active. `WorkspaceComposerView` disables submission throughout preparation
and execution, including the interval before an active run exists. The explicit
send/stop button retains its original behavior and approval policy.

Shift+Enter retains native newline editing. Other modified Enter gestures do
not submit. A key-down latch prevents repeated submission while Enter remains
held, including when eligibility becomes available or modifiers change during
that hold. Release, lost focus and visual detach reset the latch. There are no
external subscriptions or mutable global state.

The old chat-wide Ctrl+Enter send/stop binding is removed deliberately: the
shortcut is now scoped to the composer and never stops an active task. Focus
elsewhere in the chat or settings cannot submit its draft. Localized tooltip
and shortcut-help labels describe sending and newline separately.

#### Regression evidence

The unchanged baseline fails four of seven rendered integration cases: ordinary
Enter fails to submit in both themes/sizes, plain Enter changes the running
draft, and Ctrl+Enter cancels the active run. Ctrl+Enter idle submission and
Shift+Enter newline are the three already-passing baseline cases.

The final focused run passes 175 cases, including all 32 new cases. New tests
cover real MainWindow input, loopback Responses requests and persisted messages;
both themes and wide/compact windows; pending-save and active-run draft retention;
focus routing; repeat/modifier transitions; blank, readonly, disabled, hidden,
unfocused, detached and unavailable states; missing/disabled commands; and the
default TextBox IME client's preedit/commit/held-key sequence. Existing field,
wrapping, send-failure, editing and settings-shortcut cases remain enabled.

The IME case routes its active-preedit key event directly, then uses normal
headless text/key input after commit. Rendering Avalonia's static preedit
underline across independent headless UI threads initially raises a framework
thread-ownership error; the test avoids that unrelated renderer path rather
than changing production IME behavior. This checks routing, not native Windows
IME timing, candidate selection or preedit painting.

Two old test assumptions are corrected without weakening geometry: the source
guard now recognizes `AppComposerInput`, and the outside-composer routing case
focuses the chat header directly instead of looking for a deliberately
non-focusable header button. The initial focused 173/175 run is retained
separately; it is not reported as all-green.

#### Native production composition

External evidence and Python helpers, kept outside the repository, are under
`%TEMP%\MossAgent-composer-20261015`. The native candidate App SHA256 is
`64E32C47659F391815866F2775C46EAC16F4F609E8A6F81F2762782677911D5A`.
It matches the production App used by both UI test assemblies.

A fresh synthetic database contains one authorized, empty project and one
loopback-only provider/model. Its observer returns only text events, never tool
calls. No actual account, external API, browser, command or security setting is
used. The ordinary-privilege native App is the normal Debug/JIT build.

Eleven accepted Python PrintWindow captures cover Light/96-DPI maximized
1920x1032, restored 1280x820 and minimum 526x601 windows. Real native keyboard
and pointer input verifies:

- Shift+Enter leaves a two-line unsent draft with zero requests.
- Enter sends exactly one multiline request and displays the running state.
- Enter and Ctrl+Enter during that run preserve the next draft and connection.
- The first reply completes normally and leaves the next draft intact.
- Ctrl+Enter then sends that draft as the second request.
- Clicking Stop disconnects only that second request and preserves a new draft.
- The minimum-width shortcut flyout displays both send and newline instructions.

Independent `verify-native.py` checks all eleven nonblack, correctly sized,
identity-bound captures, both normal zero exits, clean stderr, exactly two
whitelisted requests and exact persisted message contents. Only tasks, messages
and conversation model preferences change; the other sixteen tables and exact
UI-preference bytes remain unchanged. There are zero tools/artifacts and no
project-file changes. The final task is cancelled, the completed first reply is
retained, and the unsent final draft is absent from stored messages.

The first capture attempt expected restored geometry but found a maximized
window. It fails explicitly and yields no accepted image; the subsequent capture
uses the observed actual bounds. Native automation also exposes lagging UIA
values during layout changes; passive pixels, observer signals and persisted
data establish results rather than stale accessibility text.

#### Complete validation

The complete solution run passes 6,175/6,175 cases across eight assemblies: App
4,530, visual 102, Agent 119, Browser 76, Infrastructure 199, Windows 142,
Providers 516 and Tools 491. There are no failures, skipped or aborted cases.
The App assembly finishes normally after 12m30s; its quiet interval is not
treated as a reason to interrupt/restart it. The 175-case focused subset is
already included in the total, not added to it.

The solution build has zero warnings/errors. Scoped whitespace verification,
one-type/file/line-limit checks, AXAML/resource parsing and scoped diff checks
pass. `verify-complete.py` independently checks all eight TRX files, unique
test identities, counters and the native/source audit records. Native captures
and both UI test assemblies share the recorded production App identity.

After that full run, a whitespace-only line split in the preparation test is
rebuilt and its two cases pass again. The full-run test assembly hash begins
`4F99205E1498`; the rebuilt test assembly begins `A51BF2871349`. The full-run
record preserves the former identity rather than attributing its results to the
later binary. The production App hash is unchanged, and these two rerun cases
are not added to the 6,175-case total.

#### Remaining scope

The fresh passive Codex reference is still a 481x601 conversation beside a
preview, not an equivalent maximized conversation. It does not certify current
whole-window pixel parity. Native Dark/high-DPI/real IME interaction and the
remaining sidebar, inspector and appearance fidelity gates remain separate work.
No AOT, JSON implementation, packaging, real user data or approval configuration
changes are made. Nothing is staged, committed, pushed, released or installed;
the overall product objective stays active.
<!-- /source: COMPOSER_KEYBOARD_ACCEPTANCE.md -->


---

<a id="conversation-draft-acceptance"></a>

## CONVERSATION_DRAFT_ACCEPTANCE.md

<!-- source: CONVERSATION_DRAFT_ACCEPTANCE.md -->
### Conversation drafts

#### Design and required self-check

- App owns editor state, selection, notices and write coalescing. Domain owns
  draft scopes and versioned text; Application owns the persistence port.
  Infrastructure implements it with SQLite. UI never accesses files or SQL.
- Existing conversation preferences describe model selection, not editor
  content, so drafts use a separate port. A scope is an existing task, a new
  conversation within one project, or a projectless new conversation.
- Draft persistence is local application metadata, not a model tool or a file
  permission grant. Task/project ownership and archive state are checked by
  the repository. Reads/writes have cancellation and bounded waits; stored text
  is bounded. Neither errors nor diagnostics expose draft content or exceptions.
- Versioned compare-and-swap prevents silent overwrites by another window.
  Batches are transactional, including clearing the new-conversation scope
  while promoting the next unsent text to a newly created task. Empty rows keep
  their revision so clearing a draft cannot introduce an ABA overwrite.
- A separate draft coordinator avoids growing WorkspaceViewModel beyond the
  500-line limit. Every new handwritten C# file contains exactly one type.
- Tests cover navigation, delayed reads, typing during loads/writes, reload,
  new-task promotion, actual send, failures/retry/shutdown and conflicting
  writers. SQLite tests cover forward migration/rollback, scope ownership,
  archived/missing tasks, cancellation, size limits and all-or-nothing writes.

#### Scope and evidence

Before the fix, both new navigation tests failed: project text appeared in
projectless chat and one task's text appeared in another task. Reproducer:
`artifacts/composer-drafts-20261018-before/results` (0 passed, 2 failed).

This change targets unsent **text** drafts. Attachment history, attachment
restoration and provider-native multimodal payloads remain separate incomplete
requirements; adding text persistence does not establish those capabilities.
Normal navigation still clears selected attachments through the existing
authorization-scoped attachment manager.

No native user window, paid provider or live credential is used for these tests.
Final candidate validation is recorded after implementation below.

#### Interaction and recovery semantics

- Editing is immediate; saved text is restored only if no newer local edit
  exists. Navigating keeps separate cached entries, so inactive completions cannot
  replace the current editor. Reads and writes use bounded waits even when a test
  port ignores cancellation. Disposal retracts late observable publication.
- Writes coalesce for 200 ms. Normal shutdown flushes them and reports failure
  rather than silently dropping local text. An abrupt process kill before an
  autosave commits can still lose the newest keystrokes; this is not a synchronous
  per-keystroke durability guarantee or automatic task replay.
- Scope ownership is verified at storage time. Empty text retains a revision;
  missing, archived or foreign task scopes cannot be used to overwrite another
  draft. Authorization roots and tool approvals do not change.
- Ambiguous failed writes are reconciled against their exact attempted revision
  and content. Ordinary retry cannot overwrite another window. The separately
  labeled local-draft action explicitly replaces all conflicting scopes in this
  window, still using compare-and-swap. Recovery operations are shared/serialized;
  competing actions cannot install stale baselines.
- The normal composer and failed-shutdown dialog expose recovery controls. Closing
  does not make conflict resolution unreachable or require force exit. Recovery
  only reads/saves draft metadata; it never repeats a provider/tool invocation.
- Secondary workspace fixtures now explicitly use isolated draft stores unless a
  shared repository is requested. This prevents the fixture's pre-seeded main
  composer from acting as an unintended competing real window. Production restart
  tests use the actual registered SQLite repository and independent composition.

The first HTTP assertions incorrectly searched raw JSON for Chinese text; they
were corrected to inspect decoded user-message fields. The initial expanded
navigation suite also retained an assumption that task and new-conversation text
are the same. It now seeds distinct drafts and verifies restoration of both,
retaining the original navigation, page, theme and no-request assertions.

A later non-UI concurrency run exposed dictionary enumeration changing during
scope creation. The coordinator now protects cache membership, captured updates,
write/recovery ownership and version publication with its instance-owned gate;
no repository operation is awaited while holding it. The final suite includes 64
independent rapid-scope/save workflows. This failure remains recorded in
`artifacts/composer-drafts-20261018/results/final-race-guards`; it is not a pass.

#### Final synchronized candidate validation

| Check | Result |
| --- | --- |
| Complete solution Debug build | Passed, 0 warnings and 0 errors |
| App workflow/component regression | 149/149 passed, no failures/skips |
| SQLite draft, ownership and migration regression | 70/70 passed, no failures/skips |
| C# syntax, single type and size audit | 36 changed/new files passed; maximum 497 file lines and 58 method lines |
| Scoped whitespace verification and `git diff --check` | Passed |

The final focused TRX files are in
`artifacts/composer-drafts-20261018-final/results/synchronized-focused`.
Earlier narrow runs overlap this scope and are not added to these counts.
The 149 App cases include the real loopback send, independent production
composition restart, actual shared composer/shutdown controls in both themes,
task/model preferences, automation execution, projectless denial boundaries,
attachment isolation and the new recovery/race tests. They are not a native EXE
restart, system selector click, paid-model test or user-window screenshot.

The previous attachment candidate naturally completed: App 4929/4930, with its
unchanged collection-allocation assertion failure (4664 vs 0 bytes); the seven
other projects passed. The draft candidate's full solution regression then
started from its immutable `artifacts/composer-drafts-20261018-final` root with
`--no-build`, storing reports under `results/full`. It remains live (PID 9992)
and has reported two unchanged allocation assertions in tool-progress lookup and
sidebar filtering. Neither previous failures nor a pending full run are treated
as a full-suite pass. Later attachment-history changes use a separate build root
and are not validated by this draft candidate's binaries.

No existing user window, real configuration, approval policy, release EXE,
commit or remote branch was modified. The overall functional-parity goal remains
incomplete. Unsent attachment restoration was not implemented at that checkpoint;
the later implementation and current evidence are in [ATTACHMENT_DRAFT_ACCEPTANCE.md](CONVERSATIONS.md#attachment-draft-acceptance).
Multimodal requests, PR/plugin workflows and other entries in
[FUNCTIONAL_PARITY_MATRIX.md](OVERVIEW.md#functional-parity-matrix) remain incomplete.
<!-- /source: CONVERSATION_DRAFT_ACCEPTANCE.md -->


---

<a id="conversation-followup-dispatch-acceptance"></a>

## CONVERSATION_FOLLOWUP_DISPATCH_ACCEPTANCE.md

<!-- source: CONVERSATION_FOLLOWUP_DISPATCH_ACCEPTANCE.md -->
### Queue configuration checks at atomic dispatch

Status: seventeen explicit paths have been merged with zero baseline conflicts;
original-checkout integration verification passes. Complete Debug regression
55845 is terminal: 8,121 pass and one queued-write audit assertion failure,
8,122 total, no skips or aborts. Its earlier complete
regression (session 97039) is terminal and
its freeze has ended: 8,101 passed, one allocation assertion failed, no skips,
8,102 total; 2,404 source/configuration and 2,035 runtime identities unchanged.
The provider completion slice is already integrated and recorded separately.

#### Confirmed race

Two deterministic production-App-DI/local-HTTP cases change project authorization
or disable the selected model while Queue attachment hydration is held. Both old
paths still issue a second request. The direct-baseline assertions fail with two
requests rather than one; no outside file, paid provider or existing window is
used. Earlier timeout-only tests are preserved but are weaker evidence.

#### Candidate behavior and ownership

The existing Application persistence port now accepts PreparedConversationFollowup.
Infrastructure checks its current project primary/roots, provider runtime settings,
model identity/availability/media requirements and agent availability inside the
same short SQLite transaction that consumes the entry and commits the user
message/Running status. A mismatch fails before changing the queue, draft or task.
The check contains no HTTP, filesystem, worktree process or other long-running I/O.
Attachment/worktree preparation remains outside the transaction under existing
cancellation and deadlines. App delegates through the port with no new SQL or
infrastructure dependency. There is no migration or reinterpretation of old data.

Saved queued model parameters, agent instructions and approval policy stay fixed;
display/default parameter edits do not replace them. Provider credential/header
comparisons use bound SQL parameters and are not logged or placed in Queue data.
This grants no new root, approval, browser, process or network permission.

The required design self-check and fifteen candidate paths are under
artifacts/queue-runtime-workspace-20261019/artifacts/followup-dispatch-races.
Its initial Debug build and 49 App/47 storage selection passed. The final cases
also change configuration immediately before claim, then restore it and manually
continue once without duplicating the message or clearing the next draft. Final
Debug/Release builds passed with zero warnings/errors; both configurations pass
51 App and all 723 Infrastructure checks. Fifteen integration source paths and
1,270 Debug/Release runtime paths/hashes match before/after. Structural checks cover
15 single-type C# files and 62 methods/accessors, maximum 55 lines, no violations.

A later test-only addition uses production Queue/HTTP/tool approval and real
Windows PowerShell parent/child processes in generated authorized directories.
Its initial case passes: cancelling one task stops its own tree and pauses its
remaining entry, while a second task's tree stays alive, completes after its own
release and retains its draft. AskEveryTime remains active and both tool requests
receive explicit test decisions. The final 17-file candidate bounds all fixture
processes and checks approval readiness. Its Debug/Release solution builds pass
with zero warnings/errors and both configurations pass 52 App and 723 Infrastructure
checks. All 17 source paths and 1,270 runtime paths/hashes were unchanged during
validation, recorded under artifacts/followup-dispatch-final. No existing window or
user process is targeted; this does not prove filesystem confinement.

The guarded integration manifest, original-file backups, builds and checks are
under artifacts/followup-dispatch-integration-20261019. Original-checkout Debug/
Release builds pass with zero warnings/errors; both configurations pass App 52
and all 723 Infrastructure checks. The rebuilt original-assembly CLI harness also
passes all four termination/recovery cases. All eight validation stages are terminal.
The full
workspace structural check covers 571 changed/untracked C# files, 29 AXAML files
and 3,119 methods/accessors, maximum 78 lines, with no violations; whitespace passes.
The isolated seventeen-path check covers 71 methods, maximum 59 lines.

All eight session 55845 reports and 2,411 source/configuration plus 2,035 Debug
runtime paths/hashes were checked unchanged; its freeze ended before later
guarded integration. The failed assertion and exact audit-identity correction are
recorded in QUEUE_TOOL_AUDIT_ASSERTION_ACCEPTANCE.md. No passing full run is claimed.

#### Separate allocation-test change

The original full run reports 3,480 bytes in the raw-counter task-lookup test.
Its specific cause is not established. The candidate uses the repository's existing
nonparallel AllocationMeasurement helper, with the same warmup/measured iterations,
identity assertions and exact zero-byte requirement. Real-allocation and invalid-
measurement controls remain required. No production lookup or GC setting changes.
See ALLOCATION_MEASUREMENT_ACCEPTANCE.md for the earlier independent counter probe;
that probe does not prove this particular failure has the same cause.

#### Remaining limits

This is not a complete App regression, native GUI or real-provider acceptance.
The original run's failure remains preserved. Source-review grant/worktree changes
at claim and changes after a committed run starts remain separate boundaries;
source-review execution still uses its existing runtime revalidation. No broader
atomicity or continuous revocation guarantee is claimed. Command filesystem
isolation, Queue browser/process ownership, Steer and other matrix gaps remain
open; this candidate cannot complete the overall product goal.
<!-- /source: CONVERSATION_FOLLOWUP_DISPATCH_ACCEPTANCE.md -->


---

<a id="conversation-followup-plan"></a>

## CONVERSATION_FOLLOWUP_PLAN.md

<!-- source: CONVERSATION_FOLLOWUP_PLAN.md -->
### Running conversation follow-ups

Status: Queue storage, production composition, commands and shared controls are
connected and have controlled settings-to-request-to-history evidence. Overall
follow-up parity remains partial: remaining live/malformed stream behavior,
native/live acceptance, default/alternate preferences and Steer are still open.
Responses/Anthropic missing-terminal failures and queued-tool approval boundaries
have now been integrated; see [PROVIDER_STREAM_COMPLETION_ACCEPTANCE.md](PROVIDERS.md#provider-stream-completion-acceptance). See
[CONVERSATION_FOLLOWUP_RUNTIME_ACCEPTANCE.md](CONVERSATIONS.md#conversation-followup-runtime-acceptance) for current evidence and limits;
[CONVERSATION_FOLLOWUP_STORAGE_ACCEPTANCE.md](CONVERSATIONS.md#conversation-followup-storage-acceptance) preserves the earlier checkpoint.

#### Reference and observed gap

The official [steering and queuing reference](https://learn.chatgpt.com/docs/prompting#steering-and-queuing)
was read on 2026-10-19. Steer adds input to the current run; Queue retains input
for the next run. Desktop queues support editing, reordering, sending and
deletion, with a default behavior and an alternate shortcut.

`WorkspaceViewModel` retains a separate stop action; the composer now routes Enter
and Ctrl+Enter to Queue during a real run. `WorkspaceRunState` owns cancellation and the selected
provider/model, while `AgentRunner` streams a provider turn and executes tools
before starting its next turn. There is no task-owned follow-up input port.

#### Required design checks

1. Domain owns bounded follow-up identities, task ownership and durable state.
   Application owns queue persistence and live-input ports; Agent consumes input
   through those ports. Infrastructure handles SQLite and forward migrations;
   App handles presentation without SQL, files or provider HTTP.
2. Reuse task cancellation, conversation attachment snapshots, model selection,
   scoped tool execution and atomic submission. Split send coordination out of
   the already 500-line WorkspaceViewModel before adding responsibilities.
3. A queued entry must retain its intended model/reasoning, selected agent,
   attachments and authorization scope. Revalidate provider/model availability
   and roots/grants before dispatch; never silently select another model or
   expand projectless/source-review tool permissions.
4. Queue claims, committed messages and draft consumption must be atomic and
   revision checked. Normal completion can dispatch an explicitly queued next
   message once. Failure, cancellation and application restart leave remaining
   entries visible and require manual continuation; never replay interrupted
   side effects. Scheduling and other conversations keep their own state.
5. Steer needs a safe live-input boundary, bounded buffering and persisted
   acknowledgement. It must not mean canceling and replaying a run or tool.
   Preserve tool call/result ordering and append input before its next model
   request. Closing, stopping and disposing must drain owned operations.

#### Acceptance order

- First deliver a real task-owned Queue path: enqueue while streaming, preserve
  the next draft, edit/reorder/delete, and dispatch once after successful completion.
- Verify settings/model/agent/attachment data through actual controlled provider
  requests, plus concurrent tasks, negative authorization, conflicts, cancellation,
  failure and independent persistence reopening with no automatic replay.
- Then deliver Steer through the Agent input port and prove that a controlled
  tool runs once while later guidance reaches the next appropriate model turn.
- Use existing shared controls/resources for keyboard and theme/viewport checks.
  Retain an explicit missing status for either behavior until its full path is
  implemented. Complete relevant tests, solution build and frozen full regression;
  keep native Windows and live-provider evidence separately labeled.

#### Verified Steer semantics (2026-10-19)

The official [App Server reference](https://learn.chatgpt.com/docs/app-server)
defines `turn/steer` as appending user input to an active in-flight turn. It
requires `expectedTurnId` matching that active turn, refuses an idle thread,
does not emit a new `turn/started`, and accepts no turn-level model, cwd,
sandbox-policy or output-schema overrides. This is a reference contract, not
an implemented MossAgent API or evidence that ordinary provider HTTP supports
mutating an in-flight request.

MossAgent must bind accepted guidance to the captured task/run identity and
persist an acknowledgement before showing it as accepted. Completion, stop,
shutdown and concurrent submissions must serialize with that acceptance. A
stale or idle run is refused without silently queueing or starting another run;
the user's input remains available to recover explicitly. Accepted text must
reach the next safe model-input boundary in the same run without replaying
prior requests or tools. Model/agent/root/approval selection stays immutable;
configuration changes require a separately selected subsequent run.

For direct provider streams, define the safe injection boundary and explicitly
state when the guidance takes effect. Do not interrupt a stream and replay its
side effects to imitate immediate input. Test acceptance while streaming and
while a tool/approval is pending; run-identity mismatch, completion/acceptance
races, concurrent tasks, duplicate submissions, failure/cancellation and restart.
Assert one run identity, ordered tool calls/results, one persisted input and
controlled-provider receipt. A failure after acknowledgement remains visible
and never automatically replays guidance after restart. Native input/status and
live-provider behavior need separate acceptance. Steer remains unimplemented.

#### Storage foundation (2026-10-19)

The foreground send flow now captures a `WorkspaceSendRequest` and delegates
preparation/start to `WorkspaceSendCoordinator`, preserving the existing draft
transaction, post-commit cancellation and task-owned execution behavior. This
reduces the workspace view model before adding queue responsibilities.

Schema 23 adds task-owned, revision-checked follow-up queues. Domain records retain
the selected model parameters, agent identity/prompt, approval policy, project/
worktree/root snapshot and file-backed attachment references. No provider keys or
attachment contents are added to queue payloads. Saving can consume a matching
task draft in the same transaction. Editing/reordering/deletion use the expected
queue revision; empty queues retain that revision to prevent stale resurrection.

The repository's dispatch operation atomically commits only the first queued
user message, Running task state and queue removal. Reusing a committed message
identity is refused. It invokes no provider, tool or process and is deliberately
separate from execution. At that storage checkpoint it was not registered in App.
The current runtime coordinator now performs model/agent, authorization,
source-review, worktree and attachment checks and handles committed cancellation
without replay. Controlled runtime evidence is recorded separately; persistence
tests alone do not establish those obligations or native acceptance.

The initializer preserves pending entries, marks committed Running tasks as
Interrupted and does not dispatch anything. Queue eligibility after successful
completion must be owned by the live run; failed/cancelled/restarted tasks require
an explicit manual continuation. Artifact retention/collection after deleting a
queued entry remains to be designed without deleting shared or committed data.

#### Runtime integration gate

Automatic continuation must be attached to an explicitly queued live run, rather
than inferred from a nonempty database queue. A restart, failure or cancellation
must leave that eligibility unarmed. An ordinary later message must not silently
resume old pending entries. Reconcile enqueue/completion races using the captured
run identity and committed task status, with a single task-owned dispatch operation.

Before committing dispatch, reload the saved provider/model/agent identities and
current project/authorization/worktree/source-review state. Preserve the queued
model parameters and prompt; refuse missing/disabled selections or revoked/changed
authorization without selecting a substitute. Validate attachment contents and
model compatibility. Use the queue revision observed during that validation for
the claim; do not execute a changed row based on an earlier validated snapshot.

Start the next run through the existing task runner/executor. A background queue
must update only its task's run/messages, preserving the foreground conversation,
model selector, draft and attachments. Completion notification must follow browser
release and removal of the previous run. Shutdown must prevent new claims and
drain owned in-flight preparation; cancellation after the database commit must
record the committed task as cancelled/interrupted, without issuing a provider
request or restoring the message to the queue for automatic replay.

Inline queue editing owns a separate local edit buffer. Revision conflicts keep
that input and expose an explicit refresh/resolve action. Reordering/deleting
compete with dispatch through the same revision, and must not hide a committed
message or change the active run's immutable selection. The current coordinator
and shared queue view implement this gate; dispatch races and native/live evidence
remain explicit verification gaps. Completion/queued-tool checks are tracked in
[PROVIDER_STREAM_COMPLETION_ACCEPTANCE.md](PROVIDERS.md#provider-stream-completion-acceptance); CLI process-termination recovery is
separate in [CONVERSATION_FOLLOWUP_PROCESS_RECOVERY.md](CONVERSATIONS.md#conversation-followup-process-recovery).

The dispatch-configuration race is now addressed through the prepared persistence
port and transaction-local configuration checks. It does not move filesystem or
provider I/O into SQLite transactions. [CONVERSATION_FOLLOWUP_DISPATCH_ACCEPTANCE.md](CONVERSATIONS.md#conversation-followup-dispatch-acceptance)
records the controlled repro, refusal/rollback, explicit recovery and actual
Windows queued-process cancellation evidence. It leaves source/worktree and
post-commit change semantics, native GUI, live providers and Steer explicitly open.

[QUEUE_BROWSER_RUNTIME_ACCEPTANCE.md](BROWSER_MCP.md#queue-browser-runtime-acceptance) adds the production Queue/Agent to actual
managed Chrome chain, including navigation cancellation while another task keeps
its approval/run/draft and service reconstruction without automatic replay.
It retains explicit gaps for browser creation cancellation, process crash/restart,
embedded/external/CDP combinations, manual takeover and native/live acceptance.
<!-- /source: CONVERSATION_FOLLOWUP_PLAN.md -->


---

<a id="conversation-followup-process-recovery"></a>

## CONVERSATION_FOLLOWUP_PROCESS_RECOVERY.md

<!-- source: CONVERSATION_FOLLOWUP_PROCESS_RECOVERY.md -->
### Queue recovery after process termination

Status: four controlled CLI cases pass against both the independent completion
candidate and the merged original checkout. This is OS process termination and production App DI, not Avalonia
window interaction, the production GUI executable, graceful close or termination
inside a database transaction.

#### Harness and identity

The separate JIT console harness under
`artifacts/queue-runtime-workspace-20261019/artifacts/queue-crash-probe` references
existing compiled assemblies, without rebuilding or editing src/tests. It uses
generated data roots, synthetic provider credentials and a local HTTP/SSE server.
Each child is created hidden, readiness is tied to a committed boundary, and
its executable identity is checked before terminating only that owned process
tree. Parent and worker verify the actual loaded App/Providers hashes against
the supplied assembly directory. No user data, existing window or browser is used.

The final identity-checked, explicitly gated run is probe-gated.log. App hash:
F80E50D84A8CD890154AF3D40FBEE63730EB7187CD22E38F564942296543D7B0
Providers hash:
19469DA4B91227863F975A237BE299C215F912D4ACC29E495ADB00AABDAA31E5

#### Established paths

Both project and projectless variants cover two boundaries:

| Termination boundary | Recovered state | Explicit recovery |
| --- | --- | --- |
| Entry committed while the first response is held | Interrupted task, one user message, one queue entry and the next draft retained; request count stays one | Queue Continue sends the entry once; task completes and the next draft remains |
| Queue dispatch committed and its real HTTP request is held | Interrupted task, two user messages, empty queue and the next draft retained; request count stays two | Queue Continue is unavailable; a manually entered new turn sends one new request without restoring or duplicating committed input |

Every final history contains crash-queued exactly once. The initial and restored
workspaces use production composition, repositories, Queue coordinator, agent and
provider adapter. Startup initialization marks interrupted execution without
replay. The first case's pending response is released only after the old process
exits, so cleanup cannot complete the old run instead of testing recovery.

The earlier two four-case runs also passed. The final run adds an explicit
enqueue acknowledgement before releasing the first response in dispatch cases,
so reaching the boundary does not depend on a fast enqueue winning a network
race. It retains loaded-assembly identity checks. Its generated SQLite/artifact directories
remain available for inspection. These four cases overlap workflow intent with
the xUnit reconstruction tests and are not added to their test count.

After integration, the harness was force rebuilt with ProbeApp pointing at the
original checkout's Debug App directory. All four cases pass again; the exact
record is artifacts/provider-stream-boundaries-20261019/queue-crash-probe.log.
Its parent and workers validated these loaded assembly hashes:

- App: 9FB7D34F02E3BE8756F0155EA82B1A87B10110694898F49AABA961F166C57D35
- Providers: BB27103F906F743C8D16BA7345BFE806E08FAF6DDB37C49E362E1466BA393051

The earlier independent hashes above are historical evidence, not the original
checkout's assembly identities. No src/tests source or original runtime output
was rebuilt by the harness build itself.

After the later dispatch-authorization integration, the original-assembly harness
was force rebuilt again and passes all four cases. Its current record is
artifacts/followup-dispatch-integration-20261019/crash-probe.log. Verified hashes:

- App: 22BD6CDF717B9CE31C83725C0AC5BB49F1F909F1A1A3861BBFDF5F1A03230CE1
- Providers: 69548380962D9C1E9A7DF998E16D51BB4F0AE07B859A69E9E8BC823EE2CAEEF3

Earlier hashes/results remain historical checkpoints; the validation records do
not establish native GUI restart or termination inside a transaction.

#### Remaining limits

No native picker, keyboard/window, screenshot, real provider, browser/process tool
or external side effect participates. No mid-commit atomicity or crash-before-
draft-flush guarantee is established. Native application restart and other
Queue dispatch races remain open; command filesystem isolation is unchanged.
<!-- /source: CONVERSATION_FOLLOWUP_PROCESS_RECOVERY.md -->


---

<a id="conversation-followup-runtime-acceptance"></a>

## CONVERSATION_FOLLOWUP_RUNTIME_ACCEPTANCE.md

<!-- source: CONVERSATION_FOLLOWUP_RUNTIME_ACCEPTANCE.md -->
### Conversation follow-up Queue runtime acceptance

Status: the production App now connects Queue storage, preparation, commands,
shared controls and run completion. The overall conversation-follow-up scope is
still partial: Steer and default/alternate-mode preferences are not implemented,
and native GUI restart/live-provider acceptance remains open. Controlled CLI
process termination is recorded separately from native GUI acceptance.

#### Ownership and behavior

Domain retains bounded immutable entry snapshots; Application's
`ConversationFollowupPreparer` rereads task, provider/model and agent availability,
checks saved model identity and reasoning, intersects image/file capabilities,
revalidates exact project/worktree roots through existing ports, checks source
review access and reads owned attachment snapshots. UI contains no new SQL,
filesystem, process or HTTP operation. Infrastructure remains schema 23; no new
migration or reinterpretation of old data was needed.

App composition registers the repository and preparer. Queue is available only
when its repository can atomically consume the same composer draft store. Enter
and Ctrl+Enter during a real run enqueue; Shift+Enter remains a newline. The
separate stop button still cancels. Each task has its own queue, preparation
cancellation and serialization gate. Preparation is registered before waiting
for the gate, so another foreground send cannot claim the task in that gap.

Enqueue saves attachment snapshots and atomically consumes only submitted draft
identities. New input is retained. Normal completion can advance only the chain
armed by an explicit enqueue on that owned run. Failure, cancellation, browser
cleanup failure, application reconstruction and shutdown pause continuation.
Appending to an old paused queue does not silently arm it. Restored entries need
manual continuation. Dispatch atomically removes the first entry and saves its
one user message and Running status; cancellation after that commit records
Cancelled without another provider call or putting the message back.

The queue shows saved model/reasoning and attachment counts and supports editing,
reordering, deletion, refresh and manual continuation. Editing uses a separate
buffer and the captured revision; conflicts preserve the buffer. Task switches
retain task-owned editors/drafts. UI publication uses the Avalonia dispatcher;
shutdown cancels and drains preparations before stopping owned runs. Controls
use existing wrappers and central appearance variants, with bounded scrolling.

#### Failure discovered through the runtime checks

A controlled Responses `response.failed` previously disappeared in the mapper,
allowing the run to become Completed and advance its queue. It now becomes a
sanitized ModelFailed; `response.incomplete` is also a localized failure. Failed
streams stop before later text/tool events. Nullable/malformed error fields use
safe fallback text. The reference schema documents the nested `response.error`:
https://developers.openai.com/api/reference/resources/responses/streaming-events#response.failed
The official completion guidance also separates failed/incomplete streams:
https://developers.openai.com/siwc/token-sharing-open-source/models-and-inference#3-wait-for-completed-inference

The later completion slice also rejects empty HTTP 200 and missing-terminal SSE
EOF in Responses and Anthropic, and stops reading after completion/failure.
Compatible Chat and Gemini already reject missing finish signals. Responses'
explicit `[DONE]` compatibility is preserved. The bounded fixtures and queued-tool
negative paths are recorded in [PROVIDER_STREAM_COMPLETION_ACCEPTANCE.md](PROVIDERS.md#provider-stream-completion-acceptance);
they do not establish every live provider/proxy's behavior.

#### Controlled evidence

The isolated source copy passed 215 App checks covering the Queue, model selection
and composer neighborhood, and all 607 Provider checks. Earlier failed runs are
retained: they revealed the missing response.failed mapping, a synthetic server
that stopped serving after a cancelled connection, two obsolete active-Enter
expectations and a two-editor test-fixture draft conflict. The fixture now drains
the first workspace before constructing the second; no conflict policy was relaxed.

After hash-checked merge of 39 files into the original dirty checkout:

- 21 Queue/runtime/safety/lifecycle/control checks passed in Debug.
- The six current control cases passed at 360/480/1000 widths and 600/800 heights,
  in light/dark variants, including project and projectless sessions. Four cases
  overlap the previous 21; these counts must not be added as unique coverage.
- Debug and Release complete solution builds passed with zero warnings/errors.
- Structural checks cover 558 changed/untracked handwritten C# files, 29 AXAML
  files and 3,070 methods/accessors; maximum method length is 78. Whitespace was
  repaired separately and its final result is recorded beside these checks.

These use production App DI, temporary SQLite/artifact directories, the real
provider HTTP adapter against a local synthetic server, and Avalonia Headless
controls. They prove saved model/reasoning and agent prompt reach HTTP, task and
draft isolation, edit/order/delete effects, cancellation/manual continuation,
provider failure, settings/root revocation, corrupted snapshot rejection,
revision conflict preservation, reconstruction without replay, and cancellation
after dispatch commit. They are not paid-provider, native picker/window, or real
process-restart acceptance. Rendered PNGs are Headless evidence; visual inspection
checks readability/overlap only, not pixel fidelity with Codex.

Artifacts: `artifacts/conversation-followup-runtime-20261019/` for original-checkout
builds, checks, screenshots and source identities; the independent-copy evidence
remains under `artifacts/queue-runtime-workspace-20261019/artifacts/`.

#### Remaining work

Audit remaining malformed/live stream cases, queue/manual-send races and
source/worktree access changes at the dispatch boundary, native GUI restart and
long queues. Saved approval/denial/cancellation now have queued-tool evidence in
the completion slice. A later real Windows Queue process case confirms that
cancelling one task kills its parent/child tree while another task completes and
retains its draft; task-owned browser isolation, broader command/terminal cases
and real-provider text/image input still need verification. Implement Steer through
a safe acknowledged input boundary without cancelling/replaying tools. The open
Windows command filesystem-isolation blocker is unchanged. This slice does not
complete the product goal, approve a release or replace the functional matrix.

#### Original-checkout Release and full-run status

The final original-checkout Release checks passed: 23 Queue checks, 607 Provider
checks and 147 Windows checks. Both full solution configurations passed with
zero warnings/errors after the final source changes. The structural/whitespace
check passed with no violations. That full Debug regression (session 14700) is
terminal: 8,070 passed, 15 failed, no skipped tests, 8,085 total. Its 2,401 source/
configuration and 2,035 Debug runtime paths/hashes were unchanged. These records
remain under artifacts/conversation-followup-runtime-20261019 and must not be
rewritten as a passing run.

That regression reported 14 visual selector failures (the Queue
adds another AppCard) and one attachment-navigation timeout. The latter did not
reach its HTTP-content assertions; it is not evidence of an attachment leak.
Its freeze ended after terminal manifests were verified. Ten explicit EOF/
completion, queued-tool and fixture paths were subsequently merged with zero
baseline conflicts; the original Windows PID fixtures were preserved. Both
solution configurations, Debug/Release App 38 and Providers 617, complete Debug
Visual 300 and four CLI process-recovery cases now pass under
artifacts/provider-stream-boundaries-20261019. Its complete Debug regression
(session 97039) is now terminal: 8,101 pass and one allocation assertion fails,
8,102 total, no skips. The prior visual/attachment failures did not recur. All
2,404 source/configuration and 2,035 runtime identities matched; the freeze ended.
The raw-counter failure and separate dispatch-validation candidate are recorded
in CONVERSATION_FOLLOWUP_DISPATCH_ACCEPTANCE.md; no complete-pass claim is made.

The subsequent seventeen-file dispatch integration is validated in the original
checkout: Debug/Release complete builds and each configuration's App 52 and
Infrastructure 723 checks pass, as do four rebuilt CLI recovery cases. The new
complete Debug regression, session 55845 under
artifacts/followup-dispatch-integration-20261019, is terminal: 8,121 pass and one
queued audit assertion failure, 8,122 total, no skips/aborts. All eight reports and
2,411 source/configuration plus 2,035 runtime paths/hashes were checked unchanged
before the freeze ended. The targeted audit assertion was subsequently corrected
without changing production execution or audit behavior, as recorded in
QUEUE_TOOL_AUDIT_ASSERTION_ACCEPTANCE.md. These results do not erase previous
failed full runs or establish a passing new complete regression.
See [PROVIDER_STREAM_COMPLETION_ACCEPTANCE.md](PROVIDERS.md#provider-stream-completion-acceptance) and
[CONVERSATION_FOLLOWUP_PROCESS_RECOVERY.md](CONVERSATIONS.md#conversation-followup-process-recovery) for exact evidence and limits.

An independent artifact-only check now connects production Queue dispatch and
Agent to real managed Chrome using a deterministic loopback model service.
Cancelling one pending page navigation pauses only its task's Queue; a second
task retains its exact click approval, completes a real click/snapshot and keeps
its draft. New production-service composition retains both histories/drafts and
requires manual continuation without replaying the cancelled navigation. See
[QUEUE_BROWSER_RUNTIME_ACCEPTANCE.md](BROWSER_MCP.md#queue-browser-runtime-acceptance); this is CLI/Chrome API evidence, not native
GUI, real-provider or EXE crash/restart acceptance. It does not change the frozen
meaning of regression 55845. After the later guarded cancellation integration,
the Queue/Chrome probe was repeated against original App assemblies; see
BROWSER_OPERATION_CANCELLATION_ACCEPTANCE.md for exact records and limits.
<!-- /source: CONVERSATION_FOLLOWUP_RUNTIME_ACCEPTANCE.md -->


---

<a id="conversation-followup-storage-acceptance"></a>

## CONVERSATION_FOLLOWUP_STORAGE_ACCEPTANCE.md

<!-- source: CONVERSATION_FOLLOWUP_STORAGE_ACCEPTANCE.md -->
### Conversation follow-up storage foundation

Status: partial implementation, not Queue/Steer product acceptance. The storage
port is not wired to App composition, a queue UI or automatic model execution.
The command filesystem isolation blocker remains open independently.

#### Ownership and safety review

- Domain owns follow-up/queue/dispatch values. Application owns persistence ports
  and bounded snapshot validation. Infrastructure owns schema 23, SQLite revision
  checks and transactions. App's foreground sender is split into a captured
  request and coordinator; no UI SQL/files/process/provider dependency is added.
- Reuse existing conversation attachment files, draft writer, task/message/failure
  writers, cancellation and task-owned run execution. A queued entry's ID becomes
  its committed user-message ID, permitting a database-enforced replay refusal.
- Queue operations run off the UI thread with a 30-second linked deadline. There
  are at most 16 entries, 1 MiB UTF-8 per serialized entry and 2 MiB per queue;
  existing prompt, attachment and draft bounds still apply. Binary/text attachment
  bodies stay in file-backed snapshots. Provider credentials are not captured.
- Projectless entries require ReadOnly, no roots and no worktree. Scope mismatches,
  archived/source-operation tasks, foreign attachment ownership, unsupported image
  input and stale revisions are rejected. These are storage boundaries, not proof
  that a saved root snapshot remains authorized at dispatch time.
- Before this port is connected to a runnable feature, an application coordinator
  must validate current provider/model/agent, roots/worktree/source-review access
  and attachment bytes. It must preserve task ownership, foreground drafts,
  approval and cancellation, including cancellation after commit. The repository
  itself has no model/tool side effects and does not establish that runtime gate.
- Handwritten files retain one type each and existing size limits. No source
  service locator, dependency reversal, AOT/trimming or user-data reset is added.

#### Recorded checks

`artifacts/conversation-followup-storage-20261019/focused/queue-storage.trx`:
33 deterministic SQLite/file-backed tests passed. They cover atomic draft/queue
saving, revision conflicts, edit/order/delete persistence, two writers/dispatchers,
single first-entry dispatch, unchanged foreground draft, message-write rollback,
committed-ID refusal, stale/active/archived/foreign task refusal, cancellation,
projectless restrictions, attachment ownership and model compatibility, bounded
payloads, real text/image snapshot reopening, and atomic/retryable schema upgrade
without deleting existing messages/drafts/tasks. Restart checks reopen persistence
and run initialization; they are not process-crash or native GUI acceptance.

The earlier complete Infrastructure run passed 705 tests before the final three
attachment/binding checks were added; it is not the final whole-solution result.
Complete-solution Debug and Release builds have zero warnings/errors. The same
33 storage and 30 App send/submission cases also pass in Release, with no skips;
TRX files are in `release/`. This is not a complete Release regression.

The focused App send/submission regression passes 30 cases in
`focused/send-refactor.trx`. The first whole-solution run is still in progress;
seven projects have terminal results, including all 708 Infrastructure tests.
Platform.Windows reports 145 passes and one PID-file sharing failure before its
process-reaping assertions. [PTY_PID_PUBLICATION_DIAGNOSTIC.md](TERMINAL_TOOLS.md#pty-pid-publication-diagnostic) records a
deterministic old-fixture reproduction and five passing checks using an
artifact-only candidate handshake. It is not yet a repository fixture repair.
Do not describe the whole-solution run as passed or terminal while App is running.

Structural auditing covers the current dirty candidate's 531 handwritten C# and
27 AXAML files, with 2,911 methods/accessors, a maximum method length of 78 and no
size/type/whitespace violations. `WorkspaceViewModel` is now 461 physical lines.
The mid-run identity check records the same 2,379 source/configuration files and
2,035 Debug runtime files as the run's initial manifest, with zero path/hash
differences. This is an intermediate check, not the final test/identity result.

An initial test build missed a namespace import; another early test assertion
compared an empty attachment list with the existing null-on-reopen convention.
Those failures were corrected, and the focused results above use the rebuilt
candidate. No failed run is described as a successful product workflow.

#### Remaining acceptance

There is no enqueue command, editable queue presentation, completion continuation,
manual-resume workflow or Steer input port in this slice. No controlled provider
request, approval/tool interaction, native screenshot or paid/live provider was
used to claim Queue acceptance. Do not change the functional matrix to implemented
until the settings-to-request-to-result workflow and failure/recovery branches are
actually connected and verified. See [CONVERSATION_FOLLOWUP_PLAN.md](CONVERSATIONS.md#conversation-followup-plan).

#### Runtime follow-up after this storage checkpoint

The frozen full run has now terminated: 8,053 passed and one failed out of 8,054;
5,391 App checks passed. Both final source and Debug runtime manifests have zero
hash differences. The preserved failure concerns PID-file publication in a
ConPTY test fixture. Its independently reproduced readiness handshake was then
applied with an original-hash check, and the real Windows project passed 147/147.
See [PTY_PID_PUBLICATION_DIAGNOSTIC.md](TERMINAL_TOOLS.md#pty-pid-publication-diagnostic) for the separate candidate and limits.

The next Queue runtime slice is now integrated in App composition; the earlier
storage-only statements above describe that checkpoint, not the current entry
points. See [CONVERSATION_FOLLOWUP_RUNTIME_ACCEPTANCE.md](CONVERSATIONS.md#conversation-followup-runtime-acceptance) for the controlled
settings-to-request-to-history evidence, UI and remaining acceptance gaps.
<!-- /source: CONVERSATION_FOLLOWUP_STORAGE_ACCEPTANCE.md -->


---

<a id="conversation-heading-acceptance"></a>

## CONVERSATION_HEADING_ACCEPTANCE.md

<!-- source: CONVERSATION_HEADING_ACCEPTANCE.md -->
### 会话标题与文本渲染验收

#### 2026-10-06 变更与所有权

- 当前 Codex 的单会话标题是文件夹图标与纯文字，不是固定宽度的标签卡片。
  MossAgent 原来的 50-DIP 左缩进、气泡图标、选中描边及额外加号与参考不符。
- 单个选中标签现在使用已有 `AppIconLabel`，不重复建立新控件。共享变体定义
  12-DIP 图标、16-DIP 图文间距、14-DIP 中等字重与 32-DIP 标题高度；共享表面
  使用 20-DIP 左缩进并移除横向分隔线。长标题在有限宽度内省略，不侵占右侧操作。
- 多个标签和新建草稿仍使用既有选择感知滚动条。关闭后台标签也会刷新呈现模式；
  右侧任务菜单增加使用既有关闭命令的入口，关闭标签不归档或删除任务。
  新对话仍可从侧边栏和任务菜单创建，草稿不丢弃之前打开的标签。
- 共享文件夹几何改为圆角，并保持统一描边。`AppWindow` 统一设置灰度文本抗锯齿，
  消除被动截图里文字的红蓝彩边，不在每页或控件实例重新配置。
  Avalonia 12.1.2 的该渲染选项只有附加访问方法，没有可用于 Style Setter 的属性字段，
  因此配置位于共享窗口初始化，而普通字体、颜色、尺寸仍由共享样式维护。

#### 实现自检

- 所有权是 App 的共享呈现和现有会话标签 ViewModel，模块依赖方向没有变化。
- 选择、新建与关闭复用现有目录和标签命令，没有新增运行端口或工具。
- 不改变授权、审批、超时、取消、输出预算或持久化；没有数据库迁移或自动重放。
- 本轮三个 C# 文件各一个类型，最长方法 46 行；涉及的 C#/AXAML 均不超过 500 行。
  `App.axaml` 保持 500 行。原生视觉控件与页面外观覆盖门禁随 App 构建执行。
- 新增 10 项回归，在修改前均失败。覆盖 980/1280/1920 宽度及明暗主题、有限宽度的
  长标题、图文居中、共享几何与渲染配置、选中/后台标签关闭、新草稿保留旧标签、
  锁定时关闭被拒绝，以及通过真实菜单键盘操作关闭但不归档。

#### 当前验证结果

- 相关标题、标签、图标及检查器定向测试 78 项通过。
  完整解决方案串行测试 1,525 项通过，其中 App 1,141 项。
- 解决方案构建 0 警告、0 错误。本轮三个 C# 文件的空白格式检查与
  `git diff --check` 通过，Roslyn 单类型、语法及方法规模审计通过。
- 新测试夹具首次组合运行遇到 Windows 临时目录句柄尚未释放的问题，清理改为
  与既有夹具相同的有界等待；随后定向和全量验证通过，没有修改无关进程用例。

#### 运行与截图证据

- 普通权限真实 App/MainWindow，使用此前子代理合成验收数据的独立副本；
  没有读取真实账号配置、发起模型请求或重跑工具。预览均正常关闭，退出码为 0。
- 外部 Python 仅通过 PrintWindow 被动捕获，不激活、点击、移动或缩放窗口。
  截图、脚本、宿主与合成数据库不加入仓库。
- 目录：`%TEMP%/MossAgent-header-fidelity-qa-20261006-01`。
  `moss-baseline-light-max.png` 与 `moss-final-light-max.png` 均为原始 1920×1032。
  当前 Codex 参考为 1936×1048，比较仅裁掉四周各 8 像素的窗口边缘，未拉伸。
  `header-final-comparison.png` 上方是 Codex、下方是 MossAgent，使用同名标题与同尺寸裁片。
- 标题采样区域中 RGB 通道差超过 15 的像素：Codex 为 0，修改前 MossAgent 为 593，
  修改后为 0。这只证明该区域彩边消除，不证明整个界面逐像素相同。
- `moss-final-dark-normal.png` 为原始 1280×820。Computer Use 在独立预览中实际点击
  会话、任务菜单及关闭标签入口，再新建草稿并返回原会话；已观察到多标签、单标题、
  旧会话仍在侧边栏和历史恢复。最终证据包括 `moss-final-dark-multiple-verified.png`、
  `moss-final-dark-restored-heading.png`、`moss-final-dark-draft.png` 和
  `moss-final-dark-returned-conversation.png`。
- 最初深色输入缺少截图几何，返回 `coordinate input geometry is unavailable`；
  重新绑定并取得截图后，唯一一次重试成功。即时可访问树有滞后，因此以稳定刷新和
  Python 返回的实际像素确认结果，不重发已经成功的点击。
- 浅色交互时 Computer Use 激活把最大化预览恢复到 1280×820；尺寸守卫拒绝该帧的
  1920×1032 声明，后续按实际尺寸记录。最大化最终截图在激活前取得。
  本轮没有修复或声称修复 Codex 的内置截图工具，也没有操作 Codex 原生界面。

#### 尚未完成的验收

- 参考与预览的正文、侧边栏项目和上下文来源不同，只有标题裁片使用同名内容。
- 未穷尽所有字体、DPI、悬停、焦点、设置页和检查器状态。图标几何与字体渲染
  仍可能有像素级差异；多标签呈现保留现有功能，不声称与 Codex 的未知多标签状态一致。
- 本轮是向全产品目标推进的已验证改动，不是全部功能或一比一界面还原的完成证明。
- 没有暂存、提交或推送，保留原有工作区改动。
<!-- /source: CONVERSATION_HEADING_ACCEPTANCE.md -->


---

<a id="conversation-preference-acceptance"></a>

## CONVERSATION_PREFERENCE_ACCEPTANCE.md

<!-- source: CONVERSATION_PREFERENCE_ACCEPTANCE.md -->
### Conversation preference and selection cleanup acceptance

#### Scope and ownership

- `WorkspaceConversationPreferences` coordinates per-conversation selection through
  an Application repository port. SQLite lives in Infrastructure, not the UI.
- `WorkspaceAgentPickerViewModel` shares `SettingsOperationLifetime` with model and
  preference reads rather than duplicating linked cancellation and deadline logic.
- Preference write completion shares one current-context guard and notice projection.
  Background-run startup is extracted from the near-limit workspace view model.
- Preferred Agent identity is independent of the currently available profile snapshot.
  Missing or disabled selections never turn silently into the default Agent.
- Agent-default reasoning is persisted separately from explicit user reasoning.
  Configuration updates affect subsequent turns, never an already captured request.
- Windows x64, ordinary JIT debugging and single-file publishing remain unchanged.
  No AOT or JSON source-generation changes are introduced.

#### Storage and lifetime boundaries

The transactional version-eleven migration adds `conversation_model_preferences`.
It preserves existing task metadata and message history. Preferences contain only
provider/model/Agent identities, canonical reasoning values and the default-source
flag. Agent defaults require an Agent identity and no explicit reasoning override;
both repository validation and SQLite constraints reject contradictory values.
Deleting provider, model or Agent configurations does not delete preferred identity.
Archived and missing tasks reject new preference writes.

Conversation reads have the existing 30-second deadline, cancellation and generation
checks. Late results cannot overwrite another conversation or an explicit selection.
Loading and failed reads block sending without clearing drafts; an explicit valid
choice can recover. Saves are serialized and coalesced per task. Normal shutdown
drains owned writes; noncooperative writes retain ownership until storage returns.
Failures show localized guidance without exception text. Explicit refresh retries
failed writes without silently replacing the current choice.

#### Regression coverage

The first complete regression run exposed eight failures. They covered disabled
Agent identity, next-turn Agent defaults, unavailable composer presentation, pending
task mutation readiness and disabled-model fallback. All eight required correction;
they were not treated as unrelated failures or weakened by removing assertions.
Task-mutation tests now await conversation preference readiness before asserting
their original target-specific send guard.

Focused checks cover:

- Independent conversation choices, stale reads, explicit-choice precedence,
  unavailable identity retention, safe failures and persistence retry.
- Read deadlines, caller cancellation, disposal and noncooperative write ownership.
- Serial/coalesced writes and preservation of existing task metadata and history.
- Agent-default and explicit-override round trips and contradictory-source rejection.
- Missing-Agent recovery, configuration refresh, task switching and actual
  settings-to-loopback request routing without paid provider calls.
- Fresh service-container restart after choosing a model/effort without sending
  another message, including Agent defaults and explicit Agent overrides.

The combined focused App run passed 82 tests before adding the additional
implicit-choice/disabled-model switching regression. The final full-solution run
passed all 5,058 tests across eight assemblies: App 3,576, Infrastructure 196,
Providers 499, Agent 83, Browser 76, Tools 483, Visual 8 and Windows 137. No tests
failed or were skipped. The final additional regression is included in that run.

Whitespace verification passed for the 23 affected C# files. Roslyn checks found
exactly one type per file, no syntax errors, no file over 500 lines and no method
over 80 lines. The largest file remains the workspace view model at 483 lines;
the preference coordinator is 221 lines. Existing unrelated changes are preserved
and no files are staged, committed or pushed.

Evidence is under the temporary root listed below: `solution-final-tests.log`,
the eight separately named files in `full-suite-final`, `source-final-audit.log`
and `format-final.log`. An attempted concurrent build encountered the running
test host's DLL lock; it is not a source compilation failure. Final build evidence
is recorded separately after the test host exits: `solution-final-build-clean.log`
confirms a successful complete solution build with zero warnings and zero errors.

#### Native restart check

On 2026-10-11 the external native QA host ran with ordinary permissions
(`QA_ELEVATED=False`), an explicitly isolated temporary data directory and synthetic
configuration. The existing saved maximized window state produced a 1920x1032
light-theme window at render scaling 1.

Using the real model menu and reasoning slider, select the Responses
`fixture-model` and high reasoning, then close normally without sending a message.
Restart the native host and select the same stored conversation. The restored model
profile identity, `selectedReasoning=高`, `effectiveReasoning=high` and
`canSendWithSelectedModel=true` matched the pre-close state. The original 12 messages
and Completed task status were unchanged. Both host processes exited normally
(`QA_UI_EXIT=0`).

Passive Python captures and whitelisted observations are outside the repository:

```text
C:\Users\wzheng\AppData\Local\Temp\MossAgent-conversation-preferences-20261011
  native-preference-before-restart.png
  native-preference-after-restart.png
  native-preference-before-restart.json
  native-preference-after-restart.json
  native-preference-first.log
  native-preference-restarted.log
```

This native check verifies model/effort persistence, not custom-Agent native menus,
all DPI/theme combinations, real provider accounts or full Codex pixel parity.
Custom-Agent default and override restart behavior has deterministic integration
coverage. The overall product goal remains active.
<!-- /source: CONVERSATION_PREFERENCE_ACCEPTANCE.md -->


---

<a id="conversation-steering-acceptance"></a>

## CONVERSATION_STEERING_ACCEPTANCE.md

<!-- source: CONVERSATION_STEERING_ACCEPTANCE.md -->
### Active-run text guidance

Status (2026-10-20): integrated into the main checkout through 96 explicit paths
after exact `candidate-r4` full Debug/Release validation. Each configuration passed
all 8,282 tests with eight completed reports, no failures/skips/aborts and no
source/runtime drift. Guarded integration preserved 40 original-file backups and
reported no conflicts. Original-checkout Debug/Release solution builds and each
configuration's 147 focused checks / four process recovery cases also pass.
Earlier failed/stopped evidence remains preserved. This text-only
slice does not establish full Steer or full-product completion.

#### Runtime and ownership

Entry: active composer “引导当前运行” or Ctrl+Enter. Enter continues to enqueue a
later run; idle Ctrl+Enter submits normally. Disabled alternate submission does
not fall back to another action. Existing repeat/IME/focus guards remain.

The command captures task/run identity and the draft revision, accepts bounded
text through `ConversationSteeringSession`, and atomically persists a receipt and
consumes that draft. At the next safe Agent boundary, a pending prefix becomes
ordered user messages in the same run. Tool batches finish before injection; no
tool/request is cancelled and replayed to imitate guidance. Run-start provider,
model, reasoning, agent, tool scope and approval policy stay selected.

All newly applied inputs must fit the next model context in full. Preparation
fails before a clipped request can be sent. Receipts mean durable local acceptance
or local boundary application, not confirmed server receipt. Required messages
remain required if more input arrives between adjacent boundaries.

Each run owns its gate, cancellation and acceptance state. Receipt IDs make exact
retries idempotent. An uncertain acknowledgement retains the same ID; reconciling
it preserves newer local draft text. Only the submitted draft entry pauses
background saving, so another conversation can save/start/accept concurrently.

#### Persistence and authorization

Schema 25 is forward-only. Text is limited to 32,768 UTF-16 units per receipt,
16 pending entries and 256 KiB pending UTF-8 data. History pages contain at most
64 receipts. No attachment bytes or large tool output are added to SQLite.

App rechecks the run's project roots, source grant/account/binding and worktree
guard before acceptance/application. The candidate also passes an immutable
authorization snapshot into the existing SQLite open/accept/apply transactions.
Under the writer lock, those writes compare project identity/primary/authorized
roots and exact source binding, grant and enabled account revision. Forced
post-precheck revocation therefore cannot consume a draft or append guidance.
Storage-only repository tests may omit that snapshot; production App sessions
always supply it. OS worktree validation remains outside SQLite. These checks
do not promise atomic immediate revocation of an already sent HTTP request.

Stop/failure/shutdown close acceptance and mark unapplied receipts Interrupted.
Database reconstruction closes abandoned runs and exposes receipts without
replaying them, starting a model request or repeating any side effect. This has
repository/service reconstruction and separate owned-process termination
evidence below. Native GUI restart and termination during a database commit or
tool side effect remain unverified.

#### UI and regression evidence

Evidence root: `artifacts/conversation-steering-20261020`.

- `app-Debug-composer-initial`: 45 passed / 6 failed. Receipt VMs were rebuilt on
  task updates; retaining them by task identity preserves expanded state.
- `app-Debug-composer-repair1`: 54 passed / 1 failed. A real global draft-write
  lock blocked another task; per-entry coordination then passed 55 cases.
- Window initial/diagnostic/repair1 logs retain four zero-height message viewport
  failures. Repair2 retained four unreachable nested-scroll action failures.
  Repair3 reached both actions but observed Queue before its asynchronous reload.
- `app-Debug-transaction-layout`: 82 passed, including four complete-window
  theme/size cases, actual keyboard controls and six forced authorization races.
- Latest focused `infrastructure-Debug-transaction-final`: 43 passed. Latest
  `app-Debug-transaction-final`: 12 passed, including eight post-precheck project,
  grant, credential-revision and binding races plus four complete-window cases.
- `structure-audit-final.json`: 2,260 C# files, 20,538 methods/accessors/lambdas,
  maximum file 496 lines, maximum method 78 lines, zero violations.

Headless screenshots cover 526×601 and 1280×820 in both themes, with four Queue
entries, two guidance receipts and a fourteen-line draft. A height-bounded composer
keeps message space below the floating context card. Stop and guidance controls
must scroll fully inside their ancestor viewport and remain keyboard reachable.
Long composer/history content remains scrollable. These are rendered Headless
frames, not native Windows interaction screenshots.

First full candidate Debug regression (`Full-Debug-current`, session 77358) is
terminal: 8,273 reported / 8,255 passed / 18 failed, no skipped or aborted tests.
Eight complete TRX reports and 2,485 source / 2,429 runtime hashes were reviewed,
with no identity drift. Failures comprise eleven missing publication-script
inputs, one Queue authorization notice, one obsolete Ctrl+Enter Queue contract,
one PR shutdown symptom and four project-root expansion workflows. Its failure
report remains historical evidence, not a result for a repaired candidate.

`candidate-r1` was copied with all 2,485 frozen source/config hashes checked, then
received two unchanged publication-script inputs and the notice repair. A
task-owned boolean carries a recognized authorization change through finalization;
Queue and guidance show the actionable scope error while remaining stopped.
No raw exception is displayed and no failed task is automatically continued.
Focused Debug checks now pass all 45 App and 11 publication-script cases, including
the original Queue failure. Publication tests use an isolated fake-dotnet sandbox,
not an actual release. The repair has 2,487 frozen source/config/script paths;
its 92 changed source/test paths are 53 additions and 39 modifications, no removals.
Structure audit: 2,260 C# files, 20,541 methods/accessors/lambdas, maximum 496/78,
zero violations. Its later full runs were explicitly stopped as superseded;
no full-run success in either configuration is claimed.

`candidate-r2` preserves the r1 snapshot and adds a reproduced history shutdown
repair plus the corrected keyboard integration contract. A blocked history read
holds the per-task operation gate; the production run-completion callback then
queues another refresh. Shutdown cancels that gate wait, which previously
escaped the loader's inner error handler and faulted task finalization. The
loader now covers registration/waiting as well as repository access, quietly
handles cancellation/disposal only during its stopped lifetime, and retains
localized retry state for ordinary failures. No run is replayed and no raw error
is exposed.

`app-Debug-history-repro-repair2` preserves the before-fix result: one passed,
one failed with `Task finalization failed` in the production App shutdown path.
`app-Debug-history-keyboard-repair2` then passed thirteen focused checks,
including that deterministic race, both PR context checks and actual composer
Enter/Ctrl+Enter behavior. This is controlled Headless/simulated-provider evidence;
it does not establish the precise cause of the old PR invocation retroactively.
Expanded failure-path and full-run evidence belong to this earlier checkpoint.
The r2 manifest contains 2,488 inputs and 94 changed paths
(54 additions, 40 modifications, no removals). Its structure audit covers 2,261
C# files and 20,566 methods/accessors/lambdas, maximum file/method 496/78 lines,
with no violations. At that checkpoint, no Steer source had been integrated into
the main checkout.

Both r2 expanded focused configurations subsequently passed 70 checks. A new
late-IO shutdown test nevertheless exposed a missing drain boundary: a read
error handled only outside the operation still faults `StopAsync`. The isolated
r3 pre-fix run (`app-Debug-late-read-repro-repair3`) preserves four passes and one
IO failure. r3 contains read failure inside its operation and gate cancellation
outside it, avoiding this regression without weakening ordinary failure hints.
`app-Debug-final-repair3` and `app-Release-final-repair3` each pass all 71 cases;
Release solution build has no warnings/errors. Structure remains 2,261 C# files,
20,568 methods/accessors/lambdas and maximum 496/78 lines, zero violations. The
source manifest still has 2,488 inputs / 94 changes. Full verification remains
required for this exact r3 identity; r2 success cannot validate r3.

The separate `process-probe` references existing candidate DLLs without rebuilding
src/tests. Its Debug r2 and Debug r3 runs each pass four actual owned-process
termination scenarios: project/projectless crossed with accepted-but-unapplied /
applied-before-held-response guidance. Production App DI, Agent, provider adapter
and SQLite recovery preserve the next flushed draft, close the old run, expose
the receipt and never automatically request a model. Pending input becomes
Interrupted without entering conversation messages; applied input stays once in
history. An explicitly entered new turn sends one new request and never reapplies
the old receipt. Parent and workers compare loaded App/Providers/Agent/
Infrastructure hashes with their supplied candidate directory. These are CLI
process checks with local simulated HTTP, no native window, tools, external side
effect or termination inside a transaction.

The first r3 Release process run passed three cases, then timed out waiting for
the second request. Read-only inspection found completed execution with guidance
already applied: the harness had not required the initial provider request to
arrive before accepting input. A parent/worker stdin handshake now waits for the
held first HTTP request, then permits acceptance, and only releases it after the
durable receipt. Both r3 gated configurations pass all four cases. The failed
log and metadata-only failure inspection remain; no product policy was weakened.

#### Current repair and validation gate

r4 addresses the four unchanged project-authorization tests, reproduced separately
as four failures on r3 Release. Exact steering authorization checks were running
on empty mailboxes and stopping the ordinary frozen-scope Agent when project
roots were only added. App now permits this compatible change only for an empty
boundary, retaining the original tool roots. Actual acceptance and pending-input
application remain exact-snapshot operations. Root removal/replacement, primary
directory changes, deleted projects and source/worktree access remain refused.
The original four UI tests keep all assertions, including path_denied, rejected
approval/audits, separately authorized future tasks and no replay on reopen.

The current 2,490-input manifest has 96 changes (56 additions, 40 modifications,
no deletions). Structure covers 2,263 C# files / 20,589 methods/accessors/lambdas,
maximum 496/78 lines, zero violations. Debug related App checks pass 79 cases,
followed by 69 expanded cases including the two new root-expansion guidance cases.
Release related App checks pass all 81 cases. Both Infrastructure configurations
pass 45 steering cases. Release child-file-change checks pass six cases.
The two configurations' gated process probes each pass four cases, with matching
loaded production assembly hashes in `process-probe-reviewed-repair4.json`.

Six superseded r1/r2/r3 full runs were deliberately stopped after their confirmed
regressions, with executable paths/start times checked before terminating only
owned test/App and VSTest process trees. All six are terminal failures with
partial App reports; their reported pass counts are not full-regression results.
Source/runtime identity remains unchanged for each. Records are
`superseded-processes-stopped.json` and `superseded-regressions-review.json`.
Those runs also retain timeout, routing and one draft-selection failure not
reproduced in the current narrow checks; resource load is not a proven cause.

Independent r4 complete Debug/Release verification is now terminal and reviewed.
Each configuration has eight distinct Completed reports: Agent 160, Providers
617, Browser 104, Windows 148, Infrastructure 788, Tools 666, shared visuals 300
and App 5,499, totaling 8,282 passing cases. No case failed, skipped or aborted;
both build/test exit codes are zero. Both builds have zero warnings/errors.
Each run's 2,490 manifest inputs and 2,429 runtime files have identical
before/after path/hash sets; only generated PNG frames are excluded from runtime
identity. The five-minute per-test watchdog did not report a hang or memory dump.
These exact reports supersede no historical failure retrospectively.

`integrate-repair4.ps1` passed its read-only preflight, then rechecked both report
sets/hashes, current candidate source/runtime and all destination Before hashes.
Its Apply run integrated 56 additions and 40 modifications without deletions,
preserved the original 40 files under `before-integration-repair4`, and recorded
Integrated / zero rollback conflicts in `integration-repair4.json`. The original
2,434-input snapshot and Git status are preserved. The scope review confirms no
change to unrelated source/config/script inputs and all 40 backup hashes match.

Independent original-checkout Debug/Release solution builds have zero warnings
and errors. Each configuration passes all 147 focused cases: App 97,
Infrastructure 45 and Agent 5, with three distinct Completed TRX reports,
zero failed/skipped/aborted cases and exit codes zero. All 2,490 expected input
paths/hashes match the validated candidate and remain unchanged across these
checks. These focused runs are separate from the exact candidate full regression.
Reports/logs are prefixed `Integrated-Debug-repair4` / `Integrated-Release-repair4`.

The separately copied `process-probe-original` references these built original
assemblies without rebuilding src/tests. Its first diagnostic build attempt
failed: its App path omitted the `win-x64` directory, and concurrent restore
reported a file-already-exists error. Those build logs remain. After a separate
restore and serial configuration builds using the actual output directories,
both configurations pass all four owned-process termination/recovery cases.
`Integrated-process-reviewed-repair4.json` checks the four outcomes and loaded
App/Providers/Agent/Infrastructure hashes against current original outputs.
These checks use production DI/SQLite and local simulated HTTP, no native window,
real provider, tool side effect or termination during a database transaction.
No staging, commit, push, publishing, elevation or system ACL change was performed.

#### Remaining scope

Delivery review on 2026-10-20 classified those 2,490 manifest entries as 2,455
source/config/script files and 35 old ignored build outputs under Application's
Artifacts subdirectories. Original manifests and hashes remain unchanged. Three
existing Application/Artifacts interface sources had also been unintentionally
ignored by Git; the delivery commit includes them and a narrow ignore exception,
while preserving generated-output exclusions. No product behavior changed. The
two interface EOF cleanups, separate PID test cleanup and focused reruns are recorded in
[DELIVERY_STATUS_AND_ROADMAP.md](OVERVIEW.md#delivery-status-and-roadmap), not retroactively part of the frozen candidate.

- Native Windows interactions, real provider responses and termination during a
  database commit or tool side effect remain unverified. Controlled CLI process
  recovery above does not establish those paths.
- Guidance currently accepts text only. Attached files remain in the draft with
  an explicit Queue alternative. User-configurable default/alternate submission
  behavior remains missing; the full reference feature is still partial.
- Command access outside authorized roots remains a separate unresolved product
  safety blocker. This candidate does not change Windows isolation or ACLs.
<!-- /source: CONVERSATION_STEERING_ACCEPTANCE.md -->


---

<a id="conversation-steering-plan"></a>

## CONVERSATION_STEERING_PLAN.md

<!-- source: CONVERSATION_STEERING_PLAN.md -->
### Durable input for an active run

Status (2026-10-20): text guidance is integrated into the main checkout through
96 explicit hash-checked paths, with backups for the 40 modified files. The exact
`candidate-r4` passed all 8,282 tests in each complete Debug/Release run, with eight
completed reports per configuration and no source/runtime drift. Original-checkout
Debug/Release solution builds have zero warnings/errors; each configuration's
147 focused checks and four controlled process recovery cases also pass, with
unchanged source inputs and reviewed loaded-assembly hashes. This remains a partial reference
feature; attachments, configurable submission behavior and native/live acceptance
are open. [CONVERSATION_FOLLOWUP_PLAN.md](CONVERSATIONS.md#conversation-followup-plan) records the reference contract.
Evidence lives under `artifacts/conversation-steering-20261020`; earlier failed
and explicitly stopped candidates remain preserved. See
[CONVERSATION_STEERING_ACCEPTANCE.md](CONVERSATIONS.md#conversation-steering-acceptance) for validation scope and remaining gates.

#### Ownership and boundaries

1. Domain owns immutable run/input receipts, bounded text and explicit states.
   Application owns persistence and live-input ports. Infrastructure owns a
   forward schema migration and atomic transactions; Agent owns safe injection
   boundaries. App owns draft coordination, keyboard commands and receipt display.
   No UI SQL, filesystem, HTTP, tool or operating-system dependency is added.
2. Reuse task identity, draft revision guards, messages, context budgets, existing
   cancellation, tool execution and task finalization. Input accepts text and the
   exact expected run ID; it accepts no provider/model/agent/root/policy overrides.
3. A per-run asynchronous gate must serialize durable acceptance, boundary
   application and closing. Cancellation stops new acceptance; already committed
   receipts remain visible. Stop/shutdown drain acceptance before finalization.
   A stale/idle run fails without Queue fallback, a new run or lost input.
4. Bound pending input to 16 entries, 32,768 UTF-16 code units per entry and
   256 KiB UTF-8 total; bound each history page to 64 receipts. Durable IDs make
   exact retries idempotent and reject identity reuse with different text/scope.
   Database operations run off the UI thread with a linked 30-second deadline.
   Store text/status only, never credentials or attachment bytes.
5. Each handwritten file retains one type and the existing file/method limits.
   Focused tests must cover concurrent accept/apply/close, stale identity, draft
   conflict/rollback, bounds, cancellation, migration retry and no replay on reopen.

#### Persistence foundation

A run registration belongs to one existing Running/Waiting task. One open run is
allowed per task; a closed run ID cannot be reopened. Pending receipts are separate
from committed conversation messages. Acceptance and matching draft consumption
commit together. A duplicate receipt returns its original acknowledgement without
consuming a newer draft; a mismatched duplicate fails.

At a safe boundary, a revision-checked pending prefix becomes user messages once,
after any preceding unpersisted assistant text. The same transaction updates its
receipt states. It never runs a provider or tool. Acknowledgement means durable
acceptance; boundary application means local model-input preparation, not proof
of server receipt or successful model processing. Recovery closes abandoned open
runs and marks unapplied input Interrupted in the same transaction as task/audit
recovery. It never inserts messages, reopens a run or dispatches work automatically.

#### Runtime integration gate

Storage is insufficient for shipping. Start the mailbox only after capturing the
run's initial history, and require the captured task/run identity on every UI
submission. Use the same run, model, agent, tool context and cancellation token.
Apply input before a subsequent model request, only after all results for the
current assistant tool-call batch. A stream with no tool calls must atomically
take pending input or seal acceptance before declaring completion; accepted input
can therefore produce another request in the same run without cancel/replay.

The existing context builder can clip recent user messages. Before committing
boundary application, prove that every newly accepted input is retained in full
in the next bounded request, or fail visibly without sending a clipped request.
Preserve provider continuation/tool-result ordering. A tool, request or browser
side effect must never be repeated to simulate steering. Failure/cancellation and
turn-limit exhaustion retain actionable receipt states; reopening is read-only
until an explicit user action, with no implicit replay into a later run.

#### Required evidence before integration

- SQLite success/denial/failure/rollback/concurrency and forward migration tests.
- Agent deterministic streams with input accepted during streaming, approval and
  tools; one run ID, one original tool execution and ordered provider receipt.
- Real App DI with simulated HTTP and actual file tools/audits, task switching,
  competing drafts, cancellation/failure, settings immutability and reconstruction.
- Shared localized controls, explicit next-boundary status and visible historical
  interrupted receipts; keyboard/theme/viewport checks and separate native/live
  acceptance. Relevant tests and Debug/Release complete solution builds.

Do not register a storage-only repository as a product capability or mark Steer
implemented. Command filesystem confinement remains an independent safety blocker.

#### Shutdown and keyboard repair review

The second repair candidate was copied only after validating every one of r1's
2,487 source/config/script hashes. It changes App history loading and its focused
tests; it adds no dependency, database migration, command or tool permission.
History uses the existing per-task operation gate, cancellation lifetime, bounded
repository page and UI dispatcher. Both operation registration and waiting for
the gate must be covered by the history loader's error boundary. Expected
shutdown cancellation/disposal must not fault the run-completion callback; other
read errors retain the localized retry state. The same file/type/method limits
apply; the r2 structural audit reports no violations.

The deterministic production-composition test blocks an actual history read,
lets an actual simulated-provider run complete and queue its final history
refresh, then shuts down. Before the repair this reproduces `Task finalization
failed`; afterward shutdown drains both reads without another provider request
or changed Completed status. Independent IO, cancellation and disposal errors
outside shutdown must remain visible and explicitly retryable. This reproduces
a cause consistent with the earlier PR test's symptom, not retrospective proof
of the original failure's exact exception.

The existing composer integration test must distinguish Enter Queue from active
Ctrl+Enter Steer. Keep actual focused keyboard input, task/run identity, no
cancellation, receipt persistence, provider input, once-only history insertion
and preservation of the next unsent draft. Do not remove the Enter Queue case.
Full independent Debug/Release gates, evidence identity and guarded integration
remain required after these targeted checks.

The subsequent late-read test forces an IO error after shutdown cancellation.
Moving only the error handler outside the operation allowed its drain task to
fault even though the loader handled the read error. r3 therefore keeps read
failure containment inside the operation as well as the outer gate-wait boundary.
The pre-fix r3 test run retains that failure; both configurations then pass all
71 relevant cases, including both shutdown paths and non-shutdown IO/cancellation/
disposal recovery. The revised structure audit remains within all limits.

#### Existing task authorization compatibility

The first full candidate regression finished with eighteen failures, including
four unchanged project-authorization workflow cases. A separate r3 Release run
reproduced all four: even an empty steering mailbox rejected an additive root
change before the ordinary Agent could exercise its original frozen tool scope.

r4 adds a separate read-only empty-boundary validator through the existing App
access guard. Application chooses it only after reading an empty pending mailbox
under the run gate. App permits an unchanged primary directory and a superset of
all original roots in this case; it never updates the task's tool context. Actual
acceptance and nonempty application keep exact authorization equality and the
SQLite transaction guard. Removal, replacement, deletion, worktree invalidation,
source grant/account/binding changes remain denied. A concurrent mailbox revision
invalidates sealing and repeats validation. Existing callers without this optional
validator retain strict behavior. No approval policy, tool or path validator is
changed.

Keep all four original UI workflow assertions unchanged, including the attempted
new-root write's path_denied result, rejected approval, two audits, future task's
separate access and no replay after reconstruction. Additional App cases cover
pending/new guidance refusal after adding roots and preserved drafts; repository
session cases prove the empty guard cannot authorize acceptance or application.
The r4 audit reports 2,263 C# files, maximum file/method 496/78, zero violations.
<!-- /source: CONVERSATION_STEERING_PLAN.md -->


---

<a id="conversation-submission-acceptance"></a>

## CONVERSATION_SUBMISSION_ACCEPTANCE.md

<!-- source: CONVERSATION_SUBMISSION_ACCEPTANCE.md -->
### Atomic conversation submission

#### Design and required self-check

- Application owns the submission persistence contract; Infrastructure commits
  the task, user message, attachment references and revision-checked draft updates
  in one SQLite transaction. App coordinates existing editor state through that
  port, without SQL, filesystem access or provider calls.
- Reuse conversation drafts, message/attachment persistence and the existing
  send-preparation cancellation lifetime. This is local metadata persistence,
  not a tool grant; projectless conversations retain no directory/tool access.
- Serialize draft writes while capturing and committing a submission. Preserve
  text and newly selected attachments entered during preparation or persistence;
  conflicts reject the submission before a model call. Never retry automatically.
- Keep attachment bytes in existing file-backed stores. SQLite atomicity does
  not make Git, snapshot files and the database one distributed transaction;
  failed operations can still leave unreferenced immutable snapshots.
- Keep one handwritten type per file and methods/files within AGENTS.md limits.
  Cover new/existing and project/projectless conversations, concurrent editors,
  rollback, cancellation, attachment promotion and independent database reopen.

#### Validation status

The four project/projectless and new/existing conversation reproducers failed
before the fix: a provider request could observe its already-committed user
message with the original draft still stored. The reports are retained under
`artifacts/conversation-submission-20261019/baseline`.

Initial focused verification passed 11 App cases and 49 Infrastructure cases
(the latter includes existing draft tests). This uses production composition,
temporary SQLite, real attachment snapshots, controlled HTTP/SSE and independent
composition reopening. The full Debug solution builds with zero warnings/errors.
Those focused results alone do not establish complete regression acceptance.

The first full attempt in `conversation-submission-20261019` was interrupted
after confirmed mixed-store fixture failures, with six completed project reports
and unchanged 2,358 source/config and 2,035 runtime files. It is not a complete
or passing regression. Some custom test editors intentionally pair a real task
repository with an in-memory draft repository; atomic submission must not alter
another editor's persistent draft. The port now explicitly checks support for
the exact draft-store connection factory. Production DI binds that pair; custom
nonpersistent editors retain their existing save path. Two added binding tests
prove production/foreign-store separation and preservation of the other draft.
The repaired submission and affected old workflows pass 22 focused App cases;
the new full candidate is `conversation-submission-bound-20261019`.

That complete Debug regression has now finished: 8,014 passed, six failed, 8,020
total across eight projects; no skips. App had 5,384 passed and six failed; all
seven other projects passed. The before/after manifests contain the identical
path/hash sets for 2,359 source/config and 2,035 runtime files. The failure records
and terminal JSON are preserved. This candidate is not a passing regression.

Two authorization UI cases waited for !HasActiveRun immediately after Send,
which can be true while atomic submission is still preparing. A separate replay
reproduced the two failures. Their completion waits now use !IsRunning, retaining
file, request, approval and audit assertions. Paused-submission cancellation tests
explicitly assert IsRunning=true and HasActiveRun=false at the commit boundary.

The other failures were two provider-answer and two approval-button hit checks.
Separate baseline replays passed all six provider and four approval cases, so the
historical failures cannot simply be dismissed. A new moved-button reproducer
deterministically fails with the old provider layout-only Flush: layout geometry
is current while InputHitTest still uses the previous rendered scene. A shared
headless frame flush now publishes layout and subsequent render work before the
same hit/actual-click assertions. This proves a harness defect, not that every
historical approval failure has the same cause. New focused and complete
regressions must establish the resulting candidate's status.

The resulting `conversation-submission-waits-20261019` candidate passes all 32
focused App cases, including the moved-button reproducer and all affected UI
families. Its complete Debug solution builds with zero warnings/errors. The
structural audit covers 513 changed/new C# files, 27 AXAML files and 2,875
methods/accessors, with no violations and a maximum method length of 78 lines.
Light/large and dark/compact pending-approval headless screenshots were inspected;
this is not native Windows interaction acceptance. The complete Debug regression
has now finished: all 8,021 cases pass across eight projects, including 5,391 App
cases; no failures, skips or aborts, with all eight TRX outcomes Completed and
process exit zero. Before/after path/hash sets are identical for 2,361 source/config
files and 2,035 Debug runtime files. `regression-terminal.json` and the original
TRX/logs are retained. The older six-failure candidate remains separate evidence.
The full Release solution build also passes with zero warnings/errors using JIT,
PublishAot=false and PublishTrimmed=false; all 32 corresponding Release App
focused cases pass. This is not a full Release regression or a published package.

#### Controlled process termination

`artifacts/submission-crash-probe-20261019/probe.log` adds four native Windows
child-process checks with production App DI and temporary SQLite: project and
projectless, each new and existing conversation. The worker persists its original
draft, commits through the production submission port, signals completion and
stays alive without disposing its service provider. The parent kills only that
owned process tree, opens fresh production composition and initializes recovery.
All four retain the submitted user message once, retain empty consumed drafts and
restore the task as Interrupted. Existing conversations retain their earlier
message. No automatic continuation is invoked.

This is real process termination after a confirmed commit, not a native GUI/EXE
restart test, a kill during SQLite commit, or a full running model/tool crash. It
has no provider request, attachment payload, Git operation or existing user data.
Pre-commit rollback, attachment promotion and UI cancellation retain their separate
controlled test evidence.

The UI retains local text and blocks sending on a revision conflict, offering the
existing explicit retry/keep-local path. A manual retry after resolving the
conflict is tested; it never starts automatically. Concurrent submissions of one
revision can commit at most once. SQL failures and archived/foreign scopes leave
task status, messages and draft revisions unchanged.

Cancellation before commit retains the original draft. Cancellation after commit
does not reconstruct an already-submitted draft: its saved conversation is
selected, marked Cancelled and displays a localized explanation. No model call
starts, and later work requires another explicit user message. The UI keeps
owning a noncooperative commit until it returns, preventing a second send.

No schema change is required; existing draft revisions, scopes and file-backed
attachment storage are reused. SQLite's atomic commit covers structured metadata
only. Debounced edits made after that commit, file/Git operations, unreferenced
snapshot reclamation and creation-time worktree crash recovery remain separate.
Separate native EXE evidence now covers a projectless send, real streaming
request and forced termination/restart without replay; see
[NATIVE_SUBMISSION_ACCEPTANCE.md](CONVERSATIONS.md#native-submission-acceptance). Native input later returns access-denied
errors and captured pixels are black, so draft typing, stop interaction and
visual acceptance remain incomplete. No mid-commit crash, live-provider or complete Release
acceptance is claimed. Release build and targeted validation,
when available, are recorded separately from full-product acceptance.
<!-- /source: CONVERSATION_SUBMISSION_ACCEPTANCE.md -->


---

<a id="conversation-timeline-acceptance"></a>

## CONVERSATION_TIMELINE_ACCEPTANCE.md

<!-- source: CONVERSATION_TIMELINE_ACCEPTANCE.md -->
### Conversation timeline acceptance

#### Scope: 2026-10-12

The shared conversation timeline groups the existing message instances into
user turns, intermediate execution activity and a final reply. Completed turns
collapse activity by default; running turns expand it. A user's explicit choice
survives stream updates and completion. Replies remain outside the disclosure.
The extracted message view retains attachments, Markdown, code-copy commands,
tool details and nested subagent dialogue.

Recorded message timestamps flow through the existing history mapper. A live
reply receives its actual completion timestamp after persistence, including
failure/cancellation paths when a non-empty reply exists. Labels use the local
time zone and localized today/yesterday/date and duration resources. Missing,
backwards or incomplete timestamps never invent a duration. Tool-only history
remains accessible without manufacturing a user bubble or reply.

#### Design self-check

1. App owns this synchronous presentation projection. Domain, Application,
   providers, tools and Infrastructure dependencies remain unchanged.
2. Existing conversation timestamps, repository ports, message instances and
   shared collection synchronization are reused. No new execution port is needed.
3. Disclosure and timestamps do not execute tools or HTTP requests. Authorization,
   approval, timeout, cancellation, output limits and audit remain in the existing
   pipeline. Dispatcher marshaling is retained for live timestamp publication.
4. Each handwritten C# file contains one type. After the incremental-refresh
   optimization, the timeline model is 141 physical lines and its turn model is
   83; all methods remain below 40 lines. The existing workspace model remains
   below the 500-line hard limit.
   Shared AXAML controls and semantic variants own appearance; App.axaml is 495
   lines, and the timeline controls do not introduce native visual controls.
5. Focused tests cover completed/live/orphan turns, missing/backwards timestamps,
   duplicate prompt text, stream identity, manual disclosure, activity availability,
   source replacement, removal, detachment, disposal and late updates. A real
   settings/model/MCP/SQLite fixture covers approval and denial plus reopen without
   model/tool replay. Headless rendering covers both themes and 280/740-DIP widths.

#### Simplification and reproduced failures

- Turn projection scans ranges in the existing source rather than copying every
  group into a list-of-lists. Activity filtering avoids its previous LINQ closure
  and array copy; turns without activity allocate no activity list.
- Derived visibility and command availability notify only when their values change.
  Two new tests fail before this adjustment: unchanged history republishes flags,
  and activity availability publishes four times instead of the two transitions.
  Both pass after the change. This is structural/allocation work, not a measured
  claim about whole-application latency or memory.
- Clearing and rebuilding active messages discarded turn identity and disclosure
  state in the full fixture. The catalog now uses existing SynchronizeWith; the
  original identity assertions pass without weakening their expectations.
- A native intermediate screenshot exposed empty response bubbles and ghost
  copy/tool controls after cancellation. Visibility bindings now explicitly target
  the parent turn rather than the message view's changed DataContext. Two negative
  rendered tests cover tool-only history with and without a user message.
- The final shared heading uses CardBorderBrush instead of BorderDefaultBrush.
  Offline samples from actual saved screenshots read RGB(237,237,237) for both
  the Codex reference divider and the final MossAgent light-theme divider.
- The temporary reflection-based timestamp contract assertion is replaced with
  direct property access. No database migration, stored JSON shape, model context,
  dependency, publish setting or source-generated JSON change is introduced.
  Debug stays JIT; publishing stays Windows x64 single-file without AOT.

#### Verification

- The first two functional contract tests fail against the previous UI. The first
  presentation regression passes 79/79. Expanded verification exposes six failures;
  after correcting catalog synchronization, a 49/49 run includes complete approval,
  denial, model/tool identity, restoration and layout cases.
- Final focused verification passes 74/74, including all 28 timeline cases and
  adjacent conversation, attachment, tool and subagent presentation regressions.
- The final serial solution run passes 5504/5504 across eight assemblies with no
  failed/skipped cases, including 3965 App cases. A later test-only whitespace
  correction is rebuilt with the solution and the same 74 focused cases pass
  again; production code and its native-verified DLL hash are unchanged.
- The solution builds with zero warnings/errors. Scoped whitespace verification and
  the repository whitespace check pass. The full Roslyn audit checks 1535 source
  files for the one-type, 500-line and 80-line-method limits; all pass. All 104
  AXAML files stay within the 500-physical-line limit.

#### Native screenshot and interaction checks

The normal-privilege production Debug app uses an external synthetic data copy,
not the user's database. Its managed DLL SHA-256 is
`F1C6749B9F99AA6684E3124636E194514CA4501A937C6FBA8E336C80AA75A6BF`.

- Passive Python capture produces non-black 96-DPI images at 1280x820, 980x720
  and maximized 1920x1032, with light/dark captures. The Codex reference is a
  passive 1920x1032 screenshot; no Codex UI input is performed.
- Clicking the duration expands the existing subagent execution details. Enter
  collapses them again without hiding the final reply. Cancelled/tool-only turns
  no longer render empty reply controls. Reply-copy entries remain visible.
- Theme changes use only the synthetic copy's appearance page. The original light
  preference is restored, and the test app closes normally. Its stdout/stderr are
  empty. Task/message/tool/artifact counts remain 1/11/6/0, and all four persisted
  table hashes are unchanged after intermediate and final app sessions.
- No new model request, approval decision or tool execution is triggered by native
  verification. Paid/production-provider calls and clipboard transfer are not
  newly tested here; deterministic fixtures retain execution and rendering coverage.

Logs, TRX, process identity, passive Python screenshots and metadata evidence stay
outside Git in
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-goal-fidelity-20261012-180814`.
The screenshot scripts are not added to the repository. No staging, commit, push
or publishing occurs. This slice does not complete whole-window pixel fidelity
or the broader product goal.

#### Incremental refresh optimization: 2026-10-12

This change remains an App-owned synchronous presentation projection, using the
existing messages and collection synchronizer. It adds no execution port, I/O,
database migration, stored JSON change, appearance variant or shared global state.
Authorization, approval, cancellation and output limits remain in the tool pipeline.
Debug remains JIT and publishing remains Windows x64 single-file without AOT.

- Collection add/remove/replace updates only affected subscriptions. Occurrence
  counts retain one subscription for repeated message references until the last
  occurrence leaves the source. Moves do not rescan or rebuild subscriptions;
  resets, source replacement and disposal still release the owned handlers.
- Unchanged turn projections retain the anchor dictionary and observable rows.
  Changed groups rebuild the current anchor mapping before publishing rows, so
  removed/reinserted turns cannot resurrect discarded disclosure state.
- Activity projection compares the existing filtered prefix directly. It creates
  a temporary list only after divergence; tail removals trim existing rows.
  Actual changes still use the shared identity-preserving collection synchronizer.
- Turn ranges still scan the source, and changed grouping reindexes its anchors.
  This is not an overall constant-time update or an application-latency claim.

Before production edits, all seven new allocation cases fail their ceilings;
the five new subscription behavior cases already pass. After the changes, all
seven allocation cases pass without weakening their limits. Warmed Debug samples
use synthetic messages without recorded timestamps:

| Case | Before bytes/operation | After bytes/operation |
| --- | ---: | ---: |
| Unchanged history, 32 turns | 5,504 | 0 |
| Unchanged history, 256 turns | 49,104 | 0 |
| Unchanged history, 1,000 turns | 206,816 | 0 |
| Invisible move and move-back, 256 messages | 13,232 | 144 |
| Invisible add and remove, 256 messages | 13,392 | 416 |

These measurements exclude initial binding, message/Markdown construction and
changed timestamp labels; they do not mean the real application allocates no memory.
A repository-external reference comparison uses the two saved original production
implementations: 20,000 deterministic mutations across 200 histories retain
equivalent projection, duplicate-reference ordering, row continuity, disclosure,
timestamps and command availability. Added regression cases also cover removing
and reinserting a turn, trailing response transitions, reset and independent owners.

The first external-output run passes 39/40 timeline cases; its sole failure is an
existing source-contract test locating the repository by walking output ancestors.
Using a separate ignored output beneath the repository passes 115/115 timeline
and adjacent tests without modifying that test or overwriting the running app.
After adding the two final reconciliation regressions, the complete App assembly
passes 3,979/3,979 cases and the visual assembly passes 8/8, without failed/skipped
tests. The fourteen added cases are included in that full run. All five touched
C# files contain one type and stay below 250 lines; scoped whitespace and the
repository diff check pass. The solution builds with zero warnings/errors.
Logs, original source copies,
TRX and the comparison project remain outside Git under
`%TEMP%/MossAgent-code-optimization-20261012`; isolated build output is ignored under
`.scratch/timeline-optimization-20261012`.
The existing native App session is not restarted. This slice does not newly
validate native pixels, live providers or the broader product objective.

Subsequent native verification of the optimized production build is recorded in
[BROWSER_CONCURRENCY_ACCEPTANCE.md](BROWSER_MCP.md#browser-concurrency-acceptance): running/cancelled/completed turns, independent
task switching, restored duration and real disclosure expansion retain their
messages and final reply. Normal and maximized light-theme captures and no-replay
restoration supplement the earlier evidence, not a full visual-state sign-off.
<!-- /source: CONVERSATION_TIMELINE_ACCEPTANCE.md -->


---

<a id="draft-background-persistence-acceptance"></a>

## DRAFT_BACKGROUND_PERSISTENCE_ACCEPTANCE.md

<!-- source: DRAFT_BACKGROUND_PERSISTENCE_ACCEPTANCE.md -->
### Draft background persistence

#### Scope and design self-check

The full App regression reached 3,812 passing cases and four already-corrected
settings-navigation failures before its test host was aborted for inactivity.
The incomplete case was `FileCardFidelityTests.TypographyAndMoreAction`.
The orphaned App process's native and async stacks show its fixture disposal
waiting for draft persistence after the Headless dispatch had returned.
This is a separate lifecycle dependency from the earlier incorrect
`Dispatch<Task>` overload. It does not prove that normal native shutdown hangs.

1. App owns scheduling and observable state; Infrastructure still implements
   persistence. Only pure persistence work moves off the UI context.
2. Reuse `IConversationDraftRepository` and `SettingsOperationLifetime`; no new
   storage, tool, permission or provider contract is required.
3. Existing cancellation, thirty-second operation deadlines, revision checks,
   attachment validation and bounded payloads remain unchanged. A storage
   failure must keep input and prevent silent successful shutdown.
4. Keep one type per file and existing size limits. Do not enlarge the already
   near-limit `WorkspaceViewModel` or alter UI markup.
5. Focused tests must let the UI dispatch return with a pending read or write,
   then prove completion, failure handling and cancellation without another UI
   pump. Notifications must still occur on the owning UI thread. Existing
   conflict, scope, attachment and shutdown tests remain required.

#### Reference feature inventory

Official [follow-up documentation](https://learn.chatgpt.com/docs/prompting#steering-and-queuing)
was fetched on 2026-10-18: steering changes the current run; queuing schedules a
later run and exposes editing, ordering, sending and deletion. These are still
MossAgent gaps, not features completed by this persistence fix. The work returned
to the reproduced lifecycle blocker before adding a partial queue entry point.

#### Implementation

`WorkspaceComposerDraftsViewModel` starts pure repository reads, delayed writes
and reconciliation through background tasks rather than UI-context yields.
The existing gate still protects draft snapshots and revisions. An idle writer
publishes its idle state while holding that gate; flush/recovery follows a
replacement writer rather than mistaking an earlier completion for a fully
drained batch. Shutdown does not require a captured UI continuation to persist
data. Observable notifications still post to the owning UI context, and failed
saves still retain input and reject silent shutdown.

The next full run also exposed an agent-selection failure. The constructor's
unawaited initialization and an explicit `LoadAsync` could overlap their model
and agent reload phases. An isolated follow-up adds `WorkspaceLoadCoordinator`
to serialize whole workspace refreshes, without coalescing an explicit refresh
into a pre-settings-change snapshot. This stays in App and reuses the existing
load pipeline/ports; it changes no tool permissions or repository deadlines.
New deterministic tests cover ordered execution, fresh snapshots and release
after failure; real SQLite agent/model selection covers ordinary and overlapping
refresh requests. No new behavior is claimed for interrupted tool execution.

#### Evidence

- Original full candidate: `artifacts/app-regression-20261018`.
- `continuation-app-stacks.log`, `continuation-async-stacks.log` and the retained
  test sequence identify the pending draft writer and fixture cleanup chain.
- `results/full/*10_35_22_net10.0.trx` records 3,812 passes, four failures and an
  aborted run. Those counts do not establish a complete App-suite result.
- The independent before candidate at `artifacts/draft-background-20261018`
  compiled but its pending-read test did not complete and the test host was
  aborted by the 90-second inactivity limit (`results/before.trx`). It has zero
  completed cases; this is an aborted reproducer, not four ordinary failures.
- `artifacts/draft-background-20261018-fixed/results/focused.trx`: **118/118
  passed**, no skips. Includes all four new background read/save/error/timeout
  cases, notification-thread checks, eleven file-card cases, draft/attachment
  scope/conflict/race tests and shutdown-related coverage.
- The fixed candidate's full solution build passed with **0 warnings and 0
  errors** (`build-solution.log`). `source-shape.json` checks 144 changed/untracked
  handwritten C# files, one type each, maximum 497 file lines / 67 method lines;
  `git diff --check` passes. The initial PowerShell audit attempt failed to load
  compatible Roslyn assemblies; this JSON was regenerated successfully by the
  ignored local .NET/Roslyn checker, not accepted from that failed attempt.
- Its full solution regression was session **57131**, rooted at
  `artifacts/draft-background-20261018-fixed`. The frozen App binaries' hashes
  are in `full-app-binary-identity.json`. It ended with exit 1: App completed
  **5,014 passed / 5 failed / 0 skipped**, with no inactivity abort. The other
  projects completed successfully: Agent 137, Visual 300, Browser 88,
  Infrastructure 328, Windows 142, Providers 600 and Tools 494. App failures
  comprise the 3,552-byte allocation check, original agent-selection assertion,
  two authorization scenarios and model-menu initialization described below.
  Allocation assertions have not been weakened or skipped. This is not a green
  full run, and isolated passing checks do not resolve its remaining failures.
- The load-serialization follow-up and expanded focused checks are isolated at
  `artifacts/draft-background-20261018-final`; they do not overwrite the running
  fixed candidate. Its `results/focused.trx` records **95/95 passed**, including
  all three load-coordinator cases, both real SQLite agent-selection refresh
  cases and the four background-persistence cases. This filter differs from
  the 118-case run, overlaps it, and is not an additive unique-test total.
- The final candidate's solution build passed with **0 warnings and 0 errors**
  (`build-solution.log`). Its `source-shape.json` covers 147 changed/untracked
  handwritten C# files, one type each, maximum 499 file lines / 67 method lines,
  with no syntax/size violations; whitespace checks pass.
- Its `results/allocation-diagnostic.trx` records **18/18 passed**, covering model
  lookup, sidebar filtering and progress allocation checks without changing any
  zero-allocation assertion. This isolated pass does not erase the current
  complete run's 3,552-byte failure or prove full-run allocation stability.
- Session 57131 is terminal and its eight TRX reports have been inspected. Neither
  the final nor the newer workspace-regression candidate inherits that result;
  their complete runs remain required. An observation timeout is not a reason
  to restart or overwrite a running candidate.

After native/async diagnostics and terminal test-host/runner results were
verified, only the two identity-checked orphaned App test executables were
retired. `retired-test-processes-continuation.json` records PIDs 24096 and 14368,
their exact candidate paths, creation times and reasons. Sessions 96000 and
25943 then ended with exit 1. No normal MossAgent/Codex window was affected.

Only this turn's 2,021,264,845-byte `continuation-app-heap.dmp` was removed after
the text diagnostics and SHA-256 metadata were retained in
`continuation-heap-metadata.json`. The earlier `original-app-heap.dmp` was not
removed. No raw heap dump was shared externally.

#### Expanded regression follow-up

The frozen full run later reported two light-theme project-authorization cases
returning `tool_failed`, and `ModelPickerMenuTests.SearchWithNoMatches` seeing an
unready catalog. Together with the earlier allocation and agent-selection cases,
these are the five terminal failures in its 5,019 executed App cases. The original
run and binaries remain unchanged; its runner completed with exit 1.

The model-menu case was reproduced through the real xUnit runner against the
unchanged final candidate: `artifacts/project-auth-probe-20261018/model-menu-xunit-0`
records a failure at the single-provider `ShowSearch` assertion. The test resolved
Workspace, which begins asynchronous initialization, but awaited only a separate
picker reload. Workspace's later reload can supersede that read; from a test
thread without UI serialization, their catalog publication can also interleave.
Both production-composition menu tests now await `Workspace.LoadAsync` before
exercising the picker. Every existing model, search, provider-group, reasoning and
empty-state assertion remains. This corrects test initialization, not production
reload cancellation semantics or the expected product behavior.

This follow-up is test-only: App.Tests owns it, it reuses the existing load entry
point, invokes no provider/tool operation, changes no permissions or persistence,
and stays within the existing type/file/method constraints. The candidate is
isolated at `artifacts/workspace-regression-20261018`; its solution build passed
with zero warnings/errors. `source-shape.json` audits 148 changed/untracked C#
files, one type each, maximum 499 file lines / 67 method lines, without violations.
Whitespace checks pass. Its `results/focused.trx` completed **99/99 passed** with
no skips, including the menu, load coordinator, agent selection, background draft,
authorization, allocation, settings navigation and file-card cases. Ten separate
xUnit processes then repeated the model-search case successfully; each has its
own TRX under `results/model-menu-repeat-*` and the aggregate is
`model-menu-repeat.json`. These repeated cases are not additional unique tests.

After session 57131 ended, a complete solution diagnostic run started as session
**89190** against this frozen candidate. Its three App/App.Tests assembly hashes
are retained in `full-app-binary-identity.json`; output is `full-regression.log`
and `results/full`. The repository-ignored .NET 10 startup hook was inherited by
PowerShell-based terminal and stdio MCP children running .NET 9.0.10, which then
failed to load `System.Runtime, Version=10.0.0.0`. This polluted those scenarios;
the run is invalid, not evidence of new product failures or a full-suite pass.
The failure is reproduced outside MossAgent: baseline PowerShell exits zero,
whereas the same command with the hook exits -532462766. Bounded logs and exit
metadata are in `diagnostics/powershell-*` and `startup-hook-compatibility.json`.

Only that identity-checked diagnostic test process tree was retired, recorded in
`retired-invalid-hook-run.json`; session 89190 ended with exit 1. No product
binaries, global environment settings or normal user windows changed. Removing
the hook makes the four affected MCP/terminal baseline cases pass **4/4**, with
the original assertions (`results/uninstrumented-child-baseline.trx`). The hook
must not be enabled globally or across a mixed-runtime test process tree.

The only current complete run is ordinary, uninstrumented session **54206**, using
the same frozen candidate and explicit empty diagnostic variables in its own
shell. Output is `full-uninstrumented.log` and `results/full-uninstrumented`.
It is still in progress; inspect its terminal reports and remaining failures
before claiming any complete result.

This uninstrumented run has already reported
`WorkspaceCatalogLookupTests.WarmCatalogLookups_DoNotAllocate(missing: True)`:
736 bytes versus the unchanged zero-byte assertion. This is a new observed
allocation failure, not a terminal run total or proof of the same cause as the
earlier 3,552-byte model check. Keep both reports and diagnose without weakening
either assertion. As of 12:56 +08:00, runner PID 5856 and App PID 2432 belong to
this exact candidate; recheck process identity before any later action.

Additional diagnostic evidence, not substitutes for the failed full run:

- The original zero-allocation method passed 1,000 direct repetitions in one
  process and one repetition in each of 50 cold processes. Reports are under
  `artifacts/allocation-probe-20261018`. This neither resolves the 3,552-byte
  failure nor proves that JIT/PGO is its cause; the zero-byte assertion is intact.
- The final candidate's isolated authorization suite passed all eight xUnit
  cases. An external Headless/reflection probe completed six final-candidate and
  eight frozen-candidate repetitions of the expansion scenario without the
  unexpected failure; captured exceptions were the expected path denials only.
  These direct diagnostic repetitions are not additional unique xUnit tests and
  do not establish the cause of the full-run `tool_failed` results.
- A separate direct model-menu reflection attempt omitted the runner's UI
  bootstrap and did not complete. Its stacks and identity-checked retirement are
  recorded under `artifacts/project-auth-probe-20261018`. It is invalid evidence,
  not a passing/failed model test; the xUnit reproduction above supersedes it.
- A repository-ignored startup diagnostic was smoke-tested with the existing
  target-lock negative test: one xUnit case passed and captured an IOException
  with sharing-violation HRESULT `0x80070020` at atomic replacement. It stores
  bounded type/HRESULT/stack observations, not exception messages or credentials.
  This validates the diagnostic only; it does not identify the authorization
  failure's cause. No diagnostic hook is part of the product or normal test run.

#### Subsequent isolated follow-ups

The later `workspace-reload-20261018` candidate also makes the two
`ModelPickerViewModelTests` await complete Workspace initialization before
changing model state. Its **107/107** focused cases pass without skipping or
weakening any model/disabled/event assertions. Its build has zero warnings/errors.

The diagnostic startup hook is now exact-process scoped and clears its variables
before the target creates any child processes. `results/scoped-hook.trx` passes
**12/12** authorization/MCP/terminal checks. Three diagnostic reports identify
the expected App.Tests process and `ChildHooksDisabled=true`, with no unexpected
IOException observations. This validates the diagnostic boundary, not the cause
of the original authorization failures; the hook remains outside the product.

An independent local .NET 10.0.12 X64 probe reproduces background-GC counter
anomalies. The short nonparallel measurement helper and retained zero/positive
controls are documented in [ALLOCATION_MEASUREMENT_ACCEPTANCE.md](ENGINEERING.md#allocation-measurement-acceptance); the
`allocation-isolation-20261018` candidate passes **113/113** focused cases.

The subsequent `snapshot-sharing-fixed-20261018` production change fixes a
deterministically reproduced internal-reader/atomic-edit conflict, with
**499/499** tool cases, **113/113** expanded App cases, a zero-warning/error solution build and the 157-file
source-shape audit. See [FILE_SNAPSHOT_SHARING_ACCEPTANCE.md](GIT_WORKTREES.md#file-snapshot-sharing-acceptance) for the successful
and negative paths, intermediate fixture failures and exact scope. It does not
prove the cause of the two old full-run `tool_failed` reports.

At 13:17 +08:00 on 2026-10-18, ordinary frozen session 54206 and its exact App
process were still executing; CPU continued advancing and no terminal App TRX
was present. Its reported model-initialization and 736-byte failures remain.
Its binaries are not replaced by any newer candidate. Do not start a second
complete regression while this identity-checked run remains active.

No native screenshot, native shutdown or real-provider claim is made here.

#### Ordinary frozen full run terminal result

Session **54206** has now ended with **exit 1**. The unchanged candidate executed
all **5,023** App cases: **5,021 passed / 2 failed**, no skips, abort or hang.
The only failures are model-picker initialization and the 736-byte catalog
allocation assertion already recorded above. All eight authorization cases pass
in this run, before the later snapshot-sharing production fix. This excludes
neither an intermittent issue nor other causes of the earlier authorization
failures; no direct exception evidence closes those old reports.

The other seven projects passed: Agent 137, Visual 300, Browser 88,
Infrastructure 328, Windows 142, Providers 600 and Tools 494. Original TRXs and
the bounded aggregate `full-uninstrumented-terminal.json` remain under the
same ignored candidate directory. No focused result relabels this run as green.

The later model-initialization/allocation and snapshot-reader fixes are included
in `attachment-thumbnails-20261018`, together with the new composer/draft image
thumbnail workflow. Its build, 344 Infrastructure cases, 82 expanded App cases
and 168-file shape audit pass. Its new ordinary complete regression is session
**95889**, with frozen assembly identities, `full-regression.log` and
`results/full`; do not overwrite it or start another complete run while live.
[ATTACHMENT_THUMBNAIL_ACCEPTANCE.md](CONVERSATIONS.md#attachment-thumbnail-acceptance) records the feature and remaining native,
history/PDF and complete-regression boundaries.
<!-- /source: DRAFT_BACKGROUND_PERSISTENCE_ACCEPTANCE.md -->


---

<a id="draft-conversation-header-acceptance"></a>

## DRAFT_CONVERSATION_HEADER_ACCEPTANCE.md

<!-- source: DRAFT_CONVERSATION_HEADER_ACCEPTANCE.md -->
### Draft conversation header acceptance

#### Scope: 2026-10-10

The native sidebar-compose follow-up exposed a stale header: starting a new
conversation cleared the selected task and inspector, but the previous task title
and plus action remained in an unselected tab strip. The cause was deriving strip
visibility only by negating `IsSingleTab`, including when no tab was selected.

`WorkspaceConversationTabsViewModel.ShowTabStrip` now also requires a selected
tab. Drafts and disposed/empty workspaces hide both the single heading and tab
strip. Existing open tabs are retained, not closed, archived or deleted; selecting
a saved task or using navigation history restores their existing instances.
Selected single/multiple conversation layouts keep their shared appearance.

#### Design and safety boundaries

- App owns this presentation state. The existing catalog selection events,
  commands and bindings are sufficient; no new port, tool or dependency is added.
- Only one production condition changes. No view-local geometry, native visual
  control, additional global state, persistence schema or database migration is
  introduced. Existing property notifications publish the derived state.
- Selection does not start a model request, approve a tool or replay work.
  Authorization, approval, task concurrency, cancellation and audit remain in
  their existing execution paths. History commands retain locked-state checks.
- All three changed handwritten C# files contain one type and stay below 500
  lines. The longest method is 68 lines, below the 80-line hard limit.
- Debug remains JIT; publishing remains Windows x64 single-file without AOT.
  No staging, commit, push or publish occurs.

#### Regression evidence

- Initial test-authoring attempts used the wrong history command names and
  sidebar appearance-class name. Those compile/selector errors are corrected
  before treating the failures as evidence of the production issue.
- The corrected 13-case run fails against the original condition: all 12
  theme/width/tab-count cases fail on the visible stale strip, and the lifecycle
  case fails on initial empty-workspace strip visibility.
- New cases use the real MainWindow and composition root, sidebar-button input,
  catalog back/forward commands and synthetic SQLite/provider fixtures. They
  cover light/dark themes at widths 980/1280/1920, single/multiple retained tabs,
  task persistence without archiving, locked new-conversation rejection and
  disposed command/subscription ownership. No provider requests are sent.
- The existing draft-heading test now rejects a visible stale tab strip as well
  as the stale single heading. Its tab-reselection/persistence behavior remains.
- The 115-case targeted run passes. Focused whitespace verification passes after
  expanding one new test's object initializer; the solution build succeeds with
  zero warnings and errors. The subsequent full run passes 4298/4298 cases across
  eight assemblies, with zero failed or skipped cases. TRX and logs stay in the
  external evidence directory below. Repository-default `git diff --check` passes;
  the index remains empty.

#### Native evidence and remaining scope

The ordinary-permission QA host uses the real App/MainWindow and new synthetic
data, not a visual mock or the user's database. Reference capture/interaction
artifacts stay outside Git:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-draft-header-20261010`.

- The fresh before capture is 1920x1032 at native scale. An actual sidebar click
  produces `selected=none; heading=False; strip=True; retained=1` and leaves the
  previous title visible in `before-light-draft.png`.
- The before host closes normally with both window/lifetime exit messages.
- The repaired 1920x1032 light window is exercised through actual sidebar,
  back and forward clicks: new/forward draft states have no title/strip, while
  back restores the selected title and inspector. Logs retain the same one
  open tab across all three states.
- The ordinary-permission 980x640 dark window also hides the old title/strip
  after a real sidebar click. Both after hosts report `QA_ELEVATED: False` and
  close normally. Dark empty-state wrapping is visibly still a separate gap.
- `before-light-draft.png`, `after-light-draft.png`, `after-light-back.png`,
  `after-light-forward.png` and `after-dark-draft.png` retain full native captures.
  The unscaled before/after header crop in `draft-header-comparison.png` changes
  from 681 foreground pixels below luminance 210 to zero in bounds
  `(320,48)-(660,92)`. This measures removal of stale chrome, not exact Codex
  contour/antialiasing equivalence. No screenshot is stretched.
- Read-only post-exit inspection of all three synthetic databases confirms nine
  stored tasks, one pinned task, zero archived tasks and zero messages per host.
  Counts are retained in `native-storage-verification.json`; no provider secrets
  or user database contents are queried. No QA host remains running.

This is a header ownership correction, not proof of whole-page Codex fidelity or
completion of the full product goal. Codex draft-state screenshots, every DPI,
responsive empty-state wrapping and all tool/provider workflows remain separate
acceptance requirements.
<!-- /source: DRAFT_CONVERSATION_HEADER_ACCEPTANCE.md -->


---

<a id="file-editing-acceptance"></a>

## FILE_EDITING_ACCEPTANCE.md

<!-- source: FILE_EDITING_ACCEPTANCE.md -->
### 文件编辑链路验收

`WorkspaceFileEditingFlowTests` 使用真实 App 服务装配、设置 ViewModel、
SQLite、OpenAI Responses 适配器、AgentRunner、工具管线、审批队列与对话呈现器。
仅模型端由监听随机回环端口的 `FileEditingModelServer` 替代，不调用付费模型。

#### 自动覆盖范围

- 从提供商和模型设置的保存命令写入配置，再从工作区主操作命令创建并运行任务。
- 模型先读取授权目录中的文件，从返回摘要获取完整文件 SHA-256，再请求写入或精确替换。
- 等待批准时目标不变，正文默认隐藏；批准后更新文件，包括空文本写入。
- 拒绝保持原文件；审批期间外部改动触发 `file_changed`，保留外部版本。
- 审批期间用工作区停止命令取消，正文清空、队列清空、任务持久化为已取消，
  工具持久化为 `tool_cancelled`，不再请求下一轮模型。
- 只读策略或越权目标在提示审批前拒绝；授权文件和外部哨兵文件均不变。
- 使用重新打开的服务装配核对保存的任务、工具对话卡片及已完成审计，
  与当前工作区卡片的成功状态和错误码保持一致；不残留临时写入文件。
- 独立执行器测试验证审批服务异常不泄露信息，以及批准与取消竞争时，
  即使工具不自行检查取消也不能开始执行。

```powershell
dotnet test tests/MossAgent.App.Tests/MossAgent.App.Tests.csproj --no-restore --disable-build-servers -m:1 --filter FullyQualifiedName~WorkspaceFileEditingFlowTests
dotnet test tests/MossAgent.Tools.Tests/MossAgent.Tools.Tests.csproj --no-restore --disable-build-servers -m:1 --filter FullyQualifiedName~ToolApprovalCancellationTests
dotnet test MossAgent.slnx --no-restore --disable-build-servers -m:1
dotnet build MossAgent.slnx --no-restore --disable-build-servers -m:1
```

#### 运行界面验收边界

自动链路测试运行真实 ViewModel 命令，但不等同于鼠标、键盘和像素验收。
实际应用仍需使用独立数据目录与本机模型端，检查输入、审批正文切换、
批准、拒绝、停止和重启后的对话展示；不得使用真实提供商凭据或生产文件。
截图需保持原始窗口尺寸与 DPI，分别检查普通和最大化窗口及明暗主题。

#### 2026-10-04 实际界面验收

使用正常权限的真实 `MossAgent.App.exe`、独立临时数据目录和本机 Responses 模型端，
通过鼠标和键盘输入读取、精确替换测试文件的请求。未使用真实凭据、生产文件或付费模型。

- 到达替换审批后，文件仍为 `alpha\n`；操作正文默认隐藏，展开后显示原文本与替换文本。
- 点击“批准本次调用”后文件为 `beta\n`；模型完成读取、替换和最终回复三轮调用。
- 只读核对 SQLite：任务状态为 Completed，保存四条消息，两个成功工具结果，
  两条已完成且批准的审计；文件 SHA-256 与保存的替换结果摘要一致。
- 关闭并重开同一数据目录后，对话、工具成功状态与最终回复恢复；没有重新执行模型或工具。
- 共享工具行使用单行摘要和省略号，不再因路径、哈希撑高；展开后保留完整摘要、
  SHA-256 和原有工具正文。运行中窗口的可访问名称为实际摘要，不再是 Grid 类型名。
- 用仓库外 Python 截图脚本的 `--passive --include-popups` 捕获审批、重开会话、
  普通窗口与最大化窗口，并检查浅色和深色主题。截图保持 1280×820、1920×1032
  原始尺寸，不缩放或拉伸截图；本轮捕获没有黑屏。

##### 共享工具行的拒绝路径复测

另建独立临时数据目录，使用同一本机模型端，通过真实输入和鼠标点击拒绝替换审批。

- 审批前后文件字节均为 `616C7068610A`，保持 `alpha\n`，没有执行替换。
- 读取行保留可展开按钮；无详情的拒绝结果显示静态行，不显示展开箭头或禁用按钮。
  可访问树中拒绝摘要与失败状态为文本，读取按钮仍使用实际摘要作为名称。
- SQLite 保存两个工具结果；替换结果与已完成审计均为失败，审批未批准，
  错误码为 `approval_denied`，没有原始详情。模型完成三轮调用并显示最终回复。
- 用仓库外 Python 脚本被动捕获并检查 1280×820 普通窗口和 1920×1032 最大化窗口，
  工具图标、摘要和状态保持对齐，无详情行不占用展开交互。
- 最终组件修正后，完整解决方案 824 项测试通过；解决方案构建零警告、零错误。

本次实际输入覆盖成功批准和拒绝路径。审批中取消、外部文件冲突等仍由自动测试验证，
尚未在本轮逐项鼠标验收；多 DPI 和 Codex 全界面逐像素对照也未完成。
本机模型端验证工具管线与真实 UI，不代表真实服务商或付费模型行为已验收。
<!-- /source: FILE_EDITING_ACCEPTANCE.md -->


---

<a id="history-context-acceptance"></a>

## HISTORY_CONTEXT_ACCEPTANCE.md

<!-- source: HISTORY_CONTEXT_ACCEPTANCE.md -->
### 历史工具上下文验收

2026-10-06 验证后续发送和应用重启后仍能使用已保存的工具结果。
本记录不代表整个产品完成，也不代表界面逐像素匹配 Codex。

#### 所有权与设计自检

- Application 的 `HistoricalToolObservation` 拥有历史结果格式；App 复用工作区
  仓储读取、校验与映射，Agent 负责上下文预算，Providers 负责协议序列化。
  不增加反向依赖，App 不直接执行 SQL。
- 复用现有消息、任务、模型运行和工具执行端口。持久化的 `MessageRole.Tool`
  不变；运行态新增 `ModelRole.ToolObservation`，无需数据库迁移。
- 历史结果明确标为不可信数据，不是指令、原生工具调用或可重放动作。
  不伪造数据库中不存在的历史参数，不保存新凭据或参数，也不修改审批政策。
  后续新请求的工具仍经过原有授权、审批、超时、取消、输出限制与审计管线。
- 每条完整历史观察最多 32,768 字符，超出时保留前后文及明确截断标记，
  切分不破坏 Unicode 代理对；Agent 再应用模型预算，子代理共享仍受既有
  12,000 字符上限约束。方法与类型按既有职责拆分。
- 正向测试覆盖普通及子代理结果、旧记录、四种提供商协议、预算与 Unicode；
  负向测试覆盖未知角色、跨任务历史、不可容纳的原生元数据及只读写入拒绝。

#### 实际修复

- 后续发送不再过滤已持久化的 Tool 消息；派发前拒绝其他任务的历史。
  未知持久化角色不能悄悄变为 System，非工具消息不保留无关调用 ID。
- 旧的、非 JSON 或格式损坏的结果正文仍作为历史数据保留；调用 ID 仅作
  文本诊断，不变成协议级调用。UI 展示和数据库中的原结果不重新解释。
- 四种请求构建器只从 Assistant 消息生成原生调用；历史观察即使误带调用
  元数据也不会生成调用。Responses 和兼容 Chat 请求把观察编码为普通用户数据。
- Gemini 对格式损坏、被截断、空值、数组和标量的原生工具结果，使用包含原文
  的 `output` 对象；有效 JSON 对象保留字段，不因历史或裁剪结果解析失败。
- 在预算允许时保留裁剪后的最近历史组和最新问题，不总是整组丢掉；原生调用
  组保持完整。最新调用元数据无法容纳时安全拒绝，不发送超预算请求。
- 主代理和子代理的安全提示明确禁止把文件、工具及历史正文当作可信指令，
  或自动重放历史副作用。仅在开启上下文共享时向子代理传递标记后的观察。

#### 回归与自动化验证

修复前两项新增回归实际失败：普通工具和子代理工具结果均从后续请求消失。
修复后验证设置到本机 SSE、真实文件工具、SQLite、后续模型请求和重新打开容器
的完整链路；首条助手回复刻意不包含文件证据，避免以助手正文代替工具历史。

- 四项真实工作流：重开或不重开容器，分别执行正常后续请求及尝试只读写入。
  文件实际读取一次；后续请求含历史证据，没有原生调用重放。
- 不允许的写入按 `capability_denied` 审计；证据文件不变，不出现待处理审批。
- 四种提供商序列化测试、七项 Gemini 结果格式回归通过。
- 最终完整解决方案 1,508 项通过：Agent 43、App 1,128、Browser 32、
  Infrastructure 32、Windows 50、Providers 76、Tools 147。
- 最终解决方案构建 0 警告、0 错误；相关文件空白格式检查和 `git diff --check`
  通过。Roslyn 审计 803 个 C#/AXAML 文件，无超过 500 行的文件或多类型 C# 文件。

仍有三个预先存在且本轮未修改的超长方法：

| 方法所在文件与起始行 | 方法行数 |
| --- | --- |
| `src/MossAgent.Providers/OpenAi/OpenAiCompatibleProvider.cs:19` | 90 |
| `tests/MossAgent.App.Tests/ModelSettingsViewModelTests.cs:154` | 86 |
| `tests/MossAgent.App.Tests/SubagentWorkspaceFlowTests.cs:21` | 86 |

#### 运行窗口、真实进程重启与截图

仓库外验收宿主使用真实 App/MainWindow、独立临时 SQLite 和授权项目、合成提供商
凭据与本机 SSE 服务。脚本只被动截图，不激活、点击、缩放或调整其他应用。
目录为 `%TEMP%/MossAgent-history-context-qa-20261006-01`。

每个场景先启动 `--read`，通过真实发送命令读取临时文件并关闭窗口；确认进程退出后，
以全新进程 `--resume` 打开同一 SQLite 任务，通过真实发送命令继续对话。
文件证据为 `HISTORY_EVIDENCE_74623`。第二次请求的历史工具正文含该证据，
而首条助手回复只说明读取完成。恢复时展开共享工具详情以检查原文呈现。

| 最终图片 | 原始像素尺寸与状态 |
| --- | --- |
| `history-read-light-max-final.png` | 1920×1032，浅色最大化，首次真实读取 |
| `history-resume-light-max-final.png` | 1920×1032，新进程恢复并继续，详情展开 |
| `history-read-dark-normal-final.png` | 1280×820，深色普通窗口，首次真实读取 |
| `history-resume-dark-normal-final.png` | 1280×820，新进程恢复并继续，详情展开 |

最终日志位于 `light-max-final` 与 `dark-normal-final` 数据子目录。
每组首次进程两次模型请求、恢复进程一次；最终各有一个任务、一条工具记录和
两条用户消息。恢复日志确认 `QA_RESTORED_EVIDENCE: True`、`QA_NO_REPLAY: True`，
证据文件不变；首次阶段尚无后续请求，两个标志为 False 不代表恢复失败。
四个最终进程均记录普通权限、持久化核对、`QA_WINDOW_CLOSED`、
`QA_EXIT_COMPLETED`，进程退出码为 0；验收结束没有遗留宿主进程。
人工检查四张图片确认非黑屏、原始尺寸、消息和共享工具行可见；深色恢复截图
随对话滚动到后续回答，旧首条消息和工具标题不全在视口中，不当作全历史同时可见证据。

首次 `light-max` 宿主尝试曾在窗口关闭后未退出：本机测试服务器异步释放
捕获了已停止的 Avalonia 调度上下文。修正测试服务器释放等待为
`ConfigureAwait(false)`，只停止已确认归属的旧验收进程，再完整重跑四个最终场景。
旧 `light-max/read.log` 缺少退出完成标记，保留为失败证据，不算正常退出验收。
该测试宿主问题不被表述为产品退出修复。

#### 未验证范围

- 程序化设置状态和执行真实命令不是物理鼠标、系统键盘或原生文件选择器验收。
- 没有付费提供商调用，没有全 DPI、全部页面状态或最新 Codex 窗口逐像素对照。
- 没有更改生产配置、真实项目文件或用户凭据；宿主、Python 脚本和截图不加入仓库。
- 没有暂存、提交或推送。整个 MossAgent 目标继续保持未完成。
<!-- /source: HISTORY_CONTEXT_ACCEPTANCE.md -->


---

<a id="image-attachment-workflow-acceptance"></a>

## IMAGE_ATTACHMENT_WORKFLOW_ACCEPTANCE.md

<!-- source: IMAGE_ATTACHMENT_WORKFLOW_ACCEPTANCE.md -->
### Image attachment workflow

This checkpoint predates projectless attachment selection. The subsequent
implementation, version-17 migration and current evidence are recorded in
[PROJECTLESS_ATTACHMENTS_ACCEPTANCE.md](CONVERSATIONS.md#projectless-attachments-acceptance); the results below retain their original
candidate and scope.

The full functional-parity goal remains active. This extends the transport stage
in [IMAGE_INPUT_ACCEPTANCE.md](CONVERSATIONS.md#image-input-acceptance); neither document defines a smaller product goal.

#### Plan and required self-check

1. Domain owns an immutable image snapshot. Application owns decoder contracts
   and attachment/request limits; Infrastructure implements bounded Skia decoding,
   authorized file reads and snapshots. App only coordinates ports and displays
   localized feedback; it does not access image files or native codecs directly.
2. Reuse the existing attachment reader/store, draft CAS repository, composer and
   history builder. Keep text snapshots and their old paths valid; images become
   native ModelMessage image blocks, never Base64 inside untrusted text context.
3. Retain project-authorized paths and per-selection identity, cancellation,
   thirty-second storage/read limits and task-owned execution. Enforce real media
   type, static frames, dimensions/pixels, encoded bytes, aggregate image bytes,
   count and selected-model capability. Invalid or missing restored snapshots
   remain removable unavailable chips and cannot be sent. No tool permission is
   granted by selecting an image. Source files are not read again after selection.
4. Keep one handwritten type per file, files <=500 lines and methods <=80 lines.
   Add a forward metadata-only SQLite migration; never place pixels/Base64 in SQL.
5. Test actual codecs/files/SQLite and independent production composition reopen,
   original-source deletion, changed metadata/hash, bad/animated/oversized images,
   unauthorized roots/links, cancellation, text regression, incompatible models,
   draft switching/promotion, native wire blocks and no binary text context.

SkiaSharp is pinned to the version already used by Avalonia (3.119.4), with the
Windows native-assets package. This is a platform-neutral codec API, not a new
Windows API in Application/Domain. No System.Drawing or new OS dependency is put
in those modules. Packaging and native/library resolution still require checks.

#### Verification

Current candidate: `artifacts/image-workflow-20261018`, Debug/JIT, 2026-10-18.

| Gate | Current authoritative evidence |
| --- | --- |
| Real codecs | `SkiaAttachmentImageDecoderTests`: four static formats; real animated GIF/APNG/WebP rejected; signatures/truncation, dimensions/pixels, encoded bytes, cancellation and immutable/non-diagnostic snapshots |
| Authorized selection | `WorkspaceImageAttachmentReaderTests`: actual magic rather than filename, multiple authorized roots, sibling traversal and fake image rejection, exact 5 MiB/10 MiB boundaries, missing decoder and cancellation; existing text/link/selection-isolation cases remain in regression |
| Submitted snapshots | `ImageAttachmentPersistenceTests`: four formats survive original-file deletion and SQLite metadata reload; real binary bytes/hash; missing, length, hash, MIME and decoded-content damage reject; forged envelopes are actually decoded before publication |
| Draft snapshots | `ImageDraftAttachmentTests`: four formats reopen with no original source; stale CAS and same-ID replacement cannot overwrite saved bytes; missing/corrupt/retyped images retain unavailable removable image metadata, not empty text |
| Forward migration | `ImageAttachmentMigrationTests`: actual v15-shaped tables with sent/draft text snapshots survive v16 upgrade; synthetic final-version failure rolls back both tables, retry/idempotency succeeds, cancellation preserves v15, MIME/size constraints and cascades remain enforced |
| Production workflow | `WorkspaceImageAttachmentWorkflowTests`: real model-editor capability save, production reader/Skia/store/SQLite, project/projectless navigation isolation, independent composition reopen of unsent/submitted images after source deletion, empty-body image send and later native Responses blocks over real loopback HTTP; unsupported model selection blocks sending without losing the draft and switching back recovers; unavailable images block requests until explicit removal |
| History budget | `WorkspaceImageAttachmentHistoryTests`: real snapshot reads, whole older-payload count/context omission with markers, latest image retained even when the model budget is too small; text-only history keeps its original default `Images` representation |

The first full Infrastructure run exposed a new test comparing a record's list
reference rather than its attachment values. The final test separately compares
message fields and ordered attachment metadata; no production assertion was
weakened. Existing cross-task SQLite coverage now explicitly names insert columns
and checks the foreign-key extended error (787), so the added MIME column cannot
make that negative test pass for a wrong column-count error. Older migration
fixtures remove all later version markers, not just version 15.

Final current-candidate results (no failures, skips or aborts):

- Infrastructure complete project: **316/316**, `results/infrastructure-final.trx`.
- Providers complete project: **600/600**, `results/providers.trx`.
- Agent complete project: **132/132**, `results/agent.trx`.
- App focused regression: **150/150**, `results/app-attachments.trx`.
- Total in this regression scope: **1198**, not the complete solution test suite.
- Full solution build succeeds with **0 warnings / 0 errors**.
- Roslyn syntax/one-type/size check: **123** changed/untracked C# files,
  largest file **497** lines, largest method/local function **59** lines.

Reproduction (all paths below are relative to the repository):

```powershell
dotnet build MossAgent.slnx --artifacts-path artifacts/image-workflow-20261018 --nologo -v minimal
dotnet test tests/MossAgent.Infrastructure.Tests/MossAgent.Infrastructure.Tests.csproj --artifacts-path artifacts/image-workflow-20261018 --logger 'trx;LogFileName=infrastructure-final.trx' --results-directory artifacts/image-workflow-20261018/results --nologo -v minimal
dotnet test tests/MossAgent.Providers.Tests/MossAgent.Providers.Tests.csproj --artifacts-path artifacts/image-workflow-20261018 --no-build --logger 'trx;LogFileName=providers.trx' --results-directory artifacts/image-workflow-20261018/results --nologo -v minimal
dotnet test tests/MossAgent.Agent.Tests/MossAgent.Agent.Tests.csproj --artifacts-path artifacts/image-workflow-20261018 --no-build --logger 'trx;LogFileName=agent.trx' --results-directory artifacts/image-workflow-20261018/results --nologo -v minimal
dotnet test tests/MossAgent.App.Tests/MossAgent.App.Tests.csproj --artifacts-path artifacts/image-workflow-20261018 --filter 'FullyQualifiedName~Attachment|FullyQualifiedName~ComposerDraft|FullyQualifiedName~ComposerSend|FullyQualifiedName~WorkspaceSendDraft|FullyQualifiedName~ProjectlessChat' --logger 'trx;LogFileName=app-attachments.trx' --results-directory artifacts/image-workflow-20261018/results --nologo -v minimal
```

#### Remaining scope and limitations

The list below records this earlier candidate's original boundaries, not the
latest overall status. Projectless explicit selection was subsequently added
in [PROJECTLESS_ATTACHMENTS_ACCEPTANCE.md](CONVERSATIONS.md#projectless-attachments-acceptance); selected/composer, restored-draft
and sent/history thumbnails are now covered by
[ATTACHMENT_THUMBNAIL_ACCEPTANCE.md](CONVERSATIONS.md#attachment-thumbnail-acceptance). Native picker/restart, full-size viewing,
other-format/native-image orientation acceptance, PDF and live-provider
acceptance remain open. JPEG EXIF thumbnail normalization is covered separately
by [IMAGE_ORIENTATION_ACCEPTANCE.md](CONVERSATIONS.md#image-orientation-acceptance), without rewriting original attachments.

- This does not complete the overall Codex functionality/workflow objective.
  [FUNCTIONAL_PARITY_MATRIX.md](OVERVIEW.md#functional-parity-matrix) retains the complete gap inventory.
- Picker selection is scripted; actual Windows codec execution, real files,
  SQLite and loopback HTTP are verified, not native picker clicks, EXE restart,
  rendered image thumbnails or paid-provider image interpretation. No existing
  user window or real credential was accessed.
- Native file pick/restart, thumbnails/previews, projectless image selection,
  PDF/native documents and image sharing with child agents remain open.
- Provider-specific accepted formats, real reasoning and useful image-cost
  estimation need further verification. The conservative Base64-character budget
  can reject an otherwise size-valid image for a small-context model; the latest
  image is not silently stripped to force a successful call.
- Synchronous native decoding is bounded by encoded bytes, dimensions and pixels
  but cannot be forcibly interrupted midway. Caller cancellation stops waiting;
  native resources are released when the bounded background decode completes.
- Paths retain existing link checks, not a claim of eliminating every filesystem
  TOCTOU race. File writes and metadata transactions are not distributed commits;
  unreferenced snapshots/abrupt-submit draft duplication remain possible and
  restored drafts are never sent automatically.
- No publish was requested or performed. Native codec resolution in these tests
  does not establish Release single-file extraction or dependency-count behavior.
- Earlier sessions `21912` and `11377` were re-polled and remained live with no new
  output. Process inventory confirms the earlier dotnet/vstest/App processes
  still exist. They were not restarted, killed or overwritten and cannot validate
  this candidate. The earlier two allocation failures remain unresolved; this is
  not a successful current complete App/all-suite checkpoint.

The later ordinary frozen workspace run has since completed with App 5,021
passes / two failures, no skips or abort; its seven other test projects pass.
Current full session 95889 tests the newer thumbnail candidate and has already
reported a different raw-counter allocation assertion. See
[DRAFT_BACKGROUND_PERSISTENCE_ACCEPTANCE.md](CONVERSATIONS.md#draft-background-persistence-acceptance) for exact terminal/run boundaries;
no current complete regression is claimed green here.
<!-- /source: IMAGE_ATTACHMENT_WORKFLOW_ACCEPTANCE.md -->


---

<a id="image-input-acceptance"></a>

## IMAGE_INPUT_ACCEPTANCE.md

<!-- source: IMAGE_INPUT_ACCEPTANCE.md -->
### Image input: transport foundation

#### Scope and sequence

The product target remains Codex Desktop functional/workflow parity. This is the
first image-input stage, not completion of multimodal attachments or the product.

1. Add immutable, bounded image envelopes to the Application model contract.
2. Encode native image blocks in all four existing provider protocols, preserving
   text, tools, Gemini continuation and advanced-parameter ownership.
3. Include image envelopes in context budgeting and verify production transports.
4. Continue with actual file decoding, dimensions/animation limits, authorized
   selection, file-backed history/drafts, migrations and native interaction.

#### Required design self-check

1. Application owns the in-memory request envelope and request validation;
   Providers owns wire JSON; Agent owns context selection. No new dependencies,
   filesystem access, UI ownership changes or Windows APIs are introduced.
2. Extend the existing ModelMessage/IModelProvider path. Do not create a parallel
   provider path or encode image bytes into ordinary message text.
3. Only explicit user messages may contain images. The selected model must declare
   image support; file support is a separate capability. Limits are 5 MiB per
   image, ten images/10 MiB of binary image data per request, and 20 MiB of final
   UTF-8 JSON for image requests. No URL fetch, upload, tools or permission grant
   is implied. Existing provider cancellation/timeout/HTTP behavior remains owned
   by its current pipeline. Local validation errors must not echo credentials,
   image bytes or caller-supplied media-type values.
4. Keep each handwritten C# file to one type, under 500 lines, with methods under
   80 lines. Split image JSON construction from existing request builders.
5. Verify immutable ownership, empty/oversized/unsupported envelopes, capability,
   roles, count/aggregate/body limits, all four wire schemas, text-only behavior,
   image-only messages, context eviction and intact latest images. Exercise real
   provider streaming entry points using synthetic credentials and local fixtures.

#### Deliberate boundaries, not completion claims

- An image envelope checks its declared media type and bounded byte ownership;
  it is not a codec and does not prove that bytes decode to a valid static image.
  The UI must not accept binary files until a decoder enforces real type,
  dimensions, decoded-pixel and animation policies. PNG, JPEG, GIF and WebP are
  transport media types, not a claim of complete file-format acceptance.
- Base64 is immutable, excluded from ordinary JSON persistence/diagnostics, and
  included only by explicit provider content-block builders. SQLite still stores
  text attachment metadata only; this stage adds no schema or binary persistence.
- Until codec dimensions and model-specific image accounting are available,
  context selection conservatively charges the encoded character count plus
  envelope overhead, using Agent's existing character-budget units. It never
  slices Base64 to fit. Large images may therefore be rejected conservatively.
- Shared subagent context remains textual and explicitly marks images as omitted;
  it must not silently imply that a child model has received the pixels.
- Authorized image selection, safe decoding, sent/unsent snapshot recovery, PDF,
  model-service acceptance, native EXE interaction and full regression remain
  incomplete. Do not enable image picking based on transport tests alone.

#### Protocol references

Read on 2026-10-18; no model/default changes are implied by documentation examples.

- OpenAI: https://developers.openai.com/api/docs/guides/images-vision
  Responses input_image/image_url data URLs; Chat Completions image_url blocks.
- Anthropic: https://platform.claude.com/docs/en/build-with-claude/vision
  image/source with type=base64, media_type and data.
- Gemini: https://ai.google.dev/api/generate-content
  contents/parts with inlineData, mimeType and Base64 data. This application uses
  generateContent, not the separate Interactions API shown by newer guides.

The OpenAI vision guide was retrieved and confirms the Responses example. The
separate Chat endpoint reference returned a generic landing page/404 in this run;
its compatible content-block schema has local fixture/loopback coverage, not
fresh endpoint-reference or paid-service acceptance evidence.

#### Verification (2026-10-18)

Candidate root: `artifacts/image-transport-20261018`. All commands use this
independent artifacts path; no existing user window, credentials or paid API was
used. Final sessions completed successfully; this is not full-solution regression
or native EXE validation.

| Check | Current result |
| --- | --- |
| Full solution Debug build | Passed: 0 warnings, 0 errors |
| Providers complete test project | 600/600 passed; `results/providers-all-fixed/wzheng_WZ-MINIPC_2026-10-18_09_39_42_net10.0.trx` |
| Agent complete test project | 132/132 passed; `results/agent-all/wzheng_WZ-MINIPC_2026-10-18_09_39_54_net10.0.trx` |
| App attachment/composer/history/projectless/subagent regression subset | 115/115 passed; `results/app-regression/wzheng_WZ-MINIPC_2026-10-18_09_40_00_net10.0.trx` |
| Roslyn syntax/type/size check of all modified/untracked handwritten C# | 105 files passed; one type each, maximum file 494 lines, maximum method 58 lines |
| Tracked diff and untracked C#/Markdown whitespace | Passed |

The new tests include actual loopback HTTP through all four production streaming
providers with deterministic SSE replies and real one-pixel PNG bytes; this proves
request encoding and response handling, not remote image interpretation. Agent
tests retain the image identity over tool rounds and deny unsupported images
before contacting even a custom/unvalidated provider. Projectless image envelopes
do not enable tools or grant authorized roots. Ordinary serialization excludes
pixels; the envelope factory snapshots caller bytes and is not a JSON persistence
constructor. Shared-image concurrent builds own independent JSON trees.

Recorded failures are retained, not overwritten or reclassified as passes:

- The first focused run passed 56/57: System.Text.Json inspected the public Span
  constructor even during serialization. Replaced it with an explicit byte factory
  and a private constructor; serialization/privacy checks now pass.
- The initial complete Providers run passed 593/599: image validation plus presence
  detection added two history enumerations. Fused request-local validation into
  existing provider message passes, passing state by reference; the original
  Gemini enumeration assertions remain unchanged and now pass. A new image case
  also requires the original two-pass no-tool history behavior.
- An initial solution build caught xUnit2031 in a new test. The assertion now uses
  the predicate overload, without disabling analyzers.

Reproduction commands (use the same candidate artifacts path):

```powershell
dotnet build MossAgent.slnx --artifacts-path artifacts/image-transport-20261018
dotnet test tests/MossAgent.Providers.Tests/MossAgent.Providers.Tests.csproj --artifacts-path artifacts/image-transport-20261018 --no-build
dotnet test tests/MossAgent.Agent.Tests/MossAgent.Agent.Tests.csproj --artifacts-path artifacts/image-transport-20261018 --no-build
dotnet test tests/MossAgent.App.Tests/MossAgent.App.Tests.csproj --artifacts-path artifacts/image-transport-20261018 --no-build --filter 'FullyQualifiedName~WorkspaceAttachment|FullyQualifiedName~WorkspaceComposer|FullyQualifiedName~WorkspaceHistoryContext|FullyQualifiedName~Projectless|FullyQualifiedName~WorkspaceSendDraft|FullyQualifiedName~WorkspaceSubagent'
```

The older composer full-suite handle remains a separate, live candidate with
previously reported allocation failures. Its dependent attachment-history run is
still waiting. Neither is evidence for this image candidate, and neither was
restarted, terminated or overwritten due to silent polling. Full regression,
native interaction and the remaining product requirements stay open.
<!-- /source: IMAGE_INPUT_ACCEPTANCE.md -->


---

<a id="image-orientation-acceptance"></a>

## IMAGE_ORIENTATION_ACCEPTANCE.md

<!-- source: IMAGE_ORIENTATION_ACCEPTANCE.md -->
### JPEG image-preview orientation

#### Confirmed defect and scope

The attachment renderer decoded raw pixels and resized them without applying
the codec's EXIF origin. Deterministic asymmetric six-color JPEG fixtures
confirmed the defect before production changes: **4 passed / 28 failed / 32
executed**, no skips. Only normal top-left orientation passed; mirrored cases
had incorrect cell positions, and transposed cases also had incorrect dimensions.
The report remains under `artifacts/image-orientation-20261018/results/before-fix`.

JPEG thumbnail previews now honor all eight EXIF orientations. This applies to
the shared renderer already used by selected images, restored drafts, sent rows
and lazy history previews. Original attachment bytes and encoded dimensions are
not normalized or rewritten. Other formats' orientation metadata, real-camera
corpora, full-size viewing and real-provider interpretation are not established
by these JPEG fixtures.

#### Design and safety review

- Infrastructure owns the change behind the existing image-decoder and thumbnail
  ports. No UI, Agent, provider, application contract or database schema changes.
- `SkiaAttachmentImageSource` retains `SKCodec.EncodedOrigin` while keeping its
  original bounded raw bitmap. Format, actual dimension/pixel limits, animation
  rejection, complete decode and cancellation checks remain shared and unchanged.
- `SkiaAttachmentImageOrientation` applies the origin only after the renderer's
  existing resize. Any extra surface is at most 96 by 96 pixels; there is no
  second full-resolution orientation bitmap. Normal top-left uses the existing
  direct path. Rotation, horizontal/vertical mirroring, transpose and transverse
  use explicit small-surface transforms.
- The final static PNG has the oriented dimensions and default top-left origin,
  so a consuming bitmap decoder does not apply the transform twice. Aspect ratio,
  small-image no-upscale and 64 KiB/96-pixel output limits are retained.
- The renderer's instance gate still stays held until its worker exits. The
  helper checks cancellation and disposes its temporary surface. The existing
  bounded-native-work limitation remains: synchronous codec/canvas work cannot
  be forcibly interrupted midway.
- Snapshot hashes, original JPEG/EXIF bytes, encoded width/height, model payloads
  and selected-file authorization stay unchanged. This does not strip metadata
  from provider uploads or claim new model-side image interpretation behavior.
- No new file reads, process, network call, permission, approval bypass, global
  cache, AOT/trimming setting or cross-module dependency was introduced.
- New handwritten C# files each contain one type; focused pixel and persistence
  tests prove success, while the existing complete Infrastructure suite retains
  malformed, animated, oversize, cancellation, hash and path-boundary negatives.

#### Validation

Candidate: `artifacts/image-orientation-20261018`.

| Evidence | Terminal result |
| --- | --- |
| Before-fix JPEG fixtures | 4 passed / 28 failed / 32 executed |
| Initial orientation/renderer/decoder focused checks after fix | 63/63 passed |
| Complete Infrastructure suite after persistence tests | 392/392 passed, no skips or abort |
| App image/chip/history/projectless/draft checks | 84/84 passed, no skips or abort |
| Final complete solution build | 0 warnings, 0 errors |
| Changed/untracked handwritten C# shape audit | 178 files, no violations; maximum 499 file lines / 67 method lines |

The 32 pixel cases cover all eight JPEG origins, little-/big-endian TIFF tags,
large inputs and inputs smaller than the thumbnail maximum. Each first checks
that native Skia actually recognizes the requested origin, then checks output
dimensions, six independently specified cell positions/colors, alpha, default
output origin, byte bounds and unchanged original bytes/encoded dimensions.

Sixteen persistence cases use real exact-file/project readers, temporary files,
SQLite and file-backed stores. All eight origins pass both project and
projectless selection, draft reopening, and sent-history snapshot reopening after
deleting the source. Projectless historical tasks are actually persisted with a
null project. Draft and historical previews agree, while original JPEG bytes,
draft text and sent text survive unchanged. These are port/SQLite checks, not
native picker clicks or an EXE restart.

The optional test evidence writer produced eight source JPEG/preview PNG pairs
under this candidate's `images`. The generated `orientation-contact-sheet.png`
was inspected directly: encoded rasters stay unchanged and all normalized
previews have the expected mirrored/rotated cell layout. This is a deterministic
codec comparison, not a desktop screenshot or a Codex pixel comparison.

#### Remaining product work

This does not complete image input or the overall Codex workflow-parity goal.
PDF, full-size viewing, child-agent image sharing, other-format orientation
metadata, native picker/restart and real-provider format/interpretation acceptance
remain open in [FUNCTIONAL_PARITY_MATRIX.md](OVERVIEW.md#functional-parity-matrix).

At 2026-10-18 14:00 +08 the earlier frozen complete run, session **95889**, was
still live with the same runner/App process identities. All six frozen binary
hashes still matched. Its recorded 1,176-byte sidebar allocation failure is not
erased by newer focused passes. No second complete regression was started and no
running candidate was rebuilt. No native user window, credential, release,
commit, push or memory was changed.
<!-- /source: IMAGE_ORIENTATION_ACCEPTANCE.md -->


---

<a id="message-send-acceptance"></a>

## MESSAGE_SEND_ACCEPTANCE.md

<!-- source: MESSAGE_SEND_ACCEPTANCE.md -->
### 消息发送与停止验收

日期：2026-10-05。此记录整理已完成的上一轮发送链路验收，不代表 Codex 界面还原完成。

#### 原子边界

- `IWorkspaceRepository.SaveTaskWithMessagesAsync` 原子保存任务状态与消息批次。
  Infrastructure 拥有 SQLite 事务，在写入前拒绝跨任务消息，App 不执行 SQL。
- 创建和继续任务的用户消息、最终状态和助手回复、助手与工具消息分段均通过该端口。
  相同消息时间戳按插入顺序读取，不改变数据库结构、不需要迁移。
- 已持久化的助手文本游标避免最终提交失败或工具呈现失败后重复保存部分回复。
- 草稿仅在准备成功落盘后且未被用户改动时清除。仅消费发送时捕获的附件，
  后续输入和新增附件保留；同一任务状态更新不能清空下一条草稿的附件。
- 待保存、发送、真正运行时的停止拥有明确的本地化状态；准备失败保留输入并显示安全提示。
  附件管理器解除集合订阅、支持幂等释放，已释放后不接受迟到的文件选择或读取结果。
- 此改动复用任务、持久化和附件端口，不改变工具授权、审批、超时、取消或输出限制。

#### 测试与运行证据

上一轮先运行的五项回归均失败，分别复现孤立任务、继续任务状态损坏、
未提交助手回复、丢失草稿及过早清空输入。修复后的完整解决方案 1,446 项通过，
解决方案构建 0 警告、0 错误；相关文件空白格式与 `git diff --check` 通过。
测试覆盖触发器拒绝时回滚、跨任务拒绝、取消、等时间戳顺序、助手回复恰好一次持久化、
发送中继续输入、新附件、导航、管理器释放及明暗主题错误提示。

实际 App/MainWindow 宿主、Python 被动截图与日志保存于：
`%TEMP%/MossAgent-send-transaction-qa-20261005-01`。

| 最终截图 | 原始像素尺寸与状态 |
| --- | --- |
| `failure-light-max-final.png` | 1920×1032，保存拒绝后保留草稿与附件 |
| `failure-dark-normal-final.png` | 1280×820，同一失败状态 |
| `cancel-dark-normal-final.png` | 1280×820，停止后保留部分回复、下一条草稿与附件 |
| `cancel-light-max-final.png` | 1920×1032，同一停止恢复状态 |

失败日志确认零任务、零模型请求、保留一份附件。停止场景使用真实本机 loopback SSE
请求，SQLite 中任务为 Cancelled，仅有一条助手回复，内容为：
“这是已流出的部分回复，停止后应只保存一次。”
所有最终日志均记录普通权限、持久化核对、窗口关闭和正常退出。
旧的非 `final` 图片存在窗口状态或附件可见性问题，不作为最终证据。

#### 限制

- 使用独立临时配置和 SQLite、合成附件与本机 SSE 服务，无付费模型调用，
  不读取真实凭据或修改真实项目文件。程序化设置状态和执行真实命令不是物理鼠标、
  系统键盘或原生文件选择器验收；截图本身不证明整个界面逐像素匹配 Codex。
- 历史工具上下文被过滤的问题不属于此轮发送事务修复；后续独立修复与验收见
  [HISTORY_CONTEXT_ACCEPTANCE.md](CONVERSATIONS.md#history-context-acceptance)。
- 仍有三个预先存在的超长方法未改动，不声称仓库所有方法规模已通过。
- 没有暂存、提交或推送；截图脚本、宿主与图片不加入仓库。
<!-- /source: MESSAGE_SEND_ACCEPTANCE.md -->


---

<a id="native-submission-acceptance"></a>

## NATIVE_SUBMISSION_ACCEPTANCE.md

<!-- source: NATIVE_SUBMISSION_ACCEPTANCE.md -->
### Native submission and EXE interruption acceptance

#### Scope and identity (2026-10-19)

`artifacts/native-submission-20261019` uses the existing production Debug/JIT
`MossAgent.App.exe`, a new synthetic data directory and a local compatible-protocol
HTTP/SSE fixture. Settings are seeded through production persistence ports;
UI actions use the Computer Use skill/Sky. Neither a user database nor a real
provider, credential, project, existing app window or daily browser participates.

The App DLL SHA-256 is
`3E8F2A0F926CAC62C80A334049A4DC0F1C38A9459DC2C1F558C63A19E2E57548`,
identical to the App DLL in the complete 8,021-case frozen Debug regression.
Launch records bind each test PID to its exact executable path and assembly hash.
The fixture uses raw existing assemblies and does not rebuild production code.

#### Established path

1. A fresh production EXE opens the no-project composer with the saved default
   fixture model. Sky types `native-first` in the observed focused editor and
   clicks the observed actual send button.
2. The local SSE provider receives exactly one real request. Native accessibility
   shows the submitted message, `native-answer-1`, the first streaming turn and
   the enabled stop button. Production repository reads while the GUI is running
   show one Running task, a null ProjectId, one user message and empty consumed
   source/target drafts. The inspector deliberately does not run database recovery.
3. The actual request uses model native-fixture, medium reasoning, temperature
   0.2, Top-P 0.9 and output limit 1,024, with no tools field. The projectless
   system instruction declares no working directory, authorized roots or tools.
4. Only the recorded test process tree is killed after path verification, while
   the controlled stream is still pending. Starting the same actual EXE with
   the same isolated data restores the task as Interrupted, retains the submitted
   user message once and leaves the consumed drafts empty. The native accessible
   conversation and its directory row remain present. No assistant completion is
   claimed: the deliberately interrupted partial stream was not finalized.
5. Provider request count remains one after restart. There is no automatic model
   call or replay. Both test EXEs and the fixture server subsequently exit; only
   those owned PIDs are terminated. This is forced termination, not graceful
   window-close acceptance.

The persisted state, selected native accessibility snapshots, request summary,
launch identities and assertions are retained in the artifact directory.
`native-evidence-terminal.json` records successful checks and explicit limitations.

#### Input and visual limitations

Native screenshot capture returns black frames. A fresh window binding still
provides a readable accessibility tree, but does not fix pixels. Subsequent
composer-focus input fails with `failed to activate captured window`; after one
fresh binding and retry, it fails with `GetCursorPos failed: Access denied
(0x80070005)`. No further clicks or key input are attempted. These errors are
recorded as an automation limitation, not established MossAgent input defects.

This establishes native typing, sending, streaming observation and EXE restart
state for one projectless conversation. It does not establish native screenshots,
typing another draft during streaming, stop-button input, graceful close,
attachments, project variants, submission-time cancellation or a kill during the
SQLite commit. Those branches retain their separate automated/headless/CLI
evidence and unfinished native status. No policy is loosened to make this pass.
<!-- /source: NATIVE_SUBMISSION_ACCEPTANCE.md -->


---

<a id="projectless-attachments-acceptance"></a>

## PROJECTLESS_ATTACHMENTS_ACCEPTANCE.md

<!-- source: PROJECTLESS_ATTACHMENTS_ACCEPTANCE.md -->
### Explicit attachments in projectless conversations

The overall functional/workflow parity goal remains active. This extends the
existing projectless plain-chat and image-attachment flows without granting a
project or directory permission. Native selector/restart and real-provider
acceptance remain incomplete; this is not whole-product acceptance.

#### Plan and required design self-check

1. Application owns a separate explicit-selection reader port; Infrastructure
   implements bounded exact-file reads and snapshot persistence. App calls it only
   after a user-initiated picker, never from a model tool. Existing project reads
   retain their root checks. No fake ProjectProfile or parent-directory grant.
2. Reuse the real text/image decoder, immutable binary stores, draft CAS, user
   message metadata, history hydration and original-turn native provider blocks.
   A selection-specific stamp distinguishes projectless snapshots from project
   authorization stamps; restoring a snapshot never re-reads its source path.
3. Read only the captured absolute files, reject links/reparse points in their
   ancestry and do not enumerate/read sibling files. Keep thirty-second I/O,
   cancellation, 10-file/text/image limits and model capability checks. Navigation,
   shutdown and authorization changes invalidate pending picker/reader results.
   Projectless Agent runs still have no roots, tools, subagents, browser or shell.
4. Use a forward SQLite migration preserving existing metadata and snapshot paths.
   Retain one handwritten type per file and existing file/method limits. Reuse
   composer wrappers and centralized labels; no new per-view visual variants.
5. Prove real selected-file/snapshot/SQLite round trips, independent composition
   reopen, scope switching, first-send promotion, later history, missing/corrupt
   snapshots, forged stamps, relative/link paths, limits, cancel/timeout and stale
   selection refusal. Verify actual loopback requests advertise no tools and
   preserve user-role text/image attachments; no native/paid-model claims.

#### Implemented workflow and safety boundary

- App exposes the existing attachment button in plain chat, with centralized
  localized labels and an accessible name. Selection is user initiated. App
  routes exact picker paths to `ISelectedAttachmentReader`; the port is not
  registered as an Agent tool. The production reader is shared with the project
  reader, but the two authorization entry points remain distinct.
- The selected-file reader captures the input list before asynchronous work,
  requires absolute paths, rejects Windows ADS/device/GLOBALROOT paths and
  link/reparse ancestors, and reads only the named files. A parent directory is
  never added to task roots. File count, UTF-8 size, actual image decoding, image
  count/aggregate size, cancellation and thirty-second read limits remain active.
  App also refuses relative picker results and incomplete/foreign reader results.
- `CreateSelection` hashes the normalized path with the
  `explicit-file-selection:v1` domain prefix. This is an internal consistency
  marker, not an unforgeable authorization token. Project authorization stamps
  are not accepted as projectless draft stamps. Restoring a draft/history entry
  reads its internal immutable snapshot, never its original source path.
- Version 17 changes the draft-attachment scope trigger to require an actual
  parent draft row, including a projectless row. It does not rewrite existing
  snapshots or metadata. Migration failure rolls back the trigger/version and
  leaves the database retryable; existing project drafts remain readable.
- Text/static-image drafts share text revision checks and remain scoped to a
  new conversation or a particular task. Successful first sends consume only
  the captured selection, while navigation and late picker/reader completion
  cannot populate another scope. Sent attachments remain original-user-turn
  inputs on follow-up requests and independent composition reopen.
- Missing/corrupt snapshots retain text and removable unavailable chips, block
  sending, and recover after explicit removal. An unsupported model retains the
  image draft and disables sending; enabling/selecting supported capabilities
  restores sendability. Images can be sent without a text body.
- A selected file does not create a project/worktree, authorize a directory or
  enable shell, terminal, browser, tools or subagents. Real loopback requests
  still advertise an empty tool list. Agent tests additionally inject unsolicited
  calls with an image under ReadOnly, AskEveryTime and FullAccess: all reject
  execution with `tools_disabled`, without retrying the provider or reading the
  tool catalog. The normal tool pipeline retains its independent disabled-tools
  boundary.

##### Startup and navigation semantics

Startup still selects the latest existing task when one exists. A saved
projectless-new-chat draft is not automatically inserted into that task. Choose
New Chat to return to the independent unsent draft. The integration test first
asserts this isolation, then explicitly navigates to the new-chat scope and
verifies text/image restoration after deleting the original selected file.
It does not claim startup automatically restores the last visible page.

#### UI regression correction

The first expanded run completed with 298 passes and six failures; its original
`results/app-attachments.trx` is retained. Four failures were obsolete assertions
that projectless chat must hide attachments. They now require a visible/enabled
button with a null project parameter, while still requiring the project approval
control to be hidden.

Two failures exposed a real responsive-layout bug: toolbar reflow only reacted
to size changes. Once attachments remained visible, switching project mode could
change approval visibility without changing toolbar size, leaving a stale stacked
layout. The view now recalculates on approval visibility as well as size changes,
and pairs that event subscription with visual-tree detach cleanup. This remains
view-owned layout behavior, with no new I/O, permissions or per-view appearance.
Both themes cover repeated 320/360/460/800-width changes, project/plain-chat
switches, model/send placement and the retained attachment action. The focused
19-case rerun and expanded 304-case rerun both pass; no failing test was skipped.

#### Verification checkpoint

Candidate: `artifacts/projectless-attachments-20261018`, Debug/JIT, 2026-10-18.
All paths below are relative to that candidate directory.

| Scope | Final evidence |
| --- | --- |
| Infrastructure full project | **328/328**, `results/infrastructure.trx`: exact-file reader boundaries, real codecs/files/SQLite, v17 migration/rollback, existing draft/history and project-root rejection cases |
| Providers full project | **600/600**, `results/providers.trx`: four native image envelopes, streaming and request/body limits; does not prove paid-provider image understanding |
| Agent full project | **137/137**, `results/agent-final.trx`: image context and all-policy projectless unsolicited-call rejection, plus repository-wide single-type/file-limit checks |
| App focused regression | **304/304**, `results/app-attachments-final.trx`: all attachment/composer/projectless-chat cases plus model-selector coverage; includes 13 explicit projectless-attachment cases |
| Tool-pipeline projectless boundary | **3/3**, `results/projectless-tool-boundary.trx`: disabled tool execution remains denied under all three approval policies |
| UI/projectless narrow rerun | **19/19**, `results/projectless-ui-fixed.trx`; overlaps the 304 App cases and is not counted twice |
| Solution build | `build-final.log`, exit 0, **0 warnings, 0 errors** |
| Changed/untracked handwritten C# | `source-shape-final.json`: 135 files, exactly one type each, maximum 497 file lines / 67 method lines, no syntax/size violations; whitespace check passes |

The primary project totals plus the separate tool-pipeline cases are **1,372
passing tests**, not a complete solution-wide test run or product completion.
The earlier 12-case workflow result predates the additional submitted-history
reopen case; that new case is included in the final 304-case run. Earlier failed
results remain evidence of the corrected defects, not final-candidate failures.

Reproduction commands, from the repository root:

```powershell
dotnet build MossAgent.slnx --artifacts-path artifacts/projectless-attachments-20261018 --nologo -v minimal
dotnet test tests/MossAgent.Infrastructure.Tests/MossAgent.Infrastructure.Tests.csproj --artifacts-path artifacts/projectless-attachments-20261018 --no-build --logger 'trx;LogFileName=infrastructure-recheck.trx' --results-directory artifacts/projectless-attachments-20261018/results --nologo -v minimal
dotnet test tests/MossAgent.Providers.Tests/MossAgent.Providers.Tests.csproj --artifacts-path artifacts/projectless-attachments-20261018 --no-build --logger 'trx;LogFileName=providers-recheck.trx' --results-directory artifacts/projectless-attachments-20261018/results --nologo -v minimal
dotnet test tests/MossAgent.Agent.Tests/MossAgent.Agent.Tests.csproj --artifacts-path artifacts/projectless-attachments-20261018 --no-build --logger 'trx;LogFileName=agent-recheck.trx' --results-directory artifacts/projectless-attachments-20261018/results --nologo -v minimal
dotnet test tests/MossAgent.App.Tests/MossAgent.App.Tests.csproj --artifacts-path artifacts/projectless-attachments-20261018 --no-build --filter 'FullyQualifiedName~Attachment|FullyQualifiedName~Composer|FullyQualifiedName~WorkspaceSendDraft|FullyQualifiedName~ProjectlessChat|FullyQualifiedName~ModelSelectorComponentTests' --logger 'trx;LogFileName=app-attachments-recheck.trx' --results-directory artifacts/projectless-attachments-20261018/results --nologo -v minimal
dotnet test tests/MossAgent.Tools.Tests/MossAgent.Tools.Tests.csproj --artifacts-path artifacts/projectless-attachments-20261018 --no-build --filter 'FullyQualifiedName~ProjectlessToolBoundaryTests' --logger 'trx;LogFileName=projectless-tool-boundary-recheck.trx' --results-directory artifacts/projectless-attachments-20261018/results --nologo -v minimal
```

#### Remaining scope and evidence limits

These are this checkpoint's original boundaries. Selected/draft and sent/history
thumbnails were subsequently added and validated in
[ATTACHMENT_THUMBNAIL_ACCEPTANCE.md](CONVERSATIONS.md#attachment-thumbnail-acceptance); that evidence does not establish native
picker/restart, full-size viewing, EXIF, PDF or paid-provider acceptance.
JPEG preview orientation is subsequently covered by
[IMAGE_ORIENTATION_ACCEPTANCE.md](CONVERSATIONS.md#image-orientation-acceptance); other-format/native-image orientation remains
unverified.

- Tests use temporary files/databases, production service composition and actual
  loopback HTTP with scripted responses. Picker results are scripted. Keyboard,
  theme and layout checks use Avalonia Headless, not native Windows selector
  clicks, desktop screenshots or an installed EXE restart. No existing user
  window, real account, credential or paid model was accessed.
- Thumbnails, PDF/native documents, child-agent image sharing, real-provider
  accepted formats/interpretation and native picker/restart remain open. The
  conservative image context estimate and native decoder cancellation limits
  from the prior image-workflow checkpoint still apply.
- Link checks are not a claim of eliminating every filesystem TOCTOU race.
  Snapshot writes and SQLite commits remain separate; failed writes may leave
  unreferenced files and abrupt submission exit may restore an unsent draft.
  Restored drafts never execute or send automatically.
- Earlier long-running sessions `21912` and `11377` were re-polled and remain
  live without new output; their associated dotnet/vstest/App processes were
  checked. They were not stopped, restarted or overwritten. Earlier allocation
  failures remain unresolved and neither run establishes a successful current
  complete App suite. The focused results above do not erase those limitations.
- No commit, push, publish, AOT or trimming change was made. The full functional
  parity goal remains active; [FUNCTIONAL_PARITY_MATRIX.md](OVERVIEW.md#functional-parity-matrix) retains the gaps.
<!-- /source: PROJECTLESS_ATTACHMENTS_ACCEPTANCE.md -->


---

<a id="projectless-chat"></a>

## PROJECTLESS_CHAT.md

<!-- source: PROJECTLESS_CHAT.md -->
### 无项目聊天

- 没有项目时，配置好提供商和模型后即可直接发送消息，无需创建目录。
- 已有项目时，可从侧栏项目菜单或输入框上方的项目选择器选择“无项目聊天”。
- 普通聊天与项目任务共用流式回复、停止、历史、近期、搜索、置顶、归档、
  标签页和前进后退导航；重启后可打开历史并继续同一会话。
- 项目内的“新对话”仍沿用当前项目，不会改变已有项目工作流。

#### 边界与数据

Domain 的 `AgentTask.ProjectId` 允许为空，空值表示真正无项目的会话，
不是临时项目、当前目录或隐藏的授权目录。Application 的工作区仓库接口
以空项目 ID 查询普通聊天，SQL 使用空值安全比较，项目任务和聊天互不混合。

Infrastructure 的 v12 前向迁移在事务中重建任务表，保留消息、审计、附件
元数据、置顶、归档、失败原因与会话偏好。迁移连接暂时关闭外键级联，
提交前检查全部外键，失败或取消时回滚，最后恢复外键。无项目会话不能绑定
worktree，保存已有任务不能改变其项目归属。迁移不删除历史，也不自动重放任务。

App 继续通过现有任务工厂、仓库、运行器与面板协调器编排，不直接访问
SQL、文件或提供商 HTTP。普通聊天没有工作目录、授权根、浏览器会话或工具
产物目录，终端、预览和差异面板不附加项目上下文；隐藏项目审批按钮。

无项目聊天可以通过附件按钮明确选择 UTF-8 文本和静态 PNG/JPEG/GIF/WebP。
独立的 `ISelectedAttachmentReader` 只读取本次选中的绝对文件，不把文件父目录
变成授权根，不创建临时项目，也不启用工具、终端、浏览器或子代理。相对路径、
Windows ADS/device 路径及包含链接/重解析点的路径被拒绝。模型不支持图像时
保留附件并禁发，切回支持模型后恢复；图片也可以不附带文字单独发送。

v17 前向迁移只调整草稿附件的作用域约束，保留现有文件与元数据。未发送附件
与文本共享草稿版本检查，按新聊天或已有任务分别恢复；已发送附件使用内部
不可变快照，不因源文件删除而丢失。启动仍优先打开最近任务；如果未发送草稿
属于“无项目新聊天”，需要点击“新对话”回到该作用域，不会混入其他任务。
具体证据和限制见 [PROJECTLESS_ATTACHMENTS_ACCEPTANCE.md](CONVERSATIONS.md#projectless-attachments-acceptance)。

工具上下文的 `ToolsEnabled=false` 是额外的能力边界，不是仅把审批策略
改为只读。Agent 不提供工具描述或子代理目录，并拒绝提供商主动返回的工具调用；
标准工具执行器也在能力验证阶段拒绝所有工具，包括只读文件工具。项目模式
继续使用原有授权、审批、超时、取消、输出限制和审计管线。

#### 验证范围

- `ProjectlessChatFlowTests`：零项目发送、续聊、同项目隔离、导航、搜索、
  置顶、归档恢复、停止及无项目上下文的安全负例。
- `ProjectlessChatRestartTests`：独立服务实例重启后恢复并继续聊天。
- `ProjectlessChatComposerTests`：宽屏浅色与窄屏深色界面的发送按钮、键盘提交
  与项目相关控件可见性。
- `ProjectlessChatAgentTests` / `ProjectlessToolBoundaryTests`：不提供工具、
  不执行未声明调用，所有审批策略下仍拒绝工具。
- `SqliteProjectlessChatTests`：空项目持久化、作用域隔离、迁移数据保留、
  级联关系、失败回滚、外键损坏与重试。

模型流使用本地确定性 HTTP 服务，不调用付费模型；界面验证使用 Avalonia
Headless，不等同于原生桌面截图验收，也不重启用户正在使用的窗口。

原始无项目聊天阶段验证：App 相关测试 220 项、Agent 全部 123 项、Tools 全部 494 项、
Infrastructure 全部 204 项通过，共 1041 项。解决方案构建零警告、零错误，
本次修改的 C# 文件通过空白格式检查；未完成整个解决方案的全量测试或原生截图验收。
<!-- /source: PROJECTLESS_CHAT.md -->


---

<a id="queue-snapshot-completion-acceptance"></a>

## QUEUE_SNAPSHOT_COMPLETION_ACCEPTANCE.md

<!-- source: QUEUE_SNAPSHOT_COMPLETION_ACCEPTANCE.md -->
### Queue snapshot completion observation

The first original Debug browser/Queue selection after browser cancellation
integration passes 211 checks and fails one enabled-agent snapshot assertion:
expected two requests, observed one. The original Release selection passes 212.
Both reports remain under artifacts/browser-timeout-runtime-20261019; neither
result substitutes for the other or for a complete regression.

#### Boundary and deterministic evidence

Workspace IsRunning includes active preparation. However, the predecessor is
removed from the tracker before its RunFinished subscribers hand off to Queue.
An observation between those actions can see IsRunning=false with one completed
request while the queued input is still saved. An existing public completion
event observer is now held before the Queue subscriber using a test-only fixture
configuration callback. With the old wait, both branches fail deterministically:
enabled expects two requests but sees one; disabled expects a refusal notice but
sees none. See observer-baseline.log/TRX under
artifacts/queue-snapshot-completion-20261019.

An earlier attachment-preparation gate was too late to reproduce this observation:
preparation already counts as IsRunning. Its timeout remains recorded separately,
not presented as the same failure. The first corrected candidate also asserted a
visible Queue row during the catalog's asynchronous refresh and saw an empty
projection. That assertion now inspects the exact persisted pending entry at the
held handoff; final outcomes still inspect the visible Queue.

#### Change and required self-check

Only App.Tests changes. The fixture's optional callback uses the existing
WorkspaceRunController event and leaves production composition, dependencies and
behavior unchanged. The held boundary proves the predecessor is absent, exactly
one request exists and the exact queued entry remains persisted, then always
releases the observer. Completion waits require an idle run/Queue and a visible
failure or a subsequent request. The attachment tests use the same terminal
outcome rule. No delay increase, alternate model, approval relaxation or retry
masks a failure.

All chosen model/reasoning, immutable saved agent prompt, disabled-agent rejection,
attachment integrity, request counts and UI assertions remain. Enabled dispatch
also requires an empty visible Queue and exactly one committed user message;
disabled dispatch must retain the entry and persist no dispatched message.
Existing deadlines/cancellation, production SQLite and simulated loopback HTTP
remain in use. One type per file and file/method limits remain required. No tool,
provider, authorization, migration, network or native process behavior changes.

#### Verification and limits

The independent four-case snapshot selection passes; Debug and Release expanded
browser/Queue selections each pass 212 checks. The two original files were merged
only after their hashes matched saved baselines, with candidate hashes checked
after copying. integration-manifest.json and merged-files.json retain identities.
Original-checkout Debug/Release complete builds pass with zero warnings/errors,
and both configurations' expanded selections pass 212 checks with no failures or
skips. Structure/whitespace checks cover 577 C# files, 29 AXAML files and 3,184
methods/accessors, maximum 78 lines, with no violations. The new complete Debug
regression session 93473 under artifacts/browser-cancellation-integrated-20261019
has ended: 8,136 passed / 2 failed, 8,138 total, no skipped/aborted tests. Eight
reports and 2,414 source/configuration plus 2,035 runtime identities match; its
freeze has ended. The two observation failures and their guarded test-only fix
are retained in RUNTIME_OBSERVATION_BOUNDARIES_ACCEPTANCE.md.

This identifies an insufficient completion observation, not every historical
test failure or a product dispatch fix. The held public event represents a
controlled handoff schedule; it does not prove the exact timing of the original
failure. Native GUI, real AI providers, a new complete regression and the command
filesystem isolation blocker remain separate acceptance work.
<!-- /source: QUEUE_SNAPSHOT_COMPLETION_ACCEPTANCE.md -->


---

<a id="queue-tool-audit-assertion-acceptance"></a>

## QUEUE_TOOL_AUDIT_ASSERTION_ACCEPTANCE.md

<!-- source: QUEUE_TOOL_AUDIT_ASSERTION_ACCEPTANCE.md -->
### Queued write audit identity assertion

Status: integrated into the original checkout after complete regression 55845
terminated and its source/runtime identities were checked. That failed run reports
a failure in
QueuedWritesUseTheirSavedPolicyAndPublishRealResultsAndAudits(AskEveryTime, true):
its unfiltered Assert.Single sees the queued write and an in-flight background
filesystem.change_statistics audit. This does not establish a duplicate write.

#### Deterministic evidence and change

The independent copy under artifacts/queue-runtime-workspace-20261019 adds an
explicit wait until the real production background statistics audit appears,
then preserves the old Assert.Single. This deterministically fails the allowed
write case with exactly those two audits; both refused-write cases pass.
The unchanged original and instrumented baseline source snapshots, TRX and log
are retained under artifacts/queue-audit-statistics-20261019/direct-baseline.

The candidate keeps the wait but now awaits a completed statistics audit. It
requires exactly two audits for a successful write, exactly one for either
refusal, exactly one filesystem.write_file, exact task identity for every audit,
and only a successful completed read-only statistics call with the expected call
prefix/policy/approval result as the other audit. Existing queued-write CallId,
saved policy, approval allow/refuse, error, actual file bytes, visible tool result,
persisted history, draft and Queue assertions remain unchanged. No filtering
hides an unknown extra tool, duplicate write, unfinished statistics or task leak.

The first candidate's five boundary cases pass. The tightened final candidate's
35 Queue cases pass in both Debug and Release with no failures/skips. These are
two configuration runs of overlapping cases, not seventy distinct scenarios.
No production tool, approval policy, timeout, output, persistence or audit code
changes. The seven-file guarded integration has zero baseline conflicts. Original
Debug/Release full builds and Browser suites pass; the related App selection's
Release 212 checks pass. Its first Debug run has 211 passes and one separate Queue
snapshot completion failure; the tightened audit cases pass in both configurations.
The original full run remains 8,121 pass/one failure, 8,122 total, no skips or
aborts, with all 2,411 source/configuration and 2,035 runtime paths/hashes unchanged.
See BROWSER_OPERATION_CANCELLATION_ACCEPTANCE.md for integration/runtime evidence
and QUEUE_SNAPSHOT_COMPLETION_ACCEPTANCE.md for the separate assertion boundary.

#### Required self-check

App.Tests owns these integration assertions; module dependencies and production
ports are unchanged. Production DI, ToolExecutor, approvals and SQLite remain in
use, with the fixture's existing deadline and cancellation. No native shell or
browser is involved. The single modified C# type remains in one file below 500
lines; all methods must remain below 80 lines. Success plus ReadOnly/approval
denial, queued approval cancellation and incomplete-call refusal remain covered.

This identifies the direct assertion error, not every historical failure or the
specific timing of the original interleaving. No passing full regression is
claimed; the original failed run and all earlier failed records remain intact.
<!-- /source: QUEUE_TOOL_AUDIT_ASSERTION_ACCEPTANCE.md -->


---

<a id="send-preparation-cancellation-acceptance"></a>

## SEND_PREPARATION_CANCELLATION_ACCEPTANCE.md

<!-- source: SEND_PREPARATION_CANCELLATION_ACCEPTANCE.md -->
### Send preparation cancellation

#### Scope and required design self-check

1. **Ownership:** App owns preparation lifetime and composer state. Application
   owns the typed Git creation failure contract; Infrastructure reports a
   retained managed path after owned Git execution returns. Dependency direction
   remains unchanged; UI performs no SQL, filesystem or process operations.
2. **Existing ports:** reuse workspace persistence, attachment snapshots,
   `IGitWorktreeService`, run tracking and the existing shutdown coordinator.
   No database migration, provider protocol or tool capability is added.
3. **Safety/lifetime:** each preparation owns its cancellation source and tracked
   completion. Cancellation requests must not abandon an outstanding mutation.
   Recovery and final status persistence use an independent 30-second cooperative
   deadline. Preserve approval, authorized roots, output limits and process
   ownership; never force-remove a worktree or automatically replay a message.
4. **Size:** preparation ownership and typed exceptions have dedicated one-type
   files. Keep the existing workspace within 500 lines and methods within 80;
   run the changed-source shape audit before handoff.
5. **Proof:** cover pre/post-commit cancellation, input/attachment preservation,
   independent conversations, uncooperative writes, shutdown, finalization
   failures, partial Git creation and actual keyboard activation. Use temporary
   Git/SQLite, a loopback provider and production Headless controls, not paid
   services or existing user windows.

#### Behavior

- The primary button shows a stop glyph and **取消发送准备** during preparation.
  After activation it shows **正在取消发送准备** and cannot be activated again
  until preparation has safely returned. Enter/Ctrl+Enter in the editor never
  submit again or cancel the pending send. Space on the focused button cancels.
- Keep original or subsequently edited text and selected attachments. Never
  consume their draft identities or start a provider after preparation is
  cancelled. Cancellation of one preparation does not cancel an independent
  conversation using the same runtime tracker.
- If persistence committed before observing cancellation, mark the task
  Cancelled using an independent token. Keep its history and owned worktree;
  expose a new task without navigating away from the current draft scope.
- If final status cannot be confirmed, keep visible task metadata and show a
  localized instruction containing its task ID: refresh and inspect before
  resending. Do not expose raw database/Git diagnostics or falsely report
  successful cancellation. The composer does not show a contradictory generic
  retry hint beside this recovery instruction.
- For a failure after worktree creation, query both active and archived task
  ownership before ordinary removal. Ambiguous ownership or failed cleanup
  retains the directory. A creation-time Git failure/cancellation with an
  existing managed directory reports its path instead of blindly deleting it.
- Shutdown joins the preparation. Synchronous disposal requests cancellation
  and detaches notifications; asynchronous disposal also joins it. Local
  workspace disposal is not global cancellation of all shared runtime tasks.
- Snapshot writes, Cancelled-status writes and unowned-worktree removal retain
  their actual operation rather than using `WaitAsync` to abandon mutations.
  Independent recovery deadlines request cancellation but do not release an
  uncooperative write. Late snapshot results are rejected before task/message
  persistence. A manual clock tests the 30-second deadline without real waiting.

#### Shared composer layout

The environment picker adds a genuine third action beside project and agent.
At narrow widths the environment and branch move together to a second row;
project/agent values stay shrinkable and their buttons retain the full labels.
Shared appearance rules let this surface grow instead of clipping its second
row. Projectless controls remain hidden and grant no directory access.

When that extra row leaves less vertical space for an empty conversation, the
shared welcome layout uses a short-viewport appearance: smaller brand and
heading, finite measured height, and restoration of the original appearance
after resizing. It does not move the composer over the heading, alter model
state, remove the project action or shrink unrelated popup text.

#### Validation

Candidate: `artifacts/send-preparation-cancellation-20261018`.

| Check | Terminal result |
| --- | --- |
| Final focused cancellation, mutation ownership, keyboard, context overflow and empty-session layout | 61/61 passed, no skips |
| Final wider related App checks, including sidebar allocation and worktree flows | 319/319 passed, no skips |
| Final complete shared visual/component suite | 300/300 passed, no skips |
| Final complete Infrastructure suite | 392/392 passed, no skips |
| Final attachment history, lifecycle and projectless boundary checks | 48/48 passed, no skips |
| Final complete solution build | 0 warnings, 0 errors |
| Changed/untracked handwritten C# shape audit | 205 files, no violations; maximum 499 file lines and 72 method lines |

All final runs are terminal. Counts are per-run and overlap; do not sum them as
unique cases. TRX reports are under `results/validated-focused`,
`validated-related`, `validated-visual`, `validated-infrastructure` and
`validated-attachment-boundaries`. The build log is `build-final.log`.
`terminal-results-summary.json`, `validated-source-identity.json`,
`validated-binary-identity.json` and `validated-identity-check.json` identify the
candidate and retained results. The identity checks cover 2,052 source/config
files and 105 copied/module/test binaries. Changed AXAML also stays within its
500-line limit, with App markup at 499. Retain initial failed builds and TRX
results; do not substitute earlier candidates' passing counts for this revision.

This is not a new complete solution test run. The older frozen full regression
remains 7,154 passed and 1 failed across 7,155 cases; its sidebar allocation
failure is historical evidence, not erased by this candidate's six passing
allocation checks. Neither that result nor these slice checks completes the
product goal.

The initial broader run finished with 292 passed and 8 failed. Three failures
were obsolete primary-button/stop-glyph assumptions; three were markup/action
assumptions predating the environment picker; two exposed real branch-label
compression. The old expectation that Enter cannot submit again is retained
and now also verifies that Enter never invokes the new cancel action.
An intermediate two-row attempt failed 9 of 83 checks: type selectors did not
match the UserControl wrapper's style key, leaving the old fixed height; hiding
the content host also deferred creation of its projectless control, and the
legacy no-environment component unnecessarily changed rows. Class-based shared
variants and environment-aware reflow fix these without weakening containment,
non-overlap, availability, icon geometry or projectless access assertions.
The intermediate `Height=Auto` build error was corrected to Avalonia's `NaN`
value. These original failure logs are retained.

Revision 5 then passed 86 focused checks and 48 attachment-boundary checks,
but its broader App run finished 300/304: all four failures were the compact
empty-conversation heading overlapping the newly taller composer by five
pixels. Its shared visual (300/300) and Infrastructure (392/392) runs also
finished; those passes did not make the App failure disappear. The welcome
layout previously returned its unconstrained natural height even when the
remaining viewport was smaller. The shared short-viewport variant and finite
measurement fix that cause. Coverage now includes actual saved agent profiles,
light/dark themes, long project names and restoring the wide appearance.

Sixteen revision-5 Headless screenshots remain under `images-final`: preparation
and cancellation in light/dark themes at 526/1000, long labels at 360/400/740,
and the full main window with its inspector docked. Light/narrow, dark/docked
and dark/cancelling frames were inspected directly. This is rendered production
control evidence, not native Windows input or EXE restart verification.
Final renders are recorded separately under `images-validated`, including the
saved empty conversation with and without a configured agent. All 28 frames
were generated; final light/long-name/agent and dark/short-name/no-agent empty
conversation frames were inspected directly in addition to the earlier frames.

#### Remaining limits

- No native EXE restart, original-window interaction, paid model call or hard
  process-crash recovery is established by Headless/loopback checks.
- A cancellation request cannot force a noncooperative persistence port to
  return. Continue owning mutations until they return rather than permitting
  a second send or closing storage underneath them.
- Snapshot files, SQLite and Git do not form one distributed transaction.
  Unreferenced snapshots can remain, and abrupt exit can restore an older draft;
  restored input is never sent automatically.
- A retained path is recovery information, not proof that a partial worktree or
  branch is healthy. Branch/base selection, automatic branch deletion,
  creation-time crash recovery and complete worktree link/reparse/race auditing
  remain incomplete or unverified.
- This slice does not establish complete Codex Desktop functional parity.
<!-- /source: SEND_PREPARATION_CANCELLATION_ACCEPTANCE.md -->


---

<a id="task-failure-acceptance"></a>

## TASK_FAILURE_ACCEPTANCE.md

<!-- source: TASK_FAILURE_ACCEPTANCE.md -->
### 安全失败类别与统一提示验收

#### 2026-10-07 Gemini 边界补充

- `ProviderBlocked` 和 `ProviderOutputLimit` 是新的稳定允许列表类别；V8 在事务内
  扩展 V7 的类别约束，保留旧失败、消息、项目、授权目录及任务状态。
  迁移、回滚和重试回归包含在 Infrastructure 完整测试中。
- Gemini 签名、原生调用身份和快照上限失败复用 `InvalidProviderResponse` 与
  `AppTaskFailureNotice`，不回显签名或推理正文，也不增加页面独立样式。
  普通权限实际输入的缺失签名场景确认失败提示、文件不变和工具审计 0，
  截图及范围见 [GEMINI_CONTINUATION_ACCEPTANCE.md](PROVIDERS.md#gemini-continuation-acceptance)。

#### 实现与约束自检

- Domain 拥有稳定的 `TaskFailureReason`，Application 只映射允许列表中的固定
  代码和异常类型，不读取异常消息。未知或伪造代码统一为 Unknown/run_failed。
- App 通过现有工作区端口保存类别，以本地化资源生成可操作提示，不展示或保存
  原始供应商错误、异常正文和未知代码。类别并不保证供应商所有错误都被精确识别。
- `AppTaskFailureNotice` 拥有共享卡片、12-DIP 警告图标和有限宽度换行；
  `SurfaceVariants.axaml` 维护对齐与外边距。页面只绑定 Hint 与可见状态，
  MOSSUI002/ValidateSharedTaskFailureNotices 拒绝替换组成或覆盖尺寸和内边距。
- Infrastructure 的 V7 新增 `agent_task_failures`，只存任务 ID 与 0–10 的类别，
  与任务状态和消息在原有事务中更新。外键和 CHECK 拒绝非法数据，不改写旧消息。
  普通、置顶和归档查询恢复类别；旧任务无类别时继续使用安全通用提示。
- 用户取消仍为 Cancelled，不成为提供商超时。手动继续及非失败状态清除类别，
  恢复历史只呈现，不重放模型或工具。后台任务不会替换当前会话的提示。
- 不新增工具、文件访问、网络请求或应用端口，不改变授权、审批、执行超时、
  取消及输出限制。UI 不直接执行 SQL；每个新增 C# 文件保持一个类型，
  文件与方法不超过工程硬限制。下面的回归同时包含成功与拒绝路径。

#### 自动验证

- Infrastructure 专项 26 项通过：全部合法类别、三种任务目录、非失败清除、
  任务隔离、非法值与孤立外键拒绝、V6→V7 保留和重复初始化、事务回滚及跨任务拒绝。
- App 失败/兼容流式/原子工作流专项 84 项、Responses 会话专项 2 项通过：
  已知/未知代码、敏感正文拒绝、异常类型、取消、继续、背景任务、工具摘要安全，
  以及限长失败后的部分回复和类别保存、重开后的消息 ID 不变与无工具执行。
- 共享组件在明暗主题、320/640-DIP 宽度下验证长提示换行且不溢出、提示更新、
  12-DIP 图标和共享卡片阴影；C#/AXAML 拒绝替换 Content、Padding 和尺寸。
- 仓库外实际构建核对对应拒绝原因：原生 Button 为 MOSSUI001，覆盖失败提示
  Content 为 MOSSUI002；原生 AXAML Button 与所有者限定高度分别被对应门禁拒绝。
  合法共享组件和结构布局通过 C# 构建及八项 AXAML 通用门禁。
- 完整解决方案 1,799 项通过，其中 App 1,310 项；解决方案构建 0 警告、0 错误。
  相关 C# 空白格式检查和差异空白检查通过；853 个 C#/AXAML 文件的文件大小、
  单类型和方法体积审计无失败。没有暂存、提交或推送。

#### 普通权限原生验收

- 仓库外宿主加载生产 App/MainWindow，使用隔离临时数据与 loopback 合成服务，
  不接触真实配置、项目、凭据和付费接口；两次日志均确认未提升权限。
- 浅色 1280×820 窗口中实际输入“验证安全失败原因与重启后的提示。”。
  Send 坐标点击因输入几何不可用失败；刷新窗口并确认草稿仍未发送后，
  Ctrl+Enter 成功发送一次。不是重复点击或重复发送，模型请求总计 1 次。
- 失败后部分助手回复保留，界面显示流式单行安全上限与重新发送建议，
  没有工具行或停止按钮，空输入的发送按钮禁用。
- 同一数据集深色最大化 1920×1032 恢复后，原消息、失败类别与提示仍显示。
  只读 SQLite 核对同一任务 ID、Failed、StreamLineLimit、用户/助手各一条、
  工具审计 0、版本 7；合成 Unicode 文件原文未变。两个宿主均正常关闭、退出码 0。
- 恢复宿主的新服务请求计数为 0，只证明该宿主未收到请求。严格的网络无重放证据
  来自保持原 loopback 服务运行的自动工作流测试，不能用不同端口的 0 请求替代。
- Python 仅被动读取原始窗口像素，截图非黑屏，不调整像素尺寸。宿主、脚本、
  图片、日志及数据位于仓库外：
  `C:\Users\wzheng\AppData\Local\Temp\MossAgent-failure-reason-qa-20261006-01`。
  图片为 `native-line-failure-light.png` 与 `native-line-failure-dark-reopen.png`。

#### 未验证范围

- 本次没有真实付费供应商、全部错误类别的物理输入、所有页面、DPI 和悬停状态验收。
  Headless 换行及原生截图不是所有组件与 Codex 逐像素一致的证明。
- 共享组件和强制门禁已覆盖本次用法，整体 MossAgent 功能和视觉目标仍未完成。
<!-- /source: TASK_FAILURE_ACCEPTANCE.md -->


---

<a id="task-menu-lifetime-acceptance"></a>

## TASK_MENU_LIFETIME_ACCEPTANCE.md

<!-- source: TASK_MENU_LIFETIME_ACCEPTANCE.md -->
### Task menu lifetime and native sidebar replay

#### Design and scope

1. App owns this shared-control behavior. `AppTaskNavigationRow` retains its
   existing task parameter and commands; dependency direction is unchanged.
2. Existing Avalonia property and visual-tree hooks suffice. The row closes its
   options on context/target/action-command replacement, effective disablement,
   own or ancestor hiding, and detach. Ancestor subscriptions are removed on
   detach and rebuilt on attach. Title/selection updates do not dismiss the menu.
3. No tool, authorization, approval, timeout, output or persistence contract
   changes. Native replay uses an independent database backup, one paused plan
   and a loopback observer refusing unexpected model requests. Only that copy's
   task pin is changed and then restored; no send, run, archive or delete occurs.
4. Production and test files remain single-type and below 250 lines; no shared
   dimensions, icons, colors, templates or page-local styles are changed.
5. Twenty focused cases cover both themes, invalid owners/targets, detach,
   command replacement, reattachment, removed-ancestor isolation and preserved
   title/selection updates. Menu dismissal never invokes a task action.

#### Reproduction and checks

Evidence stays outside source under
`%TEMP%\MossAgent-task-menu-20261013-1040`.

- The original twelve-case baseline has ten failures and two preservation
  passes. Open flyouts remain after row/parent hide or disable and context change.
- The first repaired group passes eighteen cases. With reattachment coverage,
  the wider sidebar/project/search/flyout/danger group passes 420/420, including
  all twenty lifetime cases. None fail or skip.
- The first complete eight-suite run passes 5,696/5,697. Its single failure is
  the existing `WorkspaceToolProgressOptimizationTests` allocation assertion:
  the recorded measurement is one byte/operation instead of zero. Its separate
  five-case recheck passes. No production optimization or assertion is changed
  to hide that failure. Original TRX evidence remains under `full/`.
- The final sequential eight-suite recheck passes 5,697/5,697: App 4,135, visual
  24, Agent 119, Browser 76, Infrastructure 199, Platform.Windows 137, Providers
  516 and Tools 491. None fail or skip. Its separate completed TRX files remain
  under `serial/`; the earlier failure is not erased or recast as a pass.
- The final solution build passes with zero warnings/errors, and scoped
  whitespace formatting verification passes. An earlier overlapping build
  failed to copy the App DLL while the App test host held it open; the build is
  rerun successfully only after that test process exits. The final production
  assembly hash still matches the native replay. Size and whitespace checks pass.

#### Native evidence ownership

The executing default Debug/JIT App SHA256 is
`8DFEF6A76562ABA8C499C02F728C4D501FEA2FE257E5D1B8A08A6941BB524BA0`.
Its launch, passive PNG/JSON captures, independent data and audits are under
`%TEMP%\MossAgent-narrow-fidelity-20261013-1015\native-menu`.

- Startup is maximized at 1920x1032, DPI 96 and ordinary privileges. Explicit
  activation restores 1280x820; the actual caption action subsequently restores
  maximized size. Both sizes are measured, not normalized by image scaling.
- Initial input is refused because Codex is foreground. Fresh MossAgent
  activation/observation precedes retry. Codex is passively captured only.
- Actual task-menu pin moves task A to the pinned section while keeping its
  selected conversation. Read-only SQLite checks prove only the expected pin
  appears: task rows, messages and archived rows remain unchanged.
- Ctrl+K opens search. A set-value call times out but the next observation proves
  the requested query and empty notice are present; no blind retry is sent.
  Escape dismisses the popup while retaining filtering. Reopening and the real
  clear button restore pinned/project/recent lists and retain the conversation.
- Clicking outside an open task menu first light-dismisses it; a separately
  observed second click hides the sidebar without residual popup. Restoring the
  sidebar leaves the selected pinned task's menu usable. These native actions
  do not replace the deterministic owner-invalidation/reattachment tests.
- Actual unpin restores the original pin set. Catalog rows, all six task rows,
  saved messages, four terminal histories, original audits and three hash-checked
  artifacts remain unchanged. Only the four historical requests exist; no new
  execution or descendant process is observed.
- The actual close button exits zero. The observer stops normally and port 51792
  has no listener. The original process instances exit; a later reused PID is
  not treated as a surviving observer. Original application data is not changed.

#### Codex reference comparison and limits

`sidebar-native-comparison.png` uses equal-size 1920x1032 DPI-96 passive frames
and unscaled crops. Five sampled titlebar/rail/sidebar/main/selected-row colors
match. Search and sidebar-toggle ink bounds match; pinned-heading bounds match.
The project-heading thresholded ink retains a one-pixel left-edge difference.
An initial sidebar color sample landed on Codex's scrollbar; it is corrected
to unobstructed background, not treated as an appearance defect.

Tasks/projects, rail feature availability and context-card preferences differ;
these are shell samples, not whole-window or equivalent-content acceptance.
Higher DPI, dark/current Codex and PI-Desktop reference equivalence, all visible
control states and full-product completion remain open. Keep the product goal
active. No AOT, JSON, packaging, commit, push or release change occurs.
<!-- /source: TASK_MENU_LIFETIME_ACCEPTANCE.md -->


---

<a id="task-mutation-acceptance"></a>

## TASK_MUTATION_ACCEPTANCE.md

<!-- source: TASK_MUTATION_ACCEPTANCE.md -->
### 会话菜单写入与原生操作验收

2026-10-07。仅证明下列边界和操作，不代表完整产品或 Codex 全界面逐像素一致。

同日后续补充：标题不再保存旧任务的完整快照，持久化与发布期间只阻止同目标发送。
真实 SQLite 冲突、回滚、元数据与发送状态回归及最新全量验证见
[TASK_TITLE_PERSISTENCE_ACCEPTANCE.md](CONVERSATIONS.md#task-title-persistence-acceptance)；下列原生截图仍是此前操作的历史证据。

#### 设计与约束自检

1. 行组件和协调状态由 `MossAgent.App` 所有，继续复用 `AppTaskNavigationRow`、
   `AppInput`、`AppButton`、`AppText` 和共享 `status-error` 变体，没有页面专属颜色。
2. 持久化继续通过 `IWorkspaceRepository`，复用已有 `SettingsMutationRunner`，
   不新增服务定位器、数据库迁移或具体存储依赖到 UI。
3. 四类写入共用排他状态，连接命令取消与工作区生命周期令牌，默认 30 秒协作取消截止。
   不运行工具或模型请求，不改变授权、审批或工具执行管线，不展示原始异常。
   截止或关闭不会强制终止忽略取消的存储实现；不宣称回滚已经提交的写入。
4. 保留目录协调类不变，工作区在释放时取消任务管理写入并解除其状态订阅。
   新手写 C# 文件各一个类型；任务管理约 292 行，低于 500 行硬限制。
5. 回归覆盖成功重试、存储异常、同会话新草稿、置顶元数据合并、重入、取消、截止、
   生命周期结束后迟到结果，以及恢复期间切换归档选择。

#### 已修复的问题

- 重命名、归档和恢复的存储异常不再沿异步命令传播；失败不发布成功事件，
  不清空标题草稿、归档选择或当前会话，可重新执行。错误文本来自本地化资源。
- 保存期间修改或重新打开同一会话的草稿，旧结果只更新已保存标题，不覆盖新输入。
  同一会话的元数据刷新不取消编辑；真正切换当前会话仍取消原编辑器。
- 先置顶再保存标题，编辑目标吸收新的置顶元数据，避免 UI 把已置顶会话错误移回项目列表。
- 四类写入互相排斥；控件禁用与直接调用命令检查执行相同限制。
- 恢复旧目标时，不清空用户后来选择的另一条归档记录。工作区释放后不发布迟到写入结果。

#### 回归证据

仓库外产物位于 `%TEMP%/MossAgent-task-menu-qa-20261007/`。

- `mutations-before.trx`：新增 16 项回归在修改实现前为 15 失败、1 通过。
  原始异常传播、同会话草稿被清空、写入重入、缺少生命周期取消及恢复选择被清空均实际复现。
- `pin-before.trx`：选择和未选择目标的两项置顶后保存标题回归均失败；
  两项新增协作截止与明确取消回归已经通过。
- `mutations-final.trx`：新增 20 项及相邻会话、行组件、关闭流程共 49 项通过。
- 开始本轮时还独立复核现有组件边界：538 项专项通过，真实 App 构建中的
  5 种违规 C#/AXAML 探针均被拒绝、2 种合法共享用法通过。
  该证据在 `%TEMP%/MossAgent-ui-component-current-20261007/`，不是新的视觉修改。

最终完整解决方案构建 0 警告、0 错误，七个项目依次执行的 2,406 项测试全部通过，
0 失败、0 跳过，其中 App 1,814 项。各项目独立 `*-serial.trx` 与
`serial-results.json` 保存完整计数，不用跨项目覆盖的单份报告代替全量证据。
首次并行全量测试中两项 Windows MCP 进程测试失败；完整 Windows 项目隔离重跑
50 项通过，最终串行全量再次 50 项通过。原始失败日志保留，不声称已经修复其根因。
限定文件格式化和复核通过；973 个 C#/AXAML 文件的规模、单类型和方法审计 0 违规。
本轮相关路径和新增文件空白检查通过。全仓 `git diff --check` 仍报告未触及的
`src/MossAgent.Tools.BuiltIn/Files/SearchTextTool.cs` 第 31、33、34 行尾随空白，未扩大范围修改。

#### Windows 原生操作

使用仓库外宿主加载生产 `App`、`MainWindow`、真实组件与 SQLite。
合成项目和两条已完成会话由宿主准备；不把该准备步骤冒充原生创建操作。
两个进程均确认 `QA_ELEVATED: False`，窗口分别为 1280×820 浅色、980×820 深色。
鼠标、键盘仅通过 Windows Computer Use 输入，Python 只被动截图。

- 在未选中的“菜单验收会话”行打开菜单并置顶。
- 从置顶行菜单进入重命名，真实聚焦、Ctrl+A、输入“菜单重命名验收”并点击保存。
- 从同一行菜单归档会话；上述三项动作日志中的当前选中会话 ID 始终保持原“打开的会话”。
- 正常关闭后使用同一隔离数据库重启，深色最小窗口仍显示一条归档，原会话保持选中。
- 实际展开归档列表、选择目标并点击恢复；标题和置顶状态保留，归档项消失。
  恢复按原目录行为打开恢复目标，不声称恢复动作保留旧选择。
- 深色任务菜单实际打开，Escape 关闭成功，未额外修改任务。
  Tab 后 UIA 仍报告窗口焦点，本轮不把它视为任务菜单键盘焦点循环已验收。
- 两个进程均通过 `close.signal` 正常结束，记录 `QA_EXIT_COMPLETED: 0`。

原生截图：`light-pinned.png`、`light-rename-draft.png`、`light-renamed.png`、
`light-archived.png`、`reopen-dark-narrow-archived.png`、`dark-narrow-restored.png`、
`dark-narrow-menu.png`、`dark-narrow-menu-escaped.png`。
被动 PrintWindow 仅保存主窗口，不合成独立原生弹出窗口；
`dark-narrow-menu.png` 只是菜单打开时的主窗口底图，不是菜单外观证据。
菜单打开和 Escape 关闭的原生证据是本轮 Computer Use 自动展示的完整窗口与弹出层截图。
`sqlite-archived.json` 与 `sqlite-restored.json` 来自只读连接：目标 ID 一致，
标题保存，置顶值保持 1，归档值从 1 变为 0，两个会话各自的一条消息均保留。

本轮未原生注入数据库异常或长时间写入；这些负面路径由确定性回归覆盖。
未验证所有 DPI、明暗与宽度的全组合，也未重做 Codex 全页像素对比。
截图脚本、宿主、日志和合成数据库均不进入仓库。未暂存、提交或推送，完整目标仍未完成。
<!-- /source: TASK_MUTATION_ACCEPTANCE.md -->


---

<a id="task-navigation-acceptance"></a>

## TASK_NAVIGATION_ACCEPTANCE.md

<!-- source: TASK_NAVIGATION_ACCEPTANCE.md -->
### 侧边栏会话行验收

2026-10-05。本记录只证明本轮侧边栏组件、任务菜单和所列截图检查，
不代表整个产品或 Codex 界面已经一比一还原。

2026-10-07 补充：此前原生输入的验收缺口已在新的隔离生产窗口中补测。
真实鼠标置顶、键盘输入重命名、归档、重启后恢复和 Escape 关闭任务菜单通过；
置顶、重命名和归档未切换当前打开的会话。恢复会话沿用选择恢复目标的既有行为。
新截图、只读 SQLite 核对和仍未验证的范围见 [TASK_MUTATION_ACCEPTANCE.md](CONVERSATIONS.md#task-mutation-acceptance)。
本文末尾的激活失败是 2026-10-05 的历史结果，不作为本次成功证据。

#### 实现与职责

- `AppTaskNavigationRow` 统一置顶、项目和最近列表中的会话行。
  标题、选中背景、长标题省略、选项按钮和菜单由组件维护，业务页面只绑定状态与命令。
- 三类行共用 30-DIP 行高和 28×28 选项按钮，标题右侧预留菜单区域。
  项目会话保留缩进，置顶及最近行使用相同的顶层文字起点和右侧留白。
- 当前会话保持选中背景；所选项目标题默认透明，不再同时高亮父项目和子会话。
- 每项菜单动作都传递该行的任务。置顶标签来自该任务的状态，
  重命名和归档检查该任务是否正在运行，不使用当前打开对话的状态代替。
- 重命名或归档其他会话不切换当前对话。跨项目归档不会混入当前项目的归档列表。
  旧的无参数入口仍操作当前会话，保留对话标题栏和全局任务操作的原有行为。

组件和协调状态由 `MossAgent.App` 所有，持久化继续经过 `IWorkspaceRepository`。
没有改变模块依赖、数据库结构、工具执行管线或审批策略，没有新增提供商请求。
组件不可替换内容或自行覆盖尺寸、内边距；AXAML 构建门禁和 `MOSSUI002` 同时执行此约束。
新文件各声明一个类型，相关 C#、AXAML 文件和方法均未超过仓库体积上限。

#### 回归与门禁

- 修复前，新增的 8 项任务菜单回归均实际失败：目标任务、目标置顶状态、
  目标运行状态和保存后的会话选择都可被错误处理。
- 修复后，新增 8 项任务动作测试及 13 项组件测试通过。
  组件测试在真实共享资源和 MainWindow 中检查 980、1280、1920 DIP 的明暗主题布局，
  验证有限宽度、行高、按钮尺寸、垂直对齐、标题与菜单区域不重叠。
- 通过真实按钮绑定和 Space 路由键事件打开弹出菜单并执行重命名、归档和置顶，
  验证菜单参数、数据库或目录状态改变及当前对话不被切换。
  这是 Avalonia Headless 组件验收，不冒充 Windows 原生输入验收。
- 直接执行命令也不能绕过运行中任务限制；保存重命名前再次检查目标状态。
- 临时加入指定 Width 和 Content 的会话行，实际执行
  `ValidateSharedTaskNavigationRows`，构建按预期拒绝。临时文件随后移除。
  最终解决方案构建包括有效用法及全部门禁，0 警告、0 错误。
- 最终相关测试 50 项通过，解决方案全量 1,268 项通过（App 916 项）。
  本轮 C# 空白格式检查和作用范围内 `git diff --check` 通过。

```powershell
dotnet build MossAgent.slnx --no-restore --disable-build-servers -m:1
dotnet test MossAgent.slnx --no-restore --no-build --disable-build-servers -m:1
```

#### 原生截图与限制

使用只读备份创建隔离验收数据库，包含已完成的合成对话；
在该副本中增加长标题、置顶和其他项目的最近任务。没有修改生产配置或项目文件，
没有提交新请求、执行历史工具或启动 MCP 服务；副本中 MCP 服务器数量为零。

截图保存在仓库外的临时目录：

`C:\Users\wzheng\AppData\Local\Temp\MossAgent-sidebar-qa-c77380c3273c40f2b5bda19ab3c5b89b`

- `sidebar-final-light.png`、`sidebar-final-dark.png`：最终版本，1920×1032 原生窗口。
- `sidebar-reference-comparison-final.png`：528×420 原始像素裁剪对比，不缩放截图。
  参考裁剪扣除 8 像素窗口外边缘；两侧内容并不相同，不用于证明全页像素相等。
- `sidebar-normal-light.png`：1280×820 原生窗口，来自本轮最终分组间距微调之前的版本。
  不能把它当作最终版本普通窗口的截图证明；最终各尺寸布局由组件测试覆盖。

浅色截图中，以灰度小于 150 的文字掩码测量以下纵向起点；
参考坐标扣除 8 像素窗口外边缘后，最终坐标相同：

| 区域 | Codex 有效坐标 | MossAgent 最终坐标 |
| --- | --- | --- |
| 置顶标题 | 148 | 148 |
| 第一条置顶会话文字 | 181 | 181 |
| 项目标题 | 228 | 228 |

这些数值只验证本轮纵向布局位置，不证明字体、颜色、抗锯齿或整个侧栏完全匹配。

Windows Computer Use 能返回正常截图，但本轮第一次菜单点击仍报
`failed to activate captured window`。刷新唯一目标窗口绑定后，激活重试仍失败，
随后停止全部 Windows 输入，没有改用 Python 或 PowerShell 注入鼠标键盘。
因此最终原生菜单点击、悬停和键盘操作仍待补充，不将组件测试替代为这些操作的证明。
隔离预览进程由创建方确认身份后回收；不声称已经验证正常窗口关闭流程或修复激活工具。
<!-- /source: TASK_NAVIGATION_ACCEPTANCE.md -->


---

<a id="task-snapshot-review-acceptance"></a>

## TASK_SNAPSHOT_REVIEW_ACCEPTANCE.md

<!-- source: TASK_SNAPSHOT_REVIEW_ACCEPTANCE.md -->
### 任务前后快照差异审查

#### 设计自检

1. Tools.BuiltIn 负责授权、快照校验、严格文本解码和差异生成，复用现有
   GitCommandRunner/ITaskProcessLauncher。App 仅通过 IToolExecutor 和任务
   artifact reader 操作，不读文件、启动进程或执行 SQL，依赖方向不变。
2. 新注册的 filesystem.review_change 使用 ToolDescriptor 和标准管线。
   工具不直接调用其他工具；任务快照引用来自现有 ToolFileChange 元数据。
   UI 通过每次请求的 ReviewedChange 传递所选记录，不修改 Agent 运行上下文。
   请求参数必须完整匹配该记录，不能换路径冒充其他快照；工具不在模型工具
   catalog 中宣传，但 UI 的标准管线 lookup 仍可执行。自由模型参数无法自行授权。
3. schema、授权路径和 artifact 链接验证在审批/执行前进行。工具声明只读
   FileRead/Process，既有只读策略可自动批准；注入拒绝服务仍在审批处中止。
   UI 保留 30 秒取消期限，底层受标准超时、输出限制、任务进程树和审计保护。
4. 不增加持久化迁移、全局缓存、AOT 或 JSON 源生成。每个新 C# 文件只有一个
   类型，快照不超过既有每份 2 MiB，Git 输出最多 32,000 字符且不放宽调用预算。
   patch UTF-8 大小不超过既有 128 KiB 文本预览上限，超限明确失败而非伪造完整差异。
5. 聚焦测试覆盖六种 UTF 编码、真实 no-index 差异、缺失/篡改/跨任务快照、
   路径穿越、junction、审批拒绝、取消、超时、输出限制、错误编码和恢复不重放。

#### 行为

- 文件摘要的“查看变更”和文件行打开历史差异标签，不再显示当前文件或
  当前 Git 工作区。原有文件打开和 Git 检查器保留独立语义。
- 连续哈希链合并最初 before 与最终 after 快照。相同记录不重复发布，
  回到原版本的净零变更隐藏；外部版本打断链时只展示后续片段并提示不完整。
- 每份快照重新检查名称、长度、SHA-256 与任务 artifact 目录，不回退到磁盘
  当前文件。原文缺失、旧记录和超过快照上限均显示可操作的不可用提示。
- UTF-8、带 BOM 的 UTF-16/32 使用严格解码；二进制、截断编码和无效代理对
  拒绝。BOM/编码本身不作为伪造的文本行增删；相同文本可产生空 patch。
- Git no-index 比较私有暂存文本，禁用外部 diff、textconv、颜色和自动 CRLF
  转换并固定三行上下文，强制文本模式；不要求项目存在 .git，不修改 index。
- 临时文件仅在任务目录的独立 GUID 子目录内存在，最终清理不用递归删除。
  保存的 UTF-8 patch 用任务 artifact 的长度/摘要验证后显示；text/x-diff
  仍必须满足原有 UTF-8 文本格式规则，不能借媒体类型绕过二进制拒绝。
- 当前文件与历史差异使用不同标签身份；历史差异禁止伪装成当前文件的
  资源管理器打开，刷新仍重新走标准审查管线。切换任务/关闭标签取消旧读取，
  不发布过期内容。组件直接复用共享代码预览的差异行、颜色和滚动行为。

#### 证据位置

脚本、合成数据、日志与截图保存在仓库外：

`C:\Users\wzheng\AppData\Local\Temp\MossAgent-task-snapshot-review-20261011`

首次编译、schema 定义和等价记录通知的失败尝试单独保留，不能计入通过。

##### 聚焦与构建

- `tools-verified/tools-verified.trx`：65/65，通过真实快照差异、安全边界和 Git 回归。
- `app-final/workspace-final.trx`：39/39，通过摘要链合并、历史标签、外部文件改写及恢复链路。
- `reader-focused/reader-green.trx`：4/4，通过 artifact 文本格式、长度和摘要校验。
- `solution-build-complete.log`：最终 solution build 为 0 警告、0 错误。
- `format-complete.log`：本轮 22 个 C# 文件空白格式检查通过，包含随后新增的
  子代理上下文优化及其测试；不恢复 AOT，也不改变发布配置。
- `code-size-final.log`：快照审查的 20 个 C# 文件均为单类型，最长文件 279 行，
  最长方法 61 行，无硬上限违规。新增上下文优化和测试另由完整回归门禁覆盖。

##### 普通权限原生验收

- 浅色 1280×820，真实生产 Agent/工具管线更新四个合成文件，模型请求数为 2。
  随后外部改写 Sample0.cs；实际点击“查看变更”打开四个历史标签，再选 Sample0
  并刷新，仍显示任务 before 的 -1 和 after 的 0，不显示 external sentinel。
- 同一 SQLite 数据关闭后重开，深色 980×640，模型请求数为 0，未重放写入。
  实际点击 Sample0 文件行，重新显示正确历史差异。历史标签禁止 Explorer reveal。
- 两次最终 observed 均为 SnapshotHistoryValid=true，所有 8 份原始快照的内容、
  生产 reader 校验和历史显示符合预期；两次进程均正常关闭。
- 原生构建日志为 `native-build-final.log`；最终非黑、未拉伸的被动 Python 截图为
  `native-final-light-review.png` 和 `native-final-dark-review.png`，尺寸分别是实际
  1280×820 与 980×640。普通权限及请求数见对应 `native-final-light.log`、
  `native-final-dark.log`；对应 observed/refreshed JSON 保留交互后的真实状态。
- 首轮旧 observer 的 SnapshotHistoryValid=false 是 QA 假设错误：它把保存的
  after 快照与故意外部改写后的 live 文件相比较。修正为独立验证 8 份历史快照，
  未修改生产逻辑，也未覆盖旧 false 证据。首轮与最终验收不能混计。

##### 完整回归的首轮与复测

`full-complete` 的 8 份 TRX 共 4,641 项，4,640 通过，1 项失败，无跳过。
失败是既有 WorkspaceHttpMcpFlowTests 的远端错误摘要用例，等待任务结束时
触发 fixture 的 30 秒取消，并非 snapshot review 断言失败。
原失败日志和 TRX 保留，不能算作全量通过。未放宽期限、删断言或改动 MCP 代码；
随后 `http-mcp-recheck/http-mcp-recheck.trx` 的整个相关类 8/8 通过。
单独复测不代表间歇性超时已修复；相同代码的第二次完整回归另记结果。

`full-recheck.log` 的相同命令最终退出码为 0，8 份独立 TRX 共 4,641/4,641
通过、0 失败、0 跳过；构建后没有 C#/AXAML 源码改动。

| 测试程序集 | 通过/总数 |
| --- | --- |
| Agent | 83/83 |
| App | 3,392/3,392 |
| App.Visual | 8/8 |
| Browser | 76/76 |
| Infrastructure | 171/171 |
| Platform.Windows | 137/137 |
| Providers | 377/377 |
| Tools | 397/397 |

最终 diff 空白检查通过，本轮含未跟踪文件的行尾空白检查也通过，未暂存、
提交、推送或发布。QA 两次进程与最终测试会话均已终态，不以重跑成功掩盖
首轮的 MCP 偶发超时，也不把本轮通过等同于整个产品目标完成。

#### 保留缺口

- 尚未实现受保护撤销、删除历史或 Shell/MCP 修改归因；撤销入口保持禁用。
- 不声称支持所有二进制文件、所有编码或无限大小的 patch。生成文本差异
  仍依赖 Git 可用，不可用时明确报错，不伪装成功。
- 本轮只补真实只读审查，不代表全产品或当前 Codex 全界面像素验收完成。
<!-- /source: TASK_SNAPSHOT_REVIEW_ACCEPTANCE.md -->


---

<a id="task-title-persistence-acceptance"></a>

## TASK_TITLE_PERSISTENCE_ACCEPTANCE.md

<!-- source: TASK_TITLE_PERSISTENCE_ACCEPTANCE.md -->
### 会话标题并发写入验收

2026-10-07。复用现有共享组件和强制门禁，不新建第二套包装层，
不把组件约束或存储回归通过当作 Codex 全界面逐像素一致。

#### 设计与约束自检

1. 协调与可用状态由 App ViewModel 所有；标题写入通过 Application 的
   `IWorkspaceRepository.RenameTaskAsync`，事务实现位于 Infrastructure。
   UI 不执行 SQL；任务读取复用独立映射器，不改变模块依赖或数据库结构。
2. 复用已有仓库端口、共享行、输入、按钮和本地化错误提示。
   不增加页面颜色、尺寸、阴影或原生视觉控件例外。
3. 延续已有写入排他、命令取消、生命周期取消及默认 30 秒协作截止。
   不运行工具或模型请求，不改变授权或审批；取消不保证撤销已提交事务。
4. 每个新增手写 C# 文件只有一个类型；相关文件和方法低于硬限制。
   工作区释放时解除任务管理状态订阅，避免关闭后继续更新命令可用状态。
5. 成功、元数据保留、冲突、活动任务、归档、缺失目标、取消、回滚，
   以及同任务和其他任务的发送可用性均有确定性回归。

#### 已修复的问题

- 原先重命名回写整条旧任务，可能覆盖新状态、审批策略、工作树和失败原因。
  现在事务只更新标题和时间，返回同一事务读取的权威快照供列表与置顶列表发布。
- 标题写入校验任务 ID、项目 ID 和原标题；拒绝正在运行、等待审批或已归档的目标。
  外部标题变化或任务不存在时返回冲突，不修改目标、不重新创建任务、不发布成功事件。
- 冲突提示来自本地化资源，保留当前草稿；旧保存结果仍不清空后来编辑或重开的草稿。
- 重命名、归档期间仅阻止同一目标任务开始运行，直到持久化和 UI 发布全部结束。
  其他任务仍可发送，停止运行的命令路径不变。成功、失败和取消后解除阻止状态。

#### 定向证据

仓库外产物位于 `%TEMP%/MossAgent-title-concurrency-qa-20261007/`。

- `rename-before.trx`：真实 SQLite 的 8 项新增回归全部失败，复现旧快照覆盖、
  活动/归档/外部标题冲突仍被保存，以及缺失目标被重新创建。
- `send-before.trx`：4 项新增回归全部失败，复现同目标发送未阻止与命令缺少可用状态通知。
- `mutation-after.trx`：上述回归与已有取消、截止、草稿和写入排他专项共 32 项通过。
- `persistence-after.trx`：17 项新增标题仓库回归和相邻失败元数据专项共 45 项通过。
  引号及 SQL 文本按字面标题存储；事务失败保留状态、消息、置顶与失败原因。
- `component-probes.json`：真实 App 构建中的 5 种 C#/AXAML 原生视觉控件、
  页面视觉继承及外观覆盖探针全部被拒绝，2 种合法共享组件/布局用法通过。
  初次探针脚本使用了不匹配的错误文案；修正匹配后重新复测对应 AXAML，
  保留 `component-probes-initial.json`，该脚本断言问题不是构建门禁放行。

#### 最终验证

- 完整解决方案构建 0 警告、0 错误；七个测试项目依次执行，共 2,435 项通过，
  0 失败、0 跳过，其中 App 1,826 项、Infrastructure 81 项。
  各项目独立 `*-serial.trx` 及 `serial-results.json` 保留完整计数。
- 限定文件格式化及复核通过；978 个 C#/AXAML 文件的规模、单类型及方法审计 0 违规。
  本轮修改和新增路径空白检查通过，不修改此前记录的无关 `SearchTextTool.cs` 尾随空白。

#### 验收范围

本轮不修改视觉模板、共享尺寸或颜色，未重新执行 Windows 原生鼠标键盘输入，
也未新增 Codex 对照截图。此前真实会话菜单操作证据保留在
[TASK_MUTATION_ACCEPTANCE.md](CONVERSATIONS.md#task-mutation-acceptance)，不能把历史截图冒充本轮复测。
发送门禁由带确定性模型配置的 Headless 工作区证明，没有提交模型请求。
脚本、探针、日志和合成数据库留在仓库外；不暂存、提交或推送，完整产品目标仍未完成。
<!-- /source: TASK_TITLE_PERSISTENCE_ACCEPTANCE.md -->
