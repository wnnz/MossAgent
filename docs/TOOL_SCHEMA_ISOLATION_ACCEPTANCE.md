# 工具 schema 注册表隔离验收

## 2026-10-11 根因

上一轮首次全量的子代理误判不是通过重跑解决的。本轮独立冷启动探针使用
生产 ToolExecutor、合法的 `subagent.run` 同形 schema 和无副作用合成工具：
16 个新进程，每进程 64 个并发调用者、每调用者四次调用，八个进程出现
`tool_schema_invalid`。不连接真实提供商，不写用户文件。

仓库外探针通过 first-chance 调用栈定位到
`Json.Schema.SchemaRegistry.RegisterSchema` 的非线程安全 Dictionary 并发操作。
核对已安装 JsonSchema.Net 8.0.5 nuspec 所指源码 commit
`3520d7ac43e5c6c9b91abeac5af992eb82ffbf63`：

- `new BuildOptions()` 的 SchemaRegistry 默认引用 Global，并非新实例。
- 原代码设置 `options.SchemaRegistry.Fetch` 实际改写了全局 Fetch。
- 每次 Build 把请求 schema 写进同一个全局字典，导致并发冲突、相同根 `$id`
  互相覆盖拒绝，以及校验结束后仍全局持有已处置 JsonDocument 的 schema。
- 只并发执行元 schema 检查的两个探针模式，各八个冷进程均没有失败；
  不能将该问题误归因于元 schema 检查，也不能通过重试或预热掩盖。

## 设计自检与修复

1. 修复归 Tools.BuiltIn 的现有 `ToolSchemaDialect.Create`，依赖方向不变。
2. 使用现有 BuildOptions 的 SchemaRegistry 属性，每次调用显式创建私有实例；
   ToolDescriptor、IToolExecutor、元 schema、方言与兼容处理器不变。
3. 私有 Fetch 仍拒绝远程获取。既有 schema/value 预算、路径/能力验证、审批、
   超时/取消、输出限制与审计顺序不变，不放宽非法 schema 或参数的拒绝规则。
4. 不添加全局锁、预热、重试、静态注册表或无界缓存，不修改依赖版本。
   生产文件保持一个类型且远低于文件/方法上限，所有调用状态仍归请求所有。
5. 十个回归覆盖默认及四个声明方言、重复根 `$id`、本地指针、不同 const、
   每方言 128 个并发请求、合法/非法输入的审批/执行/审计状态，以及 Global
   不出现请求 schema、Global.Fetch 身份保持不变。原代码十例全部失败。

另外新增两个实际工作区回归，不向原有 fixture 堆积逻辑：两个独立任务
通过真实设置保存命令配置 loopback 模型与 MCP 服务，使用相同根 `$id`
和不同 message const，同时从生产主操作命令启动。分别验证双成功、
一个成功而另一个非法参数被拒绝，核对各自可见结果、SQLite 和审计记录。
重建服务恢复后模型请求数不增加，非法调用始终没有远程工具执行。

## 证据

所有探针、合成日志和测试结果保留在仓库外：

`C:\Users\wzheng\AppData\Local\Temp\MossAgent-schema-cold-start-20261011`

- `red-probe-*.log`：16 进程基线，八个进程失败。
- `stack-probe-1.log`：真实生产调用栈定位全局注册表字典并发访问。
- `meta-flag-*.log`、`meta-tree-*.log`：元 schema 单独并发对照。
- `red/registry-isolation-red.trx`：十个新回归对原实现均失败，包括全局残留。
- `focused/registry-isolation-green.trx`：首次修复后专项通过 140/140。
- `green-probe-1..64.log`：64 个全新进程，共 16,384 次生产管线调用，
  失败进程和失败调用均为零，不通过重试或复用已预热进程获取该结果。
- `app-focused/workspace-isolation-green.trx`：最终双任务/MCP 与相关工作区
  专项通过 16/16，包括两个新回归的实际模型/MCP/持久化/恢复链路。
- `tools-final/registry-isolation-final.trx`：精确选择的 schema/string/ToolExecutor
  专项通过 92/92；该次选择范围与首次 140 例不同，不拼接成全量结果。
- `agent-final/agent-final.trx`：Agent 独立全量通过 69/69。
- `subagent-app-final/subagent-workspace-final.trx`：App 子代理专项通过 105/105，
  覆盖合成提供商协议、子任务运行/事件/对话展示与生命周期链路。
- `format-final.log`：三个本轮 C# 文件的专项空白格式验证通过。
- `code-size-final.log`：Roslyn 审计三个文件各一个类型，最长文件 67 行，
  最长方法 22 行；没有新增全局状态或巨型辅助类。
- `solution-build-final.log`：完整解决方案构建成功，零警告、零错误。
- `full-final.log`、`full-final/*.trx`、`full-final-summary.json`：构建完成后
  独立串行全量通过 4592/4592，八个程序集失败与跳过均为零。
  Agent 69、App 3387、App.Visual 8、Browser 76、Infrastructure 170、
  Platform.Windows 137、Providers 377、Tools 368。计数只读取本次八份
  完整 TRX，没有合并先前失败或专项结果；相对上一轮新增十个工具回归和
  两个真实工作区回归。使用 90 秒无进展守卫，正常等候 UI 回归结束。
- `diff-check-final.log`：工作区已有 tracked 差异的空白检查通过；本轮
  untracked 文件单独验证无尾随空白。暂存区为空，本轮没有提交或发布。

## 验证范围

本轮实际工作流证据来自生产 App composition、设置/主操作命令、Agent、
loopback HTTP/SSE、MCP 和 SQLite 的集成测试。没有付费调用、真实第三方
账号或原生窗口截图；没有将 headless 测试当作当前界面的像素验收。
审批、安全预算、取消、输出限制和持久化协议保持不变。

本轮不改变界面布局、数据库或发布方式，不代表完整产品目标完成。
安全撤销、任务精确差异及当前 Codex 全界面对照仍需继续推进。
