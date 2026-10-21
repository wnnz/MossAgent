# Windows command isolation: backend and host decision

Status: design/evidence checkpoint, 2026-10-19. Production commands remain
unconfined outside their authorized working directory. No SDK adapter has been
integrated, and no platform requirement has been changed. The goal stays active.

## Current host, measured without elevation

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

## Public MXC integration is available, native isolation is not available here

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

## Backend comparison and proposed direction

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

## Integration gates before implementation or availability claims

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

## Confirmed route and evidence boundaries

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
`WORKTREE_BASE_REFERENCE_ACCEPTANCE.md`; its historical statistics failure still
lacks the original error code and remains unclassified.

The next environment input is the actual supported acceptance host, not another
backend preference. Preparation is reviewable; changes to the supported execution
baseline or system provisioning require their own explicit scope and authorization.

The user subsequently confirmed that this independent host is not ready and asked
to continue local file safety work. Do not repeat backend/environment questions
or provision this machine. The read and atomic-write increments are recorded in
`READ_HANDLE_PATH_SAFETY_ACCEPTANCE.md` and `WRITE_HANDLE_PATH_SAFETY_ACCEPTANCE.md`;
neither closes command confinement.
