# Production subagent concurrency acceptance

## Scope and candidate

This closes the current two-task native subagent cancellation/restoration gate,
not complete-product acceptance or same-state Codex pixel equivalence. No
production behavior, test assertion, timeout, approval setting or packaging is
changed for this branch.

The ordinary-permission production App uses the existing final Debug/JIT
Windows x64 candidate under `.scratch/rail-navigation-final-20261015`. Its App
DLL SHA-256 remains
`19564864E0806BD8CD4B4EDFF64E34D493808BF6715276CA9EE4ED2006368458`.
Both native processes and the subsequent solution build use that identity.

Evidence and Python helpers remain outside Git at
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-child-concurrency-20261015`.
The helpers passively capture the exact PID/executable/HWND, compose owned
popups, reject black images and record DPI/elevation. All Windows input targets
only the independent MossAgent instance through supported Computer Use APIs.
Codex/ChatGPT, user data and permission prompts are not operated.

## Isolated scenario

An external seed uses the formal SQLite repositories to create two separate
projects and completed synthetic tasks, each initially FullAccess. The tasks
have distinct authorized roots, parent model preferences and context markers;
no existing task's policy is changed. Each root owns `evidence.txt` and
`continued.txt`; a fifth file belongs to an unauthorized foreign directory.

The same saved child profile supplies its name, trigger, system prompt,
`gemini-3-pro-preview`, high effort and enabled context sharing. A loopback-only
model fixture supplies Responses parent and Gemini child frames; actual file
reads, execution auditing, cancellation, persistence and UI projection use
production components. Only synthetic keys and allowlisted request flags/model
parameters are used; no headers, raw requests or credentials are logged.

Unlike the older sequential fixture, request counts are owned by each
task/parent/child route, and every emitted tool-call identity is distinct. The
provider normalizes Gemini child call IDs; audit records retain each unique
child ID beneath its own parent-call prefix.

## Native execution and isolation

1. Real composer input and the send button start A. Its child performs an
   authorized file read and receives its own file marker before holding a
   second streaming response.
2. Task switching and a second real send start B while A remains running.
   Both tasks are Running, each has one live delegation and one successful
   read audit, and each child receives only its own context and file result.
3. Selecting A and clicking its actual Stop button cancels only A. Its task
   becomes Cancelled, its delegation completes as `tool_cancelled`, and the
   fixture observes A's child connection disconnect. B stays Running with its
   delegation open and without another parent/child request.
4. Expanding and scrolling A's execution history shows the successful read and
   `CHILD-A-PARTIAL-819`. The saved child entries are no longer partial/running;
   cancellation does not discard previously received dialogue.
5. Releasing only B's synthetic response gate causes another real authorized
   read of B's `continued.txt`, then its final child answer and parent answer.
   B becomes Completed while A remains Cancelled. The child Markdown/code block
   and the separate parent answer are reached by actual disclosure and scrolling.

There are eight model POSTs: A parent/child 1/2 and B parent/child 2/3. A ninth
allowlisted log entry records A's disconnect, not another request. Five unique
audits comprise two delegations and three file reads; all finish with timestamps
and FullAccess policy approval. Four succeed, and A's delegation alone is
cancelled. A never executes its second read or reaches a final answer.

Request flags verify the selected parent model/medium effort, selected child
model/HIGH thinking, saved profile prompt, parent trigger and shared own-context.
Foreign context, project root, file marker and foreign-directory marker are
absent. Persisted messages and call prefixes also remain task-owned. All five
synthetic file hashes remain unchanged and artifact count is zero. This is
positive isolation evidence, not a new native traversal-denial test or evidence
of a user manually approving a prompt.

## Restore, responsive presentation and cleanup

The initial App PID 13488 exits normally with code zero. Restarting the same
data as PID 7112 restores cancelled A and completed B. Actual task switching,
disclosure and child-pane scrolling recover A's partial reply, B's two read
results, child Markdown/code block, parent answer and unchanged 52-second duration.
No send or continuation is requested after restart.

All nineteen table hashes, messages, audits, request records and file hashes are
identical to the completed pre-close snapshot, before restored input, after
expansion, after compact inspection and after final close. Only task state,
messages and execution audits differ from the before-input baseline; sixteen
other tables remain unchanged. The initial pre-launch snapshot helper queried
the wrong audit column (`tool_call_id` instead of `call_id`) and failed before
writing evidence. It is retained as a helper diagnostic; only the corrected
before-input baseline is claimed, not a successfully captured pre-launch seed.

Sixteen passive Python captures cover Dark/96-DPI maximized 1920x1032, normal
1280x820 and minimum 526x601 windows:

- `00` shows idle B while A is already running in the sidebar; its filename is
  not evidence that A's selected dialogue is displayed.
- `01`–`02` show both tasks running with B and A selected respectively.
- `03`–`06` show A cancellation, retained partial dialogue and unaffected B.
- `07`–`08` show B completion and its post-cancellation read/final child answer.
- `09`–`12` show restarted collapsed/expanded A/B history and final child code.
- `13`–`15` show normal/compact history, including the actual project-context
  toggle and child-pane scrolling needed to expose the entire final code block.

Both App processes exit zero through normal window-close actions, with empty
stderr, no surviving native windows and ordinary permissions. The loopback
fixture exits zero and releases its port. One idle HTTP keep-alive read reports
`ConnectionAbortedError`/WinError 10053 after normal App shutdown; this external
fixture diagnostic is retained, not described as an App failure or empty fixture
stderr. A's separately recorded live-stream disconnect establishes cancellation.
Some immediate Sky accessibility snapshots lag behind the already updated
pixels; actions use fresh observations rather than treating stale trees as bugs.

`verify.py` independently checks task/call ownership, model/profile/context flags,
successful/cancelled dialogue, eight requests, five audits, stable nineteen-table
restore, unchanged files, zero artifacts, both normal exits, released endpoint,
and all sixteen non-black captures. `native-verification.json` reports success
while retaining `FullProductComplete=false` and `FullTestRunAllGreen=false`.

## Regression and remaining gates

The current candidate passes 124 focused App subagent cases and the complete
119-case Agent suite: 243 fresh cases without adding the earlier full-run counts.
Another complete solution build has zero warnings/errors. Native/test dependency
identities remain unchanged after that build; repository whitespace checks pass.
No new product behavior requires an additional test implementation in this
acceptance-only branch; existing deterministic lifetime/gateway/approval tests
remain the regression boundary.

The preceding complete solution test run remains 6,111/6,115 with four failures;
its isolated passing reruns and these 243 cases do not turn it all-green. That
record remains in `SETTINGS_ENTRY_ACCEPTANCE.md`. A fresh complete all-green run
is still a product completion gate.

No real remote provider, manual-approval success, high-DPI transition or complete
current-version theme/size/state sweep is established. Native editor-save/sharing
off/on evidence remains in `SUBAGENT_SETTINGS_TO_RUNTIME_ACCEPTANCE.md`; this
branch seeds settings and does not claim to repeat that editor workflow. Current
Codex same-state visual references, residual UI fidelity and whole-product
acceptance remain open. Nothing is staged, committed, pushed, released or installed.
