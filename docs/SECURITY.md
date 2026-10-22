# 安全边界与 Windows 隔离

本文件集中记录安全设计和已确定的隔离路线。功能项、当前证据与详细实施步骤见
[功能清单 R01/R02/R04](FEATURE_STATUS_AND_IMPLEMENTATION_PLAN.md)。
以下结论沿用截至 2026-10-21 的检查点，本次文档整理没有重新提权、修改系统或运行隔离实验。

## 尚未关闭的阻断

**命令和 ConPTY 终端仍可访问任务授权根之外的文件。** 工作目录校验、Job Object、
审批和文件工具句柄安全都不能替代操作系统级文件隔离。Git hooks/凭据 helper、MCP server
等代码执行入口也必须一并盘点；不能只限制 Shell 就宣称整个任务隔离完成。

原生产管线反例与研究保存在[命令隔离记录](archive/SECURITY.md#command-filesystem-isolation-plan)。
在该缺口和关键工作流验收关闭前，不宣称产品安全完成或具备发布条件。

## 统一安全规则

- 所有工具经过查找、schema、能力/路径、风险、审批、超时/取消、执行、输出限制/产物及审计。
  UI 快捷入口和子代理不得旁路调用实现；未知 MCP 默认有副作用。
- 无项目聊天不隐式授权文件、Shell 或目录。项目支持多根授权，必须校验穿越、链接、
  junction、重解析点和 Windows 大小写；操作使用实际对象身份，不能只比较字符串。
- ReadOnly 拒绝变更、Shell、页面修改及有副作用的 MCP；AskEveryTime 逐次审批相应动作；
  FullAccess 仍不允许授权根越界、系统破坏或接管不属于任务的浏览器。
- 每任务独立拥有取消、事件、进程树和浏览器会话。取消和超时之后仍须等待所属资源排空，
  清理失败要可见；不能把停止等待当作进程或远端副作用已结束。
- 文件写入保留原子替换和外部并发变更检测。删除、撤销和失败清理绑定原对象，
  不能按已被替换的名称删除后来出现的数据。
- 不自动重放中断命令、写入、浏览器操作或其他未知副作用。恢复先展示已知效果、失败与待核实状态。
- 不在日志、错误、审计或导出中记录 API key、代理凭据、授权头、Cookie。
  本地 SQLite 中明文保存密钥是现有产品决定，不能据此放宽日志和导出约束。

## 已实现的本地文件边界及限制

| 机制 | 已有范围 | 仍需完成 |
| --- | --- | --- |
| 项目授权版本 | schema 26 条件保存、旧租约撤销、跨实例轮询；schema 30 持久登记、跨实例等待及陈旧登记二次确认放弃 | 真实进程崩溃核验、迟到原生/MCP/浏览器资源与提交崩溃恢复 |
| 项目根对象 grant | schema 27 绑定卷/目录文件 ID，旧项目显式确认 | ABA、别名及完整平台/文件系统支持矩阵 |
| 工作树对象 grant | schema 28 绑定 task/project/目录对象；schema 31 记录意图与恢复 UI，任务/消息/草稿/intent 原子条件提交，目标由独占目录句柄认领 | 清理交接、替换对象重新授权与崩溃恢复 |
| 文件与产物会话 | 已接入入口按句柄校验、父链固定、原子写、并发检查与原对象清理 | 最终名称条件替换、剩余枚举入口、链接/并发矩阵及产物崩溃回收 |

这些是 Windows 生产装配中的有界能力；可选端口未接入的兼容宿主不自动具备相同保证。
受控负例和具体限制见[安全归档](archive/SECURITY.md)及[工作树归档](archive/GIT_WORKTREES.md)。

## 已确定的命令隔离路线

采用 **原生 MXC，在支持它的独立 Windows 验收环境实施**。用户已选择该路线；
独立环境尚未就绪，因此先推进本地文件安全修复。无需重新选择路线，也没有授权升级当前
系统、启用 Windows 功能、更改 ACL/所有权/服务或将日常环境切换为另一执行基线。

[主机与 SDK 决策记录](archive/SECURITY.md#windows-isolation-backend-decision)中的历史普通用户探针显示：

- 当前研究主机为 Windows 11 23H2 22631.3880；Microsoft.Mxc.Sdk 1.0.1 的
  `IsSupported=true` 不代表具备原生隔离：实际 request Probe 选择 `AppContainerDacl`，
  `BaseContainerApiPresent=false`，并要求 DACL augmentation。
- 普通 AppContainer/LPAC 的包组可见性不满足严格任务根白名单。KnownDll ACL 租约路线
  已被实际拒绝；相关一次性管理员授权已用完，不延伸为再次提权或绕过系统保护的许可。
- Windows Sandbox 的单实例限制、未验证的 Windows 容器/VM 和现有 Linux Docker
  都不能当作原生 Windows 并发任务隔离的已完成替代。

## 实施门槛与顺序

1. **先验证独立环境**：只读采集系统/SDK 能力、实际请求隔离层级和依赖版本。
   启动时必须保证实际使用原生层级，覆盖 probe 到 launch 之间的能力变化；不得弱回退
   到主机令牌或 DACL 修改，也不能只依据启动前一次 `Probe`。
2. **扩展既有端口**：Tools.Abstractions 表达不可变任务进程 scope，包含任务身份、授权根、
   明确只读运行时依赖和任务私有 temp/cache；Platform.Windows 实现 SDK/原生适配。
   Application 保留生命周期/恢复职责，UI 不获得原生访问。
3. **定义进程所有权与失败清理**：复用 Job Object、管道、审批、审计和输出预算，验证部分
   setup、启动失败、超时、取消、迟到子进程和清理错误。SDK `RunAsync` 取消停止等待
   不等于工作负载终止，必须实现并证明 task-owned termination/draining。
4. **贯通交互终端**：审查 SDK ProcessContainer 与 ConPTY 的实际互操作。
   历史 SDK PTY 便捷 API 使用的 IsolationSession 有不同网络要求，成功执行有管道命令
   不能代替终端验收。正常 pwsh/provider/module、SDK、Git 与任务并发都必须保留。
5. **收紧运行范围**：明确运行时只读依赖、环境变量、网络和凭据，不继承任意主机密钥；
   不给整个用户目录/磁盘根宽泛授权。覆盖多根、相对/绝对路径、链接、硬链接、UNC、
   继承句柄、外部并发修改和运行中撤权。MCP/Git 执行入口使用同一隔离边界。
6. **贯通恢复与发布**：恢复仅清理已知所属资源，未知副作用要求人工核实；
   在 JIT Windows x64 自包含单文件包中验证 native DLL/helper/Node 依赖定位与生命周期，
   不启用 AOT、裁剪或放宽策略来通过测试。
7. **关闭验收反例**：生产 App → ToolExecutor → 真实进程验证授权根内成功、根外读写和
   子进程逃逸拒绝、外部标记不变、ReadOnly 拒绝、审批/取消/超时和审计完整。
   完成任务并发、进程树清理、原生 ConPTY、聚焦负例、完整构建/回归及对应 Release 验证。

系统预置如确有需要，应先准备可审阅的具体变更和回收方案，再按实际范围取得授权。
历史诊断细节保留在[启动实验](archive/SECURITY.md#command-bootstrap-probe)，不作为生产实现。
