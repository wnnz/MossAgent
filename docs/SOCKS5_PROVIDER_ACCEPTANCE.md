# SOCKS5 提供商真实路由验收

## 2026-10-10 设计复核

1. 生产路由仍由 Infrastructure 的 `ProviderHttpClientFactory`、现有配置仓储及
   Providers 适配器拥有，UI 只使用设置/模型发现/发送命令，不新增请求旁路。
2. SOCKS5 工厂已有实现与单次 HTTP 测试，缺少真实设置、发现、流式响应、改端口
   及显式直连的三协议完整证据。本轮扩展既有路由测试，不重复实现生产代理。
3. 夹具仅绑定回环、检查完整握手及精确目标，不转发或解析公网域名。读取有期限、
   接收长度有界，停用代理不得回退直连；不记录正文、密钥或凭据，不改审批/授权。
4. 匿名握手夹具复用 `tests/Shared`，测试项目各自链接，不增加测试程序集依赖；
   每个手写文件一个类型，继续文件 500/方法 80 行硬限。
5. 验证 Responses、兼容 Chat、Gemini 的代理、改端口、显式直连及停用拒绝；
   随后原生设置、导入、对话和重启恢复，并执行定向回归、完整构建及格式检查。

## 实现与针对性结果

- 生产 SOCKS5 路由已有正确实现，本轮没有改生产代理、提供商、工具或数据库代码。
  原有 App 路由类由 12 项扩为 24 项，净增 12 项，三协议均使用真实 App 装配、
  SQLite、设置保存命令、模型发现与流式适配器，而不是替换 HTTP 工厂的假实现。
- 每个协议验证 HTTP/SOCKS5 请求、改端口只到达新监听器、保留默认代理时显式直连、
  停用所选代理后发现/流式均拒绝且代理与直连监听器都没有连接。
- SOCKS5 CONNECT 校验 `models.example.test:80`，隧道内请求为相对目标；HTTP
  代理保留绝对目标断言。服务直接返回合成数据，不解析或转发公网域名。
- `Socks5TestHandshake` 抽为共享单类型夹具，复用有界 `ReadExactlyAsync`，替换
  Infrastructure 的重复/未检查握手与读满循环，不引入测试程序集互相引用。
- App 路由 24/24、Infrastructure HTTP/SOCKS5 工厂 4/4 通过。
- 三个 C# 文件定向 whitespace 格式和 Roslyn 审计通过：51/212/116 行，
  每文件一个类型，最大方法跨度 73 行（包含理论测试参数），没有超过硬限制。
- 完整解决方案构建 0 警告、0 错误。

## 实际设置到对话

普通权限真实 App/MainWindow、独立合成 SQLite、匿名回环 SOCKS5 服务，未读取
用户配置或真实凭据，也没有公网、付费请求、文件工具调用或审批策略改动。

1. 浅色 1280×820、96 DPI 下，原生鼠标打开预置的 HTTP 代理编辑器，选择
   `Socks5` 并保存，列表实际更新。代理、提供商和项目由宿主预置；不声称原生创建。
2. 实际进入 AI 提供商、展开模型分组、点击获取模型；当前提供商导入
   `fixture-model`，回环服务收到一次 SOCKS5 CONNECT 与 `GET /v1/models`。
3. 返回会话，在真实输入框键入合成请求并点击发送，主对话显示
   `SOCKS5_NATIVE_OK`；服务的第二个 CONNECT 收到 `POST /v1/responses`，
   `ModelMatched=true`。没有记录正文、请求头或凭据，只记录允许列表的路径和布尔值。
4. 保存白名单元数据均为 true：Socks5、ProviderUsesSavedProxy、ImportedModel；
   保存端口 61289，默认和启用状态保留。
5. 同一数据根深色 980×820、96 DPI 重启，未重新赋值代理协议、端口或模型。
   设置页显示已保存的 Socks5/端口，返回会话后同一回复与导入模型恢复；
   恢复服务请求数为 0，没有重新请求模型、发送消息或回放工具。

