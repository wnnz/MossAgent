# 授权、文件安全与隔离研究

> 历史归档：保留原报告当时的结论、实验、失败与限制。整理于 2026-10-21。
> 当前能力与未完成方案统一维护在[功能清单](../FEATURE_STATUS_AND_IMPLEMENTATION_PLAN.md)；
> 原文中的计划、主机状态、测试数字及“正在进行”等措辞均为历史检查点。
> 代码块和仓库相对产物路径保留原意；本机忽略的 `artifacts/` 不随克隆分发。

[文档入口](../README.md)

## 原报告索引

- [任务产物文件安全增量](#artifact-handle-path-safety-acceptance) — `ARTIFACT_HANDLE_PATH_SAFETY_ACCEPTANCE.md`
- [Restricted-token runtime bootstrap diagnostic](#command-bootstrap-probe) — `COMMAND_BOOTSTRAP_PROBE.md`
- [Windows command filesystem isolation](#command-filesystem-isolation-plan) — `COMMAND_FILESYSTEM_ISOLATION_PLAN.md`
- [复制与目录创建句柄安全增量](#copy-handle-path-safety-acceptance) — `COPY_HANDLE_PATH_SAFETY_ACCEPTANCE.md`
- [删除与撤销的句柄安全增量](#delete-handle-path-safety-acceptance) — `DELETE_HANDLE_PATH_SAFETY_ACCEPTANCE.md`
- [移动与重命名句柄安全增量](#move-handle-path-safety-acceptance) — `MOVE_HANDLE_PATH_SAFETY_ACCEPTANCE.md`
- [编辑快照与产物所有权会话](#owned-artifact-session-acceptance) — `OWNED_ARTIFACT_SESSION_ACCEPTANCE.md`
- [项目授权版本与运行中撤销](#project-authorization-revocation-acceptance) — `PROJECT_AUTHORIZATION_REVOCATION_ACCEPTANCE.md`
- [项目授权与紧凑侧栏交互验收](#project-authorization-ui-acceptance) — `PROJECT_AUTHORIZATION_UI_ACCEPTANCE.md`
- [文件读取句柄的路径安全增量](#read-handle-path-safety-acceptance) — `READ_HANDLE_PATH_SAFETY_ACCEPTANCE.md`
- [授权根目录对象身份](#root-object-authorization-acceptance) — `ROOT_OBJECT_AUTHORIZATION_ACCEPTANCE.md`
- [Windows command isolation: backend and host decision](#windows-isolation-backend-decision) — `WINDOWS_ISOLATION_BACKEND_DECISION.md`
- [原子文本写入的目录句柄安全增量](#write-handle-path-safety-acceptance) — `WRITE_HANDLE_PATH_SAFETY_ACCEPTANCE.md`

---

<a id="artifact-handle-path-safety-acceptance"></a>

## ARTIFACT_HANDLE_PATH_SAFETY_ACCEPTANCE.md

<!-- source: ARTIFACT_HANDLE_PATH_SAFETY_ACCEPTANCE.md -->
### 任务产物文件安全增量

#### 设计自查与分阶段计划

1. Infrastructure 继续拥有产物内容读取，Tools.Abstractions 的现有读取句柄端口负责
   平台边界，Platform.Windows 实现；App 只组合。无需数据库迁移或反向依赖。
2. 第一阶段在读取字节前校验实际打开的流，唯一范围是所属任务产物目录，不采用
   项目根、应用数据根或其他任务目录。保留路径、长度、hash、媒体类型检查。
3. 内部产物预览不发起新工具副作用；源文件预览仍经统一工具管线。读取使用调用方
   取消，UI 的 30 秒期限保持；同步路径/句柄准备移至线程池，拒绝时关闭流。
4. 方法拆分及新增测试单文件单类型，检查 500/80 行硬限制。
5. 覆盖读取前拒绝、取消、任务隔离、实际 Windows 句柄及受控目录替换；生产组合
   验证工具预览、审计、任务产物重载与用户可见失败，不将模拟/Headless 当原生 GUI。

第二阶段逐项处理写入及清理，避免仅修读取就宣称全部产物安全。当前实际入口包括：
`FilePreviewTool`、`FileChangeSnapshotStorage`、`FileChangePatchBuilder`、
`FileChangeDiffRunner`、`BrowserScreenshotTool`、`BrowserDownloadTool`。它们仍存在
路径 API；当前只接入下述生产文件预览写入分支，其他入口尚未获得本轮句柄保证。
第三阶段整合构建、回归与剩余边界证据。

第二阶段先接入真实 `filesystem.preview_file` 产物写入：复用已有目录租约和原子
写入会话，校验任务目录名，租约持有到提交；失败由暂存自身句柄清理，不按路径
删除。此缓存写入是已授权只读预览的内部产物，仍保留原有 ReadOnly 工具分类和
审批行为，不扩大项目写入能力。其他产物入口分别推进，尚未完成。

MXC 环境未就绪，本轮不提权、不修改系统、不提交/推送/发布。

#### 已实现入口和实际链路

- `TaskArtifactContentReader` 通过现有 `IFileReadHandleValidator` 在读取前验证打开的
  流，仅传所属任务产物目录。App 的既有 Windows DI 注册自动注入该依赖，端口
  拒绝不回退。任务身份/路径、长度上限、实际长度、hash 和媒体类型检查保留。
  工作线程负责同步准备，调用方取消仍贯穿读取；异常和取消都关闭流。
- `filesystem.preview_file → FilePreviewTool → FilePreviewArtifactWriter` 复用生产
  `IAuthorizedDirectoryCreator` 和 `IAtomicFileWriteSessionFactory`。缓存目录 basename
  必须匹配当前 task ID；在可信运行上下文指定的产物父目录内创建/打开任务目录，
  持续持有目录链，暂存文件由原生句柄创建和提交。最终名称随机且只允许新建，
  提交前再次核实工作区及源路径授权。暂存失败清理只使用自身句柄。
- 只注入部分写入端口会失败，不默默回退。全部不注入的兼容宿主仍保留旧托管
  写入；未注入读取守卫的兼容构造也没有句柄保证。生产组合与端口拒绝分别验证。
- 预览源仍经统一只读工具执行、输出限制及审计。界面从该产物读取失败，不会把
  已成功生成缓存的工具审计改写为失败；错误/取消在预览面板独立呈现，不暴露原始
  异常。写入端口失败则工具审计失败且没有产物记录。已有缓存可显式重新打开。

#### 当前证据及必须区分的范围

初始探测尝试在产物流打开后改名父目录，实际被原有 `FileShare.Read` 拒绝，
不能将这个时序声称为旧漏洞成功复现或新守卫的功劳。失败 TRX 保存在 `read-race/`。
后续 Windows 边界检查分别证明：原有共享约束仍有效；通过 junction 打开的根外
句柄，在路径恢复为普通合法目录后仍指向原根外文件，新的原生校验拒绝该句柄且
Position 保持 0。合法路径此时可由实际产物读取器正常读取。

这属于实际文件/句柄边界证据，与读取器调用顺序的独立测试组合使用；不是原始
未修改程序集上从检查到打开的完整竞态复现。精确原始程序集竞态仍未验证，不能用
失败的初始探测代替成功反例。

专项通过：Infrastructure 新增 7 项，Windows 新增 10 项（读取边界 2，写入/并发 8），
App 新增 4 项。覆盖读取前拒绝和取消、元数据/错误任务提前拒绝、实际流关闭、
只读工具产物生成、原子暂存取消/超时清理、提交期间父目录不能替换、错误任务/
无根/junction 拒绝，以及一个任务取消不删除另一任务已提交的产物。

App 四场景使用保存的模型、实际 Agent、本地模拟 HTTP、生产原生文件端口、SQLite
和真实临时文件。模型编辑后用实际工作区预览面板打开文件，验证正常预览、读取
端口拒绝、写入端口拒绝及读取取消；前台草稿和任务完成状态保留。核实工具审计和
产物记录，重建服务后只在显式打开持久产物时读取，模型仍为 3 次请求、不重放编辑。
这是自动化/Headless 及本地模拟模型证据，没有原生 GUI、外部真实提供商或实际崩溃。

Debug 和 Release 均已完成：各配置完整解决方案构建零警告/错误，Tools 691、Windows
308、Infrastructure 795、App 精选 229，分别合计 2023 项全部通过。八份 TRX 均
Completed，无失败、跳过或中止；两配置构建前后及最终当前源码的 2561 项输入
hash 一致，终态核对记录为同证据目录中的 `accepted-verification.json`。
结构检查覆盖 2368 个 C# 文件、21182 个方法/访问器/lambda，最大文件 496 行、
最大方法 78 行，单文件单类型及硬规模约束零违规。原始证据位于
`artifacts/artifact-handle-safety-20261021/`；相关测试不等于整个解决方案测试全套。

#### 未完成部分与详细实施方案

以下为本检查点的缺口；后续编辑快照、普通预览和最终补丁的共享会话实现与新证据
见 [OWNED_ARTIFACT_SESSION_ACCEPTANCE.md](SECURITY.md#owned-artifact-session-acceptance)。
该增量只关闭对应生产写入/生命周期切片，Git 临时 diff、浏览器及恢复仍未完成。

1. **编辑快照。** `FileChangeSnapshotStorage.CaptureAsync` 仍按路径写入，`Delete`
   仍按名称清理。应提取共享的流式产物会话，保留未发布快照的原生所有权直到编辑
   提交；失败清理校验同一对象身份，不删除同名替换物。验证文本写入/替换/撤销的
   before/after 快照、取消、并发和每个持久化断点，不能仅验证普通预览成功。
2. **审阅补丁及临时 diff。** `FileChangePatchBuilder` 的最终 diff 与
   `FileChangeDiffRunner` 的 before/after 临时文件都需同一安全存储端口；目录也应
   通过租约创建并按已知身份清理。Git 子进程读取临时文件仍是进程隔离的一部分，
   不以文件写入安全替代 MXC 隔离。逐项验证审阅结果、输出限制、取消及遗留清理。
3. **浏览器截图/下载。** `BrowserScreenshotTool` 的写入和
   `BrowserDownloadTool` 的暂存、移动和失败清理需接入共享端口；保留任务浏览器
   归属、审批、取消、大小/hash 和产物审计。用本地页面及受控二进制下载，验证端口
   拒绝、替换、取消/超时、并发及进程中断，不操作用户日常窗口。
4. **授权与恢复。** 任务目录名检查不等于持久产物授权代际，当前仍信任 App 构造的
   ArtifactDirectory；尚未提供替换/撤权的 ABA 检测、崩溃日志或遗留物全量回收。
   应绑定任务与持久产物根身份、记录创建/提交阶段，用独立进程在暂存及提交前后
   终止，核实未知副作用、记录和草稿，不自动重放或按名称猜测删除。
5. **读取和平台限制。** 读取守卫是读取前检查点，不证明后续所有映射/特权写入的
   隔离，打开本身仍使用路径 API，不能证明打开前元数据或设备访问全部受控；
   原生同步调用没有硬实时中断保证。后续需要固定目录并相对无跟随打开的最小读取
   会话，明确拒绝不支持的对象；补完整原始读取竞态及根替换时序、
   硬链接/大小写/真实卷/长路径矩阵。预览写入继承原生写入端口的本地 NTFS 限制；
   其他文件系统不应静默回退。MXC、跨卷移动、其他文件入口及完整产品目标仍未完成。
<!-- /source: ARTIFACT_HANDLE_PATH_SAFETY_ACCEPTANCE.md -->


---

<a id="command-bootstrap-probe"></a>

## COMMAND_BOOTSTRAP_PROBE.md

<!-- source: COMMAND_BOOTSTRAP_PROBE.md -->
### Restricted-token runtime bootstrap diagnostic

#### New evidence (2026-10-19)

The independent native diagnostic in
`artifacts/command-bootstrap-probe-20261019` starts a copied Windows cmd with a
fully read/write restricted token and a unique restricting SID. Only its generated
allowed directory receives a filesystem ACE. It enables loader snaps in that
owned suspended child's PEB and reads native debug events; it changes no machine
registry settings. The child exits before executing the controlled read commands.

`baseline.log` identifies the first loader failure: opening the object-manager
directory `\KnownDlls` returns 0xc0000022. This is stronger evidence than the
previous generic child exit and does not establish that every later runtime
dependency is solved. `known-dll-access.log` confirms normal-token READ_CONTROL
succeeds and WRITE_DAC returns 0xc0000022. A separate private-window-station
experiment returns Win32 error 5 at CreateWindowStation; it is not a successful
desktop-isolation implementation. Windows Sandbox/its wsb CLI are absent on this
machine; no Windows feature was enabled or installed.

#### Prepared experiment and one authorized preflight attempt

`run-probe.ps1` defaults to a read-only plan. Its explicit `-Execute` branch is
prepared for a separately authorized elevated diagnostic. The helper has no
ownership-takeover or privilege-escalation fallback if its requested handles are
unavailable. It acquires every required READ_CONTROL/WRITE_DAC handle before
installing the first ACE.

The current bounded plan contains 44 existing objects:

- `\KnownDlls`: directory query/traverse only, mask 0x3.
- Its 43 section objects: section query/map-read/map-execute only, mask 0xd.

The exact names are in `runtime-lease-plan.log`. A fresh random diagnostic SID
receives those grants; existing package/world groups are not added to the token.
The intended child only reads generated inside/outside markers using a copied
cmd. There is no provider/model call or arbitrary agent command. No real volume,
Windows DLL file, project, registry, account or service ACL is in this experiment.
It does not install a service, change ownership, enable Windows features or claim
that the ordinary product launcher has become isolated.

The helper writes a SID/object/mask journal in its generated directory before
the first change. Normal exit and handled failure remove the exact SID/mask ACE
from each current DACL, preserving other entries. A cleanup error is reported;
ordinary completion checks the explicit cleanup result. A forced helper crash
can leave these volatile object grants until manual journal-based removal or OS
restart. This is a diagnostic limitation and disqualifies it from production
integration without durable recovery. Power loss, external concurrent DACL
writes, crash cleanup, full runtime boot, SDKs, ConPTY and production process-job
integration are not established by the helper.

#### Checks completed without elevation

`--lease-self-test` uses two newly created, owned named sections. It verifies
initial refusal under the restricted token, exact read access after the grant,
continued refusal of the other section, refusal after revoke, preservation of
a separately added ACE, restoration of the original DACL after both leases are
removed, and refusal to apply a grant through a handle lacking access. The native
build and these checks pass; see `lease-self-test.log`. No KnownDlls ACL is modified
by this self-test or by the read-only plan.

The source and executable hashes used for review are in `review-manifest.json`;
the launcher refuses a changed review identity. The user authorized one
administrator diagnostic. Its RunAs helper launched at 11:09 local time on
2026-10-19. Handle acquisition returned STATUS_ACCESS_DENIED (0xc0000022) before
any kernel ACE was installed or runtime journal created; see
`elevated-20261019-110907.log`. This attempt does not establish which later handle
failed or independently attest the helper's token elevation: the original log
does not record those details. No ownership/privilege fallback or second elevated
attempt was made.

`cleanup-audit.json` reconstructs each diagnostic's exact restricting SID from
its generated GUID and checks all generated files/directories: no matching grant
remains across three experiments. An earlier broad SID scan also listed two
pre-existing inherited workspace identities; those are not these diagnostics'
SIDs and were preserved. No known-DLL lease journal exists, matching the
preflight-before-apply design. The product safety blocker remains open in
[COMMAND_FILESYSTEM_ISOLATION_PLAN.md](SECURITY.md#command-filesystem-isolation-plan).

Follow-up `inspect-runtime.exe` performs only unelevated reads of the directory,
ntdll and kernel32 section DACLs/mandatory labels. Its own token is recorded as
unelevated/medium (integrity 8192). All three permit READ_CONTROL and deny its
WRITE_DAC requests; the retrieved label data does not establish a system-integrity
write barrier. This does not explain the earlier RunAs failure. The helper never
sets an ACL or requests elevation; see `runtime-object-access.log`. Any further
setup design must identify the exact failed handle/token before requesting wider
privileges. No permanent runtime broker has been implemented.

#### Read-only elevation check did not start

`artifacts/restricted-runtime-preflight-20261019/preflight.exe` is a separate
read-only diagnostic. It records process/thread token elevation and independently
requests READ_CONTROL and READ_CONTROL/WRITE_DAC on each of the same 44 objects;
it contains no ACL-write, ownership or privilege-adjustment operation. Its normal
run records an unelevated medium token, 44 readable objects and zero writable
DACL handles. This does not attest the earlier helper's token.

The follow-up RunAs request ended with Windows error 1223 (operation cancelled
by the user). No preflight process or `runas-readonly.log` was created; the
waiting launcher has exited. `runas-terminal.json` records a null child exit
code, so the launcher's own exit code must not be treated as diagnostic success.
No second ACL-changing attempt or automatic UAC retry was made. The exact failed
handle and elevation of the original ACL-changing attempt remain unknown. This
does not justify taking ownership or requesting SYSTEM access. Work on independent
conversation features can continue while production command isolation remains
an explicit open blocker.

The existing read-only preflight has now been source-reviewed with a SHA256-bound
launcher under artifacts/restricted-runtime-preflight-20261019. Its default mode
prints the plan; -Execute is reserved for fresh explicit user authorization. The
reviewed source records process/thread elevation and effective administrator
membership and reports each object independently; it has no ACL-write, ownership
or privilege-adjustment call. READONLY_REVIEW.md records the precise scope. A
new optional authorization request was pending at that checkpoint. It was later
answered and consumed as recorded below; earlier failed/cancelled attempts and
the consumed ACL-changing authorization remain unchanged.

#### Separately authorized read-only preflight: confirmed elevated refusal

The user subsequently authorized exactly one new read-only administrator preflight.
The hash-reviewed launcher ran once at 16:49:50 local time on 2026-10-19; its
child exited zero. `readonly-authorized-20261019-164950.log` independently records
an elevated, unrestricted high-integrity process token (elevation type 2,
integrity 12288), enabled Administrators membership and no thread impersonation
token. All 44 distinct objects permit READ_CONTROL and all 44 refuse the requested
WRITE_DAC handle with 0xc0000022. A successful inspection exit does not mean those
rights were granted. No ACL, ownership, privilege or system setting was changed.
All four reviewed file hashes match before/after. The new permission is consumed;
the earlier failed/cancelled logs remain unchanged. See
`artifacts/restricted-runtime-preflight-20261019/authorized-preflight-result.json`.

An additional unelevated standalone .NET diagnostic in
`artifacts/runtime-trust-label-20261019` reads three fixed objects only. Both
GetSecurityInfo and NtQuerySecurityObject return process trust label ACE type
0x14 with SID `S-1-19-512-8192`: WinTcb-Light according to the archived Microsoft
win32metadata SDK header constants. The KnownDlls directory's label mask is
0x00020003; ntdll/kernel32 section masks are 0x0002000f. None includes WRITE_DAC
(0x00040000). The native query retains raw ACE bytes as well as decoded SID/mask
and exact SDDL. The two sections also have low-integrity no-write-up labels;
those must not be confused with the separate WinTcb trust boundary.

The earlier descriptor formatter requested only SACL_SECURITY_INFORMATION while
converting a label-specific descriptor. That conversion omitted the label from
its text. The new diagnostic passes the exact queried information flag, so the
same descriptors display TL/ML correctly. Empty earlier text was not evidence of
an absent process trust label. Native descriptor bytes independently check the
formatting correction. Original failed/partial outputs are retained. Builds
have zero warnings/errors; all three opens and nine native descriptor queries
succeed. `verified-observations.json` records identities and limits.

This identifies an additional protection boundary consistent with the confirmed
elevated refusals, beyond the ordinary DACL's Administrators allow entry. It does
not attest the historical ACL-changing helper's exact token or failed handle.
The volatile KnownDll ACL lease route is rejected for product integration on this
host. Do not broaden it into trust-label removal, ownership takeover, a SYSTEM/PPL
workaround or another privileged attempt. A supported isolation backend and its
explicit setup/compatibility requirements need a new design. Ordinary command
filesystem confinement remains unresolved; no production launcher changed.
<!-- /source: COMMAND_BOOTSTRAP_PROBE.md -->


---

<a id="command-filesystem-isolation-plan"></a>

## COMMAND_FILESYSTEM_ISOLATION_PLAN.md

<!-- source: COMMAND_FILESYSTEM_ISOLATION_PLAN.md -->
### Windows command filesystem isolation

#### Confirmed blocker (2026-10-19)

The production launcher uses ordinary CreateProcess, bounded pipes and a
kill-on-close Job Object. ShellRunTool validates its working directory, but that
does not constrain filesystem access by the shell or its descendants. This
remains an unresolved product safety gap.

`artifacts/command-root-probe-20261019/verified.log` records a native Windows
reproduction through production App composition, ToolExecutor, isolated SQLite,
an authorized temporary directory and a separate temporary marker. FullAccess
shell returned the outside marker; filesystem.read_file rejected the same path
with path_denied. ReadOnly and ToolsEnabled=false refused the command. Four
audit entries were persisted. No user files, provider requests or existing app
windows were involved. This proves the gap, not a sandbox fix.

#### Independent feasibility experiment

`artifacts/appcontainer-probe-20261019` is a disposable native diagnostic outside
src/tests, excluded from shipping composition. It uses a random AppContainer
profile with no capabilities, a temporary-directory ACE and an allowlisted
environment. It does not change a real project's or volume root's ACL.

- cmd and Windows PowerShell read/write the granted directory while the outside
  marker remains inaccessible. Windows PowerShell rejects absolute outside reads,
  outside writes, a junction escape and a cmd descendant's outside read.
- PowerShell 7 passes the direct .NET filesystem checks after explicit
  Management/Utility assembly bootstrapping (assembly-bootstrap.log and
  cleanup-verified.log). This does not prove ordinary shell compatibility.
- A normal Get-Content check fails: the filesystem provider has no D: drive.
  New-PSDrive cannot register its inaccessible volume root. The failure remains
  in mounted-authorized-drive.log. Assembly loading alone is insufficient.
- A subsequent temporary-volume experiment passes Get-Content and Set-Content.
  It maps an unused per-user drive to a newly created diagnostic directory,
  grants only ReadAttributes/Traverse on that simulated volume root, and grants
  Modify only on its allowed child. Reading its outside marker and listing the
  root still return PermissionDenied. Exact mapping ownership and exact ACE
  removal protect cleanup. See volume-root.log. The original real-volume failure
  remains valid: no real disk-root ACL was changed. Installed PowerShell 7.5.4's
  matching source confirms that default-drive initialization checks root
  Directory.Exists; this is a compatibility prerequisite, not a justification
  to grant a whole volume or modify its ACL without a concrete setup design.
- Early cleanup using an unmodified DirectorySecurity snapshot did not persist
  removal. The diagnostic now removes only its unique-SID ACE from the current
  DACL and compares the result with the original. Nine old grants were removed
  only from generated diagnostic directories. Final cleanup verification passes.
- Default package-group exposure is now demonstrated, not merely hypothetical.
  With explicit read grants only on a generated outside marker and its generated
  parent, plain AppContainer returns the marker under S-1-15-2-1. LPAC refuses
  that group, but both plain AppContainer and LPAC return it under S-1-15-2-2.
  See package-grants-parent.log and restricted-package-grants.log. All of those
  temporary grants are removed. LPAC alone therefore does not satisfy a strict
  task-root allowlist either.
- A fully read/write restricted token with a fresh NT-authority restricting SID
  can be created without elevation, and CreateProcessAsUser can start a copied
  cmd image in the granted temporary root. The child exits STATUS_ACCESS_DENIED
  (0xc0000022) before producing inside/outside results. See
  restricted-token-unique.log. No system runtime ACL was altered. This establishes
  a runtime/bootstrap obstacle; it does not identify the exact denied DLL/object
  or prove the restricted-token design usable. Early attempts using the profile's
  package-authority SID returned error 87 and remain in the logs.
  A scoped impersonation check with the same valid NT restricting SID confirms
  inside-file reads succeed while the outside marker and System32/kernel32.dll
  reads are refused (restricted-token-access.log). Runtime read provisioning is
  therefore a real prerequisite, although that is not proof of the exact child
  initialization failure. No Windows runtime or desktop-object ACL was changed.
- A later native debugger reproducer now locates the first failed bootstrap
  access: opening the object-manager directory `\KnownDlls` returns 0xc0000022.
  Normal-token inspection can read its DACL but cannot acquire WRITE_DAC.
  [COMMAND_BOOTSTRAP_PROBE.md](SECURITY.md#command-bootstrap-probe) records a prepared, hash-bound diagnostic for 44
  volatile runtime objects and successful exact-ACE lease tests on self-owned
  sections. One user-authorized RunAs diagnostic then failed its handle preflight
  with 0xc0000022, before any system ACE or journal was created. The original log
  cannot identify the exact failed handle or attest its token; no broader
  privilege/ownership fallback or second elevated attempt was made. Exact-SID
  checks confirm no generated filesystem grant remains. No system-object ACL
  changed, and later runtime compatibility is unknown.
  A subsequent read-only token/object preflight did not start: Windows cancelled
  its RunAs request (error 1223). No child log was produced and no UAC retry was
  made. This supplies no new elevated-access result; see the bootstrap record.
- A later, separately authorized read-only preflight confirms an actually
  elevated high-integrity administrator token, 44 readable descriptors and zero
  WRITE_DAC handles: all 44 requests return 0xc0000022. Its one-time authorization
  is consumed, with no ACL/ownership/privilege mutation. Unelevated direct native
  queries identify WinTcb-Light process trust labels on KnownDlls and two
  inspected sections, whose lower-trust access masks exclude WRITE_DAC. Exact
  label-specific SDDL conversion corrects the earlier formatter's omission;
  raw ACE bytes independently confirm the labels. See [COMMAND_BOOTSTRAP_PROBE.md](SECURITY.md#command-bootstrap-probe)
  and `artifacts/runtime-trust-label-20261019/verified-observations.json`.
  This rejects the volatile KnownDll ACL lease route on this host; ordinary
  elevation/ownership assumptions cannot justify production setup. No trust-label
  bypass, SYSTEM/PPL workaround or further elevated attempt is authorized or
  implemented. A supported isolation backend needs separate design/validation,
  retaining Windows command compatibility and explicit setup requirements.

The experiment lacks production job/pipes/ConPTY, multiple-root leases, crash
recovery, comprehensive SDK/Git compatibility and network acceptance. Unsuccessful
logs remain preserved. It must not be described as production command isolation.

#### Design gate and required self-check

1. Tools.Abstractions owns an immutable process filesystem scope and launcher
   contract; Platform.Windows owns tokens, native attributes and ACL leases.
   Application owns recovery metadata if needed. UI acquires no native access.
2. Reuse the executor, root resolver, task Job Object, cancellation, bounded pipes
   and audits. Extend process/terminal ports rather than invoking another tool
   or parsing command text as a security boundary.
3. Revalidate roots/links before grants and launch. Separate task roots, narrowly
   declared read-only runtime dependencies and private task cache/temp paths.
   Rootless tasks cannot start commands. Never fall back to a host token when
   sandbox setup or compatibility checks fail.
4. Split profile, native capabilities, grant leases, environment and cleanup into
   separate types within existing size limits. Do not grow a giant launcher.
5. Prove normal pwsh/provider/module behavior first. Then test production shell,
   ConPTY and Git hooks/children with multiple roots, denied absolute/relative
   paths, junctions, handles, concurrency, timeout and cancellation.

#### Implementation sequence

Current supported-backend comparison and native SDK probe evidence are in
[WINDOWS_ISOLATION_BACKEND_DECISION.md](SECURITY.md#windows-isolation-backend-decision). This host is Windows 11 23H2 22631.3880;
Microsoft.Mxc.Sdk 1.0.1 reports IsSupported=true but request Probe selects only
AppContainerDacl, with BaseContainerApiPresent=false and DACL augmentation required.
The ordinary-user probe starts no workload or setup. Hyper-V is enabled, Windows
Containers/Sandbox disabled, Docker runs Linux. Windows Sandbox's single-instance
limit and the reviewed SDK's ProcessContainer PTY/cancellation limitations prevent
calling either a drop-in fix. A supported MXC acceptance host or a separately
provisioned Windows container/VM is required before changing the execution baseline.
The user selected native MXC with a separate supported Windows acceptance host
on 2026-10-20. No OS upgrade, host setup, weaker fallback or feature removal has
been authorized or performed. The current host's ordinary-user repeat probe still
selects AppContainerDacl; it starts no workload. The next dependency is access to
the supported acceptance host, not a repeated backend-choice question.

1. Implement the selected native MXC route on the supported independent host.
   Require the effective native tier at launch, including capability changes
   between probe and launch; reject DACL fallback. The global KnownDll ACL lease
   approach remains rejected. Retain temporary-volume/restricted-token and
   package-group exposure experiments as negative evidence, not implementation
   prerequisites or permission to change real root-volume ACLs.
2. Define native policy, task scope and cleanup before integration: explicit
   project roots, declared runtime reads, private temp/cache, process/SDK handle
   ownership and no inherited secret-bearing environment. Validate partial setup,
   launch failure, cancellation, late spawn, cleanup errors and crashes. Recovery
   may clean owned resources but never replay commands. Any genuinely required
   system preparation needs a separate reviewable scope and authorization.
3. Wire real shell and terminal scopes through shared process ports. Inventory
   other code execution, including Git hooks/config and configured MCP server
   lifetimes. Fixing shell alone cannot establish the whole boundary.
4. Validate sanitized environment, private caches and network/credentials with
   real SDK/Git commands using isolated data. Do not inherit arbitrary host
   secrets or silently disable capabilities for green tests.
5. Repeat the original production-pipeline reproduction. Require successful
   authorized commands, refused outside access, unchanged markers, complete
   audits, independent tasks and cleanup. Follow with focused tests, full
   build/regression and native terminal acceptance.

This blocker takes priority over Queue/Steer and other additions. Existing command
and terminal entries remain visible; safety completion is not claimed.

The selected independent native-MXC host is not yet ready. The user explicitly
requested local file safety work first; the read-handle and atomic-write-directory
slices are tracked separately and do not substitute for this blocker. No further
backend choice or local system provisioning is implied.
<!-- /source: COMMAND_FILESYSTEM_ISOLATION_PLAN.md -->


---

<a id="copy-handle-path-safety-acceptance"></a>

## COPY_HANDLE_PATH_SAFETY_ACCEPTANCE.md

<!-- source: COPY_HANDLE_PATH_SAFETY_ACCEPTANCE.md -->
### 复制与目录创建句柄安全增量

#### 设计自查与计划

1. Tools.Abstractions 定义源树读取和授权目录创建端口，Platform.Windows 实现原生
   句柄操作；Tools.BuiltIn 编排复制，App 仅注册。依赖方向不变。
2. 文件目标复用 AtomicTextFileWriter 的流写入和原生提交会话；目录创建共享新端口，
   不调用其他工具实现。普通文件及完整目录树复制都纳入本轮。
3. 保持 Mutation 审批、任务取消/超时、输出上限和审计。源树预检有节点/深度
   限制，拒绝链接和无法复制的附加数据，不跳过无法读取的内容。每次变更前复核授权。
   目录/多文件复制不是事务，部分完成必须可见，不自动重放。
4. 原生枚举复用现有 helper；源树、会话、目录创建和编排分开，单文件单类型，检查
   文件/方法硬规模限制。
5. 先用临时数据复现目标父目录被换为 junction 后的根外写入，再验证保护链路；
   覆盖源树/目标替换、遍历、多根、内容冲突、审批、取消、部分完成、并发、输出及
   生产模型/工具/SQLite 链路。完成后运行相关测试与完整 Debug/Release 构建。

原生 MXC 环境未就绪，本增量不修改系统或关闭用户窗口。之前读取、原子写入和
删除的证据不代替本轮验证；移动、artifact、Git 元数据和持续授权仍是独立缺口。

#### 已实现的实际链路

`AppComposition` 注册 `IFileTreeReadSessionFactory → WindowsFileTreeReadSessionFactory`、
`IAuthorizedDirectoryCreator → WindowsAuthorizedDirectoryCreator`，文件目标继续使用
`IAtomicFileWriteSessionFactory → WindowsAtomicFileWriteSessionFactory`。

- `filesystem.copy_path → PathCopyTool → PathCopyOperation` 在工作线程预检源树。
  源目录链和节点以相对句柄打开，复用 `NtQueryDirectoryFile` 枚举，不跟随链接，
  持有源文件及目录句柄至操作结束。文件流不分配每个文件 80 KiB 的常驻缓冲。
  最多 4096 个节点，根深度 0，允许 64 级子节点。准备阶段没有目标写入。
- 对每个目标目录，在授权根句柄下逐级打开/创建，明确区分新建和已存在。文件父
  目录的租约一直持有到原子写入完成；创建和写入之间不释放目录身份。工具还可
  独立通过 `filesystem.create_directory` 使用同一端口，支持缺失父目录和幂等重用。
- 文件复制使用已固定源流和 `AtomicTextFileWriter.WriteAsync`，逐文件暂存并原子
  提交，保持二进制内容。只有显式 overwrite 才允许覆盖；任务先前观察过的目标
  若被外部修改，原有版本校验会拒绝。已有目标不在复制中直接截断。
- 每个变更边界复核工作区、授权根和取消；Mutation 保持 AskEveryTime 的真实
  审批及 FullAccess 的现有策略。只读、无授权、审批拒绝在原生会话创建前拒绝。
- 已提交文件逐项清除任务缓存。取消、超时、撤权或后续节点失败可能部分完成，
  返回已提交文件和新建目录计数、目标路径及人工核实提示。公共已知文件异常基类
  携带有无已知副作用信息，使统一执行器保留这类中断细节；旧写入/删除错误码保留。
  不自动回滚或重放。成功结果也包含源/目标路径，并经过标准输出上限。

#### 证据和验证边界

真实反例：独立临时授权根中打开源文件后，将目标父目录改为 junction，指向另一
个临时根外目录。未接入端口的旧复制分支返回成功并覆盖根外哨兵；生产相同组合
的新分支拒绝，根外哨兵保留。这是实际文件操作，不是只验证字符串解析。
`control/` 保存首次单独复现，`CopyHandleRaceTests` 保留旧分支对照与新分支负例。
这里的对照通过只证明旧漏洞可复现，不能当成保护成功。

新增 Windows 专项 37 项（包含上述 1 项旧分支对照），生产 App 专项 7 项。覆盖：

| 范围 | 实际验证 |
| --- | --- |
| 文件/目录树 | 文本、任意字节、空目录、只读源、多授权根、授权根作为源、显式覆盖、源/目标保留 |
| 边界拒绝 | 源/目标父目录替换、junction、路径穿越、自身/内部复制、源写入占用、附加数据流、4096 节点和 64 级限制；源预检失败不创建目标 |
| 目标一致性 | 已观察目标外部变更、目标附加数据流拒绝，旧内容不截断；父目录在写入前持续持有，重命名被拒绝 |
| 审批/中断 | 审批/能力/授权拒绝不创建源会话；取消或超时关闭源流；一个文件提交后的取消、超时和撤权保留计数和已提交结果；父目录新建后取消报告留下的目录 |
| 并发和输出 | 一个任务等待/取消时，另一任务可从相同源复制到另一个目标；结果遵守输出限制 |
| 生产 App | 保存的模型参数 → 本地 HTTP 模拟模型 → Agent → 真正审批 → 被注册原生端口 → 真实文件/SQLite → 重建后对话/审计；成功、用户拒绝及端口拒绝分别检查，不新增模型请求或重放 |

Debug 和 Release 均已完成：各配置完整构建零警告/错误，Tools 691、Windows 266、
App 精选 118，分别合计 1075 项通过，三份 TRX 均 Completed，无失败、跳过或中止。
两配置构建前后及最终当前源码的 2540 项输入 hash 一致；最终核对记录位于
`artifacts/copy-handle-safety-20261021/accepted-verification.json`，同目录保存两配置
构建日志、TRX、终态及输入清单。此结果只对应本增量冻结时的源码。
结构检查：2,347 个 C# 文件、21,031 个方法/访问器/lambda，最大文件 496 行、
最大方法 78 行，单文件单类型和硬规模限制零违规。

本轮是自动化、真实本地文件/SQLite 和本地模拟模型证据，部分 App 为 Headless。
没有原生 GUI、真实提供商、其他真实卷、正式单文件发布或独立 MXC 验收。当前相关
回归不是整个解决方案测试全套，也不与前面读取/写入/删除的重叠检查累加覆盖率。

#### 已知限制和详细下一步

1. **复制语义和兼容性。** 保持原先“文件内容与目录结构复制”的范围，新文件继承
   目标目录权限，不声称完整克隆所有源 ACL/时间戳/属性。文件有 ADS/EA 时明确拒绝，
   避免静默漏掉附加内容。目录扩展元数据、EFS/稀疏/压缩、多用户、大小写敏感目录、
   UNC/ReFS、挂载点、8.3/保留设备名/长路径和硬链接仍需独立兼容矩阵；不能将本地
   普通 NTFS 测试推广到这些范围。需要先定义逐项保留规则，再实现或明确拒绝并验证。
2. **快照和并发。** 源树是准备时打开的节点集合；之后新增的源条目不会自动纳入。
   Win32 文件共享限制不是对已有可写映射或特权/驱动写入的完整隔离。目标沿用原子
   写入已知限制：最终内容校验和目录项替换仍非原子 CAS。下一步需要逐时序的 file ID、
   同内容不同文件、删除重建和映射写入反例，再决定可验证的条件提交方案。
3. **多节点事务、撤销及崩溃。** 不提供目录树事务或整个复制的 Undo。已知正常返回
   计数不能保证崩溃前全部持久化。需要逐文件变更记录/可信前后快照和安全恢复日志，
   并用独立子进程在预检、目录创建、暂存和提交后终止；验证中断审计、草稿和历史，
   仅显式人工继续，不自动重放。清理只能删除可证明归属且身份匹配的遗留物。
4. **其他入口。** 此复制检查点未保护移动；后续同卷移动增量见
   [MOVE_HANDLE_PATH_SAFETY_ACCEPTANCE.md](SECURITY.md#move-handle-path-safety-acceptance)，跨卷仍未完成。artifact 目标写入/清理、普通目录
   枚举、Git 元数据和其他内部路径也没有因本端口自动受保护。下一步分别建立目标/
   父目录替换反例，引入最小相对句柄端口，并验证实际模型/审批/审计和失败链路。
5. **持续授权。** 当前工作区和 CapturedToolRoots 的逐边界复核不等于持久授权代际、
   即时撤权或 ABA 检测。Application 应定义任务资源租约/代际，Infrastructure 用
   向前迁移持久化；审批和 I/O 校验同一代际，撤权取消任务所属资源，未知副作用需核实。
6. **宿主和总目标。** 生产 Windows 注入全部端口，端口失败不回退。未注入端口的
   兼容宿主仍保留旧托管实现（上述旧漏洞对照使用此分支），不具备句柄保证；其他
   平台需独立实现端口。命令/终端/Git 子进程/stdio MCP 的隔离仍按原生 MXC 路线，
   独立环境尚未就绪，不自动降级。R01/R02 和产品其他缺口保持未完成。
<!-- /source: COPY_HANDLE_PATH_SAFETY_ACCEPTANCE.md -->


---

<a id="delete-handle-path-safety-acceptance"></a>

## DELETE_HANDLE_PATH_SAFETY_ACCEPTANCE.md

<!-- source: DELETE_HANDLE_PATH_SAFETY_ACCEPTANCE.md -->
### 删除与撤销的句柄安全增量

#### 设计自查

1. Tools.Abstractions 定义删除会话端口，Platform.Windows 实现原生句柄操作；
   Tools.BuiltIn 负责授权复核、快照一致性与工具结果，App 只注册依赖。
2. 普通删除及撤销新建文件共用会话端口，不直接调用其他工具实现。
3. 保持 HighRisk 审批、任务取消、超时、输出限制及审计。每个删除前复核工作区
   和授权根；会话不创建缺失父目录，不按路径回退。预检最多 4096 个节点、64 层。
4. 每个手写 C# 文件一个类型；原生枚举、节点、会话与工厂分开，保持规模约束。
5. 临时 NTFS 目录验证父目录/目标替换、链接、文件占用、多根、缺失父目录、
   树大小/深度、撤销哈希/附加元数据、审批、取消、部分完成和生产 DI/持久化。

#### 边界和交付计划

删除会话固定本地 NTFS 大小写不敏感的普通目录链，以父句柄相对打开目标。
预检持有所有待删对象的读/删除句柄，不共享写入或删除；拒绝重解析点，目录用
句柄枚举，子节点同样相对打开。撤销新建文件只接受文件，在同一独占内容句柄上
比较 SHA-256，并拒绝当前无法识别的额外元数据。

预检成功后逆序逐节点提交删除并关闭句柄。不是目录树事务：后加入的文件应使
父目录删除失败并保留新内容；取消、权限变化或 I/O 失败可能留下部分完成结果。
结果计数指系统已接受的删除请求，其他读句柄可能延迟最终移除。部分完成必须
可见，清除失效的任务快照，要求人工核实，不自动重放。崩溃时已接受的删除也
不能回滚；不自动恢复执行。

#### 验证

本增量使用独立临时数据，没有操作现有用户窗口、修改系统 ACL 或执行提权。
专项测试检查点：Windows 32 项、App 新增 8 项；这些测试不是另外的产品完成度统计。
当前源码的终态证据位于 `artifacts/delete-handle-safety-20261021/`：
`verify.ps1`、`accepted-Debug-terminal.json`、`accepted-Release-terminal.json`、
`accepted-verification.json` 和 `structure-audit-reviewed.json`。

| 配置 | 完整解决方案构建 | Tools 全套 | Windows 全套 | App 精选文件/撤销/审批/恢复 | 合计 |
| --- | --- | --- | --- | --- | --- |
| Debug | 零警告、零错误 | 691 通过 | 229 通过 | 111 通过 | 1031 |
| Release | 零警告、零错误 | 691 通过 | 229 通过 | 111 通过 | 1031 |

三份 TRX 每配置均为 Completed，无失败、跳过或中止；2,515 项源码/配置/资源/脚本
输入前后和两配置间路径/hash 一致。结构检查：2,322 个 C# 文件、20,902 个方法/
访问器/lambda，最大文件 496 行、最大方法 78 行，单文件单类型及硬规模限制零违规。
这是相关回归与完整构建，不是整个解决方案测试全套。不要将与此前读取/写入重叠的
测试累加为独立产品覆盖率。

| 入口/行为 | 运行链路与专项证据 |
| --- | --- |
| 普通删除 | `filesystem.delete_path → PathDeleteTool → PathDeletion → IPathDeleteSessionFactory → WindowsPathDeleteSession`；生产 App 注册原生工厂；模型调用链验证本地模拟 HTTP、真实 Agent、审批、文件和 SQLite |
| 撤销新建文件 | 实际 Undo 命令 → `filesystem.remove_created_file → FileChangeRemoveCreatedTool → AtomicTextFileWriter.DeleteAsync → PathDeletion`；可信变更/快照验证仍保留；独占读取同一待删句柄并比对 SHA，外部内容和 ADS 不匹配均保留文件 |
| 目录递归 | `NtQueryDirectoryFile(FileNamesInformation)` 有界分页，子项相对父句柄无跟随打开；预检保持全部句柄，子项先删，根后删；junction、锁定文件、只读和超限在删除前拒绝 |
| 拒绝路径 | 无授权、只读策略、错误 schema、审批拒绝均不创建删除会话；固定后的目标/父目录重命名被拒绝，授权根及其祖先删除被拒绝；路径校验后父目录被换为 junction 时原生层拒绝且不触及根外哨兵 |
| 并发与中断 | 固定句柄拒绝目标写入、重命名和删除；会话取消释放句柄；一个任务等待或取消时另一个任务成功；删除后新增子项保留，最终目录移除失败 |
| 部分完成 | 第一个子项删除后取消、超时、撤权或新增子项失败，结果报告已接受 `1/2` 项、要求人工核实，清除任务缓存；超时后才返回的结果保留其完成说明，防止丢失已知副作用信息 |
| 持久化与审批 | 生产 Undo/Agent 结果写入对话和真实 SQLite 审计，重建服务后仍可读；审批拒绝、原生拒绝和成功分别检查；重载不新增模型请求或重放删除 |

实现保持现有 HighRisk 策略：`FullAccess` 不自动放行高风险删除；显式审批路径使用
`AskEveryTime`。初版管线夹具误用 FullAccess 等待会话，导致一次主动中止；保留
`pipeline-fixed/` 的非成功证据。改正夹具后专项通过，未放宽产品策略。另有一轮
编译发现测试误用快照存储 API，修正后重跑。两者均不记为成功检查点。

删除预检和执行由 Tools.BuiltIn 的工作任务运行，避免在 UI 线程同步扫描目录树。
递归删除最多 4096 个节点（包含目标），目标深度为 0，允许最多 64 级子节点。

原有读取/原子写入证据见相应 acceptance 文档，不能代替本增量。原生 GUI、真实
外部提供商、独立 MXC 环境及正式发布仍未验证。

#### 未完成范围和后续方案

1. **其他文件入口。** 复制目的地、移动、目录创建/枚举、artifact 写入与清理及 Git
   元数据仍不受删除会话保护。分别建立父目录替换的真实反例，再增加窄的相对句柄
   端口；生产注入、预检、审批、取消、审计逐入口闭环，不将此修复扩大为全文件系统安全。
2. **持续授权。** 每节点复核当前工作区及 CapturedToolRoots 仍非持久授权代际、即时
   撤权或 ABA 检测。后续由 Application 定义租约/代际，Infrastructure 向前迁移；
   审批及 I/O 边界验证代际，撤权取消所属资源，未知副作用需手动核实。
3. **崩溃与恢复。** 常规取消/超时和重新加载已验证；本增量未在原生删除中逐阶段
   强制终止进程。需独立子进程在预检、第一项删除和最后一项提交后终止，再重建
   服务，验证中断审计、草稿及结果并禁止重放。崩溃前已接受请求的精确计数可能
   尚未持久化，不能将正常返回时的计数保证扩展到强制终止。
4. **一致性与兼容性。** 不提供目录树事务或撤销删除。撤销基线仍以内容 SHA 和已
   支持的元数据检查为准，不是跨时间的 file-ID/元数据完整版本。Win32 共享限制
   不等于对已有可写映射、驱动或特权代码的全面隔离。需要独立验证可写映射、硬链接、
   多用户、大小写敏感目录、UNC/ReFS/卷挂载等；现有不支持路径继续明确拒绝。
   显式普通/递归删除仍按本次预检固定的对象执行，不与任务过去读取过的每个后代
   版本逐一比较。后续若支持可审核的删除计划，应保存节点身份/快照及授权代际，
   执行时逐项匹配，冲突则保留；本次句柄固定不等于历史版本冻结。
5. **非 Windows 宿主。** 未注入工厂的兼容宿主仍用原有托管删除方式，不具备本次原生
   句柄保证；生产 Windows DI 必须注入，工厂失败不回退。其他平台需要各自实现端口
   和负例，不能将托管测试当作同等安全证明。
6. **其他安全阻断。** 原子写入最终目录项 SHA 校验/替换仍非 CAS。命令、ConPTY、Git
   子进程和 stdio MCP 不受本端口约束；原生 MXC 路线保持，独立环境尚未就绪。
   整体目标继续 active；R01/R02 和总清单中的其他缺口没有因此完成。
<!-- /source: DELETE_HANDLE_PATH_SAFETY_ACCEPTANCE.md -->


---

<a id="move-handle-path-safety-acceptance"></a>

## MOVE_HANDLE_PATH_SAFETY_ACCEPTANCE.md

<!-- source: MOVE_HANDLE_PATH_SAFETY_ACCEPTANCE.md -->
### 移动与重命名句柄安全增量

#### 设计自查与分阶段计划

1. Tools.Abstractions 定义移动会话；Platform.Windows 固定源父目录和源对象并执行
   相对目标目录句柄重命名；Tools.BuiltIn 编排授权、版本检查及父目录创建，App 仅注册。
2. 复用目录租约、原生无跟随打开和重命名 helper，不调用其他工具实现。
3. 保持统一 Mutation 审批、任务取消/超时、有界输出及审计。源对象禁止共享写入/
   删除；提交前再次复核授权。先准备源再创建目标目录；失败时明确报告留下的目录。
4. 会话、工厂和工具编排分文件，单文件单类型，检查 500/80 行硬限制。
5. 分别验证实际文件/目录重命名、父目录替换、链接、外部内容改变、覆盖、审批、
   取消、超时、并发和生产保存模型→审批→原生端口→SQLite/重载。最后完整构建并
   运行相关回归。当前仅为计划，不代表验证通过。

第一阶段保护同一本地 NTFS 卷内的移动；第二阶段验证生产链路与失败路径；第三阶段
整理兼容性及恢复缺口。跨卷复制再删除需要独立事务和恢复设计，原生分支明确拒绝，
不得静默回退到路径复制/删除，也不将跨卷能力记为完成。目录移动不读取或复制后代，
是整个目录对象的重命名，不声称固定目录全部后代。原生 MXC 独立环境仍未就绪。

#### 已实现入口和运行链路

`AppComposition` 注册 `IPathMoveSessionFactory → WindowsPathMoveSessionFactory`，
`filesystem.move_path → PathMoveTool → PathMoveOperation` 在线程池工作任务中执行，
通过相同的工具 schema、能力、路径、审批、取消/超时、输出和审计管线。

- 原生会话从授权根固定源父目录链，再相对无跟随打开源对象，拒绝重解析点及
  不支持的目录。源句柄不共享写入或删除，持续持有到结束；不会先删除源。
- 文件源在同一固定流上计算哈希，核对任务先前读取版本。若先前读过的文件已变成
  目录，或目录目标曾观察为文件后被删除，拒绝悄悄接受类型/存在性改变。
- 文件目标同样核对任务观察版本；覆盖须显式启用。已有目标在提交前重新打开、
  哈希并检查元数据，不允许静默丢失目标 ADS/EA。源的 ADS 不通过内容复制重建，
  而随同卷对象重命名保留。目录目标必须不存在，不替换已有目录。
- 源准备完成后，使用授权目录创建端口逐级创建目标父目录，租约保持至提交结束。
  最终再相对打开目标父目录，复核授权及取消，使用源句柄执行相对目标父句柄的
  原生重命名。失败不调用旧路径 API，也不退回复制后删除。
- 成功后清除源与目标的精确路径任务快照。若目标父目录已新建而随后取消、超时、
  撤权或失败，显示已知新建数量、确认的重命名次数和路径，要求人工核实，不回滚/
  重放。未知异常不向用户泄露原始诊断。
- 跨卷在打开源和创建目标前明确返回 `file_move_cross_volume`。当前兼容宿主全部
  不注入端口时仍保留旧托管分支；只注入部分依赖则失败，生产 Windows 已注入全部。
  兼容分支不具备这些安全保证，也不能用其结果证明 Windows 原生分支。

#### 验证证据及边界

当前专项验证：Windows 32 项、生产 App 6 项通过。Windows 测试覆盖真实临时文件/
目录移动、显式覆盖、多授权根、缺失父目录、源及目标外部变更、晚出现目标、源
句柄阻止写入/改名、父目录换成根外 junction、路径穿越/根目录/自身及内部目标拒绝、
源 ADS 保留与目标 ADS 拒绝、目录目标拒绝、成功后的快照失效、审批和能力拒绝、
取消/超时释放句柄、创建父目录后取消/超时/撤权的部分结果、独立任务并发及输出上限。
跨卷检查只证明不同盘符的提前拒绝，未在另一真实卷上执行移动。

App 六场景分别为文件/目录的成功、用户拒绝和端口拒绝。使用生产组合及保存模型，
经本地模拟 HTTP、Agent、真实待审批状态进入原生端口；审批前零原生调用，核实实际
文件/空目录、根外哨兵、SQLite 历史/审计和重建后展示，模型请求数不增加、不重放。
这不是实际外部模型、原生 GUI 或真实进程崩溃验收。

Debug 和 Release 均已完成：各配置完整解决方案构建零警告/错误，Tools 全套 691、
Windows 全套 298、App 精选 124，分别合计 1113 项全部通过。六份 TRX 均 Completed，
无失败、跳过或中止；两配置构建前后及最终当前源码的 2552 项输入 hash 一致，
核对记录为同证据目录的 `accepted-verification.json`。
结构检查覆盖 2359 个 C# 文件、21110 个方法/访问器/lambda，最大文件 496 行、
最大方法 78 行；单文件单类型及硬规模约束零违规。

证据目录：`artifacts/move-handle-safety-20261021/`，保留原始初次编译/测试失败、
修正后的专项及最终构建日志、TRX、输入清单和结构报告；初次失败不作为验收结果。
相关回归不是全解决方案测试，不与之前专项的重叠回归累加覆盖率。

#### 剩余缺口与详细方案

1. **最终名称与并发。** 覆盖前哈希和目录项替换不是原子 CAS，目标句柄允许删除
   共享。下一步先复现最终哈希后同名替换、同内容不同 file ID、删除重建和已有可写
   映射的时序；设计可证明的身份/版本条件提交方案，未解决前不声称全部并发隔离。
   目录源不枚举和固定全部后代，目录内部并发改变随整个对象迁移，不是树快照。
   同步原生调用也没有可强制中断的硬实时取消保证，不能以工具超时结果代替该证明。
2. **跨卷。** 同卷重命名不等于跨卷移动。需要独立复制/逐文件版本及元数据保留
   规则、持久化阶段日志、复制完成后源身份核验和安全删除，再验证任何断点不丢源、
   已完成目标可见、失败不自动重放。当前明确拒绝，保留功能缺口。
3. **恢复、审阅与撤销。** 尚无移动专用变更记录、目录后代快照整体失效、Undo 或
   重命名前后提交日志。需要记录源/目标身份和阶段、保留有界快照，再以独立子进程
   在父目录创建和原生提交后终止；确认中断审计、草稿和历史，人工核实未知副作用。
   精确路径快照失效不等于全部后代缓存已更新，后续编辑仍需重新读取。
4. **兼容性。** 本机普通本地 NTFS 的证据不能推广到 UNC/ReFS、真实其他卷、大小写
   敏感目录、挂载点、硬链接/8.3/设备名/长路径及多用户/加密文件。大小写别名按同一
   路径拒绝，尚未实现仅改变大小写的重命名。应建立逐项原生兼容矩阵和明确拒绝测试。
5. **总安全范围。** Artifact 写入/清理、普通目录枚举、Git 元数据和持久授权代际
   仍未由本会话保护。先建立每个入口的实际替换反例，再接入最小句柄端口与生产
   链路。持续授权需要 Application 租约/代际、Infrastructure 向前迁移以及审批/I/O
   同代校验。命令/终端/Git 进程/stdio MCP 隔离继续按原生 MXC 独立环境路线推进。

下一项本地修复可从 `Infrastructure/Persistence/TaskArtifactContentReader` 开始：
当前实现只有打开前后的路径检查，没有在读字节前验证实际打开句柄。先在独立任务
artifact 目录复现父目录替换时序，再注入现有读取句柄端口，以拥有该 artifact 的
任务目录为唯一范围，验证错误任务、junction、取消、大小/hash/媒体类型及生产
组合；之后分别处理 artifact 的写入和清理，不把读取修复扩大描述为全部产物保护。
<!-- /source: MOVE_HANDLE_PATH_SAFETY_ACCEPTANCE.md -->


---

<a id="owned-artifact-session-acceptance"></a>

## OWNED_ARTIFACT_SESSION_ACCEPTANCE.md

<!-- source: OWNED_ARTIFACT_SESSION_ACCEPTANCE.md -->
### 编辑快照与产物所有权会话

#### 设计自查和计划

1. Tools.Abstractions 定义产物文件会话和工厂；Platform.Windows 复用现有目录租约/
   原子写入会话实现；Tools.BuiltIn 管理快照和调用方成功边界，App 仅注册。
2. 现有原子写入端口在提交后不再删除文件，不能独自表达“快照生成成功但项目编辑
   尚未提交”。新端口分别表示内容完成和调用方确认保留，持续持有原句柄及目录。
3. 任务产物目录校验沿用 task ID 与可信运行上下文，统一工具审批/超时/取消/输出/
   审计不变。未保留产物只通过原句柄清理；清理失败需明确反馈，不按名称回退。
4. 快照、会话、工厂和有界载荷编排分文件，单类型及 500/80 行硬限制不变。
5. 分阶段接入预览、编辑 before/after 快照及最终审阅补丁。验证成功/拒绝/失败/
   取消/并发、清理失败、文件替换边界，以及生产保存模型、编辑、审阅、撤销和重载。
   完成相关 Debug/Release 构建及回归，原生 GUI 和崩溃验收分别列出。

本检查点尚未处理 Git diff 临时工作文件和浏览器产物。后续浏览器增量见
[BROWSER_ARTIFACT_SESSION_ACCEPTANCE.md](BROWSER_MCP.md#browser-artifact-session-acceptance)；
它不改变本检查点的证据范围。Git 临时输入的后续实现见
[GIT_DIFF_INPUT_SESSION_ACCEPTANCE.md](GIT_WORKTREES.md#git-diff-input-session-acceptance)，
该增量同样不扩展本检查点的历史验证范围。
不修改系统配置、不提权、不提交/推送/发布，不清理已有用户数据或旧产物。

#### 已实现的生产链路

- App 注册 `IArtifactFileSessionFactory`，Windows 实现复用目录租约和原子写入端口。
  工厂只接受绝对路径且末级为当前 task ID 的可信产物目录，以及有界的字母数字名称。
  持有本地 NTFS 目录链，随机新文件名不允许覆盖已有文件；拒绝不回退到路径操作。
- `CompleteAsync` 完成写入和相对重命名，继续独占持有文件及目录；`Retain` 是调用方
  成功后的保留确认。未 Retain 的会话在 Dispose 时通过原句柄标记删除，随后关闭。
  已完成内容仍可回滚，不再使用“原子写入提交即永远保留”的生命周期。
- `FilePreviewTool` 使用共享有界载荷写入器，最终复核源路径、工作区和取消后保留。
  这是任务内缓存写入，原只读工具能力及审批含义不变。
- `AtomicTextFileWriter` 的 before/after 快照持有至项目文件提交成功；失败、取消或
  冲突会清理已完成及未完成快照。源 hash 和复制后目标 hash 都核对，名称、2 MiB
  限额和现有快照元数据保持兼容。内容未变化时不创建快照。
- `FileChangeReviewTool` 的最终 `.diff` 使用同一会话，补丁生成后再复核源授权和取消。
  生产分支不再在调用方失败时按补丁路径删除。本检查点的 Git 临时 before/after 输入
  仍是旧链路，必须与最终产物的保护区分；后续专用输入会话见文首链接。
- 清理失败返回本地化 `artifact_cleanup_failed`；取消/超时仍保留对应错误码，并在
  摘要携带可能遗留产物的说明。关闭各个快照时收集首个已知错误，继续释放其他会话。
  SQLite 仍只保存已有元数据/审计，无 schema 修改，无自动重放或历史数据改写。

未注入新端口的兼容宿主仍使用原有实现；部分旧预览端口配置不完整仍拒绝。
这不提供其他宿主的原生安全保证，也不允许生产端口失败后退回兼容实现。

#### 针对性验证及证据性质

新增 37 项 Windows 测试和 6 项 App 流程测试。Windows 测试使用真实本地临时 NTFS
文件、junction 和原生句柄；通过明确回调 gate 控制竞争/取消/外部修改，不声称复现
原始未修改程序集的完整竞态。超时使用确定性测试时钟。

覆盖未完成/完成未保留的清理、成功保留、独占文件与父目录替换拒绝、同名冲突保留
别人的文件、错误任务/名称/相对路径/junction 拒绝、取消、输出限制和目录租约释放。
实际设置自己持有快照的只读属性制造清理拒绝，验证残留可查看、错误明确且所有流关闭；
测试仅在自己的临时目录恢复属性。没有修改用户目录、ACL 或系统设置。

编辑测试在两份快照都完成后引入冲突、撤权、取消和超时，验证不提交项目且全部清理；
在初始 hash 后改动暂存源流，验证复制后的 hash 能拒绝不一致快照。两任务真实交叠：
第一任务等待时第二任务完成编辑并保留快照，第一任务取消或冲突只清理自己的文件。
预览、最终补丁及编辑分别覆盖最终检查拒绝；旧预览专项也已切换到新生产端口。

App 使用生产组合、保存的模型/提供商设置、本地模拟 Responses HTTP、Agent、真实
审批、文件系统及 SQLite。新增链路包含编辑→快照→补丁预览→重建服务再审阅→确认撤销
→重建服务恢复；另验证审批拒绝、快照端口失败、快照完成后的主动取消和真实清理失败。
草稿及根外哨兵保留，失败审计/消息可重载。重建服务不是实际 EXE 崩溃或原生 GUI 验收。

初始 App 运行的两项失败是旧会话计数断言仍期待 1，现分别核对项目会话 1 和快照会话 2；
另一项新断言曾错误枚举审批拒绝后不存在的产物目录，已改为断言没有创建目录。
这些失败 TRX 保留于 `artifacts/owned-artifact-safety-20261021/`，不计作产品缺陷已修复的证据。

#### 当前 Debug / Release 验证

| 检查 | Debug | Release |
| --- | --- | --- |
| 完整解决方案构建 | 零警告、零错误 | 零警告、零错误 |
| Tools 完整测试 | 691 / 691 | 691 / 691 |
| Platform.Windows 完整测试 | 345 / 345 | 345 / 345 |
| Infrastructure 完整测试 | 795 / 795 | 795 / 795 |
| App 相关精选测试 | 239 / 239 | 239 / 239 |
| 每配置合计 | 2070 / 2070 | 2070 / 2070 |

八份最终 TRX 均 Completed，零失败、跳过或中止。两配置构建前后、配置之间及最终
工作区的 2578 项输入路径/hash 一致。结构检查覆盖 2385 个 C# 文件和 21289 个方法/
访问器/lambda，最大文件 496 行、最大方法 78 行，单文件单类型和硬规模限制零违规。
结构工具首次显式加载 App 输出目录 Roslyn 时发生版本冲突；宿主已有 Roslyn 可用，
同次结构检查最终正常完成。该加载报错没有被计为构建或测试成功。

原始构建日志、最终 TRX、输入清单及终态摘要位于
`artifacts/owned-artifact-safety-20261021/`；`accepted-verification.json` 逐份复核最终报告、
两个配置及当前输入，`verify.ps1` 和 `review-verification.ps1` 保留可重复的验证范围。
App 精选包括新会话、原产物预览、读写/复制/移动/删除、快照审阅/撤销、审批及恢复。
这是相关回归，不是整个解决方案测试全套，也不是发布包、真实账号或实机验收。

#### 未完成部分及实施方案

1. **Git 临时 diff 输入（本检查点的后续计划）。** 本检查点的 `FileChangeDiffRunner` 仍以路径创建/读取/清理目录与文件。
   下一步先验证 Git 打开文件的共享参数，设计同时允许受控读取且拒绝外部写入/删除的
   所有权协议，目录创建和清理复用原生租约。若不能证明共享协议，评估不依赖路径临时
   文件的差异引擎，并要求保留统一 diff、编码、numstat、限额和取消语义。不能直接关闭
   独占流后按路径重开，也不能为测试随意放宽 FileShare。进程读取隔离仍依赖 R01/MXC。
   该共享读取协议和专用原生会话的后续实现、真实 Git/生产 App 验证已记录在
   [GIT_DIFF_INPUT_SESSION_ACCEPTANCE.md](GIT_WORKTREES.md#git-diff-input-session-acceptance)；
   持久身份、授权代际、宿主崩溃及进程隔离仍未完成。
2. **浏览器截图与下载（本检查点的后续计划）。** 将截图 bytes 接入有界产物载荷会话；下载先核实 SDK 暂存身份、
   任务归属与大小，再通过受控源流复制到新会话。成功后才 Retain，取消和失败按原对象
   清理，不直接把 SDK 路径视为可信。用本地页面/二进制下载验证审批、取消/超时、替换、
   并发、hash、审计及进程中断；不操作用户日常窗口。
   受控导出会话和原 SDK 流/对象清理的后续实现及证据已记录在
   [BROWSER_ARTIFACT_SESSION_ACCEPTANCE.md](BROWSER_MCP.md#browser-artifact-session-acceptance)；
   SDK 缓存未知状态、配额和进程中断仍未完成。
3. **产物授权与崩溃恢复。** 目前仍信任 App 提供的产物父目录和任务 ID；没有持久根身份、
   授权代际、创建日志或原子元数据发布。应以向前迁移绑定根身份和创建/保留阶段，启动只
   显示遗留/未知状态与人工核实动作，不自动按名称删除。对创建后、Complete 后、项目
   提交后、审计前后分别终止独立宿主，核实草稿、历史和另一任务且不重放副作用。
4. **回收与撤权。** 当前会话只清理自己未保留的文件，空任务目录可能留下；既有历史
   快照不做全量垃圾回收。回收必须以数据库引用、运行租约及宽限期为依据，删除前校验
   同一对象身份。统一授权代际与工具取消仍见 R02，不能用路径复核声称即时撤权已完成。
5. **平台与实机。** 原生会话继承本地 NTFS、普通大小写不敏感目录及元数据限制；同步
   Windows 调用不保证硬实时取消。其他卷/大小写/长路径组合、原生 GUI、真实提供商、
   崩溃断点及 MXC 验收仍未完成。当前主机不具备原生 MXC，独立环境尚未就绪。
<!-- /source: OWNED_ARTIFACT_SESSION_ACCEPTANCE.md -->


---

<a id="project-authorization-revocation-acceptance"></a>

## PROJECT_AUTHORIZATION_REVOCATION_ACCEPTANCE.md

<!-- source: PROJECT_AUTHORIZATION_REVOCATION_ACCEPTANCE.md -->
### 项目授权版本与运行中撤销

#### 设计自查与实施顺序

1. Domain 保存项目授权版本和安全异常；Tools.Abstractions 定义任务租约，Application
   编排授权读取、通知和任务所属撤销；Infrastructure 用向前迁移和 SQLite 事务保存
   版本。App 只接线和显示结果，Windows 继续负责实际进程及终端资源。
2. 复用工作区守卫、统一工具执行器和 Agent 入口，兼容工作树校验。项目目录的任何
   修改都结束旧授权运行；新增目录不会自动扩大既有运行权限。名称修改不撤销。
3. 保存成功后通知本实例的租约取消；执行前/审批后/结果发布前复核持久版本，并以
   有界轮询发现其他实例保存的修改。租约只属于本任务/操作，撤销不可重新激活；
   重新授权必须开启新运行。保留既有审批、超时、输出限额、审计及进程终止链路。
4. 新类型分别成文件；改动后的文件/方法仍需满足 500/80 行硬限制。
5. 先验证迁移、版本递增及旧版本保存拒绝，再验证租约注册/通知/轮询/释放竞争；
   贯通保存设置、真实文件/命令、审批取消、另一任务继续和服务重建。终端会话需要
   独立持有撤销租约直到真实资源退出，不能随 start 工具返回而释放。

收尾的保存竞态修复由 Application 写端口及 Infrastructure 事务返回值承担；App 只消费
提交结果，不增加 SQL 或新的工具执行入口。沿用保存操作的取消和 30 秒编辑器时限，
不更改授权/审批策略；新增端口和两场景测试分别单类型成文件，方法保持 80 行内。
测试在提交通知中确定性插入另一仓库保存，验证本窗口不能借用新版本覆盖外部授权。

持久版本不等同于文件系统根对象身份或 OS 隔离。已发生的副作用不会回滚或自动
重放；原生同步 I/O、外部实例轮询间隔和取消到进程退出的实际时延需如实记录。
MXC 仍等待用户选择的受支持独立环境；本轮不提权、不修改系统配置、不提交/发布。

#### 生产链路与行为

- schema 26 为项目新增正整数 `authorization_revision`，旧项目初值 1；主目录或授权目录
  JSON 改变时由事务内 trigger 递增，改名称不递增。旧版本保存用条件 upsert 拒绝，
  新建只接受初始版本 1；失败不发成功通知。授权目录删除后恢复相同字符串也不会
  恢复旧版本的运行权限。版本不是对所有项目字段的乐观锁。
- `WorkspaceProjectService` 通过 Application 的 `IProjectAuthorizationWriter` 获得本次
  事务实际提交的快照，不在提交后重读并采用另一窗口的新版本。无该端口的兼容仓库
  只返回提交输入，不提升版本。编辑器的旧版本冲突会保留输入并提示重新加载；保存
  期间继续编辑的草稿保留，只推进自身已成功提交的版本。
- 生产 DI 注入 `ProjectAuthorizationMonitor`；`WorkspaceTaskFactory` 为普通项目及工作树
  都创建 `ProjectWorkspaceGuard`，工作树仍执行原 Git 环境守卫。task ID、工作目录和
  roots 必须匹配捕获的上下文，无项目聊天仍不获得工具或目录授权。
- `AgentRunner`、统一 `ToolExecutor` 与终端各自持有租约。租约注册前后读取持久版本；
  提交成功后撤销本实例旧租约，每 500 ms 轮询发现其他实例修改或项目消失，读取
  失败保守撤销。单次轮询读取的 5 秒 deadline 会结束等待，即使读取端口不合作。
  过时通知不撤销新版本；取消或释放不会重新激活旧租约。
- 审批等待和执行接入租约取消，工具结束再验证版本。撤销结果使用
  `workspace_authorization_changed`；保留已知的产物、文件变化、子对话及完成摘要，
  不将已发生的效果冒充回滚。Agent 保留工具终态后结束运行，任务失败原因 17 可持久化，
  Queue 停止自动续发，待发消息和前台草稿保留。
- `terminal.start` 在带租约的上下文必须调用支持授权的终端管理器，否则拒绝；终端
  独立持有租约至资源退出，不能随 start 工具返回释放。撤销只停止捕获的 owner，
  写入/resize 在停止期间拒绝，清理失败保留 `CleanupFailed`，不误停新会话或其他任务。
  终端界面将授权版本变化映射为专用本地化提示，要求刷新项目、核对目录后手动启动。
- 初始化器现在在同一迁移事务内复核版本标记。本轮故障注入曾观察到最后的版本
  插入被拒绝、批处理却无异常返回，造成结构已提交而版本缺失；新增复核会回滚，
  并用该反例验证列、trigger、历史及约束没有半迁移。

#### 实际证据

自动化测试使用临时项目/数据、合成凭据、loopback 模型和独立进程，不调用付费模型，
不操作用户窗口，不改变审批策略或系统 ACL。

| 层次 | 当前已执行场景 | 范围 |
| --- | --- | --- |
| 持久化及 Application | schema 25→26、重复初始化、故障回滚、旧历史/失败原因保留、非法版本拒绝、名称与目录区别、并发保存、ABA、跨实例轮询、通知注册竞争、旧通知、坏观察者、取消/释放、task/WD/roots 篡改、不合作读取 deadline、并发释放等待撤销回调结束 | `ProjectAuthorizationMigrationTests` / `PersistenceTests` / `MonitorTests`，19 项专项通过 |
| 工具统一管线 | 审批中撤销、不执行；审批后入工具前拒绝；执行中取消和迟到结果；真实 SQLite 审计终结 | `ProjectAuthorizationToolTests`，4 项专项通过；工具执行 gate 是可控测试实现 |
| Agent | 流式 provider 取消、保留部分文本、明确授权失败；先保留工具结果再终止、旧上下文不能续跑 | `ProjectAuthorizationAgentTests`，2 项专项通过；provider 是可控测试实现 |
| 终端生命周期 | start 返回后撤销、另一个项目继续、旧 owner 不影响新 owner、创建中撤销/迟到资源、清理失败；真实 ConPTY 父子进程退出 | `ProjectAuthorizationPtyTests`，4 项通过；前三项用可控 PTY connection，末项为本机真实 PowerShell/ConPTY |
| 生产 App 组合 | 实际 SQLite 保存 → monitor → Agent 请求取消/队列暂停，另一个项目独立请求继续、前台草稿保留；旧编辑器保存冲突保留输入 | `ProjectAuthorizationRuntimeTests`，2 项通过；HTTP 模型为 loopback，非原生窗口交互 |
| 保存结果竞态 | 新建/修改提交后，另一独立 SQLite 仓库立即保存更高授权版本；本窗口返回自己的已提交版本，后续旧草稿覆盖被拒绝 | `ProjectAuthorizationSaveRaceTests`，2 项先复现失败，修复后通过；不发送模型请求 |
| 生产 App 原生终端 | 标准 ToolExecutor → `terminal.start/write` → ConPTY + PowerShell 父/子进程；真实 SQLite 授权保存 → 退出 → 旧上下文拒绝，三条审计均完成 | `ProjectAuthorizationTerminalTests`，1 项通过；是真实终端进程，不是桌面窗口手工验收 |
| Avalonia Headless | 两种主题 × 两种尺寸；实际键盘/控件输入保存目录，撤销待审批运行，新任务用新目录完成真实文件写入，服务重建后旧失败仍可看、不重放 | `ProjectAuthorizationUiWorkflowTests`；修改后的撤销场景 4 项通过；原有移除目录/恢复/项目切换场景 4 项已在前一次运行通过，最后汇总以下述扩大回归为准 |

本轮初次迁移反例和早期编译/测试失败日志保留在
`artifacts/project-authorization-revocation-20261021/`。队列界面测试现在等待异步刷新完成后
检查消息，持久队列仍必须存在；HTTP 测试服务器只在已取消的 dispose 阶段接受连接关闭
异常。没有删除拒绝、审计或取消断言以获得通过。

##### 失败记录与修正

首轮 `accepted-Debug-terminal.json` 保存 3,139 项扩大回归：3,135 通过、4 失败，
没有跳过/中止；2,639 项源码/配置/脚本输入前后无差异。该次失败记录不追认为通过：

- 恢复工作树在 revoked 情况仍预期旧 `workspace_denied`。现在工具断言区分授权版本
  失效；随后复验进一步发现终端将新错误码显示为通用失败，已补专用本地化提示。
- 两项 Queue 分派竞争用旧版本回写授权来恢复项目。测试改为读取当前版本后明确
  恢复目录；旧保存拒绝和分派拒绝断言继续保留。
- 空引导边界仍预期添加目录后旧运行完成。现在任何授权目录变化都撤销旧运行；
  测试明确断言 Failed/reason 17、草稿保留及 receipt 不应用，生产边界同样严格校验。

后续 12 项复验有 11 项通过、1 项终端提示失败，保留于
`test-app-reviewed-regressions.log`。收尾另增加两项真实 SQLite 的保存后重读竞态反例，
旧实现两项均失败（新建返回外部 revision 2，修改返回外部 revision 3）；日志为
`save-race-before.log`。返回事务快照和终端提示修复后，相关 14 项全部通过，记录于
`review-fixes.log`，包含两条保存竞态、完整恢复工作树、Queue 竞争及引导目录变更。

##### 当前候选回归

`reviewed-*-terminal.json` 的两配置回归已终结，各 2,506 项：Debug 全部通过，Release
2,504 通过、2 项统计生命周期测试失败；两者均无跳过/中止，2,641 项输入前后无差异。
失败快照包含一次写入、已取消的旧统计和已开始的当前统计；旧测试却要求快照中仅
一条统计。生产统计运行会先 await 旧任务结束，再启动新任务，这不等于重复写入。

保存原测试到 `statistics-lifetime-original.cs.txt` 后，测试先在第四次请求处等待两条
统计审计均出现，原断言由此确定性两项失败，记录于 `statistics-deterministic-before.log`。
修正只涉及 `WorkspaceFollowupStatisticsLifetimeTests.cs`：按 ID、call ID、工具名、
task、风险/策略、旧结果及退出/开始先后严格核实恰好三条审计。最终还必须证明当前
统计成功，或注入的当前失败能被原断言捕获；未知错误、当前取消和未完成审计仍拒绝。
两配置各 9 项专项通过（统计生命周期、Queue 工具边界、保存竞态），记录于
`statistics-fixed-Debug.log` / `statistics-fixed-Release.log`。

最后只重建全解决方案并重跑受该测试修正影响的 App 相同 310 项选择。
`verify-final-app.ps1` 强制核对相较 reviewed 输入只改了上述测试文件，其他生产源码
及五套完整基础测试均未变；五套共 2,196 项/配置沿用已完成报告，不重复无关测试。
最后 App 复验两配置均 310/310 通过，无失败/跳过/中止。文档修订不包含在源码 hash 清单中。

| 套件 | Debug | Release | 证据来源 |
| --- | --- | --- | --- |
| Tools 完整套件 | 695/695 | 695/695 | `reviewed-*-Tools-results` |
| Platform.Windows 完整套件 | 412/412 | 412/412 | `reviewed-*-Platform.Windows-results` |
| Infrastructure 完整套件 | 814/814 | 814/814 | `reviewed-*-Infrastructure-results` |
| Browser 完整套件 | 113/113 | 113/113 | `reviewed-*-Browser-results` |
| Agent 完整套件 | 162/162 | 162/162 | `reviewed-*-Agent-results` |
| App 相关选择 | 310/310 | 310/310 | `final-*-App-results` |
| 每配置分层相关回归合计 | 2,506/2,506 | 2,506/2,506 | 前五套源码未变；App 为修正后重跑 |

两配置最后完整解决方案构建均零警告/错误，见 `final-*-build.log`；终态为
`final-Debug-terminal.json` / `final-Release-terminal.json`。各配置 2,641 项输入前后
路径/hash 无差异，配置间相同，清单为 `final-*-inputs-before/after.json` 和
`final-inputs-between-configurations.json`。这不是全解决方案测试，也不是 5,012 个
不同场景；没有以最后的通过状态覆盖 `accepted` 或 `reviewed` 的失败记录。

最后 Roslyn 结构审查覆盖 2,448 个手写 C# 文件、21,749 个方法/相关语法节点：
每文件恰好一个类型，最大文件 496 行、最大方法 78 行，无违规；记录于
`structure-audit-reviewed.json`，统计测试修正前的审查另存
`structure-audit-before-statistics.json`。`git diff --check` 通过。

以上只关闭项目授权版本与合作撤销这一切片的本轮实施/相关回归；当前候选没有完成
原生窗口、真实提供商、全部资源撤权组合或崩溃实机验收。产品总目标及下述 P0 仍未完成。

#### 仍未完成与下一步

后续 schema 27 已推进下述第 1 项及登记闭环的主根/附加根范围，详见
[ROOT_OBJECT_AUTHORIZATION_ACCEPTANCE.md](SECURITY.md#root-object-authorization-acceptance)。以下保留 schema 26 检查点的原始缺口和方案；
工作树自身身份、严格排空、崩溃恢复和 MXC 仍未完成，不将旧结果算作新版本验证。

1. **持久根对象身份（P0）**：当前版本关联配置字符串，不关联卷和文件对象 ID。
   在 Platform.Windows 增加 no-follow 根句柄身份读取端口，向前迁移保存卷/对象身份和
   显式重新授权代际；首次升级/身份变化必须明确登记，不能自动把同一路径的新对象当
   原授权。覆盖替换根目录、junction/挂载/重命名、大小写及不同卷，普通项目/工作树/
   附加根分别验收。删除项目后复用 GUID 的生命周期也需要永久代际或 tombstone。
2. **严格撤销屏障（P0）**：本实现是撤销通知和合作取消，不保证 Save 返回时所有进程
   已排空、同步原生调用已终止，也不能使最后一次检查和副作用成为原子事务。需要按
   资源类型登记任务所属操作、关闭接受入口、等待有界 drain 并显式报告未确认资源；
   对不可取消原生 I/O 评估独立 worker。不得自动重做或声称无副作用。
3. **文件最终名称与崩溃恢复（P0/P1）**：继续处理原子替换最后名称竞争、目录移动
   子树的一致性、产物发布/引用事务和崩溃遗留回收；增加提交前后实际终止和恢复反例，
   对未知效果保留人工核实状态，不根据本轮租约成功推断其已解决。
4. **跨实例与规模（P1）**：目前每个租约轮询，不是跨进程即时 IPC。增加共享只读
   版本缓存/订阅聚合及有界调度时仍保留执行边界复核、失败保守拒绝；验证大量并发租约、
   数据库长锁、通知乱序及关闭。500 ms/5 秒是检查周期与等待上限，不是进程退出 SLA。
5. **MXC（R01）**：按用户选定原生 MXC 在受支持的独立 Windows 环境验证。环境尚未
   就绪；本机没有系统变更，不把授权撤销等同命令文件系统隔离。具体环境、最小预置、
   全入口负例和恢复计划继续见 [WINDOWS_ISOLATION_BACKEND_DECISION.md](SECURITY.md#windows-isolation-backend-decision)。
6. **原生与真实服务验收（P1）**：仍需当前候选的原生窗口/系统目录选择器、真实
   provider、实际浏览器/MCP/子代理与定时运行中撤权组合及宿主强制终止恢复。已有本轮
   Headless、合成 provider 和真实终端证据各自保留，不互相替代。

##### 后续本地修复的具体闭环

以下是待实施方案，不代表已经添加端口、迁移或放开现有兼容限制。

1. **先确定根身份的数据和登记流程。** Domain 单独表达授权根 ID、规范路径、卷身份、
   文件对象 ID、代际和确认状态；Application 定义读取/核实端口，Windows 用 no-follow
   句柄读取实际根对象身份。Infrastructure 新建向前迁移，保存授权根与项目版本的绑定。
   旧数据不得静默绑定升级时同路径的新对象；先明确迁移后的待确认状态，再由用户
   显式确认目录。项目主根、附加根和任务工作树分别建模，删除后的 ID 不重新生效。
2. **贯通确认到操作。** 项目编辑器通过 Application 协调明确授权和保存，事务复核
   期望版本并一次提交身份快照；Agent、工具和终端获取租约时验证同一快照。Windows
   文件会话将授权链绑定到该根身份，身份不符时拒绝而非重新采用路径。运行中的根
   句柄所有权、目录重命名与工作树清理依赖一起审查，不用 UI 字符串比较替代原生校验。
3. **先做替换反例再验收。** 在登记前后、审批前后、会话打开和提交边界插入目录
   替换、junction、挂载、大小写及跨卷变化；覆盖多根中仅一根变化、同路径 ABA、
   同项目并发与另一项目继续、旧数据库升级/回滚和手动重新授权。断言根外无新增
   读写，旧任务不自动续跑，现有用户数据与未发生冲突的草稿保留。最后做独立原生
   Windows 实例中的实际目录选择、重开及恢复检查；这一闭环无需等待 MXC 环境。
4. **随后建立撤销资源登记与排空。** Application 用实例所属资源登记端口记录
   task/授权代际/资源 owner 和开始、停止、退出状态；撤销先拒绝新操作，再取消并
   等待所属 Agent、工具、进程和浏览器返回。持久授权提交与资源排空是两个状态，UI
   需明确显示未确认/清理失败，不用“保存成功”暗示所有副作用已停止。跨实例使用
   持久撤销状态和确认记录，超时仍保留未知结果。不可取消原生调用迁入独立 worker
   前先验证对象句柄传递及 owner 终止；不终止用户或其他任务的资源。
5. **恢复与隔离分别验收。** 在暂存、文件提交、产物保留、审计完成和资源排空
   边界实际终止独立宿主，重开只能显示已知结果/未知效果和人工继续；不能按日志
   自动补做写入。MXC 环境就绪后另接独立命令/ConPTY 后端，强制验证真正原生隔离、
   多根权限、网络/进程树和根外读写拒绝；不将本地文件会话或合作取消作为替代验收。
<!-- /source: PROJECT_AUTHORIZATION_REVOCATION_ACCEPTANCE.md -->


---

<a id="project-authorization-ui-acceptance"></a>

## PROJECT_AUTHORIZATION_UI_ACCEPTANCE.md

<!-- source: PROJECT_AUTHORIZATION_UI_ACCEPTANCE.md -->
### 项目授权与紧凑侧栏交互验收

#### 范围与复现

2026-10-16，沿用 ui-ux-pro-max 的可见焦点、弹层上下文和 Avalonia Flyout 建议，
在独立 Headless/Skia 中运行生产 MainWindow、组合根、SQLite 和真实文件工具。
目录选择器仅替换端口返回值，模型仅使用 loopback SSE 服务；没有操作、重启或截取
用户当前的 Codex/MossAgent 窗口，没有真实付费模型请求。

- 526×601 小窗口中，审批卡片覆盖侧栏的项目行，输入命中卡片而不是项目按钮。
- 项目行沿用普通导航的“点击后收起抽屉”样式，导致刚打开的项目摘要立即关闭，
  无法通过摘要进入编辑。1280×820 固定侧栏不复现该问题。
- 最初的测试夹具先检查命中、后移动焦点，误报正常的悬停/焦点动作按钮；
  已改为先键盘聚焦并刷新，再核对实际输入根中的命中。未改变正常生产按钮样式。
- 拒绝路径的审计明确保存 ApprovalGranted=false，不是 null；完整审计也包含既有
  只读文件统计/检查调用。测试保留这些审计，分别核对写入记录，不删除生产行为。

#### 设计与安全复核

1. 改动归 App 的共享项目行和 MainWindow 展示绑定，模块依赖方向不变。
2. 复用 AppNavigationPanes、AppFlyout、ApprovalViewModel 和既有工具/存储端口。
3. 抽屉展开时仅暂时隐藏审批卡片，不改变待审批请求、队列、取消或浏览器呈现阻断。
   收起抽屉后恢复当前请求；没有自动同意、切换审批策略或扩大工具授权。
4. 不新增服务定位器、业务类型或大文件；新测试按服务、夹具、输入和场景分开，
   每份手写 C# 恰好一个类型。发布、JIT 和 JSON 策略不变。
5. 覆盖多目录写入、目录撤销、同任务续跑、跨项目拒绝、运行快照不扩权、后续新任务
   获得新增授权，以及恢复时不重放、隐藏期间取消和旧审批身份拒绝。

#### 实现

- 工作区或设置的紧凑导航抽屉打开时，审批展示让位给导航；关闭后恢复同一待处理队列。
- 带项目摘要的共享项目行不触发抽屉的导航关闭。没有摘要时保持原导航收起行为；
  动态移除摘要后恢复原行为，展开项目子任务的独立按钮不变。
- 没有改动文件授权判定、工具执行管线、项目持久化或任务快照策略。

#### 交互证据

- 明暗主题，526×601 和 1280×820。实际输入项目名称、调用目录选择端口、设定主要目录、
  保存并核对 SQLite；主目录之外的第二个授权目录也能经真实审批完成原子写入。
- 在 UI 中移除目录后，同一任务再次运行被 path_denied 拒绝；重建服务恢复历史不重放
  模型调用，原有审计逐项保留；仅允许页面正常发起的只读文件统计新增审计，
  不把它们误判为写入重放或抑制既有行为。目录撤销后只读统计也必须 path_denied，
  保留授权时统计才成功。等待统计完成后取审计快照，避免把正常异步查询误判为丢失。
  切换另一项目后也不能访问原项目路径。
- 已有写入等待审批时通过 UI 新增目录，不扩大已运行任务的权限快照；原写入仍可审批，
  新路径调用拒绝。后续显式新任务取得新目录授权，只有审批后才修改目标文件。
- 工作区/设置两种抽屉内，隐藏期间的请求取消会推进队列；旧请求身份不能同意新请求。
  关闭抽屉后能用真实键盘操作拒绝当前请求。未发出模型请求。
- 独立目录中的文件哨兵、请求数量、任务身份、审计结果与可见消息同步核验。

原始失败/成功 TRX、PNG、构建和检查日志位于
`%TEMP%/MossAgent-project-auth-qa-20261016`。隔离输出在
`artifacts/project-auth-20261016`；不覆盖默认调试目录，不暂存、提交或推送。

#### 验证过程记录

- 初次扩展回归中止时记录为 1,162 通过、2 失败，不计为成功回归。两项分别为
  需要同步的新展示绑定结构断言，以及误把恢复时正常新增的只读统计审计当作重放。
- 外层 testhost 的空闲状态不能证明 xUnit 独立子进程卡死。后续构建遇到残留测试
  子进程锁定测试 EXE，核对其独立输出路径和身份后清理；没有终止用户的桌面实例。
- 进一步专项发现统计查询尚未完成或已因目录撤销而正常被拒绝的情况。测试现在
  等待实际统计结束并分别核对成功/拒绝，不删除审计或改变生产授权逻辑。
- 最终专项 28/28 通过、无跳过，覆盖两个主题、两种窗口和上述恢复/新任务链路。
  40 份 Headless PNG 保存于 `final-verified`；单类型/规模审计、scoped whitespace 和
  diff 检查通过。专项属于下述扩展 App 回归的子集，不重复累计覆盖数量。
- 两次附带 Blame hang 诊断的扩展回归分别为 1,718/1,720 和 1,725/1,726，
  原始失败记录保留。前者还包含已同步的旧审批展示结构断言；两次均遇到同一个
  侧栏 pinned/noMatches 零分配断言，实际分别为 3,848 和 5,136 字节。
- 未修改侧栏筛选生产逻辑或放宽零分配断言。旧候选和当前候选各自独立复测三轮，
  每轮六项均通过；最终移除额外 Blame 采集器后的普通扩展回归也完整通过。
  这些结果显示运行条件差异，不足以证明采集器是分配波动的根因。

#### 最终验证

| 范围 | 报告 | 结果 |
| --- | --- | --- |
| 扩展 App 项目/导航/审批/侧栏/共享组件回归 | `app-standard.trx` | 1,726/1,726 |
| 完整 Visual 程序集 | `pixels-final.trx` | 300/300 |
| 完整 Agent 程序集 | `agent-final.trx` | 123/123 |
| 完整 Tools 程序集 | `tools-final.trx` | 494/494 |

四份最终报告均完整执行且无失败、跳过或中止；不是完整 App 或八程序集回归。
解决方案构建为零警告、零错误，scoped whitespace 报告为空；八份本轮 C# 的
单类型/规模检查通过，最大文件 444 行、最大方法 67 行。

`results-final.json` 核对上述报告、构建、源码检查和 40 份专项 Headless PNG。
生产、App.Tests 和 Visual.Tests 使用的 App DLL 哈希相同，并与专项截图候选一致：
`059A5CDD19F1854D45C88ABEE7220208FDB88B1357C21F5AC7D74A62999AC812`。
此前失败报告仍保留，不用独立复测替换历史失败结果。

#### 验证边界

Headless/Skia 不等于原生 Windows 目录选择器、真实窗口输入、DWM/高 DPI 或当前 Codex
同状态逐像素对比验收。此切片也不代表完整 App/八程序集回归或整体产品目标完成。
<!-- /source: PROJECT_AUTHORIZATION_UI_ACCEPTANCE.md -->


---

<a id="read-handle-path-safety-acceptance"></a>

## READ_HANDLE_PATH_SAFETY_ACCEPTANCE.md

<!-- source: READ_HANDLE_PATH_SAFETY_ACCEPTANCE.md -->
### 文件读取句柄的路径安全增量

日期：2026-10-20。范围：R02 的打开后读取句柄校验。整个产品目标未完成，
R01 命令文件系统隔离仍阻断，R02 也未关闭。原始证据位于
`artifacts/read-handle-safety-20261020/`，不纳入源码提交。

#### 问题与实际运行链路

打开前及读取后的字符串路径检查不能绑定中间已经打开的文件。例如授权目录内
的 junction 在打开时指向根外文件，随后恢复为根内目标；字符串复核通过，旧句柄
仍可读取根外内容。本轮测试使用临时目录、两个不同标记及真实 Windows junction
可控复现这一点，没有操作用户目录或修改系统 ACL。

App 注册 `IFileReadHandleValidator -> WindowsFileReadHandleValidator`，生产
`IToolExecutor -> ToolExecutor` 注入该守卫，其优先级高于调用上下文的可选守卫。
工具仍经过原有 schema、能力/路径、审批、超时/取消、输出限制及审计。
`FileSnapshotReader.Open` 打开流后先校验，失败立即关闭流；验证前不读取内容、
计算哈希或记录文件版本。校验只读取句柄及目录元数据，不修改 ACL。

守卫读取规范化 NT 最终路径，将卷身份纳入比较，并按目录分隔符边界判断归属。
授权根通过目录句柄确认长 DOS 名称，没有匹配的真实根时拒绝；不会把被替换的根
自动解释为其新的链接目标。根目录句柄在验证期间不共享删除，验证结束即释放。
最终路径使用 Ordinal 比较；输入根的大小写由真实目录句柄归一化。
原生查询失败、无授权根、失效句柄和路径无法证明时拒绝，用户错误使用资源文本，
不输出根外路径或原始 Win32 错误。

`CapturedToolRoots` 保存调用时的根快照，并在审批后、执行前及列表访问时检测原列表
变化；检测到清空、替换或追加即拒绝，不采用新权限。它修复初版简单复制导致无法
观察审批撤权的回归。该检测不能识别两次检查之间变化后恢复的 ABA，也不是持久化
授权版本或即时取消机制，后两者仍列为后续工作。

实际接入：

- `FileReadTool`、`FilePreviewTool`：读取/哈希/预览产物创建之前。
- `TextReplaceTool`、编辑捕获及 `AtomicTextFileWriter.VerifyAsync`：读取内容或版本之前。
- `FileChangeSnapshotStorage`：复制变更前/后快照源内容之前。
- `FileChangeSnapshotReader`：读取可信任务快照之前，仅使用该任务产物目录作为窄授权根；
  原有归属、名称、长度、链接及哈希校验保留，该派生上下文不传入其他工具。
- `PathCopyTool`：文件及递归目录复制的源文件读取之前；单文件源拒绝时不创建目标。

#### 设计自查

1. Tools.Abstractions 定义只读句柄守卫端口；Tools.BuiltIn 负责接入，
   Windows API 和实现只在 Platform.Windows，App 仅作组合，模块依赖不变。
2. 复用既有工具契约及 FileSnapshotReader，无新旁路执行器。兼容无上下文的底层
   helper 调用不提供此保证；产品保证依赖生产组合，已有专项测试检验接线。
3. 守卫位于审批后的工具内部；拒绝、异常、取消结果仍归原工具执行器处理、限额和审计。
   同步文件/目录打开及 Win32 查询没有可强制中断接口，不将取消测试解释为所有原生
   调用期间都能即时中止，尤其远程文件系统尚未验证。
4. 原生查询、验证、资源及测试辅助类型分文件；遵守单类型、500 行文件/80 行方法限制。
5. 测试同时检查合法读取和根外拒绝、关闭句柄、无结果/版本/快照泄露、原文件不变，
   并覆盖生产 DI、审批/撤权、取消、超时、并发、输出限制及持久化审计。

#### 证据状态

- 初次 `baseline.log` 是测试 API 使用错误导致的编译失败，不计为产品红测。
- 修正测试后 `guard-red.log` / `TestResults/guard-red.trx`：6 项中 5 项失败、1 项通过，
  证明修改前读取链路没有调用守卫；修复后的管线专项见各 green/final 日志。
- `native-handles.log`：真实本机 Win32 句柄和临时 junction 的确定性检查；
  不属于 Avalonia 原生 GUI 验收。后续全量包含加入不同标记后的增强复现。
- `app-composition.log`：生产 App DI/Agent/Providers/SQLite，加本地模拟模型 HTTP；
  验证读取、写入快照、上下文无法替换守卫和审计持久化。没有真实提供商调用，
  没有操作已有窗口，不能称为原生实机交互验收。
- 首次尝试完整 Debug 回归发现 3 项授权撤权回归及 1 项进程测试超时；未结束的 App
  测试实例在修复前主动停止，已收集 3,300 项 App 通过结果不代表其全套通过。
  `full-Debug.log`、`full-Debug-terminal.json` 和 `initial-*-stop.json` 保留失败及中止记录；
  这一轮不是成功的完整回归。输入和运行程序集前后哈希无变化。
- 单独重跑 `ShellProcessOwnershipTests` 三项通过（`process-isolated.log`），首轮超时
  原因尚未证明，不称为已定位修复。授权回归修正后，工具/原有路径预检专项 28 项、
  原生句柄/原有终端边界专项 26 项通过（`guard-roots-fixed.log`、`native-roots-fixed.log`）。

最终相关回归使用 `verify-related.ps1`，完整构建解决方案后依次运行工具全套、
Windows 全套及明确过滤的 App 工作流集合，不称为完整解决方案测试：

| 配置 | 完整构建 | Tools 全套 | Windows 全套 | App 相关工作流 | 输入一致性 |
| --- | --- | --- | --- | --- | --- |
| Debug | 零警告/错误 | 691 通过 | 158 通过 | 336 通过 | 2,471 项输入前后无变化 |
| Release | 零警告/错误 | 691 通过 | 158 通过 | 336 通过 | 2,471 项输入前后无变化 |

`related-Debug-terminal.json`、`related-Release-terminal.json` 保存进程终态和逐程序集
TRX 统计。每配置合计 1,185 项相关测试通过，不包含首轮已中止的全量结果。
App 过滤条件保存在
验证脚本及 `app-test-inventory.log`。该集合覆盖生产 DI、文件编辑/快照/预览/撤销、
审批、多根/工作树授权和相关并发恢复；模拟服务、Headless 和真实本地资源证据
各有边界，不替代本版本的完整原生 GUI 或真实外部服务验收。

结构检查：2,278 个 C# 文件、20,659 个方法/访问器/lambda，最大文件 496 行、
最大方法 78 行，单文件单类型及行数限制零违规（`structure-audit.json`）。

#### 尚未完成及下一步

下列是读取检查点的剩余工作。后续原子文本写入已推进目录句柄租约和相对提交，
范围、证据及仍未关闭的竞态以 [WRITE_HANDLE_PATH_SAFETY_ACCEPTANCE.md](SECURITY.md#write-handle-path-safety-acceptance) 为准；
其他入口及完整 R02 不因此视为完成。MXC 独立环境尚未就绪，用户已选择先继续本地修复。

1. 本增量只验证句柄在校验时的路径。写入目标/父目录、目录枚举、重命名/删除、
   快照和产物的目标写入、Git 元数据仍需目录/文件身份及原子操作层面的方案。
   文件打开前的元数据访问、设备打开风险，以及验证后外部移动或并发修改的全部
   时序都没有由该守卫封闭。
2. 运行中即时撤权仍需 Application 授权代际、取消与工具/进程资源租约；不能把
   路径检查或列表变化检测称为撤权回滚。已发生副作用保留未知状态，不自动重做。
3. 8.3/其他无法对应长 DOS 名称的根别名保守拒绝，不自动扩大授权。UNC/SMB、
   硬链接、卷挂载变化和区分大小写目录的完整实机矩阵仍需单独验收；不得将
   本地 NTFS 大小写输入测试等同为这些场景的通过。
4. `rg`、Shell、ConPTY、Git 子进程、stdio MCP 和其他独立 I/O 入口不受此守卫隔离。
   按用户选定的原生 MXC 路线，在支持它的独立 Windows 环境实施并验证 R01；
   本机只读复检仍为 AppContainerDacl，没有执行 SDK 建议的系统预置命令。
5. 后续先为写入/父目录替换建立临时目录门控复现，再确定原生句柄租约与原子替换
   接口；然后贯通授权代际及管线审计。跨进程崩溃、原生 UI 和正式单文件包验收
   仍按总清单推进，本轮不声明发布就绪。

#### 后续 R02 的具体交付顺序

1. **写入与目录身份。** 先在现有 AtomicTextFileWriter 的分阶段写入测试中，分别于
   路径检查后、临时文件创建前、最终替换前切换目标/父目录 junction，使用根外临时
   标记记录读写和哈希变化。Tools.Abstractions 定义文件及父目录租约/身份端口；
   Platform.Windows 持有原生句柄，验证最终卷/目录身份，验证期间防止被替换。
   Tools.BuiltIn 通过该端口创建临时文件和提交，不能用一次额外字符串 Resolve
   冒充竞态修复。具体原生替换 API 须先在临时目录证明“相对于已验证目录”或等价
   原子约束，不支持的卷/权限/重解析点应明确拒绝，不回退到未验证路径写入。
2. **保留编辑冲突及快照。** 将现有 SHA-256 版本检查与文件身份同时绑定，区分
   “原文件不存在”和“打开/确认失败”；保留外部修改。临时写入、变更快照及预览
   产物分别持有其所属目录范围。目标提交失败清理本次临时文件，保留可核实的
   副作用和审计；跨卷、文件锁、取消、进程退出及已有产物不得被误报为成功。
3. **持久授权代际。** Application 定义可比授权版本及任务运行租约，Infrastructure
   以向前迁移保存项目/目录授权代际，旧数据保留。审批绑定捕获的版本；审批后、
   启动工具/进程前及异步 I/O 返回后复核。撤权取消匹配任务资源，拒绝下一步工具，
   保留其他任务和草稿。运行中的副作用无法保证回滚时记录未知结果并要求手动核实；
   重启恢复仍是 Interrupted，不自动执行迁移前或崩溃前未完成的操作。
4. **逐入口审计和验收。** 同一套门控负例覆盖读/写/复制/移动/删除、目录、快照、
   Git 元数据、子代理及辅助 UI 查询；代码执行交由 R01 的 OS 隔离，不能靠文件
   helper 管住子进程。每个入口证明根内成功、根外拒绝、撤权/审批竞争、取消/超时、
   两任务独立和恢复无重放。先跑专项及完整构建，再对稳定候选做全套 Debug/Release，
   最后独立 Windows 原生交互及 JIT 单文件验收；这些步骤未执行前不关闭总清单。
<!-- /source: READ_HANDLE_PATH_SAFETY_ACCEPTANCE.md -->


---

<a id="root-object-authorization-acceptance"></a>

## ROOT_OBJECT_AUTHORIZATION_ACCEPTANCE.md

<!-- source: ROOT_OBJECT_AUTHORIZATION_ACCEPTANCE.md -->
### 授权根目录对象身份

#### 设计与阶段计划

1. Domain 保存不可变的卷/文件对象身份和每次明确授权的 grant ID；Tools.Abstractions
   定义持有根句柄的会话。Windows 复用 no-follow NTFS 目录链，以真实句柄读取身份。
2. schema 27 新增独立授权对象表，不改写项目、消息或旧目录；旧项目无记录时保持
   待确认。新建/明确新增目录登记身份，保留目录只验证旧身份，改名称不重新授权。
3. 重新授权使用独立的 Application 端口及项目编辑器明确动作，以当前版本为条件，
   递增版本并撤销旧运行。保存、发布通知和返回快照仍在原事务链路内。
4. 生产 monitor 强制核对持久快照与目录对象；每个任务/工具/终端租约持有独立根
   句柄至退出，避免验证后根被重命名替换。无项目聊天不获得新访问能力。
5. 先做原生替换/链接/取消负例，再做旧数据迁移/回滚、明确登记及并发冲突，最后
   验证设置→真实工具/终端→拒绝→手动重新授权→新运行的完整链路。

#### 工程自查

- Native API 只在 Platform.Windows；Infrastructure 通过 Tools.Abstractions 读取身份，
  Application 编排运行校验，App 只消费端口和显示本地化状态。
- 复用标准工具审批、租约取消和超时；身份会话只读元数据，不扫描额外目录，不
  新增工具旁路。登记 I/O 在事务外且离开 UI 线程，事务内复核期望版本，提交前
  仍持有原生句柄。失败不回退路径授权，不自动重放副作用。
- 每种 DTO/端口/实现单独成文件；保持文件 500 行、方法 80 行硬限制。
- 测试使用临时 SQLite/目录和独立进程。安全负例包括对象替换、链接、未知旧授权、
  陈旧保存、取消、并发项目及登记失败不留下半授权。

#### 当前证据

日期：2026-10-21，分支 `feature/initial-mossagent`。本切片在现有未提交的文件安全与
项目撤销增量上继续实现；没有暂存、提交、推送或发布。以下只描述明确范围，不将
旧检查点的全量结果视为本候选验证。检查点与修复后验证分开记录，不累加重复用例。

| 检查点 | 实际结果 | 范围与证据 |
| --- | --- | --- |
| R2 Debug 扩大回归 | 2,912/2,912 通过；完整解决方案构建零警告/错误 | Tools 695、Windows 421、Infrastructure 825、Browser 113、Agent 162、Visual 300 六套完整测试，以及 App 精选 396；不是全解决方案测试 |
| R2 Release 扩大回归 | 2,911 通过、1 失败；完整构建零警告/错误，无跳过或中止 | 唯一失败是启动中撤权的 PTY 状态期望 Stopped、实际 Failed；其他六份 TRX 为 Completed，App 396 项通过。不能把此检查点标为全部通过 |
| R2 输入与报告核对 | 每配置前后及配置间 2,668 项路径/hash 一致 | 14 份 TRX 逐项核对执行数、结果和异常计数，失败仅为上述指定用例；`reviewed-r2-independent-audit.json` 明确保存 `AllTestsPassed=false` |
| PTY 受控反例与定向修复 | 修复前 1 项失败；修复后终端相关 14 项通过 | 真实授权租约已撤销且原令牌已取消，暂停回调使 linked startup token 尚未收到通知；迟到连接清理后仍复现 Failed。分类增加租约撤销标记；真实启动/清理失败检查保留 |
| R3 Debug 修复后扩大范围 | 完整构建零警告/错误；Windows 422 项通过，App 286 通过、5 失败，输入无漂移 | 额外纳入的取消测试使用保存前无 grant 的项目对象，被运行入口正确拒绝。只修测试准备为读取真实持久快照并断言有授权；原来的 5 项行为断言保留，定向复验全部通过。未启动 R3 Release |
| R4 修复后当前候选 | Debug/Release 各 713/713 通过；完整解决方案构建均零警告/错误 | 每配置完整 Windows 422、App 精选 291；含授权/根对象/统计/终端及运行取消流程，四份 TRX 均 Completed，无失败/跳过/中止。不冒充再次执行 R2 的全部套件 |
| R4 当前输入及报告复核 | 两配置前后及当前 2,670 项路径/hash 一致 | `reviewed-r4-independent-audit.json` 逐项核对结果、计数及构建；相对 R2 只有 PTY 分类文件、两份新测试文件和取消测试准备共四条路径变化 |
| R4 结构检查 | 2,477 个 C# 文件、21,904 个方法/访问器/lambda，零违规 | 最大文件 497 行、最大方法 78 行；`structure-audit-reviewed-r4.json`。每个手写 C# 文件恰好一个类型 |
| R4 Headless 视觉检查 | 每配置保存 12 张根对象流程 PNG | 自动检查明暗 × 526×601/1280×820。人工查看 Debug 深色窄窗/浅色宽窗、Release 浅色窄窗/深色宽窗的确认前后截图，完整路径、提示及按钮可读，草稿保留；不代表原生桌面交互 |

R2 终态、原始 TRX、构建日志及截图位于
`artifacts/root-object-authorization-20261021/reviewed-r2-*`。受控 PTY 反例与修复验证分别为
`pty-state-before-fix*`、`pty-state-after-fix*`。没有修改失败报告或放宽状态断言。
修复后的最终记录为 `reviewed-r4-*`，R3 的失败记录与未启动 Release 的事实均保留。
本轮没有再次执行完整解决方案测试，也未运行正式单文件发布、真实提供商或原生 GUI 验收。

#### 已贯通的入口与运行行为

- **登记与保留身份：** 新建项目、明确新增授权目录通过
  `WorkspaceProjectService` → `IProjectAuthorizationWriter` → `SqliteProjectAuthorization`
  → `ProjectRootEnrollment` 打开目录链。实际句柄读取卷 GUID/序列号和 128 位 FileId；
  新根生成 grant ID，保留根核对原身份且保留原 grant。改名不会接受替换对象。
- **升级和重新授权：** schema 27 增加独立 `project_root_authorizations` 表，以项目 ID
  和授权版本绑定有界 JSON；不改变既有目录、历史或用户文件。旧项目没有记录时
  保持待确认。侧栏项目菜单 → 编辑项目 → “确认并重新授权这些目录”通过独立
  `IProjectAuthorizationConfirmation` 端口登记当前对象，事务复核期望版本、递增
  revision、提交新 grant，再通知旧租约。普通保存、启动和重开都不替代该明确动作。
- **可核对且不丢草稿：** 编辑器显示目录名与完整路径；目录草稿与已保存列表不一致时
  禁用重新授权，提示关闭/重开后先确认已保存目录。确认保留名称草稿、前台草稿和
  当前任务，项目快照原位更新。名称草稿只有另行保存才写入；确认本身不启动模型。
- **侧栏恢复：** 文件统计缓存同时核对授权 revision，路径没变的重新授权也会丢弃旧
  上下文并重新查询。生产 App 服务组合验证确认后统计、审阅和撤销均使用新授权，
  前台草稿保留，不重发模型请求。错误行尚在处理时不会接受重复重试。
- **真实执行拒绝：** 生产 DI 为仓库及 `ProjectAuthorizationMonitor` 注入同一个 Windows
  会话端口。任务/工具/长驻 PTY 获取租约时比较持久授权快照与实际对象；旧快照、
  缺失登记、同路径新对象或链接被拒绝。Agent 可在首个 provider 请求前失败；工具
  仍经过标准审批/取消/输出限制/终结审计。明确确认后旧上下文依然失效，只有手动
  新运行使用新快照。
- **持续根句柄：** 租约持有 no-follow NTFS 目录链，工具返回后长驻终端仍持有自己的
  链，释放前禁止链目录的重命名替换。身份探针使用虚构子名称定位父目录，不创建
  `.moss-root-identity` 文件。重新授权引发合作取消，所属清理结束后才释放根句柄。
- **撤销不依赖旧目录可访问：** 已存在项目可移除原主目录或撤销全部根；移除的目录
  不参与登记，空授权保存不执行原生目录 I/O，空 grant 不能用于运行。主目录字符串
  不是额外授权；UI 普通创建/编辑仍要求有效主目录。该行为保留撤销与拒绝边界，
  不要求为撤销先重新授权一个已丢失的目录。
- **竞态与取消：** 原生准备在事务外、UI 线程外执行，句柄持有至事务结束。其间另一个
  保存抢先修改 scope 时 CAS 拒绝迟到确认，不留下部分登记或错误通知。失败回滚
  项目版本和根记录。不合作 reader 取消后停止等待并观察迟到结果，成功迟到会话
  被释放；这不等于原生 I/O 已同步排空。
- **启动中撤权的终态：** 终端启动的失败分类同时核对租约撤销标记，异步取消通知尚未
  到达 linked token 时也保持 Stopped；仍等待迟到连接与租约清理，不发布 Running。
  这不改变清理失败的可见状态、真实启动错误分类、审批或命令隔离边界。

#### 本轮验证的层次

所有数据均为临时项目、SQLite 和文件，不访问用户日常目录或窗口；审批策略保持原值。

| 层次 | 对应检查与实际证明 |
| --- | --- |
| Windows 原生文件系统自动化 | `RootObjectIdentityTests`、`RootObjectIdentityBoundaryTests`：稳定 FileId、多根独立持有、活跃句柄阻止替换、释放后同路径新对象不同、失败/取消释放；根及祖先 junction、相对/设备路径、重复别名拒绝；点段规范化指向明确登记的对象 |
| SQLite/契约自动化 | `RootObjectAuthorization*Tests`：真实 26→27 迁移、故障回滚/重试、历史保留且不自动登记；手动确认、改名不重新授权、增加根保留旧 grant、移除主根/全部撤销、陈旧确认及并发保存拒绝；错误身份、空响应和迟到取消清理 |
| 生产 App 服务组合 | `RootObjectAuthorizationRuntimeTests`：真实 SQLite、Windows 文件与统一工具管线；替换对象内容不能泄露，改名保存拒绝，编辑器明确确认保留两种草稿，旧上下文继续拒绝、新上下文读取成功，全部工具审计终结；不发模型请求 |
| 真实 ConPTY | `RootObjectAuthorizationTerminalTests`：标准 `terminal.start` 返回后根仍不可移动；明确重新授权结束所属终端、释放句柄，旧上下文启动拒绝。原项目授权终端用例还验证父/子进程退出与迟到标记不存在 |
| Avalonia Headless + loopback 模型 | `RootObjectAuthorizationUiWorkflowTests`：Light/Dark × 526×601/1280×820；实际键盘创建项目、同路径替换、模型请求前拒绝、编辑器路径显示、目录草稿禁用/恢复确认、Space 确认、草稿/任务保留、AskEveryTime 审批后真实写入、服务重建后无重放且结果可查 |

Headless 不是原生桌面交互；本地 SSE 模型不是实际 AI 提供商。真实 ConPTY/NTFS/SQLite
也不证明完整产品、命令文件隔离或所有资源生命周期已经验收。

#### 保留的失败与修复记录

- 首次 App 专项 13 项中 1 项失败：测试保存另一个项目后仍选择不含 grant 的原始对象。
  改为重载真实已保存快照，未给旧对象自动补授权。见 `artifacts/root-object-first-app.log`。
- 首次根对象运行专项 4 项中 1 项暴露真实草稿丢失：项目更新调用选择器，清空当前
  任务。改为原位更新选中项目并通知环境；修复后同组 4 项通过。
- 原生首轮边界 9 项中 2 项为异常断言类型错误：no-follow 打开重解析对象后属性检查
  抛 `UnauthorizedAccessException`。改为断言实际拒绝类型，9 项通过，未放宽保护。
- UI 首轮 4 项中 3 项及扩大回归中 1 项在模拟替换前遇到真实 Git 状态进程的短暂
  工作目录共享锁。测试仅对 Win32 sharing violation 有界等待；活跃授权租约下的
  替换仍须立即拒绝，不能把所有 I/O 失败当作可忽略。
- 扩大 Debug 首轮完整构建通过，五套基础测试通过，App 379 项中 6 项失败：上项 1
  项共享锁；另 5 项暴露撤销空 scope 和移除原主根时的登记限制。实现允许撤销时
  不读取已移除对象，空授权没有执行能力。修复后 27 项相关回归通过，并新增专门的
  持久化撤销负例。首轮终态与输入无漂移记录保留在
  `artifacts/root-object-authorization-20261021/reviewed-Debug-terminal.json`。
- 第二轮扩大 Debug 完整构建和五套基础测试通过，App 379 项中 11 项失败且输入无
  漂移：1 项统计测试假定 HasError 发布时重试必已启用，10 项仍用原单行标题检查
  双行目录内容的整体对齐。增加可控第二行查询证明错误可在 busy 时出现，测试等待
  真正可重试状态且保留失败审计/恢复要求；布局断言改为校验完整文本组与路径。
  记录为 `reviewed-repair-Debug-terminal.json`，未将失败候选标作通过。
- 另新增真实 App/SQLite/文件回归，在修复前复现不变路径重新授权后没有新统计查询，
  见 `artifacts/root-object-statistics-before-runtime-fix.log`；统计缓存加入授权版本后，
  同一回归贯通统计、审阅与撤销。32 项窄回归中另 1 项新断言误将服务收窄输出/超时
  后的 context 副本当原实例；改为核对其携带的新快照 store。编译和失败日志均保留，
  以之后的正式回归为当前候选结果。
- R2 Release 的完整 Windows 套件 421 项中 1 项出现迟到启动撤权状态竞态；连接已释放，
  状态却为 Failed。新增真实租约的可控回调 gate，在未修改生产代码时稳定复现同样
  结果，修复后相关 14 项通过。该反例证明这条竞态存在；原 R2 失败没有逐指令轨迹，
  不以此声称捕获了当次全部时序，也不归因于负载。原始终态和两份窄回归 TRX 均保留。
- R3 Debug 的 App 选择扩展到名称含 Terminal 的运行取消流程；291 项中新增覆盖的 5 项
  仍把保存前的项目对象交给生产运行控制器。该对象没有新 grant，守卫在 runner 前拒绝，
  因而看不到预期的工具卡片/流式片段。准备逻辑改为读取 SQLite 保存快照并断言授权
  存在，5 项全部通过；没有把旧对象自动授权，也没有删除取消后的原始状态/持久化断言。
  证据见 `reviewed-r3-Debug-terminal.json` 和 `cancellation-fixture-repair*`。

#### 支持限制、未完成范围与下一步

以下为 schema 27 检查点的剩余范围。后续 schema 28 已实现工作树对象 grant、
旧任务明确确认和运行租约，具体证据及仍未闭合的创建/清理边界见
[WORKTREE_OBJECT_AUTHORIZATION_ACCEPTANCE.md](GIT_WORKTREES.md#worktree-object-authorization-acceptance)。

1. **工作树自身身份：** 本轮登记项目主根/附加根，既有工作树归属守卫继续执行；任务
   工作树目录没有自己的持久 grant。下一步由 Domain 建模 task/worktree 的卷/对象/
   grant，Infrastructure 向前迁移，创建成功认领时登记，旧工作树待明确确认；运行、
   工具和清理持有相应根链。在实际 Git 创建/恢复/清理边界覆盖替换、伪造元数据、
   两任务并发与取消，拒绝未经确认的新对象且不删除归属未知目录。
2. **撤销严格排空：** 本轮是合作取消和所属租约生命周期。Save 返回不等于所有资源
   已退出，最后一次校验与副作用不是原子事务。Application 增加任务/授权代际所属
   的资源登记、拒绝新入口、取消、等待有界 drain 和 cleanup-unknown 状态，UI 分别
   展示配置已提交与资源已退出；不可取消原生 I/O 先研究独立 worker 和句柄传递。
   覆盖提交后已有副作用、取消不合作、关闭及跨实例迟到操作，不自动重试。
3. **最终名称和崩溃恢复：** 目录链固定不能提供最终文件名 compare-and-swap。
   继续为写入/删除/移动的最后边界建立可复现 gate，研究原生可保证的条件提交；
   无法保证时明确失败，不采用路径回退。增加 intent、阶段与产物引用的向前迁移，
   在 staging/提交/retain/审计边界终止独立宿主；恢复只展示已知/未知状态并允许人工
   检查，不补做副作用或自动删除未知目录。
4. **平台与对象生命周期：** 当前仅支持本地普通 NTFS、不含 reparse 的大小写不敏感
   目录；UNC/设备路径和不支持卷保守拒绝。卷克隆/离线替换、FileId 重用、挂载变化、
   项目删除后复用 ID、超长路径、大小写敏感卷及大规模多根仍需专项。兼容宿主未注入
   identity factory 时不具备这条原生边界；不能据兼容单元测试声称生产安全完成。
5. **跨实例与性能：** 仍为每租约 500 ms 轮询与 5 秒校验期限。后续聚合只读订阅/缓存、
   持久失效与未确认退出记录，始终保留执行前复核；测试通知乱序、长数据库锁、
   多实例大量租约与关闭。周期及期限不承诺进程退出时延。
6. **MXC 与实机：** 用户已选择真正原生 MXC 的独立 Windows 环境，目前尚未就绪。
   环境就绪后按 [WINDOWS_ISOLATION_BACKEND_DECISION.md](SECURITY.md#windows-isolation-backend-decision) 验证实际隔离 tier、pwsh/
   SDK/Git/ConPTY、多根/多任务互拒、子进程、继承句柄、超时和恢复；本地不升级系统、
   不复用已消费的管理员诊断许可。当前候选的原生目录选择/重开、真实 provider 以及
   browser/MCP/子代理/定时任务运行中重新授权组合仍待验收，不以 Headless 替代。

整个产品目标仍未完成；命令可访问任务授权根之外的文件仍是 R01 安全阻断。
<!-- /source: ROOT_OBJECT_AUTHORIZATION_ACCEPTANCE.md -->


---

<a id="windows-isolation-backend-decision"></a>

## WINDOWS_ISOLATION_BACKEND_DECISION.md

<!-- source: WINDOWS_ISOLATION_BACKEND_DECISION.md -->
### Windows command isolation: backend and host decision

Status: design/evidence checkpoint, 2026-10-19. Production commands remain
unconfined outside their authorized working directory. No SDK adapter has been
integrated, and no platform requirement has been changed. The goal stays active.

#### Current host, measured without elevation

`artifacts/mxc-compatibility-20261019/host-capabilities.json` records:

- Windows 11 Pro 23H2, build **22631.3880**. Registry ProductName is not used to
  distinguish Windows 10 from Windows 11.
- Hyper-V and VirtualMachinePlatform enabled. Windows Containers and Windows
  Sandbox disabled, according to Win32_OptionalFeature; missing Sandbox binaries
  independently agree. Running vmcompute/hns alone does not establish either.
- Docker server 29.6.1 is running **Linux** containers despite its optional
  Windows service being stopped. Only docker-desktop is registered with WSL.
  Neither daemon nor distribution was started, stopped, switched or reconfigured.

No Windows feature, ACL, account, service, firewall, ownership, OS version or
existing application window was changed. Both previous one-time administrator
authorizations remain consumed. This ordinary-user research does not reuse them.

#### Public MXC integration is available, native isolation is not available here

Microsoft's [MXC announcement](https://blogs.windows.com/windowsdeveloper/2026/10/07/microsoft-execution-containers-policy-driven-containment-for-ai-agents/)
links a public .NET SDK. Reviewed upstream references are pinned to commit
`fda29ac63bc306b7f97f3b86b4b63d44f7f40cf2` in
`artifacts/mxc-compatibility-20261019/upstream-manifest.json`. This identifies
the reviewed source, not the build provenance of the separately published package.

The diagnostic pins **Microsoft.Mxc.Sdk 1.0.1**. `dotnet nuget verify --all`
passes its Microsoft author and NuGet repository signatures. The archive SHA256
is `D47B1F70C3FCAAED1705A0C3872807E2878FE8C1B778C0DD9EB9325BF421A672`.
The archive contains no build scripts or dependency packages. It was restored
only into a separate research project, not the solution or central package list.

The real .NET 10 JIT diagnostic calls only GetPlatformSupport,
GetAvailableBackends and the request-aware Probe. It refuses an enabled
administrator token, passes no inherited environment or filesystem grants, and
never calls Run, Spawn, PTY or a lifecycle operation. `probe-final.json` records:

| Fact | Result | Meaning |
| --- | --- | --- |
| SDK assembly | 1.0.1.0 | Actual packaged SDK loaded |
| Administrator enabled | false | Ordinary process, no new RunAs |
| PlatformSupport.IsSupported | true | Some SDK sandbox is available, not proof of strict confinement |
| Effective request tier | AppContainerDacl | Lower fallback, not BaseContainer |
| BaseContainerApiPresent | false | Native ProcessContainer unavailable |
| BFS compiled/present | false / false | No bind-filter alternative in this package/host |
| NeedsDaclAugmentation | true | SDK warns about system-drive and NUL preparation; neither was performed |
| Other reported backend | Hyperlight | Availability only; experimental Linux workloads do not satisfy Windows shell compatibility |
| Workload started / product isolation validated | false / false | No success, denial or containment claim |

A zero diagnostic exit means inspection completed. It does not mean a workload
was isolated. No SDK-suggested host-prep command was run. The earlier AppContainer
package-group exposure remains valid evidence against accepting plain DACL
fallback as a strict task-root allowlist.

Microsoft's pinned [OS support table](https://github.com/microsoft/mxc/blob/fda29ac63bc306b7f97f3b86b4b63d44f7f40cf2/docs/backends/process-container/os-version-support.md)
and [official OpenAI Windows sandbox guidance](https://learn.chatgpt.com/docs/windows/windows-sandbox)
agree on native ProcessContainer minimums: Windows 11 **24H2 26100.9278** or
**25H2 26200.9278**. Newer release families have separate listed minimums; do not
apply a naive build-number comparison. Runtime capabilities and launch validation
remain authoritative on supported hosts.

#### Backend comparison and proposed direction

| Backend | Current-host state | Fit and remaining dependency |
| --- | --- | --- |
| Native MXC ProcessContainer | Unavailable; DACL fallback only | Preferred candidate for Windows tool/path semantics. Requires a supported test host, strict per-request tier enforcement, runtime dependency policy and native acceptance. |
| Windows Sandbox | Disabled | Microsoft documents one concurrent instance and no host-installed tools inside. It cannot directly implement independent concurrent task sandboxes. Do not enable it as the proposed complete fix. |
| Windows Hyper-V containers / separate Windows VMs | Hypervisor enabled; no Windows container runtime validated | Alternative if 23H2 must remain fully functional. Needs explicit provisioning/runtime/image/toolchain design, guest-path mappings, per-task lifecycle and native validation. Existing Linux Docker is not a Windows runtime. |
| Linux WSL/Docker/Hyperlight | Linux Docker available; others not exercised | Useful for an explicitly selected Linux environment. Does not preserve the requested native Windows workflow by default. |
| AppContainer/LPAC or global KnownDll ACL grants | Insufficient / rejected | Retain experiments as evidence; do not weaken isolation, change trust labels or escalate around system protection. |

The confirmed first implementation target is **native MXC**, with execution kept
in Windows on a separate supported acceptance environment. This is not
approval to upgrade this computer, remove 23H2 support or disable its existing
entries. If full 23H2 execution is required, select and provision the separate
Windows container/VM route instead. No automatic backend downgrade is acceptable.

#### Integration gates before implementation or availability claims

1. Tools.Abstractions owns an immutable task process scope: task identity,
   canonical authorized roots, explicitly resolved read-only runtime files and
   task-private temp/cache. Platform.Windows owns the SDK/native adapter. Existing
   Application, ToolExecutor and UI dependency directions remain unchanged.
2. Preserve lookup/schema/path/risk/approval, deadline/cancellation, bounded
   streaming output, artifact and audit order. Rootless tasks cannot start a
   workload. Extend process and terminal ports rather than invoking tools directly.
3. Require the effective request's approved native isolation tier at launch, not
   merely IsSupported or a startup-only Probe. Verify how the selected public API
   prevents weaker fallback and host DACL mutation, including capability changes
   between probe and launch. A preflight-only guard is insufficient.
4. The SDK README warns that RunAsync cancellation stops waiting while native
   execution continues. Use task-owned live-process termination/draining and prove
   late-spawn cleanup. Do not equate cancellation-token completion with process exit.
5. The reviewed SDK PTY convenience API lists IsolationSession on Windows, not
   ProcessContainer; IsolationSession requires unrestricted networking. Prove a
   ProcessContainer-compatible ConPTY integration or separately validate another
   backend. A successful piped command does not complete interactive terminal support.
6. Package includes mxc_ffi.dll and helper executables. Verify native asset loading,
   task ownership and helper lookup after Windows x64 self-contained single-file
   publishing, using JIT with AOT/trimming off. SDK AOT support changes no product rule.
7. Audit runtime/filesystem policy: Windows/SDK/Git dependencies, environment and
   credentials, multiple roots, links/junctions/reparse points, hardlinks, UNC,
   inherited handles and concurrent changes. No blanket user-profile/runtime grant.
8. Repeat the production-tool marker reproduction. Require inside success and
   outside denial, unchanged markers, read-only policy refusal, approvals,
   cancellation/timeout, independent tasks and process-tree cleanup. Cover Git
   hooks/children, MCP execution and ConPTY. Then run focused tests and a solution
   build, plus Release/single-file validation before release. Recovery never replays
   unknown side effects.

#### Confirmed route and evidence boundaries

On 2026-10-20 the user selected **native MXC on a separate Windows acceptance
environment which supports it**. This settles the backend priority; do not ask
the same route question again. It does not authorize upgrading this host,
provisioning a VM, changing ACLs, reusing consumed elevation permissions or
silently accepting AppContainerDacl. ConPTY integration remains a separate gate.

An ordinary-user repeat probe, without starting a workload, still reports
AppContainerDacl and BaseContainerApiPresent=false. Its output is preserved in
`artifacts/read-handle-safety-20261020/mxc-readonly-recheck.json`. The current host
therefore cannot supply native MXC acceptance evidence. Implementing and proving
the strict adapter requires the supported independent host; the command
filesystem-isolation blocker stays open.

The research diagnostic builds with zero warnings/errors. Its initial central
package-management conflict is retained in diagnostic-build.log; only the isolated
project disables central management, and the final build succeeds. Actual native
probe output, package signature log, source/runtime identities and host inventory
are retained under `artifacts/mxc-compatibility-20261019`.

No production source was edited by this research and the previous full 8,153-test
regression was not rerun. There is no new sandboxed-command, terminal, native GUI,
real-provider or Release command-isolation evidence. At this research checkpoint
the worktree-reference candidate was unintegrated. Its subsequent guarded
integration and separate runtime checks are recorded in
[WORKTREE_BASE_REFERENCE_ACCEPTANCE.md](GIT_WORKTREES.md#worktree-base-reference-acceptance); its historical statistics failure still
lacks the original error code and remains unclassified.

The next environment input is the actual supported acceptance host, not another
backend preference. Preparation is reviewable; changes to the supported execution
baseline or system provisioning require their own explicit scope and authorization.

The user subsequently confirmed that this independent host is not ready and asked
to continue local file safety work. Do not repeat backend/environment questions
or provision this machine. The read and atomic-write increments are recorded in
[READ_HANDLE_PATH_SAFETY_ACCEPTANCE.md](SECURITY.md#read-handle-path-safety-acceptance) and [WRITE_HANDLE_PATH_SAFETY_ACCEPTANCE.md](SECURITY.md#write-handle-path-safety-acceptance);
neither closes command confinement.
<!-- /source: WINDOWS_ISOLATION_BACKEND_DECISION.md -->


---

<a id="write-handle-path-safety-acceptance"></a>

## WRITE_HANDLE_PATH_SAFETY_ACCEPTANCE.md

<!-- source: WRITE_HANDLE_PATH_SAFETY_ACCEPTANCE.md -->
### 原子文本写入的目录句柄安全增量

日期：2026-10-20。继承 [READ_HANDLE_PATH_SAFETY_ACCEPTANCE.md](SECURITY.md#read-handle-path-safety-acceptance) 的读取边界；
这是 R02 的一部分，R01 和整个产品目标仍未完成。用户已确认原生 MXC 独立验收环境
尚未就绪，先继续本地安全修复。本轮没有提权、修改系统 ACL 或预置隔离环境。

#### 实现范围与入口

生产 `AppComposition` 注册 `IAtomicFileWriteSessionFactory` 的 Windows 实现，
由共享 `AtomicTextFileWriter` 使用。写文件、文本替换和复用该写入器的恢复操作
仍经过原有工具管线；没有新增旁路执行器。`DeleteAsync` 不在本增量覆盖范围。

运行链路：保存的模型 → Agent → ToolExecutor 审批/时限/取消 → 文件工具 →
原版本校验 → Windows 目录租约 → 相对目录创建暂存文件 → 写入及 SHA-256 →
前后快照 → 原版本复核 → 句柄内目标哈希/元数据确认 → 相对目录句柄的原子重命名 →
版本记录、工具结果与审计。App 测试贯通生产服务、真实文件及 SQLite，模型为本地模拟 HTTP。

- 从明确的本地固定 NTFS 卷开始，逐级打开授权根及目标父目录；保留不共享 Delete 的
  目录句柄到提交/清理结束。授权根必须已有，根内缺失的子目录可按父句柄创建。
- 每次相对打开前检查目录大小写策略；本增量保守拒绝大小写敏感目录，不按模糊的
  大小写匹配选择兄弟目录。常规 Windows 目录允许不同输入大小写。
- `NtCreateFile` 以父目录句柄和单个名称打开，使用 `OBJ_DONT_REPARSE` 与
  `FILE_OPEN_REPARSE_POINT`；返回的目录仍检查 Directory/ReparsePoint 属性。
  已有 junction、符号链接或其他重解析目录，即使指向根内，也不回退到字符串写入。
- 暂存文件独占打开，写入、哈希、后快照及提交使用同一文件句柄。后快照改为读取该流，
  不重新按临时路径打开；已有读句柄守卫仍在快照读取前执行。
- 提交通过 `NtSetInformationFile(FileRenameInformationEx)`，目的目录使用已持有的
  原生目录句柄。失败不退回 `File.Move`、`File.Replace` 或其他路径提交。
- 已有目标在最终阶段重新按目录句柄打开并哈希，保留读取句柄到重命名结束；申请
  Delete 权限，尊重外部读者的 Delete 共享限制，禁止在验证期间新增普通写句柄。
- 普通文件保留 owner、group、DACL 的访问规则和保护状态、创建时间及文件属性。
  Windows 可能重算 DACL 的 AutoInherited 控制位，不承诺安全描述符所有字节相同。
  只读、EFS 加密、稀疏、附加数据流或扩展属性文件明确拒绝替换，避免静默丢失数据。
  不声明所有 NTFS 特性或扩展元数据已经完整支持。
- 已有目标在创建暂存文件前先做元数据预检，最终提交仍复核，避免明知不支持时
  先写暂存内容。已知的安全拒绝通过 Tools.Abstractions 的类型化异常传递，工具管线
  显示本地化的操作建议并保留错误码，不透传任意原生异常。取消/超时优先于迟到拒绝。
- 未提交时通过暂存文件本身的句柄设置删除后关闭，再释放目录句柄；清理不解析路径，
  不因为撤权后失去路径权限而误删其他文件。若原生删除标记失败，只关闭句柄并保留
  临时文件供检查，不改为按名称删除。进程崩溃清理另见下方未验证范围。

仅覆盖本地固定 NTFS、常规非重解析且大小写不敏感的目录。UNC/SMB、ReFS、可移动卷、
卷挂载和无法证明的权限/目录形式会失败，不将其计为已支持。未注入工厂的底层兼容调用
仍采用原有实现；产品边界依赖生产 DI，不能将无工厂的测试调用当作安全写入。

#### 设计与代码审查自查

1. Tools.Abstractions 只定义写入会话/工厂端口；Tools.BuiltIn 管理工具流程；原生句柄、
   NTFS 元数据与系统调用均在 Platform.Windows；App 只注册依赖，依赖方向不变。
2. 使用已有写入器、上下文、版本/快照与工具契约。没有 SQL 变更、权限旁路或新恢复协议。
3. 原授权/审批先于文件工具；最终 Resolve 与授权列表变化检测保留，取消/超时、输出和
   审计仍由 ToolExecutor 统一处理。原生同步调用无法强制即时取消，不作相反保证。
4. 原生结构、目录租约、安全描述符、元数据和会话各自单文件单类型；结构审计检查
   单文件单类型、500 行文件/80 行方法硬限。
5. 门控反例及成功测试同时验证目录租约、临时文件独占、目标冲突、取消/超时、撤权、
   多根/并发、外部锁、真实 junction、元数据保留/拒绝、生产工厂接线和持久化审计。

#### 证据与解释

原始日志、TRX 和验证脚本位于 `artifacts/write-handle-safety-20261020/`。

- 最初两个目录探测虽然通过，但仅在打开的文件流期间尝试移动目录，不能复现窗口。
  随后将门控移至暂存结束后的授权枚举：`directory-red-gate.log` 和 TRX 确定性失败，
  证明旧实现可在该阶段移走父目录。该测试不是声称已经演示了根外写入。
- 接线后的测试证明暂存结束到提交之间，父目录及授权根不能被普通 Move 替换，
  提交后锁及时释放。原生提交调试中保留 sharing-violation 失败日志，不追认其为通过；
  最终采用保留目标验证句柄的 Ex 提交，并保留外部锁拒绝测试。
- 最终原生专项 39 项通过：实际 Windows API、本地 NTFS、临时 junction、真实附加流、
  扩展属性和文件权限；不属于原生 Avalonia GUI 验收。
- App 新增 6 项模型配置到文件写入/替换的检查：注册工厂确实被调用；真实工厂成功或
  明确拒绝时，文件、前后快照、对话结果及审计保存并可重新读取。不是付费提供商验收。
- 元数据预检拒绝的测试检查暂存回调没有运行；另一个实际附加流在会话创建后出现的
  反例证明最终提交仍然拒绝。生产 App 的两项元数据失败确认具体原因可见并持久化。

首轮 `verify-related.ps1` 的 Debug/Release 各 1,224 项通过：Tools 691、Windows 193、
App 相关集合 340，完整解决方案构建均零警告/错误。每配置 2,496 项源码/配置/脚本
输入前后无变化；终态见 `related-*-terminal.json`。这是加入元数据预检与类型化拒绝
之前的检查点，不将它称为最终代码的回归。

元数据/反馈审查修正后的结果由 `verify-final.ps1` 保存为 `final-*-terminal.json`，
两种配置各 910 项通过。随后仅调整新增并发测试的收尾顺序：先启动另一任务，再取消并
等待第一任务释放锁，避免随机路径落在同一锁分片时测试自身相互等待；不将其解释为
已发生的产品故障。最终源码用 `verify-accepted.ps1` 再次验证，终态为
`accepted-Debug-terminal.json`、`accepted-Release-terminal.json`。完整构建后运行 Tools/Windows 全套及精准选择的 App
写入、替换、撤销、审批、审计工作流；App 的具体过滤条件在脚本中。
**这些回归均不是整个解决方案测试全套，不把重叠测试累加为产品完成度。**

| 最终配置 | 完整解决方案构建 | Tools 全套 | Windows 全套 | App 精选工作流 | 合计 |
| --- | --- | --- | --- | --- | --- |
| Debug | 零警告、零错误 | 691 通过 | 197 通过 | 22 通过 | 910 |
| Release | 零警告、零错误 | 691 通过 | 197 通过 | 22 通过 | 910 |

各配置 2,497 项源码/配置/脚本输入前后无变化，两配置输入一致；没有新的完整 App
全套、原生 GUI、真实外部提供商或正式单文件包验收。上述测试含生产 DI、本地模拟
模型、真实本地文件/SQLite 与 Headless，不能以此代替这些未做的验收。

最终结构审计：2,304 个 C# 文件、20,786 个方法/访问器/lambda，最大文件 496 行、
最大方法 78 行，单文件单类型及硬规模限制零违规（`structure-audit-accepted.json`）。

#### 剩余缺口与详细下一步

1. **最终目录项并发替换。** 目标的最终 SHA-256 校验与文件名替换没有原子的
   compare-and-swap。为保留验证句柄完成替换需要 Delete 共享；拥有同目录重命名权限的
   外部进程仍可能在校验后替换目录项。此时原子替换不会通过该目录项解引用根外目标，
   但不能保证所有时序都保留并发新版本。需继续研究可验证的目录项条件提交/协作租约，
   用原生门控证明更换 file ID、删除后重建、同内容不同文件及链接替换；无法证明时
   显式冲突或失败，不能把再检查一次名称/哈希称为关闭该窗口。
2. **其他文件入口。** 为复制目的、移动/删除、目录枚举、artifact 目标及 Git 元数据
   分别建立同样的真实父目录替换反例，再按已有端口引入窄的句柄租约及相对操作。
   本增量的后快照读取同一流，但 artifact 的目标写入与清理仍是独立未修复边界。
   工具进入写入器之前的初次路径读取和元数据访问，也不由后建的写入会话追溯保护。
3. **持续授权。** 授权列表变化检测不是持久授权代际或即时撤权，也不能检测 ABA。
   Application 定义授权代际和任务资源租约，Infrastructure 向前迁移保留旧数据；
   审批和每次 I/O 边界复核，撤权取消所属资源。副作用未知时保留需手动核实状态。
4. **兼容性与安全元数据。** 分别准备独立 NTFS 大小写敏感目录、真实 UNC/SMB、ReFS、
   卷挂载、8.3、硬链接、压缩/加密、权限受限和多用户测试矩阵；不能通过放宽限制通过。
   先确定文件替换应保留的扩展元数据，再实现/拒绝及负例。当前普通目录大小写输入
   通过不代表大小写敏感目录或这些场景已经验证。
5. **中断与清理。** 常规取消/异常清理已经测试；创建子目录后取消可能留下空目录。
   原生同步调用及崩溃/强制终止可能留下暂存文件。应在独立子进程中逐阶段终止、
   重建服务并验证不自动续写；再设计仅清理可证明归属的遗留物，保留身份不明文件。
   本轮没有承诺崩溃后必然清理，也没有自动重放副作用。
6. **命令隔离和发布。** Shell、ConPTY、Git 子进程及 stdio MCP 的独立 I/O 不受此端口
   限制。保持用户选定的原生 MXC 路线，等待受支持独立环境；不自动降级/改系统配置。
   原生 GUI、真实外部服务、当前完整全套与正式 JIT 单文件包仍按总清单验收。
<!-- /source: WRITE_HANDLE_PATH_SAFETY_ACCEPTANCE.md -->
