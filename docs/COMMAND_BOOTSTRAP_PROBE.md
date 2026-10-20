# Restricted-token runtime bootstrap diagnostic

## New evidence (2026-10-19)

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

## Prepared experiment and one authorized preflight attempt

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

## Checks completed without elevation

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
`COMMAND_FILESYSTEM_ISOLATION_PLAN.md`.

Follow-up `inspect-runtime.exe` performs only unelevated reads of the directory,
ntdll and kernel32 section DACLs/mandatory labels. Its own token is recorded as
unelevated/medium (integrity 8192). All three permit READ_CONTROL and deny its
WRITE_DAC requests; the retrieved label data does not establish a system-integrity
write barrier. This does not explain the earlier RunAs failure. The helper never
sets an ACL or requests elevation; see `runtime-object-access.log`. Any further
setup design must identify the exact failed handle/token before requesting wider
privileges. No permanent runtime broker has been implemented.

## Read-only elevation check did not start

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

## Separately authorized read-only preflight: confirmed elevated refusal

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
