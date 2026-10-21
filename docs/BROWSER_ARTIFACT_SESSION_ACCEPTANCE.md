# 浏览器产物会话安全

## 设计自查与计划

1. Browser 拥有截图/下载的产物编排，Tools.Abstractions 既有产物文件会话作为端口，
   Platform.Windows 提供原生实现，App 继续只负责注册。不会新增 UI 文件 I/O。
2. 截图 bytes 写入新会话；现有下载端口已返回 SDK 流，保持协议，通过有界只写流写入
   未完成的受控暂存会话，得到建议名称后复制到最终会话并 hash，不按 SDK 路径重开。
   这是为保留文件扩展名及现有下载协议所需的两阶段写入，不将网页名称作为目录授权。
3. Browser 工具保持原审批/能力及统一管线，全程只使用上下文显式绑定的会话；最终
   复核工作区和取消后保留。
   导出设 32 MiB 限额，与既有内置下载限额一致；不等于 SDK 网络或浏览器磁盘配额。
   中途失败按原句柄清理；SDK 临时文件通过原 IDownload 对象清理并反馈未知结果。
4. 会话、流限制、名称规则、下载传输和各工具分文件；每个 C# 文件一个类型，500/80
   行硬限制不变。无数据库变更，不自动重试或重放下载。
5. 先实现并补充代理 SDK/真实临时文件测试，再核对生产 App、模型请求/审批/审计与
   本地页面的真实 Chrome 截图/下载。验证拒绝、取消、超时、并发、输出大小及重载。
   Debug/Release 完整构建及相关回归后更新矩阵；GUI、崩溃、MXC 单列。

## 已贯通的生产入口

- `BrowserScreenshotTool`、`BrowserDownloadTool` 由生产 App 注册的
  `IArtifactFileSessionFactory` 自动注入，继续经过标准 ToolExecutor 的能力、审批、
  取消/超时、输出和 SQLite 审计管线。审批拒绝或 ReadOnly 不进入 SDK、不创建文件。
  注入的会话拒绝时不会回退到路径实现；没有注入端口的兼容宿主仍使用旧路径分支，
  不能将新保证外推到这些宿主。
- `BrowserArtifactWriter.ScreenshotAsync` 在调用 SDK 前创建自有会话，拒绝空截图和
  超过 32 MiB 的结果。写入和 Complete 后再次复核工作区和取消，然后才 Retain。
- 下载既有端口本来就使用 `IDownload.CreateReadStreamAsync`，本轮保持接口。
  `browser-transfer` 会话仅作未提交暂存；接收流只写、不可 seek，并在每次写入前检查
  32 MiB。网页建议名称仅用作去控制字符、限制长度的显示名及 ASCII 扩展名提示，
  不决定存储路径。最终会话复制、hash、Complete 后仍保持所有权，暂存释放成功和
  最终复核通过后才 Retain；暂存清理失败会继续释放并清理最终文件。
- `PlaywrightDownloadTransfer` 只使用原 `IDownload` 和 SDK 流，不按 SDK 路径重开。
  成功尝试 Delete；失败尝试 Cancel，Cancel 失败也继续尝试 Delete。
  清理无法确认时返回本地化、有副作用标记的错误，SDK 原始错误不进入提示。
  `PlaywrightBrowserOperation` 在取消/超时后等待所属会话释放和 pending SDK 结束，
  保留下载清理未知信息，旧会话不能继续使用，不自动重试下载。
  若关闭会话本身也失败，仍等待 pending SDK 完成；有下载清理错误时保留其错误码并
  补充关闭未确认提示，否则给出独立的关闭未确认状态，不透出关闭异常原文。

## 分层验证与证据

证据目录为 `artifacts/browser-artifact-safety-20261021/`，保留失败和后续成功记录，
不覆盖此前专项目录，也不将检查点结果视为新源码验证。

| 层级 | 实际覆盖 | 边界 |
| --- | --- | --- |
| Windows 原生临时文件，26 项专项 | 审批成功/拒绝、ReadOnly、错误任务根、junction、建议名、空截图、32 MiB 越限、完成后取消/超时/授权拒绝、真实暂存清理拒绝、两任务并发隔离 | 浏览器 SDK 为模拟会话；临时目录内核句柄是真实实现 |
| Playwright SDK 代理，新增 7 项 | 原流读取、禁用 PathAsync、源/目标错误、Cancel/Delete 拒绝、取消后 drain 和不复用旧会话、敏感错误不透出 | 代理调用计数，不是 Chrome |
| 取消关闭失败，新增 2 项 | 关闭失败仍 drain pending；关闭/下载清理同时失败不丢失任一安全提示；旧会话拒绝复用 | 直接运行生产 operation，注入确定性失败，不是关闭真实 Chrome 失败 |
| App 8 项 | 保存提供商/模型 → 本地 Responses 模拟服务 → Agent → 审批 → 原生产物文件 → 工具消息/审计 → 服务重建 → 文本/图片预览；拒绝/端口拒绝/取消；保留草稿和根外哨兵 | 浏览器会话替换为测试实现；图片预览为 Avalonia Headless，不是原生窗口 |
| 生产 App 组合 + 实际 Chrome CLI | 真实截图、文本和 4096 字节二进制下载、并发、取消、超时、旧会话拒绝、新批准恢复、SQLite 重载和内容/hash 核对 | 没有真实提供商或原生 Avalonia GUI；没有终止进程 |