### 宿主收尾的失败与修复

首次浅色产品窗口关闭并完成 App 生命周期，日志为 `QA_EXIT=0`，但仓库外 SOCKS5
验收服务的 async disposal 捕获 Avalonia 同步上下文后被主线程同步等待，宿主未退出。
在确认产品窗口已关闭、配置和回复保存之后，仅终止精确匹配 PID/可执行路径的本轮
验收宿主，最终执行结果 -1；不把整个首次宿主描述为正常成功退出。

只修仓库外宿主的两处收尾 await 为 `ConfigureAwait(false)`，没有改生产 App。
最终深色恢复进程记录 `QA_SERVER_DISPOSE_BEGIN=AvaloniaSynchronizationContext`、
`QA_SERVER_DISPOSE_END=True` 和执行退出码 0，确认服务和窗口均正常收尾、没有残留。
就绪日志早于原生尺寸稳定，宽度可能还是 1280；深色尺寸以实际 980×820 原图为准。

## 完整回归复核

首次默认并行完整回归为 3987/3990，3 失败、0 跳过：
`PtyTerminalResizeOwnershipTests.StopRetainsNativeResizeUntilReturnAndBlocksReplacement`、
`PtyTerminalStateTests.NaturalCleanupMustReturnBeforeRestartAndDoesNotStopOtherTasks`、
`ShellProcessOwnershipTests.CommandExitOrCancellation_LeavesNoBackgroundDescendants(intermediate: True, cancel: False)`。
三项均在既有十秒等待处超时；相关原样测试类独立复核 15/15 通过。
没有改 Windows 代码、夹具、生产期限、并行设置，也没有添加测试重试；独立复核不能
代替完整通过或证明偶发失败根因已修复。

第二次原样完整回归为 3988/3990，2 失败、0 跳过：既有
`NaturalCleanupMustReturnBeforeRestartAndDoesNotStopOtherTasks` 再次等待超时；
App 的 HTTP/Responses 改端口场景在 SQLite 初始化期间触发取消，尚未进入模型请求。
原样独立复测 App 路由类 24/24 通过，没有修改五秒测试期限、并行设置或添加重试。
这只定位了取消发生的阶段，不证明全量并行失败根因已修复，也不声称全量通过。

后续响应读取去重复回归包含上述路由类，完整解决方案 4005/4005 通过，0 跳过；
详见 `PROVIDER_STREAM_LIMIT_ACCEPTANCE.md` 的 2026-10-10 记录及其独立证据目录。
这次通过不删除此前两轮失败证据，也不将响应重构描述为修复 Windows/SQLite 超时。

## 仓库外证据与限制

证据目录：`C:/Users/wzheng/AppData/Local/Temp/MossAgent-socks5-provider-20261010`。
`focused-app/routing.trx`、`focused-infra/factory.trx`、`full/*.trx`、
`full-recheck/*.trx`、`windows-recheck/windows-recheck.trx`、
`routing-recheck/routing-recheck.trx`、`regression-summary.json` 及构建、格式、规模日志
保留自动化证据。
`light/requests-safe.json` 记录一次 GET 与一次 POST；
`light/requests-restored-safe.json` 记录恢复时零请求，两个 settings-safe JSON 记录恢复值。

Python 被动截图按当前 PID 捕获，保留原尺寸，不点击、移动或缩放任何应用：
`socks5-editor-light.png`、`socks5-model-imported-light.png`、`socks5-response-light.png`、
`socks5-settings-restored-dark.png` 与 `socks5-response-restored-dark.png`。
截图、宿主、脚本和合成数据均不加入仓库。

原生请求只覆盖匿名 SOCKS5/Responses；兼容 Chat 与 Gemini 的真实路由由确定性
集成测试覆盖。未验证认证代理、TLS、公网提供商、全部错误/取消的原生输入、全部
DPI 或 Codex 像素一致；这些未完成范围没有被匿名回环成功代替。产品 goal 继续 active。
保留普通 JIT 调试及 Windows x64 单文件发布，未暂存、提交、推送或发布。
