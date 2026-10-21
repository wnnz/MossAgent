# Git 差异临时输入会话

## 设计自查和实施计划

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

## 生产链路和边界

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

## 分层验证

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

### 最终构建与回归（2026-10-21）

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

## 未完成范围和详细方案

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
