# Verified PR inline comments and keyboard location

## Implemented workflow

The authorized-repository PR inspection page now connects the loaded file and
inline-comment pages. After explicitly fetching both, a current-head comment on
an exact file path and a verified LEFT/RIGHT patch line appears below that line
in the shared code preview. Its location button selects the file, switches back
to changes, scrolls the card into view and focuses the actual card. Repeated
activation of the same comment can locate it again without another HTTP request.
Same-side multi-line comments require the entire range to be present in loaded
patch hunks and attach to the verified end line; their list label retains the
start and end. Context lines retain distinct old/new line numbers.

Only the two currently loaded pages participate. Historical commits, missing
line/side metadata, unloaded or case-different paths, renamed previous paths,
unavailable/incomplete patches, absent lines, gaps in multi-line context and
cross-side ranges do not receive guessed anchors. Their text and an explicit
localized explanation remain available in the inline list. No files are fetched
implicitly, and comment content is untrusted selectable text, not executable
markup, a model instruction or a URL to follow.

## Design and safety self-check

1. Domain owns `PullRequestPatchMap`, hunk validation and comment-location states.
   Infrastructure reuses the map when validating file responses and preserves
   optional start-line/side metadata in its bounded comment parser. App owns
   page reconciliation and presentation through shared controls and styles;
   it performs no HTTP, SQL, filesystem or process operation for location.
2. The existing source read ports, five exact read tools, common executor and
   revision-bound source scope remain the only remote read path. This adds no
   tool, credential storage, database schema, directory grant or source write.
3. Existing approval, thirty-second read deadlines, cancellation, bounded output,
   audit and before/after revision/grant checks remain unchanged. Patch validation
   limits are 32768 characters and 1000 newlines per patch, with the existing
   per-page budget. ASCII hunk counters reject malformed, overlapping and
   overflowing ranges; additions/deletions must match the complete patch.
4. Collection changes, file/comment pagination, selection, scope replacement,
   observed head/grant changes, leaving and disposal clear or rebuild markers.
   Generation checks invalidate saved navigation commands. The preview applies
   annotations only in diff mode, preserves ordinary search behavior, keeps
   cards in the horizontal viewport and focuses only a currently visible target.
   Subscriptions are detached on disposal. Handwritten types remain separate,
   with shared appearance variants rather than per-view constants.
5. Tests cover exact side/context/range mapping and refusal cases, production
   response parsing through view-model location, real Headless keyboard Space
   activation and focus, stale pages/commands, observed head/revocation changes,
   ordinary-preview isolation and long-code horizontal scrolling.

## Candidate and intermediate evidence

Current candidate: `artifacts/pr-inline-comments-20261018`. The earlier
`artifacts/pr-inspection-20261018` remains untouched historical evidence; its
source/runtime hashes and 1982 passes do not validate this implementation.

- `infrastructure-focused-current.trx`: 82 passed, zero failures/skips.
- `app-focused-current.trx`: 59 passed, zero failures/skips, including the complete
  multi-line metadata-to-location chain. These focused totals are not added to
  the later regression total.
- `app-scroll-focused-third.trx`: two LEFT/RIGHT cases passed. Targets initially
  lie below the viewport, two cards share the target line, and both first and
  repeated location must scroll and focus the exact live selected card fully
  within the viewport. The first attempt used an obsolete bitmap-save overload;
  the second compared a detached pre-selection template instance. The corrected
  assertion resolves the current visual card and retains the scroll/focus checks;
  those earlier build/failure logs are preserved, not counted as navigation bugs.
- An early fixture incorrectly treated a shortened context-line body as an
  incomplete patch; removing the entire context row correctly tests incomplete
  hunk shape. The initial failing report is retained.
- Initial Headless frames exposed an inline-list visibility binding evaluated
  against the wrong child data context. A parent-context visibility container
  corrected the implementation, and a focused effective-visibility assertion
  now covers it. Initial frames are not final visual evidence.
- Compact light and ordinary dark `ui-focused-second` frames were visually
  inspected: the duplicate list is hidden, the verified card and target line are
  visible, and focused text remains readable. These are component Headless
  frames, not native screenshots or whole-product visual acceptance.

## Terminal validation

- Complete solution Debug build succeeded with zero warnings/errors in
  `build-final.log`. It includes the final offscreen-navigation tests.
- Five disjoint terminal reports all completed with process exit code zero:
  Tools 644, Infrastructure 587, Agent 146, related App 354 and visual-component
  300: **2031 passed, zero failures/skips**. This is not all App or all solution
  tests; focused and earlier-candidate results are not added to this total.
- App's selected regression took 5 minutes 7 seconds. The original live session
  was awaited to completion without cancellation or restart; observation waits
  were not interpreted as failures. Visual regression took 1 minute 37 seconds.
- All 2263 selected source/root configuration files and 1426 selected test-runtime
  files retain their actual pre-test SHA-256 identities and exact path sets.
  Scope is recorded in `validation-terminal.json` and `validate-current.ps1`:
  Git-listed `src`/`tests` plus present root engineering/build configuration, and
  all files under the five test projects' `bin/Debug` directories. Docs and App's
  standalone output are not covered by these manifests. This is identity
  verification, not a restorable source/runtime archive; no ZIP was created.
- A Roslyn audit of 442 present changed/untracked C# files, including 2535
  methods/accessors, found no single-type, file-size or method-size violations;
  the largest inspected method/accessor is 78 lines. All 26 changed/untracked
  AXAML files satisfy the hard file limit. `git diff --check` passes.
- `ui-terminal` contains 28 Headless frames: 16 PR/annotation frames and 12
  source-account regression frames. Compact-light and ordinary-dark inline
  cards, the LEFT offscreen target and compact-light/ordinary-dark full PR pages
  were visually inspected. The focused target is fully visible after actual
  scrolling, and the inline list is not duplicated in the patch section. Other
  frames were generated but are not claimed to have been manually inspected.

## Remaining scope

This is the frozen inline-comment candidate's ledger. Subsequent schema-20
snapshot review chat and investigation-draft work is recorded separately in
`SOURCE_PR_REVIEW_CHAT_ACCEPTANCE.md`; the 2031 results above do not validate that
later source or change the original evidence scope.

The complete goal is still active. Inbox/URL/filter workflows, reviews/activity,
review chat, investigation drafts, pin/viewed revision persistence, related
stacks, confirmed remote feedback/creation, OAuth, GitLab and cloud review remain
open. Cross-side/historical comments are readable but not current-patch anchors;
this is not full historical diff navigation or a complete multi-line highlight.
Separate reads and revision rechecks are not server-side snapshot isolation and
cannot detect an unseen ABA revision change or guarantee comment immutability.

Evidence uses synthetic credentials, controlled HTTP, temporary SQLite and
Headless rendering. Real GitHub accounts/permissions, production TLS, native
high-DPI input, user windows, actual EXE restart and Release packaging are not
validated here. No commit, push or release is part of this slice.
