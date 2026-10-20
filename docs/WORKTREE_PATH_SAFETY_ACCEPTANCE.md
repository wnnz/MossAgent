# Managed worktree path and repository boundaries

## Plan and required self-check

1. Infrastructure owns managed-path and Git metadata validation. Keep the
   Application Git port and App dependency direction unchanged; no UI filesystem
   access or native Windows interop is introduced.
2. Reuse `IGitWorktreeService`, `GitProcessRunner` and the existing owned process
   launcher. Git queries are bounded and mutations remain explicit application
   actions, not a shortcut around the model tool pipeline.
3. Validate canonical absolute paths, authorized repository scope, reparse/link
   ancestry and Git registration before mutation. Keep cancellation, a total
   30-second cooperative deadline, bounded output and ordinary non-force removal.
   Preserve partial/ambiguous directories and never delete a branch automatically.
4. Separate path policy, repository identity and inspection into one-type files;
   keep each handwritten file below 500 lines and each method below 80 lines.
5. Verify real temporary Git repositories, clean/dirty behavior, multiple roots,
   traversal/device aliases, junctions, foreign registration, malformed metadata,
   truncated discovery, cancellation and deterministic discovery/mutation changes.

## Intended behavior

- Managed targets must be direct GUID-named children of the configured worktree
  directory. Reject the root itself, nested paths, sibling-prefix escapes,
  device/stream aliases, ambiguous Windows names and linked ancestry.
- Do not create unrelated artifact/browser directories as a side effect of
  worktree creation. Validate before directory creation and revalidate before
  invoking a mutating Git command.
- The discovered repository top level must be in an explicitly authorized root;
  selecting only a nested folder cannot implicitly authorize the whole repository
  through a checkout. Explicit additional roots can authorize that repository.
- A standalone `.git` pointer cannot implicitly authorize external metadata.
  Separate Git directories outside the working root require an explicit additional
  authorization. Registered linked-worktree sources retain their existing
  backlink-validated common-directory support.
- Use explicit discovered repository arguments and a captured source HEAD commit
  for creation. A project rooted in a linked worktree must use its HEAD, not
  silently switch to the original repository's HEAD.
- Read bounded ordinary `.git`, `commondir` and registration backlink files before
  native discovery. Compare the returned root/common/private identities with
  that preflight identity. Require complete NUL-delimited worktree registration
  records; malformed, inconsistent or truncated output cannot authorize removal.
- Removal requires a registered linked worktree in the same Git common directory,
  with consistent top-level/private metadata and ordinary, non-linked paths.
  Never remove the project's own primary repository or an ancestor of it.
- Filesystem inspection and Git work run off the UI message pump. Cancellation
  retains process ownership until it returns; errors do not clear saved metadata
  or pretend a retained directory was removed.

## Validation

Initial candidate: `artifacts/worktree-path-safety-20261018`.
The first test build exposed two xUnit assertion-style diagnostics; after fixing
the fixture, the first boundary run passed 21/27. Four failures came from trying
to overwrite Windows-hidden `.git` files in negative-test setup. Two exposed a
real path-policy gap: Windows canonicalization had already consumed terminal
dots/spaces before the policy examined them. Setup now explicitly clears only
the test-owned pointer's attributes, and production validates raw components
before canonicalization. The next boundary run passed 41/41.

The solution subsequently built with zero warnings/errors. Its complete test
attempt was interrupted: exec handle 80765 disappeared and no candidate-owned
test process remained; `full-test-terminal.json` was not produced. Six completed
TRX reports record 1,896 passes and three failures, but App and Visual reports
are missing, so this is not a completed solution regression. Infrastructure
passed all 433 checks, including all 42 current worktree checks. Two Windows
Git ownership failures still expected a raw cancellation/timeout, contrary to
the retained-directory exception contract. Their fixture also rethrew that
expected recovery exception during disposal. The unrelated PTY failure is a
sharing violation reading its test-owned `child.pid`; no PTY code is changed.
`interrupted-run-observation.json` and the original reports preserve these facts.

Follow-up candidate: `artifacts/worktree-path-safety-final-20261018`.
The eight real Windows Git ownership checks pass after adapting assertions to
the typed retained-directory exception, its exact managed path and inner caller
token/timeout. Descendant-exit and absent-late-write assertions remain, and the
fixture accepts only these expected failures during cleanup. The service and
runner both receive the controllable deadline clock. This changes no production
process-launcher or tool approval behavior.

That candidate's complete App regression is still running on frozen assemblies.
Its pre-test source identity was checked unchanged before archiving all 2,064
source/configuration files to `tested-sources.zip`. Do not describe its result as
covering later changes.

A subsequent source review found a separate plain `.git` pointer gap: the claimed
work root could be authorized while its standalone metadata belonged elsewhere.
`artifacts/worktree-external-metadata-20261018/results/external-metadata-red.trx`
records two expected failures and one passing explicit-authorization control. A
real temporary Git repository successfully created a worktree through the
unauthorized external pointer before the fix; a recording launcher also proved
native discovery was reached. Production now checks that standalone metadata
scope before launching Git. Explicit metadata authorization is supported; removing
that authorization also blocks cleanup before native discovery and retains the
existing worktree. Registered linked-source HEAD behavior remains covered.

## Current candidate terminal checks

Candidate: `artifacts/worktree-external-metadata-20261018`.

| Check | Terminal result |
| --- | --- |
| Complete Infrastructure suite | 437/437 passed, including 46 worktree cases |
| Related App worktree/preparation/environment suite | 69/69 passed |
| Real Windows Git process ownership suite | 8/8 passed |
| Complete solution build | 0 warnings, 0 errors |
| Changed/untracked handwritten C# shape | 219 files; no violations |
| Changed AXAML shape | 10 files; maximum 499 lines |

The three current test runs cover 514 distinct cases. The 46 worktree cases are
already included in Infrastructure, not an additional run to add to that total.
Handwritten C# remains one type per file; the maximum file is 499 lines and the
maximum method is 72. `results/current`, the build/test terminal JSON files and
the source/binary identity manifests identify the tested candidate. The initial
negative pointer report and previous candidates are retained unchanged.

This candidate has not completed a new full-solution regression. The preceding
candidate's pending complete run uses older assemblies and cannot establish that
the later external-pointer fix passed every suite. Headless App, real disposable
Git/SQLite and owned Windows hook subprocess evidence are not an EXE restart,
real-provider run or user-window acceptance. No user window, credential, approval
setting, commit, push or release was changed. These records do not establish
complete worktree safety or overall Codex feature parity.

## Explicit remaining limits

- Portable ancestry checks and explicit Git identity reduce exposure but do not
  pin directories with native handles. Hostile replacement during a native Git
  operation is not proven safe; handle-level race protection remains required.
- Restored task tool/panel authorization needs a separate end-to-end audit; this
  creation/removal service is not proof that every persisted worktree path is
  validated before every read or command.
- This is not a sandbox for arbitrary Git hooks, configuration/includes or object
  alternates. A user-authorized Git repository can run hooks during mutation.
- Native restart, hard-crash recovery during creation and branch/base UI selection
  remain separate unfinished work. Ordinary cleanup deliberately retains branches;
  automatic branch deletion is not implemented.
