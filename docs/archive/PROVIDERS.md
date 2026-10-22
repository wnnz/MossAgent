# 提供商、模型与代理

> 历史归档：保留原报告当时的结论、实验、失败与限制。整理于 2026-10-21。
> 当前能力与未完成方案统一维护在[功能清单](../FEATURE_STATUS_AND_IMPLEMENTATION_PLAN.md)；
> 原文中的计划、主机状态、测试数字及“正在进行”等措辞均为历史检查点。
> 代码块和仓库相对产物路径保留原意；本机忽略的 `artifacts/` 不随克隆分发。

[文档入口](../README.md)

## 原报告索引

- [Authenticated provider proxy acceptance](#authenticated-provider-proxy-acceptance) — `AUTHENTICATED_PROVIDER_PROXY_ACCEPTANCE.md`
- [兼容接口流式链路验收](#compatible-stream-acceptance) — `COMPATIBLE_STREAM_ACCEPTANCE.md`
- [Fixed-length streaming transport acceptance](#fixed-length-stream-acceptance) — `FIXED_LENGTH_STREAM_ACCEPTANCE.md`
- [Gemini 多轮协议续接验收](#gemini-continuation-acceptance) — `GEMINI_CONTINUATION_ACCEPTANCE.md`
- [2026-10-08 模型配置原生链路与下拉框外观验收](#model-configuration-native-acceptance) — `MODEL_CONFIGURATION_NATIVE_ACCEPTANCE.md`
- [Model picker optimization acceptance](#model-picker-optimization-acceptance) — `MODEL_PICKER_OPTIMIZATION_ACCEPTANCE.md`
- [Model reasoning capabilities and native acceptance](#model-reasoning-capability-acceptance) — `MODEL_REASONING_CAPABILITY_ACCEPTANCE.md`
- [模型选择与共享组件验收](#model-selection-acceptance) — `MODEL_SELECTION_ACCEPTANCE.md`
- [提供商与模型目录状态验收](#provider-catalog-acceptance) — `PROVIDER_CATALOG_ACCEPTANCE.md`
- [Provider and model dialog native acceptance](#provider-dialog-native-acceptance) — `PROVIDER_DIALOG_NATIVE_ACCEPTANCE.md`
- [Provider and model editor sessions](#provider-model-editor-session-acceptance) — `PROVIDER_MODEL_EDITOR_SESSION_ACCEPTANCE.md`
- [Provider parser optimization acceptance](#provider-parser-optimization-acceptance) — `PROVIDER_PARSER_OPTIMIZATION_ACCEPTANCE.md`
- [提供商与代理联动验收](#provider-proxy-acceptance) — `PROVIDER_PROXY_ACCEPTANCE.md`
- [提供商与模型行统一组件验收](#provider-row-component-acceptance) — `PROVIDER_ROW_COMPONENT_ACCEPTANCE.md`
- [Provider stream completion and queued tools](#provider-stream-completion-acceptance) — `PROVIDER_STREAM_COMPLETION_ACCEPTANCE.md`
- [供应商流式输入限长验收](#provider-stream-limit-acceptance) — `PROVIDER_STREAM_LIMIT_ACCEPTANCE.md`
- [Provider settings-to-runtime UI workflow acceptance](#provider-ui-workflow-acceptance) — `PROVIDER_UI_WORKFLOW_ACCEPTANCE.md`
- [网络代理编辑器修改验收](#proxy-editor-mutation-acceptance) — `PROXY_EDITOR_MUTATION_ACCEPTANCE.md`
- [Proxy editor selection acceptance](#proxy-editor-selection-acceptance) — `PROXY_EDITOR_SELECTION_ACCEPTANCE.md`
- [SOCKS5 提供商真实路由验收](#socks5-provider-acceptance) — `SOCKS5_PROVIDER_ACCEPTANCE.md`

---

<a id="authenticated-provider-proxy-acceptance"></a>

## AUTHENTICATED_PROVIDER_PROXY_ACCEPTANCE.md

<!-- source: AUTHENTICATED_PROVIDER_PROXY_ACCEPTANCE.md -->
### Authenticated provider proxy acceptance

#### Plan and design self-check

- Previous goal work made concrete progress through sidebar reconciliation,
  runtime interactions and real external-Chrome concurrency evidence. The wider
  product goal remains active; anonymous proxy evidence does not prove authentication.
- Authentication remains owned by Infrastructure's existing HTTP/SOCKS5 client
  factory. App uses real settings commands and SQLite; Providers owns discovery,
  stream parsing and safe public error mapping. No cross-module shortcut is added.
- Extend deterministic App-composition tests with authenticated loopback proxies,
  covering Responses, compatible Chat and Gemini, valid/missing/wrong credentials,
  explicit direct routing and prevention of credential leakage in failures.
- Fixture framing is bounded, cancellable and loopback-only; it validates targets
  without forwarding to or resolving remote hosts. Its observation records contain
  request route and boolean flags only, never raw credentials, headers or messages.
- Existing approvals, task-authorized paths, production timeouts/output limits,
  database schema and JIT single-file packaging remain unchanged. Handwritten helper
  types are separate files and methods stay within the engineering size limits.
- Preserve a failing security baseline if authenticated errors expose secrets;
  fix the owning public-error boundary, add no-read/cancellation regressions, then
  verify the real settings/discovery/conversation path in an isolated native host.

#### Evidence and limits

- HTTP 407 bodies can contain proxy credentials that Providers cannot redact from
  an AiProvider's ProxyId. Discovery and the common streaming error reader now
  return the same localized authentication guidance without opening or reading
  the body. Cancellation remains authoritative and responses are disposed.
  Other statuses retain their existing sanitization, byte limits and decoding.
- The four protocol adapters share error-event sanitization instead of repeating
  six type-check branches. Non-failure events retain their identity without copies;
  sanitizing failures does not mutate the original event. Provider test setup is
  shared rather than repeating protocol factories and request construction.
- Providers regressions cover all four protocols, declared/unknown lengths, empty,
  credential-bearing, malformed UTF-8, oversized and unfinished 407 bodies. All
  80 body cases initially failed, while the pre-cancelled reader test passed.
  Eight additional cases check cancellation after response headers and disposal.
  The complete Providers suite now passes 469/469, including event identity tests.
- App tests use real settings commands, isolated SQLite and production clients.
  The authenticated matrix passes 30/30 across Responses, compatible Chat and
  Gemini with HTTP Basic and SOCKS5 username/password authentication. It covers
  valid/missing/wrong credentials, no direct fallback, explicit direct routing,
  and correct -> wrong -> restored credentials without restarting the provider.
  The broader focused App proxy/routing selection passes 81/81; Infrastructure's
  client-factory and connection-tester selection passes 9/9.
- The first credential-edit test run exposed a fixture-only assumption: SOCKS5
  always expected the synthetic hostname/port, while the new no-fallback case
  intentionally targets a second loopback server. Its three failures are retained
  in `app-proxy-before-fixture-correction.trx`. The fixture now accepts an explicit
  expected endpoint without weakening target checks or forwarding any traffic.
- Evidence: `C:\Users\wzheng\AppData\Local\Temp\MossAgent-provider-cleanup-20261011`.
  `tests/proxy-error-before.trx` retains the 80/81 failing baseline; post-fix results
  are `providers-after.trx`, `app-proxy-final.trx` and `infrastructure-proxy-after.trx`.
  Assertions and observation records never print credential values or headers.

- Solution build: 0 warnings, 0 errors. Whitespace verification passes for all 16
  touched C# files; each contains one handwritten type and stays under 250 lines.
  Tracked diff checks and new-file trailing-whitespace checks also pass.
- The first solution-wide test command was interrupted during the long App visual
  group after 3,051 passing App cases. That command is not recorded as successful.
  A standalone App replay with a 60-second per-test watchdog and no memory dumps
  passes 3,531/3,531 in 4 minutes 36 seconds; the watchdog does not fire. This was
  a long-running rendering group, not a reproduced production/test deadlock.
  Other projects pass 1,427/1,427, including Windows 137/137, so all test projects
  are verified at 4,958/4,958 across the completed project runs. Evidence is in
  `full-suite/`, `app-hang-diagnostic/app-hang-diagnostic.trx` and `build.log`.

#### Native success-path follow-up

- An isolated, non-elevated native host uses the real App composition, settings,
  model picker and conversation with synthetic SQLite data and loopback endpoints.
  The existing Python window-capture script stays outside the repository. No real
  user configuration or credentials are read.
- Actual UI interaction saves the HTTP proxy configuration and discovers one
  `fixture-model` for each of Responses, Gemini and compatible Chat. Manual models
  remain present; Gemini's non-generation discovery entry is not imported.
- Responses explicitly selects the direct route in the provider editor and is
  saved through the UI. Its conversation produces only a direct
  `POST /v1/responses`, despite the configured default HTTP proxy.
- Gemini discovery and conversation use authenticated HTTP; the conversation
  target is `/v1/models/fixture-model:streamGenerateContent?alt=sse`. A first 407
  challenge followed by an authenticated retry is expected, not a wrong-password
  failure. The inherited unsupported reasoning override blocks sending until the
  real reset action restores the model default.
- Compatible Chat discovery and conversation use authenticated SOCKS5, with
  `POST /v1/chat/completions`. Neither proxied conversation adds a direct request.
  All three sends finish as Completed and add their expected reply. White-listed
  UI and route observations contain no credential-leak flags.
- The host and server both close normally with exit 0 and no stderr. Evidence is
  `C:\Users\wzheng\AppData\Local\Temp\MossAgent-authenticated-acceptance-20261011-2202`,
  including proxy-save/discovery, direct/HTTP/SOCKS5 observations and passive
  screenshots. The window renders at 1920 by 1032, scale 1, in the light theme.
- This follow-up verifies native success and explicit-direct routing only. It does
  not claim a new native wrong/missing-credential or credential-restoration run;
  those negative paths remain supported by the deterministic tests described above.

#### Native HTTP failure and recovery follow-up

- A subsequent ordinary-permission run covers Gemini model discovery and streaming
  with a wrong synthetic HTTP password, then recovery through the actual settings
  editor. Failed discovery and conversation show safe HTTP 407 guidance; no direct
  fallback, successful authentication, new reply or credential leak is observed.
- The edit exposed an out-of-catalog ListBox selection during save. App now reloads
  the catalog before publishing its saved member, retaining draft/lifetime guards.
  The fixed native save closes its dialog and displays the update notice.
- Without restarting the App or provider, discovery and conversation then succeed
  through authenticated HTTP, adding one expected reply. Direct requests remain
  zero. All observation records and messages have false credential-leak flags;
  both processes close normally with exit 0 and empty stderr.
- Evidence and the 150-case focused / 3,600-case complete App regression are in
  [PROXY_EDITOR_SELECTION_ACCEPTANCE.md](PROVIDERS.md#proxy-editor-selection-acceptance). Native missing-credential, SOCKS5 and
  other-protocol failure/recovery are not claimed by this follow-up.

No paid endpoint, real user configuration or daily browser profile is used. No
full-product, TLS/public-network or Codex pixel-equivalence completion is claimed.
JIT debugging and Windows x64 single-file publishing are unchanged. No commit,
push or release is performed.
<!-- /source: AUTHENTICATED_PROVIDER_PROXY_ACCEPTANCE.md -->


---

<a id="compatible-stream-acceptance"></a>

## COMPATIBLE_STREAM_ACCEPTANCE.md

<!-- source: COMPATIBLE_STREAM_ACCEPTANCE.md -->
### 兼容接口流式链路验收

#### 2026-10-06 实现与回归

- 复用生产 AppComposition、提供商和模型设置命令、真实 HTTP/SSE、Agent、工具
  执行管线及 SQLite；模型服务仅监听 loopback，使用合成密钥和独立临时文件。
  不调用付费接口，不修改真实配置或用户项目。
- 修复结束信号后的空 `tool_calls: []` 被误判为非法内容的问题；空数组和 null
  元数据允许完成，非空调用、错误结构和结束后的新增正文仍拒绝。
  Providers 所有解析行为，App 不解析协议；工具授权、审批和输出限制不变。
- 提供商新增 8 项回归：stop/tool_calls 两种结束信号、空数组/null 尾帧，及
  非法对象、数字、非空调用数组和迟到正文。提供商完整项目 117 项通过。
- App 新增 4 项端到端回归：提前 EOF、工具结束后非法 JSON、同批第二个工具
  参数非法，及多个候选仅执行 choice 0。失败用 FullAccess 验证不是审批掩盖了
  意外写入；文件不变、无工具审计、部分回复仅保存一次，重新装配后不自动重放。
- 成功用 ReadOnly 验证实际读文件、工具审计、第二次模型请求中的工具结果及
  最终回复；另一候选的写入和正文不合并。实际请求保留模型、思考强度、输出
  上限、temperature、top_p 参数，恢复后的消息 ID 和审计不变。
- 合成服务器的纯 I/O 与关闭路径不捕获 UI 同步上下文；新增生命周期测试证明
  Headless 回调返回后，仍可取消空闲监听并完成关闭。相关 App 专项 5 项通过。
- 最新完整解决方案 1,637 项通过，构建 0 警告、0 错误；相关 C# 空白格式检查通过。
  每个新增 C# 文件一个类型，文件和方法保持在工程硬限制内。

#### 原生运行证据

- 仓库外临时宿主链接上述合成服务，使用生产 App/MainWindow 和普通权限，
  1280×820 深色窗口。原生输入发送提前 EOF 场景，Python 只被动截取窗口像素。
  实际显示部分回复和任务失败提示，没有工具活动行或运行中的停止按钮。
- 只读检查临时 SQLite：任务 Failed，用户和部分助手消息各一条，工具审计为零；
  合成文件内容不变。重启同一数据集后截图仍显示失败与原消息；宿主关闭日志
  确认模型请求为零、退出码为零，没有自动重新发送或执行工具。
- 首次宿主窗口已经关闭，但其合成服务器退出等待捕获了已停止的 Avalonia
  同步上下文。保留诊断堆栈后清理该自有临时进程，修正服务器关闭路径并重建；
  复测宿主正常关闭。该问题属于验收服务器，不是生产窗口截图或提供商修复。
- 图片、宿主和日志位于
  `C:\Users\wzheng\AppData\Local\Temp\MossAgent-compatible-stream-qa-20261006-01`；
  最终恢复图片为 `eof/reopened.png`，没有拉伸或将截图脚本加入仓库。

#### 2026-10-06 原生成功工具往返复测

- 仓库外普通权限宿主使用生产 App/MainWindow、独立数据和 MultipleChoices 合成服务，
  实际在输入框发送读取验收文件的请求。此原生场景使用 FullAccess；前述 ReadOnly
  成功证据来自自动测试，不混淆审批模式。
- 观察到部分回复、完成的文件读取行和最终回复；输入框清空，发送禁用，不再显示停止。
  展开工具行显示中文/Unicode 文件结果；检查器收起后项目上下文卡片重新出现。
- 只读检查临时 SQLite：任务 Completed，用户、部分助手、工具结果和最终助手各一条，
  唯一审计 chosen-read / filesystem.read_file / success。合成文件未变，没有另一候选写入。
  宿主日志记录模型请求 2 次，符合工具结果回传后的第二轮回复。
- 成功场景完成后原生宿主触及其 7 分钟期限，保留 TaskCanceledException 诊断；
  不把该日志视为无失败正常关闭，也不声称生产工具或截图链路因此失败。
  同一数据集 --reopen 恢复已完成会话，模型请求 0 次；随后正常关闭，日志无失败标记，
  退出码 0，消息和工具审计不重复，没有自动重新执行。
- 证据位于 `C:\Users\wzheng\AppData\Local\Temp\MossAgent-window-fidelity-20261006-1125`，
  包含 native-success-light.png、native-tool-expanded.png 和 native-reopened-normal-dark.png。
  最终完整解决方案 1,644 项通过，构建 0 警告、0 错误；外框变更详见 WINDOW_FRAME_ACCEPTANCE.md。

#### 剩余限制

- 不代表真实付费供应商、所有协议、所有页面和 DPI 的验收，也不证明 Codex 像素一致。
- 工具调用累积器已有数量、身份和参数长度限制；后续已用共享增量读取补齐 SSE
  原始行、跨行事件及 HTTP 错误正文上限，见 PROVIDER_STREAM_LIMIT_ACCEPTANCE.md。
  这些是每行/事件/错误正文的边界，不代表整个任务、累计正文或所有传输层输入均有界。
- 协议修复不涉及数据库迁移或工具策略变更；同轮外框共享样式独立验收，没有暂存、提交或推送。
<!-- /source: COMPATIBLE_STREAM_ACCEPTANCE.md -->


---

<a id="fixed-length-stream-acceptance"></a>

## FIXED_LENGTH_STREAM_ACCEPTANCE.md

<!-- source: FIXED_LENGTH_STREAM_ACCEPTANCE.md -->
### Fixed-length streaming transport acceptance

#### Plan and design self-check

1. The preceding proxy option turn made verified progress. This slice audits the
   open fixed-length transport observation, not whole-product completion.
2. Providers own protocol streaming; deterministic tests use the existing
   IModelProvider and IProviderHttpClientFactory ports, without changing runtime
   parser, HTTP factory, application composition or tool execution policies.
3. Use bounded loopback fixtures and synthetic payloads only. A private ephemeral
   TLS certificate is pinned by exact certificate bytes, with hostname errors
   still rejected. It never enters an OS trust store or production configuration.
   Retain cancellation, stream disposal and existing framing/output budgets.
4. Keep each handwritten C# file to one type and below 250 lines where practical,
   with methods below 40 lines where practical and all hard limits respected.
5. Prove partial text before response completion, release and successful finish,
   cancellation/disconnect, early enumeration disposal and wrong-certificate or
   hostname rejection across the actual provider adapters. Never replace the
   failing plaintext observation with a green chunked/TLS result.

#### Independent transport observation

- The original .NET/raw TCP probe evidence remains under
  `%TEMP%\MossAgent-subagent-lifecycle-recheck-20261012`.
- A new repository-external Python socket probe independently reproduces the
  delay, without MossAgent, HttpClient, .NET, UI or SSE parsing. The listener writes
  HTTP headers and a complete first SSE frame in one send, then holds the tail.
- Both plaintext fixed-length cases (event-stream and octet-stream) return only
  headers during the two-second hold. The body arrives after releasing the tail.
  Raw non-HTTP, close-delimited HTTP and chunked HTTP deliver the first frame.
- The same Python fixed-length exchange over TLS delivers the first frame during
  the hold. TLS validation trusts only the generated certificate in a private
  client context; no system certificate, firewall, proxy or security setting changes.
- This narrows the observation to the local plaintext HTTP transport path. It
  does not identify the intercepting component or prove a general Windows/.NET
  defect, and it does not make plaintext fixed-length streaming verified.
- Evidence: `python-transport-probe.json` and `python-transport-tls-probe.json`
  under `%TEMP%\MossAgent-fixed-sse-20261012`. Scripts stay outside Git.

#### Verification

- The real SslStream/SocketsHttpHandler fixture sends a declared complete body
  length but holds its tail. All four actual adapters deliver the first text
  delta while the server remains open; release yields the remaining text without
  a failure. Cancellation and early disposal disconnect without releasing the
  tail. A different certificate is rejected before any response frame is sent.
- Fixture certificate validation pins exactly the generated certificate and
  rejects missing certificates or hostname errors. Its test-only factory does
  not replace production validation, configure proxies or establish public CA,
  remote-account or real-provider compatibility.
- The first fixture activation fails because Windows Schannel cannot use its
  generated ephemeral key directly. Re-importing the synthetic PFX with
  UserKeySet, without PersistKeySet, gives the disposable fixture ownership of
  the key; no OS trust-store installation or production factory change occurs.
  The original 12-failure/one-pass setup TRX is retained, followed by 13 passes.
- The added wrong-pin cases initially fail only in the fixture's server-side
  rejection observation: a TLS alert can surface during the first request read,
  after server authentication returns. The client already rejects the connection.
  The fixture now observes both stages before sending a frame, without weakening
  rejection or swallowing unexpected failures in positive cases. Original
  four-failure focused/full TRX files remain alongside the corrected replay.
- Final focused TLS replay passes 17/17; the full Providers suite passes 516/516.
  Post-build architecture checks pass 7/7 and the existing App subagent/lifecycle/
  settings/read-lifetime slice passes 45/45. The solution builds with zero
  warnings/errors. Evidence: tls-stream-focused-pin-fixed.trx,
  tls-stream-providers-pin-fixed.trx, tls-stream-architecture.trx,
  tls-stream-app-lifecycle.trx and solution-build-final.log in the evidence root.
- Both new C# files have 161 physical lines and one type declaration. Structural
  review keeps methods below 40 lines; scoped whitespace verification and the
  repository-default diff check pass. Unrelated changes remain un-staged.

No fresh native UI, DPI/theme comparison or full App rerun belongs to this slice.
The previous native child lifecycle uses chunked HTTP, not TLS. Plaintext
fixed-length incremental delivery and its responsible local component remain
unresolved; green TLS regressions are not a workaround claim or product completion.

#### Native TLS follow-up, 2026-10-12

- Evidence is retained outside Git at
  `%TEMP%\MossAgent-fixed-tls-native-20261012-154033`, including screenshots,
  native observations, fixture source, transport logs and persistence fingerprints.
  This follow-up changes no production logic, user configuration or certificate
  validation. The six external C# files each contain one type.
- The ordinary-permission host uses the real App resources, MainWindow, view models
  and production composition registrations with an isolated synthetic database and
  authorized temporary project. Its custom Application owns startup, so this is
  not verification of the complete production App startup. Only the provider HTTP
  factory is substituted, allowing two exact loopback endpoints with exact-byte
  certificate pinning and hostname-error rejection; no certificate is installed.
  The real adapters, Agent, tool pipeline, audit storage and close coordinator run.
- Native settings interaction saves trigger guidance, child prompt, the Responses
  child model, high reasoning, sharing disabled and enabled state. A Gemini parent
  uses complete HTTP responses; the Responses child uses fixed-length HTTPS with
  its final SSE bytes held back. Partial text is visible before releasing the tail.
  Three native sends in one process prove successful completion, Stop while held
  with one disconnect and a cancelled child audit, then another successful send.
  Five parent and six child requests retain the chosen child model, reasoning and
  prompt without the synthetic parent-context marker. Three file reads use the
  standard execution pipeline and leave the authorized file unchanged.
- The final database contains 11 messages and six approved terminal audits: three
  successful file reads, two successful child runs and one `tool_cancelled` child
  run. The cancelled partial answer is persisted; no dialogue remains partial.
  A new process opens the same database without sending a request. Message and
  audit fingerprints, selected identities and saved settings remain unchanged;
  the earlier cancellation remains cancelled and no side effect is replayed.
- Screenshots cover 1280-by-820 light and actual dark views, then a 980-by-640
  light restored conversation at 96 DPI. The fixture explicitly selects Light
  on startup: dark-theme preference restoration is not verified. This is not
  high-DPI, remote-provider or same-state Codex pixel-parity acceptance. Synthetic
  SSE text joins a Markdown heading directly to preview text, so the inline
  heading is not evidence of a production Markdown parsing defect.
- Fixture setup failures are preserved: resource reassignment first fails because
  the dictionary already has a parent; a later first send fails because the
  external HTTP factory lacks BaseAddress, with zero provider requests. Only the
  external fixture is repaired. Delayed native observations are checked before
  repeating input. Both successful run and restore hosts close normally with
  exit zero and are confirmed absent. Host and then-current Debug App DLL hashes
  match in `app-hashes.json`; this identifies the verified binary, not later builds.
- This follow-up passes Providers TLS 17/17 and App child-lifecycle/configuration
  30/30. The solution builds with zero warnings/errors; external fixture whitespace
  verification passes. Logs and TRX files are retained in the evidence root.
  The preceding 5,433-case full run is not a new full run for this follow-up.
  The original plaintext fixed-length delay and its responsible component remain
  unresolved; native TLS success does not reclassify that failure.
<!-- /source: FIXED_LENGTH_STREAM_ACCEPTANCE.md -->


---

<a id="gemini-continuation-acceptance"></a>

## GEMINI_CONTINUATION_ACCEPTANCE.md

<!-- source: GEMINI_CONTINUATION_ACCEPTANCE.md -->
### Gemini 多轮协议续接验收

#### 设计自检

1. Providers 负责 Gemini 原始内容与签名的验证、保留和请求编码；Application
   提供中立且不可变的 `ModelContinuation` 契约，Agent 和子代理只传递该契约。
   不向核心模块引入 Google SDK、HTTP、Avalonia 或 SQLite 依赖。
2. 复用既有模型流和消息契约；快照仅作为已验证完成事件的附加状态，不增加工具入口。
3. 快照不传入工具请求、子代理共享文本、UI、审计或 SQLite。原有授权、审批、
   超时、取消和输出限制保持不变；失败丢弃整批尚未执行的调用。
4. 每个手写 C# 文件保持一个类型；签名每个最多 128 Ki 字符、每轮合计 512 Ki，
   内容最多 4096 个 Part、快照最多 8 Mi 字符。上下文预算计入完整快照，不能裁剪签名。
5. 测试覆盖真实设置到 HTTP、工具、审计和 SQLite 的连续两次工具轮次，
   以及错误签名、原生身份、超限、跨模型/提供商、子代理、恢复不重放和隐藏状态不泄露。

#### 已确认缺口与失败基线

- 当前旧实现只保存文字及工具名/参数；`thoughtSignature`、原生调用身份与
  空文本的尾部签名没有进入下一轮请求，签名验证服务器拒绝第二轮。
- 新增七项生产链路回归在修复前全部失败：签名正向轮次失败，六种非法续接
  没有拒绝整批工具请求，任务反而被标记完成。有效基线见仓库外
  `%TEMP%/MossAgent-gemini-signature-qa-20261007/signature-before.trx`。
- 协议确定性测试不代替真实付费供应商或全部视觉验收。

#### 协议与自动验证

- 按原顺序保留收到的 Part，不合并签名、文本或空尾部；签名视为不透明数据，
  不解码或伪造加密内容。只对结构、身份、必需字段和有限尺寸做本地校验。
- Gemini 3 的首个函数 Part 必须携带签名，同轮后续并列调用可以不携带。
  连续工具结果组成同一个用户内容块，保留每个原生函数身份与结果顺序。
  可选 null 元数据不凭空生成身份或签名；Gemini 2.5 无签名调用仍兼容。
- 串行及每步并列两个调用均完成两次工具轮次、三次 HTTP 请求；合成服务器
  严格比对每个请求的完整旧 Part、空尾部、原生身份及结果分组，而非只比对回复。
  实际设置命令、SQLite、生产 Agent、文件工具与标准审计管线均参与验证。
- 六种非法续接使用 FullAccess，仍保持原文件、0 工具审计与明确失败。
  重开继续保留同一批消息 ID，并在原服务器仍在线时确认没有新请求或工具重放。
- 主 Agent 与子代理回归验证状态只进入自身下一轮模型请求，不进入工具参数、
  子代理共享文本、展示对话或通用序列化；跨提供商、协议、模型的快照被安全拒绝。
  上下文预算保留完整签名消息，无法容纳最新原生元数据时明确失败而非剪坏签名。
- 新增 38 项回归；完整七项目 2,371 项测试通过，0 失败、0 跳过。
  解决方案构建 0 警告、0 错误。全量记录为仓库外 `solution-tests.log`、
  `solution-signature-final*.trx` 和 `solution-build.log`。

#### 普通权限与原生截图

- 仓库外宿主加载生产 App/MainWindow，1280×820，隔离临时项目、配置和数据。
  三个宿主日志均确认未提升权限，不接触用户默认数据或真实付费接口。
- 浅色签名场景用实际键盘输入并点击发送，显示两个读取记录和最终回复；
  实际点击展开第一条工具记录，显示 Unicode 文件证据。任务 Completed，
  用户/助手/工具消息分别 1/3/2，审计 2，文件未变，服务请求 3。
- 深色重启恢复同一任务、六条消息及两个工具结果；只读数据库的工作区快照
  前后完全相同，逐个精确签名、原生调用身份及内部推理标记均未进入消息正文。
  新宿主服务请求 0 只证明该服务未收到请求；严格无重放证据仍以原服务在线的
  自动回归为准，不能用重启后不同端口的 0 请求替代。
- 深色缺失必需签名场景同样用实际键盘输入和点击发送，复用共享失败提示。
  任务 Failed/InvalidProviderResponse，部分回复仅一次，工具记录与审计均为 0，
  Unicode 文件原文保持不变，服务请求 1。三个宿主都正常关闭，终端退出码 0。
- 原始 Python 被动像素截图在仓库外 `native-signed/` 的 `completed-light.png`、
  `tool-expanded-light.png`、`reopened-dark.png`，以及
  `native-missing-signature/failed-dark.png`；不缩放、激活或通过 Python 输入窗口。
  模型设置由真实 ViewModel 命令准备，未将其描述为 Windows 鼠标设置操作。

#### 验收范围

- 临时宿主、Python 脚本、截图、日志和合成数据不进入仓库；没有暂存、提交或推送。
- 未调用真实付费 Gemini；未验证所有模型的思考级别差异、所有页面、DPI、
  窗口尺寸及 Codex 逐像素一致。并列调用与子代理签名以自动生产链路回归为证据，
  不冒充本次物理输入覆盖。完整产品目标仍未完成。

#### 2026-10-10 请求构建精简

- 职责仍在 Providers，复用 Application 的消息/续接契约；未引入新的工具入口、
  缓存服务或平台依赖，不改变授权、审批、取消、超时、审计及输出上限。
- Gemini 原生调用身份在单次请求构建中按需索引一次；无工具结果时不建立索引。
  使用区分大小写的首个匹配，首个原生身份为空时不改取后续重复项；只有助手
  调用参与匹配，未知身份不伪造 ID。索引不跨请求共享，仍按整个历史查找。
- 连续工具结果直接追加到当前内容块，不再建立临时 Part 数组或复制结果树；
  结果顺序、系统消息过滤、普通消息分组与原始签名续接均保持原行为。
  签名校验只解码一次字符串，仍使用原有单项/总量预算和不透明内容约束。
- CodeGraph 调用图及源码检查确认 `IModelProvider.ListModelsAsync` 无使用方，
  移除该旧接口、四个实现中的重复投影和多余发现服务依赖，以及测试替身空实现。
  设置实际使用的 `IModelDiscoveryService` 和模型分页、代理验证未删除。
- 新增 19 项回归覆盖遍历次数、分组、重复/缺失/不同大小写身份、非助手角色、
  构建间隔离和转义签名。改动前 17 项组合测试中 3 项真实失败：8/64/128 条
  工具结果触发 10/66/130 次历史枚举；改动后固定 3 次（内容、身份、系统提示），
  无结果时仍为 2 次。此处为确定性遍历计数，不作为端到端耗时百分比。
- 专项组合/续接测试 57 项、完整 Providers 测试 351 项，以及实际应用 Gemini、
  模型发现和代理链路专项 49 项均通过；完整七项目回归 4,051 项全部通过，
  0 失败、0 跳过。解决方案构建 0 警告、0 错误。
- 19 个改动 C# 文件通过定向格式检查和 Roslyn 单类型/规模检查，21 个相关文件
  通过相对本轮起点的空白检查；生产代码净减少 45 行，不包含新增测试和文档。
  没有暂存、提交、推送或改动发布配置。
- 仓库外证据目录为 `%TEMP%/MossAgent-code-optimization-20261010`；
  本轮未进行新的截图、原生窗口交互或真实付费模型验证。
<!-- /source: GEMINI_CONTINUATION_ACCEPTANCE.md -->


---

<a id="model-configuration-native-acceptance"></a>

## MODEL_CONFIGURATION_NATIVE_ACCEPTANCE.md

<!-- source: MODEL_CONFIGURATION_NATIVE_ACCEPTANCE.md -->
### 2026-10-08 模型配置原生链路与下拉框外观验收

#### 设计复核

- 外观由 App 的共享 `ComboBoxVariants.axaml` 拥有，不在设置页添加局部样式，
  不替换原生选择、键盘导航或下拉菜单行为，继续使用 `AppComboBox`。
- 原生 Tab 焦点出现蓝色整行背景。根因是 Fluent 模板的
  `HighlightBackground` 焦点可见层，不是仅设置边框的 `Background` 层。
  共享样式改用现有 `InputBackgroundBrush` 和可见的 `BorderFocusBrush`；
  悬停、按下背景同样使用共享输入背景，焦点文字与箭头使用现有主题文字资源。
- 不新增业务端口、基础设施依赖、数据库迁移或本地化文本；不改变提供商路由、
  模型参数、任务授权、工具审批、超时、取消或输出限制。
- 新测试独立成文件。Roslyn 审计确认一个类型、182 行、最长方法 24 行；
  共享 AXAML 为 56 行，未超过文件和方法硬限制。

#### 确定性回归

- 使用实际共享 App 资源、Fluent 模板及独立 Headless UI 线程。
  修复前八项明暗主题焦点/悬停检查失败，观察到蓝色高亮层和原生半透明悬停背景；
  不是仅检查 AXAML 中存在某个 Setter。
- 最终 14 项覆盖普通/紧凑下拉框、Tab 焦点、焦点与悬停组合、原生键盘选择、
  F4 打开与 Enter 关闭、禁用拒绝焦点与选择、重新启用和焦点中的实时主题切换。
  同时断言模板实际背景、边框、文字与箭头颜色，保留可见焦点。
- 初次背景修复后，深色原生截图发现箭头偏黑；补充实际 Foreground 断言后
  12 项失败。修正主题资源引用，并覆盖下拉菜单关闭后的普通焦点，再重新验收。
  辅助测试最初资源查找/API 签名错误不作为产品缺陷基线。
- 最终相关 App 测试 56 项通过；七个测试项目串行完整回归 3,090 项通过，
  0 失败、0 跳过，其中 App 2,358 项。每项目保存独立 TRX。
- 最终解决方案构建 0 警告、0 错误；限定 C# 空白格式、相关差异空白检查、
  单类型/方法/文件规模和 AXAML XML 检查通过。

#### 真实设置到请求

仓库外宿主引用生产 App、MainWindow、组合根和共享组件，使用独立合成 SQLite
及仅监听回环地址的 TCP/SSE 服务。不读取真实账户配置、不使用真实凭据或付费模型，
不自动批准任何权限。任务保持只读策略，服务只返回文字而不请求工具。

原生鼠标和键盘完成正确提供商展开、获取模型、编辑及保存：

- 本机默认提供商获得两个模型，`discovered-local-extra` 加入正确分组；
  备用 Gemini 提供商未被修改，已有默认模型 ID `native-model` 保持不变。
- 保存显示名称“原生编辑后的模型”、上下文 65536、最大输出 2048、思考 high、
  Temperature 0.6、Top-P 0.85 和合成 metadata；默认/启用保持选中，图像/文件保持关闭。
- 重启同一合成数据目录，模型编辑器与只读 SQLite 白名单查询确认上述保存值恢复。
- 返回会话，通过真实输入框和发送按钮请求本机服务，生产提供商实际出站值为：

```json
{"model":"native-model","max_output_tokens":2048,"effort":"high","temperature":0.6,"top_p":0.85,"metadata_case":"native-ui-edited"}
```

服务只记录这些白名单值，不保存请求头、密钥或完整请求；界面显示合成流式回复。
上下文长度是本地保存值，不是 Responses 的 `context_length` 出站字段。

#### 原生截图与生命周期

- 普通权限浅色 1280×820、深色 980×640，日志均确认未提权和缩放 1。
  原生 Tab 焦点、中性背景及箭头颜色通过运行窗口复核；浅色 F4 菜单可打开/关闭。
- 深色窄窗口模型表单可滚动至高级 JSON，保存/取消栏仍固定且可见。
  使用同一数据目录重启后会话与模型字段仍在，不回放任何工具。
- Python 仅被动 PrintWindow，按当前 PID 捕获、校验稳定尺寸和非黑像素；
  不激活、点击、移动、缩放窗口或拉伸图片。实际输入使用原生 Computer Use。
- 原始浅色宿主运行 12 分钟后因 QA 生命周期到期退出 1；保存已完成但请求尚未发送。
  明确确认进程和窗口已结束后才重开验收，不把该超时描述成正常成功退出。
  后续恢复和最终明暗复测宿主走正常关闭路径，退出 0。
- 脚本、宿主、数据库、日志及截图均在仓库外：
  `C:/Users/wzheng/AppData/Local/Temp/MossAgent-provider-native-flow-20261008`。
  `combo-focus-comparison.png` 为原图同坐标、不缩放裁片，左侧修复前、右侧最终修复后。
  `light/request-result.png` 记录实际回复；`restored-model-whitelist.json` 记录恢复白名单。

#### 限制

- 真实请求仅覆盖回环 OpenAI Responses；Gemini 只是备用分组，未作原生协议请求。
  不代表公网模型、认证代理、所有提供商或真实账户验收。
- 原生修复前截图只有浅色。深色有初次背景修复后与最终复测图，不能将它们
  描述为原始生产缺陷的完整明暗前后对照。
- 禁用、悬停组合、实时主题切换及完整键盘选择由 Headless 验证；
  原生只检查上述实际操作，未穷尽全应用控件、DPI、错误或并发场景。
- 这是设置链路与共享控件缺陷的验收，不是 Codex/PI-Desktop 全页逐像素一致的证明。
  字体、布局和其他界面细节仍需继续比较，整体产品目标保持进行中。
- 未暂存、提交、推送或发布；调试继续使用 JIT，既有 Windows x64 单文件发布配置不变。
<!-- /source: MODEL_CONFIGURATION_NATIVE_ACCEPTANCE.md -->


---

<a id="model-picker-optimization-acceptance"></a>

## MODEL_PICKER_OPTIMIZATION_ACCEPTANCE.md

<!-- source: MODEL_PICKER_OPTIMIZATION_ACCEPTANCE.md -->
### Model picker optimization acceptance

#### Design self-check

- App owns presentation-only model grouping, filtering and selection. Existing
  Application configuration ports still own reads; no dependency changes.
- Reuse the current picker commands, enabled-model catalog and operation lifetime.
  Explicit choices must still clear the Agent selection and publish preferences;
  Agent synchronization must not emit either of those explicit-choice events.
- No file, provider HTTP, tool, approval or persistence policy changes. Loading,
  failed reads, disposal, unavailable identities and stale snapshots retain their
  existing rejection behavior and cancellation/generation checks.
- Remove repeated selection lookup and intermediate catalog/search copies without
  introducing a global cache or another selection service. Group membership is an
  immutable snapshot; model rows remain observable and shared across search results.
- Focused tests cover provider and partial-model matching, order and row identity,
  search visibility, current selection parameters and event order, and rejection of
  mismatched provider/model identities. Existing lifetime, reload, Agent and
  conversation tests must also pass, followed by a solution build.

#### Verification

- The corrected pre-change baseline has 11 passing and two failing cases; the
  failures demonstrate unnecessary group recreation for whole-provider matches.
  The original fixture accidentally gave an unrelated provider's model a matching
  display name; that fixture was corrected before changing production code.
- The focused picker, reasoning, Agent-selection and conversation regression run
  passes 153/153 before the final allocation assertion is added. The complete App
  suite then passes 3,592/3,592, including all 16 newly added cases. The final focused
  replay after the final solution build passes 154/154.
- After 1,000 warm-up reads, 10,000 consecutive `CanUseSelectedModel` reads allocate
  zero bytes on the measured thread. This checks the binding lookup, not overall UI
  allocation or an end-to-end performance speedup.
- The solution-wide command completes all eight assemblies but is not recorded as
  successful: one existing Windows terminal test times out waiting for natural
  cleanup. Its isolated replay passes 1/1, and the complete Windows suite replay
  passes 137/137 without any terminal-code or timeout changes. The other assemblies
  pass App 3,592, Infrastructure 196, Providers 499, Agent 83, Browser 76, Tools 483
  and Visual 8. All 5,074 unique cases are therefore verified across the completed
  runs, with the original failure retained rather than silently discarded.
- Solution build has zero warnings and errors. Whitespace checks pass for the five
  affected C# files. Roslyn auditing confirms one type per file, a maximum file size
  of 460 lines and a maximum method/accessor/local-function size of 39 lines.
- The rebuilt isolated native host uses the actual shared menu and synthetic data.
  Its six rows render correctly; searching `Gemini` shows just that provider's two
  rows. Selecting its `fixture-model` returns to the quick menu, restores default
  `none` reasoning and leaves sending enabled. The 18 existing messages are
  unchanged. The UIA set-value call reports a helper timeout, but fresh observation
  confirms the value and filtered result before any further action. The host closes
  normally with exit 0, and no new provider request is sent during this check.
- Evidence is outside the repository at
  `C:\Users\wzheng\AppData\Local\Temp\MossAgent-model-picker-cleanup-20261011`:
  corrected `tests/picker-baseline.trx`, `tests/picker-focused.trx`,
  `tests/picker-final.trx`, `full-suite/`,
  `terminal-replay/`, build/whitespace/audit logs, native observations and Python
  passive screenshots. No full-product visual parity, paid/live provider test,
  commit, push or release is claimed. Windows x64, JIT debugging and single-file
  publishing stay unchanged; unrelated working-tree changes remain untouched.

#### Incremental search follow-up, 2026-10-12

- App still owns presentation only. Filtering moves into the existing immutable
  provider-group type; it retains at most one partial membership snapshot for that
  source group. Equivalent ordered rows reuse the result, without copying another
  immutable membership array. This is not a global or cross-catalog cache.
- ApplyFilter reuses the existing collection synchronizer instead of clearing the
  entire list. Equal results publish no collection delta; narrowing/removing and
  restoring providers publish only affected groups. Model selection, reasoning,
  provider order, localized labels and shared control appearance remain unchanged.
  New catalog reads create new groups, so removed/disabled or edited models cannot
  return through an old partial-result snapshot. No execution, authorization,
  cancellation, persistence or JIT/Windows x64 publish policy changes.
- Nine new ViewModel regression cases first produce seven failures and two passes
  against the original code, retained in `tests/search-baseline.trx`. After the
  refactor, the picker/reasoning/Agent/conversation/collection slice passes 169/169.
  A further search/focus replay passes 46/46, including four new light/dark shared
  menu tests that type through the real search input, retain rendered button
  instances and focus, and execute the target model command. Counts overlap and
  are not added together. No end-to-end latency or zero-allocation claim is made.
- The final focused replay passes 173/173. The solution-wide run executes all
  5,446 cases but is not recorded as clean: 5,444 pass and two existing sidebar
  allocation assertions fail (`recent`, with and without matches), observing
  4,328 and 7,944 bytes instead of zero. All six cases in that unchanged test class
  pass in an isolated replay, without source, warm-up, assertion or timeout changes.
  Original failure logs/TRX files and the replay are retained. All unique cases
  are covered across these completed runs; a second clean solution-wide run and
  the cause of the initial allocation observations are not established.
- The solution build has zero warnings/errors; scoped whitespace verification
  passes. The four affected C# files contain one type each and have 440, 45, 153
  and 83 physical lines, within the hard limits. Production source is six lines
  shorter; tests and acceptance records are additional verification, not runtime
  infrastructure. No fresh native desktop capture or high-DPI comparison belongs
  to this follow-up, and the earlier native evidence identifies an earlier binary.
- Evidence, original-source backups and logs remain outside Git at
  `%TEMP%\MossAgent-model-search-reconcile-20261012-161841`. The existing worktree
  and empty index are preserved; no commit, push or release is performed.
<!-- /source: MODEL_PICKER_OPTIMIZATION_ACCEPTANCE.md -->


---

<a id="model-reasoning-capability-acceptance"></a>

## MODEL_REASONING_CAPABILITY_ACCEPTANCE.md

<!-- source: MODEL_REASONING_CAPABILITY_ACCEPTANCE.md -->
### Model reasoning capabilities and native acceptance

Date: 2026-10-08. This continues the Responses settings acceptance without
replacing [MODEL_CONFIGURATION_NATIVE_ACCEPTANCE.md](PROVIDERS.md#model-configuration-native-acceptance).

#### Scope and design self-check

1. Application owns the immutable protocol/model effort policy. App owns editor
   and composer presentation; Providers owns request mapping and preflight.
   Dependency direction remains unchanged.
2. Existing configuration/model ports and provider streams are reused. No new
   tool, database schema or automatic migration changes existing settings.
3. Unsupported Gemini efforts fail before HTTP-client creation with a fixed safe
   code, classified into a localized task hint. Cancellation is preserved.
   Native acceptance uses synthetic credentials, an isolated SQLite directory,
   loopback SSE and read-only tasks; no tool approval is bypassed.
4. Shared semantic combo/slider wrappers own native selection synchronization.
   Per-editor option snapshots remain stable during keyboard selection, and
   detached slider handlers are removed. One type per handwritten C# file and
   file/method limits are audited; ModelSettingsViewModel is near its hard limit.
5. Focused tests cover unsupported/supported requests, no outbound HTTP on
   rejection, normalization, retained legacy settings, keyboard correction,
   dynamic range shrinkage, draft/send protection, detach/reattach, disabled input
   and light/dark endpoint geometry.

#### Capability policy

- Gemini 3.8/3.7 Flash and 3.1 Pro: low/medium/high; Gemini 3 Pro: low/high.
  Gemini 3.1 Flash Lite Image: minimal/high. Other Gemini 3 identifiers retain
  the four-level compatible mapping; future families still need remote validation.
- Gemini 2.5 Pro keeps the existing six budget levels and cannot disable thinking;
  Flash additionally accepts none. Unrecognized older/custom Gemini aliases accept
  none only rather than silently omit an explicitly requested thinking parameter.
- Efforts are trimmed and normalized case-insensitively; off aliases none, not
  Gemini 3 minimal. Non-Gemini protocols retain the general eight-level policy;
  this does not establish every remote compatible model's capabilities.
- Newly discovered models get a supported default. Saved unsupported values are
  retained and shown with a warning; saving/sending requires explicit correction.
  A model switch never clamps a previous slider choice into the user's draft.
- Google references used for the family mapping:
  https://ai.google.dev/gemini-api/docs/thinking and
  https://ai.google.dev/gemini-api/docs/gemini-3.

#### Native evidence

All helper scripts, QA hosts, databases, logs and images remain outside repository
source under `%TEMP%\MossAgent-protocol-configuration-20261008`.
Passive Python PrintWindow captures do not activate, resize or click either app.
Actual MossAgent input uses Computer Use, not a custom UI-automation helper.

- Initial request regression: 15 capability cases fail before the implementation.
  Native iteration also exposes blank legacy selection, a medium value silently
  replacing minimal, and an exception when shrinking options during selection.
  Shared semantic controls fix these causes rather than hide the editor warning.
- Light 1280x820, ordinary permissions, scale 1: saved minimal is visible,
  rejected on save, explicitly changed to high, saved through the production UI,
  then sent through the production Gemini provider and loopback SSE response.
  Captured whitelist: model gemini-3.8-flash, effort HIGH, output 4096,
  temperature 0.4, top_p 0.7.
- The same native session selects the compatible model's eighth slider position
  and sends a real loopback request: local-compatible, effort ultra, output 4096,
  temperature 0.4, top_p 0.7. The expected response appears in the conversation.
- Rebuilt final host, Dark 980x640, ordinary permissions, scale 1: the same data
  restores saved Gemini high, the edit/save footer is visible, previous responses
  restore, and the picker displays three aligned ticks and high selection.
- Files include `light/legacy-preserved-final.png`, `light/gemini-high-response.png`,
  `light/gemini-high-parameters.json`, `light/compatible-ultra-response.png`,
  `light/compatible-ultra-parameters.json` and `light/dark-restored-editor.png`.
  Main-window PrintWindow does not necessarily include separate popup HWNDs;
  popup assertions also rely on direct Computer Use observations.
- A separate checkpoint opens the dark automation empty page and new-plan editor,
  closes the unsaved form, and inspects the docked no-conversation empty state.
  No plan is saved or executed. Images are `light/dark-automation-editor-checkpoint.png`
  and `light/dark-inspector-empty-checkpoint.png`; they are baselines, not proof of
  same-state Codex fidelity. Both final native hosts close normally with exit 0.

#### Verification and limits

- Final behavior build: solution zero warnings/errors; relevant App filter
  passes 117 tests. Providers full suite passes all 283 tests.
- Seven-project full run: 3,152 executed, 3,149 passed, three sidebar-heading
  cases fail during temporary-directory deletion with file-sharing violations,
  not layout assertions. All 12 sidebar-heading cases pass on isolated rerun.
  This is not a clean full-suite result; unrelated cleanup code is unchanged.
  TRX evidence is in `verified-results` beneath the evidence root.
- Scoped whitespace verification and 35-file AST/XML scale audit pass.
- No paid/public provider, authentication proxy, all future/custom model families,
  complete token-budget edge cases, all DPI/hover states or full Codex pixel
  equivalence is claimed. The current passive Codex capture shows conversation
  and file preview, not a matching automation/settings state.
- No staging, commit, push or publish. Debugging stays managed/JIT and the existing
  Windows x64 single-file release configuration remains unchanged. Overall product
  goal remains in progress.
<!-- /source: MODEL_REASONING_CAPABILITY_ACCEPTANCE.md -->


---

<a id="model-selection-acceptance"></a>

## MODEL_SELECTION_ACCEPTANCE.md

<!-- source: MODEL_SELECTION_ACCEPTANCE.md -->
### 模型选择与共享组件验收

2026-10-05。本记录仅描述本轮增量，不代表整个 MossAgent 或 Codex 像素还原完成。

#### 实现与设计自检

- 行为归属 App 的 ViewModel 和共享控件，继续使用 `IConfigurationRepository`；
  不增加跨层依赖、静态服务定位器或持久化迁移。
- 模型目录在本地构建完整快照，再发布到 UI；过期成功、失败结果和释放后的结果不发布。
  保留检索、明确选择的模型身份和思考强度；恢复启用时恢复同一模型，不自动改用另一个。
- 加载或失败时禁止新发送；失败提示使用本地化资源，不包含异常文本或凭据。
  禁用模型或提供商时保留草稿，正在运行的任务仍能停止。
- 旧菜单对象只有身份有效时才可执行，发布和实际请求均使用更新后的模型参数。
- 输入区模型入口、模型行和思考强度入口使用 `AppActionButton`，共享字体、间距和尾图标；
  业务页面不再手工拼装这些布局，已有 AXAML 构建检查和 C# 分析器继续生效。
- 工具授权、审批、超时、取消和输出策略保持不变；本轮没有新增生产网络请求或工具操作。
  测试服务仅监听回环地址，请求大小有界，不使用付费模型或真实凭据。

#### 自动验证

- 模型目录、真实设置到请求链路、共享组件及原生组件边界的针对性运行：473 项通过。
- 最终解决方案构建 0 警告、0 错误；全量复跑 1,409 项通过，App 为 1,050 项。
  首次全量运行在未改动的 `BrowserToolbarComponentTests` 命中了 Headless 会话释放
  的 `NullReferenceException`；相关 26 项单独复测和随后全量复跑通过，未修改该组件。
- 实际临时加入原生 `Button`，`ValidateSharedVisualControls` 返回失败；
  换成覆盖宽度的 `AppIconButton`，`ValidateSharedComponentAppearance` 返回失败。
  换成结构 `Grid`、共享操作按钮和输入框后四项相关构建检查通过；临时文件已移除。
- Headless 实际视图覆盖明暗主题的就绪、加载、错误、选中不可用状态；
  断言 12 DIP 图标、图标文字中心对齐、180 DIP 模型入口上限、长名称省略和徽标保留。
- SQLite 与本地 Responses SSE 验证实际模型身份、上下文参数、输出上限、思考强度及取消持久化。
  测试夹具等待运行收尾后清理，仅清理其自身连接池，并覆盖运行中释放及重复释放。
- 本轮相关 C# 空白格式检查与 `git diff --check` 通过；新增、修改的 C#/AXAML
  满足单类型、500 行文件和 80 行方法上限。
- 全仓库结构扫描另外发现三处已有方法超过 80 行，未在本轮改动：
  `OpenAiCompatibleProvider.StreamAsync`、`SubagentWorkspaceFlowTests` 第 21 行的方法、
  `ModelSettingsViewModelTests` 第 154 行的方法；不将全仓库规模约束记为通过。

#### 运行截图与限制

- 使用普通权限 App、独立合成项目和数据目录；日志确认 `QA_ELEVATED: False`。
  外部验收宿主通过实际 ViewModel 设置场景，不修改用户项目、配置或提供商凭据。
- Python 只使用 `--passive --include-popups`；保存原始 980×700 和 1920×1032 像素，
  不注入输入、移动窗口或缩放截图；本轮图片不是黑屏。
- 检查明暗主题模型列表、思考强度菜单、长名称，以及不可用提示和禁用的发送按钮。
  被动捕获当前 Codex 作为框架参考，没有操作其 UI；不同菜单状态不能证明逐像素一致。
- 原生模型行点击首先返回 `coordinate input geometry is unavailable`；
  刷新唯一窗口绑定并请求截图后，一次恢复点击仍返回 `failed to activate captured window`。
  此后停止原生输入，不将程序化场景和 Headless 命令验证写成鼠标/键盘验收成功。
- 脚本、宿主、日志和截图全部位于仓库外：
  `C:\Users\wzheng\AppData\Local\Temp\MossAgent-model-components-qa-20261005-01`。
  本轮创建的窗口在截图后关闭；仍需人工或可用的原生输入工具检查选择、检索与键盘关闭。

#### 2026-10-10 输入区呈现与查找精简

- App 复用同一套模型加载失败、选中不可用及未选择文案。输入区仅显示模型名和
  思考强度，提示及无障碍名称仍保留完整提供商描述；同名模型继续按提供商和模型 ID 选择。
- 恢复明确选择时不再查找默认提供商及模型，仅首次无选择时计算默认项。
  不修改目录读取、超时、取消、授权、审批、数据结构或发布模式。
- 输入区发送/停止按钮与附件图标通过共享样式变体调整，不在视图覆盖控件尺寸。
  普通权限合成窗口被动 Python 截图中，发送圆由 30×30 改为 28×28，附件加号由
  14×14 改为 12×12；与本轮 Codex 原始截图中的对应像素包围盒一致。
- 原生鼠标打开快速菜单、进入模型列表并切换 Gemini 模型后，入口和无障碍描述更新，
  合成草稿保留；Escape 关闭弹层。检查浅色最大化、深色最大化、1280×820 和
  实际拖拽缩小后的 980×700 窗口。没有点击发送、审批策略或修改用户数据。
- 图像仅裁片与原尺寸拼接，不缩放；按钮局部尺寸一致不代表整体布局、字体、明暗颜色、
  菜单或所有 DPI 已逐像素还原。长名称、同名身份及不可用状态仍以真实视图 Headless 测试验证。
- 本轮证据和比较脚本位于仓库外：
  `C:\Users\wzheng\AppData\Local\Temp\MossAgent-composer-fidelity-20261010-0010`。
- 新增 13 项回归：8 项真实输入区明暗主题、尺寸、同名身份和不可用状态测试，
  5 项模型状态文案及默认选择测试。修改前输入区 8 项中 6 项失败；最终相关 75 项通过。
- 解决方案构建 0 警告、0 错误；相关 C# 空白格式、Roslyn 单类型/规模检查、AXAML
  结构和 `git diff --check` 通过。最终验收进程普通权限正常退出，父/子模型请求均为 0。
- 两次完整测试均为 3,855/3,856，无跳过。首次在未改动的
  `PtyTerminalResizeOwnershipTests.StopRetainsNativeResizeUntilReturnAndBlocksReplacement`
  等待信号时超时，其原样 3 项复测和随后串行运行的完整 Platform 129 项通过。
  随后串行运行在未改动的 `ComposerContextOverflowTests` 命中
  `HeadlessUnitTestSession.DisposeAsync` 的 `NullReferenceException`；首次完整 App
  2,923 项及最后包含该类的 75 项复测通过。未修改这些测试或框架释放行为，
  不将分次通过记作一次全量通过；仍有间歇性测试可靠性问题待单独调查。
- 保持普通 Debug/JIT 和 Windows x64 单文件发布配置；未恢复 AOT，未暂存、提交、推送或发布。

##### Headless 生命周期根因修复

- 核对 NuGet 中 12.1.2 的仓库提交 `d3c867a9e2de379249b03dbeb3495bd7f076a81a`：
  `StartNew` 的 `Task.Run` 闭包可能在任务字段赋值前创建会话，使 `DisposeAsync`
  等待空任务；取消后先关闭队列也可能让消费者的 `Take` 抛出异常。
- 仓库外并发诊断在真实 12.1.2 二进制创建/释放 16,000 个会话，捕获 222 个空任务
  和 222 次释放空引用。新增 4 项仓库回归中，升级前立即异步释放的测试复现了
  `BlockingCollection.Take` 关闭队列异常；不是从成功复测推断根因。
- 官方 12.1.3 提交 `8eeda4f6f546165b3f72e63c9f42247abb306905` 先构造并赋值任务
  再启动消费者，并等待消费者结束后关闭队列。全部 5 个 Avalonia 包统一升级至
  12.1.3，避免混用运行时、主题、字体及 Headless 版本。相同诊断 16,000 次均无错误。
- 新增回归覆盖同步/异步立即释放、释放后拒绝调度，以及成功或失败回调后的独立应用
  资源作用域。异常不重试或忽略，不引入生产反射补丁，也不改成共享可变测试会话。
- 本轮行为归属既有框架依赖及测试基础设施；生产模块方向、工具授权/审批、超时/取消、
  输出限制、持久化和 JIT/单文件发布方式不变。诊断脚本仅在仓库外使用反射检查字段，
  不进入产品代码或测试路径。
- 证据：`C:\Users\wzheng\AppData\Local\Temp\MossAgent-headless-lifetime-20261010-0040`。
- 升级后相关 88 项通过；最终一次完整并行解决方案测试 3,860/3,860 通过，无失败或跳过
  （App 2,927、Agent 63、Browser 76、Infrastructure 144、Platform 129、Providers 302、Tools 219）。
  这是同一次运行的结果，不拼接分次复测；原终端等待超时的独立根因仍未证明。
- 解决方案构建 0 警告、0 错误；新 C# 文件 52 行、一个类型、最长方法 25 行，
  空白格式、XML 结构及 `git diff --check` 通过。
- 普通权限合成宿主原生检查深色模型菜单、实际 Gemini 切换、草稿保留及 Escape 关闭；
  查看明暗 1920×1032、浅色 1280×820 和实际拖拽后的 980×700 窗口。
  明暗主题各 4 个发送按钮、附件加号及卡片角部裁片与升级前原始像素完全相同；
  `upgrade-pixel-metrics.json` 保留比较范围。不能据此声称所有页面、状态和 DPI 均相同。
- 宿主未发送请求，普通权限正常退出，父/子请求均为 0。未操作 Codex UI、用户配置、
  审批策略或付费模型；未暂存、提交、推送或发布。完整产品及整体视觉目标仍未完成。

#### 2026-10-10 读取生命周期去重

- 行为仍归属 App：模型选择器复用既有 `SettingsOperationLifetime`，删除自己的
  生命周期取消源、超时字段及链接/释放逻辑，不增加类型、端口或依赖。
- 每轮读取仍单独持有取消源，重新读取取消上一轮，版本和释放检查仍拒绝旧结果；
  默认 30 秒超时、调用方取消、错误提示、模型身份与草稿选择行为保持不变。
- 新回归使用两套实际模型选择器和非协作的合成读取：取消或释放第一套时，第二套
  读取不被取消，且能正常发布新目录。既有测试继续覆盖超时、命令重入和迟到结果。
- 无授权、审批、文件、模型网络调用、迁移或发布方式变更；相关文件只有一个类型，
  生产文件由 423 行减至 420 行，最长方法 39 行。该非视觉重构未重新做原生模型调用，
  不把先前菜单截图算作新的端到端模型服务验收。
- 最终相关 147 项通过，解决方案构建 0 警告、0 错误，空白格式与单类型/规模检查通过。
  最新完整并行运行 3,889/3,890，通过之外有一项代理集成夹具触发五秒取消；该类原样
  独立运行 12 项通过，未修改其断言、SQLite 流程或期限，根因尚未确认。完整日志位置
  和结果区分见 [SIDEBAR_SEARCH_COMPONENT_ACCEPTANCE.md](UI_COMPONENTS.md#sidebar-search-component-acceptance) 的本轮验收，不能记作全量通过。

#### 2026-10-10 模型菜单键盘入口设计复核

1. 行为归 App 的共享模型菜单组件，仍使用 `ModelPickerViewModel` 的现有目录、
   查询和选择命令，不新增网络、文件、SQL、持久化或跨模块端口。
2. 普通权限真实窗口中进入模型列表后，UIA 确认焦点仍在对话输入框；搜索需要
   额外点击。将菜单组成移入共享 wrapper，由组件维护可见模式的键盘焦点。
3. 只在当前菜单附着、可见、启用且属于当前打开的 picker 时转移焦点；关闭、
   卸载、替换 DataContext 或过期调度不得抢回草稿焦点，订阅跟随组件生命周期。
4. 保留现有共享尺寸、颜色、图标、卡片、思考滑块与模型身份，不在业务视图新增
   外观覆盖。组件和测试各自单类型，控制文件与方法规模。
5. 先以实际 AppFlyout/共享视图验证进入、初始列表、隐藏搜索、关闭和输入草稿
   边界；修复后用原生键盘检索、切换模型、关闭，再检查明暗主题与不同尺寸。

#### 2026-10-10 共享模型菜单与焦点生命周期实现

- 菜单组成移至 `AppModelPickerMenu`，业务视图缩为 7 行共享组件宿主；移除其
  代码后置未使用的原生控件引用，构造函数改为直接初始化。没有复制第二套菜单。
- 逐项比较修复前 AXAML 快照与新组件：原卡片子树、业务绑定、Classes、尺寸和
  外观属性完全保留，仅为搜索与快速入口添加内部名称；根最大高度保持不变。
- 共享组件在附着时订阅当前 picker，卸载和替换上下文时解除旧订阅。布局就绪后
  再聚焦可用入口，并检查调度版本、当前上下文、打开状态及有效可见/启用状态。
  关闭或旧 picker 通知不能抢回草稿焦点，重新附着按当前页面模式恢复入口。
- 程序化焦点改用默认导航来源。原生实窗选择模型后观察到方向导航造成的粗焦点框，
  修复后明暗主题均不再出现；真实 Tab 导航的键盘焦点提示没有被全局禁用。
- C#/AXAML 门禁禁止业务页面改写内容、模板和几何。新增门禁测试实际发现
  `DataTemplates` 的 AXAML 覆盖此前未被拒绝，已补齐，保留数据和可用性绑定。
- 新增 43 项测试，覆盖明暗进入/初始列表、实际键盘输入、搜索隐藏的选中行、
  关闭、上下文替换、旧 owner 通知、卸载/重新附着、组件及宿主隐藏/禁用，
  以及程序化导航来源、Tab 焦点提示和四种 AXAML 外观覆盖写法。
- 搜索焦点修复前 6 项断言失败；补门禁时 1 项断言失败；程序化导航来源修复前
  明暗 2 项断言失败。均保留修复前 TRX，不以编译失败作为行为红灯证据。

##### 原生复测及截图边界

- 普通权限、独立合成项目与目录；最终浅色、深色宿主均记录 `QA_ELEVATED: False`。
  最终深色为 980×700，浅色最大化为 1920×1032。实际鼠标进入模型列表后不点击
  搜索框，观察其输入光标再用原生键盘输入 `2-4`/`1-5`，列表只保留匹配模型。
- 实际点击结果后模型入口更新，原草稿完整保留，快速菜单没有粗焦点框；Escape
  关闭弹层。最终两个宿主均通过 Alt+F4 关闭，父执行会话 exit 0，进程不再存活。
- UIA 经常返回操作前的树，焦点字段也会报告窗口根；本轮以刷新后的输入光标、
  实际检索结果及草稿状态联合验证，不把旧 UIA 的根节点当作搜索焦点成功证据。
- Python 仅使用 `--passive --include-popups --pid` 截取 MossAgent；Codex 仅被动
  截图，没有操作其界面。早期 Codex 图被验收窗口遮挡，不用于整体比较；最终
  `codex-reference-clear.png` 在所有验收窗口关闭后捕获并确认无遮挡。
- 原始截图均通过非均匀像素检查；修复前与最终菜单各裁取 233×380 原尺寸片段，
  不缩放，核对菜单位置关系、宽度、字体、图标与列表行无意外变化。不同焦点、
  鼠标及视口状态产生局部差异；Codex 截图没有相同的模型菜单状态，本轮不声称
  该菜单或整个界面已逐像素一比一一致，也未验证其他 DPI。
- 没有发送消息、真实模型请求、审批操作、用户配置写入、迁移或发布模式变更。
  所有脚本、宿主、日志、截图及结构审计程序均在仓库外：
  `C:\Users\wzheng\AppData\Local\Temp\MossAgent-model-picker-keyboard-20261010`。

##### 最终自动检查与全量边界

- 模型目录/菜单、真实共享视图、焦点生命周期、无障碍、共享图文行和门禁的
  最终针对性运行 166/166 通过：`focused-recheck/focused-recheck.trx`。
- `dotnet build MossAgent.slnx --no-restore -v:minimal` 0 警告、0 错误。
  本轮相关 C# 的 `dotnet format whitespace --verify-no-changes` 和作用域
  `git diff --check` 通过；未引入格式工具或修改无关格式。
- Roslyn 对本轮涉及的十个手写 C# 文件检查单类型、文件与方法长度，均满足
  500/80 行硬上限；共享菜单代码 87 行、最长方法 13 行。两份 AXAML 为 98/7 行。
  这不是全仓库结构审计，不能替代已有超限项的单独整改。
- 首次完整默认并行运行 3,960/3,963，通过之外的三项结构测试仍读取旧业务
  菜单文件。更新读取入口至共享组件，原无障碍、滑块和图文行断言全部保留，
  并增加业务视图确实只宿主该共享组件的断言。
- 最终完整默认并行复跑 3,962/3,963：本轮相关项全部通过，但未改动的
  `WorkspaceMcpFlowTests.VerboseOrFailingMcpCall_ProducesBoundedSafePersistedResult`
  的 `error/tool_failed` 场景等待完成时触发夹具 30 秒取消。该类随后原样单独
  复测 7/7 通过；未修改 MCP 实现、测试断言、SQLite 流程或期限，根因尚未定位。
  不将独立通过代替完整回归，不记录本轮全量全绿。
- 保留两次完整运行以及独立复测结果：`full-final`、`full-recheck`、`mcp-isolated`；
  对应 `solution-tests-final.log`、`solution-tests-recheck.log`、`mcp-isolated.log`。
  未暂存、提交、推送或发布；普通 JIT 调试与 Windows x64 单文件发布策略不变。
<!-- /source: MODEL_SELECTION_ACCEPTANCE.md -->


---

<a id="provider-catalog-acceptance"></a>

## PROVIDER_CATALOG_ACCEPTANCE.md

<!-- source: PROVIDER_CATALOG_ACCEPTANCE.md -->
### 提供商与模型目录状态验收

#### 设计复核

- 行为由 App 的共享 `ProviderModelCatalogViewModel` 拥有；提供商与模型设置
  使用同一快照，不再分别读取后发布不完整列表。页面继续使用共享组件与样式。
- 复用 `IConfigurationRepository` 和 `IModelDiscoveryService`，UI 不直接操作
  SQL、文件、进程或 HTTP；没有数据库迁移或新的具体基础设施依赖。
- 配置读取使用生命周期取消和 30 秒总超时，仓库忽略取消时也限制等待；
  版本号拒绝过期成功和失败。仅显示本地化安全提示，不回显原始异常或凭据。
  模型发现还关联自身设置生命周期，关闭后不能保存晚到结果。
  原有工具授权、审批、取消、输出限制和执行管线不变。
- 编辑器刷新保存身份但不覆盖字段。删除目标后禁用保存/删除并保留草稿；
  模型还需属于当前提供商。刷新期间和错误状态不能执行列表修改操作。
- 原生截图发现禁用行操作仍与可用状态相近，修正共享设置行操作变体：
  禁用时半透明和箭头光标，恢复时正常；获取/添加模型也统一共享 24-DIP 操作高度。
  明暗页面测试同时断言禁用和恢复后的所有目录操作透明度。
- 新类型独立成文件。全仓库 876 个 C#/AXAML 文件通过类型、文件和方法体积审计，
  没有超过硬限制；不使用 partial 拆分大型 ViewModel。

#### 测试与编译门禁

新增 25 项测试覆盖：

- 初始加载、失败和成功空结果区分，部分模型读取失败保留整个旧快照。
- 过期成功/失败不能覆盖新状态；超时和释放限制不配合的读取且无晚到发布。
- 所有模型读完才发布、过滤外来模型归属、读取期间改变展开及选择状态。
- 草稿保留、原密钥留空时使用最新保存值、删除目标不能复活或换到另一提供商。
- 失效目标与外来模型拒绝保存、禁用状态、释放订阅和取消模型发现。
- 明暗主题的实际 Headless 页面：加载/错误不显示空卡片，Space 触发刷新并恢复；
  错误后保留列表与打开的模型草稿，禁用保存，重试后恢复可用。

验证结果：含共享设置行的最终专项 150 项通过；完整解决方案 1,881 项通过，其中 App 1,392 项，
均无失败或跳过。解决方案构建 0 警告、0 错误；本轮 C# 空白格式检查、
类型/体积审计与差异空白检查通过。

仓库外项目实际引用生产分析器：原生 Button 被 `MOSSUI001` 拒绝，
实例 FontSize 被 `MOSSUI002` 拒绝；合法共享组件、结构布局和语义变体编译通过。
直接导入生产 App 项目的 AXAML 门禁同样拒绝原生 Button 和实例 FontSize；
合法共享提示、结构 Grid、数据及命令绑定通过 18 项通用门禁。
只将核对到预期规则的失败计入拒绝证据。最初验证脚本使用了错误的目标名和
过严的诊断文本匹配，已修正并复跑；这些脚本错误不计为门禁成功。

#### 原生截图

普通权限临时宿主复用真实 MainWindow、设置页、共享组件和样式，
只替换内存配置仓库，不调用付费模型、网络或写入用户配置。
Python 被动 PrintWindow 每次按当前 PID 找到窗口，检查尺寸稳定和非黑像素。

- 浅色 980×700：初始加载、首次失败、重试后显示提供商。
- 深色 980×700：失败后的模型编辑器、重试后的同一草稿；保存按钮由禁用恢复。
- 浅色最大化 1920×1032：失败保留展开的提供商和模型行，重试移除错误提示并恢复操作。

第一次临时宿主缺少生产 DPI 清单，原生尺寸与预期不符，截图脚本拒绝保存。
加入同一 `asInvoker`、PerMonitorV2 清单后重新运行，980×700、缩放 1 的截图成功。
不将失败截图或日志中的状态文字代替渲染证据。

证据及临时源码均在仓库外：
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-catalog-loading-qa-20261006-01`。
`catalog-disabled-before-after.png` 使用两个真实最大化窗口中的相同区域、不缩放裁片，
对比禁用行操作修正；最终行状态截图位于 `final-light-maximized` 和 `final-dark-draft`。
所有验收宿主正常关闭，保留日志、源码和图片，不提交截图脚本或合成配置。

#### 限制

原生状态转换由临时宿主触发，键盘重试由 Headless 页面验证；
不等同于 Windows 实体鼠标/键盘验收。未验证所有 DPI、所有设置页、
所有悬停细节或 Codex 逐像素一致，未以真实凭据测试在线模型发现。
组件门禁与目录状态通过不代表整个 Coding Agent 产品目标完成。

#### 2026-10-10 读取边界复核设计

1. 行为归属 App 的配置呈现，仍复用 `IConfigurationRepository`，不跨模块访问 SQL、
   文件或 HTTP；提供商目录与保存前代理验证使用既有设置生命周期组件。
2. 读取保持默认 30 秒和 owner 取消，不延长生产期限；等待不配合的异步读取有界，
   同步阻塞返回后的已取消结果也必须拒绝，不能声称能中断任意同步端口。
3. 代理读取失败保留原选择和草稿并拒绝保存，释放后不再发布消息或写入；修改仍由
   原修改执行器持有，读取超时不回滚已经开始的外部写入。
4. 新测试类型分别成文件；现有提供商视图模型保持低于 500 行，不使用 partial 拆分。
5. 回归需证明已取消但已完成任务的拒绝、非协作等待、释放、失败恢复、草稿保留和
   不写入；实际模型发现、代理及直连路由继续由原设置集成测试验证。

#### 2026-10-10 读取边界实现与原生检查

- 目录改为复用 `SettingsOperationLifetime`，移除重复的超时和取消源管理。
  该组件持有稳定 owner Token，释放后仍可安全读取其已取消状态；目录直接引用该
  Token，不再额外缓存。读取操作独立，释放一个 owner 不会取消另一个 owner。
- 已完成的 Task 会跳过 `WaitAsync` 的取消检查。现在读取提供商、读取模型和发布前
  均重查取消与读取版本，拒绝同步端口在取消后返回的已完成快照；保留原列表，
  显示安全错误并阻止修改。不能由此推断任意同步端口可以被强制中断。
- 新增 10 项回归：目录已取消完成与释放、代理刷新/保存前验证的非协作读取、
  已取消完成、释放后静默，以及稳定 owner Token。原始 8 项行为用例在修复前
  全部失败，`read-boundary-before.trx` 保留真实断言失败，不是编译失败。
- 普通权限真实 App/MainWindow/共享提供商页面，浅色 1280×820、深色 980×700，
  均为 96 DPI。真实鼠标刷新触发读取错误，保留列表并禁用行操作；浅色通过
  Shift+Tab 和 Space，深色通过鼠标重试恢复。转场重新观察稳定后再截图。
- 仓库外宿主仅将该页面连接到合成内存配置仓库；测试期限注入为 300ms，生产仍为
  30 秒。首次读取忽略取消并迟到返回，未发布迟到名称；两次宿主均正常退出码 0。
  未读取真实用户配置、发送模型请求、执行工具或改变审批策略。
- 证据目录 `C:/Users/wzheng/AppData/Local/Temp/MossAgent-provider-read-boundary-20261010`：
  `light-ready.png`、`light-failed.png`、`light-recovered.png`、`dark-ready.png`、
  `dark-failed.png`、`dark-recovered.png`、原生日志和各专项 TRX。
  宿主、截图脚本与图片不进入仓库。

本次证明配置读取和恢复状态，不是实际 SQLite 故障注入、公网模型调用、全 DPI
或 Codex/PI-Desktop 的逐像素一致性证明。完整产品目标仍未完成。

#### 2026-10-10 最终验证

- 提供商、代理、模型选择器和生命周期专项 249 项全部通过，结果
  `read-boundary-final-focused-v2.trx`。新增读取/生命周期回归共 10 项。
- 最终一次完整并行 `dotnet test MossAgent.slnx --no-build --no-restore`：七个测试
  项目共 3,900 项全部通过，0 失败、0 跳过，其中 App 2,967 项、Platform 129 项。
  `full-final` 保留该次七份独立 TRX，`full-tests-final.log` 保留输出；没有拼接专项
  或重复测试结果来形成全量通过。此前失败的终端夹具及修正详见
  [TERMINAL_LIFECYCLE_ACCEPTANCE.md](TERMINAL_TOOLS.md#terminal-lifecycle-acceptance)，初始全量失败证据仍保留在 `full`。
- 完整解决方案构建 0 警告、0 错误。九个相关 C# 文件的空白格式、Roslyn 单类型
  和规模审计通过；最大文件 469 行、最大方法 58 行，未突破硬限。
  仓库 diff 与相关未跟踪文件的空白检查通过。日志为 `solution-build-final.log`、
  `format-final.log` 和 `source-audit-final.log`，均在上述仓库外证据目录。
- 未暂存、提交、推送或发布；普通 JIT 调试、win-x64 单文件发布设置未变。
<!-- /source: PROVIDER_CATALOG_ACCEPTANCE.md -->


---

<a id="provider-dialog-native-acceptance"></a>

## PROVIDER_DIALOG_NATIVE_ACCEPTANCE.md

<!-- source: PROVIDER_DIALOG_NATIVE_ACCEPTANCE.md -->
### Provider and model dialog native acceptance

#### Plan and design self-check

- The preceding TLS turn made verified progress; the complete product objective
  remains active. This continuation returns to native provider/model settings.
- Use production App/MainWindow, composition, shared controls/styles, existing
  settings commands and SQLite ports. Repository-external QA code only seeds
  isolated synthetic data and exposes a bounded loopback discovery/text service.
- Start with fresh screenshots and real expansion/editing/focus input. Check
  wide/narrow layout, long labels, fixed dialog footers, theme switching, saving,
  discover/manual model identity and restoration, without deleting configurations.
- Ordinary privilege, a private temporary data/project root and synthetic keys
  only; no real account, paid request, user configuration, approval change,
  authentication/security UI or system setting is touched. GUI input uses Sky;
  the external Python helper only captures passive pixels for comparison.
- Any observed product defect needs a failing focused regression and a fix in
  App's owning shared control/style or settings model. Preserve dependency,
  lifetime/cancellation, localization, one-type and file/method constraints.
- No production change is assumed before fresh runtime evidence. Run the relevant
  tests and solution build after a correction, and report native coverage exactly.

#### Evidence

Repository-external root: `%TEMP%\MossAgent-provider-dialog-20261012`.
Before/after passive Python screenshots, bounded observations, original failing
regressions, successful checks and normal-shutdown logs are retained there.

#### Confirmed native defect and correction design

- The preceding catalog cleanup turn made verified progress (App 3,694/3,694,
  solution build). This continuation revalidated the existing ordinary-privilege
  980x700 host, PID 17696, instead of restarting it for an observation timeout.
- Actual input opens the existing model editor, selects Appearance, then returns
  to AI providers. The dialog and draft remain open, but focus stays on the
  navigation button. A real Escape key leaves the model editor open. Independent
  UIA focus and the bounded in-process observation agree after the transition.
- The correction belongs to App's shared `AppSettingsDialog`: observe effective
  ancestor visibility, restore an available dialog-owned focus target, and reject
  queued transfers after hiding, closing or detaching. Preserve page navigation
  and drafts rather than changing the modal's scope or disabling the sidebar.
- No new I/O, port, tool, approval, timeout or persistence behavior is needed.
  Native confirmation uses only synthetic configuration. Focus regressions cover
  light/dark, last-field selection, unavailable children, reattachment, late
  callbacks and Escape; code must retain one type per file and size limits.

#### Implemented correction

- The shared dialog observes visibility/enabled changes on its attached visual
  ancestors and itself. Those subscriptions are released on detachment; no global
  focus hook or configuration cache is introduced.
- Returning to a retained page restores the last available dialog-owned field,
  otherwise an available input/action. A newer focus already inside the dialog
  is not overridden. Hiding, closing and detaching invalidate deferred transfers.
- TextBox's configured clear/retain-selection behavior stays unchanged. The final
  regression covers both policies instead of requiring every input to retain its
  selection after losing focus. Templates, dimensions, footer layout and colors
  are unchanged.

#### Verified results

- The pre-correction focus baseline has 8 failures and 5 passes, including the
  native page-return failure. Two additional selection-policy cases bring the
  final focus cases to 15. Focus/dialog/picker/page-switch/draft/edit-menu regressions
  pass 68/68; App full suite passes 3,709/3,709.
- Architecture constraints pass 7/7. Solution and native-host builds have zero
  warnings/errors. Scoped whitespace and diff checks pass. Roslyn audit reports
  one type per file, 169/161/81 file lines, and maximum method sizes 26/23/21 for
  the shared control, focus tests and fixture respectively.
- The old host closes normally with exit 0 and request counts 0/0. The rebuilt
  ordinary-privilege host, PID 17424, runs the production window/composition and
  independent synthetic SQLite root. It starts at 980x700 DIP, scale 1.
- In light/narrow mode, real navigation away and back restores model-ID focus;
  a real Escape closes the editor and restores the edit-row trigger. In
  dark/narrow mode, an edited display-name draft survives navigation and focus
  returns to that last field. Passive screenshots and bounded observations prove
  both outcomes.
- Real title-bar maximization checks dark/wide 1920x1032 DIP, scale 1, with the
  existing fixed dialog footer visible. Saving the synthetic display name updates
  the current model row. The native discovery action calls the bounded loopback
  service, adds `discovered-local-extra`, and preserves that edited model.
- Real manual addition creates `gemini-2.5-flash` only under the secondary Gemini
  provider. Reopening restores its ID, fallback display name, 128000 context,
  8192 output and medium effort. Real Tab reaches the next field; Escape closes
  that editor. No Gemini request is sent to the Responses-only QA service.
- The UIA set-value helper times out once; initial observation still shows the
  old text, and a later refreshed screenshot shows the intended new value. No
  second set-value call is made. Page/role trees can lag rendered transitions;
  subsequent inputs use independently refreshed text or current screenshot IDs.
- The rebuilt host closes normally with exit 0 and request counts 1/0 (discovery/
  response). No configuration is deleted, no real secret is entered, and no
  approval/security/privacy setting is changed. All GUI input uses Sky; Python
  performs passive captures only. No commit, push or publication is performed.

#### Remaining scope

- This proves the model editor's page-return behavior, not every settings dialog
  or full-product visual parity. Native provider/proxy/MCP editor transitions,
  additional DPI scales and every theme/window combination remain unverified.
- Caption-button focus behavior is outside the page-return correction. At that
  checkpoint, the manual editor displayed the previous discovery status until
  saving; the status-ownership follow-up below corrects that separate issue.
- Real external-provider streaming, full-product workflows and a fresh Codex/PI
  comparison are not claimed here. The complete product objective stays active.

#### Model status ownership follow-up design

- The confirmed stale footer is caused by the page and dialog sharing one status.
  Regression additionally reproduces a pending discovery or another row's delete
  replacing the current editor's validation error. The original focused baseline
  has 20 failures and 4 passes across light/dark production-pane bindings.
- App owns the correction: keep page operation results available while the model
  footer binds only to its draft's validation/save error. Editor entry/reset/close
  clears that draft status; retained-page navigation does not. Existing revision
  checks continue rejecting late save/delete errors from an older draft.
- Extract discovery orchestration/error mapping from the nearly 500-line settings
  model. Reuse `IModelDiscoveryService`, configuration ports and the catalog's
  mutation/import pipeline, without new I/O, global state or persistence changes.
- Preserve provider ownership, settings lifetime cancellation and the 30-second
  default discovery deadline. Check cancellation after awaited ports and never
  expose exception text, API keys or proxy credentials. This changes no tool,
  authorization, approval or output policy; synthetic native QA needs no secrets.
- Each handwritten file retains one type, file/method hard limits and shared
  visual variants. Focused regressions cover fresh/reopened editors, pending
  discovery success/failure, unrelated deletion, late writes and actionable retry;
  solution build and native screenshots follow before handing off this slice.

#### Model status follow-up verification

- The 24 new production-pane cases all pass; the initial combined focused suite
  passes 102/102. The initial App full suite passes 3,733/3,733. The extracted
  operation retains the provider/import ownership and existing lifetime guards;
  success also restores the page's non-error appearance without clearing the
  current editor's validation error.
- A further disposal regression catches direct command execution reading an
  already disposed token source. Guarding the dispatch before obtaining that
  token prevents discovery, persistence and late status publication; command
  availability alone is not treated as a safety boundary.
- Final verification after that guard passes the combined focused suite 103/103
  and App full suite 3,734/3,734 (`app-final.trx` in the external evidence root).
  The solution build again has zero warnings/errors; whitespace and size audits
  also pass. The final safety test file is 117 lines, one type, max method 65.
- Both solution and external native-host builds pass with zero warnings/errors;
  architecture constraints pass 7/7, scoped whitespace and diff checks pass.
  Roslyn audit reports one type per file: model settings 462 lines/max method 40,
  discovery operation 78/28, new visual regressions 169/28 and fixture 69/16.
- Native evidence lives in `%TEMP%\MossAgent-model-status-20261012-092036`.
  The new synthetic production-composition host runs without elevation at
  980x700 DIP, scale 1, and never accesses real user configuration or secrets.
  A real discovery action imports the extra model only for the requested primary
  provider. Opening the secondary provider's new editor keeps that page success
  visible behind the dialog but leaves its own status empty.
- Real blank-ID save shows the current localized error in the fixed footer.
  Navigating to Appearance, switching to dark, and returning retains that error
  and draft. Typing `gemini-2.5-flash` and retrying saves only under the secondary
  provider; reopening restores its parameters with no previous save-success
  footer. Maximizing to 1920x1032 DIP preserves that clean footer and layout.
- Passive Python screenshots are `light-new-after-discovery.png`,
  `light-validation-error.png`, `dark-error-after-page-return.png`,
  `dark-reopened-after-save.png` and `dark-wide-reopened.png`; bounded observations
  separately confirm the page/editor status. Comparing the preceding native
  `dark-manual-reopened.png` confirms its old "model added" footer is gone.
  `status-footer-comparison.png` retains the two passive native footer crops.
- The host exits normally with code 0 and request counts 1/0 (discovery/response).
  All GUI input uses Sky; Python only captures pixels. The initial UIA click has
  unavailable geometry, so the window is freshly rebound before a screenshot-
  backed retry. Stale UIA trees are not used to repeat completed actions.
- Pending discovery/delete and late-save isolation are deterministic headless
  regressions, not claimed as native concurrent-operation coverage. Additional
  DPI scales, real external streaming and full-product visual parity are still
  outside this slice. No commit, push or publish; the complete goal stays active.
<!-- /source: PROVIDER_DIALOG_NATIVE_ACCEPTANCE.md -->


---

<a id="provider-model-editor-session-acceptance"></a>

## PROVIDER_MODEL_EDITOR_SESSION_ACCEPTANCE.md

<!-- source: PROVIDER_MODEL_EDITOR_SESSION_ACCEPTANCE.md -->
### Provider and model editor sessions

#### Native defect and design self-check

- Fresh ordinary-permission production App input on 2026-10-13 confirms that
  Escape/cancel followed by editing the same provider or model retains discarded
  fields. The provider also retains its invalid-URL validation footer. The saved
  SQLite configuration remains unchanged; this is an editor-session defect.
- App's provider/model view models own the correction. Route selected-item and
  row edit commands through their existing catalog-validated entry methods.
  Reload saved fields when starting a fresh session on an equal selected item;
  preserve a still-open draft and ordinary page navigation.
- Reuse current catalog identity/ownership checks, shared dialogs and revision
  guards. No new port, I/O, dependency, persistence schema, secret logging,
  authorization, approval, timeout, cancellation or output behavior is introduced.
- Each handwritten C# file has one type, remains below 500 lines and keeps methods
  below 80 lines. Focused tests cover cancel/reopen parameters and validation,
  already-open draft preservation, unavailable/disposed targets and zero writes.
- Verify the failing baseline, focused/wider regressions and solution build, then
  replay real provider/model cancellation with passive Python screenshots.

#### Evidence ownership

Repository-external evidence stays at
`%TEMP%\MossAgent-provider-draft-20261013`. The original production App assembly is
`CD5FD637D11AADEF8A0379E56E3234B7B044A1E7E0CF66BBC8E50736B8642BC8`.
The independent fixture is a SQLite backup of a normally closed synthetic
fixture, not user configuration. All input uses Sky; Python only captures passive
pixels, prepares synthetic data and performs read-only bounded audits.

`provider-reopened-after-cancel-before.png` and
`model-reopened-after-cancel-before.png` show both retained discarded drafts.
`provider-return-draft.png` independently shows successful page-return draft/focus
preservation. Invalid save leaves all 19 table schemas and rows, three artifacts,
request history and light UI settings unchanged. No model request or deletion is
performed. One model set-value observation times out; fresh window selection
confirms the intended value, so input is not repeated or claimed unsuccessful.

#### Verification

The shared entry points now reload saved fields only when opening a fresh session
on the same selected catalog item. An already-open editor retains its draft.
Selected-item commands reuse the row entry points' availability and disposal
checks instead of opening an unchecked session.

- Baseline `session-baseline.trx`: eight failures and four passing preservation
  cases reproduce both cancellation defects and the unchecked selected entry.
- Repaired `session-fixed.trx`: all 12 cases pass. `session-wider.trx` passes
  311 related provider/model/dialog/configuration/page-switch cases.
- `session-architecture.trx`: seven architecture constraints pass;
  `session-pixels.trx`: all 48 App.Visual.Tests pass.
- The solution build has zero warnings/errors; scoped whitespace verification
  and source diff checks pass. This slice does not re-run the complete App or
  eight-assembly suite and does not reuse the earlier 5,804-case result as fresh
  validation of this binary.

TRX files are external under the evidence root's `tests` directory. The repaired
Debug/JIT App is isolated under
`.scratch/settings-editor-session-20261013/bin/MossAgent.App/debug_win-x64`;
its assembly SHA256 is
`04DE522716B323CCBCEB3BB8B7BA0907014EC53F1C5C2B89EA8736DE14CD141F`, unchanged after
the tests, solution build and native replay. Default output is not updated.

#### Repaired native replay

Independent `after` data and ordinary-permission production-App input confirm:

- Light/maximized provider editing: invalid save shows the actionable URL
  validation footer without a stored-row change. Navigating to Appearance,
  switching dark and returning retains the unsaved fields, validation and
  save-button focus.
- Dark/maximized Escape/reopen of the same provider restores the saved name and
  loopback endpoint and replaces the stale error with the ordinary empty-key
  hint. Returning to light keeps those restored values.
- Light/maximized model editing: page return retains the unsaved display name
  and input focus. Cancel/reopen restores the saved display name, model ID,
  context length, output limit and reasoning effort.
- Restore to 1280x820 at 96 DPI keeps the model dialog and fixed footer visible
  without black padding. Escape closes the editor and returns focus to its edit
  entry; the actual caption close exits the App and observer normally with code
  zero. Fresh enumeration and process/port checks confirm cleanup. The immediate
  post-close window enumeration raced shutdown; it is not counted as exit proof.

`after/native-observations.json` records the observed checks.
`after/final-closed.audit.json` passes SQLite integrity and exact comparison of
all 19 table schemas/rows, all three original artifact hashes, request history
and restored light UI settings. No model/discovery request, deletion or real-user
configuration write occurs. One model set-value observation times out; a fresh
window observation confirms the value without repeating input. Immediate UIA
trees can lag dismissal, and the provider validation footer is checked in pixels
rather than inferred from its absence in the tree.

The provider/model reopening frames and `model-light-normal-clean.png` have
passive Python metadata. `native-session-comparison.png` places equal-size light
1920x1032, 96-DPI native before/after crops side by side without rescaling. Its
canvas height follows crop dimensions to avoid generated trailing black padding.
This compares editor-session behavior, not Codex appearance.

#### Remaining scope

The fresh passive `codex-reference-final-pass.png` is still the conversation page,
not Appearance, and is not used as an equivalent settings reference. Codex is
not automatically navigated. Full native Appearance/PI-Desktop comparisons,
high-DPI and complete interaction/state coverage remain open. This healthy
resize replay does not repair the separately reproduced native clipping defect.
No shared dialog appearance, persistence schema, packaging, AOT, JSON or approval
behavior changes. No commit, push or release occurs. The full goal remains active.
<!-- /source: PROVIDER_MODEL_EDITOR_SESSION_ACCEPTANCE.md -->


---

<a id="provider-parser-optimization-acceptance"></a>

## PROVIDER_PARSER_OPTIMIZATION_ACCEPTANCE.md

<!-- source: PROVIDER_PARSER_OPTIMIZATION_ACCEPTANCE.md -->
### Provider parser optimization acceptance

#### Scope: 2026-10-10

- Anthropic tool blocks now retain call ID, wire name and argument buffer in one
  dictionary entry. Capture and removal no longer maintain three parallel maps.
- Compatible Chat parsing uses one event list per frame instead of allocating
  and copying another list for each selected choice. The list is not cached or
  shared between frames, requests or parsers.
- The two production files decrease from 224 to 213 physical lines. Added tests
  and this document increase total repository lines; this is not a claim that
  all repository code became smaller.

#### Design and safety boundaries

- Behavior stays in Providers with the existing Application model-event port.
  No project dependency, package, database schema or public contract changes.
- Tool-name decoding and event order remain unchanged for accepted frames.
  Other choices cannot contribute text or tool calls to the selected response.
- Compatible parsing failures discard all earlier events in that frame, including
  completed calls. Terminal failures cannot flush pending calls on EOF or DONE.
- Anthropic block state stays parser-owned. Unknown indices, repeated stops,
  index reuse and interleaved calls cannot consume another block's arguments.
  Existing invalid-argument exceptions are preserved, not converted to success.
- HTTP/SSE cancellation, encoding checks and size limits remain upstream;
  authorization, approval, timeout, output limiting and audit remain downstream.
  No new execution path or shared mutable state is introduced.
- Every changed handwritten C# file contains one type. Production files are 94
  and 119 lines; the longest changed-file method is 32 lines.

#### Deterministic verification

- Fifteen new characterization cases passed against the original parsers before
  the refactor. An initial test assertion incorrectly required the exact
  `JsonException` type; allowing its `JsonReaderException` subtype corrected the
  assertion without changing production behavior.
- Seventy-four targeted Anthropic/Compatible tests pass after the refactor,
  including existing HTTP streaming, cancellation, argument/count/identity
  limits, incomplete responses and safe malformed-frame rejection.
- New cases cover interleaved block ownership, replacement/reuse, independent
  parsers, frame event order, whole-frame failure and immutable prior results.
- Full solution build passes with zero warnings and zero errors. All eight test
  assemblies pass: 4,258 tests, zero failures and zero skips. Shared-component
  and headless visual tests are included; these are not native screenshot QA.
- Focused whitespace verification, one-type/file and file/method size checks,
  and tracked-file diff whitespace checks pass.

#### Allocation measurements

The repository-external synthetic harness runs under .NET 10 Release JIT,
warms each scenario 2,000 times and takes five samples of 20,000 operations.
It reuses tool-name metadata and validates event shapes on every operation.
Steady allocation readings from samples 2-5 are:

| Scenario | Before bytes/op | After bytes/op | Reduction |
| --- | ---: | ---: | ---: |
| Compatible frame, one selected entry | 472 | 384 | 18.6% |
| Compatible frame, four selected entries | 928 | 576 | 37.9% |
| Anthropic parser, one tool block | 1,496 | 1,096 | 26.7% |

These are local parser allocation measurements, not whole-application memory,
latency or throughput guarantees. Timing samples fluctuate and are not used to
claim a speedup. No live provider account, paid API or native UI was exercised
for this internal refactor; markup, styling and release/JIT settings are unchanged.

#### Evidence

Repository-external logs, TRX files, original parser snapshots and benchmark:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-parser-optimization-20261010-01`.

#### Gemini follow-up: 2026-10-11

##### Design and safety self-check

- Providers retains all behavior through the existing Application model events;
  no port, dependency, package, database, UI or publication setting changes.
- Text-only and metadata frames reuse the mapper's independently owned snapshot,
  instead of allocating and copying a second event list. This happens only after
  failure, finish-state and signed-continuation checks. No list is cached or reused
  by a later frame or another parser; previously returned events stay unchanged.
- Tool counting uses one frame-local counter instead of scanning all previously
  mapped text/tool events at every call. Thought and text parts do not consume
  the 128-call budget. Tool names/native identities are decoded once instead of
  allocating the same string repeatedly for validation and publication.
- Mixed frames retain call buffering and ordered text projection. Malformed,
  blocked, oversized or post-finish frames still fail closed, discard that frame
  and clear pending calls. Native identities, argument cloning, continuation
  signatures and whole-stream budgets retain their original checks.
- SSE cancellation, encoding/size limits and safe failures stay upstream; tool
  authorization, approval, timeout, output limits and audit remain downstream.
  Nothing calls a tool, uses a user database or invokes a paid provider.
- Changed source/test files each contain one type and remain below the 500-line
  file and 80-line method limits. No abstraction or mutable global state is added.
- Eleven new characterization cases pass against the original implementation
  before editing production. They cover snapshot identity/order, empty frames,
  signed text, mixed 128/129-call frames, late invalid parts, 512/513-character
  native identities and escaped strings that outlive JSON document disposal.
  An initial new-test compile error referenced `Name` instead of `ToolName`;
  correcting that assertion did not change product code or disable warnings.

Evidence, original source snapshots and synthetic checks remain outside Git:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-gemini-parser-optimization-20261011`.
This is an internal optimization slice, not whole-product or visual acceptance.

##### Measured allocation and focused verification

- The final Gemini filter passes 199/199 cases, including the eleven new cases
  and existing HTTP/SSE, cancellation, malformed/blocked/incomplete responses,
  call/argument/signature/identity budgets and continuation replay boundaries.
- The solution build has zero warnings and errors. Scoped whitespace formatting
  verification passes. Roslyn confirms one type in each changed C# file: mapper
  156 lines, parser 79 and new tests 156; the longest method is 30 lines.
- The subsequent complete solution run ends successfully: all eight assemblies
  pass 4,434/4,434 tests, with zero failures or skips. Counts are Agent 63, App 3292,
  Visual 8, Tools 316, Windows 137, Browser 76, Infrastructure 165 and Providers
  377. Raw TRX and the verified assembly counters remain under `regression`.
  The shared-component/Skia gates are included, not a new native UI comparison.
- A repository-external .NET 10 Release JIT harness links the two original source
  snapshots under a separate namespace and compares them with the live refactored
  types. Both use the same unchanged tool/continuation buffers. Each operation
  creates a parser, reads one synthetic frame, verifies STOP and drains completion.
  Tools/payloads are prepared outside measurement. Each scenario warms 2,000
  operations, then takes five 10,000-operation samples with per-operation event
  shape/count checks. All five allocation readings and checksums are stable.

| Synthetic parser operation | Before bytes/op | After bytes/op | Reduction |
| --- | ---: | ---: | ---: |
| Metadata frame | 992 | 960 | 3.2% |
| One text part | 1,632 | 1,504 | 7.8% |
| Four text parts | 2,472 | 2,424 | 1.9% |
| 128 interleaved text/tool parts | 259,760 | 224,576 | 13.5% |

These measurements concern this parser operation, not whole-application memory
or UI speed. Timing fluctuates and overlaps full regression work, so it is not
used to claim a latency improvement. This removes redundant processing rather
than claiming a net decrease in all source/test/documentation lines. The harness,
original copies, allocation CSV/logs and TRX stay outside the repository.
The final scoped tracked/untracked whitespace and diff checks pass; the index
remains empty and no QA host remains. All unrelated existing worktree changes
are preserved. No commit, push, AOT conversion or publish is performed. Real
remote provider/account behavior and whole-product fidelity remain unverified
by this optimization; the broader product goal remains active.

#### SSE line-reader follow-up: 2026-10-11

##### Design and boundaries

- Providers retains ownership through the existing SSE reader. No public contract,
  project dependency, tool execution path, database, UI or publishing change.
- Decoded chunks are scanned for CR/LF boundaries and appended as spans rather
  than character by character. Complete lines are copied directly into independently
  owned strings; unfinished segments use a lazily created, iterator-owned buffer.
- Strict UTF-8 decoding still happens before scanning each chunk. Only the initial
  BOM is removed. CR, LF, split CRLF, empty lines and incomplete final lines retain
  existing behavior. The 1 Mi-character line and 2 Mi-character event limits stay
  in force, including supplementary scalars counted as UTF-16 characters.
- Cancellation checks, immediate delivery, immutable returned strings and the
  caller's ownership of the input stream are unchanged. No shared mutable state
  or approval bypass is introduced; downstream tool safety is unaffected.
- Production code grows from 55 to 65 lines rather than adding a buffering
  abstraction. Its one method is 55 lines, below the 80-line hard limit. The new
  test file has 158 lines, one type and a longest method of 20 lines. This removes
  unnecessary per-character work and copies, not a net reduction in repository lines.

##### Verification and measurements

- Thirty new characterization cases pass against the original reader before the
  production edit, then pass with the optimized reader. They cover fragmented UTF-8,
  initial/interior BOMs, all line endings and buffer boundaries, exact/excessive
  character limits, immediate delivery, cancellation and malformed UTF-8.
- The complete Providers project passes 499/499. The focused App settings/runtime
  proxy chain passes 63/63, including authenticated HTTP/SOCKS5 and direct routing.
  The solution build has zero warnings and errors. Scoped whitespace formatting,
  one-type/file, method/file limits and trailing-whitespace checks pass. This slice
  does not rerun every unrelated test project or claim new native UI acceptance.
- An external .NET 10 Release JIT harness links the original snapshot beside the
  actual new source. With seed 1739, 250 generated valid UTF-8 inputs are compared
  at six chunk sizes: 1,500 comparisons have identical output and stream ownership.
- Each measurement consumes a new synthetic stream and checks every returned line.
  Both readers warm 2,000 operations per scenario, followed by five 1,000-operation
  samples. Allocation readings include the same fixture and async enumeration cost.

| Synthetic stream | Before bytes/op | After bytes/op | Reduction |
| --- | ---: | ---: | ---: |
| 64 short lines | 16,074 | 15,970 | 0.6% |
| One 3,006-character complete line | 35,018 | 18,938 | 45.9% |
| One 8,198-character fragmented line | 82,202 | 81,698 | 0.6% |

Allocation samples are stable apart from a 0.048-byte/op fluctuation in one
fragmented-line sample. Timings are retained but not used to promise an application
speedup. These are synthetic reader measurements, not whole-process memory or UI
latency. Short and fragmented lines do not receive the complete-line allocation gain.

Evidence, original source, the comparison/measurement harness and TRX stay outside
Git: `C:\Users\wzheng\AppData\Local\Temp\MossAgent-sse-reader-optimization-20261011`.
The earlier isolated native proxy host and server close normally with exit code 0;
their partial native acceptance remains separate and incomplete. No user settings,
paid API, screenshot/UI interaction, staging, commit, push, AOT or publish is used
for this optimization. JIT debugging and Windows x64 single-file publishing remain
unchanged; the broader product goal remains active.
<!-- /source: PROVIDER_PARSER_OPTIMIZATION_ACCEPTANCE.md -->


---

<a id="provider-proxy-acceptance"></a>

## PROVIDER_PROXY_ACCEPTANCE.md

<!-- source: PROVIDER_PROXY_ACCEPTANCE.md -->
### 提供商与代理联动验收

2026-10-10 的 SOCKS5 设置、实际发现/对话、保存恢复及三协议路由扩展见
[SOCKS5_PROVIDER_ACCEPTANCE.md](PROVIDERS.md#socks5-provider-acceptance)。下述各轮“未覆盖 SOCKS5”是对应历史范围，
不能作为当前匿名 SOCKS5/Responses 原生链路仍未验证的结论。

#### 2026-10-10 Gemini 生成能力设计复核

1. Gemini 发现解析归属 Providers，仍由 `IModelDiscoveryService` 提供结果；App 继续
   使用既有模型导入命令和配置仓储，不跨模块访问 HTTP 或 SQLite。
2. 模型列表明确声明生成能力时，仅发现支持 `generateContent` 或
   `streamGenerateContent` 的模型；嵌入、预测或仅计数模型不进入对话候选。
   缺少该可选元数据时保持兼容，不能仅凭模型名字推断能力；手动模型配置不改写。
3. 格式错误的显式能力元数据拒绝整个读取，错误不包含响应正文；每页 2 MiB、最多
   16 页、取消、代理路由、HTTP 请求头与现有导入所有权保持不变。
4. 将 Gemini 模型/分页解析抽为单类型文件，避免继续扩大现有发现服务；相关文件
   和方法遵守 500/80 行硬限，无数据库迁移或新执行入口。
5. 定向回归覆盖生成能力、明确不支持、无元数据、错误元数据、分页去重及其他协议
   不被 Gemini 规则影响；再以原生窗口贯通发现、导入、代理及直连请求。

##### 本轮实现与回归

- `ModelDiscoveryService` 移除内联 Gemini 模型和分页解析，交由独立解析器负责，
  服务由 244 行减至 200 行。保留原有排序、分页去重、请求头、代理与读取限制。
  明确声明能力时只接受生成模型，并检查能力数组的全部成员，不因第一个合法值
  而忽略后续非法值；没有按模型名称猜测能力或删除已有模型。
- 新增能力测试 15 项，修复前 11 项失败、4 项通过；模型发现专项修复后 30/30
  通过。真实 App/SQLite/回环代理与直连专项 12/12 通过，Gemini 路由夹具同时返回
  嵌入与生成模型，验证实际导入仅包含生成模型。
- 四个相关 C# 空白格式检查与单类型、文件/方法规模审计通过；最大文件 200 行，
  最长方法 34 行。完整解决方案构建零警告、零错误。
- 完整默认并行回归为 3,914/3,915 通过、1 失败、0 跳过。失败为
  `WorkspaceAgentCatalogFlowTests.AStaleMenuItem_UsesTheCurrentAgentConfigurationInTheActualRequest`，
  在夹具初始化 SQLite 时出现 `ObjectDisposedException: SQLitePCL.sqlite3`。
  随后独立复测该项 1/1 通过、所属测试类 4/4 通过；不代表全量回归已通过或根因
  已修复。本轮未修改该夹具、生产 SQLite、并行设置、期限或添加测试重试。

##### 普通权限原生链路与恢复

- 仓库外宿主运行实际 App、MainWindow 和共享组件，使用独立合成 SQLite。
  浅色 1280×820、96 DPI 下实际获取、导入两个提供商的模型，Gemini 返回嵌入和
  生成模型但只展示 `gemini-3-flash`；兼容接口展示 `gpt-5.4`。
- 通过真实 UI 先分别发送代理请求，再编辑两个提供商的 URL、明确选择直连、保存，
  重新获取模型并发送。四次回复分别为 `GEMINI_PROXY_OK`、`COMPATIBLE_PROXY_OK`、
  `COMPATIBLE_DIRECT_OK`、`GEMINI_DIRECT_OK`，POST 均携带对应模型与思考参数。
- 同一 SQLite 重开为深色 980×820、96 DPI，确认两个模型、直连 URL 与代理选择
  恢复，取消编辑并返回会话后四次历史回复仍在。默认代理保持启用，恢复期间
  没有新增请求或历史工具回放；安全日志恰好为两协议、两路由各一次 GET/POST，
  共八条，代理使用绝对请求目标，直连使用相对请求目标。
- 浅色与深色宿主均记录普通权限，并正常退出码 0；回环服务通过停止信号正常退出。
  Python 仅按 PID 被动截图，不缩放、移动窗口或注入输入。UIA 可能短暂返回旧树，
  每次操作后另行观察确认，没有根据旧树重复保存、输入或发送。
- 证据目录 `C:/Users/wzheng/AppData/Local/Temp/MossAgent-gemini-discovery-20261010/`：
  `capabilities-before.trx`、`capabilities-focused.trx`、`import-routing-focused.trx`、
  `full/` 七份 TRX、`recheck/` 独立复测、`solution-build.log`、`source-audit.log`、
  `format.log`、`server/requests-safe.jsonl`、`light/restored-metadata.json`，以及
  模型列表、四次浅色回复、深色编辑器与 `workspace-restored-dark.png`。

##### 本轮未覆盖范围

- 只访问本机匿名 HTTP 夹具，没有公网、付费请求、认证代理或 SOCKS5 原生验收。
  不读取真实用户配置，不操作密钥、审批或安全设置。截图、宿主和脚本不加入仓库。
- 本轮没有重构 UI 外观；明暗主题与窄窗口恢复不是全 DPI、全交互状态或 Codex
  像素一致性验收。整体产品目标保持进行中，未暂存、提交、推送或发布。

#### 2026-10-07 原生操作与多协议路由

- 不另建组件或网络实现。真实页面继续使用共享提供商行、设置弹窗、输入、下拉和
  工作区组件；通过现有设置命令、配置仓储、模型发现服务和提供商注册表贯通请求。
  本轮仅扩展 App 集成测试与验收文档，不改变端口、审批、授权策略或数据库结构。
  测试文件、协议夹具各自单类型，文件与方法规模审计通过。
- 普通权限浅色 1280×820 下，通过真实鼠标、键盘和原生可编辑控件创建 HTTP 默认代理，
  再创建匿名 Responses 提供商、获取并导入模型、返回工作区发送合成消息。
  环回夹具收到绝对形式的 `/v1/models` 和 `/v1/responses` 请求，工作区显示 `PROXY_OK`。
- 同一提供商在实际编辑弹窗中修改 URL，并明确选择“直连”；保留已启用默认代理。
  再次获取模型、发送消息只到达直连监听器，请求使用相对形式，工作区显示 `DIRECT_OK`。
  请求携带导入的模型身份与 `medium` 思考强度。四条路由日志分别是代理 GET/POST、
  直连 GET/POST，不存在本轮切换直连后仍走默认代理的额外请求。
- 退出并用同一隔离 SQLite 重启到深色 980×820，实际打开设置与提供商编辑弹窗，
  确认 URL、显式直连与模型仍在，主会话中的两次回复恢复。两个宿主正常退出码为 0，
  环回夹具正常停止；没有强制终止生产应用或读取真实用户配置。
- `ProviderProxyRuntimeTests` 扩展为 12 项：Responses、Chat Completions 兼容、Gemini
  各验证 HTTP 代理、修改代理端口、显式直连；另各验证已配置代理停用时，发现与
  流式请求均拒绝且代理、直连监听器都没有连接。使用真实 App 装配、SQLite、
  设置命令和环回 TCP，不依赖外部网络或付费服务。原有 2 项扩大为 12 项，净新增 10 项。
- 证据 `C:/Users/wzheng/AppData/Local/Temp/MossAgent-provider-routing-native-20261007-01/`：
  `fixture/requests-safe.jsonl`、`persistence-safe.json`、`routing-protocols.trx`、
  `provider-proxy-model-imported-light.png`、`workspace-proxy-response-light.png`、
  `provider-direct-editor-light.png`、`workspace-direct-response-light.png`、
  `workspace-restarted-dark.png` 和 `provider-restarted-dark.png`。
  元数据查询不读取密钥、凭据或请求头；夹具不记录消息体，也不转发外部请求。
  脚本、夹具、截图和临时宿主仍在仓库外。
- 最终七个测试项目串行 2,491 项全部通过，0 失败、0 跳过，其中 App 1,882 项；
  各项目独立 `MossAgent.*.Tests-final.trx`，不使用可能被覆盖的同名结果文件。
  完整解决方案构建 0 警告、0 错误，986 个 C#/AXAML 源文件规模、单类型和方法
  审计 0 违规；两个测试文件限定格式复核与相关空白检查通过。
- 原生可编辑控件的 UIA SetValue 曾报告超时；新观察确认地址已替换，没有重复输入。
  初次扩展测试误用了不存在的协议选择属性，修正为真实 `Protocol` 后 12 项通过；
  不把辅助工具的超时或测试编译修正当作产品故障或失败用例基线。

##### 本轮限制

- 原生请求只覆盖匿名 HTTP/Responses；兼容接口与 Gemini 在确定性集成测试中验证，
  不是所有协议、认证代理、SOCKS5 或公网环境的原生端到端验收。
- 深色窄窗口验证恢复、页面及编辑交互，没有重发网络请求或穷尽错误、焦点状态。
- Python 原图含有效窗口边界；当前 Codex 原图 1936×1048，与 MossAgent 尺寸、
  对话内容及检查器状态不同。没有拉伸截图，也不声称全页逐像素一致。
  `shared-navigation-original-crops.png` 只移除参考图的 8-pixel 非客户区边界并原样
  并排裁片，便于对比共享导航；品牌文字、内容及部分细节不同，不作为像素一致结论。
- 整体产品与视觉目标继续进行，未暂存、提交或推送。

#### 2026-10-05 当前实现

- 代理保存、删除和刷新后，通过主窗口订阅自动更新提供商的代理选择器。
  不调用提供商完整刷新，不覆盖名称、URL、协议、API Key 或请求头草稿。
- 用户明确选择的直连和代理身份保持不变。新建表单使用最新的已启用默认代理；
  已选择的代理被停用或删除后不自动变为直连或替换成其他代理。
  同一身份重新启用后恢复选择，刷新集合造成的下拉框临时空值不会丢失身份。
- 异步加载按版本拒绝旧结果和旧错误，保存前重新检查代理可用性和选择是否变化。
  读取失败保留草稿并阻止保存，只显示本地化安全错误，不展示底层异常或凭据。
- 代理仍被提供商、浏览器配置或 MCP 引用时，配置仓储在事务中拒绝删除。
  停用的提供商和 MCP 引用也受保护，避免恢复配置时意外使用直连。
  明确移除引用后仍可正常删除；不存在的代理删除为幂等操作。
- 主窗口释放时解除代理变化订阅。不新增运行服务、网络端点或数据库迁移，
  不改变任务审批、授权目录、超时、取消或输出限制。

#### 验证

- `ProviderProxySettingsFlowTests`：真实 App 服务装配、SQLite 和设置命令覆盖
  新增、改名、默认选择、停用、重新启用、删除、外部刷新、草稿保留及窗口释放。
  修复前八项失败，证实缺少自动同步和引用删除保护；修复后通过。
- `ProviderProxyReloadTests`：延迟和失败仓储快照覆盖乱序加载、错误脱敏、
  临时空选择、加载时修改选择、恢复缺失身份、保留非代理错误和保存时选择变化。
- `ProviderProxySelectionComponentTests`：使用实际共享 App 样式及独立 Headless
  UI 消息循环，在明暗主题下检查真实 AppComboBox 的双向绑定与恢复选择。
- `ProxyDeletionSafetyTests`：实际 SQLite 覆盖提供商、浏览器及启用、停用 MCP
  引用的拒绝路径，还有无引用、解除引用、重复删除的成功路径。
- `ProviderProxyRuntimeTests`：先创建主窗口订阅，再通过代理设置新增代理，
  不手动刷新提供商。使用真实回环 TCP 代理验证模型发现和 OpenAI Responses 流式
  请求的绝对请求行；修改代理端口后，新请求使用新端口，旧监听器没有收到请求。
  两项用例在取消手动刷新后先失败，修复后通过；不需要真实账户或付费模型。
- 相关 App 检查 32 项、基础设施检查 7 项通过。最终串行全量共 1,337 项通过，
  其中 App 978 项；完整解决方案构建零警告、零错误。
  本轮 C# 空白格式、范围内 diff 检查、单类型和文件、方法规模检查通过。

#### 普通权限真实窗口

- 仓库外验收宿主启动实际 App、MainWindow、共享样式和服务装配，仅使用独立合成数据。
  通过设置命令修改已选择代理的名称，验证提供商草稿仍在，代理下拉框显示更新后的名称。
  主窗口没有在代理保存后再执行提供商完整刷新。
- 两次进程均记录 `QA_ELEVATED: False`。Python 脚本仅被动截图，没有前台切换、
  移动窗口、鼠标输入或缩放图像；浅色最大化为 1920×1032，深色普通窗口为 980×700。
  下拉菜单内的直连、备用代理及更新后的选中代理均可见，截图没有黑屏。
- 截图及宿主位于 `C:/Users/wzheng/AppData/Local/Temp/MossAgent-provider-proxy-qa-qaga53fuo3r`：
  `provider-proxy-light.png` 和 `provider-proxy-normal-dark.png`。
  宿主、脚本和合成数据不加入仓库，不读取真实用户配置、不发出模型请求或回放历史工具。
  两次预览均已结束，最终确认对应存活进程为零；没有强制停止预览。

#### 2026-10-05 的范围限制

- 原生窗口通过验收宿主驱动设置命令和打开下拉框，没有新增原生鼠标、键盘交互验收。
- 真实回环测试覆盖 HTTP 代理和 OpenAI Responses；其他协议、认证代理、SOCKS5
  由已有针对性测试覆盖，不代表本轮做了所有协议的外部网络端到端检查。
- 截图证明本次代理同步、表单与下拉框呈现，不是 Codex 或 PI-Desktop 的逐像素一致性证明。
- 尚未穷尽全应用页面、DPI、焦点、错误状态和所有并发工作流；整体目标保持进行中。
- 没有暂存、提交或推送，保留其他工作区改动。

#### 2026-10-10 代理配置读取边界

- 提供商代理刷新及保存前验证复用目录的设置生命周期组件，每次操作独立，保持
  默认 30 秒。非协作异步端口不再让 UI 无限等待；同步端口返回已完成任务后，
  仍检查取消，不把已超时结果当成成功。
- 失败保留代理选择、名称、API Key 草稿和打开的编辑器，拒绝提供商写入；不会
  静默切换直连。页面释放取消在途读取，迟到返回不再发出错误消息或写入。
  已开始的写入仍由既有修改执行器持有，不因读取期限变化而宣称可以回滚。
- `ProviderProxyReadLifetimeTests` 六项覆盖刷新及保存两个入口的非协作、已取消
  完成和释放边界；重试可恢复并保存原代理身份。目录与公共生命周期另有四项
  回归；详见 [PROVIDER_CATALOG_ACCEPTANCE.md](PROVIDERS.md#provider-catalog-acceptance) 的同日记录及仓库外证据。
- 同日原生截图检查针对合成配置读取状态，不增加网络代理端到端验收范围。
  兼容接口、Gemini、认证代理和 SOCKS5 的原生外部网络检查仍未完成。
- 最终相关专项 249 项及一次完整并行回归 3,900 项全部通过，完整构建零警告、
  零错误。完整结果和格式/规模检查见 [PROVIDER_CATALOG_ACCEPTANCE.md](PROVIDERS.md#provider-catalog-acceptance) 的同日记录；
  单次通过不等于所有外部网络或压力计时边界已经穷尽。
<!-- /source: PROVIDER_PROXY_ACCEPTANCE.md -->


---

<a id="provider-row-component-acceptance"></a>

## PROVIDER_ROW_COMPONENT_ACCEPTANCE.md

<!-- source: PROVIDER_ROW_COMPONENT_ACCEPTANCE.md -->
### 提供商与模型行统一组件验收

#### 缺陷与修复

- 使用普通权限生产 App/MainWindow 与隔离合成配置复现：业务页的水平 StackPanel
  对长提供商名称、模型名称和模型 ID 无限测量，文字覆盖右侧编辑、删除与默认徽标。
  复现截图不是只根据源代码推断，原始最大化窗口为 1920×1032。
- 新增 `AppProviderSettingsHeader` 和 `AppModelSettingsRow`，页面绑定领域数据、
  数量、展开状态与既有命令。标题、ID、地址和参数在有限宽度 Grid 列内省略，
  名称和 ID 的完整值继续用于提示和可访问名称，数据本身不被截断。
- 提供商的默认/停用徽标和模型数量独立保留；操作区不被标签覆盖。
  编辑、删除的高度原有 1-DIP 中心差被回归复现，现由共享变体统一为 24 DIP。
  文字、图标、按钮对齐和行内边距由组件与共享样式维护，页面不自行设置外观。
- Provider/Model 或相应命令缺失时禁用操作；CanExecute 仍由绑定命令决定。
  替换数据时名称、参数、可访问名称与命令参数同步更新，不保留上一行的目标。

#### 约束自检

- 实现归 App.Controls/Styles，资源归 App.Resources；领域数据仍由 Domain 定义。
  页面复用现有设置命令与配置仓储端口，不给 Application 或 Agent 引入 UI 依赖。
- 新组件不读文件、SQL、不启动进程、不发送供应商请求，不改变数据库结构、
  授权、审批、超时、取消或输出限制。设置保存仍走原有 ViewModel/仓储链路。
- 每个手写 C# 文件只有一个类型，文件和方法不超过工程硬限制；没有新增全局可变状态。
- `ValidateSharedProviderSettingsRows` 和 MOSSUI002 拒绝内容、内边距、尺寸和内容对齐
  覆盖，包括属性元素和原生所有者限定；正常的数据、状态和命令继续允许。

#### 自动与构建验证

- 新增 27 项回归：明暗主题、320/640-DIP 长标签、徽标和数量保留、文字与按钮居中、
  完整模型 ID 提示、目标和命令替换、缺失目标/命令与 CanExecute 拒绝路径。
- Headless 真实 `ProviderAndModelSettingsPane` 中，通过按钮的 Space 事件展开第二个
  提供商、打开正确模型与提供商编辑器；检查其 Provider/Model 身份，不只直接调用命令。
  既有真实 SQLite 提供商分组/模型保存链路复测，确认没有改成占位按钮。
- 首次全量中的旧标记测试仍从业务页读取箭头，按新组件边界改为读取共享头；
  保留左右箭头几何、展开绑定和全部输入字段的可访问名称断言，不删除旧覆盖。
- 仓库外真实编译按 MOSSUI002 拒绝头的 Width 和模型行 Content；AXAML 门禁拒绝
  Control.Height 及 ContentControl.Content。合法共享行通过 C# 构建及八项通用 AXAML 门禁。
- 最终相关专项 56 项通过；完整解决方案 1,826 项通过，其中 App 1,337 项，
  解决方案构建 0 警告、0 错误。相关 C# 空白格式与差异空白检查通过；
  864 个 C#/AXAML 文件的文件大小、单类型和方法体积审计失败数均为零。

#### 运行截图与限制

- 宿主加载生产 App/MainWindow，以独立 SQLite 和合成数据运行；日志确认未提升权限。
  使用空合成 API Key 和 example.test 地址，不发送模型请求，不修改真实配置或项目。
- Python 仅被动获取原始像素，最终浅色最大化为 1920×1032，深色普通窗口为 980×700；
  长名称、长 ID、默认/停用、模型参数与右侧操作均保持可见，图片不是黑屏。
- 原始像素前后裁片不缩放，证明原本覆盖操作区的文字现已省略。
  同轮被动获取的 Codex 参考是主工作区，不是相同的提供商设置状态，不能据此声称
  Codex/PI-Desktop 全页逐像素一致；未操作 Codex UI。
- 原生编辑点击首先报告 coordinate input geometry is unavailable。重新绑定唯一窗口并
  请求真实截图后，一次恢复点击没有报错，但焦点仍在搜索框、模型编辑器未出现；
  后续 Tab 没有可验证的焦点结果。因此不把这些输入当作成功，不重复点击或删除配置。
  Headless 按钮和页面命令验证不等于物理输入验收，真实鼠标/键盘编辑仍待完成。
- 四个本轮宿主均正常关闭、退出码 0；观察失败时先核对原 PID 仍存活，没有因超时重启。
  只读核对最终明暗数据集：各有 2 个提供商、4 个模型、0 个任务，模型参数未改变。
  宿主、脚本、截图、日志和数据全部在仓库外：
  `C:\Users\wzheng\AppData\Local\Temp\MossAgent-provider-row-qa-20261006-01`。
  最终图片为 `native-final-light.png`、`native-final-dark.png` 和 `provider-row-before-after.png`。
- 本轮不证明全应用 DPI、悬停、焦点、所有设置字段或全部工作流与目标一致。
  未暂存、提交或推送；整个 MossAgent 目标保持未完成。
<!-- /source: PROVIDER_ROW_COMPONENT_ACCEPTANCE.md -->


---

<a id="provider-stream-completion-acceptance"></a>

## PROVIDER_STREAM_COMPLETION_ACCEPTANCE.md

<!-- source: PROVIDER_STREAM_COMPLETION_ACCEPTANCE.md -->
### Provider stream completion and queued tools

Status: ten hash-checked files have been merged into the original checkout with
zero baseline conflicts. Original-checkout integration verification passes; the
subsequent complete Debug regression is terminal with one allocation-test failure.
This is a failure-handling slice, not full provider or Queue acceptance.

#### Confirmed behavior and change

Responses and Anthropic previously ended their iterators silently after empty
HTTP 200 bodies or SSE EOF without a protocol completion signal. Agent therefore
could mark the task Completed, execute collected tool calls and advance Queue.
Gemini and compatible Chat already reject missing finish signals.

The adapters end Responses/Anthropic iteration on ModelCompleted or ModelFailed;
EOF without either becomes localized incomplete_response failure. Responses'
existing explicit [DONE] compatibility is retained. Stream failures pass through
the existing error sanitizer and Agent failure path. Agent already collects tool
calls until the model stream ends and discards them on failure; no new tool path
or retry is introduced. A manually continued queue sends its next uncommitted
message, without replaying an incomplete model tool call.

#### Design self-check

1. Providers owns protocol completion, using the existing IModelProvider events.
   Application/Agent/UI dependency direction and database schema stay unchanged.
2. Existing cancellation, SSE limits, error sanitization and HTTP lifecycle remain.
   The ordinary tool pipeline owns authorization, approval, output and audit.
3. No new filesystem or external service authorization is granted. Test files
   live only in generated temporary roots. FullAccess is tested explicitly to
   prove an unterminated call cannot execute even when its write would be allowed.
4. New types each have their own file; modified providers remain small. No visual
   control or production command policy is changed by this candidate.
5. Deterministic fixtures cover empty/partial streams, terminal events followed
   by late text, real App failure/pause/manual continuation, pending drafts,
   saved approval policy, denial, approval, cancellation and incomplete calls.

#### Evidence so far

The independent copy is artifacts/queue-runtime-workspace-20261019; evidence is
its artifacts/provider-terminal-boundaries directory.

- Original adapter checks: eight cases, four passed and four failed. The failures
  were Responses/Anthropic empty and partial responses.
- Forced-rebuild original App checks: all three new cases fail. Empty/partial
  responses produce a second HTTP request without manual continuation; the
  unterminated tool call actually creates its temporary file. Reports retain
  these direct failed assertions and an earlier Failed-versus-Completed check.
- The first original-App attempt reused a candidate DLL because the copied
  source retained an older timestamp. That three-pass report is invalid and
  explicitly excluded. Later comparisons force --no-incremental and record the
  provider binary hash. Candidate source restoration has zero hash differences.
- Earlier candidate checks passed 38 Queue/attachment cases. Two intervening
  failures were incorrect test expectations for ReadOnly audit codes/approval
  outcomes, repaired against the existing executor; no policy was relaxed.
- Forced-rebuild frozen candidate: 617 Provider and 300 Visual cases passed;
  App passed 37 and failed one assertion that read the queue during its transient
  refresh. The tool file stayed absent and the task was Failed. Its wait now
  requires the completed paused presentation, while still exposing old Completed
  behavior directly. The 2,399 source files and 1,041 selected Debug runtime files
  had identical paths/hashes before and after that run. Preserve its failed TRX.

The earlier original full regression is terminal: 8,070 passed and 15 failed out
of 8,085, with no skipped tests. Its 14 obsolete visual selectors and one
attachment-navigation timeout remain preserved. All 2,401 source/configuration
and 2,035 Debug runtime paths/hashes were unchanged across that run. The visual
fixture now selects composer-surface and supplies unavailable Queue state instead
of leaving bindings unset. All 22 compact layout/theme/scale/focus cases passed,
retaining original readable-width and geometry assertions; PNGs were inspected.
The attachment test passed eight cases in isolation and twelve two-case replays.
It now explicitly awaits draft readiness before sending; the historical timeout's
exact cause remains unconfirmed. No attachment-leak result was observed.

#### Acceptance limits and next steps

The final independent candidate passes complete Debug and Release solution builds
with zero warnings/errors. Debug and Release each pass 38 Queue/attachment cases;
Release Providers passes all 617 cases. The prior frozen Debug Provider/Visual
results remain applicable: their sources did not change. Ten integration files
and 1,071 selected Debug/Release runtime files have zero before/after hash/path
differences. Outside those ten paths, comparison against the original manifest
finds only the intentionally older Windows PID fixture and missing new PID test;
neither will be copied. Candidate-source-ready.json and the guarded merge script
are prepared in artifacts/provider-stream-boundaries-20261019.

A final history-retention assertion also passes in both 38-case App selections:
partial received assistant text remains persisted after failure, while an empty
response does not invent that text. Both solution builds pass again. This latest
retention checkpoint has unchanged hashes for its ten integration files and 914
App Debug/Release runtime files; retention-terminal.json is its exact record.

The guarded merge completed against integration-baseline.json; its ten paths and
hashes are recorded in artifacts/provider-stream-boundaries-20261019/merged-files.json.
Original-checkout Debug/Release solution builds pass with zero warnings/errors;
Debug/Release each pass App 38 and Providers 617; complete Debug Visual passes
300. The rebuilt CLI harness also passes all four process-recovery cases against
the original checkout's loaded assemblies. The validation script is terminal
with all nine stages successful; see validation-terminal.json in that directory.
Structural checks cover 563 changed/untracked C# files, 29 AXAML files and 3,093
methods/accessors with no violations (maximum method length 78); whitespace passes.
These new results do not change the earlier full regression's failures. Preserve
every failed and invalid report. Current evidence is synthetic
HTTP, temporary SQLite/files and Avalonia Headless, not a paid provider, native
window interaction or OS process restart. Browser/process ownership through Queue,
dispatch-time authorization races, Steer and native/live acceptance remain open.
Windows command filesystem isolation remains an unresolved product safety blocker.

The subsequent complete Debug regression (session 97039) is terminal: 8,101 pass,
one fails, no skips, 8,102 total. All eight reports have been checked. The only
failure is the raw-counter WorkspaceTaskLookupOptimizationTests case (3,480 bytes);
its exact cause remains unconfirmed. The previous 14 visual failures and attachment
navigation timeout did not recur. All 2,404 source/configuration and 2,035 Debug
runtime paths/hashes were unchanged; its freeze has ended. Records remain under
artifacts/provider-stream-boundaries-20261019, including regression-summary.json.
The independent dispatch/allocation candidate is tracked separately in
CONVERSATION_FOLLOWUP_DISPATCH_ACCEPTANCE.md and is not part of this frozen run.

[CONVERSATION_FOLLOWUP_PROCESS_RECOVERY.md](CONVERSATIONS.md#conversation-followup-process-recovery) adds four identity-checked CLI process
termination/recovery cases against this same independent candidate, covering
project/projectless sessions before and after Queue dispatch. They pass without
automatic replay and are explicitly separate from native GUI restart acceptance.
<!-- /source: PROVIDER_STREAM_COMPLETION_ACCEPTANCE.md -->


---

<a id="provider-stream-limit-acceptance"></a>

## PROVIDER_STREAM_LIMIT_ACCEPTANCE.md

<!-- source: PROVIDER_STREAM_LIMIT_ACCEPTANCE.md -->
### 供应商流式输入限长验收

#### 2026-10-06 缺口与实现

原有 SSE ReadLineAsync 会先分配整行，再由工具调用累积器检查参数；跨行事件
也可持续增长。四种协议的非成功 HTTP 正文还会使用 ReadAsStringAsync 全量分配。
工具参数和最终错误文字有上限，不能证明上述原始输入已有内存边界。

- Providers 的 SseLineReader 用固定 4096-byte 输入缓冲和 UTF-8 解码状态增量处理，
  不先读取整行，不把 StreamReader 的填满缓冲行为引入流式首帧延迟。
  每个物理行在追加前限制 1,048,576 个 UTF-16 代码单元，忽略字段与注释同样受限。
- SseDataReader 的单个 data 事件限制 2,097,152 个代码单元，包含多行分隔换行。
  空 data 行也计数，不能通过很多短行绕过。每个新事件恢复独立预算。
- 保留 CR、LF、CRLF、首个 UTF-8 BOM、跨字节块的中文/emoji，以及原有 EOF
  刷新最后事件行为。data 字段只移除一个可选 ASCII 空格，不丢失内容本身的缩进。
- ProviderSseReader 将限长及无效 UTF-8 转为唯一、固定本地化 ModelFailed；
  不回显帧内容，不伪装成 ModelCompleted。取消继续传播，不变成提供商错误。
- ProviderHttpErrorReader 将非成功正文限制为 65,536 字节；已知过大 Content-Length
  不读取正文，未知长度最多读取上限加一个探测字节。过大或无效 UTF-8 正文用固定
  本地化错误替代，不显示部分原文；合法错误仍经过已有凭据脱敏与文字长度限制。
- OpenAI Responses、OpenAI Compatible、Anthropic、Gemini 共用上述边界。
  提供商仍拥有响应/流释放；基础 SSE 读取不接管调用方的 Stream。

#### 约束自检

- 所有行为归 Providers，继续通过 IModelProvider 输出既有模型事件；
  Application/Agent 不引用具体协议、平台或 UI，不新增静态服务定位器。
- 没有新的工具或运行时端口；不修改授权目录、审批、超时、任务取消、工具输出限制
  或持久化结构。兼容接口的工具批次在模型失败后不能进入执行管线。
- 每个新增 C# 文件一个类型；文件、方法保持在工程硬限制内。
  同时拆分原有两处 86 行测试：提供商分组/代理测试独立成 ModelProviderGroupFlowTests，
  子代理请求路由和工具展示断言按职责提取，不删除覆盖或更改生产子代理行为。
- 安全边界测试包含正常边界值、拒绝、编码、取消、提前释放及实际工具不执行路径。

#### 自动验证

- 新增 38 项 Providers 回归；相关读取/限长专项 39 项通过，提供商完整项目 155 项通过。
- 单字节碎片中文/emoji、UTF-8 BOM、三种换行、准确行/事件/HTTP 上限、空 data 行计数、
  超长注释/未知字段、很多短行、异常/截断 UTF-8、未结束连接上的即时事件和挂起读取取消。
- 四种提供商均验证单行限长、事件限长、编码失败及 HTTP 正文长度；检查早停读取的
  实际字节计数和响应释放，不只检查最终字符串已被截短。异常帧和 HTTP 正文不回显合成密钥。
- App 新增两种完整链路场景：收到工具结束标记后再出现超长行/事件。FullAccess 下
  验证 Failed、部分助手消息只保存一次、文件未变、工具和审计为零；重建服务后消息 ID
  不变，原 loopback 服务仍运行且请求计数未增加。该专项 6 项通过。
- 测试职责拆分专项 12 项通过，完整解决方案 1,684 项通过；
  最终解决方案构建 0 警告、0 错误，相关 C# 空白格式及差异空白检查通过。
- 全仓库 837 个 C#/AXAML 文件审计：文件大小、单类型和方法大小失败数均为零。

#### 原生运行证据

- 仓库外普通权限宿主加载生产 App/MainWindow，使用独立临时数据和 loopback
  合成服务，不调用真实供应商或付费接口，不修改真实项目、配置或凭据。
- 实际输入并发送超长行场景，观察到已收到的部分回复和任务失败提示；
  没有工具活动行或停止按钮，输入框清空，发送处于禁用状态。
- 只读检查临时 SQLite：任务 Failed，用户/部分助手各一条，工具审计为零；
  合成文件保持“兼容接口文件证据：中文与 Unicode 🔎。”，模型服务日志请求 1 次。
- 同一数据集以深色 --reopen 恢复失败会话，消息和审计未增加，失败与原部分回复
  仍显示；新建临时服务请求计数为 0。原生恢复截图单独证明界面/持久化状态，
  无自动模型重放的网络证据来自上述保持原服务运行的自动端到端测试。
- 两次原生宿主正常关闭，日志均无失败标记、退出码 0。
  Python 只被动读取原始 1280×820 窗口像素；脚本、截图、日志、宿主和数据均在仓库外：
  `C:\Users\wzheng\AppData\Local\Temp\MossAgent-provider-stream-limits-20261006`。
  图片为 native-line-failed.png 和 native-line-reopened-dark.png。

#### 剩余范围

- 这证明原始 SSE 行、data 事件和错误正文的读取边界，不等于整个任务累计正文、
  HTTP 头、所有 SDK 内部缓冲或全局内存都受同一上限。
- 未新增真实付费供应商、大型合法工具结果或所有代理组合的原生验收；
  本轮不改变 UI 样式，也不证明所有页面、DPI 或 Codex 像素一致。
- 本记录的原生截图显示通用安全提示。后续安全失败类别持久化与共享失败提示组件
  已补齐，最新运行截图显示具体流式单行限长原因，见 [TASK_FAILURE_ACCEPTANCE.md](CONVERSATIONS.md#task-failure-acceptance)。
  原始供应商异常仍不能回显；这不扩展本记录中的 SSE 读取边界结论。
- 未暂存、提交或推送，保留既有及未提交工作。

#### 2026-10-10 响应入口精简

##### 设计与边界复核

1. 本轮仅由 Providers 拥有响应读取，继续使用 `IModelProvider`、既有 SSE 帧及
   `ProviderHttpErrorReader`；不向 Application、Agent 或 UI 引入具体实现依赖。
2. 四种适配器重复的 HTTP 状态检查、限长错误读取和成功流释放收敛到
   `ProviderSseReader` 的响应入口。协议请求、认证头、解析器、终止及补全语义不变。
   非成功状态只产生一个安全失败帧，不调用协议解析器，也不产生成功结束事件。
3. 响应、HTTP 客户端和请求仍归适配器；新入口拥有成功响应流，取消和提前结束均
   释放。原始 Stream 入口保持非拥有语义，不关闭调用方的流。取消继续原样传播。
   HTTP/SSE 上限、脱敏、代理、审批、任务隔离与持久化均不调整。
4. 内部 `SseDataFrame` 改为不可变值类型，避免每个事件再分配一个包装对象；
   不改变帧内容或失败类型，不新增全局可变状态。维持单类型、文件和方法规模限制。
5. 成功流、错误流取消、提前结束及错误正文不能进入 SSE 解析器均对四种协议验证；
   复用现有确定性响应夹具，无公网、真实凭据或付费请求。

##### 自动验证

- 现有限长/编码、错误脱敏和响应释放专项 57/57 通过。
- 净增 15 项协议回归；Providers 全项目 332/332、完整解决方案 4005/4005 通过，
  0 失败、0 跳过。App 中现有 HTTP/SOCKS5 设置到模型发现/流式场景一并通过。
- 完整解决方案构建 0 警告、0 错误；七个 C# 文件定向 whitespace 格式检查通过。
  Roslyn 结构审计通过：每文件一个类型，最大文件 227 行，最大方法跨度 46 行。
- 本轮生产文件净减少 14 行；不以新增测试行数抵消测试覆盖，不声称测得吞吐提升。
- 本轮不修改 UI；没有新增原生截图或视觉一致性结论。前两轮全量并行超时仍保留在
  [SOCKS5_PROVIDER_ACCEPTANCE.md](PROVIDERS.md#socks5-provider-acceptance)，本次通过不能证明其偶发失败根因已经修复。
- 没有修改已有测试期限、并行设置或加入重试；不将独立专项通过描述成全量通过。
- TRX、构建、格式和结构日志保存在仓库外：
  `C:/Users/wzheng/AppData/Local/Temp/MossAgent-provider-response-refactor-20261010`。
  没有暂存、提交、推送或发布；JIT 调试和 Windows x64 单文件发布设置不变。
<!-- /source: PROVIDER_STREAM_LIMIT_ACCEPTANCE.md -->


---

<a id="provider-ui-workflow-acceptance"></a>

## PROVIDER_UI_WORKFLOW_ACCEPTANCE.md

<!-- source: PROVIDER_UI_WORKFLOW_ACCEPTANCE.md -->
### Provider settings-to-runtime UI workflow acceptance

#### Scope and design self-check (2026-10-16)

The preceding complete regression/entry audit made verified progress. This
follow-up checks business behavior beyond wiring, without treating the earlier
4,038 button occurrences as successful executions of every command.

1. App.Tests owns this deterministic acceptance coverage. No production module,
   dependency direction, configuration schema or UI appearance is changed.
2. Production MainWindow, composition, configuration/workspace ports, SQLite,
   provider discovery, streaming adapters and existing commands are reused.
   The loopback server supplies protocol responses, not a substitute agent runner.
3. Each case owns synthetic storage, a loopback listener and a 60-second linked
   cancellation deadline. The shared HTTP fixture reader limits headers to 16 KiB
   and bodies to 1 MiB. Request headers/credentials are not recorded; recorded
   paths omit queries. Plain chat grants no project roots or tool capabilities.
4. Four new handwritten C# files each have one type: 104/128/82/188 physical lines,
   maximum method sizes 36/15/28/67 respectively. Existing controls/styles are used
   without adding per-page appearance or new native controls.
5. Six connected cases verify success plus HTTP discovery failure, invalid model
   save, disabled-provider submission, cross-provider isolation and real response
   rendering. No user data, approval preference or existing window is accessed.

#### Connected workflow

Each protocol runs once in actual Light at 1280x820 and once in actual Dark at
526x601: Responses, OpenAI-compatible Chat and Gemini GenerateContent. Each case
asserts the window's effective theme, not just its requested theme.

- The visible add-provider action opens the real dialog. Name, loopback URL and
  synthetic key are entered into bound inputs; saving uses Space on the actual
  footer button. Persisted protocol and explicit direct routing are verified.
- Expanding the correct provider reveals its real discovery action. Its first
  HTTP request returns 503 with a synthetic private string. The shared page
  header shows the sanitized error and no model is persisted.
- Space retries the same action. Production protocol discovery imports exactly
  one generative model; Gemini's embedding-only entry is excluded. The page
  leaves its error state, and imported image/file flags match the bundled catalog
  rather than assuming every known model supports every capability.
- The real model-row action opens its editor. Saving an empty ID preserves the
  stored model, keeps the dialog open and shows the editor error. Correcting it
  saves the same identity with 64,000 context, 2,048 maximum output, high reasoning,
  temperature 0.3 and Top-P 0.8. Image/file overrides become false/true.
- Reopening restores the edited name; real Escape dismisses the retained editor.
  Fetching again preserves the complete customized model instead of replacing it
  with catalog defaults or creating a duplicate.
- The real provider editor's enabled checkbox and save action disable the only
  available provider. After returning to chat, typed text cannot enable Send;
  Enter creates neither a task nor a streaming HTTP request.
- Re-enabling the provider restores the saved selected model/high effort. Enter
  sends exactly one plain-chat request through its actual protocol adapter.
  Recorded requests verify output limit, reasoning and sampling parameters;
  Gemini's model identity is checked in the request path.
- The task and exact user message persist as completed with a null project ID.
  The returned `hello` is rendered by MarkdownSelectableTextBlock and hit-tested
  on-screen, not merely found in storage or a hidden visual tree.
- Three discovery paths and one expected streaming path are checked. An unrelated
  disabled provider and its model remain unchanged. No functions are advertised:
  Gemini's empty function-declaration wrapper is checked semantically rather than
  mistaking its nonempty outer array for an available tool.

Text/reasoning entry, checkbox/action activation, Enter and Escape use Headless
keyboard input. Protocol selection and numeric edits use bound control properties;
settings navigation uses existing view-model commands. This is not a claim that
every field or navigation transition receives operating-system keyboard input.

#### Validation and evidence

Evidence remains outside the repository at
`%TEMP%/MossAgent-provider-ui-workflow-20261016`.

- `workflow-visible.trx`: six connected cases pass. `related.trx`: all 217 related
  App cases pass, including those six. `architecture.trx`: all seven architecture
  cases pass. The final distinct total is 224, not 230; no failures/skips/aborts.
- `related-filter.txt` records the bounded regression scope. `solution-build.log`
  records zero warnings/errors; `format.log` comes from scoped whitespace
  verification without rewriting source.
- `hashes-before-final-checks.csv` and `hashes-after-final-checks.csv` match all six
  recorded tested/runtime DLL identities. Production and both tested App copies
  match the unchanged App binary from the earlier 6,495-case complete checkpoint.
- `source-size-audit.json` records Roslyn declaration/method-span checks. Its small
  external `source-audit/` project references SDK Roslyn, not production projects.
- Earlier workflow TRX/log files retain test-development errors: an overbroad
  assumption that GPT-5 supports files, nested control namescope lookup, Gemini's
  empty declaration wrapper, and using TextBlock.Text instead of the Markdown
  renderer. They are not confirmed production defects or product red baselines.
  The initial compile log also retains a wrong test-only provider property name.

#### Remaining gates

No production change is justified by these results. The full eight-suite run and
pixel suite are not repeated after adding these six tests; the earlier complete
6,495 result is not enlarged arithmetically. Persisting context length does not
independently prove context-budget truncation, and capability overrides do not
exercise actual image/file requests. These cases do not cover every model, proxy,
paid API, concurrent configuration mutation, native DPI or arbitrary user data.

Native screenshots/existing-window operations remain user-postponed. Current
Codex/PI-Desktop visual comparison and whole-product interaction/visual acceptance
remain open. No JIT/JSON/Windows x64 packaging, user settings, staging, commit,
push or release changes are made; the full product goal remains active.
<!-- /source: PROVIDER_UI_WORKFLOW_ACCEPTANCE.md -->


---

<a id="proxy-editor-mutation-acceptance"></a>

## PROXY_EDITOR_MUTATION_ACCEPTANCE.md

<!-- source: PROXY_EDITOR_MUTATION_ACCEPTANCE.md -->
### 网络代理编辑器修改验收

#### 2026-10-07 设计复核

1. 编辑器状态归 App ViewModel，保存仍通过 Application 的 `IConfigurationRepository`，
   连接测试仍通过既有 `IProxyConnectionTester`，不在 UI 直接读写 SQLite 或发 HTTP。
2. 复用 `SettingsEditorViewModel`、`SettingsMutationRunner`、`GuardedAsyncRelayCommand`，
   不增设平行的设置执行框架或新持久化端口。
3. 命令/生命周期取消和默认 30 秒协作截止传至存储。忽略取消的写入返回前继续持有
   修改权，不假称已经撤销或回滚。状态只展示本地化安全类别，不暴露凭据或原始异常。
4. 每个类一个文件，ProxySettingsViewModel 379 行，无嵌套 DTO 或超限方法。
5. 21 项新增回归覆盖异常、重入、草稿更新、刷新、目标移除、通知失败、关闭与取消，
   邻接测试证明提供商目录和窗口释放；原生输入另核对保存/重启而非仅调用 ViewModel。

#### 修复及回归

- 真实普通权限弹窗可显示验证消息，旧消息为弱文字色；并非没有显示。
  本次改为已有 `status-error` 变体，不给页面新增颜色、尺寸或阴影。
- 写入捕获稳定身份、不可变快照和编辑修订号；旧保存成功不能清空较新的字段、
  重开的草稿或另一个目标。刷新只更新规范目录记录，保留脏字段。
- 存储异常成为安全状态，命令不向外传播异常；失败读取阻止写入，已移除的编辑目标
  不被重新创建。保存/删除修改权覆盖读取、存储、目录重载和通知，直接调用也不能重入。
- 提交后的通知失败可通过刷新重试，不再次写入；窗口释放同时释放代理编辑器、取消
  操作、解除订阅并拒绝迟到结果。原释放测试改为验证死编辑器不能写入，而非允许写入。
- `proxy-mutations-before.trx`：10 失败、0 通过，确实复现异常/草稿/竞争/取消问题。
  `proxy-focused-final.trx`：74 项通过。新增回归 21 项，最终共享组件专项还有交叉覆盖。

#### 原生输入与存储证据

- 浅色 1280×820，实际打开设置/代理/编辑器，空名称保存显示共享错误状态。
  系统键盘输入“QA HTTP 默认”，点击默认并保存；再输入“QA SOCKS5 第二个”，
  点击新共享箭头将端口增到 7891，选择 Socks5 并保存。
- 同一隔离数据库、普通权限重启为深色 980×820，实际列表恢复两项，
  重新编辑 SOCKS5 显示保存的协议和端口；减小后取消仍保留原数据。
- 只读 SQLite 仅导出 id/name/protocol/host/port/is_default/is_enabled：
  HTTP 127.0.0.1:7890 为默认，SOCKS5 127.0.0.1:7891 非默认，两者启用。
  未查询或导出用户名/密码/API Key。`proxy-safe-metadata.json` 为白名单元数据。
- 临时生产宿主预置了项目和完成会话，不代表原生创建任务的验收；代理由上述真实输入创建。
  两个本轮完成的宿主正常关闭，`QA_ELEVATED: False`、`QA_EXIT_COMPLETED: 0`。
  较早 `after-light` 宿主的八分钟截止产生 TaskCanceledException，未继续复用其窗口。
- 截图、TRX、构建、格式和审计在 `%TEMP%/MossAgent-ui-fidelity-qa-20261007-02/`。
  Python 被动截图只保存主窗口，不涵盖独立 popup；协议菜单的操作另有原生观察。
  首次临时 SQL 检查发生 Shell 引号错误，之后改用仓库外只读脚本并获得真实元数据。

#### 验收限制

- 没有原生删除操作、真实代理连接、付费提供商请求或本次提供商绑定代理的实体输入验收。
  拒绝、失败、超时和非协作行为由定向测试覆盖，不伪称为系统故障注入。
- 封装只保证共享样式归属，当前 Codex 截图与 MossAgent 内容和尺寸不同，
  不外推为全部界面/DPI/主工作流已经一比一还原。完整产品目标仍进行中。
<!-- /source: PROXY_EDITOR_MUTATION_ACCEPTANCE.md -->


---

<a id="proxy-editor-selection-acceptance"></a>

## PROXY_EDITOR_SELECTION_ACCEPTANCE.md

<!-- source: PROXY_EDITOR_SELECTION_ACCEPTANCE.md -->
### Proxy editor selection acceptance

#### Design self-check

- App owns the editor target and list publication. Saving through the unchanged
  Application configuration port must never publish a record before it belongs to
  the visible catalog: the real two-way ListBox rejects out-of-catalog selections.
- The existing save/read/notification sequence, operation deadline, cancellation,
  serialized mutation ownership and draft revision guards remain authoritative.
- No proxy transport, authentication, credential storage, tool approval or schema
  changes. Test fixtures use only synthetic credentials, with boolean assertions
  rather than printing secrets. Native diagnostics read only the isolated QA data.
- Keep the fix in the existing small save method. Every handwritten C# file has
  one type, and all new methods/files remain within repository size limits.
- Reuse one ID-based catalog lookup for editor availability, reload, save/delete
  validation and row commands. Null IDs return immediately; indexed reads avoid
  closure and enumerator allocations without adding a cache or mutable index.
- Regressions exercise the actual shared settings page in both themes, create and
  edit flows, and a draft changed while storage is pending. Run existing proxy,
  editor lifetime/notification and authenticated routing tests plus a solution build;
  then repeat native failure/recovery with the real settings and conversation.

#### Native finding

Changing the synthetic HTTP proxy password saves it, but the form stays open without
success guidance and its Delete action becomes disabled. A read-only check of the
isolated QA database confirms the changed password using a boolean only. The native
host and server are closed normally before modifying or building any App files.
The save path publishes its request record while the ListBox still owns the older
catalog. List selection feedback can clear the editor identity and invalidate the
revision before the post-save read publishes the updated list.

#### Verification

- The real-page baseline fails all 5 cases: light/dark create/edit flows and a
  newer draft entered during a pending write. After the ordering fix, all 4
  normal saves close their dialogs. The first broader replay passes 47/48; its
  remaining failure is a test assertion expecting a word absent from the actual
  localized previous-draft notice. The assertion now uses that resource directly.
- Save reloads the catalog before publishing its actual saved member. ID and
  revision guards still preserve newer/reopened drafts, await notifications and
  reject removed targets. No binding mode or persistence contract is changed.
- The lookup regression measures 7,840,000 allocated bytes for 10,000 warmed
  checks before consolidation, and zero afterward. It also verifies stale row
  records resolve by ID, disabled/missing targets cannot be tested and checks do
  not modify another draft. The final proxy/settings selection passes 150/150.
- The complete App suite passes 3,600/3,600 in 6 minutes 52 seconds. Its 60-second
  per-test watchdog does not fire and no memory dump is generated. The solution
  build has 0 warnings and 0 errors. Whitespace checks pass; Roslyn audit confirms
  one type per touched C# file, at most 384 lines and at most 47 lines per method.
- Evidence is `C:\Users\wzheng\AppData\Local\Temp\MossAgent-native-proxy-recovery-20261011`:
  failing `tests/editor-before.trx`, `tests/editor-after.trx` and
  `tests/lookup-before.trx` are retained alongside `tests/proxy-final.trx`,
  `full-suite/app-full.trx`, `build.log` and `source-audit.json`.

#### Native replay

- The isolated host's App DLL hash matches the solution build. A host-only compile
  first rejects access to an internal localized resource; the external observer
  is corrected without changing production visibility. Both host processes run
  without elevation and use only synthetic SQLite data and loopback endpoints.
- With the saved wrong HTTP password, Gemini discovery reports actionable HTTP
  407 guidance. A unique conversation send finishes Failed with the same guidance.
  Both requests are rejected after authentication retry; direct requests stay at
  zero, no authenticated request succeeds, and the 8 existing replies are unchanged.
- Restoring the synthetic password through the real editor closes its dialog and
  shows the update notice. The observer confirms no catalog-external selection,
  no proxy error and no credential-leak flags.
- Without restarting App or provider, discovery synchronizes one model and the
  same conversation completes successfully. The reply count becomes 9; the only
  authenticated requests are model discovery and the Gemini streaming endpoint.
  Direct requests remain zero. The first unauthenticated 407 challenge on each
  successful operation is expected and not misclassified as a bad password.
- Passive Python captures are `wrong-conversation.png`, `restored-save.png`,
  `restored-discovery.png` and `restored-conversation.png`; each stage also retains
  safe JSON observations. UI and server close normally with exit 0 and empty stderr.
  The Python script and the QA host remain outside the repository.

Native coverage here is Gemini over HTTP, light theme, scale 1 at 1920 by 1032.
Missing credentials, other protocols and SOCKS5 failure/recovery retain their
deterministic coverage, not new native coverage. No paid endpoint, real user
configuration, AOT/JSON migration, commit or release is involved. The wider product
and Codex visual-fidelity goal remains active.
<!-- /source: PROXY_EDITOR_SELECTION_ACCEPTANCE.md -->


---

<a id="socks5-provider-acceptance"></a>

## SOCKS5_PROVIDER_ACCEPTANCE.md

<!-- source: SOCKS5_PROVIDER_ACCEPTANCE.md -->
### SOCKS5 提供商真实路由验收

#### 2026-10-10 设计复核

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

#### 实现与针对性结果

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

#### 实际设置到对话

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

##### 宿主收尾的失败与修复

首次浅色产品窗口关闭并完成 App 生命周期，日志为 `QA_EXIT=0`，但仓库外 SOCKS5
验收服务的 async disposal 捕获 Avalonia 同步上下文后被主线程同步等待，宿主未退出。
在确认产品窗口已关闭、配置和回复保存之后，仅终止精确匹配 PID/可执行路径的本轮
验收宿主，最终执行结果 -1；不把整个首次宿主描述为正常成功退出。

只修仓库外宿主的两处收尾 await 为 `ConfigureAwait(false)`，没有改生产 App。
最终深色恢复进程记录 `QA_SERVER_DISPOSE_BEGIN=AvaloniaSynchronizationContext`、
`QA_SERVER_DISPOSE_END=True` 和执行退出码 0，确认服务和窗口均正常收尾、没有残留。
就绪日志早于原生尺寸稳定，宽度可能还是 1280；深色尺寸以实际 980×820 原图为准。

#### 完整回归复核

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
详见 [PROVIDER_STREAM_LIMIT_ACCEPTANCE.md](PROVIDERS.md#provider-stream-limit-acceptance) 的 2026-10-10 记录及其独立证据目录。
这次通过不删除此前两轮失败证据，也不将响应重构描述为修复 Windows/SQLite 超时。

#### 仓库外证据与限制

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
<!-- /source: SOCKS5_PROVIDER_ACCEPTANCE.md -->
