# Windows command filesystem isolation

## Confirmed blocker (2026-10-19)

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

## Independent feasibility experiment

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
  `COMMAND_BOOTSTRAP_PROBE.md` records a prepared, hash-bound diagnostic for 44
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
  raw ACE bytes independently confirm the labels. See `COMMAND_BOOTSTRAP_PROBE.md`
  and `artifacts/runtime-trust-label-20261019/verified-observations.json`.
  This rejects the volatile KnownDll ACL lease route on this host; ordinary
  elevation/ownership assumptions cannot justify production setup. No trust-label
  bypass, SYSTEM/PPL workaround or further elevated attempt is authorized or
  implemented. A supported isolation backend needs separate design/validation,
  retaining Windows command compatibility and explicit setup requirements.

The experiment lacks production job/pipes/ConPTY, multiple-root leases, crash
recovery, comprehensive SDK/Git compatibility and network acceptance. Unsuccessful
logs remain preserved. It must not be described as production command isolation.

## Design gate and required self-check

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

## Implementation sequence

Current supported-backend comparison and native SDK probe evidence are in
`WINDOWS_ISOLATION_BACKEND_DECISION.md`. This host is Windows 11 23H2 22631.3880;
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
