# 工具输出限流精简

## 设计自检

1. 行为仍归 Tools.BuiltIn 的 ToolOutputLimiter，通过现有 ToolExecutor 调用；
   不改变模块依赖，不增加新工具或端口，不调用其它工具实现。
2. 移除 FitsBudget 预检，按文件记录、摘要、正文、对话的既有优先级单遍扣减。
   完整集合直接保留引用，只在首次截断时创建前缀；原结果和消息不被修改。
   结果集合与原有未超限快路径一样，要求调用方发布后不再修改。
3. schema、能力、授权、风险、审批、超时、取消及审计管线不变。无效文件记录
   仍截断整段后缀并标记不完整；不丢弃已提交的有效前缀。正文不能抢占文件预算。
   仍避免拆开 Unicode 代理对；预算耗尽后的空对话也不保留，不计算可能溢出的
   聚合 int 作为判断条件。Artifacts、错误代码及消息 IsPartial 原样保留。
4. 不增加生产类型；每个新增测试文件只有一个类型，文件/方法保持硬上限。
   不改变 UI、存储 JSON、JIT 调试或 Windows x64 单文件发布配置。
5. 聚焦测试经生产 executor 检查单遍读取与集合复用，覆盖有效前缀后跟无效记录、
   首项 null/无效、角色/正文截断、精确预算、原不完整标记及源数据不变。
   复用已有 tiny/negative/int-overflow、代理对、取消/超时、审计和文件快照回归。

## 验证

仓库外证据目录为 `C:\Users\wzheng\AppData\Local\Temp\MossAgent-output-optimization-20261011`。
分配探针通过反射绑定同一生产 Limit 方法，在改动前后分别启动新进程执行；
使用同一 .NET 10、Debug 生产 DLL、Release 探针，关闭 tiered compilation，
每例预热 2000 次后测量 10000 次 thread-local 分配。只比较局部热路径，
不把分配结果当作全应用性能、内存峰值或 UI 验收。

固定运行条件下，三个输出字符数前后相同，单次分配如下（单位 bytes）：

| 场景 | 改动前 | 改动后 | 减少 |
| --- | ---: | ---: | ---: |
| 全部未超限 | 2112 | 2048 | 3.03% |
| 仅文字截断 | 5008 | 2224 | 55.59% |
| 最后一条对话截断 | 6376 | 3368 | 47.18% |

原始测量及对照保留在 `allocation-before.json`、`allocation-after.json` 和
`allocation-comparison.json`，探针源码只保存在上述临时目录，不进入仓库。

聚焦回归 `focused` 为 70/70，包含新增 9 项集合复用/单遍读取用例。
解决方案构建零警告、零错误；三份 C# 的 scoped whitespace format verify、
git diff whitespace 和含未跟踪文件的尾随空白检查均通过。生产限流文件由
99 行减为 79 行；新增测试文件分别为 143 和 25 行。

全量 `full` 的八份 TRX 为 4703/4704，进程退出码 1。唯一失败为
`FileChangeRestoreConcurrencyTests.AConcurrentCommittedWrite_IsNeverOverwrittenOrDeleted(create: false)`：
预期错误代码 file_changed，实际 tool_failed。其它七个测试程序集全部通过。
该并发类单独复测 `concurrency-recheck` 为 2/2，同一代码的工具程序集完整
复测 `tools-recheck` 为 457/457；两者均退出 0。独立 XML 汇总保留在
`full-counts.json` 和 `validation-summary.json`。

首次失败原件保留，未改并发测试或放宽其错误断言，不声称首次全量全部通过，
也不声称并发问题已根治。本轮未修改界面或进行新的原生界面验收。