App 首次图片预览失败（`App-first-results/`）；加入正确 Headless 环境仍失败
（`app-headless-results/`）。检查 PNG chunk CRC 确认原测试数据 IDAT 错误，改用有效
1×1 PNG 后 `app-valid-png-results/` 为 8/8。未修改产品 2 MiB、8M 像素和媒体校验。
Windows 专项为 `windows-complete-results/` 26/26；SDK 首批为 `sdk-results/`。
取消关闭失败补充后，一项旧断言仍要求原始 IOException，见 `final-Debug-Browser-results/`；
已改为核实安全错误码、有副作用标记及不泄露原文，`browser-corrected-results/` 为 113/113。
失败检查点不作为最终验证，最终记录使用 `verified-` 前缀。

实际 Chrome 在独立测试数据/资料目录运行，只关闭其自身两个任务的浏览器。
`real-chrome-first.log` 记录探针预期审计条数写错的失败；纠正探针断言后的
`real-chrome-accepted.log` 成功。随后增加实际超时，
`real-chrome-timeout-accepted.log` 记录 10 条已完成审计、4 个最终产物，
其中慢下载请求恰好两次（各一次明确取消和超时），没有恢复时重放旧请求。
8 个生产模块的加载 SHA256 与指定 App 输出逐个核对；环境记录在
`runtime-environment.json`（Windows build 22631、Chrome 155.0.8059.85）。
补齐关闭失败处理后，`real-chrome-final.log` 再次通过同样实际流程；加载的 Browser
模块 SHA256 为 `C1716E1A2EBB789A2E416A31B3526D4BB3B49F7075D994E81EC54A96AB05908B`。
这些实际 Chrome 检查使用 Debug 生产程序集，不是 Release 发布包验收。

实际超时结果携带“浏览器下载暂存清理未获确认”。原生受控任务产物没有留下额外文件，
但不能据此宣称 SDK 缓存已全部删除。二进制下载核对了内容/hash；当前预览限制不会
因此扩大，也不宣称所有下载格式都支持预览。

### 最终冻结回归

`accepted-verification.json` 汇总最终 `verified-Debug-*` / `verified-Release-*`，
`review-verification.log` 保存复核过程。两配置完整解决方案构建均为零警告、零错误；
相关测试结果如下，每项无失败、跳过或中止：

| 范围 | Debug | Release |
| --- | ---: | ---: |
| Tools 全套 | 691 | 691 |
| Platform.Windows 全套 | 371 | 371 |
| Infrastructure 全套 | 795 | 795 |
| Browser 全套 | 113 | 113 |
| App 精选 | 424 | 424 |
| 每配置合计 | 2,394 | 2,394 |

App 精选涵盖浏览器设置/工具栏/嵌入式会话/产物，以及前轮读写、复制、移动、删除、
预览、快照审阅/撤销、审批和恢复。不是完整解决方案测试全套，不是发布包验收。
新增 43 项专项包含 26 Windows、9 SDK/operation 和 8 App；不与上表重复累加。

10 份最终 TRX 均已复核。两个配置的构建前后、配置之间及当前工作区的 2,595 个
源码/项目/脚本输入完全一致；最终真实 Chrome 的 8 个模块 hash 与当前 Debug 输出
一致。结构检查覆盖 2,402 个 C# 文件、21,448 个方法/访问器/局部函数/表达式，
文件最大 496 行、方法最大 78 行，单文件单类型和规模规则零违规。
没有数据库迁移、系统权限修改或正式发布；当前增量未提交。

## 未完成范围和具体方案

1. **浏览器网络与缓存配额。** SDK `FailureAsync` 等待完整下载，32 MiB 只限制向
   受控文件导出；截图数组也在 SDK 完成后才检查大小。下一步分别验证 SDK 流式接口
   的背压与取消语义、任务资料目录配额和截屏尺寸预检。通过本地分块/无限流页面核实
   网络、缓存、内存、取消和多任务边界；未证明前不称作整体资源限额。
2. **SDK 清理未知状态。** 关闭会话和传输结束存在竞争，不能保证每次 Cancel/Delete
   都能获得确认。后续用持久阶段记录保存任务/原对象归属及未知状态，提供人工核实
   入口；重新批准后使用新会话，不能自动重试或按猜测路径删除浏览器缓存。
3. **崩溃与原子发布。** 文件 Retain 和 SQLite 审计元数据不是同一原子事务。
   应通过向前迁移记录创建、Complete、Retain、元数据发布阶段和持久根身份；分别在
   下载接收、Complete 后、Retain 后和审计前后终止独立宿主。恢复只显示中断/未知，
   核对引用、内容、草稿及另一任务，不重放网页点击或下载。
4. **授权代际与回收。** 目前复核 App 提供的工作区，仍信任配置的产物父目录和任务 ID；
   没有持久根身份或即时撤权代际。后续在端口/运行租约中传递授权版本，撤销时取消
   受影响任务；回收依赖数据库引用、运行租约和宽限期，并校验同一对象身份。
5. **其他入口与平台。** 本检查点未覆盖 Git 临时 diff 输入，其后续专用会话和证据见
   [GIT_DIFF_INPUT_SESSION_ACCEPTANCE.md](GIT_DIFF_INPUT_SESSION_ACCEPTANCE.md)，
   不属于本浏览器专项的验证范围；兼容宿主的旧分支不受本轮会话保护。
   原生 GUI、真实模型、日常/CDP/嵌入式组合、下载中进程崩溃及其他卷/
   大小写/长路径组合尚未验收。命令根目录逃逸阻断仍依赖受支持独立 Windows 的
   MXC 实现与验收，用户已明确环境尚未就绪；本轮未进行提权或系统变更。

整个产品目标保持进行中，本专项结果不代替产品功能清单验收。
