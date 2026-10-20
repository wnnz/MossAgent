# Confirmed single-branch Git push

## Plan and required design self-check

1. Tools.BuiltIn owns separate local-only preview and side-effecting push tools,
   query/state and endpoint validation. Tools.Abstractions owns immutable preview
   data and a local-destination authorization port; Infrastructure implements
   bare/non-bare destination inspection with existing Git path checks. App owns
   confirmation and task selection, never filesystem/process access.
2. Reuse the owned Git runner and standard schema/capability/task-guard/approval/
   deadline/cancellation/output/audit pipeline. No shell or direct tool bypass.
3. Preview binds source repository/HEAD/branch, complete bounded Git configuration,
   configured remote, effective single push URL, target branch and local target
   identity. Reauthorize and compare after approval. Push the captured commit ID
   to exactly one heads ref; explicitly disable force/mirror/prune/follow-tags,
   recursive submodule pushes and implicit push options. Keep pre-push hooks and
   signing/credential configuration. Never auto-commit, retry, force, push tags,
   update upstream configuration or use a user remote in tests. Multiple push
   URLs/custom remote helpers are explicit unsupported errors, not hidden success.
4. Permit standard HTTP(S), SSH/SCP, git and explicitly authorized local file
   transports. Reject other helpers/protocols and path aliases/reparse points.
   Raw configuration, credential-bearing URLs and Git push stdout/stderr stay out
   of tool output/audit/UI; display only a sanitized transport host/local label.
   This is not an OS sandbox for configured hooks/credential helpers or a native
   handle/network-race guarantee. Cancellation may follow a successful remote
   mutation; inspect before any explicit retry.
5. Keep one type per C# file and files/methods within limits. Verify real pushes
   against temporary bare repositories: exact captured commit/target, dirty and
   unselected data excluded, non-fast-forward rejection, denied approval/ReadOnly,
   approval-time state/URL/path changes, secret suppression and owned cancellation.
   Verify production inspector, task/input isolation, themes/keyboard, focused
   regression and a solution build. No user window, credentials or paid API.

## Status

The tools, destination port and inspector confirmation are implemented. Current
focused terminal reports pass 48 Tools (`git-push-4.trx`) and 35 App cases
(`git-push-app-4.trx`) without failures/skips. These overlap the upcoming related
regression and must not be added to its total. The complete solution build
passes with zero warnings/errors. Session 40586 is terminal with exit 0 and all
six Completed reports: 613 Tools, 442 Infrastructure, 140 Agent, 1,966 related App,
300 Visual and 146 Windows cases, totaling 3,607/3,607 without failures/skips.
This is related regression, not all solution tests or native/live-remote acceptance.
The 320-file C# type/size and 16-file AXAML parse/size audits pass. Final light/dark
rendered dialog artifacts were inspected separately from native-window behavior.

The terminal gate confirms all 2,187 source/configuration and 2,439 runtime file
hashes/path sets agree with their pre-test identities and source ZIP contents.
Snapshot SHA-256: `407C0C9EFEC56E52BC5F3DD3BCCAB260332BD0534A8F8E03F83A4986173E6B9B`.
The candidate is frozen and must not be rebuilt. Subsequent source changes need
a separate candidate and are not covered by this result. Original red reports
remain in the candidate; no development-worktree commit, push or release occurs.

Push is a coding-workflow slice, not PR, remote configuration/credential UI,
native/restart or whole-product completion. Tracking configuration remains a
separate unimplemented workflow; ordinary successful push may update Git's
remote-tracking refs. Local destinations containing alternate object stores or
links anywhere in bounded objects/refs/logs trees are explicitly rejected.
Standard network parsing is deterministic, not a live remote-authentication test.

## Evidence and fixes

- A real temporary repository exposed `remote.origin.mirror=true` overriding
  `--no-mirror` and failing with `--mirror can't be combined with refspecs`.
  Process-local `-c remote.<selected>.mirror=false` corrects this without changing
  configuration; broad tag/submodule defaults and extra refs remain excluded.
- The first production AppComposition test failed to resolve the concrete
  repository authorization dependency. Destination authorization now consumes
  the already-registered `IGitRepositoryAuthorization` port. The same real
  inspector test then passes through SQLite, shared controls, keyboard preview
  refresh/confirmation, Git push and audit. It verifies exact target commit and
  committed bytes, excludes dirty/staged bytes, retains the chat draft, refreshes
  actual diff status and returns focus. This is Headless, not native-window QA.
- Endpoint tests first fail three cases: undefined `:-1` ports on SSH/git labels
  and query-like SCP host text leaking into the label. Standard server-only URI
  formatting and SCP host validation fix these while suppressing userinfo,
  query/path values and raw failure output. Original red reports are retained.
- Source/destination changes during approval, pre-push hook refusal, object-tree
  junctions before/after preview, detached HEAD/file URI, tracking defaults,
  ReadOnly/denial/revoked guard, non-fast-forward history, cancellation/deadline,
  cleanup/audit and oversized output/configuration have focused coverage.
- App tests cover selection edits, delayed previews, task isolation/root snapshots,
  actual tool/refresh cleanup, secret-free errors and atomic busy/close ordering.
  Shared dialog keyboard/focus/bounds checks cover light/dark at 280/640 DIP widths
  and 560/700 DIP heights, with ready/busy/interrupted/failed rendered artifacts.
  Representative renders were inspected; no user window or remote was operated.
