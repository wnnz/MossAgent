# Model picker optimization acceptance

## Design self-check

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

## Verification

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

## Incremental search follow-up, 2026-10-12

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
