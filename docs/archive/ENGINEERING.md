# 历史架构、性能与工程验证

> 历史归档：保留原报告当时的结论、实验、失败与限制。整理于 2026-10-21。
> 当前能力与未完成方案统一维护在[功能清单](../FEATURE_STATUS_AND_IMPLEMENTATION_PLAN.md)；
> 原文中的计划、主机状态、测试数字及“正在进行”等措辞均为历史检查点。
> 代码块和仓库相对产物路径保留原意；本机忽略的 `artifacts/` 不随克隆分发。

[文档入口](../README.md)

## 原报告索引

- [MossAgent Architecture](#architecture) — `ARCHITECTURE.md`
- [Agent tool catalog optimization acceptance](#agent-tool-catalog-optimization-acceptance) — `AGENT_TOOL_CATALOG_OPTIMIZATION_ACCEPTANCE.md`
- [Allocation measurement isolation](#allocation-measurement-acceptance) — `ALLOCATION_MEASUREMENT_ACCEPTANCE.md`
- [App regression recovery](#app-regression-recovery-acceptance) — `APP_REGRESSION_RECOVERY_ACCEPTANCE.md`
- [App catalog and collection optimization](#catalog-collection-optimization-acceptance) — `CATALOG_COLLECTION_OPTIMIZATION_ACCEPTANCE.md`
- [配置写入、重试与草稿保护验收](#configuration-mutation-acceptance) — `CONFIGURATION_MUTATION_ACCEPTANCE.md`
- [Context building optimization acceptance](#context-building-optimization-acceptance) — `CONTEXT_BUILDING_OPTIMIZATION_ACCEPTANCE.md`
- [Page focus selection optimization acceptance](#page-focus-optimization-acceptance) — `PAGE_FOCUS_OPTIMIZATION_ACCEPTANCE.md`
- [Search refresh optimization](#search-refresh-optimization-acceptance) — `SEARCH_REFRESH_OPTIMIZATION_ACCEPTANCE.md`
- [Sidebar refresh optimization](#sidebar-refresh-optimization-acceptance) — `SIDEBAR_REFRESH_OPTIMIZATION_ACCEPTANCE.md`
- [SQLite 测试连接池隔离验收](#sqlite-test-pool-isolation-acceptance) — `SQLITE_TEST_POOL_ISOLATION_ACCEPTANCE.md`
- [UI hot-path optimization acceptance](#ui-hotpath-optimization-acceptance) — `UI_HOTPATH_OPTIMIZATION_ACCEPTANCE.md`
- [Workspace lookup optimization](#workspace-lookup-optimization-acceptance) — `WORKSPACE_LOOKUP_OPTIMIZATION_ACCEPTANCE.md`

---

<a id="architecture"></a>

## ARCHITECTURE.md

<!-- source: ARCHITECTURE.md -->
### MossAgent Architecture

`AGENTS.md` is the binding engineering policy. This document is the concise map
of the intended system and should be kept current as modules are implemented.

[FUNCTIONAL_PARITY_MATRIX.md](OVERVIEW.md#functional-parity-matrix) tracks the current functional objective, inventory
and next steps. [PRODUCT_ACCEPTANCE_MATRIX.md](OVERVIEW.md#product-acceptance-matrix) retains the detailed historical
evidence. [INTEGRATED_WORKFLOW_ACCEPTANCE.md](OVERVIEW.md#integrated-workflow-acceptance) records native production-App
composition checks; passing an individual slice does not establish full-product
completion. Pixel equivalence with Codex is no longer a completion requirement.

#### Runtime flow

```text
Avalonia UI
  -> application use case
  -> task-scoped agent runner
  -> model provider stream
  -> tool execution pipeline
  -> schema, capability and path validation, policy approval, timeout, and audit
  -> built-in, browser, Git, shell, or MCP tool
  -> bounded result and file-backed artifact metadata
  -> audit completion
  -> persisted task event
  -> next model turn and UI update
```

#### Module map

| Module | Owns |
| --- | --- |
| Domain | Entities, values, enums, domain validation |
| Application | Use cases, repository ports, orchestration ports |
| Agent | Agent loop, context budget, tool-call coordination |
| Tools.Abstractions | Tool descriptors, requests, results, execution context |
| Tools.BuiltIn | Files, search, shell, Git, and screenshot tools |
| Providers | OpenAI, Anthropic, Gemini, and compatible APIs |
| Browser | Task session routing, Playwright/CDP tabs, downloads, and browser tools |
| MCP | stdio and Streamable HTTP clients and tool adapters |
| Infrastructure | SQLite, migrations, repositories, HTTP/proxy creation |
| Platform.Windows | ConPTY terminal sessions, process integration, and capture |
| App | Avalonia views, embedded WebView2 host, view models, and composition |
| App.Analyzers | Compile-time shared visual component and appearance rules; no runtime UI behavior |

#### Stable boundaries

- Project configuration now carries a durable authorization revision (schema 26).
  Conditional saves reject stale revisions and return their own transaction's saved
  snapshot through the Application writer port, never adopting a later save's
  revision. Directory changes increment the version
  and notify instance-scoped leases after commit. Application guards combine this
  monitor with existing worktree validation. Agent/model streams, standard tools
  and long-lived PTY owners link their own cancellation to those leases; a bounded
  polling read detects changes from other instances. Revocation keeps known tool
  effects/audits and pauses queued continuation. PTY startup retirement also checks
  the lease's revoked flag before classifying
  a failed handoff, because linked-token notification may still be pending. Cleanup
  failures remain visible. Revocation is cooperative; resource draining and command
  isolation still require separate boundaries.
  See [PROJECT_AUTHORIZATION_REVOCATION_ACCEPTANCE.md](SECURITY.md#project-authorization-revocation-acceptance) for evidence and limits.

  Schema 27 adds a separate project-root authorization table. Each explicit grant
  binds a normalized root to a volume GUID/serial and 128-bit NTFS file ID. Legacy
  projects remain unconfirmed; startup and ordinary renaming never enroll the
  current object automatically. New roots enroll on explicit save; retained roots
  must match the prior object. A separate confirmation port advances the project
  revision, issues new grant IDs and revokes older leases. Registration holds the
  no-follow directory chain through its conditional transaction. Production App
  composition injects the Windows identity-session factory into both repository
  and monitor; task/tool/PTY leases retain that chain until their cleanup ends.
  The editor refreshes the selected project in place, preserving task and drafts.
  File-change statistics also invalidate cached contexts when that authorization
  revision changes, including reauthorization with unchanged path strings.
  Hosts without the optional factory retain compatibility behavior and do not
  establish this boundary. Strict resource draining, final-name CAS and command
  isolation remain separate gaps. See
  [ROOT_OBJECT_AUTHORIZATION_ACCEPTANCE.md](SECURITY.md#root-object-authorization-acceptance) for evidence and platform limits.

  Schema 28 adds immutable task/project/worktree directory-object grants in a
  separate table. Production Git creation enrolls while holding the identity
  session through its conditional insert. Restored worktrees require that saved
  object; legacy tasks require an explicit idle-task confirmation with task and
  project CAS checks. Confirmation never resumes a task or replaces an existing
  grant. ManagedWorktreeWorkspaceGuard now supplies a directory lease, combined
  with project revocation for Agent/tools/long-lived PTY cleanup. App's confirmation
  panel displays the full path and preserves the foreground draft. Creation still
  has a Git-process-to-enrollment handoff; removal releases the no-delete target
  handle before Git deletion. These races, Git metadata confinement, crash intents,
  replacement reauthorization and native GUI acceptance remain separate work.
  See [WORKTREE_OBJECT_AUTHORIZATION_ACCEPTANCE.md](GIT_WORKTREES.md#worktree-object-authorization-acceptance).

- Built-in file reads now use an optional Tools.Abstractions handle-validation
  port, injected by the production ToolExecutor. Platform.Windows resolves opened
  file/root handles before hashing or reading, refuses unmatched physical paths
  and closes rejected streams. Read/preview, edit verification, change snapshots
  and copy sources share this path; review snapshots use only the owning artifact
  directory as their explicit local scope. This is a point-in-time read boundary,
  not write-target, process or all-filesystem isolation. See
  [READ_HANDLE_PATH_SAFETY_ACCEPTANCE.md](SECURITY.md#read-handle-path-safety-acceptance) for regression evidence and remaining
  alias, revocation, native-call cancellation and concurrent-change limits.

- Atomic text writes use an optional Tools.Abstractions write-session factory,
  supplied by production DI from Platform.Windows. The session pins the local
  NTFS directory chain, creates/stages/renames relative to directory handles,
  and cleans uncommitted staging by its own handle. After snapshots consume the
  same exclusive staging stream. Existing target hashes and selected security/
  metadata are rechecked before native replacement; unsupported metadata fails
  closed before staging and again at commit. Known refusals carry localized safe
  detail through the port exception and standard tool-result/audit path.
  Reparse/case-sensitive directories and unsupported volumes are refused.
  No path-based commit fallback is used when the injected session fails. This
  does not provide final-name compare-and-swap, all filesystem operations or
  command isolation. See [WRITE_HANDLE_PATH_SAFETY_ACCEPTANCE.md](SECURITY.md#write-handle-path-safety-acceptance) for scope,
  compatibility limits and remaining recovery/concurrency work.

- Explicit path deletion and created-file undo share a Tools.Abstractions delete
  session port, registered in production from Platform.Windows. Preparation pins
  the existing NTFS directory chain and a bounded tree of no-follow object handles
  without write/delete sharing; native directory enumeration and child opens are
  relative to these handles. Undo hashes the same pinned content before deleting.
  Each post-order deletion revalidates task authorization; no native failure falls
  back to a path deletion. Partial completion invalidates task snapshots and is
  reported through the existing result/cancellation/output/audit pipeline. This is
  not a tree transaction or crash rollback. See [DELETE_HANDLE_PATH_SAFETY_ACCEPTANCE.md](SECURITY.md#delete-handle-path-safety-acceptance)
  for compatibility, evidence and remaining limits.

- File and directory copy now compose a handle-pinned source-tree session,
  authorized directory leases and the existing atomic stream writer. Source
  enumeration reuses native no-follow directory operations; preflight refuses
  unsupported entries before destination creation. Destination parent leases
  remain held through each file commit. Explicit directory creation uses the
  same port. Mutation exceptions preserve known partial effects across tool
  cancellation/timeout and the standard audit/result pipeline. See
  [COPY_HANDLE_PATH_SAFETY_ACCEPTANCE.md](SECURITY.md#copy-handle-path-safety-acceptance); this is bounded content/structure copy,
  not a transactional tree clone or complete metadata clone.

- Same-volume path move uses a Tools.Abstractions move session from Platform.Windows.
  The source parent chain and object stay pinned; file source hashes are checked on
  that handle against task observations. Destination parents remain leased through
  relative native rename, with final authorization and destination-content checks.
  No failure falls back to path copy/delete; cross-volume moves are explicitly
  refused. This is object rename, not a descendant snapshot, final-name CAS,
  journaled undo or crash recovery. See [MOVE_HANDLE_PATH_SAFETY_ACCEPTANCE.md](SECURITY.md#move-handle-path-safety-acceptance).

- Infrastructure task-artifact content reads now validate the opened stream
  against only the owning task directory before reading any bytes; preparation
  runs off the UI thread, retaining size/hash/media and cancellation checks.
  Production previews, edit snapshots and final review patches now share an
  artifact-file session built on these directory leases and atomic write sessions.
  Completion retains exclusive ownership; caller success explicitly retains the
  file, while failure cleans through the original handle. Edit snapshots stay owned
  until the project file commits and verify the copied hash. Cleanup failures are
  visible and do not prevent release of other snapshots. Crash/publication
  recovery remains a separate gap.
  See [ARTIFACT_HANDLE_PATH_SAFETY_ACCEPTANCE.md](SECURITY.md#artifact-handle-path-safety-acceptance) and
  [OWNED_ARTIFACT_SESSION_ACCEPTANCE.md](SECURITY.md#owned-artifact-session-acceptance) for exact evidence and remaining scope.

  Git diff/numstat temporary inputs now use an injected, dedicated read-only-path
  session port. Platform.Windows pins the directory chain and creates each file
  relative to its parent handle with no-follow, read-only sharing and native
  delete-on-close. Content is bounded to 4 MiB per input and checked on the same
  handle. Both inputs stay owned until the Git process stops, output drains and
  workspace/cancellation checks pass; cleanup uses the original handles and a
  cleanup refusal does not prevent release of the other input. Injected failures
  never fall back to path creation. Compatibility hosts without the port retain
  their old behavior. See [GIT_DIFF_INPUT_SESSION_ACCEPTANCE.md](GIT_WORKTREES.md#git-diff-input-session-acceptance) for actual Git,
  production-App CLI and Headless evidence. This does not isolate the Git process
  filesystem or complete worktree-root identity, strict revocation or crash recovery.

  App approval presentation publishes request identity and decision metadata
  before setting IsPending. A newly pending observer can resolve only that
  matching request; existing stale-identity rejection and policies are unchanged.
  Deterministic publication regression evidence is recorded alongside the Git
  input-session checkpoint above.

  Browser screenshots and downloads now use that injected artifact session too.
  Screenshots validate non-empty bounded bytes; downloads receive the original
  SDK stream through a write-only 32 MiB wrapper into an uncommitted staging
  session, then copy/hash into a final session using a sanitized extension.
  Staging cleanup and final workspace/cancellation checks precede final Retain.
  SDK cleanup uses the original IDownload object; cancellation drains the pending
  operation and retains cleanup-unknown detail in the tool error. This limits
  exported files, not browser network/cache use or screenshot allocation.
  Compatibility hosts without the optional session port keep the previous path
  implementation. See [BROWSER_ARTIFACT_SESSION_ACCEPTANCE.md](BROWSER_MCP.md#browser-artifact-session-acceptance) for native-file,
  simulated-SDK, Headless App and actual managed-Chrome CLI evidence and limits.

- Conversation follow-ups now connect Queue storage and runtime through App
  composition. Domain owns bounded immutable snapshots; Application revalidates
  saved models/agents, roots/worktrees, source access and attachment snapshots.
  Schema 23 atomically consumes drafts on enqueue and commits first-entry removal,
  one user message and Running status on dispatch. The dispatch port carries the
  prepared selection; the same short transaction rechecks project primary/roots,
  provider runtime settings, model identity/availability/media requirements and
  agent availability. Attachment/worktree I/O stays outside the transaction;
  mismatches leave the queue, task and next draft unchanged. Saved model parameters
  and agent instructions are retained. See [CONVERSATION_FOLLOWUP_DISPATCH_ACCEPTANCE.md](CONVERSATIONS.md#conversation-followup-dispatch-acceptance)
  for the settings race, rollback and real queued-process evidence and limits.
  App provides task-owned editing,
  ordering, removal, manual continuation and completion chaining through shared
  controls. Failure, cancellation, shutdown and reconstruction pause the chain;
  committed messages never return to the queue. Operations own cancellation,
  gates and dispatcher publication. Explicit Responses failed/incomplete events
  stop automatic continuation. Responses/Anthropic also reject empty HTTP 200
  and missing-terminal SSE EOF, stop on terminal events and discard incomplete
  model tool calls through the existing Agent failure path. Responses' explicit
  [DONE] compatibility is retained. Saved approval policy and CLI termination/
  manual recovery have controlled evidence; remaining stream variants, dispatch
  races and native/live acceptance remain gaps. See
  [PROVIDER_STREAM_COMPLETION_ACCEPTANCE.md](PROVIDERS.md#provider-stream-completion-acceptance),
  [CONVERSATION_FOLLOWUP_PROCESS_RECOVERY.md](CONVERSATIONS.md#conversation-followup-process-recovery) and
  [CONVERSATION_FOLLOWUP_RUNTIME_ACCEPTANCE.md](CONVERSATIONS.md#conversation-followup-runtime-acceptance);
  the storage checkpoint remains in [CONVERSATION_FOLLOWUP_STORAGE_ACCEPTANCE.md](CONVERSATIONS.md#conversation-followup-storage-acceptance).

  [QUEUE_BROWSER_RUNTIME_ACCEPTANCE.md](BROWSER_MCP.md#queue-browser-runtime-acceptance) adds a CLI check of the
  production Queue/Agent chain with actual managed Chrome and simulated model
  HTTP: cancelling one pending navigation preserves another task's pending
  approval/run/draft, and reconstructed services retain results and require
  manual continuation without replay. It does not establish native GUI, browser
  creation cancellation, process restart or command filesystem confinement.

  Browser session operations now check cancellation/stopped state before SDK
  entry. Cancellation or tool deadline invalidates the task session, awaits
  owned context/page cleanup and drains the pending SDK operation before returning.
  A stale session cannot be reused; explicit manager release and reattachment
  recover without replay. Borrowed CDP context/user pages remain open. Real Chrome
  reproduces the old late click and confirms the integrated fix with completed
  approvals/audits; see [BROWSER_OPERATION_CANCELLATION_ACCEPTANCE.md](BROWSER_MCP.md#browser-operation-cancellation-acceptance) for evidence
  and remaining creation/crash/native acceptance limits.

  Two later original-assembly CLI checks add actual managed-Chrome evidence at
  profile-creation cancellation and at host-only process termination after
  completed tool audits. They preserve another owned task/instance and require
  explicit newly approved recovery, with no automatic replay; see
  [BROWSER_CREATION_CANCELLATION_ACCEPTANCE.md](BROWSER_MCP.md#browser-creation-cancellation-acceptance) and
  [BROWSER_PROCESS_RECOVERY_ACCEPTANCE.md](BROWSER_MCP.md#browser-process-recovery-acceptance). These do not establish all SDK launch
  instants, mid-side-effect crashes, native GUI, embedded/daily/CDP recovery or
  Agent/Queue continuation across a real browser-host crash.

- Active-run text guidance uses `ConversationSteeringSession` and the Agent's
  steering input port. Schema 25 atomically accepts a bounded receipt and consumes
  its revision-checked draft; a safe boundary inserts a pending prefix once before
  the next request, after the current tool batch. Required input must fit intact.
  The run's provider/model/agent, tool roots, approval policy and cancellation
  remain captured. App exposes shared controls, active Ctrl+Enter and bounded
  receipt history; Enter retains Queue. Each run owns its gate and acceptance
  lifetime, while draft coordination is per entry. App and SQLite recheck exact
  authorization for acceptance/application. Any project directory change now
  revokes the old run, including an empty steering boundary; new authorization
  requires a new run. Failure/stop/reconstruction
  close acceptance and mark pending receipts Interrupted without replay. History
  registration, gate waits and late repository errors are contained during stop;
  ordinary failures retain localized manual retry. See
  [CONVERSATION_STEERING_PLAN.md](CONVERSATIONS.md#conversation-steering-plan) and [CONVERSATION_STEERING_ACCEPTANCE.md](CONVERSATIONS.md#conversation-steering-acceptance) for the
  guarded integration, exact-candidate full Debug/Release evidence and separate
  controlled process recovery. Attachment guidance, configurable submission
  behavior, native/live acceptance and mid-side-effect/commit crashes remain gaps.

- Schema 24 records observed tool interruptions separately from execution results.
  Infrastructure recovers Running/Waiting tasks and unfinished audits in one
  transaction without replay, fabricated completion/approval/success or lost
  artifacts. Earliest interruption metadata is stable; a later real completion
  remains authoritative. App's shared audit row distinguishes unknown effects
  from live work. Actual Queue/Agent + managed Chrome checks cover host termination
  during a held navigation, another preserved instance and explicit continuation;
  see [AUDIT_INTERRUPTION_RECOVERY_ACCEPTANCE.md](TERMINAL_TOOLS.md#audit-interruption-recovery-acceptance) for evidence and limits.
  [BROWSER_COMMITTED_EFFECT_RECOVERY_ACCEPTANCE.md](BROWSER_MCP.md#browser-committed-effect-recovery-acceptance) adds original Debug/Release
  checks after a flushed synthetic POST effect but before the click result:
  restart/continuation preserve the effect once and keep its audit result unknown.

- Windows commands currently validate the working directory and own a cancellable
  Job Object, but ordinary CreateProcess tokens do not isolate filesystem access.
  A production-pipeline temporary-marker reproduction confirms outside-root reads.
  This is an open safety blocker, not an established sandbox boundary. Independent
  AppContainer feasibility checks are not integrated. A disposable-volume check
  supports ordinary PowerShell 7 file commands only after granting root metadata
  access; real-volume provisioning and strict scope remain unresolved. A separate
  authorized read-only elevated preflight confirms all 44 KnownDll WRITE_DAC
  requests are refused; native queries identify WinTcb-Light trust labels, so
  the volatile global runtime-ACL lease route is rejected on this host. See
  [COMMAND_FILESYSTEM_ISOLATION_PLAN.md](SECURITY.md#command-filesystem-isolation-plan) for evidence and the implementation gate.
  [WINDOWS_ISOLATION_BACKEND_DECISION.md](SECURITY.md#windows-isolation-backend-decision) records the public MXC SDK review and
  actual ordinary-user probe: this 23H2 host offers AppContainerDacl fallback,
  not BaseContainer. IsSupported alone is insufficient. No SDK adapter, system
  setup, weaker fallback or supported-platform change is integrated. The user has
  selected native MXC on a separate supported Windows acceptance host; the next
  dependency is that host, not another backend-choice prompt. Process and
  ConPTY isolation/cancellation remain separate required acceptance gates.

- Source accounts are distinct from AI providers. Domain validates immutable
  credential-bearing account values with safe display/JSON boundaries;
  Application owns revision-checked persistence and identity ports. Schema 18
  and a dedicated Infrastructure GitHub client implement saved token/proxy
  settings and bounded HTTPS identity requests with no redirects/cookies or
  silent proxy fallback. App uses shared dialogs and metadata-only rows, not
  SQL/HTTP. Identity verification grants no project roots, tools or repository
  permissions. Schema 19 adds separate, credential-revision-bound read grants,
  irrevocable grant-ID tombstones and source-operation sessions. These sessions
  own standard task audit identities but are excluded from ordinary chat catalogs;
  they create no project, roots, worktree or model chat. App's PR page invokes
  source.pr_list/source.pr_get and separate files/comments/checks tools through
  the common executor, with fixed read-only
  capability and exact source scope. Async prevalidation runs before and after
  approval with cancellation/deadlines; Application rechecks grants/accounts after
  HTTP before returning data. Source scope restricts all other tools even without
  an explicit name allowlist, and child gateways retain the parent source scope.
  The dedicated client constructs local page/PR endpoints, bounds responses and
  parses untrusted description text without following server links. This is
  configured-repository reading, with before/after base/head checks for bounded
  file patches, comments, check runs and legacy statuses. Revision/scope changes
  invalidate all inspection categories; page cancellation and late cleanup are
  awaited. Domain's shared bounded patch map validates file patches and current
  inline-comment anchors against exact paths, head commits and LEFT/RIGHT lines.
  App renders verified comments below their diff lines and supports keyboard
  location with visible focus; same-side ranges attach to their verified end line.
  Navigation, pagination, observed revision/grant changes and disposal invalidate
  markers and old commands. Historical, missing and unsupported locations remain
  readable without pretending to match the current patch. This adds no HTTP or
  filesystem access. It is not atomic snapshot isolation or an inbox,
  OAuth or confirmed source writes. See [SOURCE_PR_INLINE_COMMENTS_ACCEPTANCE.md](GIT_WORKTREES.md#source-pr-inline-comments-acceptance),
  [SOURCE_PR_INSPECTION_ACCEPTANCE.md](GIT_WORKTREES.md#source-pr-inspection-acceptance) and [SOURCE_PR_READING_ACCEPTANCE.md](GIT_WORKTREES.md#source-pr-reading-acceptance).
  See [SOURCE_ACCOUNT_CONNECTION_ACCEPTANCE.md](GIT_WORKTREES.md#source-account-connection-acceptance).

- Schema 20 binds separate ordinary projectless review conversations to an exact
  grant/PR/base/head tuple. Application builds an immutable bounded snapshot from
  explicitly loaded inspection pages; reopening that tuple preserves the first
  snapshot, while a new base/head creates another conversation. These are model
  chats, unlike the hidden repository-operation audit sessions above. Workspace
  history, model/reasoning selection, streaming, cancellation and persistence
  remain shared. The snapshot is user-role untrusted data with fixed review
  instructions, carried separately from evictable history. Agent reserves its
  full message cost before selecting recent turns; insufficient capacity refuses
  the provider request instead of dropping or clipping the snapshot. App shows
  an actionable localized failure-card hint and never retries automatically.
  Schema 21 preserves the model-context-capacity category across catalog reloads
  and reopen using the existing task-failure port and an atomic forward migration.
  Roots,
  working directory, tools, browser and subagents remain
  unavailable. Access is revalidated at opening, before appending a user prompt,
  before provider execution and before publishing each model event. Local history
  remains readable after revocation, but new turns and later output are refused.
  Failed-check investigation only seeds an empty target draft through the existing
  composer cache and compare-and-swap persistence, preserving foreground input,
  attachments and conflicts. It never sends, follows log URLs or posts feedback.
  Inboxes, feedback writes and live/native acceptance remain separate gaps.
  See [SOURCE_PR_REVIEW_CHAT_ACCEPTANCE.md](GIT_WORKTREES.md#source-pr-review-chat-acceptance).

- Schema 22 preserves old review chats as Snapshot and adds an explicit
  ReadOnlySource mode to the grant/PR/base/head uniqueness key. Opening that
  separate conversation neither sends a model request nor reads additional source
  data. Only source.pr_get/files/comments/checks metadata is projected for its
  model; the globally hidden source tools remain hidden in ordinary chats. The
  task retains no local roots, working directory, browser or subagents. Requests
  must match its PR number and base/head; application checks grant/account
  revisions after HTTP and checks remote commits before/after inspection pages.
  The standard pipeline still owns approval, cancellation, per-tool deadlines,
  bounded output and audit. Agent starts tool execution off the caller's UI
  synchronization context, streams bounded progress, and cancels/awaits the same
  operation when enumeration stops. Access invalidation during progress can
  therefore finish the audit as either scope denial or cancellation; neither
  permits late content or another provider request. Source failures use fixed
  localized scope/revision/output hints rather than arbitrary exception text.
  Pinned context, history and explicit manual continuation stay shared; the old
  snapshot chat never acquires source tools. See
  [SOURCE_PR_ON_DEMAND_READING_ACCEPTANCE.md](GIT_WORKTREES.md#source-pr-on-demand-reading-acceptance) for controlled evidence and limits.

- Domain parses explicit PR web links without retaining query/fragment data.
  Application resolves only the selected account revision and its existing
  exact-repository grant; no URL creates scope or makes a network request by
  itself. App's separate link form invokes the existing source.pr_get pipeline,
  drains previous inspection work, rejects stale/canceled results and attaches
  the existing detail/inspection/chat flow. GitHub web/API mapping is fixed;
  Enterprise web links require the configured API host/port, preserving its API
  prefix. The shared dialog retains independent grant drafts, localized repair
  messages and keyboard focus. URL suffixes identify the current PR, not a
  historical commit or comment location. See [SOURCE_PR_URL_ACCEPTANCE.md](GIT_WORKTREES.md#source-pr-url-acceptance) for
  the candidate's validation scope and remaining native/live/PR-workflow gaps.

- App's composer owns task, project-new-conversation and projectless draft scopes
  through dedicated coordinators and the Application persistence port. Draft
  reads, delayed writes and reconciliation run independently of the UI message
  loop; notifications still marshal to the captured UI context. Initial workspace
  loading and explicit refreshes are serialized so an earlier initialization
  cannot overtake an awaited refresh and reset a later model/agent choice.
  Version-13
  SQLite text rows and version-15 file-backed attachment metadata share revision-
  checked transactional writes; version 16 adds validated media-type metadata
  and version 17 admits explicitly selected projectless attachment drafts.
  Bounded UTF-8 and static-image bytes stay outside SQLite.
  Delayed reads cannot replace newer input or another scope. Successful first
  sends promote only the next unsent text and attachment selection identities;
  failed sends retain them. Missing/corrupt snapshots retain removable chips and
  block sending; changed project authorizations require removal/reselection.
  Storage failures/conflicts retain local input and block silent normal exit.
  The shutdown overlay supports retry, explicit conflict replacement and removal
  of unavailable attachments without discarding text or valid attachments.
  This grants no tool/filesystem access. Foreground sends use Application's
  `IConversationSubmissionRepository`: task, user message, attachment references
  and revision-checked draft consumption/promotion share one SQLite transaction.
  App serializes this with background draft writes and retains input edited
  during the commit. Conflicts roll back before a model request. Cancellation
  before commit retains input; after commit the saved conversation becomes
  Cancelled and is selected for explicit manual continuation. Snapshot files/Git
  remain outside that transaction and failed writes can leave unreferenced files;
  startup never sends restored input automatically. Native restart and real-provider image
  interpretation remain incomplete. See [CONVERSATION_DRAFT_ACCEPTANCE.md](CONVERSATIONS.md#conversation-draft-acceptance),
  [ATTACHMENT_DRAFT_ACCEPTANCE.md](CONVERSATIONS.md#attachment-draft-acceptance), [IMAGE_ATTACHMENT_WORKFLOW_ACCEPTANCE.md](CONVERSATIONS.md#image-attachment-workflow-acceptance)
  and [CONVERSATION_SUBMISSION_ACCEPTANCE.md](CONVERSATIONS.md#conversation-submission-acceptance).

- App attachment selection is scoped to a project authorization snapshot or
  explicit projectless file selection, and its conversation. Navigation,
  authorization changes, cancellation, timeout and
  shutdown invalidate pending picker/reader results; stale results cannot enter
  another conversation. Explicit text/static-image selection continues through
  `IWorkspaceAttachmentReader` for projects or `ISelectedAttachmentReader` for
  exact absolute files selected without a project. The latter refuses device/ADS
  paths and link/reparse ancestors; it never authorizes the parent directory or
  exposes a model tool. Both reuse bounded reads and the image decoder port.
  Selection stamps are internal consistency hashes, not unforgeable capabilities;
  restored attachments read internal snapshots, never original source paths.
  This does not add tool permissions or PDF inputs. See
  [ATTACHMENT_SELECTION_ISOLATION_ACCEPTANCE.md](CONVERSATIONS.md#attachment-selection-isolation-acceptance) and
  [PROJECTLESS_ATTACHMENTS_ACCEPTANCE.md](CONVERSATIONS.md#projectless-attachments-acceptance).

- Submitted text attachments use `IConversationAttachmentStore`: immutable UTF-8
  files under task/message artifact identities, never re-reads of source paths.
  Version-14 SQLite commits message references/metadata with the task and message;
  bytes remain outside the database. Historical chips restore from metadata.
  Model history validates ownership, length/hash, links and bounded text reads,
  then appends untrusted data only to its original user turn. Older attachments
  outside the model-aware hydration budget receive an explicit omission marker;
  Agent retains its final context-window budget. Missing/corrupt snapshots fail
  before provider calls with a persisted localized diagnostic. Thirty-second
  file operations propagate cancellation off the UI thread. This is not a
  filesystem/SQLite distributed transaction; unreferenced snapshots may remain
  after failed persistence. See [ATTACHMENT_HISTORY_ACCEPTANCE.md](CONVERSATIONS.md#attachment-history-acceptance).

- Conversations may have no project: a null `AgentTask.ProjectId` is a persisted
  plain-chat scope, not an implicit directory grant. Such runs advertise no tools
  or subagents and reject unsolicited tool calls. The standard execution pipeline
  also denies tools when `ToolsEnabled` is false. No terminal, browser or automatic
  filesystem context is attached. Explicitly selected text/images remain user
  inputs, not tool or directory grants. See [PROJECTLESS_CHAT.md](CONVERSATIONS.md#projectless-chat) for migration
  and validation scope.

- Application's immutable `ModelImage` envelope extends `ModelMessage` without
  putting pixels into plain text or ordinary JSON persistence. Request-local
  validation checks image capability, user-only ownership, count and byte limits;
  Providers folds validation into its existing message traversal and emits native
  Responses, compatible Chat, Anthropic and Gemini blocks. Final image JSON has
  a UTF-8 body limit. Agent charges conservative encoded-character costs as fixed
  context metadata, retaining or evicting whole images, and marks image omission
  in textual shared-subagent context. [IMAGE_INPUT_ACCEPTANCE.md](CONVERSATIONS.md#image-input-acceptance) records this
  transport groundwork, not native-picker or paid-provider acceptance.

- Domain's immutable `AttachmentImage` stores validated snapshot metadata and
  non-diagnostic Base64. Infrastructure's Skia decoder verifies PNG/JPEG/GIF/WebP
  content, static frames (including APNG rejection), dimensions, pixel counts and
  encoded sizes before accepting full decoded pixels. Reader/store/draft ports
  retain authorized paths, cancellation and thirty-second operation boundaries;
  stores re-decode images before publishing immutable binary snapshots. Version-16
  SQLite changes media/size constraints while preserving old UTF-8 rows and files.
  Opaque internal `.txt` snapshot names are retained for compatibility; checked
  MIME, length/hash and actual decoding determine image content, not extensions.
  App retains unavailable typed chips, rejects incompatible model selection,
  permits image-only messages and restores image blocks on the original user
  turn, never inside text context. Context/count limits omit whole older payloads
  with explicit markers, not the latest selected image. File/SQL operations are
  still not one distributed transaction. Native synchronous decoding cannot be
  interrupted midway: bounded work may finish after the awaiting caller cancels.
  Projectless selection now uses the independent exact-file reader port.
  Composer/draft thumbnails now use Application's `IAttachmentThumbnailRenderer`
  and Infrastructure's bounded, serialized background Skia rendering. The shared
  chip owns cancellation, stale-result rejection and bitmap disposal; it only
  sees already-selected bytes and a small validated PNG, never reads source paths
  or grants tool access. The source decoder and renderer share format/animation/
  pixel validation. Sent rows retain the selected immutable thumbnail source;
  restored history receives lazy sources from App's
  `ConversationAttachmentThumbnailFactory`, which validates task/message metadata
  and reads only the existing attachment-store port. The factory gates snapshot
  loads and rendering; detach cancellation and fallback remain control-owned.
  It adds no filesystem/tool permission or eager history hydration.
  Infrastructure now retains the codec origin and applies it after thumbnail
  resizing, using only a bounded small surface. JPEG EXIF previews normalize all
  eight orientations without rewriting original bytes or encoded dimensions.
  [ATTACHMENT_THUMBNAIL_ACCEPTANCE.md](CONVERSATIONS.md#attachment-thumbnail-acceptance) and [IMAGE_ORIENTATION_ACCEPTANCE.md](CONVERSATIONS.md#image-orientation-acceptance)
  separate deterministic component/draft/sent-history evidence from remaining
  other-format orientation checks, PDF, child-agent image sharing, native
  picker/restart and live-provider format/interpretation acceptance.
  See [IMAGE_ATTACHMENT_WORKFLOW_ACCEPTANCE.md](CONVERSATIONS.md#image-attachment-workflow-acceptance).

- Tools.BuiltIn's internal snapshot reader allows delete sharing but not write
  sharing. Windows atomic edits can publish a new file while an existing preview
  retains its original handle/bytes; in-place mutation and external non-delete-
  sharing locks remain blocked. Path authorization and current-path hash checks
  still govern every edit. [FILE_SNAPSHOT_SHARING_ACCEPTANCE.md](GIT_WORKTREES.md#file-snapshot-sharing-acceptance) records the
  deterministic pipeline reproduction and unchanged conflict/cleanup boundaries;
  this does not establish the cause of older authorization-test failures.

- Tool contexts may carry an immutable, case-insensitive allowed-name scope,
  independent of directory roots and approval policy. Null retains existing task
  behavior; empty denies all tools, and no scope can override `ToolsEnabled=false`.
  The standard executor validates the scope after schema/capability checks and
  before workspace/prevalidator/approval side effects, with an audited denial.
  Agent filters each model turn's descriptors and delegation catalog; unsolicited
  calls still go through the executor. Subagent gateways capture the parent scope
  and intersect child scopes rather than permitting a broader child context.
  This is groundwork for explicit source-review tasks, not source-account grants,
  projectless tool access or a completed PR workflow. See [TOOL_SCOPE_ACCEPTANCE.md](TERMINAL_TOOLS.md#tool-scope-acceptance).
- Tools.BuiltIn owns common input-schema validation before capability checks,
  approval or execution, including MCP adapters. JsonSchema.Net 8.0.5 replaces
  the incomplete handwritten assertion evaluator; per-call dialect handlers
  retain Unicode-scalar string lengths and bounded .NET ECMAScript patterns.
  Schema/value budgets and cancellation checks reject oversized input, unsafe
  references and malformed optional subschemas. Only acyclic same-document
  JSON Pointer references are accepted; nested resource IDs, dynamic/recursive
  references, anchor-based references and external resolution fail closed.
  BuildOptions explicitly owns a new SchemaRegistry per invocation with remote
  fetching disabled; the library default points to Global and must not be used.
  Request schemas and Fetch delegates never modify the global library registry.
  Schema errors never echo argument values or library
  exception text. Existing audit, approval, output and persistence ports remain
  unchanged. See [TOOL_SCHEMA_ACCEPTANCE.md](TERMINAL_TOOLS.md#tool-schema-acceptance) for exact limits and verification,
  and [TOOL_SCHEMA_ISOLATION_ACCEPTANCE.md](TERMINAL_TOOLS.md#tool-schema-isolation-acceptance) for cold-start concurrency evidence.

- Providers' Anthropic parser stores each tool block's identity, name and argument
  buffer in one request-owned entry instead of three parallel dictionaries.
  Compatible Chat parsing appends selected choices directly to one frame-owned
  event list, avoiding per-choice lists and copies. Failures still discard the
  whole frame, alternate choices remain excluded and previously returned lists
  are never reused. Tool-name decoding, SSE limits, cancellation and downstream
  execution policies remain unchanged. See [PROVIDER_PARSER_OPTIMIZATION_ACCEPTANCE.md](PROVIDERS.md#provider-parser-optimization-acceptance).

- Provider adapters share failure-event sanitization; non-failure events keep their
  published identity. HTTP 407 is mapped to local authentication guidance before
  reading any proxy body, in both discovery and streaming. Credentials remain owned
  by Infrastructure's existing HTTP/SOCKS5 client factory, not by Providers or UI.
  Other response limits, decoding and cancellation remain unchanged. Deterministic
  settings-to-runtime and native success/direct-route evidence is in
  [AUTHENTICATED_PROVIDER_PROXY_ACCEPTANCE.md](PROVIDERS.md#authenticated-provider-proxy-acceptance). Native wrong-password/recovery is
  also verified for Gemini over HTTP; missing credentials, SOCKS5 and other-protocol
  failure/recovery retain deterministic coverage, not native coverage.

- App's proxy editor reloads the catalog before publishing a saved selection, so
  two-way ListBox feedback cannot clear its target before completion. ID/revision
  guards retain newer drafts and await configuration notifications. Availability,
  reload and command checks share an allocation-free ID lookup without a cache.
  See [PROXY_EDITOR_SELECTION_ACCEPTANCE.md](PROVIDERS.md#proxy-editor-selection-acceptance) for real-page and native regressions.

- Provider, browser and MCP settings share ProxySelection's lazy option projection:
  the localized direct option comes first, followed by enabled profiles in repository
  order. Default flags and duplicate occurrences do not alter ordering. Browser
  settings build options and enabled identities in one pass without an intermediate
  profile array; each editor retains its existing selection, unavailable-proxy,
  revision and cancellation rules. No cross-editor cache or execution-policy change.

- App's shared collection synchronization skips matched prefixes and avoids
  membership sets for unchanged snapshots, appends and tail removals. Model catalogs
  filter indexed repository snapshots directly without per-provider LINQ closures.
  Row identity, duplicate order, observable deltas, model selection and cancellation
  remain unchanged. Provider/model editors share current-snapshot identity lookups
  without LINQ closures or caches; row commands resolve their target once, retaining
  first-occurrence and provider/model ownership rules. See
  [CATALOG_COLLECTION_OPTIMIZATION_ACCEPTANCE.md](ENGINEERING.md#catalog-collection-optimization-acceptance).

- App's model search scans group members once and materializes only changed partial
  membership. Equivalent matches retain immutable groups without filtering iterators;
  the picker compares the published prefix before allocating a desired-group list.
  Changed lists still use shared observable reconciliation. Search ordering, duplicate
  occurrences, selection, focus, reload generations and unavailable-model rules remain
  unchanged. See [CATALOG_COLLECTION_OPTIMIZATION_ACCEPTANCE.md](ENGINEERING.md#catalog-collection-optimization-acceptance).

- App's project, pinned and recent sidebar lists share indexed title/pin filtering
  and lazy navigation-row reconciliation. Equal prefixes need no temporary filter
  snapshots or row maps; changed suffixes retain duplicate occurrence order and
  authoritative task metadata. Recent tasks sort at load and insert in timestamp
  order, rather than sorting on every search keystroke; timestamp ties retain the
  existing stable-order rules. Project-list subscriptions follow collection deltas;
  source moves still refresh stable duplicate-key ordering without rescanning
  subscriptions. Notifications captured before disposal cannot republish the list.
  See [SIDEBAR_REFRESH_OPTIMIZATION_ACCEPTANCE.md](ENGINEERING.md#sidebar-refresh-optimization-acceptance).

- Gemini text-only and metadata frames retain their mapper-owned event snapshots
  after failure, finish-state and continuation validation, avoiding a second list.
  Per-frame tool counting is linear and independent of text/thought parts; tool
  names and native identities are decoded once. Mixed frames still defer calls
  until verified completion and discard a whole frame on failure. No state is
  shared across frames or parsers. See [PROVIDER_PARSER_OPTIMIZATION_ACCEPTANCE.md](PROVIDERS.md#provider-parser-optimization-acceptance).

- Providers' bounded SSE line reader scans decoded chunks for line endings instead
  of appending every character. Complete lines use their own string directly;
  unfinished line segments use a lazy iterator-owned buffer. Strict UTF-8, leading
  BOM handling, CR/LF/CRLF, character budgets, cancellation and caller-owned stream
  lifetime remain unchanged. See [PROVIDER_PARSER_OPTIMIZATION_ACCEPTANCE.md](PROVIDERS.md#provider-parser-optimization-acceptance).

- App tool-message persistence stores only raw tool fields and dialogue messages.
  Computed visibility flags and dialogue view models are excluded from JSON, so
  saving results does not duplicate dialogue content or parse Markdown for storage.
  Presentation remains derived from raw messages; old payloads with redundant
  presentation fields still load without rewriting user data or database migrations.
  Standard JIT JSON serialization remains in use. See [SUBAGENT_EXECUTION_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#subagent-execution-acceptance).

- Tool output limiting consumes the existing shared budget in one pass rather than
  validating/counting again on overflow. Complete file-change and dialogue collections
  retain their published references; only truncated prefixes are copied. Invalid records,
  whole-record priority, surrogate boundaries, late committed results and audit semantics
  remain unchanged. See [TOOL_OUTPUT_OPTIMIZATION_ACCEPTANCE.md](TERMINAL_TOOLS.md#tool-output-optimization-acceptance).

- App's tool-result persistence and live presentation share one raw-payload mapping.
  Ordinary assistant/system messages still parse Markdown; user bubbles and tool
  rows retain raw text without building hidden response blocks. Subagent answers
  keep their separate Markdown projection. Table detection rejects non-candidates
  before splitting cells, preserving existing single-column and escaped-pipe rules.
  No execution policy, stored JSON shape or shared appearance changes. See
  [UI_HOTPATH_OPTIMIZATION_ACCEPTANCE.md](ENGINEERING.md#ui-hotpath-optimization-acceptance) for regression and allocation evidence.

- App's code-preview parser and literal search share one bounded line reader.
  It stops at the existing character/line limits instead of splitting discarded
  content; CRLF, lone carriage returns, trailing empty lines and truncation labels
  keep their previous meaning. Search scans visible lines once and resets results
  once per content replacement. No snapshot cache, file access, appearance or tool
  execution change. See [UI_HOTPATH_OPTIMIZATION_ACCEPTANCE.md](ENGINEERING.md#ui-hotpath-optimization-acceptance).

- App's tool progress, final result and child dialogue share indexed message-row
  lookup without captured predicates or a cross-task cache. Each event retains
  its existing first-match, call-identity and tool-name rules. Progress display
  names consume at most the first 80 Unicode scalars in a fixed local buffer;
  control removal, malformed UTF-16 replacement and localized fallback stay
  unchanged. Summary-detail checks use spans rather than copying trailing
  whitespace. See [TOOL_PROGRESS_ACCEPTANCE.md](TERMINAL_TOOLS.md#tool-progress-acceptance) for regressions and measurements.

- Agent's model context builds only the newest conversation groups needed by the
  current budget instead of materializing every historical turn. System ordering,
  whole-turn retention, tool-call/result grouping, opaque continuation signatures
  and independent request snapshots remain unchanged. Shared subagent context
  locates the latest twelve relevant messages by index without scanning discarded
  history or allocating a rolling queue; role labels and the bounded Unicode-safe
  tail remain unchanged. See [CONTEXT_BUILDING_OPTIMIZATION_ACCEPTANCE.md](ENGINEERING.md#context-building-optimization-acceptance).

- App's shared conversation timeline projects recorded timestamps and elapsed time
  from existing messages without changing persistence or model context. User turns,
  execution disclosure and final replies retain message identity; source switching
  and visual detachment release subscriptions. Range-based grouping avoids copied
  turn lists, and unchanged derived visibility/command state does not notify again.
  Message subscriptions follow collection deltas with occurrence counts; moves
  retain subscriptions and resets rebuild them. Unchanged projections reuse the
  anchor map and observable rows, creating activity snapshots only after a matched
  prefix diverges. Changed grouping prunes removed anchors before publication.
  Tool-only and cancelled turns never manufacture an empty reply. Shared controls
  and semantic styles own disclosure geometry and divider colors. See
  [CONVERSATION_TIMELINE_ACCEPTANCE.md](CONVERSATIONS.md#conversation-timeline-acceptance) for reproduced failures and native evidence.

- File-write and text-replace tools report paths and before/after SHA-256 only after
  atomic commit. Whole validated records share the standard output budget; cancelled
  or timed-out results retain already committed changes. Agent collects child-tool
  records without file access. App persists optional raw metadata in the existing
  tool payload and derives a task-owned file summary using shared controls/styles.
  Preview and Git review reuse the authorized tool pipeline; Git review remains the
  current workspace diff. The task file summary opens immutable before/after snapshot
  patches through the registered filesystem.review_change tool, not the live file or
  Git workspace. Consecutive hash-linked writes retain the first before and last after
  version; discontinuities are marked incomplete and net-zero edits are omitted.
  Writes retain bounded before/after
  raw-byte snapshots in the task artifact directory, with optional filename/length/
  SHA-256 references in existing metadata. Capture shares the original atomic lock
  and version checks; the after snapshot comes from staged bytes before commit.
  Unchanged writes and staging failures do not create history; unsuccessful commits
  discard uncommitted snapshots. Readers validate names, limits, links, length and
  digest without falling back to the live file. Review uses the bounded task-owned Git
  process runner in no-index/text mode with external diff/textconv disabled. Strict
  UTF decoding, patch bounds, task switching and cancellation fail closed; private
  staging files are removed, and the UTF-8 patch is a hash-verified task artifact.
  Current-file and historical-diff tabs remain separate. Snapshot restoration uses a
  registered UI-only mutation tool and the existing byte-preserving atomic writer;
  created-file removal is a separate high-risk tool, rejected by FullAccess and
  requiring AskEveryTime approval. Both require an exact ReviewedChange and verified
  history/current versions. Inverse metadata reuses validated snapshot references.
  Absent after versions require an explicit AfterDeleted marker, so malformed legacy
  null-after records are not reinterpreted as deletions. Shared request/schema checks
  and snapshot-pair reads also serve historical review, including deletion patches.
  Undo uses a shared confirmation notice, owned cancellation and a separate App service
  calling execution/persistence ports. Current read-only selection, running tasks,
  incomplete history and missing snapshots disable it. FullAccess created-file removal
  tightens only that request to AskEveryTime; committed records use an independent
  bounded save token even after interruption. Save failure retains the same history
  identity for persistence-only retry and prevents further undo or normal exit.
  The shutdown overlay exposes an explicit retry, never a default force exit.
  Pending and successful cleanup share their task; failed/cancelled cleanup can be
  retried without restarting models or tools or clearing unrecoverable run failures.
  Window ownership rejects duplicate attempts and late completion after disposal.
  See [SHUTDOWN_RECOVERY_ACCEPTANCE.md](TERMINAL_TOOLS.md#shutdown-recovery-acceptance) for storage-recovery and native evidence.
  Restore/remove now verify current versions under the writer's shared gate, avoiding
  redundant unlocked reads competing with Windows replacement. This is not a multi-file transaction
  or an OS-level cross-process compare-and-swap. See [FILE_CHANGE_UNDO_ACCEPTANCE.md](GIT_WORKTREES.md#file-change-undo-acceptance).
  See [FILE_CHANGE_SUMMARY_ACCEPTANCE.md](GIT_WORKTREES.md#file-change-summary-acceptance), [FILE_CHANGE_SNAPSHOT_ACCEPTANCE.md](GIT_WORKTREES.md#file-change-snapshot-acceptance)
  and [TASK_SNAPSHOT_REVIEW_ACCEPTANCE.md](CONVERSATIONS.md#task-snapshot-review-acceptance).

- File-summary subscriptions follow message collection deltas; unrelated text and
  tool progress do not rebuild the summary. Equivalent file records retain their
  rows and collapsed visible snapshot. Reordering keeps conversation-order last-write
  semantics; removed and disposed rows cannot refresh the view. Git branch discovery
  uses symbolic HEAD so newly initialized repositories need no commit, with the same
  bounded runner for detached-HEAD fallback. No approval or publishing-policy change.

- App's repository-status projection owns every Git metadata read through the
  existing operation lifetime and Git port. Refresh generations reject obsolete
  results and reentrant stale starts; a pending selection shows unknown status
  instead of another project's branch. Shutdown and disposal cancel and join all
  reads, including a noncooperative port, before releasing the workspace. Repeated
  stop calls share one completion. Metadata errors use localized unknown labels,
  never raw exception messages; null-branch and worktree-path semantics remain
  unchanged. Library waits do not capture the caller's UI context; final status
  publication uses cancellable UI dispatch so closing can retract a queued update.
  No command, approval, persistence or native-process implementation is added to
  UI. See [REPOSITORY_STATUS_LIFETIME_ACCEPTANCE.md](GIT_WORKTREES.md#repository-status-lifetime-acceptance).

- File-summary line statistics use the registered UI-only read-only
  `filesystem.change_statistics` tool, through the standard execution pipeline.
  Historical review and numstat share bounded task-owned diff staging and cleanup;
  statistics validate immutable snapshots without reading current files or creating
  patch artifacts. App merges refreshes, queries serially, cancels changed task or
  authorization contexts and waits for owned work at shutdown. Missing or incomplete
  history hides totals instead of inventing zeros; explicit retry only queries
  unavailable rows. Totals use one scan and publish only changed values, preserving
  resolved rows and their immutable array when just the completeness flag changes.
  Shared cards render per-file and aggregate statistics, with file-path accessible
  button names. See [FILE_CHANGE_STATISTICS_ACCEPTANCE.md](GIT_WORKTREES.md#file-change-statistics-acceptance) for focused, full and
  native verification; this is not a claim of full-product visual parity.

- File-summary typography, its compact plus/minus document glyph and the more-files
  chevron belong to shared FileChangeVariants. AppActionButton exposes validated
  trailing-icon spacing for scoped style overrides instead of trying to overwrite
  constructor-owned child margins; existing actions keep their default spacing.
  Per-file paths retain their immutable-snapshot commands and accessible names.
  See [FILE_CARD_FIDELITY_ACCEPTANCE.md](UI_WORKFLOWS.md#file-card-fidelity-acceptance) for native screenshots, narrow/wide geometry
  and multi-scale raster checks; card content is not substituted by a mockup.

- Model streaming and model discovery use separate Application ports. `IModelProvider`
  owns protocol selection and streaming only; settings retain `IModelDiscoveryService`
  for discovery, paging and provider/proxy validation. Adapters no longer carry an
  unused model-list projection or depend on discovery just to satisfy that contract.
  Gemini request composition lazily indexes assistant call identities once per build,
  preserves the first ordinal match (including a missing native identity), and appends
  consecutive tool responses directly to one content group without cloning JSON trees.
  The index is never shared between requests; signed content and security limits stay
  unchanged. See [GEMINI_CONTINUATION_ACCEPTANCE.md](PROVIDERS.md#gemini-continuation-acceptance) for deterministic regression evidence.

- App.Tests and Infrastructure.Tests link the same stateless `SqliteTestPool`
  source from `tests/Shared`; neither test assembly depends on the other.
  Cleanup uses the production connection factory with an explicit test data root
  and retires only that database's pool. Outer workspace roots and nested `data`
  directories remain distinct. Tests retain default pooling and parallelism;
  teardown must not clear other fixtures' pools. Missing roots are rejected.
  See [SQLITE_TEST_POOL_ISOLATION_ACCEPTANCE.md](ENGINEERING.md#sqlite-test-pool-isolation-acceptance) for cross-fixture evidence.

- Provider routing tests also link one bounded `Socks5TestHandshake` fixture from
  `tests/Shared`, checking protocol negotiation, CONNECT command, host and port.
  The real App configuration/discovery/streaming path covers HTTP and SOCKS5 for
  Responses, compatible Chat and Gemini, including edited endpoints, explicit
  direct routes and disabled-proxy rejection without fallback. This does not add
  another production proxy implementation or transfer test assembly dependencies.
  See [SOCKS5_PROVIDER_ACCEPTANCE.md](PROVIDERS.md#socks5-provider-acceptance) for native settings and loopback evidence.

- App's generic and non-generic guarded async commands share execution-state
  notifications, cancellation and disposal through the existing non-generic
  implementation. Typed entry points retain Toolkit's typed parameter validation
  without extra boxing; object entry points retain the existing null/type checks.
  Disabled, running and disposed commands still reject execution. Callback
  exceptions, owned execution tasks and per-editor operation deadlines are unchanged.

- App's project, pinned and recent task lists share task-row publication and
  selection helpers. Equal immutable snapshots retain their row instances even
  after position changes; changed snapshots receive fresh rows. Task and project
  publication share a remove/insert/move reconciler rather than resetting entire
  collections, including repeated references and independent duplicate task rows.
  Identical task snapshots avoid building row queues. Filtering, ordering and
  pinning remain owned by each list. See [SIDEBAR_RECONCILIATION_ACCEPTANCE.md](UI_WORKFLOWS.md#sidebar-reconciliation-acceptance).
  Recent tasks include non-pinned conversations from the current project as well
  as other projects; project/draft navigation does not refilter or replace those
  rows. Recent-task deduplication uses `MaxBy`, retaining the first snapshot on
  tied timestamps. This changes no persistence or execution policy. See
  [RECENT_SIDEBAR_ACCEPTANCE.md](UI_WORKFLOWS.md#recent-sidebar-acceptance) for current Codex reference and native evidence.

- App's selected-project task search publishes changed positions in one pass,
  retaining equal immutable snapshots without resetting the observable results.
  Sidebar search updates project subscriptions from collection deltas; moves
  preserve subscriptions, reset rebuilds them, and repeated references remain
  observed until their last removal. Ordering, matching, task metadata and owner
  disposal remain unchanged. No global collection semantics or execution policy
  changes. See [SEARCH_REFRESH_OPTIMIZATION_ACCEPTANCE.md](ENGINEERING.md#search-refresh-optimization-acceptance).

- App's task-owned message presentation lazily projects subagent dialogue. Repeated
  reads retain their row array and parsed Markdown; progress reuses unchanged
  role/content rows while changed rows receive independent snapshots. Raw tool
  payloads no longer own a presentation getter, so record equality, JIT JSON,
  persisted dialogue flags and task isolation are unaffected. No shared cache or
  appearance changes. See [SUBAGENT_DIALOGUE_PROJECTION_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#subagent-dialogue-projection-acceptance).

- App's streaming subagent dialogue creates one final array per progress event,
  without an intermediate list. Earlier snapshots remain untouched, and only a
  trailing partial reply from the same role is replaced. Sidebar activity sorting
  does not construct unused manual-rank dictionaries; project subscription insertion
  uses the existing set's result instead of a second lookup. No rendered geometry,
  task ownership or execution policy changes. See [UI_HOTPATH_OPTIMIZATION_ACCEPTANCE.md](ENGINEERING.md#ui-hotpath-optimization-acceptance).

- Agent's shared subagent context retains a bounded text tail while composing,
  instead of building full selected message bodies and then discarding the prefix.
  The last 12 relevant messages, role labels, historical-observation warning,
  newline order, 12,000-character limit and surrogate-safe cutoff stay unchanged.
  Native continuation metadata is still excluded. See [SUBAGENT_EXECUTION_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#subagent-execution-acceptance)
  for reference-equivalence and large-message allocation regression tests.

- App's project navigation publishes task summary and activity metadata from task
  mutations, not from search filtering. Project updates publish fallback activity
  only when an empty project's creation time changes. Search still updates rows,
  selection and visibility without triggering unrelated project sorts. Shared
  navigation styles combine identical state selectors without changing their
  setters. Native task-title baseline correction remains scoped to task text;
  hit areas and project-folder geometry stay unchanged. See
  [SIDEBAR_REFRESH_OPTIMIZATION_ACCEPTANCE.md](ENGINEERING.md#sidebar-refresh-optimization-acceptance) for regression and pixel evidence.

- Sidebar compose geometry belongs to App's shared vector/style resources.
  Its optical icon correction and icon-label spacing preserve the existing hit
  area and section rhythm, without changing global icon rendering or command
  routing. Native reference bounds and isolation/keyboard tests are recorded in
  [SIDEBAR_COMPOSE_ACCEPTANCE.md](UI_WORKFLOWS.md#sidebar-compose-acceptance).

- App's shared icon renderer accepts optional multilayer Drawing resources,
  reusing its bounded uniform-scale/center layout. Geometry-only icons retain
  their existing paint path. File-preview copy/image glyphs use optical variants
  without reducing button hit areas; the Explorer badge is a shared vector, not
  a separate file-opening implementation. Media drawings are allowed only inside
  resource owners by the visual-component gate; native controls remain rejected.
  See [PREVIEW_ICON_ACCEPTANCE.md](UI_WORKFLOWS.md#preview-icon-acceptance) for Skia pixels and ordinary native evidence.

- App's chat picker, agent/subagent editors, and automation pages read enabled
  models through one `EnabledModelCatalog` using `IConfigurationRepository`.
  The reader checks provider ownership and cancellation; each consumer retains
  its own draft, selection, timeout and stale-result guards. Agent editors share
  `AgentModelOption` rather than maintaining duplicate option types.
  Each agent/subagent editor owns an `AgentModelSelection` instance for preferred
  provider/model identity and catalog publication. Equal catalogs retain their
  object references; unavailable choices never fall back to another model, and
  returning choices restore by both identities. Settings selection remains
  in-memory; conversation-specific choices have separate persistence. Reload version,
  cancellation, disposal and draft-revision checks remain in the owning editor.

- App's `AppModelPickerMenu` owns the quick/list composition and keyboard entry;
  `ModelPickerFlyoutView` is only its shared-component host. Existing picker
  bindings, commands and appearance variants remain unchanged. Deferred focus
  targets the search field, selected model row or quick navigation action only
  for the current attached, visible, enabled and open picker. Context replacement,
  closing and detachment invalidate queued transfers and release subscriptions.
  Automatic focus uses programmatic navigation; real Tab navigation retains its
  focus adorners. Markup and analyzer gates protect content, templates and geometry.
  No model I/O, persistence or tool pipeline moves into the component. See
  [MODEL_SELECTION_ACCEPTANCE.md](PROVIDERS.md#model-selection-acceptance) for native input and regression evidence.

- App's shared settings dialog owns attachment-scoped ancestor visibility and
  enabled-state subscriptions. Returning to a retained settings page restores an
  available last dialog field without changing drafts or TextBox selection policy;
  hidden, closed and detached owners invalidate queued transfers. A new open
  session resets only its template-owned body before visibility and autofocus,
  keeping the first field's label visible; retained open pages and nested scrollers
  are not reset. Shared appearance and modal scope stay unchanged. See
  [PROVIDER_DIALOG_NATIVE_ACCEPTANCE.md](PROVIDERS.md#provider-dialog-native-acceptance) and [SETTINGS_DIALOG_REOPEN_ACCEPTANCE.md](UI_WORKFLOWS.md#settings-dialog-reopen-acceptance).

- App's proxy, browser, agent/subagent editors and task mutations share
  `SettingsOperationLifetime` for per-owner cancellation and operation deadlines.
  The model picker reuses the same component instead of maintaining another
  linked-token/deadline implementation; each reload still cancels its predecessor
  and rejects stale results. Cancellation or disposal never cancels another picker.
  Each owner keeps its own lifetime; caller cancellation never cancels sibling
  operations, and writes retain ownership until storage returns.
  The owner token remains stable, readable and cancelled after disposal; consumers
  reuse it without maintaining another cached lifetime token.
  The model picker retains immutable reasoning steps per selected snapshot
  instead of allocating a fresh list on every binding read.
  Its provider groups also keep immutable membership snapshots while rows retain
  observable selection. Whole-group search matches reuse the current group, and
  partial matches share rows without intermediate list copies. Each source group
  retains at most one immutable partial result; equal searches reuse it. Filtered
  groups use the existing collection synchronizer, avoiding list resets and
  preserving rendered row identity. Catalog reloads create new source groups, so
  retained search results cannot resurrect removed or disabled models. Explicit and Agent
  selections share one guarded current-row lookup, retaining their distinct event
  semantics; availability binding reads need no LINQ iterators or closures. See
  [MODEL_PICKER_OPTIMIZATION_ACCEPTANCE.md](PROVIDERS.md#model-picker-optimization-acceptance) for focused regression evidence.

- Application's `ModelReasoningPolicy` exposes stable immutable effort snapshots
  without per-query boxing, lowercasing model IDs or concatenating family suffixes.
  Default effort selection resolves capabilities once. App's model settings reuse
  one catalog refresh/notification sequence after reads and mutations, and resolve
  current editable rows once while preserving draft, lifetime and failure guards.

- Application owns an immutable, embedded `Data/model-capabilities.json` snapshot
  and exact, protocol-aware capability lookup. New model imports fill context,
  output, image/PDF flags and recommended effort from this offline catalog;
  existing rows and unknown aliases are never rewritten or guessed. App's shared
  `AppEditableChoice` supports selection and typing across model/agent editors.
  Compatible protocols allow bounded custom effort tokens; native protocol
  mappings retain their restrictions. The maintenance-only download script uses
  public metadata without provider credentials; discovery adds no extra network
  destination. See [MODEL_CAPABILITY_DATA.md](../MODEL_CAPABILITY_DATA.md) for provenance and limitations.

- Tools.Abstractions owns optional, metadata-only pipeline phase notifications;
  Tools.BuiltIn emits them without altering validation, approval, timeout or audit
  order. Agent forwards task/call-scoped phases through a bounded stream alongside
  subagent dialogue and retains execution ownership until cancellation/cleanup return.
  App updates one existing tool row from request checking through approval, execution
  and final result. Phase notifications are transient and contain no arguments, output
  or credentials; only existing final tool events are persisted. Observer failures must
  not approve, reject or otherwise change tool execution. See [TOOL_PROGRESS_ACCEPTANCE.md](TERMINAL_TOOLS.md#tool-progress-acceptance).

- Tools.BuiltIn's executor owns a per-call deadline using the same `TimeProvider`
  for its timer and monotonic elapsed-time checks. It rechecks cancellation and
  elapsed time before invocation and after the owned tool returns, including when
  cancellation callbacks were delayed or a tool ignored its token. Late results
  retain bounded dialogue and created-artifact metadata and complete the audit as
  interrupted, never as success. Caller cancellation takes precedence over timeout.
  Started work remains owned until return; this does not undo side effects,
  preempt native calls or move
  the deadline ahead of approval. Windows tests use explicit deadline/gate signals
  and publish process readiness atomically rather than racing partial PID writes.
  Its separate output limiter shares one character budget across summaries,
  content and dialogue. Unchanged results and retained messages are reused without
  cloning; truncation preserves artifact metadata and Unicode surrogate pairs.
  Budget checks stop at the limit rather than summing an unbounded dialogue into
  a potentially overflowing integer. Audit completion still runs for reused results.

- App's file-preview tabs reuse the standard preview-file tool for original paths
  and Application's task-artifact reader port for already-audited snapshots.
  Tools.BuiltIn owns authorized, bounded snapshot creation; Infrastructure verifies
  task directory, links, length, digest and content type. The UI decodes memory only.
  Task/scope changes and tab closure cancel reads and reject late results. Shared
  file/code components own bounded tab geometry, image fitting, syntax, diff colors
  and gutters, protected by markup/analyzer gates. See [FILE_PREVIEW_ACCEPTANCE.md](UI_WORKFLOWS.md#file-preview-acceptance)
  for format/resource limits and native verification boundaries.
  Each tab also owns bounded rendered-snapshot search and image zoom state.
  Shared code/image controls own highlighting, scrolling and fit-on-resize geometry;
  search/zoom never reread files or invoke a separate execution path.
  Code preview owns a per-instance gutter transform over its existing single
  scroller. Horizontal scrolling moves code while line numbers and diff markers
  stay fixed; the opaque gutter shares its row's semantic/search surface and
  blocks hits on code behind it. Vertical layout remains one row tree, avoiding
  independently scrolling or mismatched line lists. Search navigation preserves
  horizontal position; content replacement resets scroll state. See
  [CODE_PREVIEW_GUTTER_ACCEPTANCE.md](UI_WORKFLOWS.md#code-preview-gutter-acceptance) for native pixels and remaining fidelity limits.
  Its clipboard controller copies only authorized in-memory snapshots/paths and
  rejects late notices after ownership changes. Showing an original file in Windows
  Explorer uses a task-scoped tool through the existing pipeline, with repeated path
  validation and an owned STA Shell operation; it never launches the file itself.
  Started clipboard/Shell operations cannot be revoked and remain owned until return.
  The shared breadcrumb prioritizes the measured filename over parent segments.
  Compact file tabs reserve their close-action slot without showing inactive chrome.
  File-tab close glyphs use a shared six-DIP, one-DIP-stroke muted variant while
  retaining the 24-DIP action slot and existing hover/focus/command behavior.
  Image zoom is an owned flyout, closed on context, tab, visibility, enablement or
  attachment changes; default preview no longer allocates a zoom strip or empty
  notice row. Search and zoom continue using the existing in-memory snapshot.
  The shared preview switches to a compact toolbar below 400 DIP, retaining copy
  and search/zoom while collecting the existing reveal/refresh commands in an
  owned menu. Fixed columns avoid reentrant grid-definition changes and unused
  trailing spacing. Shared styles own the isotropic surface shadow, 14-DIP action
  icons and eight-DIP image clipping; no business view supplies these dimensions.
  See [FILE_PREVIEW_FIDELITY_ACCEPTANCE.md](UI_WORKFLOWS.md#file-preview-fidelity-acceptance) for pixel evidence and remaining gaps.
  File selection has separate, task-owned coordination with a five-minute human
  browsing deadline; it supplies paths only and reuses the same authorized preview
  pipeline with its existing 30-second read timeout. Noncooperative pickers retain
  ownership until return and cannot populate a new task with a late selection.
  Shared workspace panes own inspector expansion without destroying the primary
  content or losing preferred splitter width. The conversation header can open
  the inspector directly in full view; the ordinary inspector and preview headers
  share its restore command. Opening uses the existing two-way, conversation-owned
  panel state. Returning from preview to sources, hidden/disabled owners and
  detachment restore normal layout; active tabs remain visible after resizing.
  Shared headers refresh derived state only for properties they consume; the
  inspector reuses its instance-owned tab list and existing command parameters
  for selection, rather than allocating a new list on every layout notification.
  See [FILE_PREVIEW_WORKFLOW_ACCEPTANCE.md](UI_WORKFLOWS.md#file-preview-workflow-acceptance) for interaction and safety evidence.

- Application's automation scheduler uses repository, runner, lease and time-provider
  ports. The App lifecycle explicitly starts it after startup and cancels/awaits it
  before workspace shutdown. Infrastructure owns the exclusive data-directory lease
  and transactional version-nine migration. Revision checks and durable per-plan
  claims prevent overlapping runs and stale edits; interrupted runs are not replayed.
  App's background runner resolves the exact configured project/provider/model and
  reuses task-scoped agent execution without consuming the current draft or selection.
  Plans default to paused/read-only and only run while the application is open.
  Scheduler shutdown rejects new work and cancels owned/queued port operations
  before waiting for them, avoiding a cancellation-versus-gate deadlock. Concurrent
  stop/dispose calls share completion. Noncooperative ports, started execution and
  terminal persistence keep the lease until return; late durable claims are cancelled
  without invoking the runner. Failed finalization retains the durable claim and
  reports only the safe persistence error code rather than replaying uncertain work.
  App's separate history view/model reads at most 50 records through existing ports,
  distinguishes loading/failure/empty states and maps failure hints to localized text
  instead of echoing stored error codes. Selection, page visibility, mutation blocking
  and catalog locking invalidate late navigation; pending noncooperative reads retain
  opening ownership until returning. Shared thin-scrollbar variants reserve content
  gutters. This changes no schedule schema, execution policy or model routing.
  See [AUTOMATIONS_INSPECTOR_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#automations-inspector-acceptance) for safety, tests and native visual limits.
  App's separate plan-list model coalesces reads, preserves the last successful
  snapshot on failure and distinguishes loading, failure, refreshing and empty states.
  Scheduler notifications request a catch-up read after the current operation;
  the entire chain shares a 30-second cooperative deadline. Noncooperative ports
  retain read ownership until returning; disposal cancels and rejects late results.
  Current snapshot reference identity gates row commands, while failed/loading reads
  block mutations and history navigation, never Stop. Refresh preserves newer
  selection and revalidates pending removal targets; retry retains editor drafts.
  Shared notice/empty-state controls use localized safe errors without another
  execution path or schema change. See [AUTOMATION_LIST_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#automation-list-acceptance).
  That record also distinguishes actual production loopback schedule execution
  from read-only restoration on later UI builds; restoring terminal runs never
  establishes that the newer UI binary executed them again.
  Plan editing and mutations use App-owned operation state and guarded commands.
  Catalog/write work has linked command/lifetime cancellation and a 30-second
  cooperative deadline; read-back retains the busy owner and uses the list's own
  deadline. Noncooperative ports keep ownership until return, and cancellation
  never claims to roll back a write or stop a run that already started. Editor
  context/draft revisions reject late publication. A successful save records its
  committed identity before read-back, preventing duplicate creation on retry;
  current stored revision/content must match before rebasing or closing a draft.
  Shared operation notices expose progress/cancel and the editor's enabled binding
  explicitly reads the owning dialog model. See [AUTOMATION_MUTATION_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#automation-mutation-acceptance).
  Editor initialization calculates its schedule preview once after loading fields,
  retaining individual field notifications and draft/context revisions. History
  rebuilds use one bounded projection after the selection reset, without a second
  reset or layered LINQ iterators. See [AUTOMATION_LIST_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#automation-list-acceptance).
- App's shared appearance previews own theme-scene geometry and semantic colors.
  Theme buttons keep their existing preference commands. The read-only synthetic
  diff uses two independent shared horizontal scrollers, never project file reads
  or an execution path. Preview-specific theme resources do not override the code
  inspector. See [SETTINGS_PREVIEW_FIDELITY_ACCEPTANCE.md](UI_WORKFLOWS.md#settings-preview-fidelity-acceptance) for native pixels and limits.
- The read-only appearance diff keeps native line/page/thumb scrolling, with
  shared filled-arrow buttons and a six-DIP thumb. Its real text determines
  horizontal extent; there is no forced minimum causing blank scrolling.
  Zero-range bars retain visible muted arrows but disable interaction and hide
  the thumb. Preview-only syntax resources live in ThemePreviewVariants and do
  not replace the inspector's CodeKeyword/CodeType palette. See
  [PREVIEW_SCROLL_SYNTAX_ACCEPTANCE.md](UI_WORKFLOWS.md#preview-scroll-syntax-acceptance) for reference-crop and native evidence.
- App's shared inspector header and empty state own tab/action geometry, localized
  labels, selected/disabled states and empty-content presentation. Diff, audit and
  terminal view models reject late results after task/selection changes and dispose
  subscriptions. Terminal start/write/stop use the standard tool executor and captured
  task context, not the platform session manager directly. Missing browser views show
  an empty state rather than an unusable toolbar. Markup and analyzer gates continue
  to reject native visual controls and appearance/composition replacement.
  Platform.Windows registers each terminal owner before asynchronous ConPTY creation.
  Repeated starts share creation; stop/disposal reject new work and retain late
  creation, connection cleanup and in-flight writes until return. Porta.Pty's owned
  Job handles process-tree lifetime; connection closure keeps output draining.
  Stateful UTF-8 decoding preserves arbitrary pipe boundaries and isolated observers
  cannot terminate the reader. See [TERMINAL_LIFECYCLE_ACCEPTANCE.md](TERMINAL_TOOLS.md#terminal-lifecycle-acceptance) for real process
  evidence and remaining terminal rendering, backpressure and native shutdown limits.
  App's shared terminal pane owns localized pending/cancelling/refusal states and
  preserves a separate pipeline operation owner per task until return. Switching
  tasks cancels old scopes without blocking independent tasks or accepting old results.
  Its locked output projection coalesces one pending dispatcher callback and caps both
  queued and displayed text at 200,000 UTF-16 units, with explicit truncation notices.
  Shared variants own geometry and theme-aware disabled danger actions; markup and
  analyzer gates reject terminal composition replacement. See [TERMINAL_PANE_ACCEPTANCE.md](TERMINAL_TOOLS.md#terminal-pane-acceptance)
  for native component evidence and remaining VT/full-workspace limits.
  Tools.Abstractions additionally defines an optional metadata-only terminal state source.
  Windows publishes revisioned, session-owned snapshots and retains natural termination
  until output delivery and connection/Job cleanup return. Completed metadata is capped;
  cleanup failures retain ownership and block replacement. App coalesces state callbacks,
  revalidates snapshots before tool admission and keeps operation notices separate from
  session state. Shared empty-state content uses bounded available width so title changes
  cannot remain clipped to a prior intrinsic width. See [TERMINAL_STATE_ACCEPTANCE.md](TERMINAL_TOOLS.md#terminal-state-acceptance)
  for native natural-exit evidence, reproduced title clipping and verification limits.
  App additionally parses the untruncated producer stream through a managed VT core.
  Only explicitly admitted task scopes are cached (eight screens, 128 history rows
  each); session-tagged output, scope identity and revisions reject stale data.
  Opaque host/graphics strings are discarded without accumulating their payloads.
  A single background projection worker coalesces immutable grid frames, and shared
  controls draw only visible cells with a bounded shaping cache. Frame publication
  revalidates screen/session ownership; task switching and alternate-screen changes
  restore separate scroll positions. Reserved thin scrollbar gutters keep the last
  row and cursor visible. The older 200,000-unit text projection remains diagnostic,
  not the render/parser input. No terminal responses or host callbacks bypass tools.
  Revisioned terminal metadata now carries successfully applied grid dimensions.
  Session-bound write/resize ports validate task, session and canonical working
  directory; both tools check authorized paths before approval and again on execution.
  Resize is side-effecting and retains native ownership through timeout/stop cleanup.
  App coalesces measured dimensions through the same tool pipeline, preserving VT
  content and bounding primary/alternate cell caches under variable geometry.
  Shared viewport events route text and special keys through a separate, bounded,
  task-owned input coordinator. Classic/application cursor and Win32 input modes
  follow the immutable frame; rejected/cancelled input is discarded, never replayed.
  Noncooperative writes retain ownership and visibly block same-task replacement;
  independent tasks remain usable. PowerShell starts with explicit UTF-8 console
  encodings so combining/supplementary output does not degrade to the system code page.
  Fixed shared toolbar height avoids busy-action/measurement feedback. App's terminal
  selection copies bounded immutable rendered cells, including graphemes, wide cells,
  history and soft-wrap metadata; changed selected rows or owner/grid changes clear it.
  Explicit user clipboard gestures reuse Avalonia's API, never expose a model clipboard
  port, and retain noncooperative operations until return. Paste rejects embedded control
  sequences, confirms newlines, captures scope/session/modes and enters the same input
  pipeline as an exclusive, bounded operation. Win32 paste sends press/release records;
  VT envelope characters are not allowed to escape from clipboard content. Shared menus,
  notices and selection brushes own presentation. The viewport caches its owned screen
  and scroller after markup loading, instead of repeating namescope lookups on every
  input/layout event. Scroll restoration and layout publish candidate geometry once
  per path, using one measured visible rectangle rather than repeated translations.
  Avalonia input-method clients present bounded, underlined preedit locally and expose
  viewport-clamped candidate geometry without surrounding terminal text. Only committed
  TextInput reaches the existing input pipeline; owner/scope/command changes and lost
  eligibility invalidate old clients. VT responses and physical Windows IME acceptance
  remain separate. See [TERMINAL_IME_ACCEPTANCE.md](TERMINAL_TOOLS.md#terminal-ime-acceptance), [TERMINAL_CLIPBOARD_ACCEPTANCE.md](TERMINAL_TOOLS.md#terminal-clipboard-acceptance),
  [TERMINAL_VT_ACCEPTANCE.md](TERMINAL_TOOLS.md#terminal-vt-acceptance) and
  [TERMINAL_INTERACTION_ACCEPTANCE.md](TERMINAL_TOOLS.md#terminal-interaction-acceptance) for evidence
  and native/full-workspace verification boundaries.
  Diff review reuses the standard status/diff tools, lists individual untracked
  files and explicitly requests single-file no-index patches without staging them.
  Git uses literal paths, readable Unicode headers and disabled external/textconv
  helpers; command-local CRLF warning suppression keeps valid read-only patches
  successful while real stderr errors remain failures. Refresh/filter changes retain
  snapshot-owned selection where listed, rebind equal records and update counts.
  Localized loading/failure/empty messages stay outside code content. See
  [DIFF_REVIEW_ACCEPTANCE.md](GIT_WORKTREES.md#diff-review-acceptance) for real Git/native evidence and remaining fidelity gaps.
  Audit rows use a shared semantic component with local-time/elapsed metadata and
  allowlisted localized failure hints instead of raw stored diagnostic codes.
  Refresh preserves the current snapshot-owned selection, including newer user
  choices made during a pending read, and rebinds equal-valued record replacements.
  Visible history is task filtered/deduplicated/capped at 200; artifacts additionally
  require execution ownership and display at most 50. These are UI limits, not a
  bounded artifact repository query. Snapshot opening reuses the existing preview
  reader and leaves the task draft unchanged. See [AUDIT_INSPECTOR_ACCEPTANCE.md](TERMINAL_TOOLS.md#audit-inspector-acceptance)
  for negative tests, native interaction evidence and remaining Codex/DPI gaps.

- App's shared `AppText` computes opt-in single-line overflow during arrangement;
  navigation styles apply a fade only to overflowing task titles. Fixed task line
  height keeps mixed-script labels centered with their actions without replacing
  user font preferences or moving project/group anchors. No port, tool policy or
  persistence change; see [SIDEBAR_TASK_TEXT_ACCEPTANCE.md](UI_WORKFLOWS.md#sidebar-task-text-acceptance) for raw-pixel evidence
  and remaining typography/DPI differences.
- App shared `AppNavigationRail` owns the left rail's actions, localized names,
  measured geometry and flyout lifetime. Views supply existing commands and help
  business content. Missing commands/content disable entries, CanExecute remains
  authoritative, and ownership changes, hiding, disabling or detachment close menus.
  Shared styles and markup/analyzer guards protect composition and appearance.
  Task activity animation is isolated in its own icon theme so its initialization
  cannot replace unrelated icons' transforms. No port, persistence or tool-policy
  changes; see [NAVIGATION_RAIL_COMPONENT_ACCEPTANCE.md](UI_COMPONENTS.md#navigation-rail-component-acceptance) for evidence and limits.
- Serialization stays in its owning module and uses ordinary runtime JSON metadata.
  Model tool payloads, MCP envelopes, persisted header/root collections and browser-tab
  metadata retain their contracts. App components and AXAML use ordinary runtime
  bindings through shared visual controls. No persistence migration, port, authorization
  or tool-policy change is required.
- Desktop builds and runs use managed/JIT mode. The Windows x64 publish profile creates
  a self-contained, compressed single-file package without trimming, bundling the .NET
  runtime, native libraries and Windows Playwright driver. Publishing stages and validates
  the executable and archive before replacement and preserves previous packages on
  failure. Runtime dependencies are extracted to the .NET bundle cache; user data stays
  in the separate application data directory. See the README for publishing and cache use.
- Browser's Playwright factory retains ownership across SDK initialization, launch,
  CDP connection and page creation until each non-cancellable SDK operation returns.
  Cancellation rejects handoff and awaits cleanup of owned contexts or newly created
  pages, while borrowed CDP contexts and existing pages remain untouched. Runtime
  disposal follows cleanup even if owned context closure fails. This reuses existing
  browser ports and tool policy, with no persistence migration; native evidence and
  cancellation limits are recorded in [BROWSER_EXECUTION_ACCEPTANCE.md](BROWSER_MCP.md#browser-execution-acceptance).
- Browser settings coalesce proxy-catalog notifications while reads or writes own
  the editor. Accepted operations await queued refreshes before returning; save also
  drains notifications received during its pre-write catalog check. Pending refreshes
  block new saves, preserve dirty browser/profile fields and unavailable proxy identity,
  and keep failed refreshes unavailable until recovery. Lifetime cancellation rejects
  late results, and noncooperative writes retain ownership until storage returns.
  Disposal discards queued work. This reuses existing ports and changes no tool policy,
  browser session routing or persistence schema.

- App shared `AppResponseContent` and `AppResponseTable` own main/child response
  block templates, table geometry and bounded code/table scrolling. Views provide
  parsed blocks, table data and the existing clipboard command; the compact child
  variant belongs to shared styles. Literal tool/status/task content stays unchanged.
  Semantic paragraph spacing is applied to the shared text-block root, not an
  unreachable wrapper-type selector on generated presenters. Inline code remains
  selectable native runs with shared typography and theme-aware backgrounds; style
  updates preserve selection and stop tracking replaced runs. Markdown rendering
  enumerates paragraphs without an intermediate array and reuses container creation
  for links. All shared selectable outputs reuse the native SelectableTextBlock theme
  without changing concrete style keys. Inline-code rectangles are drawn from UTF-16
  text-layout ranges before native selection and glyphs, so rounded backgrounds do
  not cover the selection or change measurement/copy text. Markup and analyzer rules
  protect code-font, background geometry and selection tokens. Shared composed-surface
  ownership is checked once per rule instead of maintaining three duplicate type lists.
  See [RESPONSE_CONTENT_FIDELITY_ACCEPTANCE.md](UI_WORKFLOWS.md#response-content-fidelity-acceptance) for pixel evidence and remaining gaps.
  Missing commands disable code copy, while command CanExecute and keyboard behavior
  remain authoritative. Derived display blocks are excluded from serialized tool
  payloads. Markup guards and `MOSSUI002` reject business composition and geometry
  replacement. No application port, tool policy, model routing or migration changes;
  see the 2026-10-07 section of [SUBAGENT_EXECUTION_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#subagent-execution-acceptance).

- Numeric fields use App's shared `NumericFieldVariants.axaml` and `AppRepeatButton`,
  retaining NumericUpDown parsing, repeat, keyboard and range handling. The component
  owns centered small arrow geometry, hit areas, shared theme brushes and localized
  accessible names. `ValidateSharedRepeatButtons` and `MOSSUI002` reject business
  geometry overrides, including native owner-qualified property elements. No port,
  tool policy or persistence change; see [NUMERIC_FIELD_COMPONENT_ACCEPTANCE.md](UI_COMPONENTS.md#numeric-field-component-acceptance).
- Proxy settings mutations reuse App's `SettingsEditorViewModel`,
  `SettingsMutationRunner` and guarded commands through `IConfigurationRepository`.
  Captured identity/revision protect newer drafts; dirty fields survive reloads and
  removed targets cannot be recreated. Mutation ownership includes reload and catalog
  notification. Lifetime/command cancellation and a 30-second cooperative deadline
  reach storage; non-cooperative writes keep ownership until returning. Safe localized
  errors and existing shared status variants distinguish storage, reload and
  post-commit notification failure. No migration or tool-policy change; see
  [PROXY_EDITOR_MUTATION_ACCEPTANCE.md](PROVIDERS.md#proxy-editor-mutation-acceptance) for rejection and native persistence evidence.

- App shared `AppConversationHeader` owns conversation heading/status allocation,
  action geometry and menu lifetime. Views supply heading/menu business content,
  semantic state and existing commands; owned hosts inherit the business DataContext.
  Long startup warnings and running activity ellipsize inside finite columns instead
  of displacing actions. Missing targets or commands disable entries, CanExecute
  remains authoritative, and ownership changes or detach close the menu. The chat
  inspector entry is hidden while docked; the inspector's existing close action
  remains available. Shared styles, markup guards and `MOSSUI002` own appearance
  and composition. No port, tool policy or persistence change; see
  [CONVERSATION_HEADER_COMPONENT_ACCEPTANCE.md](UI_COMPONENTS.md#conversation-header-component-acceptance) for failed baselines and native limits.

- App shared `AppConversationTab` owns tab selection/options geometry, localized menu
  labels, explicit command targets and popup lifetime. Views supply semantic state and
  existing commands; popup bindings do not depend on a Window view model. Missing
  targets/commands and CanExecute disable entries. Scope changes, hiding, disabling
  and detachment close the popup and retire subscriptions. Menu dismissal runs after
  button command execution and rejects obsolete callbacks after reopening. The owned
  menu focuses its action for keyboard use. Workspace disposal also retires its tab
  model's catalog subscriptions and commands. Markup/analyzer gates protect composition
  and geometry. Closing a tab never archives/deletes its conversation. This changes
  no port, tool policy or persistence; see [CONVERSATION_TAB_COMPONENT_ACCEPTANCE.md](UI_COMPONENTS.md#conversation-tab-component-acceptance).

- Workspace agent selection belongs to App's `WorkspaceAgentPickerViewModel`,
  through existing configuration repository ports and the shared model picker.
  It preserves the preferred identity when an agent or its model becomes unavailable,
  resolves stale menu targets against the current snapshot, and distinguishes explicit
  model choices from catalog refreshes. Loading or failed reads block new sends,
  never the running task's Stop action; captured requests remain unchanged.
  Linked cancellation, a 30-second read deadline and generation checks reject late
  publication. Shared composer controls present safe notices and a bounded agent menu.
  No migration or tool-policy change; see [WORKSPACE_AGENT_SELECTION_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#workspace-agent-selection-acceptance).

- App's `WorkspaceConversationPreferences` owns task-specific model, reasoning and
  preferred Agent identities through Application's `IConversationPreferenceRepository`.
  Infrastructure's transactional version-eleven migration adds a separate SQLite
  table without changing task metadata or conversation history; provider/model/Agent
  deletion never cascades away a preference. Only identities, canonical effort values
  and the Agent-default source flag are stored, never credentials or message content.
  Missing choices remain visible and block sending until explicitly repaired or
  replaced. Agent defaults track current configuration; explicit reasoning overrides
  survive refresh, task switching and restart. A retained identity needs no fabricated
  Agent snapshot when its configuration is absent. Model/Agent/conversation reads
  share the existing per-owner deadline component; generations reject stale reads.
  Writes are serialized and coalesced per task, and normal shutdown drains owned
  writes. Noncooperative writes keep ownership until storage returns. Reads fail closed
  with localized notices; write failure retains the in-memory choice for explicit retry.
  `WorkspaceBackgroundRunCoordinator` owns the extracted scheduled-run startup and
  captures its exact model preference without changing execution or approval policies.
  See [CONVERSATION_PREFERENCE_ACCEPTANCE.md](CONVERSATIONS.md#conversation-preference-acceptance) for regression and restart evidence.

- App shared `AppComposerContextBar` owns the composer's project, branch and agent
  presentation, bounded adaptive columns and flyout lifetime. Views bind semantic
  data and existing commands; the project picker inherits business DataContext.
  Late agent loading changes existing column definitions instead of replacing the
  collection, keeping Avalonia's measurement subscriptions current. Missing data,
  picker content or commands disable entries; command CanExecute remains authoritative.
  Shared styles own icon, spacing, surface and menu geometry, while markup guards
  and `MOSSUI002` reject composition and dimension overrides. This changes no port,
  tool policy, persistence or provider request. See [COMPOSER_CONTEXT_COMPONENT_ACCEPTANCE.md](UI_COMPONENTS.md#composer-context-component-acceptance)
  for reproduced overflow, rejected-path tests and ordinary-permission native evidence.

- App shared `AppComposerInput` owns focused Enter/Ctrl+Enter submission and leaves
  Shift+Enter to TextBox newline editing. It preserves the existing input template
  and IME client, guards active preedit and held-key repeats, and invokes only the
  bound existing command. The composer disables this path during preparation/run;
  Stop remains an explicit button rather than a chat-wide keyboard side effect.
  Hidden, disabled, readonly, detached, unfocused, blank or command-ineligible inputs
  cannot submit. No tool, approval, provider or persistence contract changes; see
  [COMPOSER_KEYBOARD_ACCEPTANCE.md](CONVERSATIONS.md#composer-keyboard-acceptance) for loopback, native and IME validation limits.

- Project editing uses a dedicated bounded flyout view with a scrolling directory
  body and fixed field/action regions. Shared variants own its appearance;
  keyboard focus reveals row actions and remains inside the editor. The sidebar
  create trigger explicitly resets edit identity. Busy/disposed command guards
  preserve an in-flight draft, while close retains unsaved input. Composition
  exposes the existing service registrations for deterministic port substitution
  without changing production defaults. See [PROJECT_EDITOR_VIEWPORT_ACCEPTANCE.md](UI_WORKFLOWS.md#project-editor-viewport-acceptance).

- Project editor mutations capture the project identity and draft revision before
  storage. A completed older snapshot still refreshes the catalog, but cannot clear
  a newer draft or another editor context. App-owned `GuardedAsyncRelayCommand`
  checks CanExecute before both invocation paths, preventing a rejected reentry
  from cancelling the existing operation inside the toolkit. Editor lifetime and
  command cancellation reach the existing workspace/folder-picker ports; saves
  request cancellation after 30 seconds. Owner disposal rejects late publication
  and detaches subscriptions. Validation uses allowlisted failure categories and
  localized hints; storage/picker exceptions never appear verbatim in the UI.
  No migration or tool-authorization change; see [PROJECT_MANAGEMENT_ACCEPTANCE.md](UI_WORKFLOWS.md#project-management-acceptance).
- App shared `AppProjectNavigationRow` owns the sidebar project header, bounded
  label, expanded folder state, independent select/expand actions and summary
  flyout lifetime. Views supply semantic data, existing commands and summary
  business content; the owned content host forwards the business DataContext.
  Shared variants own dedicated project-folder geometry, optical alignment,
  stroke and spacing without changing global folder icons. Markup guards and `MOSSUI002`
  reject content, padding, dimensions and alignment overrides. Missing targets or
  commands disable actions; CanExecute remains authoritative. Target, DataContext,
  command or content replacement, unavailable ancestors, command rejection and
  detachment close the summary and reset drag ownership. Tree subscriptions are
  removed on detachment. No application port, persistence or tool-policy change;
  see [PROJECT_ROW_COMPONENT_ACCEPTANCE.md](UI_COMPONENTS.md#project-row-component-acceptance) for regressions, pixels and native limits.
- App's sidebar uses one navigation viewport for pinned tasks, project groups and
  recent tasks. `NavigationActionVariants.axaml` owns group rhythm, list insets,
  title fading, the light rounded scroll thumb and action/status allocation.
  The header uses the existing pinned-list availability to lift only the pinned
  section title while preserving task/project positions and the no-pin layout;
  see [SIDEBAR_HEADING_ACCEPTANCE.md](UI_WORKFLOWS.md#sidebar-heading-acceptance) for native and complete-window evidence.
  `AppTaskNavigationRow` reserves its trailing action slot only while running,
  hovered, focused or showing its menu, and closes menus on target replacement
  or detachment. The activity marker derives from each task snapshot; selection
  does not imply execution. Brush and animation value objects are not visual
  controls; native buttons, text, shapes and scroll viewers remain forbidden.
  Existing catalog commands, authorization, persistence and JIT packaging are
  unchanged. See [SIDEBAR_FIDELITY_ACCEPTANCE.md](UI_WORKFLOWS.md#sidebar-fidelity-acceptance) for validation and remaining gaps.
- The sidebar's shared scroll variant cancels Fluent's inherited panning-thumb
  transform and owns a six-DIP rounded visual inside a twelve-DIP hit area.
  Its viewport extends to the sidebar edge while matching content padding keeps
  row widths unchanged. Line-arrow chrome is hidden; page actions, wheel input
  and thumb dragging still use the framework's existing scrolling behavior.
  Dedicated theme brushes do not change global separators or other scroll viewers.
  No port, persistence, authorization or packaging change is involved. Headless
  checks cover transformed bounds, hover/pressed chrome, disabled input, fitting
  content and drag scrolling; native pixel evidence and limits are in
  [SIDEBAR_FIDELITY_ACCEPTANCE.md](UI_WORKFLOWS.md#sidebar-fidelity-acceptance).
- Content, thin navigation lists and terminal viewports share
  `ScrollBarVariants.axaml` for transparent tracks, six-DIP rounded visuals and
  twelve-DIP hit areas. Feature styles retain only palette and behavioral
  differences; diff previews keep their sixteen-DIP horizontal bar and arrow
  buttons. Existing template parts still own scrolling, auto-hide and disabled
  input; settings-page scrollbar visibility remains unchanged. Production-window
  comparison and regression limits are in [CONTENT_SCROLLBAR_ACCEPTANCE.md](UI_WORKFLOWS.md#content-scrollbar-acceptance).
- App's shared project row owns thresholded native dragging and themed insertion
  feedback. An in-process payload is limited to one sidebar's reorder scope and
  current project identities; external data and stale/disabled targets are rejected.
  The shared navigation viewport owns edge scrolling, stopping on drop, invalidation
  and detach without treating an internal child transition as a viewport exit.
  Hidden or disabled rows, viewports and ancestors reject late dragging and stop
  timer scrolling; drag events and timer ticks share the same availability checks.
  Reordering uses the existing sidebar-preference port, guarded command ownership
  and atomic persistence, preserving selection and expansion without changing the
  catalog source collection. Current source/target identities resolve in one pass;
  unchanged insertion positions return before building an ID order or saving.
  No tool policy, migration or packaging change is involved. See
  [SIDEBAR_PROJECT_DRAG_ACCEPTANCE.md](UI_WORKFLOWS.md#sidebar-project-drag-acceptance) for native input and restart evidence.
- App shared `AppSidebarSearch` owns the search trigger, flat input, clear action,
  empty-result notice and popup lifetime. `WorkspaceSidebarSearchViewModel` adapts
  the existing catalog search and aggregates project, pinned and recent results,
  rather than treating the selected project's task list as the entire catalog.
  Ctrl+K only intercepts a visible, enabled, attached sidebar; Escape keeps the
  filter. Generation checks reject queued focus after closure, context replacement
  or detach, and guarded restoration does not steal another editor's focus.
  Missing or unavailable clear commands remain disabled; disposed presentation
  state rejects late mutations and removes catalog subscriptions. Shared styles,
  markup guards and `MOSSUI002` own composition and geometry. No repository port,
  tool policy, migration, model request or JIT packaging change is involved; see
  [SIDEBAR_SEARCH_COMPONENT_ACCEPTANCE.md](UI_COMPONENTS.md#sidebar-search-component-acceptance) for native evidence and fidelity limits.
  Header bell/search geometry is supplied by semantic shared variants, scoped to
  direct header glyphs so search popup clear actions retain their own appearance.
  The general icon resources and 32-DIP header hit areas remain unchanged. Markup
  tests accept only unconditional typed variants backed by existing geometry
  resources; hover-only or unresolved defaults do not satisfy the icon contract.
- App shared `AppWorkspacePanes` owns workspace columns, floating context geometry
  and the docked inspector's retained preferred width. Views supply pane instances
  and bind visibility; `AppPaneSplitter` wraps Avalonia's pointer and keyboard
  resizing. Shared styles own minimum widths, default proportion and handle size.
  View models do not compute columns or reconstruct task/browser panes on resize.
  Column subscriptions follow visual-tree lifetime; temporary width clamping does
  not overwrite the user's preference. Final arrangement recalculates shared geometry
  from actual available width, since native maximization can arrange a wider pane
  than its earlier measured constraint. Its template is measured again at that
  width before arrangement, so adaptive child controls do not retain an obsolete
  compact/wide mode even when the outer columns look correct. `AppBrowserViewport` also observes ancestor
  bounds, visibility and transform placement while attached, so a floating-to-docked
  transition updates native presentation even when the viewport's local bounds do
  not change. Detachment removes every ancestor subscription and parks the view;
  approval cancellation restores the latest placement without replacing the session.
  Markup guards and `MOSSUI002` reject geometry
  and composition overrides. This changes no tool policy, authorization, provider
  request or persistence contract. See [WORKSPACE_PANE_COMPONENT_ACCEPTANCE.md](UI_COMPONENTS.md#workspace-pane-component-acceptance) for
  layout, lifecycle, rejected-path tests and ordinary-permission native evidence.
- App shared `AppProjectPrompt` and `AppApprovalPolicyButton` own the empty-session
  heading and composer permission presentation. Views bind semantic data and the
  existing project/policy commands, not child controls or dimensions. Shared styles
  own bounded labels, centered icons and menu appearance; compiler and markup guards
  reject composition/action overrides. Neither component changes approval policy,
  authorizes tools, accesses files or introduces a persistence migration. Missing
  commands are unavailable and command CanExecute remains authoritative. Focused
  regressions cover narrow/wide layout, resize, live bindings, rejection and ownership;
  see [WORKSPACE_PROMPT_COMPONENT_ACCEPTANCE.md](UI_COMPONENTS.md#workspace-prompt-component-acceptance) for passive native pixels and limits.
  The heading selects inline or stacked layout from its styled children's measured
  widths, not a fixed viewport breakpoint. Existing bounds and project-name trimming
  remain shared styles; the project button is retained through reflow. See
  [PROJECT_PROMPT_RESPONSIVE_ACCEPTANCE.md](UI_WORKFLOWS.md#project-prompt-responsive-acceptance) for regression and native resize evidence.
- Tool approval presentation keeps its existing identity-bound decisions and queue.
  App shared variants constrain the card to the available viewport; only its body
  scrolls, while decision buttons remain visible. No policy, tool execution, native
  browser ownership or persistence boundary changes. Shared action rows preserve
  explicit accessible names and align internal icon, label and badge geometry.
  Async Headless regressions use an awaiting dispatch adapter, with exception and
  cancellation probes and a source gate against async Action callbacks. See
  [TOOL_APPROVAL_LAYOUT_ACCEPTANCE.md](TERMINAL_TOOLS.md#tool-approval-layout-acceptance) for reproduced failures, native pixels and limits.
- AppApprovalButton captures command and request identity at gesture start, retaining
  it across held Enter and overlapping activation keys. A cancelled request cannot
  transfer a held mouse/keyboard decision to its successor through a rebound
  CommandParameter. The two decision controls share AppButton styling and native
  activation; VM identity checks and execution policies stay unchanged. See
  [APPROVAL_GESTURE_IDENTITY_ACCEPTANCE.md](TERMINAL_TOOLS.md#approval-gesture-identity-acceptance) for the failing baseline, rendered file/
  audit integration and passive native comparisons, not automated native approval.
- MainWindow owns one global approval surface outside the retained settings/workspace
  page switchers, below the shutdown overlay. Full-page settings opt into Continue
  keyboard navigation through AppPageSwitcher; other secondary pages keep Cycle.
  Requests do not close settings, steal editor focus or resolve automatically.
  Identity-bound queue/cancellation and browser presentation blocking are unchanged.
  See [GLOBAL_APPROVAL_PRESENTATION_ACCEPTANCE.md](TERMINAL_TOOLS.md#global-approval-presentation-acceptance) for production-window regressions
  and passive Python captures of synthetic pending requests, not tool execution
  or same-state Codex approval pixel equivalence.
- Avalonia packages are aligned at 12.1.3, the first patch containing the Headless
  startup/queue-shutdown fixes. A session's consumer task is assigned before it
  starts, and cancellation joins the consumer before closing its queue. Immediate
  disposal, rejected post-disposal dispatch and fresh application scopes after
  completed/failed callbacks have focused regressions. No retry, exception
  suppression, reflection workaround or shared mutable test session is used.
- Provider/model writes and discovered-model imports share the App catalog's
  `SettingsMutationRunner`. It prevents overlapping configuration mutations,
  forwards the editor lifetime token, and turns storage failures into localized
  safe errors without publishing exception text. Editors track draft revisions;
  a late successful write cannot clear a newer or changed draft. Proxy validation
  rejects a draft that changed while awaiting the repository. Deleting another
  model does not reset the current editor. New drafts retain one identity through
  retries. A successful first commit attaches that identity without reloading or
  overwriting fields edited while saving; refreshed catalogs can then reject a
  deleted target instead of recreating it. Failure callbacks publish only to the
  current live draft revision. Failed saves refresh the catalog without declaring
  success or replaying the write. A persisted draft identity is attached from the
  successful read without replacing edited fields; failed reads disable retry
  until a successful refresh. SQLite model updates prioritize the stable row ID
  and enforce its provider ownership. Changing the API model ID rejects collisions
  and rolls back default changes instead of merging another row; new identities
  retain the existing provider/API-ID upsert behavior. This reuses configuration
  ports and
  shared controls, with no migration or tool-policy change. See
  [CONFIGURATION_MUTATION_ACCEPTANCE.md](ENGINEERING.md#configuration-mutation-acceptance) for rejected-path tests and native input.
- Provider and model settings share one App-owned catalog snapshot through existing
  configuration repository ports. All reads finish before publication; loading,
  safe failure and successful-empty states are distinct. Previous rows and open
  editor drafts survive failed or overlapping refreshes, but mutations require a
  current successful snapshot and matching provider/model identity. A removed target
  cannot silently rebind or resurrect itself. Catalog reads, proxy reloads and
  save-time proxy validation reuse `SettingsOperationLifetime` with the unchanged
  30-second default. Async waiting is bounded even for noncooperative reads;
  cancellation/version checks also reject completed tasks returned after cancellation,
  which `WaitAsync` alone cannot reject. This cannot preempt a synchronously blocked
  repository call. Disposal prevents late notices or writes; discovery also cancels
  with its settings owner. Subscriptions are released with their owners.
  Discovery imports pass the provider to a static model factory instead of
  allocating a capturing factory per fetch. Provider-row fetch commands forward
  the owned asynchronous operation without another async wrapper.
  App's separate `ModelDiscoveryOperation` owns discovery/import coordination and
  localized safe failure mapping. Page operation results and the model editor's
  validation/save error have independent presentation; discovery or another row's
  deletion cannot replace that editor error. New/reset/closed editors clear only
  their draft status, while retained-page navigation preserves it. Draft revisions
  continue rejecting late mutation errors. No configuration port or schema changes.
  The page reuses shared Controls and Styles, with no new persistence schema,
  concrete infrastructure dependency or tool-policy exception. See
  [PROVIDER_CATALOG_ACCEPTANCE.md](PROVIDERS.md#provider-catalog-acceptance) for focused tests, native pixels and limits.
- Proxy, MCP, agent and subagent list presentation belongs to App shared
  `AppSettingsItemRow`. Its finite-width title, detail, metadata, badge and optional
  target-bound actions are composed once; shared styles own centered 24-DIP actions,
  compact rows and two-line descriptions. Business views bind data and existing
  commands, without replacing composition or dimensions. MCP keeps the existing
  safe endpoint summary; no credentials are added to visible text or tooltips.
  This reuses Controls and Styles without a new application port, database migration,
  or change to authorization, approval, timeout, cancellation and output policies.
  One type per file and size limits remain enforced; focused regressions cover layout,
  command targets, disabled state, safe presentation and rejected appearance overrides.
  See [SETTINGS_ITEM_COMPONENT_ACCEPTANCE.md](UI_COMPONENTS.md#settings-item-component-acceptance) for native evidence and validation limits.
- Provider headers and model settings rows belong to App shared
  `AppProviderSettingsHeader` and `AppModelSettingsRow`. They own finite-width
  label columns, truncation, accessible actions and target-bound command parameters;
  shared styles own aligned 24-DIP action buttons. Missing data/commands disable
  actions, while the existing command CanExecute rules still apply. Views bind
  data and existing settings commands rather than rebuilding the row. Compile
  guards reject composition and dimension overrides. This changes no port,
  persistence, provider request or tool policy; see [PROVIDER_ROW_COMPONENT_ACCEPTANCE.md](PROVIDERS.md#provider-row-component-acceptance)
  for rendered overflow evidence, page/command regression and native limits.
- Task failures use a stable, allowlisted Domain category, not persisted provider
  messages or arbitrary error codes. Application classifies known codes and
  exception types; App supplies localized actionable hints through the shared
  `AppTaskFailureNotice`. Its wrapping, icon alignment and card appearance belong
  to Controls and shared Styles, with C#/AXAML guards against instance overrides.
  Infrastructure stores the category through the existing atomic workspace port
  and V7/V8 forward migrations; legacy failures retain a safe generic fallback.
  V8 expands the category constraint for provider blocking and output limits in
  one transaction while preserving prior failure records and workspace data.
  Resume clears the category, and own cancellation remains cancellation. Tool
  authorization, approval, timeout and output policies are unchanged; restoration
  never replays a failed run. See [TASK_FAILURE_ACCEPTANCE.md](CONVERSATIONS.md#task-failure-acceptance) for safety,
  preservation, layout and ordinary-permission native evidence.
- Agent owns the lifetime of a child execution that streams progress through a
  bounded channel. A consumer break, event-presentation failure or iterator
  disposal cancels only that child with a linked token, then waits for the existing
  tool executor to finish cleanup and audit. Parent and concurrent task tokens are
  not cancelled by this local cleanup. Normal cancellation still publishes the
  final bounded tool result before stopping. This reuses existing contracts and
  approval, timeout, output and persistence policies, without platform or UI access.
  Focused regressions cover abandoned previews, presentation exceptions, audit
  draining and shared-runner task isolation; no new schema or replay is introduced.
- Persisted tool results resume as provider-neutral `ToolObservation` data,
  not native calls with fabricated arguments. Application owns the bounded,
  Unicode-safe observation format; App reuses the workspace repository and rejects
  foreign-task history and unknown roles before model dispatch. Providers encode
  observations as ordinary user data and cannot emit native calls from that role.
  Agent context budgeting retains a clipped recent history group when possible,
  preserves complete native call groups, and refuses metadata that cannot fit.
  Shared subagent context includes labeled observations only when sharing is
  enabled; instructions treat file and tool results as untrusted, non-replayable
  data. Stored tool records and UI presentation are unchanged, with no schema or
  migration change and no new credential or argument persistence. Tool policies
  still apply to every newly requested operation. Focused tests cover legacy data,
  role/task rejection, protocol serialization, budgets, Unicode and real local
  file-tool-to-SQLite-to-follow-up requests, including read-only refusal.
- Attachment presentation belongs to App shared `AppAttachmentChip`; composer
  and message views bind file labels and an optional removal command. The control
  owns alignment, truncation, accessible text and command visibility; shared styles
  own sizing and the symmetric surface shadow. Compile guards reject business
  replacement of content, padding or dimensions. This reuses existing attachment
  commands, adds no file access or persistence migration, and changes no tool
  authorization, approval, timeout, cancellation or output policy. Focused tests
  cover owned bindings, removal availability, read-only messages, long labels,
  light/dark layout and negative boundary cases; each type stays in its own file
  and methods and files remain within the engineering hard limits.
- Task creation/resume and their user message, final status and assistant text,
  and assistant/tool event segments use the atomic `SaveTaskWithMessagesAsync`
  repository port. Infrastructure owns the SQLite transaction and rejects
  cross-task batches before writing; App does not issue SQL. No schema change or
  migration is needed. A committed assistant cursor prevents duplicate text if
  later presentation fails, and equal message timestamps preserve insertion order.
  The composer clears a draft only after durable preparation and only if it still
  equals the captured input. It consumes only captured attachments; status updates
  of the same task cannot clear the next draft's attachments. Safe, localized
  preparation errors and send/pending/stop labels use shared controls and styles.
  Tool authorization, approval, timeout, cancellation and output budgets remain
  unchanged. Focused tests cover rollback, task isolation, cancellation, retry,
  concurrent typing, UI state and interrupted/cancelled conversation recovery;
  keep implementation types and methods within the existing hard limits.
- Embedded browser sessions carry the owning task ID through launch options.
  Each live session owns its pane, tabs and native views; selecting a task only
  selects presentation state and never launches a browser. Background sessions
  cannot replace the selected task's address, tabs or toolbar commands.
  A window-level shared `AppBrowserHost` keeps every native view attached and
  arranged, including before its inspector is opened. `AppBrowserViewport`
  supplies the selected inspector rectangle; other views remain laid out outside
  the window's content bounds, without hit testing or keyboard focus. Hiding
  the inspector or opening settings parks all views without recreating sessions.
  Pending approval also blocks native presentation, so child HWNDs cannot cover
  the approval surface. The window coordinator dispatches presentation updates
  onto the UI thread and unsubscribes on disposal without touching UI objects
  from the service-container disposal thread; tool approval policy is unchanged.
  The Windows backend's composited modes return synthetic frames, so they are
  not a substitute for live embedded hosting. Session release removes only its
  own native views and matching pane; stale release cannot clear a replacement.
  This remains App presentation behavior, uses the existing session factory
  contract and tool pipeline, and changes no authorization or persistence policy.
  Focused tests cover task routing, inactive/background layout, selection and
  release isolation. Native acceptance must cover two local synthetic tasks,
  hidden-inspector startup, tab switching and independent cancellation.
- Full-page settings navigation uses the shared `AppPageSwitcher`. It retains
  workspace and editor instances, hides the inactive page from keyboard and
  accessibility navigation, and owns initial focus, restoration, and stale
  focus callback rejection. UI page switching does not change tool policies,
  repository state, or task execution ownership. Focus selection uses one lazy
  visual-order traversal, preserving remembered targets and the first usable
  fallback while stopping at the first available field rather than materializing
  the full page. See [PAGE_FOCUS_OPTIMIZATION_ACCEPTANCE.md](ENGINEERING.md#page-focus-optimization-acceptance) for allocation,
  Headless resize, native settings/inspector evidence and unresolved layout limits.
- Automatic page/drawer focus targets must fit every ancestor scroll viewport.
  Offscreen remembered fields fall back to a visible eligible control rather than
  causing BringIntoView to hide the page heading. Visible remembered editors keep
  their scroll/draft, while explicit user focus can still scroll to later fields.
  This is shared App focus policy, not a native resize/DPI workaround; see
  [SETTINGS_VIEWPORT_FOCUS_ACCEPTANCE.md](UI_WORKFLOWS.md#settings-viewport-focus-acceptance) for reproduction and validation limits.
- Browser toolbar and selection-aware horizontal scrolling belong to App shared
  controls; views supply address, commands, and selected item rather than child
  controls or instance sizes. Navigation callbacks capture their owning native
  view because NativeWebView forwards backend senders; replaced and detached
  views cannot update current address or history commands. Event delegates are
  unsubscribed on detach. These changes reuse existing session and command ports,
  add no persistence or authorization policy, and keep files and methods within
  the hard limits. Focused tests cover two-way binding, Enter, narrow layout,
  component ownership rejection, bounded scrolling and stale navigation events.
  Stateful component binding tests use an isolated Headless UI dispatcher;
  native browser acceptance still requires a running ordinary-permission window.
- Manual browser actions share the existing `IToolExecutor` pipeline with agent
  tools, including navigation, history, tabs and `browser.devtools`. App's
  `BrowserPaneActionsViewModel` owns busy/cancel state and a five-minute total
  lifetime including approval; the executor retains its own execution timeout.
  Contexts must match their owning task and route to its attached session, never
  a replacement lazy session. Revoking context or replacing a session cancels
  pending work and rejects stale results; non-cooperative execution remains busy
  until it returns. Tab selection follows actual session metadata, restoring
  the bound selector after denial. Missing contexts disable actions instead of
  falling back to direct native calls. Developer-tool support is an optional
  browser port capability; unsupported sessions return a safe localized error.
  Read-only tab metadata and native events remain presentation projections.
  Focused tests cover policy, audit, ownership, lifetime and bound selection;
  native acceptance uses synthetic local tasks without provider calls or accounts.
- Models emit normalized agent events instead of provider-specific UI objects.
- Normal window close must keep the UI dispatcher alive while task cancellation,
  final conversation persistence and browser release finish, then dispose startup
  discovery and the service container. App owns this lifetime coordination and
  reuses existing task, repository and cancellation contracts; no tool pipeline,
  authorization or database schema change is needed. Shared dialog controls own
  pending/error presentation. Task registration is closed before final draining,
  including task creation already in progress. Persist partial assistant text on
  cancellation. Focused tests must cover concurrent runs, creation/close races,
  duplicate close requests, safe failure presentation and persisted cancellation;
  keep each new type in its own file and methods below the existing hard limits.
- Model catalog presentation belongs to App and reads through the existing
  configuration repository port. Reloads publish a complete snapshot, discard
  stale completions, and retain the user's provider/model identity and explicit
  reasoning choice. A disabled or deleted selection does not silently fall back;
  loading or failed reads block new sends without clearing the draft. Running
  tasks retain their Stop action and already captured model configuration.
  Menu commands resolve stale identities to current records before publishing
  parameters, and disposal prevents late catalog publication. Shared action
  components own the composer selector, model rows and quick-menu layout.
  This introduces no persistence migration, tool capability or approval change.
  Focused tests cover overlapping reads, unavailable selections, safe errors,
  disposal, real settings-to-SQLite-to-loopback requests and light/dark layout.
- Application's immutable `ModelReasoningPolicy` owns protocol/model-specific
  effort capabilities and normalization. Settings, agent/subagent editors and
  the composer reuse it; provider execution rejects unsupported Gemini efforts
  before creating an HTTP client, and request building repeats the check.
  Existing invalid configuration remains visible and requires explicit correction;
  no migration or silent fallback rewrites user choices. App's shared
  `AppChoiceComboBox` and `AppDiscreteSlider` separate semantic selection from
  native list/range coercion so dynamic options cannot overwrite a draft.
  Slider input subscriptions follow visual attachment lifetime and shared tick
  layout owns endpoint alignment. Safe failure classification reuses existing
  persistence. See [MODEL_REASONING_CAPABILITY_ACCEPTANCE.md](PROVIDERS.md#model-reasoning-capability-acceptance) for loopback/native
  evidence, supported-family boundaries and the non-clean full-suite result.
- The shared `AppEditMenu` owns titlebar editing commands, aligned shortcut rows,
  valid same-window text targets, selection restoration during menu focus, and
  subscription disposal. It uses Avalonia's editor and clipboard operations,
  rejects password copying and read-only mutations even on direct execution,
  and does not restore a selection onto externally replaced text. This is App
  presentation behavior, not an agent tool or persistence operation; capability,
  approval, timeout, cancellation and output policies are unchanged. Compile
  guards reject business replacements of its content, flyout, commands and sizes.
- Model discovery reads each HTTP response as a stream and limits each JSON page
  to 2 MiB before parsing; Gemini discovery also caps the number of pages.
  Its separate `GeminiModelDiscoveryParser` owns model and pagination metadata.
  Explicit generation methods must include `generateContent` or
  `streamGenerateContent`; non-generative models are filtered before deduplication.
  Missing optional capability metadata retains compatibility, while malformed
  explicit metadata rejects the read without echoing the response body. This does
  not rewrite manually configured models or change routing and cancellation.
- Provider SSE error codes and messages are redacted and bounded at the provider
  boundary before they become user-visible model failures.
- Gemini owns validated, bounded content snapshots, including signed thought,
  text, function-call and empty trailing parts in their original order. Only a
  selected candidate's valid STOP followed by validated stream completion releases
  calls and its continuation. Invalid signatures, identities, late content or limits
  discard the whole batch; Gemini 3 requires a signature on the first function part.
  Consecutive parallel results share one user content block, preserving native IDs.
  Application's immutable `ModelContinuation` is provider-neutral and scoped by
  provider, protocol and model identity. Main and child Agent loops retain it only
  in memory and account for it as non-trimmable context metadata. It never enters
  tool parameters, shared child text, UI, audit or SQLite, and generic diagnostics
  and serialization omit its opaque payload. Restoration uses existing tool
  observations, not native signed calls, and never replays work. No new database
  schema or approval exception is introduced; see [GEMINI_CONTINUATION_ACCEPTANCE.md](PROVIDERS.md#gemini-continuation-acceptance).
- Providers share a strict, incremental UTF-8 SSE reader: each physical line is
  limited to 1,048,576 UTF-16 code units before appending, including ignored fields
  and comments; each joined data event is limited to 2,097,152 code units including
  separator newlines. Fixed byte/character buffers preserve split UTF-8 sequences,
  CR/LF framing and a leading UTF-8 BOM without waiting for EOF to deliver an event.
  Limit/encoding failures emit one safe ModelFailed, not raw payloads; cancellation
  remains cancellation. The transport owner disposes the response stream.
  Non-success HTTP bodies are streamed with a 65,536-byte limit regardless of
  Content-Length; oversized or invalid UTF-8 bodies are not shown or logged.
  The shared provider SSE entry point checks HTTP status before handing any data
  to protocol parsers and owns the success-stream lifetime, including cancellation
  and early enumeration disposal. Adapters retain ownership of the HTTP response,
  client, request headers, payload and protocol terminal semantics. Internal SSE
  frames are immutable values instead of one heap object per event. The raw Stream
  overload remains non-owning for callers that manage their own stream lifetime.
  This reuses IModelProvider and changes no tool, approval, task or database policy.
  See [PROVIDER_STREAM_LIMIT_ACCEPTANCE.md](PROVIDERS.md#provider-stream-limit-acceptance) for protocol, workflow and native evidence.
  Real pinned-TLS loopback regressions also cover fixed-length prefix delivery,
  completion, cancellation, early disposal and certificate rejection across the
  four adapters, without changing production certificate validation. The separate
  local plaintext HTTP withholding observation remains open; independent Python
  transport evidence is in [FIXED_LENGTH_STREAM_ACCEPTANCE.md](PROVIDERS.md#fixed-length-stream-acceptance).
- Compatible chat streams isolate choice 0 and reject incomplete or malformed
  tool batches before execution. Empty/null tail tool metadata after finish is
  harmless; nonempty late calls and text remain invalid. Settings-to-loopback
  HTTP-to-tool/audit-to-SQLite/reopen tests exercise the production chain without
  paid requests. See [COMPATIBLE_STREAM_ACCEPTANCE.md](PROVIDERS.md#compatible-stream-acceptance) for runtime evidence;
  the shared framing boundary also rejects oversized lines and multiline events
  before the accumulated tool batch can execute.
- Per-model advanced JSON can add protocol-specific parameters, but cannot
  replace system messages, tools, model identity, or the output and reasoning
  controls managed by the model editor. The same rule is applied when building
  requests from previously saved profiles; compatible chat models can select
  `max_completion_tokens` only within their configured output limit.
- The agent injects task-bound safety guidance and constructs a bounded context
  suffix using each model's configured context and output limits.
- Tools execute through one guarded pipeline and return bounded `ToolResult`
  values plus optional artifacts. Summary, content, and dialogue roles/text share
  the task's output-character budget before audit and model-context insertion.
  Declared string-length constraints are checked before approval using Unicode
  scalar counts; malformed or inverted length bounds fail closed. File content
  and replacement text may be empty or whitespace without relaxing path checks.
  Cancelling a pending approval is classified as `tool_cancelled`, not an
  approval-service failure. Approval exceptions without caller cancellation
  remain `approval_failed` and never expose raw exception text. The executor
  checks cancellation again immediately before invoking any tool, including
  a non-cooperative tool after an approval/cancellation race.
- MCP reload limits initialization and tool discovery to 15 seconds per server,
  disposes failed clients, and preserves caller cancellation. Connection errors
  and unexpected tool exceptions use safe summaries rather than raw exception text.
  Reload ownership includes waiting for the reload gate and configuration reads.
  Shutdown rejects new reloads, cancels running/queued operations and awaits their
  return before disposing clients, removing only MCP tools and releasing the gate.
  Noncooperative configuration reads retain ownership and cannot start late servers.
  Repeated disposal shares one completion task. The optional time-provider seam
  keeps production on system time with the same 15-second default; tests advance
  deadlines after binding real server descendants instead of racing process startup.
  See [MCP_EXECUTION_ACCEPTANCE.md](BROWSER_MCP.md#mcp-execution-acceptance) for shutdown evidence and remaining limits.
- MCP registration identities include the persisted server profile GUID and a
  digest of the exact remote tool name. Normalized labels are descriptive, not
  sufficient identities. Discovery validates a complete server batch before
  publishing adapters; duplicate identities fail closed and dispose the client.
  Remote calls preserve original case and punctuation. Legacy ambiguous names
  are not aliased or rebound, and historical records are not rewritten. See
  [MCP_TOOL_IDENTITY_ACCEPTANCE.md](BROWSER_MCP.md#mcp-tool-identity-acceptance) for compatibility and end-to-end evidence.
- MCP settings expose persistent localized names for transport-specific inputs
  and typed error severity through the shared header/footer appearance variants.
  Explicit reconnect refreshes registration without replaying failed tool calls.
  Production Headless UI recovery, real HTTP/approval/audit, cancellation, denied
  calls and operable restored history are recorded in [MCP_UI_RECOVERY_ACCEPTANCE.md](BROWSER_MCP.md#mcp-ui-recovery-acceptance);
  this does not establish automatic transport resumption or native pixel parity.
- stdio MCP connections use the portable process launcher with owned standard
  input and per-server environment snapshots. Each connection owns a Windows
  kill-on-close Job independently of shell commands. Failed or cancelled startup,
  parent exit, reload, and disposal reclaim its descendants. Protocol frames are
  serialized and limited to 2,097,152 characters; stderr is drained without being
  copied into tool results. Cancelled sent requests send an advisory MCP
  cancellation notification and remove their pending response entry.
- MCP transport and process cleanup continuations do not depend on a UI
  synchronization context. Initial MCP discovery starts on a background task
  after the main window opens. The shared startup state prevents premature agent
  runs without clearing the composer or blocking settings navigation. Completion
  is posted to the UI dispatcher; safe warnings link to MCP settings. Application
  exit cancels and awaits startup before disposing the service container, without
  waiting for queued UI callbacks. Database initialization and composition still
  happen before window creation; this is not a fully asynchronous bootstrap.
- Streamable HTTP MCP bounds each request and response to 2 MiB of bytes.
  JSON and SSE responses use strict UTF-8 and validated JSON-RPC envelopes;
  incremental SSE completes on the matching result without waiting for EOF.
  Progress and heartbeat bytes share the response budget. Cancellation attempts
  a bounded advisory notification; failed initialization or connection disposal
  cancels in-flight requests and best-effort terminates an assigned HTTP session.
  Automatic redirects are disabled to avoid forwarding configured headers to
  another endpoint. No selected MCP proxy means no implicit system proxy.
  Remote protocol and tool-error bodies never become persisted failure summaries.
- MCP settings expose stdio arguments and environment variables or HTTP URL,
  headers, and proxy according to the selected transport. Saving drops inactive
  transport fields; malformed JSON and credential-bearing URLs are rejected
  without echoing configuration secrets into status messages.
- `AuthorizedRoots` alone grant filesystem access. `WorkingDirectory` only
  resolves relative paths and never becomes an implicit authorized root.
- Path-aware tools validate every requested path before approval and resolve it
  again immediately before execution, including the interactive terminal's
  working directory. Rejected paths are audited without prompting the user.
- Text reads return a full-file SHA-256 and retain bounded, task-owned version
  metadata for the current run; context copies used by child tools share only
  their parent task's version store. Cross-task store reuse is rejected.
  Writes and exact replacements stage a separate file in the target directory,
  revalidate authorization and the original version before atomic replacement,
  and update the observed version only after committing. Existing targets use
  replacement; new targets use non-overwriting rename. An application-owned,
  bounded lock set serializes overlapping writes through the shared writer.
  Exact replacement retains the detected text encoding and rejects files over
  16 MiB. Model instructions require explicit `expectedSha256`, which also
  protects a version observed before the current run. Known conflicts surface
  as `file_changed` and require a fresh read rather than automatic retries.
  This is optimistic concurrency, not a filesystem transaction against hostile
  processes that alter path bindings between the final check and rename.
- Subagents inherit the parent task's authorization, approval, timeout, and
  cancellation context. Child tool calls re-enter the same executor and audit
  pipeline; recursive delegation is not available to child models. Saved child
  model identity/provider and reasoning effort are checked against current
  protocol/model capabilities before starting a child run. A later configuration
  edit cannot silently substitute or invoke an unsupported child; rejection is
  actionable, localized and audited. See [SUBAGENT_RUNTIME_CONFIGURATION_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#subagent-runtime-configuration-acceptance).
  Child membership checks do not rebuild descriptor projections; parent model
  turns bypass no-op delegation filtering while retaining independent snapshots
  and current dynamic registrations. See [AGENT_TOOL_CATALOG_OPTIMIZATION_ACCEPTANCE.md](ENGINEERING.md#agent-tool-catalog-optimization-acceptance).
  Progress
  appears in the active conversation, while the final bounded dialogue is
  persisted with the parent tool result. Child text is previewed before its
  model turn completes; previews are coalesced into one reply row, limited to
  1024 characters and throttled at 64-character increments. The progress channel
  is bounded to 256 entries; final dialogue replaces previews and is persisted
  only after the normal tool output limiter runs.
- Cancelling a subagent stops its model/tool execution but drains the bounded
  progress channel until the executor returns its audited, limited terminal
  result. That result is persisted before cancellation propagates; no next
  model turn or queued tool starts. Tool history preserves cancellation and
  timeout error codes, while terminal run events clear any orphaned live cards
  without claiming that an unfinished tool succeeded.
  Interrupted child dialogue is carried by `ToolRunInterruptedException`;
  the normal executor still classifies cancellation versus timeout, applies
  the shared output budget, and completes audit persistence before delivery.
- The App projects persisted subagent dialogue into task, status, tool, and
  reply rows for display. This presentation does not change the stored dialogue
  or bypass the parent tool's output limits.
  Tool detail viewports share a bounded-height, vertical-only scroll variant so
  wrapped prose and long paths receive finite width. Code-block viewports retain
  horizontal scrolling. Detail surface spacing belongs to the shared style;
  content, persistence, tool policy and output limits are unchanged. Headless
  layout tests load the real App styles and fonts without application startup,
  and cover long unbroken text at narrow/wide widths in both themes.
- Subagent model choices are App-owned presentation snapshots loaded through
  the existing configuration repository and model-catalog change callback.
  Reloading retains editor drafts and the preferred provider/model identity;
  unavailable choices are removed without selecting a different model. A load
  generation rejects late snapshots, and save reloads the catalog before
  validation. Query failures preserve the draft and report a safe message rather
  than saving a stale selection. The window unsubscribes its catalog callback
  on disposal. This changes no authorization, approval, timeout, cancellation,
  output-limit or persistence contract, and adds no database migration.
  Focused tests cover settings-to-SQLite catalog changes, failed and out-of-order
  loads, unavailable selection recovery, stale-save refusal and disposal;
  affected files and methods remain within the engineering hard limits.
- Subagent editor writes capture immutable drafts and stable identities before
  awaiting storage. Shared mutation and guarded-command ownership reject overlap
  without cancelling accepted work; late results cannot replace newer drafts or
  another editor context. Lifetime and command cancellation reach repository ports,
  with a 30-second cancellation request and bounded reads. Noncooperative writes
  retain ownership until they return, rather than claiming rollback or forced exit.
  Refresh failures preserve drafts but block mutations; removed targets cannot
  be resurrected. Canonical snapshot selections explicitly notify bound controls,
  while unchanged collections retain item instances and scroll placement. Shared
  controls own appearance; structural footer rows give action and feedback finite
  width. No migration or tool-policy change; see [SUBAGENT_EXECUTION_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#subagent-execution-acceptance)
  for failed baselines, bound-view tests, native input and verification limits.
- Custom-agent settings use the same App editor revision, guarded commands and
  mutation runner through existing configuration ports. Immutable clicked drafts
  retain stable identities; late save/delete results cannot overwrite new edits
  or another context. Model-catalog notifications reload model options without
  resetting agent fields. Failed reads retain prior rows but block writes, and
  only available, completed snapshots can prove an editing target was removed.
  Workspace-notification failure is distinguished from a committed write and
  can be retried by refresh without another save. Lifetime cancellation rejects
  late editor publication; noncooperative writes retain mutation ownership until
  returning. Shared controls own the finite-width action and feedback presentation.
  No migration or tool-policy change; see [AGENT_CONFIGURATION_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#agent-configuration-acceptance) for
  actual settings-to-HTTP requests, persisted replies, native input and limits.
- Browser implementations expose one task-bound `IBrowserSession` contract;
  tab identifiers are stable only for the lifetime of that session.
  CDP attachment creates a task-owned page; borrowed contexts expose only that
  task's attached/created pages and their descendant popups. Disposal waits for
  in-flight creation and closes owned pages/runtime state once, never the attached
  browser, borrowed context or unrelated pre-existing pages. A shared CDP context
  still shares cookies/login/storage; page isolation does not imply storage
  isolation. See [CDP_TASK_ISOLATION_ACCEPTANCE.md](BROWSER_MCP.md#cdp-task-isolation-acceptance) for deterministic and real
  settings-to-standard-tool-pipeline evidence and remaining native limits.
  [CDP_AGENT_WORKFLOW_ACCEPTANCE.md](BROWSER_MCP.md#cdp-agent-workflow-acceptance) extends the same production ports through
  Agent/Responses conversation, targeted cancellation and restored operable UI
  history using a local scripted provider and test-owned headless Chrome.
- CDP endpoint policy lives in Domain and is enforced again by the browser
  factory before Playwright starts. HTTP discovery bypasses system proxies,
  rejects redirects and oversized responses, and validates the discovered
  WebSocket host and port before any browser connection.
- Task browser sessions are created lazily and released when a run completes,
  fails, or is cancelled. Released handles cannot start a new browser; a resumed
  run receives a fresh session. Existing CDP connections are limited to explicit
  loopback endpoints without URI credentials.
- Each embedded session owns an operation queue covering navigation, DOM work,
  screenshots, downloads, tab reads and tab mutations. Disposal rejects pending
  and new work, waits for the active operation, and performs native cleanup once.
  Pane commands use that session contract rather than independently navigating
  its native view. Tab creation, switching and closing commit on the UI thread;
  a cancelled unpublished view is disposed without changing the active tab.
  Tab-list refreshes carry a session and generation check, so late success or
  failure cannot repopulate a closed session or overwrite a newer refresh.
  Each task has its own embedded pane; the shared native host retains the views
  and parks inactive presentation without reparenting. App's separate inspector
  view model retains tab, docking and floating-card state by task identity, with
  project-specific draft state. Record replacements and background scheduled
  runs cannot overwrite that presentation. Archive drops the task's retained
  state; disposal unsubscribes and rejects later mutations. This UI state is
  in-memory, not a persistence or approval-policy change. See
  [INSPECTOR_TASK_STATE_ACCEPTANCE.md](UI_WORKFLOWS.md#inspector-task-state-acceptance) for the regression and native boundaries.
  Two-session native DOM/screenshot/independent-release acceptance remains
  required before claiming a completed concurrent-browser capability.
- Browser downloads write through a caller-owned stream so the browser layer
  never selects arbitrary filesystem destinations.
- Large binary outputs stay in the task artifact directory. SQLite stores only
  names, paths, media types, lengths, and SHA-256 hashes.
- Persistence is accessed through application ports, not raw connections from
  UI or agent code.
- Proxy settings notify the provider editor after save, deletion or refresh.
  Provider proxy-only reloads preserve drafts and desired routing identity;
  unavailable choices are not replaced with direct access or another proxy.
  Load generations reject older snapshots, and save revalidates the selected
  enabled proxy through the configuration port. The window unsubscribes on disposal.
  The configuration repository atomically refuses deletion of a proxy referenced
  by a provider, browser configuration or MCP server. It reports `ProxyInUseException`
  rather than letting foreign-key cleanup silently change the route to direct.
  This preserves existing data and changes no schema, tool approval, authorization,
  timeout, cancellation or output contract. App owns editor synchronization;
  Application owns the port failure and Infrastructure owns transactional checks.
  Focused tests cover drafts, missing and disabled choices, reload ordering,
  safe errors, reference protection and real loopback discovery/streaming requests.
- Pinned conversations live in an additive SQLite table and are loaded across
  projects into a separate sidebar section. Archived conversations are hidden
  from that section without deleting their pin, so restoring preserves it.
- Workspace page navigation follows explicit, validated catalog navigation
  requests, including selecting the current conversation and opening an empty
  draft. Catalog refreshes and background task metadata updates do not switch
  pages. `WorkspaceNavigationController` owns history command availability;
  `WorkspacePageNavigationViewModel` disposes its navigation subscriptions.
  Shared `AppEmptyState` variants keep fixed-height inspector notices readable.
  See [WORKSPACE_NAVIGATION_ACCEPTANCE.md](UI_WORKFLOWS.md#workspace-navigation-acceptance) for native reproduction and regressions.
- Appearance preferences use an application store port and an infrastructure
  JSON adapter; theme and code-font saves preserve unrelated settings and
  replace the file atomically. The appearance page reports save failures.
- Light and dark theme palettes persist separately in that JSON store. Hex
  colors must maintain a readable foreground/background contrast; the App
  updates theme brushes on the UI dispatcher and can restore default colors.
  Appearance exposes independent light and dark palette editors so switching
  the active theme does not discard either editor's unsaved color draft.
- UI, content, and code fonts persist separately for light and dark themes.
  Existing global UI and code font choices remain as fallbacks until each theme
  is customized; content defaults to the active UI font. Theme changes update
  shared font resources, while conversation prose and code spans use separate
  typography variants.
- Windows-first Default UI and uncustomized content use Segoe UI, matching the
  installed Codex system-font stack. Explicit bundled Inter remains selectable;
  existing enum identities and saved defaults are not migrated or rewritten.
  Layout tests use Skia fonts with headless windowing, not substituted BareMinimum
  system-font metrics. See [DEFAULT_UI_FONT_FIDELITY_ACCEPTANCE.md](UI_COMPONENTS.md#default-ui-font-fidelity-acceptance) for failing
  baselines, native screenshots, persistence audits and remaining reference limits.
- Platform-specific facilities implement portable ports and stay outside the
  domain and application projects.
- `shell.run` launches through the platform-neutral process contracts in
  `Tools.Abstractions.Processes`. The Windows implementation creates each
  process atomically inside its own kill-on-close Job Object, with an explicit
  inherited-handle allowlist, before resuming its first thread. Parent exit,
  cancellation, timeout, and disposal terminate and await the remaining job
  processes before output readers and command ownership are released. Each
  command has its own job; descendants do not outlive a completed shell call.
  Launch failure never falls back to an unowned process. Git commands and
  stdio MCP connections also use this launcher with their own operation or
  connection jobs; PTY and browser lifetimes remain separate implementations.
- Parent standard-I/O endpoints use asynchronous, current-user-only, single-instance
  named pipes; child endpoints remain synchronous and are the only inherited ends.
  This avoids worker-pool starvation while shell/Git/MCP wait for idle output.
  Stream transfer preserves endpoint ownership, while the launcher keeps its
  explicit inherited-handle allowlist and per-operation kill-on-close Job.
  No tool policy, timeout, output, audit or process port changes are required.
  Deterministic old/new starvation probes, cancellation/inheritance regressions
  and production-composition/native-restore evidence are recorded in
  [ASYNC_PROCESS_PIPE_ACCEPTANCE.md](TERMINAL_TOOLS.md#async-process-pipe-acceptance).
- Git tool execution requires the existing owned process launcher and retains
  the standard schema, path, approval, timeout, output, and audit pipeline.
  Infrastructure branch queries and managed worktree operations share
  `GitProcessRunner`, which drains both streams, retains at most 16,384 characters
  per stream, and enforces a 30-second deadline. Normal exit, cancellation, and
  timeout reclaim hook descendants and observe both readers before returning.
  Truncated branch output is not reported as a valid branch. Cleanup does not
  roll back worktree or branch mutations and never replays cancelled operations.
  See [GIT_PROCESS_OWNERSHIP_ACCEPTANCE.md](GIT_WORKTREES.md#git-process-ownership-acceptance) for real Git/hook evidence and limits.
- File-level Git index mutations are separate `git.stage_file` and
  `git.unstage_file` tools. Infrastructure implements the Tools.Abstractions
  repository-authorization port through its bounded identity inspector, including
  unborn repositories, complete repository scope and independently authorized
  external metadata. Tools pin the captured Git/worktree directories and literal
  file pathspecs, reject metadata/directories/links/device aliases, and revalidate
  file paths after approval. Both mutations are external-side-effect risks:
  filters or repository hooks may execute programs. They retain the standard
  capability, task-workspace guard, approval, cancellation, deadline, bounded
  output and audit pipeline. This is not an operating-system sandbox for hooks.
  App's dedicated index-actions view model snapshots task roots, cancels obsolete
  operations, rejects their results and awaits real tool/refresh cleanup on
  asynchronous disposal. The diff inspector exposes a keyboard-accessible action
  for the selected staged/unstaged side and refreshes real status after success.
  Only that side's rename includes its source; copies do not unstage independent
  edits to the source. Cancellation warns that the index may already have changed.
  No working-file reset, commit, push, PR, stage-all or hunk workflow is implied.
  See [GIT_STAGING_ACCEPTANCE.md](GIT_WORKTREES.md#git-staging-acceptance) for evidence and remaining limits.
- Staged-only commits use separate `git.commit_preview` and `git.commit` tools
  through that same repository authorization and execution pipeline. The preview
  reads the complete bounded index plus HEAD/branch twice around repository
  reauthorization; a SHA-256 state token includes repository and metadata paths.
  Commit checks this token again after ordinary approval, refusing changed state,
  conflicts or an empty staged diff. It passes a literal bounded message to an
  ordinary `git commit`, without staging, amending, configuring identity, bypassing
  hooks/signing, pushing or retrying. This does not eliminate native path/index
  races or sandbox hooks. App's dedicated task-scoped modal preserves in-memory
  message drafts across navigation, cancels obsolete work and awaits actual tool
  and refresh cleanup. Failed/cancelled execution requires a fresh preview and
  warns that a commit may already exist. No draft restart persistence is implied.
  Successful completion releases operation ownership and publishes non-busy and
  closed state together, so modal close cannot expose a still-busy trigger between
  separate dispatcher updates. Obsolete generations cannot close a newer dialog.
  See [GIT_COMMIT_ACCEPTANCE.md](GIT_WORKTREES.md#git-commit-acceptance) for evidence and outstanding acceptance.
- Confirmed pushes use separate local-only `git.push_preview` and side-effecting
  `git.push` tools. A bounded configuration/HEAD/branch/endpoint snapshot is sampled
  twice around repository authorization and checked again after approval. The
  command pins its captured commit ID to one heads ref, overriding broad mirror,
  tag, submodule and push-option defaults only for that process. Ordinary hooks,
  credentials and signing remain active; no automatic commit, force, upstream
  configuration or retry is added. Multiple push URLs and custom helpers fail
  explicitly. Infrastructure implements a local-destination authorization port,
  supporting bare/non-bare repositories only inside authorized roots with bounded
  metadata-tree link checks; alternate object stores are currently unsupported.
  Raw configuration, credential-bearing URLs and push output never reach tool
  output/audit/UI. App's task-scoped inspector modal invalidates edited previews,
  preserves selections and awaits actual tool/refresh cleanup on cancellation or
  disposal. Successful close publishes non-busy state first. Cancellation can
  follow remote mutation; native TOCTOU and hook/helper sandboxing are not claimed.
  See [GIT_PUSH_ACCEPTANCE.md](GIT_WORKTREES.md#git-push-acceptance) for evidence and outstanding acceptance.
- Both dedicated Git runners remove all `GIT_` environment variables from child
  processes, including inherited values and explicit overrides, using the optional
  prefix-removal process contract. Windows validates prefixes, builds a private
  environment block and does not mutate the parent environment. This prevents
  index/object/config/trace redirection through inherited Git variables; it also
  intentionally excludes environment-only Git identity, config and SSH overrides.
  Ordinary repository/user/system Git configuration, hooks and signing remain
  active. The contract does not restrict shell commands or provide an OS sandbox.
  App's shared modal returns focus after a temporarily disabled trigger command
  becomes available, without overriding newer focus, reopens or detached views.
- New conversations in projects expose a dedicated environment-selection view model
  through the shared composer context bar. Selection delegates to the existing
  Git worktree port, never UI filesystem/process code, and retains approval
  policy. Existing task environments are fixed and restored from task metadata.
  The flyout accepts a local branch, tag or commit; empty selection retains HEAD.
  App captures the reference with the send snapshot and locks it during preparation.
  The existing Application worktree port carries it to Infrastructure, which
  validates bounded input and resolves a commit with rev-parse --verify
  --end-of-options before passing only its immutable hash to worktree add.
  Invalid/noncommit references refuse creation and retain the draft; there is no
  implicit fallback or fetch, existing-task override or database migration.
  See [WORKTREE_BASE_REFERENCE_ACCEPTANCE.md](GIT_WORKTREES.md#worktree-base-reference-acceptance) for integration and evidence limits.
  After snapshot/persistence failure, preparation queries active and archived
  ownership under an independent 30-second recovery deadline; only an unowned
  new worktree is removed, without force. Ambiguous persistence or failed cleanup
  preserves the path with a localized recovery notice. Preparation has its own
  cancellation source and tracked completion; the composer exposes cancellation
  while Enter remains submission-only and disabled during preparation. Late
  snapshots are rejected before message persistence; pending snapshot/status writes
  and recovery removal retain ownership until return even when a cooperative
  cancellation deadline expires. Narrow context bars reflow environment/branch
  onto a second row, and the welcome layout respects the finite remaining height
  with a shared short-viewport appearance.
  An already committed task is finalized as Cancelled with an independent deadline without
  consuming the draft or starting a provider. Unconfirmed finalization retains
  visible task metadata and an inspect-before-resend notice. Git creation failure
  with a retained managed directory reports a typed recovery path after owned
  process cleanup instead of blindly deleting it.
  Infrastructure now restricts managed creation/removal to direct GUID children,
  validates absolute paths and link/reparse ancestry, and checks Git pointer files
  before native discovery. Source repository scope must be explicitly authorized;
  standalone metadata outside the working root requires an explicit additional
  authorization, while registered linked sources retain backlink-validated common
  metadata. Discovery identities and linked-worktree backlinks must agree. Mutations use
  captured common-directory arguments and the resolved selected commit (or captured
  source HEAD), preserving the selected linked-source baseline. Authorization is snapshotted before worker dispatch;
  operations share a total cooperative 30-second deadline and await owned process
  cleanup. Ordinary removal retains branches and dirty/ambiguous directories.
  These checks do not pin native directory identities. See
  [WORKTREE_PATH_SAFETY_ACCEPTANCE.md](GIT_WORKTREES.md#worktree-path-safety-acceptance). Restored task contexts additionally bind
  Application's `ManagedWorktreeWorkspaceGuard` to the exact task-GUID path and
  project/root authorization snapshot. Infrastructure's read-only validator
  verifies repository identity and registration on each operation, including
  before approval and again before tool execution. Context copies for preview,
  diff, statistics, undo, terminal/browser controls and child agents retain the
  guard. Resume validates before message/status persistence and run preparation
  validates before model invocation; direct branch reads and explicit cleanup
  fail closed without matching task/project authorization. Cleanup cannot use a
  different task's otherwise-valid worktree path. Checks await cooperative
  cancellation/deadline cleanup; raw validation errors are not exposed. This
  remains snapshot authorization, not a live revocation subscription or native
  handle-level race protection. See [RESTORED_WORKTREE_AUTHORIZATION_ACCEPTANCE.md](GIT_WORKTREES.md#restored-worktree-authorization-acceptance).
  Native restart, creation-time crash recovery and complete worktree path/race auditing remain incomplete;
  see [WORKTREE_CONVERSATION_ACCEPTANCE.md](GIT_WORKTREES.md#worktree-conversation-acceptance) and
  [SEND_PREPARATION_CANCELLATION_ACCEPTANCE.md](CONVERSATIONS.md#send-preparation-cancellation-acceptance).
- Each active task owns an isolated run state and cancellation source. Agent
  event presentation crosses the Avalonia dispatcher before touching UI-bound
  observable collections, while tool approvals are serialized through a
  cancellation-aware queue.
  Event presentation dispatches to Avalonia only when an application host exists
  and the caller is off the UI thread; headless workflow acceptance uses the same
  presenter without waiting on an unstarted UI message loop.

#### Visual component boundary

- Field metrics belong to App shared `Styles/FieldVariants.axaml`: text, numeric
  and selection fields share a 32-DIP minimum and centered content; multiline
  configuration and prompt editors select semantic variants instead of page-local
  heights or alignment. Composer growth remains bounded by its shared variant.
  `ValidateSharedFieldSizing` and `MOSSUI002` reject business overrides through
  markup, property elements, C# assignment and styled-property mutation. Widths
  remain structural; values, bindings, read-only state and commands remain
  functional. This reuses existing components and changes no module dependencies,
  authorization, approval, timeout, output limits or persistence contracts.
  Focused tests cover both themes, narrow/wide layout, multiline growth, compact
  browser exceptions, allowed functional data and rejected appearance overrides;
  handwritten types and methods remain within the engineering hard limits.

- Conversation-header inspector actions reuse the existing panel command and
  `AppIconButton` state-dependent geometry. Labels, tooltips and accessibility
  names follow the open state through localized resources. Shared header
  variants own compact geometry, context selection and the separator; views
  do not override appearance. Closing the panel preserves the user's floating
  context-card preference. This changes no task or persistence behavior.
  See [CHAT_HEADER_ACCEPTANCE.md](UI_WORKFLOWS.md#chat-header-acceptance) for actual layout,
  regression and native evidence, including incomplete final native interactions.

- The main shell keeps a 52-DIP navigation rail beside a 264-DIP project
  sidebar; each surface has its own theme brush and shared control variants.
  The active home icon uses the shared filled foreground variant, while the
  sidebar header, pinned section, and project heading follow the same vertical
  rhythm as the desktop reference.
  The shared titlebar is 44 DIP in both workspace and settings, with a 36-DIP
  control band and an 8-DIP header-colored bottom inset, without a divider.
  Settings replace the workspace rail with a full-width 276-DIP sidebar. Its
  back action returns to the workspace independently of the workspace panel
  toggle. The titlebar omits the panel button in settings.
  The settings content begins with a rounded top-left corner;
  standard pages share a maximum 768-DIP content column, with 20- and 34-DIP
  minimum side insets so narrow windows retain the reference card width.
  `SettingsLayoutVariants.axaml` applies this geometry to both standard and
  provider pages. The appearance page shows three full-size theme choices,
  a default-visible diff sample, and independent light/dark palette cards with
  UI, content, and code font editors.
  Accent choices apply immediately to the active
  light or dark palette and persist separately; invalid or unreadable colors are
  rejected without replacing saved settings.

- Workspace task titles use the existing repository port's dedicated rename
  transaction, changing only title and timestamp while returning authoritative
  status, approval, worktree, pin and failure metadata. Task/project identity and
  expected title must match; active, archived and missing targets are rejected
  without replacing the editor draft or recreating a task. App metadata mutations
  retain ownership through persistence and publication; rename/archive block
  starting only their target task, not other tasks or stopping an active run.
  Owner cancellation and the existing cooperative deadline remain in force.
  No schema or tool-policy change; see [TASK_TITLE_PERSISTENCE_ACCEPTANCE.md](CONVERSATIONS.md#task-title-persistence-acceptance)
  for reproduced failures, transaction rollback and verification limits.
- App views use the shared controls in `MossAgent.App.Controls` for buttons,
  icons, text, inputs, cards, selectors, checkboxes, toggles, lists, numeric
  inputs, sliders, selectable and rich text, scroll viewers, and flyouts. Reusable
  visual controls belong in this namespace; `Views` contains only `AppWindow`
  and `AppUserControl` hosts. Direct Avalonia visual
  controls in App AXAML are disallowed by an allowlist in
  `NativeVisualControlUsageTests` and the App project's build-time
  `ValidateSharedVisualControls` target, including new markup directories.
  `ValidateSharedVisualNamespaces` also rejects controls from other AXAML
  namespaces, so a third-party visual cannot bypass the shared wrapper boundary.
  A new visual primitive needs a wrapper and a shared style before it can be
  used in a view. See [UI_COMPONENTS.md](../UI_COMPONENTS.md) for the component
  catalogue, examples, extension workflow, and enforcement rules.
  `MOSSUI001` applies the same rule to C# construction and inheritance during
  compilation, including third-party visual controls; only wrapper code in
  `MossAgent.App.Controls` and structural layout primitives are exempt.
  Composed `AppUserControl` wrappers retain Avalonia's `UserControl` style key;
  shared descendant styles target a stable class on the wrapper root instead
  of its concrete type selector.
  The embedded browser's third-party native view is created through
  `AppEmbeddedBrowserView`; its surrounding surface still uses shared App styles.
  `EmbeddedBrowserPane` hosts that view directly as an `AppBorder.Child`, rather
  than through a templated content control, so dynamically attached native views
  receive valid measure and arrange passes. The Windows application embeds
  `app.manifest` with the Windows 10/11 supported-OS declaration required by
  Avalonia native child-window attachment, per-monitor DPI awareness, and
  `asInvoker` privileges. Native hosting never requires application elevation.
  See [BROWSER_EXECUTION_ACCEPTANCE.md](BROWSER_MCP.md#browser-execution-acceptance) for the
  isolated native-runtime verification and its remaining limits.
- Views and reusable control markup cannot define local `Style` or
  `ControlTheme` declarations. `ValidateNoViewLocalStyles` enforces this at
  build time; visual and interaction variants belong in `Styles/`.
  `MOSSUI002` also rejects direct C# mutations of a control's local `Styles`
  collection, including index replacement and collection-interface casts;
  application-level shared style composition and wrapper internals remain valid.
- Shared metadata and tool activity status use theme-aware foreground roles and
  bounded header geometry. Approval's task-control adapter exposes the existing
  stop command only for a live request belonging to the selected running task;
  queue identity and disposal guards prevent stale gestures from becoming a
  different task action. It changes no execution or approval policy. See
  [UI_METADATA_POLISH_ACCEPTANCE.md](UI_WORKFLOWS.md#ui-metadata-polish-acceptance) for contrast, compact stop and Headless
  production-workflow evidence and its native/pixel-equivalence limits.
- Local component resources cannot shadow shared appearance tokens.
  `ValidateSharedComponentResources` rejects visual-resource declarations,
  resource imports and resource-dictionary replacement in business AXAML;
  converters and data templates using shared controls remain allowed.
  `MOSSUI002` rejects business C# writes to control resources, including aliases,
  merged dictionaries and theme dictionaries. Read-only queries, component
  internals and application-level appearance services remain allowed.
  This is an App compile-time boundary, introduces no runtime port or policy,
  and changes no authorization, cancellation, persistence or output handling.
  Focused negative tests cover both markup and C# bypasses without changing
  rendered styles; component consistency is not pixel-fidelity acceptance.
- Shared appearance cannot be overridden through native base-class property
  elements such as `Button.Background` on an `AppButton`.
  `ValidateInheritedAppearancePropertyElements` rejects this bypass at build
  time while retaining behavior and layout properties such as `Button.Flyout`.
- Icon-and-label actions use `AppActionButton` so their icon size, gap, baseline,
  text color, and hover color follow the same button style. Optional status
  badges stay inside that control, including disabled sidebar actions. Views
  do not use font glyphs as action icons.
- Sidebar pinned, project, and recent conversations use `AppTaskNavigationRow`.
  The component owns selection, title truncation, the shared option button, and
  its action menu. Every action receives the row's task parameter; pin labels
  reflect that task, not the selected conversation. Task metadata operations
  reuse `IWorkspaceRepository`, keep unrelated selection intact, and check the
  target's run state. Views cannot replace the row's content or dimensions.
  The menu closes when its context, target or action command changes, its row
  becomes unavailable or detaches, or an ancestor hides. Ancestor subscriptions
  follow visual-tree lifetime; title/selection updates preserve the current menu.
  See [TASK_MENU_LIFETIME_ACCEPTANCE.md](CONVERSATIONS.md#task-menu-lifetime-acceptance) for focused and native evidence.
- App's `WorkspaceSidebarProjectsViewModel` and `WorkspaceSidebarProjectList` own
  project ordering without mutating the catalog's source collection. Activity order
  uses complete task snapshots, including pinned and search-hidden tasks. Manual
  order uses stable project identities and preserves selected/expanded row instances.
  Sorting preferences load independently from catalog/model startup; failures disable
  sorting writes, not task creation. Commands keep ownership until storage returns,
  use lifetime cancellation and a ten-second cooperative deadline, and reject late
  completion after disposal. The Application `ISidebarPreferenceStore` port is implemented
  by Infrastructure's atomic, ordinary runtime JSON `sidebar-settings.json` store.
  Corrupt preferences are never overwritten; appearance settings and SQLite schema
  are unchanged. The project-section menu contains sorting, not conversation mutations.
  Project summaries offer real up/down moves; shared project rows also support native
  before/after dragging and navigation-viewport edge scrolling. The same preference
  port saves manual order and restores it on restart. See
  [SIDEBAR_PROJECT_DRAG_ACCEPTANCE.md](UI_WORKFLOWS.md#sidebar-project-drag-acceptance) for verified behavior and remaining native limits.
- App's workspace catalog, project snapshots and conversation tabs share typed,
  indexed identity lookups. Project editing finds the current collection index
  without copying the project list. Tabs reuse reconciliation's selected-row
  lookup rather than scanning again; unchanged rows retain their identity and
  collection notifications. Lookups use complete current snapshots, including
  pinned and search-hidden tasks, and preserve first-occurrence/owning-project
  rules without a cache. No I/O or execution policy changes. See
  [WORKSPACE_LOOKUP_OPTIMIZATION_ACCEPTANCE.md](ENGINEERING.md#workspace-lookup-optimization-acceptance) for regression and allocation evidence.
- Icon-only actions use `AppIconButton`, which owns the icon geometry, shared
  hit area, foreground binding, tooltip, and accessible label. Size differences
  belong in shared component variants rather than per-view icon markup. Its
  alternate geometry follows a bound state for expand/collapse actions, so
  views do not stack two icons inside a generic button. Window chrome actions
  use the same component and size variant; maximize/restore switches geometry
  without replacing the button. Component tests reject generic icon-only buttons.
  Caption variants retain 46x36 Arrow-cursor hit areas while the close icon uses
  Size=10, ordinary maximize Size=9, and minimize/restore Size=8. Ordinary maximize
  uses flat line caps and a shared half-pixel translation for solid 96-DPI edges;
  its alternate pseudo-class restores the existing restore-glyph presentation.
  Their shared control theme disables
  keyboard focus so pointer caption actions retain the active editor's focus
  and dialog Escape route instead of introducing titlebar Tab stops. Commands
  and accessible labels remain unchanged. Focus preservation, disabled commands
  and native model-editor resize/Escape evidence are recorded in
  [WINDOW_CAPTION_FOCUS_ACCEPTANCE.md](UI_WORKFLOWS.md#window-caption-focus-acceptance). The shared close-hover rule uses
  `#E81123` with a white glyph. Deterministic checks and version-specific native
  evidence, including unresolved capture mismatches, are separated in
  [WINDOW_CAPTION_FIDELITY_ACCEPTANCE.md](UI_WORKFLOWS.md#window-caption-fidelity-acceptance).
  The titlebar's rounded-square panel glyph uses Size=10 with stroke 1, preserving
  the existing 32x30 hit area. Its Skia regressions belong to the separate pixel
  assembly rather than mixing renderer/font implementations in the App test host.
  Native measured bounds and replay are in [WINDOW_SIDEBAR_ICON_ACCEPTANCE.md](UI_WORKFLOWS.md#window-sidebar-icon-acceptance).
- Action menus opt into `AppFlyout.DismissOnButtonClick` so selecting an action
  closes the popup after its command runs. Search, editor, and model-picker
  flyouts remain open for multi-step interaction. The empty-chat project link
  and composer context share a searchable project picker backed by the catalog's
  selection command. Its create action opens the existing project editor after
  the picker closes, avoiding competing flyouts.
  Shared navigation variants pair hidden opacity with disabled pointer hit testing,
  reveal actions on row hover or keyboard focus, and retain the anchor while its
  flyout is open. Revealing an action never enables a disabled command. These are
  App presentation rules; task authorization and persistence are unchanged.
- The conversation header renders open tasks through `WorkspaceConversationTabsView`.
  `WorkspaceConversationTabsViewModel` follows catalog selection, keeps tabs across
  projects, updates titles from catalog task changes, and removes archived tasks.
  Closing a tab only closes its UI entry; it does not archive or delete the task.
  Selecting a tab routes through the catalog's existing task-selection command.
  One selected tab uses a borderless, bounded `AppIconLabel` heading instead of a
  tab chip; multiple tabs retain the selection-aware tab strip while a task is
  selected. Drafts hide both heading and strip without closing existing tabs;
  task selection and history navigation restore the same retained tab instances.
  See [DRAFT_CONVERSATION_HEADER_ACCEPTANCE.md](CONVERSATIONS.md#draft-conversation-header-acceptance) for native and regression evidence.
  The common task menu can close the selected tab, including the single-tab case.
  Header geometry and typography remain shared variants. `AppWindow` configures
  grayscale text rasterization once, rather than per-view rendering overrides.
- Rich-text view controls inherit `AppSelectableText` rather than native
  `SelectableTextBlock`; a component test enforces this boundary for new views.
- Settings navigation uses `AppSettingsNavButton`; its icon-label layout and
  toggle theme are shared rather than repeated in the settings view.
  `AppActionButton` and `AppSettingsNavButton` both compose `AppIconLabel`,
  keeping their icon size, text alignment, and spacing in one component.
  Non-interactive icon/text metadata can use `AppIconLabel` directly; its
  truncation and alignment stay shared while purpose classes in `Styles/`
  choose the icon size, text size, and color. `ValidateSharedComposedControls`
  prevents views from replacing those controls' content or overriding the
  icon-label layout; functional flyouts remain allowed.
- Layout and framework plumbing such as `Grid`, `StackPanel`,
  `ContentPresenter` and templates may remain native; page and window hosts use
  `AppUserControl` and `AppWindow` wrappers.
  they are not interchangeable visual primitives. Layout primitives cannot own
  backgrounds, borders, corners, shadows, or foreground colors in views;
  `ValidateLayoutPrimitivesRemainStructural` checks this during the App build.
  Use `AppBorder` with a shared surface class for those appearances. Flyout surfaces use
  `AppFlyout` and `AppCard`, including the same border and four-sided shadow.
  List rendering and content hosting use `AppItemsControl` and
  `AppContentControl`; their inherited Avalonia themes remain intact.
- Shared colors, dimensions, focus and hover states, and card shadows belong
  in `App.axaml`; reusable control themes live in `Styles/ComponentVariants.axaml`
  and card, popup, and field variants live in `Styles/SurfaceVariants.axaml`.
  Card and dropdown popup shadows reference the single `SharedSurfaceShadow`
  resource. Shared shadow setters must use that resource or `None`; the App
  build rejects independently defined shadow values and property elements.
  Card content insets use `card-inset-*` variants in
  `Styles/CardInsetVariants.axaml` rather than page-local `Padding`, so card
  spacing stays consistent across settings and flyouts.
  Inspector overview metadata uses shared grouping variants in
  `Styles/InspectorVariants.axaml`; title/status, source heading and source rows
  retain separate layout rhythms under the shared inspector card inset.
  Existing commands and draft state stay view-model-owned. Native light/dark,
  narrow/wide evidence and ROI measurement limits are documented in
  [INSPECTOR_OVERVIEW_FIDELITY_ACCEPTANCE.md](UI_WORKFLOWS.md#inspector-overview-fidelity-acceptance).
  Shared text hierarchy, settings headings, and sidebar typography live in
  `Styles/TypographyVariants.axaml`.
  View text uses shared `type-*`, `weight-*`, and `line-*` classes rather than
  inline font size, weight, family, or line height; the usage test checks that
  every class has a matching shared style. This includes selectable Markdown
  and syntax-highlighted code text.
  Appearance theme thumbnails use one 80-by-60 `AppThemePreview` and reusable
  `AppThemePreviewScene` artwork, not three independently styled mockups.
  `Styles/ThemeThumbnailVariants.axaml` owns miniature conversation geometry and
  ink; `Styles/ThemeModePickerVariants.axaml` owns interactive choice appearance.
  System mode clips two real scenes into 40-by-60 light and dark halves.
  Each scene separates the noninteractive inner-screen shadow from the clipped
  rounded screen content. Both share bounds/corners; only the shadow sibling owns
  SharedSurfaceShadow, preserving the existing artwork and choice hit area.
  [THEME_THUMBNAIL_SHADOW_ACCEPTANCE.md](UI_COMPONENTS.md#theme-thumbnail-shadow-acceptance) records the clipping regression, native
  frames, historical-only metrics and postponed restart/current-reference gates.
  [THEME_THUMBNAIL_ARTWORK_ACCEPTANCE.md](UI_COMPONENTS.md#theme-thumbnail-artwork-acceptance) records native 96-DPI crop comparisons
  and 100/125/200-percent headless compositor checks, not whole-page parity.
  The static appearance diff sample uses `AppDiffPreview` and the same code-font
  resource and shared theme-preview styles; it is not presented as an action.
  Views and reusable controls do not define local styles, including layout-only
  styles. New variants belong in the shared `Styles/` files rather than a view.
  Icon-label compositions use bounded Grid columns and leading-aligned,
  ellipsized text, avoiding infinite horizontal measurement and the centering
  of capped labels. `AppActionButton` reserves separate columns for a badge
  and optional trailing geometry. Sidebar and project-picker actions and composer
  selectors reuse it, preserving their commands, flyouts, selection, and accessible
  names. `Styles/ActionButtonVariants.axaml` owns their semantic sizing and spacing.
  Icon-label truncation limits belong in shared variants as well;
  `ValidateSharedLabelSizing` and `MOSSUI002` reject per-instance `LabelMaxWidth` overrides.
  View markup must not set literal colors or override shared text foregrounds
  and icon strokes; add semantic theme brushes and component classes instead.
  Status warning/error/success and approval badge colors follow this rule in
  both light and dark themes.
  Card and structural border colors, radii, and shadows, plus field colors, borders,
  typography, and padding, must not be overridden on individual view instances.
  Structural `AppBorder` surfaces also use named shared variants; the App build
  rejects inline backgrounds, borders, corners, and shadows in view markup.
  Content padding and sizing used solely for layout remain view-owned.
  The selected project's composer context is a full-width strip above the
  input card using the shared `composer-context-surface` variant. The project
  and agent selectors remain interactive; branch status is separate, passive
  metadata rather than part of the project selector's click target. At window
  heights below 720 DIP, `ComposerResponsiveVariants.axaml` keeps a 20-DIP bottom
  inset and shortens the context strip and input card so the composer remains visible.
  Composer, multiline prose and system-prompt inputs share wrapped, vertical-only
  editing in FieldVariants; single-line and JSON/code inputs retain their separate
  horizontal editing behavior. Composer height remains bounded at 180 DIP.
  The shared checkbox template owns a transparent full-bounds hit surface while
  preserving its centered glyph, native keyboard/three-state behavior and disabled
  state. See [INPUT_HIT_TARGET_ACCEPTANCE.md](UI_COMPONENTS.md#input-hit-target-acceptance) for reproduced failures and native
  light/dark, narrow/wide and unsent-draft verification.
  Its theme-default keyboard adorner uses a non-interactive rounded AppBorder and
  semantic focus brush. Focus does not change the check-surface border thickness,
  avoiding glyph shifts from layout rounding at fractional DPI. Local focus
  ownership remains respected; see [CHECKBOX_FOCUS_APPEARANCE_ACCEPTANCE.md](UI_COMPONENTS.md#checkbox-focus-appearance-acceptance).
  Full settings pages use the shared `settings-page-scroll` variant so their
  inner scrollbar is hidden without disabling wheel or keyboard scrolling;
  modal and conversation scroll surfaces retain their own behavior.
  `AppIcon` size is
  selected through shared `icon-micro`, `icon-small`, `icon-large`, `icon-hero`,
  or `icon-brand` classes; the default size is 14. Views must not set numeric
  icon dimensions inline. Icon-only button hit areas use the shared `icon`,
  `icon-compact`, `icon-micro`, and `icon-copy` variants; window controls and
  navigation buttons have their own shared dimensions. Icon-only buttons render
  `AppIcon` geometry rather than font-dependent Unicode glyphs.
  The collapsed context card floats above the chat. Its measured height offsets
  the scroll viewport only when the viewport is too narrow to place the message
  column beside the card; wide conversations begin below the header. Opening
  the inspector reserves a fixed right column so the
  composer and messages reflow
  instead of being covered. The composer model selector truncates within a shared width
  variant at the minimum supported window size.
  Wrappers that retain a Fluent `StyleKeyOverride` expose an `app-*` class;
  shared styles match that class instead of the wrapper type. A custom
  `ControlTheme` must target the retained native style key.
  `AppComboBox` also styles its Fluent-generated popup and option states through
  `Styles/ComboBoxVariants.axaml`, avoiding an unthemed native dropdown.
  View buttons must not set colors, typography, radii, dimensions, or padding
  inline or through AXAML property elements; use a shared `AppButton` size or
  purpose variant instead. The App build rejects native AXAML controls
  (including shapes), appearance overrides on styled controls, and page-local
  visual styles. Functional property elements such as button flyouts and item
  templates remain allowed.
  `MOSSUI002` rejects per-instance C# appearance assignments, direct styled-property
  writes, bindings and `ClearValue` resets, and page-local style/theme construction
  outside `Controls`. Functional resets and structural layout sizing remain allowed.
  Shared component implementations own their appearance; view code selects classes
  and sets functional properties, data bindings, and structural layout only.
  The shared `danger` button variant resolves theme-specific palette resources
  from `DangerButtonResources.axaml`. Existing disabled treatment, commands and
  click geometry remain shared; settings pages do not supply their own colors.
  `NativeVisualControlUsageTests` additionally guards direct C# construction in App code,
  including namespace aliases. `NativeVisualControlInheritanceTests` rejects native
  visual subclasses outside `Controls`; only structural layout primitives remain exempt.

The workspace shell separates its outer frame shadow from an inner rounded
content clip. Only the outer window page switcher allows shadow rendering above
the workspace row; nested page-switcher clipping is unchanged. Shared card
shadows remain on SharedSurfaceShadow, while the measured SharedWindowFrameShadow
variant is restricted by the build guard to the exact shared frame selector.
See [WINDOW_FRAME_SHADOW_ACCEPTANCE.md](UI_WORKFLOWS.md#window-frame-shadow-acceptance) for pixel/native evidence and the
reproduced, still-unrepaired native restore clipping defect.
[NATIVE_RESIZE_DIAGNOSTIC_PROGRESS.md](UI_WORKFLOWS.md#native-resize-diagnostic-progress) separates failed original-binary captures
from healthy telemetry and fixture-specific screenshot-anchor checks; no
diagnostic hooks or forced-layout workaround are part of the product.

[NATIVE_RESIZE_MESSAGE_PROGRESS.md](UI_WORKFLOWS.md#native-resize-message-progress) records repository-external raw native message
observations without changing handling, layout or product dependencies. Transient
Bounds lag inside resize callbacks is not treated as a settled failure; an actual
DPI-transition/failed-state event history is still required before a root-cause fix.

Settings uses the same navigation rail and rounded frame/clip as the workspace,
with shared sidebar/title/search variants. Return keeps the active conversation
and draft; automation/inspector navigation leaves settings before showing its
destination. Settings selection overrides hidden workspace-page selection and
owner visibility closes rail flyouts. [SETTINGS_SHELL_ALIGNMENT_PROGRESS.md](UI_WORKFLOWS.md#settings-shell-alignment-progress)
separates deterministic rendered checks from still-missing native comparisons.
MainWindowViewModel also subscribes to the catalog's explicit ConversationRequested
event and unsubscribes on disposal: successful task/menu/history navigation leaves
settings, while rejected requests and background catalog refreshes do not. Settings
search filters navigation and empty group headings without selecting a content
page or clearing an unsaved editor. [SETTINGS_NAVIGATION_INTERACTION_ACCEPTANCE.md](UI_WORKFLOWS.md#settings-navigation-interaction-acceptance)
records keyboard, negative-path and state-preservation checks.
[NATIVE_SETTINGS_INTERACTION_PROGRESS.md](UI_WORKFLOWS.md#native-settings-interaction-progress) adds versioned native navigation,
unsent-draft and 96-DPI theme/size replays without model calls. The shared settings
sidebar title uses Bold at its existing 18px/28px geometry; bounded native crops
separate title ink from unchanged search/selection anchors. Failed-state geometry
and an external Headless event simulation narrow, but do not repair, the native
resize defect. No diagnostic hooks or forced-layout code enter the application.

Provider/model edit-session boundaries are recorded in
[PROVIDER_MODEL_EDITOR_SESSION_ACCEPTANCE.md](PROVIDERS.md#provider-model-editor-session-acceptance). Selected-item commands reuse the
catalog-validated row entry points. Opening a new session on the same selected
item reloads saved fields and clears discarded validation; an already-open draft
and ordinary settings-page navigation remain intact. No shared dialog, port,
repository, revision guard, dependency or execution-policy boundary changes.

Shared workspace panes distinguish explicit inspector expansion from effective
full-width presentation. An open inspector automatically takes the host when the
configured primary/inspector minimum widths and splitter cannot fit; widening
restores the retained split without replacing controls or clearing the primary
draft. Headers consume effective state, while preview cleanup consumes explicit
expansion intent to avoid repeated layout-derived toggles. Compact restore is
disabled; the existing close action remains usable. This is shared App layout,
not a tool, persistence or dependency change. That slice retained MainWindow's
minimum pending navigation/settings/composer adaptation.
[NARROW_WORKSPACE_LAYOUT_ACCEPTANCE.md](UI_WORKFLOWS.md#narrow-workspace-layout-acceptance) records deterministic compact coverage,
ordinary-size native replay and the remaining reference/DPI acceptance limits.

Caption palette and geometry live in shared WindowCaptionResources and
WindowCaptionVariants. The caption variant loads after generic ghost-button
styles so normal/hover foreground cannot silently fall back to muted navigation
icons. Disabled, close-hover, Arrow cursor, non-focusability and existing caption
commands retain their boundaries; no per-view appearance or new native port is
added. The icon button exposes its existing alternate geometry state as a
pseudo-class; the caption-only maximize variant does not affect other icons.
[WINDOW_CAPTION_CONTRAST_ACCEPTANCE.md](UI_WORKFLOWS.md#window-caption-contrast-acceptance) separates dedicated palette, render-scale
quantization checks, ordinary-maximize rasterization and native replay from
remaining same-state reference and native high-DPI limits.

AppNavigationPanes now owns the shared workspace/settings navigation layout.
Its 264-DIP sidebar and 360-DIP primary minimum determine a 624-DIP host
breakpoint. Compact mode uses a temporary drawer instead of changing the user's
wide-window sidebar preference. Both shells retain their real controls, drafts
and inspector expansion intent. A drawer blocks underlying pointer hit testing
and primary keyboard traversal without disabling business controls. Selection,
outside left click and unconsumed Escape dismiss it and restore eligible primary
focus. Root/ancestor subscriptions and deferred focus are invalidated on detach,
replacement and unavailable hosts. The same titlebar toggle targets the active
shell, including settings; the header tint follows actual navigation visibility.

The automation page is a sibling of the retained conversation navigation layout,
not its primary pane. It uses the full content frame beside the navigation rail
without changing the conversation's sidebar preference. Its titlebar navigation
toggle and sidebar tint are hidden; settings retain their own navigation rules.
[AUTOMATION_PAGE_LAYOUT_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#automation-page-layout-acceptance) records theme/size/return-path tests and
the user-postponed native screenshot boundary.

MainWindow permits 526x601 after real-consumer compact checks. AppResponsiveColumns
stacks automation list/history when their configured columns cannot fit, preserving
their instances and independent scrolling. Settings dialogs share width/height
constraints through ModalAvailableExtentConverter, also used by approval cards;
their fixed footers remain outside the scrollable body. These changes stay in App
layout/styles and invoke no tools, provider requests or new persistence behavior.
[COMPACT_NAVIGATION_ACCEPTANCE.md](UI_WORKFLOWS.md#compact-navigation-acceptance) records native 96-DPI replay and the remaining
same-state reference, separate native clipping/high-DPI and product boundaries.

Project navigation retains its complete task snapshot while a separate App-owned
preview exposes five existing task rows by default. Expansion is per project and
session; search exposes all matches, and a newly selected hidden task is revealed
after filtering/loading permits it. The preview uses the existing collection
coordinator, does not clone rows or add subscriptions, and never truncates history
or repository data. Shared button typography and localized labels remain in their
existing owners. [PROJECT_TASK_PREVIEW_ACCEPTANCE.md](UI_WORKFLOWS.md#project-task-preview-acceptance) records native replay,
component measurements, regression status and remaining reference/DPI limits.

Project rows with summary flyouts retain the compact navigation drawer while
opening their contextual actions; summary-free selections retain normal dismissal.
Global approval presentation temporarily yields to either compact drawer without
changing request identity, queue, cancellation, browser blocking or execution policy.
UI-to-tool authorization and snapshot/recovery checks are recorded in
[PROJECT_AUTHORIZATION_UI_ACCEPTANCE.md](SECURITY.md#project-authorization-ui-acceptance); native picker and same-state reference
validation remain separate.

When a feature does not fit these boundaries, update the architecture first;
do not work around it with cross-project references or global state.

Settings form rows share AppSettingsRow's adaptive two-column/stacked layout,
retaining controls and drafts across resizing. Catalog empty states and compact
page headings use existing shared components; scrolling and feedback wrapping
remain shared style decisions. [ALL_PAGES_UI_POLISH_ACCEPTANCE.md](UI_WORKFLOWS.md#all-pages-ui-polish-acceptance) records the
cross-page theme/viewport review and its native/reference acceptance limits.
<!-- /source: ARCHITECTURE.md -->


---

<a id="agent-tool-catalog-optimization-acceptance"></a>

## AGENT_TOOL_CATALOG_OPTIMIZATION_ACCEPTANCE.md

<!-- source: AGENT_TOOL_CATALOG_OPTIMIZATION_ACCEPTANCE.md -->
### Agent tool catalog optimization acceptance

#### Design self-check

1. Agent owns model-visible tool projection and child delegation restrictions.
   Keep the existing Application/Tools.Abstractions dependency direction; no
   concrete catalog dependency or UI change in production.
2. Reuse `IToolCatalog` and `IToolExecutor`. Read the current catalog each model
   turn and child invocation, without caching dynamically registered MCP tools.
3. Child requests still enter the standard executor for schema, capability/path,
   approval, timeout/cancellation, output limits and audit. Reject recursive
   delegation case-insensitively before forwarding. Preserve the task identity,
   authorized roots, shared file-version store and parent cancellation token;
   strip only conversation/delegation/progress fields already excluded before.
4. Change two small existing methods, without introducing production types or
   state. New tests retain one handwritten type per file and remain below the
   size limits. Do not change JIT, JSON, packaging or persistence.
5. Prove current-catalog membership, removal/registration, recursive/unknown-tool
   rejection, executor forwarding, approval/output limiting, collection fallback,
   duplicate/order retention and earlier request independence after replacement
   or in-place catalog updates. Guard allocations
   and redundant copying with deterministic regressions, not timing assertions.

#### Changes

- `SubagentToolGateway.ExecuteAsync` checks the requested name against the
  unprojected catalog after excluding `subagent.run`. It no longer builds a whole
  filtered descriptor array just to perform a membership test. The model-visible
  child descriptor projection remains unchanged.
- When delegation is enabled, `AgentRunner` directly copies the catalog rather
  than creating a capturing LINQ predicate that accepts every descriptor. Keep
  independent request snapshots: `IReadOnlyCollection` does not guarantee that
  another catalog implementation cannot mutate its underlying list or array.
  Disabled delegation retains the same case-insensitive filtering, order and
  duplicates, using a non-capturing predicate.
- The runner reads the catalog once per turn. Child calls use the current
  catalog, so registration/removal does not leave stale cached permissions.
  Published arrays are not mutated by either method.

#### Failing baseline and correction

The child allocation baseline failed: 128 membership checks allocated 300,032
bytes, versus 274,432 bytes for the old filtered membership expression alone.
An initial attempt to reuse list snapshots was rejected after two focused
failures proved that aliasing a mutable catalog can rewrite earlier requests.
The final implementation keeps the necessary snapshot copy and removes only
the no-op filtering around it. No catalog ownership assumption is added.

The allocation regression
requires child execution, including its context clone, to allocate less than
half of that redundant projection baseline on the same thread after warmup.
This demonstrates removal of that allocation, not an application-wide speedup
or a benchmark of catalog generation inside the real `ToolRegistry`.

#### Validation scope

The final focused `Subagent|AgentDescriptorProjection|Architecture` run passed
84/84 cases, including 16 new catalog/gateway regressions. The final Agent test
assembly passed 112/112. Scoped whitespace verification and the final solution
build passed, with zero build warnings/errors.

The Roslyn audit
found one type per changed/new C# file; the new test methods are at most 28 lines.
Existing long methods remain below the 80-line hard limit.

The first solution-wide run passed the other seven assemblies, but an unrelated
sidebar allocation assertion observed 1,408 bytes instead of zero. Its isolated
six-case rerun passed without changing sidebar code. That first App run was
manually stopped after 5 minutes 40 seconds (3,451 passed, one failed), so it is
not reported as a completed full-suite acceptance. Standalone diagnostic App
tests subsequently passed 3,739/3,739. An overlapping rebuild encountered the
test worker's DLL locks; after the worker exited, the final solution build passed.
The final-build App assembly then passed 3,739/3,739 with diagnostic timeout
enabled and no dump generated. No sidebar or test-runner implementation was
changed to obtain the passing reruns.

Completed passing assembly runs in this slice: Agent 112, App 3,739, App.Visual
8, Browser 76, Platform.Windows 137, Infrastructure 196, Tools 483 and Providers
516 (5,267 cases in total). These are completed assembly runs, not a claim that
the interrupted first solution-wide command returned success.

This slice changes no visual component or native interaction. It does not claim
new screenshot, real-account, paid-provider or pixel-parity acceptance. The
ongoing product goal and preceding native evidence remain separate.
<!-- /source: AGENT_TOOL_CATALOG_OPTIMIZATION_ACCEPTANCE.md -->


---

<a id="allocation-measurement-acceptance"></a>

## ALLOCATION_MEASUREMENT_ACCEPTANCE.md

<!-- source: ALLOCATION_MEASUREMENT_ACCEPTANCE.md -->
### Allocation measurement isolation

#### Scope and evidence

The ordinary frozen `workspace-regression-20261018` full run reported a 736-byte
delta in `WorkspaceCatalogLookupTests` despite its unchanged zero-byte assertion.
An earlier full run reported 3,552 bytes in `ProviderCatalogLookupTests`. Neither
report is erased by focused passes, and their individual causes are not proven.

A package-free local probe under ignored
`artifacts/gc-allocation-counter-20261018` independently reproduces nonzero
`GC.GetAllocatedBytesForCurrentThread` deltas around a no-inline method containing
only two counter reads and `Thread.SpinWait`. Its `default.json` records:

| Control on .NET 10.0.12 / Windows X64 workstation GC | Result |
| --- | --- |
| Ordinary empty intervals | 32/32 zero |
| Explicit retained `new byte[37]` | 32/32 exactly 64 bytes |
| Empty intervals while a bounded worker requests background GC | 11/192 nonzero |
| Empty intervals in a short no-GC region after the worker stops | 32/32 zero |
| Explicit allocations in the same protected control | 32/32 exactly 64 bytes |

The probe reports `InvalidControls=false`. Exit 1 denotes reproduction of the
unexpected empty-interval delta, not a successful ordinary test run. It does not
identify the cause of every historical test failure.

Public context checked on 2026-10-18:
[dotnet/runtime#134724](https://github.com/dotnet/runtime/issues/134724) and
[the proposed accounting fix](https://github.com/dotnet/runtime/pull/134855).
The PR remains open and unmerged; its description is not proof that the local
runtime is patched or that every application-test delta has this cause.

#### Test-only implementation

App.Tests owns `AllocationMeasurement` and its nonparallel xUnit collection.
This changes no production runtime configuration, GC mode, tool policy, model
selection, persistence or dependency direction, and needs no new application port.

- Callers perform setup and warmup before passing a bounded synchronous action.
- `Measure` reserves a 16 MiB no-GC region, reads the current-thread counter,
  executes the action once and returns its actual delta.
- `finally` ends the region. Failure to start or end it fails the measurement.
- There is no retry, minimum selection, byte subtraction, allowance or skip.
- A dedicated collection disables concurrent test collections in this process;
  it does not change product behavior or other test executables.
- The two catalog test families retain all functional checks, 1,000 warmup
  iterations, 10,000 measured iterations and exact zero-byte assertions.
- Other allocation tests are not silently rewritten or declared fixed.

Six focused controls cover an empty action, real retained allocation, propagation
of an operation failure, rejection of nested measurement, null input and an
explicit collection invalidating the measurement. Release checks prove a later
measurement can start after the successful and rejected paths. A real allocation
must still produce a positive delta; invalid accounting cannot become a pass.

Each new file declares one type and stays below the repository size limits.
Actions are synchronous; this helper is not for async work, long-lived operations
or code intentionally forcing GC.

#### Validation

- `allocation-isolation-20261018/results/focused`: **28/28 passed**, no skips.
- The same candidate's `results/expanded`: **113/113 passed**, no skips; includes
  model menus/reloads, workspace loading, draft/background persistence, settings
  navigation, real authorization tools and both catalog lookup families.
- That candidate's solution build completed with **0 warnings and 0 errors**.
- The subsequent production snapshot-sharing change is a different candidate;
  its own **113/113** expanded App cases pass with no skips. Its validation is
  recorded in [FILE_SNAPSHOT_SHARING_ACCEPTANCE.md](GIT_WORKTREES.md#file-snapshot-sharing-acceptance).

Results are under ignored `artifacts/`. Focused checks are not a green complete
App regression. The old frozen full run remains separate and must be inspected
through its own terminal results. No native UI, real provider, release, commit,
push, AOT, trimming or memory update is claimed.

The frozen ordinary run has since ended with App **5,021 passed / 2 failed /
5,023 executed**, no skips or abort. Its 736-byte allocation assertion remains
one of the two failures. The newer no-GC helper is not present in those frozen
binaries. Session 95889 tests the later thumbnail candidate that includes this
helper; its focused passes are still not a complete-run result.

That newer full run now reports **1,176 bytes** in
`SidebarFilterAllocationTests.WarmUnchangedFilters_DoNotAllocateOrReplaceRows`
for recent/no-matches. This is a different family still measuring with raw
counter reads; no production or test change has been made to its frozen binary.
The newer `history-thumbnails-20261018` source also routes the synchronous
sidebar-filter family through the same helper and nonparallel collection.
All six pinned/recent/project and match/no-match cases retain 100 warmups,
10,000 measured filters, row identity/selection/notification assertions and
the exact zero-byte expectation. No production filter code changed.
The six sidebar cases and six helper controls pass in its 31-case focused run
and 409-case expanded App run;
that does not establish the exact cause of the frozen full-run failure.
The shared helper now covers three families. The other raw-counter tests still
need an explicit boundary/collection audit, not blanket attribution to the
runtime issue or relaxation of zero-byte expectations. In particular, do not
convert async work into an async-void Action or subtract thread-local counters
across an await.

The later provider-stream-boundaries-20261019 complete run (session 97039) reports
3,480 bytes in WorkspaceTaskLookupOptimizationTests.WarmTaskLookup_DoesNotAllocate
for missing=false. That separate raw-counter family was not changed in the frozen
run. Its exact cause remains unconfirmed. An independent dispatch-race candidate
routes it through the existing helper/nonparallel collection while retaining
1,000 warmups, 10,000 measured lookups, reference checks and the exact zero-byte
assertion. Candidate controls and Debug/Release validation are recorded in
CONVERSATION_FOLLOWUP_DISPATCH_ACCEPTANCE.md; they do not erase the original failure
or establish a production lookup defect/fix. This test-only change is now merged:
both original-checkout configurations pass the 52-case App selection, including
the task-lookup family and helper controls. Complete session 55845 is terminal:
8,121 pass and one separate queued-write audit assertion failure, 8,122 total,
no skips or aborts, unchanged source/runtime identities. The allocation checks
pass in that run; the exact zero-byte assertion was not relaxed. This does not
identify the historical raw-counter failure's cause or establish a product fix.
<!-- /source: ALLOCATION_MEASUREMENT_ACCEPTANCE.md -->


---

<a id="app-regression-recovery-acceptance"></a>

## APP_REGRESSION_RECOVERY_ACCEPTANCE.md

<!-- source: APP_REGRESSION_RECOVERY_ACCEPTANCE.md -->
### App regression recovery

This checkpoint removes a deterministic test-harness deadlock that prevented
complete validation of the existing product. It does not establish native UI
acceptance, fix a demonstrated production shutdown failure, or complete the
Codex functional-parity goal.

#### Required design self-check

1. The test harness owns this behavior. Production modules, authorization,
   persistence contracts and dependency direction remain unchanged.
2. Reuse `HeadlessTestSessionExtensions.DispatchAsync`, which returns a non-Task
   generic result through Avalonia's asynchronous dispatch overload. Do not add
   another dispatcher, duplicate callback runner or skip affected tests.
3. Tests use isolated temporary roots and deterministic local data. Keep test
   cancellation, exception propagation and disposal. Diagnostic tools target only
   identity-checked test processes; no user application/window, approval policy,
   credential or paid provider is involved.
4. Handwritten C# retains one type per file and existing size/method limits.
   No UI markup or per-view appearance is changed.
5. Prove the original hanging case, real production-composition cleanup, delayed
   draft flush, callback failure/cancellation and continued session use. Extend
   the existing syntax guard, retaining its async-Action rejection while adding
   Task-valued generic dispatch rejection and positive/negative parser fixtures.

#### Confirmed cause

The original full-solution run was still live, not simply missing a result file.
Its App test process had an idle Headless queue. Managed and async-heap stacks
located the pending chain at:

```text
ConversationHeadingLayoutTests.WithWindow
  -> service-provider asynchronous disposal
  -> WorkspaceViewModel.DisposeAsync
  -> draft StopCoreAsync / RecoverCoreAsync
  -> DrainWritesAsync continuation after a completed Task.Delay
```

The five affected window helpers explicitly selected
`Dispatch<Task>(async () => ...)`, then called `Unwrap`. With TResult equal to
Task, an async Task-returning lambda binds to the synchronous `Func<TResult>`
overload, not `Func<Task<TResult>>`. The Headless dispatcher finishes its operation
when the inner Task is returned. Unwrap waits for that Task outside the dispatcher
operation; it cannot keep the UI loop alive for its pending continuations.
The awaited delay was complete in the dump; this was not evidence of a provider
or SQLite operation blocking. Avalonia's installed API documentation confirms
the two distinct generic overloads.

The existing source guard rejected untyped async-Action dispatch, but did not
inspect `GenericNameSyntax`, so these explicit Task-result calls escaped it.

#### Correction

- Conversation heading, chat header, full inspector, inspector overview and task
  navigation window helpers now use the existing awaiting DispatchAsync helper.
  Their setup, production composition, assertions and cleanup remain intact.
- The syntax guard retains its original untyped async-Action protection and
  additionally rejects async callbacks explicitly returning Task/Task<T> as the
  generic result. Fixtures distinguish both unsafe forms from awaiting bool
  callbacks, DispatchAsync, and an intentionally context-independent cleanup
  Task returned by a synchronous callback.
- A new runtime regression flushes a delayed draft before the Headless callback
  returns, checks the saved text, and then dispatches another callback. Existing
  callback-failure and cancellation cases remain in the focused run.
- The intentional cleanup-retry test still returns a Task through a synchronous
  callback; its cleanup uses ConfigureAwait(false) and is not incorrectly banned.
  No allocation assertion was relaxed, no test was skipped, and no product
  permission or persistence semantics were changed.

#### Evidence

Candidate: `artifacts/app-regression-20261018`, Debug/JIT, 2026-10-18.

| Check | Evidence |
| --- | --- |
| Original full-run identity and wait | Original session 21912, dotnet PID 9992 / App PID 11744, process metadata and repeated live polls |
| Native/async wait chain | `original-app-stacks.log`, `original-async-stacks.log`, `draft-writer-async-fields.log`, `completed-delay-root.log`; the heap dump is a local ignored diagnostic artifact, not a repository deliverable |
| Independent current-code reproducer | `results/before/heading-dispatch-before.trx` and its sequence file: test-host inactivity abort while SingleConversation_UsesAnAlignedBorderlessHeading was running |
| Corrected window/helper regression | **74/74 passed**, `results/headless-dispatch-fixed.trx`, no failures/skips; covers all five affected classes, callbacks and the first extended guard fixtures |
| Final callback/dispatch-guard rerun | **12/12 passed**, `results/final-dispatch-guards.trx`, including the additional async-Action fixture; overlaps the 74-case run and is not added to it |
| Final solution build | `build-final.log`, exit 0, **0 warnings, 0 errors**; includes the additional guard fixture retaining untyped async-Action protection |
| Source/size/whitespace audit | `source-shape-final.json`: 142 changed/untracked handwritten C# files, one type per file, maximum 497 file lines / 67 method lines, no syntax/size violations; `git diff --check` passes |
| Allocation diagnostic | **11/11 passed**, `results/allocation-diagnostic.trx`, with unchanged zero-allocation assertions; an isolated pass is not proof that the earlier full-run allocation failures are resolved |
| Original expanded full-solution regression | Session 96000 ended with exit 1: App has 3,812 passes and four settings/new-task failures, then an inactivity abort during fixture cleanup; the seven other test projects passed. This is not a complete/green App result. See [DRAFT_BACKGROUND_PERSISTENCE_ACCEPTANCE.md](CONVERSATIONS.md#draft-background-persistence-acceptance) for the separate persistence lifecycle fix and current candidates |

The retained failed reproduction is not counted as a passing test. The 74-case
run predates the final additional async-Action detection fixture; the final build,
12-case guard rerun and full run include it. Focused evidence is not a full
App-suite result.

##### Settings navigation follow-up

The expanded run passed the former deadlock point and exposed four
`SettingsConversationNavigationTests` failures (menu/shortcut in both themes).
The old assertion expected an existing task's draft to become the project-new-
conversation draft. That contradicts the independent scope behavior introduced
by the draft workflow: the destination correctly restored the fixture's older
project-new-conversation text instead of copying the task's text.

The follow-up candidate is isolated at
`artifacts/app-regression-20261018-final`; the running original candidate's DLL
is preserved and its SHA-256 is recorded in `full-app-binary-identity.json`.
No production draft behavior is changed to force the old assertion to pass.
The revised test captures the real SQLite destination draft, checks that the
new-conversation entry restores it, edits/saves a distinct destination draft,
checks the original task draft in SQLite, and navigates to both scopes to verify
each is retained. Original theme/menu/shortcut, settings-close, visible-chat,
task-count and zero-provider-request assertions remain.

The follow-up passed **31/31** cases with no failures/skips, recorded at
`artifacts/app-regression-20261018-final/results/settings-draft-navigation.trx`.
That includes all 17 settings-navigation cases, the two independent-draft
navigation cases and the 12 final callback/guard cases. These overlap earlier
focused runs and are not summed as unique tests. The follow-up complete solution
build passed with **0 warnings and 0 errors** (`build-final.log`). Its
`source-shape-final.json` covers 143 changed/untracked handwritten C# files, one
type each, maximum 497 file lines / 67 method lines; whitespace checks pass.

No full-candidate pass is inferred from this focused correction. The original
full run ended with four failures and a later fixture-cleanup abort; its compiled
candidate and results remain unchanged. Native/async diagnostics identified a
separate draft-writer dependency on a UI loop after the correct dispatch had
returned. The follow-up lifecycle fix, current full run and isolated final
candidate are recorded in [DRAFT_BACKGROUND_PERSISTENCE_ACCEPTANCE.md](CONVERSATIONS.md#draft-background-persistence-acceptance).

##### Explicit retirement of irrecoverable test processes

After stack/heap evidence, source inspection and the independent aborted
reproducer confirmed the same deadlock, only identity-checked owned test
processes were retired. This was not a restart based solely on observation time.
`retired-test-processes.json` records the old App process, the queued PowerShell
job waiting for that obsolete candidate, and the orphaned reproducer child.

The queued session 11377 ended with exit -1. The old solution run then finished
with exit 1; its partial App record contains 2 allocation failures and 2,855
passes before the fatal test-process retirement. Its other project results do
not make that App run complete or green. All original logs/results are retained.
The newly built full run is a different candidate with the dispatch correction,
not a relabeling of the old failed run.

#### Reproduction

```powershell
dotnet build MossAgent.slnx --artifacts-path artifacts/app-regression-20261018 --nologo -v minimal
dotnet test tests/MossAgent.App.Tests/MossAgent.App.Tests.csproj --artifacts-path artifacts/app-regression-20261018 --no-build --filter 'FullyQualifiedName~HeadlessAsyncDispatchTests|FullyQualifiedName~HeadlessDispatchUsageTests|FullyQualifiedName~ConversationHeadingLayoutTests|FullyQualifiedName~ChatHeaderInspectorTests|FullyQualifiedName~InspectorFullViewWorkflowTests|FullyQualifiedName~InspectorOverviewGeometryTests|FullyQualifiedName~TaskNavigationRowTests' --blame-hang-timeout 90s --blame-hang-dump-type none --logger trx --results-directory artifacts/app-regression-20261018/results/recheck --nologo -v minimal
dotnet test MossAgent.slnx --artifacts-path artifacts/app-regression-20261018 --no-build --no-restore -m:1 --blame-hang-timeout 3m --blame-hang-dump-type none --logger trx --results-directory artifacts/app-regression-20261018/results/full-recheck --nologo -v minimal
```

Diagnostic dotnet-stack/dotnet-dump 10.0.750501 were installed only under ignored
`artifacts/diagnostic-tools`, not globally. No commit, push, publish, AOT, trimming
or memory update was performed. The product gap inventory remains in
[FUNCTIONAL_PARITY_MATRIX.md](OVERVIEW.md#functional-parity-matrix).

#### Background-persistence candidate terminal result

The later frozen `draft-background-20261018-fixed` full run (session 57131)
completed with exit 1 rather than hanging or being aborted: App executed 5,019
cases, with 5,014 passes and five failures. The other seven projects passed.
The failures concern allocation, agent selection, two authorization scenarios and
model-menu initialization; their exact reports and follow-ups remain in
[DRAFT_BACKGROUND_PERSISTENCE_ACCEPTANCE.md](CONVERSATIONS.md#draft-background-persistence-acceptance). Passing the former cleanup point
does not establish native application shutdown or a green product regression.

The newest `workspace-regression-20261018` candidate passes 99 focused cases,
ten separate model-search repetitions, a zero-warning/error solution build and
the 148-file type/size audit. Diagnostic session 89190 was invalidated and its
owned process tree retired after the .NET 10 hook prevented .NET 9 PowerShell
children from starting. Baseline and hooked commands reproduce the environment
failure; the four affected MCP/terminal cases pass without instrumentation.
Ordinary full session 54206 is now the only complete regression running, using
the same frozen binaries with no diagnostic hook. Its log/results remain under
`full-uninstrumented`; no old failure is erased by isolated passes, and no normal
user window was operated.

Further isolated candidates add full Workspace initialization waits to two
model-picker tests and test-only no-GC allocation measurement; 113 expanded App
cases pass with the original behavior/zero-byte assertions. A separate confirmed
internal-reader/atomic-edit defect is fixed in the production snapshot reader,
with 499 tool cases passing, zero solution-build warnings/errors and a clean
157-file source-shape audit. See [ALLOCATION_MEASUREMENT_ACCEPTANCE.md](ENGINEERING.md#allocation-measurement-acceptance) and
[FILE_SNAPSHOT_SHARING_ACCEPTANCE.md](GIT_WORKTREES.md#file-snapshot-sharing-acceptance) for candidate paths, earlier failed reports,
positive/negative controls and the remaining full-regression/native boundaries.
None of these focused results establishes a terminal full-run pass or identifies
the direct cause of the two older authorization `tool_failed` reports.

Ordinary session 54206 has subsequently reached its terminal result: **5,021
App passes / 2 failures / 5,023 executed**, no skips, hang or abort; the other
seven projects pass. Its only failures are model initialization and a 736-byte
allocation assertion. All eight authorization cases pass before the later
snapshot-reader change, without proving why their older intermittent reports
failed. The terminal aggregate is `full-uninstrumented-terminal.json`.

The newest candidate adds bounded composer/draft thumbnails and includes the
intervening fixes. Its 344 Infrastructure and 82 expanded App cases, zero-warning
solution build and 168-file shape audit pass. Session 95889 is its only ordinary
complete regression, started after 54206 ended, with frozen binaries under
`artifacts/attachment-thumbnails-20261018`. See
[ATTACHMENT_THUMBNAIL_ACCEPTANCE.md](CONVERSATIONS.md#attachment-thumbnail-acceptance); this run's terminal result is not yet known.
<!-- /source: APP_REGRESSION_RECOVERY_ACCEPTANCE.md -->


---

<a id="catalog-collection-optimization-acceptance"></a>

## CATALOG_COLLECTION_OPTIMIZATION_ACCEPTANCE.md

<!-- source: CATALOG_COLLECTION_OPTIMIZATION_ACCEPTANCE.md -->
### App catalog and collection optimization

#### Design self-check

- App owns both helpers. Existing configuration repository ports remain the only
  data access; dependency direction and UI control wrappers are unchanged.
- Collection synchronization retains equal rows and occurrence ordering. A shared
  prefix is skipped; append, tail removal and unchanged snapshots need no membership
  set. Mixed changes still use the existing membership/removal/move algorithm.
- Model loading filters enabled providers and matching enabled models directly from
  indexed repository snapshots, without per-provider closures and LINQ iterators.
  Output order, duplicate records, independently created picker state and stable
  agent-option label sorting remain unchanged.
- No new I/O, approvals, execution policy, timeout, task ownership, persistence,
  secrets, JSON serialization, publication or appearance behavior is introduced.
  Existing cancellation checks and cancellable repository waits remain in place.
- Each handwritten C# file contains one type. Changed methods stay below 40 lines.
- Focused tests cover every transition among 121 short duplicate-value sequences
  (14,641 pairs), nulls, equal references, move/add/remove notifications, disabled
  and foreign models, ordering, cancellation and prior-snapshot isolation.

#### Baseline

The new focused tests ran against the pre-optimization implementation on Windows
x64, .NET 10 Debug/JIT. Behavioral tests passed; two allocation guards failed:

| Warm scenario | Iterations | Bytes before | Bytes after | Reduction |
| --- | ---: | ---: | ---: | ---: |
| Append/remove a tail item from 32 rows | 1,000 cycles | 1,680,000 | 192,000 | 88.57% |
| Read 16 enabled providers with 64 disabled models each | 1,000 reads | 5,048,000 | 752,000 | 85.10% |
| Synchronize an unchanged 32-row snapshot | 10,000 reads | 0 | 0 | Already allocation-free |

Measurements use thread allocation counters after 100 warm-up iterations. The
catalog fixture returns completed cached tasks, excluding repository allocation
and network/disk costs. Collection measurements include required observable
notifications; no-change synchronization was already allocation-free on .NET 10.
These are bounded microbenchmarks, not claims about total application performance.

#### Verification

- Focused catalog, picker, sidebar, agent and subagent regressions: 530/530 passed.
- Full solution build: zero warnings and errors.
- `dotnet format whitespace` verification for the five changed C# files: passed.
  Scoped `git diff --check` also passed.
- Changed files: at most 176 lines, one type per file; changed methods below
  40 lines. No shared appearance or native component boundary was modified.
- Full App regressions: 3,622/3,623 passed. The sole failure was an `IOException`
  at `WorkspaceConversationRestartTests.cs:75`, while removing its temporary
  directory after the model/agent restoration assertions and shutdown succeeded.
  Three subsequent isolated runs of that class passed 3/3 each, without production
  or cleanup-code changes. Whole-suite cleanup reliability is not established;
  those retries do not convert the failed full-suite run into a pass. All three
  allocation measurements were identical in focused and full-suite runs.
- This slice does not include a fresh native-window screenshot, live provider
  calls, Release/AOT publication or completion of the separate native subagent
  lifecycle acceptance. The prior temporary lifecycle host was closed normally
  before rebuilding (exit 0, parent/child request counts 0/0).

Evidence is repository-external:
`%TEMP%\MossAgent-catalog-collection-optimization-20261011`.

#### Provider/editor identity lookup cleanup (2026-10-12)

##### Design self-check

- App's existing provider/model catalog owns current-snapshot identity lookup;
  repository I/O stays behind `IConfigurationRepository`. No dependency changes,
  new service, runtime package, cache or persistence format are introduced.
- Provider and model editors reuse the lookup instead of repeating capturing
  LINQ predicates. Row actions resolve the current provider once, and model
  deletion checks reuse the editor's existing target-availability validation.
- Null and absent identities return no target. First-occurrence ordering,
  disabled-provider editability and provider/model identity pairs stay unchanged;
  a missing model in the first matching group never falls back to a duplicate
  group. Moves and removals take effect immediately without cache invalidation.
- Existing catalog-read deadlines, cancellation, disposal, mutation ownership,
  stale-result rejection, draft preservation and unavailable-target checks remain
  in the owning editors. No tool, authorization, approval or secret handling changes.
- The three production files have 151, 487 and 494 lines; the test file has 167.
  Each has exactly one type. Changed methods stay below 40 lines; the largest
  existing method remains 68 lines, below the 80-line hard limit.

##### Allocation baseline

The pre-change behavioral cases pass, while all four new allocation guards fail.
The final seven lookup cases pass, including the added current-order regression.
Measurements use 1,000 warm-up iterations and 10,000 synchronous checks on the
current thread, with all fixtures constructed outside the measured region.

| Warm check sequence | Bytes before | Bytes after |
| --- | ---: | ---: |
| Provider commands/availability, present target | 5,120,000 | 0 |
| Provider commands/availability, missing target | 5,120,000 | 0 |
| Model commands/availability, present target | 26,800,000 | 0 |
| Model commands/availability, missing target | 10,000,000 | 0 |

These are binding/command lookup measurements, not total UI allocations or an
end-to-end latency benchmark. Catalog loading and required observable events are
outside the measured region.

##### Verification

- Full App suite: 3,694/3,694 passed, including stale and duplicate rows, ownership,
  unavailable targets, catalog refresh, draft, read deadline and disposal coverage.
- Architecture constraints: 7/7 passed. The rebuilt repository-external Roslyn
  audit confirms the file, method and one-type limits above.
- Solution build after tests finish: zero warnings and errors. An earlier build
  overlapped the full suite and failed copying its locked App DLL; that failure
  is retained separately and required no production or test-policy changes.
- Scoped whitespace verification and diff checks pass. No AXAML or shared visual
  appearance changes, fresh native-window screenshot, paid/live provider request,
  full-solution test run, commit, push or publication is claimed. JIT debugging
  and Windows x64 single-file release settings remain untouched.

Evidence and the per-turn production-file baselines are repository-external:
`%TEMP%\MossAgent-catalog-lookup-cleanup-20261012-083839`.

#### Model search projection cleanup (2026-10-13)

##### Design self-check

- App owns group filtering and picker projection. Existing configuration ports,
  model availability checks and shared collection reconciliation stay in place;
  there are no new dependencies or cross-editor caches.
- Group filtering visits each member once. A changed partial match lazily builds
  immutable membership; equivalent matches retain the previous group. Whole and
  empty matches retain their original semantics, including empty provider groups.
- The picker compares published groups by occurrence before building a changed
  snapshot. Unchanged results allocate no projection list; a shortened matching
  prefix only removes the tail. Mixed changes use existing observable synchronization.
- No I/O, tool execution, approval, authorization, timeout, cancellation, persistence,
  secret handling, JIT or Windows x64 single-file publication behavior changes.
  Existing tests still reject stale loads, disabled models and foreign identity pairs.
- Changed production files have 64 and 453 lines with one type each. Changed
  production methods have at most 22 lines; new test methods have at most 42,
  below the 80-line hard limit. Regressions cover duplicate row occurrences,
  shortened and expanded prefixes, immutable earlier snapshots, empty groups,
  unchanged collection notifications, selection and rendered focus.

##### Allocation baseline

The new five behavioral cases pass against the pre-change implementation; all
eight allocation guards fail. The final focused picker/catalog/collection run
passes 168/168 cases, including those guards. Measurements use thread allocation
counters after 100 warm-up searches, with fixtures outside the measured region.

| Equivalent search result | Picker bytes before | Picker bytes after | Group bytes before | Group bytes after |
| --- | ---: | ---: | ---: | ---: |
| Partial model matches (`target` / `unique`) | 2,944,000 | 120,000 | 208,000 | 0 |
| Whole model matches (`shared` / `model`) | 5,920,000 | 120,000 | 456,000 | 0 |
| Empty matches (`absent` / `missing`) | 2,440,000 | 120,000 | 176,000 | 0 |
| Provider matches (`fixture` / `FIXTURE`) | 736,000 | 120,000 | 24,000 | 0 |

Each row measures 1,000 alternating synchronous searches. The picker fixture has
12 providers and 32 models per provider; the group fixture measures one provider.
Required property notifications are still included in picker allocations. These
are warm projection microbenchmarks, not total App memory or latency measurements.
Changed results still allocate their necessary immutable/observable snapshots.

##### Verification

- Full App regressions: 4,067/4,067 passed, including all 13 new cases, the four
  light/dark search-focus cases and 13 shared visual-component boundary cases.
- Separate visual-layout regressions: 12/12 passed after the final solution build.
- Architecture constraints: 7/7 passed. Scoped syntax auditing confirms one type
  per file, the production sizes above and all changed methods below hard limits.
- Solution build: zero warnings and errors. Scoped whitespace verification passes
  after correcting one test initializer's line wrapping; scoped diff checks pass.
- This is not a full-solution test run. Existing native window-restore/reflow
  acceptance gaps are unchanged; passing headless tests does not resolve them.

No AXAML, shared appearance or native-window behavior is changed in this slice.
Headless shared-view focus, selection and light/dark tests pass in the focused run;
fresh native screenshots, paid/live provider requests and publication are not claimed.

Evidence and production-file baselines are repository-external:
`%TEMP%\MossAgent-model-search-cleanup-20261013-075416`.
<!-- /source: CATALOG_COLLECTION_OPTIMIZATION_ACCEPTANCE.md -->


---

<a id="configuration-mutation-acceptance"></a>

## CONFIGURATION_MUTATION_ACCEPTANCE.md

<!-- source: CONFIGURATION_MUTATION_ACCEPTANCE.md -->
### 配置写入、重试与草稿保护验收

#### 设计自检

1. 行为归 App：目录共享 `SettingsMutationRunner`，编辑器复用
   `SettingsEditorViewModel` 的草稿版本，模型导入由 `ModelDiscoveryImporter`
   拆分。UI 只调用已有配置仓库端口，依赖方向不变。
2. 复用 `IConfigurationRepository` 与现有模型发现端口，不建立新持久化协议、
   SQL 或 HTTP 访问路径，没有迁移。
3. 写入关联所属编辑器生命周期并保留仓库取消语义。提供商、模型和发现结果的
   保存不能重叠；失败只呈现本地化安全提示，不回显异常、API Key 或凭据。
   写入不通过超时后放弃等待来冒充成功，工具授权、审批、超时和输出策略不变。
4. 新类型各自独立成文件，无 partial 拆分；最终体积审计另行检查所有 C#/AXAML。
5. 正向覆盖保存、重试、导入与删除；负向覆盖存储失败、跨编辑器竞争、
   生命周期取消、迟到完成和验证期间草稿改变，另检查实际页面命令。

#### 复现与修复

修复前，新增四项保存/删除失败回归全部实际失败，配置仓库的合成 IOException
穿透提供商和模型命令，没有安全状态转换。修复后命令不抛出该异常，
失败不关闭编辑器、不清空草稿或选择，可继续重试。

- 同一目录中的写入和发现结果导入共用互斥状态；提供商写入时模型修改也禁用。
- 保存捕获目标和字段；重新打开或编辑草稿后，旧成功不会关闭或重置新编辑器。
- 提供商验证代理期间若名称或地址改变，拒绝混用旧 URL 和新字段，提示重新保存。
- 删除另外一条模型不影响当前打开的草稿；成功删除原编辑目标关闭该编辑器。
- 释放所属设置取消等待中的协作式仓库写入，不发布迟到结果；模型编辑器释放
  不取消独立提供商编辑器。没有更改 SQLite 的原有原子写入契约。
- 发现结果无法取得写入权时给出明确重试状态，不遗留“正在获取模型”。

#### 自动化证据

新增 18 项：4 项安全保存/删除失败，10 项草稿、互斥、取消、其他模型删除、
发现导入竞争和代理验证竞争，以及 4 项明暗主题实际 Headless 页面保存/重试。
页面通过 Space 触发真实按钮命令，确认安全提示可见、弹窗保留、重试后关闭。

最终专项 131 项通过；完整解决方案 1,899 项通过，其中 App 1,410 项，
无失败或跳过。解决方案构建 0 警告、0 错误。
相关 C# 空白格式检查通过，第一次仅发现新页面测试的对象初始化换行，修正后复核。
本轮审计 883 个 C#/AXAML 文件，类型、文件和方法体积没有违规，差异空白检查通过。

#### 实际 Windows 输入与截图

普通权限临时宿主复用真实主窗口、设置页面和共享组件，内存仓库模拟存储异常。
仅操作明确绑定的临时 MossAgent 进程，不操作 Codex/ChatGPT 界面、权限策略、
终端、用户数据或真实凭据，不调用在线模型。

- `@oai/sky` 实际点击模型编辑入口、名称字段，核对焦点后通过系统键盘
  Ctrl+A 和文字输入写入“系统键盘输入验收模型”。屏幕与后续控件树确认该值。
- 实际点击保存后出现安全错误，窗口继续存在，同一草稿保留，按钮可重试。
- 移除夹具故障后，实际再次点击保存，弹窗关闭，提供商分组内模型行显示新名称。
- Python 被动 PrintWindow 保存原始 980×700 的 `save-failed-draft.png` 和
  `save-retried-row.png`；窗口已正常关闭，日志记录普通权限和退出码 0。
- 首次未请求截图的输入缺少坐标几何，重新绑定并取得非黑截图后点击成功。
  早先一次按键报 foreground process 错误，之后核对到进程已正常结束，
  新建更长验收窗口复测；不把失败输入或瞬时旧控件树当作成功。

证据根目录：
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-mutation-qa-20261006-01`。
临时宿主源码继续保留在前轮仓库外目录 `MossAgent-catalog-loading-qa-20261006-01`，
没有将截图、脚本或合成数据库加入仓库，未暂存、提交或推送。

#### 范围限制

故障由内存仓库注入，不宣称已经在真实磁盘故障中验证 SQLite。
取消测试要求仓库协作取消，不把取消令牌当作能阻止任意不协作实现的晚到磁盘提交。
本次系统输入验证仅覆盖模型配置保存/重试，没有证明所有设置页、在线发现、
全部 DPI、主对话、审批和浏览器交互已通过，更不是 Codex 全界面像素一致验收。
完整产品目标仍未完成。

#### 异步命令去重（2026-10-10）

设计自检：

1. 行为仍归 App。泛型命令复用已有非泛型命令的执行状态、事件转发、取消与释放，
   不新增运行时服务、全局状态或生产文件。
2. 继续实现现有 `IAsyncRelayCommand` / `IAsyncRelayCommand<T>`，调用方无需迁移；
   类型化入口仍调用类型化 Toolkit 命令，不改用 object 转换或额外参数装箱。
3. 保持禁用和运行中拒绝重入、协作取消、执行所有权与异常传播。
   不改仓库端口、业务超时、审批、授权、输出限制、持久化或 JIT 发布设置。
4. 两个手写文件仍各自只有一个类型，未使用 partial 或增加通用基类文件。
5. 对两种命令共同验证禁用、重入、事件发送者、异常后重试、取消与幂等释放；
   另覆盖类型化/object 入口、值类型、null 与错误参数拒绝。

重构前，同一组 17 项行为测试通过，作为保留既有契约的基线。

重构后验证：

- 同一组命令测试 17/17 通过；两个生产文件合计由 101 行减至 74 行，净减 27 行。
- 解决方案构建 0 警告、0 错误。三个相关 C# 文件的空白格式、单类型、
  文件/方法体积审计，以及相对本轮开始快照的差异空白检查通过。
- 本轮一次完整并行测试通过 3,873/3,874，无跳过。App 2,940/2,941，
  其余 Agent 63、Browser 76、Infrastructure 144、Platform.Windows 129、
  Providers 302、Tools 219 全部通过。
- 唯一失败是 `WorkspaceCommandFlowTests.ApprovedParentExit_ReclaimsBackgroundChildBeforeConversationCompletes`
  在读取合成 `child.pid` 时发生文件占用异常。该夹具仅等待文件存在后立即读取，
  子进程以 `WriteAllText` 发布 PID；本轮未修改该夹具、命令执行或进程回收代码。
  单独复测 1/1 通过。保留首次并行失败记录，不将单独通过视为文件占用竞态已修复。
- 未修改视觉控件、AXAML、样式或发布配置；本轮未重新进行原生窗口截图验收，
  不宣称新增像素一致结果。所有既有改动保留，未暂存、提交、推送或发布。

本轮日志、前后快照和 TRX 位于仓库外：
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-command-refactor-20261010`。

#### 新草稿提交身份与迟到失败（2026-10-10）

设计自检：

1. 行为仍归 App 的提供商和模型编辑器，目录及写入互斥职责不变。
2. 只复用 `IConfigurationRepository`、目录刷新和编辑器草稿版本，不新增
   仓储端口、SQL、HTTP、数据库迁移或跨模块依赖。
3. 沿用写入生命周期和仓库协作取消；旧保存/删除失败只能更新对应的、仍存活
   且未改变版本的草稿。错误仍使用安全资源文本，不回显异常或密钥。
   本轮未增加写入期限或用户取消入口，不改工具授权、审批和输出限制。
4. 每个新草稿只生成一个 GUID，重试复用该身份。成功提交后通过现有字段通知
   绑定已提交快照，不调用会覆盖字段的编辑器加载流程，也不新建通用状态框架。
5. 回归同时验证重试更新、保留最新字段和合成密钥、外部删除后拒绝复活、
   提交后报错、保存中另开新草稿，以及旧保存/删除失败不能污染新草稿。

修复前新增 12 项回归中，10 项因真实行为断言失败，2 项通过；不是编译失败。
修复后专项 53/53 通过，包含新增 12 项、既有写入竞争/失败、明暗 Headless
页面、提供商/模型设置和代理读取生命周期回归。

- 保存等待期间修改新草稿，首次提交后表单和最新字段保留，草稿绑定已提交
  身份；再次保存更新同一条配置，不新增第二行。
- 提供商首次成功提交后，密钥输入留空仍保留该已提交快照中的密钥。
- 已提交草稿对应配置被外部删除并刷新目录后，保留草稿并禁用保存，不复活目标。
- 仓储提交后才报告失败时，重试复用原 GUID；不把失败本身当成提交成功。
- 保存中另开新草稿会生成独立身份，旧完成不能将其绑定到旧配置；旧保存/删除
  失败也不会改变新草稿的状态或错误提示。

##### 原生 Windows 与真实 SQLite

仓库外普通权限宿主使用真实主窗口、设置页面、共享控件和 SQLite 仓储。
只操作明确绑定的 `MossAgent.DraftQa.exe` 验收窗口，使用合成配置与合成密钥，
不读取用户数据库、不调用在线模型、不操作 Codex/ChatGPT 界面。

端口包装器仅在第一次写入前设置受控 gate；F8 是仓库外宿主释放 gate 的
测试按键，不是 MossAgent 产品功能。正常保存与重试均走生产编辑器和真实仓储。

- 浅色 1280×820：实际新建提供商、保存等待时改名、释放 gate，再清空合成
  密钥输入并重试。表单关闭，目录只有基础提供商和一条修改后提供商。
  关闭后白名单快照确认两次写入身份相同、持久化身份一致且合成密钥保留。
- 深色 980×820：同一数据根恢复已保存提供商；实际添加模型、保存等待时
  修改显示名称、释放 gate，再保存。目录仅有一条修改后模型；关闭后的快照
  确认两次模型写入身份相同、持久化身份一致，提供商和密钥仍正确。
- 再次重启深色窗口，只读展开分组，真实页面恢复修改后提供商和修改后模型。
  此轮没有保存，快照中的 `SavedTwice` / `SameIdentity` 等写入观测值为 false
  是预期；先前写入证据保留在独立的 `dark-mutation-safe.json`，不覆写结论。
- Python `--passive --include-popups` 保存两种窗口尺寸的原始非黑截图，
  包括等待中已修改草稿、首次提交后草稿、重试后的唯一配置行和重启恢复。
  三次验收进程均正常关闭，日志记录普通权限与退出码 0。

证据根目录：
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-configuration-draft-20261010`。
宿主源码、截图、脚本、日志和合成数据库均在仓库外，未暂存、提交或推送。

##### 最终工程检查

- `dotnet build MossAgent.slnx --no-restore -v:minimal`：0 警告、0 错误。
- `dotnet test MossAgent.slnx --no-build --no-restore --logger trx`：一次完整运行
  4,017/4,017 通过，无失败、跳过；其中 App 3,054，其余六个测试项目 963。
  使用默认并行和既有期限，没有通过重试或降低并发调整结果。
- 五个相关 C# 文件定向 `dotnet format whitespace --verify-no-changes` 通过。
  全部 1,392 个手写 C#/AXAML 的 Roslyn 结构审计通过：单类型、文件不超过
  500 行、方法不超过 80 行；两个设置 ViewModel 分别为 482 和 492 行。
  旧仓库外审计宿主初次缺少 Roslyn 运行依赖，正常重新构建宿主后检查通过，
  未为此修改产品源码。
- 已跟踪差异与三个相关未跟踪 C# 文件的空白检查通过；暂存区仍空，
  没有遗留本轮原生验收进程，没有提交、推送或发布。

##### 范围限制

本轮不宣称修复所有配置读取/通知的迟到状态、所有写入取消/期限或真实磁盘故障。
外部删除拒绝依赖成功刷新后的目录快照，不是新的仓库乐观并发或跨进程锁契约。
未验证提供商提交后报错且随后清空密钥的组合，也未验证在线模型发现、所有设置页、
全部 DPI 或 Codex 全界面的像素一致性。Windows x64、JIT 调试和单文件发布设置不变，
未恢复 AOT。完整产品目标仍未完成。

#### 提交结果恢复与模型身份更新（2026-10-10）

设计自检：

1. 草稿恢复仍归 App 编辑器和共享目录；模型行的原子更新与提供商归属检查
   归 Infrastructure 仓储，不让 UI 直接访问文件或 SQL。
2. 复用 `IConfigurationRepository`、目录刷新和稳定草稿 GUID，不新增端口、
   状态框架、数据库迁移或模块依赖。
3. 写入仍关联所属生命周期并保持互斥。失败只触发一次有既有 30 秒期限的
   目录读取，不自动重放写入、不把读取成功当作保存成功。读取失败阻止重试，
   后续显式刷新成功后才恢复保存。授权、审批、输出限制和安全错误文本不变。
4. 三个生产文件各含一个类型，仍低于 500 行；未用 partial 拆分或修改发布配置。
5. 正向覆盖提交后恢复身份、保留最新草稿与密钥、修改 API 模型 ID；负向覆盖
   读取失败、删除后拒绝复活、旧完成与新草稿隔离、重命名冲突和跨提供商身份。

##### 复现与修复

新增 15 项中，修复前 App 10 项有 6 项实际断言失败，SQLite 5 项有 3 项
实际失败，均完成编译。SQLite 不只是拒绝合法改名：冲突路径还可能更新另一条
已有模型，使用另一提供商模型身份时也可能命中语义冲突并修改目标行。

- 保存提交后端口报错，重新读取目录并按当前草稿 GUID 恢复编辑身份；
  不调用覆盖字段的加载流程，不关闭弹窗或清除安全失败提示。提供商输入密钥
  留空的重试使用已读取快照中的原密钥，补齐上一节未验证的组合。
- 恢复读取失败时目录显示安全提示并禁用写入；显式刷新成功可绑定已提交身份，
  再由用户重试。失败前尚未提交的草稿仍可正常新建；另开的新草稿不绑定旧身份。
- 已恢复身份的目标被外部删除并刷新后，保存仍拒绝复活该配置。
- SQLite 优先按 `models.id` 更新，同一提供商内允许修改 API 模型 ID，
  保留 GUID 和其他参数。改名碰撞另一行会失败，并在同一事务中回滚默认标记；
  不把两条模型合并，也不修改另一行。
- 稳定模型身份属于另一提供商时拒绝写入，即使请求的 API 模型 ID 已存在，
  两个提供商的行及默认标记都保持不变。新 GUID 的既有语义 upsert 行为保留。

修复后专项 75/75 通过：App 69，SQLite 6，包含新增 15 项与既有草稿、
写入竞争/失败、设置页面、模型分组和生命周期回归。

##### 原生 Windows 与真实 SQLite

普通权限的仓库外宿主加载真实 App 页面、组件和 SQLite 仓储。包装端口只在
第一次真实提交返回后抛出合成异常；不是模拟 SQLite 提交，也不是产品故障开关。
不调用在线服务、真实账号或用户数据库，仅操作绑定的 `MossAgent.RecoveryQa.exe`。

- 浅色 1280×820：实际创建提供商，首次保存真实提交后报错。窗口保留草稿与
  安全错误，标题切换为编辑，出现删除入口和“留空保留原密钥”提示。实际修改
  名称并清空合成密钥输入，重试后弹窗关闭、列表只有两行（基础行加新建行）。
  关闭后的白名单快照确认只注入一次失败、两次写入 GUID 相同且原密钥保留。
- 深色 980×820：恢复前轮提供商，实际添加模型，首次真实提交后报错。
  实际将 API 模型 ID 从 `fixture-native-old-model` 改为 `fixture-native-new-model`
  并修改显示名称，再次保存成功。列表仅一条新 ID 模型，白名单快照确认
  两次写入 GUID 相同、持久化身份一致且提供商密钥仍保留。
- 再次重启深色窗口，只读展开分组，页面恢复新模型 ID 和显示名称。
  此轮未保存，写入观测值为 false 是预期；前轮写入快照另存为
  `dark-mutation-safe.json`。三个进程均正常关闭，退出码 0，未遗留宿主。
- Python 被动截图保存首次提交后失败、密钥留空重试、模型改名重试、成功列表
  及重启状态，原始两种尺寸均非黑。没有将截图或脚本加入仓库。

本轮证据根目录：
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-configuration-recovery-20261010`。

##### 最终工程检查

- 解决方案构建 0 警告、0 错误；一次完整默认并行回归 4,032/4,032 通过，
  无失败或跳过。其中 App 3,064、Infrastructure 149，其余五个测试项目 819。
  没有改测试并行、业务期限、默认池化或添加重试来调整结果。
- 六个相关 C# 文件的定向空白格式检查通过；全库 1,395 个手写 C#/AXAML
  的单类型、文件不超过 500 行和方法不超过 80 行的 Roslyn 审计通过。
  提供商/模型编辑器分别为 488/492 行，配置仓储 256 行。
- 本轮开始源码快照与生产差异已复核，所有已跟踪差异和相关未跟踪文件
  空白检查通过。暂存区仍为空，没有残留验收窗口或进程。

##### 范围限制

端口异常由测试包装器注入，不宣称验证了真实磁盘损坏。恢复读取不是跨进程锁或
仓库乐观并发契约；未观测到的提交后立即删除不能仅靠读取推断其历史。
本轮未改变新 GUID 命中已有 API 模型 ID 的语义 upsert，也未证明这类重复新建
在所有编辑器竞争中均已处理。未新增写入取消或期限，未覆盖所有设置读取通知、
在线发现、全部 DPI 或 Codex 全界面像素一致性。JIT 和 Windows x64 单文件发布
保持不变，整体产品目标仍未完成。所有既有改动保留，未暂存、提交、推送或发布。
<!-- /source: CONFIGURATION_MUTATION_ACCEPTANCE.md -->


---

<a id="context-building-optimization-acceptance"></a>

## CONTEXT_BUILDING_OPTIMIZATION_ACCEPTANCE.md

<!-- source: CONTEXT_BUILDING_OPTIMIZATION_ACCEPTANCE.md -->
### Context building optimization acceptance

#### Scope and boundaries

`ModelContextWindowBuilder` reads conversation turns from newest to oldest and
materializes only the groups it examines before reaching the context budget.
It no longer builds arrays and a group list for discarded older history.
System messages still reserve the same budget and precede conversation messages
in their original order. The first turn that cannot fit still stops selection;
the builder never skips it to include an older, smaller turn.

`SubagentConversationContextBuilder` finds the newest twelve relevant messages
by index, then renders them in conversation order. It keeps the same eligible
roles, whitespace filtering, untrusted observation labels, 12,000-character
limit, truncation marker and surrogate-pair boundaries. Empty context returns
null without constructing a string builder.

Both changes belong to Agent and use existing model contracts. There are no new
dependencies, caches, persistence formats, provider parameters or UI styles.
Authorization, approvals, cancellation, tool execution, signature ownership and
JIT/Windows x64 single-file publishing settings are unchanged.

#### Regression evidence

- The three focused context/continuation suites pass 33/33 tests.
- New cases cover mixed system placement, duplicate message identity, independent
  snapshots, empty/system-only input and an older turn with oversized metadata.
- Signed continuation content remains intact; oversized latest metadata remains
  rejected. Existing tool-call/result and Unicode truncation tests still pass.
- Before production edits, the new allocation cases failed at 117,616 bytes for
  2,000 old turns and 667,064 bytes for 10,000 old turns. Both now satisfy the
  32,000-byte per-build ceiling.
- A suffix-access probe drops from 10,036 reads to at most 72 reads when twelve
  eligible messages, interleaved with excluded messages, follow discarded history.
- A repository-external comparison of both original implementations against the
  new implementations matches 1,000/1,000 deterministic mixed histories, including
  tool metadata, signed continuations, exceptions and several context sizes.
- The solution builds with zero warnings/errors. All eight test assemblies have
  passing results, totaling 5,511/5,511 tests. The initial parallel run timed out
  in one Windows shell-process ownership case; its three-case suite and the full
  137-case Windows assembly pass on isolated reruns without code changes there.
- Scoped whitespace verification and diff checks pass. All five touched C# files
  contain one handwritten type and remain below 250 physical lines.

#### Local allocation probe

Release-mode measurements on the same synthetic inputs:

| Case | Before bytes/call | After bytes/call |
| --- | ---: | ---: |
| Model context, 10,000 old turns, narrow budget | 667,100 | 4,512 |
| Model context, twelve old turns, all fit | 2,264 | 1,800 |
| Shared subagent context, 10,000 old turns | 27,296 | 26,824 |

The narrow-budget case reduces temporary allocations by about 99.3%; the system
message pass still visits all input messages. Shared-context output allocation
remains bounded while locating the eligible suffix no longer visits discarded
history. These are local fixture measurements, not universal latency guarantees.

The comparison project and copied baseline implementations remain outside the
repository under `%TEMP%/MossAgent-context-optimization-20261012-191707`.
No new paid-provider, account or native visual acceptance is claimed by this
pure in-memory refactor.
<!-- /source: CONTEXT_BUILDING_OPTIMIZATION_ACCEPTANCE.md -->


---

<a id="page-focus-optimization-acceptance"></a>

## PAGE_FOCUS_OPTIMIZATION_ACCEPTANCE.md

<!-- source: PAGE_FOCUS_OPTIMIZATION_ACCEPTANCE.md -->
### Page focus selection optimization acceptance

#### Scope and ownership

This change removes eager whole-page focus-candidate materialization from
`AppPageSwitcher.SelectFocusTarget`. One lazy visual-order traversal retains
the first usable fallback and returns immediately at the first usable input,
combo box or numeric field. An available remembered target still takes priority.
Focusable pages, hidden/disabled ancestors, stale focus callbacks and page-owned
restoration retain their existing behavior. No visual markup changes.

The required design self-check is:

1. App owns page focus; dependency direction remains unchanged.
2. The existing shared page-switcher contract expresses the behavior; no new port.
3. This is local presentation selection, not tool execution. Authorization,
   approval, timeout, cancellation and output policies remain unchanged.
4. The changed control and two new test files each declare one type and remain
   below the repository file/method hard limits.
5. Focus priority, hidden/disabled rejection and allocation tests protect the
   selector; real MainWindow resize tests protect page/client bounds and drafts.

No AOT, JSON, packaging, commit, push or release change occurs. Existing dirty
work is preserved. Debug/JIT output is isolated in the ignored repository path
`.scratch/page-focus-20261013`, not substituted into the default build output.

#### Deterministic evidence

TRX files, scoped validation and final build evidence are under
`%TEMP%\MossAgent-native-surfaces-20261013`.

- Five new selector cases cover the three priority-field types, remembered
  targets, hidden/disabled ancestors, visual order and focusable-page fallback.
  The correct before baseline passes four and fails the allocation case at
  58,096 bytes per selection. After optimization all five pass with the fixture
  budget at or below 1,024 bytes per selection; this is not a zero-allocation or
  whole-page performance claim.
- Eighteen new MainWindow cases cover light/dark, all seven settings tabs and
  the workspace with inspector open/closed. Each window transitions through
  1920x1032, 1280x820, 980x640 and back to 1920x1032. Active page, root, caption
  actions, composer/rail/inspector stay within client bounds. Drafts remain and
  no synthetic model request is sent. These are Headless checks, not native
  Windows resize or high-DPI acceptance.
- Initial test-fixture errors are retained in the original TRX files: an
  unattached UserControl content does not become a visual child, and resize
  assertions need the queued UI dispatcher work to run first. Corrected Grid
  children and dispatcher scheduling pass; neither is a product repair.
- The related group passes 66/66. The first default-parallel complete App run
  passes 4,157/4,158; the existing SidebarFilterAllocationTests project case
  records 104 bytes where zero is expected. Its implementation/assertion is
  unchanged, and the failing TRX is retained. The final serial App run passes
  all 4,158 without failures or skips; no cause for the first failure is asserted.
- The final eight-assembly regression passes 5,720/5,720: App 4,158, visual 24,
  Agent 119, Browser 76, Infrastructure 199, Platform.Windows 137, Providers 516
  and Tools 491. All final runs have zero failures/skips.
- The final solution build has zero warnings/errors. Scoped whitespace
  formatting, type/file-size checks and diff whitespace checks pass.

#### Native evidence ownership

The fresh Debug/JIT App assembly SHA256 is
`D384764AC1C1A3AC60011EC51099249DE3B6B2FDBB6412F14541E523BD0E539B`.
Launch metadata, passive PNG/JSON captures, independent fixture data and audits
are under `%TEMP%\MossAgent-narrow-fidelity-20261013-1015\native-focus`.

- Cold startup is maximized at 1920x1032, DPI 96 and ordinary privileges.
  Subsequent accepted frames are native 1280x820; no image scaling is used to
  normalize layout. An initial input is refused because Codex is foreground;
  fresh MossAgent activation/observation precedes retry. Codex is not automated.
- All seven settings pages are captured in light/dark at 1280x820. Provider
  expansion retains two models across page/theme changes. The same model editor
  is opened, cancelled with Escape and reopened without saving configuration.
  Search autofocus, a no-match query and clearing are observed. The long subagent
  page reaches its save region, preserves scroll across switching, then returns
  to its top. Empty subagent/MCP catalogs do not prove configured execution.
- The workspace and all five inspector tabs are captured in dark. The file
  `inspector-home-dark-normal.png` shows Sources, not a separate Home page.
  Diff is empty, Terminal is not started and Browser is unconnected. Audit shows
  restored execution/artifact history, not new tool execution. A final light
  Audit frame verifies restoration of Light theme.
- Before/after closing, read-only comparison confirms all nineteen SQLite table
  schemas and rows match the source backup, including 26 messages, six tasks,
  four terminal automation runs, browser configuration, catalogs and audits.
  Three original artifacts pass hash checks. Only the four historical model
  requests exist; no App descendant process is observed. No tool, terminal,
  browser-start or approval-setting action is invoked during this sweep.
- The QA App closes normally with Alt+F4 and exits zero. Its observer exits zero;
  both original processes are gone and port 51792 has no listener. Python
  screenshot and extended audit scripts remain outside the repository.

#### Limits and remaining gates

An earlier App instance had a 1280x820 native client while its content retained
the maximized layout; passive screen and PrintWindow captures both showed
clipping. The fresh instance reflows correctly at normal size, but no root cause
for the earlier instance is established. A restart or Headless dispatcher test
is not presented as a fix for that observed native issue.

Current Codex settings references, PI-Desktop provider references, native
980x640 on this binary, higher DPI, equivalent-content reference comparisons and
the complete interactive-control/state matrix remain open. These screenshots
are bounded native acceptance evidence, not one-to-one whole-window fidelity.
The complete product objective remains active.
<!-- /source: PAGE_FOCUS_OPTIMIZATION_ACCEPTANCE.md -->


---

<a id="search-refresh-optimization-acceptance"></a>

## SEARCH_REFRESH_OPTIMIZATION_ACCEPTANCE.md

<!-- source: SEARCH_REFRESH_OPTIMIZATION_ACCEPTANCE.md -->
### Search refresh optimization

#### Design and required self-check

- App owns synchronous in-memory search projection and subscriptions. Existing
  view models and collection contracts remain; no module dependencies change.
- The existing task-search result collection can publish changed positions rather
  than clearing and rebuilding equal snapshots. Source ordering, title matching,
  pinned metadata and duplicate snapshots retain their current meaning.
- Sidebar search can observe collection deltas instead of detaching and attaching
  every project after each insertion, removal, replacement or move. Reset still
  rebuilds subscriptions; duplicate group references must stay observed until
  their final occurrence is removed. Disposal releases every subscription.
- No I/O, model requests, tool calls or storage migration is introduced. Existing
  authorization, approval, cancellation, timeout, output and audit ports are not
  changed or bypassed. No new global state, service, package or cache is needed.
- The two production files remain below 250 lines with one handwritten type each;
  methods must remain below the 80-line hard limit.
- Focused tests cover equivalent snapshots, filtered-out changes, metadata
  replacement, ordering, duplicate entries, all collection actions, reset and
  disposal. Existing headless workspace tests cover real search/clear bindings,
  unchanged draft/selection and zero model requests in both themes.
- JIT debug and Windows x64 single-file publishing remain unchanged. Existing
  dirty work is retained; no commit, push or publish is requested.

#### Verification

- Twelve new task-search cases cover snapshot identity, hidden mutations,
  changed metadata, delta notifications, reordering, duplicates and six query
  variants with mutation sequences. Before the production change, six fail on
  redundant resets/replacement and six reference-behavior cases pass.
- Five new subscription cases pass before and after the refactor: duplicate
  references, replacement with an existing group, move, reset and owner isolation.
  Existing add/remove/replace, pinned/recent results and disposed-command cases
  remain passing. Removed groups and disposed owners cannot publish late results.
- The final focused search/sidebar/task-navigation run passes 300/300 cases.
  Actual headless workspace search and clear interactions pass in both themes,
  retaining selected project/task and draft text without model requests.
- The solution build passes with zero warnings and errors. All eight test
  assemblies run serially with their existing internal parallelism: 4813/4813
  pass, with zero failures or skipped cases. Counts are Agent 83, App 3478,
  Visual 8, Browser 76, Infrastructure 171, Windows 137, Providers 377 and Tools 483.
- Scoped `dotnet format whitespace --verify-no-changes` and repository
  `git diff --check` pass. Roslyn audits confirm one type per changed/new C# file,
  85/114 production lines, 175/116 test lines and a longest method of 33 lines.
- No native window or screenshot comparison is run for this optimization-only
  slice. No visual markup, shared appearance, provider configuration or publishing
  setting changes. Headless interaction coverage is not native pixel acceptance.

#### Allocation evidence

A repository-external .NET 10 Windows Release harness links either the saved
original task-search source or the new source, using the same Domain and Toolkit
assemblies. Each workload starts with 200 immutable synthetic tasks, warms up
256 calls, then measures 1000 calls with `GC.GetAllocatedBytesForCurrentThread`.
The filtered workloads retain 100 rows; inputs exist before measurement. Original
and new outputs have identical counts and ordered ID/title/status SHA-256 checksums.

| Workload | Before bytes/call | After bytes/call | Reduction | Result notifications before/after |
| --- | ---: | ---: | ---: | ---: |
| Equal task upsert | 14552 | 88 | 99.40% | 201000 / 0 |
| Filtered-out task upsert | 7437.44 | 86.528 | 98.84% | 101000 / 0 |
| Equivalent query with changed outer whitespace | 7452 | 92 | 98.77% | 101000 / 0 |

These are allocation measurements for the specified unchanged-result workloads,
not whole-app memory or latency measurements. Query/command-state notifications
remain; only unnecessary result-collection events disappear. Sidebar subscription
savings are not included in this table. An initial harness used a platform-neutral
target and emitted CA1416 warnings; the final harness targets Windows and builds
with zero warnings/errors. Reported measurements come from the final run.

Sources, benchmark projects, saved original source, logs, patches, audits and TRX
files remain outside Git in
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-search-optimization-20261011-143413`.
Final counters and measurements are `full-counters.json` and
`allocation-comparison.json`. Existing dirty work is retained and the index remains
empty. This slice does not claim full-product visual parity or complete the wider
MossAgent goal.
<!-- /source: SEARCH_REFRESH_OPTIMIZATION_ACCEPTANCE.md -->


---

<a id="sidebar-refresh-optimization-acceptance"></a>

## SIDEBAR_REFRESH_OPTIMIZATION_ACCEPTANCE.md

<!-- source: SIDEBAR_REFRESH_OPTIMIZATION_ACCEPTANCE.md -->
### Sidebar refresh optimization

#### Design self-check

- App owns sidebar filtering, ordering and observable row reconciliation. No
  dependency direction changes; the existing workspace/configuration ports still
  own persistence and I/O.
- Project, pinned and recent lists share the existing navigation collection helper
  instead of allocating separate filter pipelines and snapshots on every refresh.
  Keep culture-aware title matching, project-name matches, pin exclusion, row
  identity, duplicate occurrence order, selection and observable deltas.
- Recent tasks are sorted when loaded or inserted, not on each search keystroke.
  Initial duplicate selection still takes the newest record and the first record
  when timestamps tie. Updated tasks still follow other equal-timestamp tasks.
- There are no new file, command, HTTP, approval, timeout or cancellation operations.
  No tool execution, authorization, task lifetime, secrets, JSON or publication
  policy changes. The optimization introduces no global or cross-task cache.
- Use lazy reconciliation state only after an actual mismatch. Equal prefixes and
  tail removals do not need row maps; changed suffixes still reuse equal rows and
  retain independent row instances for duplicate task records.
- Each handwritten C# file retains one type. Changed production methods remain
  below 40 lines and files below 250 lines.
- Focused tests cover 14,641 short duplicate-sequence transitions, equal records,
  prefix/tail notifications, filters with/without matches, selected rows, newest
  duplicate pin state, stable timestamp ties and updated ordering. Existing
  sidebar/search regressions and a solution build are also required.

#### Baseline and allocation measurements

The pre-change focused run passes all 13 behavioral cases and fails the seven new
allocation guards. These failures establish the redundant work before changing
production code. All 324 focused sidebar/search/collection regressions then pass,
including the 14,641 duplicate-sequence transitions inside one test case.

| Warm scenario | Iterations | Bytes before | Bytes after |
| --- | ---: | ---: | ---: |
| Pinned, matching filter | 10,000 | 6,080,000 | 0 |
| Recent, matching filter | 10,000 | 17,840,000 | 0 |
| Project, matching filter | 10,000 | 7,760,000 | 0 |
| Pinned, no matches | 10,000 | 2,160,000 | 0 |
| Recent, no matches | 10,000 | 3,040,000 | 0 |
| Project, no matches | 10,000 | 3,840,000 | 0 |
| Append/remove one row after 32 retained rows | 1,000 cycles | 18,024,000 | 512,000 |

Measurements use Windows x64, .NET 10 Debug/JIT and thread allocation counters
after 100 warm-up iterations. The filters use normalized queries and existing
in-memory task snapshots. Tail measurements include new row creation and required
observable notifications. The append/remove reduction is 97.16%; these are
bounded microbenchmarks, not an overall UI speed or memory-use claim.

#### Verification

- Focused sidebar, recent-task, search and collection regressions: 324/324 passed.
- Complete App regressions: 3,638/3,638 passed. The seven allocation measurements
  match the focused run. This is a new successful run, not a reinterpretation of
  the previous collection-optimization run's temporary-directory cleanup failure.
- Architecture constraints: 7/7 passed. The other complete test assemblies were
  not rerun in this slice.
- Post-build replay of the new allocation/collection and recent-task cases:
  20/20 passed.
- Solution build after test-host exit: zero warnings and errors. Two earlier
  concurrent build attempts failed copying the App DLL locked by the running
  test host; their logs are retained. No process was killed or source changed to
  bypass the lock, and the final serial build succeeds.
- Scoped `dotnet format whitespace --verify-no-changes` and `git diff --check`
  pass. Roslyn auditing confirms one type per changed C# file, a maximum file
  size of 153 lines and a maximum method/accessor/local-function size of 32 lines.
- No appearance markup, shared-control geometry or native component boundary was
  changed. This slice does not include a fresh native-window screenshot, paid/live
  provider requests or completion of the separate subagent native lifecycle check.
- Windows x64, JIT debugging and single-file publishing remain unchanged. No AOT,
  JSON source generation, commit, push or release is introduced; unrelated
  working-tree changes are retained.

Evidence is outside the repository at
`%TEMP%\MossAgent-sidebar-refresh-optimization-20261011`: `baseline.trx`,
`focused.trx`, `app-full.trx`, `architecture.trx`, `final-replay.trx`, `solution-serial-build.log`,
whitespace/source-audit logs and the pre-change file snapshots. Earlier failed
build logs remain `solution-build.log` and `solution-final-build.log`.

#### Project-list subscriptions: 2026-10-12

##### Design self-check

- App owns this synchronous in-memory projection. Existing Application ports,
  persistence and dependency direction remain unchanged; no new service or cache.
- The project list no longer builds an `Except` membership set and scans every
  project subscription after each source change. Add/remove/replace process only
  affected rows. Constructor/reset rebuild subscriptions, and source moves leave
  subscriptions untouched while still refreshing sorted rows.
- Last-occurrence checks preserve duplicate references; stable sorting still
  respects source order when every sort key, including project identity, ties.
  Existing manual/activity order, row identity, selection and expansion remain.
- Captured collection/property notifications can still run after another listener
  disposes the list. Guards reject these callbacks without republishing rows or
  reattaching subscriptions. Independent list owners retain their own handlers.
- No file, command, network, provider or tool operations are introduced. Existing
  authorization, approval, timeout, cancellation, output and audit limits are not
  changed. No migration, secret handling, appearance or publication change.
- The three handwritten C# files each contain one type: 97/144/50 physical lines,
  with largest methods of 24/23/30 lines respectively.
- Focused tests cover replacement with existing references, duplicate lifetimes,
  equal-key source moves, reset/reinsertion, owner isolation and callbacks captured
  before disposal. Allocation guards compare source moves to equivalent sorting
  refreshes, bounding extra event work independently of sorting allocations.

##### Allocation evidence

Windows x64, .NET 10 Debug/JIT, current-thread allocation counters. Each workload
uses synthetic projects with unique creation times, warms up 100 two-operation
cycles, then measures 1,000 cycles (2,000 source moves). Sorting is still performed;
there are no visible row changes or collection notifications in this workload.

| Projects | Move bytes before | Move bytes after | Reduction |
| --- | ---: | ---: | ---: |
| 32 | 8,880,000 | 6,736,000 | 24.14% |
| 256 | 53,168,000 | 40,784,000 | 23.29% |

These are bounded allocation measurements for source moves, not overall UI speed
or application memory-use claims. Add/remove/replace have deterministic behavior
coverage but are not separately benchmarked here.

##### Verification

- Original final baseline: 5/9 pass; both allocation guards and both captured-
  notification disposal regressions fail. The earlier eight-case baseline is
  also retained rather than reinterpreted as successful acceptance.
- Post-change focused sidebar/recent/search/collection tests: 382/382 pass.
- Architecture constraints: 7/7 pass. Solution build: zero warnings and errors.
- Scoped whitespace and diff checks pass. Roslyn source auditing confirms the
  file/type/method limits above using PowerShell's loaded parser.
- First complete App run: 3,765/3,766 pass. The existing provider-catalog
  `WarmModelCommandAndAvailabilityChecks_DoNotAllocate(missing: false)` assertion
  expected zero bytes but measured 1,616. No provider implementation or test is
  changed. That suite and all nine new cases then pass together, 16/16, in a
  separate replay; the failed complete run remains preserved.
- Second complete App run without code changes: 3,765/3,766 pass. The provider
  guard now passes; the existing project/no-match `WarmUnchangedFilters` guard
  instead expects zero bytes but measures 1,304. Both affected allocation suites
  and all nine new cases pass together in the final replay, 22/22. Neither failed
  full run is classified as passing. The intermittent allocations' cause is not
  diagnosed in this slice, and no zero-allocation assertion is relaxed.
- Other full test assemblies are not rerun in this slice, apart from the seven
  architecture constraints above.
- No native window/screenshot comparison or paid/live provider request is run
  for this optimization-only slice. No styles, control geometry or markup change;
  headless behavior coverage does not complete native visual acceptance.
- Debug remains JIT and publishing remains Windows x64 single-file, without AOT
  or JSON source generation. Existing dirty work is preserved; no staging,
  commit, push or publish.

Evidence is outside the repository at
`%TEMP%\MossAgent-project-subscriptions-20261012-112239`: `baseline.trx`,
`baseline-final.trx`, `focused.trx`, `architecture.trx`, `solution-build.log`,
`whitespace.log`, `diff-check.log`, `source-audit.log`, `app-full.trx`,
`app-full-repeat.trx`, `final-replay.trx`, `allocation-replay.trx`,
`solution-final-build.log` and the pre-change source/document snapshots.

#### Intermittent-allocation diagnostic follow-up (2026-10-16)

The historical nonzero allocation assertions remain unexplained. This follow-up
does not change production filtering, the measured loop, warm-up count, test
parallelism or the strict zero-byte assertions.

- The unchanged six sidebar allocation cases pass in `sidebar-baseline.trx`.
- An external probe reuses the actual pinned/recent/project view models, 32
  synthetic tasks, the existing 100-call warm-up and 10,000-call measured loop.
  Matching/missing filters run both normally and with a separate thread repeatedly
  collecting garbage: 12 scenarios, 12 batches each, 1,440,000 total filter calls.
- All 144 measured batches report zero current-thread allocation and zero
  collection notifications while retaining row identity. The pressure scenarios
  observe 2,556 generation-zero collections. This experiment does not reproduce
  the failure or establish that GC, JIT or concurrent activity caused it.
- A new complete App process then passes 4,794/4,794 in 15 minutes 18 seconds,
  with no skips or aborts, including all six unchanged sidebar allocation cases.
  The App and App.Tests DLL hashes remain unchanged for that complete run. Its
  emitted application predates the subsequent selector-focus UI correction, so
  it is not complete-suite acceptance of that new appearance revision.
- Older 1,200/1,304/3,288-byte failures and the incomplete historical run remain
  preserved. This passing process neither replaces those reports nor proves a
  causal repair. No additional performance fix is claimed.

Evidence: `%TEMP%\MossAgent-sidebar-allocation-20261016`, including
`sidebar-baseline.trx`, `gc-pressure.json`, `gc-pressure.log`, external `probe/`,
`app-full-current.trx` and `app-full-current.log`. The selector review's
`completed-baseline-app-test-output-identities.json` records the unchanged DLLs.
<!-- /source: SIDEBAR_REFRESH_OPTIMIZATION_ACCEPTANCE.md -->


---

<a id="sqlite-test-pool-isolation-acceptance"></a>

## SQLITE_TEST_POOL_ISOLATION_ACCEPTANCE.md

<!-- source: SQLITE_TEST_POOL_ISOLATION_ACCEPTANCE.md -->
### SQLite 测试连接池隔离验收

日期：2026-10-09。本记录是设置页预览回归中发现的测试隔离问题的后续，
不是生产 SQLite 实现变更，也不代表 Codex 界面或完整产品目标已完成。

#### 问题与范围

前次默认并发全量测试中，既有
`SqliteProviderFailureMigrationTests.VersionSevenUpgrade_PreservesHistoryAndAllowsNewFailureCategories`
的 category 11 在归档任务时抛出 `ObjectDisposedException: SQLitePCL.sqlite3`。
失败栈经过 `SafeHandle.DangerousAddRef`、`SqliteDataReader.NextResult` 和
`SqliteWorkspaceRepository.SetTaskArchivedAsync`。

仓库的生产连接工厂默认启用 pooling；测试各自使用独立临时数据库，
但多处 teardown 调用 `SqliteConnection.ClearAllPools()`，会清理同一进程中
其他测试拥有的池。单独重跑通过或串行全量通过，不能证明这个并发边界安全。

本次只修改 `MossAgent.Infrastructure.Tests` 的清理范围：11 个既有测试/夹具
文件中的 17 处全局清理替换为 `SqliteTestPool.Clear(root)`，并新增两个辅助/测试文件。
生产连接工厂、repository、迁移、schema、调度和测试并发设置均未改变。

#### 设计自检

1. 行为归 Infrastructure.Tests 的测试支持，不增加生产依赖或 App 的文件/数据库访问。
   辅助类复用现有 `SqliteConnectionFactory` 的连接字符串，定向退休当前数据库的池。
2. 使用现有 `SqliteConnection.ClearPool(connection)`，不关闭 pooling，
   不增加异常重试、全局锁或测试串行化来掩盖跨库干扰。
3. root 为 null、空或空白时立即拒绝，不能回退到用户默认数据目录。
   没有工具授权、审批、执行超时、取消或输出限制变化；探针只使用合成临时数据库。
4. Roslyn 审计的 13 个 C# 文件均只有一个类型；最大文件 221 行，
   最大方法或 lambda 43 行。开发和测试保持普通 JIT，不改变发布或 JSON 方案。
5. 测试覆盖 owned/foreign、idle/active handle、缺失 root、真实夹具清理及源码门禁。
   非当前数据库连接保持可用；当前数据库的活动连接在 Close 前仍可用、Close 后退休。

#### 确定性红绿灯

新增 `SqliteTestPoolTests` 共八个 cases：

- 清理 owned idle handle，不退休另一个数据库的 idle handle，后者可复用。
- 重复清理 owned pool，不影响另一个数据库的活动连接及 SQL 查询。
- 分别拒绝 null、空、空白 root，不触碰已存在的其他 pool。
- owned active handle 在 Close 前仍可查询，Close 后退休，重开取得新 handle。
- 源码门禁拒绝 Infrastructure.Tests 中再次调用全局 `ClearAllPools()`。
- 真实 failure fixture 的 disposal 不退休另一个 fixture 的 idle handle；
  另一个 fixture 可继续归档任务并读取保留的历史。

初版辅助类暂用旧全局清理跑红灯：六项中两项失败、四项通过。
跨库 idle handle 断言和源码门禁均失败。改为定向清理后，新增八项及
既有四项迁移测试一起通过；没有降低 handle 身份或历史断言。

#### 独立并发探针

外部探针使用本地 Microsoft.Data.Sqlite 10.0.12 和生产 Infrastructure，
八个独立 worker 数据库各做 250 轮归档/取消归档与计数查询，第九个数据库负责清理。
global/scoped 在独立进程执行，清理节奏相同；没有用户数据库、GUI、提供商或付费请求。

| 清理方式 | 成功操作 | 失败 | deadline | 时长 |
| --- | --- | --- | --- | --- |
| global | 3,998 | 1，`ObjectDisposedException: SQLitePCL.sqlite3` | 否 | 23.97 秒 |
| scoped | 4,000 | 0 | 否 | 5.36 秒 |

这是同类句柄异常的复现及范围修正证据，不是所有 SQLite 并发行为的证明。
首次更激进的探针到达 90 秒 deadline，保留诊断日志，但不计作成功或异常复现。

#### 验证证据

证据目录：`%TEMP%\MossAgent-sqlite-pool-20261009`。
Python/探针/审计脚本、合成数据和日志全部在仓库外，不是发布文件。

- `focused/focused.trx`：新测试与既有迁移专项 12/12 通过。
- `parallel/parallel-1.trx` 至 `parallel-3.trx`：Infrastructure.Tests 默认并发完整跑
  三轮，每轮 144/144 通过，失败和未执行均为 0。
- `global-probe-v2.log`、`scoped-probe-v1.log`：上述两个有效探针结果。
- `source-audit.log`：单类型和文件/方法规模审计通过。
- `format.log`：13 个相关 C# 的 scoped whitespace 验证通过。
- `solution-build.log`：完整解决方案构建 0 警告、0 错误。
- `full/` 的七份独立 TRX：默认项目/测试并发完整解决方案 3,370/3,370 通过，
  失败和未执行均为 0；App 2,559、Infrastructure 144、Providers 283、Tools 188、
  Browser 76、Agent 63、Platform.Windows 57。没有使用 `-m:1`。

```powershell
dotnet test tests/MossAgent.Infrastructure.Tests/MossAgent.Infrastructure.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~SqliteTestPoolTests|FullyQualifiedName~SqliteProviderFailureMigrationTests"
dotnet test tests/MossAgent.Infrastructure.Tests/MossAgent.Infrastructure.Tests.csproj --no-build --no-restore
dotnet build MossAgent.slnx --no-restore -v:minimal
dotnet test MossAgent.slnx --no-build --no-restore --logger trx -v:minimal
```

#### 剩余边界

- 其他测试项目仍有全局清理调用，本次没有扩大修改到那些夹具。
  门禁也只约束 Infrastructure.Tests，不声称所有测试进程的连接池清理已经隔离。
- 没有修改或原生复测生产 SQLite 使用策略。探针和确定性测试不等于
  Microsoft.Data.Sqlite 所有并发问题已修复。
- 没有 UI 修改，不需要为测试支持改动重新截图；设置页的原生对照与仍有的
  像素差异保留在 [SETTINGS_PREVIEW_FIDELITY_ACCEPTANCE.md](UI_WORKFLOWS.md#settings-preview-fidelity-acceptance)。
- 未暂存、提交、推送或发布；完整产品目标保持进行中。

#### 2026-10-10 App 测试隔离设计复核

1. 本轮解决 App 完整并行回归中的测试清理边界，不修改生产 SQLite。当前 App 的
   43 个测试/夹具文件共 78 处全局清理会影响同进程的其他数据库；最近一次全量
   在 `WorkspaceAgentCatalogFlowTests` 初始化独立数据库时出现句柄已释放异常。
2. 将既有定向清理实现移至共享测试源文件，由 App.Tests 和 Infrastructure.Tests
   链接编译；两者没有运行时项目依赖。所有调用必须传入实际数据目录，区分夹具
   外层项目目录与内层 `data`，复用生产连接工厂生成的连接字符串。
3. 保持 pooling、测试并行、既有关闭顺序、取消和期限，不增加异常重试或全局锁。
   缺失 root 立即拒绝；只触碰合成测试数据库，不访问真实用户数据或工具审批。
4. 辅助类与新增测试各自单类型，按原有文件进行局部替换，不合并生产职责或更改
   普通 JIT 调试、Windows x64 单文件发布和 JSON 方案。
5. 先通过真实命令、历史夹具的释放验证其他数据库的 idle/active handle 和 SQL
   查询不受影响，并新增 App/共享源码门禁；再运行既有基础设施边界测试、受影响
   App 回归、完整默认并行测试、格式/规模检查和完整构建。

##### 实现与确定性验证

- `tests/Shared/SqliteTestPool.cs` 保留既有 `ClearPool(connection)` 和缺失 root 拒绝
  行为，移除 Infrastructure.Tests 内的重复源文件；两个测试项目链接同一文件并
  使用 `MossAgent.Testing` 命名空间，不引入新的测试或生产项目依赖。
- App 的 78 处调用替换为定向清理，并移除 42 个不再使用的 SQLite using。
  六个工作区夹具传入自己的内层 `data`；原子持久化夹具传入 `_root`，模型校验
  测试传入 `fixture.Root`，其余传入原有直接数据目录。原有关闭/等待/删除顺序不变。
- `SqliteFixtureIsolationTests` 的四项实际夹具用例，在关闭命令/历史夹具后验证
  另一个数据库的 idle handle 或 active connection 关闭后的 handle 没有退休，
  重开仍复用同一句柄并可查询。第五项门禁拒绝 App 与共享测试源中的全局清理。
  新增五项修复前全部因断言失败，修复后全部通过，不是编译失败或增大期限的结果。
- App 定向回归 9/9，包括此前失败的四项代理目录流；既有基础设施连接池边界
  8/8 通过，覆盖 owned/foreign、idle/active、缺失 root 与历史保留。
  跨库测试不执行命令、不请求付费模型，也不读取真实用户数据库。
- 与本轮开始前的快照逐文件比较，43 个既有 App 文件仅包含清理调用与无用 using
  的变更；范围内 45 个 C# 单类型、文件和方法规模审计零违规，空白格式检查通过。
  首次局部替换遗漏模型校验测试的 tuple 字段名，编译报错后修正为 `fixture.Root`；
  不把这个改动期间的编译错误当作原故障复现证据。

##### 最终默认并行回归

- 完整解决方案构建零警告、零错误；默认项目和测试并行下，七个项目共
  3,920/3,920 通过，失败和未执行均为零。App 2,972、Infrastructure 144、
  Providers 317、Tools 219、Browser 76、Agent 63、Platform.Windows 129。
- 此前全量失败的代理目录实际请求用例包含在这次 App 全量中。新增隔离测试
  在完整默认并行运行时也通过；未使用 `-m:1`、禁止并行、关闭 pooling、测试重试
  或扩大已有期限。这个结果证明当前回归通过，不声称所有 SQLite 并发故障均已修复。
- `full/` 保存七份独立 TRX，`full.log` 保存这一次完整执行，
  `solution-build.log`、`format.log`、`source-audit.log`、`diff-check.log` 分别保存
  构建、45 个 C# 的格式/规模审计和与开始前快照的局部变更核查。

```powershell
dotnet test tests/MossAgent.App.Tests/MossAgent.App.Tests.csproj --no-restore --filter "FullyQualifiedName~SqliteFixtureIsolationTests|FullyQualifiedName~WorkspaceAgentCatalogFlowTests"
dotnet test tests/MossAgent.Infrastructure.Tests/MossAgent.Infrastructure.Tests.csproj --no-restore --filter "FullyQualifiedName~SqliteTestPoolTests"
dotnet build MossAgent.slnx --no-restore -v:minimal
dotnet test MossAgent.slnx --no-build --no-restore --logger trx -v:minimal
```

##### 本轮范围限制

- 本轮只改变测试支持与测试项目链接，不修改生产 SQLite、迁移、服务生命周期、
  UI、提供商、审批或授权行为；没有为测试支持改动新增原生截图验收。
- 其他测试项目的池清理未在本轮扩大修改，不能宣称全仓所有 SQLite 清理或压力
  并发问题均已修复。一次并行全量通过也不能替代确定性的隔离断言。
- 仓库外证据目录：
  `C:/Users/wzheng/AppData/Local/Temp/MossAgent-app-sqlite-isolation-20261010/`。
  保存本轮前快照、`red/before.trx`、`focused/` 两份 TRX、构建、格式、规模和差异
  检查日志；脚本、日志与合成数据库不加入仓库。未暂存、提交、推送或发布。
<!-- /source: SQLITE_TEST_POOL_ISOLATION_ACCEPTANCE.md -->


---

<a id="ui-hotpath-optimization-acceptance"></a>

## UI_HOTPATH_OPTIMIZATION_ACCEPTANCE.md

<!-- source: UI_HOTPATH_OPTIMIZATION_ACCEPTANCE.md -->
### UI hot-path optimization acceptance

#### Scope: 2026-10-11, chat presentation

- Live tool results and persisted tool messages use `ToolConversationPayload.FromEvent`
  instead of maintaining two copies of the raw-field mapping. Success, failure,
  detail text, dialogue and error codes retain their existing representation.
- User bubbles and tool activity rows no longer build invisible regular-response
  Markdown blocks. Their raw content, summary/details, attachment metadata and
  observable text changes remain available. Assistant/system messages and subagent
  answers keep their existing Markdown/code/table rendering and streaming updates.
- Table detection checks for a candidate pipe before allocating cell lists, and
  validates separator cells before splitting headers. The unreachable empty-header
  array check is removed; the splitter always emits at least one cell, including
  an empty header. A pipe on either the header or separator still supports one
  column, and code fences, escaped pipes, inline-code pipes and short rows retain
  their existing semantics.
- These are synchronous App-owned presentation operations. No Application port,
  tool execution, authorization/approval, cancellation, timeout, output budget,
  audit, database migration, visual control/style or package change is introduced.
  Debug remains JIT and publishing remains Windows x64 single-file, without AOT
  or source-generated JSON. No commit, push or publish is performed.

##### Focused verification

- Before the production refactor, 50 characterization/rendering/serialization
  cases pass, including 22 new table and live-versus-persisted mapping cases.
- After the refactor, 233 targeted parsing, response rendering, tool progress,
  conversation/history/cancellation and subagent flow cases pass. Six additional
  cases verify that user/tool rows have no hidden blocks while regular streaming
  and subagent Markdown remain intact. The response suite covers both themes
  and narrow/wide layouts; this is headless coverage, not new native screenshots.
- The solution build passes with zero warnings/errors. Scoped whitespace checks
  pass. All eight changed handwritten C# files contain exactly one type and stay
  within the file/method hard limits; the longest file is 209 lines. The existing
  event dispatcher shrinks from 75 to 72 lines; all other methods are at most 38.
- The complete serial solution run passes 4498/4498 cases across eight assemblies,
  with zero failed/skipped cases. After extracting a test-only assertion helper,
  the solution is rebuilt and the 233 targeted cases are rerun against that build.
  Final scoped formatting and repository `git diff --check` pass. The index stays
  empty; status changes from the saved baseline consist only of the three new
  focused test files, with all pre-existing worktree changes preserved.

##### Allocation measurement

A repository-external .NET 10 Release harness links the original or refactored
parser with the same production block types. For each 100-line input it warms up
256 parses, then measures 1000 calls with `GC.GetAllocatedBytesForCurrentThread`.
The input is allocated before measurement; output block counts remain identical.
An additional deterministic 5000-input mixed Markdown corpus produces identical
serialized-block SHA256 checksums before and after the change.

| 100-line parser input | Before bytes/call | After bytes/call | Reduction |
| --- | ---: | ---: | ---: |
| Ordinary text without pipes | 115632 | 33264 | 71.23% |
| Non-table text containing pipes | 113240 | 72056 | 36.37% |
| Valid table with 100 data rows | 61168 | 61168 | 0% |

This measures parser allocation only, not full-application memory, latency or
the separate savings from omitting hidden message blocks. No native app window,
provider credential, paid API request or user database is used by the harness.
Snapshots, benchmark source/results, audit output, logs and TRX stay outside Git:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-chat-optimization-20261011-082302`.

This optimization slice does not complete the broader product/fidelity goal.

#### Scope: 2026-10-10

- `SubagentDialogueProgress.Apply` writes directly into one final array instead
  of copying history into a temporary list, growing that list on append, then
  copying it again into the returned array.
- `WorkspaceSidebarProjectList.Refresh` builds manual-rank dictionaries only
  for manual sorting, not for activity sorting where they are unused.
- Project subscription insertion uses `HashSet.Add` to detect new rows rather
  than checking membership and then inserting with a second lookup. The existing
  removal and disposal paths remain unchanged.
- No shared abstraction, cache, package, native control, visual style, database
  migration or public contract is added. Debug remains JIT; publishing remains
  Windows x64 single-file without AOT. No commit, push or publish occurs.

#### Design and safety boundaries

- App owns these synchronous, in-memory presentation operations. Existing
  Application ports, repository calls and module dependencies remain unchanged.
- Every progress event still receives its own array; previous history, completed
  replies and tool messages are never modified. Only a trailing partial message
  with the same case-sensitive role is replaced. Different roles append.
- Activity/creation/name/identity tie-breakers, saved manual ordering, unranked
  project fallback and source collection order remain unchanged for valid
  preferences. Inactive manual ranks no longer need to be enumerated.
- Project row instances retain selection, expansion and search state. Reordering
  continues to use moves instead of resets. Removed rows and disposed lists no
  longer influence the visible list, as before.
- Task ownership, cancellation, authorization, approval, timeouts, output budgets,
  audit and persistence remain upstream/downstream; no execution path changes.
- All four changed handwritten C# files contain exactly one type and stay below
  500 lines. The longest changed-file method is 26 lines. No appearance changes
  require a new screenshot comparison; this is not native visual acceptance.

#### Deterministic regression verification

- Seventeen new characterization cases pass against the original production
  implementations before refactoring. Together with three existing dialogue
  cases, that first run passes 20/20.
- Dialogue cases cover null/empty history, the full partial/same-role matrix,
  immutable previous snapshots, independent branches, array/list/read-only inputs
  and retention of message references.
- Sidebar cases cover inactive saved ranks, activity moves, manual fallback,
  tied name/identity ordering, unchanged-list notifications and subscription
  cleanup after removal/disposal. Existing workspace/drag/preference tests retain
  cancellation, rejected reentry and failed-save coverage.
- After refactoring, 114 targeted dialogue, presenter, workspace, sidebar,
  preference, drag and search cases pass.
- Focused whitespace verification and the four-file Roslyn size/type audit pass.
- The solution build succeeds with zero warnings and errors. The subsequent full
  run passes 4285/4285 cases across eight assemblies, with zero failed or skipped
  cases. No paid provider calls are required by these checks.
- Both repository-default and focused `git diff --check` pass. The index remains
  empty; all pre-existing worktree changes remain uncommitted.

#### Allocation measurement

A repository-external .NET 10 Release harness links the original or refactored
dialogue helper and the same production message record. Each scenario warms up
2000 calls, then measures 10000 calls with `GC.GetAllocatedBytesForCurrentThread`.
Histories and incoming messages are allocated outside the measurement. Both
versions produce the same count checksum and preserve the incoming last message.

| Operation | Previous messages | Before bytes/call | After bytes/call |
| --- | ---: | ---: | ---: |
| Append | 0 | 120 | 32 |
| Append | 16 | 624 | 160 |
| Replace trailing partial | 16 | 336 | 152 |
| Append | 128 | 4208 | 1056 |
| Replace trailing partial | 128 | 2128 | 1048 |

This sample reduces dialogue-helper allocation by approximately 51-75%. It does
not measure full application memory or establish a general latency improvement:
the longest-history refactored sample takes longer in this short run. No live
provider request, credential, user database or native app window is used.

Original source snapshots, harnesses, CSV, logs and TRX files stay outside Git:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-hotpath-optimization-20261010`.

This finishes only this optimization slice, not the full product/fidelity goal.

#### 2026-10-12: Inspector layout notification cleanup

- The two layout-dimension setters now share finite/nonnegative validation,
  the existing 0.5-DIP tolerance, accepted-value storage and margin comparison.
  Accepted measurements are still retained while the card is hidden or the
  viewport is wide, so showing the card uses its latest dimensions.
- Presentation notifications compare the previous visible card, panel state
  and computed inset. Toggling the floating card no longer republishes the
  unchanged panel label. Restoring another task with identical presentation
  no longer republishes the tab, title, panel, visibility or inset.
- A task's complete restored state is assigned before any notification. The
  existing per-project/per-task preferences, archive cleanup, property-changing
  behavior of the panel setter, dispatcher ownership and disposal guards remain.
  No cache, global state, new port, style or database migration is introduced.

##### Required self-check

1. State presentation remains in App's existing inspector view model; dependency
   direction and the shared visual-control boundary are unchanged.
2. Existing catalog events, command bindings and observable properties are reused.
3. The change performs no tool execution, file access, process launch or provider
   request; authorization, approval, timeout and output limits are unaffected.
4. The production file has 148 physical lines, one type and a longest method of
   21 lines. The new test file has 217 lines, one type and a longest body of 22 lines.
5. Deterministic tests cover real changes, unchanged snapshots, hidden/wide
   measurements, both overlap-boundary directions, invalid/subpixel inputs,
   complete state restoration and disposal. Existing inspector ownership,
   rejected navigation, archival and background-run tests also pass.

##### Verification and scope

- The 21 new cases run against the original implementation first: 8 pass and
  13 fail on redundant notifications. After refactoring, all 21 pass; the focused
  inspector/ownership/safety/main-window group passes 61/61.
- Each of four same-region resize cases performs 1,000 two-operation cycles:
  2,000 accepted width changes publish zero inset notifications, without changing
  the inset. This is an event-count regression, not an application-wide speed,
  frame-rate, allocation or memory benchmark.
- Architecture constraints pass 7/7; the solution build has zero warnings/errors.
  Scoped whitespace verification and Roslyn file/type/body-size auditing pass.
- This optimization slice does not change appearance or run a fresh native
  screenshot comparison. Preceding toolbar changes are retained and covered by
  the application regression; their native evidence and remaining visual gaps
  are recorded in [CONVERSATION_HEADER_COMPONENT_ACCEPTANCE.md](UI_COMPONENTS.md#conversation-header-component-acceptance).
- The prior sidebar full-run failures remain recorded in
  [SIDEBAR_REFRESH_OPTIMIZATION_ACCEPTANCE.md](ENGINEERING.md#sidebar-refresh-optimization-acceptance); this slice neither changes nor
  relaxes the intermittent zero-allocation guards.
- Test data is synthetic and isolated; no paid/live provider, user credentials
  or production database is used. Debug remains JIT, release remains Windows x64
  single-file, and AOT/JSON source generation are not reintroduced. No staging,
  commit, push or publish is performed; the broader product goal remains active.

Evidence is outside Git at
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-inspector-notifications-20261012-122119`:
original source/document snapshots, `baseline.trx`, `focused.trx`,
`architecture.trx`, `solution-build.log`, `whitespace.log` and `source-audit.log`.

##### Initial complete App run

- The first full App run passes 3,797/3,798, with one failed menu integration test;
  this run is not classified as passing. Its existing locator assumes the flyout
  card directly contains `AppContentControl`, whereas the preceding toolbar change
  adds a layout `StackPanel` around the business slot and expansion action.
- `ConversationHeadingLayoutTests` now locates the unique shared business-slot
  control and asserts that its content is the header's actual menu. Label, target,
  locked-command rejection, keyboard close, tab removal and retention of the
  unarchived task are still checked. No production behavior or assertion is relaxed.
- `app-full.trx`, `ConversationHeadingLayoutTests.before.cs` and
  `heading-source-audit.log` preserve the failed run, original test and final audit.

##### Final verification

- Expanded header, inspector, icon, shell and visual-component coverage passes
  370/370, including the updated menu integration test and all 21 new cases.
- The second complete App run passes 3,797/3,798; the menu test now passes. Its
  sole failure is the unchanged `WorkspaceConversationFlowTests` failure-response
  case: `Directory.Delete` at line 44 cannot remove its temporary root because
  another process holds it. The failing process is not identified or repaired;
  this complete run also remains classified as failed.
- An independent replay of both conversation-flow cases, all 21 new inspector
  cases and the menu-close case passes 24/24. No assertion is weakened, cleanup
  delay added or unrelated conversation-flow source changed. Neither replay nor
  the 370-case group is presented as a passing complete App run.
- Final solution build has zero warnings/errors. Final solution-scoped whitespace
  verification passes; changed-file diff checks and source-size audits pass. The
  updated heading test remains one type, 246 lines, with a longest body of 47 lines.
- Other complete test assemblies are not rerun in this App-only slice, except
  the seven architecture constraints. No new native screenshot or release publish
  is performed, and the index remains empty.

Additional evidence: `header-inspector-broad.trx`, `app-full-final.trx`,
`final-replay.trx`, `solution-final-build.log`, `whitespace-final.log` and
`diff-check.log`, in the same repository-external evidence directory.

#### 2026-10-12 Inspector tab notification cleanup

The existing App-owned inspector state remains the single source of truth.
Direct tab selection and per-task restoration now share changed-value checks
instead of invalidating every title and visibility flag on every transition.
`home` and `sources` retain their common title/source pane without redundant
notifications. No cache, new service, persistence shape, appearance variant,
execution port or authorization policy is introduced.

- `InspectorTabNotificationTests` covers all 36 ordered tab pairs through direct
  selection and task restoration, plus six invalid values: 78 deterministic cases.
  It checks the exact notification multiset and the complete published tab state
  during every callback, including no-op transitions and the home/source alias.
  The existing layout suite retains disposal, dimensions and context-card checks.
- The original implementation fails 60/78 new cases; its 18 no-op/invalid cases
  pass. After the focused production change, all 78 new cases and the existing
  header, toolbar, inspector and menu workflow coverage pass 145/145.
- A changed tab previously publishes eight properties. It now publishes two to
  five, depending on the transition; equal tabs still publish none. These are
  deterministic event counts, not full-app timing, allocation or memory results.
- The production file remains one type, 149 lines, with a longest body of 21
  lines; the new test is one type, 121 lines, with a longest body of 20 lines.
  Changed C#/AXAML syntax and size audits pass, as does scoped whitespace
  verification. The solution builds with zero warnings/errors.
- Tests use isolated synthetic data and make no provider requests. This
  notification-only change does not add a fresh native tab-switch screenshot;
  the preceding final-offset toolbar capture and context-toggle observations
  are recorded separately in [CONVERSATION_HEADER_COMPONENT_ACCEPTANCE.md](UI_COMPONENTS.md#conversation-header-component-acceptance).
  Debug stays JIT and release stays Windows x64 single-file, without AOT or
  JSON source generation. No files are staged, committed, pushed or published.

Evidence remains outside Git at
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-inspector-tabs-20261012-131604`:
original source/document snapshots, `tabs-red.trx`, `tabs-focused.trx`,
`solution-build.log`, `whitespace.log` and `source-audit.log`.

##### Final App and architecture verification

- The final complete App run passes 3,877/3,878 with no skipped cases. All 78
  new tab-notification cases and both preceding activity-color cases pass.
  The sole failure is again the unchanged failure-response case in
  `WorkspaceConversationFlowTests`: temporary-root deletion at line 44 raises
  `IOException` because another process holds the directory. This complete run
  is classified as failed, not as a full pass.
- An independent replay of both conversation-flow cases passes 2/2. The process
  holding the directory has not been identified or repaired; no unrelated
  cleanup source, retry policy or assertion is changed to hide the failure.
- Architecture constraints pass 7/7, including repository-wide handwritten
  C# type-count/file-size checks. Changed-file Roslyn body-size and syntax checks
  and tracked/untracked-path whitespace checks pass. No allocation guard is
  relaxed. Other complete test assemblies are not rerun in this App-only slice.
- Final toolbar screenshots use the restored minus-one-pixel implementation;
  the ordinary-permission synthetic native host exits normally with `QA_EXIT: 0`.
  This does not establish native dark/narrow/DPI coverage or full-product parity.
  The complete product goal remains active and the Git index remains empty.

Additional evidence: `app-full.trx`, `architecture.trx`,
`conversation-replay.trx` and `diff-check.log`, in the same external directory.

The later Git-status lifecycle work, its independently diagnosed UI wait-chain
repair and a complete 5,425/5,425 final test run are recorded separately in
[REPOSITORY_STATUS_LIFETIME_ACCEPTANCE.md](GIT_WORKTREES.md#repository-status-lifetime-acceptance). That later success does not replace
the failed runs or prove the earlier directory holder's identity.

#### 2026-10-13: Bounded preview projection cleanup

##### Design and safety self-check

- App owns in-memory code-preview/search projection. Both use the existing
  parser's bounded line reader; no new service, port, package or dependency is
  introduced. Authorized snapshot readers and tool execution remain upstream.
- The reader scans only the first 131,072 UTF-16 code units and materializes at most
  2,000 lines. Limits apply before CRLF normalization, as before. Lone carriage
  returns, empty/trailing lines, literal case-insensitive matching, diff gutters
  and the existing localized truncation row retain their previous semantics.
- Search uses one indexed pass instead of tuple/filter/projection iterators.
  Its match list is lazy, so unsuccessful queries do not allocate a temporary
  list. Content reset clears the query and refreshes results once, removing the
  duplicate refresh when a previous query was nonempty. Closing an open search
  still publishes its own visibility change.
- There is no I/O, authorization, approval, timeout, cancellation, output-budget,
  persistence or secret-handling change. Snapshots remain instance-owned; no
  cross-tab cache or mutable global state is added. JIT debugging and Windows x64
  single-file publishing without AOT remain unchanged.
- The production files have 81 and 92 lines. Each changed/new handwritten C# file
  contains one type, and changed methods remain below the 80-line hard limit.
  Tests cover both successful projection and hidden-line/character rejection,
  content replacement, notification counts and independent tab state.

##### Focused verification and allocation evidence

- The pre-change run passes 36/41 cases and fails five: one duplicated result
  refresh and four allocation guards. The post-change run passes 41/41, including
  all 34 new cases. Plain/diff parsing, newline boundaries, literal search,
  wrapping and unavailable/disposed preview behavior retain coverage.
- Measurements use `GC.GetAllocatedBytesForCurrentThread` after warm-up. The
  fixture has 2,000 visible `shown` lines followed by 100,000 hidden newlines and
  a hidden suffix, all within the character limit. Fixture allocation is outside
  the measured region. These are projection microbenchmarks, not whole-App
  memory, startup or latency measurements.

| Operation | Before bytes | After bytes |
| --- | ---: | ---: |
| Plain-text parsing | 1,143,680 | 360,608 |
| Diff parsing | 1,089,248 | 306,176 |
| Search content reset | 896,240 | 97,104 |
| 1,000 alternating unsuccessful queries | 470,480 | 150,904 |

- The unsuccessful-query guard allows 192,000 bytes, leaving runtime warm-up
  headroom while still rejecting the original filtering pipeline. The first
  successful focused run measured 150,904 bytes against its initial 160,000-byte
  budget; broad regression uses the more conservative budget, not a zero-allocation
  assertion. Required property notifications remain included.
- Scoped whitespace checks pass after expanding the parser's existing compressed
  extension switch entries. The complete solution builds with zero warnings/errors.
- Initial external build output passes focused tests but cannot satisfy existing
  source-level tests that locate the repository through the test assembly's parents.
  That unsuitable full-run attempt is stopped after the root-discovery failures;
  its logs are retained. Full verification uses separate ignored repository `obj`
  output instead, without changing tests or overwriting the running Debug app.

No AXAML or appearance is changed. Native screenshots, live/paid model calls,
publication and whole-product visual parity are not claimed for this slice.
Evidence, pre-change source, TRX and logs stay outside Git in
`%TEMP%\MossAgent-preview-cleanup-20261013-083312`.

##### Complete regression result and remaining limit

- The eight complete test assemblies pass 5,650/5,651 cases with zero skipped.
  Agent 119, Visual 12, Browser 76, Infrastructure 199, Windows 137, Providers
  516 and Tools 491 pass completely. App passes 4,100/4,101, including all 34
  new cases. This complete run is classified as failed, not as a full pass.
- The sole failure is the untouched
  `ProviderCatalogLookupTests.WarmModelCommandAndAvailabilityChecks_DoNotAllocate`
  case with `missing: false`: its zero-allocation assertion observes 2,920 bytes.
  A subsequent 217-case preview/lookup regression passes completely, including
  that case, rendered preview interactions and all new allocation guards. No
  provider implementation, old allocation assertion or test isolation is changed.
  The initial failure's cause is not established; the passing replay does not
  replace the failed complete App run.
- Subsequent focused/full measurements retain the parser/reset values in the
  table and measure 144,000 bytes for 1,000 unsuccessful queries. All query-budget
  checks pass. These samples remain workload-specific, not App memory claims.
- Repository architecture checks pass 7/7 within the Agent run. The scoped Roslyn
  audit verifies one type per file, no syntax errors and a longest method of 37
  lines. Scoped whitespace checks, explicit tracked/untracked source comparisons
  and the repository-default `git diff --check` pass.
- Builds use the existing Debug/JIT configuration in isolated output. The running
  default Debug App is not restarted or overwritten; its App DLL remains SHA-256
  `319A951FBB3562556F23F378B0EB6648059445D29B914560D107217D0E5D4FEC`.
  Automatic temporary-output cleanup is rejected by the execution-tool policy;
  outputs remain in ignored repository `obj` and the external evidence directory.
  Source baselines, reports and logs remain available. The Git index stays
  empty, all pre-existing changes are retained, and no commit/push/publish occurs.

This completes only the preview-projection cleanup, not the whole product goal
or a repair of the pre-existing allocation-test failure.
<!-- /source: UI_HOTPATH_OPTIMIZATION_ACCEPTANCE.md -->


---

<a id="workspace-lookup-optimization-acceptance"></a>

## WORKSPACE_LOOKUP_OPTIMIZATION_ACCEPTANCE.md

<!-- source: WORKSPACE_LOOKUP_OPTIMIZATION_ACCEPTANCE.md -->
### Workspace lookup optimization

#### Scope: 2026-10-12

This slice removes redundant work in existing workspace presentation. It does
not introduce another sidebar, task catalog, persistence port or navigation
workflow. Ordinary JIT debugging and Windows x64 single-file publishing remain
unchanged; there is no AOT or JSON source-generation change.

#### Design self-check

- App owns the synchronous in-memory lookup and tab projection. Dependency
  direction remains unchanged; Application repositories still own all I/O.
- The catalog, project navigation and tabs reuse one typed lookup helper for
  current project indices, projects, groups and tasks. Project editing no longer
  copies the entire project collection into a temporary list. Selected-tab
  lookup is folded into existing reconciliation; there is no cross-task cache.
- No new file, command, provider, browser or tool operation is introduced.
  Authorization, approval, risk, cancellation, deadlines, audit and output
  limits stay in existing execution paths. Closing a tab still closes only its
  presentation, not the stored task. No secrets or user data are read by tests.
- Every changed C# file retains one type and stays below 500 lines. The largest
  production file is the existing catalog at 464 lines; its longest method is
  43 lines. The new helper is 43 lines and has no method longer than ten lines.
  Tab synchronization remains 30 lines, below the 40-line preferred limit.
- Focused tests preserve first matching project/group/task, duplicate order,
  owning-project scope, full snapshots despite pin/search filtering, retained
  tab instances, current titles, draft selection, removed command parameters and
  close-without-archive behavior. Missing identities, removed rows and duplicate
  groups do not trigger fallback to a different owner or stale cached snapshot.
  Existing history, archive, lock/disposal and rendered-header tests are reused.

#### Baseline and allocation evidence

The initial baseline included an incorrect expectation that closing a manually
injected duplicate tab would immediately select its surviving duplicate. Existing
catalog selection suppresses an equal-record notification, so that expectation
was removed without changing production behavior. The corrected pre-change run
passes all four behavioral cases and fails only the four new allocation guards.

The subsequent 13-case focused run passes every case. Measurements use Windows
x64 .NET 10 Debug/JIT, current-thread allocation counters and 1,000 warm-up
iterations. Tab measurements invoke the existing synchronous projection via a
delegate bound once outside the measured region; no navigation, storage reads or
provider requests are included. Collections have 32 task records unless noted.

| Warm scenario | Iterations | Bytes before | Bytes after |
| --- | ---: | ---: | ---: |
| Task identity lookup, found | 10,000 | 960,000 | 0 |
| Task identity lookup, missing | 10,000 | 960,000 | 0 |
| Unchanged single-tab synchronization | 1,000 | 480,000 | 48,000 |
| Unchanged 32-tab synchronization | 1,000 | 7,424,000 | 48,000 |
| Combined project/index/group lookup, found or missing | 10,000 | Not measured | 0 |

The 32-tab reduction is 99.35%; retained allocations are existing property
notification overhead. This measures temporary allocations in these operations,
not whole-application memory consumption or a claimed UI frame-rate improvement.

#### Validation scope

- The new focused tests pass 13/13 with no skipped cases.
- The wider workspace/sidebar/project/history/header regression group passes
  491/491; architecture constraints pass 7/7, with no skipped cases.
- The complete serial App suite passes 4,032/4,032 with no failed or skipped
  cases. Solution build succeeds after the test host exits, with zero warnings
  and errors. Other test assemblies are built but are not rerun in this slice
  beyond the seven architecture cases.
- A final post-build replay of all 13 new cases passes; its task/catalog lookups
  still allocate zero bytes and both tab-count cases retain 48 bytes per refresh.
- A Roslyn source audit confirms one type per file, valid syntax and the file and
  method limits for all seven changed/new C# files.
- Scoped whitespace verification and repository `git diff --check` pass.
- No visual markup, shared appearance, component geometry, persisted JSON shape,
  database schema or execution pipeline changes. This code-only slice does not
  claim a new native screenshot comparison, Codex pixel equivalence, paid-provider
  acceptance or completion of the full product goal.
- Existing unrelated work remains intact. Nothing is staged, committed, pushed
  or published.

Evidence remains outside Git at
`%TEMP%\MossAgent-workspace-lookup-optimization-20261012-232727`: pre-change source
snapshots, `baseline.trx`, `baseline-corrected.trx`, `lookup-focused.trx`, run logs
and `source-audit.json`. Wider regressions and architecture results remain in
`workspace-regressions.trx` and `architecture.trx`; formatting and diff diagnostics
remain in `whitespace.log` and `diff-check.log`. The complete serial App run is
`app-serial.trx`, using the retained `serial.runsettings`; the solution build is
`solution-build.log` and the post-build replay is `final-replay.trx`.
<!-- /source: WORKSPACE_LOOKUP_OPTIMIZATION_ACCEPTANCE.md -->
