# 任务前后快照差异审查

## 设计自检

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

## 行为

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

## 证据位置

脚本、合成数据、日志与截图保存在仓库外：

`C:\Users\wzheng\AppData\Local\Temp\MossAgent-task-snapshot-review-20261011`

首次编译、schema 定义和等价记录通知的失败尝试单独保留，不能计入通过。

### 聚焦与构建

- `tools-verified/tools-verified.trx`：65/65，通过真实快照差异、安全边界和 Git 回归。
- `app-final/workspace-final.trx`：39/39，通过摘要链合并、历史标签、外部文件改写及恢复链路。
- `reader-focused/reader-green.trx`：4/4，通过 artifact 文本格式、长度和摘要校验。
- `solution-build-complete.log`：最终 solution build 为 0 警告、0 错误。
- `format-complete.log`：本轮 22 个 C# 文件空白格式检查通过，包含随后新增的
  子代理上下文优化及其测试；不恢复 AOT，也不改变发布配置。
- `code-size-final.log`：快照审查的 20 个 C# 文件均为单类型，最长文件 279 行，
  最长方法 61 行，无硬上限违规。新增上下文优化和测试另由完整回归门禁覆盖。

### 普通权限原生验收

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

### 完整回归的首轮与复测

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

## 保留缺口

- 尚未实现受保护撤销、删除历史或 Shell/MCP 修改归因；撤销入口保持禁用。
- 不声称支持所有二进制文件、所有编码或无限大小的 patch。生成文本差异
  仍依赖 Git 可用，不可用时明确报错，不伪装成功。
- 本轮只补真实只读审查，不代表全产品或当前 Codex 全界面像素验收完成。
